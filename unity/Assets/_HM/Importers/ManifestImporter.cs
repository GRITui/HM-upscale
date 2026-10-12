using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace HM.Importers
{
    public sealed class ImportReport
    {
        public readonly List<string> Imported = new List<string>();
        public readonly List<string> Errors = new List<string>();
        public bool Ok => Errors.Count == 0;
    }

    /// <summary>
    /// Reads the converted-assets manifest and builds Addressables groups and prefabs from it.
    /// Files are copied under the destination as {type}/{id}.{ext}, so original file names never reach the project.
    /// </summary>
    public static class ManifestImporter
    {
        public const string ManifestFileName = "manifest.json";
        public const string DefaultDestination = "Assets/_HM/Imported";
        public const string DefaultRelativeFolder = "assets/hm-raw/converted";

        /// <summary>HM_CONVERTED env var, else EditorPrefs value, else ../assets/hm-raw/converted relative to the repo root.</summary>
        public static string ResolveConvertedRoot()
        {
            string env = Environment.GetEnvironmentVariable("HM_CONVERTED");
            if (!string.IsNullOrEmpty(env)) return Path.GetFullPath(env);
            if (!string.IsNullOrEmpty(ImportSettings.Root)) return ImportSettings.Root;
            string repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            return Path.Combine(repoRoot, DefaultRelativeFolder);
        }

        [MenuItem("HM/Importers/Import Converted Assets")]
        public static void ImportFromMenu()
        {
            var report = Import(ResolveConvertedRoot());
            foreach (var e in report.Errors) Debug.LogError("HM import: " + e);
            Debug.Log($"HM import: {report.Imported.Count} imported, {report.Errors.Count} errors");
        }

        /// <summary>Batch entry: -executeMethod HM.Importers.ManifestImporter.ImportBatch</summary>
        public static void ImportBatch()
        {
            var report = Import(ResolveConvertedRoot());
            foreach (var e in report.Errors) Debug.LogError("HM import: " + e);
            EditorApplication.Exit(report.Ok ? 0 : 1);
        }

        public static ImportReport Import(string convertedRoot, string destination = DefaultDestination)
        {
            var report = new ImportReport();
            string manifestPath = Path.Combine(convertedRoot, ManifestFileName);
            if (!File.Exists(manifestPath)) { report.Errors.Add("manifest not found: " + manifestPath); return report; }

            AssetManifest manifest;
            try { manifest = AssetManifest.Parse(File.ReadAllText(manifestPath)); }
            catch (Exception e) { report.Errors.Add("manifest invalid: " + e.Message); return report; }
            report.Errors.AddRange(manifest.Validate());
            if (!report.Ok) return report;

            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            AssetDatabase.StartAssetEditing();
            var copied = new List<(ManifestEntry e, string assetPath)>();
            try
            {
                foreach (var e in manifest.assets)
                {
                    string src = Path.Combine(convertedRoot, e.path);
                    if (!File.Exists(src)) { report.Errors.Add($"{e.id}: file missing '{e.path}'"); continue; }
                    string dir = $"{destination}/{e.type}";
                    Directory.CreateDirectory(dir);
                    string dest = $"{dir}/{e.id}{Path.GetExtension(e.path).ToLowerInvariant()}";
                    File.Copy(src, dest, true);
                    if (e.Kind == AssetKind.Model) CopyGltfSidecars(src, dir, report, e.id);
                    copied.Add((e, dest));
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            foreach (var (e, assetPath) in copied)
            {
                try
                {
                    string addressable = assetPath;
                    if (e.Kind == AssetKind.Model)
                    {
                        addressable = MakePrefab(e.id, assetPath, destination);
                        if (addressable == null) { report.Errors.Add($"{e.id}: glTF did not import"); continue; }
                    }
                    var group = GetOrCreateGroup(settings, "HM_" + Capital(e.type));
                    var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(addressable), group, false, false);
                    entry.SetAddress(e.id);
                    foreach (var t in e.tags ?? new string[0]) { settings.AddLabel(t, false); entry.SetLabel(t, true, false, false); }
                    report.Imported.Add(e.id);
                }
                catch (Exception ex) { report.Errors.Add($"{e.id}: {ex.Message}"); }
            }
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true);
            AssetDatabase.SaveAssets();
            return report;
        }

        /// <summary>Removes a destination folder and the HM_ groups; used by tests.</summary>
        public static void Clean(string destination = DefaultDestination)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            if (settings != null)
            {
                foreach (var g in new List<AddressableAssetGroup>(settings.groups))
                    if (g != null && g.Name.StartsWith("HM_")) settings.RemoveGroup(g);
            }
            if (AssetDatabase.IsValidFolder(destination)) AssetDatabase.DeleteAsset(destination);
        }

        static string MakePrefab(string id, string modelPath, string destination)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null) return null;
            string dir = destination + "/prefab";
            Directory.CreateDirectory(dir);
            string prefabPath = $"{dir}/{id}.prefab";
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = id;
            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            UnityEngine.Object.DestroyImmediate(instance);
            return prefab != null ? prefabPath : null;
        }

        static void CopyGltfSidecars(string srcGltf, string destDir, ImportReport report, string id)
        {
            if (!srcGltf.EndsWith(".gltf", StringComparison.OrdinalIgnoreCase)) return;
            string text = File.ReadAllText(srcGltf);
            foreach (Match m in Regex.Matches(text, "\"uri\"\\s*:\\s*\"([^\"]+)\""))
            {
                string uri = m.Groups[1].Value;
                if (uri.StartsWith("data:") || uri.Contains("..") || uri.StartsWith("/")) continue;
                string side = Path.Combine(Path.GetDirectoryName(srcGltf), uri);
                if (!File.Exists(side)) { report.Errors.Add($"{id}: glTF references missing file '{uri}'"); continue; }
                string target = Path.Combine(destDir, uri);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(side, target, true);
            }
        }

        static AddressableAssetGroup GetOrCreateGroup(AddressableAssetSettings settings, string name)
        {
            var g = settings.FindGroup(name);
            if (g != null) return g;
            return settings.CreateGroup(name, false, false, false, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
        }

        static string Capital(string s) => char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}

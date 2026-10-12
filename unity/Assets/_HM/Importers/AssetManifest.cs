using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace HM.Importers
{
    public enum AssetKind { Model, Texture, Audio, Data }

    [Serializable]
    public sealed class ManifestEntry
    {
        public string id;
        /// <summary>model | texture | audio | data</summary>
        public string type;
        /// <summary>Path relative to the converted folder.</summary>
        public string path;
        public string[] tags = new string[0];
        /// <summary>Licensing note, for example "original-placeholder" or "user-owned-copy". Required.</summary>
        public string license;

        public AssetKind Kind => ParseKind(type);

        public static AssetKind ParseKind(string t)
        {
            switch (t)
            {
                case "model": return AssetKind.Model;
                case "texture": return AssetKind.Texture;
                case "audio": return AssetKind.Audio;
                case "data": return AssetKind.Data;
                default: throw new FormatException("unknown asset type '" + t + "'");
            }
        }
    }

    /// <summary>Asset manifest written by the converters next to their output. Schema: docs/adr/0004-asset-manifest.md.</summary>
    [Serializable]
    public sealed class AssetManifest
    {
        public const int CurrentSchema = 1;
        static readonly Regex IdPattern = new Regex("^[a-z][a-z0-9_]{0,63}$", RegexOptions.Compiled);

        public int schema = CurrentSchema;
        public ManifestEntry[] assets = new ManifestEntry[0];

        public static AssetManifest Parse(string json)
        {
            var m = JsonUtility.FromJson<AssetManifest>(json);
            if (m == null) throw new FormatException("manifest did not parse");
            if (m.schema < 1 || m.schema > CurrentSchema) throw new FormatException("unsupported manifest schema " + m.schema);
            m.assets ??= new ManifestEntry[0];
            return m;
        }

        /// <summary>Returns human readable problems; empty means valid.</summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            var seen = new HashSet<string>();
            foreach (var e in assets)
            {
                string who = string.IsNullOrEmpty(e.id) ? "<no id>" : e.id;
                if (string.IsNullOrEmpty(e.id) || !IdPattern.IsMatch(e.id))
                    problems.Add($"{who}: id must match ^[a-z][a-z0-9_]{{0,63}}$");
                else if (!seen.Add(e.id)) problems.Add($"{who}: duplicate id");

                AssetKind kind = AssetKind.Data;
                bool kindOk = true;
                try { kind = e.Kind; } catch (FormatException ex) { problems.Add($"{who}: {ex.Message}"); kindOk = false; }

                if (string.IsNullOrEmpty(e.path)) { problems.Add($"{who}: path required"); continue; }
                if (e.path.StartsWith("/") || e.path.Contains("..") || e.path.Contains("\\") || e.path.Contains(":"))
                    problems.Add($"{who}: path must be relative, forward slashed, without '..'");
                else if (kindOk && !ExtensionOk(kind, e.path))
                    problems.Add($"{who}: extension of '{e.path}' does not match type '{e.type}'");
                if (string.IsNullOrWhiteSpace(e.license)) problems.Add($"{who}: license required");
                if (e.tags != null)
                    foreach (var t in e.tags)
                        if (string.IsNullOrEmpty(t) || !IdPattern.IsMatch(t)) problems.Add($"{who}: bad tag '{t}'");
            }
            return problems;
        }

        public static string[] ExtensionsFor(AssetKind k)
        {
            switch (k)
            {
                case AssetKind.Model: return new[] { ".gltf", ".glb" };
                case AssetKind.Texture: return new[] { ".png" };
                case AssetKind.Audio: return new[] { ".ogg" };
                default: return new[] { ".json" };
            }
        }

        static bool ExtensionOk(AssetKind k, string path)
        {
            string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            return Array.IndexOf(ExtensionsFor(k), ext) >= 0;
        }
    }
}

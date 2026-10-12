using UnityEditor;
using UnityEngine;

namespace HM.Importers
{
    /// <summary>
    /// Per-machine location of converter output (glTF/PNG/OGG/JSON). Stored in EditorPrefs, never in the repo.
    /// See docs/adr/0003-asset-import-pipeline.md.
    /// </summary>
    public static class ImportSettings
    {
        const string Key = "HM.ConvertedAssetsRoot";

        public static string Root
        {
            get => EditorPrefs.GetString(Key, string.Empty);
            set => EditorPrefs.SetString(Key, value ?? string.Empty);
        }

        [MenuItem("HM/Importers/Set Converted Assets Folder...")]
        static void Pick()
        {
            string p = EditorUtility.OpenFolderPanel("Converted assets folder", Root, "");
            if (!string.IsNullOrEmpty(p)) Root = p;
        }
    }
}

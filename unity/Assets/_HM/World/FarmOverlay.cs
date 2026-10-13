using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HM.World
{
    /// <summary>Locates the git-ignored converted farm output. Pure path logic so it can be tested with a temp folder.</summary>
    public static class FarmOverlay
    {
        public const string EnvRoot = "HM_CONVERTED";
        public const string EnvDisable = "HM_FARM_OVERLAY";
        /// <summary>First row of the farm scene set (rows 80..95); the others differ by season/variant.</summary>
        public const int DefaultRow = 80;

        public sealed class Paths
        {
            public string Root, MapJson, Glb;
            public bool HasGlb => Glb != null;
        }

        /// <summary>Candidate converted roots in priority order: HM_CONVERTED, then repo assets/hm-converted.</summary>
        public static IEnumerable<string> CandidateRoots()
        {
            var env = Environment.GetEnvironmentVariable(EnvRoot);
            if (!string.IsNullOrEmpty(env)) yield return env;
            // Application.dataPath = <repo>/unity/Assets in the editor.
            string repo = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            yield return Path.Combine(repo, "assets", "hm-converted");
        }

        /// <summary>Paths for the given row under a root, or null when the map JSON is absent. The GLB is optional.</summary>
        public static Paths Resolve(string root, int row)
        {
            if (string.IsNullOrEmpty(root)) return null;
            string json = Path.Combine(root, "maps", "map_" + row.ToString("D3") + ".json");
            if (!File.Exists(json)) return null;
            string glb = Path.Combine(root, "scenes", "farm_" + row.ToString("D2") + ".glb");
            return new Paths { Root = root, MapJson = json, Glb = File.Exists(glb) ? glb : null };
        }

        public static Paths Find(int row = DefaultRow)
        {
            foreach (var r in CandidateRoots()) { var p = Resolve(r, row); if (p != null) return p; }
            return null;
        }

        public static bool Disabled => Environment.GetEnvironmentVariable(EnvDisable) == "0";
    }
}

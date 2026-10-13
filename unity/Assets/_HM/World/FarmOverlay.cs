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
        /// <summary>
        /// Row of the outdoor farm yard in spring with the base buildings. Rows 0..3 are the seasons, 4..7 the
        /// building variant (tools/hmconv/gf.py farm_row). Rows 80..95 are the farmhouse INTERIORS, not the yard.
        /// </summary>
        public const int DefaultRow = 0;

        /// <summary>First interior row (0x50); FUN_00068804 adds season + 4 * level for a door of type 0x50.</summary>
        public const int HouseBase = 0x50;
        public const int YardRows = 8;

        /// <summary>
        /// The state that selects a row. Season is 0..3. BuildingFlag is 0 or 1 (the yard variant). The three levels are
        /// the DAT_001ae1b6 / 1b8 / 1ba values FUN_00068804 adds for door types 0x50 / 0x60 / 0x70 (house, and the
        /// two other upgradable buildings; which of 0x60 / 0x70 is which is not decoded).
        /// </summary>
        public sealed class RowState
        {
            public int Season, BuildingFlag, HouseLevel, Level60, Level70;
        }

        public static readonly RowState State = new RowState();

        /// <summary>Outdoor yard row for a season and building flag (FUN_0006872c, tools/hmconv/gf.py farm_row). The only place this is computed.</summary>
        public static int FarmRow(int season, int buildingFlag) => Mathf.Clamp(season, 0, 3) + 4 * Mathf.Clamp(buildingFlag, 0, 1);

        public static int YardRow => FarmRow(State.Season, State.BuildingFlag);

        /// <summary>Farmhouse interior row (FUN_00068804 for type 0x50, gf.house_row).</summary>
        public static int HouseRow(int season, int level) => HouseBase + Mathf.Clamp(season, 0, 3) + 4 * Mathf.Max(0, level);

        /// <summary>
        /// Row a door region of the given type leads to (FUN_00068804): 0x50 adds season + 4 * house level, 0x60 and 0x70
        /// add season + 4 * their level, every other value (92..95 and so on) is the row itself.
        /// </summary>
        public static int DoorTargetRow(int type, RowState st)
        {
            switch (type)
            {
                case 0x50: return HouseRow(st.Season, st.HouseLevel);
                case 0x60: return 0x60 + Mathf.Clamp(st.Season, 0, 3) + 4 * Mathf.Max(0, st.Level60);
                case 0x70: return 0x70 + Mathf.Clamp(st.Season, 0, 3) + 4 * Mathf.Max(0, st.Level70);
                default: return type;
            }
        }

        public static bool IsYardRow(int row) => row >= 0 && row < YardRows;

        /// <summary>Parses "x,z" in raw units (the -hm-shot-pos value). False on anything else.</summary>
        public static bool TryParseRawPos(string s, out float x, out float z)
        {
            x = z = 0f;
            if (string.IsNullOrEmpty(s)) return false;
            var p = s.Split(',');
            return p.Length == 2
                && float.TryParse(p[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x)
                && float.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out z);
        }

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
            string name = "farm_" + row.ToString("D2") + ".glb";
            string glb = Path.Combine(root, "scenes_textured", name); // textured export first
            if (!File.Exists(glb)) glb = Path.Combine(root, "scenes", name);
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

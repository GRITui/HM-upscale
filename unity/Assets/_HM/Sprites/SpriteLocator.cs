using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HM.Sprites
{
    /// <summary>Finds the git-ignored converted sprite output and resolves a role name to a sheet. Nothing here is committed data.</summary>
    public static class SpriteLocator
    {
        public const string EnvRoot = "HM_CONVERTED";
        public const string EnvSprites = "HM_SPRITES";

        /// <summary>
        /// Observed defaults. Docs claimed main_00/01 were the player; the decoded data shows they are a cow and a sheep.
        /// obj_14_r00 is the orange-haired boy. The girl is not identified. Override with a JSON file named by $HM_SPRITES:
        /// { "roles": [ { "role": "player", "sheet": "obj_14_r00" } ] }
        /// </summary>
        public static readonly Dictionary<string, string> DefaultRoles = new Dictionary<string, string>
        {
            { "player", "obj_14_r00" },
            { "cow", "main_00_r00" },
            { "sheep", "main_01_r00" },
        };

        [Serializable] class RoleEntry { public string role, sheet; }
        [Serializable] class RoleFile { public RoleEntry[] roles; }

        public static IEnumerable<string> CandidateRoots()
        {
            var env = Environment.GetEnvironmentVariable(EnvRoot);
            if (!string.IsNullOrEmpty(env)) yield return env;
            string repo = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            yield return Path.Combine(repo, "assets", "hm-converted");
        }

        public static string FindRoot()
        {
            foreach (var r in CandidateRoots()) if (Directory.Exists(Path.Combine(r, "sprites"))) return r;
            return null;
        }

        public static string SheetPath(string root, string sheet, string ext) => Path.Combine(root, "sprites", sheet + "." + ext);

        public static Dictionary<string, string> Roles(string overrideJson = null)
        {
            var map = new Dictionary<string, string>(DefaultRoles);
            try
            {
                if (overrideJson == null)
                {
                    var p = Environment.GetEnvironmentVariable(EnvSprites);
                    if (!string.IsNullOrEmpty(p) && File.Exists(p)) overrideJson = File.ReadAllText(p);
                }
                if (!string.IsNullOrEmpty(overrideJson))
                {
                    var f = JsonUtility.FromJson<RoleFile>(overrideJson);
                    if (f?.roles != null) foreach (var e in f.roles) if (!string.IsNullOrEmpty(e.role) && !string.IsNullOrEmpty(e.sheet)) map[e.role] = e.sheet;
                }
            }
            catch (Exception e) { Debug.LogWarning("[HM-SPRITES] bad role override: " + e.Message); }
            return map;
        }
    }
}

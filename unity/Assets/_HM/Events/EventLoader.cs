using System;
using System.IO;
using UnityEngine;

namespace HM.Events
{
    /// <summary>
    /// Loads converted event JSON (assets/hm-converted/events/mapNNN.json, git-ignored game data).
    /// Absent data is a normal state: every method returns false or null instead of throwing.
    /// Search order: <see cref="OverrideDir"/>, env HM_EVENTS_DIR, then the repo-relative default.
    /// </summary>
    public static class EventLoader
    {
        public const string EnvVar = "HM_EVENTS_DIR";
        public const string DefaultRelative = "assets/hm-converted/events";

        /// <summary>Tests and tools can point the loader at another directory.</summary>
        public static string OverrideDir;

        public static string MapFileName(int map) => "map" + map.ToString("000") + ".json";

        public static string ResolveDirectory()
        {
            if (!string.IsNullOrEmpty(OverrideDir)) return Directory.Exists(OverrideDir) ? OverrideDir : null;
            string env = Environment.GetEnvironmentVariable(EnvVar);
            if (!string.IsNullOrEmpty(env) && Directory.Exists(env)) return env;
            try
            {
                // Application.dataPath = <repo>/unity/Assets
                string repo = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
                string d = Path.Combine(repo, DefaultRelative);
                if (Directory.Exists(d)) return d;
            }
            catch (Exception) { /* not on the main thread or no data path: treat as absent */ }
            return null;
        }

        public static bool Available => ResolveDirectory() != null;

        public static EventFile Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var f = JsonUtility.FromJson<EventFile>(json);
                return f != null && f.schema == 1 ? f : null;
            }
            catch (Exception) { return null; }
        }

        public static bool TryLoadMap(int map, out EventFile file)
        {
            file = null;
            string dir = ResolveDirectory();
            if (dir == null) return false;
            string path = Path.Combine(dir, MapFileName(map));
            if (!File.Exists(path)) return false;
            try { file = Parse(File.ReadAllText(path)); }
            catch (Exception) { file = null; }
            return file != null;
        }
    }
}

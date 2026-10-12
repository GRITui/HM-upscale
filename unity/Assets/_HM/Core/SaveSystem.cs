using System;
using System.IO;
using UnityEngine;

namespace HM.Core
{
    [Serializable]
    public sealed class SaveData
    {
        public int version = SaveSystem.CurrentVersion;
        public double totalMinutes;
        public int gold;
        public float playerX, playerY, playerZ;
    }

    public enum SaveLoadResult { Ok, NotFound, Corrupt, NewerVersion }

    /// <summary>
    /// JSON save files with a version field. Writes go to a temp file first, then replace the target,
    /// so a crash mid-write never leaves a truncated save.
    /// </summary>
    public sealed class SaveSystem
    {
        public const int CurrentVersion = 1;
        const string Extension = ".json";

        readonly string directory;

        public SaveSystem(string directory)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("directory required", nameof(directory));
            this.directory = directory;
        }

        public string PathFor(string slot) => System.IO.Path.Combine(directory, slot + Extension);

        public bool Exists(string slot) => File.Exists(PathFor(slot));

        public void Save(string slot, SaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            data.version = CurrentVersion;
            Directory.CreateDirectory(directory);
            string target = PathFor(slot);
            string temp = target + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(data, true));
            if (File.Exists(target)) File.Replace(temp, target, null);
            else File.Move(temp, target);
        }

        public SaveLoadResult TryLoad(string slot, out SaveData data)
        {
            data = null;
            string path = PathFor(slot);
            if (!File.Exists(path)) return SaveLoadResult.NotFound;
            SaveData parsed;
            try { parsed = JsonUtility.FromJson<SaveData>(File.ReadAllText(path)); }
            catch (Exception) { return SaveLoadResult.Corrupt; }
            if (parsed == null || parsed.version < 1) return SaveLoadResult.Corrupt;
            if (parsed.version > CurrentVersion) return SaveLoadResult.NewerVersion;
            data = Migrate(parsed);
            return SaveLoadResult.Ok;
        }

        /// <summary>Upgrade older versions step by step. Version 1 is current, so nothing to do yet.</summary>
        static SaveData Migrate(SaveData d)
        {
            d.version = CurrentVersion;
            return d;
        }

        public void Delete(string slot)
        {
            string p = PathFor(slot);
            if (File.Exists(p)) File.Delete(p);
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace HM.Audio
{
    public enum AudioKind { Sfx, Music }

    /// <summary>
    /// Audio entries read from the converter manifest (ADR 0004). Only type "audio" entries are kept.
    /// Entries tagged "track" (or "music") are music; everything else is a sound effect.
    /// The importer is editor-only, so this reads the manifest JSON on its own.
    /// </summary>
    public sealed class AudioCatalog
    {
        [Serializable] sealed class RawEntry { public string id; public string type; public string path; public string[] tags; }
        [Serializable] sealed class RawManifest { public int schema; public RawEntry[] assets; }

        readonly Dictionary<string, AudioKind> kinds = new Dictionary<string, AudioKind>();
        readonly List<string> music = new List<string>();
        readonly List<string> sfx = new List<string>();

        public IReadOnlyList<string> MusicIds => music;
        public IReadOnlyList<string> SfxIds => sfx;
        public bool Contains(string id) => id != null && kinds.ContainsKey(id);
        public bool TryGetKind(string id, out AudioKind kind) => kinds.TryGetValue(id ?? "", out kind);

        public static AudioCatalog FromManifestJson(string json)
        {
            var raw = JsonUtility.FromJson<RawManifest>(json);
            if (raw == null) throw new FormatException("manifest did not parse");
            if (raw.schema != 1) throw new FormatException("unsupported manifest schema " + raw.schema);
            var c = new AudioCatalog();
            if (raw.assets == null) return c;
            foreach (var e in raw.assets)
            {
                if (e == null || e.type != "audio" || string.IsNullOrEmpty(e.id)) continue;
                if (c.kinds.ContainsKey(e.id)) continue;
                bool isMusic = e.tags != null && (Array.IndexOf(e.tags, "track") >= 0 || Array.IndexOf(e.tags, "music") >= 0);
                c.kinds[e.id] = isMusic ? AudioKind.Music : AudioKind.Sfx;
                (isMusic ? c.music : c.sfx).Add(e.id);
            }
            return c;
        }

        public static AudioCatalog FromManifestFile(string path) => FromManifestJson(System.IO.File.ReadAllText(path));
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace HM.Audio
{
    /// <summary>
    /// Music by track id, SFX by id, per-bus volume, loops. Lives in the Boot scene (or any
    /// persistent object). Call Initialize with a catalog and a clip source before use.
    /// Missing ids and clips never throw: play calls return false so game code can carry on silent.
    /// </summary>
    public sealed class AudioService : MonoBehaviour
    {
        public const int SfxVoices = 8;

        AudioCatalog catalog;
        IClipSource clips;
        AudioSource musicSource;
        AudioSource[] sfxSources;
        int nextVoice;
        float master = 1f, music = 0.8f, sfx = 1f;

        public string CurrentMusicId { get; private set; }
        public bool MusicPlaying => musicSource != null && musicSource.isPlaying;
        public float MasterVolume { get => master; set { master = Mathf.Clamp01(value); Apply(); } }
        public float MusicVolume { get => music; set { music = Mathf.Clamp01(value); Apply(); } }
        public float SfxVolume { get => sfx; set { sfx = Mathf.Clamp01(value); Apply(); } }
        public AudioCatalog Catalog => catalog;

        public void Initialize(AudioCatalog catalog, IClipSource clips)
        {
            this.catalog = catalog;
            this.clips = clips;
            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.playOnAwake = false;
                musicSource.spatialBlend = 0f;
                sfxSources = new AudioSource[SfxVoices];
                for (int i = 0; i < SfxVoices; i++)
                {
                    var s = gameObject.AddComponent<AudioSource>();
                    s.playOnAwake = false;
                    s.spatialBlend = 0f;
                    sfxSources[i] = s;
                }
            }
            Apply();
        }

        /// <summary>Plays a music track, looping by default. Replaying the current track is a no-op.</summary>
        public bool PlayMusic(string trackId, bool loop = true)
        {
            if (!Ready(trackId, AudioKind.Music, out var clip)) return false;
            if (CurrentMusicId == trackId && musicSource.isPlaying) { musicSource.loop = loop; return true; }
            musicSource.Stop();
            musicSource.clip = clip;
            musicSource.loop = loop;
            musicSource.Play();
            CurrentMusicId = trackId;
            return true;
        }

        public void StopMusic()
        {
            if (musicSource == null) return;
            musicSource.Stop();
            musicSource.clip = null;
            CurrentMusicId = null;
        }

        /// <summary>Plays a one-shot effect on the next free voice (oldest voice is stolen when all are busy).</summary>
        public bool PlaySfx(string sfxId, float volumeScale = 1f)
        {
            if (!Ready(sfxId, AudioKind.Sfx, out var clip)) return false;
            var s = NextVoice();
            s.Stop();
            s.clip = clip;
            s.loop = false;
            s.volume = sfx * master * Mathf.Clamp01(volumeScale);
            s.Play();
            return true;
        }

        /// <summary>Starts a looping effect (ambience, machinery). Returns the voice index, or -1.</summary>
        public int PlaySfxLoop(string sfxId)
        {
            if (!Ready(sfxId, AudioKind.Sfx, out var clip)) return -1;
            int idx = nextVoice;
            var s = NextVoice();
            s.Stop();
            s.clip = clip;
            s.loop = true;
            s.volume = sfx * master;
            s.Play();
            return idx;
        }

        public void StopSfx(int voice)
        {
            if (sfxSources == null || voice < 0 || voice >= sfxSources.Length) return;
            sfxSources[voice].Stop();
            sfxSources[voice].loop = false;
        }

        public int ActiveSfxVoices
        {
            get
            {
                int n = 0;
                if (sfxSources != null) foreach (var s in sfxSources) if (s.isPlaying) n++;
                return n;
            }
        }

        AudioSource NextVoice()
        {
            for (int i = 0; i < sfxSources.Length; i++)
            {
                int k = (nextVoice + i) % sfxSources.Length;
                if (!sfxSources[k].isPlaying) { nextVoice = (k + 1) % sfxSources.Length; return sfxSources[k]; }
            }
            var stolen = sfxSources[nextVoice];
            nextVoice = (nextVoice + 1) % sfxSources.Length;
            return stolen;
        }

        bool Ready(string id, AudioKind want, out AudioClip clip)
        {
            clip = null;
            if (catalog == null || clips == null || string.IsNullOrEmpty(id)) return false;
            if (!catalog.TryGetKind(id, out var kind) || kind != want) return false;
            clip = clips.Get(id);
            return clip != null;
        }

        void Apply()
        {
            if (musicSource == null) return;
            musicSource.volume = music * master;
            foreach (var s in sfxSources) if (!s.isPlaying) s.volume = sfx * master;
        }
    }
}

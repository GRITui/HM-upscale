using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace HM.Audio
{
    /// <summary>Resolves a clip id (the manifest id) to an AudioClip, or null if unavailable.</summary>
    public interface IClipSource
    {
        AudioClip Get(string id);
    }

    public sealed class DictionaryClipSource : IClipSource
    {
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        public void Add(string id, AudioClip clip) => clips[id] = clip;
        public AudioClip Get(string id) => id != null && clips.TryGetValue(id, out var c) ? c : null;
    }

    /// <summary>Loads clips by Addressables address (= manifest id) on first use and keeps them cached.</summary>
    public sealed class AddressablesClipSource : IClipSource
    {
        readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

        public AudioClip Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (cache.TryGetValue(id, out var hit)) return hit;
            AudioClip clip = null;
            var locs = Addressables.LoadResourceLocationsAsync(id, typeof(AudioClip));
            locs.WaitForCompletion();
            if (locs.Status == AsyncOperationStatus.Succeeded && locs.Result.Count > 0)
                clip = Addressables.LoadAssetAsync<AudioClip>(id).WaitForCompletion();
            Addressables.Release(locs);
            if (clip == null) Debug.LogWarning("AudioService: no clip with address '" + id + "'");
            cache[id] = clip;
            return clip;
        }
    }
}

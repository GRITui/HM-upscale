using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace HM.World
{
    /// <summary>Resolves a manifest model id to a prefab. Returns null when the id is unknown.</summary>
    public interface IModelSource
    {
        GameObject Get(string modelId);
    }

    /// <summary>Dictionary-backed source, used by tests and tools.</summary>
    public sealed class DictionaryModelSource : IModelSource
    {
        readonly Dictionary<string, GameObject> map = new Dictionary<string, GameObject>();
        public void Add(string id, GameObject prefab) => map[id] = prefab;
        public GameObject Get(string modelId) => modelId != null && map.TryGetValue(modelId, out var g) ? g : null;
    }

    /// <summary>Loads prefabs from Addressables by id (addresses are the manifest ids). Missing keys yield null.</summary>
    public sealed class AddressablesModelSource : IModelSource
    {
        readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();

        public GameObject Get(string modelId)
        {
            if (string.IsNullOrEmpty(modelId)) return null;
            if (cache.TryGetValue(modelId, out var hit)) return hit;
            GameObject result = null;
            try
            {
                var locs = Addressables.LoadResourceLocationsAsync(modelId, typeof(GameObject));
                locs.WaitForCompletion();
                if (locs.Status == AsyncOperationStatus.Succeeded && locs.Result.Count > 0)
                {
                    var op = Addressables.LoadAssetAsync<GameObject>(modelId);
                    result = op.WaitForCompletion();
                }
                Addressables.Release(locs);
            }
            catch (Exception) { result = null; }
            cache[modelId] = result;
            return result;
        }
    }
}

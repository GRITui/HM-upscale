using System.Collections.Generic;
using UnityEngine;

namespace HM.World
{
    /// <summary>
    /// Map-JSON areas (ADR 0005) living inside the currently loaded scenes. Complements
    /// AreaSceneService: a scene holds hand-authored areas, MapAreas holds areas built at runtime
    /// from converted data. Loading an id that is already loaded replaces it.
    /// </summary>
    public sealed class MapAreas
    {
        readonly Dictionary<string, BuiltMap> maps = new Dictionary<string, BuiltMap>();
        readonly IModelSource models;
        readonly Material terrainMaterial;
        readonly Material placeholderMaterial;

        public MapAreas(IModelSource models, Material terrainMaterial = null, Material placeholderMaterial = null)
        {
            this.models = models;
            this.terrainMaterial = terrainMaterial;
            this.placeholderMaterial = placeholderMaterial;
        }

        public IEnumerable<string> Loaded => maps.Keys;
        public bool IsLoaded(string id) => maps.ContainsKey(id);
        public bool TryGet(string id, out BuiltMap map) => maps.TryGetValue(id, out map);

        public BuiltMap Load(string json)
        {
            var data = MapData.Parse(json);
            Unload(data.id);
            var built = MapLoader.Build(data, models, terrainMaterial, placeholderMaterial);
            maps[data.id] = built;
            return built;
        }

        public bool Unload(string id)
        {
            if (id == null || !maps.TryGetValue(id, out var built)) return false;
            maps.Remove(id);
            if (built.Root != null)
            {
                if (Application.isPlaying) Object.Destroy(built.Root); else Object.DestroyImmediate(built.Root);
            }
            return true;
        }

        public void UnloadAll()
        {
            foreach (var id in new List<string>(maps.Keys)) Unload(id);
        }
    }
}

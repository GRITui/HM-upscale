using System;
using UnityEngine;

namespace HM.World
{
    /// <summary>Serializable map description. Schema: docs/adr/0005-map-json.md. Vectors are float[3].</summary>
    [Serializable]
    public sealed class MapData
    {
        public const int CurrentSchema = 1;
        public int schema = CurrentSchema;
        public string id;
        public string name;
        public MapTerrain terrain;
        public MapObject[] objects = new MapObject[0];
        public MapSpawn[] spawns = new MapSpawn[0];
        public MapCollider[] colliders = new MapCollider[0];

        public static MapData Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("empty map json");
            var m = JsonUtility.FromJson<MapData>(json);
            if (m == null) throw new FormatException("map json did not parse");
            if (m.schema < 1 || m.schema > CurrentSchema) throw new FormatException("unsupported map schema " + m.schema);
            if (string.IsNullOrEmpty(m.id)) throw new FormatException("map id required");
            m.objects ??= new MapObject[0];
            m.spawns ??= new MapSpawn[0];
            m.colliders ??= new MapCollider[0];
            if (m.terrain != null) m.terrain.Validate();
            return m;
        }

        public static Vector3 V3(float[] a, Vector3 fallback = default) =>
            a != null && a.Length >= 3 ? new Vector3(a[0], a[1], a[2]) : fallback;
    }

    [Serializable]
    public sealed class MapTerrain
    {
        /// <summary>Samples along X and Z (each at least 2).</summary>
        public int width, depth;
        /// <summary>World metres between samples.</summary>
        public float cellSize = 1f;
        /// <summary>Row-major (z * width + x) heights in metres; length must be width * depth.</summary>
        public float[] heights;
        /// <summary>RGB 0..1.</summary>
        public float[] color;

        public void Validate()
        {
            if (width < 2 || depth < 2) throw new FormatException("terrain needs width and depth >= 2");
            if (cellSize <= 0f) throw new FormatException("terrain cellSize must be > 0");
            if (heights == null || heights.Length != width * depth)
                throw new FormatException("terrain heights length must equal width * depth");
        }
    }

    [Serializable]
    public sealed class MapObject
    {
        public string id;
        /// <summary>Asset manifest id of a model. Missing or unknown ids render a placeholder cube.</summary>
        public string model;
        public float[] position;
        public float rotationY;
        public float scale = 1f;
        /// <summary>Placeholder size used when the model is missing.</summary>
        public float[] size;
    }

    [Serializable]
    public sealed class MapSpawn
    {
        public string name;
        public float[] position;
        public float rotationY;
    }

    [Serializable]
    public sealed class MapCollider
    {
        public float[] center;
        public float[] size;
        public float rotationY;
    }
}

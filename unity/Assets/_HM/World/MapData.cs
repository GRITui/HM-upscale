using System;
using System.Text.RegularExpressions;
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
        /// <summary>Raw [x, y, z] where the player stands on arriving from the farmhouse (yard frame); optional.</summary>
        public int[] entry;
        public MapCollider[] colliders = new MapCollider[0];
        /// <summary>Decoded trigger rectangles (raw PSX units). Data only; event ids are not interpreted.</summary>
        public MapRegion[] regions = new MapRegion[0];
        /// <summary>Decoded placed-model records (raw PSX units). Data only; geometry comes from the scene GLB.</summary>
        public MapInstance[] instances = new MapInstance[0];

        static readonly Regex HeightsArray = new Regex("\"heights\"\\s*:\\s*\\[[^\\]]*\\]", RegexOptions.Compiled);
        static readonly Regex NullToken = new Regex("\\bnull\\b", RegexOptions.Compiled);

        public static MapData Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new ArgumentException("empty map json");
            // JsonUtility cannot hold null in a float[]; converted terrain uses null for "not walkable".
            json = HeightsArray.Replace(json, mt => NullToken.Replace(mt.Value, MapTerrain.BlockedLiteral));
            var m = JsonUtility.FromJson<MapData>(json);
            if (m == null) throw new FormatException("map json did not parse");
            if (m.schema < 1 || m.schema > CurrentSchema) throw new FormatException("unsupported map schema " + m.schema);
            if (string.IsNullOrEmpty(m.id)) throw new FormatException("map id required");
            m.objects ??= new MapObject[0];
            m.spawns ??= new MapSpawn[0];
            m.colliders ??= new MapCollider[0];
            m.regions ??= new MapRegion[0];
            m.instances ??= new MapInstance[0];
            // JsonUtility turns "terrain": null into an empty object; stale converter output has exactly that.
            if (m.terrain != null && m.terrain.width == 0 && m.terrain.depth == 0 && m.terrain.heights == null) m.terrain = null;
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

        /// <summary>Marks a sample that is not walkable (a JSON null in converted data).</summary>
        public const float Blocked = 1e30f;
        public const string BlockedLiteral = "1e30";

        /// <summary>
        /// Converted (raw) form: <c>cell</c> &gt; 0 and <c>origin</c> = [x, z] of sample (0,0), both in raw PSX units
        /// (not metres). Heights are raw Y (down is positive; walkable ground is 0) and Blocked marks non-walkable samples.
        /// </summary>
        public float[] origin;
        public float cell;

        public bool IsRaw => cell > 0f;
        public bool IsWalkableSample(int i) => heights[i] < Blocked * 0.5f;

        public void Validate()
        {
            if (width < 2 || depth < 2) throw new FormatException("terrain needs width and depth >= 2");
            if (IsRaw)
            {
                if (origin == null || origin.Length < 2) throw new FormatException("raw terrain needs origin [x, z]");
                if (heights == null || heights.Length != width * depth)
                    throw new FormatException("terrain heights length must equal width * depth");
                return;
            }
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

    /// <summary>One decoded region rectangle. Coordinates are raw PSX units; event and warp ids stay data.</summary>
    [Serializable]
    public sealed class MapRegion
    {
        public int index, flags, type, @event = -1, facing, condition;
        /// <summary>[x, z] corners in raw units.</summary>
        public int[] min, max;
        /// <summary>[x, y, z] arrival point in raw units.</summary>
        public int[] arrival;
    }

    [Serializable]
    public sealed class MapInstance
    {
        public int index, flags;
        public int[] model_offsets, shorts;
    }
}

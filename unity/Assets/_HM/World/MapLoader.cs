using System.Collections.Generic;
using UnityEngine;

namespace HM.World
{
    /// <summary>Result of building a map: the root object plus lookups for gameplay code.</summary>
    public sealed class BuiltMap
    {
        public GameObject Root;
        public MapData Data;
        public readonly Dictionary<string, Transform> Spawns = new Dictionary<string, Transform>();
        public int PlaceholderCount;
        public int ModelCount;
        public int RegionCount;
        public int WalkableCells;
    }

    /// <summary>Builds an area from a MapData. Missing models become placeholder primitives (never an exception).</summary>
    public static class MapLoader
    {
        public const float RegionHeight = 2f;

        public static BuiltMap Build(string json, IModelSource models, Material terrainMaterial = null, Material placeholderMaterial = null)
            => Build(MapData.Parse(json), models, terrainMaterial, placeholderMaterial);

        public static BuiltMap Build(MapData data, IModelSource models, Material terrainMaterial = null, Material placeholderMaterial = null)
        {
            var built = new BuiltMap { Data = data, Root = new GameObject("Map " + data.id) };
            var root = built.Root.transform;

            if (data.terrain != null && data.terrain.IsRaw)
            {
                WalkGrid.Build(data.terrain, root, terrainMaterial);
                built.WalkableCells = WalkGrid.WalkableCellCount(data.terrain);
            }
            else if (data.terrain != null) BuildTerrain(data.terrain, root, terrainMaterial);

            var objects = new GameObject("Objects"); objects.transform.SetParent(root, false);
            foreach (var o in data.objects)
            {
                var prefab = models?.Get(o.model);
                GameObject go;
                if (prefab != null)
                {
                    go = Object.Instantiate(prefab, objects.transform);
                    built.ModelCount++;
                }
                else
                {
                    go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.transform.SetParent(objects.transform, false);
                    var size = MapData.V3(o.size, Vector3.one);
                    go.transform.localScale = size;
                    if (placeholderMaterial != null) go.GetComponent<Renderer>().sharedMaterial = placeholderMaterial;
                    built.PlaceholderCount++;
                }
                go.name = string.IsNullOrEmpty(o.id) ? (o.model ?? "object") : o.id;
                var pos = MapData.V3(o.position);
                go.transform.localPosition = pos;
                go.transform.localRotation = Quaternion.Euler(0f, o.rotationY, 0f);
                float s = o.scale > 0f ? o.scale : 1f;
                go.transform.localScale = Vector3.Scale(go.transform.localScale, new Vector3(s, s, s));
            }

            var spawns = new GameObject("Spawns"); spawns.transform.SetParent(root, false);
            foreach (var sp in data.spawns)
            {
                var t = new GameObject("Spawn " + sp.name).transform;
                t.SetParent(spawns.transform, false);
                t.localPosition = MapData.V3(sp.position);
                t.localRotation = Quaternion.Euler(0f, sp.rotationY, 0f);
                built.Spawns[sp.name ?? "default"] = t;
            }

            if (data.entry != null && data.entry.Length >= 3 && !built.Spawns.ContainsKey("default"))
            {
                var t = new GameObject("Spawn default").transform;
                t.SetParent(spawns.transform, false);
                t.localPosition = FarmCoords.ToWorld(data.entry);
                built.Spawns["default"] = t;
            }

            if (data.terrain != null && data.terrain.IsRaw && !built.Spawns.ContainsKey("default") && WalkGrid.MostOpen(data.terrain, 5, out var open))
            {
                var t = new GameObject("Spawn default").transform;
                t.SetParent(spawns.transform, false);
                t.localPosition = open;
                built.Spawns["default"] = t;
            }

            var regions = new GameObject("Regions"); regions.transform.SetParent(root, false);
            foreach (var r in data.regions)
            {
                if (r.min == null || r.max == null || r.min.Length < 2 || r.max.Length < 2) continue;
                var a = FarmCoords.ToWorld(r.min[0], 0, r.min[1]); var b = FarmCoords.ToWorld(r.max[0], 0, r.max[1]);
                var go = new GameObject("Region " + r.index);
                go.transform.SetParent(regions.transform, false);
                go.transform.localPosition = (a + b) * 0.5f + new Vector3(0, RegionHeight * 0.5f, 0);
                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(Mathf.Abs(a.x - b.x), RegionHeight, Mathf.Abs(a.z - b.z));
                var trig = go.AddComponent<MapRegionTrigger>();
                trig.index = r.index; trig.flags = r.flags; trig.type = r.type; trig.eventId = r.@event;
                trig.facing = r.facing; trig.condition = r.condition; trig.arrival = FarmCoords.ToWorld(r.arrival);
                built.RegionCount++;
            }

            var colliders = new GameObject("Colliders"); colliders.transform.SetParent(root, false);
            for (int i = 0; i < data.colliders.Length; i++)
            {
                var c = data.colliders[i];
                var go = new GameObject("Collider " + i);
                go.transform.SetParent(colliders.transform, false);
                go.transform.localPosition = MapData.V3(c.center);
                go.transform.localRotation = Quaternion.Euler(0f, c.rotationY, 0f);
                go.AddComponent<BoxCollider>().size = MapData.V3(c.size, Vector3.one);
            }
            return built;
        }

        static void BuildTerrain(MapTerrain t, Transform parent, Material material)
        {
            var go = new GameObject("Terrain");
            go.transform.SetParent(parent, false);
            var mesh = new Mesh { name = "TerrainMesh" };
            var verts = new Vector3[t.width * t.depth];
            var uvs = new Vector2[verts.Length];
            for (int z = 0; z < t.depth; z++)
                for (int x = 0; x < t.width; x++)
                {
                    int i = z * t.width + x;
                    verts[i] = new Vector3(x * t.cellSize, t.heights[i], z * t.cellSize);
                    uvs[i] = new Vector2(x / (float)(t.width - 1), z / (float)(t.depth - 1));
                }
            var tris = new int[(t.width - 1) * (t.depth - 1) * 6];
            int k = 0;
            for (int z = 0; z < t.depth - 1; z++)
                for (int x = 0; x < t.width - 1; x++)
                {
                    int a = z * t.width + x, b = a + 1, c = a + t.width, d = c + 1;
                    // Counter-clockwise seen from above so normals face up.
                    tris[k++] = a; tris[k++] = c; tris[k++] = b;
                    tris[k++] = b; tris[k++] = c; tris[k++] = d;
                }
            mesh.vertices = verts; mesh.uv = uvs; mesh.triangles = tris;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            if (material != null) r.sharedMaterial = material;
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                var m = new Material(shader);
                var col = t.color != null && t.color.Length >= 3 ? new Color(t.color[0], t.color[1], t.color[2]) : new Color(0.36f, 0.55f, 0.28f);
                m.SetColor("_BaseColor", col); m.color = col;
                r.sharedMaterial = m;
            }
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        /// <summary>Ground height at world-local x,z by bilinear sampling; used for placing things on terrain.</summary>
        public static float SampleHeight(MapTerrain t, float x, float z)
        {
            float fx = Mathf.Clamp(x / t.cellSize, 0, t.width - 1);
            float fz = Mathf.Clamp(z / t.cellSize, 0, t.depth - 1);
            int x0 = Mathf.Min((int)fx, t.width - 2), z0 = Mathf.Min((int)fz, t.depth - 2);
            float tx = fx - x0, tz = fz - z0;
            float h00 = t.heights[z0 * t.width + x0], h10 = t.heights[z0 * t.width + x0 + 1];
            float h01 = t.heights[(z0 + 1) * t.width + x0], h11 = t.heights[(z0 + 1) * t.width + x0 + 1];
            return Mathf.Lerp(Mathf.Lerp(h00, h10, tx), Mathf.Lerp(h01, h11, tx), tz);
        }
    }
}

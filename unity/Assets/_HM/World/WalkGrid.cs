using System.Collections.Generic;
using UnityEngine;

namespace HM.World
{
    /// <summary>
    /// Turns a raw (converted) terrain grid into collision: a floor mesh over walkable cells and merged box walls on
    /// every edge between a walkable cell and a non-walkable one. A cell is walkable when all four of its corner
    /// samples are non-null. Ground is flat (raw height 0), so the floor sits at y = -height * scale.
    /// </summary>
    public static class WalkGrid
    {
        public const float WallHeight = 3f;
        public const float WallThickness = 0.5f;

        public static bool CellWalkable(MapTerrain t, int cx, int cz)
        {
            if (cx < 0 || cz < 0 || cx >= t.width - 1 || cz >= t.depth - 1) return false;
            int a = cz * t.width + cx;
            return t.IsWalkableSample(a) && t.IsWalkableSample(a + 1) && t.IsWalkableSample(a + t.width) && t.IsWalkableSample(a + t.width + 1);
        }

        public static int WalkableCellCount(MapTerrain t)
        {
            int n = 0;
            for (int z = 0; z < t.depth - 1; z++) for (int x = 0; x < t.width - 1; x++) if (CellWalkable(t, x, z)) n++;
            return n;
        }

        /// <summary>World position of grid sample (ix, iz) at the given raw height.</summary>
        public static Vector3 SampleToWorld(MapTerrain t, float ix, float iz, float rawY = 0f) =>
            FarmCoords.ToWorld(t.origin[0] + ix * t.cell, rawY, t.origin[1] + iz * t.cell);

        /// <summary>Centre of the walkable cell nearest the given world point; false when the grid has none.</summary>
        public static bool NearestWalkable(MapTerrain t, Vector3 near, out Vector3 pos)
        {
            pos = default; float best = float.MaxValue; bool found = false;
            for (int z = 0; z < t.depth - 1; z++)
                for (int x = 0; x < t.width - 1; x++)
                {
                    if (!CellWalkable(t, x, z)) continue;
                    var c = SampleToWorld(t, x + 0.5f, z + 0.5f);
                    float d = (c - near).sqrMagnitude;
                    if (d < best) { best = d; pos = c; found = true; }
                }
            return found;
        }

        /// <summary>Walkable cell centre with the most walkable neighbours within radius cells (open ground for a farm).</summary>
        public static bool MostOpen(MapTerrain t, int radius, out Vector3 pos)
        {
            pos = default; int best = -1;
            for (int z = 0; z < t.depth - 1; z++)
                for (int x = 0; x < t.width - 1; x++)
                {
                    if (!CellWalkable(t, x, z)) continue;
                    int n = 0;
                    for (int dz = -radius; dz <= radius; dz++) for (int dx = -radius; dx <= radius; dx++) if (CellWalkable(t, x + dx, z + dz)) n++;
                    if (n > best) { best = n; pos = SampleToWorld(t, x + 0.5f, z + 0.5f); }
                }
            return best >= 0;
        }

        public static void Build(MapTerrain t, Transform parent, Material material)
        {
            var go = new GameObject("WalkGrid");
            go.transform.SetParent(parent, false);

            var verts = new List<Vector3>(); var tris = new List<int>(); var uvs = new List<Vector2>();
            var index = new int[t.width * t.depth];
            for (int i = 0; i < index.Length; i++) index[i] = -1;
            int Vert(int ix, int iz)
            {
                int k = iz * t.width + ix;
                if (index[k] < 0)
                {
                    index[k] = verts.Count;
                    verts.Add(SampleToWorld(t, ix, iz, t.heights[k]));
                    uvs.Add(new Vector2(ix, iz));
                }
                return index[k];
            }
            for (int z = 0; z < t.depth - 1; z++)
                for (int x = 0; x < t.width - 1; x++)
                {
                    if (!CellWalkable(t, x, z)) continue;
                    int a = Vert(x, z), b = Vert(x + 1, z), c = Vert(x, z + 1), d = Vert(x + 1, z + 1);
                    if (FarmCoords.MirrorX) { tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(b); tris.Add(d); tris.Add(c); }
                    else { tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(d); }
                }
            if (tris.Count > 0)
            {
                var mesh = new Mesh { name = "WalkGridMesh" };
                mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
                mesh.RecalculateNormals(); mesh.RecalculateBounds();
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                if (material != null) r.sharedMaterial = material;
                else
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    var m = new Material(shader); var col = new Color(0.36f, 0.55f, 0.28f);
                    m.SetColor("_BaseColor", col); m.color = col; r.sharedMaterial = m;
                }
            }
            BuildWalls(t, go.transform);
        }

        static void BuildWalls(MapTerrain t, Transform parent)
        {
            var walls = new GameObject("Walls"); walls.transform.SetParent(parent, false);
            int count = 0;
            // Edges parallel to X: between row z-1 and z, for every grid line z in [0, depth-1].
            for (int line = 0; line < t.depth; line++)
                for (int side = -1; side <= 1; side += 2) // -1: walkable cell lies at line-1 (wall on +z side), +1: cell at line
                {
                    int runStart = -1;
                    for (int x = 0; x <= t.width - 1; x++)
                    {
                        bool edge = x < t.width - 1 && CellWalkable(t, x, side < 0 ? line - 1 : line) && !CellWalkable(t, x, side < 0 ? line : line - 1);
                        if (edge && runStart < 0) runStart = x;
                        if (!edge && runStart >= 0)
                        {
                            AddWall(t, walls.transform, runStart, x, line, line, side, true, count++);
                            runStart = -1;
                        }
                    }
                }
            // Edges parallel to Z: between column x-1 and x.
            for (int line = 0; line < t.width; line++)
                for (int side = -1; side <= 1; side += 2)
                {
                    int runStart = -1;
                    for (int z = 0; z <= t.depth - 1; z++)
                    {
                        bool edge = z < t.depth - 1 && CellWalkable(t, side < 0 ? line - 1 : line, z) && !CellWalkable(t, side < 0 ? line : line - 1, z);
                        if (edge && runStart < 0) runStart = z;
                        if (!edge && runStart >= 0)
                        {
                            AddWall(t, walls.transform, line, line, runStart, z, side, false, count++);
                            runStart = -1;
                        }
                    }
                }
        }

        /// <summary>Wall along a run of grid edges; (x0..x1, z0..z1) are sample indices, side is the walkable side (-1 / +1).</summary>
        static void AddWall(MapTerrain t, Transform parent, int x0, int x1, int z0, int z1, int side, bool alongX, int n)
        {
            var a = SampleToWorld(t, x0, z0); var b = SampleToWorld(t, x1, z1);
            var mid = (a + b) * 0.5f;
            var len = (b - a); len = new Vector3(Mathf.Abs(len.x), 0, Mathf.Abs(len.z));
            // Outward offset puts the box on the blocked side of the edge. Grid +x maps to world x * XSign.
            float outward = -side * (WallThickness * 0.5f);
            float axisSign = alongX ? 1f : FarmCoords.XSign;
            Vector3 off = alongX ? new Vector3(0, 0, outward) : new Vector3(outward * axisSign, 0, 0);
            var go = new GameObject("Wall " + n);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = mid + off + new Vector3(0, WallHeight * 0.5f, 0);
            var box = go.AddComponent<BoxCollider>();
            box.size = alongX ? new Vector3(len.x + WallThickness, WallHeight, WallThickness) : new Vector3(WallThickness, WallHeight, len.z + WallThickness);
        }
    }
}

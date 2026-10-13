using System;
using System.IO;
using HM.World;
using NUnit.Framework;
using UnityEngine;

namespace HM.Tests
{
    /// <summary>Converted-farm loader tests on synthetic fixtures only; no real game data is read.</summary>
    public class FarmMapTests
    {
        // 4x3 samples, origin (-100,200), 100-unit cells; the null at sample 6 leaves only column 0 of cells walkable.
        const string Json = @"{""schema"":1,""id"":""farm_test"",""name"":""Farm Test"",
          ""terrain"":{""origin"":[-100,200],""cell"":100,""width"":4,""depth"":3,
            ""heights"":[0,0,0,0, 0,0,null,0, 0,0,0,0]},
          ""objects"":[],""spawns"":[],""colliders"":[],
          ""regions"":[{""index"":7,""flags"":16384,""type"":2,""event"":42,""facing"":1,""condition"":99,
             ""min"":[-100,200],""max"":[0,300],""arrival"":[-50,0,250]}],
          ""instances"":[{""index"":0,""flags"":64,""model_offsets"":[10,0,0],""shorts"":[1,2,3]}],
          ""models"":[{""vertex_offset"":1}],""undecoded"":[""x""]}";

        BuiltMap built;
        string temp;

        [TearDown]
        public void TearDown()
        {
            if (built != null) UnityEngine.Object.DestroyImmediate(built.Root);
            if (temp != null && Directory.Exists(temp)) Directory.Delete(temp, true);
        }

        [Test]
        public void ParseKeepsNullsAsBlockedAndReadsRegions()
        {
            var m = MapData.Parse(Json);
            Assert.IsTrue(m.terrain.IsRaw);
            Assert.AreEqual(12, m.terrain.heights.Length);
            Assert.IsFalse(m.terrain.IsWalkableSample(6));
            Assert.IsTrue(m.terrain.IsWalkableSample(5));
            Assert.AreEqual(1, m.regions.Length);
            Assert.AreEqual(42, m.regions[0].@event);
            Assert.AreEqual(1, m.instances.Length);
        }

        [Test]
        public void WalkableCellsRequireAllFourCorners()
        {
            var t = MapData.Parse(Json).terrain;
            Assert.AreEqual(2, WalkGrid.WalkableCellCount(t));
            Assert.IsTrue(WalkGrid.CellWalkable(t, 0, 0));
            Assert.IsTrue(WalkGrid.CellWalkable(t, 0, 1));
            Assert.IsFalse(WalkGrid.CellWalkable(t, 1, 0));
            Assert.IsFalse(WalkGrid.CellWalkable(t, -1, 0));
            Assert.IsFalse(WalkGrid.CellWalkable(t, 3, 0));
        }

        [Test]
        public void RawTerrainNeedsOriginAndMatchingHeights()
        {
            Assert.Throws<FormatException>(() => MapData.Parse(
                @"{""schema"":1,""id"":""x"",""terrain"":{""cell"":100,""width"":2,""depth"":2,""heights"":[0,0,0,0]}}"));
            Assert.Throws<FormatException>(() => MapData.Parse(
                @"{""schema"":1,""id"":""x"",""terrain"":{""origin"":[0,0],""cell"":100,""width"":2,""depth"":2,""heights"":[0,0,0]}}"));
        }

        [Test]
        public void FloorCollidesOnWalkableCellsOnly()
        {
            built = MapLoader.Build(Json, null);
            Assert.AreEqual(2, built.WalkableCells);
            var mc = built.Root.GetComponentInChildren<MeshCollider>();
            Assert.IsNotNull(mc);
            Physics.SyncTransforms();
            var walk = WalkGrid.SampleToWorld(MapData.Parse(Json).terrain, 0.5f, 0.5f);
            Assert.IsTrue(Physics.Raycast(walk + Vector3.up * 2f, Vector3.down, out var hit, 5f), "floor under a walkable cell");
            Assert.AreEqual(0f, hit.point.y, 0.01f);
            var blocked = WalkGrid.SampleToWorld(MapData.Parse(Json).terrain, 1.5f, 0.5f);
            Assert.IsFalse(Physics.Raycast(blocked + Vector3.up * 2f, Vector3.down, 5f, ~0, QueryTriggerInteraction.Ignore), "no floor under a blocked cell");
        }

        [Test]
        public void WallsStandOnTheBlockedSideOfEachWalkableEdge()
        {
            built = MapLoader.Build(Json, null);
            var walls = built.Root.transform.Find("WalkGrid/Walls");
            Assert.IsNotNull(walls);
            Assert.Greater(walls.childCount, 0);
            var t = MapData.Parse(Json).terrain;
            // The edge between walkable column 0 and blocked column 1 is the line of samples x = 1.
            var edgeMid = WalkGrid.SampleToWorld(t, 1f, 0.5f) + Vector3.up;
            bool covered = false;
            Physics.SyncTransforms();
            foreach (var c in walls.GetComponentsInChildren<BoxCollider>())
                if (c.bounds.Contains(edgeMid)) covered = true;
            Assert.IsTrue(covered, "a wall covers the edge");
            // A point well inside the walkable cell is not inside any wall.
            var inside = WalkGrid.SampleToWorld(t, 0.5f, 0.5f) + Vector3.up;
            foreach (var c in walls.GetComponentsInChildren<BoxCollider>())
                Assert.IsFalse(c.bounds.Contains(inside));
        }

        [Test]
        public void RegionsBecomeTriggerVolumesCarryingRawIds()
        {
            built = MapLoader.Build(Json, null);
            Assert.AreEqual(1, built.RegionCount);
            var trig = built.Root.GetComponentInChildren<MapRegionTrigger>();
            Assert.AreEqual(7, trig.index);
            Assert.AreEqual(42, trig.eventId);
            Assert.AreEqual(99, trig.condition);
            var box = trig.GetComponent<BoxCollider>();
            Assert.IsTrue(box.isTrigger);
            var a = FarmCoords.ToWorld(-100, 0, 200); var b = FarmCoords.ToWorld(0, 0, 300);
            Assert.AreEqual(Mathf.Abs(a.x - b.x), box.size.x, 1e-4f);
            Assert.AreEqual(Mathf.Abs(a.z - b.z), box.size.z, 1e-4f);
            Assert.AreEqual((a.x + b.x) * 0.5f, trig.transform.localPosition.x, 1e-4f);
            Assert.AreEqual(FarmCoords.ToWorld(-50, 0, 250).x, trig.arrival.x, 1e-4f);
        }

        [Test]
        public void DefaultSpawnLandsOnWalkableGround()
        {
            built = MapLoader.Build(Json, null);
            Assert.IsTrue(built.Spawns.TryGetValue("default", out var sp));
            var t = MapData.Parse(Json).terrain;
            WalkGrid.NearestWalkable(t, sp.position, out var nearest);
            Assert.AreEqual(0f, Vector3.Distance(sp.position, nearest), 1e-3f);
        }

        [Test]
        public void CoordinateMappingIsScaledAndYDown()
        {
            var p = FarmCoords.ToWorld(100, 200, 300);
            Assert.AreEqual(1f * (FarmCoords.MirrorX ? -1f : 1f) * (FarmCoords.UnitsToMetres * 100f), p.x, 1e-5f);
            Assert.AreEqual(-2f * (FarmCoords.UnitsToMetres * 100f), p.y, 1e-5f);
            Assert.AreEqual(3f * (FarmCoords.UnitsToMetres * 100f), p.z, 1e-5f);
        }

        [Test]
        public void WalkPaceMatchesTheDecompiledRawSpeed()
        {
            // 41 raw units per 30 Hz update (hmpsp.prx FUN_0004786c); see docs/baseline.md.
            Assert.AreEqual(6.15f, 41f * 30f * FarmCoords.UnitsToMetres, 1e-3f);
            Assert.AreEqual(10.95f, 73f * 30f * FarmCoords.UnitsToMetres, 1e-3f);
        }

        [Test]
        public void StaleConvertedMapWithoutTerrainStillParses()
        {
            var m = MapData.Parse(@"{""schema"":1,""id"":""map_080"",""terrain"":null,""regions"":[]}");
            Assert.IsNull(m.terrain);
            built = MapLoader.Build(m, null);
            Assert.AreEqual(0, built.WalkableCells);
        }

        [Test]
        public void OverlayResolveReturnsNullWhenAbsentAndPathsWhenPresent()
        {
            temp = Path.Combine(Path.GetTempPath(), "hm_overlay_" + Guid.NewGuid().ToString("N"));
            Assert.IsNull(FarmOverlay.Resolve(temp, 80), "missing root");
            Assert.IsNull(FarmOverlay.Resolve(null, 80));
            Directory.CreateDirectory(Path.Combine(temp, "maps"));
            Assert.IsNull(FarmOverlay.Resolve(temp, 80), "missing json");
            File.WriteAllText(Path.Combine(temp, "maps", "map_080.json"), Json);
            var p = FarmOverlay.Resolve(temp, 80);
            Assert.IsNotNull(p);
            Assert.IsFalse(p.HasGlb, "glb is optional");
            Directory.CreateDirectory(Path.Combine(temp, "scenes"));
            File.WriteAllBytes(Path.Combine(temp, "scenes", "farm_80.glb"), new byte[] { 1 });
            Assert.IsTrue(FarmOverlay.Resolve(temp, 80).HasGlb);
        }
    }
}

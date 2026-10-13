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
            Assert.AreEqual(1f * FarmCoords.XSign * (FarmCoords.UnitsToMetres * 100f), p.x, 1e-5f);
            Assert.AreEqual(-2f * (FarmCoords.UnitsToMetres * 100f), p.y, 1e-5f);
            Assert.AreEqual(3f * (FarmCoords.UnitsToMetres * 100f), p.z, 1e-5f);
        }

        [Test]
        public void RawPlusXIsWorldPlusX()
        {
            // The GLB is converted once at the source (scene_glb.to_gltf), so no runtime mirror remains.
            Assert.IsFalse(FarmCoords.MirrorX);
            Assert.AreEqual(1f, FarmCoords.XSign);
            Assert.Greater(FarmCoords.ToWorld(100, 0, 0).x, 0f);
            Assert.Greater(FarmCoords.ToWorld(0, 0, 100).z, 0f);
        }

        [Test]
        public void ToRawInvertsToWorld()
        {
            var w = FarmCoords.ToWorld(-1234, 0, 567);
            FarmCoords.ToRaw(w, out var x, out var z);
            Assert.AreEqual(-1234f, x, 1e-2f);
            Assert.AreEqual(567f, z, 1e-2f);
        }

        [Test]
        public void WalkPaceMatchesTheDecompiledRawSpeed()
        {
            // 41 raw units per 30 Hz update (hmpsp.prx FUN_0004786c); see docs/baseline.md.
            Assert.AreEqual(6.15f, FarmCoords.WalkUnitsPerSecond * FarmCoords.DecompiledUnitsToMetres, 1e-3f);
            Assert.AreEqual(10.95f, FarmCoords.RunUnitsPerSecond * FarmCoords.DecompiledUnitsToMetres, 1e-3f);
            Assert.AreEqual(6.15f, FarmCoords.DecompiledMetresPerSecond, 1e-3f);
            Assert.AreEqual(10.95f, FarmCoords.DecompiledRunMetresPerSecond, 1e-3f);
        }

        [Test]
        public void WorldIsOnePointFiveTimesTheDecompiledEstimateByDesign()
        {
            Assert.AreEqual(1.5f, FarmCoords.WorldScale, 1e-6f);
            Assert.AreEqual(0.0075f, FarmCoords.UnitsToMetres, 1e-6f);
            Assert.AreEqual(0.75f, FarmCoords.GlbScale, 1e-6f);
            // Same m/s over a 1.5x larger world: crossing takes 1.5x longer.
            const float rawSpan = 2000f;
            float original = rawSpan * FarmCoords.DecompiledUnitsToMetres / FarmCoords.DecompiledMetresPerSecond;
            float now = rawSpan * FarmCoords.UnitsToMetres / FarmCoords.DecompiledMetresPerSecond;
            Assert.AreEqual(1.5f, now / original, 1e-4f);
        }

        [Test]
        public void StaleConvertedMapWithoutTerrainStillParses()
        {
            var m = MapData.Parse(@"{""schema"":1,""id"":""map_000"",""terrain"":null,""regions"":[]}");
            Assert.IsNull(m.terrain);
            built = MapLoader.Build(m, null);
            Assert.AreEqual(0, built.WalkableCells);
        }

        [Test]
        public void OverlayResolveReturnsNullWhenAbsentAndPathsWhenPresent()
        {
            temp = Path.Combine(Path.GetTempPath(), "hm_overlay_" + Guid.NewGuid().ToString("N"));
            Assert.IsNull(FarmOverlay.Resolve(temp, 0), "missing root");
            Assert.IsNull(FarmOverlay.Resolve(null, 80));
            Directory.CreateDirectory(Path.Combine(temp, "maps"));
            Assert.IsNull(FarmOverlay.Resolve(temp, 0), "missing json");
            File.WriteAllText(Path.Combine(temp, "maps", "map_000.json"), Json);
            var p = FarmOverlay.Resolve(temp, 0);
            Assert.IsNotNull(p);
            Assert.IsFalse(p.HasGlb, "glb is optional");
            Directory.CreateDirectory(Path.Combine(temp, "scenes"));
            File.WriteAllBytes(Path.Combine(temp, "scenes", "farm_00.glb"), new byte[] { 1 });
            Assert.IsTrue(FarmOverlay.Resolve(temp, 0).HasGlb);
            Assert.IsTrue(FarmOverlay.Resolve(temp, 0).Glb.Contains("scenes" + Path.DirectorySeparatorChar + "farm_00"));
            Directory.CreateDirectory(Path.Combine(temp, "scenes_textured"));
            File.WriteAllBytes(Path.Combine(temp, "scenes_textured", "farm_00.glb"), new byte[] { 1 });
            Assert.IsTrue(FarmOverlay.Resolve(temp, 0).Glb.Contains("scenes_textured"), "textured export wins");
        }

        [Test]
        public void DefaultRowIsTheOutdoorYardNotAFarmhouseInterior()
        {
            Assert.AreEqual(0, FarmOverlay.DefaultRow, "rows 0..3 are the yard; 80..95 are interiors");
        }

        [Test]
        public void EntryBecomesTheDefaultSpawn()
        {
            var json = @"{""schema"":1,""id"":""map_000"",""entry"":[-4464,0,4032],""terrain"":null,""regions"":[]}";
            built = MapLoader.Build(MapData.Parse(json), null);
            Assert.IsTrue(built.Spawns.TryGetValue("default", out var sp));
            Assert.AreEqual(FarmCoords.ToWorld(-4464, 0, 4032).x, sp.position.x, 1e-3f);
            Assert.AreEqual(FarmCoords.ToWorld(-4464, 0, 4032).z, sp.position.z, 1e-3f);
        }
    }

    /// <summary>Emulator-measured raw positions (docs/baseline.md) through FarmCoords under the unmirrored handedness.</summary>
    public class FarmBaselinePositionTests
    {
        [Test]
        public void YardNorthExitMapsToPositiveZUnmirrored()
        {
            // Player frame: eye x 2110 and z -1944 minus the camera offset (6363, -6364) = (-4253, 4420), the point where
            // walking up leaves the yard (also the max z of the emulator walk, walk.csv).
            var w = FarmCoords.ToWorld(-4253, 0, 4420);
            Assert.AreEqual(-4253f * 0.0075f, w.x, 1e-3f);
            Assert.AreEqual(4420f * 0.0075f, w.z, 1e-3f);
            Assert.AreEqual(0f, w.y, 1e-6f);
            Assert.Less(w.x, 0f, "raw -x stays -x: no mirror");
            FarmCoords.ToRaw(w, out var rx, out var rz);
            Assert.AreEqual(-4253f, rx, 0.01f); Assert.AreEqual(4420f, rz, 0.01f);
        }

        [Test]
        public void FarmArrivalMapsAcrossHandedness()
        {
            // Farmhouse interior (row 80) region 0 arrival (-4464, 0, 4032) in the yard frame: the yard position after the door.
            var w = FarmCoords.ToWorld(new[] { -4464, 0, 4032 });
            Assert.AreEqual(-33.48f, w.x, 0.01f);
            Assert.AreEqual(30.24f, w.z, 0.01f);
        }
    }
}

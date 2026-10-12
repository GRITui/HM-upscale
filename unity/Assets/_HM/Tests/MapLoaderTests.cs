using HM.Core;
using HM.World;
using NUnit.Framework;
using UnityEngine;

namespace HM.Tests
{
    public class MapLoaderTests
    {
        // Original synthetic map: 3x3 height grid with a hill in the middle.
        const string Json = @"{""schema"":1,""id"":""test_area"",""name"":""Test Area"",
          ""terrain"":{""width"":3,""depth"":3,""cellSize"":2,""heights"":[0,0,0, 0,2,0, 0,0,0],""color"":[0.3,0.5,0.2]},
          ""objects"":[
            {""id"":""o1"",""model"":""mdl_known"",""position"":[1,0,1],""rotationY"":90},
            {""id"":""o2"",""model"":""mdl_unknown"",""position"":[3,0,3],""size"":[2,3,2],""scale"":2}],
          ""spawns"":[{""name"":""default"",""position"":[2,2,2],""rotationY"":180},{""name"":""gate"",""position"":[0,0,0]}],
          ""colliders"":[{""center"":[2,1,2],""size"":[1,2,1],""rotationY"":45}]}";

        GameObject known;
        BuiltMap built;

        [TearDown]
        public void TearDown()
        {
            if (built != null) Object.DestroyImmediate(built.Root);
            if (known != null) Object.DestroyImmediate(known);
        }

        DictionaryModelSource Source()
        {
            known = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var src = new DictionaryModelSource();
            src.Add("mdl_known", known);
            return src;
        }

        [Test]
        public void BuildsTerrainObjectsSpawnsAndColliders()
        {
            built = MapLoader.Build(Json, Source());
            Assert.AreEqual(1, built.ModelCount);
            Assert.AreEqual(1, built.PlaceholderCount);
            Assert.IsTrue(built.Spawns.ContainsKey("default") && built.Spawns.ContainsKey("gate"));
            Assert.AreEqual(180f, built.Spawns["default"].eulerAngles.y, 0.01f);

            var mesh = built.Root.transform.Find("Terrain").GetComponent<MeshFilter>().sharedMesh;
            Assert.AreEqual(9, mesh.vertexCount);
            Assert.AreEqual(2 * 2 * 6, mesh.triangles.Length);
            Assert.AreEqual(2f, mesh.bounds.max.y, 1e-4f);
            Assert.IsNotNull(built.Root.transform.Find("Terrain").GetComponent<MeshCollider>());

            var box = built.Root.transform.Find("Colliders/Collider 0").GetComponent<BoxCollider>();
            Assert.AreEqual(new Vector3(1, 2, 1), box.size);
        }

        [Test]
        public void MissingModelBecomesScaledPlaceholder()
        {
            built = MapLoader.Build(Json, Source());
            var o2 = built.Root.transform.Find("Objects/o2");
            Assert.IsNotNull(o2.GetComponent<MeshRenderer>());
            Assert.AreEqual(new Vector3(4, 6, 4), o2.localScale);
            Assert.AreEqual(new Vector3(3, 0, 3), o2.localPosition);
        }

        [Test]
        public void NullSourceStillBuildsEverythingAsPlaceholders()
        {
            built = MapLoader.Build(Json, null);
            Assert.AreEqual(2, built.PlaceholderCount);
            Assert.AreEqual(0, built.ModelCount);
        }

        [Test]
        public void TerrainFaceNormalsPointUp()
        {
            built = MapLoader.Build(Json, null);
            var mesh = built.Root.transform.Find("Terrain").GetComponent<MeshFilter>().sharedMesh;
            foreach (var n in mesh.normals) Assert.Greater(n.y, 0f);
        }

        [Test]
        public void SampleHeightInterpolates()
        {
            var data = MapData.Parse(Json);
            Assert.AreEqual(2f, MapLoader.SampleHeight(data.terrain, 2f, 2f), 1e-4f);
            Assert.AreEqual(1f, MapLoader.SampleHeight(data.terrain, 1f, 2f), 1e-4f);
            Assert.AreEqual(0f, MapLoader.SampleHeight(data.terrain, 0f, 0f), 1e-4f);
        }

        [Test]
        public void BadMapsAreRejected()
        {
            Assert.Throws<System.FormatException>(() => MapData.Parse(@"{""schema"":9,""id"":""x""}"));
            Assert.Throws<System.FormatException>(() => MapData.Parse(@"{""schema"":1}"));
            Assert.Throws<System.FormatException>(() => MapData.Parse(@"{""schema"":1,""id"":""x"",""terrain"":{""width"":2,""depth"":2,""heights"":[0,0,0]}}"));
            Assert.Throws<System.ArgumentException>(() => MapData.Parse(""));
        }
    }

    public class FrameTimeStatsTests
    {
        [Test]
        public void PercentilesAndBudget()
        {
            var s = new FrameTimeStats();
            for (int i = 1; i <= 100; i++) s.Add(i * 0.2f); // 0.2 .. 20.0
            Assert.AreEqual(100, s.Count);
            Assert.AreEqual(19f, s.Percentile(95), 1e-3f);
            Assert.AreEqual(20f, s.Max, 1e-3f);
            Assert.AreEqual(10.1f, s.Average, 1e-3f);
            Assert.Greater(s.OverBudget, 0);
            StringAssert.Contains("p95=19.00ms", s.Summary());
        }

        [Test]
        public void EmptyIsZero()
        {
            var s = new FrameTimeStats();
            Assert.AreEqual(0f, s.Average);
            Assert.AreEqual(0f, s.Percentile(99));
        }
    }
}

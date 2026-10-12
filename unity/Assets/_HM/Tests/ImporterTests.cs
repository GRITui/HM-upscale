using System;
using System.IO;
using HM.Importers;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace HM.Tests
{
    public class ImporterTests
    {
        const string Dest = "Assets/_HM/Imported/_test";
        string root;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "hm-conv-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root + "/models");
            Directory.CreateDirectory(root + "/textures");
            Directory.CreateDirectory(root + "/data");
            Directory.CreateDirectory(root + "/audio");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
            ManifestImporter.Clean(Dest);
        }

        /// <summary>Original synthetic unit cube as a self-contained glTF 2.0 (positions + indices, base64 buffer).</summary>
        public static string CubeGltf()
        {
            float[] pos =
            {
                -.5f,-.5f,-.5f,  .5f,-.5f,-.5f,  .5f,.5f,-.5f,  -.5f,.5f,-.5f,
                -.5f,-.5f, .5f,  .5f,-.5f, .5f,  .5f,.5f, .5f,  -.5f,.5f, .5f,
            };
            ushort[] idx =
            {
                0,2,1, 0,3,2,  4,5,6, 4,6,7,  0,1,5, 0,5,4,
                3,6,2, 3,7,6,  0,4,7, 0,7,3,  1,2,6, 1,6,5,
            };
            var bytes = new byte[pos.Length * 4 + idx.Length * 2];
            Buffer.BlockCopy(pos, 0, bytes, 0, pos.Length * 4);
            Buffer.BlockCopy(idx, 0, bytes, pos.Length * 4, idx.Length * 2);
            string b64 = Convert.ToBase64String(bytes);
            return "{\"asset\":{\"version\":\"2.0\",\"generator\":\"hm-test\"},"
                 + "\"scene\":0,\"scenes\":[{\"nodes\":[0]}],\"nodes\":[{\"mesh\":0,\"name\":\"cube\"}],"
                 + "\"meshes\":[{\"primitives\":[{\"attributes\":{\"POSITION\":0},\"indices\":1}]}],"
                 + "\"accessors\":[{\"bufferView\":0,\"componentType\":5126,\"count\":8,\"type\":\"VEC3\",\"min\":[-0.5,-0.5,-0.5],\"max\":[0.5,0.5,0.5]},"
                 + "{\"bufferView\":1,\"componentType\":5123,\"count\":" + idx.Length + ",\"type\":\"SCALAR\"}],"
                 + "\"bufferViews\":[{\"buffer\":0,\"byteOffset\":0,\"byteLength\":" + pos.Length * 4 + ",\"target\":34962},"
                 + "{\"buffer\":0,\"byteOffset\":" + pos.Length * 4 + ",\"byteLength\":" + idx.Length * 2 + ",\"target\":34963}],"
                 + "\"buffers\":[{\"byteLength\":" + bytes.Length + ",\"uri\":\"data:application/octet-stream;base64," + b64 + "\"}]}";
        }

        static byte[] GeneratedPng()
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++) tex.SetPixel(x, y, (x + y) % 2 == 0 ? Color.white : Color.gray);
            tex.Apply();
            var png = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);
            return png;
        }

        void WriteFixture(string manifestJson)
        {
            File.WriteAllText(root + "/models/box.gltf", CubeGltf());
            File.WriteAllBytes(root + "/textures/checker.png", GeneratedPng());
            File.WriteAllText(root + "/data/items.json", "{\"items\":[]}");
            File.WriteAllText(root + "/manifest.json", manifestJson);
        }

        const string GoodManifest = @"{""schema"":1,""assets"":[
          {""id"":""mdl_box"",""type"":""model"",""path"":""models/box.gltf"",""tags"":[""prop"",""synthetic""],""license"":""original-synthetic""},
          {""id"":""tex_checker"",""type"":""texture"",""path"":""textures/checker.png"",""tags"":[""synthetic""],""license"":""original-synthetic""},
          {""id"":""data_items"",""type"":""data"",""path"":""data/items.json"",""tags"":[],""license"":""original-synthetic""}]}";

        [Test]
        public void ManifestValidationCatchesBadEntries()
        {
            var m = AssetManifest.Parse(@"{""schema"":1,""assets"":[
              {""id"":""Bad Id"",""type"":""model"",""path"":""a.gltf"",""license"":""x""},
              {""id"":""ok"",""type"":""model"",""path"":""../a.gltf"",""license"":""x""},
              {""id"":""ok2"",""type"":""texture"",""path"":""a.gltf"",""license"":""x""},
              {""id"":""ok3"",""type"":""blob"",""path"":""a.bin"",""license"":""x""},
              {""id"":""ok4"",""type"":""audio"",""path"":""a.ogg"",""license"":""""},
              {""id"":""ok4"",""type"":""audio"",""path"":""b.ogg"",""license"":""x""}]}");
            var problems = m.Validate();
            Assert.GreaterOrEqual(problems.Count, 6, string.Join("\n", problems));
        }

        [Test]
        public void ManifestAcceptsGoodEntries()
        {
            Assert.IsEmpty(AssetManifest.Parse(GoodManifest).Validate());
        }

        [Test]
        public void FutureSchemaRejected() =>
            Assert.Throws<FormatException>(() => AssetManifest.Parse("{\"schema\":99}"));

        [Test]
        public void MissingManifestIsReportedNotThrown()
        {
            var r = ManifestImporter.Import(root, Dest);
            Assert.IsFalse(r.Ok);
        }

        [Test]
        public void InvalidManifestImportsNothing()
        {
            File.WriteAllText(root + "/manifest.json", @"{""schema"":1,""assets"":[{""id"":""X"",""type"":""model"",""path"":""a.gltf"",""license"":""x""}]}");
            var r = ManifestImporter.Import(root, Dest);
            Assert.IsFalse(r.Ok);
            Assert.IsEmpty(r.Imported);
        }

        [Test]
        public void ImportsModelTextureAndDataIntoAddressables()
        {
            WriteFixture(GoodManifest);
            var r = ManifestImporter.Import(root, Dest);
            Assert.IsTrue(r.Ok, string.Join("\n", r.Errors));
            CollectionAssert.AreEquivalent(new[] { "mdl_box", "tex_checker", "data_items" }, r.Imported);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Dest + "/prefab/mdl_box.prefab");
            Assert.IsNotNull(prefab, "prefab should exist");
            Assert.IsNotNull(prefab.GetComponentInChildren<MeshFilter>(), "prefab should contain the glTF mesh");
            Assert.AreEqual(12 * 3, prefab.GetComponentInChildren<MeshFilter>().sharedMesh.triangles.Length);
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<Texture2D>(Dest + "/texture/tex_checker.png"));
            Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<TextAsset>(Dest + "/data/data_items.json"));
            // Original file names must not leak into the project.
            Assert.IsFalse(File.Exists(Dest + "/model/box.gltf"));

            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            Assert.IsNotNull(settings.FindGroup("HM_Model"));
            Assert.IsNotNull(settings.FindGroup("HM_Texture"));
            Assert.IsNotNull(settings.FindGroup("HM_Data"));
            var entry = settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(Dest + "/prefab/mdl_box.prefab"));
            Assert.IsNotNull(entry);
            Assert.AreEqual("mdl_box", entry.address);
            Assert.IsTrue(entry.labels.Contains("prop"));
        }

        [Test]
        public void ReimportIsIdempotent()
        {
            WriteFixture(GoodManifest);
            Assert.IsTrue(ManifestImporter.Import(root, Dest).Ok);
            Assert.IsTrue(ManifestImporter.Import(root, Dest).Ok);
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            Assert.AreEqual(1, settings.FindGroup("HM_Model").entries.Count);
        }

        [Test]
        public void MissingFileIsAnError()
        {
            File.WriteAllText(root + "/manifest.json", @"{""schema"":1,""assets"":[{""id"":""gone"",""type"":""texture"",""path"":""textures/none.png"",""license"":""x""}]}");
            var r = ManifestImporter.Import(root, Dest);
            Assert.IsFalse(r.Ok);
        }
    }
}

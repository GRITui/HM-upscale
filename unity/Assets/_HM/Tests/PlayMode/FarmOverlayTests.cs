using System;
using System.Collections;
using System.IO;
using HM.Characters;
using HM.Gameplay;
using HM.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HM.Tests.PlayMode
{
    /// <summary>Farm overlay in the real SliceFarm scene: clean fallback, and (when converted data exists locally) walk-grid collision.</summary>
    public class FarmOverlayTests
    {
        [Test]
        public void RunIsCircleAndSeventyThreeOverFortyOne()
        {
            Assert.AreEqual("<Gamepad>/buttonEast", PlayerMover.RunGamepadBinding);
            Assert.IsNotEmpty(PlayerMover.RunKeyboardBinding);
            Assert.AreEqual(73f / 41f, FarmCoords.DecompiledRunMetresPerSecond / FarmCoords.DecompiledMetresPerSecond, 1e-4f);
        }

        [TearDown]
        public void Cleanup()
        {
            Environment.SetEnvironmentVariable(FarmOverlay.EnvDisable, null);
            Environment.SetEnvironmentVariable(FarmOverlay.EnvRoot, null);
        }

        static IEnumerator Load()
        {
            var op = SceneManager.LoadSceneAsync("SliceFarm", LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
        }

        static IEnumerator Shot(string name)
        {
            string dir = Environment.GetEnvironmentVariable("HM_SHOT_DIR");
            if (string.IsNullOrEmpty(dir)) yield break;
            Directory.CreateDirectory(dir);
            var cam = Camera.main;
            var rt = new RenderTexture(1280, 720, 24);
            var prev = cam.targetTexture;
            cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev;
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.Destroy(tex); rt.Release(); UnityEngine.Object.Destroy(rt);
        }

        [UnityTest]
        public IEnumerator MissingConvertedDataFallsBackToPlaceholderSlice()
        {
            // The repo-relative candidate always applies, so this fallback is only observable on a checkout without converted data.
            if (FarmOverlay.Find() != null) Assert.Ignore("converted farm present locally; fallback covered by FarmMapTests path resolution");
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"\[HM-FARM\] converted farm not found"));
            yield return Load();
            yield return new WaitForSeconds(0.3f);
            Assert.IsNotNull(GameObject.Find("Ground"), "placeholder ground stays");
            Assert.IsNotNull(UnityEngine.Object.FindFirstObjectByType<PlayerMover>());
        }

        [UnityTest]
        public IEnumerator DisabledOverlayLogsAndKeepsPlaceholderSlice()
        {
            Environment.SetEnvironmentVariable(FarmOverlay.EnvDisable, "0");
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex(@"\[HM-FARM\] overlay disabled"));
            yield return Load();
            yield return new WaitForSeconds(0.3f);
            Assert.IsFalse(FarmOverlayBootstrap.Loaded);
            Assert.IsTrue(GameObject.Find("Ground").activeSelf);
        }

        [UnityTest]
        public IEnumerator RealFarmWalkGridContainsThePlayer()
        {
            if (FarmOverlay.Find() == null) Assert.Ignore("converted farm not present locally");
            yield return Load();
            float t = 0f;
            while (!FarmOverlayBootstrap.GlbLoaded && t < 30f) { t += Time.unscaledDeltaTime; yield return null; }
            Assert.IsTrue(FarmOverlayBootstrap.Loaded, "overlay loaded");
            var ov = UnityEngine.Object.FindFirstObjectByType<FarmOverlayBootstrap>();
            var terrain = ov.Built.Data.terrain;
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerMover>();
            var cc = player.GetComponent<CharacterController>();
            Assert.IsNull(GameObject.Find("Ground"), "hand-placed ground is hidden");
            yield return new WaitForSeconds(0.5f);
            yield return Shot("farm_overlay_default");

            // Push the character hard in every direction; the wall boxes must keep it on the walkable grid.
            Vector3[] dirs = { Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
            foreach (var d in dirs)
                for (int i = 0; i < 400; i++)
                {
                    cc.Move(d * 0.5f + Vector3.down * 0.1f);
                    if (i % 20 == 0) yield return null;
                }
            WalkGrid.NearestWalkable(terrain, player.transform.position, out var near);
            float dist = Vector2.Distance(new Vector2(near.x, near.z), new Vector2(player.transform.position.x, player.transform.position.z));
            Assert.Less(dist, 0.75f, "player stays on walkable cells (distance to nearest cell centre)");
            Assert.AreEqual(0f, player.transform.position.y, 0.2f, "player rests on the floor");

            // Debug view: show the collision floor over the GLB to check scale and handedness line up.
            foreach (var r in ov.Built.Root.GetComponentsInChildren<MeshRenderer>(true)) { r.enabled = true; r.transform.position += Vector3.up * 0.05f; }
            player.transform.position = ov.Built.Spawns["default"].position;
            yield return null;
            yield return Shot("farm_overlay_collision_floor");
        }

        [UnityTest]
        public IEnumerator TeardownMidGlbLoadLogsNothing()
        {
            if (FarmOverlay.Find() == null) Assert.Ignore("converted farm not present locally");
            var seen = new System.Collections.Generic.List<string>();
            Application.LogCallback cb = (msg, _, type) => { if (msg.Contains("[HM-FARM] scene GLB failed") || msg.Contains("has been destroyed")) seen.Add(msg); };
            Application.logMessageReceived += cb;
            try
            {
                yield return Load();
                // Wait until the overlay is built but cut the scene away before the GLB finishes instantiating.
                float t = 0f;
                while (!FarmOverlayBootstrap.Loaded && t < 30f) { t += Time.unscaledDeltaTime; yield return null; }
                var ov = UnityEngine.Object.FindFirstObjectByType<FarmOverlayBootstrap>();
                Assert.IsNotNull(ov);
                UnityEngine.Object.Destroy(ov.gameObject);
                for (int i = 0; i < 120; i++) yield return null;
                var empty = SceneManager.CreateScene("TeardownProbe");
                SceneManager.SetActiveScene(empty);
                yield return null;
            }
            finally { Application.logMessageReceived -= cb; }
            Assert.IsEmpty(seen, "no destroyed-object noise after teardown: " + string.Join(" | ", seen));
        }
    }
}

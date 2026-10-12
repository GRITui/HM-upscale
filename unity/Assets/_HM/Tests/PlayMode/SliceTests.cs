using System.Collections;
using System.IO;
using HM.Characters;
using HM.Core;
using HM.Farming;
using HM.Gameplay;
using HM.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HM.Tests.PlayMode
{
    /// <summary>Farm slice: day cycle in the real scene and the single end-to-end loop.</summary>
    public class SliceTests : InputTestFixture
    {
        PlayerMover player;
        PlayerInteractor actor;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
        }

        [TearDown]
        public void DestroyPersistentClock()
        {
            if (GameClockBehaviour.Instance != null) Object.DestroyImmediate(GameClockBehaviour.Instance.gameObject);
        }

        IEnumerator Load()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            var op = SceneManager.LoadSceneAsync("SliceFarm", LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
            player = Object.FindFirstObjectByType<PlayerMover>();
            actor = player.GetComponent<PlayerInteractor>();
            Assert.IsNotNull(actor, "player needs a PlayerInteractor");
            yield return new WaitForSeconds(0.2f);
        }

        void Place(Vector3 p)
        {
            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            player.transform.SetPositionAndRotation(p, Quaternion.identity); // faces +Z
            cc.enabled = true;
        }

        /// <summary>Stand so the held tool lands on the given tile, then press the act key path.</summary>
        void ActOn(FarmWorld w, TilePos t)
        {
            Place(w.TileCentre(t) - Vector3.forward * actor.reach);
            actor.Act();
        }

        IEnumerator Shot(string name)
        {
            string dir = System.Environment.GetEnvironmentVariable("HM_SHOT_DIR");
            if (string.IsNullOrEmpty(dir)) yield break;
            Directory.CreateDirectory(dir);
            // Render the main camera into a texture; WaitForEndOfFrame never fires in batchmode.
            var cam = Camera.main;
            var rt = new RenderTexture(1280, 720, 24);
            var prev = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prev;
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex); rt.Release(); Object.Destroy(rt);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SleepingAfterMidnightForcesWakeSameDay()
        {
            yield return Load();
            var clock = GameClockBehaviour.Instance.Clock;
            int day = clock.DayIndex;
            clock.SetTotalMinutes(day * GameClock.MinutesPerDay + 2 * 60 + 5);
            yield return new WaitForSeconds(1.5f);
            Assert.AreEqual(1, SleepController.Instance.SleepCount, "collapse at 02:00");
            Assert.AreEqual(day, clock.DayIndex, "no extra rollover after midnight");
            Assert.GreaterOrEqual(clock.HourOfDay, 6f);
        }

        [UnityTest]
        public IEnumerator SunIsBrighterAtNoonThanAtNight()
        {
            yield return Load();
            var sun = Object.FindFirstObjectByType<DayNightLight>();
            var light = sun.GetComponent<Light>();
            sun.Apply(0.5f); float noon = light.intensity;
            sun.Apply(0.0f); float night = light.intensity;
            Assert.Greater(noon, night * 3f);
        }

        [UnityTest]
        public IEnumerator PlantWaterSleepGrowAndTalkToNpc()
        {
            yield return Load();
            var w = FarmWorld.Instance;
            var clock = GameClockBehaviour.Instance.Clock;
            clock.SetTotalMinutes(clock.DayIndex * GameClock.MinutesPerDay + 9 * 60); // morning, far from forced sleep
            var tile = new TilePos(2, 1);
            int day0 = clock.DayIndex;

            w.SelectTool(Tool.Hoe); ActOn(w, tile);
            Assert.AreEqual(SoilState.Tilled, w.Sim.TileAt(tile).Soil);
            w.SelectTool(Tool.SeedBag); ActOn(w, tile);
            Assert.IsNotNull(w.Sim.TileAt(tile).CropId, "seed planted");
            w.SelectTool(Tool.WateringCan); ActOn(w, tile);
            Assert.IsTrue(w.Sim.TileAt(tile).Watered);
            Assert.AreEqual(0, w.Sim.TileAt(tile).DaysGrown);
            yield return null;
            yield return Shot("farm_planted");

            // Talk: planting today unlocks the gated third choice.
            var mira = Object.FindFirstObjectByType<NpcDialogue>();
            Place(mira.transform.position - Vector3.forward * 1.2f);
            actor.Act();
            Assert.AreSame(mira, NpcDialogue.Active, "dialogue opens");
            Assert.AreEqual(3, mira.Runner.VisibleChoices().Count);
            yield return null;
            yield return Shot("dialogue");
            mira.Close();

            // Sleep in the bed through the real interactable and fade.
            var bed = Object.FindFirstObjectByType<Bed>();
            Place(bed.transform.position - Vector3.forward * 1.2f);
            actor.Act();
            float guard = 0f;
            while ((SleepController.Instance.Sleeping || SleepController.Instance.SleepCount == 0) && guard < 5f) { guard += Time.unscaledDeltaTime; yield return null; }
            Assert.AreEqual(1, SleepController.Instance.SleepCount);
            Assert.AreEqual(day0 + 1, clock.DayIndex);
            Assert.AreEqual(1, w.Sim.TileAt(tile).DaysGrown, "watered crop advanced overnight");
            Assert.IsFalse(w.Sim.TileAt(tile).Watered);
            Assert.AreEqual(6f, clock.HourOfDay, 0.5f);
            yield return Shot("next_morning");

            // Next day the flag is gone, so the gated choice is hidden, but Mira still talks.
            Place(mira.transform.position - Vector3.forward * 1.2f);
            actor.Act();
            Assert.AreSame(mira, NpcDialogue.Active);
            Assert.AreEqual(2, mira.Runner.VisibleChoices().Count);
        }
    }
}

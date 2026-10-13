using System.Collections;
using System.IO;
using HM.Characters;
using HM.Core;
using HM.Gameplay;
using HM.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HM.Tests.PlayMode
{
    /// <summary>Runs the real SliceFarm scene in play mode. Unity fails each test on any logged error or exception.</summary>
    public class PlayModeTests : InputTestFixture
    {
        const string SceneName = "SliceFarm"; // listed in build settings by ProjectSetup
        Keyboard keyboard;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        [TearDown]
        public void DestroyPersistentClock()
        {
            System.Environment.SetEnvironmentVariable(FarmOverlay.EnvDisable, null);
            // The clock host uses DontDestroyOnLoad, so remove it between tests.
            if (GameClockBehaviour.Instance != null) Object.DestroyImmediate(GameClockBehaviour.Instance.gameObject);
        }

        IEnumerator LoadFarm()
        {
            // Headless runs otherwise spin at thousands of fps, and per-frame moves fall below
            // CharacterController.minMoveDistance. Pin a realistic frame rate.
            // These tests exercise the hand-placed slice; the converted farm overlay (present only on machines with
            // local converted data) moves the player asynchronously and would make them machine dependent.
            System.Environment.SetEnvironmentVariable(FarmOverlay.EnvDisable, "0");
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            var op = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerMovesWhenKeyHeld()
        {
            yield return LoadFarm();
            var player = Object.FindFirstObjectByType<PlayerMover>();
            Assert.IsNotNull(player, "SliceFarm must contain a PlayerMover");
            yield return new WaitForSeconds(0.3f); // settle onto the ground
            Vector3 start = player.transform.position;

            Press(keyboard.wKey);
            int frames = 0; float simTime = 0f;
            while (simTime < 0.6f && frames < 200000) { yield return null; simTime += Time.deltaTime; frames++; }
            Release(keyboard.wKey);

            float moved = Vector3.Distance(new Vector3(start.x, 0, start.z), new Vector3(player.transform.position.x, 0, player.transform.position.z));
            Assert.Greater(moved, 0.5f, "player should travel when W is held");
        }

        [UnityTest]
        public IEnumerator PlayerStaysPutWithoutInput()
        {
            yield return LoadFarm();
            var player = Object.FindFirstObjectByType<PlayerMover>();
            yield return new WaitForSeconds(0.3f);
            Vector3 start = player.transform.position;
            yield return new WaitForSeconds(0.4f);
            Assert.Less(Vector3.Distance(start, player.transform.position), 0.05f);
        }

        [UnityTest]
        public IEnumerator ClockAdvancesAndSunFollowsIt()
        {
            yield return LoadFarm();
            var host = GameClockBehaviour.Instance;
            Assert.IsNotNull(host, "a GameClockBehaviour must exist in the scene");
            var sun = Object.FindFirstObjectByType<DayNightLight>();
            Assert.IsNotNull(sun);
            double t0 = host.Clock.TotalMinutes;
            Quaternion r0 = sun.transform.rotation;

            yield return new WaitForSeconds(1.0f);

            double dt = host.Clock.TotalMinutes - t0;
            Assert.Greater(dt, 1.0, "at 2 game minutes per second the clock must move by more than a minute in one second");
            Assert.Less(dt, 4.0);
            Assert.Greater(Quaternion.Angle(r0, sun.transform.rotation), 0.1f, "sun should rotate as time passes");
        }

        [UnityTest]
        public IEnumerator DayRollsOver()
        {
            yield return LoadFarm();
            var clock = GameClockBehaviour.Instance.Clock;
            int days = 0;
            clock.DayChanged += _ => days++;
            clock.SetTotalMinutes(GameClock.MinutesPerDay - 1);
            yield return new WaitForSeconds(1.0f);
            Assert.AreEqual(1, days);
            Assert.AreEqual(1, clock.DayIndex);
        }

        [UnityTest]
        public IEnumerator SaveLoadRoundTripInScene()
        {
            yield return LoadFarm();
            var clock = GameClockBehaviour.Instance.Clock;
            var player = Object.FindFirstObjectByType<PlayerMover>().transform;
            string dir = Path.Combine(Path.GetTempPath(), "hm-pm-" + System.Guid.NewGuid().ToString("N"));
            try
            {
                clock.Paused = true;
                clock.SetTotalMinutes(5000);
                player.GetComponent<CharacterController>().enabled = false;
                player.position = new Vector3(4f, 1.1f, -3f);
                player.GetComponent<CharacterController>().enabled = true;

                var sys = new SaveSystem(dir);
                sys.Save("slot", SaveBridge.Capture(clock, player, 250));

                // Change the world, then restore it from disk.
                clock.SetTotalMinutes(10);
                SaveBridge.Apply(new SaveData { playerX = -8f, playerY = 1.1f, playerZ = 8f }, clock, player);
                yield return null;
                Assert.AreEqual(-8f, player.position.x, 0.01f);

                Assert.AreEqual(SaveLoadResult.Ok, sys.TryLoad("slot", out var data));
                SaveBridge.Apply(data, clock, player);
                yield return null;

                Assert.AreEqual(5000.0, clock.TotalMinutes, 1e-6);
                Assert.AreEqual(4f, player.position.x, 0.05f);
                Assert.AreEqual(-3f, player.position.z, 0.05f);
                Assert.AreEqual(250, data.gold);
            }
            finally
            {
                clock.Paused = false;
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [UnityTest]
        public IEnumerator MapLoaderBuildsAreaWithPlaceholderFallback()
        {
            const string json = @"{""schema"":1,""id"":""pm_map"",""name"":""t"",
              ""terrain"":{""width"":3,""depth"":3,""cellSize"":2,""heights"":[0,0,0,0,1,0,0,0,0]},
              ""objects"":[{""id"":""a"",""model"":""missing_model"",""position"":[1,1,1],""size"":[1,2,1]}],
              ""spawns"":[{""name"":""default"",""position"":[2,1,2]}],
              ""colliders"":[{""center"":[0,0,0],""size"":[1,1,1]}]}";
            var built = MapLoader.Build(json, new DictionaryModelSource());
            yield return null;
            Assert.AreEqual(1, built.PlaceholderCount);
            Assert.IsTrue(built.Spawns.ContainsKey("default"));
            Assert.IsNotNull(built.Root.GetComponentInChildren<MeshCollider>());
            Object.Destroy(built.Root);
        }
    }
}

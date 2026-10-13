using System.Collections;
using HM.Audio;
using HM.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HM.Tests.PlayMode
{
    public class AreaSceneTests
    {
        [TearDown]
        public void Cleanup()
        {
            if (GameClockBehaviour.Instance != null) Object.DestroyImmediate(GameClockBehaviour.Instance.gameObject);
        }

        [UnityTest]
        public IEnumerator LoadsAreaAdditivelyAndUnloadsIt()
        {
            var svc = new AreaSceneService();
            string loadedEvent = null, unloadedEvent = null;
            svc.AreaLoaded += a => loadedEvent = a;
            svc.AreaUnloaded += a => unloadedEvent = a;
            int scenesBefore = SceneManager.sceneCount;

            yield return svc.Load("SliceFarm");
            Assert.IsTrue(svc.IsLoaded("SliceFarm"));
            Assert.AreEqual("SliceFarm", svc.Current);
            Assert.AreEqual("SliceFarm", SceneManager.GetActiveScene().name);
            Assert.AreEqual(scenesBefore + 1, SceneManager.sceneCount, "additive: existing scenes stay");
            Assert.AreEqual("SliceFarm", loadedEvent);

            yield return svc.Load("SliceFarm"); // idempotent
            Assert.AreEqual(1, svc.Loaded.Count);

            yield return svc.Unload("SliceFarm");
            Assert.IsFalse(svc.IsLoaded("SliceFarm"));
            Assert.IsNull(svc.Current);
            Assert.AreEqual("SliceFarm", unloadedEvent);
            Assert.AreEqual(scenesBefore, SceneManager.sceneCount);
        }

        [UnityTest]
        public IEnumerator PersistentBootSceneSurvivesAreaSwitch()
        {
            var op = SceneManager.LoadSceneAsync("Boot", UnityEngine.SceneManagement.LoadSceneMode.Single);
            while (!op.isDone) yield return null;
            // Boot's GameStartup loads SliceFarm additively on Start.
            float until = Time.realtimeSinceStartup + 20f; // real time: batch mode renders thousands of frames per second
            while (Time.realtimeSinceStartup < until && !(GameStartup.Areas != null && GameStartup.Areas.IsLoaded("SliceFarm"))) yield return null;
            Assert.IsNotNull(GameStartup.Areas);
            Assert.IsTrue(GameStartup.Areas.IsLoaded("SliceFarm"));
            Assert.AreEqual("SliceFarm", SceneManager.GetActiveScene().name);
            Assert.IsTrue(SceneManager.GetSceneByName("Boot").isLoaded);

            yield return GameStartup.Areas.Unload("SliceFarm");
            Assert.IsTrue(SceneManager.GetSceneByName("Boot").isLoaded, "Boot persists when the area goes");
            Assert.IsFalse(SceneManager.GetSceneByName("SliceFarm").isLoaded);
        }

        [UnityTest]
        public IEnumerator UnknownSceneThrowsInsteadOfHanging()
        {
            var svc = new AreaSceneService();
            LogAssert.ignoreFailingMessages = true;
            bool threw = false;
            var it = svc.Load("NoSuchArea");
            try { while (it.MoveNext()) { } } catch (System.InvalidOperationException) { threw = true; }
            LogAssert.ignoreFailingMessages = false;
            Assert.IsTrue(threw);
            Assert.IsFalse(svc.IsLoaded("NoSuchArea"));
            yield return null;
        }
    }

    public class AudioServiceTests
    {
        GameObject go;
        AudioService svc;
        readonly DictionaryClipSource clips = new DictionaryClipSource();

        [SetUp]
        public void SetUp()
        {
            var catalog = AudioCatalog.FromManifestJson(@"{""schema"":1,""assets"":[
              {""id"":""se_000"",""type"":""audio"",""path"":""a.ogg"",""tags"":[""se""],""license"":""x""},
              {""id"":""track_001"",""type"":""audio"",""path"":""b.ogg"",""tags"":[""track""],""license"":""x""}]}");
            clips.Add("se_000", AudioClip.Create("se_000", 44100, 1, 44100, false));
            clips.Add("track_001", AudioClip.Create("track_001", 44100, 1, 44100, false));
            go = new GameObject("audio");
            svc = go.AddComponent<AudioService>();
            svc.Initialize(catalog, clips);
        }

        [TearDown]
        public void TearDown() { Object.Destroy(go); }

        [UnityTest]
        public IEnumerator PlaysMusicByIdAndLoops()
        {
            Assert.IsTrue(svc.PlayMusic("track_001"));
            yield return null;
            Assert.AreEqual("track_001", svc.CurrentMusicId);
            Assert.IsTrue(svc.MusicPlaying);
            svc.StopMusic();
            Assert.IsNull(svc.CurrentMusicId);
        }

        [UnityTest]
        public IEnumerator SfxUsesVoicesAndLoopsCanBeStopped()
        {
            Assert.IsTrue(svc.PlaySfx("se_000"));
            int loop = svc.PlaySfxLoop("se_000");
            Assert.GreaterOrEqual(loop, 0);
            yield return null;
            Assert.AreEqual(2, svc.ActiveSfxVoices);
            svc.StopSfx(loop);
            Assert.AreEqual(1, svc.ActiveSfxVoices);
        }

        [Test]
        public void WrongKindOrUnknownIdIsRejectedWithoutThrowing()
        {
            Assert.IsFalse(svc.PlayMusic("se_000"), "an sfx id is not a track");
            Assert.IsFalse(svc.PlaySfx("track_001"));
            Assert.IsFalse(svc.PlaySfx("nope"));
            Assert.IsFalse(svc.PlayMusic(null));
        }

        [Test]
        public void VolumesAreClampedAndApplied()
        {
            svc.MasterVolume = 2f;
            Assert.AreEqual(1f, svc.MasterVolume);
            svc.MusicVolume = -1f;
            Assert.AreEqual(0f, svc.MusicVolume);
            svc.MusicVolume = 0.5f;
            svc.MasterVolume = 0.5f;
            svc.PlayMusic("track_001");
            Assert.AreEqual(0.25f, go.GetComponents<AudioSource>()[0].volume, 0.0001f);
        }
    }
}

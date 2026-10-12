using HM.Audio;
using HM.CameraRig;
using HM.Core;
using HM.Tools;
using HM.World;
using NUnit.Framework;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;

namespace HM.Tests
{
    public class AudioCatalogTests
    {
        const string Json = @"{""schema"":1,""assets"":[
          {""id"":""se_000"",""type"":""audio"",""path"":""audio/se/se_000.ogg"",""tags"":[""se"",""vag""],""license"":""x""},
          {""id"":""track_001"",""type"":""audio"",""path"":""audio/t.ogg"",""tags"":[""track"",""atrac3plus""],""license"":""x""},
          {""id"":""face_000"",""type"":""texture"",""path"":""t.png"",""tags"":[""portrait""],""license"":""x""}]}";

        [Test]
        public void SplitsMusicFromSfxAndIgnoresOtherTypes()
        {
            var c = AudioCatalog.FromManifestJson(Json);
            CollectionAssert.AreEqual(new[] { "track_001" }, c.MusicIds);
            CollectionAssert.AreEqual(new[] { "se_000" }, c.SfxIds);
            Assert.IsFalse(c.Contains("face_000"));
            Assert.IsTrue(c.TryGetKind("track_001", out var k) && k == AudioKind.Music);
        }

        [Test]
        public void RejectsUnknownSchema()
        {
            Assert.Throws<System.FormatException>(() => AudioCatalog.FromManifestJson(@"{""schema"":9,""assets"":[]}"));
        }
    }

    public class DevCommandsTests
    {
        [Test]
        public void TimeKeepsDayAndSetsClock()
        {
            var c = new GameClock(2f, 3 * GameClock.MinutesPerDay);
            DevCommands.Execute(c, "time 14:30");
            Assert.AreEqual(3, c.DayIndex);
            Assert.AreEqual(14.5f, c.HourOfDay, 0.001f);
        }

        [Test]
        public void DateJumpsAndRaisesDayChanged()
        {
            var c = new GameClock();
            int raised = -1;
            c.DayChanged += d => raised = d;
            DevCommands.Execute(c, "date 2 fall 5");
            Assert.AreEqual(Calendar.ToDayIndex(2, Season.Fall, 5), c.DayIndex);
            Assert.AreEqual(c.DayIndex, raised);
        }

        [Test]
        public void DayKeepsSeasonAndYear()
        {
            var c = new GameClock(2f, Calendar.ToDayIndex(1, Season.Summer, 2) * (double)GameClock.MinutesPerDay + 600);
            DevCommands.Execute(c, "day 20");
            Assert.AreEqual(Season.Summer, c.Date.Season);
            Assert.AreEqual(20, c.Date.DayOfSeason);
            Assert.AreEqual(10f, c.HourOfDay, 0.001f);
        }

        [Test]
        public void NextGoesToSixAmTomorrow()
        {
            var c = new GameClock(2f, 23 * 60);
            DevCommands.Execute(c, "next");
            Assert.AreEqual(1, c.DayIndex);
            Assert.AreEqual(6f, c.HourOfDay, 0.001f);
        }

        [Test]
        public void BadInputReportsUsageAndLeavesClockAlone()
        {
            var c = new GameClock(2f, 360);
            Assert.That(DevCommands.Execute(c, "time 25:00"), Does.StartWith("usage"));
            Assert.That(DevCommands.Execute(c, "day 31"), Does.StartWith("usage"));
            Assert.That(DevCommands.Execute(c, "speed -1"), Does.StartWith("usage"));
            Assert.That(DevCommands.Execute(c, "bogus"), Does.Contain("unknown"));
            Assert.AreEqual(360d, c.TotalMinutes, 0.0001);
        }
    }

    public class FarmCameraTests
    {
        [Test]
        public void OffsetSitsAboveAndBehindAtRequestedDistance()
        {
            var o = FarmCamera.Offset(50f, 45f, 14f);
            Assert.AreEqual(14f, o.magnitude, 0.001f);
            Assert.Greater(o.y, 0f);
            Assert.AreEqual(14f * Mathf.Sin(50f * Mathf.Deg2Rad), o.y, 0.001f);
            // Looking from the camera to the target must match the camera rotation's forward.
            var fwd = FarmCamera.Rotation(50f, 45f) * Vector3.forward;
            Assert.AreEqual(1f, Vector3.Dot(fwd, -o.normalized), 0.0001f);
        }

        [Test]
        public void FitDistanceGrowsWithAreaAndShrinksWithWiderLens()
        {
            float d30 = FarmCamera.FitDistance(30f, 35f, 50f);
            Assert.Greater(FarmCamera.FitDistance(60f, 35f, 50f), d30);
            Assert.Less(FarmCamera.FitDistance(30f, 60f, 50f), d30);
        }

        [Test]
        public void ConfigureBuildsFixedAngleFollowRig()
        {
            var target = new GameObject("t");
            var rigGo = new GameObject("rig", typeof(CinemachineCamera));
            try
            {
                FarmCamera.Configure(rigGo.GetComponent<CinemachineCamera>(), target.transform);
                var cam = rigGo.GetComponent<CinemachineCamera>();
                var follow = rigGo.GetComponent<CinemachineFollow>();
                Assert.IsNotNull(follow);
                Assert.AreSame(target.transform, cam.Follow);
                Assert.IsNull(cam.LookAt, "no aim target: rotation stays fixed");
                Assert.IsNull(rigGo.GetComponent<CinemachineRotationComposer>());
                Assert.AreEqual(FarmCamera.DefaultFov, cam.Lens.FieldOfView, 0.001f);
                Assert.AreEqual(BindingMode.WorldSpace, follow.TrackerSettings.BindingMode);
            }
            finally { Object.DestroyImmediate(rigGo); Object.DestroyImmediate(target); }
        }
    }

    public class MapAreasTests
    {
        static string MapJson(string id) => @"{""schema"":1,""id"":""" + id + @""",""name"":""n"",
          ""terrain"":{""width"":2,""depth"":2,""cellSize"":2,""heights"":[0,0,0,0],""color"":[0.3,0.5,0.2]},
          ""objects"":[],""spawns"":[{""name"":""default"",""position"":[1,0,1]}],""colliders"":[]}";

        [Test]
        public void LoadsUnloadsAndReplacesAreas()
        {
            var areas = new MapAreas(new DictionaryModelSource());
            var a = areas.Load(MapJson("farm"));
            areas.Load(MapJson("town"));
            Assert.IsTrue(areas.IsLoaded("farm") && areas.IsLoaded("town"));

            var again = areas.Load(MapJson("farm"));
            Assert.AreNotSame(a, again);
            Assert.IsTrue(a.Root == null, "old build for the same id is destroyed");

            Assert.IsTrue(areas.Unload("town"));
            Assert.IsFalse(areas.IsLoaded("town"));
            Assert.IsFalse(areas.Unload("town"));
            areas.UnloadAll();
            Assert.IsFalse(areas.IsLoaded("farm"));
            Assert.IsTrue(again.Root == null);
        }
    }
}

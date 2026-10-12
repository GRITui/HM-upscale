using HM.Core;
using HM.Tools;
using NUnit.Framework;
using UnityEngine;

namespace HM.Tests
{
    public class DayCycleTests
    {
        static GameClock At(int day, float hour) => new GameClock(2f, day * GameClock.MinutesPerDay + hour * 60.0);

        [Test]
        public void SleepBeforeMidnightWakesNextMorningAndRaisesDayChangedOnce()
        {
            var c = At(2, 22f);
            int events = 0; c.DayChanged += _ => events++;
            int rolled = DayCycle.Sleep(c);
            Assert.AreEqual(1, rolled);
            Assert.AreEqual(1, events);
            Assert.AreEqual(3, c.DayIndex);
            Assert.AreEqual(DayCycle.WakeHour, c.HourOfDay, 0.001f);
        }

        [Test]
        public void SleepAfterMidnightWakesSameDayWithoutAnotherRollover()
        {
            var c = At(3, 1f);
            int events = 0; c.DayChanged += _ => events++;
            Assert.AreEqual(0, DayCycle.Sleep(c));
            Assert.AreEqual(0, events);
            Assert.AreEqual(3, c.DayIndex);
            Assert.AreEqual(DayCycle.WakeHour, c.HourOfDay, 0.001f);
        }

        [Test]
        public void SleepAtMorningHoursStillGoesToNextMorning()
        {
            var c = At(0, 8f);
            DayCycle.Sleep(c);
            Assert.AreEqual(1, c.DayIndex);
            Assert.AreEqual(6f, c.HourOfDay, 0.001f);
        }

        [TestCase(1.99f, false)]
        [TestCase(2.0f, true)]
        [TestCase(5.9f, true)]
        [TestCase(6.0f, false)]
        [TestCase(12f, false)]
        [TestCase(23.5f, false)]
        public void ForcedSleepWindow(float hour, bool expected)
        {
            Assert.AreEqual(expected, DayCycle.ShouldForceSleep(At(1, hour)));
        }
    }

    public class DevSkipCommandTests
    {
        [Test]
        public void SleepCommandJumpsToMorning()
        {
            var c = new GameClock(2f, 20 * 60.0);
            DevCommands.Execute(c, "sleep");
            Assert.AreEqual(1, c.DayIndex);
            Assert.AreEqual(6f, c.HourOfDay, 0.001f);
        }

        [Test]
        public void SkipAdvancesHoursAndRaisesDayChanged()
        {
            var c = new GameClock(2f, 6 * 60.0);
            int events = 0; c.DayChanged += _ => events++;
            DevCommands.Execute(c, "skip 30");
            Assert.AreEqual(1, events);
            Assert.AreEqual(1, c.DayIndex);
            Assert.AreEqual(12f, c.HourOfDay, 0.001f);
        }

        [Test]
        public void SkipAcceptsHoursAndMinutes()
        {
            var c = new GameClock(2f, 6 * 60.0);
            DevCommands.Execute(c, "skip 1:30");
            Assert.AreEqual(7.5f, c.HourOfDay, 0.001f);
        }

        [Test]
        public void SkipRejectsNonPositiveAndJunk()
        {
            var c = new GameClock(2f, 6 * 60.0);
            DevCommands.Execute(c, "skip 0");
            DevCommands.Execute(c, "skip abc");
            Assert.AreEqual(6f, c.HourOfDay, 0.001f);
        }
    }

    public class PerfCaptureSessionTests
    {
        [Test]
        public void WarmupFramesAreExcludedFromStats()
        {
            var s = new PerfCaptureSession(5f, 10f);
            // 5 s of warmup including a 3.4 s hitch: 1 hitch + 100 frames of 16 ms
            s.AddFrame(3.4f);
            for (int i = 0; i < 101; i++) s.AddFrame(0.016f);
            Assert.IsFalse(s.InWarmup);
            Assert.AreEqual(0, s.Frames, "no measured frames yet");
            // measured window: 20 ms frames
            for (int i = 0; i < 50; i++) s.AddFrame(0.020f);
            var r = s.Result();
            Assert.AreEqual(50, r.frames);
            Assert.AreEqual(20f, r.avgMs, 0.01f);
            Assert.AreEqual(20f, r.maxMs, 0.01f, "the warmup hitch must not leak into max");
            Assert.AreEqual(3400f, r.warmupMaxMs, 0.5f);
            Assert.AreEqual(1, r.warmupFramesOver100Ms);
        }

        [Test]
        public void PercentilesAndCountersAreComputed()
        {
            var s = new PerfCaptureSession(0f, 100f);
            for (int i = 1; i <= 100; i++) s.AddFrame(i / 1000f, 100 + i, 10, 1000 * i);
            var r = s.Result();
            Assert.AreEqual(100, r.frames);
            Assert.AreEqual(95f, r.p95Ms, 1.01f);
            Assert.AreEqual(99f, r.p99Ms, 1.01f);
            Assert.IsTrue(r.renderCountersAvailable);
            Assert.AreEqual(200, r.drawCallsMax);
            Assert.AreEqual(150.5f, r.drawCallsAvg, 0.01f);
            Assert.AreEqual(100000, r.trianglesMax);
        }

        [Test]
        public void StopsRecordingWhenDurationElapsed()
        {
            var s = new PerfCaptureSession(1f, 1f);
            for (int i = 0; i < 400; i++) s.AddFrame(0.01f);
            Assert.IsTrue(s.Done);
            Assert.LessOrEqual(s.Frames, 101);
        }

        [Test]
        public void JsonRoundTrips()
        {
            var s = new PerfCaptureSession(0f, 1f);
            for (int i = 0; i < 10; i++) s.AddFrame(0.0167f);
            var back = JsonUtility.FromJson<PerfCaptureResult>(s.ToJson());
            Assert.AreEqual(10, back.frames);
            Assert.AreEqual(16.7f, back.avgMs, 0.01f);
        }
    }
}

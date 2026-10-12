using HM.Core;
using NUnit.Framework;

namespace HM.Tests
{
    public class GameClockTests
    {
        [Test]
        public void TickScalesRealSecondsToGameMinutes()
        {
            var c = new GameClock(2f, 0);
            c.Tick(30f);
            Assert.AreEqual(60.0, c.TotalMinutes, 1e-6);
            Assert.AreEqual(1f, c.HourOfDay, 1e-4);
        }

        [Test]
        public void TimeScaleMultipliesRate()
        {
            var c = new GameClock(1f, 0) { TimeScale = 4f };
            c.Tick(10f);
            Assert.AreEqual(40.0, c.TotalMinutes, 1e-6);
        }

        [Test]
        public void PausedClockDoesNotAdvance()
        {
            var c = new GameClock(1f, 100) { Paused = true };
            c.Tick(50f);
            Assert.AreEqual(100.0, c.TotalMinutes, 1e-6);
            c.Paused = false;
            c.Tick(5f);
            Assert.AreEqual(105.0, c.TotalMinutes, 1e-6);
        }

        [TestCase(0f, DayPhase.Night)]
        [TestCase(4.99f, DayPhase.Night)]
        [TestCase(5f, DayPhase.Dawn)]
        [TestCase(7f, DayPhase.Day)]
        [TestCase(12f, DayPhase.Day)]
        [TestCase(18f, DayPhase.Dusk)]
        [TestCase(20f, DayPhase.Night)]
        [TestCase(23.5f, DayPhase.Night)]
        public void PhaseBoundaries(float hour, DayPhase expected) =>
            Assert.AreEqual(expected, GameClock.PhaseAt(hour));

        [Test]
        public void DayChangedFiresOnceAtMidnight()
        {
            var c = new GameClock(1f, GameClock.MinutesPerDay - 5);
            int fired = 0, lastDay = -1;
            c.DayChanged += d => { fired++; lastDay = d; };
            c.Tick(4f);
            Assert.AreEqual(0, fired);
            c.Tick(2f);
            Assert.AreEqual(1, fired);
            Assert.AreEqual(1, lastDay);
            Assert.AreEqual(1, c.DayIndex);
            Assert.AreEqual(1f, c.MinuteOfDay, 1e-3);
        }

        [Test]
        public void DateFollowsCalendar()
        {
            var c = new GameClock(1f, 31 * GameClock.MinutesPerDay + 60);
            Assert.AreEqual(Season.Summer, c.Date.Season);
            Assert.AreEqual(2, c.Date.DayOfSeason);
        }

        [Test]
        public void FormatTimeIsZeroPadded()
        {
            var c = new GameClock(1f, 6 * 60 + 5);
            Assert.AreEqual("06:05", c.FormatTime());
        }

        [Test]
        public void NegativeAndZeroDeltaIgnored()
        {
            var c = new GameClock(1f, 10);
            c.Tick(-5f);
            c.Tick(0f);
            Assert.AreEqual(10.0, c.TotalMinutes, 1e-6);
        }
    }
}

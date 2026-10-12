using System;
using HM.Core;
using NUnit.Framework;

namespace HM.Tests
{
    public class CalendarTests
    {
        [Test]
        public void DayZeroIsYear1SpringDay1Monday()
        {
            var d = Calendar.FromDayIndex(0);
            Assert.AreEqual(1, d.Year);
            Assert.AreEqual(Season.Spring, d.Season);
            Assert.AreEqual(1, d.DayOfSeason);
            Assert.AreEqual(Weekday.Monday, d.Weekday);
        }

        [Test]
        public void SeasonRollsOverAfter30Days()
        {
            Assert.AreEqual(30, Calendar.FromDayIndex(29).DayOfSeason);
            Assert.AreEqual(Season.Spring, Calendar.FromDayIndex(29).Season);
            var d = Calendar.FromDayIndex(30);
            Assert.AreEqual(Season.Summer, d.Season);
            Assert.AreEqual(1, d.DayOfSeason);
        }

        [Test]
        public void YearRollsOverAfter120Days()
        {
            var d = Calendar.FromDayIndex(119);
            Assert.AreEqual(1, d.Year);
            Assert.AreEqual(Season.Winter, d.Season);
            Assert.AreEqual(30, d.DayOfSeason);
            var n = Calendar.FromDayIndex(120);
            Assert.AreEqual(2, n.Year);
            Assert.AreEqual(Season.Spring, n.Season);
        }

        [Test]
        public void WeekdayCyclesEverySevenDaysAcrossSeasons()
        {
            Assert.AreEqual(Weekday.Monday, Calendar.FromDayIndex(7).Weekday);
            Assert.AreEqual(Weekday.Wednesday, Calendar.FromDayIndex(30).Weekday);
        }

        [Test]
        public void RoundTripAllDaysOfTwoYears()
        {
            for (int i = 0; i < Calendar.DaysPerYear * 2; i++)
            {
                var d = Calendar.FromDayIndex(i);
                Assert.AreEqual(i, Calendar.ToDayIndex(d.Year, d.Season, d.DayOfSeason));
            }
        }

        [Test]
        public void NegativeIndexThrows() =>
            Assert.Throws<ArgumentOutOfRangeException>(() => Calendar.FromDayIndex(-1));

        [Test]
        public void InvalidDayOfSeasonThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Calendar.ToDayIndex(1, Season.Fall, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => Calendar.ToDayIndex(1, Season.Fall, 31));
        }
    }
}

using System;

namespace HM.Core
{
    public enum Season { Spring = 0, Summer = 1, Fall = 2, Winter = 3 }

    public enum Weekday { Monday = 0, Tuesday, Wednesday, Thursday, Friday, Saturday, Sunday }

    /// <summary>A calendar date. Year and DayOfSeason are 1-based.</summary>
    public readonly struct GameDate : IEquatable<GameDate>
    {
        public readonly int Year;
        public readonly Season Season;
        public readonly int DayOfSeason;
        public readonly Weekday Weekday;

        public GameDate(int year, Season season, int dayOfSeason, Weekday weekday)
        {
            Year = year;
            Season = season;
            DayOfSeason = dayOfSeason;
            Weekday = weekday;
        }

        public bool Equals(GameDate other) =>
            Year == other.Year && Season == other.Season && DayOfSeason == other.DayOfSeason;

        public override bool Equals(object obj) => obj is GameDate d && Equals(d);
        public override int GetHashCode() => (Year * 4 + (int)Season) * 31 + DayOfSeason;
        public override string ToString() => $"Year {Year}, {Season} {DayOfSeason} ({Weekday})";
    }

    /// <summary>
    /// 4 seasons x 30 days = 120 days per year. Day index 0 is Year 1, Spring 1, Monday.
    /// Because 30 is not a multiple of 7, weekdays run continuously across season boundaries.
    /// </summary>
    public static class Calendar
    {
        public const int SeasonsPerYear = 4;
        public const int DaysPerSeason = 30;
        public const int DaysPerYear = SeasonsPerYear * DaysPerSeason;
        public const int DaysPerWeek = 7;

        public static GameDate FromDayIndex(int dayIndex)
        {
            if (dayIndex < 0) throw new ArgumentOutOfRangeException(nameof(dayIndex));
            int year = dayIndex / DaysPerYear + 1;
            int inYear = dayIndex % DaysPerYear;
            var season = (Season)(inYear / DaysPerSeason);
            int day = inYear % DaysPerSeason + 1;
            return new GameDate(year, season, day, (Weekday)(dayIndex % DaysPerWeek));
        }

        public static int ToDayIndex(int year, Season season, int dayOfSeason)
        {
            if (year < 1) throw new ArgumentOutOfRangeException(nameof(year));
            if (dayOfSeason < 1 || dayOfSeason > DaysPerSeason) throw new ArgumentOutOfRangeException(nameof(dayOfSeason));
            return (year - 1) * DaysPerYear + (int)season * DaysPerSeason + (dayOfSeason - 1);
        }
    }
}

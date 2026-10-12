using System;
using System.Globalization;
using HM.Core;
using Calendar = HM.Core.Calendar;

namespace HM.Tools
{
    /// <summary>
    /// Text commands for the developer console. Pure logic over a GameClock so it is testable.
    /// time HH[:MM] | sleep | skip H[:M] | day N | date YEAR SEASON DAY | next | speed X | pause | resume | help
    /// Day numbers are 1-based days of the season; day N keeps the current season and year.
    /// </summary>
    public static class DevCommands
    {
        public const string Help =
            "time HH[:MM]   set time of day\n" +
            "day N          jump to day N (1-30) of this season\n" +
            "date Y S D     jump to year Y, season (spring|summer|fall|winter), day D\n" +
            "next           jump to 06:00 of the next day\n" +
            "sleep          go to bed now (wakes 06:00, rolls the day over)\n" +
            "skip H[:M]     advance the clock by H hours (and M minutes), firing day changes\n" +
            "speed X        time scale (0..100)\n" +
            "pause|resume   stop or restart the clock";

        public static string Execute(GameClock clock, string line)
        {
            if (clock == null) return "no clock";
            if (string.IsNullOrWhiteSpace(line)) return "";
            var p = line.Trim().ToLowerInvariant().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            try
            {
                switch (p[0])
                {
                    case "help": return Help;
                    case "pause": clock.Paused = true; return "paused";
                    case "resume": clock.Paused = false; return "running";
                    case "next":
                        clock.JumpTo((clock.DayIndex + 1) * (double)GameClock.MinutesPerDay + 6 * 60);
                        return Status(clock);
                    case "sleep":
                        DayCycle.Sleep(clock);
                        return Status(clock);
                    case "skip":
                        if (p.Length < 2 || !TryParseTime(p[1], out double delta, 10000) || delta <= 0)
                            return "usage: skip H[:M] (advance, e.g. skip 5 or skip 0:30)";
                        clock.JumpTo(clock.TotalMinutes + delta);
                        return Status(clock);
                    case "time":
                        if (p.Length < 2 || !TryParseTime(p[1], out double minutes)) return "usage: time HH[:MM]";
                        clock.JumpTo(clock.DayIndex * (double)GameClock.MinutesPerDay + minutes);
                        return Status(clock);
                    case "day":
                        if (p.Length < 2 || !int.TryParse(p[1], out int day) || day < 1 || day > Calendar.DaysPerSeason)
                            return "usage: day 1-" + Calendar.DaysPerSeason;
                        var cur = clock.Date;
                        return JumpDate(clock, cur.Year, cur.Season, day);
                    case "date":
                        if (p.Length < 4 || !int.TryParse(p[1], out int y) || y < 1 || !Enum.TryParse(p[2], true, out Season s)
                            || !int.TryParse(p[3], out int d) || d < 1 || d > Calendar.DaysPerSeason)
                            return "usage: date YEAR spring|summer|fall|winter DAY";
                        return JumpDate(clock, y, s, d);
                    case "speed":
                        if (p.Length < 2 || !float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) || x < 0f || x > 100f)
                            return "usage: speed 0-100";
                        clock.TimeScale = x;
                        return "speed " + x.ToString(CultureInfo.InvariantCulture);
                    default: return "unknown command '" + p[0] + "' (try help)";
                }
            }
            catch (ArgumentOutOfRangeException e) { return e.Message; }
        }

        static string JumpDate(GameClock clock, int year, Season season, int day)
        {
            int idx = Calendar.ToDayIndex(year, season, day);
            clock.JumpTo(idx * (double)GameClock.MinutesPerDay + clock.MinuteOfDay);
            return Status(clock);
        }

        public static bool TryParseTime(string s, out double minutes, int maxHours = 23)
        {
            minutes = 0;
            var parts = s.Split(':');
            if (parts.Length > 2 || !int.TryParse(parts[0], out int h)) return false;
            int m = 0;
            if (parts.Length == 2 && !int.TryParse(parts[1], out m)) return false;
            if (h < 0 || h > maxHours || m < 0 || m > 59) return false;
            minutes = h * 60 + m;
            return true;
        }

        public static string Status(GameClock clock) => clock.Date + " " + clock.FormatTime();
    }
}

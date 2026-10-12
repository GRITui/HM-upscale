using System;

namespace HM.Core
{
    /// <summary>
    /// End-of-day rules over a GameClock. Pure logic, no Unity types.
    /// Sleeping jumps to the wake time. If it is before the wake hour (after midnight) the same calendar
    /// day is kept, because the midnight rollover already ran; otherwise the next day is entered and
    /// GameClock.DayChanged fires exactly once, which drives FarmClockBridge.
    /// </summary>
    public static class DayCycle
    {
        /// <summary>Wake time, in hours. PLACEHOLDER value (source: placeholder, confidence: low).</summary>
        public const float WakeHour = 6f;
        /// <summary>Hour after midnight at which the player collapses and is put to bed. PLACEHOLDER.</summary>
        public const float ForcedSleepHour = 2f;

        /// <summary>Total minutes the clock will read after sleeping now.</summary>
        public static double WakeTotalMinutes(GameClock clock)
        {
            if (clock == null) throw new ArgumentNullException(nameof(clock));
            double dayStart = clock.DayIndex * (double)GameClock.MinutesPerDay;
            bool beforeWake = clock.MinuteOfDay < WakeHour * 60f;
            return (beforeWake ? dayStart : dayStart + GameClock.MinutesPerDay) + WakeHour * 60.0;
        }

        /// <summary>Skip to the wake time. Returns the number of days that rolled over (0 or 1).</summary>
        public static int Sleep(GameClock clock)
        {
            int before = clock.DayIndex;
            clock.JumpTo(WakeTotalMinutes(clock));
            return clock.DayIndex - before;
        }

        /// <summary>True in the small hours between ForcedSleepHour and WakeHour.</summary>
        public static bool ShouldForceSleep(GameClock clock) =>
            clock.MinuteOfDay >= ForcedSleepHour * 60f && clock.MinuteOfDay < WakeHour * 60f;
    }
}

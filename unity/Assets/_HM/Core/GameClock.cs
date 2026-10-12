using System;

namespace HM.Core
{
    public enum DayPhase { Night, Dawn, Day, Dusk }

    /// <summary>
    /// Pure C# game clock: converts real seconds to in-game minutes. No Unity dependency so it is
    /// testable in EditMode. Drive it with Tick(realDeltaSeconds).
    /// </summary>
    public sealed class GameClock
    {
        public const int MinutesPerDay = 24 * 60;
        public const float DawnStartHour = 5f;
        public const float DayStartHour = 7f;
        public const float DuskStartHour = 18f;
        public const float NightStartHour = 20f;

        /// <summary>In-game minutes that elapse per real second at TimeScale 1.</summary>
        public float MinutesPerRealSecond { get; }

        public float TimeScale { get; set; } = 1f;
        public bool Paused { get; set; }

        /// <summary>Total in-game minutes since Year 1, Spring 1, 00:00.</summary>
        public double TotalMinutes { get; private set; }

        public event Action<int> DayChanged;

        public GameClock(float minutesPerRealSecond = 2f, double startTotalMinutes = 6 * 60)
        {
            if (minutesPerRealSecond <= 0f) throw new ArgumentOutOfRangeException(nameof(minutesPerRealSecond));
            MinutesPerRealSecond = minutesPerRealSecond;
            TotalMinutes = Math.Max(0, startTotalMinutes);
        }

        public int DayIndex => (int)(TotalMinutes / MinutesPerDay);
        public float MinuteOfDay => (float)(TotalMinutes - (double)DayIndex * MinutesPerDay);
        public float HourOfDay => MinuteOfDay / 60f;
        /// <summary>0..1 through the current day, 0 = midnight.</summary>
        public float DayFraction => MinuteOfDay / MinutesPerDay;
        public GameDate Date => Calendar.FromDayIndex(DayIndex);
        public DayPhase Phase => PhaseAt(HourOfDay);

        public static DayPhase PhaseAt(float hour)
        {
            if (hour >= DawnStartHour && hour < DayStartHour) return DayPhase.Dawn;
            if (hour >= DayStartHour && hour < DuskStartHour) return DayPhase.Day;
            if (hour >= DuskStartHour && hour < NightStartHour) return DayPhase.Dusk;
            return DayPhase.Night;
        }

        public void Tick(float realDeltaSeconds)
        {
            if (Paused || realDeltaSeconds <= 0f) return;
            int before = DayIndex;
            TotalMinutes += realDeltaSeconds * MinutesPerRealSecond * TimeScale;
            int after = DayIndex;
            if (after != before) DayChanged?.Invoke(after);
        }

        public void SetTotalMinutes(double totalMinutes)
        {
            TotalMinutes = Math.Max(0, totalMinutes);
        }

        /// <summary>Like SetTotalMinutes but raises DayChanged when the day index changes (used by dev tools and sleep skips).</summary>
        public void JumpTo(double totalMinutes)
        {
            int before = DayIndex;
            SetTotalMinutes(totalMinutes);
            if (DayIndex != before) DayChanged?.Invoke(DayIndex);
        }

        public string FormatTime()
        {
            int m = (int)MinuteOfDay;
            return $"{m / 60:00}:{m % 60:00}";
        }
    }
}

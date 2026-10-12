using System;
using HM.Core;

namespace HM.Farming
{
    /// <summary>Subscribes a FarmSim to GameClock.DayChanged. Dispose to unsubscribe.</summary>
    public sealed class FarmClockBridge : IDisposable
    {
        readonly GameClock clock;
        readonly FarmSim sim;
        public int DaysAdvanced { get; private set; }

        public FarmClockBridge(GameClock clock, FarmSim sim)
        {
            this.clock = clock; this.sim = sim;
            clock.DayChanged += OnDay;
        }

        void OnDay(int dayIndex) { sim.NewDay(); DaysAdvanced++; }

        public void Dispose() => clock.DayChanged -= OnDay;
    }
}

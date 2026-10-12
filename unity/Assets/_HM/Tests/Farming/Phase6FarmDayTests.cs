using HM.Core;
using HM.Farming;
using NUnit.Framework;

namespace HM.Tests.Farming
{
    public class SleepAdvancesCropsTests
    {
        [Test]
        public void SleepingRollsTheDayAndGrowsAWateredCrop()
        {
            var rules = FarmingTests.LoadRules();
            var sim = new FarmSim(rules);
            var clock = new GameClock(2f, 15 * 60.0);
            using (var bridge = new FarmClockBridge(clock, sim))
            {
                var p = new TilePos(1, 1);
                sim.BuySeeds("sprout_beet", 1);
                Assert.AreEqual(ToolResult.Done, sim.UseTool(Tool.Hoe, p));
                Assert.AreEqual(ToolResult.Done, sim.UseTool(Tool.SeedBag, p, "sprout_beet"));
                Assert.AreEqual(ToolResult.Done, sim.UseTool(Tool.WateringCan, p));
                DayCycle.Sleep(clock);
                Assert.AreEqual(1, bridge.DaysAdvanced);
                Assert.AreEqual(1, sim.TileAt(p).DaysGrown);
                Assert.IsFalse(sim.TileAt(p).Watered);
            }
        }

        [Test]
        public void UnwateredCropDoesNotGrowOvernight()
        {
            var sim = new FarmSim(FarmingTests.LoadRules());
            var clock = new GameClock(2f, 15 * 60.0);
            using (new FarmClockBridge(clock, sim))
            {
                var p = new TilePos(0, 0);
                sim.BuySeeds("sprout_beet", 1);
                sim.UseTool(Tool.Hoe, p); sim.UseTool(Tool.SeedBag, p, "sprout_beet");
                DayCycle.Sleep(clock);
                Assert.AreEqual(0, sim.TileAt(p).DaysGrown);
            }
        }

        [Test]
        public void ForcedSleepAfterMidnightDoesNotDoubleAdvance()
        {
            var sim = new FarmSim(FarmingTests.LoadRules());
            var clock = new GameClock(2f, 23 * 60.0 + 59);
            using (var bridge = new FarmClockBridge(clock, sim))
            {
                clock.Tick(1f);              // crosses midnight: one rollover (clock event)
                clock.SetTotalMinutes(clock.TotalMinutes + 120);
                clock.JumpTo(clock.TotalMinutes);
                int before = bridge.DaysAdvanced;
                DayCycle.Sleep(clock);       // 02:00 same day: wake at 06:00, no new day
                Assert.AreEqual(before, bridge.DaysAdvanced);
            }
        }
    }
}

using System.Collections;
using System.IO;
using HM.Core;
using HM.Dialogue;
using HM.Farming;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HM.Tests.Farming.PlayMode
{
    public class FarmDayPlayModeTests
    {
        GameObject host;

        [TearDown] public void Cleanup()
        {
            if (host != null) Object.DestroyImmediate(host);
            if (GameClockBehaviour.Instance != null) Object.DestroyImmediate(GameClockBehaviour.Instance.gameObject);
        }

        /// <summary>One in-game day driven by the real GameClockBehaviour: wake, till, plant, water,
        /// talk, ship, then the clock crosses midnight and the farm rolls over by itself.</summary>
        [UnityTest] public IEnumerator OneDayEndToEnd()
        {
            host = new GameObject("clock", typeof(GameClockBehaviour));
            yield return null;                       // Awake runs
            var clock = GameClockBehaviour.Instance.Clock;
            clock.SetTotalMinutes(6 * 60);           // Day 0, 06:00

            var rules = RulesData.FromJson(File.ReadAllText(Path.Combine(Application.dataPath, "_HM/Farming/Data/rules.json")));
            var sim = new FarmSim(rules);
            using (var bridge = new FarmClockBridge(clock, sim))
            {
                var p = new TilePos(1, 1);
                var gold0 = sim.Gold;
                sim.BuySeeds("sprout_beet", 1);
                Assert.AreEqual(ToolResult.Done, sim.UseTool(Tool.Hoe, p));
                Assert.AreEqual(ToolResult.Done, sim.UseTool(Tool.SeedBag, p, "sprout_beet"));
                Assert.AreEqual(ToolResult.Done, sim.UseTool(Tool.WateringCan, p));

                var dlg = new DialogueRunner(DialogueData.FromJson(File.ReadAllText(
                    Path.Combine(Application.dataPath, "_HM/Dialogue/Data/dialogue.json"))));
                dlg.Flags.Add("planted_today");
                dlg.Begin();
                Assert.AreEqual(3, dlg.VisibleChoices().Count);
                dlg.Choose(1);
                Assert.AreEqual("mira_planted", dlg.Current.id);

                int day = clock.DayIndex;
                clock.TimeScale = 600f;              // fast-forward through real frames
                float guard = 0;
                while (clock.DayIndex == day && guard < 10f) { guard += Time.deltaTime; yield return null; }

                Assert.AreEqual(day + 1, clock.DayIndex, "clock crossed midnight");
                Assert.AreEqual(1, bridge.DaysAdvanced);
                Assert.AreEqual(1, sim.TileAt(p).DaysGrown, "watered crop grew overnight");
                Assert.IsFalse(sim.TileAt(p).Watered);
                Assert.AreEqual(sim.MaxStamina, sim.Stamina);
                Assert.AreEqual(gold0 - rules.Crop("sprout_beet").seedPrice.value, sim.Gold);
            }
        }
    }
}

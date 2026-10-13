using System.IO;
using HM.Core;
using HM.Farming;
using NUnit.Framework;
using UnityEngine;

namespace HM.Tests.Farming
{
    public class FarmingTests
    {
        public static RulesData LoadRules() =>
            RulesData.FromJson(File.ReadAllText(Path.Combine(Application.dataPath, "_HM/Farming/Data/rules.json")));

        static readonly TilePos P = new TilePos(2, 3);
        RulesData rules;
        FarmSim sim;

        [SetUp] public void Setup() { rules = LoadRules(); sim = new FarmSim(rules); }

        static bool ValidSource(string s) => s == "extracted" || s == "observed" || s == "placeholder";

        [Test] public void RulesAreAllFlaggedWithSource()
        {
            foreach (var t in rules.tools) Assert.IsTrue(ValidSource(t.staminaCost.source));
            foreach (var c in rules.crops)
            {
                Assert.IsTrue(ValidSource(c.growDays.source));
                Assert.That(c.seedPrice.confidence, Is.Not.Empty);
            }
        }

        [Test] public void CommittedRulesAreNumericOnly()
        {
            // Small numeric rule values (gold, prices, growth days) may be committed;
            // game text and assets may not (repo data policy).
            var json = File.ReadAllText(Path.Combine(Application.dataPath, "_HM/Farming/Data/rules.json"));
            StringAssert.DoesNotContain("\"text\"", json);
        }

        [Test] public void OverlayOverridesAndTagsSource()
        {
            int n = rules.ApplyOverlay("{\"overrides\":[{\"path\":\"crop.sprout_beet.seedPrice\",\"value\":7,\"source\":\"extracted\",\"confidence\":\"high\"}]}");
            Assert.AreEqual(1, n);
            Assert.AreEqual(7, rules.Crop("sprout_beet").seedPrice.value);
            Assert.AreEqual("extracted", rules.Crop("sprout_beet").seedPrice.source);
        }

        [Test] public void OverlayUnknownPathThrows() =>
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() =>
                rules.ApplyOverlay("{\"overrides\":[{\"path\":\"crop.nope.seedPrice\",\"value\":1}]}"));

        [Test] public void HoeTillsAndCostsStamina()
        {
            Assert.AreEqual(ToolResult.Done, sim.UseTool(Tool.Hoe, P));
            Assert.AreEqual(SoilState.Tilled, sim.TileAt(P).Soil);
            Assert.AreEqual(sim.MaxStamina - rules.Tool("hoe").staminaCost.value, sim.Stamina);
        }

        [Test] public void NoEffectCostsNothing()
        {
            Assert.AreEqual(ToolResult.NoEffect, sim.UseTool(Tool.WateringCan, P));
            sim.UseTool(Tool.Hoe, P);
            int s = sim.Stamina;
            Assert.AreEqual(ToolResult.NoEffect, sim.UseTool(Tool.Hoe, P));
            Assert.AreEqual(s, sim.Stamina);
        }

        [Test] public void OutOfBounds() => Assert.AreEqual(ToolResult.OutOfBounds, sim.UseTool(Tool.Hoe, new TilePos(-1, 0)));

        [Test] public void ExhaustedStaminaBlocksTool()
        {
            int guard = 0;
            while (sim.Stamina >= rules.Tool("hoe").staminaCost.value && guard++ < 1000)
                sim.UseTool(Tool.Hoe, new TilePos(guard % 16, guard / 16 % 16));
            var before = sim.Stamina;
            Assert.AreEqual(ToolResult.NoStamina, sim.UseTool(Tool.Hoe, new TilePos(15, 15)));
            Assert.AreEqual(before, sim.Stamina);
        }

        [Test] public void PlantRequiresSeedsAndTilledSoil()
        {
            Assert.AreEqual(ToolResult.NoEffect, sim.UseTool(Tool.SeedBag, P, "sprout_beet"));
            sim.UseTool(Tool.Hoe, P);
            Assert.AreEqual(ToolResult.NoSeeds, sim.UseTool(Tool.SeedBag, P, "sprout_beet"));
            Assert.IsTrue(sim.BuySeeds("sprout_beet", 2));
            Assert.AreEqual(ToolResult.Done, sim.UseTool(Tool.SeedBag, P, "sprout_beet"));
            Assert.AreEqual(1, sim.SeedCount("sprout_beet"));
            Assert.AreEqual("sprout_beet", sim.TileAt(P).CropId);
        }

        [Test] public void BuySeedsChargesGoldAndRefusesWhenBroke()
        {
            int price = rules.Crop("plain_corn").seedPrice.value;
            Assert.IsTrue(sim.BuySeeds("plain_corn", 1));
            Assert.AreEqual(rules.startingGold.value - price, sim.Gold);
            Assert.IsFalse(sim.BuySeeds("plain_corn", 1000));
        }

        void Plant(string crop)
        {
            sim.BuySeeds(crop, 1);
            sim.UseTool(Tool.Hoe, P);
            sim.UseTool(Tool.SeedBag, P, crop);
        }

        [Test] public void CropGrowsOnlyOnWateredDays()
        {
            Plant("sprout_beet");
            sim.NewDay();                         // unwatered: no growth
            Assert.AreEqual(0, sim.TileAt(P).DaysGrown);
            sim.UseTool(Tool.WateringCan, P);
            sim.NewDay();
            Assert.AreEqual(1, sim.TileAt(P).DaysGrown);
            Assert.IsFalse(sim.TileAt(P).Watered);
        }

        [Test] public void HarvestAfterGrowDaysThenShipAndPaidNextMorning()
        {
            Plant("sprout_beet");
            int days = rules.Crop("sprout_beet").growDays.value;
            for (int i = 0; i < days; i++)
            {
                Assert.AreEqual(ToolResult.NoEffect, sim.UseTool(Tool.Sickle, P));
                sim.UseTool(Tool.WateringCan, P);
                sim.NewDay();
            }
            Assert.IsTrue(sim.IsMature(sim.TileAt(P)));
            Assert.AreEqual(ToolResult.Done, sim.UseTool(Tool.Sickle, P));
            Assert.AreEqual(1, sim.ProduceCount("sprout_beet"));
            Assert.IsNull(sim.TileAt(P).CropId);

            int gold = sim.Gold;
            Assert.IsTrue(sim.Ship("sprout_beet"));
            Assert.AreEqual(gold, sim.Gold, "no payout until the next morning");
            Assert.AreEqual(1, sim.BinCount("sprout_beet"));
            sim.NewDay();
            Assert.AreEqual(gold + rules.Crop("sprout_beet").sellPrice.value, sim.Gold);
            Assert.AreEqual(0, sim.BinCount("sprout_beet"));
        }

        [Test] public void ShipRefusesWhatYouDoNotHave() => Assert.IsFalse(sim.Ship("sprout_beet"));

        [Test] public void NewDayRestoresStamina()
        {
            sim.UseTool(Tool.Hoe, P);
            sim.NewDay();
            Assert.AreEqual(sim.MaxStamina, sim.Stamina);
        }

        [Test] public void ClockBridgeAdvancesDayOnRollover()
        {
            var clock = new GameClock(1f, GameClock.MinutesPerDay - 2);
            using (var bridge = new FarmClockBridge(clock, sim))
            {
                sim.UseTool(Tool.Hoe, P);
                clock.Tick(5f);
                Assert.AreEqual(1, bridge.DaysAdvanced);
                Assert.AreEqual(sim.MaxStamina, sim.Stamina);
            }
            clock.Tick(GameClock.MinutesPerDay);
            sim.UseTool(Tool.Hoe, new TilePos(0, 0));
            Assert.Less(sim.Stamina, sim.MaxStamina, "unsubscribed bridge must not fire");
        }
    }
}

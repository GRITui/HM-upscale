# Rule parity table

"Test" is the automated check that exists today (against the rule as implemented). "PPSSPP check"
is the observation still to be made in the original game; until then the rule stays unverified.

| Rule | Source | Confidence | Test (exists) | PPSSPP check (pending) |
|---|---|---|---|---|
| Seed buy price | tables.md f12 via local overlay; placeholder in git | high (overlay) / low | FarmingTests.BuySeedsChargesGold..., OverlayOverridesAndTagsSource | compare shop price per seed |
| Sell price paid next morning | placeholder (sell f13 medium, overlay) | low | HarvestAfterGrowDaysThenShipAndPaidNextMorning | ship item, check gold after sleep |
| Crop growth days | placeholder (table not located) | low | CropGrowsOnlyOnWateredDays, Harvest... | plant and count days to harvest |
| Unwatered day does not grow, no wither | placeholder | low | CropGrowsOnlyOnWateredDays | skip watering 1 and 3 days |
| Hoe tills untilled soil only | observed (genre convention) | low | HoeTillsAndCostsStamina, NoEffectCostsNothing | hoe tilled tile |
| Watering needs tilled soil | placeholder | low | NoEffectCostsNothing | water bare ground |
| Planting needs tilled soil and a seed | placeholder | low | PlantRequiresSeedsAndTilledSoil | plant on bare ground |
| Sickle harvests mature crop only | placeholder | low | HarvestAfter... | sickle on young crop |
| Stamina cost per tool | placeholder (table not located) | low | HoeTillsAndCostsStamina, ExhaustedStaminaBlocksTool | count uses until exhausted |
| No stamina spent on no-effect action | placeholder | low | NoEffectCostsNothing | swing at nothing |
| Stamina refills on new day | placeholder | low | NewDayRestoresStamina, PlayMode OneDayEndToEnd | check bar after sleep |
| Watering resets at day end | placeholder | low | CropGrowsOnlyOnWateredDays | check tile next morning |
| Day rollover drives farm | existing GameClock.DayChanged | high (own code) | ClockBridgeAdvancesDayOnRollover, OneDayEndToEnd | n/a |
| Dialogue lines, speaker, face id | text.md (face id high) / original lines | high (format) | DialogueTests.StartsAtGreeting... | compare face ids per speaker |
| Choice gating and flag set/test | placeholder | low | RequireFlagHidesChoice, AdviceSetsFlagAndBranchesOnIt | observe a flag-gated choice |
| Wake hour after sleeping | placeholder (DayCycle.WakeHour=6) | low | DayCycleTests, SleepAdvancesCropsTests | note wake time |
| Forced sleep window 02:00 to 06:00 | placeholder (DayCycle.ForcedSleepHour=2) | low | ForcedSleepWindow, SleepingAfterMidnightForcesWakeSameDay | stay up past midnight |
| Sleep before midnight rolls the day, after midnight does not | own design | high (own code) | SleepBeforeMidnightWakes..., SleepAfterMidnightWakes... | sleep at 22:00 and 01:00 |
| Day tint and sun path | own design | high (own code) | SunIsBrighterAtNoonThanAtNight | compare lighting over a day |
| Farm slice loop (till, plant, water, sleep, grow, talk) | rules.json + placeholders | low | PlantWaterSleepGrowAndTalkToNpc | play one in-game day |
| Starting seeds in the slice | placeholder (FarmWorld.startingSeeds=4) | low | n/a | start a new game |

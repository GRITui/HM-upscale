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

## Decoded calendar and clock (tools/hmconv/rules.py)

Source: reading the clock tick and new-game init in the EBOOT. Structural, not game text.

| rule | decoded | confidence |
|---|---|---|
| calendar | 4 seasons x 30 days, 7-day weekday cycle, year capped at 99 | high |
| day boundary | the day counter advances at 06:00; the clock wraps 23:59 to 00:00 | high |
| minute length | one minute per 15 frame ticks at the default speed setting | medium |
| new game | year 0, spring, day index 1, weekday 1, 06:00 | medium |
| festival table | 18 rows (season, day, event id); first 14 scanned by the lookup | medium |

## Decoded economy (tools/hmconv/economy.py)

Source: player-init, daily crop update, fatigue and item/crop tables in the EBOOT. Per-crop
numbers are written only to the git-ignored assets/hm-converted/, never committed.

| rule | decoded | confidence |
|---|---|---|
| starting gold, stamina | gold 500; stamina 100 of 100; max stamina cap 255 (committed in rules.json) | high |
| fatigue | 0 to 100; stamina and fatigue changed together by one modifier; gain depends on hour | medium |
| pass-out sleep | fatigue 50-69 sleeps 2 h, 70-79 sleeps 4 h, 80+ sleeps 6 h; day rolls at 06:00 | medium |
| crop table | 22 seed indices: season, stage count, regrow stage, 4 stage-day values, produce row | high |
| growth days | sum of the first N stage-day values (N = stage count) | high |
| season death | a crop whose season differs from the current season dies in the daily update | high |
| watering | watered ground tile advances the crop one day, then reverts to dry; dry tile does not | high |
| seed buy price, produce sell price | seed row field 8, produce row field 9 of the item table | high |
| item table alignment | base vaddr 0x1275A8, stride 0x28 (tables.py base is 0x20 bytes high) | high |

Not decoded yet: forced-sleep clock cutoff (no hour trigger found; 02:00 stays a placeholder),
per-tool stamina costs (a table of -1 to -5 exists at vaddr 0x13760A, indexing unclear),
starting inventory, affection scale and heart tiers, animals, cooking, regrow-stage meaning,
and the mapping from the 22 seed indices to repo crop ids (crop entries in rules.json stay
placeholder).

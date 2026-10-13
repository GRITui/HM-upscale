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
| Stamina cost per tool | extracted: table 0x13760A, uncharged use = 1 for every tool (rules.json) | medium | HoeTillsAndCostsStamina, ExhaustedStaminaBlocksTool | count uses until exhausted |
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

## Decoded in Sprint (addresses are vaddr; RAM = 0x08804000 + vaddr; file offset = vaddr + 0x74)

| rule | decoded | address | confidence |
|---|---|---|---|
| stamina cost per tool | 5x5 shorts, every row -1,-2,-3,-4,-5; row = (tool slot-1)/5, column = charge level = held frames/20; so an uncharged use costs 1 for hoe, can, seed bag and sickle alike | table 0x13760A, read in FUN_0004966c, applied by FUN_000517f8 | high (values), medium (frame mapping) |
| tiles per charge level | 1, 3, 6, 9, 25 | 0x137600 | medium |
| max stamina | 100 at new game; raised by FUN_00051974 (cap 255), which refills stamina | FUN_00094030, FUN_00051974 | high |
| exhaustion | stamina and fatigue clamped to [0,max] and [0,100]; fatigue is held at 99 at 06:00 | FUN_000517f8 | high |
| fatigue per action | base +1 if flag DAT_001b81b0, +1 at 22-23, +2 at 00-05, +2 if stamina is 0, halved if DAT_001aef1c bit 0x200, zero if DAT_001aeee6 bit 8 | FUN_0004853c | high |
| hourly fatigue | +3 from 00, +4 from 02, +5 from 04, +10 from 06 | table 0x1376E8, FUN_0004d978 | medium |
| sleep recovery | stamina +min(127, hours*7+50); fatigue change max(-128, -(hours*2+5)) | FUN_0004d72c, FUN_0004d630, FUN_0004d65c | medium |
| forced sleep | the collapse is event driven: script opcode sets fatigue to 100 and event flag 0x49, then FUN_000259b4 starts the pass-out sleep (+50 stamina, -22 fatigue; +100/-25 in a window). No clock-hour trigger exists in code; 06:00 is the day rollover. The 02:00 ForcedSleepHour stays a placeholder | FUN_0005f5f4 case 5, FUN_000259b4 | medium (mechanism), unknown (trigger conditions) |
| crop table | 22 rows; season is 0 spring, 1 summer, 2 fall, 4 none | 0x127448 | high |
| regrow stage | regrow days = sum of stage-day values from the regrow stage to the last stage, plus one. Gives 3/4/6/9 for all nine renewable crops, as the wiki states | crop table field 2 | high |
| seed index to crop | 0 Turnip, 1 Potato, 2 Cucumber, 3 Strawberry, 4 Cabbage, 5 Tomato, 6 Corn, 7 Onion, 8 Pumpkin, 9 Pineapple, 10 Eggplant, 11 Carrot, 12 Sweet Potato, 13 Spinach, 14 Green Pepper, 15 Grass, 16 Moondrop, 17 Pink Cat, 18/19 Magic Flower, 20 Toy Flower, 21 unnamed | economy.SEED_NAMES; price, sell and grow days of every named row match the wiki | high (0-18, 20), low (19, 21) |
| festivals | 18 rows, season and 0-based day. All 18 dates equal the wiki (spring 1,8,14,18,22; summer 1,7,12,20,24; fall 3,9,13,21; winter 10,14,24,30). Event ids exist only for 6 of them | 0x13ED1C | high |

The repo crop ids sprout_beet and plain_corn are slice-local names with no decoded counterpart,
so their growDays and prices remain placeholder. A real crop is added by writing its overlay.

Not decoded: the fatigue-100 collapse condition and any clock cutoff, starting inventory (the
new-game init FUN_00094030 zeroes the rucksack, so starting items come from event scripts),
affection scale and heart tiers, animals, cooking.

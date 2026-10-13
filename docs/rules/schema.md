# Rules data schema (Phase 6)

Logic reads numbers from `rules.json` (HM.Farming.RulesData, JsonUtility). The committed file
`unity/Assets/_HM/Farming/Data/rules.json` holds placeholder values and small numeric
rule values extracted from the original (gold, prices, growth days), each tagged with its `source`
and `confidence`. The user approved committing such numbers. Game text, sprites, models and any other
asset data are never committed (ADR 0002, repo data policy).

## Value object
`{ "value": int, "source": "extracted|observed|placeholder", "confidence": "low|medium|high" }`

## rules.json (schema 1)
| key | type | meaning |
|---|---|---|
| schema | int | must be 1 |
| startingGold, maxStamina | value | start money, stamina cap (refilled each new day) |
| tools[] | `{id, staminaCost}` | ids: hoe, wateringCan, seedBag, sickle |
| crops[] | `{id, growDays, seedPrice, sellPrice}` | growDays = watered days to maturity |

## Local overlay: rules.local.json
Git-ignored, same folder. Loaded after rules.json via `RulesData.ApplyOverlay`.
`{ "overrides": [ { "path": "crop.sprout_beet.seedPrice", "value": 40, "source": "extracted", "confidence": "high" } ] }`
Paths: `startingGold`, `maxStamina`, `tool.<id>.staminaCost`, `crop.<id>.growDays|seedPrice|sellPrice`.
Unknown paths throw. Candidates from `docs/formats/tables.md`: seed buy price (f12, high),
shop item prices (f12, high). Sell values (f13) are medium. Seed index to crop id mapping is
done by whoever writes the overlay locally. Growth days (crop table 0x127448) and stamina costs (table 0x13760A) are located; see parity.md.

## dialogue.json (schema 1)
`{ schema, source, start, nodes[] }`. Node: `id, speaker, face, text, next, ifFlag, ifNext, setFlags[], choices[]`.
Choice: `text, next, requireFlag, setFlags[]`. Empty `next` ends the conversation. `face` is the
face id (text.md: face = 0x0FEF - code). Placeholder lines are original (NPC "Mira").

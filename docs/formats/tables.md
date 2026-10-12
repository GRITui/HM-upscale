# Data tables (items, crops, prices, calendar, relationships) - NOT SOLVED

Status after round 3: no rule table has been located or parsed.

## What is known
- Item/shop/event strings are not stored as ASCII; dialogue and names use the u16 glyph
  codes in text.md, so string search over the ELF finds nothing useful.
- seg011 (GF_FARM overlay) and the event_s overlay (seg012) are the likeliest homes for
  farm rules (crop growth, shipping prices). They are MIPS code plus data and need
  disassembly around the shop and shipping routines.
- Event scripts (map.md) are 16-byte commands whose opcodes are still unknown; calendar
  events and relationship checks will be expressed there.

## Next steps
1. Decode the Font_all page so item names can be searched as glyph-code sequences.
2. Locate the name string, then find u32/u16 arrays adjacent to it (price, sell value).
3. Trace the shipping-bin handler in seg011 to find the sell-price lookup base.

No parser is provided.

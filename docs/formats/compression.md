# 0x100D chunk compression

Status: SOLVED for the chunk payloads (high confidence). Verified byte-exact on real data.

## Finding

The decoder is not recognisable in hmpsp.prx (no 0x100D constant, no LZ or nibble
decoder in the 3397 decompiled functions; the only nibble routine is SPU ADPCM). The
algorithm was derived from data and confirmed by exact size match:

| sample | compressed | decoded | expected |
|--------|-----------|---------|----------|
| face.bin (seg001) block 0 | 2970 | 6400 | 80x80 8bpp |
| Font_all.tex (seg007) chunk 2 | 30291 | 31752 | 63 words x 252 rows x 2 bytes |

## Token stream (PackBits-like, own description)

No header, no end marker; the compressed size comes from the chunk header.

| control byte c | meaning |
|----------------|---------|
| 0x01..0x7F | repeat the next byte c times (2 bytes consumed) |
| 0x80..0xFF | copy the next (256 - c) bytes literally |
| 0x00 | not observed; treated as an error |

Note the literal count is 256 - c, not the 257 - c of classic PackBits. A literal run
is often followed by a control byte that looks like data (0xF9..0xFF); that is the next
literal-run token, not part of the previous run.

## Stored chunks

A chunk whose flag is 0x0400 and whose size equals w*2*h is raw (seg007 chunk 1).
That the flag means "stored" is a guess; the size comparison is what the parser uses.

## Code

tools/hmconv/decompress.py: `rle_decode`, a reference `rle_encode` (not the game's),
and `parse_image` (see texture.md). Tests: tools/hmconv/tests/test_decompress.py
(synthetic round trip, token cases, container parse).

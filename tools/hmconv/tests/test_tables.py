import struct
from hmconv import tables


def row(**k):
    f = [0] * 20
    for i, v in k.items():
        f[int(i[1:])] = v
    return struct.pack("<20H", *f)


def test_rows_and_seed_prices():
    data = b"\0" * 8 + row(f0=0xFFFE, f4=0x4000, f12=2000) + row(f4=0x4040, f5=0x0502, f12=150)
    rows = tables.parse_rows(data, off=8, n=2)
    assert rows[0]["delta"] == -2 and rows[0]["price"] == 2000
    assert tables.seed_prices(rows) == {5: 150}


def test_truncated():
    assert len(tables.parse_rows(b"\0" * 50, off=0, n=5)) == 1

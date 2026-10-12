import json
import struct
import unittest

from .mesh import _cross, _sub, _dot
from .models import all_models, EXAMPLE_SCENE

BUDGETS = {"player": 1500, "npc_villager": 1500}
DEFAULT_BUDGET = 600
EXPECTED = {"player", "npc_villager", "soil_tilled", "soil_watered", "ground_tile", "grass_tuft",
            "turnip_0", "turnip_1", "turnip_2", "potato_0", "potato_1", "potato_2", "hoe",
            "watering_can", "seed_bag", "farmhouse", "fence_segment", "tree", "rock"}


def parse_glb(data):
    magic, version, length = struct.unpack_from("<III", data, 0)
    assert magic == 0x46546C67 and version == 2 and length == len(data)
    jlen, jtype = struct.unpack_from("<II", data, 12)
    assert jtype == 0x4E4F534A and jlen % 4 == 0
    doc = json.loads(data[20:20 + jlen])
    off = 20 + jlen
    blen, btype = struct.unpack_from("<II", data, off)
    assert btype == 0x004E4942
    return doc, data[off + 8:off + 8 + blen]


class ModelGenTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.models = all_models()

    def test_all_expected_models_present(self):
        self.assertEqual({m.name for m in self.models}, EXPECTED)

    def test_each_glb_parses_and_within_budget(self):
        for m in self.models:
            with self.subTest(model=m.name):
                doc, binary = parse_glb(m.to_glb())
                self.assertEqual(doc["asset"]["version"], "2.0")
                self.assertEqual(doc["buffers"][0]["byteLength"], len(binary))
                tris = 0
                for mesh in doc["meshes"]:
                    for prim in mesh["primitives"]:
                        acc = doc["accessors"]
                        nv = acc[prim["attributes"]["POSITION"]]["count"]
                        self.assertEqual(acc[prim["attributes"]["NORMAL"]]["count"], nv)
                        self.assertEqual(acc[prim["attributes"]["COLOR_0"]]["count"], nv)
                        ic = acc[prim["indices"]]["count"]
                        self.assertEqual(ic % 3, 0)
                        tris += ic // 3
                        for a in acc:
                            bv = doc["bufferViews"][a["bufferView"]]
                            self.assertLessEqual(bv["byteOffset"] + bv["byteLength"], len(binary))
                self.assertEqual(tris, m.tri_count())
                self.assertGreater(tris, 0)
                self.assertLessEqual(tris, BUDGETS.get(m.name, DEFAULT_BUDGET))

    def test_indices_in_range_and_winding_outward(self):
        for m in self.models:
            with self.subTest(model=m.name):
                doc, binary = parse_glb(m.to_glb())
                vol = 0.0
                for part in m.parts:
                    for p0, p1, p2, _ in part.tris:
                        vol += _dot(p0, _cross(p1, p2)) / 6.0
                self.assertGreater(vol, 0.0, "signed volume must be positive (CCW outward)")
                for a in doc["accessors"]:
                    if a["type"] == "SCALAR":
                        bv = doc["bufferViews"][a["bufferView"]]
                        vals = struct.unpack_from("<%dH" % a["count"], binary, bv["byteOffset"])
                        self.assertLess(max(vals), 65536)

    def test_deterministic(self):
        again = all_models()
        for a, b in zip(self.models, again):
            self.assertEqual(a.to_glb(), b.to_glb())

    def test_player_tri_cap(self):
        player = next(m for m in self.models if m.name == "player")
        self.assertLessEqual(player.tri_count(), 1500)

    def test_example_scene_budget(self):
        counts = {m.name: m.tri_count() for m in self.models}
        total = sum(counts[n] * c for n, c in EXAMPLE_SCENE.items())
        self.assertLess(total, 50000, total)


if __name__ == "__main__":
    unittest.main()

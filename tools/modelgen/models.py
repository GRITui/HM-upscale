"""Original warm, chunky, cozy farm-sim placeholder models. Pure geometry, no external data."""
from .mesh import Model

PAL = {
    "skin": "#e8b48a", "skin_shade": "#d49a72", "hair": "#6b3f23", "hair_npc": "#d9a441",
    "shirt": "#3f7fbf", "shirt_npc": "#c8553d", "pants": "#5b4a8a", "pants_npc": "#4f6b3a",
    "boot": "#4a2f1d", "belt": "#2e2a26", "straw": "#e3c46a", "straw_dark": "#c9a54a", "apron": "#f1e6cf",
    "soil": "#7a4e2d", "soil_dark": "#5a3a22", "soil_wet": "#4a2f1d", "soil_wet_dark": "#34221a",
    "grass": "#7cbf4a", "grass_dark": "#5a9a3a", "grass_light": "#a4d65e",
    "leaf": "#4f9a3a", "leaf_light": "#7cc04a", "turnip": "#efe3f2", "turnip_top": "#a84fb0",
    "potato": "#c89a5a", "potato_dark": "#a87a3e", "flower": "#f4f0e0",
    "wood": "#a8733f", "wood_dark": "#7a4e2a", "wood_light": "#c99560",
    "steel": "#9aa6ad", "steel_dark": "#6f7a82", "can": "#4aa3a0", "can_dark": "#2f7b79",
    "bag": "#d8b27a", "bag_tie": "#b5483a", "seed": "#6a8f3a",
    "wall": "#f0dcb4", "wall_trim": "#c99560", "roof": "#c4563a", "roof_dark": "#9c3f2a",
    "door": "#7a4e2a", "glass": "#a8d8ea", "stone": "#8f8a85", "stone_dark": "#6e6a66", "stone_light": "#aaa59f",
    "trunk": "#7a4e2a", "canopy": "#4f9a3a", "canopy_light": "#6cb84a", "canopy_dark": "#3d7d30",
}
P = PAL


def _humanoid(name, shirt, pants, hair, apron=False, hat=False, tall=1.0):
    m = Model(name)
    body = m.part("body")
    body.box((0, 0.55 * tall, 0), (0.46, 0.5, 0.28), P[shirt])
    body.box((0, 0.31 * tall, 0), (0.48, 0.07, 0.30), P["belt"])
    if apron:
        body.box((0, 0.42 * tall, 0.15), (0.34, 0.42, 0.03), P["apron"])
    head = m.part("head", (0, 0.9 * tall, 0))
    head.sphere((0, 1.08 * tall, 0), (0.22, 0.22, 0.21), 8, 5, P["skin"])
    head.sphere((0, 1.14 * tall, -0.02), (0.235, 0.2, 0.225), 8, 4, P[hair])
    head.box((0, 1.04 * tall, 0.2), (0.06, 0.06, 0.05), P["skin_shade"])  # nose
    head.box((-0.08, 1.1 * tall, 0.2), (0.04, 0.05, 0.02), P["belt"])
    head.box((0.08, 1.1 * tall, 0.2), (0.04, 0.05, 0.02), P["belt"])
    if hat:
        head.cyl((0, 1.27 * tall, 0), 0.34, 0.03, 10, P["straw"])
        head.cyl((0, 1.33 * tall, 0), 0.2, 0.1, 10, P["straw_dark"], r_top=0.17)
    for side, sx in (("arm_l", -1), ("arm_r", 1)):
        a = m.part(side, (sx * 0.29, 0.76 * tall, 0))
        a.box((sx * 0.29, 0.58 * tall, 0), (0.12, 0.38, 0.14), P[shirt])
        a.box((sx * 0.29, 0.34 * tall, 0), (0.11, 0.1, 0.12), P["skin"])
    for side, sx in (("leg_l", -1), ("leg_r", 1)):
        l = m.part(side, (sx * 0.12, 0.3 * tall, 0))
        l.box((sx * 0.12, 0.16 * tall, 0), (0.18, 0.3, 0.2), P[pants])
        l.box((sx * 0.12, 0.04 * tall, 0.03), (0.2, 0.09, 0.28), P["boot"])
    return m


def player():
    return _humanoid("player", "shirt", "pants", "hair", hat=True)


def npc_villager():
    return _humanoid("npc_villager", "shirt_npc", "pants_npc", "hair_npc", apron=True, tall=0.95)


def _slab(m, wet):
    s = m.part("body")
    base, ridge = (P["soil_wet"], P["soil_wet_dark"]) if wet else (P["soil"], P["soil_dark"])
    s.box((0, 0.05, 0), (1.0, 0.1, 1.0), base)
    for z in (-0.3, 0.0, 0.3):
        s.box((0, 0.12, z), (0.92, 0.06, 0.16), ridge)
    return s


def soil_tilled():
    m = Model("soil_tilled")
    _slab(m, False)
    return m


def soil_watered():
    m = Model("soil_watered")
    s = _slab(m, True)
    for x, z in ((-0.3, -0.15), (0.25, 0.15), (0.0, 0.42)):
        s.box((x, 0.155, z), (0.14, 0.01, 0.1), "#6f8fb0")  # wet sheen
    return m


def ground_tile():
    m = Model("ground_tile")
    g = m.part("body")
    g.box((0, 0.05, 0), (1.0, 0.1, 1.0), P["grass"])
    g.box((0.3, 0.105, -0.25), (0.2, 0.01, 0.2), P["grass_light"])
    g.box((-0.3, 0.105, 0.3), (0.25, 0.01, 0.15), P["grass_dark"])
    return m


def grass_tuft():
    m = Model("grass_tuft")
    g = m.part("body")
    blades = [(0, 0, 0.32, (0, 0, 0)), (-0.07, 0.03, 0.26, (10, 0, 22)), (0.07, 0.02, 0.28, (-8, 0, -22)),
              (0.02, -0.06, 0.22, (-20, 0, 6)), (-0.03, 0.07, 0.24, (20, 0, -6))]
    for i, (x, z, h, rot) in enumerate(blades):
        g.cyl((x, h / 2, z), 0.04, h, 3, P["grass_light" if i % 2 else "grass_dark"], r_top=0.0, rot=rot)
    return m


def turnip(stage):
    m = Model("turnip_%d" % stage)
    c = m.part("plant")
    if stage == 0:
        c.cyl((0, 0.1, 0), 0.025, 0.1, 4, P["leaf_light"])
        c.sphere((-0.04, 0.17, 0), (0.06, 0.03, 0.04), 6, 3, P["leaf_light"], rot=(0, 0, 25))
        c.sphere((0.04, 0.17, 0), (0.06, 0.03, 0.04), 6, 3, P["leaf_light"], rot=(0, 0, -25))
    elif stage == 1:
        c.sphere((0, 0.07, 0), (0.07, 0.05, 0.07), 6, 3, P["turnip_top"])
        for i, a in enumerate((0, 72, 144, 216, 288)):
            c.cyl((0, 0.2, 0), 0.04, 0.22, 4, P["leaf"], r_top=0.015, rot=(28, a, 0))
    else:
        c.sphere((0, 0.1, 0), (0.15, 0.13, 0.15), 8, 4, P["turnip"])
        c.sphere((0, 0.17, 0), (0.15, 0.06, 0.15), 8, 3, P["turnip_top"])
        for i, a in enumerate((0, 60, 120, 180, 240, 300)):
            c.cyl((0, 0.36, 0), 0.05, 0.32, 4, P["leaf" if i % 2 else "leaf_light"], r_top=0.02, rot=(30, a, 0))
    return m


def potato(stage):
    m = Model("potato_%d" % stage)
    c = m.part("plant")
    if stage == 0:
        c.cyl((0, 0.08, 0), 0.025, 0.08, 4, P["leaf"])
        c.sphere((0, 0.14, 0), (0.07, 0.03, 0.07), 6, 3, P["leaf_light"])
    elif stage == 1:
        c.cyl((0, 0.13, 0), 0.03, 0.2, 4, P["leaf"])
        for i, a in enumerate((0, 90, 180, 270)):
            c.sphere((0.08, 0.2, 0), (0.08, 0.04, 0.06), 6, 3, P["leaf_light" if i % 2 else "leaf"],
                     rot=(0, a, 15))
    else:
        c.sphere((0, 0.2, 0), (0.2, 0.17, 0.2), 8, 4, P["leaf"])
        c.sphere((0.05, 0.3, -0.03), (0.12, 0.08, 0.12), 6, 3, P["leaf_light"])
        for x, z in ((0.1, 0.15), (-0.12, 0.12), (0.0, -0.18)):
            c.sphere((x, 0.06, z), (0.07, 0.05, 0.06), 6, 3, P["potato"])
        c.sphere((-0.06, 0.34, 0.06), (0.03, 0.03, 0.03), 5, 2, P["flower"])
    return m


def hoe():
    m = Model("hoe")
    h = m.part("body")
    h.cyl((0, 0.45, 0), 0.025, 0.9, 6, P["wood"])
    h.box((0, 0.9, 0.06), (0.04, 0.05, 0.14), P["steel_dark"])
    h.box((0, 0.87, 0.14), (0.05, 0.14, 0.03), P["steel"], rot=(-20, 0, 0))
    return m


def watering_can():
    m = Model("watering_can")
    w = m.part("body")
    w.cyl((0, 0.14, 0), 0.13, 0.28, 8, P["can"])
    w.cyl((0, 0.29, 0), 0.13, 0.02, 8, P["can_dark"])
    w.cyl((0, 0.18, 0.2), 0.025, 0.28, 5, P["can_dark"], r_top=0.018, rot=(-50, 0, 0))
    w.cyl((0, 0.31, 0.31), 0.05, 0.03, 6, P["steel"], rot=(-50, 0, 0))
    w.box((-0.15, 0.2, 0), (0.03, 0.2, 0.06), P["can_dark"])
    w.box((-0.09, 0.31, 0), (0.18, 0.03, 0.06), P["can_dark"])
    return m


def seed_bag():
    m = Model("seed_bag")
    b = m.part("body")
    b.sphere((0, 0.11, 0), (0.14, 0.12, 0.1), 8, 4, P["bag"])
    b.cyl((0, 0.235, 0), 0.07, 0.07, 6, P["bag"], r_top=0.035)
    b.cyl((0, 0.265, 0), 0.04, 0.03, 6, P["bag_tie"])
    b.box((0, 0.1, 0.095), (0.12, 0.09, 0.015), P["apron"])
    b.sphere((0, 0.1, 0.105), (0.03, 0.03, 0.01), 5, 3, P["seed"])
    return m


def farmhouse():
    m = Model("farmhouse")
    h = m.part("body")
    h.box((0, 0.05, 0), (3.2, 0.1, 2.8), P["stone"])
    h.box((0, 1.15, 0), (3.0, 2.1, 2.6), P["wall"])
    h.box((0, 0.2, 0), (3.06, 0.2, 2.66), P["wall_trim"])
    for sx in (-1, 1):
        for sz in (-1, 1):
            h.box((sx * 1.5, 1.15, sz * 1.3), (0.12, 2.1, 0.12), P["wood_dark"])
    h.roof((0, 2.7, 0), (3.8, 1.2, 3.2), P["roof"])
    h.box((0, 3.26, 0), (0.12, 0.08, 3.3), P["roof_dark"])
    h.box((0.9, 3.0, -0.5), (0.4, 0.9, 0.4), P["stone_dark"])
    h.box((0, 0.7, 1.32), (0.7, 1.2, 0.06), P["door"])
    h.box((0.22, 0.7, 1.36), (0.06, 0.06, 0.04), P["steel"])
    h.box((0, 0.14, 1.55), (1.0, 0.1, 0.4), P["stone_light"])
    for x in (-1.0, 1.0):
        h.box((x, 1.3, 1.32), (0.55, 0.55, 0.05), P["glass"])
        h.box((x, 1.3, 1.35), (0.65, 0.07, 0.04), P["wood_dark"])
        h.box((x, 1.3, 1.35), (0.07, 0.65, 0.04), P["wood_dark"])
    return m


def fence_segment():
    m = Model("fence_segment")
    f = m.part("body")
    for x in (-0.45, 0.45):
        f.box((x, 0.4, 0), (0.1, 0.8, 0.1), P["wood_dark"])
        f.box((x, 0.82, 0), (0.12, 0.04, 0.12), P["wood_light"])
    for y in (0.3, 0.6):
        f.box((0, y, 0), (1.0, 0.1, 0.05), P["wood"])
    return m


def tree():
    m = Model("tree")
    t = m.part("body")
    t.cyl((0, 0.7, 0), 0.22, 1.4, 6, P["trunk"], r_top=0.16)
    t.cyl((0, 0.05, 0), 0.34, 0.1, 6, P["trunk"])
    t.sphere((0, 1.9, 0), (0.95, 0.8, 0.95), 8, 5, P["canopy"], jitter=0.04, seed=3)
    t.sphere((0.3, 2.5, 0.1), (0.7, 0.6, 0.7), 8, 4, P["canopy_light"], jitter=0.04, seed=5)
    t.sphere((-0.45, 1.65, 0.3), (0.55, 0.45, 0.55), 6, 4, P["canopy_dark"], jitter=0.04, seed=7)
    return m


def rock():
    m = Model("rock")
    r = m.part("body")
    r.sphere((0, 0.22, 0), (0.42, 0.28, 0.36), 7, 4, P["stone"], jitter=0.12, seed=11)
    r.sphere((0.3, 0.1, 0.2), (0.2, 0.14, 0.18), 6, 3, P["stone_dark"], jitter=0.1, seed=13)
    r.sphere((-0.1, 0.38, -0.05), (0.2, 0.1, 0.18), 6, 3, P["stone_light"], jitter=0.08, seed=17)
    return m


def all_models():
    out = [player(), npc_villager(), soil_tilled(), soil_watered(), ground_tile(), grass_tuft()]
    for s in range(3):
        out.append(turnip(s))
    for s in range(3):
        out.append(potato(s))
    out += [hoe(), watering_can(), seed_bag(), farmhouse(), fence_segment(), tree(), rock()]
    return out


# Worked example of a full farm scene (name -> instance count) used for the ~50k tri budget check.
EXAMPLE_SCENE = {
    "player": 1, "npc_villager": 2, "farmhouse": 1, "ground_tile": 225, "soil_tilled": 40,
    "soil_watered": 24, "turnip_2": 32, "potato_2": 32, "fence_segment": 40, "tree": 12,
    "rock": 10, "grass_tuft": 100, "hoe": 1, "watering_can": 1, "seed_bag": 2,
}

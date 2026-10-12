using System;
using System.Collections.Generic;
using HM.Core;
using HM.Farming;
using UnityEngine;

namespace HM.Gameplay
{
    /// <summary>Visual stages for one crop. Slot is swappable: assign glb prefabs, or leave empty for primitives.</summary>
    [Serializable]
    public class CropSlot
    {
        public string cropId;
        public GameObject[] stages = new GameObject[3];
        public Color fallbackColour = Color.green;
    }

    /// <summary>
    /// Owns the FarmSim for the slice, maps world positions to tiles and keeps tile visuals in step.
    /// All numbers come from rules.json (placeholder values stay flagged in the json).
    /// </summary>
    public sealed class FarmWorld : MonoBehaviour
    {
        public static FarmWorld Instance { get; private set; }

        [Header("Data")]
        public TextAsset rulesJson;
        public TextAsset rulesLocalJson; // optional, never committed

        [Header("Plot grid (1 tile = 1 m, origin = corner of tile 0,0)")]
        public Vector3 origin = new Vector3(-3f, 0f, 3f);
        public int plotsX = 6;
        public int plotsY = 4;

        [Header("Art slots (empty = primitive fallback)")]
        public GameObject untilledPrefab;
        public GameObject tilledPrefab;
        public GameObject wateredPrefab;
        public CropSlot[] crops = Array.Empty<CropSlot>();
        public GameObject[] toolPrefabs = new GameObject[4]; // Hoe, WateringCan, SeedBag, Sickle
        public Material fallbackMaterial;

        // PLACEHOLDER: starting seed stock for the slice. Not from game data.
        public int startingSeeds = 4;

        public RulesData Rules { get; private set; }
        public FarmSim Sim { get; private set; }
        public Tool HeldTool { get; private set; } = Tool.Hoe;
        public string SeedCrop => Rules.crops.Length == 0 ? null : Rules.crops[cropIndex % Rules.crops.Length].id;
        public HashSet<string> Flags { get; } = new HashSet<string>();
        public string LastMessage { get; private set; } = "";
        public int Version { get; private set; }
        public event Action Changed;

        int cropIndex;
        FarmClockBridge bridge;
        GameClock boundClock;
        sealed class View { public GameObject soil; public int soilKind = -1; public GameObject crop; public int cropKey = -1; }
        readonly Dictionary<TilePos, View> views = new Dictionary<TilePos, View>();

        void Awake()
        {
            Instance = this;
            Rules = RulesData.FromJson(rulesJson.text);
            if (rulesLocalJson != null) Rules.ApplyOverlay(rulesLocalJson.text);
            Sim = new FarmSim(Rules);
            if (Rules.crops.Length > 0) Sim.BuySeeds(Rules.crops[0].id, startingSeeds);
        }

        void Start()
        {
            BindClock();
            for (int y = 0; y < plotsY; y++)
                for (int x = 0; x < plotsX; x++) views[new TilePos(x, y)] = new View();
            RefreshViews();
        }

        void BindClock()
        {
            var host = GameClockBehaviour.Instance;
            if (host == null || boundClock == host.Clock) return;
            bridge?.Dispose();
            boundClock = host.Clock;
            bridge = new FarmClockBridge(boundClock, Sim);
            boundClock.DayChanged += OnDayChanged;
        }

        void OnDayChanged(int day)
        {
            Flags.Clear();
            Notify("Day " + (day + 1) + " begins.");
        }

        void OnDestroy()
        {
            if (boundClock != null) boundClock.DayChanged -= OnDayChanged;
            bridge?.Dispose();
            if (Instance == this) Instance = null;
        }

        public void Notify(string message)
        {
            LastMessage = message;
            Version++;
            RefreshViews();
            Changed?.Invoke();
        }

        public void SelectTool(Tool t) { HeldTool = t; Notify("Holding " + t); }
        public void CycleCrop()
        {
            if (Rules.crops.Length == 0) return;
            cropIndex = (cropIndex + 1) % Rules.crops.Length;
            Notify("Seed: " + SeedCrop);
        }

        public bool BuySeed()
        {
            bool ok = SeedCrop != null && Sim.BuySeeds(SeedCrop, 1);
            Notify(ok ? "Bought 1 " + SeedCrop + " seed." : "Not enough gold.");
            return ok;
        }

        // ---- tile mapping ----
        public Vector3 TileCentre(TilePos p) => origin + new Vector3(p.X + 0.5f, 0f, -(p.Y + 0.5f));

        public bool TryGetTile(Vector3 world, out TilePos pos)
        {
            int x = Mathf.FloorToInt(world.x - origin.x);
            int y = Mathf.FloorToInt(origin.z - world.z);
            pos = new TilePos(x, y);
            return x >= 0 && y >= 0 && x < plotsX && y < plotsY;
        }

        /// <summary>Use the held tool on the plot under a world position.</summary>
        public ToolResult UseHeldAt(Vector3 world)
        {
            if (!TryGetTile(world, out var pos)) { Notify("Nothing to work here."); return ToolResult.OutOfBounds; }
            return UseAt(pos);
        }

        public ToolResult UseAt(TilePos pos)
        {
            var r = Sim.UseTool(HeldTool, pos, HeldTool == Tool.SeedBag ? SeedCrop : null);
            if (r == ToolResult.Done && HeldTool == Tool.SeedBag) Flags.Add("planted_today");
            Notify(Describe(r));
            return r;
        }

        string Describe(ToolResult r)
        {
            switch (r)
            {
                case ToolResult.Done: return HeldTool + " used.";
                case ToolResult.NoStamina: return "Too tired.";
                case ToolResult.NoSeeds: return "No seeds. Press B to buy.";
                case ToolResult.OutOfBounds: return "Out of the field.";
                default: return "That does nothing here.";
            }
        }

        /// <summary>Move all held produce into the bin. Returns how many items.</summary>
        public int ShipAll()
        {
            int n = 0;
            foreach (var c in Rules.crops)
            {
                int have = Sim.ProduceCount(c.id);
                if (have > 0 && Sim.Ship(c.id, have)) n += have;
            }
            Notify(n > 0 ? "Shipped " + n + ". Paid tomorrow." : "Nothing to ship.");
            return n;
        }

        // ---- visuals ----
        public int CropStage(SoilTile t)
        {
            if (t.CropId == null) return -1;
            int grow = Math.Max(1, Rules.Crop(t.CropId).growDays.value);
            float f = (float)t.DaysGrown / grow;
            return f >= 1f ? 2 : f >= 0.5f ? 1 : 0;
        }

        CropSlot SlotFor(string id)
        {
            foreach (var s in crops) if (s.cropId == id) return s;
            return null;
        }

        void RefreshViews()
        {
            if (Sim == null) return;
            foreach (var kv in views)
            {
                var t = Sim.TileAt(kv.Key);
                var v = kv.Value;
                int kind = t.Soil == SoilState.Untilled ? 0 : t.Watered ? 2 : 1;
                if (kind != v.soilKind)
                {
                    if (v.soil != null) Destroy(v.soil);
                    v.soil = MakeSoil(kind, TileCentre(kv.Key));
                    v.soilKind = kind;
                }
                int stage = CropStage(t);
                int key = t.CropId == null ? -1 : (t.CropId.GetHashCode() * 4) + stage;
                if (key != v.cropKey)
                {
                    if (v.crop != null) Destroy(v.crop);
                    v.crop = t.CropId == null ? null : MakeCrop(t.CropId, stage, TileCentre(kv.Key));
                    v.cropKey = key;
                }
            }
        }

        GameObject MakeSoil(int kind, Vector3 centre)
        {
            var prefab = kind == 0 ? untilledPrefab : kind == 1 ? tilledPrefab : wateredPrefab;
            GameObject go;
            if (prefab != null) go = Instantiate(prefab, centre, Quaternion.identity, transform);
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(transform, false);
                go.transform.position = centre + new Vector3(0f, 0.025f, 0f);
                go.transform.localScale = new Vector3(0.94f, 0.05f, 0.94f);
                var r = go.GetComponent<Renderer>();
                if (fallbackMaterial != null) r.sharedMaterial = fallbackMaterial;
                r.material.color = kind == 0 ? new Color(0.42f, 0.6f, 0.32f) : kind == 1 ? new Color(0.45f, 0.3f, 0.18f) : new Color(0.28f, 0.18f, 0.1f);
            }
            go.name = "Soil" + kind;
            return go;
        }

        GameObject MakeCrop(string id, int stage, Vector3 centre)
        {
            var slot = SlotFor(id);
            var pos = centre + new Vector3(0f, 0.1f, 0f); // crops sit at tile top, y = 0.1
            GameObject prefab = slot != null && slot.stages != null && stage < slot.stages.Length ? slot.stages[stage] : null;
            GameObject go;
            if (prefab != null) go = Instantiate(prefab, pos, Quaternion.identity, transform);
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(transform, false);
                float s = 0.2f + 0.2f * stage;
                go.transform.localScale = Vector3.one * s;
                go.transform.position = pos + new Vector3(0f, s * 0.5f, 0f);
                var r = go.GetComponent<Renderer>();
                if (fallbackMaterial != null) r.sharedMaterial = fallbackMaterial;
                r.material.color = slot != null ? slot.fallbackColour : Color.green;
            }
            go.name = "Crop " + id + " s" + stage;
            return go;
        }
    }
}

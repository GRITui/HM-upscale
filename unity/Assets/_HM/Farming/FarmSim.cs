using System;
using System.Collections.Generic;

namespace HM.Farming
{
    public enum Tool { Hoe, WateringCan, SeedBag, Sickle }

    public enum ToolResult
    {
        Done, NoStamina, NoEffect, NoSeeds, OutOfBounds
    }

    public enum SoilState { Untilled, Tilled }

    public struct TilePos : IEquatable<TilePos>
    {
        public int X, Y;
        public TilePos(int x, int y) { X = x; Y = y; }
        public bool Equals(TilePos o) => X == o.X && Y == o.Y;
        public override bool Equals(object o) => o is TilePos p && Equals(p);
        public override int GetHashCode() => X * 73856093 ^ Y * 19349663;
    }

    public sealed class SoilTile
    {
        public SoilState Soil;
        public bool Watered;
        public string CropId;       // null when empty
        public int DaysGrown;       // days on which the crop was watered
    }

    /// <summary>
    /// Deterministic farm rules: soil, tools, crop growth, harvest, shipping bin. No Unity types.
    /// Day rollover is driven by NewDay(); see FarmClockBridge for the GameClock hookup.
    /// </summary>
    public sealed class FarmSim
    {
        readonly RulesData rules;
        readonly Dictionary<TilePos, SoilTile> tiles = new Dictionary<TilePos, SoilTile>();
        readonly Dictionary<string, int> seeds = new Dictionary<string, int>();
        readonly Dictionary<string, int> produce = new Dictionary<string, int>();
        readonly Dictionary<string, int> bin = new Dictionary<string, int>();

        public int Width { get; }
        public int Height { get; }
        public int Gold { get; private set; }
        public int Stamina { get; private set; }
        public int MaxStamina => rules.maxStamina.value;
        public int LastPayout { get; private set; }

        public FarmSim(RulesData rules, int width = 16, int height = 16)
        {
            this.rules = rules;
            Width = width; Height = height;
            Gold = rules.startingGold.value;
            Stamina = MaxStamina;
        }

        public SoilTile TileAt(TilePos p)
        {
            if (!InBounds(p)) throw new ArgumentOutOfRangeException(nameof(p));
            if (!tiles.TryGetValue(p, out var t)) tiles[p] = t = new SoilTile();
            return t;
        }

        bool InBounds(TilePos p) => p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height;

        public int SeedCount(string crop) => seeds.TryGetValue(crop, out var n) ? n : 0;
        public int ProduceCount(string crop) => produce.TryGetValue(crop, out var n) ? n : 0;
        public int BinCount(string crop) => bin.TryGetValue(crop, out var n) ? n : 0;

        public bool BuySeeds(string crop, int count)
        {
            int cost = rules.Crop(crop).seedPrice.value * count;
            if (count <= 0 || cost > Gold) return false;
            Gold -= cost;
            seeds[crop] = SeedCount(crop) + count;
            return true;
        }

        public bool IsMature(SoilTile t) =>
            t.CropId != null && t.DaysGrown >= rules.Crop(t.CropId).growDays.value;

        /// <summary>Use a tool on a tile. Stamina is only spent when the action takes effect.</summary>
        public ToolResult UseTool(Tool tool, TilePos pos, string seedCrop = null)
        {
            if (!InBounds(pos)) return ToolResult.OutOfBounds;
            var t = TileAt(pos);
            int cost = rules.Tool(ToolId(tool)).staminaCost.value;

            switch (tool)
            {
                case Tool.Hoe:
                    if (t.Soil == SoilState.Tilled || t.CropId != null) return ToolResult.NoEffect;
                    break;
                case Tool.WateringCan:
                    if (t.Soil != SoilState.Tilled || t.Watered) return ToolResult.NoEffect;
                    break;
                case Tool.SeedBag:
                    if (t.Soil != SoilState.Tilled || t.CropId != null || seedCrop == null) return ToolResult.NoEffect;
                    if (SeedCount(seedCrop) < 1) return ToolResult.NoSeeds;
                    break;
                case Tool.Sickle:
                    if (!IsMature(t)) return ToolResult.NoEffect;
                    break;
            }
            if (Stamina < cost) return ToolResult.NoStamina;
            Stamina -= cost;

            switch (tool)
            {
                case Tool.Hoe: t.Soil = SoilState.Tilled; break;
                case Tool.WateringCan: t.Watered = true; break;
                case Tool.SeedBag:
                    seeds[seedCrop] = SeedCount(seedCrop) - 1;
                    t.CropId = seedCrop; t.DaysGrown = 0;
                    break;
                case Tool.Sickle:
                    produce[t.CropId] = ProduceCount(t.CropId) + 1;
                    t.CropId = null; t.DaysGrown = 0;
                    break;
            }
            return ToolResult.Done;
        }

        static string ToolId(Tool t)
        {
            switch (t)
            {
                case Tool.Hoe: return "hoe";
                case Tool.WateringCan: return "wateringCan";
                case Tool.SeedBag: return "seedBag";
                default: return "sickle";
            }
        }

        /// <summary>Move held produce into the shipping bin. Paid out at the next NewDay().</summary>
        public bool Ship(string crop, int count = 1)
        {
            if (count <= 0 || ProduceCount(crop) < count) return false;
            produce[crop] = ProduceCount(crop) - count;
            bin[crop] = BinCount(crop) + count;
            return true;
        }

        /// <summary>Day rollover: pay the bin, grow watered crops, clear water, restore stamina.</summary>
        public void NewDay()
        {
            int pay = 0;
            foreach (var kv in bin) pay += rules.Crop(kv.Key).sellPrice.value * kv.Value;
            bin.Clear();
            Gold += pay;
            LastPayout = pay;

            foreach (var t in tiles.Values)
            {
                if (t.CropId != null && t.Watered) t.DaysGrown++;
                t.Watered = false;
            }
            Stamina = MaxStamina;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace HM.Farming
{
    /// <summary>One tunable number with provenance. source: extracted | observed | placeholder.</summary>
    [Serializable]
    public class RuleValue
    {
        public int value;
        public string source = "placeholder";
        public string confidence = "low";
    }

    [Serializable]
    public class ToolRule
    {
        public string id;
        public RuleValue staminaCost;
    }

    [Serializable]
    public class CropRule
    {
        public string id;
        public RuleValue growDays;
        public RuleValue seedPrice;
        public RuleValue sellPrice;
    }

    /// <summary>One override from rules.local.json. path = "crop.&lt;id&gt;.seedPrice" etc.</summary>
    [Serializable]
    public class OverlayEntry
    {
        public string path;
        public int value;
        public string source = "extracted";
        public string confidence = "high";
    }

    [Serializable]
    class OverlayFile
    {
        public OverlayEntry[] overrides;
    }

    /// <summary>Rule numbers loaded from rules.json (committed, placeholder) plus an optional local overlay.</summary>
    [Serializable]
    public class RulesData
    {
        public int schema;
        public RuleValue startingGold;
        public RuleValue maxStamina;
        public ToolRule[] tools;
        public CropRule[] crops;

        public static RulesData FromJson(string json)
        {
            var r = JsonUtility.FromJson<RulesData>(json);
            if (r == null || r.schema != 1) throw new FormatException("rules.json: unsupported schema");
            r.tools ??= Array.Empty<ToolRule>();
            r.crops ??= Array.Empty<CropRule>();
            return r;
        }

        public ToolRule Tool(string id)
        {
            foreach (var t in tools) if (t.id == id) return t;
            throw new KeyNotFoundException("tool " + id);
        }

        public CropRule Crop(string id)
        {
            foreach (var c in crops) if (c.id == id) return c;
            throw new KeyNotFoundException("crop " + id);
        }

        /// <summary>
        /// Apply rules.local.json (git-ignored). Paths: startingGold, maxStamina, tool.&lt;id&gt;.staminaCost,
        /// crop.&lt;id&gt;.growDays|seedPrice|sellPrice. Unknown paths throw so typos are not silent.
        /// Returns the number of overrides applied.
        /// </summary>
        public int ApplyOverlay(string overlayJson)
        {
            var o = JsonUtility.FromJson<OverlayFile>(overlayJson);
            if (o?.overrides == null) return 0;
            foreach (var e in o.overrides)
            {
                var v = Resolve(e.path);
                v.value = e.value; v.source = e.source; v.confidence = e.confidence;
            }
            return o.overrides.Length;
        }

        RuleValue Resolve(string path)
        {
            if (path == "startingGold") return startingGold;
            if (path == "maxStamina") return maxStamina;
            var p = path.Split('.');
            if (p.Length == 3)
            {
                if (p[0] == "tool" && p[2] == "staminaCost") return Tool(p[1]).staminaCost;
                if (p[0] == "crop")
                {
                    var c = Crop(p[1]);
                    switch (p[2])
                    {
                        case "growDays": return c.growDays;
                        case "seedPrice": return c.seedPrice;
                        case "sellPrice": return c.sellPrice;
                    }
                }
            }
            throw new KeyNotFoundException("overlay path " + path);
        }
    }
}

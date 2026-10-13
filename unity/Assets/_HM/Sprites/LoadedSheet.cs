using System;
using System.IO;
using UnityEngine;

namespace HM.Sprites
{
    /// <summary>A sheet's decoded data plus one point-filtered Sprite per non-empty cell. CLUT colours are already baked into the PNG.</summary>
    public sealed class LoadedSheet
    {
        /// <summary>Raw units one sprite pixel spans on screen: the camera distance (about 10062) times the projection
        /// scale gives about 10.06 units per native screen pixel (0.0994 px per unit, docs/baseline.md), so sprites are
        /// drawn 1:1 with the screen. DERIVED, rounded to 10.</summary>
        public const float RawUnitsPerSpritePixel = 10f;
        /// <summary>Metres per sprite pixel at the raw proportions (a 46 px tall boy is about 2.3 m).</summary>
        public const float BaseMetresPerPixel = RawUnitsPerSpritePixel * HM.World.FarmCoords.DecompiledUnitsToMetres;
        /// <summary>World metres per sprite pixel. Characters scale with the world (FarmCoords.WorldScale, 1.5x by
        /// design) so they stay proportionate to the doors and furniture of the GLB: a 46 px boy is about 3.45 m and is 46 px tall on a 272 px screen, as in the original.</summary>
        public const float MetresPerPixel = BaseMetresPerPixel * HM.World.FarmCoords.WorldScale;

        public SpriteSheetData Data;
        public Texture2D Texture;
        public Sprite[] Sprites;

        /// <summary>Null when either file is missing or unreadable.</summary>
        public static LoadedSheet Load(string root, string sheet)
        {
            try
            {
                string jp = SpriteLocator.SheetPath(root, sheet, "json"), pp = SpriteLocator.SheetPath(root, sheet, "png");
                if (!File.Exists(jp) || !File.Exists(pp)) return null;
                return FromBytes(SpriteSheetData.Parse(File.ReadAllText(jp)), File.ReadAllBytes(pp));
            }
            catch (Exception e) { Debug.LogWarning("[HM-SPRITES] could not load " + sheet + ": " + e.Message); return null; }
        }

        public static LoadedSheet FromBytes(SpriteSheetData data, byte[] png)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            if (!tex.LoadImage(png)) { UnityEngine.Object.DestroyImmediate(tex); throw new FormatException("png did not decode"); }
            tex.filterMode = FilterMode.Point;
            var ls = new LoadedSheet { Data = data, Texture = tex, Sprites = new Sprite[data.cells.Length] };
            float ppu = 1f / MetresPerPixel;
            for (int i = 0; i < data.cells.Length; i++)
            {
                var c = data.cells[i];
                if (c.Empty || c.x < 0 || c.y < 0 || c.x + c.w > tex.width || c.y + c.h > tex.height) continue;
                // PNG rows are top-down, Unity textures bottom-up.
                var rect = new Rect(c.x, tex.height - c.y - c.h, c.w, c.h);
                int ox = c.origin != null && c.origin.Length > 0 ? c.origin[0] : 0, oy = c.origin != null && c.origin.Length > 1 ? c.origin[1] : 0;
                var pivot = new Vector2(-ox / (float)c.w, 1f - (-oy) / (float)c.h);
                ls.Sprites[i] = Sprite.Create(tex, rect, pivot, ppu, 0, SpriteMeshType.FullRect);
            }
            return ls;
        }

        public Sprite SpriteFor(SpriteFrame f) => f != null && f.sprite >= 0 && f.sprite < Sprites.Length ? Sprites[f.sprite] : null;
    }
}

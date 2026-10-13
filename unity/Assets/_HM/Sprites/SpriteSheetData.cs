using System;
using UnityEngine;

namespace HM.Sprites
{
    /// <summary>One decoded sprite resource (res_0e) as written by tools/hmconv: a PNG atlas plus this JSON.</summary>
    [Serializable]
    public sealed class SpriteSheetData
    {
        public SpriteCell[] cells;
        public SpriteAnim[] anims;
        public SpriteFrame[] frames;

        public static SpriteSheetData Parse(string json)
        {
            var d = JsonUtility.FromJson<SpriteSheetData>(json);
            if (d == null) throw new FormatException("sprite json is empty");
            d.cells ??= Array.Empty<SpriteCell>();
            d.anims ??= Array.Empty<SpriteAnim>();
            d.frames ??= Array.Empty<SpriteFrame>();
            return d;
        }

        public bool HasAnim(int i) => i >= 0 && i < anims.Length && anims[i].frames != null && anims[i].frames.Length > 0;
        public SpriteFrame Frame(int i) => i >= 0 && i < frames.Length ? frames[i] : null;
        public SpriteCell Cell(int sprite) => sprite >= 0 && sprite < cells.Length ? cells[sprite] : null;
    }

    [Serializable]
    public sealed class SpriteCell
    {
        public int sprite, x, y, w, h;
        /// <summary>Pivot offset from the cell's top-left, in pixels (usually negative: the pivot is below and right of the origin).</summary>
        public int[] origin;
        public bool Empty => w <= 0 || h <= 0;
    }

    [Serializable]
    public sealed class SpriteAnim
    {
        public int frame_count;
        public int[] frames;
    }

    [Serializable]
    public sealed class SpriteFrame
    {
        public int sprite, xoff, yoff, duration, control;
    }
}

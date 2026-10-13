using System;

namespace HM.Sprites
{
    public enum Facing { Down = 0, Up = 1, Left = 2, Right = 3 }

    /// <summary>
    /// Steps one animation table. Frame control: 255 holds on that frame forever, 128 loops back to the first frame after
    /// its duration, anything else advances. Duration is in 60 Hz ticks. Pure logic, no Unity objects.
    /// </summary>
    public sealed class SpriteAnimPlayer
    {
        public const float TicksPerSecond = 60f;
        public const int ControlHold = 255, ControlLoop = 128;

        readonly SpriteSheetData sheet;
        int[] frames = Array.Empty<int>();
        int pos;
        float timer;
        public int AnimIndex { get; private set; } = -1;
        public bool Finished { get; private set; }

        public SpriteAnimPlayer(SpriteSheetData sheet) { this.sheet = sheet; }

        public SpriteFrame Current
        {
            get
            {
                if (frames.Length == 0) return null;
                return sheet.Frame(frames[Math.Min(pos, frames.Length - 1)]);
            }
        }

        /// <summary>Start an animation. Re-selecting the running one is a no-op unless restart is true.</summary>
        public bool Play(int anim, bool restart = false)
        {
            if (!sheet.HasAnim(anim)) return false;
            if (anim == AnimIndex && !restart) return true;
            AnimIndex = anim;
            frames = sheet.anims[anim].frames;
            pos = 0; timer = 0f; Finished = false;
            return true;
        }

        public void Advance(float seconds)
        {
            if (frames.Length == 0 || Finished) return;
            timer += seconds;
            for (int guard = 0; guard < 64; guard++)
            {
                var f = Current;
                if (f == null) return;
                if (f.control == ControlHold) { Finished = true; return; }
                float dur = Math.Max(1, f.duration) / TicksPerSecond;
                if (timer < dur) return;
                timer -= dur;
                if (f.control == ControlLoop) pos = 0;
                else if (pos + 1 < frames.Length) pos++;
                else { Finished = true; return; } // ran off the end without a loop flag: rest on the last frame
            }
        }
    }
}

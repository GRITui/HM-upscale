using System.IO;
using HM.Sprites;
using NUnit.Framework;
using UnityEngine;

namespace HM.Tests
{
    /// <summary>Sprite layer logic with synthetic data only; no converted assets are read.</summary>
    public class SpriteTests
    {
        const string Json = "{\"cells\":[{\"sprite\":0,\"x\":0,\"y\":0,\"w\":4,\"h\":8,\"origin\":[-2,-6]},{\"sprite\":1,\"x\":4,\"y\":0,\"w\":0,\"h\":0,\"origin\":[0,0]},{\"sprite\":2,\"x\":4,\"y\":0,\"w\":4,\"h\":8,\"origin\":[-2,-6]}],"
            + "\"anims\":[{\"frame_count\":2,\"frames\":[0,1]},{\"frame_count\":3,\"frames\":[2,3,4]},{\"frame_count\":1,\"frames\":[5]}],"
            + "\"frames\":[{\"sprite\":0,\"duration\":1,\"control\":255},{\"sprite\":2,\"duration\":1,\"control\":255},"
            + "{\"sprite\":0,\"duration\":6,\"control\":0},{\"sprite\":2,\"duration\":6,\"control\":0},{\"sprite\":1,\"duration\":6,\"control\":128},"
            + "{\"sprite\":2,\"duration\":6,\"control\":255}]}";

        static SpriteSheetData Data() => SpriteSheetData.Parse(Json);

        [Test]
        public void ParsesCellsAnimsAndFrames()
        {
            var d = Data();
            Assert.AreEqual(3, d.cells.Length);
            Assert.IsTrue(d.cells[1].Empty);
            Assert.IsTrue(d.HasAnim(1));
            Assert.IsFalse(d.HasAnim(7));
            Assert.AreEqual(6, d.frames.Length);
        }

        [Test]
        public void LoopControlWrapsAndDurationIsSixtyHertz()
        {
            var p = new SpriteAnimPlayer(Data());
            Assert.IsTrue(p.Play(1));
            Assert.AreEqual(0, p.Current.sprite);
            p.Advance(0.05f);                      // 6 ticks = 0.1 s, not yet
            Assert.AreEqual(0, p.Current.sprite);
            p.Advance(0.06f);
            Assert.AreEqual(2, p.Current.sprite);
            p.Advance(0.1f);
            Assert.AreEqual(1, p.Current.sprite);  // loop-flagged frame
            p.Advance(0.1f);
            Assert.AreEqual(0, p.Current.sprite);  // wrapped to the first
            Assert.IsFalse(p.Finished);
        }

        [Test]
        public void HoldControlStopsAdvancing()
        {
            var p = new SpriteAnimPlayer(Data());
            p.Play(0);
            p.Advance(5f);
            Assert.IsTrue(p.Finished);
            Assert.AreEqual(0, p.Current.sprite);
        }

        [Test]
        public void UnknownAnimIsRejected()
        {
            var p = new SpriteAnimPlayer(Data());
            Assert.IsFalse(p.Play(9));
            Assert.IsNull(p.Current);
        }

        [Test]
        public void FacingFromScreenDelta()
        {
            Assert.AreEqual(Facing.Up, SpriteActor.FacingFromScreenDelta(new Vector2(0.1f, 1f)));
            Assert.AreEqual(Facing.Down, SpriteActor.FacingFromScreenDelta(new Vector2(0.1f, -1f)));
            Assert.AreEqual(Facing.Left, SpriteActor.FacingFromScreenDelta(new Vector2(-1f, 0.2f)));
            Assert.AreEqual(Facing.Right, SpriteActor.FacingFromScreenDelta(new Vector2(1f, 0.2f)));
        }

        [Test]
        public void RoleOverrideReplacesDefault()
        {
            var r = SpriteLocator.Roles("{\"roles\":[{\"role\":\"player\",\"sheet\":\"x_00\"}]}");
            Assert.AreEqual("x_00", r["player"]);
            Assert.IsTrue(r.ContainsKey("cow"));
            Assert.AreEqual("obj_14_r00", SpriteLocator.Roles("not json")["player"]);
        }

        [Test]
        public void LoadedSheetIsPointFilteredWithPivotFromOrigin()
        {
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++) tex.SetPixel(x, y, new Color(1, 0, 0, 1));
            var png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            var s = LoadedSheet.FromBytes(Data(), png);
            Assert.AreEqual(FilterMode.Point, s.Texture.filterMode);
            Assert.IsNotNull(s.Sprites[0]);
            Assert.IsNull(s.Sprites[1], "empty cell has no sprite");
            Assert.AreEqual(2f, s.Sprites[0].pivot.x, 1e-3f);
            Assert.AreEqual(2f, s.Sprites[0].pivot.y, 1e-3f, "8 px cell, pivot 6 px from the top");
            Object.DestroyImmediate(s.Texture);
        }

        [Test]
        public void MissingSheetLoadsAsNull()
        {
            Assert.IsNull(LoadedSheet.Load(Path.Combine(Path.GetTempPath(), "hm-no-such-root"), "nope"));
        }
    }
}

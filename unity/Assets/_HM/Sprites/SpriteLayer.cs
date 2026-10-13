using System.Collections.Generic;
using UnityEngine;

namespace HM.Sprites
{
    /// <summary>
    /// Runtime sprite layer over the farm overlay. Everything is loaded from the git-ignored converted output; if it is absent
    /// the caller keeps its placeholder visuals and this returns false.
    /// </summary>
    public static class SpriteLayer
    {
        const string Tag = "[HM-SPRITES] ";

        public sealed class Result
        {
            public SpriteActor Player;
            public readonly List<SpriteActor> Others = new List<SpriteActor>();
        }

        /// <summary>
        /// Replace the player's visual with the boy sprite and scatter demo billboards (animals, an NPC) around an anchor.
        /// Placement is a stand-in: the decoded map instances are 3D model placements, not sprite actors, so true actor
        /// positions are not known yet.
        /// </summary>
        public static Result Spawn(Transform player, Vector3 anchor, Transform parent, string rootOverride = null)
        {
            string root = rootOverride ?? SpriteLocator.FindRoot();
            if (root == null) { Debug.Log(Tag + "no converted sprites found; keeping placeholder visuals"); return null; }
            var roles = SpriteLocator.Roles();
            var cache = new Dictionary<string, LoadedSheet>();
            LoadedSheet Get(string role)
            {
                if (!roles.TryGetValue(role, out var name)) return null;
                if (!cache.TryGetValue(name, out var s)) cache[name] = s = LoadedSheet.Load(root, name);
                return s;
            }

            var res = new Result();
            var ps = Get("player");
            if (ps != null && player != null)
            {
                res.Player = SpriteActor.Create("PlayerSprite", ps, player, Vector3.zero, SpriteActor.Mode.Directional);
                // Hide the placeholder model but keep the controller; the sprite lives under the same transform.
                foreach (var r in player.GetComponentsInChildren<Renderer>(true)) if (r.gameObject != res.Player.gameObject) r.enabled = false;
            }
            else Debug.Log(Tag + "player sheet missing; keeping the placeholder player");

            var demo = new (string role, Vector3 offset)[] { ("cow", new Vector3(3f, 0f, 2f) * HM.World.FarmCoords.WorldScale), ("sheep", new Vector3(-3f, 0f, 3f) * HM.World.FarmCoords.WorldScale) };
            foreach (var (role, off) in demo)
            {
                var s = Get(role);
                if (s == null) continue;
                res.Others.Add(SpriteActor.Create(role, s, parent, anchor + off, SpriteActor.Mode.Loop, 0));
            }
            Debug.Log(Tag + "spawned player=" + (res.Player != null) + " others=" + res.Others.Count);
            return res;
        }
    }
}

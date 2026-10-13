using System;
using System.Collections.Generic;
using HM.Core;
using HM.Events;
using UnityEngine;

namespace HM.Gameplay
{
    /// <summary>
    /// IEventHost for the SliceFarm. Time, player position and dialogue are real; camera, actors other than
    /// the player, audio and every engine-internal op are logged stubs that complete immediately (so a
    /// script never stalls on behaviour we have not decoded). Each distinct stub is logged once and kept in
    /// <see cref="StubLog"/>. No game text lives here: dialogue is by message id through the DialogueBridge.
    /// </summary>
    public sealed class FarmEventHost : EventHostBase
    {
        const string Tag = "[HM-EVENT] ";
        readonly Transform player;
        readonly DialogueBridge bridge;
        readonly Func<GameClock> clock;
        readonly HashSet<string> seen = new HashSet<string>();
        readonly Dictionary<int, int> fields = new Dictionary<int, int>();
        System.Random rng = new System.Random(1);
        int mapId;

        /// <summary>Distinct stubbed behaviours that were hit, in order ("op name args").</summary>
        public readonly List<string> StubLog = new List<string>();
        public bool InputLocked { get; private set; }
        public int CurrentMap { get => mapId; set => mapId = value; }

        public FarmEventHost(Transform player, DialogueBridge bridge, Func<GameClock> clock)
        {
            this.player = player;
            this.bridge = bridge;
            this.clock = clock;
        }

        public void Seed(int seed) => rng = new System.Random(seed);

        void Stub(string what)
        {
            if (!seen.Add(what)) return;
            StubLog.Add(what);
            Debug.Log(Tag + "stub: " + what);
        }

        // ---- fields and clock ----
        public override int GetField(int kind, int index)
        {
            var c = clock?.Invoke();
            switch (kind)
            {
                case 10: return c != null ? (int)c.HourOfDay : 0;
                case 26: return c != null ? (int)c.MinuteOfDay % 60 : 0;
                case 11: return c != null ? (int)c.Date.Weekday : 0;
                case 12: return c != null ? c.Date.Year : 1;
                case 13: return c != null ? (int)c.Date.Season : 0;
                case 14: return c != null ? c.Date.DayOfSeason : 1;
                case 15: return mapId;
                case 21: case 22: case 23:
                    GetActorPos(0x29, out int y, out int x, out int z);
                    return kind == 21 ? x : kind == 22 ? y : z;
            }
            return fields.TryGetValue(kind * 4096 + index, out int v) ? v : 0;
        }

        public override void SetField(int kind, int index, int value)
        {
            if (kind == 15) { mapId = value; return; }
            if (kind >= 10 && kind <= 14 || kind == 26) { Stub("set_field clock kind " + kind); return; }
            fields[kind * 4096 + index] = value;
        }

        public override long TotalMinutes()
        {
            var c = clock?.Invoke();
            return c != null ? (long)c.TotalMinutes : 0;
        }

        // ---- dialogue ----
        public override void DialogBegin(int channel, int entry) => bridge.Begin(channel, entry);
        public override bool DialogPoll(out int result) => bridge.Poll(out result);

        // ---- player and actors ----
        static bool IsPlayer(int actor) => actor == 0x29;

        public override void SetPlayerPos(int mode, int y, int x, int z) => Teleport(y, x, z);

        public override void SetActorPos(int actor, int y, int x, int z)
        {
            if (IsPlayer(actor)) Teleport(y, x, z);
            else Stub("set_pos npc " + actor);
        }

        void Teleport(int y, int x, int z)
        {
            if (player == null) return;
            var w = HM.World.FarmCoords.ToWorld(x, y, z);
            var cc = player.GetComponent<CharacterController>();
            bool was = cc != null && cc.enabled;
            if (was) cc.enabled = false;
            // Keep the floor height: the raw y of a spot is not the walkable surface in our collision floor.
            player.position = new Vector3(w.x, player.position.y, w.z);
            if (was) cc.enabled = true;
        }

        public override bool GetActorPos(int actor, out int y, out int x, out int z)
        {
            y = 0; x = 0; z = 0;
            if (!IsPlayer(actor) || player == null) return false;
            float s = HM.World.FarmCoords.UnitsToMetres;
            var p = player.position;
            x = Mathf.RoundToInt((HM.World.FarmCoords.MirrorX ? -p.x : p.x) / s);
            z = Mathf.RoundToInt(p.z / s);
            return true;
        }

        public override void GetActorCell(int actor, out int x, out int z)
        {
            GetActorPos(actor, out _, out x, out z);
        }

        public override bool MoveActor(int actor, int dir, int target, int speed, int anim, bool begin)
        { if (begin) Stub("move_actor " + (IsPlayer(actor) ? "player" : "npc")); return true; }
        public override bool PlayAnim(int actor, int anim, int param, bool immediate, bool begin)
        { if (begin) Stub("anim/emote actor " + actor + " anim " + anim); return true; }
        public override void SetActorVisible(int actor, bool visible) => Stub("set_visible actor " + actor);
        public override void FacePlayer(int actor) => Stub("face_player actor " + actor);
        public override void FaceActor(int actor, int target) => Stub("face_actor " + actor);
        public override void SetFace(int actor, int face) => Stub("set_face actor " + actor);
        public override void ResetActor(int actor) => Stub("reset_actor");
        public override void InitActor(int actor) => Stub("init_actor");
        public override void ClearActorFlags(int actor, int mode) => Stub("clear_actor_flags");
        public override void ActorEffect(int actor, int effect) => Stub("actor_effect " + effect);
        public override void SetActorMap(int actor, int map, bool reset) => Stub("set_actor_map");

        // ---- camera, screen, audio (stubs) ----
        public override bool CameraMove(int speedSel, int y, int x, int z, bool begin)
        { if (begin) Stub("camera_move"); return true; }
        public override void CameraFollow(int actor, bool moveCamera, int speed) => Stub("camera_follow");
        public override bool ScreenFade(int variant, int duration, int speed, bool begin)
        { if (begin) Stub("screen_fade variant " + variant); return true; }
        public override void ScreenTint(int r, int g, int b) => Stub("screen_tint");
        public override void Bgm(int track, bool alsoSfx, int mode) => Stub("bgm");
        public override bool BgmFade(int frames, int phase, bool begin)
        { if (begin) Stub("bgm_fade"); return true; }
        public override void Sfx(int id) => Stub("sfx");

        // ---- items and UI ----
        public override void SetInputLock(bool locked) => InputLocked = locked;
        public override void PrintNumber(int slot, int value) => Stub("print_number");
        public override void GiveItem(int item, bool notify) => Stub("give_item");
        public override void RemoveItem(int item, int bag, int count) => Stub("remove_item");
        public override void StashInventory(bool save) => Stub("stash_inventory");

        public override int Random(int range) => range <= 0 ? 0 : rng.Next(range);

        public override void ScriptEnded(int variant, int flag, int cleanupBits) { InputLocked = false; }

        public override int EngineValue(int id) => id == EngineId.GameMode ? 3 : 0;

        public override bool Engine(int op, int p1, int p2, int p3, int p4, bool begin, EventState state)
        {
            if (begin) Stub("engine op " + op + " (" + p1 + "," + p2 + "," + p3 + "," + p4 + ")");
            return true;
        }

        public override void Unimplemented(int op, string name, string confidence)
            => Stub("unimplemented op " + op + " " + name + " [" + confidence + "]");
    }
}

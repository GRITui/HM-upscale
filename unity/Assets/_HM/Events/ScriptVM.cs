using System;
using System.Collections.Generic;

namespace HM.Events
{
    /// <summary>
    /// Deterministic interpreter for one map-script block, mirroring FUN_000596e0.
    /// A handler result of 1 jumps to on_done, 0 to on_wait (0xff = next command). A command whose
    /// yield bit is set ends the frame. Pure C#: all world effects go through <see cref="IEventHost"/>.
    /// High and medium confidence opcodes are implemented; low and unknown ones are logged and
    /// treated as done (never a crash).
    /// </summary>
    public sealed class ScriptVM
    {
        public const int Next = 0xFF;
        public const int EndOp = 0xFF;
        public const int PlayerActor = 0x29;
        public const int AllNpcs = 0x2A;
        public const int FlagKindFirst = 0x31, FlagKindLast = 0x31F, FlagBias = 0x30;
        public const int MaxStepsPerTick = 1024;

        readonly ScriptCommand[] cmds;
        readonly IEventHost host;

        public EventState State { get; }
        public int ScriptIndex { get; }
        public int Pc { get; private set; }
        public bool Finished { get; private set; }
        public bool Truncated { get; private set; }
        public long Steps { get; private set; }
        public readonly List<string> Log = new List<string>();

        // Per-command progress for multi-frame operations; cleared whenever the pc moves.
        bool started;
        int timer;
        long target;
        int dialogResult;
        // Op 80 rewrites the on_done byte of its own command in the RAM image; kept per run, never in the shared data.
        readonly Dictionary<int, int> doneOverride = new Dictionary<int, int>();

        public ScriptVM(EventScript script, EventState state, IEventHost host, int scriptIndex = 0)
        {
            cmds = script?.commands ?? new ScriptCommand[0];
            State = state ?? new EventState();
            this.host = host ?? new EventHostBase();
            ScriptIndex = scriptIndex;
            if (cmds.Length == 0) Finished = true;
        }

        public void Abort() => Finished = true;

        /// <summary>Run commands until one yields, the script ends, or the step budget is spent.</summary>
        public void Tick()
        {
            int budget = MaxStepsPerTick;
            while (!Finished && budget-- > 0)
            {
                if (Pc < 0 || Pc >= cmds.Length) { Finished = true; return; }
                var c = cmds[Pc];
                if (c.op == EndOp) { Finished = true; return; }
                Steps++;
                bool ok = Exec(c);
                if (Finished) return;
                int to = ok ? (doneOverride.TryGetValue(Pc, out int ov) ? ov : c.done) : c.wait;
                int nextPc = to == Next ? Pc + 1 : to;
                if (nextPc != Pc) { started = false; timer = 0; target = 0; }
                Pc = nextPc;
                if (c.Yields) return;
            }
            if (!Finished && budget <= 0) Truncated = true;
        }

        // ---- value helpers ----

        public static bool Compare(int lhs, int mode, int rhs)
        {
            switch (mode)
            {
                case 0: return lhs >= rhs;
                case 1: return lhs > rhs;
                case 2: return lhs <= rhs;
                case 3: return lhs < rhs;
                case 4: return lhs == rhs;
                default: return true;   // 5 or more is always true
            }
        }

        public static int Arith(int cur, int mode, int val)
        {
            switch (mode)
            {
                case 1: return unchecked(cur + val);
                case 2: return unchecked(cur - val);
                case 3: return unchecked(cur * val);
                case 4: return val == 0 ? cur : cur / val;
                case 5: return val == 0 ? cur : cur % val;
                default: return val;
            }
        }

        static bool IsFlagKind(int k) => k >= FlagKindFirst && k <= FlagKindLast;

        int GetField(int kind, int index) =>
            IsFlagKind(kind) ? State.GetFlag(kind - FlagBias) : host.GetField(kind, index);

        void SetField(int kind, int index, int value)
        {
            if (IsFlagKind(kind)) State.SetFlag(kind - FlagBias, value);
            else host.SetField(kind, index, value);
        }

        int Var(int i) => State.GetVar(i);
        int Actor(ScriptCommand c, int slot, bool isVar) { int a = c.P(slot); return isVar ? Var(a) : a; }
        int MinuteOfDay() => GetField(10, 0) * 60 + GetField(26, 0);

        void Note(string s) { if (Log.Count < 256) Log.Add(s); }

        bool Unimpl(ScriptCommand c)
        {
            State.CountUnhandled(c.op);
            host.Unimplemented(c.op, OpInfo.Name(c.op), OpInfo.LevelName(c.op));
            Note("pc " + Pc + ": op " + c.op + " (" + OpInfo.Name(c.op) + ", " + OpInfo.LevelName(c.op) + ") not implemented");
            return true;
        }

        // ---- interpreter ----

        bool Exec(ScriptCommand c)
        {
            int p1 = c.P(0), p2 = c.P(1), p3 = c.P(2), p4 = c.P(3);
            bool begin = !started;
            started = true;
            switch (c.op)
            {
                case 0: return host.MoveActor(p1, p2, p3, p4, -1, begin);
                case 1: return host.MoveActor(p1, p2, p3, 0, p4, begin);
                case 2:
                {
                    int entry = p4 != 0 ? Var(p2) : p2;
                    if (begin) host.DialogBegin(p1 >= 0 && p1 <= 4 ? p1 : 0, entry);
                    if (!host.DialogPoll(out dialogResult)) return false;
                    if (p3 >= 0) State.SetVar(p3, dialogResult);
                    return true;
                }
                case 3:
                    if (begin) timer = p1;
                    if (timer > 0) { timer--; return false; }
                    return true;
                case 4: host.SetActorPos(p1, p2, p3, p4); return true;
                case 5: return host.PlayAnim(p1, p2, p3, p4 != 0, begin);
                case 6: host.SetActorVisible(Actor(c, 0, p3 != 0), p2 != 0); return true;
                case 7:
                    if (p3 >= 0) State.SetVar(p3, host.GetActorMap(p1));
                    else host.SetActorMap(p1, p2, p4 != 0);
                    return true;
                case 9: return host.CameraMove(p1, p2, p3, p4, begin);
                case 12: host.FacePlayer(Actor(c, 0, p2 != 0)); return true;
                case 13: return Compare(GetField(p1, p2), p3, p4);
                case 14: return Compare(GetField(p1, p2), p3, Var(p4));
                case 15: return Compare(Var(p1), p3, p4);
                case 16: return Compare(Var(p1), p3, Var(p2));
                case 17: { int v = GetField(p1, p2); return v >= p3 && v <= p4; }
                case 19: SetField(p1, p2, Arith(GetField(p1, p2), p4, p3)); return true;
                case 20: SetField(p1, p2, Arith(GetField(p1, p2), p4, Var(p3))); return true;
                case 21: State.SetVar(p1, Arith(Var(p1), p4, p2 != 0 ? Var(p3) : p3)); return true;
                case 22: State.SetVar(p3, Arith(Var(p3), p4, GetField(p1, p2))); return true;
                case 23: return host.TransitionDone();
                case 24: host.Bgm(p1, p2 != 0, p3); return true;
                case 26: return host.BgmFade(p1, p2, begin);
                case 27:
                    if (p3 != 0) return host.ScreenFade(p1, p2, p4, false);   // query_done: poll only
                    host.ScreenFade(p1, p2, p4, true);                          // start; does not block
                    return true;
                case 28: host.SetInputLock(p1 == 0); return true;
                case 29:
                case 30:
                {
                    bool has = host.ItemCount(p1, p2) >= p3;
                    if (has && p4 != 0) host.RemoveItem(p1, p2, p3);
                    return has;
                }
                case 31: host.GiveItem(p1, false); return true;
                case 32: host.GiveItem(p1, true); return true;
                case 34:
                {
                    int dx = Math.Abs(GetField(21, 0) - p3), dz = Math.Abs(GetField(23, 0) - p4);
                    return dx <= p1 && dz <= p2;
                }
                case 35: host.ResetActor(p1); return true;
                case 36:
                {
                    int h = (p3 & 1) != 0 ? Var(p1) : p1, m = (p3 & 2) != 0 ? Var(p2) : p2;
                    return MinuteOfDay() >= h * 60 + m;
                }
                case 37: host.ScriptEnded(p1, p3, p4); Finished = true; return true;
                case 38:
                {
                    int r = host.Random(p1);
                    if (p2 >= 0) State.SetVar(p2, r);
                    return Compare(r, p3, p4);
                }
                case 42: host.ScreenTint(p2, p3, p4); return true;
                case 43: host.ActorEffect(p1, p2); return true;
                case 46: host.StashInventory(p1 == 0); return true;
                case 47: host.PrintNumber(p2, p4 != 0 ? Var(p3) : p3); return true;
                case 51: host.CameraFollow(Actor(c, 0, p2 != 0), p3 != 0, p4); return true;
                case 52: if (p1 >= 0 && p1 < EventState.SlotCount) { State.Slots[p1, 0] = p2; State.Slots[p1, 1] = p3; State.Slots[p1, 2] = p4; } return true;
                case 53: if (p1 >= 0 && p1 < EventState.SlotCount) State.SlotSet[p1] = p2 != 0; return true;
                case 55: if (p1 >= 0 && p1 < EventState.SlotCount) State.SetVar(p2, State.SlotSet[p1] ? 1 : 0); return true;
                case 60: host.SetActorPos(p1, Var(p2), Var(p3), Var(p4)); return true;
                case 61:
                    if (p1 == 1) host.SetPlayerPos(1, Var(p2), Var(p3), Var(p4));
                    else host.SetPlayerPos(p1, p2, p3, p4);
                    return true;
                case 64: host.SetFace(p2, p1); return true;
                case 66:
                    if (p1 == 0) { State.SetFlag(p3, p2); return true; }
                    return State.GetFlag(p3) == p2;
                case 70: host.Sfx(p1); return true;
                case 71: host.ClearActorFlags(p1, p2); return true;
                case 76:
                {
                    int h = (p4 & 1) != 0 ? Var(p1) : p1, m = (p4 & 2) != 0 ? Var(p2) : p2;
                    if (p3 != 0) return MinuteOfDay() >= h * 60 + m;           // absolute clock time
                    if (begin) target = host.TotalMinutes() + h * 60 + m;      // relative duration
                    return host.TotalMinutes() >= target;
                }
                case 78: host.InitActor(p1); return true;
                case 79: State.SetVar(p1, c.wait == Next ? Pc + 1 : c.wait); return true;   // return address for a call
                case 87: return Compare(GetField(p1, Var(p2)), p3, p4);
                case 88: SetField(p1, Var(p2), Arith(GetField(p1, Var(p2)), p4, p3)); return true;
                case 91:
                case 97:
                    SetField(p1, 0, p3);
                    Note("pc " + Pc + ": op " + c.op + " applied to index 0 only");
                    return true;
                case 94:
                {
                    host.GetActorCell(Actor(c, 0, p2 != 0), out int x, out int z);
                    State.SetVar(p3, x); State.SetVar(p4, z);
                    return true;
                }
                case 103:
                {
                    int a = (p2 & 1) != 0 ? Var(p1) : p1, b = (p2 & 2) != 0 ? Var(p4) : p4;
                    host.FaceActor(a, b);
                    return true;
                }
                case 8:
                case 25:
                case 62:
                case 63:
                case 77:
                case 82:
                case 83:
                    return host.Engine(c.op, p1, p2, p3, p4, begin, State);
                case 10: ScriptRelease(p1, p2); return true;
                case 11: return SleepTrigger(p1, p2, p3, p4);
                case 40:
                {
                    int id = p4 != 0 ? Var(p1) : p1;
                    if (p2 == 0) return host.Engine(40, id, p2, p3, 0, begin, State);
                    if (p2 >= 5) return true;
                    int st = host.AnimalState(id);
                    switch (p2)
                    {
                        case 1: return st == 1;
                        case 2: return st == 3;
                        case 3: return st == 2;
                        default: return st != 0 && st != 1;
                    }
                }
                case 50: return host.Engine(50, p3 != 0 ? Var(p1) : p1, p2, p3, p4, begin, State);
                case 54:
                {
                    if (p1 < 0 || p1 >= EventState.SlotCount) return true;
                    if (!host.GetActorPos(p2, out int y, out int x, out int z)) return true;
                    int dir = p4 != 0 ? host.DirectionBetween(PlayerActor, p2) : p3;
                    int dx = 0, dz = 0;
                    switch (dir) { case 0: dz = -100; break; case 1: dx = -100; break; case 2: dz = 100; break; case 3: dx = 100; break; }
                    State.Slots[p1, 0] = y - 100; State.Slots[p1, 1] = x + dx; State.Slots[p1, 2] = z + dz;
                    State.SlotSet[p1] = true;
                    return true;
                }
                case 56: return host.EngineValue(EngineId.GameMode) == 3;
                case 57:
                {
                    int item = host.EngineValue(EngineId.HeldItem);
                    if (item == 0) return false;
                    int pref = host.ItemPreference(item, p3 != 0 ? Var(p1) : p1);
                    State.SetVar(p2, pref >= 0 && pref < 7 ? pref + 1 : 0);
                    return true;
                }
                case 58:
                {
                    int a = host.EngineValue(EngineId.HeldItem), b = host.EngineValue(EngineId.HeldAlt);
                    if (p2 == 0) { a = host.EngineValue(EngineId.HeldItem2); b = host.EngineValue(EngineId.HeldAlt2); }
                    if (p3 != 0 && a == 0xA4) a = b;
                    if (a == 0) return false;
                    if (p1 != -1) State.SetVar(p1, a);
                    return true;
                }
                case 59:
                    if (p1 == host.EngineValue(EngineId.CurrentState)) return true;
                    if (p1 == -1) host.Engine(59, p1, p2, p3, p4, begin, State);
                    return false;
                case 69:
                {
                    int bank = p4 != 0 ? EventState.BankPlayer : EventState.BankMode;
                    int mask = p4 != 0 ? p3 & 0xFFFF : p3;
                    if (p1 != 0) return (State.Bank[bank] & mask) != 0;
                    if (p2 != 0) State.Bank[bank] |= mask; else State.Bank[bank] &= ~mask;
                    return true;
                }
                case 72:
                    return host.Engine(72, p1, Var(p2) << 16 >> 16, Var(p3), Var(p4), begin, State);
                case 73:
                {
                    if (p1 != PlayerActor && (p1 < 1 || p1 > 0x24)) return false;
                    if (host.GetActorPos(p1, out int y, out int x, out int z))
                    {
                        if (p2 != -1) State.SetVar(p2, y);
                        if (p3 != -1) State.SetVar(p3, x);
                        if (p4 != -1) State.SetVar(p4, z);
                    }
                    return true;
                }
                case 74:
                {
                    if (p1 == 0) return true;
                    if (p2 == -1) return false;
                    State.SetVar(p2, host.EngineValue(EngineId.EventY));
                    State.SetVar(p2 + 1, host.EngineValue(EngineId.EventX));
                    State.SetVar(p2 + 2, host.EngineValue(EngineId.EventZ));
                    return true;
                }
                case 80: doneOverride[Pc] = Var(p1) & 0xFF; return true;                    // return: jump to the address in v[p1]
                case 81: return BigSwitch(p1, p2, p3, p4, begin);
                case 84:
                    State.SetFlag(4, p1 == 1 ? 1 : 0);
                    State.SetFlag(5, p1 == 0 ? 1 : 0);
                    State.SetFlag(0xB0, p1 == 0 ? 1 : 0);
                    return true;
                case 99:
                {
                    ref int mode = ref State.Bank[EventState.BankMode];
                    if (p1 == -1) { mode &= unchecked((int)0xFF7FFFF7); return true; }
                    if (p1 == 1 || p1 == 2)
                    {
                        mode |= 0x800000;
                        host.Engine(99, p1, p2, p3, p4, begin, State);
                        return true;
                    }
                    return (mode & 8) == 0;
                }
                case 100:
                {
                    int key = p3 != 0 ? Var(p1) : p1;
                    for (int i = 0; i < EventState.PairCount; i++)
                    {
                        if (State.Pairs[i, 0] == -1 || State.Pairs[i, 0] == key)
                        {
                            State.Pairs[i, 0] = key; State.Pairs[i, 1] = p2;
                            return true;
                        }
                    }
                    return false;
                }
                case 104: host.OffsetActorPos(p1, p2 * 2, p3 * 2, p4 * 2); return true;
                case 105:
                {
                    int v = p2 != 0 ? Var(p1) : p1;
                    if (v < 0xB) return true;
                    if (v == 0xB) return State.GetFlag(0x129) != 1 && (host.EngineValue(EngineId.TownFlag) & 0xFF) == 0xFF;
                    if (v != 0x14) return true;
                    return State.GetFlag(0x25) != 1 || host.EngineValue(EngineId.Mode2) == 1;
                }
                default: return Unimpl(c);
            }
        }

        // ---- decoded game-logic ops ----

        /// <summary>Op 10: leave scripted camera/control mode.</summary>
        void ScriptRelease(int release, int keepBit)
        {
            ref int mode = ref State.Bank[EventState.BankMode];
            if (release == 0) mode |= 0x800000;
            else
            {
                mode &= unchecked((int)0xFF7FFBFF);
                int pm = host.EngineValue(EngineId.PlayerMode);
                if (pm == 1 || pm == 6) State.Bank[EventState.BankPlayer] &= ~8;
                else State.Bank[EventState.BankPlayer] |= 8;
            }
            if (keepBit == 0) mode &= ~2;
        }

        /// <summary>Op 11: consume the pending sleep trigger (Pending, flag 0x46).</summary>
        bool SleepTrigger(int outVar, int a, int b, int c4)
        {
            int pend = State.Pending, r = 0;
            if (pend != -1)
            {
                if (outVar != -1) State.SetVar(outVar, pend);
                r = c4 != 0 ? State.GetFlag(0x46) : 1;
                if (r == 1)
                {
                    // a != 0: start sleep effect (a - 1); b != 0: play jingle 0x2C for the pending id.
                    if (a != 0 || b != 0) host.Engine(11, pend, a != 0 ? a - 1 : -1, b, 0, true, State);
                }
                if (c4 == 0 || r == 1) { State.Pending = -1; State.SetFlag(0x46, 0); }
            }
            ScriptRelease(r == 0 ? 1 : 0, a);
            return r == 1;
        }

        /// <summary>Op 81: the big switch. Cases that only touch engine words are native, the rest go to the host.</summary>
        bool BigSwitch(int cs, int sub, int a, int b, bool begin)
        {
            var bk = State.Bank;
            switch (cs)
            {
                case 6:
                    if (a != 0) return (bk[EventState.BankScratch] & sub) != 0;
                    bk[EventState.BankScratch] |= sub;
                    return true;
                case 7:
                case 9: State.SetFlag(0x66, 1); return true;
                case 0xB: bk[EventState.BankEngine] |= 2; return true;
                case 0x12:
                    if (b != 0) sub = Var(sub);
                    if (a != 0) { bk[EventState.BankToken] = sub; return false; }
                    return bk[EventState.BankToken] == (sub & 0xFFFF);
                case 0x13:
                    if (sub == 0) return State.GetFlag(8) != 1;
                    State.SetFlag(8, 1);
                    return true;
                case 0x16: bk[EventState.BankCounter] = Math.Min(bk[EventState.BankCounter] + 1, 15); return true;
                case 0x17: bk[EventState.BankEngine] |= 0x2000000; return true;
                case 0x19: bk[EventState.BankEngine] |= 0x200000; return true;
                case 0x1B: bk[EventState.BankEngine2] |= 0x800000; return true;
                default: return host.Engine(81, cs, sub, a, b, begin, State);
            }
        }
    }
}

namespace HM.Events
{
    /// <summary>
    /// World effects the script VM asks for. Methods that return bool report "finished" and are polled
    /// once per tick until they do; begin is true on the first poll of a given command.
    /// All methods have no-op defaults in <see cref="EventHostBase"/>.
    /// </summary>
    public interface IEventHost
    {
        // Game fields: gold, clock, map, player position, per-actor fields (kind/index as in script.py).
        int GetField(int kind, int index);
        void SetField(int kind, int index, int value);
        long TotalMinutes();

        // Dialogue: message ids only (channel 0-4, entry). Poll returns true when closed; result is the choice index.
        void DialogBegin(int channel, int entry);
        bool DialogPoll(out int result);

        // Actors (player = 0x29, all npcs = 0x2A).
        bool MoveActor(int actor, int dir, int target, int speed, int anim, bool begin);
        void SetActorPos(int actor, int y, int x, int z);
        void GetActorCell(int actor, out int x, out int z);
        bool PlayAnim(int actor, int anim, int param, bool immediate, bool begin);
        void SetActorVisible(int actor, bool visible);
        int GetActorMap(int actor);
        void SetActorMap(int actor, int map, bool reset);
        void FacePlayer(int actor);
        void FaceActor(int actor, int target);
        void SetFace(int actor, int face);
        void ResetActor(int actor);
        void InitActor(int actor);
        void ClearActorFlags(int actor, int mode);
        void ActorEffect(int actor, int effect);
        void SetPlayerPos(int mode, int y, int x, int z);

        // Camera, screen and audio.
        bool CameraMove(int speedSel, int y, int x, int z, bool begin);
        void CameraFollow(int actor, bool moveCamera, int speed);
        bool TransitionDone();
        bool ScreenFade(int variant, int duration, int speed, bool begin);
        void ScreenTint(int r, int g, int b);
        void Bgm(int track, bool alsoSfx, int mode);
        bool BgmFade(int frames, int phase, bool begin);
        void Sfx(int id);

        // Items and UI.
        int ItemCount(int item, int bag);
        void RemoveItem(int item, int bag, int count);
        void GiveItem(int item, bool notify);
        void StashInventory(bool save);
        void PrintNumber(int slot, int value);
        void SetInputLock(bool locked);

        // Randomness is host-owned so runs are reproducible from a seed.
        int Random(int range);

        // Script ended (op 37) with its variant, flag and cleanup bits.
        void ScriptEnded(int variant, int flag, int cleanupBits);

        // Engine queries and hooks for ops decoded from the game logic (see docs/formats/event-vm.md).
        // GetActorPos: false when the actor is unknown. OffsetActorPos is op 104 (already doubled).
        bool GetActorPos(int actor, out int y, out int x, out int z);
        void OffsetActorPos(int actor, int dy, int dx, int dz);
        int DirectionBetween(int fromActor, int toActor);       // 0 +z, 1 +x, 2 -z, 3 -x (as op 0)
        int ItemPreference(int item, int npc);                  // op 57
        int AnimalState(int id);                                // op 40 (0 none, 1 .. 3)
        /// <summary>Engine values by id, see <see cref="EngineId"/>.</summary>
        int EngineValue(int id);
        /// <summary>
        /// Engine-internal ops the VM cannot model (8, 25, 40 start, 50, 62, 63, 72, 77, 80, 82, 83, 99 start
        /// and unmodelled op 81 cases). Return true when done, false to be polled again (begin = first poll).
        /// </summary>
        bool Engine(int op, int p1, int p2, int p3, int p4, bool begin, EventState state);

        // Diagnostics: unimplemented or unknown opcode reached.
        void Unimplemented(int op, string name, string confidence);
    }

    /// <summary>Ids for <see cref="IEventHost.EngineValue"/>.</summary>
    public static class EngineId
    {
        public const int TownFlag = 0;       // 0x1b832f byte; 0xFF = none
        public const int Mode2 = 1;          // 0x1ae1a2
        public const int PlayerMode = 2;     // 0x1aefae (1 or 6 = special)
        public const int GameMode = 3;       // 0x1ddbb0 (3 = event mode, 0x18 = busy)
        public const int EventY = 4;         // 0x1ddac6, position of the current event actor
        public const int EventX = 5;         // 0x1ddac4
        public const int EventZ = 6;         // 0x1ddac8
        public const int HeldItem = 7;       // 0x1aef3a
        public const int HeldItem2 = 8;      // 0x1aef34
        public const int HeldAlt = 9;        // 0x1aef3c
        public const int HeldAlt2 = 10;      // 0x1aef36
        public const int CurrentState = 11;  // FUN_000066e0 (op 59)
    }
}

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

        // Diagnostics: unimplemented or unknown opcode reached.
        void Unimplemented(int op, string name, string confidence);
    }
}

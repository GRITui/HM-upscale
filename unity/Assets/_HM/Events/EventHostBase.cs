namespace HM.Events
{
    /// <summary>No-op host. Waiting operations complete immediately. Derive and override what you need.</summary>
    public class EventHostBase : IEventHost
    {
        public virtual int GetField(int kind, int index) => 0;
        public virtual void SetField(int kind, int index, int value) { }
        public virtual long TotalMinutes() => 0;
        public virtual void DialogBegin(int channel, int entry) { }
        public virtual bool DialogPoll(out int result) { result = 0; return true; }
        public virtual bool MoveActor(int actor, int dir, int target, int speed, int anim, bool begin) => true;
        public virtual void SetActorPos(int actor, int y, int x, int z) { }
        public virtual void GetActorCell(int actor, out int x, out int z) { x = 0; z = 0; }
        public virtual bool PlayAnim(int actor, int anim, int param, bool immediate, bool begin) => true;
        public virtual void SetActorVisible(int actor, bool visible) { }
        public virtual int GetActorMap(int actor) => 0;
        public virtual void SetActorMap(int actor, int map, bool reset) { }
        public virtual void FacePlayer(int actor) { }
        public virtual void FaceActor(int actor, int target) { }
        public virtual void SetFace(int actor, int face) { }
        public virtual void ResetActor(int actor) { }
        public virtual void InitActor(int actor) { }
        public virtual void ClearActorFlags(int actor, int mode) { }
        public virtual void ActorEffect(int actor, int effect) { }
        public virtual void SetPlayerPos(int mode, int y, int x, int z) { }
        public virtual bool CameraMove(int speedSel, int y, int x, int z, bool begin) => true;
        public virtual void CameraFollow(int actor, bool moveCamera, int speed) { }
        public virtual bool TransitionDone() => true;
        public virtual bool ScreenFade(int variant, int duration, int speed, bool begin) => true;
        public virtual void ScreenTint(int r, int g, int b) { }
        public virtual void Bgm(int track, bool alsoSfx, int mode) { }
        public virtual bool BgmFade(int frames, int phase, bool begin) => true;
        public virtual void Sfx(int id) { }
        public virtual int ItemCount(int item, int bag) => 0;
        public virtual void RemoveItem(int item, int bag, int count) { }
        public virtual void GiveItem(int item, bool notify) { }
        public virtual void StashInventory(bool save) { }
        public virtual void PrintNumber(int slot, int value) { }
        public virtual void SetInputLock(bool locked) { }
        public virtual int Random(int range) => 0;
        public virtual void ScriptEnded(int variant, int flag, int cleanupBits) { }
        public virtual bool GetActorPos(int actor, out int y, out int x, out int z) { y = 0; x = 0; z = 0; return false; }
        public virtual void OffsetActorPos(int actor, int dy, int dx, int dz) { }
        public virtual int DirectionBetween(int fromActor, int toActor) => 0;
        public virtual int ItemPreference(int item, int npc) => 0;
        public virtual int AnimalState(int id) => 0;
        public virtual int EngineValue(int id) => 0;
        public virtual bool Engine(int op, int p1, int p2, int p3, int p4, bool begin, EventState state) => true;
        public virtual void Unimplemented(int op, string name, string confidence) { }
    }
}

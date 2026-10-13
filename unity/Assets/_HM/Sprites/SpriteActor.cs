using UnityEngine;

namespace HM.Sprites
{
    /// <summary>
    /// Camera-aligned billboard playing a sheet's anim table. In player mode it derives facing and idle/walk from the
    /// parent's motion; anim 0 holds one idle pose per facing and anims 1..4 are the walk cycles in the same facing order.
    /// </summary>
    public sealed class SpriteActor : MonoBehaviour
    {
        public enum Mode { Loop, Directional }

        public Mode mode = Mode.Loop;
        public int loopAnim;
        /// <summary>Facing to anim-index, and per-facing mirror flags. Defaults are observed, not confirmed (see ADR notes).</summary>
        public int[] walkAnimForFacing = { 1, 2, 3, 4 };
        public bool[] flipForFacing = { false, false, false, false };
        public float moveThreshold = 0.2f;

        LoadedSheet sheet;
        SpriteAnimPlayer player;
        SpriteRenderer sr;
        Transform followed;
        Vector3 last;
        Facing facing = Facing.Down;
        public Facing CurrentFacing => facing;
        public bool Moving { get; private set; }

        public static SpriteActor Create(string name, LoadedSheet sheet, Transform parent, Vector3 localPos, Mode mode, int loopAnim = 0)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var a = go.AddComponent<SpriteActor>();
            a.Init(sheet, mode, loopAnim);
            return a;
        }

        public void Init(LoadedSheet s, Mode m, int anim)
        {
            sheet = s; mode = m; loopAnim = anim;
            player = new SpriteAnimPlayer(s.Data);
            sr = gameObject.GetComponent<SpriteRenderer>();
            if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            followed = transform.parent;
            last = followed != null ? followed.position : transform.position;
            if (mode == Mode.Loop) player.Play(loopAnim); else PlayDirectional(false);
            Apply();
        }

        void PlayDirectional(bool moving)
        {
            if (moving) { if (!player.Play(walkAnimForFacing[(int)facing])) player.Play(0); }
            else player.Play(0, restart: true);
        }

        void LateUpdate()
        {
            if (sheet == null) return;
            if (mode == Mode.Directional)
            {
                var p = followed.position;
                var d = p - last; d.y = 0f; last = p;
                float dt = Mathf.Max(Time.deltaTime, 1e-5f);
                bool moving = d.magnitude / dt > moveThreshold;
                if (moving) facing = FacingFromScreenDelta(ScreenDelta(d));
                if (moving != Moving || (moving && player.AnimIndex != walkAnimForFacing[(int)facing])) { Moving = moving; PlayDirectional(moving); }
            }
            player.Advance(Time.deltaTime);
            Apply();
            // Full camera-aligned billboard: with a steep follow camera a yaw-only quad would foreshorten to a sliver.
            var cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }

        static Vector2 ScreenDelta(Vector3 worldDelta)
        {
            var cam = Camera.main;
            if (cam == null) return new Vector2(worldDelta.x, worldDelta.z);
            var t = cam.transform;
            var r = t.right; r.y = 0f; var f = t.forward; f.y = 0f;
            return new Vector2(Vector3.Dot(worldDelta, r.normalized), Vector3.Dot(worldDelta, f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward));
        }

        /// <summary>Screen-space movement to a facing: away from camera (+y) is Up, toward is Down.</summary>
        public static Facing FacingFromScreenDelta(Vector2 d)
        {
            if (Mathf.Abs(d.x) > Mathf.Abs(d.y)) return d.x < 0f ? Facing.Left : Facing.Right;
            return d.y > 0f ? Facing.Up : Facing.Down;
        }

        void Apply()
        {
            var f = player.Current;
            var spr = sheet.SpriteFor(f);
            if (mode == Mode.Directional && !Moving)
            {
                // Idle: anim 0 holds one pose per facing.
                var idle = sheet.Data.anims.Length > 0 ? sheet.Data.anims[0].frames : null;
                if (idle != null && (int)facing < idle.Length) spr = sheet.SpriteFor(sheet.Data.Frame(idle[(int)facing]));
            }
            sr.sprite = spr;
            sr.flipX = mode == Mode.Directional && flipForFacing[(int)facing];
        }
    }
}

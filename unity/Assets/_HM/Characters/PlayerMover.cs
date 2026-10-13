using HM.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HM.Characters
{
    /// <summary>
    /// CharacterController mover driven by the Input System. Like the original (FUN_00047754) it moves along the
    /// absolute raw axes, one axis at a time, in four facings only: up = +z, right = +x, down = -z, left = -x in the
    /// raw frame, converted through <see cref="FarmCoords"/>. It is not camera-relative and has no diagonals.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMover : MonoBehaviour
    {
        // Static decompile of hmpsp.prx (docs/baseline.md): walk is 41 raw units per 30 Hz update = 1230 units/s,
        // run is 73 per update = 2190 units/s (run button Circle, emulator-verified). At the decompiled estimate 0.005 m/unit (confidence medium) that is 6.15 and 10.95 m/s. The
        // farm is built FarmCoords.WorldScale (1.5x) larger by design, but the pace stays at these metre values, so
        // crossing takes 1.5x longer. Do not multiply by WorldScale here.
        [SerializeField] float walkSpeed = FarmCoords.DecompiledMetresPerSecond;
        [SerializeField] float runSpeed = FarmCoords.DecompiledRunMetresPerSecond;
        [SerializeField] float turnSharpness = 14f;
        [SerializeField] float gravity = -20f;

        public const string RunGamepadBinding = "<Gamepad>/buttonEast";
        public const string RunKeyboardBinding = "<Keyboard>/leftShift";

        CharacterController controller;
        InputAction move;
        InputAction run;
        float verticalVelocity;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            controller.minMoveDistance = 0f; // default 0.001 m swallows small per-frame moves at very high frame rates
            move = new InputAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/dpad");
            move.AddBinding("<Gamepad>/leftStick");
            run = new InputAction("Run", InputActionType.Button);
            // Run is Circle on the original pad (bit 0x20, docs/baseline.md); on an Xbox-style pad that is buttonEast.
            run.AddBinding(RunGamepadBinding);
            run.AddBinding(RunKeyboardBinding);
        }

        void OnEnable() { move.Enable(); run.Enable(); }
        void OnDisable() { move.Disable(); run.Disable(); }

        void OnDestroy() { move?.Dispose(); run?.Dispose(); }

        void Update()
        {
            Vector3 dir = FacingToWorld(Quantise(move.ReadValue<Vector2>()));
            float speed = run.IsPressed() ? runSpeed : walkSpeed;

            verticalVelocity = controller.isGrounded ? -1f : verticalVelocity + gravity * Time.deltaTime;
            Vector3 velocity = dir * speed + Vector3.up * verticalVelocity;
            controller.Move(velocity * Time.deltaTime);

            if (dir.sqrMagnitude > 0.0001f)
            {
                var target = Quaternion.LookRotation(dir, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-turnSharpness * Time.deltaTime));
            }
        }

        /// <summary>Stick magnitude on the dominant axis below which no direction registers.</summary>
        public const float Deadzone = 0.5f;

        /// <summary>
        /// Quantises a pad/keyboard vector (x right, y up) to one of the original's four facings:
        /// 0 = +z (up), 1 = +x (right), 2 = -z (down), 3 = -x (left), or -1 for none. The dominant axis wins;
        /// a tie goes to the vertical axis. The tie rule and the stick deadzone are choices, not decoded values.
        /// </summary>
        public static int Quantise(Vector2 input)
        {
            float ax = Mathf.Abs(input.x), ay = Mathf.Abs(input.y);
            if (Mathf.Max(ax, ay) < Deadzone) return -1;
            if (ay >= ax) return input.y > 0f ? 0 : 2;
            return input.x > 0f ? 1 : 3;
        }

        /// <summary>Raw (x, z) step direction of a facing, the movement-table order of the original.</summary>
        public static Vector2 FacingToRaw(int facing)
        {
            switch (facing)
            {
                case 0: return new Vector2(0f, 1f);
                case 1: return new Vector2(1f, 0f);
                case 2: return new Vector2(0f, -1f);
                case 3: return new Vector2(-1f, 0f);
                default: return Vector2.zero;
            }
        }

        /// <summary>World-space unit direction of a facing, converted once through <see cref="FarmCoords"/>.</summary>
        public static Vector3 FacingToWorld(int facing)
        {
            Vector2 r = FacingToRaw(facing);
            if (r == Vector2.zero) return Vector3.zero;
            Vector3 w = FarmCoords.ToWorld(r.x, 0f, r.y);
            w.y = 0f;
            return w.normalized;
        }
    }
}

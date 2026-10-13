using UnityEngine;
using UnityEngine.InputSystem;

namespace HM.Characters
{
    /// <summary>CharacterController mover driven by the Input System. Moves relative to the camera.</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMover : MonoBehaviour
    {
        // Measured in the original (docs/baseline.md): 20.5 raw units per frame at 60 fps = 1230 units/s
        // = 12.3 m/s at FarmCoords.UnitsToMetres 0.01 (source: PPSSPP RAM trace, confidence high for the raw
        // speed, low for the metre value because the unit scale is unproven). Run is the walk speed times the
        // script speed ratio 20/12 (source: event VM speed codes, confidence low: no run button was observed).
        [SerializeField] float walkSpeed = 12.3f;
        [SerializeField] float runSpeed = 20.5f;
        [SerializeField] float turnSharpness = 14f;
        [SerializeField] float gravity = -20f;

        CharacterController controller;
        InputAction move;
        InputAction run;
        Transform cam;
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
            move.AddBinding("<Gamepad>/leftStick");
            run = new InputAction("Run", InputActionType.Button);
            run.AddBinding("<Keyboard>/leftShift");
            run.AddBinding("<Gamepad>/buttonSouth");
        }

        void OnEnable() { move.Enable(); run.Enable(); }
        void OnDisable() { move.Disable(); run.Disable(); }

        void OnDestroy() { move?.Dispose(); run?.Dispose(); }

        void Update()
        {
            if (cam == null && Camera.main != null) cam = Camera.main.transform;
            Vector2 input = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
            Vector3 dir = ToWorldDirection(input, cam != null ? cam.eulerAngles.y : 0f);
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

        /// <summary>Converts a 2D stick vector to a world direction relative to a camera yaw in degrees.</summary>
        public static Vector3 ToWorldDirection(Vector2 input, float cameraYawDegrees)
        {
            var yaw = Quaternion.Euler(0f, cameraYawDegrees, 0f);
            return yaw * new Vector3(input.x, 0f, input.y);
        }
    }
}

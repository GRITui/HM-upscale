using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;

namespace HM.CameraRig
{
    /// <summary>
    /// Fixed-angle follow camera using the emulator-measured offset (docs/baseline.md). The camera never rotates
    /// and never lags: it holds a constant world-space offset from the target.
    /// </summary>
    public static class FarmCamera
    {
        /// <summary>Raw projection constant H of the PSP engine (docs/baseline.md): focal length in screen pixels. MEASURED.</summary>
        public const float RawProjectionH = 1000f;
        /// <summary>Native screen height in pixels (480x272). MEASURED.</summary>
        public const float ScreenHeight = 272f;
        /// <summary>
        /// Vertical field of view in degrees, DERIVED (not measured): the lens that reproduces H=1000 on a 272 high
        /// screen, 2*atan((272/2)/1000) = 15.49 deg. Used with the centre (160,120) approximated as the screen centre.
        /// </summary>
        public static readonly float DerivedVerticalFov = 2f * Mathf.Atan(ScreenHeight * 0.5f / RawProjectionH) * Mathf.Rad2Deg;

        /// <summary>Measured fixed offset from the player (feet) to the camera, in world metres. See FarmCoords.RawCameraOffset.</summary>
        public static Vector3 MeasuredOffset => HM.World.FarmCoords.CameraOffset;
        /// <summary>Rotation looking from the measured offset back at the player: yaw 315 (-45), pitch 26.57 deg down.</summary>
        public static Quaternion MeasuredRotation => Quaternion.LookRotation(-MeasuredOffset, Vector3.up);

        /// <summary>World-space camera offset from the target for a pitch/yaw/distance orbit position.</summary>
        public static Vector3 Offset(float pitchDeg, float yawDeg, float distance)
        {
            var rot = Quaternion.Euler(pitchDeg, yawDeg, 0f);
            return rot * new Vector3(0f, 0f, -distance);
        }

        /// <summary>Camera rotation that looks along the orbit direction (towards the target).</summary>
        public static Quaternion Rotation(float pitchDeg, float yawDeg) => Quaternion.Euler(pitchDeg, yawDeg, 0f);

        /// <summary>
        /// Distance at which a ground circle of the given radius fits the vertical field of view
        /// (pitch tilts the view, so the ground footprint is stretched by 1/sin(pitch)).
        /// </summary>
        public static float FitDistance(float groundRadius, float verticalFovDeg, float pitchDeg)
        {
            float halfFov = Mathf.Deg2Rad * verticalFovDeg * 0.5f;
            float s = Mathf.Max(0.2f, Mathf.Sin(Mathf.Deg2Rad * pitchDeg));
            return groundRadius * s / Mathf.Tan(halfFov);
        }

        /// <summary>
        /// Applies the measured emulator camera to a CinemachineCamera: fixed world-space offset from the player, no
        /// damping (the original is a fixed POV with no lag), rotation looking at the player, derived FOV. Adds the
        /// components it needs.
        /// </summary>
        public static void Configure(CinemachineCamera cam, Transform target)
        {
            cam.Follow = target;
            cam.LookAt = null; // fixed rotation: no aim component, so nothing rotates the camera
            cam.Lens.FieldOfView = DerivedVerticalFov;
            cam.Lens.NearClipPlane = 1f;
            cam.Lens.FarClipPlane = 500f;
            cam.transform.rotation = MeasuredRotation;

            var follow = cam.GetComponent<CinemachineFollow>();
            if (follow == null) follow = cam.gameObject.AddComponent<CinemachineFollow>();
            follow.FollowOffset = MeasuredOffset;
            follow.TrackerSettings.BindingMode = BindingMode.WorldSpace;
            follow.TrackerSettings.PositionDamping = Vector3.zero;
            follow.TrackerSettings.AngularDampingMode = AngularDampingMode.Euler;
            follow.TrackerSettings.RotationDamping = Vector3.zero;
        }

        /// <summary>Keeps the camera inside an area's bounds. The collider must be a trigger-free volume spanning the area.</summary>
        public static CinemachineConfiner3D Confine(CinemachineCamera cam, Collider bounds, float damping = 0.5f)
        {
            var c = cam.GetComponent<CinemachineConfiner3D>();
            if (c == null) c = cam.gameObject.AddComponent<CinemachineConfiner3D>();
            c.BoundingVolume = bounds;
            c.SlowingDistance = damping;
            return c;
        }
    }
}

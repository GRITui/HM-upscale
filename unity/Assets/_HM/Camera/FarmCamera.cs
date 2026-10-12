using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;

namespace HM.CameraRig
{
    /// <summary>
    /// Fixed-angle 3/4 follow camera for a farm-sized area (roughly 60 to 120 m across).
    /// The camera never rotates: it holds a constant world-space offset from the target at a
    /// downward pitch and a diagonal yaw, so movement relative to the camera stays predictable.
    /// </summary>
    public static class FarmCamera
    {
        public const float DefaultPitch = 50f;      // degrees down from horizontal
        public const float DefaultYaw = 45f;        // degrees around Y; 45 shows two faces of a building
        public const float DefaultDistance = 20f;   // metres from target to camera
        public const float DefaultFov = 35f;        // narrow lens flattens perspective, reads as 3/4 view
        public static readonly Vector3 TargetHeight = new Vector3(0f, 1f, 0f);

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

        /// <summary>Configures a CinemachineCamera as the 3/4 follow rig. Adds the components it needs.</summary>
        public static void Configure(CinemachineCamera cam, Transform target,
            float pitch = DefaultPitch, float yaw = DefaultYaw, float distance = DefaultDistance, float fov = DefaultFov)
        {
            cam.Follow = target;
            cam.LookAt = null; // fixed rotation: no aim component, so nothing rotates the camera
            cam.Lens.FieldOfView = fov;
            cam.Lens.NearClipPlane = 0.3f;
            cam.Lens.FarClipPlane = 300f;
            cam.transform.rotation = Rotation(pitch, yaw);

            var follow = cam.GetComponent<CinemachineFollow>();
            if (follow == null) follow = cam.gameObject.AddComponent<CinemachineFollow>();
            follow.FollowOffset = Offset(pitch, yaw, distance) + TargetHeight;
            follow.TrackerSettings.BindingMode = BindingMode.WorldSpace;
            follow.TrackerSettings.PositionDamping = new Vector3(0.6f, 0.9f, 0.6f);
            follow.TrackerSettings.AngularDampingMode = AngularDampingMode.Euler;
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

using UnityEngine;

namespace HM.World
{
    /// <summary>
    /// The single place that maps converted raw PSX scene units to Unity metres. Scale 0.005 m per unit is an
    /// ESTIMATE (docs/baseline.md, Unit scale: anchored on the 48-unit collision probe radius, 100-unit cells and
    /// the sprite height; the engine has no metre unit, confidence medium). Player coordinates are the scene frame
    /// itself (static decompile, confidence high). X is mirrored so the scene matches the GLB as glTFast imports it (glTF is right handed, Unity is left handed, glTFast negates X;
    /// the converter writes glTF y = -y_raw, so Unity y = -y_raw * scale). Change a constant here and the collision
    /// grid and regions move together; nothing else in the loader knows about handedness.
    /// </summary>
    public static class FarmCoords
    {
        public const float UnitsToMetres = 0.005f;
        public const bool MirrorX = true;

        public static Vector3 ToWorld(float x, float y, float z) =>
            new Vector3((MirrorX ? -x : x) * UnitsToMetres, -y * UnitsToMetres, z * UnitsToMetres);

        public static Vector3 ToWorld(int[] raw, Vector3 fallback = default) =>
            raw != null && raw.Length >= 3 ? ToWorld(raw[0], raw[1], raw[2]) : fallback;
    }
}

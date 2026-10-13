using UnityEngine;

namespace HM.World
{
    /// <summary>
    /// The single place that maps converted raw PSX scene units to Unity metres.
    ///
    /// Two layers, both here and nowhere else:
    ///  - <see cref="DecompiledUnitsToMetres"/> 0.005 m per unit is the ESTIMATE from the decompile and baseline
    ///    (docs/baseline.md, Unit scale: anchored on the 48-unit collision probe radius, 100-unit cells and the sprite
    ///    height; the engine has no metre unit, confidence medium).
    ///  - <see cref="WorldScale"/> 1.5 is a DESIGN decision (docs/adr/0007-farm-overlay.md): the farm is built 1.5x
    ///    larger than the raw proportions so it reads as a place, not a 14 m yard. It is a deliberate departure from
    ///    the original, not a measurement. Set it to 1 to get the raw proportions back.
    /// <see cref="UnitsToMetres"/> is their product (0.0075) and is what every converter of raw coordinates uses.
    /// Player and actor speeds are NOT scaled by it: they stay at the metre values of the raw estimate
    /// (<see cref="DecompiledMetresPerSecond"/>), so crossing the larger farm takes WorldScale times longer.
    ///
    /// HANDEDNESS (the single conversion point). The raw frame is right handed: x right, y down, z into the screen,
    /// d-pad up = +z and right = +x (docs/baseline.md). Unity is left handed with x right, y up, z forward, so the
    /// conversion is Unity = (x, -y, z) * scale and nothing is mirrored: the original scene reads the same on screen.
    /// The GLB agrees because the converter writes glTF = (-x, -y, z) (tools/hmconv/scene_glb.py to_gltf, a proper
    /// rotation) and glTFast negates x on import, landing on the same (x, -y, z). <see cref="MirrorX"/> is false; it
    /// exists so a wrong GLB can never again be hidden by mirroring only the player. Everything that needs the sign
    /// uses <see cref="XSign"/> or <see cref="ToRaw"/>. Change a constant here and the collision grid, regions,
    /// spawns, GLB, sprites and camera move together; nothing else knows about scale or handedness.
    /// </summary>
    public static class FarmCoords
    {
        public const float DecompiledUnitsToMetres = 0.005f;
        public const float WorldScale = 1.5f;
        public const float UnitsToMetres = DecompiledUnitsToMetres * WorldScale;
        /// <summary>False: raw +x is world +x (see the class remarks). Do not set it to hide a converter/importer mirror.</summary>
        public static readonly bool MirrorX = false;
        /// <summary>World x per raw x: +1 unmirrored, -1 mirrored.</summary>
        public static float XSign => MirrorX ? -1f : 1f;

        /// <summary>Scale the converter bakes into the scene GLB (tools/hmconv/scene_glb.py SCALE).</summary>
        public const float GlbUnitsToMetres = 0.01f;
        /// <summary>Uniform scale applied to the imported GLB root so it lines up with the collision grid.</summary>
        public const float GlbScale = UnitsToMetres / GlbUnitsToMetres;

        /// <summary>Raw walk speed: 41 units per 30 Hz update (static decompile, confidence high).</summary>
        public const float WalkUnitsPerSecond = 41f * 30f;
        /// <summary>Raw run speed: 73 units per update (confidence medium, run button unverified).</summary>
        public const float RunUnitsPerSecond = 73f * 30f;
        /// <summary>Metres per second at the raw estimate scale, deliberately NOT multiplied by WorldScale.</summary>
        public const float DecompiledMetresPerSecond = WalkUnitsPerSecond * DecompiledUnitsToMetres; // 6.15
        public const float DecompiledRunMetresPerSecond = RunUnitsPerSecond * DecompiledUnitsToMetres; // 10.95

        public static Vector3 ToWorld(float x, float y, float z) =>
            new Vector3(XSign * x * UnitsToMetres, -y * UnitsToMetres, z * UnitsToMetres);

        /// <summary>Inverse of <see cref="ToWorld(float,float,float)"/> for the horizontal axes: world metres to raw units (x, z).</summary>
        public static void ToRaw(Vector3 world, out float x, out float z)
        {
            x = XSign * world.x / UnitsToMetres;
            z = world.z / UnitsToMetres;
        }

        public static Vector3 ToWorld(int[] raw, Vector3 fallback = default) =>
            raw != null && raw.Length >= 3 ? ToWorld(raw[0], raw[1], raw[2]) : fallback;
    }
}

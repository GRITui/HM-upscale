using System;
using System.IO;
using System.Threading.Tasks;
using GLTFast;
using HM.World;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HM.Gameplay
{
    /// <summary>
    /// Added to the SliceFarm scene at load. If converted farm output exists locally it replaces the hand-placed
    /// layout with the decoded walkable grid (collision), region triggers and the scene GLB (visuals). If not, it logs
    /// one clear line and leaves the placeholder slice untouched. Nothing here is committed data: the overlay lives in
    /// the git-ignored assets/hm-converted (or $HM_CONVERTED).
    /// </summary>
    public sealed class FarmOverlayBootstrap : MonoBehaviour
    {
        public const string SceneName = "SliceFarm";
        const string Tag = "[HM-FARM] ";
        static readonly string[] PlaceholderNames = { "Ground", "Farmhouse", "Fence", "Tree", "Rock", "Tuft" };
        static readonly string[] GameplayNames = { "Bed", "Mira", "ShippingBin" };

        public BuiltMap Built { get; private set; }
        public static bool Loaded { get; private set; }
        public static bool GlbLoaded { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            Loaded = false; GlbLoaded = false;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != SceneName) return;
            var go = new GameObject("FarmOverlay");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<FarmOverlayBootstrap>();
        }

        void Start() => _ = Run();

        async Task Run()
        {
            if (FarmOverlay.Disabled) { Debug.Log(Tag + "overlay disabled by " + FarmOverlay.EnvDisable + "=0; using the placeholder slice"); return; }
            var paths = FarmOverlay.Find();
            if (paths == null)
            {
                Debug.Log(Tag + "converted farm not found (looked for maps/map_080.json under $" + FarmOverlay.EnvRoot + " and the repo assets/hm-converted); using the placeholder slice");
                return;
            }
            MapData data;
            try { data = MapData.Parse(File.ReadAllText(paths.MapJson)); }
            catch (Exception e) { Debug.LogWarning(Tag + "could not read " + paths.MapJson + " (" + e.Message + "); using the placeholder slice"); return; }
            if (data.terrain == null || !data.terrain.IsRaw || WalkGrid.WalkableCellCount(data.terrain) == 0)
            {
                Debug.LogWarning(Tag + paths.MapJson + " has no decoded terrain (stale converter output? rerun tools/hmconv scene.py); using the placeholder slice");
                return;
            }

            Built = MapLoader.Build(data, null);
            Built.Root.transform.SetParent(transform, false);
            if (!Built.Spawns.TryGetValue("default", out var spawn)) return;

            var player = FindFirstObjectByType<HM.Characters.PlayerMover>();
            var offset = player != null ? spawn.position - player.transform.position : Vector3.zero;
            offset.y = 0f;
            HidePlaceholders(scene: gameObject.scene);
            ShiftGameplay(gameObject.scene, offset);
            if (player != null)
            {
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                player.transform.position = spawn.position;
                if (cc != null) cc.enabled = true;
            }
            Loaded = true;
            Debug.Log(Tag + "loaded " + data.id + ": " + Built.WalkableCells + " walkable cells, " + Built.RegionCount + " regions, spawn " + spawn.position
                + " (scale " + FarmCoords.UnitsToMetres + " m/unit, mirrorX " + FarmCoords.MirrorX + " are assumptions)");

            if (!paths.HasGlb) { Debug.Log(Tag + "no scene GLB found; collision only"); return; }
            try
            {
                var gltf = new GltfImport();
                bool ok = await gltf.LoadGltfBinary(File.ReadAllBytes(paths.Glb), new Uri(paths.Glb));
                if (!ok) { Debug.LogWarning(Tag + "glTFast failed to load " + paths.Glb); return; }
                var visuals = new GameObject("FarmVisuals");
                visuals.transform.SetParent(transform, false);
                await gltf.InstantiateMainSceneAsync(visuals.transform);
                // The GLB supplies the visible ground; the walk grid stays as invisible collision.
                foreach (var r in Built.Root.GetComponentsInChildren<MeshRenderer>()) r.enabled = false;
                GlbLoaded = true;
                Debug.Log(Tag + "scene GLB instantiated");
            }
            catch (Exception e) { Debug.LogWarning(Tag + "scene GLB failed: " + e.Message); }
        }

        static void HidePlaceholders(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (Array.IndexOf(PlaceholderNames, root.name) >= 0) root.SetActive(false);
        }

        static void ShiftGameplay(Scene scene, Vector3 offset)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (Array.IndexOf(GameplayNames, root.name) >= 0) root.transform.position += offset;
                var fw = root.GetComponent<FarmWorld>();
                if (fw != null) fw.origin += offset;
            }
        }
    }
}

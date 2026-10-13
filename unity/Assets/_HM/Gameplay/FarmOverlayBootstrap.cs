using System;
using System.IO;
using System.Threading.Tasks;
using GLTFast;
using HM.Core;
using HM.Sprites;
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
            Loaded = false; GlbLoaded = false; // per scene load, not just per play session
            var go = new GameObject("FarmOverlay");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<FarmOverlayBootstrap>();
        }

        /// <summary>Row currently loaded (yard 0..7, interiors 80+).</summary>
        public int CurrentRow { get; private set; } = -1;
        public static FarmOverlayBootstrap Instance { get; private set; }
        bool switching, firstDone;
        float lastWarp = -10f;
        GameObject visuals;
        const float WarpCooldown = 0.75f;
        /// <summary>Door region: flags 0x9000 (36864), type = target map id through FarmOverlay.DoorTargetRow.</summary>
        public const int DoorFlags = 0x9000;

        void OnEnable() => MapRegionTrigger.Entered += OnRegionEntered;
        void OnDisable() { MapRegionTrigger.Entered -= OnRegionEntered; if (Instance == this) Instance = null; }

        void Start() => _ = Run();

        static string Arg(string name)
        {
            var a = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }

        async Task Run()
        {
            Instance = this;
            if (FarmOverlay.Disabled) { Debug.Log(Tag + "overlay disabled by " + FarmOverlay.EnvDisable + "=0; using the placeholder slice"); return; }
            // Season and building flag choose the row in one place (FarmOverlay.YardRow). Flags override the clock for captures.
            var clock = GameClockBehaviour.Instance;
            if (clock != null) FarmOverlay.State.Season = (int)clock.Clock.Date.Season;
            if (int.TryParse(Arg("-hm-farm-season"), out int ss)) FarmOverlay.State.Season = Mathf.Clamp(ss, 0, 3);
            if (int.TryParse(Arg("-hm-farm-building"), out int bf)) FarmOverlay.State.BuildingFlag = Mathf.Clamp(bf, 0, 1);
            Vector3? place = null;
            if (FarmOverlay.TryParseRawPos(Arg("-hm-shot-pos"), out float px, out float pz)) place = FarmCoords.ToWorld(px, 0f, pz);
            int row = FarmOverlay.YardRow;
            if (int.TryParse(Arg("-hm-farm-row"), out int fr)) row = fr; // capture aid: load any exported row directly
            await LoadRow(row, place, true);
        }

        /// <summary>Loads a row, replacing the previous one. place is an absolute world position, or null for the map spawn.</summary>
        async Task<bool> LoadRow(int row, Vector3? place, bool first)
        {
            var paths = FarmOverlay.Find(row);
            if (paths == null)
            {
                Debug.Log(Tag + "converted farm not found (looked for maps/map_" + row.ToString("D3") + ".json under $" + FarmOverlay.EnvRoot + " and the repo assets/hm-converted); " + (first ? "using the placeholder slice" : "staying on the current map"));
                return false;
            }
            MapData data;
            try { data = MapData.Parse(File.ReadAllText(paths.MapJson)); }
            catch (Exception e) { Debug.LogWarning(Tag + "could not read " + paths.MapJson + " (" + e.Message + "); " + (first ? "using the placeholder slice" : "staying on the current map")); return false; }
            if (data.terrain == null || !data.terrain.IsRaw || WalkGrid.WalkableCellCount(data.terrain) == 0)
            {
                Debug.LogWarning(Tag + paths.MapJson + " has no decoded terrain (stale converter output? rerun tools/hmconv scene.py); " + (first ? "using the placeholder slice" : "staying on the current map"));
                return false;
            }

            if (Built != null) Destroy(Built.Root);
            if (visuals != null) Destroy(visuals);
            Built = MapLoader.Build(data, null);
            Built.Root.transform.SetParent(transform, false);
            CurrentRow = row;
            Vector3 spawnPos;
            if (place.HasValue) spawnPos = place.Value;
            else if (Built.Spawns.TryGetValue("default", out var spawn)) spawnPos = spawn.position;
            else return false;

            var player = FindFirstObjectByType<HM.Characters.PlayerMover>();
            if (first)
            {
                var offset = player != null ? spawnPos - player.transform.position : Vector3.zero;
                offset.y = 0f;
                HidePlaceholders(scene: gameObject.scene);
                ShiftGameplay(gameObject.scene, offset);
            }
            SetGameplayVisible(gameObject.scene, FarmOverlay.IsYardRow(row));
            if (player != null)
            {
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                player.transform.position = spawnPos;
                if (cc != null) cc.enabled = true;
            }
            Loaded = true;
            if (first)
            {
                ScaleCamera(gameObject.scene);
                try
                {
                    var dir = new GameObject("FarmEventDirector").AddComponent<FarmEventDirector>();
                    dir.transform.SetParent(transform, false);
                    dir.Init(player != null ? player.transform : null, FarmEventDirector.MapNumber(data.id));
                }
                catch (Exception e) { Debug.LogWarning(Tag + "event director failed: " + e.Message); }
                try { SpriteLayer.Spawn(player != null ? player.transform : null, spawnPos, transform); }
                catch (Exception e) { Debug.LogWarning(Tag + "sprite layer failed, keeping placeholders: " + e.Message); }
            }
            else FarmEventDirector.Instance?.SetMap(FarmEventDirector.MapNumber(data.id));
            firstDone = true;
            Debug.Log(Tag + "loaded " + data.id + ": " + Built.WalkableCells + " walkable cells, " + Built.RegionCount + " regions, spawn " + spawnPos
                + " (scale " + FarmCoords.UnitsToMetres + " m/unit = " + FarmCoords.DecompiledUnitsToMetres + " x world scale " + FarmCoords.WorldScale + ", mirrorX " + FarmCoords.MirrorX + " are assumptions)");

            if (!paths.HasGlb) { Debug.Log(Tag + "no scene GLB found; collision only"); return true; }
            try
            {
                var gltf = new GltfImport();
                bool ok = await gltf.LoadGltfBinary(File.ReadAllBytes(paths.Glb), new Uri(paths.Glb));
                if (this == null) { gltf.Dispose(); return true; } // scene unloaded while the GLB was loading
                if (!ok) { Debug.LogWarning(Tag + "glTFast failed to load " + paths.Glb); return true; }
                if (CurrentRow != row) { gltf.Dispose(); return true; } // another warp started meanwhile
                var v = new GameObject("FarmVisuals");
                v.transform.SetParent(transform, false);
                visuals = v;
                // The converter bakes 0.01 m/unit into the GLB; bring it to the same scale as the collision grid.
                v.transform.localScale = Vector3.one * FarmCoords.GlbScale;
                await gltf.InstantiateMainSceneAsync(v.transform);
                if (this == null) { gltf.Dispose(); return true; } // destroyed mid-instantiate; nothing left to configure
                // The GLB supplies the visible ground; the walk grid stays as invisible collision.
                if (CurrentRow == row)
                {
                    foreach (var r in Built.Root.GetComponentsInChildren<MeshRenderer>()) r.enabled = false;
                    GlbLoaded = true;
                    Debug.Log(Tag + "scene GLB instantiated (" + System.IO.Path.GetFileName(paths.Glb) + ")");
                }
            }
            catch (Exception e)
            {
                if (this == null) return true; // teardown race, not a failure
                Debug.LogWarning(Tag + "scene GLB failed: " + e.Message);
            }
            return true;
        }

        /// <summary>
        /// Walking into a door region (flags 0x9000, type = target map) loads that map and places the player at the region's
        /// arrival. In any non-yard map, region 0 (flags 0, type 0) is the exit: its arrival is the yard position, which is
        /// the same entry the converter stores. Neither the door condition nor the arrival facing is applied (documented gaps).
        /// </summary>
        void OnRegionEntered(MapRegionTrigger t, Collider who)
        {
            if (t == null || switching || Time.unscaledTime - lastWarp < WarpCooldown || !firstDone) return;
            int target;
            if (t.flags == DoorFlags && t.type > 0) target = FarmOverlay.DoorTargetRow(t.type, FarmOverlay.State);
            else if (!FarmOverlay.IsYardRow(CurrentRow) && t.index == 0 && t.flags == 0 && t.type == 0) target = FarmOverlay.YardRow;
            else return;
            if (t.arrival == Vector3.zero) return;
            _ = Warp(target, t.arrival);
        }

        /// <summary>Loads a row and puts the player on arrival (world metres, y kept). Public so tests and tools can drive it.</summary>
        public async Task<bool> Warp(int row, Vector3 arrival)
        {
            if (switching) return false;
            switching = true;
            try
            {
                var player = FindFirstObjectByType<HM.Characters.PlayerMover>();
                if (player != null) arrival.y = player.transform.position.y;
                bool ok = await LoadRow(row, arrival, false);
                lastWarp = Time.unscaledTime;
                if (ok) Debug.Log(Tag + "warp to row " + row);
                return ok;
            }
            finally { switching = false; }
        }

        static void SetGameplayVisible(Scene scene, bool on)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (Array.IndexOf(GameplayNames, root.name) >= 0) root.SetActive(on);
        }

        /// <summary>The scene stores a guessed camera; replace it with the measured one (offset, rotation, FOV, no lag).</summary>
        static void ScaleCamera(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var c in root.GetComponentsInChildren<Unity.Cinemachine.CinemachineCamera>(true))
                    HM.CameraRig.FarmCamera.Configure(c, c.Follow);
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

using System.IO;
using HM.Characters;
using HM.Core;
using HM.Gameplay;
using HM.UI;
using HM.World;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HM.Importers
{
    /// <summary>Idempotent project/scene generation. Run via HM/Setup or -executeMethod HM.Importers.ProjectSetup.Run.</summary>
    public static class ProjectSetup
    {
        const string SceneDir = "Assets/_HM/Scenes";
        const string SettingsDir = "Assets/_HM/Settings";

        [MenuItem("HM/Setup/Rebuild Project Settings and Scenes")]
        public static void Run()
        {
            ConfigurePlayer();
            ConfigureUrp();
            Directory.CreateDirectory(SceneDir);
            BuildSliceFarm();
            BuildBoot();
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(SceneDir + "/Boot.unity", true),
                new EditorBuildSettingsScene(SceneDir + "/SliceFarm.unity", true),
            };
            AssetDatabase.SaveAssets();
            Debug.Log("HM setup complete");
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.productName = "HM Upscale";
            // Apple Silicon (arm64) standalone target.
            UnityEditor.OSXStandalone.UserBuildSettings.architecture = UnityEditor.Build.OSArchitecture.ARM64;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
        }

        static void ConfigureUrp()
        {
            Directory.CreateDirectory(SettingsDir);
            string path = SettingsDir + "/HM_URP.asset";
            string rendererPath = SettingsDir + "/HM_URP_Renderer.asset";
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (asset != null && AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath) == null)
            {
                // An earlier run saved the pipeline asset without a renderer; rebuild both.
                AssetDatabase.DeleteAsset(path);
                asset = null;
            }
            if (asset == null)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, rendererPath);
                asset = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(asset, path);
                AssetDatabase.SaveAssets();
            }
            GraphicsSettings.defaultRenderPipeline = asset;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
                QualitySettings.vSyncCount = 0;
            }
            QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, false);
            EditorUtility.SetDirty(asset);
        }

        static void BuildBoot()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cam = new GameObject("Boot Camera", typeof(Camera));
            cam.GetComponent<Camera>().backgroundColor = Color.black;
            cam.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            new GameObject("GameClock", typeof(GameClockBehaviour));
            new GameObject("Audio", typeof(HM.Audio.AudioService), typeof(HM.Audio.AudioBootstrap));
            new GameObject("DevConsole", typeof(HM.Tools.DevConsole));
            new GameObject("Bootstrap", typeof(GameStartup));
            EditorSceneManager.SaveScene(scene, SceneDir + "/Boot.unity");
        }

        static void BuildSliceFarm()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Fallback clock so the scene also runs when opened directly (the Boot clock wins when present).
            new GameObject("GameClock", typeof(GameClockBehaviour));

            var sunGo = new GameObject("Sun");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sunGo.AddComponent<DayNightLight>();
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat;

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(10f, 1f, 10f);
            ground.GetComponent<Renderer>().sharedMaterial = FlatMaterial("Ground", new Color(0.36f, 0.55f, 0.28f));

            // Player: empty root carries the controller; the visual is a swappable child (glb or capsule).
            var player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 0f, -3f);
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = 0.35f; cc.center = new Vector3(0f, 0.9f, 0f);
            player.AddComponent<PlayerMover>();
            player.AddComponent<PlayerInteractor>();
            player.AddComponent<WalkBob>();
            Visual(player.transform, Model("player"), PrimitiveType.Capsule, "PlayerVisual", new Color(0.9f, 0.7f, 0.2f), new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f));

            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(CinemachineBrain));
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(-8f, 12f, -8f);

            var rigGo = new GameObject("Follow Camera", typeof(CinemachineCamera));
            HM.CameraRig.FarmCamera.Configure(rigGo.GetComponent<CinemachineCamera>(), player.transform);
            camGo.transform.rotation = HM.CameraRig.FarmCamera.MeasuredRotation;

            // Farm world: rules + dialogue data, plot grid and art slots.
            var worldGo = new GameObject("FarmWorld", typeof(FarmWorld));
            var world = worldGo.GetComponent<FarmWorld>();
            world.rulesJson = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_HM/Farming/Data/rules.json");
            var dialogue = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/_HM/Dialogue/Data/dialogue.json");
            world.fallbackMaterial = FlatMaterial("Flat", Color.white);
            world.untilledPrefab = Model("ground_tile");
            world.tilledPrefab = Model("soil_tilled");
            world.wateredPrefab = Model("soil_watered");
            world.crops = new[]
            {
                new CropSlot { cropId = "sprout_beet", stages = new[] { Model("turnip_0"), Model("turnip_1"), Model("turnip_2") }, fallbackColour = new Color(0.8f, 0.25f, 0.45f) },
                new CropSlot { cropId = "plain_corn", stages = new[] { Model("potato_0"), Model("potato_1"), Model("potato_2") }, fallbackColour = new Color(0.9f, 0.8f, 0.3f) },
            };
            world.toolPrefabs = new[] { Model("hoe"), Model("watering_can"), Model("seed_bag"), null };
            new GameObject("Sleep", typeof(SleepController));

            // House with a bed trigger by the door, an NPC and the shipping bin.
            Visual(null, Model("farmhouse"), PrimitiveType.Cube, "Farmhouse", new Color(0.7f, 0.6f, 0.5f), new Vector3(10f, 1.5f, -1f), new Vector3(4f, 3f, 4f));
            var bed = new GameObject("Bed", typeof(Bed));
            bed.transform.position = new Vector3(7.5f, 0f, -1f);
            bed.GetComponent<Bed>().radius = 2.5f;

            var npc = new GameObject("Mira", typeof(NpcDialogue));
            npc.transform.position = new Vector3(5f, 0f, 1.5f);
            npc.GetComponent<NpcDialogue>().dialogueJson = dialogue;
            Visual(npc.transform, Model("npc_villager"), PrimitiveType.Capsule, "NpcVisual", new Color(0.3f, 0.5f, 0.8f), new Vector3(0f, 0.9f, 0f), new Vector3(0.7f, 0.9f, 0.7f));
            npc.transform.rotation = Quaternion.Euler(0f, -135f, 0f);

            var bin = new GameObject("ShippingBin", typeof(ShippingBin));
            bin.transform.position = new Vector3(4f, 0f, -4f);
            Visual(bin.transform, null, PrimitiveType.Cube, "BinVisual", new Color(0.55f, 0.38f, 0.2f), new Vector3(0f, 0.4f, 0f), new Vector3(1.2f, 0.8f, 0.8f));

            // Scenery: fence around the plots, trees and rocks.
            var fence = Model("fence_segment");
            for (int i = -4; i <= 3; i++)
            {
                Visual(null, fence, PrimitiveType.Cube, "Fence", new Color(0.6f, 0.45f, 0.28f), new Vector3(i + 0.5f, 0f, 4.5f), new Vector3(1f, 0.5f, 0.1f));
                Visual(null, fence, PrimitiveType.Cube, "Fence", new Color(0.6f, 0.45f, 0.28f), new Vector3(i + 0.5f, 0f, -1.5f), new Vector3(1f, 0.5f, 0.1f));
            }
            var tree = Model("tree"); var rock = Model("rock"); var tuft = Model("grass_tuft");
            Vector3[] trees = { new Vector3(7f, 0, 8f), new Vector3(-9f, 0, -1f), new Vector3(12f, 0, -7f), new Vector3(2f, 0, 10f), new Vector3(-10f, 0, 6f) };
            foreach (var t in trees) Visual(null, tree, PrimitiveType.Cylinder, "Tree", new Color(0.2f, 0.45f, 0.2f), t + new Vector3(0, 1.5f, 0), new Vector3(1f, 1.5f, 1f));
            Vector3[] rocks = { new Vector3(7f, 0, -2f), new Vector3(-9f, 0, -5f), new Vector3(11f, 0, 7f) };
            foreach (var r in rocks) Visual(null, rock, PrimitiveType.Sphere, "Rock", new Color(0.5f, 0.5f, 0.5f), r + new Vector3(0, 0.3f, 0), new Vector3(0.8f, 0.6f, 0.8f));
            for (int i = 0; i < 24; i++)
            {
                float a = i * 2.4f; float rad = 6f + (i % 7) * 1.7f;
                var pos = new Vector3(Mathf.Cos(a) * rad, 0f, Mathf.Sin(a) * rad);
                if (tuft != null) Visual(null, tuft, PrimitiveType.Cube, "Tuft", Color.green, pos, Vector3.one);
            }

            var hudGo = new GameObject("Slice HUD", typeof(SliceHud));
            _ = hudGo;
            EditorSceneManager.SaveScene(scene, SceneDir + "/SliceFarm.unity");
        }

        static GameObject Model(string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_HM/Art/Models/" + name + ".glb");

        /// <summary>Instantiate the glb if present, else a flat-colour primitive. Primitive pos/scale only apply to the fallback.</summary>
        static GameObject Visual(Transform parent, GameObject model, PrimitiveType fallback, string name, Color colour, Vector3 pos, Vector3 scale)
        {
            GameObject go;
            if (model != null)
            {
                go = (GameObject)PrefabUtility.InstantiatePrefab(model);
                go.transform.position = parent != null ? parent.position : pos;
                if (parent == null) go.transform.position = new Vector3(pos.x, 0f, pos.z);
            }
            else
            {
                go = GameObject.CreatePrimitive(fallback);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.position = (parent != null ? parent.position : Vector3.zero) + pos;
                go.transform.localScale = scale;
                go.GetComponent<Renderer>().sharedMaterial = FlatMaterial(name, colour);
            }
            go.name = name;
            if (parent != null) go.transform.SetParent(parent, true);
            return go;
        }

        static Material FlatMaterial(string name, Color colour)
        {
            string path = $"{SettingsDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", colour);
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}

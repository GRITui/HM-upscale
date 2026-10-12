using System.IO;
using HM.Characters;
using HM.Core;
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

            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
            player.transform.position = new Vector3(0f, 1.1f, 0f);
            var cc = player.AddComponent<CharacterController>();
            cc.height = 2f; cc.radius = 0.5f;
            player.AddComponent<PlayerMover>();
            player.GetComponent<Renderer>().sharedMaterial = FlatMaterial("Player", new Color(0.9f, 0.7f, 0.2f));

            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(CinemachineBrain));
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0f, 3f, -6f);

            var rigGo = new GameObject("Follow Camera", typeof(CinemachineCamera));
            var rig = rigGo.GetComponent<CinemachineCamera>();
            rig.Follow = player.transform;
            rig.LookAt = player.transform;
            rig.Lens.FieldOfView = 50f;
            var tp = rigGo.AddComponent<CinemachineThirdPersonFollow>();
            tp.CameraDistance = 6f;
            tp.ShoulderOffset = new Vector3(0f, 0.5f, 0f);
            tp.VerticalArmLength = 0.4f;
            rigGo.AddComponent<CinemachineRotationComposer>();

            // A few flat-colour props so movement is readable.
            for (int i = 0; i < 6; i++)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "Crate " + i;
                cube.transform.position = new Vector3(-12f + i * 5f, 0.5f, 10f);
                cube.GetComponent<Renderer>().sharedMaterial = FlatMaterial("Crate", new Color(0.55f, 0.38f, 0.2f));
            }

            new GameObject("Clock HUD", typeof(ClockHud));
            EditorSceneManager.SaveScene(scene, SceneDir + "/SliceFarm.unity");
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

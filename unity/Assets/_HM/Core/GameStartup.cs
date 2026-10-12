using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HM.Core
{
    /// <summary>
    /// Boot scene entry: sets the 60 fps target and loads the first area additively. The Boot scene
    /// stays loaded for the whole session and owns persistent services (clock, audio, dev console).
    /// </summary>
    public sealed class GameStartup : MonoBehaviour
    {
        public const int TargetFrameRate = 60;
        [SerializeField] string firstScene = "SliceFarm";

        /// <summary>Shared area loader. Valid after Awake; reset when the Boot scene is destroyed.</summary>
        public static AreaSceneService Areas { get; private set; }

        void Awake()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFrameRate;
            // Diagnostic override (-hm-target-fps -1 = uncapped) to separate real frame cost from limiter overshoot.
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-hm-target-fps");
            if (i >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out int fps)) Application.targetFrameRate = fps;
            Areas = new AreaSceneService();
        }

        IEnumerator Start()
        {
            yield return Areas.Load(firstScene, true);
            // Compile every shader variant that survived stripping while the loading frames still cover
            // the screen, instead of paying for each pipeline state on its first visible draw.
            // -hm-no-prewarm disables this so the before/after hitch can be measured.
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-hm-no-prewarm") < 0)
            {
                Shader.WarmupAllShaders();
                yield return null;
            }
            // The Boot camera only covers the loading frames; the area brings its own camera.
            foreach (var go in gameObject.scene.GetRootGameObjects())
                foreach (var cam in go.GetComponentsInChildren<Camera>())
                    cam.enabled = false;
        }

        void OnDestroy() { Areas = null; }
    }
}

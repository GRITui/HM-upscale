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
            Areas = new AreaSceneService();
        }

        IEnumerator Start()
        {
            yield return Areas.Load(firstScene, true);
            // The Boot camera only covers the loading frames; the area brings its own camera.
            foreach (var go in gameObject.scene.GetRootGameObjects())
                foreach (var cam in go.GetComponentsInChildren<Camera>())
                    cam.enabled = false;
        }

        void OnDestroy() { Areas = null; }
    }
}

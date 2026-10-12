using UnityEngine;
using UnityEngine.SceneManagement;

namespace HM.Core
{
    /// <summary>Boot scene entry: sets the 60 fps target and loads the first gameplay scene.</summary>
    public sealed class GameStartup : MonoBehaviour
    {
        public const int TargetFrameRate = 60;
        [SerializeField] string firstScene = "SliceFarm";

        void Awake()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = TargetFrameRate;
        }

        void Start() => SceneManager.LoadScene(firstScene);
    }
}

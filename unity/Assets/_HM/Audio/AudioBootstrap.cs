using System.IO;
using UnityEngine;

namespace HM.Audio
{
    /// <summary>
    /// Boot-scene component that creates the AudioService from the converter manifest.
    /// Lookup order: HM_CONVERTED env var, StreamingAssets/hm-manifest.json, and in the editor
    /// the repo-local assets/hm-raw/converted/manifest.json. With no manifest the game runs silent.
    /// </summary>
    [RequireComponent(typeof(AudioService))]
    public sealed class AudioBootstrap : MonoBehaviour
    {
        public static AudioService Service { get; private set; }

        void Awake()
        {
            var svc = GetComponent<AudioService>();
            var path = FindManifest();
            if (path == null) { Debug.Log("AudioBootstrap: no manifest found, audio disabled"); return; }
            try
            {
                svc.Initialize(AudioCatalog.FromManifestFile(path), new AddressablesClipSource());
                Service = svc;
                DontDestroyOnLoad(gameObject);
            }
            catch (System.Exception e) { Debug.LogWarning("AudioBootstrap: " + e.Message); }
        }

        void OnDestroy() { if (Service == GetComponent<AudioService>()) Service = null; }

        static string FindManifest()
        {
            var env = System.Environment.GetEnvironmentVariable("HM_CONVERTED");
            if (!string.IsNullOrEmpty(env) && File.Exists(Path.Combine(env, "manifest.json"))) return Path.Combine(env, "manifest.json");
            var sa = Path.Combine(Application.streamingAssetsPath, "hm-manifest.json");
            if (File.Exists(sa)) return sa;
#if UNITY_EDITOR
            var repo = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "assets", "hm-raw", "converted", "manifest.json"));
            if (File.Exists(repo)) return repo;
#endif
            return null;
        }
    }
}

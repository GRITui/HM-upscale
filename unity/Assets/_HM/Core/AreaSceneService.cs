using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HM.Core
{
    /// <summary>
    /// Loads and unloads area scenes additively on top of the persistent Boot scene.
    /// An area is a scene identified by its name. At most one area at a time is the "current" one
    /// (the active scene: lighting settings and instantiation target); others may stay loaded
    /// as neighbours so a transition can stream the next area before the old one goes away.
    /// </summary>
    public sealed class AreaSceneService
    {
        readonly List<string> loaded = new List<string>();
        readonly HashSet<string> loading = new HashSet<string>();

        /// <summary>Names of areas currently loaded, in load order.</summary>
        public IReadOnlyList<string> Loaded => loaded;
        /// <summary>The area whose scene is the active scene, or null.</summary>
        public string Current { get; private set; }

        public event Action<string> AreaLoaded;
        public event Action<string> AreaUnloaded;

        public bool IsLoaded(string area) => loaded.Contains(area);

        /// <summary>
        /// Loads an area additively. Completes immediately if it is already loaded or being loaded.
        /// If makeCurrent is true the area becomes the active scene when it finishes.
        /// </summary>
        public IEnumerator Load(string area, bool makeCurrent = true)
        {
            if (string.IsNullOrEmpty(area)) throw new ArgumentException("area name required", nameof(area));
            if (loaded.Contains(area)) { if (makeCurrent) SetCurrent(area); yield break; }
            if (!loading.Add(area)) { while (loading.Contains(area)) yield return null; if (makeCurrent) SetCurrent(area); yield break; }

            AsyncOperation op = null;
            try { op = SceneManager.LoadSceneAsync(area, LoadSceneMode.Additive); }
            catch (Exception) { loading.Remove(area); throw; }
            if (op == null) { loading.Remove(area); throw new InvalidOperationException("scene '" + area + "' is not in the build/test scene list"); }
            while (!op.isDone) yield return null;

            loading.Remove(area);
            loaded.Add(area);
            if (makeCurrent) SetCurrent(area);
            AreaLoaded?.Invoke(area);
        }

        /// <summary>Unloads an area. No-op if it is not loaded. If it was current, the persistent scene (if any) becomes active again.</summary>
        public IEnumerator Unload(string area)
        {
            if (!loaded.Remove(area)) yield break;
            var scene = SceneManager.GetSceneByName(area);
            if (scene.IsValid() && scene.isLoaded)
            {
                var op = SceneManager.UnloadSceneAsync(scene);
                while (op != null && !op.isDone) yield return null;
            }
            if (Current == area) Current = loaded.Count > 0 ? loaded[loaded.Count - 1] : null;
            if (Current != null) SetCurrent(Current);
            AreaUnloaded?.Invoke(area);
        }

        /// <summary>Loads the target and, once it is current, unloads every other area.</summary>
        public IEnumerator Switch(string area)
        {
            yield return Load(area, true);
            foreach (var other in new List<string>(loaded))
                if (other != area) yield return Unload(other);
        }

        void SetCurrent(string area)
        {
            var scene = SceneManager.GetSceneByName(area);
            if (scene.IsValid() && scene.isLoaded) SceneManager.SetActiveScene(scene);
            Current = area;
        }
    }
}

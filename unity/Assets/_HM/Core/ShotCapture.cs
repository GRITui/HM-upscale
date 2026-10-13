using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace HM.Core
{
    /// <summary>
    /// Player flag driven screenshot: -hm-shot PATH [-hm-shot-delay SECONDS] [-hm-shot-size WxH]
    /// [-hm-shot-frames N -hm-shot-interval SECONDS] (N numbered PNGs, PATH_000.png...).
    /// Renders the main camera and any uGUI canvases into one RenderTexture and writes a PNG, then quits.
    /// It exists so the scene and HUD can be checked in a headless or locked-session run where a window
    /// grab is impossible. It is an in-engine render, not an OS window capture.
    /// </summary>
    public sealed class ShotCapture : MonoBehaviour
    {
        string path;
        float delay;
        int w = 1280, h = 720;
        float t;
        int frames = 1, index;
        float interval = 0.5f;
        bool done;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoCreate()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-hm-shot");
            if (i < 0 || i + 1 >= args.Length) return;
            var go = new GameObject("ShotCapture");
            DontDestroyOnLoad(go);
            var c = go.AddComponent<ShotCapture>();
            c.path = args[i + 1];
            int d = Array.IndexOf(args, "-hm-shot-delay");
            if (d >= 0 && d + 1 < args.Length) float.TryParse(args[d + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out c.delay);
            int fr = Array.IndexOf(args, "-hm-shot-frames");
            if (fr >= 0 && fr + 1 < args.Length) int.TryParse(args[fr + 1], out c.frames);
            int iv = Array.IndexOf(args, "-hm-shot-interval");
            if (iv >= 0 && iv + 1 < args.Length) float.TryParse(args[iv + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out c.interval);
            int z = Array.IndexOf(args, "-hm-shot-size");
            if (z >= 0 && z + 1 < args.Length)
            {
                var p = args[z + 1].Split('x');
                if (p.Length == 2) { int.TryParse(p[0], out c.w); int.TryParse(p[1], out c.h); }
            }
        }

        void Update()
        {
            if (done) return;
            t += Time.unscaledDeltaTime;
            if (t < delay) return;
            done = true;
            var cam = Camera.main;
            if (cam == null) { Debug.LogError("[HM-SHOT] no main camera"); Application.Quit(1); return; }

            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            // Overlay canvases draw to the backbuffer, which a headless run cannot read; route them through the camera.
            foreach (var cv in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (cv.isRootCanvas && cv.renderMode == RenderMode.ScreenSpaceOverlay)
                { cv.renderMode = RenderMode.ScreenSpaceCamera; cv.worldCamera = cam; cv.planeDistance = 1f; }
            cam.targetTexture = rt;
            cam.Render();
            StartCoroutine(Finish(rt, cam));
        }

        System.Collections.IEnumerator Finish(RenderTexture rt, Camera cam)
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            string outPath = frames > 1
                ? Path.Combine(Path.GetDirectoryName(path) ?? "", Path.GetFileNameWithoutExtension(path) + "_" + index.ToString("000") + ".png")
                : path;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
                File.WriteAllBytes(outPath, tex.EncodeToPNG());
                Debug.Log("[HM-SHOT] wrote " + outPath);
            }
            catch (Exception e) { Debug.LogError("[HM-SHOT] " + e.Message); }
            cam.targetTexture = null;
            Destroy(tex); rt.Release(); Destroy(rt);
            if (++index < frames) { done = false; t = 0f; delay = interval; yield break; }
            Application.Quit();
        }
    }
}

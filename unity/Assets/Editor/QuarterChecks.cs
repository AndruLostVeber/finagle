using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kuro.EditorTools
{
    public static class QuarterChecks
    {
        static readonly string Evidence = Path.GetFullPath("../artifacts/unity-quarter");

        [MenuItem("Kuro/Quarter/Input checks")]
        public static void InputChecks()
        {
            var type = typeof(SeatedLook).Assembly.GetType("Kuro.TouchLookGesture");
            Require(type != null, "Touch gesture must distinguish release taps from drags");
            dynamic gesture = Activator.CreateInstance(type);
            gesture.Begin(new Vector2(100, 100), 0f, 12f);
            gesture.Move(new Vector2(105, 102));
            Require(gesture.End(new Vector2(105, 102), 0.2f), "Small finger jitter stays a tap");
            gesture.Begin(Vector2.zero, 0f, 12f);
            gesture.Move(new Vector2(30, 0));
            gesture.Move(Vector2.zero);
            Require(!gesture.End(Vector2.zero, 0.2f), "Drag returning to its origin must never tap");
            gesture.Begin(Vector2.zero, 0f, 12f);
            Require(!gesture.End(new Vector2(30, 0), 0.2f), "Release displacement counts even without a moved frame");
            gesture.Begin(Vector2.zero, 0f, 12f);
            Require(!gesture.End(Vector2.zero, 0.8f), "Long press must not accidentally activate");
            gesture.Begin(Vector2.zero, 0f, 12f);
            gesture.Cancel();
            Require(!gesture.End(Vector2.zero, 0.1f), "Blocked or canceled touch must not activate");
            Require(!gesture.End(Vector2.zero, 0.1f), "A release may activate only once");
            Debug.Log("QUARTER INPUT: 6 checks passed");
        }

        public static void Baseline() { Capture("before"); }

        public static void Regenerate()
        {
            InputChecks();
            OfficeBuilder.Build();
            Capture("after");
        }

        static void Capture(string label)
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Office.unity");
            Require(GraphicsSettings.currentRenderPipeline == null, "Keep the existing Built-in renderer");
            Directory.CreateDirectory(Evidence);
            var camera = Camera.main;
            var arrival = UnityEngine.Object.FindAnyObjectByType<ArrivalCinematic>();
            Shot(camera, label + "-office");
            arrival.Play();
            foreach (float t in new[] { 2f, 10f, 14.5f, 17.8f })
            {
                arrival.Pose(t);
                Shot(camera, label + "-arrival-" + t.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
            }
            arrival.Finish();
            Debug.Log("QUARTER CAPTURE: " + label + " / Built-in / 1280x720 / " + Evidence);
        }

        public static void Shot(Camera camera, string name)
        {
            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGBHalf) { antiAliasing = 4 };
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var png = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            png.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            png.Apply();
            File.WriteAllBytes(Path.Combine(Evidence, name + ".png"), png.EncodeToPNG());
            camera.targetTexture = oldTarget;
            RenderTexture.active = oldActive;
            UnityEngine.Object.DestroyImmediate(png);
            UnityEngine.Object.DestroyImmediate(target);
        }

        static void Require(bool ok, string message)
        {
            if (!ok) throw new InvalidOperationException(message);
        }
    }
}

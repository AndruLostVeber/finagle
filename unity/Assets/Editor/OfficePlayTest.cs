using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Kuro.EditorTools
{
    // Проверка офиса в Play-режиме: поворот у краев экрана, затем для каждого предмета —
    // поворот головы, наведение (контур и подпись), приближение по клику и возврат.
    // На мониторе дополнительно открывается «КУРО OS». Итог — в консоли.
    // Клик и Esc имитируются вызовами Activate и Release — их же вызывает SeatedLook на ввод.
    public static class OfficePlayTest
    {
        static readonly string[] Items =
        {
            "Office/Monitor", "Office/Inbox", "Office/Notebook", "Office/Phone", "Office/Calendar",
            "Office/Clock", "Office/Archive", "Office/Shelf/Accounts", "Office/Shelf/Policy", "Office/Door",
        };

        static IEnumerator<float> run;
        static double nextTime;
        static int passed, failed;

        [MenuItem("Kuro/Проверить офис (в Play)")]
        public static void Run()
        {
            if (!EditorApplication.isPlaying || !SeatedLook.Instance)
            {
                Debug.LogWarning("Проверка офиса: сначала включите Play в сцене Office.");
                return;
            }
            passed = failed = 0;
            run = Steps();
            nextTime = 0;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.update -= Tick;
                return;
            }
            if (EditorApplication.timeSinceStartup < nextTime) return;
            if (run.MoveNext())
            {
                nextTime = EditorApplication.timeSinceStartup + run.Current;
                return;
            }
            EditorApplication.update -= Tick;
            var summary = $"Проверка офиса: {passed} из {passed + failed} пройдено";
            if (failed == 0) Debug.Log(summary);
            else Debug.LogError(summary);
        }

        static void Check(string name, bool ok, string details)
        {
            if (ok) passed++;
            else failed++;
            var line = $"[{(ok ? "OK" : "FAIL")}] {name} — {details}";
            if (ok) Debug.Log(line);
            else Debug.LogError(line);
        }

        static IEnumerator<float> Steps()
        {
            var look = SeatedLook.Instance;
            var cam = look.GetComponent<Camera>();
            var os = Object.FindAnyObjectByType<KuroOS>();
            var osRoot = os.GetComponent<UIDocument>().rootVisualElement;
            var hudLabel = OfficeHud.Instance.GetComponent<UIDocument>().rootVisualElement.Q<Label>(className: "hud__label");
            Vector3 seat = look.transform.position;
            Vector2 center = new Vector2(cam.pixelWidth * 0.5f, cam.pixelHeight * 0.5f);

            // Поворот у краев экрана.
            look.PointerOverride = new Vector2(2f, center.y);
            yield return 2f;
            Check("левый край", Mathf.Abs(look.Yaw - look.yawMin) < 1f, $"yaw {look.Yaw:F1}°");
            look.PointerOverride = new Vector2(cam.pixelWidth - 2f, cam.pixelHeight - 2f);
            yield return 2.5f;
            Check("правый верхний угол", Mathf.Abs(look.Yaw - look.yawMax) < 1f && Mathf.Abs(look.Pitch - look.pitchLimit) < 1f,
                $"yaw {look.Yaw:F1}°, pitch {look.Pitch:F1}°");
            look.PointerOverride = center;
            yield return 0.3f;
            float stillYaw = look.Yaw;
            yield return 0.6f;
            Check("центр экрана", Mathf.Abs(look.Yaw - stillYaw) < 0.5f, $"yaw {stillYaw:F1}° → {look.Yaw:F1}°");

            foreach (var path in Items)
            {
                var it = GameObject.Find(path).GetComponent<Interactable>();
                var col = it.GetComponent<Collider>();
                Vector3 dir = col.bounds.center - seat;
                float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                float pitch = Mathf.Atan2(dir.y, new Vector2(dir.x, dir.z).magnitude) * Mathf.Rad2Deg;

                look.PointerOverride = center;
                bool farTurn = Mathf.Abs(Mathf.DeltaAngle(look.Yaw, yaw)) > 90f;
                look.LookAt(yaw, pitch);
                yield return farTurn ? 2.5f : 1.5f;

                look.PointerOverride = cam.WorldToScreenPoint(VisiblePoint(cam.transform.position, col));
                yield return 0.5f;
                var renderer = it.GetComponentInChildren<MeshRenderer>();
                bool outline = renderer.sharedMaterials.Length > 1;
                bool label = hudLabel.resolvedStyle.display == DisplayStyle.Flex && hudLabel.text == it.label;
                Check($"{it.label}: наведение", look.Hovered == it && it.IsHovered && outline && label,
                    $"под мышью «{(look.Hovered ? look.Hovered.label : "ничего")}», контур {outline}, подпись «{hudLabel.text}»");

                it.Activate();
                yield return look.focusTime + 0.6f;
                float posErr = Vector3.Distance(cam.transform.position, it.focusPoint.position);
                float rotErr = Quaternion.Angle(cam.transform.rotation, it.focusPoint.rotation);
                Check($"{it.label}: приближение", look.IsFocused && look.Focused == it && posErr < 0.01f && rotErr < 1f && !it.IsHovered,
                    $"до точки {posErr * 100f:F1} см, {rotErr:F1}°");

                if (it == os.monitor)
                {
                    var overlay = osRoot.Q(className: "os");
                    Check("КУРО OS: открыт", os.IsOpen && overlay.resolvedStyle.display == DisplayStyle.Flex,
                        $"IsOpen {os.IsOpen}, иконок {osRoot.Query<Button>(className: "os__icon").ToList().Count}");
                    var app = osRoot.Q<Button>(className: "os__icon");
                    using (var e = NavigationSubmitEvent.GetPooled())
                    {
                        e.target = app;
                        app.SendEvent(e);
                    }
                    yield return 0.4f;
                    var title = osRoot.Q<Label>(className: "os__win-title");
                    var win = osRoot.Q(className: "os__win");
                    Check("КУРО OS: окно приложения", win.resolvedStyle.display == DisplayStyle.Flex && title.text == "Бухгалтерия",
                        $"окно «{title.text}»");
                }

                look.Release();
                yield return look.focusTime + 0.6f;
                float seatErr = Vector3.Distance(cam.transform.position, seat);
                bool osClosed = it != os.monitor || !os.IsOpen;
                Check($"{it.label}: возврат", look.IsFree && seatErr < 0.01f && osClosed,
                    $"до кресла {seatErr * 100f:F1} см{(it == os.monitor ? $", КУРО OS закрыт {!os.IsOpen}" : "")}");
            }

            look.PointerOverride = center;
            look.LookAt(0f, 0f);
            yield return 2.5f;
            Check("снова к столу", Mathf.Abs(look.Yaw) < 1f, $"yaw {look.Yaw:F1}°");
            look.PointerOverride = null;
        }

        // Точка предмета, которую видно из глаз: центр может закрывать другой предмет
        // (полку — монитор Марии), тогда пробуем точки ближе к краям.
        static Vector3 VisiblePoint(Vector3 eye, Collider col)
        {
            var b = col.bounds;
            Vector3 e = b.extents * 0.6f;
            Vector3[] offsets =
            {
                Vector3.zero, new Vector3(0f, e.y, 0f), new Vector3(0f, -e.y, 0f),
                new Vector3(e.x, 0f, e.z), new Vector3(-e.x, 0f, -e.z),
                new Vector3(e.x, e.y, e.z), new Vector3(-e.x, e.y, -e.z),
            };
            foreach (var o in offsets)
            {
                Vector3 p = b.center + o;
                if (Physics.Raycast(eye, p - eye, out var hit, 20f, ~0, QueryTriggerInteraction.Ignore) && hit.collider == col)
                    return p;
            }
            return b.center;
        }
    }
}

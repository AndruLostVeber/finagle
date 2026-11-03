using UnityEngine;
using UnityEngine.UIElements;

namespace Kuro
{
    // Заглушка экрана компьютера «КУРО OS»: рабочий стол со списком приложений.
    // Открывается, когда камера подлетела к монитору, и ложится ровно на его экран.
    // Назад в офис — Esc, правая кнопка или «Назад» в HUD.
    [RequireComponent(typeof(UIDocument))]
    public class KuroOS : MonoBehaviour
    {
        public Interactable monitor;
        [Tooltip("Поверхность экрана 3D-монитора: по ней рабочий стол встает на место.")]
        public Renderer screenSurface;
        public StyleSheet styleSheet;
        public string date = "12.01.2026";
        public string time = "09:00";

        static readonly (string name, string glyph, string color)[] Apps =
        {
            ("Бухгалтерия", "Дт", "#1F6F68"),
            ("Банк-клиент", "51", "#2F5E8C"),
            ("Почта", "@", "#C4453B"),
            ("Калькулятор", "±", "#3A4045"),
            ("План счетов", "№", "#6E4E36"),
        };

        VisualElement overlay, screen, window;
        Label windowTitle, taskTab;
        bool isOpen;

        public bool IsOpen => isOpen;

        void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            KuroUI.Prepare(root, styleSheet);
            Build(root);
            Close();
            if (monitor)
            {
                monitor.Focused += Open;
                monitor.Released += Close;
            }
        }

        void OnDisable()
        {
            if (!monitor) return;
            monitor.Focused -= Open;
            monitor.Released -= Close;
        }

        void LateUpdate()
        {
            if (isOpen) FitToScreen();
        }

        void Build(VisualElement root)
        {
            overlay = KuroUI.Add<VisualElement>(root, "os");
            screen = KuroUI.Add<VisualElement>(overlay, "os__screen", pickable: true);
            var desk = KuroUI.Add<VisualElement>(screen, "os__desk", pickable: true);

            var icons = KuroUI.Add<VisualElement>(desk, "os__icons");
            foreach (var app in Apps)
            {
                var icon = KuroUI.Add<Button>(icons, "os__icon", pickable: true);
                var ic = KuroUI.Add<VisualElement>(icon, "os__ic");
                if (ColorUtility.TryParseHtmlString(app.color, out var c)) ic.style.backgroundColor = c;
                KuroUI.Text(ic, "os__ic-glyph", app.glyph);
                KuroUI.Text(icon, "os__icon-name", app.name);
                string name = app.name;
                icon.clicked += () => OpenApp(name);
            }
            KuroUI.Text(desk, "os__logo", "КУРО\nПРОДАКШН");

            window = KuroUI.Add<VisualElement>(desk, "os__win", pickable: true);
            var bar = KuroUI.Add<VisualElement>(window, "os__win-bar");
            windowTitle = KuroUI.Text(bar, "os__win-title", "");
            KuroUI.Add<VisualElement>(bar, "os__spacer");
            var close = KuroUI.Add<Button>(bar, "os__win-close", pickable: true);
            close.text = "×";
            close.clicked += CloseApp;
            var body = KuroUI.Add<VisualElement>(window, "os__win-body");
            KuroUI.Text(body, "os__win-text", "В разработке");

            var taskbar = KuroUI.Add<VisualElement>(screen, "os__taskbar");
            KuroUI.Text(taskbar, "os__start", "КУРО OS");
            taskTab = KuroUI.Text(taskbar, "os__tab", "");
            KuroUI.Add<VisualElement>(taskbar, "os__spacer");
            KuroUI.Text(taskbar, "os__date", date);
            KuroUI.Text(taskbar, "os__clock", time);
        }

        void Open()
        {
            // В браузере вместо заглушки открывается компьютер из демо.
            if (WebBridge.Active) return;
            isOpen = true;
            overlay.style.display = DisplayStyle.Flex;
            FitToScreen();
            overlay.schedule.Execute(() => overlay.AddToClassList("os--open"));
        }

        // Прямоугольник экрана 3D-монитора в координатах панели.
        void FitToScreen()
        {
            var cam = Camera.main;
            var panel = overlay.panel;
            if (!screenSurface || !cam || panel == null) return;
            var b = screenSurface.bounds;
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                var corner = b.center + Vector3.Scale(b.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 s = cam.WorldToScreenPoint(corner);
                var p = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(s.x, Screen.height - s.y));
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            screen.style.left = min.x;
            screen.style.top = min.y;
            screen.style.width = max.x - min.x;
            screen.style.height = max.y - min.y;
        }

        void Close()
        {
            isOpen = false;
            overlay.RemoveFromClassList("os--open");
            overlay.style.display = DisplayStyle.None;
            CloseApp();
        }

        void OpenApp(string name)
        {
            windowTitle.text = name;
            taskTab.text = name;
            window.style.display = DisplayStyle.Flex;
            taskTab.style.display = DisplayStyle.Flex;
        }

        void CloseApp()
        {
            window.style.display = DisplayStyle.None;
            taskTab.style.display = DisplayStyle.None;
        }
    }
}

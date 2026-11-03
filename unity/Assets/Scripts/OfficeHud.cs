using UnityEngine;
using UnityEngine.UIElements;

namespace Kuro
{
    // Низ экрана в офисе: подпись предмета под мышью, подсказка A/D в обзоре
    // и кнопка «Назад», когда камера у предмета, — для тех, кто играет одной мышью.
    [RequireComponent(typeof(UIDocument))]
    public class OfficeHud : MonoBehaviour
    {
        public static OfficeHud Instance { get; private set; }

        public StyleSheet styleSheet;

        VisualElement root, hud, hint;
        Label label;
        Button back;

        void OnEnable()
        {
            Instance = this;
            root = GetComponent<UIDocument>().rootVisualElement;
            KuroUI.Prepare(root, styleSheet);

            hud = KuroUI.Add<VisualElement>(root, "hud");
            var bottom = KuroUI.Add<VisualElement>(hud, "hud__bottom");
            label = KuroUI.Text(bottom, "hud__label", "");
            label.style.display = DisplayStyle.None;

            var row = KuroUI.Add<VisualElement>(bottom, "hud__row");
            hint = KuroUI.Add<VisualElement>(row, "hud__hint");
            KuroUI.Text(hint, "kc", "A");
            KuroUI.Text(hint, "kc", "D");
            KuroUI.Text(hint, "hud__hint-text", "осмотреться");

            back = KuroUI.Add<Button>(row, "hud__btn", pickable: true);
            KuroUI.Text(back, "kc", "Esc");
            KuroUI.Text(back, "hud__btn-text", "Назад");
            back.clicked += OnBack;
        }

        void OnDisable()
        {
            if (back != null) back.clicked -= OnBack;
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            var look = SeatedLook.Instance;
            if (!look) return;
            // В браузере подписи рисует страница, а поверх игры бывают окна демо со своим «Назад».
            hud.style.display = WebBridge.Active ? DisplayStyle.None : DisplayStyle.Flex;
            hint.style.display = look.IsFree ? DisplayStyle.Flex : DisplayStyle.None;
            back.style.display = look.IsFree ? DisplayStyle.None : DisplayStyle.Flex;
        }

        void OnBack()
        {
            if (SeatedLook.Instance) SeatedLook.Instance.Release();
        }

        public void ShowLabel(string text)
        {
            if (label == null) return;
            label.text = text ?? "";
            label.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        // Есть ли под указателем кнопка интерфейса (тогда клик не уходит в сцену).
        public bool IsPointerOverUI(Vector2 screenPos)
        {
            var panel = root?.panel;
            if (panel == null) return false;
            var pos = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
            return panel.Pick(pos) != null;
        }
    }
}

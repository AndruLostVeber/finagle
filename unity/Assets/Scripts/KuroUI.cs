using UnityEngine;
using UnityEngine.UIElements;

namespace Kuro
{
    // Общие мелочи для экранов на UI Toolkit.
    public static class KuroUI
    {
        static Font font;

        // Корень документа не ловит мышь, чтобы клики проходили в 3D-сцену.
        // Встроенный шрифт — с кириллицей. В WebGL текст UI Toolkit не рисуется,
        // там подписи показывает страница (см. WebBridge).
        public static void Prepare(VisualElement root, StyleSheet sheet)
        {
            if (sheet && !root.styleSheets.Contains(sheet)) root.styleSheets.Add(sheet);
            root.pickingMode = PickingMode.Ignore;
            if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            root.style.unityFontDefinition = FontDefinition.FromFont(font);
        }

        public static T Add<T>(VisualElement parent, string className, bool pickable = false) where T : VisualElement, new()
        {
            var e = new T();
            e.AddToClassList(className);
            if (!pickable) e.pickingMode = PickingMode.Ignore;
            parent.Add(e);
            return e;
        }

        public static Label Text(VisualElement parent, string className, string text)
        {
            var l = Add<Label>(parent, className);
            l.text = text;
            return l;
        }
    }
}

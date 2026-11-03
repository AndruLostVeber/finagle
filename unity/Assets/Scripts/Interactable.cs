using System;
using UnityEngine;

namespace Kuro
{
    // Предмет офиса: контур при наведении, подпись, приближение камеры по клику, возврат по Esc.
    // Предмет под мышью находит SeatedLook и вызывает SetHovered и Activate.
    public class Interactable : MonoBehaviour
    {
        public string label = "Предмет";
        [Tooltip("Куда встает камера. Пусто — камера подлетает к центру предмета на focusDistance.")]
        public Transform focusPoint;
        public float focusDistance = 0.8f;
        public Material outlineMaterial;
        [Tooltip("Что открыть в демо, когда камера подлетела (WebGL): pc, tray, book, phone, accounts, terms, docs, cal, clock, door.")]
        public string webAction;

        // Камера долетела до предмета / начала возвращаться в обзор.
        public event Action Focused;
        public event Action Released;

        public bool IsHovered { get; private set; }

        static Material boxOutline, smoothOutline;

        Renderer[] renderers;
        Material[][] baseMaterials, hoverMaterials;

        void Awake()
        {
            renderers = Array.FindAll(GetComponentsInChildren<Renderer>(), r => !r.GetComponent<TextMesh>());
            baseMaterials = new Material[renderers.Length][];
            hoverMaterials = new Material[renderers.Length][];
            for (int i = 0; i < renderers.Length; i++)
            {
                var mats = renderers[i].sharedMaterials;
                baseMaterials[i] = mats;
                if (!outlineMaterial)
                {
                    hoverMaterials[i] = mats;
                    continue;
                }
                var hover = new Material[mats.Length + 1];
                mats.CopyTo(hover, 0);
                hover[mats.Length] = OutlineFor(renderers[i]);
                hoverMaterials[i] = hover;
            }
        }

        void OnDisable() => SetHovered(false);

        public void SetHovered(bool on)
        {
            if (IsHovered == on || renderers == null) return;
            IsHovered = on;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i]) renderers[i].sharedMaterials = on ? hoverMaterials[i] : baseMaterials[i];
            if (OfficeHud.Instance) OfficeHud.Instance.ShowLabel(on ? label : null);
        }

        public void Activate()
        {
            var look = SeatedLook.Instance;
            if (look && look.IsFree) look.FocusOn(this);
        }

        public void GetFocusPose(Vector3 eye, out Vector3 position, out Quaternion rotation)
        {
            if (focusPoint)
            {
                position = focusPoint.position;
                rotation = focusPoint.rotation;
                return;
            }
            Vector3 center = WorldBounds().center;
            Vector3 dir = (center - eye).normalized;
            position = center - dir * focusDistance;
            rotation = Quaternion.LookRotation(dir);
        }

        internal void NotifyFocused() => Focused?.Invoke();
        internal void NotifyReleased() => Released?.Invoke();

        Bounds WorldBounds()
        {
            var col = GetComponent<Collider>();
            if (col) return col.bounds;
            var b = new Bounds(transform.position, Vector3.zero);
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        // Кубам контур нужен по углам, иначе на рёбрах рвется; остальным — по нормалям.
        Material OutlineFor(Renderer r)
        {
            var filter = r.GetComponent<MeshFilter>();
            bool box = filter && filter.sharedMesh && filter.sharedMesh.name == "Cube";
            if (box)
            {
                if (!boxOutline) boxOutline = MakeOutline(1f);
                return boxOutline;
            }
            if (!smoothOutline) smoothOutline = MakeOutline(0f);
            return smoothOutline;
        }

        Material MakeOutline(float boxMode)
        {
            var m = new Material(outlineMaterial) { name = outlineMaterial.name + (boxMode > 0f ? " Box" : " Smooth") };
            m.SetFloat("_Box", boxMode);
            return m;
        }
    }
}

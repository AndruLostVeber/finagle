using UnityEngine;

namespace Kuro
{
    // Лист бумаги или ткани, который шевелит сквозняк (Wind). Меш — сетка в плоскости XZ
    // (MeshKit.Sheet); край +Z закреплен, остальное отклоняется вдоль локальной +Y.
    // Бумага: поднимается свободный край. Ткань (cloth): висит складками и колышется.
    [RequireComponent(typeof(MeshFilter))]
    public class WindSheet : MonoBehaviour
    {
        [Tooltip("Насколько отходит свободный край в сильный порыв, м.")]
        public float lift = 0.03f;
        public bool cloth;
        [Tooltip("Ткань: глубина складок, м.")]
        public float folds = 0.02f;
        public float foldCount = 5f;
        [Tooltip("Ткань: постоянное полоскание, как у флага на ветру, м.")]
        public float wave;
        public float phase = -1f;

        Mesh mesh;
        Vector3[] rest, work;
        float minX, maxX, minZ, maxZ;

        void Start()
        {
            var filter = GetComponent<MeshFilter>();
            mesh = Instantiate(filter.sharedMesh);
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;
            rest = mesh.vertices;
            work = new Vector3[rest.Length];
            minX = minZ = float.MaxValue;
            maxX = maxZ = float.MinValue;
            foreach (var p in rest)
            {
                minX = Mathf.Min(minX, p.x);
                maxX = Mathf.Max(maxX, p.x);
                minZ = Mathf.Min(minZ, p.z);
                maxZ = Mathf.Max(maxZ, p.z);
            }
            if (phase < 0f) phase = Random.value * 20f;
            Deform(0f, 0f);
        }

        void Update()
        {
            float t = Time.time;
            Deform(Wind.Gust(t, phase), t);
        }

        void Deform(float gust, float t)
        {
            float width = Mathf.Max(maxX - minX, 1e-4f), depth = Mathf.Max(maxZ - minZ, 1e-4f);
            for (int i = 0; i < rest.Length; i++)
            {
                var p = rest[i];
                float k = (maxZ - p.z) / depth;      // 0 — у закрепленного края, 1 — свободный край
                float x = (p.x - minX) / width;
                float d;
                if (cloth)
                {
                    float fold = Mathf.Sin(x * foldCount * Mathf.PI * 2f) * folds * (0.5f + 0.5f * k);
                    float wave = 0.65f + 0.35f * Mathf.Sin(t * 2.6f + x * 7f + k * 3f + phase);
                    d = fold + gust * lift * Mathf.Pow(k, 1.4f) * wave
                        + this.wave * k * Mathf.Sin(t * 5.5f - k * 7f + x * 2f + phase);
                }
                else
                {
                    float ripple = 0.6f + 0.4f * Mathf.Sin(t * 11f + x * 9f + phase);
                    d = gust * lift * k * k * ripple;
                }
                work[i] = p + Vector3.up * d;
            }
            mesh.vertices = work;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
    }
}

using UnityEngine;

namespace Kuro
{
    // Картинка камеры офиса: свечение ярких мест (лампы, экраны, телефон), экспозиция,
    // тонирование ACES, контраст, насыщенность, теплый оттенок, виньетка (шейдер Hidden/Kuro/Post).
    // В игре еще ставит качество: WebGL по умолчанию берет уровень High — там 2 источника света,
    // грубые тени и без сглаживания.
    [RequireComponent(typeof(Camera))]
    [ExecuteAlways]
    public class KuroPost : MonoBehaviour
    {
        public Shader shader;

        [Header("Цвет")]
        public float exposure = 1.12f;
        public float contrast = 1.06f;
        public float saturation = 1.06f;
        public Color tint = new Color(1.02f, 1f, 0.97f);

        [Header("Свечение")]
        public float bloomThreshold = 1.4f;
        public float bloomKnee = 0.5f;
        public float bloomIntensity = 0.5f;
        [Range(2, 7)] public int bloomSteps = 6;

        [Header("Виньетка")]
        [Range(0f, 1f)] public float vignette = 0.3f;

        [Header("Качество в игре")]
        public int qualityLevel = 5;

        Material mat;
        readonly RenderTexture[] chain = new RenderTexture[8];

        void Awake()
        {
            GetComponent<Camera>().allowHDR = true;
            if (Application.isPlaying) ApplyQuality();
        }

        void ApplyQuality()
        {
            if (qualityLevel >= 0 && qualityLevel < QualitySettings.names.Length && QualitySettings.GetQualityLevel() != qualityLevel)
                QualitySettings.SetQualityLevel(qualityLevel, true);
            QualitySettings.pixelLightCount = 8;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.shadowDistance = 16f;
            QualitySettings.shadowCascades = 2;
            QualitySettings.antiAliasing = 4;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        }

        void OnDisable()
        {
            if (mat) DestroyImmediate(mat);
        }

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (!shader || !shader.isSupported)
            {
                Graphics.Blit(src, dst);
                return;
            }
            if (!mat) mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };

            mat.SetFloat("_Threshold", bloomThreshold);
            mat.SetFloat("_Knee", Mathf.Max(bloomKnee, 0.01f));
            var format = src.format;
            int w = src.width / 2, h = src.height / 2;
            var current = RenderTexture.GetTemporary(w, h, 0, format);
            Graphics.Blit(src, current, mat, 0);
            chain[0] = current;
            int steps = 1;
            for (; steps < bloomSteps; steps++)
            {
                w /= 2;
                h /= 2;
                if (w < 4 || h < 4) break;
                var next = RenderTexture.GetTemporary(w, h, 0, format);
                Graphics.Blit(current, next, mat, 1);
                chain[steps] = current = next;
            }
            for (int i = steps - 2; i >= 0; i--)
            {
                Graphics.Blit(current, chain[i], mat, 2);
                current = chain[i];
            }

            mat.SetTexture("_Bloom", current);
            mat.SetFloat("_BloomIntensity", bloomIntensity);
            mat.SetFloat("_Exposure", exposure);
            mat.SetFloat("_Contrast", contrast);
            mat.SetFloat("_Saturation", saturation);
            mat.SetFloat("_Vignette", vignette);
            mat.SetColor("_Tint", tint);
            Graphics.Blit(src, dst, mat, 3);

            for (int i = 0; i < steps; i++)
            {
                RenderTexture.ReleaseTemporary(chain[i]);
                chain[i] = null;
            }
        }
    }
}

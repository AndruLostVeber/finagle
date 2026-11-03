using UnityEngine;

namespace Kuro
{
    // Сквозняк из приоткрытой форточки: редкие порывы и мелкая дрожь между ними.
    // phase разводит предметы во времени, чтобы они не шевелились одинаково.
    public static class Wind
    {
        public static float Gust(float time, float phase)
        {
            float slow = Mathf.PerlinNoise(time * 0.11f + phase * 0.05f, 0.37f);
            float gust = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 0.78f, slow));
            float flutter = Mathf.PerlinNoise(time * 2.3f + phase, phase * 3.1f);
            return gust * (0.55f + 0.45f * flutter) + 0.05f * flutter;
        }
    }
}

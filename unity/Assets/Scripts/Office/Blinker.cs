using UnityEngine;

namespace Kuro
{
    // Свечение лампы меняется: мигает (Blink), плавно пульсирует (Pulse) или
    // изредка подрагивает, как старая люминесцентная трубка или пламя (Flicker).
    // Меняет только _EmissionColor через MaterialPropertyBlock и яркость света, если он задан.
    public class Blinker : MonoBehaviour
    {
        public enum Mode { Blink, Pulse, Flicker }

        public Mode mode = Mode.Blink;
        public Color emission = Color.white;
        public float period = 1.2f;
        public Light lightSource;

        Renderer rend;
        MaterialPropertyBlock block;
        float baseIntensity, phase, dip, dipUntil;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        void Start()
        {
            rend = GetComponent<Renderer>();
            block = new MaterialPropertyBlock();
            if (lightSource) baseIntensity = lightSource.intensity;
            phase = Random.value * period;
        }

        void Update()
        {
            float t = Time.time + phase, k;
            switch (mode)
            {
                case Mode.Blink:
                    k = Mathf.Repeat(t, period) < period * 0.5f ? 1f : 0.08f;
                    break;
                case Mode.Pulse:
                    k = 0.55f + 0.45f * Mathf.Sin(t / period * Mathf.PI * 2f);
                    break;
                default:
                    if (Time.time > dipUntil && Random.value < Time.deltaTime / period)
                    {
                        dipUntil = Time.time + Random.Range(0.05f, 0.35f);
                        dip = Random.Range(0.2f, 0.7f);
                    }
                    k = Time.time < dipUntil ? dip + Random.value * 0.2f : 0.95f + 0.05f * Mathf.PerlinNoise(t * 8f, 0f);
                    break;
            }
            if (rend)
            {
                rend.GetPropertyBlock(block);
                block.SetColor(EmissionId, emission * k);
                rend.SetPropertyBlock(block);
            }
            if (lightSource) lightSource.intensity = baseIntensity * k;
        }
    }
}

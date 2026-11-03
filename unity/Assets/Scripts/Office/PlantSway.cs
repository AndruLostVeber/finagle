using UnityEngine;

namespace Kuro
{
    // Листья растения покачиваются на сквозняке: каждый дочерний объект — черенок листа.
    public class PlantSway : MonoBehaviour
    {
        public float angle = 7f;

        Transform[] leaves;
        Quaternion[] rest;
        float[] phase;

        void Start()
        {
            leaves = new Transform[transform.childCount];
            rest = new Quaternion[leaves.Length];
            phase = new float[leaves.Length];
            for (int i = 0; i < leaves.Length; i++)
            {
                leaves[i] = transform.GetChild(i);
                rest[i] = leaves[i].localRotation;
                phase[i] = Random.value * 10f;
            }
        }

        void Update()
        {
            float t = Time.time;
            for (int i = 0; i < leaves.Length; i++)
            {
                float g = Wind.Gust(t, phase[i]);
                float a = g * angle * (0.6f + 0.4f * Mathf.Sin(t * 5f + phase[i]));
                leaves[i].localRotation = rest[i] * Quaternion.Euler(a, 0f, a * 0.4f);
            }
        }
    }
}

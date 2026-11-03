using UnityEngine;

namespace Kuro
{
    // Флаконы едут по ленте вдоль локальной +Z и возвращаются в начало.
    public class Conveyor : MonoBehaviour
    {
        public Transform[] items;
        public float length = 3.3f;
        public float speed = 0.22f;

        Vector3[] start;

        void Start()
        {
            start = new Vector3[items.Length];
            for (int i = 0; i < items.Length; i++) start[i] = items[i].localPosition;
        }

        void Update()
        {
            float shift = Time.time * speed;
            for (int i = 0; i < items.Length; i++)
            {
                var p = start[i];
                p.z = Mathf.Repeat(p.z + length / 2f + shift, length) - length / 2f;
                items[i].localPosition = p;
            }
        }
    }
}

using UnityEngine;

namespace Kuro
{
    // Автоматическая дверь или турникет: когда камера ближе radius, створки плавно
    // сдвигаются на openOffsets и поворачиваются на openAngles (локально), потом закрываются.
    public class AutoDoor : MonoBehaviour
    {
        public Transform[] leaves;
        public Vector3[] openOffsets;
        public Vector3[] openAngles;
        public float radius = 5f;
        [Tooltip("Сколько раз в секунду можно открыть-закрыть полностью.")]
        public float speed = 1.1f;

        Vector3[] pos0;
        Quaternion[] rot0;
        float open;

        void Start()
        {
            pos0 = new Vector3[leaves.Length];
            rot0 = new Quaternion[leaves.Length];
            for (int i = 0; i < leaves.Length; i++)
            {
                pos0[i] = leaves[i].localPosition;
                rot0[i] = leaves[i].localRotation;
            }
        }

        void Update()
        {
            var cam = Camera.main;
            bool near = cam && Vector3.Distance(cam.transform.position, transform.position) < radius;
            open = Mathf.MoveTowards(open, near ? 1f : 0f, Time.deltaTime * speed);
            float e = open * open * (3f - 2f * open);
            for (int i = 0; i < leaves.Length; i++)
            {
                var offset = openOffsets != null && i < openOffsets.Length ? openOffsets[i] : Vector3.zero;
                var angle = openAngles != null && i < openAngles.Length ? openAngles[i] : Vector3.zero;
                leaves[i].localPosition = pos0[i] + offset * e;
                leaves[i].localRotation = rot0[i] * Quaternion.Euler(angle * e);
            }
        }
    }
}

using UnityEngine;

namespace Kuro
{
    // Вращение вокруг локальной оси: вентилятор, мешалка, ролики конвейера.
    public class Spinner : MonoBehaviour
    {
        public Vector3 axis = Vector3.up;
        public float degreesPerSecond = 180f;

        void Update() => transform.Rotate(axis, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}

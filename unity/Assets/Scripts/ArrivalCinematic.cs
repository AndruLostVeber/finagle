using UnityEngine;

namespace Kuro
{
    // Первый день: камера подходит к башне делового центра, входит в лобби, проходит
    // турникет и дверь офиса (сцена Arrival далеко от офиса, включена только на время ролика).
    // Путь — точки и цели взгляда в координатах origin, время в секундах, сплайн Catmull-Rom.
    // Конец ролика в WebGL — WebBridge.Arrived (страница затемняет экран и начинает день);
    // в редакторе — сразу офис. Двери и турникеты открывает AutoDoor по близости камеры.
    public class ArrivalCinematic : MonoBehaviour
    {
        public static ArrivalCinematic Instance { get; private set; }

        public GameObject world;
        public Transform origin;
        public Vector3[] points;
        public Vector3[] looks;
        public float[] times;
        [Tooltip("Скорость, с которой шаг начинает покачивать камеру, м/с.")]
        public float walkSpeed = 1.2f;

        public bool Playing { get; private set; }
        public float Duration => times[times.Length - 1];

        Camera cam;
        SeatedLook look;
        float time, farClip;
        CameraClearFlags clearFlags;
        bool reported;

        void Awake()
        {
            Instance = this;
            if (world) world.SetActive(false);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Play()
        {
            cam = Camera.main;
            look = cam.GetComponent<SeatedLook>();
            if (!Playing)
            {
                farClip = cam.farClipPlane;
                clearFlags = cam.clearFlags;
            }
            world.SetActive(true);
            if (look) look.enabled = false;
            cam.farClipPlane = 700f;
            cam.clearFlags = CameraClearFlags.Skybox;
            time = 0f;
            reported = false;
            Playing = true;
            Pose(0f);
        }

        public void Skip()
        {
            if (!Playing) return;
            time = Duration;
            Report();
        }

        // Вернуть камеру офису: выключить сцену, восстановить камеру, отдать управление SeatedLook.
        public void Finish()
        {
            if (!Playing) return;
            Playing = false;
            world.SetActive(false);
            cam.farClipPlane = farClip;
            cam.clearFlags = clearFlags;
            if (look) look.enabled = true;
        }

        void Update()
        {
            if (!Playing) return;
            time = Mathf.Min(time + Time.deltaTime, Duration);
            Pose(time);
            if (time >= Duration) Report();
        }

        void Report()
        {
            if (reported) return;
            reported = true;
            if (WebBridge.Active)
            {
                WebBridge.Arrived();
                return;
            }
            Finish();
            if (look) look.Wake(0.4f);
        }

        // Положение камеры в момент t (и для проверочных кадров в редакторе).
        public void Pose(float t)
        {
            if (!cam) cam = Camera.main;
            int i = Segment(t);
            float u = Mathf.InverseLerp(times[i], times[i + 1], t);
            Vector3 pos = Spline(i, u);
            Vector3 target = Vector3.Lerp(looks[i], looks[i + 1], u * u * (3f - 2f * u));

            // Шаг: камера покачивается, пока идет.
            float speed = (Spline(i, Mathf.Min(u + 0.02f, 1f)) - Spline(i, Mathf.Max(u - 0.02f, 0f))).magnitude
                          / Mathf.Max((times[i + 1] - times[i]) * 0.04f, 1e-3f);
            float walk = Mathf.Clamp01(speed / walkSpeed - 0.3f);
            pos.y += Mathf.Abs(Mathf.Sin(t * Mathf.PI * 1.9f)) * 0.035f * walk;

            var at = origin.TransformPoint(pos);
            var aim = origin.TransformPoint(target);
            var rot = Quaternion.LookRotation(aim - at) * Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 0.95f) * 0.6f * walk);
            cam.transform.SetPositionAndRotation(at, rot);
        }

        int Segment(float t)
        {
            for (int i = 0; i < times.Length - 2; i++)
                if (t < times[i + 1]) return i;
            return times.Length - 2;
        }

        Vector3 Spline(int i, float u)
        {
            Vector3 p0 = points[Mathf.Max(i - 1, 0)], p1 = points[i], p2 = points[i + 1], p3 = points[Mathf.Min(i + 2, points.Length - 1)];
            float u2 = u * u, u3 = u2 * u;
            return 0.5f * (2f * p1 + (p2 - p0) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (3f * p1 - p0 - 3f * p2 + p3) * u3);
        }
    }
}

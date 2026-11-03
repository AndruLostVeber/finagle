using UnityEngine;

namespace Kuro
{
    // Сидячий вид от первого лица, как в FNAF: голова поворачивается, когда мышь у края
    // экрана, A/D делают то же с клавиатуры. Поворот только влево-вправо в пределах,
    // где стоят предметы (yawMin…yawMax), назад не развернуться.
    // Клик по Interactable приближает камеру к предмету, Esc или правая кнопка возвращают в обзор.
    [RequireComponent(typeof(Camera))]
    public class SeatedLook : MonoBehaviour
    {
        public static SeatedLook Instance { get; private set; }

        [Header("Обзор")]
        [Tooltip("Крайний поворот влево — до двери на левой стене.")]
        public float yawMin = -64f;
        [Tooltip("Крайний поворот вправо — до шкафа-архива.")]
        public float yawMax = 32f;
        public float pitchLimit = 15f;
        [Range(0.02f, 0.4f)] public float edgeZone = 0.14f;
        [Range(0.02f, 0.4f)] public float edgeZoneVertical = 0.1f;
        public float edgeSpeed = 110f;
        public float keySpeed = 90f;
        public float smoothTime = 0.12f;
        public float lookAtTime = 0.35f;
        [Tooltip("Кресло поворачивается вместе с игроком.")]
        public Transform chair;

        [Header("Приближение")]
        public float focusTime = 0.55f;
        public float pickDistance = 15f;

        [Header("Утро")]
        [Tooltip("Насколько опущена голова, пока глаза закрыты.")]
        public float wakePitch = -22f;
        public float wakeRiseTime = 0.45f;

        // Позиция указателя в пикселях экрана для автотестов; null — настоящая мышь.
        public Vector2? PointerOverride { get; set; }

        // Поверх игры открыт 2D-экран (WebGL: окно демо) — мышь и клавиши его, не камеры.
        public bool InputBlocked { get; set; }

        public bool IsFree => state == State.Free;
        public bool IsFocused => state == State.MovingIn || state == State.Focused;
        public Interactable Hovered => hovered;
        public Interactable Focused => focused;
        public float Yaw => yaw;
        public float Pitch => pitch;

        enum State { Free, MovingIn, Focused, MovingOut }

        Camera cam;
        State state;
        Vector3 seatPos;
        float yaw, pitch, targetYaw, targetPitch, yawVel, pitchVel;
        float homeYaw, homePitch, riseDelay;
        bool turning, rising;
        Interactable hovered, focused;
        Vector3 fromPos, toPos;
        Quaternion fromRot, toRot;
        float moveT;
        Vector2 startMouse;
        bool mouseMoved;

        Vector2 Pointer => PointerOverride ?? (Vector2)Input.mousePosition;

        // Пока мышь ни разу не двигалась над игрой (в браузере до первого движения она в 0,0 —
        // в углу экрана), края экрана не крутят камеру и предметы не подсвечиваются.
        bool PointerLive
        {
            get
            {
                if (PointerOverride.HasValue) return true;
                if (!mouseMoved && (Vector2)Input.mousePosition != startMouse) mouseMoved = true;
                return mouseMoved;
            }
        }

        Quaternion SeatRotation => Quaternion.Euler(-pitch, yaw, 0f);

        void Awake()
        {
            Instance = this;
            cam = GetComponent<Camera>();
            seatPos = transform.position;
            yaw = targetYaw = homeYaw = Mathf.DeltaAngle(0f, transform.eulerAngles.y);
            pitch = targetPitch = homePitch = -Mathf.DeltaAngle(0f, transform.eulerAngles.x);
            startMouse = Input.mousePosition;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            switch (state)
            {
                case State.Free:
                    UpdateLook();
                    UpdatePointer();
                    break;
                case State.MovingIn:
                case State.Focused:
                    if (!InputBlocked && (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1))) Release();
                    break;
            }
            if (state == State.MovingIn || state == State.MovingOut) UpdateMove();
        }

        void UpdateLook()
        {
            bool live = PointerLive && !InputBlocked;
            Vector2 edge = live && (Application.isFocused || PointerOverride.HasValue)
                ? EdgeInput(Pointer, new Vector2(Screen.width, Screen.height), edgeZone, edgeZoneVertical)
                : Vector2.zero;
            float keys = 0f;
            if (!InputBlocked)
            {
                if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) keys -= 1f;
                if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) keys += 1f;
            }

            float dt = Time.deltaTime;
            if (riseDelay > 0f)
            {
                // Голова еще у стола: ждем, пока глаза начнут открываться.
                riseDelay -= dt;
                if (riseDelay <= 0f) targetPitch = homePitch;
            }
            else
            {
                targetYaw = Mathf.Clamp(targetYaw + (edge.x * edgeSpeed + keys * keySpeed) * dt, yawMin, yawMax);
                targetPitch = Mathf.Clamp(targetPitch + edge.y * edgeSpeed * 0.5f * dt, -pitchLimit, pitchLimit);
            }

            if (turning && Mathf.Abs(targetYaw - yaw) < 1f) turning = false;
            if (rising && riseDelay <= 0f && Mathf.Abs(targetPitch - pitch) < 0.5f) rising = false;
            yaw = Mathf.SmoothDamp(yaw, targetYaw, ref yawVel, turning ? lookAtTime : smoothTime);
            pitch = Mathf.SmoothDamp(pitch, targetPitch, ref pitchVel, rising ? wakeRiseTime : smoothTime);
            transform.SetPositionAndRotation(seatPos, SeatRotation);
            if (chair) chair.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        void UpdatePointer()
        {
            Interactable hit = null;
            Vector2 p = Pointer;
            bool onScreen = p.x >= 0f && p.y >= 0f && p.x <= Screen.width && p.y <= Screen.height;
            bool overUi = OfficeHud.Instance && OfficeHud.Instance.IsPointerOverUI(p);
            if (PointerLive && !InputBlocked && onScreen && !overUi && !turning &&
                Physics.Raycast(cam.ScreenPointToRay(p), out var info, pickDistance, ~0, QueryTriggerInteraction.Ignore))
                hit = info.collider.GetComponentInParent<Interactable>();

            SetHovered(hit);
            if (hovered && Input.GetMouseButtonDown(0)) hovered.Activate();
        }

        void SetHovered(Interactable target)
        {
            if (target == hovered) return;
            if (hovered) hovered.SetHovered(false);
            hovered = target;
            if (hovered) hovered.SetHovered(true);
        }

        // Плавно повернуть голову к заданным углам (в пределах yawMin…yawMax):
        // например, к предмету, о котором напомнила игра.
        public void LookAt(float yawDeg, float pitchDeg)
        {
            if (state != State.Free) return;
            targetYaw = Mathf.Clamp(Mathf.DeltaAngle(0f, yawDeg), yawMin, yawMax);
            targetPitch = Mathf.Clamp(pitchDeg, -pitchLimit, pitchLimit);
            turning = true;
        }

        public void FocusOn(Interactable target)
        {
            if (state != State.Free || !target) return;
            SetHovered(null);
            focused = target;
            target.GetFocusPose(seatPos, out toPos, out toRot);
            BeginMove(State.MovingIn);
        }

        // Начало дня: камера сразу в кресле, взгляд на стол, голова опущена;
        // через delay секунд голова поднимается к обычному обзору.
        public void Wake(float delay)
        {
            SetHovered(null);
            if (focused) focused.NotifyReleased();
            focused = null;
            state = State.Free;
            turning = false;
            rising = true;
            riseDelay = Mathf.Max(delay, 0.01f);
            yaw = targetYaw = homeYaw;
            pitch = targetPitch = homePitch + wakePitch;
            yawVel = pitchVel = 0f;
            transform.SetPositionAndRotation(seatPos, SeatRotation);
            if (chair) chair.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        public void Release()
        {
            if (!IsFocused) return;
            if (focused) focused.NotifyReleased();
            BeginMove(State.MovingOut);
        }

        void BeginMove(State next)
        {
            fromPos = transform.position;
            fromRot = transform.rotation;
            moveT = 0f;
            state = next;
        }

        void UpdateMove()
        {
            moveT = Mathf.Min(1f, moveT + Time.deltaTime / focusTime);
            float k = Mathf.SmoothStep(0f, 1f, moveT);
            bool back = state == State.MovingOut;
            transform.SetPositionAndRotation(
                Vector3.Lerp(fromPos, back ? seatPos : toPos, k),
                Quaternion.Slerp(fromRot, back ? SeatRotation : toRot, k));
            if (moveT < 1f) return;

            if (back)
            {
                state = State.Free;
                focused = null;
            }
            else
            {
                state = State.Focused;
                focused.NotifyFocused();
            }
        }

        // Сила поворота по осям (-1..1), когда указатель у края экрана. Вне экрана — ноль.
        public static Vector2 EdgeInput(Vector2 pointer, Vector2 screen, float zoneX, float zoneY)
        {
            if (pointer.x < 0f || pointer.y < 0f || pointer.x > screen.x || pointer.y > screen.y) return Vector2.zero;
            return new Vector2(
                EdgeAxis(pointer.x, screen.x, screen.x * zoneX),
                EdgeAxis(pointer.y, screen.y, screen.y * zoneY));
        }

        static float EdgeAxis(float v, float size, float zone)
        {
            if (v < zone) return -Sq(1f - v / zone);
            if (v > size - zone) return Sq(1f - (size - v) / zone);
            return 0f;
        }

        static float Sq(float t) => t * t;
    }
}

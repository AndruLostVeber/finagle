using UnityEngine;

namespace Kuro
{
    // Настенные часы: секундная стрелка тикает, минутная и часовая идут с 9:00 от начала дня.
    // Рабочее время идет быстрее настоящего (minutesPerSecond).
    public class OfficeClock : MonoBehaviour
    {
        public Transform hour, minute, second;
        public float startHour = 9f;
        [Tooltip("Минут рабочего времени за секунду игры.")]
        public float minutesPerSecond = 0.5f;

        float dayStart;

        void OnEnable() => OfficeState.DayStarted += ResetDay;
        void OnDisable() => OfficeState.DayStarted -= ResetDay;

        void Start() => ResetDay();

        void ResetDay() => dayStart = Time.time;

        void Update()
        {
            float real = Time.time - dayStart;
            // Тик: стрелка прыгает в начале секунды и чуть пружинит.
            float frac = real - Mathf.Floor(real);
            float tick = Mathf.Floor(real) + Mathf.Clamp01(frac / 0.08f) + Mathf.Sin(Mathf.Clamp01(frac / 0.25f) * Mathf.PI) * 0.08f;
            Set(second, tick * 6f);
            float minutes = real * minutesPerSecond;
            Set(minute, minutes * 6f);
            Set(hour, (startHour + minutes / 60f) * 30f);
        }

        // Угол по часовой от 12, если смотреть на циферблат.
        static void Set(Transform hand, float angle)
        {
            if (hand) hand.localRotation = Quaternion.Euler(0f, 0f, -angle);
        }
    }
}

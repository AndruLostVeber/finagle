using System;

namespace Kuro
{
    // Что сейчас в игре (в WebGL присылает демо через WebBridge): непрочитанные сообщения
    // и открытые задачи дня. На это смотрят телефон (PhoneAlert) и лоток «Входящие» (InboxStack).
    public static class OfficeState
    {
        public static int Unread { get; private set; }
        public static int Tasks { get; private set; } = 3;

        public static event Action Changed;
        // Начался новый день (глаза открылись): часы снова показывают 9:00.
        public static event Action DayStarted;

        public static void Set(int unread, int tasks)
        {
            if (unread == Unread && tasks == Tasks) return;
            Unread = unread;
            Tasks = tasks;
            Changed?.Invoke();
        }

        public static void StartDay() => DayStarted?.Invoke();
    }
}

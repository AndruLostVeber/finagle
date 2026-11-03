using System.Runtime.InteropServices;
using UnityEngine;

namespace Kuro
{
    // Связь с веб-страницей в WebGL-сборке. Камера подлетела к предмету — страница открывает
    // его окно в демо (Interactable.webAction). Подпись предмета под мышью тоже рисует страница:
    // текст UI Toolkit в WebGL не отображается. Страница сообщает обратно:
    // SetBlocked("1"/"0") — демо поверх игры или снова свободный офис; OnWebClosed — окно закрыто;
    // Wake(delay) — начался день; SetOffice("непрочитанные,задачи") — для телефона и лотка;
    // PlayArrival / SkipArrival — ролик прихода в деловой центр (конец ролика — KuroArrived).
    // В редакторе и других сборках ничего не делает.
    public class WebBridge : MonoBehaviour
    {
        public static bool Active { get; private set; }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void KuroOpen(string action);
        [DllImport("__Internal")] static extern void KuroHover(string label);
        [DllImport("__Internal")] static extern void KuroArrived();
#else
        static void KuroOpen(string action) { }
        static void KuroHover(string label) { }
        static void KuroArrived() { }
#endif

        string shownLabel = "";

        void Awake()
        {
            Active = Application.platform == RuntimePlatform.WebGLPlayer;
            // Пока открыто окно демо, фокус во встроенной странице — без этого Unity встает на паузу
            // (замирают ролик прихода, телефон, сквозняк).
            if (Active) Application.runInBackground = true;
        }

        void Start()
        {
            if (!Active) return;
            // Пока страница не сказала иначе, поверх игры демо (титульный экран).
            if (SeatedLook.Instance) SeatedLook.Instance.InputBlocked = true;
            foreach (var it in FindObjectsByType<Interactable>(FindObjectsInactive.Include))
            {
                var item = it;
                item.Focused += () => Open(item.webAction);
            }
        }

        void Update()
        {
            var look = SeatedLook.Instance;
            if (!Active || !look) return;
            // Esc в свободном офисе — меню паузы демо.
            if (look.IsFree && !look.InputBlocked && Input.GetKeyDown(KeyCode.Escape)) Open("pause");

            string label = look.IsFree && look.Hovered ? look.Hovered.label : "";
            if (label == shownLabel) return;
            shownLabel = label;
            KuroHover(label);
        }

        static void Open(string action)
        {
            if (Active && !string.IsNullOrEmpty(action)) KuroOpen(action);
        }

        public void SetBlocked(string on)
        {
            if (SeatedLook.Instance) SeatedLook.Instance.InputBlocked = on == "1";
        }

        public void OnWebClosed()
        {
            if (SeatedLook.Instance) SeatedLook.Instance.Release();
        }

        // Начало дня: страница открывает глаза, голова поднимается от стола через delay секунд.
        public void Wake(float delay)
        {
            if (ArrivalCinematic.Instance) ArrivalCinematic.Instance.Finish();
            if (SeatedLook.Instance) SeatedLook.Instance.Wake(delay);
            OfficeState.StartDay();
        }

        public void PlayArrival()
        {
            if (ArrivalCinematic.Instance) ArrivalCinematic.Instance.Play();
            else KuroArrived();
        }

        public void SkipArrival()
        {
            if (ArrivalCinematic.Instance) ArrivalCinematic.Instance.Skip();
        }

        public static void Arrived() => KuroArrived();

        public void SetOffice(string data)
        {
            var parts = data.Split(',');
            if (parts.Length == 2 && int.TryParse(parts[0], out int unread) && int.TryParse(parts[1], out int tasks))
                OfficeState.Set(unread, tasks);
        }
    }
}

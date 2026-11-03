using UnityEngine;

namespace Kuro
{
    // Телефон на столе: пришло сообщение — вибрирует, звенит, экран и уведомление горят,
    // над телефоном светится отблеск. Пока сообщения не прочитаны — экран пульсирует
    // и телефон изредка вздрагивает. Прочитали (открыли телефон в демо) — гаснет.
    public class PhoneAlert : MonoBehaviour
    {
        [Tooltip("Что трясется при вибрации (корень предмета не двигаем: на нем коллайдер).")]
        public Transform body;
        public Renderer screen;
        public Renderer notice;
        public Light glow;
        public Color screenColor = new Color(0.55f, 0.85f, 1f);
        public Color noticeColor = new Color(1f, 0.7f, 0.25f);
        [Tooltip("Сколько секунд звонит после нового сообщения.")]
        public float ringTime = 8f;
        [Range(0f, 1f)] public float volume = 0.35f;

        AudioSource source;
        AudioClip buzz, chime;
        MaterialPropertyBlock block;
        Vector3 restPos;
        Quaternion restRot;
        int unread;
        float ringUntil, nextBuzz, buzzStart = -10f, lit;
        float glowBase;
        static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        void OnEnable() => OfficeState.Changed += Refresh;
        void OnDisable() => OfficeState.Changed -= Refresh;

        void Start()
        {
            block = new MaterialPropertyBlock();
            if (body)
            {
                restPos = body.localPosition;
                restRot = body.localRotation;
            }
            if (glow) glowBase = glow.intensity;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0.4f;
            source.volume = volume;
            buzz = MakeBuzz();
            chime = MakeChime();
            unread = OfficeState.Unread;
            if (unread > 0) ringUntil = Time.time + ringTime;
        }

        void Refresh()
        {
            int n = OfficeState.Unread;
            if (n > unread)
            {
                ringUntil = Time.time + ringTime;
                nextBuzz = Time.time + 0.15f;
                if (chime) source.PlayOneShot(chime, 0.8f);
            }
            unread = n;
        }

        void Update()
        {
            float now = Time.time;
            bool pending = unread > 0;
            bool ringing = pending && now < ringUntil;
            if (pending && now >= nextBuzz)
            {
                buzzStart = now;
                nextBuzz = now + (ringing ? 2f : 14f);
                if (buzz) source.PlayOneShot(buzz, ringing ? 1f : 0.5f);
            }

            // Вибрация: 0,35 с жужжит, 0,15 с пауза, снова 0,35 с — как звук buzz.
            float since = now - buzzStart;
            bool shaking = since < 0.85f && !(since > 0.35f && since < 0.5f);
            if (body)
            {
                float s = shaking ? 1f : 0f;
                body.localPosition = restPos + s * new Vector3(Mathf.Sin(now * 97f) * 0.0011f, 0f, Mathf.Cos(now * 83f) * 0.0011f);
                body.localRotation = restRot * Quaternion.Euler(0f, s * Mathf.Sin(now * 61f) * 1.4f, 0f);
            }

            float pulse = 0.5f + 0.5f * Mathf.Sin(now * (ringing ? 7f : 2.2f));
            float target = pending ? (ringing ? 0.75f + 0.25f * pulse : 0.35f + 0.25f * pulse) : 0.03f;
            lit = Mathf.MoveTowards(lit, target, Time.deltaTime * 3f);
            SetEmission(screen, screenColor * (lit * 1.6f));
            if (notice)
            {
                notice.enabled = pending;
                SetEmission(notice, noticeColor * (pending ? 0.6f + 0.9f * pulse : 0f));
            }
            if (glow)
            {
                glow.intensity = glowBase * (pending ? lit * (shaking ? 1.4f : 1f) : 0f);
                glow.enabled = glow.intensity > 0.01f;
            }
        }

        void SetEmission(Renderer r, Color c)
        {
            if (!r) return;
            r.GetPropertyBlock(block);
            block.SetColor(EmissionId, c);
            r.SetPropertyBlock(block);
        }

        // Жужжание вибромотора: низкий тон с дребезгом, две посылки.
        static AudioClip MakeBuzz()
        {
            const int rate = 22050;
            int n = (int)(rate * 0.85f);
            var data = new float[n];
            var rnd = new System.Random(3);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                bool on = t < 0.35f || (t > 0.5f && t < 0.85f);
                if (!on) continue;
                float local = t < 0.35f ? t : t - 0.5f;
                float env = Mathf.Clamp01(local / 0.02f) * Mathf.Clamp01((0.35f - local) / 0.03f);
                float tone = Mathf.Sin(2f * Mathf.PI * 165f * t) * (0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 33f * t));
                float rattle = ((float)rnd.NextDouble() * 2f - 1f) * 0.25f * Mathf.Abs(Mathf.Sin(2f * Mathf.PI * 165f * t));
                data[i] = (tone + rattle) * env * 0.5f;
            }
            var clip = AudioClip.Create("PhoneBuzz", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Короткий сигнал сообщения: две ноты, ми и ля.
        static AudioClip MakeChime()
        {
            const int rate = 22050;
            int n = (int)(rate * 0.55f);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                float f = t < 0.14f ? 1318.5f : 1760f;
                float local = t < 0.14f ? t : t - 0.14f;
                float env = Mathf.Clamp01(local / 0.005f) * Mathf.Exp(-local * (t < 0.14f ? 14f : 7f));
                data[i] = (Mathf.Sin(2f * Mathf.PI * f * t) + 0.25f * Mathf.Sin(4f * Mathf.PI * f * t)) * env * 0.35f;
            }
            var clip = AudioClip.Create("PhoneChime", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}

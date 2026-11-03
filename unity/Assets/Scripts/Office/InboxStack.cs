using UnityEngine;

namespace Kuro
{
    // Лоток «Входящие»: листов в стопке столько, сколько открытых задач дня (OfficeState.Tasks).
    // Новые листы ложатся сверху, сделанные уезжают со стола. Верхний лист шевелит сквозняк.
    public class InboxStack : MonoBehaviour
    {
        [Tooltip("Листы снизу вверх.")]
        public Transform[] papers;

        Vector3[] rest;
        Quaternion[] restRot;
        float[] shown;
        Renderer[] renderers;
        WindSheet[] flutter;

        void Start()
        {
            rest = new Vector3[papers.Length];
            restRot = new Quaternion[papers.Length];
            shown = new float[papers.Length];
            renderers = new Renderer[papers.Length];
            flutter = new WindSheet[papers.Length];
            int n = Count();
            for (int i = 0; i < papers.Length; i++)
            {
                rest[i] = papers[i].localPosition;
                restRot[i] = papers[i].localRotation;
                renderers[i] = papers[i].GetComponent<Renderer>();
                flutter[i] = papers[i].GetComponent<WindSheet>();
                shown[i] = i < n ? 1f : 0f;
            }
            Apply();
        }

        int Count() => Mathf.Clamp(OfficeState.Tasks, 0, papers.Length);

        void Update()
        {
            int n = Count();
            for (int i = 0; i < papers.Length; i++)
            {
                // Листы приходят снизу вверх и уходят сверху вниз — по одному.
                float target = i < n ? 1f : 0f;
                if (target > shown[i] && i > 0 && shown[i - 1] < 0.5f) continue;
                if (target < shown[i] && i < papers.Length - 1 && shown[i + 1] > 0.5f) continue;
                shown[i] = Mathf.MoveTowards(shown[i], target, Time.deltaTime * 2.2f);
            }
            Apply();
        }

        void Apply()
        {
            int top = -1;
            for (int i = 0; i < papers.Length; i++)
            {
                float s = shown[i];
                float e = s * s * (3f - 2f * s);
                papers[i].localPosition = rest[i] + new Vector3(0.02f, 0.05f, -0.32f) * (1f - e);
                papers[i].localRotation = Quaternion.Euler(0f, (1f - e) * -25f, 0f) * restRot[i];
                if (renderers[i]) renderers[i].enabled = s > 0.02f;
                if (s > 0.98f) top = i;
            }
            for (int i = 0; i < papers.Length; i++)
                if (flutter[i]) flutter[i].enabled = i == top;
        }
    }
}

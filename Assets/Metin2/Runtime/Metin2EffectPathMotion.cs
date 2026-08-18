using UnityEngine;

namespace Metin2Dev.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class Metin2EffectPathMotion : MonoBehaviour
    {
        public Vector3[] positions;
        public float[] times;
        public float startDelay;
        public bool loop;

        float elapsed;

        void LateUpdate()
        {
            if (positions == null || times == null || positions.Length == 0 || positions.Length != times.Length) return;
            elapsed += Time.deltaTime;
            if (elapsed < startDelay) return;
            float time = elapsed - startDelay;
            float end = times[times.Length - 1];
            if (loop && end > 0f) time %= end;
            else time = Mathf.Min(time, end);

            int next = 1;
            while (next < times.Length && time > times[next]) next++;
            if (next >= times.Length) { transform.localPosition = positions[positions.Length - 1]; return; }
            float segment = Mathf.InverseLerp(times[next - 1], times[next], time);
            transform.localPosition = Vector3.LerpUnclamped(positions[next - 1], positions[next], segment);
        }
    }
}

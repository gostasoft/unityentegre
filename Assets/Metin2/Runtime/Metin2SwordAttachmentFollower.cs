using UnityEngine;

namespace Metin2Dev.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class Metin2SwordAttachmentFollower : MonoBehaviour
    {
        public Metin2SwordAttachmentSettings settings;

        public void Apply()
        {
            if (settings == null) return;
            transform.SetLocalPositionAndRotation(settings.LocalPosition, Quaternion.Euler(settings.LocalEulerAngles));
            transform.localScale = settings.localScale;
        }

        void LateUpdate()
        {
            // ScriptableObject inspector edits are reflected immediately while Play mode is running.
            Apply();
        }
    }
}

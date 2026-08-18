using UnityEngine;

namespace Metin2Dev.Gameplay
{
    [CreateAssetMenu(menuName = "Metin2/Sword Attachment Settings", fileName = "Metin2SwordAttachmentSettings")]
    public sealed class Metin2SwordAttachmentSettings : ScriptableObject
    {
        [Header("Applied directly under Bip01 R Hand")]
        [Header("Kabza Konumu")]
        [Tooltip("Kabzayı elden aşağı/yukarı taşır.")]
        public float verticalOffset;
        [Tooltip("Kabzayı elde sağa/sola taşır.")]
        public float horizontalOffset;
        [Tooltip("Kabzayı ele doğru/elden dışarı taşır.")]
        public float depthOffset;

        [Header("Kılıç Dönüşü")]
        [Tooltip("Dikey eğim (X).")]
        public float verticalRotation;
        [Tooltip("Yatay yön (Y).")]
        public float horizontalRotation;
        [Tooltip("Kendi ekseninde dönüş (Z).")]
        public float rollRotation = 90f;
        public Vector3 localScale = Vector3.one;

        public Vector3 LocalPosition => new Vector3(horizontalOffset, verticalOffset, depthOffset);
        public Vector3 LocalEulerAngles => new Vector3(verticalRotation, horizontalRotation, rollRotation);
    }
}

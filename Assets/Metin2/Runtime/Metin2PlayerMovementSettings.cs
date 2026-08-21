using UnityEngine;

namespace Metin2Dev.Gameplay
{
    [CreateAssetMenu(menuName = "Metin2/Player Movement Settings", fileName = "Metin2PlayerMovementSettings")]
    public sealed class Metin2PlayerMovementSettings : ScriptableObject
    {
        [Min(0f)] public float walkSpeedMultiplier = 2f;
        [Min(0f)] public float runSpeedMultiplier = 3f;
    }
}

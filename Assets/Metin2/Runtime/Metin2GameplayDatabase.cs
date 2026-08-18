using System.Collections.Generic;
using UnityEngine;

namespace Metin2Dev.Gameplay
{
    // Unity needs the ScriptableObject's file name to match this type to serialize its script reference.
    [CreateAssetMenu(menuName = "Metin2/Gameplay Database", fileName = "Metin2GameplayDatabase")]
    public sealed class Metin2GameplayDatabase : ScriptableObject
    {
        public List<Metin2RaceMotionSet> races = new List<Metin2RaceMotionSet>();

        public Metin2RaceMotionSet Find(Metin2Dev.Frontend.Metin2CharacterClass characterClass,
            Metin2Dev.Frontend.Metin2Gender gender)
        {
            return races.Find(item => item != null && item.characterClass == characterClass && item.gender == gender);
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Metin2Dev.Gameplay
{
    [Serializable]
    public sealed class Metin2MotionEvent
    {
        public int type;
        public float startTime;
        public float duration;
        public string attachingBone;
        public bool attachToBone;
        public bool followAttachment;
        public Vector3 position;
        public float radius;
        public int hitLimit;
        public GameObject effectPrefab;
    }

    [Serializable]
    public sealed class Metin2MotionRecord
    {
        public string mode;
        public string name;
        public AnimationClip clip;
        public float duration;
        public Vector3 accumulation;
        public float preInputTime = -1f;
        public float directInputTime = -1f;
        public float inputLimitTime = -1f;
        public float linkTime = -1f;
        public float attackStartTime = -1f;
        public float attackEndTime = -1f;
        public float weaponLength;
        public string sourceMsa;
        public List<Metin2MotionEvent> events = new List<Metin2MotionEvent>();

        public bool IsLoop => name == "wait" || name == "wait_1" || name == "wait_2" ||
                              name == "walk" || name == "run" || name == "fishing_wait";
    }

    [Serializable]
    public sealed class Metin2RaceMotionSet
    {
        public Metin2Dev.Frontend.Metin2CharacterClass characterClass;
        public Metin2Dev.Frontend.Metin2Gender gender;
        public string sourcePack;
        public GameObject playerPrefab;
        public RuntimeAnimatorController animatorController;
        public List<Metin2MotionRecord> motions = new List<Metin2MotionRecord>();

        public Metin2MotionRecord Find(string mode, string name)
        {
            return motions.Find(item => item != null &&
                string.Equals(item.mode, mode, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.name, name, StringComparison.OrdinalIgnoreCase));
        }

        public Metin2MotionRecord FindAny(string name)
        {
            return motions.Find(item => item != null && string.Equals(item.name, name, StringComparison.OrdinalIgnoreCase));
        }
    }

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

using System;
using UnityEngine;

namespace Metin2Dev.Frontend
{
    public enum Metin2Empire
    {
        None = 0,
        Shinsoo = 1,
        Chunjo = 2,
        Jinno = 3,
    }

    public enum Metin2CharacterClass
    {
        Warrior = 0,
        Assassin = 1,
        Sura = 2,
        Shaman = 3,
    }

    public enum Metin2Gender
    {
        Male = 0,
        Female = 1,
    }

    [Serializable]
    public sealed class Metin2CharacterData
    {
        public string characterName;
        public Metin2CharacterClass characterClass;
        public Metin2Gender gender;
        public int level = 1;
        public int playMinutes;
        public int vitality;
        public int intelligence;
        public int strength;
        public int dexterity;
    }

    [Serializable]
    public sealed class Metin2FrontendSaveData
    {
        public string accountId;
        public Metin2Empire empire;
        public Metin2CharacterData[] characters = new Metin2CharacterData[4];

        public void EnsureSlots()
        {
            if (characters == null || characters.Length != 4)
            {
                Metin2CharacterData[] replacement = new Metin2CharacterData[4];
                if (characters != null)
                    Array.Copy(characters, replacement, Mathf.Min(characters.Length, replacement.Length));
                characters = replacement;
            }
        }
    }

    [CreateAssetMenu(menuName = "Metin2/Frontend Config", fileName = "Metin2FrontendConfig")]
    public sealed class Metin2FrontendConfig : ScriptableObject
    {
        [Header("Original client backgrounds")]
        public Texture2D loginBackground;
        public Texture2D serverBackground;
        public Texture2D selectionBackground;
        public Texture2D empireMap;
        public Texture2D[] loadingBackgrounds = new Texture2D[4];

        [Header("Original client UI patterns")]
        public Sprite inventoryBoardFrame;
        public Sprite inventoryBoardCenter;

        [Header("Preview rendering")]
        public Shader previewShader;

        [Header("Race previews: class * 2 + gender")]
        public GameObject[] racePrefabs = new GameObject[8];
        public GameObject[] hairPrefabs = new GameObject[8];
        public Texture2D[] bodyTextures = new Texture2D[8];
        public Texture2D[] faceTextures = new Texture2D[8];
        public Texture2D[] hairTextures = new Texture2D[8];

        [Header("Starting scenes")]
        public string shinsooScene = "metin2_map_a1";
        public string chunjoScene = "metin2_map_b1";
        public string jinnoScene = "metin2_map_c1";

        public int RaceIndex(Metin2CharacterClass characterClass, Metin2Gender gender)
        {
            return ((int)characterClass * 2) + (int)gender;
        }

        public GameObject GetRacePrefab(Metin2CharacterClass characterClass, Metin2Gender gender)
        {
            int index = RaceIndex(characterClass, gender);
            return racePrefabs != null && index >= 0 && index < racePrefabs.Length ? racePrefabs[index] : null;
        }

        public GameObject GetHairPrefab(Metin2CharacterClass characterClass, Metin2Gender gender)
        {
            int index = RaceIndex(characterClass, gender);
            return hairPrefabs != null && index >= 0 && index < hairPrefabs.Length ? hairPrefabs[index] : null;
        }

        public Texture2D GetBodyTexture(Metin2CharacterClass characterClass, Metin2Gender gender)
        {
            return GetTexture(bodyTextures, RaceIndex(characterClass, gender));
        }

        public Texture2D GetFaceTexture(Metin2CharacterClass characterClass, Metin2Gender gender)
        {
            return GetTexture(faceTextures, RaceIndex(characterClass, gender));
        }

        public Texture2D GetHairTexture(Metin2CharacterClass characterClass, Metin2Gender gender)
        {
            return GetTexture(hairTextures, RaceIndex(characterClass, gender));
        }

        public string GetScene(Metin2Empire empire)
        {
            switch (empire)
            {
                case Metin2Empire.Shinsoo: return shinsooScene;
                case Metin2Empire.Chunjo: return chunjoScene;
                case Metin2Empire.Jinno: return jinnoScene;
                default: return shinsooScene;
            }
        }

        static Texture2D GetTexture(Texture2D[] textures, int index)
        {
            return textures != null && index >= 0 && index < textures.Length ? textures[index] : null;
        }
    }
}

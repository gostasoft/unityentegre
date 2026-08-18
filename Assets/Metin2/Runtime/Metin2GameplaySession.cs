using UnityEngine;
using Metin2Dev.Frontend;

namespace Metin2Dev.Gameplay
{
    public static class Metin2GameplaySession
    {
        public static bool HasCharacter { get; private set; }
        public static string CharacterName { get; private set; }
        public static Metin2CharacterClass CharacterClass { get; private set; }
        public static Metin2Gender Gender { get; private set; }
        public static Metin2Empire Empire { get; private set; }
        public static int Level { get; private set; }
        public static int PlayMinutes { get; private set; }
        public static int Vitality { get; private set; }
        public static int Intelligence { get; private set; }
        public static int Strength { get; private set; }
        public static int Dexterity { get; private set; }
        public static GameObject PlayerPrefab { get; private set; }
        public static GameObject HairPrefab { get; private set; }
        public static Texture2D BodyTexture { get; private set; }
        public static Texture2D FaceTexture { get; private set; }
        public static Texture2D HairTexture { get; private set; }

        public static void Select(Metin2CharacterData character, Metin2Empire empire, GameObject playerPrefab,
            GameObject hairPrefab, Texture2D bodyTexture, Texture2D faceTexture, Texture2D hairTexture)
        {
            if (character == null) return;
            HasCharacter = true;
            CharacterName = character.characterName;
            CharacterClass = character.characterClass;
            Gender = character.gender;
            Empire = empire;
            Level = Mathf.Max(1, character.level);
            PlayMinutes = Mathf.Max(0, character.playMinutes);
            Vitality = Mathf.Max(1, character.vitality);
            Intelligence = Mathf.Max(1, character.intelligence);
            Strength = Mathf.Max(1, character.strength);
            Dexterity = Mathf.Max(1, character.dexterity);
            PlayerPrefab = playerPrefab;
            HairPrefab = hairPrefab;
            BodyTexture = bodyTexture;
            FaceTexture = faceTexture;
            HairTexture = hairTexture;
        }

        public static void UseEditorDefault()
        {
            if (HasCharacter) return;
            HasCharacter = true;
            CharacterName = "Oyuncu";
            CharacterClass = Metin2CharacterClass.Warrior;
            Gender = Metin2Gender.Male;
            Empire = Metin2Empire.Shinsoo;
            Level = 1;
            Vitality = 4;
            Intelligence = 3;
            Strength = 6;
            Dexterity = 3;
        }
    }
}

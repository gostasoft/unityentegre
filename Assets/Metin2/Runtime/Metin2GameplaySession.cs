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

        public static void Select(Metin2CharacterData character, Metin2Empire empire)
        {
            if (character == null) return;
            HasCharacter = true;
            CharacterName = character.characterName;
            CharacterClass = character.characterClass;
            Gender = character.gender;
            Empire = empire;
        }

        public static void UseEditorDefault()
        {
            if (HasCharacter) return;
            HasCharacter = true;
            CharacterName = "Oyuncu";
            CharacterClass = Metin2CharacterClass.Warrior;
            Gender = Metin2Gender.Male;
            Empire = Metin2Empire.Shinsoo;
        }
    }
}

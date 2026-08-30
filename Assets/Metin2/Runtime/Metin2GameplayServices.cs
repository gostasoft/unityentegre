using System;
using System.Collections.Generic;
using UnityEngine;

namespace Metin2Dev.Gameplay
{
    public enum Metin2ChatChannel { Talking, Party, Guild, Shout, Whisper, Info }

    [Serializable]
    public sealed class Metin2ChatEntry
    {
        public DateTime time;
        public Metin2ChatChannel channel;
        public string sender;
        public string text;
    }

    public static class Metin2ChatService
    {
        static readonly List<Metin2ChatEntry> entries = new List<Metin2ChatEntry>();
        public static IReadOnlyList<Metin2ChatEntry> Entries => entries;
        public static event Action<Metin2ChatEntry> MessageAdded;

        public static void Submit(string text, Metin2ChatChannel channel = Metin2ChatChannel.Talking)
        {
            text = (text ?? string.Empty).Trim();
            if (text.Length == 0) return;
            if (text.StartsWith("#")) { channel = Metin2ChatChannel.Party; text = text.Substring(1).TrimStart(); }
            else if (text.StartsWith("%")) { channel = Metin2ChatChannel.Guild; text = text.Substring(1).TrimStart(); }
            else if (text.StartsWith("!")) { channel = Metin2ChatChannel.Shout; text = text.Substring(1).TrimStart(); }
            else if (text.StartsWith("/w ", StringComparison.OrdinalIgnoreCase))
            {
                string[] whisper = text.Substring(3).Trim().Split(new[] { ' ' }, 2);
                if (whisper.Length < 2 || string.IsNullOrWhiteSpace(whisper[0]) || string.IsNullOrWhiteSpace(whisper[1]))
                {
                    Append(Metin2ChatChannel.Info, "Kullanım: /w oyuncu mesaj");
                    return;
                }
                Append(Metin2ChatChannel.Whisper, whisper[1], "-> " + whisper[0]);
                return;
            }
            Append(channel, text, Metin2GameplaySession.CharacterName);
        }

        public static void Append(Metin2ChatChannel channel, string text, string sender = "Sistem")
        {
            Metin2ChatEntry entry = new Metin2ChatEntry { time = DateTime.Now, channel = channel, sender = sender, text = text ?? string.Empty };
            entries.Add(entry);
            if (entries.Count > 200) entries.RemoveAt(0);
            MessageAdded?.Invoke(entry);
        }
    }

    public enum Metin2QuestObjectiveType { Kill, Level, Visit, Collect }

    [Serializable]
    public sealed class Metin2QuestState
    {
        public int id;
        public string title;
        public string description;
        public Metin2QuestObjectiveType objectiveType;
        public int targetVnum;
        public int required;
        public int progress;
        public int rewardExperience;
        public int rewardGold;
        public bool completed;
        public bool rewarded;
    }

    public static class Metin2QuestService
    {
        static readonly List<Metin2QuestState> quests = new List<Metin2QuestState>();
        static bool initialized;
        public static IReadOnlyList<Metin2QuestState> Quests { get { EnsureInitialized(); return quests; } }
        public static event Action Changed;

        public static void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;
            quests.Add(new Metin2QuestState { id = 1, title = "İlk Av", description = "Köy çevresindeki yaratıklardan 5 tane yen.", objectiveType = Metin2QuestObjectiveType.Kill, targetVnum = 0, required = 5, rewardExperience = 250, rewardGold = 1000 });
            quests.Add(new Metin2QuestState { id = 2, title = "Güçlen", description = "5. seviyeye ulaş.", objectiveType = Metin2QuestObjectiveType.Level, required = 5, rewardExperience = 500, rewardGold = 2500 });
        }

        public static void ReportKill(int vnum)
        {
            EnsureInitialized();
            foreach (Metin2QuestState quest in quests)
            {
                if (quest.completed || quest.objectiveType != Metin2QuestObjectiveType.Kill) continue;
                if (quest.targetVnum != 0 && quest.targetVnum != vnum) continue;
                quest.progress = Mathf.Min(quest.required, quest.progress + 1);
                CompleteIfReady(quest);
            }
            Changed?.Invoke();
        }

        public static void ReportLevel(int level)
        {
            EnsureInitialized();
            foreach (Metin2QuestState quest in quests)
            {
                if (quest.completed || quest.objectiveType != Metin2QuestObjectiveType.Level) continue;
                quest.progress = Mathf.Min(quest.required, level);
                CompleteIfReady(quest);
            }
            Changed?.Invoke();
        }

        static void CompleteIfReady(Metin2QuestState quest)
        {
            if (quest.progress < quest.required || quest.completed) return;
            quest.completed = true;
            Metin2ChatService.Append(Metin2ChatChannel.Info, "Görev tamamlandı: " + quest.title);
        }

        public static bool Claim(int id)
        {
            EnsureInitialized();
            Metin2QuestState quest = quests.Find(item => item.id == id);
            if (quest == null || !quest.completed || quest.rewarded || Metin2PlayerState.Local == null) return false;
            quest.rewarded = true;
            Metin2PlayerState.Local.GainExperience(quest.rewardExperience);
            Metin2PlayerState.Local.AddGold(quest.rewardGold);
            Changed?.Invoke();
            return true;
        }
    }

    [Serializable]
    public sealed class Metin2MessengerContact
    {
        public string name;
        public bool online;
    }

    public static class Metin2MessengerService
    {
        static readonly List<Metin2MessengerContact> contacts = new List<Metin2MessengerContact>();
        public static IReadOnlyList<Metin2MessengerContact> Contacts => contacts;
        public static event Action Changed;

        public static void Add(string name)
        {
            name = (name ?? string.Empty).Trim();
            if (name.Length == 0 || contacts.Exists(item => string.Equals(item.name, name, StringComparison.OrdinalIgnoreCase))) return;
            contacts.Add(new Metin2MessengerContact { name = name, online = false });
            Changed?.Invoke();
        }

        public static bool Remove(string name)
        {
            name = (name ?? string.Empty).Trim();
            int index = contacts.FindIndex(item => string.Equals(item.name, name, StringComparison.OrdinalIgnoreCase));
            if (index < 0) return false;
            contacts.RemoveAt(index);
            Changed?.Invoke();
            return true;
        }
    }
}

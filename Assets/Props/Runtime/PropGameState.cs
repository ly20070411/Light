using System;
using System.Collections.Generic;
using UnityEngine;
using Emerge.Battle;

namespace Emerge.Props
{
    // This state belongs to the player. CaptureJson/RestoreJson connect to the game's save system.
    public sealed class PropGameState : MonoBehaviour
    {
        [Serializable] public sealed class InventoryEntry
        {
            public string key;
            public string displayName;
            public int amount;
        }
        [Serializable] public sealed class Snapshot
        {
            public int version = 1;
            public List<InventoryEntry> inventory = new List<InventoryEntry>();
            public List<string> flags = new List<string>();
            public List<string> consumedInstances = new List<string>();
            public List<BattleAttemptSeed> battleAttempts = new List<BattleAttemptSeed>();
            public List<BattleVictoryRecord> battleVictories = new List<BattleVictoryRecord>();
        }
        [SerializeField] private List<InventoryEntry> inventory = new List<InventoryEntry>();
        [SerializeField] private List<string> flags = new List<string>();
        [SerializeField] private List<string> consumedInstances = new List<string>();
        [SerializeField] private List<BattleAttemptSeed> battleAttempts = new List<BattleAttemptSeed>();
        [SerializeField] private List<BattleVictoryRecord> battleVictories = new List<BattleVictoryRecord>();
        public IReadOnlyList<BattleVictoryRecord> BattleVictories => battleVictories;
        public int LockBattleSeed(string contextId, string encounterId, int proposedSeed)
        {
            var existing = battleAttempts.Find(x => x.contextId == contextId && x.encounterId == encounterId);
            if (existing != null) return existing.seed;
            battleAttempts.Add(new BattleAttemptSeed { contextId = contextId, encounterId = encounterId, seed = proposedSeed });
            Changed?.Invoke(); return proposedSeed;
        }
        public void CompleteBattleAttempt(string contextId, string encounterId)
        { if (battleAttempts.RemoveAll(x => x.contextId == contextId && x.encounterId == encounterId) > 0) Changed?.Invoke(); }
        public BattleVictoryRecord Victory(string encounterId, string balanceVersion)
            => battleVictories.Find(x => x.encounterId == encounterId && x.balanceVersion == balanceVersion);
        public void RecordBattleVictory(string encounterId, string balanceVersion, string sessionId, int rounds, int swiftRounds)
        {
            var record = Victory(encounterId, balanceVersion);
            if (record != null && record.lastSessionId == sessionId) return;
            if (record == null) { record = new BattleVictoryRecord { encounterId = encounterId, balanceVersion = balanceVersion }; battleVictories.Add(record); }
            record.wins++; record.lastRounds = rounds; record.bestRounds = record.bestRounds == 0 ? rounds : Math.Min(record.bestRounds, rounds); record.lastSessionId = sessionId;
            SetFlag("battle-achievement:first:" + balanceVersion + ":" + encounterId);
            if (rounds <= swiftRounds) SetFlag("battle-achievement:swift:" + balanceVersion + ":" + encounterId);
            Changed?.Invoke();
        }
        public IReadOnlyList<InventoryEntry> Inventory => inventory;
        public event Action Changed;
        public static event Action<PropGameState> Restored;
        public int Count(string key)
        {
            var entry = inventory.Find(item => item.key == key);
            return entry == null ? 0 : entry.amount;
        }
        public void AddItem(string key, string displayName, int amount)
        {
            if (string.IsNullOrWhiteSpace(key) || amount <= 0) return;
            var entry = inventory.Find(item => item.key == key);
            if (entry == null) { entry = new InventoryEntry { key = key, displayName = displayName }; inventory.Add(entry); }
            entry.amount += amount;
            Changed?.Invoke();
        }
        public bool RemoveItem(string key, int amount)
        {
            if (amount < 1 || Count(key) < amount) return false;
            var entry = inventory.Find(item => item.key == key);
            entry.amount -= amount;
            if (entry.amount == 0) inventory.Remove(entry);
            Changed?.Invoke();
            return true;
        }
        public bool HasFlag(string flag) => string.IsNullOrWhiteSpace(flag) || flags.Contains(flag);
        public void SetFlag(string flag)
        {
            if (string.IsNullOrWhiteSpace(flag) || flags.Contains(flag)) return;
            flags.Add(flag);
            Changed?.Invoke();
        }
        public bool IsConsumed(string instanceId) => !string.IsNullOrEmpty(instanceId) && consumedInstances.Contains(instanceId);
        public void MarkConsumed(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId) || consumedInstances.Contains(instanceId)) return;
            consumedInstances.Add(instanceId);
            Changed?.Invoke();
        }
        public Snapshot CaptureSnapshot() => JsonUtility.FromJson<Snapshot>(CaptureJson());
        public bool RestoreSnapshot(Snapshot saved) => IsValidSnapshot(saved) && RestoreJson(JsonUtility.ToJson(saved));
        public static bool IsValidSnapshot(Snapshot saved)
        {
            if (saved == null || saved.version != 1 || saved.inventory == null || saved.flags == null || saved.consumedInstances == null)
                return false;
            var keys = new HashSet<string>();
            foreach (var entry in saved.inventory)
                if (entry == null || string.IsNullOrWhiteSpace(entry.key) || entry.amount <= 0 || !keys.Add(entry.key)) return false;
            keys.Clear();
            foreach (string flag in saved.flags) if (string.IsNullOrWhiteSpace(flag) || !keys.Add(flag)) return false;
            keys.Clear();
            foreach (string id in saved.consumedInstances) if (string.IsNullOrWhiteSpace(id) || !keys.Add(id)) return false;
            keys.Clear();
            if (saved.battleAttempts != null) foreach (var attempt in saved.battleAttempts)
                if (attempt == null || string.IsNullOrWhiteSpace(attempt.contextId) || string.IsNullOrWhiteSpace(attempt.encounterId) || !keys.Add(attempt.contextId + "\n" + attempt.encounterId)) return false;
            keys.Clear();
            if (saved.battleVictories != null) foreach (var victory in saved.battleVictories)
                if (victory == null || string.IsNullOrWhiteSpace(victory.encounterId) || string.IsNullOrWhiteSpace(victory.balanceVersion) || string.IsNullOrWhiteSpace(victory.lastSessionId) ||
                    victory.wins < 1 || victory.lastRounds < 1 || victory.bestRounds < 1 || victory.bestRounds > victory.lastRounds || !keys.Add(victory.encounterId + "\n" + victory.balanceVersion)) return false;
            return true;
        }
        public string CaptureJson() => JsonUtility.ToJson(new Snapshot
        { inventory = inventory, flags = flags, consumedInstances = consumedInstances, battleAttempts = battleAttempts, battleVictories = battleVictories }, true);
        public bool RestoreJson(string json)
        {
            Snapshot saved;
            try { saved = JsonUtility.FromJson<Snapshot>(json); }
            catch (Exception) { return false; }
            if (!IsValidSnapshot(saved)) return false;
            inventory = saved.inventory;
            flags = new List<string>(new HashSet<string>(saved.flags));
            consumedInstances = new List<string>(new HashSet<string>(saved.consumedInstances));
            battleAttempts = saved.battleAttempts ?? new List<BattleAttemptSeed>();
            battleVictories = saved.battleVictories ?? new List<BattleVictoryRecord>();
            Changed?.Invoke();
            Restored?.Invoke(this);
            return true;
        }
    }
}

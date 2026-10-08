using System;
using System.Collections.Generic;
using System.Linq;
using Emerge.Checks;
using Emerge.Checks.Divination;
using UnityEngine;

namespace Emerge.Battle
{
    public enum PointBattlePhase { Casting, Player, Enemy, Victory, Defeat }
    public enum PointEnemyEffect { Attack, Shield, Heal, Strength, Vulnerability, Corrosion, Weaken }
    [Serializable] public sealed class PointEnemyMove
    {
        public string name = "攻击";
        public PointEnemyEffect effect;
        [Min(0)] public double amount = 3;
        [Min(1)] public int hits = 1;
    }
    [Serializable] public sealed class PointEnemyProfile
    {
        public string id, name;
        [Min(1)] public double health = 18;
        public Color color = new Color(.65f, .3f, .4f);
        public Sprite portrait;
        // Repeats in order. A telegraphed move never changes in response to the player's roll.
        public List<PointEnemyMove> pattern = new List<PointEnemyMove>();
        public bool Valid() => !string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name) &&
            PointBattleEngine.Finite(health) && health > 0 && pattern != null && pattern.Count > 0 &&
            pattern.All(m => m != null && !string.IsNullOrWhiteSpace(m.name) && Enum.IsDefined(typeof(PointEnemyEffect), m.effect) &&
                PointBattleEngine.Finite(m.amount) && m.amount >= 0 && m.hits >= 1 && m.hits <= 100);
    }
    [CreateAssetMenu(menuName = "Light/战斗/点数战斗配置")]
    public sealed class PointBattleDefinition : ScriptableObject
    {
        public const string ResourcePath = "Battle/PointBattleRules";
        public string balanceVersion = "v0.8-points";
        [Min(1)] public double playerHealth = 40;
        [Min(1)] public int actionCost = 2;
        [Min(0)] public int roundActionPoints = 3, carryLimit = 1;
        public bool temporaryActionStatuses = true;
        public List<PointEnemyProfile> enemies = new List<PointEnemyProfile>();
        public bool Valid() => !string.IsNullOrWhiteSpace(balanceVersion) && PointBattleEngine.Finite(playerHealth) && playerHealth > 0 &&
            actionCost == 2 && roundActionPoints >= 0 && carryLimit >= 0 && enemies != null && enemies.All(e => e != null && e.Valid()) &&
            enemies.Select(e => e.id).Distinct().Count() == enemies.Count;
        public PointEnemyProfile Profile(BattleEnemyDefinition enemy, int index)
        {
            var configured = enemies.Find(e => e.id == enemy.id);
            if (configured != null) return JsonUtility.FromJson<PointEnemyProfile>(JsonUtility.ToJson(configured));
            return new PointEnemyProfile { id = enemy.id, name = enemy.displayName, health = 16 + index * 2,
                portrait = enemy.battlePortrait, color = enemy.color, pattern = new List<PointEnemyMove> {
                    new PointEnemyMove { name = "试探攻击", amount = 3 },
                    new PointEnemyMove { name = "准备攻势", effect = PointEnemyEffect.Strength, amount = 1 },
                    new PointEnemyMove { name = "重击", amount = 5 } } };
        }
    }
    [Serializable] public sealed class PointEnemyState
    {
        public PointEnemyProfile profile;
        public double hp, shield, strength, vulnerability, attackReduction;
        public int corrosion, intentIndex;
        public bool Alive => hp > 0;
        public PointEnemyMove Intent => profile.pattern[intentIndex % profile.pattern.Count];
    }
    [Serializable] public sealed class PointBattleRoundAction
    {
        public PointCalculationResult calculation;
        public int rawCheckPoints, effectiveCheckPoints, uses;
        public bool available = true;
        public string unavailableReason;
    }
    [Serializable] public sealed class PointBattleSession
    {
        public int version = 1;
        public string sessionId, encounterId, contextId, name, victoryFlag, balanceVersion;
        public int seed, round, ap, carriedAP, enemyCursor, swiftVictoryRounds;
        public double hp, maximumHP, shield, damageBonus, vulnerability, weakness;
        public int corrosion, debuffsExpireRound;
        public bool outcomeApplied, temporaryActionStatuses;
        public int actionCost = 2, roundAP = 3, carryLimit = 1;
        public ActorCheckAttributes attributes;
        public string anchor;
        public List<FrozenPointItem> equipment = new List<FrozenPointItem>();
        public List<PointEnemyState> enemies = new List<PointEnemyState>();
        public List<PointBattleRoundAction> actions = new List<PointBattleRoundAction>();
        [SerializeReference] public DivinationRecord divination;
        public PointBattlePhase phase;
        public List<string> log = new List<string>();
    }
    [Serializable] public sealed class PointBattleSnapshot
    {
        [SerializeReference] public PointBattleSession session;
        [SerializeReference] public BattleReturnPoint returnPoint;
    }
}

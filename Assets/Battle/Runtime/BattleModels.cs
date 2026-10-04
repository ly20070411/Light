using System;
using System.Collections.Generic;
using Emerge.Checks.Divination;

namespace Emerge.Battle
{
    [Serializable] public sealed class BattlePlayerState
    {
        public int hp, mp, shield, regeneration, regenerationTicks, nextMana;
        public float reduction, weakness;
        public int burn, burnTicks, exposureUntilRound;
        public float exposure;
    }
    [Serializable] public sealed class EnemySkillUses { public string skillId; public int count, lastRound; }
    [Serializable] public sealed class BattleEnemyState
    {
        public string definitionId, intentSkillId;
        public int hp, maxHP, mp, shield, vulnerabilityHits;
        public float vulnerability, weakness;
        public bool silenced, stunned, determined;
        public bool charged;
        public List<EnemySkillUses> uses = new List<EnemySkillUses>();
    }
    [Serializable] public sealed class BattleAction
    {
        public int serial, targetIndex, score, value, regenerationTicks, round;
        public string skillId, skillName;
        public BattleFamily family;
        public BattleTarget target;
        public BattleEffect effect;
        public float power, multiplier;
        public bool appliesVulnerability, movingLine;
        public DivinationRecord divination;
    }
    [Serializable] public sealed class BattleSession
    {
        public int version = 1, seed, round = 1, actionSerial, enemyCursor;
        public string sessionId, encounterId, contextId, balanceVersion;
        public BattlePhase phase;
        public BattlePlayerState player = new BattlePlayerState();
        public List<BattleEnemyState> enemies = new List<BattleEnemyState>();
        [UnityEngine.SerializeReference] public BattleAction pending, lastAction;
        public bool outcomeApplied;
        public List<string> log = new List<string>();
        [UnityEngine.SerializeReference] public DivinationRecord roundDivination;
        public List<string> unlockedSkills = new List<string>();
        public List<EnemySkillUses> skillUses = new List<EnemySkillUses>();
        public List<EnemySkillUses> roundStartUses = new List<EnemySkillUses>();
    }
    [Serializable] public sealed class BattleSnapshot
    {
        public int version = 1;
        public string catalogPath = BattleCatalog.ResourcePath;
        [UnityEngine.SerializeReference] public BattleSession session;
        // Optional in version-1 saves created before the battle UI supported escape.
        [UnityEngine.SerializeReference] public BattleReturnPoint returnPoint;
    }
}

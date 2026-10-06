using System;
using System.Collections.Generic;
using Emerge.Checks.Divination;

namespace Emerge.Battle
{
    public enum BattleStatusKind { DamageUp, DamageReduction, DefenseBreak, IncomingUp, DamageDown, Burn, ShadowCurse, Thunder, Blind, Reflect, ManaOnHit, KillMana, ManaLock, Taunt, DefenseDown, HeavenMomentum }
    [Serializable] public sealed class BattleTimedStatus
    {
        public BattleStatusKind kind;
        public float power;
        public int rounds, count = 1;
    }
    public enum BattleSummonKind { Clone, GuNest, WindBlade }
    [Serializable] public sealed class BattleSummonState
    {
        public BattleSummonKind kind;
        public int remainingRounds, targetIndex = -1;
        public float power;
        public bool enhanced;
    }
    [Serializable] public sealed class BattlePlayerState
    {
        public int hp, mp, shield, regeneration, regenerationTicks, nextMana;
        public float reduction, weakness;
        public int burn, burnTicks, exposureUntilRound;
        public float exposure;
        public int shieldRounds, criticalTalentRound;
        public bool criticalCharge;
        public List<BattleTimedStatus> statuses = new List<BattleTimedStatus>();
    }
    [Serializable] public sealed class EnemySkillUses { public string skillId; public int count, lastRound; }
    [Serializable] public sealed class BattleEnemyState
    {
        public string definitionId, intentSkillId;
        public int hp, maxHP, mp, shield, vulnerabilityHits;
        public float vulnerability, weakness;
        public bool silenced, stunned, determined;
        public bool charged;
        public float defense;
        public List<BattleTimedStatus> statuses = new List<BattleTimedStatus>();
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
        public bool enhanced, critical, auspicious;
        public int randomSerialBefore;
        public DivinationRecord divination;
    }
    [Serializable] public sealed class BattleSession
    {
        public int version = 1, seed, round = 1, actionSerial, enemyCursor;
        public string sessionId, encounterId, contextId, balanceVersion;
        public Emerge.Checks.ActorCheckAttributes attributes;
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
        public List<string> enhancedSkills = new List<string>();
        public List<BattleSummonState> summons = new List<BattleSummonState>();
        public int randomSerial, domainRounds;
        public bool domainEnhanced;
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

namespace Emerge.Battle
{
    // Explicit values follow the authored table; serialized assets and saves keep stable identities.
    public enum BattleSkillKind
    {
        Legacy = 0,
        HeavenRadiance = 1, MetalSever = 2, StealHexagram = 3, PrisonBreak = 4, HiddenStrike = 5,
        ThunderMark = 6, FlameForge = 7, MountainBarrier = 8, ShadowMark = 9, SolarCurse = 10,
        GuNursery = 11, TripleDoom = 12, WindBlades = 13, HeartLight = 14, ReturningBreath = 15,
        SixLineFateGu = 16, ThunderFire = 17, EarthWard = 18, WaterClone = 19, LunarBlessing = 20,
        TripleChange = 21, MysticArmor = 22, RevolvingGu = 23, DrawCalamity = 24,
        AllLinesChange = 25, FateVerdict = 26, HeavenBurial = 27, YinYangSlash = 28,
        RecoilTalent = 29, CriticalTalent = 30
    }

    // Initial test values are centralized so balance passes can change them without changing skill identities.
    public static class BattleSkillTableRules
    {
        public const string BalanceVersion = "v0.6-skill-table";
        public const int EnhancementPointThreshold = 3;
        public const int NormalOfferCount = 3;
        public const int UltimateFirstRound = 3;
        public const float ManaCostScale = .65f;
        public const float CritChance = .20f;
        public const float CritMultiplier = 1.5f;
        public const float FateGoodChance = .50f;
        public const float EnhancedFateGoodChance = .75f;
        public const float RecoilTalentChance = .30f;
        public const int RecoilTalentDamage = 7;
        public const int RecoilTalentMana = 3;
        public const float CriticalTalentBonus = .20f;
        public const int CloneRounds = 4;
        public const int TripleChangeRounds = 6;
    }
}

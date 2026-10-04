using System;

namespace Emerge.Battle
{
    [Serializable] public sealed class BattleAttemptSeed
    { public string contextId, encounterId; public int seed; }
    [Serializable] public sealed class BattleVictoryRecord
    {
        public string encounterId, balanceVersion, lastSessionId;
        public int wins, lastRounds, bestRounds;
    }
}

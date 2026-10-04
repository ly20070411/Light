using System;

namespace Emerge.Checks.Divination
{
    [Serializable]
    public sealed class DivinationRecord
    {
        public const string CurrentRulesVersion = "docx-2026-10-03";
        public string rulesVersion = CurrentRulesVersion;
        public string month;
        public string day;
        public CoinCastResult casting;
        [UnityEngine.SerializeReference] public LiuYaoPaiPan.PaiPanResult chart;
        public int revealedLines;
    }
}

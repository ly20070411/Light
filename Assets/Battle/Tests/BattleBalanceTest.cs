#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emerge.Props;
using UnityEngine;

namespace Emerge.Battle.Tests
{
    // Runs the shipping engine, with paired seeds and only information visible to the player.
    public sealed class BattleBalanceTest : MonoBehaviour
    {
        [Serializable] public sealed class Result
        {
            public string encounter, policy;
            public bool items;
            public int trials, wins, timeouts, medianRounds, p90Rounds, medianHP, maximumRounds;
            public float winPercent, meanRounds, meanHP;
        }
        [Serializable] public sealed class Report
        { public string version, completedUtc; public int seedStart, trialsPerGroup; public List<Result> results = new List<Result>(); }
        public static event Action<bool> Completed;
        private BattleCatalog catalog;
        public void Run() => StartCoroutine(Sweep());
        private IEnumerator Sweep()
        {
            catalog = Resources.Load<BattleCatalog>(BattleCatalog.ResourcePath);
            var report = new Report { version = catalog.rules.balanceVersion, seedStart = 31001, trialsPerGroup = 1000 };
            var fixture = new GameObject("Balance simulation inventory"); var inventory = fixture.AddComponent<PropGameState>();
            foreach (var encounter in catalog.encounters)
                foreach (bool items in new[] { false })
                    foreach (int policy in new[] { 0, 1, 2 })
                    {
                        bool adaptive = policy == 2;
                        var result = new Result { encounter = encounter.id, policy = adaptive ? "respond-to-intent" : policy == 1 ? "guard-then-offense" : "offense-only", items = items, trials = report.trialsPerGroup };
                        var rounds = new List<int>(); var hp = new List<int>();
                        for (int i = 0; i < result.trials; i++)
                        {
                            inventory.RestoreSnapshot(new PropGameState.Snapshot());
                            if (items) SeedKit(inventory);
                            var engine = new BattleEngine(catalog, inventory); engine.Start(encounter, report.seedStart + i);
                            Simulate(engine, adaptive, policy == 1);
                            if (engine.State.phase == BattlePhase.Victory) { result.wins++; rounds.Add(engine.State.round); hp.Add(engine.State.player.hp); }
                            else if (engine.State.phase != BattlePhase.Defeat) result.timeouts++;
                            if (i % 50 == 49) yield return null;
                        }
                        rounds.Sort(); hp.Sort(); result.winPercent = result.wins * 100f / result.trials;
                        if (rounds.Count > 0) { result.medianRounds = rounds[(rounds.Count - 1) / 2]; result.p90Rounds = rounds[Math.Min(rounds.Count - 1, (int)(rounds.Count * .9f))]; result.maximumRounds = rounds.Last(); result.meanRounds = (float)rounds.Average(); result.medianHP = hp[(hp.Count - 1) / 2]; result.meanHP = (float)hp.Average(); }
                        report.results.Add(result);
                        Directory.CreateDirectory("Validation"); File.WriteAllText("Validation/battle-balance-report.json", JsonUtility.ToJson(report, true));
                        Debug.Log("BALANCE " + result.encounter + " " + result.policy + " kit=" + items + " wins=" + result.winPercent + "% rounds=" + result.medianRounds);
                        yield return null;
                    }
            report.completedUtc = DateTime.UtcNow.ToString("O"); File.WriteAllText("Validation/battle-balance-report.json", JsonUtility.ToJson(report, true));
            Destroy(fixture); Completed?.Invoke(report.results.All(r => r.timeouts == 0));
        }
        public static void SeedKit(PropGameState inventory)
        {
            inventory.AddItem("ITEM_MED", "医疗包", 2); inventory.AddItem("ITEM_MP", "凝神剂", 1);
            inventory.AddItem("ITEM_CLEAN", "驱秽符", 1); inventory.AddItem("ITEM_BLAST", "散灵符", 1);
        }
        public static void Reveal(BattleEngine e) { while (e.RevealLine()) { } e.ResolveRound(); }
        public static void Simulate(BattleEngine e, bool adaptive, bool fixedGuard = false)
        {
            while (e.State.round <= 40 && e.State.phase != BattlePhase.Victory && e.State.phase != BattlePhase.Defeat)
            {
                Reveal(e); Act(e, adaptive, fixedGuard);
                if (e.State.phase != BattlePhase.Player) break;
                e.EndTurn(); while (e.State.phase == BattlePhase.Enemy) e.StepEnemy();
            }
        }
        private static bool Use(BattleEngine e, string id, int target = 0) => e.CommitSkill(id, target, out _);
        private static int Target(BattleEngine e)
        {
            // Finish a vulnerable or low-HP target; interference enemies win close ties.
            return Enumerable.Range(0, e.State.enemies.Count).Where(i => e.State.enemies[i].hp > 0)
                .OrderBy(i => (e.State.enemies[i].hp + e.State.enemies[i].shield) / (e.State.enemies[i].vulnerabilityHits > 0 ? 1.2f : 1f) - (e.State.enemies[i].definitionId == "E03" ? 18 : 0)).First();
        }
        private static int Damage(BattleEngine e, BattleSkillDefinition s, int i)
        {
            var foe = e.State.enemies[i]; float v = foe.vulnerabilityHits > 0 ? 1 + foe.vulnerability : 1;
            return Math.Max(0, BattleRules.Round(s.power * e.SkillMultiplier(s.id) * (1 - e.State.player.weakness) * v) - foe.shield);
        }
        private static void Act(BattleEngine e, bool adaptive, bool fixedGuard)
        {
            var p = e.State.player;
            if (adaptive && BattleEngine.HasNegativeState(p))
            {
                bool cleanse = p.burnTicks > 0 || p.exposure > 0 || p.weakness >= .2f;
                if (cleanse && !e.UseItem("ITEM_CLEAN", out _)) Use(e, "MP_CLEANSE");
            }
            // Both policies have the same item stock; offensive policy uses obvious emergency consumables.
            if (p.hp <= 65) e.UseItem("ITEM_MED", out _);
            if (p.mp <= 18) e.UseItem("ITEM_MP", out _);
            if (e.State.enemies.Count(x => x.hp > 0) > 1 || e.State.enemies.Sum(x => x.hp) <= 10) e.UseItem("ITEM_BLAST", out _);
            if (e.State.phase != BattlePhase.Player) return;
            if (fixedGuard) Use(e, "DEF_GUARD");
            if (adaptive)
            {
                int forecast = e.ForecastDamage();
                if (p.hp <= 65 || p.hp <= forecast + 20) Use(e, "HEAL_DIRECT");
                if (p.regenerationTicks == 0 && p.hp <= 85 && e.State.enemies.Sum(x => x.hp) >= 65) Use(e, "HEAL_REGEN");
                // Control is chosen for special attacks with a tangible damage/status benefit.
                int special = Enumerable.Range(0, e.State.enemies.Count).Where(i => e.State.enemies[i].hp > 0 && !e.State.enemies[i].silenced)
                    .OrderByDescending(i => e.Catalog.Enemy(e.State.enemies[i].definitionId).skills.First(s => s.id == e.State.enemies[i].intentSkillId).power).First();
                var intent = e.Catalog.Enemy(e.State.enemies[special].definitionId).skills.First(s => s.id == e.State.enemies[special].intentSkillId);
                if (intent.mpCost > 0 && (intent.power >= 22 || intent.effect == EnemyEffect.Burn || intent.effect == EnemyEffect.Exposure) && e.SkillMultiplier("CTRL_SEAL") >= 1) Use(e, "CTRL_SEAL", special);
                if (e.SkillMultiplier("CTRL_BIND") >= 1.4f && !e.Catalog.Enemy(e.State.enemies[special].definitionId).resistsStun && e.ForecastDamage() >= 20) Use(e, "CTRL_BIND", special);
                // Defensive investment is worthwhile when enemies survive this round.
                int totalHP = e.State.enemies.Sum(x => x.hp + x.shield);
                int potential = p.mp / 3 * 7;
                if (totalHP > potential && e.ForecastDamage() > 0)
                {
                    Use(e, "DEF_GUARD");
                    if (e.ForecastDamage() >= 16 && p.hp <= 50 && p.mp >= 9 && e.SkillMultiplier("DEF_SHIELD") >= 1) Use(e, "DEF_SHIELD");
                }
                if (totalHP > 60 && p.mp >= 8 && p.mp <= 16 && p.nextMana == 0) Use(e, "MP_BREATH");
            }
            for (int command = 0; command < 50 && e.State.phase == BattlePhase.Player; command++)
            {
                int target = Target(e);
                var candidates = e.Catalog.skills.Where(s => s.effect == BattleEffect.Damage && e.CanUseSkill(s.id, target, out _))
                    .OrderByDescending(s => s.target == BattleTarget.AllEnemies ? e.State.enemies.Select((x, i) => x.hp > 0 ? Math.Min(x.hp, Damage(e, s, i)) : 0).Sum() / (float)s.mpCost : (Math.Min(e.State.enemies[target].hp, Damage(e, s, target)) + (adaptive && s.appliesVulnerability && e.SkillMultiplier(s.id) >= 1 && e.State.enemies[target].hp > Damage(e, s, target) + 14 && e.State.enemies[target].vulnerabilityHits == 0 ? 5 : 0)) / (float)s.mpCost).ToList();
                if (candidates.Count == 0) break;
                // Spend a limited heavy hit only if it beats the always-available attack's efficiency.
                if (!Use(e, candidates[0].id, target)) break;
            }
        }
    }
}
#endif

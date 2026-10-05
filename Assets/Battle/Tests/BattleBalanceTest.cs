#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emerge.Checks;
using Emerge.Props;
using UnityEngine;

namespace Emerge.Battle.Tests
{
    // Runs the shipping engine, with paired seeds and only information visible to the player.
    public sealed class BattleBalanceTest : MonoBehaviour
    {
        [Serializable] public sealed class Result
        {
            public string encounter, policy, build, kit;
            public ActorCheckAttributes attributes;
            public bool items;
            public int trials, wins, timeouts, medianRounds, p90Rounds, medianHP, maximumRounds, minimumRounds, winsWithinTwoRounds;
            public float winPercent, meanRounds, meanHP;
        }
        [Serializable] public sealed class Report
        { public string version, completedUtc; public bool passed; public int seedStart, trialsPerGroup; public List<Result> results = new List<Result>(); public List<Acceptance> checks = new List<Acceptance>(); }
        [Serializable] public sealed class Acceptance { public string name, observed; public bool passed; }
        private sealed class Build
        { public string name; public ActorCheckAttributes attributes; }
        private static Build[] Builds() => new[]
        {
            new Build { name = "balanced", attributes = SixKinAttributes.DefaultBuild() },
            new Build { name = "officer-8", attributes = new ActorCheckAttributes { officer = 8 } },
            new Build { name = "officer-6-support", attributes = new ActorCheckAttributes { officer = 6, parent = 1, wealth = 1 } },
            new Build { name = "officer-4-sustain", attributes = new ActorCheckAttributes { officer = 4, parent = 2, wealth = 2 } },
            new Build { name = "control-sustain", attributes = new ActorCheckAttributes { officer = 3, parent = 2, wealth = 2, sibling = 1 } },
            new Build { name = "defense-sustain", attributes = new ActorCheckAttributes { officer = 3, parent = 4, wealth = 1 } }
        };
        public static event Action<bool> Completed;
        private BattleCatalog catalog;
        public void Run() => StartCoroutine(Sweep());
        private IEnumerator Sweep()
        {
            catalog = Resources.Load<BattleCatalog>(BattleCatalog.ResourcePath);
            var report = new Report { version = catalog.rules.balanceVersion, seedStart = 31001, trialsPerGroup = 1000 };
            var fixture = new GameObject("Balance simulation inventory"); var inventory = fixture.AddComponent<PropGameState>();
            foreach (var encounter in catalog.encounters)
                foreach (var build in encounter.id == "ENC05" ? Builds() : Builds().Take(1))
                  foreach (int kit in encounter.id == "ENC05" ? new[] { 0, 1, 2 } : new[] { 0, 1 })
                    foreach (int policy in encounter.id == "ENC05" ? new[] { 0, 1, 2, 3 } : new[] { 0, 2 })
                    {
                        bool adaptive = policy == 2;
                        bool items = kit > 0;
                        var result = new Result { encounter = encounter.id, build = build.name, kit = kit == 0 ? "none" : kit == 1 ? "starter" : "two-mana-stress", attributes = build.attributes.Clone(), policy = adaptive ? "respond-to-intent" : policy == 1 ? "guard-then-offense" : policy == 3 ? "basic-spam" : "offense-only", items = items, trials = report.trialsPerGroup };
                        var rounds = new List<int>(); var hp = new List<int>();
                        for (int i = 0; i < result.trials; i++)
                        {
                            inventory.RestoreSnapshot(new PropGameState.Snapshot());
                            if (items) SeedKit(inventory);
                            if (kit == 2) inventory.AddItem("ITEM_MP", "凝神剂", 1);
                            var engine = new BattleEngine(catalog, inventory) { EmitRuntimeLogs = false }; engine.Start(encounter, report.seedStart + i, attributes: build.attributes);
                            Simulate(engine, adaptive, policy == 1, policy == 3);
                            if (engine.State.phase == BattlePhase.Victory) { result.wins++; if (engine.State.round <= 2) result.winsWithinTwoRounds++; rounds.Add(engine.State.round); hp.Add(engine.State.player.hp); }
                            else if (engine.State.phase != BattlePhase.Defeat) result.timeouts++;
                            if (i % 50 == 49) yield return null;
                        }
                        rounds.Sort(); hp.Sort(); result.winPercent = result.wins * 100f / result.trials;
                        if (rounds.Count > 0) { result.minimumRounds = rounds.First(); result.medianRounds = rounds[(rounds.Count - 1) / 2]; result.p90Rounds = rounds[Math.Min(rounds.Count - 1, (int)(rounds.Count * .9f))]; result.maximumRounds = rounds.Last(); result.meanRounds = (float)rounds.Average(); result.medianHP = hp[(hp.Count - 1) / 2]; result.meanHP = (float)hp.Average(); }
                        report.results.Add(result);
                        WriteReport(report);
                        Debug.Log("BALANCE " + result.encounter + " " + result.build + " " + result.policy + " kit=" + result.kit + " wins=" + result.winPercent + "% rounds=" + result.medianRounds + "; two-round-wins=" + result.winsWithinTwoRounds);
                        yield return null;
                    }
            var boss = report.results.Where(r => r.encounter == "ENC05").ToArray();
            Check(report, "All simulations terminate", report.results.All(r => r.timeouts == 0), "timeouts=" + report.results.Sum(r => r.timeouts));
            Check(report, "Boss cannot be defeated within two rounds", boss.All(r => r.winsWithinTwoRounds == 0), "two-round-wins=" + boss.Sum(r => r.winsWithinTwoRounds));
            Check(report, "Eight officer points cannot brute force the boss, even with the starter kit", boss.Where(r => r.build == "officer-8").All(r => r.wins == 0), "wins=" + boss.Where(r => r.build == "officer-8").Sum(r => r.wins));
            Check(report, "Attack-only policies cannot defeat the boss", boss.Where(r => r.policy == "offense-only" || r.policy == "basic-spam").All(r => r.wins == 0), "wins=" + boss.Where(r => r.policy == "offense-only" || r.policy == "basic-spam").Sum(r => r.wins));
            var viable = boss.Where(r => !r.items && r.policy == "respond-to-intent" && (r.build == "officer-4-sustain" || r.build == "control-sustain" || r.build == "defense-sustain")).ToArray();
            Check(report, "Sustain, control and defense builds can win without items", viable.Length == 3 && viable.All(r => r.winPercent >= 80), "minimum=" + viable.Min(r => r.winPercent));
            var balanced = boss.Single(r => r.kit == "starter" && r.policy == "respond-to-intent" && r.build == "balanced");
            Check(report, "Balanced build remains viable with the starter kit", balanced.winPercent >= 80, "wins=" + balanced.winPercent + "%");
            report.passed = report.checks.All(c => c.passed); report.completedUtc = DateTime.UtcNow.ToString("O"); WriteReport(report);
            Destroy(fixture); Completed?.Invoke(report.passed);
        }
        private static void Check(Report report, string name, bool passed, string observed)
        { report.checks.Add(new Acceptance { name = name, passed = passed, observed = observed }); Debug.Log("BALANCE_CHECK " + (passed ? "PASS " : "FAIL ") + name + ": " + observed); }
        private static void WriteReport(Report report)
        {
            string path = Path.GetFullPath("Validation/battle-balance-report.json"), backups = Path.GetFullPath("Temp/BalanceReports");
            Directory.CreateDirectory(Path.GetDirectoryName(path)); Directory.CreateDirectory(backups);
            string temporary = Path.Combine(backups, Guid.NewGuid().ToString("N") + ".tmp"); File.WriteAllText(temporary, JsonUtility.ToJson(report, true));
            if (File.Exists(path)) File.Replace(temporary, path, Path.Combine(backups, Guid.NewGuid().ToString("N") + ".json")); else File.Move(temporary, path);
        }
        public static void SeedKit(PropGameState inventory)
        {
            inventory.AddItem("ITEM_MED", "医疗包", 2); inventory.AddItem("ITEM_MP", "凝神剂", 1);
            inventory.AddItem("ITEM_CLEAN", "驱秽符", 1); inventory.AddItem("ITEM_BLAST", "散灵符", 1);
        }
        public static void Reveal(BattleEngine e) { while (e.RevealLine()) { } e.ResolveRound(); }
        public static void Simulate(BattleEngine e, bool adaptive, bool fixedGuard = false, bool basicOnly = false)
        {
            while (e.State.round <= 40 && e.State.phase != BattlePhase.Victory && e.State.phase != BattlePhase.Defeat)
            {
                Reveal(e); Act(e, adaptive, fixedGuard, basicOnly);
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
        private static void Act(BattleEngine e, bool adaptive, bool fixedGuard, bool basicOnly)
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
                bool retaliation = e.HasRetaliationTarget(BattleTarget.AllEnemies, 0);
                int forecast = e.ForecastDamage();
                // Start regeneration before emergency healing so it pays all three ticks.
                if (p.regenerationTicks == 0 && p.hp <= 90 && e.State.enemies.Sum(x => x.hp) >= 65) Use(e, "HEAL_REGEN");
                if (p.hp <= 65 || p.hp <= forecast + 20) Use(e, "HEAL_DIRECT");
                // Control is chosen for special attacks with a tangible damage/status benefit.
                int special = Enumerable.Range(0, e.State.enemies.Count).Where(i => e.State.enemies[i].hp > 0 && !e.State.enemies[i].silenced)
                    .OrderByDescending(i => e.Catalog.Enemy(e.State.enemies[i].definitionId).skills.First(s => s.id == e.State.enemies[i].intentSkillId).power).First();
                var intent = e.Catalog.Enemy(e.State.enemies[special].definitionId).skills.First(s => s.id == e.State.enemies[special].intentSkillId);
                if (intent.mpCost > 0 && BattleEngine.IsAttack(intent.effect) && (intent.power >= 22 || intent.effect == EnemyEffect.Burn || intent.effect == EnemyEffect.Exposure) && e.SkillMultiplier("CTRL_SEAL") >= 1) Use(e, "CTRL_SEAL", special);
                if ((e.State.version >= 3 || e.SkillMultiplier("CTRL_BIND") >= 1.4f) && !e.Catalog.Enemy(e.State.enemies[special].definitionId).resistsStun && e.ForecastDamage() >= 20) Use(e, "CTRL_BIND", special);
                // Defensive investment is worthwhile when enemies survive this round.
                int totalHP = e.State.enemies.Sum(x => x.hp + x.shield);
                int potential = p.mp / 3 * 7;
                if ((totalHP > potential && e.ForecastDamage() > 0) || retaliation)
                {
                    Use(e, "DEF_GUARD");
                    if ((e.ForecastDamage() >= 16 && p.hp <= 50 || retaliation && p.hp <= 85) && p.mp >= 9 && e.SkillMultiplier("DEF_SHIELD") >= 1) Use(e, "DEF_SHIELD");
                }
                if (totalHP > 60 && p.mp >= 8 && p.mp <= 16 && p.nextMana == 0) Use(e, "MP_BREATH");
            }
            for (int command = 0; command < 50 && e.State.phase == BattlePhase.Player; command++)
            {
                int target = Target(e);
                var candidates = e.Catalog.skills.Where(s => s.effect == BattleEffect.Damage && (!basicOnly || s.id == "ATK_BASIC") && e.CanUseSkill(s.id, target, out _))
                    .OrderByDescending(s => s.target == BattleTarget.AllEnemies ? e.State.enemies.Select((x, i) => x.hp > 0 ? Math.Min(x.hp, Damage(e, s, i)) : 0).Sum() / (float)s.mpCost : (Math.Min(e.State.enemies[target].hp, Damage(e, s, target)) + (adaptive && s.appliesVulnerability && e.SkillMultiplier(s.id) >= 1 && e.State.enemies[target].hp > Damage(e, s, target) + 14 && e.State.enemies[target].vulnerabilityHits == 0 ? 5 : 0)) / (float)s.mpCost).ToList();
                if (candidates.Count == 0) { if (e.UseItem("ITEM_MP", out _)) continue; break; }
                if (adaptive && e.ForecastRetaliation(candidates[0].id, target) >= p.hp)
                {
                    if (Use(e, "HEAL_DIRECT") || Use(e, "DEF_SHIELD") || e.UseItem("ITEM_MED", out _)) continue;
                    break;
                }
                // Spend a limited heavy hit only if it beats the always-available attack's efficiency.
                if (!Use(e, candidates[0].id, target)) break;
            }
        }
    }
}
#endif

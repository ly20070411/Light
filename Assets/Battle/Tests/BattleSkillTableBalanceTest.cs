#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emerge.Checks;
using UnityEngine;

namespace Emerge.Battle.Tests
{
    // Each decision reads the revealed turn and public combat state. No forecast of a future roll,
    // cloned-engine search or inventory is used by either policy.
    public static class BattleSkillTableBalanceTest
    {
        [Serializable] public sealed class Count { public string name; public int count; }
        [Serializable] public sealed class Usage
        { public string id, kind; public int actions, battles, offeredBattles, enhancedActions, passiveTriggers; public float actionsPerBattle, battlePercent; }
        [Serializable] public sealed class Result
        {
            public string encounter, build, policy;
            public ActorCheckAttributes attributes;
            public bool items;
            public int trials, wins, defeats, timeouts, invalidActions, negativeMana, minimumRounds, p10Rounds,
                medianRounds, p90Rounds, maximumRounds, winsWithinTwoRounds, medianHP;
            public float winPercent, meanRounds, meanHP;
            public List<Count> roundDistribution = new List<Count>(), failures = new List<Count>();
            public List<Usage> skills = new List<Usage>();
        }
        [Serializable] public sealed class Acceptance { public string name, observed; public bool passed; }
        [Serializable] public sealed class Pair
        { public string encounter, build; public int bothWin, adaptiveOnly, blindOnly, neitherWin; }
        [Serializable] public sealed class Report
        {
            public string version, completedUtc, policyInformation = "revealed offers, current HP/MP/statuses and displayed enemy intentions only";
            public bool passed, completed, items = false;
            public int seedStart = 31001, trialsPerGroup = 1000, totalBattles;
            public List<Result> results = new List<Result>();
            public List<Pair> pairedComparisons = new List<Pair>();
            public List<Usage> allSkillsUsage = new List<Usage>();
            public List<Acceptance> checks = new List<Acceptance>();
            public List<string> notes = new List<string>();
        }
        private sealed class Build { public string name; public ActorCheckAttributes attributes; public bool reasonable; }
        private sealed class Observation
        {
            public readonly HashSet<string> used = new HashSet<string>(), offered = new HashSet<string>();
            public readonly Dictionary<string, int> actions = new Dictionary<string, int>(), enhanced = new Dictionary<string, int>();
            public readonly Dictionary<string, int> passiveTriggers = new Dictionary<string, int>();
            public int invalidActions, negativeMana;
            public string failure;
        }
        private sealed class Command { public BattleSkillDefinition skill; public int target; public float score; }
        public static Report LastReport { get; private set; }
        private static Build[] Builds() => new[]
        {
            new Build { name = "balanced", reasonable = true, attributes = SixKinAttributes.DefaultBuild() },
            new Build { name = "offense-defense", reasonable = true, attributes = new ActorCheckAttributes { officer = 3, parent = 3, offspring = 2 } },
            new Build { name = "offense-healing", reasonable = true, attributes = new ActorCheckAttributes { officer = 3, offspring = 3, parent = 2 } },
            new Build { name = "wealth-assault", reasonable = true, attributes = new ActorCheckAttributes { wealth = 3, parent = 3, offspring = 2 } },
            new Build { name = "offense-control", reasonable = true, attributes = new ActorCheckAttributes { officer = 3, sibling = 3, offspring = 2 } },
            new Build { name = "defense-healing", reasonable = true, attributes = new ActorCheckAttributes { parent = 3, offspring = 3, wealth = 2 } },
            new Build { name = "officer-8", attributes = new ActorCheckAttributes { officer = 8 } }
        };
        public static IEnumerator RunSweep(Action<bool> completed = null, int trialsPerGroup = 1000)
        {
            var catalog = Resources.Load<BattleCatalog>(BattleCatalog.ResourcePath);
            string error = null;
            if (catalog == null || !catalog.Validate(out error)) throw new InvalidOperationException(error ?? "Missing battle catalog");
            var report = LastReport = new Report { version = catalog.rules.balanceVersion, trialsPerGroup = trialsPerGroup };
            report.notes.Add("Every policy/build uses the same contiguous seed range. Both policies obey real MP, offers and whole-battle caps.");
            report.notes.Add("Boss 4–9 rounds is a diagnostic target, not a hard acceptance gate. Two-round boss wins are measured separately.");
            foreach (var encounter in catalog.encounters)
                foreach (var build in Builds())
                {
                    var pair = new Pair { encounter = encounter.id, build = build.name };
                    var adaptiveWins = new bool[trialsPerGroup];
                    foreach (bool adaptive in new[] { true, false })
                    {
                        var result = new Result { encounter = encounter.id, build = build.name, policy = adaptive ? "respond-to-intent" : "damage-only-spam", attributes = build.attributes.Clone(), trials = trialsPerGroup };
                        foreach (var skill in catalog.skills) result.skills.Add(new Usage { id = skill.id, kind = skill.kind.ToString() });
                        var rounds = new List<int>(); var health = new List<int>();
                        for (int trial = 0; trial < trialsPerGroup; trial++)
                        {
                            var engine = new BattleEngine(catalog, null) { EmitRuntimeLogs = false };
                            engine.Start(encounter, report.seedStart + trial, attributes: build.attributes);
                            var observation = Simulate(engine, adaptive);
                            bool won = engine.State.phase == BattlePhase.Victory;
                            if (adaptive) adaptiveWins[trial] = won;
                            else if (adaptiveWins[trial]) { if (won) pair.bothWin++; else pair.adaptiveOnly++; }
                            else { if (won) pair.blindOnly++; else pair.neitherWin++; }
                            if (won)
                            {
                                result.wins++; rounds.Add(engine.State.round); health.Add(engine.State.player.hp);
                                if (engine.State.round <= 2) result.winsWithinTwoRounds++;
                                Increment(result.roundDistribution, engine.State.round.ToString());
                            }
                            else
                            {
                                if (engine.State.phase == BattlePhase.Defeat) result.defeats++; else result.timeouts++;
                                Increment(result.failures, observation.failure ?? "round-limit");
                            }
                            result.invalidActions += observation.invalidActions; result.negativeMana += observation.negativeMana;
                            foreach (var usage in result.skills)
                            {
                                usage.actions += Get(observation.actions, usage.id); usage.enhancedActions += Get(observation.enhanced, usage.id);
                                usage.passiveTriggers += Get(observation.passiveTriggers, usage.id);
                                if (observation.used.Contains(usage.id)) usage.battles++;
                                if (observation.offered.Contains(usage.id)) usage.offeredBattles++;
                            }
                            report.totalBattles++;
                            if (trial % 25 == 24) yield return null;
                        }
                        rounds.Sort(); health.Sort(); result.winPercent = result.wins * 100f / result.trials;
                        if (rounds.Count > 0)
                        {
                            result.minimumRounds = rounds[0]; result.maximumRounds = rounds[rounds.Count - 1]; result.p10Rounds = Percentile(rounds, .1f);
                            result.medianRounds = Percentile(rounds, .5f); result.p90Rounds = Percentile(rounds, .9f); result.meanRounds = (float)rounds.Average();
                            result.medianHP = Percentile(health, .5f); result.meanHP = (float)health.Average();
                        }
                        foreach (var usage in result.skills) { usage.actionsPerBattle = usage.actions / (float)result.trials; usage.battlePercent = usage.battles * 100f / result.trials; }
                        report.results.Add(result); WriteReport(report);
                        Debug.Log("SKILL_TABLE_BALANCE " + result.encounter + " " + result.build + " " + result.policy + " wins=" + result.winPercent.ToString("F1") + "% rounds=" + result.medianRounds + " two-round=" + result.winsWithinTwoRounds);
                        yield return null;
                    }
                    report.pairedComparisons.Add(pair);
                }
            Check(report, "All simulations terminate without items", report.results.All(r => r.timeouts == 0 && !r.items), "timeouts=" + report.results.Sum(r => r.timeouts));
            Check(report, "Every action obeys MP and eligibility", report.results.All(r => r.invalidActions == 0 && r.negativeMana == 0), "invalid=" + report.results.Sum(r => r.invalidActions) + "; negativeMP=" + report.results.Sum(r => r.negativeMana));
            var ordinary = report.results.Where(r => r.policy == "respond-to-intent" && r.build != "officer-8" && !IsBoss(catalog.Encounter(r.encounter))).ToArray();
            Check(report, "Reasonable builds win ordinary encounters at least 80%", ordinary.Length > 0 && ordinary.All(r => r.winPercent >= 80), string.Join("; ", ordinary.Where(r => r.winPercent < 80).Select(r => r.encounter + "/" + r.build + "=" + r.winPercent.ToString("F1") + "%")));
            var blind = report.results.Where(r => r.policy == "damage-only-spam").ToArray();
            Check(report, "Damage-only spam does not pass every scenario", blind.Any(r => r.winPercent < 80), "blind minimum=" + blind.Min(r => r.winPercent).ToString("F1") + "%");
            var boss = report.results.Where(r => IsBoss(catalog.Encounter(r.encounter))).ToArray();
            var viableBoss = boss.Where(r => r.policy == "respond-to-intent" && r.build != "officer-8").ToArray();
            Check(report, "Reasonable builds win the boss at least 80% without items", viableBoss.Length > 0 && viableBoss.All(r => r.winPercent >= 80), string.Join("; ", viableBoss.Select(r => r.build + "=" + r.winPercent.ToString("F1") + "%")));
            Check(report, "Damage-only boss spam stays below 20%", boss.Where(r => r.policy == "damage-only-spam").All(r => r.winPercent < 20), string.Join("; ", boss.Where(r => r.policy == "damage-only-spam").Select(r => r.build + "=" + r.winPercent.ToString("F1") + "%")));
            Check(report, "Boss is not routinely defeated in two rounds", boss.Length > 0 && boss.All(r => r.winsWithinTwoRounds <= r.trials * .05f), "two-round boss wins=" + boss.Sum(r => r.winsWithinTwoRounds));
            report.notes.Add("Boss adaptive medians: " + string.Join("; ", boss.Where(r => r.policy == "respond-to-intent").Select(r => r.build + "=" + r.medianRounds + " rounds / " + r.winPercent.ToString("F1") + "%")));
            foreach (var skill in catalog.skills)
            {
                var rows = report.results.SelectMany(r => r.skills).Where(u => u.id == skill.id).ToArray();
                var summary = new Usage { id = skill.id, kind = skill.kind.ToString(), actions = rows.Sum(u => u.actions), battles = rows.Sum(u => u.battles), offeredBattles = rows.Sum(u => u.offeredBattles), enhancedActions = rows.Sum(u => u.enhancedActions), passiveTriggers = rows.Sum(u => u.passiveTriggers) };
                summary.actionsPerBattle = summary.actions / (float)report.totalBattles; summary.battlePercent = summary.battles * 100f / report.totalBattles; report.allSkillsUsage.Add(summary);
            }
            report.passed = report.checks.All(c => c.passed); report.completed = true; report.completedUtc = DateTime.UtcNow.ToString("O"); WriteReport(report); completed?.Invoke(report.passed);
        }
        public static bool IsBoss(BattleEncounterDefinition encounter) => encounter.enemies.Any(slot => slot.enemy.resistsStun || slot.enemy.skills.Any(s => s.effect == EnemyEffect.Charge));
        public static void Reveal(BattleEngine engine) { while (engine.RevealLine()) { } engine.ResolveRound(); }
        private static Observation Simulate(BattleEngine engine, bool adaptive)
        {
            var observation = new Observation();
            foreach (var permanent in engine.Catalog.skills.Where(s => s.alwaysAvailable || s.isPassive)) observation.offered.Add(permanent.id);
            for (int round = 0; round < 40 && engine.State.phase != BattlePhase.Victory && engine.State.phase != BattlePhase.Defeat; round++)
            {
                Reveal(engine); foreach (string id in engine.State.unlockedSkills) observation.offered.Add(id);
                for (int action = 0; action < 50 && engine.State.phase == BattlePhase.Player; action++)
                {
                    var command = Choose(engine, adaptive);
                    if (command == null || command.score <= 0) break;
                    bool enhanced = engine.IsEnhanced(command.skill.id);
                    int before = engine.State.player.hp, talentRound = engine.State.player.criticalTalentRound;
                    if (!engine.CommitSkill(command.skill.id, command.target, out _)) { observation.invalidActions++; break; }
                    observation.used.Add(command.skill.id); Increment(observation.actions, command.skill.id);
                    if (enhanced) Increment(observation.enhanced, command.skill.id);
                    if (engine.State.player.criticalTalentRound > talentRound)
                        PassiveTrigger(engine, observation, BattleSkillKind.CriticalTalent);
                    if (engine.State.player.mp < 0) observation.negativeMana++;
                    if (engine.State.phase == BattlePhase.Defeat) observation.failure = before > 0 ? "retaliation-or-skill-cost" : "player-state";
                }
                if (engine.State.phase != BattlePhase.Player) break;
                int hp = engine.State.player.hp; int burn = engine.State.player.burn;
                engine.EndTurn();
                if (engine.State.phase == BattlePhase.Defeat) observation.failure = burn > 0 ? "damage-over-time" : "turn-end";
                while (engine.State.phase == BattlePhase.Enemy)
                {
                    int cursor = engine.State.enemyCursor;
                    int randomBefore = engine.State.randomSerial;
                    string intent = cursor < engine.State.enemies.Count ? engine.State.enemies[cursor].intentSkillId : "status-tick";
                    engine.StepEnemy();
                    if (engine.State.randomSerial > randomBefore && cursor < engine.State.enemies.Count && engine.State.log.Count >= 2 && engine.State.log[engine.State.log.Count - 2].StartsWith("卜卦反噬", StringComparison.Ordinal))
                        PassiveTrigger(engine, observation, BattleSkillKind.RecoilTalent);
                    if (engine.State.phase == BattlePhase.Defeat) observation.failure = "enemy-intent:" + intent;
                }
            }
            return observation;
        }
        private static void PassiveTrigger(BattleEngine engine, Observation observation, BattleSkillKind kind)
        {
            var talent = engine.Catalog.skills.FirstOrDefault(s => s.kind == kind); if (talent == null) return;
            observation.used.Add(talent.id); Increment(observation.passiveTriggers, talent.id);
        }
        private static Command Choose(BattleEngine e, bool adaptive)
        {
            Command best = null;
            foreach (var skill in e.Catalog.skills)
            {
                if (!adaptive && skill.effect != BattleEffect.Damage && skill.kind != BattleSkillKind.MountainBarrier) continue;
                int targets = skill.target == BattleTarget.Enemy ? e.State.enemies.Count : 1;
                for (int target = 0; target < targets; target++)
                {
                    if (!e.CanUseSkill(skill.id, target, out _)) continue;
                    float value = Utility(e, skill, target, adaptive);
                    float score = value / skill.mpCost;
                    if (best == null || score > best.score + .0001f) best = new Command { skill = skill, target = target, score = score };
                }
            }
            return best;
        }
        private static float Utility(BattleEngine e, BattleSkillDefinition s, int target, bool adaptive)
        {
            var p = e.State.player; var foes = e.State.enemies;
            float multiplier = e.SkillMultiplier(s.id), power = s.power * multiplier;
            bool enhanced = e.IsEnhanced(s.id);
            int forecast = e.ForecastDamage();
            float endPressure = EndPressure(e);
            float incoming = forecast + Mathf.Max(0, endPressure - p.shield) + p.burn * p.burnTicks + BattleEngine.Status(p.statuses, BattleStatusKind.Burn);
            bool retaliation = e.HasRetaliationTarget(BattleTarget.AllEnemies, 0);
            float statusReduction = BattleEngine.Status(p.statuses, BattleStatusKind.DamageReduction);
            float currentReduction = Mathf.Min(e.Catalog.rules.reductionCap, p.reduction + statusReduction);
            float hpNeed = e.Catalog.rules.maxHP - p.hp;
            float survival = p.hp <= incoming + 15 ? 2.4f : p.hp <= 50 ? 1.15f : .65f;
            int totalHP = foes.Sum(x => x.hp + x.shield);
            if (adaptive && s.kind == BattleSkillKind.SolarCurse)
            {
                float additionalDown = Mathf.Max(0, (enhanced ? .30f : .15f) - BattleEngine.Status(foes[target].statuses, BattleStatusKind.DamageDown));
                return additionalDown * (ShownDamage(e, target) * 2.3f + RetaliationBudget(e, target) * (1 - foes[target].weakness)) * Mathf.Max(.9f, survival);
            }
            if (adaptive && s.kind == BattleSkillKind.LunarBlessing)
                return Mathf.Max(0, (enhanced ? .22f : .10f) - BattleEngine.Status(p.statuses, BattleStatusKind.DamageUp)) * Mathf.Min(totalHP, 150) + (enhanced && BattleEngine.Status(p.statuses, BattleStatusKind.KillMana) == 0 ? foes.Count(x => x.hp > 0) * 6 : 0);
            if (adaptive && s.kind == BattleSkillKind.TripleChange)
                return e.State.domainRounds > 0 || totalHP < 100 ? -1 : (enhanced ? 30 : 8);
            if (s.effect == BattleEffect.Damage || s.kind == BattleSkillKind.MountainBarrier)
            {
                float damage = 0;
                for (int i = 0; i < foes.Count; i++)
                {
                    var foe = foes[i]; if (foe.hp <= 0 || (s.target == BattleTarget.Enemy && i != target)) continue;
                    float expected = s.power;
                    switch (s.kind)
                    {
                        case BattleSkillKind.HeavenRadiance: if (enhanced) expected += 12; break;
                        case BattleSkillKind.ThunderMark:
                            if (enhanced) expected += 13 * ((foe.statuses.Find(x => x.kind == BattleStatusKind.Thunder)?.count ?? 0) + 1); break;
                        case BattleSkillKind.FlameForge:
                            if (BattleEngine.Status(foe.statuses, BattleStatusKind.Burn) == 0) expected += (enhanced ? 8 : 5) * 2; break;
                        case BattleSkillKind.ShadowMark:
                            expected = BattleEngine.Status(foe.statuses, BattleStatusKind.ShadowCurse) == 0 ? 8 * Mathf.Min(4, totalHP > 100 ? 4 : 2) : 0; break;
                        case BattleSkillKind.GuNursery: expected = 8 * (enhanced ? 2 : 1) * 3; break;
                        case BattleSkillKind.TripleDoom: if (foe.hp > foe.maxHP * .5f) expected += 12; break;
                        case BattleSkillKind.WindBlades: if (enhanced) expected += 12; break;
                        case BattleSkillKind.HeartLight:
                            if (foe.statuses.Any(x => x.kind == BattleStatusKind.Thunder || x.kind == BattleStatusKind.ShadowCurse)) expected += 8; break;
                        case BattleSkillKind.SixLineFateGu: expected = enhanced ? 44.5f : 36; break;
                        case BattleSkillKind.ThunderFire:
                            if (foe.statuses.Any(x => x.kind == BattleStatusKind.Burn || x.kind == BattleStatusKind.Thunder)) expected += 10; break;
                        case BattleSkillKind.WaterClone: expected = s.power * (enhanced ? 2 : 1) * Mathf.Min(4, totalHP > 90 ? 4 : 2); break;
                        case BattleSkillKind.RevolvingGu: if (enhanced) expected += 14; break;
                        case BattleSkillKind.FateVerdict: expected = 22.5f; break;
                        case BattleSkillKind.YinYangSlash: if (foe.hp < foe.maxHP * .3f) expected += 15; break;
                    }
                    float raw = expected * multiplier * (1 - p.weakness) * (1 + BattleEngine.Status(p.statuses, BattleStatusKind.DamageUp) + BattleEngine.Status(p.statuses, BattleStatusKind.HeavenMomentum)) * (1 - BattleEngine.Status(p.statuses, BattleStatusKind.DamageDown)) *
                        (1 + (foe.vulnerabilityHits > 0 ? foe.vulnerability : 0) + BattleEngine.Status(foe.statuses, BattleStatusKind.IncomingUp)) * (1 - foe.defense * (1 - BattleEngine.Status(foe.statuses, BattleStatusKind.DefenseBreak)));
                    float hit = Mathf.Max(0, raw - foe.shield);
                    if (adaptive && s.appliesVulnerability && foe.vulnerabilityHits == 0 && foe.hp > hit + 10) hit += power * .35f;
                    float capped = Mathf.Min(foe.hp, hit); damage += capped;
                    if (foe.hp <= hit) damage += adaptive ? ShownDamage(e, i) * .9f : 0;
                    if (adaptive && foe.definitionId == "E03") damage += 1;
                }
                if (adaptive)
                {
                    int reflected = e.ForecastRetaliation(s.id, target);
                    if (reflected >= p.hp) return -1;
                    damage -= reflected * survival;
                    if (s.kind == BattleSkillKind.SixLineFateGu) { if (p.hp <= 28) return -1; damage -= (enhanced ? 7 : 14) * survival; }
                    if (s.kind == BattleSkillKind.FateVerdict) { if (p.hp <= 38) return -1; damage += Mathf.Min(50, forecast + 15) * .5f - 19 * survival; }
                    if (s.kind == BattleSkillKind.AllLinesChange)
                    {
                        float additionalDamageUp = Mathf.Max(0, .25f - BattleEngine.Status(p.statuses, BattleStatusKind.DamageUp));
                        float futureOutput = Mathf.Min(Mathf.Max(0, totalHP - damage), (Mathf.Max(0, p.mp - s.mpCost) + e.Catalog.rules.roundMana * 3) * 2.3f);
                        float additionalReduction = Mathf.Max(0, Mathf.Min(e.Catalog.rules.reductionCap, p.reduction + Mathf.Max(statusReduction, .20f)) - currentReduction);
                        damage += additionalDamageUp * futureOutput;
                        damage += additionalReduction * forecast * 2 / Mathf.Max(.01f, 1 - currentReduction) * Mathf.Max(.9f, survival);
                        for (int enemyIndex = 0; enemyIndex < foes.Count; enemyIndex++)
                            if (foes[enemyIndex].hp > 0) damage += additionalReduction * RetaliationBudget(e, enemyIndex) / Mathf.Max(.01f, 1 - currentReduction) * Mathf.Max(.9f, survival);
                    }
                    if (s.kind == BattleSkillKind.MountainBarrier)
                    {
                        var existingDown = foes[target].statuses.FirstOrDefault(status => status.kind == BattleStatusKind.DamageDown);
                        float priorDown = existingDown?.power ?? 0;
                        float newDown = enhanced ? .50f : .30f;
                        float priorNextDown = existingDown != null && existingDown.rounds > 1 ? priorDown : 0;
                        float nextDown = newDown >= priorDown ? newDown : priorNextDown;
                        bool stun = enhanced && !e.Catalog.Enemy(foes[target].definitionId).resistsStun && !foes[target].determined;
                        float beforeFactor = (1 - foes[target].weakness) * (1 - priorDown);
                        float afterFactor = (1 - Mathf.Max(foes[target].weakness, enhanced && !stun ? .20f : 0)) * (1 - Mathf.Max(priorDown, newDown));
                        float currentSaved = Mathf.Max(0, beforeFactor - afterFactor), nextSaved = Mathf.Max(0, nextDown - priorNextDown);
                        float currentPrevention = stun ? beforeFactor : currentSaved;
                        // Weakness expires after this enemy action. Only the two-round DamageDown persists.
                        damage += ShownDamage(e, target) * (currentPrevention + nextSaved) * (1 - currentReduction) * Mathf.Max(.9f, survival);
                        float currentRetaliation = RetaliationBudget(e, target, 0, s.mpCost);
                        float nextRetaliation = Mathf.Max(0, RetaliationBudget(e, target, 1, s.mpCost) - currentRetaliation);
                        damage += (currentRetaliation * currentSaved + nextRetaliation * nextSaved) * Mathf.Max(.9f, survival);
                    }
                    if (s.kind == BattleSkillKind.ThunderMark && enhanced && e.Catalog.Enemy(foes[target].definitionId).resistsStun)
                        damage += RetaliationBudget(e, target, 0, s.mpCost) * Mathf.Max(0, .20f - foes[target].weakness) * (1 - BattleEngine.Status(foes[target].statuses, BattleStatusKind.DamageDown)) * Mathf.Max(.9f, survival);
                    if (s.kind == BattleSkillKind.WaterClone && e.State.summons.Count(x => x.kind == BattleSummonKind.Clone) >= 2) damage *= .25f;
                    if (s.kind == BattleSkillKind.GuNursery && e.State.summons.Any(x => x.kind == BattleSummonKind.GuNest)) damage *= .4f;
                    if (s.kind == BattleSkillKind.HiddenStrike) damage += ShownDamage(e, target) * .5f * survival;
                    if (s.kind == BattleSkillKind.DrawCalamity && BattleEngine.Status(foes[target].statuses, BattleStatusKind.Taunt) == 0)
                    {
                        var fallback = e.Catalog.Enemy(foes[target].definitionId).skills.First(x => x.effect == EnemyEffect.Damage && x.mpCost == 0 && x.maximumHealthFraction == 1 && x.maximumUses == 0 && x.cooldownRounds == 0);
                        damage += Mathf.Max(0, ShownDamage(e, target) - fallback.power) * 2 * (1 - currentReduction) * Mathf.Max(.9f, survival);
                    }
                    if (s.kind == BattleSkillKind.RevolvingGu && enhanced)
                    {
                        var cleanse = e.Catalog.Skill("MP_CLEANSE");
                        bool canClearLock = cleanse != null && e.RemainingUses(cleanse.id) > 0 && p.mp - s.mpCost >= cleanse.mpCost;
                        damage -= canClearLock ? cleanse.mpCost * 1.6f : e.Catalog.rules.roundMana * 1.6f;
                    }
                    if (s.kind == BattleSkillKind.MetalSever && BattleEngine.Status(foes[target].statuses, BattleStatusKind.DefenseBreak) == 0) damage += foes[target].defense * Mathf.Min(totalHP, 80) * (enhanced ? .25f : .12f);
                    if ((s.kind == BattleSkillKind.StealHexagram || s.kind == BattleSkillKind.HeartLight) && foes[target].shield > 0) damage += Mathf.Min(foes[target].shield, 30);
                }
                return damage;
            }
            if (!adaptive) return -1;
            switch (s.effect)
            {
                case BattleEffect.Reduction:
                    float extraReduction = Mathf.Max(0, Mathf.Min(e.Catalog.rules.reductionCap, Mathf.Max(p.reduction, power) + statusReduction) - currentReduction);
                    return ((forecast + endPressure) * extraReduction / Mathf.Max(.01f, 1 - currentReduction) + extraReduction * AttackRetaliationBudget(e, Mathf.Max(0, p.mp - s.mpCost))) * Mathf.Max(.9f, survival);
                case BattleEffect.Shield:
                    float shieldPower = s.kind == BattleSkillKind.EarthWard && enhanced ? 75 * multiplier : power;
                    float prevented = Mathf.Max(0, shieldPower - p.shield);
                    float pressure = forecast * (s.kind == BattleSkillKind.Legacy ? 1 : 1.8f) + endPressure + AttackRetaliationBudget(e, Mathf.Max(0, p.mp - s.mpCost)) * (1 - currentReduction);
                    return Mathf.Min(prevented, pressure) * Mathf.Max(.9f, survival) + (s.kind == BattleSkillKind.MysticArmor && BattleEngine.Status(p.statuses, BattleStatusKind.Reflect) == 0 ? foes.Count(x => x.hp > 0) * 8 : 0) + (s.kind == BattleSkillKind.EarthWard && enhanced && BattleEngine.Status(p.statuses, BattleStatusKind.ManaOnHit) == 0 && forecast > 0 ? 8 : 0);
                case BattleEffect.Heal: return (Mathf.Min(hpNeed, power) + (s.kind == BattleSkillKind.ReturningBreath && p.regenerationTicks == 0 ? Mathf.Min(hpNeed + forecast, 12 * multiplier) * .6f : 0)) * survival;
                case BattleEffect.Regeneration:
                    if (p.regenerationTicks > 0) return -1;
                    return Mathf.Min(hpNeed + forecast * 1.8f, power * Mathf.Min(s.regenerationTicks, totalHP > 90 ? 3 : 2)) * .7f;
                case BattleEffect.NextMana:
                    if (totalHP < 35 || p.nextMana >= e.Catalog.rules.nextManaCap) return -1;
                    return Mathf.Min(e.Catalog.rules.nextManaCap - p.nextMana, power) * (p.mp < 12 ? 2.1f : 1.6f);
                case BattleEffect.Cleanse:
                    var newBurn = p.statuses.FirstOrDefault(x => x.kind == BattleStatusKind.Burn);
                    float statusBurn = newBurn == null ? 0 : newBurn.power * newBurn.rounds;
                    float lockValue = BattleEngine.Status(p.statuses, BattleStatusKind.ManaLock) > 0 ? Mathf.Min(e.Catalog.rules.maxMP, e.Catalog.rules.roundMana + p.nextMana) * 1.6f : 0;
                    return (p.burn * p.burnTicks + statusBurn) * survival + p.exposure * forecast +
                        (p.weakness + BattleEngine.Status(p.statuses, BattleStatusKind.DamageDown)) * Mathf.Min(totalHP, p.mp * 3 + 25) +
                        (BattleEngine.Status(p.statuses, BattleStatusKind.IncomingUp) + BattleEngine.Status(p.statuses, BattleStatusKind.DefenseDown)) * (forecast + endPressure) +
                        lockValue + (p.nextMana == 0 && totalHP > 60 ? power * 1.2f : 0);
                case BattleEffect.Silence:
                    if (foes[target].silenced || foes[target].stunned) return -1;
                    var intent = Intent(e, target); if (intent.mpCost == 0) return -1;
                    var fallback = e.Catalog.Enemy(foes[target].definitionId).skills.First(x => x.effect == EnemyEffect.Damage && x.mpCost == 0);
                    return Mathf.Max(0, ShownDamage(e, target) - fallback.power) * survival + (intent.effect == EnemyEffect.Burn ? 10 : intent.effect == EnemyEffect.Exposure ? 5 : 0);
                case BattleEffect.Bind:
                    if (foes[target].stunned) return -1;
                    return ShownDamage(e, target) * (e.Catalog.Enemy(foes[target].definitionId).resistsStun || foes[target].determined ? Mathf.Min(.5f, .2f * multiplier) : 1) * survival;
                default: return 0;
            }
        }
        private static EnemySkillDefinition Intent(BattleEngine e, int i) => e.Catalog.Enemy(e.State.enemies[i].definitionId).skills.First(s => s.id == e.State.enemies[i].intentSkillId);
        private static int ShownDamage(BattleEngine e, int i) => e.State.enemies[i].hp <= 0 || e.State.enemies[i].stunned ? 0 : BattleEngine.IntentDamage(Intent(e, i));
        private static float RetaliationBudget(BattleEngine e, int target, int futureRounds = 2, int skillCost = 0)
        {
            var foe = e.State.enemies[target]; var p = e.State.player;
            float planned = Mathf.Min(foe.hp, (Mathf.Max(0, p.mp - skillCost) + e.Catalog.rules.roundMana * futureRounds) * 2.3f);
            float reduction = Mathf.Min(e.Catalog.rules.reductionCap, p.reduction + BattleEngine.Status(p.statuses, BattleStatusKind.DamageReduction));
            return planned * e.Catalog.Enemy(foe.definitionId).retaliation * (1 - reduction) * (1 + p.exposure + BattleEngine.Status(p.statuses, BattleStatusKind.IncomingUp) + BattleEngine.Status(p.statuses, BattleStatusKind.DefenseDown));
        }
        private static float AttackRetaliationBudget(BattleEngine e, int availableMP)
        {
            var living = e.State.enemies.Where(x => x.hp > 0).ToArray(); if (living.Length == 0) return 0;
            float coefficient = living.Average(foe => e.Catalog.Enemy(foe.definitionId).retaliation * (1 - foe.weakness) * (1 - BattleEngine.Status(foe.statuses, BattleStatusKind.DamageDown)));
            return Mathf.Min(living.Sum(foe => foe.hp), availableMP * 2.3f) * coefficient * (1 + e.State.player.exposure + BattleEngine.Status(e.State.player.statuses, BattleStatusKind.IncomingUp) + BattleEngine.Status(e.State.player.statuses, BattleStatusKind.DefenseDown));
        }
        private static float EndPressure(BattleEngine e)
        {
            var living = e.State.enemies.Where(x => x.hp > 0).ToArray(); if (living.Length == 0) return 0;
            float total = 0;
            foreach (var foe in living)
                total += (BattleEngine.Status(foe.statuses, BattleStatusKind.Burn) + BattleEngine.Status(foe.statuses, BattleStatusKind.ShadowCurse)) * (1 - foe.defense) * e.Catalog.Enemy(foe.definitionId).retaliation * (1 - foe.weakness) * (1 - BattleEngine.Status(foe.statuses, BattleStatusKind.DamageDown));
            foreach (var summon in e.State.summons)
            {
                if (summon.kind == BattleSummonKind.WindBlade && summon.targetIndex >= 0 && summon.targetIndex < e.State.enemies.Count)
                {
                    var foe = e.State.enemies[summon.targetIndex]; if (foe.hp > 0) total += summon.power * (1 - foe.defense) * e.Catalog.Enemy(foe.definitionId).retaliation * (1 - foe.weakness) * (1 - BattleEngine.Status(foe.statuses, BattleStatusKind.DamageDown));
                }
                else total += summon.power * living.Average(foe => (1 - foe.defense) * e.Catalog.Enemy(foe.definitionId).retaliation * (1 - foe.weakness) * (1 - BattleEngine.Status(foe.statuses, BattleStatusKind.DamageDown)));
            }
            float reduction = Mathf.Min(e.Catalog.rules.reductionCap, e.State.player.reduction + BattleEngine.Status(e.State.player.statuses, BattleStatusKind.DamageReduction));
            float ongoingFactor = (1 - e.State.player.weakness) * (1 - BattleEngine.Status(e.State.player.statuses, BattleStatusKind.DamageDown)) * (1 + BattleEngine.Status(e.State.player.statuses, BattleStatusKind.DamageUp) + BattleEngine.Status(e.State.player.statuses, BattleStatusKind.HeavenMomentum));
            return total * ongoingFactor * (1 - reduction) * (1 + e.State.player.exposure + BattleEngine.Status(e.State.player.statuses, BattleStatusKind.IncomingUp) + BattleEngine.Status(e.State.player.statuses, BattleStatusKind.DefenseDown));
        }
        private static int Percentile(List<int> values, float percent) => values[Math.Min(values.Count - 1, (int)Math.Floor((values.Count - 1) * percent))];
        private static int Get(Dictionary<string, int> values, string key) => values.TryGetValue(key, out var count) ? count : 0;
        private static void Increment(Dictionary<string, int> values, string key) => values[key] = Get(values, key) + 1;
        private static void Increment(List<Count> values, string key) { var item = values.Find(x => x.name == key); if (item == null) values.Add(item = new Count { name = key }); item.count++; }
        private static void Check(Report report, string name, bool passed, string observed) => report.checks.Add(new Acceptance { name = name, passed = passed, observed = observed });
        private static void WriteReport(Report report)
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Validation/skill-table-balance-report.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, JsonUtility.ToJson(report, true));
        }
    }
}
#endif

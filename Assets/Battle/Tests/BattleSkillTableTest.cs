#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emerge.Checks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Emerge.Battle.Tests
{
    public static class BattleSkillTableTest
    {
        [Serializable] public sealed class Check { public string name, observed; public bool passed; }
        [Serializable] public sealed class Report
        { public bool passed; public string unityVersion, version, completedUtc; public List<Check> checks = new List<Check>(); }
        private static BattleCatalog catalog, fixtureCatalog;
        private static Report report;
        private static readonly List<UnityEngine.Object> owned = new List<UnityEngine.Object>();
        public static Report LastReport { get; private set; }
        public static bool RunChecks()
        {
            report = LastReport = new Report { unityVersion = Application.unityVersion };
            try
            {
                catalog = Resources.Load<BattleCatalog>(BattleCatalog.ResourcePath);
                if (catalog == null) throw new InvalidOperationException("Missing battle catalog");
                report.version = catalog.rules.balanceVersion; fixtureCatalog = CloneFixture(catalog);
                foreach (var stage in new Action[] { Configuration, EnhancementAndCosts, EverySkill, RandomAndPersistence, DurationsAndTargets, BuffsAndReflection, StateAndSaveValidation, LegacySaveRegression })
                    try { stage(); } catch (Exception exception) { Add(stage.Method.Name + " completes", false, exception.ToString()); }
            }
            catch (Exception exception) { Add("Fixture initializes", false, exception.ToString()); }
            finally { foreach (var asset in owned) if (asset != null) UnityEngine.Object.DestroyImmediate(asset); owned.Clear(); }
            report.passed = report.checks.Count > 0 && report.checks.All(c => c.passed); report.completedUtc = DateTime.UtcNow.ToString("O");
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Validation/skill-table-test-report.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, JsonUtility.ToJson(report, true));
            Debug.Log("SKILL_TABLE_VALIDATION_" + (report.passed ? "PASS" : "FAIL") + " checks=" + report.checks.Count);
            return report.passed;
        }
        private static BattleCatalog CloneFixture(BattleCatalog source)
        {
            var clone = UnityEngine.Object.Instantiate(source); owned.Add(clone);
            clone.encounters = source.encounters.Select(original =>
            {
                var encounter = UnityEngine.Object.Instantiate(original); owned.Add(encounter);
                encounter.enemies = original.enemies.Select(slot => new BattleEnemySlot { enemy = slot.enemy, healthOverride = 5000 }).ToArray();
                return encounter;
            }).ToArray();
            return clone;
        }
        private static ActorCheckAttributes Build(BattleFamily family, int points = 3)
        {
            var attributes = new ActorCheckAttributes();
            switch (family)
            {
                case BattleFamily.Officer: attributes.officer = points; attributes.wealth = 8 - points; break;
                case BattleFamily.Parent: attributes.parent = points; attributes.officer = 8 - points; break;
                case BattleFamily.Wealth: attributes.wealth = points; attributes.officer = 8 - points; break;
                case BattleFamily.Offspring: attributes.offspring = points; attributes.officer = 8 - points; break;
                case BattleFamily.Sibling: attributes.sibling = points; attributes.officer = 8 - points; break;
            }
            return attributes;
        }
        private static BattleSkillDefinition Skill(BattleSkillKind kind) => catalog.skills.Single(s => s.kind == kind);
        private static BattleEngine New(BattleSkillKind kind, int points = 3, string encounter = "ENC01", int round = 1)
        {
            var skill = Skill(kind); var attributes = Build(skill.family, points);
            for (int seed = 1; seed <= 10000; seed++)
            {
                var engine = new BattleEngine(fixtureCatalog, null) { EmitRuntimeLogs = false };
                engine.Start(fixtureCatalog.Encounter(encounter), seed, attributes: attributes);
                BattleSkillTableBalanceTest.Reveal(engine);
                while (engine.State.round < round) Advance(engine, true);
                if (skill.alwaysAvailable || skill.isPassive || (skill.isUltimate && round < BattleSkillTableRules.UltimateFirstRound) || engine.State.unlockedSkills.Contains(skill.id)) return engine;
            }
            throw new InvalidOperationException("No offered fixture: " + kind);
        }
        private static void Advance(BattleEngine engine, bool harmless = false)
        {
            if (harmless) foreach (var enemy in engine.State.enemies) enemy.stunned = true;
            if (!engine.EndTurn()) throw new InvalidOperationException("Cannot end turn");
            while (engine.State.phase == BattlePhase.Enemy) engine.StepEnemy();
            BattleSkillTableBalanceTest.Reveal(engine);
        }
        private static void Cast(BattleEngine engine, BattleSkillKind kind, int target = 0)
        { if (!engine.CommitSkill(Skill(kind).id, target, out var reason)) throw new InvalidOperationException(kind + ": " + reason); }
        private static void Configuration()
        {
            Add("Shipping catalog validates", catalog.Validate(out var error), error);
            var authored = catalog.skills.Where(s => s.kind != BattleSkillKind.Legacy).ToArray();
            Add("Thirty authored identities appear once alongside three permanent commands", authored.Length == 30 && authored.Select(s => s.kind).Distinct().Count() == 30 && catalog.skills.Count(s => s.kind == BattleSkillKind.Legacy && s.alwaysAvailable) == 3);
            Add("Full active, ultimate and passive groups are installed", authored.Count(s => !s.isUltimate && !s.isPassive) == 24 && authored.Count(s => s.isUltimate) == 4 && authored.Count(s => s.isPassive) == 2);
            Add("Active actions require MP; talents are free and passive", catalog.skills.All(s => s.isPassive ? s.mpCost == 0 : s.mpCost > 0));
            Add("Ultimates and passives never have an enhanced version", catalog.skills.Where(s => s.isUltimate || s.isPassive).All(s => !s.enhancedAvailable));
            Add("Documented durations are four and six rounds", BattleSkillTableRules.CloneRounds == 4 && BattleSkillTableRules.TripleChangeRounds == 6);
        }
        private static void EnhancementAndCosts()
        {
            var kind = BattleSkillKind.ThunderMark; var skill = Skill(kind);
            var below = New(kind, 2); Add("Two points cannot enhance an offered skill", below.State.unlockedSkills.Contains(skill.id) && !below.IsEnhanced(skill.id));
            var enhanced = New(kind, 3); Add("Three points plus current offer enables enhancement", enhanced.IsEnhanced(skill.id));
            int mana = enhanced.State.player.mp, actions = enhanced.State.actionSerial; Cast(enhanced, kind);
            Add("Enhancement costs one action and one MP payment", enhanced.State.player.mp == mana - skill.mpCost && enhanced.State.actionSerial == actions + 1 && enhanced.State.lastAction.enhanced);
            Add("Enhanced use is recorded once and immediately downgrades", enhanced.State.enhancedSkills.Count(id => id == skill.id) == 1 && !enhanced.IsEnhanced(skill.id));
            if (enhanced.State.player.mp >= skill.mpCost && enhanced.CanUseSkill(skill.id, 0, out _))
            {
                mana = enhanced.State.player.mp; Cast(enhanced, kind);
                Add("Later use is the ordinary version at the same cost", !enhanced.State.lastAction.enhanced && enhanced.State.player.mp == mana - skill.mpCost && enhanced.State.enhancedSkills.Count(id => id == skill.id) == 1);
            }
            else Add("Fixture supports an ordinary second use", false, "Cost=" + skill.mpCost + " remaining=" + enhanced.State.player.mp);
            var saved = enhanced.Capture(); var continued = new BattleEngine(fixtureCatalog, null) { EmitRuntimeLogs = false };
            Add("Save/load preserves the spent enhancement and whole-battle quota", continued.Restore(saved) && !continued.IsEnhanced(skill.id) && continued.RemainingUses(skill.id) == enhanced.RemainingUses(skill.id));
            Advance(continued, true); Add("A later round never rearms a spent enhancement", !continued.IsEnhanced(skill.id) && continued.State.enhancedSkills.Count(id => id == skill.id) == 1);
            var shortage = New(kind, 3); shortage.State.player.mp = skill.mpCost - 1;
            string before = JsonUtility.ToJson(shortage.Capture());
            Add("Insufficient MP rejects without RNG, quota or enhancement mutation", !shortage.CommitSkill(skill.id, 0, out _) && before == JsonUtility.ToJson(shortage.Capture()) && shortage.IsEnhanced(skill.id));
            var invalid = New(kind, 3); before = JsonUtility.ToJson(invalid.Capture());
            Add("Invalid target rejects without payment or mutation", !invalid.CommitSkill(skill.id, 99, out _) && before == JsonUtility.ToJson(invalid.Capture()));
            var cap = New(kind, 3);
            for (int rounds = 0; cap.RemainingUses(skill.id) > 0 && rounds < 50; rounds++)
            {
                while (cap.CanUseSkill(skill.id, 0, out _)) Cast(cap, kind);
                if (cap.RemainingUses(skill.id) > 0) Advance(cap, true);
            }
            before = JsonUtility.ToJson(cap.Capture());
            Add("Whole-battle quota blocks further uses after real paid casts", skill.maximumUses > 0 && cap.RemainingUses(skill.id) == 0 && cap.State.skillUses.Single(u => u.skillId == skill.id).count == skill.maximumUses && !cap.CommitSkill(skill.id, 0, out _) && before == JsonUtility.ToJson(cap.Capture()) && BattleEngine.ValidateSnapshot(cap.Capture(), fixtureCatalog));
            var ordinary = New(kind, 3); ordinary.State.unlockedSkills.Remove(skill.id);
            Add("Enhancement does not unlock an unoffered skill", !ordinary.IsEnhanced(skill.id) && !ordinary.CommitSkill(skill.id, 0, out _));
            foreach (var ultimate in catalog.skills.Where(s => s.isUltimate))
            {
                var early = New(ultimate.kind, 3);
                Add("Ultimate cannot be submitted before round three: " + ultimate.kind, !early.CommitSkill(ultimate.id, 0, out _) && !early.IsEnhanced(ultimate.id));
            }
        }
        private static void EverySkill()
        {
            foreach (var skill in catalog.skills.Where(s => s.kind != BattleSkillKind.Legacy))
            {
                try
                {
                    var engine = New(skill.kind, 3, round: skill.isUltimate ? BattleSkillTableRules.UltimateFirstRound : 1);
                    if (skill.isPassive)
                    {
                        string before = JsonUtility.ToJson(engine.Capture());
                        Add("Passive talent cannot be clicked: " + skill.kind, !engine.CommitSkill(skill.id, 0, out _) && before == JsonUtility.ToJson(engine.Capture())); continue;
                    }
                    engine.State.player.hp = 45; engine.State.player.burn = 5; engine.State.player.burnTicks = 2;
                    int mana = engine.State.player.mp; Cast(engine, skill.kind);
                    Add("Authored action executes with one MP payment: " + skill.kind, engine.State.lastAction.skillId == skill.id && engine.State.player.mp >= 0 && engine.State.player.mp <= mana && engine.State.actionSerial == 1,
                        "MP=" + mana + "→" + engine.State.player.mp + "; action=" + engine.State.lastAction.value);
                    Add("Executed authored action remains save-valid: " + skill.kind, BattleEngine.ValidateSnapshot(engine.Capture(), fixtureCatalog));
                }
                catch (Exception exception) { Add("Authored action executes: " + skill.kind, false, exception.Message); }
            }
        }
        private static void RandomAndPersistence()
        {
            var fate = New(BattleSkillKind.SixLineFateGu); var snapshot = fate.Capture();
            var resumed = new BattleEngine(fixtureCatalog, null) { EmitRuntimeLogs = false };
            Add("Version-four active snapshot validates and restores", snapshot.session.version == 4 && BattleEngine.ValidateSnapshot(snapshot, fixtureCatalog) && resumed.Restore(snapshot));
            var coins = fate.State.roundDivination.casting.coinFaces.ToArray(); int serial = fate.State.randomSerial;
            Cast(fate, BattleSkillKind.SixLineFateGu); Cast(resumed, BattleSkillKind.SixLineFateGu);
            Add("Independent effect RNG does not reroll the six-line chart", fate.State.randomSerial > serial && coins.SequenceEqual(fate.State.roundDivination.casting.coinFaces));
            Add("Save/reload reproduces fate, damage and RNG cursor", JsonUtility.ToJson(fate.Capture()) == JsonUtility.ToJson(resumed.Capture()));
            var critical = New(BattleSkillKind.MetalSever); snapshot = critical.Capture(); resumed = new BattleEngine(fixtureCatalog, null) { EmitRuntimeLogs = false };
            Add("Precritical snapshot restores", resumed.Restore(snapshot));
            Cast(critical, BattleSkillKind.MetalSever); Cast(resumed, BattleSkillKind.MetalSever);
            Add("Save/reload reproduces critical result and complete state", critical.State.lastAction.critical == resumed.State.lastAction.critical && JsonUtility.ToJson(critical.Capture()) == JsonUtility.ToJson(resumed.Capture()));
            Add("Post-action fate and critical snapshots remain valid", BattleEngine.ValidateSnapshot(fate.Capture(), fixtureCatalog) && BattleEngine.ValidateSnapshot(critical.Capture(), fixtureCatalog));
            Reject(fate.Capture(), "forged auspicious flag", copy => copy.session.lastAction.auspicious = !copy.session.lastAction.auspicious);
            Reject(critical.Capture(), "forged critical flag", copy => copy.session.lastAction.critical = !copy.session.lastAction.critical);
            int good = 0, bad = 0, crit = 0, ordinary = 0;
            var fateSkill = Skill(BattleSkillKind.SixLineFateGu); var strike = Skill(BattleSkillKind.MetalSever);
            for (int seed = 5001; seed <= 5300; seed++)
            {
                var engine = new BattleEngine(fixtureCatalog, null) { EmitRuntimeLogs = false }; engine.Start(fixtureCatalog.Encounter("ENC01"), seed, attributes: Build(fateSkill.family));
                BattleSkillTableBalanceTest.Reveal(engine);
                if (engine.CanUseSkill(fateSkill.id, 0, out _)) { Cast(engine, fateSkill.kind); if (engine.State.lastAction.auspicious) good++; else bad++; }
                engine = new BattleEngine(fixtureCatalog, null) { EmitRuntimeLogs = false }; engine.Start(fixtureCatalog.Encounter("ENC01"), seed, attributes: Build(strike.family)); BattleSkillTableBalanceTest.Reveal(engine);
                if (engine.CanUseSkill(strike.id, 0, out _)) { Cast(engine, strike.kind); if (engine.State.lastAction.critical) crit++; else ordinary++; }
            }
            Add("Fate and critical mechanics expose both outcomes across seeds", good > 0 && bad > 0 && crit > 0 && ordinary > 0, "good=" + good + "; bad=" + bad + "; critical=" + crit + "; ordinary=" + ordinary);
        }
        private static void DurationsAndTargets()
        {
            var clone = New(BattleSkillKind.WaterClone); int cloneStartHP = clone.State.enemies[0].hp; Cast(clone, BattleSkillKind.WaterClone);
            Add("Water clone has no additional immediate attack", clone.State.enemies[0].hp == cloneStartHP);
            Add("Water clone is summoned for exactly four rounds", clone.State.summons.Any(s => s.kind == BattleSummonKind.Clone && s.remainingRounds == 4));
            var cloneTicks = new List<int>();
            for (int i = 1; i <= 4; i++)
            {
                int beforeTick = clone.State.enemies[0].hp;
                Advance(clone, true);
                cloneTicks.Add(beforeTick - clone.State.enemies[0].hp);
                Add("Clone duration after " + i + " completed rounds", i < 4 ? clone.State.summons.Any(s => s.kind == BattleSummonKind.Clone && s.remainingRounds == 4 - i) : clone.State.summons.All(s => s.kind != BattleSummonKind.Clone));
            }
            Add("Both enhanced clones attack once on each of their four rounds", cloneTicks.All(x => x > 0) && cloneTicks.Distinct().Count() == 1 && clone.State.summons.All(s => s.kind != BattleSummonKind.Clone));
            var nursery = New(BattleSkillKind.GuNursery); int nurseryHP = nursery.State.enemies[0].hp; Cast(nursery, BattleSkillKind.GuNursery);
            Add("Gu nursery begins with a summon and no immediate attack", nursery.State.enemies[0].hp == nurseryHP && nursery.State.summons.Any(s => s.kind == BattleSummonKind.GuNest && s.remainingRounds == 3));
            nursery.State.player.shield = 10; nursery.State.player.shieldRounds = 3;
            for (int i = 0; i < 3; i++) Advance(nursery, true);
            Add("Enhanced nursery ends after three ticks and clears the player's shield", nursery.State.summons.All(s => s.kind != BattleSummonKind.GuNest) && nursery.State.player.shield == 0);
            var domain = New(BattleSkillKind.TripleChange); Cast(domain, BattleSkillKind.TripleChange);
            Add("Three-line domain begins at six rounds", domain.State.domainRounds == 6);
            for (int i = 1; i <= 6; i++) { Advance(domain, true); Add("Domain duration after " + i + " completed rounds", domain.State.domainRounds == 6 - i); }
            var blades = New(BattleSkillKind.WindBlades, encounter: "ENC03"); Cast(blades, BattleSkillKind.WindBlades);
            var summoned = blades.State.summons.Where(s => s.kind == BattleSummonKind.WindBlade).ToArray();
            Add("Wind blades create one pending strike per living enemy", summoned.Length == 3 && summoned.Select(s => s.targetIndex).Distinct().Count() == 3);
            int[] hp = blades.State.enemies.Select(e => e.hp).ToArray(); Advance(blades, true);
            Add("Every wind blade hits its own enemy once in the first round", blades.State.enemies.Select((e, i) => e.hp < hp[i]).All(x => x) && blades.State.summons.Where(s => s.kind == BattleSummonKind.WindBlade).All(s => s.remainingRounds == 1));
            int[] firstDamage = blades.State.enemies.Select((e, i) => hp[i] - e.hp).ToArray();
            hp = blades.State.enemies.Select(e => e.hp).ToArray(); Advance(blades, true);
            Add("Each surviving enemy receives one equal further strike on round two", blades.State.enemies.Select((e, i) => hp[i] - e.hp).SequenceEqual(firstDamage) && blades.State.summons.All(s => s.kind != BattleSummonKind.WindBlade));
            hp = blades.State.enemies.Select(e => e.hp).ToArray(); Advance(blades, true);
            Add("Wind blades stop after their authored duration", hp.SequenceEqual(blades.State.enemies.Select(e => e.hp)));
            var shadow = New(BattleSkillKind.ShadowMark, encounter: "ENC03"); hp = shadow.State.enemies.Select(e => e.hp).ToArray(); Cast(shadow, BattleSkillKind.ShadowMark);
            Add("Enhanced shadow applies all enemy curses without immediate damage", hp.SequenceEqual(shadow.State.enemies.Select(e => e.hp)) && shadow.State.enemies.All(e => BattleEngine.Status(e.statuses, BattleStatusKind.ShadowCurse) > 0) && !shadow.State.lastAction.critical);
            int randomBefore = shadow.State.randomSerial; Advance(shadow, true);
            Add("Damage-over-time ticks every cursed enemy without critical RNG", shadow.State.enemies.Select((e, i) => hp[i] > e.hp).All(x => x) && shadow.State.randomSerial == randomBefore);
        }
        private static void BuffsAndReflection()
        {
            var steal = New(BattleSkillKind.StealHexagram); var enemy = steal.State.enemies[0];
            enemy.shield = 20; enemy.statuses.Add(new BattleTimedStatus { kind = BattleStatusKind.DamageUp, power = .1f, rounds = 3 });
            enemy.statuses.Add(new BattleTimedStatus { kind = BattleStatusKind.DamageReduction, power = .2f, rounds = 3 });
            enemy.statuses.Add(new BattleTimedStatus { kind = BattleStatusKind.Burn, power = 5, rounds = 2 });
            Cast(steal, BattleSkillKind.StealHexagram);
            Add("Enhanced steal transfers exactly two enemy buffs", steal.State.player.shield == 20 && BattleEngine.Status(steal.State.player.statuses, BattleStatusKind.DamageUp) == .1f && enemy.shield == 0 && BattleEngine.Status(enemy.statuses, BattleStatusKind.DamageUp) == 0 && BattleEngine.Status(enemy.statuses, BattleStatusKind.DamageReduction) == .2f);
            Add("Steal never transfers negative enemy states", BattleEngine.Status(enemy.statuses, BattleStatusKind.Burn) == 5 && BattleEngine.Status(steal.State.player.statuses, BattleStatusKind.Burn) == 0);
            Add("Enhanced steal applies its incoming-damage tradeoff", BattleEngine.Status(steal.State.player.statuses, BattleStatusKind.IncomingUp) == .1f);
            var dispel = New(BattleSkillKind.HeartLight); enemy = dispel.State.enemies[0]; enemy.shield = 60;
            enemy.statuses.Add(new BattleTimedStatus { kind = BattleStatusKind.DamageUp, power = .1f, rounds = 3 });
            enemy.statuses.Add(new BattleTimedStatus { kind = BattleStatusKind.Burn, power = 5, rounds = 2 });
            Cast(dispel, BattleSkillKind.HeartLight);
            Add("Dispel removes one buff layer while preserving other states", enemy.shield == 0 && BattleEngine.Status(enemy.statuses, BattleStatusKind.DamageUp) == .1f && BattleEngine.Status(enemy.statuses, BattleStatusKind.Burn) == 5);
            var reflect = New(BattleSkillKind.MysticArmor, encounter: "ENC05"); Cast(reflect, BattleSkillKind.MysticArmor);
            var definition = reflect.Catalog.Enemy(reflect.State.enemies[0].definitionId);
            reflect.State.enemies[0].intentSkillId = definition.skills.First(s => s.effect == EnemyEffect.Damage && s.mpCost == 0).id;
            int hp = reflect.State.enemies[0].hp, shield = reflect.State.player.shield, rng = reflect.State.randomSerial;
            reflect.EndTurn(); reflect.StepEnemy();
            Add("Shielded direct enemy hit can reflect without recursive retaliation", reflect.State.enemies[0].hp < hp && reflect.State.player.hp == 100 && reflect.State.player.shield < shield && reflect.State.randomSerial - rng == 1,
                "enemy loss=" + (hp - reflect.State.enemies[0].hp) + "; playerHP=" + reflect.State.player.hp + "; RNG advances=" + (reflect.State.randomSerial - rng));
        }
        private static void StateAndSaveValidation()
        {
            var original = New(BattleSkillKind.ThunderMark); var snapshot = original.Capture();
            Add("Valid snapshot baseline", BattleEngine.ValidateSnapshot(snapshot, fixtureCatalog));
            Reject(snapshot, "Negative MP", copy => copy.session.player.mp = -1);
            Reject(snapshot, "Negative status duration", copy => copy.session.player.statuses.Add(new BattleTimedStatus { kind = BattleStatusKind.DamageUp, power = .1f, rounds = -1 }));
            Reject(snapshot, "Nonfinite status magnitude", copy => copy.session.player.statuses.Add(new BattleTimedStatus { kind = BattleStatusKind.DamageUp, power = float.NaN, rounds = 2 }));
            Reject(snapshot, "Unknown status kind", copy => copy.session.player.statuses.Add(new BattleTimedStatus { kind = (BattleStatusKind)999, power = 1, rounds = 2 }));
            Reject(snapshot, "Duplicate enhanced IDs", copy => { copy.session.enhancedSkills.Add(Skill(BattleSkillKind.ThunderMark).id); copy.session.enhancedSkills.Add(Skill(BattleSkillKind.ThunderMark).id); });
            Reject(snapshot, "Unknown enhanced ID", copy => copy.session.enhancedSkills.Add("forged-skill"));
            Reject(snapshot, "Negative independent RNG cursor", copy => copy.session.randomSerial = -1);
            Reject(snapshot, "Invalid summon target", copy => copy.session.summons.Add(new BattleSummonState { kind = BattleSummonKind.WindBlade, remainingRounds = 1, power = 5, targetIndex = 99 }));
            Reject(snapshot, "Clone duration exceeds four rounds", copy => copy.session.summons.Add(new BattleSummonState { kind = BattleSummonKind.Clone, remainingRounds = 5, power = 5 }));
            Reject(snapshot, "Domain duration exceeds six rounds", copy => copy.session.domainRounds = 7);
            Reject(snapshot, "Altered offered skill set", copy => copy.session.unlockedSkills.Clear());
        }
        private static void Reject(BattleSnapshot original, string name, Action<BattleSnapshot> alter)
        {
            var copy = JsonUtility.FromJson<BattleSnapshot>(JsonUtility.ToJson(original)); alter(copy);
            Add("Strict snapshot validation rejects " + name, !BattleEngine.ValidateSnapshot(copy, fixtureCatalog));
        }
        private static void LegacySaveRegression()
        {
            var archive = Resources.Load<BattleCatalog>("Battle/LegacyV05/BattleCatalog");
            Add("Original version-five catalog remains available for existing saves", archive != null && archive.skills.Length == 11 && archive.rules.balanceVersion == "v0.5-counterplay");
            if (archive == null) return;
            var oldEngine = new BattleEngine(archive, null) { EmitRuntimeLogs = false };
            oldEngine.Start(archive.Encounter("ENC01"), 12637, attributes: BattleBuildRules.DefaultBuild());
            oldEngine.State.version = 3; BattleSkillTableBalanceTest.Reveal(oldEngine);
            var legacyObject = JObject.Parse(JsonUtility.ToJson(oldEngine.Capture()));
            RemoveUpgradeFields(legacyObject);
            string legacyJson = legacyObject.ToString();
            var snapshot = JsonUtility.FromJson<BattleSnapshot>(legacyJson);
            Add("Old JSON contains only the pre-upgrade version-three layout", snapshot.session.version == 3 && !legacyJson.Contains("statuses") && !legacyJson.Contains("enhancedSkills") && !legacyJson.Contains("summons") && !legacyJson.Contains("randomSerial") && !legacyJson.Contains("domainRounds"), "JSON bytes=" + legacyJson.Length);
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Validation/skill-table-legacy-v3-fixture.json")); File.WriteAllText(path, legacyJson);
            Add("Pre-upgrade JSON validates against the archived configuration", BattleEngine.ValidateSnapshot(snapshot, archive));
            Add("Main catalog resolves old battle JSON to its original rules", BattleEngine.ValidateSnapshot(snapshot, catalog));
            var resumed = new BattleEngine(archive, null) { EmitRuntimeLogs = false };
            Add("Old version-three JSON restores without migration or reroll", resumed.Restore(snapshot) && resumed.State.version == 3 && resumed.State.unlockedSkills.SequenceEqual(oldEngine.State.unlockedSkills));
            int mp = resumed.State.player.mp, enemyHP = resumed.State.enemies[0].hp;
            Add("Restored old battle can still use its original basic attack", resumed.CommitSkill("ATK_BASIC", 0, out _) && resumed.State.player.mp == mp - archive.Skill("ATK_BASIC").mpCost && resumed.State.enemies[0].hp < enemyHP && BattleEngine.ValidateSnapshot(resumed.Capture(), archive));
        }
        private static void RemoveUpgradeFields(JToken token)
        {
            var addedFields = new HashSet<string> { "statuses", "shieldRounds", "criticalTalentRound", "criticalCharge", "defense", "enhancedSkills", "summons", "randomSerial", "domainRounds", "domainEnhanced", "enhanced", "critical", "auspicious", "randomSerialBefore" };
            if (token is JObject obj)
                foreach (var property in obj.Properties().ToArray()) { if (addedFields.Contains(property.Name)) property.Remove(); else RemoveUpgradeFields(property.Value); }
            else if (token is JArray array) foreach (var item in array) RemoveUpgradeFields(item);
        }
        private static void Add(string name, bool passed, string observed = null)
        { report.checks.Add(new Check { name = name, passed = passed, observed = observed ?? "" }); if (!passed) Debug.LogWarning("SKILL_TABLE_CHECK_FAIL " + name + " " + observed); }
    }
}
#endif

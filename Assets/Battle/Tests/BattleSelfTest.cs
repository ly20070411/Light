#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emerge.Checks.Divination;
using Emerge.Checks;
using Emerge.GameFlow;
using Emerge.Props;
using UnityEngine;
using UnityEngine.UI;

namespace Emerge.Battle.Tests
{
    public sealed class BattleSelfTest : MonoBehaviour
    {
        [Serializable] public sealed class Check { public string name, observed; public bool passed; }
        [Serializable] public sealed class Report { public bool passed; public string unityVersion, completedUtc; public List<Check> checks = new List<Check>(); }
        public static event Action<bool> Completed, PreviewCompleted;
        private Report report;
        private BattleCatalog catalog;
        private readonly List<GameObject> actors = new List<GameObject>();
        public void RunChecks() { report = new Report { unityVersion = Application.unityVersion }; StartCoroutine(Run()); }
        private IEnumerator Run()
        {
            yield return null;
            catalog = Resources.Load<BattleCatalog>(BattleCatalog.ResourcePath);
            foreach (var stage in new Action[] { Configuration, Commands, Statuses, Persistence, BuildRules, Retaliation, EncounterSmoke })
                try { stage(); } catch (Exception e) { Add(stage.Method.Name + " completes", false, e.ToString()); }
            var ui = UI();
            while (true)
            {
                bool next = false; object current = null;
                try { next = ui.MoveNext(); if (next) current = ui.Current; }
                catch (Exception e) { Add("UI tests complete", false, e.ToString()); }
                if (!next) break; yield return current;
            }
            foreach (var go in actors) if (go != null) Destroy(go);
            report.passed = report.checks.Count > 0 && report.checks.All(c => c.passed); report.completedUtc = DateTime.UtcNow.ToString("O");
            Directory.CreateDirectory("Validation"); File.WriteAllText("Validation/battle-test-report.json", JsonUtility.ToJson(report, true));
            Debug.Log("BATTLE_VALIDATION_" + (report.passed ? "PASS" : "FAIL") + " checks=" + report.checks.Count); Completed?.Invoke(report.passed);
        }
        private void Configuration()
        {
            Add("Catalog validates", catalog != null && catalog.Validate(out _));
            Add("Initial authored counts", catalog.skills.Length == 11 && catalog.enemies.Length == 4 && catalog.items.Length == 4 && catalog.encounters.Length == 5);
            Add("All actions cost energy", catalog.skills.All(s => s.mpCost > 0));
            Add("Three permanent skills", catalog.skills.Where(s => s.alwaysAvailable).Select(s => s.id).OrderBy(x => x).SequenceEqual(new[] { "ATK_BASIC", "DEF_GUARD", "MP_CLEANSE" }));
            Add("Advanced skills are finite", catalog.skills.Where(s => !s.alwaysAvailable).All(s => s.maximumUses > 0));
            Add("Strong attacks have small whole-battle quotas", catalog.Skill("ATK_HEAVY").maximumUses == 3 && catalog.Skill("ATK_SWEEP").maximumUses == 2);
            Add("Five score multipliers", catalog.rules.multipliers.SequenceEqual(new[] { .6f, .8f, 1f, 1.2f, 1.4f }));
            Add("Positive midpoint rounding", BattleRules.Round(10.5f) == 11 && BattleRules.Round(10.49f) == 10);
            Add("Encounter sizes", catalog.encounters.Select(e => e.enemies.Length).SequenceEqual(new[] { 1, 2, 3, 2, 1 }));
            Add("Increased ordinary enemy HP", catalog.Enemy("E01").maxHP >= 75 && catalog.Enemy("E02").maxHP >= 100 && catalog.Enemy("E03").maxHP >= 65);
            Add("Boss has telegraphed charge and cooldown", catalog.Enemy("B01").resistsStun && catalog.Enemy("B01").skills.Any(s => s.effect == EnemyEffect.Charge) && catalog.Enemy("B01").skills.Any(s => s.effect == EnemyEffect.ChargedDamage));
            var art = Resources.Load<BattlePresentation>(BattlePresentation.ResourcePath);
            Add("Replaceable placeholders remain installed", art != null && art.heroPortrait != null && art.coinFront != null && catalog.enemies.All(e => e.battlePortrait != null));
        }
        private BattleEngine New(string encounter = "ENC01", int seed = 12637, bool open = true, ActorCheckAttributes attributes = null)
        {
            var go = new GameObject("Battle fixture"); actors.Add(go); var inventory = go.AddComponent<PropGameState>();
            foreach (var i in catalog.items) inventory.AddItem(i.inventoryKey, i.displayName, 2);
            var engine = new BattleEngine(catalog, inventory) { EmitRuntimeLogs = false }; engine.Start(catalog.Encounter(encounter), seed, attributes: attributes); if (open) Open(engine); return engine;
        }
        private static void Open(BattleEngine e) => BattleBalanceTest.Reveal(e);
        private void Cast(BattleEngine e, string id, int target = 0)
        { if (!e.CommitSkill(id, target, out var reason)) throw new InvalidOperationException(id + ": " + reason); }
        private static void Round(BattleEngine e) { e.EndTurn(); while (e.State.phase == BattlePhase.Enemy) e.StepEnemy(); Open(e); }
        private int OfferSeed(string id, string encounter = "ENC01", int minimumScore = -2, bool exact = false)
        {
            var d = catalog.Encounter(encounter);
            for (int seed = 1; seed < 10000; seed++)
            {
                var coins = CoinCasting.Cast(BattleEngine.RoundSeed(seed, 1));
                var record = new DivinationRecord { month = d.month, day = d.day, casting = coins, chart = new LiuYaoPaiPan().PaiPan(d.month, d.day, coins.yaoValues) };
                int score = BattleEngine.FamilyScore(record, catalog.Skill(id).family);
                if ((exact ? score == minimumScore : score >= minimumScore) && BattleBuildRules.SelectOffers(catalog, record, new List<EnemySkillUses>(), BattleBuildRules.DefaultBuild()).Contains(id)) return seed;
            }
            throw new Exception("No offered fixture: " + id);
        }
        private void Commands()
        {
            var e = New(open: false);
            Add("Start gates actions behind round casting", e.State.version == 3 && e.State.phase == BattlePhase.RoundCasting && e.State.player.mp == 30 && e.State.actionSerial == 0);
            Add("Opening cannot consume items or skills or end", !e.CommitSkill("ATK_BASIC", 0, out _) && !e.UseItem("ITEM_BLAST", out _) && !e.EndTurn());
            Add("Cannot resolve an incomplete round", !e.ResolveRound());
            string intent = e.State.enemies[0].intentSkillId; Open(e);
            Add("Round reveal costs no MP and opens three distinct skills", e.State.phase == BattlePhase.Player && e.State.player.mp == 30 && e.State.unlockedSkills.Count == 3 && e.State.unlockedSkills.Distinct().Count() == 3);
            Add("Offers use the build-weighted pool", e.State.unlockedSkills.SequenceEqual(BattleBuildRules.SelectOffers(catalog, e.State.roundDivination, e.State.roundStartUses, e.State.attributes)));
            Add("Completed casting cannot replay", !e.ResolveRound() && !e.ResolveSkill() && !e.RevealLine());
            string locked = catalog.skills.First(s => !s.alwaysAvailable && !e.State.unlockedSkills.Contains(s.id)).id;
            Add("Locked command is rejected without payment", !e.CommitSkill(locked, 0, out _) && e.State.player.mp == 30 && e.State.actionSerial == 0);
            int hp = e.State.enemies[0].hp; Cast(e, "ATK_BASIC");
            Add("Basic uses fixed attribute multiplier", e.State.phase == BattlePhase.Player && hp - e.State.enemies[0].hp == 8 && e.State.player.mp == 27 && Mathf.Approximately(e.State.lastAction.multiplier, 1.2f));
            var coins = e.State.roundDivination.casting.coinFaces.ToArray(); Cast(e, "ATK_BASIC");
            Add("Multiple actions share round coins without reroll", e.State.player.mp == 24 && coins.SequenceEqual(e.State.lastAction.divination.casting.coinFaces));
            Add("Actions do not change enemy intention", e.State.enemies[0].intentSkillId == intent && e.State.player.hp == 100);
            Cast(e, "DEF_GUARD"); Add("Defense includes fixed parent bonus", Mathf.Approximately(e.State.player.reduction, .42f));
            Add("Duplicate guard and invalid target consume nothing", !e.CommitSkill("DEF_GUARD", 0, out _) && !e.CommitSkill("ATK_BASIC", 9, out _) && e.State.player.mp == 24 - catalog.Skill("DEF_GUARD").mpCost);
            e.State.player.mp = 0; Add("No free infinite attacks", !e.CommitSkill("ATK_BASIC", 0, out _) && !e.CommitSkill("missing", 0, out _));
            e.UseItem("ITEM_MP", out _); Add("Item continues same turn without skill use", e.State.player.mp == 12 && e.State.phase == BattlePhase.Player && e.State.actionSerial == 3);
            e.EndTurn(); Add("Only explicit end advances enemy phase", e.State.phase == BattlePhase.Enemy && !e.EndTurn());
            e.StepEnemy(); Add("Enemy uses base damage with player defense", e.State.player.hp == 100 - BattleRules.Round(catalog.Enemy("E01").skills.First(s => s.id == intent).power * .58f));
            e.StepEnemy(); Add("New round restores natural MP and clears defense", e.State.round == 2 && e.State.phase == BattlePhase.RoundCasting && e.State.player.mp == Math.Min(30, 12 + catalog.rules.roundMana) && e.State.player.reduction == 0);
            e = New("ENC03", OfferSeed("ATK_SWEEP", "ENC03")); var before = e.State.enemies.Select(x => x.hp).ToArray(); Cast(e, "ATK_SWEEP");
            Add("AoE pays once and hits all enemies", e.State.actionSerial == 1 && e.State.player.mp == 18 && e.State.enemies.Select((x, i) => before[i] - x.hp).All(x => x == e.State.lastAction.value));
            e.State.player.mp = 30; Cast(e, "ATK_SWEEP"); int mp = e.State.player.mp;
            Add("Whole-battle cap rejects third sweep", e.RemainingUses("ATK_SWEEP") == 0 && !e.CommitSkill("ATK_SWEEP", 0, out _) && e.State.player.mp == mp);
            e.State.enemies[1].hp = 0; e.EndTurn(); e.StepEnemy(); int after = e.State.player.hp; e.StepEnemy();
            Add("Dead enemy skips exactly one cursor position", e.State.player.hp == after && e.State.enemyCursor == 2); e.StepEnemy(); e.StepEnemy(); Open(e);
            Add("Spent skill disappears from later offers", e.State.round == 2 && !e.State.unlockedSkills.Contains("ATK_SWEEP") && e.RemainingUses("ATK_SWEEP") == 0);
            e = New(seed: OfferSeed("ATK_HEAVY", minimumScore: 0)); Cast(e, "ATK_HEAVY"); float multiplier = e.State.lastAction.multiplier;
            Add("Advanced uses known round multiplier", multiplier == e.SkillMultiplier("ATK_HEAVY") && e.State.lastAction.score == 2 && Mathf.Approximately(multiplier, 1.2f));
            e.State.player.mp = 30; Cast(e, "ATK_HEAVY"); Add("Second heavy retains exact multiplier", e.State.lastAction.multiplier == multiplier && e.RemainingUses("ATK_HEAVY") == 1);
            var reloaded = New(); reloaded.Restore(e.Capture());
            Add("Reload keeps remaining whole-battle uses", reloaded.RemainingUses("ATK_HEAVY") == 1 && reloaded.State.unlockedSkills.SequenceEqual(e.State.unlockedSkills));
            e = New(); e.State.skillUses = catalog.skills.Where(s => !s.alwaysAvailable).Select(s => new EnemySkillUses { skillId = s.id, count = s.maximumUses, lastRound = 1 }).ToList(); e.State.actionSerial = e.State.skillUses.Sum(x => x.count); Round(e);
            Add("Exhausted advanced pool preserves permanent basic commands", e.State.unlockedSkills.Count == 0 && e.CanUseSkill("ATK_BASIC", 0, out _) && e.CanUseSkill("DEF_GUARD", 0, out _) && BattleEngine.ValidateSnapshot(e.Capture(), catalog));
        }
        private void Statuses()
        {
            var e = New(seed: OfferSeed("MP_BREATH")); Cast(e, "MP_BREATH"); int extra = e.State.player.nextMana;
            Add("Mana skill invests now and restores next round", e.State.player.mp == 26 && extra > 0); e.State.player.mp = 0; Round(e);
            Add("Delayed mana pays once", e.State.player.mp == Math.Min(30, catalog.rules.roundMana + extra) && e.State.player.nextMana == 0);
            e = New(seed: OfferSeed("HEAL_REGEN")); e.State.player.hp = 40; Cast(e, "HEAL_REGEN"); int regen = e.State.player.regeneration;
            Add("Regen has no immediate HP", e.State.player.hp == 40 && e.State.player.regenerationTicks == 3);
            for (int i = 0; i < 3; i++) { e.State.enemies[0].stunned = true; Round(e); }
            Add("Regen pays three times then expires", e.State.player.hp == Math.Min(100, 40 + 3 * regen) && e.State.player.regenerationTicks == 0 && e.State.player.regeneration == 0);
            e = New(); e.State.player.burn = catalog.rules.burnDamage; e.State.player.burnTicks = catalog.rules.burnTicks; Cast(e, "DEF_GUARD"); e.EndTurn();
            Add("Burn ticks at player end and ignores guard", e.State.player.hp == 100 - catalog.rules.burnDamage && e.State.player.burnTicks == 1);
            e = New(); var p = e.State.player; p.weakness = .2f; p.burn = 5; p.burnTicks = 2; p.exposure = .2f; p.exposureUntilRound = 2; Cast(e, "MP_CLEANSE");
            Add("Permanent finite cleanse removes all negative states", !BattleEngine.HasNegativeState(p) && p.burn == 0 && p.exposureUntilRound == 0 && e.RemainingUses("MP_CLEANSE") == 3);
            Add("Repeated empty cleanse is blocked", !e.CommitSkill("MP_CLEANSE", 0, out _));
            p.burn = 5; p.burnTicks = 2; e.UseItem("ITEM_CLEAN", out _); Add("Cleanse consumable has same status semantics", !BattleEngine.HasNegativeState(p));
            e = New("ENC02"); e.State.enemies[0].stunned = true; e.State.enemies[1].intentSkillId = "ember"; Round(e);
            Add("Burn gives a full player cleansing window", e.State.player.burnTicks == 2 && e.State.player.hp == 95);
            e = New("ENC02"); e.State.enemies[0].stunned = true; e.State.enemies[1].intentSkillId = "mark"; Round(e);
            Add("Exposure survives into next player phase", e.State.player.exposure == .2f && e.State.player.exposureUntilRound == 2);
            e.State.enemies[0].stunned = true; e.State.enemies[1].intentSkillId = "shock"; e.EndTurn(); e.StepEnemy(); e.StepEnemy();
            Add("Exposure amplifies next direct hit", e.State.player.hp == 100 - 4 - BattleRules.Round(8 * 1.2f)); e.StepEnemy();
            Add("Exposure expires after following enemy phase", e.State.player.exposure == 0 && e.State.player.exposureUntilRound == 0);
            e = New(seed: OfferSeed("ATK_HEAVY", minimumScore: 0)); Cast(e, "ATK_HEAVY"); int hp = e.State.enemies[0].hp; int hits = e.State.enemies[0].vulnerabilityHits; Cast(e, "ATK_BASIC");
            Add("Heavy vulnerability amplifies subsequent hit", hp - e.State.enemies[0].hp == BattleRules.Round(7 * 1.2f * 1.2f) && e.State.enemies[0].vulnerabilityHits == hits - 1);
            e = New(seed: OfferSeed("CTRL_BIND", minimumScore: 2)); Cast(e, "CTRL_BIND"); Round(e);
            Add("Strong bind skips action and gives determination", e.State.player.hp == 100 && e.State.enemies[0].determined);
            e = New("ENC05", OfferSeed("CTRL_BIND", "ENC05", 2)); Cast(e, "CTRL_BIND");
            Add("Boss stun resistance converts to weakening", !e.State.enemies[0].stunned && Mathf.Approximately(e.State.enemies[0].weakness, .22f));
            e = New("ENC05"); e.State.enemies[0].intentSkillId = "charge"; Round(e);
            Add("Charge telegraphs next-round release without damage", e.State.enemies[0].charged && e.State.enemies[0].intentSkillId == "release" && e.State.player.hp == 100);
            Cast(e, "DEF_GUARD"); e.EndTurn(); e.StepEnemy();
            Add("Release respects guard and exposes boss", e.State.player.hp == 100 - BattleRules.Round(42 * .58f) && !e.State.enemies[0].charged && e.State.enemies[0].vulnerabilityHits == 3 && e.State.enemies[0].vulnerability == .25f);
            e = New("ENC05", OfferSeed("CTRL_SEAL", "ENC05", 0)); e.State.enemies[0].charged = true; e.State.enemies[0].intentSkillId = "release"; Cast(e, "CTRL_SEAL"); e.EndTurn(); e.StepEnemy();
            Add("Seal downgrades release and consumes charge", e.State.player.hp == 82 && !e.State.enemies[0].charged && e.State.enemies[0].uses.Any(x => x.skillId == "tide"));
            bool varied = false, capped = true, cooldown = true;
            var intents = new HashSet<string>();
            for (int seed = 1; seed <= 100; seed++)
            {
                e = New("ENC03", seed); intents.Add(string.Join(",", e.State.enemies.Select(x => x.intentSkillId)));
                capped &= e.ForecastDamage() <= catalog.rules.enemyIntentBudget;
                e.State.enemies[0].intentSkillId = "heavy"; e.State.enemies[1].stunned = e.State.enemies[2].stunned = true; Round(e);
                cooldown &= e.State.enemies[0].intentSkillId != "heavy";
            }
            varied = intents.Count >= 5;
            Add("Enemy intentions vary across seeds", varied, "unique=" + intents.Count);
            Add("Team raw burst budget applies", capped); Add("Heavy cooldown prevents consecutive specials", cooldown);
        }
        private void BuildRules()
        {
            var defaults = BattleBuildRules.DefaultBuild();
            foreach (BattleFamily family in Enum.GetValues(typeof(BattleFamily)))
            {
                var build = new ActorCheckAttributes();
                switch (family)
                {
                    case BattleFamily.Parent: build.parent = 8; break;
                    case BattleFamily.Offspring: build.offspring = 8; break;
                    case BattleFamily.Officer: build.officer = 8; break;
                    case BattleFamily.Wealth: build.wealth = 8; break;
                    case BattleFamily.Sibling: build.sibling = 8; break;
                }
                var e = New(attributes: build);
                int available = catalog.skills.Count(s => !s.alwaysAvailable && s.family == family);
                Add("Single-family build excludes all zero-point skills: " + family,
                    e.State.unlockedSkills.Count == Math.Min(available, catalog.rules.advancedOptions) &&
                    e.State.unlockedSkills.All(id => catalog.Skill(id).family == family));
                Add("Single-family build has 1.8 multiplier: " + family,
                    catalog.skills.Where(s => s.family == family).All(s => Mathf.Approximately(e.SkillMultiplier(s.id), 1.8f)));
                build.parent = build.officer = build.offspring = build.wealth = build.sibling = 0;
                Add("Battle keeps an independent attribute snapshot: " + family, SixKinAttributes.IsValidLegacyBuild(e.State.attributes));
                string blocked = catalog.skills.First(s => !s.alwaysAvailable && s.family != family).id;
                Add("Zero-point skill cannot be manually submitted: " + family, !e.CommitSkill(blocked, 0, out _) && e.State.actionSerial == 0);
            }
            var offense = new ActorCheckAttributes { officer = 8 };
            var attack = New(attributes: offense);
            int hp = attack.State.enemies[0].hp;
            Cast(attack, "ATK_HEAVY");
            Add("Eight attack points produce fixed 36 heavy damage", hp - attack.State.enemies[0].hp == 36 && attack.State.lastAction.value == 36);
            Add("Heavy has fixed two-hit vulnerability without a moving-line bonus", attack.State.enemies[0].vulnerabilityHits == catalog.rules.vulnerabilityHits && !attack.State.lastAction.movingLine);
            var parent = New(attributes: new ActorCheckAttributes { parent = 8 }); Cast(parent, "DEF_GUARD");
            Add("High defense respects the cap and blocks redundant guard", Mathf.Approximately(parent.State.player.reduction, .5f) && !parent.CanUseSkill("DEF_GUARD", 0, out _));
            var ghost = New(attributes: new ActorCheckAttributes { sibling = 8 }); Cast(ghost, "CTRL_BIND");
            Add("Bind stuns ordinary enemies independently of chart score", ghost.State.enemies[0].stunned);
            var boss = New("ENC05", attributes: new ActorCheckAttributes { sibling = 8 }); Cast(boss, "CTRL_BIND");
            Add("Resistant bind has fixed attribute-scaled weakening", !boss.State.enemies[0].stunned && Mathf.Approximately(boss.State.enemies[0].weakness, .36f));
            Cast(boss, "CTRL_SEAL"); Add("Seal works independently of chart score", boss.State.enemies[0].silenced);

            var scores = new HashSet<int>(); bool stable = true;
            for (int seed = 1; seed <= 30; seed++)
            {
                var e = New(seed: seed, attributes: offense);
                scores.Add(BattleEngine.FamilyScore(e.State.roundDivination, BattleFamily.Officer));
                int before = e.State.enemies[0].hp; Cast(e, "ATK_HEAVY");
                stable &= before - e.State.enemies[0].hp == 36 && Mathf.Approximately(e.SkillMultiplier("ATK_HEAVY"), 1.8f);
            }
            Add("Different hexagrams never change damage or multiplier", scores.Count > 1 && stable, "distinct chart scores=" + scores.Count);

            var sampleCatalog = Instantiate(catalog); sampleCatalog.rules = Instantiate(catalog.rules); sampleCatalog.rules.advancedOptions = 1;
            var coins = CoinCasting.Cast(31);
            var record = new DivinationRecord { casting = coins, chart = new LiuYaoPaiPan().PaiPan("巳月", "戊子日", coins.yaoValues) };
            var buildA = new ActorCheckAttributes { officer = 4, parent = 2, wealth = 1, offspring = 1 };
            var buildB = new ActorCheckAttributes { officer = 1, parent = 5, wealth = 1, offspring = 1 };
            var counts = new Dictionary<BattleFamily, int>();
            foreach (BattleFamily f in Enum.GetValues(typeof(BattleFamily))) counts[f] = 0;
            int bAttack = 0; const int trials = 20000;
            for (int seed = 1; seed <= trials; seed++)
            {
                record.casting = new CoinCastResult { seed = seed };
                var a = BattleBuildRules.SelectOffers(sampleCatalog, record, new List<EnemySkillUses>(), buildA);
                counts[catalog.Skill(a[0]).family]++;
                var b = BattleBuildRules.SelectOffers(sampleCatalog, record, new List<EnemySkillUses>(), buildB);
                if (catalog.Skill(b[0]).family == BattleFamily.Officer) bAttack++;
            }
            float totalWeight = counts.Keys.Sum(f => buildA.Get(BattleBuildRules.Attribute(f)) * (4 + record.chart.yaos.Count(y => y.benLiuqin == BattleRules.FamilyName(f))));
            foreach (var family in counts.Keys)
            {
                float expected = buildA.Get(BattleBuildRules.Attribute(family)) * (4 + record.chart.yaos.Count(y => y.benLiuqin == BattleRules.FamilyName(family))) / totalWeight;
                float observed = counts[family] / (float)trials;
                Add("First-offer sampling matches family probability: " + family, Math.Abs(expected - observed) < .015f,
                    "expected=" + expected.ToString("P2") + "; observed=" + observed.ToString("P2") + "; trials=" + trials);
            }
            Add("Increasing attack allocation increases attack offer frequency", counts[BattleFamily.Officer] > bAttack * 2, "4 points=" + counts[BattleFamily.Officer] + "; 1 point=" + bAttack);
            Add("Zero control allocation stays impossible across 20000 seeds", counts[BattleFamily.Sibling] == 0);
            Destroy(sampleCatalog.rules); Destroy(sampleCatalog);

            var e3 = New(attributes: buildA, open: false); e3.RevealLine(); var saved = e3.Capture();
            var restored = New(attributes: offense); Add("Attribute battle snapshot restores", restored.Restore(saved));
            Open(e3); Open(restored); Add("Reload freezes build, lottery and multipliers", JsonUtility.ToJson(e3.State) == JsonUtility.ToJson(restored.State));
            saved = e3.Capture(); saved.session.attributes.self = 1; Add("Removed self attribute is rejected in v3 saves", !BattleEngine.ValidateSnapshot(saved, catalog));
            saved = e3.Capture(); saved.session.attributes.officer++; Add("Invalid point budget is rejected in v3 saves", !BattleEngine.ValidateSnapshot(saved, catalog));
            saved = e3.Capture(); saved.session.unlockedSkills.Add("CTRL_BIND"); Add("Injected zero-point offer is rejected on load", !BattleEngine.ValidateSnapshot(saved, catalog));
            var old = New(open: false).Capture(); old.session.version = 2; old.session.attributes = null;
            Add("Existing v2 round-casting battle remains readable", restored.Restore(old) && restored.State.version == 2);
            Open(restored); hp = restored.State.enemies[0].hp; Cast(restored, "ATK_BASIC");
            Add("Existing v2 battle retains original basic multiplier", hp - restored.State.enemies[0].hp == 7 && restored.State.lastAction.multiplier == 1 && BattleEngine.ValidateSnapshot(restored.Capture(), catalog));
        }

        private void Persistence()
        {
            var e = New("ENC03", open: false); e.RevealLine(); e.RevealLine(); var saved = e.Capture();
            Add("Partial round snapshot validates", BattleEngine.ValidateSnapshot(saved, catalog));
            var clone = New("ENC03"); clone.Restore(saved);
            Add("Restore freezes round coins without payment", clone.State.player.mp == 30 && clone.State.roundDivination.revealedLines == 2 && clone.State.roundDivination.casting.coinFaces.SequenceEqual(e.State.roundDivination.casting.coinFaces));
            Open(e); Open(clone); Add("Reload opens identical offers and multipliers", JsonUtility.ToJson(e.State) == JsonUtility.ToJson(clone.State));
            Cast(e, "ATK_BASIC"); clone.Restore(e.Capture()); Add("Immediate action snapshot has no pending replay", BattleEngine.ValidateSnapshot(e.Capture(), catalog) && !clone.ResolveSkill() && clone.State.actionSerial == 1 && clone.State.player.mp == 27);
            e.EndTurn(); e.StepEnemy(); clone.Restore(e.Capture()); e.StepEnemy(); clone.StepEnemy();
            Add("Enemy cursor reload never repeats enemy actions", JsonUtility.ToJson(e.State) == JsonUtility.ToJson(clone.State));
            saved.session.roundDivination.casting.coinFaces[0] ^= 1; Add("Tampered coins rejected", !BattleEngine.ValidateSnapshot(saved, catalog));
            saved = e.Capture(); saved.session.skillUses[0].count++; Add("Tampered use count rejected", !BattleEngine.ValidateSnapshot(saved, catalog));
            saved = e.Capture(); saved.session.unlockedSkills.Clear(); Add("Tampered offers rejected", !BattleEngine.ValidateSnapshot(saved, catalog));
            saved = e.Capture(); saved.session.player.mp = 31; Add("Invalid MP rejected", !BattleEngine.ValidateSnapshot(saved, catalog));
            saved = e.Capture(); saved.session.enemies[0] = null; Add("Null enemy rejected safely", !BattleEngine.ValidateSnapshot(saved, catalog));
            saved = e.Capture(); saved.session.balanceVersion = "other"; Add("Balance revisions do not mix snapshots", !BattleEngine.ValidateSnapshot(saved, catalog));
            var data = new GameSaveData { savedUtcTicks = DateTime.UtcNow.Ticks, scenePath = gameObject.scene.path, playerId = "fixture" };
            data.actors.Add(new SavedActor { id = "fixture", propState = clone.Inventory.CaptureSnapshot(), battleState = clone.Capture() });
            var store = new GameSaveStore(Path.GetFullPath("Validation/battle-save-test-v3"));
            bool write = store.TryWrite(SaveSlot.Manual, data, out var error); bool read = store.TryRead(SaveSlot.Manual, out var loaded, out var message);
            Add("Checksummed disk save round-trips v3 battle", write && read && loaded.actors[0].battleState.session.actionSerial == 1, error + message);
            e = New(); e.State.enemies[0].hp = 1; Cast(e, "ATK_BASIC"); e.ApplyOutcome(); e.ApplyOutcome(); var victory = e.Inventory.Victory("ENC01", catalog.rules.balanceVersion);
            Add("Victory records current/best rounds and counts once", victory != null && victory.wins == 1 && victory.lastRounds == 1 && victory.bestRounds == 1 && e.State.outcomeApplied);
            Add("First and swift achievements are persisted", e.Inventory.HasFlag("battle-achievement:first:" + catalog.rules.balanceVersion + ":ENC01") && e.Inventory.HasFlag("battle-achievement:swift:" + catalog.rules.balanceVersion + ":ENC01"));
            var wonSave = e.Capture(); clone.Inventory.RestoreJson(e.Inventory.CaptureJson()); clone.Restore(wonSave); clone.ApplyOutcome();
            Add("Loading completed victory cannot duplicate record", clone.Inventory.Victory("ENC01", catalog.rules.balanceVersion).wins == 1);
            data.actors[0].propState = e.Inventory.CaptureSnapshot(); data.actors[0].battleState = wonSave;
            write = store.TryWrite(SaveSlot.Manual, data, out error); read = store.TryRead(SaveSlot.Manual, out loaded, out message);
            Add("Victory records survive checksummed disk saves", write && read && loaded.actors[0].propState.battleVictories.Count == 1 && loaded.actors[0].propState.battleVictories[0].bestRounds == 1 && loaded.actors[0].propState.battleVictories[0].wins == 1);
            e.Inventory.RecordBattleVictory("ENC01", catalog.rules.balanceVersion, "second", 4, 3); victory = e.Inventory.Victory("ENC01", catalog.rules.balanceVersion);
            Add("Slower victory updates last and preserves best", victory.wins == 2 && victory.lastRounds == 4 && victory.bestRounds == 1);
            Add("Progress snapshot validates and restores", PropGameState.IsValidSnapshot(e.Inventory.CaptureSnapshot()) && clone.Inventory.RestoreJson(e.Inventory.CaptureJson()) && clone.Inventory.Victory("ENC01", catalog.rules.balanceVersion).lastRounds == 4);
            e = New(); e.State.player.hp = 1; Round(e); e.ApplyOutcome(); Add("Defeat never awards a victory", e.State.phase == BattlePhase.Defeat && e.Inventory.BattleVictories.Count == 0 && BattleEngine.ValidateSnapshot(e.Capture(), catalog));
            int seed1 = e.Inventory.LockBattleSeed("attempt", "ENC01", 11); int seed2 = e.Inventory.LockBattleSeed("attempt", "ENC01", 22);
            Add("Attempt seed remains locked until outcome", seed1 == 11 && seed2 == 11); e.Inventory.CompleteBattleAttempt("attempt", "ENC01");
            Add("Completed attempt accepts new randomness", e.Inventory.LockBattleSeed("attempt", "ENC01", 22) == 22);
            e.UseItem("ITEM_MP", out _); e.Start(catalog.Encounter("ENC01"), 99); Add("New battle is full with same inventory", e.State.player.hp == 100 && e.State.player.mp == 30);
            var old = new BattleLegacyEngine(catalog, e.Inventory); old.Start(catalog.Encounter("ENC01"), 123); old.CommitSkill("ATK_BASIC", 0, out _); old.RevealLine();
            var oldSave = old.Capture(); Add("v1 paid per-skill cast stays readable", BattleEngine.ValidateSnapshot(oldSave, catalog) && e.Restore(oldSave) && e.State.version == 1 && e.State.player.mp == 27 && e.State.pending.divination.revealedLines == 1);
            while (e.RevealLine()) { } Add("v1 pending action resolves once under old flow", e.ResolveSkill() && e.State.phase == BattlePhase.Player && !e.ResolveSkill());
            Add("Old inventory JSON without progress remains readable", e.Inventory.RestoreJson("{\"version\":1,\"inventory\":[],\"flags\":[],\"consumedInstances\":[]}"));
        }
        private void Retaliation()
        {
            Add("Boss uses the counterplay balance revision", catalog.rules.balanceVersion == "v0.5-counterplay" && catalog.Enemy("B01").maxHP == 400 && Mathf.Approximately(catalog.Enemy("B01").retaliation, .6f));
            var e = New("ENC05"); int predicted = e.ForecastRetaliation("ATK_BASIC", 0), hp = e.State.player.hp;
            Cast(e, "ATK_BASIC");
            Add("Unprotected hit reflects actual HP damage and matches preview", predicted == 5 && hp - e.State.player.hp == predicted && e.State.enemies[0].hp == 392);
            Add("Retaliation is recorded in the logic chain", e.State.log.Any(line => line.Contains("潮棘反震") && line.Contains("主角损失 5 HP")));
            var saved = e.Capture(); var clone = New("ENC05");
            Add("Retaliation damage survives save and load", BattleEngine.ValidateSnapshot(saved, catalog) && clone.Restore(saved) && clone.State.player.hp == 95 && clone.State.enemies[0].hp == 392);

            e = New("ENC05"); Cast(e, "DEF_GUARD"); e.State.player.shield = 2; hp = e.State.player.hp;
            predicted = e.ForecastRetaliation("ATK_BASIC", 0); Cast(e, "ATK_BASIC");
            Add("Guard and shield protect against immediate retaliation", predicted == 1 && hp - e.State.player.hp == 1 && e.State.player.shield == 0);
            e = New("ENC05"); e.State.enemies[0].shield = 20; hp = e.State.player.hp;
            predicted = e.ForecastRetaliation("ATK_BASIC", 0); Cast(e, "ATK_BASIC");
            Add("Enemy shield absorption does not trigger retaliation", predicted == 0 && e.State.player.hp == hp && e.State.enemies[0].hp == 400 && e.State.enemies[0].shield == 12);
            e = New("ENC05"); e.State.player.weakness = .2f; e.State.player.exposure = .2f; e.State.player.exposureUntilRound = 2;
            e.State.enemies[0].vulnerability = .25f; e.State.enemies[0].vulnerabilityHits = 3; hp = e.State.player.hp;
            predicted = e.ForecastRetaliation("ATK_BASIC", 0); Cast(e, "ATK_BASIC");
            Add("Retaliation preview includes weakness, vulnerability and exposure", predicted > 0 && hp - e.State.player.hp == predicted);
            e = New("ENC05"); hp = e.State.player.hp; predicted = e.ForecastRetaliation("ITEM_BLAST", 0, true);
            e.UseItem("ITEM_BLAST", out _);
            Add("Damage items cannot bypass retaliation", predicted == 6 && hp - e.State.player.hp == 6 && e.State.enemies[0].hp == 390);
            e = New("ENC05"); e.State.enemies[0].hp = 1; e.State.player.hp = 1;
            predicted = e.ForecastRetaliation("ATK_BASIC", 0); Cast(e, "ATK_BASIC");
            Add("Lethal hit still reflects only the remaining HP, simultaneous death loses", predicted == 1 && e.State.enemies[0].hp == 0 && e.State.player.hp == 0 && e.State.phase == BattlePhase.Defeat && BattleEngine.ValidateSnapshot(e.Capture(), catalog));
            e = New("ENC05", open: false); saved = e.Capture(); saved.session.version = 2; saved.session.attributes = null;
            e.Restore(saved); Open(e); Cast(e, "ATK_BASIC");
            Add("Version two battles do not acquire the new retaliation rule", e.State.version == 2 && e.State.player.hp == 100 && e.State.enemies[0].hp == 393);

            // Optimistically allow every integer hit size and unlimited MP. Even then, attacking
            // without defense costs more HP than 100 HP plus the two starter medical kits.
            int[] costs = new int[catalog.Enemy("B01").maxHP + 1];
            int maximumHit = catalog.skills.Where(s => s.effect == BattleEffect.Damage).Max(s => BattleRules.Round(s.power * 1.8f * 1.25f));
            for (int remaining = 1; remaining < costs.Length; remaining++)
            {
                costs[remaining] = int.MaxValue;
                for (int hit = 7; hit <= maximumHit; hit++)
                {
                    int actual = Math.Min(remaining, hit);
                    costs[remaining] = Math.Min(costs[remaining], costs[remaining - actual] + BattleRules.Round(actual * catalog.Enemy("B01").retaliation));
                }
            }
            int maximumHPBudget = catalog.rules.maxHP + 2 * catalog.Item("ITEM_MED").power;
            Add("Attack-only impossibility holds even with unlimited MP and the starter healing budget", costs.Last() > maximumHPBudget, "minimum reflected HP=" + costs.Last() + "; maximum HP budget=" + maximumHPBudget);
        }
        private void EncounterSmoke()
        {
            foreach (var encounter in catalog.encounters)
            {
                var e = New(encounter.id); e.Inventory.RestoreSnapshot(new PropGameState.Snapshot()); BattleBalanceTest.SeedKit(e.Inventory); BattleBalanceTest.Simulate(e, true);
                Add("Adaptive kit strategy wins " + encounter.id, e.State.phase == BattlePhase.Victory, "phase=" + e.State.phase + "; rounds=" + e.State.round);
                Add("Complete battle snapshot validates " + encounter.id, BattleEngine.ValidateSnapshot(e.Capture(), catalog));
                bool valid = true; int transitions = 0;
                for (int seed = 701; seed < 711; seed++)
                {
                    e = New(encounter.id, seed, false); e.Inventory.RestoreSnapshot(new PropGameState.Snapshot());
                    var sampled = e;
                    sampled.Changed += () => { transitions++; valid &= BattleEngine.ValidateSnapshot(sampled.Capture(), catalog); };
                    BattleBalanceTest.Simulate(sampled, true);
                }
                Add("All action boundaries validate across seeded fights " + encounter.id, valid && transitions > 100, "transitions=" + transitions);
            }
        }

        private IEnumerator UI()
        {
            var controller = FindObjectsOfType<BattleController>().First(c => c.gameObject.scene == gameObject.scene && c.GetComponent<PixelPrototype.PlayerMovement>() != null);
            controller.enabled = false;
            Add("Actual controller starts authored triple encounter", controller.TryBegin(catalog.Encounter("ENC03")));
            Add("Battle gates world movement and prop interaction", BattleController.AnyBattleActive && !GameSessionController.GameplayInputAllowed && GameSessionController.SessionInputAllowed);
            yield return new WaitForSeconds(.3f);
            controller.QuickCast();
            Add("Round-casting result lists unlocked skills", controller.View.CastVisible && controller.Engine.State.phase == BattlePhase.Player && controller.Engine.State.unlockedSkills.Count == 3);
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot("Validation/battle-round-result-ui.png"); yield return null;
            yield return new WaitForSeconds(.8f);
            var view = controller.View; view.SelectTarget(2);
            Add("Enemy click selects a gold highlighted portrait", view.SelectedTarget == 2 && view.GetComponentsInChildren<Outline>().Count(o => o.enabled && o.effectDistance.x == 3) == 1);
            Add("Every portrait has red HP and blue MP bars", view.GetComponentsInChildren<Text>().Count(t => t.text.StartsWith("HP ")) == 4 && view.GetComponentsInChildren<Text>().Count(t => t.text.StartsWith("MP ")) == 4);
            Add("Skill buttons show only names", catalog.skills.All(s => view.GetComponentsInChildren<Button>().Any(b => b.GetComponentInChildren<Text>().text == s.displayName)));
            var heavy = view.GetComponentsInChildren<BattleHoverTarget>().First(h => h.skillId == "ATK_HEAVY");
            var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = new Vector2(Screen.width * .3f, Screen.height * .23f) };
            UnityEngine.EventSystems.ExecuteEvents.Execute(heavy.gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerEnterHandler);
            Add("Actual hover shows cost, effect and attached status values", view.TooltipVisible && view.TooltipContent.Contains("8 MP") && view.TooltipContent.Contains("基础伤害") && view.StatusTooltipContent.Contains("20%") && view.StatusTooltipContent.Contains("次"));
            var tooltip = view.transform.Find("战斗场景/悬浮详情"); var main = (RectTransform)tooltip.Find("技能与意图详情"); var status = (RectTransform)tooltip.Find("附加状态说明");
            Add("Status tooltip is immediately below main tooltip", Mathf.Approximately(status.anchoredPosition.y, -main.sizeDelta.y));
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot("Validation/battle-skill-tooltip-ui.png"); yield return null;
            UnityEngine.EventSystems.ExecuteEvents.Execute(heavy.gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerExitHandler);
            Add("Leaving skill hides tooltip", !view.TooltipVisible);
            var enemyHover = view.GetComponentsInChildren<BattleHoverTarget>().First(h => h.enemyIndex == 2);
            UnityEngine.EventSystems.ExecuteEvents.Execute(enemyHover.gameObject, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerEnterHandler);
            Add("Enemy hover exposes frozen intention", view.TooltipVisible && view.TooltipContent.Contains("敌方意图") && view.TooltipContent.Contains(catalog.Enemy(controller.Engine.State.enemies[2].definitionId).skills.First(k => k.id == controller.Engine.State.enemies[2].intentSkillId).displayName));
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot("Validation/battle-enemy-intent-ui.png"); yield return null;
            view.HideTooltip();
            var bag = view.GetComponentsInChildren<Button>().First(b => b.GetComponentInChildren<Text>().text == "背包"); bag.onClick.Invoke();
            Add("Tab click activates backpack and hides skill page", view.CurrentPage == BattlePage.Backpack && !heavy.gameObject.activeInHierarchy);
            controller.Engine.State.player.mp = 20; view.Refresh(); int mpItems = controller.Engine.Inventory.Count("ITEM_MP");
            var manaItem = view.GetComponentsInChildren<BattleHoverTarget>().First(h => h.itemId == "ITEM_MP"); manaItem.GetComponent<Button>().onClick.Invoke();
            Add("Backpack button consumes item and leaves player turn", controller.Engine.State.player.mp == 30 && controller.Engine.Inventory.Count("ITEM_MP") == mpItems - 1 && controller.Engine.State.phase == BattlePhase.Player);
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot("Validation/battle-backpack-ui.png"); yield return null;
            view.SwitchPage(BattlePage.Negotiation); Add("Negotiation page is a placeholder", view.GetComponentsInChildren<Text>().Any(t => t.text.Contains("预留剧情入口")));
            view.SwitchPage(BattlePage.Escape); Add("Escape page and persistent end-turn are available", controller.CanFlee && view.GetComponentsInChildren<Button>().Any(b => b.GetComponentInChildren<Text>().text == "结束回合 →"));
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot("Validation/battle-escape-ui.png"); yield return null;
            view.SwitchPage(BattlePage.Skills);
            var basic = view.GetComponentsInChildren<Button>().First(b => b.GetComponentInChildren<Text>().text == "普攻");
            var frozenCoins = controller.Engine.State.roundDivination.casting.coinFaces.ToArray(); int targetHP = controller.Engine.State.enemies[2].hp;
            basic.onClick.Invoke();
            Add("Actual skill UI immediately attacks selected target", controller.Engine.State.phase == BattlePhase.Player && controller.Engine.State.lastAction.targetIndex == 2 && controller.Engine.State.enemies[2].hp == targetHP - 8);
            Add("Round-mode action does not open another coin modal", !view.CastVisible && controller.Engine.State.roundDivination.casting.coinFaces.SequenceEqual(frozenCoins));
            Add("Immediate action still animates portrait effects", view.GetComponentsInChildren<BattlePortraitMotion>().Any(m => m.effect.gameObject.activeSelf));
            yield return new WaitForSeconds(.7f);
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot("Validation/battle-player-ui.png"); yield return null;
            var end = view.GetComponentsInChildren<Button>().First(b => b.GetComponentInChildren<Text>().text == "结束回合 →"); end.onClick.Invoke();
            Add("Actual end-turn button switches phases", controller.Engine.State.phase == BattlePhase.Enemy);
            controller.View.PlayEnemyAction(0, controller.Engine.State.enemies[0].intentSkillId);
            var portrait = view.GetComponentsInChildren<BattlePortraitMotion>().First(m => m.gameObject.GetComponentInParent<BattleHoverTarget>()?.enemyIndex == 0);
            var home = ((RectTransform)portrait.transform).anchoredPosition;
            yield return new WaitForSeconds(.12f);
            Add("Enemy action moves portrait and displays hit image", ((RectTransform)portrait.transform).anchoredPosition != home && view.GetComponentsInChildren<BattlePortraitMotion>().Any(m => m.effect.gameObject.activeSelf));
            while (controller.Engine.State.phase == BattlePhase.Enemy) controller.Engine.StepEnemy();
            controller.Engine.RevealLine(); controller.Engine.RevealLine();
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot("Validation/battle-casting-ui.png"); yield return null;
            var saved = controller.CaptureSnapshot(); bool restored = controller.RestoreSnapshot(saved);
            Add("Controller restores round-casting UI", restored && controller.View.gameObject.activeSelf && controller.Engine.State.roundDivination.revealedLines == 2);
            controller.QuickCast(); yield return new WaitForSeconds(.8f);
            for (int i = 0; i < controller.Engine.State.enemies.Count; i++) { controller.Engine.State.enemies[i].hp = 1; controller.Engine.State.player.mp = 30; controller.UseSkill("ATK_BASIC", i); }
            Add("Runtime victory UI applies outcome and round record", controller.Engine.State.phase == BattlePhase.Victory && controller.Engine.State.outcomeApplied && controller.Engine.Inventory.Victory("ENC03", catalog.rules.balanceVersion).lastRounds == 2);
            yield return new WaitForSeconds(.9f);
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot("Validation/battle-victory-ui.png"); yield return null;
            controller.CloseResult(); Add("Closing result unlocks world", !BattleController.AnyBattleActive && GameSessionController.GameplayInputAllowed);
            var inventory = controller.GetComponent<PropGameState>(); var before = inventory.CaptureJson(); var beforePosition = controller.transform.position;
            var prop = PropInstance.Instances.First(p => p != null && p.gameObject.scene == controller.gameObject.scene); var propPosition = prop.transform.position; bool propActive = prop.gameObject.activeSelf;
            controller.TryBegin(catalog.Encounter("ENC01")); controller.QuickCast(); controller.UseSkill("ATK_BASIC", 0);
            inventory.SetFlag("ui-escape-local-test"); controller.transform.position += Vector3.right * 4; prop.transform.position += Vector3.right * 3; prop.gameObject.SetActive(false);
            var returnSave = controller.CaptureSnapshot();
            Add("Battle save carries valid independent return point", returnSave.returnPoint != null && BattleReturnPoint.Validate(returnSave.returnPoint));
            var invalid = JsonUtility.FromJson<BattleSnapshot>(JsonUtility.ToJson(returnSave)); invalid.returnPoint.position.x = float.NaN;
            Add("Invalid return point is rejected without changing battle", !controller.RestoreSnapshot(invalid) && controller.Engine.State != null);
            var legacy = JsonUtility.FromJson<BattleSnapshot>(JsonUtility.ToJson(returnSave)); legacy.returnPoint = null;
            Add("Legacy battle save remains readable with escape disabled", controller.RestoreSnapshot(legacy) && !controller.CanFlee);
            controller.RestoreSnapshot(returnSave);
            Add("Escape restores local inventory, position, props and flags", controller.Flee() && controller.Engine.State == null && !BattleController.AnyBattleActive && inventory.CaptureJson() == JsonUtility.ToJson(returnSave.returnPoint.propState, true) && controller.transform.position == beforePosition && prop.transform.position == propPosition && prop.gameObject.activeSelf == propActive);
            Add("Actual controller opens the revised boss encounter", controller.TryBegin(catalog.Encounter("ENC05")));
            controller.QuickCast(); yield return new WaitForSeconds(.8f);
            view = controller.View;
            view.ShowEnemyTooltip(0, new Vector2(Screen.width * .5f, Screen.height * .5f));
            Add("Boss hover explains retaliation and its defenses", view.TooltipContent.Contains("潮棘") && view.TooltipContent.Contains("60%") && view.TooltipContent.Contains("护盾"));
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot("Validation/battle-boss-counterplay-ui.png"); yield return null;
            view.ShowSkillTooltip("ATK_BASIC", new Vector2(Screen.width * .5f, Screen.height * .5f));
            int reflected = controller.Engine.ForecastRetaliation("ATK_BASIC", 0), playerHP = controller.Engine.State.player.hp;
            Add("Attack hover gives the current retaliation HP forecast", view.TooltipContent.Contains("触发反震") && view.TooltipContent.Contains("损失 " + reflected + " HP"));
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot("Validation/battle-boss-attack-risk-ui.png"); yield return null;
            controller.UseSkill("ATK_BASIC", 0);
            Add("Real boss UI attack applies the displayed retaliation loss", controller.Engine.State.player.hp == playerHP - reflected);
            Add("Boss battle save remains valid and escape restores exploration", BattleEngine.ValidateSnapshot(controller.CaptureSnapshot(), catalog) && controller.Flee());
            controller.enabled = true;
            var menu = MenuPersistence(); while (menu.MoveNext()) yield return menu.Current;
            var scenes = SceneSmoke(); while (scenes.MoveNext()) yield return scenes.Current;
        }
        private IEnumerator MenuPersistence()
        {
            // Use isolated saves and the real scene-loading menu; leave user slots untouched.
            DontDestroyOnLoad(gameObject);
            var session = new GameObject("Battle menu integration fixture").AddComponent<GameSessionController>();
            yield return null;
            string directory = Path.GetFullPath("Validation/battle-menu-save-test-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));
            session.UseTestSaveDirectory(directory); session.gameScenePath = "Assets/Scenes/BattleSingleDemo.unity";
            session.BeginNewGame();
            for (int startingPointIndex = 0; startingPointIndex < 8; startingPointIndex++) session.AdjustAttribute(Emerge.Checks.CheckBehavior.Wealth, 1);
            if (!session.ConfirmCharacterCreation()) throw new Exception("Battle fixture character allocation was not confirmed");
            float deadline = Time.realtimeSinceStartup + 12;
            while (session.Phase != GameSessionPhase.Playing && Time.realtimeSinceStartup < deadline) yield return null;
            if (session.Phase != GameSessionPhase.Playing) throw new Exception("New-game menu load timeout");
            yield return null;
            var controller = FindObjectsOfType<BattleController>().First(c => c.GetComponent<PixelPrototype.PlayerMovement>() != null);
            controller.enabled = false;
            var terminal = FindObjectsOfType<PropInstance>().First(p => p.Definition.battleEncounter != null);
            var beforePosition = controller.transform.position; var terminalPosition = terminal.transform.position;
            var beforeInventory = controller.GetComponent<PropGameState>().CaptureJson();
            Add("Existing prop interaction enters battle", terminal.Interact(controller.GetComponent<PlayerInteractor>()));
            controller.Engine.RevealLine(); controller.Engine.RevealLine(); var frozen = controller.CaptureSnapshot();
            var combatCanvas = controller.View.GetComponent<Canvas>(); var menuCanvas = session.GetComponentInChildren<Canvas>();
            Add("Combat and pause canvases share camera with menu above combat", combatCanvas.renderMode == RenderMode.ScreenSpaceCamera &&
                combatCanvas.worldCamera == menuCanvas.worldCamera && combatCanvas.sortingOrder < menuCanvas.sortingOrder);
            session.OpenSettings();
            Add("Pause blocks combat commands and world input", !GameSessionController.SessionInputAllowed && !GameSessionController.GameplayInputAllowed);
            Add("Battle settings disable reallocation and direct calls cannot change its frozen build", !session.CanChangeAttributes &&
                !session.GetComponentsInChildren<Button>(true).First(button => button.name == "Reallocate Attributes").interactable &&
                !session.BeginAttributeReallocation() && session.Phase == GameSessionPhase.Settings && !session.IsChangingAttributes);
            int serial = controller.Engine.State.actionSerial; controller.QuickCast(); controller.EndTurn(); controller.UseItem("ITEM_MP");
            Add("Paused combat cannot mutate state", controller.Engine.State.actionSerial == serial && controller.Engine.State.phase == BattlePhase.RoundCasting);
            yield return null; Canvas.ForceUpdateCanvases();
            var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = new Vector2(Screen.width / 2f, Screen.height / 2f) };
            var hits = new List<UnityEngine.EventSystems.RaycastResult>(); UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer, hits);
            Add("Paused UI raycast reaches menu before combat", hits.Count > 0 && hits[0].gameObject.GetComponentInParent<GameMenuUI>() != null,
                string.Join(", ", hits.Take(5).Select(h => h.gameObject.name + " order=" + h.sortingOrder)));
            Add("Real pause menu saves pending battle", session.SaveManual());
            session.LoadManual(); deadline = Time.realtimeSinceStartup + 12;
            while (session.Phase != GameSessionPhase.Playing && Time.realtimeSinceStartup < deadline) yield return null;
            if (session.Phase != GameSessionPhase.Playing) throw new Exception("Battle menu restore timeout: " + session.LastMessage);
            controller = BattleController.Active;
            Add("Actual scene reload restores battle and original cast", controller != null && controller.Engine.State.phase == BattlePhase.RoundCasting &&
                controller.Engine.State.roundDivination.revealedLines == 2 && controller.Engine.State.player.mp == 30 &&
                controller.Engine.State.roundDivination.casting.coinFaces.SequenceEqual(frozen.session.roundDivination.casting.coinFaces));
            controller.enabled = false; yield return new WaitForSeconds(.3f);
            session.OpenSettings(); yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot("Validation/battle-pause-ui.png"); yield return null;
            session.ResumeGame(); controller.QuickCast(); controller.Engine.State.player.mp = 0; controller.UseItem("ITEM_MP");
            int remaining = controller.GetComponent<PropGameState>().Count("ITEM_MP");
            session.OpenSettings(); Add("Item consumption can be saved with resolved battle", session.SaveManual()); session.LoadManual(); deadline = Time.realtimeSinceStartup + 12;
            while (session.Phase != GameSessionPhase.Playing && Time.realtimeSinceStartup < deadline) yield return null;
            controller = BattleController.Active;
            Add("Reload preserves inventory consumption without demo replenishment", controller != null && controller.Engine.State.phase == BattlePhase.Player &&
                controller.Engine.State.player.mp == 12 && controller.GetComponent<PropGameState>().Count("ITEM_MP") == remaining);
            controller.enabled = false;
            yield return new WaitForSeconds(.3f);
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot("Validation/battle-gameplay-ui.png"); yield return null;
            var point = controller.CaptureSnapshot().returnPoint;
            Add("Real scene reload retains prebattle world and plot snapshot", point != null && point.world != null && BattleReturnPoint.Validate(point) && point.world.actors.All(a => a.battleState == null));
            var invalidPoint = JsonUtility.FromJson<BattleReturnPoint>(JsonUtility.ToJson(point)); invalidPoint.world.actors[0].battleState = controller.CaptureSnapshot();
            Add("Nested battle return graphs are rejected", !BattleReturnPoint.Validate(invalidPoint));
            var inventory = controller.GetComponent<PropGameState>(); inventory.SetFlag("ui-escape-world-test"); controller.transform.position += Vector3.right * 5;
            terminal = FindObjectsOfType<PropInstance>().First(p => p.Definition.battleEncounter != null); terminal.transform.position += Vector3.up * 3; terminal.gameObject.SetActive(false);
            Add("Escape after battle reload restores world, story and consumed items", controller.Flee() && inventory.CaptureJson() == JsonUtility.ToJson(point.world.actors[0].propState, true) && controller.transform.position == beforePosition && terminal.transform.position == terminalPosition && terminal.gameObject.activeSelf && !BattleController.AnyBattleActive && GameSessionController.GameplayInputAllowed);
            Add("Escape automatically saves restored exploration without battle", session.Store.TryRead(SaveSlot.Auto, out var escaped, out _) && escaped.actors.All(a => a.battleState == null) && JsonUtility.ToJson(escaped.actors[0].propState) == JsonUtility.ToJson(point.world.actors[0].propState));
            controller.TryBegin(catalog.Encounter("ENC01"));
            session.OpenSettings(); session.SaveAndReturnToMenu();
            Add("Save-and-return keeps resumable battle", session.Phase == GameSessionPhase.MainMenu && session.Store.TryRead(SaveSlot.Manual, out var saved, out _) && saved.actors[0].battleState != null);
            controller.RestoreSnapshot(null); Destroy(session.gameObject); yield return null;
        }
        private IEnumerator SceneSmoke()
        {
            var scenes = new[] { "BattleSingleDemo", "BattleDualDemo", "BattleTripleDemo", "BattleDefenseDemo", "BattleBossDemo" };
            for (int i = 0; i < scenes.Length; i++)
            {
                string path = "Assets/Scenes/" + scenes[i] + ".unity";
                Add("Authored scene is in build: " + scenes[i], Application.CanStreamedLevelBeLoaded(path));
                yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(path); yield return null;
                var context = FindObjectOfType<Emerge.Battle.Demo.BattleDemoContext>();
                var controller = context.player; controller.enabled = false;
                Add("Authored scene opens correct playable encounter: " + scenes[i], context.encounter == catalog.encounters[i] && controller.TryBegin(context.encounter) &&
                    controller.View != null && controller.Engine.State.enemies.Count == catalog.encounters[i].enemies.Length);
                controller.QuickCast(); controller.Engine.State.player.hp = 1; controller.Engine.State.player.burn = 5; controller.Engine.State.player.burnTicks = 2; controller.EndTurn();
                while (controller.Engine.State.phase == BattlePhase.Enemy) controller.Engine.StepEnemy();
                Add("Authored scene handles defeat and returns to map: " + scenes[i], controller.Engine.State.phase == BattlePhase.Defeat);
                controller.CloseResult(); controller.enabled = true;
                yield return null;
            }
        }
        public void BeginPreview() { RunChecks(); Completed += PreviewForward; }
        private void PreviewForward(bool passed) { Completed -= PreviewForward; PreviewCompleted?.Invoke(passed); }
        private void Add(string name, bool passed, string observed = "")
        { report.checks.Add(new Check { name = name, passed = passed, observed = observed }); Debug.Log("[BattleValidation] " + (passed ? "PASS " : "FAIL ") + name + (string.IsNullOrEmpty(observed) ? "" : " · " + observed)); }
    }
}
#endif

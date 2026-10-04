using System;
using System.Collections.Generic;
using System.Linq;
using Emerge.Checks.Divination;
using Emerge.Props;
using UnityEngine;

namespace Emerge.Battle
{
    // Version 2 moves randomness before decisions. Version 1 saves keep their original casting flow.
    public sealed class BattleEngine
    {
        public BattleCatalog Catalog { get; }
        public PropGameState Inventory { get; }
        private BattleSession state;
        private BattleLegacyEngine legacy;
        private bool committing;
        public BattleSession State => legacy != null ? legacy.State : state;
        public bool IsCommitting => committing || (legacy?.IsCommitting ?? false);
        public event Action Changed;
        public BattleEngine(BattleCatalog catalog, PropGameState inventory)
        { string error = null; if (catalog == null || !catalog.Validate(out error)) throw new ArgumentException(error ?? "缺少配置"); Catalog = catalog; Inventory = inventory; }
        public void Start(BattleEncounterDefinition encounter, int seed, string contextId = null)
        {
            if (encounter == null || Catalog.Encounter(encounter.id) != encounter || IsCommitting) throw new ArgumentException("未知遭遇或行动未完成");
            legacy = null; state = new BattleSession { version = 2, balanceVersion = Catalog.rules.balanceVersion, seed = seed, sessionId = Guid.NewGuid().ToString("N"), encounterId = encounter.id, contextId = contextId ?? encounter.id,
                player = new BattlePlayerState { hp = Catalog.rules.maxHP, mp = Catalog.rules.maxMP } };
            foreach (var slot in encounter.enemies) { int hp = slot.healthOverride > 0 ? slot.healthOverride : slot.enemy.maxHP; state.enemies.Add(new BattleEnemyState { definitionId = slot.enemy.id, hp = hp, maxHP = hp, mp = slot.enemy.maxMP }); }
            BeginRound(); Log("战斗开始 · " + encounter.displayName); Changed?.Invoke();
        }
        public static int CastSeed(int seed, int serial) => BattleLegacyEngine.CastSeed(seed, serial);
        public static int RoundSeed(int seed, int round) => unchecked(seed ^ round * 1000003 ^ 0x752BAC);
        public static int FamilyScore(DivinationRecord record, BattleFamily family)
        { string name = BattleRules.FamilyName(family); var yaos = record.chart.yaos.Where(x => x.benLiuqin == name); return yaos.Any() ? Mathf.Clamp(yaos.Max(x => x.wangshuaiScore), -2, 2) : 0; }
        public int RemainingUses(string id)
        { var skill = Catalog.Skill(id); return skill == null ? 0 : State?.version == 1 || skill.maximumUses == 0 ? int.MaxValue : Math.Max(0, skill.maximumUses - (State?.skillUses.Find(x => x.skillId == id)?.count ?? 0)); }
        public float SkillMultiplier(string id)
        { var skill = Catalog.Skill(id); return skill == null || skill.alwaysAvailable || State?.roundDivination == null ? 1 : Catalog.rules.Multiplier(FamilyScore(State.roundDivination, skill.family)); }
        public bool CanUseSkill(string id, int target, out string reason)
        {
            if (legacy != null) return legacy.CanUseSkill(id, target, out reason);
            reason = ""; var skill = Catalog.Skill(id);
            if (committing || state?.phase != BattlePhase.Player) reason = "请等待当前行动完成";
            else if (skill == null) reason = "技能不存在";
            else if (RemainingUses(id) == 0) reason = "本场次数已耗尽";
            else if (!skill.alwaysAvailable && !state.unlockedSkills.Contains(id)) reason = "本轮未解锁";
            else if (state.player.mp < skill.mpCost) reason = "MP 不足";
            else if (skill.target == BattleTarget.Enemy && (target < 0 || target >= state.enemies.Count || state.enemies[target].hp <= 0)) reason = "请选择存活敌人";
            else if (skill.effect == BattleEffect.Heal && state.player.hp >= Catalog.rules.maxHP) reason = "HP 已满";
            else if (skill.effect == BattleEffect.Shield && state.player.shield >= Catalog.rules.shieldCap) reason = "护盾已达上限";
            else if (skill.effect == BattleEffect.Reduction && state.player.reduction >= skill.power) reason = "已处于守御状态";
            else if (skill.effect == BattleEffect.Cleanse && !HasNegativeState(state.player) && state.player.nextMana >= skill.power) reason = "已清心，无需重复施加";
            return reason.Length == 0;
        }
        public bool CommitSkill(string id, int target, out string reason)
        {
            if (legacy != null) return legacy.CommitSkill(id, target, out reason);
            if (!CanUseSkill(id, target, out reason)) return false;
            var skill = Catalog.Skill(id); committing = true;
            try
            {
                var action = new BattleAction { serial = ++state.actionSerial, round = state.round, skillId = id, skillName = skill.displayName,
                    targetIndex = skill.target == BattleTarget.Enemy ? target : 0, family = skill.family, target = skill.target, effect = skill.effect, power = skill.power,
                    appliesVulnerability = skill.appliesVulnerability, regenerationTicks = skill.regenerationTicks, divination = state.roundDivination };
                Grade(action, skill, Catalog.rules); state.player.mp -= skill.mpCost;
                var used = state.skillUses.Find(x => x.skillId == id); if (used == null) { used = new EnemySkillUses { skillId = id }; state.skillUses.Add(used); } used.count++; used.lastRound = state.round;
                ApplySkill(action); state.lastAction = action;
                Log(action.skillName + " · ×" + action.multiplier.ToString("0.0") + " · 基础效果 " + action.value); CheckOutcome();
            }
            finally { committing = false; }
            Changed?.Invoke(); return true;
        }
        private static void Grade(BattleAction action, BattleSkillDefinition skill, BattleRules rules)
        {
            action.score = skill.alwaysAvailable ? 0 : FamilyScore(action.divination, skill.family); action.multiplier = skill.alwaysAvailable ? 1 : rules.Multiplier(action.score);
            action.value = BattleRules.Round(action.power * action.multiplier);
            string family = BattleRules.FamilyName(skill.family); var representative = action.divination.chart.yaos.Where(x => x.benLiuqin == family).OrderByDescending(x => x.wangshuaiScore).ThenBy(x => x.index).FirstOrDefault();
            action.movingLine = representative != null && representative.isDongYao;
        }
        private void ApplySkill(BattleAction a)
        {
            var p = state.player; var r = Catalog.rules;
            switch (a.effect)
            {
                case BattleEffect.Damage:
                    foreach (int i in Targets(a.target, a.targetIndex)) { HitEnemy(i, a.power * a.multiplier * (1 - p.weakness)); var e = state.enemies[i]; if (e.hp > 0 && a.appliesVulnerability && a.score >= 0) { e.vulnerability = r.vulnerability; e.vulnerabilityHits = r.vulnerabilityHits + (a.movingLine ? 1 : 0); } } break;
                case BattleEffect.Shield: p.shield = Math.Min(r.shieldCap, p.shield + a.value); break;
                case BattleEffect.Reduction: p.reduction = Math.Max(p.reduction, Math.Min(r.reductionCap, a.power * a.multiplier)); break;
                case BattleEffect.Heal: p.hp = Math.Min(r.maxHP, p.hp + a.value); break;
                case BattleEffect.Regeneration: p.regeneration = Math.Max(p.regeneration, a.value); p.regenerationTicks = a.regenerationTicks; break;
                case BattleEffect.NextMana: p.nextMana = Math.Min(r.nextManaCap, Math.Max(p.nextMana, a.value)); break;
                case BattleEffect.Cleanse: Cleanse(p); p.nextMana = Math.Min(r.nextManaCap, Math.Max(p.nextMana, a.value)); break;
                case BattleEffect.Silence: if (a.score < 0) Weaken(a.targetIndex, .1f); else state.enemies[a.targetIndex].silenced = true; break;
                case BattleEffect.Bind: var e2 = state.enemies[a.targetIndex]; if (a.score < 2) Weaken(a.targetIndex, a.score < 0 ? .1f : a.score == 0 ? .2f : .3f); else if (e2.determined || Catalog.Enemy(e2.definitionId).resistsStun) Weaken(a.targetIndex, r.bossWeakness); else e2.stunned = true; break;
            }
        }
        public bool RevealLine()
        {
            if (legacy != null) return legacy.RevealLine();
            if (committing || state?.phase != BattlePhase.RoundCasting || state.roundDivination.revealedLines >= 6) return false;
            state.roundDivination.revealedLines++; Changed?.Invoke(); return true;
        }
        public bool ResolveSkill() => legacy != null && legacy.ResolveSkill();
        public bool ResolveRound()
        {
            if (legacy != null || committing || state?.phase != BattlePhase.RoundCasting || state.roundDivination.revealedLines != 6) return false;
            state.unlockedSkills = SelectOffers(Catalog, state.roundDivination, state.roundStartUses);
            state.phase = BattlePhase.Player; Log("本轮解锁 · " + string.Join("、", state.unlockedSkills.Select(id => Catalog.Skill(id).displayName))); Changed?.Invoke(); return true;
        }
        public static List<string> SelectOffers(BattleCatalog catalog, DivinationRecord record, List<EnemySkillUses> baseline)
        {
            var pool = catalog.skills.Where(x => !x.alwaysAvailable && (baseline.Find(u => u.skillId == x.id)?.count ?? 0) < x.maximumUses).ToList();
            var random = new System.Random(unchecked(record.casting.seed ^ 0x347D)); var selected = new List<string>();
            if (pool.Any(x => x.effect == BattleEffect.Damage)) PickOffer(pool.Where(x => x.effect == BattleEffect.Damage).ToList(), pool, selected, random, record);
            if (selected.Count < catalog.rules.advancedOptions && pool.Any(x => x.effect != BattleEffect.Damage)) PickOffer(pool.Where(x => x.effect != BattleEffect.Damage).ToList(), pool, selected, random, record);
            while (selected.Count < catalog.rules.advancedOptions && pool.Count > 0) PickOffer(pool.ToList(), pool, selected, random, record);
            return selected;
        }
        private static void PickOffer(List<BattleSkillDefinition> candidates, List<BattleSkillDefinition> pool, List<string> selected, System.Random random, DivinationRecord record)
        {
            Func<BattleSkillDefinition, int> weight = s => 1 + record.chart.yaos.Count(y => y.benLiuqin == BattleRules.FamilyName(s.family)) * 2;
            int roll = random.Next(candidates.Sum(weight)); var chosen = candidates[0];
            foreach (var s in candidates) { if (roll < weight(s)) { chosen = s; break; } roll -= weight(s); }
            pool.Remove(chosen); selected.Add(chosen.id);
        }
        private void BeginRound()
        {
            var d = Catalog.Encounter(state.encounterId); var coins = CoinCasting.Cast(RoundSeed(state.seed, state.round));
            state.roundDivination = new DivinationRecord { month = d.month, day = d.day, casting = coins, chart = new LiuYaoPaiPan().PaiPan(d.month, d.day, coins.yaoValues) };
            state.roundStartUses = state.skillUses.Select(x => new EnemySkillUses { skillId = x.skillId, count = x.count, lastRound = x.lastRound }).ToList();
            state.unlockedSkills.Clear(); state.lastAction = null; state.phase = BattlePhase.RoundCasting; PrepareIntents();
        }
        public static bool HasNegativeState(BattlePlayerState p) => p.weakness > 0 || p.burnTicks > 0 || p.exposure > 0;
        private static void Cleanse(BattlePlayerState p) { p.weakness = p.exposure = 0; p.burn = p.burnTicks = p.exposureUntilRound = 0; }
        public bool CanUseItem(string id, out string reason)
        {
            if (legacy != null) return legacy.CanUseItem(id, out reason);
            reason = ""; var item = Catalog.Item(id);
            if (committing || state?.phase != BattlePhase.Player) reason = "请等待当前行动完成";
            else if (item == null || Inventory == null || Inventory.Count(item.inventoryKey) < 1) reason = "背包中没有此物品";
            else if (item.effect == BattleItemEffect.Heal && state.player.hp >= Catalog.rules.maxHP) reason = "HP 已满";
            else if (item.effect == BattleItemEffect.Mana && state.player.mp >= Catalog.rules.maxMP) reason = "MP 已满";
            else if (item.effect == BattleItemEffect.Cleanse && !HasNegativeState(state.player)) reason = "没有可移除的负面状态";
            return reason.Length == 0;
        }
        public bool UseItem(string id, out string reason)
        {
            if (legacy != null) return legacy.UseItem(id, out reason);
            if (!CanUseItem(id, out reason)) return false; var item = Catalog.Item(id); committing = true;
            try
            {
                if (!Inventory.RemoveItem(item.inventoryKey, 1)) return false;
                switch (item.effect) { case BattleItemEffect.Heal: state.player.hp = Math.Min(Catalog.rules.maxHP, state.player.hp + item.power); break; case BattleItemEffect.Mana: state.player.mp = Math.Min(Catalog.rules.maxMP, state.player.mp + item.power); break; case BattleItemEffect.Cleanse: Cleanse(state.player); break; case BattleItemEffect.DamageAll: foreach (int i in Targets(BattleTarget.AllEnemies, 0)) HitEnemy(i, item.power); break; }
                Log("使用 " + item.displayName); CheckOutcome();
            }
            finally { committing = false; }
            Changed?.Invoke(); return true;
        }
        public bool EndTurn()
        {
            if (legacy != null) return legacy.EndTurn();
            if (committing || state?.phase != BattlePhase.Player) return false;
            var p = state.player;
            if (p.burnTicks > 0) { p.hp = Math.Max(0, p.hp - p.burn); p.burnTicks--; Log("灼伤 · 损失 " + p.burn + " HP"); if (p.burnTicks == 0) p.burn = 0; }
            p.weakness = 0; CheckOutcome();
            if (state.phase != BattlePhase.Defeat)
            { state.phase = BattlePhase.Enemy; state.enemyCursor = 0; foreach (var e in state.enemies) { e.shield = 0; var d = Catalog.Enemy(e.definitionId); e.mp = Math.Min(d.maxMP, e.mp + d.roundMana); } }
            Changed?.Invoke(); return true;
        }
        public bool StepEnemy()
        {
            if (legacy != null) return legacy.StepEnemy();
            if (committing || state?.phase != BattlePhase.Enemy) return false;
            if (state.enemyCursor >= state.enemies.Count)
            {
                var p = state.player; if (p.exposureUntilRound <= state.round) { p.exposure = 0; p.exposureUntilRound = 0; }
                state.round++; state.enemyCursor = 0; p.shield = 0; p.reduction = 0;
                p.mp = Math.Min(Catalog.rules.maxMP, p.mp + Catalog.rules.roundMana + p.nextMana); p.nextMana = 0;
                if (p.regenerationTicks > 0) { p.hp = Math.Min(Catalog.rules.maxHP, p.hp + p.regeneration); if (--p.regenerationTicks == 0) p.regeneration = 0; }
                BeginRound(); Changed?.Invoke(); return true;
            }
            var enemy = state.enemies[state.enemyCursor++]; var definition = Catalog.Enemy(enemy.definitionId);
            if (enemy.hp > 0)
            {
                if (enemy.stunned) { enemy.determined = true; enemy.charged = false; Log(definition.displayName + " · 眩晕，跳过行动"); }
                else
                {
                    var skill = definition.skills.First(x => x.id == enemy.intentSkillId);
                    if (enemy.mp < skill.mpCost || (enemy.silenced && skill.mpCost > 0)) skill = Fallback(definition);
                    enemy.mp -= skill.mpCost; var used = enemy.uses.Find(x => x.skillId == skill.id); if (used == null) { used = new EnemySkillUses { skillId = skill.id }; enemy.uses.Add(used); } used.count++; used.lastRound = state.round;
                    enemy.charged = skill.effect == EnemyEffect.Charge;
                    switch (skill.effect)
                    {
                        case EnemyEffect.Shield: enemy.shield = skill.power; break;
                        case EnemyEffect.Heal: enemy.hp = Math.Min(enemy.maxHP, enemy.hp + skill.power); break;
                        case EnemyEffect.Charge: break;
                        default:
                            HitPlayer(skill.power * (1 - enemy.weakness));
                            if (skill.effect == EnemyEffect.Weaken) state.player.weakness = Math.Max(state.player.weakness, skill.weakness);
                            if (skill.effect == EnemyEffect.Burn) { state.player.burn = Catalog.rules.burnDamage; state.player.burnTicks = Catalog.rules.burnTicks; }
                            if (skill.effect == EnemyEffect.Exposure) { state.player.exposure = Catalog.rules.exposure; state.player.exposureUntilRound = state.round + 1; }
                            if (skill.effect == EnemyEffect.ChargedDamage) { enemy.vulnerability = .25f; enemy.vulnerabilityHits = 3; }
                            break;
                    }
                    Log(definition.displayName + " · " + skill.displayName); enemy.determined = false;
                }
                enemy.stunned = enemy.silenced = false; enemy.weakness = 0;
            }
            CheckOutcome(); Changed?.Invoke(); return true;
        }
        private void PrepareIntents()
        {
            var random = new System.Random(unchecked(state.seed ^ state.round * 104729 ^ 0x15AF29)); int budget = Catalog.rules.enemyIntentBudget;
            for (int i = 0; i < state.enemies.Count; i++)
            {
                var e = state.enemies[i];
                var d = Catalog.Enemy(e.definitionId); var candidates = d.skills.Where(s =>
                    (s.effect == EnemyEffect.ChargedDamage) == e.charged && s.mpCost <= Math.Min(d.maxMP, e.mp + d.roundMana) &&
                    e.hp <= e.maxHP * s.maximumHealthFraction && e.shield < s.maximumShield &&
                    (s.maximumUses == 0 || (e.uses.Find(u => u.skillId == s.id)?.count ?? 0) < s.maximumUses) &&
                    (s.cooldownRounds == 0 || !e.uses.Any(u => u.skillId == s.id && state.round - u.lastRound <= s.cooldownRounds)) &&
                    IntentDamage(s) <= budget - state.enemies.Skip(i + 1).Where(x => x.hp > 0).Sum(x => IntentDamage(Fallback(Catalog.Enemy(x.definitionId))))).ToList();
                var selected = Fallback(d);
                if (e.hp > 0 && candidates.Count > 0)
                { int roll = random.Next(candidates.Sum(s => s.weight)); foreach (var s in candidates) { if (roll < s.weight) { selected = s; break; } roll -= s.weight; } }
                e.intentSkillId = selected.id; if (e.hp > 0) budget -= IntentDamage(selected);
            }
        }
        public static bool IsAttack(EnemyEffect effect) => effect != EnemyEffect.Shield && effect != EnemyEffect.Heal && effect != EnemyEffect.Charge;
        public static int IntentDamage(EnemySkillDefinition s) => IsAttack(s.effect) ? s.power : 0;
        public int ForecastDamage()
        {
            if (State == null) return 0;
            int total = 0, shield = State.player.shield; float exposure = State.player.exposure;
            for (int i = State.phase == BattlePhase.Enemy ? State.enemyCursor : 0; i < State.enemies.Count; i++)
            {
                var e = State.enemies[i]; if (e.hp <= 0 || e.stunned) continue;
                var d = Catalog.Enemy(e.definitionId); var s = d.skills.First(x => x.id == e.intentSkillId);
                int mana = State.phase == BattlePhase.Enemy ? e.mp : Math.Min(d.maxMP, e.mp + d.roundMana);
                if ((e.silenced && s.mpCost > 0) || mana < s.mpCost) s = Fallback(d);
                int hit = BattleRules.Round(IntentDamage(s) * (1 - e.weakness) * (1 - State.player.reduction) * (1 + exposure));
                int absorbed = Math.Min(shield, hit); shield -= absorbed; total += hit - absorbed;
                if (s.effect == EnemyEffect.Exposure) exposure = Catalog.rules.exposure;
            }
            return total;
        }
        private static EnemySkillDefinition Fallback(BattleEnemyDefinition d) => d.skills.First(s => s.effect == EnemyEffect.Damage && s.mpCost == 0 && s.maximumHealthFraction == 1 && s.maximumUses == 0 && s.cooldownRounds == 0);
        private IEnumerable<int> Targets(BattleTarget target, int index) { for (int i = 0; i < state.enemies.Count; i++) if (state.enemies[i].hp > 0 && (target == BattleTarget.AllEnemies || i == index)) yield return i; }
        private void HitEnemy(int index, float raw)
        { var e = state.enemies[index]; int damage = BattleRules.Round(raw * (1 + (e.vulnerabilityHits > 0 ? e.vulnerability : 0))); int absorbed = Math.Min(e.shield, damage); e.shield -= absorbed; e.hp = Math.Max(0, e.hp - damage + absorbed); if (e.vulnerabilityHits > 0 && --e.vulnerabilityHits == 0) e.vulnerability = 0; }
        private void HitPlayer(float raw)
        { var p = state.player; int damage = BattleRules.Round(raw * (1 - p.reduction) * (1 + p.exposure)); int absorbed = Math.Min(p.shield, damage); p.shield -= absorbed; p.hp = Math.Max(0, p.hp - damage + absorbed); }
        private void Weaken(int target, float weakness) { state.enemies[target].weakness = Math.Max(state.enemies[target].weakness, weakness); }
        private void CheckOutcome() { if (state.player.hp <= 0) state.phase = BattlePhase.Defeat; else if (state.enemies.All(e => e.hp <= 0)) state.phase = BattlePhase.Victory; }
        private void Log(string text) { state.log.Add(text); if (state.log.Count > 80) state.log.RemoveAt(0); }
        public void ApplyOutcome()
        {
            var s = State; if (s == null || s.outcomeApplied || (s.phase != BattlePhase.Victory && s.phase != BattlePhase.Defeat)) return; committing = true;
            try
            {
                if (legacy != null) legacy.ApplyOutcome(); else s.outcomeApplied = true;
                if (Inventory != null)
                {
                    Inventory.CompleteBattleAttempt(s.contextId, s.encounterId);
                    if (s.phase == BattlePhase.Victory)
                    {
                        var encounter = Catalog.Encounter(s.encounterId); Inventory.SetFlag("battle-won:" + s.contextId); if (!string.IsNullOrWhiteSpace(encounter.victoryFlag)) Inventory.SetFlag(encounter.victoryFlag);
                        Inventory.RecordBattleVictory(s.encounterId, s.version == 1 ? "legacy" : s.balanceVersion, s.sessionId, s.round, encounter.swiftVictoryRounds);
                    }
                }
            }
            finally { committing = false; }
        }
        public BattleSnapshot Capture()
        { if (IsCommitting) throw new InvalidOperationException("行动提交期间不能保存"); return new BattleSnapshot { session = State == null ? null : JsonUtility.FromJson<BattleSession>(JsonUtility.ToJson(State)) }; }
        public bool Restore(BattleSnapshot snapshot)
        {
            if (IsCommitting || !ValidateSnapshot(snapshot, Catalog)) return false;
            if (snapshot.session?.version == 1) { var restored = new BattleLegacyEngine(Catalog, Inventory); if (!restored.Restore(snapshot)) return false; legacy = restored; legacy.Changed += () => Changed?.Invoke(); state = null; }
            else { legacy = null; state = snapshot.session == null ? null : JsonUtility.FromJson<BattleSession>(JsonUtility.ToJson(snapshot.session)); }
            Changed?.Invoke(); return true;
        }
        public static bool ValidateSnapshot(BattleSnapshot snapshot, BattleCatalog catalog = null)
        {
            if (snapshot == null || snapshot.version != 1 || snapshot.catalogPath != BattleCatalog.ResourcePath ||
                (snapshot.returnPoint != null && (!BattleReturnPoint.Validate(snapshot.returnPoint) || snapshot.session == null))) return false;
            if (snapshot.session == null) return true;
            if (catalog == null) catalog = Resources.Load<BattleCatalog>(BattleCatalog.ResourcePath);
            if (catalog == null || !catalog.Validate(out _)) return false;
            if (snapshot.session.version == 1) return BattleLegacyEngine.ValidateSnapshot(snapshot, catalog);
            var s = snapshot.session; var p = s.player; var r = catalog.rules; var d = catalog.Encounter(s.encounterId);
            if (s.version != 2 || s.balanceVersion != r.balanceVersion || string.IsNullOrWhiteSpace(s.sessionId) || string.IsNullOrWhiteSpace(s.contextId) || s.round < 1 || s.actionSerial < 0 || d == null || p == null ||
                !Enum.IsDefined(typeof(BattlePhase), s.phase) || s.phase == BattlePhase.Casting || s.pending != null ||
                p.hp < 0 || p.hp > r.maxHP || p.mp < 0 || p.mp > r.maxMP || p.shield < 0 || p.shield > r.shieldCap || !Fraction(p.reduction, r.reductionCap) || !Fraction(p.weakness, 1) ||
                !Fraction(p.exposure, r.exposure) || p.exposureUntilRound < 0 || p.exposureUntilRound > s.round + 1 || (p.exposure > 0 && p.exposureUntilRound < s.round) || ((p.exposure == 0) != (p.exposureUntilRound == 0)) ||
                p.burn < 0 || p.burn > r.burnDamage || p.burnTicks < 0 || p.burnTicks > r.burnTicks || ((p.burn == 0) != (p.burnTicks == 0)) ||
                p.nextMana < 0 || p.nextMana > r.nextManaCap || p.regeneration < 0 || p.regeneration > 10000 || p.regenerationTicks < 0 || p.regenerationTicks > 100 ||
                s.enemies == null || s.enemies.Count != d.enemies.Length || s.enemies.Any(e => e == null) || s.enemyCursor < 0 || s.enemyCursor > s.enemies.Count ||
                s.log == null || s.log.Count > 80 || s.log.Any(x => x == null || x.Length > 1000) || !ValidUses(s.skillUses, catalog, s.round) || !ValidUses(s.roundStartUses, catalog, s.round) ||
                s.skillUses.Sum(x => (long)x.count) != s.actionSerial || s.roundStartUses.Any(x => (s.skillUses.Find(u => u.skillId == x.skillId)?.count ?? 0) < x.count) ||
                !ValidRecord(s.roundDivination, s, d, s.round) || s.unlockedSkills == null || s.unlockedSkills.Distinct().Count() != s.unlockedSkills.Count) return false;
            bool opening = s.phase == BattlePhase.RoundCasting;
            if ((!opening && s.roundDivination.revealedLines != 6) || (opening && s.unlockedSkills.Count != 0) ||
                (!opening && !s.unlockedSkills.SequenceEqual(SelectOffers(catalog, s.roundDivination, s.roundStartUses))) ||
                (s.phase == BattlePhase.Victory && (p.hp <= 0 || s.enemies.Any(e => e.hp > 0))) || (s.phase == BattlePhase.Defeat && p.hp != 0) ||
                ((opening || s.phase == BattlePhase.Player || s.phase == BattlePhase.Enemy) && (p.hp == 0 || s.enemies.All(e => e.hp <= 0))) ||
                (s.outcomeApplied && s.phase != BattlePhase.Victory && s.phase != BattlePhase.Defeat)) return false;
            for (int i = 0; i < s.enemies.Count; i++)
            {
                var e = s.enemies[i]; var def = d.enemies[i].enemy;
                if (e == null || e.definitionId != def.id || e.maxHP != (d.enemies[i].healthOverride > 0 ? d.enemies[i].healthOverride : def.maxHP) || e.hp < 0 || e.hp > e.maxHP ||
                    e.mp < 0 || e.mp > def.maxMP || e.shield < 0 || e.shield > 10000 || e.vulnerabilityHits < 0 || e.vulnerabilityHits > Math.Max(3, r.vulnerabilityHits + 1) || !Fraction(e.vulnerability, .5f) || !Fraction(e.weakness, 1) ||
                    !def.skills.Any(x => x.id == e.intentSkillId) || e.uses == null || e.uses.Any(u => u == null || u.count < 1 || u.lastRound < 1 || u.lastRound > s.round || !def.skills.Any(x => x.id == u.skillId && (x.maximumUses == 0 || u.count <= x.maximumUses))) || e.uses.Select(u => u.skillId).Distinct().Count() != e.uses.Count) return false;
            }
            var a = s.lastAction;
            if (a == null) return true;
            var skill = catalog.Skill(a.skillId);
            if (skill == null || a.round != s.round || a.serial != s.actionSerial || a.power != skill.power || a.family != skill.family || a.effect != skill.effect || a.target != skill.target ||
                a.targetIndex < 0 || a.targetIndex >= s.enemies.Count || a.appliesVulnerability != skill.appliesVulnerability || a.regenerationTicks != skill.regenerationTicks || a.skillName != skill.displayName ||
                !ValidRecord(a.divination, s, d, a.round) || a.divination.revealedLines != 6 || (!skill.alwaysAvailable && !s.unlockedSkills.Contains(skill.id))) return false;
            var expected = new BattleAction { family = a.family, power = a.power, divination = a.divination }; Grade(expected, skill, r);
            return a.score == expected.score && a.multiplier == expected.multiplier && a.value == expected.value && a.movingLine == expected.movingLine;
        }
        private static bool ValidUses(List<EnemySkillUses> uses, BattleCatalog c, int round)
            => uses != null && uses.Select(x => x?.skillId).Distinct().Count() == uses.Count && uses.All(x => x != null && c.Skill(x.skillId) != null && x.count > 0 && x.lastRound > 0 && x.lastRound <= round && (c.Skill(x.skillId).maximumUses == 0 || x.count <= c.Skill(x.skillId).maximumUses));
        private static bool ValidRecord(DivinationRecord record, BattleSession s, BattleEncounterDefinition d, int round)
            => LiuYaoPaiPan.TryNormalizeCalendar(d.month, d.day, out var month, out var day, out _) &&
                record != null && record.rulesVersion == DivinationRecord.CurrentRulesVersion && record.month == d.month && record.day == d.day && record.revealedLines >= 0 && record.revealedLines <= 6 &&
                CoinCasting.IsValid(record.casting) && record.casting.seed == RoundSeed(s.seed, round) && CoinCasting.Cast(record.casting.seed).coinFaces.SequenceEqual(record.casting.coinFaces) &&
                LiuYaoPaiPan.IsValidResult(record.chart) && record.chart.yueling == month && record.chart.richen == day && record.chart.yao6789.SequenceEqual(record.casting.yaoValues);
        private static bool Fraction(float value, float max) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0 && value <= max;
    }
}

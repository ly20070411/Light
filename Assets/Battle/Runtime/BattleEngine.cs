using System;
using System.Collections.Generic;
using System.Linq;
using Emerge.Checks.Divination;
using Emerge.Checks;
using Emerge.Props;
using UnityEngine;

namespace Emerge.Battle
{
    // Version 2 moves randomness before decisions. Version 1 saves keep their original casting flow.
    public sealed partial class BattleEngine
    {
        public BattleCatalog Catalog { get; }
        public PropGameState Inventory { get; }
        private BattleSession state;
        private BattleLegacyEngine legacy;
        private bool committing;
        public bool EmitRuntimeLogs { get; set; } = true;
        public BattleSession State => legacy != null ? legacy.State : state;
        public bool IsCommitting => committing || (legacy?.IsCommitting ?? false);
        public event Action Changed;
        public BattleEngine(BattleCatalog catalog, PropGameState inventory)
        { string error = null; if (catalog == null || !catalog.Validate(out error)) throw new ArgumentException(error ?? "缺少配置"); Catalog = catalog; Inventory = inventory; }
        public void Start(BattleEncounterDefinition encounter, int seed, string contextId = null, ActorCheckAttributes attributes = null)
        {
            if (encounter == null || Catalog.Encounter(encounter.id) == null || IsCommitting) throw new ArgumentException("未知遭遇或行动未完成");
            encounter = Catalog.Encounter(encounter.id);
            var build = attributes ?? BattleBuildRules.DefaultBuild();
            if (!SixKinAttributes.IsValidLegacyBuild(build)) throw new ArgumentException("五亲点数必须非负且合计为 8。");
            legacy = null; state = new BattleSession { version = BattleBuildRules.Version, attributes = build.Clone(), balanceVersion = Catalog.rules.balanceVersion, seed = seed, sessionId = Guid.NewGuid().ToString("N"), encounterId = encounter.id, contextId = contextId ?? encounter.id,
                player = new BattlePlayerState { hp = Catalog.rules.maxHP, mp = Catalog.rules.maxMP } };
            foreach (var slot in encounter.enemies) { int hp = slot.healthOverride > 0 ? slot.healthOverride : slot.enemy.maxHP; state.enemies.Add(new BattleEnemyState { definitionId = slot.enemy.id, hp = hp, maxHP = hp, mp = slot.enemy.maxMP, defense = slot.enemy.defense }); }
            Log("属性快照 · " + string.Join("、", Enumerable.Range(0, SixKinAttributes.Count).Select(i => SixKinAttributes.Get((CheckBehavior)i).name + " " + build.Get((CheckBehavior)i))) + "；每点基础效果 +10%；种子 " + seed);
            BeginRound(); Log("战斗开始 · " + encounter.displayName); Changed?.Invoke();
        }
        public static int CastSeed(int seed, int serial) => BattleLegacyEngine.CastSeed(seed, serial);
        public static int RoundSeed(int seed, int round) => unchecked(seed ^ round * 1000003 ^ 0x752BAC);
        public static int FamilyScore(DivinationRecord record, BattleFamily family)
        { string name = BattleRules.FamilyName(family); var yaos = record.chart.yaos.Where(x => x.benLiuqin == name); return yaos.Any() ? Mathf.Clamp(yaos.Max(x => x.wangshuaiScore), -2, 2) : 0; }
        public int RemainingUses(string id)
        { var skill = Catalog.Skill(id); return skill == null ? 0 : State?.version == 1 || skill.maximumUses == 0 ? int.MaxValue : Math.Max(0, skill.maximumUses - (State?.skillUses.Find(x => x.skillId == id)?.count ?? 0)); }
        public float SkillMultiplier(string id)
        {
            var skill = Catalog.Skill(id);
            if (skill == null || State == null) return 1;
            if (skill.isUltimate || skill.isPassive) return 1;
            if (State.version >= 3) return BattleBuildRules.Multiplier(BattleBuildRules.Points(State.attributes, skill.family));
            return skill.alwaysAvailable || State.roundDivination == null ? 1 : Catalog.rules.Multiplier(FamilyScore(State.roundDivination, skill.family));
        }
        public bool CanUseSkill(string id, int target, out string reason)
        {
            if (legacy != null) return legacy.CanUseSkill(id, target, out reason);
            reason = ""; var skill = Catalog.Skill(id);
            if (committing || state?.phase != BattlePhase.Player) reason = "请等待当前行动完成";
            else if (skill == null) reason = "技能不存在";
            else if (skill.isPassive) reason = "被动天赋自动生效";
            else if (state.version >= 3 && !skill.alwaysAvailable && !skill.isUltimate && BattleBuildRules.Points(state.attributes, skill.family) == 0) reason = BattleRules.FamilyName(skill.family) + "为 0 点，无法解锁该类高级技能";
            else if (RemainingUses(id) == 0) reason = "本场次数已耗尽";
            else if (!skill.alwaysAvailable && !state.unlockedSkills.Contains(id)) reason = "本轮未解锁";
            else if (state.player.mp < skill.mpCost) reason = "MP 不足";
            else if (skill.target == BattleTarget.Enemy && (target < 0 || target >= state.enemies.Count || state.enemies[target].hp <= 0)) reason = "请选择存活敌人";
            else if (skill.effect == BattleEffect.Heal && state.player.hp >= Catalog.rules.maxHP) reason = "HP 已满";
            else if (skill.effect == BattleEffect.Shield && state.player.shield >= Catalog.rules.shieldCap) reason = "护盾已达上限";
            else if (skill.kind == BattleSkillKind.Legacy && skill.effect == BattleEffect.Reduction && state.player.reduction >= (state.version >= 3 ? Math.Min(Catalog.rules.reductionCap, skill.power * SkillMultiplier(id)) : skill.power)) reason = "已处于守御状态";
            else if (skill.effect == BattleEffect.Cleanse && !HasNegativeState(state.player) && state.player.nextMana >= (state.version >= 3 ? Math.Min(Catalog.rules.nextManaCap, BattleRules.Round(skill.power * SkillMultiplier(id))) : skill.power)) reason = "已清心，无需重复施加";
            else if (skill.kind == BattleSkillKind.TripleChange && state.domainRounds > 0) reason = "三爻同动领域仍在持续";
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
                Grade(action, skill, Catalog.rules, state.version >= 3 ? state.attributes : null);
                action.enhanced = IsEnhanced(id); action.randomSerialBefore = state.randomSerial;
                if (action.enhanced) state.enhancedSkills.Add(id);
                state.player.mp -= skill.mpCost;
                var used = state.skillUses.Find(x => x.skillId == id); if (used == null) { used = new EnemySkillUses { skillId = id }; state.skillUses.Add(used); } used.count++; used.lastRound = state.round;
                if (state.version >= 4) ApplySkillTableAction(action, skill); else ApplySkill(action);
                state.lastAction = action;
                if (state.version >= 3) Log("结算状态 · HP " + state.player.hp + "；MP " + state.player.mp + "；护盾 " + state.player.shield + "；减伤 " + state.player.reduction.ToString("P0") + "；下轮额外回气 " + state.player.nextMana);
                Log(action.skillName + (state.version >= 3 ? " · " + BattleRules.FamilyName(skill.family) + " " + action.score + " 点；固定倍率 " : " · 卦势倍率 ") + "×" + action.multiplier.ToString("0.0") + " · 基础 " + action.power + " → " + action.value); CheckOutcome();
            }
            finally { committing = false; }
            Changed?.Invoke(); return true;
        }
        private static void Grade(BattleAction action, BattleSkillDefinition skill, BattleRules rules, ActorCheckAttributes attributes = null)
        {
            if (attributes != null)
            {
                action.score = skill.isUltimate || skill.isPassive ? 0 : BattleBuildRules.Points(attributes, skill.family);
                action.multiplier = skill.isUltimate || skill.isPassive ? 1 : BattleBuildRules.Multiplier(action.score);
                action.value = BattleRules.Round(action.power * action.multiplier);
                action.movingLine = false;
                return;
            }
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
                    foreach (int i in Targets(a.target, a.targetIndex)) { HitEnemy(i, a.power * a.multiplier * (1 - p.weakness)); var e = state.enemies[i]; if (e.hp > 0 && a.appliesVulnerability && (state.version >= 3 || a.score >= 0)) { e.vulnerability = r.vulnerability; e.vulnerabilityHits = r.vulnerabilityHits + (state.version < 3 && a.movingLine ? 1 : 0); } if (p.hp == 0) break; } break;
                case BattleEffect.Shield: p.shield = Math.Min(r.shieldCap, p.shield + a.value); break;
                case BattleEffect.Reduction: p.reduction = Math.Max(p.reduction, Math.Min(r.reductionCap, a.power * a.multiplier)); break;
                case BattleEffect.Heal: p.hp = Math.Min(r.maxHP, p.hp + a.value); break;
                case BattleEffect.Regeneration: p.regeneration = Math.Max(p.regeneration, a.value); p.regenerationTicks = a.regenerationTicks; break;
                case BattleEffect.NextMana: p.nextMana = Math.Min(r.nextManaCap, Math.Max(p.nextMana, a.value)); break;
                case BattleEffect.Cleanse: Cleanse(p); p.nextMana = Math.Min(r.nextManaCap, Math.Max(p.nextMana, a.value)); break;
                case BattleEffect.Silence: if (state.version < 3 && a.score < 0) Weaken(a.targetIndex, .1f); else state.enemies[a.targetIndex].silenced = true; break;
                case BattleEffect.Bind:
                    var e2 = state.enemies[a.targetIndex];
                    if (state.version >= 3)
                    {
                        if (e2.determined || Catalog.Enemy(e2.definitionId).resistsStun) Weaken(a.targetIndex, Math.Min(.5f, .2f * a.multiplier));
                        else e2.stunned = true;
                    }
                    else if (a.score < 2) Weaken(a.targetIndex, a.score < 0 ? .1f : a.score == 0 ? .2f : .3f);
                    else if (e2.determined || Catalog.Enemy(e2.definitionId).resistsStun) Weaken(a.targetIndex, r.bossWeakness);
                    else e2.stunned = true;
                    break;
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
            state.unlockedSkills = state.version >= 3 ? BattleBuildRules.SelectOffers(Catalog, state.roundDivination, state.roundStartUses, state.attributes, Log, state.round) : SelectOffers(Catalog, state.roundDivination, state.roundStartUses);
            if (state.unlockedSkills.Count == 0) Log("有效高级技能已耗尽，本轮使用常驻技能。");
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
            if (state.version >= 3) Log("第 " + state.round + " 轮定卦 · " + state.roundDivination.chart.benGuaName + "；种子 " + coins.seed + "；爻数 " + string.Join("、", Enum.GetValues(typeof(BattleFamily)).Cast<BattleFamily>().Select(f => BattleRules.FamilyName(f) + "=" + state.roundDivination.chart.yaos.Count(y => y.benLiuqin == BattleRules.FamilyName(f)))) + "；仅用于技能解锁");
            state.roundStartUses = state.skillUses.Select(x => new EnemySkillUses { skillId = x.skillId, count = x.count, lastRound = x.lastRound }).ToList();
            state.unlockedSkills.Clear(); state.lastAction = null; state.phase = BattlePhase.RoundCasting; PrepareIntents();
        }
        public static bool HasNegativeState(BattlePlayerState p) => p.weakness > 0 || p.burnTicks > 0 || p.exposure > 0 || (p.statuses?.Any(s => IsNegativeStatus(s.kind)) ?? false);
        private static void Cleanse(BattlePlayerState p) { p.weakness = p.exposure = 0; p.burn = p.burnTicks = p.exposureUntilRound = 0; p.statuses?.RemoveAll(s => IsNegativeStatus(s.kind)); }
        public bool CanUseItem(string id, out string reason)
        {
            if (legacy != null) return legacy.CanUseItem(id, out reason);
            reason = ""; var item = Catalog.Item(id);
            if (committing || state?.phase != BattlePhase.Player) reason = "请等待当前行动完成";
            else if (item == null || Inventory == null || Inventory.Count(item.inventoryKey) < 1) reason = "背包中没有此物品";
            else if (item.effect == BattleItemEffect.Heal && state.player.hp >= Catalog.rules.maxHP) reason = "HP 已满";
            else if (item.effect == BattleItemEffect.Mana && state.player.mp >= Catalog.rules.maxMP) reason = "MP 已满";
            else if (item.effect == BattleItemEffect.Mana && Status(state.player.statuses, BattleStatusKind.ManaLock) > 0) reason = "禁回 MP 期间无法使用回蓝物品";
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
                switch (item.effect) { case BattleItemEffect.Heal: state.player.hp = Math.Min(Catalog.rules.maxHP, state.player.hp + item.power); break; case BattleItemEffect.Mana: RestoreMana(item.power); break; case BattleItemEffect.Cleanse: Cleanse(state.player); break; case BattleItemEffect.DamageAll: foreach (int i in Targets(BattleTarget.AllEnemies, 0)) { HitEnemy(i, item.power); if (state.player.hp == 0) break; } break; }
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
            if (state.version >= 4) TickSkillTableEnd();
            if (p.burnTicks > 0) { p.hp = Math.Max(0, p.hp - p.burn); p.burnTicks--; Log("灼伤 · 损失 " + p.burn + " HP"); if (p.burnTicks == 0) p.burn = 0; }
            p.weakness = 0; CheckOutcome();
            if (state.phase == BattlePhase.Player)
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
                if (state.version >= 4) TickSkillTableStart();
                state.round++; state.enemyCursor = 0;
                if (state.version < 4 || p.shieldRounds <= 0) p.shield = 0;
                p.reduction = 0;
                if (state.version < 4 || Status(p.statuses, BattleStatusKind.ManaLock) <= 0) p.mp = Math.Min(Catalog.rules.maxMP, p.mp + Catalog.rules.roundMana + p.nextMana);
                p.nextMana = 0;
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
                    if (enemy.mp < skill.mpCost || (enemy.silenced && skill.mpCost > 0) || (state.version >= 4 && Status(enemy.statuses, BattleStatusKind.Taunt) > 0)) skill = Fallback(definition);
                    enemy.mp -= skill.mpCost; var used = enemy.uses.Find(x => x.skillId == skill.id); if (used == null) { used = new EnemySkillUses { skillId = skill.id }; enemy.uses.Add(used); } used.count++; used.lastRound = state.round;
                    enemy.charged = skill.effect == EnemyEffect.Charge;
                    switch (skill.effect)
                    {
                        case EnemyEffect.Shield: enemy.shield = skill.power; break;
                        case EnemyEffect.Heal: enemy.hp = Math.Min(enemy.maxHP, enemy.hp + skill.power); break;
                        case EnemyEffect.Charge: break;
                        default:
                            bool missed = state.version >= 4 && Status(enemy.statuses, BattleStatusKind.Blind) > 0 && NextSkillRoll() < .5f;
                            enemy.statuses?.RemoveAll(s => s.kind == BattleStatusKind.Blind);
                            if (missed) { Log(definition.displayName + " · 匿影，攻击落空"); break; }
                            HitPlayer(skill.power * (1 - enemy.weakness) * (1 - Status(enemy.statuses, BattleStatusKind.DamageDown)), state.enemyCursor - 1, true);
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
                if ((e.silenced && s.mpCost > 0) || mana < s.mpCost || Status(e.statuses, BattleStatusKind.Taunt) > 0) s = Fallback(d);
                int hit = BattleRules.Round(IntentDamage(s) * (1 - e.weakness) * (1 - Status(e.statuses, BattleStatusKind.DamageDown)) * (1 - EffectiveReduction()) * (1 + exposure + Status(State.player.statuses, BattleStatusKind.IncomingUp) + Status(State.player.statuses, BattleStatusKind.DefenseDown)));
                int absorbed = Math.Min(shield, hit); shield -= absorbed; total += hit - absorbed;
                if (s.effect == EnemyEffect.Exposure) exposure = Catalog.rules.exposure;
            }
            return total;
        }
        private static EnemySkillDefinition Fallback(BattleEnemyDefinition d) => d.skills.First(s => s.effect == EnemyEffect.Damage && s.mpCost == 0 && s.maximumHealthFraction == 1 && s.maximumUses == 0 && s.cooldownRounds == 0);
        private IEnumerable<int> Targets(BattleTarget target, int index) { for (int i = 0; i < state.enemies.Count; i++) if (state.enemies[i].hp > 0 && (target == BattleTarget.AllEnemies || i == index)) yield return i; }
        private void HitEnemy(int index, float raw, bool retaliate = true, float ignoreDefense = 0)
        {
            var e = state.enemies[index]; int damage = EnemyDamage(e, raw, ignoreDefense), absorbed = Math.Min(e.shield, damage);
            int healthDamage = Math.Min(e.hp, damage - absorbed); e.shield -= absorbed; e.hp -= healthDamage;
            if (e.vulnerabilityHits > 0 && --e.vulnerabilityHits == 0) e.vulnerability = 0;
            var definition = Catalog.Enemy(e.definitionId);
            if (state.version >= 3)
            {
                Log("命中 " + definition.displayName + "；输入 " + raw.ToString("0.##") + " → 修正伤害 " + damage + "；护盾抵消 " + absorbed + "；剩余 HP " + e.hp);
                if (e.hp <= 0 && healthDamage > 0 && state.version >= 4) RestoreMana(BattleRules.Round(Status(state.player.statuses, BattleStatusKind.KillMana)));
                if (retaliate && healthDamage > 0 && definition.retaliation > 0)
                {
                    int beforeHP = state.player.hp, beforeShield = state.player.shield;
                    float retaliation = definition.retaliation * (1 - e.weakness) * (1 - Status(e.statuses, BattleStatusKind.DamageDown));
                    HitPlayer(healthDamage * retaliation);
                    Log("潮棘反震 · 实际 HP 伤害 " + healthDamage + " ×" + retaliation.ToString("P0") + "；护盾抵消 " + (beforeShield - state.player.shield) + "；主角损失 " + (beforeHP - state.player.hp) + " HP；剩余 " + state.player.hp);
                }
            }
        }
        private static int EnemyDamage(BattleEnemyState e, float raw, float ignoreDefense = 0)
            => BattleRules.Round(raw * (1 - Status(e.statuses, BattleStatusKind.DamageReduction)) * (1 - e.defense * (1 - Mathf.Clamp01(Status(e.statuses, BattleStatusKind.DefenseBreak) + ignoreDefense))) * (1 + (e.vulnerabilityHits > 0 ? e.vulnerability : 0) + Status(e.statuses, BattleStatusKind.IncomingUp)));
        public bool HasRetaliationTarget(BattleTarget target, int index)
            => State?.version >= 3 && State.enemies.Where((e, i) => e.hp > 0 && (target == BattleTarget.AllEnemies || target == BattleTarget.Enemy && i == index)).Any(e => Catalog.Enemy(e.definitionId).retaliation > 0);
        public int ForecastRetaliation(string id, int index, bool item = false)
        {
            if (State?.version < 3 || State == null) return 0;
            if (State.version >= 4) return ForecastTableRetaliation(id, index, item);
            var skill = item ? null : Catalog.Skill(id); var consumable = item ? Catalog.Item(id) : null;
            if (item ? consumable == null || consumable.effect != BattleItemEffect.DamageAll : skill == null || skill.effect != BattleEffect.Damage) return 0;
            var target = item ? BattleTarget.AllEnemies : skill.target;
            float raw = item ? consumable.power : skill.power * SkillMultiplier(id) * (1 - State.player.weakness);
            int total = 0, shield = State.player.shield;
            foreach (var e in State.enemies.Where((e, i) => e.hp > 0 && (target == BattleTarget.AllEnemies || i == index)))
            {
                int healthDamage = Math.Min(e.hp, Math.Max(0, EnemyDamage(e, raw) - e.shield));
                int reflected = BattleRules.Round(healthDamage * Catalog.Enemy(e.definitionId).retaliation * (1 - State.player.reduction) * (1 + State.player.exposure));
                int absorbed = Math.Min(shield, reflected); shield -= absorbed; total += reflected - absorbed;
            }
            return Math.Min(total, State.player.hp);
        }
        private void HitPlayer(float raw, int source = -1, bool enemyAction = false)
        {
            var p = state.player; int damage = BattleRules.Round(raw * (1 - EffectiveReduction()) * (1 + p.exposure + Status(p.statuses, BattleStatusKind.IncomingUp) + Status(p.statuses, BattleStatusKind.DefenseDown)));
            int absorbed = Math.Min(p.shield, damage); p.shield -= absorbed; p.hp = Math.Max(0, p.hp - damage + absorbed);
            if (state.version >= 4 && enemyAction && source >= 0 && damage > 0 && p.hp > 0)
            {
                if (absorbed > 0) { RestoreMana(BattleRules.Round(Status(p.statuses, BattleStatusKind.ManaOnHit))); float reflect = Status(p.statuses, BattleStatusKind.Reflect); if (reflect > 0) HitEnemy(source, reflect, false); }
                if (HasTalent(BattleSkillKind.RecoilTalent) && NextSkillRoll() < .3f) { HitEnemy(source, 7, false); RestoreMana(3); Log("卜卦反噬 · 反伤 7、回复 3 MP"); }
            }
        }
        private void Weaken(int target, float weakness) { state.enemies[target].weakness = Math.Max(state.enemies[target].weakness, weakness); }
        private void CheckOutcome() { if (state.player.hp <= 0) state.phase = BattlePhase.Defeat; else if (state.enemies.All(e => e.hp <= 0)) state.phase = BattlePhase.Victory; }
        private void Log(string text)
        {
            state.log.Add(text); if (state.log.Count > 80) state.log.RemoveAt(0);
            if (EmitRuntimeLogs) { Debug.Log("[战斗链] " + text); BattleLogicTrace.Record(state, text); }
        }
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
        { if (IsCommitting) throw new InvalidOperationException("行动提交期间不能保存"); return new BattleSnapshot { catalogPath = Catalog.SaveResourcePath, session = State == null ? null : JsonUtility.FromJson<BattleSession>(JsonUtility.ToJson(State)) }; }
        public bool Restore(BattleSnapshot snapshot)
        {
            if (IsCommitting || (snapshot?.session != null && snapshot.session.version != 1 && snapshot.session.balanceVersion != Catalog.rules.balanceVersion) || !ValidateSnapshot(snapshot, Catalog)) return false;
            if (snapshot.session?.version == 1) { var restored = new BattleLegacyEngine(Catalog, Inventory); if (!restored.Restore(snapshot)) return false; legacy = restored; legacy.Changed += () => Changed?.Invoke(); state = null; }
            else
            {
                legacy = null; state = snapshot.session == null ? null : JsonUtility.FromJson<BattleSession>(JsonUtility.ToJson(snapshot.session));
                if (state != null && state.version < 4)
                {
                    if (state.player.statuses == null) state.player.statuses = new List<BattleTimedStatus>();
                    if (state.enhancedSkills == null) state.enhancedSkills = new List<string>();
                    if (state.summons == null) state.summons = new List<BattleSummonState>();
                    foreach (var e in state.enemies) if (e.statuses == null) e.statuses = new List<BattleTimedStatus>();
                }
            }
            Changed?.Invoke(); return true;
        }
        public static bool ValidateSnapshot(BattleSnapshot snapshot, BattleCatalog catalog = null)
        {
            if (snapshot == null || snapshot.version != 1 ||
                (snapshot.returnPoint != null && (!BattleReturnPoint.Validate(snapshot.returnPoint) || snapshot.session == null))) return false;
            catalog = ResolveSnapshotCatalog(snapshot, catalog);
            if (!BattleCatalog.TryResolveSaveCatalog(snapshot.catalogPath, catalog, out catalog)) return false;
            if (snapshot.session == null) return true;
            if (snapshot.session.version == 1) return BattleLegacyEngine.ValidateSnapshot(snapshot, catalog);
            var s = snapshot.session; var p = s.player; var r = catalog.rules; var d = catalog.Encounter(s.encounterId);
            if ((s.version != 2 && s.version != 3 && s.version != 4) || (s.version >= 3 && !SixKinAttributes.IsValidLegacyBuild(s.attributes)) || s.balanceVersion != r.balanceVersion || string.IsNullOrWhiteSpace(s.sessionId) || string.IsNullOrWhiteSpace(s.contextId) || s.round < 1 || s.actionSerial < 0 || d == null || p == null ||
                !Enum.IsDefined(typeof(BattlePhase), s.phase) || s.phase == BattlePhase.Casting || s.pending != null ||
                p.hp < 0 || p.hp > r.maxHP || p.mp < 0 || p.mp > r.maxMP || p.shield < 0 || p.shield > r.shieldCap || !Fraction(p.reduction, r.reductionCap) || !Fraction(p.weakness, 1) ||
                !Fraction(p.exposure, r.exposure) || p.exposureUntilRound < 0 || p.exposureUntilRound > s.round + 1 || (p.exposure > 0 && p.exposureUntilRound < s.round) || ((p.exposure == 0) != (p.exposureUntilRound == 0)) ||
                p.burn < 0 || p.burn > r.burnDamage || p.burnTicks < 0 || p.burnTicks > r.burnTicks || ((p.burn == 0) != (p.burnTicks == 0)) ||
                p.nextMana < 0 || p.nextMana > r.nextManaCap || p.regeneration < 0 || p.regeneration > 10000 || p.regenerationTicks < 0 || p.regenerationTicks > 100 ||
                s.enemies == null || s.enemies.Count != d.enemies.Length || s.enemies.Any(e => e == null) || s.enemyCursor < 0 || s.enemyCursor > s.enemies.Count ||
                s.log == null || s.log.Count > 80 || s.log.Any(x => x == null || x.Length > 1000) || !ValidUses(s.skillUses, catalog, s.round) || !ValidUses(s.roundStartUses, catalog, s.round) ||
                s.skillUses.Sum(x => (long)x.count) != s.actionSerial || s.roundStartUses.Any(x => (s.skillUses.Find(u => u.skillId == x.skillId)?.count ?? 0) < x.count) ||
                !ValidRecord(s.roundDivination, s, d, s.round) || s.unlockedSkills == null || s.unlockedSkills.Distinct().Count() != s.unlockedSkills.Count) return false;
            if (s.version >= 4 && !ValidSkillTableState(s, catalog)) return false;
            bool opening = s.phase == BattlePhase.RoundCasting;
            if ((!opening && s.roundDivination.revealedLines != 6) || (opening && s.unlockedSkills.Count != 0) ||
                (!opening && !s.unlockedSkills.SequenceEqual(s.version >= 3 ? BattleBuildRules.SelectOffers(catalog, s.roundDivination, s.roundStartUses, s.attributes, round: s.round) : SelectOffers(catalog, s.roundDivination, s.roundStartUses))) ||
                (s.phase == BattlePhase.Victory && (p.hp <= 0 || s.enemies.Any(e => e.hp > 0))) || (s.phase == BattlePhase.Defeat && p.hp != 0) ||
                ((opening || s.phase == BattlePhase.Player || s.phase == BattlePhase.Enemy) && (p.hp == 0 || s.enemies.All(e => e.hp <= 0))) ||
                (s.outcomeApplied && s.phase != BattlePhase.Victory && s.phase != BattlePhase.Defeat)) return false;
            for (int i = 0; i < s.enemies.Count; i++)
            {
                var e = s.enemies[i]; var def = d.enemies[i].enemy;
                if (e == null || e.definitionId != def.id || e.maxHP != (d.enemies[i].healthOverride > 0 ? d.enemies[i].healthOverride : def.maxHP) || e.hp < 0 || e.hp > e.maxHP ||
                    e.mp < 0 || e.mp > def.maxMP || (s.version >= 4 && (!Fraction(e.defense, .8f) || e.defense != def.defense)) || e.shield < 0 || e.shield > 10000 || e.vulnerabilityHits < 0 || e.vulnerabilityHits > Math.Max(3, r.vulnerabilityHits + 1) || !Fraction(e.vulnerability, .5f) || !Fraction(e.weakness, 1) ||
                    !def.skills.Any(x => x.id == e.intentSkillId) || e.uses == null || e.uses.Any(u => u == null || u.count < 1 || u.lastRound < 1 || u.lastRound > s.round || !def.skills.Any(x => x.id == u.skillId && (x.maximumUses == 0 || u.count <= x.maximumUses))) || e.uses.Select(u => u.skillId).Distinct().Count() != e.uses.Count) return false;
            }
            var a = s.lastAction;
            if (a == null) return true;
            var skill = catalog.Skill(a.skillId);
            if (skill == null || a.round != s.round || a.serial != s.actionSerial || a.power != skill.power || a.family != skill.family || a.effect != skill.effect || a.target != skill.target ||
                a.targetIndex < 0 || a.targetIndex >= s.enemies.Count || a.appliesVulnerability != skill.appliesVulnerability || a.regenerationTicks != skill.regenerationTicks || a.skillName != skill.displayName ||
                !ValidRecord(a.divination, s, d, a.round) || a.divination.revealedLines != 6 || (!skill.alwaysAvailable && !s.unlockedSkills.Contains(skill.id))) return false;
            if (s.version >= 4 && (a.randomSerialBefore < 0 || a.randomSerialBefore > s.randomSerial || (a.enhanced && (!skill.enhancedAvailable || !s.enhancedSkills.Contains(skill.id))) || skill.isPassive || !ValidSkillTableAction(s, skill, a))) return false;
            var expected = new BattleAction { family = a.family, power = a.power, divination = a.divination }; Grade(expected, skill, r, s.version >= 3 ? s.attributes : null);
            return a.score == expected.score && a.multiplier == expected.multiplier && a.value == expected.value && a.movingLine == expected.movingLine;
        }
        public static BattleCatalog ResolveSnapshotCatalog(BattleSnapshot snapshot, BattleCatalog current = null)
        {
            if (snapshot?.session != null && snapshot.session.version <= 3 && snapshot.session.balanceVersion == "v0.5-counterplay" && snapshot.catalogPath == BattleCatalog.ResourcePath &&
                (current == null || current.rules.balanceVersion != snapshot.session.balanceVersion))
                return Resources.Load<BattleCatalog>("Battle/LegacyV05/BattleCatalog");
            return current;
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

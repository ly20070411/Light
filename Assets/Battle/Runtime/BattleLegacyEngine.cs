using System;
using System.Collections.Generic;
using System.Linq;
using Emerge.Checks.Divination;
using Emerge.Props;
using UnityEngine;

namespace Emerge.Battle
{
    // A command completes atomically. Animation only reveals an already committed cast.
    internal sealed class BattleLegacyEngine
    {
        public BattleCatalog Catalog { get; }
        public BattleSession State { get; private set; }
        public PropGameState Inventory { get; }
        public bool IsCommitting { get; private set; }
        public event Action Changed;
        public BattleLegacyEngine(BattleCatalog catalog, PropGameState inventory)
        {
            string error = null;
            if (catalog == null || !catalog.Validate(out error)) throw new ArgumentException(error ?? "缺少配置库");
            Catalog = catalog; Inventory = inventory;
        }
        public void Start(BattleEncounterDefinition encounter, int seed, string contextId = null)
        {
            if (encounter == null || Catalog.Encounter(encounter.id) != encounter) throw new ArgumentException("未知遭遇");
            State = new BattleSession { seed = seed, sessionId = Guid.NewGuid().ToString("N"), encounterId = encounter.id,
                contextId = contextId ?? encounter.id, phase = BattlePhase.Player,
                player = new BattlePlayerState { hp = Catalog.rules.maxHP, mp = Catalog.rules.maxMP } };
            foreach (var slot in encounter.enemies)
            {
                int hp = slot.healthOverride > 0 ? slot.healthOverride : slot.enemy.maxHP;
                State.enemies.Add(new BattleEnemyState { definitionId = slot.enemy.id, maxHP = hp, hp = hp, mp = slot.enemy.maxMP });
            }
            PrepareIntents(); Log("战斗开始 · " + encounter.displayName); Changed?.Invoke();
        }
        public bool CanUseSkill(string id, int target, out string reason)
        {
            reason = ""; var skill = Catalog.Skill(id);
            if (IsCommitting || State == null || State.phase != BattlePhase.Player) reason = "请等待当前行动完成";
            else if (skill == null) reason = "技能不存在";
            else if (State.player.mp < skill.mpCost) reason = "MP 不足";
            else if (skill.target == BattleTarget.Enemy && (target < 0 || target >= State.enemies.Count || State.enemies[target].hp <= 0)) reason = "请选择存活敌人";
            else if (skill.effect == BattleEffect.Heal && State.player.hp >= Catalog.rules.maxHP) reason = "HP 已满";
            else if (skill.effect == BattleEffect.Shield && State.player.shield >= Catalog.rules.shieldCap) reason = "护盾已达上限";
            else if (skill.effect == BattleEffect.Reduction && State.player.reduction >= Catalog.rules.reductionCap) reason = "减伤已达上限";
            return reason.Length == 0;
        }
        public bool CommitSkill(string id, int target, out string reason)
        {
            if (!CanUseSkill(id, target, out reason)) return false;
            var skill = Catalog.Skill(id); var encounter = Catalog.Encounter(State.encounterId);
            int serial = State.actionSerial + 1;
            var casting = CoinCasting.Cast(CastSeed(State.seed, serial));
            var record = new DivinationRecord { month = encounter.month, day = encounter.day, casting = casting,
                chart = new LiuYaoPaiPan().PaiPan(encounter.month, encounter.day, casting.yaoValues), revealedLines = 0 };
            State.pending = new BattleAction { serial = serial, skillId = id, skillName = skill.displayName, targetIndex = skill.target == BattleTarget.Enemy ? target : 0,
                family = skill.family, target = skill.target, effect = skill.effect, power = skill.power,
                appliesVulnerability = skill.appliesVulnerability, regenerationTicks = skill.regenerationTicks, divination = record };
            State.actionSerial = serial; State.player.mp -= skill.mpCost; State.phase = BattlePhase.Casting;
            Log(skill.displayName + " · 消耗 " + skill.mpCost + " MP，开始起卦"); Changed?.Invoke(); return true;
        }
        public bool RevealLine()
        {
            if (IsCommitting || State?.phase != BattlePhase.Casting || State.pending == null || State.pending.divination.revealedLines >= 6) return false;
            State.pending.divination.revealedLines++; Changed?.Invoke(); return true;
        }
        public bool ResolveSkill()
        {
            if (IsCommitting || State?.phase != BattlePhase.Casting || State.pending?.divination.revealedLines != 6) return false;
            var action = State.pending; var p = State.player; var rules = Catalog.rules;
            Grade(action, rules);
            switch (action.effect)
            {
                case BattleEffect.Damage:
                    foreach (int i in Targets(action.target, action.targetIndex))
                    {
                        HitEnemy(i, action.power * action.multiplier * (1 - p.weakness));
                        var enemy = State.enemies[i];
                        if (enemy.hp > 0 && action.appliesVulnerability && action.score >= 0)
                        { enemy.vulnerability = rules.vulnerability; enemy.vulnerabilityHits = rules.vulnerabilityHits + (action.movingLine ? 1 : 0); }
                    }
                    break;
                case BattleEffect.Shield: p.shield = Math.Min(rules.shieldCap, p.shield + action.value); break;
                case BattleEffect.Reduction: p.reduction = Mathf.Max(p.reduction, Mathf.Min(rules.reductionCap, action.power * action.multiplier)); break;
                case BattleEffect.Heal: p.hp = Math.Min(rules.maxHP, p.hp + action.value); break;
                case BattleEffect.Regeneration:
                    p.regeneration = Math.Max(p.regeneration, action.value); p.regenerationTicks = action.regenerationTicks; break;
                case BattleEffect.NextMana: p.nextMana = Math.Min(rules.nextManaCap, Math.Max(p.nextMana, action.value)); break;
                case BattleEffect.Cleanse:
                    p.weakness = 0; p.nextMana = Math.Min(rules.nextManaCap, Math.Max(p.nextMana, action.value)); break;
                case BattleEffect.Silence:
                    if (action.score < 0) Weaken(action.targetIndex, .1f); else State.enemies[action.targetIndex].silenced = true; break;
                case BattleEffect.Bind:
                    var e = State.enemies[action.targetIndex];
                    if (action.score < 2) Weaken(action.targetIndex, action.score < 0 ? .1f : action.score == 0 ? .2f : .3f);
                    else if (e.determined || Catalog.Enemy(e.definitionId).resistsStun) Weaken(action.targetIndex, rules.bossWeakness);
                    else e.stunned = true;
                    break;
            }
            Log(action.skillName + " · " + BattleRules.FamilyName(action.family) + " " + action.score.ToString("+0;-0;0") +
                " / ×" + action.multiplier.ToString("0.0") + " · " + EffectText(action));
            State.lastAction = action; State.pending = null; State.phase = BattlePhase.Player; CheckOutcome(); Changed?.Invoke(); return true;
        }
        public bool CanUseItem(string id, out string reason)
        {
            reason = ""; var item = Catalog.Item(id);
            if (IsCommitting || State?.phase != BattlePhase.Player) reason = "请等待当前行动完成";
            else if (item == null || Inventory == null || Inventory.Count(item.inventoryKey) < 1) reason = "背包中没有此物品";
            else if (item.effect == BattleItemEffect.Heal && State.player.hp >= Catalog.rules.maxHP) reason = "HP 已满";
            else if (item.effect == BattleItemEffect.Mana && State.player.mp >= Catalog.rules.maxMP) reason = "MP 已满";
            else if (item.effect == BattleItemEffect.Cleanse && State.player.weakness <= 0) reason = "没有可移除的负面状态";
            return reason.Length == 0;
        }
        public bool UseItem(string id, out string reason)
        {
            if (!CanUseItem(id, out reason)) return false;
            var item = Catalog.Item(id); IsCommitting = true;
            try
            {
                // Inventory callbacks cannot observe/save half of this transaction.
                if (!Inventory.RemoveItem(item.inventoryKey, 1)) return false;
                switch (item.effect)
                {
                    case BattleItemEffect.Heal: State.player.hp = Math.Min(Catalog.rules.maxHP, State.player.hp + item.power); break;
                    case BattleItemEffect.Mana: State.player.mp = Math.Min(Catalog.rules.maxMP, State.player.mp + item.power); break;
                    case BattleItemEffect.Cleanse: State.player.weakness = 0; break;
                    case BattleItemEffect.DamageAll: foreach (int i in Targets(BattleTarget.AllEnemies, 0)) HitEnemy(i, item.power); break;
                }
                Log("使用 " + item.displayName + " · 剩余 " + Inventory.Count(item.inventoryKey)); CheckOutcome();
            }
            finally { IsCommitting = false; }
            Changed?.Invoke(); return true;
        }
        public bool EndTurn()
        {
            if (IsCommitting || State?.phase != BattlePhase.Player) return false;
            State.phase = BattlePhase.Enemy; State.enemyCursor = 0; State.player.weakness = 0;
            // Enemies' old shields protected this player phase only.
            foreach (var e in State.enemies)
            { e.shield = 0; var d = Catalog.Enemy(e.definitionId); e.mp = Math.Min(d.maxMP, e.mp + d.roundMana); }
            Log("结束玩家回合"); Changed?.Invoke(); return true;
        }
        public bool StepEnemy()
        {
            if (IsCommitting || State?.phase != BattlePhase.Enemy) return false;
            if (State.enemyCursor >= State.enemies.Count) { BeginNextRound(); Changed?.Invoke(); return true; }
            var e = State.enemies[State.enemyCursor++]; var d = Catalog.Enemy(e.definitionId);
            if (e.hp > 0)
            {
                if (e.stunned) { Log(d.displayName + " · 眩晕，跳过行动；获得定力"); e.determined = true; }
                else
                {
                    var skill = d.skills.FirstOrDefault(s => s.id == e.intentSkillId);
                    if (skill == null || e.mp < skill.mpCost || (e.silenced && skill.mpCost > 0)) skill = Fallback(d);
                    e.mp -= skill.mpCost;
                    var uses = e.uses.FirstOrDefault(s => s.skillId == skill.id);
                    if (uses == null) { uses = new EnemySkillUses { skillId = skill.id }; e.uses.Add(uses); } uses.count++;
                    switch (skill.effect)
                    {
                        case EnemyEffect.Damage: HitPlayer(skill.power * (1 - e.weakness)); break;
                        case EnemyEffect.Weaken: HitPlayer(skill.power * (1 - e.weakness)); State.player.weakness = Mathf.Max(State.player.weakness, skill.weakness); break;
                        case EnemyEffect.Shield: e.shield = skill.power; break;
                        case EnemyEffect.Heal: e.hp = Math.Min(e.maxHP, e.hp + skill.power); break;
                    }
                    Log(d.displayName + " · " + skill.displayName); e.determined = false;
                }
                e.stunned = false; e.silenced = false; e.weakness = 0;
            }
            CheckOutcome(); Changed?.Invoke(); return true;
        }
        private void BeginNextRound()
        {
            var p = State.player; var r = Catalog.rules;
            State.round++; State.enemyCursor = 0; p.shield = 0; p.reduction = 0;
            p.mp = Math.Min(r.maxMP, p.mp + r.roundMana + p.nextMana); p.nextMana = 0;
            if (p.regenerationTicks > 0)
            { p.hp = Math.Min(r.maxHP, p.hp + p.regeneration); if (--p.regenerationTicks == 0) p.regeneration = 0; }
            State.phase = BattlePhase.Player; PrepareIntents(); Log("第 " + State.round + " 轮 · 恢复 MP，结算续航");
        }
        private void PrepareIntents()
        {
            // AI and divination have separate streams. Casting more skills never changes intentions.
            var random = new System.Random(unchecked(State.seed ^ (State.round * 104729) ^ 0x15AF29));
            foreach (var e in State.enemies)
            {
                var d = Catalog.Enemy(e.definitionId);
                var candidates = d.skills.Where(s => (int)s.effect <= 3 && s.mpCost <= Math.Min(d.maxMP, e.mp + d.roundMana) &&
                    e.hp <= e.maxHP * s.maximumHealthFraction && e.shield < s.maximumShield &&
                    (s.maximumUses == 0 || (e.uses.FirstOrDefault(u => u.skillId == s.id)?.count ?? 0) < s.maximumUses)).ToArray();
                int total = candidates.Sum(s => s.weight); int roll = total > 0 ? random.Next(total) : 0;
                var selected = Fallback(d);
                foreach (var s in candidates) { if (roll < s.weight) { selected = s; break; } roll -= s.weight; }
                e.intentSkillId = selected.id;
            }
        }
        private static EnemySkillDefinition Fallback(BattleEnemyDefinition d) => d.skills.First(s => s.effect == EnemyEffect.Damage && s.mpCost == 0 && s.maximumHealthFraction == 1 && s.maximumUses == 0);
        private IEnumerable<int> Targets(BattleTarget target, int index)
        { for (int i = 0; i < State.enemies.Count; i++) if (State.enemies[i].hp > 0 && (target == BattleTarget.AllEnemies || i == index)) yield return i; }
        private void HitEnemy(int i, float power)
        {
            var e = State.enemies[i]; int damage = BattleRules.Round(power * (1 + (e.vulnerabilityHits > 0 ? e.vulnerability : 0)));
            int shield = Math.Min(e.shield, damage); e.shield -= shield; e.hp = Math.Max(0, e.hp - (damage - shield));
            if (e.vulnerabilityHits > 0 && --e.vulnerabilityHits == 0) e.vulnerability = 0;
            Log(Catalog.Enemy(e.definitionId).displayName + " 受到 " + damage + " 伤害" + (shield > 0 ? "（护盾吸收 " + shield + "）" : ""));
        }
        private void HitPlayer(float power)
        {
            var p = State.player; int damage = BattleRules.Round(power * (1 - p.reduction)); int shield = Math.Min(p.shield, damage);
            p.shield -= shield; p.hp = Math.Max(0, p.hp - (damage - shield)); Log("主角受到 " + damage + " 伤害（护盾吸收 " + shield + "）");
        }
        private void Weaken(int i, float value) => State.enemies[i].weakness = Mathf.Max(State.enemies[i].weakness, value);
        private void CheckOutcome()
        {
            if (State.player.hp <= 0) State.phase = BattlePhase.Defeat;
            else if (State.enemies.All(e => e.hp <= 0)) State.phase = BattlePhase.Victory;
        }
        public void ApplyOutcome()
        {
            if (State == null || State.outcomeApplied || (State.phase != BattlePhase.Victory && State.phase != BattlePhase.Defeat)) return;
            IsCommitting = true;
            try
            {
                State.outcomeApplied = true;
                if (State.phase == BattlePhase.Victory && Inventory != null)
                { Inventory.SetFlag("battle-won:" + State.contextId); var d = Catalog.Encounter(State.encounterId); if (!string.IsNullOrEmpty(d.victoryFlag)) Inventory.SetFlag(d.victoryFlag); }
            }
            finally { IsCommitting = false; }
        }
        public static int CastSeed(int seed, int serial) => unchecked(seed + serial * 1000003 + 0x53A9);
        private static void Grade(BattleAction action, BattleRules rules)
        {
            string family = BattleRules.FamilyName(action.family); var yaos = action.divination.chart.yaos.Where(y => y.benLiuqin == family).ToArray();
            int score = yaos.Length == 0 ? 0 : yaos.Max(y => y.wangshuaiScore);
            var representative = yaos.Where(y => y.wangshuaiScore == score).OrderBy(y => y.index).FirstOrDefault();
            action.score = Mathf.Clamp(score, -2, 2); action.movingLine = representative != null && representative.isDongYao;
            action.multiplier = rules.Multiplier(action.score); action.value = BattleRules.Round(action.power * action.multiplier);
        }
        private static string EffectText(BattleAction a)
        {
            switch (a.effect)
            {
                case BattleEffect.Reduction: return "减伤 " + (a.power * a.multiplier).ToString("P0");
                case BattleEffect.Bind: return a.score >= 2 ? "缚灵：眩晕或削弱" : "削弱攻击";
                case BattleEffect.Silence: return a.score < 0 ? "削弱攻击 10%" : "封锁耗 MP 的技能";
                case BattleEffect.Regeneration: return "每轮回复 " + a.value + " HP，持续 " + a.regenerationTicks + " 轮";
                case BattleEffect.NextMana: case BattleEffect.Cleanse: return "下轮额外恢复 " + a.value + " MP";
                default: return "基础效果 " + a.value;
            }
        }
        private void Log(string text) { State.log.Add(text); if (State.log.Count > 80) State.log.RemoveAt(0); }
        public BattleSnapshot Capture()
        {
            if (IsCommitting) throw new InvalidOperationException("行动正在提交，请稍后保存");
            return new BattleSnapshot { session = State == null ? null : JsonUtility.FromJson<BattleSession>(JsonUtility.ToJson(State)) };
        }
        public bool Restore(BattleSnapshot snapshot)
        {
            if (!ValidateSnapshot(snapshot, Catalog)) return false;
            State = snapshot?.session == null ? null : JsonUtility.FromJson<BattleSession>(JsonUtility.ToJson(snapshot.session)); Changed?.Invoke(); return true;
        }
        public static bool ValidateSnapshot(BattleSnapshot snapshot, BattleCatalog catalog = null)
        {
            if (snapshot == null || snapshot.version != 1 || snapshot.catalogPath != BattleCatalog.ResourcePath) return false;
            if (snapshot.returnPoint != null && (!BattleReturnPoint.Validate(snapshot.returnPoint) || snapshot.session == null)) return false;
            if (snapshot.session == null) return true;
            if (catalog == null) catalog = Resources.Load<BattleCatalog>(snapshot.catalogPath);
            if (catalog == null || !catalog.Validate(out _)) return false;
            var s = snapshot.session; var r = catalog.rules; var d = catalog.Encounter(s.encounterId); var p = s.player;
            if (s.version != 1 || string.IsNullOrEmpty(s.sessionId) || string.IsNullOrEmpty(s.contextId) || d == null || s.round < 1 || s.actionSerial < 0 ||
                !Enum.IsDefined(typeof(BattlePhase), s.phase) || p == null || s.enemies == null || s.enemies.Count != d.enemies.Length ||
                s.enemyCursor < 0 || s.enemyCursor > s.enemies.Count || s.log == null || s.log.Count > 80 || s.log.Any(x => x == null || x.Length > 1000) ||
                p.hp < 0 || p.hp > r.maxHP || p.mp < 0 || p.mp > r.maxMP || p.shield < 0 || p.shield > r.shieldCap ||
                p.regeneration < 0 || p.regeneration > 10000 || p.regenerationTicks < 0 || p.regenerationTicks > 100 || p.nextMana < 0 || p.nextMana > r.nextManaCap ||
                !Fraction(p.reduction, r.reductionCap) || !Fraction(p.weakness, 1)) return false;
            for (int i = 0; i < s.enemies.Count; i++)
            {
                var e = s.enemies[i]; var def = d.enemies[i].enemy;
                if (e == null || e.definitionId != def.id || (e.maxHP < 1 || e.maxHP > 10000) ||
                    e.hp < 0 || e.hp > e.maxHP || e.mp < 0 || e.mp > def.maxMP || e.shield < 0 || e.shield > 10000 ||
                    e.vulnerabilityHits < 0 || e.vulnerabilityHits > r.vulnerabilityHits + 1 || !Fraction(e.vulnerability, r.vulnerability) || !Fraction(e.weakness, 1) ||
                    !def.skills.Any(x => x.id == e.intentSkillId) || e.uses == null || e.uses.Any(u => u == null || u.count < 0 || !def.skills.Any(x => x.id == u.skillId)) ||
                    e.uses.Select(u => u.skillId).Distinct().Count() != e.uses.Count) return false;
            }
            if ((s.phase == BattlePhase.Casting) != (s.pending != null) || (s.phase == BattlePhase.Victory && (p.hp <= 0 || s.enemies.Any(e => e.hp > 0))) ||
                (s.phase == BattlePhase.Defeat && p.hp != 0) || ((s.phase == BattlePhase.Player || s.phase == BattlePhase.Casting || s.phase == BattlePhase.Enemy) &&
                (p.hp == 0 || s.enemies.All(e => e.hp == 0))) || (s.outcomeApplied && s.phase != BattlePhase.Victory && s.phase != BattlePhase.Defeat)) return false;
            return ValidAction(s.pending, s, catalog, true) && ValidAction(s.lastAction, s, catalog, false);
        }
        private static bool Fraction(float v, float max) => !float.IsNaN(v) && !float.IsInfinity(v) && v >= 0 && v <= max;
        private static bool ValidAction(BattleAction a, BattleSession s, BattleCatalog c, bool pending)
        {
            if (a == null) return true;
            var skill = c.Skill(a.skillId); var record = a.divination; var d = c.Encounter(s.encounterId);
            if (skill == null || a.serial < 1 || a.serial > s.actionSerial || (pending && a.serial != s.actionSerial) || a.power != skill.power ||
                a.family != skill.family || a.effect != skill.effect || a.target != skill.target || a.appliesVulnerability != skill.appliesVulnerability ||
                a.regenerationTicks != skill.regenerationTicks || a.targetIndex < 0 || a.targetIndex >= s.enemies.Count || record == null ||
                record.rulesVersion != DivinationRecord.CurrentRulesVersion || record.month != d.month || record.day != d.day ||
                record.revealedLines < 0 || record.revealedLines > 6 || (!pending && record.revealedLines != 6) ||
                !CoinCasting.IsValid(record.casting) || record.casting.seed != CastSeed(s.seed, a.serial) ||
                !CoinCasting.Cast(record.casting.seed).coinFaces.SequenceEqual(record.casting.coinFaces) || !LiuYaoPaiPan.IsValidResult(record.chart) ||
                !record.chart.yao6789.SequenceEqual(record.casting.yaoValues)) return false;
            if (!pending)
            { var expected = new BattleAction { family = a.family, power = a.power, divination = a.divination }; Grade(expected, c.rules);
                if (expected.score != a.score || expected.multiplier != a.multiplier || expected.value != a.value || expected.movingLine != a.movingLine) return false; }
            return true;
        }
    }
}

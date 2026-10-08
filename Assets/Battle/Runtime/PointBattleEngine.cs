using System;
using System.Collections.Generic;
using System.Linq;
using Emerge.Checks;
using Emerge.Checks.Divination;
using Emerge.Props;
using UnityEngine;

namespace Emerge.Battle
{
    // A separate ruleset: legacy MP sessions continue to use BattleEngine.
    public sealed class PointBattleEngine
    {
        public PointBattleSession State { get; private set; }
        public event Action Changed;
        private readonly PropGameState inventory;
        public PointBattleEngine(PropGameState inventory = null) { this.inventory = inventory; }
        public static bool Finite(double n) => !double.IsNaN(n) && !double.IsInfinity(n);
        public static PointCalculationResult Calculate(ActorCheckAttributes attributes, CheckBehavior kin, int check, List<FrozenPointItem> equipment)
        {
            // Clamp only after every ordered item effect; negative intermediate values remain visible.
            var result = PointCalculation.Calculate(attributes, kin, check, equipment);
            result.finalPoints = Math.Max(0, result.finalPoints);
            result.action = ActionValue(kin, result.finalPoints);
            return result;
        }
        public static PointActionValue ActionValue(CheckBehavior kin, int x)
        {
            switch (kin)
            {
                case CheckBehavior.Wealth: return new PointActionValue { kind = PointActionKind.Damage, amount = x, description = "对目标造成 " + x + " 点伤害" };
                case CheckBehavior.Officer: return new PointActionValue { kind = PointActionKind.Shield, amount = x, description = "获得 " + x + " 点护盾" };
                case CheckBehavior.Offspring: return new PointActionValue { kind = PointActionKind.Vulnerability, amount = x * .125, description = "目标易伤 +" + (x * 12.5).ToString("0.###") + "%" };
                case CheckBehavior.Sibling: return new PointActionValue { kind = PointActionKind.ReduceEnemyAttackPoints, amount = x * 1.25, description = "目标攻击点数 −" + (x * 1.25).ToString("0.###") };
                case CheckBehavior.Parent: return new PointActionValue { kind = PointActionKind.DamageBonus, amount = x * .1, description = "自身增伤 +" + (x * 10.0).ToString("0.###") + "%" };
                case CheckBehavior.Self: return new PointActionValue { kind = PointActionKind.ActionPoints, amount = x, description = "本回合自动获得 " + x + " 行动点" };
                default: throw new ArgumentOutOfRangeException(nameof(kin));
            }
        }
        public void Start(string encounter, string context, string name, string victoryFlag, int swiftRounds, int seed,
            PointBattleDefinition rules, ActorCheckAttributes attributes, List<FrozenPointItem> equipment, string anchor,
            List<PointEnemyProfile> enemies, string month = "巳月", string day = "戊子日")
        {
            if (State != null || rules == null || !rules.Valid() || !SixKinAttributes.IsValidBuild(attributes) ||
                !PointCalculation.ValidFrozenItems(equipment) || !MentalAnchors.ValidEquipped(anchor) || enemies == null || enemies.Count == 0 ||
                enemies.Any(e => e == null || !e.Valid()) || string.IsNullOrWhiteSpace(encounter) || string.IsNullOrWhiteSpace(context))
                throw new ArgumentException("点数战斗配置无效。");
            if (!LiuYaoPaiPan.TryNormalizeCalendar(month, day, out month, out day, out _)) throw new ArgumentException("月令或日辰无效。");
            State = new PointBattleSession { sessionId = Guid.NewGuid().ToString("N"), encounterId = encounter, contextId = context,
                name = name, victoryFlag = victoryFlag, swiftVictoryRounds = Math.Max(1, swiftRounds), seed = seed,
                balanceVersion = rules.balanceVersion, hp = rules.playerHealth, maximumHP = rules.playerHealth,
                actionCost = rules.actionCost, roundAP = rules.roundActionPoints, carryLimit = rules.carryLimit,
                temporaryActionStatuses = rules.temporaryActionStatuses, attributes = attributes.Clone(), anchor = anchor,
                equipment = JsonUtility.FromJson<EquipmentCopy>(JsonUtility.ToJson(new EquipmentCopy { items = equipment })).items,
                enemies = enemies.Select(e => new PointEnemyState { profile = JsonUtility.FromJson<PointEnemyProfile>(JsonUtility.ToJson(e)), hp = e.health }).ToList(),
                divination = new DivinationRecord { month = month, day = day } };
            BeginRound(); Changed?.Invoke();
        }
        [Serializable] private sealed class EquipmentCopy { public List<FrozenPointItem> items; }
        private void Log(string line)
        { State.log.Add(line); if (State.log.Count > 80) State.log.RemoveAt(0); }
        private void BeginRound()
        {
            var s = State; s.round++; s.shield = 0; s.enemyCursor = 0; s.actions.Clear(); s.ap = 0;
            if (s.corrosion > 0) { s.hp = Math.Max(0, s.hp - s.corrosion); Log("流蚀：主角失去 " + s.corrosion + " 生命"); s.corrosion--; }
            foreach (var enemy in s.enemies.Where(e => e.Alive))
            {
                if (enemy.corrosion > 0) { enemy.hp = Math.Max(0, enemy.hp - enemy.corrosion); Log(enemy.profile.name + " 流蚀 −" + enemy.corrosion); enemy.corrosion--; }
                enemy.intentIndex = (s.round - 1) % enemy.profile.pattern.Count;
            }
            if (FinishIfNeeded()) return;
            var casting = CoinCasting.Cast(unchecked(s.seed + s.round * 104729));
            s.divination = new DivinationRecord { month = s.divination.month, day = s.divination.day, casting = casting };
            s.phase = PointBattlePhase.Casting; Log("第 " + s.round + " 回合 · 定卦");
        }
        public bool RevealLine()
        {
            if (State?.phase != PointBattlePhase.Casting || State.divination.revealedLines >= 6) return false;
            State.divination.revealedLines++; Changed?.Invoke(); return true;
        }
        public bool ResolveRound()
        {
            var s = State;
            if (s?.phase != PointBattlePhase.Casting || s.divination.revealedLines != 6) return false;
            var record = s.divination;
            record.chart = new LiuYaoPaiPan().PaiPan(record.month, record.day, record.casting.yaoValues);
            for (int i = 0; i < 6; i++)
            {
                int raw = record.chart.behaviorModifiers[i];
                bool deer = s.anchor == MentalAnchors.LuJianshen;
                int effective = deer && raw > 0 ? checked(raw * 2) : raw;
                var action = new PointBattleRoundAction { rawCheckPoints = raw, effectiveCheckPoints = effective,
                    available = !deer || raw > 0, unavailableReason = deer && raw <= 0 ? "鹿见深精神锚：非正检定，本回合不可用" : "",
                    calculation = Calculate(s.attributes, (CheckBehavior)i, effective, s.equipment) };
                s.actions.Add(action);
            }
            var self = s.actions[(int)CheckBehavior.Self];
            s.ap = checked(s.carriedAP + s.roundAP + (self.available ? self.calculation.finalPoints : 0));
            s.carriedAP = 0; s.phase = PointBattlePhase.Player;
            Log("行动点 " + s.ap + "（每回合 " + s.roundAP + " + 我 " + (self.available ? self.calculation.finalPoints : 0) + " + 上回合保留）");
            if (self.available) ApplyAddons(self.calculation.actionAddons, s.enemies.FindIndex(e => e.Alive));
            FinishIfNeeded(); Changed?.Invoke(); return true;
        }
        public bool CanAct(CheckBehavior kin, int target, out string reason)
        {
            reason = ""; var s = State;
            if (s?.phase != PointBattlePhase.Player) { reason = "现在不能行动"; return false; }
            if ((int)kin < 0 || (int)kin >= 5) { reason = "我只在回合开始自动提供行动点"; return false; }
            if (s.ap < s.actionCost) { reason = "行动点不足（需要 " + s.actionCost + "）"; return false; }
            var action = s.actions[(int)kin];
            if (!action.available) { reason = action.unavailableReason; return false; }
            if (kin == CheckBehavior.Sibling && s.anchor == MentalAnchors.LinXi && action.uses >= 2)
            { reason = "林溪精神锚：兄弟每回合最多两次"; return false; }
            if ((kin == CheckBehavior.Wealth || kin == CheckBehavior.Offspring || kin == CheckBehavior.Sibling ||
                action.calculation.actionAddons.Any(a => NeedsEnemy(a.effectKey))) &&
                (target < 0 || target >= s.enemies.Count || !s.enemies[target].Alive))
            { reason = "请选择存活的目标"; return false; }
            return true;
        }
        private static bool NeedsEnemy(string key) => key == "damage" || key == "corrosion" || key == "vulnerability" || key == "attackReduction";
        public bool Act(CheckBehavior kin, int target, out string reason)
        {
            if (!CanAct(kin, target, out reason)) return false;
            var s = State; var action = s.actions[(int)kin]; int x = action.calculation.finalPoints;
            s.ap -= s.actionCost; action.uses++;
            switch (kin)
            {
                case CheckBehavior.Wealth: DamageEnemy(target, x); break;
                case CheckBehavior.Officer: s.shield += x; break;
                case CheckBehavior.Offspring: s.enemies[target].vulnerability += x * .125; break;
                case CheckBehavior.Sibling:
                    s.enemies[target].attackReduction += x * 1.25;
                    if (s.anchor == MentalAnchors.LinXi) s.enemies[target].corrosion = checked(s.enemies[target].corrosion + x / 2);
                    break;
                case CheckBehavior.Parent: s.damageBonus += x * .1; break;
            }
            ApplyAddons(action.calculation.actionAddons, target);
            Log(SixKinAttributes.Get(kin).name + "：" + action.calculation.action.description + "；剩余 AP " + s.ap);
            FinishIfNeeded(); Changed?.Invoke(); return true;
        }
        private void ApplyAddons(List<ResolvedPointAddon> addons, int target)
        {
            foreach (var addon in addons)
            {
                int amount = Math.Max(0, addon.amount); var s = State;
                var enemy = target >= 0 && target < s.enemies.Count ? s.enemies[target] : null;
                switch (addon.effectKey)
                {
                    case "damage": if (enemy?.Alive == true) DamageEnemy(target, amount); break;
                    case "shield": s.shield += amount; break;
                    case "heal": s.hp = Math.Min(s.maximumHP, s.hp + amount); break;
                    case "corrosion": if (enemy?.Alive == true) enemy.corrosion = checked(enemy.corrosion + amount); break;
                    case "vulnerability": if (enemy?.Alive == true) enemy.vulnerability += amount / 100.0; break;
                    case "attackReduction": if (enemy?.Alive == true) enemy.attackReduction += amount; break;
                    case "damageBonus": s.damageBonus += amount / 100.0; break;
                    default: throw new InvalidOperationException("未实现的行动附加效果：" + addon.effectKey);
                }
                Log("道具附加：" + addon.description + "（" + amount + "）");
            }
        }
        private void DamageEnemy(int target, double amount)
        {
            var s = State; var e = s.enemies[target];
            double damage = Math.Max(0, amount * (1 + s.damageBonus) * (1 + e.vulnerability) - s.weakness);
            double absorbed = Math.Min(e.shield, damage); e.shield -= absorbed; e.hp = Math.Max(0, e.hp - (damage - absorbed));
            Log(e.profile.name + " 受到 " + damage.ToString("0.##") + " 伤害");
        }
        private void DamagePlayer(double amount)
        {
            var s = State; double damage = Math.Max(0, amount) * (1 + s.vulnerability);
            double absorbed = Math.Min(s.shield, damage); s.shield -= absorbed; s.hp = Math.Max(0, s.hp - (damage - absorbed));
            Log("主角受到 " + damage.ToString("0.##") + " 伤害（护盾吸收 " + absorbed.ToString("0.##") + "）");
        }
        public double PreviewEnemyAttack(int index)
        {
            var e = State.enemies[index]; var move = e.Intent;
            return move.effect == PointEnemyEffect.Attack ? Math.Max(0, move.amount + e.strength - e.attackReduction) * (1 + State.vulnerability) : 0;
        }
        public bool EndTurn()
        {
            var s = State; if (s?.phase != PointBattlePhase.Player) return false;
            s.carriedAP = Math.Min(s.carryLimit, s.ap); s.ap = 0; s.phase = PointBattlePhase.Enemy; s.enemyCursor = 0;
            // Enemy block lasts through the player's next turn, then clears before their next action.
            foreach (var e in s.enemies) e.shield = 0;
            Changed?.Invoke(); return true;
        }
        public bool UseItem(BattleItemDefinition item, out string error)
        {
            error = "当前不可使用该物品。"; var s = State;
            if (s?.phase != PointBattlePhase.Player || item == null || item.effect == BattleItemEffect.Mana || item.pointPower < 1 ||
                inventory == null || s.ap < s.actionCost || inventory.Count(item.inventoryKey) < 1) return false;
            if (!inventory.RemoveItem(item.inventoryKey, 1)) return false;
            s.ap -= s.actionCost;
            switch (item.effect)
            {
                case BattleItemEffect.Heal: s.hp = Math.Min(s.maximumHP, s.hp + item.pointPower); break;
                case BattleItemEffect.Cleanse: s.corrosion = 0; s.vulnerability = 0; s.weakness = 0; break;
                case BattleItemEffect.DamageAll:
                    for (int i = 0; i < s.enemies.Count; i++) if (s.enemies[i].Alive) DamageEnemy(i, item.pointPower);
                    break;
            }
            Log("使用 " + item.displayName + " · −2 AP"); error = ""; FinishIfNeeded(); Changed?.Invoke(); return true;
        }
        public bool StepEnemy()
        {
            var s = State; if (s?.phase != PointBattlePhase.Enemy) return false;
            if (s.enemyCursor < s.enemies.Count)
            {
                var e = s.enemies[s.enemyCursor++];
                if (e.Alive)
                {
                    var move = e.Intent; Log(e.profile.name + " · " + move.name);
                    switch (move.effect)
                    {
                        case PointEnemyEffect.Attack:
                            for (int hit = 0; hit < move.hits && s.hp > 0; hit++) DamagePlayer(move.amount + e.strength - e.attackReduction);
                            break;
                        case PointEnemyEffect.Shield: e.shield += move.amount; break;
                        case PointEnemyEffect.Heal: e.hp = Math.Min(e.profile.health, e.hp + move.amount); break;
                        case PointEnemyEffect.Strength: e.strength += move.amount; break;
                        case PointEnemyEffect.Vulnerability: s.vulnerability += move.amount / 100; s.debuffsExpireRound = s.round + 1; break;
                        case PointEnemyEffect.Weaken: s.weakness += move.amount; s.debuffsExpireRound = s.round + 1; break;
                        case PointEnemyEffect.Corrosion: s.corrosion = checked(s.corrosion + (int)Math.Floor(move.amount)); break;
                    }
                }
                FinishIfNeeded();
            }
            if (s.phase == PointBattlePhase.Enemy && s.enemyCursor == s.enemies.Count)
            {
                if (s.temporaryActionStatuses)
                {
                    s.damageBonus = 0;
                    foreach (var e in s.enemies) { e.vulnerability = 0; e.attackReduction = 0; }
                    if (s.debuffsExpireRound <= s.round) { s.vulnerability = 0; s.weakness = 0; }
                }
                BeginRound();
            }
            Changed?.Invoke(); return true;
        }
        private bool FinishIfNeeded()
        {
            var s = State;
            if (s.hp <= 0) s.phase = PointBattlePhase.Defeat;
            else if (s.enemies.All(e => !e.Alive)) s.phase = PointBattlePhase.Victory;
            else return false;
            ApplyOutcome(); return true;
        }
        public void ApplyOutcome()
        {
            var s = State;
            if (s == null || s.outcomeApplied || (s.phase != PointBattlePhase.Victory && s.phase != PointBattlePhase.Defeat)) return;
            s.outcomeApplied = true;
            if (inventory == null) return;
            inventory.CompleteBattleAttempt(s.contextId, s.encounterId);
            if (s.phase == PointBattlePhase.Victory)
            {
                inventory.SetFlag("battle-won:" + s.contextId); inventory.SetFlag(s.victoryFlag);
                inventory.RecordBattleVictory(s.encounterId, s.balanceVersion, s.sessionId, s.round, s.swiftVictoryRounds);
            }
        }
        public PointBattleSnapshot Capture() => new PointBattleSnapshot { session = State == null ? null : JsonUtility.FromJson<PointBattleSession>(JsonUtility.ToJson(State)) };
        public bool Restore(PointBattleSnapshot snapshot)
        {
            if (!Validate(snapshot, false)) return false;
            State = snapshot?.session == null ? null : JsonUtility.FromJson<PointBattleSession>(JsonUtility.ToJson(snapshot.session));
            Changed?.Invoke(); return true;
        }
        public static bool Validate(PointBattleSnapshot snapshot, bool requireReturnPoint = true)
        {
            if (snapshot == null) return true;
            var s = snapshot.session;
            if (s == null) return snapshot.returnPoint == null;
            if (s.version != 1 || string.IsNullOrWhiteSpace(s.sessionId) || string.IsNullOrWhiteSpace(s.encounterId) ||
                string.IsNullOrWhiteSpace(s.contextId) || string.IsNullOrWhiteSpace(s.balanceVersion) || !Enum.IsDefined(typeof(PointBattlePhase), s.phase) ||
                !SixKinAttributes.IsValidBuild(s.attributes) || !MentalAnchors.ValidEquipped(s.anchor) || !PointCalculation.ValidFrozenItems(s.equipment) ||
                s.actionCost != 2 || s.roundAP < 0 || s.carryLimit < 0 || s.ap < 0 || s.carriedAP < 0 || s.carriedAP > s.carryLimit ||
                s.round < 1 || s.enemyCursor < 0 || s.enemies == null || s.enemies.Count == 0 || s.enemyCursor > s.enemies.Count ||
                s.actions == null || s.log == null || s.log.Count > 80 || !NonNegative(s.hp, s.maximumHP, s.shield, s.damageBonus, s.vulnerability, s.weakness) ||
                s.maximumHP <= 0 || s.hp > s.maximumHP || s.corrosion < 0 || (requireReturnPoint && !BattleReturnPoint.Validate(snapshot.returnPoint))) return false;
            if (s.equipment.Any(e => e.item.actionAddons.Any(a => !SupportedAddon(a.effectKey)))) return false;
            if (s.enemies.Any(e => e == null || e.profile == null || !e.profile.Valid() || !NonNegative(e.hp, e.shield, e.strength, e.vulnerability, e.attackReduction) ||
                e.hp > e.profile.health || e.corrosion < 0 || e.intentIndex < 0 || e.intentIndex >= e.profile.pattern.Count)) return false;
            var record = s.divination;
            if (record == null || record.rulesVersion != DivinationRecord.CurrentRulesVersion || !CoinCasting.IsValid(record.casting) ||
                record.revealedLines < 0 || record.revealedLines > 6 || !LiuYaoPaiPan.TryNormalizeCalendar(record.month, record.day, out string month, out string day, out _) ||
                record.month != month || record.day != day) return false;
            if (s.phase == PointBattlePhase.Casting) return record.chart == null && s.actions.Count == 0 && s.ap == 0 && !s.outcomeApplied;
            // Corrosion may finish a battle before the next roll. The previous resolved roll remains valid.
            if (s.actions.Count == 0 && (s.phase == PointBattlePhase.Victory || s.phase == PointBattlePhase.Defeat))
                return s.outcomeApplied && (s.phase == PointBattlePhase.Defeat ? s.hp == 0 : s.hp > 0 && s.enemies.All(e => !e.Alive));
            if (record.revealedLines != 6 || !LiuYaoPaiPan.IsValidResult(record.chart) || record.chart.yueling != month || record.chart.richen != day ||
                !record.chart.yao6789.SequenceEqual(record.casting.yaoValues) || s.actions.Count != 6) return false;
            try
            {
                for (int i = 0; i < 6; i++)
                {
                    var a = s.actions[i]; int raw = record.chart.behaviorModifiers[i]; bool deer = s.anchor == MentalAnchors.LuJianshen;
                    int effective = deer && raw > 0 ? checked(raw * 2) : raw;
                    if (a == null || a.uses < 0 || a.rawCheckPoints != raw || a.effectiveCheckPoints != effective || a.available != (!deer || raw > 0) ||
                        !SameCalculation(a.calculation, Calculate(s.attributes, (CheckBehavior)i, effective, s.equipment))) return false;
                }
            }
            catch (Exception) { return false; }
            return (s.phase != PointBattlePhase.Victory || s.hp > 0 && s.enemies.All(e => !e.Alive)) &&
                (s.phase != PointBattlePhase.Defeat || s.hp == 0) &&
                ((s.phase == PointBattlePhase.Victory || s.phase == PointBattlePhase.Defeat) == s.outcomeApplied);
        }
        private static bool NonNegative(params double[] values) => values.All(v => Finite(v) && v >= 0);
        private static bool SameCalculation(PointCalculationResult actual, PointCalculationResult expected)
        {
            if (actual?.action == null || !Finite(actual.action.amount) ||
                Math.Abs(actual.action.amount - expected.action.amount) > 1e-9 * Math.Max(1, Math.Abs(expected.action.amount))) return false;
            // Unity JSON can change the last bit of a double. Every integer intermediate and effect remains exact.
            var normalized = JsonUtility.FromJson<PointCalculationResult>(JsonUtility.ToJson(actual));
            normalized.action.amount = expected.action.amount;
            return JsonUtility.ToJson(normalized) == JsonUtility.ToJson(expected);
        }
        public static bool SupportedAddon(string key) => key == "damage" || key == "shield" || key == "heal" || key == "corrosion" ||
            key == "vulnerability" || key == "attackReduction" || key == "damageBonus";
    }
}

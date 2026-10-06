using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Emerge.Battle
{
    public sealed partial class BattleEngine
    {
        public bool IsEnhanced(string id)
        {
            var skill = Catalog.Skill(id); var s = State;
            return s != null && s.version >= 4 && skill != null && skill.enhancedAvailable && !skill.isUltimate && !skill.isPassive &&
                !skill.alwaysAvailable && s.unlockedSkills.Contains(id) && !s.enhancedSkills.Contains(id) &&
                BattleBuildRules.Points(s.attributes, skill.family) >= BattleSkillTableRules.EnhancementPointThreshold;
        }
        // A separate persisted stream: no UI/preview call advances it, and save/load cannot reroll a committed result.
        private float NextSkillRoll()
            => SkillRoll(state.seed, ++state.randomSerial);
        private static float SkillRoll(int seed, int serial)
        {
            uint n = unchecked((uint)seed + (uint)serial * 0x9E3779B9u);
            n ^= n >> 16; n *= 0x7FEB352Du; n ^= n >> 15; n *= 0x846CA68Bu; n ^= n >> 16;
            return (n >> 8) / 16777216f;
        }
        public static float Status(List<BattleTimedStatus> list, BattleStatusKind kind)
            => list == null ? 0 : list.Where(s => s.kind == kind).Select(s => s.power).DefaultIfEmpty(0).Max();
        private static bool IsNegativeStatus(BattleStatusKind kind)
            => kind == BattleStatusKind.IncomingUp || kind == BattleStatusKind.DamageDown || kind == BattleStatusKind.Burn ||
                kind == BattleStatusKind.ShadowCurse || kind == BattleStatusKind.ManaLock || kind == BattleStatusKind.DefenseDown || kind == BattleStatusKind.Blind;
        private static void SetStatus(List<BattleTimedStatus> list, BattleStatusKind kind, float power, int rounds, int count = 1)
        {
            var existing = list.Find(s => s.kind == kind);
            if (existing == null) list.Add(new BattleTimedStatus { kind = kind, power = power, rounds = rounds, count = count });
            else if (power >= existing.power) { existing.power = power; existing.rounds = Math.Max(existing.rounds, rounds); existing.count = Math.Max(existing.count, count); }
        }
        private float EffectiveReduction() => Math.Min(Catalog.rules.reductionCap, State.player.reduction + Status(State.player.statuses, BattleStatusKind.DamageReduction));
        private bool HasTalent(BattleSkillKind kind) => Catalog.skills.Any(s => s.isPassive && s.kind == kind);
        private void RestoreMana(int amount)
        {
            if (amount > 0 && Status(state.player.statuses, BattleStatusKind.ManaLock) <= 0)
                state.player.mp = Math.Min(Catalog.rules.maxMP, state.player.mp + amount);
        }
        private void GiveShield(float amount, int rounds)
        {
            int value = Math.Min(Catalog.rules.shieldCap, BattleRules.Round(amount));
            if (value < state.player.shield) return;
            state.player.shield = value;
            state.player.shieldRounds = Math.Max(state.player.shieldRounds, rounds);
        }
        private float DamageFactor(BattleAction a, bool direct = true)
            => a.multiplier * (1 - state.player.weakness) * (1 - Status(state.player.statuses, BattleStatusKind.DamageDown)) *
                (1 + Status(state.player.statuses, BattleStatusKind.DamageUp) + Status(state.player.statuses, BattleStatusKind.HeavenMomentum)) *
                (direct && a.critical ? BattleSkillTableRules.CritMultiplier : 1);
        private void Damage(BattleAction a, int index, float power, float ignoreDefense = 0)
        { if (state.player.hp > 0 && state.enemies[index].hp > 0) HitEnemy(index, power * DamageFactor(a), true, ignoreDefense); }
        private void Control(int index, float reduction)
        {
            var e = state.enemies[index];
            if (Catalog.Enemy(e.definitionId).resistsStun || e.determined) Weaken(index, reduction);
            else e.stunned = true;
        }
        private void Dispel(int index)
        {
            var e = state.enemies[index];
            if (e.shield > 0) { e.shield = 0; Log("驱散 · 敌方护盾"); }
            else if (e.charged)
            { e.charged = false; e.intentSkillId = Fallback(Catalog.Enemy(e.definitionId)).id; Log("驱散 · 敌方蓄势"); }
            else { var buff = e.statuses.Find(s => s.kind == BattleStatusKind.DamageUp || s.kind == BattleStatusKind.DamageReduction); if (buff != null) e.statuses.Remove(buff); }
        }
        private void Steal(int index, int layers)
        {
            var e = state.enemies[index];
            for (int i = 0; i < layers; i++)
            {
                if (e.shield > 0) { GiveShield(e.shield, 3); e.shield = 0; }
                else if (e.charged) { SetStatus(state.player.statuses, BattleStatusKind.DamageUp, .10f, 3); Dispel(index); }
                else { var buff = e.statuses.Find(s => s.kind == BattleStatusKind.DamageUp || s.kind == BattleStatusKind.DamageReduction); if (buff == null) break; SetStatus(state.player.statuses, buff.kind, buff.power, 3); e.statuses.Remove(buff); }
            }
        }
        private void ApplySkillTableAction(BattleAction a, BattleSkillDefinition skill)
        {
            var p = state.player; bool enhanced = a.enhanced; int t = a.targetIndex;
            bool direct = IsDirectSkill(skill);
            a.critical = direct && NextSkillRoll() < BattleSkillTableRules.CritChance;
            bool charge = direct && a.target == BattleTarget.Enemy && p.criticalCharge;
            float canonicalMultiplier = a.multiplier;
            if (charge) { a.multiplier *= 1.2f; p.criticalCharge = false; }
            switch (skill.kind)
            {
                case BattleSkillKind.Legacy:
                    if (skill.effect == BattleEffect.Damage)
                    { foreach (int i in Targets(a.target, t).ToArray()) { Damage(a, i, skill.power); if (a.appliesVulnerability && state.enemies[i].hp > 0) { state.enemies[i].vulnerability = Catalog.rules.vulnerability; state.enemies[i].vulnerabilityHits = Catalog.rules.vulnerabilityHits; } } }
                    else ApplySkill(a);
                    break;
                case BattleSkillKind.HeavenRadiance:
                    if (enhanced) SetStatus(p.statuses, BattleStatusKind.HeavenMomentum, .20f, 4, 5);
                    Damage(a, t, skill.power + (enhanced ? 12 : 0), enhanced ? .15f : 0);
                    SetStatus(p.statuses, BattleStatusKind.HeavenMomentum, enhanced ? .20f : .08f, 4, enhanced ? 5 : 2); break;
                case BattleSkillKind.MetalSever:
                    Damage(a, t, skill.power); SetStatus(state.enemies[t].statuses, BattleStatusKind.DefenseBreak, enhanced ? .25f : .12f, 3); if (enhanced) Dispel(t); break;
                case BattleSkillKind.StealHexagram:
                    Steal(t, enhanced ? 2 : 1); Damage(a, t, skill.power); if (enhanced) SetStatus(p.statuses, BattleStatusKind.IncomingUp, .10f, 3); break;
                case BattleSkillKind.PrisonBreak:
                    bool guarded = state.enemies[t].shield > 0 || state.enemies[t].statuses.Any(s => s.kind == BattleStatusKind.DamageReduction);
                    Damage(a, t, guarded ? skill.power + 10 : skill.power); if (guarded) SetStatus(state.enemies[t].statuses, BattleStatusKind.DefenseBreak, .10f, 3); break;
                case BattleSkillKind.HiddenStrike:
                    Damage(a, t, skill.power); SetStatus(state.enemies[t].statuses, BattleStatusKind.Blind, .5f, 1); break;
                case BattleSkillKind.ThunderMark:
                    Damage(a, t, skill.power); var thunder = state.enemies[t].statuses.Find(s => s.kind == BattleStatusKind.Thunder);
                    SetStatus(state.enemies[t].statuses, BattleStatusKind.Thunder, 13 * a.multiplier, 4, Math.Min(6, (thunder?.count ?? 0) + 1));
                    if (enhanced)
                        foreach (int i in Targets(BattleTarget.AllEnemies, 0).ToArray())
                        { var mark = state.enemies[i].statuses.Find(s => s.kind == BattleStatusKind.Thunder); if (mark == null) continue; Damage(a, i, 13 * mark.count); state.enemies[i].statuses.Remove(mark); if (state.enemies[i].hp > 0) Control(i, .20f); }
                    break;
                case BattleSkillKind.FlameForge:
                    Damage(a, t, skill.power);
                    foreach (int i in enhanced ? Targets(BattleTarget.AllEnemies, 0).ToArray() : new[] { t })
                        SetStatus(state.enemies[i].statuses, BattleStatusKind.Burn, (enhanced ? 8 : 5) * a.multiplier, 2);
                    if (enhanced) Dispel(t); break;
                case BattleSkillKind.MountainBarrier:
                    Damage(a, t, skill.power); SetStatus(state.enemies[t].statuses, BattleStatusKind.DamageDown, enhanced ? .50f : .30f, 2);
                    // One initial collision in a turn-based arena; no geometry-dependent repeated hits.
                    if (enhanced && state.enemies[t].hp > 0) Control(t, .20f); break;
                case BattleSkillKind.ShadowMark:
                    foreach (int i in enhanced ? Targets(BattleTarget.AllEnemies, 0).ToArray() : new[] { t })
                    { SetStatus(state.enemies[i].statuses, BattleStatusKind.ShadowCurse, 8 * a.multiplier, 5); if (enhanced) SetStatus(state.enemies[i].statuses, BattleStatusKind.IncomingUp, .12f, 5); }
                    break;
                case BattleSkillKind.SolarCurse:
                    SetStatus(state.enemies[t].statuses, BattleStatusKind.DamageDown, enhanced ? .30f : .15f, 3); break;
                case BattleSkillKind.GuNursery:
                    state.summons.Add(new BattleSummonState { kind = BattleSummonKind.GuNest, power = skill.power * a.multiplier * (enhanced ? 2 : 1), remainingRounds = 3, enhanced = enhanced }); break;
                case BattleSkillKind.TripleDoom:
                    Damage(a, t, skill.power + (state.enemies[t].hp > state.enemies[t].maxHP * .5f ? 12 : 0)); break;
                case BattleSkillKind.WindBlades:
                    foreach (int i in Targets(BattleTarget.AllEnemies, 0).ToArray())
                    { Damage(a, i, skill.power); if (enhanced && state.enemies[i].hp > 0) state.summons.Add(new BattleSummonState { kind = BattleSummonKind.WindBlade, targetIndex = i, power = 6 * a.multiplier, remainingRounds = 2 }); }
                    break;
                case BattleSkillKind.HeartLight:
                    bool marked = state.enemies[t].statuses.Any(s => s.kind == BattleStatusKind.Thunder || s.kind == BattleStatusKind.ShadowCurse);
                    Dispel(t); Damage(a, t, skill.power + (marked ? 8 : 0)); break;
                case BattleSkillKind.ReturningBreath:
                    p.hp = Math.Min(Catalog.rules.maxHP, p.hp + a.value); p.regeneration = BattleRules.Round(6 * a.multiplier); p.regenerationTicks = 2; break;
                case BattleSkillKind.SixLineFateGu:
                    a.auspicious = NextSkillRoll() < (enhanced ? BattleSkillTableRules.EnhancedFateGoodChance : BattleSkillTableRules.FateGoodChance);
                    Damage(a, t, a.auspicious ? 53 : 19);
                    if (!a.auspicious) { p.hp = Math.Max(0, p.hp - 28); Log("六爻断命蛊 · 凶，反噬 28 HP（无视护盾，不触发反伤）"); }
                    else Log("六爻断命蛊 · 吉"); break;
                case BattleSkillKind.ThunderFire:
                    bool burning = state.enemies[t].statuses.Any(s => s.kind == BattleStatusKind.Burn || s.kind == BattleStatusKind.Thunder);
                    Damage(a, t, skill.power + (burning ? 10 : 0)); break;
                case BattleSkillKind.EarthWard:
                    GiveShield((enhanced ? 75 : skill.power) * a.multiplier, 3);
                    p.regeneration = Math.Max(p.regeneration, BattleRules.Round(4 * a.multiplier)); p.regenerationTicks = Math.Max(p.regenerationTicks, 2);
                    if (enhanced) SetStatus(p.statuses, BattleStatusKind.ManaOnHit, 5, 3); break;
                case BattleSkillKind.WaterClone:
                    for (int i = 0; i < (enhanced ? 2 : 1); i++) state.summons.Add(new BattleSummonState { kind = BattleSummonKind.Clone, power = skill.power * a.multiplier, remainingRounds = 4 });
                    if (enhanced) SetStatus(p.statuses, BattleStatusKind.DamageReduction, .20f, 1); break;
                case BattleSkillKind.LunarBlessing:
                    SetStatus(p.statuses, BattleStatusKind.DamageUp, enhanced ? .22f : .10f, 8); if (enhanced) SetStatus(p.statuses, BattleStatusKind.KillMana, 8, 8); break;
                case BattleSkillKind.TripleChange:
                    state.domainRounds = 6; state.domainEnhanced = enhanced; break;
                case BattleSkillKind.MysticArmor:
                    GiveShield(skill.power * a.multiplier, 3); SetStatus(p.statuses, BattleStatusKind.Reflect, 8, 3); break;
                case BattleSkillKind.RevolvingGu:
                    Damage(a, t, skill.power + (enhanced ? 14 : 0));
                    if (enhanced) { int type = (int)(NextSkillRoll() * 3); if (type == 0) SetStatus(p.statuses, BattleStatusKind.HeavenMomentum, .12f, 2, 5); else if (type == 1) SetStatus(state.enemies[t].statuses, BattleStatusKind.Thunder, 13 * a.multiplier, 3); else SetStatus(state.enemies[t].statuses, BattleStatusKind.Burn, 5 * a.multiplier, 2); SetStatus(p.statuses, BattleStatusKind.ManaLock, 1, 2); } break;
                case BattleSkillKind.DrawCalamity:
                    Damage(a, t, skill.power); SetStatus(state.enemies[t].statuses, BattleStatusKind.Taunt, 1, 2); break;
                case BattleSkillKind.AllLinesChange:
                    foreach (int i in Targets(BattleTarget.AllEnemies, 0).ToArray()) { Damage(a, i, skill.power); SetStatus(state.enemies[i].statuses, BattleStatusKind.DefenseBreak, .20f, 4); }
                    SetStatus(p.statuses, BattleStatusKind.DamageUp, .25f, 4); SetStatus(p.statuses, BattleStatusKind.DamageReduction, .20f, 4); break;
                case BattleSkillKind.FateVerdict:
                    a.auspicious = NextSkillRoll() < .5f;
                    if (a.auspicious) a.critical = false;
                    if (a.auspicious) { GiveShield(50, 4); SetStatus(p.statuses, BattleStatusKind.DamageUp, .30f, 4); }
                    else { foreach (int i in Targets(BattleTarget.AllEnemies, 0).ToArray()) Damage(a, i, 45); p.hp = Math.Max(0, p.hp - 38); }
                    Log("爻定吉凶 · " + (a.auspicious ? "吉，护盾增伤" : "凶，群伤反噬")); break;
                case BattleSkillKind.HeavenBurial:
                    foreach (int i in Targets(BattleTarget.AllEnemies, 0).ToArray()) Damage(a, i, i == t ? skill.power : 22);
                    SetStatus(p.statuses, BattleStatusKind.DefenseDown, .15f, 2); break;
                case BattleSkillKind.YinYangSlash:
                    Damage(a, t, skill.power + (state.enemies[t].hp < state.enemies[t].maxHP * .30f ? 15 : 0)); break;
            }
            // Charge changes only this execution; the action's canonical family multiplier remains save-verifiable.
            a.multiplier = canonicalMultiplier;
            if (a.critical) { Log("暴击 · ×" + BattleSkillTableRules.CritMultiplier.ToString("0.0")); if (HasTalent(BattleSkillKind.CriticalTalent) && p.criticalTalentRound != state.round) { p.criticalCharge = true; p.criticalTalentRound = state.round; } }
            if (enhanced) Log(skill.displayName + " · 强化已用，后续恢复普通版");
        }
        private void TickSkillTableEnd()
        {
            foreach (var summon in state.summons.ToArray())
            {
                var living = Targets(BattleTarget.AllEnemies, 0).ToArray(); if (living.Length == 0 || state.player.hp <= 0) break;
                int packets = summon.kind == BattleSummonKind.GuNest && summon.enhanced ? 2 : 1;
                for (int j = 0; j < packets && state.player.hp > 0; j++)
                {
                    living = Targets(BattleTarget.AllEnemies, 0).ToArray(); if (living.Length == 0) break;
                    int target = summon.kind == BattleSummonKind.WindBlade ? summon.targetIndex : living[Math.Min(living.Length - 1, (int)(NextSkillRoll() * living.Length))];
                    if (target >= 0 && target < state.enemies.Count && state.enemies[target].hp > 0)
                    { HitEnemy(target, summon.power / packets * OngoingDamageFactor()); Log((summon.kind == BattleSummonKind.Clone ? "玄水分身" : summon.kind == BattleSummonKind.GuNest ? "蛊虫" : "盘旋风刃") + " · 命中 " + Catalog.Enemy(state.enemies[target].definitionId).displayName); }
                }
                if (--summon.remainingRounds == 0 && summon.kind == BattleSummonKind.GuNest && summon.enhanced) { state.player.shield = state.player.shieldRounds = 0; Log("强化养蛊领域结束 · 己方护盾清空"); }
            }
            state.summons.RemoveAll(s => s.remainingRounds <= 0 || (s.kind == BattleSummonKind.WindBlade && state.enemies[s.targetIndex].hp <= 0));
            for (int i = 0; i < state.enemies.Count && state.player.hp > 0; i++)
            {
                var e = state.enemies[i]; if (e.hp <= 0) continue;
                float dot = Status(e.statuses, BattleStatusKind.Burn) + Status(e.statuses, BattleStatusKind.ShadowCurse);
                if (dot > 0) { HitEnemy(i, dot * OngoingDamageFactor()); Log("灼烧/引晦 · " + BattleRules.Round(dot) + "，不暴击"); }
            }
            if (state.domainRounds > 0 && state.player.hp > 0 && state.enemies.Any(e => e.hp > 0))
            {
                int roll = Math.Min(5, (int)(NextSkillRoll() * 6));
                List<BattleTimedStatus> recipient = state.player.statuses;
                if (state.domainEnhanced && roll >= 3)
                { var living = Targets(BattleTarget.AllEnemies, 0).ToArray(); if (living.Length > 0) recipient = state.enemies[living[Math.Min(living.Length - 1, (int)(NextSkillRoll() * living.Length))]].statuses; }
                switch (roll)
                {
                    case 0: SetStatus(recipient, BattleStatusKind.DamageUp, .15f, 2); break;
                    case 1: SetStatus(recipient, BattleStatusKind.DamageReduction, .15f, 2); break;
                    case 2: state.player.hp = Math.Min(Catalog.rules.maxHP, state.player.hp + 10); break;
                    case 3: SetStatus(recipient, BattleStatusKind.DamageDown, .15f, 2); break;
                    case 4: SetStatus(recipient, BattleStatusKind.IncomingUp, .15f, 2); break;
                    case 5: SetStatus(recipient, BattleStatusKind.Burn, 4, 2); break;
                }
                Log("三爻同动 · " + (state.domainEnhanced && roll >= 3 ? "敌方" : "己方") + "领域事件 " + (roll + 1)); state.domainRounds--;
            }
            float selfBurn = Status(state.player.statuses, BattleStatusKind.Burn);
            if (selfBurn > 0) state.player.hp = Math.Max(0, state.player.hp - BattleRules.Round(selfBurn));
        }
        private void TickSkillTableStart()
        {
            if (state.player.shieldRounds > 0) state.player.shieldRounds--;
            foreach (var status in state.player.statuses)
                if (status.kind == BattleStatusKind.HeavenMomentum && status.count < 5) { status.count = Math.Min(5, status.count + 2); status.power = status.count * .04f; }
            foreach (var list in new[] { state.player.statuses }.Concat(state.enemies.Select(e => e.statuses)))
            { foreach (var s in list) s.rounds--; list.RemoveAll(s => s.rounds <= 0); }
        }
        private float OngoingDamageFactor() => (1 - state.player.weakness) * (1 - Status(state.player.statuses, BattleStatusKind.DamageDown)) *
            (1 + Status(state.player.statuses, BattleStatusKind.DamageUp) + Status(state.player.statuses, BattleStatusKind.HeavenMomentum));
        private static bool IsDirectSkill(BattleSkillDefinition skill) => skill.power > 0 && (skill.effect == BattleEffect.Damage || skill.kind == BattleSkillKind.MountainBarrier) && skill.kind != BattleSkillKind.WaterClone && skill.kind != BattleSkillKind.GuNursery;
        private static bool ValidSkillTableAction(BattleSession s, BattleSkillDefinition skill, BattleAction a)
        {
            bool direct = IsDirectSkill(skill);
            bool fortune = skill.kind == BattleSkillKind.SixLineFateGu || skill.kind == BattleSkillKind.FateVerdict;
            float chance = skill.kind == BattleSkillKind.SixLineFateGu && a.enhanced ? BattleSkillTableRules.EnhancedFateGoodChance : BattleSkillTableRules.FateGoodChance;
            if (a.auspicious != (fortune && SkillRoll(s.seed, a.randomSerialBefore + (direct ? 2 : 1)) < chance)) return false;
            if (a.critical != (direct && !(skill.kind == BattleSkillKind.FateVerdict && a.auspicious) && SkillRoll(s.seed, a.randomSerialBefore + 1) < BattleSkillTableRules.CritChance)) return false;
            return s.randomSerial >= a.randomSerialBefore + (direct ? 1 : 0) + (fortune ? 1 : 0);
        }
        private int ForecastTableRetaliation(string id, int index, bool item)
        {
            var skill = item ? null : Catalog.Skill(id); var consumable = item ? Catalog.Item(id) : null;
            if (item ? consumable == null || consumable.effect != BattleItemEffect.DamageAll : skill == null || !IsDirectSkill(skill)) return 0;
            var p = State.player; bool enhanced = !item && IsEnhanced(id);
            bool splash = !item && skill.kind == BattleSkillKind.HeavenBurial;
            var target = item || splash ? BattleTarget.AllEnemies : skill.target;
            float heaven = Status(p.statuses, BattleStatusKind.HeavenMomentum);
            if (!item && enhanced && skill.kind == BattleSkillKind.HeavenRadiance) heaven = Math.Max(heaven, .20f);
            float multiplier = item ? 1 : SkillMultiplier(id) * (1 - p.weakness) * (1 - Status(p.statuses, BattleStatusKind.DamageDown)) *
                (1 + Status(p.statuses, BattleStatusKind.DamageUp) + heaven) * (skill.target == BattleTarget.Enemy && p.criticalCharge ? 1.2f : 1);
            int total = 0, shield = p.shield;
            for (int i = 0; i < State.enemies.Count; i++)
            {
                var e = State.enemies[i]; if (e.hp <= 0 || (target != BattleTarget.AllEnemies && i != index)) continue;
                float power = item ? consumable.power : splash && i != index ? 22 : skill.power;
                float ignore = 0;
                if (!item)
                {
                    if (enhanced && skill.kind == BattleSkillKind.HeavenRadiance) { power += 12; ignore = .15f; }
                    if (skill.kind == BattleSkillKind.PrisonBreak && e.shield > 0) power += 10;
                    if (skill.kind == BattleSkillKind.TripleDoom && e.hp > e.maxHP * .5f) power += 12;
                    if (skill.kind == BattleSkillKind.ThunderFire && e.statuses.Any(s => s.kind == BattleStatusKind.Burn || s.kind == BattleStatusKind.Thunder)) power += 10;
                    if (skill.kind == BattleSkillKind.HeartLight && e.statuses.Any(s => s.kind == BattleStatusKind.Thunder || s.kind == BattleStatusKind.ShadowCurse)) power += 8;
                    if (skill.kind == BattleSkillKind.YinYangSlash && e.hp < e.maxHP * .3f) power += 15;
                    if (enhanced && skill.kind == BattleSkillKind.RevolvingGu) power += 14;
                    if (skill.kind == BattleSkillKind.StealHexagram && e.shield > 0) shield = Math.Max(shield, Math.Min(Catalog.rules.shieldCap, e.shield));
                }
                bool dispelsFirst = !item && (skill.kind == BattleSkillKind.HeartLight || skill.kind == BattleSkillKind.StealHexagram);
                int health = Math.Min(e.hp, Math.Max(0, EnemyDamage(e, power * multiplier, ignore) - (dispelsFirst ? 0 : e.shield)));
                int reflected = BattleRules.Round(health * Catalog.Enemy(e.definitionId).retaliation * (1 - e.weakness) * (1 - Status(e.statuses, BattleStatusKind.DamageDown)) * (1 - EffectiveReduction()) *
                    (1 + p.exposure + Status(p.statuses, BattleStatusKind.IncomingUp) + Status(p.statuses, BattleStatusKind.DefenseDown)));
                int absorbed = Math.Min(shield, reflected); shield -= absorbed; total += reflected - absorbed;
            }
            return Math.Min(total, p.hp);
        }
        private static bool ValidSkillTableState(BattleSession s, BattleCatalog catalog)
        {
            if (s.randomSerial < 0 || s.randomSerial > 1000000 || s.domainRounds < 0 || s.domainRounds > 6 || s.enhancedSkills == null ||
                s.enhancedSkills.Distinct().Count() != s.enhancedSkills.Count || s.enhancedSkills.Any(id => catalog.Skill(id) == null || !catalog.Skill(id).enhancedAvailable ||
                BattleBuildRules.Points(s.attributes, catalog.Skill(id).family) < BattleSkillTableRules.EnhancementPointThreshold || !s.skillUses.Any(u => u.skillId == id)) ||
                s.player.shieldRounds < 0 || s.player.shieldRounds > 8 || s.player.criticalTalentRound < 0 || s.player.criticalTalentRound > s.round ||
                s.summons == null || s.summons.Count > 100 || s.summons.Any(x => x == null || !Enum.IsDefined(typeof(BattleSummonKind), x.kind) || !Fraction(x.power, 10000) || x.remainingRounds < 1 || x.remainingRounds > 4 || x.targetIndex < -1 || x.targetIndex >= s.enemies.Count || (x.kind == BattleSummonKind.WindBlade && x.targetIndex < 0))) return false;
            return new[] { s.player.statuses }.Concat(s.enemies.Select(e => e.statuses)).All(list => list != null && list.Count <= 20 &&
                list.Select(x => x?.kind).Distinct().Count() == list.Count && list.All(x => x != null && Enum.IsDefined(typeof(BattleStatusKind), x.kind) && Fraction(x.power, 10000) && x.rounds >= 1 && x.rounds <= 8 && x.count >= 1 && x.count <= 6));
        }
    }
}

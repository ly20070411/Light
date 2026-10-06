using System;
using System.Collections.Generic;
using System.Linq;
using Emerge.Checks.Divination;
using UnityEngine;

namespace Emerge.Battle
{
    [CreateAssetMenu(menuName = "Light/战斗/配置库")]
    public sealed class BattleCatalog : ScriptableObject
    {
        public const string ResourcePath = "Battle/BattleCatalog";
        [Tooltip("此配置库在 Resources 下的稳定路径。旧资产保持默认路径；独立剧情库使用自己的资源路径。")]
        public string resourcePath = ResourcePath;
        public string SaveResourcePath => string.IsNullOrWhiteSpace(resourcePath) ? ResourcePath : resourcePath;
        public BattleRules rules;
        public BattleSkillDefinition[] skills = Array.Empty<BattleSkillDefinition>();
        public BattleEnemyDefinition[] enemies = Array.Empty<BattleEnemyDefinition>();
        public BattleItemDefinition[] items = Array.Empty<BattleItemDefinition>();
        public BattleEncounterDefinition[] encounters = Array.Empty<BattleEncounterDefinition>();
        public BattleSkillDefinition Skill(string id) => skills.FirstOrDefault(value => value != null && value.id == id);
        public BattleEnemyDefinition Enemy(string id) => enemies.FirstOrDefault(value => value != null && value.id == id);
        public BattleEncounterDefinition Encounter(string id) => encounters.FirstOrDefault(value => value != null && value.id == id);
        public BattleItemDefinition Item(string id) => items.FirstOrDefault(value => value != null && value.id == id);

        public static bool TryResolveSaveCatalog(string path, BattleCatalog current, out BattleCatalog resolved)
        {
            resolved = null;
            if (!ValidResourcePath(path) || (current != null && current.SaveResourcePath != path)) return false;
            var resource = Resources.Load<BattleCatalog>(path);
            if (resource == null || resource.SaveResourcePath != path) return false;
            // Runtime clones deliberately used by simulation/tests retain the same resource identity.
            // Check the supplied clone's complete battle data below instead of requiring object equality.
            resolved = current != null ? current : resource;
            return resolved.Validate(out _);
        }

        private static bool ValidResourcePath(string path)
            => !string.IsNullOrWhiteSpace(path) && path == path.Trim() && !path.Contains("\\") &&
                !path.Contains(":") && !path.Contains(".") && path.Split('/').All(segment => !string.IsNullOrWhiteSpace(segment));

        public bool Validate(out string error)
        {
            error = null;
            if (!ValidResourcePath(SaveResourcePath)) { error = "战斗库 Resources 路径无效。"; return false; }
            if (rules == null || !rules.Validate(out error)) { error = error ?? "缺少战斗规则。"; return false; }
            if (skills == null || skills.Length == 0 || enemies == null || enemies.Length == 0 ||
                items == null || encounters == null || encounters.Length == 0) { error = "战斗库缺少配置。"; return false; }
            var ids = new HashSet<string>();
            var kinds = new HashSet<BattleSkillKind>();
            foreach (var skill in skills)
                if (skill == null || string.IsNullOrWhiteSpace(skill.id) || !ids.Add(skill.id) ||
                    skill.maximumUses < 0 || (!skill.alwaysAvailable && !skill.isPassive && skill.maximumUses == 0) ||
                    (skill.isPassive ? skill.mpCost != 0 || skill.alwaysAvailable || skill.isUltimate || skill.enhancedAvailable : skill.mpCost < 1) ||
                    !Finite(skill.power) || skill.power < 0 || skill.power > 10000 ||
                    !Enum.IsDefined(typeof(BattleEffect), skill.effect) || !Enum.IsDefined(typeof(BattleTarget), skill.target) ||
                    !Enum.IsDefined(typeof(BattleFamily), skill.family) || !Enum.IsDefined(typeof(BattleSkillKind), skill.kind) || skill.regenerationTicks < 1 ||
                    (skill.kind == BattleSkillKind.Legacy ? !ValidLegacySkill(skill) :
                        !kinds.Add(skill.kind) || skill.isPassive != (skill.kind == BattleSkillKind.RecoilTalent || skill.kind == BattleSkillKind.CriticalTalent) ||
                        skill.isUltimate != ((int)skill.kind >= 25 && (int)skill.kind <= 28)))
                { error = "玩家技能 ID、数值或类型无效。"; return false; }
            ids.Clear();
            foreach (var enemy in enemies)
            {
                if (enemy == null || string.IsNullOrWhiteSpace(enemy.id) || !ids.Add(enemy.id) || enemy.maxHP < 1 ||
                    enemy.maxMP < 1 || enemy.roundMana < 0 || !Finite(enemy.retaliation) || enemy.retaliation < 0 || enemy.retaliation > 1 ||
                    !Finite(enemy.defense) || enemy.defense < 0 || enemy.defense > .8f || enemy.skills == null || enemy.skills.Length == 0)
                { error = "敌人配置无效。"; return false; }
                var skillIds = new HashSet<string>(); bool fallback = false;
                foreach (var skill in enemy.skills)
                {
                    if (skill == null || string.IsNullOrWhiteSpace(skill.id) || !skillIds.Add(skill.id) || skill.mpCost < 0 ||
                        skill.power < 0 || skill.power > 10000 || skill.weight < 1 || skill.weight > 100000 || skill.maximumShield < 1 || !Finite(skill.weakness) || skill.weakness < 0 || skill.weakness > 1 ||
                        !Finite(skill.maximumHealthFraction) || skill.maximumHealthFraction <= 0 ||
                        skill.maximumHealthFraction > 1 || skill.maximumUses < 0 || skill.cooldownRounds < 0 || !Enum.IsDefined(typeof(EnemyEffect), skill.effect))
                    { error = "敌方技能配置无效。"; return false; }
                    fallback |= skill.effect == EnemyEffect.Damage && skill.mpCost == 0 &&
                        skill.maximumHealthFraction == 1 && skill.maximumUses == 0 && skill.cooldownRounds == 0;
                }
                if (!fallback) { error = "敌人必须配置无条件、0 MP 的普攻。"; return false; }
                if (enemy.skills.Any(s => s.effect == EnemyEffect.Charge) != enemy.skills.Any(s => s.effect == EnemyEffect.ChargedDamage) ||
                    enemy.skills.Any(s => s.effect == EnemyEffect.ChargedDamage && (s.mpCost > enemy.maxMP || s.maximumHealthFraction != 1 || s.maximumUses != 0 || s.cooldownRounds != 0)))
                { error = "蓄势与释放必须成对配置，释放需要始终可执行。"; return false; }
            }
            ids.Clear(); var keys = new HashSet<string>();
            foreach (var item in items)
                if (item == null || string.IsNullOrWhiteSpace(item.id) || !ids.Add(item.id) ||
                    string.IsNullOrWhiteSpace(item.inventoryKey) || !keys.Add(item.inventoryKey) || item.power < 1 ||
                    !Enum.IsDefined(typeof(BattleItemEffect), item.effect))
                { error = "战斗物品 ID 或背包键无效。"; return false; }
            ids.Clear();
            foreach (var encounter in encounters)
            {
                if (encounter == null || string.IsNullOrWhiteSpace(encounter.id) || !ids.Add(encounter.id) ||
                    encounter.swiftVictoryRounds < 1 || encounter.enemies == null || encounter.enemies.Length < 1 || encounter.enemies.Length > 3 ||
                    encounter.enemies.Any(slot => slot == null || slot.enemy == null || Enemy(slot.enemy.id) != slot.enemy || slot.healthOverride < 0) ||
                    !LiuYaoPaiPan.TryNormalizeCalendar(encounter.month, encounter.day, out _, out _, out _))
                { error = "遭遇需要 1～3 个有效敌人及有效月令、日辰。"; return false; }
                if (encounter.enemies.Sum(slot => slot.enemy.skills.First(s => s.effect == EnemyEffect.Damage && s.mpCost == 0 && s.maximumHealthFraction == 1 && s.maximumUses == 0 && s.cooldownRounds == 0).power) > rules.enemyIntentBudget)
                { error = "敌方总伤害预算必须覆盖队伍的基础攻击。"; return false; }
            }
            return true;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool ValidLegacySkill(BattleSkillDefinition skill)
        {
            return !skill.isPassive && !skill.isUltimate &&
                !((skill.effect != BattleEffect.Damage && skill.effect != BattleEffect.Silence && skill.effect != BattleEffect.Bind && skill.target != BattleTarget.Self) ||
                  ((skill.effect == BattleEffect.Silence || skill.effect == BattleEffect.Bind) && skill.target != BattleTarget.Enemy) ||
                  (skill.effect == BattleEffect.Damage && skill.target == BattleTarget.Self) ||
                  (skill.effect == BattleEffect.Reduction && skill.power > 1));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Emerge.Battle;
using Emerge.Characters;
using Emerge.Checks;
using Emerge.Props;
using Emerge.Props.Editor;
using UnityEditor;
using UnityEngine;

namespace Emerge.Day1.Editor
{
    /// <summary>
    /// Creates missing Day1 content only. Existing assets remain editable and are never regenerated.
    /// P01/P02 are automatic in Day1FlowController; P20 reuses Rules. Thus 20 logical points
    /// use 17 independent definitions, without changing any CharacterDefinition.mapProp.
    /// </summary>
    public static class Day1ContentBuilder
    {
        public const string ConfigPath = "Assets/Day1/Config";
        public const string LibraryPath = ConfigPath + "/Day1PropLibrary.asset";
        public const string BattleCatalogResourcePath = "Day1/Day1BattleCatalog";
        public const string BattleCatalogPath = "Assets/Resources/Day1/Day1BattleCatalog.asset";
        private const string LegacyBattleCatalogPath = ConfigPath + "/Day1BattleCatalog.asset";
        public const string FreeRoamFlag = "day1.free_roam";

        public static readonly string[] Keys =
        {
            "Rules", "Environment", "Supplies", "LinXi", "Diagnosis", "Hydrologist",
            "Geologist", "Plant", "YangYinglong", "Threat", "SeaGate", "ContainmentResearcher",
            "Repair", "Mechanic", "Tools", "Parts", "Power"
        };

        [MenuItem("Tools/剧情/Day1/补齐剧情配置资产")]
        public static void BuildDefault()
        {
            Build(CharacterCatalog.LoadDefault());
            Debug.Log("DAY1_CONTENT_OK: " + LibraryPath + "；仅补齐缺失资产，已有配置保持原样。");
        }

        public static PropDefinition Load(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || Array.IndexOf(Keys, key) < 0) return null;
            return AssetDatabase.LoadAssetAtPath<PropDefinition>(ConfigPath + "/" + key + ".asset");
        }

        public static BattleCatalog LoadBattleCatalog()
            => AssetDatabase.LoadAssetAtPath<BattleCatalog>(BattleCatalogPath);

        public static PropLibrary Build(CharacterCatalog characters)
        {
            if (characters == null) throw new InvalidOperationException("Day1 缺少角色库。请先完成角色资产创建。");
            foreach (string id in new[] { CharacterIds.LinXi, CharacterIds.Hydrologist, CharacterIds.Geologist,
                CharacterIds.YangYinglong, CharacterIds.ContainmentResearcher, CharacterIds.Mechanic })
                if (characters.Find(id) == null) throw new InvalidOperationException("Day1 缺少角色：" + id);

            PropAssetFactory.EnsureFolder(ConfigPath);
            var library = AssetDatabase.LoadAssetAtPath<PropLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<PropLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }

            var diagnosis = EnsureCheck("DiagnosisCheck", "day1.diagnosis", "总控室设备检查", "诊断终端",
                "读取终端状态，再整理各路信息。", "整理终端记录", CheckBehavior.Parent, 7,
                "day1.quest.lin-xi", "day1.diagnosis_done", "day1.diagnosis_success",
                "你从交错的数据中整理出了可用于后续检查的记录。", "你取得了基础检查结果，但未能整理出额外可用的记录。",
                "检查结果已保存，可以回报林溪。", "day1.calibration-note", "校验记录");
            var plant = EnsureCheck("PlantCheck", "day1.plant", "植物样本采集", "植物采样",
                "保留根部和附着土壤，尝试完整分离样本。", "分离并保存植物", CheckBehavior.Offspring, 9,
                "day1.quest.geologist", "day1.plant_collected", "day1.plant_success",
                "样本保存完整，根部与附着土壤也一并留存。", "你取得了可用样本，但在分离时损伤了部分结构。",
                "基础样本已取得，可以交给地质学家。", "day1.good-plant", "完整植物样本");
            var repair = EnsureCheck("RepairCheck", "day1.repair", "作战设备检修", "维修工作台",
                "检查故障位置，再修复异常部件。", "检查并修复异常部件", CheckBehavior.Offspring, 9,
                "day1.quest.containment-researcher", "day1.repair_done", "day1.repair_success",
                "异常部件已恢复，设备通过本次检修。", "你记录下故障位置。研究员协助完成了基础修复，设备恢复到可用状态。",
                "检修结果已保存，可以回报收容部安保。", null, null);
            var parts = EnsureCheck("PartsCheck", "day1.parts", "码头零件搜寻", "零件箱",
                "核对现有零件，找出可供检修的替换件。", "辨认并整理零件", CheckBehavior.Wealth, 10,
                "day1.quest.mechanic", "day1.parts_collected", "day1.parts_success",
                "你找到了一件状况良好的备用零件。", "你找到了可用于检修的零件，但没有找到额外的完好备件。",
                "所需零件已找到，工具齐备后可以交给机械师。", "day1.spare-part", "备用零件");

            BattleEncounterDefinition encounter = EnsureThreatEncounter();
            EnsureBattleCatalog(encounter);

            EnsureProp(library, "Rules", "入驻规则终端", "终端", "Table", p =>
            {
                p.interactionLabel = "读取 / 确认今日作业";
                p.description = "P03 入驻规则宣读；P20 当日收尾复用此处。具体阶段和确认由 Day1FlowController 处理。";
                // No initial required flag: the controller owns admission/rules/closing phases.
            });
            EnsureProp(library, "Environment", "环境监测台", "终端", "Table", p =>
            {
                p.interactionLabel = "读取环境记录";
                p.tint = new Color(.72f, .88f, 1f);
                p.description = "P04 保存环境数据档案并收到物资通知。【占位】海况数值、科考次数、堰段编号尚未确定。";
            });
            EnsureProp(library, "Supplies", "生活物资", "物资", "Crate", p =>
            {
                p.interactionLabel = "领取物资";
                p.description = "P05 领取后触发一次影子演出。【占位】具体物资内容尚未确定；道具不自行发放奖励。";
            });
            EnsureCharacter(library, "LinXi", characters.Find(CharacterIds.LinXi));
            EnsureCharacter(library, "Hydrologist", characters.Find(CharacterIds.Hydrologist));
            EnsureCharacter(library, "Geologist", characters.Find(CharacterIds.Geologist));
            EnsureCharacter(library, "YangYinglong", characters.Find(CharacterIds.YangYinglong));
            EnsureCharacter(library, "ContainmentResearcher", characters.Find(CharacterIds.ContainmentResearcher));
            EnsureCharacter(library, "Mechanic", characters.Find(CharacterIds.Mechanic));

            EnsureProp(library, "Diagnosis", "设备诊断终端", "检定", "Table", p =>
            {
                ConfigureTaskCheck(p, diagnosis, "day1.quest.lin-xi", "请先向林溪接取检查任务");
                p.interactionLabel = "检查终端状态";
                p.tint = new Color(.64f, .83f, 1f);
                p.description = "P07 实际六爻信息整理检定。【占位】父母目标 7，使用现有演示月日；成功奖励校验记录，失败仍保存基础检查结果。";
            });
            EnsureProp(library, "Plant", "植物采样点", "检定", "Torch", p =>
            {
                ConfigureTaskCheck(p, plant, "day1.quest.geologist", "请先向地质学家接取植物采样任务");
                p.interactionLabel = "采集植物样本";
                p.worldSize = new Vector2(.85f, 1.25f);
                p.tint = new Color(.3f, .8f, .36f);
                p.isSolid = false;
                p.description = "P10 实际六爻采集检定。【占位】默认火炬贴图着绿代替植物；子孙目标 9，样本数量 1；成功奖励完整样本，失败也可完成基础采样。";
            });
            EnsureProp(library, "Threat", "周边威胁", "战斗", "Shadow", p =>
            {
                p.battleEncounter = encounter;
                p.sprite = encounter.enemies[0].enemy.battlePortrait;
                p.requiredFlags = new[] { FreeRoamFlag, "day1.quest.yang-yinglong" };
                p.lockedHint = "请先向杨应隆确认周边威胁";
                p.interactionLabel = "开始清除行动";
                p.worldSize = new Vector2(1.35f, 1.2f);
                p.tint = new Color(.8f, .4f, .45f);
                p.isSolid = false;
                p.description = "P12 六爻战斗教学。【占位】敌种复用 E01 外观并降低数值，非 Boss；胜利标记 day1.threat_cleared。";
            });
            var threatProp = Load("Threat");
            // Narrow migration of the old invisible automatic placeholder only; custom art stays intact.
            if (threatProp != null && threatProp.sprite == Art("Shadow") && encounter.enemies.Length > 0 &&
                encounter.enemies[0].enemy != null && encounter.enemies[0].enemy.battlePortrait != null)
            {
                threatProp.sprite = encounter.enemies[0].enemy.battlePortrait;
                EditorUtility.SetDirty(threatProp);
            }
            EnsureProp(library, "SeaGate", "蚀海侧作业入口", "边界", "Crate", p =>
            {
                p.requiredFlags = new[] { FreeRoamFlag };
                p.interactionLabel = "查看蚀海侧通路";
                p.worldSize = new Vector2(1.5f, 1.5f);
                p.tint = new Color(.7f, .65f, .45f);
                p.description = "P13 杨应隆劝阻事件，不依赖水样任务；第一日始终不开放，不因战斗胜利解除。";
            });
            EnsureProp(library, "Repair", "装备维修工作台", "检定", "Table", p =>
            {
                ConfigureTaskCheck(p, repair, "day1.quest.containment-researcher", "请先向收容部安保接取维修任务");
                p.interactionLabel = "检修作战设备";
                p.tint = new Color(.93f, .68f, .45f);
                p.description = "P15 实际六爻维修检定。【占位】子孙目标 9；失败由研究员协助完成基础修复，不锁死教学；无额外奖励。";
            });
            EnsureProp(library, "Tools", "码头工具箱", "任务物资", "Crate", p =>
            {
                p.requiredFlags = new[] { FreeRoamFlag, "day1.quest.mechanic" };
                p.lockedHint = "请先向机械师确认所需工具与零件";
                p.interactionLabel = "搜寻工具";
                p.tint = new Color(.76f, .86f, .96f);
                p.description = "P17 所需工具由 Day1FlowController 发放并防止重复领取。【占位】工具种类、数量尚未确定。";
            });
            EnsureProp(library, "Parts", "码头零件箱", "检定", "Crate", p =>
            {
                ConfigureTaskCheck(p, parts, "day1.quest.mechanic", "请先向机械师接取工具与零件任务");
                p.interactionLabel = "搜寻替换零件";
                p.tint = new Color(.6f, .76f, .93f);
                p.description = "P18 实际六爻搜索检定。【占位】妻财目标 10；成功额外奖励备用零件，失败仍取得必需零件；奖励与任务交付分开。";
            });
            EnsureProp(library, "Power", "码头供电箱", "状态", "Table", p =>
            {
                p.requiredFlags = new[] { FreeRoamFlag };
                p.interactionLabel = "查看供电状态";
                p.worldSize = new Vector2(1.4f, 1.3f);
                p.tint = new Color(.55f, .71f, .82f);
                p.description = "P19 可选查看机械师任务前后的供电状态；不增加第二个必做维修任务。";
            });

            AssetDatabase.SaveAssets();
            return library;
        }

        private static void EnsureCharacter(PropLibrary library, string key, CharacterDefinition character)
        {
            EnsureProp(library, key, character.DisplayName, "角色", null, p =>
            {
                p.character = character;
                p.sprite = character.mapSprite;
                p.interactionLabel = "交谈";
                p.requiredFlags = new[] { FreeRoamFlag };
                p.lockedHint = "请先完成科考站入驻准备";
                p.description = "Day1 独立角色交互配置；引用角色库外观与资料，原始角色地图道具保持不变。剧情对白和任务状态由 Day1FlowController 管理。";
            }, character.mapProp);
        }

        private static void EnsureProp(PropLibrary library, string key, string title, string category,
            string artName, Action<PropDefinition> configure, PropDefinition source = null)
        {
            string path = ConfigPath + "/" + key + ".asset";
            var prop = AssetDatabase.LoadAssetAtPath<PropDefinition>(path);
            if (prop == null)
            {
                prop = source != null ? UnityEngine.Object.Instantiate(source) : ScriptableObject.CreateInstance<PropDefinition>();
                prop.RenewIdentity();
                prop.displayName = title;
                prop.category = "Day1 / " + category;
                prop.actions = PropActions.Custom;
                prop.dialogue = new List<PropDialogueLine>();
                prop.itemHandover = new PropItemHandover();
                prop.checkEvent = null;
                prop.battleEncounter = null;
                prop.grantedFlags = Array.Empty<string>();
                prop.requiredFlags = Array.Empty<string>();
                prop.requiredItemKey = null;
                prop.consumeRequiredItem = false;
                prop.singleUse = false;
                prop.cooldown = .2f;
                prop.interactionRange = 1.8f;
                prop.hideAfterPickup = false;
                if (source == null)
                {
                    prop.sprite = Art(artName);
                    prop.worldSize = artName == "Table" ? new Vector2(1.8f, 1.8f) : new Vector2(1f, 1.25f);
                    prop.isSolid = true;
                    prop.colliderSize = new Vector2(.75f, .35f);
                    prop.colliderOffset = new Vector2(0, .12f);
                    prop.physicsMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>("Assets/Art/NoFriction.physicsMaterial2D");
                }
                configure(prop);
                AssetDatabase.CreateAsset(prop, path);
            }
            if (library.Add(prop)) EditorUtility.SetDirty(library);
        }

        private static void ConfigureTaskCheck(PropDefinition prop, CheckEventDefinition check, string quest, string hint)
        {
            prop.checkEvent = check;
            prop.requiredFlags = new[] { FreeRoamFlag, quest };
            prop.lockedHint = hint;
        }

        // Design placeholders: current demo calendar; one action per tutorial; thresholds use
        // the default actor's Parent 6, Offspring 9, Wealth 10 plus the genuine divination modifier.
        // Failure always records the base deliverable, while only success adds an extra reward.
        private static CheckEventDefinition EnsureCheck(string file, string id, string title, string speaker,
            string intro, string action, CheckBehavior behavior, int target, string quest, string doneFlag,
            string successFlag, string successText, string failureText, string continuation,
            string rewardKey, string rewardName)
        {
            string path = ConfigPath + "/" + file + ".asset";
            var check = AssetDatabase.LoadAssetAtPath<CheckEventDefinition>(path);
            if (check != null) return check;
            check = ScriptableObject.CreateInstance<CheckEventDefinition>();
            check.eventId = id;
            check.title = title;
            check.speaker = speaker;
            check.intro = intro;
            check.completedText = continuation;
            check.useDivination = true;
            check.divinationMonth = "巳月";
            check.divinationDay = "戊子日";
            check.revealDifficultyBeforeChoice = true;
            check.options = new List<CheckOptionDefinition>
            {
                new CheckOptionDefinition
                {
                    id = "perform", label = action, behavior = behavior, targetValue = target,
                    requiredFlags = new[] { FreeRoamFlag, quest },
                    success = new CheckOutcomeDefinition
                    {
                        text = successText, continuation = continuation,
                        grantedFlags = new[] { doneFlag, successFlag },
                        rewardItemKey = rewardKey, rewardItemName = rewardName,
                        rewardItemAmount = string.IsNullOrWhiteSpace(rewardKey) ? 0 : 1
                    },
                    failure = new CheckOutcomeDefinition
                    {
                        text = failureText, continuation = continuation, grantedFlags = new[] { doneFlag }
                    }
                }
            };
            check.ValidateOrThrow();
            AssetDatabase.CreateAsset(check, path);
            return check;
        }

        private static BattleEncounterDefinition EnsureThreatEncounter()
        {
            const string enemyPath = ConfigPath + "/Day1ThreatEnemy.asset";
            var enemy = AssetDatabase.LoadAssetAtPath<BattleEnemyDefinition>(enemyPath);
            if (enemy == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<BattleEnemyDefinition>("Assets/Battle/Config/E01.asset");
                if (source == null) throw new InvalidOperationException("Day1 教学战斗缺少现有 E01 配置。");
                enemy = UnityEngine.Object.Instantiate(source);
                enemy.id = "DAY1_E01";
                enemy.displayName = "异常影（占位）";
                // Placeholder enemy species; 35 HP and 4/7 damage keep a normal attack-only path viable.
                enemy.maxHP = 35; enemy.maxMP = 6; enemy.roundMana = 2; enemy.resistsStun = false;
                enemy.skills = new[]
                {
                    new EnemySkillDefinition { id = "claw", displayName = "抓击", effect = EnemyEffect.Damage,
                        mpCost = 0, power = 4, weight = 75 },
                    new EnemySkillDefinition { id = "heavy", displayName = "重击", effect = EnemyEffect.Damage,
                        mpCost = 6, power = 7, weight = 25, cooldownRounds = 1 }
                };
                AssetDatabase.CreateAsset(enemy, enemyPath);
            }
            string path = ConfigPath + "/Day1ThreatEncounter.asset";
            var encounter = AssetDatabase.LoadAssetAtPath<BattleEncounterDefinition>(path);
            if (encounter != null) return encounter;
            encounter = ScriptableObject.CreateInstance<BattleEncounterDefinition>();
            encounter.id = "DAY1_THREAT";
            encounter.displayName = "科考站外围清除行动";
            encounter.description = "【占位】Day1 首场六爻战斗教学；敌种复用现有 E01，占位低难数值，非逆模因 Boss。月令与日辰沿用演示设置。";
            encounter.month = "巳月"; encounter.day = "戊子日";
            encounter.enemies = new[] { new BattleEnemySlot { enemy = enemy } };
            encounter.repeatable = false;
            encounter.victoryFlag = "day1.threat_cleared";
            encounter.swiftVictoryRounds = 5;
            AssetDatabase.CreateAsset(encounter, path);
            return encounter;
        }

        private static BattleCatalog EnsureBattleCatalog(BattleEncounterDefinition encounter)
        {
            PropAssetFactory.EnsureFolder("Assets/Resources/Day1");
            if (LoadBattleCatalog() == null && AssetDatabase.LoadAssetAtPath<BattleCatalog>(LegacyBattleCatalogPath) != null)
            {
                // MoveAsset moves the .meta as well, preserving GUID references in the existing scene.
                string moveError = AssetDatabase.MoveAsset(LegacyBattleCatalogPath, BattleCatalogPath);
                if (!string.IsNullOrEmpty(moveError)) throw new InvalidOperationException("Day1 战斗库迁移失败：" + moveError);
            }
            var catalog = LoadBattleCatalog();
            if (catalog != null)
            {
                // Resource identity is migration metadata; preserve all authored gameplay configuration.
                if (catalog.resourcePath != BattleCatalogResourcePath)
                { catalog.resourcePath = BattleCatalogResourcePath; EditorUtility.SetDirty(catalog); }
                return catalog;
            }
            var source = AssetDatabase.LoadAssetAtPath<BattleCatalog>("Assets/Resources/Battle/BattleCatalog.asset");
            if (source == null) throw new InvalidOperationException("Day1 缺少现有战斗库。");
            catalog = UnityEngine.Object.Instantiate(source);
            catalog.resourcePath = BattleCatalogResourcePath;
            catalog.enemies = (source.enemies ?? Array.Empty<BattleEnemyDefinition>()).Concat(encounter.enemies.Select(s => s.enemy)).Distinct().ToArray();
            catalog.encounters = (source.encounters ?? Array.Empty<BattleEncounterDefinition>()).Concat(new[] { encounter }).ToArray();
            if (!catalog.Validate(out string error))
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                throw new InvalidOperationException("Day1 战斗库配置无效：" + error);
            }
            AssetDatabase.CreateAsset(catalog, BattleCatalogPath);
            return catalog;
        }

        private static Sprite Art(string name)
            => string.IsNullOrEmpty(name) ? null : AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Generated/" + name + ".png");
    }
}

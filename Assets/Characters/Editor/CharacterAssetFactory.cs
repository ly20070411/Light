using System;
using Emerge.Props;
using Emerge.Props.Editor;
using UnityEditor;
using UnityEngine;

namespace Emerge.Characters.Editor
{
    public static class CharacterAssetFactory
    {
        public const string CatalogPath = "Assets/Resources/Characters/CharacterCatalog.asset";
        public const string DefinitionsPath = "Assets/Characters/Definitions";
        public const string PropsPath = "Assets/Characters/MapProps";
        public const string PrefabsPath = "Assets/Characters/Prefabs";

        [MenuItem("Tools/角色系统/补齐初始角色资源")]
        public static void Install()
        {
            PropAssetFactory.EnsureFolder("Assets/Resources/Characters");
            PropAssetFactory.EnsureFolder(DefinitionsPath);
            PropAssetFactory.EnsureFolder(PropsPath);
            PropAssetFactory.EnsureFolder(PrefabsPath);
            var catalog = AssetDatabase.LoadAssetAtPath<CharacterCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<CharacterCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            var props = PropAssetFactory.EnsureDefaultLibrary();
            Seed(catalog, props, CharacterIds.HuanYujian, "HuanYujian", c =>
            {
                c.characterName = "桓玉鉴"; c.roleLabel = "队长"; c.gender = CharacterGender.Male;
                c.narrativeRole = CharacterRole.Player; c.faction = "罗天教"; c.department = "悬鉴司";
                c.title = "总管"; c.function = "队长"; c.hexagram = "恒"; c.rank = CharacterRank.HexagramLord;
                c.abilityName = "锚定";
                c.abilityDescription = "恒象征持续不动、永恒，无固定类象，表现持恒的概念。创建物质或概念上的锚点，维持事物的本质；具有记住被抹消之人的思维范式。";
                c.background = "悬鉴司负责认知与模因相关工作，包括隐藏罗天教的存在、修改记忆、对抗信息类劫灾。桓玉鉴见过许多战友牺牲后被从认知与存在上抹消，最后只有他记得他们，因此养成记住每个相识之人名字的习惯。\n“他们慷慨拥抱虚无的时候，眼神中的含义不就是‘至少你会记住我’么？”";
                c.appearanceNotes = "遵循罗天教、悬鉴司的势力服饰；恒卦无固定类象。当前占位使用深色长衣与金色饰边，区别于普通 NPC。";
                c.dayOneLocation = "认知缓冲间 → 总控室 → 生活区 → 自由探索";
                c.dayOneActivity = "接受入站认知测试、宣读站内注意事项、查阅环境信息、领取生活物资、认识队员。";
                c.dayOneTask = "取得身份认证卡与环境数据档案，完成第一日流程。";
                c.planningNotes = "人物表职称为总管，故事背景称悬鉴司掌鉴，正式称谓待统一。角色预制体只提供静态占位，不替换现有玩家控制器。";
            });
            Seed(catalog, props, CharacterIds.LinXi, "LinXi", c =>
            {
                c.characterName = "林溪"; c.roleLabel = "副队长"; c.gender = CharacterGender.Female;
                c.narrativeRole = CharacterRole.Supporting; c.faction = "太衍道"; c.function = "副队长，随队卦者";
                c.hexagram = "比"; c.rank = CharacterRank.HexagramBearer; c.abilityName = "整流";
                c.abilityDescription = "比卦象征水行地上、亲密无间，权能与连接有关。整流能够串联高度复杂的信息流。";
                c.background = "太衍道对应道教。林溪是此次勘察队的副队长与随队卦者。";
                c.appearanceNotes = "以道袍为基础，按科考环境改造；作为次要人物与普通 NPC 区分。当前占位为青色道袍与连接状饰带。";
                c.dayOneLocation = "机房"; c.dayOneActivity = "检查站内设备情况。";
                c.dayOneTask = "检查总控室状况；采集任务，包含检定，可获得增益道具。";
            });
            Seed(catalog, props, CharacterIds.Hydrologist, "Hydrologist", c =>
            {
                c.roleLabel = "水文学家"; c.gender = CharacterGender.Male; c.narrativeRole = CharacterRole.Npc;
                c.faction = "蓬莱馆"; c.department = "青龙馆"; c.title = "教授"; c.function = "科研人员，水文学家";
                c.hexagram = "屯"; c.rank = CharacterRank.HeavenlyEye; c.abilityName = "生命操纵";
                c.abilityDescription = "屯卦具有生命操纵的权能，含困难与朝气蓬勃之意，象征草木破土而出。";
                c.background = "青龙馆为蓬莱馆下属研究生物、生态与医学的学院。";
                c.appearanceNotes = "蓬莱馆青龙馆科研人员的基础服饰；当前占位为白色实验服、青色内衬与取样瓶。";
                c.dayOneLocation = "科研区／化学分析室"; c.dayOneActivity = "分析水质。";
                c.dayOneTask = "采集蚀海海水样本；第一日前往蚀海侧会被杨应隆劝阻，任务顺延至第二日。";
                c.planningNotes = "姓名待定，先以职能名称显示；勿自行补写个人经历。";
            });
            Seed(catalog, props, CharacterIds.Geologist, "Geologist", c =>
            {
                c.roleLabel = "地质学家"; c.gender = CharacterGender.Male; c.narrativeRole = CharacterRole.Npc;
                c.faction = "蓬莱馆"; c.department = "青龙馆"; c.title = "教授"; c.function = "科研人员，地质学家";
                c.hexagram = "屯"; c.rank = CharacterRank.HeavenlyEye; c.abilityName = "生命操纵";
                c.abilityDescription = "屯卦具有生命操纵的权能，含困难与朝气蓬勃之意，象征草木破土而出。";
                c.background = "青龙馆为蓬莱馆下属研究生物、生态与医学的学院。";
                c.appearanceNotes = "与水文学家共用青龙馆基础服饰，加入野外科考装备；当前占位为浅色外衣、绿色内衬与植物样本。";
                c.dayOneLocation = "野外草丛"; c.dayOneActivity = "采集植物样本。";
                c.dayOneTask = "采集植物样本；包含检定，可获得增益道具。";
                c.planningNotes = "姓名待定，先以职能名称显示。人物职能为地质学家，第一日采集植物，按提供的设定保留。";
            });
            Seed(catalog, props, CharacterIds.YangYinglong, "YangYinglong", c =>
            {
                c.characterName = "杨应隆"; c.roleLabel = "安保人员"; c.gender = CharacterGender.Male;
                c.narrativeRole = CharacterRole.Supporting; c.faction = "罗天教"; c.department = "青霆司";
                c.title = "华杨（特勤队队长）"; c.function = "安保人员";
                c.hexagram = "大过"; c.rank = CharacterRank.HexagramBearer; c.abilityName = "过衍化";
                c.abilityDescription = "大过象征过度、物极必反，以高大树木或房梁为象征。树木过盛而衰，权能与凋亡有关；使事物快速衍化至衰亡状态。";
                c.background = "青霆司是罗天教执行部，负责直接执行劫灾的收容。杨应隆为纯战士。";
                c.appearanceNotes = "按战斗人员设计，沿用罗天教势力服饰，并与普通安保 NPC 区分；当前占位为宽肩深色战衣、红色护甲与长武器。";
                c.dayOneLocation = "野外地图边界"; c.dayOneActivity = "巡逻；劝阻第一日前往蚀海侧的玩家。";
                c.dayOneTask = "清除周围威胁；战斗任务，用于熟悉六爻系统。";
                c.planningNotes = "原备注中有“比卦象征过度”的字样；此处按人物表的大过卦录入，保留该文本差异。";
            });
            Seed(catalog, props, CharacterIds.ContainmentResearcher, "ContainmentResearcher", c =>
            {
                c.roleLabel = "收容部安保"; c.gender = CharacterGender.Female; c.narrativeRole = CharacterRole.Npc;
                c.faction = "罗天教"; c.department = "收容部（名称待定）"; c.title = "孑遗研究员";
                c.function = "安保人员，孑遗研究员"; c.hexagram = "井"; c.rank = CharacterRank.HeavenlyEye;
                c.abilityName = "收容与保存";
                c.abilityDescription = "井卦含稳定与可持续发展的意义，以水井为象征，权能与收容、保存有关。";
                c.background = "所属部门负责劫灾的收容管理与研究。使用高科技装备战斗，偏文职。";
                c.appearanceNotes = "罗天教收容管理与研究人员的基础服饰；当前占位为灰蓝工作服与亮青色科技设备。";
                c.dayOneLocation = "仓储区"; c.dayOneActivity = "检修作战设备。";
                c.dayOneTask = "修复装备；维修任务，用于熟悉六爻系统。";
                c.planningNotes = "姓名与部门正式名称待定，暂用“收容部安保”显示。";
            });
            Seed(catalog, props, CharacterIds.Mechanic, "Mechanic", c =>
            {
                c.roleLabel = "机械师"; c.gender = CharacterGender.Male; c.narrativeRole = CharacterRole.Npc;
                c.faction = "蓬莱馆"; c.department = "白虎馆"; c.title = "教授"; c.function = "机械师";
                c.hexagram = "丰"; c.rank = CharacterRank.HeavenlyEye; c.abilityName = "能量";
                c.abilityDescription = "丰卦象征宏大威仪，以太阳、雷电为象征，权能与能量有关。";
                c.background = "白虎馆为蓬莱馆下属研究工学的学院。";
                c.appearanceNotes = "蓬莱馆白虎馆工学人员的基础服饰；当前占位为赭黄工作服、护目镜与扳手。";
                c.dayOneLocation = "码头"; c.dayOneActivity = "检修码头供电系统。";
                c.dayOneTask = "搜寻工具、零件；采集任务，包含检定，可获得增益道具。";
                c.planningNotes = "姓名待定，先以职能名称显示。";
            });
            Seed(catalog, props, CharacterIds.TanYue, "TanYue", c =>
            {
                c.characterName = "谭礿"; c.roleLabel = "前科考队队长"; c.gender = CharacterGender.Female;
                c.narrativeRole = CharacterRole.Main; c.team = CharacterTeam.PreviousSurvey;
                c.faction = "无"; c.title = "流浪作家"; c.function = "前一支勘察队队长";
                c.hexagram = "萃"; c.rank = CharacterRank.HexagramLord; c.abilityName = "收集";
                c.abilityDescription = "萃为聚集、萃取之意，以聚集的水与祭祀为象征。将物质、概念容纳入自己的精神，也可释放或使用。";
                c.background = "谭礿带领的科考队在绥海堰遭遇逆模因劫灾。为保护队员，她启用科考站储备的模因掩体，暂时抹消整队存在，并发现绥海堰真相。救援队到来后，她通过隐秘方式引导反击。";
                c.appearanceNotes = "无势力所属，按流浪作家身份设计；当前占位为紫褐色外衣、肩包与书本。此形象不代表第一日影子的最终演出。";
                c.dayOneLocation = "生活区走廊（影子演出）";
                c.dayOneActivity = "玩家与物资交互后，神秘影子经过；主角当下不产生怀疑。";
                c.dayOneTask = "无第一日可接取任务；正式接触安排于第二日。";
                c.planningNotes = "属于前一支科考队，不计入主角队伍七人。逆模因、掩体及身世信息属于策划资料，不自动作为对话显示。";
            });
            EditorUtility.SetDirty(catalog); EditorUtility.SetDirty(props);
            AssetDatabase.SaveAssets();
            Debug.Log("CHARACTERS_INSTALL_OK: " + catalog.Characters.Count + " characters, sprites, portraits, map definitions and prefabs.");
        }

        private static void Seed(CharacterCatalog catalog, PropLibrary props, string id, string file, Action<CharacterDefinition> configure)
        {
            string path = DefinitionsPath + "/" + file + ".asset";
            var character = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(path);
            if (character == null)
            {
                character = ScriptableObject.CreateInstance<CharacterDefinition>();
                character.InitializeIdentity(id); configure(character);
                AssetDatabase.CreateAsset(character, path);
            }
            catalog.Add(character);
            if (character.mapSprite == null) character.mapSprite = CharacterPlaceholderArt.MapSprite(file, id);
            if (character.portrait == null) character.portrait = CharacterPlaceholderArt.Portrait(file, id);
            if (character.mapProp == null)
            {
                string propPath = PropsPath + "/" + file + ".asset";
                var prop = AssetDatabase.LoadAssetAtPath<PropDefinition>(propPath);
                if (prop == null)
                {
                    prop = ScriptableObject.CreateInstance<PropDefinition>();
                    prop.category = "角色"; prop.sprite = character.mapSprite; prop.character = character;
                    prop.displayName = character.DisplayName; prop.actions = PropActions.None;
                    prop.allowRotation = false; prop.worldSize = new Vector2(.8f, 1.2f);
                    prop.isSolid = true; prop.colliderSize = new Vector2(.45f, .2f); prop.colliderOffset = new Vector2(0, .08f);
                    prop.interactionLabel = "交谈";
                    prop.physicsMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>("Assets/Art/NoFriction.physicsMaterial2D");
                    AssetDatabase.CreateAsset(prop, propPath);
                }
                character.mapProp = prop;
            }
            props.Add(character.mapProp);
            if (character.prefab == null)
            {
                string prefabPath = PrefabsPath + "/" + file + ".prefab";
                character.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (character.prefab == null)
                {
                    var obj = new GameObject(character.DisplayName);
                    try
                    {
                        obj.AddComponent<PropInstance>().Configure(character.mapProp);
                        character.prefab = PrefabUtility.SaveAsPrefabAsset(obj, prefabPath);
                    }
                    finally { UnityEngine.Object.DestroyImmediate(obj); }
                }
            }
            EditorUtility.SetDirty(character);
        }

        public static void SyncMapProp(CharacterDefinition character)
        {
            if (character == null || character.mapProp == null) return;
            Undo.RecordObject(character.mapProp, "同步角色外观与名称");
            character.mapProp.character = character;
            character.mapProp.displayName = character.DisplayName;
            character.mapProp.sprite = character.mapSprite;
            EditorUtility.SetDirty(character.mapProp);
            PropDefinitionEditor.ApplyToScene(character.mapProp);
        }
    }
}

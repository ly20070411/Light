using System;
using System.Collections.Generic;
using Emerge.Characters;
using Emerge.Props;
using Emerge.Props.Editor;
using UnityEditor;
using UnityEngine;

namespace Emerge.Day1
{
    /// <summary>Creates the initial editable draft once; repeated setup keeps the user's text.</summary>
    public static class Day1StoryBuilder
    {
        public const string AssetPath = "Assets/Day1/Config/Day1Story.asset";

        public static Day1StoryDefinition Ensure(CharacterCatalog characters)
        {
            var story = AssetDatabase.LoadAssetAtPath<Day1StoryDefinition>(AssetPath);
            if (story != null) { AppendImplementationNotes(story); return story; }
            if (characters == null) throw new InvalidOperationException("Day1 文案缺少角色库。");
            PropAssetFactory.EnsureFolder("Assets/Day1/Config");
            story = ScriptableObject.CreateInstance<Day1StoryDefinition>();
            story.planningNotes =
                "【文案草案】依据 Docs/Story/Day1.md 创建；此配置仅保存文本，不执行流程。后续修改此资产不会被生成器覆盖。\n" +
                "【占位】认知测试采用三题身份、位置与异常记录确认，不计隐藏值，复核后继续。\n" +
                "【占位】四位未命名队员使用职业称呼，其对白不关联姓名或头像；待正式命名后由策划修改。\n" +
                "【占位】安保通讯的具体发送者、海况数据、堰段编号和生活物资种类待定。\n" +
                "【占位】收尾复用总控室终端；海水采样保留至第二日。具体结束门槛由流程配置决定。\n" +
                "【占位】六爻目标值和奖励效果见各检定资产描述；本配置不决定隐藏线索分值与结局判定。";
            story.quiz = new List<Day1QuizQuestion>
            {
                new Day1QuizQuestion
                {
                    prompt = "入站认知校准：请确认你的姓名。",
                    correctLabel = "桓玉鉴", reviewLabel = "暂时无法确认",
                    correctFeedback = "姓名与身份记录一致。请继续确认当前环境。",
                    reviewFeedback = "身份记录已调出：桓玉鉴。请对照认证信息重新确认。"
                },
                new Day1QuizQuestion
                {
                    prompt = "你当前所在的位置是？",
                    correctLabel = "科考站认知缓冲间", reviewLabel = "野外采样点",
                    correctFeedback = "当前位置确认完成。请核对异常记录的处理方式。",
                    reviewFeedback = "当前位置识别需要复核。门侧标识为“认知缓冲间”，请观察后重新确认。"
                },
                new Day1QuizQuestion
                {
                    prompt = "记录中出现无法辨认的字段时，应如何处理？",
                    correctLabel = "保留原始记录并报告", reviewLabel = "按印象补写",
                    correctFeedback = "处理方式确认完成。精神状态校准完成，身份认证卡已发放。",
                    reviewFeedback = "请保留未识别信息并报告，避免以推测替代原始记录。请重新确认处理方式。"
                }
            };
            story.rules = Lines(
                Hero(characters, "各位，先核对站内的注意事项。"),
                Terminal("规则终端", "绥海堰科考站 · 入驻注意事项\n一、出入科考站须完成身份核验与精神状态检查。\n二、环境异常以原始记录为准，不得仅凭常识修正仪器输出。"),
                Terminal("规则终端", "三、记录中的姓名、时间或用途字段若无法读取，应保留原样并报告，不得自行补写或删除。\n四、若出现记忆缺失、读写困难或无法说明当前工作目的，应停止单独作业，联系随队卦者复核。"),
                Terminal("规则终端", "五、外围安全确认完成前，不得独自进入蚀海侧作业区。"),
                Hero(characters, "异常照实记录。记不清的，也照实记录，不要替它找一个解释。"),
                Hero(characters, "林溪，站内设备由你组织检查。其他人先做入驻准备。外围与供电确认后，再安排外出作业。"),
                Named(characters, CharacterIds.LinXi, "明白。我去机房核对设备状态。"),
                Named(characters, CharacterIds.YangYinglong, "我先检查站外边界。"));
            story.environment = Lines(
                Terminal("环境监测台", "当前环境记录已调出。请保存原始数据后再进行分析。"),
                Hero(characters, "先把这份记录带上。后续采样要和它对照。"),
                Terminal("记录", "获得：环境数据档案。"),
                Terminal("安保通讯", "生活物资已经放在食堂餐桌上，请过去领取。"),
                Hero(characters, "收到。"));
            story.supplies = Lines(Hero(characters, "物资放在这里了。"), Terminal("记录", "已领取生活物资。"));
            story.shadowAfter = Lines(Hero(characters, "灯接触不良么……回头让机房看一下。"));
            story.seaGate = Lines(
                Named(characters, CharacterIds.YangYinglong, "停一下。蚀海侧今天还不能进去。"),
                Named(characters, CharacterIds.YangYinglong, "这边还没完成安全确认，今天不要单独过去。采样明天再安排。"),
                Hero(characters, "好，我先回去做准备。"));
            story.tools = Lines(Terminal("工具箱", "找到所需工具。"));
            story.powerBefore = Lines(Terminal("供电箱", "码头供电：检修中。"));
            story.powerAfter = Lines(Terminal("供电箱", "码头供电：检修完成。"));
            story.closing = Lines(Hero(characters, "今天先到这里。其余工作明天继续。"));

            story.npcs = new List<Day1NpcScript>
            {
                new Day1NpcScript
                {
                    roleId = CharacterIds.LinXi,
                    offer = Lines(
                        Hero(characters, "林溪，站内设备怎么样？"),
                        Named(characters, CharacterIds.LinXi, "几路信息还没接顺。我先核对机房，你替我看看总控室的终端状态。"),
                        Hero(characters, "需要查哪些？"),
                        Named(characters, CharacterIds.LinXi, "先确认各终端能否正常读取。拿到原始检查结果就回来，别急着替记录下结论。")),
                    accepted = Lines(Hero(characters, "我去总控室检查，拿到结果就回来。")),
                    ongoing = Lines(Named(characters, CharacterIds.LinXi, "总控室的检查结果拿到了么？")),
                    handover = Lines(Named(characters, CharacterIds.LinXi, "有这份结果，我就能把各路信息接起来。谢谢。")),
                    completed = Lines(Named(characters, CharacterIds.LinXi, "设备状态已经对上了。遇到读不出的记录，先留着，我会再看。"))
                },
                new Day1NpcScript
                {
                    roleId = CharacterIds.Hydrologist,
                    offer = Lines(
                        Terminal("水文学家", "我来自青龙馆，水文方向。这次负责水质分析。"),
                        Hero(characters, "水文方向，我记下了。这里有什么需要准备的？"),
                        Terminal("水文学家", "我要把新取的海水样本和站里的环境记录对照。你方便时，替我带一份蚀海海水回来。"),
                        Hero(characters, "取样要求也一并给我。"),
                        Terminal("水文学家", "位置、时间和容器状态都要记录。样本有异常，别自行处理，直接带回原始记录。")),
                    accepted = Lines(Hero(characters, "我先确认外围安排，开放后再去采样。")),
                    ongoing = Lines(Terminal("水文学家", "采样前先确认外围是否开放，容器和原始记录都别遗漏。")),
                    handover = Lines(
                        Hero(characters, "蚀海侧今天还没开放，水样明天再取。"),
                        Terminal("水文学家", "知道了，先按安保安排。我把分析台准备好，明天取回来再处理。")),
                    completed = Lines(Terminal("水文学家", "水样明天再取。今天先把准备工作做完。"))
                },
                new Day1NpcScript
                {
                    roleId = CharacterIds.Geologist,
                    offer = Lines(
                        Terminal("地质学家", "我来自青龙馆，地质方向，正在记录这里的植物分布。"),
                        Hero(characters, "植物也归这次采样？"),
                        Terminal("地质学家", "生长的位置要和地表状况一起看。帮我取一份完整样本，根部和周围的土也留一些。")),
                    accepted = Lines(Hero(characters, "我取好后带回来。")),
                    ongoing = Lines(Terminal("地质学家", "要保留完整结构，别只摘叶子。")),
                    handover = Lines(Terminal("地质学家", "够做基础记录了。你取下它的位置，我也会一起记上。")),
                    completed = Lines(Terminal("地质学家", "这处的样本已经够了。其他区域等后续作业再看。"))
                },
                new Day1NpcScript
                {
                    roleId = CharacterIds.YangYinglong,
                    offer = Lines(
                        Hero(characters, "杨应隆，外围情况如何？"),
                        Named(characters, CharacterIds.YangYinglong, "边界附近有需要清理的威胁。先处理掉，其他人才能安心采样。"),
                        Hero(characters, "我和你确认一下位置。"),
                        Named(characters, CharacterIds.YangYinglong, "就在前面。准备好再过去。")),
                    accepted = Lines(Hero(characters, "我去处理周围威胁。")),
                    ongoing = Lines(Named(characters, CharacterIds.YangYinglong, "威胁还在。准备好了再处理。")),
                    handover = Lines(Named(characters, CharacterIds.YangYinglong, "这段先清出来了。我继续巡逻，你去看其他人的准备情况。")),
                    completed = Lines(Named(characters, CharacterIds.YangYinglong, "外围这段已经清理。蚀海侧的作业安排仍要等明天确认。"))
                },
                new Day1NpcScript
                {
                    roleId = CharacterIds.ContainmentResearcher,
                    offer = Lines(
                        Terminal("收容部安保", "我是随队的孑遗研究员，这次负责安保和装备维护。"),
                        Hero(characters, "装备有什么问题？"),
                        Terminal("收容部安保", "这套作战设备还没通过检修。帮我把异常位置找出来，先恢复到可用状态。"),
                        Hero(characters, "在这里处理？"),
                        Terminal("收容部安保", "对，工具在工作台上。先检查，再决定怎么动手。")),
                    accepted = Lines(Hero(characters, "我先检查故障，再处理异常部件。")),
                    ongoing = Lines(Terminal("收容部安保", "设备就在维修台。检查结果和处理步骤都记下来，遇到困难我会协助。")),
                    handover = Lines(Terminal("收容部安保", "我再复核一遍。装备能正常使用，安保准备才算做完。")),
                    completed = Lines(Terminal("收容部安保", "这套已经检修过了，暂时不用再拆。"))
                },
                new Day1NpcScript
                {
                    roleId = CharacterIds.Mechanic,
                    offer = Lines(
                        Terminal("机械师", "我来自白虎馆。机械和供电这边由我负责。"),
                        Hero(characters, "还缺什么？"),
                        Terminal("机械师", "工具和替换零件没配齐。帮我在码头附近找找，我先检查线路。")),
                    accepted = Lines(Hero(characters, "找齐后交给你。")),
                    ongoing = Lines(Terminal("机械师", "还需要工具和替换零件。都在码头附近，找齐后一起交给我。")),
                    handover = Lines(Terminal("机械师", "找齐了。这下能把供电接稳。你先回站里，我把剩下的处理好。")),
                    completed = Lines(Terminal("机械师", "码头供电恢复了。明天出发前我会再检查一次。"))
                }
            };
            AssetDatabase.CreateAsset(story, AssetPath);
            AppendImplementationNotes(story);
            AssetDatabase.SaveAssets();
            return story;
        }

        private static List<PropDialogueLine> Lines(params PropDialogueLine[] lines)
            => new List<PropDialogueLine>(lines);

        private static void AppendImplementationNotes(Day1StoryDefinition story)
        {
            if ((story.planningNotes ?? "").Contains("【当前原型】")) return;
            story.planningNotes += "\n【当前原型】收尾暂定五项作业与明日采水安排全部回报，再主动确认结束；检定失败保留基础成果，成功额外奖励只入包，增益效果待接。\n" +
                "【当前原型】植物用着绿火炬、桌箱代设备与门；敌种为E01占位，35HP，4/7伤害；影子演出1.5秒，噪声为临时效果。\n" +
                "【当前原型】H01–H03候选线索、隐藏分值与结局判定尚未接入；第二日流程待制作。";
            EditorUtility.SetDirty(story);
        }

        private static PropDialogueLine Terminal(string speaker, string text)
            => new PropDialogueLine { speaker = speaker, text = text };

        private static PropDialogueLine Hero(CharacterCatalog characters, string text)
            => Named(characters, CharacterIds.HuanYujian, text);

        private static PropDialogueLine Named(CharacterCatalog characters, string roleId, string text)
        {
            var character = characters.Find(roleId);
            if (character == null) throw new InvalidOperationException("Day1 文案缺少角色：" + roleId);
            return new PropDialogueLine { character = character, text = text };
        }
    }
}

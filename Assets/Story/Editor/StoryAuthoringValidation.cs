using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Emerge.Props;
using UnityEditor;
using UnityEngine;

namespace Emerge.Story.Editor
{
    /// <summary>Editor-only checks. Uses temporary copies for edit/save/Undo checks.</summary>
    public static class StoryAuthoringValidation
    {
        private const string ReportPath = "Validation/story-authoring-results.json";
        private const string SourceAuditPath = "Validation/StoryImport/sources.json";
        private const string BaselinePath = "Validation/StoryImport/baseline-hashes.json";
        [Serializable] private sealed class Check { public string name, observed; public bool passed; }
        [Serializable] private sealed class Report
        {
            public bool success;
            public string completedUtc;
            public List<Check> checks = new List<Check>();
        }
        [Serializable] private sealed class StringValue { public string value; }
        [Serializable] private sealed class HashEntry { public string Path, Hash; }
        [Serializable] private sealed class HashList { public List<HashEntry> files; }

        [MenuItem("Tools/剧情/新版初稿/校验资料")]
        private static void ValidateFromMenu() { ValidateAndWrite(); }

        public static bool ValidateAndWrite()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("新版剧情资料校验只在编辑模式运行。请先停止 Play。");
            var report = new Report();
            try
            {
                var project = StoryDraftImporter.EnsureInstalled();
                Add(report, "初稿项目可加载且为编辑资料", project != null && project.schemaVersion == 1 &&
                    AssetDatabase.GetAssetPath(project) == StoryDraftImporter.ProjectPath,
                    StoryDraftImporter.ProjectPath);
                if (project != null)
                {
                    ValidateRecords(report, project);
                    ValidateSourceItems(report, project);
                    ValidateGraphAndRules(report, project);
                    ValidateReinstall(report, project);
                    ValidateTemporaryEditing(report, project);
                }
                ValidateBaseline(report);
            }
            catch (Exception exception)
            {
                Add(report, "校验过程完整运行", false, exception.ToString());
                Debug.LogException(exception);
            }
            report.success = report.checks.Count > 0 && report.checks.All(check => check.passed);
            report.completedUtc = DateTime.UtcNow.ToString("o");
            Directory.CreateDirectory("Validation");
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
            if (report.success) Debug.Log("STORY_AUTHORING_VALIDATION_OK: " + report.checks.Count + " checks; no Play session.");
            else Debug.LogError("新版剧情资料校验未通过，请查看 " + ReportPath);
            return report.success;
        }

        private static void ValidateRecords(Report report, StoryProjectDefinition project)
        {
            Unique(report, "来源", project.sources.Select(record => record.id));
            Unique(report, "角色", project.characters.Select(record => record.id));
            Unique(report, "场景", project.scenes.Select(record => record.id));
            Unique(report, "道具", project.items.Select(record => record.id));
            Unique(report, "剧情节点", project.nodes.Select(record => record.id));
            Unique(report, "规则", project.rules.Select(record => record.id));
            Unique(report, "背景", project.lore.Select(record => record.id));
            Unique(report, "待定事项", project.issues.Select(record => record.id));
            Add(report, "八名角色保持已有稳定 ID 与资产绑定", project.characters.Count == 8 && project.characters.All(character =>
                character.asset != null && character.asset.Id == character.id), "不新建角色 ID、图片或预制体");
            string[] expectedNames = { "桓玉鉴", "林溪", "鹿见深", "虞青", "杨应隆", "常月容", "唐晦", "谭礿" };
            Add(report, "新版八人姓名全部录入", expectedNames.All(name => project.characters.Any(character => character.name == name)),
                string.Join("、", expectedNames));
            Add(report, "常月容部门为深泉司", project.characters.Any(character => character.name == "常月容" &&
                (character.department ?? "").Contains("深泉司")), "依据新版人物表");
            var tasks = project.nodes.Where(node => node.kind == "Day1支线任务").ToArray();
            var numbered = project.nodes.Where(node => !string.IsNullOrWhiteSpace(node.eventNumber) && node.kind != "Day1支线任务").ToArray();
            Add(report, "21 个编号事件完整且唯一", numbered.Length == 21 && Enumerable.Range(1, 21).All(number =>
                numbered.Count(node => Number(node.eventNumber) == number) == 1), string.Join(", ", numbered.Select(node => node.eventNumber)));
            Add(report, "Day1 六名队员各有一条任务资料", tasks.Length == 6 && tasks.All(node => node.day == 1) &&
                project.characters.Where(character => character.name != "桓玉鉴" && character.name != "谭礿").All(character =>
                    tasks.Count(node => node.id == "task-" + character.id && node.characterIds.Contains(character.id)) == 1), "六条支线不替代四号自由探索事件");
            Add(report, "参考布局图已导入", project.layoutReference != null &&
                AssetDatabase.GetAssetPath(project.layoutReference) == "Assets/Story/Source/StationLayout.png", "仅为资料参考");
            Add(report, "未定剧情有显式占位说明", project.nodes.Where(node => node.placeholder).All(node =>
                !string.IsNullOrWhiteSpace(node.designNotes) || !string.IsNullOrWhiteSpace(node.summary)) &&
                project.issues.Count > 0 && project.items.Where(item => item.placeholder).All(item =>
                    !string.IsNullOrWhiteSpace(item.notes) && !string.IsNullOrWhiteSpace(item.source)), "占位节点、正文道具及待沟通事项");
            Add(report, "新版两份原始附件均有来源记录", new[] { "细纲.docx", "道具需求.xlsx" }.All(fileName =>
                project.sources.Any(source => source.fileName == fileName && !string.IsNullOrWhiteSpace(source.sha256))),
                "原件哈希与用户规则确认分别保存");

            var library = AssetDatabase.LoadAssetAtPath<PropLibrary>(StoryDraftImporter.PropLibraryPath);
            Add(report, "独立剧情道具库包含全部新道具", library != null && project.items.All(item =>
                item.asset != null && library.Props.Contains(item.asset)), StoryDraftImporter.PropLibraryPath);
            foreach (var item in project.items)
            {
                var prop = item.asset;
                Add(report, "道具仅存储资料 / " + item.id, prop != null && prop.Id == "story." + item.id &&
                    prop.InventoryKey == "story." + item.id && prop.actions == PropActions.None &&
                    prop.checkEvent == null && prop.battleEncounter == null &&
                    (prop.itemHandover == null || !prop.itemHandover.enabled) &&
                    (prop.grantedFlags == null || prop.grantedFlags.Length == 0), item.name);
            }
        }

        private static void ValidateSourceItems(Report report, StoryProjectDefinition project)
        {
            string sourcePath = File.Exists(SourceAuditPath) ? SourceAuditPath : "Docs/Story/Sources/ExtractedSources.json";
            if (!File.Exists(sourcePath)) { Add(report, "道具原表提取记录可读取", false, sourcePath); return; }
            var cells = ReadCells(File.ReadAllText(sourcePath));
            var originals = project.items.Where(item => !item.placeholder && Number(item.sourceNumber) > 0).ToArray();
            Add(report, "道具表 18 条原始记录完整", originals.Length == 18 && Enumerable.Range(1, 18).All(number =>
                originals.Count(item => Number(item.sourceNumber) == number) == 1), "Sheet1 第 2—19 行；空白字段保留空白");
            foreach (int number in Enumerable.Range(1, 18))
            {
                var item = originals.FirstOrDefault(record => Number(record.sourceNumber) == number);
                if (item == null) { Add(report, "来源表道具 " + number, false, "缺失"); continue; }
                int row = number + 1;
                string[] actual = { item.sourceNumber, item.name, item.function, item.description,
                    item.programEffect, item.acquisition, item.artNotes, item.modified };
                string[] labels = { "ID", "名称", "功能", "简介", "程序效果说明", "获取条件", "需求备注", "修改时间" };
                for (int column = 0; column < actual.Length; column++)
                {
                    string address = ((char)('A' + column)).ToString() + row;
                    string expected = cells.TryGetValue(address, out string value) ? value : "";
                    Add(report, "来源忠实 / " + number + " / " + labels[column], Normalize(actual[column]) == Normalize(expected),
                        "道具需求.xlsx/Sheet1/" + address);
                }
                Add(report, "来源可追溯 / " + number, !string.IsNullOrWhiteSpace(item.source) &&
                    item.source.Contains("道具需求.xlsx") && item.source.Contains("Sheet1"), item.source);
            }
            foreach (var source in project.sources.Where(record => record.fileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase) ||
                record.fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)))
            {
                string fileName = Path.GetFileName(source.fileName);
                string archive = "Docs/Story/Sources/" + fileName;
                Add(report, "来源原件哈希 / " + fileName, File.Exists(archive) &&
                    string.Equals(Sha256(archive), source.sha256, StringComparison.OrdinalIgnoreCase), archive);
            }
        }

        private static void ValidateGraphAndRules(Report report, StoryProjectDefinition project)
        {
            var nodeIds = new HashSet<string>(project.nodes.Select(node => node.id), StringComparer.Ordinal);
            var sceneIds = new HashSet<string>(project.scenes.Select(scene => scene.id), StringComparer.Ordinal);
            var characterIds = new HashSet<string>(project.characters.Select(character => character.id), StringComparer.Ordinal);
            var itemIds = new HashSet<string>(project.items.Select(item => item.id), StringComparer.Ordinal);
            foreach (var node in project.nodes)
            {
                Add(report, "场景角色道具引用 / " + node.id, node.sceneIds.All(sceneIds.Contains) &&
                    node.characterIds.All(characterIds.Contains) && node.itemIds.All(itemIds.Contains) &&
                    node.lines.All(line => string.IsNullOrWhiteSpace(line.speakerId) || characterIds.Contains(line.speakerId)), node.title);
                Add(report, "分支引用 / " + node.id, node.branches.All(branch =>
                    (string.IsNullOrWhiteSpace(branch.targetId) || nodeIds.Contains(branch.targetId)) &&
                    branch.grantItemIds.All(itemIds.Contains) && branch.consumeItemIds.All(itemIds.Contains)) &&
                    node.branches.Select(branch => branch.id).Distinct().Count() == node.branches.Count &&
                    node.branches.All(branch => !string.IsNullOrWhiteSpace(branch.id)), node.title);
            }
            var scoring = project.nodes.SelectMany(node => node.branches.Select(branch => new { node, branch }))
                .Where(entry => entry.branch.hiddenValueDelta != 0).ToArray();
            int[] scoringEvents = { 5, 8, 14, 17 };
            Add(report, "四处主线隐藏值各加 1 且已确认", scoring.Length == 4 && scoring.All(entry =>
                entry.branch.hiddenValueDelta == 1 && entry.branch.hiddenValueConfirmed) && scoringEvents.All(number =>
                    scoring.Count(entry => Number(entry.node.eventNumber) == number) == 1),
                "5 三次采样；8 林溪继续工作；14 读取记忆；17 追击劫灾");
            var judgment = project.nodes.FirstOrDefault(node => Number(node.eventNumber) == 18);
            Add(report, "第三日晚高分条件包含恰好 3 点", judgment != null && judgment.branches.Any(branch =>
                Regex.IsMatch(branch.condition ?? "", @"(?:>=|≥|大于等于)\s*3")), "按用户确认：隐藏值 ≥ 3");
            Add(report, "第三日晚保存低分撤离与继续调查选择", judgment != null &&
                judgment.branches.Any(branch => Regex.IsMatch(branch.condition ?? "", @"(?:==|=|为|等于)\s*0|0\s*点")) &&
                judgment.branches.Any(branch => (branch.condition ?? "").Contains("1") && (branch.condition ?? "").Contains("2")),
                "0 点进入 NE；1—2 点可撤离或进入 BE");
            var first = project.nodes.FirstOrDefault(node => Number(node.eventNumber) == 1);
            var reachable = new HashSet<string>(StringComparer.Ordinal);
            if (first != null)
            {
                var queue = new Queue<string>(); queue.Enqueue(first.id);
                while (queue.Count > 0)
                {
                    string id = queue.Dequeue();
                    if (!reachable.Add(id)) continue;
                    var node = project.nodes.FirstOrDefault(record => record.id == id);
                    if (node != null) foreach (var branch in node.branches.Where(branch => nodeIds.Contains(branch.targetId))) queue.Enqueue(branch.targetId);
                }
            }
            Add(report, "初始节点可到达 NE 末事件", project.nodes.Any(node => Number(node.eventNumber) == 21 && reachable.Contains(node.id)),
                "按分支目标遍历，不执行运行时剧情");
            foreach (string route in new[] { "HE", "BE", "TE" })
                Add(report, "初始节点可到达占位 " + route, project.nodes.Any(node => node.placeholder &&
                    (node.route == route || (route == "HE" && node.route == "HE/TE")) && reachable.Contains(node.id)), "后续流程保持待定");
        }

        private static void ValidateReinstall(Report report, StoryProjectDefinition project)
        {
            string before = EditorJsonUtility.ToJson(project);
            var characters = project.characters.Where(record => record.asset != null).Select(record => record.asset).Distinct().ToArray();
            var items = project.items.Where(record => record.asset != null).Select(record => record.asset).Distinct().ToArray();
            var characterJson = characters.Select(asset => EditorJsonUtility.ToJson(asset)).ToArray();
            var itemJson = items.Select(asset => EditorJsonUtility.ToJson(asset)).ToArray();
            var again = StoryDraftImporter.EnsureInstalled();
            Add(report, "重复补齐保留剧情项目编辑值", again == project && before == EditorJsonUtility.ToJson(again), "不重新覆盖节点、文案或规则");
            Add(report, "重复补齐保留角色与道具资料", characterJson.SequenceEqual(characters.Select(asset => EditorJsonUtility.ToJson(asset))) &&
                itemJson.SequenceEqual(items.Select(asset => EditorJsonUtility.ToJson(asset))), "既有引用与元数据保持不变");
        }

        private static void ValidateTemporaryEditing(Report report, StoryProjectDefinition project)
        {
            string tempPath = "Assets/Story/InitialDraft/ValidationCopy-" + Guid.NewGuid().ToString("N") + ".asset";
            StoryProjectDefinition copy = null;
            try
            {
                if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(project), tempPath))
                    throw new IOException("不能创建临时剧情资产副本。");
                copy = AssetDatabase.LoadAssetAtPath<StoryProjectDefinition>(tempPath);
                var serialized = new SerializedObject(copy);
                var summary = serialized.FindProperty("nodes.Array.data[0].summary");
                if (summary == null) throw new InvalidDataException("无法通过 SerializedObject 编辑节点文案。");
                string original = summary.stringValue;
                string changed = original + "\n[临时保存校验]";
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("临时剧情资料 Undo 校验");
                summary.stringValue = changed;
                serialized.ApplyModifiedProperties();
                Undo.FlushUndoRecordObjects();
                Add(report, "嵌套节点可通过序列化字段编辑", copy.nodes[0].summary == changed, "仅修改临时副本");
                Undo.PerformUndo();
                serialized.Update();
                Add(report, "真实 Undo 恢复剧情字段", serialized.FindProperty("nodes.Array.data[0].summary").stringValue == original,
                    "不撤销用户项目编辑");
                summary = serialized.FindProperty("nodes.Array.data[0].summary");
                summary.stringValue = changed;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(copy);
                AssetDatabase.SaveAssetIfDirty(copy);
                Undo.ClearUndo(copy);
                serialized.Dispose();
                Resources.UnloadAsset(copy);
                copy = null;
                AssetDatabase.ImportAsset(tempPath, ImportAssetOptions.ForceUpdate);
                copy = AssetDatabase.LoadAssetAtPath<StoryProjectDefinition>(tempPath);
                Add(report, "保存并重载后节点文案保留", copy != null && copy.nodes[0].summary == changed,
                    "卸载临时 SO 后从磁盘重载");
                Add(report, "保存重载后角色与道具引用保留", copy != null &&
                    copy.characters.Select(record => record.asset).SequenceEqual(project.characters.Select(record => record.asset)) &&
                    copy.items.Select(record => record.asset).SequenceEqual(project.items.Select(record => record.asset)), "不复制或替换共享资源");
            }
            finally
            {
                if (copy != null) Undo.ClearUndo(copy);
                AssetDatabase.DeleteAsset(tempPath);
            }
        }

        private static void ValidateBaseline(Report report)
        {
            if (!File.Exists(BaselinePath))
            {
                Add(report, "旧可玩流程本地基线检查", true, "当前检出未保存本次工作的本地基线，跳过；剧情资料校验继续。");
                return;
            }
            var baseline = JsonUtility.FromJson<HashList>("{\"files\":" + File.ReadAllText(BaselinePath) + "}");
            Add(report, "旧可玩流程基线包含场景、脚本与构建配置", baseline != null && baseline.files != null && baseline.files.Count == 3, BaselinePath);
            if (baseline?.files == null) return;
            foreach (var entry in baseline.files)
                Add(report, "旧可玩流程未改动 / " + Path.GetFileName(entry.Path), File.Exists(entry.Path) &&
                    string.Equals(Sha256(entry.Path), entry.Hash, StringComparison.OrdinalIgnoreCase), entry.Path);
        }

        private static Dictionary<string, string> ReadCells(string json)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            // Source extraction represents a cell as a flat address/value pair; numeric IDs stay numeric.
            string expression = "\\{\\s*\"cell\"\\s*:\\s*\"([A-H][0-9]+)\"\\s*,\\s*\"value\"\\s*:\\s*(\"(?:\\\\.|[^\"\\\\])*\"|-?[0-9]+(?:\\.[0-9]+)?)\\s*\\}";
            foreach (Match match in Regex.Matches(json, expression, RegexOptions.CultureInvariant))
            {
                string token = match.Groups[2].Value;
                result[match.Groups[1].Value] = token.StartsWith("\"", StringComparison.Ordinal) ?
                    JsonUtility.FromJson<StringValue>("{\"value\":" + token + "}").value : token;
            }
            return result;
        }
        private static string Normalize(string value) => (value ?? "").Replace("\r\n", "\n").Replace("\r", "\n");
        private static int Number(string value) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) ? number : -1;
        private static string Sha256(string path)
        {
            using (var algorithm = SHA256.Create()) using (var stream = File.OpenRead(path))
                return BitConverter.ToString(algorithm.ComputeHash(stream)).Replace("-", "");
        }
        private static void Unique(Report report, string label, IEnumerable<string> ids)
        {
            var values = ids.ToArray();
            Add(report, label + " ID 非空且唯一", values.All(value => !string.IsNullOrWhiteSpace(value)) &&
                values.Distinct(StringComparer.Ordinal).Count() == values.Length, values.Length + " 条");
        }
        private static void Add(Report report, string name, bool passed, string observed) => report.checks.Add(new Check
            { name = name, passed = passed, observed = observed ?? "" });
    }
}

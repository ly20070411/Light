using System;
using System.Collections;
using System.IO;
using Emerge.Battle.Tests;
using UnityEditor;
using UnityEngine;

namespace Emerge.Battle.Editor
{
    // Pure engine checks run in Edit mode, leaving scene setup and game save data untouched.
    [InitializeOnLoad]
    public static class BattleSkillTableValidation
    {
        [Serializable] private sealed class Command { public string id, verb; public int trialsPerGroup = 1000; }
        [Serializable] private sealed class Result { public string id, verb, state, message, completedUtc; public bool success; }
        private static IEnumerator sweep;
        private static string currentId, currentVerb;
        private static bool functionPassed = true, balancePassed;
        private static double nextPoll;
        private const string LastKey = "Light.BattleSkillTableValidation.LastCommand";
        static BattleSkillTableValidation() { EditorApplication.update += Update; }
        [MenuItem("Tools/战斗系统/新版技能表/运行功能验证")]
        public static void Run() => Begin(Guid.NewGuid().ToString("N"), "validate", 1000);
        [MenuItem("Tools/战斗系统/新版技能表/运行无物品数值模拟")]
        public static void Balance() => Begin(Guid.NewGuid().ToString("N"), "balance", 1000);
        [MenuItem("Tools/战斗系统/新版技能表/运行全部验证")]
        public static void All() => Begin(Guid.NewGuid().ToString("N"), "all", 1000);
        // Unity -batchmode -projectPath <project> -executeMethod Emerge.Battle.Editor.BattleSkillTableValidation.RunAllBatch
        public static void RunAllBatch()
        {
            bool passed = false;
            try
            {
                BattleSkillTableSetup.Install();
                bool functions = BattleSkillTableTest.RunChecks(); bool balances = false;
                var iterator = BattleSkillTableBalanceTest.RunSweep(value => balances = value);
                while (iterator.MoveNext()) { }
                passed = functions && balances;
            }
            catch (Exception exception) { Debug.LogException(exception); }
            if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
        }
        private static void Begin(string id, string verb, int trials)
        {
            if (sweep != null) throw new InvalidOperationException("新版技能表模拟正在运行。");
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("请在脚本导入完成后的编辑模式运行。");
            currentId = id; currentVerb = verb; functionPassed = true; balancePassed = false;
            Write("running", true, "正在使用真实引擎验证新版技能表；数值模拟不使用物品。");
            try
            {
                if (verb == "validate" || verb == "all") functionPassed = BattleSkillTableTest.RunChecks();
                if (verb == "balance" || verb == "all") sweep = BattleSkillTableBalanceTest.RunSweep(value => balancePassed = value, Mathf.Clamp(trials, 1, 10000));
                else Write("complete", functionPassed, functionPassed ? "新版技能表功能验证完成。" : "功能验证存在失败项，请查看报告。");
            }
            catch (Exception exception) { sweep = null; Write("failed", false, exception.ToString()); Debug.LogException(exception); }
        }
        private static void Update()
        {
            if (sweep != null)
            {
                try
                {
                    if (sweep.MoveNext()) return;
                    sweep = null; bool passed = functionPassed && balancePassed;
                    Write("complete", passed, passed ? "新版技能表验证完成。" : "验证存在失败项，请查看两个报告。");
                }
                catch (Exception exception) { sweep = null; Write("failed", false, exception.ToString()); Debug.LogException(exception); }
                return;
            }
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            nextPoll = EditorApplication.timeSinceStartup + .7;
            string path = ProjectPath("Validation/skill-table-command.json"); if (!File.Exists(path)) return;
            Command command;
            try { command = JsonUtility.FromJson<Command>(File.ReadAllText(path)); } catch (Exception) { return; }
            if (command == null || string.IsNullOrWhiteSpace(command.id) || command.id == SessionState.GetString(LastKey, "")) return;
            try
            {
                var previous = JsonUtility.FromJson<Result>(File.ReadAllText(ProjectPath("Validation/skill-table-command-result.json")));
                if (previous != null && previous.id == command.id) { SessionState.SetString(LastKey, command.id); return; }
            }
            catch (Exception) { }
            SessionState.SetString(LastKey, command.id);
            if (command.verb != "validate" && command.verb != "balance" && command.verb != "all")
            { currentId = command.id; currentVerb = command.verb; Write("rejected", false, "支持 validate、balance 和 all。"); return; }
            try { Begin(command.id, command.verb, command.trialsPerGroup <= 0 ? 1000 : command.trialsPerGroup); }
            catch (Exception exception) { currentId = command.id; currentVerb = command.verb; Write("rejected", false, exception.Message); }
        }
        private static void Write(string state, bool success, string message)
        {
            string path = ProjectPath("Validation/skill-table-command-result.json"); Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(new Result { id = currentId, verb = currentVerb, state = state, success = success, message = message, completedUtc = DateTime.UtcNow.ToString("O") }, true));
        }
        private static string ProjectPath(string path) => Path.GetFullPath(Path.Combine(Application.dataPath, "../" + path));
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Emerge.GameFlow.Editor;
using Emerge.Props.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Emerge.Checks.Editor
{
    // Only the two existing regression runners are available through this local command file.
    [InitializeOnLoad]
    public static class CheckRegressionValidation
    {
        private const string Prefix = "Emerge.Checks.Regression.";
        private const string PendingKey = Prefix + "Pending";
        private const string StageKey = Prefix + "Stage";
        private const string SetupKey = Prefix + "Setup";
        private const string ReportKey = Prefix + "Report";
        private const string StartedKey = Prefix + "Started";
        private const string StageUtcKey = Prefix + "StageUtc";
        private const string LastCommandKey = Prefix + "LastCommand";
        private const int FruitRunning = 1, GameFlowReady = 2, GameFlowRunning = 3, Finishing = 4;
        private static double nextPoll;
        private static bool deferTransition;

        [Serializable] private sealed class Command { public string id; public string verb; }
        [Serializable] private sealed class SceneSnapshot { public SceneSetup[] scenes; }
        [Serializable] private sealed class ExistingCheck { public string name; public bool passed; public string observed; }
        [Serializable] private sealed class ExistingReport
        { public bool passed; public string completedUtc; public List<ExistingCheck> checks; }
        [Serializable] public sealed class SuiteResult
        { public string name, reportPath, completedUtc, error; public bool fresh, passed; public int checkCount; }
        [Serializable] public sealed class Report
        {
            public string id, verb = "run", state, startedUtc, completedUtc, message;
            public bool passed;
            public List<SuiteResult> suites = new List<SuiteResult>();
        }

        static CheckRegressionValidation()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += Update;
        }

        [MenuItem("Tools/检定系统/运行原流程回归")]
        public static void Run() => Begin("manual-" + Guid.NewGuid().ToString("N"));

        private static void Begin(string commandId)
        {
            if (SessionState.GetBool(PendingKey, false)) throw new InvalidOperationException("原流程回归已在运行。");
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先停止 Play 模式，再运行原流程回归。");
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) throw new InvalidOperationException("请等待脚本与资源导入完成。");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("当前场景有未保存改动；请保存后运行回归，避免测试切换场景时丢失改动。");
            SessionState.SetString(SetupKey, JsonUtility.ToJson(new SceneSnapshot { scenes = EditorSceneManager.GetSceneManagerSetup() }));
            SessionState.SetBool(Prefix + "Background", Application.runInBackground);
            SessionState.SetBool(Prefix + "Paused", EditorApplication.isPaused);
            SessionState.SetBool(Prefix + "Suppression", SessionState.GetBool("Light.GameFlow.SuppressForValidation", false));
            SessionState.SetFloat(Prefix + "TimeScale", Time.timeScale);
            SessionState.SetString(StartedKey, EditorApplication.timeSinceStartup.ToString("R", CultureInfo.InvariantCulture));
            SaveReport(new Report { id = commandId, state = "running", startedUtc = DateTime.UtcNow.ToString("O"), message = "正在验证果实交付与菜单存档流程。" });
            SessionState.SetBool(PendingKey, true);
            SessionState.SetInt(StageKey, FruitRunning);
            SessionState.SetString(StageUtcKey, DateTime.UtcNow.ToString("O"));
            Application.runInBackground = true;
            EditorApplication.isPaused = false;
            try { FruitQuestValidation.Run(); }
            catch (Exception exception)
            {
                AppendFailure("果实交付", "Validation/fruit-quest-results.json", exception.Message);
                SessionState.SetInt(StageKey, Finishing);
                Finish();
            }
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(PendingKey, false) || state != PlayModeStateChange.EnteredEditMode) return;
            int stage = SessionState.GetInt(StageKey, 0);
            if (stage == FruitRunning)
            {
                ReadSuite("果实交付", "Validation/fruit-quest-results.json");
                SessionState.SetInt(StageKey, GameFlowReady);
            }
            else if (stage == GameFlowRunning)
            {
                ReadSuite("菜单与存档", "Validation/menu-save-results.json");
                SessionState.SetInt(StageKey, Finishing);
            }
            // Let the existing runners finish their own EnteredEditMode cleanup first.
            deferTransition = true;
        }

        private static void Update()
        {
            if (SessionState.GetBool(PendingKey, false))
            {
                EditorApplication.QueuePlayerLoopUpdate();
                if (deferTransition) { deferTransition = false; return; }
                double started;
                if (double.TryParse(SessionState.GetString(StartedKey, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out started) &&
                    EditorApplication.timeSinceStartup - started > 60 && SessionState.GetInt(StageKey, 0) != Finishing)
                {
                    int stage = SessionState.GetInt(StageKey, 0);
                    AppendFailure(stage == FruitRunning ? "果实交付" : "菜单与存档",
                        stage == FruitRunning ? "Validation/fruit-quest-results.json" : "Validation/menu-save-results.json", "Combined regression exceeded 60 seconds.");
                    SessionState.SetInt(StageKey, Finishing);
                    if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode(); else Finish();
                    return;
                }
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
                int current = SessionState.GetInt(StageKey, 0);
                if (current == GameFlowReady)
                {
                    SessionState.SetInt(StageKey, GameFlowRunning);
                    SessionState.SetString(StageUtcKey, DateTime.UtcNow.ToString("O"));
                    EditorApplication.isPaused = false;
                    try { GameFlowValidation.Run(); }
                    catch (Exception exception)
                    {
                        AppendFailure("菜单与存档", "Validation/menu-save-results.json", exception.Message);
                        SessionState.SetInt(StageKey, Finishing); Finish();
                    }
                }
                else if (current == Finishing) Finish();
                return;
            }
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            nextPoll = EditorApplication.timeSinceStartup + .7;
            string path = ProjectPath("Validation/check-regression-command.json");
            if (!File.Exists(path)) return;
            Command command;
            try { command = JsonUtility.FromJson<Command>(File.ReadAllText(path)); } catch (Exception) { return; }
            if (command == null || string.IsNullOrWhiteSpace(command.id) || command.id == SessionState.GetString(LastCommandKey, "")) return;
            try
            {
                var previous = JsonUtility.FromJson<Report>(File.ReadAllText(OutputPath));
                if (previous != null && previous.id == command.id && previous.state != "running")
                { SessionState.SetString(LastCommandKey, command.id); return; }
            }
            catch (Exception) { }
            SessionState.SetString(LastCommandKey, command.id);
            if (command.verb != "run")
            {
                SaveReport(new Report { id = command.id, verb = command.verb, state = "rejected", completedUtc = DateTime.UtcNow.ToString("O"), message = "只支持 run。" });
                return;
            }
            try { Begin(command.id); }
            catch (Exception exception)
            {
                SaveReport(new Report { id = command.id, state = "rejected", completedUtc = DateTime.UtcNow.ToString("O"), message = exception.Message });
                Debug.LogWarning("[CheckRegression] " + exception.Message);
            }
        }

        private static void ReadSuite(string name, string path)
        {
            var result = new SuiteResult { name = name, reportPath = path };
            try
            {
                var source = JsonUtility.FromJson<ExistingReport>(File.ReadAllText(ProjectPath(path)));
                DateTime completed, stageStart;
                result.fresh = source != null && DateTime.TryParse(source.completedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out completed) &&
                    DateTime.TryParse(SessionState.GetString(StageUtcKey, ""), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out stageStart) && completed >= stageStart;
                result.checkCount = source?.checks?.Count ?? 0;
                result.completedUtc = source?.completedUtc;
                result.passed = result.fresh && source.passed && result.checkCount > 0 && source.checks.All(check => check != null && check.passed);
                if (!result.fresh) result.error = "The runner did not produce a newly completed report for this regression.";
                else if (!result.passed) result.error = "The existing runner reported failed checks.";
            }
            catch (Exception exception) { result.error = exception.Message; }
            var report = LoadReport(); report.suites.Add(result); SaveReport(report);
        }

        private static void AppendFailure(string name, string path, string error)
        {
            var report = LoadReport(); report.suites.Add(new SuiteResult { name = name, reportPath = path, error = error }); SaveReport(report);
        }

        private static void Finish()
        {
            if (!SessionState.GetBool(PendingKey, false) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var report = LoadReport();
            try
            {
                var setup = JsonUtility.FromJson<SceneSnapshot>(SessionState.GetString(SetupKey, ""));
                if (setup?.scenes != null && setup.scenes.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup.scenes);
            }
            catch (Exception exception)
            { report.suites.Add(new SuiteResult { name = "恢复原场景", error = exception.Message }); }
            finally
            {
                Application.runInBackground = SessionState.GetBool(Prefix + "Background", false);
                EditorApplication.isPaused = SessionState.GetBool(Prefix + "Paused", false);
                SessionState.SetBool("Light.GameFlow.SuppressForValidation", SessionState.GetBool(Prefix + "Suppression", false));
                Time.timeScale = SessionState.GetFloat(Prefix + "TimeScale", 1);
                SessionState.SetBool(PendingKey, false); SessionState.SetInt(StageKey, 0);
            }
            report.passed = report.suites.Count == 2 && report.suites.All(suite => suite.passed && suite.fresh);
            report.state = "complete"; report.completedUtc = DateTime.UtcNow.ToString("O");
            report.message = report.passed ? "果实交付与菜单存档回归通过，原场景已恢复。" : "原流程回归未通过；请查看各项报告。";
            SaveReport(report);
            Debug.Log("CHECK_REGRESSION_" + (report.passed ? "PASS" : "FAIL"));
        }

        private static Report LoadReport() => JsonUtility.FromJson<Report>(SessionState.GetString(ReportKey, "{}")) ?? new Report();
        private static void SaveReport(Report report)
        {
            string json = JsonUtility.ToJson(report, true); SessionState.SetString(ReportKey, json);
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath)); File.WriteAllText(OutputPath, json);
        }
        private static string ProjectPath(string path) => Path.GetFullPath(Path.Combine(Application.dataPath, "../" + path));
        private static string OutputPath => ProjectPath("Validation/check-regression-results.json");
    }
}

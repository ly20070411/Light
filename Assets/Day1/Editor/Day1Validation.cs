using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Emerge.Day1.Tests;
using Emerge.GameFlow;
using Emerge.Props;
using PixelPrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Emerge.Day1.Editor
{
    /// <summary>Only exposes the Day1 validation command; never accepts arbitrary editor code.</summary>
    [InitializeOnLoad]
    public static class Day1Validation
    {
        private const string Prefix = "Light.Day1.Validation.";
        private static double nextPoll;
        private static bool restoring;
        [Serializable] private sealed class Setup { public SceneSetup[] scenes; }
        [Serializable] private sealed class Command { public string id, verb; }
        [Serializable] private sealed class CommandResult
        { public string id, verb, state, message, completedUtc; public bool success; }

        static Day1Validation()
        {
            EditorApplication.playModeStateChanged += PlayStateChanged;
            EditorApplication.update += Update;
            if (EditorApplication.isPlaying && SessionState.GetBool(Prefix + "Pending", false) &&
                SessionState.GetBool(Prefix + "RuntimeStarted", false) && !SessionState.GetBool(Prefix + "Finished", false))
                EditorApplication.delayCall += InterruptedByCompilation;
        }

        [MenuItem("Tools/剧情/Day1/运行完整流程验证")]
        public static void Run() => Begin("manual-" + Guid.NewGuid().ToString("N"));

        private static void Begin(string commandId)
        {
            if (SessionState.GetBool(Prefix + "Pending", false) || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请停止 Play 模式后运行 Day1 验证。");
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("请等待资源导入与编译完成。");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("当前场景有未保存改动；请先保存，避免验证切换场景时丢失改动。");
            string scenePath = Day1SceneSetup.ScenePath;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null) throw new InvalidOperationException("尚未制作 Day1 场景。");

            SessionState.SetString(Prefix + "Setup", JsonUtility.ToJson(new Setup { scenes = EditorSceneManager.GetSceneManagerSetup() }));
            SessionState.SetBool(Prefix + "Background", Application.runInBackground);
            SessionState.SetBool(Prefix + "Paused", EditorApplication.isPaused);
            SessionState.SetBool(Prefix + "Suppression", SessionState.GetBool("Light.GameFlow.SuppressForValidation", false));
            SessionState.SetFloat(Prefix + "TimeScale", Time.timeScale);
            SessionState.SetString(Prefix + "Id", commandId);
            SessionState.SetString(Prefix + "Scene", scenePath);
            SessionState.SetString(Prefix + "Started", EditorApplication.timeSinceStartup.ToString("R", CultureInfo.InvariantCulture));
            SessionState.SetBool(Prefix + "Passed", false);
            SessionState.SetBool(Prefix + "Finished", false);
            SessionState.SetBool(Prefix + "RuntimeStarted", false);
            SessionState.SetBool(Prefix + "Pending", true);
            restoring = false;
            try
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var report = new Day1SelfTest.Report { scene = scenePath, unityVersion = Application.unityVersion };
                var flow = UnityEngine.Object.FindObjectOfType<Day1FlowController>();
                Add(report, "Day1 scene provides the real story controller", flow != null, scenePath);
                Add(report, "Day1 scene is available for new game and saved scene reload", EditorBuildSettings.scenes.Any(s => s.enabled && s.path == scenePath), scenePath);
                Add(report, "Day1 uses a two dimensional orthographic camera", Camera.main != null && Camera.main.orthographic && Mathf.Abs(Camera.main.transform.eulerAngles.x) < .01f && Mathf.Abs(Camera.main.transform.eulerAngles.y) < .01f,
                    "PixelRoom camera direction; no 2.5D tilt");
                var player = UnityEngine.Object.FindObjectOfType<PlayerMovement>();
                Add(report, "Player has dialogue, inventory and a stable save identity", player != null && player.GetComponent<PlayerInteractor>() != null && player.GetComponent<PropGameState>() != null && player.GetComponent<SaveIdentity>() != null,
                    "Existing player and save integration");
                var props = UnityEngine.Object.FindObjectsOfType<PropInstance>(true).Where(p => p.Definition != null).ToArray();
                Add(report, "Placed props have unique and stable save identities", props.Length > 0 && props.All(p => !string.IsNullOrWhiteSpace(p.InstanceId)) && props.Select(p => p.InstanceId).Distinct().Count() == props.Length,
                    props.Length + " placed props");
                if (flow != null)
                {
                    Add(report, "All planned interaction locations are present", Day1ContentBuilder.Keys.All(k => flow.Find(k) != null && flow.Find(k).Prop != null), string.Join(", ", Day1ContentBuilder.Keys));
                    Add(report, "Four collection and repair points use actual divination events", new[] { "Diagnosis", "Plant", "Repair", "Parts" }.All(k =>
                    { var check = flow.Find(k)?.Prop?.Definition?.checkEvent; return check != null && check.useDivination && check.Validate(out _); }), "Diagnosis / Plant / Repair / Parts");
                    var threat = flow.Find("Threat")?.Prop?.Definition?.battleEncounter;
                    var catalog = Day1ContentBuilder.LoadBattleCatalog();
                    Add(report, "The teaching encounter belongs to the local valid battle catalog", threat != null && catalog != null && catalog.Validate(out _) && catalog.Encounter(threat.id) == threat && threat.victoryFlag == "day1.threat_cleared",
                        "Real BattleController encounter lookup and victory flag");
                    Add(report, "Six teammates reference the existing placeholder character assets", new[] { "LinXi", "Hydrologist", "Geologist", "YangYinglong", "ContainmentResearcher", "Mechanic" }
                        .All(k => flow.Find(k)?.Prop?.Character != null && flow.Find(k).Prop.Character.mapSprite != null), "Existing CharacterDefinition references");
                }
                SessionState.SetString(Prefix + "Report", JsonUtility.ToJson(report));
                Day1SelfTest.WriteReport(report);
                SessionState.SetBool("Light.GameFlow.SuppressForValidation", false);
                Application.runInBackground = true;
                EditorApplication.isPaused = false;
                WriteCommandResult(commandId, "running", true, "正在真实 Play 模式验证 Day1，使用独立测试存档。");
                EditorApplication.EnterPlaymode();
            }
            catch (Exception) { Restore(); throw; }
        }

        private static void PlayStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Prefix + "Pending", false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.SetBool(Prefix + "RuntimeStarted", true);
                Application.runInBackground = true;
                EditorApplication.isPaused = false;
                Day1SelfTest.Completed -= Complete;
                Day1SelfTest.Completed += Complete;
                var runner = new GameObject("Day1 完整流程自动验证（临时）").AddComponent<Day1SelfTest>();
                runner.Initialize(SessionState.GetString(Prefix + "Report", "{}"), SessionState.GetString(Prefix + "Scene", ""));
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                restoring = true;
                EditorApplication.delayCall += Restore;
            }
        }

        private static void Complete(bool passed)
        {
            SessionState.SetBool(Prefix + "Passed", passed);
            SessionState.SetBool(Prefix + "Finished", true);
            Day1SelfTest.Completed -= Complete;
            EditorApplication.ExitPlaymode();
        }

        private static void InterruptedByCompilation()
        {
            if (!EditorApplication.isPlaying || !SessionState.GetBool(Prefix + "Pending", false) || SessionState.GetBool(Prefix + "Finished", false)) return;
            Day1SelfTest.Report report;
            try { report = JsonUtility.FromJson<Day1SelfTest.Report>(File.ReadAllText(ProjectPath("Validation/day1-results.json"))); }
            catch (Exception) { report = JsonUtility.FromJson<Day1SelfTest.Report>(SessionState.GetString(Prefix + "Report", "{}")); }
            Add(report, "Runtime validation remained in one uninterrupted Play session", false,
                "Script compilation reloaded the domain during Play and stopped the test coroutine. Wait for imports and compilation to finish, then rerun.");
            report.passed = false;
            report.completedUtc = DateTime.UtcNow.ToString("O");
            Day1SelfTest.WriteReport(report);
            SessionState.SetBool(Prefix + "Passed", false);
            SessionState.SetBool(Prefix + "Finished", true);
            Debug.LogWarning("DAY1_VALIDATION_INTERRUPTED_BY_COMPILATION");
            EditorApplication.ExitPlaymode();
        }

        private static void Update()
        {
            if (SessionState.GetBool(Prefix + "Pending", false))
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && !EditorApplication.isUpdating &&
                    (restoring || SessionState.GetBool(Prefix + "Finished", false))) { Restore(); return; }
                EditorApplication.QueuePlayerLoopUpdate();
                if (double.TryParse(SessionState.GetString(Prefix + "Started", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out double start) &&
                    EditorApplication.timeSinceStartup - start > 240)
                {
                    SessionState.SetBool(Prefix + "Passed", false);
                    restoring = true;
                    Debug.LogError("DAY1_VALIDATION_TIMEOUT");
                    if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode(); else Restore();
                }
                return;
            }
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            nextPoll = EditorApplication.timeSinceStartup + .7;
            string commandPath = ProjectPath("Validation/day1-command.json");
            if (!File.Exists(commandPath)) return;
            Command command;
            try { command = JsonUtility.FromJson<Command>(File.ReadAllText(commandPath)); } catch (Exception) { return; }
            if (command == null || string.IsNullOrWhiteSpace(command.id) || command.id == SessionState.GetString(Prefix + "LastCommand", "")) return;
            try
            {
                var prior = JsonUtility.FromJson<CommandResult>(File.ReadAllText(ProjectPath("Validation/day1-command-result.json")));
                if (prior != null && prior.id == command.id && prior.state != "running")
                { SessionState.SetString(Prefix + "LastCommand", command.id); return; }
            }
            catch (Exception) { }
            SessionState.SetString(Prefix + "LastCommand", command.id);
            if (command.verb != "validate") { WriteCommandResult(command.id, "rejected", false, "只支持 validate。"); return; }
            try { Begin(command.id); }
            catch (Exception exception) { WriteCommandResult(command.id, "rejected", false, exception.Message); Debug.LogWarning("[Day1Validation] " + exception.Message); }
        }

        private static void Restore()
        {
            if (!SessionState.GetBool(Prefix + "Pending", false) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            bool passed = SessionState.GetBool(Prefix + "Passed", false) && SessionState.GetBool(Prefix + "Finished", false);
            string error = "";
            try
            {
                var setup = JsonUtility.FromJson<Setup>(SessionState.GetString(Prefix + "Setup", ""));
                if (setup?.scenes != null && setup.scenes.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup.scenes);
            }
            catch (Exception exception) { passed = false; error = exception.Message; }
            finally
            {
                Day1SelfTest.Completed -= Complete;
                Application.runInBackground = SessionState.GetBool(Prefix + "Background", false);
                EditorApplication.isPaused = SessionState.GetBool(Prefix + "Paused", false);
                SessionState.SetBool("Light.GameFlow.SuppressForValidation", SessionState.GetBool(Prefix + "Suppression", false));
                Time.timeScale = SessionState.GetFloat(Prefix + "TimeScale", 1);
                SessionState.SetBool(Prefix + "Pending", false);
                restoring = false;
            }
            WriteCommandResult(SessionState.GetString(Prefix + "Id", ""), "complete", passed,
                !string.IsNullOrEmpty(error) ? error : passed ? "Day1 完整流程验证通过，原场景和编辑器状态已恢复。" : "Day1 验证未完成或未通过，请查看 day1-results.json；原场景已恢复。");
        }

        private static void Add(Day1SelfTest.Report report, string name, bool passed, string observed)
            => report.checks.Add(new Day1SelfTest.CheckResult { name = name, passed = passed, observed = observed });
        private static void WriteCommandResult(string id, string state, bool success, string message)
        {
            string path = ProjectPath("Validation/day1-command-result.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(new CommandResult { id = id, verb = "validate", state = state, success = success,
                message = message, completedUtc = DateTime.UtcNow.ToString("O") }, true));
        }
        private static string ProjectPath(string relative) => Path.GetFullPath(Path.Combine(Application.dataPath, "../" + relative));
    }
}

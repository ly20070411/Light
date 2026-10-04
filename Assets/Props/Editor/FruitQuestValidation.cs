using System;
using System.Collections.Generic;
using System.IO;
using Emerge.PixelMap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Emerge.Props.Editor
{
    [InitializeOnLoad]
    public static class FruitQuestValidation
    {
        public const string FruitPath = "Assets/Props/Library/Definitions/Fruit.asset";
        public const string GuidePath = "Assets/Props/Library/Definitions/Guide.asset";
        private const string PendingKey = "Emerge.Props.FruitQuest.Validation.Pending";
        private const string ReportKey = "Emerge.Props.FruitQuest.Validation.Report";
        private static double started;

        static FruitQuestValidation() { EditorApplication.playModeStateChanged += PlayStateChanged; }

        [MenuItem("Tools/道具系统/验证果实交付流程")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            // Preserve all currently edited scenes before changing the active scene.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isDirty) continue;
                if (string.IsNullOrEmpty(scene.path))
                    throw new InvalidOperationException("请先为未命名场景设置保存路径，再运行果实验证。");
                if (!EditorSceneManager.SaveScene(scene))
                    throw new InvalidOperationException("无法保存场景：" + scene.path);
            }
            EditorSceneManager.OpenScene(PropBootstrap.DemoScenePath);
            var report = new FruitQuestSelfTest.Report { unityVersion = Application.unityVersion, scene = SceneManager.GetActiveScene().name };
            var fruit = AssetDatabase.LoadAssetAtPath<PropDefinition>(FruitPath);
            var guide = AssetDatabase.LoadAssetAtPath<PropDefinition>(GuidePath);
            var library = AssetDatabase.LoadAssetAtPath<PropLibrary>(PropAssetFactory.LibraryPath);
            var mapLibrary = AssetDatabase.LoadAssetAtPath<MapBlockLibrary>("Assets/PixelMap/Library/DefaultBlockLibrary.asset");
            Add(report, "Fruit and guide are persistent editable definitions", fruit != null && guide != null
                && AssetDatabase.GetAssetPath(fruit) == FruitPath && AssetDatabase.GetAssetPath(guide) == GuidePath,
                FruitPath + " / " + GuidePath);
            Add(report, "Both editors share a library containing fruit and guide", library != null && mapLibrary != null
                && mapLibrary.PropLibrary == library && Contains(library, fruit) && Contains(library, guide),
                "Default map library links to the same serialized prop library");
            Add(report, "Fruit has the edited pickup quantity, inventory key and sprite", fruit != null
                && fruit.DisplayName == "果实" && fruit.InventoryKey == "fruit" && fruit.pickupAmount == 1
                && fruit.HasAction(PropActions.Pickup) && fruit.hideAfterPickup && fruit.sprite != null,
                "Fruit quantity=1; inventory key=fruit; pickup hides the world item");
            var handover = guide == null ? null : guide.itemHandover;
            Add(report, "Guide stores the configured fruit handover", handover != null && handover.enabled
                && handover.itemKey == "fruit" && handover.amount == 1 && handover.completionFlag == "guide_fruit_given"
                && !string.IsNullOrWhiteSpace(handover.acceptLabel) && !string.IsNullOrWhiteSpace(handover.declineLabel),
                "Item key, amount, completion flag and two choice labels are serialized");
            Add(report, "All five guide dialogue branches are editable", handover != null
                && ValidLines(handover.offerDialogue) && ValidLines(handover.acceptedDialogue)
                && ValidLines(handover.declinedDialogue) && ValidLines(handover.missingItemDialogue)
                && ValidLines(handover.completedDialogue), "Offer / accepted / declined / missing / completed");
            bool fruitPlaced = false, guidePlaced = false;
            foreach (var instance in UnityEngine.Object.FindObjectsOfType<PropInstance>(true))
            {
                if (instance.Definition == fruit && fruit != null) fruitPlaced = true;
                if (instance.Definition == guide && guide != null) guidePlaced = true;
            }
            Add(report, "The playable demo places the same fruit and guide assets", fruitPlaced && guidePlaced,
                "Scene instances reference editable definitions");
            var player = UnityEngine.Object.FindObjectOfType<PixelPrototype.PlayerMovement>();
            var interactor = player == null ? null : player.GetComponent<PlayerInteractor>();
            Add(report, "Demo player shares the editable prop library and backpack", interactor != null
                && interactor.propLibrary == library && interactor.State != null, "E interaction / I backpack");
            SessionState.SetString(ReportKey, JsonUtility.ToJson(report));
            WriteReport(report);
            SessionState.SetBool(PendingKey, true);
            Application.runInBackground = true;
            Time.timeScale = 1f;
            SessionState.SetBool("Light.GameFlow.SuppressForValidation", true);
            EditorApplication.EnterPlaymode();
        }

        private static bool Contains(PropLibrary library, PropDefinition definition)
        {
            if (library == null || definition == null) return false;
            foreach (var entry in library.Props) if (entry == definition) return true;
            return false;
        }

        private static bool ValidLines(List<PropDialogueLine> lines)
        {
            if (lines == null || lines.Count == 0) return false;
            foreach (var line in lines) if (line == null || string.IsNullOrWhiteSpace(line.text)) return false;
            return true;
        }

        private static void PlayStateChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(PendingKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                started = EditorApplication.timeSinceStartup;
                Application.runInBackground = true;
                Time.timeScale = 1f;
                FruitQuestSelfTest.Completed += Complete;
                var runner = new GameObject("果实交付流程自动验证").AddComponent<FruitQuestSelfTest>();
                runner.Initialize(AssetDatabase.LoadAssetAtPath<PropDefinition>(FruitPath),
                    AssetDatabase.LoadAssetAtPath<PropDefinition>(GuidePath), SessionState.GetString(ReportKey, ""));
                EditorApplication.update += Timeout;
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool("Light.GameFlow.SuppressForValidation", false);
                SessionState.SetBool(PendingKey, false);
                EditorApplication.update -= Timeout;
                FruitQuestSelfTest.Completed -= Complete;
                PropEditorWindow.Open();
                Emerge.PixelMap.Editor.PixelMapEditorWindow.OpenPropsTab();
            }
        }

        private static void Complete(bool passed)
        {
            FruitQuestSelfTest.Completed -= Complete;
            EditorApplication.update -= Timeout;
            EditorApplication.ExitPlaymode();
        }

        private static void Timeout()
        {
            EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup - started < 60f) return;
            EditorApplication.update -= Timeout;
            FruitQuestSelfTest.Completed -= Complete;
            var report = JsonUtility.FromJson<FruitQuestSelfTest.Report>(SessionState.GetString(ReportKey, "{}"));
            Add(report, "Fruit quest runtime verification finished", false, "Timed out after 60 seconds");
            report.completedUtc = DateTime.UtcNow.ToString("O");
            report.passed = false;
            WriteReport(report);
            Debug.LogError("FRUIT_QUEST_VALIDATION_TIMEOUT");
            EditorApplication.ExitPlaymode();
        }

        private static void Add(FruitQuestSelfTest.Report report, string name, bool passed, string observed)
        { report.checks.Add(new FruitQuestSelfTest.Check { name = name, passed = passed, observed = observed }); }

        private static void WriteReport(FruitQuestSelfTest.Report report)
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Validation/fruit-quest-results.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
        }
    }
}

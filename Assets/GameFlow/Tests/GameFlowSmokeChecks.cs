#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emerge.Props;
using PixelPrototype;
using UnityEngine;
using UnityEngine.UI;

namespace Emerge.GameFlow.Tests
{
    /// <summary>Runs the ported UI and persistence against the real fruit/guide scene in isolated storage.</summary>
    public sealed class GameFlowSmokeChecks : MonoBehaviour
    {
        [Serializable] public sealed class CheckResult
        { public string name; public bool passed; public string observed; }
        [Serializable] public sealed class Report
        {
            public string unityVersion, scene, completedUtc, testSaveDirectory;
            public bool passed;
            public List<CheckResult> checks = new List<CheckResult>();
        }
        public static event Action<bool> Completed;
        public int passed, failed;
        public bool completed;
        public string report;
        private Report results;
        private GameSessionController session;
        private string testDirectory, originalDirectory;
        private bool originalBackground;
        private float originalAutoInterval;
        private bool running;
        private string initialReport;

        public void Initialize(string editorReport) { initialReport = editorReport; }

        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            yield return new WaitUntil(() => GameSessionController.Instance != null && GameSessionController.Instance.IsReady);
            RunChecks();
        }

        [UnityEditor.MenuItem("Tools/菜单与存档/运行 Play 模式自检（主菜单中）")]
        private static void FromMenu()
        {
            if (!Application.isPlaying || GameSessionController.Instance == null ||
                GameSessionController.Instance.Phase != GameSessionPhase.MainMenu)
            { Debug.LogWarning("[GameFlowChecks] 请在 Play 模式主菜单中运行。"); return; }
            new GameObject("Game Flow Smoke Checks").AddComponent<GameFlowSmokeChecks>();
        }

        public void RunChecks()
        {
            if (running) return;
            session = GameSessionController.Instance;
            if (session == null || session.Phase != GameSessionPhase.MainMenu)
                throw new InvalidOperationException("Menu/save verification must start in the main menu.");
            running = true;
            results = string.IsNullOrEmpty(initialReport) ? new Report() : JsonUtility.FromJson<Report>(initialReport);
            results.unityVersion = Application.unityVersion;
            results.scene = session.gameScenePath;
            originalDirectory = session.SaveDirectory;
            originalAutoInterval = session.autoSaveInterval;
            originalBackground = Application.runInBackground;
            Application.runInBackground = true;
            testDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/MenuSaveChecks_" + Guid.NewGuid().ToString("N")));
            string storage = Path.Combine(testDirectory, "Session");
            results.testSaveDirectory = storage;
            session.UseTestSaveDirectory(storage);
            session.autoSaveInterval = 1;
            StartCoroutine(GuardedRun());
        }

        private IEnumerator GuardedRun()
        {
            var routine = Run();
            while (true)
            {
                bool next;
                object waiting;
                try { next = routine.MoveNext(); waiting = next ? routine.Current : null; }
                catch (Exception exception)
                { Check(false, "Runtime verification completed without exception", exception.ToString()); break; }
                if (!next) break;
                yield return waiting;
            }
            Finish();
        }

        private void Check(bool condition, string label, string observed = "")
        {
            if (condition) passed++; else failed++;
            report += (condition ? "PASS " : "FAIL ") + label + "\n";
            results.checks.Add(new CheckResult { name = label, passed = condition, observed = observed });
            Debug.Log("MENU_SAVE_CHECK " + (condition ? "PASS " : "FAIL ") + label + ": " + observed);
            // Preserve completed checks if a later scene load hangs and the editor watchdog exits play mode.
            string progressPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Validation/menu-save-results.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(progressPath));
            File.WriteAllText(progressPath, JsonUtility.ToJson(results, true));
        }
        private Button FindButton(string name) => session.GetComponentsInChildren<Button>(true).First(button => button.name == name);
        private void Click(string name)
        {
            var button = FindButton(name);
            bool available = button.interactable && button.gameObject.activeInHierarchy;
            Check(available, "UI button available: " + name);
            if (!available) throw new InvalidOperationException("Button unavailable: " + name);
            button.onClick.Invoke();
        }
        private PlayerInteractor Player => FindObjectsOfType<PlayerInteractor>(true).First(actor => actor.GetComponent<PlayerMovement>() != null);
        private PropInstance Fruit => FindObjectsOfType<PropInstance>(true).First(item => item.Definition != null && item.Definition.InventoryKey == "fruit" && item.Definition.HasAction(PropActions.Pickup));
        private PropInstance Guide => FindObjectsOfType<PropInstance>(true).First(item => item.Definition != null && item.Definition.itemHandover != null && item.Definition.itemHandover.enabled && item.Definition.itemHandover.itemKey == "fruit");
        private void PreparePlayer()
        {
            Player.keyboardInput = false;
            Player.GetComponent<PlayerMovement>().SetScriptedInput(Vector2.zero);
            var body = Player.GetComponent<Rigidbody2D>();
            if (body != null) body.velocity = Vector2.zero;
        }
        private void MovePlayer(Vector3 position)
        {
            Player.transform.position = position;
            var body = Player.GetComponent<Rigidbody2D>();
            if (body != null) { body.position = position; body.velocity = Vector2.zero; }
            Physics2D.SyncTransforms();
        }
        private bool FinishDialogue(List<PropDialogueLine> expected)
        {
            bool matched = expected != null && expected.Count > 0;
            if (expected != null) foreach (var line in expected)
            {
                matched &= line != null && Player.IsInDialogue && !Player.IsAwaitingChoice &&
                    Player.CurrentDialogueText == line.text && Player.CurrentDialogueSpeaker == line.speaker;
                Player.AdvanceDialogue();
            }
            return matched && !Player.IsInDialogue;
        }
        private bool OfferDialogue(List<PropDialogueLine> expected)
        {
            bool matched = expected != null && expected.Count > 0;
            if (expected != null) foreach (var line in expected)
            {
                matched &= line != null && Player.IsInDialogue && Player.CurrentDialogueText == line.text;
                Player.AdvanceDialogue();
            }
            return matched && Player.IsAwaitingChoice;
        }

        private IEnumerator Run()
        {
            Check(session.Phase == GameSessionPhase.MainMenu && Time.timeScale == 0 && !GameSessionController.GameplayInputAllowed,
                "Startup opens a paused main menu");
            Check(!FindButton("Continue Game").interactable, "Continue is disabled without saves");
            Check(!Directory.Exists(session.SaveDirectory), "Verification uses a new isolated save directory", session.SaveDirectory);

            var store = new GameSaveStore(Path.Combine(testDirectory, "Storage"));
            var data = new GameSaveData { version = 2, savedUtcTicks = DateTime.UtcNow.Ticks, scenePath = session.gameScenePath,
                playerId = "test-player", reason = "storage fixture", actors = new List<SavedActor> {
                    new SavedActor { id = "test-player", position = new Vector3(1, 2, 0), active = true,
                        propState = new PropGameState.Snapshot { version = 1,
                            inventory = new List<PropGameState.InventoryEntry> { new PropGameState.InventoryEntry { key = "fruit", displayName = "果实", amount = 1 } },
                            flags = new List<string> { "fixture_flag" }, consumedInstances = new List<string> { "fixture_fruit" } } } },
                props = new List<SavedProp> { new SavedProp { instanceId = "fixture_fruit", definitionId = "fixture-fruit-definition",
                    position = new Vector3(2, 3, 0), rotationZ = 0, localScale = Vector3.one, active = false } } };
            Check(!store.TryLatest(out _, out _, out _), "Empty storage has no continue checkpoint");
            Check(store.TryWrite(SaveSlot.Manual, data, out string writeError), "Manual checksum save is written", writeError);
            Check(store.TryRead(SaveSlot.Manual, out var first, out _) && first.actors[0].propState.inventory[0].amount == 1 && !first.props[0].active,
                "Checksummed JSON roundtrip includes backpack and hidden world prop");
            data.savedUtcTicks++; data.actors[0].propState.inventory[0].amount = 2;
            Check(store.TryWrite(SaveSlot.Manual, data, out _) && File.Exists(store.SlotPath(SaveSlot.Manual) + ".bak"),
                "Atomic replacement keeps a previous backup");
            Check(!File.Exists(store.SlotPath(SaveSlot.Manual) + ".tmp"), "Atomic commit leaves no temporary save");
            File.WriteAllText(store.SlotPath(SaveSlot.Manual), "{corrupt}");
            Check(store.TryRead(SaveSlot.Manual, out var recovered, out string recoveryMessage) && recovered.actors[0].propState.inventory[0].amount == 1 && !string.IsNullOrEmpty(recoveryMessage),
                "Corrupt primary recovers the validated backup", recoveryMessage);
            string corruptedContents = File.ReadAllText(store.SlotPath(SaveSlot.Manual));
            data.actors[0].position = new Vector3(float.NaN, 0, 0);
            Check(!store.TryWrite(SaveSlot.Manual, data, out _) && File.ReadAllText(store.SlotPath(SaveSlot.Manual)) == corruptedContents,
                "Nonfinite actor position cannot overwrite existing storage");
            data.actors[0].position = Vector3.zero;
            data.actors[0].propState.inventory[0].amount = -1;
            Check(!store.TryWrite(SaveSlot.Manual, data, out _), "Invalid backpack quantities are rejected");
            data.actors[0].propState.inventory[0].amount = 1;
            data.props[0].localScale = new Vector3(float.PositiveInfinity, 1, 1);
            Check(!store.TryWrite(SaveSlot.Manual, data, out _), "Nonfinite prop transform is rejected");
            data.props[0].localScale = Vector3.one;
            data.props.Add(data.props[0]);
            Check(!store.TryWrite(SaveSlot.Manual, data, out _), "Duplicate world prop identities are rejected");
            data.props.RemoveAt(1); data.savedUtcTicks += 100;
            Check(store.TryWrite(SaveSlot.Auto, data, out _) && store.TryLatest(out _, out var latestSlot, out _) && latestSlot == SaveSlot.Auto,
                "Continue chooses the newest validated slot");

            Click("New Game");
            yield return new WaitUntil(() => session.Phase == GameSessionPhase.Playing);
            PreparePlayer();
            Check(Time.timeScale == 1 && GameSessionController.GameplayInputAllowed, "New game opens the playable prop scene");
            Check(session.Store.TryRead(SaveSlot.Auto, out var initial, out _) && initial.reason == "新游戏初始存档", "New game immediately creates its initial automatic checkpoint");
            Vector3 spawn = Player.transform.position;
            string fruitId = Fruit.InstanceId;
            string guideId = Guide.InstanceId;
            Check(Player.State.Count("fruit") == 0 && !Player.State.HasFlag("guide_fruit_given") && Fruit.gameObject.activeSelf,
                "New game starts with an empty backpack and available fruit");
            MovePlayer(Fruit.transform.position + new Vector3(.1f, 0, 0));
            Click("Settings Button");
            Check(session.Phase == GameSessionPhase.Settings && Time.timeScale == 0 && !GameSessionController.GameplayInputAllowed,
                "Settings pauses gameplay and disables game input");
            Check(!Fruit.Interact(Player) && !Player.TryInteractNearest() && Player.State.Count("fruit") == 0 && Fruit.gameObject.activeSelf,
                "Paused gameplay rejects prop interaction and pickup");
            Click("Resume Game");
            Check(Fruit.Interact(Player) && Player.State.Count("fruit") == 1 && !Fruit.gameObject.activeSelf && Player.State.IsConsumed(fruitId),
                "Actual fruit pickup fills backpack and hides its stable world instance");
            Vector3 checkpointPosition = spawn + new Vector3(.6f, .4f, 0);
            MovePlayer(checkpointPosition);
            Click("Settings Button");
            Click("Save Game");
            Check(session.Store.TryRead(SaveSlot.Manual, out var checkpoint, out _) &&
                checkpoint.actors.Find(actor => actor.id == checkpoint.playerId).propState.inventory.Any(item => item.key == "fruit" && item.amount == 1) &&
                checkpoint.props.Any(prop => prop.instanceId == fruitId && !prop.active), "UI manual save captures backpack and picked fruit visibility");
            Click("Save And Return");
            Check(session.Phase == GameSessionPhase.MainMenu && Time.timeScale == 0, "Save-and-return opens the paused main menu");
            Check(FindButton("Continue Game").interactable, "Saved progress enables Continue");
            Click("Continue Game");
            yield return new WaitUntil(() => session.Phase == GameSessionPhase.Playing);
            PreparePlayer();
            Check(Vector3.Distance(Player.transform.position, checkpointPosition) < .01f, "Continue restores the player position");
            Check(Player.State.Count("fruit") == 1 && Player.State.IsConsumed(fruitId) && Fruit.InstanceId == fruitId && !Fruit.gameObject.activeSelf,
                "Continue restores backpack fruit and keeps the world fruit hidden");
            Check(Guide.InstanceId == guideId, "Scene reload retains persistent guide identity");
            MovePlayer(Guide.transform.position + new Vector3(.1f, 0, 0));
            var handover = Guide.Definition.itemHandover;
            Check(Guide.Interact(Player) && OfferDialogue(handover.offerDialogue), "Restored fruit unlocks the real guide handover choice");
            Check(Player.ChooseDialogueOption(false) && FinishDialogue(handover.declinedDialogue) && Player.State.Count("fruit") == 1,
                "Declining the handover uses its edited dialogue and preserves fruit");
            yield return new WaitForSecondsRealtime(Mathf.Max(.35f, Guide.Definition.cooldown + .05f));
            Check(Guide.Interact(Player) && OfferDialogue(handover.offerDialogue) && Player.ChooseDialogueOption(true) && FinishDialogue(handover.acceptedDialogue) &&
                Player.State.Count("fruit") == 0 && Player.State.HasFlag("guide_fruit_given"), "Accepting and completing dialogue consumes fruit and saves quest completion");
            Click("Settings Button"); Click("Save And Return"); Click("Continue Game");
            yield return new WaitUntil(() => session.Phase == GameSessionPhase.Playing);
            PreparePlayer();
            Check(Player.State.Count("fruit") == 0 && Player.State.HasFlag("guide_fruit_given") && !Fruit.gameObject.activeSelf,
                "Save-and-continue persists delivered fruit and completion flag");
            MovePlayer(Guide.transform.position + new Vector3(.1f, 0, 0));
            int repeatedRewards = 0;
            Guide.onInteracted.AddListener(actor => repeatedRewards++);
            Check(Guide.Interact(Player) && FinishDialogue(Guide.Definition.itemHandover.completedDialogue) && repeatedRewards == 0 && Player.State.Count("fruit") == 0,
                "Restored completion opens the post-delivery dialogue without repeating rewards");

            yield return new WaitForSecondsRealtime(1.3f);
            Check(session.Store.TryRead(SaveSlot.Auto, out var timed, out _) && timed.reason == "定时自动存档", "Periodic automatic checkpoint runs");
            session.SendMessage("OnApplicationFocus", false);
            Check(session.Store.TryRead(SaveSlot.Auto, out var focusSave, out _) && focusSave.reason == "失去焦点自动存档", "Focus loss writes an automatic checkpoint");
            session.SendMessage("OnApplicationPause", true);
            Check(session.Store.TryRead(SaveSlot.Auto, out var pauseSave, out _) && pauseSave.reason == "暂停应用自动存档", "Application suspension writes an automatic checkpoint");
            session.SendMessage("OnApplicationQuit");
            Check(session.Store.TryRead(SaveSlot.Auto, out var quitSave, out _) && quitSave.reason == "关闭应用自动存档", "Application quit callback writes an automatic checkpoint");
            Click("Settings Button"); Click("Save And Return"); Click("Load Saves"); Click("Load Auto");
            yield return new WaitUntil(() => session.Phase == GameSessionPhase.Playing);
            PreparePlayer();
            Check(Player.State.HasFlag("guide_fruit_given") && !Fruit.gameObject.activeSelf, "Automatic save selection restores quest and world state");
            Click("Settings Button"); Click("Save And Return"); Click("Load Saves"); Click("Load Manual");
            yield return new WaitUntil(() => session.Phase == GameSessionPhase.Playing);
            PreparePlayer();
            Check(Player.State.HasFlag("guide_fruit_given") && !Fruit.gameObject.activeSelf, "Manual save selection restores quest and world state");
            Click("Settings Button"); Click("Save And Return");
            string manualContents = File.ReadAllText(session.Store.SlotPath(SaveSlot.Manual));
            Click("New Game");
            Check(session.GetComponent<GameMenuUI>().HasSecondaryPage && session.Phase == GameSessionPhase.MainMenu, "Existing progress requires new-game confirmation");
            Click("Cancel New Game");
            Check(session.Phase == GameSessionPhase.MainMenu && !session.GetComponent<GameMenuUI>().HasSecondaryPage, "Cancel preserves the existing save selection");
            Click("New Game"); Click("Confirm New Game");
            yield return new WaitUntil(() => session.Phase == GameSessionPhase.Playing);
            PreparePlayer();
            Check(Player.State.Count("fruit") == 0 && !Player.State.HasFlag("guide_fruit_given") && !Player.State.IsConsumed(fruitId) && Fruit.gameObject.activeSelf &&
                Vector3.Distance(Player.transform.position, spawn) < .01f, "New game resets backpack, quest, spawn and world fruit");
            Check(File.ReadAllText(session.Store.SlotPath(SaveSlot.Manual)) == manualContents, "New game preserves the prior manual save");
            Check(FindObjectsOfType<GameSessionController>(true).Length == 1, "Repeated scene loads retain exactly one persistent controller");
            Click("Settings Button"); Click("Save And Return");

            string blockedPath = Path.Combine(testDirectory, "BlockedSaveDirectory");
            File.WriteAllText(blockedPath, "Owned test file blocks a directory for save-error verification.");
            session.UseTestSaveDirectory(blockedPath);
            session.BeginNewGame();
            yield return new WaitUntil(() => session.Phase == GameSessionPhase.Playing);
            PreparePlayer();
            Click("Settings Button"); Click("Save And Return");
            Check(session.Phase == GameSessionPhase.Settings && session.LastMessage.Contains("保存失败"), "Failed save keeps the paused game and its progress");
            Click("Resume Game");
            Check(session.Phase == GameSessionPhase.Playing, "Game remains resumable after save failure");
            File.Delete(blockedPath);
            Click("Settings Button"); Click("Save And Return");
            Check(session.Phase == GameSessionPhase.MainMenu, "Saving recovers after storage obstruction is removed");
            session.autoSaveInterval = originalAutoInterval;
            session.UseTestSaveDirectory(originalDirectory);
        }

        private void Finish()
        {
            completed = true;
            results.passed = results.checks.Count > 0 && results.checks.TrueForAll(check => check.passed);
            results.completedUtc = DateTime.UtcNow.ToString("O");
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Validation/menu-save-results.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(results, true));
            Debug.Log("MENU_SAVE_VALIDATION_" + (results.passed ? "PASS" : "FAIL") + " checks=" + results.checks.Count);
            Application.runInBackground = originalBackground;
            Completed?.Invoke(results.passed);
        }
        private void OnDestroy() { if (running) Application.runInBackground = originalBackground; }
    }
}
#endif

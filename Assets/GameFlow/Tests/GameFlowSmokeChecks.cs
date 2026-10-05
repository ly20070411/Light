#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emerge.Props;
using Emerge.Checks;
using PixelPrototype;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

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
            // Keep progress in the Console; repeatedly truncating a report open in a file reader can fail on Windows.
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
        private void CompleteAllocation(int[] points)
        {
            Check(session.Phase == GameSessionPhase.CharacterCreation && session.AllocationRemaining == 8,
                "New character starts with eight unspent points");
            for (int i = 0; i < points.Length; i++)
                for (int point = 0; point < points[i]; point++) FindButton("Attribute Plus " + (CheckBehavior)i).onClick.Invoke();
            Check(session.AllocationRemaining == 0 && FindButton("Confirm Attributes").interactable,
                "A complete allocation enables the start-adventure button");
            Click("Confirm Attributes");
        }
        private bool AttributesMatch(ActorCheckAttributes attributes, int[] expected)
            => attributes != null && Enumerable.Range(0, SixKinAttributes.Count).All(i => attributes.Get((CheckBehavior)i) == expected[i]);
        private IEnumerator CaptureCreation(string filename)
        {
            if (Application.isBatchMode) yield break;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Validation/" + filename));
            DateTime requested = DateTime.UtcNow;
            yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(path);
            float until = Time.realtimeSinceStartup + 5;
            while ((!File.Exists(path) || File.GetLastWriteTimeUtc(path) < requested || new FileInfo(path).Length == 0) && Time.realtimeSinceStartup < until)
                yield return null;
            Check(File.Exists(path) && File.GetLastWriteTimeUtc(path) >= requested && new FileInfo(path).Length > 0,
                "Character creation screenshot completed: " + filename);
        }
        private IEnumerator SettingsReallocationChecks(int[] originalPoints)
        {
            var checks = Player.GetComponent<CheckActorState>();
            var definition = ScriptableObject.CreateInstance<CheckEventDefinition>();
            definition.eventId = "settings-reallocation-frozen"; definition.useDivination = false;
            definition.options.Add(new CheckOptionDefinition { id = "create", label = "保持旧检定基础值", behavior = CheckBehavior.Offspring, targetValue = 1,
                success = new CheckOutcomeDefinition(), failure = new CheckOutcomeDefinition() });
            var pending = checks.GetOrPrepare(definition);
            string pendingJson = JsonUtility.ToJson(pending), progress = Player.State.CaptureJson();
            int contamination = checks.Contamination; var position = Player.transform.position;
            var manualBytes = File.ReadAllBytes(session.Store.SlotPath(SaveSlot.Manual));
            var autoBytes = File.ReadAllBytes(session.Store.SlotPath(SaveSlot.Auto));
            Check(session.CanChangeAttributes && FindButton("Reallocate Attributes").interactable, "Exploration settings exposes reallocation");
            var capture = CaptureCreation("settings-reallocation-entry-preview.png"); while (capture.MoveNext()) yield return capture.Current;
            Click("Reallocate Attributes");
            Check(session.IsChangingAttributes && session.Phase == GameSessionPhase.CharacterCreation && session.AllocationRemaining == 0 && Time.timeScale == 0 &&
                Enumerable.Range(0, SixKinAttributes.Count).All(i => session.AllocatedPoints((CheckBehavior)i) == originalPoints[i]),
                "Settings reallocation starts from the live eight-point build and keeps gameplay paused");
            Check(FindButton("Cancel Character Creation").GetComponentInChildren<Text>().text.Contains("返回设置") &&
                FindButton("Confirm Attributes").GetComponentInChildren<Text>().text.Contains("自动保存"), "Reallocation labels distinguish applying changes and returning to settings");
            Click("Attribute Minus Officer");
            Check(!session.ConfirmCharacterCreation() && !FindButton("Confirm Attributes").interactable && AttributesMatch(checks.attributes, originalPoints),
                "Unspent drafts cannot apply or mutate the live character");
            Check(!session.SaveManual() && !session.SaveAutomatic("draft-must-not-save"), "Draft allocation cannot be persisted by unrelated save actions");
            Click("Cancel Character Creation");
            Check(session.Phase == GameSessionPhase.Settings && !session.IsChangingAttributes && Time.timeScale == 0 && AttributesMatch(checks.attributes, originalPoints) &&
                File.ReadAllBytes(session.Store.SlotPath(SaveSlot.Manual)).SequenceEqual(manualBytes) && File.ReadAllBytes(session.Store.SlotPath(SaveSlot.Auto)).SequenceEqual(autoBytes),
                "Cancelling reallocation returns to paused settings and preserves both slots byte for byte");

            int[] changedPoints = { 1, 0, 4, 2, 1 };
            Click("Reallocate Attributes"); Click("Reset Attributes");
            for (int i = 0; i < changedPoints.Length; i++)
                for (int point = 0; point < changedPoints[i]; point++) session.AdjustAttribute((CheckBehavior)i, 1);
            Check(session.AllocationRemaining == 0 && session.GetComponent<GameMenuUI>().AllocationWarningText.Contains("子孙"), "Reallocation retains budget limits and zero-family warnings");
            capture = CaptureCreation("settings-reallocation-preview.png"); while (capture.MoveNext()) yield return capture.Current;
            string blockedTemporary = session.Store.SlotPath(SaveSlot.Auto) + ".tmp";
            Directory.CreateDirectory(blockedTemporary);
            try
            {
                Check(!session.ConfirmCharacterCreation() && session.IsChangingAttributes && session.Phase == GameSessionPhase.CharacterCreation &&
                    AttributesMatch(checks.attributes, originalPoints) && session.LastMessage.Contains("保存失败") &&
                    session.GetComponentsInChildren<Text>().Any(text => text.text.Contains("保存失败")),
                    "Save failure preserves the live build and completed draft and displays a retryable error");
                Check(File.ReadAllBytes(session.Store.SlotPath(SaveSlot.Auto)).SequenceEqual(autoBytes), "Failed reallocation preserves the valid automatic checkpoint");
            }
            finally { Directory.Delete(blockedTemporary); }
            Click("Confirm Attributes");
            Check(session.Phase == GameSessionPhase.Settings && !session.IsChangingAttributes && Time.timeScale == 0 && AttributesMatch(checks.attributes, changedPoints) &&
                !session.ConfirmCharacterCreation(), "Confirmed reallocation applies once and returns to paused settings");
            Check(Player.State.CaptureJson() == progress && Player.transform.position == position && checks.Contamination == contamination &&
                ReferenceEquals(checks.Sessions.First(item => item.sessionId == pending.sessionId), pending) && JsonUtility.ToJson(pending) == pendingJson,
                "Reallocation preserves inventory, plot, position, contamination and the existing live check session");
            Check(session.Store.TryRead(SaveSlot.Auto, out var saved, out _) && saved.reason == "角色重新加点" &&
                AttributesMatch(saved.actors.Find(actor => actor.id == saved.playerId).checkState.attributes, changedPoints) &&
                File.ReadAllBytes(session.Store.SlotPath(SaveSlot.Manual)).SequenceEqual(manualBytes), "Reallocation saves the new build automatically while keeping the manual checkpoint");
            Click("Resume Game");
            Check(checks.TryResolve(definition, pending, "create", out var frozenResult, out _) && frozenResult.baseValue == originalPoints[1],
                "A check already in progress keeps its original point bases after reallocation");
            var fresh = ScriptableObject.CreateInstance<CheckEventDefinition>(); fresh.eventId = "settings-reallocation-fresh"; fresh.useDivination = false;
            fresh.options.Add(new CheckOptionDefinition { id = "create", label = "新检定", behavior = CheckBehavior.Offspring, targetValue = 1,
                success = new CheckOutcomeDefinition(), failure = new CheckOutcomeDefinition() });
            Check(checks.TryResolve(fresh, checks.GetOrPrepare(fresh), "create", out var freshResult, out _) && freshResult.baseValue == 0,
                "New checks use the changed point bases");
            var catalog = Resources.Load<Emerge.Battle.BattleCatalog>(Emerge.Battle.BattleCatalog.ResourcePath);
            var battle = new Emerge.Battle.BattleEngine(catalog, Player.State) { EmitRuntimeLogs = false };
            battle.Start(catalog.Encounter("ENC01"), 31001, attributes: checks.attributes); Emerge.Battle.Tests.BattleBalanceTest.Reveal(battle);
            Check(Mathf.Approximately(battle.SkillMultiplier("ATK_BASIC"), 1.4f) && !battle.State.unlockedSkills.Contains("MP_BREATH"),
                "New battles use the reallocated multiplier and exclude zero-point advanced families");
            Destroy(definition); Destroy(fresh);
            Click("Settings Button"); session.LoadAutomatic();
            yield return new WaitUntil(() => session.Phase == GameSessionPhase.Playing); PreparePlayer();
            Check(AttributesMatch(Player.GetComponent<CheckActorState>().attributes, changedPoints) && Player.State.CaptureJson() == progress && Player.transform.position == position,
                "Loading the reallocation checkpoint restores changed points together with world progress");
            Click("Settings Button"); Click("Reallocate Attributes"); Click("Reset Attributes");
            for (int i = 0; i < originalPoints.Length; i++)
                for (int point = 0; point < originalPoints[i]; point++) session.AdjustAttribute((CheckBehavior)i, 1);
            Click("Confirm Attributes");
            Check(AttributesMatch(Player.GetComponent<CheckActorState>().attributes, originalPoints), "Repeated settings reallocation creates a fresh independent draft");
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

            var draft = new CharacterAttributeAllocation();
            for (int i = 0; i < 8; i++) draft.TryChange(CheckBehavior.Officer, 1);
            Check(draft.IsComplete && !draft.TryChange(CheckBehavior.Parent, 1) && !draft.TryChange(CheckBehavior.Parent, -1) &&
                !draft.TryChange((CheckBehavior)99, 1) && !draft.TryChange(CheckBehavior.Officer, int.MaxValue),
                "Allocation permits all eight points in one attribute and rejects overspending, negative or invalid input");
            var copy = draft.ToAttributes(); copy.officer = -100;
            Check(draft.Get(CheckBehavior.Officer) == 8 && draft.TryChange(CheckBehavior.Officer, -1) && draft.Remaining == 1,
                "Draft exports a separate attribute copy and refunds removed points");
            draft.Reset();
            Check(draft.Remaining == 8 && Enumerable.Range(0, SixKinAttributes.Count).All(i => draft.Get((CheckBehavior)i) == 0), "Reset returns all eight points");

            Click("New Game");
            Check(session.GetComponentsInChildren<CharacterAttributeHover>(true).Length == SixKinAttributes.Count &&
                !session.GetComponentsInChildren<Button>(true).Any(button => button.name == "Attribute Plus Self") &&
                !session.AdjustAttribute(CheckBehavior.Self, 1), "Creation exposes five attributes and rejects the removed self attribute");
            Check(session.Phase == GameSessionPhase.CharacterCreation && Time.timeScale == 0 && !GameSessionController.GameplayInputAllowed &&
                !FindButton("Confirm Attributes").interactable && !session.ConfirmCharacterCreation(),
                "New-game UI opens a paused allocation page and blocks incomplete confirmation");
            session.BeginNewGame();
            Check(session.AllocationRemaining == 8, "Repeated new-game calls cannot bypass the creation page");
            session.SendMessage("OnApplicationFocus", false); session.SendMessage("OnApplicationPause", true);
            Check(!session.SaveManual() && !File.Exists(session.Store.SlotPath(SaveSlot.Auto)),
                "Incomplete character creation neither saves nor overwrites an automatic checkpoint");
            var capture = CaptureCreation("character-creation-preview.png"); while (capture.MoveNext()) yield return capture.Current;
            var ui = session.GetComponent<GameMenuUI>();
            Check(ui.AllocationWarningText.Contains("未投入") && ui.AllocationWarningText.Contains("高级技能"), "Zero-point consequences are shown before allocation");
            foreach (CheckBehavior attribute in Enumerable.Range(0, SixKinAttributes.Count).Select(i => (CheckBehavior)i))
            {
                var row = session.GetComponentsInChildren<CharacterAttributeHover>(true).First(item => item.name == "Attribute Row " + attribute);
                var pointer = new PointerEventData(EventSystem.current) { position = new Vector2(Screen.width * .6f, Screen.height * .55f) };
                ExecuteEvents.Execute(row.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                var info = SixKinAttributes.Get(attribute);
                Check(ui.AttributeTooltipVisible && ui.AttributeTooltipText.Contains(info.name) && ui.AttributeTooltipText.Contains(info.description) &&
                    ui.AttributeTooltipText.Contains(info.actions) && ui.AttributeTooltipText.Contains(info.battleRole), "Actual hover shows the correct description and actions: " + attribute);
                if (attribute == CheckBehavior.Parent)
                {
                    capture = CaptureCreation("character-creation-tooltip-preview.png"); while (capture.MoveNext()) yield return capture.Current;
                }
                ExecuteEvents.Execute(row.gameObject, pointer, ExecuteEvents.pointerExitHandler);
                Check(!ui.AttributeTooltipVisible, "Leaving an attribute hides its tooltip: " + attribute);
            }
            Click("Attribute Plus Parent"); Click("Attribute Plus Parent"); Click("Attribute Minus Parent");
            Check(session.AllocatedPoints(CheckBehavior.Parent) == 1 && session.AllocationRemaining == 7,
                "Real plus/minus buttons update and refund the shared budget");
            Click("Reset Attributes");
            Check(session.AllocationRemaining == 8 && !FindButton("Attribute Minus Parent").interactable, "UI reset restores zero values and disables subtraction");
            Click("Cancel Character Creation");
            Check(session.Phase == GameSessionPhase.MainMenu && !File.Exists(session.Store.SlotPath(SaveSlot.Auto)),
                "Cancelling creation returns to the main menu without creating a save");
            Click("New Game");
            int[] initialPoints = { 2, 1, 2, 2, 1 };
            for (int i = 0; i < initialPoints.Length; i++)
                for (int point = 0; point < initialPoints[i]; point++) FindButton("Attribute Plus " + (CheckBehavior)i).onClick.Invoke();
            Check(session.AllocationRemaining == 0 && !session.AdjustAttribute(CheckBehavior.Wealth, 1) &&
                !FindButton("Attribute Plus Wealth").interactable && FindButton("Confirm Attributes").interactable,
                "Spending exactly eight points disables additions and enables confirmation");
            Check(ui.AllocationWarningText.Contains("均已投入"), "Zero-point warning updates after all five families receive points");
            capture = CaptureCreation("character-creation-complete-preview.png"); while (capture.MoveNext()) yield return capture.Current;
            Click("Confirm Attributes");
            Check(!session.ConfirmCharacterCreation(), "Duplicate confirmation cannot start a second scene load");
            yield return new WaitUntil(() => session.Phase == GameSessionPhase.Playing);
            PreparePlayer();
            Check(Time.timeScale == 1 && GameSessionController.GameplayInputAllowed, "New game opens the playable prop scene");
            Check(session.Store.TryRead(SaveSlot.Auto, out var initial, out _) && initial.reason == "新游戏初始存档", "New game immediately creates its initial automatic checkpoint");
            var checks = Player.GetComponent<CheckActorState>();
            Check(checks != null && AttributesMatch(checks.attributes, initialPoints) &&
                AttributesMatch(initial.actors.Find(actor => actor.id == initial.playerId).checkState.attributes, initialPoints),
                "All five confirmed attributes become the player's check bases and its initial save");
            var checkEvent = ScriptableObject.CreateInstance<CheckEventDefinition>();
            checkEvent.eventId = "character-allocation-fixture"; checkEvent.useDivination = false;
            checkEvent.options.Add(new CheckOptionDefinition { id = "create", label = "验证创造", behavior = CheckBehavior.Offspring, targetValue = 1,
                success = new CheckOutcomeDefinition(), failure = new CheckOutcomeDefinition() });
            var checkSession = checks.GetOrPrepare(checkEvent);
            Check(AttributesMatch(checkSession.attributes, initialPoints) && checks.TryResolve(checkEvent, checkSession, "create", out var checkResult, out _) &&
                checkResult.baseValue == 1 && checkResult.finalValue == 1 && checkResult.success,
                "The real check pipeline and resolver use allocated points rather than old scene defaults");
            Destroy(checkEvent);
            var recognition = ScriptableObject.CreateInstance<CheckEventDefinition>();
            recognition.eventId = "legacy-recognition-fixture"; recognition.useDivination = false;
            recognition.options.Add(new CheckOptionDefinition { id = "understand", label = "理解旧认知事件", behavior = CheckBehavior.Self, targetValue = 2,
                success = new CheckOutcomeDefinition(), failure = new CheckOutcomeDefinition() });
            var recognitionSession = checks.GetOrPrepare(recognition);
            Check(checks.TryResolve(recognition, recognitionSession, "understand", out var recognitionResult, out _) &&
                recognitionResult.behavior == CheckBehavior.Parent && recognitionResult.baseValue == 2 && recognitionResult.success,
                "New recognition checks use parent points while legacy serialized behavior stays readable");
            Destroy(recognition);
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
            var reallocationChecks = SettingsReallocationChecks(initialPoints); while (reallocationChecks.MoveNext()) yield return reallocationChecks.Current;
            Click("Save And Return");
            Check(session.Phase == GameSessionPhase.MainMenu && Time.timeScale == 0, "Save-and-return opens the paused main menu");
            Check(FindButton("Continue Game").interactable, "Saved progress enables Continue");
            Click("Continue Game");
            yield return new WaitUntil(() => session.Phase == GameSessionPhase.Playing);
            PreparePlayer();
            Check(Vector3.Distance(Player.transform.position, checkpointPosition) < .01f, "Continue restores the player position");
            Check(AttributesMatch(Player.GetComponent<CheckActorState>().attributes, initialPoints) && !FindButton("Confirm Attributes").gameObject.activeInHierarchy,
                "Continue restores all five attributes directly without reopening allocation");
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
            string autoContents = File.ReadAllText(session.Store.SlotPath(SaveSlot.Auto));
            Click("Attribute Plus Officer"); Click("Cancel Character Creation");
            Check(File.ReadAllText(session.Store.SlotPath(SaveSlot.Manual)) == manualContents && File.ReadAllText(session.Store.SlotPath(SaveSlot.Auto)) == autoContents,
                "Cancelling allocation preserves both existing save slots byte for byte");
            Click("New Game"); Click("Confirm New Game");
            int[] replacementPoints = { 0, 2, 0, 6, 0 };
            CompleteAllocation(replacementPoints);
            yield return new WaitUntil(() => session.Phase == GameSessionPhase.Playing);
            PreparePlayer();
            Check(Player.State.Count("fruit") == 0 && !Player.State.HasFlag("guide_fruit_given") && !Player.State.IsConsumed(fruitId) && Fruit.gameObject.activeSelf &&
                Vector3.Distance(Player.transform.position, spawn) < .01f, "New game resets backpack, quest, spawn and world fruit");
            Check(File.ReadAllText(session.Store.SlotPath(SaveSlot.Manual)) == manualContents, "New game preserves the prior manual save");
            Check(AttributesMatch(Player.GetComponent<CheckActorState>().attributes, replacementPoints),
                "A later new game uses a fresh allocation, including zero-point attributes");
            Check(FindObjectsOfType<GameSessionController>(true).Length == 1, "Repeated scene loads retain exactly one persistent controller");
            Click("Settings Button"); Click("Save And Return");

            string blockedPath = Path.Combine(testDirectory, "BlockedSaveDirectory");
            var legacy = session.CaptureGame("旧六属性存档");
            var legacyPlayer = legacy.actors.Find(actor => actor.id == legacy.playerId);
            legacyPlayer.checkState.attributeRulesVersion = 0;
            legacyPlayer.checkState.attributes = new ActorCheckAttributes { parent = 2, offspring = 1, officer = 1, wealth = 1, sibling = 1, self = 2 };
            legacyPlayer.checkState.contamination = 7;
            legacyPlayer.checkState.sessions.Add(new CheckSession { eventId = "old-recognition", contextId = "old-recognition", sessionId = Guid.NewGuid().ToString("N"),
                attributes = legacyPlayer.checkState.attributes.Clone(), modifiers = new[] { 0, 0, 0, 0, 0, 1 }, phase = CheckSessionPhase.Completed, outcomeApplied = true,
                result = new Emerge.Checks.CheckResult { optionId = "read", behavior = CheckBehavior.Self, baseValue = 2, modifier = 1, finalValue = 3, targetValue = 2, margin = 1, success = true } });
            legacyPlayer.propState.flags.Add("migration-progress");
            Check(session.Store.TryWrite(SaveSlot.Manual, legacy, out var migrationError), "Legacy six-attribute save is still accepted", migrationError);
            var legacyBytes = File.ReadAllBytes(session.Store.SlotPath(SaveSlot.Manual));
            session.LoadManual();
            Check(session.Phase == GameSessionPhase.CharacterCreation && session.IsReallocating && session.AllocationRemaining == 8,
                "Loading a legacy role asks for one five-family reallocation before gameplay");
            session.CancelCharacterCreation();
            Check(File.ReadAllBytes(session.Store.SlotPath(SaveSlot.Manual)).SequenceEqual(legacyBytes), "Cancelling legacy reallocation leaves the original save untouched");
            session.LoadManual(); CompleteAllocation(initialPoints);
            yield return new WaitUntil(() => session.Phase == GameSessionPhase.Playing);
            PreparePlayer();
            checks = Player.GetComponent<CheckActorState>();
            Check(checks.AttributeRulesVersion == SixKinAttributes.RulesVersion && AttributesMatch(checks.attributes, initialPoints) && checks.attributes.self == 0 &&
                checks.Contamination == 7 && Player.State.HasFlag("migration-progress") && Vector3.Distance(Player.transform.position, legacyPlayer.position) < .01f,
                "Reallocation preserves plot, position and contamination while replacing all point bases");
            var oldRecognition = checks.Sessions.First(item => item.eventId == "old-recognition");
            Check(oldRecognition.result.finalValue == 3 && oldRecognition.result.behavior == CheckBehavior.Self && oldRecognition.attributes.self == 2,
                "Completed legacy recognition checks keep their original result and snapshot");
            Check(session.Store.TryRead(SaveSlot.Auto, out var upgraded, out _) && upgraded.actors.Find(actor => actor.id == upgraded.playerId).checkState.attributeRulesVersion == SixKinAttributes.RulesVersion,
                "Confirmed reallocation writes a compatible upgraded automatic save");
            Check(session.Store.TryRead(SaveSlot.Manual, out var upgradedSource, out _) && upgradedSource.actors.Find(actor => actor.id == upgradedSource.playerId).checkState.attributeRulesVersion == SixKinAttributes.RulesVersion,
                "Confirmed reallocation also upgrades its source slot so it cannot ask again");
            Click("Settings Button"); Click("Save And Return"); session.ContinueGame();
            Check(session.Phase != GameSessionPhase.CharacterCreation && !session.IsReallocating, "Upgraded saves never ask for a second reallocation");
            yield return new WaitUntil(() => session.Phase == GameSessionPhase.Playing);
            PreparePlayer(); Click("Settings Button"); Click("Save And Return");
            File.WriteAllText(blockedPath, "Owned test file blocks a directory for save-error verification.");
            session.UseTestSaveDirectory(blockedPath);
            session.BeginNewGame();
            CompleteAllocation(initialPoints);
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
            try { WriteReport(results); }
            catch (IOException exception) { results.passed = false; Debug.LogWarning("MENU_SAVE_REPORT_WRITE_FAILED: " + exception.Message); }
            Debug.Log("MENU_SAVE_VALIDATION_" + (results.passed ? "PASS" : "FAIL") + " checks=" + results.checks.Count);
            Application.runInBackground = originalBackground;
            Completed?.Invoke(results.passed);
        }
        public static void WriteReport(Report value)
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Validation/menu-save-results.json"));
            string backups = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/MenuSaveReports"));
            Directory.CreateDirectory(Path.GetDirectoryName(path)); Directory.CreateDirectory(backups);
            string temporary = Path.Combine(backups, Guid.NewGuid().ToString("N") + ".tmp");
            File.WriteAllText(temporary, JsonUtility.ToJson(value, true));
            // Preserve a mapped old file as a backup instead of truncating or deleting its underlying data.
            if (File.Exists(path)) File.Replace(temporary, path, Path.Combine(backups, Guid.NewGuid().ToString("N") + ".json"));
            else File.Move(temporary, path);
        }
        private void OnDestroy() { if (running) Application.runInBackground = originalBackground; }
    }
}
#endif

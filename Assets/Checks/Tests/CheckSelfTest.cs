#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Emerge.Checks.Demo;
using Emerge.GameFlow;
using Emerge.Props;
using PixelPrototype;
using UnityEngine;

namespace Emerge.Checks.Tests
{
    // Uses the real dialogue selection and check state, including commit-on-selection cancellation.
    public sealed class CheckSelfTest : MonoBehaviour
    {
        private const string ScenePath = CheckDemoContext.ScenePath;
        private const string EventPath = "Assets/Checks/Demo/ObservationGate.asset";
        private const string TerminalPath = "Assets/Checks/Demo/ObservationTerminal.asset";
        private const string RepairKitPath = "Assets/Checks/Demo/RepairKit.asset";
        [Serializable] public sealed class Check { public string name; public bool passed; public string observed; }
        [Serializable] public sealed class Report
        {
            public string unityVersion, scene, completedUtc, testSaveDirectory;
            public bool passed;
            public List<Check> checks = new List<Check>();
        }
        // Version-2 DTO before the check module was added: it has no checkState field at all.
        [Serializable] private sealed class LegacyActor
        { public string id; public Vector3 position; public bool active = true; public PropGameState.Snapshot propState; }
        [Serializable] private sealed class LegacyGameSave
        {
            public int version = 2;
            public long savedUtcTicks;
            public string scenePath;
            public float playedSeconds;
            public string reason;
            public string playerId;
            public List<LegacyActor> actors = new List<LegacyActor>();
            public List<SavedProp> props = new List<SavedProp>();
        }
        [Serializable] private sealed class LegacyEnvelope
        { public int format = 1; public string payload; public string checksum; }
        public static event Action<bool> Completed;
        public bool completed;
        public int passed, failed;
        private bool running;
        private Report report;
        private readonly List<UnityEngine.Object> cleanup = new List<UnityEngine.Object>();
        private int testActorIndex;

        public void RunChecks()
        {
            if (running || completed) return;
            running = true;
            report = new Report { unityVersion = Application.unityVersion, scene = gameObject.scene.path };
            StartCoroutine(GuardedRun());
        }

        // Explicitly requested preview only; adding this component never starts tests or changes the demo.
        public void BeginChoicePreview()
        {
            Application.runInBackground = true;
            var gameView = typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameView != null) UnityEditor.EditorWindow.GetWindow(gameView).Focus();
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            StartCoroutine(ChoicePreview());
        }
        private IEnumerator ChoicePreview()
        {
            if (!GameSessionController.GameplayInputAllowed)
                throw new InvalidOperationException("Start the demo game before preparing the preview.");
            var actor = FindObjectsOfType<PlayerInteractor>().FirstOrDefault(item => item.gameObject.scene.path == ScenePath &&
                item.GetComponent<CheckActorState>() != null);
            var terminal = FindObjectsOfType<PropInstance>().FirstOrDefault(item => item.gameObject.scene.path == ScenePath &&
                item.Definition != null && item.Definition.checkEvent != null);
            if (actor == null || terminal == null) throw new InvalidOperationException("Open ChecksDemo before preparing the preview.");
            actor.CancelDialogue();
            actor.State.RestoreSnapshot(new PropGameState.Snapshot());
            actor.GetComponent<CheckActorState>().RestoreSnapshot(new CheckActorState.Snapshot
            { attributes = new ActorCheckAttributes { parent = 7, offspring = 9, officer = 7, wealth = 10, sibling = 8, self = 8 } });
            actor.GetComponent<CheckActorState>().ConfigurePreparationPipeline(PlaceholderPipeline());
            actor.transform.position = terminal.transform.position + Vector3.down;
            actor.GetComponent<PlayerMovement>().SetScriptedInput(Vector2.zero);
            actor.showUI = true; actor.keyboardInput = false;
            Physics2D.SyncTransforms();
            if (!terminal.Interact(actor)) throw new InvalidOperationException("Cannot begin the check preview.");
            ReachChoice(actor);
            yield return null;
            yield return new WaitForEndOfFrame();
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Validation/check-choice-preview.png"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("CHECK_PREVIEW_REQUESTED: " + path);
        }

        private IEnumerator GuardedRun()
        {
            var routine = Run();
            while (true)
            {
                bool next; object waiting;
                try { next = routine.MoveNext(); waiting = next ? routine.Current : null; }
                catch (Exception exception)
                { Add("Runtime verification completes without exception", false, exception.ToString()); break; }
                if (!next) break;
                yield return waiting;
            }
            foreach (var item in cleanup) if (item != null) Destroy(item);
            Finish();
        }

        private IEnumerator Run()
        {
            var definition = UnityEditor.AssetDatabase.LoadAssetAtPath<CheckEventDefinition>(EventPath);
            var terminalDefinition = UnityEditor.AssetDatabase.LoadAssetAtPath<PropDefinition>(TerminalPath);
            var kitDefinition = UnityEditor.AssetDatabase.LoadAssetAtPath<PropDefinition>(RepairKitPath);
            Add("Persistent event has three valid configured actions", definition != null && definition.Validate(out _) &&
                definition.options.Count == 3 && definition.options.Select(item => item.id).Distinct().Count() == 3, EventPath);
            if (definition == null || terminalDefinition == null || kitDefinition == null) throw new InvalidOperationException("Install the check demo before validation.");
            Add("Terminal points to the editable check event", terminalDefinition.checkEvent == definition, TerminalPath);
            Add("Demo is available to new-game loading", Application.CanStreamedLevelBeLoaded(ScenePath), ScenePath);
            var actor = FindObjectsOfType<PlayerInteractor>().FirstOrDefault(item => item.gameObject.scene == gameObject.scene && item.GetComponent<CheckActorState>() != null);
            var terminal = FindObjectsOfType<PropInstance>().FirstOrDefault(item => item.gameObject.scene == gameObject.scene && item.Definition == terminalDefinition);
            var kit = FindObjectsOfType<PropInstance>().FirstOrDefault(item => item.gameObject.scene == gameObject.scene && item.Definition == kitDefinition);
            Add("Playable scene contains a saved player, check terminal and repair pickup", actor != null && terminal != null && kit != null &&
                actor.GetComponent<SaveIdentity>() != null && !string.IsNullOrWhiteSpace(actor.GetComponent<SaveIdentity>().Id), "Tests first use actual serialized demo objects");
            if (actor == null || terminal == null || kit == null) throw new InvalidOperationException("Demo player or props are missing.");
            actor.keyboardInput = false;
            var movement = actor.GetComponent<PlayerMovement>(); movement.SetScriptedInput(Vector2.zero);
            var state = actor.GetComponent<CheckActorState>();
            state.ConfigurePreparationPipeline(PlaceholderPipeline());
            actor.transform.position = terminal.transform.position + new Vector3(0, -1, 0); Physics2D.SyncTransforms();
            int resolvedEvents = 0; state.Resolved += result => resolvedEvents++;
            Add("Entry opens the real dialogue and disables movement", terminal.Interact(actor) && actor.IsInDialogue && !movement.enabled,
                "PropInstance.Interact → CheckEncounter → PlayerInteractor");
            var prepared = state.Sessions.Single();
            string context = terminal.InstanceId + ":" + definition.eventId;
            Add("Casting, chart and modifier stages remain explicit placeholders", prepared.modifiers.Length == 6 && prepared.modifiers.All(value => value == 0) &&
                prepared.castingStatus.Contains("占位") && prepared.chartStatus.Contains("占位") && prepared.modifierStatus.Contains("占位"), "Six modifiers are zero; no random casting");
            actor.CancelDialogue();
            Add("Cancel before choice keeps the prepared session and releases movement", prepared.phase == CheckSessionPhase.Ready && prepared.result == null &&
                !prepared.outcomeApplied && movement.enabled && !actor.State.HasFlag("check-gate-open"), "No cost or outcome before selection");
            state.attributes.sibling = -100;
            Add("Reopening shares the original attribute snapshot", terminal.Interact(actor) && ReferenceEquals(prepared, state.GetOrPrepare(definition, context)) &&
                prepared.attributes.sibling == 8 && state.Sessions.Count == 1, "Changing live attributes cannot reprepare this event");
            ReachChoice(actor);
            var repairChoice = actor.DialogueOptions.FirstOrDefault(option => option.id == "repair");
            Add("Missing repair kit produces a disabled actionable choice", repairChoice != null && !repairChoice.enabled && !string.IsNullOrWhiteSpace(repairChoice.disabledReason), "Repair prerequisite appears in the choice list");
            Add("Difficulty is hidden before choosing", !actor.DialogueOptions.Any(option => option.label.Contains("目标")), "revealDifficultyBeforeChoice=false");
            Add("Unknown and disabled choice submissions have no effect", !actor.ChooseDialogueOption("missing") && !actor.ChooseDialogueOption("repair") &&
                actor.IsAwaitingChoice && prepared.phase == CheckSessionPhase.Ready && resolvedEvents == 0, "No result or outcome for invalid choices");
            Add("Equality succeeds through the real choice callback", actor.ChooseDialogueOption("communicate") && prepared.result != null && prepared.result.success &&
                prepared.result.finalValue == 8 && prepared.result.targetValue == 8 && prepared.result.margin == 0, "8 + 0 >= 8");
            Add("Successful choice applies its flag and event once", actor.State.HasFlag("check-gate-open") && prepared.outcomeApplied && resolvedEvents == 1,
                "Outcome commits at selection");
            Add("Result dialogue reveals the actual comparison", actor.CurrentDialogueText.Contains("8") && actor.CurrentDialogueText.Contains("目标") &&
                !actor.IsAwaitingChoice && !movement.enabled, actor.CurrentDialogueText);
            Add("Duplicate callback and direct resolution cannot repeat effects", !actor.ChooseDialogueOption("communicate") &&
                !state.TryResolve(definition, prepared, "communicate", out _, out _) && resolvedEvents == 1, "Session is sealed");
            string story = FinishDialogue(actor);
            Add("Success branch continues the story then completes the session", prepared.phase == CheckSessionPhase.Completed && story.Contains("研究员") &&
                !actor.IsInDialogue && movement.enabled, "Result → branch → continuation → movement restored");
            Add("Completed entry shows its previous result without another event", terminal.Interact(actor) && !actor.IsAwaitingChoice &&
                actor.DialogueOptions.Count == 0 && resolvedEvents == 1, "Re-entry is read-only");
            FinishDialogue(actor);

            var force = NewActor("Force"); var forceState = force.GetComponent<CheckActorState>();
            var forceTerminal = Place(terminalDefinition, force.transform.position + Vector3.up);
            int forceEvents = 0; forceState.Resolved += result => forceEvents++;
            Add("Failure scenario opens independently", forceTerminal.Interact(force), "A fresh actor and context"); ReachChoice(force);
            Add("Force fails against its higher difficulty", force.ChooseDialogueOption("force") && forceState.Sessions.Single().result.finalValue == 10 &&
                forceState.Sessions.Single().result.targetValue == 12 && !forceState.Sessions.Single().result.success &&
                forceState.Sessions.Single().result.margin == -2, "10 + 0 < 12");
            var forceSession = forceState.Sessions.Single();
            Add("Failure creates the side route and adds contamination once", force.State.HasFlag("check-side-route") && !force.State.HasFlag("check-gate-open") &&
                forceState.Contamination == 15 && forceEvents == 1, "Failure still offers a continuation");
            force.CancelDialogue();
            Add("Cancel after choice retains committed failure without a refund", forceSession.phase == CheckSessionPhase.Resolved && forceSession.outcomeApplied &&
                forceState.Contamination == 15 && force.GetComponent<PlayerMovement>().enabled, "Cancellation stops presentation, preserving selection");
            Add("Reopen after selection presents the same failure", forceTerminal.Interact(force) && force.DialogueOptions.Count == 0 && !force.IsAwaitingChoice &&
                forceState.Sessions.Count == 1 && forceEvents == 1, "No new choice or reroll");
            string failureStory = FinishDialogue(force);
            Add("Failure continuation remains playable", failureStory.Contains("侧路") && forceSession.phase == CheckSessionPhase.Completed &&
                forceState.Contamination == 15 && forceEvents == 1, "Continue along a different route");

            var repair = NewActor("Repair"); var repairState = repair.GetComponent<CheckActorState>();
            var repairTerminal = Place(terminalDefinition, repair.transform.position + Vector3.up);
            repairTerminal.Interact(repair); ReachChoice(repair); var repairSession = repairState.Sessions.Single();
            Add("Insufficient inventory cannot resolve directly", !repairState.TryResolve(definition, repairSession, "repair", out _, out _) &&
                repairSession.phase == CheckSessionPhase.Ready && repair.State.Count("repair-kit") == 0, "Prerequisites also apply outside the UI");
            repair.CancelDialogue();
            var pickup = Place(kitDefinition, repair.transform.position + Vector3.right * .5f);
            Add("Actual kit pickup enters the backpack once", pickup.Interact(repair) && repair.State.Count("repair-kit") == 1 && !pickup.gameObject.activeSelf &&
                !pickup.Interact(repair), "Uses the existing prop pickup flow");
            repairTerminal.Interact(repair); ReachChoice(repair);
            Add("Repair becomes enabled after pickup", repair.DialogueOptions.First(option => option.id == "repair").enabled, "Requirements are rebuilt on re-entry");
            repair.State.RemoveItem("repair-kit", 1);
            Add("Requirements are rechecked when inventory changes during choice", repair.ChooseDialogueOption("repair") && repairSession.phase == CheckSessionPhase.Ready &&
                !repairSession.outcomeApplied && !repair.State.HasFlag("check-gate-open"), "Visible enabled choice cannot bypass changed inventory");
            repair.CancelDialogue(); repair.State.AddItem("repair-kit", "维修包", 1);
            repairTerminal.Interact(repair); ReachChoice(repair); int repairEvents = 0; repairState.Resolved += result => repairEvents++;
            Add("Repair equality succeeds and spends one kit on selection", repair.ChooseDialogueOption("repair") && repairSession.result.success &&
                repairSession.result.finalValue == 9 && repairSession.result.targetValue == 9 && repair.State.Count("repair-kit") == 0 &&
                repair.State.HasFlag("check-gate-open") && repairEvents == 1, "9 + 0 >= 9; kit cost commits once");
            repair.CancelDialogue(); repairTerminal.Interact(repair); FinishDialogue(repair);
            Add("Cancel and reopen do not consume a second kit", repair.State.Count("repair-kit") == 0 && repairEvents == 1 && repairSession.phase == CheckSessionPhase.Completed,
                "Committed repair is never charged twice");

            var restored = NewActor("Restored"); var restoredState = restored.GetComponent<CheckActorState>();
            var snapshot = forceState.CaptureSnapshot();
            Add("Resolved sessions, contamination and frozen attributes survive a JSON round trip", restoredState.RestoreSnapshot(JsonUtility.FromJson<CheckActorState.Snapshot>(JsonUtility.ToJson(snapshot))) &&
                restored.State.RestoreSnapshot(force.State.CaptureSnapshot()) && restoredState.Contamination == 15 &&
                restoredState.Sessions.Single().sessionId == forceSession.sessionId && restored.State.HasFlag("check-side-route"), "Separate check and prop snapshots");
            Add("Restored prepared context returns the same session and cannot replay", restoredState.GetOrPrepare(definition, forceSession.contextId).sessionId == forceSession.sessionId &&
                !restoredState.TryResolve(definition, restoredState.Sessions.Single(), "force", out _, out _) && restoredState.Contamination == 15,
                "Persistent context and commit state");
            snapshot.contamination = 123;
            Add("Captured and restored snapshots do not alias live state", forceState.Contamination == 15 && restoredState.Contamination == 15, "Deep copied snapshots");
            var invalidSnapshot = restoredState.CaptureSnapshot(); invalidSnapshot.sessions[0].result.finalValue++;
            Add("Inconsistent saved arithmetic is rejected atomically", !CheckActorState.IsValidSnapshot(invalidSnapshot) && !restoredState.RestoreSnapshot(invalidSnapshot) &&
                restoredState.Contamination == 15 && restoredState.Sessions[0].result.finalValue == 10, "A corrupt result cannot replace valid live state");
            var ready = NewActor("Ready"); var readyState = ready.GetComponent<CheckActorState>(); var readySession = readyState.GetOrPrepare(definition, "ready-context");
            var readyCopy = readyState.CaptureSnapshot();
            Add("Unselected session is valid in a save", CheckActorState.IsValidSnapshot(readyCopy) && readyCopy.sessions[0].phase == CheckSessionPhase.Ready &&
                readyCopy.sessions[0].result == null && !readyCopy.sessions[0].outcomeApplied, "Cancelled entry can persist");
            Add("Foreign sessions and unknown action IDs are rejected", !readyState.TryResolve(definition, forceSession, "force", out _, out _) &&
                !readyState.TryResolve(definition, readySession, "unknown", out _, out _) && readySession.phase == CheckSessionPhase.Ready, "Ownership and action identity");

            var rewardEvent = Clone(definition); rewardEvent.eventId = "reward-check";
            rewardEvent.options[0].success.rewardItemKey = "test-token"; rewardEvent.options[0].success.rewardItemName = "测试凭证"; rewardEvent.options[0].success.rewardItemAmount = 2;
            rewardEvent.options[0].requiredFlags = new[] { "test-permission" };
            var rewardActor = NewActor("Reward"); var rewardState = rewardActor.GetComponent<CheckActorState>();
            var rewardSession = rewardState.GetOrPrepare(rewardEvent);
            Add("Flag prerequisite blocks an otherwise successful action", !rewardState.CanChoose(rewardEvent.options[0], out _) &&
                !rewardState.TryResolve(rewardEvent, rewardSession, "communicate", out _, out _), "Outcome rewards require permission");
            rewardActor.State.SetFlag("test-permission");
            Add("Reward items are applied once on a permitted success", rewardState.TryResolve(rewardEvent, rewardSession, "communicate", out _, out _) &&
                rewardActor.State.Count("test-token") == 2 && !rewardState.TryResolve(rewardEvent, rewardSession, "communicate", out _, out _) &&
                rewardActor.State.Count("test-token") == 2, "Reward token ×2 is sealed with the session");

            VerifyCoreBoundaries(definition);
            VerifyLegacyChoices(ready);
            VerifyDisabledOwner(definition, terminalDefinition);
            VerifySaveStore(force);
            VerifyReadySaveStore(definition);
            yield return null;
        }

        private void VerifyCoreBoundaries(CheckEventDefinition source)
        {
            var invalid = Clone(source); invalid.options[1].id = invalid.options[0].id;
            Add("Duplicate action IDs are rejected during authoring", !invalid.Validate(out _), "Selections remain unambiguous");
            var overflowDefinition = Clone(source);
            var attrs = new ActorCheckAttributes { sibling = int.MaxValue };
            var session = PlaceholderPipeline().Prepare(overflowDefinition, attrs); session.modifiers[(int)CheckBehavior.Sibling] = 1;
            bool rejected = false;
            try { CheckResolver.Resolve(session, overflowDefinition.options[0]); } catch (OverflowException) { rejected = true; }
            Add("Overflow never commits a partial core result", rejected && session.phase == CheckSessionPhase.Ready && session.result == null,
                "int.MaxValue + 1 is rejected before changing state");
            var other = PlaceholderPipeline().Prepare(source, new ActorCheckAttributes { sibling = 7 });
            var result = CheckResolver.Resolve(other, source.options[0]);
            Add("One point below target fails", !result.success && result.finalValue == 7 && result.margin == -1, "7 < 8");
        }

        private void VerifyLegacyChoices(PlayerInteractor actor)
        {
            var propDefinition = ScriptableObject.CreateInstance<PropDefinition>(); cleanup.Add(propDefinition);
            propDefinition.actions = PropActions.Dialogue; propDefinition.cooldown = 0;
            var owner = Place(propDefinition, actor.transform.position + Vector3.up);
            bool selected = false, finished = false;
            var lines = new List<PropDialogueLine> { new PropDialogueLine { speaker = "旧对话", text = "是否交付？" } };
            Add("Existing boolean choice dialogue still begins", actor.BeginChoiceDialogue(owner, lines, "交给", "不给", choice =>
                { selected = choice; actor.ReplaceDialogue(new List<PropDialogueLine> { new PropDialogueLine { speaker = "旧对话", text = "已交付" } }); },
                success => finished = success), "Previous item-handover choice API");
            ReachChoice(actor);
            Add("Existing boolean selection is independent of generic options", actor.DialogueOptions.Count == 0 && actor.ChooseDialogueOption(true) && selected,
                "Legacy bool callback remains available");
            FinishDialogue(actor);
            Add("Legacy dialogue completion releases movement", finished && !actor.IsInDialogue && actor.GetComponent<PlayerMovement>().enabled, "No generic-option regression");
        }

        private void VerifySaveStore(PlayerInteractor actor)
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/CheckSaveTest_" + Guid.NewGuid().ToString("N")));
            report.testSaveDirectory = directory;
            var store = new GameSaveStore(directory);
            var data = new GameSaveData { savedUtcTicks = DateTime.UtcNow.Ticks, scenePath = ScenePath, playerId = "test-check-actor" };
            data.actors.Add(new SavedActor { id = data.playerId, propState = actor.State.CaptureSnapshot(), checkState = actor.GetComponent<CheckActorState>().CaptureSnapshot() });
            Add("Game save store writes and reads check progression", store.TryWrite(SaveSlot.Manual, data, out _) &&
                store.TryRead(SaveSlot.Manual, out var loaded, out _) && loaded.actors[0].checkState != null &&
                loaded.actors[0].checkState.contamination == 15 && loaded.actors[0].checkState.sessions.Count == 1,
                "Uses real checksummed version-2 save format in isolated test storage");
            data.actors[0].checkState = null;
            Add("Older version-2 saves without check state remain readable", store.TryWrite(SaveSlot.Auto, data, out _) &&
                store.TryRead(SaveSlot.Auto, out var legacy, out _) && legacy.actors[0].checkState == null, "Optional new field supports existing saves");
            var original = new LegacyGameSave { savedUtcTicks = DateTime.UtcNow.Ticks, scenePath = ScenePath,
                playerId = "legacy-player", reason = "Save created before checks existed" };
            original.actors.Add(new LegacyActor { id = original.playerId, propState = actor.State.CaptureSnapshot() });
            string payload = JsonUtility.ToJson(original);
            string legacyDirectory = Path.Combine(directory, "OriginalLegacyFormat");
            var legacyStore = new GameSaveStore(legacyDirectory);
            Directory.CreateDirectory(legacyDirectory);
            string envelope = JsonUtility.ToJson(new LegacyEnvelope { payload = payload, checksum = PayloadHash(payload) }, true);
            File.WriteAllText(legacyStore.SlotPath(SaveSlot.Manual), envelope, Encoding.UTF8);
            Add("Actual old-format payload omits checkState entirely", !payload.Contains("\"checkState\"") && payload.Contains("\"version\":2"),
                "Serialized the original actor DTO, rather than setting a new field to null");
            Add("Original legacy checksum envelope is accepted by the real save reader", legacyStore.TryRead(SaveSlot.Manual, out var originalLoaded, out _) &&
                originalLoaded.actors.Count == 1 && originalLoaded.actors[0].id == "legacy-player" && originalLoaded.actors[0].checkState == null &&
                originalLoaded.actors[0].propState.flags.Contains("check-side-route"), "Reads original version-2 fields with a valid SHA-256 envelope; no backup exists");
            File.WriteAllText(legacyStore.SlotPath(SaveSlot.Manual), JsonUtility.ToJson(new LegacyEnvelope
            { payload = payload, checksum = "invalid-checksum" }), Encoding.UTF8);
            Add("Legacy-format reader still enforces checksum verification", !legacyStore.TryRead(SaveSlot.Manual, out _, out _),
                "Same original payload with an invalid checksum is rejected; no backup can mask rejection");
            data.actors[0].checkState = actor.GetComponent<CheckActorState>().CaptureSnapshot(); data.actors[0].checkState.sessions[0].result.success = true;
            Add("Game save rejects invalid check data", !store.TryWrite(SaveSlot.Auto, data, out _), "Malformed success flag cannot enter a save");
        }

        private void VerifyDisabledOwner(CheckEventDefinition definition, PropDefinition terminalDefinition)
        {
            var actor = NewActor("DisabledOwner");
            var state = actor.GetComponent<CheckActorState>();
            var owner = Place(terminalDefinition, actor.transform.position + Vector3.up);
            int outcomeEvents = 0; state.Resolved += result => outcomeEvents++;
            Add("Disable-owner scenario opens a pending check choice", owner.Interact(actor), "Actual PropInstance owner and option dialogue");
            ReachChoice(actor); var session = state.Sessions.Single();
            owner.gameObject.SetActive(false);
            Add("Disabling check owner cancels immediately and restores movement", !actor.IsInDialogue && !actor.IsAwaitingChoice &&
                actor.DialogueOptions.Count == 0 && actor.GetComponent<PlayerMovement>().enabled, "No Update or extra frame was required after SetActive(false)");
            Add("Inactive check owner prevents stale choice submission", !actor.ChooseDialogueOption("communicate") && session.phase == CheckSessionPhase.Ready &&
                session.result == null && !session.outcomeApplied && outcomeEvents == 0 && !actor.State.HasFlag("check-gate-open"),
                "No result, cost or outcome survives the cancelled callback");
            owner.gameObject.SetActive(true);
            Add("Re-enabled check owner reopens its same unselected session", owner.Interact(actor) &&
                ReferenceEquals(session, state.GetOrPrepare(definition, owner.InstanceId + ":" + definition.eventId)) && state.Sessions.Count == 1,
                "Owner lifecycle does not prepare another session");
            ReachChoice(actor);
            Add("Reopened owner supports one fresh valid selection", actor.IsAwaitingChoice && actor.ChooseDialogueOption("communicate") &&
                session.result != null && session.result.success && outcomeEvents == 1, "Cancellation clears stale controls, while normal interaction remains usable");
            FinishDialogue(actor);
        }

        private void VerifyReadySaveStore(CheckEventDefinition definition)
        {
            string directory = Path.Combine(report.testSaveDirectory, "PreparedSession");
            var store = new GameSaveStore(directory);
            var source = NewActor("ReadySaveSource"); var sourceState = source.GetComponent<CheckActorState>();
            var prepared = sourceState.GetOrPrepare(definition, "prepared-save-context");
            sourceState.attributes.sibling = -100;
            var data = new GameSaveData { savedUtcTicks = DateTime.UtcNow.Ticks, scenePath = ScenePath, playerId = "ready-save-player" };
            data.actors.Add(new SavedActor { id = data.playerId, propState = source.State.CaptureSnapshot(), checkState = sourceState.CaptureSnapshot() });
            bool written = store.TryWrite(SaveSlot.Manual, data, out string writeError);
            GameSaveData loaded = null; string readError = "Not written";
            bool read = written && store.TryRead(SaveSlot.Manual, out loaded, out readError);
            Add("Real game save writes and reads an unselected Ready session", read && loaded.actors[0].checkState != null &&
                loaded.actors[0].checkState.sessions.Count == 1 && loaded.actors[0].checkState.sessions[0].phase == CheckSessionPhase.Ready &&
                loaded.actors[0].checkState.sessions[0].result == null && !loaded.actors[0].checkState.sessions[0].outcomeApplied,
                "Write=" + written + "; read=" + read + "; " + (written ? readError : writeError));
            var restored = NewActor("ReadySaveRestored"); var restoredState = restored.GetComponent<CheckActorState>();
            bool applied = read && restored.State.RestoreSnapshot(loaded.actors[0].propState) && restoredState.RestoreSnapshot(loaded.actors[0].checkState);
            var session = applied ? restoredState.GetOrPrepare(definition, "prepared-save-context") : null;
            Add("Ready save restoration preserves session identity and frozen values", applied && session.sessionId == prepared.sessionId &&
                session.attributes.sibling == 8 && restoredState.attributes.sibling == -100 && session.modifiers.All(value => value == 0),
                "The save preserves prepared attributes independently of changed live attributes");
            int events = 0; restoredState.Resolved += result => events++;
            bool resolved = applied && restoredState.TryResolve(definition, session, "communicate", out _, out _);
            Add("Restored Ready session can submit and apply its outcome exactly once", resolved && session.result.success && session.result.finalValue == 8 &&
                restored.State.HasFlag("check-gate-open") && events == 1 && !restoredState.TryResolve(definition, session, "communicate", out _, out _) && events == 1,
                "Saved Ready → success at frozen 8 → sealed result");
        }

        private static string PayloadHash(string payload)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(payload))).Replace("-", "");
        }

        private PlayerInteractor NewActor(string name)
        {
            var root = new GameObject("Check test " + name); cleanup.Add(root);
            root.transform.position = new Vector3(100 + testActorIndex++ * 10, 100, 0);
            var body = root.AddComponent<Rigidbody2D>(); body.gravityScale = 0; body.constraints = RigidbodyConstraints2D.FreezeRotation;
            root.AddComponent<PlayerMovement>().SetScriptedInput(Vector2.zero);
            root.AddComponent<PropGameState>();
            var actor = root.AddComponent<PlayerInteractor>(); actor.keyboardInput = false; actor.showUI = false;
            var checks = root.AddComponent<CheckActorState>();
            checks.attributes = new ActorCheckAttributes { sibling = 8, offspring = 9, wealth = 10 };
            checks.ConfigurePreparationPipeline(PlaceholderPipeline());
            return actor;
        }
        private static CheckPipeline PlaceholderPipeline() => new CheckPipeline(
            new PlaceholderCastingProvider(), new PlaceholderChartProvider(), new PlaceholderModifierProvider());
        private PropInstance Place(PropDefinition definition, Vector3 position)
        {
            var root = new GameObject("Check test " + definition.DisplayName); cleanup.Add(root); root.transform.position = position;
            var prop = root.AddComponent<PropInstance>(); prop.Configure(definition); Physics2D.SyncTransforms(); return prop;
        }
        private CheckEventDefinition Clone(CheckEventDefinition source)
        { var clone = ScriptableObject.CreateInstance<CheckEventDefinition>(); JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), clone); cleanup.Add(clone); return clone; }
        private static void ReachChoice(PlayerInteractor actor)
        { int remaining = 20; while (actor.IsInDialogue && !actor.IsAwaitingChoice && remaining-- > 0) actor.AdvanceDialogue(); }
        private static string FinishDialogue(PlayerInteractor actor)
        {
            string lines = ""; int remaining = 30;
            while (actor.IsInDialogue && !actor.IsAwaitingChoice && remaining-- > 0)
            { lines += actor.CurrentDialogueText + "\n"; actor.AdvanceDialogue(); }
            return lines;
        }
        private void Add(string name, bool condition, string observed)
        {
            if (condition) passed++; else failed++;
            report.checks.Add(new Check { name = name, passed = condition, observed = observed });
            Debug.Log("CHECK_FLOW " + (condition ? "PASS " : "FAIL ") + name + ": " + observed);
            WriteReport();
        }
        private void WriteReport()
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Validation/check-results.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, JsonUtility.ToJson(report, true));
        }
        private void Finish()
        {
            completed = true; report.completedUtc = DateTime.UtcNow.ToString("O");
            report.passed = report.checks.Count > 0 && report.checks.All(check => check.passed); WriteReport();
            Debug.Log("CHECK_VALIDATION_" + (report.passed ? "PASS" : "FAIL") + " checks=" + report.checks.Count);
            Completed?.Invoke(report.passed);
        }
    }
}
#endif

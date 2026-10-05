#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Emerge.Battle;
using Emerge.Checks;
using Emerge.GameFlow;
using Emerge.Props;
using PixelPrototype;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Emerge.Day1.Tests
{
    /// <summary>
    /// Exercises scene interactions, the real dialogue/casting interfaces, battle buttons,
    /// Physics2D doors and actual scene reloads. It never grants story flags or test rewards.
    /// </summary>
    public sealed class Day1SelfTest : MonoBehaviour
    {
        [Serializable] public sealed class CheckResult { public string name, observed; public bool passed; }
        [Serializable] public sealed class Report
        {
            public string unityVersion, scene, completedUtc, testSaveDirectory;
            public bool passed;
            public List<CheckResult> checks = new List<CheckResult>();
        }
        public static event Action<bool> Completed;
        private Report results;
        private string initialReport, scenePath, originalDirectory, originalScenePath;
        private bool originalBackground, finished;
        private float originalAutoInterval;
        private GameSessionController session;
        private Dictionary<string, string> userSaveHashes;
        private Day1FlowController Flow => FindObjectOfType<Day1FlowController>();
        private PlayerInteractor Player => Flow != null ? Flow.actor : null;
        private PropGameState State => Player.State;
        private CheckActorState Checks => Player.GetComponent<CheckActorState>();

        public void Initialize(string editorReport, string scene)
        { initialReport = editorReport; scenePath = scene; }

        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            yield return null;
            results = string.IsNullOrEmpty(initialReport) ? new Report() : JsonUtility.FromJson<Report>(initialReport);
            results.unityVersion = Application.unityVersion;
            results.scene = scenePath;
            yield return GuardedRun();
        }

        private IEnumerator GuardedRun()
        {
            // Drive nested routines here so an exception cannot silently strand the editor in Play.
            var routines = new Stack<IEnumerator>();
            routines.Push(Run());
            while (routines.Count > 0)
            {
                bool next; object wait;
                try { next = routines.Peek().MoveNext(); wait = next ? routines.Peek().Current : null; }
                catch (Exception exception) { Add(false, "Day1 runtime verification finished without exception", exception.ToString()); break; }
                if (!next) { routines.Pop(); continue; }
                if (wait is IEnumerator nested) { routines.Push(nested); continue; }
                yield return wait;
            }
            Finish();
        }

        private IEnumerator Run()
        {
            yield return Until(() => GameSessionController.Instance != null && GameSessionController.Instance.IsReady, "main menu ready");
            session = GameSessionController.Instance;
            Require(session.Phase == GameSessionPhase.MainMenu, "Validation starts in the paused main menu");
            originalDirectory = session.SaveDirectory;
            originalScenePath = session.gameScenePath;
            originalAutoInterval = session.autoSaveInterval;
            originalBackground = Application.runInBackground;
            userSaveHashes = HashFiles(originalDirectory);
            results.testSaveDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/Day1Checks_" + Guid.NewGuid().ToString("N"), "Saves"));
            session.UseTestSaveDirectory(results.testSaveDirectory);
            session.gameScenePath = scenePath;
            session.autoSaveInterval = 600;
            Application.runInBackground = true;
            Add(!Directory.Exists(results.testSaveDirectory), "Test saves are isolated from existing player saves", results.testSaveDirectory);
            ClickMenu("New Game");
            Require(session.Phase == GameSessionPhase.CharacterCreation, "Day1 new game enters the five-attribute creation page");
            int[] startingPoints = { 1, 2, 2, 2, 1 };
            for (int i = 0; i < startingPoints.Length; i++)
                for (int point = 0; point < startingPoints[i]; point++) Require(session.AdjustAttribute((CheckBehavior)i, 1), "Allocate Day1 starting attributes");
            ClickMenu("Confirm Attributes");
            yield return Until(() => session.Phase == GameSessionPhase.Playing && Flow != null && Player != null, "Day1 new game loaded");
            Add(Enumerable.Range(0, SixKinAttributes.Count).All(i => Checks.attributes.Get((CheckBehavior)i) == startingPoints[i]),
                "Day1 starts with the eight points confirmed in character creation");
            PreparePlayer();
            yield return Until(() => Player.IsInDialogue, "automatic admission questionnaire");
            Add(!State.HasFlag("day1.admitted") && State.Count("day1.identity-card") == 0, "A new game starts with admission incomplete and no identity card");
            yield return ToChoice();
            Require(Player.DialogueOptions.Any(o => o.id == "review"), "The admission test offers a review response");
            Require(Player.ChooseDialogueOption("review"), "Select an incorrect admission answer through the actual dialogue interface");
            yield return FinishDialogue();
            Add(!State.HasFlag("day1.test.q1") && !State.HasFlag("day1.admitted") && State.Count("day1.identity-card") == 0,
                "An incorrect admission answer does not pass the question or grant a card");
            yield return Until(() => Player.IsInDialogue, "admission question can be retried");
            Player.CancelDialogue();
            Add(!State.HasFlag("day1.admitted") && State.Count("day1.identity-card") == 0,
                "Cancelling admission leaves the stage and inventory uncommitted");
            float admissionDeadline = Time.realtimeSinceStartup + 12;
            while (!State.HasFlag("day1.admitted"))
            {
                if (Time.realtimeSinceStartup > admissionDeadline) throw new TimeoutException("Admission did not finish.");
                if (Player.IsInDialogue)
                {
                    if (Player.IsAwaitingChoice) Require(Player.ChooseDialogueOption("correct"), "Confirm an admission question");
                    else Player.AdvanceDialogue();
                }
                yield return null;
            }
            Add(State.Count("day1.identity-card") == 1 && new[] { "day1.test.q1", "day1.test.q2", "day1.test.q3" }.All(State.HasFlag),
                "Three completed answers grant exactly one identity card");
            Add(Flow.MetCount == 0, "Group admission does not count as six personal introductions");

            yield return Interact("Environment");
            yield return FinishDialogue();
            Add(!State.HasFlag("day1.environment_recorded") && State.Count("day1.environment-file") == 0,
                "Environment records cannot be claimed before rules are read");
            yield return Interact("Rules");
            Player.CancelDialogue();
            Add(!State.HasFlag("day1.rules_read"), "Cancelling the rules speech keeps it available");
            yield return Interact("Rules");
            yield return FinishDialogue();
            Add(State.HasFlag("day1.rules_read") && !State.HasFlag("day1.free_roam"), "Completed rules disperse the team and retain the environment objective", Flow.Objective);
            yield return Interact("Supplies");
            yield return FinishDialogue();
            Add(!State.HasFlag("day1.supplies_received") && State.Count("day1.supplies") == 0,
                "Supplies cannot be claimed before the environment notification");
            yield return Interact("Environment");
            Player.CancelDialogue();
            Add(!State.HasFlag("day1.environment_recorded") && State.Count("day1.environment-file") == 0,
                "Cancelling the environment report does not issue the archive");
            yield return Interact("Environment");
            yield return FinishDialogue();
            Add(State.HasFlag("day1.environment_recorded") && State.Count("day1.environment-file") == 1,
                "The completed environment interaction saves one archive and the supply objective", Flow.Objective);
            yield return Interact("Supplies");
            Player.CancelDialogue();
            Add(!State.HasFlag("day1.supplies_received"), "Cancelled supply collection does not trigger the shadow");
            yield return Interact("Supplies");
            yield return FinishDialogue();
            yield return Until(() => Flow.IsShadowPlaying, "shadow playback begins");
            Add(State.Count("day1.supplies") == 1 && State.HasFlag("day1.supplies_received") && !State.HasFlag("day1.shadow_seen"),
                "Supply receipt and shadow completion have separate saved stages");
            ClickMenu("Settings Button");
            yield return new WaitForSecondsRealtime(1.7f);
            Add(Flow.IsShadowPlaying && !State.HasFlag("day1.shadow_seen"), "Paused settings suspend the shadow rather than completing it");
            ClickMenu("Save Game");
            Require(session.Store.TryRead(SaveSlot.Manual, out _, out string shadowSaveError), "A real manual save captures an interrupted shadow", shadowSaveError);
            session.LoadManual();
            yield return Until(() => session.Phase == GameSessionPhase.Playing && Player != null, "interrupted shadow scene reload");
            PreparePlayer();
            Add(State.Count("day1.supplies") == 1 && !State.HasFlag("day1.shadow_seen"), "Reload retains collected supplies without a duplicate reward");
            yield return Until(() => Player.IsInDialogue && Flow.IsShadowPlaying, "shadow resumes into the protagonist's reaction", 12);
            Add(Player.CurrentDialogueText.Contains("灯接触不良") && Player.CurrentDialogueSpeaker == "桓玉鉴",
                "The resumed shadow remains unnamed and the protagonist attributes it to faulty lighting");
            yield return FinishDialogue();
            yield return Until(() => State.HasFlag("day1.shadow_seen") && State.HasFlag("day1.free_roam") && !Player.IsInDialogue && !Flow.IsShadowPlaying,
                "shadow reaction completes and free exploration opens", 12);
            Add(Player.GetComponent<PlayerMovement>().enabled, "The completed shadow releases the movement lock");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Application.dataPath, "../Validation/day1-gameplay-preview.png")));
            var suppliesBefore = InventoryCounts();
            yield return Interact("Supplies"); yield return FinishDialogue();
            Add(SameInventory(suppliesBefore) && !Flow.IsShadowPlaying, "Repeated supplies neither issue items nor replay the shadow");

            yield return Door(new Vector2(-9, 4.5f), Vector2.right, "Exterior to buffer");
            yield return Door(new Vector2(-3, 4.5f), Vector2.right, "Buffer to control");
            yield return Door(new Vector2(1.5f, 5), Vector2.up, "Control to living area");
            yield return Door(new Vector2(-3, -.5f), Vector2.left, "Control to machine room");
            yield return Door(new Vector2(1.5f, -3), Vector2.down, "Control to chemical laboratory");
            yield return Door(new Vector2(7, .5f), Vector2.right, "Control to storage");
            yield return Door(new Vector2(-16.5f, -10), Vector2.down, "Exterior to dock");
            Add(!Flow.CanFinishDay, "Day1 cannot finish while preparation tasks are outstanding");
            yield return Interact("Rules"); yield return ToChoice();
            Add(Player.DialogueOptions.Any(o => o.id == "finish" && !o.enabled) && !Player.ChooseDialogueOption("finish") && !State.HasFlag("day1.finished"),
                "The actual close out option is disabled until required preparation is complete");
            Require(Player.ChooseDialogueOption("later"), "Keep exploring through the close out dialogue");
            yield return FinishDialogue();

            yield return Interact("Diagnosis", false);
            Add(!Player.IsInDialogue && Player.GetComponent<CheckCastingUI>()?.IsOpen != true && !State.HasFlag("day1.diagnosis_done"),
                "A task check is inaccessible before accepting its task");
            yield return Interact("LinXi"); yield return ToChoice();
            Require(Player.ChooseDialogueOption("accept"), "Select a task offer before cancelling its response");
            Player.CancelDialogue();
            Add(!State.HasFlag("day1.quest.lin-xi") && !State.HasFlag("day1.met.lin-xi"),
                "Cancelling an accepted task response commits neither the task nor the personal introduction");
            yield return Choice("LinXi", "later");
            Add(State.HasFlag("day1.met.lin-xi") && !State.HasFlag("day1.quest.lin-xi"), "Meeting Lin Xi and postponing the task are separate states");
            yield return Choice("LinXi", "accept");
            Add(State.HasFlag("day1.quest.lin-xi"), "Revisiting Lin Xi allows later task acceptance");

            yield return Interact("Diagnosis");
            var cast = Player.GetComponent<CheckCastingUI>();
            Require(cast != null && cast.IsOpen, "Diagnosis opens the real three coin casting interface");
            cast.throwDuration = .02f;
            Require(cast.ThrowNext(), "Reveal a real diagnosis cast");
            yield return Until(() => !cast.IsAnimating && cast.RevealedLines == 1, "first diagnosis line revealed");
            string diagnosisSession = cast.Session.sessionId;
            int[] diagnosisCoins = (int[])cast.Session.divination.casting.coinFaces.Clone();
            cast.CancelCasting();
            Add(!State.HasFlag("day1.diagnosis_done") && Player.GetComponent<PlayerMovement>().enabled,
                "Cancelling a partial cast retains progress and releases movement without granting a deliverable");
            Require(session.SaveManual(), "Save a partially revealed actual cast", session.LastMessage);
            session.LoadManual();
            yield return Until(() => session.Phase == GameSessionPhase.Playing && Player != null, "partial cast reload");
            PreparePlayer();
            yield return Interact("Diagnosis");
            cast = Player.GetComponent<CheckCastingUI>();
            Add(cast != null && cast.IsOpen && cast.RevealedLines == 1 && cast.Session.sessionId == diagnosisSession && cast.Session.divination.casting.coinFaces.SequenceEqual(diagnosisCoins),
                "Scene reload reuses the exact same cast, session and revealed line");
            yield return CompleteCast();
            yield return ToChoice();
            Require(Player.ChooseDialogueOption("perform"), "Submit diagnosis through the real action option");
            var diagnosed = Checks.Sessions.First(s => s.sessionId == diagnosisSession);
            Add(diagnosed.phase == CheckSessionPhase.Resolved && diagnosed.outcomeApplied && State.HasFlag("day1.diagnosis_done"),
                "The actual check applies its outcome once before the result dialogue finishes");
            var resolvedInventory = InventoryCounts();
            Require(session.SaveManual(), "Save a resolved check before closing its result dialogue", session.LastMessage);
            session.LoadManual();
            yield return Until(() => session.Phase == GameSessionPhase.Playing && Player != null, "resolved check reload");
            PreparePlayer();
            yield return Interact("Diagnosis");
            Add(Player.IsInDialogue && Player.GetComponent<CheckCastingUI>()?.IsOpen != true && SameInventory(resolvedInventory),
                "Resolved check reload opens its saved result without recasting or issuing rewards again");
            yield return FinishDialogue();
            Add(Checks.Sessions.First(s => s.sessionId == diagnosisSession).phase == CheckSessionPhase.Completed, "Finishing the restored result completes its existing check session");
            yield return Interact("LinXi"); yield return ToChoice();
            Require(Player.ChooseDialogueOption("handover"), "Select a task return before cancelling its response");
            Player.CancelDialogue();
            Add(!State.HasFlag("day1.completed.lin-xi"), "Cancelling a task return does not prematurely complete the task");
            yield return Choice("LinXi", "handover");
            Add(State.HasFlag("day1.completed.lin-xi"), "The completed diagnosis can be reported to Lin Xi");
            yield return RepeatCheckAndNpc("Diagnosis", "LinXi");

            yield return Choice("Hydrologist", "accept");
            Add(State.HasFlag("day1.quest.hydrologist") && !State.HasFlag("day1.water_deferred"), "Water sampling is accepted without pretending a sample was collected");
            yield return Interact("SeaGate"); Player.CancelDialogue();
            Add(!State.HasFlag("day1.water_deferred"), "Cancelled sea side dialogue does not defer the task early");
            yield return Interact("SeaGate"); yield return FinishDialogue();
            Add(State.HasFlag("day1.water_deferred") && !State.HasFlag("day1.water_sample_collected"), "Sea side refusal schedules water sampling for Day2 without failing it");
            yield return Interact("Hydrologist"); yield return FinishDialogue("handover");
            Add(State.HasFlag("day1.completed.hydrologist") && !State.HasFlag("day1.water_sample_collected"), "Reporting the delay completes preparation while actual water sampling stays pending");

            yield return Choice("Geologist", "accept");
            yield return CheckPoint("Plant", "day1.plant_collected");
            yield return Choice("Geologist", "handover");
            Add(State.HasFlag("day1.completed.geologist"), "Plant check and return complete the geologist task");
            yield return RepeatCheckAndNpc("Plant", "Geologist");

            yield return Choice("ContainmentResearcher", "accept");
            // A low-skill fixture exercises the actual failed check and recovery branch, without
            // modifying assets, creating results or manually setting any completion flag.
            if (Checks == null) Player.gameObject.AddComponent<CheckActorState>();
            int offspringBefore = Checks.attributes.offspring;
            Checks.attributes.offspring = 0;
            yield return CheckPoint("Repair", "day1.repair_done");
            var repairResult = Checks.Sessions.Last(s => s.eventId == "day1.repair");
            Add(repairResult.result != null && !repairResult.result.success && State.HasFlag("day1.repair_done"),
                "A real failed repair check still records the researcher's assisted repair", "Unskilled test actor; real six coin result and configured failure continuation");
            Checks.attributes.offspring = offspringBefore;
            yield return Choice("ContainmentResearcher", "handover");
            Add(State.HasFlag("day1.completed.containment-researcher"), "A failed teaching repair remains reportable and cannot lock Day1");
            yield return RepeatCheckAndNpc("Repair", "ContainmentResearcher");

            yield return Choice("Mechanic", "accept");
            yield return Interact("Power");
            Add(Player.CurrentDialogueText.Contains("检修中"), "Dock power shows its incomplete state before task delivery");
            yield return FinishDialogue();
            yield return Interact("Tools"); yield return FinishDialogue();
            Add(State.HasFlag("day1.tools_found") && State.Count("day1.tools") == 1, "Searching the real tool point grants one task tool");
            var toolInventory = InventoryCounts();
            yield return Interact("Tools"); yield return FinishDialogue();
            Add(SameInventory(toolInventory), "Repeated tool search does not duplicate the task tool");
            yield return CheckPoint("Parts", "day1.parts_collected");
            int spareCount = State.Count("day1.spare-part");
            yield return Choice("Mechanic", "handover");
            Add(State.HasFlag("day1.completed.mechanic") && State.Count("day1.tools") == 0 && State.Count("day1.spare-part") == spareCount,
                "Mechanic delivery consumes the tool and keeps separately rewarded spare parts");
            yield return Interact("Power");
            Add(Player.CurrentDialogueText.Contains("检修完成"), "Dock power changes state after mechanic delivery");
            yield return FinishDialogue();
            yield return RepeatCheckAndNpc("Parts", "Mechanic");

            yield return Choice("YangYinglong", "accept");
            yield return Interact("Threat");
            if (Player.IsInDialogue) yield return FinishDialogue("start");
            yield return Until(() => BattleController.AnyBattleActive, "teaching battle opens");
            var battle = Player.GetComponent<BattleController>();
            Require(battle != null && battle.View != null, "The threat point opens the real battle interface");
            yield return OpenBattleActions();
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Path.Combine(Application.dataPath, "../Validation/day1-battle-preview.png")));
            Add(ButtonReceivesPointer(battle.View.GetComponentsInChildren<Button>(true).First(b => b.name == battle.catalog.Skill("ATK_BASIC").displayName)),
                "The real normal attack button receives the UI raycast after the casting overlay closes");
            string battleSessionId = battle.Engine.State.sessionId;
            int[] battleCoins = (int[])battle.Engine.State.roundDivination.casting.coinFaces.Clone();
            Require(session.SaveManual(), "Save a real active teaching battle", session.LastMessage);
            session.LoadManual();
            yield return Until(() => session.Phase == GameSessionPhase.Playing && Player != null && BattleController.AnyBattleActive, "battle save reload");
            PreparePlayer(); battle = Player.GetComponent<BattleController>();
            Add(battle.Engine.State.sessionId == battleSessionId && battle.Engine.State.roundDivination.casting.coinFaces.SequenceEqual(battleCoins),
                "Scene reload restores the same battle session and fixed round cast");
            ClickBattle("逃跑", b => b.transform.parent.name == "操作面板");
            ClickBattle("逃跑", b => b.transform.parent.name == "逃跑页面");
            yield return Until(() => !BattleController.AnyBattleActive, "battle escape restores exploration");
            Add(!State.HasFlag("day1.threat_cleared") && State.HasFlag("day1.quest.yang-yinglong") && State.HasFlag("day1.completed.mechanic"),
                "Escaping the saved teaching battle restores prior story progress without a victory");
            PreparePlayer();
            yield return Interact("Threat");
            if (Player.IsInDialogue) yield return FinishDialogue("start");
            yield return Until(() => BattleController.AnyBattleActive, "teaching battle restarts after escape");
            yield return WinBattle();
            Add(State.HasFlag("day1.threat_cleared") && !BattleController.AnyBattleActive && GameSessionController.GameplayInputAllowed,
                "Real battle UI victory closes the result and unlocks exploration");
            yield return Choice("YangYinglong", "handover");
            Add(State.HasFlag("day1.completed.yang-yinglong"), "The actual threat victory can be reported to Yang Yinglong");
            yield return Interact("SeaGate"); yield return FinishDialogue();
            Add(!State.HasFlag("day1.water_sample_collected"), "Winning the peripheral battle does not open Day1 water sampling");
            Add(Flow.MetCount == 6 && new[] { "lin-xi", "hydrologist", "geologist", "yang-yinglong", "containment-researcher", "mechanic" }
                .All(id => State.HasFlag("day1.met." + id)), "All six personal introductions and preparation task returns persist");
            Add(Flow.CanFinishDay && !State.HasFlag("day1.water_sample_collected"), "Day1 completion requires preparation and deferral, never an impossible water sample");

            var completedInventory = InventoryCounts();
            yield return Interact("Rules"); yield return ToChoice();
            Require(Player.ChooseDialogueOption("finish"), "Choose the actual Day1 close out option");
            Player.CancelDialogue();
            Add(!State.HasFlag("day1.finished"), "Cancelling the final confirmation does not prematurely end Day1");
            yield return Choice("Rules", "finish");
            Add(State.HasFlag("day1.finished") && !State.HasFlag("day2.started") && !State.HasFlag("day1.water_sample_collected"),
                "Explicit close out ends only Day1 and retains the unresolved Day2 sample");
            Require(session.SaveManual(), "Save the finished Day1 checkpoint", session.LastMessage);
            ClickMenu("Settings Button"); ClickMenu("Save And Return"); ClickMenu("Continue Game");
            yield return Until(() => session.Phase == GameSessionPhase.Playing && Player != null, "finished Day1 continue reload");
            PreparePlayer();
            Add(State.HasFlag("day1.finished") && State.HasFlag("day1.water_deferred") && Flow.MetCount == 6 && SameInventory(completedInventory),
                "Continue reload preserves Day1 close out, all introductions, pending water and inventory");
            foreach (string key in new[] { "Environment", "Supplies", "Tools", "LinXi", "Geologist", "ContainmentResearcher", "Mechanic", "YangYinglong", "Hydrologist", "Rules" })
            { yield return Interact(key); yield return FinishDialogue("later"); }
            Add(SameInventory(completedInventory), "Revisiting completed dialogue and collection points after reload does not repeat rewards");
            Add(FindObjectsOfType<GameSessionController>(true).Length == 1, "Repeated Day1 reloads keep exactly one persistent session controller");
            ClickMenu("Settings Button"); ClickMenu("Save And Return");
            RestoreSession();
            Add(SameHashes(userSaveHashes, HashFiles(originalDirectory)), "Existing player save files remain byte for byte unchanged");
        }

        private IEnumerator Door(Vector2 center, Vector2 direction, string label)
        {
            MovePlayer(center - direction * 1.25f);
            var movement = Player.GetComponent<PlayerMovement>();
            movement.SetScriptedInput(direction);
            yield return new WaitForSeconds(.7f);
            movement.SetScriptedInput(Vector2.zero);
            float progress = Vector2.Dot((Vector2)Player.transform.position - center, direction);
            Add(progress > .55f, "Physics2D passage: " + label, "Door center " + center + "; crossed distance " + progress.ToString("F2"));
            yield return new WaitForFixedUpdate();
        }

        private IEnumerator CheckPoint(string key, string deliverableFlag)
        {
            yield return Interact(key);
            yield return CompleteCast();
            yield return ToChoice();
            Require(Player.ChooseDialogueOption("perform"), "Submit real check action: " + key);
            yield return FinishDialogue();
            Add(State.HasFlag(deliverableFlag), "Completed real six coin check: " + key);
        }

        private IEnumerator CompleteCast()
        {
            var cast = Player.GetComponent<CheckCastingUI>();
            Require(cast != null && cast.IsOpen, "Actual casting view is open");
            cast.QuickCast = true;
            cast.quickThrowDuration = .02f;
            if (cast.RevealedLines < 6) Require(cast.ThrowNext(), "Begin actual sequential six coin reveals");
            yield return Until(() => !cast.IsAnimating && cast.RevealedLines == 6, "actual casting animation completes");
            Require(cast.ConfirmContinue(), "Confirm the real completed divination chart");
        }

        private IEnumerator RepeatCheckAndNpc(string checkKey, string npcKey)
        {
            var inventory = InventoryCounts();
            int sessionsBefore = Checks.Sessions.Count;
            yield return Interact(checkKey); yield return FinishDialogue();
            yield return Interact(npcKey); yield return FinishDialogue("handover");
            Add(SameInventory(inventory) && Checks.Sessions.Count == sessionsBefore, "Completed check and NPC do not recast or repeat rewards: " + checkKey);
        }

        private IEnumerator OpenBattleActions()
        {
            var battle = Player.GetComponent<BattleController>();
            yield return new WaitForSecondsRealtime(.3f);
            if (battle.Engine.State.phase == BattlePhase.RoundCasting) ClickBattle("快速定卦");
            yield return Until(() => battle.Engine.State.phase == BattlePhase.Player, "battle action phase");
            if (battle.View.CastVisible) ClickBattle("快速定卦");
            yield return null;
        }

        private IEnumerator WinBattle()
        {
            var battle = Player.GetComponent<BattleController>();
            float deadline = Time.realtimeSinceStartup + 25;
            while (battle.Engine.State.phase != BattlePhase.Victory && battle.Engine.State.phase != BattlePhase.Defeat)
            {
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Teaching battle did not finish.");
                if (battle.Engine.State.phase == BattlePhase.RoundCasting) yield return OpenBattleActions();
                else if (battle.Engine.State.phase == BattlePhase.Player)
                {
                    var attack = battle.catalog.Skill("ATK_BASIC");
                    if (battle.Engine.CanUseSkill(attack.id, 0, out _)) ClickBattle(attack.displayName);
                    else ClickBattle("结束回合 →");
                }
                yield return new WaitForSecondsRealtime(.08f);
            }
            Require(battle.Engine.State.phase == BattlePhase.Victory, "Unmodified teaching encounter is winnable through real normal attack buttons");
            Add(State.HasFlag("day1.threat_cleared") && !GameSessionController.GameplayInputAllowed,
                "Battle applies its victory flag while retaining the world input lock until return");
            yield return Until(() => battle.View.GetComponentsInChildren<Button>(true).Any(b => b.name == "返回场景" && b.gameObject.activeInHierarchy && b.interactable), "battle result return button");
            ClickBattle("返回场景");
        }

        private IEnumerator Choice(string key, string choice)
        {
            yield return Interact(key);
            yield return ToChoice();
            Require(Player.ChooseDialogueOption(choice), "Dialogue choice: " + key + "/" + choice);
            yield return FinishDialogue();
        }

        private IEnumerator Interact(string key, bool expectedAllowed = true)
        {
            yield return new WaitForSeconds(.23f);
            var point = Flow.Find(key);
            if (point == null || point.Prop == null) throw new InvalidOperationException("Missing interaction: " + key);
            MovePlayer((Vector2)point.transform.position + Vector2.down * .65f);
            bool started = point.Prop.Interact(Player);
            if (expectedAllowed) Require(started, "Scene interaction available: " + key);
            else Add(!started, "Scene correctly rejects locked interaction: " + key);
            yield return null;
        }

        private IEnumerator ToChoice()
        {
            float deadline = Time.realtimeSinceStartup + 4;
            while (Player.IsInDialogue && !Player.IsAwaitingChoice)
            {
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Dialogue choice was not offered.");
                Player.AdvanceDialogue(); yield return null;
            }
            if (!Player.IsInDialogue || !Player.IsAwaitingChoice) throw new InvalidOperationException("Expected an actual dialogue choice.");
        }

        private IEnumerator FinishDialogue(string optionalChoice = null)
        {
            float deadline = Time.realtimeSinceStartup + 4;
            while (Player.IsInDialogue)
            {
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Dialogue did not finish: " + Player.CurrentDialogueText);
                if (Player.IsAwaitingChoice)
                {
                    if (string.IsNullOrEmpty(optionalChoice) || !Player.ChooseDialogueOption(optionalChoice))
                        throw new InvalidOperationException("Unexpected dialogue choice: " + string.Join(", ", Player.DialogueOptions.Select(o => o.id)));
                }
                else Player.AdvanceDialogue();
                // The automatic admission controller may open another question on its next
                // Update. Finish only this dialogue, not a newly opened automatic event.
                if (!Player.IsInDialogue) yield break;
                yield return null;
            }
        }

        private IEnumerator Until(Func<bool> condition, string label, float timeout = 20)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!condition()) { if (Time.realtimeSinceStartup > deadline) throw new TimeoutException(label); yield return null; }
        }

        private void PreparePlayer()
        {
            Player.keyboardInput = false;
            Player.GetComponent<PlayerMovement>().SetScriptedInput(Vector2.zero);
            var body = Player.GetComponent<Rigidbody2D>();
            if (body != null) body.velocity = Vector2.zero;
        }
        private void MovePlayer(Vector2 position)
        {
            Player.transform.position = new Vector3(position.x, position.y, Player.transform.position.z);
            var body = Player.GetComponent<Rigidbody2D>();
            if (body != null) { body.position = position; body.velocity = Vector2.zero; }
            Physics2D.SyncTransforms();
        }
        private void ClickMenu(string name)
        {
            var button = session.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == name);
            Require(button != null && button.interactable && button.gameObject.activeInHierarchy, "Actual menu button is available: " + name);
            button.onClick.Invoke();
        }
        private void ClickBattle(string name, Func<Button, bool> predicate = null)
        {
            var battle = Player.GetComponent<BattleController>();
            var button = battle.View.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.name == name && b.gameObject.activeInHierarchy && b.interactable && (predicate == null || predicate(b)));
            if (button == null) throw new InvalidOperationException("Battle button unavailable: " + name);
            button.onClick.Invoke();
        }
        private static bool ButtonReceivesPointer(Button button)
        {
            if (button == null || !button.gameObject.activeInHierarchy || !button.interactable || EventSystem.current == null) return false;
            Canvas.ForceUpdateCanvases();
            var rect = button.transform as RectTransform;
            var canvas = button.GetComponentInParent<Canvas>();
            if (rect == null || canvas == null) return false;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var point = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
            return hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button;
        }
        private Dictionary<string, int> InventoryCounts() => State.Inventory.ToDictionary(i => i.key, i => i.amount);
        private bool SameInventory(Dictionary<string, int> expected) => expected.Count == State.Inventory.Count && expected.All(i => State.Count(i.Key) == i.Value);
        private static Dictionary<string, string> HashFiles(string directory)
        {
            var hashes = new Dictionary<string, string>();
            if (!Directory.Exists(directory)) return hashes;
            using (var hash = SHA256.Create()) foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
                hashes.Add(file, Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(file))));
            return hashes;
        }
        private static bool SameHashes(Dictionary<string, string> a, Dictionary<string, string> b)
            => a != null && a.Count == b.Count && a.All(pair => b.TryGetValue(pair.Key, out string hash) && hash == pair.Value);
        private void Require(bool condition, string label, string observed = "")
        { Add(condition, label, observed); if (!condition) throw new InvalidOperationException(label + ": " + observed); }
        private void Add(bool condition, string label, string observed = "")
        {
            results.checks.Add(new CheckResult { name = label, passed = condition, observed = observed });
            WriteReport(results);
            Debug.Log("DAY1_CHECK " + (condition ? "PASS " : "FAIL ") + label + ": " + observed);
        }
        public static void WriteReport(Report report)
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Validation/day1-results.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
        }
        private void RestoreSession()
        {
            if (session == null || string.IsNullOrEmpty(originalDirectory)) return;
            if (session.Phase != GameSessionPhase.MainMenu && session.Phase != GameSessionPhase.Loading) session.SaveAndReturnToMenu();
            if (session.Phase == GameSessionPhase.MainMenu) session.UseTestSaveDirectory(originalDirectory);
            session.gameScenePath = originalScenePath;
            session.autoSaveInterval = originalAutoInterval;
            Application.runInBackground = originalBackground;
        }
        private void Finish()
        {
            if (finished) return;
            finished = true;
            RestoreSession();
            if (userSaveHashes != null && !results.checks.Any(c => c.name == "Existing player save files remain byte for byte unchanged"))
                Add(SameHashes(userSaveHashes, HashFiles(originalDirectory)), "Existing player save files remain byte for byte unchanged");
            results.passed = results.checks.Count > 0 && results.checks.All(c => c.passed);
            results.completedUtc = DateTime.UtcNow.ToString("O");
            WriteReport(results);
            Debug.Log("DAY1_VALIDATION_" + (results.passed ? "PASS" : "FAIL") + " checks=" + results.checks.Count);
            Completed?.Invoke(results.passed);
        }
        private void OnDestroy() { if (!finished) RestoreSession(); }
    }
}
#endif

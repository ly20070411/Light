#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emerge.Checks.Demo;
using Emerge.Checks.Divination;
using Emerge.GameFlow;
using Emerge.Props;
using PixelPrototype;
using UnityEngine;

namespace Emerge.Checks.Tests
{
    public sealed class DivinationSelfTest : MonoBehaviour
    {
        private const int FixtureSeed = 12637;
        private static readonly int[] FixtureYaos = { 9, 8, 7, 6, 9, 8 };
        private static readonly int[] FixtureModifiers = { -1, 0, 0, 0, 0, 0 };
        private static readonly string[] BehaviorLiuqin = { "父母", "子孙", "官鬼", "妻财", "兄弟" };
        [Serializable] public sealed class Check { public string name, observed; public bool passed; }
        [Serializable] public sealed class Report
        {
            public string unityVersion, scene, completedUtc, testSaveDirectory;
            public bool passed;
            public int exhaustiveCases, hexagramCount;
            public List<Check> checks = new List<Check>();
        }
        [Serializable] private sealed class LogRow { public string sessionId; public CheckTraceEntry entry; }
        [Serializable] private sealed class PreviewReport
        { public bool passed; public string completedUtc, error; public string[] screenshots; }
        public static event Action<bool> Completed;
        public static event Action<bool> PreviewCompleted;
        public bool completed;
        private bool running;
        private Report report;
        private int actorIndex;
        private readonly List<UnityEngine.Object> cleanup = new List<UnityEngine.Object>();

        public void RunChecks()
        {
            if (running || completed) return;
            running = true; report = new Report { unityVersion = Application.unityVersion, scene = gameObject.scene.path };
            StartCoroutine(GuardedRun());
        }

        private IEnumerator GuardedRun()
        {
            var routine = Run();
            while (true)
            {
                bool next; object waiting;
                try { next = routine.MoveNext(); waiting = next ? routine.Current : null; }
                catch (Exception exception) { Add("Runtime checks finish without an exception", false, exception.ToString()); break; }
                if (!next) break;
                yield return waiting;
            }
            foreach (var item in cleanup) if (item != null) Destroy(item);
            completed = true; report.completedUtc = DateTime.UtcNow.ToString("O");
            report.passed = report.checks.Count > 0 && report.checks.All(check => check.passed); WriteReport();
            Debug.Log("DIVINATION_VALIDATION_" + (report.passed ? "PASS" : "FAIL") + " checks=" + report.checks.Count);
            Completed?.Invoke(report.passed);
        }

        private IEnumerator Run()
        {
            VerifyCoins();
            VerifyFixtures();
            VerifyExhaustiveCharts();
            var definition = Event("persisted-divination");
            VerifyPreparationAndSave(definition);
            var uiRoutine = VerifyUI(Event("ui-divination"));
            while (uiRoutine.MoveNext()) yield return uiRoutine.Current;
        }

        private void VerifyCoins()
        {
            var counts = new int[4]; bool mapping = true;
            for (int bits = 0; bits < 8; bits++)
            {
                var faces = new int[18]; int sum = 6;
                for (int coin = 0; coin < 3; coin++)
                { int face = bits >> coin & 1; sum += face; for (int line = 0; line < 6; line++) faces[line * 3 + coin] = face; }
                var cast = CoinCasting.FromFaces(0, faces); counts[sum - 6]++;
                mapping &= cast.yaoValues.Length == 6 && cast.yaoValues.All(value => value == sum) && CoinCasting.IsValid(cast);
            }
            Add("Every three-coin face pattern maps to the correct yao value", mapping, "0=back=2; 1=front=3; six bottom-to-top triples");
            Add("Fair three-coin outcome weights are 1:3:3:1", counts.SequenceEqual(new[] { 1, 3, 3, 1 }), "Eight equally weighted patterns produce 6/7/8/9");
            var fixture = CoinCasting.Cast(FixtureSeed);
            Add("A fixed seed reproduces the independent document fixture", fixture.yaoValues.SequenceEqual(FixtureYaos) &&
                fixture.coinFaces.SequenceEqual(new[] { 1, 1, 1, 1, 1, 0, 1, 0, 0, 0, 0, 0, 1, 1, 1, 0, 1, 1 }), "Seed 12637 → 9,8,7,6,9,8");
            Add("Repeated seed uses exactly the same eighteen stored faces", fixture.coinFaces.SequenceEqual(CoinCasting.Cast(FixtureSeed).coinFaces) &&
                !fixture.coinFaces.SequenceEqual(CoinCasting.Cast(FixtureSeed + 1).coinFaces), "Reproducibility across repeated preparation");
            var inputs = new int[18]; var frozen = CoinCasting.FromFaces(3, inputs); inputs[0] = 1;
            Add("Casting copies its input and rejects contradictory stored totals", frozen.coinFaces[0] == 0 && frozen.yaoValues[0] == 6,
                "Caller edits cannot change a prepared cast");
            frozen.yaoValues[0] = 9;
            Add("Stored coin-face and yao disagreement is invalid", !CoinCasting.IsValid(frozen), "The eighteen faces determine all six totals");
            Add("Invalid coin input is rejected", ThrowsArgument(() => CoinCasting.FromFaces(0, null)) &&
                ThrowsArgument(() => CoinCasting.FromFaces(0, new int[17])) &&
                ThrowsArgument(() => CoinCasting.FromFaces(0, Enumerable.Repeat(2, 18).ToArray())), "Null, wrong length, and nonbinary faces");
        }

        private void VerifyFixtures()
        {
            var chart = new LiuYaoPaiPan().PaiPan("巳月", "戊子日", FixtureYaos);
            Add("Document fixture has the expected original and changed hexagrams", chart.benGuaName == "水火既济" && chart.bianGuaName == "雷山小过" &&
                chart.bengua.SequenceEqual(new[] { 1, 0, 1, 0, 1, 0 }) && chart.biangua.SequenceEqual(new[] { 0, 0, 1, 1, 0, 0 }), "Independent bottom-to-top fixture");
            Add("Document fixture fixes palace, shi and ying", chart.benGong == "坎" && chart.benGongWuxing == "水" && chart.bianGong == "兑" &&
                chart.benShiType == 3 && chart.shiYaoIndex == 2 && chart.yingYaoIndex == 5, "Shi is third yao; ying is sixth");
            Add("Document fixture has the independently expected original najia", chart.yaos.Select(line => line.benGanzhi).SequenceEqual(new[] { "己卯", "己丑", "己亥", "戊申", "戊戌", "戊子" }) &&
                chart.yaos.Select(line => line.benWuxing).SequenceEqual(new[] { "木", "土", "水", "金", "土", "水" }) &&
                chart.yaos.Select(line => line.benLiuqin).SequenceEqual(new[] { "子孙", "官鬼", "兄弟", "父母", "官鬼", "兄弟" }), "Six original branches, elements and relationships");
            Add("Changed relationships continue to use the original palace", chart.yaos.Select(line => line.bianGanzhi).SequenceEqual(new[] { "丙辰", "丙午", "丙申", "庚午", "庚申", "庚戌" }) &&
                chart.yaos.Select(line => line.bianLiuqin).SequenceEqual(new[] { "官鬼", "妻财", "父母", "妻财", "父母", "官鬼" }), "Changed hexagram uses original water palace for liuqin");
            Add("Document fixture has expected six spirits and moving positions", chart.yaos.Select(line => line.liushen).SequenceEqual(new[] { "勾陈", "螣蛇", "白虎", "玄武", "青龙", "朱雀" }) &&
                chart.yaos.Where(line => line.isDongYao).Select(line => line.index).SequenceEqual(new[] { 0, 3, 4 }), "Day stem determines spirit offset; only 6 and 9 move");
            Add("Document fixture preserves the negative maximum and missing class zero", chart.yaos.Select(line => line.wangshuaiScore).SequenceEqual(new[] { 0, 0, 0, -1, 0, 0 }) &&
                chart.behaviorModifiers.SequenceEqual(FixtureModifiers) && chart.liuqinSummary["父母"] == -1 && chart.liuqinSummary["妻财"] == 0,
                "Modifiers are [-1,0,0,0,0,0], not clamped to zero");
            var qian = new LiuYaoPaiPan().PaiPan("巳月", "戊子日", Enumerable.Repeat(7, 6).ToArray());
            Add("Independent static qian scoring fixture matches all six expected values", qian.benGuaName == "乾为天" && qian.bianGuaName == "乾为天" &&
                qian.yaos.Select(line => line.wangshuaiScore).SequenceEqual(new[] { 0, 1, 1, -1, -1, 1 }) &&
                qian.behaviorModifiers.SequenceEqual(new[] { 1, 0, -1, 1, -1, 1 }), "Month/day only, no moving lines; Self is shi score +1");
            var kun = new LiuYaoPaiPan().PaiPan("巳月", "戊子日", Enumerable.Repeat(8, 6).ToArray());
            Add("Independent static kun fixture retains negative offspring and Self", kun.benGuaName == "坤为地" &&
                kun.yaos.Select(line => line.wangshuaiScore).SequenceEqual(new[] { 1, -1, 1, 1, 0, -1 }) &&
                kun.behaviorModifiers.SequenceEqual(new[] { -1, -1, 1, 0, 1, -1 }), "Self takes sixth/shi yao -1");
            Add("Calendar forms normalize without changing scores", LiuYaoPaiPan.TryNormalizeCalendar("巳", "戊子", out string month, out string day, out _) &&
                month == "巳月" && day == "戊子日" && new LiuYaoPaiPan().PaiPan("巳", "戊子", FixtureYaos).behaviorModifiers.SequenceEqual(FixtureModifiers), "Explicit month and stem/branch day");
            Add("Malformed chart input fails before calculation", ThrowsArgument(() => new LiuYaoPaiPan().PaiPan("巳月", "戊子日", null)) &&
                ThrowsArgument(() => new LiuYaoPaiPan().PaiPan("巳月", "戊子日", new[] { 7, 7, 7, 7, 7 })) &&
                ThrowsArgument(() => new LiuYaoPaiPan().PaiPan("巳月", "戊子日", new[] { 7, 7, 7, 7, 7, 10 })) &&
                !LiuYaoPaiPan.TryNormalizeCalendar("未知月", "戊子日", out _, out _, out _) &&
                !LiuYaoPaiPan.TryNormalizeCalendar("巳月", "未知日", out _, out _, out _), "Illegal lengths, values and calendar inputs");
            string display = new LiuYaoPaiPan().PaiPanToString(chart);
            Add("Human-readable chart output contains both named hexagrams", !string.IsNullOrWhiteSpace(display) && display.Contains("水火既济") && display.Contains("雷山小过"), "Reusable chart printout");
        }

        private void VerifyExhaustiveCharts()
        {
            var calculator = new LiuYaoPaiPan(); var names = new HashSet<string>();
            bool full = true, aggregation = true, self = true, geometry = true; bool sawMissing = false, sawNegative = false;
            string firstFailure = "";
            for (int code = 0; code < 4096; code++)
            {
                int remainder = code; var values = new int[6];
                for (int index = 0; index < 6; index++) { values[index] = 6 + remainder % 4; remainder /= 4; }
                var chart = calculator.PaiPan("巳月", "戊子日", values); names.Add(chart.benGuaName);
                if (!LiuYaoPaiPan.IsValidResult(chart)) { full = false; if (firstFailure == "") firstFailure = string.Join(",", values); }
                geometry &= chart.yaos.Count == 6 && chart.yaos.Count(line => line.isShi) == 1 && chart.yaos.Count(line => line.isYing) == 1 &&
                    chart.yingYaoIndex == (chart.shiYaoIndex + 3) % 6;
                for (int index = 0; index < 6; index++)
                {
                    int original = values[index] == 7 || values[index] == 9 ? 1 : 0;
                    int changed = values[index] == 6 || values[index] == 7 ? 1 : 0;
                    geometry &= chart.bengua[index] == original && chart.biangua[index] == changed && chart.yaos[index].index == index &&
                        chart.yaos[index].isDongYao == (values[index] == 6 || values[index] == 9);
                }
                for (int behavior = 0; behavior < 5; behavior++)
                {
                    var lines = chart.yaos.Where(line => line.benLiuqin == BehaviorLiuqin[behavior]).ToArray();
                    int expected = lines.Length == 0 ? 0 : lines.Max(line => line.wangshuaiScore);
                    aggregation &= chart.behaviorModifiers[behavior] == expected && chart.liuqinSummary[BehaviorLiuqin[behavior]] == expected;
                    sawMissing |= lines.Length == 0; sawNegative |= expected < 0;
                }
                self &= chart.behaviorModifiers[(int)CheckBehavior.Self] == chart.yaos[chart.shiYaoIndex].wangshuaiScore;
            }
            report.exhaustiveCases = 4096; report.hexagramCount = names.Count;
            Add("All 4096 legal six-yao combinations produce valid charts", full, "4^6 cases; first invalid input: " + firstFailure);
            Add("The complete sixty-four-hexagram table is covered", names.Count == 64, "Distinct original hexagrams = " + names.Count);
            Add("Every chart preserves bottom-to-top yin/yang, moving lines, shi and ying", geometry, "All 4096 inputs");
            Add("Every repeated liuqin uses its highest score, with missing classes zero", aggregation && sawMissing && sawNegative, "Exhaustive cases include absent classes and all-negative classes");
            Add("Self equals its original shi yao score in every chart", self, "All 4096 inputs");
        }

        private void VerifyPreparationAndSave(CheckEventDefinition definition)
        {
            var actor = Actor("Persistence"); var state = actor.GetComponent<CheckActorState>();
            var session = state.GetOrPrepare(definition, "persisted-context");
            Add("Real pipeline creates one frozen Preparing cast without early modifiers", session.phase == CheckSessionPhase.Preparing && session.divination != null &&
                session.divination.casting.yaoValues.SequenceEqual(FixtureYaos) && session.divination.revealedLines == 0 && session.divination.chart == null &&
                session.modifiers.All(value => value == 0), "Real providers are the default");
            Add("Unfinished preparation cannot resolve or become Ready", !state.TryResolve(definition, session, "learn", out _, out _) &&
                !state.FinishPreparation(session, out _) && session.phase == CheckSessionPhase.Preparing && session.result == null, "No choice before six completed throws");
            Add("Only completed throws reveal one bottom-up line each", state.RevealNextCast(session) && state.RevealNextCast(session) && session.divination.revealedLines == 2,
                "Two reveals use six of the eighteen already-frozen faces");
            int[] frozenFaces = (int[])session.divination.casting.coinFaces.Clone();
            state.attributes.parent = -500; definition.divinationSeed = FixtureSeed + 1; definition.divinationMonth = "子月";
            var reopened = state.GetOrPrepare(definition, "persisted-context");
            Add("Reopening keeps revealed lines, original calendar, seed and attributes", ReferenceEquals(reopened, session) && session.divination.revealedLines == 2 &&
                session.divination.month == "巳月" && session.divination.casting.seed == FixtureSeed && session.divination.casting.coinFaces.SequenceEqual(frozenFaces) &&
                session.attributes.parent == 8 && state.Sessions.Count == 1, "Live definition edits cannot reroll this event");
            var saved = state.CaptureSnapshot();
            Add("Partially revealed Preparing sessions are valid saved data", CheckActorState.IsValidSnapshot(saved) && saved.sessions[0].phase == CheckSessionPhase.Preparing &&
                saved.sessions[0].divination.revealedLines == 2 && saved.sessions[0].divination.chart == null, "Serializes null result/chart correctly");
            var corrupt = state.CaptureSnapshot(); corrupt.sessions[0].divination.casting.coinFaces[0] ^= 1;
            Add("Corrupt stored faces are rejected without replacing live data", !CheckActorState.IsValidSnapshot(corrupt) && !state.RestoreSnapshot(corrupt) &&
                session.divination.casting.coinFaces.SequenceEqual(frozenFaces), "Face/yao disagreement");
            corrupt = state.CaptureSnapshot(); corrupt.sessions[0].divination.revealedLines = 7;
            Add("Out-of-range reveal progress is rejected", !CheckActorState.IsValidSnapshot(corrupt), "Seven reveals cannot belong to a six-line cast");
            corrupt = state.CaptureSnapshot(); corrupt.sessions[0].modifiers[0] = 2;
            Add("Preparing sessions cannot smuggle ready modifiers", !CheckActorState.IsValidSnapshot(corrupt), "Modifiers must remain zero until chart completion");
            corrupt = state.CaptureSnapshot(); corrupt.sessions[0].divination.month = "巳";
            Add("Saved calendar values must already be normalized", !CheckActorState.IsValidSnapshot(corrupt), "A save stores 巳月, rather than a shorthand that changes on restore");
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/DivinationSaveTest_" + Guid.NewGuid().ToString("N")));
            report.testSaveDirectory = directory; var store = new GameSaveStore(directory);
            var data = SaveData(actor, saved);
            bool written = store.TryWrite(SaveSlot.Manual, data, out string error);
            GameSaveData loaded = null; bool read = written && store.TryRead(SaveSlot.Manual, out loaded, out error);
            Add("Real game save writes and reads partially revealed divination", read && loaded.actors[0].checkState.sessions[0].divination.revealedLines == 2 &&
                loaded.actors[0].checkState.sessions[0].divination.casting.coinFaces.SequenceEqual(frozenFaces), error ?? "Checksummed game save round trip");
            var restored = Actor("Restored"); var restoredState = restored.GetComponent<CheckActorState>();
            bool restoredOk = read && restored.State.RestoreSnapshot(loaded.actors[0].propState) && restoredState.RestoreSnapshot(loaded.actors[0].checkState);
            var restoredSession = restoredOk ? restoredState.GetOrPrepare(definition, "persisted-context") : null;
            Add("Save restoration resumes the same two revealed lines without reroll", restoredOk && restoredSession.sessionId == session.sessionId &&
                restoredSession.divination.revealedLines == 2 && restoredSession.divination.casting.seed == FixtureSeed, "Same context and session identity");
            if (!restoredOk) throw new InvalidOperationException("Cannot continue a failed Preparing-save restore.");
            for (int i = 2; i < 6; i++) if (!restoredState.RevealNextCast(restoredSession)) throw new InvalidOperationException("Cannot reveal persisted line.");
            Add("The resumed six-line cast computes the independent fixture modifiers", restoredState.FinishPreparation(restoredSession, out _) &&
                restoredSession.phase == CheckSessionPhase.Ready && restoredSession.modifiers.SequenceEqual(FixtureModifiers) &&
                restoredSession.divination.chart.benGuaName == "水火既济", "Stored calendar and faces drive the chart");
            int traceCount = restoredSession.logicTrace.Count;
            Add("Repeated preparation confirmation neither rerolls nor repeats chart logs", restoredState.FinishPreparation(restoredSession, out _) &&
                !restoredState.RevealNextCast(restoredSession) && restoredSession.logicTrace.Count == traceCount &&
                restoredSession.divination.casting.coinFaces.SequenceEqual(frozenFaces), "Idempotent Ready preparation");
            corrupt = restoredState.CaptureSnapshot(); corrupt.sessions[0].divination.chart.yaos[0].wangshuaiScore++;
            Add("Corrupt per-yao chart scores fail independent recomputation", !CheckActorState.IsValidSnapshot(corrupt) &&
                !store.TryWrite(SaveSlot.Auto, SaveData(restored, corrupt), out _), "Derived chart details are verified at save boundaries");
            int originalModifier = restoredSession.modifiers[0]; restoredSession.modifiers[0]++;
            Add("Ready confirmation and submission reject modifiers inconsistent with its chart", !restoredState.FinishPreparation(restoredSession, out _) &&
                !restoredState.TryResolve(definition, restoredSession, "learn", out _, out _) && restoredSession.phase == CheckSessionPhase.Ready &&
                restoredSession.result == null && !restoredSession.outcomeApplied, "Both entry points enforce chart/modifier agreement");
            restoredSession.modifiers[0] = originalModifier;
            Add("Prepared negative modifier enters the normal >= resolver", restoredState.TryResolve(definition, restoredSession, "learn", out var result, out _) &&
                result.baseValue == 8 && result.modifier == -1 && result.finalValue == 7 && result.targetValue == 7 && result.success,
                "8 - 1 = 7 >= 7");
            Add("Real divination outcome is applied exactly once", restored.State.HasFlag("divination-success") &&
                !restoredState.TryResolve(definition, restoredSession, "learn", out _, out _), "Selection seals the persisted session");
        }

        private IEnumerator VerifyUI(CheckEventDefinition definition)
        {
            var configuredTerminal = FindObjectsOfType<PropInstance>().FirstOrDefault(item => item.gameObject.scene.path == CheckDemoContext.ScenePath &&
                item.Definition != null && item.Definition.checkEvent != null);
            var configuredEvent = configuredTerminal == null ? null : configuredTerminal.Definition.checkEvent;
            Add("Serialized observation-gate demo enables the real divination pipeline", configuredEvent != null && configuredEvent.useDivination &&
                configuredEvent.Validate(out _) && configuredEvent.divinationMonth == "巳月" && configuredEvent.divinationDay == "戊子日",
                "The playable scene's existing terminal uses the new preparation flow");
            var actor = FindObjectsOfType<PlayerInteractor>().FirstOrDefault(item => item.gameObject.scene.path == CheckDemoContext.ScenePath &&
                item.GetComponent<CheckActorState>() != null && item.GetComponent<CheckActorState>().Sessions.Count == 0);
            Add("Real demo player is available for the casting UI test", actor != null && actor.GetComponent<SaveIdentity>() != null, "Serialized ChecksDemo player");
            if (actor == null) throw new InvalidOperationException("Open ChecksDemo with a fresh player to validate the UI.");
            actor.keyboardInput = false; actor.showUI = true; actor.GetComponent<PlayerMovement>().SetScriptedInput(Vector2.zero);
            actor.GetComponent<CheckActorState>().attributes = Attributes();
            var owner = Owner(definition, actor.transform.position + Vector3.up);
            Add("Real event entrance opens casting before action choices", owner.Interact(actor) && actor.IsInDialogue && !actor.IsAwaitingChoice &&
                !actor.GetComponent<PlayerMovement>().enabled, "Prop interaction → Preparing → casting UI");
            var ui = actor.GetComponent<CheckCastingUI>();
            if (ui == null) throw new InvalidOperationException("No casting view was installed.");
            ui.throwDuration = .08f; ui.quickThrowDuration = .08f; ui.QuickCast = false;
            var session = ui.Session; var state = actor.GetComponent<CheckActorState>();
            int[] faces = (int[])session.divination.casting.coinFaces.Clone();
            Add("Confirmation before six casts and duplicate animation clicks are blocked", !ui.ConfirmContinue() && ui.ThrowNext() && !ui.ThrowNext() &&
                ui.IsAnimating && ui.RevealedLines == 0, "Animation changes presentation only");
            var wait = WaitIdle(ui); while (wait.MoveNext()) yield return wait.Current;
            Add("First animation reveals only the already prepared bottom line", ui.RevealedLines == 1 && session.divination.casting.coinFaces.SequenceEqual(faces) &&
                session.phase == CheckSessionPhase.Preparing, "No extra random outcome from animation frames");
            ui.ThrowNext(); ui.CancelCasting();
            Add("Cancelling mid-animation preserves completed lines and releases movement", !ui.IsOpen && !actor.IsInDialogue &&
                actor.GetComponent<PlayerMovement>().enabled && session.divination.revealedLines == 1 && !session.outcomeApplied, "The unfinished visual throw was never revealed");
            Add("Re-entry resumes the same cast rather than preparing new random faces", owner.Interact(actor) && ReferenceEquals(ui.Session, session) &&
                ui.RevealedLines == 1 && session.divination.casting.coinFaces.SequenceEqual(faces), "One session per physical encounter");
            ui.QuickCast = true;
            Add("Quick cast begins from the saved reveal count", ui.ThrowNext(), "Only remaining five lines are animated");
            wait = WaitIdle(ui); while (wait.MoveNext()) yield return wait.Current;
            Add("Quick mode finishes all remaining lines and computes real chart modifiers", ui.RevealedLines == 6 && session.phase == CheckSessionPhase.Ready &&
                session.modifiers.SequenceEqual(FixtureModifiers) && session.divination.casting.coinFaces.SequenceEqual(faces) && ui.IsOpen,
                "Six throws → chart and modifiers; choice still requires confirmation");
            Add("Casting UI hands off to normal choices only after confirmation", ui.ConfirmContinue() && !ui.IsOpen && actor.IsInDialogue &&
                !actor.GetComponent<PlayerMovement>().enabled, "The dialogue owns the movement lock after handoff");
            ReachChoice(actor);
            var choice = actor.DialogueOptions.FirstOrDefault(item => item.id == "learn");
            Add("Actual action labels display the computed negative modifier and total", choice != null && choice.enabled && choice.label.Contains("= 7") &&
                !choice.label.Contains("目标"), choice?.label ?? "No matching action choice");
            Add("Old casting controls cannot submit another animation or handoff", !ui.ThrowNext() && !ui.ConfirmContinue(), "Closed view has no live actions");
            int events = 0; state.Resolved += result => events++;
            Add("Real choice callback resolves equality using the computed modifier", actor.ChooseDialogueOption("learn") && session.result != null &&
                session.result.modifier == -1 && session.result.finalValue == 7 && session.result.success && events == 1, "Real casting UI → real PlayerInteractor callback → >= result");
            string story = FinishDialogue(actor);
            Add("Real result and continuation complete the session and restore movement", story.Contains("继续调查") && session.phase == CheckSessionPhase.Completed &&
                actor.GetComponent<PlayerMovement>().enabled && actor.State.HasFlag("divination-success"), "Success branch and subsequent story");
            VerifyTrace(session);
            var failure = Actor("RealFailure"); var failureState = failure.GetComponent<CheckActorState>();
            var failureSession = failureState.GetOrPrepare(definition, "failure-context");
            for (int i = 0; i < 6; i++) failureState.RevealNextCast(failureSession);
            failureState.FinishPreparation(failureSession, out _);
            Add("The same real chart also supports a normal failure comparison", failureState.TryResolve(definition, failureSession, "force", out var failed, out _) &&
                failed.finalValue == 10 && failed.modifier == 0 && failed.targetValue == 12 && !failed.success && failure.State.HasFlag("divination-side-route"),
                "10 + 0 < 12; failure has a continuation");
        }

        private void VerifyTrace(CheckSession session)
        {
            var trace = session.logicTrace;
            bool sequence = trace != null && trace.Select((entry, index) => entry != null && entry.sequence == index + 1 &&
                !string.IsNullOrWhiteSpace(entry.utc)).All(valid => valid);
            string[] chain = { "事件入口", "起卦输入", "投掷", "填爻", "排盘", "爻加值", "行为加值", "准备确认", "选择行动", "提交行动", "判定", "应用后果", "结果分支", "继续剧情", "剧情完成" };
            int previous = -1; bool ordered = true;
            foreach (string stage in chain)
            { int index = trace.FindIndex(entry => entry.stage == stage); ordered &= index > previous; previous = index; }
            Add("Runtime logic chain preserves stage order and sequential entries", sequence && ordered, string.Join(" → ", chain));
            Add("Logic logs count completed throws and calculations once, independent of animation frames", trace.Count(entry => entry.stage == "事件入口") == 1 &&
                trace.Count(entry => entry.stage == "起卦输入") == 1 && trace.Count(entry => entry.stage == "投掷") == 6 && trace.Count(entry => entry.stage == "填爻") == 6 &&
                trace.Count(entry => entry.stage == "排盘") == 1 && trace.Count(entry => entry.stage == "爻加值") == 6 && trace.Count(entry => entry.stage == "行为加值") == SixKinAttributes.Count &&
                trace.Count(entry => entry.stage == "判定") == 1 && trace.Count(entry => entry.stage == "应用后果") == 1, "Includes cancellation and reopened casting without duplicate logical throws");
            var rows = File.Exists(CheckLogicTrace.LogPath) ? File.ReadLines(CheckLogicTrace.LogPath).Where(line => line.Contains(session.sessionId))
                .Select(line => JsonUtility.FromJson<LogRow>(line)).ToArray() : new LogRow[0];
            Add("Each session trace entry is also written to the runtime JSONL log", rows.Length == trace.Count &&
                rows.Select((row, index) => row.entry != null && row.entry.sequence == trace[index].sequence && row.entry.stage == trace[index].stage).All(valid => valid),
                CheckLogicTrace.LogPath);
        }

        public void BeginPreview()
        {
            Directory.CreateDirectory(ProjectPath("Validation"));
            StartCoroutine(GuardedPreview());
        }
        private IEnumerator GuardedPreview()
        {
            var routine = Preview(); string error = null;
            while (true)
            {
                bool next; object waiting;
                try { next = routine.MoveNext(); waiting = next ? routine.Current : null; }
                catch (Exception exception) { error = exception.ToString(); break; }
                if (!next) break;
                yield return waiting;
            }
            string[] files = { "divination-animation-preview.png", "divination-casting-preview.png", "divination-chart-preview.png", "divination-choice-preview.png" };
            bool passed = error == null && files.All(file => File.Exists(ProjectPath("Validation/" + file)));
            File.WriteAllText(ProjectPath("Validation/divination-preview.json"), JsonUtility.ToJson(new PreviewReport
            { passed = passed, completedUtc = DateTime.UtcNow.ToString("O"), error = error, screenshots = files }, true));
            PreviewCompleted?.Invoke(passed);
        }

        private IEnumerator Preview()
        {
            var actor = FindObjectsOfType<PlayerInteractor>().FirstOrDefault(item => item.gameObject.scene.path == CheckDemoContext.ScenePath &&
                item.GetComponent<CheckActorState>() != null && item.GetComponent<CheckActorState>().Sessions.Count == 0);
            if (actor == null || !GameSessionController.GameplayInputAllowed) throw new InvalidOperationException("A fresh active ChecksDemo player is required.");
            actor.GetComponent<CheckActorState>().attributes = Attributes(); actor.keyboardInput = false; actor.showUI = true;
            actor.GetComponent<PlayerMovement>().SetScriptedInput(Vector2.zero);
            var owner = Owner(Event("preview-divination"), actor.transform.position + Vector3.up);
            if (!owner.Interact(actor)) throw new InvalidOperationException("Cannot open the casting preview.");
            var ui = actor.GetComponent<CheckCastingUI>(); ui.throwDuration = 1.5f; ui.quickThrowDuration = .08f;
            ui.QuickCast = false; ui.ThrowNext();
            yield return new WaitForSecondsRealtime(.5f);
            yield return new WaitForEndOfFrame();
            DateTime captureRequested = DateTime.UtcNow;
            string imagePath = ProjectPath("Validation/divination-animation-preview.png");
            ScreenCapture.CaptureScreenshot(imagePath);
            var capture = WaitScreenshot(imagePath, captureRequested); while (capture.MoveNext()) yield return capture.Current;
            var wait = WaitIdle(ui); while (wait.MoveNext()) yield return wait.Current;
            yield return new WaitForEndOfFrame(); captureRequested = DateTime.UtcNow;
            imagePath = ProjectPath("Validation/divination-casting-preview.png"); ScreenCapture.CaptureScreenshot(imagePath);
            capture = WaitScreenshot(imagePath, captureRequested); while (capture.MoveNext()) yield return capture.Current;
            ui.QuickCast = true; ui.ThrowNext(); wait = WaitIdle(ui); while (wait.MoveNext()) yield return wait.Current;
            yield return new WaitForEndOfFrame(); captureRequested = DateTime.UtcNow;
            imagePath = ProjectPath("Validation/divination-chart-preview.png"); ScreenCapture.CaptureScreenshot(imagePath);
            capture = WaitScreenshot(imagePath, captureRequested); while (capture.MoveNext()) yield return capture.Current;
            if (!ui.ConfirmContinue()) throw new InvalidOperationException("Cannot confirm the completed chart.");
            ReachChoice(actor);
            yield return new WaitForEndOfFrame(); captureRequested = DateTime.UtcNow;
            imagePath = ProjectPath("Validation/divination-choice-preview.png"); ScreenCapture.CaptureScreenshot(imagePath);
            capture = WaitScreenshot(imagePath, captureRequested); while (capture.MoveNext()) yield return capture.Current;
        }

        private CheckEventDefinition Event(string id)
        {
            var definition = ScriptableObject.CreateInstance<CheckEventDefinition>(); cleanup.Add(definition);
            definition.eventId = id; definition.title = "观测站门控 · 六爻检定"; definition.speaker = "观测站终端";
            definition.intro = "当前局势已经排定。选择研究门控记录，或强行处理门锁。"; definition.completedText = "沿通道继续调查。";
            definition.useDivination = true; definition.divinationMonth = "巳月"; definition.divinationDay = "戊子日";
            definition.useFixedDivinationSeed = true; definition.divinationSeed = FixtureSeed;
            definition.options = new List<CheckOptionDefinition>
            {
                new CheckOptionDefinition { id = "learn", label = "研究门控记录", behavior = CheckBehavior.Parent, targetValue = 7,
                    success = new CheckOutcomeDefinition { text = "你读懂记录中的开门协议。", continuation = "隔离门开启，你进入观测站继续调查。", grantedFlags = new[] { "divination-success" } },
                    failure = new CheckOutcomeDefinition { text = "记录仍然模糊。你找到了一条检修侧路。", continuation = "沿侧路继续调查。", grantedFlags = new[] { "divination-side-route" } } },
                new CheckOptionDefinition { id = "force", label = "强行处理门锁", behavior = CheckBehavior.Wealth, targetValue = 12,
                    success = new CheckOutcomeDefinition { text = "门锁脱离锁定。", continuation = "沿正门继续调查。", grantedFlags = new[] { "divination-success" } },
                    failure = new CheckOutcomeDefinition { text = "门锁失控，你退向检修侧路。", continuation = "沿侧路继续调查。", grantedFlags = new[] { "divination-side-route" } } }
            };
            return definition;
        }
        private PropInstance Owner(CheckEventDefinition definition, Vector3 position)
        {
            var propDefinition = ScriptableObject.CreateInstance<PropDefinition>(); cleanup.Add(propDefinition);
            propDefinition.displayName = "起卦验证终端"; propDefinition.actions = PropActions.Dialogue; propDefinition.cooldown = 0;
            propDefinition.interactionRange = 3; propDefinition.checkEvent = definition;
            var root = new GameObject("起卦验证终端（临时）"); cleanup.Add(root); root.transform.position = position;
            var owner = root.AddComponent<PropInstance>(); owner.Configure(propDefinition); Physics2D.SyncTransforms(); return owner;
        }
        private PlayerInteractor Actor(string label)
        {
            var root = new GameObject("Divination test " + label); cleanup.Add(root); root.transform.position = new Vector3(100 + actorIndex++ * 10, 100, 0);
            var body = root.AddComponent<Rigidbody2D>(); body.gravityScale = 0; body.constraints = RigidbodyConstraints2D.FreezeRotation;
            root.AddComponent<PlayerMovement>().SetScriptedInput(Vector2.zero); root.AddComponent<PropGameState>();
            var actor = root.AddComponent<PlayerInteractor>(); actor.keyboardInput = false; actor.showUI = false;
            root.AddComponent<CheckActorState>().attributes = Attributes(); return actor;
        }
        private static ActorCheckAttributes Attributes() => new ActorCheckAttributes { parent = 8, offspring = 9, officer = 7, wealth = 10, sibling = 8, self = 8 };
        private static GameSaveData SaveData(PlayerInteractor actor, CheckActorState.Snapshot checks)
        {
            var data = new GameSaveData { savedUtcTicks = DateTime.UtcNow.Ticks, scenePath = CheckDemoContext.ScenePath, playerId = "divination-test-player" };
            data.actors.Add(new SavedActor { id = data.playerId, propState = actor.State.CaptureSnapshot(), checkState = checks }); return data;
        }
        private static IEnumerator WaitIdle(CheckCastingUI ui)
        {
            float until = Time.realtimeSinceStartup + 8;
            while (ui.IsAnimating && Time.realtimeSinceStartup < until) yield return null;
            if (ui.IsAnimating) throw new TimeoutException("Casting animation did not finish in eight seconds.");
        }
        private static IEnumerator WaitScreenshot(string path, DateTime requestedUtc)
        {
            float until = Time.realtimeSinceStartup + 5;
            while ((!File.Exists(path) || File.GetLastWriteTimeUtc(path) < requestedUtc || new FileInfo(path).Length == 0) &&
                Time.realtimeSinceStartup < until) yield return null;
            if (!File.Exists(path) || File.GetLastWriteTimeUtc(path) < requestedUtc || new FileInfo(path).Length == 0)
                throw new TimeoutException("A fresh screenshot was not written: " + path);
        }
        private static void ReachChoice(PlayerInteractor actor)
        { int remaining = 20; while (actor.IsInDialogue && !actor.IsAwaitingChoice && remaining-- > 0) actor.AdvanceDialogue(); }
        private static string FinishDialogue(PlayerInteractor actor)
        {
            string text = ""; int remaining = 30;
            while (actor.IsInDialogue && !actor.IsAwaitingChoice && remaining-- > 0) { text += actor.CurrentDialogueText + "\n"; actor.AdvanceDialogue(); }
            return text;
        }
        private static bool ThrowsArgument(Action action)
        { try { action(); return false; } catch (ArgumentException) { return true; } }
        private void Add(string name, bool passed, string observed)
        { report.checks.Add(new Check { name = name, passed = passed, observed = observed }); Debug.Log("DIVINATION_CHECK " + (passed ? "PASS " : "FAIL ") + name + ": " + observed); WriteReport(); }
        private void WriteReport()
        {
            string path = ProjectPath("Validation/divination-results.json");
            string backups = ProjectPath("Temp/DivinationReports");
            Directory.CreateDirectory(Path.GetDirectoryName(path)); Directory.CreateDirectory(backups);
            string temporary = Path.Combine(backups, Guid.NewGuid().ToString("N") + ".tmp");
            File.WriteAllText(temporary, JsonUtility.ToJson(report, true));
            // A report viewer may hold a mapped file; replace it without truncating its data.
            if (File.Exists(path)) File.Replace(temporary, path, Path.Combine(backups, Guid.NewGuid().ToString("N") + ".json"));
            else File.Move(temporary, path);
        }
        private static string ProjectPath(string path) => Path.GetFullPath(Path.Combine(Application.dataPath, "../" + path));
    }
}
#endif

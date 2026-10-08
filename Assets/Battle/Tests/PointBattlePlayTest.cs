#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emerge.Checks;
using Emerge.GameFlow;
using Emerge.Props;
using Emerge.Battle.Demo;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Emerge.Battle.Tests
{
    public sealed class PointBattlePlayTest : MonoBehaviour
    {
        public static event Action<bool> Completed;
        private GameSessionController session;
        private void Awake() { DontDestroyOnLoad(gameObject); }
        private IEnumerator Start()
        {
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (stack.Count > 0)
            {
                bool next; object current;
                try { next = stack.Peek().MoveNext(); current = next ? stack.Peek().Current : null; }
                catch (Exception ex) { PointBattleTest.Check("Actual runtime flow completes", false, ex.ToString()); break; }
                if (!next) { stack.Pop(); continue; }
                if (current is IEnumerator nested) stack.Push(nested); else yield return current;
            }
            PointBattleTest.Write(); Completed?.Invoke(PointBattleTest.Results.passed);
        }
        private IEnumerator Until(Func<bool> test)
        {
            float end = Time.realtimeSinceStartup + 25;
            while (!test()) { if (Time.realtimeSinceStartup > end) throw new TimeoutException("Point battle play-flow timeout"); yield return null; }
        }
        private PlayerInteractor Player() => FindObjectsOfType<PlayerInteractor>().First(p => p.GetComponent<PixelPrototype.PlayerMovement>() != null);
        private void Click(string name) => session.GetComponentsInChildren<Button>(true).Single(b => b.name == name).onClick.Invoke();
        private IEnumerator Capture(string name)
        {
            if (Application.isBatchMode) yield break;
            // Let the shared entry fade finish before inspecting the layout.
            yield return new WaitForSecondsRealtime(.3f);
            var gameView = typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameView != null) UnityEditor.EditorWindow.GetWindow(gameView).Focus();
            yield return null; yield return new WaitForEndOfFrame();
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Validation/" + name));
            DateTime now = DateTime.UtcNow; ScreenCapture.CaptureScreenshot(path);
            yield return Until(() => File.Exists(path) && File.GetLastWriteTimeUtc(path) >= now && new FileInfo(path).Length > 0);
            PointBattleTest.Check("Actual UI screenshot: " + name, true, path);
        }
        private void CreateConfigurationZone()
        {
            var root = new GameObject("测试用仓储区"); var zone = root.AddComponent<PointConfigurationZone>();
            zone.warehouse = true; zone.size = new Vector2(1000, 1000); root.AddComponent<PointRefitStation>().range = 1000;
        }
        private IEnumerator ReloadManual()
        { session.OpenSettings(); session.LoadManual(); yield return Until(() => session.Phase == GameSessionPhase.Playing); }
        private IEnumerator Run()
        {
            session = new GameObject("点数战斗验证隔离菜单").AddComponent<GameSessionController>();
            yield return Until(() => session.IsReady);
            session.UseTestSaveDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/PointBattlePlayChecks_" + Guid.NewGuid().ToString("N"))));
            session.gameScenePath = "Assets/Scenes/PropsDemo.unity"; session.autoSaveInterval = 600;
            Click("New Game"); var build = SixKinAttributes.DefaultBuild();
            for (int i = 0; i < 6; i++) for (int j = 1; j < build.Get((CheckBehavior)i); j++) Click("Attribute Plus " + (CheckBehavior)i);
            Click("Confirm Attributes"); yield return Until(() => session.Phase == GameSessionPhase.Playing);
            var player = Player(); player.keyboardInput = false;
            CreateConfigurationZone();
            var checks = player.GetComponent<CheckActorState>();
            player.State.AddItem("points.sword", "剑", 1); player.State.AddItem("points.talisman", "符咒", 1); player.State.AddItem("day1.identity-card", "身份认证卡", 1);
            checks.TryEquipPointItem("points.sword", CheckBehavior.Wealth, out _); checks.TryEquipPointItem("points.talisman", CheckBehavior.Wealth, out _);
            checks.CreateMentalAnchor(MentalAnchors.LinXi); checks.TryEquipAnchor(MentalAnchors.LinXi, out _);
            checks.EnsureRefitOffers(out _); string bagId = checks.PointBag[0].instanceId; checks.RefitPointItem(bagId, 0, out _);
            var beforeProgression = JsonUtility.ToJson(checks.CaptureProgression());
            PointBattleTest.Check("Runtime equipped order and passive are ready", checks.GrowthPoints(CheckBehavior.Wealth) == 2 && checks.EquippedAnchor == MentalAnchors.LinXi);
            player.State.SetFlag("point-runtime-before"); Vector3 beforePosition = player.transform.position;
            var encounter = Resources.Load<BattleEncounterDefinition>("Battle/PointEncounters/point.single");
            var legacyFacade = player.GetComponent<BattleController>() ?? player.gameObject.AddComponent<BattleController>();
            PointBattleTest.Check("Existing battle entry routes new characters to point combat", legacyFacade.TryBegin(encounter, "point-runtime"));
            var battle = player.GetComponent<PointBattleController>();
            PointBattleTest.Check("New combat locks exploration and displays UI", battle?.View != null && BattleController.AnyBattleActive && !GameSessionController.GameplayInputAllowed && legacyFacade.Engine.State == null);
            battle.Engine.RevealLine(); battle.Engine.RevealLine();
            int rollSeed = battle.Engine.State.divination.casting.seed;
            PointBattleTest.Check("Actual save accepts partial coin cast and nonrecursive world return point", session.SaveManual() && session.Store.TryRead(SaveSlot.Manual, out var saved, out _) &&
                saved.actors.First(a => a.id == saved.playerId).pointBattleState.returnPoint.world.actors.All(a => a.pointBattleState == null && a.battleState == null));
            yield return ReloadManual(); player = Player(); battle = player.GetComponent<PointBattleController>(); checks = player.GetComponent<CheckActorState>();
            PointBattleTest.Check("Actual scene reload restores exact locked coin seed", battle.Engine.State.divination.casting.seed == rollSeed);
            battle.QuickCast(); int ap = battle.Engine.State.ap;
            battle.Act(CheckBehavior.Officer, 0, out _);
            int afterActionAP = battle.Engine.State.ap;
            PointBattleTest.Check("Runtime action consumes exactly two AP", afterActionAP == ap - 2 && battle.Engine.State.phase == PointBattlePhase.Player);
            PointBattleTest.Check("Mid-turn manual save succeeds", session.SaveManual());
            yield return ReloadManual(); player = Player(); battle = player.GetComponent<PointBattleController>(); checks = player.GetComponent<CheckActorState>();
            PointBattleTest.Check("Actual load resumes AP and does not grant Self AP twice", battle.Engine.State.ap == afterActionAP && battle.Engine.State.actions[2].uses == 1);
            battle.View.QuickCastButton.onClick.Invoke();
            yield return Capture("point-battle-ui-preview.png");
            player.State.SetFlag("point-runtime-during"); player.State.RemoveItem("day1.identity-card", 1); player.transform.position += Vector3.right * 3;
            PointBattleTest.Check("Flee restores full story, inventory and position", battle.Flee() && !BattleController.AnyBattleActive && player.State.HasFlag("point-runtime-before") &&
                !player.State.HasFlag("point-runtime-during") && player.State.Count("day1.identity-card") == 1 && Vector3.Distance(player.transform.position, beforePosition) < .01f);
            PointBattleTest.Check("Flee restores exact affix, passive and refit quota", JsonUtility.ToJson(checks.CaptureProgression()) == beforeProgression);
            legacyFacade = player.GetComponent<BattleController>() ?? player.gameObject.AddComponent<BattleController>();
            PointBattleTest.Check("Retry keeps original seed after flee", legacyFacade.TryBegin(encounter, "point-runtime") && player.GetComponent<PointBattleController>().Engine.State.divination.casting.seed == rollSeed);
            battle = player.GetComponent<PointBattleController>(); battle.QuickCast();
            battle.Engine.State.enemies[0].hp = .1; battle.Engine.State.ap = Math.Max(2, battle.Engine.State.ap);
            PointBattleTest.Check("Actual victory updates story and round achievement", battle.Act(CheckBehavior.Wealth, 0, out _) &&
                battle.Engine.State.phase == PointBattlePhase.Victory && player.State.Victory(encounter.id, battle.Engine.State.balanceVersion)?.lastRounds == 1);
            PointBattleTest.Check("Victory result can be saved", session.SaveManual());
            yield return ReloadManual(); player = Player(); battle = player.GetComponent<PointBattleController>();
            PointBattleTest.Check("Loading victory does not award again", player.State.Victory(encounter.id, battle.Engine.State.balanceVersion)?.wins == 1);
            battle.CloseResult(); PointBattleTest.Check("Closing result returns to exploration", !BattleController.AnyBattleActive && GameSessionController.GameplayInputAllowed);
            var crowd = Resources.Load<BattleEncounterDefinition>("Battle/PointEncounters/point.crowd");
            legacyFacade = player.GetComponent<BattleController>() ?? player.gameObject.AddComponent<BattleController>(); legacyFacade.TryBegin(crowd, "point-runtime-crowd"); battle = player.GetComponent<PointBattleController>(); battle.QuickCast();
            PointBattleTest.Check("Actual battle supports more than the old three-enemy cap", battle.Engine.State.enemies.Count == 4);
            PointBattleTest.Check("Shared UI supports the four-enemy queue", battle.View.UsesPointRules && battle.View.RenderedEnemyCount == 4 && battle.View.HasEnemyScroll);
            yield return Capture("point-battle-multiple-preview.png"); battle.Flee();
            CreateConfigurationZone();
            checks = player.GetComponent<CheckActorState>(); checks.GetComponent<PointLoadoutUI>().Show();
            yield return Capture("point-battle-loadout-preview.png");
            checks.GetComponent<PointLoadoutUI>().SelectPage(1);
            yield return Capture("point-battle-anchors-preview.png");
            checks.GetComponent<PointLoadoutUI>().SelectPage(2); checks.EnsureRefitOffers(out _);
            yield return Capture("point-battle-refit-preview.png");
            checks.GetComponent<PointLoadoutUI>().Close();
            yield return CheckOriginalScenes();
        }
        private IEnumerator CheckOriginalScenes()
        {
            string[] paths = { "Assets/Scenes/BattleSingleDemo.unity", "Assets/Scenes/BattleDualDemo.unity", "Assets/Scenes/BattleTripleDemo.unity",
                "Assets/Scenes/BattleDefenseDemo.unity", "Assets/Scenes/BattleBossDemo.unity" };
            foreach (string path in paths)
            {
                session.SaveAndReturnToMenu(); session.gameScenePath = path;
                // Allocate enough Self points to exercise consecutive actions even after a negative check.
                Click("New Game"); Click("Confirm New Game");
                var build = new ActorCheckAttributes { parent = 1, offspring = 1, officer = 3, wealth = 3, sibling = 1, self = 5 };
                for (int i = 0; i < 6; i++) for (int j = 1; j < build.Get((CheckBehavior)i); j++) Click("Attribute Plus " + (CheckBehavior)i);
                Click("Confirm Attributes"); yield return Until(() => session.Phase == GameSessionPhase.Playing); yield return null;
                var context = FindObjectOfType<BattleDemoContext>(); var player = Player(); player.keyboardInput = false;
                var checks = player.GetComponent<CheckActorState>(); string label = Path.GetFileNameWithoutExtension(path);
                PointBattleTest.Check(label + ": new-game build and test loadout station", checks.AttributeRulesVersion == 3 && checks.CanConfigureLoadout && checks.AtTangHui &&
                    checks.PointBag.Any(i => i.item.key == "points.sword") && checks.CaptureProgression().anchors.Count == 2);
                PointBattleTest.Check(label + ": original entry launches point rules", context != null && context.player.TryBegin(context.encounter));
                var battle = player.GetComponent<PointBattleController>(); var view = battle.View;
                PointBattleTest.Check(label + ": original uGUI presentation and enemy count", view.UsesPointRules && view.GetComponent<Canvas>() != null &&
                    view.RenderedEnemyCount == context.encounter.enemies.Length && context.player.Engine.State == null && view.CastVisible);
                PointBattleTest.Check(label + ": each original enemy has an explicit point profile", battle.Engine.State.enemies.All(e =>
                    battle.rules.enemies.Any(p => p.id == e.profile.id) && e.profile.health == battle.rules.enemies.First(p => p.id == e.profile.id).health));
                if (path == paths[0]) yield return Capture("point-battle-original-casting-preview.png");
                view.QuickCastButton.onClick.Invoke(); view.QuickCastButton.onClick.Invoke();
                PointBattleTest.Check(label + ": coin button grants AP exactly once", battle.Engine.State.phase == PointBattlePhase.Player &&
                    battle.Engine.State.ap == 3 + battle.Engine.State.actions[5].calculation.finalPoints && view.ActionPointsContent.StartsWith("AP "));
                int chosen = battle.Engine.State.enemies.Count - 1; view.EnemyButton(chosen).onClick.Invoke();
                PointBattleTest.Check(label + ": click selects and highlights the intended enemy", view.SelectedTarget == chosen && view.TargetHighlighted(chosen));
                var pointer = new PointerEventData(EventSystem.current) { position = new Vector2(Screen.width * .5f, Screen.height * .8f) };
                ExecuteEvents.Execute(view.ActionButton(CheckBehavior.Offspring).gameObject, pointer, ExecuteEvents.pointerEnterHandler);
                PointBattleTest.Check(label + ": action hover shows point chain and adjacent status help", view.TooltipVisible &&
                    view.TooltipContent.Contains("最终点数") && view.TooltipContent.Contains("基础") && view.StatusTooltipContent.Contains("12.5%"));
                if (path == paths[0]) yield return Capture("point-battle-original-tooltip-preview.png");
                view.HideTooltip();
                int beforeAP = battle.Engine.State.ap; double beforeHP = battle.Engine.State.enemies[chosen].hp;
                int damage = battle.Engine.State.actions[(int)CheckBehavior.Wealth].calculation.finalPoints;
                view.ActionButton(CheckBehavior.Wealth).onClick.Invoke();
                PointBattleTest.Check(label + ": attack button spends AP and hits selected target", battle.Engine.State.ap == beforeAP - 2 &&
                    Math.Abs(battle.Engine.State.enemies[chosen].hp - Math.Max(0, beforeHP - damage)) < .0001);
                int afterFirst = battle.Engine.State.ap;
                view.ActionButton(CheckBehavior.Officer).onClick.Invoke();
                PointBattleTest.Check(label + ": multiple actions in the same player turn", battle.Engine.State.ap == afterFirst - 2 &&
                    battle.Engine.State.phase == PointBattlePhase.Player && battle.Engine.State.actions[(int)CheckBehavior.Officer].uses == 1);
                foreach (BattlePage page in Enum.GetValues(typeof(BattlePage)))
                { view.PageButton(page).onClick.Invoke(); PointBattleTest.Check(label + ": page button " + page, view.CurrentPage == page); }
                view.PageButton(BattlePage.Skills).onClick.Invoke();
                view.ShowEnemyTooltip(chosen, pointer.position);
                PointBattleTest.Check(label + ": intent tooltip matches fixed enemy action", view.TooltipVisible && view.TooltipContent.Contains(battle.Engine.State.enemies[chosen].Intent.name));
                view.HideTooltip();
                if (path == paths[2]) yield return Capture("point-battle-original-triple-preview.png");
                if (path == paths[4]) yield return Capture("point-battle-original-boss-preview.png");
                if (path == paths[0])
                {
                    PointBattleTest.Check(label + ": shared UI mid-turn save", session.SaveManual());
                    yield return ReloadManual(); player = Player(); battle = player.GetComponent<PointBattleController>(); view = battle.View;
                    PointBattleTest.Check(label + ": reload restores shared UI and exact spent AP", view.UsesPointRules && battle.Engine.State.ap == afterFirst - 2);
                }
                int round = battle.Engine.State.round; int expectedCarry = Math.Min(1, battle.Engine.State.ap);
                view.EndTurnButton.onClick.Invoke();
                PointBattleTest.Check(label + ": end-turn button enters enemy phase and retains at most one AP", battle.Engine.State.phase == PointBattlePhase.Enemy && battle.Engine.State.carriedAP == expectedCarry);
                yield return Until(() => battle.Engine.State.phase == PointBattlePhase.Player || battle.Engine.State.phase == PointBattlePhase.Casting || battle.Engine.State.phase == PointBattlePhase.Defeat);
                if (battle.Engine.State.phase == PointBattlePhase.Casting) { view.QuickCastButton.onClick.Invoke(); view.QuickCastButton.onClick.Invoke(); }
                PointBattleTest.Check(label + ": enemy pattern completes and next round grants AP", battle.Engine.State.round == round + 1 &&
                    battle.Engine.State.ap == 3 + battle.Engine.State.actions[5].calculation.finalPoints + expectedCarry);
                view.PageButton(BattlePage.Backpack).onClick.Invoke(); var consumable = player.GetComponent<BattleController>().catalog.items.First(d => d.effect == BattleItemEffect.Heal);
                int beforeItem = battle.Engine.State.ap; int beforeCount = player.State.Count(consumable.inventoryKey);
                view.ItemButton(consumable.id).onClick.Invoke();
                PointBattleTest.Check(label + ": bag button consumes an item and two AP", player.State.Count(consumable.inventoryKey) == beforeCount - 1 && battle.Engine.State.ap == beforeItem - 2);
                view.PageButton(BattlePage.Escape).onClick.Invoke(); view.EscapeButton.onClick.Invoke();
                PointBattleTest.Check(label + ": escape button returns to the same original scene and restores inventory", !BattleController.AnyBattleActive &&
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().path == path && player.State.Count(consumable.inventoryKey) == beforeCount && GameSessionController.GameplayInputAllowed);
            }
        }
    }
}
#endif

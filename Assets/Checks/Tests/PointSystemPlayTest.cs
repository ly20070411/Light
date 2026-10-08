#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emerge.GameFlow;
using Emerge.Props;
using UnityEngine;
using UnityEngine.UI;

namespace Emerge.Checks.Tests
{
    public sealed class PointSystemPlayTest : MonoBehaviour
    {
        public static event Action<bool> Completed;
        private GameSessionController session;
        private void Awake() { DontDestroyOnLoad(gameObject);  }
        private IEnumerator Start()
        {
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (stack.Count > 0)
            {
                bool next; object current;
                try { next = stack.Peek().MoveNext(); current = next ? stack.Peek().Current : null; }
                catch (Exception exception) { PointSystemTest.Check("Real creation/save/load completes", false, exception.ToString()); break; }
                if (!next) { stack.Pop(); continue; }
                if (current is IEnumerator nested) stack.Push(nested); else yield return current;
            }
            PointSystemTest.SaveReport();
            Completed?.Invoke(PointSystemTest.Results.passed);
        }
        private IEnumerator Until(Func<bool> predicate)
        {
            float deadline = Time.realtimeSinceStartup + 30;
            while (!predicate()) { if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("Point-system real-flow timeout"); yield return null; }
        }
        private Button Button(string name) => session.GetComponentsInChildren<Button>(true).Single(button => button.name == name);
        private void Click(string name) => Button(name).onClick.Invoke();
        private IEnumerator Capture(string filename)
        {
            if (Application.isBatchMode) yield break;
            var gameView = typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameView != null) UnityEditor.EditorWindow.GetWindow(gameView).Focus();
            yield return null;
            yield return new WaitForEndOfFrame();
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Validation/" + filename));
            DateTime requested = DateTime.UtcNow;
            ScreenCapture.CaptureScreenshot(path);
            yield return Until(() => File.Exists(path) && File.GetLastWriteTimeUtc(path) >= requested && new FileInfo(path).Length > 0);
            PointSystemTest.Check("Actual interface captured: " + filename, true, path);
        }
        private IEnumerator Run()
        {
            session = new GameObject("点数验证隔离菜单").AddComponent<GameSessionController>();
            yield return Until(() => session.IsReady);
            session.UseTestSaveDirectory(Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/PointSystemPlayChecks_" + Guid.NewGuid().ToString("N"))));
            session.gameScenePath = "Assets/Scenes/PropsDemo.unity"; session.autoSaveInterval = 600;
            Click("New Game");
            PointSystemTest.Check("New game opens six-attribute creation before entering the world", session.Phase == GameSessionPhase.CharacterCreation && session.AllocationRemaining == 8 && session.GetComponentsInChildren<CharacterAttributeHover>(true).Length == 6);
            PointSystemTest.Check("Creation defaults and disabled minus buttons are correct", Enumerable.Range(0, 6).All(i => session.AllocatedPoints((CheckBehavior)i) == 1 && !Button("Attribute Minus " + (CheckBehavior)i).interactable));
            PointSystemTest.Check("Incomplete creation cannot confirm or save", !Button("Confirm Attributes").interactable && !session.ConfirmCharacterCreation() && !session.SaveManual());
            PointSystemTest.Check("Creation text contains no old multipliers", !session.GetComponentsInChildren<Text>(true).Where(text => text.transform.IsChildOf(Button("Confirm Attributes").transform.parent)).Any(text => text.text.Contains("10% 技能基础效果") || text.text.Contains("效果 ×")));
            for (int i = 0; i < 8; i++) Click("Attribute Plus Self");
            PointSystemTest.Check("Actual Self buttons spend all eight extra points", session.AllocationRemaining == 0 && session.AllocatedPoints(CheckBehavior.Self) == 9 && Button("Confirm Attributes").interactable && !Button("Attribute Plus Parent").interactable);
            Canvas.ForceUpdateCanvases();
            var rows = session.GetComponentsInChildren<CharacterAttributeHover>(true);
            var parentRect = (RectTransform)rows[0].transform.parent;
            PointSystemTest.Check("All six rows fit within the creation card", rows.All(row => { var corners = new Vector3[4]; ((RectTransform)row.transform).GetWorldCorners(corners); return corners.All(corner => parentRect.rect.Contains(parentRect.InverseTransformPoint(corner))); }));
            yield return Capture("point-system-creation-preview.png");
            Click("Confirm Attributes");
            yield return Until(() => session.Phase == GameSessionPhase.Playing);
            var player = FindObjectsOfType<PlayerInteractor>().First(actor => actor.GetComponent<PixelPrototype.PlayerMovement>() != null);
            player.keyboardInput = false;
            var checks = player.GetComponent<CheckActorState>();
            var zone = new GameObject("点数流程测试卧室").AddComponent<PointConfigurationZone>(); zone.size = new Vector2(1000, 1000);
            PointSystemTest.Check("Confirmed six bases reach the world and its initial save", checks.attributes.self == 9 && checks.AttributeRulesVersion == 3 && session.Store.TryRead(SaveSlot.Auto, out var initial, out _) && initial.actors.Find(actor => actor.id == initial.playerId).checkState.attributes.self == 9);
            player.State.AddItem("points.sword", "剑", 1); player.State.AddItem("points.talisman", "符咒", 1);
            PointSystemTest.Check("Runtime character has the configuration UI", player.GetComponent<PointLoadoutUI>() != null);
            var ui = player.GetComponent<PointLoadoutUI>(); ui.Show();
            PointSystemTest.Check("Opening configuration locks world interaction", PointLoadoutUI.AnyOpen && !GameSessionController.GameplayInputAllowed && !player.GetComponent<PixelPrototype.PlayerMovement>().enabled);
            PointSystemTest.Check("Runtime equip supports the independent Self lane", checks.TryEquipPointItem("points.sword", CheckBehavior.Self, out _) && checks.TryEquipPointItem("points.talisman", CheckBehavior.Self, out _) && checks.GrowthPoints(CheckBehavior.Self) == 2 && checks.PreviewPoints(CheckBehavior.Self, -1).finalPoints == 13);
            ui.SelectAttribute(CheckBehavior.Self);
            yield return Capture("point-system-loadout-preview.png");
            ui.Close(); PointSystemTest.Check("Closing configuration restores interaction", !PointLoadoutUI.AnyOpen && GameSessionController.GameplayInputAllowed && player.GetComponent<PixelPrototype.PlayerMovement>().enabled);
            PointSystemTest.Check("Manual save persists runtime equipment", session.SaveManual() && session.Store.TryRead(SaveSlot.Manual, out var saved, out _) && saved.actors.Find(actor => actor.id == saved.playerId).checkState.pointEquipment.Count == 2);
            session.OpenSettings(); session.LoadManual();
            yield return Until(() => session.Phase == GameSessionPhase.Playing);
            player = FindObjectsOfType<PlayerInteractor>().First(actor => actor.GetComponent<PixelPrototype.PlayerMovement>() != null); player.keyboardInput = false;
            checks = player.GetComponent<CheckActorState>();
            PointSystemTest.Check("Actual scene reload restores equipped order, growth and reserved copies", checks.PointEquipment.Select(slot => slot.itemKey).SequenceEqual(new[] { "points.sword", "points.talisman" }) && checks.GrowthPoints(CheckBehavior.Self) == 2 && player.State.Count("points.sword") == 0 && player.State.Count("points.talisman") == 0);
            session.OpenSettings(); Click("Reallocate Attributes");
            PointSystemTest.Check("Reallocation begins from existing bases, not growth", session.AllocatedPoints(CheckBehavior.Self) == 9 && session.AllocationRemaining == 0);
            Click("Attribute Minus Self"); Click("Attribute Plus Officer"); Click("Confirm Attributes");
            PointSystemTest.Check("Base reallocation keeps equipment and its growth independent", checks.attributes.self == 8 && checks.attributes.officer == 2 && checks.GrowthPoints(CheckBehavior.Self) == 2);
            session.ResumeGame();
            // An old-save upgrade must preserve story/world state and leave the source untouched until confirmation.
            player.State.SetFlag("points-migration-progress");
            var legacy = session.CaptureGame("点数旧存档迁移验证");
            var legacyPlayer = legacy.actors.Find(actor => actor.id == legacy.playerId);
            legacyPlayer.checkState.attributeRulesVersion = 2;
            legacyPlayer.checkState.attributes = Emerge.Battle.BattleBuildRules.DefaultBuild();
            legacyPlayer.checkState.pointEquipment.Clear();
            legacyPlayer.propState.inventory.Add(new PropGameState.InventoryEntry { key = "points.sword", displayName = "剑", amount = 1 });
            legacyPlayer.propState.inventory.Add(new PropGameState.InventoryEntry { key = "points.talisman", displayName = "符咒", amount = 1 });
            PointSystemTest.Check("Legacy slot remains readable before upgrade", session.Store.TryWrite(SaveSlot.Manual, legacy, out _));
            string legacyBytes = File.ReadAllText(session.Store.SlotPath(SaveSlot.Manual));
            session.OpenSettings(); session.LoadManual();
            PointSystemTest.Check("Old five-family save enters new six-base allocation", session.Phase == GameSessionPhase.CharacterCreation && session.IsReallocating && session.AllocationRemaining == 8 && session.AllocatedPoints(CheckBehavior.Self) == 1);
            Click("Cancel Character Creation");
            PointSystemTest.Check("Cancelling legacy reallocation does not overwrite the source", File.ReadAllText(session.Store.SlotPath(SaveSlot.Manual)) == legacyBytes);
            session.LoadManual(); for (int i = 0; i < 8; i++) Click("Attribute Plus Officer"); Click("Confirm Attributes");
            yield return Until(() => session.Phase == GameSessionPhase.Playing);
            player = FindObjectsOfType<PlayerInteractor>().First(actor => actor.GetComponent<PixelPrototype.PlayerMovement>() != null);
            checks = player.GetComponent<CheckActorState>();
            PointSystemTest.Check("Confirmed migration preserves plot, inventory and world position", checks.AttributeRulesVersion == 3 && checks.attributes.officer == 9 && checks.attributes.self == 1 && player.State.HasFlag("points-migration-progress") && player.State.Count("points.sword") == 1 && Vector3.Distance(player.transform.position, legacyPlayer.position) < .01f);
        }
    }
}
#endif

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emerge.Checks.Divination;
using Emerge.Props;
using Emerge.GameFlow;
using UnityEngine;

namespace Emerge.Checks.Tests
{
    public static class PointSystemTest
    {
        [Serializable] public sealed class Entry { public string name, observed; public bool passed; }
        [Serializable] public sealed class Report
        {
            public string completedUtc, unityVersion;
            public bool passed;
            public List<Entry> checks = new List<Entry>();
        }
        public static Report Results;
        public static void Check(string name, bool value, string observed = "")
        { Results.checks.Add(new Entry { name = name, passed = value, observed = observed }); if (!value) Debug.LogError("POINT_CHECK_FAILED: " + name + " " + observed); }
        public static bool RunChecks()
        {
            Results = new Report { unityVersion = Application.unityVersion };
            var objects = new List<UnityEngine.Object>();
            try
            {
                var draft = new CharacterAttributeAllocation();
                Check("Six attributes begin at one with eight extra points", draft.Remaining == 8 && Enumerable.Range(0, 6).All(i => draft.Get((CheckBehavior)i) == 1));
                Check("Cannot subtract the default one or use an unknown attribute", !draft.TryChange(CheckBehavior.Self, -1) && !draft.TryChange((CheckBehavior)6, 1) && !draft.TryChange(CheckBehavior.Parent, 2));
                for (int i = 0; i < 8; i++) Check("Self accepts freely allocated point " + i, draft.TryChange(CheckBehavior.Self, 1));
                Check("Eight extra points may all go to Self, with no overspending", draft.Get(CheckBehavior.Self) == 9 && draft.IsComplete && !draft.TryChange(CheckBehavior.Parent, 1) && SixKinAttributes.IsValidBuild(draft.ToAttributes()));
                var exported = draft.ToAttributes(); exported.self = 0;
                Check("Exported base attributes cannot mutate the draft", draft.Get(CheckBehavior.Self) == 9 && draft.TryChange(CheckBehavior.Self, -1) && draft.Remaining == 1);
                draft.Reset(); Check("Reset restores six default ones and all eight extras", draft.Remaining == 8 && Enumerable.Range(0, 6).All(i => draft.Get((CheckBehavior)i) == 1));
                Check("Legacy eight-point builds are separate", SixKinAttributes.IsValidLegacyBuild(Emerge.Battle.BattleBuildRules.DefaultBuild()) && !SixKinAttributes.IsValidBuild(Emerge.Battle.BattleBuildRules.DefaultBuild()));
                var catalog = Resources.Load<PointItemCatalog>(PointItemCatalog.ResourcePath);
                Check("Installed point item library is valid", catalog != null && catalog.IsValid());
                var owner = new GameObject("Point calculation isolated actor"); objects.Add(owner); owner.AddComponent<PointConfigurationZone>().size = new Vector2(1000, 1000);
                var state = owner.AddComponent<PropGameState>(); var actor = owner.AddComponent<CheckActorState>(); actor.pointItemCatalog = catalog;
                var attributes = new ActorCheckAttributes { parent = 3, offspring = 2, officer = 5, wealth = 1, sibling = 1, self = 2 };
                Check("New base snapshot restores", actor.RestoreSnapshot(new CheckActorState.Snapshot { attributeRulesVersion = 3, attributes = attributes }));
                Check("Unowned items cannot be equipped", !actor.TryEquipPointItem("points.sword", CheckBehavior.Officer, out _));
                state.AddItem("points.sword", "剑", 1); state.AddItem("points.talisman", "符咒", 1);
                Check("Equip sword and talisman from actual inventory", actor.TryEquipPointItem("points.sword", CheckBehavior.Officer, out _) && actor.TryEquipPointItem("points.talisman", CheckBehavior.Officer, out _));
                Check("Equipped copies are reserved, not duplicated", state.Count("points.sword") == 0 && state.Count("points.talisman") == 0 && !actor.TryEquipPointItem("points.sword", CheckBehavior.Parent, out _));
                var example = actor.PreviewPoints(CheckBehavior.Officer, -1);
                Check("Sword then talisman gives precisely 5+2-1 -> 7 -> 9", example.basePoints == 5 && example.growthPoints == 2 && example.outsidePoints == 7 && example.checkPoints == -1 && example.initialCurrentPoints == 6 && example.iterations[0].after == 7 && example.finalPoints == 9, example.Describe());
                Check("Each item advances the iteration index, not multiplication", example.iterations.Select(s => s.iteration).SequenceEqual(new[] { 1, 2 }) && example.action.amount == 9);
                string sword = actor.PointEquipment.First(s => s.itemKey == "points.sword").instanceId;
                string talisman = actor.PointEquipment.First(s => s.itemKey == "points.talisman").instanceId;
                Check("Move talisman before sword", actor.TryMovePointItem(talisman, CheckBehavior.Officer, 0, out _));
                var reverse = actor.PreviewPoints(CheckBehavior.Officer, -1);
                Check("Talisman then sword gives 6 -> 8 -> 8", reverse.finalPoints == 8 && reverse.iterations[0].after == 8 && !reverse.iterations[1].rules[0].triggered, reverse.Describe());
                Check("Reordering does not change base, growth or outside points", reverse.basePoints == 5 && reverse.growthPoints == 2 && reverse.outsidePoints == 7 && actor.attributes.officer == 5);
                Check("Move item to another attribute without duplication", actor.TryMovePointItem(sword, CheckBehavior.Parent, 0, out _) && actor.GrowthPoints(CheckBehavior.Parent) == 1 && actor.GrowthPoints(CheckBehavior.Officer) == 1 && actor.PointEquipment.Count == 2);
                Check("Invalid move preserves slot order", !actor.TryMovePointItem(sword, CheckBehavior.Parent, 9, out _) && actor.PointEquipment.First(s => s.instanceId == sword).attribute == CheckBehavior.Parent);
                Check("Unequip returns exactly one reserved copy", actor.TryUnequipPointItem(sword, out _) && state.Count("points.sword") == 1 && !actor.TryUnequipPointItem(sword, out _));
                Check("Negative check points and negative final points are preserved", actor.PreviewPoints(CheckBehavior.Self, -9).finalPoints == -7);
                Check("Offspring reduction retains fractional final-point amount", PointCalculation.ActionValue(CheckBehavior.Offspring, 3).amount == 3.75);
                Check("Wealth means 12.5 percent per point", PointCalculation.ActionValue(CheckBehavior.Wealth, 3).amount == .375);
                Check("Sibling means 10 percent per point", Math.Abs(PointCalculation.ActionValue(CheckBehavior.Sibling, 3).amount - .3) < 1e-12);
                Check("Officer, Parent and Self use the final points directly", new[] { CheckBehavior.Officer, CheckBehavior.Parent, CheckBehavior.Self }.All(a => PointCalculation.ActionValue(a, 5).amount == 5));
                Check("Point-only equipment still adds growth", actor.TryUnequipPointItem(talisman, out _) && AddAndEquip(state, actor, "points.knot", CheckBehavior.Officer) && actor.PreviewPoints(CheckBehavior.Officer, 0).finalPoints == 6);
                var slotCopy = actor.PointEquipment[0]; slotCopy.attribute = CheckBehavior.Sibling;
                Check("External slot views cannot mutate the live loadout", actor.GrowthPoints(CheckBehavior.Officer) == 1);
                var frozen = actor.FreezePointEquipment();
                var oldDefinition = frozen[0].item.key;
                Check("Frozen definitions have independent data", frozen[0].item != catalog.Find(oldDefinition).data);
                var context = new PointContext { basePoints = 5, growthPoints = 2, outsidePoints = 7, checkPoints = -1, currentPoints = 6 };
                foreach (PointSource source in Enum.GetValues(typeof(PointSource)))
                {
                    int expected = source == PointSource.Constant ? 99 : source == PointSource.Base ? 5 : source == PointSource.Growth ? 2 : source == PointSource.Outside ? 7 : source == PointSource.Check ? -1 : 6;
                    Check("Typed point operand " + source, new PointOperand { source = source, constant = 99 }.Read(context) == expected);
                }
                var def = ScriptableObject.CreateInstance<CheckEventDefinition>(); objects.Add(def);
                def.eventId = "points-real-chart"; def.useFixedDivinationSeed = true; def.divinationMonth = "巳月"; def.divinationDay = "戊子日";
                def.options.Add(new CheckOptionDefinition { id = "attack", label = "点数行动", behavior = CheckBehavior.Officer, targetValue = 5, success = new CheckOutcomeDefinition { grantedFlags = new[] { "point-success" } }, failure = new CheckOutcomeDefinition { grantedFlags = new[] { "point-failure" } } });
                for (int seed = 1; seed < 1000; seed++)
                {
                    var cast = CoinCasting.Cast(seed); var chart = new LiuYaoPaiPan().PaiPan(def.divinationMonth, def.divinationDay, cast.yaoValues);
                    if (chart.behaviorModifiers[2] < 0) { def.divinationSeed = seed; break; }
                }
                var session = actor.GetOrPrepare(def);
                Check("Actual check freezes item order at preparation", session.attributeRulesVersion == 3 && session.pointEquipment.Count == 1 && session.pointEquipment[0].item.key == "points.knot");
                for (int i = 0; i < 6; i++) Check("Real casting reveal " + (i + 1), actor.RevealNextCast(session));
                Check("Actual chart supplies signed check points", actor.FinishPreparation(session, out _) && session.modifiers[2] < 0);
                Check("Self modifier is the independent world-line score", session.modifiers[5] == session.divination.chart.yaos[session.divination.chart.shiYaoIndex].wangshuaiScore && CheckResolver.EffectiveBehavior(session, CheckBehavior.Self) == CheckBehavior.Self);
                Check("Changing equipment after preparation leaves session frozen", actor.TryUnequipPointItem(actor.PointEquipment[0].instanceId, out _) && session.pointEquipment.Count == 1);
                var expectedPoints = CheckResolver.Points(session, CheckBehavior.Officer);
                Check("Resolve uses the point chain, including frozen growth", actor.TryResolve(def, session, "attack", out var resolved, out _) && resolved.finalValue == expectedPoints.finalPoints && resolved.pointCalculation.growthPoints == 1);
                Check("Repeated resolution is rejected", !actor.TryResolve(def, session, "attack", out _, out _));
                var saved = actor.CaptureSnapshot();
                Check("New state and resolved point trace validate", CheckActorState.IsValidSnapshot(saved));
                var roundTrip = JsonUtility.FromJson<CheckActorState.Snapshot>(JsonUtility.ToJson(saved));
                Check("JSON preserves signed scores, frozen equipment and result", actor.RestoreSnapshot(roundTrip) && actor.Sessions[0].result.finalValue == expectedPoints.finalPoints && actor.Sessions[0].result.pointCalculation.growthPoints == 1);
                roundTrip.sessions[0].result.pointCalculation.iterations[0].after++;
                Check("Tampered intermediate point trace is rejected", !CheckActorState.IsValidSnapshot(roundTrip));
                var invalid = actor.CaptureSnapshot(); invalid.pointEquipment.Add(new PointEquipmentSlot { instanceId = "duplicate", itemKey = "points.sword", attribute = CheckBehavior.Officer }); invalid.pointEquipment.Add(invalid.pointEquipment[0]);
                Check("Duplicate physical equipped copies are rejected on restore", !actor.RestoreSnapshot(invalid));
                var legacy = new CheckActorState.Snapshot { attributeRulesVersion = 2, attributes = Emerge.Battle.BattleBuildRules.DefaultBuild() };
                Check("Old five-family saves still load without changing old point rules", CheckActorState.IsValidSnapshot(legacy));
                var legacySession = new CheckSession { attributeRulesVersion = 2, attributes = legacy.attributes, modifiers = new[] { 1, 0, 0, 0, 0, 99 } };
                Check("Only version-two sessions retain Self-to-Parent mapping", CheckResolver.EffectiveBehavior(legacySession, CheckBehavior.Self) == CheckBehavior.Parent && CheckResolver.Points(legacySession, CheckBehavior.Self).finalPoints == 3);
                var inventorySnapshot = state.CaptureSnapshot();
                var equippedSave = actor.CaptureSnapshot();
                Check("Equip again before save integration", actor.TryEquipPointItem("points.sword", CheckBehavior.Officer, out _));
                string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/PointSystemSaveChecks"));
                var store = new GameSaveStore(directory);
                var data = new GameSaveData { savedUtcTicks = DateTime.UtcNow.Ticks, scenePath = "Assets/Scenes/PropsDemo.unity", playerId = "points-player" };
                data.actors.Add(new SavedActor { id = data.playerId, propState = state.CaptureSnapshot(), checkState = actor.CaptureSnapshot() });
                Check("Whole-game save accepts and restores equipment reservations", store.TryWrite(SaveSlot.Manual, data, out _) && store.TryRead(SaveSlot.Manual, out var loaded, out _) && loaded.actors[0].checkState.pointEquipment.Count == 1 && loaded.actors[0].propState.inventory.All(item => item.key != "points.sword"));
                var overflowItems = new List<FrozenPointItem> { new FrozenPointItem { instanceId = "overflow", attribute = CheckBehavior.Officer,
                    item = new PointItemData { key = "overflow", displayName = "overflow", modifiers = new List<PointModifierRule> { new PointModifierRule { operation = PointOperation.Add, operand = new PointOperand { constant = int.MaxValue } } } } } };
                bool overflow = false; try { PointCalculation.Calculate(attributes, CheckBehavior.Officer, 0, overflowItems); } catch (OverflowException) { overflow = true; }
                Check("Point arithmetic overflow fails explicitly", overflow);
                var compoundItem = new FrozenPointItem { instanceId = "compound", attribute = CheckBehavior.Officer,
                    item = new PointItemData { key = "compound", displayName = "复合道具", modifiers = new List<PointModifierRule> {
                        new PointModifierRule { operation = PointOperation.Add, operand = new PointOperand { source = PointSource.Check } },
                        new PointModifierRule { comparison = PointComparison.Less, left = new PointOperand { source = PointSource.Current }, right = new PointOperand { source = PointSource.Base }, operation = PointOperation.Set, operand = new PointOperand { source = PointSource.Outside } } },
                        actionAddons = new List<PointActionAddon> { new PointActionAddon { effectKey = "sample.action-addon", description = "占位附加效果", amount = new PointOperand { source = PointSource.Current } } } } };
                var compound = PointCalculation.Calculate(attributes, CheckBehavior.Officer, -2, new List<FrozenPointItem> { compoundItem });
                Check("Rules within one item see the updated current points and named scopes", compound.outsidePoints == 6 && compound.initialCurrentPoints == 4 && compound.iterations.Count == 1 && compound.iterations[0].rules[0].after == 2 && compound.finalPoints == 6);
                Check("Action add-ons are emitted once with their item's resolved point value", compound.actionAddons.Count == 1 && compound.actionAddons[0].amount == 6 && compound.actionAddons[0].effectKey == "sample.action-addon");
                var pickupData = ScriptableObject.CreateInstance<PropDefinition>(); objects.Add(pickupData);
                pickupData.pointItem = catalog.Find("points.talisman"); pickupData.actions = PropActions.Pickup; pickupData.displayName = "符咒拾取测试"; pickupData.pickupAmount = 2;
                var pickupObject = new GameObject("点数道具实际拾取"); objects.Add(pickupObject);
                var pickup = pickupObject.AddComponent<PropInstance>(); pickup.Configure(pickupData);
                var interactor = owner.AddComponent<PlayerInteractor>(); interactor.showUI = false; interactor.keyboardInput = false;
                int beforePickup = state.Count("points.talisman");
                Check("Existing world pickup grants the linked growth item key", pickupData.InventoryKey == "points.talisman" && pickup.Interact(interactor) && state.Count("points.talisman") == beforePickup + 2);
                Check("A consumed world pickup cannot duplicate growth items", !pickup.Interact(interactor) && state.Count("points.talisman") == beforePickup + 2);
                foreach (int modifier in new[] { -8, -1, 0, 1, 9 })
                    for (int i = 0; i < 6; i++)
                    {
                        var value = PointCalculation.Calculate(attributes, (CheckBehavior)i, modifier, new List<FrozenPointItem>());
                        Check("No-equipment identity for signed modifier " + modifier + " / " + i, value.finalPoints == attributes.Get((CheckBehavior)i) + modifier && value.growthPoints == 0);
                    }
            }
            catch (Exception exception) { Check("Point-system checks complete without exception", false, exception.ToString()); }
            finally { foreach (var item in objects) UnityEngine.Object.DestroyImmediate(item); }
            SaveReport(); return Results.passed;
        }
        private static bool AddAndEquip(PropGameState inventory, CheckActorState actor, string key, CheckBehavior kin)
        { inventory.AddItem(key, key, 1); return actor.TryEquipPointItem(key, kin, out _); }
        public static void SaveReport()
        {
            Results.completedUtc = DateTime.UtcNow.ToString("O"); Results.passed = Results.checks.All(check => check.passed);
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Validation/point-system-results.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, JsonUtility.ToJson(Results, true));
            Debug.Log("POINT_SYSTEM_CHECKS: " + Results.checks.Count + "; passed=" + Results.passed);
        }
    }
}
#endif

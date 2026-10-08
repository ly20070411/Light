#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emerge.Checks;
using Emerge.Props;
using UnityEngine;

namespace Emerge.Battle.Tests
{
    public static class PointBattleTest
    {
        [Serializable] public sealed class Entry { public string name, observed; public bool passed; }
        [Serializable] public sealed class Report
        {
            public string completedUtc, unityVersion;
            public bool passed;
            public List<Entry> checks = new List<Entry>();
        }
        public static Report Results;
        public static void Check(string name, bool condition, string observed = "")
        { Results.checks.Add(new Entry { name = name, passed = condition, observed = observed }); if (!condition) Debug.LogWarning("POINT_BATTLE_CHECK_FAILED: " + name + " " + observed); }
        public static void Write()
        {
            Results.completedUtc = DateTime.UtcNow.ToString("O"); Results.passed = Results.checks.All(c => c.passed);
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Validation/point-battle-results.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, JsonUtility.ToJson(Results, true));
        }
        public static bool RunChecks()
        {
            Results = new Report { unityVersion = Application.unityVersion };
            var objects = new List<UnityEngine.Object>();
            try
            {
                var rules = Resources.Load<PointBattleDefinition>(PointBattleDefinition.ResourcePath);
                Check("Installed point battle rules are valid", rules != null && rules.Valid());
                Check("Living supplies and all story draft items have growth definitions", Resources.Load<PointItemCatalog>(PointItemCatalog.ResourcePath).Find("day1.supplies") != null &&
                    Resources.Load<PointItemCatalog>(PointItemCatalog.ResourcePath).Find("story.item-08") != null);
                var fresh = ScriptableObject.CreateInstance<PointBattleDefinition>(); objects.Add(fresh); fresh.playerHealth = 100;
                var build = SixKinAttributes.DefaultBuild();
                Check("Latest five kin action mapping", PointBattleEngine.ActionValue(CheckBehavior.Wealth, 4).kind == PointActionKind.Damage &&
                    PointBattleEngine.ActionValue(CheckBehavior.Officer, 4).kind == PointActionKind.Shield &&
                    PointBattleEngine.ActionValue(CheckBehavior.Offspring, 4).amount == .5 &&
                    PointBattleEngine.ActionValue(CheckBehavior.Sibling, 4).amount == 5 && PointBattleEngine.ActionValue(CheckBehavior.Parent, 4).amount == .4);
                var floor = PointBattleEngine.Calculate(build, CheckBehavior.Wealth, -20, new List<FrozenPointItem>());
                Check("Final combat floor is zero; signed check and negative intermediate survive", floor.finalPoints == 0 && floor.checkPoints == -20 && floor.initialCurrentPoints == -18);
                var e = Begin(fresh); Cast(e);
                var s = e.State; int startingAP = s.ap;
                Check("Exactly one round roll gives 3 plus Self final AP", startingAP == 3 + s.actions[5].calculation.finalPoints && !e.ResolveRound() && s.ap == startingAP);
                Check("Real divination supplies signed modifiers for all six kin", Enumerable.Range(0, 6).All(i => s.actions[i].rawCheckPoints == s.divination.chart.behaviorModifiers[i]));
                Check("Self is automatic and cannot be used as a repeat action", !e.Act(CheckBehavior.Self, 0, out _));
                int used = 0; while (e.Act(CheckBehavior.Officer, 0, out _)) used++;
                Check("Ordinary actions repeat until AP runs out", used == startingAP / 2 && s.ap == startingAP % 2 && s.phase == PointBattlePhase.Player);
                Check("No automatic end turn on AP exhaustion", !e.Act(CheckBehavior.Officer, 0, out _) && s.phase == PointBattlePhase.Player);
                e.EndTurn();
                Check("Only one leftover AP can carry", s.carriedAP == startingAP % 2 && s.ap == 0);
                Check("Actions are blocked in enemy phase", !e.Act(CheckBehavior.Officer, 0, out _));
                e.StepEnemy(); Cast(e);
                Check("Player block expires before own next turn", s.shield == 0 && s.round == 2);
                int round2AP = s.ap; e.EndTurn(); Check("Ending early carries at most one", s.carriedAP == Math.Min(1, round2AP));

                var deer = Begin(fresh, MentalAnchors.LuJianshen); Cast(deer); var ds = deer.State;
                Check("Deer doubles positive check points only", ds.actions.All(a => a.effectiveCheckPoints == (a.rawCheckPoints > 0 ? a.rawCheckPoints * 2 : a.rawCheckPoints)));
                Check("Deer blocks zero and negative checks", ds.actions.All(a => a.available == (a.rawCheckPoints > 0)));
                Check("Deer never blocks the fixed 3 AP", ds.ap == 3 + (ds.actions[5].available ? ds.actions[5].calculation.finalPoints : 0));
                var plain = Begin(fresh); Cast(plain);
                Check("Anchor and roll never modify enemy base intent or numbers", JsonUtility.ToJson(ds.enemies[0]) == JsonUtility.ToJson(plain.State.enemies[0]));

                var lin = Begin(fresh, MentalAnchors.LinXi, 2039); Cast(lin); var ls = lin.State; ls.ap = 30;
                int x = ls.actions[(int)CheckBehavior.Sibling].calculation.finalPoints;
                lin.Act(CheckBehavior.Sibling, 0, out _); lin.Act(CheckBehavior.Sibling, 0, out _);
                Check("Lin adds floor(x/2) corrosion and repeated applications stack", ls.enemies[0].corrosion == 2 * (x / 2));
                Check("Lin limits sibling to twice per round", !lin.Act(CheckBehavior.Sibling, 0, out _) && ls.actions[4].uses == 2 && ls.ap == 26);
                int stacks = ls.enemies[0].corrosion; double hp = ls.enemies[0].hp;
                lin.EndTurn(); lin.StepEnemy();
                Check("Corrosion loses HP directly then decays once at round start", ls.enemies[0].hp == hp - stacks && ls.enemies[0].corrosion == Math.Max(0, stacks - 1));
                Cast(lin); Check("Lin's action limit resets each round", lin.Act(CheckBehavior.Sibling, 0, out _));

                var combo = Begin(fresh); Cast(combo); var cs = combo.State; cs.ap = 20;
                int attack = cs.actions[3].calculation.finalPoints, offspring = cs.actions[1].calculation.finalPoints, parent = cs.actions[0].calculation.finalPoints;
                combo.Act(CheckBehavior.Parent, 0, out _); combo.Act(CheckBehavior.Offspring, 0, out _);
                double before = cs.enemies[0].hp; combo.Act(CheckBehavior.Wealth, 0, out _);
                Check("Damage applies fractional damage bonus and vulnerability", Math.Abs(before - cs.enemies[0].hp - attack * (1 + parent * .1) * (1 + offspring * .125)) < 1e-8);
                combo.Act(CheckBehavior.Sibling, 0, out _); combo.EndTurn(); combo.StepEnemy();
                Check("Player action statuses clear at enemy-turn end", cs.damageBonus == 0 && cs.enemies[0].vulnerability == 0 && cs.enemies[0].attackReduction == 0);

                var eight = Begin(fresh, null, 999, 8); Cast(eight); var es = eight.State;
                double playerBefore = es.hp; eight.EndTurn(); for (int i = 0; i < 8; i++) eight.StepEnemy();
                Check("Eight enemies all get exactly one action", es.round == 2 && playerBefore - es.hp == 8 * 3);
                Cast(eight); es.enemies[2].hp = 0; es.enemies[6].hp = 0; playerBefore = es.hp;
                eight.EndTurn(); for (int i = 0; i < 8; i++) eight.StepEnemy();
                Check("Dead enemies are skipped", playerBefore - es.hp == 6 * 3);

                var debuffs = Begin(fresh); Cast(debuffs); var bs = debuffs.State;
                bs.enemies[0].profile.pattern = new List<PointEnemyMove> { new PointEnemyMove { name = "流蚀", effect = PointEnemyEffect.Corrosion, amount = 3 },
                    new PointEnemyMove { name = "易伤", effect = PointEnemyEffect.Vulnerability, amount = 20 }, new PointEnemyMove { name = "削弱", effect = PointEnemyEffect.Weaken, amount = 2 } };
                debuffs.EndTurn(); debuffs.StepEnemy(); Check("Enemy corrosion ticks at next round start", bs.hp == 97 && bs.corrosion == 2);
                Cast(debuffs); debuffs.EndTurn(); debuffs.StepEnemy(); Check("Enemy vulnerability survives into player's next turn", bs.vulnerability == .2);
                Cast(debuffs); debuffs.EndTurn(); debuffs.StepEnemy(); Check("Enemy weaken alters player damage in next turn", bs.weakness == 2);

                var owner = new GameObject("点数战斗独立测试"); objects.Add(owner); var inventory = owner.AddComponent<PropGameState>(); var checks = owner.AddComponent<CheckActorState>();
                checks.TrySetAllocatedAttributes(build); var zoneObject = new GameObject("点数配置区域测试"); objects.Add(zoneObject); var zone = zoneObject.AddComponent<PointConfigurationZone>(); zone.warehouse = true; zone.size = new Vector2(2, 2);
                var station = owner.AddComponent<PointRefitStation>();
                inventory.AddItem("day1.identity-card", "身份认证卡", 2);
                var bag = checks.PointBag;
                Check("Story copies receive unique persistent affixes on acquisition", bag.Count == 2 && bag[0].instanceId != bag[1].instanceId && bag.All(i => i.item.IsValid()));
                string id = bag[0].instanceId, original = JsonUtility.ToJson(bag[0].item);
                Check("Equip a specific copy and reserve its physical inventory", checks.TryEquipPointInstance(id, CheckBehavior.Wealth, out _) && inventory.Count("day1.identity-card") == 1);
                Check("Unequip retains instance and exact randomized effect", checks.TryUnequipPointItem(id, out _) && inventory.Count("day1.identity-card") == 2 && JsonUtility.ToJson(checks.PointBag.First(i => i.instanceId == id).item) == original);
                owner.transform.position = new Vector3(20, 20);
                Check("Configuration denied outside bedroom/warehouse", !checks.TryEquipPointInstance(id, CheckBehavior.Wealth, out _) && !checks.TryEquipAnchor(null, out _));
                owner.transform.position = Vector3.zero;
                Check("Story completion grants anchor once", checks.CreateMentalAnchor(MentalAnchors.LinXi) && checks.CreateMentalAnchor(MentalAnchors.LinXi) && checks.CaptureProgression().anchors.Count == 1);
                Check("Only owned anchor can be equipped", !checks.TryEquipAnchor(MentalAnchors.LuJianshen, out _) && checks.TryEquipAnchor(MentalAnchors.LinXi, out _));
                checks.CreateMentalAnchor(MentalAnchors.LuJianshen); checks.TryEquipAnchor(MentalAnchors.LuJianshen, out _);
                Check("Exactly one anchor is active", checks.EquippedAnchor == MentalAnchors.LuJianshen && checks.CaptureProgression().anchors.Count == 2);
                Check("Tang offers three distinct effects", checks.EnsureRefitOffers(out _) && checks.CaptureProgression().offers.Select(o => o.key).Distinct().Count() == 3);
                string offered = string.Join("|", checks.CaptureProgression().offers.Select(o => JsonUtility.ToJson(o)));
                checks.EnsureRefitOffers(out _);
                Check("Reopening Tang does not reroll offers", offered == string.Join("|", checks.CaptureProgression().offers.Select(o => JsonUtility.ToJson(o))));
                Check("One refresh per story day", checks.RefreshRefitOffers(out _) && !checks.RefreshRefitOffers(out _));
                var affix = checks.CaptureProgression().offers[0];
                Check("Refit replaces original affix and retains item identity", checks.RefitPointItem(id, 0, out _) && checks.PointBag.First(i => i.instanceId == id).item.key == "day1.identity-card" &&
                    checks.PointBag.First(i => i.instanceId == id).item.description == affix.description);
                Check("Daily limit is two refits", checks.EnsureRefitOffers(out _) && checks.RefitPointItem(id, 1, out _) && !checks.EnsureRefitOffers(out _));
                var progressionSnapshot = checks.CaptureSnapshot();
                Check("Progression save keeps affixes, anchors, choices and daily quota", checks.RestoreSnapshot(JsonUtility.FromJson<CheckActorState.Snapshot>(JsonUtility.ToJson(progressionSnapshot))) && checks.CaptureProgression().modificationsUsed == 2 && checks.EquippedAnchor == MentalAnchors.LuJianshen);
                Check("Same day and rollback cannot reset refit quota", checks.BeginStoryDay(1) && !checks.BeginStoryDay(0) && checks.CaptureProgression().modificationsUsed == 2);
                Check("Advancing story day resets both quotas", checks.BeginStoryDay(2) && checks.CaptureProgression().modificationsUsed == 0 && checks.CaptureProgression().refreshesUsed == 0 && checks.CaptureProgression().offers.Count == 0);
                inventory.RemoveItem("day1.identity-card", 1);
                Check("Story item consumption removes its unequipped instance", checks.PointBag.Count == 1);
                var oldSave = checks.CaptureSnapshot(); oldSave.progression.bag.Clear();
                checks.RestoreSnapshot(oldSave); string migrationA = JsonUtility.ToJson(checks.PointBag[0].item); string migrationId = checks.PointBag[0].instanceId;
                checks.RestoreSnapshot(oldSave);
                Check("Reloading pre-affix saves cannot reroll migrated story items", JsonUtility.ToJson(checks.PointBag[0].item) == migrationA && checks.PointBag[0].instanceId == migrationId);

                var gear = new List<FrozenPointItem> { new FrozenPointItem { instanceId = "addon", attribute = CheckBehavior.Officer,
                    item = new PointItemData { key = "test", displayName = "护佑道具", actionAddons = new List<PointActionAddon> {
                        new PointActionAddon { effectKey = "heal", description = "治疗", amount = new PointOperand { constant = 2 } } } } } };
                var addonEngine = Begin(fresh, equipment: gear); Cast(addonEngine); addonEngine.State.hp = 50; addonEngine.Act(CheckBehavior.Officer, 0, out _);
                Check("Equipped action addon actually executes", addonEngine.State.hp == 52 && addonEngine.State.actions[2].calculation.growthPoints == 1);
                var frozenSnapshot = addonEngine.Capture(); gear[0].item.actionAddons[0].amount.constant = 99;
                Check("Active battle freezes equipment effects", addonEngine.State.equipment[0].item.actionAddons[0].amount.constant == 2);
                var copiedSnapshot = JsonUtility.FromJson<PointBattleSnapshot>(JsonUtility.ToJson(frozenSnapshot));
                var mismatch = Enumerable.Range(0, 6).Where(i => JsonUtility.ToJson(frozenSnapshot.session.actions[i].calculation) !=
                    JsonUtility.ToJson(PointBattleEngine.Calculate(frozenSnapshot.session.attributes, (CheckBehavior)i, frozenSnapshot.session.actions[i].effectiveCheckPoints, frozenSnapshot.session.equipment))).ToArray();
                Check("Battle JSON round-trip validates all calculated point chains", PointBattleEngine.Validate(frozenSnapshot, false) && new PointBattleEngine().Restore(copiedSnapshot),
                    "original=" + PointBattleEngine.Validate(frozenSnapshot, false) + " copied=" + PointBattleEngine.Validate(copiedSnapshot, false) + " mismatches=" + string.Join(",", mismatch) +
                    " chart=" + Emerge.Checks.Divination.LiuYaoPaiPan.IsValidResult(frozenSnapshot.session.divination.chart));
                if (!PointBattleEngine.Validate(frozenSnapshot, false)) File.WriteAllText(Path.GetFullPath(Path.Combine(Application.dataPath, "../Validation/point-battle-debug.json")), JsonUtility.ToJson(frozenSnapshot, true));
                frozenSnapshot.session.actions[2].calculation.finalPoints++;
                Check("Tampered final chain is rejected", !PointBattleEngine.Validate(frozenSnapshot, false));
                var bad = addonEngine.Capture(); bad.session.ap = -1; Check("Negative AP save rejected", !PointBattleEngine.Validate(bad, false));
                var castEngine = Begin(fresh); castEngine.RevealLine(); var castingSave = castEngine.Capture();
                var restored = new PointBattleEngine(); restored.Restore(castingSave); Cast(castEngine); Cast(restored);
                Check("Mid-roll save restores the same coins and AP without rerolling", JsonUtility.ToJson(castEngine.State.actions[5]) == JsonUtility.ToJson(restored.State.actions[5]) && castEngine.State.ap == restored.State.ap);
                var consumable = ScriptableObject.CreateInstance<BattleItemDefinition>(); objects.Add(consumable); consumable.inventoryKey = "point-test-potion"; consumable.displayName = "药"; consumable.pointPower = 4;
                inventory.AddItem(consumable.inventoryKey, consumable.displayName, 1);
                var itemBattle = Begin(fresh, inventory: inventory); Cast(itemBattle); itemBattle.State.hp = 50; int ap = itemBattle.State.ap;
                Check("Consumable costs two AP, consumes one copy, uses separate point power", itemBattle.UseItem(consumable, out _) && itemBattle.State.hp == 54 && itemBattle.State.ap == ap - 2 && inventory.Count(consumable.inventoryKey) == 0);
                Check("Absent consumable cannot be reused", !itemBattle.UseItem(consumable, out _));
                var win = Begin(fresh, inventory: inventory); Cast(win); win.State.ap = 100; win.State.enemies[0].hp = .1;
                if (win.State.actions[3].calculation.finalPoints == 0) win.State.damageBonus = 100;
                win.Act(CheckBehavior.Wealth, 0, out _);
                Check("Victory records win round count", win.State.phase == PointBattlePhase.Victory && inventory.Victory("point-test", fresh.balanceVersion)?.lastRounds == 1);
                win.ApplyOutcome(); Check("Victory outcome is idempotent", inventory.Victory("point-test", fresh.balanceVersion)?.wins == 1);
            }
            catch (Exception ex) { Check("Unhandled test exception", false, ex.ToString()); }
            finally { foreach (var obj in objects) if (obj != null) UnityEngine.Object.DestroyImmediate(obj); Write(); }
            return Results.passed;
        }
        public static PointBattleEngine Begin(PointBattleDefinition rules, string anchor = null, int seed = 917, int count = 1,
            List<FrozenPointItem> equipment = null, PropGameState inventory = null)
        {
            var enemies = Enumerable.Range(0, count).Select(i => new PointEnemyProfile { id = "test" + i, name = "测试敌人 " + i, health = 1000,
                pattern = new List<PointEnemyMove> { new PointEnemyMove { amount = 3 } } }).ToList();
            var engine = new PointBattleEngine(inventory);
            engine.Start("point-test", "point-test-context", "测试", "point-test-won", 3, seed, rules, SixKinAttributes.DefaultBuild(), equipment ?? new List<FrozenPointItem>(), anchor, enemies);
            return engine;
        }
        public static void Cast(PointBattleEngine engine) { while (engine.RevealLine()) { } engine.ResolveRound(); }
    }
}
#endif

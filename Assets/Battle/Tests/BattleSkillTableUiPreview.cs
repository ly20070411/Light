#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emerge.Checks;
using Emerge.GameFlow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Emerge.Battle.Tests
{
    public sealed class BattleSkillTableUiPreview : MonoBehaviour
    {
        [Serializable] private sealed class Check { public string name, observed; public bool passed; }
        [Serializable] private sealed class Report { public bool passed; public string completedUtc; public List<Check> checks = new List<Check>(); }
        public static event Action<bool> Completed;
        private readonly Report report = new Report();
        private BattleController controller;

        public void BeginPreview() { StartCoroutine(Run()); }
        private IEnumerator Run()
        {
            var preview = Preview();
            while (true)
            {
                bool next = false; object current = null;
                try { next = preview.MoveNext(); if (next) current = preview.Current; }
                catch (Exception exception) { Add("Skill table UI preview completes", false, exception.ToString()); }
                if (!next) break;
                yield return current;
            }
            if (controller != null) controller.RestoreSnapshot(null);
            report.passed = report.checks.Count > 0 && report.checks.All(c => c.passed);
            report.completedUtc = DateTime.UtcNow.ToString("O");
            Directory.CreateDirectory("Validation");
            File.WriteAllText("Validation/battle-skill-ui-results.json", JsonUtility.ToJson(report, true));
            Debug.Log("BATTLE_SKILL_UI_" + (report.passed ? "PASS" : "FAIL") + " checks=" + report.checks.Count);
            Completed?.Invoke(report.passed);
        }
        private IEnumerator Preview()
        {
            yield return null;
            Directory.CreateDirectory("Validation");
            var catalog = Resources.Load<BattleCatalog>(BattleCatalog.ResourcePath);
            string error = null;
            if (catalog == null || !catalog.Validate(out error)) throw new InvalidOperationException(error ?? "Battle catalog is missing.");
            controller = FindObjectsOfType<BattleController>().First(c => c.gameObject.scene == gameObject.scene && c.GetComponent<PixelPrototype.PlayerMovement>() != null);
            controller.enabled = false;
            var checks = controller.GetComponent<CheckActorState>() ?? controller.gameObject.AddComponent<CheckActorState>();
            checks.attributes = new ActorCheckAttributes { parent = 8 };
            Add("Actual controller starts the new skill catalog", controller.TryBegin(catalog.Encounter("ENC03")), catalog.rules.balanceVersion);
            var returnPoint = controller.CaptureSnapshot().returnPoint;
            string returnInventory = JsonUtility.ToJson(returnPoint.propState, true);
            controller.QuickCast();
            yield return new WaitForSeconds(.8f);
            var view = controller.View;
            Canvas.ForceUpdateCanvases();
            BattleSkillTableUiChecks.Run(view, catalog, Add);
            view.Refresh();
            view.EnemyButton(2).onClick.Invoke();
            Add("Actual enemy button selects its target and gold marker", view.SelectedTarget == 2 && view.GetComponentsInChildren<Outline>().Count(o => o.enabled && o.effectDistance.x == 3) == 1, "selected=" + view.SelectedTarget);
            Add("Every fighter displays matching HP and MP bars", view.GetComponentsInChildren<Text>(true).Count(t => t.text.StartsWith("HP ")) == 4 &&
                view.GetComponentsInChildren<Text>(true).Count(t => t.text.StartsWith("MP ")) == 4, "four HP and four MP labels");
            int targetHP = controller.Engine.State.enemies[2].hp, attackMP = controller.Engine.State.player.mp;
            view.SkillButton("ATK_BASIC").onClick.Invoke();
            Add("Actual attack button damages the selected enemy and spends MP", controller.Engine.State.lastAction.targetIndex == 2 &&
                controller.Engine.State.enemies[2].hp < targetHP && controller.Engine.State.player.mp == attackMP - catalog.Skill("ATK_BASIC").mpCost, "target=" + controller.Engine.State.lastAction.targetIndex);
            var healing = catalog.items.First(i => i.effect == BattleItemEffect.Heal);
            controller.Engine.Inventory.AddItem(healing.inventoryKey, healing.displayName, 1);
            controller.Engine.State.player.hp = catalog.rules.maxHP - 20;
            int itemCount = controller.Engine.Inventory.Count(healing.inventoryKey);
            view.Refresh(); view.SwitchPage(BattlePage.Backpack); view.ItemButton(healing.id).onClick.Invoke();
            Add("Actual backpack button heals and consumes exactly one item", controller.Engine.State.player.hp == Math.Min(catalog.rules.maxHP, catalog.rules.maxHP - 20 + healing.power) &&
                controller.Engine.Inventory.Count(healing.inventoryKey) == itemCount - 1 && controller.Engine.State.phase == BattlePhase.Player, healing.displayName);
            controller.Engine.State.player.mp = catalog.rules.maxMP; view.SwitchPage(BattlePage.Skills); view.Refresh();
            var enhanced = catalog.skills.First(s => controller.Engine.IsEnhanced(s.id));
            Add("Eligible enhancement has a gold button highlight", view.IsEnhancedHighlighted(enhanced.id), enhanced.displayName);
            int beforeMP = controller.Engine.State.player.mp;
            view.SkillButton(enhanced.id).onClick.Invoke();
            Add("Actual enhanced button executes once at the ordinary MP cost", controller.Engine.State.lastAction.enhanced &&
                controller.Engine.State.player.mp == beforeMP - enhanced.mpCost && !view.IsEnhancedHighlighted(enhanced.id), enhanced.displayName);
            yield return new WaitForSeconds(.6f);
            controller.Engine.State.player.statuses.Add(new BattleTimedStatus { kind = BattleStatusKind.DamageUp, power = .2f, rounds = 3 });
            controller.Engine.State.enemies[0].statuses.Add(new BattleTimedStatus { kind = BattleStatusKind.Burn, power = 5, rounds = 2 });
            controller.Engine.State.enemies[0].statuses.Add(new BattleTimedStatus { kind = BattleStatusKind.Thunder, power = 13, rounds = 4, count = 2 });
            controller.Engine.State.summons.Add(new BattleSummonState { kind = BattleSummonKind.Clone, power = 6, remainingRounds = 4 });
            controller.Engine.State.domainRounds = 6; view.Refresh();
            Add("Status and summon UI includes remaining rounds", view.HeroStatusContent.Contains("3 轮") && view.EnemyStatusContent(0).Contains("2 轮") &&
                view.SummonStatusContent.Contains("4 轮") && view.SummonStatusContent.Contains("6 轮"), view.SummonStatusContent);
            yield return Capture("Validation/battle-skill-family-ui.png");
            foreach (BattleFamily family in Enum.GetValues(typeof(BattleFamily))) view.FamilyScroll(family).verticalNormalizedPosition = 0;
            yield return Capture("Validation/battle-skill-family-bottom-ui.png");
            foreach (BattleFamily family in Enum.GetValues(typeof(BattleFamily))) view.FamilyScroll(family).verticalNormalizedPosition = 1;
            var pointer = new PointerEventData(EventSystem.current) { position = new Vector2(Screen.width * .25f, Screen.height * .25f) };
            ExecuteEvents.Execute(view.SkillButton(enhanced.id).gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            Add("Actual skill hover shows authored effects and enhancement rules", view.TooltipVisible && view.TooltipContent.Contains(enhanced.description) &&
                view.TooltipContent.Contains(enhanced.mpCost + " MP") && view.StatusTooltipContent.Contains("3 点"), enhanced.displayName);
            yield return Capture("Validation/battle-skill-tooltip-ui.png"); view.HideTooltip();
            view.SwitchSkillGroup(BattleSkillGroup.Special);
            yield return Capture("Validation/battle-skill-special-ui.png");
            var passive = catalog.skills.First(s => s.isPassive);
            ExecuteEvents.Execute(view.PassiveCard(passive.id).gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            Add("Actual passive hover explains automatic activation", view.TooltipVisible && view.TooltipContent.Contains("自动触发"), passive.displayName);
            yield return Capture("Validation/battle-skill-passive-ui.png"); view.HideTooltip();
            var fate = catalog.skills.First(s => s.kind == BattleSkillKind.SixLineFateGu);
            for (int seed = 1; seed <= 100; seed++)
            {
                controller.Engine.Start(catalog.Encounter("ENC03"), seed, "ui-fate", new ActorCheckAttributes { offspring = 8 });
                controller.QuickCast();
                if (controller.Engine.State.unlockedSkills.Contains(fate.id)) break;
            }
            yield return new WaitForSeconds(.8f);
            view.SwitchSkillGroup(BattleSkillGroup.Families); view.SkillButton(fate.id).onClick.Invoke();
            int randomSerial = controller.Engine.State.randomSerial;
            view.ShowSkillResult(controller.Engine.State.lastAction); view.ShowSkillTooltip(fate.id, pointer.position); view.HideTooltip();
            Add("Fate result overlay displays committed result without rerolling", view.CastVisible && controller.Engine.State.randomSerial == randomSerial &&
                view.LastActionContent.Contains(controller.Engine.State.lastAction.auspicious ? "吉兆" : "凶兆"), "randomSerial=" + randomSerial);
            yield return new WaitForSeconds(.2f);
            yield return Capture("Validation/battle-skill-fate-ui.png");
            controller.Engine.Start(catalog.Encounter("ENC05"), 12637, "ui-retaliation", new ActorCheckAttributes { parent = 8 });
            controller.QuickCast(); yield return new WaitForSeconds(1);
            var clone = catalog.skills.First(s => s.kind == BattleSkillKind.WaterClone);
            ExecuteEvents.Execute(view.SkillButton(clone.id).gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            Add("New direct and summon skills explain retaliation", view.TooltipContent.Contains("基础反震预估") &&
                view.TooltipContent.Contains("未含暴击 / 追加") && view.TooltipContent.Contains("由主角承受反震"), view.TooltipContent);
            yield return Capture("Validation/battle-skill-retaliation-ui.png"); view.HideTooltip();
            var enemy = controller.Engine.State.enemies[0];
            enemy.intentSkillId = catalog.Enemy(enemy.definitionId).skills.First(s => s.mpCost > 0).id;
            enemy.statuses.Add(new BattleTimedStatus { kind = BattleStatusKind.Taunt, power = 1, rounds = 2 });
            view.ShowEnemyTooltip(0, pointer.position);
            Add("Taunted special intent explicitly becomes a basic attack", view.TooltipContent.Contains("改为基础普攻") && view.TooltipContent.Contains("不执行上方特殊效果"), view.TooltipContent);
            view.HideTooltip();
            controller.Engine.Inventory.SetFlag("ui-preview-temporary");
            controller.transform.position += new Vector3(1, 1, 0);
            controller.GetComponent<Rigidbody2D>().position = controller.transform.position;
            view.SwitchPage(BattlePage.Escape); view.EscapeButton.onClick.Invoke();
            Add("Actual escape button restores the battle return point and closes combat", controller.Engine.State == null && !BattleController.AnyBattleActive &&
                controller.Engine.Inventory.CaptureJson() == returnInventory && Vector2.Distance(controller.GetComponent<Rigidbody2D>().position, returnPoint.position) < .01f &&
                GameSessionController.GameplayInputAllowed, "return position=" + controller.transform.position);
        }
        private static IEnumerator Capture(string path)
        {
            Canvas.ForceUpdateCanvases(); yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(path); yield return null;
        }
        private void Add(string name, bool passed, string observed) { report.checks.Add(new Check { name = name, passed = passed, observed = observed }); }
    }
}
#endif

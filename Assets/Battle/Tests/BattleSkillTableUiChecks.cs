#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Emerge.Battle.Tests
{
    /// <summary>Independent UI checks callable from the battle preview after building its view.</summary>
    public static class BattleSkillTableUiChecks
    {
        public static void Run(BattleView view, BattleCatalog catalog, Action<string, bool, string> report)
        {
            if (view == null || catalog == null) throw new ArgumentNullException(view == null ? nameof(view) : nameof(catalog));
            var active = catalog.skills.Where(s => !s.isPassive).Select(s => s.id).ToArray();
            var passive = catalog.skills.Where(s => s.isPassive).Select(s => s.id).ToArray();
            report("UI includes every active skill and every passive", active.OrderBy(x => x).SequenceEqual(view.RenderedActiveSkillIds.OrderBy(x => x)) &&
                passive.OrderBy(x => x).SequenceEqual(view.RenderedPassiveSkillIds.OrderBy(x => x)), "active=" + active.Length + "; passive=" + passive.Length);
            report("Passive skill cards cannot be clicked as actions", passive.All(id => view.SkillButton(id) == null && view.PassiveCard(id) != null &&
                view.PassiveCard(id).GetComponent<Button>() == null), "passive cards=" + passive.Length);
            report("Skill buttons contain only their authored names", active.All(id => view.SkillButton(id).GetComponentInChildren<Text>(true).text == catalog.Skill(id).displayName), "checked " + active.Length + " labels");
            report("Every family has an independently clipped vertical skill list", view.SkillColumnScrollCount == 5 &&
                Enum.GetValues(typeof(BattleFamily)).Cast<BattleFamily>().All(f =>
                {
                    var scroll = view.FamilyScroll(f);
                    return scroll != null && scroll.vertical && !scroll.horizontal && scroll.viewport.GetComponent<RectMask2D>() != null &&
                        scroll.content.sizeDelta.y >= scroll.viewport.sizeDelta.y;
                }), "five independent ScrollRects");
            view.SwitchPage(BattlePage.Skills); view.SwitchSkillGroup(BattleSkillGroup.Special);
            report("Ultimate skills and passive cards are visible in the special group", catalog.skills.Where(s => s.isUltimate && !s.isPassive).All(s => view.SkillButton(s.id).gameObject.activeInHierarchy) &&
                passive.All(id => view.PassiveCard(id).gameObject.activeInHierarchy), "special group");
            view.SwitchSkillGroup(BattleSkillGroup.Families);
            report("Special skills are isolated from the family scroll lists", catalog.skills.Where(s => s.isUltimate && !s.isPassive).All(s => !view.SkillButton(s.id).gameObject.activeInHierarchy) &&
                passive.All(id => !view.PassiveCard(id).gameObject.activeInHierarchy), "family group");
            foreach (BattleFamily family in Enum.GetValues(typeof(BattleFamily)))
            {
                var scroll = view.FamilyScroll(family);
                scroll.verticalNormalizedPosition = 0;
                Canvas.ForceUpdateCanvases();
                string last = catalog.skills.LastOrDefault(s => s.family == family && !s.isUltimate && !s.isPassive)?.id;
                bool finalSkillVisible = last == null;
                if (last != null)
                {
                    var buttonCorners = new Vector3[4]; var viewportCorners = new Vector3[4];
                    ((RectTransform)view.SkillButton(last).transform).GetWorldCorners(buttonCorners);
                    scroll.viewport.GetWorldCorners(viewportCorners);
                    finalSkillVisible = buttonCorners[0].y >= viewportCorners[0].y - .5f && buttonCorners[1].y <= viewportCorners[1].y + .5f &&
                        buttonCorners[0].x >= viewportCorners[0].x - .5f && buttonCorners[2].x <= viewportCorners[2].x + .5f;
                }
                report("Family list can scroll to its final skill: " + BattleRules.FamilyName(family), last == null ||
                    view.SkillButton(last).transform.parent == scroll.content && finalSkillVisible, last ?? "empty");
                scroll.verticalNormalizedPosition = 1;
            }
        }
    }
}
#endif

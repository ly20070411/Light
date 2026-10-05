using System;
using System.Collections.Generic;
using System.Linq;
using Emerge.GameFlow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Emerge.Battle
{
    // Runtime uGUI: replace the presentation sprites without rebuilding the demo scenes.
    public sealed class BattleView : MonoBehaviour
    {
        private sealed class Fighter
        {
            public RectTransform root, hpFill, mpFill;
            public Image portrait;
            public Outline outline;
            public Text name, hp, mp, status, marker;
            public Button button;
            public BattlePortraitMotion motion;
            public float width;
        }
        private BattleController owner;
        private BattlePresentation art;
        private Canvas canvas;
        private CanvasScaler scaler;
        private CanvasGroup group;
        private Font font;
        private RectTransform root, castLayer, result, tooltipLayer, mainTooltip, stateTooltip;
        private Text title, hint, castTitle, castLines, castResult, resultText, tooltipText, stateText, fleeText, quickText, roundInfo;
        private Button end, quick, flee;
        private Fighter hero;
        private readonly List<Fighter> enemies = new List<Fighter>();
        private readonly Dictionary<string, Button> skills = new Dictionary<string, Button>(), items = new Dictionary<string, Button>();
        private readonly Dictionary<string, Text> itemLabels = new Dictionary<string, Text>();
        private readonly List<RectTransform> pages = new List<RectTransform>();
        private readonly List<Image> tabs = new List<Image>();
        private readonly List<Image> coins = new List<Image>();
        private readonly List<Vector2> coinHomes = new List<Vector2>();
        private Image entryFade;
        private int target;
        private string sessionId;
        private BattleAction shownCast;
        private float castUntil, resultAt, entryRemaining;
        private static readonly Color Gold = new Color(.92f, .76f, .43f);
        private static readonly Color Panel = new Color(.10f, .15f, .21f);
        public int SelectedTarget => target;
        public BattlePage CurrentPage { get; private set; }
        public bool TooltipVisible => tooltipLayer != null && tooltipLayer.gameObject.activeSelf;
        public string TooltipContent => tooltipText != null ? tooltipText.text : "";
        public string StatusTooltipContent => stateTooltip != null && stateTooltip.gameObject.activeSelf ? stateText.text : "";
        public bool CastVisible => castLayer != null && castLayer.gameObject.activeSelf;
        public void SelectTarget(int index)
        {
            var s = owner?.Engine?.State;
            if (s?.phase != BattlePhase.Player || index < 0 || index >= s.enemies.Count || s.enemies[index].hp <= 0) return;
            target = index; HideTooltip(); Refresh();
        }
        public void Build(BattleController controller)
        {
            if (root != null) return;
            owner = controller; art = Resources.Load<BattlePresentation>(BattlePresentation.ResourcePath);
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Noto Sans CJK SC", "Arial" }, 24);
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            gameObject.layer = 5;
            canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 500;
            var menuCamera = GameSessionController.Instance?.GetComponentInChildren<Camera>();
            if (menuCamera != null) { canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = menuCamera; canvas.planeDistance = 2; }
            scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            gameObject.AddComponent<GraphicRaycaster>(); group = gameObject.AddComponent<CanvasGroup>();
            if (FindObjectOfType<EventSystem>() == null) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            var viewport = PanelAt(transform, "战斗遮幕", 0, 0, 0, 0, Color.black);
            viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one; viewport.offsetMin = viewport.offsetMax = Vector2.zero;
            root = PanelAt(transform, "战斗场景", 0, 0, 1600, 900, new Color(.035f, .065f, .105f));
            root.anchorMin = root.anchorMax = new Vector2(.5f, .5f); root.anchoredPosition = new Vector2(-800, 450);
            Picture(root, "战斗背景", 0, 0, 1600, 610, art?.background, new Color(.12f, .18f, .24f), false);
            Label(root, 36, 22, 490, 38, "六爻战斗", 27, Gold);
            title = Label(root, 530, 22, 610, 38, "", 24, Color.white); title.alignment = TextAnchor.MiddleCenter;
            var guidance = Label(root, 530, 65, 610, 28, "点击选敌 · 悬停查看意图", 18, new Color(.65f, .75f, .82f)); guidance.alignment = TextAnchor.MiddleCenter;
            roundInfo = Label(root, 500, 170, 410, 270, "", 19, new Color(.72f, .8f, .85f)); roundInfo.alignment = TextAnchor.MiddleCenter;
            hero = FighterAt("主角立绘", 110, 130, 290, 350,
                controller.character != null ? controller.character.portrait : art?.heroPortrait, -1);
            for (int i = 0; i < 3; i++) enemies.Add(FighterAt("敌人立绘 " + (i + 1), 950 + i * 200, 170, 185, 310, art?.defaultEnemyPortrait, i));
            BuildCommands(); BuildTooltip(); BuildCasting();
            result = PanelAt(root, "结果遮幕", 0, 0, 1600, 900, new Color(0, 0, 0, .65f), true);
            var modal = PanelAt(result, "战斗结果", 440, 250, 720, 390, Panel); Border(modal);
            resultText = Label(modal, 40, 40, 640, 220, "", 29, Gold); resultText.alignment = TextAnchor.MiddleCenter;
            ButtonAt(modal, 170, 285, 380, 65, "返回场景", () => owner.CloseResult(), out _);
            result.gameObject.SetActive(false);
            entryFade = PanelAt(root, "进入战斗过渡", 0, 0, 1600, 900, Color.black, true).GetComponent<Image>();
            entryFade.gameObject.SetActive(false); SwitchPage(BattlePage.Skills);
        }
        private void BuildCommands()
        {
            var panel = PanelAt(root, "操作面板", 16, 609, 1568, 278, Panel); Border(panel);
            string[] names = { "技能", "背包", "交涉", "逃跑" };
            Sprite[] icons = { art?.skillIcon, art?.bagIcon, art?.talkIcon, art?.fleeIcon };
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                var b = ButtonAt(panel, i * 232, 0, 228, 54, names[i], () => SwitchPage((BattlePage)index), out var label);
                Position(label.rectTransform, 68, 8, 146, 38); tabs.Add(b.GetComponent<Image>());
                Picture(b.transform, names[i] + "图标", 22, 11, 32, 32, icons[i], Gold);
                pages.Add(PanelAt(panel, names[i] + "页面", 12, 65, 1544, 175, Color.clear));
            }
            for (int family = 0; family < 5; family++)
            {
                float x = 16 + family * 306;
                var header = Label(pages[0], x, 0, 290, 28, BattleRules.FamilyName((BattleFamily)family), 23, Gold);
                header.alignment = TextAnchor.MiddleCenter; int row = 0;
                foreach (var d in owner.catalog.skills.Where(s => (int)s.family == family))
                {
                    string id = d.id;
                    var b = ButtonAt(pages[0], x, 38 + row++ * 45, 290, 38, d.displayName, () => owner.UseSkill(id, target), out _);
                    skills.Add(id, b); b.gameObject.AddComponent<BattleHoverTarget>().view = this;
                    b.GetComponent<BattleHoverTarget>().skillId = id;
                }
            }
            for (int i = 0; i < owner.catalog.items.Length; i++)
            {
                var d = owner.catalog.items[i]; string id = d.id;
                var b = ButtonAt(pages[1], 18 + i * 379, 22, 360, 117, "", () => owner.UseItem(id), out var label);
                Position(label.rectTransform, 98, 12, 244, 92); label.fontSize = 19; label.alignment = TextAnchor.MiddleLeft;
                Picture(b.transform, "物品图标", 18, 26, 64, 64, art?.itemIcon, Gold);
                items.Add(id, b); itemLabels.Add(id, label);
                var hover = b.gameObject.AddComponent<BattleHoverTarget>(); hover.view = this; hover.itemId = id;
            }
            var negotiation = Label(pages[2], 160, 20, 1224, 128, "交涉\n预留剧情入口", 24, new Color(.62f, .7f, .77f));
            negotiation.alignment = TextAnchor.MiddleCenter;
            fleeText = Label(pages[3], 210, 4, 1120, 62, "", 21, Color.white); fleeText.alignment = TextAnchor.MiddleCenter;
            flee = ButtonAt(pages[3], 622, 80, 300, 52, "逃跑", () => owner.Flee(), out _);
            hint = Label(panel, 24, 242, 1160, 28, "", 18, Gold);
            end = ButtonAt(panel, 1280, 239, 264, 34, "结束回合 →", () => owner.EndTurn(), out _);
            end.GetComponent<Image>().color = new Color(.32f, .29f, .15f);
        }
        private Fighter FighterAt(string name, float x, float y, float w, float h, Sprite sprite, int index)
        {
            var f = new Fighter { width = w };
            f.root = PanelAt(root, name, x, y, w, h + 112, Color.clear, index >= 0);
            f.portrait = Picture(f.root, "立绘", 4, 0, w - 8, h, sprite, Color.white);
            f.outline = f.portrait.gameObject.AddComponent<Outline>(); f.outline.effectColor = Gold; f.outline.effectDistance = new Vector2(3, -3); f.outline.enabled = false;
            f.marker = Label(f.root, 0, -35, w, 28, "▼ 当前目标", 20, Gold); f.marker.alignment = TextAnchor.MiddleCenter;
            f.marker.gameObject.SetActive(false);
            f.name = Label(f.root, 0, h + 1, w, 28, index < 0 ? (owner.character != null ? owner.character.DisplayName : "主角") : "", 23, Color.white); f.name.alignment = TextAnchor.MiddleCenter;
            f.hpFill = Bar(f.root, "红色血条", w, h + 32, new Color(.78f, .20f, .24f), out f.hp);
            f.mpFill = Bar(f.root, "蓝色法力条", w, h + 57, new Color(.16f, .48f, .85f), out f.mp);
            f.status = Label(f.root, 0, h + 82, w, 52, "", 16, Gold); f.status.alignment = TextAnchor.UpperCenter;
            f.status.resizeTextForBestFit = true; f.status.resizeTextMinSize = 12; f.status.resizeTextMaxSize = 16;
            f.motion = f.portrait.gameObject.AddComponent<BattlePortraitMotion>(); f.motion.portrait = f.portrait;
            f.motion.effect = Picture(f.portrait.transform, "行动特效", 0, h * .15f, w - 8, h * .65f, null, Color.clear);
            f.motion.effect.gameObject.SetActive(false); f.motion.ResetHome();
            if (index >= 0)
            {
                f.button = f.root.gameObject.AddComponent<Button>(); f.button.transition = Selectable.Transition.None;
                f.button.onClick.AddListener(() => SelectTarget(index));
                var hover = f.root.gameObject.AddComponent<BattleHoverTarget>(); hover.view = this; hover.enemyIndex = index;
            }
            return f;
        }
        private RectTransform Bar(Transform parent, string name, float w, float y, Color color, out Text label)
        {
            var back = PanelAt(parent, name, 0, y, w, 21, new Color(.02f, .04f, .07f));
            var fill = PanelAt(back, "填充", 2, 2, w - 4, 17, color);
            label = Label(back, 4, 0, w - 8, 21, "", 15, Color.white); label.alignment = TextAnchor.MiddleCenter; return fill;
        }
        public void SwitchPage(BattlePage page)
        {
            if ((int)page < 0 || (int)page >= pages.Count) return;
            CurrentPage = page; HideTooltip();
            for (int i = 0; i < pages.Count; i++) { pages[i].gameObject.SetActive(i == (int)page); tabs[i].color = i == (int)page ? new Color(.28f, .26f, .17f) : Panel; }
        }
        private void BuildTooltip()
        {
            var go = new GameObject("悬浮详情", typeof(RectTransform)); go.layer = 5; go.transform.SetParent(root, false); tooltipLayer = (RectTransform)go.transform;
            mainTooltip = PanelAt(tooltipLayer, "技能与意图详情", 0, 0, 370, 180, new Color(.06f, .095f, .14f, .98f)); Border(mainTooltip);
            tooltipText = Label(mainTooltip, 15, 12, 340, 160, "", 18, Color.white); tooltipText.lineSpacing = 1.12f;
            stateTooltip = PanelAt(tooltipLayer, "附加状态说明", 0, 180, 370, 160, new Color(.13f, .14f, .13f, .98f)); Border(stateTooltip);
            stateText = Label(stateTooltip, 15, 12, 340, 140, "", 18, Gold); stateText.lineSpacing = 1.12f;
            HideTooltip();
        }
        public void ShowSkillTooltip(string id, Vector2 pointer)
        {
            if (owner.Engine.State?.phase != BattlePhase.Player || !GameSessionController.SessionInputAllowed) return;
            var d = owner.catalog.Skill(id); if (d == null) return;
            string content = BattleDescriptions.Skill(d, owner.catalog.rules, owner.Engine.State.version >= 3);
            if (owner.Engine.State.version >= 2) content += (owner.Engine.State.version >= 3 ? "\n属性固定倍率：×" : "\n本轮倍率：×") + owner.Engine.SkillMultiplier(id).ToString("0.0") + "\n本场剩余：" + (owner.Engine.RemainingUses(id) == int.MaxValue ? "不限次数" : owner.Engine.RemainingUses(id) + " / " + d.maximumUses + " 次") + (d.alwaysAvailable ? "\n常驻技能" : "\n高级技能 · 每轮定卦解锁");
            if (owner.Engine.State.version >= 3)
            {
                float multiplier = owner.Engine.SkillMultiplier(id); int value = BattleRules.Round(d.power * multiplier);
                var p = owner.Engine.State.player; var rules = owner.catalog.rules;
                string effect = d.effect == BattleEffect.Reduction ? "实际减伤 " + Mathf.Min(rules.reductionCap, d.power * multiplier).ToString("P0") :
                    d.effect == BattleEffect.Shield ? "本次新增护盾 " + Mathf.Min(rules.shieldCap - p.shield, value) :
                    d.effect == BattleEffect.Heal ? "本次回复 HP " + Mathf.Min(rules.maxHP - p.hp, value) :
                    d.effect == BattleEffect.Damage ? "基础伤害 " + value :
                    d.effect == BattleEffect.NextMana || d.effect == BattleEffect.Cleanse ? "下轮额外回气 " + Mathf.Min(rules.nextManaCap, value) :
                    d.effect == BattleEffect.Regeneration ? "每轮回复 HP " + value : "控制持续一次行动";
                content += "\n" + effect;
                if (d.effect == BattleEffect.Damage && owner.Engine.HasRetaliationTarget(d.target, target))
                    content += "\n触发反震，当前预计损失 " + owner.Engine.ForecastRetaliation(id, target) + " HP。";
            }
            if (!owner.Engine.CanUseSkill(id, target, out var reason)) content += "\n" + reason;
            ShowTooltip(content, BattleDescriptions.SkillStatuses(d, owner.catalog.rules, owner.Engine.State.version >= 3), pointer);
        }
        public void ShowItemTooltip(string id, Vector2 pointer)
        {
            if (owner.Engine.State?.phase != BattlePhase.Player || !GameSessionController.SessionInputAllowed) return;
            var d = owner.catalog.Item(id); if (d == null) return;
            string content = BattleDescriptions.Item(d);
            if (d.effect == BattleItemEffect.DamageAll && owner.Engine.HasRetaliationTarget(BattleTarget.AllEnemies, target))
                content += "\n触发反震，当前预计损失 " + owner.Engine.ForecastRetaliation(id, target, true) + " HP。";
            if (!owner.Engine.CanUseItem(id, out var reason)) content += "\n" + reason;
            ShowTooltip(content, d.effect == BattleItemEffect.DamageAll ? "易伤\n散灵符的直接伤害同样受目标易伤影响，并消耗一次命中次数。" : "", pointer);
        }
        public void ShowEnemyTooltip(int index, Vector2 pointer)
        {
            var s = owner.Engine.State;
            if (s == null || s.phase == BattlePhase.Casting || s.phase == BattlePhase.RoundCasting || index < 0 || index >= s.enemies.Count || !GameSessionController.SessionInputAllowed) return;
            var e = s.enemies[index]; ShowTooltip(BattleDescriptions.Intent(owner.catalog.Enemy(e.definitionId), e, owner.catalog.rules, s.version >= 3), "", pointer);
        }
        private void ShowTooltip(string content, string status, Vector2 pointer)
        {
            tooltipText.text = content; stateText.text = status;
            float h = Mathf.Clamp(tooltipText.preferredHeight + 28, 90, 340), sh = string.IsNullOrEmpty(status) ? 0 : Mathf.Clamp(stateText.preferredHeight + 28, 80, 270);
            Position(mainTooltip, 0, 0, 370, h); Position(tooltipText.rectTransform, 15, 12, 340, h - 24);
            Position(stateTooltip, 0, h, 370, sh); Position(stateText.rectTransform, 15, 12, 340, Mathf.Max(0, sh - 24)); stateTooltip.gameObject.SetActive(sh > 0);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, pointer, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var local);
            Position(tooltipLayer, Mathf.Clamp(local.x + 18, 12, 1218), Mathf.Clamp(-local.y - h - sh - 16, 65, 888 - h - sh), 370, h + sh);
            tooltipLayer.gameObject.SetActive(true);
        }
        public void HideTooltip() { if (tooltipLayer != null) tooltipLayer.gameObject.SetActive(false); }
        private void BuildCasting()
        {
            castLayer = PanelAt(root, "定卦遮幕", 0, 0, 1600, 900, new Color(0, 0, 0, .43f), true);
            var modal = PanelAt(castLayer, "铜币定卦", 570, 82, 460, 450, Panel); Border(modal);
            castTitle = Label(modal, 20, 14, 420, 40, "", 25, Gold); castTitle.alignment = TextAnchor.MiddleCenter;
            for (int i = 0; i < 3; i++) { var c = Picture(modal, "铜币 " + i, 75 + i * 120, 76, 65, 65, art?.coinFront, Gold); coins.Add(c); coinHomes.Add(c.rectTransform.anchoredPosition); }
            var text = Label(modal, 24, 147, 412, 28, "六爻自下而上逐次揭示", 18, Color.white); text.alignment = TextAnchor.MiddleCenter;
            castLines = Label(modal, 46, 179, 368, 139, "", 18, new Color(.78f, .85f, .9f));
            castResult = Label(modal, 24, 320, 412, 60, "", 20, Gold); castResult.alignment = TextAnchor.MiddleCenter;
            quick = ButtonAt(modal, 24, 392, 412, 39, "快速定卦", () => { if (owner.Engine.State?.pending != null || owner.Engine.State?.phase == BattlePhase.RoundCasting) owner.QuickCast(); else HideCastResult(); }, out quickText);
            castLayer.gameObject.SetActive(false);
        }
        private void RenderCast(BattleAction action, bool resolved)
        {
            castTitle.text = action.skillName + " · 定卦"; var r = action.divination;
            var lines = new List<string>();
            for (int i = 5; i >= 0; i--)
            {
                if (i >= r.revealedLines) lines.Add("···    未揭示");
                else { var y = r.chart.yaos[i]; lines.Add(y.benSymbol + "  " + y.yaowei + " " + y.benLiuqin + " " + y.wangshuaiScore.ToString("+0;-0;0") + (y.isDongYao ? " 动" : "")); }
            }
            castLines.text = string.Join("\n", lines);
            castResult.text = resolved ? r.chart.benGuaName + " → " + r.chart.bianGuaName + "\n评分 " + action.score + (owner.Engine.State.version >= 3 ? " 点 · 固定倍率 ×" : " · 倍率 ×") + action.multiplier.ToString("0.0") : "已揭示 " + r.revealedLines + " / 6";
            quickText.text = resolved ? "继续" : "快速定卦";
            if (resolved) for (int i = 0; i < coins.Count; i++) { coins[i].rectTransform.anchoredPosition = coinHomes[i]; coins[i].transform.localScale = Vector3.one; coins[i].transform.localRotation = Quaternion.identity; SetCoinFace(i, 5); }
        }
        private void SetCoinFace(int index, int line)
        { if (art != null && shownCast != null) coins[index].sprite = shownCast.divination.casting.coinFaces[Mathf.Clamp(line, 0, 5) * 3 + index] == 1 ? art.coinFront : art.coinBack; }
        private void RenderRound(bool resolved)
        {
            var s = owner.Engine.State;
            shownCast = new BattleAction { skillName = "第 " + s.round + " 轮", divination = s.roundDivination };
            RenderCast(shownCast, resolved); castTitle.text = "第 " + s.round + " 轮 · 定卦";
            if (resolved) { castResult.text = s.roundDivination.chart.benGuaName + " → " + s.roundDivination.chart.bianGuaName + (s.version >= 3 ? "\n本轮高级技能已解锁 · 效果由加点固定" : "\n本轮技能与倍率已确定"); quickText.text = "开始行动"; }
        }
        public void ShowRoundResult()
        { castUntil = Time.time + .65f; HideTooltip(); RenderRound(true); castLayer.gameObject.SetActive(true); }
        public void ShowSkillResult(BattleAction action)
        {
            if (action == null) return;
            if (owner.Engine.State.version == 1) { shownCast = action; castUntil = resultAt = Time.time + .85f; HideTooltip(); RenderCast(action, true); castLayer.gameObject.SetActive(true); result.gameObject.SetActive(false); }
            else { castUntil = 0; resultAt = Time.time + .55f; HideTooltip(); castLayer.gameObject.SetActive(false); result.gameObject.SetActive(false); }
            bool attack = action.effect == BattleEffect.Damage || action.effect == BattleEffect.Bind || action.effect == BattleEffect.Silence;
            var fx = attack ? art?.attackEffect : action.effect == BattleEffect.Shield || action.effect == BattleEffect.Reduction ? art?.shieldEffect : art?.healingEffect;
            hero.motion.Play(attack ? 35 : 0, attack ? null : fx, attack ? Gold : new Color(.4f, 1, .75f));
            if (attack) for (int i = 0; i < owner.Engine.State.enemies.Count; i++) if (action.target == BattleTarget.AllEnemies || i == action.targetIndex) enemies[i].motion.Play(10, fx, new Color(1, .4f, .35f), true);
        }
        public void PlayEnemyAction(int index, string skillId)
        {
            var s = owner.Engine.State; if (index < 0 || index >= s.enemies.Count) return;
            var e = s.enemies[index]; var d = owner.catalog.Enemy(e.definitionId); var skill = d.skills.First(k => k.id == skillId);
            if (e.silenced && skill.mpCost > 0) skill = d.skills.First(k => k.mpCost == 0 && k.effect == EnemyEffect.Damage);
            bool attack = BattleEngine.IsAttack(skill.effect);
            enemies[index].motion.Play(attack ? -27 : 0, attack ? null : skill.effect == EnemyEffect.Heal ? art?.healingEffect : art?.shieldEffect, Gold);
            if (attack) hero.motion.Play(-8, art?.attackEffect, new Color(1, .4f, .35f), true);
        }
        public void PlayItemAction(string id)
        {
            if (owner.catalog.Item(id).effect == BattleItemEffect.DamageAll)
            { for (int i = 0; i < owner.Engine.State.enemies.Count; i++) if (owner.Engine.State.enemies[i].hp > 0) enemies[i].motion.Play(10, art?.attackEffect, new Color(1, .4f, .35f), true); }
            else hero.motion.Play(0, art?.healingEffect, new Color(.4f, 1, .75f));
        }
        private void HideCastResult() { castUntil = 0; if (castLayer != null) castLayer.gameObject.SetActive(false); Refresh(); }
        public void Refresh()
        {
            var engine = owner?.Engine; var s = engine?.State; if (s == null || root == null) return;
            if (sessionId != s.sessionId) { sessionId = s.sessionId; target = 0; shownCast = null; castUntil = resultAt = 0; entryRemaining = .24f; entryFade.gameObject.SetActive(true); SwitchPage(BattlePage.Skills); }
            if (target < 0 || target >= s.enemies.Count || s.enemies[target].hp <= 0) { target = s.enemies.FindIndex(e => e.hp > 0); if (target < 0) target = 0; }
            var p = s.player; var rules = owner.catalog.rules;
            title.text = owner.catalog.Encounter(s.encounterId).displayName + " · 第 " + s.round + " 轮 · " + PhaseName(s.phase);
            RefreshBars(hero, p.hp, rules.maxHP, p.mp, rules.maxMP);
            hero.status.text = (p.shield > 0 ? "护盾 " + p.shield + "  " : "") + (p.reduction > 0 ? "减伤 " + p.reduction.ToString("P0") : "") +
                (p.weakness > 0 ? " 削弱 " + p.weakness.ToString("P0") : "") + (p.regenerationTicks > 0 ? "\n生息 " + p.regeneration + " × " + p.regenerationTicks : "") + (p.nextMana > 0 ? " 下轮回蓝 +" + p.nextMana : "");
            if (p.burnTicks > 0) hero.status.text += "\n灼伤 " + p.burn + " × " + p.burnTicks;
            if (p.exposure > 0) hero.status.text += " 破绽 +" + p.exposure.ToString("P0");
            roundInfo.text = s.version >= 2 && s.roundDivination.revealedLines == 6 ? "本轮 · " + s.roundDivination.chart.benGuaName + "\n\n" + string.Join("\n", s.unlockedSkills.Where(id => engine.RemainingUses(id) > 0).Select(id => owner.catalog.Skill(id).displayName + " ×" + engine.SkillMultiplier(id).ToString("0.0"))) + "\n\n基础技能常驻 · 高级技能限次" : s.version == 1 ? "旧版战斗 · 逐技能定卦" : "正在定卦…";
            for (int i = 0; i < enemies.Count; i++)
            {
                var f = enemies[i]; f.root.gameObject.SetActive(i < s.enemies.Count); if (i >= s.enemies.Count) continue;
                float center = s.enemies.Count == 1 ? 1260 : s.enemies.Count == 2 ? 1100 + i * 260 : 1042 + i * 200;
                f.root.anchoredPosition = new Vector2(center - f.width / 2, -170);
                var e = s.enemies[i]; var d = owner.catalog.Enemy(e.definitionId);
                f.portrait.sprite = d.battlePortrait != null ? d.battlePortrait : art?.defaultEnemyPortrait;
                f.portrait.canvasRenderer.SetAlpha(e.hp <= 0 ? .25f : 1);
                f.name.text = d.displayName; RefreshBars(f, e.hp, e.maxHP, e.mp, d.maxMP);
                f.status.text = (e.hp <= 0 ? "已倒下" : "") + (e.charged ? "蓄势 " : "") + (e.shield > 0 ? "护盾 " + e.shield + "  " : "") + (e.vulnerabilityHits > 0 ? "易伤 " + e.vulnerabilityHits + " 次 " : "") +
                    (e.stunned ? "眩晕 " : "") + (e.silenced ? "封诀 " : "") + (e.weakness > 0 ? "削弱 " + e.weakness.ToString("P0") : "") + (s.version >= 3 && e.hp > 0 && d.retaliation > 0 ? " 反震 " + d.retaliation.ToString("P0") : "");
                f.outline.enabled = i == target && e.hp > 0; f.marker.gameObject.SetActive(f.outline.enabled); f.button.interactable = s.phase == BattlePhase.Player && e.hp > 0;
            }
            foreach (var pair in skills) pair.Value.interactable = engine.CanUseSkill(pair.Key, target, out _);
            foreach (var pair in items)
            {
                var d = owner.catalog.Item(pair.Key); pair.Value.interactable = engine.CanUseItem(pair.Key, out _);
                itemLabels[pair.Key].text = d.displayName + " ×" + engine.Inventory.Count(d.inventoryKey) + "\n" + d.description + "\n点击使用";
            }
            end.interactable = s.phase == BattlePhase.Player; flee.interactable = owner.CanFlee;
            fleeText.text = owner.CanFlee ? "离开战斗，恢复进入前的位置、背包与剧情状态。" : s.phase == BattlePhase.Player ? "此旧存档没有战前快照，无法回退。" : "请等待当前行动结束。";
            hint.text = s.phase == BattlePhase.Player ? "剩余 MP " + p.mp + " · 预告直接伤害 " + engine.ForecastDamage() + " · 自行结束回合 · 下轮回蓝 +" + rules.roundMana : s.phase == BattlePhase.RoundCasting ? (s.version >= 3 ? "每轮定卦 · 按五亲加点解锁高级技能 · 倍率由加点固定" : "每轮定卦 · 决定本轮高级技能与倍率") : s.phase == BattlePhase.Casting ? "旧版逐技能定卦" : s.phase == BattlePhase.Enemy ? "敌方依次行动…" : "本场战斗已结束";
            if (s.phase != BattlePhase.Player) HideTooltip();
            if (s.phase == BattlePhase.RoundCasting) { RenderRound(false); castLayer.gameObject.SetActive(true); }
            else if (s.pending != null) { shownCast = s.pending; RenderCast(shownCast, false); castLayer.gameObject.SetActive(true); }
            else if (shownCast == null || Time.time >= castUntil) castLayer.gameObject.SetActive(false);
            bool finished = (s.phase == BattlePhase.Victory || s.phase == BattlePhase.Defeat) && Time.time >= resultAt; result.gameObject.SetActive(finished);
            if (finished)
            {
                var record = engine.Inventory.Victory(s.encounterId, s.version == 1 ? "legacy" : rules.balanceVersion);
                resultText.text = (s.phase == BattlePhase.Victory ? "战斗胜利" : "战斗失败") + "\n\n本次 " + s.round + " 轮" + (s.phase == BattlePhase.Victory && record != null ? " · 最佳 " + record.bestRounds + " 轮\n累计胜利 " + record.wins + " 次 · " + (record.bestRounds <= owner.catalog.Encounter(s.encounterId).swiftVictoryRounds ? "已达成速战" : "已达成首胜") : "") + "\n再次入战恢复 HP / MP，保留物品消耗";
            }
        }
        private void RefreshBars(Fighter f, int hp, int maxHP, int mp, int maxMP)
        { f.hp.text = "HP " + hp + " / " + maxHP; f.mp.text = "MP " + mp + " / " + maxMP; f.hpFill.sizeDelta = new Vector2((f.width - 4) * Mathf.Clamp01((float)hp / maxHP), 17); f.mpFill.sizeDelta = new Vector2((f.width - 4) * (maxMP == 0 ? 0 : Mathf.Clamp01((float)mp / maxMP)), 17); }
        private void Update()
        {
            if (group == null || owner == null || owner.Engine == null) return;
            scaler.matchWidthOrHeight = (float)Screen.width / Mathf.Max(1, Screen.height) >= 1600f / 900 ? 1 : 0;
            group.interactable = GameSessionController.SessionInputAllowed; group.blocksRaycasts = true;
            if (!group.interactable) { HideTooltip(); entryRemaining = 0; entryFade.gameObject.SetActive(false); }
            if (entryRemaining > 0) { entryRemaining = Mathf.Max(0, entryRemaining - Time.deltaTime); entryFade.color = new Color(0, 0, 0, entryRemaining / .24f); if (entryRemaining == 0) entryFade.gameObject.SetActive(false); }
            var s = owner.Engine.State; if (s == null) return;
            if (s.pending != null || s.phase == BattlePhase.RoundCasting)
            {
                float t = Time.time * 14;
                for (int i = 0; i < coins.Count; i++) { var rt = coins[i].rectTransform; rt.anchoredPosition = coinHomes[i] + Vector2.up * Mathf.Abs(Mathf.Sin(t + i)) * 24; rt.localScale = new Vector3(.5f + .5f * Mathf.Abs(Mathf.Cos(t + i)), 1, 1); rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t + i) * 25); SetCoinFace(i, Mathf.Max(0, (s.pending?.divination ?? s.roundDivination).revealedLines - 1)); }
            }
            else if (castLayer.gameObject.activeSelf && Time.time >= castUntil) HideCastResult();
            if ((s.phase == BattlePhase.Victory || s.phase == BattlePhase.Defeat) && !result.gameObject.activeSelf && Time.time >= resultAt) Refresh();
        }
        private static string PhaseName(BattlePhase phase) => phase == BattlePhase.Player ? "玩家回合" : phase == BattlePhase.Casting || phase == BattlePhase.RoundCasting ? "定卦中" : phase == BattlePhase.Enemy ? "敌方回合" : phase == BattlePhase.Victory ? "胜利" : "失败";
        private RectTransform PanelAt(Transform parent, string name, float x, float y, float w, float h, Color color, bool raycast = false)
        { var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.layer = 5; go.transform.SetParent(parent, false); var rt = (RectTransform)go.transform; Position(rt, x, y, w, h); var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = raycast; return rt; }
        private Image Picture(Transform parent, string name, float x, float y, float w, float h, Sprite sprite, Color fallback, bool preserve = true)
        { var rt = PanelAt(parent, name, x, y, w, h, sprite != null ? Color.white : fallback); var image = rt.GetComponent<Image>(); image.sprite = sprite; image.preserveAspect = preserve; return image; }
        private static void Border(RectTransform panel) { var outline = panel.gameObject.AddComponent<Outline>(); outline.effectColor = new Color(.55f, .47f, .31f); outline.effectDistance = new Vector2(1, -1); }
        private Text Label(Transform parent, float x, float y, float w, float h, string value, int size, Color color)
        { var go = new GameObject("文字", typeof(RectTransform), typeof(Text)); go.layer = 5; go.transform.SetParent(parent, false); Position((RectTransform)go.transform, x, y, w, h); var text = go.GetComponent<Text>(); text.font = font; text.text = value; text.fontSize = size; text.color = color; text.alignment = TextAnchor.UpperLeft; text.raycastTarget = false; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate; return text; }
        private Button ButtonAt(Transform parent, float x, float y, float w, float h, string label, Action action, out Text text)
        { var rt = PanelAt(parent, label, x, y, w, h, Panel, true); Border(rt); var b = rt.gameObject.AddComponent<Button>(); var c = b.colors; c.highlightedColor = new Color(1.25f, 1.25f, 1.15f); c.disabledColor = new Color(.48f, .48f, .48f); b.colors = c; text = Label(rt, 12, 3, w - 24, h - 6, label, 22, Color.white); text.alignment = TextAnchor.MiddleCenter; b.onClick.AddListener(() => action()); return b; }
        private static void Position(RectTransform rt, float x, float y, float w, float h)
        { rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1); rt.anchoredPosition = new Vector2(x, -y); rt.sizeDelta = new Vector2(w, h); }
    }
}

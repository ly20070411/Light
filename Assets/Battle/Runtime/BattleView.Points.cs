using System;
using System.Collections.Generic;
using System.Linq;
using Emerge.Checks;
using Emerge.GameFlow;
using Emerge.Props;
using UnityEngine;
using UnityEngine.UI;

namespace Emerge.Battle
{
    // Both rule sets share the same presentation, layout, hover panels and coin animation.
    public sealed partial class BattleView
    {
        private PointBattleController pointOwner;
        private readonly Dictionary<CheckBehavior, Button> pointActions = new Dictionary<CheckBehavior, Button>();
        private readonly Dictionary<CheckBehavior, Text> pointValues = new Dictionary<CheckBehavior, Text>();
        private int pointCastRound;
        private ScrollRect pointEnemyScroll;
        private Text pointAnchorLabel;
        public bool UsesPointRules => pointOwner != null;
        public int RenderedEnemyCount => enemies.Count;
        public Button ActionButton(CheckBehavior kin) => pointActions.TryGetValue(kin, out var b) ? b : null;
        public Button EndTurnButton => end;
        public Button QuickCastButton => quick;
        public Button PageButton(BattlePage page) => (int)page >= 0 && (int)page < tabs.Count ? tabs[(int)page].GetComponent<Button>() : null;
        public string ActionPointsContent => hero?.mp?.text ?? "";
        public bool TargetHighlighted(int index) => index >= 0 && index < enemies.Count && enemies[index].outline.enabled;
        public bool HasEnemyScroll => pointEnemyScroll != null;
        private PointBattleSession Points => pointOwner?.Engine?.State;
        private static string Number(double value) => value.ToString("0.##");
        private static string ActionName(CheckBehavior kin)
        {
            switch (kin)
            {
                case CheckBehavior.Wealth: return "直接攻击";
                case CheckBehavior.Officer: return "构筑护盾";
                case CheckBehavior.Offspring: return "施加易伤";
                case CheckBehavior.Sibling: return "削弱攻击";
                case CheckBehavior.Parent: return "提升伤害";
                default: return "回合行动点";
            }
        }
        public void Build(PointBattleController controller)
        {
            pointOwner = controller;
            Build(controller.GetComponent<BattleController>() ?? controller.gameObject.AddComponent<BattleController>());
        }
        private void BuildPointCommands(RectTransform panel)
        {
            var order = new[] { CheckBehavior.Wealth, CheckBehavior.Officer, CheckBehavior.Offspring, CheckBehavior.Sibling, CheckBehavior.Parent };
            for (int i = 0; i < order.Length; i++)
            {
                var kin = order[i]; float x = 16 + i * 306;
                var header = Label(pages[0], x, 0, 290, 28, SixKinAttributes.Get(kin).name, 23, Gold);
                header.alignment = TextAnchor.MiddleCenter;
                var b = ButtonAt(pages[0], x + 4, 39, 282, 46, ActionName(kin), () => pointOwner.Act(kin, target, out _), out _);
                pointActions.Add(kin, b);
                var hover = b.gameObject.AddComponent<BattleHoverTarget>(); hover.view = this; hover.pointAction = true; hover.action = kin;
                var value = Label(pages[0], x, 95, 290, 70, "", 18, new Color(.72f, .8f, .85f));
                value.alignment = TextAnchor.UpperCenter; pointValues.Add(kin, value);
            }
            var anchor = pointAnchorLabel = Label(panel, 944, 8, 600, 38, MentalAnchors.Name(Points.anchor), 20, Gold);
            anchor.alignment = TextAnchor.MiddleRight; anchor.raycastTarget = true;
            var anchorHover = anchor.gameObject.AddComponent<BattleHoverTarget>(); anchorHover.view = this; anchorHover.rules = true;
            var usableItems = owner.catalog.items.Where(d => d != null && d.effect != BattleItemEffect.Mana && d.pointPower > 0).ToArray();
            // Scroll when a catalog has more than four items; the four page layout stays fixed.
            var itemViewport = PanelAt(pages[1], "背包视口", 0, 0, 1544, 175, Color.clear, true);
            itemViewport.gameObject.AddComponent<RectMask2D>();
            var content = PanelAt(itemViewport, "背包内容", 0, 0, Math.Max(1544, usableItems.Length * 379 + 18), 175, Color.clear);
            var scroll = itemViewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = itemViewport; scroll.content = content;
            scroll.horizontal = true; scroll.vertical = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 60;
            for (int i = 0; i < usableItems.Length; i++)
            {
                var d = usableItems[i]; string id = d.id;
                var b = ButtonAt(content, 18 + i * 379, 22, 360, 117, "", () => pointOwner.UseItem(d, out _), out var label);
                Position(label.rectTransform, 98, 12, 244, 92); label.fontSize = 19; label.alignment = TextAnchor.MiddleLeft;
                Picture(b.transform, "物品图标", 18, 26, 64, 64, art?.itemIcon, Gold);
                items.Add(id, b); itemLabels.Add(id, label);
                var hover = b.gameObject.AddComponent<BattleHoverTarget>(); hover.view = this; hover.itemId = id;
            }
            var negotiation = Label(pages[2], 160, 20, 1224, 128, "交涉\n预留剧情入口", 24, new Color(.62f, .7f, .77f));
            negotiation.alignment = TextAnchor.MiddleCenter;
            fleeText = Label(pages[3], 210, 4, 1120, 62, "", 21, Color.white); fleeText.alignment = TextAnchor.MiddleCenter;
            flee = ButtonAt(pages[3], 622, 80, 300, 52, "逃跑", () => pointOwner.Flee(), out _);
            hint = Label(panel, 24, 242, 1160, 28, "", 18, Gold);
            end = ButtonAt(panel, 1280, 239, 264, 34, "结束回合 →", () => pointOwner.EndTurn(), out _);
            end.GetComponent<Image>().color = new Color(.32f, .29f, .15f);
        }
        private void BuildPointEnemyScroll()
        {
            var viewport = PanelAt(root, "敌方队列视口", 940, 130, 640, 478, Color.clear, true);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = PanelAt(viewport, "敌方队列", 0, 0, enemies.Count * 200 + 20, 478, Color.clear);
            pointEnemyScroll = viewport.gameObject.AddComponent<ScrollRect>(); pointEnemyScroll.viewport = viewport; pointEnemyScroll.content = content;
            pointEnemyScroll.horizontal = true; pointEnemyScroll.vertical = false; pointEnemyScroll.inertia = false;
            pointEnemyScroll.movementType = ScrollRect.MovementType.Clamped; pointEnemyScroll.scrollSensitivity = 75;
            for (int i = 0; i < enemies.Count; i++)
            { enemies[i].root.SetParent(content, false); Position(enemies[i].root, 10 + i * 200, 40, 185, 436); }
            var track = PanelAt(viewport, "队列滚动条", 10, 469, 620, 7, new Color(.02f, .04f, .07f), true);
            var handle = PanelAt(track, "滑块", 0, 0, 620, 7, Gold, true);
            handle.sizeDelta = handle.anchoredPosition = Vector2.zero;
            var bar = track.gameObject.AddComponent<Scrollbar>(); bar.handleRect = handle; bar.targetGraphic = handle.GetComponent<Image>();
            bar.direction = Scrollbar.Direction.LeftToRight; pointEnemyScroll.horizontalScrollbar = bar;
            Label(root, 960, 100, 600, 25, "左右滚动查看全部敌人", 17, Gold).alignment = TextAnchor.MiddleCenter;
        }
        private void SelectPointTarget(int index)
        {
            var s = Points;
            if (s?.phase != PointBattlePhase.Player || index < 0 || index >= s.enemies.Count || !s.enemies[index].Alive) return;
            target = index; HideTooltip(); Refresh();
        }
        private string ItemEffect(BattleItemDefinition d) => d.effect == BattleItemEffect.Heal ? "回复 " + d.pointPower + " 点生命" :
            d.effect == BattleItemEffect.DamageAll ? "对全体敌人造成 " + d.pointPower + " 点伤害" : "清除自身流蚀、易伤和削弱";
        private bool CanUsePointItem(BattleItemDefinition d) => Points?.phase == PointBattlePhase.Player && Points.ap >= Points.actionCost &&
            d.effect != BattleItemEffect.Mana && d.pointPower > 0 && pointOwner.GetComponent<PropGameState>().Count(d.inventoryKey) > 0;
        private void RefreshPoints()
        {
            var s = Points; if (s == null || root == null) return;
            if (sessionId != s.sessionId)
            {
                sessionId = s.sessionId; target = 0; shownCast = null; pointCastRound = 0; castUntil = resultAt = 0;
                entryRemaining = .24f; entryFade.gameObject.SetActive(true); SwitchPage(BattlePage.Skills);
            }
            if (target < 0 || target >= s.enemies.Count || !s.enemies[target].Alive) target = Math.Max(0, s.enemies.FindIndex(e => e.Alive));
            title.text = s.name + " · 第 " + s.round + " 轮 · " + PointPhaseName(s.phase);
            pointAnchorLabel.text = MentalAnchors.Name(s.anchor);
            hero.hp.text = "HP " + Number(s.hp) + " / " + Number(s.maximumHP);
            hero.hpFill.sizeDelta = new Vector2((hero.width - 4) * Mathf.Clamp01((float)(s.hp / s.maximumHP)), 17);
            hero.mp.text = "AP " + s.ap + " · 行动 " + s.actionCost;
            int self = s.actions.Count == 6 && s.actions[5].available ? s.actions[5].calculation.finalPoints : 0;
            hero.mpFill.sizeDelta = new Vector2((hero.width - 4) * Mathf.Clamp01((float)s.ap / Math.Max(1, Math.Max(s.ap, s.roundAP + self + s.carryLimit))), 17);
            hero.status.text = PointHeroStatuses(s);
            for (int i = 0; i < enemies.Count; i++)
            {
                var f = enemies[i]; var e = s.enemies[i];
                if (pointEnemyScroll == null)
                {
                    float center = s.enemies.Count == 1 ? 1260 : s.enemies.Count == 2 ? 1100 + i * 260 : 1042 + i * 200;
                    f.root.anchoredPosition = new Vector2(center - f.width / 2, -170);
                }
                var original = owner.catalog.Enemy(e.profile.id);
                var sprite = e.profile.portrait != null ? e.profile.portrait : original != null && original.battlePortrait != null ? original.battlePortrait : art?.defaultEnemyPortrait;
                f.portrait.sprite = sprite; f.portrait.color = sprite != null ? Color.white : e.profile.color;
                f.portrait.canvasRenderer.SetAlpha(e.Alive ? 1 : .25f); f.name.text = e.profile.name;
                f.hp.text = "HP " + Number(e.hp) + " / " + Number(e.profile.health);
                f.hpFill.sizeDelta = new Vector2((f.width - 4) * Mathf.Clamp01((float)(e.hp / e.profile.health)), 17);
                f.mp.text = "护盾 " + Number(e.shield);
                f.mpFill.sizeDelta = new Vector2((f.width - 4) * Mathf.Clamp01((float)(e.shield / e.profile.health)), 17);
                f.status.text = !e.Alive ? "已倒下" : e.Intent.name + "\n" + PointEnemyStatuses(e);
                f.outline.enabled = i == target && e.Alive; f.marker.gameObject.SetActive(f.outline.enabled);
                f.button.interactable = s.phase == PointBattlePhase.Player && e.Alive;
            }
            foreach (var pair in pointActions)
            {
                pair.Value.interactable = pointOwner.Engine.CanAct(pair.Key, target, out _);
                var a = s.actions.Count == 6 ? s.actions[(int)pair.Key] : null;
                pointValues[pair.Key].text = a == null ? "定卦后显示点数" : !a.available ? "本轮不可用\n检定 " + a.rawCheckPoints.ToString("+0;-0;0") :
                    "最终点数 " + a.calculation.finalPoints + " · " + s.actionCost + " AP\n" + a.calculation.action.description;
            }
            var inventory = pointOwner.GetComponent<PropGameState>();
            foreach (var pair in items)
            {
                var d = owner.catalog.Item(pair.Key); pair.Value.interactable = CanUsePointItem(d);
                itemLabels[pair.Key].text = d.displayName + " ×" + inventory.Count(d.inventoryKey) + "\n" + ItemEffect(d) + "\n消耗 " + s.actionCost + " AP";
            }
            roundInfo.text = s.actions.Count == 6 ? "本轮 · " + s.divination.chart.benGuaName + "\n\n" +
                string.Join("\n", s.actions.Select(a => SixKinAttributes.Get(a.calculation.attribute).name + "  检定 " + a.rawCheckPoints.ToString("+0;-0;0") + " → " +
                    (a.available ? "最终 " + a.calculation.finalPoints : "不可用"))) + "\n\n我：自动 +" + self + " AP" : "正在投掷铜币…\n检定后获得本轮行动点";
            lastActionText.text = string.Join("\n", s.log.Skip(Math.Max(0, s.log.Count - 3)));
            end.interactable = s.phase == PointBattlePhase.Player; flee.interactable = pointOwner.CanFlee;
            fleeText.text = pointOwner.CanFlee ? "离开战斗，恢复进入前的位置、背包、装备与剧情状态。" : "当前阶段不能逃跑。";
            double forecast = s.enemies.Select((e, i) => e.Alive ? pointOwner.Engine.PreviewEnemyAttack(i) * e.Intent.hits : 0).Sum();
            hint.text = s.phase == PointBattlePhase.Player ? "剩余 AP " + s.ap + " · 每次行动 " + s.actionCost + " AP · 预告伤害 " + Number(forecast) + " · 结束回合最多保留 " + s.carryLimit + " AP" :
                s.phase == PointBattlePhase.Casting ? "每轮定卦 · 基础 + 成长 + 检定 → 按装备顺序修正 → 最终点数" : s.phase == PointBattlePhase.Enemy ? "敌方依次行动…" : "本场战斗已结束";
            if (s.phase != PointBattlePhase.Player) HideTooltip();
            if (s.phase == PointBattlePhase.Casting) { RenderPointCast(false); castLayer.gameObject.SetActive(true); }
            else if (s.actions.Count == 6 && pointCastRound != s.round && s.phase == PointBattlePhase.Player)
            { pointCastRound = s.round; castUntil = Time.time + .65f; RenderPointCast(true); castLayer.gameObject.SetActive(true); }
            else if (Time.time >= castUntil) castLayer.gameObject.SetActive(false);
            bool finished = (s.phase == PointBattlePhase.Victory || s.phase == PointBattlePhase.Defeat) && Time.time >= resultAt;
            result.gameObject.SetActive(finished);
            if (finished)
            {
                var record = inventory.Victory(s.encounterId, s.balanceVersion);
                resultText.text = (s.phase == PointBattlePhase.Victory ? "战斗胜利" : "战斗失败") + "\n\n本次 " + s.round + " 轮" +
                    (s.phase == PointBattlePhase.Victory && record != null ? " · 最佳 " + record.bestRounds + " 轮\n累计胜利 " + record.wins + " 次" : "") + "\n返回场景后可重新调整装备与精神锚";
            }
        }
        private static string PointPhaseName(PointBattlePhase phase) => phase == PointBattlePhase.Player ? "玩家回合" : phase == PointBattlePhase.Casting ? "定卦中" : phase == PointBattlePhase.Enemy ? "敌方回合" : phase == PointBattlePhase.Victory ? "胜利" : "失败";
        private static string PointHeroStatuses(PointBattleSession s) => "护盾 " + Number(s.shield) + (s.damageBonus > 0 ? "  增伤 " + s.damageBonus.ToString("P0") : "") +
            (s.corrosion > 0 ? "\n流蚀 " + s.corrosion : "") + (s.vulnerability > 0 ? "  易伤 " + s.vulnerability.ToString("P0") : "") + (s.weakness > 0 ? "  削弱 " + Number(s.weakness) : "");
        private static string PointEnemyStatuses(PointEnemyState e) => (e.vulnerability > 0 ? "易伤 " + e.vulnerability.ToString("P0") + "  " : "") +
            (e.attackReduction > 0 ? "攻击 −" + Number(e.attackReduction) + "  " : "") + (e.corrosion > 0 ? "流蚀 " + e.corrosion + "  " : "") + (e.strength > 0 ? "力量 +" + Number(e.strength) : "");
        private void RenderPointCast(bool resolved)
        {
            var s = Points; var r = s.divination; shownCast = new BattleAction { divination = r };
            castTitle.text = "第 " + s.round + " 轮 · 定卦";
            var lines = new List<string>();
            for (int i = 5; i >= 0; i--)
            {
                if (i >= r.revealedLines) lines.Add("···    未揭示");
                else if (resolved) { var y = r.chart.yaos[i]; lines.Add(y.benSymbol + "  " + y.yaowei + " " + y.benLiuqin + " " + y.wangshuaiScore.ToString("+0;-0;0")); }
                else { int value = r.casting.yaoValues[i]; lines.Add((value % 2 == 1 ? "━━━━━" : "━━  ━━") + "   " + (i + 1) + " 爻 · " + value + (value == 6 || value == 9 ? " 动" : "")); }
            }
            castLines.text = string.Join("\n", lines);
            castResult.text = resolved ? r.chart.benGuaName + " → " + r.chart.bianGuaName + "\n本轮 AP " + s.ap + " · 点数已确定" : "已揭示 " + r.revealedLines + " / 6";
            quickText.text = resolved ? "开始行动" : "快速定卦";
            if (resolved) for (int i = 0; i < coins.Count; i++)
            { coins[i].rectTransform.anchoredPosition = coinHomes[i]; coins[i].transform.localScale = Vector3.one; coins[i].transform.localRotation = Quaternion.identity; SetCoinFace(i, 5); }
        }
        private void UpdatePoints()
        {
            var s = Points; if (s == null) return;
            if (s.phase == PointBattlePhase.Casting)
            {
                for (int i = 0; i < coins.Count; i++)
                {
                    float t = Time.time * 14 + i; var rt = coins[i].rectTransform;
                    rt.anchoredPosition = coinHomes[i] + Vector2.up * Mathf.Abs(Mathf.Sin(t)) * 24;
                    rt.localScale = new Vector3(.5f + .5f * Mathf.Abs(Mathf.Cos(t)), 1, 1); rt.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t) * 25);
                    SetCoinFace(i, Mathf.Max(0, s.divination.revealedLines - 1));
                }
            }
            else if (castLayer.gameObject.activeSelf && Time.time >= castUntil) HideCastResult();
            if ((s.phase == PointBattlePhase.Victory || s.phase == PointBattlePhase.Defeat) && !result.gameObject.activeSelf && Time.time >= resultAt) Refresh();
        }
        public void ShowPointActionTooltip(CheckBehavior kin, Vector2 pointer)
        {
            var s = Points; if (s?.phase != PointBattlePhase.Player || !GameSessionController.SessionInputAllowed || (int)kin < 0 || (int)kin >= 5) return;
            var a = s.actions[(int)kin]; var c = a.calculation;
            string content = SixKinAttributes.Get(kin).name + " · " + ActionName(kin) + "\n消耗 " + s.actionCost + " AP\n" + c.Describe();
            if (a.rawCheckPoints != a.effectiveCheckPoints) content += "\n精神锚：检定 " + a.rawCheckPoints + " → " + a.effectiveCheckPoints;
            if (c.actionAddons.Count > 0) content += "\n" + string.Join("\n", c.actionAddons.Select(e => "道具附加：" + e.description + "（" + e.amount + "）"));
            if (kin == CheckBehavior.Wealth && target < s.enemies.Count)
            {
                double damage = Math.Max(0, c.finalPoints * (1 + s.damageBonus) * (1 + s.enemies[target].vulnerability) - s.weakness);
                content += "\n当前直接伤害 " + Number(damage) + "（目标护盾优先吸收）";
            }
            if (!pointOwner.Engine.CanAct(kin, target, out var reason)) content += "\n" + reason;
            string status = kin == CheckBehavior.Offspring ? "易伤\n受到的伤害乘以（1 + 易伤比例），可叠加。每点 x 增加 12.5%，敌方回合结束清零。" :
                kin == CheckBehavior.Parent ? "增伤\n自身伤害乘以（1 + 增伤比例），可叠加。每点 x 增加 10%，敌方回合结束清零。" :
                kin == CheckBehavior.Sibling ? "攻击削减\n目标每次攻击的点数减少 x × 1.25，最低为 0，敌方回合结束清零。" :
                kin == CheckBehavior.Officer ? "护盾\n优先吸收伤害，可叠加，下次玩家回合开始清零。" : "";
            if (kin == CheckBehavior.Sibling && s.anchor == MentalAnchors.LinXi) status += "\n流蚀\n本次额外施加 " + c.finalPoints / 2 + " 层；可叠加。回合开始损失等于层数的生命，再减少一层。兄弟每轮最多两次。";
            if (c.actionAddons.Any(e => e.effectKey == "corrosion") && !(kin == CheckBehavior.Sibling && s.anchor == MentalAnchors.LinXi)) status += "\n流蚀\n回合开始损失等于层数的生命，再减少一层；层数可叠加。";
            ShowTooltip(content, status.Trim(), pointer);
        }
        private void ShowPointItemTooltip(string id, Vector2 pointer)
        {
            if (Points?.phase != PointBattlePhase.Player || !GameSessionController.SessionInputAllowed) return;
            var d = owner.catalog.Item(id); if (d == null) return;
            ShowTooltip(d.displayName + "\n" + ItemEffect(d) + "\n消耗 " + Points.actionCost + " AP；使用后消耗一个。" +
                (CanUsePointItem(d) ? "" : "\n物品数量或行动点不足。"), d.effect == BattleItemEffect.DamageAll ? "伤害\n受自身增伤、目标易伤与自身削弱影响；目标护盾优先吸收。" : "", pointer);
        }
        private void ShowPointEnemyTooltip(int index, Vector2 pointer)
        {
            var s = Points; if (s == null || index < 0 || index >= s.enemies.Count || !GameSessionController.SessionInputAllowed) return;
            var e = s.enemies[index]; var move = e.Intent;
            string effect = move.effect == PointEnemyEffect.Attack ? "预计造成 " + Number(pointOwner.Engine.PreviewEnemyAttack(index)) + " × " + move.hits + " 伤害（护盾前）" :
                move.effect == PointEnemyEffect.Shield ? "获得 " + Number(move.amount) + " 护盾" : move.effect == PointEnemyEffect.Heal ? "回复 " + Number(move.amount) + " 生命" :
                move.effect == PointEnemyEffect.Strength ? "自身力量 +" + Number(move.amount) : move.effect == PointEnemyEffect.Corrosion ? "给主角施加 " + Number(move.amount) + " 层流蚀" :
                move.effect == PointEnemyEffect.Vulnerability ? "给主角施加 " + Number(move.amount) + "% 易伤" : "降低主角伤害 " + Number(move.amount) + " 点";
            ShowTooltip(e.profile.name + "\n" + (e.Alive ? "意图 · " + move.name + "\n" + effect : "已倒下") + "\n" + PointEnemyStatuses(e),
                "敌方行动\n按预告依次执行，不受定卦改变。\n易伤、增伤和攻击削减在敌方回合结束清零。流蚀在回合开始扣血后减一层。", pointer);
        }
        private void ShowPointRulesTooltip(Vector2 pointer)
        {
            if (Points == null || !GameSessionController.SessionInputAllowed) return;
            ShowTooltip("点数战斗\n基础 + 成长 + 检定 → 装备依顺序修正 → 最终点数 x（最低 0）。\n每轮获得 3 + 我最终点数 AP；每次行动或使用物品消耗 2 AP。\n可重复行动，主动结束回合，剩余 AP 最多保留 1。",
                MentalAnchors.Name(Points.anchor) + "\n" + MentalAnchors.Description(Points.anchor), pointer);
        }
        private void ShowPointHeroTooltip(Vector2 pointer)
        {
            if (Points == null || !GameSessionController.SessionInputAllowed) return;
            ShowTooltip("主角状态\n" + PointHeroStatuses(Points) + "\n剩余 AP " + Points.ap, MentalAnchors.Name(Points.anchor) + "\n" + MentalAnchors.Description(Points.anchor), pointer);
        }
        public void PlayPointAction(CheckBehavior kin, int index)
        {
            HideTooltip(); castUntil = 0; castLayer.gameObject.SetActive(false); resultAt = Time.time + .55f; result.gameObject.SetActive(false);
            bool attack = kin == CheckBehavior.Wealth || kin == CheckBehavior.Offspring || kin == CheckBehavior.Sibling;
            hero.motion.Play(attack ? 35 : 0, attack ? null : art?.shieldEffect, Gold);
            if (attack && index >= 0 && index < enemies.Count) enemies[index].motion.Play(10, art?.attackEffect, new Color(1, .4f, .35f), true);
        }
        public void PlayPointEnemyAction(int index)
        {
            if (Points == null || index < 0 || index >= enemies.Count || !Points.enemies[index].Alive) return;
            var move = Points.enemies[index].Intent; bool attack = move.effect == PointEnemyEffect.Attack;
            enemies[index].motion.Play(attack ? -27 : 0, attack ? null : move.effect == PointEnemyEffect.Heal ? art?.healingEffect : art?.shieldEffect, Gold);
            if (attack) hero.motion.Play(-8, art?.attackEffect, new Color(1, .4f, .35f), true);
        }
        public void PlayPointItem(BattleItemDefinition item)
        {
            if (item.effect == BattleItemEffect.DamageAll)
            { for (int i = 0; i < enemies.Count; i++) if (Points.enemies[i].Alive) enemies[i].motion.Play(10, art?.attackEffect, new Color(1, .4f, .35f), true); }
            else hero.motion.Play(0, art?.healingEffect, new Color(.4f, 1, .75f));
        }
    }
}

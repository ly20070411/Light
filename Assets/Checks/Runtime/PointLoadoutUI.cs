using System;
using System.Linq;
using System.Text;
using Emerge.Checks.Divination;
using Emerge.GameFlow;
using UnityEngine;

namespace Emerge.Checks
{
    [DisallowMultipleComponent, RequireComponent(typeof(CheckActorState))]
    public sealed partial class PointLoadoutUI : MonoBehaviour
    {
        public static PointLoadoutUI Open { get; private set; }
        public static bool AnyOpen => Open != null;
        private CheckActorState actor;
        private CheckBehavior selected = CheckBehavior.Officer;
        private PixelPrototype.PlayerMovement movement;
        private bool restoreMovement;
        private Vector2 slotsScroll, inventoryScroll, resultScroll;
        private string message;
        private DivinationRecord divination;
        private Font font;
        private GUISkin pointSkin;
        public CheckBehavior SelectedAttribute => selected;
        public void SelectAttribute(CheckBehavior attribute)
        {
            if (!Enum.IsDefined(typeof(CheckBehavior), attribute)) throw new ArgumentOutOfRangeException(nameof(attribute));
            selected = attribute; resultScroll = Vector2.zero;
        }
        private void Awake() { actor = GetComponent<CheckActorState>(); }
        private void Update()
        {
            bool allowed = GameSessionController.SessionInputAllowed && !Emerge.Battle.BattleController.AnyBattleActive &&
                actor.AttributeRulesVersion >= 3 && GetComponent<Emerge.Props.PlayerInteractor>()?.IsInDialogue != true;
            if (!allowed && Open == this) Close();
            if (allowed && Input.GetKeyDown(KeyCode.P)) { if (Open == this) Close(); else Show(); }
            if (Open == this && Input.GetKeyDown(KeyCode.Escape)) Close();
        }
        public void Show()
        {
            if (AnyOpen || actor.AttributeRulesVersion < 3 || !GameSessionController.SessionInputAllowed ||
                Emerge.Battle.BattleController.AnyBattleActive || GetComponent<Emerge.Props.PlayerInteractor>()?.IsInDialogue == true) return;
            Open = this; movement = GetComponent<PixelPrototype.PlayerMovement>();
            restoreMovement = movement != null && movement.enabled;
            if (restoreMovement) { if (movement.Body != null) movement.Body.velocity = Vector2.zero; movement.enabled = false; }
        }
        public void Close()
        {
            if (Open != this) return;
            Open = null;
            if (restoreMovement && movement != null) movement.enabled = true;
            restoreMovement = false;
        }
        private void OnDisable() => Close();
        private void OnDestroy()
        {
            if (pointSkin != null) Destroy(pointSkin);
            if (font != null) Destroy(font);
        }
        private void CastPreview()
        {
            var casting = CoinCasting.Cast(BitConverter.ToInt32(Guid.NewGuid().ToByteArray(), 0));
            // Same calendar defaults and same documented scorer as ordinary divination checks.
            divination = new DivinationRecord { month = "巳月", day = "戊子日", casting = casting, revealedLines = 6 };
            var chart = new LiuYaoPaiPan().PaiPan(divination.month, divination.day, casting.yaoValues);
            divination.month = chart.yueling; divination.day = chart.richen; divination.chart = chart;
            message = "六爻点数已生成；这是试算，不会执行战斗行动。";
        }
        private void Label(string text, float width = 480)
        {
            // IMGUI's default word wrapping treats long CJK phrases as single words.
            // Explicit line breaks and height keep Chinese descriptions and the final result visible.
            var wrapped = new StringBuilder(); float lineWidth = 0; int lines = 1;
            foreach (char character in text ?? "")
            {
                if (character == '\n') { wrapped.Append(character); lineWidth = 0; lines++; continue; }
                float advance = character < 128 ? 10 : 19;
                if (lineWidth + advance > width) { wrapped.Append('\n'); lineWidth = 0; lines++; }
                wrapped.Append(character); lineWidth += advance;
            }
            GUILayout.Label(wrapped.ToString(), GUILayout.Height(lines * 28 + 12));
        }
        private void OnGUI()
        {
            if (actor == null || actor.AttributeRulesVersion < 3 || !GameSessionController.SessionInputAllowed || Emerge.Battle.BattleController.AnyBattleActive) return;
            if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Arial" }, 18);
            if (pointSkin == null)
            {
                pointSkin = Instantiate(GUI.skin); pointSkin.hideFlags = HideFlags.HideAndDontSave; pointSkin.font = font;
                pointSkin.label.fontSize = 18; pointSkin.label.wordWrap = false; pointSkin.label.fixedHeight = 0;
                pointSkin.label.padding = new RectOffset(4, 4, 5, 7); pointSkin.label.normal.textColor = Color.white;
                pointSkin.button.fontSize = 18; pointSkin.button.wordWrap = true;
                pointSkin.button.padding = new RectOffset(8, 8, 6, 6);
                pointSkin.box.fontSize = 18;
            }
            var oldSkin = GUI.skin; GUI.skin = pointSkin;
            var oldMatrix = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(new Vector3((Screen.width - 1280 * scale) / 2, (Screen.height - 720 * scale) / 2), Quaternion.identity, Vector3.one * scale);
            try
            {
                if (Open != this)
                {
                    if (!AnyOpen && GetComponent<Emerge.Props.PlayerInteractor>()?.IsInDialogue != true && GUI.Button(new Rect(16, 86, 200, 28), "P · 六亲成长道具")) Show();
                    return;
                }
                var oldColor = GUI.color; GUI.color = new Color(.055f, .09f, .14f, 1);
                GUI.DrawTexture(new Rect(100, 45, 1080, 630), Texture2D.whiteTexture); GUI.color = oldColor;
                GUI.Box(new Rect(100, 45, 1080, 630), "");
                GUI.Label(new Rect(120, 56, 340, 30), "六亲点数 · 道具与精神锚");
                if (GUI.Button(new Rect(1080, 56, 82, 28), "关闭 [P]")) Close();
                if (GUI.Button(new Rect(470, 55, 150, 30), "成长道具")) loadoutPage = 0;
                if (GUI.Button(new Rect(630, 55, 150, 30), "精神锚")) loadoutPage = 1;
                if (GUI.Button(new Rect(790, 55, 150, 30), "唐晦改装")) loadoutPage = 2;
                if (loadoutPage != 0) { DrawProgressionPage(); return; }
                for (int i = 0; i < 6; i++)
                {
                    var kin = (CheckBehavior)i;
                    bool oldEnabled = GUI.enabled; GUI.enabled = kin != selected;
                    if (GUI.Button(new Rect(120 + i * 172, 94, 160, 34), SixKinAttributes.Get(kin).name)) SelectAttribute(kin);
                    GUI.enabled = oldEnabled;
                }
                GUILayout.BeginArea(new Rect(120, 142, 500, 480));
                Label(SixKinAttributes.Get(selected).name + "：基础 " + actor.attributes.Get(selected) + " + 成长 " + actor.GrowthPoints(selected) + " = 局外 " + actor.OutsidePoints(selected));
                Label("配置顺序（每件 +1 成长；上下调整执行顺序）");
                slotsScroll = GUILayout.BeginScrollView(slotsScroll, GUILayout.Height(220));
                var slots = actor.PointEquipment.Where(item => item.attribute == selected).ToArray();
                if (slots.Length == 0) Label("尚未配置成长道具");
                for (int i = 0; i < slots.Length; i++)
                {
                    var slot = slots[i]; var definition = actor.PointItems != null ? actor.PointItems.Find(slot.itemKey) : null;
                    var data = slot.instanceData ?? definition?.data;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label((i + 1) + ". " + (data != null ? data.displayName : slot.itemKey), GUILayout.Width(240));
                    GUI.enabled = actor.CanConfigureLoadout && i > 0;
                    if (GUILayout.Button("↑", GUILayout.Width(38))) actor.TryMovePointItem(slot.instanceId, selected, i - 1, out message);
                    GUI.enabled = actor.CanConfigureLoadout && i < slots.Length - 1;
                    if (GUILayout.Button("↓", GUILayout.Width(38))) actor.TryMovePointItem(slot.instanceId, selected, i + 1, out message);
                    GUI.enabled = actor.CanConfigureLoadout;
                    if (GUILayout.Button("卸下", GUILayout.Width(64))) actor.TryUnequipPointItem(slot.instanceId, out message);
                    GUILayout.EndHorizontal();
                    GUI.enabled = true;
                    if (data != null) Label(data.description, 450);
                }
                GUILayout.EndScrollView();
                Label("背包中的成长道具（配置占用一份，卸下返还）");
                inventoryScroll = GUILayout.BeginScrollView(inventoryScroll);
                bool found = false;
                foreach (var item in actor.PointBag)
                {
                    found = true; GUILayout.BeginHorizontal(); GUILayout.Label(item.item.displayName);
                    GUI.enabled = actor.CanConfigureLoadout;
                    if (GUILayout.Button("配置到" + SixKinAttributes.Get(selected).name, GUILayout.Width(130))) actor.TryEquipPointInstance(item.instanceId, selected, out message);
                    GUI.enabled = true; GUILayout.EndHorizontal();
                    Label(item.item.description, 450);
                }
                if (!found) Label("暂无成长道具；在剧情中获得后可在此配置。");
                GUILayout.EndScrollView(); GUILayout.EndArea();
                GUILayout.BeginArea(new Rect(650, 142, 510, 480));
                Label("局内点数试算（不执行战斗行动）");
                if (GUILayout.Button("投掷六爻 · 生成检定点数", GUILayout.Height(36)))
                { try { CastPreview(); } catch (Exception exception) { message = exception.Message; } }
                resultScroll = GUILayout.BeginScrollView(resultScroll);
                if (divination == null) Label("当前预览按检定 0 点计算。投掷后使用真实排盘正负得分。");
                else
                {
                    Label(divination.chart.benGuaName + " → " + divination.chart.bianGuaName);
                    Label(string.Join(" / ", Enumerable.Range(0, 6).Select(i => SixKinAttributes.Get((CheckBehavior)i).name + " " + divination.chart.behaviorModifiers[i].ToString("+0;-0;0"))));
                }
                try
                {
                    int check = divination == null ? 0 : divination.chart.behaviorModifiers[(int)selected];
                    var result = Emerge.Battle.PointBattleEngine.Calculate(actor.attributes, selected, check, actor.FreezePointEquipment());
                    Label(result.Describe());
                    foreach (var addon in result.actionAddons) Label("附加效果：" + addon.description + "（数值 " + addon.amount + "）");
                }
                catch (Exception exception) { Label("无法计算：" + exception.Message); }
                GUILayout.EndScrollView(); GUILayout.EndArea();
                GUI.Label(new Rect(120, 635, 1030, 30), string.IsNullOrEmpty(message) ? (actor.CanConfigureLoadout ? actor.ConfigurationLocation + " · 可以配置道具；每件成长 +1。" : "仅在主角卧室或仓储区可修改配置；此处可查看与试算。") : message);
            }
            finally { GUI.matrix = oldMatrix; GUI.skin = oldSkin; }
        }
    }
}

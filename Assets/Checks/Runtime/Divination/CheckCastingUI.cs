using System;
using System.Collections.Generic;
using Emerge.GameFlow;
using Emerge.Props;
using Emerge.Checks.Divination;
using UnityEngine;

namespace Emerge.Checks
{
    /// <summary>Displays a persisted cast; animation never generates or changes its coin results.</summary>
    [DisallowMultipleComponent]
    public sealed class CheckCastingUI : MonoBehaviour
    {
        [Min(0.01f)] public float throwDuration = 0.9f;
        [Min(0.01f)] public float quickThrowDuration = 0.22f;
        public bool QuickCast { get; set; }
        public bool IsOpen => isOpen;
        public bool IsAnimating => isAnimating;
        public int RevealedLines => session?.divination == null ? 0 : session.divination.revealedLines;
        public CheckSession Session => session;
        public string Error => errorMessage;

        private PropInstance owner;
        private PlayerInteractor actor;
        private CheckEventDefinition definition;
        private CheckActorState state;
        private CheckSession session;
        private Action onReady;
        private bool previousUI, previousKeyboard;
        private bool isOpen, isAnimating, quickSequence, handingOff;
        private float elapsed, duration;
        private int rollingLine;
        private string errorMessage;
        private Vector2 scroll;
        private Texture2D coinTexture;

        public static bool StartCasting(PropInstance owner, PlayerInteractor actor, CheckEventDefinition definition,
            CheckActorState state, CheckSession session, Action onReady)
        {
            if (owner == null || actor == null || definition == null || state == null || session == null ||
                session.phase != CheckSessionPhase.Preparing || session.divination == null ||
                !CoinCasting.IsValid(session.divination.casting) || session.divination.revealedLines < 0 ||
                session.divination.revealedLines > 6 || !owner.isActiveAndEnabled || actor.IsInDialogue ||
                !GameSessionController.GameplayInputAllowed) return false;
            var view = actor.GetComponent<CheckCastingUI>();
            if (view == null) view = actor.gameObject.AddComponent<CheckCastingUI>();
            if (view.isOpen) return false;
            return view.Begin(owner, actor, definition, state, session, onReady);
        }

        private bool Begin(PropInstance source, PlayerInteractor player, CheckEventDefinition check,
            CheckActorState playerState, CheckSession prepared, Action ready)
        {
            owner = source; actor = player; definition = check; state = playerState; session = prepared; onReady = ready;
            previousUI = actor.showUI; previousKeyboard = actor.keyboardInput;
            handingOff = false; isAnimating = false; quickSequence = false; errorMessage = null; scroll = Vector2.zero;
            var introduction = new List<PropDialogueLine>
            {
                new PropDialogueLine { speaker = string.IsNullOrWhiteSpace(check.speaker) ? check.title : check.speaker,
                    text = "投掷三枚铜钱，依次形成六爻。" }
            };
            if (!actor.BeginDialogue(owner, introduction, _ => CloseView()))
            { enabled = false; return false; }
            isOpen = true;
            actor.showUI = false; actor.keyboardInput = false;
            enabled = true;
            state.RecordTrace(session, "起卦界面", "进入铜钱投掷，已揭示 " + RevealedLines + "/6 爻；继续使用本次固定结果。");
            if (RevealedLines == 6) PrepareChart();
            return true;
        }

        public bool ThrowNext()
        {
            if (!CanOperate() || isAnimating || session.phase != CheckSessionPhase.Preparing || RevealedLines >= 6) return false;
            errorMessage = null;
            quickSequence = QuickCast;
            state.RecordTrace(session, "投掷动画", "开始揭示第 " + (RevealedLines + 1) + " 爻" + (quickSequence ? "，快速连续投掷。" : "。"));
            BeginAnimation();
            return true;
        }

        private void BeginAnimation()
        {
            rollingLine = RevealedLines;
            elapsed = 0;
            duration = Mathf.Max(0.01f, quickSequence ? quickThrowDuration : throwDuration);
            isAnimating = true;
        }

        public bool ConfirmContinue()
        {
            if (!CanOperate() || isAnimating || RevealedLines != 6) return false;
            if (session.phase == CheckSessionPhase.Preparing && !PrepareChart()) return false;
            if (session.phase != CheckSessionPhase.Ready) return false;
            state.RecordTrace(session, "准备确认", "六爻排盘与行为加值已确认，进入行动选择。");
            handingOff = true;
            // Cancel first so PlayerInteractor releases the owner and movement lock before another dialogue begins.
            actor.CancelDialogue();
            return true;
        }

        public void CancelCasting()
        {
            if (!isOpen) return;
            handingOff = false;
            if (actor != null && actor.IsInDialogue) actor.CancelDialogue();
            else CloseView();
        }

        private bool CanOperate()
        {
            if (!isOpen || actor == null || state == null || session == null || owner == null || !owner.isActiveAndEnabled ||
                !actor.IsInDialogue) return false;
            return GameSessionController.GameplayInputAllowed;
        }

        private bool PrepareChart()
        {
            if (session.phase == CheckSessionPhase.Ready) return true;
            try
            {
                if (state.FinishPreparation(session, out string error)) { errorMessage = null; return true; }
                errorMessage = error;
            }
            catch (Exception exception) { errorMessage = exception.Message; }
            quickSequence = false;
            return false;
        }

        private void Update()
        {
            if (!isOpen) return;
            if (actor == null || state == null || owner == null || !owner.isActiveAndEnabled || !actor.IsInDialogue)
            { CancelCasting(); return; }
            if (!GameSessionController.GameplayInputAllowed) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { CancelCasting(); return; }
            if (!isAnimating) return;
            elapsed += Time.unscaledDeltaTime;
            if (elapsed < duration) return;
            isAnimating = false;
            if (!state.RevealNextCast(session))
            { errorMessage = "本次投掷暂时无法揭示，请关闭后重新进入。"; quickSequence = false; return; }
            if (RevealedLines == 6) { quickSequence = false; PrepareChart(); }
            else if (quickSequence) BeginAnimation();
        }

        private void CloseView()
        {
            if (!isOpen) return;
            bool continueToActions = handingOff;
            var callback = onReady;
            isOpen = false; isAnimating = false; quickSequence = false; handingOff = false; onReady = null;
            if (actor != null) { actor.showUI = previousUI; actor.keyboardInput = previousKeyboard; }
            if (!continueToActions && state != null && session != null)
                state.RecordTrace(session, "关闭起卦", "保留已揭示的 " + RevealedLines + "/6 爻与本次铜钱结果。");
            enabled = false;
            if (continueToActions) callback?.Invoke();
        }

        private void OnDisable() { if (isOpen) CancelCasting(); }
        private void OnDestroy() { if (coinTexture != null) Destroy(coinTexture); }

        private void OnGUI()
        {
            if (!isOpen || !GameSessionController.GameplayInputAllowed) return;
            bool animate = isAnimating;
            bool ready = session.phase == CheckSessionPhase.Ready;
            int revealed = RevealedLines;
            bool throwRequested = false, confirmRequested = false, cancelRequested = false;
            Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0.02f, 0.04f, 0.07f, 0.9f));
            float width = Mathf.Max(1, Mathf.Min(880, Screen.width - 28));
            float height = Mathf.Max(1, Mathf.Min(720, Screen.height - 28));
            var window = new Rect((Screen.width - width) / 2, (Screen.height - height) / 2, width, height);
            Fill(window, new Color(0.08f, 0.11f, 0.16f));
            GUI.BeginGroup(window);
            var title = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, normal = { textColor = new Color(0.94f, 0.82f, 0.55f) } };
            var label = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true, normal = { textColor = new Color(0.86f, 0.91f, 0.96f) } };
            GUI.Label(new Rect(22, 14, width - 90, 32), definition.title + " · 起卦", title);
            cancelRequested = GUI.Button(new Rect(width - 58, 16, 36, 30), "×");
            GUI.Label(new Rect(22, 51, width - 44, 26), session.divination.month + "　" + session.divination.day + "　·　已成 " + revealed + "/6 爻", label);
            var viewport = new Rect(18, 86, width - 36, Mathf.Max(1, height - 158));
            float contentWidth = Mathf.Max(1, viewport.width - 18);
            scroll = GUI.BeginScrollView(viewport, scroll, new Rect(0, 0, contentWidth, 588));
            DrawHexagrams(contentWidth, revealed, label);
            DrawCoins(new Rect(0, 250, contentWidth, 175), animate, revealed, label);
            GUI.Label(new Rect(8, 426, contentWidth - 16, 29), revealed == 6 ? "六爻已成，排盘与加值" : "三枚铜钱自下而上成爻，动爻在变卦中阴阳互换。", label);
            if (ready)
            {
                float cellWidth = (contentWidth - 20) / 3;
                for (int i = 0; i < 6; i++)
                {
                    int bonus = session.modifiers[i];
                    var cell = new Rect(8 + i % 3 * cellWidth, 465 + i / 3 * 40, cellWidth - 8, 32);
                    Fill(cell, new Color(0.13f, 0.19f, 0.26f));
                    GUI.Label(new Rect(cell.x + 8, cell.y + 5, cell.width - 16, 24), CheckEncounter.BehaviorName((CheckBehavior)i) + "　" + (bonus >= 0 ? "+" : "") + bonus, label);
                }
            }
            else GUI.Label(new Rect(8, 466, contentWidth - 16, 70), revealed == 6 ? "排盘未完成，请检查下方提示后重试。" : "正面计 3，背面计 2。6 老阴、7 少阳、8 少阴、9 老阳；老阴与老阳为动爻。", label);
            if (!string.IsNullOrWhiteSpace(errorMessage)) GUI.Label(new Rect(8, 548, contentWidth - 16, 38), errorMessage, label);
            GUI.EndScrollView();
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && !animate && !ready;
            QuickCast = GUI.Toggle(new Rect(22, height - 61, 150, 26), QuickCast, "快速连续投掷");
            GUI.enabled = previousEnabled && !animate;
            string buttonText = ready ? "选择行动" : (revealed == 6 ? "重试排盘" : "投掷铜钱 · 第 " + (revealed + 1) + " 爻");
            if (GUI.Button(new Rect(width - 260, height - 72, 236, 40), animate ? "铜钱落定中…" : buttonText))
            { if (revealed == 6) confirmRequested = true; else throwRequested = true; }
            GUI.enabled = previousEnabled;
            GUI.Label(new Rect(22, height - 32, width - 44, 22), "Esc 关闭 · 重新进入会保留已揭示的结果", label);
            GUI.EndGroup();
            // All controls are drawn from the same snapshot before a click can change the dialogue.
            if (cancelRequested) CancelCasting();
            else if (confirmRequested) ConfirmContinue();
            else if (throwRequested) ThrowNext();
        }

        private void DrawHexagrams(float width, int revealed, GUIStyle label)
        {
            float column = (width - 20) / 2;
            string originalName = session.divination.chart == null ? "本卦" : "本卦 · " + session.divination.chart.benGuaName;
            string changedName = session.divination.chart == null ? "变卦" : "变卦 · " + session.divination.chart.bianGuaName;
            GUI.Label(new Rect(12, 4, column - 10, 28), originalName, label);
            GUI.Label(new Rect(column + 22, 4, column - 10, 28), changedName, label);
            for (int row = 0; row < 6; row++)
            {
                int index = 5 - row;
                float y = 44 + row * 32;
                GUI.Label(new Rect(8, y - 5, 46, 26), (index + 1) + "爻", label);
                int value = index < revealed ? session.divination.casting.yaoValues[index] : 0;
                DrawLine(new Rect(58, y, Mathf.Max(20, column - 100), 8), value, false);
                DrawLine(new Rect(column + 50, y, Mathf.Max(20, column - 100), 8), value, true);
                if (value != 0)
                {
                    string marker = value == 6 || value == 9 ? "动 " : "";
                    GUI.Label(new Rect(column - 36, y - 6, 56, 27), marker + value, label);
                }
            }
        }

        private static void DrawLine(Rect rect, int value, bool changed)
        {
            if (value == 0) { Fill(rect, new Color(0.17f, 0.23f, 0.3f)); return; }
            bool moving = value == 6 || value == 9;
            bool yang = value == 7 || value == 9;
            if (changed && moving) yang = !yang;
            Color tint = moving ? new Color(0.96f, 0.72f, 0.32f) : new Color(0.7f, 0.84f, 0.94f);
            if (yang) Fill(rect, tint);
            else
            {
                float half = rect.width * 0.42f;
                Fill(new Rect(rect.x, rect.y, half, rect.height), tint);
                Fill(new Rect(rect.xMax - half, rect.y, half, rect.height), tint);
            }
        }

        private void DrawCoins(Rect area, bool animate, int revealed, GUIStyle label)
        {
            EnsureCoinTexture();
            Fill(area, new Color(0.055f, 0.08f, 0.115f));
            float t = animate ? Mathf.Clamp01(elapsed / duration) : 1;
            int lineIndex = animate ? rollingLine : Mathf.Max(0, revealed - 1);
            float size = Mathf.Min(72, area.width / 5);
            for (int i = 0; i < 3; i++)
            {
                int face = session.divination.casting.coinFaces[lineIndex * 3 + i];
                float spin = animate ? Mathf.Cos(t * Mathf.PI * 12 + i * 1.4f) : 1;
                float coinWidth = size * (animate ? Mathf.Max(0.12f, Mathf.Abs(spin)) : 1);
                float x = area.x + area.width / 2 + (i - 1) * (size + 38) - coinWidth / 2;
                float y = area.y + 75 - size / 2 - (animate ? Mathf.Sin(t * Mathf.PI) * 58 : 0);
                GUI.DrawTexture(new Rect(x, y, coinWidth, size), coinTexture, ScaleMode.StretchToFill);
                if (animate || revealed > 0)
                {
                    bool front = animate && t < 0.9f ? spin >= 0 : face == 1;
                    GUI.Label(new Rect(x - 18, y + size + 6, coinWidth + 36, 25), front ? "正 · 3" : "背 · 2", label);
                }
            }
            string message = animate ? "第 " + (rollingLine + 1) + " 爻 · 铜钱抛起、翻转、落定" : "点击投掷，三枚铜钱同时落定";
            if (!animate && revealed > 0)
            {
                int value = session.divination.casting.yaoValues[revealed - 1];
                string name = value == 6 ? "老阴（动）" : value == 7 ? "少阳" : value == 8 ? "少阴" : "老阳（动）";
                message = "第 " + revealed + " 爻：" + value + " · " + name;
            }
            GUI.Label(new Rect(area.x + 16, area.y + area.height - 29, area.width - 32, 25), message, label);
        }

        private void EnsureCoinTexture()
        {
            if (coinTexture != null) return;
            const int size = 64;
            coinTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "CheckCastingCoin", hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = x - 31.5f, dy = y - 31.5f;
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                bool hole = Mathf.Abs(dx) < 7 && Mathf.Abs(dy) < 7;
                pixels[y * size + x] = radius > 31 || hole ? Color.clear : radius > 26 ? new Color(0.94f, 0.72f, 0.32f) :
                    new Color(0.61f, 0.39f, 0.13f);
            }
            coinTexture.SetPixels(pixels); coinTexture.Apply();
        }

        private static void Fill(Rect rect, Color color)
        {
            Color before = GUI.color; GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = before;
        }
    }
}

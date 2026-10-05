using System;
using System.IO;
using System.Linq;
using Emerge.Checks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Emerge.GameFlow
{
    public sealed class GameMenuUI : MonoBehaviour
    {
        private GameSessionController session;
        private GameObject mainPage, loadPage, settingsPage, confirmationPage, loadingPage, allocationPage;
        private Button continueButton, manualButton, autoButton, reallocateButton, cancelAllocation;
        private GameObject settingsButton;
        private Text saveSummary, manualSummary, autoSummary, mainMessage, settingsMessage, settingsAllocationHint, loadingText, toast;
        private Text remainingPoints, allocationHint, allocationTitle, allocationWarning, tooltipTitle, tooltipDescription, tooltipActions;
        private readonly Text[] attributeBonuses = new Text[SixKinAttributes.Count];
        private readonly Text[] attributeValues = new Text[SixKinAttributes.Count];
        private readonly Button[] attributePlus = new Button[SixKinAttributes.Count], attributeMinus = new Button[SixKinAttributes.Count];
        private Button confirmAllocation;
        private RectTransform attributeTooltip;
        private Camera menuCamera;
        private int tooltipAttribute = -1;
        public bool AttributeTooltipVisible => attributeTooltip != null && attributeTooltip.gameObject.activeInHierarchy;
        public string AttributeTooltipText => tooltipTitle == null ? "" : tooltipTitle.text + "\n" + tooltipDescription.text + "\n" + tooltipActions.text;
        public string AllocationWarningText => allocationWarning?.text ?? "";
        private readonly Color ink = new Color(0.07f, 0.09f, 0.15f, 1);
        private readonly Color accent = new Color(0.35f, 0.78f, 0.66f, 1);
        private static Font cachedFont;
        private static Font MenuFont
        {
            get
            {
                if (cachedFont == null)
                    cachedFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Noto Sans CJK SC", "Arial" }, 32);
                if (cachedFont == null) cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return cachedFont;
            }
        }
        public bool HasSecondaryPage => (loadPage != null && loadPage.activeSelf) || (confirmationPage != null && confirmationPage.activeSelf);
        private void LateUpdate()
        {
            // Combat has its own command footer; save messages remain visible in the pause menu.
            if (toast != null && session != null)
                toast.gameObject.SetActive(session.Phase == GameSessionPhase.Playing && !HasSecondaryPage && !Emerge.Battle.BattleController.AnyBattleActive);
        }

        public void Build(GameSessionController owner)
        {
            if (session != null) return;
            session = owner;
            var cameraObject = new GameObject("Menu UI Camera");
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.localPosition = new Vector3(0, 0, -50);
            var camera = cameraObject.AddComponent<Camera>();
            menuCamera = camera;
            camera.orthographic = true; camera.orthographicSize = 5;
            camera.clearFlags = CameraClearFlags.Depth;
            camera.cullingMask = 1 << 5; camera.depth = 100;
            camera.nearClipPlane = 0.01f; camera.farClipPlane = 20;
            var root = new GameObject("Game Menu Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.layer = 5;
            root.transform.SetParent(transform, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = 1; canvas.sortingOrder = 1000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = 0.5f;
            EnsureEventSystem();

            mainPage = ScreenPanel("Main Menu", root.transform, ink);
            Decorate(mainPage.transform);
            var panel = CenterPanel(mainPage.transform, "Menu Card", new Vector2(440, 584));
            Label(panel, "LIGHT", new Vector2(24, -20), new Vector2(392, 70), 52, accent);
            Label(panel, "像素冒险", new Vector2(24, -98), new Vector2(392, 28), 19, Color.white);
            Label(panel, "你的下一段冒险，从这里开始。", new Vector2(24, -133), new Vector2(392, 25), 15, new Color(0.65f, 0.73f, 0.85f));
            continueButton = Button(panel, "Continue Game", "继续游戏", new Vector2(24, -184), new Vector2(392, 52), session.ContinueGame, true);
            Button(panel, "New Game", "开始游戏 / 新的游戏", new Vector2(24, -250), new Vector2(392, 52), RequestNewGame);
            Button(panel, "Load Saves", "读取存档", new Vector2(24, -316), new Vector2(392, 52), ShowLoadPage);
            Button(panel, "Quit Game", "退出游戏", new Vector2(24, -382), new Vector2(392, 52), session.QuitGame);
            saveSummary = Label(panel, "", new Vector2(24, -450), new Vector2(392, 72), 14, new Color(0.7f, 0.8f, 0.87f));
            mainMessage = Label(panel, "", new Vector2(24, -530), new Vector2(392, 45), 14, accent);
            var footer = Rect("Footer", mainPage.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 18), new Vector2(900, 28));
            var footerLabel = Label(footer, "自动保存每 " + session.autoSaveInterval.ToString("0.#") + " 秒执行 · 手动与自动存档分别保留一份备份", Vector2.zero, new Vector2(900, 28), 14, new Color(0.6f, 0.67f, 0.78f));
            footerLabel.alignment = TextAnchor.MiddleCenter;

            loadPage = ScreenPanel("Load Menu", root.transform, ink);
            var loadPanel = CenterPanel(loadPage.transform, "Load Card", new Vector2(520, 450));
            Label(loadPanel, "读取存档", new Vector2(28, -24), new Vector2(464, 42), 28, accent);
            manualButton = Button(loadPanel, "Load Manual", "读取手动存档", new Vector2(28, -92), new Vector2(464, 48), session.LoadManual);
            manualSummary = Label(loadPanel, "", new Vector2(28, -150), new Vector2(464, 68), 15, Color.white);
            autoButton = Button(loadPanel, "Load Auto", "读取自动存档", new Vector2(28, -223), new Vector2(464, 48), session.LoadAutomatic);
            autoSummary = Label(loadPanel, "", new Vector2(28, -281), new Vector2(464, 68), 15, Color.white);
            Button(loadPanel, "Back From Load", "返回", new Vector2(28, -365), new Vector2(464, 48), CloseSecondaryPage);

            settingsPage = ScreenPanel("Settings Modal", root.transform, new Color(0, 0, 0, 0.72f));
            var settingsPanel = CenterPanel(settingsPage.transform, "Settings Card", new Vector2(460, 542));
            Label(settingsPanel, "游戏已暂停", new Vector2(28, -22), new Vector2(404, 44), 28, accent);
            Button(settingsPanel, "Resume Game", "继续游戏", new Vector2(28, -92), new Vector2(404, 48), session.ResumeGame, true);
            Button(settingsPanel, "Save Game", "保存游戏", new Vector2(28, -152), new Vector2(404, 48), () => session.SaveManual());
            reallocateButton = Button(settingsPanel, "Reallocate Attributes", "重新分配加点", new Vector2(28, -212), new Vector2(404, 48), () => session.BeginAttributeReallocation());
            Button(settingsPanel, "Save And Return", "保存并返回主菜单", new Vector2(28, -272), new Vector2(404, 48), session.SaveAndReturnToMenu);
            Button(settingsPanel, "Quit From Settings", "保存并退出游戏", new Vector2(28, -332), new Vector2(404, 48), session.QuitGame);
            settingsAllocationHint = Label(settingsPanel, "", new Vector2(28, -394), new Vector2(404, 48), 14, new Color(.76f, .83f, .9f));
            settingsMessage = Label(settingsPanel, "", new Vector2(28, -460), new Vector2(404, 66), 15, Color.white);

            confirmationPage = ScreenPanel("New Game Confirmation", root.transform, new Color(0, 0, 0, 0.82f));
            var confirmPanel = CenterPanel(confirmationPage.transform, "Confirmation Card", new Vector2(480, 300));
            Label(confirmPanel, "开始新的游戏？", new Vector2(28, -24), new Vector2(424, 42), 26, accent);
            Label(confirmPanel, "接下来将分配六亲点数。确认加点后会重置进度并覆盖自动存档。\n已有手动存档将保留，可从“读取存档”恢复。",
                new Vector2(28, -90), new Vector2(424, 88), 17, Color.white);
            Button(confirmPanel, "Confirm New Game", "开始新游戏", new Vector2(28, -214), new Vector2(198, 48), session.BeginNewGame, true);
            Button(confirmPanel, "Cancel New Game", "取消", new Vector2(254, -214), new Vector2(198, 48), CloseSecondaryPage);

            BuildCharacterCreation(root.transform);

            loadingPage = ScreenPanel("Loading Screen", root.transform, ink);
            var loadingPanel = CenterPanel(loadingPage.transform, "Loading Card", new Vector2(440, 150));
            loadingText = Label(loadingPanel, "正在加载…", new Vector2(24, -48), new Vector2(392, 52), 23, accent);
            loadingText.alignment = TextAnchor.MiddleCenter;

            var settings = Rect("Settings Button", root.transform, Vector2.one, Vector2.one, new Vector2(-22, -22), new Vector2(132, 44));
            settingsButton = settings.gameObject;
            AddButton(settings, "设置  [Esc]", session.OpenSettings, false);
            var toastRect = Rect("Save Toast", root.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 56), new Vector2(700, 32));
            toast = Label(toastRect, "", Vector2.zero, new Vector2(700, 32), 15, accent);
            toast.alignment = TextAnchor.MiddleCenter;
        }

        public void ShowMainMenu()
        {
            HidePages(); mainPage.SetActive(true); settingsButton.SetActive(false);
            toast.gameObject.SetActive(false); RefreshSaveSummary();
            mainMessage.text = session.LastMessage;
        }
        public void ShowGameplay()
        {
            HidePages(); settingsButton.SetActive(true); toast.gameObject.SetActive(true);
            EnsureEventSystem();
        }
        public void ShowSettings()
        {
            HidePages(); settingsPage.SetActive(true); settingsButton.SetActive(false); toast.gameObject.SetActive(false);
            settingsMessage.text = session.LastMessage + "\nEsc 可以继续游戏。";
            reallocateButton.interactable = session.CanChangeAttributes;
            settingsAllocationHint.text = Emerge.Battle.BattleController.AnyBattleActive ? "战斗期间不可重新加点。请结束战斗后再修改。" : "重新加点确认后自动保存；手动存档仍需点击“保存游戏”。";
        }
        public void ShowLoading(string text)
        {
            HidePages(); loadingPage.SetActive(true); loadingText.text = text;
            settingsButton.SetActive(false); toast.gameObject.SetActive(false);
        }
        public void ShowCharacterCreation()
        {
            HidePages(); allocationPage.SetActive(true); settingsButton.SetActive(false); toast.gameObject.SetActive(false);
            allocationTitle.text = session.IsChangingAttributes ? "五亲加点 · 重新分配" : session.IsReallocating ? "五亲加点 · 旧角色重新分配" : "五亲加点";
            confirmAllocation.GetComponentInChildren<Text>().text = session.IsChangingAttributes ? "确认修改 · 自动保存" : session.IsReallocating ? "确认加点 · 恢复进度" : "确认加点 · 开始冒险";
            cancelAllocation.GetComponentInChildren<Text>().text = session.IsChangingAttributes ? "返回设置  [Esc]" : "返回主菜单  [Esc]";
            EnsureEventSystem(); RefreshAttributeAllocation();
        }
        public void RefreshAttributeAllocation()
        {
            if (remainingPoints == null) return;
            int remaining = session.AllocationRemaining;
            remainingPoints.text = "剩余点数  " + remaining + " / " + SixKinAttributes.StartingPoints;
            allocationHint.text = remaining == 0 ? (session.IsChangingAttributes ? "确认后立即生效并自动保存；已开始的检定保持原加点。" : session.IsReallocating ? "分配完成，确认后保留原进度并恢复游戏。" : "分配完成，确认后开始冒险。") : "请分配剩余的 " + remaining + " 点。";
            var zeroNames = Enumerable.Range(0, SixKinAttributes.Count).Where(i => session.AllocatedPoints((CheckBehavior)i) == 0).Select(i => SixKinAttributes.Get((CheckBehavior)i).name).ToArray();
            allocationWarning.text = zeroNames.Length == 0 ? "五亲均已投入，可抽取所有类别的高级技能。" : "未投入：" + string.Join("、", zeroNames) + "。这些类别的高级技能不会在战斗中解锁。";
            for (int i = 0; i < SixKinAttributes.Count; i++)
            {
                int value = session.AllocatedPoints((CheckBehavior)i);
                attributeValues[i].text = value.ToString();
                attributeBonuses[i].text = "效果 ×" + Emerge.Battle.BattleBuildRules.Multiplier(value).ToString("0.0");
                attributeMinus[i].interactable = value > 0;
                attributePlus[i].interactable = remaining > 0;
            }
            confirmAllocation.interactable = remaining == 0;
            if (AttributeTooltipVisible) ShowAttributeTooltip((CheckBehavior)tooltipAttribute, Input.mousePosition);
        }
        public void ShowAttributeTooltip(CheckBehavior attribute, Vector2 screenPoint)
        {
            if (session.Phase != GameSessionPhase.CharacterCreation || !allocationPage.activeInHierarchy) return;
            var info = SixKinAttributes.Get(attribute);
            tooltipAttribute = (int)attribute;
            tooltipTitle.text = info.name + " · " + info.behavior;
            tooltipDescription.text = info.description;
            tooltipActions.text = "对应行为：" + info.behavior + "\n" + info.actions + "\n\n" + info.battleRole +
                "\n当前技能效果 ×" + Emerge.Battle.BattleBuildRules.Multiplier(session.AllocatedPoints(attribute)).ToString("0.0") +
                (session.AllocatedPoints(attribute) == 0 ? "；0 点无法解锁该类高级技能。" : "；加点提高该类抽取权重。");
            attributeTooltip.gameObject.SetActive(true); MoveAttributeTooltip(screenPoint);
        }
        public void MoveAttributeTooltip(Vector2 screenPoint)
        {
            if (!AttributeTooltipVisible) return;
            var parent = (RectTransform)allocationPage.transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPoint, menuCamera, out var point)) return;
            var bounds = parent.rect;
            float x = Mathf.Clamp(point.x + 18, bounds.xMin + 12, bounds.xMax - attributeTooltip.rect.width - 12);
            float y = Mathf.Clamp(point.y - 18, bounds.yMin + attributeTooltip.rect.height + 12, bounds.yMax - 12);
            attributeTooltip.anchoredPosition = new Vector2(x, y);
        }
        public void HideAttributeTooltip(CheckBehavior attribute)
        {
            if (tooltipAttribute != (int)attribute || attributeTooltip == null) return;
            tooltipAttribute = -1; attributeTooltip.gameObject.SetActive(false);
        }
        public void ShowLoadPage()
        {
            HidePages(); loadPage.SetActive(true); settingsButton.SetActive(false); RefreshSaveSummary();
        }
        public void RequestNewGame()
        {
            if (session.Store.TryLatest(out _, out _, out _))
            { HidePages(); confirmationPage.SetActive(true); }
            else session.BeginNewGame();
        }
        public void CloseSecondaryPage()
        {
            if (session.Phase == GameSessionPhase.MainMenu) ShowMainMenu();
            else if (session.Phase == GameSessionPhase.Settings) ShowSettings();
        }
        public void SetMessage(string message)
        {
            if (mainMessage == null) return;
            mainMessage.text = message; settingsMessage.text = message; toast.text = message;
            if (session.IsChangingAttributes && allocationHint != null) allocationHint.text = message;
        }

        private void RefreshSaveSummary()
        {
            bool hasLatest = session.Store.TryLatest(out var latest, out var latestSlot, out string message);
            continueButton.interactable = hasLatest;
            continueButton.GetComponentInChildren<Text>().text = hasLatest ? "继续游戏" : "继续游戏（暂无存档）";
            saveSummary.text = hasLatest ? "最近存档 · " + (latestSlot == SaveSlot.Manual ? "手动" : "自动") + "\n" + Summary(latest) :
                "还没有存档，点击“开始游戏 / 新的游戏”开始冒险。";
            bool manual = session.Store.TryRead(SaveSlot.Manual, out var manualData, out var manualMessageText);
            bool auto = session.Store.TryRead(SaveSlot.Auto, out var autoData, out var autoMessageText);
            manualButton.interactable = manual; autoButton.interactable = auto;
            manualSummary.text = manual ? Summary(manualData) + (string.IsNullOrEmpty(manualMessageText) ? "" : "\n备份恢复") : manualMessageText;
            autoSummary.text = auto ? Summary(autoData) + (string.IsNullOrEmpty(autoMessageText) ? "" : "\n备份恢复") : autoMessageText;
        }

        private static string Summary(GameSaveData data)
        {
            var player = data.actors == null ? null : data.actors.Find(actor => actor != null && actor.id == data.playerId);
            int itemCount = 0, fruitCount = 0;
            if (player != null && player.propState != null && player.propState.inventory != null)
            {
                foreach (var item in player.propState.inventory)
                {
                    if (item == null) continue;
                    itemCount += Mathf.Max(0, item.amount);
                    if (item.displayName == "果实" || item.key == "fruit") fruitCount += Mathf.Max(0, item.amount);
                }
            }
            string sceneName = Path.GetFileNameWithoutExtension(data.scenePath);
            return new DateTime(data.savedUtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("MM-dd HH:mm:ss") +
                " · 时长 " + TimeSpan.FromSeconds(data.playedSeconds).ToString(@"hh\:mm\:ss") +
                "\n" + sceneName + " · 背包 " + itemCount + " 件 · 果实 × " + fruitCount +
                "\n" + data.reason;
        }
        private void HidePages()
        {
            if (attributeTooltip != null) attributeTooltip.gameObject.SetActive(false);
            tooltipAttribute = -1;
            foreach (var page in new[] { mainPage, loadPage, settingsPage, confirmationPage, loadingPage, allocationPage }) page.SetActive(false);
        }

        private void BuildCharacterCreation(Transform parent)
        {
            allocationPage = ScreenPanel("Character Creation", parent, ink);
            Decorate(allocationPage.transform);
            var panel = CenterPanel(allocationPage.transform, "Attribute Allocation Card", new Vector2(1060, 640));
            allocationTitle = Label(panel, "五亲加点", new Vector2(28, -22), new Vector2(740, 52), 32, accent);
            Label(panel, "将 8 点自由分配给五亲，构建你的检定能力与战斗流派。\n鼠标悬停查看解释、对应行为及战斗职责。",
                new Vector2(28, -84), new Vector2(720, 54), 17, new Color(.76f, .83f, .9f));
            remainingPoints = Label(panel, "", new Vector2(780, -36), new Vector2(252, 40), 25, accent);
            for (int i = 0; i < SixKinAttributes.Count; i++)
            {
                var attribute = (CheckBehavior)i; var info = SixKinAttributes.Get(attribute);
                var row = Rect("Attribute Row " + attribute, panel, new Vector2(0, 1), new Vector2(0, 1),
                    new Vector2(28, -152 - i * 62), new Vector2(1004, 54));
                row.gameObject.AddComponent<Image>().color = new Color(.13f, .19f, .27f);
                row.gameObject.AddComponent<CharacterAttributeHover>().Bind(this, attribute);
                Label(row, info.name, new Vector2(18, -10), new Vector2(115, 34), 24, Color.white);
                Label(row, info.behavior + "  ·  " + info.actions, new Vector2(140, -15), new Vector2(490, 32), 16, new Color(.75f, .83f, .88f));
                attributeBonuses[i] = Label(row, "", new Vector2(636, -15), new Vector2(128, 32), 16, accent);
                attributeMinus[i] = Button(row, "Attribute Minus " + attribute, "−", new Vector2(766, -6), new Vector2(42, 42),
                    () => session.AdjustAttribute(attribute, -1));
                attributeValues[i] = Label(row, "0", new Vector2(816, -6), new Vector2(68, 42), 26, accent);
                attributeValues[i].alignment = TextAnchor.MiddleCenter;
                attributePlus[i] = Button(row, "Attribute Plus " + attribute, "+", new Vector2(892, -6), new Vector2(42, 42),
                    () => session.AdjustAttribute(attribute, 1));
            }
            Label(panel, "属性点数作为对应检定基础值，提高该类高级技能抽取权重，每点增加 10% 技能基础效果。\n某亲 0 点时，其高级技能不会出现；常驻基础技能可用。定卦只决定解锁，倍率由加点固定。",
                new Vector2(28, -462), new Vector2(1004, 48), 15, new Color(.76f, .83f, .9f));
            allocationWarning = Label(panel, "", new Vector2(28, -516), new Vector2(1004, 28), 15, new Color(1, .72f, .45f));
            allocationHint = Label(panel, "", new Vector2(28, -546), new Vector2(1004, 28), 16, accent);
            cancelAllocation = Button(panel, "Cancel Character Creation", "返回主菜单  [Esc]", new Vector2(28, -578), new Vector2(242, 42), session.CancelCharacterCreation);
            Button(panel, "Reset Attributes", "重置点数", new Vector2(290, -578), new Vector2(180, 42), session.ResetAttributeAllocation);
            confirmAllocation = Button(panel, "Confirm Attributes", "确认加点 · 开始冒险", new Vector2(738, -578), new Vector2(294, 42),
                () => session.ConfirmCharacterCreation(), true);

            attributeTooltip = Rect("Attribute Tooltip", allocationPage.transform, Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(420, 320));
            var background = attributeTooltip.gameObject.AddComponent<Image>();
            background.color = new Color(.045f, .075f, .12f, .99f); background.raycastTarget = false;
            tooltipTitle = Label(attributeTooltip, "", new Vector2(20, -16), new Vector2(380, 36), 25, accent);
            tooltipDescription = Label(attributeTooltip, "", new Vector2(20, -64), new Vector2(380, 66), 17, Color.white);
            tooltipActions = Label(attributeTooltip, "", new Vector2(20, -136), new Vector2(380, 170), 16, new Color(.75f, .84f, .9f));
            attributeTooltip.gameObject.SetActive(false);
        }
        private static void EnsureEventSystem()
        {
            if (EventSystem.current == null) new GameObject("Game UI EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private void Decorate(Transform parent)
        {
            for (int i = 0; i < 18; i++)
            {
                var rect = Rect("Pixel Tile", parent, new Vector2(i % 2 == 0 ? 0 : 1, 0.5f), Vector2.one * 0.5f,
                    new Vector2((i % 2 == 0 ? 1 : -1) * (80 + i % 5 * 38), (i - 9) * 48), new Vector2(24 + i % 3 * 16, 24 + i % 3 * 16));
                rect.gameObject.AddComponent<Image>().color = new Color(0.15f, 0.25f + i % 3 * 0.03f, 0.3f, 0.65f);
            }
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = 5;
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot;
            rect.anchoredPosition = position; rect.sizeDelta = size;
            return rect;
        }
        private static GameObject ScreenPanel(string name, Transform parent, Color color)
        {
            var panel = Rect(name, parent, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            panel.anchorMax = Vector2.one; panel.offsetMin = panel.offsetMax = Vector2.zero;
            panel.gameObject.AddComponent<Image>().color = color;
            return panel.gameObject;
        }
        private RectTransform CenterPanel(Transform parent, string name, Vector2 size)
        {
            var frame = Rect(name + " Border", parent, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, size + Vector2.one * 4);
            frame.gameObject.AddComponent<Image>().color = new Color(0.25f, 0.42f, 0.46f);
            var panel = Rect(name, frame, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero, size);
            panel.gameObject.AddComponent<Image>().color = new Color(0.09f, 0.13f, 0.2f);
            return panel;
        }
        private static Text Label(Transform parent, string value, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            var label = Rect("Label", parent, new Vector2(0, 1), new Vector2(0, 1), position, size).gameObject.AddComponent<Text>();
            label.font = MenuFont; label.fontSize = fontSize; label.text = value;
            label.color = color; label.raycastTarget = false;
            return label;
        }
        private Button Button(Transform parent, string name, string text, Vector2 position, Vector2 size,
            UnityEngine.Events.UnityAction callback, bool primary = false)
        {
            var rect = Rect(name, parent, new Vector2(0, 1), new Vector2(0, 1), position, size);
            return AddButton(rect, text, callback, primary);
        }
        private Button AddButton(RectTransform rect, string text, UnityEngine.Events.UnityAction callback, bool primary)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = primary ? accent : new Color(0.19f, 0.26f, 0.35f);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(0.85f, 1, 1);
            colors.pressedColor = new Color(0.6f, 0.8f, 0.8f); colors.disabledColor = new Color(0.38f, 0.4f, 0.45f);
            button.colors = colors; button.onClick.AddListener(callback);
            var label = Label(rect, text, Vector2.zero, rect.sizeDelta, 20, primary ? ink : Color.white);
            label.alignment = TextAnchor.MiddleCenter;
            return button;
        }
    }
}

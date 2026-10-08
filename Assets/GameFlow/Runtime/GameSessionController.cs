using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Emerge.Props;
using Emerge.Checks;
using PixelPrototype;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Emerge.GameFlow
{
    [DefaultExecutionOrder(-2000), RequireComponent(typeof(GameMenuUI))]
    public sealed class GameSessionController : MonoBehaviour
    {
        public string gameScenePath = "Assets/Scenes/PropsDemo.unity";
        [Min(1)] public float autoSaveInterval = 10;
        [SerializeField] private GameSessionPhase phase = GameSessionPhase.MainMenu;
        [SerializeField] private float playedSeconds;
        [SerializeField] private string lastMessage;
        private GameSaveStore store;
        private GameMenuUI menu;
        private float nextAutoSave;
        private bool hasStartedGame;
        private bool restoringBattleWorld;
        private CharacterAttributeAllocation allocation;
        private GameSaveData reallocationSave;
        private SaveSlot? reallocationSlot;
        private bool changingAttributes;
        public bool IsReallocating => reallocationSave != null;
        public bool IsChangingAttributes => changingAttributes;
        public bool CanChangeAttributes => phase == GameSessionPhase.Settings && hasStartedGame && !Emerge.Battle.BattleController.AnyBattleActive;
        public static GameSessionController Instance { get; private set; }
        public static bool SessionInputAllowed => Instance == null || Instance.phase == GameSessionPhase.Playing;
        public static bool GameplayInputAllowed => SessionInputAllowed && !Emerge.Battle.BattleController.AnyBattleActive && !PointLoadoutUI.AnyOpen;
        public GameSessionPhase Phase => phase;
        public float PlayedSeconds => playedSeconds;
        public string LastMessage => lastMessage;
        public string SaveDirectory => store?.DirectoryPath;
        public GameSaveStore Store => store;
        public bool IsTransitioning => phase == GameSessionPhase.Loading;
        public bool IsReady { get; private set; }
        public int AllocationRemaining => allocation?.Remaining ?? SixKinAttributes.StartingPoints;
        public int AllocatedPoints(CheckBehavior attribute) => allocation?.Get(attribute) ?? 0;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Time.timeScale = 0;
            // Emerge uses the same Unity product name; keep the two games' save files separate.
            store = new GameSaveStore(Path.Combine(Application.persistentDataPath, "Saves", "LightPrototype"));
            menu = GetComponent<GameMenuUI>();
        }
        private void Start()
        {
            if (Instance != this) return;
            menu.Build(this); menu.ShowMainMenu(); IsReady = true;
        }
        private void Update()
        {
            if (Instance != this || !IsReady) return;
            if (phase == GameSessionPhase.Playing)
            {
                playedSeconds += Time.deltaTime;
                if (Time.unscaledTime >= nextAutoSave)
                {
                    SaveAutomatic("定时自动存档");
                    nextAutoSave = Time.unscaledTime + Mathf.Max(1, autoSaveInterval);
                }
            }
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (PointLoadoutUI.AnyOpen) { PointLoadoutUI.Open.Close(); return; }
            if (phase == GameSessionPhase.CharacterCreation) CancelCharacterCreation();
            else if (menu.HasSecondaryPage) menu.CloseSecondaryPage();
            else if (phase == GameSessionPhase.Playing)
            {
                var dialogue = FindObjectsOfType<PlayerInteractor>().FirstOrDefault(actor => actor.IsInDialogue);
                if (dialogue != null) dialogue.CancelDialogue(); else OpenSettings();
            }
            else if (phase == GameSessionPhase.Settings) ResumeGame();
        }
        public void BeginNewGame()
        {
            if (!IsReady || phase != GameSessionPhase.MainMenu) return;
            if (!Application.CanStreamedLevelBeLoaded(gameScenePath)) { Notify("游戏场景未加入 Build Settings。"); return; }
            allocation = new CharacterAttributeAllocation();
            reallocationSave = null;
            reallocationSlot = null;
            changingAttributes = false;
            hasStartedGame = false; phase = GameSessionPhase.CharacterCreation; Time.timeScale = 0;
            menu.ShowCharacterCreation();
        }
        public bool AdjustAttribute(CheckBehavior attribute, int delta)
        {
            if (phase != GameSessionPhase.CharacterCreation || allocation == null || !allocation.TryChange(attribute, delta)) return false;
            menu.RefreshAttributeAllocation(); return true;
        }
        public bool BeginAttributeReallocation()
        {
            if (!CanChangeAttributes) { Notify("请在战斗结束后，通过设置重新分配点数。"); return false; }
            var checks = MainPlayer()?.GetComponent<CheckActorState>();
            if (checks == null || !SixKinAttributes.IsValidBuild(checks.attributes)) { Notify("当前角色的六亲点数无效，无法重新分配。"); return false; }
            allocation = new CharacterAttributeAllocation(checks.attributes);
            reallocationSave = null; reallocationSlot = null; changingAttributes = true;
            phase = GameSessionPhase.CharacterCreation; Time.timeScale = 0; menu.ShowCharacterCreation();
            return true;
        }
        public void ResetAttributeAllocation()
        {
            if (phase != GameSessionPhase.CharacterCreation || allocation == null) return;
            allocation.Reset(); menu.RefreshAttributeAllocation();
        }
        public bool ConfirmCharacterCreation()
        {
            if (phase != GameSessionPhase.CharacterCreation || allocation == null || !allocation.IsComplete) return false;
            var attributes = allocation.ToAttributes();
            if (changingAttributes) return ConfirmAttributeReallocation(attributes);
            var saved = reallocationSave;
            var sourceSlot = reallocationSlot;
            if (saved != null) UpgradePlayerAttributes(saved, attributes);
            reallocationSave = null;
            reallocationSlot = null;
            allocation = null;
            StartCoroutine(EnterGame(saved, newAttributes: saved == null ? attributes : null, upgradeSlot: sourceSlot));
            return true;
        }
        public void CancelCharacterCreation()
        {
            if (phase != GameSessionPhase.CharacterCreation) return;
            if (changingAttributes)
            {
                allocation = null; changingAttributes = false; phase = GameSessionPhase.Settings; Time.timeScale = 0; menu.ShowSettings();
                return;
            }
            allocation = null; reallocationSave = null; reallocationSlot = null; phase = GameSessionPhase.MainMenu; Time.timeScale = 0; menu.ShowMainMenu();
        }
        private bool ConfirmAttributeReallocation(ActorCheckAttributes attributes)
        {
            if (!hasStartedGame || Emerge.Battle.BattleController.AnyBattleActive || !SixKinAttributes.IsValidBuild(attributes)) return false;
            var checks = MainPlayer()?.GetComponent<CheckActorState>();
            if (checks == null) { Notify("没有可重新加点的角色。"); return false; }
            try
            {
                var data = CaptureGame("角色重新加点");
                UpgradePlayerAttributes(data, attributes, "角色重新加点");
                // Persist the complete world first. On a write failure, the live build and draft stay intact.
                if (!store.TryWrite(SaveSlot.Auto, data, out var error)) { Notify(error); return false; }
                checks.TrySetAllocatedAttributes(attributes);
            }
            catch (Exception exception) { Notify("重新加点保存失败：" + exception.Message); return false; }
            allocation = null; changingAttributes = false; phase = GameSessionPhase.Settings; Time.timeScale = 0;
            menu.ShowSettings(); Notify("已重新分配六亲点数并自动保存；新的检定和战斗使用新加点。");
            Debug.Log("[角色加点] 设置中重新分配：父母 " + attributes.parent + "，子孙 " + attributes.offspring + "，官鬼 " + attributes.officer + "，妻财 " + attributes.wealth + "，兄弟 " + attributes.sibling);
            return true;
        }
        private static PlayerMovement MainPlayer()
        {
            var players = FindObjectsOfType<PlayerMovement>(true).Where(actor => actor.gameObject.scene == SceneManager.GetActiveScene()).ToArray();
            return players.FirstOrDefault(actor => actor.CompareTag("Player")) ?? players.FirstOrDefault();
        }
        public void ContinueGame()
        {
            if (!IsReady || IsTransitioning || phase == GameSessionPhase.CharacterCreation) return;
            if (!store.TryLatest(out var data, out var slot, out string message)) { Notify(message); return; }
            LoadData(data, message, slot);
        }
        public void LoadManual() { LoadSlot(SaveSlot.Manual); }
        public void LoadAutomatic() { LoadSlot(SaveSlot.Auto); }
        private void LoadSlot(SaveSlot slot)
        {
            if (!IsReady || IsTransitioning || phase == GameSessionPhase.CharacterCreation) return;
            if (!store.TryRead(slot, out var data, out string message)) { Notify(message); return; }
            LoadData(data, message, slot);
        }
        private void LoadData(GameSaveData data, string warning, SaveSlot slot)
        {
            if (!Application.CanStreamedLevelBeLoaded(data.scenePath)) { Notify("存档中的场景未加入 Build Settings，无法加载。"); return; }
            var player = data.actors.Find(actor => actor.id == data.playerId);
            if (player.checkState == null || player.checkState.attributeRulesVersion != SixKinAttributes.RulesVersion)
            {
                reallocationSave = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(data));
                reallocationSlot = slot;
                allocation = new CharacterAttributeAllocation(); hasStartedGame = false;
                phase = GameSessionPhase.CharacterCreation; Time.timeScale = 0; menu.ShowCharacterCreation();
                return;
            }
            StartCoroutine(EnterGame(data, warning));
        }
        private static void UpgradePlayerAttributes(GameSaveData data, ActorCheckAttributes attributes, string reason = "六亲规则升级")
        {
            data.reason = reason;
            var player = data.actors.Find(actor => actor.id == data.playerId);
            if (player.checkState == null) player.checkState = new CheckActorState.Snapshot();
            player.checkState.attributes = attributes.Clone();
            player.checkState.attributeRulesVersion = SixKinAttributes.RulesVersion;
            var point = player.pointBattleState?.returnPoint ?? player.battleState?.returnPoint;
            if (point != null)
            {
                if (point.checkState == null) point.checkState = new CheckActorState.Snapshot();
                point.checkState.attributes = attributes.Clone(); point.checkState.attributeRulesVersion = SixKinAttributes.RulesVersion;
                if (point.world != null) UpgradePlayerAttributes(point.world, attributes, reason);
            }
        }
        private IEnumerator EnterGame(GameSaveData data, string warning = null, ActorCheckAttributes newAttributes = null, SaveSlot? upgradeSlot = null)
        {
            CancelDialogues();
            phase = GameSessionPhase.Loading; hasStartedGame = false; Time.timeScale = 0;
            menu.ShowLoading(data == null ? "正在开始新的冒险…" : "正在读取存档…");
            AsyncOperation operation = null;
            try { operation = SceneManager.LoadSceneAsync(data != null ? data.scenePath : gameScenePath, LoadSceneMode.Single); }
            catch (Exception exception) { Notify("场景加载失败：" + exception.Message); }
            if (operation == null) { phase = GameSessionPhase.MainMenu; menu.ShowMainMenu(); yield break; }
            yield return operation;
            yield return null;
            playedSeconds = data != null ? data.playedSeconds : 0;
            string restoreError = null;
            if (data != null)
                try { RestoreGame(data); } catch (Exception exception) { restoreError = "恢复存档失败：" + exception.Message; }
            else
                try { ApplyCreatedAttributes(newAttributes); } catch (Exception exception) { restoreError = "创建角色失败：" + exception.Message; }
            if (restoreError != null) { phase = GameSessionPhase.MainMenu; menu.ShowMainMenu(); Notify(restoreError); yield break; }
            phase = GameSessionPhase.Playing; hasStartedGame = true; Time.timeScale = 1;
            menu.ShowGameplay(); nextAutoSave = Time.unscaledTime + Mathf.Max(1, autoSaveInterval);
            if (data == null) SaveAutomatic("新游戏初始存档");
            else
            {
                Notify((string.IsNullOrEmpty(warning) ? "已读取存档" : warning) + " · " + new DateTime(data.savedUtcTicks, DateTimeKind.Utc).ToLocalTime().ToString("MM-dd HH:mm:ss"));
                if (upgradeSlot.HasValue)
                {
                    if (!store.TryWrite(upgradeSlot.Value, data, out var upgradeError)) Notify(upgradeError);
                    SaveAutomatic("六亲规则升级");
                }
            }
        }
        private void ApplyCreatedAttributes(ActorCheckAttributes attributes)
        {
            if (attributes == null) throw new InvalidOperationException("尚未确认六亲基础点数。");
            var players = FindObjectsOfType<PlayerMovement>(true).Where(actor => actor.gameObject.scene == SceneManager.GetActiveScene()).ToArray();
            var main = players.FirstOrDefault(actor => actor.CompareTag("Player")) ?? players.FirstOrDefault();
            if (main == null) throw new InvalidOperationException("当前场景没有可创建的主角。");
            var checks = main.GetComponent<CheckActorState>();
            if (checks == null) checks = main.gameObject.AddComponent<CheckActorState>();
            if (!checks.RestoreSnapshot(new CheckActorState.Snapshot { attributeRulesVersion = SixKinAttributes.RulesVersion, attributes = attributes.Clone() }))
                throw new InvalidOperationException("六亲基础点数无效。");
            Debug.Log("[角色创建] 六亲基础点数已确认：父母 " + attributes.parent + "，子孙 " + attributes.offspring +
                "，官鬼 " + attributes.officer + "，妻财 " + attributes.wealth + "，兄弟 " + attributes.sibling + "，我 " + attributes.self + "；六项默认各 1 点，另分配 8 点，按点数直接计算。");
        }
        public void OpenSettings()
        {
            if (phase != GameSessionPhase.Playing) return;
            phase = GameSessionPhase.Settings; Time.timeScale = 0;
            foreach (var movement in FindObjectsOfType<PlayerMovement>()) if (movement.Body != null) movement.Body.velocity = Vector2.zero;
            menu.ShowSettings();
        }
        public void ResumeGame()
        {
            if (phase != GameSessionPhase.Settings) return;
            phase = GameSessionPhase.Playing; Time.timeScale = 1; menu.ShowGameplay();
        }
        public bool SaveManual() => Save(SaveSlot.Manual, "手动保存");
        public bool SaveAutomatic(string reason) => Save(SaveSlot.Auto, reason);
        private bool Save(SaveSlot slot, string reason)
        {
            if (!hasStartedGame || IsTransitioning || phase == GameSessionPhase.MainMenu || phase == GameSessionPhase.CharacterCreation || restoringBattleWorld) return false;
            try
            {
                if (!store.TryWrite(slot, CaptureGame(reason), out string error)) { Notify(error); return false; }
                Notify((slot == SaveSlot.Manual ? "游戏已保存" : "已自动保存") + " · " + DateTime.Now.ToString("HH:mm:ss")); return true;
            }
            catch (Exception exception) { Notify("保存失败：" + exception.Message); return false; }
        }
        public void SaveAndReturnToMenu()
        {
            CancelDialogues();
            if (!SaveManual()) return;
            if (!SaveAutomatic("保存并返回主菜单")) return;
            hasStartedGame = false; phase = GameSessionPhase.MainMenu; Time.timeScale = 0; menu.ShowMainMenu();
        }
        public void QuitGame()
        {
            if (IsTransitioning) return;
            if (hasStartedGame && !SaveAutomatic("退出游戏自动存档")) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        public GameSaveData CaptureGame(string reason)
        {
            var data = new GameSaveData { savedUtcTicks = DateTime.UtcNow.Ticks, scenePath = SceneManager.GetActiveScene().path, playedSeconds = playedSeconds, reason = reason };
            var players = FindObjectsOfType<PlayerMovement>(true).Where(actor => actor.gameObject.scene == SceneManager.GetActiveScene()).ToArray();
            var main = players.FirstOrDefault(actor => actor.CompareTag("Player")) ?? players.FirstOrDefault();
            if (main == null) throw new InvalidOperationException("当前场景没有可保存的主角。");
            var seen = new HashSet<string>();
            foreach (var actor in players)
            {
                var identity = actor.GetComponent<SaveIdentity>(); var state = actor.GetComponent<PropGameState>();
                if (identity == null || string.IsNullOrEmpty(identity.Id) || !seen.Add(identity.Id) || state == null)
                    throw new InvalidOperationException("角色存档 ID 缺失或重复，请重新安装菜单与存档系统。");
                var position = actor.transform.position;
                if (actor.Body != null) { position.x = actor.Body.position.x; position.y = actor.Body.position.y; }
                data.actors.Add(new SavedActor { id = identity.Id, position = position, active = actor.gameObject.activeSelf,
                    propState = state.CaptureSnapshot(), checkState = actor.GetComponent<Emerge.Checks.CheckActorState>()?.CaptureSnapshot(),
                    battleState = actor.GetComponent<Emerge.Battle.BattleController>()?.CaptureSnapshot(),
                    pointBattleState = actor.GetComponent<Emerge.Battle.PointBattleController>()?.CaptureSnapshot() });
                if (actor == main) data.playerId = identity.Id;
            }
            foreach (var prop in FindObjectsOfType<PropInstance>(true).Where(item => item.gameObject.scene == SceneManager.GetActiveScene() && item.Definition != null))
                data.props.Add(new SavedProp { instanceId = prop.InstanceId, definitionId = prop.Definition.Id, position = prop.transform.position,
                    rotationZ = prop.transform.eulerAngles.z, localScale = prop.transform.localScale, active = prop.gameObject.activeSelf });
            return data;
        }
        public void RestoreBattleWorld(GameSaveData data)
        {
            if (!SessionInputAllowed || data == null || data.scenePath != SceneManager.GetActiveScene().path ||
                data.actors == null || data.actors.Any(a => a == null || a.battleState != null || a.pointBattleState != null) || !GameSaveStore.IsValidData(data))
                throw new InvalidDataException("战前世界快照无效，无法回退。");
            restoringBattleWorld = true;
            try { RestoreGame(data); playedSeconds = data.playedSeconds; }
            finally { restoringBattleWorld = false; }
            SaveAutomatic("逃跑：恢复战前状态");
        }
        private void RestoreGame(GameSaveData data)
        {
            var players = FindObjectsOfType<PlayerMovement>(true).Where(actor => actor.gameObject.scene == SceneManager.GetActiveScene());
            var identities = players.Where(actor => actor.GetComponent<SaveIdentity>() != null).ToDictionary(actor => actor.GetComponent<SaveIdentity>().Id);
            if (!identities.TryGetValue(data.playerId, out var main)) throw new InvalidDataException("存档中的主角在当前场景中不存在。");
            CancelDialogues();
            foreach (var saved in data.actors)
            {
                if (!identities.TryGetValue(saved.id, out var actor)) continue;
                actor.gameObject.SetActive(saved.active); actor.transform.position = saved.position;
                var body = actor.GetComponent<Rigidbody2D>();
                if (body != null) { body.position = saved.position; body.velocity = Vector2.zero; body.angularVelocity = 0; }
                actor.ResumeKeyboardInput();
                if (!actor.GetComponent<PropGameState>().RestoreSnapshot(saved.propState)) throw new InvalidDataException("背包或道具进度无效。");
                var checks = actor.GetComponent<Emerge.Checks.CheckActorState>();
                if (saved.checkState != null)
                {
                    if (checks == null) checks = actor.gameObject.AddComponent<Emerge.Checks.CheckActorState>();
                    if (!checks.RestoreSnapshot(saved.checkState)) throw new InvalidDataException("检定进度无效。");
                }
                else if (checks != null)
                {
                    // Legacy saves contain no check progress; reset any live/new-scene sessions.
                    checks.RestoreSnapshot(new Emerge.Checks.CheckActorState.Snapshot
                    { attributes = checks.attributes.Clone() });
                }
                var battle = actor.GetComponent<Emerge.Battle.BattleController>();
                if (saved.battleState != null && battle == null) battle = actor.gameObject.AddComponent<Emerge.Battle.BattleController>();
                if (battle != null && !battle.RestoreSnapshot(saved.battleState)) throw new InvalidDataException("战斗进度无效。");
                var pointBattle = actor.GetComponent<Emerge.Battle.PointBattleController>();
                if (saved.pointBattleState != null && pointBattle == null) pointBattle = actor.gameObject.AddComponent<Emerge.Battle.PointBattleController>();
                if (pointBattle != null && !pointBattle.RestoreSnapshot(saved.pointBattleState)) throw new InvalidDataException("点数战斗进度无效。");
            }
            var props = FindObjectsOfType<PropInstance>(true).Where(prop => prop.gameObject.scene == SceneManager.GetActiveScene() && prop.Definition != null).GroupBy(prop => prop.InstanceId).ToDictionary(group => group.Key, group => group.First());
            foreach (var saved in data.props)
            {
                if (!props.TryGetValue(saved.instanceId, out var prop) || prop.Definition.Id != saved.definitionId) continue;
                prop.transform.position = saved.position; prop.transform.rotation = Quaternion.Euler(0, 0, saved.rotationZ);
                prop.transform.localScale = saved.localScale; prop.gameObject.SetActive(saved.active);
            }
            PropInstance.RestoreAll(main.GetComponent<PropGameState>());
            Physics2D.SyncTransforms();
            foreach (var camera in FindObjectsOfType<CameraFollow>()) camera.SnapToTarget();
        }
        private static void CancelDialogues() { foreach (var actor in FindObjectsOfType<PlayerInteractor>(true)) actor.CancelDialogue(); }
        private void Notify(string message) { lastMessage = message; if (menu != null) menu.SetMessage(message); }
        private void OnApplicationFocus(bool focus) { if (!focus && Instance == this && hasStartedGame) SaveAutomatic("失去焦点自动存档"); }
        private void OnApplicationPause(bool pause) { if (pause && Instance == this && hasStartedGame) SaveAutomatic("暂停应用自动存档"); }
        private void OnApplicationQuit() { if (Instance == this && hasStartedGame) SaveAutomatic("关闭应用自动存档"); }
        private void OnDestroy() { if (Instance == this) { Instance = null; Time.timeScale = 1; } }
#if UNITY_EDITOR
        public void UseTestSaveDirectory(string directory)
        {
            if (phase != GameSessionPhase.MainMenu) throw new InvalidOperationException("测试目录只能在主菜单切换。");
            store = new GameSaveStore(directory); if (IsReady) menu.ShowMainMenu();
        }
#endif
    }
}

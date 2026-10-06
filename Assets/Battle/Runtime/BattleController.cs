using System;
using Emerge.GameFlow;
using Emerge.Props;
using UnityEngine;

namespace Emerge.Battle
{
    [DisallowMultipleComponent, RequireComponent(typeof(PropGameState))]
    public sealed class BattleController : MonoBehaviour
    {
        public BattleCatalog catalog;
        [Tooltip("可选的战斗主角外观引用；未指定时沿用战斗演示默认外观。")]
        public Emerge.Characters.CharacterDefinition character;
        public BattleEngine Engine { get; private set; }
        public static BattleController Active { get; private set; }
        public static bool AnyBattleActive => Active != null && Active.Engine?.State != null;
        public BattleView View { get; private set; }
        private float nextStep;
        private BattleReturnPoint returnPoint;
        private bool returning;
        public bool CanFlee => !returning && Engine != null && !Engine.IsCommitting && returnPoint != null && Engine.State?.phase == BattlePhase.Player;
        public event Action Closed;
        private void Awake() { EnsureEngine(); }
        private void EnsureEngine()
        {
            if (catalog == null) catalog = Resources.Load<BattleCatalog>(BattleCatalog.ResourcePath);
            if (Engine?.State == null && catalog != null && catalog.rules.balanceVersion == "v0.5-counterplay")
            {
                var current = Resources.Load<BattleCatalog>(BattleCatalog.ResourcePath);
                if (current != null && current.rules.balanceVersion == BattleSkillTableRules.BalanceVersion) catalog = current;
            }
            if (catalog == null) return;
            if (Engine != null && (Engine.Catalog == catalog || Engine.State != null)) return;
            SetEngine(new BattleEngine(catalog, GetComponent<PropGameState>()));
        }
        private void SetEngine(BattleEngine engine)
        {
            if (Engine != null) Engine.Changed -= OnChanged;
            Engine = engine;
            catalog = engine.Catalog;
            Engine.Changed += OnChanged;
        }
        public bool TryBegin(BattleEncounterDefinition encounter, string contextId = null)
        {
            EnsureEngine();
            if (!GameSessionController.SessionInputAllowed || AnyBattleActive || Engine == null || encounter == null ||
                GetComponent<PlayerInteractor>()?.IsInDialogue == true) return false;
            if (!encounter.repeatable && GetComponent<PropGameState>().HasFlag("battle-won:" + (contextId ?? encounter.id))) return false;
            string context = contextId ?? encounter.id;
            int proposedSeed = encounter.useFixedSeed ? encounter.fixedSeed : unchecked((int)DateTime.UtcNow.Ticks ^ Guid.NewGuid().GetHashCode());
            int seed = GetComponent<PropGameState>().LockBattleSeed(context, encounter.id, proposedSeed);
            var attributes = GetComponent<Emerge.Checks.CheckActorState>()?.attributes;
            if (!Emerge.Checks.SixKinAttributes.IsValidBuild(attributes))
            {
                if (GameSessionController.Instance != null) { Debug.LogWarning("[战斗链] 请先完成五亲 8 点分配。"); return false; }
                attributes = Emerge.Checks.SixKinAttributes.DefaultBuild();
                Debug.Log("[战斗链] 独立演示使用默认五亲 8 点配置。");
            }
            returnPoint = BattleReturnPoint.Capture(this);
            Active = this; ShowView();
            Engine.Start(encounter, seed, contextId, attributes); nextStep = Time.time + .35f; return true;
        }
        private void ShowView()
        {
            if (View == null) View = new GameObject("六爻战斗界面").AddComponent<BattleView>();
            View.Build(this); View.gameObject.SetActive(true);
        }
        private void OnChanged()
        {
            if (Engine.State?.phase == BattlePhase.Victory || Engine.State?.phase == BattlePhase.Defeat) Engine.ApplyOutcome();
            if (View != null) View.Refresh();
        }
        private void Update()
        {
            if (Active != this || Engine?.State == null || !GameSessionController.SessionInputAllowed) return;
            if (Time.time < nextStep) return;
            if (Engine.State.phase == BattlePhase.RoundCasting)
            {
                if (Engine.State.roundDivination.revealedLines < 6) { Engine.RevealLine(); nextStep = Time.time + .22f; }
                else { if (Engine.ResolveRound()) View?.ShowRoundResult(); nextStep = Time.time + .35f; }
            }
            else if (Engine.State.phase == BattlePhase.Casting)
            {
                if (Engine.State.pending.divination.revealedLines < 6) { Engine.RevealLine(); nextStep = Time.time + .3f; }
                else { ResolveSkill(); nextStep = Time.time + .35f; }
            }
            else if (Engine.State.phase == BattlePhase.Enemy)
            {
                int index = Engine.State.enemyCursor;
                if (index < Engine.State.enemies.Count && Engine.State.enemies[index].hp > 0 && !Engine.State.enemies[index].stunned)
                { var enemy = Engine.State.enemies[index]; View?.PlayEnemyAction(index, enemy.intentSkillId); }
                Engine.StepEnemy(); nextStep = Time.time + .75f;
            }
        }
        public void UseSkill(string id, int target)
        { if (GameSessionController.SessionInputAllowed && Engine.CommitSkill(id, target, out _)) { if (Engine.State.version >= 2) View?.ShowSkillResult(Engine.State.lastAction); nextStep = Time.time + .3f; } }
        public void UseItem(string id)
        { if (GameSessionController.SessionInputAllowed && Engine.UseItem(id, out _)) View?.PlayItemAction(id); }
        public void EndTurn() { if (GameSessionController.SessionInputAllowed && Engine.EndTurn()) nextStep = Time.time + .4f; }
        public void QuickCast()
        {
            if (!GameSessionController.SessionInputAllowed || Engine?.State == null) return;
            if (Engine.State.phase == BattlePhase.RoundCasting) { while (Engine.RevealLine()) { } if (Engine.ResolveRound()) View?.ShowRoundResult(); }
            else if (Engine.State.phase == BattlePhase.Casting) { while (Engine.RevealLine()) { } ResolveSkill(); }
        }
        private void ResolveSkill()
        { var action = Engine.State.pending; if (Engine.ResolveSkill()) View?.ShowSkillResult(action); }
        public bool Flee()
        {
            if (!GameSessionController.SessionInputAllowed || !CanFlee || returnPoint.scenePath != gameObject.scene.path) return false;
            var point = returnPoint; returning = true;
            try
            {
                if (point.world != null && GameSessionController.Instance != null) GameSessionController.Instance.RestoreBattleWorld(point.world);
                else { point.RestoreLocal(this); RestoreSnapshot(null); }
            }
            finally { returning = false; }
            Closed?.Invoke(); return true;
        }
        public void CloseResult()
        {
            if (!GameSessionController.SessionInputAllowed || (Engine?.State?.phase != BattlePhase.Victory && Engine?.State?.phase != BattlePhase.Defeat)) return;
            Engine.ApplyOutcome(); Engine.Restore(new BattleSnapshot { catalogPath = Engine.Catalog.SaveResourcePath }); Active = null;
            returnPoint = null;
            if (View != null) View.gameObject.SetActive(false); Closed?.Invoke();
        }
        public BattleSnapshot CaptureSnapshot()
        {
            if (Engine?.State == null) return null;
            if (returning) throw new InvalidOperationException("正在恢复战前状态，请稍后保存");
            var saved = Engine.Capture();
            saved.returnPoint = returnPoint == null ? null : JsonUtility.FromJson<BattleReturnPoint>(JsonUtility.ToJson(returnPoint)); return saved;
        }
        public bool RestoreSnapshot(BattleSnapshot snapshot)
        {
            EnsureEngine();
            var compatibleCatalog = BattleEngine.ResolveSnapshotCatalog(snapshot, Engine?.Catalog);
            if (snapshot != null && (Engine == null || Engine.Catalog.SaveResourcePath != snapshot.catalogPath || (compatibleCatalog != null && compatibleCatalog != Engine.Catalog)))
            {
                if (!BattleCatalog.TryResolveSaveCatalog(snapshot.catalogPath, compatibleCatalog, out var savedCatalog) ||
                    !BattleEngine.ValidateSnapshot(snapshot, savedCatalog)) return false;
                // Prepare and validate the replacement before touching the current battle or UI.
                var restored = new BattleEngine(savedCatalog, GetComponent<PropGameState>());
                if (!restored.Restore(snapshot)) return false;
                SetEngine(restored);
            }
            else
            {
                if (Engine == null) return snapshot == null;
                if (!Engine.Restore(snapshot ?? new BattleSnapshot { catalogPath = Engine.Catalog.SaveResourcePath })) return false;
            }
            returnPoint = snapshot?.returnPoint == null ? null : JsonUtility.FromJson<BattleReturnPoint>(JsonUtility.ToJson(snapshot.returnPoint));
            if (Engine.State != null)
            { Active = this; ShowView(); OnChanged(); nextStep = Time.time + .5f; }
            else { if (Active == this) Active = null; if (View != null) View.gameObject.SetActive(false); }
            return true;
        }
        private void OnDestroy()
        { if (Active == this) Active = null; if (View != null) Destroy(View.gameObject); if (Engine != null) Engine.Changed -= OnChanged; }
    }
}

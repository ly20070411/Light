using System;
using System.Linq;
using Emerge.Checks;
using Emerge.GameFlow;
using Emerge.Props;
using UnityEngine;

namespace Emerge.Battle
{
    [RequireComponent(typeof(CheckActorState)), DisallowMultipleComponent]
    public sealed class PointBattleController : MonoBehaviour
    {
        public static PointBattleController Active { get; private set; }
        public static bool AnyActive => Active != null && Active.Engine?.State != null;
        public PointBattleEngine Engine { get; private set; }
        public BattleView View { get; private set; }
        public PointBattleDefinition rules;
        private BattleReturnPoint returnPoint;
        private float nextStep;
        private bool returning, ownedRules;
        public event Action Closed;
        private void Awake() { Engine = new PointBattleEngine(GetComponent<PropGameState>()); Engine.Changed += Changed; }
        private void Changed()
        {
            if (View != null && View.gameObject.activeSelf && Engine.State != null && View.RenderedEnemyCount == Engine.State.enemies.Count) View.Refresh();
        }
        public bool CanFlee => !returning && Engine.State?.phase == PointBattlePhase.Player &&
            BattleReturnPoint.Validate(returnPoint) && returnPoint.scenePath == gameObject.scene.path;
        public bool TryBegin(BattleEncounterDefinition encounter, string context = null)
        {
            var checks = GetComponent<CheckActorState>();
            if (!GameSessionController.SessionInputAllowed || BattleController.AnyBattleActive || encounter == null || checks.AttributeRulesVersion < 3 ||
                GetComponent<PlayerInteractor>()?.IsInDialogue == true || encounter.enemies == null || encounter.enemies.Length == 0 ||
                encounter.enemies.Any(e => e == null || e.enemy == null)) return false;
            context = context ?? encounter.id;
            var state = GetComponent<PropGameState>();
            if (!encounter.repeatable && state.HasFlag("battle-won:" + context)) return false;
            if (rules == null) rules = Resources.Load<PointBattleDefinition>(PointBattleDefinition.ResourcePath);
            if (rules == null) { rules = ScriptableObject.CreateInstance<PointBattleDefinition>(); ownedRules = true; }
            if (!rules.Valid()) return false;
            var equipment = checks.FreezePointEquipment();
            if (equipment.Any(e => e.item.actionAddons.Any(a => !PointBattleEngine.SupportedAddon(a.effectKey)))) return false;
            int seed = state.LockBattleSeed(context, encounter.id, encounter.useFixedSeed ? encounter.fixedSeed : Guid.NewGuid().GetHashCode());
            var point = BattleReturnPoint.Capture(this);
            var profiles = encounter.enemies.Select((e, i) => rules.Profile(e.enemy, i)).ToList();
            Engine.Start(encounter.id, context, encounter.displayName, encounter.victoryFlag, encounter.swiftVictoryRounds, seed,
                rules, checks.attributes, equipment, checks.EquippedAnchor, profiles, encounter.month, encounter.day);
            returnPoint = point; Active = this;
            GetComponent<PointLoadoutUI>()?.Close(); ShowView(); nextStep = Time.time + .3f; return true;
        }
        private void ShowView()
        {
            if (View != null && View.RenderedEnemyCount != Engine.State.enemies.Count)
            { View.gameObject.SetActive(false); Destroy(View.gameObject); View = null; }
            if (View == null) { View = new GameObject("战斗界面").AddComponent<BattleView>(); View.Build(this); }
            View.gameObject.SetActive(true); View.Refresh();
        }
        private void Update()
        {
            if (Active != this || Engine.State == null || !GameSessionController.SessionInputAllowed || Time.time < nextStep) return;
            if (Engine.State.phase == PointBattlePhase.Casting)
            { if (!Engine.RevealLine()) Engine.ResolveRound(); nextStep = Time.time + .25f; }
            else if (Engine.State.phase == PointBattlePhase.Enemy)
            { View?.PlayPointEnemyAction(Engine.State.enemyCursor); Engine.StepEnemy(); nextStep = Time.time + .55f; }
        }
        public void QuickCast()
        { if (!GameSessionController.SessionInputAllowed) return; while (Engine.RevealLine()) { } Engine.ResolveRound(); }
        public bool Act(CheckBehavior kin, int target, out string error)
        {
            error = "游戏已暂停";
            if (!GameSessionController.SessionInputAllowed || !Engine.Act(kin, target, out error)) return false;
            View?.PlayPointAction(kin, target); return true;
        }
        public void EndTurn()
        { if (GameSessionController.SessionInputAllowed && Engine.EndTurn()) nextStep = Time.time + .25f; }
        public bool UseItem(BattleItemDefinition item, out string error)
        {
            error = "游戏已暂停";
            if (!GameSessionController.SessionInputAllowed || !Engine.UseItem(item, out error)) return false;
            View?.PlayPointItem(item); return true;
        }
        public bool Flee()
        {
            if (returning || !GameSessionController.SessionInputAllowed || Engine.State?.phase != PointBattlePhase.Player ||
                !BattleReturnPoint.Validate(returnPoint) || returnPoint.scenePath != gameObject.scene.path) return false;
            returning = true; var point = returnPoint;
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
            if (!GameSessionController.SessionInputAllowed || Engine.State == null ||
                (Engine.State.phase != PointBattlePhase.Victory && Engine.State.phase != PointBattlePhase.Defeat)) return;
            Engine.ApplyOutcome(); RestoreSnapshot(null); Closed?.Invoke();
        }
        public PointBattleSnapshot CaptureSnapshot()
        {
            if (Engine.State == null) return null;
            var snapshot = Engine.Capture(); snapshot.returnPoint = JsonUtility.FromJson<BattleReturnPoint>(JsonUtility.ToJson(returnPoint)); return snapshot;
        }
        public bool RestoreSnapshot(PointBattleSnapshot snapshot)
        {
            if (!PointBattleEngine.Validate(snapshot)) return false;
            if (snapshot?.session != null && BattleController.Active?.Engine?.State != null) return false;
            if (!Engine.Restore(snapshot)) return false;
            returnPoint = snapshot?.returnPoint == null ? null : JsonUtility.FromJson<BattleReturnPoint>(JsonUtility.ToJson(snapshot.returnPoint));
            if (Engine.State != null) { Active = this; ShowView(); nextStep = Time.time + .3f; }
            else { if (Active == this) Active = null; if (View != null) View.gameObject.SetActive(false); }
            return true;
        }
        private void OnDestroy()
        { if (Active == this) Active = null; if (View != null) Destroy(View.gameObject); if (ownedRules && rules != null) Destroy(rules); }
    }
}

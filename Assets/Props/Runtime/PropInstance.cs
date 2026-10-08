using System;
using System.Collections.Generic;
using Emerge.PixelMap;
using UnityEngine;
using UnityEngine.Events;

namespace Emerge.Props
{
    [Serializable] public sealed class PropInteractionEvent : UnityEvent<PlayerInteractor> { }

    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class PropInstance : MonoBehaviour
    {
        private static readonly HashSet<PropInstance> instances = new HashSet<PropInstance>();
        public static IEnumerable<PropInstance> Instances => instances;
        [SerializeField] private PropDefinition definition;
        [SerializeField] private string instanceId;
        [SerializeField] private MapPlacementMode placementMode;
        [SerializeField] private Vector2Int gridCoordinate;
        [SerializeField, HideInInspector] private Transform visualRoot;
        [SerializeField, HideInInspector] private BoxCollider2D solidCollider;
        public PropInteractionEvent onInteracted = new PropInteractionEvent();
        public PropInteractionEvent onPickedUp = new PropInteractionEvent();
        private SpriteRenderer[] renderers = Array.Empty<SpriteRenderer>();
        private PropSpriteAnimator frameAnimator;
        private Animator animator;
        private PlayerInteractor pendingActor;
        private bool pendingRefresh;
        private bool completed;
        private bool hiddenByState;
        private float nextInteraction;
        public PropDefinition Definition => definition;
        public Emerge.Characters.CharacterDefinition Character => definition != null ? definition.character : null;
        public string InstanceId => instanceId;
        public MapPlacementMode PlacementMode => placementMode;
        public Vector2Int GridCoordinate => gridCoordinate;
        public BoxCollider2D SolidCollider => solidCollider;
        public bool Busy => pendingActor != null;

        internal bool TryRegisterDialogue(PlayerInteractor actor)
        {
            if (actor == null || !isActiveAndEnabled || (pendingActor != null && pendingActor != actor)) return false;
            pendingActor = actor;
            return true;
        }
        internal void ReleaseDialogue(PlayerInteractor actor)
        {
            if (pendingActor == actor) pendingActor = null;
        }

        public void Configure(PropDefinition source, MapPlacementMode mode = MapPlacementMode.Free, Vector2Int coordinate = default)
        {
            definition = source; placementMode = mode; gridCoordinate = coordinate;
            if (string.IsNullOrEmpty(instanceId)) RenewIdentity();
            ApplyDefinition();
        }
        public void RenewIdentity() { instanceId = Guid.NewGuid().ToString("N"); }
        private void OnEnable()
        {
            instances.Add(this);
            if (string.IsNullOrEmpty(instanceId)) RenewIdentity();
            pendingRefresh = true;
        }
        private void OnDisable()
        {
            if (pendingActor != null) pendingActor.CancelDialogueFrom(this);
            pendingActor = null;
        }
        private void OnDestroy() { instances.Remove(this); }
        private void OnValidate() { pendingRefresh = true; }
        private void Update()
        {
            if (pendingRefresh) { pendingRefresh = false; ApplyDefinition(); }
        }
        private void LateUpdate()
        {
            if (definition == null || !definition.sortByY) return;
            int order = Mathf.RoundToInt(-transform.position.y * 32f) + definition.sortingOrder;
            for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].sortingOrder = order + i;
        }

        public void ApplyDefinition(bool recordUndo = false)
        {
            pendingRefresh = false;
            if (visualRoot != null)
            {
                if (Application.isPlaying) { visualRoot.gameObject.SetActive(false); Destroy(visualRoot.gameObject); }
                else
                {
#if UNITY_EDITOR
                    if (recordUndo) UnityEditor.Undo.DestroyObjectImmediate(visualRoot.gameObject);
                    else
#endif
                        DestroyImmediate(visualRoot.gameObject);
                }
                visualRoot = null;
            }
            frameAnimator = null; animator = null; renderers = Array.Empty<SpriteRenderer>();
            if (definition == null)
            { if (solidCollider != null) solidCollider.enabled = false; return; }
            var visual = new GameObject("外观（由道具定义生成）");
            visual.transform.SetParent(transform, false);
            visualRoot = visual.transform;
            if (definition.visualPrefab != null)
            {
                GameObject prefabVisual = null;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    prefabVisual = UnityEditor.PrefabUtility.InstantiatePrefab(definition.visualPrefab) as GameObject;
#endif
                if (prefabVisual == null) prefabVisual = Instantiate(definition.visualPrefab);
                prefabVisual.transform.SetParent(visualRoot, false);
                foreach (var collider in prefabVisual.GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;
                foreach (var body in prefabVisual.GetComponentsInChildren<Rigidbody2D>(true)) body.simulated = false;
            }
            SpriteRenderer primary = visual.GetComponentInChildren<SpriteRenderer>(true);
            if (primary == null) primary = visual.AddComponent<SpriteRenderer>();
            Sprite first = definition.sprite;
            if (definition.visualMode == PropVisualMode.SpriteFrames && definition.idleFrames != null && definition.idleFrames.Length > 0)
                first = definition.idleFrames[0];
            if (first != null || definition.visualPrefab == null) primary.sprite = first;
            if (primary.sprite != null)
            {
                Vector2 sourceSize = primary.sprite.bounds.size;
                visual.transform.localScale = new Vector3(definition.worldSize.x / Mathf.Max(0.01f, sourceSize.x),
                    definition.worldSize.y / Mathf.Max(0.01f, sourceSize.y), 1f);
            }
            renderers = visual.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var renderer in renderers)
            { renderer.color = definition.tint; renderer.sortingLayerName = definition.sortingLayerName; renderer.sortingOrder = definition.sortingOrder; }
            if (definition.visualMode == PropVisualMode.SpriteFrames)
            { frameAnimator = primary.gameObject.AddComponent<PropSpriteAnimator>(); frameAnimator.Configure(primary, definition); }
            if (definition.visualMode == PropVisualMode.Animator)
            {
                animator = visual.GetComponentInChildren<Animator>(true);
                if (animator == null) animator = primary.gameObject.AddComponent<Animator>();
                animator.runtimeAnimatorController = definition.animatorController;
                animator.applyRootMotion = false;
            }
            if (solidCollider == null && definition.isSolid)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying && recordUndo) solidCollider = UnityEditor.Undo.AddComponent<BoxCollider2D>(gameObject);
                else
#endif
                    solidCollider = gameObject.AddComponent<BoxCollider2D>();
            }
            if (solidCollider != null)
            {
                solidCollider.enabled = definition.isSolid;
                solidCollider.isTrigger = false;
                solidCollider.size = definition.colliderSize;
                solidCollider.offset = definition.colliderOffset;
                solidCollider.sharedMaterial = definition.physicsMaterial;
            }
#if UNITY_EDITOR
            if (!Application.isPlaying && recordUndo) UnityEditor.Undo.RegisterCreatedObjectUndo(visual, "更新道具外观");
#endif
        }

        public float DistanceTo(Vector2 position)
        {
            Vector2 point = transform.position;
            if (solidCollider != null && solidCollider.enabled && gameObject.activeInHierarchy) point = solidCollider.ClosestPoint(position);
            return Vector2.Distance(point, position);
        }
        public bool CanInteract(PlayerInteractor actor, out string reason)
        {
            if (!Emerge.GameFlow.GameSessionController.GameplayInputAllowed) { reason = "游戏已暂停"; return false; }
            reason = "";
            if (definition == null || actor == null || !isActiveAndEnabled ||
                (definition.actions == PropActions.None && definition.checkEvent == null && definition.battleEncounter == null))
            { reason = "不可交互"; return false; }
            if (DistanceTo(actor.transform.position) > definition.interactionRange)
            { reason = "距离太远"; return false; }
            if (Busy || actor.IsInDialogue) { reason = "正在对话"; return false; }
            if (completed || actor.State.IsConsumed(instanceId)) { reason = "已交互"; return false; }
            if (Time.time < nextInteraction) { reason = "请稍候"; return false; }
            foreach (var flag in definition.requiredFlags ?? Array.Empty<string>())
                if (!actor.State.HasFlag(flag)) { reason = definition.lockedHint; return false; }
            if (!string.IsNullOrWhiteSpace(definition.requiredItemKey) && actor.State.Count(definition.requiredItemKey) < definition.requiredItemAmount)
            { reason = definition.lockedHint; return false; }
            return true;
        }
        public bool Interact(PlayerInteractor actor)
        {
            if (!CanInteract(actor, out string reason)) { if (actor != null) actor.ShowFeedback(reason); return false; }
            if (frameAnimator != null) frameAnimator.PlayInteraction();
            if (animator != null && !string.IsNullOrEmpty(definition.interactionTrigger))
                foreach (var parameter in animator.parameters)
                    if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == definition.interactionTrigger)
                    { animator.SetTrigger(parameter.nameHash); break; }
            if (definition.battleEncounter != null)
            {
                var battle = actor.GetComponent<Emerge.Battle.BattleController>();
                if (battle == null) battle = actor.gameObject.AddComponent<Emerge.Battle.BattleController>();
                bool started = battle.TryBegin(definition.battleEncounter, instanceId);
                if (!started) actor.ShowFeedback("此战斗已完成或当前无法进入战斗");
                return started;
            }
            if (definition.checkEvent != null)
                return Emerge.Checks.CheckEncounter.TryBegin(this, actor, definition.checkEvent);
            if (definition.HasAction(PropActions.Dialogue) && definition.itemHandover != null && definition.itemHandover.enabled)
                return BeginItemHandover(actor);
            if (definition.HasAction(PropActions.Dialogue) && definition.dialogue.Count > 0)
            {
                pendingActor = actor;
                if (!actor.BeginDialogue(this, definition.dialogue, success =>
                {
                    pendingActor = null;
                    if (success && this != null && isActiveAndEnabled) CompleteInteraction(actor);
                })) { pendingActor = null; return false; }
            }
            else CompleteInteraction(actor);
            return true;
        }
        private bool BeginItemHandover(PlayerInteractor actor)
        {
            var handover = definition.itemHandover;
            bool accepted = false;
            pendingActor = actor;
            Action<bool> finished = success =>
            {
                pendingActor = null;
                if (this == null || !isActiveAndEnabled) return;
                nextInteraction = Time.time + definition.cooldown;
                if (success && accepted) CompleteInteraction(actor, handover);
            };
            bool started;
            if (!string.IsNullOrWhiteSpace(handover.completionFlag) && actor.State.HasFlag(handover.completionFlag))
                started = actor.BeginDialogue(this, LinesOrFallback(handover.completedDialogue, "谢谢你的帮助。"), finished);
            else if (actor.State.Count(handover.itemKey) < handover.amount)
                started = actor.BeginDialogue(this, LinesOrFallback(handover.missingItemDialogue, "你还没有带来需要的物品。"), finished);
            else
                started = actor.BeginChoiceDialogue(this, LinesOrFallback(handover.offerDialogue, "你愿意把物品交给我吗？"),
                    handover.acceptLabel, handover.declineLabel, give =>
                    {
                        accepted = give && actor.State.Count(handover.itemKey) >= handover.amount;
                        actor.ReplaceDialogue(LinesOrFallback(give && !accepted ? handover.missingItemDialogue :
                            (accepted ? handover.acceptedDialogue : handover.declinedDialogue), accepted ? "谢谢你！" : "等你准备好了再来吧。"));
                    }, finished);
            if (!started) pendingActor = null;
            return started;
        }
        private List<PropDialogueLine> LinesOrFallback(List<PropDialogueLine> source, string fallback)
        {
            return source != null && source.Count > 0 ? source : new List<PropDialogueLine>
            { new PropDialogueLine { speaker = definition.DisplayName, text = fallback } };
        }
        private void CompleteInteraction(PlayerInteractor actor, PropItemHandover handover = null)
        {
            if (actor == null || definition == null) return;
            // Requirements may change while a dialogue is open; don't grant rewards twice or consume missing items.
            foreach (var flag in definition.requiredFlags ?? Array.Empty<string>())
                if (!actor.State.HasFlag(flag)) { actor.ShowFeedback(definition.lockedHint); return; }
            if (handover != null)
            {
                if (!string.IsNullOrWhiteSpace(handover.completionFlag) && actor.State.HasFlag(handover.completionFlag)) return;
                int amountNeeded = handover.amount;
                if (definition.consumeRequiredItem && definition.requiredItemKey == handover.itemKey) amountNeeded += definition.requiredItemAmount;
                if (actor.State.Count(handover.itemKey) < amountNeeded)
                { actor.ShowFeedback("所需物品不足，交付未完成"); return; }
            }
            if (!string.IsNullOrWhiteSpace(definition.requiredItemKey))
            {
                if (actor.State.Count(definition.requiredItemKey) < definition.requiredItemAmount)
                { actor.ShowFeedback(definition.lockedHint); return; }
                if (definition.consumeRequiredItem) actor.State.RemoveItem(definition.requiredItemKey, definition.requiredItemAmount);
            }
            if (handover != null)
            {
                if (!actor.State.RemoveItem(handover.itemKey, handover.amount)) return;
                actor.State.SetFlag(handover.completionFlag);
            }
            bool pickup = definition.HasAction(PropActions.Pickup);
            completed = definition.singleUse || pickup;
            nextInteraction = Time.time + definition.cooldown;
            if (completed) actor.State.MarkConsumed(instanceId);
            foreach (var flag in definition.grantedFlags ?? Array.Empty<string>()) actor.State.SetFlag(flag);
            if (pickup)
            {
                actor.State.AddItem(definition.InventoryKey, definition.DisplayName, definition.pickupAmount);
                actor.ShowFeedback("获得 " + definition.DisplayName + " × " + definition.pickupAmount);
                onPickedUp.Invoke(actor);
            }
            else if (definition.HasAction(PropActions.Inspect)) actor.ShowFeedback(definition.description);
            if (!string.IsNullOrWhiteSpace(definition.createdMentalAnchorId))
                actor.GetComponent<Emerge.Checks.CheckActorState>()?.CreateMentalAnchor(definition.createdMentalAnchorId);
            onInteracted.Invoke(actor);
            if (pickup && definition.hideAfterPickup)
            { hiddenByState = true; gameObject.SetActive(false); }
        }
        public void RestoreState(PropGameState state)
        {
            completed = state.IsConsumed(instanceId);
            nextInteraction = 0f;
            if (definition == null) return;
            bool hide = completed && definition.HasAction(PropActions.Pickup) && definition.hideAfterPickup;
            if (hide) { hiddenByState = true; gameObject.SetActive(false); }
            else if (hiddenByState) { hiddenByState = false; gameObject.SetActive(true); }
        }
        public static void RestoreAll(PropGameState state)
        {
            // Snapshot because activation callbacks can alter the set.
            foreach (var prop in new List<PropInstance>(instances)) if (prop != null) prop.RestoreState(state);
        }
    }
}

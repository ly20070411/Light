using System;
using System.Collections.Generic;
using UnityEngine;

namespace Emerge.Props
{
    public enum PropVisualMode { Sprite, SpriteFrames, Animator }
    [Flags] public enum PropActions { None = 0, Dialogue = 1, Pickup = 2, Inspect = 4, Custom = 8 }
    [Serializable] public sealed class PropDialogueLine
    {
        public string speaker;
        public Emerge.Characters.CharacterDefinition character;
        [TextArea(2, 5)] public string text;
        public Sprite portrait;
        public string SpeakerName => !string.IsNullOrWhiteSpace(speaker) ? speaker : character != null ? character.DisplayName : "";
        public Sprite Portrait => portrait != null ? portrait : character != null ? character.portrait : null;
    }

    [Serializable] public sealed class PropDialogueChoice
    {
        public string id;
        public string label;
        public bool enabled = true;
        public string disabledReason;
    }

    [Serializable] public sealed class PropItemHandover
    {
        public bool enabled;
        public string itemKey;
        [Min(1)] public int amount = 1;
        [Tooltip("成功交付后设置；非空时防止重复交付，并使用交付后对话。")]
        public string completionFlag;
        public string acceptLabel = "交给";
        public string declineLabel = "不给";
        public List<PropDialogueLine> offerDialogue = new List<PropDialogueLine>();
        public List<PropDialogueLine> acceptedDialogue = new List<PropDialogueLine>();
        public List<PropDialogueLine> declinedDialogue = new List<PropDialogueLine>();
        public List<PropDialogueLine> missingItemDialogue = new List<PropDialogueLine>();
        public List<PropDialogueLine> completedDialogue = new List<PropDialogueLine>();
    }

    [CreateAssetMenu(fileName = "Prop", menuName = "Emerge/道具/道具定义")]
    public sealed class PropDefinition : ScriptableObject
    {
        [SerializeField] private string id = Guid.NewGuid().ToString("N");
        public string displayName = "新道具";
        public string category = "通用";
        public Emerge.Characters.CharacterDefinition character;
        [TextArea] public string description;
        public PropVisualMode visualMode;
        public Sprite sprite;
        [Tooltip("可选外观预制体；碰撞由道具定义统一管理。动画可驱动该预制体的子节点。")]
        public GameObject visualPrefab;
        public Color tint = Color.white;
        public Vector2 worldSize = Vector2.one;
        public Sprite[] idleFrames = Array.Empty<Sprite>();
        public Sprite[] interactionFrames = Array.Empty<Sprite>();
        [Min(0.1f)] public float framesPerSecond = 6f;
        public bool loop = true;
        public RuntimeAnimatorController animatorController;
        public string interactionTrigger = "Interact";
        public string sortingLayerName = "Default";
        public int sortingOrder;
        public bool sortByY = true;
        public bool allowRotation = true;
        public bool isSolid;
        public Vector2 colliderSize = new Vector2(0.8f, 0.5f);
        public Vector2 colliderOffset = new Vector2(0f, 0.15f);
        public PhysicsMaterial2D physicsMaterial;
        public PropActions actions = PropActions.Inspect;
        [Min(0.1f)] public float interactionRange = 1.5f;
        public string interactionLabel = "交互";
        public bool singleUse;
        [Min(0f)] public float cooldown = 0.3f;
        public List<PropDialogueLine> dialogue = new List<PropDialogueLine>();
        public PropItemHandover itemHandover = new PropItemHandover();
        [Tooltip("指定后优先进入检定事件；行动消耗、奖励和结果由检定事件管理。")]
        public Emerge.Checks.CheckEventDefinition checkEvent;
        public Emerge.Battle.BattleEncounterDefinition battleEncounter;
        [Tooltip("留空时使用道具 ID；同一个物品类型应使用同一个键。")]
        public string inventoryKey;
        [Tooltip("可选：拾取为成长道具。使用此定义的物品键；请同时加入点数道具库。")]
        public Emerge.Checks.PointItemDefinition pointItem;
        [Tooltip("成功完成创建精神锚点事件时发放；lin-xi 或 hydrologist。")]
        public string createdMentalAnchorId;
        [Min(1)] public int pickupAmount = 1;
        public bool hideAfterPickup = true;
        public string[] requiredFlags = Array.Empty<string>();
        public string requiredItemKey;
        [Min(1)] public int requiredItemAmount = 1;
        public bool consumeRequiredItem;
        public string[] grantedFlags = Array.Empty<string>();
        public string lockedHint = "尚未满足交互条件";

        public string Id => id;
        public string DisplayName => character != null ? character.DisplayName : string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string InventoryKey => pointItem != null && pointItem.data != null && !string.IsNullOrWhiteSpace(pointItem.data.key) ? pointItem.data.key : string.IsNullOrWhiteSpace(inventoryKey) ? id : inventoryKey;
        public bool HasAction(PropActions action) => (actions & action) != 0;
        public void RenewIdentity() { id = Guid.NewGuid().ToString("N"); }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(id)) RenewIdentity();
            worldSize = new Vector2(Mathf.Max(0.01f, worldSize.x), Mathf.Max(0.01f, worldSize.y));
            colliderSize = new Vector2(Mathf.Max(0.01f, colliderSize.x), Mathf.Max(0.01f, colliderSize.y));
            interactionRange = Mathf.Max(0.1f, interactionRange);
            framesPerSecond = Mathf.Max(0.1f, framesPerSecond);
            pickupAmount = Mathf.Max(1, pickupAmount);
            requiredItemAmount = Mathf.Max(1, requiredItemAmount);
            cooldown = Mathf.Max(0f, cooldown);
            if (itemHandover != null) itemHandover.amount = Mathf.Max(1, itemHandover.amount);
        }
    }
}

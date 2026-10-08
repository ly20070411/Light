using System;
using System.Collections.Generic;
using System.Linq;
using Emerge.GameFlow;
using UnityEngine;

namespace Emerge.Checks
{
    public sealed partial class CheckActorState
    {
        [SerializeField] private List<PointEquipmentSlot> pointEquipment = new List<PointEquipmentSlot>();
        public PointItemCatalog pointItemCatalog;
        private bool changingEquipment;
        public PointItemCatalog PointItems => pointItemCatalog != null ? pointItemCatalog : Resources.Load<PointItemCatalog>(PointItemCatalog.ResourcePath);
        public IReadOnlyList<PointEquipmentSlot> PointEquipment => pointEquipment.Select(item => new PointEquipmentSlot
            { instanceId = item.instanceId, itemKey = item.itemKey, attribute = item.attribute, instanceData = item.instanceData?.Clone() }).ToArray();
        private void Awake()
        {
            if (GetComponent<PixelPrototype.PlayerMovement>() != null && GetComponent<PointLoadoutUI>() == null)
                gameObject.AddComponent<PointLoadoutUI>();
        }
        public int GrowthPoints(CheckBehavior attribute) => pointEquipment.Count(item => item.attribute == attribute);
        public int OutsidePoints(CheckBehavior attribute) => checked(attributes.Get(attribute) + GrowthPoints(attribute));
        private bool CanConfigurePoints => AttributeRulesVersion == SixKinAttributes.RulesVersion && !changingEquipment &&
            !Emerge.Battle.BattleController.AnyBattleActive && GetComponent<Emerge.Props.PlayerInteractor>()?.IsInDialogue != true;

        public bool TryEquipPointItem(string key, CheckBehavior attribute, out string error)
        {
            SynchronizePointInventory();
            var owned = progression.bag.Find(item => item.item.key == key);
            if (owned == null) { error = "背包中没有这件成长道具。"; return false; }
            return TryEquipPointInstance(owned.instanceId, attribute, out error);
        }
        public bool TryUnequipPointItem(string instanceId, out string error)
        {
            error = "";
            if (!CanConfigureLoadout) { error = "当前不能修改成长道具。"; return false; }
            var slot = pointEquipment.Find(item => item.instanceId == instanceId);
            if (slot == null) { error = "找不到这件配置道具。"; return false; }
            if (PropState.Count(slot.itemKey) == int.MaxValue) { error = "背包数量超出范围。"; return false; }
            changingEquipment = true;
            try
            {
                var definition = PointItems != null ? PointItems.Find(slot.itemKey) : null;
                pointEquipment.Remove(slot);
                var data = slot.instanceData ?? definition?.data;
                if (data != null) progression.bag.Add(new OwnedPointItem { instanceId = slot.instanceId, item = data.Clone() });
                PropState.AddItem(slot.itemKey, definition != null ? definition.data.displayName : slot.itemKey, 1);
                Changed?.Invoke(); return true;
            }
            finally { changingEquipment = false; }
        }
        public bool TryMovePointItem(string instanceId, CheckBehavior attribute, int index, out string error)
        {
            error = "";
            var slot = pointEquipment.Find(item => item.instanceId == instanceId);
            if (!CanConfigureLoadout || slot == null || !Enum.IsDefined(typeof(CheckBehavior), attribute)) { error = "当前不能移动这件成长道具。"; return false; }
            var others = pointEquipment.Where(item => item.attribute == attribute && item != slot).ToList();
            if (index < 0 || index > others.Count) { error = "槽位顺序无效。"; return false; }
            pointEquipment.Remove(slot); slot.attribute = attribute;
            if (index < others.Count) pointEquipment.Insert(pointEquipment.IndexOf(others[index]), slot);
            else if (others.Count > 0) pointEquipment.Insert(pointEquipment.IndexOf(others[others.Count - 1]) + 1, slot);
            else pointEquipment.Add(slot);
            Changed?.Invoke(); return true;
        }
        public List<FrozenPointItem> FreezePointEquipment() => PointCalculation.Freeze(pointEquipment, PointItems);
        public PointCalculationResult PreviewPoints(CheckBehavior attribute, int checkPoints)
            => PointCalculation.Calculate(attributes, attribute, checkPoints, FreezePointEquipment());

        // Pure calculation entry point for story checks. It does not spend AP/MP or mutate combatants.
        public PointCalculationResult CalculateDivinationAction(CheckBehavior attribute, Divination.DivinationRecord record)
        {
            if (!Enum.IsDefined(typeof(CheckBehavior), attribute) || record == null || record.rulesVersion != Divination.DivinationRecord.CurrentRulesVersion || record.revealedLines != 6 || !Divination.CoinCasting.IsValid(record.casting) ||
                !Divination.LiuYaoPaiPan.IsValidResult(record.chart) || record.chart.yueling != record.month || record.chart.richen != record.day ||
                !record.chart.yao6789.SequenceEqual(record.casting.yaoValues)) throw new ArgumentException("需要完整且一致的六爻检定结果。");
            return PreviewPoints(attribute, record.chart.behaviorModifiers[(int)attribute]);
        }
    }
}

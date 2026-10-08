using System;
using System.Collections.Generic;
using System.Linq;
using Emerge.Props;
using UnityEngine;

namespace Emerge.Checks
{
    public static class MentalAnchors
    {
        public const string LinXi = "lin-xi", LuJianshen = "hydrologist";
        public static bool Known(string id) => id == LinXi || id == LuJianshen;
        public static bool ValidEquipped(string id) => string.IsNullOrEmpty(id) || Known(id);
        public static string Name(string id) => id == LinXi ? "林溪的精神锚" : id == LuJianshen ? "鹿见深的精神锚" : "不携带精神锚";
        public static string Description(string id) => id == LinXi ? "兄弟额外施加 floor(x×0.5) 层流蚀，每回合最多两次。流蚀在回合开始扣除等于层数的生命，再减少一层。" :
            id == LuJianshen ? "正检定点数翻倍；检定小于或等于零时，该行动本回合禁用。‘我’被禁用时仍获得固定的 3 AP。" : "";
    }
    [Serializable] public sealed class OwnedPointItem
    {
        public string instanceId;
        public PointItemData item;
    }
    [Serializable] public sealed class PointProgressionState
    {
        public List<OwnedPointItem> bag = new List<OwnedPointItem>();
        public List<string> anchors = new List<string>();
        public string equippedAnchor;
        public int storyDay = 1, modificationsUsed, refreshesUsed;
        public List<PointItemData> offers = new List<PointItemData>();
        public int offerSerial;
        public bool Valid(List<PointEquipmentSlot> equipment)
        {
            if (bag == null || anchors == null || offers == null || bag.Count > 10000 || storyDay < 1 || modificationsUsed < 0 || modificationsUsed > 2 ||
                refreshesUsed < 0 || refreshesUsed > 1 || offerSerial < 0 || (offers.Count != 0 && offers.Count != 3) ||
                anchors.Any(a => !MentalAnchors.Known(a)) || anchors.Distinct().Count() != anchors.Count ||
                !MentalAnchors.ValidEquipped(equippedAnchor) || (!string.IsNullOrEmpty(equippedAnchor) && !anchors.Contains(equippedAnchor))) return false;
            var ids = new HashSet<string>(equipment.Select(e => e.instanceId));
            return bag.All(i => i != null && !string.IsNullOrWhiteSpace(i.instanceId) && ids.Add(i.instanceId) && i.item != null && i.item.IsValid()) &&
                offers.All(i => i != null && i.IsValid());
        }
    }
    public sealed partial class CheckActorState
    {
        [SerializeField] private PointProgressionState progression = new PointProgressionState();
        public PointProgressionState CaptureProgression() => JsonUtility.FromJson<PointProgressionState>(JsonUtility.ToJson(progression));
        public string EquippedAnchor => progression.equippedAnchor;
        public string ConfigurationLocation
        {
            get
            {
                foreach (var zone in FindObjectsOfType<PointConfigurationZone>())
                    if (zone.gameObject.scene == gameObject.scene && zone.Contains(transform.position)) return zone.warehouse ? "仓储区" : "主角卧室";
                var flow = FindObjectOfType<Emerge.Day1.Day1FlowController>();
                return flow != null && flow.actor != null && flow.actor.gameObject == gameObject && flow.CurrentArea == "仓储区" ? "仓储区" : "";
            }
        }
        public bool CanConfigureLoadout => CanConfigurePoints && !string.IsNullOrEmpty(ConfigurationLocation);
        public bool AtTangHui
        {
            get
            {
                if (ConfigurationLocation != "仓储区") return false;
                return PropInstance.Instances.Any(p => p != null && p.gameObject.scene == gameObject.scene && p.isActiveAndEnabled &&
                    p.Character != null && p.Character.Id == "mechanic" && Vector2.Distance(p.transform.position, transform.position) <= 2.5f) ||
                    FindObjectsOfType<PointRefitStation>().Any(p => p.gameObject.scene == gameObject.scene && Vector2.Distance(p.transform.position, transform.position) <= p.range);
            }
        }
        public bool CreateMentalAnchor(string characterId)
        {
            if (!MentalAnchors.Known(characterId)) return false;
            if (!progression.anchors.Contains(characterId)) { progression.anchors.Add(characterId); PropState.SetFlag("anchor.created." + characterId); Changed?.Invoke(); }
            return true;
        }
        public bool TryEquipAnchor(string id, out string error)
        {
            error = "";
            if (!CanConfigureLoadout) { error = "请在主角卧室或仓储区修改精神锚。"; return false; }
            if (!MentalAnchors.ValidEquipped(id) || !string.IsNullOrEmpty(id) && !progression.anchors.Contains(id)) { error = "尚未创建此精神锚。"; return false; }
            progression.equippedAnchor = id; Changed?.Invoke(); return true;
        }
        public bool BeginStoryDay(int day)
        {
            if (day <= progression.storyDay) return day == progression.storyDay;
            if (Emerge.Battle.BattleController.AnyBattleActive) return false;
            progression.storyDay = day; progression.modificationsUsed = 0; progression.refreshesUsed = 0;
            progression.offers.Clear(); Changed?.Invoke(); return true;
        }
        public void OnPointItemAcquired(string key, int amount)
        {
            if (changingEquipment || AttributeRulesVersion < 3) return;
            var definition = PointItems?.Find(key); if (definition == null) return;
            for (int i = 0; i < amount; i++)
                progression.bag.Add(new OwnedPointItem { instanceId = Guid.NewGuid().ToString("N"), item =
                    definition.randomizeOnAcquire ? RandomAffix(definition.data, Guid.NewGuid().GetHashCode()) : definition.data.Clone() });
            Changed?.Invoke();
        }
        public void OnPointItemRemoved(string key, int amount)
        {
            if (changingEquipment) return;
            for (int i = 0; i < amount; i++) { int index = progression.bag.FindIndex(p => p.item.key == key); if (index < 0) break; progression.bag.RemoveAt(index); }
            Changed?.Invoke();
        }
        public void SynchronizePointInventory()
        {
            // Old saves had stack counts but no affixes. Migrate once, never reroll an existing instance.
            foreach (var entry in PropState.Inventory)
            {
                int known = progression.bag.Count(i => i.item.key == entry.key);
                var definition = PointItems?.Find(entry.key);
                if (known >= entry.amount || definition == null || AttributeRulesVersion < 3) continue;
                var ids = new HashSet<string>(progression.bag.Select(i => i.instanceId).Concat(pointEquipment.Select(i => i.instanceId)));
                int ordinal = known;
                for (int i = known; i < entry.amount; i++)
                {
                    string id; byte[] hash;
                    do
                    {
                        string identity = (GetComponent<Emerge.GameFlow.SaveIdentity>()?.Id ?? gameObject.name) + "|" + entry.key + "|" + ordinal++;
                        using (var sha = System.Security.Cryptography.SHA256.Create()) hash = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(identity));
                        id = "migrated-" + BitConverter.ToString(hash, 0, 16).Replace("-", "");
                    } while (!ids.Add(id));
                    progression.bag.Add(new OwnedPointItem { instanceId = id, item = definition.randomizeOnAcquire ?
                        RandomAffix(definition.data, BitConverter.ToInt32(hash, 0)) : definition.data.Clone() });
                }
            }
            foreach (var group in progression.bag.GroupBy(i => i.item.key).ToArray())
                foreach (var extra in group.Skip(PropState.Count(group.Key)).ToArray()) progression.bag.Remove(extra);
        }
        public IReadOnlyList<OwnedPointItem> PointBag { get { SynchronizePointInventory(); return CaptureProgression().bag; } }
        public bool TryEquipPointInstance(string id, CheckBehavior attribute, out string error)
        {
            error = "";
            if (!CanConfigureLoadout || !Enum.IsDefined(typeof(CheckBehavior), attribute)) { error = "请在主角卧室或仓储区配置成长道具。"; return false; }
            SynchronizePointInventory(); var owned = progression.bag.Find(i => i.instanceId == id);
            if (owned == null || pointEquipment.Count >= 1024) { error = "背包中找不到这件道具。"; return false; }
            changingEquipment = true;
            try
            {
                if (!PropState.RemoveItem(owned.item.key, 1)) { error = "背包数量不足。"; return false; }
                progression.bag.Remove(owned);
                pointEquipment.Add(new PointEquipmentSlot { instanceId = owned.instanceId, itemKey = owned.item.key, attribute = attribute, instanceData = owned.item.Clone() });
                Changed?.Invoke(); return true;
            }
            finally { changingEquipment = false; }
        }
        public bool EnsureRefitOffers(out string error)
        {
            error = "";
            if (!CanConfigureLoadout || !AtTangHui) { error = "请到仓储区唐晦身旁改装。"; return false; }
            if (progression.modificationsUsed >= 2) { error = "今天的两次改装机会已用完。"; return false; }
            if (progression.offers.Count == 0) GenerateOffers();
            return true;
        }
        private void GenerateOffers()
        {
            var pool = PointAffixes.Pool(); var random = new System.Random(Guid.NewGuid().GetHashCode());
            progression.offers = pool.OrderBy(_ => random.Next()).Take(3).Select(a => a.Clone()).ToList();
            progression.offerSerial++; Changed?.Invoke();
        }
        public bool RefreshRefitOffers(out string error)
        {
            if (!EnsureRefitOffers(out error)) return false;
            if (progression.refreshesUsed >= 1) { error = "今天的刷新机会已用完。"; return false; }
            progression.refreshesUsed++; GenerateOffers(); return true;
        }
        public bool RefitPointItem(string instanceId, int offerIndex, out string error)
        {
            if (!EnsureRefitOffers(out error)) return false;
            if (offerIndex < 0 || offerIndex >= progression.offers.Count) { error = "请选择有效的词条。"; return false; }
            SynchronizePointInventory();
            var bag = progression.bag.Find(i => i.instanceId == instanceId);
            var slot = pointEquipment.Find(i => i.instanceId == instanceId);
            var original = bag?.item ?? slot?.instanceData ?? (slot == null ? null : PointItems?.Find(slot.itemKey)?.data);
            if (original == null) { error = "找不到待改装道具。"; return false; }
            var updated = ApplyAffix(original, progression.offers[offerIndex]);
            if (bag != null) bag.item = updated; else slot.instanceData = updated;
            progression.modificationsUsed++; progression.offers.Clear(); Changed?.Invoke(); return true;
        }
        private static PointItemData RandomAffix(PointItemData item, int seed)
        { var pool = PointAffixes.Pool(); return ApplyAffix(item, pool[new System.Random(seed).Next(pool.Count)]); }
        private static PointItemData ApplyAffix(PointItemData item, PointItemData affix)
        { var result = affix.Clone(); result.key = item.key; result.displayName = item.displayName; return result; }
    }
    public static class PointAffixes
    {
        public static List<PointItemData> Pool() => new List<PointItemData> {
            new PointItemData { key = "affix.steady", displayName = "稳固", description = "当前点数低于局外点数时，恢复至局外点数。",
                modifiers = new List<PointModifierRule> { new PointModifierRule { comparison = PointComparison.Less, operation = PointOperation.Set,
                    operand = new PointOperand { source = PointSource.Outside } } } },
            new PointItemData { key = "affix.focus", displayName = "聚焦", description = "执行到此道具时，当前点数 +2。",
                modifiers = new List<PointModifierRule> { new PointModifierRule { operation = PointOperation.Add, operand = new PointOperand { constant = 2 } } } },
            Addon("shield", "护佑", "使用配置行动时额外获得 2 护盾。", 2),
            Addon("heal", "生息", "使用配置行动时恢复 1 生命。", 1),
            Addon("corrosion", "侵蚀", "使用配置行动时，为目标增加 1 层流蚀。", 1),
            Addon("damage", "追击", "使用配置行动时，对目标额外造成 1 基础伤害。", 1)
        };
        private static PointItemData Addon(string key, string name, string text, int amount) => new PointItemData { key = "affix." + key,
            displayName = name, description = text, actionAddons = new List<PointActionAddon> {
                new PointActionAddon { effectKey = key, description = text, amount = new PointOperand { constant = amount } } } };
    }
}

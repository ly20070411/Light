using System.Linq;
using UnityEngine;

namespace Emerge.Checks
{
    public sealed partial class PointLoadoutUI
    {
        private int loadoutPage;
        private string refitItem;
        private Vector2 progressionScroll;
        public void SelectPage(int page)
        { if (page < 0 || page > 2) throw new System.ArgumentOutOfRangeException(nameof(page)); loadoutPage = page; }
        private void DrawProgressionPage()
        {
            var state = actor.CaptureProgression();
            GUILayout.BeginArea(new Rect(120, 104, 1030, 510));
            Label(actor.CanConfigureLoadout ? "当前位置：" + actor.ConfigurationLocation : "请在主角卧室或仓储区修改配置。", 980);
            progressionScroll = GUILayout.BeginScrollView(progressionScroll);
            if (loadoutPage == 1)
            {
                Label("当前携带：" + MentalAnchors.Name(state.equippedAnchor), 960);
                GUI.enabled = actor.CanConfigureLoadout;
                if (GUILayout.Button("卸下精神锚", GUILayout.Height(34))) actor.TryEquipAnchor(null, out message);
                GUI.enabled = true;
                foreach (string id in state.anchors)
                {
                    Label(MentalAnchors.Name(id) + "\n" + MentalAnchors.Description(id), 960);
                    GUI.enabled = actor.CanConfigureLoadout && state.equippedAnchor != id;
                    if (GUILayout.Button("携带 " + MentalAnchors.Name(id), GUILayout.Height(34))) actor.TryEquipAnchor(id, out message);
                    GUI.enabled = true;
                }
                if (state.anchors.Count == 0) Label("完成角色的‘创建精神锚点’剧情事件后获得被动。", 960);
            }
            else
            {
                Label("唐晦 · 剧情第 " + state.storyDay + " 天 · 改装剩余 " + (2 - state.modificationsUsed) + "/2 · 刷新剩余 " + (1 - state.refreshesUsed) + "/1", 960);
                Label("选择一件道具，再从三个随机词条中选一个替换原效果。成长 +1 保留。", 960);
                foreach (var item in actor.PointBag)
                    if (GUILayout.Button((refitItem == item.instanceId ? "✓ " : "") + "背包 · " + item.item.displayName + " · " + item.item.description, GUILayout.Height(42))) refitItem = item.instanceId;
                foreach (var slot in actor.PointEquipment)
                {
                    var item = slot.instanceData ?? actor.PointItems?.Find(slot.itemKey)?.data;
                    if (item != null && GUILayout.Button((refitItem == slot.instanceId ? "✓ " : "") + SixKinAttributes.Get(slot.attribute).name + " · " + item.displayName + " · " + item.description, GUILayout.Height(42))) refitItem = slot.instanceId;
                }
                GUI.enabled = actor.CanConfigureLoadout && actor.AtTangHui && state.modificationsUsed < 2;
                if (GUILayout.Button("查看唐晦提供的三个效果", GUILayout.Height(35))) actor.EnsureRefitOffers(out message);
                GUI.enabled = actor.CanConfigureLoadout && actor.AtTangHui && state.refreshesUsed < 1 && state.modificationsUsed < 2;
                if (GUILayout.Button("刷新效果（每天一次）", GUILayout.Height(35))) actor.RefreshRefitOffers(out message);
                GUI.enabled = true;
                for (int i = 0; i < state.offers.Count; i++)
                {
                    GUI.enabled = actor.CanConfigureLoadout && actor.AtTangHui && !string.IsNullOrEmpty(refitItem) && state.modificationsUsed < 2;
                    if (GUILayout.Button(state.offers[i].displayName + " · " + state.offers[i].description, GUILayout.Height(46))) actor.RefitPointItem(refitItem, i, out message);
                    GUI.enabled = true;
                }
                if (!actor.AtTangHui) Label("改装需要前往仓储区的唐晦身旁。", 960);
            }
            GUILayout.EndScrollView(); GUILayout.EndArea();
            GUI.Label(new Rect(120, 635, 1030, 30), message ?? "精神锚只能携带一个；改装次数按剧情日期重置。");
        }
    }
}

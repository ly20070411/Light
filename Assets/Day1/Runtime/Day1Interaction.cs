using Emerge.Props;
using UnityEngine;

namespace Emerge.Day1
{
    [DisallowMultipleComponent, RequireComponent(typeof(PropInstance))]
    public sealed class Day1Interaction : MonoBehaviour
    {
        public string key;
        public Day1FlowController flow;
        [Tooltip("宣读规则后角色回到这里；与临时集合位置分开保存。")]
        public Vector3 homePosition;
        public PropInstance Prop => GetComponent<PropInstance>();
        private void OnEnable() { Prop.onInteracted.AddListener(OnInteracted); }
        private void OnDisable() { Prop.onInteracted.RemoveListener(OnInteracted); }
        private void OnInteracted(PlayerInteractor actor) { if (flow != null) flow.HandleInteraction(this, actor); }
    }
}

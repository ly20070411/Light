using System.Collections.Generic;
using UnityEngine;

namespace Emerge.Props
{
    [CreateAssetMenu(fileName = "PropLibrary", menuName = "Emerge/道具/道具库")]
    public sealed class PropLibrary : ScriptableObject
    {
        [SerializeField] private List<PropDefinition> props = new List<PropDefinition>();
        public IReadOnlyList<PropDefinition> Props => props;
        public bool Add(PropDefinition definition)
        {
            if (definition == null || props.Contains(definition)) return false;
            props.Add(definition);
            return true;
        }
        public bool Remove(PropDefinition definition) => definition != null && props.Remove(definition);
        public PropDefinition Find(string id) => props.Find(item => item != null && item.Id == id);
        private void OnValidate() { props.RemoveAll(item => item == null); }
    }
}

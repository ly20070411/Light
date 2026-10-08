using System;
using System.Collections.Generic;
using UnityEngine;

namespace Emerge.Checks
{
    [CreateAssetMenu(menuName = "Emerge/点数系统/成长道具库", fileName = "PointItemCatalog")]
    public sealed class PointItemCatalog : ScriptableObject
    {
        public const string ResourcePath = "Checks/PointItemCatalog";
        public List<PointItemDefinition> items = new List<PointItemDefinition>();
        public PointItemDefinition Find(string key) => items.Find(item => item != null && item.data != null && item.data.key == key);
        public bool IsValid()
        {
            if (items == null) return false;
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in items) if (item == null || item.data == null || !item.data.IsValid() || !keys.Add(item.data.key)) return false;
            return true;
        }
    }
}

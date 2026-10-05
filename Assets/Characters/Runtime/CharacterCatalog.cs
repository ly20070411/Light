using System;
using System.Collections.Generic;
using UnityEngine;

namespace Emerge.Characters
{
    [CreateAssetMenu(fileName = "CharacterCatalog", menuName = "Emerge/角色/角色库")]
    public sealed class CharacterCatalog : ScriptableObject
    {
        public const string ResourcePath = "Characters/CharacterCatalog";
        [SerializeField] private List<CharacterDefinition> characters = new List<CharacterDefinition>();
        public IReadOnlyList<CharacterDefinition> Characters => characters;
        public static CharacterCatalog LoadDefault() => Resources.Load<CharacterCatalog>(ResourcePath);
        public CharacterDefinition Find(string id) => string.IsNullOrWhiteSpace(id) ? null :
            characters.Find(item => item != null && string.Equals(item.Id, id, StringComparison.Ordinal));

        public bool Add(CharacterDefinition character)
        {
            if (character == null || characters.Contains(character)) return false;
            if (Find(character.Id) != null) throw new InvalidOperationException("角色 ID 重复：" + character.Id);
            characters.Add(character);
            return true;
        }
    }
}

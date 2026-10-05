using System;
using UnityEngine;

namespace Emerge.Characters
{
    public enum CharacterGender { Male, Female }
    public enum CharacterRole { Player, Main, Supporting, Npc }
    public enum CharacterRank { HeavenlyEye, HexagramBearer, HexagramLord }
    public enum CharacterTeam { CurrentSurvey, PreviousSurvey }

    [CreateAssetMenu(fileName = "Character", menuName = "Emerge/角色/角色定义")]
    public sealed class CharacterDefinition : ScriptableObject
    {
        [SerializeField] private string id = Guid.NewGuid().ToString("N");
        public string characterName;
        public string roleLabel;
        public CharacterGender gender;
        public CharacterRole narrativeRole;
        public CharacterTeam team;
        public string faction;
        public string department;
        public string title;
        public string function;
        public string hexagram;
        public CharacterRank rank;
        public string abilityName;
        [TextArea(3, 8)] public string abilityDescription;
        [TextArea(3, 10)] public string background;
        [TextArea(3, 8)] public string appearanceNotes;
        public string dayOneLocation;
        [TextArea] public string dayOneActivity;
        [TextArea] public string dayOneTask;
        [TextArea(2, 6)] public string planningNotes;
        public Sprite mapSprite;
        public Sprite portrait;
        public GameObject prefab;
        public Emerge.Props.PropDefinition mapProp;

        public string Id => id;
        public string DisplayName => !string.IsNullOrWhiteSpace(characterName) ? characterName :
            !string.IsNullOrWhiteSpace(roleLabel) ? roleLabel : name;
        public bool IsNamePending => string.IsNullOrWhiteSpace(characterName);

        public GameObject Spawn(Vector3 position, Transform parent = null)
        {
            if (prefab == null) throw new InvalidOperationException("角色缺少预制体：" + DisplayName);
            var instance = Instantiate(prefab, position, Quaternion.identity, parent);
            instance.name = DisplayName;
            var prop = instance.GetComponent<Emerge.Props.PropInstance>();
            if (prop != null) prop.RenewIdentity();
            return instance;
        }

        // Assigned once when creating an asset; changing a name never changes its story key.
        public void InitializeIdentity(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("角色 ID 不能为空。", nameof(value));
            id = value;
        }
    }

    public static class CharacterIds
    {
        public const string HuanYujian = "huan-yujian";
        public const string LinXi = "lin-xi";
        public const string Hydrologist = "hydrologist";
        public const string Geologist = "geologist";
        public const string YangYinglong = "yang-yinglong";
        public const string ContainmentResearcher = "containment-researcher";
        public const string Mechanic = "mechanic";
        public const string TanYue = "tan-yue";
    }
}

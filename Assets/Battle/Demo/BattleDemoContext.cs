using Emerge.GameFlow;
using Emerge.Props;
using UnityEngine;

namespace Emerge.Battle.Demo
{
    public sealed class BattleDemoContext : MonoBehaviour
    {
        public BattleEncounterDefinition encounter;
        public BattleController player;
        private bool seeded;
        private GUIStyle title, normal, button;
        private void Start()
        { if (GameSessionController.Instance != null) GameSessionController.Instance.gameScenePath = gameObject.scene.path; }
        private void Update()
        {
            if (seeded || player == null || !GameSessionController.SessionInputAllowed) return;
            var state = player.GetComponent<PropGameState>();
            if (!state.HasFlag("battle-demo-kit-v1"))
            {
                // Mark first: returning to this scene never replenishes a spent kit.
                state.SetFlag("battle-demo-kit-v1");
                state.AddItem("ITEM_MED", "医疗包", 2); state.AddItem("ITEM_MP", "凝神剂", 1);
                state.AddItem("ITEM_CLEAN", "驱秽符", 1); state.AddItem("ITEM_BLAST", "散灵符", 1);
            }
            seeded = true;
        }
        private void OnGUI()
        {
            if (!GameSessionController.SessionInputAllowed || BattleController.AnyBattleActive || player == null || encounter == null) return;
            if (title == null)
            {
                var font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Arial" }, 24);
                title = new GUIStyle(GUI.skin.label) { font = font, fontSize = 26, wordWrap = true };
                normal = new GUIStyle(GUI.skin.label) { font = font, fontSize = 18, wordWrap = true };
                button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 22 };
            }
            float w = Mathf.Min(580, Screen.width - 40); GUI.Box(new Rect(20, 20, w, 280), "");
            GUI.Label(new Rect(40, 35, w - 40, 42), "六爻战斗测试 · " + encounter.displayName, title);
            GUI.Label(new Rect(40, 90, w - 40, 105), encounter.description + "\nWASD 移动，靠近终端按 E，或点击下方开始。\n初始物品仅发放一次；Esc 可暂停、保存和读取。", normal);
            if (GUI.Button(new Rect(40, 218, w - 40, 54), "开始战斗", button)) player.TryBegin(encounter);
        }
    }
}

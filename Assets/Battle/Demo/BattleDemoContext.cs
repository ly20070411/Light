using Emerge.GameFlow;
using Emerge.Checks;
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
            var checks = player.GetComponent<CheckActorState>() ?? player.gameObject.AddComponent<CheckActorState>();
            // Standalone scene runs use the current build. Loading an existing character keeps its rules.
            if (GameSessionController.Instance == null && checks.AttributeRulesVersion == 0)
                checks.TrySetAllocatedAttributes(SixKinAttributes.DefaultBuild());
            var state = player.GetComponent<PropGameState>();
            if (!state.HasFlag("battle-demo-kit-v1"))
            {
                // Mark first: returning to this scene never replenishes a spent kit.
                state.SetFlag("battle-demo-kit-v1");
                state.AddItem("ITEM_MED", "医疗包", 2); state.AddItem("ITEM_MP", "凝神剂", 1);
                state.AddItem("ITEM_CLEAN", "驱秽符", 1); state.AddItem("ITEM_BLAST", "散灵符", 1);
            }
            if (checks.AttributeRulesVersion >= 3 && !state.HasFlag("battle-demo-point-kit-v1"))
            {
                state.SetFlag("battle-demo-point-kit-v1");
                state.AddItem("points.sword", "剑", 1); state.AddItem("points.talisman", "符咒", 1);
                state.AddItem("day1.identity-card", "身份认证卡", 1);
                checks.CreateMentalAnchor(MentalAnchors.LinXi); checks.CreateMentalAnchor(MentalAnchors.LuJianshen);
            }
            seeded = true;
        }
        private void OnGUI()
        {
            if (!GameSessionController.SessionInputAllowed || BattleController.AnyBattleActive || PointLoadoutUI.AnyOpen || player == null || encounter == null) return;
            if (title == null)
            {
                var font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Arial" }, 24);
                title = new GUIStyle(GUI.skin.label) { font = font, fontSize = 26, wordWrap = true };
                normal = new GUIStyle(GUI.skin.label) { font = font, fontSize = 18, wordWrap = true };
                button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 22 };
            }
            bool points = player.GetComponent<CheckActorState>()?.AttributeRulesVersion >= 3;
            float w = Mathf.Min(580, Screen.width - 40); GUI.Box(new Rect(20, 20, w, points ? 354 : 280), "");
            GUI.Label(new Rect(40, 35, w - 40, 42), "六爻战斗测试 · " + encounter.displayName, title);
            string description = points ? "点数战斗 · " + encounter.enemies.Length + " 个敌人\n每轮定卦：3 + 我最终点数 AP，每次行动 2 AP。\nP：装备顺序 / 精神锚 / 唐晦改装；本测试区域可配置。\nWASD 移动，靠近终端按 E；Esc 暂停与存读档。" :
                encounter.description + "\nWASD 移动，靠近终端按 E，或点击下方开始。\n初始物品仅发放一次；Esc 可暂停、保存和读取。";
            GUI.Label(new Rect(40, 90, w - 40, 120), description, normal);
            if (GUI.Button(new Rect(40, 218, w - 40, 54), "开始战斗", button)) player.TryBegin(encounter);
            if (points && GUI.Button(new Rect(40, 286, w - 40, 54), "装备 / 精神锚 / 改装", button)) player.GetComponent<PointLoadoutUI>()?.Show();
        }
    }
}

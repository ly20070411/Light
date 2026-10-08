using System.Collections;
using System.Linq;
using Emerge.Checks;
using Emerge.GameFlow;
using Emerge.Props;
using UnityEngine;

namespace Emerge.Battle
{
    public sealed class PointBattleDemo : MonoBehaviour
    {
        public GameObject player;
        public BattleEncounterDefinition[] encounters;
        private CheckActorState checks;
        private IEnumerator Start()
        {
            if (GameSessionController.Instance != null) GameSessionController.Instance.gameScenePath = gameObject.scene.path;
            yield return new WaitUntil(() => GameSessionController.Instance == null || GameSessionController.Instance.Phase == GameSessionPhase.Playing);
            checks = player.GetComponent<CheckActorState>();
            if (GameSessionController.Instance == null) checks.TrySetAllocatedAttributes(SixKinAttributes.DefaultBuild());
            var state = player.GetComponent<PropGameState>();
            if (!state.HasFlag("point-battle-demo-granted"))
            {
                state.AddItem("points.sword", "剑", 1); state.AddItem("points.talisman", "符咒", 1);
                state.AddItem("day1.identity-card", "身份认证卡", 1);
                checks.CreateMentalAnchor(MentalAnchors.LinXi); checks.CreateMentalAnchor(MentalAnchors.LuJianshen);
                state.SetFlag("point-battle-demo-granted");
            }
        }
        private void OnGUI()
        {
            if (checks == null || !GameSessionController.GameplayInputAllowed) return;
            var old = GUI.matrix; float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            GUILayout.BeginArea(new Rect(260, 55, 760, 610), GUI.skin.box);
            GUILayout.Label("点数战斗测试 · 仓储区唐晦身旁（占位）");
            GUILayout.Label("P：装备顺序 / 精神锚 / 改装。每轮 3 + 我最终点数 AP，行动 2 AP。\n三个遭遇均不额外赠送消耗品。切换精神锚后对比同一敌方意图。 ");
            foreach (var encounter in encounters.Where(e => e != null))
                if (GUILayout.Button(encounter.displayName + "（" + encounter.enemies.Length + " 敌）", GUILayout.Height(44)))
                {
                    checks.GetComponent<PointLoadoutUI>()?.Close();
                    var controller = player.GetComponent<BattleController>() ?? player.AddComponent<BattleController>();
                    controller.TryBegin(encounter, "point-demo:" + encounter.id);
                }
            if (GUILayout.Button("打开装备 / 精神锚 / 改装", GUILayout.Height(44))) checks.GetComponent<PointLoadoutUI>().Show();
            GUILayout.EndArea(); GUI.matrix = old;
        }
    }
}

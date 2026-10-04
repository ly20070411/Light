using Emerge.GameFlow;
using Emerge.Props;
using UnityEngine;

namespace Emerge.Checks.Demo
{
    // This scene owns its new-game destination without changing the shared menu prefab.
    [DefaultExecutionOrder(-1000)]
    public sealed class CheckDemoContext : MonoBehaviour
    {
        public const string ScenePath = "Assets/Scenes/ChecksDemo.unity";
        private void Awake()
        {
            if (gameObject.scene.path == ScenePath && GameSessionController.Instance != null)
                GameSessionController.Instance.gameScenePath = ScenePath;
        }

        private void OnGUI()
        {
            if (!GameSessionController.GameplayInputAllowed) return;
            var style = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 15 };
            var title = new GUIStyle(style) { fontStyle = FontStyle.Bold, fontSize = 18 };
            float width = Mathf.Min(510f, Screen.width - 32f);
            GUILayout.BeginArea(new Rect(16, 16, width, 98), GUI.skin.box);
            GUILayout.Label("观测站：失效的隔离门", title);
            GUILayout.Label("靠近终端按 E，先投掷铜币起卦，再按 1 / 2 / 3 选择行动。左侧可拾取维修包。", style);
            var actor = FindObjectOfType<CheckActorState>();
            if (actor != null)
            {
                var backpack = actor.GetComponent<PropGameState>();
                string progress = backpack != null && backpack.HasFlag("check-gate-open") ? "大门已开启" :
                    backpack != null && backpack.HasFlag("check-side-route") ? "已发现侧路" : "等待处理隔离门";
                GUILayout.Label(progress + "  ·  侵染 " + actor.Contamination, style);
            }
            GUILayout.EndArea();
        }
    }
}

using System.Collections;
using Emerge.GameFlow;
using Emerge.Props;
using UnityEngine;

namespace Emerge.Checks
{
    public sealed class PointSystemDemo : MonoBehaviour
    {
        public GameObject player;
        private void Awake()
        {
            var zone = gameObject.AddComponent<PointConfigurationZone>(); zone.size = new Vector2(100, 100);
            if (GameSessionController.Instance != null) GameSessionController.Instance.gameScenePath = "Assets/Scenes/PointSystemDemo.unity";
        }
        private IEnumerator Start()
        {
            yield return new WaitUntil(() => GameSessionController.Instance == null || GameSessionController.Instance.Phase == GameSessionPhase.Playing);
            if (player == null) yield break;
            var checks = player.GetComponent<CheckActorState>();
            if (GameSessionController.Instance == null) checks.TrySetAllocatedAttributes(SixKinAttributes.DefaultBuild());
            var state = player.GetComponent<PropGameState>();
            if (!state.HasFlag("points-demo-granted"))
            {
                state.AddItem("points.sword", "剑", 1); state.AddItem("points.talisman", "符咒", 1); state.AddItem("points.knot", "平安结", 1);
                state.SetFlag("points-demo-granted");
            }
            checks.GetComponent<PointLoadoutUI>()?.Show();
        }
    }
}

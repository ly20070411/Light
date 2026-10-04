using UnityEngine;

namespace Emerge.GameFlow
{
    public static class GameFlowBootstrap
    {
        public const string ResourcePath = "GameFlow/GameFlow";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void StartGameFlow()
        {
#if UNITY_EDITOR
            if (UnityEditor.SessionState.GetBool("Light.GameFlow.SuppressForValidation", false)) return;
#endif
            if (GameSessionController.Instance != null || Object.FindObjectOfType<GameSessionController>(true) != null) return;
            var prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning("[GameFlow] 尚未安装菜单与存档，请运行 Tools > 菜单与存档 > 安装或更新。");
                return;
            }
            var instance = Object.Instantiate(prefab);
            instance.name = "Game Flow";
            Object.DontDestroyOnLoad(instance);
        }
    }
}

using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PixelPrototype.Editor
{
    [InitializeOnLoad]
    public static class PrototypeValidation
    {
        private const string Pending = "PixelPrototype.Validation.Pending";
        private const string ResultCode = "PixelPrototype.Validation.ResultCode";
        private static double started;

        static PrototypeValidation()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += WatchTimeout;
        }

        [MenuItem("Pixel Prototype/Run PlayMode Validation")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before running validation.");
            EditorSceneManager.OpenScene(PrototypeBootstrap.ScenePath);
            SessionState.SetBool(Pending, true);
            SessionState.SetInt(ResultCode, 2);
            started = EditorApplication.timeSinceStartup;
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                started = EditorApplication.timeSinceStartup;
                PrototypeSelfTest.OutputPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Validation/playmode-results.json"));
                PrototypeSelfTest.Completed -= OnCompleted;
                PrototypeSelfTest.Completed += OnCompleted;
                new GameObject("Validation runner (temporary)").AddComponent<PrototypeSelfTest>();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                int result = SessionState.GetInt(ResultCode, 2);
                SessionState.SetBool(Pending, false);
                if (Application.isBatchMode) EditorApplication.Exit(result);
            }
        }

        private static void OnCompleted(bool passed)
        {
            SessionState.SetInt(ResultCode, passed ? 0 : 1);
            EditorApplication.isPlaying = false;
        }

        private static void WatchTimeout()
        {
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying) return;
            if (started <= 0) started = EditorApplication.timeSinceStartup;
            if (EditorApplication.timeSinceStartup - started < 90d) return;
            Debug.LogError("Pixel Prototype PlayMode validation timed out.");
            SessionState.SetInt(ResultCode, 2);
            EditorApplication.isPlaying = false;
        }
    }
}

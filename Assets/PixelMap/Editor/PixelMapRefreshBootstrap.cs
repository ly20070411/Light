using UnityEditor;

namespace Emerge.PixelMap.Editor
{
    [InitializeOnLoad]
    internal static class PixelMapRefreshBootstrap
    {
        static PixelMapRefreshBootstrap()
        {
            EditorApplication.update += RefreshOnNextEditorUpdate;
        }

        private static void RefreshOnNextEditorUpdate()
        {
            EditorApplication.update -= RefreshOnNextEditorUpdate;
            AssetDatabase.Refresh();
        }
    }
}
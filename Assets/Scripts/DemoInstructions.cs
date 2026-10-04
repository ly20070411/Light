using UnityEngine;

namespace PixelPrototype
{
    public sealed class DemoInstructions : MonoBehaviour
    {
        private GUIStyle heading;
        private GUIStyle label;

        private void OnGUI()
        {
            if (!Emerge.GameFlow.GameSessionController.GameplayInputAllowed) return;
            if (heading == null)
            {
                heading = new GUIStyle(GUI.skin.label) { fontSize = 23, fontStyle = FontStyle.Bold };
                heading.normal.textColor = new Color(0.89f, 0.94f, 1f);
                label = new GUIStyle(GUI.skin.label) { fontSize = 16 };
                label.normal.textColor = new Color(0.78f, 0.86f, 0.95f);
            }
            GUI.Box(new Rect(16, 16, 400, 94), GUIContent.none);
            GUI.Label(new Rect(30, 23, 380, 34), "PIXEL ROOM / MOVEMENT STUDY", heading);
            GUI.Label(new Rect(30, 61, 380, 23), "WASD / arrows: move   |   Hold Shift: sprint", label);
            GUI.Label(new Rect(30, 84, 380, 23), "Camera follows directly   |   Walls block your feet", label);
        }
    }
}

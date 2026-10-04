using UnityEngine;

namespace Emerge.GameFlow
{
    // Assigned by the installer and stored in the scene, so IDs survive scene reloads.
    [DisallowMultipleComponent]
    public sealed class SaveIdentity : MonoBehaviour
    {
        [SerializeField] private string id;
        public string Id => id;
        public void AssignId(string value) { id = value; }
    }
}

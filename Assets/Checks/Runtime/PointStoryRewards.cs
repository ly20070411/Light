using UnityEngine;
namespace Emerge.Checks
{
    // Connect successful story-event completion, rather than dialogue opening, to these methods.
    public sealed class PointStoryRewards : MonoBehaviour
    {
        public CheckActorState actor;
        public void CreateLinXiAnchor() => actor?.CreateMentalAnchor(MentalAnchors.LinXi);
        public void CreateLuJianshenAnchor() => actor?.CreateMentalAnchor(MentalAnchors.LuJianshen);
        public void BeginStoryDay(int day) => actor?.BeginStoryDay(day);
    }
}

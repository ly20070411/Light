using UnityEngine;

namespace Emerge.Props
{
    public sealed class PropSpriteAnimator : MonoBehaviour
    {
        private SpriteRenderer target;
        private PropDefinition definition;
        private float started;
        private bool interacting;
        public int FrameIndex { get; private set; }
        public void Configure(SpriteRenderer renderer, PropDefinition source)
        { target = renderer; definition = source; started = Time.time; interacting = false; SetFrame(0); }
        public void PlayInteraction()
        {
            if (definition == null || definition.interactionFrames == null || definition.interactionFrames.Length == 0) return;
            interacting = true;
            started = Time.time;
        }
        private void Update()
        {
            if (target == null || definition == null) return;
            var frames = interacting ? definition.interactionFrames : definition.idleFrames;
            if (frames == null || frames.Length == 0) return;
            int frame = Mathf.FloorToInt((Time.time - started) * definition.framesPerSecond);
            if (interacting && frame >= frames.Length)
            { interacting = false; started = Time.time; SetFrame(0); return; }
            frame = !interacting && definition.loop ? frame % frames.Length : Mathf.Min(frame, frames.Length - 1);
            SetFrame(frame);
        }
        private void SetFrame(int frame)
        {
            if (target == null || definition == null) return;
            var frames = interacting ? definition.interactionFrames : definition.idleFrames;
            if (frames == null || frames.Length == 0) return;
            FrameIndex = frame;
            if (frames[frame] != null) target.sprite = frames[frame];
        }
    }
}

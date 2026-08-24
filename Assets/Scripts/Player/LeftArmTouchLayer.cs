using UnityEngine;

namespace SurvivalHorror
{
    /// <summary>
    /// Fades the masked left-arm layer in and out around the door-touch animation.
    ///
    /// The layer cannot simply be left at weight 1. It is an Override layer, so while
    /// its state machine sits on the empty state it would drive the masked bones to
    /// their bind pose and tear the left arm off the gun. Weight has to be 0 by
    /// default and only raised while the touch is actually playing.
    ///
    /// Nothing needs to call this: the layer's own state machine reacts to the
    /// OpenDoor trigger, and this watches which state that layer is in. That keeps
    /// DoorLatch free of any knowledge about layers.
    ///
    /// Put this on the same object as the hands Animator.
    /// </summary>
    public class LeftArmTouchLayer : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;

        [Tooltip("Name of the masked layer holding the left-arm touch.")]
        [SerializeField] private string layerName = "LeftArm Touch";

        [Tooltip("State on that layer that should raise the weight.")]
        [SerializeField] private string touchStateName = "Touch_Left";

        [Header("Blend")]
        [Tooltip("Seconds to fade the arm in. Too fast and it snaps off the gun.")]
        [SerializeField] private float fadeInTime = 0.14f;
        [Tooltip("Seconds to fade back onto the weapon.")]
        [SerializeField] private float fadeOutTime = 0.2f;

        private int layerIndex = -1;
        private int touchStateHash;
        private float weight;

        private void Reset()
        {
            animator = GetComponent<Animator>();
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();

            layerIndex = animator.GetLayerIndex(layerName);
            touchStateHash = Animator.StringToHash(touchStateName);

            if (layerIndex >= 0) animator.SetLayerWeight(layerIndex, 0f);
        }

        private void Update()
        {
            if (animator == null || layerIndex < 0) return;

            // A layer's state machine keeps running even at weight 0, so the trigger
            // still moves it into Touch_Left and this can react to that.
            var state = animator.GetCurrentAnimatorStateInfo(layerIndex);
            bool touching = state.shortNameHash == touchStateHash;

            if (!touching && animator.IsInTransition(layerIndex))
                touching = animator.GetNextAnimatorStateInfo(layerIndex).shortNameHash == touchStateHash;

            float target = touching ? 1f : 0f;
            float time = touching ? fadeInTime : fadeOutTime;

            weight = time <= 0f
                ? target
                : Mathf.MoveTowards(weight, target, Time.deltaTime / time);

            animator.SetLayerWeight(layerIndex, weight);
        }
    }
}

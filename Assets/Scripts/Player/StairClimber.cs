using System;
using UnityEngine;

namespace SurvivalHorror
{
    /// <summary>
    /// Lets the capsule walk up stairs.
    ///
    /// A Rigidbody capsule cannot climb a step on its own: the riser is a vertical
    /// face, so the capsule just presses into it and stops. This probes ahead with two
    /// rays — one at ankle height, one at the maximum step height — and lifts the body
    /// only when the low ray is blocked and the high one is clear. That distinction is
    /// what separates a stair from a wall: a wall blocks both.
    ///
    /// Put this on the Player root, alongside the Rigidbody and CapsuleCollider.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class StairClimber : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CapsuleCollider body;
        [Tooltip("Supplies the intended walk direction. Without it the climber falls " +
                 "back to velocity, which a step collision has already cancelled.")]
        [SerializeField] private FirstPersonController controller;

        [Header("Step Detection")]
        [Tooltip("Tallest step that can be climbed. Anything higher is treated as a wall.")]
        [SerializeField] private float maxStepHeight = 0.4f;
        [Tooltip("How far ahead to probe for a riser.")]
        [SerializeField] private float probeDistance = 0.5f;
        [Tooltip("Height of the low probe above the foot. Keep small.")]
        [SerializeField] private float lowProbeHeight = 0.06f;
        [Tooltip("Extra clearance above the step the high probe needs to find empty.")]
        [SerializeField] private float headroom = 0.08f;
        [Tooltip("Layers counted as walkable geometry. Exclude the player's own layer.")]
        [SerializeField] private LayerMask stepMask = ~0;

        [Header("Climb")]
        [Tooltip("Vertical lift speed while stepping up, in metres per second.")]
        [SerializeField] private float climbSpeed = 4.5f;
        [Tooltip("Minimum horizontal speed before stepping is attempted.")]
        [SerializeField] private float minSpeed = 0.12f;
        [Tooltip("Ground clearance below the foot required to consider the player grounded.")]
        [SerializeField] private float groundCheckDistance = 0.35f;

        [Tooltip("Vertical distance climbed between camera jolts. Set this to your riser " +
                 "height so one jolt matches one real step.")]
        [SerializeField] private float stepJoltHeight = 0.16f;

        private Rigidbody rb;
        private float climbedSinceJolt;

        /// <summary>True on frames where the body is being lifted over a step.</summary>
        public bool IsClimbing { get; private set; }

        /// <summary>Raised once each time a new step is started. Drives the camera jolt.</summary>
        public event Action OnStepUp;

        private void Reset()
        {
            body = GetComponent<CapsuleCollider>();
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (body == null) body = GetComponent<CapsuleCollider>();
            if (controller == null) controller = GetComponent<FirstPersonController>();
        }

        private void FixedUpdate()
        {
            IsClimbing = false;

            if (body == null) return;

            // Intent first: walking into a riser cancels the velocity on contact, so
            // reading velocity alone means the climb can never start once you are
            // touching the step. The input keeps pointing at the stair regardless.
            Vector3 dir = controller != null ? controller.DesiredMoveDirection : Vector3.zero;

            if (dir.sqrMagnitude < 0.0001f)
            {
                Vector3 horizontal = rb.linearVelocity;
                horizontal.y = 0f;
                if (horizontal.magnitude < minSpeed) return;
                dir = horizontal.normalized;
            }
            Bounds b = body.bounds;
            float footY = b.min.y;

            // Airborne bodies should fall, not climb.
            Vector3 groundOrigin = new Vector3(b.center.x, footY + 0.05f, b.center.z);
            if (!Physics.Raycast(groundOrigin, Vector3.down, groundCheckDistance,
                                 stepMask, QueryTriggerInteraction.Ignore))
                return;

            // Low probe: is something directly in front of the foot?
            Vector3 lowOrigin = new Vector3(b.center.x, footY + lowProbeHeight, b.center.z);
            if (!Physics.Raycast(lowOrigin, dir, out RaycastHit lowHit, probeDistance,
                                 stepMask, QueryTriggerInteraction.Ignore))
                return;

            // The player shares a layer with the level, so the mask cannot exclude it.
            // Reject our own colliders explicitly instead.
            if (lowHit.collider.transform.IsChildOf(transform)) return;

            // A near-horizontal normal means an upright face. Ramps are already
            // walkable and must not be lifted, or the player floats up them.
            if (Mathf.Abs(lowHit.normal.y) > 0.3f) return;

            // High probe: if this is also blocked it is a wall, not a step.
            Vector3 highOrigin = new Vector3(b.center.x, footY + maxStepHeight + headroom, b.center.z);
            if (Physics.Raycast(highOrigin, dir, out RaycastHit highHit, probeDistance + body.radius,
                                stepMask, QueryTriggerInteraction.Ignore)
                && !highHit.collider.transform.IsChildOf(transform))
                return;

            // Clear above, blocked below: step up.
            IsClimbing = true;

            float lift = climbSpeed * Time.fixedDeltaTime;
            rb.position += Vector3.up * lift;

            // Jolt per riser actually climbed, not per probe edge. The probe flickers
            // on and off several times while crossing a single step, so edge-triggering
            // stacked impulses on top of each other and shook the camera apart.
            climbedSinceJolt += lift;
            if (climbedSinceJolt >= stepJoltHeight)
            {
                climbedSinceJolt -= stepJoltHeight;
                OnStepUp?.Invoke();
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (body == null) body = GetComponent<CapsuleCollider>();
            if (body == null) return;

            Bounds b = body.bounds;
            float footY = b.min.y;
            Vector3 dir = transform.forward;

            Gizmos.color = Color.red;
            Vector3 low = new Vector3(b.center.x, footY + lowProbeHeight, b.center.z);
            Gizmos.DrawLine(low, low + dir * probeDistance);

            Gizmos.color = Color.green;
            Vector3 high = new Vector3(b.center.x, footY + maxStepHeight + headroom, b.center.z);
            Gizmos.DrawLine(high, high + dir * (probeDistance + body.radius));
        }
#endif
    }
}

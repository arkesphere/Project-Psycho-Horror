using System.Collections.Generic;
using UnityEngine;

namespace SurvivalHorror
{
    /// <summary>
    /// Two-tier RE-style detection driving a single floating prompt:
    ///   1. Proximity — an omnidirectional OverlapSphere, checked a few times a
    ///      second. The CLOSEST interactable inside gets the prompt in its Far
    ///      state (arrow icon), regardless of whether you're looking at it.
    ///   2. Focus     — the look-based spherecast, checked every frame. Only
    ///      within the (shorter) interact distance AND while looking at the
    ///      object does the prompt switch to Near (the E box), which is also
    ///      the only state that lets you actually press the key.
    /// Put this on the player root; assign the first-person camera.
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Inventory inventory;
        [Tooltip("The single floating box that shows an arrow from a distance and cross-fades to the key hint up close.")]
        [SerializeField] private WorldSpacePrompt worldPrompt;
        [SerializeField] private string interactKeyHint = "E";

        [Header("Focus Detection (look-based, shows the E prompt)")]
        [SerializeField, Min(0.1f)] private float interactDistance = 2.5f;
        [Tooltip("Spherecast radius. A little thickness makes aiming at small props far more forgiving.")]
        [SerializeField, Min(0f)] private float castRadius = 0.12f;
        [Tooltip("Layers that can contain interactables. Exclude the player's own layer.")]
        [SerializeField] private LayerMask interactionMask = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide;
        [Tooltip("Max angle between the camera forward and the item, to reject things caught at the edge of the sphere.")]
        [SerializeField, Range(0f, 90f)] private float maxViewAngle = 45f;

        [Header("Proximity Detection (omnidirectional, shows the arrow)")]
        [Tooltip("Should be larger than Interact Distance — this is the 'something's here' range, checked in all directions regardless of where you're looking.")]
        [SerializeField, Min(0.1f)] private float proximityRadius = 4f;
        [Tooltip("How often the proximity sphere is re-checked. Doesn't need per-frame precision.")]
        [SerializeField, Min(0.02f)] private float proximityCheckInterval = 0.15f;

        private IInteractable _focused;
        private Component _focusedComponent;
        private IInteractable _closestNearby;

        private readonly Collider[] _proximityBuffer = new Collider[16];
        private readonly RaycastHit[] _castBuffer = new RaycastHit[16];
        private float _nextProximityCheck;

        public Camera PlayerCamera => playerCamera;
        public Inventory Inventory => inventory;
        public IInteractable Focused => _focused;

        private void Reset()
        {
            playerCamera = GetComponentInChildren<Camera>();
            inventory = GetComponent<Inventory>();
            }

        private void Awake()
        {
            if (playerCamera == null) playerCamera = Camera.main;
            if (inventory == null) inventory = GetComponent<Inventory>();
            if (worldPrompt != null) worldPrompt.SetCamera(playerCamera);
        }

        private void Update()
        {
            // Examine view, menus and cutscenes suppress world interaction entirely.
            if (PlayerControlGate.Locked)
            {
                if (_focused != null) ClearFocus();
                _closestNearby = null;
                worldPrompt?.Hide();
                return;
            }

            UpdateProximity();
            ScanForInteractable();
            UpdatePromptState();

            if (_focused != null && _focused.CanInteract && InputCombat.InteractPressed)
                _focused.Interact(this);
        }

        /// <summary>
        /// Omnidirectional "what's the nearest thing around me" pass. Independent
        /// of the look-based focus scan below — this is what feeds the Far (arrow)
        /// state, which can be visible while the player isn't looking at anything.
        /// </summary>
        private void UpdateProximity()
        {
            if (Time.time < _nextProximityCheck) return;
            _nextProximityCheck = Time.time + proximityCheckInterval;

            int count = Physics.OverlapSphereNonAlloc(
                transform.position, proximityRadius, _proximityBuffer,
                interactionMask, triggerInteraction);

            IInteractable closest = null;
            float closestSqrDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var col = _proximityBuffer[i];
                if (col == null) continue;
                if (col.transform.IsChildOf(transform)) continue;

                var candidate = col.GetComponentInParent<IInteractable>();
                if (candidate == null || !candidate.CanInteract) continue;

                float sqrDist = (candidate.PromptWorldPosition - transform.position).sqrMagnitude;
                if (sqrDist < closestSqrDist)
                {
                    closestSqrDist = sqrDist;
                    closest = candidate;
                }
            }

            _closestNearby = closest;
        }

        /// <summary>
        /// One decision point for what the floating box should show right now.
        /// Focus (Near) always beats Proximity (Far): if you're close enough and
        /// looking at something, that's what you see, even if a different item is
        /// technically nearer in space.
        /// </summary>
        private void UpdatePromptState()
        {
            if (worldPrompt == null) return;

            if (_focused != null)
            {
                worldPrompt.SetState(PromptState.Near, _focused.PromptWorldPosition, interactKeyHint);
            }
            else if (_closestNearby != null)
            {
                worldPrompt.SetState(PromptState.Far, _closestNearby.PromptWorldPosition);
            }
            else
            {
                worldPrompt.SetState(PromptState.Hidden, Vector3.zero);
            }
        }

        private void ScanForInteractable()
        {
            if (playerCamera == null) return;

            Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            IInteractable found = null;
            Component foundComponent = null;

            // The camera sits inside the player's own capsule, and a sweep that starts
            // overlapping a collider reports it as a zero-distance hit. A single-result
            // cast therefore always came back as the player and never saw the world, so
            // gather every hit and take the nearest one that isn't part of the player.
            int count = castRadius > 0f
                ? Physics.SphereCastNonAlloc(ray, castRadius, _castBuffer, interactDistance, interactionMask, triggerInteraction)
                : Physics.RaycastNonAlloc(ray, _castBuffer, interactDistance, interactionMask, triggerInteraction);

            float nearest = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                var hit = _castBuffer[i];
                if (hit.collider == null) continue;
                if (hit.collider.transform.IsChildOf(transform)) continue;

                var candidate = hit.collider.GetComponentInParent<IInteractable>();
                if (candidate == null || !candidate.CanInteract) continue;
                if (!IsWithinViewAngle(hit.collider.transform)) continue;

                // An initial overlap reports distance 0, which would otherwise always
                // win; measure from the camera instead so ordering stays meaningful.
                float dist = Vector3.Distance(ray.origin, hit.collider.bounds.ClosestPoint(ray.origin));
                if (dist >= nearest) continue;

                nearest = dist;
                found = candidate;
                foundComponent = candidate as Component;
            }

            if (ReferenceEquals(found, _focused)) return;

            if (_focused != null && _focusedComponent != null) _focused.OnFocusExit();

            _focused = found;
            _focusedComponent = foundComponent;

            if (_focused != null) _focused.OnFocusEnter();
        }

        private bool IsWithinViewAngle(Transform target)
        {
            if (maxViewAngle >= 90f) return true;
            Vector3 toTarget = target.position - playerCamera.transform.position;
            if (toTarget.sqrMagnitude < 0.0001f) return true;
            return Vector3.Angle(playerCamera.transform.forward, toTarget) <= maxViewAngle;
        }

        private void ClearFocus()
        {
            if (_focused != null && _focusedComponent != null) _focused.OnFocusExit();
            _focused = null;
            _focusedComponent = null;
        }

        /// <summary>Re-reads the anchor position from the focused object (after a partial pickup, say).</summary>
        public void RefreshPrompt()
        {
            if (_focused == null || !_focused.CanInteract) ClearFocus();
            UpdatePromptState();
        }

        /// <summary>Called by an interactable that just destroyed or disabled itself.</summary>
        public void NotifyInteractableConsumed(IInteractable which)
        {
            if (ReferenceEquals(which, _closestNearby)) _closestNearby = null;

            if (ReferenceEquals(which, _focused))
            {
                _focused = null;
                _focusedComponent = null;
            }

            UpdatePromptState();
        }

        /// <summary>Short centre-screen notice, e.g. "No more room."</summary>
public void ShowMessage(string message, float duration = 1.8f)
        {
            EventBus.Publish(new InteractionMessageRequestedEvent(message, duration));
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (playerCamera == null) return;

            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
            Vector3 origin = playerCamera.transform.position;
            Vector3 end = origin + playerCamera.transform.forward * interactDistance;
            Gizmos.DrawLine(origin, end);
            if (castRadius > 0f) Gizmos.DrawWireSphere(end, castRadius);

            Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, proximityRadius);
        }
#endif
    }
}

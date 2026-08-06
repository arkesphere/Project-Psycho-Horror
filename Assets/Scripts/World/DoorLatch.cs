using UnityEngine;
using SurvivalHorror;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(HingeJoint))]
public class DoorLatch : MonoBehaviour, IInteractable
{
    [Header("Latch")]
    [SerializeField] private float latchAngle = 1.5f;
    [SerializeField] private float clearAngle = 6f;
    [SerializeField] private float minimumPushSpeed = 0.03f;
    [SerializeField] private float releaseImpulse = 0f;
    [SerializeField] private float releaseBlockTime = 0.15f;

    [Header("Interaction")]
    [Tooltip("Animator that receives the OpenDoor trigger. Defaults to an Animator on this object or its children.")]
    [SerializeField] private Animator doorAnimator;
    [Tooltip("Trigger fired when the player opens the door from the front while it is fully latched.")]
    [SerializeField] private string openTrigger = "OpenDoor";
    [Tooltip("Local-space direction that points OUT of the door's front face. Flip (e.g. -Z) if front/back are reversed.")]
    [SerializeField] private Vector3 frontLocalDirection = Vector3.forward;
    [Tooltip("Prompt text shown by the interaction UI when the door can be opened.")]
    [SerializeField] private string promptText = "Open Door";
    [Tooltip("Height above the door pivot where the interaction prompt floats.")]
    [SerializeField] private float promptHeight = 1.1f;

    private Rigidbody doorRigidbody;
    private HingeJoint hinge;
    private JointLimits openLimits;

    private bool hasLeftCenter;
    private float allowReleaseTime;
    private int openTriggerHash;

    public bool IsLatched { get; private set; }

    private void Awake()
    {
        doorRigidbody = GetComponent<Rigidbody>();
        hinge = GetComponent<HingeJoint>();

        // Save the normal open-door limits from the Inspector.
        openLimits = hinge.limits;

        if (doorAnimator == null)
            doorAnimator = GetComponentInChildren<Animator>();

        openTriggerHash = Animator.StringToHash(openTrigger);
    }

    private System.Collections.IEnumerator Start()
{
    // Let Unity initialize the prefab, Rigidbody, colliders and joint.
    yield return new WaitForFixedUpdate();

    doorRigidbody.linearVelocity = Vector3.zero;
    doorRigidbody.angularVelocity = Vector3.zero;

    Physics.SyncTransforms();

    hinge.useLimits = true;
    LatchDoor();
}

    private void FixedUpdate()
    {
        if (IsLatched)
            return;

        float angle = Mathf.Abs(hinge.angle);

        // Prevent immediate relatching after being released.
        if (!hasLeftCenter && angle >= clearAngle)
            hasLeftCenter = true;

        if (hasLeftCenter && angle <= latchAngle)
            LatchDoor();
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryRelease(collision, true);
    }

    private void OnCollisionStay(Collision collision)
    {
        TryRelease(collision, true);
    }

    private void TryRelease(
        Collision collision,
        bool requireMovement)
    {
        if (!IsLatched || Time.time < allowReleaseTime)
            return;

        Rigidbody playerBody = collision.rigidbody;

        if (playerBody == null || !playerBody.CompareTag("Player"))
            return;

        ContactPoint contact = collision.GetContact(0);

        float pushSpeed = Mathf.Abs(
            Vector3.Dot(collision.relativeVelocity, contact.normal)
        );

        if (requireMovement && pushSpeed < minimumPushSpeed)
            return;

        Vector3 pushDirection =
            contact.point - playerBody.worldCenterOfMass;

        pushDirection.y = 0f;

        if (pushDirection.sqrMagnitude < 0.001f)
            pushDirection = -contact.normal;

        ReleaseDoor(pushDirection.normalized, contact.point);
    }

    private void LatchDoor()
    {
        doorRigidbody.angularVelocity = Vector3.zero;

        JointLimits lockedLimits = hinge.limits;

        lockedLimits.min = -0.2f;
        lockedLimits.max = 0.2f;
        lockedLimits.bounciness = 0f;
        lockedLimits.bounceMinVelocity = 0f;
        lockedLimits.contactDistance = 0.05f;

        hinge.limits = lockedLimits;
        hinge.useLimits = true;

        IsLatched = true;
        allowReleaseTime = Time.time + releaseBlockTime;
    }

    private void ReleaseDoor(
        Vector3 pushDirection,
        Vector3 pushPoint)
    {
        // Restore the normal opening limits.
        hinge.limits = openLimits;

        IsLatched = false;
        hasLeftCenter = false;

        doorRigidbody.AddForceAtPosition(
            pushDirection * releaseImpulse,
            pushPoint,
            ForceMode.Impulse
        );
    }

    // ----- IInteractable -----

    /// <summary>Only a fully latched (closed, original-position) door can be opened.</summary>
    public bool CanInteract => IsLatched;

    public Vector3 PromptWorldPosition => transform.position + Vector3.up * promptHeight;

    public string GetPromptText() => promptText;

    public void OnFocusEnter() { }
    public void OnFocusExit() { }
    public void OnProximityEnter() { }
    public void OnProximityExit() { }

    /// <summary>
    /// Fire the OpenDoor trigger, but only when the door is fully latched AND the
    /// player is standing in front of it. Interacting from behind does nothing.
    /// </summary>
    public void Interact(PlayerInteractor interactor)
    {
        // Guard again even though PlayerInteractor gates on CanInteract: the door
        // must be fully locked in its original position to open.
        if (!IsLatched)
            return;

        if (interactor != null && !IsPlayerInFront(interactor.transform.position))
            return;

        if (doorAnimator != null)
            doorAnimator.SetTrigger(openTriggerHash);
    }

    /// <summary>
    /// True when the given world position is on the door's front face. Because we
    /// only open while latched, the door is at its rest orientation whenever this
    /// runs, so the front normal is stable. Compared on the horizontal plane only.
    /// </summary>
    private bool IsPlayerInFront(Vector3 playerPosition)
    {
        Vector3 frontNormal = transform.TransformDirection(frontLocalDirection);
        frontNormal.y = 0f;

        Vector3 toPlayer = playerPosition - transform.position;
        toPlayer.y = 0f;

        return Vector3.Dot(toPlayer, frontNormal) > 0f;
    }
}
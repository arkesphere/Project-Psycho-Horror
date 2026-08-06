using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(HingeJoint))]
public class DoorLatch : MonoBehaviour
{
    [Header("Latch")]
    [SerializeField] private float latchAngle = 1.5f;
    [SerializeField] private float clearAngle = 6f;
    [SerializeField] private float minimumPushSpeed = 0.03f;
    [SerializeField] private float releaseImpulse = 0f;
    [SerializeField] private float releaseBlockTime = 0.15f;

    [Header("Animation")]
    [SerializeField] private string openTrigger = "OpenDoor";
    [SerializeField] private float animationLockTime = 0.8f;

    private static float nextAnimationAllowedTime;

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

        // Save the normal open limits configured in the Inspector.
        openLimits = hinge.limits;

        openTriggerHash = Animator.StringToHash(openTrigger);
    }

    private System.Collections.IEnumerator Start()
    {
        // Let Unity finish initializing physics.
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

    private void TryRelease(Collision collision, bool requireMovement)
    {
        if (!IsLatched || Time.time < allowReleaseTime)
            return;

        Rigidbody playerBody = collision.rigidbody;

        if (playerBody == null || !playerBody.CompareTag("Player"))
            return;

        ContactPoint contact = collision.GetContact(0);

        float pushSpeed = Mathf.Abs(
            Vector3.Dot(collision.relativeVelocity, contact.normal));

        if (requireMovement && pushSpeed < minimumPushSpeed)
            return;

        // Trigger the player's hand animation.
        if (Time.time >= nextAnimationAllowedTime)
        {
            Animator animator = playerBody.transform.root.GetComponentInChildren<Animator>(true);

            if (animator != null)
            {
                animator.SetTrigger(openTriggerHash);
                nextAnimationAllowedTime = Time.time + animationLockTime;
            }
        }

        Vector3 pushDirection = contact.point - playerBody.worldCenterOfMass;
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

    private void ReleaseDoor(Vector3 pushDirection, Vector3 pushPoint)
    {
        // Restore the door's normal opening limits.
        hinge.limits = openLimits;

        IsLatched = false;
        hasLeftCenter = false;

        if (releaseImpulse > 0f)
        {
            doorRigidbody.AddForceAtPosition(
                pushDirection * releaseImpulse,
                pushPoint,
                ForceMode.Impulse);
        }
    }
}
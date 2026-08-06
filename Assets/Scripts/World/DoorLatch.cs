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

    private Rigidbody doorRigidbody;
    private HingeJoint hinge;
    private JointLimits openLimits;

    private bool hasLeftCenter;
    private float allowReleaseTime;

    public bool IsLatched { get; private set; }

    private void Awake()
    {
        doorRigidbody = GetComponent<Rigidbody>();
        hinge = GetComponent<HingeJoint>();

        // Save the normal open-door limits from the Inspector.
        openLimits = hinge.limits;
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
}
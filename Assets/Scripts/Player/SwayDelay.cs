using UnityEngine;

public class SwayDelay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform;

    [Header("Input Smoothing")]
    [SerializeField] private float deltaSmoothTime = 0.08f;

    [Header("Position Sway")]
    [SerializeField] private float positionSwayAmount = 0.012f;
    [SerializeField] private float maxPositionSway = 0.045f;
    [SerializeField] private float positionSmoothTime = 0.09f;

    [Header("Rotation Sway")]
    [SerializeField] private float rotationSwayAmount = 1.8f;
    [SerializeField] private float maxRotationSway = 5f;
    [SerializeField] private float rotationFollowSpeed = 10f;

    private Vector3 startLocalPosition;
    private Quaternion startLocalRotation;

    private Vector3 previousCameraEuler;
    private Vector2 smoothedDelta;
    private Vector2 deltaVelocity;
    private Vector3 positionVelocity;

    private void Awake()
    {
        startLocalPosition = transform.localPosition;
        startLocalRotation = transform.localRotation;

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (cameraTransform != null)
            previousCameraEuler = cameraTransform.eulerAngles;
    }

    private void OnEnable()
    {
        if (cameraTransform != null)
            previousCameraEuler = cameraTransform.eulerAngles;

        smoothedDelta = Vector2.zero;
        deltaVelocity = Vector2.zero;
        positionVelocity = Vector3.zero;
    }

    private void LateUpdate()
    {
        if (cameraTransform == null)
            return;

        Vector3 currentCameraEuler = cameraTransform.eulerAngles;

        Vector2 rawDelta = new Vector2(
            Mathf.DeltaAngle(previousCameraEuler.y, currentCameraEuler.y),
            Mathf.DeltaAngle(previousCameraEuler.x, currentCameraEuler.x)
        );

        previousCameraEuler = currentCameraEuler;

        smoothedDelta = Vector2.SmoothDamp(
            smoothedDelta,
            rawDelta,
            ref deltaVelocity,
            deltaSmoothTime
        );

        float swayX = Mathf.Clamp(-smoothedDelta.x * positionSwayAmount, -maxPositionSway, maxPositionSway);
        float swayY = Mathf.Clamp(smoothedDelta.y * positionSwayAmount, -maxPositionSway, maxPositionSway);

        Vector3 targetPosition = startLocalPosition + new Vector3(swayX, swayY, 0f);

        transform.localPosition = Vector3.SmoothDamp(
            transform.localPosition,
            targetPosition,
            ref positionVelocity,
            positionSmoothTime
        );

        float pitch = Mathf.Clamp(smoothedDelta.y * rotationSwayAmount, -maxRotationSway, maxRotationSway);
        float yaw = Mathf.Clamp(-smoothedDelta.x * rotationSwayAmount, -maxRotationSway, maxRotationSway);
        float roll = Mathf.Clamp(-smoothedDelta.x * rotationSwayAmount, -maxRotationSway, maxRotationSway);

        Quaternion targetRotation = startLocalRotation * Quaternion.Euler(pitch, yaw, roll);

        transform.localRotation = Quaternion.Slerp(
            transform.localRotation,
            targetRotation,
            1f - Mathf.Exp(-rotationFollowSpeed * Time.deltaTime)
        );
    }
}
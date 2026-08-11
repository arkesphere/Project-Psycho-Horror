using System.Collections;
using UnityEngine;

namespace SurvivalHorror
{
    public sealed class CockroachController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Animator animator;

        [Tooltip("The collider of the wall/surface the cockroach is crawling on.")]
        [SerializeField] private Collider currentSurface;

        [Header("Movement")]
        [SerializeField] private float walkSpeed = 0.5f;
        [SerializeField] private float acceleration = 3f;
        [SerializeField] private float wanderDistance = 1.5f;

        [Header("Surface")]
        [SerializeField] private float surfaceCheckDistance = 0.5f;
        [SerializeField] private float surfaceOffset = 0.01f;

        [Header("Idle")]
        [SerializeField] private float minIdleTime = 0.5f;
        [SerializeField] private float maxIdleTime = 2f;

        [Header("Rotation")]
        [Tooltip("X rotation used when moving upward.")]
        [SerializeField] private float upRotationX = 0f;

        [Tooltip("X rotation used when moving downward.")]
        [SerializeField] private float downRotationX = 180f;

        [Header("Player Detection")]
        [SerializeField] private LayerMask playerLayer;

        [Tooltip("Direction the cockroach looks for the player. Local space.")]
        [SerializeField] private Vector3 playerDetectionDirection = Vector3.forward;

        [SerializeField] private float playerDetectionDistance = 2f;

        [Header("Escape Rotation")]
        [SerializeField] private float escapeRotationX = 90f;
        [SerializeField] private float escapeRotationY = 0f;
        [SerializeField] private float escapeRotationZ = 0f;

        [Tooltip("Direction the cockroach flies in, relative to itself.")]
        [SerializeField] private Vector3 escapeFlyDirection = Vector3.forward;

        [SerializeField] private float escapeSpeed = 3f;

        [SerializeField] private float escapeRotationSpeed = 15f;

        [SerializeField] private float destroyAfterEscape = 2f;

        [Header("Animation")]
        [SerializeField] private float animationDamp = 0.1f;
        [SerializeField] private float twitchChance = 0.15f;

        private Vector3 surfaceNormal;
        private Vector3 targetPosition;

        private float currentSpeed;
        private float targetSpeed;

        private float idleTimer;
        private float idleDuration;

        private bool isIdle;
        private bool isEscaping;

        // These NEVER change after Awake.
        private float lockedY;
        private float lockedZ;

        private static readonly int SpeedHash =
            Animator.StringToHash("Speed");

        private static readonly int TwitchHash =
            Animator.StringToHash("Twitch");
        
        private static readonly int IsFlyingHash =
            Animator.StringToHash("IsFlying");

        private void Awake()
        {
            if (currentSurface == null)
            {
                Debug.LogError(
                    $"{name}: Current Surface is not assigned.",
                    this
                );
            }

            // Remember initial Y and Z.
            lockedY = transform.localEulerAngles.y;
            lockedZ = transform.localEulerAngles.z;
        }

        private void Start()
        {
            if (!FindSurface())
            {
                Debug.LogError(
                    $"{name}: Could not find surface.",
                    this
                );

                enabled = false;
                return;
            }

            EnterIdle();
        }

        private void Update()
        {
            if (isEscaping)
                return;

            if (currentSurface == null)
                return;

            // Check player BEFORE doing normal movement.
            if (CheckForPlayer())
            {
                StartEscape();
                return;
            }

            KeepOnSurface();

            if (isIdle)
            {
                UpdateIdle();
            }
            else
            {
                UpdateWalking();
            }

            UpdateAnimator();
        }

        // ============================================================
        // PLAYER DETECTION
        // ============================================================

        private bool CheckForPlayer()
        {
            Vector3 direction =
                transform.TransformDirection(
                    playerDetectionDirection.normalized
                );

            Vector3 origin =
                transform.position;

            Debug.DrawRay(
                origin,
                direction * playerDetectionDistance,
                Color.red
            );

            if (Physics.Raycast(
                origin,
                direction,
                out RaycastHit hit,
                playerDetectionDistance,
                playerLayer,
                QueryTriggerInteraction.Ignore))
            {
                /*
                 * If the player has colliders on child objects,
                 * check the parent too.
                 */
                if (hit.collider.CompareTag("Player"))
                    return true;

                if (hit.collider.GetComponentInParent<Transform>()
                    != null)
                {
                    Transform root =
                        hit.collider.transform.root;

                    if (root.CompareTag("Player"))
                        return true;
                }
            }

            return false;
        }

        // ============================================================
        // ESCAPE
        // ============================================================

        private void StartEscape()
        {
            if (isEscaping)
                return;

            isEscaping = true;
            isIdle = true;

            currentSpeed = 0f;

            if (animator != null)
            {
                animator.SetFloat(SpeedHash, 0f);

                // Start flying animation.
                animator.SetBool(IsFlyingHash, true);
            }

            StartCoroutine(EscapeRoutine());
        }
        private IEnumerator EscapeRoutine()
        {
            /*
             * Stop all normal movement.
             */

            if (animator != null)
            {
                animator.SetFloat(
                    SpeedHash,
                    0f
                );
            }

            /*
             * Only X changes.
             *
             * Y and Z remain locked.
             */
            Vector3 targetRotation = new Vector3(
                escapeRotationX,
                escapeRotationY,
                escapeRotationZ
            );

            while (
                Quaternion.Angle(
                    transform.localRotation,
                    Quaternion.Euler(targetRotation)
                ) > 1f)
            {
                transform.localRotation =
                    Quaternion.RotateTowards(
                        transform.localRotation,
                        Quaternion.Euler(targetRotation),
                        escapeRotationSpeed *
                        100f *
                        Time.deltaTime
                    );

                yield return null;
            }

// Make sure we end exactly at the requested rotation.
            transform.localRotation =
                Quaternion.Euler(targetRotation);

            /*
             * Convert the serialized local escape direction
             * into world space using the new rotation.
             */
            Vector3 flyDirection =
                transform.TransformDirection(
                    escapeFlyDirection.normalized
                );

            float elapsed = 0f;

            /*
             * Fly away for the configured duration.
             */
            while (elapsed < destroyAfterEscape)
            {
                transform.position +=
                    flyDirection *
                    escapeSpeed *
                    Time.deltaTime;

                elapsed += Time.deltaTime;

                yield return null;
            }

            Destroy(gameObject);
        }

        // ============================================================
        // IDLE
        // ============================================================

        private void EnterIdle()
        {
            isIdle = true;

            currentSpeed = 0f;

            idleTimer = 0f;

            idleDuration = Random.Range(
                minIdleTime,
                maxIdleTime
            );

            if (animator != null &&
                Random.value < twitchChance)
            {
                animator.SetTrigger(TwitchHash);
            }
        }

        private void UpdateIdle()
        {
            idleTimer += Time.deltaTime;

            if (idleTimer >= idleDuration)
            {
                StartWalking();
            }
        }

        // ============================================================
        // WALKING
        // ============================================================

        private void StartWalking()
        {
            isIdle = false;

            targetSpeed =
                walkSpeed *
                Random.Range(0.75f, 1.25f);

            PickRandomDestination();
        }

        private void UpdateWalking()
        {
            Vector3 direction =
                targetPosition - transform.position;

            /*
             * Movement is restricted to the wall.
             */
            direction =
                Vector3.ProjectOnPlane(
                    direction,
                    surfaceNormal
                );

            if (direction.sqrMagnitude < 0.01f)
            {
                EnterIdle();
                return;
            }

            direction.Normalize();

            currentSpeed = Mathf.MoveTowards(
                currentSpeed,
                targetSpeed,
                acceleration *
                Time.deltaTime
            );

            /*
             * Move across the wall.
             */
            transform.position +=
                direction *
                currentSpeed *
                Time.deltaTime;

            /*
             * ONLY X rotation changes.
             */
            UpdateXRotation(direction);

            /*
             * Keep physically attached to wall.
             * This does NOT change rotation.
             */
            KeepOnSurface();

            if (Vector3.Distance(
                    transform.position,
                    targetPosition) < 0.08f)
            {
                EnterIdle();
            }
        }

        // ============================================================
        // RANDOM DESTINATION
        // ============================================================

        private void PickRandomDestination()
        {
            Vector3 right =
                Vector3.ProjectOnPlane(
                    Vector3.right,
                    surfaceNormal
                ).normalized;

            Vector3 up =
                Vector3.ProjectOnPlane(
                    Vector3.up,
                    surfaceNormal
                ).normalized;

            if (right.sqrMagnitude < 0.001f)
                right = transform.right;

            if (up.sqrMagnitude < 0.001f)
                up = transform.up;

            float horizontal =
                Random.Range(
                    -wanderDistance,
                    wanderDistance
                );

            float vertical =
                Random.Range(
                    -wanderDistance,
                    wanderDistance
                );

            Vector3 candidate =
                transform.position +
                right * horizontal +
                up * vertical;

            if (FindSurfacePoint(
                    candidate,
                    out Vector3 surfacePoint))
            {
                targetPosition = surfacePoint;
            }
            else
            {
                targetPosition =
                    transform.position;
            }
        }

        // ============================================================
        // ROTATION
        // ============================================================

        private void UpdateXRotation(
            Vector3 movementDirection)
        {
            /*
             * ONLY X IS MODIFIED.
             *
             * Y and Z remain locked.
             */

            float vertical =
                Vector3.Dot(
                    movementDirection,
                    Vector3.up
                );

            float targetX;

            if (vertical > 0.1f)
            {
                targetX = upRotationX;
            }
            else if (vertical < -0.1f)
            {
                targetX = downRotationX;
            }
            else
            {
                targetX =
                    transform.localEulerAngles.x;
            }

            Vector3 currentEuler =
                transform.localEulerAngles;

            float newX =
                Mathf.LerpAngle(
                    currentEuler.x,
                    targetX,
                    10f *
                    Time.deltaTime
                );

            transform.localEulerAngles =
                new Vector3(
                    newX,
                    lockedY,
                    lockedZ
                );
        }

        // ============================================================
        // FIND INITIAL SURFACE
        // ============================================================

        private bool FindSurface()
        {
            if (currentSurface == null)
                return false;

            Vector3 closestPoint =
                currentSurface.ClosestPoint(
                    transform.position
                );

            Vector3 normal =
                transform.position -
                closestPoint;

            if (normal.sqrMagnitude < 0.000001f)
            {
                Vector3[] directions =
                {
                    Vector3.forward,
                    -Vector3.forward,
                    Vector3.right,
                    -Vector3.right,
                    Vector3.up,
                    Vector3.down
                };

                foreach (Vector3 direction in directions)
                {
                    Vector3 origin =
                        transform.position -
                        direction * 0.2f;

                    if (Physics.Raycast(
                        origin,
                        direction,
                        out RaycastHit hit,
                        1f,
                        Physics.DefaultRaycastLayers,
                        QueryTriggerInteraction.Ignore))
                    {
                        if (hit.collider == currentSurface)
                        {
                            surfaceNormal =
                                hit.normal;

                            transform.position =
                                hit.point +
                                hit.normal *
                                surfaceOffset;

                            return true;
                        }
                    }
                }

                return false;
            }

            surfaceNormal =
                normal.normalized;

            transform.position =
                closestPoint +
                surfaceNormal *
                surfaceOffset;

            return true;
        }

        // ============================================================
        // KEEP ON SURFACE
        // ============================================================

        private void KeepOnSurface()
        {
            Vector3 origin =
                transform.position +
                surfaceNormal * 0.2f;

            Debug.DrawRay(
                origin,
                -surfaceNormal *
                surfaceCheckDistance,
                Color.green
            );

            if (Physics.Raycast(
                origin,
                -surfaceNormal,
                out RaycastHit hit,
                surfaceCheckDistance,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore))
            {
                if (hit.collider == currentSurface)
                {
                    surfaceNormal =
                        hit.normal;

                    /*
                     * POSITION ONLY.
                     *
                     * Rotation is NOT changed here.
                     */
                    transform.position =
                        hit.point +
                        hit.normal *
                        surfaceOffset;
                }
            }
        }

        // ============================================================
        // FIND SURFACE POINT
        // ============================================================

        private bool FindSurfacePoint(
            Vector3 position,
            out Vector3 result)
        {
            Vector3 origin =
                position +
                surfaceNormal *
                surfaceCheckDistance;

            if (Physics.Raycast(
                origin,
                -surfaceNormal,
                out RaycastHit hit,
                surfaceCheckDistance * 2f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore))
            {
                if (hit.collider == currentSurface)
                {
                    result =
                        hit.point +
                        hit.normal *
                        surfaceOffset;

                    return true;
                }
            }

            result = position;

            return false;
        }

        // ============================================================
        // ANIMATION
        // ============================================================

        private void UpdateAnimator()
        {
            if (animator == null)
                return;

            float normalizedSpeed =
                Mathf.InverseLerp(
                    0f,
                    walkSpeed,
                    currentSpeed
                );

            animator.SetFloat(
                SpeedHash,
                normalizedSpeed,
                animationDamp,
                Time.deltaTime
            );
        }

        // ============================================================
        // DEBUG
        // ============================================================

        private void OnDrawGizmosSelected()
        {
            if (currentSurface != null)
            {
                Gizmos.DrawWireCube(
                    currentSurface.bounds.center,
                    currentSurface.bounds.size
                );
            }

            Gizmos.DrawSphere(
                targetPosition,
                0.04f
            );

            Gizmos.DrawLine(
                transform.position,
                transform.position +
                surfaceNormal * 0.3f
            );

            /*
             * Player detection ray.
             */
            Gizmos.color = Color.red;

            Vector3 detectionDirection =
                transform.TransformDirection(
                    playerDetectionDirection.normalized
                );

            Gizmos.DrawRay(
                transform.position,
                detectionDirection *
                playerDetectionDistance
            );

            /*
             * Escape direction.
             */
            Gizmos.color = Color.yellow;

            Vector3 escapeDirection =
                transform.TransformDirection(
                    escapeFlyDirection.normalized
                );

            Gizmos.DrawRay(
                transform.position,
                escapeDirection
            );
        }
    }
}
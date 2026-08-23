using UnityEngine;
using Unity.Cinemachine;

namespace SurvivalHorror
{
    /// <summary>
    /// Handheld-camera feel for the first-person view.
    ///
    /// Two separate systems, because they solve different problems:
    ///   Continuous — the Perlin noise on the virtual camera is scaled by movement
    ///   speed, so the view breathes when still, sways when walking and jolts when
    ///   running. This is a sustained state, not an event.
    ///   One-shot   — Cinemachine Impulse for discrete hits: gunshots, knife swings,
    ///   landings. These stack and decay on their own.
    ///
    /// Put this on the Player root and point it at the virtual camera.
    /// </summary>
    public class PlayerCameraFeel : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CinemachineCamera virtualCamera;
        [Tooltip("Speed source for the sway. Falls back to a Rigidbody on this object.")]
        [SerializeField] private Rigidbody playerBody;
        [Tooltip("Impulse source used for world events (landings, explosions).")]
        [SerializeField] private CinemachineImpulseSource impulseSource;
        [Tooltip("Procedural weapon recoil. Drives the gun kick and the knife swing move.")]
        [SerializeField] private CameraRecoil recoil;
        [Tooltip("Optional. Adds a jolt per stair step and extra sway while climbing.")]
        [SerializeField] private StairClimber stairs;

        [Header("Idle Sway (standing still)")]
        [SerializeField] private float idleAmplitude = 0.18f;
        [SerializeField] private float idleFrequency = 0.35f;

        [Header("Walk Sway")]
        [SerializeField] private float walkAmplitude = 0.45f;
        [SerializeField] private float walkFrequency = 0.9f;

        [Header("Run Sway")]
        [SerializeField] private float runAmplitude = 0.9f;
        [SerializeField] private float runFrequency = 1.5f;

        [Header("Speed Mapping")]
        [Tooltip("Speed treated as a full walk. Match the controller's walkSpeed.")]
        [SerializeField] private float walkSpeed = 2f;
        [Tooltip("Speed treated as a full sprint. Match the controller's sprintSpeed.")]
        [SerializeField] private float sprintSpeed = 3.5f;
        [Tooltip("How quickly sway eases between idle/walk/run. Higher is snappier.")]
        [SerializeField] private float blendSpeed = 6f;

        [Header("Impulse Force (world events only)")]
        [SerializeField] private float landForce = 0.3f;

        [Header("Stairs")]
        [Tooltip("Jolt fired each time a step is climbed. Keep this well under the " +
                 "shoot recoil — a footfall should read as a nudge, not a hit.")]
        [SerializeField] private float stairStepForce = 0.05f;
        [Tooltip("Extra sway amplitude added while climbing, on top of the walk sway.")]
        [SerializeField] private float stairSwayBoost = 0.1f;

        private CinemachineBasicMultiChannelPerlin perlin;
        private float currentAmplitude;
        private float currentFrequency;

        private void Awake()
        {
            if (playerBody == null) playerBody = GetComponent<Rigidbody>();
            if (virtualCamera == null) virtualCamera = GetComponentInChildren<CinemachineCamera>();
            if (impulseSource == null) impulseSource = GetComponent<CinemachineImpulseSource>();

            if (virtualCamera != null)
            {
                perlin = virtualCamera.GetComponent<CinemachineBasicMultiChannelPerlin>();
                if (recoil == null) recoil = virtualCamera.GetComponent<CameraRecoil>();
            }

            if (stairs == null) stairs = GetComponent<StairClimber>();

            currentAmplitude = idleAmplitude;
            currentFrequency = idleFrequency;
        }

        private void OnEnable()
        {
            if (stairs != null) stairs.OnStepUp += HandleStepUp;
        }

        private void OnDisable()
        {
            if (stairs != null) stairs.OnStepUp -= HandleStepUp;
        }

        /// <summary>
        /// One jolt per step. Mostly vertical, with a little sideways scatter so a
        /// staircase does not read as the same bump repeated.
        /// </summary>
        private void HandleStepUp()
        {
            // Downward-weighted: a footfall settles the body, it does not launch it.
            Fire(new Vector3(0.25f, -1f, 0.15f), stairStepForce);
        }

        private void Update()
        {
            if (perlin == null) return;

            // Sway is suppressed entirely in the examine view / menus, where the
            // player is not moving and a drifting camera reads as a bug.
            float speed = PlayerControlGate.Locked ? 0f : HorizontalSpeed();

            float targetAmplitude, targetFrequency;

            if (speed <= 0.05f)
            {
                targetAmplitude = idleAmplitude;
                targetFrequency = idleFrequency;
            }
            else if (speed <= walkSpeed)
            {
                // Standing -> walking.
                float t = Mathf.InverseLerp(0.05f, walkSpeed, speed);
                targetAmplitude = Mathf.Lerp(idleAmplitude, walkAmplitude, t);
                targetFrequency = Mathf.Lerp(idleFrequency, walkFrequency, t);
            }
            else
            {
                // Walking -> sprinting.
                float t = Mathf.InverseLerp(walkSpeed, sprintSpeed, speed);
                targetAmplitude = Mathf.Lerp(walkAmplitude, runAmplitude, t);
                targetFrequency = Mathf.Lerp(walkFrequency, runFrequency, t);
            }

            // Climbing is heavier work than walking, so the view rides harder.
            if (stairs != null && stairs.IsClimbing)
                targetAmplitude += stairSwayBoost;

            float k = 1f - Mathf.Exp(-blendSpeed * Time.deltaTime);
            currentAmplitude = Mathf.Lerp(currentAmplitude, targetAmplitude, k);
            currentFrequency = Mathf.Lerp(currentFrequency, targetFrequency, k);

            perlin.AmplitudeGain = currentAmplitude;
            perlin.FrequencyGain = currentFrequency;
        }

        private float HorizontalSpeed()
        {
            if (playerBody == null) return 0f;
            Vector3 v = playerBody.linearVelocity;
            v.y = 0f;
            return v.magnitude;
        }

        /// <summary>
        /// Gun recoil. Handed to the spring rather than an impulse, so the view rises
        /// sharply and settles instead of jolting in a random direction.
        /// </summary>
        public void ShakeShoot()
        {
            if (recoil != null) recoil.FireGun();
        }

        /// <summary>
        /// Knife swing. Runs a curve across the whole swing animation — wind up,
        /// strike, settle — rather than a single hit at the start.
        /// </summary>
        public void ShakeSwing()
        {
            if (recoil != null) recoil.StartKnifeSwing();
        }

        /// <summary>Landing thud. Straight down.</summary>
        public void ShakeLand() => Fire(new Vector3(0.15f, 1f, 0.15f), landForce);

        /// <summary>Generic one-shot for anything else (explosions, doors slamming).</summary>
        public void Shake(float force) => Fire(new Vector3(0.5f, 1f, 0.5f), force);

        private void Fire(Vector3 axis, float force)
        {
            if (impulseSource == null || force <= 0f) return;

            // Randomise the sign so repeated shots do not kick identically.
            axis.x *= Random.value < 0.5f ? -1f : 1f;
            axis.z *= Random.value < 0.5f ? -1f : 1f;

            impulseSource.GenerateImpulseWithVelocity(axis.normalized * force);
        }
    }
}

using UnityEngine;
using Unity.Cinemachine;

namespace SurvivalHorror
{
    /// <summary>
    /// Procedural weapon recoil for the first-person camera, applied as an additive
    /// orientation correction so it layers cleanly over the Perlin sway.
    ///
    /// Impulse is deliberately not used here. An impulse is a fire-and-forget signal
    /// with a fixed envelope, which is why it reads as a random jolt: the gun kicks in
    /// an arbitrary direction and the knife gets one blip at the start of a one-second
    /// animation. Weapon feel needs the camera to follow the motion of the weapon.
    ///
    /// Gun   — a damped spring. One angular velocity kick upward, then a smooth
    ///         critically-damped return. Sharp rise, no snap-back, no randomness in
    ///         the dominant axis, which is what makes it read as recoil.
    /// Knife — a curve evaluated across the ENTIRE swing. Anticipation pulls the view
    ///         back before the strike, the strike sweeps through, then it settles.
    ///         Alternating direction per swing stops it looking looped.
    ///
    /// Put this on the same GameObject as the CinemachineCamera.
    /// </summary>
    [SaveDuringPlay]
    public class CameraRecoil : CinemachineExtension
    {
        [Header("Gun Recoil (damped spring)")]
        [Tooltip("Upward kick per shot, in degrees of angular velocity.")]
        [SerializeField] private float gunKickUp = 55f;
        [Tooltip("Random sideways kick. Small — the vertical rise should dominate.")]
        [SerializeField] private float gunKickSideways = 14f;
        [Tooltip("Random roll per shot, for a little asymmetry.")]
        [SerializeField] private float gunKickRoll = 12f;
        [Tooltip("How hard the spring pulls back to centre. Higher = faster recovery.")]
        [SerializeField] private float gunStiffness = 170f;
        [Tooltip("Damping. Near-critical avoids a springy bounce back past centre.")]
        [SerializeField] private float gunDamping = 17f;

        [Header("Knife Swing (curve over full duration)")]
        [Tooltip("Should match the length of root_Knife_Swing.")]
        [SerializeField] private float swingDuration = 0.983f;
        [Tooltip("Shape across the swing: negative = wind up, positive = follow through.")]
        [SerializeField]
        private AnimationCurve swingEnvelope = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.24f, -0.28f),   // anticipation: pull back before the strike
            new Keyframe(0.44f, 1f),       // strike: sweep through
            new Keyframe(0.63f, -0.26f),   // overshoot past centre
            new Keyframe(0.82f, 0.09f),    // small counter-settle
            new Keyframe(1f, 0f));

        [Tooltip("Horizontal sweep of the slash, in degrees.")]
        [SerializeField] private float swingYaw = 4.2f;
        [Tooltip("Vertical component, in degrees.")]
        [SerializeField] private float swingPitch = 1.9f;
        [Tooltip("Camera lean into the slash, in degrees.")]
        [SerializeField] private float swingRoll = 3.1f;
        [Tooltip("Flip the swing direction each time, so repeats do not look identical.")]
        [SerializeField] private bool alternateSwingDirection = true;

        // Spring state for the gun, in degrees.
        private Vector3 springOffset;
        private Vector3 springVelocity;

        // Knife swing playback.
        private float swingTime = -1f;
        private float swingSign = 1f;

        /// <summary>Combined offset applied this frame.</summary>
        private Vector3 totalEuler;

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            IntegrateSpring(dt);

            Vector3 swing = Vector3.zero;
            if (swingTime >= 0f)
            {
                swingTime += dt;
                float t = swingTime / Mathf.Max(0.01f, swingDuration);

                if (t >= 1f) swingTime = -1f;
                else
                {
                    float e = swingEnvelope.Evaluate(t);
                    swing = new Vector3(
                        swingPitch * e,
                        swingYaw * e * swingSign,
                        swingRoll * -e * swingSign);
                }
            }

            totalEuler = springOffset + swing;
        }

        /// <summary>
        /// Semi-implicit integration of a damped spring toward zero. This is what makes
        /// the gun rise fast and return smoothly instead of popping.
        /// </summary>
        private void IntegrateSpring(float dt)
        {
            // Sub-step so a stiff spring stays stable at low frame rates.
            const float maxStep = 1f / 120f;
            int steps = Mathf.Clamp(Mathf.CeilToInt(dt / maxStep), 1, 8);
            float h = dt / steps;

            for (int i = 0; i < steps; i++)
            {
                Vector3 accel = -gunStiffness * springOffset - gunDamping * springVelocity;
                springVelocity += accel * h;
                springOffset += springVelocity * h;
            }

            if (springOffset.sqrMagnitude < 1e-6f && springVelocity.sqrMagnitude < 1e-6f)
            {
                springOffset = Vector3.zero;
                springVelocity = Vector3.zero;
            }
        }

        /// <summary>One shot. Kicks the muzzle up with a little sideways scatter.</summary>
        public void FireGun(float scale = 1f)
        {
            float side = Random.Range(-1f, 1f);
            springVelocity += new Vector3(
                -gunKickUp * scale,                       // negative pitch = view rises
                gunKickSideways * side * scale,
                gunKickRoll * -side * scale);             // roll opposes yaw, like a wrist twist
        }

        /// <summary>Starts the full-length knife swing move.</summary>
        public void StartKnifeSwing(float duration = -1f)
        {
            if (duration > 0f) swingDuration = duration;
            if (alternateSwingDirection) swingSign = -swingSign;
            swingTime = 0f;
        }

        /// <summary>Cancels any in-flight swing, e.g. on weapon swap.</summary>
        public void CancelSwing() => swingTime = -1f;

        protected override void PostPipelineStageCallback(
            CinemachineVirtualCameraBase vcam,
            CinemachineCore.Stage stage,
            ref CameraState state,
            float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Finalize) return;
            if (totalEuler == Vector3.zero) return;

            // Additive channel: layers on top of the Perlin sway rather than replacing it.
            state.OrientationCorrection *= Quaternion.Euler(totalEuler);
        }
    }
}

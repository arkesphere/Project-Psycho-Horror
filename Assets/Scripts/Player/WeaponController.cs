using System.Collections;
using UnityEngine;

namespace SurvivalHorror
{

    public class WeaponController : MonoBehaviour
    {
        private enum Weapon { Gun, Knife }

        [Header("References")]
        [SerializeField] private Animator animator;

        [SerializeField] private Animator gunAnim;
        [SerializeField] private GameObject gunModel;
        [SerializeField] private GameObject knifeModel;
        [Tooltip("Used to derive the Speed parameter for the walk/run blend.")]
        [SerializeField] private Rigidbody playerBody;
        [Tooltip("Optional. Adds camera kick on shoot and swing.")]
        [SerializeField] private PlayerCameraFeel cameraFeel;

        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int ShootParam = Animator.StringToHash("Shoot");
        private static readonly int ReloadParam = Animator.StringToHash("Reload");
        private static readonly int InspectParam = Animator.StringToHash("Inspect");
        private static readonly int SwingParam = Animator.StringToHash("Swing");
        private static readonly int TouchParam = Animator.StringToHash("Touch");
        private static readonly int EquipKnifeParam = Animator.StringToHash("EquipKnife");
        private static readonly int EquipGunParam = Animator.StringToHash("EquipGun");
        // Tells the controller which weapon to return to after the Touch (door)
        // animation, which would otherwise always fall back to the gun.
        private static readonly int HasKnifeParam = Animator.StringToHash("HasKnife");

        // The controller has the weapon-swap and swing STATES but no transitions
        // feeding them, so those are driven directly by name via CrossFade instead
        // of through triggers. The chains that already exist in the controller
        // (Gun_Unequip -> Knife_Equip, Knife_Unequip -> Gun_Equip -> IdleMovement)
        // still carry the rest of each swap.
        private static readonly int GunUnequipState = Animator.StringToHash("Gun_Unequip");
        private static readonly int KnifeEquipState = Animator.StringToHash("Knife_Equip");
        private static readonly int KnifeSwingState = Animator.StringToHash("Knife_Swing");
        private static readonly int KnifeUnequipState = Animator.StringToHash("Knife_Unequip");
        private static readonly int GunEquipState = Animator.StringToHash("Gun_Equip");
        private static readonly int GunShootState = Animator.StringToHash("Gun_Shoot");

        private Weapon _current = Weapon.Gun;

        [SerializeField] private Camera playerCamera;
        [SerializeField] private LayerMask wallMask = ~0;

        [SerializeField] private float wallDistance = 0.75f;
        [SerializeField] private float sphereRadius = 0.12f;
        [SerializeField] private float facingThreshold = 0.8f;
        [SerializeField] private float wallBlendSpeed = 8f;

        [Header("Weapon Switching")]
        [Tooltip("Scroll the wheel to swap between gun and knife.")]
        [SerializeField] private bool scrollToSwitch = true;
        [Tooltip("Minimum scroll magnitude that counts as one notch.")]
        [SerializeField] private float scrollDeadzone = 0.1f;
        [Tooltip("Length of root_Gun_Unequip. The models swap when it finishes.")]
        [SerializeField] private float gunUnequipTime = 0.483f;
        [Tooltip("Length of root_Knife_Equip.")]
        [SerializeField] private float knifeEquipTime = 0.733f;
        [Tooltip("Length of root_Knife_Unequip. The models swap when it finishes.")]
        [SerializeField] private float knifeUnequipTime = 0.567f;
        [Tooltip("Length of root_Gun_Equip.")]
        [SerializeField] private float gunEquipTime = 0.983f;
        [Tooltip("Length of root_Knife_Swing.")]
        [SerializeField] private float knifeSwingTime = 0.983f;
        [Tooltip("Safety cap on the fire gate, in case Gun_Shoot is renamed or missing. " +
                 "The real rate of fire comes from the animation itself, not this value.")]
        [SerializeField] private float gunShootTimeout = 2f;
        [SerializeField] private float swapCrossFade = 0.08f;

        private static readonly int WallProximityParam =
            Animator.StringToHash("WallProximity");

        private float wallBlend;

        private bool switching;
        private bool swinging;
        private bool shooting;

        private void Reset()
        {
            animator = GetComponentInChildren<Animator>();
            playerBody = GetComponent<Rigidbody>();
        }

        private void Awake()
        {
            if (playerCamera == null)
                playerCamera = Camera.main;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if(playerBody==null) playerBody = GetComponent<Rigidbody>();
            SetModelsActive();
        }

        private void Update()
        {
            UpdateSpeed();
            UpdateWallProximity();

            // Menus and the examine view own the scroll wheel and the mouse.
            if (PlayerControlGate.Locked)
                return;

            HandleSwitchInput();

            // Ignore weapon actions while a swap animation is playing.
            if (switching)
                return;

            if (InputCombat.FirePressed)
            {
                if (_current == Weapon.Gun) FireGun();
                else SwingKnife();
            }

            if (_current == Weapon.Gun && InputCombat.ReloadPressed) animator.SetTrigger(ReloadParam);
            if (_current == Weapon.Gun && InputCombat.InspectPressed) animator.SetTrigger(InspectParam);
            if (InputCombat.TouchPressed) animator.SetTrigger(TouchParam);

            if (_current == Weapon.Gun && InputCombat.EquipKnifePressed) EquipKnife();
            else if (_current == Weapon.Knife && InputCombat.EquipGunPressed) EquipGun();
        }

        /// <summary>Any scroll notch toggles between the two weapons.</summary>
        private void HandleSwitchInput()
        {
            if (!scrollToSwitch || switching)
                return;

            if (Mathf.Abs(InputCombat.ScrollDelta) < scrollDeadzone)
                return;

            if (_current == Weapon.Gun) EquipKnife();
            else EquipGun();
        }

        private void UpdateWallProximity()
        {
            if (animator == null || playerCamera == null)
                return;

            bool nearWall = false;

            Ray ray = new Ray(
                playerCamera.transform.position,
                playerCamera.transform.forward);

            if (Physics.SphereCast(
                    ray,
                    sphereRadius,
                    out RaycastHit hit,
                    wallDistance,
                    wallMask,
                    QueryTriggerInteraction.Ignore))
            {
                float facing =
                    Vector3.Dot(playerCamera.transform.forward, -hit.normal);

                nearWall = facing > facingThreshold;
            }

            float target = nearWall ? 1f : 0f;

            float current = animator.GetFloat(WallProximityParam);

// Raise the weapon quickly, lower it more slowly.
            float dampTime = target > current ? 0.08f : 0.18f;

            animator.SetFloat(
                WallProximityParam,
                target,
                dampTime,
                Time.deltaTime);
        }

        private void UpdateSpeed()
        {
            if (animator == null) return;

            float speed = 0f;

            if (playerBody != null)
            {
                Vector3 v = playerBody.linearVelocity;
                v.y = 0f;
                speed = v.magnitude;
            }

            // Faster when accelerating, slower when decelerating.
            float currentAnimSpeed = animator.GetFloat(SpeedParam);
            float dampTime = speed > currentAnimSpeed ? 0.06f : 0.1f;

            animator.SetFloat(SpeedParam, speed, dampTime, Time.deltaTime);
        }

        public void EquipKnife()
        {
            if (switching || _current == Weapon.Knife) return;
            StartCoroutine(SwapToKnife());
        }

        public void EquipGun()
        {
            if (switching || _current == Weapon.Gun) return;
            StartCoroutine(SwapToGun());
        }

        /// <summary>
        /// Plays Gun_Unequip and hands off to Knife_Equip. The models are swapped on
        /// the handoff itself, when the hand is at the bottom of its arc and both
        /// weapons are out of frame.
        /// </summary>
        private IEnumerator SwapToKnife()
        {
            switching = true;
            swinging = false;
            shooting = false;

            animator.SetTrigger(EquipKnifeParam);
            animator.CrossFadeInFixedTime(GunUnequipState, swapCrossFade, 0);

            yield return WaitForHandoffTo(KnifeEquipState, swapCrossFade + gunUnequipTime + 0.5f);

            _current = Weapon.Knife;
            SetModelsActive();

            yield return new WaitForSeconds(knifeEquipTime);

            switching = false;
        }

        /// <summary>
        /// Plays Knife_Unequip and hands off to Gun_Equip, swapping the models on the
        /// handoff for the same reason as above.
        /// </summary>
        private IEnumerator SwapToGun()
        {
            switching = true;
            swinging = false;
            shooting = false;

            animator.SetTrigger(EquipGunParam);
            animator.CrossFadeInFixedTime(KnifeUnequipState, swapCrossFade, 0);

            yield return WaitForHandoffTo(GunEquipState, swapCrossFade + knifeUnequipTime + 0.5f);

            _current = Weapon.Gun;
            SetModelsActive();

            yield return new WaitForSeconds(gunEquipTime);

            switching = false;
        }

        /// <summary>
        /// Blocks until the animator begins handing over to <paramref name="toState"/>.
        ///
        /// Waiting a fixed number of seconds is not reliable here: the wall clock also
        /// covers the cross-fade into the unequip clip, so a hardcoded delay fires
        /// while the weapon is still on screen and the swap is visible. Watching the
        /// state machine instead pins the swap to the exact frame the hand reaches the
        /// bottom of the unequip, whatever the blend or clip length happens to be.
        /// The timeout is only a safety net against a renamed or missing state.
        /// </summary>
        private IEnumerator WaitForHandoffTo(int toState, float timeout)
        {
            float elapsed = 0f;

            while (elapsed < timeout)
            {
                if (animator.IsInTransition(0))
                {
                    if (animator.GetNextAnimatorStateInfo(0).shortNameHash == toState)
                        yield break;
                }
                else if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash == toState)
                {
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private void FireGun()
        {
            if (shooting) return;
            StartCoroutine(FireRoutine());
        }

        /// <summary>
        /// One shot. The hand animator and the gun's own animator are triggered on the
        /// same frame so the slide cycles with the recoil rather than drifting out of
        /// step, and no further shot is accepted until the hand animation has played
        /// out. Without the gate, every click re-entered the gun's Any -> GunAnim
        /// transition and restarted the slide mid-cycle.
        /// </summary>
        private IEnumerator FireRoutine()
        {
            shooting = true;

            animator.SetTrigger(ShootParam);
            if (gunAnim != null) gunAnim.SetTrigger(ShootParam);
            if (cameraFeel != null) cameraFeel.ShakeShoot();

            yield return WaitForStateToFinish(GunShootState, gunShootTimeout);

            // A trigger that never found a transition stays latched on the animator and
            // fires the moment one opens up, which shows as a phantom extra shot. The
            // hands cannot reach Gun_Shoot from the WallProximity state, so this really
            // does happen when firing while pressed against a wall.
            animator.ResetTrigger(ShootParam);
            if (gunAnim != null) gunAnim.ResetTrigger(ShootParam);

            shooting = false;
        }

        /// <summary>
        /// Waits for a state to be entered and then played to its end. The rate of fire
        /// therefore follows the clip itself, so retiming root_Gun_Shoot retimes the
        /// gun with no code change. The timeout only guards against a missing state.
        /// </summary>
        private IEnumerator WaitForStateToFinish(int stateHash, float timeout)
        {
            float elapsed = 0f;
            bool entered = false;

            while (elapsed < timeout)
            {
                var cur = animator.GetCurrentAnimatorStateInfo(0);
                bool inState = cur.shortNameHash == stateHash;

                if (!entered)
                {
                    if (inState) entered = true;
                }
                else if (!inState || cur.normalizedTime >= 1f)
                {
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private void SwingKnife()
        {
            if (swinging) return;
            if (cameraFeel != null) cameraFeel.ShakeSwing();
            StartCoroutine(SwingRoutine());
        }

        /// <summary>
        /// The Swing trigger is wired from the knife movement states, and the
        /// controller returns to knife idle on its own once the swing ends. This
        /// only holds the re-swing lock for the length of the animation.
        /// </summary>
        private IEnumerator SwingRoutine()
        {
            swinging = true;

            animator.SetTrigger(SwingParam);

            yield return new WaitForSeconds(knifeSwingTime);

            swinging = false;
        }

        private void SetModelsActive()
        {
            if (gunModel != null) gunModel.SetActive(_current == Weapon.Gun);
            if (knifeModel != null) knifeModel.SetActive(_current == Weapon.Knife);
            if (animator != null) animator.SetBool(HasKnifeParam, _current == Weapon.Knife);
        }
    }
}

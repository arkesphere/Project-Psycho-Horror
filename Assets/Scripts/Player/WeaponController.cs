using UnityEngine;

namespace SurvivalHorror
{
    /// <summary>
    /// Drives the first-person hand Animator (walk/run blend, shoot, reload, inspect,
    /// swing) and shows/hides the gun and knife models on weapon swap.
    /// Put this on the hand rig object that carries the Animator.
    /// </summary>
    public class WeaponController : MonoBehaviour
    {
        private enum Weapon { Gun, Knife }

        [Header("References")]
        [SerializeField] private Animator animator;
        [SerializeField] private GameObject gunModel;
        [SerializeField] private GameObject knifeModel;
        [Tooltip("Used to derive the Speed parameter for the walk/run blend.")]
        [SerializeField] private Rigidbody playerBody;

        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int ShootParam = Animator.StringToHash("Shoot");
        private static readonly int ReloadParam = Animator.StringToHash("Reload");
        private static readonly int InspectParam = Animator.StringToHash("Inspect");
        private static readonly int SwingParam = Animator.StringToHash("Swing");
        private static readonly int TouchParam = Animator.StringToHash("Touch");
        private static readonly int EquipKnifeParam = Animator.StringToHash("EquipKnife");
        private static readonly int EquipGunParam = Animator.StringToHash("EquipGun");

        private Weapon _current = Weapon.Gun;

        private void Reset()
        {
            animator = GetComponent<Animator>();
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            SetModelsActive();
        }

        private void Update()
        {
            UpdateSpeed();

            if (InputCompat.FirePressed)
                animator.SetTrigger(_current == Weapon.Gun ? ShootParam : SwingParam);

            if (_current == Weapon.Gun && InputCompat.ReloadPressed) animator.SetTrigger(ReloadParam);
            if (_current == Weapon.Gun && InputCompat.InspectPressed) animator.SetTrigger(InspectParam);
            if (InputCompat.TouchPressed) animator.SetTrigger(TouchParam);

            if (_current == Weapon.Gun && InputCompat.EquipKnifePressed) EquipKnife();
            else if (_current == Weapon.Knife && InputCompat.EquipGunPressed) EquipGun();
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
            animator.SetFloat(SpeedParam, speed);
        }

        public void EquipKnife()
        {
            _current = Weapon.Knife;
            animator.SetTrigger(EquipKnifeParam);
            SetModelsActive();
        }

        public void EquipGun()
        {
            _current = Weapon.Gun;
            animator.SetTrigger(EquipGunParam);
            SetModelsActive();
        }

        private void SetModelsActive()
        {
            if (gunModel != null) gunModel.SetActive(_current == Weapon.Gun);
            if (knifeModel != null) knifeModel.SetActive(_current == Weapon.Knife);
        }
    }
}

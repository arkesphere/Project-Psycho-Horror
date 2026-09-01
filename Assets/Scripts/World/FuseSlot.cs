using UnityEngine;

namespace SurvivalHorror
{
    /// <summary>
    /// The empty socket in the fuse box. Interacting with a fuse in the inventory
    /// seats it and restores mains power.
    ///
    /// The slot is only interactable once the fuse box lid is open, so the player
    /// cannot fit a fuse through a shut door.
    ///
    /// Put this on the FuseHolder, which already carries the collider.
    /// </summary>
    public class FuseSlot : MonoBehaviour, IInteractable
    {
        [Header("Requirement")]
        [Tooltip("The item that has to be in the inventory to fill this slot.")]
        [SerializeField] private ItemData requiredFuse;
        [Tooltip("The lid this slot sits behind. The slot stays inert until it opens.")]
        [SerializeField] private LidContainer lid;

        [Header("Result")]
        [Tooltip("The seated fuse mesh, hidden until the fuse is fitted.")]
        [SerializeField] private GameObject seatedFuse;
        [SerializeField] private HousePower power;

        [Header("Prompt")]
        [SerializeField] private string promptEmpty = "Insert Fuse";
        [SerializeField] private Vector3 promptOffset = new Vector3(0f, 0.15f, 0f);
        [Tooltip("Shown when the player has no fuse yet.")]
        [SerializeField] private string missingFuseMessage = "A fuse is missing.";

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip insertSound;

        private bool filled;

        public bool IsFilled => filled;

        private void Awake()
        {
            if (seatedFuse != null) seatedFuse.SetActive(false);
        }

        // ----- IInteractable -----

        /// <summary>
        /// Hidden entirely until the lid is open. Prompting the player to fit a fuse
        /// into a closed box would just be confusing.
        /// </summary>
        public bool CanInteract => !filled && (lid == null || lid.IsOpen);

        /// <summary>
        /// Anchored to the collider rather than the transform: the fuse-holder pivot
        /// sits at the bottom of the rail, which would float the prompt at knee height.
        /// </summary>
        public Vector3 PromptWorldPosition
        {
            get
            {
                var col = GetComponent<Collider>();
                Vector3 basePoint = col != null ? col.bounds.center : transform.position;
                return basePoint + promptOffset;
            }
        }

        public string GetPromptText() => promptEmpty;

        public void OnFocusEnter() { }
        public void OnFocusExit() { }
        public void OnProximityEnter() { }
        public void OnProximityExit() { }

        public void Interact(PlayerInteractor interactor)
        {
            if (filled) return;

            var inventory = interactor != null ? interactor.Inventory : null;
            if (requiredFuse == null || inventory == null) return;

            // Carrying the fuse is the gate. Without it, say so rather than failing
            // silently, or the player will not know what the socket wants.
            if (!inventory.Has(requiredFuse))
            {
                interactor.ShowMessage(missingFuseMessage);
                return;
            }

            inventory.TryRemove(requiredFuse);
            filled = true;

            if (seatedFuse != null) seatedFuse.SetActive(true);

            if (audioSource != null && insertSound != null)
                audioSource.PlayOneShot(insertSound);

            if (power != null) power.TurnOn();
        }
    }
}

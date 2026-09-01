using System.Collections;
using UnityEngine;

namespace SurvivalHorror
{
    /// <summary>
    /// A container with a hinged lid that opens once and reveals what is inside.
    ///
    /// Used for both the toolbox and the fuse box. The contents start disabled and are
    /// switched on partway through the lid animation rather than at the start, so the
    /// player never glimpses items through a lid that has not lifted yet.
    ///
    /// Put this on the object carrying the lid Animator.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class LidContainer : MonoBehaviour, IInteractable
    {
        [Header("References")]
        [SerializeField] private Animator animator;
        [Tooltip("Trigger on the lid's controller.")]
        [SerializeField] private string openTrigger = "Open";

        [Header("Contents")]
        [Tooltip("Revealed once the lid is open. Leave empty for a container that " +
                 "only needs to open.")]
        [SerializeField] private GameObject[] contents;
        [Tooltip("Seconds into the lid animation before the contents appear.")]
        [SerializeField] private float revealDelay = 0.7f;

        [Header("Prompt")]
        [SerializeField] private string prompt = "Open";
        [Tooltip("Where the interaction prompt floats. Defaults to this object.")]
        [SerializeField] private Transform promptAnchor;
        [SerializeField] private Vector3 promptOffset = new Vector3(0f, 0.25f, 0f);

        [Header("Inspect (optional)")]
        [Tooltip("Set this and interacting raises the container into the examine view " +
                 "instead of opening it outright. The player then has to find the lid " +
                 "and click it. The asset only carries the examine model — it never " +
                 "enters the inventory.")]
        [SerializeField] private ItemData examineData;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip openSound;
        [Range(0f, 1f)] [SerializeField] private float volume = 0.9f;

        private bool opened;
        private int triggerHash;
        private ItemExaminer examiner;

        /// <summary>True once the lid has been opened.</summary>
        public bool IsOpen => opened;

        private void Reset()
        {
            animator = GetComponent<Animator>();
            audioSource = GetComponent<AudioSource>();
        }

        private void Awake()
        {
            if (animator == null) animator = GetComponent<Animator>();
            triggerHash = Animator.StringToHash(openTrigger);

            // Hidden until the lid actually lifts.
            SetContentsActive(false);
        }

        private void SetContentsActive(bool on)
        {
            if (contents == null) return;
            foreach (var c in contents)
                if (c != null) c.SetActive(on);
        }

        // ----- IInteractable -----

        /// <summary>A lid only opens once; after that there is nothing to interact with.</summary>
        public bool CanInteract => !opened;

        public Vector3 PromptWorldPosition =>
            (promptAnchor != null ? promptAnchor.position : transform.position) + promptOffset;

        public string GetPromptText() => prompt;

        public void OnFocusEnter() { }
        public void OnFocusExit() { }
        public void OnProximityEnter() { }
        public void OnProximityExit() { }

        private void OnEnable()
        {
            if (examineData != null)
                EventBus.Subscribe<ExamineContainerEmptiedEvent>(OnEmptiedInExamineView);
        }

        private void OnDisable()
        {
            if (examineData != null)
                EventBus.Unsubscribe<ExamineContainerEmptiedEvent>(OnEmptiedInExamineView);
        }

        public void Interact(PlayerInteractor interactor)
        {
            if (opened) return;

            if (examineData != null)
            {
                // Hand it to the examine view; the lid opens only once the player
                // has turned the box over and clicked it.
                EventBus.Publish(new ItemExamineRequestedEvent(examineData));
                return;
            }

            Open();
        }

        /// <summary>
        /// The player emptied this container while holding it. The items are already
        /// in the inventory, so the world object only has to catch up: open its lid
        /// and stay open. Its contents are never revealed here — they were taken in
        /// the examine view, and showing them again would duplicate them.
        /// </summary>
        private void OnEmptiedInExamineView(ExamineContainerEmptiedEvent evt)
        {
            if (opened || evt.Item != examineData) return;

            opened = true;
            if (animator != null) animator.SetTrigger(triggerHash);
            if (audioSource != null && openSound != null) audioSource.PlayOneShot(openSound, volume);
        }

        /// <summary>Opens the lid. Public so a cutscene or script can force it.</summary>
        public void Open()
        {
            if (opened) return;
            opened = true;

            if (animator != null) animator.SetTrigger(triggerHash);

            if (audioSource != null && openSound != null)
                audioSource.PlayOneShot(openSound, volume);

            StartCoroutine(RevealAfterDelay());
        }

        private IEnumerator RevealAfterDelay()
        {
            if (revealDelay > 0f) yield return new WaitForSeconds(revealDelay);
            SetContentsActive(true);
        }
    }
}

using UnityEngine;

namespace SurvivalHorror
{
    /// <summary>
    /// Put this on any physical item lying in the level, together with a Collider.
    /// The collider's layer must be included in PlayerInteractor's interactionMask.
    /// </summary>
    [DisallowMultipleComponent]
    public class WorldItem : MonoBehaviour, IInteractable
    {
        [Header("Contents")]
        public ItemData itemData;
        [Min(1)] public int quantity = 1;

        [Header("Prompt")]
        [Tooltip("Verb shown before the item name, e.g. 'Take' -> \"Take Green Herb\".")]
        [SerializeField] private string verb = "Take";
        [Tooltip("Overrides the generated prompt entirely when non-empty.")]
        [SerializeField] private string promptOverride;
        [Tooltip("Where the floating key-hint box appears. Leave empty to auto-use the " +
                 "renderer bounds center with a small upward nudge.")]
        [SerializeField] private Transform promptAnchor;
        [SerializeField] private Vector3 promptAutoOffset = new Vector3(0f, 0.1f, 0f);

        [Header("Behaviour")]
        [Tooltip("Destroy the object on pickup. Disable if you pool your pickups.")]
        [SerializeField] private bool destroyOnPickup = true;
        [Tooltip("Idle spin, like the shimmering pickups in older survival horror games.")]
        [SerializeField] private bool idleSpin = false;
        [SerializeField] private float idleSpinSpeed = 35f;

        [Header("Focus Highlight (optional)")]
        [Tooltip("Boosts emission on these renderers while the crosshair is on the item and it's " +
                 "in interact range. Purely optional now — the arrow-to-E box already carries the " +
                 "'something's here' cue, so this is just a bit of extra pop if you want it. Leave " +
                 "the array empty to skip material highlighting entirely.")]
        [SerializeField] private Renderer[] highlightRenderers;
        [SerializeField, ColorUsage(false, true)] private Color highlightEmission = new Color(1f, 0.85f, 0.5f) * 2f;

        private static readonly int EmissiveColorId = Shader.PropertyToID("_EmissiveColor");
        private MaterialPropertyBlock _mpb;
        private bool _consumed;

        public bool CanInteract => !_consumed && itemData != null && quantity > 0;

        public Vector3 PromptWorldPosition
        {
            get
            {
                if (promptAnchor != null) return promptAnchor.position;

                if (highlightRenderers != null && highlightRenderers.Length > 0)
                {
                    Bounds b = highlightRenderers[0].bounds;
                    for (int i = 1; i < highlightRenderers.Length; i++)
                        if (highlightRenderers[i] != null) b.Encapsulate(highlightRenderers[i].bounds);
                    return b.center + promptAutoOffset;
                }

                return transform.position + promptAutoOffset;
            }
        }

        private void Reset()
        {
            highlightRenderers = GetComponentsInChildren<Renderer>();
        }

        private void Update()
        {
            if (idleSpin) transform.Rotate(Vector3.up, idleSpinSpeed * Time.deltaTime, Space.World);
        }

        public string GetPromptText()
        {
            if (!string.IsNullOrEmpty(promptOverride)) return promptOverride;
            if (itemData == null) return verb;

            string label = string.IsNullOrEmpty(itemData.displayName) ? itemData.name : itemData.displayName;
            return quantity > 1 ? $"{verb} {label} x{quantity}" : $"{verb} {label}";
        }

        public void OnFocusEnter() => SetHighlight(true);

        public void OnFocusExit() => SetHighlight(false);

        // Proximity feedback is handled entirely by the world-space arrow-to-E box
        // now (see WorldSpacePrompt / PlayerInteractor), not by the material. Left
        // as no-ops rather than removed from the interface, in case you want to add
        // a subtler proximity-only material cue later — the hook is already here.
        public void OnProximityEnter() { }
        public void OnProximityExit() { }

        public void Interact(PlayerInteractor interactor)
        {
            if (!CanInteract || interactor == null) return;

            var inventory = interactor.Inventory;
            if (inventory == null)
            {
                Debug.LogWarning($"{name}: PlayerInteractor has no Inventory assigned.", this);
                return;
            }

            bool tookEverything = inventory.TryAdd(itemData, quantity, out int leftover);

            if (!tookEverything && leftover == quantity)
            {
                interactor.ShowMessage("No more room.");
                return;
            }

            if (itemData.pickupSound != null)
                AudioSource.PlayClipAtPoint(itemData.pickupSound, transform.position, 0.9f);

            if (!tookEverything)
            {
                // Partial pickup: leave the remainder on the ground.
                quantity = leftover;
                interactor.ShowMessage("No more room.");
                interactor.RefreshPrompt();
                return;
            }

            _consumed = true;
            SetHighlight(false);

            // Raise the item up in front of the camera, RE-style, before it vanishes.
            if (itemData.examineOnPickup)
                EventBus.Publish(new ItemExamineRequestedEvent(itemData));

            interactor.NotifyInteractableConsumed(this);

            if (destroyOnPickup) Destroy(gameObject);
            else gameObject.SetActive(false);
        }

        private void SetHighlight(bool on)
        {
            if (highlightRenderers == null || highlightRenderers.Length == 0) return;
            _mpb ??= new MaterialPropertyBlock();

            for (int i = 0; i < highlightRenderers.Length; i++)
            {
                var r = highlightRenderers[i];
                if (r == null) continue;

                r.GetPropertyBlock(_mpb);
                _mpb.SetColor(EmissiveColorId, on ? highlightEmission : Color.black);
                r.SetPropertyBlock(_mpb);
            }
        }
    }
}

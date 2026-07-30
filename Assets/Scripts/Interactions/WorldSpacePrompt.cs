using TMPro;
using UnityEngine;

namespace SurvivalHorror
{
    public enum PromptState
    {
        Hidden,   // nothing nearby
        Far,      // proximity only — shows the arrow
        Near      // focused and in range — shows the E box
    }

    /// <summary>
    /// The RE7/8/9-style two-stage prompt: ONE box, floating in 3D space at the
    /// interactable's position, always facing the camera. From a distance it shows
    /// a small arrow inside the box; get close enough to actually interact and the
    /// arrow cross-fades into the key hint ("E"). Same frame throughout — only the
    /// icon inside it changes.
    ///
    /// One reusable instance, since only one interactable is ever the "current"
    /// target — moved and driven by PlayerInteractor via SetState.
    /// </summary>
    public class WorldSpacePrompt : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The World Space Canvas this component lives on.")]
        [SerializeField] private Canvas canvas;
        [Tooltip("Controls overall visibility — fades the whole box in/out.")]
        [SerializeField] private CanvasGroup canvasGroup;
        [Tooltip("The arrow icon shown from a distance (Far state).")]
        [SerializeField] private CanvasGroup arrowGroup;
        [Tooltip("The key-hint label shown up close (Near state).")]
        [SerializeField] private CanvasGroup keyGroup;
        [SerializeField] private TextMeshProUGUI keyLabel;

        [Header("Feel")]
        [SerializeField] private float overallFadeSpeed = 10f;
        [Tooltip("How fast the arrow and the key label cross-fade into each other.")]
        [SerializeField] private float crossfadeSpeed = 8f;
        [Tooltip("Small pop when a fresh target is picked up.")]
        [SerializeField] private float appearScaleStart = 0.75f;
        [SerializeField] private float appearSpeed = 14f;
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.05f, 0f);
        [Tooltip("Gentle up/down drift on the arrow only, while in the Far state — draws the eye from a distance without disturbing the box once you're close and reading the key.")]
        [SerializeField] private float arrowBobAmount = 0.03f;
        [SerializeField] private float arrowBobSpeed = 2.2f;

        private Transform _camTransform;
        private Vector3 _anchorPosition;
        private PromptState _state = PromptState.Hidden;

        private float _overallAlphaTarget;
        private float _arrowAlphaTarget;
        private float _keyAlphaTarget;

        private float _scaleTarget = 1f;
        private float _currentScale = 1f;

        private void Awake()
        {
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (arrowGroup != null) arrowGroup.alpha = 0f;
            if (keyGroup != null) keyGroup.alpha = 0f;
            _currentScale = appearScaleStart;
        }

        public void SetCamera(Camera cam)
        {
            _camTransform = cam != null ? cam.transform : null;
        }

        /// <summary>
        /// Single entry point. Call every frame with the desired state — Hidden when
        /// nothing is nearby, Far while merely in proximity, Near while focused and
        /// in interact range. Cheap to call repeatedly; only reacts on actual change.
        /// </summary>
        public void SetState(PromptState state, Vector3 worldPosition, string key = "E")
        {
            _anchorPosition = worldPosition;

            bool enteringFromHidden = _state == PromptState.Hidden && state != PromptState.Hidden;
            bool changed = _state != state;
            _state = state;

            if (enteringFromHidden)
            {
                transform.position = worldPosition + worldOffset;   // snap once, don't fly in from the old spot
                _currentScale = appearScaleStart;
            }

            if (changed && keyLabel != null && state == PromptState.Near) keyLabel.text = key;

            _overallAlphaTarget = state == PromptState.Hidden ? 0f : 1f;
            _arrowAlphaTarget = state == PromptState.Far ? 1f : 0f;
            _keyAlphaTarget = state == PromptState.Near ? 1f : 0f;
        }

        public void Hide() => SetState(PromptState.Hidden, _anchorPosition);

        private void LateUpdate()
        {
            if (_camTransform == null) return;

            float dt = Time.unscaledDeltaTime;

            Vector3 basePos = _anchorPosition + worldOffset;
            if (_state == PromptState.Far)
                basePos += Vector3.up * (Mathf.Sin(Time.unscaledTime * arrowBobSpeed) * arrowBobAmount);

            transform.position = basePos;

            // Billboard: face the camera dead-on so the label never renders backwards.
            transform.rotation = Quaternion.LookRotation(transform.position - _camTransform.position);

            if (canvasGroup != null)
                canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, _overallAlphaTarget, overallFadeSpeed * dt);
            if (arrowGroup != null)
                arrowGroup.alpha = Mathf.MoveTowards(arrowGroup.alpha, _arrowAlphaTarget, crossfadeSpeed * dt);
            if (keyGroup != null)
                keyGroup.alpha = Mathf.MoveTowards(keyGroup.alpha, _keyAlphaTarget, crossfadeSpeed * dt);

            _currentScale = Mathf.MoveTowards(_currentScale, _scaleTarget, appearSpeed * dt);
            transform.localScale = Vector3.one * _currentScale;
        }
    }
}

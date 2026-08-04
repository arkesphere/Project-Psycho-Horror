using TMPro;
using UnityEngine;

namespace SurvivalHorror
{
    /// <summary>
    /// Short transient screen notices only ("No more room."). The per-object
    /// "press E" hint is WorldSpacePrompt, anchored to the item in 3D space —
    /// this component no longer draws it.
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        [SerializeField] private CanvasGroup messageGroup;
        [SerializeField] private TextMeshProUGUI messageLabel;
        [SerializeField] private float fadeSpeed = 12f;

        private float _messageTarget;
        private float _messageHideAt;

        private void Awake()
        {
            if (messageGroup != null) messageGroup.alpha = 0f;
        }

        private void Update()
        {
            if (_messageTarget > 0f && Time.unscaledTime >= _messageHideAt) _messageTarget = 0f;

            if (messageGroup != null)
                messageGroup.alpha = Mathf.MoveTowards(messageGroup.alpha, _messageTarget, fadeSpeed * Time.unscaledDeltaTime);
        }

        public void ShowTemporaryMessage(string text, float duration = 1.8f)
        {
            if (messageLabel != null) messageLabel.text = text;
            _messageTarget = 1f;
            _messageHideAt = Time.unscaledTime + duration;
        }
    

private void HandleMessageRequested(InteractionMessageRequestedEvent gameEvent)
        {
            ShowTemporaryMessage(gameEvent.Text, gameEvent.Duration);
        }


private void OnDisable()
        {
            EventBus.Unsubscribe<InteractionMessageRequestedEvent>(HandleMessageRequested);
        }


private void OnEnable()
        {
            EventBus.Subscribe<InteractionMessageRequestedEvent>(HandleMessageRequested);
        }
}
}

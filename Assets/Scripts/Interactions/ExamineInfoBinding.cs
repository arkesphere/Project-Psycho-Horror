using TMPro;
using UnityEngine;

namespace SurvivalHorror
{
    /// <summary>
    /// Optional overlay shown during the examine view: item name, description and
    /// a control hint. Assign it to ItemExaminer's infoUI field.
    /// The root object is toggled, so keep it as a child panel of your HUD canvas.
    /// </summary>
    public class ExamineInfoBinding : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TextMeshProUGUI nameLabel;
        [SerializeField] private TextMeshProUGUI descriptionLabel;
        [SerializeField] private TextMeshProUGUI hintLabel;
        [SerializeField] private string hintText = "Drag to rotate   ·   Scroll to zoom   ·   Esc to close";

        private void Awake()
        {
            if (root == null) root = gameObject;
            EventBus.Subscribe<ItemExaminationChangedEvent>(HandleExaminationChanged);
            Clear();
        }

        public void Bind(ItemData item)
        {
            if (item == null) { Clear(); return; }

            if (nameLabel != null)
                nameLabel.text = string.IsNullOrEmpty(item.displayName) ? item.name : item.displayName;
            if (descriptionLabel != null)
                descriptionLabel.text = item.description;
            if (hintLabel != null)
                hintLabel.text = hintText;

            if (root != null) root.SetActive(true);
        }

        public void Clear()
        {
            if (root != null) root.SetActive(false);
        }
    

private void HandleExaminationChanged(ItemExaminationChangedEvent gameEvent)
        {
            if (gameEvent.IsExamining) Bind(gameEvent.Item);
            else Clear();
        }


private void OnDestroy()
        {
            EventBus.Unsubscribe<ItemExaminationChangedEvent>(HandleExaminationChanged);
        }
}
}

using System.Collections.Generic;
using TMPro;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SurvivalHorror
{
    public class InventoryMenuController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private Inventory inventory;

        [Header("UI")]
        [SerializeField] private GameObject menuRoot;
        [SerializeField] private InventoryPreviewCarousel previewCarousel;
        [SerializeField] private TextMeshProUGUI[] categoryLabels;
        [SerializeField] private TextMeshProUGUI itemNameLabel;
        [SerializeField] private TextMeshProUGUI itemDescriptionLabel;

        [Header("Input Actions")]
        [SerializeField] private InputActionReference inventoryAction;
        [SerializeField] private InputActionReference navigateAction;
        [SerializeField] private InputActionReference investigateAction;
        [SerializeField] private InputActionReference cancelAction;

        [Header("Navigation")]
        [SerializeField, Min(0.01f)] private float firstRepeatDelay = 0.35f;
        [SerializeField, Min(0.01f)] private float repeatDelay = 0.12f;
        [SerializeField] private Color selectedCategoryColor =
            new Color(0.85f, 0.82f, 0.70f, 1f);
        [SerializeField] private Color normalCategoryColor =
            new Color(0.45f, 0.45f, 0.42f, 1f);

        [Header("Menu Motion")]
        [SerializeField, Min(0.01f)] private float textTransitionDuration = 0.14f;
        [SerializeField, Min(0f)] private float textMoveDistance = 8f;
        [SerializeField, Min(0f)] private float textStagger = 0.015f;
        
        private static void EnableAction(InputActionReference actionReference)
        {
            if (actionReference != null && actionReference.action != null)
                actionReference.action.Enable();
        }

        private static readonly ItemCategory[] Categories =
        {
            ItemCategory.Weapon,
            ItemCategory.Medical,
            ItemCategory.KeyItem,
            ItemCategory.Story
        };

        private readonly List<ItemData> visibleItems = new List<ItemData>();

        private bool isOpen;
        private ItemData pendingExamineItem;
        
        private bool reopenAfterExamine;
        private bool navigationHeld;
        private float nextNavigationTime;
        private int categoryIndex;
        private int itemIndex;
        private Sequence menuTransition;

        private sealed class MenuTextState
        {
            public readonly TextMeshProUGUI Text;
            public readonly Vector2 RestingPosition;
            public readonly float RestingAlpha;

            public MenuTextState(TextMeshProUGUI text)
            {
                Text = text;
                RestingPosition = text.rectTransform.anchoredPosition;
                RestingAlpha = text.color.a;
            }
        }

        private readonly List<MenuTextState> menuTextStates =
            new List<MenuTextState>();

        private void Awake()
        {
            CacheMenuTextStates();
            ConfigurePointerControls();

            if (menuRoot != null)
                menuRoot.SetActive(false);

            RefreshCategoryLabels();
            ClearDetails();
        }
        
        private void ConfigurePointerControls()
        {
            for (int i = 0; i < categoryLabels.Length; i++)
            {
                if (categoryLabels[i] == null)
                    continue;

                categoryLabels[i].raycastTarget = true;

                InventoryPointerControl pointer =
                    categoryLabels[i].GetComponent<InventoryPointerControl>();

                if (pointer != null)
                    pointer.Configure(this, i, true);
            }

            Transform previous = menuRoot.transform.Find("ItemArea/PreviousItemPlaceholder");
            Transform next = menuRoot.transform.Find("ItemArea/NextItemPlaceholder");

            if (previous != null &&
                previous.TryGetComponent(out InventoryPointerControl previousPointer))
            {
                previousPointer.Configure(this, -1, false);
            }

            if (next != null &&
                next.TryGetComponent(out InventoryPointerControl nextPointer))
            {
                nextPointer.Configure(this, 1, false);
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<InventoryChangedEvent>(HandleInventoryChanged);
            EventBus.Subscribe<ItemExaminationChangedEvent>(HandleExaminationChanged);
            
            EnableAction(inventoryAction);
            EnableAction(navigateAction);
            EnableAction(investigateAction);
            EnableAction(cancelAction);

            if (inventoryAction != null)
                inventoryAction.action.performed += HandleInventoryAction;

            if (investigateAction != null)
                investigateAction.action.performed += HandleInvestigateAction;

            if (cancelAction != null)
                cancelAction.action.performed += HandleCancelAction;
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<InventoryChangedEvent>(HandleInventoryChanged);
            EventBus.Unsubscribe<ItemExaminationChangedEvent>(HandleExaminationChanged);

            if (inventoryAction != null)
                inventoryAction.action.performed -= HandleInventoryAction;

            if (investigateAction != null)
                investigateAction.action.performed -= HandleInvestigateAction;

            if (cancelAction != null)
                cancelAction.action.performed -= HandleCancelAction;
        }

        private void Update()
        {
            HandleNavigation();
        }

        private void HandleInventoryAction(InputAction.CallbackContext context)
        {
            if (isOpen)
                Close();
            else
                Open();
        }

private void HandleInvestigateAction(InputAction.CallbackContext context)
        {
            if (!isOpen || visibleItems.Count == 0)
                return;

            reopenAfterExamine = true;
            pendingExamineItem = visibleItems[itemIndex];
            Close();
        }

        private void HandleCancelAction(InputAction.CallbackContext context)
        {
            if (isOpen)
                Close();
        }

        private void HandleNavigation()
        {
            if (!isOpen || navigateAction == null || navigateAction.action == null)
                return;

            Vector2 input = navigateAction.action.ReadValue<Vector2>();

            if (input.sqrMagnitude < 0.25f)
            {
                navigationHeld = false;
                return;
            }

            if (Time.unscaledTime < nextNavigationTime)
                return;

            if (Mathf.Abs(input.y) >= Mathf.Abs(input.x))
            {
                ChangeCategory(input.y > 0f ? -1 : 1);
            }
            else
            {
                ChangeItem(input.x > 0f ? 1 : -1);
            }

            nextNavigationTime = Time.unscaledTime +
                (navigationHeld ? repeatDelay : firstRepeatDelay);

            navigationHeld = true;
        }

public void Open()
        {
            if (isOpen || inventory == null || PlayerControlGate.Locked)
                return;

            isOpen = true;
            menuRoot.SetActive(true);

            PlayerControlGate.Push();

            RebuildVisibleItems();
            RefreshCategoryLabels();
            PlayOpenAnimation();
        }

public void Close()
        {
            if (!isOpen)
                return;

            isOpen = false;
            navigationHeld = false;
            PlayCloseAnimation();
        }

        private void ChangeCategory(int direction)
        {
            categoryIndex = PositiveModulo(
                categoryIndex + direction,
                Categories.Length
            );

            itemIndex = 0;
            RebuildVisibleItems();
            RefreshCategoryLabels();
        }

        private void ChangeItem(int direction)
        {
            if (visibleItems.Count == 0)
                return;

            itemIndex = PositiveModulo(
                itemIndex + direction,
                visibleItems.Count
            );

            previewCarousel.Select(itemIndex);
            RefreshDetails();
        }

        private void RebuildVisibleItems()
        {
            visibleItems.Clear();

            if (inventory != null)
            {
                ItemCategory activeCategory = Categories[categoryIndex];

                for (int i = 0; i < inventory.Slots.Count; i++)
                {
                    InventorySlot slot = inventory.Slots[i];

                    if (slot.IsEmpty || slot.item.category != activeCategory)
                        continue;

                    if (!visibleItems.Contains(slot.item))
                        visibleItems.Add(slot.item);
                }
            }

            visibleItems.Sort((first, second) =>
            {
                int orderComparison =
                    first.inventorySortOrder.CompareTo(second.inventorySortOrder);

                if (orderComparison != 0)
                    return orderComparison;

                return string.Compare(
                    first.displayName,
                    second.displayName,
                    System.StringComparison.Ordinal
                );
            });

            itemIndex = visibleItems.Count == 0
                ? 0
                : Mathf.Clamp(itemIndex, 0, visibleItems.Count - 1);

            previewCarousel.Rebuild(visibleItems, itemIndex);
            RefreshDetails();
        }

        private void RefreshCategoryLabels()
        {
            int count = Mathf.Min(categoryLabels.Length, Categories.Length);

            for (int i = 0; i < count; i++)
            {
                if (categoryLabels[i] != null)
                    categoryLabels[i].color = i == categoryIndex
                        ? selectedCategoryColor
                        : normalCategoryColor;
            }
        }

private void RefreshDetails()
        {
            if (visibleItems.Count == 0)
            {
                ClearDetails();
                return;
            }

            SetDetailsVisible(true);

            ItemData selectedItem = visibleItems[itemIndex];
            int quantity = inventory.CountOf(selectedItem);

            string displayName = string.IsNullOrEmpty(selectedItem.displayName)
                ? selectedItem.name
                : selectedItem.displayName;

            if (itemNameLabel != null)
                itemNameLabel.text = quantity > 1
                    ? $"{displayName}  x{quantity}"
                    : displayName;

            if (itemDescriptionLabel != null)
                itemDescriptionLabel.text = selectedItem.description;

            RefreshPointerNavigation();
        }

private void ClearDetails()
        {
            SetDetailsVisible(false);
            RefreshPointerNavigation();
        }

private void CacheMenuTextStates()
        {
            menuTextStates.Clear();

            if (menuRoot == null)
                return;

            TextMeshProUGUI[] texts =
                menuRoot.GetComponentsInChildren<TextMeshProUGUI>(true);

            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null)
                    menuTextStates.Add(new MenuTextState(texts[i]));
            }
        }

private void PlayOpenAnimation()
        {
            menuTransition?.Kill();
            menuTransition = DOTween.Sequence().SetUpdate(true);

            for (int i = 0; i < menuTextStates.Count; i++)
            {
                MenuTextState state = menuTextStates[i];
                if (state.Text == null)
                    continue;

                state.Text.rectTransform.anchoredPosition =
                    state.RestingPosition + Vector2.down * textMoveDistance;

                Color color = state.Text.color;
                color.a = 0f;
                state.Text.color = color;

                float delay = i * textStagger;
                menuTransition.Insert(delay,
                    FadeText(state.Text, state.RestingAlpha, textTransitionDuration)
                        .SetEase(Ease.OutQuad));
                menuTransition.Insert(delay,
                    MoveText(state.Text.rectTransform, state.RestingPosition,
                        textTransitionDuration).SetEase(Ease.OutCubic));
            }
        }

private void PlayCloseAnimation()
        {
            menuTransition?.Kill();

            if (menuTextStates.Count == 0)
            {
                FinishClose();
                return;
            }

            menuTransition = DOTween.Sequence().SetUpdate(true);

            for (int i = 0; i < menuTextStates.Count; i++)
            {
                MenuTextState state = menuTextStates[i];
                if (state.Text == null)
                    continue;

                float delay = i * textStagger;
                Vector2 exitPosition =
                    state.RestingPosition + Vector2.down * textMoveDistance;

                menuTransition.Insert(delay,
                    FadeText(state.Text, 0f, textTransitionDuration)
                        .SetEase(Ease.InQuad));
                menuTransition.Insert(delay,
                    MoveText(state.Text.rectTransform, exitPosition,
                        textTransitionDuration).SetEase(Ease.InCubic));
            }

            menuTransition.OnComplete(FinishClose);
        }

private void FinishClose()
        {
            if (menuRoot != null)
                menuRoot.SetActive(false);

            menuTransition = null;
            PlayerControlGate.Pop();

            if (pendingExamineItem == null)
                return;

            ItemData itemToExamine = pendingExamineItem;
            pendingExamineItem = null;
            EventBus.Publish(new ItemExamineRequestedEvent(itemToExamine));
        }

private static Tween FadeText(
            TextMeshProUGUI text,
            float targetAlpha,
            float duration)
        {
            return DOTween.To(
                () => text.color.a,
                alpha =>
                {
                    Color color = text.color;
                    color.a = alpha;
                    text.color = color;
                },
                targetAlpha,
                duration);
        }

        private static Tween MoveText(
            RectTransform target,
            Vector2 targetPosition,
            float duration)
        {
            return DOTween.To(
                () => target.anchoredPosition,
                position => target.anchoredPosition = position,
                targetPosition,
                duration);
        }



        private void HandleInventoryChanged(InventoryChangedEvent gameEvent)
        {
            if (isOpen && gameEvent.Inventory == inventory)
                RebuildVisibleItems();
        }

private void HandleExaminationChanged(ItemExaminationChangedEvent gameEvent)
        {
            if (!reopenAfterExamine || gameEvent.IsExamining)
                return;

            reopenAfterExamine = false;
            Open();
        }


        private static int PositiveModulo(int value, int divisor)
        {
            return (value % divisor + divisor) % divisor;
        }
    

private void SetDetailsVisible(bool visible)
        {
            if (itemNameLabel != null)
                itemNameLabel.gameObject.SetActive(visible);

            if (itemDescriptionLabel != null)
                itemDescriptionLabel.gameObject.SetActive(visible);
        }


public void SelectCategoryFromPointer(int newCategoryIndex)
        {
            if (!isOpen || newCategoryIndex < 0 || newCategoryIndex >= Categories.Length)
                return;

            categoryIndex = newCategoryIndex;
            itemIndex = 0;
            RebuildVisibleItems();
            RefreshCategoryLabels();
        }

        public void ChangeItemFromPointer(int direction)
        {
            Debug.Log($"Direction: {direction}");

            if (!isOpen)
                return;

            ChangeItem(direction);
        }

private void RefreshPointerNavigation()
        {
            if (menuRoot == null)
                return;

            Transform previous = menuRoot.transform.Find(
                "ItemArea/PreviousItemPlaceholder");
            Transform next = menuRoot.transform.Find(
                "ItemArea/NextItemPlaceholder");

            bool hasMultipleItems = visibleItems.Count > 1;

            if (previous != null)
                previous.gameObject.SetActive(hasMultipleItems && itemIndex > 0);

            if (next != null)
                next.gameObject.SetActive(
                    hasMultipleItems && itemIndex < visibleItems.Count - 1);
        }
}
}
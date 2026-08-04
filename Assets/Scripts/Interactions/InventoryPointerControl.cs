using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SurvivalHorror
{
    public sealed class InventoryPointerControl : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private InventoryMenuController menu;
        private int value;
        private bool isCategory;
        private Vector3 restingScale;
        private Tween scaleTween;

        private void Awake()
        {
            restingScale = transform.localScale;
        }

        public void Configure(InventoryMenuController targetMenu, int targetValue,
            bool targetsCategory)
        {
            menu = targetMenu;
            value = targetValue;
            isCategory = targetsCategory;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            AnimateScale(1.06f);

            if (isCategory)
                menu?.SelectCategoryFromPointer(value);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            AnimateScale(1f);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (isCategory)
                menu?.SelectCategoryFromPointer(value);
            else
                menu?.ChangeItemFromPointer(value);
        }

        private void OnDisable()
        {
            scaleTween?.Kill();
            transform.localScale = restingScale;
        }

        private void AnimateScale(float multiplier)
        {
            scaleTween?.Kill();

            scaleTween = transform
                .DOScale(restingScale * multiplier, 0.09f)
                .SetUpdate(true);
        }
        
    }
}
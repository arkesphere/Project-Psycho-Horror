using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;


namespace SurvivalHorror
{
    public class InventoryPreviewCarousel : MonoBehaviour
    {
        [Header("References")] 
        [SerializeField]
        private Transform carouselRoot;
        
        [Header("Layout")]
        [SerializeField, Min(0.1f)] private float itemSpacing = 1.15f;
        [SerializeField, Min(0.01f)] private float targetModelSize = 1.1f;
        [SerializeField, Range(0.1f, 1f)] private float neighbourScale = 0.68f;
        [SerializeField, Min(1f)] private float selectedScale = 1.05f;
        [Tooltip("Metres the unselected items are pushed away from the camera. The " +
                 "carousel is otherwise flat, so depth of field has no depth to work " +
                 "with and the neighbours stay as sharp as the selected item.")]
        [SerializeField, Min(0f)] private float neighbourDepthOffset = 0.9f;

        [Header("Motion")]
        [Tooltip("Seconds to slide between items. 0 snaps.")]
        [SerializeField, Min(0f)] private float transitionDuration = 0.28f;
        [SerializeField] private Ease transitionEase = Ease.OutCubic;

        [Header("HDRP Lighting")]
        [Tooltip("HDRP Light Layer used by preview models. Match the preview lights to this layer so scene lighting cannot wash previews out.")]
        [SerializeField, Range(-1, 31)] private int previewRenderingLayer = 1;
        
        private readonly List<Transform> slots = new List<Transform>();
        private int selectedIndex = -1;

        public int Count => slots.Count;
        public int SelectedIndex => selectedIndex;

        public void Rebuild(IReadOnlyList<ItemData> items, int newSelectedIndex = 0)
        {
            Clear();

            if (items == null || items.Count == 0) return;

            if (carouselRoot == null) return;

            for (int i = 0; i < items.Count; i++)
            {
                ItemData item = items[i];
                GameObject prefab = item != null ? item.ExamineModel : null;
                
                if(prefab==null) continue;

                GameObject slotObject = new GameObject($"PreviewSlot_{i}_{item.name}");
                slotObject.layer = carouselRoot.gameObject.layer;

                Transform slot = slotObject.transform;
                slot.SetParent(carouselRoot, false);
                
                GameObject model = Instantiate(prefab, slot);
                model.name = $"Preview_{item.name}";
                
                
                DisableGameplayParts(model);
                SetLayerRecursively(model.transform, carouselRoot.gameObject.layer);
                FitAndCentre(model, slot, item);

                slots.Add(slot);

            }
            Select(newSelectedIndex, true);
        }
        
        public void Select(int newSelectedIndex)
        {
            Select(newSelectedIndex, false);
        }

        /// <summary>
        /// Moves the carousel to an item. Pass <paramref name="instant"/> when the
        /// list has just been rebuilt — the slots are new and have no previous
        /// position worth animating from.
        /// </summary>
        public void Select(int newSelectedIndex, bool instant)
        {
            if (slots.Count == 0)
            {
                selectedIndex = -1;
                return;
            }

            selectedIndex = Mathf.Clamp(newSelectedIndex, 0, slots.Count - 1);

            for (int i = 0; i < slots.Count; i++)
            {
                bool selected = i == selectedIndex;

                Vector3 targetPosition = new Vector3(
                    (i - selectedIndex) * itemSpacing,
                    0f,
                    selected ? 0f : neighbourDepthOffset
                );

                Vector3 targetScale = Vector3.one *
                                      (selected ? selectedScale : neighbourScale);

                slots[i].DOKill();

                if (instant || transitionDuration <= 0f)
                {
                    slots[i].localPosition = targetPosition;
                    slots[i].localScale = targetScale;
                    continue;
                }

                // Unscaled: the menu holds the game at timeScale 0 while it is open.
                slots[i].DOLocalMove(targetPosition, transitionDuration)
                    .SetEase(transitionEase)
                    .SetUpdate(true);

                slots[i].DOScale(targetScale, transitionDuration)
                    .SetEase(transitionEase)
                    .SetUpdate(true);
            }
        }
        
        public void Clear()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] == null) continue;

                // Kill first: a tween still driving a destroyed transform throws.
                slots[i].DOKill();
                Destroy(slots[i].gameObject);
            }

            slots.Clear();
            selectedIndex = -1;
        }
        
        private void OnDestroy()
        {
            Clear();
        }
        
        private static Bounds GetWorldBounds(Renderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;

            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return bounds;
        }
        
        private void FitAndCentre(GameObject model, Transform slot, ItemData item)
        {
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(item.inventoryRotationOffset);
            model.transform.localScale = Vector3.one;

            Renderer[] renderers = model.GetComponentsInChildren<Renderer>();

            if (previewRenderingLayer >= 0)
            {
                uint mask = 1u << previewRenderingLayer;

                for (int i = 0; i < renderers.Length; i++)
                    renderers[i].renderingLayerMask = mask;
            }

            if (renderers.Length == 0)
                return;

            Bounds bounds = GetWorldBounds(renderers);

            float largestDimension = Mathf.Max(
                bounds.size.x,
                Mathf.Max(bounds.size.y, bounds.size.z)
            );

            float scale = targetModelSize / Mathf.Max(largestDimension, 0.0001f);
            scale *= Mathf.Max(0.01f, item.examineScaleMultiplier);

            model.transform.localScale = Vector3.one * scale;

            bounds = GetWorldBounds(renderers);
            Vector3 centreInSlotSpace = slot.InverseTransformPoint(bounds.center);

            model.transform.localPosition -= centreInSlotSpace;
        }
        
        private static void DisableGameplayParts(GameObject model)
        {
            foreach (Rigidbody body in model.GetComponentsInChildren<Rigidbody>(true))
            {
                body.isKinematic = true;
                body.detectCollisions = false;
            }

            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;

            foreach (WorldItem worldItem in model.GetComponentsInChildren<WorldItem>(true))
                worldItem.enabled = false;
        }
        
        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;

            for (int i = 0; i < root.childCount; i++)
                SetLayerRecursively(root.GetChild(i), layer);
        }


    }
}

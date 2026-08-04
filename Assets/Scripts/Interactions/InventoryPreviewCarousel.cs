using System.Collections.Generic;
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
            Select(newSelectedIndex);
        }
        
        public void Select(int newSelectedIndex)
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

                slots[i].localPosition = new Vector3(
                    (i - selectedIndex) * itemSpacing,
                    0f,
                    0f
                );

                slots[i].localScale = Vector3.one *
                                      (selected ? selectedScale : neighbourScale);
            }
        }
        
        public void Clear()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null)
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

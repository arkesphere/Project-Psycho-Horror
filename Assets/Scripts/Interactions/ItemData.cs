using UnityEngine;

namespace SurvivalHorror
{
    public enum ItemCategory
    {
        Consumable,
        KeyItem,
        Weapon,
        Ammo,
        Ingredient,
        Document,
        Treasure
    }

    /// <summary>
    /// Data-only definition of an item. One asset per item type; the same asset is
    /// shared by every world instance and every inventory slot holding that item.
    /// Create via: Assets > Create > Survival Horror > Item Data
    /// </summary>
    [CreateAssetMenu(fileName = "Item_New", menuName = "Survival Horror/Item Data")]
    public class ItemData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable string used for saves and comparisons. Auto-filled from the asset name.")]
        public string itemId;
        public string displayName = "New Item";
        [TextArea(2, 5)] public string description;
        public Sprite icon;
        public ItemCategory category = ItemCategory.KeyItem;

        [Header("Stacking")]
        public bool stackable = false;
        [Min(1)] public int maxStack = 1;

        [Header("Prefabs")]
        [Tooltip("Spawned when the item is dropped back into the world.")]
        public GameObject worldPrefab;
        [Tooltip("High-detail model used in the examine view. Falls back to worldPrefab if empty.")]
        public GameObject examinePrefab;

        [Header("Examine View")]
        [Tooltip("Starting rotation so the item faces the camera the way you want.")]
        public Vector3 examineRotationOffset;
        [Tooltip("Fine-tune how large the item appears. 1 = auto-fit to the examiner's target size.")]
        [Min(0.05f)] public float examineScaleMultiplier = 1f;
        [Tooltip("Show the examine view automatically the moment this item is picked up.")]
        public bool examineOnPickup = true;

        [Header("Audio")]
        public AudioClip pickupSound;

        /// <summary>The model to show in the examine view.</summary>
        public GameObject ExamineModel => examinePrefab != null ? examinePrefab : worldPrefab;

        public int EffectiveMaxStack => stackable ? Mathf.Max(1, maxStack) : 1;

        /// <summary>Identity comparison that survives asset duplication.</summary>
        public bool Matches(ItemData other)
        {
            if (other == null) return false;
            if (ReferenceEquals(this, other)) return true;
            return !string.IsNullOrEmpty(itemId) && itemId == other.itemId;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(itemId)) itemId = name;
            if (!stackable) maxStack = 1;
        }
#endif
    }
}

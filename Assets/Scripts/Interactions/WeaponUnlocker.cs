using UnityEngine;

namespace SurvivalHorror
{
    /// <summary>
    /// Bridges the inventory to the weapon rig: picking the knife up out of the
    /// garage toolbox is what makes it equippable.
    ///
    /// This lives on the Interaction side rather than inside WeaponController
    /// because the player assembly cannot see ItemData without a circular assembly
    /// reference. WeaponController only exposes a plain UnlockKnife() call.
    ///
    /// Put this on the player root, next to Inventory and PlayerInteractor.
    /// </summary>
    public class WeaponUnlocker : MonoBehaviour
    {
        [SerializeField] private Inventory inventory;
        [SerializeField] private WeaponController weapons;
        [Tooltip("Picking this up makes the knife equippable.")]
        [SerializeField] private ItemData knifeItem;

        private void Awake()
        {
            if (inventory == null) inventory = GetComponentInChildren<Inventory>();
            if (weapons == null) weapons = GetComponentInChildren<WeaponController>(true);
        }

        private void OnEnable()
        {
            EventBus.Subscribe<InventoryItemChangedEvent>(OnInventoryItemChanged);

            // A save load or an inspector-seeded inventory would never fire the event,
            // so reconcile once on startup as well.
            Refresh();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<InventoryItemChangedEvent>(OnInventoryItemChanged);
        }

        private void OnInventoryItemChanged(InventoryItemChangedEvent evt) => Refresh();

        private void Refresh()
        {
            if (weapons == null || inventory == null || knifeItem == null) return;
            if (!weapons.KnifeUnlocked && inventory.Has(knifeItem)) weapons.UnlockKnife();
        }
    }
}

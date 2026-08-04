using System;
using System.Collections.Generic;
using UnityEngine;

namespace SurvivalHorror
{
    [Serializable]
    public class InventorySlot
    {
        public ItemData item;
        public int quantity;

        public bool IsEmpty => item == null || quantity <= 0;

        public void Clear()
        {
            item = null;
            quantity = 0;
        }
    }

    /// <summary>
    /// Fixed-capacity slot inventory. Put this on the player root alongside
    /// PlayerInteractor. UI systems subscribe to InventoryChangedEvent to redraw.
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        [SerializeField, Min(1)] private int capacity = 12;
        [SerializeField] private List<InventorySlot> slots = new List<InventorySlot>();

        public int Capacity => capacity;
        public IReadOnlyList<InventorySlot> Slots => slots;

        private void Awake()
        {
            ResizeToCapacity();
        }

        private void ResizeToCapacity()
        {
            while (slots.Count < capacity) slots.Add(new InventorySlot());
            while (slots.Count > capacity) slots.RemoveAt(slots.Count - 1);
        }

        /// <summary>
        /// Tries to take <paramref name="amount"/> of an item. Fills partial stacks
        /// first, then empty slots. Returns true only if everything fit;
        /// <paramref name="leftover"/> is what could not be stored.
        /// </summary>
        public bool TryAdd(ItemData item, int amount, out int leftover)
        {
            leftover = Mathf.Max(0, amount);
            if (item == null || leftover == 0) return false;

            int stackSize = item.EffectiveMaxStack;

            if (item.stackable)
            {
                for (int i = 0; i < slots.Count && leftover > 0; i++)
                {
                    var slot = slots[i];
                    if (slot.IsEmpty || !slot.item.Matches(item)) continue;

                    int space = stackSize - slot.quantity;
                    if (space <= 0) continue;

                    int move = Mathf.Min(space, leftover);
                    slot.quantity += move;
                    leftover -= move;
                }
            }

            for (int i = 0; i < slots.Count && leftover > 0; i++)
            {
                var slot = slots[i];
                if (!slot.IsEmpty) continue;

                int move = Mathf.Min(stackSize, leftover);
                slot.item = item;
                slot.quantity = move;
                leftover -= move;
            }

            int added = amount - leftover;
            if (added > 0)
            {
                EventBus.Publish(new InventoryItemChangedEvent(this, item, added, true));
                EventBus.Publish(new InventoryChangedEvent(this));
            }

            return leftover == 0;
        }

        public bool TryAdd(ItemData item, int amount = 1) => TryAdd(item, amount, out _);

        /// <summary>Removes up to <paramref name="amount"/>. Returns true if the full amount was removed.</summary>
        public bool TryRemove(ItemData item, int amount = 1)
        {
            if (item == null || amount <= 0) return false;
            if (CountOf(item) < amount) return false;

            int remaining = amount;
            for (int i = slots.Count - 1; i >= 0 && remaining > 0; i--)
            {
                var slot = slots[i];
                if (slot.IsEmpty || !slot.item.Matches(item)) continue;

                int take = Mathf.Min(slot.quantity, remaining);
                slot.quantity -= take;
                remaining -= take;
                if (slot.quantity <= 0) slot.Clear();
            }

            EventBus.Publish(new InventoryItemChangedEvent(this, item, amount, false));
            EventBus.Publish(new InventoryChangedEvent(this));
            return true;
        }

        public int CountOf(ItemData item)
        {
            if (item == null) return 0;
            int total = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (!slot.IsEmpty && slot.item.Matches(item)) total += slot.quantity;
            }
            return total;
        }

        public bool Has(ItemData item, int amount = 1) => CountOf(item) >= amount;

        /// <summary>Would the given amount fit right now? Used to show "Inventory full".</summary>
        public bool HasSpaceFor(ItemData item, int amount = 1)
        {
            if (item == null || amount <= 0) return false;
            int stackSize = item.EffectiveMaxStack;
            int room = 0;

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot.IsEmpty) room += stackSize;
                else if (item.stackable && slot.item.Matches(item)) room += stackSize - slot.quantity;

                if (room >= amount) return true;
            }
            return false;
        }

        public int FreeSlotCount()
        {
            int count = 0;
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].IsEmpty) count++;
            return count;
        }

        public void ClearAll()
        {
            for (int i = 0; i < slots.Count; i++) slots[i].Clear();
            EventBus.Publish(new InventoryChangedEvent(this));
        }

        /// <summary>Expands the case, RE-style. Existing contents are preserved.</summary>
        public void SetCapacity(int newCapacity)
        {
            capacity = Mathf.Max(1, newCapacity);
            ResizeToCapacity();
            EventBus.Publish(new InventoryChangedEvent(this));
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying) ResizeToCapacity();
        }
#endif
    }
}

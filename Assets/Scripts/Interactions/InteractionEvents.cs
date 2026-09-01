namespace SurvivalHorror
{
    public readonly struct InventoryChangedEvent
    {
        public readonly Inventory Inventory;

        public InventoryChangedEvent(Inventory inventory)
        {
            Inventory = inventory;
        }
    }

    public readonly struct InventoryItemChangedEvent
    {
        public readonly Inventory Inventory;
        public readonly ItemData Item;
        public readonly int Amount;
        public readonly bool WasAdded;

        public InventoryItemChangedEvent(Inventory inventory, ItemData item, int amount, bool wasAdded)
        {
            Inventory = inventory;
            Item = item;
            Amount = amount;
            WasAdded = wasAdded;
        }
    }

    public readonly struct ItemExamineRequestedEvent
    {
        public readonly ItemData Item;

        public ItemExamineRequestedEvent(ItemData item)
        {
            Item = item;
        }
    }

    /// <summary>
    /// The player clicked a marked part of the model currently held in the examine
    /// view. Whoever opened the view decides what that means.
    /// </summary>
    public readonly struct ExamineHotspotActivatedEvent
    {
        public readonly ItemData Item;
        public readonly string HotspotId;

        public ExamineHotspotActivatedEvent(ItemData item, string hotspotId)
        {
            Item = item;
            HotspotId = hotspotId;
        }
    }

    /// <summary>
    /// Every item has been taken out of a container held in the examine view. The
    /// world object it came from listens for this to mark itself emptied.
    /// </summary>
    public readonly struct ExamineContainerEmptiedEvent
    {
        public readonly ItemData Item;

        public ExamineContainerEmptiedEvent(ItemData item)
        {
            Item = item;
        }
    }

    public readonly struct ItemExaminationChangedEvent
    {
        public readonly ItemData Item;
        public readonly bool IsExamining;

        public ItemExaminationChangedEvent(ItemData item, bool isExamining)
        {
            Item = item;
            IsExamining = isExamining;
        }
    }
}
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
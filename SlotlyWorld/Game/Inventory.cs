using System;
using System.Collections.Generic;

namespace SlotlyWorld
{
    public class Inventory
    {
        public Dictionary<ItemType, int> Items { get; private set; }

        public Inventory()
        {
            Items = new Dictionary<ItemType, int>();
            foreach (ItemType type in Enum.GetValues(typeof(ItemType)))
            {
                Items[type] = 0;
            }
        }

        public void AddItem(ItemType type, int count = 1)
        {
            Items[type] += count;
        }

        public void RemoveItem(ItemType type, int count = 1)
        {
            if (Items[type] >= count)
            {
                Items[type] -= count;
            }
        }
    }
}

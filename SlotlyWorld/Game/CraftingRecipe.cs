using System.Collections.Generic;

namespace SlotlyWorld
{
    public class CraftingRecipe
    {
        public string Name { get; set; }
        public Dictionary<ItemType, int> RequiredItems { get; set; }
        public ItemType ResultItem { get; set; }
        public int ResultCount { get; set; }
        public bool IsSmelting { get; set; }

        public CraftingRecipe(string name, Dictionary<ItemType, int> requiredItems,
                              ItemType resultItem, int resultCount = 1)
        {
            Name = name;
            RequiredItems = requiredItems;
            ResultItem = resultItem;
            ResultCount = resultCount;
        }
    }
}

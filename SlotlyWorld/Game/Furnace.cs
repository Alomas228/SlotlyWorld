using System.Collections.Generic;

namespace SlotlyWorld
{
    public class Furnace
    {
        public int X { get; set; }
        public int Y { get; set; }
        public bool IsActive { get; set; }
        public Dictionary<ItemType, int> InputItems { get; set; } = new Dictionary<ItemType, int>();
    }
}

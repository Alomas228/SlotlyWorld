using System;
using System.Collections.Generic;
using System.IO;

namespace SlotlyWorld
{
    public static class SaveSystem
    {
        private const int Magic = 0x53544C57; // "SLTW"
        private const int Version = 1;

        public static string SaveDirectory =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SlotlyWorld");

        public static string SavePath => Path.Combine(SaveDirectory, "save.dat");

        public static bool Exists() => File.Exists(SavePath);

        public static void Delete()
        {
            if (Exists()) File.Delete(SavePath);
        }

        public static void Save(Player player, TileType[,] world, List<Furnace> furnaces, float dayNightCycleTimer)
        {
            Directory.CreateDirectory(SaveDirectory);

            using (var stream = File.Create(SavePath))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Magic);
                writer.Write(Version);
                WritePlayer(writer, player);
                WriteWorld(writer, world);
                WriteFurnaces(writer, furnaces);
                writer.Write(dayNightCycleTimer);
            }
        }

        public static SaveData Load()
        {
            using (var stream = File.OpenRead(SavePath))
            using (var reader = new BinaryReader(stream))
            {
                int magic = reader.ReadInt32();
                if (magic != Magic)
                    throw new InvalidDataException("Файл не является сохранением SlotlyWorld.");

                int version = reader.ReadInt32();
                if (version != Version)
                    throw new InvalidDataException("Неподдерживаемая версия сохранения: " + version);

                return new SaveData
                {
                    Player = ReadPlayer(reader),
                    World = ReadWorld(reader),
                    Furnaces = ReadFurnaces(reader),
                    DayNightCycleTimer = reader.ReadSingle(),
                };
            }
        }

        private static void WritePlayer(BinaryWriter w, Player p)
        {
            w.Write(p.X);
            w.Write(p.Y);
            w.Write(p.Health);
            w.Write(p.MaxHealth);
            w.Write(p.Armor);
            w.Write(p.MaxArmor);
            w.Write(p.HasTorch);
            w.Write(p.CurrentTool.HasValue);
            if (p.CurrentTool.HasValue) w.Write((int)p.CurrentTool.Value);

            w.Write(p.Inventory.Items.Count);
            foreach (var kv in p.Inventory.Items)
            {
                w.Write((int)kv.Key);
                w.Write(kv.Value);
            }
        }

        private static Player ReadPlayer(BinaryReader r)
        {
            var p = new Player();
            p.X = r.ReadSingle();
            p.Y = r.ReadSingle();
            p.Health = r.ReadInt32();
            p.MaxHealth = r.ReadInt32();
            p.Armor = r.ReadInt32();
            p.MaxArmor = r.ReadInt32();
            p.HasTorch = r.ReadBoolean();
            bool hasTool = r.ReadBoolean();
            if (hasTool) p.CurrentTool = (ItemType)r.ReadInt32();

            int invCount = r.ReadInt32();
            for (int i = 0; i < invCount; i++)
            {
                var key = (ItemType)r.ReadInt32();
                int value = r.ReadInt32();
                // Если в сохранении встретится незнакомый ItemType
                // (после обновления игры) — просто пропускаем его.
                if (p.Inventory.Items.ContainsKey(key))
                    p.Inventory.Items[key] = value;
            }
            return p;
        }

        private static void WriteWorld(BinaryWriter w, TileType[,] world)
        {
            int width = world.GetLength(0);
            int height = world.GetLength(1);
            w.Write(width);
            w.Write(height);
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    w.Write((byte)world[x, y]);
        }

        private static TileType[,] ReadWorld(BinaryReader r)
        {
            int width = r.ReadInt32();
            int height = r.ReadInt32();
            var world = new TileType[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    world[x, y] = (TileType)r.ReadByte();
            return world;
        }

        private static void WriteFurnaces(BinaryWriter w, List<Furnace> furnaces)
        {
            w.Write(furnaces.Count);
            foreach (var f in furnaces)
            {
                w.Write(f.X);
                w.Write(f.Y);
                w.Write(f.IsActive);
                w.Write(f.InputItems.Count);
                foreach (var kv in f.InputItems)
                {
                    w.Write((int)kv.Key);
                    w.Write(kv.Value);
                }
            }
        }

        private static List<Furnace> ReadFurnaces(BinaryReader r)
        {
            int count = r.ReadInt32();
            var list = new List<Furnace>(count);
            for (int i = 0; i < count; i++)
            {
                var f = new Furnace
                {
                    X = r.ReadInt32(),
                    Y = r.ReadInt32(),
                    IsActive = r.ReadBoolean(),
                };
                int inputCount = r.ReadInt32();
                for (int j = 0; j < inputCount; j++)
                {
                    var key = (ItemType)r.ReadInt32();
                    int value = r.ReadInt32();
                    f.InputItems[key] = value;
                }
                list.Add(f);
            }
            return list;
        }
    }

    public class SaveData
    {
        public Player Player;
        public TileType[,] World;
        public List<Furnace> Furnaces;
        public float DayNightCycleTimer;
    }
}

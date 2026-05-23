using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SlotlyWorld
{
    public class Player
    {
        public float X { get; set; } = 0f;
        public float Y { get; set; } = 0f;
        public int Size { get; set; } = 14;
        public float Speed { get; set; } = 2.5f;
        public Inventory Inventory { get; private set; } = new Inventory();
        private HashSet<Keys> activeDirections = new HashSet<Keys>();

        public int Health { get; set; } = 100;
        public int MaxHealth { get; set; } = 100;
        public int Armor { get; set; } = 0;
        public int MaxArmor { get; set; } = 100;

        public bool HasTorch { get; set; } = false;

        public ItemType? CurrentTool { get; set; } = null;

        public void Draw(Graphics g)
        {
            g.FillEllipse(Brushes.Blue, (int)Math.Round(X), (int)Math.Round(Y), Size, Size);
        }

        public bool CanMove(float dx, float dy, TileType[,] world, int tileSize)
        {
            float newX = X + dx;
            float newY = Y + dy;

            if (newX < 0 || newY < 0 ||
                newX + Size > world.GetLength(0) * tileSize ||
                newY + Size > world.GetLength(1) * tileSize)
            {
                return false;
            }

            int tileX1 = (int)(newX / tileSize);
            int tileY1 = (int)(newY / tileSize);
            int tileX2 = (int)((newX + Size - 1) / tileSize);
            int tileY2 = (int)((newY + Size - 1) / tileSize);

            for (int x = tileX1; x <= tileX2; x++)
            {
                for (int y = tileY1; y <= tileY2; y++)
                {
                    if (x >= 0 && y >= 0 && x < world.GetLength(0) && y < world.GetLength(1))
                    {
                        TileType tile = world[x, y];
                        if (tile == TileType.Water || tile == TileType.Stone ||
                            tile == TileType.Tree || tile == TileType.CoalOre ||
                            tile == TileType.IronOre || tile == TileType.GoldOre ||
                            tile == TileType.Plank)
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        public void SetDirection(Keys key, bool isActive)
        {
            if (isActive)
                activeDirections.Add(key);
            else
                activeDirections.Remove(key);
        }

        public void CalculateMovement(out float dx, out float dy)
        {
            dx = 0;
            dy = 0;
            float speed = Speed;

            foreach (var key in activeDirections)
            {
                switch (key)
                {
                    case Keys.W: dy -= speed; break;
                    case Keys.S: dy += speed; break;
                    case Keys.A: dx -= speed; break;
                    case Keys.D: dx += speed; break;
                }
            }

            if (dx != 0 && dy != 0)
            {
                float factor = 0.7071f;
                dx *= factor;
                dy *= factor;
            }
        }

        public void ApplyDamage(int damage)
        {
            if (this.Armor > 0)
            {
                int armorDamage = Math.Min(damage, this.Armor);
                this.Armor -= armorDamage;
                damage -= armorDamage;
            }

            if (damage > 0)
            {
                this.Health = Math.Max(0, this.Health - damage);
            }
        }
    }
}

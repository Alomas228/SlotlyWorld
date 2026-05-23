using System;
using System.Drawing;

namespace SlotlyWorld
{
    public class Mob
    {
        public float X { get; set; }
        public float Y { get; set; }
        public int Size { get; set; } = 16;
        public float Speed { get; set; }
        public int Health { get; set; }
        public int MaxHealth { get; set; }
        public int Damage { get; set; }
        public bool IsActive { get; set; } = false;
        public float AttackCooldown { get; set; } = 0f;
        public float AttackCooldownMax { get; set; } = 1.0f;
        public bool CanAttack => AttackCooldown <= 0f;

        public Mob(int health, int damage, float speed)
        {
            MaxHealth = health;
            Health = health;
            Damage = damage;
            Speed = speed;
        }

        public void Draw(Graphics g, Color color)
        {
            using (var brush = new SolidBrush(color))
            {
                g.FillEllipse(brush, -Size / 2, -Size / 2, Size, Size);
            }
        }

        public void Update(Player player, TileType[,] world, int tileSize, float deltaTime)
        {
            if (!IsActive) return;

            if (AttackCooldown > 0)
            {
                AttackCooldown -= deltaTime;
            }

            float dx = player.X - X;
            float dy = player.Y - Y;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);

            if (distance <= 150)
            {
                if (distance > 0)
                {
                    dx /= distance;
                    dy /= distance;
                }

                // Speed выражена в пикселях за кадр при 60 FPS — приводим к реальному dt.
                float frameScale = deltaTime * 60f;
                float newX = X + dx * Speed * frameScale;
                float newY = Y + dy * Speed * frameScale;

                if (CanMove(newX - X, newY - Y, world, tileSize))
                {
                    X = newX;
                    Y = newY;
                }
            }
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
    }
}

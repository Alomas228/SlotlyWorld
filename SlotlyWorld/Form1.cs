using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SlotlyWorld
{
    public partial class Form1 : Form
    {
        // Константы и поля
        private int viewWidth = 20;  // Ширина видимой области в тайлах
        private int viewHeight = 12; // Высота видимой области в тайлах
        private int viewOffsetX = 0; // Смещение видимой области
        private int viewOffsetY = 0;
        private int tileSize = 24;  // Размер клетки в пикселях
        private int worldWidth = 1100; // Размер мира (в клетках)
        private int worldHeight = 1100;
        private bool isInventoryVisible = false;
        private bool isDay = true;
        private float dayNightCycleTimer = 0f;
        private const float dayNightCycleDuration = 120f; // секунд на полный цикл день/ночь
        private DayNightCycle currentCycle = DayNightCycle.Day;
        private float currentLightLevel = 1.0f; // 1.0 - полный день, 0.0 - полная ночь
        private int miniMapSize = 150; // Уменьшаем размер для производительности
        private float miniMapScale;
        private Panel miniMapPanel;
        private PictureBox miniMapBox;
        private Bitmap miniMapCache; // Кэш для миникарты
        private bool miniMapNeedsUpdate = true; // Флаг необходимости обновления
        private Rectangle lastPlayerRect; // Последняя позиция игрока
        private Rectangle lastVisibleArea; // Последняя видимая область
        private List<Mob> mobs = new List<Mob>();
        private int maxMobs = 100; // Максимальное количество мобов
        private float mobSpawnTimer = 0f;
        private float mobSpawnInterval = 2f; // Интервал спавна мобов в секундах
        private const float FurnaceInteractionDistance = 2.0f; // Максимальное расстояние для взаимодействия с печью
        private bool isNearFurnace = false;
        private List<CraftingRecipe> furnaceRecipes = new List<CraftingRecipe>(); // Отдельный список рецептов для печи
        private float healthRegenTimer = 0f;
        private const float healthRegenInterval = 1f; // 1 секунда
        private const int healthRegenAmount = 10; // Количество восстанавливаемого здоровья


        private bool[,] exploredTiles;


        private TileType[,] world; // Двумерный массив мира
        private Random random = new Random();
        private Player player = new Player();
        private System.Windows.Forms.Panel inventoryPanel;
        private System.Windows.Forms.Label inventoryLabel;
        private System.Windows.Forms.Timer gameTimer;
        private bool isCraftingVisible = false; // Видимость панели крафта
        private System.Windows.Forms.Panel craftingPanel; // Панель крафта
        private System.Windows.Forms.Label craftingLabel; // Заголовок крафта
        private List<CraftingRecipe> recipes = new List<CraftingRecipe>(); // Список рецептов
        private ItemType? selectedBlockType = null; // Тип выбранного блока
        private bool isBlockSelected = false; // Флаг выбора блока для размещения

        private BlockBreaking currentBlockBreaking = new BlockBreaking();
        private Dictionary<TileType, float> blockBreakTimes = new Dictionary<TileType, float>();
        private bool isMouseDown = false;
        private Point lastMousePos;
        private float fractionalX = 0; // Дробная часть смещения по X
        private float fractionalY = 0; // Дробная часть смещения по Y

        // Реальный deltaTime между кадрами в секундах.
        private Stopwatch frameStopwatch = new Stopwatch();

        // Persistent-буфер для рендера, чтобы не аллоцировать Bitmap каждый кадр.
        private Bitmap worldBitmap;
        private Graphics worldGraphics;

        // Кэшированные ресурсы HUD — иначе создаются каждый кадр.
        private readonly Font hudFont = new Font("Arial", 12, FontStyle.Bold);
        private readonly Brush hudTextBrush = new SolidBrush(Color.White);
        private readonly Brush hudBgBrush = new SolidBrush(Color.FromArgb(128, 0, 0, 0));

        // true — загрузить из сохранения, false — начать новую игру.
        private readonly bool loadExisting;

        private void InitializeMiniMap()
        {
            miniMapScale = Math.Min((float)miniMapSize / worldWidth, (float)miniMapSize / worldHeight);

            miniMapPanel = new Panel();
            miniMapPanel.BackColor = Color.FromArgb(150, 0, 0, 0);
            miniMapPanel.Size = new Size(miniMapSize + 20, miniMapSize + 20);
            miniMapPanel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            miniMapPanel.Location = new Point(
                this.ClientSize.Width - miniMapPanel.Width - 10,
                this.ClientSize.Height - miniMapPanel.Height - 10);

            miniMapBox = new PictureBox();
            miniMapBox.Size = new Size(miniMapSize, miniMapSize);
            miniMapBox.Location = new Point(10, 10);

            miniMapPanel.Controls.Add(miniMapBox);
            this.Controls.Add(miniMapPanel);
            miniMapPanel.BringToFront();

            // Создаем кэш миникарты один раз при инициализации
            GenerateMiniMapCache();
        }

        private void GenerateMiniMapCache()
        {
            if (world == null) return;

            miniMapCache = new Bitmap(miniMapSize, miniMapSize);
            using (Graphics g = Graphics.FromImage(miniMapCache))
            {
                for (int x = 0; x < worldWidth; x++)
                {
                    for (int y = 0; y < worldHeight; y++)
                    {
                        Color tileColor = GetTileColor(world[x, y]);
                        using (Brush brush = new SolidBrush(tileColor))
                        {
                            int mapX = (int)(x * miniMapScale);
                            int mapY = (int)(y * miniMapScale);
                            int size = Math.Max(1, (int)miniMapScale); // Минимум 1 пиксель

                            g.FillRectangle(brush, mapX, mapY, size, size);
                        }
                    }
                }
            }
            miniMapNeedsUpdate = true;
        }

        private void picWorld_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isMouseDown = true;
                lastMousePos = e.Location;
                StartBreakingBlock(e);
            }
        }

        private void picWorld_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isMouseDown = false;
                currentBlockBreaking.IsBreaking = false;
            }
        }

        private void picWorld_MouseMove(object sender, MouseEventArgs e)
        {
            if (isMouseDown && e.Button == MouseButtons.Left)
            {
                // Получаем текущий блок под курсором
                int worldX = viewOffsetX + (int)((e.X + fractionalX * tileSize) / tileSize);
                int worldY = viewOffsetY + (int)((e.Y + fractionalY * tileSize) / tileSize);

                // Если курсор переместился на другой блок, сбрасываем прогресс
                if (currentBlockBreaking.IsBreaking &&
                    (worldX != currentBlockBreaking.X || worldY != currentBlockBreaking.Y))
                {
                    currentBlockBreaking.IsBreaking = false;
                    StartBreakingBlock(e); // Начинаем ломать новый блок
                }
                // Иначе ничего не делаем (прогресс продолжается)
            }
        }

        private void StartBreakingBlock(MouseEventArgs e)
        {
            // Получаем координаты клика в мировых координатах
            int worldX = viewOffsetX + (int)((e.X + fractionalX * tileSize) / tileSize);
            int worldY = viewOffsetY + (int)((e.Y + fractionalY * tileSize) / tileSize);

            // Проверяем расстояние до блока
            float playerTileX = player.X / tileSize;
            float playerTileY = player.Y / tileSize;
            float distanceX = Math.Abs(worldX - playerTileX);
            float distanceY = Math.Abs(worldY - playerTileY);

            if (distanceX > 2.5f || distanceY > 2.5f)
            {
                currentBlockBreaking.IsBreaking = false;
                return;
            }

            // Проверяем, можно ли ломать этот блок с текущим инструментом
            if (worldX >= 0 && worldX < worldWidth &&
                worldY >= 0 && worldY < worldHeight &&
                CanBreakBlock(world[worldX, worldY]))
            {
                currentBlockBreaking.X = worldX;
                currentBlockBreaking.Y = worldY;
                currentBlockBreaking.Progress = 0.0f;
                currentBlockBreaking.TotalTime = blockBreakTimes[world[worldX, worldY]];
                currentBlockBreaking.IsBreaking = true;
            }
            else
            {
                currentBlockBreaking.IsBreaking = false;
            }
        }

        private bool CanBreakBlock(TileType tileType)
        {
            // Без инструмента можно ломать только дерево и пол
            if (player.CurrentTool == null)
            {
                return tileType == TileType.Tree || tileType == TileType.Floor || tileType == TileType.Plank;
            }

            // Проверяем, можно ли ломать блок с текущим инструментом
            switch (player.CurrentTool)
            {
                case ItemType.WoodPickaxe:
                    return tileType == TileType.Tree ||
                           tileType == TileType.Stone ||
                           tileType == TileType.CoalOre ||
                           tileType == TileType.Floor;

                case ItemType.StonePickaxe:
                    return tileType == TileType.Tree ||
                           tileType == TileType.Stone ||
                           tileType == TileType.CoalOre ||
                           tileType == TileType.IronOre ||
                           tileType == TileType.Floor;

                case ItemType.IronPickaxe:
                    return tileType == TileType.Tree ||
                           tileType == TileType.Stone ||
                           tileType == TileType.CoalOre ||
                           tileType == TileType.IronOre ||
                           tileType == TileType.GoldOre ||
                           tileType == TileType.Plank ||
                           tileType == TileType.Floor;

                default:
                    return tileType ==  TileType.Tree || tileType == TileType.Floor || tileType == TileType.Plank;
            }
        }

        private void UpdateBlockBreaking(float deltaTime)
        {
            if (!currentBlockBreaking.IsBreaking) return;

            // Проверяем, что блок еще не разрушен
            if (world[currentBlockBreaking.X, currentBlockBreaking.Y] == TileType.Grass)
            {
                currentBlockBreaking.IsBreaking = false;
                return;
            }

            currentBlockBreaking.Progress += deltaTime / currentBlockBreaking.TotalTime;

            if (currentBlockBreaking.Progress >= 1.0f)
            {
                BreakBlock(currentBlockBreaking.X, currentBlockBreaking.Y);
                currentBlockBreaking.IsBreaking = false;
            }
        }

        private List<Furnace> furnaces = new List<Furnace>();
        private Furnace currentFurnace = null;

        private void BreakBlock(int x, int y)
        {
            switch (world[x, y])
            {
                case TileType.Stone:
                    world[x, y] = TileType.Grass;
                    player.Inventory.AddItem(ItemType.Stone);
                    break;
                case TileType.Tree:
                    world[x, y] = TileType.Grass;
                    player.Inventory.AddItem(ItemType.Wood);
                    break;
                case TileType.CoalOre:
                    world[x, y] = TileType.Grass;
                    player.Inventory.AddItem(ItemType.Coal);
                    break;
                case TileType.Floor:
                    world[x, y] = TileType.Grass;
                    player.Inventory.AddItem(ItemType.Floor);
                    break;
                case TileType.IronOre:
                    world[x, y] = TileType.Grass;
                    player.Inventory.AddItem(ItemType.IronOre); // Изменено с IronIngot на IronOre
                    break;
                case TileType.GoldOre:
                    world[x, y] = TileType.Grass;
                    player.Inventory.AddItem(ItemType.GoldOre); // Изменено с GoldIngot на GoldOre
                    break;
                case TileType.Plank:
                    world[x, y] = TileType.Grass;
                    player.Inventory.AddItem(ItemType.Plank);
                    break;
            }

            DrawWorld();
            UpdateInventoryDisplay();
        }
        private void UpdateMiniMap()
        {
            if (!miniMapNeedsUpdate &&
                lastPlayerRect.Contains((int)(player.X / tileSize * miniMapScale),
                                      (int)(player.Y / tileSize * miniMapScale)) &&
                lastVisibleArea.X == (int)(viewOffsetX * miniMapScale) &&
                lastVisibleArea.Y == (int)(viewOffsetY * miniMapScale))
            {
                return; // Пропускаем обновление, если ничего не изменилось
            }

            if (miniMapCache == null) GenerateMiniMapCache();

            if (miniMapFrame == null)
            {
                miniMapFrame = new Bitmap(miniMapSize, miniMapSize);
                miniMapFrameGraphics = Graphics.FromImage(miniMapFrame);
                miniMapBox.Image = miniMapFrame;
            }

            miniMapFrameGraphics.DrawImageUnscaled(miniMapCache, 0, 0);

            Rectangle visibleArea = new Rectangle(
                (int)(viewOffsetX * miniMapScale),
                (int)(viewOffsetY * miniMapScale),
                (int)(viewWidth * miniMapScale),
                (int)(viewHeight * miniMapScale));

            miniMapFrameGraphics.DrawRectangle(Pens.Yellow, visibleArea);

            int playerX = (int)(player.X / tileSize * miniMapScale);
            int playerY = (int)(player.Y / tileSize * miniMapScale);
            int crossSize = 3;

            miniMapFrameGraphics.DrawLine(Pens.Red, playerX - crossSize, playerY, playerX + crossSize, playerY);
            miniMapFrameGraphics.DrawLine(Pens.Red, playerX, playerY - crossSize, playerX, playerY + crossSize);

            lastPlayerRect = new Rectangle(playerX - crossSize, playerY - crossSize,
                                         crossSize * 2, crossSize * 2);
            lastVisibleArea = visibleArea;

            miniMapBox.Invalidate();
            miniMapNeedsUpdate = false;
        }

        private Bitmap miniMapFrame;
        private Graphics miniMapFrameGraphics;

        // Инициализация формы

        // Конструктор формы
        public Form1() : this(false) { }

        public Form1(bool loadExisting)
        {
            this.loadExisting = loadExisting;
            InitializeComponent();
            this.KeyPreview = true;
            picWorld.MouseDown += picWorld_MouseDown;
            picWorld.MouseUp += picWorld_MouseUp;
            picWorld.MouseMove += picWorld_MouseMove;

            // Расположение инвентаря
            this.inventoryPanel = new System.Windows.Forms.Panel();
            this.inventoryPanel.BackColor = System.Drawing.Color.FromArgb(128, 0, 0, 0); // Полупрозрачный черный
            this.inventoryPanel.Location = new System.Drawing.Point(10, 110); // Под окном информации (которое на 10,10)
            this.inventoryPanel.Name = "inventoryPanel";
            this.inventoryPanel.Size = new System.Drawing.Size(200, 400);
            this.inventoryPanel.TabIndex = 3;

            this.inventoryLabel = new System.Windows.Forms.Label();
            this.inventoryLabel.AutoSize = true;
            this.inventoryLabel.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.inventoryLabel.ForeColor = System.Drawing.Color.White;
            this.inventoryLabel.Location = new System.Drawing.Point(10, 85); // Над инвентарем
            this.inventoryLabel.Name = "inventoryLabel";
            this.inventoryLabel.Size = new System.Drawing.Size(100, 24);
            this.inventoryLabel.TabIndex = 4;
            this.inventoryLabel.Text = "Инвентарь";

            this.Controls.Add(this.inventoryLabel);
            this.Controls.Add(this.inventoryPanel);

            inventoryPanel.Visible = false;
            inventoryLabel.Visible = false;
            isInventoryVisible = false;

            InitializeHealthDisplay();
            this.Resize += Form1_Resize;

            UpdateInventoryDisplay();

            // Инициализация панели крафта
            this.craftingPanel = new System.Windows.Forms.Panel();
            this.craftingPanel.BackColor = Color.FromArgb(128, 0, 0, 0); // Полупрозрачный черный
            this.craftingPanel.Location = new Point(220, 110); // Рядом с инвентарем
            this.craftingPanel.Name = "craftingPanel";
            this.craftingPanel.Size = new Size(200, 450);
            this.craftingPanel.TabIndex = 5;

            this.craftingLabel = new Label();
            this.craftingLabel.Text = "Крафт";
            this.craftingLabel.AutoSize = true;
            this.craftingLabel.Font = new Font("Arial", 12, FontStyle.Bold);
            this.craftingLabel.ForeColor = Color.White;
            this.craftingLabel.Location = new Point(220, 85); // Над крафтовым окном
            this.craftingLabel.Name = "craftingLabel";

            this.Controls.Add(this.craftingLabel);
            this.Controls.Add(this.craftingPanel);

            craftingPanel.Visible = false;
            craftingLabel.Visible = false;
            isCraftingVisible = false;

            // Инициализация рецептов
            InitializeRecipes();
            // Инициализация времени ломания блоков
            blockBreakTimes.Add(TileType.Tree, 1.0f);
            blockBreakTimes.Add(TileType.Stone, 1.0f);
            blockBreakTimes.Add(TileType.CoalOre, 1.0f);
            blockBreakTimes.Add(TileType.IronOre, 1.0f);
            blockBreakTimes.Add(TileType.GoldOre, 15.0f);
            blockBreakTimes.Add(TileType.Plank, 1.0f);
            blockBreakTimes.Add(TileType.Floor, 1.0f);


            viewWidth = picWorld.Width / tileSize;
            viewHeight = picWorld.Height / tileSize;

            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.None;

            picWorld.MouseClick += picWorld_MouseClick;
            this.KeyUp += OnKeyUp;

            gameTimer = new Timer();
            gameTimer.Interval = 16; // ~60 FPS
            gameTimer.Tick += GameUpdate;
            frameStopwatch.Start();
            gameTimer.Start();
            InitializeMiniMap();
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            var healthPanel = this.Controls.Find("healthPanel", true).FirstOrDefault() as Panel;
            if (healthPanel != null)
            {
                healthPanel.Location = new Point(0, this.ClientSize.Height - healthPanel.Height);
            }

            if (miniMapPanel != null)
            {
                miniMapPanel.Location = new Point(
                    this.ClientSize.Width - miniMapPanel.Width - 10,
                    this.ClientSize.Height - miniMapPanel.Height - 10);
            }
        }

        private void UpdateHealthDisplay()
        {
            var healthPanel = this.Controls.Find("healthPanel", true).FirstOrDefault() as Panel;
            if (healthPanel != null)
            {
                healthPanel.Location = new Point(0, this.ClientSize.Height - healthPanel.Height);
            }

            var healthBar = this.Controls.Find("healthBar", true).FirstOrDefault() as ProgressBar;
            var armorBar = this.Controls.Find("armorBar", true).FirstOrDefault() as ProgressBar;

            if (healthBar != null)
            {
                healthBar.Value = player.Health;
                healthBar.Maximum = player.MaxHealth;
            }

            if (armorBar != null)
            {
                armorBar.Value = player.Armor;
                armorBar.Maximum = player.MaxArmor;
            }
        }

        private void InitializeHealthDisplay()
        {
            // Панель для фона
            var healthPanel = new Panel();
            healthPanel.BackColor = Color.FromArgb(100, 0, 0, 0); // Полупрозрачный черный
            healthPanel.Size = new Size(200, 60);
            healthPanel.Anchor = AnchorStyles.Left | AnchorStyles.Bottom; // Закрепляем внизу слева
            healthPanel.Location = new Point(0, this.ClientSize.Height - healthPanel.Height);
            healthPanel.Name = "healthPanel";
            this.Controls.Add(healthPanel);
            healthPanel.BringToFront();

            // Метка для здоровья
            var healthLabel = new Label();
            healthLabel.Text = "Здоровье:";
            healthLabel.ForeColor = Color.White;
            healthLabel.Location = new Point(10, 5);
            healthLabel.AutoSize = true;
            healthPanel.Controls.Add(healthLabel);

            // Прогресс-бар для здоровья
            var healthBar = new ProgressBar();
            healthBar.Name = "healthBar";
            healthBar.Maximum = player.MaxHealth;
            healthBar.Value = player.Health;
            healthBar.ForeColor = Color.Red;
            healthBar.BackColor = Color.DarkRed;
            healthBar.Size = new Size(180, 15);
            healthBar.Location = new Point(10, 25);
            healthPanel.Controls.Add(healthBar);

            // Метка для брони
            var armorLabel = new Label();
            armorLabel.Text = "Броня:";
            armorLabel.ForeColor = Color.White;
            armorLabel.Location = new Point(10, 45);
            armorLabel.AutoSize = true;
            healthPanel.Controls.Add(armorLabel);

            // Прогресс-бар для брони
            var armorBar = new ProgressBar();
            armorBar.Name = "armorBar";
            armorBar.Maximum = player.MaxArmor;
            armorBar.Value = player.Armor;
            armorBar.ForeColor = Color.LightBlue;
            armorBar.BackColor = Color.DarkBlue;
            armorBar.Size = new Size(180, 15);
            armorBar.Location = new Point(10, 65);
            healthPanel.Controls.Add(armorBar);


        }

        private void InitializeRecipes()
        {
            // Примеры рецептов
            recipes.Add(new CraftingRecipe("Доски (4 шт.)",
                new Dictionary<ItemType, int> { { ItemType.Wood, 1 } },
                ItemType.Plank, 4));

            recipes.Add(new CraftingRecipe("Палки (4 шт.)",
                new Dictionary<ItemType, int> { { ItemType.Plank, 2 } },
                ItemType.Stick, 4));

            recipes.Add(new CraftingRecipe("Деревянная кирка",
                new Dictionary<ItemType, int> {
            { ItemType.Stick, 2 },
            { ItemType.Plank, 3 }
                },
                ItemType.WoodPickaxe));
            recipes.Add(new CraftingRecipe("Пол (4 шт.)",
                new Dictionary<ItemType, int> { { ItemType.Plank, 1 } },
                ItemType.Floor, 4));
            // Рецепты, требующие печи (только для интерфейса печи)
            furnaceRecipes.Add(new CraftingRecipe("Железный слиток",
    new Dictionary<ItemType, int> { { ItemType.IronOre, 1 }, { ItemType.Coal, 1 } },
    ItemType.IronIngot)
            { IsSmelting = false }); // Теперь только ручной крафт

            furnaceRecipes.Add(new CraftingRecipe("Золотой слиток",
    new Dictionary<ItemType, int> { { ItemType.GoldOre, 1 }, { ItemType.Coal, 1 } },
    ItemType.GoldIngot)
            { IsSmelting = false }); // Теперь только ручной крафт




            recipes.Add(new CraftingRecipe("Деревянный меч",
                new Dictionary<ItemType, int> {
            { ItemType.Stick, 2 },
            { ItemType.Plank, 2 }
                },
                ItemType.WoodSword));

            recipes.Add(new CraftingRecipe("Каменная кирка",
                new Dictionary<ItemType, int> {
            { ItemType.Stick, 2 },
            { ItemType.Stone, 3 }
                },
                ItemType.StonePickaxe));

            recipes.Add(new CraftingRecipe("Каменный меч",
                new Dictionary<ItemType, int> {
            { ItemType.Stick, 2 },
            { ItemType.Stone, 2 }
                },
                ItemType.StoneSword));

            recipes.Add(new CraftingRecipe("Печь",
                new Dictionary<ItemType, int> { { ItemType.Stone, 8 } },
                ItemType.Furnace));
            // Железная кирка
            recipes.Add(new CraftingRecipe("Железная кирка",
                new Dictionary<ItemType, int> {
            { ItemType.Stick, 2 },
            { ItemType.IronIngot, 3 }
                },
                ItemType.IronPickaxe));

            // Железный меч
            recipes.Add(new CraftingRecipe("Железный меч",
                new Dictionary<ItemType, int> {
            { ItemType.Stick, 2 },
            { ItemType.IronIngot, 2 }
                },
                ItemType.IronSword));

            // Факел
            recipes.Add(new CraftingRecipe("Факел",
        new Dictionary<ItemType, int> {
            { ItemType.Coal, 2 },
            { ItemType.Stick, 2 }

        },
        ItemType.Torch));
        }

        private void UpdateDayNightCycle(float deltaTime)
        {
            dayNightCycleTimer += deltaTime;

            DayNightCycle previousCycle = currentCycle; // Запоминаем предыдущий цикл

            if (dayNightCycleTimer > dayNightCycleDuration)
            {
                dayNightCycleTimer = 0f;
            }

            float dayDuration = dayNightCycleDuration * 0.4f; // 40% времени - день
            float nightDuration = dayNightCycleDuration * 0.4f; // 40% времени - ночь
            float transitionDuration = dayNightCycleDuration * 0.1f; // 10% времени на каждый переход

            if (dayNightCycleTimer < dayDuration)
            {
                // День
                currentCycle = DayNightCycle.Day;
                currentLightLevel = 1.0f;

                // Если только что закончилась ночь - удаляем мобов
                if (previousCycle == DayNightCycle.TransitionToDay)
                {
                    RemoveAllMobs();
                }
            }
            else if (dayNightCycleTimer < dayDuration + transitionDuration)
            {
                // Переход от дня к ночи (закат)
                currentCycle = DayNightCycle.TransitionToNight;
                float transitionProgress = (dayNightCycleTimer - dayDuration) / transitionDuration;
                currentLightLevel = 1.0f - transitionProgress * 0.7f; // Плавное уменьшение света
            }
            else if (dayNightCycleTimer < dayDuration + transitionDuration + nightDuration)
            {
                // Ночь
                currentCycle = DayNightCycle.Night;
                currentLightLevel = 0.3f;
            }
            else
            {
                // Переход от ночи к дню (рассвет)
                currentCycle = DayNightCycle.TransitionToDay;
                float transitionProgress = (dayNightCycleTimer - (dayDuration + transitionDuration + nightDuration)) / transitionDuration;
                currentLightLevel = 0.3f + transitionProgress * 0.7f; // Плавное увеличение света
            }
        }

        private void UpdateCraftingDisplay()
        {
            if (!isCraftingVisible) return;

            craftingPanel.Controls.Clear();

            int yPos = 10;
            foreach (var recipe in recipes)
            {
                Button craftButton = new Button();
                craftButton.Text = recipe.Name;
                craftButton.Tag = recipe;
                craftButton.Location = new Point(10, yPos);
                craftButton.Size = new Size(180, 30);
                craftButton.Font = new Font("Arial", 9);
                craftButton.FlatStyle = FlatStyle.Flat;
                craftButton.ForeColor = Color.White;
                craftButton.Click += CraftButton_Click;

                // Проверяем, можно ли крафтить
                bool canCraft = true;
                foreach (var requirement in recipe.RequiredItems)
                {
                    if (player.Inventory.Items[requirement.Key] < requirement.Value)
                    {
                        canCraft = false;
                        break;
                    }
                }

                craftButton.Enabled = canCraft;
                craftButton.BackColor = canCraft ? Color.FromArgb(100, 0, 100, 0) : Color.FromArgb(100, 100, 0, 0);

                craftingPanel.Controls.Add(craftButton);
                yPos += 35;
            }
        }

        private Color GetDarkerColor(Color color, float factor = 0.7f)
        {
            return Color.FromArgb(
                (int)(color.R * factor),
                (int)(color.G * factor),
                (int)(color.B * factor));
        }
        private void CraftButton_Click(object sender, EventArgs e)
        {
            Button button = sender as Button;
            if (button != null && button.Tag is CraftingRecipe recipe)
            {
                // Проверяем, требует ли рецепт печи
                bool requiresFurnace = furnaceRecipes.Contains(recipe);
                if (requiresFurnace && !isNearFurnace)
                {
                    MessageBox.Show("Для этого рецепта нужна печь! Подойдите к печи и откройте её интерфейс.");
                    return;
                }

                // Двойная проверка предметов
                bool canCraft = true;
                foreach (var requirement in recipe.RequiredItems)
                {
                    if (player.Inventory.Items[requirement.Key] < requirement.Value)
                    {
                        canCraft = false;
                        break;
                    }
                }

                if (canCraft)
                {
                    // Удаляем нужные предметы
                    foreach (var requirement in recipe.RequiredItems)
                    {
                        player.Inventory.RemoveItem(requirement.Key, requirement.Value);
                    }

                    // Добавляем результат
                    player.Inventory.AddItem(recipe.ResultItem, recipe.ResultCount);

                    // Обновляем интерфейс
                    UpdateInventoryDisplay();
                    UpdateCraftingDisplay();
                }
            }
        }



        // Методы инициализации и загрузки
        private void Form1_Load(object sender, EventArgs e)
        {
            picWorld.Dock = DockStyle.Fill;
            viewWidth = picWorld.Width / tileSize;
            viewHeight = picWorld.Height / tileSize;

            if (loadExisting && SaveSystem.Exists())
            {
                try
                {
                    LoadGame();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "Не удалось загрузить сохранение: " + ex.Message + "\nНачинаем новую игру.",
                        "SlotlyWorld",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    StartNewGame();
                }
            }
            else
            {
                StartNewGame();
            }

            DrawWorld();
        }

        private void StartNewGame()
        {
            GenerateWorld();

            int startX = 0, startY = 0;
            bool foundSafeSpot = false;

            for (int i = 0; i < 100; i++)
            {
                startX = random.Next(0, worldWidth - 1) * tileSize;
                startY = random.Next(0, worldHeight - 1) * tileSize;

                int tileX = startX / tileSize;
                int tileY = startY / tileSize;

                if (world[tileX, tileY] == TileType.Grass)
                {
                    foundSafeSpot = true;
                    break;
                }
            }

            dayNightCycleTimer = 0f;

            if (!foundSafeSpot)
            {
                startX = 0;
                startY = 0;
            }

            player.X = startX;
            player.Y = startY;
        }

        private void LoadGame()
        {
            var data = SaveSystem.Load();

            player = data.Player;
            world = data.World;
            worldWidth = world.GetLength(0);
            worldHeight = world.GetLength(1);
            furnaces = data.Furnaces;
            dayNightCycleTimer = data.DayNightCycleTimer;

            exploredTiles = new bool[worldWidth, worldHeight];
            miniMapScale = Math.Min(
                (float)miniMapSize / worldWidth,
                (float)miniMapSize / worldHeight);
            GenerateMiniMapCache();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            gameTimer?.Stop();
            base.OnFormClosing(e);

            try
            {
                if (world != null && player != null)
                    SaveSystem.Save(player, world, furnaces, dayNightCycleTimer);
            }
            catch
            {
                // Не блокируем закрытие, если сохранение упало.
            }
        }

        // Генерация мира
        private void GenerateWorld()
        {
            world = new TileType[worldWidth, worldHeight];
            exploredTiles = new bool[worldWidth, worldHeight]; // Инициализация массива видимых тайлов

            for (int x = 0; x < worldWidth; x++)
                for (int y = 0; y < worldHeight; y++)
                    world[x, y] = TileType.Grass;

            // Генерируем одно огромное озеро
            GenerateHugeLake();

            int maxLakeRadius = Math.Min(15, worldWidth / 4);
            GenerateLakes(3, 5, maxLakeRadius);
            GenerateRivers(Math.Min(2, worldWidth / 50));

            for (int x = 0; x < worldWidth; x++)
            {
                for (int y = 0; y < worldHeight; y++)
                {
                    if (world[x, y] == TileType.Grass)
                    {
                        int noise = random.Next(1000); // Увеличиваем диапазон для более точного контроля
                        if (noise > 987) world[x, y] = TileType.Tree;       // 1% (было 5%)
                        else if (noise > 986) world[x, y] = TileType.Stone; // 0.5% (было 5%)
                        else if (noise >= 985) world[x, y] = TileType.CoalOre; // 0.2% (было 3%)
                        else if (noise >= 984) world[x, y] = TileType.IronOre; // 0.3% (было 2%)
                        else if (noise >= 983) world[x, y] = TileType.GoldOre; // 0.2% (было 1%)


                    }
                }
                GenerateTreeClusters(10, 3, 8); // 10 рощ, размером 3-8 деревьев
            }
            GenerateMiniMapCache();


        }

        private void GenerateHugeLake()
        {
            // Размеры огромного озера (примерно 1/3 от размера мира)
            int lakeWidth = worldWidth / 3;
            int lakeHeight = worldHeight / 3;

            // Центр озера (может быть случайным или фиксированным)
            int centerX = random.Next(lakeWidth, worldWidth - lakeWidth);
            int centerY = random.Next(lakeHeight, worldHeight - lakeHeight);

            // Генерируем озеро с неровными краями
            for (int x = centerX - lakeWidth / 2; x < centerX + lakeWidth / 2; x++)
            {
                for (int y = centerY - lakeHeight / 2; y < centerY + lakeHeight / 2; y++)
                {
                    if (x >= 0 && y >= 0 && x < worldWidth && y < worldHeight)
                    {
                        // Делаем края неровными
                        double distanceX = (double)(x - centerX) / (lakeWidth / 2);
                        double distanceY = (double)(y - centerY) / (lakeHeight / 2);
                        double distance = Math.Sqrt(distanceX * distanceX + distanceY * distanceY);

                        // Добавляем случайные отклонения для более естественного вида
                        double noise = 0.8 + random.NextDouble() * 0.4;

                        if (distance < noise)
                        {
                            world[x, y] = TileType.Water;
                        }
                    }
                }
            }

            // Добавляем несколько островов в озеро
            GenerateIslandsInLake(centerX, centerY, lakeWidth, lakeHeight);
        }

        private void GenerateIslandsInLake(int lakeCenterX, int lakeCenterY, int lakeWidth, int lakeHeight)
        {
            int islandCount = random.Next(3, 8); // От 3 до 7 островов

            for (int i = 0; i < islandCount; i++)
            {
                // Позиция острова (в пределах озера)
                int islandX = lakeCenterX + random.Next(-lakeWidth / 3, lakeWidth / 3);
                int islandY = lakeCenterY + random.Next(-lakeHeight / 3, lakeHeight / 3);
                int islandSize = random.Next(5, 15); // Размер острова

                for (int x = islandX - islandSize; x < islandX + islandSize; x++)
                {
                    for (int y = islandY - islandSize; y < islandY + islandSize; y++)
                    {
                        if (x >= 0 && y >= 0 && x < worldWidth && y < worldHeight &&
                            world[x, y] == TileType.Water)
                        {
                            double distance = Math.Sqrt(Math.Pow(x - islandX, 2) + Math.Pow(y - islandY, 2));
                            if (distance < islandSize * (0.7 + random.NextDouble() * 0.3))
                            {
                                world[x, y] = TileType.Grass;

                                // Иногда добавляем деревья на острова
                                if (random.Next(100) > 70)
                                {
                                    world[x, y] = TileType.Tree;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void GenerateTreeClusters(int clusterCount, int minSize, int maxSize)
        {
            for (int i = 0; i < clusterCount; i++)
            {
                int centerX = random.Next(0, worldWidth);
                int centerY = random.Next(0, worldHeight);
                int size = random.Next(minSize, maxSize + 1);

                for (int j = 0; j < size; j++)
                {
                    int x = centerX + random.Next(-2, 3);
                    int y = centerY + random.Next(-2, 3);
                    if (x >= 0 && x < worldWidth && y >= 0 && y < worldHeight &&
                        world[x, y] == TileType.Grass && random.Next(100) > 50)
                    {
                        world[x, y] = TileType.Tree;
                    }
                }
            }
        }

        private void GenerateLakes(int minLakes, int maxLakes, int maxRadius)
        {
            int lakesCount = random.Next(minLakes, maxLakes + 1);

            for (int i = 0; i < lakesCount; i++)
            {
                int centerX = random.Next(maxRadius, worldWidth - maxRadius);
                int centerY = random.Next(maxRadius, worldHeight - maxRadius);
                int radius = random.Next(5, maxRadius);

                for (int x = centerX - radius; x < centerX + radius; x++)
                {
                    for (int y = centerY - radius; y < centerY + radius; y++)
                    {
                        if (x >= 0 && y >= 0 && x < worldWidth && y < worldHeight)
                        {
                            double distance = Math.Sqrt(Math.Pow(x - centerX, 2) + Math.Pow(y - centerY, 2));
                            if (distance <= radius)
                            {
                                world[x, y] = TileType.Water;
                            }
                        }
                    }
                }
            }
        }

        private void GenerateRivers(int count)
        {
            List<Point> waterSources = new List<Point>();
            for (int x = 0; x < worldWidth; x++)
            {
                for (int y = 0; y < worldHeight; y++)
                {
                    if (world[x, y] == TileType.Water)
                    {
                        if ((x > 0 && world[x - 1, y] != TileType.Water) ||
                            (x < worldWidth - 1 && world[x + 1, y] != TileType.Water) ||
                            (y > 0 && world[x, y - 1] != TileType.Water) ||
                            (y < worldHeight - 1 && world[x, y + 1] != TileType.Water))
                        {
                            waterSources.Add(new Point(x, y));
                        }
                    }
                }
            }

            if (waterSources.Count == 0) return;

            waterSources = waterSources.OrderBy(x => random.Next()).ToList();

            int riversToGenerate = Math.Min(count, waterSources.Count);
            for (int i = 0; i < riversToGenerate; i++)
            {
                Point source = waterSources[i];
                GenerateRiverFromSource(source.X, source.Y);
            }
        }

        private void GenerateRiverFromSource(int startX, int startY)
        {
            int x = startX;
            int y = startY;
            int length = random.Next(400, 600);
            int width = random.Next(2, 3);
            int directionX = 0, directionY = 0;

            if (random.Next(2) == 0)
            {
                directionX = startX < worldWidth / 2 ? 1 : -1;
            }
            else
            {
                directionY = startY < worldHeight / 2 ? 1 : -1;
            }

            for (int j = 0; j < length; j++)
            {
                if (random.Next(100) < 5)
                {
                    if (directionX != 0)
                    {
                        directionX = 0;
                        directionY = random.Next(2) == 0 ? 1 : -1;
                    }
                    else
                    {
                        directionY = 0;
                        directionX = random.Next(2) == 0 ? 1 : -1;
                    }
                }

                int newX = x + directionX;
                int newY = y + directionY;

                if (newX < 0 || newX >= worldWidth || newY < 0 || newY >= worldHeight)
                    break;

                x = newX;
                y = newY;

                for (int w = -width; w <= width; w++)
                {
                    int ry = y + w;
                    if (ry >= 0 && ry < worldHeight)
                    {
                        world[x, ry] = TileType.Water;
                    }
                }

                if (x == 0 || x == worldWidth - 1 || y == 0 || y == worldHeight - 1)
                    break;
            }
        }

        // Обработка ввода
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.W:
                case Keys.S:
                case Keys.A:
                case Keys.D:
                    player.SetDirection(keyData, true);
                    return true;
                case Keys.Tab:
                    ToggleInventory();
                    return true;
                case Keys.E:
                    ToggleCrafting();
                    return true;
                case Keys.F:
                    if (player.HasTorch)
                    {
                        player.HasTorch = false;
                        // Возвращаем факел в инвентарь
                        player.Inventory.AddItem(ItemType.Torch);
                        UpdateInventoryDisplay();
                    }
                    return true;
                case Keys.Escape:
                    // Отмена выбора блока
                    isBlockSelected = false;
                    selectedBlockType = null;
                    UpdateInventoryDisplay();
                    return true;
                case Keys.D1: // Выбор первого инструмента (например, руки - нет инструмента)
                    player.CurrentTool = null;
                    return true;

                case Keys.D2: // Выбор деревянной кирки
                    if (player.Inventory.Items[ItemType.WoodPickaxe] > 0)
                        player.CurrentTool = ItemType.WoodPickaxe;
                    return true;

                case Keys.D3: // Выбор каменной кирки
                    if (player.Inventory.Items[ItemType.StonePickaxe] > 0)
                        player.CurrentTool = ItemType.StonePickaxe;
                    return true;

                case Keys.D4: // Выбор железной кирки
                    if (player.Inventory.Items[ItemType.IronPickaxe] > 0)
                        player.CurrentTool = ItemType.IronPickaxe;
                    return true;
                case Keys.D5: // Выбор деревянного меча
                    if (player.Inventory.Items[ItemType.WoodSword] > 0)
                        player.CurrentTool = ItemType.WoodSword;
                    return true;
                case Keys.D6: // Выбор каменного меча
                    if (player.Inventory.Items[ItemType.StoneSword] > 0)
                        player.CurrentTool = ItemType.StoneSword;
                    return true;
                case Keys.D7: // Выбор железного меча
                    if (player.Inventory.Items[ItemType.IronSword] > 0)
                        player.CurrentTool = ItemType.IronSword;
                    return true;

                default:
                    return base.ProcessCmdKey(ref msg, keyData);
            }
        }
        private void RemoveAllMobs()
        {
            mobs.Clear(); // Просто очищаем список мобов
            Console.WriteLine("Все мобы удалены - наступил день");
        }
        private void ToggleCrafting()
        {
            isCraftingVisible = !isCraftingVisible;
            craftingPanel.Visible = isCraftingVisible;
            craftingLabel.Visible = isCraftingVisible;

            // Показываем инвентарь вместе с крафтом
            if (isCraftingVisible)
            {
                isInventoryVisible = true;
                inventoryPanel.Visible = true;
                inventoryLabel.Visible = true;
            }

            if (isCraftingVisible)
            {
                UpdateCraftingDisplay();
                UpdateInventoryDisplay();
            }
        }

        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.W:
                case Keys.S:
                case Keys.A:
                case Keys.D:
                    player.SetDirection(e.KeyCode, false);
                    break;
            }
        }

        private void picWorld_MouseClick(object sender, MouseEventArgs e)
        {
            if (isInventoryVisible) return;

            // Получаем точные координаты игрока в тайлах (с дробной частью)
            float playerTileX = player.X / tileSize;
            float playerTileY = player.Y / tileSize;

            // Координаты клика в мировых координатах с учетом смещения и дробной части
            int worldX = viewOffsetX + (int)((e.X + fractionalX * tileSize) / tileSize);
            int worldY = viewOffsetY + (int)((e.Y + fractionalY * tileSize) / tileSize);

            // Проверяем расстояние до блока (макс. 3 тайла)
            float distanceX = Math.Abs(worldX - playerTileX);
            float distanceY = Math.Abs(worldY - playerTileY);

            if (distanceX > 3.0f || distanceY > 3.0f)
                return;

            // ПКМ - взаимодействие с печью
            if (e.Button == MouseButtons.Right)
            {
                Furnace clickedFurnace = furnaces.FirstOrDefault(f =>
                    f.X == worldX && f.Y == worldY);

                if (clickedFurnace != null)
                {
                    float distanceToFurnace = (float)Math.Sqrt(
                        Math.Pow(playerTileX - worldX, 2) +
                        Math.Pow(playerTileY - worldY, 2));

                    if (distanceToFurnace <= FurnaceInteractionDistance)
                    {
                        currentFurnace = clickedFurnace;
                        isNearFurnace = true;
                        OpenFurnaceInterface();
                    }
                }
            }

            // ЛКМ - атака мобов или начало разрушения блоков
            if (e.Button == MouseButtons.Left)
            {
                // Сначала проверяем, попал ли клик по мобу
                bool mobHit = false;
                foreach (var mob in mobs.ToList())
                {
                    float mobScreenX = (mob.X / tileSize - viewOffsetX - fractionalX) * tileSize;
                    float mobScreenY = (mob.Y / tileSize - viewOffsetY - fractionalY) * tileSize;

                    Rectangle mobRect = new Rectangle(
                        (int)mobScreenX - mob.Size / 2,
                        (int)mobScreenY - mob.Size / 2,
                        mob.Size,
                        mob.Size);

                    if (mobRect.Contains(e.Location))
                    {
                        // Определяем урон в зависимости от меча
                        int damage = 2; // Базовый урон (без меча)

                        if (player.CurrentTool == ItemType.WoodSword) damage = 5;
                        else if (player.CurrentTool == ItemType.StoneSword) damage = 7;
                        else if (player.CurrentTool == ItemType.IronSword) damage = 10;

                        mob.Health -= damage;
                        if (mob.Health <= 0)
                        {
                            player.Inventory.AddItem(ItemType.Coal);
                            mobs.Remove(mob);
                        }
                        mobHit = true;
                        break;
                    }
                }

                if (!mobHit)
                {
                    StartBreakingBlock(e);
                }
            }
            // ПКМ - размещение блоков
            else if (e.Button == MouseButtons.Right && isBlockSelected && selectedBlockType.HasValue)
            {
                // Проверяем, что координаты в пределах мира
                if (worldX >= 0 && worldX < worldWidth && worldY >= 0 && worldY < worldHeight)
                {
                    // Размещение печи
                    if (selectedBlockType == ItemType.Furnace)
                    {
                        if (world[worldX, worldY] == TileType.Grass && player.Inventory.Items[ItemType.Furnace] > 0)
                        {
                            furnaces.Add(new Furnace { X = worldX, Y = worldY });
                            world[worldX, worldY] = TileType.Stone; // Печь использует текстуру камня
                            player.Inventory.RemoveItem(ItemType.Furnace, 1);
                            DrawWorld();
                            UpdateInventoryDisplay();
                        }
                    }
                    // Особый случай: факел
                    if (selectedBlockType == ItemType.Torch)
                    {
                        player.HasTorch = true;
                        player.Inventory.RemoveItem(ItemType.Torch, 1);
                        UpdateInventoryDisplay();
                        return;
                    }

                    // Проверяем, можно ли разместить блок на этом месте
                    if (world[worldX, worldY] == TileType.Grass)
                    {
                        TileType? tileType = ItemTypeToTileType(selectedBlockType.Value);
                        if (tileType.HasValue && player.Inventory.Items[selectedBlockType.Value] > 0)
                        {
                            world[worldX, worldY] = tileType.Value;
                            player.Inventory.RemoveItem(selectedBlockType.Value, 1);
                            DrawWorld();
                            UpdateInventoryDisplay();
                            UpdateCraftingDisplay();
                        }
                    }
                }
            }
        }

        private void OpenFurnaceInterface()
        {
            if (currentFurnace == null) return;

            // Создаем панель для печи
            Panel furnacePanel = new Panel();
            furnacePanel.BackColor = Color.FromArgb(220, 220, 220);
            furnacePanel.Size = new Size(350, 400);
            furnacePanel.Location = new Point(
                (this.ClientSize.Width - furnacePanel.Width) / 2,
                (this.ClientSize.Height - furnacePanel.Height) / 2);
            furnacePanel.Tag = "FurnaceInterface";
            furnacePanel.BringToFront();

            // Добавляем элементы интерфейса печи
            Label titleLabel = new Label();
            titleLabel.Text = "Печь";
            titleLabel.Font = new Font("Arial", 14, FontStyle.Bold);
            titleLabel.Location = new Point(10, 10);
            titleLabel.Size = new Size(330, 25);
            titleLabel.TextAlign = ContentAlignment.MiddleCenter;
            furnacePanel.Controls.Add(titleLabel);

            // Вкладки (Инвентарь/Плавка/Крафт)
            TabControl tabControl = new TabControl();
            tabControl.Size = new Size(330, 330);
            tabControl.Location = new Point(10, 40);

            // Вкладка инвентаря
            TabPage inventoryTab = new TabPage("Инвентарь");
            tabControl.TabPages.Add(inventoryTab);

            // Вкладка плавки
            TabPage smeltingTab = new TabPage("Плавка");
            tabControl.TabPages.Add(smeltingTab);

            // Вкладка крафта (только печные рецепты)
            TabPage furnaceCraftingTab = new TabPage("Крафт");
            tabControl.TabPages.Add(furnaceCraftingTab);

            furnacePanel.Controls.Add(tabControl);

            // Заполняем вкладки сразу после создания
            UpdateInventoryDisplayForFurnace(inventoryTab);
            UpdateSmeltingDisplay(smeltingTab);
            UpdateFurnaceCraftingDisplay(furnaceCraftingTab);

            // Кнопка закрытия
            Button closeButton = new Button();
            closeButton.Text = "Закрыть";
            closeButton.Size = new Size(100, 30);
            closeButton.Location = new Point(125, 380);
            closeButton.Click += (s, e) => CloseFurnaceInterface();
            furnacePanel.Controls.Add(closeButton);

            this.Controls.Add(furnacePanel);
            furnacePanel.BringToFront();

            // Открываем инвентарь и крафт
            isInventoryVisible = true;
            inventoryPanel.Visible = true;
            inventoryLabel.Visible = true;

            isCraftingVisible = true;
            craftingPanel.Visible = true;
            craftingLabel.Visible = true;

            UpdateInventoryDisplay();
            UpdateCraftingDisplay();
        }

        // Метод для обновления вкладки крафта в печи
        private void UpdateFurnaceCraftingDisplay(TabPage tabPage)
        {
            tabPage.Controls.Clear();

            if (currentFurnace == null) return;

            int yPos = 10;
            foreach (var recipe in furnaceRecipes)
            {
                Button craftButton = new Button();
                craftButton.Text = $"{recipe.Name} (Требуется: {string.Join(", ", recipe.RequiredItems.Select(x => $"{x.Value} {GetItemName(x.Key)}"))})";
                craftButton.Tag = recipe;
                craftButton.Location = new Point(10, yPos);
                craftButton.Size = new Size(300, 40); // Увеличим размер для лучшей читаемости
                craftButton.Font = new Font("Arial", 9);
                craftButton.FlatStyle = FlatStyle.Flat;

                // Проверяем наличие всех необходимых предметов в печи
                bool canCraft = recipe.RequiredItems.All(req =>
                    currentFurnace.InputItems.ContainsKey(req.Key) &&
                    currentFurnace.InputItems[req.Key] >= req.Value);

                craftButton.Enabled = canCraft;
                craftButton.BackColor = canCraft ? Color.LightGreen : Color.LightGray;
                craftButton.Click += FurnaceCraftButton_Click;

                tabPage.Controls.Add(craftButton);
                yPos += 45;
            }
        }

        // Обработчик крафта в печи
        private void FurnaceCraftButton_Click(object sender, EventArgs e)
        {
            if (!(sender is Button button) || !(button.Tag is CraftingRecipe recipe) || currentFurnace == null)
                return;

            // Проверяем наличие всех предметов В ПЕЧИ
            bool canCraft = recipe.RequiredItems.All(req =>
                currentFurnace.InputItems.ContainsKey(req.Key) &&
                currentFurnace.InputItems[req.Key] >= req.Value);

            if (!canCraft)
            {
                MessageBox.Show($"Не хватает материалов! Нужно: {string.Join(", ", recipe.RequiredItems.Select(x => $"{x.Value} {x.Key}"))}");
                return;
            }

            // Удаляем материалы ИЗ ПЕЧИ
            foreach (var requirement in recipe.RequiredItems)
            {
                currentFurnace.InputItems[requirement.Key] -= requirement.Value;
                if (currentFurnace.InputItems[requirement.Key] <= 0)
                    currentFurnace.InputItems.Remove(requirement.Key);
            }

            // Добавляем результат в инвентарь ИГРОКА
            player.Inventory.AddItem(recipe.ResultItem, recipe.ResultCount);

            // Обновляем интерфейс
            UpdateFurnaceInterface();
        }

        private void UpdateFurnaceInterface()
        {
            var furnacePanel = this.Controls.OfType<Panel>()
                .FirstOrDefault(p => p.Tag?.ToString() == "FurnaceInterface");

            if (furnacePanel != null)
            {
                var tabControl = furnacePanel.Controls.OfType<TabControl>().FirstOrDefault();
                if (tabControl != null)
                {
                    UpdateInventoryDisplayForFurnace(tabControl.TabPages[0]);
                    UpdateSmeltingDisplay(tabControl.TabPages[1]);
                    UpdateFurnaceCraftingDisplay(tabControl.TabPages[2]);
                }
            }
            UpdateInventoryDisplay();
        }

        private void UpdateInventoryDisplayForFurnace(TabPage tabPage)
        {
            tabPage.Controls.Clear();

            int yPos = 10;
            foreach (var item in player.Inventory.Items)
            {
                if (item.Value > 0 && IsSmeltableItem(item.Key))
                {
                    Label itemLabel = new Label();
                    string itemName = GetItemName(item.Key);
                    itemLabel.Text = $"{itemName}: {item.Value}";
                    itemLabel.ForeColor = Color.Black;
                    itemLabel.Font = new Font("Arial", 10);
                    itemLabel.Location = new Point(10, yPos);
                    itemLabel.Tag = item.Key;
                    itemLabel.Click += FurnaceItemLabel_Click;
                    yPos += 25;

                    tabPage.Controls.Add(itemLabel);
                }
            }
        }
        // Новый метод для проверки, можно ли переплавить предмет
        private bool IsSmeltableItem(ItemType itemType)
        {
            return itemType == ItemType.IronOre || itemType == ItemType.GoldOre || itemType == ItemType.Coal;
        }

        // Новый обработчик клика по предмету в интерфейсе печи
        private void FurnaceItemLabel_Click(object sender, EventArgs e)
        {
            if (currentFurnace == null) return;

            Label clickedLabel = sender as Label;
            if (clickedLabel != null && clickedLabel.Tag is ItemType itemType)
            {
                // Добавляем предмет в печь для плавки
                if (player.Inventory.Items[itemType] > 0)
                {
                    if (!currentFurnace.InputItems.ContainsKey(itemType))
                    {
                        currentFurnace.InputItems[itemType] = 0;
                    }

                    currentFurnace.InputItems[itemType]++;
                    player.Inventory.RemoveItem(itemType, 1);

                    // Обновляем отображение
                    var furnacePanel = this.Controls.OfType<Panel>()
                        .FirstOrDefault(p => p.Tag?.ToString() == "FurnaceInterface");

                    if (furnacePanel != null)
                    {
                        var tabControl = furnacePanel.Controls.OfType<TabControl>().FirstOrDefault();
                        if (tabControl != null)
                        {
                            UpdateInventoryDisplayForFurnace(tabControl.TabPages[0]);
                            UpdateSmeltingDisplay(tabControl.TabPages[1]);
                        }
                    }
                }
            }
        }

        // Метод извлечения предметов:
        private void TakeAllFromFurnace()
        {
            if (currentFurnace == null) return;

            foreach (var item in currentFurnace.InputItems)
            {
                player.Inventory.AddItem(item.Key, item.Value);
            }
            currentFurnace.InputItems.Clear();

            UpdateFurnaceInterface();
        }

        // Метод для обновления вкладки плавки
        private void UpdateSmeltingDisplay(TabPage tabPage)
        {
            tabPage.Controls.Clear();
            if (currentFurnace == null) return;

            int yPos = 10;
            Label label = new Label { Text = "Предметы в печи:", Location = new Point(10, yPos) };
            tabPage.Controls.Add(label);
            yPos += 25;

            foreach (var item in currentFurnace.InputItems)
            {
                tabPage.Controls.Add(new Label
                {
                    Text = $"{GetItemName(item.Key)}: {item.Value}",
                    Location = new Point(20, yPos)
                });
                yPos += 20;
            }

            // Кнопка извлечения
            Button takeBtn = new Button
            {
                Text = "Извлечь все",
                Location = new Point(10, yPos + 10),
                Size = new Size(100, 30)
            };
            takeBtn.Click += (s, e) => TakeAllFromFurnace();
            tabPage.Controls.Add(takeBtn);
        }
        // Метод для извлечения предметов из печи
        private void TakeItemsFromFurnace()
        {
            if (currentFurnace == null) return;

            foreach (var item in currentFurnace.InputItems)
            {
                player.Inventory.AddItem(item.Key, item.Value);
            }

            currentFurnace.InputItems.Clear();

            // Обновляем отображение
            var furnacePanel = this.Controls.OfType<Panel>()
                .FirstOrDefault(p => p.Tag?.ToString() == "FurnaceInterface");

            if (furnacePanel != null)
            {
                var tabControl = furnacePanel.Controls.OfType<TabControl>().FirstOrDefault();
                if (tabControl != null)
                {
                    UpdateInventoryDisplayForFurnace(tabControl.TabPages[0]);
                    UpdateSmeltingDisplay(tabControl.TabPages[1]);
                    UpdateFurnaceCraftingDisplay(tabControl.TabPages[2]);
                }
            }

            UpdateInventoryDisplay();
        }



        // Игровая логика
        private void GameUpdate(object sender, EventArgs e)
        {
            // Реальный deltaTime в секундах. Ограничиваем сверху, чтобы после фриза
            // не получить гигантский скачок симуляции.
            float deltaTime = (float)frameStopwatch.Elapsed.TotalSeconds;
            frameStopwatch.Restart();
            if (deltaTime > 0.1f) deltaTime = 0.1f;

            // Проверяем расстояние до текущей печи
            if (currentFurnace != null)
            {
                float playerTileX = player.X / tileSize;
                float playerTileY = player.Y / tileSize;
                float distanceToFurnace = (float)Math.Sqrt(
                    Math.Pow(playerTileX - currentFurnace.X, 2) +
                    Math.Pow(playerTileY - currentFurnace.Y, 2));

                if (distanceToFurnace > FurnaceInteractionDistance)
                {
                    CloseFurnaceInterface();
                }
            }

            UpdateDayNightCycle(deltaTime);
            UpdateFurnaces(deltaTime);
            UpdateHealthRegeneration(deltaTime);
            UpdateMobSpawning(deltaTime);
            UpdateBlockBreaking(deltaTime);
            player.CalculateMovement(out float dx, out float dy);

            if (dx != 0 || dy != 0)
            {
                if (player.CanMove(dx, dy, world, tileSize))
                {
                    player.X += dx;
                    player.Y += dy;
                }
                else
                {
                    bool movedX = false;
                    bool movedY = false;

                    if (dx != 0 && player.CanMove(dx, 0, world, tileSize))
                    {
                        player.X += dx;
                        movedX = true;
                        UpdateMiniMap();
                    }

                    if (dy != 0 && player.CanMove(0, dy, world, tileSize))
                    {
                        player.Y += dy;
                        movedY = true;
                    }

                    if (!movedX && !movedY)
                    {
                        float smallStep = 0.5f;
                        if (dx != 0) player.X -= Math.Sign(dx) * smallStep;
                        if (dy != 0) player.Y -= Math.Sign(dy) * smallStep;
                    }
                }
            }

            UpdateMobs(deltaTime);

            DrawWorld();
        }

        private void UpdateHealthRegeneration(float deltaTime)
        {
            // Регенерируем здоровье только если игрок жив и здоровье не полное
            if (player.Health <= 0 || player.Health >= player.MaxHealth)
            {
                healthRegenTimer = 0f;
                return;
            }

            healthRegenTimer += deltaTime;

            if (healthRegenTimer >= healthRegenInterval)
            {
                Heal(healthRegenAmount);
                healthRegenTimer = 0f;
            }
        }

        private void CloseFurnaceInterface()
        {
            if (currentFurnace != null)
            {
                var furnacePanel = this.Controls.OfType<Panel>()
                    .FirstOrDefault(p => p.Tag?.ToString() == "FurnaceInterface");

                if (furnacePanel != null)
                {
                    this.Controls.Remove(furnacePanel);
                    furnacePanel.Dispose();
                }

                currentFurnace = null;
                isNearFurnace = false;

                // Закрываем инвентарь и крафт, если они были открыты через печь
                if (isInventoryVisible && !isCraftingVisible)
                {
                    ToggleInventory();
                }
            }
        }

        private void UpdateFurnaces(float deltaTime)
        {
            
        }

        private void UpdateMobSpawning(float deltaTime)
        {
            // Спавним мобов только ночью или во время заката
            if (currentCycle != DayNightCycle.Night &&
                currentCycle != DayNightCycle.TransitionToNight)
            {
                mobSpawnTimer = 0f;
                return;
            }

            mobSpawnTimer += deltaTime;

            if (mobSpawnTimer >= mobSpawnInterval && mobs.Count < maxMobs)
            {
                SpawnMob();
                mobSpawnTimer = 0f;
            }
        }

        private void SpawnMob()
        {
            for (int i = 0; i < 20; i++)
            {
                float angle = (float)(random.NextDouble() * Math.PI * 2);
                float distance = 100 + (float)(random.NextDouble() * 100);
                int mobX = (int)(player.X + Math.Cos(angle) * distance);
                int mobY = (int)(player.Y + Math.Sin(angle) * distance);

                // Проверяем, не пытаемся ли заспавнить на полу
                int tileX = mobX / tileSize;
                int tileY = mobY / tileSize;

                if (tileX >= 0 && tileX < worldWidth &&
                    tileY >= 0 && tileY < worldHeight &&
                    world[tileX, tileY] == TileType.Floor)
                {
                    continue; // Пропускаем эту позицию
                }

                // Генерируем случайные характеристики
                int health = random.Next(30, 151); // 30-150 HP
                int damage = random.Next(5, 21);   // 5-20 урона
                float speed = (float)(1.0 + random.NextDouble() * 1.5); // 1.0-2.5 скорость

                Mob mob = new Mob(health, damage, speed) { X = mobX, Y = mobY, IsActive = true };

                if (mob.CanMove(0, 0, world, tileSize))
                {
                    mobs.Add(mob);
                    Console.WriteLine($"Моб создан: HP={mob.Health}, DMG={mob.Damage}, SPD={mob.Speed:F1}");
                    return;
                }
            }
            Console.WriteLine("Не удалось создать моба");
        }

        private void UpdateMobs(float deltaTime)
        {
            mobs.RemoveAll(m => m.Health <= 0);

            foreach (var mob in mobs)
            {
                if (mob.IsActive)
                {
                    mob.Update(player, world, tileSize, deltaTime);

                    if (CheckCollision(mob, player))
                    {
                        if (mob.CanAttack)
                        {
                            player.ApplyDamage(mob.Damage);
                            mob.AttackCooldown = mob.AttackCooldownMax;
                        }
                    }
                }
            }
        }

        private bool CheckCollision(Mob mob, Player player)
        {
            Rectangle mobRect = new Rectangle((int)mob.X, (int)mob.Y, mob.Size, mob.Size);
            Rectangle playerRect = new Rectangle((int)player.X, (int)player.Y, player.Size, player.Size);

            // Зона столкновения для более плавного взаимодействия
            mobRect.Inflate(5, 5);
            playerRect.Inflate(5, 5);

            return mobRect.IntersectsWith(playerRect);
        }

        private void ToggleInventory()
        {
            isInventoryVisible = !isInventoryVisible;
            inventoryPanel.Visible = isInventoryVisible;
            inventoryLabel.Visible = isInventoryVisible;

            // Скрываем крафт при скрытии инвентаря
            if (!isInventoryVisible)
            {
                isCraftingVisible = false;
                craftingPanel.Visible = false;
                craftingLabel.Visible = false;
            }

            if (isInventoryVisible)
            {
                UpdateInventoryDisplay();
            }
        }

        private void UpdateInventoryDisplay()
        {
            if (!isInventoryVisible) return;

            inventoryPanel.Controls.Clear();

            int yPos = 10;
            foreach (var item in player.Inventory.Items)
            {
                if (item.Value > 0)
                {
                    Label itemLabel = new Label();
                    string itemName = GetItemName(item.Key);
                    itemLabel.Text = $"{itemName}: {item.Value}";
                    itemLabel.ForeColor = Color.White;
                    itemLabel.Font = new Font("Arial", 10);
                    itemLabel.Location = new Point(10, yPos);
                    itemLabel.Tag = item.Key;
                    itemLabel.Click += ItemLabel_Click;
                    yPos += 25;

                    // Подсвечиваем выбранный предмет
                    if (isBlockSelected && selectedBlockType == item.Key)
                    {
                        itemLabel.BackColor = Color.FromArgb(100, 0, 0, 255); // Полупрозрачный синий
                    }

                    inventoryPanel.Controls.Add(itemLabel);
                }
            }
        }

        private void ItemLabel_Click(object sender, EventArgs e)
        {
            Label clickedLabel = sender as Label;
            if (clickedLabel != null && clickedLabel.Tag is ItemType itemType)
            {
                // Проверяем, можно ли размещать этот предмет как блок
                if (IsPlaceableItem(itemType))
                {
                    selectedBlockType = itemType;
                    isBlockSelected = true;
                    UpdateInventoryDisplay(); // Обновляем отображение для подсветки выбранного предмета
                }
            }
        }

        private bool IsPlaceableItem(ItemType itemType)
        {
            switch (itemType)
            {
                case ItemType.Stone:
                case ItemType.Plank:
                case ItemType.Wood:
                case ItemType.Furnace:
                case ItemType.Floor: 
                    return true;
                case ItemType.Torch:
                    return true;
                default:
                    return false;
            }
        }


        private TileType? ItemTypeToTileType(ItemType? itemType)
        {
            if (itemType == null) return null;

            switch (itemType)
            {
                case ItemType.Stone: return TileType.Stone;
                case ItemType.Plank: return TileType.Plank; // Теперь преобразуется в TileType.Plank
                case ItemType.Wood: return TileType.Tree;
                case ItemType.Furnace: return TileType.Stone; // Или добавьте новый тип для печи
                case ItemType.Floor: return TileType.Floor;

                case ItemType.Torch: return null; // Факел не преобразуется в тайл
                default: return null;
            }
        }

        // Метод для получения понятных названий предметов
        private string GetItemName(ItemType type)
        {
            switch (type)
            {
                case ItemType.Stone: return "Камень";
                case ItemType.Wood: return "Дерево";
                case ItemType.Plank: return "Доски";
                case ItemType.Floor: return "Пол";
                case ItemType.Stick: return "Палки";
                case ItemType.WoodPickaxe: return "Деревянная_кирка";
                case ItemType.WoodSword: return "Деревянный_меч";
                case ItemType.StonePickaxe: return "Каменная_кирка";
                case ItemType.StoneSword: return "Каменный меч";
                case ItemType.Furnace: return "Печь";
                case ItemType.Coal: return "Уголь";
                case ItemType.IronIngot: return "Железный_слиток";
                case ItemType.GoldIngot: return "Золотой_слиток";
                case ItemType.IronPickaxe: return "Железная_кирка";
                case ItemType.IronSword: return "Железный_меч";
                case ItemType.Torch: return "Факел";
                case ItemType.IronOre: return "Железная_руда";
                case ItemType.GoldOre: return "Золотая_руда";
                default: return type.ToString();

            }
        }

        // Метод для восстановления здоровья и брони:
        public void Heal(int amount)
        {
            player.Health = Math.Min(player.MaxHealth, player.Health + amount);
            UpdateHealthDisplay();
        }

        public void AddArmor(int amount)
        {
            player.Armor = Math.Min(player.MaxArmor, player.Armor + amount);
            UpdateHealthDisplay();
        }

        // Метод для нанесения урона
        public void ApplyDamage(int damage)
        {
            if (player.Armor > 0)
            {
                int armorDamage = Math.Min(damage, player.Armor);
                player.Armor -= armorDamage;
                damage -= armorDamage;
            }

            if (damage > 0)
            {
                player.Health = Math.Max(0, player.Health - damage);
            }

            if (player.Health <= 0)
            {
                // Игрок умер - можно добавить обработку смерти
                MessageBox.Show("Игра окончена!");
            }

            UpdateHealthDisplay();
        }

        // Отрисовка
        private void DrawWorld()
        {
            if (isInventoryVisible)
            {
                inventoryPanel.BringToFront();
                inventoryLabel.BringToFront();
            }

            if (isCraftingVisible)
            {
                craftingPanel.BringToFront();
                craftingLabel.BringToFront();
            }

            UpdateHealthDisplay();

            float playerTileX = player.X / tileSize;
            float playerTileY = player.Y / tileSize;

            viewOffsetX = (int)(playerTileX - viewWidth / 2);
            viewOffsetY = (int)(playerTileY - viewHeight / 2);

            viewOffsetX = Math.Max(0, Math.Min(viewOffsetX, worldWidth - viewWidth));
            viewOffsetY = Math.Max(0, Math.Min(viewOffsetY, worldHeight - viewHeight));

            // Сохраняем дробную часть для плавного перемещения
            fractionalX = (playerTileX - viewWidth / 2) - viewOffsetX;
            fractionalY = (playerTileY - viewHeight / 2) - viewOffsetY;


            EnsureWorldBitmap();
            Graphics g = worldGraphics;
            g.Clear(Color.Black);
            {
                // 1. Отрисовываем мир с учетом времени суток
                for (int x = -1; x <= viewWidth; x++)
                {
                    for (int y = -1; y <= viewHeight; y++)
                    {
                        int worldX = viewOffsetX + x;
                        int worldY = viewOffsetY + y;

                        if (worldX >= 0 && worldX < worldWidth &&
                            worldY >= 0 && worldY < worldHeight)
                        {
                            TileType currentTile = world[worldX, worldY];
                            Color tileColor = GetTileColor(currentTile);
                            float tileCenterX = (worldX + 0.5f) * tileSize;
                            float tileCenterY = (worldY + 0.5f) * tileSize;
                            tileColor = ApplyLightLevel(tileColor, currentLightLevel, tileCenterX, tileCenterY);

                            float drawX = x * tileSize - fractionalX * tileSize;
                            float drawY = y * tileSize - fractionalY * tileSize;

                            using (Brush brush = new SolidBrush(tileColor))
                            {
                                g.FillRectangle(brush, drawX, drawY, tileSize, tileSize);
                            }
                        }
                    }
                }

                // 2. Отрисовываем тени и свечения
                for (int x = -1; x <= viewWidth; x++)
                {
                    for (int y = -1; y <= viewHeight; y++)
                    {
                        int worldX = viewOffsetX + x;
                        int worldY = viewOffsetY + y;

                        if (worldX >= 0 && worldX < worldWidth &&
                            worldY >= 0 && worldY < worldHeight)
                        {
                            TileType currentTile = world[worldX, worldY];
                            float drawX = x * tileSize - fractionalX * tileSize;
                            float drawY = y * tileSize - fractionalY * tileSize;

                            // Параметры теней
                            int shadowHeight = Math.Max(1, tileSize / 4);
                            int sideShadowWidth = Math.Max(1, tileSize / 14);
                            int highlightHeight = Math.Max(1, tileSize / 12);

                            // Для травы и пола - особые условия отбрасывания теней на воду
                            if (currentTile == TileType.Grass || currentTile == TileType.Floor)
                            {
                                // Проверяем блок ниже
                                int belowY = worldY + 1;
                                bool hasWaterBelow = belowY < worldHeight && world[worldX, belowY] == TileType.Water;

                                // Проверяем блок справа
                                int rightX = worldX + 1;
                                bool hasWaterRight = rightX < worldWidth && world[rightX, worldY] == TileType.Water;

                                // Цвет тени для травы/пола (темнее основного цвета)
                                Color tileColor = GetTileColor(currentTile);
                                Color shadowColor = GetDarkerColor(tileColor, 0.5f);

                                // Если под травой/полом вода - рисуем тень вниз
                                if (hasWaterBelow)
                                {
                                    float belowDrawY = (y + 1) * tileSize - fractionalY * tileSize;

                                    using (Brush shadowBrush = new SolidBrush(shadowColor))
                                    {
                                        g.FillRectangle(shadowBrush,
                                                      drawX,
                                                      belowDrawY,
                                                      tileSize,
                                                      shadowHeight);
                                    }
                                }

                                // Если справа от травы/пола вода - рисуем тень вправо
                                if (hasWaterRight)
                                {
                                    using (Brush sideShadowBrush = new SolidBrush(shadowColor))
                                    {
                                        g.FillRectangle(sideShadowBrush,
                                                      drawX + tileSize - sideShadowWidth,
                                                      drawY,
                                                      sideShadowWidth,
                                                      tileSize);
                                    }
                                }
                            }
                            // Для остальных блоков - стандартные тени
                            else if (ShouldHaveShadows(currentTile))
                            {
                                // Проверяем блок ниже (игнорируем траву, пол и воду)
                                int belowY = worldY + 1;
                                bool hasBlockBelow = belowY < worldHeight &&
                                                   world[worldX, belowY] != TileType.Grass &&
                                                   world[worldX, belowY] != TileType.Water &&
                                                   world[worldX, belowY] != TileType.Floor;

                                // Проверяем блок справа (игнорируем траву, пол и воду)
                                int rightX = worldX + 1;
                                bool hasBlockRight = rightX < worldWidth &&
                                                  world[rightX, worldY] != TileType.Grass &&
                                                  world[rightX, worldY] != TileType.Water &&
                                                  world[rightX, worldY] != TileType.Floor;

                                // Цвет тени (темнее цвета текущего блока)
                                Color tileColor = GetTileColor(currentTile);
                                Color shadowColor = GetDarkerColor(tileColor, 0.6f);
                                shadowColor = ApplyLightLevel(shadowColor, currentLightLevel, worldX * tileSize + tileSize / 2f, worldY * tileSize + tileSize / 2f);

                                // Если нет блока ниже - рисуем нижнюю тень
                                if (!hasBlockBelow)
                                {
                                    float belowDrawY = (y + 1) * tileSize - fractionalY * tileSize;

                                    using (Brush shadowBrush = new SolidBrush(shadowColor))
                                    {
                                        g.FillRectangle(shadowBrush,
                                                      drawX,
                                                      belowDrawY,
                                                      tileSize,
                                                      shadowHeight);
                                    }
                                }

                                // Боковая тень
                                Color sideShadowColor = GetDarkerColor(tileColor, 0.5f);
                                sideShadowColor = ApplyLightLevel(sideShadowColor, currentLightLevel, worldX * tileSize + tileSize / 2f, worldY * tileSize + tileSize / 2f);

                                // Если нет блока справа - рисуем боковую тень
                                if (!hasBlockRight)
                                {
                                    using (Brush sideShadowBrush = new SolidBrush(sideShadowColor))
                                    {
                                        g.FillRectangle(sideShadowBrush,
                                                      drawX + tileSize - sideShadowWidth,
                                                      drawY,
                                                      sideShadowWidth,
                                                      tileSize);
                                    }
                                }

                                // Проверяем блок сверху (игнорируем траву, пол и воду)
                                int aboveY = worldY - 1;
                                bool hasBlockAbove = aboveY >= 0 &&
                                                   world[worldX, aboveY] != TileType.Grass &&
                                                   world[worldX, aboveY] != TileType.Water &&
                                                   world[worldX, aboveY] != TileType.Floor;

                                // Если нет блока сверху - рисуем верхнее свечение
                                if (!hasBlockAbove)
                                {
                                    using (Brush highlightBrush = new SolidBrush(Color.FromArgb(50, 255, 255, 255)))
                                    {
                                        g.FillRectangle(highlightBrush,
                                                      drawX,
                                                      drawY,
                                                      tileSize - (hasBlockRight ? 0 : sideShadowWidth),
                                                      highlightHeight);
                                    }
                                }
                            }
                        }
                    }
                }

                // Вычисляем позицию игрока на экране
                float playerScreenX = (player.X / tileSize - viewOffsetX - fractionalX) * tileSize;
                float playerScreenY = (player.Y / tileSize - viewOffsetY - fractionalY) * tileSize;

                // Координаты центра игрока в мировых координатах
                float playerCenterX = player.X + player.Size / 2f;
                float playerCenterY = player.Y + player.Size / 2f;

                // Применяем освещение к цвету игрока
                Color playerColor = ApplyLightLevel(Color.Blue, currentLightLevel, playerCenterX, playerCenterY);

                // Отрисовка игрока
                using (var playerBrush = new SolidBrush(playerColor))
                {
                    g.FillEllipse(playerBrush,
                                 (int)Math.Round(playerScreenX),
                                 (int)Math.Round(playerScreenY),
                                 player.Size,
                                 player.Size);
                }

                // Эффект свечения от факела
                if (player.HasTorch && currentCycle != DayNightCycle.Day)
                {
                    float glowSize = player.Size * 2.5f;
                    using (var glowBrush = new SolidBrush(Color.FromArgb(50, 255, 255, 150)))
                    {
                        g.FillEllipse(glowBrush,
                                     (int)Math.Round(playerScreenX - (glowSize - player.Size) / 2),
                                     (int)Math.Round(playerScreenY - (glowSize - player.Size) / 2),
                                     glowSize,
                                     glowSize);
                    }
                }
                // Отрисовка прогресса ломания блока
                if (currentBlockBreaking.IsBreaking)
                {
                    float blockScreenX = (currentBlockBreaking.X - viewOffsetX - fractionalX) * tileSize;
                    float blockScreenY = (currentBlockBreaking.Y - viewOffsetY - fractionalY) * tileSize;

                    // Рисуем фон прогресс-бара
                    g.FillRectangle(Brushes.Black, blockScreenX, blockScreenY - 10, tileSize, 5);

                    // Рисуем сам прогресс
                    g.FillRectangle(Brushes.Green, blockScreenX, blockScreenY - 10,
                                   tileSize * currentBlockBreaking.Progress, 5);
                }

                // Отрисовка мобов с учетом освещения
                foreach (var mob in mobs)
                {
                    if (mob.IsActive)
                    {
                        float mobScreenX = (mob.X / tileSize - viewOffsetX - fractionalX) * tileSize;
                        float mobScreenY = (mob.Y / tileSize - viewOffsetY - fractionalY) * tileSize;

                        float mobCenterX = mob.X + mob.Size / 2f;
                        float mobCenterY = mob.Y + mob.Size / 2f;
                        Color mobColor = ApplyLightLevel(Color.Red, currentLightLevel, mobCenterX, mobCenterY);

                        // Сохраняем текущую трансформацию
                        var oldTransform = g.Transform;

                        // Применяем трансформацию
                        g.TranslateTransform(mobScreenX, mobScreenY);

                        // Рисуем моба с учетом освещения
                        mob.Draw(g, mobColor);

                        // Восстанавливаем трансформацию
                        g.Transform = oldTransform;

                        // Рисуем индикатор здоровья
                        int healthBarWidth = mob.Size;
                        int healthBarHeight = 5;
                        float healthPercentage = (float)mob.Health / mob.MaxHealth;

                        // Фон индикатора здоровья
                        g.FillRectangle(Brushes.Black,
                                       mobScreenX,
                                       mobScreenY - 10,
                                       healthBarWidth,
                                       healthBarHeight);

                        // Сам индикатор здоровья
                        g.FillRectangle(Brushes.Red,
                                       mobScreenX,
                                       mobScreenY - 10,
                                       healthBarWidth * healthPercentage,
                                       healthBarHeight);

                        // Обводка индикатора здоровья
                        g.DrawRectangle(Pens.White,
                                       mobScreenX,
                                       mobScreenY - 10,
                                       healthBarWidth,
                                       healthBarHeight);
                    }
                }

                // Отрисовка информации о времени суток и факеле
                string timeOfDay;
                switch (currentCycle)
                {
                    case DayNightCycle.Day:
                        timeOfDay = "День";
                        break;
                    case DayNightCycle.TransitionToNight:
                        timeOfDay = "Закат";
                        break;
                    case DayNightCycle.Night:
                        timeOfDay = "Ночь";
                        break;
                    case DayNightCycle.TransitionToDay:
                        timeOfDay = "Рассвет";
                        break;
                    default:
                        timeOfDay = "Неизвестно";
                        break;
                }

                string torchStatus = player.HasTorch ? "Факел: активен" : "Факел: неактивен";
                string toolName = player.CurrentTool.HasValue
                    ? GetItemName(player.CurrentTool.Value)
                    : "Руки";

                g.FillRectangle(hudBgBrush, 10, 10, 200, 70);
                g.DrawString($"Время: {timeOfDay}", hudFont, hudTextBrush, 15, 15);
                g.DrawString(torchStatus, hudFont, hudTextBrush, 15, 35);
                g.DrawString($"Инструмент: {toolName}", hudFont, hudTextBrush, 15, 55);
            }

            UpdateMiniMap();
            picWorld.Invalidate();
        }

        private void EnsureWorldBitmap()
        {
            int w = Math.Max(1, picWorld.Width);
            int h = Math.Max(1, picWorld.Height);

            if (worldBitmap == null || worldBitmap.Width != w || worldBitmap.Height != h)
            {
                worldGraphics?.Dispose();
                worldBitmap?.Dispose();
                worldBitmap = new Bitmap(w, h);
                worldGraphics = Graphics.FromImage(worldBitmap);
                picWorld.Image = worldBitmap;
            }
        }


        private bool ShouldHaveShadows(TileType tile)
        {
            // Всегда возвращаем true для блоков, которые могут отбрасывать тень
            // Фактическое решение о тени будет приниматься в DrawWorld
            switch (tile)
            {
                case TileType.Tree:
                case TileType.Stone:
                case TileType.CoalOre:
                case TileType.IronOre:
                case TileType.GoldOre:
                case TileType.Plank:
                    return true;
                default:
                    return false; // Пол и трава не отбрасывают тени
            }
        }

        private Color GetTileColor(TileType tileType)
        {
            switch (tileType)
            {
                case TileType.Water: return Color.Blue;
                case TileType.Grass: return Color.Green;
                case TileType.Tree: return Color.DarkGreen;
                case TileType.Stone: return Color.DarkGray;
                case TileType.CoalOre: return Color.DarkSlateGray;
                case TileType.IronOre: return Color.Gray;
                case TileType.GoldOre: return Color.Gold;
                case TileType.Plank: return Color.SaddleBrown; // Цвет для досок
                case TileType.Furnace: return Color.LightGray; // Цвет печи
                case TileType.Floor: return Color.Sienna; // Цвет для пола
                default: return Color.Black;
            }
        }
        private Color ApplyLightLevel(Color color, float lightLevel, float worldX, float worldY)
        {
            // Если это тень, делаем её ещё темнее ночью
            bool isShadow = color.R < 50 && color.G < 50 && color.B < 50; // Простая проверка на тень
            if (isShadow && currentCycle != DayNightCycle.Day)
            {
                lightLevel *= 0.7f; // Дополнительное затемнение для теней ночью
            }
            // Если у игрока есть факел и ночь, добавляем дополнительное освещение
            if (player.HasTorch && currentCycle != DayNightCycle.Day)
            {
                // Координаты центра игрока
                float playerCenterX = player.X + player.Size / 2f;
                float playerCenterY = player.Y + player.Size / 2f;

                // Расстояние от игрока до текущей точки
                float dx = worldX - playerCenterX;
                float dy = worldY - playerCenterY;
                float distance = (float)Math.Sqrt(dx * dx + dy * dy);

                // Радиус освещения факела (в пикселях)
                float torchRadius = 15f * tileSize; // Уменьшенный радиус для лучшего эффекта

                // Проверяем, нет ли блоков между игроком и текущей точкой
                if (!IsPathBlocked(playerCenterX, playerCenterY, worldX, worldY))
                {
                    // Интенсивность света факела
                    float torchIntensity = Math.Max(0, 1f - distance / torchRadius);
                    torchIntensity = (float)Math.Pow(torchIntensity, 2); // Квадратичное затухание

                    // Комбинируем с общим освещением
                    lightLevel = Math.Max(lightLevel, torchIntensity);
                }
            }

            // Ночной синий оттенок
            float nightBlue = 0.2f * (1 - lightLevel);

            return Color.FromArgb(
                (int)(color.R * lightLevel),
                (int)(color.G * lightLevel),
                (int)(color.B * lightLevel + 255 * nightBlue));
        }

        private bool IsPathBlocked(float startX, float startY, float endX, float endY)
        {
            // Преобразуем координаты в тайлы
            int startTileX = (int)(startX / tileSize);
            int startTileY = (int)(startY / tileSize);
            int endTileX = (int)(endX / tileSize);
            int endTileY = (int)(endY / tileSize);

            // Используем алгоритм Брезенхема для проверки линии
            int dx = Math.Abs(endTileX - startTileX);
            int dy = Math.Abs(endTileY - startTileY);
            int sx = startTileX < endTileX ? 1 : -1;
            int sy = startTileY < endTileY ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                // Пропускаем проверку начальной точки (где стоит игрок)
                if (startTileX != (int)(player.X / tileSize) || startTileY != (int)(player.Y / tileSize))
                {
                    // Проверяем, является ли текущий тайл блоком
                    if (startTileX >= 0 && startTileX < worldWidth &&
                        startTileY >= 0 && startTileY < worldHeight)
                    {
                        TileType tile = world[startTileX, startTileY];
                        if (tile != TileType.Grass && tile != TileType.Water && tile != TileType.Floor) // Добавляем Floor
                        {
                            return true; // Найден блок на пути
                        }
                    }
                }

                if (startTileX == endTileX && startTileY == endTileY)
                    break;

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    startTileX += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    startTileY += sy;
                }
            }

            return false; // Путь свободен
        }

    }
}


using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Diagnostics.Contracts;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Media;
using System.IO;



namespace dimangeon_keeper
{
    public class Game
    {
        private const int TileSize = 32; //это сколько пикселей уйедт на 1 тайл
        private const int MapWidth = 80; //а это сколько тайлов в ширину
        private const int MapHeight = 60; //ну а это в высоту
        private List<DigJob> digJobs = new List<DigJob>(); // zadachi impov 
        private List<Creature> creatures = new List<Creature>(); //moi impiki 

        private const int BaseX = MapWidth/2;
        private const int BaseY = MapHeight/2;
        private const int BaseW = 9;
        private const int BaseH = 9;

        private ToolMode mode = ToolMode.Dig;
        private int heartX;
        private int heartY;

        //zoloto
        private int goldCapacity = 0;
        private const int TreasuryCostPerTile = 300; //стоимость для кладовки 
        private const int GoldCapacityPerTreasuryTile = 1000; //сколько хранит кладовочка

        //lair (logovo) 
        private const int LairCostPerTile = 150;
        private const int BedsPerLairTile = 1;
        private int bedCapacity = 0;
        private int lastBedCapacity = -1;

        //karta
        private Tile[,] map;
        //kartinki
        private Bitmap rockBmp = Properties.Resources.rock;
        private Bitmap dirtBmp = Properties.Resources.dirt;
        private Bitmap goldRockBmp = Properties.Resources.goldrock;

        private Bitmap treasuryFloorBmp = Properties.Resources.treasury_floor;
        private Bitmap treasuryPileBmp = Properties.Resources.treasury_pile;
        private Bitmap treasuryChestBmp = Properties.Resources.treasury_chest;

        private Bitmap heartBmp = Properties.Resources.dungeon_heart;

        private readonly Random rng = new Random(); 
        private Bitmap lair1Bmp = Properties.Resources.lair_1;
        private Bitmap lair2Bmp = Properties.Resources.lair_2;
        private Bitmap lair3Bmp = Properties.Resources.lair_3;
        private Bitmap lair4Bmp = Properties.Resources.lair_4;

        private Bitmap[] lairBmps;

        //tashim rectangle
        private bool isDragBuild;
        private Point dragStartTile;
        private Point dragEndTile;
        private MouseButtons dragButton;
        private ToolMode dragModeAtStart;

        //risovalki
        private readonly Pen gridPen = Pens.Black;
        private readonly Pen digPen = new Pen(Color.Red, 2);
        private readonly Pen assignedPen = new Pen(Color.Yellow, 1);
        private readonly Brush unclaimedShade = new SolidBrush(Color.FromArgb(90, 0, 0, 0));
        private readonly Brush whitebrush = new SolidBrush(Color.White);
        private readonly Pen previewPen = new Pen(Color.FromArgb(180, 255, 255, 255), 2);
        private readonly Brush portik = new SolidBrush(Color.MediumPurple);


        //portal 
        private int portalX;
        private int portalY;
        private bool portalActive;
        private Bitmap portalSprite;



        private float goblinSpawnTimer;
        private const float GoblinSpawnInterval = 6f;
        private const int MaxGoblins = 5;
        private bool portalClosed;
        private int goblinsSpawnedTotal;


        // ===== Enemies сверху базы =====
        private int enemyCampX;
        private int enemyCampY;
        private int enemyTotal;
        private int enemySpawned;
        private bool enemiesActivated;
        private bool enemiesDigging;
        private int enemyDigY;
        private float enemySpawnTimer;
        private float enemyDigTimer;

        private bool enemyWaveCleared; // НОВОЕ: верхняя волна полностью побеждена

        // ===== Antonius (слева от базы) =====
        private int bossCampX;
        private int bossCampY;
        private bool bossSpawned; // теперь это "антониус уже заспавнен"


        private const float EnemySpawnInterval = 1.8f;
        private const float EnemyDigInterval = 0.55f;


        private int heartHp = 300;
        private int heartMaxHp = 300;
        private bool gameOver;
        private bool gameWon;
        private bool endShown;
        private string endMessage;


        //zvuki
        private SoundPlayer sndDig;
        private float digSfxCooldown;

        public event Action HitSfxRequested;
        public event Action TeleportSfxRequested;
        public event Action<bool> EndMusicRequested; // true=win, false=lose


        private void InitDigSfxOnce()
        {
            if (sndDig != null) return;

            // имя ресурса = то, что видно в Properties -> Resources (обычно без расширения)
            sndDig = new SoundPlayer(Properties.Resources.dig);
            sndDig.LoadAsync();
        }

        private void PlayDigSfx()
        {
            if (sndDig == null) return;
            if (digSfxCooldown > 0f) return;

            digSfxCooldown = 0.12f;
            sndDig.Play();
        }


        private void PrepareArtForTileSize()
        {
            // Масштабирование ресурсов один раз (если вдруг картинки не 32x32).
            // Это убирает постоянное масштабирование в DrawImage и заметно ускоряет.
            rockBmp = ScaleToTile(rockBmp);
            dirtBmp = ScaleToTile(dirtBmp);
            goldRockBmp = ScaleToTile(goldRockBmp);

            treasuryFloorBmp = ScaleToTile(treasuryFloorBmp);
            treasuryPileBmp = ScaleToTile(treasuryPileBmp);
            treasuryChestBmp = ScaleToTile(treasuryChestBmp);

            heartBmp = ScaleToTile(heartBmp);

            lair1Bmp = ScaleToTile(lair1Bmp);
            lair2Bmp = ScaleToTile(lair2Bmp);
            lair3Bmp = ScaleToTile(lair3Bmp);
            lair4Bmp = ScaleToTile(lair4Bmp);

            lairBmps = new[] { lair1Bmp, lair2Bmp, lair3Bmp, lair4Bmp };

        }

        private Bitmap ScaleToTile(Bitmap srcBmp)
        {
            if (srcBmp == null) return null;
            if (srcBmp.Width == TileSize && srcBmp.Height == TileSize) return srcBmp;

            Bitmap scaled = new Bitmap(TileSize, TileSize, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(scaled))
            {
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;

                g.Clear(Color.Transparent);
                g.DrawImage(srcBmp, new Rectangle(0, 0, TileSize, TileSize));
            }
            return scaled;
        }

        private void InitPortalSpriteOnce()
        {
            if (portalSprite != null) return;
            portalSprite = ScaleToTile(Properties.Resources.sprite_portal);
        }


        // ===== Render cache (чанки карты) =====
        // Идея: карта (тайлы) меняется редко, но рисуем её 60 FPS.
        // Поэтому мы заранее "собираем" тайлы в битмапы-чанки и каждый кадр рисуем
        // не тысячи DrawImage, а десятки.

        private const int ChunkTiles = 16; // 16x16 тайлов в чанке (512x512 px при TileSize=32)
        private Bitmap[,] mapChunkBmp;
        private bool[,] mapChunkDirty;
        private int chunkCountX;
        private int chunkCountY;

        private void InitChunkCache()
        {
            chunkCountX = (MapWidth + ChunkTiles - 1) / ChunkTiles;
            chunkCountY = (MapHeight + ChunkTiles - 1) / ChunkTiles;

            mapChunkBmp = new Bitmap[chunkCountX, chunkCountY];
            mapChunkDirty = new bool[chunkCountX, chunkCountY];

            MarkAllChunksDirty();
        }

        private void MarkAllChunksDirty()
        {
            if (mapChunkDirty == null) return;

            for (int cx = 0; cx < chunkCountX; cx++)
            {
                for (int cy = 0; cy < chunkCountY; cy++)
                {
                    mapChunkDirty[cx, cy] = true;
                }
            }
        }

        private void MarkChunkDirtyByTile(int x, int y)
        {
            if (mapChunkDirty == null) return;

            int cx = x / ChunkTiles;
            int cy = y / ChunkTiles;

            if (cx < 0 || cy < 0 || cx >= chunkCountX || cy >= chunkCountY) return;

            mapChunkDirty[cx, cy] = true;
        }

        private void EnsureChunkUpToDate(int cx, int cy)
        {
            if (mapChunkBmp == null || mapChunkDirty == null) return;
            if (cx < 0 || cy < 0 || cx >= chunkCountX || cy >= chunkCountY) return;

            if (!mapChunkDirty[cx, cy] && mapChunkBmp[cx, cy] != null)
            {
                return;
            }

            RebuildChunk(cx, cy);
            mapChunkDirty[cx, cy] = false;
        }

        private void RebuildChunk(int cx, int cy)
        {
            int x0 = cx * ChunkTiles;
            int y0 = cy * ChunkTiles;

            int x1 = Math.Min(MapWidth - 1, x0 + ChunkTiles - 1);
            int y1 = Math.Min(MapHeight - 1, y0 + ChunkTiles - 1);

            int tilesW = (x1 - x0 + 1);
            int tilesH = (y1 - y0 + 1);

            int pxW = tilesW * TileSize;
            int pxH = tilesH * TileSize;

            Bitmap chunk = mapChunkBmp[cx, cy];
            if (chunk == null || chunk.Width != pxW || chunk.Height != pxH)
            {
                mapChunkBmp[cx, cy]?.Dispose();
                chunk = new Bitmap(pxW, pxH, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                mapChunkBmp[cx, cy] = chunk;
            }

            using (Graphics cg = Graphics.FromImage(chunk))
            {
                cg.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
                cg.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
                cg.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                cg.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;

                // Можно сделать прозрачный фон, но чёрный для подземелья обычно ок
                cg.Clear(Color.Black);

                for (int x = x0; x <= x1; x++)
                {
                    for (int y = y0; y <= y1; y++)
                    {
                        int localX = (x - x0) * TileSize;
                        int localY = (y - y0) * TileSize;

                        DrawTileStatic(cg, localX, localY, map[x, y]);

                        // Сетка как раньше, но теперь рисуется только при перестройке чанка
                        cg.DrawRectangle(gridPen, new Rectangle(localX, localY, TileSize, TileSize));
                    }
                }
            }
        }

        private void DrawTileStatic(Graphics g, int screenX, int screenY, Tile tile)
        {
            Rectangle rect = new Rectangle(screenX, screenY, TileSize, TileSize);

            Bitmap baseBmp = null;
            Color fallback = Color.Magenta;

            if (tile.Type == TileType.Rock)
            {
                baseBmp = rockBmp;
                fallback = Color.DarkSlateGray;
            }
            else if (tile.Type == TileType.GoldRock)
            {
                baseBmp = goldRockBmp;
                fallback = Color.Goldenrod;
            }
            else
            {
                baseBmp = dirtBmp;
                fallback = Color.SaddleBrown;
            }

            if (baseBmp != null)
            {
                g.DrawImage(baseBmp, rect);
            }
            else
            {
                using (Brush b = new SolidBrush(fallback))
                {
                    g.FillRectangle(b, rect);
                }
            }

            // Не-claimed грязь затемняем (визуально как "чужая" / не ваша)
            if (tile.Type == TileType.Dirt && !tile.IsClaimed)
            {
                g.FillRectangle(unclaimedShade, rect);
            }

            if (tile.Room == RoomType.Treasury)
            {
                if (treasuryFloorBmp != null)
                {
                    g.DrawImage(treasuryFloorBmp, rect);
                }

                if (tile.TreasuryGold > 0)
                {
                    Bitmap decor = tile.TreasuryGold >= 500 ? treasuryChestBmp : treasuryPileBmp;
                    if (decor != null)
                    {
                        g.DrawImage(decor, rect);
                    }
                }
            }
            if (tile.Room == RoomType.Lair)
            {
                // Берём один из 4 спрайтов по RoomVariant
                if (lairBmps != null && lairBmps.Length > 0)
                {
                    Bitmap b = lairBmps[tile.RoomVariant % lairBmps.Length];
                    if (b != null)
                        g.DrawImage(b, rect);
                }
            }

        }

        private void DrawDigJobOverlays(Graphics g, int firstTileX, int firstTileY, int lastTileX, int lastTileY)
        {
            // DigJobs рисуем поверх карты (не в чанке), чтобы не перестраивать чанки при каждом клике
            for (int i = 0; i < digJobs.Count; i++)
            {
                DigJob job = digJobs[i];

                if (job.X < firstTileX || job.X > lastTileX || job.Y < firstTileY || job.Y > lastTileY)
                {
                    continue;
                }

                int sx = job.X * TileSize - camX;
                int sy = job.Y * TileSize - camY;

                Rectangle rect = new Rectangle(sx, sy, TileSize, TileSize);
                g.DrawRectangle(digPen, rect);

                if (map[job.X, job.Y].DigJobAssigned)
                {
                    Rectangle rect1 = new Rectangle(sx + 2, sy + 2, TileSize - 5, TileSize - 5);
                    g.DrawRectangle(assignedPen, rect1);
                }
            }
        }

        //vse dlya kameri 
        private int camX;
        private int camY;
        private int viewWidth;
        private int viewHeight;

        private int mouseX;
        private int mouseY;

        private const int EdgeScrollMargin = 24;
        private const int CameraSpeed = 700;
        private bool cameraInitialized;

        public void SetViewportSize(int width, int height)
        {
            viewWidth = Math.Max(1, width);
            viewHeight = Math.Max(1, height);
            if (!cameraInitialized)
            {
                CenterCameraOnHeart();
                cameraInitialized = true;
            }
            else
            {
                ClampCamera();
            }
               

        }

        public void SetMousePosition(int x,int y)
        {
            mouseX = x;
            mouseY = y;
        }

        public void PanCameraByTiles(int dxTiles, int dyTiles)
        {
            camX += dxTiles * TileSize;
            camY += dyTiles * TileSize;
            ClampCamera();
        }

        private void ClampCamera()
        {
            int worldW = MapWidth * TileSize;
            int worldH = MapHeight * TileSize;

            int maxX = Math.Max(0, worldW - viewWidth);
            int maxY = Math.Max(0, worldH - viewHeight);

            if (camX < 0)
            {
                camX = 0;

            }
            if (camY < 0)
            {
                camY = 0;
            }
            if (camX > maxX)
            {
                camX = maxX;
            }
            if (camY> maxY)
            {
                camY = maxY;
            }
        }

        private void UpdateCamera(float dt)
        {
            if (viewWidth <=0 || viewHeight <= 0)
            {
                return;
            }
            int step = (int)(CameraSpeed * dt);
            if (step<1)
            {
                step = 1;
            }
            bool moved = false;

            if (mouseX <= EdgeScrollMargin)
            {
                camX -= step;
                moved = true;

            }
            else if (mouseX >= viewWidth - EdgeScrollMargin)
            {
                camX += step;
                moved = true;
            }
            if (mouseY <= EdgeScrollMargin)
            {
                camY -= step;
                moved = true;
            }
            else if (mouseY >= viewHeight - EdgeScrollMargin)
            {
                camY += step;
                moved = true;   
            }
            if (moved)
            {
                ClampCamera();
            }
        }

        public void CenterCameraOnTile(int tileX, int tileY)
        {
            int targetX = tileX * TileSize + TileSize / 2;
            int targetY = tileY * TileSize + TileSize / 2;

            camX = targetX - viewWidth / 2;
            camY = targetY - viewHeight / 2;

            ClampCamera();
        }

        public void CenterCameraOnHeart()
        {
            CenterCameraOnTile(heartX, heartY);
        }


        private bool IsWalkable(int x, int y)
        {
            if (x < 0 || x >= MapWidth || y < 0 || y >= MapHeight)
            {
                return false;
            }
            return map[x, y].Type == TileType.Dirt;
        }
        private void DirtBase(int x0, int y0, int w, int h)
        {
            for (int i = x0; i < x0 + w; i++)
            {
                for (int j = y0; j < y0 + h; j++)
                {
                    if (i < 0 || i >= MapWidth || j < 0 || j >= MapHeight)
                    {
                        continue;
                    }
                    map[i, j].Type = TileType.Dirt;
                    map[i, j].HasDigJob = false;

                }
            }
        }

        private void RecomputeBedCapacity()
        {
            int lairTiles = 0;
            for (int x = 0; x < MapWidth; x++)
            {
                for (int y = 0; y < MapHeight; y++)
                {
                    if (map[x, y].Room == RoomType.Lair)
                    {
                        lairTiles++;
                    }
                }
            }
            bedCapacity = lairTiles * BedsPerLairTile;
        }
        private void EndGame(bool won, string msg)
        {

            if (gameOver) return;
            EndMusicRequested?.Invoke(won);
            gameOver = true;
            gameWon = won;
            endMessage = msg;
        }

        private void ShowEndDialogIfNeeded()
        {
            if (endShown) return;
            endShown = true;
            MessageBox.Show(endMessage, gameWon ? "Победааа!!!" : "Поражение((");
        }

        public Game()
        {
            CreateMap();
            PrepareArtForTileSize();
            InitDigSfxOnce();
            DirtBase(BaseX, BaseY, BaseW, BaseH);

            heartX = BaseX + BaseW / 2;
            heartY = BaseY + BaseH / 2;

            map[heartX, heartY].Type = TileType.Dirt;

            RecomputeClaimedFromHeart();
            CreateInitialTreasury();
            RecomputeGoldCapacity();

            PlaceGoldVeins();
            PlaceBossCamp();
            PlacePortal();
            PlaceEnemyCamp();
            InitPortalSpriteOnce();
            InitChunkCache();




            //создаем 4 импа
            for (int i = 0; i < 4; i++)
            {
                Creature c = new Creature(BaseX + i + 2, BaseY + 5);
                c.MaxHp = 20;
                c.Hp = 20;
                c.Damage = 0;
                c.WalkFrames = new Bitmap[]
                {
                    Properties.Resources.sprite_imp0,
                    Properties.Resources.sprite_imp1,
                    Properties.Resources.sprite_imp2
                };
                creatures.Add(c);
            }
        }

        private void CreateMap()
        {
            map = new Tile[MapWidth, MapHeight];
            for (int x = 0; x < MapWidth; x++)
            {
                for (int y = 0; y < MapHeight; y++)
                {
                    map[x, y] = new Tile(TileType.Rock);
                }
            }
        }
        private void UpdateEnemyWave(float dt)
        {
            // 1) активация если игрок докопался до лагеря
            if (!enemiesActivated)
            {
                if (IsPlayerReachedEnemyCamp())
                    ActivateEnemyWave();
            }

            // 2) враги сами копают вниз, если у игрока есть 5 гоблинов и >4 lairs
            if (!enemiesActivated)
            {
                if (!enemiesDigging && CountAlive(CreatureKind.Goblin) >= 5 && bedCapacity > 4)
                {
                    enemiesDigging = true;
                    enemyDigY = enemyCampY + 1;
                    enemyDigTimer = 0f;
                }

                if (enemiesDigging)
                {
                    enemyDigTimer += dt;
                    if (enemyDigTimer >= EnemyDigInterval)
                    {
                        enemyDigTimer = 0f;

                        if (enemyDigY < BaseY)
                        {
                            DigTileForEnemies(enemyCampX, enemyDigY);
                            enemyDigY++;
                        }
                        else
                        {
                            ActivateEnemyWave();
                        }
                    }
                }
            }

            // 3) спавн 5 воинов сверху (без босса)
            if (enemiesActivated)
            {
                enemySpawnTimer += dt;
                if (enemySpawned < enemyTotal && enemySpawnTimer >= EnemySpawnInterval)
                {
                    enemySpawnTimer = 0f;
                    SpawnEnemyWarrior();
                    enemySpawned++;
                }

                // 4) волна считается побежденной, когда все 5 появились и все мертвы
                if (!enemyWaveCleared && enemySpawned >= enemyTotal && CountAlive(CreatureKind.EnemyWarrior) == 0)
                {
                    enemyWaveCleared = true;
                }
            }
        }


        private bool IsPlayerReachedEnemyCamp()
        {
            // если возле лагеря есть “claimed dirt”, значит игрок докопался
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    int x = enemyCampX + dx;
                    int y = enemyCampY + dy;
                    if (x < 0 || x >= MapWidth || y < 0 || y >= MapHeight) continue;

                    if (map[x, y].IsClaimed && IsWalkable(x, y))
                        return true;
                }
            }
            return false;
        }

        private void ActivateEnemyWave()
        {
            enemiesActivated = true;
            enemiesDigging = false;

            // делаем клетку лагеря проходимой для спавна
            map[enemyCampX, enemyCampY].Type = TileType.Dirt;
            map[enemyCampX, enemyCampY].HasDigJob = false;

            MarkChunkDirtyByTile(enemyCampX, enemyCampY);
            RecomputeClaimedFromHeart();
        }

        private void DigTileForEnemies(int x, int y)
        {
            if (x < 0 || x >= MapWidth || y < 0 || y >= MapHeight) return;

            var t = map[x, y];

            // копаем только камень/золото-камень, базу/комнаты не трогаем
            if (t.Type == TileType.Rock || t.Type == TileType.GoldRock)
            {
                t.Type = TileType.Dirt;
                PlayDigSfx();
                t.HasDigJob = false;
                MarkChunkDirtyByTile(x, y);
                RecomputeClaimedFromHeart();
            }
        }

        private int CountAlive(CreatureKind kind)
        {
            int n = 0;
            foreach (var c in creatures)
                if (c.Kind == kind && c.State != CreatureState.Dead && c.Hp > 0)
                    n++;
            return n;
        }

        private void SpawnEnemyWarrior()
        {
            InitKnightFramesOnce();

            var e = new Creature(enemyCampX + 0.5f, enemyCampY + 0.5f);
            e.Kind = CreatureKind.EnemyWarrior;
            e.Speed = 3.1f;

            e.MaxHp = 50;
            e.Hp = e.MaxHp;
            e.Damage = 5;

            // ставим спрайты вместо кружка
            e.WalkFrames = knightFrames;

            e.State = CreatureState.GoingToAttack;
            creatures.Add(e);
        }


        private void SpawnBossAntonius(bool tooEarly)
        {
            InitKingFramesOnce();

            var b = new Creature(bossCampX + 0.5f, bossCampY + 0.5f);
            b.Kind = CreatureKind.BossAntonius;

            b.WalkFrames = kingFrames;
            b.FrameIndex = 0;

            if (tooEarly)
            {
                // игрок пришёл слишком рано -> Антониус гарантированно выносит
                b.MaxHp = 9999;
                b.Hp = 9999;
                b.Damage = 60;
                b.Speed = 2.8f;
            }
            else
            {
                // нормальный босс после победы над верхней волной
                b.MaxHp = 800;
                b.Hp = 800;
                b.Damage = 26;
                b.Speed = 2.2f;
            }

            b.State = CreatureState.GoingToAttack;
            creatures.Add(b);

            bossSpawned = true;
        }




        private const float AttackInterval = 0.55f;
        private const float AttackRange = 0.85f;

        private void UpdateEnemy(float dt, Creature e)
        {
            if (e.Hp <= 0)
            {
                e.State = CreatureState.Dead;
                return;
            }

            // цель: гоблины -> импы -> сердце
            Creature target = FindNearestTarget(e);

            if (target == null)
            {
                // бьём сердце
                MoveAndAttackHeart(dt, e);
                return;
            }

            MoveAndAttackCreature(dt, e, target);
        }

        private Creature FindNearestTarget(Creature from)
        {
            Creature best = null;
            float bestD2 = float.MaxValue;

            // 1) goblins
            foreach (var c in creatures)
            {
                if (c.State == CreatureState.Dead || c.Hp <= 0) continue;
                if (c.Kind != CreatureKind.Goblin) continue;

                float dx = c.X - from.X;
                float dy = c.Y - from.Y;
                float d2 = dx * dx + dy * dy;
                if (d2 < bestD2) { bestD2 = d2; best = c; }
            }
            if (best != null) return best;

            // 2) imps
            foreach (var c in creatures)
            {
                if (c.State == CreatureState.Dead || c.Hp <= 0) continue;
                if (c.Kind != CreatureKind.Imp) continue;

                float dx = c.X - from.X;
                float dy = c.Y - from.Y;
                float d2 = dx * dx + dy * dy;
                if (d2 < bestD2) { bestD2 = d2; best = c; }
            }
            return best;
        }

        private void MoveAndAttackCreature(float dt, Creature attacker, Creature target)
        {
            float dx = target.X - attacker.X;
            float dy = target.Y - attacker.Y;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);

            if (dist <= AttackRange)
            {
                attacker.UpdateAnimation(dt, false);

                attacker.State = CreatureState.Attacking;
                attacker.AttackTimer += dt;

                if (attacker.AttackTimer >= AttackInterval)
                {
                    attacker.AttackTimer = 0f;
                    target.Hp -= attacker.Damage;
                    HitSfxRequested?.Invoke();

                    if (target.Hp <= 0)
                    {
                        target.Hp = 0;
                        target.State = CreatureState.Dead;
                    }
                }
                return;
            }

            attacker.State = CreatureState.GoingToAttack;

            Point start = new Point((int)attacker.X, (int)attacker.Y);
            Point goal = new Point((int)target.X, (int)target.Y);

            // пересчитываем путь, если цель поменялась или пути нет
            if (attacker.Path == null || attacker.PathIndex >= attacker.Path.Count || attacker.ApproachCell != goal)
            {
                attacker.ClearPath();
                if (TryFindPath(start, goal, out List<Point> p))
                {
                    attacker.Path = p;
                    attacker.PathIndex = 0;
                    attacker.ApproachCell = goal;
                }
            }
            attacker.UpdateAnimation(dt, true);

            MoveAlongPath(dt, attacker);
        }

        private void MoveAndAttackHeart(float dt, Creature attacker)
        {
            float tx = heartX + 0.5f;
            float ty = heartY + 0.5f;

            float dx = tx - attacker.X;
            float dy = ty - attacker.Y;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);

            if (dist <= AttackRange)
            {
                attacker.State = CreatureState.Attacking;
                attacker.AttackTimer += dt;

                if (attacker.AttackTimer >= AttackInterval)
                {
                    attacker.AttackTimer = 0f;
                    heartHp -= attacker.Damage;
                    HitSfxRequested?.Invoke();
                    

                    if (heartHp <= 0)
                    {
                        heartHp = 0;
                        EndGame(false, "Поражение: Dungeon Heart уничтожено.");

                    }
                }
                return;
            }

            attacker.State = CreatureState.GoingToAttack;

            Point start = new Point((int)attacker.X, (int)attacker.Y);
            Point goal = new Point(heartX, heartY);

            if (attacker.Path == null || attacker.PathIndex >= attacker.Path.Count || attacker.ApproachCell != goal)
            {
                attacker.ClearPath();
                if (TryFindPath(start, goal, out List<Point> p))
                {
                    attacker.Path = p;
                    attacker.PathIndex = 0;
                    attacker.ApproachCell = goal;
                }
            }

            MoveAlongPath(dt, attacker);
        }

        private void MoveAlongPath(float dt, Creature c)
        {
            if (c.Path == null || c.PathIndex >= c.Path.Count)
                return;

            Point nextCell = c.Path[c.PathIndex];
            float tx = nextCell.X + 0.5f;
            float ty = nextCell.Y + 0.5f;

            float dx = tx - c.X;
            float dy = ty - c.Y;
            float dist = (float)Math.Sqrt(dx * dx + dy * dy);

            float step = c.Speed * dt;

            if (dist < 0.05f)
            {
                c.X = tx;
                c.Y = ty;
                c.PathIndex++;
                if (c.PathIndex >= c.Path.Count)
                    c.ClearPath();
                return;
            }

            if (step > dist) step = dist;
            c.X += dx / dist * step;
            c.Y += dy / dist * step;
        }


        public void Update(float dt)
        {
            if (gameOver)
            {
                ShowEndDialogIfNeeded();
                return;
            }
            if (digSfxCooldown > 0f) digSfxCooldown -= dt;
            UpdateCamera(dt);

            UpdateGoblinSpawner(dt);
            UpdateEnemyWave(dt);
            UpdateBossAntoniusCamp(dt);

            // если lair'ы изменились — подтянуть статы гоблинов (чтобы бонусы были “живые”)
            RefreshGoblinStats();

            foreach (Creature c in creatures)
            {
                if (c.State == CreatureState.Dead)
                    continue;

                switch (c.Kind)
                {
                    case CreatureKind.Imp:
                        UpdateCreature(dt, c);
                        break;

                    case CreatureKind.Goblin:
                        UpdateGoblin(dt, c);
                        break;

                    case CreatureKind.EnemyWarrior:
                    case CreatureKind.BossAntonius:
                        UpdateEnemy(dt, c);
                        break;
                }
            }
            CleanupDeadCreatures();
        }
        private void CleanupDeadCreatures()
        {
            for (int i = creatures.Count - 1; i >= 0; i--)
            {
                var c = creatures[i];
                if (c.State == CreatureState.Dead || c.Hp <= 0)
                {
                    // если имп умер с активной задачей — освободим
                    if (c.Kind == CreatureKind.Imp && c.CurrentJob != null)
                    {
                        c.CurrentJob.Unassign();
                        map[c.CurrentJob.X, c.CurrentJob.Y].DigJobAssigned = false;
                        c.CurrentJob = null;
                    }
                    creatures.RemoveAt(i);
                    if (c.Kind == CreatureKind.BossAntonius)
                    {
                        EndGame(true, "Победа: Антониус повержен. Уровень пройден.");
                    }

                }
            }
        }

        public void Draw(Graphics g)
        {
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.None;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;

            g.Clear(Color.Black);

            DrawMap(g);
            DrawDungeonHeart(g);
            DrawPortal(g);
            DrawCreatures(g);
            DrawHub(g);
            DrawBuildPreview(g);

        }
        private int CountGoblins()
        {
            int count = 0;
            foreach (var c in creatures)
                if (c.Kind == CreatureKind.Goblin) count++;
            return count;
        }
        private void SpawnGoblin()
        {
            InitGoblinFramesOnce();
            TeleportSfxRequested?.Invoke();

            Creature g = new Creature(portalX + 0.5f, portalY + 0.5f);

            // ВАЖНО: чтобы CountGoblins() работал
            g.Kind = CreatureKind.Goblin;

            // Если у тебя это поле/свойство реально есть — оставь.
            // Если компилятор ругнётся — удали строку.
            g.IsGoblin = true;

            g.Speed = 3.5f;
            g.WalkFrames = goblinFrames;

            ApplyGoblinStats(g);

            g.RallyCell = PickRandomBaseCell();
            TryStartGoblinMoveToRally(g);

            // ВАЖНО: иначе гоблин не появится вообще
            creatures.Add(g);
        }

        private void PlaceEnemyCamp()
        {
            int gap = rng.Next(5, 8); // 5-7 камня
            enemyCampX = heartX;
            enemyCampY = BaseY - gap - 1;
            if (enemyCampY < 1) enemyCampY = 1;

            enemyTotal = 5;               // фиксированно 5
            enemySpawned = 0;
            enemiesActivated = false;
            enemiesDigging = false;
            enemyDigY = enemyCampY + 1;
            enemySpawnTimer = 0f;
            enemyDigTimer = 0f;

            enemyWaveCleared = false;     // НОВОЕ
        }

        private void PlaceBossCamp()
        {
            int gap = rng.Next(5, 8); // 5-7 камня слева
            bossCampX = BaseX - gap;
            bossCampY = heartY;

            if (bossCampX < 1) bossCampX = 1;
            if (bossCampY < 1) bossCampY = 1;
            if (bossCampX >= MapWidth) bossCampX = MapWidth - 1;
            if (bossCampY >= MapHeight) bossCampY = MapHeight - 1;

            // фиксируем что это точно ROCK, чтобы генерация золота не перезаписала
            map[bossCampX, bossCampY].Type = TileType.Rock;
            map[bossCampX, bossCampY].HasDigJob = false;

            bossSpawned = false;
        }

        private void UpdateBossAntoniusCamp(float dt)
        {
            if (bossSpawned || gameOver)
                return;

            Tile t = map[bossCampX, bossCampY];

            // Антониус появляется только если игрок реально раскопал его клетку и она connected (claimed)
            if (t.Type != TileType.Dirt || !t.IsClaimed)
                return;

            bool tooEarly = !enemyWaveCleared;  // если верхние 5 не побеждены — босс непобедим

            SpawnBossAntonius(tooEarly);
        }

        private void ApplyGoblinStats(Creature g)
        {
            int lairTiles = bedCapacity; // BedsPerLairTile = 1

            // важно: если lair = 0, гоблины должны проигрывать
            int baseHp = 30;
            int baseDmg = 3;

            int newMax = baseHp + lairTiles * 15;  // 2 lair -> 60, 4 lair -> 90
            int newDmg = baseDmg + lairTiles * 4;  // 2 lair -> 11, 4 lair -> 19

            if (g.MaxHp <= 0)
            {
                // первичная инициализация (на спавне)
                g.MaxHp = newMax;
                g.Hp = newMax;
            }
            else
            {
                // если lair построили позже — подтянуть максимум без полного хила
                float pct = (g.MaxHp > 0) ? (float)g.Hp / g.MaxHp : 1f;
                g.MaxHp = newMax;
                g.Hp = Math.Max(1, (int)Math.Round(pct * g.MaxHp));
            }

            g.Damage = newDmg;
        }


        

        private void RefreshGoblinStats()
        {
            if (bedCapacity == lastBedCapacity) return;
            lastBedCapacity = bedCapacity;

            foreach (var c in creatures)
                if (c.Kind == CreatureKind.Goblin && c.State != CreatureState.Dead)
                    ApplyGoblinStats(c);
        }


        private Bitmap[] goblinFrames;
        private Bitmap[] knightFrames;
        private Bitmap[] kingFrames; // sprite_king (idle) + sprite_king1 (walk)
        private const int BossTiles = 2; // 2x2 тайла = 4 тайла площадью


        private void InitGoblinFramesOnce()
        {
            if (goblinFrames != null) return;
            goblinFrames = new Bitmap[]
            {
                Properties.Resources.sprite_0,
                Properties.Resources.sprite_1,
                Properties.Resources.sprite_2
            };
        }
        private void InitKnightFramesOnce()
        {
            if (knightFrames != null) return;

            Bitmap k0 = Properties.Resources.sprite_knight0;
            Bitmap k1 = Properties.Resources.sprite_knight1;

            if (k0 == null)
            {
                knightFrames = null;
                return;
            }
            if (k1 == null) k1 = k0;

            Bitmap k0Tight = ScaleToTileTight(k0);
            Bitmap k1Tight = ScaleToTileTight(k1);

            // кадр 0 = idle, 1.. = walk
            knightFrames = new Bitmap[]
            {
                k0Tight,
                k1Tight,
                k0Tight
            };
        }

        //dlya bossa
        private Bitmap ScaleToSize(Bitmap srcBmp, int w, int h)
        {
            if (srcBmp == null) return null;
            if (srcBmp.Width == w && srcBmp.Height == h) return srcBmp;

            Bitmap scaled = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(scaled))
            {
                g.CompositingQuality = CompositingQuality.HighSpeed;
                g.SmoothingMode = SmoothingMode.None;
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.PixelOffsetMode = PixelOffsetMode.Half;

                g.Clear(Color.Transparent);
                g.DrawImage(srcBmp, new Rectangle(0, 0, w, h));
            }
            return scaled;
        }
        private Bitmap TrimTransparent(Bitmap src)
        {
            if (src == null) return null;

            int minX = src.Width, minY = src.Height, maxX = -1, maxY = -1;

            for (int y = 0; y < src.Height; y++)
                for (int x = 0; x < src.Width; x++)
                {
                    var c = src.GetPixel(x, y);
                    if (c.A == 0) continue;

                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }

            if (maxX < 0) return src; // полностью прозрачный

            int w = maxX - minX + 1;
            int h = maxY - minY + 1;

            Bitmap cropped = new Bitmap(w, h);
            using (Graphics g = Graphics.FromImage(cropped))
            {
                g.DrawImage(src, new Rectangle(0, 0, w, h),
                    new Rectangle(minX, minY, w, h), GraphicsUnit.Pixel);
            }
            return cropped;
        }

        private Bitmap ScaleToTileTight(Bitmap src)
        {
            Bitmap trimmed = TrimTransparent(src);
            return ScaleToTile(trimmed); // твоя функция 32x32 NearestNeighbor
        }

        private void InitKingFramesOnce()
        {
            if (kingFrames != null) return;

            int w = TileSize * BossTiles; // 64
            int h = TileSize * BossTiles; // 64

            Bitmap idle = Properties.Resources.sprite_king;
            Bitmap walk = Properties.Resources.sprite_king1;

            // если walk вдруг не добавился в ресурсы — используем idle
            if (walk == null) walk = idle;

            kingFrames = new Bitmap[]
            {
                ScaleToSize(idle, w, h),  // 0 = стоит
                ScaleToSize(walk, w, h)   // 1 = движение
            };
        }

        private void UpdateGoblinSpawner(float dt)
        {
            // 1) Портал "обнаружен" только когда его клетка раскопана и стала claimed
            if (!portalActive)
            {
                Tile t = map[portalX, portalY];
                if (t.Type == TileType.Dirt && t.IsClaimed)
                {
                    portalActive = true;
                    portalClosed = false;
                    goblinSpawnTimer = 0f;
                }
                return;
            }

            // 2) Если портал уже исчерпан (5 гоблинов уже вышли) — больше никогда не спавним
            if (portalClosed || goblinsSpawnedTotal >= MaxGoblins)
            {
                portalClosed = true;
                return;
            }

            goblinSpawnTimer += dt;
            if (goblinSpawnTimer < GoblinSpawnInterval)
                return;

            goblinSpawnTimer = 0f;

            SpawnGoblin();
            goblinsSpawnedTotal++;

            if (goblinsSpawnedTotal >= MaxGoblins)
                portalClosed = true;
        }


        private void DrawBuildPreview(Graphics g)
        {
            if (!isDragBuild)
                return;

            var r = MakeTileRect(dragStartTile, dragEndTile);

            int x1 = r.X * TileSize - camX;
            int y1 = r.Y * TileSize - camY;
            int w = r.Width * TileSize;
            int h = r.Height * TileSize;

            g.DrawRectangle(previewPen, x1, y1, w, h);
        }

        private void DrawMap(Graphics g)
        {
            // Какие тайлы видим
            int firstTileX = camX / TileSize;
            int firstTileY = camY / TileSize;

            int tilesX = (viewWidth / TileSize) + 2;
            int tilesY = (viewHeight / TileSize) + 2;

            int lastTileX = Math.Min(MapWidth - 1, firstTileX + tilesX);
            int lastTileY = Math.Min(MapHeight - 1, firstTileY + tilesY);

            // Диапазон чанков, которые нужно нарисовать
            int firstChunkX = firstTileX / ChunkTiles;
            int firstChunkY = firstTileY / ChunkTiles;
            int lastChunkX = lastTileX / ChunkTiles;
            int lastChunkY = lastTileY / ChunkTiles;

            for (int cx = firstChunkX; cx <= lastChunkX; cx++)
            {
                for (int cy = firstChunkY; cy <= lastChunkY; cy++)
                {
                    EnsureChunkUpToDate(cx, cy);

                    Bitmap chunk = mapChunkBmp[cx, cy];
                    if (chunk == null) continue;

                    int worldX = cx * ChunkTiles * TileSize;
                    int worldY = cy * ChunkTiles * TileSize;

                    int screenX = worldX - camX;
                    int screenY = worldY - camY;

                    // Рисуем чанк 1:1 (без масштабирования)
                    g.DrawImageUnscaled(chunk, screenX, screenY);
                }
            }

            // Оверлеи задач копки (их мало — можно рисовать поверх)
            DrawDigJobOverlays(g, firstTileX, firstTileY, lastTileX, lastTileY);
        }
        private void DrawDungeonHeart(Graphics g)
        {
            Rectangle r = new Rectangle(heartX * TileSize - camX, heartY * TileSize - camY, TileSize, TileSize);
            if (heartBmp!= null)
            {
                g.DrawImage(heartBmp, r);
            }  
        }

        private void DrawCreatures(Graphics g)
        {
            //эээ, ну корочеее.. чтобы пискель арт не размывался :)
            //тупо надо!
            //g.InterpolationMode = InterpolationMode.NearestNeighbor;
            //g.SmoothingMode = SmoothingMode.None;
            //g.PixelOffsetMode = PixelOffsetMode.Half;

            foreach (Creature c in creatures)
            {
                Bitmap frame = c.GetCurrentFrame();
                float px = c.X * TileSize - camX;
                float py = c.Y * TileSize - camY;

                int drawW = TileSize;
                int drawH = TileSize;

                if (c.Kind == CreatureKind.BossAntonius)
                {
                    drawW = TileSize * BossTiles; // 64
                    drawH = TileSize * BossTiles; // 64
                }

                int drawX = (int)(px - drawW / 2f);
                int drawY = (int)(py - drawH / 2f);

                if (frame == null)
                {
                    Brush b = (c.Kind == CreatureKind.BossAntonius) ? Brushes.OrangeRed : Brushes.Red;
                    g.FillEllipse(b, new Rectangle(drawX, drawY, drawW, drawH));
                    g.DrawEllipse(Pens.Black, new Rectangle(drawX, drawY, drawW, drawH));
                }
                else
                {
                    g.DrawImage(frame, new Rectangle(drawX, drawY, drawW, drawH));
                }

            }
        }
        private readonly Font hubFont = new Font("Consolas", 14, FontStyle.Bold);
        private void DrawHub(Graphics g)
        {
            //tut popravil razmer vne kommita
            Rectangle r = new Rectangle(0, 0, 950, 40);
            Brush b = new SolidBrush(Color.Black);
            g.FillRectangle(b, r);
            g.DrawRectangle(Pens.Black, r);
            int totalGold = GetTotalGold();
            string text = $"Mode: {mode} | Gold: {totalGold}/{goldCapacity} | Beds: {bedCapacity} | Goblins {CountGoblins()} | (D=Dig, T=Treasury, L=Lair)";
            g.DrawString(text, hubFont, whitebrush, 8, 8);

            
        }

        private void PlacePortal()
        {
            int gap = rng.Next(5, 8);
            portalX = heartX;
            portalY = BaseY + BaseH + gap;

            portalY = Math.Min(MapHeight - 2, portalY); //zashita ot vixoda za grani

            portalActive = false;
            map[portalX, portalY].Type = TileType.Rock;
        }


        private void DrawPortal(Graphics g)
        {
            if (!portalActive)
                return;

            InitPortalSpriteOnce();

            int sx = portalX * TileSize - camX;
            int sy = portalY * TileSize - camY;

            Rectangle r = new Rectangle(sx, sy, TileSize, TileSize);

            if (portalSprite != null)
            {
                g.DrawImage(portalSprite, r);
            }
            else
            {
                // fallback если спрайт не загрузился
                g.FillRectangle(portik, r);
                g.DrawRectangle(Pens.Black, r);
            }
        }


        private void SetTreasury(int x, int y)
        {
            if (x < 0 || y < 0 || x >= MapWidth || y >= MapHeight)
            {
                return;
            }
            if (map[x, y].Type != TileType.Dirt)
            {
                return;
            }
            map[x, y].Room = RoomType.Treasury;
            MarkChunkDirtyByTile(x, y);
        }
        private void CreateInitialTreasury()
        {
            SetTreasury(heartX + 1, heartY);
            SetTreasury(heartX + 1, heartY + 1);
            SetTreasury(heartX + 2, heartY);
            SetTreasury(heartX + 2, heartY + 1);

        }

        public void HandleMouseMove(int mouseX, int mouseY)
        {
            if (!isDragBuild)
                return;

            int tileX = (mouseX + camX) / TileSize;
            int tileY = (mouseY + camY) / TileSize;

            if (tileX < 0 || tileY < 0 || tileX >= MapWidth || tileY >= MapHeight)
                return;

            dragEndTile = new Point(tileX, tileY);
        }

        private Rectangle MakeTileRect(Point a, Point b)
        {
            int x1 = Math.Min(a.X, b.X);
            int y1 = Math.Min(a.Y, b.Y);
            int x2 = Math.Max(a.X, b.X);
            int y2 = Math.Max(a.Y, b.Y);
            return new Rectangle(x1, y1, x2 - x1 + 1, y2 - y1 + 1);
        }
        public void HandleMouseUp(int mouseX, int mouseY, MouseButtons button)
        {
            if (!isDragBuild)
                return;

            // отпустили не ту кнопку — игнор
            if (button != dragButton)
                return;

            isDragBuild = false;

            // применяем прямоугольник
            var rect = MakeTileRect(dragStartTile, dragEndTile);

            if (dragModeAtStart == ToolMode.BuildTreasury)
                ApplyTreasuryRect(rect, dragButton);

            if (dragModeAtStart == ToolMode.BuildLair)      // если у тебя уже есть Lair tool
                ApplyLairRect(rect, dragButton);
        }


        public void HandleMouseDown(int mouseX, int mouseY, MouseButtons button)
        {
            int tileX = (mouseX+camX) / TileSize;
            int tileY = (mouseY +camY) / TileSize;

            if (tileX < 0 || tileY < 0 || tileX >= MapWidth || tileY >= MapHeight)
            {
                return;
            }

            if (mode == ToolMode.Dig)
            {
                HandleDigMouse(tileX, tileY, button);
                return;
            }
            if (mode == ToolMode.BuildTreasury || mode == ToolMode.BuildLair)
            {
                isDragBuild = true;
                dragButton = button;
                dragModeAtStart = mode;
                dragStartTile = new Point(tileX, tileY);
                dragEndTile = dragStartTile;
                return;
            }
            

        }

        private void ApplyTreasuryRect(Rectangle r, MouseButtons button)
        {
            if (button == MouseButtons.Left)
            {
                // 1) собираем подходящие клетки
                List<Point> toBuild = new List<Point>();

                for (int x = r.X; x < r.X + r.Width; x++)
                    for (int y = r.Y; y < r.Y + r.Height; y++)
                    {
                        Tile t = map[x, y];

                        if (t.Type != TileType.Dirt) continue;
                        if (!t.IsClaimed) continue;
                        if (t.Room != RoomType.None) continue;

                        toBuild.Add(new Point(x, y));
                    }

                if (toBuild.Count == 0) return;

                int totalCost = toBuild.Count * TreasuryCostPerTile;
                if (!TrySpendGold(totalCost))
                    return;

                // 2) строим
                foreach (var p in toBuild)
                {
                    map[p.X, p.Y].Room = RoomType.Treasury;
                    MarkChunkDirtyByTile(p.X, p.Y);
                }

                RecomputeGoldCapacity(); //после пачки, а не на каждую клетку
                return;
            }

            if (button == MouseButtons.Right)
            {
                bool changed = false;

                for (int x = r.X; x < r.X + r.Width; x++)
                    for (int y = r.Y; y < r.Y + r.Height; y++)
                    {
                        Tile t = map[x, y];
                        if (t.Room != RoomType.Treasury) continue;

                        // запретить сносить, если в этой клетке реально лежит золото (иначе “пропадёт”)
                        if (t.TreasuryGold > 0) continue;

                        t.Room = RoomType.None;
                        MarkChunkDirtyByTile(x, y);
                        changed = true;
                    }

                if (changed)
                    RecomputeGoldCapacity();

                return;
            }
        }
        private void ApplyLairRect(Rectangle r, MouseButtons button)
        {
            if (lairBmps == null || lairBmps.Length == 0)
            {
                // На всякий случай, чтобы не падало.
                lairBmps = new[] { lair1Bmp, lair2Bmp, lair3Bmp, lair4Bmp };
            }

            if (button == MouseButtons.Left)
            {
                // 1) собираем клетки, которые МОЖНО построить
                List<Point> toBuild = new List<Point>();

                for (int x = r.X; x < r.X + r.Width; x++)
                    for (int y = r.Y; y < r.Y + r.Height; y++)
                    {
                        if (x < 0 || y < 0 || x >= MapWidth || y >= MapHeight)
                            continue;

                        Tile t = map[x, y];

                        if (t.Type != TileType.Dirt) continue;
                        if (!t.IsClaimed) continue;
                        if (t.Room != RoomType.None) continue;

                        toBuild.Add(new Point(x, y));
                    }

                if (toBuild.Count == 0)
                    return;

                // 2) платим ОДИН РАЗ за всю пачку (чтобы не было "частично построилось")
                int totalCost = toBuild.Count * LairCostPerTile;
                if (!TrySpendGold(totalCost))
                    return;

                // 3) строим
                foreach (var p in toBuild)
                {
                    Tile t = map[p.X, p.Y];
                    t.Room = RoomType.Lair;

                    // Используем ЕДИНЫЙ RoomVariant (как у тебя в Tile)
                    t.RoomVariant = (byte)rng.Next(0, lairBmps.Length);

                    MarkChunkDirtyByTile(p.X, p.Y);
                }
                RecomputeBedCapacity();

                var refresh = r;
                refresh.Inflate(1, 1);
                RecomputeLairVisuals(refresh);

                // Если у тебя есть пересчёт бонусов/ёмкости логова — можно вызвать тут.
                // RecomputeLairBonus(); // если появится позже

                return;
            }

            if (button == MouseButtons.Right)
            {
                bool changed = false;
                int removedTiles = 0;

                for (int x = r.X; x < r.X + r.Width; x++)
                    for (int y = r.Y; y < r.Y + r.Height; y++)
                    {
                        if (x < 0 || y < 0 || x >= MapWidth || y >= MapHeight)
                            continue;

                        Tile t = map[x, y];
                        if (t.Room != RoomType.Lair)
                            continue;

                        t.Room = RoomType.None;
                        t.RoomVariant = 0;

                        MarkChunkDirtyByTile(x, y);
                        changed = true;
                        removedTiles++;
                    }
                    
                // Возврат денег (можешь сделать 50%, если хочешь как “штраф”)
                if (removedTiles > 0)
                {
                    AddGold(removedTiles * LairCostPerTile);
                }
                RecomputeBedCapacity();

                var refresh = r;
                refresh.Inflate(1, 1);
                RecomputeLairVisuals(refresh);



                return;
            }
        }

        private int CountLairNeighbors(int x, int y)
        {
            int n = 0;
            if (x > 0 && map[x - 1, y].Room == RoomType.Lair) n++;
            if (x < MapWidth - 1 && map[x + 1, y].Room == RoomType.Lair) n++;
            if (y > 0 && map[x, y - 1].Room == RoomType.Lair) n++;
            if (y < MapHeight - 1 && map[x, y + 1].Room == RoomType.Lair) n++;
            return n;
        }

        private void RecomputeLairVisuals(Rectangle area)
        {
            if (lairBmps == null || lairBmps.Length == 0)
                lairBmps = new[] { lair1Bmp, lair2Bmp, lair3Bmp, lair4Bmp };

            int x0 = Math.Max(0, area.X);
            int y0 = Math.Max(0, area.Y);
            int x1 = Math.Min(MapWidth - 1, area.X + area.Width - 1);
            int y1 = Math.Min(MapHeight - 1, area.Y + area.Height - 1);

            for (int x = x0; x <= x1; x++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    Tile t = map[x, y];
                    if (t.Room != RoomType.Lair)
                        continue;

                    int neigh = CountLairNeighbors(x, y);

                    // 0..3 (у тебя 4 спрайта)
                    byte variant;
                    if (neigh <= 0) variant = 0;
                    else if (neigh == 1) variant = 1;
                    else if (neigh == 2) variant = 2;
                    else variant = 3;

                    if (t.RoomVariant != variant)
                    {
                        t.RoomVariant = variant;
                        MarkChunkDirtyByTile(x, y);
                    }
                }
            }
        }


        private bool TryPickJobWithPath(Creature c, out DigJob job, out Point Approach, out List<Point> path)
        {
            job = null;
            Approach = new Point(-1, -1);
            path = null;

            DigJob bestJob = null;
            Point bestApproach = new Point(-1, -1);
            List<Point> bestPath = null;

            foreach (DigJob j in digJobs)
            {
                if (j.IsCancelled || j.IsAssigned)
                {
                    continue;
                }
                if (!TryGetApproachCell(c, j, out Point a, out List<Point> p))
                {
                    continue;
                }
                if (bestPath == null || p.Count < bestPath.Count)
                {
                    bestJob = j;
                    bestApproach = a;
                    bestPath = p;
                }
            }
            if (bestJob == null)
            {
                return false;
            }
            job = bestJob;
            Approach = bestApproach;
            path = bestPath;
            return true;

        }

        private Point PickRandomBaseCell()
        {
            for (int attempt = 0; attempt < 200; attempt++)
            {
                int x = rng.Next(BaseX, BaseX + BaseW);
                int y = rng.Next(BaseY, BaseY + BaseH);

                if (IsWalkable(x, y))
                    return new Point(x, y);
            }

            return new Point(heartX, heartY);
        }


        private void TryStartGoblinMoveToRally(Creature c)
        {
            Point start = new Point((int)c.X, (int)c.Y);
            Point goal = c.RallyCell;

            if (TryFindPath(start, goal, out List<Point> path))
            {
                c.Path = path;
                c.PathIndex = 0;
                c.State = CreatureState.GoingToRally;
            }
            else
            {
                c.State = CreatureState.Rallying; // нет пути — пусть стоит где есть
            }
        }


        private void UpdateGoblin(float dt, Creature c)
        {
            bool isWalking = (c.State == CreatureState.GoingToRally);
            c.UpdateAnimation(dt, isWalking);

            if (c.State == CreatureState.Idle)
            {
                // если почему-то без цели — выберем цель снова
                c.RallyCell = PickRandomBaseCell();
                TryStartGoblinMoveToRally(c);
                return;
            }

            if (c.State == CreatureState.GoingToRally)
            {
                if (c.Path == null || c.PathIndex >= c.Path.Count)
                {
                    c.State = CreatureState.Rallying;
                    c.ClearPath();
                    return;
                }

                Point nextCell = c.Path[c.PathIndex];
                float tx = nextCell.X + 0.5f;
                float ty = nextCell.Y + 0.5f;

                float dx = tx - c.X;
                float dy = ty - c.Y;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                float step = c.Speed * dt;

                if (dist < 0.05f)
                {
                    c.X = tx;
                    c.Y = ty;
                    c.PathIndex++;

                    if (c.PathIndex >= c.Path.Count)
                    {
                        c.State = CreatureState.Rallying;
                        c.ClearPath();
                    }
                    return;
                }

                if (step > dist) step = dist;
                c.X += dx / dist * step;
                c.Y += dy / dist * step;
                return;
            }

            if (c.State == CreatureState.Rallying)
            {
                // ищем ближайшего врага
                Creature enemy = null;
                float bestD2 = float.MaxValue;

                foreach (var other in creatures)
                {
                    if (other.State == CreatureState.Dead || other.Hp <= 0) continue;
                    if (other.Kind != CreatureKind.EnemyWarrior && other.Kind != CreatureKind.BossAntonius) continue;

                    float dx = other.X - c.X;
                    float dy = other.Y - c.Y;
                    float d2 = dx * dx + dy * dy;

                    if (d2 < bestD2)
                    {
                        bestD2 = d2;
                        enemy = other;
                    }
                }

                if (enemy != null)
                {
                    float dist = (float)Math.Sqrt(bestD2);

                    if (dist <= AttackRange)
                    {
                        // оставляемся в Rallying, чтобы каждый кадр продолжать бить
                        c.AttackTimer += dt;

                        if (c.AttackTimer >= AttackInterval)
                        {
                            c.AttackTimer = 0f;
                            enemy.Hp -= c.Damage;

                            if (enemy.Hp <= 0)
                            {
                                enemy.Hp = 0;
                                enemy.State = CreatureState.Dead;
                            }
                        }
                        return;
                    }


                    // если враг далеко — бежим к нему
                    c.RallyCell = new Point((int)enemy.X, (int)enemy.Y);
                    TryStartGoblinMoveToRally(c);
                    return;
                }

                // врагов нет — стоим
                return;
            }

        }



        //крутая функция, обновляем состояние нашего создания в зависимости от поставленной задачи
        private void UpdateCreature(float dt, Creature c)
        {

            //проверочка, а не отменена ли задачка
            //если таки она отменена, то юнит сразу же останавливается и забывает про эту задачку
            //ну или прекращает ее делать если уже начал делать 
            if (c.CurrentJob != null && c.CurrentJob.IsCancelled)
            {
                c.CurrentJob.Unassign();
                map[c.CurrentJob.X, c.CurrentJob.Y].DigJobAssigned = false;
                c.CurrentJob = null;
                c.State = CreatureState.Idle;
                c.WorkTimer = 0f;
                return;
            }

            //Это нужно для анимации движения, то есть если идет, то будь добр, меня картинки!
            bool IsWalking = (c.State == CreatureState.GoingToJob || c.State == CreatureState.CarryingToTreasury);
            c.UpdateAnimation(dt, IsWalking);

            //вот если сейчас он свободен, да еще есть задачки свободные, то будь добр, выполни ка задачку!!
            if (c.State == CreatureState.Idle)
            {
                if (c.CarryingGold > 0)
                {
                    TryStartCarryGoldToTreasury(c);
                    return;
                }
                if (TryPickJobWithPath(c, out DigJob job, out Point approach, out List<Point> path))
                {
                    if (job.TryAssign(c))
                    {
                        c.CurrentJob = job;
                        map[job.X, job.Y].DigJobAssigned = true;
                        c.ApproachCell = approach;
                        c.Path = path;
                        c.PathIndex = 0;
                        c.State = CreatureState.GoingToJob;
                    }
                }
                return;
            }
            //если сейчас выполняет задачу, то нужно двигаться к месту назначения (к тому тайлу, где находится задачка)
            //тут в силу входит лютейшая алгебра... нормализация вектора, то бишь получаем вектор направления, который указывает куда 
            //идет наш юнит, ну и мы умножаем этот вектор на скорость (4 тайла в сек), чтобы он двигался к цели
            if (c.State == CreatureState.GoingToJob)
            {

                if (c.Path == null || c.PathIndex >= c.Path.Count)
                {
                    c.CurrentJob?.Unassign();
                    c.CurrentJob = null;
                    c.State = CreatureState.Idle;
                    c.ClearPath();
                    return;
                }

                Point nextCell = c.Path[c.PathIndex];
                float targetX = nextCell.X + 0.5f;
                float targetY = nextCell.Y + 0.5f;

                float dx = targetX - c.X;
                float dy = targetY - c.Y;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                float step = c.Speed * dt;

                if (dist < 0.05f)
                {
                    c.X = targetX;
                    c.Y = targetY;
                    c.PathIndex++;

                    if (c.PathIndex >= c.Path.Count)
                    {
                        c.State = CreatureState.Working;
                        c.WorkTimer = 0f;
                    }
                    return;
                }

                
                if (step > dist)
                {
                    step = dist;
                }
                c.X += dx / dist * step;
                c.Y += dy / dist * step;
                return;
            }


            if (c.State == CreatureState.CarryingToTreasury)
            {
                if (c.Path == null || c.PathIndex >= c.Path.Count)
                {
                    c.State = CreatureState.Idle;
                    c.ClearPath();
                    return;
                }

                Point nextCell = c.Path[c.PathIndex];
                float targetX = nextCell.X + 0.5f;
                float targetY = nextCell.Y + 0.5f;

                float dx = targetX - c.X;
                float dy = targetY - c.Y;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                float step = c.Speed * dt;

                if (dist < 0.05f)
                {
                    c.X = targetX;
                    c.Y = targetY;
                    c.PathIndex++;
                    if (c.PathIndex >= c.Path.Count)
                    {
                        Point t = c.TreasuryTargetCell;
                        int put = DepositGoldToTreasuryTile(t.X, t.Y, c.CarryingGold);
                        c.CarryingGold -= put;
                        c.State = CreatureState.Idle;
                        c.ClearPath();
                        if (c.CarryingGold > 0)
                        {
                            TryStartCarryGoldToTreasury(c);
                        }
                    }
                    return;
                }
                if (step > dist)
                {
                    step = dist;
                }
                c.X += dx / dist * step;
                c.Y += dy / dist * step;
                return;
            }


            //елси он добрался до цели, то начинает копать, копает почти 1 сек, время высчитывается покадрово 0.16 сек
            //и тут же метод finisdigjob чтобы убрать задачку из списка + поменять тайл с камня на землю
            if (c.State == CreatureState.Working)
            {
                c.WorkTimer += dt;
                if (c.WorkTimer >= 0.2f)
                {
                    FinishDigJob(c);
                    c.State = CreatureState.Idle;
                    c.CurrentJob = null;
                }
            }
        }

        //и тут же метод finisdigjob чтобы убрать задачку из списка + поменять тайл с камня на землю
        private void FinishDigJob(Creature c)
        {
            DigJob job = c.CurrentJob;
            if (job == null)
            {
                return;
            }

            Tile tile = map[job.X, job.Y];

            bool wasGold = (tile.Type == TileType.GoldRock);

            tile.Type = TileType.Dirt;
            PlayDigSfx();

            MarkChunkDirtyByTile(job.X, job.Y);
            tile.HasDigJob = false;
            tile.DigJobAssigned = false;
            job.Unassign();

            if (wasGold)
            {
                c.CarryingGold+=250; //потом исправить 
            }

            RecomputeClaimedFromHeart();

            if (job.X == portalX && job.Y == portalY)
            {
                if (map[portalX, portalY].Type == TileType.Dirt && map[portalX, portalY].IsClaimed)
                    portalActive = true;
            }

            MarkAllChunksDirty();

            for (int i = digJobs.Count - 1; i >= 0; i--)
            {
                if (digJobs[i].X == job.X && digJobs[i].Y == job.Y)
                {
                    digJobs.RemoveAt(i);
                    break;
                }
            }
            
        }
        //??
        private DigJob FindNearestFreeJob(Creature c)
        {
            DigJob best = null;
            float bestDist = float.MaxValue;
            foreach (DigJob job in digJobs)
            {
                if (job.IsCancelled || job.IsAssigned)
                {
                    continue;
                }

                float targetX = job.X + 0.5f;
                float targetY = job.Y + 0.5f;

                float dx = targetX - c.X;
                float dy = targetY - c.Y;

                float d2 = dx * dx + dy * dy;
                if (d2 < bestDist)
                {
                    bestDist = d2;
                    best = job;

                }
            }
            return best;
        }

        //проверка на то, что мы можем дойти до нашего камушка по земле 
        private bool TryFindPath(Point start, Point goal, out List<Point> path)
        {
            path = null; //мы должны хоть что-то присвоить так как это out параметр
            if (!IsWalkable(start.X,start.Y) || !IsWalkable(goal.X, goal.Y))
            {
                return false;
            }

            bool[,] visited = new bool[MapWidth, MapHeight];
            Point[,] prev = new Point[MapWidth, MapHeight];
            //Enqueue(x) — добавить в конец очереди (для очереди)
            //Dequeue() — взять и удалить первый элемент (тоже для очереди) 
            Queue<Point> q = new Queue<Point>();

            visited[start.X, start.Y] = true;
            prev[start.X, start.Y] = new Point(-1, -1);
            q.Enqueue(start);

            //хитрые 2 массивчика для направлений 
            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };


            while (q.Count > 0)
            {
                Point cur = q.Dequeue();
                if (cur.X == goal.X && cur.Y == goal.Y)
                {
                    List<Point> rev = new List<Point>();
                    Point p = cur;
                    while (p.X != -1)
                    {
                        rev.Add(p);
                        p = prev[p.X, p.Y];

                    }
                    rev.Reverse();
                    path = rev;
                    return true;
                }
                //вот тут в цикле мы смотрим все 4 нарпавления и обрабатываем их 
                for (int i = 0; i < 4; i++)
                {
                    int nx = cur.X + dx[i];
                    int ny = cur.Y + dy[i];

                    if (!IsWalkable(nx, ny))
                    {
                        continue;
                    }

                    if (visited[nx, ny])
                    {
                        continue;
                    }
                    visited[nx, ny] = true;
                    prev[nx, ny] = cur;
                    q.Enqueue(new Point(nx, ny));
                }
            }
            return false;
        }
        //смотрим, в через какой пунть выгоднее всего идти (всего 4 дорожки) 
        private bool TryGetApproachCell (Creature c, DigJob job, out Point approachCell, out List<Point> path)
        {
            approachCell = new Point(-1, -1);
            path = null;

            Point start = new Point((int)c.X, (int)c.Y);
            Point[] candidates =
            {
                new Point(job.X+1, job.Y),
                new Point (job.X-1,job.Y),
                new Point (job.X, job.Y+1),
                new Point (job.X, job.Y-1),
            };

            List<Point> bestPath = null;
            Point bestCell = new Point(-1, -1);
            foreach (Point cand in candidates)
            {
                if (!IsWalkable(cand.X, cand.Y))
                {
                    continue;
                }
                if (TryFindPath(start, cand, out List<Point> CandPath))
                {
                    if (bestPath == null || CandPath.Count < bestPath.Count)
                    {
                        bestPath = CandPath;
                        bestCell = cand;
                    }
                }
            }
            if (bestPath == null)
            {
                return false;
            }
            approachCell = bestCell;
            path = bestPath;
            return true;
        }

        private void RecomputeClaimedFromHeart()
        {
            for (int x = 0; x < MapWidth; x++)
            {
                for (int  y = 0; y < MapHeight; y++)
                {
                    map[x, y].IsClaimed = false;
                }
            }

            if (!IsWalkable(heartX, heartY))
            {
                return;
            }

            bool[,] visited = new bool[MapWidth, MapHeight];
            Queue<System.Drawing.Point> q = new Queue<System.Drawing.Point>();
            visited[heartX, heartY] = true;
            map[heartX, heartY].IsClaimed = true;
            q.Enqueue(new System.Drawing.Point(heartX, heartY));
            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };

            while (q.Count> 0)
            {
                var cur = q.Dequeue();
                for (int i = 0; i < 4; i++)
                {
                    int nx = cur.X + dx[i];
                    int ny = cur.Y + dy[i];

                    if (nx <0 || nx>=MapWidth || ny < 0 || ny >= MapHeight)
                    {
                        continue;
                    }
                    if (visited[nx, ny])
                    {
                        continue;
                    }
                    if (!IsWalkable(nx, ny))
                    {
                        continue;
                    }
                    visited[nx, ny] = true;
                    map[nx, ny].IsClaimed = true;
                    q.Enqueue(new System.Drawing.Point(nx, ny));
                }
            }

            MarkAllChunksDirty();
        }

        private void RecomputeGoldCapacity()
        {
            int treasuryTails = 0;
            for (int x = 0; x < MapWidth; x++)
            {
                for (int y = 0; y < MapHeight; y++)
                {
                    if (map[x, y].Room == RoomType.Treasury) 
                    {
                        treasuryTails++;

                    }
                }
            }
            goldCapacity = treasuryTails * GoldCapacityPerTreasuryTile;

            int total = GetTotalGold();
            if (total > goldCapacity)
            {
                int extra = total - goldCapacity;
                for (int i = 0; i < MapWidth; i++)
                {
                    for (int j = 0; j < MapHeight; j++)
                    {
                        Tile t = map[i, j];
                        if (t.Room != RoomType.Treasury)
                        {
                            continue;
                        }
                        if (t.TreasuryGold <= 0)
                        {
                            continue;
                        }
                        int take = Math.Min(extra, t.TreasuryGold);
                        t.TreasuryGold -= take;
                        extra -= take;
                        if (take > 0) MarkChunkDirtyByTile(i, j);
                    }
                }
            }
           
        }
        private bool TrySpendGold(int amount)
        {
            if (amount <= 0)
            {
                return false;
            }
            int total = GetTotalGold();
            if (total < amount)
            {
                return false;
            }
            int left = amount;
            for (int x = 0;x<MapWidth && left > 0; x++)
            {
                for (int y = 0;y<MapHeight && left > 0; y++)
                {
                    Tile t = map[x, y];
                    if (t.Room != RoomType.Treasury)
                    {
                        continue;
                    }
                    int take = Math.Min(t.TreasuryGold, left);
                    t.TreasuryGold -= take;
                    left -= take;
                    if (take > 0) MarkChunkDirtyByTile(x, y);
                }
            }
            return true;
        }
        private void AddGold(int amount)
        {
            AddGoldToTreasury(amount);
        }

        private void HandleDigMouse(int tileX, int tileY, MouseButtons button)
        {
            Tile tile = map[tileX, tileY];
            if (button == MouseButtons.Left)
            {
                if ((tile.Type == TileType.Rock || tile.Type == TileType.GoldRock)&& !tile.HasDigJob)
                {
                    tile.HasDigJob = true;
                    tile.DigJobAssigned = false;
                    digJobs.Add(new DigJob(tileX, tileY));
                    
                }
            }
            else if (button == MouseButtons.Right)
            {
                if (tile.HasDigJob)
                {
                    tile.HasDigJob = false;
                    tile.DigJobAssigned = false;
                    for (int i =digJobs.Count -1;i>=0; i--)
                    {
                        if (digJobs[i].X == tileX && digJobs[i].Y == tileY)
                        {
                            
                            digJobs[i].Cancel();
                            if (digJobs[i].IsAssigned && digJobs[i].AssignedTo != null)
                            {
                                Creature c = digJobs[i].AssignedTo;

                                if (c.CurrentJob == digJobs[i])
                                {
                                    c.CurrentJob = null;
                                }
                                c.ClearPath();
                                c.State = CreatureState.Idle;
                                digJobs[i].Unassign();
                            }
                            digJobs.RemoveAt(i);
                            break;
                        }
                    }
                }
            }
        }
        public void HandleKeyDown(Keys key)
        {
            if (key == Keys.D)
                mode = ToolMode.Dig;

            if (key == Keys.T)
                mode = ToolMode.BuildTreasury;

            if (key == Keys.L)
                mode = ToolMode.BuildLair;

            if (key == Keys.Space)
                CenterCameraOnHeart();
        }



        private void PlaceGoldVeins()
        {
            int clusters = 6;
            int clusterSize = 10;

            Random rng = new Random();

            for (int i = 0; i < clusters; i++)
            {
                int cx = BaseX + BaseW + 3 + rng.Next(0, 10);
                int cy = BaseY + rng.Next(0, BaseH);

                for (int j = 0; j < clusterSize; j++)
                {
                    int x = cx + rng.Next(-2, 3);
                    int y = cy + rng.Next(-2, 3);
                    if (x < 0 || y<0 || x >= MapWidth || y >= MapHeight)
                    {
                        continue;
                    }

                    if (map[x,y].Type == TileType.Rock)
                    {
                        map[x, y].Type = TileType.GoldRock;
                    }
                }
            }
        }


        private int GetTotalGold()
        {
            int sum = 0;
            for (int x = 0; x < MapWidth; x++)
            {
                for (int y = 0; y < MapHeight; y++)
                {
                    if (map[x, y].Room == RoomType.Treasury)
                    {
                        sum += map[x, y].TreasuryGold;
                    }
                }
            }
            return sum;
        }

        private int GetFreeSpaceInTreausryTile (int x, int y)
        {
            if (map[x,y].Room  != RoomType.Treasury)
            {
                return 0;
            }
            int free = GoldCapacityPerTreasuryTile - map[x, y].TreasuryGold;
            if (free < 0)
            {
                free = 0;
            }
            return free;
        }

        private int DepositGoldToTreasuryTile(int x, int y, int amount)
        {
            if (amount < 0)
            {
                return 0;
            }
            Tile t = map[x, y];
            if (t.Room != RoomType.Treasury)
            {
                return 0;
            }
            int free = GoldCapacityPerTreasuryTile - t.TreasuryGold;
            if ( free <= 0)
            {
                return 0;
            }
            int put = Math.Min(amount, free);
            t.TreasuryGold += put;
            if (put > 0) MarkChunkDirtyByTile(x, y);
            return put;
        }

        private bool TryFindNearestTreasuryWithSpace(Point start, out Point treasuryCell, out List<Point> path)
        {
            treasuryCell = new Point(-1, -1);
            path = null;

            if (!IsWalkable(start.X, start.Y))
            {
                return false;
            }

            bool[,] visited = new bool[MapWidth, MapHeight];
            Point[,] prev = new Point[MapWidth, MapHeight];
            Queue<Point> q = new Queue<Point>();

            visited[start.X, start.Y] = true;
            prev[start.X, start.Y] = new Point(-1, -1);
            q.Enqueue(start);

            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };

            while(q.Count > 0)
            {
                Point cur = q.Dequeue();

                if (map[cur.X, cur.Y].Room == RoomType.Treasury && GetFreeSpaceInTreausryTile(cur.X, cur.Y) > 0)
                {
                    List<Point> rev = new List<Point>();
                    Point p = cur;
                    while (p.X != -1)
                    {
                        rev.Add(p);
                        p = prev[p.X, p.Y];

                    }

                    rev.Reverse();
                    treasuryCell = cur;
                    path = rev;
                    return true;
                }

                for (int i = 0; i< 4; i++)
                {
                    int px = cur.X + dx[i];
                    int py = cur.Y + dy[i];

                    if (!IsWalkable(px, py))
                    {
                        continue;
                    }
                    if (visited[px, py])
                    {
                        continue;
                    }
                    visited[px, py] = true;
                    prev[px, py] = cur;
                    q.Enqueue(new Point(px, py));
                
                }
            }
            return false;
        }

        private bool TryStartCarryGoldToTreasury(Creature c)
        {
            if (c.CarryingGold <= 0)
            {
                return false;
            }
            Point start = new Point((int)c.X, (int)c.Y);
            if (map[start.X, start.Y].Room == RoomType.Treasury)
            {
                int put = DepositGoldToTreasuryTile(start.X, start.Y, c.CarryingGold);
                c.CarryingGold -= put;
                if (c.CarryingGold <= 0)
                {
                    return true;
                }
            }
            if (TryFindNearestTreasuryWithSpace(start, out Point tcell, out List<Point> p))
            {
                c.TreasuryTargetCell = tcell;
                c.Path = p;
                c.PathIndex = 0;
                c.State = CreatureState.CarryingToTreasury;
                return true;
            }
            return false;
        }


        private int AddGoldToTreasury(int amount)
        {
            if (amount <= 0)
            {
                return 0;

            }
            int left = amount;
            for (int x = 0;x<MapWidth; x++)
            {
                for (int y = 0; y < MapHeight; y++)
                {
                    if (map[x,y].Room != RoomType.Treasury)
                    {
                        continue;
                    }
                    int put = DepositGoldToTreasuryTile(x, y, left);
                    left -= put;
                }
            }
            return amount - left;
        }






    }

}

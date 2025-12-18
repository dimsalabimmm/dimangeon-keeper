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



namespace dimangeon_keeper
{
    public class Game
    {
        private const int TileSize = 32; //это сколько пикселей уйедт на 1 тайл
        private const int MapWidth = 50; //а это сколько тайлов в ширину
        private const int MapHeight = 50; //ну а это в высоту
        private List<DigJob> digJobs = new List<DigJob>(); // zadachi impov 
        private List<Creature> creatures = new List<Creature>(); //moi impiki 

        private const int BaseX = 6;
        private const int BaseY = 6;
        private const int BaseW = 9;
        private const int BaseH = 9;

        private ToolMode mode = ToolMode.Dig;
        private int heartX;
        private int heartY;

        private int goldCapacity = 0;
        private int gold = 2000;

        private const int TreasuryCostPerTile = 300;
        private const int GoldCapacityPerTreasuryTile = 1000;

        private Tile[,] map;

        private bool IsWalkable (int x,int y)
        {
            if (x<0 || x >= MapWidth || y<0 || y >= MapHeight)
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
                    if (i < 0 || i >=MapWidth || j< 0 || j>= MapHeight)
                    {
                        continue;
                    }
                    map[i, j].Type = TileType.Dirt;
                    map[i, j].HasDigJob = false;

                }
            }
        }
        public Game()
        {
            CreateMap();
            DirtBase(BaseX, BaseY, BaseW, BaseH);

            heartX = BaseX + BaseW / 2;
            heartY = BaseY + BaseH / 2;

            map[heartX, heartY].Type = TileType.Dirt;

            RecomputeClaimedFromHeart();
            CreateInitialTreasury();
            RecomputeGoldCapacity();
            PlaceGoldVeins();

            //создаем 4 импа
            for (int i = 0; i < 4; i++)
            {
                Creature c = new Creature(BaseX + i+2, BaseY+5);

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
        public void Update(float dt)
        {
            foreach (Creature c in creatures)
            {
                UpdateCreature(dt, c);
            }
        }
        public void Draw(Graphics g)
        {
            DrawMap(g);
            DrawDungeonHeart(g);
            DrawCreatures(g);
            DrawHub(g);
        }

        private void DrawMap(Graphics g)
        {
            for (int x = 0; x < MapWidth; x++)
            {
                for (int y = 0; y < MapHeight; y++)
                {
                    DrawTile(g, x, y, map[x, y]);
                }
            }
        }
        private void DrawDungeonHeart(Graphics g)
        {
            Rectangle r = new Rectangle(heartX * TileSize, heartY * TileSize, TileSize, TileSize);
            using (Brush b = new SolidBrush(Color.DarkRed))
            {
                g.FillRectangle(b, r);
            }
            g.DrawRectangle(Pens.Black, r);
        }

        private void DrawCreatures(Graphics g)
        {
            //эээ, ну корочеее.. чтобы пискель арт не размывался :)
            //тупо надо!
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.SmoothingMode = SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.Half;



            foreach (Creature c in creatures)
            {
                Bitmap frame = c.GetCurrentFrame();
                if (frame == null)
                {
                    continue;
                }
                float px = c.X * TileSize;
                float py = c.Y * TileSize;

                int drawSize = 32;
                int drawX = (int)(px - drawSize / 2);
                int drawY = (int)(py - drawSize / 2);

                g.DrawImage(frame, new Rectangle(drawX, drawY, drawSize, drawSize));
            }
        }
        private readonly Font hubFont = new Font("Consolas",14,FontStyle.Bold);
        private void DrawHub(Graphics g)
        {
            string text = $"Mode: {mode} | Gold: {gold}/{goldCapacity} | (D=Dig, T=Treasury)";
            using (Brush b = new SolidBrush(Color.White))
            {
                g.DrawString(text, hubFont, b, 8, 8);
            }
        }
        private void DrawTile(Graphics g, int x, int y, Tile tile)
        {
            int screenX = x * TileSize;
            int screenY = y * TileSize;

            Rectangle rect = new Rectangle(screenX, screenY, TileSize, TileSize); //красный 
            Rectangle rect1 = new Rectangle(screenX+2, screenY+2, TileSize-5, TileSize-5);  //желтый (типо имп занял задчу) 
            Color color;
            if (tile.Type == TileType.Rock)
            {
                color = Color.DarkSlateGray;
            }
            else if (tile.Type == TileType.GoldRock)
            {
                color = Color.Goldenrod;
            }
            else
            {
                color = tile.IsClaimed ? Color.SaddleBrown : Color.FromArgb(60, 40, 25);
            }
            using (Brush brush = new SolidBrush(color))
            {
                g.FillRectangle(brush, rect);
            }
            if (tile.Room == RoomType.Treasury)
            {
                using (Brush b = new SolidBrush(Color.Goldenrod))
                {
                    g.FillRectangle(b, rect);
                }
            }
            g.DrawRectangle(Pens.Black, rect);
            if (tile.HasDigJob)
            {
                using (Pen pen = new Pen(Color.Red, 2))
                {
                    g.DrawRectangle(pen, rect);
                }
                DigJob job = FindJobAt(x, y);
                if (job!=null && job.IsAssigned && !job.IsCancelled)
                {
                    using (Pen pen2 = new Pen(Color.Yellow, 1))
                    {
                        g.DrawRectangle(pen2, rect1);
                    }
                }
            }
        }

        private void SetTreasury(int x, int y)
        {
            if (x<0 || y<0 || x >= MapWidth || y >= MapHeight)
            {
                return;
            }
            if (map[x, y].Type != TileType.Dirt)
            {
                return;
            }
            map[x, y].Room = RoomType.Treasury;
        }
        private void CreateInitialTreasury()
        {
            SetTreasury(heartX + 1, heartY);
            SetTreasury(heartX +1, heartY+1);
            SetTreasury(heartX + 2, heartY);
            SetTreasury(heartX + 2, heartY+1);

        }
        private DigJob FindJobAt(int x,int y)
        {
            for (int i = 0; i < digJobs.Count; i++)
            {
                if (digJobs[i].X == x && digJobs[i].Y == y)
                {
                    return digJobs[i];
                }
            }
            return null;
        }

        public void HandleMouseDown(int mouseX, int mouseY, MouseButtons button)
        {
            int tileX = mouseX / TileSize;
            int tileY = mouseY / TileSize;

            if (tileX < 0 || tileY < 0 || tileX >= MapWidth || tileY >= MapHeight)
            {
                return;
            }

            if (mode == ToolMode.Dig)
            {
                HandleDigMouse(tileX, tileY, button);
                return;
            }
            if (mode == ToolMode.BuildTreasury)
            {
                HandleTreasuryMouse(tileX, tileY, button);
                return;
            }
           
        }

        public void HandleKeyDown(Keys key)
        {
            if (key == Keys.D)
            {
                mode = ToolMode.Dig;
            }
            if (key == Keys.T)
            {
                mode = ToolMode.BuildTreasury;  
            }
        }
        private bool TryPickJobWithPath(Creature c, out DigJob job , out Point Approach, out List<Point> path)
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
                if (!TryGetApproachCell(c,j, out Point a, out List<Point> p))
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

        //крутая функция, обновляем состояние нашего создания в зависимости от поставленной задачи
        private void UpdateCreature(float dt, Creature c)
        {

            //проверочка, а не отменена ли задачка
            //если таки она отменена, то юнит сразу же останавливается и забывает про эту задачку
            //ну или прекращает ее делать если уже начал делать 
            if (c.CurrentJob != null && c.CurrentJob.IsCancelled)
            {
                c.CurrentJob.Unassign();
                c.CurrentJob = null;
                c.State = CreatureState.Idle;
                c.WorkTimer = 0f;
                return;
            }

            //Это нужно для анимации движения, то есть если идет, то будь добр, меня картинки!
            bool IsWalking = (c.State == CreatureState.GoingToJob);
            c.UpdateAnimation(dt, IsWalking);

            //вот если сейчас он свободен, да еще есть задачки свободные, то будь добр, выполни ка задачку!!
            if (c.State == CreatureState.Idle)
            {
                if (TryPickJobWithPath(c, out DigJob job, out Point approach, out List<Point> path))
                {
                    if (job.TryAssign(c))
                    {
                        c.CurrentJob = job;
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
            //елси он добрался до цели, то начинает копать, копает почти 1 сек, время высчитывается покадрово 0.16 сек
            //и тут же метод finisdigjob чтобы убрать задачку из списка + поменять тайл с камня на землю
            if (c.State == CreatureState.Working)
            {
                c.WorkTimer += dt;
                if (c.WorkTimer >= 0.8f)
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
            tile.HasDigJob = false;
            job.Unassign();

            if (wasGold)
            {
                AddGold(250); //потом исправить 
            }

            RecomputeClaimedFromHeart();

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
                    if (!IsWalkable(heartX, heartY))
                    {
                        continue;
                    }
                    visited[nx, ny] = true;
                    map[nx, ny].IsClaimed = true;
                    q.Enqueue(new System.Drawing.Point(nx, ny));
                }
            }
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
            if (gold > goldCapacity)
            {
                gold = goldCapacity;
            }
        }
        private bool TrySpendGold(int amount)
        {
            if (amount <= 0)
            {
                return false;
            }
            if (gold < amount)
            {
                return false;
            }
            gold -= amount;
            return true;
        }
        private void AddGold(int amount)
        {
            if (amount <= 0)
            {
                return;
            }
            gold += amount;
            if (gold > goldCapacity)
            {
                gold = goldCapacity;
            }
        }

        private void HandleDigMouse(int tileX, int tileY, MouseButtons button)
        {
            Tile tile = map[tileX, tileY];
            if (button == MouseButtons.Left)
            {
                if ((tile.Type == TileType.Rock || tile.Type == TileType.GoldRock)&& !tile.HasDigJob)
                {
                    tile.HasDigJob = true;
                    digJobs.Add(new DigJob(tileX, tileY));
                }
            }
            else if (button == MouseButtons.Right)
            {
                if (tile.HasDigJob)
                {
                    tile.HasDigJob = false;

                    for (int i =digJobs.Count -1;i>=0; i--)
                    {
                        if (digJobs[i].X == tileX && digJobs[i].Y == tileY)
                        {
                            digJobs[i].Cancel();
                            digJobs.RemoveAt(i);
                            break;
                        }
                    }
                }
            }
        }

        private void HandleTreasuryMouse (int tileX, int tileY, MouseButtons button)
        {
            Tile tile = map[tileX, tileY];
            if (tile.Type != TileType.Dirt)
            {
                return;
            }
            if (!tile.IsClaimed)
            {
                return;

            }
            if (button == MouseButtons.Left)
            {
                if (tile.Room == RoomType.None)
                {
                    if (!TrySpendGold(TreasuryCostPerTile))
                    {
                        return;
                    }
                    tile.Room = RoomType.Treasury;
                    RecomputeGoldCapacity();
                }
            }
            else if (button == MouseButtons.Right)
            {
                if (tile.Room == RoomType.Treasury)
                {
                    tile.Room = RoomType.None;
                    AddGold(TreasuryCostPerTile);
                    RecomputeGoldCapacity();

                }
            }
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




    }

}

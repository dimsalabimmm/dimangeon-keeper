using System;
using System.Drawing;
using System.Windows.Forms;

namespace dimangeon_keeper
{
    public partial class MainForm : Form
    {
        private readonly GamePanel gamePanel;
        private readonly Game game;
        private readonly Timer timer;
        private DateTime last;

        public MainForm()
        {
           

            KeyPreview = true;
            KeyDown += MainForm_KeyDown;

            gamePanel = new GamePanel
            {
                Dock = DockStyle.Fill,
                TabStop = true
            };
            Controls.Add(gamePanel);

            game = new Game();
            game.SetViewportSize(gamePanel.Width, gamePanel.Height);

            gamePanel.Paint += GamePanel_Paint;
            gamePanel.MouseDown += GamePanel_MouseDown;
            gamePanel.MouseMove += GamePanel_MouseMove;
            gamePanel.MouseUp += GamePanel_MouseUp;
            gamePanel.Resize += GamePanel_Resize;

            Shown += (s, e) => gamePanel.Focus();

            timer = new Timer { Interval = 16 };
            timer.Tick += Timer_tick;
            timer.Start();

            last = DateTime.Now;
        }

        private void Timer_tick(object sender, EventArgs e)
        {
            var now = DateTime.Now;
            float dt = (float)(now - last).TotalSeconds;
            last = now;

            game.Update(dt);
            gamePanel.Invalidate();
        }

        private void GamePanel_Paint(object sender, PaintEventArgs e)
        {
            game.Draw(e.Graphics);
        }

        private void GamePanel_MouseDown(object sender, MouseEventArgs e)
        {
            // Чтобы продолжать получать MouseMove/MouseUp даже если курсор уйдёт за пределы панели
            gamePanel.Capture = true;

            game.HandleMouseDown(e.X, e.Y, e.Button);

            // На всякий случай сразу перерисуем (появится preview рамка)
            gamePanel.Invalidate();
        }

        private void GamePanel_MouseMove(object sender, MouseEventArgs e)
        {
            // Камера (edge scroll) использует это
            game.SetMousePosition(e.X, e.Y);

            // ВАЖНО: это обновляет dragEndTile при перетаскивании прямоугольника
            game.HandleMouseMove(e.X, e.Y);

            // Чтобы рамка прямоугольника рисовалась в реальном времени
            gamePanel.Invalidate();
        }

        private void GamePanel_MouseUp(object sender, MouseEventArgs e)
        {
            // Перед отпусканием можно обновить конечную точку ещё раз
            game.HandleMouseMove(e.X, e.Y);

            game.HandleMouseUp(e.X, e.Y, e.Button);

            gamePanel.Capture = false;
            gamePanel.Invalidate();
        }

        private void GamePanel_Resize(object sender, EventArgs e)
        {
            game.SetViewportSize(gamePanel.Width, gamePanel.Height);
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left)
            {
                game.PanCameraByTiles(-1, 0);
                return;
            }
            if (e.KeyCode == Keys.Right)
            {
                game.PanCameraByTiles(1, 0);
                return;
            }
            if (e.KeyCode == Keys.Up)
            {
                game.PanCameraByTiles(0, -1);
                return;
            }
            if (e.KeyCode == Keys.Down)
            {
                game.PanCameraByTiles(0, 1);
                return;
            }

            game.HandleKeyDown(e.KeyCode);
        }
    }
}

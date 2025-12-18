using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Deployment.Application;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            InitializeComponent();

            KeyPreview = true;
            KeyDown += MainForm_KeyDown;


            gamePanel = new GamePanel
            {
                Dock = DockStyle.Fill
            };
            Controls.Add(gamePanel);

            game = new Game();
            gamePanel.Paint += GamePanel_Paint;
            gamePanel.MouseDown += GamePanel_MouseDown;

            timer = new Timer { Interval = 16};
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
            game.HandleMouseDown(e.X, e.Y, e.Button);
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            game.HandleKeyDown(e.KeyCode);  
        }
    }
}

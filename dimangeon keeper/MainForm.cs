using AxWMPLib;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace dimangeon_keeper
{
    public partial class MainForm : Form
    {
        private readonly GamePanel gamePanel;
        private readonly Game game;
        private readonly Timer timer;
        private DateTime last;


        private AxWindowsMediaPlayer sfxPlayer;
        private AxWindowsMediaPlayer musicPlayer;

        private readonly Dictionary<string, string> extractedMp3 = new Dictionary<string, string>();
        private readonly Random sfxRng = new Random();

        private DateTime lastHitSfx = DateTime.MinValue;
        private AxWindowsMediaPlayer axWindowsMediaPlayer1;
        private DateTime lastTeleportSfx = DateTime.MinValue;

        private bool introPlayed = false;



        private void InitAudioPlayers()
        {
            sfxPlayer = new AxWindowsMediaPlayer();
            sfxPlayer.CreateControl();
            sfxPlayer.Visible = false;
            sfxPlayer.settings.volume = 80;
            Controls.Add(sfxPlayer);

            musicPlayer = new AxWindowsMediaPlayer();
            musicPlayer.CreateControl();
            musicPlayer.Visible = false;
            musicPlayer.settings.volume = 40;
            musicPlayer.settings.setMode("loop", false);
            Controls.Add(musicPlayer);
        }

        private string ExtractMp3(string key, byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return null;

            string dir = Path.Combine(Path.GetTempPath(), "dimangeon_keeper_audio");
            Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, key + ".mp3");

            // пишем файл только если его нет или он другой по размеру
            if (!File.Exists(path) || new FileInfo(path).Length != bytes.Length)
                File.WriteAllBytes(path, bytes);

            extractedMp3[key] = path;
            return path;
        }

        private void ExtractAllMp3FromResources()
        {
            // Замени имена на свои реальные (это важно)
            ExtractMp3("hit1", Properties.Resources.fight1);
            ExtractMp3("hit2", Properties.Resources.fight2);
            ExtractMp3("hit3", Properties.Resources.fight3);
            ExtractMp3("intro", Properties.Resources.startmp);


            ExtractMp3("teleport", Properties.Resources.portal);

            ExtractMp3("victory", Properties.Resources.win);
            ExtractMp3("defeat", Properties.Resources.game_over);
        }

        private void PlaySfx(string key)
        {
            if (!extractedMp3.TryGetValue(key, out string path) || string.IsNullOrEmpty(path))
                return;

            sfxPlayer.URL = path;
            sfxPlayer.Ctlcontrols.play();
        }

        private void PlayRandomHitSfx()
        {
            // кулдаун, иначе WMP может заикаться
            if ((DateTime.Now - lastHitSfx).TotalMilliseconds < 90)
                return;

            lastHitSfx = DateTime.Now;

            int r = sfxRng.Next(1, 4); // 1..3
            PlaySfx("hit" + r);
        }

        private void PlayTeleportSfx()
        {
            if ((DateTime.Now - lastTeleportSfx).TotalMilliseconds < 140)
                return;

            lastTeleportSfx = DateTime.Now;
            PlaySfx("teleport");
        }

        private void PlayEndMusic(bool win)
        {
            string key = win ? "victory" : "defeat";
            if (!extractedMp3.TryGetValue(key, out string path) || string.IsNullOrEmpty(path))
                return;

            musicPlayer.settings.setMode("loop", false);
            musicPlayer.URL = path;
            musicPlayer.Ctlcontrols.play();
        }
        private void MainForm_Shown(object sender, EventArgs e)
        {
            gamePanel.Focus();

            if (introPlayed) return;
            introPlayed = true;

            PlayIntroSpeech();
        }

        private void PlayIntroSpeech()
        {
            if (!extractedMp3.TryGetValue("intro", out string path))
                return;

            musicPlayer.settings.setMode("loop", false);
            musicPlayer.settings.volume = 70; // по желанию
            musicPlayer.URL = path;
            musicPlayer.Ctlcontrols.play();
        }


        public MainForm()
        {

            InitAudioPlayers();
            ExtractAllMp3FromResources();

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

            game.HitSfxRequested += PlayRandomHitSfx;
            game.TeleportSfxRequested += PlayTeleportSfx;
            game.EndMusicRequested += PlayEndMusic;

            gamePanel.Paint += GamePanel_Paint;
            gamePanel.MouseDown += GamePanel_MouseDown;
            gamePanel.MouseMove += GamePanel_MouseMove;
            gamePanel.MouseUp += GamePanel_MouseUp;
            gamePanel.Resize += GamePanel_Resize;

            Shown += MainForm_Shown;


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

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.axWindowsMediaPlayer1 = new AxWMPLib.AxWindowsMediaPlayer();
            ((System.ComponentModel.ISupportInitialize)(this.axWindowsMediaPlayer1)).BeginInit();
            this.SuspendLayout();
            // 
            // axWindowsMediaPlayer1
            // 
            this.axWindowsMediaPlayer1.Enabled = true;
            this.axWindowsMediaPlayer1.Location = new System.Drawing.Point(314, 148);
            this.axWindowsMediaPlayer1.Name = "axWindowsMediaPlayer1";
            this.axWindowsMediaPlayer1.OcxState = ((System.Windows.Forms.AxHost.State)(resources.GetObject("axWindowsMediaPlayer1.OcxState")));
            this.axWindowsMediaPlayer1.Size = new System.Drawing.Size(75, 23);
            this.axWindowsMediaPlayer1.TabIndex = 0;
            // 
            // MainForm
            // 
            this.ClientSize = new System.Drawing.Size(417, 314);
            this.Controls.Add(this.axWindowsMediaPlayer1);
            this.Name = "MainForm";
            ((System.ComponentModel.ISupportInitialize)(this.axWindowsMediaPlayer1)).EndInit();
            this.ResumeLayout(false);

        }
    }
}

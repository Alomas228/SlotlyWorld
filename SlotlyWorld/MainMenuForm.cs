using System;
using System.Drawing;
using System.Windows.Forms;

namespace SlotlyWorld
{
    public class MainMenuForm : Form
    {
        private readonly Button btnNewGame;
        private readonly Button btnContinue;
        private readonly Button btnExit;
        private readonly Label titleLabel;

        public MainMenuForm()
        {
            Text = "SlotlyWorld";
            ClientSize = new Size(420, 380);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            BackColor = Color.FromArgb(30, 30, 40);

            titleLabel = new Label
            {
                Text = "SlotlyWorld",
                Font = new Font("Arial", 28, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(ClientSize.Width, 60),
                Location = new Point(0, 40),
            };
            Controls.Add(titleLabel);

            btnNewGame = MakeButton("Начать игру", 140);
            btnNewGame.Click += BtnNewGame_Click;
            Controls.Add(btnNewGame);

            btnContinue = MakeButton("Продолжить игру", 205);
            btnContinue.Click += BtnContinue_Click;
            Controls.Add(btnContinue);

            btnExit = MakeButton("Выйти", 290);
            btnExit.Click += (s, e) => Close();
            Controls.Add(btnExit);

            RefreshContinueButton();
        }

        private Button MakeButton(string text, int y)
        {
            const int width = 260;
            return new Button
            {
                Text = text,
                Font = new Font("Arial", 14, FontStyle.Bold),
                Size = new Size(width, 50),
                Location = new Point((ClientSize.Width - width) / 2, y),
                BackColor = Color.FromArgb(60, 60, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
            };
        }

        private void RefreshContinueButton()
        {
            bool exists = SaveSystem.Exists();
            btnContinue.Enabled = exists;
            btnContinue.BackColor = exists
                ? Color.FromArgb(60, 90, 60)
                : Color.FromArgb(40, 40, 50);
            btnContinue.ForeColor = exists ? Color.White : Color.Gray;
        }

        private void BtnNewGame_Click(object sender, EventArgs e)
        {
            if (SaveSystem.Exists())
            {
                var result = MessageBox.Show(
                    "Существующее сохранение будет удалено. Продолжить?",
                    "Новая игра",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (result != DialogResult.Yes) return;
                SaveSystem.Delete();
            }
            LaunchGame(loadExisting: false);
        }

        private void BtnContinue_Click(object sender, EventArgs e)
        {
            LaunchGame(loadExisting: true);
        }

        private void LaunchGame(bool loadExisting)
        {
            Hide();
            try
            {
                using (var game = new Form1(loadExisting))
                {
                    game.ShowDialog(this);
                }
            }
            finally
            {
                RefreshContinueButton();
                Show();
                BringToFront();
            }
        }
    }
}

using System;

namespace SlotlyWorld
{
    partial class Form1
    {
        /// <summary>
        /// Обязательная переменная конструктора.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Освободить все используемые ресурсы.
        /// </summary>
        /// <param name="disposing">истинно, если управляемый ресурс должен быть удален; иначе ложно.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                components?.Dispose();
                worldGraphics?.Dispose();
                worldBitmap?.Dispose();
                miniMapFrameGraphics?.Dispose();
                miniMapFrame?.Dispose();
                miniMapCache?.Dispose();
                hudFont?.Dispose();
                hudTextBrush?.Dispose();
                hudBgBrush?.Dispose();
            }
            base.Dispose(disposing);
        }

        private void picWorld_Click(object sender, EventArgs e)
        {
            // Обработка клика по PictureBox

        }

        private void picWorld_MouseEnter(object sender, EventArgs e)
        {
            picWorld.Focus();
        }

        #region Код, автоматически созданный конструктором форм Windows

        /// <summary>
        /// Требуемый метод для поддержки конструктора — не изменяйте 
        /// содержимое этого метода с помощью редактора кода.
        /// </summary>
        private void InitializeComponent()
        {
            this.picWorld = new System.Windows.Forms.PictureBox();
            ((System.ComponentModel.ISupportInitialize)(this.picWorld)).BeginInit();
            this.SuspendLayout();
            // 
            // picWorld
            // 
            this.picWorld.Location = new System.Drawing.Point(23, 7);
            this.picWorld.Name = "picWorld";
            this.picWorld.Size = new System.Drawing.Size(1920, 1080);
            this.picWorld.TabIndex = 0;
            this.picWorld.TabStop = false;
            this.picWorld.Click += new System.EventHandler(this.picWorld_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(811, 472);
            this.Controls.Add(this.picWorld);
            this.Name = "Form1";
            this.Text = "Form1";
            this.Load += new System.EventHandler(this.Form1_Load);
            ((System.ComponentModel.ISupportInitialize)(this.picWorld)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.PictureBox picWorld;
    }
}


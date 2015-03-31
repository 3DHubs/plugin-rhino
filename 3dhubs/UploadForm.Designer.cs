namespace _3dhubs
{
    partial class UploadForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(UploadForm));
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.title = new System.Windows.Forms.Label();
            this.icon = new System.Windows.Forms.PictureBox();
            this.actionButton = new System.Windows.Forms.Button();
            this.status = new System.Windows.Forms.RichTextBox();
            ((System.ComponentModel.ISupportInitialize)(this.icon)).BeginInit();
            this.SuspendLayout();
            // 
            // progressBar
            // 
            this.progressBar.Location = new System.Drawing.Point(12, 110);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(395, 40);
            this.progressBar.TabIndex = 0;
            // 
            // title
            // 
            this.title.AutoSize = true;
            this.title.Font = new System.Drawing.Font("Segoe UI", 24F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel, ((byte)(0)));
            this.title.Location = new System.Drawing.Point(69, 12);
            this.title.Margin = new System.Windows.Forms.Padding(6, 0, 6, 0);
            this.title.Name = "title";
            this.title.Size = new System.Drawing.Size(65, 32);
            this.title.TabIndex = 2;
            this.title.Text = "Title";
            // 
            // icon
            // 
            this.icon.Image = ((System.Drawing.Image)(resources.GetObject("icon.Image")));
            this.icon.Location = new System.Drawing.Point(12, 12);
            this.icon.Name = "icon";
            this.icon.Size = new System.Drawing.Size(48, 50);
            this.icon.TabIndex = 1;
            this.icon.TabStop = false;
            // 
            // actionButton
            // 
            this.actionButton.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.actionButton.Location = new System.Drawing.Point(413, 110);
            this.actionButton.Name = "actionButton";
            this.actionButton.Size = new System.Drawing.Size(94, 40);
            this.actionButton.TabIndex = 4;
            this.actionButton.Text = "&Cancel";
            this.actionButton.UseVisualStyleBackColor = true;
            this.actionButton.Click += new System.EventHandler(this.ActionButtonClick);
            // 
            // status
            // 
            this.status.BackColor = System.Drawing.SystemColors.Control;
            this.status.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.status.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.status.Location = new System.Drawing.Point(75, 52);
            this.status.Margin = new System.Windows.Forms.Padding(0);
            this.status.Name = "status";
            this.status.ReadOnly = true;
            this.status.ShortcutsEnabled = false;
            this.status.Size = new System.Drawing.Size(432, 55);
            this.status.TabIndex = 5;
            this.status.Text = "Here\'s a long message to see whether there\'s enough space for a wrapped diagnosti" +
    "c.";
            this.status.Enter += new System.EventHandler(this.StatusTextEnter);
            // 
            // UploadForm
            // 
            this.AcceptButton = this.actionButton;
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 21F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.actionButton;
            this.ClientSize = new System.Drawing.Size(519, 162);
            this.Controls.Add(this.status);
            this.Controls.Add(this.actionButton);
            this.Controls.Add(this.title);
            this.Controls.Add(this.icon);
            this.Controls.Add(this.progressBar);
            this.DoubleBuffered = true;
            this.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.Name = "UploadForm";
            this.ShowInTaskbar = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.Text = "3D Hubs";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.UploadFormClosing);
            ((System.ComponentModel.ISupportInitialize)(this.icon)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.PictureBox icon;
        private System.Windows.Forms.Label title;
        private System.Windows.Forms.Button actionButton;
        private System.Windows.Forms.RichTextBox status;
    }
}
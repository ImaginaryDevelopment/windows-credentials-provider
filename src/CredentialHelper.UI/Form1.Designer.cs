namespace CredentialHelper.UI
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.cameraIndexComboBox = new System.Windows.Forms.ComboBox();
            this.runButton = new System.Windows.Forms.Button();
            this.snapButton = new System.Windows.Forms.Button();
            this.txtQrValue = new System.Windows.Forms.TextBox();
            this.btnLogin = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // pictureBox1
            // 
            this.pictureBox1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pictureBox1.Location = new System.Drawing.Point(0, 0);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(862, 546);
            this.pictureBox1.TabIndex = 0;
            this.pictureBox1.TabStop = false;
            // 
            // cameraIndexComboBox
            // 
            this.cameraIndexComboBox.FormattingEnabled = true;
            this.cameraIndexComboBox.Location = new System.Drawing.Point(22, 491);
            this.cameraIndexComboBox.Name = "cameraIndexComboBox";
            this.cameraIndexComboBox.Size = new System.Drawing.Size(121, 21);
            this.cameraIndexComboBox.TabIndex = 1;
            this.cameraIndexComboBox.SelectedValueChanged += new System.EventHandler(this.cameraIndexComboBox_SelectedValueChanged);
            // 
            // runButton
            // 
            this.runButton.Enabled = false;
            this.runButton.Location = new System.Drawing.Point(149, 489);
            this.runButton.Name = "runButton";
            this.runButton.Size = new System.Drawing.Size(75, 23);
            this.runButton.TabIndex = 2;
            this.runButton.Text = "run";
            this.runButton.UseVisualStyleBackColor = true;
            this.runButton.Click += new System.EventHandler(this.runButton_Click);
            // 
            // snapButton
            // 
            this.snapButton.Location = new System.Drawing.Point(230, 489);
            this.snapButton.Name = "snapButton";
            this.snapButton.Size = new System.Drawing.Size(75, 23);
            this.snapButton.TabIndex = 3;
            this.snapButton.Text = "snapButton";
            this.snapButton.UseVisualStyleBackColor = true;
            this.snapButton.Click += new System.EventHandler(this.snapButton_Click);
            // 
            // txtQrValue
            // 
            this.txtQrValue.Location = new System.Drawing.Point(311, 491);
            this.txtQrValue.Name = "txtQrValue";
            this.txtQrValue.Size = new System.Drawing.Size(100, 20);
            this.txtQrValue.TabIndex = 4;
            this.txtQrValue.Text = "320016909";
            // 
            // btnLogin
            // 
            this.btnLogin.Location = new System.Drawing.Point(417, 489);
            this.btnLogin.Name = "btnLogin";
            this.btnLogin.Size = new System.Drawing.Size(75, 23);
            this.btnLogin.TabIndex = 5;
            this.btnLogin.Text = "Login";
            this.btnLogin.UseVisualStyleBackColor = true;
            this.btnLogin.Click += new System.EventHandler(this.btnLogin_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(862, 546);
            this.Controls.Add(this.btnLogin);
            this.Controls.Add(this.txtQrValue);
            this.Controls.Add(this.snapButton);
            this.Controls.Add(this.runButton);
            this.Controls.Add(this.cameraIndexComboBox);
            this.Controls.Add(this.pictureBox1);
            this.Name = "Form1";
            this.Text = "Form1";
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.ComboBox cameraIndexComboBox;
        private System.Windows.Forms.Button runButton;
        private System.Windows.Forms.Button snapButton;
        private System.Windows.Forms.TextBox txtQrValue;
        private System.Windows.Forms.Button btnLogin;
    }
}
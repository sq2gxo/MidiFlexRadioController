
namespace MidiFlexRadioController
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            statusStrip = new StatusStrip();
            statusLabel = new ToolStripStatusLabel();
            label1 = new Label();
            label2 = new Label();
            label3 = new Label();
            radioLabel = new Label();
            statusStrip.SuspendLayout();
            SuspendLayout();
            // 
            // statusStrip
            // 
            statusStrip.Items.AddRange(new ToolStripItem[] { statusLabel });
            statusStrip.Location = new Point(0, 89);
            statusStrip.Name = "statusStrip";
            statusStrip.Size = new Size(384, 22);
            statusStrip.TabIndex = 1;
            statusStrip.Text = "statusStrip1";
            // 
            // statusLabel
            // 
            statusLabel.Name = "statusLabel";
            statusLabel.Size = new Size(0, 17);
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("Segoe UI", 14.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            label1.Location = new Point(12, 9);
            label1.Name = "label1";
            label1.Size = new Size(158, 25);
            label1.TabIndex = 2;
            label1.Text = "MIDI Controller:";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Font = new Font("Segoe UI", 14.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            label2.Location = new Point(12, 43);
            label2.Name = "label2";
            label2.Size = new Size(109, 25);
            label2.TabIndex = 3;
            label2.Text = "Flex Radio:";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Font = new Font("Segoe UI", 14.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            label3.ForeColor = SystemColors.HotTrack;
            label3.Location = new Point(167, 9);
            label3.Name = "label3";
            label3.Size = new Size(183, 25);
            label3.TabIndex = 4;
            label3.Text = "DJControl Starlight";
            // 
            // radioLabel
            // 
            radioLabel.AutoSize = true;
            radioLabel.Font = new Font("Segoe UI", 14.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            radioLabel.ForeColor = SystemColors.HotTrack;
            radioLabel.Location = new Point(121, 43);
            radioLabel.Name = "radioLabel";
            radioLabel.Size = new Size(0, 25);
            radioLabel.TabIndex = 7;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(384, 111);
            Controls.Add(radioLabel);
            Controls.Add(label3);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(statusStrip);
            Name = "MainForm";
            Text = "FlexRadio MIDI Control";
            FormClosing += MainForm_FormClosing;
            Load += MainForm_Load;
            statusStrip.ResumeLayout(false);
            statusStrip.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusLabel;
        private Label label1;
        private Label label2;
        private Label label3;
        private Label radioLabel;
    }
}

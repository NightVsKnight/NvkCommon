using System.Drawing;
using System.Windows.Forms;

namespace NvkCommon
{
    partial class HotKeyControl
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            buttonPredefinedKeyBindRemove = new Button();
            buttonPredefinedKeyBindAdd = new Button();
            textBoxPredefinedKeyBind = new TextBox();
            SuspendLayout();
            // 
            // buttonPredefinedKeyBindRemove
            // 
            buttonPredefinedKeyBindRemove.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            buttonPredefinedKeyBindRemove.AutoSize = true;
            buttonPredefinedKeyBindRemove.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            buttonPredefinedKeyBindRemove.Location = new Point(175, 3);
            buttonPredefinedKeyBindRemove.Name = "buttonPredefinedKeyBindRemove";
            buttonPredefinedKeyBindRemove.Size = new Size(22, 25);
            buttonPredefinedKeyBindRemove.TabIndex = 3;
            buttonPredefinedKeyBindRemove.Text = "-";
            buttonPredefinedKeyBindRemove.UseVisualStyleBackColor = true;
            buttonPredefinedKeyBindRemove.Click += buttonPredefinedKeyBindRemove_Click;
            // 
            // buttonPredefinedKeyBindAdd
            // 
            buttonPredefinedKeyBindAdd.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            buttonPredefinedKeyBindAdd.AutoSize = true;
            buttonPredefinedKeyBindAdd.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            buttonPredefinedKeyBindAdd.Location = new Point(144, 3);
            buttonPredefinedKeyBindAdd.Name = "buttonPredefinedKeyBindAdd";
            buttonPredefinedKeyBindAdd.Size = new Size(25, 25);
            buttonPredefinedKeyBindAdd.TabIndex = 2;
            buttonPredefinedKeyBindAdd.Text = "+";
            buttonPredefinedKeyBindAdd.UseVisualStyleBackColor = true;
            buttonPredefinedKeyBindAdd.Click += buttonPredefinedKeyBindAdd_Click;
            // 
            // textBoxPredefinedKeyBind
            // 
            textBoxPredefinedKeyBind.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            textBoxPredefinedKeyBind.BackColor = Color.Green;
            textBoxPredefinedKeyBind.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
            textBoxPredefinedKeyBind.ForeColor = SystemColors.ControlLightLight;
            textBoxPredefinedKeyBind.Location = new Point(3, 5);
            textBoxPredefinedKeyBind.Name = "textBoxPredefinedKeyBind";
            textBoxPredefinedKeyBind.ReadOnly = true;
            textBoxPredefinedKeyBind.Size = new Size(135, 23);
            textBoxPredefinedKeyBind.TabIndex = 1;
            // 
            // HotKeyControl
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(buttonPredefinedKeyBindRemove);
            Controls.Add(buttonPredefinedKeyBindAdd);
            Controls.Add(textBoxPredefinedKeyBind);
            Name = "HotKeyControl";
            Size = new Size(200, 35);
            Load += HotKeyControl_Load;
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.Button buttonPredefinedKeyBindRemove;
        private System.Windows.Forms.Button buttonPredefinedKeyBindAdd;
        private System.Windows.Forms.TextBox textBoxPredefinedKeyBind;
    }
}

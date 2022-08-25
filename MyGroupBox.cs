using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NvkCommon
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
    public partial class MyGroupBox : GroupBox
    {
        private Color _borderColor = Color.Black;
        public Color BorderColor
        {
            get => _borderColor;
            set
            {
                _borderColor = value;
                Invalidate();
            }
        }

        private bool _IsBoldBorder = false;

        public bool IsBoldBorder
        {
            get
            {
                return _IsBoldBorder;
            }
            set
            {
                _IsBoldBorder = value;
                Invalidate();
            }
        }

        public MyGroupBox() : base()
        {
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!IsBoldBorder)
            {
                base.OnPaint(e);
                return;
            }

            // See GroupBox.OnPaint->GroupBoxRenderer.DrawGroupBox->GroupBoxRenderer.DrawThemedGroupBoxWithText...

            int thickness = 3;
            int halfThickness = thickness / 2;

            Size tSize = TextRenderer.MeasureText(this.Text, this.Font);
            Rectangle borderRect = e.ClipRectangle;
            borderRect.Y += tSize.Height / 2;
            borderRect.Height -= tSize.Height / 2;
            using (Pen p = new Pen(BorderColor, thickness))
            {
                e.Graphics.DrawRectangle(p, new Rectangle(borderRect.X + halfThickness,
                                                          borderRect.Y,
                                                          borderRect.Width - thickness,
                                                          borderRect.Height - thickness));
            }

            // TODO: Improve this text rendering margin...
            Rectangle textRect = e.ClipRectangle;
            textRect.X += 8;
            textRect.Width = tSize.Width + 3;
            textRect.Height = tSize.Height;
            e.Graphics.FillRectangle(new SolidBrush(this.BackColor), textRect);
            e.Graphics.DrawString(this.Text, this.Font, new SolidBrush(this.ForeColor), textRect);
        }
    }
}

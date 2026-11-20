using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace AcrFfbGain
{
    public static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(23, 25, 31);
        public static readonly Color Panel = Color.FromArgb(31, 34, 42);
        public static readonly Color PanelAlt = Color.FromArgb(38, 42, 52);
        public static readonly Color Hover = Color.FromArgb(44, 48, 60);
        public static readonly Color Border = Color.FromArgb(52, 57, 70);
        public static readonly Color Text = Color.FromArgb(232, 234, 240);
        public static readonly Color Muted = Color.FromArgb(146, 153, 170);
        public static readonly Color Faint = Color.FromArgb(96, 102, 118);
        public static readonly Color Accent = Color.FromArgb(255, 122, 26);
        public static readonly Color Good = Color.FromArgb(72, 199, 116);
        public static readonly Color Bad = Color.FromArgb(232, 72, 72);
        public static readonly Color Track = Color.FromArgb(60, 65, 80);

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = radius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    public class FlatButton : Button
    {
        public FlatButton(string text)
        {
            Text = text;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderColor = Theme.Border;
            FlatAppearance.MouseOverBackColor = Theme.PanelAlt;
            FlatAppearance.MouseDownBackColor = Theme.Border;
            BackColor = Theme.Panel;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9.5f);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Padding = new Padding(8, 2, 8, 2);
            Cursor = Cursors.Hand;
            UseVisualStyleBackColor = false;
        }
    }

    /// <summary>
    /// Compact "−  160  +" control. Click −/+ to step by 5 (hold to repeat), click the number to type.
    /// </summary>
    public class GainStepper : Control
    {
        int _value = 100;
        bool _mixed;
        int _hover;   // -1 minus, 0 number, 1 plus, 2 none
        int _pressed = 2;
        readonly Timer _repeat = new Timer();
        TextBox _edit;
        public int Step = 5;
        public event EventHandler ValueChanged;

        public GainStepper()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            Size = new Size(132, 34);
            _hover = 2;
            _repeat.Tick += delegate
            {
                _repeat.Interval = 70;
                if (_pressed == -1) Nudge(-1);
                else if (_pressed == 1) Nudge(1);
            };
        }

        public int Value
        {
            get { return _value; }
            set
            {
                int v = Math.Max(0, Math.Min(200, value));
                if (v == _value && !_mixed) return;
                _value = v;
                _mixed = false;
                Invalidate();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }

        /// <summary>Shows "mixed" instead of a number (stages of a rally have different values).</summary>
        public bool Mixed
        {
            get { return _mixed; }
            set { if (_mixed != value) { _mixed = value; Invalidate(); } }
        }

        public void SetSilently(int v)
        {
            _value = Math.Max(0, Math.Min(200, v));
            Invalidate();
        }

        void Nudge(int dir)
        {
            // Snap to the step grid: 147 -> 150 / 145
            int v = dir > 0 ? (_value / Step + 1) * Step : ((_value + Step - 1) / Step - 1) * Step;
            Value = v;
        }

        int PartAt(Point p)
        {
            int b = Height;
            if (p.X < b) return -1;
            if (p.X >= Width - b) return 1;
            return 0;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = PartAt(e.Location);
            if (h != _hover) { _hover = h; Cursor = h == 0 ? Cursors.IBeam : Cursors.Hand; Invalidate(); }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = 2;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || !Enabled) return;
            int part = PartAt(e.Location);
            if (part == 0) { BeginEdit(); return; }
            _pressed = part;
            Nudge(part);
            _repeat.Interval = 400;
            _repeat.Start();
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _repeat.Stop();
            _pressed = 2;
            Invalidate();
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Invalidate();
        }

        void BeginEdit()
        {
            if (_edit != null) return;
            _edit = new TextBox
            {
                BorderStyle = BorderStyle.None,
                BackColor = Theme.Bg,
                ForeColor = Theme.Text,
                Font = new Font("Segoe UI", 12f, FontStyle.Bold),
                TextAlign = HorizontalAlignment.Center,
                MaxLength = 3,
                Text = _mixed ? "" : _value.ToString(),
                Width = Width - 2 * Height - 4,
            };
            _edit.Left = Height + 2;
            _edit.Top = (Height - _edit.PreferredHeight) / 2 + 1;
            _edit.KeyPress += (s, e) => { if (!char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar)) e.Handled = true; };
            _edit.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; EndEdit(true); }
                else if (e.KeyCode == Keys.Escape) { e.SuppressKeyPress = true; EndEdit(false); }
            };
            _edit.LostFocus += delegate { EndEdit(true); };
            Controls.Add(_edit);
            _edit.Focus();
            _edit.SelectAll();
            Invalidate();
        }

        void EndEdit(bool commit)
        {
            if (_edit == null) return;
            var box = _edit;
            _edit = null;
            int v;
            if (commit && int.TryParse(box.Text, out v)) Value = v;
            Controls.Remove(box);
            box.Dispose();
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Panel);

            var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var path = Theme.Round(rect, Height / 2f - 1))
            {
                using (var b = new SolidBrush(Theme.Bg)) g.FillPath(b, path);
                g.SetClip(path);
                foreach (int part in new[] { -1, 1 })
                {
                    if (!Enabled) continue;
                    var pr = part < 0 ? new Rectangle(0, 0, Height, Height) : new Rectangle(Width - Height, 0, Height, Height);
                    Color c = _pressed == part ? Theme.Border : _hover == part ? Theme.Hover : Color.Empty;
                    if (c != Color.Empty) using (var b = new SolidBrush(c)) g.FillRectangle(b, pr);
                }
                g.ResetClip();
                using (var p = new Pen(_edit != null ? Theme.Accent : Theme.Border, 1f)) g.DrawPath(p, path);
            }

            Color glyph = Enabled ? Theme.Text : Theme.Faint;
            float cy = Height / 2f, s = 5f;
            using (var p = new Pen(glyph, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                float mx = Height / 2f + 2;
                g.DrawLine(p, mx - s, cy, mx + s, cy);
                float px = Width - Height / 2f - 2;
                g.DrawLine(p, px - s, cy, px + s, cy);
                g.DrawLine(p, px, cy - s, px, cy + s);
            }

            if (_edit == null)
            {
                string text = _mixed ? "mixed" : _value.ToString();
                using (var f = _mixed ? new Font("Segoe UI", 9.5f, FontStyle.Italic) : new Font("Segoe UI", 12f, FontStyle.Bold))
                using (var b = new SolidBrush(!Enabled ? Theme.Faint : _mixed ? Theme.Muted : Theme.Accent))
                {
                    var sz = g.MeasureString(text, f);
                    g.DrawString(text, f, b, (Width - sz.Width) / 2f, (Height - sz.Height) / 2f);
                }
            }
        }
    }

    /// <summary>Small on/off switch.</summary>
    public class ToggleSwitch : Control
    {
        bool _checked = true;
        public event EventHandler CheckedChanged;

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Size = new Size(36, 20);
            Cursor = Cursors.Hand;
        }

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value) return;
                _checked = value;
                Invalidate();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button == MouseButtons.Left) Checked = !Checked;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : Theme.Panel);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (var path = Theme.Round(r, r.Height / 2f))
            using (var b = new SolidBrush(_checked ? Theme.Accent : Theme.Track))
                g.FillPath(b, path);
            float d = Height - 6;
            float x = _checked ? Width - d - 3.5f : 3f;
            using (var b = new SolidBrush(Color.White)) g.FillEllipse(b, x, 3f, d, d);
        }
    }

    /// <summary>One stage inside an expanded rally card.</summary>
    public class StageRow : Panel
    {
        public readonly StageConfig Stage;
        public readonly ToggleSwitch Toggle;
        public readonly Label Title;
        public readonly GainStepper Stepper;
        public event EventHandler Changed;
        readonly ToolTip _tip = new ToolTip();

        public StageRow(StageConfig stage)
        {
            Stage = stage;
            Height = 48;
            BackColor = Theme.Panel;
            Margin = Padding.Empty;

            Toggle = new ToggleSwitch { Left = 52, Top = (Height - 20) / 2 };
            Toggle.Checked = stage.Enabled;
            _tip.SetToolTip(Toggle, "Off = leave the game's FFB gain unchanged on this stage");
            Title = new Label
            {
                Text = stage.Name,
                AutoSize = false,
                Left = 100,
                Top = 0,
                Height = Height,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 10.5f),
                AutoEllipsis = true,
                BackColor = Color.Transparent,
            };
            Stepper = new GainStepper { Top = (Height - 34) / 2 };
            Stepper.SetSilently(stage.Gain);

            Controls.Add(Toggle);
            Controls.Add(Title);
            Controls.Add(Stepper);

            Toggle.CheckedChanged += delegate { Stage.Enabled = Toggle.Checked; UpdateLook(); Raise(); };
            Stepper.ValueChanged += delegate { Stage.Gain = Stepper.Value; Raise(); };
            Title.Click += delegate { Toggle.Checked = !Toggle.Checked; };
            UpdateLook();
        }

        void Raise() { if (Changed != null) Changed(this, EventArgs.Empty); }

        public void UpdateLook()
        {
            Title.ForeColor = Stage.Enabled ? Theme.Text : Theme.Faint;
            Stepper.Enabled = Stage.Enabled;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Stepper == null) return; // called from the base constructor
            Stepper.Left = Width - Stepper.Width - 16;
            Title.Width = Math.Max(40, Stepper.Left - Title.Left - 10);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, 52, 0, Width - 16, 0);
        }
    }

    /// <summary>Rally card: header with chevron, name, summary and an "all stages" stepper; stages fold out.</summary>
    public class RallyCard : Panel
    {
        public readonly RallyConfig Rally;
        readonly Panel _header;
        readonly Label _name;
        readonly Label _summary;
        readonly GainStepper _stepper;
        readonly List<StageRow> _rows = new List<StageRow>();
        bool _expanded;
        bool _hoverHeader;
        public event EventHandler Changed;
        public event EventHandler ExpandedChanged;
        const int HeaderH = 64;

        public RallyCard(RallyConfig rally)
        {
            Rally = rally;
            BackColor = Theme.Panel;
            Margin = new Padding(0, 0, 0, 10);
            DoubleBuffered = true;

            _header = new DoubleBufferedPanel { Left = 0, Top = 0, Height = HeaderH, BackColor = Theme.Panel, Cursor = Cursors.Hand };
            _name = new Label
            {
                Text = rally.Name,
                AutoSize = true,
                Left = 46,
                Top = 12,
                Font = new Font("Segoe UI Semibold", 12.5f),
                ForeColor = Theme.Text,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
            };
            _summary = new Label
            {
                AutoSize = true,
                Left = 47,
                Top = 37,
                Font = new Font("Segoe UI", 9f),
                ForeColor = Theme.Muted,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
            };
            _stepper = new GainStepper { Top = (HeaderH - 34) / 2 };
            _stepper.SetSilently(rally.Gain);
            _header.Controls.Add(_name);
            _header.Controls.Add(_summary);
            _header.Controls.Add(_stepper);
            Controls.Add(_header);

            foreach (var s in rally.Stages)
            {
                var row = new StageRow(s) { Visible = false, Left = 0 };
                row.Changed += delegate { RefreshSummary(); Raise(); };
                _rows.Add(row);
                Controls.Add(row);
            }

            EventHandler toggle = delegate { Expanded = !Expanded; };
            _header.Click += toggle;
            _name.Click += toggle;
            _summary.Click += toggle;
            foreach (Control c in new Control[] { _header, _name, _summary })
            {
                c.MouseEnter += delegate { SetHover(true); };
                c.MouseLeave += delegate { SetHover(_header.ClientRectangle.Contains(_header.PointToClient(Cursor.Position)) && !_stepper.Bounds.Contains(_header.PointToClient(Cursor.Position))); };
            }
            _stepper.MouseEnter += delegate { SetHover(false); };
            _header.Paint += PaintHeader;

            _stepper.ValueChanged += delegate
            {
                Rally.Gain = _stepper.Value;
                foreach (var r in _rows) { r.Stage.Gain = Rally.Gain; r.Stepper.SetSilently(Rally.Gain); }
                RefreshSummary();
                Raise();
            };

            RefreshSummary();
            LayoutCard();
        }

        void Raise() { if (Changed != null) Changed(this, EventArgs.Empty); }

        void SetHover(bool on)
        {
            if (_hoverHeader == on) return;
            _hoverHeader = on;
            _header.BackColor = on ? Theme.PanelAlt : Theme.Panel;
            _header.Invalidate(true);
        }

        public bool Expanded
        {
            get { return _expanded; }
            set
            {
                if (_expanded == value) return;
                _expanded = value;
                SuspendLayout();
                foreach (var r in _rows) r.Visible = value;
                LayoutCard();
                ResumeLayout();
                _header.Invalidate();
                if (ExpandedChanged != null) ExpandedChanged(this, EventArgs.Empty);
            }
        }

        void RefreshSummary()
        {
            var stages = Rally.Stages;
            int on = stages.Count(s => s.Enabled);
            var gains = stages.Where(s => s.Enabled).Select(s => s.Gain).Distinct().ToList();
            string count = stages.Count == 1 ? "1 stage" : stages.Count + " stages";
            string detail;
            if (on == 0) detail = "all off";
            else if (gains.Count == 1) detail = on == stages.Count ? "all " + gains[0] : on + " on, " + gains[0];
            else detail = "custom per stage (" + gains.Min() + "–" + gains.Max() + ")";
            _summary.Text = count + "  ·  " + detail;

            bool mixed = gains.Count > 1;
            if (gains.Count == 1 && gains[0] != Rally.Gain) { Rally.Gain = gains[0]; _stepper.SetSilently(gains[0]); }
            _stepper.Mixed = mixed;
        }

        void LayoutCard()
        {
            // children are inset by 1px so the card border stays visible
            int y = 1 + HeaderH;
            if (_expanded)
            {
                foreach (var r in _rows) { r.Left = 1; r.Top = y; r.Width = Width - 2; y += r.Height; }
                y += 6;
            }
            Height = y + 1;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (_header == null) return; // called from the base constructor
            _header.Left = 1;
            _header.Top = 1;
            _header.Width = Width - 2;
            _stepper.Left = _header.Width - _stepper.Width - 16;
            foreach (var r in _rows) r.Width = Width - 2;
            Invalidate();
        }

        void PaintHeader(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // chevron: › when closed, ⌄ when open
            float cx = 24, cy = HeaderH / 2f;
            using (var p = new Pen(_hoverHeader ? Theme.Text : Theme.Muted, 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                if (_expanded) g.DrawLines(p, new[] { new PointF(cx - 6, cy - 3), new PointF(cx, cy + 3), new PointF(cx + 6, cy - 3) });
                else g.DrawLines(p, new[] { new PointF(cx - 3, cy - 6), new PointF(cx + 3, cy), new PointF(cx - 3, cy + 6) });
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var p = new Pen(Theme.Border)) e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
        }
    }

    public class DarkMenuColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground { get { return Theme.Panel; } }
        public override Color MenuBorder { get { return Theme.Border; } }
        public override Color MenuItemBorder { get { return Theme.Hover; } }
        public override Color MenuItemSelected { get { return Theme.Hover; } }
        public override Color MenuItemSelectedGradientBegin { get { return Theme.Hover; } }
        public override Color MenuItemSelectedGradientEnd { get { return Theme.Hover; } }
        public override Color ImageMarginGradientBegin { get { return Theme.Panel; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.Panel; } }
        public override Color ImageMarginGradientEnd { get { return Theme.Panel; } }
        public override Color SeparatorDark { get { return Theme.Border; } }
        public override Color SeparatorLight { get { return Theme.Panel; } }
        public override Color CheckBackground { get { return Theme.Hover; } }
        public override Color CheckSelectedBackground { get { return Theme.Border; } }
        public override Color CheckPressedBackground { get { return Theme.Border; } }
    }

    public class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }
    }
}

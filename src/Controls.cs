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
        readonly AppConfig _cfg;
        readonly LinkLabel _addCar;
        readonly List<Control> _carControls = new List<Control>();
        readonly List<GainStepper> _carSteppers = new List<GainStepper>();
        readonly List<string> _carLineIds = new List<string>();
        const int LineH = 48, CarH = 40, LogoW = 30, LogoH = 20;

        public StageRow(StageConfig stage, AppConfig cfg)
        {
            Stage = stage;
            _cfg = cfg;
            Height = LineH;
            BackColor = Theme.Panel;
            Margin = Padding.Empty;

            Toggle = new ToggleSwitch { Left = 88, Top = (Height - 20) / 2 };
            Toggle.Checked = stage.Enabled;
            _tip.SetToolTip(Toggle, "Off = leave the game's FFB gain unchanged on this stage");
            Title = new Label
            {
                Text = stage.Name,
                AutoSize = false,
                Left = 134,
                Top = 0,
                Height = Height,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 10.5f),
                AutoEllipsis = true,
                BackColor = Color.Transparent,
            };
            Stepper = new GainStepper { Top = (LineH - 34) / 2 };
            Stepper.SetSilently(stage.Gain);

            // "+ Car" (only with the "Different gain per car" option)
            _addCar = new LinkLabel
            {
                Text = "+ Car",
                AutoSize = true,
                Top = (LineH - 17) / 2,
                Font = new Font("Segoe UI Semibold", 9f),
                LinkColor = Theme.Muted,
                ActiveLinkColor = Theme.Accent,
                LinkBehavior = LinkBehavior.HoverUnderline,
                BackColor = Color.Transparent,
                Visible = false,
            };
            _tip.SetToolTip(_addCar, "Give a car its own FFB gain on this stage");
            _addCar.LinkClicked += delegate { ShowAddCarMenu(); };

            Controls.Add(Toggle);
            Controls.Add(Title);
            Controls.Add(_addCar);
            Controls.Add(Stepper);

            Toggle.CheckedChanged += delegate { Stage.Enabled = Toggle.Checked; UpdateLook(); Raise(); };
            Stepper.ValueChanged += delegate { Stage.Gain = Stepper.Value; Raise(); };
            Title.Click += delegate { Toggle.Checked = !Toggle.Checked; };
            BuildCarLines();
            UpdateLook();
        }

        void Raise() { if (Changed != null) Changed(this, EventArgs.Empty); }

        public void UpdateLook()
        {
            Title.ForeColor = Stage.Enabled ? Theme.Text : Theme.Faint;
            Stepper.Enabled = Stage.Enabled;
            foreach (var s in _carSteppers) s.Enabled = Stage.Enabled;
        }

        string CarName(string id)
        {
            var c = _cfg.Cars.FirstOrDefault(x => x.Id == id);
            return c != null ? c.Name : id;
        }

        /// <summary>One line per car that has its own value: "Subaru Impreza S3   ✕  − 160 +"</summary>
        void BuildCarLines()
        {
            SuspendLayout();
            foreach (var c in _carControls) { Controls.Remove(c); c.Dispose(); }
            _carControls.Clear();
            _carSteppers.Clear();
            _carLineIds.Clear();

            bool on = _cfg.PerCar;
            _addCar.Visible = on;
            _addCar.BringToFront();
            int y = LineH;
            if (on)
            {
                foreach (var kv in Stage.CarGains.OrderBy(k => CarName(k.Key), StringComparer.OrdinalIgnoreCase).ToList())
                {
                    string carId = kv.Key;
                    _carLineIds.Add(carId);
                    var name = new Label
                    {
                        Text = CarName(carId),
                        AutoSize = false,
                        Left = Title.Left + 18 + LogoW + 10,
                        Top = y,
                        Height = CarH,
                        TextAlign = ContentAlignment.MiddleLeft,
                        Font = new Font("Segoe UI", 9.5f),
                        ForeColor = Theme.Muted,
                        AutoEllipsis = true,
                        BackColor = Color.Transparent,
                    };
                    var remove = new Label
                    {
                        Text = "✕",
                        AutoSize = false,
                        Size = new Size(24, 24),
                        Top = y + (CarH - 24) / 2,
                        TextAlign = ContentAlignment.MiddleCenter,
                        Font = new Font("Segoe UI Symbol", 9f),
                        ForeColor = Theme.Faint,
                        BackColor = Color.Transparent,
                        Cursor = Cursors.Hand,
                        Tag = "remove",
                    };
                    _tip.SetToolTip(remove, "Remove — this car uses the stage value again");
                    remove.MouseEnter += delegate { remove.ForeColor = Theme.Bad; };
                    remove.MouseLeave += delegate { remove.ForeColor = Theme.Faint; };
                    remove.Click += delegate
                    {
                        Stage.CarGains.Remove(carId);
                        BuildCarLines();
                        Raise();
                    };
                    var stepper = new GainStepper { Size = new Size(120, 30), Top = y + (CarH - 30) / 2 };
                    stepper.SetSilently(kv.Value);
                    stepper.ValueChanged += delegate { Stage.CarGains[carId] = stepper.Value; Raise(); };

                    _carControls.Add(name);
                    _carControls.Add(remove);
                    _carControls.Add(stepper);
                    _carSteppers.Add(stepper);
                    Controls.Add(name);
                    Controls.Add(remove);
                    Controls.Add(stepper);
                    y += CarH;
                }
                if (Stage.CarGains.Count > 0) y += 6;
            }
            Height = y;
            LayoutRow();
            ResumeLayout();
            UpdateLook();
            Invalidate();
        }

        void ShowAddCarMenu()
        {
            var menu = new ContextMenuStrip { Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors()), ShowImageMargin = true, ImageScalingSize = new Size(LogoW, LogoH), BackColor = Theme.Panel, ForeColor = Theme.Text, Font = new Font("Segoe UI", 9.5f) };
            var available = _cfg.Cars.Where(c => !Stage.CarGains.ContainsKey(c.Id)).ToList();
            var current = available.FirstOrDefault(c => c.Id == AppState.CurrentCarId);
            if (current != null)
            {
                available.Remove(current);
                available.Insert(0, current);
            }
            if (available.Count == 0)
                menu.Items.Add(new ToolStripMenuItem("All cars already added") { Enabled = false });
            foreach (var car in available)
            {
                var c = car;
                var item = new ToolStripMenuItem(c == current ? c.Name + "   (current car)" : c.Name) { ForeColor = c == current ? Theme.Accent : Theme.Text, Image = BrandMark.Bitmap(c.Name, LogoW, LogoH), ImageScaling = ToolStripItemImageScaling.None };
                item.Click += delegate
                {
                    Stage.CarGains[c.Id] = Stage.Gain;
                    BuildCarLines();
                    Raise();
                };
                menu.Items.Add(item);
                if (c == current && available.Count > 1) menu.Items.Add(new ToolStripSeparator());
            }
            menu.Closed += delegate { BeginInvoke((Action)menu.Dispose); };
            menu.Show(_addCar, new Point(0, _addCar.Height + 2));
        }

        void LayoutRow()
        {
            if (Stepper == null) return; // called from the base constructor
            Stepper.Left = Width - Stepper.Width - 16;
            _addCar.Left = Stepper.Left - _addCar.Width - 14;
            // Use the setting, not _addCar.Visible: while the rally is folded the row is hidden and every
            // child reports Visible = false, which made the name label cover the "+ Car" link.
            int titleRight = _cfg.PerCar ? _addCar.Left - 8 : Stepper.Left - 10;
            Title.Width = Math.Max(40, titleRight - Title.Left);
            foreach (var c in _carControls)
            {
                var gs = c as GainStepper;
                if (gs != null) gs.Left = Stepper.Right - gs.Width;
                else if ((c.Tag as string) == "remove") c.Left = Stepper.Left - 6 - 24 - 4 + (Stepper.Width - 120);
                else c.Width = Math.Max(40, Stepper.Left - 40 - c.Left);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutRow();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var p = new Pen(Theme.Border)) e.Graphics.DrawLine(p, Toggle.Left, 0, Width - 16, 0);
            using (var b = new SolidBrush(Strip)) e.Graphics.FillRectangle(b, 0, 0, 4, Height);

            // car make marks in front of each car line
            for (int i = 0; i < _carLineIds.Count; i++)
            {
                float y = LineH + i * CarH + (CarH - LogoH) / 2f;
                BrandMark.Draw(e.Graphics, new RectangleF(Title.Left + 18, y, LogoW, LogoH), CarName(_carLineIds[i]));
            }
        }

        /// <summary>Colour of the location strip on the left edge.</summary>
        public Color Strip = Theme.Border;
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
        readonly RallyStyle _style;
        readonly Color _strip;
        bool _expanded;
        bool _hoverHeader;
        public event EventHandler Changed;
        public event EventHandler ExpandedChanged;
        const int HeaderH = 64;
        const int TextLeft = 88;
        static readonly RectangleF FlagRect = new RectangleF(42, (HeaderH - 24) / 2f, 36, 24);

        readonly AppConfig _cfg;

        public RallyCard(RallyConfig rally, AppConfig cfg)
        {
            Rally = rally;
            _cfg = cfg;
            _style = RallyStyle.For(rally.Id, rally.Name);
            _strip = RallyStyle.SurfaceColor(_style.Surface);
            BackColor = Theme.Panel;
            Margin = new Padding(0, 0, 0, 10);
            DoubleBuffered = true;

            _header = new DoubleBufferedPanel { Left = 0, Top = 0, Height = HeaderH, BackColor = Theme.Panel, Cursor = Cursors.Hand };
            _name = new Label
            {
                Text = rally.Name,
                AutoSize = true,
                Left = TextLeft - 2,
                Top = 11,
                Font = new Font("Segoe UI Semibold", 12.5f),
                ForeColor = Theme.Text,
                BackColor = Color.Transparent,
                Cursor = Cursors.Hand,
            };
            _summary = new Label
            {
                AutoSize = true,
                Left = TextLeft - 1,
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
                var row = new StageRow(s, cfg) { Visible = false, Left = 0, Strip = _strip };
                row.Changed += delegate { RefreshSummary(); Raise(); };
                row.SizeChanged += delegate { if (_expanded) LayoutCard(); };
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

        const float BadgeW = 88, BadgeH = 26;

        RectangleF BadgeRect()
        {
            return new RectangleF(_stepper.Left - 18 - BadgeW, (HeaderH - BadgeH) / 2f, BadgeW, BadgeH);
        }

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
            int carOverrides = _cfg.PerCar ? stages.Sum(s => s.CarGains.Count) : 0;
            string cars = carOverrides == 0 ? "" : "  ·  " + carOverrides + (carOverrides == 1 ? " car setting" : " car settings");
            _summary.Text = count + "  ·  " + detail + cars;

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
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            // location strip (surface colour), flag
            using (var b = new SolidBrush(_strip)) g.FillRectangle(b, 0, 0, 4, HeaderH);
            _style.DrawFlag(g, FlagRect);

            // surface badge: same size on every card, in a fixed column left of the stepper
            string surface = RallyStyle.SurfaceName(_style.Surface);
            if (surface != null)
            {
                using (var f = new Font("Segoe UI Semibold", 8.5f))
                {
                    var c = RallyStyle.SurfaceColor(_style.Surface);
                    var pill = BadgeRect();
                    using (var path = Theme.Round(pill, pill.Height / 2f))
                    using (var b = new SolidBrush(Color.FromArgb(38, c)))
                    using (var p = new Pen(Color.FromArgb(110, c)))
                    {
                        g.FillPath(b, path);
                        g.DrawPath(p, path);
                    }
                    // icon + label centred as one group
                    var ts = g.MeasureString(surface, f);
                    const float icon = 14, gap = 4;
                    float groupW = icon + gap + ts.Width;
                    float gx = pill.X + (pill.Width - groupW) / 2f;
                    RallyStyle.DrawSurfaceIcon(g, new RectangleF(gx, pill.Y + (pill.Height - icon) / 2f, icon, icon), _style.Surface, c);
                    using (var b = new SolidBrush(c))
                        g.DrawString(surface, f, b, gx + icon + gap, pill.Y + (pill.Height - ts.Height) / 2f + 0.5f);
                }
            }

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
            using (var b = new SolidBrush(_strip)) e.Graphics.FillRectangle(b, 1, 1, 4, Height - 2);
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

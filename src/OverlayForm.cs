using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AcrFfbGain
{
    /// <summary>
    /// Click-through, always-on-top popup shown over the game (works in borderless / windowed mode).
    /// </summary>
    public class OverlayForm : Form
    {
        readonly Timer _hideTimer = new Timer();
        readonly Timer _fadeTimer = new Timer();
        string _value = "";
        string _title = "";
        string _subtitle = "";
        bool _ok = true;

        static readonly Color Bg = Color.FromArgb(20, 22, 28);
        static readonly Color Accent = Color.FromArgb(255, 122, 26);
        static readonly Color Warn = Color.FromArgb(232, 72, 72);

        public OverlayForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            BackColor = Bg;
            Size = new Size(460, 108);
            DoubleBuffered = true;

            _hideTimer.Tick += delegate { _hideTimer.Stop(); _fadeTimer.Start(); };
            _fadeTimer.Interval = 30;
            _fadeTimer.Tick += delegate
            {
                Opacity -= 0.06;
                if (Opacity <= 0.05) { _fadeTimer.Stop(); Hide(); }
            };
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= 0x00000080   // WS_EX_TOOLWINDOW
                            | 0x08000000   // WS_EX_NOACTIVATE
                            | 0x00000020   // WS_EX_TRANSPARENT (click-through)
                            | 0x00080000   // WS_EX_LAYERED
                            | 0x00000008;  // WS_EX_TOPMOST
                return cp;
            }
        }

        public void ShowMessage(string value, string title, string subtitle, bool ok, int seconds)
        {
            _value = value;
            _title = title;
            _subtitle = subtitle;
            _ok = ok;

            using (var g = CreateGraphics())
            using (var fValue = new Font("Segoe UI", 30f, FontStyle.Bold))
            using (var fTitle = new Font("Segoe UI Semibold", 13f))
            using (var fSub = new Font("Segoe UI", 10.5f))
            {
                int textW = (int)Math.Max(g.MeasureString(_title, fTitle).Width, g.MeasureString(_subtitle, fSub).Width);
                int valueW = (int)g.MeasureString(_value, fValue).Width;
                Width = Math.Max(360, Math.Min(900, 28 + valueW + 24 + textW + 30));
            }

            var scr = Screen.PrimaryScreen.Bounds;
            Location = new Point(scr.Left + (scr.Width - Width) / 2, scr.Top + 48);
            using (var path = RoundRect(new Rectangle(0, 0, Width, Height), 16))
                Region = new Region(path);

            _fadeTimer.Stop();
            _hideTimer.Stop();
            Opacity = 0.96;
            if (!Visible) Show();
            SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            Invalidate();
            _hideTimer.Interval = Math.Max(1, seconds) * 1000;
            _hideTimer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            var accent = _ok ? Accent : Warn;

            using (var b = new SolidBrush(Bg)) g.FillRectangle(b, ClientRectangle);
            using (var b = new SolidBrush(accent)) g.FillRectangle(b, 0, 0, 7, Height);

            using (var fValue = new Font("Segoe UI", 30f, FontStyle.Bold))
            using (var fTitle = new Font("Segoe UI Semibold", 13f))
            using (var fSub = new Font("Segoe UI", 10.5f))
            using (var bValue = new SolidBrush(accent))
            using (var bTitle = new SolidBrush(Color.White))
            using (var bSub = new SolidBrush(Color.FromArgb(170, 176, 190)))
            {
                var vs = g.MeasureString(_value, fValue);
                g.DrawString(_value, fValue, bValue, 24, (Height - vs.Height) / 2);
                float x = 24 + vs.Width + 14;
                g.DrawString(_title, fTitle, bTitle, x, 26);
                g.DrawString(_subtitle, fSub, bSub, x, 56);
            }
        }

        static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            var p = new GraphicsPath();
            int d = radius * 2;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOACTIVATE = 0x0010, SWP_SHOWWINDOW = 0x0040;

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    }
}

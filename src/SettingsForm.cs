using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AcrFfbGain
{
    /// <summary>
    /// Settings window: a two-column grid of cards, each with a title, a switch (or button) and a short
    /// explanation. Changes take effect immediately; the owner supplies what each option does.
    /// </summary>
    public class SettingsForm : Form
    {
        const int CardW = 320, Gap = 12, Margin_ = 22, Pad = 16, SwitchW = 36;
        readonly Panel _body;
        readonly List<Panel> _row = new List<Panel>();
        int _y = 4;

        public SettingsForm()
        {
            Text = "Settings — " + AppInfo.Name;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            Icon = AppIcon.Create();
            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };

            var header = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = Theme.Bg };
            header.Controls.Add(new Label { Text = "Settings", AutoSize = true, Left = Margin_ - 2, Top = 12, Font = new Font("Segoe UI Semibold", 16f), ForeColor = Theme.Text });

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 56, BackColor = Theme.Bg };
            var done = new FlatButton("Done") { AutoSize = false, Size = new Size(96, 34), Top = 10, BackColor = Theme.Accent, ForeColor = Color.White, Font = new Font("Segoe UI Semibold", 10f) };
            done.FlatAppearance.BorderColor = Theme.Accent;
            done.FlatAppearance.MouseOverBackColor = Color.FromArgb(255, 145, 64);
            done.Click += delegate { Close(); };
            footer.Controls.Add(done);
            footer.Controls.Add(new Label { Text = "Changes are saved straight away.", AutoSize = true, Left = Margin_, Top = 19, ForeColor = Theme.Faint, Font = new Font("Segoe UI", 9f) });
            footer.Resize += delegate { done.Left = footer.Width - done.Width - Margin_; };
            AcceptButton = done;

            _body = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Bg };

            Controls.Add(_body);
            Controls.Add(footer);
            Controls.Add(header);

            ClientSize = new Size(Margin_ + 2 * CardW + Gap + Margin_, 600);
        }

        /// <summary>Uppercase heading; the cards after it start a new row.</summary>
        public void Section(string title)
        {
            FlushRow();
            if (_y > 4) _y += 10;
            _body.Controls.Add(new Label
            {
                Text = title.ToUpperInvariant(), AutoSize = true, Left = Margin_ + 2, Top = _y,
                Font = new Font("Segoe UI Semibold", 8.5f), ForeColor = Theme.Accent,
            });
            _y += 22;
        }

        /// <summary>An on/off option. Clicking anywhere on the card flips it.</summary>
        public void Toggle(string title, string description, bool value, Action<bool> changed)
        {
            var card = NewCard();
            var sw = new ToggleSwitch { Checked = value, Left = CardW - Pad - SwitchW, Top = Pad + 2 };
            var t = TitleLabel(title, sw.Left - 8 - Pad);
            var d = DescLabel(description, CardW - 2 * Pad, t.Bottom + 4);
            card.Controls.Add(t);
            card.Controls.Add(d);
            card.Controls.Add(sw);
            sw.CheckedChanged += delegate { changed(sw.Checked); };
            EventHandler enter = delegate { card.BackColor = Theme.PanelAlt; sw.Invalidate(); };
            EventHandler leave = delegate
            {
                if (card.ClientRectangle.Contains(card.PointToClient(Cursor.Position))) return;
                card.BackColor = Theme.Panel;
                sw.Invalidate();
            };
            foreach (Control c in new Control[] { card, t, d })
            {
                c.Cursor = Cursors.Hand;
                c.Click += delegate { sw.Checked = !sw.Checked; };
                c.MouseEnter += enter;
                c.MouseLeave += leave;
            }
            sw.MouseEnter += enter;
            sw.MouseLeave += leave;
            AddCard(card, d.Bottom);
        }

        /// <summary>
        /// An option with a button, e.g. a folder: title + button, a short explanation and the current value.
        /// Returns an action that refreshes the shown value.
        /// </summary>
        public Action ButtonRow(string title, string description, Func<string> value, string buttonText, Action click)
        {
            var card = NewCard();
            var btn = new FlatButton(buttonText) { AutoSize = false, Size = new Size(92, 30), Top = Pad - 4, BackColor = Theme.PanelAlt };
            btn.Left = CardW - Pad - btn.Width;
            var t = TitleLabel(title, btn.Left - 8 - Pad);
            var d = DescLabel(description, CardW - 2 * Pad, btn.Bottom + 4);
            var v = new Label
            {
                AutoSize = false, AutoEllipsis = true, Left = Pad, Top = d.Bottom + 6, Width = CardW - 2 * Pad, Height = 18,
                Font = new Font("Consolas", 8.5f), ForeColor = Theme.Text, BackColor = Color.Transparent,
            };
            var tip = new ToolTip();
            Action refresh = delegate
            {
                string s = value();
                v.Text = string.IsNullOrEmpty(s) ? "(not found yet)" : s;
                v.ForeColor = string.IsNullOrEmpty(s) ? Theme.Muted : Theme.Text;
                tip.SetToolTip(v, v.Text);
            };
            refresh();
            btn.Click += delegate { click(); refresh(); };
            card.Controls.Add(t);
            card.Controls.Add(d);
            card.Controls.Add(v);
            card.Controls.Add(btn);
            AddCard(card, v.Bottom);
            return refresh;
        }

        Panel NewCard()
        {
            var card = new DoubleBufferedPanel { Width = CardW, BackColor = Theme.Panel };
            card.Paint += (s, e) => { using (var p = new Pen(Theme.Border)) e.Graphics.DrawRectangle(p, 0, 0, card.Width - 1, card.Height - 1); };
            return card;
        }

        /// <summary>Places the card in the next free grid cell (two per row).</summary>
        void AddCard(Panel card, int contentBottom)
        {
            card.Height = contentBottom + Pad - 2;
            card.Left = Margin_ + _row.Count * (CardW + Gap);
            card.Top = _y;
            _body.Controls.Add(card);
            _row.Add(card);
            if (_row.Count == 2) FlushRow();
        }

        /// <summary>Gives the cards of the current row the same height and moves on to the next row.</summary>
        void FlushRow()
        {
            if (_row.Count == 0) return;
            int h = 0;
            foreach (var c in _row) h = Math.Max(h, c.Height);
            foreach (var c in _row) c.Height = h;
            _y += h + Gap;
            _row.Clear();
        }

        static Label TitleLabel(string text, int width)
        {
            return new Label
            {
                Text = text, AutoSize = false, AutoEllipsis = true, Left = Pad, Top = Pad - 1, Width = width, Height = 24,
                Font = new Font("Segoe UI Semibold", 10.5f), ForeColor = Theme.Text, BackColor = Color.Transparent,
            };
        }

        static Label DescLabel(string text, int width, int top)
        {
            var l = new Label
            {
                Text = text, AutoSize = false, Left = Pad, Top = top, Width = width,
                Font = new Font("Segoe UI", 9f), ForeColor = Theme.Muted, BackColor = Color.Transparent,
            };
            l.Height = l.GetPreferredSize(new Size(width, 0)).Height + 2;
            return l;
        }

        protected override void OnLoad(EventArgs e)
        {
            FlushRow();
            // fit the window to its content, up to most of the screen height (scrolls beyond that)
            int chrome = Height - _body.ClientSize.Height;
            int max = Screen.FromControl(Owner ?? this).WorkingArea.Height - 60;
            int want = chrome + _y + 4;
            if (want > max) Width += SystemInformation.VerticalScrollBarWidth;
            Height = Math.Min(max, want);
            base.OnLoad(e);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // dark title bar (Windows 10 2004+ / 11)
            try { int on = 1; DwmSetWindowAttribute(Handle, 20, ref on, 4); } catch { }
        }

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    }
}

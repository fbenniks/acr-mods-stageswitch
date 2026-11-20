using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace AcrFfbGain
{
    public class MainForm : Form
    {
        readonly AppConfig _cfg;
        readonly OverlayForm _overlay = new OverlayForm();
        readonly GainWriter _writer = new GainWriter();
        readonly NotifyIcon _tray = new NotifyIcon();
        readonly System.Windows.Forms.Timer _poll = new System.Windows.Forms.Timer();
        readonly System.Windows.Forms.Timer _saveTimer = new System.Windows.Forms.Timer();

        Label _lblGame, _lblStage, _lblApplied, _lblFooter;
        FlowLayoutPanel _list;

        readonly HashSet<string> _expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // rallies opened this session
        string _lastStageKey;
        DateTime _menuSince = DateTime.MinValue;
        bool _activePopupShown;
        const int MenuPopupDelaySeconds = 6;
        int _lastPid;
        bool _exiting;
        bool _scanning;

        // apply state
        bool _applying;
        int _queuedGain = -1;
        string _queuedLabel;
        RallyStyle _queuedStyle;
        bool _queuedPopup;
        int _appliedGain = -1;
        volatile int _applyGen;
        readonly System.Windows.Forms.Timer _reapplyTimer = new System.Windows.Forms.Timer();

        public MainForm(AppConfig cfg)
        {
            _cfg = cfg;
            Text = "AC Rally FFB Gain";
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9.5f);
            ClientSize = new Size(820, 720);
            MinimumSize = new Size(640, 420);
            StartPosition = FormStartPosition.CenterScreen;
            Icon = AppIcon.Create();

            BuildUi();
            BuildTray();
            RebuildList();

            _saveTimer.Interval = 800;
            _saveTimer.Tick += delegate { _saveTimer.Stop(); _cfg.Save(); };

            // When a value for the stage you're on is changed in the app, apply it straight away.
            _reapplyTimer.Interval = 700;
            _reapplyTimer.Tick += delegate { _reapplyTimer.Stop(); ReapplyCurrent(); };

            _poll.Interval = 500;
            _poll.Tick += delegate { Poll(); };
            _poll.Start();

            if (_cfg.StartMinimized)
            {
                WindowState = FormWindowState.Minimized;
                ShowInTaskbar = false;
            }

            Shown += delegate
            {
                if (_cfg.StartMinimized) Hide();
                StartScan(false);
            };
        }

        // ---------------------------------------------------------------- UI

        void BuildUi()
        {
            // Header: title + what's happening on the left, game status + options on the right.
            var header = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Theme.Bg };
            var title = new Label { Text = "FFB Gain", AutoSize = true, Font = new Font("Segoe UI Semibold", 18f), ForeColor = Theme.Text, Left = 18, Top = 14 };
            _lblStage = new Label { AutoSize = true, Left = 21, Top = 56, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 9.5f) };
            _lblGame = new Label { AutoSize = true, Top = 24, Font = new Font("Segoe UI Semibold", 9.5f) };
            _lblApplied = new Label { AutoSize = true, Top = 56, ForeColor = Theme.Muted, Font = new Font("Segoe UI", 9.5f) };
            var btnMenu = new FlatButton("⚙") { AutoSize = false, Size = new Size(38, 34), Top = 17, Font = new Font("Segoe UI Symbol", 13f), Padding = Padding.Empty, BackColor = Theme.Bg };
            new ToolTip().SetToolTip(btnMenu, "Options");

            var menu = new ContextMenuStrip { Renderer = new ToolStripProfessionalRenderer(new DarkMenuColors()), ShowImageMargin = false, ShowCheckMargin = true, BackColor = Theme.Panel, ForeColor = Theme.Text, Font = new Font("Segoe UI", 9.5f) };
            var miOverlay = new ToolStripMenuItem("Show popup in game") { CheckOnClick = true, Checked = _cfg.ShowOverlay, ForeColor = Theme.Text };
            miOverlay.CheckedChanged += delegate { _cfg.ShowOverlay = miOverlay.Checked; QueueSave(); };
            var miMinimized = new ToolStripMenuItem("Start minimized to tray") { CheckOnClick = true, Checked = _cfg.StartMinimized, ForeColor = Theme.Text };
            miMinimized.CheckedChanged += delegate { _cfg.StartMinimized = miMinimized.Checked; QueueSave(); };
            menu.Items.Add(miOverlay);
            menu.Items.Add(miMinimized);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(new ToolStripMenuItem("Game folder…", null, delegate { ChooseGameFolder(); }) { ForeColor = Theme.Text });
            menu.Items.Add(new ToolStripMenuItem("Open settings folder", null, delegate { System.Diagnostics.Process.Start("explorer.exe", "\"" + AppConfig.Folder + "\""); }) { ForeColor = Theme.Text });
            btnMenu.Click += delegate { menu.Show(btnMenu, new Point(btnMenu.Width - menu.Width, btnMenu.Height + 2)); };

            header.Controls.Add(title);
            header.Controls.Add(_lblStage);
            header.Controls.Add(_lblGame);
            header.Controls.Add(_lblApplied);
            header.Controls.Add(btnMenu);
            EventHandler place = delegate
            {
                btnMenu.Left = header.Width - btnMenu.Width - 16;
                _lblGame.Left = btnMenu.Left - _lblGame.Width - 12;
                _lblApplied.Left = header.Width - _lblApplied.Width - 18;
            };
            header.Resize += place;
            _lblGame.SizeChanged += place;
            _lblApplied.SizeChanged += place;

            _lblFooter = new Label { Dock = DockStyle.Bottom, Height = 30, ForeColor = Theme.Faint, Padding = new Padding(20, 0, 0, 0), TextAlign = ContentAlignment.MiddleLeft, BackColor = Theme.Bg, Font = new Font("Segoe UI", 8.5f) };

            _list = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Theme.Bg,
                Padding = new Padding(16, 4, 16, 8),
            };
            _list.Resize += delegate { FitRows(); };

            Controls.Add(_list);
            Controls.Add(_lblFooter);
            Controls.Add(header);
        }

        void FitRows()
        {
            int w = _list.ClientSize.Width - _list.Padding.Horizontal - 2;
            if (_list.VerticalScroll.Visible) w -= 2;
            _list.SuspendLayout();
            foreach (Control c in _list.Controls) c.Width = Math.Max(300, w);
            _list.ResumeLayout();
        }

        void RebuildList()
        {
            _list.SuspendLayout();
            foreach (Control c in _list.Controls) c.Dispose();
            _list.Controls.Clear();

            if (_cfg.Rallies.Count == 0)
            {
                _list.Controls.Add(new Label
                {
                    Text = _scanning ? "Scanning game files for stages…" : "No stages found yet. Use “Game folder…” if AC Rally is installed somewhere unusual.",
                    AutoSize = false, Height = 60, Width = 600, ForeColor = Theme.Muted, TextAlign = ContentAlignment.MiddleLeft,
                });
            }

            foreach (var rally in _cfg.Rallies)
            {
                var card = new RallyCard(rally);
                card.Expanded = _expanded.Contains(rally.Id);
                card.Changed += delegate { QueueSave(); };
                card.ExpandedChanged += delegate
                {
                    if (card.Expanded) _expanded.Add(card.Rally.Id); else _expanded.Remove(card.Rally.Id);
                };
                _list.Controls.Add(card);
            }
            _list.ResumeLayout();
            FitRows();

            int stages = _cfg.Rallies.Sum(x => x.Stages.Count);
            _lblFooter.Text = string.Format("{0} rallies  ·  {1} stages  ·  click a rally to show its stages", _cfg.Rallies.Count, stages);
        }

        void QueueSave()
        {
            _saveTimer.Stop();
            _saveTimer.Start();
            if (_lastStageKey != null) { _reapplyTimer.Stop(); _reapplyTimer.Start(); }
        }

        void ChooseGameFolder()
        {
            using (var dlg = new FolderBrowserDialog { Description = "Select the \"Assetto Corsa Rally\" install folder", SelectedPath = _cfg.GameDir ?? "" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                if (!StageScanner.IsGameDir(dlg.SelectedPath))
                {
                    MessageBox.Show(this, "That folder does not look like the AC Rally install (acr\\Content\\Paks is missing).", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                _cfg.GameDir = dlg.SelectedPath;
                QueueSave();
                StartScan(true);
            }
        }

        void StartScan(bool userAsked)
        {
            if (_scanning) return;
            _scanning = true;
            _lblFooter.Text = "Scanning game files for stages…";
            string dir = StageScanner.IsGameDir(_cfg.GameDir) ? _cfg.GameDir : StageScanner.FindGameDir();
            var t = new Thread(() =>
            {
                List<ScannedStage> found = null;
                string err = null;
                try
                {
                    if (dir == null) err = "AC Rally install folder not found. Use “Game folder…”.";
                    else found = StageScanner.Scan(dir);
                }
                catch (Exception ex) { err = ex.Message; }
                try
                {
                    BeginInvoke((Action)(() =>
                    {
                        _scanning = false;
                        if (err != null)
                        {
                            _lblFooter.Text = err;
                            Log.Write("Scan failed: " + err);
                            if (userAsked) MessageBox.Show(this, err, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            if (_cfg.Rallies.Count == 0) RebuildList();
                            return;
                        }
                        _cfg.GameDir = dir;
                        bool first = !_cfg.Rallies.Any(x => x.Stages.Any(s => !s.Detected));
                        int added = StageScanner.Merge(_cfg, found);
                        if (first) ApplyFirstRunDefaults();
                        _cfg.Save();
                        RebuildList();
                        Log.Write("Scan: " + found.Count + " stages, " + added + " new");
                        if (added > 0 && !first)
                            _tray.ShowBalloonTip(5000, "New stages found", added + " new stage(s) added. Set their FFB gain in the app.", ToolTipIcon.Info);
                        else if (userAsked)
                            _lblFooter.Text = "Scan complete: " + found.Count + " stages, " + added + " new.   " + _lblFooter.Text;
                    }));
                }
                catch (InvalidOperationException) { }
            });
            t.IsBackground = true;
            t.Start();
        }

        /// <summary>Starting values the user told us about: gravel Wales 200, tarmac Monte Carlo 160.</summary>
        void ApplyFirstRunDefaults()
        {
            var presets = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { { "Wales", 200 }, { "MonteCarlo", 160 } };
            foreach (var r in _cfg.Rallies)
            {
                int g;
                if (!presets.TryGetValue(r.Id, out g)) continue;
                r.Gain = g;
                foreach (var s in r.Stages) s.Gain = g;
            }
        }

        // ---------------------------------------------------------------- tray

        void BuildTray()
        {
            _tray.Icon = Icon;
            _tray.Text = "AC Rally FFB Gain";
            _tray.Visible = true;
            var menu = new ContextMenuStrip();
            menu.Items.Add("Open", null, delegate { ShowFromTray(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, delegate { _exiting = true; Close(); });
            _tray.ContextMenuStrip = menu;
            _tray.DoubleClick += delegate { ShowFromTray(); };
        }

        void ShowFromTray()
        {
            ShowInTaskbar = true;
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Minimized && Visible && _tray.Visible)
            {
                Hide();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_exiting && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                _tray.ShowBalloonTip(3000, "Still running", "AC Rally FFB Gain keeps watching the game from the tray. Right-click the icon to exit.", ToolTipIcon.Info);
                return;
            }
            _poll.Stop();
            _cfg.Save();
            _tray.Visible = false;
            base.OnFormClosing(e);
        }

        // ---------------------------------------------------------------- detection

        void Poll()
        {
            TelemetrySnapshot snap;
            try { snap = Telemetry.Read(); }
            catch (Exception ex) { Log.Write("Telemetry: " + ex.Message); return; }

            if (!snap.GameRunning)
            {
                SetGame("●  Game not running", Theme.Muted);
                _lblStage.Text = "Start AC Rally — the gain is applied automatically when a stage loads.";
                if (_lastPid != 0) { _writer.Reset(); _lastPid = 0; }
                _lastStageKey = null;
                _appliedGain = -1;
                return;
            }

            if (snap.ProcessId != _lastPid)
            {
                _lastPid = snap.ProcessId;
                _writer.Reset();
                _lastStageKey = null;
                _menuSince = DateTime.MinValue;
                _activePopupShown = false;
                StartScan(false); // pick up stages from a game update
            }

            if (!snap.SharedMemory)
            {
                SetGame("●  Game running", Theme.Good);
                _lblStage.Text = "Waiting for the game's telemetry…";
                return;
            }

            // AC Rally fills the track name when a stage is loaded and clears it in the menus.
            bool inStage = snap.Track.Length > 0;
            SetGame("●  Game running", Theme.Good);
            _lblStage.Text = inStage ? "On stage: " + snap.Track + (snap.Car.Length > 0 ? "   ·   " + snap.Car : "") : "In menus";

            if (!inStage)
            {
                _lastStageKey = null;
                ShowActivePopupOnce();
                return;
            }
            _menuSince = DateTime.MinValue;
            _activePopupShown = true; // went straight into a stage: the stage popup is enough

            if (_scanning) return; // wait until the stage list is up to date

            if (snap.Track != _lastStageKey)
            {
                _lastStageKey = snap.Track;
                OnStageLoaded(snap.Track, true);
            }
        }

        /// <summary>
        /// Once per game launch: when the game sits in its menus (telemetry up, no stage) for a few
        /// seconds, show that the switcher is running.
        /// </summary>
        void ShowActivePopupOnce()
        {
            if (_activePopupShown) return;
            if (_menuSince == DateTime.MinValue) { _menuSince = DateTime.Now; return; }
            if ((DateTime.Now - _menuSince).TotalSeconds < MenuPopupDelaySeconds) return;
            _activePopupShown = true;
            if (!_cfg.ShowOverlay) return;

            int stages = _cfg.Rallies.Sum(r => r.Stages.Count(s => s.Enabled));
            _overlay.ShowMessage("ON", "FFB gain switcher active",
                stages + " stages set up  ·  gain changes when a stage loads", true, _cfg.OverlaySeconds);
            Log.Write("Game in menu: showed 'switcher active' popup");
        }

        void ReapplyCurrent()
        {
            if (_lastStageKey == null) return;
            OnStageLoaded(_lastStageKey, false);
        }

        void SetGame(string text, Color c)
        {
            if (_lblGame.Text != text) _lblGame.Text = text;
            _lblGame.ForeColor = c;
        }

        void OnStageLoaded(string track, bool isNewLoad)
        {
            RallyConfig rally;
            StageConfig stage = MatchStage(track, out rally);
            if (isNewLoad)
                Log.Write("Stage loaded: '" + track + "' -> " + (stage != null ? rally.Id + "/" + stage.Id : rally != null ? rally.Id : "no match"));

            int gain;
            string label;
            if (stage != null)
            {
                label = rally.Name + "  ·  " + stage.Name;
                if (!stage.Enabled)
                {
                    if (isNewLoad) _lblApplied.Text = "Not changed (" + stage.Name + " is unticked)";
                    return;
                }
                gain = stage.Gain;
            }
            else if (rally != null)
            {
                // Known rally, but the exact stage couldn't be determined: use the rally's value.
                gain = rally.Gain;
                label = rally.Name;
            }
            else
            {
                if (!isNewLoad) return;
                AddDetectedStage(track);
                if (_cfg.ShowOverlay)
                    _overlay.ShowMessage("?", "New stage: " + track, "FFB not changed — set a value for it in the app", false, _cfg.OverlaySeconds);
                _lblApplied.Text = "New stage detected: " + track;
                return;
            }

            if (!isNewLoad && gain == _appliedGain) return;
            _queuedStyle = RallyStyle.For(rally.Id, rally.Name);
            QueueApply(gain, label, isNewLoad);
        }

        /// <summary>Writes the gain on a background thread (finding it in memory can take a few seconds).</summary>
        void QueueApply(int gain, string label, bool fullRescan)
        {
            _queuedGain = gain;
            _queuedLabel = label;
            _queuedPopup = true;
            if (_applying) return;
            RunApply(fullRescan);
        }

        void RunApply(bool fullRescan)
        {
            int pid = _lastPid;
            int gain = _queuedGain;
            string label = _queuedLabel;
            RallyStyle style = _queuedStyle;
            bool popup = _queuedPopup;
            int myGen = ++_applyGen;
            _queuedGain = -1;
            _applying = true;
            _lblApplied.Text = "Setting FFB gain to " + gain + "…";

            var t = new Thread(() =>
            {
                string err;
                // Uses the addresses found earlier when they are still valid (instant), otherwise scans.
                // Retry for a while in case the settings aren't in place yet while the stage loads.
                bool ok = false;
                DateTime until = DateTime.Now.AddSeconds(30);
                bool rescan = false;
                while (true)
                {
                    ok = _writer.Apply(pid, gain, rescan, out err);
                    if (ok || DateTime.Now > until || Telemetry.FindGame() == null) break;
                    rescan = true;
                    Thread.Sleep(2000);
                }
                try
                {
                    BeginInvoke((Action)(() =>
                    {
                        _applying = false;
                        if (ok)
                        {
                            _appliedGain = gain;
                            Log.Write("Applied FFB gain " + gain + " for " + label);
                            _lblApplied.Text = "Last change: " + gain + "  (" + label + ")";
                            if (_cfg.ShowOverlay && popup)
                                _overlay.ShowMessage(gain.ToString(), "FFB gain set to " + gain, label, true, _cfg.OverlaySeconds, style);
                        }
                        else
                        {
                            Log.Write("Failed to apply FFB gain " + gain + ": " + err);
                            _lblApplied.Text = "Could not change FFB: " + err;
                            if (_cfg.ShowOverlay && popup)
                                _overlay.ShowMessage("!", "FFB gain NOT changed", "Wanted " + gain + " for " + label + " — " + err, false, _cfg.OverlaySeconds);
                        }
                        if (_queuedGain >= 0) RunApply(false);
                    }));
                }
                catch (InvalidOperationException) { }

                // The game can create new copies of its settings (e.g. when the stage or the settings menu
                // loads). After a stage load, scan again and write to every copy found.
                if (ok && fullRescan)
                {
                    Thread.Sleep(2000);
                    string ignored;
                    if (_applyGen == myGen) _writer.Apply(pid, gain, true, out ignored);
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        StageConfig MatchStage(string track, out RallyConfig rally)
        {
            string t = StageScanner.Norm(track);
            rally = null;

            // 1. Telemetry only names the rally and an area (e.g. "Wales Cwmbiga" for Hafren North).
            //    The exact stage you picked is written to the save file when the event starts
            //    (e.g. "WelesS3HafrenNorthFullForward"), so use that for the rally telemetry reports.
            RallyConfig telemetryRally = null;
            int rallyLen = 0;
            foreach (var r in _cfg.Rallies)
            {
                if (r.Id == "Other") continue;
                foreach (var n in new[] { StageScanner.Norm(r.Id), StageScanner.Norm(r.Name) })
                    if (n.Length > rallyLen && t.StartsWith(n)) { telemetryRally = r; rallyLen = n.Length; }
            }
            if (telemetryRally != null)
            {
                var fromSave = StageFromSave(telemetryRally);
                if (fromSave != null) { rally = telemetryRally; return fromSave; }
            }

            // 2. Fall back to the telemetry name: stages from the game files first (longest name wins,
            //    so "Hafren North" beats "Hafren"), then stages added from telemetry.
            for (int pass = 0; pass < 2; pass++)
            {
                StageConfig best = null;
                int bestLen = 0;
                foreach (var r in _cfg.Rallies)
                {
                    foreach (var s in r.Stages)
                    {
                        if (s.Detected != (pass == 1)) continue;
                        string n = StageScanner.Norm(s.Id);
                        if (n.Length == 0) continue;
                        bool hit = pass == 0 ? t.Contains(n) : t == n;
                        if (hit && n.Length > bestLen) { best = s; rally = r; bestLen = n.Length; }
                    }
                }
                if (best != null) return best;
            }

            foreach (var r in _cfg.Rallies)
            {
                string n = StageScanner.Norm(r.Id);
                if (n.Length > 0 && r.Id != "Other" && t.Contains(n)) { rally = r; return null; }
            }
            return null;
        }

        static readonly System.Text.RegularExpressions.Regex SaveStageRx =
            new System.Text.RegularExpressions.Regex(@"(?<![A-Za-z])([A-Z][a-z]+(?:[A-Z][a-z]+)?)S\d+([A-Za-z0-9]+?(?:Forward|Reverse)[A-Za-z0-9]*)");

        /// <summary>First stage of the given rally named in the save file (= the event that was just started).</summary>
        StageConfig StageFromSave(RallyConfig rally)
        {
            byte[] save = GainWriter.ReadSave();
            if (save == null) return null;
            string s = System.Text.Encoding.GetEncoding(28591).GetString(save);
            foreach (System.Text.RegularExpressions.Match m in SaveStageRx.Matches(s))
            {
                if (!string.Equals(StageScanner.NormalizeRally(m.Groups[1].Value), rally.Id, StringComparison.OrdinalIgnoreCase)) continue;
                string token = StageScanner.Norm(m.Groups[2].Value);
                StageConfig best = null;
                foreach (var st in rally.Stages)
                {
                    if (st.Detected) continue;
                    string n = StageScanner.Norm(st.Id);
                    if (n.Length > 0 && token.StartsWith(n) && (best == null || n.Length > StageScanner.Norm(best.Id).Length)) best = st;
                }
                if (best != null)
                {
                    Log.Write("Save file says: " + m.Value + " -> " + best.Id);
                    return best;
                }
            }
            return null;
        }

        void AddDetectedStage(string track)
        {
            var other = _cfg.FindRally("Other");
            if (other == null)
            {
                other = new RallyConfig { Id = "Other", Name = "Other (detected in game)", Gain = _cfg.DefaultGain };
                _cfg.Rallies.Add(other);
            }
            other.Stages.Add(new StageConfig { Id = track, Name = track, Gain = _cfg.DefaultGain, Enabled = false, Detected = true });
            _cfg.Save();
            RebuildList();
        }
    }

    public static class AppIcon
    {
        public static Icon Create()
        {
            var bmp = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (var b = new SolidBrush(Theme.Accent)) g.FillEllipse(b, 1, 1, 30, 30);
                using (var p = new Pen(Color.White, 3f)) g.DrawEllipse(p, 7, 7, 18, 18);
                using (var p = new Pen(Color.White, 3f)) { g.DrawLine(p, 16, 16, 16, 25); g.DrawLine(p, 8, 14, 24, 14); }
            }
            return Icon.FromHandle(bmp.GetHicon());
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TimeZoneWidgets
{
    /// <summary>
    /// The application: a tray icon that owns all widget windows, the globe, the settings dialog
    /// and the wallpaper watcher. Nothing here needs a network connection.
    /// </summary>
    public class TrayApp : ApplicationContext
    {
        public const string ShowSettingsEventName = @"Local\TimeZoneWidgets.ShowSettings";

        public AppSettings Settings;
        public Theme Theme;

        readonly List<WidgetForm> _widgets = new List<WidgetForm>();
        GlobeForm _globe;
        readonly NotifyIcon _tray;
        readonly System.Windows.Forms.Timer _wallpaperPoll;
        string _wallpaperSignature;
        SettingsForm _settingsForm;
        ToolStripMenuItem _startupItem, _hideItem, _globeItem;
        bool _hidden;
        bool _exiting;

        public TrayApp()
        {
            Settings = AppSettings.Load();
            StartupManager.RepairIfMoved();
            Theme = ComputeTheme();
            _wallpaperSignature = WallpaperAnalyzer.Signature();

            _tray = new NotifyIcon();
            _tray.Icon = LoadAppIcon();
            _tray.Text = "Time Zone Widgets";
            _tray.ContextMenuStrip = BuildTrayMenu();
            _tray.DoubleClick += delegate { ShowSettings(); };
            _tray.Visible = true;

            if (Settings.Widgets.Count == 0) AddDefaultWidgets();
            foreach (var cfg in Settings.Widgets.ToArray()) CreateWidget(cfg);
            if (Settings.ShowGlobe) CreateGlobe();
            LayOutUnplaced();
            Settings.Save();

            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

            // Slideshow / Spotlight wallpapers change without any notification we can hook, so poll cheaply.
            _wallpaperPoll = new System.Windows.Forms.Timer();
            _wallpaperPoll.Interval = 20000;
            _wallpaperPoll.Tick += delegate { CheckWallpaper(); };
            _wallpaperPoll.Start();

            ListenForSecondInstance();
        }

        // ---- widgets --------------------------------------------------------------------------

        public IList<WidgetForm> Widgets { get { return _widgets.AsReadOnly(); } }
        public bool GlobeVisible { get { return _globe != null && !_globe.IsDisposed; } }

        IEnumerable<LayeredWindow> AllWindows
        {
            get
            {
                foreach (var w in _widgets) yield return w;
                if (GlobeVisible) yield return _globe;
            }
        }

        void AddDefaultWidgets()
        {
            var ids = new List<string>();
            ids.Add(TimeZoneInfo.Local.Id);
            foreach (var id in new[] { "Eastern Standard Time", "GMT Standard Time", "Tokyo Standard Time" })
                if (!ids.Contains(id) && ZoneCatalog.Exists(id)) ids.Add(id);
            foreach (var id in ids)
            {
                var cfg = new WidgetConfig();
                cfg.TimeZoneId = id;
                cfg.Label = id == TimeZoneInfo.Local.Id ? "Local" : ZoneCatalog.LabelFor(id);
                cfg.X = -1;
                cfg.Y = -1;
                Settings.Widgets.Add(cfg);
            }
        }

        WidgetForm CreateWidget(WidgetConfig cfg)
        {
            var w = new WidgetForm(this, cfg, Settings, Theme);
            _widgets.Add(w);
            w.Show();
            return w;
        }

        void CreateGlobe()
        {
            if (GlobeVisible) return;
            _globe = new GlobeForm(this, Settings, Theme);
            _globe.Show();
            if (_hidden) _globe.Hide();
        }

        /// <summary>Place windows that have no saved position (or whose monitor disappeared) in a column at the top-right.</summary>
        void LayOutUnplaced()
        {
            var area = Screen.PrimaryScreen.WorkingArea;
            int margin = 24, gap = 12;
            int nextY = area.Top + margin;
            // Start below any window already sitting in that column.
            foreach (var w in AllWindows)
                if (w.SavedX >= 0 && IsOnScreen(w.SavedX, w.SavedY) && w.SavedX > area.Right - w.Width - margin - 40)
                    nextY = Math.Max(nextY, w.SavedY + w.Height + gap);
            foreach (var w in AllWindows)
            {
                if (w.SavedX >= 0 && IsOnScreen(w.SavedX, w.SavedY)) continue;
                if (nextY + w.Height > area.Bottom) nextY = area.Top + margin;
                w.MoveTo(area.Right - w.Width - margin, nextY);
                nextY += w.Height + gap;
            }
        }

        static bool IsOnScreen(int x, int y)
        {
            foreach (var screen in Screen.AllScreens)
                if (screen.WorkingArea.Contains(new Point(x + 20, y + 20))) return true;
            return false;
        }

        public void AddWidget(string windowsId, string label)
        {
            var cfg = new WidgetConfig();
            cfg.TimeZoneId = windowsId;
            cfg.Label = label;
            cfg.X = -1;
            cfg.Y = -1;
            Settings.Widgets.Add(cfg);
            var w = CreateWidget(cfg);
            if (_hidden) w.Hide();
            LayOutUnplaced();
            Settings.Save();
            ZonesChanged();
        }

        /// <summary>Adds a widget for the next popular zone not already shown, just below the given one.</summary>
        public void AddWidgetNear(WidgetForm anchor)
        {
            ZoneEntry pick = null;
            foreach (var zone in ZoneCatalog.Popular)
            {
                if (!ZoneCatalog.Exists(zone.WindowsId)) continue;
                bool used = false;
                foreach (var w in _widgets)
                    if (string.Equals(w.Config.TimeZoneId, zone.WindowsId, StringComparison.OrdinalIgnoreCase)) { used = true; break; }
                if (!used) { pick = zone; break; }
            }
            if (pick == null) pick = new ZoneEntry("UTC", "UTC", 0, 0);

            var cfg = new WidgetConfig();
            cfg.TimeZoneId = pick.WindowsId;
            cfg.Label = pick.City;
            cfg.X = anchor.Left;
            cfg.Y = anchor.Top + anchor.Height + 12;
            Settings.Widgets.Add(cfg);
            CreateWidget(cfg);
            if (!IsOnScreen(cfg.X, cfg.Y))
            {
                cfg.X = -1;
                LayOutUnplaced();
            }
            Settings.Save();
            ZonesChanged();
        }

        public void RemoveWidget(WidgetForm w)
        {
            _widgets.Remove(w);
            Settings.Widgets.Remove(w.Config);
            w.Close();
            w.Dispose();
            Settings.Save();
            ZonesChanged();
            if (_settingsForm != null && !_settingsForm.IsDisposed) _settingsForm.RefreshWidgetList();
        }

        /// <summary>Called whenever the set of zones changes, so the globe can move its markers.</summary>
        public void ZonesChanged()
        {
            if (GlobeVisible) _globe.RefreshMarkers();
            if (_settingsForm != null && !_settingsForm.IsDisposed) _settingsForm.RefreshWidgetList();
        }

        public void SetGlobeVisible(bool visible)
        {
            Settings.ShowGlobe = visible;
            if (visible)
            {
                CreateGlobe();
                LayOutUnplaced();
            }
            else if (GlobeVisible)
            {
                _globe.Close();
                _globe.Dispose();
                _globe = null;
            }
            Settings.Save();
            if (_globeItem != null) _globeItem.Checked = visible;
            if (_settingsForm != null && !_settingsForm.IsDisposed) _settingsForm.RefreshGlobe();
        }

        public void ResetPositions()
        {
            foreach (var w in AllWindows) w.SavedX = -1;
            LayOutUnplaced();
            Settings.Save();
        }

        public void SetHidden(bool hidden)
        {
            _hidden = hidden;
            foreach (var w in AllWindows)
            {
                if (hidden) w.Hide();
                else w.Show();
            }
            if (_hideItem != null) _hideItem.Text = hidden ? "Show widgets" : "Hide widgets";
        }

        // ---- settings & theme -----------------------------------------------------------------

        Theme ComputeTheme()
        {
            return Settings.AutoTheme ? WallpaperAnalyzer.ComputeTheme() : Theme.FromSettings(Settings);
        }

        public void SaveSettings()
        {
            Settings.Save();
        }

        /// <summary>Push the current settings to every window and persist them.</summary>
        public void ApplyAll()
        {
            Theme = ComputeTheme();
            foreach (var w in AllWindows) w.ApplySettings(Settings, Theme);
            Settings.Save();
        }

        /// <summary>Force a fresh look at the wallpaper (used by the "Re-detect" button).</summary>
        public void RedetectWallpaper()
        {
            _wallpaperSignature = WallpaperAnalyzer.Signature();
            ApplyAll();
            if (_settingsForm != null && !_settingsForm.IsDisposed) _settingsForm.RefreshSwatches();
        }

        void CheckWallpaper()
        {
            if (!Settings.AutoTheme) return;
            var sig = WallpaperAnalyzer.Signature();
            if (sig == _wallpaperSignature) return;
            _wallpaperSignature = sig;
            ApplyAll();
            if (_settingsForm != null && !_settingsForm.IsDisposed) _settingsForm.RefreshSwatches();
        }

        void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.Desktop || e.Category == UserPreferenceCategory.General
                || e.Category == UserPreferenceCategory.Color || e.Category == UserPreferenceCategory.VisualStyle)
            {
                // The wallpaper file may still be being written; look again shortly.
                var t = new System.Windows.Forms.Timer();
                t.Interval = 1500;
                t.Tick += delegate { t.Stop(); t.Dispose(); _wallpaperSignature = null; CheckWallpaper(); };
                t.Start();
            }
        }

        void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            foreach (var w in AllWindows)
                if (!IsOnScreen(w.SavedX, w.SavedY)) w.SavedX = -1;
            LayOutUnplaced();
        }

        public void ShowSettings()
        {
            if (_settingsForm == null || _settingsForm.IsDisposed)
            {
                _settingsForm = new SettingsForm(this);
                _settingsForm.Show();
            }
            else
            {
                if (_settingsForm.WindowState == FormWindowState.Minimized) _settingsForm.WindowState = FormWindowState.Normal;
                _settingsForm.Show();
                _settingsForm.BringToFront();
            }
            _settingsForm.Activate();
        }

        public void Exit()
        {
            if (_exiting) return;
            _exiting = true;
            Settings.Save();
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            _wallpaperPoll.Stop();
            if (_signalPoll != null) _signalPoll.Stop();
            _tray.Visible = false;
            _tray.Dispose();
            foreach (var w in _widgets.ToArray()) { w.Close(); w.Dispose(); }
            _widgets.Clear();
            if (GlobeVisible) { _globe.Close(); _globe.Dispose(); _globe = null; }
            if (_settingsForm != null && !_settingsForm.IsDisposed) _settingsForm.Close();
            ExitThread();
        }

        // ---- tray -----------------------------------------------------------------------------

        ContextMenuStrip BuildTrayMenu()
        {
            var menu = new ContextMenuStrip();
            menu.Font = new Font("Segoe UI", 9.5f);

            var header = new ToolStripMenuItem("Time Zone Widgets");
            header.Enabled = false;
            menu.Items.Add(header);
            menu.Items.Add(new ToolStripSeparator());

            var add = new ToolStripMenuItem("Add widget");
            foreach (var region in ZoneCatalog.Regions)
            {
                var regionItem = new ToolStripMenuItem(region.Name);
                foreach (var zone in region.Zones)
                {
                    if (!ZoneCatalog.Exists(zone.WindowsId)) continue;
                    var z = zone;
                    var item = new ToolStripMenuItem(zone.City);
                    item.Click += delegate { AddWidget(z.WindowsId, z.City); };
                    regionItem.DropDownItems.Add(item);
                }
                add.DropDownItems.Add(regionItem);
            }
            var local = new ToolStripMenuItem("Local time");
            local.Click += delegate { AddWidget(TimeZoneInfo.Local.Id, "Local"); };
            add.DropDownItems.Add(new ToolStripSeparator());
            add.DropDownItems.Add(local);
            menu.Items.Add(add);

            _globeItem = new ToolStripMenuItem("Show globe");
            _globeItem.CheckOnClick = true;
            _globeItem.Checked = Settings.ShowGlobe;
            _globeItem.Click += delegate { SetGlobeVisible(_globeItem.Checked); };
            menu.Items.Add(_globeItem);

            var settings = new ToolStripMenuItem("Settings...");
            settings.Click += delegate { ShowSettings(); };
            menu.Items.Add(settings);

            _hideItem = new ToolStripMenuItem("Hide widgets");
            _hideItem.Click += delegate { SetHidden(!_hidden); };
            menu.Items.Add(_hideItem);

            _startupItem = new ToolStripMenuItem("Start with Windows");
            _startupItem.CheckOnClick = true;
            _startupItem.Click += delegate
            {
                try { StartupManager.SetEnabled(_startupItem.Checked); }
                catch (Exception ex) { MessageBox.Show("Could not change the startup setting:\n" + ex.Message, "Time Zone Widgets", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                if (_settingsForm != null && !_settingsForm.IsDisposed) _settingsForm.RefreshStartup();
            };
            menu.Items.Add(_startupItem);
            menu.Items.Add(new ToolStripSeparator());

            var exit = new ToolStripMenuItem("Exit");
            exit.Click += delegate { Exit(); };
            menu.Items.Add(exit);

            menu.Opening += delegate
            {
                _startupItem.Checked = StartupManager.IsEnabled();
                _globeItem.Checked = GlobeVisible;
            };
            return menu;
        }

        static Icon LoadAppIcon()
        {
            try
            {
                var icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (icon != null) return icon;
            }
            catch { }
            return DrawFallbackIcon();
        }

        static Icon DrawFallbackIcon()
        {
            using (var bmp = new Bitmap(32, 32))
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                using (var b = new SolidBrush(Color.FromArgb(30, 99, 214))) g.FillEllipse(b, 1, 1, 30, 30);
                using (var p = new Pen(Color.White, 2.5f))
                {
                    p.StartCap = LineCap.Round;
                    p.EndCap = LineCap.Round;
                    g.DrawLine(p, 16, 16, 16, 7);
                    g.DrawLine(p, 16, 16, 23, 19);
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
        }

        /// <summary>A second launch of the exe signals this event; we respond by opening Settings.</summary>
        EventWaitHandle _showSettingsSignal;
        System.Windows.Forms.Timer _signalPoll;

        void ListenForSecondInstance()
        {
            try { _showSettingsSignal = new EventWaitHandle(false, EventResetMode.AutoReset, ShowSettingsEventName); }
            catch { return; }
            _signalPoll = new System.Windows.Forms.Timer();
            _signalPoll.Interval = 400;
            _signalPoll.Tick += delegate
            {
                if (_exiting) return;
                if (_showSettingsSignal.WaitOne(0)) ShowSettings();
            };
            _signalPoll.Start();
        }
    }
}

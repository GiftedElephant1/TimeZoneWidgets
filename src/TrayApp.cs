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
    /// The application: a tray icon that owns the clock widgets (or the docked ribbon), the globe,
    /// the settings dialog and the wallpaper watcher. Nothing here needs a network connection.
    /// </summary>
    public class TrayApp : ApplicationContext
    {
        public const string ShowSettingsEventName = @"Local\TimeZoneWidgets.ShowSettings";

        public AppSettings Settings;
        public Theme Theme;

        readonly List<WidgetForm> _widgets = new List<WidgetForm>();
        GlobeForm _globe;
        readonly List<RibbonForm> _ribbons = new List<RibbonForm>();   // one per monitor it is shown on
        readonly NotifyIcon _tray;
        readonly System.Windows.Forms.Timer _wallpaperPoll;
        string _wallpaperSignature;
        SettingsForm _settingsForm;
        ToolStripMenuItem _startupItem, _hideItem, _globeItem;
        readonly List<ToolStripMenuItem> _layoutItems = new List<ToolStripMenuItem>();
        readonly Dictionary<string, TimeZoneInfo> _zoneCache = new Dictionary<string, TimeZoneInfo>(StringComparer.OrdinalIgnoreCase);
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
            if (RibbonMode) CreateRibbons();
            else foreach (var cfg in Settings.Widgets.ToArray()) CreateWidget(cfg);
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

        // ---- zones ----------------------------------------------------------------------------

        public IList<WidgetForm> Widgets { get { return _widgets.AsReadOnly(); } }
        public bool GlobeVisible { get { return _globe != null && !_globe.IsDisposed; } }
        public bool RibbonMode { get { return Settings.LayoutMode == AppSettings.LayoutRibbon; } }

        /// <summary>Resolve (and cache) the TimeZoneInfo for a configured zone.</summary>
        public TimeZoneInfo ZoneFor(WidgetConfig cfg)
        {
            TimeZoneInfo tz;
            if (!_zoneCache.TryGetValue(cfg.TimeZoneId ?? "", out tz))
            {
                tz = ZoneCatalog.Find(cfg.TimeZoneId);
                _zoneCache[cfg.TimeZoneId ?? ""] = tz;
            }
            return tz;
        }

        IEnumerable<LayeredWindow> AllWindows
        {
            get
            {
                foreach (var w in _widgets) yield return w;
                if (GlobeVisible) yield return _globe;
                foreach (var r in _ribbons) if (!r.IsDisposed) yield return r;
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
                cfg.Label = id == TimeZoneInfo.Local.Id ? ZoneCatalog.LocalCity() : ZoneCatalog.LabelFor(id);
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
            if (_hidden) w.Hide();
            return w;
        }

        void CreateGlobe()
        {
            if (GlobeVisible) return;
            _globe = new GlobeForm(this, Settings, Theme);
            _globe.Show();
            if (_hidden) _globe.Hide();
        }

        /// <summary>The monitors the ribbon should be on: every monitor, or the chosen one (falling
        /// back to the primary monitor when the chosen one is not connected right now).</summary>
        List<Screen> RibbonTargets()
        {
            var list = new List<Screen>();
            if (Settings.RibbonAllMonitors) { list.AddRange(Screen.AllScreens); return list; }
            foreach (var s in Screen.AllScreens)
                if (!string.IsNullOrEmpty(Settings.RibbonMonitor) && s.DeviceName == Settings.RibbonMonitor) { list.Add(s); return list; }
            list.Add(Screen.PrimaryScreen);
            return list;
        }

        /// <summary>Make the set of ribbon windows match the monitors they should be on.</summary>
        void CreateRibbons()
        {
            var targets = RibbonTargets();
            foreach (var r in _ribbons.ToArray())
            {
                bool wanted = false;
                foreach (var s in targets) if (!r.IsDisposed && s.DeviceName == r.ScreenDevice) { wanted = true; break; }
                if (!wanted) { _ribbons.Remove(r); if (!r.IsDisposed) { r.Close(); r.Dispose(); } }
            }
            foreach (var s in targets)
            {
                bool have = false;
                foreach (var r in _ribbons) if (r.ScreenDevice == s.DeviceName) { have = true; break; }
                if (have) continue;
                var ribbon = new RibbonForm(this, Settings, Theme, s.DeviceName);
                _ribbons.Add(ribbon);
                ribbon.Show();
                if (_hidden) ribbon.Hide();
            }
        }

        void CloseRibbons()
        {
            foreach (var r in _ribbons) if (!r.IsDisposed) { r.Close(); r.Dispose(); }
            _ribbons.Clear();
        }

        /// <summary>"Monitor 2 (primary), 1920×1080" for menus and the settings dialog.</summary>
        public static string DescribeScreen(Screen s, int index)
        {
            return "Monitor " + (index + 1) + (s.Primary ? " (primary), " : ", ") + s.Bounds.Width + "×" + s.Bounds.Height;
        }

        /// <summary>Dock the ribbon to one monitor (and stop showing it on the others).</summary>
        public void SetRibbonMonitor(string deviceName)
        {
            Settings.RibbonMonitor = deviceName ?? "";
            Settings.RibbonAllMonitors = false;
            if (Settings.LayoutMode != AppSettings.LayoutRibbon) { SetLayoutMode(AppSettings.LayoutRibbon); return; }
            CreateRibbons();
            Settings.Save();
            RibbonEdgeChanged();
        }

        public void SetRibbonAllMonitors(bool all)
        {
            Settings.RibbonAllMonitors = all;
            if (Settings.LayoutMode != AppSettings.LayoutRibbon) { SetLayoutMode(AppSettings.LayoutRibbon); return; }
            CreateRibbons();
            Settings.Save();
            RibbonEdgeChanged();
        }

        /// <summary>A ribbon was dragged onto another monitor: remember it and drop the old window.</summary>
        public void RibbonMovedToMonitor(string deviceName)
        {
            Settings.RibbonMonitor = deviceName ?? "";
            CreateRibbons();
            Settings.Save();
            RibbonEdgeChanged();
        }

        /// <summary>Place floating windows that have no saved position (or whose monitor disappeared) in a column at the top-right.</summary>
        void LayOutUnplaced()
        {
            var area = Screen.PrimaryScreen.WorkingArea;
            int margin = 24, gap = 12;
            int nextY = area.Top + margin;
            // Start below any window already sitting in that column.
            foreach (var w in AllWindows)
            {
                if (w is RibbonForm) continue;
                if (w.SavedX >= 0 && IsOnScreen(w.SavedX, w.SavedY) && w.SavedX > area.Right - w.Width - margin - 40)
                    nextY = Math.Max(nextY, w.SavedY + w.Height + gap);
            }
            foreach (var w in AllWindows)
            {
                if (w is RibbonForm) continue;
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
            if (RibbonMode)
            {
                foreach (var r in _ribbons) if (!r.IsDisposed) r.ZonesChanged();
            }
            else
            {
                CreateWidget(cfg);
                LayOutUnplaced();
            }
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
                foreach (var c in Settings.Widgets)
                    if (string.Equals(c.TimeZoneId, zone.WindowsId, StringComparison.OrdinalIgnoreCase)) { used = true; break; }
                if (!used) { pick = zone; break; }
            }
            if (pick == null) pick = new ZoneEntry("UTC", "UTC", "UTC", 0, 0);

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

        WidgetForm FormFor(WidgetConfig cfg)
        {
            foreach (var w in _widgets) if (w.Config == cfg) return w;
            return null;
        }

        public void ChangeZone(WidgetConfig cfg, string windowsId, string label)
        {
            var form = FormFor(cfg);
            if (form != null)
            {
                form.SetTimeZone(windowsId, label);   // updates cfg, saves and calls ZonesChanged
                return;
            }
            cfg.TimeZoneId = windowsId;
            cfg.Label = string.IsNullOrEmpty(label) ? ZoneCatalog.LabelFor(windowsId) : label;
            foreach (var r in _ribbons) if (!r.IsDisposed) r.ZonesChanged();
            Settings.Save();
            ZonesChanged();
        }

        public void RenameZone(WidgetConfig cfg, string label)
        {
            var form = FormFor(cfg);
            if (form != null) { form.Rename(label); }
            else
            {
                cfg.Label = string.IsNullOrEmpty(label) ? ZoneCatalog.LabelFor(cfg.TimeZoneId) : label;
                foreach (var r in _ribbons) if (!r.IsDisposed) r.ZonesChanged();
                Settings.Save();
            }
            ZonesChanged();
        }

        public void RemoveZone(WidgetConfig cfg)
        {
            Settings.Widgets.Remove(cfg);
            var form = FormFor(cfg);
            if (form != null)
            {
                _widgets.Remove(form);
                form.Close();
                form.Dispose();
            }
            foreach (var r in _ribbons) if (!r.IsDisposed) r.ZonesChanged();
            Settings.Save();
            ZonesChanged();
        }

        public void RemoveWidget(WidgetForm w)
        {
            RemoveZone(w.Config);
        }

        /// <summary>Called whenever the set of zones changes, so the globe and the settings list follow.</summary>
        public void ZonesChanged()
        {
            if (GlobeVisible) _globe.RefreshMarkers();
            if (_settingsForm != null && !_settingsForm.IsDisposed) _settingsForm.RefreshWidgetList();
        }

        // ---- layout: floating widgets vs. docked ribbon ---------------------------------------

        public void SetLayoutMode(string mode)
        {
            if (mode != AppSettings.LayoutRibbon) mode = AppSettings.LayoutFloating;
            if (Settings.LayoutMode != mode)
            {
                Settings.LayoutMode = mode;
                if (mode == AppSettings.LayoutRibbon)
                {
                    foreach (var w in _widgets.ToArray()) { w.Close(); w.Dispose(); }
                    _widgets.Clear();
                    CreateRibbons();
                }
                else
                {
                    CloseRibbons();
                    foreach (var cfg in Settings.Widgets.ToArray()) CreateWidget(cfg);
                    LayOutUnplaced();
                }
                Settings.Save();
            }
            RibbonEdgeChanged();
        }

        public void SetRibbonEdge(string edge)
        {
            Settings.RibbonEdge = edge;
            if (Settings.LayoutMode != AppSettings.LayoutRibbon) { SetLayoutMode(AppSettings.LayoutRibbon); return; }
            Settings.Save();
            RibbonEdgeChanged();
        }

        /// <summary>Keeps every ribbon, the tray menu and the settings dialog in step after the
        /// ribbon's edge or monitor changed (from a menu, the dialog, or by dragging a bar).</summary>
        public void RibbonEdgeChanged()
        {
            foreach (var r in _ribbons) if (!r.IsDisposed) r.ApplySettings(Settings, Theme);
            UpdateLayoutChecks();
            if (_settingsForm != null && !_settingsForm.IsDisposed) _settingsForm.RefreshLayout();
        }

        void UpdateLayoutChecks()
        {
            foreach (var item in _layoutItems)
            {
                var tag = (string)item.Tag;
                item.Checked = tag == "floating" ? !RibbonMode : (RibbonMode && Settings.RibbonEdge == tag);
            }
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
            if (RibbonMode) CreateRibbons();   // a monitor may have been plugged in or removed
            foreach (var w in AllWindows)
            {
                if (w is RibbonForm) { w.ApplySettings(Settings, Theme); continue; }
                if (!IsOnScreen(w.SavedX, w.SavedY)) w.SavedX = -1;
            }
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
            CloseRibbons();
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

            var add = new ToolStripMenuItem("Add time zone");
            ZoneMenus.Fill(add, delegate(string id, string label) { AddWidget(id, label); });
            menu.Items.Add(add);

            var layout = new ToolStripMenuItem("Layout");
            foreach (var pair in new[] { new[] { "Floating widgets", "floating" }, new[] { "Ribbon at the top", AppSettings.EdgeTop },
                                         new[] { "Ribbon at the bottom", AppSettings.EdgeBottom }, new[] { "Ribbon on the left", AppSettings.EdgeLeft },
                                         new[] { "Ribbon on the right", AppSettings.EdgeRight } })
            {
                var tag = pair[1];
                var item = new ToolStripMenuItem(pair[0]);
                item.Tag = tag;
                item.Click += delegate
                {
                    if (tag == "floating") SetLayoutMode(AppSettings.LayoutFloating);
                    else SetRibbonEdge(tag);
                };
                _layoutItems.Add(item);
                layout.DropDownItems.Add(item);
                if (tag == "floating") layout.DropDownItems.Add(new ToolStripSeparator());
            }
            menu.Items.Add(layout);

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
                UpdateLayoutChecks();
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

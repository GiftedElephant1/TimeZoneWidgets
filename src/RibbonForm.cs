using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Windows.Forms;

namespace TimeZoneWidgets
{
    /// <summary>
    /// The "ribbon" layout: every time zone in one bar docked to an edge of the primary screen.
    /// Horizontal ribbons show each zone as a compact cell: city and day/date stacked on the left,
    /// the time beside them on the right (a very thin ribbon collapses to a single line).
    /// Vertical ribbons stack the three rows. Drag the bar toward another edge to move it there;
    /// drag its inner edge to change its height (top/bottom) or width (left/right). By default it
    /// overlays the screen; optionally it reserves space like the taskbar.
    /// </summary>
    public class RibbonForm : LayeredWindow
    {
        const int WM_WINDOWPOSCHANGED = 0x0047;
        const int ResizeBand = 7;      // design pixels along the inner edge that act as a resize grip
        const int EdgeDragThreshold = 40;

        class Cell
        {
            public WidgetConfig Config;
            public TimeZoneInfo Tz;
            public Rectangle Bounds;
            public string Label, Time, AmPm, Offset, Day, Date, Glyph;
            public string Market = "", MarketText = "";   // stock exchange + countdown (empty when unknown/off)
            public bool MarketOpen;
            public int MarketPhase;      // see MarketStatus.Phase
        }

        readonly Timer _timer;
        readonly List<Cell> _cells = new List<Cell>();
        string _lastKey;
        AppBar _appBar;

        Font _fLabel, _fTime, _fAmPm, _fDay, _fDate, _fSmall, _fGlyph, _fTiny, _fMarket;
        bool _marketRow;   // horizontal compact layout shows the stock market on a second line
        bool _nudgedShell; // see TargetBounds
        Bitmap _frame;     // the last full rendering of the bar (auto-hide pushes a cropped part of it)
        float _fit = 1f;
        bool _singleRow;

        bool _resizing, _edgeDrag, _menuOpen;
        bool _pendingDrag, _reorder;   // left button down, not yet decided / dragging a zone along the bar
        WidgetConfig _dragCfg;         // the zone under the mouse when the drag started
        const int ReorderThreshold = 12;
        Point _dragStart;

        // Auto-hide: the bar's full ("shown") bounds, and how far it is currently slid off-screen.
        readonly Timer _hoverTimer;
        Rectangle _shown;
        int _slide;
        DateTime _lastSeen = DateTime.MinValue;
        const int HideDelayMs = 700;

        public RibbonForm(TrayApp app, AppSettings settings, Theme theme, string screenDevice)
            : base(app)
        {
            Text = "Time zone ribbon";
            ScreenDevice = screenDevice ?? "";
            Size = new Size(600, 80);
            _timer = new Timer();
            _timer.Interval = 1000;
            _timer.Tick += delegate { Tick(); };
            _hoverTimer = new Timer();
            _hoverTimer.Interval = 50;
            _hoverTimer.Tick += delegate { HoverTick(); };
            ApplySettings(settings, theme);
        }

        bool AutoHide { get { return S != null && S.RibbonAutoHide; } }

        /// <summary>Device name (Screen.DeviceName) of the monitor this bar docks to.</summary>
        public string ScreenDevice { get; private set; }

        /// <summary>The monitor this bar docks to; the primary one if that monitor is gone.</summary>
        public Screen TargetScreen
        {
            get
            {
                foreach (var s in Screen.AllScreens) if (s.DeviceName == ScreenDevice) return s;
                return Screen.PrimaryScreen;
            }
        }

        // An auto-hiding bar must be able to slide over other windows, so it is always on top and
        // never pinned to the desktop while auto-hide is on.
        protected override bool PinnedToBottom { get { return base.PinnedToBottom && !AutoHide; } }
        protected override bool WantsTopMost(AppSettings settings) { return base.WantsTopMost(settings) || settings.RibbonAutoHide; }

        // The ribbon positions itself; it has no free-floating saved position.
        public override int SavedX { get { return 0; } set { } }
        public override int SavedY { get { return 0; } set { } }
        protected override int OpacityPercent { get { return S == null ? 100 : S.Opacity; } }

        string Edge { get { return S.RibbonEdge; } }
        bool Horizontal { get { return Edge == AppSettings.EdgeTop || Edge == AppSettings.EdgeBottom; } }
        int ThicknessDesign
        {
            get { return Horizontal ? S.RibbonHeight : S.RibbonWidth; }
            set { if (Horizontal) S.RibbonHeight = value; else S.RibbonWidth = value; }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            _appBar = new AppBar(Handle);
            base.OnHandleCreated(e);
            // Form.CreateHandle is still running at this point and clamps any size set now to the
            // minimum for a captioned window (39 px). Apply the real bounds once creation is over
            // so a thin single-line ribbon really is thin.
            BeginInvoke((MethodInvoker)delegate { if (IsHandleCreated) { Relayout(); Render(true); } });
            _timer.Start();
            _hoverTimer.Enabled = AutoHide;
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (_appBar != null) { _appBar.Unregister(); _appBar = null; }
            base.OnHandleDestroyed(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _timer.Stop();
            _timer.Dispose();
            _hoverTimer.Stop();
            _hoverTimer.Dispose();
            if (_appBar != null) { _appBar.Unregister(); _appBar = null; }
            if (_frame != null) { _frame.Dispose(); _frame = null; }
            DisposeFonts();
            base.OnFormClosed(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (_appBar != null && m.Msg == (int)_appBar.CallbackMessage)
            {
                if ((int)m.WParam == AppBar.ABN_POSCHANGED) { Relayout(); Render(true); }
                return;
            }
            base.WndProc(ref m);
            if (m.Msg == WM_WINDOWPOSCHANGED && _appBar != null) _appBar.NotifyPositionChanged();
        }

        public override void ApplySettings(AppSettings settings, Theme theme)
        {
            RebuildCells(settings);
            if (!settings.RibbonAutoHide) _slide = 0;
            base.ApplySettings(settings, theme);
            _hoverTimer.Enabled = settings.RibbonAutoHide && IsHandleCreated;
        }

        // ---- auto-hide --------------------------------------------------------------------------

        /// <summary>The shown bounds shifted off-screen by <paramref name="slide"/> pixels toward the edge.</summary>
        Rectangle Placed(Rectangle shown, int slide)
        {
            switch (Edge)
            {
                case AppSettings.EdgeTop: return new Rectangle(shown.X, shown.Y - slide, shown.Width, shown.Height);
                case AppSettings.EdgeBottom: return new Rectangle(shown.X, shown.Y + slide, shown.Width, shown.Height);
                case AppSettings.EdgeLeft: return new Rectangle(shown.X - slide, shown.Y, shown.Width, shown.Height);
                default: return new Rectangle(shown.X + slide, shown.Y, shown.Width, shown.Height);
            }
        }

        /// <summary>How far to slide when hidden: the whole bar, so nothing stays on screen.</summary>
        int HiddenSlide
        {
            get { return Math.Max(0, Horizontal ? _shown.Height : _shown.Width); }
        }

        /// <summary>True when the mouse is pressed against the screen edge the ribbon lives on,
        /// somewhere along the ribbon's length.</summary>
        bool AtEdge(Point p)
        {
            var screen = TargetScreen.Bounds;
            switch (Edge)
            {
                case AppSettings.EdgeTop: return p.Y >= screen.Top && p.Y <= screen.Top + 1 && p.X >= _shown.Left && p.X < _shown.Right;
                case AppSettings.EdgeBottom: return p.Y >= screen.Bottom - 2 && p.Y < screen.Bottom && p.X >= _shown.Left && p.X < _shown.Right;
                case AppSettings.EdgeLeft: return p.X >= screen.Left && p.X <= screen.Left + 1 && p.Y >= _shown.Top && p.Y < _shown.Bottom;
                default: return p.X >= screen.Right - 2 && p.X < screen.Right && p.Y >= _shown.Top && p.Y < _shown.Bottom;
            }
        }

        void HoverTick()
        {
            if (S == null || !IsHandleCreated || !Visible) return;
            if (!AutoHide) { _hoverTimer.Stop(); if (_slide != 0) { _slide = 0; ApplySlide(); } return; }

            var now = DateTime.UtcNow;
            var p = Cursor.Position;
            bool wanted = _menuOpen || _resizing || _edgeDrag || _reorder || _pendingDrag || AtEdge(p) || Bounds.Contains(p);
            if (wanted) _lastSeen = now;
            int target = (wanted || (now - _lastSeen).TotalMilliseconds < HideDelayMs) ? 0 : HiddenSlide;
            if (target == _slide) return;

            // Slide a quarter of the remaining distance per tick (at least 3 px): quick but smooth.
            int remaining = target - _slide;
            int step = Math.Max(3, Math.Abs(remaining) / 4);
            if (Math.Abs(remaining) <= step) _slide = target;
            else _slide += remaining > 0 ? step : -step;
            ApplySlide();
        }

        void ApplySlide() { PushSlid(); }

        /// <summary>Shows the part of the bar that is still on its monitor. The hidden part is cut off
        /// rather than moved, so nothing spills onto a neighbouring monitor; a fully hidden bar is
        /// parked far off-screen.</summary>
        void PushSlid()
        {
            if (_frame == null || !IsHandleCreated) return;
            int full = Horizontal ? _shown.Height : _shown.Width;
            int slide = Math.Max(0, Math.Min(_slide, full));
            if (slide >= full)
            {
                if (Left != -32000) Location = new Point(-32000, -32000);
                return;
            }
            Rectangle crop, bounds;
            switch (Edge)
            {
                case AppSettings.EdgeTop:
                    crop = new Rectangle(0, slide, _shown.Width, _shown.Height - slide);
                    bounds = new Rectangle(_shown.X, _shown.Y, _shown.Width, _shown.Height - slide); break;
                case AppSettings.EdgeBottom:
                    crop = new Rectangle(0, 0, _shown.Width, _shown.Height - slide);
                    bounds = new Rectangle(_shown.X, _shown.Y + slide, _shown.Width, _shown.Height - slide); break;
                case AppSettings.EdgeLeft:
                    crop = new Rectangle(slide, 0, _shown.Width - slide, _shown.Height);
                    bounds = new Rectangle(_shown.X, _shown.Y, _shown.Width - slide, _shown.Height); break;
                default:
                    crop = new Rectangle(0, 0, _shown.Width - slide, _shown.Height);
                    bounds = new Rectangle(_shown.X + slide, _shown.Y, _shown.Width - slide, _shown.Height); break;
            }
            if (Bounds != bounds) Bounds = bounds;
            if (slide == 0) { Push(_frame); return; }
            using (var part = _frame.Clone(crop, _frame.PixelFormat)) Push(part);
        }

        /// <summary>Call when zones were added, removed or changed.</summary>
        public void ZonesChanged()
        {
            RebuildCells(S);
            if (IsHandleCreated) { Relayout(); Render(true); }
        }

        void RebuildCells(AppSettings s)
        {
            _cells.Clear();
            foreach (var cfg in s.Widgets)
            {
                var c = new Cell();
                c.Config = cfg;
                c.Tz = App.ZoneFor(cfg);
                _cells.Add(c);
            }
        }

        // ---- interaction ----------------------------------------------------------------------

        bool InResizeBand(Point p)
        {
            int band = (int)Math.Ceiling(ResizeBand * Px);
            switch (Edge)
            {
                case AppSettings.EdgeTop: return p.Y >= Height - band;
                case AppSettings.EdgeBottom: return p.Y < band;
                case AppSettings.EdgeLeft: return p.X >= Width - band;
                default: return p.X < band;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_resizing)
            {
                var p = Cursor.Position;
                int px;
                switch (Edge)
                {
                    case AppSettings.EdgeTop: px = p.Y - Top; break;
                    case AppSettings.EdgeBottom: px = Bottom - p.Y; break;
                    case AppSettings.EdgeLeft: px = p.X - Left; break;
                    default: px = Right - p.X; break;
                }
                int design = (int)Math.Round(px / Px);
                design = Math.Max(Horizontal ? AppSettings.MinRibbonHeight : AppSettings.MinRibbonWidth,
                         Math.Min(Horizontal ? AppSettings.MaxRibbonHeight : AppSettings.MaxRibbonWidth, design));
                if (design != ThicknessDesign)
                {
                    ThicknessDesign = design;
                    Relayout();
                    Render(true);
                }
                return;
            }
            if (_pendingDrag)
            {
                // Decide what a left-drag means: along the bar on a zone = reorder it; away from
                // the bar = move the whole bar to another edge or monitor.
                var p = Cursor.Position;
                int dx = p.X - _dragStart.X, dy = p.Y - _dragStart.Y;
                int along = Horizontal ? dx : dy, across = Horizontal ? dy : dx;
                if (Math.Abs(across) >= EdgeDragThreshold) { _pendingDrag = false; _edgeDrag = true; }
                else if (_dragCfg != null && Math.Abs(along) >= ReorderThreshold) { _pendingDrag = false; _reorder = true; Render(true); }
                else return;
            }
            if (_reorder)
            {
                var over = CellAt(e.Location);
                if (over != null && over.Config != _dragCfg)
                {
                    int to = S.Widgets.IndexOf(over.Config);
                    App.MoveZoneTo(_dragCfg, to);   // rebuilds the cells on every ribbon
                    Render(true);
                }
                return;
            }
            if (_edgeDrag)
            {
                var p = Cursor.Position;
                int dx = p.X - _dragStart.X, dy = p.Y - _dragStart.Y;
                if (dx * dx + dy * dy < EdgeDragThreshold * EdgeDragThreshold) return;
                // The bar follows the mouse: the nearest edge of whichever monitor the mouse is on.
                var under = Screen.FromPoint(p);
                var bounds = under.Bounds;
                int dTop = p.Y - bounds.Top, dBottom = bounds.Bottom - p.Y, dLeft = p.X - bounds.Left, dRight = bounds.Right - p.X;
                string edge = AppSettings.EdgeTop;
                int best = dTop;
                if (dBottom < best) { best = dBottom; edge = AppSettings.EdgeBottom; }
                if (dLeft < best) { best = dLeft; edge = AppSettings.EdgeLeft; }
                if (dRight < best) { best = dRight; edge = AppSettings.EdgeRight; }
                bool movedMonitor = !S.RibbonAllMonitors && under.DeviceName != TargetScreen.DeviceName;
                if (edge != Edge || movedMonitor)
                {
                    S.RibbonEdge = edge;
                    if (movedMonitor)
                    {
                        // Carry this very window over so the drag continues seamlessly; the tray app
                        // then drops any bar left on the old monitor.
                        ScreenDevice = under.DeviceName;
                        S.RibbonMonitor = under.DeviceName;
                    }
                    Relayout();
                    Render(true);
                    if (movedMonitor) App.RibbonMovedToMonitor(under.DeviceName); else App.RibbonEdgeChanged();
                }
                return;
            }
            if (S != null && !S.LockPositions && InResizeBand(e.Location))
                Cursor = Horizontal ? Cursors.SizeNS : Cursors.SizeWE;
            else
                Cursor = Cursors.Default;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || S.LockPositions) return;
            if (InResizeBand(e.Location)) _resizing = true;
            else
            {
                _pendingDrag = true;
                _dragStart = Cursor.Position;
                var cell = CellAt(e.Location);
                _dragCfg = cell != null ? cell.Config : null;
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_resizing || _edgeDrag || _reorder || _pendingDrag)
            {
                bool redraw = _reorder;
                _resizing = false;
                _edgeDrag = false;
                _reorder = false;
                _pendingDrag = false;
                _dragCfg = null;
                if (redraw) Render(true);
                App.SaveSettings();
            }
            if (e.Button == MouseButtons.Right) ShowMenu(e.Location);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button != MouseButtons.Left) return;
            _edgeDrag = false; _resizing = false; _reorder = false; _pendingDrag = false; _dragCfg = null;
            // Double-click a zone: convert a time in that zone. Double-click empty bar: Settings.
            var cell = CellAt(e.Location);
            if (cell != null) ConvertForm.ShowFor(App, cell.Tz, cell.Config.Label, PointToScreen(new Point(cell.Bounds.Left + cell.Bounds.Width / 2, Edge == AppSettings.EdgeTop ? Height : 0)));
            else App.ShowSettings();
        }

        Cell CellAt(Point p)
        {
            foreach (var c in _cells) if (c.Bounds.Contains(p)) return c;
            return null;
        }

        void ShowMenu(Point at)
        {
            var menu = new ContextMenuStrip();
            menu.Font = new Font("Segoe UI", 9.5f);
            var cell = CellAt(at);
            if (cell != null)
            {
                var cfg = cell.Config;
                var header = new ToolStripMenuItem(cfg.Label);
                header.Enabled = false;
                menu.Items.Add(header);
                var change = new ToolStripMenuItem("Change time zone");
                ZoneMenus.Fill(change, delegate(string id, string label) { App.ChangeZone(cfg, id, label); });
                menu.Items.Add(change);
                var rename = new ToolStripMenuItem("Rename...");
                rename.Click += delegate
                {
                    var name = PromptDialog.Show("Rename", "Label shown for this time zone:", cfg.Label);
                    if (name != null) App.RenameZone(cfg, name.Trim());
                };
                menu.Items.Add(rename);
                var remove = new ToolStripMenuItem("Remove " + cfg.Label);
                remove.Click += delegate { App.RemoveZone(cfg); };
                menu.Items.Add(remove);
                menu.Items.Add(new ToolStripSeparator());
            }

            var add = new ToolStripMenuItem("Add time zone");
            ZoneMenus.Fill(add, delegate(string id, string label) { App.AddWidget(id, label); });
            menu.Items.Add(add);

            var position = new ToolStripMenuItem("Ribbon position");
            foreach (var pair in new[] { new[] { "Top", AppSettings.EdgeTop }, new[] { "Bottom", AppSettings.EdgeBottom }, new[] { "Left", AppSettings.EdgeLeft }, new[] { "Right", AppSettings.EdgeRight } })
            {
                var edge = pair[1];
                var item = new ToolStripMenuItem(pair[0]);
                item.Checked = Edge == edge;
                item.Click += delegate { App.SetRibbonEdge(edge); };
                position.DropDownItems.Add(item);
            }
            menu.Items.Add(position);

            var screens = Screen.AllScreens;
            if (screens.Length > 1)
            {
                var monitor = new ToolStripMenuItem("Monitor");
                for (int i = 0; i < screens.Length; i++)
                {
                    var scr = screens[i];
                    var item = new ToolStripMenuItem(TrayApp.DescribeScreen(scr, i));
                    item.Checked = !S.RibbonAllMonitors && scr.DeviceName == TargetScreen.DeviceName;
                    item.Click += delegate { App.SetRibbonMonitor(scr.DeviceName); };
                    monitor.DropDownItems.Add(item);
                }
                var allMon = new ToolStripMenuItem("All monitors");
                allMon.Checked = S.RibbonAllMonitors;
                allMon.Click += delegate { App.SetRibbonAllMonitors(!S.RibbonAllMonitors); };
                monitor.DropDownItems.Add(allMon);
                monitor.DropDownItems.Add(new ToolStripSeparator());
                var dragHint = new ToolStripMenuItem("Tip: drag the bar onto another monitor to move it there");
                dragHint.Enabled = false;
                monitor.DropDownItems.Add(dragHint);
                menu.Items.Add(monitor);
            }

            var overlay = new ToolStripMenuItem("Overlay the screen (don't reserve space)");
            overlay.Checked = !S.RibbonReserveSpace;
            overlay.Click += delegate { S.RibbonReserveSpace = false; App.ApplyAll(); };
            menu.Items.Add(overlay);
            var reserve = new ToolStripMenuItem("Reserve screen space (like the taskbar)");
            reserve.Checked = S.RibbonReserveSpace;
            reserve.Click += delegate { S.RibbonReserveSpace = true; App.ApplyAll(); };
            menu.Items.Add(reserve);
            var codes = new ToolStripMenuItem("Show 3-letter city codes (NYC, TPE)");
            codes.Checked = S.RibbonCityCodes;
            codes.Click += delegate { S.RibbonCityCodes = !S.RibbonCityCodes; App.ApplyAll(); };
            menu.Items.Add(codes);
            var autoHide = new ToolStripMenuItem("Auto-hide (touch the screen edge to show)");
            autoHide.Checked = S.RibbonAutoHide;
            autoHide.Click += delegate { S.RibbonAutoHide = !S.RibbonAutoHide; App.ApplyAll(); };
            menu.Items.Add(autoHide);

            var floating = new ToolStripMenuItem("Switch to floating widgets");
            floating.Click += delegate { App.SetLayoutMode(AppSettings.LayoutFloating); };
            menu.Items.Add(floating);

            var lockItem = new ToolStripMenuItem("Lock position and size");
            lockItem.Checked = S.LockPositions;
            lockItem.Click += delegate { S.LockPositions = !S.LockPositions; App.ApplyAll(); };
            menu.Items.Add(lockItem);
            menu.Items.Add(new ToolStripSeparator());

            var settings = new ToolStripMenuItem("Settings...");
            settings.Click += delegate { App.ShowSettings(); };
            menu.Items.Add(settings);
            menu.Items.Add(new ToolStripSeparator());
            var exit = new ToolStripMenuItem("Exit");
            exit.Click += delegate { App.Exit(); };
            menu.Items.Add(exit);

            _menuOpen = true;
            menu.Closed += delegate { _menuOpen = false; _lastSeen = DateTime.UtcNow; };
            menu.Show(this, at);
        }

        // ---- layout ---------------------------------------------------------------------------

        void DisposeFonts()
        {
            foreach (var f in new[] { _fLabel, _fTime, _fAmPm, _fDay, _fDate, _fSmall, _fGlyph, _fTiny, _fMarket }) if (f != null) f.Dispose();
            _fLabel = _fTime = _fAmPm = _fDay = _fDate = _fSmall = _fGlyph = _fTiny = _fMarket = null;
        }

        /// <summary>Splits "UTC+8" / "UTC−7" / "UTC+5:30" / "UTC" into the "UTC" word and the difference.</summary>
        static void SplitOffset(string offset, out string utc, out string diff)
        {
            if (string.IsNullOrEmpty(offset)) { utc = ""; diff = ""; return; }
            utc = "UTC";
            diff = offset.Length > 3 ? offset.Substring(3) : "±0";
        }

        bool AnyMarket()
        {
            foreach (var c in _cells) if (c.Market.Length > 0) return true;
            return false;
        }

        /// <summary>The second line of a compact cell: "● NYSE · closes 4h 17m".</summary>
        static string MarketLine(Cell c) { return (c.MarketOpen ? "● " : "○ ") + c.Market + " · " + c.MarketText; }

        /// <summary>Translucent green laid over a cell while its market is trading.</summary>
        /// <summary>Text colour for a cell's market line: by phase, else the given fallback.</summary>
        static Color MarketColor(Cell c, Theme t, Color fallback)
        {
            return c.MarketPhase != 0 ? MarketStatus.PhaseText(c.MarketPhase, t.DarkPanel) : fallback;
        }

        /// <summary>Width of the small stacked column: sun/moon glyph over "UTC" over the difference.</summary>
        float InfoColumnWidth(Graphics g, Cell c, StringFormat sf)
        {
            string utc, diff;
            SplitOffset(c.Offset, out utc, out diff);
            return Math.Max(W(g, c.Glyph, _fGlyph, sf), Math.Max(W(g, utc, _fTiny, sf), W(g, diff, _fTiny, sf)));
        }

        void CreateFonts(float fit, bool singleRow)
        {
            DisposeFonts();
            float k = Px * fit;
            // Horizontal cells: the city is as prominent as the time (same size, semibold), with the
            // day/date stacked in small text between them. Vertical ribbons keep the three-row clock.
            float timePt = singleRow ? 13f : Horizontal ? 16.5f : 19f;
            _fTime = new Font("Segoe UI", timePt * k, FontStyle.Regular, GraphicsUnit.Point);
            _fLabel = Horizontal ? new Font("Segoe UI Semibold", timePt * k, FontStyle.Regular, GraphicsUnit.Point)
                                 : new Font("Segoe UI", 9f * k, FontStyle.Bold, GraphicsUnit.Point);
            _fAmPm = new Font("Segoe UI", 8.5f * k, FontStyle.Bold, GraphicsUnit.Point);
            _fDay = new Font("Segoe UI", 8.5f * k, FontStyle.Bold, GraphicsUnit.Point);
            _fDate = new Font("Segoe UI", 8.5f * k, FontStyle.Regular, GraphicsUnit.Point);
            _fSmall = new Font("Segoe UI", 8f * k, FontStyle.Regular, GraphicsUnit.Point);
            _fGlyph = new Font("Segoe UI Symbol", 8f * k, FontStyle.Regular, GraphicsUnit.Point);
            _fTiny = new Font("Segoe UI", 6.5f * k, FontStyle.Regular, GraphicsUnit.Point);
            // The market line keeps a readable size however much the top row has to shrink to fit.
            _fMarket = new Font("Segoe UI", Math.Max(11f * Px, 11f * k), FontStyle.Regular, GraphicsUnit.Point);
        }

        static StringFormat Typographic()
        {
            var sf = (StringFormat)StringFormat.GenericTypographic.Clone();
            sf.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces;
            return sf;
        }

        static float W(Graphics g, string s, Font f, StringFormat sf)
        {
            return string.IsNullOrEmpty(s) ? 0 : g.MeasureString(s, f, PointF.Empty, sf).Width;
        }

        void CaptureCells()
        {
            var utc = DateTime.UtcNow;
            var inv = CultureInfo.InvariantCulture;
            foreach (var c in _cells)
            {
                var z = TimeZoneInfo.ConvertTimeFromUtc(utc, c.Tz);
                c.Label = S.RibbonCityCodes ? ZoneCatalog.CodeFor(c.Config.TimeZoneId, c.Config.Label) : c.Config.Label;
                if (S.Use24Hour) { c.Time = z.ToString(S.ShowSeconds ? "HH:mm:ss" : "HH:mm", inv); c.AmPm = ""; }
                else { c.Time = z.ToString(S.ShowSeconds ? "h:mm:ss" : "h:mm", inv); c.AmPm = z.Hour < 12 ? "AM" : "PM"; }
                c.Offset = S.ShowUtcOffset ? ZoneCatalog.FormatOffset(c.Tz.GetUtcOffset(utc)) : "";
                c.Day = z.ToString(_singleRow ? "ddd" : "dddd", CultureInfo.CurrentCulture);
                c.Date = z.ToString("MMM d", CultureInfo.CurrentCulture);
                int diff = (z.Date - DateTime.Now.Date).Days;
                if (diff != 0) c.Date += (diff > 0 ? " (+" : " (−") + Math.Abs(diff) + ")";
                c.Glyph = (z.Hour >= 6 && z.Hour < 18) ? "☀" : "☽";
                c.Market = ""; c.MarketText = ""; c.MarketOpen = false; c.MarketPhase = 0;
                if (S.ShowMarkets)
                {
                    var st = MarketCatalog.GetStatus(MarketCatalog.ForWidget(c.Config.TimeZoneId, c.Config.Label), utc);
                    if (st.Known)
                    {
                        c.Market = st.Market.Name;
                        c.MarketOpen = st.Open;
                        c.MarketPhase = st.Phase;
                        c.MarketText = (st.Open ? "closes " : st.Break ? "break · opens " : "opens ") + st.Countdown;
                    }
                }
            }
        }

        string Key()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(Edge).Append('|').Append(Width).Append('x').Append(Height).Append('|');
            foreach (var c in _cells) sb.Append(c.Label).Append(c.Time).Append(c.AmPm).Append(c.Offset).Append(c.Day).Append(c.Date).Append(c.Glyph).Append(c.Market).Append(c.MarketText).Append(c.MarketPhase).Append('|');
            return sb.ToString();
        }

        Rectangle TargetBounds()
        {
            var screen = TargetScreen;
            int thickness = (int)Math.Round(ThicknessDesign * Px);
            if (S.RibbonReserveSpace && !S.RibbonAutoHide && _appBar != null)
            {
                int abe = Edge == AppSettings.EdgeTop ? AppBar.ABE_TOP : Edge == AppSettings.EdgeBottom ? AppBar.ABE_BOTTOM
                        : Edge == AppSettings.EdgeLeft ? AppBar.ABE_LEFT : AppBar.ABE_RIGHT;
                return _appBar.SetPosition(abe, thickness, screen.Bounds);
            }
            if (_appBar != null)
            {
                // Overlay mode reserves nothing. Registering and unregistering once makes the
                // shell re-check its appbar list, which drops any reservation left behind by an
                // instance that was killed while it still had space reserved.
                if (!_nudgedShell) { _appBar.Register(); _nudgedShell = true; }
                _appBar.Unregister();
            }
            var area = screen.WorkingArea;
            switch (Edge)
            {
                case AppSettings.EdgeTop: return new Rectangle(area.Left, area.Top, area.Width, thickness);
                case AppSettings.EdgeBottom: return new Rectangle(area.Left, area.Bottom - thickness, area.Width, thickness);
                case AppSettings.EdgeLeft: return new Rectangle(area.Left, area.Top, thickness, area.Height);
                default: return new Rectangle(area.Right - thickness, area.Top, thickness, area.Height);
            }
        }

        protected override void Relayout()
        {
            if (S == null) return;
            _shown = TargetBounds();
            if (_slide > HiddenSlide) _slide = HiddenSlide;
            if (_slide == 0 && Bounds != _shown) Bounds = _shown;

            // Pick font sizes that fit the ribbon's thickness (and, horizontally, its length).
            float px = Px;
            _singleRow = Horizontal && _shown.Height < 36 * px;
            CaptureCells();
            _marketRow = Horizontal && !_singleRow && S.ShowMarkets && AnyMarket();
            CreateFonts(1f, _singleRow);
            using (var bmp = new Bitmap(1, 1))
            using (var g = Graphics.FromImage(bmp))
            using (var sf = Typographic())
            {
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                float fit;
                if (Horizontal)
                {
                    // Scale the text to the ribbon's height: shrink to fit a thin bar, grow (up to
                    // 1.8x) to fill a tall one.
                    float columnH = _fGlyph.GetHeight(g) + 2 * _fTiny.GetHeight(g) - 6 * px;   // tight leading
                    float needH = _singleRow ? _fTime.GetHeight(g) + 6 * px
                                             : Math.Max(columnH, Math.Max(_fTime.GetHeight(g), _fDay.GetHeight(g) + _fDate.GetHeight(g) + 2 * px))
                                               + (_marketRow ? _fMarket.GetHeight(g) + 2 * px : 0) + 8 * px;
                    fit = Math.Min(1.8f, (_shown.Height - 2 * px) / needH);
                    // Shrink the top row until every cell fits the bar's length. The market line
                    // keeps its own size (see CreateFonts), so only clocks and dates get smaller.
                    for (int step = 0; step < 40; step++)
                    {
                        CreateFonts(Math.Max(0.4f, fit), _singleRow);
                        float total = 0;
                        foreach (var c in _cells) total += CellWidth(g, c, sf) + 12 * px;
                        if (total <= _shown.Width - 20 * px || fit <= 0.4f) break;
                        fit -= 0.04f;
                    }
                }
                else
                {
                    float widest = 1;
                    foreach (var c in _cells) widest = Math.Max(widest, CellWidth(g, c, sf));
                    fit = Math.Min(1f, (_shown.Width - 24 * px) / widest);
                }
                _fit = Math.Max(0.4f, fit);
            }
            CreateFonts(_fit, _singleRow);
        }

        float CellWidth(Graphics g, Cell c, StringFormat sf)
        {
            float px = Px;
            if (_singleRow)
                return W(g, c.Label, _fLabel, sf) + 8 * px + W(g, c.Time, _fTime, sf) + (c.AmPm.Length > 0 ? 3 * px + W(g, c.AmPm, _fAmPm, sf) : 0)
                     + 8 * px + W(g, c.Day + " · " + c.Date, _fDate, sf)
                     + (c.Market.Length > 0 ? 8 * px + W(g, c.Market + " " + c.MarketText, _fTiny, sf) : 0);
            float row1 = W(g, c.Label, _fLabel, sf) + 8 * px + W(g, c.Glyph, _fGlyph, sf) + 3 * px + W(g, c.Offset, _fSmall, sf);
            float row2 = W(g, S.ShowSeconds ? "88:88:88" : "88:88", _fTime, sf) + (c.AmPm.Length > 0 || !S.Use24Hour ? 4 * px + W(g, "PM", _fAmPm, sf) : 0);
            float row3 = W(g, c.Day, _fDay, sf) + W(g, " · " + c.Date, _fDate, sf);
            if (Horizontal)
            {
                // Compact cell: city | [glyph / UTC / ±n] column | day over date | time.
                float stackW = Math.Max(W(g, c.Day, _fDay, sf), W(g, c.Date, _fDate, sf));
                float mainW = W(g, c.Label, _fLabel, sf) + 7 * px + InfoColumnWidth(g, c, sf) + 10 * px + stackW + 10 * px + row2;
                float marketRowW = _marketRow && c.Market.Length > 0 ? W(g, MarketLine(c), _fMarket, sf) : 0;
                return Math.Max(mainW, marketRowW);
            }
            float row4 = c.Market.Length > 0 ? W(g, "● " + c.Market + " · " + c.MarketText, _fSmall, sf) : 0;
            return Math.Max(Math.Max(row1, row4), Math.Max(row2, row3));
        }

        // ---- rendering ------------------------------------------------------------------------

        void Tick()
        {
            int ms = DateTime.Now.Millisecond;
            _timer.Interval = Math.Max(50, 1000 - ms + 10);
            if (!Visible || S == null) return;
            Render(false);
        }

        protected override void Render(bool force)
        {
            if (!IsHandleCreated || S == null || _fTime == null) return;
            CaptureCells();
            var key = Key();
            if (!force && key == _lastKey) return;
            _lastKey = key;
            int w = Math.Max(1, _shown.Width > 0 ? _shown.Width : Width), h = Math.Max(1, _shown.Height > 0 ? _shown.Height : Height);
            if (_frame != null) _frame.Dispose();
            _frame = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(_frame))
            {
                g.Clear(Color.Transparent);
                Draw(g, w, h);
            }
            PushSlid();
        }

        void Draw(Graphics g, int w, int h)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            float px = Px;
            var t = T;
            var muted = Color.FromArgb(t.DarkPanel ? 165 : 150, t.Text);
            var sepColor = Color.FromArgb(t.DarkPanel ? 40 : 45, t.Text);

            using (var sf = Typographic())
            using (var bg = new SolidBrush(t.Background))
            using (var text = new SolidBrush(t.Text))
            using (var mutedBrush = new SolidBrush(muted))
            using (var accent = new SolidBrush(t.Accent))
            using (var sep = new Pen(sepColor, 1f))
            using (var innerEdge = new Pen(t.Accent, 2 * px))
            {
                g.FillRectangle(bg, 0, 0, w, h);
                // Accent line along the inner edge (the side facing the desktop).
                switch (Edge)
                {
                    case AppSettings.EdgeTop: g.DrawLine(innerEdge, 0, h - px, w, h - px); break;
                    case AppSettings.EdgeBottom: g.DrawLine(innerEdge, 0, px, w, px); break;
                    case AppSettings.EdgeLeft: g.DrawLine(innerEdge, w - px, 0, w - px, h); break;
                    default: g.DrawLine(innerEdge, px, 0, px, h); break;
                }

                float labelH = _fLabel.GetHeight(g), timeH = _fTime.GetHeight(g), dateH = _fDate.GetHeight(g);
                float ampmH = _fAmPm.GetHeight(g), smallH = _fSmall.GetHeight(g), glyphH = _fGlyph.GetHeight(g);

                if (Horizontal)
                {
                    float gap = 12 * px;
                    var widths = new float[_cells.Count];
                    float total = 0;
                    for (int i = 0; i < _cells.Count; i++) { widths[i] = CellWidth(g, _cells[i], sf); total += widths[i]; }
                    total += gap * 2 * Math.Max(0, _cells.Count - 1);
                    float x = Math.Max(10 * px, (w - total) / 2);
                    for (int i = 0; i < _cells.Count; i++)
                    {
                        var c = _cells[i];
                        if (i > 0)
                        {
                            g.DrawLine(sep, x - gap, h * 0.2f, x - gap, h * 0.8f);
                        }
                        c.Bounds = new Rectangle((int)(x - gap), 0, (int)(widths[i] + 2 * gap), h);
                        if (c.MarketPhase != 0) using (var tint = new SolidBrush(MarketStatus.PhaseTint(c.MarketPhase, t.DarkPanel))) g.FillRectangle(tint, c.Bounds);
                        if (_reorder && c.Config == _dragCfg) using (var hl = new SolidBrush(Color.FromArgb(55, t.Accent))) g.FillRectangle(hl, c.Bounds);
                        if (_singleRow)
                        {
                            float cy = h / 2f;
                            float cx = x;
                            g.DrawString(c.Label, _fLabel, text, cx, cy - labelH / 2, sf);
                            cx += W(g, c.Label, _fLabel, sf) + 8 * px;
                            g.DrawString(c.Time, _fTime, accent, cx, cy - timeH / 2, sf);
                            cx += W(g, c.Time, _fTime, sf);
                            if (c.AmPm.Length > 0)
                            {
                                g.DrawString(c.AmPm, _fAmPm, text, cx + 3 * px, cy + timeH / 2 - ampmH - 2 * px, sf);
                                cx += 3 * px + W(g, c.AmPm, _fAmPm, sf);
                            }
                            g.DrawString(c.Day + " · " + c.Date, _fDate, mutedBrush, cx + 8 * px, cy - dateH / 2, sf);
                            if (c.Market.Length > 0)
                            {
                                cx += 8 * px + W(g, c.Day + " · " + c.Date, _fDate, sf) + 8 * px;
                                using (var mb = new SolidBrush(MarketColor(c, t, muted)))
                                    g.DrawString(c.Market + " " + c.MarketText, _fTiny, mb, cx, cy - _fTiny.GetHeight(g) / 2, sf);
                            }
                        }
                        else
                        {
                            // City (as big as the time), then a narrow column with the sun/moon
                            // glyph over "UTC" over the difference, then the day over the date in
                            // small text, then the time.
                            float cx = x;
                            float dayH = _fDay.GetHeight(g);
                            string utc, diff;
                            SplitOffset(c.Offset, out utc, out diff);
                            float tinyH = _fTiny.GetHeight(g);
                            float colW = InfoColumnWidth(g, c, sf);
                            float colH = utc.Length > 0 ? glyphH + 2 * tinyH - 6 * px : glyphH;
                            float stackH = dayH + dateH + 2 * px;
                            // Row 1 (city, column, date stack, time) centred as a block; row 2 is the market.
                            float mainH = Math.Max(Math.Max(labelH, timeH), Math.Max(colH, stackH));
                            float marketH = _marketRow ? _fMarket.GetHeight(g) + 2 * px : 0;
                            float top = (h - (mainH + marketH)) / 2;
                            float rowCy = top + mainH / 2;
                            g.DrawString(c.Label, _fLabel, text, cx, rowCy - labelH / 2, sf);
                            cx += W(g, c.Label, _fLabel, sf) + 7 * px;

                            float colY = rowCy - colH / 2;
                            float colMid = cx + colW / 2;
                            g.DrawString(c.Glyph, _fGlyph, mutedBrush, colMid - W(g, c.Glyph, _fGlyph, sf) / 2, colY, sf);
                            if (utc.Length > 0)
                            {
                                g.DrawString(utc, _fTiny, mutedBrush, colMid - W(g, utc, _fTiny, sf) / 2, colY + glyphH - 3 * px, sf);
                                g.DrawString(diff, _fTiny, mutedBrush, colMid - W(g, diff, _fTiny, sf) / 2, colY + glyphH + tinyH - 6 * px, sf);
                            }
                            cx += colW + 10 * px;

                            float stackW = Math.Max(W(g, c.Day, _fDay, sf), W(g, c.Date, _fDate, sf));
                            float sy = rowCy - stackH / 2;
                            g.DrawString(c.Day, _fDay, text, cx, sy, sf);
                            g.DrawString(c.Date, _fDate, mutedBrush, cx, sy + dayH + 2 * px, sf);
                            cx += stackW + 10 * px;
                            float ty = rowCy - timeH / 2;
                            g.DrawString(c.Time, _fTime, accent, cx - px, ty, sf);
                            if (c.AmPm.Length > 0)
                                g.DrawString(c.AmPm, _fAmPm, text, cx + W(g, c.Time, _fTime, sf) + 4 * px, ty + timeH - ampmH - 4 * px, sf);
                            if (_marketRow && c.Market.Length > 0)
                            {
                                // Row 2: "● NYSE · closes 4h 17m" – green while the market trades.
                                float my = top + mainH + 2 * px;
                                float lineW = W(g, MarketLine(c), _fMarket, sf);
                                float mx = x + (widths[i] - lineW) / 2;   // centred under the zone
                                float dotW = W(g, "● ", _fMarket, sf);
                                using (var dot = new SolidBrush(MarketColor(c, t, muted)))
                                using (var mb = new SolidBrush(MarketColor(c, t, t.Text)))
                                {
                                    g.DrawString(c.MarketOpen ? "●" : "○", _fMarket, dot, mx, my, sf);
                                    g.DrawString(c.Market + " · " + c.MarketText, _fMarket, mb, mx + dotW, my, sf);
                                }
                            }
                        }
                        x += widths[i] + 2 * gap;
                    }
                }
                else
                {
                    float padX = 12 * px, padY = 8 * px;
                    float y = 10 * px;
                    float contentH = labelH + timeH + dateH + 2 * px;
                    for (int i = 0; i < _cells.Count; i++)
                    {
                        var c = _cells[i];
                        if (i > 0) g.DrawLine(sep, padX, y, w - padX, y);
                        float cy = y + padY;
                        float cellH = contentH + (c.Market.Length > 0 ? smallH + px : 0);
                        c.Bounds = new Rectangle(0, (int)y, w, (int)(cellH + 2 * padY));
                        if (c.MarketPhase != 0) using (var tint = new SolidBrush(MarketStatus.PhaseTint(c.MarketPhase, t.DarkPanel))) g.FillRectangle(tint, c.Bounds);
                        if (_reorder && c.Config == _dragCfg) using (var hl = new SolidBrush(Color.FromArgb(55, t.Accent))) g.FillRectangle(hl, c.Bounds);
                        g.DrawString(c.Label, _fLabel, text, padX, cy, sf);
                        float offW = W(g, c.Offset, _fSmall, sf);
                        float glyphW = W(g, c.Glyph, _fGlyph, sf);
                        float rx = w - padX - offW;
                        g.DrawString(c.Offset, _fSmall, mutedBrush, rx, cy + (labelH - smallH) / 2, sf);
                        g.DrawString(c.Glyph, _fGlyph, mutedBrush, rx - glyphW - (offW > 0 ? 3 * px : 0), cy + (labelH - glyphH) / 2, sf);
                        cy += labelH + px;
                        g.DrawString(c.Time, _fTime, accent, padX - px, cy, sf);
                        if (c.AmPm.Length > 0)
                            g.DrawString(c.AmPm, _fAmPm, text, padX + W(g, c.Time, _fTime, sf) + 4 * px, cy + timeH - ampmH - 4 * px, sf);
                        cy += timeH + px;
                        g.DrawString(c.Day, _fDay, text, padX, cy, sf);
                        g.DrawString(" · " + c.Date, _fDate, mutedBrush, padX + W(g, c.Day, _fDay, sf), cy, sf);
                        if (c.Market.Length > 0)
                        {
                            cy += dateH + px;
                            using (var mb = new SolidBrush(MarketColor(c, t, muted)))
                                g.DrawString((c.MarketOpen ? "● " : "○ ") + c.Market + " · " + c.MarketText, _fSmall, mb, padX, cy, sf);
                        }
                        y += cellH + 2 * padY;
                    }
                }
            }
        }
    }

    /// <summary>Builds the shared "pick a time zone" submenus (popular cities by region, local, and every Windows zone).</summary>
    public static class ZoneMenus
    {
        public delegate void ZonePicked(string windowsId, string label);

        public static void Fill(ToolStripMenuItem parent, ZonePicked onPick)
        {
            var find = new ToolStripMenuItem("Find a city...");
            find.Font = new Font(find.Font, FontStyle.Bold);
            find.Click += delegate { string id, label; if (CityPickerForm.Show(null, out id, out label)) onPick(id, label); };
            parent.DropDownItems.Add(find);
            parent.DropDownItems.Add(new ToolStripSeparator());
            foreach (var region in ZoneCatalog.Regions)
            {
                var regionItem = new ToolStripMenuItem(region.Name);
                foreach (var zone in region.Zones)
                {
                    if (!ZoneCatalog.Exists(zone.WindowsId)) continue;
                    var z = zone;
                    var item = new ToolStripMenuItem(zone.City);
                    item.Click += delegate { onPick(z.WindowsId, z.City); };
                    regionItem.DropDownItems.Add(item);
                }
                parent.DropDownItems.Add(regionItem);
            }
            parent.DropDownItems.Add(new ToolStripSeparator());
            var local = new ToolStripMenuItem("Local time");
            local.Click += delegate { onPick(TimeZoneInfo.Local.Id, ZoneCatalog.LocalCity()); };
            parent.DropDownItems.Add(local);
            var all = new ToolStripMenuItem("All time zones");
            all.DropDownItems.Add(new ToolStripMenuItem("Loading..."));
            bool built = false;
            all.DropDownOpening += delegate
            {
                if (built) return;
                built = true;
                all.DropDownItems.Clear();
                foreach (var tz in ZoneCatalog.All)
                {
                    var zone = tz;
                    var item = new ToolStripMenuItem(tz.DisplayName);
                    item.Click += delegate { onPick(zone.Id, ZoneCatalog.ShortDisplayName(zone)); };
                    all.DropDownItems.Add(item);
                }
            };
            parent.DropDownItems.Add(all);
        }
    }
}

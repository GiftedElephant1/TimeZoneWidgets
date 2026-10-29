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
        }

        readonly Timer _timer;
        readonly List<Cell> _cells = new List<Cell>();
        string _lastKey;
        AppBar _appBar;

        Font _fLabel, _fTime, _fAmPm, _fDay, _fDate, _fSmall, _fGlyph;
        float _fit = 1f;
        bool _singleRow;

        bool _resizing, _edgeDrag;
        Point _dragStart;

        public RibbonForm(TrayApp app, AppSettings settings, Theme theme)
            : base(app)
        {
            Text = "Time zone ribbon";
            Size = new Size(600, 80);
            _timer = new Timer();
            _timer.Interval = 1000;
            _timer.Tick += delegate { Tick(); };
            ApplySettings(settings, theme);
        }

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
            if (_appBar != null) { _appBar.Unregister(); _appBar = null; }
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
            base.ApplySettings(settings, theme);
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
            if (_edgeDrag)
            {
                var p = Cursor.Position;
                int dx = p.X - _dragStart.X, dy = p.Y - _dragStart.Y;
                if (dx * dx + dy * dy < EdgeDragThreshold * EdgeDragThreshold) return;
                var bounds = Screen.PrimaryScreen.Bounds;
                int dTop = p.Y - bounds.Top, dBottom = bounds.Bottom - p.Y, dLeft = p.X - bounds.Left, dRight = bounds.Right - p.X;
                string edge = AppSettings.EdgeTop;
                int best = dTop;
                if (dBottom < best) { best = dBottom; edge = AppSettings.EdgeBottom; }
                if (dLeft < best) { best = dLeft; edge = AppSettings.EdgeLeft; }
                if (dRight < best) { best = dRight; edge = AppSettings.EdgeRight; }
                if (edge != Edge)
                {
                    S.RibbonEdge = edge;
                    Relayout();
                    Render(true);
                    App.RibbonEdgeChanged();
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
            else { _edgeDrag = true; _dragStart = Cursor.Position; }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_resizing || _edgeDrag)
            {
                _resizing = false;
                _edgeDrag = false;
                App.SaveSettings();
            }
            if (e.Button == MouseButtons.Right) ShowMenu(e.Location);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button == MouseButtons.Left) { _edgeDrag = false; _resizing = false; App.ShowSettings(); }
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

            menu.Show(this, at);
        }

        // ---- layout ---------------------------------------------------------------------------

        void DisposeFonts()
        {
            foreach (var f in new[] { _fLabel, _fTime, _fAmPm, _fDay, _fDate, _fSmall, _fGlyph }) if (f != null) f.Dispose();
            _fLabel = _fTime = _fAmPm = _fDay = _fDate = _fSmall = _fGlyph = null;
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
            }
        }

        string Key()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(Edge).Append('|').Append(Width).Append('x').Append(Height).Append('|');
            foreach (var c in _cells) sb.Append(c.Label).Append(c.Time).Append(c.AmPm).Append(c.Offset).Append(c.Day).Append(c.Date).Append(c.Glyph).Append('|');
            return sb.ToString();
        }

        Rectangle TargetBounds()
        {
            var screen = Screen.PrimaryScreen;
            int thickness = (int)Math.Round(ThicknessDesign * Px);
            if (S.RibbonReserveSpace && _appBar != null)
            {
                int abe = Edge == AppSettings.EdgeTop ? AppBar.ABE_TOP : Edge == AppSettings.EdgeBottom ? AppBar.ABE_BOTTOM
                        : Edge == AppSettings.EdgeLeft ? AppBar.ABE_LEFT : AppBar.ABE_RIGHT;
                return _appBar.SetPosition(abe, thickness, screen.Bounds);
            }
            if (_appBar != null) _appBar.Unregister();
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
            var target = TargetBounds();
            if (Bounds != target) Bounds = target;

            // Pick font sizes that fit the ribbon's thickness (and, horizontally, its length).
            float px = Px;
            _singleRow = Horizontal && Height < 36 * px;
            CaptureCells();
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
                    float needH = _singleRow ? _fTime.GetHeight(g) + 6 * px
                                             : Math.Max(_fTime.GetHeight(g), _fDay.GetHeight(g) + _fDate.GetHeight(g) + 2 * px) + 8 * px;
                    fit = Math.Min(1.8f, (Height - 2 * px) / needH);
                    // Also shrink if the cells would overflow the screen width.
                    float total = 0;
                    foreach (var c in _cells) total += CellWidth(g, c, sf) + 12 * px;
                    if (total > Width - 20 * px) fit = Math.Min(fit, (Width - 20 * px) / total);
                }
                else
                {
                    float widest = 1;
                    foreach (var c in _cells) widest = Math.Max(widest, CellWidth(g, c, sf));
                    fit = Math.Min(1f, (Width - 24 * px) / widest);
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
                     + 8 * px + W(g, c.Day + " · " + c.Date, _fDate, sf);
            float row1 = W(g, c.Label, _fLabel, sf) + 8 * px + W(g, c.Glyph, _fGlyph, sf) + 3 * px + W(g, c.Offset, _fSmall, sf);
            float row2 = W(g, S.ShowSeconds ? "88:88:88" : "88:88", _fTime, sf) + (c.AmPm.Length > 0 || !S.Use24Hour ? 4 * px + W(g, "PM", _fAmPm, sf) : 0);
            float row3 = W(g, c.Day, _fDay, sf) + W(g, " · " + c.Date, _fDate, sf);
            if (Horizontal)
            {
                // Compact cell: city (+ sun/moon and offset)  |  day over date  |  time.
                float stackW = Math.Max(W(g, c.Day, _fDay, sf), W(g, c.Date, _fDate, sf));
                return row1 + 10 * px + stackW + 10 * px + row2;
            }
            return Math.Max(row1, Math.Max(row2, row3));
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
            int w = Math.Max(1, Width), h = Math.Max(1, Height);
            using (var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    Draw(g, w, h);
                }
                Push(bmp);
            }
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
                        }
                        else
                        {
                            // City (as big as the time, with sun/moon and UTC offset beside it),
                            // then the day over the date in small text, then the time.
                            float cx = x;
                            float dayH = _fDay.GetHeight(g);
                            g.DrawString(c.Label, _fLabel, text, cx, (h - labelH) / 2, sf);
                            cx += W(g, c.Label, _fLabel, sf) + 8 * px;
                            float midSmall = h / 2f;
                            g.DrawString(c.Glyph, _fGlyph, mutedBrush, cx, midSmall - glyphH / 2, sf);
                            cx += W(g, c.Glyph, _fGlyph, sf) + 3 * px;
                            g.DrawString(c.Offset, _fSmall, mutedBrush, cx, midSmall - smallH / 2, sf);
                            cx += W(g, c.Offset, _fSmall, sf) + 10 * px;

                            float stackW = Math.Max(W(g, c.Day, _fDay, sf), W(g, c.Date, _fDate, sf));
                            float stackH = dayH + dateH + 2 * px;
                            float sy = (h - stackH) / 2;
                            g.DrawString(c.Day, _fDay, text, cx, sy, sf);
                            g.DrawString(c.Date, _fDate, mutedBrush, cx, sy + dayH + 2 * px, sf);
                            cx += stackW + 10 * px;

                            float ty = (h - timeH) / 2;
                            g.DrawString(c.Time, _fTime, accent, cx - px, ty, sf);
                            if (c.AmPm.Length > 0)
                                g.DrawString(c.AmPm, _fAmPm, text, cx + W(g, c.Time, _fTime, sf) + 4 * px, ty + timeH - ampmH - 4 * px, sf);
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
                        c.Bounds = new Rectangle(0, (int)y, w, (int)(contentH + 2 * padY));
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
                        y += contentH + 2 * padY;
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

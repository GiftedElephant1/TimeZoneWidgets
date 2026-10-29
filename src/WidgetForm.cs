using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Windows.Forms;

namespace TimeZoneWidgets
{
    /// <summary>One floating clock showing a time zone's day, date and time.</summary>
    public class WidgetForm : LayeredWindow
    {
        class Snapshot
        {
            public string Label, Time, AmPm, Offset, Day, Date, Badge, Glyph;
            public string Market = "", MarketText = "";   // stock exchange + countdown (empty when unknown/off)
            public bool MarketOpen;
            public int MarketPhase;      // see MarketStatus.Phase
            public string Key;
        }

        public readonly WidgetConfig Config;
        TimeZoneInfo _tz;
        readonly Timer _timer;
        string _lastKey;

        Font _fLabel, _fSmall, _fTime, _fAmPm, _fDay, _fDate, _fGlyph;
        ContextMenuStrip _menu;
        ToolStripMenuItem _allZonesItem, _lockItem;
        bool _allZonesBuilt;

        public WidgetForm(TrayApp app, WidgetConfig config, AppSettings settings, Theme theme)
            : base(app)
        {
            Config = config;
            _tz = ZoneCatalog.Find(config.TimeZoneId);
            if (string.IsNullOrEmpty(Config.Label)) Config.Label = ZoneCatalog.LabelFor(config.TimeZoneId);

            Text = "Time zone widget: " + Config.Label;
            Location = new Point(config.X, config.Y);
            Size = new Size(230, 118);

            BuildMenu();
            ContextMenuStrip = _menu;

            _timer = new Timer();
            _timer.Interval = 1000;
            _timer.Tick += delegate { Tick(); };

            ApplySettings(settings, theme);
        }

        public TimeZoneInfo TimeZone { get { return _tz; } }

        public override int SavedX { get { return Config.X; } set { Config.X = value; } }
        public override int SavedY { get { return Config.Y; } set { Config.Y = value; } }
        protected override int OpacityPercent { get { return S == null ? 100 : S.Opacity; } }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            _timer.Start();
        }

        // ---- settings -------------------------------------------------------------------------

        public void SetTimeZone(string windowsId, string label)
        {
            Config.TimeZoneId = windowsId;
            Config.Label = string.IsNullOrEmpty(label) ? ZoneCatalog.LabelFor(windowsId) : label;
            _tz = ZoneCatalog.Find(windowsId);
            Text = "Time zone widget: " + Config.Label;
            Relayout();
            Render(true);
            App.SaveSettings();
            App.ZonesChanged();
        }

        public void Rename(string label)
        {
            Config.Label = string.IsNullOrEmpty(label) ? ZoneCatalog.LabelFor(Config.TimeZoneId) : label;
            Text = "Time zone widget: " + Config.Label;
            Relayout();
            Render(true);
            App.SaveSettings();
        }

        // ---- interaction ----------------------------------------------------------------------

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left) BeginMove(e.Location);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            ContinueMove();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            EndMove();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button == MouseButtons.Left)
            {
                EndMove();
                // Convert a time in this zone (Settings stays on the right-click menu and the tray icon).
                ConvertForm.ShowFor(App, _tz, Config.Label, new Point(Left + Width / 2, Bottom));
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _timer.Stop();
            _timer.Dispose();
            DisposeFonts();
            base.OnFormClosed(e);
        }

        void BuildMenu()
        {
            _menu = new ContextMenuStrip();
            _menu.Font = new Font("Segoe UI", 9.5f);

            var change = new ToolStripMenuItem("Change time zone");
            var find = new ToolStripMenuItem("Find a city...");
            find.Font = new Font(find.Font, FontStyle.Bold);
            find.Click += delegate { string id, label; if (CityPickerForm.Show(null, out id, out label)) SetTimeZone(id, label); };
            change.DropDownItems.Add(find);
            change.DropDownItems.Add(new ToolStripSeparator());
            foreach (var region in ZoneCatalog.Regions)
            {
                var regionItem = new ToolStripMenuItem(region.Name);
                foreach (var zone in region.Zones)
                {
                    if (!ZoneCatalog.Exists(zone.WindowsId)) continue;
                    var z = zone;
                    var item = new ToolStripMenuItem(zone.City);
                    item.Click += delegate { SetTimeZone(z.WindowsId, z.City); };
                    regionItem.DropDownItems.Add(item);
                }
                change.DropDownItems.Add(regionItem);
            }
            change.DropDownItems.Add(new ToolStripSeparator());
            var local = new ToolStripMenuItem("Local time");
            local.Click += delegate { SetTimeZone(TimeZoneInfo.Local.Id, ZoneCatalog.LocalCity()); };
            change.DropDownItems.Add(local);
            _allZonesItem = new ToolStripMenuItem("All time zones");
            _allZonesItem.DropDownItems.Add(new ToolStripMenuItem("Loading..."));
            _allZonesItem.DropDownOpening += delegate { BuildAllZones(); };
            change.DropDownItems.Add(_allZonesItem);
            _menu.Items.Add(change);

            var rename = new ToolStripMenuItem("Rename...");
            rename.Click += delegate
            {
                var name = PromptDialog.Show("Rename widget", "Label shown on this widget:", Config.Label);
                if (name != null) Rename(name.Trim());
            };
            _menu.Items.Add(rename);
            _menu.Items.Add(new ToolStripSeparator());

            var add = new ToolStripMenuItem("Add another widget");
            add.Click += delegate { App.AddWidgetNear(this); };
            _menu.Items.Add(add);

            var remove = new ToolStripMenuItem("Remove this widget");
            remove.Click += delegate { App.RemoveWidget(this); };
            _menu.Items.Add(remove);
            _menu.Items.Add(new ToolStripSeparator());

            _lockItem = new ToolStripMenuItem("Lock widget positions");
            _lockItem.CheckOnClick = true;
            _lockItem.Click += delegate { App.Settings.LockPositions = _lockItem.Checked; App.ApplyAll(); };
            _menu.Items.Add(_lockItem);

            var settings = new ToolStripMenuItem("Settings...");
            settings.Click += delegate { App.ShowSettings(); };
            _menu.Items.Add(settings);
            _menu.Items.Add(new ToolStripSeparator());

            var exit = new ToolStripMenuItem("Exit");
            exit.Click += delegate { App.Exit(); };
            _menu.Items.Add(exit);

            _menu.Opening += delegate { _lockItem.Checked = S != null && S.LockPositions; };
        }

        void BuildAllZones()
        {
            if (_allZonesBuilt) return;
            _allZonesBuilt = true;
            _allZonesItem.DropDownItems.Clear();
            foreach (var tz in ZoneCatalog.All)
            {
                var zone = tz;
                var item = new ToolStripMenuItem(tz.DisplayName);
                item.Click += delegate { SetTimeZone(zone.Id, ZoneCatalog.ShortDisplayName(zone)); };
                _allZonesItem.DropDownItems.Add(item);
            }
        }

        // ---- layout & rendering ---------------------------------------------------------------

        void DisposeFonts()
        {
            if (_fLabel != null) _fLabel.Dispose();
            if (_fSmall != null) _fSmall.Dispose();
            if (_fTime != null) _fTime.Dispose();
            if (_fAmPm != null) _fAmPm.Dispose();
            if (_fDay != null) _fDay.Dispose();
            if (_fDate != null) _fDate.Dispose();
            if (_fGlyph != null) _fGlyph.Dispose();
            _fLabel = _fSmall = _fTime = _fAmPm = _fDay = _fDate = _fGlyph = null;
        }

        void CreateFonts()
        {
            DisposeFonts();
            // The off-screen bitmap is 96 dpi, so point sizes are scaled by the monitor DPI by hand.
            float k = Px;
            _fLabel = new Font("Segoe UI", 9.5f * k, FontStyle.Bold, GraphicsUnit.Point);
            _fSmall = new Font("Segoe UI", 8.5f * k, FontStyle.Regular, GraphicsUnit.Point);
            _fTime = new Font("Segoe UI", 27f * k, FontStyle.Regular, GraphicsUnit.Point);
            _fAmPm = new Font("Segoe UI", 10f * k, FontStyle.Bold, GraphicsUnit.Point);
            _fDay = new Font("Segoe UI", 9f * k, FontStyle.Bold, GraphicsUnit.Point);
            _fDate = new Font("Segoe UI", 9f * k, FontStyle.Regular, GraphicsUnit.Point);
            _fGlyph = new Font("Segoe UI Symbol", 9f * k, FontStyle.Regular, GraphicsUnit.Point);
        }

        static StringFormat Typographic()
        {
            var sf = (StringFormat)StringFormat.GenericTypographic.Clone();
            sf.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces;
            return sf;
        }

        static SizeF Measure(Graphics g, string s, Font f, StringFormat sf)
        {
            if (string.IsNullOrEmpty(s)) return SizeF.Empty;
            return g.MeasureString(s, f, PointF.Empty, sf);
        }

        Snapshot CaptureSnapshot()
        {
            var utc = DateTime.UtcNow;
            var zoned = TimeZoneInfo.ConvertTimeFromUtc(utc, _tz);
            var inv = CultureInfo.InvariantCulture;
            var snap = new Snapshot();
            snap.Label = Config.Label;
            if (S.Use24Hour)
            {
                snap.Time = zoned.ToString(S.ShowSeconds ? "HH:mm:ss" : "HH:mm", inv);
                snap.AmPm = "";
            }
            else
            {
                snap.Time = zoned.ToString(S.ShowSeconds ? "h:mm:ss" : "h:mm", inv);
                snap.AmPm = zoned.Hour < 12 ? "AM" : "PM";
            }
            snap.Offset = S.ShowUtcOffset ? ZoneCatalog.FormatOffset(_tz.GetUtcOffset(utc)) : "";
            snap.Day = zoned.ToString("dddd", CultureInfo.CurrentCulture);
            snap.Date = zoned.ToString("MMM d, yyyy", CultureInfo.CurrentCulture);
            int dayDiff = (zoned.Date - DateTime.Now.Date).Days;
            if (dayDiff > 0) snap.Badge = "+" + dayDiff + (dayDiff == 1 ? " day" : " days");
            else if (dayDiff < 0) snap.Badge = "−" + (-dayDiff) + (dayDiff == -1 ? " day" : " days");
            else snap.Badge = "";
            bool isDay = zoned.Hour >= 6 && zoned.Hour < 18;
            snap.Glyph = isDay ? "☀" : "☽";
            if (S.ShowMarkets)
            {
                var st = MarketCatalog.GetStatus(MarketCatalog.ForWidget(Config.TimeZoneId, Config.Label), utc);
                if (st.Known)
                {
                    snap.Market = st.Market.Name;
                    snap.MarketOpen = st.Open;
                    snap.MarketPhase = st.Phase;
                    snap.MarketText = (st.Open ? "closes in " : st.Break ? "break · opens in " : "opens in ") + st.Countdown;
                }
            }
            snap.Key = string.Join("|", new string[] { snap.Label, snap.Time, snap.AmPm, snap.Offset, snap.Day, snap.Date, snap.Badge, snap.Glyph, snap.Market, snap.MarketText, snap.MarketPhase.ToString() });
            return snap;
        }

        void Tick()
        {
            // Re-align so the display flips right on the second boundary.
            int ms = DateTime.Now.Millisecond;
            _timer.Interval = Math.Max(50, 1000 - ms + 10);
            if (!Visible || S == null) return;
            var snap = CaptureSnapshot();
            if (snap.Key == _lastKey) return;
            if (snap.Day != _lastDay) { _lastDay = snap.Day; Relayout(); }
            Render(snap);
        }
        string _lastDay;

        protected override void Relayout()
        {
            if (S == null) return;
            CreateFonts();
            var snap = CaptureSnapshot();
            using (var bmp = new Bitmap(1, 1))
            using (var g = Graphics.FromImage(bmp))
            using (var sf = Typographic())
            {
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                float px = Px;
                float pad = 14 * px, gap = 2 * px;
                float labelH = _fLabel.GetHeight(g);
                float timeH = _fTime.GetHeight(g);
                float dateH = _fDate.GetHeight(g);
                float smallH = _fSmall.GetHeight(g);

                string widestTime = (S.ShowSeconds ? "88:88:88" : "88:88");
                float timeW = Measure(g, widestTime, _fTime, sf).Width;
                if (!S.Use24Hour) timeW += 6 * px + Measure(g, "PM", _fAmPm, sf).Width;
                float dateW = Measure(g, snap.Day, _fDay, sf).Width + Measure(g, " · " + snap.Date, _fDate, sf).Width;
                if (snap.Badge.Length > 0) dateW += 10 * px + Measure(g, snap.Badge, _fSmall, sf).Width + 12 * px;
                float topW = Measure(g, snap.Label, _fLabel, sf).Width + 12 * px
                    + Measure(g, snap.Glyph, _fGlyph, sf).Width + 5 * px + Measure(g, snap.Offset, _fSmall, sf).Width;
                float marketW = snap.Market.Length > 0 ? Measure(g, "● " + snap.Market + " · " + snap.MarketText, _fSmall, sf).Width : 0;
                float contentW = Math.Max(Math.Max(timeW, marketW), Math.Max(dateW, topW));

                int w = (int)Math.Ceiling(Math.Max(230 * px, contentW + 2 * pad));
                int h = (int)Math.Ceiling(pad + labelH + gap + timeH + gap + dateH + (snap.Market.Length > 0 ? gap + smallH : 0) + pad);
                if (w != Width || h != Height) Size = new Size(w, h);
            }
        }

        protected override void Render(bool force)
        {
            if (!IsHandleCreated || S == null) return;
            var snap = CaptureSnapshot();
            if (!force && snap.Key == _lastKey) return;
            Render(snap);
        }

        void Render(Snapshot snap)
        {
            if (!IsHandleCreated || _fTime == null) return;
            _lastKey = snap.Key;
            int w = Math.Max(1, Width), h = Math.Max(1, Height);
            using (var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    Draw(g, w, h, snap);
                }
                Push(bmp);
            }
        }

        void Draw(Graphics g, int w, int h, Snapshot snap)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;

            float px = Px;
            float pad = 14 * px, gap = 2 * px, radius = 14 * px;
            var t = T;
            var muted = Color.FromArgb(t.DarkPanel ? 165 : 150, t.Text);

            using (var sf = Typographic())
            using (var bgBrush = new SolidBrush(snap.MarketPhase != 0 ? Overlay(t.Background, MarketStatus.PhaseTint(snap.MarketPhase, t.DarkPanel)) : t.Background))
            using (var textBrush = new SolidBrush(t.Text))
            using (var mutedBrush = new SolidBrush(muted))
            using (var accentBrush = new SolidBrush(t.Accent))
            using (var borderPen = new Pen(Color.FromArgb(t.DarkPanel ? 46 : 70, t.DarkPanel ? Color.White : Color.Black), 1f))
            {
                using (var path = RoundedRect(new RectangleF(0, 0, w, h), radius))
                    g.FillPath(bgBrush, path);
                using (var path = RoundedRect(new RectangleF(0.5f, 0.5f, w - 1, h - 1), radius - 0.5f))
                    g.DrawPath(borderPen, path);

                // Thin accent line along the left edge to tie the widget to the wallpaper color.
                using (var accentPen = new Pen(t.Accent, 3 * px))
                {
                    accentPen.StartCap = LineCap.Round;
                    accentPen.EndCap = LineCap.Round;
                    g.DrawLine(accentPen, pad * 0.55f, pad + 2 * px, pad * 0.55f, h - pad - 2 * px);
                }

                float labelH = _fLabel.GetHeight(g);
                float smallH = _fSmall.GetHeight(g);
                float glyphH = _fGlyph.GetHeight(g);
                float timeH = _fTime.GetHeight(g);
                float ampmH = _fAmPm.GetHeight(g);
                float dateH = _fDate.GetHeight(g);

                // Row 1: label on the left, day/night glyph + UTC offset on the right.
                float y = pad;
                g.DrawString(snap.Label, _fLabel, textBrush, pad, y, sf);
                float offW = Measure(g, snap.Offset, _fSmall, sf).Width;
                float x = w - pad - offW;
                if (snap.Offset.Length > 0)
                    g.DrawString(snap.Offset, _fSmall, mutedBrush, x, y + (labelH - smallH) / 2, sf);
                float glyphW = Measure(g, snap.Glyph, _fGlyph, sf).Width;
                g.DrawString(snap.Glyph, _fGlyph, mutedBrush, x - glyphW - (offW > 0 ? 5 * px : 0), y + (labelH - glyphH) / 2, sf);

                // Row 2: the time in the accent color, AM/PM tucked beside it.
                y += labelH + gap;
                g.DrawString(snap.Time, _fTime, accentBrush, pad - 1.5f * px, y, sf);
                if (snap.AmPm.Length > 0)
                {
                    float timeW = Measure(g, snap.Time, _fTime, sf).Width;
                    g.DrawString(snap.AmPm, _fAmPm, textBrush, pad + timeW + 5 * px, y + timeH - ampmH - 7 * px, sf);
                }

                // Row 3: weekday, date, and a "+1 day" badge when the zone is on a different calendar day.
                y += timeH + gap;
                g.DrawString(snap.Day, _fDay, textBrush, pad, y, sf);
                float dayW = Measure(g, snap.Day, _fDay, sf).Width;
                g.DrawString(" · " + snap.Date, _fDate, mutedBrush, pad + dayW, y, sf);
                if (snap.Badge.Length > 0)
                {
                    var badgeSize = Measure(g, snap.Badge, _fSmall, sf);
                    float bw = badgeSize.Width + 12 * px, bh = smallH + 2 * px;
                    float bx = w - pad - bw, by = y + (dateH - bh) / 2;
                    using (var pill = RoundedRect(new RectangleF(bx, by, bw, bh), bh / 2))
                    using (var pillBrush = new SolidBrush(Color.FromArgb(t.DarkPanel ? 56 : 40, t.Accent)))
                        g.FillPath(pillBrush, pill);
                    g.DrawString(snap.Badge, _fSmall, accentBrush, bx + 6 * px, by + 1 * px, sf);
                }

                // Row 4: the local stock market – countdown to the open or close; green while trading.
                if (snap.Market.Length > 0)
                {
                    y += dateH + gap;
                    var marketColor = snap.MarketPhase != 0 ? MarketStatus.PhaseText(snap.MarketPhase, t.DarkPanel) : muted;
                    using (var mb = new SolidBrush(marketColor))
                        g.DrawString((snap.MarketOpen ? "● " : "○ ") + snap.Market + " · " + snap.MarketText, _fSmall, mb, pad, y, sf);
                }
            }
        }

        /// <summary>Lays a translucent colour over an opaque one (what the ribbon's tint does on screen).</summary>
        static Color Overlay(Color under, Color over)
        {
            float a = over.A / 255f;
            return Color.FromArgb(under.A, (int)(under.R + (over.R - under.R) * a), (int)(under.G + (over.G - under.G) * a), (int)(under.B + (over.B - under.B) * a));
        }

        public static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            float d = Math.Max(1f, radius * 2);
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}

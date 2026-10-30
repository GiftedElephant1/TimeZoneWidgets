using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TimeZoneWidgets
{
    /// <summary>
    /// A small globe that shows where it is day and night right now, the 24 one-hour time-zone
    /// meridians, and a marker for every clock widget's city. It turns only as real time passes
    /// (one rotation per day, so the sun stays put and the Earth moves under it) or when the
    /// user turns it (drag on the sphere, or the mouse wheel). Dragging the glowing rim around
    /// the sphere (or Shift/Ctrl+drag anywhere) moves the globe, in any layout.
    /// </summary>
    public class GlobeForm : LayeredWindow
    {
        const double Deg = Math.PI / 180.0;
        const float BaseDiameter = 172f;   // design pixels at 100% scale
        const float Glow = 16f;            // soft halo outside the sphere, design pixels; also the drag handle
        const int SecondsBetweenTimeFrames = 15;   // 15 s = 1/16 of a degree of real rotation

        readonly Timer _timer;
        double _centerLon;     // longitude at the center of the view, degrees
        double _centerLat = 18; // latitude at the center of the view (tilt), degrees
        double _lonFromSun;    // view center relative to the sub-solar longitude (when following time)
        bool _following;       // whether _lonFromSun is current
        bool _spinning;        // left-drag rotating the sphere
        Point _lastMouse;
        DateTime _lastStaticRender = DateTime.MinValue;

        int _d;                // sphere diameter in pixels
        int _size;             // window size in pixels (sphere + glow)
        ContextMenuStrip _menu;
        ToolStripMenuItem _autoItem;

        // Colors
        static readonly Color OceanDay = Color.FromArgb(28, 84, 156);
        static readonly Color OceanNight = Color.FromArgb(9, 18, 42);
        static readonly Color LandDay = Color.FromArgb(104, 164, 92);
        static readonly Color LandNight = Color.FromArgb(34, 46, 36);

        public GlobeForm(TrayApp app, AppSettings settings, Theme theme)
            : base(app)
        {
            Text = "Time zone globe";
            Location = new Point(settings.GlobeX, settings.GlobeY);
            Size = new Size(200, 200);
            // Start centered on the local zone so the user's own region is in view.
            SetCenterLon(TimeZoneInfo.Local.BaseUtcOffset.TotalHours * 15.0);

            BuildMenu();
            ContextMenuStrip = _menu;

            _timer = new Timer();
            _timer.Interval = 500;
            _timer.Tick += delegate { Frame(); };

            ApplySettings(settings, theme);
        }

        public override int SavedX { get { return S.GlobeX; } set { S.GlobeX = value; } }
        public override int SavedY { get { return S.GlobeY; } set { S.GlobeY = value; } }
        protected override int OpacityPercent { get { return S == null ? 100 : S.Opacity; } }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            _timer.Start();
        }

        /// <summary>Longitude the sun is directly above right now, degrees (east positive).</summary>
        static double SubSolarLon(DateTime utc)
        {
            return (12.0 - utc.TimeOfDay.TotalHours) * 15.0;
        }

        static double Wrap(double lon)
        {
            while (lon < -180) lon += 360;
            while (lon > 180) lon -= 360;
            return lon;
        }

        /// <summary>Points the view at a longitude and remembers it relative to the sun, so
        /// "turn with the time" continues from there instead of jumping.</summary>
        void SetCenterLon(double lon)
        {
            _centerLon = Wrap(lon);
            _lonFromSun = _centerLon - SubSolarLon(DateTime.UtcNow);
            _following = true;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _timer.Stop();
            _timer.Dispose();
            base.OnFormClosed(e);
        }

        /// <summary>Called when a clock widget's zone changes so the markers follow.</summary>
        public void RefreshMarkers()
        {
            Render(true);
        }

        public void ResetView()
        {
            SetCenterLon(TimeZoneInfo.Local.BaseUtcOffset.TotalHours * 15.0);
            _centerLat = 18;
            Render(true);
        }

        // ---- interaction ----------------------------------------------------------------------

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            // On the sphere: turn it. On the rim around it (or with Shift/Ctrl, or the middle button): move it.
            bool moveModifier = (ModifierKeys & (Keys.Shift | Keys.Control)) != 0;
            bool wantsMove = e.Button == MouseButtons.Middle || (e.Button == MouseButtons.Left && (moveModifier || !InsideSphere(e.Location)));
            if (wantsMove)
            {
                BeginMove(e.Location);
            }
            else if (e.Button == MouseButtons.Left)
            {
                _spinning = true;
                _lastMouse = e.Location;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (IsMoving)
            {
                ContinueMove();
                return;
            }
            if (!_spinning) Cursor = InsideSphere(e.Location) ? Cursors.Default : Cursors.SizeAll;
            if (_spinning)
            {
                int dx = e.X - _lastMouse.X, dy = e.Y - _lastMouse.Y;
                _lastMouse = e.Location;
                double degPerPixel = 180.0 / Math.Max(40, _d);
                SetCenterLon(_centerLon - dx * degPerPixel);
                _centerLat = Math.Max(-75, Math.Min(75, _centerLat + dy * degPerPixel));
                Render(true);
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (IsMoving) EndMove();
            if (_spinning) _spinning = false;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            // Each notch turns the globe 10 degrees.
            SetCenterLon(_centerLon - e.Delta / 120 * 10.0);
            Render(true);
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button == MouseButtons.Left && (ModifierKeys & Keys.Shift) == 0)
            {
                _spinning = false;
                App.ShowSettings();
            }
        }

        /// <summary>True when a window point is on the sphere itself (not the rim around it).</summary>
        bool InsideSphere(Point p)
        {
            float cx = _size / 2f, cy = _size / 2f, r = _d / 2f - 1.5f * Px;
            float dx = p.X - cx, dy = p.Y - cy;
            return dx * dx + dy * dy <= r * r;
        }

        void BuildMenu()
        {
            _menu = new ContextMenuStrip();
            _menu.Font = new Font("Segoe UI", 9.5f);

            _autoItem = new ToolStripMenuItem("Turn with the time of day");
            _autoItem.CheckOnClick = true;
            _autoItem.Click += delegate { App.Settings.GlobeAutoRotate = _autoItem.Checked; App.ApplyAll(); };
            _menu.Items.Add(_autoItem);

            var reset = new ToolStripMenuItem("Reset view");
            reset.Click += delegate { ResetView(); };
            _menu.Items.Add(reset);

            var hint = new ToolStripMenuItem("Drag the globe to turn it · drag its rim to move it");
            hint.Enabled = false;
            _menu.Items.Add(hint);
            _menu.Items.Add(new ToolStripSeparator());

            var hide = new ToolStripMenuItem("Hide globe");
            hide.Click += delegate { App.SetGlobeVisible(false); };
            _menu.Items.Add(hide);

            var settings = new ToolStripMenuItem("Settings...");
            settings.Click += delegate { App.ShowSettings(); };
            _menu.Items.Add(settings);
            _menu.Items.Add(new ToolStripSeparator());

            var exit = new ToolStripMenuItem("Exit");
            exit.Click += delegate { App.Exit(); };
            _menu.Items.Add(exit);

            _menu.Opening += delegate { _autoItem.Checked = S != null && S.GlobeAutoRotate; };
        }

        // ---- animation ------------------------------------------------------------------------

        void Frame()
        {
            var now = DateTime.UtcNow;
            if (!Visible || S == null || _spinning || IsMoving) return;
            if ((now - _lastStaticRender).TotalSeconds < SecondsBetweenTimeFrames) return;

            if (S.GlobeAutoRotate)
            {
                // Keep the view fixed relative to the sun: the Earth turns under it at the real
                // rate of 15° per hour, so the day/night line stays where it is on screen and
                // the continents drift through it as the day goes on.
                if (!_following) SetCenterLon(_centerLon);
                _centerLon = Wrap(SubSolarLon(now) + _lonFromSun);
            }
            else
            {
                // View fixed on the chosen longitude; only the day/night line moves.
                _following = false;
            }
            Render(true);
        }

        // ---- layout & rendering ---------------------------------------------------------------

        protected override void Relayout()
        {
            if (S == null) return;
            _d = Math.Max(48, (int)Math.Round(BaseDiameter * Px));
            _size = _d + (int)Math.Round(2 * Glow * Px);
            if (Width != _size || Height != _size) Size = new Size(_size, _size);
        }

        protected override void Render(bool force)
        {
            if (!IsHandleCreated || S == null || _d <= 0) return;
            _lastStaticRender = DateTime.UtcNow;
            using (var bmp = new Bitmap(_size, _size, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    DrawGlow(g);
                }
                DrawSphere(bmp);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    DrawMarkers(g);
                }
                Push(bmp);
            }
        }

        void DrawGlow(Graphics g)
        {
            float glow = Glow * Px;
            float cx = _size / 2f, cy = _size / 2f;
            float r = _d / 2f + glow;
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(cx - r, cy - r, 2 * r, 2 * r);
                // A barely visible disc under the glow keeps the whole rim clickable (fully
                // transparent pixels of a layered window let clicks fall through).
                using (var rim = new SolidBrush(Color.FromArgb(10, T.Accent))) g.FillPath(rim, path);
                using (var brush = new PathGradientBrush(path))
                {
                    brush.CenterColor = Color.FromArgb(150, T.Accent);
                    brush.SurroundColors = new Color[] { Color.FromArgb(0, T.Accent) };
                    brush.FocusScales = new PointF(0.80f, 0.80f);
                    g.FillPath(brush, path);
                }
            }
        }

        struct Sun { public double X, Y, Z; }

        /// <summary>Direction to the sun in world coordinates (x toward 90°E, y north, z toward 0°).</summary>
        static Sun SunDirection(DateTime utc, double centerLonDeg)
        {
            double doy = utc.DayOfYear + utc.TimeOfDay.TotalHours / 24.0;
            double decl = 23.44 * Math.Sin(2 * Math.PI * (doy - 81) / 365.25);
            double sunLon = (12.0 - utc.TimeOfDay.TotalHours) * 15.0;
            // Express the sun relative to the view's center longitude so pixels need no extra rotation.
            double rel = (sunLon - centerLonDeg) * Deg;
            var s = new Sun();
            s.X = Math.Cos(decl * Deg) * Math.Sin(rel);
            s.Y = Math.Sin(decl * Deg);
            s.Z = Math.Cos(decl * Deg) * Math.Cos(rel);
            return s;
        }

        // Per-pixel geometry cache. Everything here depends only on the sphere size and the tilt
        // (center latitude), not on the spin, so a frame is just lookups and a few multiplies.
        int _cacheD = -1;
        double _cacheTilt = double.NaN;
        float[] _cEdge, _cNz, _cWx, _cWy, _cWz, _cLonRel, _cLatDeg, _cCosLat;
        int[] _cMaskRow;

        void EnsureGeometry()
        {
            if (_cacheD == _d && _cacheTilt == _centerLat) return;
            _cacheD = _d;
            _cacheTilt = _centerLat;
            int n = _size * _size;
            if (_cEdge == null || _cEdge.Length != n)
            {
                _cEdge = new float[n]; _cNz = new float[n]; _cWx = new float[n]; _cWy = new float[n]; _cWz = new float[n];
                _cLonRel = new float[n]; _cLatDeg = new float[n]; _cCosLat = new float[n]; _cMaskRow = new int[n];
            }
            int mh = GlobeData.MaskHeight;
            double cx = _size / 2.0, cy = _size / 2.0, r = _d / 2.0;
            double tilt = _centerLat * Deg;
            double sinT = Math.Sin(tilt), cosT = Math.Cos(tilt);
            for (int j = 0; j < _size; j++)
            {
                double ny = (cy - (j + 0.5)) / r;
                for (int i = 0; i < _size; i++)
                {
                    int idx = j * _size + i;
                    double nx = (i + 0.5 - cx) / r;
                    double d2 = nx * nx + ny * ny;
                    double edge = (1.0 - Math.Sqrt(d2)) * r + 0.5;     // anti-aliased rim coverage
                    if (edge <= 0) { _cEdge[idx] = 0; continue; }
                    if (edge > 1) edge = 1;
                    double nz = Math.Sqrt(Math.Max(0, 1 - d2));
                    // View -> world (undo the latitude tilt). World: x east, y north, z toward the viewer.
                    double wy = ny * cosT + nz * sinT;
                    double wz = nz * cosT - ny * sinT;
                    double wx = nx;
                    double lat = Math.Asin(Math.Max(-1, Math.Min(1, wy)));
                    int my = (int)((90.0 - lat / Deg) / 180.0 * mh);
                    if (my < 0) my = 0; else if (my >= mh) my = mh - 1;
                    _cEdge[idx] = (float)edge;
                    _cNz[idx] = (float)nz;
                    _cWx[idx] = (float)wx; _cWy[idx] = (float)wy; _cWz[idx] = (float)wz;
                    _cLonRel[idx] = (float)(Math.Atan2(wx, wz) / Deg);
                    _cLatDeg[idx] = (float)(lat / Deg);
                    _cCosLat[idx] = (float)Math.Max(0.15, Math.Cos(lat));
                    _cMaskRow[idx] = my * GlobeData.MaskWidth;
                }
            }
        }

        void DrawSphere(Bitmap bmp)
        {
            var mask = GlobeData.Mask;
            int mw = GlobeData.MaskWidth;
            EnsureGeometry();
            double centerLon = _centerLon;
            var sun = SunDirection(DateTime.UtcNow, centerLon);
            float sx = (float)sun.X, sy = (float)sun.Y, sz = (float)sun.Z;

            // Meridians to highlight: one per clock widget, at its current UTC offset.
            var highlights = new List<double>();
            foreach (var cfg in App.Settings.Widgets)
                highlights.Add(App.ZoneFor(cfg).GetUtcOffset(DateTime.UtcNow).TotalHours * 15.0);
            var hl = highlights.ToArray();

            Color accent = T.Accent;
            float aR = accent.R, aG = accent.G, aB = accent.B;
            float oceanDR = OceanDay.R, oceanDG = OceanDay.G, oceanDB = OceanDay.B;
            float oceanNR = OceanNight.R, oceanNG = OceanNight.G, oceanNB = OceanNight.B;
            float landDR = LandDay.R, landDG = LandDay.G, landDB = LandDay.B;
            float landNR = LandNight.R, landNG = LandNight.G, landNB = LandNight.B;

            var data = bmp.LockBits(new Rectangle(0, 0, _size, _size), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                var row = new byte[stride];
                for (int j = 0; j < _size; j++)
                {
                    IntPtr rowPtr = new IntPtr(data.Scan0.ToInt64() + j * stride);
                    Marshal.Copy(rowPtr, row, 0, stride);
                    bool touched = false;
                    for (int i = 0; i < _size; i++)
                    {
                        int idx = j * _size + i;
                        float edge = _cEdge[idx];
                        if (edge <= 0) continue;
                        touched = true;

                        float lonDeg = _cLonRel[idx] + (float)centerLon;
                        if (lonDeg > 180) lonDeg -= 360; else if (lonDeg < -180) lonDeg += 360;
                        int mx = (int)((lonDeg + 180f) / 360f * mw);
                        if (mx < 0) mx = 0; else if (mx >= mw) mx = mw - 1;
                        bool land = mask[_cMaskRow[idx] + mx] != 0;

                        // Day/night with a soft terminator.
                        float light = _cWx[idx] * sx + _cWy[idx] * sy + _cWz[idx] * sz;
                        float day = (light + 0.10f) / 0.20f;
                        if (day < 0) day = 0; else if (day > 1) day = 1;
                        day = day * day * (3 - 2 * day);
                        float lambert = 0.70f + 0.30f * (light > 0 ? light : 0);
                        float rim = 0.72f + 0.28f * _cNz[idx];              // limb darkening for a round look

                        float rr, gg, bb;
                        if (land)
                        {
                            rr = (landNR + (landDR * lambert - landNR) * day) * rim;
                            gg = (landNG + (landDG * lambert - landNG) * day) * rim;
                            bb = (landNB + (landDB * lambert - landNB) * day) * rim;
                        }
                        else
                        {
                            rr = (oceanNR + (oceanDR * lambert - oceanNR) * day) * rim;
                            gg = (oceanNG + (oceanDG * lambert - oceanNG) * day) * rim;
                            bb = (oceanNB + (oceanDB * lambert - oceanNB) * day) * rim;
                        }

                        // Time-zone meridians every 15 degrees (one hour), faint; the equator too.
                        float m = lonDeg % 15f; if (m < 0) m += 15f; if (m > 7.5f) m = 15f - m;
                        float lineW = 0.40f / _cCosLat[idx];
                        float grid = 0;
                        if (m < lineW) grid = 0.28f;
                        float latAbs = _cLatDeg[idx] < 0 ? -_cLatDeg[idx] : _cLatDeg[idx];
                        if (latAbs < 0.35f && grid < 0.22f) grid = 0.22f;
                        // Highlighted meridians for the widgets' zones.
                        float hlAmount = 0;
                        for (int k = 0; k < hl.Length; k++)
                        {
                            float dd = lonDeg - (float)hl[k];
                            if (dd < 0) dd = -dd;
                            if (dd > 180) dd = 360 - dd;
                            if (dd < lineW * 1.6f) { hlAmount = 0.85f; break; }
                        }
                        if (hlAmount > 0)
                        {
                            rr += (aR - rr) * hlAmount;
                            gg += (aG - gg) * hlAmount;
                            bb += (aB - bb) * hlAmount;
                        }
                        else if (grid > 0)
                        {
                            rr += (235 - rr) * grid;
                            gg += (240 - gg) * grid;
                            bb += (255 - bb) * grid;
                        }

                        // Composite over whatever is there (the glow) using straight alpha.
                        int o = i * 4;
                        float a = edge;
                        float bgA = row[o + 3] / 255f;
                        float outA = a + bgA * (1 - a);
                        float invA = outA > 0 ? 1f / outA : 0;
                        row[o] = ClampByte((bb * a + row[o] * bgA * (1 - a)) * invA);
                        row[o + 1] = ClampByte((gg * a + row[o + 1] * bgA * (1 - a)) * invA);
                        row[o + 2] = ClampByte((rr * a + row[o + 2] * bgA * (1 - a)) * invA);
                        row[o + 3] = ClampByte(outA * 255);
                    }
                    if (touched) Marshal.Copy(row, 0, rowPtr, stride);
                }
            }
            finally
            {
                bmp.UnlockBits(data);
            }
        }

        static byte ClampByte(float v)
        {
            if (v <= 0) return 0;
            if (v >= 255) return 255;
            return (byte)(v + 0.5f);
        }

        /// <summary>Project a lat/lon onto the view; returns false when it is on the far side.</summary>
        bool Project(double latDeg, double lonDeg, out PointF p, out double depth)
        {
            double lat = latDeg * Deg, lon = (lonDeg - _centerLon) * Deg;
            double tilt = _centerLat * Deg;
            double wx = Math.Cos(lat) * Math.Sin(lon);
            double wy = Math.Sin(lat);
            double wz = Math.Cos(lat) * Math.Cos(lon);
            double ny = wy * Math.Cos(tilt) - wz * Math.Sin(tilt);
            double nz = wy * Math.Sin(tilt) + wz * Math.Cos(tilt);
            double r = _d / 2.0, cx = _size / 2.0, cy = _size / 2.0;
            p = new PointF((float)(cx + wx * r), (float)(cy - ny * r));
            depth = nz;
            return nz > 0.02;
        }

        void DrawMarkers(Graphics g)
        {
            float px = Px;
            var seen = new HashSet<string>();
            foreach (var cfg in App.Settings.Widgets)
            {
                var entry = ZoneCatalog.EntryFor(cfg.TimeZoneId, cfg.Label);
                double lat, lon;
                if (entry != null) { lat = entry.Latitude; lon = entry.Longitude; }
                else if (string.Equals(cfg.TimeZoneId, TimeZoneInfo.Local.Id, StringComparison.OrdinalIgnoreCase)
                         && ZoneCatalog.EntryFor(TimeZoneInfo.Local.Id, null) != null)
                {
                    var e = ZoneCatalog.EntryFor(TimeZoneInfo.Local.Id, null);
                    lat = e.Latitude; lon = e.Longitude;
                }
                else continue;   // zones without a known city still get their meridian highlighted

                string key = lat.ToString("F1") + "," + lon.ToString("F1");
                if (!seen.Add(key)) continue;

                PointF p; double depth;
                if (!Project(lat, lon, out p, out depth)) continue;
                float rad = (3.2f + 1.3f * (float)depth) * px;
                using (var ring = new Pen(Color.FromArgb(230, Color.White), 1.6f * px))
                using (var fill = new SolidBrush(T.Accent))
                {
                    g.FillEllipse(fill, p.X - rad, p.Y - rad, 2 * rad, 2 * rad);
                    g.DrawEllipse(ring, p.X - rad, p.Y - rad, 2 * rad, 2 * rad);
                }
            }
        }
    }
}

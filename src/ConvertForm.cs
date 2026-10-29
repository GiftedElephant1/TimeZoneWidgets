using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace TimeZoneWidgets
{
    /// <summary>
    /// "What time is that here?" popup, opened by double-clicking a widget or a ribbon zone.
    /// Type a time in that zone (prefilled with its current time), see it in your local time at a
    /// glance, and a table of the surrounding hours converted to every zone you have. A swap
    /// button turns it around (type a local time, see it in that zone). Nothing here changes any
    /// widget; it is only a calculator.
    /// </summary>
    public class ConvertForm : Form
    {
        static ConvertForm _open;

        readonly TrayApp _app;
        readonly TimeZoneInfo _zone;
        readonly string _zoneLabel;
        bool _fromLocal;                 // false: type a time in the zone, see local; true: the reverse

        Label _header, _prompt, _result, _hint;
        TextBox _time;
        DateTimePicker _date;
        Button _swap, _now;
        DataGridView _grid;
        bool _loading;

        /// <summary>Opens (or replaces) the popup for a zone, placed next to the given screen point.</summary>
        public static void ShowFor(TrayApp app, TimeZoneInfo zone, string label, Point nearScreenPoint)
        {
            if (_open != null && !_open.IsDisposed) _open.Close();
            _open = new ConvertForm(app, zone, label);
            _open.PlaceNear(nearScreenPoint);
            _open.Show();
            _open.Activate();
        }

        ConvertForm(TrayApp app, TimeZoneInfo zone, string label)
        {
            _app = app;
            _zone = zone;
            _zoneLabel = string.IsNullOrEmpty(label) ? ZoneCatalog.ShortDisplayName(zone) : label;

            Text = "Convert a time – " + _zoneLabel;
            Font = new Font("Segoe UI", 9.5f);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            KeyPreview = true;
            ClientSize = new Size(560, 300);
            KeyDown += delegate(object s, KeyEventArgs e) { if (e.KeyCode == Keys.Escape) Close(); };
            // Esc also works through the form's cancel button mechanism, whatever control has focus.
            var esc = new Button();
            esc.Size = new Size(0, 0);
            esc.TabStop = false;
            esc.Click += delegate { Close(); };
            Controls.Add(esc);
            CancelButton = esc;

            _header = new Label();
            _header.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            _header.AutoSize = true;
            _header.Location = new Point(10, 8);
            Controls.Add(_header);

            _prompt = new Label();
            _prompt.AutoSize = true;
            _prompt.Location = new Point(10, 38);
            Controls.Add(_prompt);

            _time = new TextBox();
            _time.Font = new Font("Segoe UI", 11f);
            _time.Location = new Point(118, 34);
            _time.Width = 90;
            _time.TextChanged += delegate { Recompute(); };
            Controls.Add(_time);

            _date = new DateTimePicker();
            _date.Format = DateTimePickerFormat.Custom;
            _date.CustomFormat = "ddd, MMM d";
            _date.Location = new Point(214, 36);
            _date.Width = 118;
            _date.ValueChanged += delegate { Recompute(); };
            Controls.Add(_date);

            _now = new Button();
            _now.Text = "Now";
            _now.Location = new Point(338, 34);
            _now.Size = new Size(48, 26);
            _now.Click += delegate { SetToNow(); };
            Controls.Add(_now);

            _swap = new Button();
            _swap.Text = "⇄ Swap";
            _swap.Location = new Point(392, 34);
            _swap.Size = new Size(118, 26);
            _swap.Click += delegate { _fromLocal = !_fromLocal; SetToNow(); };
            Controls.Add(_swap);

            _result = new Label();
            _result.Font = new Font("Segoe UI", 13f, FontStyle.Bold);
            _result.AutoSize = true;
            _result.Location = new Point(10, 66);
            Controls.Add(_result);

            _grid = new DataGridView();
            _grid.Location = new Point(10, 96);
            _grid.Size = new Size(540, 172);
            _grid.RowTemplate.Height = 19;
            _grid.DefaultCellStyle.Font = new Font("Segoe UI", 8.5f);
            _grid.ReadOnly = true;
            _grid.AllowUserToAddRows = false;
            _grid.AllowUserToDeleteRows = false;
            _grid.AllowUserToResizeRows = false;
            _grid.RowHeadersVisible = false;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _grid.MultiSelect = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            _grid.BackgroundColor = SystemColors.Window;
            _grid.BorderStyle = BorderStyle.FixedSingle;
            _grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            _grid.ScrollBars = ScrollBars.Both;
            _grid.ShowCellToolTips = false;
            _grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(225, 236, 255);
            _grid.DefaultCellStyle.SelectionForeColor = SystemColors.WindowText;
            _grid.CellDoubleClick += delegate(object s, DataGridViewCellEventArgs e) { if (e.RowIndex >= 0) PickRow(e.RowIndex); };
            Controls.Add(_grid);

            _hint = new Label();
            _hint.AutoSize = true;
            _hint.ForeColor = SystemColors.GrayText;
            _hint.Location = new Point(10, 276);
            _hint.Font = new Font("Segoe UI", 8f);
            _hint.Text = "Type a time · wheel ±1 h (Ctrl ±15 min) · double-click a row to use it · Esc closes";
            Controls.Add(_hint);

            // The mouse wheel anywhere over the popup steps the time (the table re-centres).
            MouseWheel += OnWheel;
            foreach (Control c in Controls) c.MouseWheel += OnWheel;

            SetToNow();
        }

        void OnWheel(object sender, MouseEventArgs e)
        {
            var handled = e as HandledMouseEventArgs;
            if (handled != null) handled.Handled = true;   // keep the grid/date picker from scrolling themselves
            int steps = e.Delta / 120;
            if (steps == 0) return;
            int minutes = (ModifierKeys & Keys.Control) == Keys.Control ? 15 : 60;
            ShiftBy(-steps * minutes);   // wheel up = earlier (the rows above), wheel down = later
        }

        /// <summary>Moves the typed time by a number of minutes (keeping the date in step).</summary>
        void ShiftBy(int minutes)
        {
            TimeSpan tod;
            DateTime src = TryParseTime(_time.Text, out tod) ? _date.Value.Date + tod
                                                              : TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Source);
            src = src.AddMinutes(minutes);
            _loading = true;
            _time.Text = FormatTime(src);
            _date.Value = src.Date;
            _loading = false;
            Recompute();
        }

        // ---- which way round ------------------------------------------------------------------

        TimeZoneInfo Source { get { return _fromLocal ? TimeZoneInfo.Local : _zone; } }
        TimeZoneInfo Target { get { return _fromLocal ? _zone : TimeZoneInfo.Local; } }
        string SourceName { get { return _fromLocal ? LocalName : _zoneLabel; } }
        string TargetName { get { return _fromLocal ? _zoneLabel : LocalName; } }
        static string LocalName { get { return ZoneCatalog.LocalCity() + " (your time)"; } }

        void SetToNow()
        {
            _loading = true;
            var nowSrc = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Source);
            _time.Text = FormatTime(nowSrc);
            _date.Value = nowSrc.Date;
            _loading = false;
            _header.Text = "Time in " + SourceName + "  →  " + TargetName;
            _prompt.Text = "Time in " + (_fromLocal ? ZoneCatalog.LocalCity() : _zoneLabel) + ":";
            _swap.Text = "⇄ " + (_fromLocal ? "Back to " + _zoneLabel : "From " + ZoneCatalog.LocalCity());
            Recompute();
            _time.SelectAll();
            _time.Focus();
        }

        string FormatTime(DateTime t)
        {
            return _app.Settings.Use24Hour ? t.ToString("HH:mm", CultureInfo.InvariantCulture) : t.ToString("h:mm tt", CultureInfo.InvariantCulture);
        }

        // Note: a one-character format would be read as a *standard* format and throw, hence "%H".
        static readonly string[] Formats = { "h:mm tt", "h:mmtt", "h tt", "htt", "hh:mm tt", "hh:mmtt", "H:mm", "HH:mm", "HHmm", "%H", "HH", "h:mm:ss tt", "H:mm:ss" };

        static bool TryParseTime(string text, out TimeSpan time)
        {
            time = TimeSpan.Zero;
            if (string.IsNullOrEmpty(text)) return false;
            var s = text.Trim().ToUpperInvariant().Replace(".", "").Replace("A M", "AM").Replace("P M", "PM");
            DateTime dt;
            try
            {
                if (DateTime.TryParseExact(s, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)) { time = dt.TimeOfDay; return true; }
                if (DateTime.TryParse(s, CultureInfo.CurrentCulture, DateTimeStyles.NoCurrentDateDefault, out dt)) { time = dt.TimeOfDay; return true; }
            }
            catch (FormatException) { }
            return false;
        }

        // ---- the conversion -------------------------------------------------------------------

        void Recompute()
        {
            if (_loading) return;
            TimeSpan tod;
            if (!TryParseTime(_time.Text, out tod))
            {
                _result.Text = "Enter a time such as 9:30 PM or 21:30";
                _result.ForeColor = SystemColors.GrayText;
                _grid.Rows.Clear();
                return;
            }
            var src = DateTime.SpecifyKind(_date.Value.Date + tod, DateTimeKind.Unspecified);
            DateTime srcUtc;
            try { srcUtc = TimeZoneInfo.ConvertTimeToUtc(src, Source); }
            catch (ArgumentException)
            {
                // The clocks skip this hour on that day (start of daylight saving).
                _result.Text = "That time does not exist in " + SourceName + " on that day (clocks jump forward).";
                _result.ForeColor = SystemColors.GrayText;
                _grid.Rows.Clear();
                return;
            }
            var dst = TimeZoneInfo.ConvertTimeFromUtc(srcUtc, Target);
            _result.ForeColor = SystemColors.WindowText;
            _result.Text = "=  " + FormatTime(dst) + "  " + TargetName + DayNote(dst, src);
            FillGrid(srcUtc, src.Date);
        }

        static string DayNote(DateTime t, DateTime reference)
        {
            int diff = (t.Date - reference.Date).Days;
            if (diff == 0) return "   (" + t.ToString("ddd, MMM d", CultureInfo.CurrentCulture) + ")";
            return "   (" + t.ToString("ddd, MMM d", CultureInfo.CurrentCulture) + ", " + (diff > 0 ? "next day" : "previous day") + ")";
        }

        void FillGrid(DateTime centerUtc, DateTime sourceDate)
        {
            _grid.SuspendLayout();
            _grid.Rows.Clear();
            _grid.Columns.Clear();

            // Columns: the source zone, the target zone, then every other configured zone.
            var zones = new List<TimeZoneInfo>();
            var names = new List<string>();
            zones.Add(Source); names.Add(SourceName.Replace(" (your time)", ""));
            zones.Add(Target); names.Add(TargetName.Replace(" (your time)", ""));
            foreach (var cfg in _app.Settings.Widgets)
            {
                var tz = _app.ZoneFor(cfg);
                bool dup = false;
                foreach (var z in zones) if (z.Id == tz.Id) { dup = true; break; }
                if (dup) continue;
                zones.Add(tz);
                names.Add(_app.Settings.RibbonCityCodes ? ZoneCatalog.CodeFor(cfg.TimeZoneId, cfg.Label) : cfg.Label);
            }
            for (int i = 0; i < zones.Count; i++)
            {
                var col = new DataGridViewTextBoxColumn();
                col.HeaderText = names[i] + "\n" + ZoneCatalog.FormatOffset(zones[i].GetUtcOffset(centerUtc));
                col.SortMode = DataGridViewColumnSortMode.NotSortable;
                col.MinimumWidth = 66;
                _grid.Columns.Add(col);
            }
            _grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8f, FontStyle.Bold);
            _grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            _grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            // Rows: the typed time and the hours around it.
            for (int h = -3; h <= 3; h++)
            {
                var utc = centerUtc.AddHours(h);
                var cells = new object[zones.Count];
                for (int i = 0; i < zones.Count; i++)
                {
                    var t = TimeZoneInfo.ConvertTimeFromUtc(utc, zones[i]);
                    int dayDiff = (t.Date - sourceDate).Days;
                    cells[i] = FormatTime(t) + (dayDiff > 0 ? "⁺¹" : dayDiff < 0 ? "⁻¹" : "");
                }
                int row = _grid.Rows.Add(cells);
                _grid.Rows[row].Tag = utc;
                if (h == 0)
                {
                    _grid.Rows[row].DefaultCellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                    _grid.Rows[row].DefaultCellStyle.BackColor = Color.FromArgb(236, 243, 255);
                }
            }
            _grid.ClearSelection();
            _grid.ResumeLayout();
        }

        /// <summary>Double-clicking a row makes that row's time the new input.</summary>
        void PickRow(int row)
        {
            var utc = _grid.Rows[row].Tag as DateTime?;
            if (utc == null) return;
            var src = TimeZoneInfo.ConvertTimeFromUtc(utc.Value, Source);
            _loading = true;
            _time.Text = FormatTime(src);
            _date.Value = src.Date;
            _loading = false;
            Recompute();
        }

        // ---- placement --------------------------------------------------------------------------

        void PlaceNear(Point p)
        {
            var area = Screen.FromPoint(p).WorkingArea;
            int w = Width, h = Height;
            int x = p.X - w / 2, y = p.Y + 16;
            if (y + h > area.Bottom) y = p.Y - h - 16;
            if (y < area.Top) y = area.Top + 8;
            if (x + w > area.Right) x = area.Right - w - 8;
            if (x < area.Left) x = area.Left + 8;
            Location = new Point(x, y);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            if (_open == this) _open = null;
        }
    }
}

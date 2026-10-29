using System;
using System.Drawing;
using System.Windows.Forms;

namespace TimeZoneWidgets
{
    /// <summary>The settings window. Every change applies immediately and is saved.</summary>
    public class SettingsForm : Form
    {
        readonly TrayApp _app;
        bool _loading = true;

        RadioButton _r12, _r24;
        CheckBox _seconds, _offset, _auto, _startup, _lock, _globe, _globeSpin;
        TrackBar _scale, _opacity;
        Label _scaleLabel, _opacityLabel;
        Button _bg, _text, _accent, _redetect;
        ComboBox _layer;
        ListBox _widgetList;

        AppSettings S { get { return _app.Settings; } }

        public SettingsForm(TrayApp app)
        {
            _app = app;
            Text = "Time Zone Widgets - Settings";
            Font = new Font("Segoe UI", 9.5f);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            var table = new TableLayoutPanel();
            table.ColumnCount = 2;
            table.AutoSize = true;
            table.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            table.Padding = new Padding(16, 14, 16, 10);
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            Controls.Add(table);

            // Clock format
            var fmt = Flow();
            _r12 = new RadioButton(); _r12.Text = "12-hour (AM/PM)"; _r12.AutoSize = true;
            _r24 = new RadioButton(); _r24.Text = "24-hour (military)"; _r24.AutoSize = true; _r24.Margin = new Padding(18, 3, 3, 3);
            _r12.CheckedChanged += delegate { if (_r12.Checked) { S.Use24Hour = false; Apply(); } };
            _r24.CheckedChanged += delegate { if (_r24.Checked) { S.Use24Hour = true; Apply(); } };
            fmt.Controls.Add(_r12); fmt.Controls.Add(_r24);
            AddRow(table, "Clock format", fmt);

            _seconds = Check("Show seconds", delegate { S.ShowSeconds = _seconds.Checked; Apply(); });
            AddRow(table, "", _seconds);
            _offset = Check("Show UTC offset on each widget", delegate { S.ShowUtcOffset = _offset.Checked; Apply(); });
            AddRow(table, "", _offset);

            // Size
            var sizeRow = Flow();
            _scale = Slider(50, 200, 25);
            _scaleLabel = new Label(); _scaleLabel.AutoSize = true; _scaleLabel.Margin = new Padding(6, 8, 0, 0); _scaleLabel.Width = 60;
            _scale.ValueChanged += delegate { _scaleLabel.Text = _scale.Value + "%"; if (!_loading) { S.Scale = _scale.Value; Apply(); } };
            sizeRow.Controls.Add(_scale); sizeRow.Controls.Add(_scaleLabel);
            AddRow(table, "Widget size", sizeRow);

            // Transparency
            var opRow = Flow();
            _opacity = Slider(20, 100, 10);
            _opacityLabel = new Label(); _opacityLabel.AutoSize = true; _opacityLabel.Margin = new Padding(6, 8, 0, 0);
            _opacity.ValueChanged += delegate { _opacityLabel.Text = _opacity.Value + "% opaque"; if (!_loading) { S.Opacity = _opacity.Value; Apply(); } };
            opRow.Controls.Add(_opacity); opRow.Controls.Add(_opacityLabel);
            AddRow(table, "Transparency", opRow);

            // Colors
            _auto = Check("Match my wallpaper automatically", delegate { ToggleAuto(); });
            AddRow(table, "Colors", _auto);
            var colorRow = Flow();
            _bg = ColorButton("Background", delegate { PickColor("bg"); });
            _text = ColorButton("Text", delegate { PickColor("text"); });
            _accent = ColorButton("Clock", delegate { PickColor("accent"); });
            _redetect = new Button(); _redetect.Text = "Re-detect from wallpaper"; _redetect.AutoSize = true; _redetect.Height = 30; _redetect.Padding = new Padding(6, 0, 6, 0); _redetect.Margin = new Padding(12, 3, 3, 3);
            _redetect.Click += delegate { _app.RedetectWallpaper(); };
            colorRow.Controls.Add(_bg); colorRow.Controls.Add(_text); colorRow.Controls.Add(_accent); colorRow.Controls.Add(_redetect);
            AddRow(table, "", colorRow);

            // Layer
            _layer = new ComboBox();
            _layer.DropDownStyle = ComboBoxStyle.DropDownList;
            _layer.Width = 300;
            _layer.Items.Add("Normal window");
            _layer.Items.Add("Always on top");
            _layer.Items.Add("Pinned to desktop (behind other windows)");
            _layer.SelectedIndexChanged += delegate
            {
                if (_loading) return;
                S.Layer = _layer.SelectedIndex == 1 ? AppSettings.LayerTop : _layer.SelectedIndex == 2 ? AppSettings.LayerBottom : AppSettings.LayerNormal;
                Apply();
            };
            AddRow(table, "Window layer", _layer);

            _lock = Check("Lock widget positions", delegate { S.LockPositions = _lock.Checked; Apply(); });
            AddRow(table, "", _lock);

            // Globe
            _globe = Check("Show the rotating globe", delegate { _app.SetGlobeVisible(_globe.Checked); });
            AddRow(table, "Globe", _globe);
            _globeSpin = Check("Rotate automatically (drag the globe to spin it yourself)", delegate { S.GlobeAutoRotate = _globeSpin.Checked; Apply(); });
            AddRow(table, "", _globeSpin);

            // Startup
            _startup = Check("Start automatically when I sign in to Windows", delegate
            {
                if (_loading) return;
                try { StartupManager.SetEnabled(_startup.Checked); }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Could not change the startup setting:\n" + ex.Message, "Time Zone Widgets", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    RefreshStartup();
                }
            });
            AddRow(table, "Startup", _startup);

            // Widgets
            var widgetsPanel = Flow();
            _widgetList = new ListBox();
            _widgetList.Width = 240;
            _widgetList.Height = 110;
            _widgetList.IntegralHeight = false;
            widgetsPanel.Controls.Add(_widgetList);
            var buttons = new FlowLayoutPanel();
            buttons.FlowDirection = FlowDirection.TopDown;
            buttons.AutoSize = true;
            buttons.Margin = new Padding(8, 0, 0, 0);
            var addBtn = new Button(); addBtn.Text = "Add widget..."; addBtn.AutoSize = true; addBtn.Width = 140;
            addBtn.Click += delegate { ShowAddMenu(addBtn); };
            var removeBtn = new Button(); removeBtn.Text = "Remove selected"; removeBtn.AutoSize = true; removeBtn.Width = 140;
            removeBtn.Click += delegate
            {
                int i = _widgetList.SelectedIndex;
                if (i >= 0 && i < _app.Widgets.Count) _app.RemoveWidget(_app.Widgets[i]);
            };
            var resetBtn = new Button(); resetBtn.Text = "Reset positions"; resetBtn.AutoSize = true; resetBtn.Width = 140;
            resetBtn.Click += delegate { _app.ResetPositions(); };
            buttons.Controls.Add(addBtn); buttons.Controls.Add(removeBtn); buttons.Controls.Add(resetBtn);
            widgetsPanel.Controls.Add(buttons);
            AddRow(table, "Widgets", widgetsPanel);

            var tip = new Label();
            tip.Text = "Tip: drag a widget to move it, double-click one to open these settings, right-click for its menu.";
            tip.AutoSize = true;
            tip.ForeColor = SystemColors.GrayText;
            tip.Margin = new Padding(3, 10, 3, 3);
            AddRow(table, "", tip);

            var close = new Button();
            close.Text = "Close";
            close.AutoSize = true;
            close.Width = 100;
            close.Click += delegate { Close(); };
            close.Margin = new Padding(3, 10, 3, 3);
            close.Anchor = AnchorStyles.Right;
            AddRow(table, "", close);
            CancelButton = close;

            LoadValues();
            ClientSize = new Size(table.PreferredSize.Width, table.PreferredSize.Height);
            _loading = false;
        }

        // ---- helpers --------------------------------------------------------------------------

        static FlowLayoutPanel Flow()
        {
            var f = new FlowLayoutPanel();
            f.AutoSize = true;
            f.WrapContents = false;
            f.Margin = new Padding(0);
            return f;
        }

        static TrackBar Slider(int min, int max, int tick)
        {
            var t = new TrackBar();
            t.Minimum = min;
            t.Maximum = max;
            t.TickFrequency = tick;
            t.SmallChange = 5;
            t.LargeChange = tick;
            t.Width = 260;
            t.AutoSize = false;
            t.Height = 34;
            return t;
        }

        CheckBox Check(string text, EventHandler onChange)
        {
            var c = new CheckBox();
            c.Text = text;
            c.AutoSize = true;
            c.CheckedChanged += delegate(object s, EventArgs e) { if (!_loading) onChange(s, e); };
            return c;
        }

        static Button ColorButton(string text, EventHandler onClick)
        {
            var b = new Button();
            b.Text = text;
            b.Width = 96;
            b.Height = 30;
            b.FlatStyle = FlatStyle.Flat;
            b.Click += onClick;
            return b;
        }

        static void AddRow(TableLayoutPanel table, string caption, Control control)
        {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var label = new Label();
            label.Text = caption;
            label.AutoSize = true;
            label.Margin = new Padding(0, 8, 0, 0);
            label.Font = new Font(table.Font, FontStyle.Bold);
            table.Controls.Add(label, 0, row);
            table.Controls.Add(control, 1, row);
        }

        void Apply()
        {
            if (_loading) return;
            _app.ApplyAll();
            RefreshSwatches();
        }

        void LoadValues()
        {
            _loading = true;
            _r12.Checked = !S.Use24Hour;
            _r24.Checked = S.Use24Hour;
            _seconds.Checked = S.ShowSeconds;
            _offset.Checked = S.ShowUtcOffset;
            _scale.Value = Math.Max(_scale.Minimum, Math.Min(_scale.Maximum, S.Scale));
            _scaleLabel.Text = _scale.Value + "%";
            _opacity.Value = Math.Max(_opacity.Minimum, Math.Min(_opacity.Maximum, S.Opacity));
            _opacityLabel.Text = _opacity.Value + "% opaque";
            _auto.Checked = S.AutoTheme;
            _layer.SelectedIndex = S.Layer == AppSettings.LayerTop ? 1 : S.Layer == AppSettings.LayerBottom ? 2 : 0;
            _lock.Checked = S.LockPositions;
            _globe.Checked = _app.GlobeVisible;
            _globeSpin.Checked = S.GlobeAutoRotate;
            _startup.Checked = StartupManager.IsEnabled();
            RefreshSwatches();
            RefreshWidgetList();
            _loading = false;
        }

        public void RefreshStartup()
        {
            bool was = _loading;
            _loading = true;
            _startup.Checked = StartupManager.IsEnabled();
            _loading = was;
        }

        public void RefreshGlobe()
        {
            bool was = _loading;
            _loading = true;
            _globe.Checked = _app.GlobeVisible;
            _loading = was;
        }

        public void RefreshSwatches()
        {
            var t = _app.Theme;
            Swatch(_bg, t.Background);
            Swatch(_text, t.Text);
            Swatch(_accent, t.Accent);
            bool manual = !S.AutoTheme;
            _bg.Enabled = manual;
            _text.Enabled = manual;
            _accent.Enabled = manual;
            _redetect.Enabled = !manual;
        }

        static void Swatch(Button b, Color c)
        {
            b.BackColor = c;
            b.ForeColor = Theme.Luminance(c) < 0.5 ? Color.White : Color.Black;
        }

        public void RefreshWidgetList()
        {
            int selected = _widgetList.SelectedIndex;
            _widgetList.BeginUpdate();
            _widgetList.Items.Clear();
            foreach (var w in _app.Widgets)
                _widgetList.Items.Add(w.Config.Label + "  (" + ZoneCatalog.ShortDisplayName(w.TimeZone) + ", " + ZoneCatalog.FormatOffset(w.TimeZone.GetUtcOffset(DateTime.UtcNow)) + ")");
            _widgetList.EndUpdate();
            if (selected >= 0 && selected < _widgetList.Items.Count) _widgetList.SelectedIndex = selected;
        }

        void ToggleAuto()
        {
            if (_auto.Checked)
            {
                S.AutoTheme = true;
            }
            else
            {
                // Start manual colors from whatever the wallpaper gave us, so nothing jumps.
                var t = _app.Theme;
                S.AutoTheme = false;
                S.BackgroundColor = Theme.ToHtml(t.Background);
                S.TextColor = Theme.ToHtml(t.Text);
                S.AccentColor = Theme.ToHtml(t.Accent);
            }
            Apply();
        }

        void PickColor(string which)
        {
            using (var dialog = new ColorDialog())
            {
                dialog.FullOpen = true;
                dialog.AnyColor = true;
                dialog.Color = which == "bg" ? _app.Theme.Background : which == "text" ? _app.Theme.Text : _app.Theme.Accent;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                var html = Theme.ToHtml(dialog.Color);
                if (which == "bg") S.BackgroundColor = html;
                else if (which == "text") S.TextColor = html;
                else S.AccentColor = html;
                S.AutoTheme = false;
                Apply();
            }
        }

        void ShowAddMenu(Control anchor)
        {
            var menu = new ContextMenuStrip();
            menu.Font = Font;
            foreach (var region in ZoneCatalog.Regions)
            {
                var regionItem = new ToolStripMenuItem(region.Name);
                foreach (var zone in region.Zones)
                {
                    if (!ZoneCatalog.Exists(zone.WindowsId)) continue;
                    var z = zone;
                    var item = new ToolStripMenuItem(zone.City);
                    item.Click += delegate { _app.AddWidget(z.WindowsId, z.City); };
                    regionItem.DropDownItems.Add(item);
                }
                menu.Items.Add(regionItem);
            }
            menu.Items.Add(new ToolStripSeparator());
            var local = new ToolStripMenuItem("Local time");
            local.Click += delegate { _app.AddWidget(TimeZoneInfo.Local.Id, "Local"); };
            menu.Items.Add(local);
            var all = new ToolStripMenuItem("All time zones");
            foreach (var tz in ZoneCatalog.All)
            {
                var zone = tz;
                var item = new ToolStripMenuItem(tz.DisplayName);
                item.Click += delegate { _app.AddWidget(zone.Id, ZoneCatalog.ShortDisplayName(zone)); };
                all.DropDownItems.Add(item);
            }
            menu.Items.Add(all);
            menu.Show(anchor, new Point(0, anchor.Height));
        }
    }
}

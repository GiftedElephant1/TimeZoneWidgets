using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace TimeZoneWidgets
{
    /// <summary>
    /// "Find a city": type a few letters of a city, country, code (NYC) or zone name and pick from
    /// the matching catalog cities and Windows time zones. Works offline like everything else.
    /// </summary>
    public class CityPickerForm : Form
    {
        class Row
        {
            public string Text, Search, WindowsId, Label;
            public override string ToString() { return Text; }
        }

        readonly TextBox _box;
        readonly ListBox _list;
        readonly List<Row> _rows = new List<Row>();
        Row _picked;

        /// <summary>Shows the picker; returns true and the chosen zone/label, or false if cancelled.</summary>
        public static bool Show(IWin32Window owner, out string windowsId, out string label)
        {
            using (var f = new CityPickerForm())
            {
                var result = owner != null ? f.ShowDialog(owner) : f.ShowDialog();
                if (result == DialogResult.OK && f._picked != null)
                {
                    windowsId = f._picked.WindowsId;
                    label = f._picked.Label;
                    return true;
                }
            }
            windowsId = null;
            label = null;
            return false;
        }

        CityPickerForm()
        {
            Text = "Find a city";
            Font = new Font("Segoe UI", 9.5f);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(460, 420);

            var label = new Label();
            label.Text = "City, country, 3-letter code or time zone:";
            label.AutoSize = true;
            label.Location = new Point(12, 12);
            Controls.Add(label);

            _box = new TextBox();
            _box.Location = new Point(12, 34);
            _box.Width = 436;
            _box.TextChanged += delegate { Filter(); };
            _box.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Down) { MoveSelection(1); e.Handled = true; e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Up) { MoveSelection(-1); e.Handled = true; e.SuppressKeyPress = true; }
                else if (e.Control && e.KeyCode == Keys.A) { _box.SelectAll(); e.Handled = true; e.SuppressKeyPress = true; }
            };
            Controls.Add(_box);

            _list = new ListBox();
            _list.Location = new Point(12, 64);
            _list.Size = new Size(436, 300);
            _list.IntegralHeight = false;
            _list.DoubleClick += delegate { Accept(); };
            Controls.Add(_list);

            var ok = new Button();
            ok.Text = "Add";
            ok.Location = new Point(292, 378);
            ok.Size = new Size(75, 28);
            ok.Click += delegate { Accept(); };
            Controls.Add(ok);

            var cancel = new Button();
            cancel.Text = "Cancel";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Location = new Point(373, 378);
            cancel.Size = new Size(75, 28);
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;

            BuildRows();
            Filter();
        }

        void BuildRows()
        {
            var utc = DateTime.UtcNow;
            foreach (var region in ZoneCatalog.Regions)
            {
                foreach (var z in region.Zones)
                {
                    if (!ZoneCatalog.Exists(z.WindowsId)) continue;
                    var tz = ZoneCatalog.Find(z.WindowsId);
                    var row = new Row();
                    row.WindowsId = z.WindowsId;
                    row.Label = z.City;
                    string offset = ZoneCatalog.FormatOffset(tz.GetUtcOffset(utc));
                    row.Text = z.City + (z.Country.Length > 0 && z.Country != z.City ? ", " + z.Country : "") + "   " + z.Code + "   " + offset;
                    row.Search = (z.City + " " + z.Country + " " + z.Code + " " + region.Name + " " + tz.DisplayName + " " + z.WindowsId).ToLowerInvariant();
                    _rows.Add(row);
                }
            }
            // Every zone Windows knows, for places not in the catalog.
            foreach (var tz in ZoneCatalog.All)
            {
                var row = new Row();
                row.WindowsId = tz.Id;
                row.Label = ZoneCatalog.ShortDisplayName(tz);
                row.Text = "Time zone: " + tz.DisplayName;
                row.Search = (tz.DisplayName + " " + tz.Id).ToLowerInvariant();
                _rows.Add(row);
            }
        }

        static string Fold(string s)
        {
            // Accent-insensitive matching ("Zurich" finds "Zürich").
            var sb = new System.Text.StringBuilder();
            foreach (char ch in s.Normalize(System.Text.NormalizationForm.FormD))
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(ch);
            return sb.ToString();
        }

        void Filter()
        {
            var words = Fold(_box.Text.Trim().ToLowerInvariant()).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var row in _rows)
            {
                bool ok = true;
                var hay = Fold(row.Search);
                foreach (var w in words) if (hay.IndexOf(w, StringComparison.Ordinal) < 0) { ok = false; break; }
                if (ok) _list.Items.Add(row);
                if (_list.Items.Count >= 400) break;
            }
            _list.EndUpdate();
            if (_list.Items.Count > 0) _list.SelectedIndex = 0;
        }

        void MoveSelection(int delta)
        {
            if (_list.Items.Count == 0) return;
            int i = Math.Max(0, Math.Min(_list.Items.Count - 1, _list.SelectedIndex + delta));
            _list.SelectedIndex = i;
        }

        void Accept()
        {
            _picked = _list.SelectedItem as Row;
            if (_picked == null) return;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _box.Focus();
        }
    }
}

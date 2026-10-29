using System;
using System.Drawing;
using System.Windows.Forms;

namespace TimeZoneWidgets
{
    /// <summary>A tiny single-line input dialog (used for renaming a widget).</summary>
    public class PromptDialog : Form
    {
        readonly TextBox _box;

        PromptDialog(string title, string caption, string initial)
        {
            Text = title;
            Font = new Font("Segoe UI", 9.5f);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ClientSize = new Size(360, 120);

            var label = new Label();
            label.Text = caption;
            label.AutoSize = true;
            label.Location = new Point(12, 12);
            Controls.Add(label);

            _box = new TextBox();
            _box.Text = initial ?? "";
            _box.Location = new Point(12, 36);
            _box.Width = 336;
            Controls.Add(_box);

            var ok = new Button();
            ok.Text = "OK";
            ok.DialogResult = DialogResult.OK;
            ok.Location = new Point(192, 78);
            ok.Size = new Size(75, 28);
            Controls.Add(ok);

            var cancel = new Button();
            cancel.Text = "Cancel";
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Location = new Point(273, 78);
            cancel.Size = new Size(75, 28);
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _box.SelectAll();
            _box.Focus();
        }

        /// <summary>Returns the entered text, or null if the user cancelled.</summary>
        public static string Show(string title, string caption, string initial)
        {
            using (var dialog = new PromptDialog(title, caption, initial))
            {
                return dialog.ShowDialog() == DialogResult.OK ? dialog._box.Text : null;
            }
        }
    }
}

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace TimeZoneWidgets
{
    /// <summary>
    /// Base class for the floating widgets. Each one is a per-pixel-alpha layered window: the
    /// subclass paints its whole surface into an ARGB bitmap and calls <see cref="Push"/>, which
    /// hands it to Windows with UpdateLayeredWindow. That gives smooth rounded shapes, real
    /// transparency and click-through transparent areas without any child controls.
    /// </summary>
    public abstract class LayeredWindow : Form
    {
        const int WS_EX_LAYERED = 0x00080000;
        const int WS_EX_TOOLWINDOW = 0x00000080;
        const int WM_WINDOWPOSCHANGING = 0x0046;
        const int WM_DPICHANGED = 0x02E0;
        const uint SWP_NOZORDER = 0x0004;
        const byte AC_SRC_OVER = 0x00;
        const byte AC_SRC_ALPHA = 0x01;
        const int ULW_ALPHA = 0x02;
        static readonly IntPtr HWND_BOTTOM = new IntPtr(1);

        [StructLayout(LayoutKind.Sequential)]
        struct WINDOWPOS
        {
            public IntPtr hwnd;
            public IntPtr hwndInsertAfter;
            public int x, y, cx, cy;
            public uint flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct POINT { public int x, y; public POINT(int x, int y) { this.x = x; this.y = y; } }

        [StructLayout(LayoutKind.Sequential)]
        struct SIZE { public int cx, cy; public SIZE(int cx, int cy) { this.cx = cx; this.cy = cy; } }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct BLENDFUNCTION
        {
            public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat;
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref POINT pptDst, ref SIZE psize,
            IntPtr hdcSrc, ref POINT pptSrc, int crKey, ref BLENDFUNCTION pblend, int dwFlags);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hDC);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr hObject);

        protected readonly TrayApp App;
        protected AppSettings S;
        protected Theme T;
        protected float Dpi = 1f;

        bool _moving;
        Point _moveOffset;

        protected LayeredWindow(TrayApp app)
        {
            App = app;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            MinimizeBox = false;
            MaximizeBox = false;
        }

        /// <summary>Where this window's position is remembered (-1 means "not placed yet").</summary>
        public abstract int SavedX { get; set; }
        public abstract int SavedY { get; set; }

        /// <summary>Opacity in percent for the whole window.</summary>
        protected abstract int OpacityPercent { get; }

        /// <summary>Recompute fonts/size for the current settings and DPI.</summary>
        protected abstract void Relayout();

        /// <summary>Paint the window and push it to the screen.</summary>
        protected abstract void Render(bool force);

        /// <summary>User scale (1.0 = 100%) times monitor DPI scale: pixels per design pixel.</summary>
        protected float Px { get { return (S == null ? 100 : S.Scale) / 100f * Dpi; } }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Dpi = DeviceDpi / 96f;
            Relayout();
            Render(true);
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible && IsHandleCreated) Render(true);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_WINDOWPOSCHANGING && S != null && S.Layer == AppSettings.LayerBottom)
            {
                // Keep the widget glued to the bottom of the z-order (behind every other window).
                var wp = (WINDOWPOS)Marshal.PtrToStructure(m.LParam, typeof(WINDOWPOS));
                wp.hwndInsertAfter = HWND_BOTTOM;
                wp.flags &= ~SWP_NOZORDER;
                Marshal.StructureToPtr(wp, m.LParam, false);
            }
            else if (m.Msg == WM_DPICHANGED)
            {
                Dpi = ((int)m.WParam & 0xFFFF) / 96f;
                Relayout();
                Render(true);
                return;
            }
            base.WndProc(ref m);
        }

        public virtual void ApplySettings(AppSettings settings, Theme theme)
        {
            S = settings;
            T = theme;
            TopMost = settings.Layer == AppSettings.LayerTop;
            if (IsHandleCreated)
            {
                if (settings.Layer == AppSettings.LayerBottom) SendToBack();
                Relayout();
                Render(true);
            }
        }

        public void MoveTo(int x, int y)
        {
            Location = new Point(x, y);
            SavePosition();
        }

        protected void SavePosition()
        {
            if (SavedX != Left || SavedY != Top)
            {
                SavedX = Left;
                SavedY = Top;
                App.SaveSettings();
            }
        }

        // ---- window dragging (subclasses decide which mouse gesture starts it) -----------------

        protected bool IsMoving { get { return _moving; } }

        protected void BeginMove(Point mouseInWindow)
        {
            if (S != null && S.LockPositions) return;
            _moving = true;
            _moveOffset = mouseInWindow;
        }

        protected void ContinueMove()
        {
            if (!_moving) return;
            var p = Cursor.Position;
            Location = new Point(p.X - _moveOffset.X, p.Y - _moveOffset.Y);
        }

        protected void EndMove()
        {
            if (!_moving) return;
            _moving = false;
            SavePosition();
        }

        // ---- compositing ----------------------------------------------------------------------

        /// <summary>Hands a finished (non-premultiplied) 32bpp ARGB bitmap to the window manager.</summary>
        protected void Push(Bitmap bmp)
        {
            if (!IsHandleCreated) return;
            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr hBitmap = IntPtr.Zero;
            IntPtr oldBitmap = IntPtr.Zero;
            try
            {
                hBitmap = bmp.GetHbitmap(Color.FromArgb(0));
                oldBitmap = SelectObject(memDc, hBitmap);
                var size = new SIZE(bmp.Width, bmp.Height);
                var source = new POINT(0, 0);
                var topLeft = new POINT(Left, Top);
                var blend = new BLENDFUNCTION();
                blend.BlendOp = AC_SRC_OVER;
                blend.BlendFlags = 0;
                blend.SourceConstantAlpha = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(OpacityPercent * 2.55)));
                blend.AlphaFormat = AC_SRC_ALPHA;
                UpdateLayeredWindow(Handle, screenDc, ref topLeft, ref size, memDc, ref source, 0, ref blend, ULW_ALPHA);
            }
            finally
            {
                if (oldBitmap != IntPtr.Zero) SelectObject(memDc, oldBitmap);
                if (hBitmap != IntPtr.Zero) DeleteObject(hBitmap);
                DeleteDC(memDc);
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }
    }
}

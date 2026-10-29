using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace TimeZoneWidgets
{
    /// <summary>
    /// Registers a window as a Windows "appbar" so the shell reserves screen space for it along
    /// one edge, exactly like the taskbar does. Other windows then maximize next to it instead
    /// of underneath it.
    /// </summary>
    public class AppBar : IDisposable
    {
        const int ABM_NEW = 0;
        const int ABM_REMOVE = 1;
        const int ABM_QUERYPOS = 2;
        const int ABM_SETPOS = 3;
        const int ABM_WINDOWPOSCHANGED = 9;

        public const int ABE_LEFT = 0;
        public const int ABE_TOP = 1;
        public const int ABE_RIGHT = 2;
        public const int ABE_BOTTOM = 3;

        /// <summary>Sent to the window (as wParam of the callback message) when the shell wants us to reposition.</summary>
        public const int ABN_POSCHANGED = 1;

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int left, top, right, bottom; }

        [StructLayout(LayoutKind.Sequential)]
        struct APPBARDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public RECT rc;
            public IntPtr lParam;
        }

        [DllImport("shell32.dll")] static extern UIntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint RegisterWindowMessage(string name);

        readonly IntPtr _hwnd;
        bool _registered;
        public readonly uint CallbackMessage;

        public AppBar(IntPtr hwnd)
        {
            _hwnd = hwnd;
            CallbackMessage = RegisterWindowMessage("TimeZoneWidgets.AppBarCallback");
        }

        public bool IsRegistered { get { return _registered; } }

        APPBARDATA NewData()
        {
            var abd = new APPBARDATA();
            abd.cbSize = Marshal.SizeOf(typeof(APPBARDATA));
            abd.hWnd = _hwnd;
            abd.uCallbackMessage = CallbackMessage;
            return abd;
        }

        public void Register()
        {
            if (_registered) return;
            var abd = NewData();
            SHAppBarMessage(ABM_NEW, ref abd);
            _registered = true;
        }

        public void Unregister()
        {
            if (!_registered) return;
            var abd = NewData();
            SHAppBarMessage(ABM_REMOVE, ref abd);
            _registered = false;
        }

        /// <summary>Ask the shell for a slot of the given thickness on the given edge of the screen; returns the rectangle we were granted.</summary>
        public Rectangle SetPosition(int edge, int thickness, Rectangle screen)
        {
            Register();
            var abd = NewData();
            abd.uEdge = (uint)edge;
            abd.rc.left = screen.Left;
            abd.rc.top = screen.Top;
            abd.rc.right = screen.Right;
            abd.rc.bottom = screen.Bottom;
            switch (edge)
            {
                case ABE_TOP: abd.rc.bottom = abd.rc.top + thickness; break;
                case ABE_BOTTOM: abd.rc.top = abd.rc.bottom - thickness; break;
                case ABE_LEFT: abd.rc.right = abd.rc.left + thickness; break;
                default: abd.rc.left = abd.rc.right - thickness; break;
            }
            SHAppBarMessage(ABM_QUERYPOS, ref abd);
            // The shell may have nudged us away from the taskbar; keep our thickness on the edge it gave us.
            switch (edge)
            {
                case ABE_TOP: abd.rc.bottom = abd.rc.top + thickness; break;
                case ABE_BOTTOM: abd.rc.top = abd.rc.bottom - thickness; break;
                case ABE_LEFT: abd.rc.right = abd.rc.left + thickness; break;
                default: abd.rc.left = abd.rc.right - thickness; break;
            }
            SHAppBarMessage(ABM_SETPOS, ref abd);
            return Rectangle.FromLTRB(abd.rc.left, abd.rc.top, abd.rc.right, abd.rc.bottom);
        }

        public void NotifyPositionChanged()
        {
            if (!_registered) return;
            var abd = NewData();
            SHAppBarMessage(ABM_WINDOWPOSCHANGED, ref abd);
        }

        public void Dispose()
        {
            Unregister();
        }
    }
}

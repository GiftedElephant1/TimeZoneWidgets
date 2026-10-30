using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace TimeZoneWidgets
{
    /// <summary>
    /// Tells whether the window in front covers a whole monitor: an exclusive full-screen game,
    /// or a "windowed full screen" / borderless game such as Warcraft III or League of Legends
    /// set that way. The desktop itself, the taskbar and this app's own windows don't count.
    /// </summary>
    public static class FullscreenDetector
    {
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr hWnd, StringBuilder name, int max);
        [DllImport("shell32.dll")] static extern int SHQueryUserNotificationState(out int state);

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        const int QUNS_RUNNING_D3D_FULL_SCREEN = 3;   // an exclusive-mode Direct3D app is in front

        static readonly string[] ShellClasses = { "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "Windows.UI.Core.CoreWindow" };

        /// <summary>True when a full-screen app or game is in front on the given monitor.</summary>
        public static bool IsFullscreenAppOn(Screen screen)
        {
            try
            {
                int state;
                if (SHQueryUserNotificationState(out state) == 0 && state == QUNS_RUNNING_D3D_FULL_SCREEN) return true;

                var fg = GetForegroundWindow();
                if (fg == IntPtr.Zero) return false;
                uint pid;
                GetWindowThreadProcessId(fg, out pid);
                if (pid == (uint)Process.GetCurrentProcess().Id) return false;
                var cls = new StringBuilder(128);
                GetClassName(fg, cls, cls.Capacity);
                foreach (var shell in ShellClasses) if (cls.ToString() == shell) return false;
                RECT r;
                if (!GetWindowRect(fg, out r)) return false;
                var rect = Rectangle.FromLTRB(r.Left, r.Top, r.Right, r.Bottom);
                // Borderless "windowed full screen" games sit exactly on the monitor; exclusive ones
                // too. Ordinary maximized windows stop at the work area (or overhang it by their
                // invisible 8 px frame), so a 2 px tolerance tells them apart.
                var b = screen.Bounds;
                return rect.Left <= b.Left + 2 && rect.Top <= b.Top + 2 && rect.Right >= b.Right - 2 && rect.Bottom >= b.Bottom - 2
                    && rect.Width <= b.Width + 4 && rect.Height <= b.Height + 4;
            }
            catch
            {
                return false;
            }
        }
    }
}

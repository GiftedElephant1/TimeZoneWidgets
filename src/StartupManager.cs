using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace TimeZoneWidgets
{
    /// <summary>Registers / unregisters the app in the per-user Run key so it starts at sign-in. No admin rights needed.</summary>
    public static class StartupManager
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string ValueName = "TimeZoneWidgets";

        static string Command
        {
            get { return "\"" + Application.ExecutablePath + "\" --startup"; }
        }

        public static bool IsEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey))
                {
                    if (key == null) return false;
                    return key.GetValue(ValueName) as string != null;
                }
            }
            catch
            {
                return false;
            }
        }

        public static void SetEnabled(bool enabled)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (key == null) return;
                if (enabled) key.SetValue(ValueName, Command, RegistryValueKind.String);
                else key.DeleteValue(ValueName, false);
            }
        }

        /// <summary>If startup is enabled but the exe moved, point the Run entry at the current location.</summary>
        public static void RepairIfMoved()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    if (key == null) return;
                    var current = key.GetValue(ValueName) as string;
                    if (current != null && !string.Equals(current, Command, StringComparison.OrdinalIgnoreCase))
                        key.SetValue(ValueName, Command, RegistryValueKind.String);
                }
            }
            catch { }
        }
    }
}

using System;
using System.Threading;
using System.Windows.Forms;

namespace TimeZoneWidgets
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            bool createdNew;
            using (var mutex = new Mutex(true, @"Local\TimeZoneWidgets.SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    // Already running: ask the live instance to open its settings window instead.
                    try
                    {
                        using (var signal = EventWaitHandle.OpenExisting(TrayApp.ShowSettingsEventName))
                            signal.Set();
                    }
                    catch { }
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { ReportCrash(e.Exception); };
                AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e) { ReportCrash(e.ExceptionObject as Exception); };

                Application.Run(new TrayApp());
                GC.KeepAlive(mutex);
            }
        }

        static void ReportCrash(Exception ex)
        {
            try
            {
                MessageBox.Show("Time Zone Widgets hit an unexpected error:\n\n" + (ex == null ? "(unknown)" : ex.ToString()),
                    "Time Zone Widgets", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }
    }
}

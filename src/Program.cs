using System;
using System.Threading;
using System.Windows.Forms;

namespace AcrFfbGain
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool created;
            using (var mutex = new Mutex(true, "AcrFfbGain_SingleInstance", out created))
            {
                if (!created)
                {
                    MessageBox.Show("AC Rally FFB Gain is already running (check the system tray).", "AC Rally FFB Gain");
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += (s, e) => Log.Write("UI error: " + e.Exception);
                AppDomain.CurrentDomain.UnhandledException += (s, e) => Log.Write("Fatal: " + e.ExceptionObject);
                Application.Run(new MainForm(AppConfig.Load()));
            }
        }
    }
}

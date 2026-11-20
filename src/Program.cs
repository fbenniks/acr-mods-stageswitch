using System;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("AC Rally StageSwitch")]
[assembly: AssemblyProduct("AC Rally StageSwitch")]
[assembly: AssemblyDescription("Sets AC Rally's in-game settings per stage and car")]

namespace AcrFfbGain
{
    public static class AppInfo
    {
        public const string Name = "AC Rally StageSwitch";
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool created;
            using (var mutex = new Mutex(true, "ACRallyStageSwitch_SingleInstance", out created))
            {
                if (!created)
                {
                    MessageBox.Show(AppInfo.Name + " is already running (check the system tray).", AppInfo.Name);
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

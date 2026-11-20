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

    /// <summary>"Start with Windows": the current user's Run key (no admin rights needed).</summary>
    public static class Autostart
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

        static string Command { get { return "\"" + Application.ExecutablePath + "\""; } }

        public static bool Enabled
        {
            get
            {
                try
                {
                    using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey))
                        return k != null && k.GetValue(AppInfo.Name) != null;
                }
                catch { return false; }
            }
            set
            {
                try
                {
                    using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RunKey))
                    {
                        if (value) k.SetValue(AppInfo.Name, Command);
                        else if (k.GetValue(AppInfo.Name) != null) k.DeleteValue(AppInfo.Name);
                    }
                    Log.Write("Start with Windows: " + (value ? "on" : "off"));
                }
                catch (Exception ex) { Log.Write("Start with Windows: " + ex.Message); }
            }
        }

        /// <summary>Keeps the entry pointing at this exe if the app folder was moved.</summary>
        public static void Refresh()
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, true))
                {
                    var cur = k == null ? null : k.GetValue(AppInfo.Name) as string;
                    if (cur != null && cur != Command) k.SetValue(AppInfo.Name, Command);
                }
            }
            catch { }
        }
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
                Autostart.Refresh();
                Application.Run(new MainForm(AppConfig.Load()));
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace AcrFfbGain
{
    public class StageConfig
    {
        public string Id;          // e.g. "HafrenNorth" (or a raw telemetry track name for auto-detected stages)
        public string Name;        // display name, e.g. "Hafren North"
        public bool Enabled = true;
        public int Gain = 100;
        public bool Detected;      // true when added from telemetry rather than from the game files
        public bool NameFromGame;  // Name was taken from what the game reports for this stage
        public Dictionary<string, int> CarGains = new Dictionary<string, int>(); // optional per-car values (car Id -> gain)
    }

    public class CarInfo
    {
        public string Id;          // e.g. "SubaruImprezaS3" (livery folder name) or the telemetry name
        public string Name;        // e.g. "Subaru Impreza S3"
        public bool NameFromGame;  // Name was reported by the game itself
    }

    public class RallyConfig
    {
        public string Id;          // e.g. "Wales"
        public string Name;        // e.g. "Wales"
        public int Gain = 100;
        public List<StageConfig> Stages = new List<StageConfig>();
    }

    public class AppConfig
    {
        public string GameDir;
        public bool ShowOverlay = true;
        public int OverlaySeconds = 10;
        public bool StartMinimized;
        public int DefaultGain = 100;
        public bool PerCar;        // "Different gain per car" option
        public List<RallyConfig> Rallies = new List<RallyConfig>();
        public List<CarInfo> Cars = new List<CarInfo>();

        public static string Folder
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AcrFfbGain"); }
        }

        public static string FilePath
        {
            get { return Path.Combine(Folder, "settings.json"); }
        }

        public static AppConfig Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var cfg = new JavaScriptSerializer().Deserialize<AppConfig>(File.ReadAllText(FilePath));
                    if (cfg != null)
                    {
                        if (cfg.Rallies == null) cfg.Rallies = new List<RallyConfig>();
                        if (cfg.Cars == null) cfg.Cars = new List<CarInfo>();
                        foreach (var r in cfg.Rallies)
                        {
                            if (r.Stages == null) r.Stages = new List<StageConfig>();
                            foreach (var s in r.Stages) if (s.CarGains == null) s.CarGains = new Dictionary<string, int>();
                        }
                        if (cfg.OverlaySeconds <= 0) cfg.OverlaySeconds = 10;
                        return cfg;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Write("Could not read settings: " + ex.Message);
            }
            return new AppConfig();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Folder);
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, new JavaScriptSerializer().Serialize(this));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch (Exception ex)
            {
                Log.Write("Could not save settings: " + ex.Message);
            }
        }

        public CarInfo FindCar(string idOrName)
        {
            string n = StageScanner.Norm(idOrName);
            if (n.Length == 0) return null;
            foreach (var c in Cars)
                if (StageScanner.Norm(c.Id) == n || StageScanner.Norm(c.Name) == n) return c;
            return null;
        }

        public RallyConfig FindRally(string id)
        {
            foreach (var r in Rallies)
                if (string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase)) return r;
            return null;
        }
    }

    /// <summary>Runtime state shared with the UI.</summary>
    public static class AppState
    {
        public static string CurrentCarId; // car on the current stage, if any
    }

    public static class Log
    {
        static readonly object Gate = new object();

        public static void Write(string msg)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(AppConfig.Folder);
                    string path = Path.Combine(AppConfig.Folder, "log.txt");
                    var fi = new FileInfo(path);
                    if (fi.Exists && fi.Length > 1024 * 1024) fi.Delete();
                    File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + msg + Environment.NewLine);
                }
            }
            catch { }
        }
    }
}

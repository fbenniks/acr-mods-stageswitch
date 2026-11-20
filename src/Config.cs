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
        public int ResScale;       // render resolution scale in % (33–200); 0 only until filled in from the game's setting
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
        public int ResScale;       // render resolution scale in % for all stages
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
        public bool Learn = true;  // "Learn from in-game changes": settings changed in the game on a stage are stored for that stage
        public List<RallyConfig> Rallies = new List<RallyConfig>();
        public List<CarInfo> Cars = new List<CarInfo>();

        public static string Folder
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AC Rally StageSwitch"); }
        }

        /// <summary>Settings folder used before the rename to StageSwitch (kept as a backup after migrating).</summary>
        static string OldFolder
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AcrFfbGain"); }
        }

        /// <summary>Copies settings (and log) from the old "AcrFfbGain" folder the first time the renamed app runs.</summary>
        static void MigrateFromOldFolder()
        {
            try
            {
                string oldSettings = Path.Combine(OldFolder, "settings.json");
                if (File.Exists(FilePath) || !File.Exists(oldSettings)) return;
                Directory.CreateDirectory(Folder);
                File.Copy(oldSettings, FilePath);
                string oldLog = Path.Combine(OldFolder, "log.txt");
                string newLog = Path.Combine(Folder, "log.txt");
                if (File.Exists(oldLog) && !File.Exists(newLog)) File.Copy(oldLog, newLog);
                Log.Write("Settings copied from " + OldFolder + " (old folder kept as backup)");
            }
            catch (Exception ex)
            {
                Log.Write("Could not copy old settings: " + ex.Message);
            }
        }

        public static string FilePath
        {
            get { return Path.Combine(Folder, "settings.json"); }
        }

        public static AppConfig Load()
        {
            MigrateFromOldFolder();
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

        /// <summary>Every stage has a resolution scale: new ones start at the game's own menu value.</summary>
        public bool FillMissingResScale(int gameValue)
        {
            int v = Math.Max(33, Math.Min(200, gameValue));
            bool changed = false;
            foreach (var r in Rallies)
            {
                foreach (var s in r.Stages) if (s.ResScale <= 0) { s.ResScale = r.ResScale > 0 ? r.ResScale : v; changed = true; }
                if (r.ResScale <= 0) { r.ResScale = r.Stages.Count > 0 ? r.Stages[0].ResScale : v; changed = true; }
            }
            return changed;
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
        public static int Upscaler = -1;   // from the game's settings: 2 TSR, 3 DLSS, 4 XeSS, 5 FSR
        public static int GameResScale = 100; // resolution scale set in the game's menu

        public static string UpscalerName { get { return GameGraphics.UpscalerName(Upscaler); } }

        /// <summary>Starting value when a resolution scale is first set for a stage.</summary>
        public static int ResStart() { return Math.Max(33, Math.Min(200, GameResScale)); }

        /// <summary>DLSS resolution presets (shown values) and the exact ratios DLSS uses.</summary>
        public static readonly int[] DlssPresets = { 33, 50, 58, 67, 100 };
        static readonly string[] DlssNames = { "Ultra Performance", "Performance", "Balanced", "Quality", "DLAA" };
        static readonly float[] DlssExact = { 100f / 3f, 50f, 58f, 200f / 3f, 100f };

        public static bool IsDlss { get { return UpscalerName == "DLSS"; } }

        /// <summary>
        /// Measured in-game: when the game's own menu is on DLSS 100 % (DLAA), DLSS stays in DLAA and
        /// ignores lower values written by the app. From any other DLSS preset the app can set every
        /// value, including 100 % (DLAA).
        /// </summary>
        public static bool DlaaLocked { get { return IsDlss && GameResScale >= 100; } }

        /// <summary>
        /// Stored stage value -> the value used with the current upscaler. With DLSS this copies what the
        /// game itself does (measured in-game): nearest preset below 100 %, DLAA only at 100 % and above.
        ///   33-34 -> 33.3 | 45-51 -> 50 | 55-60 -> 58 | 63-99 -> 66.7 | 100+ -> 100
        /// </summary>
        public static int SnapRes(int v)
        {
            if (!IsDlss) return v;
            return SnapDlss(v);
        }

        public static int SnapDlss(int v)
        {
            if (v >= 100) return 100;
            int best = DlssPresets[0];
            for (int i = 0; i < DlssPresets.Length - 1; i++)  // all presets except DLAA
                if (Math.Abs(DlssPresets[i] - v) < Math.Abs(best - v)) best = DlssPresets[i];
            return best;
        }

        /// <summary>The exact percentage written to the game (DLSS Quality = 66.67 %, not 67).</summary>
        public static float ResWriteValue(int v)
        {
            if (!IsDlss) return v;
            int i = Array.IndexOf(DlssPresets, SnapRes(v));
            return DlssExact[i];
        }

        public static string DlssName(int v)
        {
            int i = Array.IndexOf(DlssPresets, SnapRes(v));
            return i >= 0 ? DlssNames[i] : null;
        }

        /// <summary>Tooltip for the Res scale column; lists the DLSS presets while DLSS is selected.</summary>
        public static string ResTooltip()
        {
            string t = "Render resolution scale (33–200%).";
            if (IsDlss)
            {
                t = "Render resolution scale.\n\nWith DLSS the game only uses DLSS presets, so − / + step through them:\n" +
                    "   33%   Ultra Performance\n" +
                    "   50%   Performance\n" +
                    "   58%   Balanced\n" +
                    "   67%   Quality\n" +
                    " 100%   DLAA\n\n" +
                    "In the game's own menu other values are rounded the same way: anything from about\n" +
                    "63% to 99% (including the Ultra preset's 80%) is really Quality, 100% and up is DLAA.\n\n" +
                    "Other upscalers (TSR, FSR, XeSS) allow any value from 33 to 200%.";
            }
            else if (UpscalerName != null)
                t += "\nUpscaler: " + UpscalerName + " (from the game's graphics settings).";
            return t;
        }

        /// <summary>A stepper set up for the resolution scale column (DLSS: presets only).</summary>
        public static GainStepper NewResStepper()
        {
            return new GainStepper { Minimum = 33, Maximum = 200, Suffix = "%", Choices = IsDlss ? DlssPresets : null, Snap = IsDlss ? (Func<int, int>)SnapDlss : null };
        }
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

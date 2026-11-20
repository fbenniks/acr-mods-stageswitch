using System;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Text;

namespace AcrFfbGain
{
    public class TelemetrySnapshot
    {
        public bool GameRunning;
        public bool SharedMemory;
        public int Status;       // 0 = off, 1 = replay, 2 = live, 3 = pause
        public string Track = "";
        public string Car = "";
        public int ProcessId;
    }

    /// <summary>
    /// Reads AC Rally's shared memory, which uses the ACC layout
    /// (Local\acpmf_static and Local\acpmf_graphics).
    /// </summary>
    public static class Telemetry
    {
        public static readonly string[] ProcessNames = { "acr-Win64-Shipping", "acr" };

        const int StaticCarModelOffset = 68;   // wchar_t[33]
        const int StaticTrackOffset = 134;     // wchar_t[33]
        const int GraphicsStatusOffset = 4;    // int

        public static Process FindGame()
        {
            foreach (var n in ProcessNames)
            {
                var ps = Process.GetProcessesByName(n);
                foreach (var p in ps)
                {
                    try { if (!p.HasExited) return p; } catch { }
                }
            }
            return null;
        }

        public static TelemetrySnapshot Read()
        {
            var snap = new TelemetrySnapshot();
            var proc = FindGame();
            if (proc == null) return snap;
            snap.GameRunning = true;
            snap.ProcessId = proc.Id;

            try
            {
                using (var mmf = MemoryMappedFile.OpenExisting("Local\\acpmf_static", MemoryMappedFileRights.Read))
                using (var acc = mmf.CreateViewAccessor(0, 512, MemoryMappedFileAccess.Read))
                {
                    snap.Car = ReadWide(acc, StaticCarModelOffset, 33);
                    snap.Track = ReadWide(acc, StaticTrackOffset, 33);
                    snap.SharedMemory = true;
                }
                using (var mmf = MemoryMappedFile.OpenExisting("Local\\acpmf_graphics", MemoryMappedFileRights.Read))
                using (var acc = mmf.CreateViewAccessor(0, 64, MemoryMappedFileAccess.Read))
                {
                    snap.Status = acc.ReadInt32(GraphicsStatusOffset);
                }
            }
            catch
            {
                // Shared memory is not there (yet).
            }
            return snap;
        }

        static string ReadWide(MemoryMappedViewAccessor acc, int offset, int chars)
        {
            var buf = new byte[chars * 2];
            acc.ReadArray(offset, buf, 0, buf.Length);
            string s = Encoding.Unicode.GetString(buf);
            int z = s.IndexOf('\0');
            if (z >= 0) s = s.Substring(0, z);
            return s.Trim();
        }

        public static string StatusText(int status)
        {
            switch (status)
            {
                case 0: return "menu";
                case 1: return "replay";
                case 2: return "driving";
                case 3: return "paused";
                default: return "status " + status;
            }
        }
    }
}

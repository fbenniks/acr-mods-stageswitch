using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace AcrFfbGain
{
    /// <summary>
    /// The game's graphics choices as stored in its save file, next to the graphics preset name
    /// ("Custom"): the upscaler (one byte) and the resolution scale shown in the menu (float, percent).
    /// </summary>
    public static class GameGraphics
    {
        // Layout (game version 2026-11), relative to the "EAlwaysOnScalabilityQuality" FString that follows:
        //   ... [upscaler byte] ...40 bytes... [preset FString "Custom"] [int] [float resScale] [float 100] [int] [int len]"EAlwaysOn..."
        const string Anchor = "EAlwaysOnScalabilityQuality";

        public static bool TryRead(out int upscaler, out float resScale)
        {
            upscaler = -1;
            resScale = 0;
            byte[] b = GainWriter.ReadSave();
            if (b == null) return false;
            string s = Encoding.GetEncoding(28591).GetString(b);
            int a = s.IndexOf(Anchor, StringComparison.Ordinal) - 4;   // its length prefix
            if (a < 64) return false;
            resScale = BitConverter.ToSingle(b, a - 12);
            if (!(resScale >= 10f && resScale <= 400f)) return false;

            // preset FString ends where the int before resScale starts (a - 16); find its length prefix
            int end = a - 16, p = -1;
            for (int q = end - 5; q > end - 80 && q >= 0; q--)
            {
                if (BitConverter.ToInt32(b, q) == end - (q + 4)) { p = q; break; }
            }
            if (p >= 40) upscaler = b[p - 40];
            return true;
        }

        public static string UpscalerName(int id)
        {
            switch (id)
            {
                case 2: return "TSR";
                case 3: return "DLSS";
                case 4: return "XeSS";
                case 5: return "FSR";
                default: return null;
            }
        }
    }

    /// <summary>
    /// Changes the render resolution scale live by writing the engine's r.ScreenPercentage console
    /// variable (the value the renderer uses each frame), and keeps the game's settings copies in step
    /// so the graphics menu shows the same value.
    ///
    /// The console variable is recognised by its help text, which is the engine's own and doesn't move
    /// between game versions: object = [vtable][Help FString ptr, len, max] ... value, value (+80).
    /// </summary>
    public class ResScaleWriter
    {
        const string HelpStart = "To render in lower resolution and upscale";
        const int ValueOffset = 80;   // value pair relative to the object start (vtable)
        const int MaxMenuCopies = 6;

        readonly object _gate = new object();
        int _pid;
        long _cvar;                               // address of the first float of the value pair
        readonly List<long> _menuCopies = new List<long>();
        bool _menuSearched;

        public void Reset()
        {
            lock (_gate) { _pid = 0; _cvar = 0; _menuCopies.Clear(); _menuSearched = false; }
        }

        /// <summary>
        /// Finds the render-scale setting ahead of time (it takes a few seconds), without changing it.
        /// Returns true once it is found; call again later if not (early in the game's start-up it may not exist yet).
        /// </summary>
        public bool Warmup(int pid)
        {
            lock (_gate)
            {
                try
                {
                    using (var mem = new ProcessMemory(pid))
                    {
                        if (pid != _pid) { _pid = pid; _cvar = 0; _menuCopies.Clear(); _menuSearched = false; }
                        if (_cvar != 0 && !IsCvar(mem, _cvar, Module(pid))) _cvar = 0;
                        if (_cvar == 0) _cvar = FindCvar(mem, pid);
                        if (_cvar != 0 && _menuCopies.Count == 0 && !_menuSearched) { _menuCopies.AddRange(FindMenuCopies(mem)); _menuSearched = true; }
                        Log.Write("ResScale warm-up: r.ScreenPercentage " + (_cvar != 0 ? "found" : "not found yet") + ", " + _menuCopies.Count + " menu copies");
                        return _cvar != 0;
                    }
                }
                catch (Exception ex) { Log.Write("ResScale warm-up: " + ex.Message); return false; }
            }
        }

        public bool Apply(int pid, float percent, out string error)
        {
            lock (_gate)
            {
                error = null;
                float v = Math.Max(33f, Math.Min(200f, percent));
                try
                {
                    using (var mem = new ProcessMemory(pid))
                    {
                        if (pid != _pid) { _pid = pid; _cvar = 0; _menuCopies.Clear(); _menuSearched = false; }
                        if (_cvar == 0 || !IsCvar(mem, _cvar, Module(pid)))
                        {
                            _cvar = FindCvar(mem, pid);
                            Log.Write("ResScale: r.ScreenPercentage " + (_cvar != 0 ? "found" : "NOT found"));
                        }
                        if (_cvar == 0) { error = "render scale setting not found (game update?)"; return false; }

                        var bytes = BitConverter.GetBytes(v);
                        bool ok = mem.Write(_cvar, bytes) & mem.Write(_cvar + 4, bytes);
                        if (!ok) { error = "could not write to game memory"; return false; }

                        // menu copies: searched once per game launch (keeps the graphics menu in step)
                        _menuCopies.RemoveAll(a => !IsMenuCopy(mem, a));
                        if (_menuCopies.Count == 0 && !_menuSearched) { _menuCopies.AddRange(FindMenuCopies(mem)); _menuSearched = true; }
                        foreach (var a in _menuCopies) mem.Write(a, bytes);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    Log.Write("ResScale error: " + ex);
                    return false;
                }
            }
        }

        static long[] Module(int pid)
        {
            try
            {
                var m = Process.GetProcessById(pid).MainModule;
                long b = m.BaseAddress.ToInt64();
                return new[] { b, b + m.ModuleMemorySize };
            }
            catch { return new long[] { 0x7FF000000000, 0x7FFFFFFFFFFF }; }
        }

        long FindCvar(ProcessMemory mem, int pid)
        {
            var mod = Module(pid);
            // value pair (v, v) with a plausible percentage, object header 80 bytes before it
            var candidates = mem.Scan((buf, i, len) =>
            {
                if ((i & 3) != 0 || i - ValueOffset < 0 || i + 8 > len) return false;
                int a0 = BitConverter.ToInt32(buf, i);
                if (a0 != BitConverter.ToInt32(buf, i + 4)) return false;
                float f = BitConverter.ToSingle(buf, i);
                if (!(f >= 1f && f <= 400f)) return false;
                int o = i - ValueOffset;
                long vtable = BitConverter.ToInt64(buf, o);
                if (vtable < mod[0] || vtable >= mod[1]) return false;
                int helpLen = BitConverter.ToInt32(buf, o + 16);
                return helpLen > HelpStart.Length && helpLen < 4000 && BitConverter.ToInt64(buf, o + 8) > 0x10000;
            }, 2000, ValueOffset, 8);

            foreach (var c in candidates)
                if (IsCvar(mem, c, mod)) return c;
            return 0;
        }

        static bool IsCvar(ProcessMemory mem, long value, long[] mod)
        {
            var hdr = mem.Read(value - ValueOffset, ValueOffset + 8);
            if (hdr == null) return false;
            long vtable = BitConverter.ToInt64(hdr, 0);
            if (vtable < mod[0] || vtable >= mod[1]) return false;
            long help = BitConverter.ToInt64(hdr, 8);
            var text = mem.Read(help, HelpStart.Length * 2);
            return text != null && Encoding.Unicode.GetString(text) == HelpStart;
        }

        // Settings copies: [resScale][100.0][16 quality bytes, each 0..6]
        static bool LooksLikeMenuCopy(byte[] buf, int i)
        {
            float f = BitConverter.ToSingle(buf, i);
            if (!(f >= 10f && f <= 400f)) return false;
            if (BitConverter.ToSingle(buf, i + 4) != 100f) return false;
            for (int k = 8; k < 24; k++) if (buf[i + k] > 6) return false;
            return true;
        }

        static bool IsMenuCopy(ProcessMemory mem, long a)
        {
            var b = mem.Read(a, 24);
            return b != null && LooksLikeMenuCopy(b, 0);
        }

        static List<long> FindMenuCopies(ProcessMemory mem)
        {
            int up; float saved;
            if (!GameGraphics.TryRead(out up, out saved)) return new List<long>();
            int bits = BitConverter.ToInt32(BitConverter.GetBytes(saved), 0);
            var found = mem.Scan((buf, i, len) =>
                i + 24 <= len && BitConverter.ToInt32(buf, i) == bits && LooksLikeMenuCopy(buf, i), 16, 0, 24);
            // The game keeps 2-4 copies. Many more means the pattern also matched unrelated data
            // (seen right after start-up): don't write to any of them, the menu just won't update.
            if (found.Count > MaxMenuCopies)
            {
                Log.Write("ResScale: " + found.Count + " possible menu copies — too many to be sure, not using them");
                return new List<long>();
            }
            Log.Write("ResScale: " + found.Count + " menu copies");
            return found;
        }
    }
}

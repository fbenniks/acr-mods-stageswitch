using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AcrFfbGain
{
    /// <summary>
    /// Changes the in-game FFB gain of the running game.
    ///
    /// AC Rally keeps its player settings in a struct that it also writes, byte for byte, into
    /// %LOCALAPPDATA%\acr\Saved\SaveGames\PlayerDataSaveSlot.sav (rewritten whenever settings are applied).
    /// The FFB gain is a float multiplier (150 in the menu = 1.5f). The game holds a few copies of the
    /// struct in memory; writing the value into all of them changes the force live and the menu shows it.
    ///
    /// To find the copies we take the bytes around the gain from the save file and look for the same
    /// bytes in the game's memory. This follows the user's own settings, so changing other options
    /// doesn't break it.
    /// </summary>
    public class GainWriter
    {
        // Byte window around the gain used as the fingerprint. Within this window the save file and the
        // in-memory struct line up exactly; further back the save is packed differently (padding).
        const int Before = 48;
        const int After = 64;

        // Where the gain sits in the save, relative to two string anchors (game version 2026-11).
        //   end of the FString that precedes the struct ("Normal\0") + 96
        //   start of the "Custom" FString length prefix - 261
        const int OffsetFromNormalEnd = 96;
        const int OffsetBeforeCustom = 261;

        readonly object _gate = new object();
        int _pid;
        List<long> _addresses = new List<long>();
        int[] _sigIdx;   // word offsets (relative to gain, in bytes) that must match
        int[] _sigVal;

        public static string SavePath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"acr\Saved\SaveGames\PlayerDataSaveSlot.sav"); }
        }

        /// <summary>Reads the game's save file (the game may be writing it: shared access, short retries).</summary>
        public static byte[] ReadSave()
        {
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    using (var fs = new FileStream(SavePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (var ms = new MemoryStream()) { fs.CopyTo(ms); return ms.ToArray(); }
                }
                catch (FileNotFoundException) { return null; }
                catch (DirectoryNotFoundException) { return null; }
                catch (Exception) { System.Threading.Thread.Sleep(200); }
            }
            Log.Write("Could not read " + SavePath);
            return null;
        }

        public void Reset()
        {
            lock (_gate) { _pid = 0; _addresses.Clear(); _sigIdx = null; }
        }

        /// <summary>Applies the gain (0–200). Blocking: may scan the game's memory (several seconds).</summary>
        public bool Apply(int pid, int gainPercent, bool fullRescan, out string error)
        {
            lock (_gate)
            {
                error = null;
                float value = Math.Max(0, Math.Min(200, gainPercent)) / 100f;
                try
                {
                    using (var mem = new ProcessMemory(pid))
                    {
                        if (pid != _pid) { _pid = pid; _addresses.Clear(); }

                        // Keep only cached addresses that still look right.
                        if (_sigIdx != null)
                            _addresses = _addresses.Where(a => Matches(mem, a)).ToList();

                        if (fullRescan || _addresses.Count == 0)
                        {
                            if (!BuildSignature(out error)) return false;
                            var found = Find(mem);
                            foreach (var a in found) if (!_addresses.Contains(a)) _addresses.Add(a);
                            Log.Write("GainWriter: found " + found.Count + " settings copies");
                        }

                        if (_addresses.Count == 0)
                        {
                            error = "FFB setting not found in game memory (game update?)";
                            return false;
                        }

                        int ok = 0;
                        foreach (var a in _addresses)
                        {
                            if (mem.WriteFloat(a, value))
                            {
                                var back = mem.ReadFloat(a);
                                if (back.HasValue && Math.Abs(back.Value - value) < 1e-5) ok++;
                            }
                        }
                        if (ok == 0)
                        {
                            error = "could not write to game memory";
                            return false;
                        }
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                    Log.Write("GainWriter error: " + ex);
                    return false;
                }
            }
        }

        /// <summary>Reads the gain the game currently uses (0–200), or null.</summary>
        public int? ReadCurrent(int pid)
        {
            lock (_gate)
            {
                if (pid != _pid || _addresses.Count == 0) return null;
                try
                {
                    using (var mem = new ProcessMemory(pid))
                    {
                        var f = mem.ReadFloat(_addresses[0]);
                        if (!f.HasValue) return null;
                        return (int)Math.Round(f.Value * 100);
                    }
                }
                catch { return null; }
            }
        }

        bool BuildSignature(out string error)
        {
            error = null;
            byte[] save = ReadSave();
            if (save == null) { error = "could not read the game's save file"; return false; }

            string s = Encoding.GetEncoding(28591).GetString(save);
            var candidates = new List<int>();
            int normal = s.IndexOf("Normal\0", StringComparison.Ordinal);
            if (normal > 0) candidates.Add(normal + 7 + OffsetFromNormalEnd);
            int custom = s.IndexOf("Custom\0", StringComparison.Ordinal);
            if (custom > 4) candidates.Add(custom - 4 - OffsetBeforeCustom);

            foreach (int k in candidates.Distinct())
            {
                if (k - Before < 0 || k + After > save.Length) continue;
                float g = BitConverter.ToSingle(save, k);
                if (!(g >= 0f && g <= 2.0001f)) continue;

                var idx = new List<int>();
                var val = new List<int>();
                for (int off = -Before; off < After; off += 4)
                {
                    if (off == 0) continue;
                    int w = BitConverter.ToInt32(save, k + off);
                    if (w == 0) continue; // zero in the save can be padding with junk in memory
                    idx.Add(off);
                    val.Add(w);
                }
                if (idx.Count < 5) continue;
                _sigIdx = idx.ToArray();
                _sigVal = val.ToArray();
                Log.Write("GainWriter: signature from save offset " + k + " (" + idx.Count + " words, saved gain " + Math.Round(g * 100) + ")");
                return true;
            }
            error = "save file layout not recognised (game update?)";
            return false;
        }

        List<long> Find(ProcessMemory mem)
        {
            var idx = _sigIdx;
            var val = _sigVal;
            int allowedMisses = idx.Length >= 12 ? 1 : 0;
            return mem.Scan((buf, i, len) =>
            {
                if (i - Before < 0 || i + After > len) return false;
                int misses = 0;
                for (int n = 0; n < idx.Length; n++)
                {
                    if (BitConverter.ToInt32(buf, i + idx[n]) != val[n] && ++misses > allowedMisses) return false;
                }
                float g = BitConverter.ToSingle(buf, i);
                return g >= 0f && g <= 2.0001f;
            }, 64, Before, After);
        }

        bool Matches(ProcessMemory mem, long addr)
        {
            var buf = mem.Read(addr - Before, Before + After);
            if (buf == null) return false;
            float g = BitConverter.ToSingle(buf, Before);
            if (!(g >= 0f && g <= 2.0001f)) return false;
            int misses = 0, allowed = _sigIdx.Length >= 12 ? 1 : 0;
            for (int n = 0; n < _sigIdx.Length; n++)
                if (BitConverter.ToInt32(buf, Before + _sigIdx[n]) != _sigVal[n] && ++misses > allowed) return false;
            return true;
        }
    }
}

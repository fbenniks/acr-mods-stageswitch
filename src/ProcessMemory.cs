using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace AcrFfbGain
{
    /// <summary>Minimal read/write/scan access to another process's memory.</summary>
    public sealed class ProcessMemory : IDisposable
    {
        public readonly int Pid;
        IntPtr _h;

        public ProcessMemory(int pid)
        {
            Pid = pid;
            _h = OpenProcess(PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION | PROCESS_QUERY_INFORMATION, false, pid);
            if (_h == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error(), "OpenProcess failed");
        }

        public void Dispose()
        {
            if (_h != IntPtr.Zero) { CloseHandle(_h); _h = IntPtr.Zero; }
        }

        public struct Region
        {
            public long Base;
            public long Size;
        }

        /// <summary>Committed, private, read-write regions (where the game's heap objects live).</summary>
        public List<Region> WritableRegions()
        {
            var list = new List<Region>();
            long addr = 0x10000;
            long max = 0x7FFFFFFF0000;
            var mbi = new MEMORY_BASIC_INFORMATION();
            int sz = Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION));
            while (addr < max)
            {
                if (VirtualQueryEx(_h, new IntPtr(addr), out mbi, new IntPtr(sz)) == IntPtr.Zero) break;
                long b = mbi.BaseAddress.ToInt64();
                long s = mbi.RegionSize.ToInt64();
                if (s <= 0) break;
                if (mbi.State == MEM_COMMIT && mbi.Type == MEM_PRIVATE && mbi.Protect == PAGE_READWRITE)
                    list.Add(new Region { Base = b, Size = s });
                addr = b + s;
            }
            return list;
        }

        public byte[] Read(long address, int count)
        {
            var buf = new byte[count];
            IntPtr read;
            if (!ReadProcessMemory(_h, new IntPtr(address), buf, new IntPtr(count), out read)) return null;
            if (read.ToInt64() != count) return null;
            return buf;
        }

        public bool Write(long address, byte[] data)
        {
            IntPtr written;
            return WriteProcessMemory(_h, new IntPtr(address), data, new IntPtr(data.Length), out written) && written.ToInt64() == data.Length;
        }

        public float? ReadFloat(long address)
        {
            var b = Read(address, 4);
            if (b == null) return null;
            return BitConverter.ToSingle(b, 0);
        }

        public bool WriteFloat(long address, float value)
        {
            return Write(address, BitConverter.GetBytes(value));
        }

        /// <summary>
        /// Scans writable memory. For every 4-byte aligned offset, <paramref name="match"/> is called
        /// with the chunk buffer and index; return true to record the address.
        /// </summary>
        public List<long> Scan(Func<byte[], int, int, bool> match, int maxResults, int lookBehind, int lookAhead)
        {
            var results = new List<long>();
            const int Chunk = 4 * 1024 * 1024;
            foreach (var r in WritableRegions())
            {
                if (r.Size > 512L * 1024 * 1024) continue;
                for (long off = 0; off < r.Size; off += Chunk)
                {
                    long start = Math.Max(r.Base, r.Base + off - lookBehind);
                    long end = Math.Min(r.Base + r.Size, r.Base + off + Chunk + lookAhead);
                    int len = (int)(end - start);
                    var buf = Read(start, len);
                    if (buf == null) continue;
                    int first = (int)(r.Base + off - start);
                    int last = Math.Min(len - lookAhead, first + Chunk);
                    for (int i = first; i + 4 <= last; i += 4)
                    {
                        if (match(buf, i, len))
                        {
                            results.Add(start + i);
                            if (results.Count >= maxResults) return results;
                        }
                    }
                }
            }
            return results;
        }

        const uint PROCESS_VM_READ = 0x0010, PROCESS_VM_WRITE = 0x0020, PROCESS_VM_OPERATION = 0x0008, PROCESS_QUERY_INFORMATION = 0x0400;
        const uint MEM_COMMIT = 0x1000, MEM_PRIVATE = 0x20000, PAGE_READWRITE = 0x04;

        [StructLayout(LayoutKind.Sequential)]
        struct MEMORY_BASIC_INFORMATION
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public uint AllocationProtect;
            public uint __alignment1;
            public IntPtr RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
            public uint __alignment2;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr h);
        [DllImport("kernel32.dll")]
        static extern IntPtr VirtualQueryEx(IntPtr h, IntPtr addr, out MEMORY_BASIC_INFORMATION mbi, IntPtr len);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadProcessMemory(IntPtr h, IntPtr addr, [Out] byte[] buf, IntPtr size, out IntPtr read);
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteProcessMemory(IntPtr h, IntPtr addr, byte[] buf, IntPtr size, out IntPtr written);
    }
}

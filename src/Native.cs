using System;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace CacheTrim
{
    /// <summary>
    /// Thin wrapper over the Win32/NT primitives RAMMap uses for memory reclamation.
    /// Every call fails closed: an error string comes back instead of a silent success.
    /// </summary>
    internal static class Native
    {
        // ---------------- tokens and privileges ----------------

        private const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
        private const uint TOKEN_QUERY = 0x0008;
        private const uint SE_PRIVILEGE_ENABLED = 0x0002;
        private const int ERROR_NOT_ALL_ASSIGNED = 1300;

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID_AND_ATTRIBUTES
        {
            public LUID Luid;
            public uint Attributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct TOKEN_PRIVILEGES
        {
            public uint PrivilegeCount;
            public LUID_AND_ATTRIBUTES Privileges;
        }

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, out LUID lpLuid);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, bool DisableAllPrivileges,
            ref TOKEN_PRIVILEGES NewState, int BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        public static bool EnablePrivilege(string name)
        {
            IntPtr token;
            if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out token))
                return false;

            try
            {
                LUID luid;
                if (!LookupPrivilegeValue(null, name, out luid))
                    return false;

                TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES();
                tp.PrivilegeCount = 1;
                tp.Privileges.Luid = luid;
                tp.Privileges.Attributes = SE_PRIVILEGE_ENABLED;

                if (!AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero))
                    return false;

                // AdjustTokenPrivileges reports success even when the privilege is absent.
                return Marshal.GetLastWin32Error() != ERROR_NOT_ALL_ASSIGNED;
            }
            finally
            {
                CloseHandle(token);
            }
        }

        public static bool IsElevated
        {
            get
            {
                using (WindowsIdentity id = WindowsIdentity.GetCurrent())
                {
                    return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
                }
            }
        }

        // ---------------- memory reclamation ----------------

        private const int SystemFileCacheInformation = 0x15;
        private const int SystemMemoryListInformation = 0x50;

        // SYSTEM_MEMORY_LIST_COMMAND
        public enum PurgeTarget
        {
            WorkingSets = 2,
            ModifiedList = 3,
            StandbyList = 4,
            LowPriorityStandby = 5
        }

        [DllImport("ntdll.dll")]
        private static extern int NtSetSystemInformation(int SystemInformationClass, IntPtr SystemInformation, int SystemInformationLength);

        [DllImport("ntdll.dll")]
        private static extern int NtQuerySystemInformation(int SystemInformationClass, IntPtr SystemInformation,
            int SystemInformationLength, out int ReturnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetSystemFileCacheSize(IntPtr MinimumFileCacheSize, IntPtr MaximumFileCacheSize, uint Flags);

        /// <summary>Returns null on success, otherwise a human-readable error.</summary>
        public static string Purge(PurgeTarget target)
        {
            EnablePrivilege("SeProfileSingleProcessPrivilege");

            IntPtr buffer = Marshal.AllocHGlobal(sizeof(int));
            try
            {
                Marshal.WriteInt32(buffer, (int)target);
                int status = NtSetSystemInformation(SystemMemoryListInformation, buffer, sizeof(int));
                if (status >= 0)
                    return null;

                return string.Format("NtSetSystemInformation(0x{0:X8}) для {1}", status, target);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        /// <summary>Trims the kernel file cache back to its default size. Needs SeIncreaseQuotaPrivilege.</summary>
        public static string FlushSystemFileCache()
        {
            if (!EnablePrivilege("SeIncreaseQuotaPrivilege"))
                return "нет привилегии SeIncreaseQuotaPrivilege (запуск без админа?)";

            // -1/-1 = restore the default cache ceiling; the kernel trims immediately.
            if (!SetSystemFileCacheSize(new IntPtr(-1), new IntPtr(-1), 0))
                return "SetSystemFileCacheSize: " + Marshal.GetLastWin32Error();

            return null;
        }

        // ---------------- counters ----------------

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [StructLayout(LayoutKind.Sequential)]
        private struct PERFORMANCE_INFORMATION
        {
            public uint cb;
            public IntPtr CommitTotal;
            public IntPtr CommitLimit;
            public IntPtr CommitPeak;
            public IntPtr PhysicalTotal;
            public IntPtr PhysicalAvailable;
            public IntPtr SystemCache;
            public IntPtr KernelTotal;
            public IntPtr KernelPaged;
            public IntPtr KernelNonpaged;
            public IntPtr PageSize;
            public uint HandleCount;
            public uint ProcessCount;
            public uint ThreadCount;
        }

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool GetPerformanceInfo(out PERFORMANCE_INFORMATION pPerformanceInformation, uint cb);

        [DllImport("dnsapi.dll")]
        private static extern int DnsFlushResolverCache();

        public static bool FlushDnsCache()
        {
            return DnsFlushResolverCache() != 0;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);

        public static bool EmptyRecycleBin()
        {
            const uint NOCONFIRMATION = 0x00000001;
            const uint NOPROGRESSUI = 0x00000002;
            const uint NOSOUND = 0x00000004;
            int hr = SHEmptyRecycleBin(IntPtr.Zero, null, NOCONFIRMATION | NOPROGRESSUI | NOSOUND);
            return hr == 0; // S_OK; S_FALSE when already empty counts as cleaned
        }

        public sealed class MemStats
        {
            public ulong TotalPhys;
            public ulong AvailPhys;
            public ulong Standby;
            public ulong Modified;
            public ulong Free;
            public ulong Cache;
            public ulong CommitUsed;
            public ulong CommitLimit;
            public uint ProcessCount;
            public uint HandleCount;
            public bool QueryFailed;
        }

        public static MemStats GetMemoryStats()
        {
            MemStats s = new MemStats();

            MEMORYSTATUSEX ms = new MEMORYSTATUSEX();
            ms.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            if (GlobalMemoryStatusEx(ref ms))
            {
                s.TotalPhys = ms.ullTotalPhys;
                s.AvailPhys = ms.ullAvailPhys;
            }

            PERFORMANCE_INFORMATION pi;
            if (GetPerformanceInfo(out pi, (uint)Marshal.SizeOf(typeof(PERFORMANCE_INFORMATION))))
            {
                ulong page = (ulong)pi.PageSize.ToInt64();
                s.Cache = (ulong)pi.SystemCache.ToInt64() * page;
                s.CommitUsed = (ulong)pi.CommitTotal.ToInt64() * page;
                s.CommitLimit = (ulong)pi.CommitLimit.ToInt64() * page;
                s.AvailPhys = (ulong)pi.PhysicalAvailable.ToInt64() * page;
                s.ProcessCount = pi.ProcessCount;
                s.HandleCount = pi.HandleCount;
                s.Free = page; // keep a sane page size even if the NT query fails
            }
            else
            {
                s.QueryFailed = true;
            }

            // Available = standby + free + zero, so standby falls out exactly once
            // the NT query gives us the free and zero lists.
            ulong pageSize = s.Free;
            s.Free = 0;
            ulong zeroPages = 0;
            ulong freePages = 0;
            ulong modifiedPages = 0;
            if (QueryMemoryLists(out zeroPages, out freePages, out modifiedPages))
            {
                s.Free = (zeroPages + freePages) * pageSize;
                s.Modified = modifiedPages * pageSize;
                s.Standby = s.AvailPhys > s.Free ? s.AvailPhys - s.Free : 0;
            }
            else
            {
                s.QueryFailed = true;
            }

            return s;
        }

        private static bool QueryMemoryLists(out ulong zeroPages, out ulong freePages, out ulong modifiedPages)
        {
            zeroPages = 0;
            freePages = 0;
            modifiedPages = 0;

            // SYSTEM_MEMORY_LIST_INFORMATION; the first five fields have been stable since Vista.
            const int fields = 21;
            int size = fields * IntPtr.Size + 64;
            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                int returned;
                int status = NtQuerySystemInformation(SystemMemoryListInformation, buffer, size, out returned);
                if (status < 0)
                    return false;

                zeroPages = (ulong)Marshal.ReadIntPtr(buffer, 0).ToInt64();
                freePages = (ulong)Marshal.ReadIntPtr(buffer, IntPtr.Size).ToInt64();
                modifiedPages = (ulong)Marshal.ReadIntPtr(buffer, IntPtr.Size * 2).ToInt64();
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        // ------------------------------------------------------------------ window chrome

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, StringBuilder lParam);

        /// <summary>
        /// Paints the system title bar dark. Attribute 20 exists from Windows 10 2004;
        /// 19 is the 1809..1909 name for the same flag, so both are tried.
        /// </summary>
        public static void UseDarkTitleBar(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
                return;

            int on = 1;
            if (DwmSetWindowAttribute(hwnd, 20, ref on, sizeof(int)) != 0)
                DwmSetWindowAttribute(hwnd, 19, ref on, sizeof(int));
        }

        /// <summary>Scrolls a multiline edit control by whole lines; used by the log view.</summary>
        public static void ScrollLines(IntPtr handle, int lines)
        {
            const int EM_LINESCROLL = 0x00B6;
            if (handle != IntPtr.Zero)
                SendMessage(handle, EM_LINESCROLL, IntPtr.Zero, (IntPtr)lines);
        }

        public static int FirstVisibleLine(IntPtr handle)
        {
            const int EM_GETFIRSTVISIBLELINE = 0x00CE;
            return handle == IntPtr.Zero ? 0 : (int)SendMessage(handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero);
        }

        public static int LineCount(IntPtr handle)
        {
            const int EM_GETLINECOUNT = 0x00BA;
            return handle == IntPtr.Zero ? 0 : (int)SendMessage(handle, EM_GETLINECOUNT, IntPtr.Zero, IntPtr.Zero);
        }
    }
}

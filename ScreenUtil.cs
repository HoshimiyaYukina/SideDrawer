using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace SideDrawer
{
    /// <summary>按窗口所在显示器 + 实际 DPI 计算工作区（SystemParameters 在 125% 缩放下会返回过期值）。</summary>
    internal static class ScreenUtil
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO mi);
        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

        private const uint MONITOR_DEFAULTTONEAREST = 2;

        public static Rect WorkAreaDIP(Visual v)
        {
            try
            {
                Window w = v as Window;
                IntPtr h = w != null ? new WindowInteropHelper(w).Handle : IntPtr.Zero;
                IntPtr mon = MonitorFromWindow(h, MONITOR_DEFAULTTONEAREST);
                if (mon != IntPtr.Zero)
                {
                    MONITORINFO mi = new MONITORINFO();
                    mi.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                    if (GetMonitorInfo(mon, ref mi))
                    {
                        double scale = 0;
                        try
                        {
                            object o = Microsoft.Win32.Registry.GetValue(
                                @"HKEY_CURRENT_USER\Control Panel\Desktop\WindowMetrics", "AppliedDPI", 96);
                            if (o != null) scale = Convert.ToDouble(o) / 96.0;
                        }
                        catch { }
                        if (scale <= 0.5) { try { scale = Glass.Dpi(v)[0]; } catch { scale = 1; } }
                        if (scale <= 0.1) scale = 1;
                        double left = mi.rcWork.Left / scale;
                        double top = mi.rcWork.Top / scale;
                        double wd = (mi.rcWork.Right - mi.rcWork.Left) / scale;
                        double ht = (mi.rcWork.Bottom - mi.rcWork.Top) / scale;
                        if (wd > 100 && ht > 100) return new Rect(left, top, wd, ht);
                    }
                }
            }
            catch { }
            return SystemParameters.WorkArea;
        }
    }
}

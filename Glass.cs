using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;

namespace SideDrawer
{
    /// <summary>共享的“液态玻璃”渲染工具：截屏 -> 模糊 -> 生成可复用的模糊底图。</summary>
    internal static class Glass
    {
        public static double[] Dpi(Visual v)
        {
            double sx = 1, sy = 1;
            try
            {
                PresentationSource ps = PresentationSource.FromVisual(v);
                if (ps != null && ps.CompositionTarget != null)
                {
                    Matrix m = ps.CompositionTarget.TransformToDevice;
                    sx = m.M11;
                    sy = m.M22;
                }
            }
            catch { }
            return new double[] { sx, sy };
        }

        public static BitmapSource Capture(double left, double top, double w, double h, Visual dpiFrom)
        {
            double[] d = Dpi(dpiFrom);
            int x = (int)Math.Round(left * d[0]);
            int y = (int)Math.Round(top * d[1]);
            int cw = (int)Math.Round(w * d[0]);
            int ch = (int)Math.Round(h * d[1]);
            if (cw < 4 || ch < 4) return null;

            using (System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(cw, ch, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmp))
                    g.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(cw, ch),
                        System.Drawing.CopyPixelOperation.SourceCopy);

                System.Drawing.Imaging.BitmapData data = bmp.LockBits(
                    new System.Drawing.Rectangle(0, 0, cw, ch),
                    System.Drawing.Imaging.ImageLockMode.ReadOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                try
                {
                    int stride = data.Stride;
                    byte[] buf = new byte[stride * ch];
                    Marshal.Copy(data.Scan0, buf, 0, buf.Length);
                    BitmapSource raw = BitmapSource.Create(cw, ch, 96, 96, PixelFormats.Bgra32, null, buf, stride);
                    raw.Freeze();
                    return raw;
                }
                finally { bmp.UnlockBits(data); }
            }
        }

        public static BitmapSource RenderElement(FrameworkElement el, int w, int h)
        {
            el.Width = w;
            el.Height = h;
            el.Measure(new Size(w, h));
            el.Arrange(new Rect(0, 0, w, h));
            el.UpdateLayout();
            RenderTargetBitmap rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(el);
            rtb.Freeze();
            return rtb;
        }

        public static ImageSource BlurDown(BitmapSource src, int w, int h)
        {
            int f = 10;
            int sw = Math.Max(2, w / f);
            int sh = Math.Max(2, h / f);

            Image small = new Image();
            small.Source = src;
            small.Stretch = Stretch.Fill;
            BlurEffect blur = new BlurEffect();
            blur.KernelType = KernelType.Gaussian;
            blur.Radius = Math.Max(4, Math.Min(14, sw / 4.0));
            blur.RenderingBias = RenderingBias.Quality;
            small.Effect = blur;
            BitmapSource mid = RenderElement(small, sw, sh);

            Image big = new Image();
            big.Source = mid;
            big.Stretch = Stretch.Fill;
            RenderOptions.SetBitmapScalingMode(big, BitmapScalingMode.HighQuality);
            return RenderElement(big, w, h);
        }

        /// <summary>截取矩形区域并模糊，返回可直接当背景笔刷用的图。</summary>
        public static ImageSource Backdrop(double left, double top, double w, double h, Visual dpiFrom)
        {
            try
            {
                BitmapSource raw = Capture(left, top, w, h, dpiFrom);
                if (raw == null) return null;
                return BlurDown(raw, (int)Math.Round(w), (int)Math.Round(h));
            }
            catch { return null; }
        }

        /// <summary>在后台线程完成截屏与 3-pass 盒式近似高斯模糊，零 UI 线程阻塞。</summary>
        public static void BackdropAsync(double left, double top, double w, double h, Visual dpiFrom, Action<ImageSource> onReady)
        {
            if (onReady == null) return;
            double[] d = Dpi(dpiFrom);
            int x = (int)Math.Round(left * d[0]);
            int y = (int)Math.Round(top * d[1]);
            int cw = (int)Math.Round(w * d[0]);
            int ch = (int)Math.Round(h * d[1]);
            if (cw < 4 || ch < 4) return;

            Dispatcher disp = dpiFrom != null ? dpiFrom.Dispatcher : Dispatcher.CurrentDispatcher;
            ThreadPool.QueueUserWorkItem(delegate
            {
                ImageSource img = null;
                try { img = CaptureAndBlurOffThread(x, y, cw, ch); }
                catch { img = null; }
                if (img == null || disp == null) return;
                try
                {
                    disp.BeginInvoke(DispatcherPriority.Render, new Action(delegate { onReady(img); }));
                }
                catch { }
            });
        }

        private static BitmapSource CaptureAndBlurOffThread(int x, int y, int cw, int ch)
        {
            int f = 6;
            int sw = Math.Max(6, cw / f);
            int sh = Math.Max(6, ch / f);

            using (System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(cw, ch, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
            {
                using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmp))
                    g.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(cw, ch), System.Drawing.CopyPixelOperation.SourceCopy);

                using (System.Drawing.Bitmap small = new System.Drawing.Bitmap(sw, sh, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    using (System.Drawing.Graphics g2 = System.Drawing.Graphics.FromImage(small))
                    {
                        g2.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
                        g2.DrawImage(bmp, new System.Drawing.Rectangle(0, 0, sw, sh));
                    }

                    System.Drawing.Imaging.BitmapData data = small.LockBits(
                        new System.Drawing.Rectangle(0, 0, sw, sh),
                        System.Drawing.Imaging.ImageLockMode.ReadOnly,
                        System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    try
                    {
                        int stride = data.Stride;
                        byte[] buf = new byte[stride * sh];
                        Marshal.Copy(data.Scan0, buf, 0, buf.Length);

                        int rx = Math.Max(2, Math.Min(8, sw / 5));
                        int ry = Math.Max(2, Math.Min(8, sh / 5));
                        BoxBlur3(buf, sw, sh, stride, rx, ry);

                        BitmapSource bs = BitmapSource.Create(sw, sh, 96, 96, PixelFormats.Bgra32, null, buf, stride);
                        bs.Freeze();
                        return bs;
                    }
                    finally { small.UnlockBits(data); }
                }
            }
        }

        private static void BoxBlur3(byte[] buf, int w, int h, int stride, int rx, int ry)
        {
            byte[] tmp = new byte[buf.Length];
            for (int pass = 0; pass < 3; pass++)
            {
                BoxBlurH(buf, tmp, w, h, stride, rx);
                BoxBlurV(tmp, buf, w, h, stride, ry);
            }
        }

        private static void BoxBlurH(byte[] src, byte[] dst, int w, int h, int stride, int r)
        {
            int div = r * 2 + 1;
            for (int y = 0; y < h; y++)
            {
                int row = y * stride;
                int sb = 0, sg = 0, sr = 0;
                for (int i = -r; i <= r; i++)
                {
                    int xi = i < 0 ? 0 : (i >= w ? w - 1 : i);
                    int idx = row + xi * 4;
                    sb += src[idx];
                    sg += src[idx + 1];
                    sr += src[idx + 2];
                }
                for (int x = 0; x < w; x++)
                {
                    int o = row + x * 4;
                    dst[o] = (byte)(sb / div);
                    dst[o + 1] = (byte)(sg / div);
                    dst[o + 2] = (byte)(sr / div);
                    dst[o + 3] = 255;

                    int addX = x + r + 1; if (addX >= w) addX = w - 1;
                    int subX = x - r; if (subX < 0) subX = 0;
                    int ia = row + addX * 4;
                    int isub = row + subX * 4;
                    sb += src[ia] - src[isub];
                    sg += src[ia + 1] - src[isub + 1];
                    sr += src[ia + 2] - src[isub + 2];
                }
            }
        }

        private static void BoxBlurV(byte[] src, byte[] dst, int w, int h, int stride, int r)
        {
            int div = r * 2 + 1;
            for (int x = 0; x < w; x++)
            {
                int col = x * 4;
                int sb = 0, sg = 0, sr = 0;
                for (int i = -r; i <= r; i++)
                {
                    int yi = i < 0 ? 0 : (i >= h ? h - 1 : i);
                    int idx = yi * stride + col;
                    sb += src[idx];
                    sg += src[idx + 1];
                    sr += src[idx + 2];
                }
                for (int y = 0; y < h; y++)
                {
                    int o = y * stride + col;
                    dst[o] = (byte)(sb / div);
                    dst[o + 1] = (byte)(sg / div);
                    dst[o + 2] = (byte)(sr / div);
                    dst[o + 3] = 255;

                    int addY = y + r + 1; if (addY >= h) addY = h - 1;
                    int subY = y - r; if (subY < 0) subY = 0;
                    int ia = addY * stride + col;
                    int isub = subY * stride + col;
                    sb += src[ia] - src[isub];
                    sg += src[ia + 1] - src[isub + 1];
                    sr += src[ia + 2] - src[isub + 2];
                }
            }
        }

        public static BitmapCache MakeCache(Visual dpiFrom)
        {
            double s = 1.0;
            try
            {
                double[] d = Dpi(dpiFrom);
                if (d[0] >= 1.0 && d[0] <= 2.5) s = d[0];
            }
            catch { }
            BitmapCache bc = new BitmapCache(s);
            bc.SnapsToDevicePixels = true;
            return bc;
        }

        public static Brush Mask(Geometry geo, double w, double h)
        {
            DrawingBrush db = new DrawingBrush();
            db.Drawing = new GeometryDrawing(Brushes.White, null, geo);
            db.Viewbox = new Rect(0, 0, w, h);
            db.ViewboxUnits = BrushMappingMode.Absolute;
            db.Viewport = new Rect(0, 0, w, h);
            db.ViewportUnits = BrushMappingMode.Absolute;
            db.Stretch = Stretch.Fill;
            db.Freeze();
            return db;
        }

        public static ImageSource Noise(int w, int h, int seed)
        {
            Random rnd = new Random(seed);
            int stride = w * 4;
            byte[] px = new byte[stride * h];
            for (int i = 0; i < w * h; i++)
            {
                byte v = (byte)rnd.Next(0, 255);
                px[i * 4] = v;
                px[i * 4 + 1] = v;
                px[i * 4 + 2] = v;
                px[i * 4 + 3] = 255;
            }
            BitmapSource bs = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, px, stride);
            bs.Freeze();
            return bs;
        }

        public static void Shadow(UIElement el, double blur, double opacity, Color? color)
        {
            DropShadowEffect e = new DropShadowEffect();
            e.Color = color.HasValue ? color.Value : Colors.Black;
            e.BlurRadius = blur;
            e.ShadowDepth = 0;
            e.Opacity = opacity;
            e.Freeze();
            el.Effect = e;
        }
    }

    /// <summary>Windows 10/11 原生 DWM 实时亚克力毛玻璃工具（零截屏、不随失焦变灰、硬件圆角阴影）。</summary>
    internal static class DwmGlass
    {
        public const int ACCENT_DISABLED = 0;
        public const int ACCENT_ENABLE_BLURBEHIND = 3;
        public const int ACCENT_ENABLE_ACRYLICBLURBEHIND = 4;

        private const int WCA_ACCENT_POLICY = 19;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_BORDER_COLOR = 34;
        private const int DWMWCP_ROUND = 2;
        private const int DWMWA_COLOR_NONE = unchecked((int)0xFFFFFFFE);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const uint SWP_ASYNCWINDOWPOS = 0x4000;
        private const int SW_HIDE = 0;
        private const int SW_SHOWNA = 8;

        [StructLayout(LayoutKind.Sequential)]
        private struct MARGINS
        {
            public int Left, Right, Top, Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy
        {
            public int AccentState;
            public int AccentFlags;
            public uint GradientColor;
            public int AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowCompositionAttributeData
        {
            public int Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("winmm.dll")]
        private static extern uint timeBeginPeriod(uint uPeriod);

        [DllImport("winmm.dll")]
        private static extern uint timeEndPeriod(uint uPeriod);

        /// <summary>在窗口构造阶段配置无边框 DWM 透明帧（不使用 AllowsTransparency）。</summary>
        public static void PrepareWindow(Window w)
        {
            w.WindowStyle = WindowStyle.None;
            w.AllowsTransparency = false;
            w.ResizeMode = ResizeMode.NoResize;
            w.Background = Brushes.Transparent;

            WindowChrome chrome = new WindowChrome();
            chrome.CaptionHeight = 0;
            chrome.ResizeBorderThickness = new Thickness(0);
            chrome.GlassFrameThickness = new Thickness(-1);
            chrome.CornerRadius = new CornerRadius(0);
            WindowChrome.SetWindowChrome(w, chrome);
        }

        /// <summary>在 OnSourceInitialized 中启用 DWM 透明合成、Win11 硬件圆角与可选实时毛玻璃。</summary>
        public static void InitFrame(Window w, bool enableBlur, byte a, byte r, byte g, byte b)
        {
            try
            {
                IntPtr h = new WindowInteropHelper(w).Handle;
                if (h == IntPtr.Zero) return;

                HwndSource src = HwndSource.FromHwnd(h);
                if (src != null && src.CompositionTarget != null)
                    src.CompositionTarget.BackgroundColor = Colors.Transparent;

                MARGINS m = new MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
                DwmExtendFrameIntoClientArea(h, ref m);

                int corner = DWMWCP_ROUND;
                DwmSetWindowAttribute(h, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));

                int noBorder = DWMWA_COLOR_NONE;
                DwmSetWindowAttribute(h, DWMWA_BORDER_COLOR, ref noBorder, sizeof(int));

                // 使用 ACCENT_ENABLE_BLURBEHIND(3)：纯实时毛玻璃模糊，不叠加 Win11 强白/强黑亮度遮罩
                SetAccent(h, enableBlur ? ACCENT_ENABLE_BLURBEHIND : ACCENT_DISABLED, a, r, g, b, false);
            }
            catch { }
        }

        public static void SetAccent(IntPtr hwnd, int accentState, byte a, byte r, byte g, byte b)
        {
            SetAccent(hwnd, accentState, a, r, g, b, false);
        }

        public static void SetAccent(IntPtr hwnd, int accentState, byte a, byte r, byte g, byte b, bool rebindVisible)
        {
            if (hwnd == IntPtr.Zero) return;
            try
            {
                // 动态切换模糊开关时，通过 SW_HIDE -> 设属性 -> SW_SHOWNA 强制 Win11 DWM 立即重建合成表面，杜绝纯黑帧
                if (rebindVisible) ShowWindow(hwnd, SW_HIDE);

                MARGINS m = new MARGINS { Left = -1, Right = -1, Top = -1, Bottom = -1 };
                DwmExtendFrameIntoClientArea(hwnd, ref m);

                AccentPolicy accent = new AccentPolicy();
                accent.AccentState = accentState;
                if (accentState == ACCENT_ENABLE_ACRYLICBLURBEHIND)
                {
                    if (a < 1) a = 1;
                    accent.AccentFlags = 2;
                    accent.GradientColor = ((uint)a << 24) | ((uint)b << 16) | ((uint)g << 8) | (uint)r;
                }
                else if (accentState == ACCENT_ENABLE_BLURBEHIND)
                {
                    accent.AccentFlags = 0;
                    accent.GradientColor = 0;
                }

                int size = Marshal.SizeOf(typeof(AccentPolicy));
                IntPtr ptr = Marshal.AllocHGlobal(size);
                try
                {
                    Marshal.StructureToPtr(accent, ptr, false);
                    WindowCompositionAttributeData data = new WindowCompositionAttributeData();
                    data.Attribute = WCA_ACCENT_POLICY;
                    data.SizeOfData = size;
                    data.Data = ptr;
                    SetWindowCompositionAttribute(hwnd, ref data);
                }
                finally { Marshal.FreeHGlobal(ptr); }

                int corner = DWMWCP_ROUND;
                DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
                int noBorder = DWMWA_COLOR_NONE;
                DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref noBorder, sizeof(int));
                SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);

                if (rebindVisible)
                {
                    ShowWindow(hwnd, SW_SHOWNA);
                    Native.KeepTopmost(hwnd);
                }
            }
            catch { }
        }

        public static void MovePx(IntPtr hwnd, int xPx, int yPx)
        {
            if (hwnd == IntPtr.Zero) return;
            SetWindowPos(hwnd, IntPtr.Zero, xPx, yPx, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE);
        }

        public static void MovePxAsync(IntPtr hwnd, int xPx, int yPx)
        {
            if (hwnd == IntPtr.Zero) return;
            SetWindowPos(hwnd, IntPtr.Zero, xPx, yPx, 0, 0, SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS);
        }

        /// <summary>
        /// 独立高精度时钟线程驱动 DWM 窗口平滑滑动（~160-180fps）：
        /// 彻底绕开 WPF MediaContext 在屏外窗口被 Win11 节流到 4fps 导致的“直接闪现”问题。
        /// </summary>
        public static void SlideHorizontalAsync(
            IntPtr hwnd,
            int fromXPx,
            int toXPx,
            int yPx,
            double durMs,
            bool easeOut,
            Func<bool> isCancelled,
            Action<int, double> onCompleted)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                int frames = 0;
                double totalMs = 0;
                bool periodSet = false;
                try
                {
                    periodSet = (timeBeginPeriod(1) == 0);
                    MovePxAsync(hwnd, fromXPx, yPx);
                    long start = System.Diagnostics.Stopwatch.GetTimestamp();
                    double freq = System.Diagnostics.Stopwatch.Frequency;
                    int lastX = fromXPx;

                    while (true)
                    {
                        if (isCancelled != null && isCancelled()) return;
                        double elapsed = (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / freq;
                        double t = durMs > 0 ? elapsed / durMs : 1.0;
                        if (t >= 1.0)
                        {
                            totalMs = elapsed;
                            break;
                        }
                        double ease = easeOut
                            ? (1.0 - Math.Pow(1.0 - t, 3.0))
                            : (t * t * t);
                        int curX = fromXPx + (int)Math.Round((toXPx - fromXPx) * ease);
                        if (curX != lastX)
                        {
                            MovePxAsync(hwnd, curX, yPx);
                            lastX = curX;
                            frames++;
                        }
                        Thread.Sleep(5);
                    }

                    if (isCancelled == null || !isCancelled())
                    {
                        MovePxAsync(hwnd, toXPx, yPx);
                        frames++;
                        if (onCompleted != null) onCompleted(frames, totalMs);
                    }
                }
                catch
                {
                    if (onCompleted != null && (isCancelled == null || !isCancelled()))
                        onCompleted(frames, totalMs);
                }
                finally
                {
                    if (periodSet) { try { timeEndPeriod(1); } catch { } }
                }
            });
        }
    }
}

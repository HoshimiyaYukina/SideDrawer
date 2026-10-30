using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

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

        public static Brush Mask(Geometry geo, double w, double h)
        {
            DrawingBrush db = new DrawingBrush();
            db.Drawing = new GeometryDrawing(Brushes.White, null, geo);
            db.Viewbox = new Rect(0, 0, w, h);
            db.ViewboxUnits = BrushMappingMode.Absolute;
            db.Viewport = new Rect(0, 0, w, h);
            db.ViewportUnits = BrushMappingMode.Absolute;
            db.Stretch = Stretch.Fill;
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
            el.Effect = e;
        }
    }
}

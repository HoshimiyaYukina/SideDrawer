using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace SideDrawer
{
    /// <summary>细线包边 + 四角线稿樱花（纯线条，无填充）。</summary>
    internal static class OrnateFrame
    {
        public static Canvas Create(double w, double h, double inset, Color line, double opacity)
        {
            Canvas c = new Canvas();
            c.Width = w;
            c.Height = h;
            c.IsHitTestVisible = false;

            SolidColorBrush stroke = new SolidColorBrush(Color.FromArgb((byte)(opacity * 255), line.R, line.G, line.B));

            // 内圈
            Border inner = new Border();
            inner.Width = w - inset * 2;
            inner.Height = h - inset * 2;
            inner.CornerRadius = new CornerRadius(14);
            inner.BorderThickness = new Thickness(1.2);
            inner.BorderBrush = stroke;
            inner.Background = Brushes.Transparent;
            Canvas.SetLeft(inner, inset);
            Canvas.SetTop(inner, inset);
            c.Children.Add(inner);

            // 外圈（更淡）
            Border outer = new Border();
            outer.Width = w - 14;
            outer.Height = h - 14;
            outer.CornerRadius = new CornerRadius(16);
            outer.BorderThickness = new Thickness(1);
            outer.BorderBrush = new SolidColorBrush(Color.FromArgb((byte)(opacity * 90), line.R, line.G, line.B));
            Canvas.SetLeft(outer, 7);
            Canvas.SetTop(outer, 7);
            c.Children.Add(outer);

            // 四角樱花：花瓣外缘与包边相切（不压住线条）
            double r = 20;
            double d = inset + r * 0.96;   // 花心到角的距离 ≈ 花瓣半径 → 尖端刚好贴线
            c.Children.Add(Sakura(d, d, r, stroke, 225));
            c.Children.Add(Sakura(w - d, d, r, stroke, 315));
            c.Children.Add(Sakura(d, h - d, r, stroke, 135));
            c.Children.Add(Sakura(w - d, h - d, r, stroke, 45));

            // 线条投影：整层一次性加，避免每个元素单独开效果
            DropShadowEffect sh = new DropShadowEffect();
            sh.Color = Color.FromRgb(24, 6, 12);
            sh.BlurRadius = 5;
            sh.ShadowDepth = 1;
            sh.Direction = 270;
            sh.Opacity = 0.55;
            c.Effect = sh;

            return c;
        }
        /// <summary>从包边角延伸到樱花的枝蔓（带一个小卷）。</summary>
        private static UIElement Tendril(double x0, double y0, double x1, double y1, int sx, int sy, Brush stroke)
        {
            Canvas cc = new Canvas();
            double left = Math.Min(x0, x1) - 24;
            double top = Math.Min(y0, y1) - 24;
            cc.Width = Math.Abs(x1 - x0) + 70;
            cc.Height = Math.Abs(y1 - y0) + 70;
            Canvas.SetLeft(cc, left);
            Canvas.SetTop(cc, top);

            double ax = x0 - left, ay = y0 - top;
            double bx = x1 - left, by = y1 - top;

            Path p = new Path();
            p.Data = Geometry.Parse(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "M{0},{1} C{2},{3} {4},{5} {6},{7}",
                F(ax), F(ay),
                F(ax + (bx - ax) * 0.1), F(ay + (by - ay) * 0.75),
                F(ax + (bx - ax) * 0.8), F(ay + (by - ay) * 0.15),
                F(bx), F(by)));
            p.Stroke = stroke;
            p.StrokeThickness = 1.0;
            p.StrokeLineJoin = PenLineJoin.Round;
            cc.Children.Add(p);

            return cc;
        }
        /// <summary>线稿樱花：花瓣只描边，中心一个小圈 + 花蕊短线。</summary>
        /// <summary>线稿樱花：带缺口的花瓣，只描边。</summary>
        private static UIElement Sakura(double cx, double cy, double r, Brush stroke, double spin)
        {
            Canvas cc = new Canvas();
            cc.Width = r * 2 + 10;
            cc.Height = r * 2 + 10;
            double mid = r + 5;

            string petal = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "M0,{0} C{1},{2} {3},{4} {5},{6} L0,{7} L{8},{6} C{9},{4} {10},{2} 0,{0} Z",
                F(-r * 0.05),
                F(-r * 0.50), F(-r * 0.30),
                F(-r * 0.95), F(-r * 0.72),
                F(-r * 0.28), F(-r * 0.94),
                F(-r * 0.74),
                F(r * 0.28),
                F(r * 0.95), F(-r * 0.72),
                F(r * 0.50));

            for (int i = 0; i < 5; i++)
            {
                Path p = new Path();
                p.Data = Geometry.Parse(petal);
                p.Stroke = stroke;
                p.StrokeThickness = 1.3;
                p.StrokeLineJoin = PenLineJoin.Round;
                p.Fill = null;
                p.RenderTransform = new RotateTransform(i * 72.0 + spin);
                Canvas.SetLeft(p, mid);
                Canvas.SetTop(p, mid);
                cc.Children.Add(p);
            }

            Ellipse core = new Ellipse();
            core.Width = r * 0.30;
            core.Height = r * 0.30;
            core.Stroke = stroke;
            core.StrokeThickness = 1;
            core.Fill = null;
            Canvas.SetLeft(core, mid - r * 0.15);
            Canvas.SetTop(core, mid - r * 0.15);
            cc.Children.Add(core);


            Canvas.SetLeft(cc, cx - mid);
            Canvas.SetTop(cc, cy - mid);
            return cc;
        }
        private static string F(double v)
        {
            return v.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}

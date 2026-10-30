using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace SideDrawer
{
    /// <summary>贴在屏幕左边缘的书签：鼠标靠近才滑出，平时完全不可见。</summary>
    internal sealed class EdgeTabWindow : Window
    {
        public const double TabW = 32;
        public const double TabH = 368;
        private const double HotW = 26;
        private const double R = 14;

        private readonly DrawerWindow _owner;
        private Grid _root;
        private Grid _tab;
        private TranslateTransform _shift;
        private Image _backdrop;
        private DispatcherTimer _watch;
        private int _idle;
        private bool _visible;

        public EdgeTabWindow(DrawerWindow owner)
        {
            _owner = owner;

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Title = "SideDrawerTab";
            UseLayoutRounding = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

            Rect wa = SystemParameters.WorkArea;
            Left = wa.Left;
            Top = wa.Top;
            Width = TabW + 6;
            Height = wa.Height;

            Build();

            _watch = new DispatcherTimer();
            _watch.Interval = TimeSpan.FromMilliseconds(60);
            _watch.Tick += delegate { Watch(); };
            _watch.Start();
        }

        private double TabTop { get { return (Height - TabH) / 2.0; } }

        private void Build()
        {
            _root = new Grid();
            _root.Background = Brushes.Transparent;
            Content = _root;

            _tab = new Grid();
            _tab.Width = TabW;
            _tab.Height = TabH;
            _tab.HorizontalAlignment = HorizontalAlignment.Left;
            _tab.VerticalAlignment = VerticalAlignment.Center;
            _shift = new TranslateTransform(-TabW - 8, 0);
            _tab.RenderTransform = _shift;
            _tab.Cursor = Cursors.Hand;
            _tab.ToolTip = "点击展开侧边栏";
            Glass.Shadow(_tab, 26, 0.45, null);
            _root.Children.Add(_tab);

            Geometry shape = Ribbon(TabW, TabH, 16, 0);

            Grid layers = new Grid();
            layers.OpacityMask = Glass.Mask(shape, TabW, TabH);

            _backdrop = new Image();
            _backdrop.Stretch = Stretch.Fill;
            layers.Children.Add(_backdrop);

            Rectangle tint = new Rectangle();
            LinearGradientBrush tb = new LinearGradientBrush();
            tb.StartPoint = new Point(0, 0);
            tb.EndPoint = new Point(1, 1);
            tb.GradientStops.Add(new GradientStop(Color.FromArgb(96, 255, 255, 255), 0));
            tb.GradientStops.Add(new GradientStop(Color.FromArgb(26, 255, 255, 255), 0.55));
            tb.GradientStops.Add(new GradientStop(Color.FromArgb(44, 255, 255, 255), 1));
            tint.Fill = tb;
            layers.Children.Add(tint);

            Rectangle veil = new Rectangle();
            veil.Fill = new SolidColorBrush(Color.FromArgb(108, 8, 8, 16));
            layers.Children.Add(veil);

            Rectangle noise = new Rectangle();
            ImageBrush nb = new ImageBrush(Glass.Noise(64, 64, 5));
            nb.TileMode = TileMode.Tile;
            nb.Viewport = new Rect(0, 0, 64, 64);
            nb.ViewportUnits = BrushMappingMode.Absolute;
            noise.Fill = nb;
            noise.Opacity = 0.035;
            layers.Children.Add(noise);

            Rectangle glint = new Rectangle();
            LinearGradientBrush gb = new LinearGradientBrush();
            gb.StartPoint = new Point(0, 0);
            gb.EndPoint = new Point(0, 1);
            gb.GradientStops.Add(new GradientStop(Color.FromArgb(110, 255, 255, 255), 0));
            gb.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.45));
            glint.Fill = gb;
            layers.Children.Add(glint);

            _tab.Children.Add(layers);

            Path rim = new Path();
            rim.Data = shape;
            LinearGradientBrush rb = new LinearGradientBrush();
            rb.StartPoint = new Point(0, 0);
            rb.EndPoint = new Point(1, 1);
            rb.GradientStops.Add(new GradientStop(Color.FromArgb(215, 255, 255, 255), 0));
            rb.GradientStops.Add(new GradientStop(Color.FromArgb(90, 255, 255, 255), 0.5));
            rb.GradientStops.Add(new GradientStop(Color.FromArgb(150, 255, 255, 255), 1));
            rim.Stroke = rb;
            rim.StrokeThickness = 1;
            _tab.Children.Add(rim);

            TextBlock chev = new TextBlock();
            chev.Text = "\uE76C";
            chev.FontFamily = new FontFamily("Segoe MDL2 Assets");
            chev.FontSize = 12;
            chev.Foreground = new SolidColorBrush(Color.FromArgb(240, 255, 255, 255));
            chev.HorizontalAlignment = HorizontalAlignment.Right;
            chev.VerticalAlignment = VerticalAlignment.Center;
            chev.Margin = new Thickness(0);
            chev.IsHitTestVisible = false;
            System.Windows.Media.Effects.DropShadowEffect ce = new System.Windows.Media.Effects.DropShadowEffect();
            ce.Color = Colors.Black;
            ce.BlurRadius = 6;
            ce.ShadowDepth = 0;
            ce.Opacity = 0.65;
            chev.Effect = ce;
            _tab.Children.Add(chev);

            _tab.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                HideNow();
                if (_owner != null) _owner.OpenFromEdge();
            };
        }

        /// <summary>靠外一侧的铁轨样式：两条细轨 + 等距枕木。</summary>
        private static Geometry TrackGeometry(double w, double h)
        {
            GeometryGroup g = new GeometryGroup();
            double railA = w - 15;
            double railB = w - 5;
            g.Children.Add(new LineGeometry(new Point(railA, 8), new Point(railA, h - 8)));
            g.Children.Add(new LineGeometry(new Point(railB, 8), new Point(railB, h - 8)));
            for (double y = 14; y < h - 10; y += 20)
                g.Children.Add(new LineGeometry(new Point(railA - 3, y), new Point(railB + 2, y)));
            g.Freeze();
            return g;
        }

        private static UIElement BuildTrack(double w, double h)
        {
            System.Windows.Shapes.Path p = new System.Windows.Shapes.Path();
            p.Data = TrackGeometry(w, h);
            p.Stroke = new SolidColorBrush(Color.FromArgb(120, 255, 255, 255));
            p.StrokeThickness = 1;
            p.StrokeStartLineCap = PenLineCap.Round;
            p.StrokeEndLineCap = PenLineCap.Round;
            p.IsHitTestVisible = false;
            return p;
        }

        /// <summary>贴屏一侧是凹弧（反弧度），外侧是圆角。</summary>
        private static Geometry Ribbon(double w, double h, double r, double bow)
        {
            StreamGeometry g = new StreamGeometry();
            using (StreamGeometryContext c = g.Open())
            {
                c.BeginFigure(new Point(0, 0), true, true);
                c.LineTo(new Point(w - r, 0), true, false);
                c.ArcTo(new Point(w, r), new Size(r, r), 0, false, SweepDirection.Clockwise, true, false);
                c.LineTo(new Point(w, h - r), true, false);
                c.ArcTo(new Point(w - r, h), new Size(r, r), 0, false, SweepDirection.Clockwise, true, false);
                c.LineTo(new Point(0, h), true, false);
                c.QuadraticBezierTo(new Point(bow, h / 2), new Point(0, 0), true, false);
            }
            g.Freeze();
            return g;
        }
        /// <summary>用光标位置判断，不依赖 WPF 的鼠标进出事件（分层窗口不可靠）。</summary>
        private void Watch()
        {
            bool ownerOpen = _owner != null && _owner.IsOpen;
            if (ownerOpen)
            {
                if (_visible) HideNow();
                _idle = 0;
                return;
            }

            Native.POINT p;
            if (!Native.GetCursorPos(out p)) return;
            double[] d = Glass.Dpi(this);
            if (d[0] <= 0) d[0] = 1;
            if (d[1] <= 0) d[1] = 1;
            double x = p.X / d[0];
            double y = p.Y / d[1];

            bool inTabBand = y >= TabTop - 40 && y <= TabTop + TabH + 40;
            bool nearEdge = x >= Left - 2 && x <= Left + HotW && inTabBand;
            bool overTab = _visible && x >= Left && x <= Left + TabW + 10
                           && y >= TabTop - 10 && y <= TabTop + TabH + 10;

            if (nearEdge || overTab)
            {
                _idle = 0;
                if (!_visible) ShowTab();
            }
            else
            {
                _idle += 60;
                if (_visible && _idle >= 420) HideTab();
            }
        }

        public bool IsTabVisible { get { return _visible; } }

        public void ShowTab()
        {
            if (_visible) return;
            _visible = true;

            try
            {
                ImageSource shot = Glass.Backdrop(Left, Top + TabTop, TabW, TabH, this);
                if (shot != null) _backdrop.Source = shot;
            }
            catch { }

            try { Native.KeepTopmost(new WindowInteropHelper(this).Handle); } catch { }
            try { Point scr = _tab.PointToScreen(new Point(0, 0)); Program.Log("tab screen=" + scr.X.ToString("0") + "," + scr.Y.ToString("0") + " dpi=" + Glass.Dpi(this)[0].ToString("0.00") + " winTop=" + Top.ToString("0") + " winH=" + Height.ToString("0")); } catch { }

            _shift.BeginAnimation(TranslateTransform.XProperty, null);
            _shift.X = -TabW - 8;
            DoubleAnimation a = new DoubleAnimation(0, TimeSpan.FromMilliseconds(280));
            a.EasingFunction = new BackEase { Amplitude = 0.22, EasingMode = EasingMode.EaseOut };
            _shift.BeginAnimation(TranslateTransform.XProperty, a);
        }

        public void HideTab()
        {
            if (!_visible) return;
            _visible = false;
            DoubleAnimation a = new DoubleAnimation(-TabW - 8, TimeSpan.FromMilliseconds(200));
            a.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn };
            _shift.BeginAnimation(TranslateTransform.XProperty, a);
        }

        public void HideNow()
        {
            _visible = false;
            _idle = 0;
            _shift.BeginAnimation(TranslateTransform.XProperty, null);
            _shift.X = -TabW - 8;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr h = new WindowInteropHelper(this).Handle;
            int ex = Native.GetWindowLong(h, Native.GWL_EXSTYLE);
            Native.SetWindowLong(h, Native.GWL_EXSTYLE, ex | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE);
        }
    }
}

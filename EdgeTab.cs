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
        private int _dwell;
        private int _stillMs;
        private int _lastPx = int.MinValue;
        private int _lastPy = int.MinValue;
        private double _dpiX = 1.0;
        private double _dpiY = 1.0;
        private bool _dpiReady;
        private int _showGen;
        private bool _visible;
        private bool _tabDown;

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
            layers.Clip = shape;

            _backdrop = new Image();
            _backdrop.Stretch = Stretch.Fill;
            RenderOptions.SetBitmapScalingMode(_backdrop, BitmapScalingMode.Linear);
            layers.Children.Add(_backdrop);

            Rectangle tint = new Rectangle();
            LinearGradientBrush tb = new LinearGradientBrush();
            tb.StartPoint = new Point(0, 0);
            tb.EndPoint = new Point(1, 1);
            tb.GradientStops.Add(new GradientStop(Color.FromArgb(96, 255, 255, 255), 0));
            tb.GradientStops.Add(new GradientStop(Color.FromArgb(26, 255, 255, 255), 0.55));
            tb.GradientStops.Add(new GradientStop(Color.FromArgb(44, 255, 255, 255), 1));
            tb.Freeze();
            tint.Fill = tb;
            layers.Children.Add(tint);

            Rectangle veil = new Rectangle();
            SolidColorBrush vb = new SolidColorBrush(Color.FromArgb(108, 8, 8, 16));
            vb.Freeze();
            veil.Fill = vb;
            layers.Children.Add(veil);

            Rectangle noise = new Rectangle();
            ImageBrush nb = new ImageBrush(Glass.Noise(64, 64, 5));
            nb.TileMode = TileMode.Tile;
            nb.Viewport = new Rect(0, 0, 64, 64);
            nb.ViewportUnits = BrushMappingMode.Absolute;
            nb.Freeze();
            noise.Fill = nb;
            noise.Opacity = 0.035;
            layers.Children.Add(noise);

            Rectangle glint = new Rectangle();
            LinearGradientBrush gb = new LinearGradientBrush();
            gb.StartPoint = new Point(0, 0);
            gb.EndPoint = new Point(0, 1);
            gb.GradientStops.Add(new GradientStop(Color.FromArgb(110, 255, 255, 255), 0));
            gb.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.45));
            gb.Freeze();
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
            rb.Freeze();
            rim.Stroke = rb;
            rim.StrokeThickness = 1;
            _tab.Children.Add(rim);

            TextBlock chev = new TextBlock();
            chev.Text = "\uE76C";
            chev.FontFamily = new FontFamily("Segoe MDL2 Assets");
            chev.FontSize = 12;
            SolidColorBrush chevFg = new SolidColorBrush(Color.FromArgb(240, 255, 255, 255));
            chevFg.Freeze();
            chev.Foreground = chevFg;
            chev.HorizontalAlignment = HorizontalAlignment.Right;
            chev.VerticalAlignment = VerticalAlignment.Center;
            chev.Margin = new Thickness(0);
            chev.IsHitTestVisible = false;
            System.Windows.Media.Effects.DropShadowEffect ce = new System.Windows.Media.Effects.DropShadowEffect();
            ce.Color = Colors.Black;
            ce.BlurRadius = 6;
            ce.ShadowDepth = 0;
            ce.Opacity = 0.65;
            ce.Freeze();
            chev.Effect = ce;
            _tab.Children.Add(chev);

            _tab.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                int guard = _owner != null ? _owner.EdgeGuardMode : 1;
                if (guard > 0 && Native.IsCursorCapturedOrHidden()) { _tabDown = false; HideNow(); return; }
                _tabDown = true;
            };
            _tab.MouseLeave += delegate { _tabDown = false; };
            _tab.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                if (!_tabDown) return;
                _tabDown = false;
                int guard = _owner != null ? _owner.EdgeGuardMode : 1;
                if (guard > 0 && Native.IsCursorCapturedOrHidden()) { HideNow(); return; }
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
        private void EnsureDpi()
        {
            if (_dpiReady) return;
            try
            {
                PresentationSource ps = PresentationSource.FromVisual(this);
                if (ps != null && ps.CompositionTarget != null)
                {
                    Matrix m = ps.CompositionTarget.TransformToDevice;
                    if (m.M11 > 0.1 && m.M22 > 0.1)
                    {
                        _dpiX = m.M11;
                        _dpiY = m.M22;
                        _dpiReady = true;
                        if (_tab != null) _tab.CacheMode = Glass.MakeCache(this);
                        Program.Log("tab ready dpi=" + _dpiX.ToString("0.00") + " left=" + Left.ToString("0") + " top=" + Top.ToString("0") + " h=" + Height.ToString("0") + " guard=" + (_owner != null ? _owner.EdgeGuardMode : -1));
                    }
                }
            }
            catch { }
        }

        /// <summary>用光标位置判断，不依赖 WPF 的鼠标进出事件（分层窗口不可靠）。</summary>
        private void Watch()
        {
            EnsureDpi();

            bool ownerOpen = _owner != null && _owner.IsOpen;
            if (ownerOpen)
            {
                if (_visible) HideNow();
                _idle = 0;
                _dwell = 0;
                return;
            }

            Native.POINT p;
            if (!Native.GetCursorPos(out p)) return;

            if (p.X == _lastPx && p.Y == _lastPy)
            {
                if (_stillMs < 5000) _stillMs += 60;
            }
            else
            {
                _lastPx = p.X;
                _lastPy = p.Y;
                _stillMs = 0;
            }

            double x = p.X / _dpiX;
            double y = p.Y / _dpiY;

            bool inTabBand = y >= TabTop - 40 && y <= TabTop + TabH + 40;
            bool nearEdge = x >= Left - 2 && x <= Left + HotW && inTabBand;
            bool overTab = _visible && x >= Left && x <= Left + TabW + 10
                           && y >= TabTop - 10 && y <= TabTop + TabH + 10;

            int guard = _owner != null ? _owner.EdgeGuardMode : 1;
            bool desktopFg = false;
            if (guard > 0)
            {
                // 光标被隐藏（CURSOR_SHOWING==0）或被 ClipCursor 锁定 → 游戏接管鼠标，立即屏蔽
                if (Native.IsCursorCapturedOrHidden())
                {
                    _dwell = 0;
                    _idle = 0;
                    if (_visible) HideNow();
                    return;
                }

                // 按住鼠标键（开枪 / 拖拽）：未弹出时不弹出；已弹出且不在书签上时立即隐藏
                // 注意：若书签已弹出且光标正落在书签上，允许用户正常左键点击展开！
                if (Native.IsAnyMouseButtonDown())
                {
                    if (!_visible)
                    {
                        _dwell = 0;
                        return;
                    }
                    if (!overTab)
                    {
                        _dwell = 0;
                        _idle = 0;
                        HideNow();
                        return;
                    }
                }

                desktopFg = Native.IsDesktopForeground();

                // 方案 A（模式 2）：仅桌面/任务栏在前台时启用边缘热区
                if (guard == 2 && !desktopFg)
                {
                    _dwell = 0;
                    _idle = 0;
                    if (_visible) HideTab();
                    return;
                }
            }

            if (nearEdge || overTab)
            {
                _idle = 0;
                if (!_visible)
                {
                    // Raw Input 游戏：光标可能死停在进入游戏时的左边缘位置（静止超过 1 秒且非桌面）
                    if (guard > 0 && !desktopFg && _stillMs >= 1000)
                    {
                        _dwell = 0;
                        return;
                    }

                    int needDwell = 0;
                    if (guard == 1) needDwell = desktopFg ? 60 : 220;
                    else if (guard == 2) needDwell = 120;

                    _dwell += 60;
                    if (_dwell >= needDwell)
                    {
                        _dwell = 0;
                        ShowTab();
                    }
                }
            }
            else
            {
                _dwell = 0;
                _idle += 60;
                if (_visible && _idle >= 420) HideTab();
            }
        }

        public bool IsTabVisible { get { return _visible; } }

        public void ShowTab()
        {
            if (_visible) return;
            _visible = true;
            int gen = ++_showGen;

            // 先立刻弹出动画（0ms 阻塞），背景毛玻璃在后台线程异步抓取并回填
            Glass.BackdropAsync(Left, Top + TabTop, TabW, TabH, this, delegate(ImageSource shot)
            {
                if (!_visible || gen != _showGen || shot == null) return;
                _backdrop.Source = shot;
            });

            try { Native.KeepTopmost(new WindowInteropHelper(this).Handle); } catch { }

            _shift.BeginAnimation(TranslateTransform.XProperty, null);
            _shift.X = -TabW - 8;
            DoubleAnimation a = new DoubleAnimation(0, TimeSpan.FromMilliseconds(260));
            a.EasingFunction = new BackEase { Amplitude = 0.22, EasingMode = EasingMode.EaseOut };
            _shift.BeginAnimation(TranslateTransform.XProperty, a);
        }

        public void HideTab()
        {
            if (!_visible) return;
            _visible = false;
            _tabDown = false;
            _showGen++;
            DoubleAnimation a = new DoubleAnimation(-TabW - 8, TimeSpan.FromMilliseconds(190));
            a.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn };
            _shift.BeginAnimation(TranslateTransform.XProperty, a);
        }

        public void HideNow()
        {
            _visible = false;
            _tabDown = false;
            _showGen++;
            _idle = 0;
            _dwell = 0;
            _shift.BeginAnimation(TranslateTransform.XProperty, null);
            _shift.X = -TabW - 8;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr h = new WindowInteropHelper(this).Handle;
            int ex = Native.GetWindowLong(h, Native.GWL_EXSTYLE);
            Native.SetWindowLong(h, Native.GWL_EXSTYLE, ex | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE);
            EnsureDpi();
        }
    }
}

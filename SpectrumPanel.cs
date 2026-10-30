using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SideDrawer
{
    /// <summary>底部音频可视化：和音乐面板一样，是主窗口里的一个 Grid，跟随侧边栏显隐。</summary>
    internal sealed class SpectrumPanel : Grid
    {
        public const double PH = 104;

        private readonly List<Border> _bars = new List<Border>();
        private readonly double[] _val;
        private readonly double _maxH = 72;
        private readonly double _idleH = 10;
        private readonly Random _rnd = new Random();
        private DispatcherTimer _timer;
        private bool _playing;

        public SpectrumPanel(double width)
        {
            Width = width;
            Height = PH;
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Top;
            IsHitTestVisible = false;

            int n = 28;
            _val = new double[n];

            Border card = new Border();
            card.Width = width;
            card.Height = PH;
            card.CornerRadius = new CornerRadius(20);
            card.Background = new SolidColorBrush(Color.FromArgb(120, 12, 12, 20));
            card.BorderBrush = new SolidColorBrush(Color.FromArgb(56, 255, 255, 255));
            card.BorderThickness = new Thickness(1);
            card.HorizontalAlignment = HorizontalAlignment.Left;
            card.VerticalAlignment = VerticalAlignment.Top;
            Children.Add(card);

            double gap = 2.5;
            double inner = width - 40;
            double bw = Math.Max(2, (inner - gap * (n - 1)) / n);
            double baseY = PH - 14;

            for (int i = 0; i < n; i++)
            {
                Border bar = new Border();
                bar.Width = bw;
                bar.Height = _maxH;
                bar.CornerRadius = new CornerRadius(Math.Min(3, bw / 2));
                LinearGradientBrush g = new LinearGradientBrush();
                g.StartPoint = new Point(0, 1);
                g.EndPoint = new Point(0, 0);
                g.GradientStops.Add(new GradientStop(Color.FromArgb(150, 150, 215, 255), 0));
                g.GradientStops.Add(new GradientStop(Color.FromArgb(240, 255, 190, 215), 1));
                bar.Background = g;
                bar.HorizontalAlignment = HorizontalAlignment.Left;
                bar.VerticalAlignment = VerticalAlignment.Top;
                bar.RenderTransformOrigin = new Point(0.5, 1);
                ScaleTransform sc = new ScaleTransform(1, _idleH / _maxH);
                bar.RenderTransform = sc;
                bar.Margin = new Thickness(20 + i * (bw + gap), baseY - _maxH, 0, 0);
                Children.Add(bar);
                _bars.Add(bar);
            }

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(130);
            _timer.Tick += delegate { Tick(); };
        }

        private void Tick()
        {
            if (!_playing)
            {
                for (int i = 0; i < _bars.Count; i++)
                {
                    _val[i] = 0;
                    ((ScaleTransform)_bars[i].RenderTransform).ScaleY = _idleH / _maxH;
                }
                _timer.Stop();
                return;
            }

            for (int i = 0; i < _bars.Count; i++)
            {
                double target = 0.1 + _rnd.NextDouble() * 0.9;
                _val[i] = _val[i] * 0.6 + target * 0.4;
                double h = _idleH + _val[i] * (_maxH - _idleH);
                ((ScaleTransform)_bars[i].RenderTransform).ScaleY = h / _maxH;
            }
        }

        public void SetPlaying(bool playing)
        {
            _playing = playing;
            if (playing)
            {
                if (Visibility == Visibility.Visible && !_timer.IsEnabled) _timer.Start();
            }
            else
            {
                if (_timer.IsEnabled) Tick();
                _timer.Stop();
            }
        }

        public void ShowPanel()
        {
            Visibility = Visibility.Visible;

            // 和侧边栏一起淡入，避免“突然出现”
            BeginAnimation(OpacityProperty, null);
            Opacity = 0;
            DoubleAnimation fade = new DoubleAnimation(1, TimeSpan.FromMilliseconds(240));
            fade.BeginTime = TimeSpan.FromMilliseconds(120);
            BeginAnimation(OpacityProperty, fade);

            // 幻灯片动画结束前先不跑条子，避免掉帧
            _timer.Stop();
            DispatcherTimer kick = new DispatcherTimer();
            kick.Interval = TimeSpan.FromMilliseconds(420);
            kick.Tick += delegate
            {
                kick.Stop();
                if (_playing && Visibility == Visibility.Visible && !_timer.IsEnabled) _timer.Start();
            };
            kick.Start();
        }

        public void HidePanel()
        {
            _timer.Stop();
            Visibility = Visibility.Collapsed;
        }
    }
}
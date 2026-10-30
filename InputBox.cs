using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SideDrawer
{
    /// <summary>简单的文字输入对话框（无 XAML）。</summary>
    internal sealed class InputBox : Window
    {
        private readonly TextBox _box;
        public string Value { get; private set; }

        public InputBox(string title, string initial)
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Width = 360;
            Height = 168;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;

            Border card = new Border();
            card.CornerRadius = new CornerRadius(18);
            card.Background = new SolidColorBrush(Color.FromArgb(246, 26, 26, 36));
            card.BorderBrush = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255));
            card.BorderThickness = new Thickness(1);
            card.Padding = new Thickness(22, 18, 22, 16);

            StackPanel sp = new StackPanel();
            TextBlock cap = new TextBlock();
            cap.Text = title;
            cap.FontFamily = new FontFamily("Microsoft YaHei UI");
            cap.FontSize = 13;
            cap.Foreground = new SolidColorBrush(Color.FromArgb(230, 255, 255, 255));
            cap.Margin = new Thickness(0, 0, 0, 12);
            sp.Children.Add(cap);

            _box = new TextBox();
            _box.Text = initial ?? "";
            _box.FontFamily = new FontFamily("Microsoft YaHei UI");
            _box.FontSize = 15;
            _box.Padding = new Thickness(10, 8, 10, 8);
            _box.CaretBrush = Brushes.White;
            _box.Foreground = Brushes.White;
            _box.Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
            _box.BorderBrush = new SolidColorBrush(Color.FromArgb(110, 255, 255, 255));
            _box.BorderThickness = new Thickness(1);
            sp.Children.Add(_box);

            StackPanel btns = new StackPanel();
            btns.Orientation = Orientation.Horizontal;
            btns.HorizontalAlignment = HorizontalAlignment.Right;
            btns.Margin = new Thickness(0, 14, 0, 0);

            Border cancel = Btn("取消", false);
            Border ok = Btn("确定", true);
            cancel.MouseLeftButtonUp += delegate { DialogResult = false; Close(); };
            ok.MouseLeftButtonUp += delegate { Value = _box.Text; DialogResult = true; Close(); };
            btns.Children.Add(cancel);
            btns.Children.Add(ok);
            sp.Children.Add(btns);

            card.Child = sp;
            Content = card;

            KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.Key == Key.Enter) { Value = _box.Text; DialogResult = true; Close(); }
                else if (e.Key == Key.Escape) { DialogResult = false; Close(); }
            };

            Loaded += delegate { _box.Focus(); _box.SelectAll(); };
        }

        private Border Btn(string text, bool primary)
        {
            Border b = new Border();
            b.CornerRadius = new CornerRadius(9);
            b.Padding = new Thickness(18, 7, 18, 8);
            b.Margin = new Thickness(8, 0, 0, 0);
            b.Cursor = Cursors.Hand;
            b.Background = new SolidColorBrush(primary ? Color.FromArgb(235, 90, 110, 170) : Color.FromArgb(60, 255, 255, 255));
            b.BorderBrush = new SolidColorBrush(Color.FromArgb(90, 255, 255, 255));
            b.BorderThickness = new Thickness(1);
            TextBlock t = new TextBlock();
            t.Text = text;
            t.FontFamily = new FontFamily("Microsoft YaHei UI");
            t.FontSize = 13;
            t.Foreground = Brushes.White;
            b.Child = t;
            return b;
        }
    }
}
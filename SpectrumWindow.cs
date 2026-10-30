using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace SideDrawer
{
    /// <summary>独立窗口承载频谱：重绘只影响自己，不拖累主窗口。</summary>
    internal sealed class SpectrumWindow : Window
    {
        private readonly SpectrumPanel _panel;

        public SpectrumWindow(double width)
        {
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            Topmost = true;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Width = width;
            Height = SpectrumPanel.PH;
            Left = -20000;
            Top = -20000;

            _panel = new SpectrumPanel(width);
            Content = _panel;
        }

        public SpectrumPanel Panel { get { return _panel; } }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr h = new WindowInteropHelper(this).Handle;
            int ex = Native.GetWindowLong(h, Native.GWL_EXSTYLE);
            Native.SetWindowLong(h, Native.GWL_EXSTYLE, ex | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE);
        }

        public void ShowAt(double left, double top)
        {
            Left = left;
            Top = top;
            if (!IsVisible) Show();
            Panel.ShowPanel();
            try { Native.KeepTopmost(new WindowInteropHelper(this).Handle); } catch { }
        }

        public void HideAll()
        {
            Panel.HidePanel();
            Hide();
            Left = -20000;
            Top = -20000;
        }
    }
}
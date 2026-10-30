using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace SideDrawer
{
    // ---------------------------------------------------------------- data
    internal class TrackInfo
    {
        public bool Has;
        public string Title = "";
        public string Artist = "";
        public string Album = "";
        public int Status;
        public double Pos;
        public double Dur;
        public byte[] Art;
    }

    internal class LyricLine
    {
        public double Time;
        public string Text = "";
    }

    /// <summary>从 lrclib.net 拉同步歌词（免认证）。</summary>
    internal static class Lyrics
    {
        private static readonly Dictionary<string, LyricLine[]> _cache = new Dictionary<string, LyricLine[]>();
        private static readonly object _lock = new object();

        public static LyricLine[] Get(string title, string artist, double dur)
        {
            if (string.IsNullOrEmpty(title)) return new LyricLine[0];
            string key = title + "\u0001" + artist;
            lock (_lock)
            {
                LyricLine[] hit;
                if (_cache.TryGetValue(key, out hit)) return hit;
            }

            LyricLine[] lines = new LyricLine[0];
            try
            {
                string url = "https://lrclib.net/api/get?track_name=" + Uri.EscapeDataString(title)
                           + "&artist_name=" + Uri.EscapeDataString(artist ?? "");
                if (dur > 1) url += "&duration=" + ((int)Math.Round(dur)).ToString();

                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Timeout = 4500;
                req.ReadWriteTimeout = 4500;
                req.UserAgent = "SideDrawer/1.0";
                req.Accept = "application/json";
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    string json = sr.ReadToEnd();
                    JavaScriptSerializer ser = new JavaScriptSerializer();
                    Dictionary<string, object> d = ser.Deserialize<Dictionary<string, object>>(json);
                    if (d != null && d.ContainsKey("syncedLyrics") && d["syncedLyrics"] != null)
                        lines = Parse(Convert.ToString(d["syncedLyrics"]));
                }
            }
            catch (Exception ex) { lines = new LyricLine[0]; Program.Log("lrclib err: " + ex.Message); }

            if (lines.Length == 0)
            {
                try { lines = Netease(title, artist); Program.Log("netease lines=" + lines.Length + " for " + title); }
                catch (Exception ex) { lines = new LyricLine[0]; Program.Log("netease err: " + ex.Message); }
            }

            lock (_lock) { _cache[key] = lines; }
            return lines;
        }

        private static string HttpGet(string url)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Timeout = 5000;
            req.ReadWriteTimeout = 5000;
            req.UserAgent = "Mozilla/5.0 SideDrawer/1.0";
            req.Accept = "application/json";
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return sr.ReadToEnd();
        }

        /// <summary>备用歌词源：网易云（无需登录）。</summary>
        private static LyricLine[] Netease(string title, string artist)
        {
            JavaScriptSerializer ser = new JavaScriptSerializer();
            string cleanArtist = (artist ?? "").Trim();
            int sep = cleanArtist.IndexOf('\u2014');
            if (sep > 0) cleanArtist = cleanArtist.Substring(0, sep);
            sep = cleanArtist.IndexOf(" - ");
            if (sep > 0) cleanArtist = cleanArtist.Substring(0, sep);
            cleanArtist = cleanArtist.Trim(' ', '-', '\u2014', '\u2013');
            string q = (title + " " + cleanArtist).Trim();
            Program.Log("netease q=" + q);
            string searchUrl = "https://music.163.com/api/search/get?s=" + Uri.EscapeDataString(q) + "&type=1&limit=10";
            string json = HttpGet(searchUrl);
            Program.Log("netease search len=" + json.Length + " head=" + json.Substring(0, Math.Min(120, json.Length)));
            Dictionary<string, object> root = ser.Deserialize<Dictionary<string, object>>(json);
            if (root == null || !root.ContainsKey("result") || root["result"] == null) return new LyricLine[0];
            Dictionary<string, object> result = root["result"] as Dictionary<string, object>;
            if (result == null || !result.ContainsKey("songs")) return new LyricLine[0];
            System.Collections.IEnumerable songs = result["songs"] as System.Collections.IEnumerable;
            if (songs == null) { Program.Log("netease no songs key"); return new LyricLine[0]; }
            int n = 0; foreach (object _x in songs) n++;
            Program.Log("netease songs=" + n);
            if (n == 0) return new LyricLine[0];
            foreach (object so in songs)
            {
                Dictionary<string, object> s = so as Dictionary<string, object>;
                if (s == null || !s.ContainsKey("id")) continue;
                string songName = s.ContainsKey("name") ? Convert.ToString(s["name"]) : "";
                if (songName.Length > 0 && title.Length > 0 &&
                    songName.IndexOf(title, StringComparison.OrdinalIgnoreCase) < 0 &&
                    title.IndexOf(songName, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                string id = Convert.ToString(s["id"]);
                string lyricUrl = "https://music.163.com/api/song/lyric?id=" + id + "&lv=1&kv=1&tv=-1";
                try
                {
                    Dictionary<string, object> lr = ser.Deserialize<Dictionary<string, object>>(HttpGet(lyricUrl));
                    if (lr == null || !lr.ContainsKey("lrc") || lr["lrc"] == null) continue;
                    Dictionary<string, object> lrcDict = lr["lrc"] as Dictionary<string, object>;
                    if (lrcDict == null || !lrcDict.ContainsKey("lyric")) continue;
                    LyricLine[] got = Parse(Convert.ToString(lrcDict["lyric"]));
                    if (got.Length > 0) return got;
                }
                catch { }
            }
            return new LyricLine[0];
        }

        private static LyricLine[] Parse(string lrc)
        {
            List<LyricLine> list = new List<LyricLine>();
            if (string.IsNullOrEmpty(lrc)) return list.ToArray();
            string[] rows = lrc.Replace("\r", "").Split('\n');
            foreach (string raw in rows)
            {
                string row = raw.Trim();
                if (row.Length == 0) continue;
                while (row.StartsWith("["))
                {
                    int close = row.IndexOf(']');
                    if (close < 0) break;
                    string stamp = row.Substring(1, close - 1);
                    row = row.Substring(close + 1);
                    double t;
                    if (TryTime(stamp, out t))
                    {
                        string txt = row.Trim();
                        if (txt.Length > 0)
                        {
                            LyricLine l = new LyricLine();
                            l.Time = t;
                            l.Text = txt;
                            list.Add(l);
                        }
                    }
                }
            }
            list.Sort(delegate(LyricLine a, LyricLine b) { return a.Time.CompareTo(b.Time); });
            return list.ToArray();
        }

        private static bool TryTime(string s, out double sec)
        {
            sec = 0;
            try
            {
                int colon = s.IndexOf(':');
                if (colon < 0) return false;
                int min = int.Parse(s.Substring(0, colon));
                string rest = s.Substring(colon + 1);
                double ss = double.Parse(rest, System.Globalization.CultureInfo.InvariantCulture);
                sec = min * 60 + ss;
                return true;
            }
            catch { return false; }
        }
    }

    /// <summary>通过 Windows SMTC（系统媒体控制）读取/控制任意支持它的播放器。</summary>
    internal static class Smtc
    {
        private static GlobalSystemMediaTransportControlsSessionManager _mgr;
        private static readonly object _lock = new object();

        private static T Wait<T>(Windows.Foundation.IAsyncOperation<T> op)
        {
            int guard = 0;
            while (op.Status == Windows.Foundation.AsyncStatus.Started && guard++ < 400) Thread.Sleep(10);
            if (op.Status == Windows.Foundation.AsyncStatus.Error) throw op.ErrorCode;
            return op.GetResults();
        }

        private static GlobalSystemMediaTransportControlsSessionManager Manager()
        {
            lock (_lock)
            {
                if (_mgr == null) _mgr = Wait(GlobalSystemMediaTransportControlsSessionManager.RequestAsync());
                return _mgr;
            }
        }

        private static GlobalSystemMediaTransportControlsSession Session()
        {
            try
            {
                GlobalSystemMediaTransportControlsSessionManager m = Manager();
                return m == null ? null : m.GetCurrentSession();
            }
            catch { return null; }
        }

        public static TrackInfo Read()
        {
            TrackInfo ti = new TrackInfo();
            try
            {
                GlobalSystemMediaTransportControlsSession s = Session();
                if (s == null) return ti;

                ti.Has = true;
                try
                {
                    GlobalSystemMediaTransportControlsSessionMediaProperties props = Wait(s.TryGetMediaPropertiesAsync());
                    if (props != null)
                    {
                        ti.Title = props.Title ?? "";
                        ti.Artist = props.Artist ?? "";
                        ti.Album = props.AlbumTitle ?? "";
                        IRandomAccessStreamReference th = props.Thumbnail;
                        if (th != null) ti.Art = ReadStream(th);
                    }
                }
                catch { }

                try
                {
                    GlobalSystemMediaTransportControlsSessionPlaybackInfo info = s.GetPlaybackInfo();
                    ti.Status = (int)info.PlaybackStatus;
                }
                catch { }

                try
                {
                    GlobalSystemMediaTransportControlsSessionTimelineProperties tl = s.GetTimelineProperties();
                    ti.Dur = (tl.EndTime - tl.StartTime).TotalSeconds;
                    ti.Pos = (tl.Position - tl.StartTime).TotalSeconds;
                }
                catch { }
            }
            catch { }
            return ti;
        }

        private static byte[] ReadStream(IRandomAccessStreamReference reference)
        {
            IRandomAccessStreamWithContentType stream = null;
            DataReader reader = null;
            try
            {
                stream = Wait(reference.OpenReadAsync());
                if (stream == null || stream.Size == 0) return null;
                uint size = (uint)stream.Size;
                reader = new DataReader(stream);
                Wait(reader.LoadAsync(size));
                byte[] buf = new byte[size];
                reader.ReadBytes(buf);
                return buf;
            }
            catch { return null; }
            finally
            {
                if (reader != null) { try { reader.Dispose(); } catch { } }
                if (stream != null) { try { stream.Dispose(); } catch { } }
            }
        }

        public static void Toggle()
        {
            try { GlobalSystemMediaTransportControlsSession s = Session(); if (s != null) s.TryTogglePlayPauseAsync(); }
            catch { }
        }

        public static void Next()
        {
            try { GlobalSystemMediaTransportControlsSession s = Session(); if (s != null) s.TrySkipNextAsync(); }
            catch { }
        }

        public static void Prev()
        {
            try { GlobalSystemMediaTransportControlsSession s = Session(); if (s != null) s.TrySkipPreviousAsync(); }
            catch { }
        }
    }

    internal sealed class MusicPanel : Grid
    {
    // ---------------------------------------------------------------- panel
        public const double PW = 372;
        public const double PH = 488;
        public static double S = 1.0;

        /// <summary>按分辨率缩放音乐面板（整体 LayoutTransform）。</summary>
        public static void SetScale(double s)
        {
            if (s <= 0) return;
            S = s;
        }
        private const double R = 26;

        private Rectangle _veil;
        private Grid _card;
        private TranslateTransform _shift;
        private Image _backdrop;
        private Image _art;
        private TextBlock _artGlyph;
        private TextBlock _title;
        private TextBlock _artist;
        private TextBlock _lyric;
        private TextBlock _timeNow;
        private TextBlock _timeAll;
        private Grid _progress;
        private TextBlock _playGlyph;

        private DispatcherTimer _poll;
        private DispatcherTimer _lyricTick;
        private bool _busy;
        private bool _shown;
        private byte[] _lastArt;
        private string _lyricKey = "";
        private LyricLine[] _lyricLines;
        private double _basePos;
        private DateTime _baseStamp = DateTime.Now;
        private int _lyricIndex = -2;

        public MusicPanel()
        {
            Width = PW;
            LayoutTransform = new ScaleTransform(S, S);   // 分辨率缩放
            Height = PH;
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Top;
            Margin = new Thickness(0, 26, 0, 0);
            Visibility = Visibility.Collapsed;

            _shift = new TranslateTransform(PW + 40, 0);
            RenderTransform = _shift;

            Build();

            _poll = new DispatcherTimer();
            _poll.Interval = TimeSpan.FromMilliseconds(1200);
            _poll.Tick += delegate { Pump(); };

            _lyricTick = new DispatcherTimer();
            _lyricTick.Interval = TimeSpan.FromMilliseconds(250);
            _lyricTick.Tick += delegate { SyncLyric(); };
        }

        public bool IsShown { get { return _shown; } }

        public void SetPos(double left, double top)
        {
            Margin = new Thickness(left, top, 0, 0);
        }

        private bool _lastPlaying;
        private bool _idleShown;
        private static BitmapImage _idleArt;

        /// <summary>待机封面（未播放时显示）。</summary>
        private static BitmapImage IdleArt()
        {
            if (_idleArt != null) return _idleArt;
            try
            {
                string p = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "idle.jpg");
                if (System.IO.File.Exists(p))
                {
                    BitmapImage bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.UriSource = new Uri(p);
                    bi.EndInit();
                    bi.Freeze();
                    _idleArt = bi;
                }
            }
            catch { }
            return _idleArt;
        }
        public bool IsPlayingNow() { return _lastPlaying; }
        public void SetPlayingFlag(bool v) { _lastPlaying = v; }

        public void SetVeil(int alpha)
        {
            if (_veil == null) return;
            if (alpha < 0) alpha = 0;
            if (alpha > 200) alpha = 200;
            _veil.Fill = new SolidColorBrush(Color.FromArgb((byte)alpha, 10, 10, 16));
        }

        // ------------------------------------------------------------ ui
        private void Build()
        {
            _card = new Grid();
            _card.Width = PW;
            _card.Height = PH;
            Children.Add(_card);
            Glass.Shadow(_card, 40, 0.42, null);

            Grid layers = new Grid();
            if (Environment.GetEnvironmentVariable("SIDEDRAWER_NOMASK") != "1") layers.OpacityMask = Glass.Mask(new RectangleGeometry(new Rect(0, 0, PW, PH), R, R), PW, PH);
            _card.Children.Add(layers);

            _backdrop = new Image();
            _backdrop.Stretch = Stretch.Fill;
            layers.Children.Add(_backdrop);

            Rectangle tint = new Rectangle();
            LinearGradientBrush tb = new LinearGradientBrush();
            tb.StartPoint = new Point(0, 0);
            tb.EndPoint = new Point(0.3, 1);
            tb.GradientStops.Add(new GradientStop(Color.FromArgb(42, 255, 255, 255), 0));
            tb.GradientStops.Add(new GradientStop(Color.FromArgb(13, 255, 255, 255), 0.5));
            tb.GradientStops.Add(new GradientStop(Color.FromArgb(24, 255, 255, 255), 1));
            tint.Fill = tb;
            layers.Children.Add(tint);

            _veil = new Rectangle();
            _veil.Fill = new SolidColorBrush(Color.FromArgb(44, 10, 10, 16));
            layers.Children.Add(_veil);

            Rectangle noise = new Rectangle();
            ImageBrush nb = new ImageBrush(Glass.Noise(96, 96, 11));
            nb.TileMode = TileMode.Tile;
            nb.Viewport = new Rect(0, 0, 96, 96);
            nb.ViewportUnits = BrushMappingMode.Absolute;
            noise.Fill = nb;
            noise.Opacity = 0.03;
            layers.Children.Add(noise);

            System.Windows.Shapes.Path rim = new System.Windows.Shapes.Path();
            rim.Data = new RectangleGeometry(new Rect(0.5, 0.5, PW - 1, PH - 1), R, R);
            LinearGradientBrush rb = new LinearGradientBrush();
            rb.StartPoint = new Point(0, 0);
            rb.EndPoint = new Point(1, 1);
            rb.GradientStops.Add(new GradientStop(Color.FromArgb(160, 255, 255, 255), 0));
            rb.GradientStops.Add(new GradientStop(Color.FromArgb(54, 255, 255, 255), 0.5));
            rb.GradientStops.Add(new GradientStop(Color.FromArgb(100, 255, 255, 255), 1));
            rim.Stroke = rb;
            rim.StrokeThickness = 1;
            layers.Children.Add(rim);

            StackPanel col = new StackPanel();
            col.Margin = new Thickness(28, 24, 28, 20);
            layers.Children.Add(col);

            Border artHost = new Border();
            artHost.Width = 176;
            artHost.Height = 176;
            artHost.CornerRadius = new CornerRadius(20);
            artHost.Background = new SolidColorBrush(Color.FromArgb(22, 255, 255, 255));
            artHost.BorderBrush = new SolidColorBrush(Color.FromArgb(36, 255, 255, 255));
            artHost.BorderThickness = new Thickness(1);
            artHost.HorizontalAlignment = HorizontalAlignment.Center;
            artHost.ClipToBounds = true;

            Grid artGrid = new Grid();
            _artGlyph = new TextBlock();
            _artGlyph.Text = "\uE8D6";
            _artGlyph.FontFamily = new FontFamily("Segoe MDL2 Assets");
            _artGlyph.FontSize = 42;
            _artGlyph.Foreground = new SolidColorBrush(Color.FromArgb(76, 255, 255, 255));
            _artGlyph.HorizontalAlignment = HorizontalAlignment.Center;
            _artGlyph.VerticalAlignment = VerticalAlignment.Center;
            artGrid.Children.Add(_artGlyph);

            _art = new Image();
            _art.Stretch = Stretch.UniformToFill;
            _art.Visibility = Visibility.Collapsed;
            artGrid.Children.Add(_art);

            artHost.Child = artGrid;
            col.Children.Add(artHost);

            _title = new TextBlock();
            _title.FontFamily = new FontFamily("Microsoft YaHei UI");
            _title.FontSize = 15.5;
            _title.FontWeight = FontWeights.SemiBold;
            _title.Foreground = Brushes.White;
            _title.TextAlignment = TextAlignment.Center;
            _title.TextTrimming = TextTrimming.CharacterEllipsis;
            _title.Margin = new Thickness(0, 16, 0, 0);
            _title.Text = "未在播放";
            Glass.Shadow(_title, 6, 0.5, null);
            col.Children.Add(_title);

            _artist = new TextBlock();
            _artist.FontFamily = new FontFamily("Microsoft YaHei UI");
            _artist.FontSize = 11.5;
            _artist.Foreground = new SolidColorBrush(Color.FromArgb(160, 255, 255, 255));
            _artist.TextAlignment = TextAlignment.Center;
            _artist.TextTrimming = TextTrimming.CharacterEllipsis;
            _artist.Margin = new Thickness(0, 4, 0, 0);
            _artist.Text = "打开播放器开始播放";
            col.Children.Add(_artist);

            _lyric = new TextBlock();
            _lyric.FontFamily = new FontFamily("Microsoft YaHei UI");
            _lyric.FontSize = 12.5;
            _lyric.Foreground = new SolidColorBrush(Color.FromArgb(235, 255, 255, 255));
            _lyric.TextAlignment = TextAlignment.Center;
            _lyric.TextTrimming = TextTrimming.CharacterEllipsis;
            _lyric.Margin = new Thickness(0, 12, 0, 0);
            _lyric.Height = 20;
            _lyric.Text = "";
            Glass.Shadow(_lyric, 5, 0.6, null);
            col.Children.Add(_lyric);

            _progress = new Grid();
            _progress.Height = 4;
            _progress.Margin = new Thickness(0, 10, 0, 0);
            Border track = new Border();
            track.CornerRadius = new CornerRadius(2);
            track.Background = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255));
            _progress.Children.Add(track);

            Grid fillGrid = new Grid();
            fillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0, GridUnitType.Star) });
            fillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Border fill = new Border();
            fill.CornerRadius = new CornerRadius(2);
            fill.Background = new SolidColorBrush(Color.FromArgb(225, 255, 255, 255));
            Grid.SetColumn(fill, 0);
            fillGrid.Children.Add(fill);
            _progress.Children.Add(fillGrid);
            _progress.Tag = fillGrid;
            col.Children.Add(_progress);

            Grid times = new Grid();
            times.Margin = new Thickness(0, 7, 0, 0);
            _timeNow = new TextBlock();
            _timeNow.FontFamily = new FontFamily("Microsoft YaHei UI");
            _timeNow.FontSize = 10;
            _timeNow.Foreground = new SolidColorBrush(Color.FromArgb(140, 255, 255, 255));
            _timeNow.Text = "0:00";
            times.Children.Add(_timeNow);
            _timeAll = new TextBlock();
            _timeAll.FontFamily = new FontFamily("Microsoft YaHei UI");
            _timeAll.FontSize = 10;
            _timeAll.Foreground = new SolidColorBrush(Color.FromArgb(140, 255, 255, 255));
            _timeAll.HorizontalAlignment = HorizontalAlignment.Right;
            _timeAll.Text = "0:00";
            times.Children.Add(_timeAll);
            col.Children.Add(times);

            StackPanel ctl = new StackPanel();
            ctl.Orientation = Orientation.Horizontal;
            ctl.HorizontalAlignment = HorizontalAlignment.Center;
            ctl.Margin = new Thickness(0, 16, 0, 0);

            ctl.Children.Add(RoundButton("\uE100", 42, 26, delegate { Smtc.Prev(); DelayedPump(260); }));
            Border play = RoundButton("\uE768", 52, 72, delegate { Smtc.Toggle(); DelayedPump(320); });
            play.Margin = new Thickness(16, 0, 16, 0);
            _playGlyph = play.Child as TextBlock;
            ctl.Children.Add(play);
            ctl.Children.Add(RoundButton("\uE101", 42, 26, delegate { Smtc.Next(); DelayedPump(260); }));
            col.Children.Add(ctl);

            MouseEnter += delegate { _poll.Stop(); _poll.Start(); };
        }

        private Border RoundButton(string glyph, double size, int bgAlpha, Action onClick)
        {
            Border b = new Border();
            b.Width = size;
            b.Height = size;
            b.CornerRadius = new CornerRadius(size / 2);
            b.Background = new SolidColorBrush(Color.FromArgb((byte)bgAlpha, 255, 255, 255));
            b.Cursor = Cursors.Hand;
            TextBlock t = new TextBlock();
            t.Text = glyph;
            t.FontFamily = new FontFamily("Segoe MDL2 Assets");
            t.FontSize = size * 0.34;
            t.Foreground = new SolidColorBrush(Color.FromArgb(242, 255, 255, 255));
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.VerticalAlignment = VerticalAlignment.Center;
            b.Child = t;
            int hover = Math.Min(255, bgAlpha + 36);
            b.MouseEnter += delegate { b.Background = new SolidColorBrush(Color.FromArgb((byte)hover, 255, 255, 255)); };
            b.MouseLeave += delegate { b.Background = new SolidColorBrush(Color.FromArgb((byte)bgAlpha, 255, 255, 255)); };
            b.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e) { e.Handled = true; onClick(); };
            return b;
        }

        // ------------------------------------------------------------ show / hide
        public void Show(double screenLeft, double screenTop, Visual dpiFrom)
        {
            Visibility = Visibility.Visible;
            _shown = true;
            // 液态玻璃背景：延后到滑入动画结束后再截屏+模糊，避免和滑动抢 CPU 造成卡顿
            DispatcherTimer glassDelay = new DispatcherTimer();
            glassDelay.Interval = TimeSpan.FromMilliseconds(300);
            glassDelay.Tick += delegate
            {
                glassDelay.Stop();
                if (!_shown) return;
                try
                {
                    ImageSource shot = Glass.Backdrop(screenLeft, screenTop, PW, PH, dpiFrom);
                    if (shot != null) _backdrop.Source = shot;
                }
                catch { }
            };
            glassDelay.Start();



            _shift.BeginAnimation(TranslateTransform.XProperty, null);
            _shift.X = PW + 40;
            DoubleAnimation a = new DoubleAnimation(0, TimeSpan.FromMilliseconds(380));
            a.BeginTime = TimeSpan.FromMilliseconds(90);
            a.EasingFunction = new BackEase { Amplitude = 0.14, EasingMode = EasingMode.EaseOut };
            _shift.BeginAnimation(TranslateTransform.XProperty, a);

            _poll.Start();
            _lyricTick.Start();
            Pump();

            DispatcherTimer dbg = new DispatcherTimer();
            dbg.Interval = TimeSpan.FromMilliseconds(1500);
            dbg.Tick += delegate
            {
                dbg.Stop();
                try
                {
                    Point p = TranslatePoint(new Point(0, 0), Application.Current.MainWindow);
                    Program.Log(string.Format("musicpos shift={0:0} vis={1} aw={2:0} pos={3:0},{4:0}",
                        _shift.X, Visibility, ActualWidth, p.X, p.Y));
                }
                catch (Exception ex) { Program.Log("musicpos err " + ex.Message); }
            };
            dbg.Start();
        }

        public void Hide()
        {
            if (!_shown) return;
            _shown = false;
            _poll.Stop();
            _lyricTick.Stop();
            DoubleAnimation a = new DoubleAnimation(PW + 40, TimeSpan.FromMilliseconds(260));
            a.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn };
            a.Completed += delegate { if (!_shown) Visibility = Visibility.Collapsed; };
            _shift.BeginAnimation(TranslateTransform.XProperty, a);
        }

        public void HideNow()
        {
            _shown = false;
            _poll.Stop();
            _lyricTick.Stop();
            _shift.BeginAnimation(TranslateTransform.XProperty, null);
            if (_card != null) _card.Effect = null;
            _shift.X = PW + 40;
            Visibility = Visibility.Collapsed;
        }

        // ------------------------------------------------------------ polling
        private void Pump()
        {
            if (_busy || !_shown) return;
            _busy = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                TrackInfo info = Smtc.Read();
                try
                {
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        _busy = false;
                        Apply(info);
                    }));
                }
                catch { _busy = false; }
            });
        }

        private void DelayedPump(int ms)
        {
            DispatcherTimer t = new DispatcherTimer();
            t.Interval = TimeSpan.FromMilliseconds(ms);
            t.Tick += delegate { t.Stop(); Pump(); };
            t.Start();
        }

        private void Apply(TrackInfo ti)
        {
            if (ti == null || !ti.Has)
            {
                _title.Text = "未在播放";
                _artist.Text = "打开播放器开始播放";
                _lyric.Text = "";
                _art.Visibility = Visibility.Collapsed;
                _artGlyph.Visibility = Visibility.Visible;
                _timeNow.Text = "0:00";
                _timeAll.Text = "0:00";
                SetProgress(0);
                if (_playGlyph != null) _playGlyph.Text = "\uE768";
                _lyricLines = null;
                _lyricKey = "";
                return;
            }

            _title.Text = string.IsNullOrEmpty(ti.Title) ? "未知曲目" : ti.Title;
            _artist.Text = string.IsNullOrEmpty(ti.Artist)
                ? (string.IsNullOrEmpty(ti.Album) ? "—" : ti.Album)
                : ti.Artist;
            _timeNow.Text = Clock(ti.Pos);
            _timeAll.Text = Clock(ti.Dur);
            SetProgress(ti.Dur > 0 ? ti.Pos / ti.Dur : 0);

            bool playing = ti.Status == 4;
            SetPlayingFlag(playing);
            if (_playGlyph != null) _playGlyph.Text = playing ? "\uE769" : "\uE768";

            _basePos = ti.Pos;
            _baseStamp = DateTime.Now;

            string key = ti.Title + "|" + ti.Artist;
            if (key != _lyricKey)
            {
                _lyricKey = key;
                _lyricLines = null;
                _lyricIndex = -2;
                _lyric.Text = "正在查找歌词…";
                string t2 = ti.Title, a2 = ti.Artist;
                double d2 = ti.Dur;
                ThreadPool.QueueUserWorkItem(delegate
                {
                    LyricLine[] lines = Lyrics.Get(t2, a2, d2);
                    try
                    {
                        Dispatcher.BeginInvoke(new Action(delegate
                        {
                            if (_lyricKey != t2 + "|" + a2) return;
                            _lyricLines = lines;
                            _lyricIndex = -2;
                            SyncLyric();
                        }));
                    }
                    catch { }
                });
            }

            if (ti.Art != null && ti.Art.Length > 0)
            {
                if (_lastArt == null || _lastArt.Length != ti.Art.Length)
                {
                    try
                    {
                        BitmapImage bi = new BitmapImage();
                        bi.BeginInit();
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.StreamSource = new MemoryStream(ti.Art);
                        bi.EndInit();
                        bi.Freeze();
                        _art.Source = bi;
                        _art.Visibility = Visibility.Visible;
                        _artGlyph.Visibility = Visibility.Collapsed;
                        _lastArt = ti.Art;
                        _idleShown = false;
                    }
                    catch { }
                }
            }
            else
            {
                // 未播放：有 idle.jpg 就用它，没有就显示音符占位
                if (_lastArt != null || _art.Source == null || !_idleShown)
                {
                    _lastArt = null;
                    BitmapImage idle = IdleArt();
                    if (idle != null)
                    {
                        _art.Source = idle;
                        _art.Visibility = Visibility.Visible;
                        _artGlyph.Visibility = Visibility.Collapsed;
                    }
                    else
                    {
                        _art.Source = null;
                        _art.Visibility = Visibility.Collapsed;
                        _artGlyph.Visibility = Visibility.Visible;
                    }
                    _idleShown = true;
                }
            }
        }

        /// <summary>按当前播放位置滚动歌词，只显示一行。</summary>
        private void SyncLyric()
        {
            if (!_shown || _lyricLines == null) return;
            if (_lyricLines.Length == 0)
            {
                if (_lyric.Text != "暂无歌词") _lyric.Text = "暂无歌词";
                return;
            }

            double pos = _basePos + (DateTime.Now - _baseStamp).TotalSeconds;
            int idx = -1;
            for (int i = 0; i < _lyricLines.Length; i++)
            {
                if (_lyricLines[i].Time <= pos + 0.25) idx = i;
                else break;
            }
            if (idx == _lyricIndex) return;
            _lyricIndex = idx;
            _lyric.Text = idx >= 0 ? _lyricLines[idx].Text : "";
        }

        private void SetProgress(double ratio)
        {
            if (ratio < 0) ratio = 0;
            if (ratio > 1) ratio = 1;
            Grid g = _progress.Tag as Grid;
            if (g == null) return;
            g.ColumnDefinitions[0].Width = new GridLength(ratio, GridUnitType.Star);
            g.ColumnDefinitions[1].Width = new GridLength(1 - ratio, GridUnitType.Star);
        }

        private static string Clock(double sec)
        {
            if (sec < 0 || double.IsNaN(sec)) sec = 0;
            int s = (int)Math.Round(sec);
            return string.Format("{0}:{1:00}", s / 60, s % 60);
        }
    }
}

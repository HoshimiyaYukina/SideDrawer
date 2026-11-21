using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using IOPath = System.IO.Path;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
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
        static Lyrics()
        {
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; } catch { }
        }
        public static LyricLine[] Get(string title, string artist, double dur)
        {
            if (string.IsNullOrEmpty(title)) return new LyricLine[0];
            string key = title + "\u0001" + artist;
            lock (_lock)
            {
                LyricLine[] hit;
                if (_cache.TryGetValue(key, out hit)) return hit;
            }

            string cleanTitle = Clean(title);
            string cleanArtist = Clean(artist);
            Program.Log("lyric lookup title=[" + title + "] artist=[" + artist + "] dur=" + dur.ToString("0"));

            LyricLine[] lines = new LyricLine[0];

            // 0) 本地歌词优先（exe 目录 lyrics\曲名.lrc），冷门曲目可手动补
            LyricLine[] local = LocalLrc(cleanTitle, artist);
            if (local != null && local.Length > 0)
            {
                lock (_lock) { _cache[key] = local; }
                Program.Log("lyric result (local) lines=" + local.Length + " for [" + cleanTitle + "]");
                return local;
            }

            // 顺序按「在国内网络可用性」排：网易云 → QQ → lrclib（后者常被墙，放最后免得拖慢）
            try { lines = Netease(cleanTitle, cleanArtist); Program.Log("netease lines=" + lines.Length); }
            catch (Exception ex) { Program.Log("netease err: " + ex.Message); }

            if (lines.Length == 0 && cleanTitle.Length > 0)
            {
                try { lines = Netease(cleanTitle, ""); Program.Log("netease(title only) lines=" + lines.Length); }
                catch (Exception ex) { Program.Log("netease2 err: " + ex.Message); }
            }

            if (lines.Length == 0)
            {
                try { lines = QQ(cleanTitle, cleanArtist); Program.Log("qq lines=" + lines.Length); }
                catch (Exception ex) { Program.Log("qq err: " + ex.Message); }
            }

            if (lines.Length == 0 && _lrclibCooldownUntil < DateTime.Now)
            {
                try { lines = LrclibGet(cleanTitle, cleanArtist, dur); }
                catch (Exception ex) { Program.Log("lrclib get err: " + ex.Message); MarkLrclibDown(); }
                if (lines.Length == 0)
                {
                    try { lines = LrclibSearch(cleanTitle, cleanArtist, dur); }
                    catch (Exception ex) { Program.Log("lrclib search err: " + ex.Message); MarkLrclibDown(); }
                }
            }

            lock (_lock) { _cache[key] = lines; }
            Program.Log("lyric result lines=" + lines.Length + " for [" + cleanTitle + "]");
            return lines;
        }

        private static DateTime _lrclibCooldownUntil = DateTime.MinValue;

        /// <summary>lrclib 连不上时冷却 10 分钟，避免每次切歌都白等两次超时。</summary>
        private static void MarkLrclibDown()
        {
            _lrclibCooldownUntil = DateTime.Now.AddMinutes(10);
        }
        /// <summary>本地歌词：exe 目录下 lyrics\曲名.lrc（或「曲名 - 歌手.lrc」），找不到返回 null。</summary>
        private static LyricLine[] LocalLrc(string title, string artist)
        {
            try
            {
                string dir = IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "lyrics");
                if (!Directory.Exists(dir)) return null;
                string[] names = new string[]
                {
                    title + ".lrc",
                    (title + " - " + (artist ?? "")).Trim() + ".lrc",
                    (artist ?? "") + " - " + title + ".lrc"
                };
                foreach (string nm in names)
                {
                    if (string.IsNullOrWhiteSpace(nm) || nm.StartsWith(".lrc")) continue;
                    string f = IOPath.Combine(dir, nm);
                    if (!File.Exists(f))
                    {
                        // 大小写/空格不敏感兜底
                        foreach (string cand in Directory.GetFiles(dir, "*.lrc"))
                        {
                            if (string.Equals(IOPath.GetFileNameWithoutExtension(cand), IOPath.GetFileNameWithoutExtension(nm),
                                StringComparison.OrdinalIgnoreCase)) { f = cand; break; }
                        }
                    }
                    if (!File.Exists(f)) continue;
                    string txt = File.ReadAllText(f, Encoding.UTF8);
                    LyricLine[] got = Parse(txt);
                    if (got.Length > 0) { Program.Log("local lrc hit: " + IOPath.GetFileName(f)); return got; }
                }
            }
            catch { }
            return null;
        }

        /// <summary>清掉曲名/歌手里的括号后缀、feat.、Remastered 之类的噪音。</summary>
        /// <summary>归一化字符串，用于宽松比对曲名。</summary>
        private static string Norm(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            string t = s.ToLowerInvariant();
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (char c in t)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            }
            return sb.ToString();
        }


        private static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            string t = s;
            // 去掉 (feat. xxx)、(Live)、(Remastered 2011)、[xxx]、（xxx）
            t = System.Text.RegularExpressions.Regex.Replace(t, @"\s*[\(\（\[【][^\)\）\]】]*[\)\）\]】]", " ");
            string[] noise = new string[] { " - Remastered", " - Remaster", " - Live", " - Edit", " - Radio Edit", " - Single Version", " (From", " - From" };
            foreach (string n in noise) { int i = t.IndexOf(n, StringComparison.OrdinalIgnoreCase); if (i > 0) t = t.Substring(0, i); }
            int dash = t.IndexOf(" - ", StringComparison.Ordinal);
            if (dash > 0) t = t.Substring(0, dash);
            t = t.Replace("\u2014", "-").Replace("\u2013", "-");
            t = System.Text.RegularExpressions.Regex.Replace(t, @"\s+", " ").Trim();
            return t;
        }

        private static LyricLine[] LrclibGet(string title, string artist, double dur)
        {
            try
            {
                string url = "https://lrclib.net/api/get?track_name=" + Uri.EscapeDataString(title)
                           + "&artist_name=" + Uri.EscapeDataString(artist ?? "");
                if (dur > 1) url += "&duration=" + ((int)Math.Round(dur)).ToString();
                string json = HttpGet(url);
                JavaScriptSerializer ser = new JavaScriptSerializer();
                Dictionary<string, object> d = ser.Deserialize<Dictionary<string, object>>(json);
                if (d != null && d.ContainsKey("syncedLyrics") && d["syncedLyrics"] != null)
                    return Parse(Convert.ToString(d["syncedLyrics"]));
            }
            catch (Exception ex) { Program.Log("lrclib get err: " + ex.Message); }
            return new LyricLine[0];
        }

        private static LyricLine[] LrclibSearch(string title, string artist, double dur)
        {
            try
            {
                string q = (title + " " + (artist ?? "")).Trim();
                if (q.Length == 0) return new LyricLine[0];
                string url = "https://lrclib.net/api/search?q=" + Uri.EscapeDataString(q);
                string json = HttpGet(url);
                JavaScriptSerializer ser = new JavaScriptSerializer();
                List<Dictionary<string, object>> arr = ser.Deserialize<List<Dictionary<string, object>>>(json);
                if (arr == null || arr.Count == 0) return new LyricLine[0];

                Dictionary<string, object> best = null;
                double bestScore = double.MaxValue;
                foreach (Dictionary<string, object> it in arr)
                {
                    if (it == null) continue;
                    if (!it.ContainsKey("syncedLyrics") || it["syncedLyrics"] == null) continue;
                    string st = Convert.ToString(it["syncedLyrics"]);
                    if (string.IsNullOrEmpty(st)) continue;
                    double idur = 0;
                    if (it.ContainsKey("duration") && it["duration"] != null) double.TryParse(Convert.ToString(it["duration"]), out idur);
                    double score = (dur > 1 && idur > 1) ? Math.Abs(idur - dur) : 5;
                    if (score < bestScore) { bestScore = score; best = it; }
                }
                if (best == null) return new LyricLine[0];
                Program.Log("lrclib search hit diff=" + bestScore.ToString("0.0"));
                return Parse(Convert.ToString(best["syncedLyrics"]));
            }
            catch (Exception ex) { Program.Log("lrclib search err: " + ex.Message); }
            return new LyricLine[0];
        }

        private static string HttpGet(string url)
        {
            return HttpGet(url, null);
        }

        private static string HttpGet(string url, string referer)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Timeout = 5000;
            req.ReadWriteTimeout = 5000;
            req.UserAgent = "Mozilla/5.0 SideDrawer/1.1";
            req.Accept = "application/json";
            if (!string.IsNullOrEmpty(referer)) req.Referer = referer;
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return sr.ReadToEnd();
        }

        /// <summary>第三个来源：QQ 音乐（对日系/动画歌曲覆盖更好）。</summary>
        private static LyricLine[] QQ(string title, string artist)
        {
            try
            {
                JavaScriptSerializer ser = new JavaScriptSerializer();
                string q = (title + " " + (artist ?? "")).Trim();
                if (q.Length == 0) return new LyricLine[0];
                string payload = "{\"req_1\":{\"method\":\"DoSearchForQQMusicDesktop\",\"module\":\"music.search.SearchCgiService\",\"param\":{\"num_per_page\":6,\"page_num\":1,\"query\":"
                    + ser.Serialize(q) + "}}}";
                string url = "https://u.y.qq.com/cgi-bin/musicu.fcg?data=" + Uri.EscapeDataString(payload);
                string json = HttpGet(url, "https://y.qq.com/");
                Dictionary<string, object> root = ser.Deserialize<Dictionary<string, object>>(json);
                if (root == null) return new LyricLine[0];
                Dictionary<string, object> req1 = root.ContainsKey("req_1") ? root["req_1"] as Dictionary<string, object> : null;
                if (req1 == null) return new LyricLine[0];
                Dictionary<string, object> data = req1.ContainsKey("data") ? req1["data"] as Dictionary<string, object> : null;
                if (data == null) return new LyricLine[0];
                Dictionary<string, object> body = data.ContainsKey("body") ? data["body"] as Dictionary<string, object> : null;
                if (body == null) return new LyricLine[0];
                Dictionary<string, object> song = body.ContainsKey("song") ? body["song"] as Dictionary<string, object> : null;
                if (song == null || !song.ContainsKey("list")) return new LyricLine[0];
                System.Collections.IEnumerable list = song["list"] as System.Collections.IEnumerable;
                if (list == null) return new LyricLine[0];
                // 只接受「曲名 + 歌手」都能对上的候选；对不上宁可不显示，避免配错歌词
                string wantName = Norm(title);
                string wantArtist = Norm(artist);
                foreach (object o in list)
                {
                    Dictionary<string, object> it = o as Dictionary<string, object>;
                    if (it == null || !it.ContainsKey("mid")) continue;
                    string mid = Convert.ToString(it["mid"]);
                    if (string.IsNullOrEmpty(mid)) continue;
                    string nm = it.ContainsKey("name") ? Convert.ToString(it["name"]) : "";
                    string gotName = Norm(nm);
                    if (gotName.Length == 0) continue;

                    // 歌名匹配：完全相等，或一方完整包含另一方且长度差不大
                    bool nameExact = gotName == wantName;
                    bool nameContain = wantName.Length >= 3 && gotName.Length >= 3 &&
                        (gotName.Contains(wantName) || wantName.Contains(gotName));
                    int lenDiff = Math.Abs(gotName.Length - wantName.Length);
                    bool nameOk = nameExact || (nameContain && lenDiff <= 12);

                    // 歌手匹配（QQ 的 singer 是数组）
                    bool artistOk = false;
                    if (wantArtist.Length >= 2 && it.ContainsKey("singer"))
                    {
                        System.Collections.IEnumerable sl = it["singer"] as System.Collections.IEnumerable;
                        if (sl != null)
                        {
                            foreach (object so in sl)
                            {
                                Dictionary<string, object> sj = so as Dictionary<string, object>;
                                if (sj == null || !sj.ContainsKey("name")) continue;
                                string ga = Norm(Convert.ToString(sj["name"]));
                                if (ga.Length == 0) continue;
                                if (ga.Contains(wantArtist) || wantArtist.Contains(ga)) { artistOk = true; break; }
                            }
                        }
                    }

                    if (!nameExact && !(nameOk && artistOk)) continue;

                    string lurl = "https://c.y.qq.com/lyric/fcgi-bin/fcg_query_lyric_new.fcg?songmid=" + mid + "&format=json&nobase64=1";
                    try
                    {
                        string lj = HttpGet(lurl, "https://y.qq.com/");
                        if (lj.StartsWith("MusicJsonCallback"))
                        {
                            int p = lj.IndexOf('(');
                            if (p > 0) lj = lj.Substring(p + 1).TrimEnd(')', ';');
                        }
                        Dictionary<string, object> lr = ser.Deserialize<Dictionary<string, object>>(lj);
                        if (lr == null || !lr.ContainsKey("lyric") || lr["lyric"] == null) continue;
                        string txt = Convert.ToString(lr["lyric"]);
                        if (string.IsNullOrEmpty(txt)) continue;
                        txt = txt.Replace("&apos;", "'").Replace("&quot;", "\"").Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">");
                        LyricLine[] got = Parse(txt);
                        if (got.Length > 0)
                        {
                            Program.Log("qq hit [" + nm + "] exact=" + nameExact + " artist=" + artistOk + " lines=" + got.Length);
                            return got;
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex) { Program.Log("qq err: " + ex.Message); }
            return new LyricLine[0];
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
        private static string _cachedArtKey = "";
        private static byte[] _cachedArtBytes;

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
                        string artKey = ti.Title + "\u0001" + ti.Artist + "\u0001" + ti.Album;
                        if (artKey == _cachedArtKey)
                        {
                            ti.Art = _cachedArtBytes;
                        }
                        else
                        {
                            IRandomAccessStreamReference th = props.Thumbnail;
                            byte[] bytes = th != null ? ReadStream(th) : null;
                            _cachedArtKey = artKey;
                            _cachedArtBytes = bytes;
                            ti.Art = bytes;
                        }
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

    internal sealed class MusicPanel : Window
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
        private const double R = 8;   // 精确贴合 Win11 原生 DWMWCP_ROUND 硬件圆角

        private Rectangle _veil;
        private Grid _card;
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
        private volatile int _animGen;
        private volatile bool _sliding;
        private double _lastProgressRatio = -1;
        private byte[] _lastArt;
        private string _lyricKey = "";
        private LyricLine[] _lyricLines;
        private double _basePos;
        private DateTime _baseStamp = DateTime.Now;
        private int _lyricIndex = -2;

        // 独立原生 DWM 窗口滑入/滑出动画状态
        private IntPtr _hwnd;
        private double _dpiX = 1.0;
        private double _dpiY = 1.0;
        private double _targetLeft;
        private double _targetTop;
        private double _hiddenLeft;
        private double _slideFromX;
        private double _slideToX;
        private double _slideDurMs;
        private bool _slideShowing;
        private bool _slideClockStarted;
        private long _slideStartTicks;

        public MusicPanel()
        {
            DwmGlass.PrepareWindow(this);
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Title = "SideDrawerMusic";
            UseLayoutRounding = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.Grayscale);

            Width = Math.Round(PW * S);
            Height = Math.Round(PH * S);
            Left = -20000;
            Top = 26;

            Build();

            _poll = new DispatcherTimer();
            _poll.Interval = TimeSpan.FromMilliseconds(2000);
            _poll.Tick += delegate { Pump(); };

            _lyricTick = new DispatcherTimer();
            _lyricTick.Interval = TimeSpan.FromMilliseconds(250);
            _lyricTick.Tick += delegate { SyncLyric(); };
        }

        public bool IsShown { get { return _shown; } }

        public void SetPos(double left, double top)
        {
            _targetLeft = left;
            _targetTop = top;
            _hiddenLeft = left + Math.Round(PW * S) + 48;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _hwnd = new WindowInteropHelper(this).Handle;
            int ex = Native.GetWindowLong(_hwnd, Native.GWL_EXSTYLE);
            Native.SetWindowLong(_hwnd, Native.GWL_EXSTYLE, ex | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE);
            // 常驻 Win11 原生 DWM 实时模糊玻璃（纯 BlurBehind 不强加白雾/黑底，通透且支持动态壁纸实时模糊）
            DwmGlass.InitFrame(this, true, 0, 0, 0, 0);
            RefreshDpi();
        }

        private void RefreshDpi()
        {
            try
            {
                double[] d = Glass.Dpi(this);
                if (d[0] > 0.1) _dpiX = d[0];
                if (d[1] > 0.1) _dpiY = d[1];
            }
            catch { }
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
            // 极轻透磨砂（与主面板通透度一致，不再偏黑）
            int a = Math.Max(14, Math.Min(32, alpha));
            SolidColorBrush b = new SolidColorBrush(Color.FromArgb((byte)a, 16, 18, 28));
            b.Freeze();
            _veil.Fill = b;
        }

        // ------------------------------------------------------------ ui
        private void Build()
        {
            _card = new Grid();
            _card.Width = PW;
            _card.Height = PH;
            _card.LayoutTransform = new ScaleTransform(S, S);
            Content = _card;

            Grid layers = new Grid();
            _card.Children.Add(layers);

            Rectangle tint = new Rectangle();
            LinearGradientBrush tb = new LinearGradientBrush();
            tb.StartPoint = new Point(0, 0);
            tb.EndPoint = new Point(0.3, 1);
            tb.GradientStops.Add(new GradientStop(Color.FromArgb(56, 255, 255, 255), 0));
            tb.GradientStops.Add(new GradientStop(Color.FromArgb(22, 255, 255, 255), 0.5));
            tb.GradientStops.Add(new GradientStop(Color.FromArgb(34, 255, 255, 255), 1));
            tb.Freeze();
            tint.Fill = tb;
            layers.Children.Add(tint);

            _veil = new Rectangle();
            SolidColorBrush vb = new SolidColorBrush(Color.FromArgb(20, 16, 18, 28));
            vb.Freeze();
            _veil.Fill = vb;
            layers.Children.Add(_veil);

            Rectangle noise = new Rectangle();
            ImageBrush nb = new ImageBrush(Glass.Noise(96, 96, 11));
            nb.TileMode = TileMode.Tile;
            nb.Viewport = new Rect(0, 0, 96, 96);
            nb.ViewportUnits = BrushMappingMode.Absolute;
            nb.Freeze();
            noise.Fill = nb;
            noise.Opacity = 0.025;
            layers.Children.Add(noise);

            Border rim = new Border();
            rim.CornerRadius = new CornerRadius(R);
            rim.BorderThickness = new Thickness(1);
            LinearGradientBrush rb = new LinearGradientBrush();
            rb.StartPoint = new Point(0, 0);
            rb.EndPoint = new Point(1, 1);
            rb.GradientStops.Add(new GradientStop(Color.FromArgb(150, 255, 255, 255), 0));
            rb.GradientStops.Add(new GradientStop(Color.FromArgb(50, 255, 255, 255), 0.5));
            rb.GradientStops.Add(new GradientStop(Color.FromArgb(95, 255, 255, 255), 1));
            rb.Freeze();
            rim.BorderBrush = rb;
            rim.IsHitTestVisible = false;
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
            RenderOptions.SetBitmapScalingMode(_art, BitmapScalingMode.HighQuality);
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
            SolidColorBrush normalBg = new SolidColorBrush(Color.FromArgb((byte)bgAlpha, 255, 255, 255));
            normalBg.Freeze();
            SolidColorBrush hoverBg = new SolidColorBrush(Color.FromArgb((byte)hover, 255, 255, 255));
            hoverBg.Freeze();
            b.Background = normalBg;
            b.MouseEnter += delegate { b.Background = hoverBg; };
            b.MouseLeave += delegate { b.Background = normalBg; };
            b.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e) { e.Handled = true; onClick(); };
            return b;
        }

        // ------------------------------------------------------------ show / hide
        public bool ContainsPoint(double xDip, double yDip)
        {
            if (!_shown) return false;
            double w = Math.Round(PW * S);
            double h = Math.Round(PH * S);
            return xDip >= _targetLeft && xDip <= _targetLeft + w && yDip >= _targetTop && yDip <= _targetTop + h;
        }

        public void KeepTopmost()
        {
            if (_shown && _hwnd != IntPtr.Zero)
            {
                try { Native.KeepTopmost(_hwnd); } catch { }
            }
        }

        public void Show(double screenLeft, double screenTop, Visual dpiFrom)
        {
            SetPos(screenLeft, screenTop);
            _shown = true;
            _animGen++;

            _card.BeginAnimation(UIElement.OpacityProperty, null);
            _card.Opacity = 1.0;

            Left = _hiddenLeft;
            Top = _targetTop;
            Width = Math.Round(PW * S);
            Height = Math.Round(PH * S);
            if (_hwnd != IntPtr.Zero)
                DwmGlass.MovePx(_hwnd, (int)Math.Round(_hiddenLeft * _dpiX), (int)Math.Round(_targetTop * _dpiY));
            if (!IsVisible) base.Show();
            RefreshDpi();
            if (_hwnd != IntPtr.Zero)
            {
                DwmGlass.MovePx(_hwnd, (int)Math.Round(_hiddenLeft * _dpiX), (int)Math.Round(_targetTop * _dpiY));
                try { Native.KeepTopmost(_hwnd); } catch { }
            }

            StartSlide(_hiddenLeft, _targetLeft, 260, true);
            _poll.Start();
            _lyricTick.Start();
            Pump();
        }

        public new void Hide()
        {
            if (!_shown) return;
            _shown = false;
            _animGen++;
            _poll.Stop();
            _lyricTick.Stop();

            double curLeft = Left;
            if (double.IsNaN(curLeft) || curLeft < -10000) curLeft = _targetLeft;
            StartSlide(curLeft, _hiddenLeft, 220, false);
        }

        public void HideNow()
        {
            _shown = false;
            _animGen++;
            StopSlide();
            _poll.Stop();
            _lyricTick.Stop();
            try { base.Hide(); } catch { }
            Left = -20000;
        }

        private void StartSlide(double fromX, double toX, double durMs, bool showing)
        {
            int gen = _animGen;
            _slideFromX = fromX;
            _slideToX = toX;
            _slideDurMs = durMs;
            _slideShowing = showing;
            _sliding = true;

            int fromPx = (int)Math.Round(fromX * _dpiX);
            int toPx = (int)Math.Round(toX * _dpiX);
            int yPx = (int)Math.Round(_targetTop * _dpiY);
            IntPtr h = _hwnd;

            DwmGlass.SlideHorizontalAsync(
                h, fromPx, toPx, yPx, durMs, showing,
                delegate { return gen != _animGen || !_sliding; },
                delegate(int frames, double totalMs)
                {
                    try
                    {
                        Dispatcher.BeginInvoke(DispatcherPriority.Send, new Action(delegate
                        {
                            if (gen != _animGen) return;
                            _sliding = false;
                            if (showing)
                            {
                                Left = toX;
                            }
                            else if (!_shown)
                            {
                                Left = -20000;
                                if (_hwnd != IntPtr.Zero) DwmGlass.MovePx(_hwnd, -20000, yPx);
                            }
                        }));
                    }
                    catch { }
                });
        }

        private void StopSlide()
        {
            _sliding = false;
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

            string newTitle = string.IsNullOrEmpty(ti.Title) ? "未知曲目" : ti.Title;
            if (_title.Text != newTitle) _title.Text = newTitle;
            string newArtist = string.IsNullOrEmpty(ti.Artist)
                ? (string.IsNullOrEmpty(ti.Album) ? "—" : ti.Album)
                : ti.Artist;
            if (_artist.Text != newArtist) _artist.Text = newArtist;
            string tNow = Clock(ti.Pos);
            if (_timeNow.Text != tNow) _timeNow.Text = tNow;
            string tAll = Clock(ti.Dur);
            if (_timeAll.Text != tAll) _timeAll.Text = tAll;
            SetProgress(ti.Dur > 0 ? ti.Pos / ti.Dur : 0);

            bool playing = ti.Status == 4;
            SetPlayingFlag(playing);
            if (_lyricTick != null)
            {
                if (playing && _shown) { if (!_lyricTick.IsEnabled) _lyricTick.Start(); }
                else { if (_lyricTick.IsEnabled) _lyricTick.Stop(); }
            }
            string glyph = playing ? "\uE769" : "\uE768";
            if (_playGlyph != null && _playGlyph.Text != glyph) _playGlyph.Text = glyph;

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
                        bi.DecodePixelWidth = 320;
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
            if (Math.Abs(ratio - _lastProgressRatio) < 0.002) return;
            _lastProgressRatio = ratio;
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

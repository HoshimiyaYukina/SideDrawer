using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Web.Script.Serialization;
using Microsoft.Win32;
using IOPath = System.IO.Path;

namespace SideDrawer
{
    // ---------------------------------------------------------------- model
    internal class Item
    {
        public string Name;
        public string Path;
        public bool IsFolder;
        public int FolderIndex = -1;
        public double Size;
        public double X = -1;
        public double Y = -1;
        public int Span = 1;
        public ItemCfg Cfg;
        public List<Item> Children = new List<Item>();
        public ImageSource Icon;
    }

    internal class ItemCfg
    {
        public string Path = "";
        public double Size;
        public double X = -1;
        public double Y = -1;
        public int Span = 1;
    }

    internal class GroupDef
    {
        public string Name = "";
        public bool Expanded = false;
        public List<string> Items = new List<string>();
    }

    internal class TagFolder
    {
        public string Name = "";
        public List<ItemCfg> Items = new List<ItemCfg>();
    }

    internal class TagDef
    {
        public string Name = "";
        public List<ItemCfg> Items = new List<ItemCfg>();
        public List<TagFolder> Folders = new List<TagFolder>();
    }

    internal class FolderDef
    {
        public string Name = "";
        public double Size;
        public double X = -1;
        public double Y = -1;
        public int Span = 1;
        public List<ItemCfg> Items = new List<ItemCfg>();
    }

    internal class Cfg
    {
        public double IconSize = 52;
        public bool ShowClock = true;
        public bool Pinned = false;
        public bool MusicPanel = true;
        public double WidthRatio = 0.5;
        public int OpacityLevel = 1;
        public int BlurLevel = 1;
        public int GroupSort = 0;   // 0=创建顺序 1=按名称
        public int IconSort = 0;   // 0=拖入顺序 1=按名称
        public int ClockColor = 0;   // 0=黑色 1=白色
        public bool RainEffect = false;
        public bool HideTagged = true;
        public List<GroupDef> Groups = new List<GroupDef>();
        public List<ItemCfg> Pins = new List<ItemCfg>();
        public List<ItemCfg> Dock = new List<ItemCfg>();
        public List<TagDef> Tags = new List<TagDef>();
        public List<FolderDef> Folders = new List<FolderDef>();
        public string ConfigPath = "";
    }

    // ---------------------------------------------------------------- config
    internal static class ConfigStore
    {
        public static string PathFor()
        {
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            try
            {
                string test = IOPath.Combine(dir, ".wtest");
                File.WriteAllText(test, "x");
                File.Delete(test);
                return IOPath.Combine(dir, "drawer.json");
            }
            catch
            {
                string ad = IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SideDrawer");
                Directory.CreateDirectory(ad);
                return IOPath.Combine(ad, "drawer.json");
            }
        }

        public static string DesktopDir()
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        }

        public static bool Alive(string p)
        {
            return !string.IsNullOrEmpty(p) && (File.Exists(p) || Directory.Exists(p));
        }

        public static ItemCfg ParseItem(object o)
        {
            string s = o as string;
            if (s != null)
            {
                ItemCfg i = new ItemCfg();
                i.Path = s;
                return i;
            }
            Dictionary<string, object> d = o as Dictionary<string, object>;
            if (d != null)
            {
                ItemCfg i = new ItemCfg();
                if (d.ContainsKey("path")) i.Path = Convert.ToString(d["path"]);
                if (d.ContainsKey("size")) i.Size = Convert.ToDouble(d["size"]);
                if (d.ContainsKey("x")) i.X = Convert.ToDouble(d["x"]);
                if (d.ContainsKey("y")) i.Y = Convert.ToDouble(d["y"]);
                if (d.ContainsKey("span")) i.Span = Convert.ToInt32(d["span"]);
                if (string.IsNullOrEmpty(i.Path)) return null;
                return i;
            }
            return null;
        }

        public static object ItemToJson(ItemCfg i)
        {
            if (i.Size <= 0 && i.X < 0 && i.Y < 0 && i.Span <= 1) return i.Path;
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["path"] = i.Path;
            if (i.Size > 0) d["size"] = i.Size;
            if (i.X >= 0) d["x"] = i.X;
            if (i.Y >= 0) d["y"] = i.Y;
            if (i.Span > 1) d["span"] = i.Span;
            return d;
        }

        public static Cfg Load()
        {
            Cfg c = new Cfg();
            c.ConfigPath = PathFor();

            if (File.Exists(c.ConfigPath))
            {
                try
                {
                    string txt = File.ReadAllText(c.ConfigPath, Encoding.UTF8);
                    JavaScriptSerializer ser = new JavaScriptSerializer();
                    Dictionary<string, object> d = ser.Deserialize<Dictionary<string, object>>(txt);
                    if (d != null)
                    {
                        if (d.ContainsKey("iconSize")) c.IconSize = Convert.ToDouble(d["iconSize"]);
                        if (d.ContainsKey("showClock")) c.ShowClock = Convert.ToBoolean(d["showClock"]);
                        if (d.ContainsKey("pinned")) c.Pinned = Convert.ToBoolean(d["pinned"]);
                        if (d.ContainsKey("musicPanel")) c.MusicPanel = Convert.ToBoolean(d["musicPanel"]);
                        if (d.ContainsKey("widthRatio")) c.WidthRatio = Convert.ToDouble(d["widthRatio"]);
                        if (d.ContainsKey("opacityLevel")) c.OpacityLevel = Convert.ToInt32(d["opacityLevel"]);
                        c.BlurLevel = 0;   // 透明玻璃只保留一个版本
            if (d.ContainsKey("groupSort")) c.GroupSort = Convert.ToInt32(d["groupSort"]);
            if (d.ContainsKey("iconSort")) c.IconSort = Convert.ToInt32(d["iconSort"]);
            if (d.ContainsKey("clockColor")) c.ClockColor = Convert.ToInt32(d["clockColor"]);
                        if (d.ContainsKey("rainEffect")) c.RainEffect = Convert.ToBoolean(d["rainEffect"]);
                        if (d.ContainsKey("hideTagged")) c.HideTagged = Convert.ToBoolean(d["hideTagged"]);
                        if (d.ContainsKey("pins"))
                            foreach (object o in (System.Collections.IEnumerable)d["pins"])
                            {
                                ItemCfg it = ParseItem(o);
                                if (it != null) c.Pins.Add(it);
                            }
                        if (d.ContainsKey("groups"))
                            foreach (object o in (System.Collections.IEnumerable)d["groups"])
                            {
                                Dictionary<string, object> gd = o as Dictionary<string, object>;
                                if (gd == null) continue;
                                GroupDef g2 = new GroupDef();
                                if (gd.ContainsKey("name")) g2.Name = Convert.ToString(gd["name"]);
                                if (gd.ContainsKey("expanded")) g2.Expanded = Convert.ToBoolean(gd["expanded"]);
                                if (gd.ContainsKey("items"))
                                    foreach (object io in (System.Collections.IEnumerable)gd["items"]) g2.Items.Add(Convert.ToString(io));
                                c.Groups.Add(g2);
                            }                        if (d.ContainsKey("tags"))
                            foreach (object o in (System.Collections.IEnumerable)d["tags"])
                            {
                                Dictionary<string, object> td = o as Dictionary<string, object>;
                                if (td == null) continue;
                                TagDef tg = new TagDef();
                                if (td.ContainsKey("name")) tg.Name = Convert.ToString(td["name"]);
                                if (td.ContainsKey("items"))
                                    foreach (object io in (System.Collections.IEnumerable)td["items"])
                                    {
                                        ItemCfg ic = ParseItem(io);
                                        if (ic != null) tg.Items.Add(ic);
                                    }
                                if (td.ContainsKey("folders"))
                                    foreach (object fo in (System.Collections.IEnumerable)td["folders"])
                                    {
                                        Dictionary<string, object> fdd = fo as Dictionary<string, object>;
                                        if (fdd == null) continue;
                                        TagFolder tf2 = new TagFolder();
                                        if (fdd.ContainsKey("name")) tf2.Name = Convert.ToString(fdd["name"]);
                                        if (fdd.ContainsKey("items"))
                                            foreach (object io2 in (System.Collections.IEnumerable)fdd["items"])
                                            {
                                                ItemCfg ic2 = ParseItem(io2);
                                                if (ic2 != null) tf2.Items.Add(ic2);
                                            }
                                        tg.Folders.Add(tf2);
                                    }
                                c.Tags.Add(tg);
                            }                        if (d.ContainsKey("dock"))
                            foreach (object o in (System.Collections.IEnumerable)d["dock"])
                            {
                                ItemCfg it = ParseItem(o);
                                if (it != null) c.Dock.Add(it);
                            }
                        if (d.ContainsKey("folders"))
                        {
                            foreach (object fo in (System.Collections.IEnumerable)d["folders"])
                            {
                                Dictionary<string, object> fd = fo as Dictionary<string, object>;
                                if (fd == null) continue;
                                FolderDef f = new FolderDef();
                                if (fd.ContainsKey("name")) f.Name = Convert.ToString(fd["name"]);
                                if (fd.ContainsKey("size")) f.Size = Convert.ToDouble(fd["size"]);
                                if (fd.ContainsKey("x")) f.X = Convert.ToDouble(fd["x"]);
                                if (fd.ContainsKey("y")) f.Y = Convert.ToDouble(fd["y"]);
                                if (fd.ContainsKey("span")) f.Span = Convert.ToInt32(fd["span"]);
                                if (fd.ContainsKey("items"))
                                    foreach (object io in (System.Collections.IEnumerable)fd["items"])
                                    {
                                        ItemCfg it = ParseItem(io);
                                        if (it != null) f.Items.Add(it);
                                    }
                                c.Folders.Add(f);
                            }
                        }
                        c.Pins.RemoveAll(delegate(ItemCfg p) { return !Alive(p.Path); });
                        foreach (FolderDef f in c.Folders)
                            f.Items.RemoveAll(delegate(ItemCfg p) { return !Alive(p.Path); });
                    }
                }
                catch { }
            }

            if (c.Folders.Count == 0 && c.Pins.Count == 0) BuildDefault(c);
            return c;
        }

        /// <summary>按名字猜分类（新用户默认分组用）。</summary>
        private static string GuessGroupName(string name)
        {
            string n = (name ?? "").ToLowerInvariant();
            string[] launch = new string[] { "steam", "epic", "dmm", "dlsite", "taptap", "mihoyo", "launcher", "启动器", "battle.net", "gog", "ubisoft", "uplay", "origin", "ea app", "wegame", "加速器", "galgame" };
            string[] daily = new string[] { "wechat", "微信", "qq", "tencent meeting", "腾讯会议", "edge", "chrome", "firefox", "wallpaper", "网易云", "spotify", "音乐", "视频", "bilibili", "哔哩", "telegram", "discord" };
            foreach (string k in launch) if (n.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) return "Launchers";
            foreach (string k in daily) if (n.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) return "Daily";
            return "Tools";
        }

        public static void BuildDefault(Cfg c)
        {
            string desk = DesktopDir();
            FolderDef games = new FolderDef();
            games.Name = "游戏";
            FolderDef tools = new FolderDef();
            tools.Name = "工具";

            List<string> urls = new List<string>();
            List<string> lnks = new List<string>();
            try
            {
                List<string> _desks = new List<string>();
                _desks.Add(desk);
                string _pub = Environment.GetEnvironmentVariable("PUBLIC");
                if (!string.IsNullOrEmpty(_pub))
                {
                    string _pd = System.IO.Path.Combine(_pub, "Desktop");
                    if (Directory.Exists(_pd)) _desks.Add(_pd);
                }
                foreach (string _dk in _desks)
                foreach (string f in Directory.GetFiles(_dk))
                {
                    string e = IOPath.GetExtension(f).ToLowerInvariant();
                    if (e == ".url") urls.Add(f);
                    else if (e == ".lnk" || e == ".appref-ms" || e == ".exe") lnks.Add(f);
                }
            }
            catch { }

            Program.Log("BuildDefault scan urls=" + urls.Count + " lnks=" + lnks.Count);
            urls.Sort(StringComparer.OrdinalIgnoreCase);
            lnks.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (string u in urls) { ItemCfg i = new ItemCfg(); i.Path = u; games.Items.Add(i); }
            foreach (string l in lnks) { ItemCfg i = new ItemCfg(); i.Path = l; tools.Items.Add(i); }
            c.Folders.Add(games);

            // 新用户默认也使用分类胶囊（Games / Launchers / Daily / Tools）
            Dictionary<string, GroupDef> gm = new Dictionary<string, GroupDef>(StringComparer.OrdinalIgnoreCase);
            string[] gOrder = new string[] { "Launchers", "Daily", "Tools", "Games" };
            foreach (string gn in gOrder)
            {
                GroupDef gd = new GroupDef();
                gd.Name = gn;
                gm[gn] = gd;
                c.Groups.Add(gd);
            }
            List<string> allShort = new List<string>();
            allShort.AddRange(urls);
            allShort.AddRange(lnks);
            HashSet<string> seenName = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string sp in allShort)
            {
                string nm = IOPath.GetFileNameWithoutExtension(sp);
                string norm = nm.ToLowerInvariant().Replace(" ", "");
                if (!seenName.Add(norm)) continue;
                string extp = IOPath.GetExtension(sp).ToLowerInvariant();
                string gname = GuessGroupName(nm);
                if (gname == "Tools" && extp == ".url") gname = "Games";   // 网址快捷方式归到游戏
                gm[gname].Items.Add(sp);
            }

            c.Folders.Add(tools);

            string[] want = new string[] { "DeepSeek", "QQ", "clash", "MuMu", "FL studio", "Magpie", "LetsGal", "Server of dream" };
            foreach (string w in want)
                foreach (string l in lnks)
                    if (IOPath.GetFileName(l).IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0 && c.Pins.Count < 8 && !HasPin(c, l))
                    {
                        ItemCfg i = new ItemCfg();
                        i.Path = l;
                        c.Pins.Add(i);
                    }
            foreach (string l in lnks)
            {
                if (c.Pins.Count >= 7) break;
                if (!HasPin(c, l))
                {
                    ItemCfg i = new ItemCfg();
                    i.Path = l;
                    c.Pins.Add(i);
                }
            }
            Save(c);
        }

        private static bool HasPin(Cfg c, string path)
        {
            foreach (ItemCfg i in c.Pins) if (string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static void Save(Cfg c)
        {
            try
            {
                Dictionary<string, object> d = new Dictionary<string, object>();
                d["iconSize"] = c.IconSize;
                d["showClock"] = c.ShowClock;
                d["pinned"] = c.Pinned;
                d["musicPanel"] = c.MusicPanel;
                d["widthRatio"] = c.WidthRatio;
                d["opacityLevel"] = c.OpacityLevel;
                d["blurLevel"] = c.BlurLevel;
        d["groupSort"] = c.GroupSort;
        d["iconSort"] = c.IconSort;
        d["clockColor"] = c.ClockColor;
                d["rainEffect"] = c.RainEffect;
                d["hideTagged"] = c.HideTagged;
                List<object> pins = new List<object>();
                foreach (ItemCfg i in c.Pins) pins.Add(ItemToJson(i));
                d["pins"] = pins;
                List<object> dock = new List<object>();
                foreach (ItemCfg i in c.Dock) dock.Add(ItemToJson(i));
                d["dock"] = dock;
                List<Dictionary<string, object>> tl = new List<Dictionary<string, object>>();
                foreach (TagDef tg in c.Tags)
                {
                    Dictionary<string, object> td = new Dictionary<string, object>();
                    td["name"] = tg.Name;
                    List<object> til = new List<object>();
                    foreach (ItemCfg ti in tg.Items) til.Add(ItemToJson(ti));
                    td["items"] = til;
                    List<Dictionary<string, object>> tfl = new List<Dictionary<string, object>>();
                    foreach (TagFolder tf3 in tg.Folders)
                    {
                        Dictionary<string, object> tfd = new Dictionary<string, object>();
                        tfd["name"] = tf3.Name;
                        List<object> tfi = new List<object>();
                        foreach (ItemCfg tii in tf3.Items) tfi.Add(ItemToJson(tii));
                        tfd["items"] = tfi;
                        tfl.Add(tfd);
                    }
                    td["folders"] = tfl;
                    tl.Add(td);
                }
                d["tags"] = tl;
                List<Dictionary<string, object>> gl = new List<Dictionary<string, object>>();
                foreach (GroupDef g2 in c.Groups)
                {
                    Dictionary<string, object> gd2 = new Dictionary<string, object>();
                    gd2["name"] = g2.Name;
                    gd2["expanded"] = g2.Expanded;
                    gd2["items"] = g2.Items;
                    gl.Add(gd2);
                }
                d["groups"] = gl;
                List<Dictionary<string, object>> fl = new List<Dictionary<string, object>>();
                foreach (FolderDef f in c.Folders)
                {
                    Dictionary<string, object> fd = new Dictionary<string, object>();
                    fd["name"] = f.Name;
                    if (f.Size > 0) fd["size"] = f.Size;
                    if (f.X >= 0) fd["x"] = f.X;
                    if (f.Y >= 0) fd["y"] = f.Y;
                    if (f.Span > 1) fd["span"] = f.Span;
                    List<object> items = new List<object>();
                    foreach (ItemCfg i in f.Items) items.Add(ItemToJson(i));
                    fd["items"] = items;
                    fl.Add(fd);
                }
                d["folders"] = fl;
                JavaScriptSerializer ser = new JavaScriptSerializer();
                File.WriteAllText(c.ConfigPath, ser.Serialize(d), new UTF8Encoding(false));
            }
            catch { }
        }
    }

    // ---------------------------------------------------------------- icons
    internal static class IconCache
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
        }

        [ComImport, Guid("46EB5926-582E-4017-9FDF-E8998DAA0950"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IImageList
        {
            int Add(IntPtr hbmImage, IntPtr hbmMask, out int pi);
            int ReplaceIcon(int i, IntPtr hicon, out int pi);
            int SetOverlayImage(int iImage, int iOverlay);
            int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);
            int AddMasked(IntPtr hbmImage, int crMask, out int pi);
            int Draw(IntPtr pimldp);
            int Remove(int i);
            int GetIcon(int i, int flags, out IntPtr picon);
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, out SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("shell32.dll")]
        private static extern int SHGetImageList(int iImageList, ref Guid riid, out IImageList ppv);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private const uint SHGFI_ICON = 0x100;
        private const uint SHGFI_LARGEICON = 0x0;
        private const uint SHGFI_SYSICONINDEX = 0x4000;
        private const int SHIL_EXTRALARGE = 0x2;
        private const int SHIL_JUMBO = 0x4;

        private static readonly Dictionary<string, ImageSource> _cache =
            new Dictionary<string, ImageSource>(StringComparer.OrdinalIgnoreCase);
        private static readonly object _lock = new object();

        public static ImageSource Get(string path)
        {
            lock (_lock)
            {
                ImageSource hit;
                if (_cache.TryGetValue(path, out hit)) return hit;
            }
            // 自定义图标优先：<exe目录>\icons\<快捷方式名>.png
            try
            {
                string nm = IOPath.GetFileNameWithoutExtension(path);
                string cp = IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons", nm + ".png");
                if (File.Exists(cp))
                {
                    BitmapImage bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.UriSource = new Uri(cp);
                    bi.EndInit();
                    bi.Freeze();
                    lock (_lock) { _cache[path] = bi; }
                    return bi;
                }
            }
            catch { }
            ImageSource src = null;
            try { src = LoadHighQuality(path); } catch { }
            if (src == null) { try { src = LoadFallback(path); } catch { } }
            if (src == null) src = new DrawingImage();
            else { try { ImageSource t = Trim(src); if (t != null) src = t; } catch { } }
            lock (_lock) { _cache[path] = src; }
            return src;
        }

        private static ImageSource Trim(ImageSource source)
        {
            BitmapSource src = source as BitmapSource;
            if (src == null) return null;
            BitmapSource bgra = src;
            if (src.Format != PixelFormats.Bgra32 && src.Format != PixelFormats.Pbgra32)
                bgra = new FormatConvertedBitmap(src, PixelFormats.Bgra32, null, 0);

            int w = bgra.PixelWidth, h = bgra.PixelHeight;
            if (w < 2 || h < 2 || w > 512 || h > 512) return src;
            int stride = w * 4;
            byte[] px = new byte[stride * h];
            bgra.CopyPixels(px, stride, 0);

            int minX = w, minY = h, maxX = -1, maxY = -1;
            for (int y = 0; y < h; y++)
            {
                int row = y * stride;
                for (int x = 0; x < w; x++)
                {
                    if (px[row + x * 4 + 3] > 20)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }
            if (maxX < 0) return src;

            int cw = maxX - minX + 1, ch = maxY - minY + 1;
            if (cw > w * 0.72 && ch > h * 0.72) return src;

            int pad = Math.Max(1, (int)(Math.Max(cw, ch) * 0.04));
            int x0 = Math.Max(0, minX - pad), y0 = Math.Max(0, minY - pad);
            int x1 = Math.Min(w - 1, maxX + pad), y1 = Math.Min(h - 1, maxY + pad);
            CroppedBitmap crop = new CroppedBitmap(bgra, new Int32Rect(x0, y0, x1 - x0 + 1, y1 - y0 + 1));
            crop.Freeze();
            return crop;
        }

        private static ImageSource FromHIcon(IntPtr h)
        {
            if (h == IntPtr.Zero) return null;
            try
            {
                BitmapSource bs = Imaging.CreateBitmapSourceFromHIcon(h, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                bs.Freeze();
                return bs;
            }
            finally { DestroyIcon(h); }
        }

        private static int SysIndex(string path)
        {
            SHFILEINFO fi = new SHFILEINFO();
            SHGetFileInfo(path, 0, out fi, (uint)Marshal.SizeOf(typeof(SHFILEINFO)), SHGFI_SYSICONINDEX);
            return fi.iIcon;
        }

        private static ImageSource LoadHighQuality(string path)
        {
            int idx = SysIndex(path);
            if (idx <= 0) return null;
            Guid iid = typeof(IImageList).GUID;
            int[] lists = new int[] { SHIL_JUMBO, SHIL_EXTRALARGE };
            foreach (int list in lists)
            {
                IImageList il = null;
                try
                {
                    int hr = SHGetImageList(list, ref iid, out il);
                    if (hr != 0 || il == null) continue;
                    IntPtr h;
                    if (il.GetIcon(idx, 1, out h) == 0 && h != IntPtr.Zero)
                    {
                        ImageSource s = FromHIcon(h);
                        if (s != null) return s;
                    }
                }
                catch { }
                finally { if (il != null) Marshal.ReleaseComObject(il); }
            }
            return null;
        }

        private static ImageSource LoadFallback(string path)
        {
            SHFILEINFO fi = new SHFILEINFO();
            SHGetFileInfo(path, 0, out fi, (uint)Marshal.SizeOf(typeof(SHFILEINFO)), SHGFI_ICON | SHGFI_LARGEICON);
            return FromHIcon(fi.hIcon);
        }
    }

    // ---------------------------------------------------------------- native
    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder sb, int max);
        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll")]
        public static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        public static extern bool EnumWindows(EnumProc lpEnumFunc, IntPtr lParam);
        public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr child, string cls, string win);

        /// <summary>找到桌面图标所在窗口（SHELLDLL_DefView）。</summary>
        public static IntPtr FindDefView()
        {
            IntPtr progman = IntPtr.Zero;
            EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                StringBuilder sb = new StringBuilder(64);
                GetClassName(h, sb, 64);
                if (sb.ToString() == "Progman") { progman = h; return false; }
                return true;
            }, IntPtr.Zero);
            if (progman != IntPtr.Zero)
            {
                IntPtr d = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (d != IntPtr.Zero) return d;
            }
            return progman;
        }
        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int n);
        [DllImport("user32.dll")]
        public static extern bool SetProcessDpiAwarenessContext(IntPtr value);
        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT pt);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; }
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOACTIVATE = 0x0010;

        public static void KeepTopmost(IntPtr h)
        {
            SetWindowPos(h, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }

        public const int GWL_EXSTYLE = -20;
        public const int WS_EX_TOOLWINDOW = 0x00000080;        public const int WS_EX_NOACTIVATE = 0x08000000;
        public const int WM_HOTKEY = 0x0312;
        [DllImport("kernel32.dll")]
        public static extern bool GetSystemTimes(out ulong idleTime, out ulong kernelTime, out ulong userTime);
        [DllImport("user32.dll")]
        public static extern uint GetDpiForSystem();

        public static bool ForegroundIsFullscreen()
        {
            IntPtr h = GetForegroundWindow();
            if (h == IntPtr.Zero) return false;
            StringBuilder sb = new StringBuilder(256);
            GetClassName(h, sb, 256);
            string cls = sb.ToString();
            if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd") return false;
            RECT r;
            if (!GetWindowRect(h, out r)) return false;
            int w = GetSystemMetrics(0), hh = GetSystemMetrics(1);
            return r.Left <= 0 && r.Top <= 0 && r.Right >= w && r.Bottom >= hh;
        }
    }

    // ---------------------------------------------------------------- window
    internal sealed class DrawerWindow : Window
    {
        private const double Radius = 16;
        private const double HotW = 6;
        private const double HotH = 320;

        private readonly Cfg _cfg;
        private double _panelW;
        private double _uiScale = 1.0;   // 分辨率缩放（1104 工作区高度 = 1.0）
        private double _panelH;
        private Rect _workArea;
        private double _visibleRight;
        private double _visibleBottom;

        private bool _open;
        private bool _busy;
        private bool _menuOpen;

        private Grid _root;
        private Grid _panelHost;
        private TranslateTransform _shift;
        private Border _glass;
        private Grid _overlay;
        private Border _nub;
        private Rectangle _veil;
        private Rectangle _tintRect;
        private Rectangle _glowRect;
        private Rectangle _noiseRect;
        private Canvas _rainCanvas;
        private class HeadRef
        {
            public GroupDef G;
            public Border Bg;
            public Grid Host;
        }
        private List<HeadRef> _groupHeads = new List<HeadRef>();
        private bool _dragging;
        private Point _dragStart;
        private Item _dragItem;
        private FrameworkElement _dragTile;
        private Canvas _homeCanvas;
        private Point _dragOffset;
        private double _dragFromX;
        private double _dragFromY;
        private WrapPanel _folderWrap;
        private Grid _homePage;
        private Grid _folderPage;
        private TextBlock _folderTitle;
        private TextBlock _clock;
        private TextBlock _date;
        private Border _pinBtn;
        private TextBlock _memPct, _memSub, _diskPct, _diskSub;
        private TextBlock _cpuPct, _cpuSub, _gpuPct, _gpuSub;
        private Grid _cpuBar, _gpuBar;
        private volatile bool _specWantShow;
        private volatile bool _specWantPlay;
        private double _specWantTop;
        private Thread _specThread;
        private Border _cpuFill, _memFill, _netFill;
        private TextBlock _cpuVal, _memVal, _netVal;
        private ulong _prevIdle, _prevKernel, _prevUser;
        private long _prevNetBytes;
        private DateTime _prevNetStamp = DateTime.MinValue;
        private Grid _memBar, _diskBar;
        private DispatcherTimer _closeTimer;
        private DispatcherTimer _glassTimer;
        private int _refractTick;
        private DispatcherTimer _clockTimer;
        private EdgeTabWindow _tab;
        private Grid _envelope;
        private StackPanel _tagRail;
        private Border _tagPanel;
        private int _tagOpen = -1;
        private double _tagW = 300;
        private Grid _refract;
        private bool _tagDragging;
        private Point _tagDragOffset;
        private Point _tagDragStart;
        private double _tagDragFromX;
        private double _tagDragFromY;
        private double _tagDragStartX;
        private double _tagDragStartY;
        private Item _tagDragItem;
        private FrameworkElement _tagDragTile;
        private MusicPanel _music;

        private bool _panelsReady;

        private readonly FontFamily _ui = new FontFamily("Microsoft YaHei UI");
        private readonly FontFamily _clockFont = new FontFamily("Candara");

        public DrawerWindow()
        {
            _cfg = ConfigStore.Load();

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.Manual;
            Title = "SideDrawer";
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
            TextOptions.SetTextRenderingMode(this, TextRenderingMode.Grayscale);

            Loaded += delegate { EnsurePanels(); if (!_open) ApplyClosedBounds(); };
        }

        private void ComputeWorkArea()
        {
            // 这台机器是 125% 缩放：SystemParameters 给的是 2048 逻辑宽，实际可见只有 1638
            // 窗口必须按“可见宽度”创建，否则右侧 20% 是桌面、贴右边缘的元素会被裁掉
            // 2560x1440 @125% -> 逻辑 2048x1152，工作区 2048x1104
            Rect wa = SystemParameters.WorkArea;
            if (wa.Width < 400 || wa.Height < 300) wa = ScreenUtil.WorkAreaDIP(this);
            _workArea = wa;
            _visibleRight = wa.X + wa.Width;
            _visibleBottom = wa.Y + wa.Height;
        }

        private void InitLayout()
        {
            Rect wa = _workArea;
            _panelW = Math.Round(wa.Width * Math.Max(0.3, Math.Min(0.9, _cfg.WidthRatio)));
            _uiScale = Math.Max(0.75, Math.Min(1.7, wa.Height / 1104.0));
            _panelH = Math.Round(wa.Height);

            BuildUi();

            _closeTimer = new DispatcherTimer();
            _closeTimer.Interval = TimeSpan.FromMilliseconds(1600);
            _closeTimer.Tick += delegate
            {
                _closeTimer.Stop();
                if (_cfg.Pinned || _menuOpen) return;
                if (_tagOpen >= 0) return;
                if (_dragging || _tagDragging) return;
                if (Mouse.LeftButton == MouseButtonState.Pressed) return;
                Point mp = Mouse.GetPosition(this);
                bool overPanel = mp.X >= 0 && mp.X <= _panelW && mp.Y >= 0 && mp.Y <= ActualHeight;
                if (overPanel) return;
                if (_music != null && _music.IsShown && CursorInsideMusic()) return;
                if (CursorOverTagPanel()) return;
                CloseDrawer();
            };

            _clockTimer = new DispatcherTimer();
            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            int ticks = 0;
            _clockTimer.Tick += delegate
            {
                UpdateClock();
                if (++ticks % 3 == 0 && _memPct != null) UpdateWidgets();
                _specWantPlay = _music != null && _music.IsPlayingNow();
            };
            _clockTimer.Start();
            StartSpectrumThread();

            DispatcherTimer topTimer = new DispatcherTimer();
            topTimer.Interval = TimeSpan.FromMilliseconds(600);
            topTimer.Tick += delegate
            {
                if (!_open || _busy || _menuOpen) return;   // 菜单打开期间不抢回置顶
                try { Native.KeepTopmost(new WindowInteropHelper(this).Handle); } catch { }
            };
            topTimer.Start();
            UpdateClock();

        }

        private void EnsurePanels()
        {
            if (_panelsReady) return;
            _panelsReady = true;
            try
            {
                _tab = new EdgeTabWindow(this);
                _tab.Show();
            }
            catch (Exception ex) { Program.Log("tab failed: " + ex); }

        }

        private bool CursorInsideMusic()
        {
            if (_music == null || !_music.IsShown) return false;
            Native.POINT p;
            if (!Native.GetCursorPos(out p)) return false;
            double[] d = Glass.Dpi(this);
            double x = p.X / d[0];
            double y = p.Y / d[1];
            double left = ActualWidth - (MusicPanel.PW * MusicPanel.S) - 18;
            double top = 26;
            return x >= left && x <= left + (MusicPanel.PW * MusicPanel.S) && y >= top && y <= top + (MusicPanel.PH * MusicPanel.S);
        }

        // ------------------------------------------------------------ geometry
        private void ApplyClosedBounds()
        {
            if (_root == null) return;
            Rect wa = _workArea;
            Left = wa.Left;
            Top = wa.Top;
            Width = _panelW;
            Height = _panelH;
            _panelHost.CacheMode = new BitmapCache(1.0);   // 动画期间改用位图缓存，减少每帧重绘
            _shift.BeginAnimation(TranslateTransform.XProperty, null);
            _shift.X = -_panelW;
            Hide();
        }

        private void ApplyOpenBounds()
        {
            Rect wa = _workArea;
            Left = wa.Left;
            Top = wa.Top;
            Width = wa.Width;
            Height = wa.Height;
        }

        // ------------------------------------------------------------ ui
        private void BuildUi()
        {
            _root = new Grid();
            _root.Background = Brushes.Transparent;
            Content = _root;

            _nub = BuildNub();
            _nub.HorizontalAlignment = HorizontalAlignment.Left;
            _nub.VerticalAlignment = VerticalAlignment.Center;
            _nub.Margin = new Thickness(1, 0, 0, 0);
            _root.Children.Add(_nub);
            _nub.Opacity = 0;
            _nub.IsHitTestVisible = false;

            _panelHost = new Grid();
            _panelHost.HorizontalAlignment = HorizontalAlignment.Left;
            _panelHost.Margin = new Thickness(0);
            _panelHost.Width = _panelW;
            _shift = new TranslateTransform(-_panelW, 0);
            _panelHost.RenderTransform = _shift;
            _root.Children.Add(_panelHost);

            MusicPanel.SetScale(_uiScale);
            _music = new MusicPanel();
            _root.Children.Add(_music);
            if (Environment.GetEnvironmentVariable("SIDEDRAWER_MARK") == "1")
            {
                Canvas cv = new Canvas();
                double[] xs = new double[] { 1100, 1300, 1500, 1700, 1900 };
                Color[] cs = new Color[] { Colors.Red, Colors.Lime, Colors.Blue, Colors.Magenta, Colors.Orange };
                for (int i = 0; i < xs.Length; i++)
                {
                    Border mk = new Border();
                    mk.Width = 80; mk.Height = 80;
                    mk.Background = new SolidColorBrush(cs[i]);
                    Canvas.SetLeft(mk, xs[i]);
                    Canvas.SetTop(mk, 600);
                    cv.Children.Add(mk);
                }
                _root.Children.Add(cv);
            }
            if (Environment.GetEnvironmentVariable("SIDEDRAWER_REDTEST") == "1")
            {
                Border rbt = new Border();
                rbt.Width = 200; rbt.Height = 200; rbt.Background = Brushes.Red;
                rbt.HorizontalAlignment = HorizontalAlignment.Right;
                rbt.VerticalAlignment = VerticalAlignment.Top;
                _root.Children.Add(rbt);
            }

            _glass = new Border();
            _glass.HorizontalAlignment = HorizontalAlignment.Left;
            _glass.Width = _panelW;
            _glass.Height = _panelH;
            _glass.CornerRadius = new CornerRadius(Radius);
            _glass.BorderThickness = new Thickness(1);
            _glass.BorderBrush = RimBrush();
            // 去掉整块玻璃的阴影：44px 模糊每帧重绘，滑动时会掉帧'

            _overlay = new Grid();
            _overlay.OpacityMask = RoundedMask(_panelW, _panelH, Radius);
            _glass.Child = _overlay;
            _panelHost.Children.Add(_glass);

            Rectangle tint = new Rectangle();
            LinearGradientBrush lg = new LinearGradientBrush();
            lg.StartPoint = new Point(0, 0);
            lg.GradientStops.Add(Stop(Color.FromArgb(52, 255, 255, 255), 0));
            lg.GradientStops.Add(Stop(Color.FromArgb(18, 255, 255, 255), 0.45));
            lg.GradientStops.Add(Stop(Color.FromArgb(34, 255, 255, 255), 1));
            lg.GradientStops.Add(Stop(Color.FromArgb(34, 255, 255, 255), 1));
            tint.Fill = lg;


            _veil = new Rectangle();
            _veil.Fill = new SolidColorBrush(ToArgb(VeilAlpha(), 10, 10, 16));


            Rectangle glow = new Rectangle();
            RadialGradientBrush rg = new RadialGradientBrush();
            rg.Center = new Point(0.2, 0.0);
            rg.GradientOrigin = new Point(0.15, 0.0);
            rg.RadiusX = 1.1;
            rg.RadiusY = 0.65;
            rg.GradientStops.Add(Stop(Color.FromArgb(70, 255, 255, 255), 0));
            rg.GradientStops.Add(Stop(Color.FromArgb(0, 255, 255, 255), 1));
            glow.Fill = rg;


            Rectangle scrimTop = new Rectangle();
            LinearGradientBrush stg = new LinearGradientBrush();
            stg.StartPoint = new Point(0, 0);
            stg.EndPoint = new Point(0, 1);
            stg.GradientStops.Add(Stop(Color.FromArgb(96, 0, 0, 0), 0));
            stg.GradientStops.Add(Stop(Color.FromArgb(30, 0, 0, 0), 0.18));
            stg.GradientStops.Add(Stop(Color.FromArgb(0, 0, 0, 0), 0.38));
            scrimTop.Fill = stg;
            _overlay.Children.Add(scrimTop);

            Rectangle scrimBottom = new Rectangle();
            LinearGradientBrush sbg = new LinearGradientBrush();
            sbg.StartPoint = new Point(0, 0);
            sbg.EndPoint = new Point(0, 1);
            sbg.GradientStops.Add(Stop(Color.FromArgb(0, 0, 0, 0), 0.6));
            sbg.GradientStops.Add(Stop(Color.FromArgb(52, 0, 0, 0), 1));
            scrimBottom.Fill = sbg;
            _overlay.Children.Add(scrimBottom);

            Rectangle noise = new Rectangle();
            ImageBrush nb = new ImageBrush(MakeNoise(96, 96));
            nb.TileMode = TileMode.Tile;
            nb.Viewport = new Rect(0, 0, 96, 96);
            nb.ViewportUnits = BrushMappingMode.Absolute;
            noise.Fill = nb;




            Grid sweepHost = new Grid();
            sweepHost.ClipToBounds = true;
            Rectangle sweep = new Rectangle();
            sweep.Width = 2400;
            sweep.Height = 6000;
            sweep.RenderTransform = new RotateTransform(-18);
            LinearGradientBrush sg = new LinearGradientBrush();
            sg.StartPoint = new Point(0, 0);
            sg.EndPoint = new Point(1, 0);
            sg.GradientStops.Add(Stop(Color.FromArgb(0, 255, 255, 255), 0));
            sg.GradientStops.Add(Stop(Color.FromArgb(0, 255, 255, 255), 0.4));
            sg.GradientStops.Add(Stop(Color.FromArgb(16, 255, 255, 255), 0.48));
            sg.GradientStops.Add(Stop(Color.FromArgb(26, 255, 255, 255), 0.5));
            sg.GradientStops.Add(Stop(Color.FromArgb(16, 255, 255, 255), 0.52));
            sg.GradientStops.Add(Stop(Color.FromArgb(0, 255, 255, 255), 0.6));
            sg.GradientStops.Add(Stop(Color.FromArgb(0, 255, 255, 255), 1));
            sweep.Fill = sg;
            sweep.HorizontalAlignment = HorizontalAlignment.Left;
            sweep.VerticalAlignment = VerticalAlignment.Center;
            TranslateTransform st = new TranslateTransform(-1100, 0);
            Grid holder = new Grid();
            holder.RenderTransform = st;
            holder.Children.Add(sweep);
            sweepHost.Children.Add(holder);
            // 扫光已停用：它每 9 秒在面板上扫过一条白色高光，会让颜色忽明忽暗
            if (Environment.GetEnvironmentVariable("SIDEDRAWER_SWEEP") == "1") _overlay.Children.Add(sweepHost);

            DoubleAnimation anim = new DoubleAnimation(-1100, _panelW + 500, TimeSpan.FromSeconds(9));
            anim.RepeatBehavior = RepeatBehavior.Forever;
            anim.BeginTime = TimeSpan.FromSeconds(3);
            SineEase ease = new SineEase();
            ease.EasingMode = EasingMode.EaseInOut;
            anim.EasingFunction = ease;
            if (Environment.GetEnvironmentVariable("SIDEDRAWER_SWEEP") == "1") st.BeginAnimation(TranslateTransform.XProperty, anim);



            Grid content = new Grid();
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid head = BuildHeader();
            Grid.SetRow(head, 0);
            content.Children.Add(head);

            Rectangle line = new Rectangle();
            line.Height = 1;
            line.Margin = new Thickness(26, 2, 26, 0);
            line.Fill = new LinearGradientBrush(new GradientStopCollection {
                    Stop(Color.FromArgb(0,255,255,255), 0),
                    Stop(Color.FromArgb(54,255,255,255), 0.5),
                    Stop(Color.FromArgb(0,255,255,255), 1)
                }, new Point(0, 0), new Point(1, 0));
            Grid.SetRow(line, 1);
            content.Children.Add(line);

            Grid pages = new Grid();
            Grid.SetRow(pages, 2);
            content.Children.Add(pages);

            _homePage = new Grid();
            _homePage.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _homePage.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _homePage.RowDefinitions.Add(new RowDefinition { Height = new GridLength(54) });   // 底部装饰行（固定）

            FrameworkElement widgets = BuildWidgetStrip();
            Grid.SetRow(widgets, 0);
            _homePage.Children.Add(widgets);

            ScrollViewer sv = new ScrollViewer();
            sv.Margin = new Thickness(0, 0, 0, 16);   // 底部死区（压低）
            sv.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            sv.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            sv.PanningMode = PanningMode.VerticalOnly;
            _homeCanvas = new Canvas();
            _homeCanvas.Margin = new Thickness(0, 0, 0, 0);
            _homeCanvas.HorizontalAlignment = HorizontalAlignment.Left;
            _homeCanvas.VerticalAlignment = VerticalAlignment.Top;
            sv.Content = _homeCanvas;
            sv.PreviewMouseWheel += delegate(object s, MouseWheelEventArgs e)
            {
                Program.Log("WHEEL delta=" + e.Delta + " off=" + sv.VerticalOffset.ToString("0") + " svH=" + sv.ActualHeight.ToString("0") + " ext=" + sv.ExtentHeight.ToString("0") + " canvas=" + (_homeCanvas == null ? -1 : _homeCanvas.ActualHeight) + " h=" + (_homeCanvas == null ? -1 : _homeCanvas.Height) + " vis=" + (_homeCanvas == null ? "null" : _homeCanvas.Visibility.ToString()) + " kids=" + (_homeCanvas == null ? -1 : _homeCanvas.Children.Count));
            };
            Grid.SetRow(sv, 1);
            _homePage.Children.Add(sv);
            // 固定底部装饰（第 3 行，不随滚动）
            try
            {
                FrameworkElement sF = BuildStrip();
                sF.HorizontalAlignment = HorizontalAlignment.Left;
                sF.Margin = new Thickness(12, 3, 0, 3);
                sF.IsHitTestVisible = false;
                Grid.SetRow(sF, 2);
                _homePage.Children.Add(sF);
                Program.Log("STRIP F ok");
            }
            catch (Exception ex) { Program.Log("STRIP F fail " + ex.Message); }


            _homePage.RenderTransform = new TranslateTransform(0, 0);
            pages.Children.Add(_homePage);

            _folderPage = new Grid();
            _folderPage.Opacity = 0;
            _folderPage.Visibility = Visibility.Collapsed;
            _folderPage.RenderTransform = new TranslateTransform(26, 0);
            ScrollViewer sv2 = new ScrollViewer();
            sv2.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            sv2.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            StackPanel stack = new StackPanel();
            stack.Margin = new Thickness(20, 12, 12, 20);
            Border back = new Border();
            back.CornerRadius = new CornerRadius(9);
            back.Padding = new Thickness(8, 4, 14, 4);
            back.HorizontalAlignment = HorizontalAlignment.Left;
            back.Background = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255));
            back.MouseLeftButtonUp += delegate { CloseFolder(); };
            back.MouseEnter += delegate { back.Background = new SolidColorBrush(Color.FromArgb(46, 255, 255, 255)); };
            back.MouseLeave += delegate { back.Background = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)); };
            back.Cursor = Cursors.Hand;
            TextBlock backTxt = new TextBlock();
            backTxt.Text = "\u2039  返回";
            backTxt.FontFamily = _ui;
            backTxt.FontSize = 11.5;
            backTxt.Foreground = new SolidColorBrush(Color.FromArgb(220, 255, 255, 255));
            back.Child = backTxt;
            stack.Children.Add(back);

            _folderTitle = new TextBlock();
            _folderTitle.FontFamily = _ui;
            _folderTitle.FontSize = 19;
            _folderTitle.FontWeight = FontWeights.SemiBold;
            _folderTitle.Foreground = Brushes.White;
            TextShadow(_folderTitle);
            _folderTitle.Margin = new Thickness(6, 12, 0, 2);
            stack.Children.Add(_folderTitle);

            _folderWrap = new WrapPanel();
            _folderWrap.Margin = new Thickness(0, 4, 0, 0);
            stack.Children.Add(_folderWrap);
            sv2.Content = stack;
            _folderPage.Children.Add(sv2);
            pages.Children.Add(_folderPage);

            TextBlock footer = new TextBlock();
            footer.Text = "右键设置    ·    Ctrl + Alt + D 呼出";
            footer.FontFamily = _ui;
            footer.FontSize = 10;
            footer.Foreground = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
            footer.HorizontalAlignment = HorizontalAlignment.Center;
            footer.Margin = new Thickness(0, 6, 0, 10);

            Grid.SetRow(footer, 3);
            content.Children.Add(footer);

            _refract = new Grid();
            _refract.IsHitTestVisible = false;
            // 液态玻璃：斜向高光 + 底部提亮 + 极淡的底色
            Grid liquid = new Grid();
            liquid.IsHitTestVisible = false;

            Rectangle spec = new Rectangle();
            LinearGradientBrush lqSpec = new LinearGradientBrush();
            lqSpec.StartPoint = new Point(0, 0);
            lqSpec.EndPoint = new Point(0.8, 1);
            lqSpec.GradientStops.Add(new GradientStop(Color.FromArgb(30, 255, 255, 255), 0));
            lqSpec.GradientStops.Add(new GradientStop(Color.FromArgb(8, 255, 255, 255), 0.28));
            lqSpec.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.5));
            lqSpec.GradientStops.Add(new GradientStop(Color.FromArgb(16, 255, 255, 255), 0.78));
            lqSpec.GradientStops.Add(new GradientStop(Color.FromArgb(2, 255, 255, 255), 1));
            spec.Fill = sg;
            spec.IsHitTestVisible = false;
            liquid.Children.Add(spec);

            Rectangle bottomGlow = new Rectangle();
            LinearGradientBrush lqBot = new LinearGradientBrush();
            lqBot.StartPoint = new Point(0, 0);
            lqBot.EndPoint = new Point(0, 1);
            lqBot.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.55));
            lqBot.GradientStops.Add(new GradientStop(Color.FromArgb(22, 255, 255, 255), 1));
            bottomGlow.Fill = lqBot;
            bottomGlow.IsHitTestVisible = false;
            liquid.Children.Add(bottomGlow);

            _overlay.Children.Add(liquid);


            // 镜面高光边：左上最亮、右下留一点冷光（液态玻璃的边缘反光）
            Border rim = new Border();
            rim.CornerRadius = new CornerRadius(Radius);
            rim.BorderThickness = new Thickness(1.5);
            LinearGradientBrush rb2 = new LinearGradientBrush();
            rb2.StartPoint = new Point(0, 0);
            rb2.EndPoint = new Point(1, 1);
            rb2.GradientStops.Add(new GradientStop(Color.FromArgb(150, 255, 255, 255), 0));
            rb2.GradientStops.Add(new GradientStop(Color.FromArgb(60, 255, 255, 255), 0.25));
            rb2.GradientStops.Add(new GradientStop(Color.FromArgb(16, 255, 255, 255), 0.55));
            rb2.GradientStops.Add(new GradientStop(Color.FromArgb(40, 210, 230, 255), 0.82));
            rb2.GradientStops.Add(new GradientStop(Color.FromArgb(96, 255, 255, 255), 1));
            rim.BorderBrush = rb2;
            rim.IsHitTestVisible = false;
            _overlay.Children.Add(rim);
            _overlay.Children.Add(_refract);

            _overlay.Children.Add(content);


            // 暗角：四周压暗、中心透亮
            Rectangle vignette = new Rectangle();
            RadialGradientBrush vg = new RadialGradientBrush();
            vg.Center = new Point(0.5, 0.5);
            vg.GradientOrigin = new Point(0.5, 0.5);
            vg.RadiusX = 0.72;
            vg.RadiusY = 0.72;
            vg.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0));
            vg.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.52));
            vg.GradientStops.Add(new GradientStop(Color.FromArgb(120, 0, 0, 0), 0.78));
            vg.GradientStops.Add(new GradientStop(Color.FromArgb(255, 0, 0, 0), 1));
            vignette.Fill = vg;
            vignette.IsHitTestVisible = false;
            Panel.SetZIndex(vignette, -50);   // 暗角只压玻璃底，不压文字/图标
            _overlay.Children.Add(vignette);

            try
            {
                FrameworkElement frame = OrnateFrame.Create(_panelW, _panelH, 10, Color.FromRgb(0xFF, 0xA8, 0xC0), 0.92);
                _overlay.Children.Add(frame);

            }
            catch { }


            _envelope = BuildEnvelopeHandle();
            _envelope.HorizontalAlignment = HorizontalAlignment.Left;
            _envelope.VerticalAlignment = VerticalAlignment.Center;
            _envelope.Margin = new Thickness(_panelW - 8, 0, 0, 0);
            _panelHost.Children.Add(_envelope);

            BuildTagRail();

            _glass.MouseEnter += delegate { _closeTimer.Stop(); };
            _glass.MouseLeave += delegate { StartCloseTimer(); };
            _glass.MouseRightButtonUp += delegate { OpenMenuInPanel(); };
            _nub.MouseEnter += delegate { if (!_open) Open(); };

            RebuildTiles();
            ApplyGlassStrength();
        }

        /// <summary>右侧的“信封”把手，点一下收起侧边栏。</summary>
        /// <summary>底部固定栏（手机桌面 dock）。</summary>
        /// <summary>右侧标签栏：点开在侧边栏与音乐面板之间弹出归类窗口。</summary>
        private void BuildTagRail()
        {
            _tagRail = new StackPanel();
            _tagRail.Orientation = Orientation.Vertical;
            _tagRail.HorizontalAlignment = HorizontalAlignment.Right;
            _tagRail.VerticalAlignment = VerticalAlignment.Top;
            _tagRail.Margin = new Thickness(0, 96, 16, 0);
            _panelHost.Children.Add(_tagRail);
            RefreshTagRail();
        }

        private void RefreshTagRail()
        {
            if (_tagRail == null) return;
            _tagRail.Children.Clear();

            for (int i = 0; i < _cfg.Tags.Count; i++)
            {
                int idx = i;
                TagDef tg = _cfg.Tags[i];
                bool sel = _tagOpen == i;

                Grid root = new Grid();
                root.Width = 108;
                root.Height = 36;
                root.Margin = new Thickness(0, 0, 0, 8);
                root.Cursor = Cursors.Hand;
                root.ToolTip = tg.Name + "（" + tg.Items.Count + " 个应用，右键重命名/删除）";

                // 书签形状：左端圆角，右端 V 形缺口
                System.Windows.Shapes.Path body = new System.Windows.Shapes.Path();
                body.Data = Geometry.Parse("M100,0 H6 A6,6 0 0 0 0,6 V30 A6,6 0 0 0 6,36 H100 A6,6 0 0 1 106,30 V26 L90,18 L106,10 V6 A6,6 0 0 1 100,0 Z");

                LinearGradientBrush lg = new LinearGradientBrush();
                lg.StartPoint = new Point(0, 0);
                lg.EndPoint = new Point(0.25, 1);
                if (sel)
                {
                    lg.GradientStops.Add(new GradientStop(Color.FromArgb(252, 255, 255, 255), 0));
                    lg.GradientStops.Add(new GradientStop(Color.FromArgb(236, 226, 232, 248), 0.55));
                    lg.GradientStops.Add(new GradientStop(Color.FromArgb(216, 198, 206, 232), 1));
                }
                else
                {
                    lg.GradientStops.Add(new GradientStop(Color.FromArgb(210, 58, 60, 78), 0));
                    lg.GradientStops.Add(new GradientStop(Color.FromArgb(188, 30, 32, 44), 0.55));
                    lg.GradientStops.Add(new GradientStop(Color.FromArgb(206, 16, 18, 26), 1));
                }
                body.Fill = lg;
                body.Stroke = new SolidColorBrush(Color.FromArgb(sel ? (byte)90 : (byte)70, 255, 255, 255));
                body.StrokeThickness = 1;

                DropShadowEffect bs = new DropShadowEffect();
                bs.Color = Colors.Black;
                bs.BlurRadius = 10;
                bs.ShadowDepth = 2;
                bs.Direction = 270;
                bs.Opacity = 0.45;
                body.Effect = bs;
                root.Children.Add(body);

                // 顶部高光（沿着上边缘的一条细亮线）
                System.Windows.Shapes.Path gloss = new System.Windows.Shapes.Path();
                gloss.Data = Geometry.Parse("M100,2 H8 A4,4 0 0 0 4,6");
                gloss.Stroke = new SolidColorBrush(Color.FromArgb(sel ? (byte)60 : (byte)46, 255, 255, 255));
                gloss.StrokeThickness = 2;
                gloss.StrokeStartLineCap = PenLineCap.Round;
                gloss.StrokeEndLineCap = PenLineCap.Round;
                gloss.IsHitTestVisible = false;
                root.Children.Add(gloss);

                // 左侧的彩色小色块
                Border accent = new Border();
                accent.Width = 6;
                accent.Height = 16;
                accent.CornerRadius = new CornerRadius(3);
                accent.HorizontalAlignment = HorizontalAlignment.Left;
                accent.VerticalAlignment = VerticalAlignment.Center;
                accent.Margin = new Thickness(9, 0, 0, 0);
                Color[] palette = new Color[] {
                    Color.FromArgb(255, 255, 138, 128), Color.FromArgb(255, 140, 200, 255),
                    Color.FromArgb(255, 168, 236, 180), Color.FromArgb(255, 250, 220, 130),
                    Color.FromArgb(255, 210, 170, 255) };
                accent.Background = new SolidColorBrush(palette[i % palette.Length]);
                accent.Opacity = sel ? 0.95 : 0.75;
                accent.IsHitTestVisible = false;
                root.Children.Add(accent);

                TextBlock tb = new TextBlock();
                tb.Text = tg.Name;
                tb.FontFamily = new FontFamily("Candara, Microsoft YaHei UI");
                tb.FontSize = 12;
                tb.FontWeight = FontWeights.Medium;
                tb.Foreground = new SolidColorBrush(sel ? Color.FromArgb(255, 18, 18, 26) : Color.FromArgb(240, 255, 255, 255));
                tb.TextAlignment = TextAlignment.Center;
                tb.HorizontalAlignment = HorizontalAlignment.Center;
                tb.VerticalAlignment = VerticalAlignment.Center;
                tb.Margin = new Thickness(22, 0, 14, 0);
                tb.IsHitTestVisible = false;
                root.Children.Add(tb);

                root.Opacity = sel ? 1.0 : 0.9;
                root.MouseEnter += delegate { root.Opacity = 1.0; };
                root.MouseLeave += delegate { root.Opacity = sel ? 1.0 : 0.9; };
                root.MouseLeftButtonUp += delegate(object o, MouseButtonEventArgs e)
                {
                    e.Handled = true;
                    ToggleTag(idx);
                };
                root.MouseRightButtonUp += delegate(object o, MouseButtonEventArgs e)
                {
                    e.Handled = true;
                    OpenTagMenu(idx, root);
                };

                _tagRail.Children.Add(root);
            }
        }

        private void OpenTagMenu(int idx, FrameworkElement target)
        {
            if (idx < 0 || idx >= _cfg.Tags.Count) return;
            ContextMenu cm = new ContextMenu();
            cm.Background = new SolidColorBrush(Color.FromArgb(250, 20, 20, 26));
            cm.Foreground = Brushes.White;

            MenuItem ren = new MenuItem();
            ren.Header = "重命名标签";
            ren.Click += delegate
            {
                string nn = PromptWindow.Show(this, "重命名标签", _cfg.Tags[idx].Name);
                if (!string.IsNullOrWhiteSpace(nn))
                {
                    _cfg.Tags[idx].Name = nn.Trim();
                    ConfigStore.Save(_cfg);
                    RefreshTagRail();
                    if (_tagOpen == idx) ShowTagPanel(idx);
                }
            };
            cm.Items.Add(ren);

            MenuItem del = new MenuItem();
            del.Header = "删除标签";
            del.Click += delegate
            {
                if (_tagOpen == idx) HideTagPanel();
                _cfg.Tags.RemoveAt(idx);
                ConfigStore.Save(_cfg);
                _tagOpen = -1;
                RefreshTagRail();
            };
            cm.Items.Add(new Separator());
            cm.Items.Add(del);

            cm.PlacementTarget = target;
            cm.IsOpen = true;
        }
        private void ToggleTag(int index)
        {
            if (_tagOpen == index) { HideTagPanel(); return; }
            _tagOpen = index;
            ShowTagPanel(index);
            RefreshTagRail();
        }

        private void HideTagPanel()
        {
            _tagOpen = -1;
            if (_tagPanel != null) _tagPanel.Visibility = Visibility.Collapsed;
            RefreshTagRail();
        }

        private Canvas _tagCanvas;

        private void ShowTagPanel(int index)
        {
            if (index < 0 || index >= _cfg.Tags.Count) return;
            TagDef tg = _cfg.Tags[index];

            if (_tagPanel == null)
            {
                _tagPanel = new Border();
                _tagPanel.CornerRadius = new CornerRadius(22);
                LinearGradientBrush tagBg = new LinearGradientBrush();
                tagBg.StartPoint = new Point(0, 0);
                tagBg.EndPoint = new Point(0.4, 1);
                tagBg.GradientStops.Add(new GradientStop(Color.FromArgb(236, 255, 255, 255), 0));
                tagBg.GradientStops.Add(new GradientStop(Color.FromArgb(214, 240, 242, 250), 0.6));
                tagBg.GradientStops.Add(new GradientStop(Color.FromArgb(228, 246, 246, 252), 1));
                _tagPanel.Background = tagBg;
                _tagPanel.BorderThickness = new Thickness(1);
                _tagPanel.BorderBrush = new SolidColorBrush(ToArgb(46, 255, 255, 255));
                DropShadowEffect tsh = new DropShadowEffect();
                tsh.Color = Colors.Black;
                tsh.BlurRadius = 30;
                tsh.ShadowDepth = 0;
                tsh.Opacity = 0.5;
                _tagPanel.Effect = tsh;
                _tagPanel.HorizontalAlignment = HorizontalAlignment.Left;
                _tagPanel.VerticalAlignment = VerticalAlignment.Top;
                _root.Children.Add(_tagPanel);
            }

            double musicLeft = _workArea.Width - 372 - 18;
            double gapW = musicLeft - _panelW - 44;
            if (gapW < 300) gapW = 300;
            _tagPanel.Width = gapW;
            _tagPanel.Height = _panelH * 2.0 / 3.0;
            _tagPanel.Margin = new Thickness(_panelW + 22, 26, 0, 0);
            double clipH = _panelH * 2.0 / 3.0;
            _tagPanel.Clip = new RectangleGeometry(new Rect(0, 0, gapW, clipH), 22, 22);
            _tagW = gapW - 40;


            Grid shell = new Grid();
            shell.Margin = new Thickness(16, 14, 16, 16);
            shell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });



            // 白色磨砂的漫射光：中心偏亮，边缘略暗，模拟模糊后的柔光
            Rectangle frost = new Rectangle();
            RadialGradientBrush frb = new RadialGradientBrush();
            frb.Center = new Point(0.42, 0.35);
            frb.GradientOrigin = new Point(0.42, 0.35);
            frb.RadiusX = 0.85;
            frb.RadiusY = 0.85;
            frb.GradientStops.Add(new GradientStop(Color.FromArgb(70, 255, 255, 255), 0));
            frb.GradientStops.Add(new GradientStop(Color.FromArgb(24, 255, 255, 255), 0.55));
            frb.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1));
            frost.Fill = frb;
            frost.IsHitTestVisible = false;
            frost.RadiusX = 22;
            frost.RadiusY = 22;
            shell.Children.Insert(0, frost);
            Rectangle tspec = new Rectangle();
            LinearGradientBrush tsg = new LinearGradientBrush();
            tsg.StartPoint = new Point(0, 0);
            tsg.EndPoint = new Point(0.8, 1);
            tsg.GradientStops.Add(new GradientStop(Color.FromArgb(34, 255, 255, 255), 0));
            tsg.GradientStops.Add(new GradientStop(Color.FromArgb(10, 255, 255, 255), 0.3));
            tsg.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0.55));
            tsg.GradientStops.Add(new GradientStop(Color.FromArgb(18, 255, 255, 255), 0.82));
            tsg.GradientStops.Add(new GradientStop(Color.FromArgb(4, 255, 255, 255), 1));
            tspec.Fill = tsg;
            tspec.IsHitTestVisible = false;
            tspec.Margin = new Thickness(-16, -14, -16, -16);
            tspec.RadiusX = 22;
            tspec.RadiusY = 22;
            shell.Children.Insert(0, tspec);



            TextBlock title = new TextBlock();
            title.Text = tg.Name + "   ·   " + tg.Items.Count;
            title.FontFamily = _ui;
            title.FontSize = 14;
            title.FontWeight = FontWeights.SemiBold;
            title.Foreground = new SolidColorBrush(Color.FromArgb(240, 28, 28, 40));
            title.Margin = new Thickness(4, 0, 0, 12);
            Grid.SetRow(title, 0);
            shell.Children.Add(title);

            ScrollViewer sv = new ScrollViewer();
            sv.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            sv.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;

            _tagCanvas = new Canvas();
            _tagCanvas.Width = _tagW;
            _tagCanvas.Height = 460;
            _tagCanvas.HorizontalAlignment = HorizontalAlignment.Left;
            _tagCanvas.VerticalAlignment = VerticalAlignment.Top;
            sv.Content = _tagCanvas;
            _tagScroll = sv;
            Grid.SetRow(sv, 1);
            shell.Children.Add(sv);

            _tagPanel.Child = shell;

            _tagPanel.Visibility = Visibility.Visible;
            RebuildTagTiles(index);
        }

        /// <summary>标签内的图标：与首页一样按格排布、可拖动、可右调占位。</summary>
        private int _tagFolder = -1;
        private int _openFolderIdx = -1;
        private bool _inFolderPage;
        private bool _imported;
        private Canvas _petals;
        private System.Collections.Generic.List<TransformGroup> _petalTf = new System.Collections.Generic.List<TransformGroup>();
        private System.Collections.Generic.List<double[]> _petalData = new System.Collections.Generic.List<double[]>();
        private DispatcherTimer _petalTimer;
        private List<Border> _specBars = new List<Border>();
        private double[] _specVal = new double[0];
        private DispatcherTimer _specTimer;
        private Random _specRnd = new Random();
        private Canvas _specCanvas;
        private double _specBaseY = 14;
        private bool _fDrag;
        private Point _fStart;
        private FrameworkElement _fTile;
        private bool _fHooked;
        private ScrollViewer _tagScroll;
        private BitmapSource _tagGlass;
        private int _tagIndexNow = -1;

        private void RebuildTagTiles(int tagIndex)
        {
            if (_tagCanvas == null) return;
            _tagCanvas.Children.Clear();
            if (tagIndex < 0 || tagIndex >= _cfg.Tags.Count) return;

            _tagIndexNow = tagIndex;
            TagDef tg = _cfg.Tags[tagIndex];
            bool inFolder = _tagFolder >= 0 && _tagFolder < tg.Folders.Count;
            List<ItemCfg> items = inFolder ? tg.Folders[_tagFolder].Items : tg.Items;

            double cell = 78;
            int cols = Math.Max(1, (int)(_tagW / cell));
            HashSet<string> used = new HashSet<string>();
            double maxY = 0;
            int slot = 0;

            if (inFolder)
            {
                FrameworkElement back = BuildBackTile(tagIndex);
                Canvas.SetLeft(back, 6);
                Canvas.SetTop(back, 4);
                _tagCanvas.Children.Add(back);
                Mark(used, 1, 0, 0);
                slot = 1;
            }

            // 已在侧边栏文件夹里的图标，标签窗口不再重复显示
            List<string> inFolders = new List<string>();
            foreach (FolderDef fdx in _cfg.Folders)
                foreach (ItemCfg fdx2 in fdx.Items) inFolders.Add(fdx2.Path);

            foreach (ItemCfg ic in items)
            {

                if (!ConfigStore.Alive(ic.Path)) continue;
                Item it = new Item();
                it.Name = IOPath.GetFileNameWithoutExtension(ic.Path);
                it.Path = ic.Path;
                it.Size = ic.Size;
                it.X = ic.X;
                it.Y = ic.Y;
                it.Span = ic.Span;
                it.Cfg = ic;
                it.Icon = IconCache.Get(ic.Path);

                int span = ic.Span < 1 ? 1 : (ic.Span > 3 ? 3 : ic.Span);
                int c, r;
                if (ic.X < 0 || ic.Y < 0) { if (!FindSlot(used, cols, span, out c, out r)) { c = 0; r = 0; } }
                else
                {
                    c = (int)Math.Round((ic.X - 6) / cell);
                    r = (int)Math.Round((ic.Y - 4) / cell);
                    if (c < 0) c = 0;
                    if (r < 0) r = 0;
                    if (!CanPlace(used, cols, span, c, r)) { if (!FindSlot(used, cols, span, out c, out r)) { c = 0; r = 0; } }
                }
                Mark(used, span, c, r);

                FrameworkElement tile = BuildTagTile(it, tagIndex, cell * span - 10);
                Canvas.SetLeft(tile, 6 + c * cell);
                Canvas.SetTop(tile, 4 + r * cell);
                _tagCanvas.Children.Add(tile);
                if (4 + r * cell + span * cell > maxY) maxY = 4 + r * cell + span * cell;
            }

            if (!inFolder)
            {
                for (int fi = 0; fi < tg.Folders.Count; fi++)
                {
                    int c, r;
                    if (!FindSlot(used, cols, 1, out c, out r)) break;
                    Mark(used, 1, c, r);
                    FrameworkElement tile = BuildTagFolderTile(tg, fi, tagIndex, cell - 10);
                    Canvas.SetLeft(tile, 6 + c * cell);
                    Canvas.SetTop(tile, 4 + r * cell);
                    _tagCanvas.Children.Add(tile);
                    if (4 + r * cell + cell > maxY) maxY = 4 + r * cell + cell;
                }
            }

            _tagCanvas.Height = Math.Max(300, maxY + 20);
        }

        private FrameworkElement BuildBackTile(int tagIndex)
        {
            Border b = new Border();
            b.Width = 68;
            b.Height = 68;
            b.CornerRadius = new CornerRadius(14);
            b.Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
            b.Cursor = Cursors.Hand;
            TextBlock tb = new TextBlock();
            tb.Text = "\u2039";
            tb.FontSize = 26;
            tb.Foreground = Brushes.White;
            tb.HorizontalAlignment = HorizontalAlignment.Center;
            tb.VerticalAlignment = VerticalAlignment.Center;
            b.Child = tb;
            b.ToolTip = "返回";
            int ti = tagIndex;
            b.MouseLeftButtonUp += delegate(object o, MouseButtonEventArgs e)
            {
                e.Handled = true;
                _tagFolder = -1;
                RebuildTagTiles(ti);
            };
            return b;
        }

        private FrameworkElement BuildTagFolderTile(TagDef tg, int folderIndex, int tagIndex, double w)
        {
            TagFolder tf = tg.Folders[folderIndex];
            Border b = new Border();
            b.Width = 68;
            b.Height = 68;
            b.CornerRadius = new CornerRadius(14);
            b.Background = new SolidColorBrush(Color.FromArgb(52, 255, 255, 255));
            b.BorderBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
            b.BorderThickness = new Thickness(1);
            b.Cursor = Cursors.Hand;
            b.ToolTip = tf.Name + "（" + tf.Items.Count + "）";

            Grid g = new Grid();
            WrapPanel wp = new WrapPanel();
            wp.Width = 52;
            wp.Height = 52;
            wp.HorizontalAlignment = HorizontalAlignment.Center;
            wp.VerticalAlignment = VerticalAlignment.Center;
            for (int k = 0; k < 4; k++)
            {
                Image im = new Image();
                im.Width = 24;
                im.Height = 24;
                if (k < tf.Items.Count) im.Source = IconCache.Get(tf.Items[k].Path);
                RenderOptions.SetBitmapScalingMode(im, BitmapScalingMode.HighQuality);
                wp.Children.Add(im);
            }
            g.Children.Add(wp);
            b.Child = g;

            int fi = folderIndex;
            int ti2 = tagIndex;
            b.MouseLeftButtonUp += delegate(object o, MouseButtonEventArgs e)
            {
                e.Handled = true;
                _tagFolder = fi;
                RebuildTagTiles(ti2);
            };
            b.MouseRightButtonUp += delegate(object o, MouseButtonEventArgs e)
            {
                e.Handled = true;
                ContextMenu cm = new ContextMenu();
                cm.Background = new SolidColorBrush(Color.FromArgb(250, 20, 20, 26));
                cm.Foreground = Brushes.White;
                MenuItem dis = new MenuItem();
                dis.Header = "解散文件夹";
                dis.Click += delegate
                {
                    TagFolder tfd = _cfg.Tags[ti2].Folders[fi];
                    foreach (ItemCfg x2 in tfd.Items) _cfg.Tags[ti2].Items.Add(x2);
                    _cfg.Tags[ti2].Folders.RemoveAt(fi);
                    ConfigStore.Save(_cfg);
                    _tagFolder = -1;
                    RebuildTagTiles(ti2);
                };
                cm.Items.Add(dis);
                cm.PlacementTarget = b;
                cm.IsOpen = true;
            };
            return b;
        }
        private FrameworkElement BuildTagTile(Item it, int tagIndex, double w)
        {
            StackPanel sp = new StackPanel();
            sp.Width = w;
            sp.Background = Brushes.Transparent;

            Image icon = new Image();
            icon.Source = it.Icon;
            double sz = Math.Min(this.CellW - 14, w - 12) * (it.Span >= 2 ? 1.0 : 1.0);
            icon.Width = sz;
            icon.Height = sz;
            icon.Stretch = Stretch.Uniform;
            RenderOptions.SetBitmapScalingMode(icon, BitmapScalingMode.HighQuality);
            icon.HorizontalAlignment = HorizontalAlignment.Center;
            sp.Children.Add(icon);

            TextBlock tb = new TextBlock();
            tb.Text = it.Name;
            tb.FontFamily = new FontFamily("Candara, Microsoft YaHei UI");
            tb.FontSize = 10;
            tb.Foreground = new SolidColorBrush(Color.FromArgb(235, 255, 255, 255));
            tb.TextTrimming = TextTrimming.CharacterEllipsis;
            tb.TextAlignment = TextAlignment.Center;
            tb.Margin = new Thickness(2, 4, 2, 0);
            sp.Children.Add(tb);

            sp.ToolTip = it.Path;
            sp.Tag = it;

            Item captured = it;
            int tindex = tagIndex;

            sp.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                Point pos = e.GetPosition(this);
                if (e.ClickCount == 2)
                {
                    e.Handled = true;
                    Launch(captured.Path);
                    return;
                }
                _tagDragItem = captured;
                _tagDragTile = sp;
                _tagDragFromX = Canvas.GetLeft(sp);
                _tagDragFromY = Canvas.GetTop(sp);
                _tagDragOffset = e.GetPosition(sp);
                _tagDragStartX = e.GetPosition(this).X;
                _tagDragStartY = e.GetPosition(this).Y;
                _tagDragging = false;
                sp.CaptureMouse();
                e.Handled = true;
            };
            sp.MouseMove += delegate(object s, MouseEventArgs e)
            {
                if (_tagDragTile != sp || e.LeftButton != MouseButtonState.Pressed) return;
                Point p = e.GetPosition(this);
                if (!_tagDragging && (Math.Abs(p.X - _tagDragStartX) > 6 || Math.Abs(p.Y - _tagDragStartY) > 6))
                {
                    _tagDragging = true;
                    sp.Opacity = 0.5;
                }
            };
            sp.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
            {
                sp.ReleaseMouseCapture();
                sp.Opacity = 1;
                if (_tagDragging && _tagCanvas != null)
                {
                    _tagDragging = false;
                    Point p = e.GetPosition(_tagCanvas);
                    double cell = 78;
                    int c = (int)Math.Round((p.X - _tagDragOffset.X - 6) / cell);
                    int r = (int)Math.Round((p.Y - _tagDragOffset.Y - 4) / cell);
                    if (c < 0) c = 0;
                    if (r < 0) r = 0;
                    int span = captured.Span < 1 ? 1 : (captured.Span > 3 ? 3 : captured.Span);
                    Canvas.SetLeft(sp, 6 + c * cell);
                    Canvas.SetTop(sp, 4 + r * cell);
                    if (captured.Cfg != null)
                    {
                        captured.Cfg.X = 6 + c * cell;
                        captured.Cfg.Y = 4 + r * cell;
                    }
                    ConfigStore.Save(_cfg);
                }
                _tagDragItem = null;
                _tagDragTile = null;
                e.Handled = true;
            };

            sp.MouseRightButtonUp += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                ContextMenu cm = new ContextMenu();
                cm.Background = new SolidColorBrush(Color.FromArgb(250, 20, 20, 26));
                cm.Foreground = Brushes.White;

                for (int si = 1; si <= 3; si++)
                {
                    int sv = si;
                    MenuItem mi = new MenuItem();
                    mi.Header = "占位 " + si + " × " + si;
                    mi.IsChecked = (captured.Span < 1 ? 1 : Math.Min(3, captured.Span)) == si;
                    mi.Click += delegate
                    {
                        if (captured.Cfg != null)
                        {
                            captured.Cfg.Span = sv;
                            captured.Cfg.X = -1;
                            captured.Cfg.Y = -1;
                        }
                        ConfigStore.Save(_cfg);
                        RebuildTagTiles(tindex);
                    };
                    cm.Items.Add(mi);
                }

                MenuItem moveTo = new MenuItem();
                moveTo.Header = "移入文件夹";
                TagDef tgNow = _cfg.Tags[tagIndex];
                for (int fi2 = 0; fi2 < tgNow.Folders.Count; fi2++)
                {
                    int fIdx = fi2;
                    MenuItem fmi = new MenuItem();
                    fmi.Header = tgNow.Folders[fi2].Name;
                    fmi.Click += delegate
                    {
                        if (captured.Cfg == null) return;
                        _cfg.Tags[tagIndex].Items.Remove(captured.Cfg);
                        _cfg.Tags[tagIndex].Folders[fIdx].Items.Add(captured.Cfg);
                        ConfigStore.Save(_cfg);
                        RebuildTagTiles(tagIndex);
                    };
                    moveTo.Items.Add(fmi);
                }
                if (moveTo.Items.Count > 0) cm.Items.Add(moveTo);
                MenuItem rm = new MenuItem();
                rm.Header = "从标签移除";
                rm.Click += delegate
                {
                    if (tindex >= 0 && tindex < _cfg.Tags.Count) _cfg.Tags[tindex].Items.Remove(captured.Cfg);
                    ConfigStore.Save(_cfg);
                    RebuildTagTiles(tindex);
                };
                cm.Items.Add(new Separator());
                cm.Items.Add(rm);

                cm.PlacementTarget = sp;
                cm.IsOpen = true;
            };

            return sp;
        }
        private Grid BuildEnvelopeHandle()
        {
            Grid g = new Grid();
            g.Width = 46;
            g.Height = 368;
            g.Cursor = Cursors.Hand;
            g.ToolTip = "收起侧边栏";

            Border body = new Border();
            body.CornerRadius = new CornerRadius(23);
            LinearGradientBrush bg = new LinearGradientBrush();
            bg.StartPoint = new Point(0, 0);
            bg.EndPoint = new Point(1, 0);
            bg.GradientStops.Add(new GradientStop(Color.FromArgb(72, 255, 255, 255), 0));
            bg.GradientStops.Add(new GradientStop(Color.FromArgb(132, 255, 255, 255), 0.55));
            bg.GradientStops.Add(new GradientStop(Color.FromArgb(84, 255, 255, 255), 1));
            body.Background = bg;
            body.BorderThickness = new Thickness(1);
            body.BorderBrush = new SolidColorBrush(Color.FromArgb(105, 255, 255, 255));
            g.Children.Add(body);

            TextBlock chev = new TextBlock();
            chev.Text = "\uE76B";
            chev.FontFamily = new FontFamily("Segoe MDL2 Assets");
            chev.FontSize = 15;
            chev.Foreground = new SolidColorBrush(Color.FromArgb(190, 255, 255, 255));
            chev.HorizontalAlignment = HorizontalAlignment.Center;
            chev.VerticalAlignment = VerticalAlignment.Center;
            chev.Margin = new Thickness(2, 0, 0, 0);
            chev.IsHitTestVisible = false;
            g.Children.Add(chev);

            g.MouseEnter += delegate
            {
                bg.GradientStops[0].Color = Color.FromArgb(60, 255, 255, 255);
                bg.GradientStops[1].Color = Color.FromArgb(110, 255, 255, 255);
                bg.GradientStops[2].Color = Color.FromArgb(64, 255, 255, 255);
            };
            g.MouseLeave += delegate
            {
                bg.GradientStops[0].Color = Color.FromArgb(80, 255, 255, 255);
                bg.GradientStops[1].Color = Color.FromArgb(150, 255, 255, 255);
                bg.GradientStops[2].Color = Color.FromArgb(90, 255, 255, 255);
            };
            g.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e) { e.Handled = true; CloseDrawer(); };
            return g;
        }
        private Border BuildNub()
        {
            Border b = new Border();
            b.Width = 4;
            b.Height = 96;
            b.CornerRadius = new CornerRadius(2);
            LinearGradientBrush g = new LinearGradientBrush();
            g.StartPoint = new Point(0, 0);
            g.EndPoint = new Point(0, 1);
            g.GradientStops.Add(Stop(Color.FromArgb(0, 255, 255, 255), 0));
            g.GradientStops.Add(Stop(Color.FromArgb(120, 255, 255, 255), 0.28));
            g.GradientStops.Add(Stop(Color.FromArgb(175, 255, 255, 255), 0.5));
            g.GradientStops.Add(Stop(Color.FromArgb(120, 255, 255, 255), 0.72));
            g.GradientStops.Add(Stop(Color.FromArgb(0, 255, 255, 255), 1));
            b.Background = g;
            DropShadowEffect sh = new DropShadowEffect();
            sh.Color = Colors.White;
            sh.BlurRadius = 14;
            sh.ShadowDepth = 0;
            sh.Opacity = 0.55;
            b.Effect = sh;
            b.ToolTip = "移到屏幕左边缘 / 点击打开   (Ctrl+Alt+D)";
            return b;
        }

        private Grid BuildHeader()
        {
            Grid head = new Grid();
            head.Margin = new Thickness(58 * _uiScale, 52 * _uiScale, 40, 14 * _uiScale);   // 避开左上角樱花与包边


            StackPanel left = new StackPanel();
            _clock = new TextBlock();
            _clock.FontFamily = _clockFont;
            _clock.FontSize = 46 * _uiScale;
            _clock.FontWeight = FontWeights.Bold;
            _clock.Foreground = new SolidColorBrush(Color.FromArgb(255, 16, 16, 16));
            _clock.Text = "--:--";
            
            TextShadow(_clock);
            TextShadow(_clock);
            left.Children.Add(_clock);

            _date = new TextBlock();
            _date.FontFamily = _ui;
            _date.FontSize = 12.5 * _uiScale;
            _date.FontWeight = FontWeights.Medium;
            _date.Margin = new Thickness(3, 0, 0, 0);
            _date.Foreground = new SolidColorBrush(Color.FromArgb(235, 56, 56, 56));
            TextShadow(_date);
            TextShadow(_date);
            left.Children.Add(_date);

            Border clockPlate = new Border();
            clockPlate.CornerRadius = new CornerRadius(20);
            clockPlate.Background = Brushes.Transparent;   // 只保留文字，不要胶囊
            clockPlate.BorderBrush = Brushes.Transparent;
            clockPlate.BorderBrush = Brushes.Transparent;
            clockPlate.BorderThickness = new Thickness(0);
            clockPlate.Padding = new Thickness(16, 10, 18, 11);
            clockPlate.HorizontalAlignment = HorizontalAlignment.Left;
            clockPlate.Child = left;
            head.Children.Add(clockPlate);
            ApplyClockColor();

            StackPanel right = new StackPanel();
            right.Orientation = Orientation.Horizontal;
            right.HorizontalAlignment = HorizontalAlignment.Right;
            right.VerticalAlignment = VerticalAlignment.Top;
            right.Margin = new Thickness(0, 12, 0, 0);


            _pinBtn = MakeRoundButton("\u25CF");
            _pinBtn.ToolTip = "固定（不自动收回）";
            _pinBtn.MouseLeftButtonUp += delegate
            {
                _cfg.Pinned = !_cfg.Pinned;
                UpdatePinVisual();
                ConfigStore.Save(_cfg);
            };
            right.Children.Add(_pinBtn);

            Border menuBtn = MakeRoundButton("\u22EF");
            menuBtn.ToolTip = "设置";
            menuBtn.Margin = new Thickness(8, 0, 0, 0);
            menuBtn.MouseLeftButtonUp += delegate { OpenMenuInPanel(); };
            right.Children.Add(menuBtn);

            head.Children.Add(right);
            UpdatePinVisual();
            return head;
        }

        private void UpdatePinVisual()
        {
            if (_pinBtn == null) return;
            _pinBtn.Background = new SolidColorBrush(_cfg.Pinned
                ? Color.FromArgb(72, 255, 255, 255)
                : Color.FromArgb(26, 255, 255, 255));
            TextBlock t = _pinBtn.Child as TextBlock;
            if (t != null) t.Opacity = _cfg.Pinned ? 1.0 : 0.5;
        }

        private Border MakeRoundButton(string glyph, bool mdl2 = false)
        {
            Border b = new Border();
            b.Width = 30;
            b.Height = 30;
            b.CornerRadius = new CornerRadius(15);
            b.Background = new SolidColorBrush(Color.FromArgb(26, 255, 255, 255));
            b.Cursor = Cursors.Hand;
            TextBlock t = new TextBlock();
            t.Text = glyph;
            t.FontSize = mdl2 ? 10 : 11;
            t.FontFamily = mdl2 ? new FontFamily("Segoe MDL2 Assets") : _ui;
            t.Foreground = new SolidColorBrush(Color.FromArgb(235, 255, 255, 255));
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.VerticalAlignment = VerticalAlignment.Center;
            b.Child = t;
            b.MouseEnter += delegate { b.Background = new SolidColorBrush(Color.FromArgb(56, 255, 255, 255)); };
            b.MouseLeave += delegate { UpdatePinVisual(); };
            return b;
        }

        private static void TextShadow(TextBlock t)
        {
            DropShadowEffect e = new DropShadowEffect();
            e.Color = Color.FromRgb(8, 8, 12);
            e.BlurRadius = 1.1;
            e.ShadowDepth = 0;
            e.Opacity = 1.0;
            t.Effect = e;
        }

        private int BlurRadius()
        {
            int[] radii = new int[] { 0, 2, 6, 12, 22 };
            int i = _cfg.BlurLevel;
            if (i < 0) i = 0;
            if (i > 4) i = 4;
            return radii[i];
        }

        /// <summary>遮罩：上下偏暗、中间保持原亮度，让胶囊颜色稳定不变。</summary>
        private Brush VeilBrush()
        {
            int baseA = VeilAlpha();          // 中间的基础压暗
            int edge = Math.Min(255, baseA + 96);   // 上下边缘更深
            LinearGradientBrush g = new LinearGradientBrush();
            g.StartPoint = new Point(0, 0);
            g.EndPoint = new Point(0, 1);
            g.GradientStops.Add(new GradientStop(ToArgb(edge, 8, 8, 14), 0));
            g.GradientStops.Add(new GradientStop(ToArgb(baseA + 30, 8, 8, 14), 0.22));
            g.GradientStops.Add(new GradientStop(ToArgb(baseA, 8, 8, 14), 0.5));
            g.GradientStops.Add(new GradientStop(ToArgb(baseA + 30, 8, 8, 14), 0.78));
            g.GradientStops.Add(new GradientStop(ToArgb(edge, 8, 8, 14), 1));
            return g;
        }

        /// <summary>固定背景：完全不采样屏幕内容，保证配色恒定。</summary>
        private static Brush FixedBackdrop()
        {
            LinearGradientBrush b = new LinearGradientBrush();
            b.StartPoint = new Point(0, 0);
            b.GradientStops.Add(new GradientStop(Color.FromArgb(18, 18, 18, 26), 0));
            b.GradientStops.Add(new GradientStop(Color.FromArgb(22, 24, 24, 34), 0.5));
            b.GradientStops.Add(new GradientStop(Color.FromArgb(16, 16, 16, 24), 1));
            b.Freeze();
            return b;
        }
        private int VeilAlpha()
        {
            int i = _cfg.BlurLevel;
            if (i < 0) i = 0;
            return 14;   // 固定最透档（透明度选项已移除）
        }

        private void ApplyVeil()
        {
            if (_veil != null) _veil.Fill = new SolidColorBrush(ToArgb(VeilAlpha(), 10, 10, 16));
            ApplyGlassStrength();
        }

        /// <summary>模糊档越低 → 涂层越淡，越接近纯透明（能直接看见动态壁纸）。</summary>
        /// <summary>四边折射：把背景按边缘放大/压缩重新采样，模拟厚玻璃弯曲光线。</summary>
        private void UpdateRefraction(ImageSource src)
        {
            if (_refract == null) return;
            _refract.Children.Clear();
            if (src == null) return;

            double w = _panelW;
            double h = _panelH;
            const double band = 28;   // 折射带宽度

            AddRefractStrip(src, 0, 0, w, band, new ScaleTransform(1, 1.45, 0.5, 0), 0.9);   // 上
            AddRefractStrip(src, 0, h - band, w, band, new ScaleTransform(1, 1.45, 0.5, 1), 0.9); // 下
            AddRefractStrip(src, 0, 0, band, h, new ScaleTransform(1.45, 1, 0, 0.5), 0.75);   // 左
            AddRefractStrip(src, w - band, 0, band, h, new ScaleTransform(1.45, 1, 1, 0.5), 0.75); // 右

            // 内侧亮线（光在校正折射时的亮边）
            Border caustic = new Border();
            caustic.CornerRadius = new CornerRadius(Radius - 4);
            caustic.BorderThickness = new Thickness(1);
            caustic.Margin = new Thickness(band - 3);
            caustic.BorderBrush = new SolidColorBrush(ToArgb(30, 255, 255, 255));
            caustic.IsHitTestVisible = false;
            _refract.Children.Add(caustic);
        }

        private void AddRefractStrip(ImageSource src, double x, double y, double w, double h, Transform rt, double opacity)
        {
            Rectangle r = new Rectangle();
            ImageBrush ib = new ImageBrush(src);
            ib.Stretch = Stretch.Fill;
            ib.RelativeTransform = rt;
            r.Fill = ib;
            r.Width = w;
            r.Height = h;
            r.Opacity = opacity;
            r.IsHitTestVisible = false;
            RenderOptions.SetBitmapScalingMode(r, BitmapScalingMode.Linear);
            Canvas.SetLeft(r, x);
            Canvas.SetTop(r, y);

            BlurEffect be = new BlurEffect();
            be.Radius = 1.4;
            r.Effect = be;

            _refract.Children.Add(r);
            if (_refract is Grid) { /* 用绝对定位 */ }
            r.HorizontalAlignment = HorizontalAlignment.Left;
            r.VerticalAlignment = VerticalAlignment.Top;
            r.Margin = new Thickness(x, y, 0, 0);
        }
        private void ApplyGlassStrength()
        {
            int i = _cfg.BlurLevel;
            if (i < 0) i = 0;
            if (i > 4) i = 4;
            double k = i == 0 ? 0.18 : 0.35 + i * 0.16;
            if (_tintRect != null) _tintRect.Opacity = k;
            if (_glowRect != null) _glowRect.Opacity = k;
            if (_noiseRect != null) _noiseRect.Opacity = i == 0 ? 0.006 : 0.012 + i * 0.004;
        }

        private static Color ToArgb(int a, int r, int g, int b)
        {
            return Color.FromArgb((byte)Math.Max(0, Math.Min(255, a)), (byte)r, (byte)g, (byte)b);
        }

        private static GradientStop Stop(Color c, double off)
        {
            return new GradientStop(c, off);
        }

        private static Brush RimBrush()
        {
            LinearGradientBrush b = new LinearGradientBrush();
            b.StartPoint = new Point(0, 0);
            b.EndPoint = new Point(1, 0.6);
            b.GradientStops.Add(Stop(Color.FromArgb(150, 255, 255, 255), 0));
            b.GradientStops.Add(Stop(Color.FromArgb(70, 255, 255, 255), 0.3));
            b.GradientStops.Add(Stop(Color.FromArgb(30, 255, 255, 255), 0.65));
            b.GradientStops.Add(Stop(Color.FromArgb(95, 255, 255, 255), 1));
            return b;
        }

        private static Geometry RoundedGeometry(double w, double h, double tl, double tr, double br, double bl)
        {
            StreamGeometry g = new StreamGeometry();
            using (StreamGeometryContext c = g.Open())
            {
                c.BeginFigure(new Point(tl, 0), true, true);
                c.LineTo(new Point(w - tr, 0), true, false);
                c.ArcTo(new Point(w, tr), new Size(tr, tr), 0, false, SweepDirection.Clockwise, true, false);
                c.LineTo(new Point(w, h - br), true, false);
                c.ArcTo(new Point(w - br, h), new Size(br, br), 0, false, SweepDirection.Clockwise, true, false);
                c.LineTo(new Point(bl, h), true, false);
                c.ArcTo(new Point(0, h - bl), new Size(bl, bl), 0, false, SweepDirection.Clockwise, true, false);
                c.LineTo(new Point(0, tl), true, false);
                c.ArcTo(new Point(tl, 0), new Size(tl, tl), 0, false, SweepDirection.Clockwise, true, false);
            }
            g.Freeze();
            return g;
        }

        private static Brush RoundedMask(double w, double h, double r)
        {
            DrawingBrush db = new DrawingBrush();
            db.Drawing = new GeometryDrawing(Brushes.White, null, RoundedGeometry(w, h, r, r, r, r));
            db.Viewbox = new Rect(0, 0, w, h);
            db.ViewboxUnits = BrushMappingMode.Absolute;
            db.Viewport = new Rect(0, 0, w, h);
            db.ViewportUnits = BrushMappingMode.Absolute;
            db.Stretch = Stretch.Fill;
            return db;
        }

        private static ImageSource MakeNoise(int w, int h)
        {
            Random rnd = new Random(7);
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

        // ------------------------------------------------------------ widgets
        private FrameworkElement BuildWidgetStrip()
        {
            Border card = new Border();
            card.CornerRadius = new CornerRadius(20);
            card.Background = new SolidColorBrush(Color.FromArgb(112, 255, 255, 255));
            card.BorderBrush = new SolidColorBrush(Color.FromArgb(140, 255, 255, 255));
            card.BorderThickness = new Thickness(1);
            card.Padding = new Thickness(22 * _uiScale, 16 * _uiScale, 22 * _uiScale, 16 * _uiScale);
            card.Margin = new Thickness(22 * _uiScale, 4 * _uiScale, 22 * _uiScale, 4 * _uiScale);
            card.Width = Math.Max(360, Math.Min(900, 560 * _uiScale));
            card.HorizontalAlignment = HorizontalAlignment.Left;

            Grid g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            g.Children.Add(WidgetColumn("CPU", out _cpuPct, out _cpuSub, out _cpuBar, 0));
            g.Children.Add(WidgetColumn("GPU", out _gpuPct, out _gpuSub, out _gpuBar, 1));
            g.Children.Add(WidgetColumn("内存", out _memPct, out _memSub, out _memBar, 2));

            card.Child = g;
            UpdateWidgets();
            return card;
        }

        private FrameworkElement WidgetColumn(string title, out TextBlock big, out TextBlock sub, out Grid bar, int column)
        {
            StackPanel sp = new StackPanel();
            Grid.SetColumn(sp, column);

            TextBlock t = new TextBlock();
            t.Text = title;
            t.FontFamily = new FontFamily("Candara, Microsoft YaHei UI");
            t.Foreground = new SolidColorBrush(Color.FromArgb(240, 38, 38, 38));
            TextShadow(t);
            sp.Children.Add(t);

            big = new TextBlock();
            big.FontFamily = _clockFont;
            big.FontSize = 27 * _uiScale;
            big.Foreground = new SolidColorBrush(Color.FromArgb(255, 12, 12, 12));
            big.Margin = new Thickness(0, 1 * _uiScale, 0, 6 * _uiScale);
            
            sp.Children.Add(big);

            bar = new Grid();
            bar.Height = 5;
            Border track = new Border();
            track.Background = new SolidColorBrush(Color.FromArgb(46, 60, 60, 80));
            track.Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
            bar.Children.Add(track);

            Grid fillGrid = new Grid();
            fillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.5, GridUnitType.Star) });
            fillGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.5, GridUnitType.Star) });
            Border fill = new Border();
            fill.Background = new SolidColorBrush(Color.FromArgb(215, 90, 110, 170));
            fill.Background = new SolidColorBrush(Color.FromArgb(190, 255, 255, 255));
            Grid.SetColumn(fill, 0);
            fillGrid.Children.Add(fill);
            bar.Children.Add(fillGrid);
            bar.Tag = fillGrid;
            sp.Children.Add(bar);

            sub = new TextBlock();
            sub.FontFamily = new FontFamily("Candara, Microsoft YaHei UI");
            sub.FontSize = 10.5 * _uiScale;
            sub.Foreground = new SolidColorBrush(Color.FromArgb(225, 52, 52, 52));
            TextShadow(sub);
            sp.Children.Add(sub);
            return sp;
        }

        private static void SetBar(Grid bar, double ratio)
        {
            if (bar == null) return;
            ratio = Math.Max(0.02, Math.Min(1.0, ratio));
            Grid fillGrid = bar.Tag as Grid;
            if (fillGrid == null) return;
            fillGrid.ColumnDefinitions[0].Width = new GridLength(ratio, GridUnitType.Star);
            fillGrid.ColumnDefinitions[1].Width = new GridLength(1 - ratio, GridUnitType.Star);
        }

        private void UpdateWidgets()
        {
            try
            {
                Native.MEMORYSTATUSEX ms = new Native.MEMORYSTATUSEX();
                ms.dwLength = (uint)Marshal.SizeOf(typeof(Native.MEMORYSTATUSEX));
                if (Native.GlobalMemoryStatusEx(ref ms) && ms.ullTotalPhys > 0)
                {
                    double total = ms.ullTotalPhys / 1073741824.0;
                    double used = (ms.ullTotalPhys - ms.ullAvailPhys) / 1073741824.0;
                    _memPct.Text = ms.dwMemoryLoad + "%";
                    _memSub.Text = string.Format("已用 {0:0.0} / {1:0.0} GB", used, total);
                    SetBar(_memBar, ms.dwMemoryLoad / 100.0);
                }
            }
            catch { }

            try
            {
                ulong idle, kernel, user;
                if (Native.GetSystemTimes(out idle, out kernel, out user))
                {
                    ulong di = idle - _prevIdle, dk = kernel - _prevKernel, du = user - _prevUser;
                    ulong total2 = dk + du;
                    double pct = total2 > 0 ? (1.0 - (double)di / total2) : 0;
                    if (pct < 0) pct = 0;
                    if (pct > 1) pct = 1;
                    _prevIdle = idle; _prevKernel = kernel; _prevUser = user;
                    if (_cpuPct != null)
                    {
                        _cpuPct.Text = (pct * 100).ToString("0") + "%";
                        _cpuSub.Text = CpuName();
                        SetBar(_cpuBar, pct);
                    }
                }
            }
            catch { }

            try
            {
                if (_gpuPct != null)
                {
                if (_gpuPct != null)
                {
                    double[] gpus = GpuUsages();
                    double g0 = gpus[0];
                    double g1 = gpus[1];
                    if (g1 >= 0)
                    {
                        _gpuPct.Text = g0.ToString("0") + "% / " + g1.ToString("0") + "%";
                        _gpuSub.Text = "双显卡 1 / 2";
                        SetBar(_gpuBar, g0 / 100.0);
                    }
                    else
                    {
                        _gpuPct.Text = g0.ToString("0") + "%";
                        _gpuSub.Text = "显卡占用";
                        SetBar(_gpuBar, g0 / 100.0);
                    }
                }
                }
            }
            catch { }
        }

        private static string _cpuName;
        private static string CpuName()
        {
            if (_cpuName != null) return _cpuName;
            try
            {
                // 优先读注册表里的真实型号（如 Intel(R) Core(TM) Ultra 7 265K）
                string n = null;
                try
                {
                    using (Microsoft.Win32.RegistryKey rk = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                        @"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
                    {
                        if (rk != null) n = rk.GetValue("ProcessorNameString") as string;
                    }
                }
                catch { }
                if (string.IsNullOrEmpty(n))
                    n = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER");
                if (string.IsNullOrEmpty(n)) n = "CPU";
                n = n.Trim();
                n = n.Replace("(R)", "").Replace("(TM)", "").Replace("  ", " ");   // 去掉商标标记，省地方
                _cpuName = n.Length > 34 ? n.Substring(0, 34) : n;
            }
            catch { _cpuName = "CPU"; }
            return _cpuName;
        }

        private static System.Diagnostics.PerformanceCounter[] _gpuCounters;
        private static string[] _gpuKeys;
        private static DateTime _gpuStamp = DateTime.MinValue;
        private static double[] _gpuLast = new double[] { -1, -1 };

        /// <summary>按适配器（LUID）分组统计 GPU 占用，最多返回 2 个。</summary>
        private static double[] GpuUsages()
        {
            try
            {
                if (_gpuCounters == null || (DateTime.Now - _gpuStamp).TotalSeconds > 30)
                {
                    _gpuStamp = DateTime.Now;
                    List<System.Diagnostics.PerformanceCounter> list = new List<System.Diagnostics.PerformanceCounter>();
                    List<string> keys = new List<string>();
                    System.Diagnostics.PerformanceCounterCategory cat = new System.Diagnostics.PerformanceCounterCategory("GPU Engine");
                    foreach (string inst in cat.GetInstanceNames())
                    {
                        if (inst.IndexOf("engtype_3D", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        list.Add(new System.Diagnostics.PerformanceCounter("GPU Engine", "Utilization Percentage", inst, true));
                        keys.Add(AdapterKey(inst));
                    }
                    _gpuCounters = list.ToArray();
                    _gpuKeys = keys.ToArray();
                }

                Dictionary<string, double> sums = new Dictionary<string, double>();
                for (int i = 0; i < _gpuCounters.Length; i++)
                {
                    double v = 0;
                    try { v = _gpuCounters[i].NextValue(); } catch { continue; }
                    string k = _gpuKeys[i];
                    if (!sums.ContainsKey(k)) sums[k] = 0;
                    sums[k] += v;
                }

                List<double> vals = new List<double>();
                foreach (KeyValuePair<string, double> kv in sums)
                {
                    double v = kv.Value;
                    if (v < 0) v = 0;
                    if (v > 100) v = 100;
                    vals.Add(v);
                }
                vals.Sort();
                vals.Reverse();

                double[] result = new double[] { vals.Count > 0 ? vals[0] : 0, vals.Count > 1 ? vals[1] : -1 };
                _gpuLast = result;
                return result;
            }
            catch { return _gpuLast; }
        }

        /// <summary>从 GPU Engine 实例名里取适配器标识（luid_..._phys_N）。</summary>
        private static string AdapterKey(string inst)
        {
            int i = inst.IndexOf("luid_", StringComparison.OrdinalIgnoreCase);
            if (i < 0) return "0";
            int j = inst.IndexOf("eng_", i, StringComparison.OrdinalIgnoreCase);
            return j > i ? inst.Substring(i, j - i) : inst.Substring(i);
        }
        // ------------------------------------------------------------ tiles
        private List<Item> BuildHomeItems()
        {
            List<Item> list = new List<Item>();
            // 已归入标签（含标签内文件夹）的图标不再出现在首页
            List<string> tagged = new List<string>();
            foreach (TagDef tgx in _cfg.Tags)
            {
                foreach (ItemCfg tx in tgx.Items) tagged.Add(tx.Path);
                foreach (TagFolder tfx in tgx.Folders)
                    foreach (ItemCfg tx2 in tfx.Items) tagged.Add(tx2.Path);
            }

            // 去重：文件夹里的图标不在首页重复出现；同一路径只显示一次
            List<string> _seen = new List<string>();
            foreach (FolderDef _fdx in _cfg.Folders)
                foreach (ItemCfg _fxi in _fdx.Items)
                    if (_fxi.Path != null) _seen.Add(_fxi.Path);

            foreach (ItemCfg p in _cfg.Pins)
            {
                if (p.Path == null) continue;
                bool _dup = false;
                foreach (string _s in _seen) if (string.Equals(_s, p.Path, StringComparison.OrdinalIgnoreCase)) { _dup = true; break; }
                if (_dup) continue;
                _seen.Add(p.Path);

                Item it = new Item();
                it.Name = IOPath.GetFileNameWithoutExtension(p.Path);
                it.Path = p.Path;
                it.Size = p.Size;
                it.X = p.X;
                it.Y = p.Y;
                it.Span = p.Span;
                it.Cfg = p;
                it.Icon = IconCache.Get(p.Path);
                list.Add(it);
            }
            for (int fi = 0; fi < _cfg.Folders.Count; fi++)
            {
                FolderDef f = _cfg.Folders[fi];
                Item dir = new Item();
                dir.IsFolder = true;
                dir.Name = f.Name;
                dir.Path = "";
                dir.FolderIndex = fi;
                dir.Size = f.Size;
                dir.X = f.X;
                dir.Y = f.Y;
                dir.Span = f.Span;
                foreach (ItemCfg p in f.Items)
                {
                    Item c = new Item();
                    c.Name = IOPath.GetFileNameWithoutExtension(p.Path);
                    c.Path = p.Path;
                    c.Size = p.Size;
                    c.Cfg = p;
                    c.Icon = IconCache.Get(p.Path);
                    dir.Children.Add(c);
                }
                list.Add(dir);
            }
            return list;
        }

        private const double PadX = 20;
        private const double PadY = 14;

        private double CellW { get { return Math.Max(78, _cfg.IconSize + 26); } }
        private double CellH { get { return CellW + 26; } }

        /// <summary>修复：标签里引用的图标如果首页/文件夹都没有，补回首页，避免“图标丢失”。</summary>
        /// <summary>启动时把桌面上（含公共桌面）尚未收录的快捷方式补进首页。</summary>
        private void ImportDesktopShortcuts()
        {
            try
            {
                List<string> dirs = new List<string>();
                string u = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                if (!string.IsNullOrEmpty(u)) dirs.Add(u);
                string pub = Environment.GetEnvironmentVariable("PUBLIC");
                if (!string.IsNullOrEmpty(pub)) dirs.Add(System.IO.Path.Combine(pub, "Desktop"));


                List<string> known = new List<string>();
                HashSet<string> knownNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (GroupDef g0 in _cfg.Groups) foreach (string pth in g0.Items) { known.Add(pth); knownNames.Add(NormName(pth)); }

                int added = 0;
                foreach (string d in dirs)
                {
                    if (!Directory.Exists(d)) continue;
                    List<string> files = new List<string>();
                    try { files.AddRange(Directory.GetFiles(d, "*.*", SearchOption.AllDirectories)); } catch { }
                    foreach (string f in files)
                    {
                        string e = IOPath.GetExtension(f).ToLowerInvariant();
                        if (e != ".lnk" && e != ".url" && e != ".appref-ms") continue;   // 只要快捷方式
                        if (IOPath.GetFileName(f).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                        if (knownNames.Contains(NormName(f))) continue;   // 同名只收一份
                        bool dupPath = false;
                        foreach (string kk in known) if (string.Equals(kk, f, StringComparison.OrdinalIgnoreCase)) { dupPath = true; break; }
                        if (dupPath) continue;
                        string name = IOPath.GetFileNameWithoutExtension(f);
                        GroupDef target = PickGroupFor(name);
                        if (target == null) continue;
                        target.Items.Add(f);
                        known.Add(f); knownNames.Add(NormName(f));
                        added++;
                    }
                }
                if (added > 0) { ConfigStore.Save(_cfg); Program.Log("import added=" + added); }
                else Program.Log("import nothing new");
            }
            catch (Exception ex) { Program.Log("import failed: " + ex.Message); }
        }

        /// <summary>按名字猜分类：启动器 → Launchers，常用软件 → Daily，其余 → Tools。</summary>
        /// <summary>快捷方式去重用的规范化名字（忽略大小写、空格、全角冒号、标点）。</summary>
        private static string NormName(string path)
        {
            string n = IOPath.GetFileNameWithoutExtension(path ?? "").ToLowerInvariant();
            n = n.Replace("：", ":").Replace(" ", "").Replace("　", "");
            StringBuilder sb = new StringBuilder();
            foreach (char c in n)
                if (char.IsLetterOrDigit(c) || c > 0x2E80) sb.Append(c);
            return sb.ToString();
        }

        private GroupDef PickGroupFor(string name)
        {
            string n = (name ?? "").ToLowerInvariant();
            string want = "tools";

            string[] launchKeys = new string[] { "steam", "epic", "dmm", "dlsite", "taptap", "mihoyo", "launcher",
                "启动器", "battle.net", "gog", "ubisoft", "uplay", "origin", "ea app", "wegame", "加速器", "galgame" };
            string[] dailyKeys = new string[] { "wechat", "微信", "qq", "tencent meeting", "腾讯会议", "edge", "chrome",
                "firefox", "wallpaper", "网易云", "spotify", "音乐", "视频", "bilibili", "哔哩", "telegram", "discord" };

            foreach (string k in launchKeys) if (n.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) { want = "launchers"; break; }
            if (want == "tools")
                foreach (string k in dailyKeys) if (n.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) { want = "daily"; break; }

            foreach (GroupDef g in _cfg.Groups)
                if (string.Equals(g.Name, want, StringComparison.OrdinalIgnoreCase)) return g;
            foreach (GroupDef g in _cfg.Groups)
                if (string.Equals(g.Name, "tools", StringComparison.OrdinalIgnoreCase)) return g;
            if (_cfg.Groups.Count > 0) return _cfg.Groups[_cfg.Groups.Count - 1];
            return null;
        }
        private void RepairMissingIcons()
        {
            bool changed = false;
            List<string> known = new List<string>();
            foreach (ItemCfg p in _cfg.Pins) known.Add(p.Path);
            foreach (FolderDef f in _cfg.Folders)
                foreach (ItemCfg fi in f.Items) known.Add(fi.Path);

            foreach (TagDef tg in _cfg.Tags)
            {
                List<ItemCfg> all = new List<ItemCfg>();
                foreach (ItemCfg i2 in tg.Items) all.Add(i2);
                foreach (TagFolder tf in tg.Folders)
                    foreach (ItemCfg i3 in tf.Items) all.Add(i3);

                foreach (ItemCfg i4 in all)
                {
                    if (string.IsNullOrEmpty(i4.Path)) continue;
                    if (!ConfigStore.Alive(i4.Path)) continue;
                    if (known.Contains(i4.Path)) continue;
                    ItemCfg np = new ItemCfg();
                    np.Path = i4.Path;
                    _cfg.Pins.Add(np);
                    known.Add(i4.Path);
                    changed = true;
                }
            }

            if (changed) ConfigStore.Save(_cfg);
        }
        /// <summary>按分类渲染：每类一行胶囊 + 名称 + 展开按钮。</summary>
        private void LOGCLIP()
        {
            Dispatcher.BeginInvoke(new Action(delegate
            {
                try
                {
                    FrameworkElement par = _homeCanvas.Parent as FrameworkElement;
                    Program.Log("CLIP canvas=" + _homeCanvas.ActualWidth.ToString("0") + " parent=" + (par == null ? "null" : par.GetType().Name + ":" + par.ActualWidth.ToString("0")) + " panelW=" + _panelW.ToString("0"));
                }
                catch (Exception ex) { Program.Log("CLIP err " + ex.Message); }
            }));
        }

        /// <summary>底部：系统三线 + 樱花枝装饰。</summary>
        /// <summary>频谱跑在独立 STA 线程 + 独立 Dispatcher，主线程忙也不影响它。</summary>
        private void StartSpectrumThread()
        {
            _specThread = new Thread(delegate()
            {
                try
                {
                    SpectrumWindow win = new SpectrumWindow(_panelW - 24);
                    System.Windows.Threading.Dispatcher disp = System.Windows.Threading.Dispatcher.CurrentDispatcher;
                    System.Windows.Threading.DispatcherTimer sync = new System.Windows.Threading.DispatcherTimer();
                    sync.Interval = TimeSpan.FromMilliseconds(80);
                    sync.Tick += delegate
                    {
                        try
                        {
                            win.Panel.SetPlaying(_specWantPlay);
                            if (_specWantShow)
                            {
                                Rect wa = SystemParameters.WorkArea;
                                win.ShowAt(wa.Left + 12, wa.Top + _specWantTop);
                            }
                            else win.HideAll();
                        }
                        catch { }
                    };
                    sync.Start();
                    System.Windows.Threading.Dispatcher.Run();
                }
                catch (Exception ex) { Program.Log("spec thread: " + ex.Message); }
            });
            _specThread.SetApartmentState(ApartmentState.STA);
            _specThread.IsBackground = true;
            _specThread.Start();
        }
        /// <summary>新建分类。</summary>
        /// <summary>手动把一个快捷方式 / 程序加到指定分类（右键菜单用）。</summary>
        private void AddIconToGroup(GroupDef g)
        {
            try
            {
                string start = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                AddIconToGroupCore(g, start);
            }
            catch (Exception ex) { Program.Log("add icon failed: " + ex.Message); }
        }

        private void AddIconToGroupCore(GroupDef g, string initialDir)
        {
            if (g == null) return;
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.Title = "选择要加入「" + g.Name + "」的程序或快捷方式";
            dlg.Filter = "快捷方式与程序|*.lnk;*.url;*.appref-ms;*.exe|所有文件|*.*";
            dlg.Multiselect = true;
            if (!string.IsNullOrEmpty(initialDir)) dlg.InitialDirectory = initialDir;
            bool? ok = dlg.ShowDialog();
            if (ok != true) return;

            int added = 0;
            foreach (string f in dlg.FileNames)
            {
                if (string.IsNullOrEmpty(f)) continue;
                string key = NormName(f);
                bool dup = false;
                foreach (GroupDef gg in _cfg.Groups)
                    foreach (string pth in gg.Items)
                        if (NormName(pth) == key || string.Equals(pth, f, StringComparison.OrdinalIgnoreCase)) { dup = true; break; }
                if (dup)
                {
                    Program.Log("add icon skip dup: " + f);
                    continue;
                }
                g.Items.Add(f);
                added++;
            }
            if (added > 0)
            {
                ConfigStore.Save(_cfg);
                RebuildTiles();
                Program.Log("add icon ok=" + added + " -> " + g.Name);
            }
        }
        private void AddGroup()
        {
            InputBox box = new InputBox("新建分类", "新分类");
            box.Owner = this;
            bool ok = box.ShowDialog() == true;
            string name = (box.Value ?? "").Trim();
            if (!ok || name.Length == 0) return;

            GroupDef g = new GroupDef();
            g.Name = name;
            g.Expanded = true;
            _cfg.Groups.Add(g);
            ConfigStore.Save(_cfg);
            RebuildTiles();
        }

        /// <summary>重命名分类。</summary>
        private void RenameGroup(GroupDef g)
        {
            InputBox box = new InputBox("重命名分类", g.Name);
            box.Owner = this;
            bool ok = box.ShowDialog() == true;
            string name = (box.Value ?? "").Trim();
            if (!ok || name.Length == 0) return;
            g.Name = name;
            ConfigStore.Save(_cfg);
            RebuildTiles();
        }

        /// <summary>删除分类（里面的图标会回到桌面扫描结果，不会丢文件）。</summary>
        private void DeleteGroup(GroupDef g)
        {
            MessageBoxResult r = MessageBox.Show(this,
                "删除分类「" + g.Name + "」？\n分类里的 " + g.Items.Count + " 个快捷方式会回到自动扫描的结果里（不会删除文件）。",
                "删除分类", MessageBoxButton.OKCancel, MessageBoxImage.Question);
            if (r != MessageBoxResult.OK) return;
            _cfg.Groups.Remove(g);
            ConfigStore.Save(_cfg);
            RebuildTiles();
        }
        private void RebuildGrouped()
        {
            _homeCanvas.Children.Clear();
            _groupHeads.Clear();
            double y = 14;
            double rowH = CellH;   // 行高（改回原来的紧凑高度）
            int cols = Math.Max(1, (int)((_panelW - 52 - 36) / CellW));   // 卡片内左右各留 18

            List<GroupDef> ordered = new List<GroupDef>(_cfg.Groups);
            if (_cfg.GroupSort == 1)
                ordered.Sort(delegate(GroupDef a, GroupDef b) { return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase); });
            foreach (GroupDef g in ordered)
            {
                int shownCnt = g.Expanded ? g.Items.Count : Math.Min(cols, g.Items.Count);
                int rowCnt = Math.Max(1, (int)Math.Ceiling(shownCnt / (double)cols));
                double cardH = 34 * _uiScale + rowCnt * rowH + 10 * _uiScale;
                Border cardBg = new Border();
                cardBg.Width = _panelW - 52;
                cardBg.Height = cardH;
                cardBg.CornerRadius = new CornerRadius(20);
                cardBg.Background = new SolidColorBrush(Color.FromArgb(118, 255, 255, 255));
                cardBg.BorderBrush = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255));
                cardBg.BorderThickness = new Thickness(1);
                cardBg.IsHitTestVisible = false;
                Canvas.SetLeft(cardBg, 26);
                Canvas.SetTop(cardBg, y - 6);
                _homeCanvas.Children.Add(cardBg);

                // 标题行：文字在左，箭头按钮贴右（用 Grid 自动对齐，避免绝对坐标被裁掉）
                Grid head = new Grid();
                head.Width = cardBg.Width - 32;
                head.Height = 26 * _uiScale;
                Canvas.SetLeft(head, 42);
                Canvas.SetTop(head, y + 3);
                _homeCanvas.Children.Add(head);

                Border headBg = new Border();
                headBg.CornerRadius = new CornerRadius(9);
                headBg.Margin = new Thickness(-6, -2, -6, -2);
                headBg.Background = Brushes.Transparent;
                head.Children.Add(headBg);
                HeadRef href = new HeadRef();
                href.G = g;
                href.Bg = headBg;
                href.Host = head;
                _groupHeads.Add(href);

                StackPanel nameRow = new StackPanel();
                nameRow.Orientation = Orientation.Horizontal;
                nameRow.VerticalAlignment = VerticalAlignment.Center;
                nameRow.HorizontalAlignment = HorizontalAlignment.Left;
                
                TextBlock title = new TextBlock();
                title.Text = g.Name.ToUpperInvariant();
                title.FontFamily = _clockFont;
                title.FontSize = 17 * _uiScale;
                title.FontWeight = FontWeights.Bold;
                title.Foreground = new SolidColorBrush(Color.FromArgb(255, 20, 20, 20));
                TextShadow(title);
                nameRow.Children.Add(title);

                TextBlock cnt = new TextBlock();
                cnt.Text = "  " + g.Items.Count;
                cnt.FontFamily = new FontFamily("Candara, Microsoft YaHei UI");
                cnt.FontSize = 11 * _uiScale;
                cnt.VerticalAlignment = VerticalAlignment.Center;
                cnt.Foreground = new SolidColorBrush(Color.FromArgb(235, 46, 46, 46));
                TextShadow(cnt);
                nameRow.Children.Add(cnt);
                head.Children.Add(nameRow);
                nameRow.Background = Brushes.Transparent;
                nameRow.Cursor = Cursors.Hand;
                GroupDef menuG = g;
                nameRow.MouseRightButtonUp += delegate(object s3, MouseButtonEventArgs e3)
                {
                    e3.Handled = true;
                    ShowGroupMenu(menuG);
                };

                Border tog = new Border();
                tog.Width = 40 * _uiScale;
                tog.Height = 22 * _uiScale;
                tog.CornerRadius = new CornerRadius(11 * _uiScale);
                tog.Background = new SolidColorBrush(Color.FromArgb(232, 58, 58, 72));
                tog.BorderBrush = new SolidColorBrush(Color.FromArgb(190, 255, 255, 255));
                tog.BorderThickness = new Thickness(1);
                tog.Cursor = Cursors.Hand;

                System.Windows.Shapes.Path chev = new System.Windows.Shapes.Path();
                chev.Data = System.Windows.Media.Geometry.Parse(g.Expanded ? "M0,5 L5,0 L10,5" : "M0,0 L5,5 L10,0");
                chev.Stroke = new SolidColorBrush(Color.FromArgb(240, 255, 255, 255));
                chev.StrokeThickness = 1.7;
                chev.StrokeStartLineCap = PenLineCap.Round;
                chev.StrokeEndLineCap = PenLineCap.Round;
                chev.HorizontalAlignment = HorizontalAlignment.Center;
                chev.VerticalAlignment = VerticalAlignment.Center;
                tog.Child = chev;

                GroupDef capturedG = g;
                tog.MouseLeftButtonUp += delegate(object s2, MouseButtonEventArgs e2)
                {
                    e2.Handled = true;
                    capturedG.Expanded = !capturedG.Expanded;
                    ConfigStore.Save(_cfg);
                    RebuildTiles();
                };
                Canvas.SetLeft(tog, 26 + (_panelW - 52) - 46);
                Canvas.SetTop(tog, y + 4);
                _homeCanvas.Children.Add(tog);

                y += 32;

                List<string> itemList = new List<string>(g.Items);
                if (_cfg.IconSort == 1)
                    itemList.Sort(delegate(string a, string b)
                    {
                        return string.Compare(IOPath.GetFileNameWithoutExtension(a), IOPath.GetFileNameWithoutExtension(b),
                            StringComparison.CurrentCultureIgnoreCase);
                    });

                int shown = Math.Min(g.Expanded ? itemList.Count : cols, itemList.Count);
                for (int i = 0; i < shown; i++)
                {
                    string path = itemList[i];
                    if (!ConfigStore.Alive(path)) continue;
                    Item it = new Item();
                    it.Name = IOPath.GetFileNameWithoutExtension(path);
                    it.Path = path;
                    it.Icon = IconCache.Get(path);
                    FrameworkElement tile = BuildTile(it);
                    int c = i % cols, r = i / cols;
                    Canvas.SetLeft(tile, 26 + 18 + c * CellW);
                    Canvas.SetTop(tile, y + r * rowH);
                    _homeCanvas.Children.Add(tile);
                }
                int rows = Math.Max(1, (int)Math.Ceiling(shown / (double)cols));
                y += cardH + 6;
            }





            _homeCanvas.Height = Math.Max(220, y + 20);   // 让 ScrollViewer 有可滚动范围

        }
        private void RebuildTiles()
        {
            // 首次运行导入桌面 + 开始菜单（分组模式也要跑）
            if (!_imported && _homeCanvas != null) { _imported = true; ImportDesktopShortcuts(); }
            if (_cfg.Groups.Count > 0) { RebuildGrouped(); return; }
            if (_homeCanvas == null) return;
            _homeCanvas.Children.Clear();

            List<Item> items = BuildHomeItems();
            int cols = Math.Max(1, (int)((_panelW - PadX * 2) / CellW));
            HashSet<string> used = new HashSet<string>();
            double maxY = 0;

            for (int i = 0; i < items.Count; i++)
            {
                Item it = items[i];
                int span = it.Span < 1 ? 1 : (it.Span > 3 ? 3 : it.Span);
                int c, r;

                if (it.X < 0 || it.Y < 0)
                {
                    if (!FindSlot(used, cols, span, out c, out r)) { c = 0; r = 0; }
                }
                else
                {
                    c = (int)Math.Round((it.X - PadX) / CellW);
                    r = (int)Math.Round((it.Y - PadY) / CellH);
                    if (c < 0) c = 0;
                    if (r < 0) r = 0;
                    if (!CanPlace(used, cols, span, c, r))
                    {
                        if (!FindSlot(used, cols, span, out c, out r)) { c = 0; r = 0; }
                    }
                }

                Mark(used, span, c, r);

                FrameworkElement tile = BuildTile(it);
                double x = PadX + c * CellW;
                double y = PadY + r * CellH;
                Canvas.SetLeft(tile, x);
                Canvas.SetTop(tile, y);
                _homeCanvas.Children.Add(tile);
                if (y + span * CellH > maxY) maxY = y + span * CellH;
            }

            _homeCanvas.Width = Math.Max(100, _panelW - 16);
            _homeCanvas.Height = Math.Max(220, maxY + 40);
        }

        private static bool CanPlace(HashSet<string> used, int cols, int span, int c, int r)
        {
            if (c + span > cols) return false;
            for (int dr = 0; dr < span; dr++)
                for (int dc = 0; dc < span; dc++)
                    if (used.Contains(r + dr + "." + (c + dc))) return false;
            return true;
        }

        private static void Mark(HashSet<string> used, int span, int c, int r)
        {
            for (int dr = 0; dr < span; dr++)
                for (int dc = 0; dc < span; dc++)
                    used.Add(r + dr + "." + (c + dc));
        }

        private static bool FindSlot(HashSet<string> used, int cols, int span, out int c, out int r)
        {
            c = 0; r = 0;
            for (int row = 0; row < 400; row++)
                for (int col = 0; col + span <= cols; col++)
                    if (CanPlace(used, cols, span, col, row)) { c = col; r = row; return true; }
            return false;
        }

        /// <summary>把拖动后的位置写回配置。</summary>
        /// <summary>把当前排布固化成显式坐标，移动单个图标时其它图标不再重排。</summary>
        private void FreezeLayout()
        {
            bool changed = false;
            foreach (UIElement ch in _homeCanvas.Children)
            {
                FrameworkElement fe = ch as FrameworkElement;
                if (fe == null) continue;
                Item it = fe.Tag as Item;
                if (it == null) continue;
                double cx = Canvas.GetLeft(fe);
                double cy = Canvas.GetTop(fe);
                if (double.IsNaN(cx) || double.IsNaN(cy)) continue;
                if (it.IsFolder)
                {
                    if (it.FolderIndex >= 0 && it.FolderIndex < _cfg.Folders.Count)
                    {
                        FolderDef fd = _cfg.Folders[it.FolderIndex];
                        if (fd.X < 0 || fd.Y < 0) { fd.X = cx; fd.Y = cy; changed = true; }
                    }
                }
                else if (it.Cfg != null)
                {
                    if (it.Cfg.X < 0 || it.Cfg.Y < 0) { it.Cfg.X = cx; it.Cfg.Y = cy; changed = true; }
                }
            }
            if (changed) ConfigStore.Save(_cfg);
        }

        private void SetItemPos(Item it, double x, double y)
        {
            if (it == null) return;
            it.X = x;
            it.Y = y;
            if (it.IsFolder)
            {
                if (it.FolderIndex >= 0 && it.FolderIndex < _cfg.Folders.Count)
                {
                    _cfg.Folders[it.FolderIndex].X = x;
                    _cfg.Folders[it.FolderIndex].Y = y;
                }
            }
            else if (it.Cfg != null)
            {
                it.Cfg.X = x;
                it.Cfg.Y = y;
            }
            ConfigStore.Save(_cfg);
        }

        private void SetItemSize(ItemCfg cfg, double size)
        {
            if (cfg == null) return;
            cfg.Size = size;
            ConfigStore.Save(_cfg);
            RebuildTiles();
            ApplyGlassStrength();
        }

        private void SetFolderSize(int index, double size)
        {
            if (index < 0 || index >= _cfg.Folders.Count) return;
            _cfg.Folders[index].Size = size;
            ConfigStore.Save(_cfg);
            RebuildTiles();
            ApplyGlassStrength();
        }

        private Item TileAt(Point p)
        {
            if (_homeCanvas == null) return null;
            foreach (UIElement ch in _homeCanvas.Children)
            {
                FrameworkElement fe = ch as FrameworkElement;
                if (fe == null) continue;
                try
                {
                    Point o = fe.TransformToAncestor(_homeCanvas).Transform(new Point(0, 0));
                    Rect r = new Rect(o, new Size(fe.ActualWidth, fe.ActualHeight));
                    if (r.Contains(p))
                    {
                        Item it = fe.Tag as Item;
                        if (it != null) return it;
                    }
                }
                catch { }
            }
            return null;
        }

        /// <summary>把图标拖进文件夹。</summary>
        private void MoveIntoFolder(Item item, Item folder)
        {
            if (item == null || folder == null || !folder.IsFolder || item.IsFolder) return;
            if (folder.FolderIndex < 0 || folder.FolderIndex >= _cfg.Folders.Count) return;
            if (item.Cfg == null) return;

            _cfg.Pins.Remove(item.Cfg);
            _cfg.Folders[folder.FolderIndex].Items.Add(item.Cfg);
            ConfigStore.Save(_cfg);
            RebuildTiles();
        }

        /// <summary>把首页图标拖进标签：从首页移除，加入标签。</summary>
        private void MoveToTag(Item item, int tagIndex)
        {
            if (item == null || item.Cfg == null) return;
            if (tagIndex < 0 || tagIndex >= _cfg.Tags.Count) return;

            string path = item.Cfg.Path;
            TagDef tg = _cfg.Tags[tagIndex];
            foreach (ItemCfg ex in tg.Items)
            {
                if (ex.Path == path) { RebuildTiles(); return; }
            }

            _cfg.Pins.Remove(item.Cfg);
            ItemCfg moved = new ItemCfg();
            moved.Path = item.Cfg.Path;
            moved.Span = item.Cfg.Span;
            tg.Items.Add(moved);

            ConfigStore.Save(_cfg);
            RebuildTiles();
            if (_tagOpen == tagIndex) ShowTagPanel(tagIndex);
            RefreshTagRail();
        }
        /// <summary>文件夹内上下移动。</summary>
        private void MoveFolderItem(string path, int delta)
        {
            if (_openFolderIdx < 0 || _openFolderIdx >= _cfg.Folders.Count) return;
            FolderDef fo = _cfg.Folders[_openFolderIdx];
            int idx = -1;
            for (int k = 0; k < fo.Items.Count; k++)
                if (string.Equals(fo.Items[k].Path, path, StringComparison.OrdinalIgnoreCase)) { idx = k; break; }
            if (idx < 0) return;
            int target = idx + delta;
            if (target < 0 || target >= fo.Items.Count) return;
            ItemCfg mv = fo.Items[idx];
            fo.Items.RemoveAt(idx);
            fo.Items.Insert(target, mv);
            ConfigStore.Save(_cfg);
            RebuildTiles();
            foreach (Item fx in BuildHomeItems())
                if (fx.IsFolder && fx.FolderIndex == _openFolderIdx) { OpenFolder(fx); break; }
        }
        private void SetItemSpan(Item it, int span)
        {
            if (it == null || it.Cfg == null) return;
            it.Cfg.Span = span;
            it.Cfg.X = -1;
            it.Cfg.Y = -1;
            ConfigStore.Save(_cfg);
            RebuildTiles();
        }

        private void SetFolderSpan(int index, int span)
        {
            if (index < 0 || index >= _cfg.Folders.Count) return;
            _cfg.Folders[index].Span = span;
            _cfg.Folders[index].X = -1;
            _cfg.Folders[index].Y = -1;
            ConfigStore.Save(_cfg);
            RebuildTiles();
        }
        private FrameworkElement BuildTile(Item it)
        {
            int span = it.Span < 1 ? 1 : (it.Span > 3 ? 3 : it.Span);
            // 图标尺寸跟随格子：1x1≈52、2x2≈130、3x3≈208，保证视觉占位与格子一致
            double sz = CellW * span - 26;
            double tw = CellW * span - 8;

            Border b = new Border();
            b.Width = tw;
            b.CornerRadius = new CornerRadius(16);
            b.Margin = new Thickness(1, 2, 1, 2);
            b.Padding = new Thickness(2, 6, 2, 4);
            b.Background = Brushes.Transparent;
            b.Cursor = Cursors.Hand;

            StackPanel sp = new StackPanel();
            sp.HorizontalAlignment = HorizontalAlignment.Center;

            if (it.IsFolder) sp.Children.Add(BuildFolderGlyph(it, sz));
            else
            {
                Image im = new Image();
                im.Source = it.Icon;
                im.Width = sz;
                im.Height = sz;
                im.Stretch = Stretch.Uniform;
                // 图标统一圆角（约 22%）
                im.Clip = new RectangleGeometry(new Rect(0, 0, sz, sz), sz * 0.22, sz * 0.22);
                RenderOptions.SetBitmapScalingMode(im, BitmapScalingMode.HighQuality);
                sp.Children.Add(im);
            }

            TextBlock tb = new TextBlock();
            tb.Text = it.Name;
            tb.FontFamily = new FontFamily("Candara, Microsoft YaHei UI");
            tb.Foreground = new SolidColorBrush(Color.FromArgb(248, 18, 18, 18));
            TextShadow(tb);
            tb.Foreground = new SolidColorBrush(Color.FromArgb(248, 18, 18, 18));
            TextShadow(tb);
            tb.TextAlignment = TextAlignment.Center;
            tb.TextTrimming = TextTrimming.CharacterEllipsis;
            tb.Width = tw - 6;
            tb.Margin = new Thickness(0, 7, 0, 1);
            sp.Children.Add(tb);

            b.Child = sp;
            b.Tag = it;

            b.MouseEnter += delegate { b.Background = new SolidColorBrush(Color.FromArgb(36, 255, 255, 255)); };
            b.MouseLeave += delegate { b.Background = Brushes.Transparent; };

            Item captured = it;
            b.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            {
                // 文件夹页交给 _folderWrap 的拖拽逻辑，不拦截事件
                if (_inFolderPage) return;
                e.Handled = true;
                _dragItem = captured;
                _dragTile = b;
                _dragStart = e.GetPosition(this);
                _dragOffset = e.GetPosition(b);
                _dragFromX = Canvas.GetLeft(b);
                _dragFromY = Canvas.GetTop(b);
                _dragging = false;
                b.CaptureMouse();
            };
            b.MouseMove += delegate(object s, MouseEventArgs e)
            {
                if (_dragTile != b || e.LeftButton != MouseButtonState.Pressed) return;
                Point p = e.GetPosition(this);
                if (_dragTile == b && e.LeftButton == MouseButtonState.Pressed) HighlightGroupHead(p);
                if (!_dragging && (Math.Abs(p.X - _dragStart.X) > 6 || Math.Abs(p.Y - _dragStart.Y) > 6))
                {
                    _dragging = true;
                    b.Opacity = 0.45;
                }
            };
            b.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                b.ReleaseMouseCapture();
                b.Opacity = 1;
                if (_dragging)
                {
                    _dragging = false;
                    ClearGroupHeadHighlight();
                    {
                        GroupDef dropG = GroupUnderPoint(e.GetPosition(this));
                        if (dropG != null && _dragItem != null && !_dragItem.IsFolder)
                        {
                            MoveItemToGroup(_dragItem, dropG);
                            _dragItem = null;
                            _dragTile = null;
                            b.Opacity = 1;
                            return;
                        }
                    }
                    if (_dragItem != null)
                    {
                        // 拖到书签上也能归入对应标签（窗口没开时也能用）
                        if (_tagRail != null && !_dragItem.IsFolder)
                        {
                            for (int bi = 0; bi < _tagRail.Children.Count && bi < _cfg.Tags.Count; bi++)
                            {
                                FrameworkElement bm = _tagRail.Children[bi] as FrameworkElement;
                                if (bm == null) continue;
                                Point bp = e.GetPosition(bm);
                                if (bp.X >= 0 && bp.Y >= 0 && bp.X <= bm.ActualWidth && bp.Y <= bm.ActualHeight)
                                {
                                    MoveToTag(_dragItem, bi);
                                    _dragItem = null;
                                    _dragTile = null;
                                    b.Opacity = 1;
                                    return;
                                }
                            }
                        }
                        // 文件夹页里拖动 → 在文件夹内重新排序
                        if (_inFolderPage && b.Parent == _folderWrap && _openFolderIdx >= 0 && _openFolderIdx < _cfg.Folders.Count)
                        {
                            int targetIdx = -1;
                            for (int fi3 = 0; fi3 < _folderWrap.Children.Count; fi3++)
                            {
                                FrameworkElement fe3 = _folderWrap.Children[fi3] as FrameworkElement;
                                if (fe3 == null || fe3 == b) continue;
                                Point fp = e.GetPosition(fe3);
                                if (fp.X >= 0 && fp.Y >= 0 && fp.X <= fe3.ActualWidth && fp.Y <= fe3.ActualHeight)
                                {
                                    Item tgt = fe3.Tag as Item;
                                    if (tgt != null)
                                    {
                                        for (int qj = 0; qj < _cfg.Folders[_openFolderIdx].Items.Count; qj++)
                                        {
                                            if (string.Equals(_cfg.Folders[_openFolderIdx].Items[qj].Path, tgt.Path, StringComparison.OrdinalIgnoreCase)) { targetIdx = qj; break; }
                                        }
                                    }
                                    if (targetIdx >= 0) break;
                                }
                            }
                            if (targetIdx >= 0)
                            {
                                FolderDef fo = _cfg.Folders[_openFolderIdx];
                                string dragPath = _dragItem.Path;
                                int from = -1, to = -1;
                                for (int qi = 0; qi < fo.Items.Count; qi++)
                                {
                                    if (string.Equals(fo.Items[qi].Path, dragPath, StringComparison.OrdinalIgnoreCase)) from = qi;
                                }
                                to = targetIdx;
                                if (from >= 0 && to >= 0 && from != to)
                                {
                                    ItemCfg mv = fo.Items[from];
                                    fo.Items.RemoveAt(from);
                                    fo.Items.Insert(to, mv);
                                    ConfigStore.Save(_cfg);
                                    // 重新生成首页数据，再用最新的文件夹内容重开，否则页面还是旧顺序
                                    RebuildTiles();
                                    foreach (Item fx in BuildHomeItems())
                                    {
                                        if (fx.IsFolder && fx.FolderIndex == _openFolderIdx) { OpenFolder(fx); break; }
                                    }
                                }
                            }
                            _dragItem = null;
                            _dragTile = null;
                            b.Opacity = 1;
                            return;
                        }
                        // 落在开着的标签窗口里 → 直接移进该标签
                        if (_tagOpen >= 0 && _tagPanel != null && _tagPanel.Visibility == Visibility.Visible && !_dragItem.IsFolder)
                        {
                            Point tp = e.GetPosition(_tagPanel);
                            if (tp.X >= 0 && tp.Y >= 0 && tp.X <= _tagPanel.ActualWidth && tp.Y <= _tagPanel.ActualHeight)
                            {
                                MoveToTag(_dragItem, _tagOpen);
                                _dragItem = null;
                                _dragTile = null;
                                b.Opacity = 1;
                                return;
                            }
                        }
                        Point p = e.GetPosition(_homeCanvas);
                        Point pc = e.GetPosition(_homeCanvas);
                        Item over = TileAt(pc);
                        if (over != null && over.IsFolder && !_dragItem.IsFolder)
                        {
                            MoveIntoFolder(_dragItem, over);
                            _dragItem = null;
                            _dragTile = null;
                            return;
                        }
                        int c = (int)Math.Round((p.X - _dragOffset.X - PadX) / CellW);
                        int r = (int)Math.Round((p.Y - _dragOffset.Y - PadY) / CellH);
                        if (c < 0) c = 0;
                        if (r < 0) r = 0;
                        int dspan = _dragItem.Span < 1 ? 1 : (_dragItem.Span > 3 ? 3 : _dragItem.Span);
                        int cols = Math.Max(1, (int)((_panelW - PadX * 2) / CellW));

                        HashSet<string> used = new HashSet<string>();
                        foreach (UIElement ch in _homeCanvas.Children)
                        {
                            if (ch == b) continue;
                            FrameworkElement fe2 = ch as FrameworkElement;
                            Item other = fe2 == null ? null : fe2.Tag as Item;
                            if (fe2 == null || other == null) continue;
                            int oc = (int)Math.Round((Canvas.GetLeft(fe2) - PadX) / CellW);
                            int or2 = (int)Math.Round((Canvas.GetTop(fe2) - PadY) / CellH);
                            int os = other.Span < 1 ? 1 : (other.Span > 3 ? 3 : other.Span);
                            Mark(used, os, oc, or2);
                        }

                        FreezeLayout();
                        if (CanPlace(used, cols, dspan, c, r))
                        {
                            Canvas.SetLeft(b, PadX + c * CellW);
                            Canvas.SetTop(b, PadY + r * CellH);
                            SetItemPos(_dragItem, PadX + c * CellW, PadY + r * CellH);
                        }
                        else
                        {
                            Canvas.SetLeft(b, _dragFromX);
                            Canvas.SetTop(b, _dragFromY);
                        }
                    }
                    _dragItem = null;
                    _dragTile = null;
                    return;
                }
                _dragTile = null;
                _dragItem = null;
                if (captured.IsFolder) OpenFolder(captured);
                else Launch(captured.Path);
            };

            b.MouseRightButtonUp += delegate(object s2, MouseButtonEventArgs e2)
            {
                e2.Handled = true;
                ShowItemMenu(captured, b);
            };

            if (!it.IsFolder) b.ToolTip = it.Path;
            return b;
        }

        private FrameworkElement BuildFolderGlyph(Item it, double sz)
        {
            Border outer = new Border();
            outer.Width = sz;
            outer.Height = sz;
            outer.CornerRadius = new CornerRadius(sz * 0.24);
            outer.Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
            outer.BorderThickness = new Thickness(1);
            outer.BorderBrush = new SolidColorBrush(Color.FromArgb(56, 255, 255, 255));

            // 文件夹预览：最多 3x3
            UniformGrid ug = new UniformGrid();
            ug.Rows = 3;
            ug.Columns = 3;
            ug.Margin = new Thickness(sz * 0.12);
            double mini = sz * 0.24;
            double rad = mini * 0.22;

            for (int i = 0; i < 9; i++)
            {
                if (i < it.Children.Count)
                {
                    Image im = new Image();
                    im.Source = it.Children[i].Icon;
                    im.Width = mini;
                    im.Height = mini;
                    im.Stretch = Stretch.Uniform;
                    im.Margin = new Thickness(0.8);
                    im.Clip = new RectangleGeometry(new Rect(0, 0, mini, mini), rad, rad);
                    RenderOptions.SetBitmapScalingMode(im, BitmapScalingMode.HighQuality);
                    ug.Children.Add(im);
                }
                else ug.Children.Add(new Border());
            }
            outer.Child = ug;
            return outer;
        }

        // ------------------------------------------------------------ pages
        private void OpenFolder(Item folder)
        {
            _inFolderPage = true;
            _openFolderIdx = folder.FolderIndex;
            _folderWrap.Children.Clear();
            _folderTitle.Text = folder.Name + "   ·   " + folder.Children.Count;
            // 已归入标签的图标不在文件夹里重复出现
            List<string> taggedPaths = new List<string>();
            foreach (TagDef tgx2 in _cfg.Tags)
            {
                foreach (ItemCfg tx3 in tgx2.Items) taggedPaths.Add(tx3.Path);
                foreach (TagFolder tfx2 in tgx2.Folders)
                    foreach (ItemCfg tx4 in tfx2.Items) taggedPaths.Add(tx4.Path);
            }
            foreach (Item c in folder.Children)
            {
                if (taggedPaths.Contains(c.Path)) continue;
                _folderWrap.Children.Add(BuildTile(c));
            }

            if (!_fHooked && _folderWrap != null)
            {
                _fHooked = true;
                _folderWrap.AddHandler(UIElement.MouseLeftButtonDownEvent, new MouseButtonEventHandler(delegate(object s0, MouseButtonEventArgs e0)
                {
                    _fDrag = false;
                    _fTile = null;
                    _fStart = e0.GetPosition(this);
                    foreach (UIElement ch in _folderWrap.Children)
                    {
                        FrameworkElement fe = ch as FrameworkElement;
                        if (fe == null) continue;
                        Point q = e0.GetPosition(fe);
                        if (q.X >= 0 && q.Y >= 0 && q.X <= fe.ActualWidth && q.Y <= fe.ActualHeight) { _fTile = fe; break; }
                    }
                }), true);
                _folderWrap.AddHandler(UIElement.MouseMoveEvent, new MouseEventHandler(delegate(object s0, MouseEventArgs e0)
                {
                    if (_fTile == null || e0.LeftButton != MouseButtonState.Pressed) return;
                    Point q = e0.GetPosition(this);
                    if (!_fDrag && (Math.Abs(q.X - _fStart.X) > 5 || Math.Abs(q.Y - _fStart.Y) > 5))
                    {
                        _fDrag = true;
                        _fTile.Opacity = 0.5;
                        _folderWrap.CaptureMouse();
                    }
                }), true);
                _folderWrap.AddHandler(UIElement.MouseLeftButtonUpEvent, new MouseButtonEventHandler(delegate(object s0, MouseButtonEventArgs e0)
                {
                    _folderWrap.ReleaseMouseCapture();
                    if (!_fDrag || _fTile == null) { _fDrag = false; return; }
                    _fDrag = false;
                    FrameworkElement from = _fTile;
                    from.Opacity = 1;
                    Item fromItem = from.Tag as Item;
                    FrameworkElement over = null;
                    foreach (UIElement ch in _folderWrap.Children)
                    {
                        FrameworkElement fe = ch as FrameworkElement;
                        if (fe == null || fe == from) continue;
                        Point q = e0.GetPosition(fe);
                        if (q.X >= 0 && q.Y >= 0 && q.X <= fe.ActualWidth && q.Y <= fe.ActualHeight) { over = fe; break; }
                    }
                    if (fromItem == null || over == null) return;
                    Item overItem = over.Tag as Item;
                    if (overItem == null) return;
                    if (_openFolderIdx < 0 || _openFolderIdx >= _cfg.Folders.Count) return;

                    FolderDef fo = _cfg.Folders[_openFolderIdx];
                    int a = -1, bIdx = -1;
                    for (int q2 = 0; q2 < fo.Items.Count; q2++)
                    {
                        if (string.Equals(fo.Items[q2].Path, fromItem.Path, StringComparison.OrdinalIgnoreCase)) a = q2;
                        if (string.Equals(fo.Items[q2].Path, overItem.Path, StringComparison.OrdinalIgnoreCase)) bIdx = q2;
                    }
                    if (a < 0 || bIdx < 0 || a == bIdx) return;
                    ItemCfg mv = fo.Items[a];
                    fo.Items.RemoveAt(a);
                    fo.Items.Insert(bIdx, mv);
                    ConfigStore.Save(_cfg);
                    RebuildTiles();
                    foreach (Item fx in BuildHomeItems())
                        if (fx.IsFolder && fx.FolderIndex == _openFolderIdx) { OpenFolder(fx); break; }
                }), true);
            }
            _folderPage.Visibility = Visibility.Visible;
            _folderPage.IsHitTestVisible = true;
            _homePage.IsHitTestVisible = false;

            Fade(_folderPage, 1, 190, 0);
            Slide((TranslateTransform)_folderPage.RenderTransform, 26, 0, 230, false);
            Fade(_homePage, 0, 140, 0);
            Slide((TranslateTransform)_homePage.RenderTransform, 0, -22, 230, false);

            DispatcherTimer hide = new DispatcherTimer();
            hide.Interval = TimeSpan.FromMilliseconds(220);
            hide.Tick += delegate
            {
                hide.Stop();
                if (_folderPage.Visibility == Visibility.Visible) _homePage.Visibility = Visibility.Collapsed;
            };
            hide.Start();
        }

        private void CloseFolder()
        {
            _inFolderPage = false;
            if (_folderPage.Visibility != Visibility.Visible) return;
            _folderPage.IsHitTestVisible = false;
            _homePage.IsHitTestVisible = true;
            _homePage.Visibility = Visibility.Visible;
            Fade(_folderPage, 0, 140, 0);
            Slide((TranslateTransform)_folderPage.RenderTransform, 0, 26, 200, false);
            Fade(_homePage, 1, 200, 0);
            Slide((TranslateTransform)_homePage.RenderTransform, -22, 0, 230, false);
        }

        private static void Fade(UIElement el, double to, int ms, int delay)
        {
            DoubleAnimation a = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms));
            a.BeginTime = TimeSpan.FromMilliseconds(delay);
            a.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            el.BeginAnimation(UIElement.OpacityProperty, a);
        }

        private static void Slide(TranslateTransform tt, double from, double to, int ms, bool overshoot)
        {
            tt.X = from;
            DoubleAnimation a = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms));
            a.EasingFunction = overshoot
                ? (IEasingFunction)new BackEase { Amplitude = 0.35, EasingMode = EasingMode.EaseOut }
                : new CubicEase { EasingMode = EasingMode.EaseOut };
            tt.BeginAnimation(TranslateTransform.XProperty, a);
        }

        // ------------------------------------------------------------ open/close
        public bool IsOpen { get { return _open; } }

        /// <summary>由边缘书签触发；如果动画进行中则排队重试。</summary>
        public void OpenFromEdge()
        {
            if (_open) return;
            try
            {
                if (_busy) { DispatcherTimer t = new DispatcherTimer(); t.Interval = TimeSpan.FromMilliseconds(120); t.Tick += delegate { t.Stop(); OpenFromEdge(); }; t.Start(); return; }
                Open();
            }
            catch { }
        }

        public void CancelAutoClose()
        {
            _closeTimer.Stop();
        }

        public void Open()
        {
            if (_open || _busy) return;
            if (_root == null) { ComputeWorkArea(); InitLayout(); }
            _open = true;
            _busy = true;
            try { OpenCore(); }
            catch (Exception ex) { Program.Log("Open failed: " + ex); _busy = false; }
        }

        private void OpenCore()
        {
            if (_tab != null) _tab.HideNow();
            ApplyOpenBounds();
            Show();
            Native.KeepTopmost(new WindowInteropHelper(this).Handle);
            if (_cfg.MusicPanel && _music != null)
            {
                Rect wa2 = _workArea;
            _music.SetVeil(VeilAlpha() + 10);
                double mx = Math.Max(0, ActualWidth - (MusicPanel.PW * MusicPanel.S) - 18);
                _music.SetPos(mx, 26);
                _music.Show(mx, 26, this);   // 和侧边栏同时弹出
                Program.Log("music show vis=" + _music.Visibility + " w=" + Width + " aw=" + ActualWidth + " root=" + _root.ActualWidth + " tabActual=" + (PresentationSource.FromVisual(this) != null));
            }

            // 先用缓存/底色立即开始滑动，玻璃在动画开始后再算（避免开面板时卡住）
            if (true)
            {
                _glass.Background = FixedBackdrop();   // 固定底色，不随背后窗口变化
            }
            {
                double specL = 12, specT = _panelH - 176;
            }
            _specWantShow = true;

            _specWantTop = _panelH - 176;
            try { RefreshGlass(); } catch { }
            if (_glassTimer == null)
            {
                _glassTimer = new DispatcherTimer();
                _glassTimer.Interval = TimeSpan.FromMilliseconds(700);
                _glassTimer.Tick += delegate
                {
                    if (!_open || _busy) return;
                    try { RefreshGlass(); } catch { }
                };
            }


            _panelHost.CacheMode = new BitmapCache(1.0);   // 动画期间改用位图缓存，减少每帧重绘
            _shift.BeginAnimation(TranslateTransform.XProperty, null);
            _shift.X = -_panelW;
            DoubleAnimation a = new DoubleAnimation(0, TimeSpan.FromMilliseconds(260));
            a.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            a.Completed += delegate { _busy = false; _panelHost.CacheMode = null; };
            _shift.BeginAnimation(TranslateTransform.XProperty, a);
            Fade(_overlay, 1, 260, 0);
        }

        private void RefreshGlass()
        {
            ApplyGlassStrength();
            // 固定底色：不再截屏，也不再随模糊档变透明，彻底杜绝背景变化
            _glass.Background = FixedBackdrop();
        }

        public void CloseDrawer()
        {
            if (!_open || _busy) return;
            CloseFolder();
            if (_music != null) _music.Hide();
            _specWantShow = false;
            HideTagPanel();
            DoubleAnimation a = new DoubleAnimation(-_panelW, TimeSpan.FromMilliseconds(250));
            a.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn };
            a.Completed += delegate
            {
                _open = false;
                ApplyClosedBounds();
            };
            _shift.BeginAnimation(TranslateTransform.XProperty, a);
            Fade(_overlay, 0.1, 230, 0);
        }

        public void Toggle()
        {
            if (_open) CloseDrawer(); else Open();
        }

        private void FadeNub(double to, int ms)
        {
            if (_nub == null) return;
            DoubleAnimation a = new DoubleAnimation(to, TimeSpan.FromMilliseconds(ms));
            _nub.BeginAnimation(UIElement.OpacityProperty, a);
        }

        private void StartCloseTimer()
        {
            _closeTimer.Stop();
            if (_cfg.Pinned) return;
            if (_tagOpen >= 0) return;
            if (_menuOpen) return;
            if (_dragging || _tagDragging) return;
            if (Mouse.LeftButton == MouseButtonState.Pressed) return;
            if (CursorOverTagPanel()) return;
            _closeTimer.Start();
        }

        private bool CursorOverTagPanel()
        {
            if (_tagPanel == null || _tagPanel.Visibility != Visibility.Visible) return false;
            try
            {
                System.Windows.Point p = Mouse.GetPosition(_tagPanel);
                return p.X >= 0 && p.Y >= 0 && p.X <= _tagPanel.ActualWidth && p.Y <= _tagPanel.ActualHeight;
            }
            catch { return false; }
        }
        // ------------------------------------------------------------ glass capture
        private ImageSource CaptureForGlass()
        {
            Rect wa = ScreenUtil.WorkAreaDIP(this);
            double px = 1, py = 1;
            try
            {
                PresentationSource src = PresentationSource.FromVisual(this);
                if (src != null && src.CompositionTarget != null)
                {
                    Matrix m = src.CompositionTarget.TransformToDevice;
                    px = m.M11; py = m.M22;
                }
            }
            catch { }

            int x = (int)Math.Round(wa.Left * px);
            int y = (int)Math.Round(wa.Top * py);
            int w = (int)Math.Round(_panelW * px);
            int h = (int)Math.Round(_panelH * py);
            if (w < 8 || h < 8) return null;

            try
            {
                using (System.Drawing.Bitmap bmp = new System.Drawing.Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(bmp))
                        g.CopyFromScreen(x, y, 0, 0, new System.Drawing.Size(w, h), System.Drawing.CopyPixelOperation.SourceCopy);
                    int sw = Math.Max(8, w / 2);
                    int sh = Math.Max(8, h / 2);
                    System.Drawing.Bitmap half = new System.Drawing.Bitmap(sw, sh, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    using (System.Drawing.Graphics g2 = System.Drawing.Graphics.FromImage(half))
                    {
                        g2.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
                        g2.DrawImage(bmp, new System.Drawing.Rectangle(0, 0, sw, sh));
                    }
                    bmp.Dispose();
                    System.Drawing.Imaging.BitmapData data = half.LockBits(
                        new System.Drawing.Rectangle(0, 0, sw, sh),
                        System.Drawing.Imaging.ImageLockMode.ReadOnly,
                        System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    try
                    {
                        int stride = data.Stride;
                        byte[] buf = new byte[stride * sh];
                        Marshal.Copy(data.Scan0, buf, 0, buf.Length);
                        try
                        {
                            double sc = (double)sw / Math.Max(1.0, _panelW);
                            RefractInPlace(buf, sw, sh, Radius * sc, 26 * sc, -22 * sc);
                        }
                        catch { }                        BitmapSource raw = BitmapSource.Create(sw, sh, 96, 96, PixelFormats.Bgra32, null, buf, stride);
                        raw.Freeze();
                        return BlurDown(raw, sw, sh, BlurRadius());
                    }
                    finally { half.UnlockBits(data); }
                }
            }
            catch { return FallbackGlass(); }
        }

        private static BitmapSource RenderElement(FrameworkElement el, int w, int h)
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

        // ---------------------------------------------------------- 折射（移植自 AndroidLiquidGlass / kmp）
        // 对应 Shaders.kt 里的 RoundedRectRefractionShader：
        //   sd = sdRoundedRect(...)；边缘 refractionHeight 内的像素按 circleMap 向边缘位移采样。
        private static void RefractInPlace(byte[] buf, int w, int h, double radius, double refractionHeight, double refractionAmount)
        {
            if (refractionHeight <= 0 || refractionAmount == 0) return;
            int stride = w * 4;
            byte[] src = new byte[buf.Length];
            Buffer.BlockCopy(buf, 0, src, 0, buf.Length);

            double hx = w * 0.5, hy = h * 0.5;
            double r = Math.Min(radius, Math.Min(hx, hy));
            double gradR = Math.Min(r * 1.5, Math.Min(hx, hy));

            int band = (int)Math.Ceiling(refractionHeight) + 2;
            for (int y = 0; y < h; y++)
            {
                bool rowEdge = (y < band) || (y >= h - band);
                if (!rowEdge && (h - 2 * band) <= 0) rowEdge = true;
                double cy = (y + 0.5) - hy;
                for (int xi = 0; xi < w; xi++)
                {
                    // 环带优化：非上下边缘行时，只处理左右两条竖边
                    int x = xi;
                    if (!rowEdge && xi >= band && xi < w - band) continue;
                    double cx = (x + 0.5) - hx;
                    double sdx = Math.Abs(cx) - (hx - r);
                    double sdy = Math.Abs(cy) - (hy - r);
                    double outside = Math.Sqrt(Math.Max(sdx, 0) * Math.Max(sdx, 0) + Math.Max(sdy, 0) * Math.Max(sdy, 0)) - r;
                    double inside = Math.Min(Math.Max(sdx, sdy), 0);
                    double sd = outside + inside;
                    if (-sd >= refractionHeight) continue;

                    double t = 1.0 + sd / refractionHeight;      // sd<=0 → t∈[0,1]
                    if (t < 0) t = 0;
                    double d = (1.0 - Math.Sqrt(Math.Max(0.0, 1.0 - t * t))) * refractionAmount;

                    // 梯度（圆角矩形的外法线）
                    double gcx = Math.Abs(cx) - (hx - gradR);
                    double gcy = Math.Abs(cy) - (hy - gradR);
                    double gx, gy;
                    if (gcx >= 0 || gcy >= 0)
                    {
                        double nx = Math.Max(gcx, 0), ny = Math.Max(gcy, 0);
                        double len = Math.Sqrt(nx * nx + ny * ny);
                        if (len < 1e-6) { nx = 0; ny = 1; len = 1; }
                        gx = Math.Sign(cx) * nx / len;
                        gy = Math.Sign(cy) * ny / len;
                    }
                    else
                    {
                        double stepG = gcy <= gcx ? 1.0 : 0.0;
                        gx = Math.Sign(cx) * stepG;
                        gy = Math.Sign(cy) * (1.0 - stepG);
                        if (gx == 0 && gy == 0) gy = 1;
                    }

                    int sx = (int)Math.Round(x + d * gx);
                    int sy = (int)Math.Round(y + d * gy);
                    if (sx < 0) sx = 0; if (sx >= w) sx = w - 1;
                    if (sy < 0) sy = 0; if (sy >= h) sy = h - 1;

                    int di = y * stride + x * 4;
                    int si = sy * stride + sx * 4;
                    buf[di] = src[si];
                    buf[di + 1] = src[si + 1];
                    buf[di + 2] = src[si + 2];
                    buf[di + 3] = 255;
                }
            }
        }
        private static ImageSource BlurDown(BitmapSource src, int w, int h, int radius)
        {
            if (radius <= 0) return src;
            int f = 10;
            int sw = Math.Max(2, w / f);
            int sh = Math.Max(2, h / f);

            Image small = new Image();
            small.Source = src;
            small.Stretch = Stretch.Fill;
            // 边缘夹取（等价 TileMode.Clamp）：略放大再模糊，避免四周变暗
            double pad = 1.0 + Math.Min(0.25, 24.0 / Math.Max(1, w));
            small.RenderTransformOrigin = new Point(0.5, 0.5);
            small.RenderTransform = new ScaleTransform(pad, pad);
            BlurEffect blur = new BlurEffect();
            blur.KernelType = KernelType.Gaussian;
            blur.Radius = 9;
            blur.RenderingBias = RenderingBias.Quality;
            small.Effect = blur;
            BitmapSource mid = RenderElement(small, sw, sh);

            Image big = new Image();
            big.Source = mid;
            big.Stretch = Stretch.Fill;
            RenderOptions.SetBitmapScalingMode(big, BitmapScalingMode.HighQuality);
            RenderOptions.SetEdgeMode(big, EdgeMode.Unspecified);
            return RenderElement(big, w, h);
        }

        private ImageSource FallbackGlass()
        {
            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext dc = dv.RenderOpen())
            {
                LinearGradientBrush b = new LinearGradientBrush(Color.FromRgb(30, 30, 38), Color.FromRgb(16, 16, 20), 90);
                dc.DrawRectangle(b, null, new Rect(0, 0, _panelW, _panelH));
            }
            RenderTargetBitmap rtb = new RenderTargetBitmap((int)_panelW, (int)_panelH, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Freeze();
            return rtb;
        }

        // ------------------------------------------------------------ launch
        private void Launch(string path)
        {
            StartTarget(path, false);
        }

        /// <summary>以管理员身份启动（需要 UAC 提升的程序，比如会注入别人进程的工具）。</summary>
        private void LaunchAdmin(string path)
        {
            StartTarget(path, true);
        }

        private void StartTarget(string path, bool asAdmin)
        {
            try
            {
                string target = path;
                string arga = "";
                string wd = null;

                // .lnk：解析出真实目标，便于指定工作目录 / 提权
                if (path.ToLowerInvariant().EndsWith(".lnk"))
                {
                    try
                    {
                        Type sh = Type.GetTypeFromProgID("WScript.Shell");
                        object shell = Activator.CreateInstance(sh);
                        object sc = sh.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { path });
                        string t = sc.GetType().InvokeMember("TargetPath", System.Reflection.BindingFlags.GetProperty, null, sc, null) as string;
                        string a = sc.GetType().InvokeMember("Arguments", System.Reflection.BindingFlags.GetProperty, null, sc, null) as string;
                        string w = sc.GetType().InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.GetProperty, null, sc, null) as string;
                        if (!string.IsNullOrEmpty(t)) { target = t; arga = a ?? ""; wd = string.IsNullOrEmpty(w) ? IOPath.GetDirectoryName(t) : w; }
                    }
                    catch (Exception ex) { Program.Log("lnk resolve failed: " + ex.Message); }
                }

                ProcessStartInfo psi = new ProcessStartInfo();
                if (asAdmin)
                {
                    psi.FileName = target;
                    if (!string.IsNullOrEmpty(arga)) psi.Arguments = arga;
                    if (!string.IsNullOrEmpty(wd)) psi.WorkingDirectory = wd;
                    psi.Verb = "runas";
                }
                else
                {
                    // 普通启动：用原始路径走 ShellExecute，保留快捷方式自身的“管理员”标记
                    psi.FileName = path;
                }
                psi.UseShellExecute = true;
                Process pr = Process.Start(psi);
                Program.Log("launch " + path + " -> " + target + (asAdmin ? " (admin)" : "") + " pid=" + (pr == null ? 0 : pr.Id));

                DispatcherTimer closeAfter = new DispatcherTimer();
                closeAfter.Interval = TimeSpan.FromMilliseconds(220);
                closeAfter.Tick += delegate
                {
                    closeAfter.Stop();
                    try { CloseDrawer(); } catch { }
                };
                closeAfter.Start();
            }
            catch (Exception ex)
            {
                Program.Log("launch FAILED " + path + ": " + ex.Message);
            }
        }

        private void OpenItemMenu(Item it, FrameworkElement anchor)
        {
            ContextMenu cm = MakeMenu();
            MenuItem open = new MenuItem();
            open.Header = "打开";
            open.Click += delegate { if (it.IsFolder) OpenFolder(it); else Launch(it.Path); };
            cm.Items.Add(open);

            MenuItem loc = new MenuItem();
            loc.Header = "打开所在位置";
            loc.Click += delegate
            {
                try
                {
                    string target = it.IsFolder ? (it.Children.Count > 0 ? it.Children[0].Path : "") : it.Path;
                    if (string.IsNullOrEmpty(target)) return;
                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.FileName = "explorer.exe";
                    psi.Arguments = "/select,\"" + target + "\"";
                    psi.UseShellExecute = true;
                    Process.Start(psi);
                }
                catch { }
            };
            cm.Items.Add(loc);

            if (!it.IsFolder)
            {
                MenuItem adm = new MenuItem();
                adm.Header = "以管理员身份运行";
                adm.Click += delegate { LaunchAdmin(it.Path); };
                cm.Items.Add(adm);
            }

            if (!it.IsFolder && it.Cfg != null)
            {
                cm.Items.Add(new Separator());


                MenuItem spanItem = new MenuItem();
                spanItem.Header = "占位大小";
                for (int si = 1; si <= 3; si++)
                {
                    int sv = si;
                    MenuItem sm = new MenuItem();
                    sm.Header = si == 1 ? "1 × 1（普通）" : (si == 2 ? "2 × 2（大格子）" : "3 × 3（超大格子）");
                    sm.IsChecked = (it.Cfg.Span <= 1 ? 1 : (it.Cfg.Span >= 3 ? 3 : 2)) == si;
                    sm.Click += delegate { SetItemSpan(it, sv); };
                    spanItem.Items.Add(sm);
                }
                cm.Items.Add(spanItem);
            }
            else if (it.IsFolder && it.FolderIndex >= 0)
            {
                cm.Items.Add(new Separator());

                MenuItem renameFolder = new MenuItem();
                renameFolder.Header = "重命名文件夹";
                renameFolder.Click += delegate
                {
                    if (it.FolderIndex < 0 || it.FolderIndex >= _cfg.Folders.Count) return;
                    string nn = PromptWindow.Show(this, "重命名文件夹", _cfg.Folders[it.FolderIndex].Name);
                    if (string.IsNullOrEmpty(nn)) return;
                    _cfg.Folders[it.FolderIndex].Name = nn.Trim();
                    ConfigStore.Save(_cfg);
                    RebuildTiles();
                };
                cm.Items.Add(renameFolder);
                FolderDef f = _cfg.Folders[it.FolderIndex];

                MenuItem spanItem2 = new MenuItem();
                spanItem2.Header = "占位大小";
                for (int si = 1; si <= 3; si++)
                {
                    int sv = si;
                    MenuItem sm = new MenuItem();
                    sm.Header = si == 1 ? "1 × 1（普通）" : (si == 2 ? "2 × 2（大格子）" : "3 × 3（超大格子）");
                    sm.IsChecked = (f.Span <= 1 ? 1 : (f.Span >= 3 ? 3 : 2)) == si;
                    sm.Click += delegate { SetFolderSpan(it.FolderIndex, sv); };
                    spanItem2.Items.Add(sm);
                }
                cm.Items.Add(spanItem2);

                MenuItem dissolve = new MenuItem();
                dissolve.Header = "解散文件夹（图标移回首页）";
                dissolve.Click += delegate
                {
                    if (it.FolderIndex < 0 || it.FolderIndex >= _cfg.Folders.Count) return;
                    FolderDef df = _cfg.Folders[it.FolderIndex];
                    _cfg.Pins.AddRange(df.Items);
                    _cfg.Folders.RemoveAt(it.FolderIndex);
                    ConfigStore.Save(_cfg);
                    RebuildTiles();
                };
                cm.Items.Add(dissolve);
            }

            _menuOpen = true;
            cm.Closed += delegate { _menuOpen = false; };
            cm.PlacementTarget = anchor;
            cm.IsOpen = true;
        }

        /// <summary>把图标移动到某个分类（会从其他分类里移除同名项）。</summary>
        private void MoveItemToGroup(Item it, GroupDef target)
        {
            if (it == null || target == null || string.IsNullOrEmpty(it.Path)) return;

            foreach (GroupDef g in _cfg.Groups)
            {
                for (int i = g.Items.Count - 1; i >= 0; i--)
                {
                    if (string.Equals(g.Items[i], it.Path, StringComparison.OrdinalIgnoreCase))
                        g.Items.RemoveAt(i);
                }
            }
            target.Items.Add(it.Path);
            ConfigStore.Save(_cfg);
            Program.Log("move " + it.Path + " -> " + target.Name);
            RebuildTiles();
        }

        /// <summary>拖拽时高亮鼠标下的分类标题。</summary>
        private void HighlightGroupHead(Point p)
        {
            for (int i = 0; i < _groupHeads.Count; i++)
            {
                HeadRef h = _groupHeads[i];
                if (h.Host == null) continue;
                Point lp = p;
                try { lp = this.TranslatePoint(p, h.Host); } catch { }
                bool inside = lp.X >= 0 && lp.Y >= 0 && lp.X <= h.Host.ActualWidth && lp.Y <= h.Host.ActualHeight;
                h.Bg.Background = inside
                    ? new SolidColorBrush(Color.FromArgb(70, 255, 255, 255))
                    : Brushes.Transparent;
            }
        }

        private void ClearGroupHeadHighlight()
        {
            for (int i = 0; i < _groupHeads.Count; i++)
                if (_groupHeads[i].Bg != null) _groupHeads[i].Bg.Background = Brushes.Transparent;
        }

        /// <summary>如果松手时在某个分类标题上，返回那个分类。</summary>
        private GroupDef GroupUnderPoint(Point p)
        {
            for (int i = 0; i < _groupHeads.Count; i++)
            {
                HeadRef h = _groupHeads[i];
                if (h.Host == null) continue;
                Point lp = p;
                try { lp = this.TranslatePoint(p, h.Host); } catch { }
                if (lp.X >= 0 && lp.Y >= 0 && lp.X <= h.Host.ActualWidth && lp.Y <= h.Host.ActualHeight)
                    return h.G;
            }
            return null;
        }
        private void OpenMenu()
        {
            ContextMenu cm = MakeMenu();

            MenuItem sizeItem = new MenuItem();
            sizeItem.Header = "图标大小";
            double[] sizes = new double[] { 40, 52, 66, 82 };
            string[] names = new string[] { "小", "中", "大", "特大" };
            for (int i = 0; i < sizes.Length; i++)
            {
                double s = sizes[i];
                MenuItem mi = new MenuItem();
                mi.Header = names[i] + "  (" + ((int)s) + "px)";
                mi.IsChecked = Math.Abs(_cfg.IconSize - s) < 0.5;
                mi.Click += delegate
                {
                    _cfg.IconSize = s;
                    ConfigStore.Save(_cfg);
                    RebuildTiles();
                };
                sizeItem.Items.Add(mi);
            }
            cm.Items.Add(sizeItem);

            MenuItem clockItem = new MenuItem();
            clockItem.Header = "显示时钟";
            clockItem.IsCheckable = true;
            clockItem.IsChecked = _cfg.ShowClock;
            clockItem.Click += delegate
            {
                _cfg.ShowClock = !_cfg.ShowClock;
                _clock.Visibility = _cfg.ShowClock ? Visibility.Visible : Visibility.Collapsed;
                _date.Visibility = _cfg.ShowClock ? Visibility.Visible : Visibility.Collapsed;
                ConfigStore.Save(_cfg);
            };
            cm.Items.Add(clockItem);


            MenuItem musicItem = new MenuItem();
            musicItem.Header = "音乐面板（跟随系统播放器）";
            musicItem.IsCheckable = true;
            musicItem.IsChecked = _cfg.MusicPanel;
            musicItem.Click += delegate
            {
                _cfg.MusicPanel = !_cfg.MusicPanel;
                ConfigStore.Save(_cfg);
                if (_music == null) return;
                if (_cfg.MusicPanel && _open)
                {
                    Rect wam = _workArea;
                    double mxm = Math.Max(0, ActualWidth - (MusicPanel.PW * MusicPanel.S) - 18);
                    _music.SetPos(mxm, 26);
                    _music.Show(mxm, 26, this);
                }
                else _music.Hide();
            };
            cm.Items.Add(musicItem);

            

            cm.Items.Add(new Separator());

            MenuItem deskIcons = new MenuItem();
            deskIcons.Header = "显示 / 隐藏桌面图标";
            deskIcons.Click += delegate
            {
                try
                {
                    IntPtr def = Native.FindDefView();
                    if (def != IntPtr.Zero)
                        Native.SendMessage(def, 0x0111, (IntPtr)0x7402, IntPtr.Zero);
                }
                catch { }
            };
            cm.Items.Add(deskIcons);




            MenuItem relayout = new MenuItem();
            relayout.Header = "图标重新排列（恢复网格）";
            relayout.Click += delegate
            {
                foreach (ItemCfg ic in _cfg.Pins) { ic.X = -1; ic.Y = -1; }
                foreach (FolderDef fd in _cfg.Folders) { fd.X = -1; fd.Y = -1; }
                ConfigStore.Save(_cfg);
                RebuildTiles();
            };
            cm.Items.Add(relayout);

            MenuItem rescan = new MenuItem();
            rescan.Header = "重新扫描桌面";
            rescan.Click += delegate
            {
                Cfg tmp = new Cfg();
                tmp.ConfigPath = _cfg.ConfigPath;
                try { ConfigStore.BuildDefault(tmp); } catch { }

                List<string> known = new List<string>();
                foreach (ItemCfg p0 in _cfg.Pins) known.Add(p0.Path);
                foreach (FolderDef f0 in _cfg.Folders)
                    foreach (ItemCfg fi0 in f0.Items) known.Add(fi0.Path);
                foreach (TagDef t0 in _cfg.Tags)
                {
                    foreach (ItemCfg ti0 in t0.Items) known.Add(ti0.Path);
                    foreach (TagFolder tf0 in t0.Folders)
                        foreach (ItemCfg ti1 in tf0.Items) known.Add(ti1.Path);
                }

                int added = 0;
                foreach (ItemCfg np in tmp.Pins)
                {
                    if (string.IsNullOrEmpty(np.Path)) continue;
                    bool dup = false;
                    foreach (string k0 in known)
                        if (string.Equals(k0, np.Path, StringComparison.OrdinalIgnoreCase)) { dup = true; break; }
                    if (dup) continue;
                    _cfg.Pins.Add(np);
                    known.Add(np.Path);
                    added++;
                }

                foreach (FolderDef tf in tmp.Folders)
                {
                    foreach (ItemCfg nf2 in tf.Items)
                    {
                        if (string.IsNullOrEmpty(nf2.Path)) continue;
                        bool dup2 = false;
                        foreach (string k1 in known)
                            if (string.Equals(k1, nf2.Path, StringComparison.OrdinalIgnoreCase)) { dup2 = true; break; }
                        if (dup2) continue;
                        _cfg.Pins.Add(nf2);
                        known.Add(nf2.Path);
                        added++;
                    }
                }

                ConfigStore.Save(_cfg);
                RebuildTiles();
            };
            cm.Items.Add(rescan);

            MenuItem openCfg = new MenuItem();
            openCfg.Header = "编辑配置 (drawer.json)";
            openCfg.Click += delegate { try { Process.Start("notepad.exe", _cfg.ConfigPath); } catch { } };
            cm.Items.Add(openCfg);

            MenuItem startup = new MenuItem();
            startup.Header = "开机自启";
            startup.IsCheckable = true;
            startup.IsChecked = IsStartup();
            startup.Click += delegate
            {
                try
                {
                    using (RegistryKey k = Registry.CurrentUser.OpenSubKey(
                        @"Software\Microsoft\Windows\CurrentVersion\Run", true))
                    {
                        if (IsStartup()) k.DeleteValue("SideDrawer", false);
                        else k.SetValue("SideDrawer", "\"" + System.Reflection.Assembly.GetExecutingAssembly().Location + "\"");
                    }
                }
                catch { }
            };
            cm.Items.Add(startup);

            cm.Items.Add(new Separator());

            MenuItem quit = new MenuItem();
            quit.Header = "退出";
            quit.Click += delegate { Application.Current.Shutdown(); };
            cm.Items.Add(quit);

            _menuOpen = true;
            cm.Closed += delegate { _menuOpen = false; };
            cm.PlacementTarget = _glass;
            cm.IsOpen = true;
        }

        private Canvas _menuLayer;

        private void ClosePanelMenu()
        {
            if (_menuLayer != null)
            {
                _overlay.Children.Remove(_menuLayer);
                _menuLayer = null;
            }
            _menuOpen = false;
        }

        /// <summary>面板内菜单：直接画在侧边栏窗口里，永远在最上层，绝不被遮挡。</summary>
        private void OpenMenuInPanel()
        {
            ClosePanelMenu();
            _menuOpen = true;

            _menuLayer = new Canvas();
            _menuLayer.Width = _panelW;
            _menuLayer.Height = _panelH;
            Panel.SetZIndex(_menuLayer, 900);

            Rectangle catcher = new Rectangle();
            catcher.Width = _panelW;
            catcher.Height = _panelH;
            catcher.Fill = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
            catcher.MouseLeftButtonUp += delegate { ClosePanelMenu(); };
            _menuLayer.Children.Add(catcher);

            Border card = new Border();
            card.Width = 268;
            card.CornerRadius = new CornerRadius(14);
            card.Background = new SolidColorBrush(Color.FromArgb(250, 22, 22, 30));
            card.BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
            card.BorderThickness = new Thickness(1);
            card.Padding = new Thickness(6);

            ScrollViewer sv = new ScrollViewer();
            sv.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            sv.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            sv.MaxHeight = 640;
            StackPanel list = new StackPanel();
            sv.Content = list;
            card.Child = sv;

            AddMenuItem(list, "＋ 新建分类", delegate { AddGroup(); }, false);
            AddMenuSection(list, "添加图标（选文件）");
            foreach (GroupDef ag in _cfg.Groups)
            {
                GroupDef agLocal = ag;
                AddMenuItem(list, "＋ 加入 " + agLocal.Name, delegate { AddIconToGroup(agLocal); }, false);
            }
            AddMenuSeparator(list);

            AddMenuSection(list, "图标排序（分类内）");
            AddMenuItem(list, "按拖入顺序", delegate
            {
                _cfg.IconSort = 0;
                ConfigStore.Save(_cfg);
                RebuildTiles();
            }, _cfg.IconSort == 0);
            AddMenuItem(list, "按名称 A→Z", delegate
            {
                _cfg.IconSort = 1;
                ConfigStore.Save(_cfg);
                RebuildTiles();
            }, _cfg.IconSort == 1);

            AddMenuSection(list, "图标大小");
            double[] sizes = new double[] { 40, 52, 66, 82 };
            string[] names = new string[] { "小 (40)", "中 (52)", "大 (66)", "特大 (82)" };
            for (int i = 0; i < sizes.Length; i++)
            {
                double sz = sizes[i];
                AddMenuItem(list, names[i], delegate { _cfg.IconSize = sz; ConfigStore.Save(_cfg); RebuildTiles(); }, Math.Abs(_cfg.IconSize - sz) < 0.5);
            }

            AddMenuSection(list, "分类栏排序");
            AddMenuItem(list, "按创建时间", delegate
            {
                _cfg.GroupSort = 0;
                ConfigStore.Save(_cfg);
                RebuildTiles();
            }, _cfg.GroupSort == 0);
            AddMenuItem(list, "按名称 A→Z", delegate
            {
                _cfg.GroupSort = 1;
                ConfigStore.Save(_cfg);
                RebuildTiles();
            }, _cfg.GroupSort == 1);


            AddMenuSeparator(list);
            AddMenuSection(list, "时钟文字");
            AddMenuItem(list, "黑色", delegate { _cfg.ClockColor = 0; ConfigStore.Save(_cfg); ApplyClockColor(); }, _cfg.ClockColor == 0);
            AddMenuItem(list, "白色", delegate { _cfg.ClockColor = 1; ConfigStore.Save(_cfg); ApplyClockColor(); }, _cfg.ClockColor == 1);
            AddMenuItem(list, "显示时钟", delegate
            {
                _cfg.ShowClock = !_cfg.ShowClock;
                _clock.Visibility = _cfg.ShowClock ? Visibility.Visible : Visibility.Collapsed;
                _date.Visibility = _cfg.ShowClock ? Visibility.Visible : Visibility.Collapsed;
                ConfigStore.Save(_cfg);
            }, _cfg.ShowClock);

            AddMenuItem(list, "音乐面板（跟随系统播放器）", delegate
            {
                _cfg.MusicPanel = !_cfg.MusicPanel;
                ConfigStore.Save(_cfg);
                if (_music == null) return;
                if (_cfg.MusicPanel && _open)
                {
                    double mxm = Math.Max(0, ActualWidth - (MusicPanel.PW * MusicPanel.S) - 18);
                    _music.SetPos(mxm, 26);
                    _music.Show(mxm, 26, this);
                }
                else _music.Hide();
            }, _cfg.MusicPanel);

            AddMenuSeparator(list);
            AddMenuItem(list, "显示 / 隐藏桌面图标", delegate
            {
                try
                {
                    IntPtr def = Native.FindDefView();
                    if (def != IntPtr.Zero) Native.SendMessage(def, 0x0111, (IntPtr)0x7402, IntPtr.Zero);
                }
                catch { }
            }, false);

            AddMenuItem(list, "图标重新排列（恢复网格）", delegate
            {
                foreach (ItemCfg ic in _cfg.Pins) { ic.X = -1; ic.Y = -1; }
                foreach (FolderDef fd in _cfg.Folders) { fd.X = -1; fd.Y = -1; }
                ConfigStore.Save(_cfg);
                RebuildTiles();
            }, false);

            AddMenuItem(list, "编辑配置 (drawer.json)", delegate
            {
                try { Process.Start("notepad.exe", _cfg.ConfigPath); } catch { }
            }, false);

            AddMenuItem(list, "开机自启", delegate
            {
                try
                {
                    using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                    {
                        if (IsStartup()) k.DeleteValue("SideDrawer", false);
                        else k.SetValue("SideDrawer", "\"" + System.Reflection.Assembly.GetEntryAssembly().Location + "\"");
                    }
                }
                catch { }
            }, IsStartup());

            AddMenuSeparator(list);
            AddMenuItem(list, "退出", delegate { Application.Current.Shutdown(); }, false);

            _menuLayer.Children.Add(card);

            Point mp = Mouse.GetPosition(this);
            double cardH = Math.Min(660, list.Children.Count * 33 + 24);
            double x = mp.X + 6;
            double y = mp.Y + 6;
            if (x + 268 > _panelW - 12) x = Math.Max(12, _panelW - 280);
            if (y + cardH > _panelH - 12) y = Math.Max(12, _panelH - cardH - 12);
            Canvas.SetLeft(card, x);
            Canvas.SetTop(card, y);

            _overlay.Children.Add(_menuLayer);
        }

        private void AddMenuSection(StackPanel list, string text)
        {
            TextBlock t = new TextBlock();
            t.Text = text;
            t.FontFamily = new FontFamily("Microsoft YaHei UI");
            t.FontSize = 10.5;
            t.Foreground = new SolidColorBrush(Color.FromArgb(140, 255, 255, 255));
            t.Margin = new Thickness(12, 8, 12, 4);
            list.Children.Add(t);
        }

        private void AddMenuItem(StackPanel list, string text, Action act, bool isChecked)
        {
            Border row = new Border();
            row.CornerRadius = new CornerRadius(9);
            row.Padding = new Thickness(10, 7, 14, 8);
            row.Background = Brushes.Transparent;
            row.Cursor = Cursors.Hand;

            Grid gr = new Grid();
            gr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
            gr.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock chk = new TextBlock();
            chk.Text = "✓";
            chk.FontSize = 12;
            chk.Foreground = new SolidColorBrush(Color.FromRgb(255, 168, 192));
            chk.VerticalAlignment = VerticalAlignment.Center;
            chk.Visibility = isChecked ? Visibility.Visible : Visibility.Collapsed;
            Grid.SetColumn(chk, 0);
            gr.Children.Add(chk);

            TextBlock tx = new TextBlock();
            tx.Text = text;
            tx.FontFamily = new FontFamily("Microsoft YaHei UI");
            tx.FontSize = 12.5;
            tx.Foreground = new SolidColorBrush(Color.FromArgb(240, 255, 255, 255));
            tx.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(tx, 1);
            gr.Children.Add(tx);

            row.Child = gr;
            row.MouseEnter += delegate { row.Background = new SolidColorBrush(Color.FromArgb(46, 255, 255, 255)); };
            row.MouseLeave += delegate { row.Background = Brushes.Transparent; };
            row.MouseLeftButtonUp += delegate
            {
                try { if (act != null) act(); } catch { }
                ClosePanelMenu();
            };
            list.Children.Add(row);
        }

        private void AddMenuSeparator(StackPanel list)
        {
            Border b = new Border();
            b.Height = 1;
            b.Margin = new Thickness(10, 5, 10, 5);
            b.Background = new SolidColorBrush(Color.FromArgb(34, 255, 255, 255));
            list.Children.Add(b);
        }
        /// <summary>在面板内画一个菜单卡片，返回内容容器。</summary>
        private StackPanel BeginPanelMenu(double width)
        {
            ClosePanelMenu();
            _menuOpen = true;
            _menuLayer = new Canvas();
            _menuLayer.Width = _panelW;
            _menuLayer.Height = _panelH;
            Panel.SetZIndex(_menuLayer, 950);

            Rectangle catcher = new Rectangle();
            catcher.Width = _panelW;
            catcher.Height = _panelH;
            catcher.Fill = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0));
            catcher.MouseLeftButtonUp += delegate { ClosePanelMenu(); };
            _menuLayer.Children.Add(catcher);

            Border card = new Border();
            card.Width = width;
            card.CornerRadius = new CornerRadius(14);
            card.Background = new SolidColorBrush(Color.FromArgb(250, 22, 22, 30));
            card.BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
            card.BorderThickness = new Thickness(1);
            card.Padding = new Thickness(6);

            ScrollViewer sv = new ScrollViewer();
            sv.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            sv.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            sv.MaxHeight = 600;
            StackPanel list = new StackPanel();
            sv.Content = list;
            card.Child = sv;
            _menuLayer.Children.Add(card);

            Point mp = Mouse.GetPosition(this);
            double cardH = Math.Min(620, list.Children.Count * 33 + 30);
            double x = mp.X + 6;
            double y = mp.Y + 6;
            if (x + width > _panelW - 12) x = Math.Max(12, _panelW - width - 12);
            if (y + cardH > _panelH - 12) y = Math.Max(12, _panelH - cardH - 12);
            Canvas.SetLeft(card, x);
            Canvas.SetTop(card, y);

            _overlay.Children.Add(_menuLayer);
            return list;
        }

        private void ShowGroupMenu(GroupDef g)
        {
            StackPanel list = BeginPanelMenu(230);
            AddMenuItem(list, "重命名分类", delegate { RenameGroup(g); }, false);
            AddMenuItem(list, "删除分类", delegate { DeleteGroup(g); }, false);
        }

        private void ShowItemMenu(Item it, FrameworkElement anchor)
        {
            if (it == null) return;
            StackPanel list = BeginPanelMenu(250);

            AddMenuItem(list, "打开", delegate
            {
                if (it.IsFolder) OpenFolder(it); else Launch(it.Path);
            }, false);

            if (!it.IsFolder)
            {
                AddMenuItem(list, "以管理员身份运行", delegate { LaunchAdmin(it.Path); }, false);
            }

            AddMenuItem(list, "打开所在位置", delegate
            {
                try
                {
                    string target = it.IsFolder ? (it.Children.Count > 0 ? it.Children[0].Path : "") : it.Path;
                    if (string.IsNullOrEmpty(target)) return;
                    ProcessStartInfo psi = new ProcessStartInfo();
                    psi.FileName = "explorer.exe";
                    psi.Arguments = "/select,\"" + target + "\"";
                    psi.UseShellExecute = true;
                    Process.Start(psi);
                }
                catch { }
            }, false);

            if (!it.IsFolder)
            {
                AddMenuSeparator(list);
                AddMenuSection(list, "占位大小（1×1 普通）");
                int cur = it.Span <= 1 ? 1 : (it.Span >= 3 ? 3 : 2);
                for (int si = 1; si <= 3; si++)
                {
                    int sv2 = si;
                    AddMenuItem(list, si == 1 ? "1 × 1" : (si == 2 ? "2 × 2" : "3 × 3"), delegate { SetSpan(it, sv2); }, cur == si);
                }
            }
        }

        /// <summary>设置单个图标的占位大小。</summary>
        private void SetSpan(Item it, int span)
        {
            try
            {
                if (_cfg.Groups.Count > 0)
                {
                    // 分组视图下：只改显示尺寸（写回配置里同名项，若存在）
                    it.Span = span;
                }
                if (it.Cfg != null) it.Cfg.Span = span;
                else if (!string.IsNullOrEmpty(it.Path))
                {
                    foreach (ItemCfg ic in _cfg.Pins)
                        if (string.Equals(ic.Path, it.Path, StringComparison.OrdinalIgnoreCase)) { ic.Span = span; break; }
                }
                ConfigStore.Save(_cfg);
                RebuildTiles();
            }
            catch { }
        }
        /// <summary>底部死区装饰：粉色光带 + 曲枝樱花（枝条 / 花蕾 / 落瓣 / 星点）。</summary>
        private FrameworkElement BuildStrip()
        {
            double w = _panelW - 24;   // 铺满到包边内侧
            Canvas c = new Canvas();
            c.Width = w;
            c.Height = 46;
            c.IsHitTestVisible = false;

            // 光带
            Rectangle glow = new Rectangle();
            glow.Width = w;
            glow.Height = 44;
            LinearGradientBrush gb = new LinearGradientBrush();
            gb.StartPoint = new Point(0, 0);
            gb.EndPoint = new Point(0, 1);
            gb.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 168, 192), 0));
            gb.GradientStops.Add(new GradientStop(Color.FromArgb(58, 255, 168, 192), 0.5));
            gb.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 168, 192), 1));
            glow.Fill = gb;
            Canvas.SetTop(glow, 2);
            c.Children.Add(glow);

            SolidColorBrush pink = new SolidColorBrush(Color.FromArgb(235, 255, 200, 220));
            SolidColorBrush soft = new SolidColorBrush(Color.FromArgb(150, 255, 190, 210));
            SolidColorBrush core = new SolidColorBrush(Color.FromArgb(245, 255, 246, 210));
            SolidColorBrush dot = new SolidColorBrush(Color.FromArgb(170, 255, 220, 235));
            // 樱花（大小不一）
            double[] xs = new double[] { 0.10, 0.30, 0.50, 0.70, 0.88 };
            double[] rs = new double[] { 5.5, 7.5, 9.0, 7.0, 5.0 };
            for (int i = 0; i < xs.Length; i++)
            {
                double cx = w * xs[i];
                double cy = 23 + (i % 2 == 0 ? -4 : 4);
                for (int p = 0; p < 5; p++)
                {
                    Ellipse pet = new Ellipse();
                    pet.Width = rs[i] * 0.62;
                    pet.Height = rs[i] * 1.05;
                    pet.Stroke = pink;
                    pet.StrokeThickness = 1.0;
                    TransformGroup tg = new TransformGroup();
                    tg.Children.Add(new TranslateTransform(0, -rs[i] * 0.48));
                    tg.Children.Add(new RotateTransform(i * 14 + p * 72, 0, rs[i] * 0.48));
                    pet.RenderTransform = tg;
                    Canvas.SetLeft(pet, cx - pet.Width / 2);
                    Canvas.SetTop(pet, cy - rs[i] * 0.5);
                    c.Children.Add(pet);
                }
                Ellipse ec = new Ellipse();
                ec.Width = rs[i] * 0.3;
                ec.Height = rs[i] * 0.3;
                ec.Fill = core;
                Canvas.SetLeft(ec, cx - rs[i] * 0.15);
                Canvas.SetTop(ec, cy - rs[i] * 0.15);
                c.Children.Add(ec);
            }


            // 花蕾
            double[] buds = new double[] { 0.20, 0.40, 0.62, 0.79 };
            for (int i = 0; i < buds.Length; i++)
            {
                Ellipse bd = new Ellipse();
                bd.Width = 3.2;
                bd.Height = 5;
                bd.Stroke = soft;
                bd.StrokeThickness = 0.9;
                Canvas.SetLeft(bd, w * buds[i]);
                Canvas.SetTop(bd, 12 - (i % 2) * 3);
                c.Children.Add(bd);
            }

            // 落瓣 + 星点
            Random rnd = new Random(11);
            for (int i = 0; i < 12; i++)
            {
                Ellipse p2 = new Ellipse();
                p2.Width = 3;
                p2.Height = 4.4;
                p2.Stroke = soft;
                p2.StrokeThickness = 0.8;
                Canvas.SetLeft(p2, 20 + rnd.NextDouble() * (w - 40));
                Canvas.SetTop(p2, 6 + rnd.NextDouble() * 34);
                c.Children.Add(p2);
            }
            for (int i = 0; i < 34; i++)
            {
                Ellipse d2 = new Ellipse();
                d2.Width = 1.8;
                d2.Height = 1.8;
                d2.Fill = dot;
                Canvas.SetLeft(d2, 10 + rnd.NextDouble() * (w - 20));
                Canvas.SetTop(d2, 8 + rnd.NextDouble() * 32);
                c.Children.Add(d2);
            }
            return c;
        }
        private static bool IsStartup()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Run", false))
                    return k != null && k.GetValue("SideDrawer") != null;
            }
            catch { return false; }
        }

        private static Style _menuStyle;
        private static Style _menuItemStyle;
        private static Style _menuSepStyle;

        /// <summary>深色圆角菜单样式（用 XamlReader 定义模板，避免手写 ControlTemplate）。</summary>
        private static void BuildMenuStyles()
        {
            string menuXaml = @"<Style xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                                       xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
                TargetType=""ContextMenu"">
                <Setter Property=""Template"">
                    <Setter.Value>
                        <ControlTemplate TargetType=""ContextMenu"">
                            <Border Background=""#F2181822"" BorderBrush=""#40FFFFFF"" BorderThickness=""1""
                                    CornerRadius=""14"" Padding=""6"">
                                <Border.Effect>
                                    <DropShadowEffect Color=""#000000"" BlurRadius=""18"" ShadowDepth=""2"" Opacity=""0.55""/>
                                </Border.Effect>
                                <ItemsPresenter/>
                            </Border>
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
            </Style>";

            string itemXaml = @"<Style xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                                       xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
                TargetType=""MenuItem"">
                <Setter Property=""Foreground"" Value=""#F0FFFFFF""/>
                <Setter Property=""FontFamily"" Value=""Microsoft YaHei UI""/>
                <Setter Property=""FontSize"" Value=""12.5""/>
                <Setter Property=""Background"" Value=""Transparent""/>
                <Setter Property=""Template"">
                    <Setter.Value>
                        <ControlTemplate TargetType=""MenuItem"">
                            <Border x:Name=""bd"" CornerRadius=""9"" Padding=""12,7,26,8"" Background=""Transparent"">
                                <Grid>
                                    <Grid.ColumnDefinitions>
                                        <ColumnDefinition Width=""18""/>
                                        <ColumnDefinition Width=""*""/>
                                        <ColumnDefinition Width=""14""/>
                                    </Grid.ColumnDefinitions>
                                    <TextBlock x:Name=""chk"" Grid.Column=""0"" Text=""✓"" Foreground=""#FFA8C0""
                                               VerticalAlignment=""Center"" Visibility=""Collapsed""/>
                                    <ContentPresenter Grid.Column=""1"" ContentSource=""Header"" VerticalAlignment=""Center""/>
                                    <TextBlock x:Name=""arrow"" Grid.Column=""2"" Text=""›"" Foreground=""#B0FFFFFF""
                                               HorizontalAlignment=""Right"" VerticalAlignment=""Center""/>
                                    <Popup x:Name=""PART_Popup"" Placement=""Right"" IsOpen=""{TemplateBinding IsSubmenuOpen}""
                                           AllowsTransparency=""True"" Focusable=""False"" PopupAnimation=""Fade"">
                                        <Border Background=""#F2181822"" BorderBrush=""#40FFFFFF"" BorderThickness=""1""
                                                CornerRadius=""14"" Padding=""6"" Margin=""4,0,0,0"">
                                            <StackPanel IsItemsHost=""True"" KeyboardNavigation.DirectionalNavigation=""Cycle""/>
                                        </Border>
                                    </Popup>
                                </Grid>
                            </Border>
                            <ControlTemplate.Triggers>
                                <Trigger Property=""IsHighlighted"" Value=""True"">
                                    <Setter TargetName=""bd"" Property=""Background"" Value=""#33FFFFFF""/>
                                </Trigger>
                                <Trigger Property=""IsChecked"" Value=""True"">
                                    <Setter TargetName=""chk"" Property=""Visibility"" Value=""Visible""/>
                                </Trigger>
                                <Trigger Property=""HasItems"" Value=""False"">
                                    <Setter TargetName=""arrow"" Property=""Visibility"" Value=""Collapsed""/>
                                </Trigger>
                                <Trigger Property=""IsEnabled"" Value=""False"">
                                    <Setter Property=""Opacity"" Value=""0.4""/>
                                </Trigger>
                            </ControlTemplate.Triggers>
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
                <Style.Triggers>
                    <Trigger Property=""Role"" Value=""SubmenuHeader"">
                        <Setter Property=""FontWeight"" Value=""SemiBold""/>
                    </Trigger>
                </Style.Triggers>
            </Style>";

            string sepXaml = @"<Style xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
                                       xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
                TargetType=""Separator"">
                <Setter Property=""Template"">
                    <Setter.Value>
                        <ControlTemplate TargetType=""Separator"">
                            <Border Height=""1"" Background=""#22FFFFFF"" Margin=""10,5,10,5""/>
                        </ControlTemplate>
                    </Setter.Value>
                </Setter>
            </Style>";

            _menuStyle = (Style)System.Windows.Markup.XamlReader.Parse(menuXaml);
            _menuItemStyle = (Style)System.Windows.Markup.XamlReader.Parse(itemXaml);
            _menuSepStyle = (Style)System.Windows.Markup.XamlReader.Parse(sepXaml);
        }

        private ContextMenu MakeMenu()
        {
            try { if (_menuStyle == null) BuildMenuStyles(); } catch { }
            ContextMenu cm = new ContextMenu();
            if (_menuStyle != null) cm.Style = _menuStyle;
            if (_menuItemStyle != null) { cm.Resources[typeof(MenuItem)] = _menuItemStyle; }
            if (_menuSepStyle != null) { cm.Resources[typeof(Separator)] = _menuSepStyle; }
            cm.FontFamily = _ui;
            cm.FontSize = 12.5;

            // 保证菜单压在侧边栏之上（弹窗默认不是 TOPMOST）
            cm.Opened += delegate
            {
                try
                {
                    // 主窗口 TOPMOST 会把菜单压住：打开菜单时临时取消置顶，关闭后恢复
                    Native.SetWindowPos(new WindowInteropHelper(this).Handle, Native.HWND_NOTOPMOST, 0, 0, 0, 0,
                        Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
                }
                catch { }
            };
            cm.Closed += delegate
            {
                try { Native.KeepTopmost(new WindowInteropHelper(this).Handle); } catch { }
            };
            return cm;
        }

        // ------------------------------------------------------------ clock
        private void ApplyClockColor()
        {
            bool white = _cfg.ClockColor == 1;

            if (_clock != null) _clock.Foreground = new SolidColorBrush(white ? Color.FromArgb(255, 255, 255, 255) : Color.FromArgb(255, 0, 0, 0));
            if (_date != null) _date.Foreground = new SolidColorBrush(white ? Color.FromArgb(235, 255, 255, 255) : Color.FromArgb(255, 20, 20, 20));

            // 描边取反色：黑字配白描边、白字配黑描边，任何壁纸下都清晰
            ApplyClockOutline(white);
        }

        private void ApplyClockOutline(bool white)
        {
            // 细描边：黑字配一圈紧贴的白边，白字配一圈暗边 —— 任何背景都能读
            System.Windows.Media.Effects.DropShadowEffect fx = new System.Windows.Media.Effects.DropShadowEffect();
            fx.Color = white ? Color.FromRgb(0, 0, 0) : Color.FromRgb(255, 255, 255);
            fx.BlurRadius = white ? 1.6 : 1.0;
            fx.ShadowDepth = 0;
            fx.Opacity = white ? 0.9 : 1.0;
            if (_clock != null) _clock.Effect = fx;

            System.Windows.Media.Effects.DropShadowEffect fx2 = new System.Windows.Media.Effects.DropShadowEffect();
            fx2.Color = fx.Color;
            fx2.BlurRadius = white ? 1.3 : 0.9;
            fx2.ShadowDepth = 0;
            fx2.Opacity = fx.Opacity;
            if (_date != null) _date.Effect = fx2;
        }
        private void UpdateClock()
        {
            DateTime n = DateTime.Now;
            _clock.Text = n.ToString("HH:mm");
            string[] wd = new string[] { "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六" };
            _date.Text = string.Format("{0}月{1}日   {2}", n.Month, n.Day, wd[(int)n.DayOfWeek]);
        }

        // ------------------------------------------------------------ debug shot
        public void OpenInstant()
        {
            ApplyOpenBounds();
            UpdateLayout();
            RefreshGlass();
            if (Environment.GetEnvironmentVariable("SIDEDRAWER_NOGLASS") == "1")
                _glass.Background = new SolidColorBrush(Color.FromArgb(255, 12, 12, 16));
            _panelHost.CacheMode = new BitmapCache(1.0);   // 动画期间改用位图缓存，减少每帧重绘
            _shift.BeginAnimation(TranslateTransform.XProperty, null);
            _shift.X = 0;
            _overlay.Opacity = 1;
        }

        public void OpenFirstFolder()
        {
            foreach (Item it in BuildHomeItems())
                if (it.IsFolder) { OpenFolder(it); return; }
        }

        public void DumpLayout()
        {
            Program.Log("### folder tiles = " + _folderWrap.Children.Count);
            for (int i = 0; i < _folderWrap.Children.Count; i++)
            {
                FrameworkElement b = _folderWrap.Children[i] as FrameworkElement;
                if (b == null) continue;
                Item it = b.Tag as Item;
                Image im = FindImage(b);
                Program.Log(string.Format("{0,2} {1,-40} tile={2}x{3} img={4}",
                    i, it == null ? "?" : it.Name,
                    b.ActualWidth.ToString("0"), b.ActualHeight.ToString("0"),
                    im == null ? "none" : im.ActualWidth.ToString("0") + "x" + im.ActualHeight.ToString("0")));
            }
        }

        private static Image FindImage(DependencyObject root)
        {
            int n = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                DependencyObject c = VisualTreeHelper.GetChild(root, i);
                Image im = c as Image;
                if (im != null) return im;
                Image deep = FindImage(c);
                if (deep != null) return deep;
            }
            return null;
        }

        public void DumpSmall()
        {
            Stack<DependencyObject> stack = new Stack<DependencyObject>();
            stack.Push(_root);
            int found = 0;
            while (stack.Count > 0 && found < 20)
            {
                DependencyObject d = stack.Pop();
                int n = VisualTreeHelper.GetChildrenCount(d);
                for (int i = 0; i < n; i++)
                {
                    DependencyObject c = VisualTreeHelper.GetChild(d, i);
                    FrameworkElement fe = c as FrameworkElement;
                    if (fe is Image && fe.ActualWidth > 0 && fe.ActualWidth < 24)
                    {
                        Point sp = fe.PointToScreen(new Point(0, 0)); Program.Log("SMALL " + fe.ActualWidth.ToString("0.0") + "x" + fe.ActualHeight.ToString("0.0") + " at " + sp.X.ToString("0") + "," + sp.Y.ToString("0") + "  " + Chain(fe));
                        found++;
                    }
                    stack.Push(c);
                }
            }
        }

        private static string Chain(DependencyObject d)
        {
            string s = "";
            int guard = 0;
            while (d != null && guard++ < 12)
            {
                s = d.GetType().Name + " < " + s;
                d = VisualTreeHelper.GetParent(d);
            }
            return s;
        }

        public void DumpIcons()
        {
            foreach (Item it in BuildHomeItems())
            {
                string d = Describe(it);
                Program.Log(d);
                foreach (Item c in it.Children) Program.Log("   " + Describe(c));
            }
        }

        private static string Describe(Item it)
        {
            string s = it.Name + "  folder=" + it.IsFolder;
            BitmapSource bs = it.Icon as BitmapSource;
            if (bs != null) s += "  " + bs.PixelWidth + "x" + bs.PixelHeight + " dpi=" + bs.DpiX.ToString("0") + " fmt=" + bs.Format;
            else s += "  " + (it.Icon == null ? "null" : it.Icon.GetType().Name);
            return s;
        }

        public void SaveShot(string path)
        {
            UpdateLayout();
            int w = (int)Math.Ceiling(ActualWidth);
            int h = (int)Math.Ceiling(ActualHeight);
            if (w < 2 || h < 2) return;
            DrawingVisual dv = new DrawingVisual();
            using (DrawingContext dc = dv.RenderOpen()) DrawWallpaper(dc, w, h);
            RenderTargetBitmap rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            rtb.Render(_root);
            PngBitmapEncoder enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using (FileStream fs = File.Create(path)) enc.Save(fs);
        }

        private void DrawWallpaper(DrawingContext dc, int w, int h)
        {
            try
            {
                string wp = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Control Panel\Desktop", "WallPaper", "");
                if (!string.IsNullOrEmpty(wp) && File.Exists(wp))
                {
                    BitmapImage bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.DecodePixelWidth = 1400;
                    bi.UriSource = new Uri(wp);
                    bi.EndInit();
                    bi.Freeze();
                    dc.DrawImage(bi, new Rect(0, 0, w, h));
                    return;
                }
            }
            catch { }
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(24, 24, 30)), null, new Rect(0, 0, w, h));
        }

        // ------------------------------------------------------------ window hook
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr h = new WindowInteropHelper(this).Handle;
            int ex = Native.GetWindowLong(h, Native.GWL_EXSTYLE);
            Native.SetWindowLong(h, Native.GWL_EXSTYLE, ex | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE);

            HwndSource src = HwndSource.FromHwnd(h);
            if (src != null) src.AddHook(WndProc);
            Native.RegisterHotKey(h, 1, 3, 0x44);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // 窗口不激活，滚轮消息不会进来，这里手动处理标签窗口的滚动
            if (msg == 0x020A && _tagScroll != null && _tagPanel != null && _tagPanel.Visibility == Visibility.Visible)
            {
                try
                {
                    Point cpos = Mouse.GetPosition(_tagPanel);
                    if (cpos.X >= 0 && cpos.Y >= 0 && cpos.X <= _tagPanel.ActualWidth && cpos.Y <= _tagPanel.ActualHeight)
                    {
                        int delta = (int)((long)wParam >> 16);
                        double target = _tagScroll.VerticalOffset + (delta > 0 ? -60.0 : 60.0);
                        if (target < 0) target = 0;
                        if (target > _tagScroll.ScrollableHeight) target = _tagScroll.ScrollableHeight;
                        _tagScroll.ScrollToVerticalOffset(target);
                        handled = true;
                    }
                }
                catch { }
            }
            if (msg == Native.WM_HOTKEY)
            {
                Toggle();
                handled = true;
            }
            return IntPtr.Zero;
        }

        protected override void OnClosed(EventArgs e)
        {
            try { Native.UnregisterHotKey(new WindowInteropHelper(this).Handle, 1); }
            catch { }
            base.OnClosed(e);
        }
    }

    // ---------------------------------------------------------------- entry
    internal static class Program
    {
        public static readonly string LogPath = IOPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "log.txt");

        public static void Log(string msg)
        {
            try { File.AppendAllText(LogPath, DateTime.Now.ToString("HH:mm:ss.fff") + "  " + msg + Environment.NewLine, Encoding.UTF8); }
            catch { }
        }

        [STAThread]
        private static void Main(string[] args)
        {
            try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; } catch { }

            bool created;
            using (Mutex m = new Mutex(true, "SideDrawer.Local.SingleInstance", out created))
            {
                if (!created) return;
                if (args != null && args.Length >= 2 && args[0] == "--shot") { RunShot(args[1]); return; }
                Application app = new Application();
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                DrawerWindow win = new DrawerWindow();
                app.Run(win);
                GC.KeepAlive(m);
            }
        }

        private static void RunShot(string path)
        {
            Application app = new Application();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            DrawerWindow w = new DrawerWindow();
            app.Startup += delegate
            {
                w.Show();
                w.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(delegate
                {
                    try { w.OpenInstant(); if (path.IndexOf("folder", StringComparison.OrdinalIgnoreCase) >= 0) w.OpenFirstFolder(); if (path.IndexOf("info", StringComparison.OrdinalIgnoreCase) >= 0) w.DumpIcons(); }
                    catch (Exception ex) { Log("instant failed: " + ex); }
                    DispatcherTimer t = new DispatcherTimer();
                    t.Interval = TimeSpan.FromMilliseconds(650);
                    t.Tick += delegate
                    {
                        t.Stop();
                        try { if (path.IndexOf("info", StringComparison.OrdinalIgnoreCase) >= 0) w.DumpSmall(); } catch { }
                        try { w.SaveShot(path); Log("shot saved " + path); }
                        catch (Exception ex) { Log("shot failed: " + ex); }
                        app.Shutdown();
                    };
                    t.Start();
                }));
            };
            app.Run();
        }
    }

    /// <summary>简易输入框（WPF 没有内置 InputBox）。</summary>
    internal static class PromptWindow
    {
        public static string Show(Window owner, string title, string initial)
        {
            Window w = new Window();
            w.Title = title;
            w.Width = 360;
            w.Height = 168;
            w.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            if (owner != null) w.Owner = owner;
            w.Topmost = true;
            w.ResizeMode = ResizeMode.NoResize;
            w.ShowInTaskbar = false;
            w.WindowStyle = WindowStyle.ToolWindow;

            Grid g = new Grid();
            g.Margin = new Thickness(16);
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            TextBox box = new TextBox();
            box.Text = initial;
            box.FontSize = 14;
            box.Padding = new Thickness(6, 4, 6, 4);
            Grid.SetRow(box, 0);
            g.Children.Add(box);

            StackPanel btns = new StackPanel();
            btns.Orientation = Orientation.Horizontal;
            btns.HorizontalAlignment = HorizontalAlignment.Right;
            btns.VerticalAlignment = VerticalAlignment.Bottom;

            string result = null;
            Button ok = new Button();
            ok.Content = "确定";
            ok.Width = 80;
            ok.IsDefault = true;
            ok.Margin = new Thickness(0, 0, 8, 0);
            ok.Click += delegate { result = box.Text; try { w.DialogResult = true; } catch { } };
            Button cancel = new Button();
            cancel.Content = "取消";
            cancel.Width = 80;
            cancel.IsCancel = true;
            cancel.Click += delegate { try { w.DialogResult = false; } catch { } };
            btns.Children.Add(ok);
            btns.Children.Add(cancel);
            Grid.SetRow(btns, 1);
            g.Children.Add(btns);

            w.Content = g;
            w.Loaded += delegate { box.Focus(); box.SelectAll(); };
            bool? r = w.ShowDialog();
            return r == true ? result : null;
        }
    }}

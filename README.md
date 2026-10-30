# 侧边抽屉 SideDrawer

一个 Windows 桌面侧边抽屉：鼠标滑到屏幕左边缘呼出，半屏宽、液态玻璃质感，用来放桌面快捷方式、系统状态和音乐播放。

原生 **C# + WPF** 实现，**不依赖任何第三方库**（没有 NuGet、没有 NuGet 包、没有 XAML 文件），全部 UI 用代码构建，用系统自带的 `csc.exe` 直接编译。

![screenshot](docs/screenshot.png)

## 功能

- **侧边抽屉**：鼠标移到屏幕左边缘滑出书签，点击或 `Ctrl + Alt + D` 拉出面板；移开鼠标自动收回
- **图标网格**：自动扫描桌面快捷方式（当前用户桌面 + 公共桌面），按名称归类成 `日常 / 工具 / 启动器 / 游戏` 四组
- **分类胶囊**：Win10 磁贴式分组，可展开折叠、重命名、删除、新建；图标可以拖到分类标题上归组
- **手动添加**：右键面板 → 「添加图标」→ 选择文件即可加入指定分类（支持多选、同名去重）
- **系统状态**：CPU / GPU / 内存占用，CPU 型号从注册表读取真实名称
- **音乐面板**：通过 SMTC（Windows 媒体控制）读取系统正在播放的音乐，显示封面、歌名、歌词（lrclib / 网易云公开接口）
- **液态玻璃**：渐变底色 + 噪点 + 樱花线稿包边 + 暗角，支持自定义图标（`icons/名字.png`）
- **分辨率自适应**：性能卡片、分类胶囊、时钟、音乐面板按屏幕高度等比缩放
- **其他**：开机自启、时钟黑/白切换、图标大小（40/52/66/82）、分类与图标排序

## 构建

**环境要求**

- Windows 10 1809+ / Windows 11（64 位）
- .NET Framework 4.x（系统自带，无需安装）

**编译**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1
```

产物：`bin\SideDrawer.exe`（约 200 KB，单文件）

`build.ps1` 做的事情就是调用系统自带的编译器：

```
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
```

编译时会引用 WPF 程序集和 `C:\Windows\System32\WinMetadata\*.winmd`（用于 WinRT：SMTC、屏幕捕获等）。

## 使用

1. 双击 `SideDrawer.exe`（免安装、免管理员、不写注册表）
2. 鼠标移到屏幕**最左边中间**，滑出书签 → 点击，或按 `Ctrl + Alt + D`
3. 右键面板空白处打开设置菜单

首次运行会在 exe 同目录生成 `drawer.json`（配置文件），可直接编辑或通过右键菜单修改。

## 目录结构

```
Program.cs          主程序：窗口、面板、分类、菜单、拖拽、配置
MusicPanel.cs       音乐面板：SMTC 轮询、歌词、封面
Glass.cs            玻璃材质：截屏模糊、圆角遮罩、噪点、阴影
OrnateFrame.cs      樱花线稿包边（几何绘制）
EdgeTab.cs          左侧边缘触发的书签把手
ScreenUtil.cs       屏幕 / DPI / 工作区计算
InputBox.cs         输入框对话框（新建分类、重命名）
SpectrumPanel.cs    音频频谱（实验性，默认不启用）
SpectrumWindow.cs
build.ps1           编译脚本（csc）
app.manifest        DPI 感知声明
```

## 隐私说明

- 程序**不联网上传任何数据**；`drawer.json` 只保存在本地 exe 同目录
- 联网仅用于音乐面板：`lrclib.net`（歌词）、`music.163.com`（歌词兜底），只发送歌曲名/歌手
- 仓库中**不含**任何 API Key、Token 或个人信息；`drawer.json`、`log.txt` 已加入 `.gitignore`

## 已知限制

- 玻璃背景是**截屏 + 高斯模糊**（打开面板时抓一次），不是实时模糊。WPF 无法直接使用 Win11 的 Acrylic/Mica：实测 `DWMWA_SYSTEMBACKDROP_TYPE` 在 WPF 窗口上渲染为黑色（需要 `WS_EX_NOREDIRECTIONBITMAP` + DirectComposition），详见 [docs/玻璃背景与变色分析.md](docs/玻璃背景与变色分析.md)
- 半透明玻璃会透出壁纸，所以面板颜色会随壁纸变化，这是设计使然

## License

MIT，见 [LICENSE](LICENSE)

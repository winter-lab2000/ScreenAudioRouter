# Screen Audio Router

**按显示器切换 Windows 默认扬声器 / 麦克风。**

多屏时，窗口拖到哪个屏幕，就自动把系统默认播放设备、通信麦克风切到该屏幕绑定的音响 / 话筒。

| | |
|--|--|
| 平台 | Windows 10 / 11（x64） |
| 运行时 | .NET 8 Desktop Runtime（或使用自包含发布包，无需另装） |
| 界面 | 系统托盘常驻 + 主面板（macOS 风格） |
| 协议 | MIT |

---

## 功能一览

- 枚举显示器与音频设备，按屏绑定「输出 + 输入」
- 窗口 / 播放进程在屏幕间移动时，自动切换默认设备
- 显示器显示系统识别名（如 `Mi monitor`、`XGIMI TV`），并按名称自动匹配 HDMI 音频
- 独立「设置」页：路由开关、麦克风、开机自启、轮询间隔
- 托盘菜单：开面板、暂停/继续、开机自启、退出
- 配置与日志落在用户目录，便于排查

---

## 快速开始（使用）

### 方式 A：从 GitHub Releases 下载（推荐）

1. 打开本仓库 **[Releases](../../releases)** 页  
2. 下载最新的 `ScreenAudioRouter-vX.Y.Z-win-x64.zip`  
3. 解压到任意目录（例如 `D:\Tools\ScreenAudioRouter`）  
4. 双击 `ScreenAudioRouter.exe`  
5. 按下面「如何配置」绑定设备  

> 选择 **self-contained** 包则无需安装 .NET；若下载 **framework-dependent** 包，需先安装 [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)。

### 方式 B：使用安装脚本

```powershell
# 先解压 Release 包，或自行 publish（见「从源码构建」）
cd ScreenAudioRouter\installer

# 交互式选择安装目录（默认 %LOCALAPPDATA%\Programs\ScreenAudioRouter）
powershell -ExecutionPolicy Bypass -File .\install.ps1

# 指定路径 + 桌面快捷方式 + 立即启动
powershell -ExecutionPolicy Bypass -File .\install.ps1 `
  -InstallDir "D:\Tools\ScreenAudioRouter" -DesktopShortcut -StartApp
```

| 参数 | 默认 | 说明 |
|------|------|------|
| `-InstallDir` | `%LOCALAPPDATA%\Programs\ScreenAudioRouter` | 安装位置（可任意路径） |
| `-DesktopShortcut` | **不创建** | 创建桌面快捷方式 |
| `-StartupShortcut` | **不创建** | 创建登录启动项 |
| `-StartApp` | 否 | 安装后立即运行 |
| `-Silent` | 否 | 不提示路径 |

**默认不会创建桌面图标**，只写入开始菜单。桌面图标需显式加 `-DesktopShortcut`。

安装后可用安装目录下的 `uninstall.bat` 卸载。

### 方式 C：Inno Setup 安装包（可选）

已提供 `installer/ScreenAudioRouter.iss`。安装前需先：

1. 编译产物到 `src/ScreenAudioRouter/bin/publish/win-x64`（见源码构建）  
2. 本机安装 [Inno Setup 6](https://jrsoftware.org/isinfo.php)  
3. 编译：`iscc installer\ScreenAudioRouter.iss`  

向导中可自定义安装目录；「创建桌面快捷方式」**默认不勾选**。

---

## 如何配置（核心）

1. **启动程序** — 托盘出现图标；主面板左侧列出显示器。  
2. **选择显示器** — 点侧栏或拓扑图中的屏。  
3. **绑定设备**  
   - 输出：选该屏对应的扬声器 / HDMI 音频 / 耳机  
   - 输入：选该屏对应的麦克风（可不绑）  
4. **保存映射** — 点「保存映射」。  
5. **打开总开关** — 右上角绿色开关。  
6. **验证** — 播放视频的窗口拖到另一块屏，声音应改从该屏绑定设备输出。

### 推荐绑定示例

| 显示器 | 输出示例 | 输入示例 |
|--------|----------|----------|
| 电脑主屏 | 扬声器 (Redmi 电脑音箱) | 麦克风 (Redmi 电脑音箱) |
| 电视 / 第二显示器 | XGIMI TV (NVIDIA High Definition Audio) | 可不绑，或 USB 麦 |

点「刷新设备」时，程序会按显示器硬件名尝试自动绑定未分配 / 重复绑定的 HDMI 音频。

### 设置页

侧栏 **设置**：

| 选项 | 作用 |
|------|------|
| 自动切换默认扬声器 | 窗口移动时切换系统默认播放设备 |
| 自动切换默认麦克风 | 按前台窗口所在屏切换默认通信麦克风 |
| 窗口移动时切换默认输出设备 | 关闭则只写日志，不改系统默认 |
| 轮询间隔 | 100–1000 ms，越小越灵敏 |
| 开机自动启动 | 写入 `HKCU\...\Run`，无需管理员 |

### 托盘

| 操作 | 行为 |
|------|------|
| 右键托盘图标 | 打开信息菜单 |
| 点击菜单外任意处 | 关闭菜单 |
| 左键 / 双击托盘 | 打开主面板 |
| 主窗口红灯 / 黄灯 / 灰灯 | 关闭到托盘 / 最小化 / 最大化 |

### 配置与日志位置

```
%AppData%\ScreenAudioRouter\settings.json   # 映射与选项
%AppData%\ScreenAudioRouter\router.log      # 运行日志
```

主面板「设置 → 文件与日志」可一键打开。

---

## 工作原理（简）

```
枚举显示器 + 音频端点
        │
轮询：检测播放中进程 / 前台窗口所在屏
        │
查询该屏绑定的 output/input device id
        │
IPolicyConfig.SetDefaultEndpoint 切换系统默认设备
```

**麦克风限制：** Windows 无公开的「按进程切换采集设备」API。本工具将系统**默认通信麦克风**切到当前屏绑定设备。若 Zoom / Teams / Discord 在应用内锁定了麦克风，系统切换不会生效。

**输出限制：** 切换的是**系统默认播放设备**（跟随播放中/前台窗口所在屏），不是把多个进程同时绑到不同设备并行出声。

**同一设备绑两块屏：** 拖动窗口时听感不会变化。请为每块屏绑定**不同**输出（例如各屏的 HDMI 音频）。

---

## 从源码构建

### 环境

- Windows 10/11 x64  
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)（含 Windows Desktop）  
- 本仓库目录：`ScreenAudioRouter/`

```powershell
# 1) 编译 Debug
powershell -ExecutionPolicy Bypass -File .\build\build.ps1

# 运行
.\src\ScreenAudioRouter\bin\Debug\net8.0-windows\ScreenAudioRouter.exe

# 2) 发布 Release（用于打 GitHub Release 包）
powershell -ExecutionPolicy Bypass -File .\build\publish.ps1
powershell -ExecutionPolicy Bypass -File .\build\pack-release.ps1
```

若本机 `dotnet restore` 报 NuGet 错误，可使用仓库内离线资产脚本：

```powershell
powershell -ExecutionPolicy Bypass -File .\build\Write-OfflineAssets.ps1 `
  -ProjectDir .\src\ScreenAudioRouter -ProjectName ScreenAudioRouter `
  -Tfm net8.0-windows -WindowsDesktop
dotnet build .\src\ScreenAudioRouter\ScreenAudioRouter.csproj -c Release --no-restore
```

`build\pack-release.ps1` 会把 publish 产物打成：

```
dist\ScreenAudioRouter-v0.1.0-win-x64.zip
```

把该 zip 挂到 GitHub **Release** 即可，不要把 `bin/obj/dist` 提交进代码仓库。

---

## 仓库结构

```
ScreenAudioRouter/
├── README.md                 # 本文
├── LICENSE                   # MIT
├── .gitignore
├── ScreenAudioRouter.sln
├── src/ScreenAudioRouter/    # WPF 源码
│   ├── Interop/              # Core Audio / 显示器 / 窗口
│   ├── Services/             # 路由引擎、设备、启动项
│   ├── Models/
│   ├── Themes/               # 设计 token、托盘菜单
│   ├── Assets/               # 图标
│   └── MainWindow.xaml(.cs)
├── build/                    # 构建与打包脚本
├── installer/
│   ├── install.ps1           # 自定义路径安装（默认无桌面图标）
│   └── ScreenAudioRouter.iss # Inno Setup 脚本
├── docs/DESIGN.md            # UI 设计说明
└── dist/                     # 打包输出（不提交 git）
```

---

## GitHub 发布建议

| 内容 | 放哪里 |
|------|--------|
| 源码、脚本、README | 仓库默认分支（`main`） |
| `ScreenAudioRouter-v*-win-x64.zip` | **Releases** 附件 |
| `installer/Output/*-Setup.exe`（若有） | **Releases** 附件 |

**不要**提交：`bin/`、`obj/`、`dist/`、`*.log`、个人 `settings.json`。

---

## 常见问题

**Q: 拖了窗口声音没变？**  
A: 检查两块屏是否绑定了**不同**输出设备；总开关是否打开；日志中是否有 `Output → ...`。

**Q: 用了 Redmi / HDMI 后电视没声音？**  
A: 在第二屏的「输出」里选 `XXX (NVIDIA High Definition Audio)` 一类 HDMI 端点，保存后点「立即切换」试听。

**Q: 麦克风没跟着切？**  
A: 需在设置中打开「自动切换默认麦克风」；且目标应用未锁定输入设备。

**Q: 开机没启动？**  
A: 设置页勾选「开机自动启动」；检查 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 是否有 `ScreenAudioRouter`。

**Q: 杀毒软件提示？**  
A: 未签名的本地 exe 可能被提示，选择信任或自行编译；开源代码可审计。

---

## 上传 GitHub 时你需要准备什么

见仓库内 `docs/GITHUB_UPLOAD.md`。

## License

MIT — 详见 [LICENSE](LICENSE)。

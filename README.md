# Screen Audio Router

多显示器时，窗口在哪个屏，就把默认扬声器、麦克风切到那个屏对应的设备。

比如：浏览器拖到电视上，声音改从电视 HDMI 出；拖回主屏，再切回电脑音箱。

**系统：** Windows 10 / 11（64 位）  
**运行时：** 需要 [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)  
**协议：** MIT

---

## 安装

到 [Releases](https://github.com/winter-lab2000/ScreenAudioRouter/releases) 下载最新 zip：

**便携版**

1. 下载 `ScreenAudioRouter-vX.Y.Z-win-x64.zip`
2. 解压到任意目录
3. 运行 `ScreenAudioRouter.exe`

**安装版**

1. 下载 `ScreenAudioRouter-Installer-vX.Y.Z.zip`
2. 解压后运行 `install.bat`，按提示选安装路径
3. 需要桌面图标时，在目录下执行：

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1 -InstallDir "D:\Tools\ScreenAudioRouter" -DesktopShortcut
```

默认只加开始菜单，不自动创建桌面快捷方式。

---

## 怎么用

1. 打开程序，托盘里会出现图标。
2. 左侧选一块显示器。
3. 在右侧选这台屏要用的：
   - **输出**：音箱 / HDMI 音频 / 耳机
   - **输入**：麦克风（可以不选）
4. 点 **保存映射**。
5. 确认右上角总开关是打开的。
6. 把正在播放的窗口拖到另一块屏，听声音是否从对应设备出来。

**注意：** 两台显示器不要绑同一个输出设备，否则拖窗口也不会有变化。  
你有 HDMI 显示器 / 电视时，优先选类似 `XXX (NVIDIA High Definition Audio)` 的设备。

### 设置页

侧栏进入 **设置** 可以改：

- 是否自动切换扬声器、麦克风
- 是否跟随窗口切换默认输出
- 检测间隔（毫秒）
- 开机自动启动（写入当前用户注册表 Run 项，不用管理员）

### 托盘

- **右键**：菜单（点菜单外任意处可关闭）
- **左键**：打开主界面
- 菜单里也能暂停/继续路由、开关开机自启

### 配置文件

```
%AppData%\ScreenAudioRouter\settings.json
%AppData%\ScreenAudioRouter\router.log
```

---

## 已知限制

- **麦克风：** Windows 没有公开接口给「某个进程单独指定麦克风」。本程序切换的是系统**默认通信麦克风**。Zoom、Teams、Discord 若在软件里锁定了设备，系统切换无效。
- **输出：** 切换的是系统默认播放设备，跟着正在播放或前台窗口所在屏走；不是多个程序同时各走各的设备。
- 杀毒软件可能提示未签名 exe，可自行编译或加入信任。

---

## 从源码编译

需要 .NET 8 SDK（含 Windows Desktop）。

```powershell
dotnet build .\src\ScreenAudioRouter\ScreenAudioRouter.csproj -c Release --no-restore
```

运行：

```
.\src\ScreenAudioRouter\bin\Release\net8.0-windows\ScreenAudioRouter.exe
```

本机如果 `dotnet restore` 报错，仓库里有 `build\Write-OfflineAssets.ps1` 可以先生成离线还原文件再编译。

---

## 目录

```
src/ScreenAudioRouter/   源码
build/                   构建与打包脚本
installer/               install.ps1 / install.bat / Inno Setup 脚本
docs/                    设计与发布说明
```

---

## License

MIT，见 [LICENSE](LICENSE)。

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using ScreenAudioRouter.Models;
using ScreenAudioRouter.Services;
using ScreenAudioRouter.Themes;

namespace ScreenAudioRouter;

public partial class MainWindow : Window
{
    private readonly RoutingEngine _engine;
    private readonly LogService _log = App.Log;
    private AppSettings _settings = SettingsService.Load();

    private readonly ObservableCollection<MonitorItem> _monitorItems = [];
    private List<AudioDeviceInfo> _outputs = [];
    private List<AudioDeviceInfo> _inputs = [];
    private int _selectedMonitor;
    private bool _suppressEvents;
    private System.Windows.Forms.NotifyIcon? _tray;

    public MainWindow()
    {
        InitializeComponent();

        // Subtle continuous-corner feel on Win11
        try
        {
            var pref = Interop.NativeMethods.DWMWCP_ROUND;
            Interop.NativeMethods.DwmSetWindowAttribute(
                new System.Windows.Interop.WindowInteropHelper(this).EnsureHandle(),
                Interop.NativeMethods.DWMWA_WINDOW_CORNER_PREFERENCE,
                ref pref, sizeof(int));
        }
        catch { /* pre-Win11 */ }

        _engine = new RoutingEngine(_log);
        _engine.ApplySettings(_settings);

        MonitorList.ItemsSource = _monitorItems;
        MasterToggle.IsChecked = _settings.Enabled;
        OptPlayback.IsChecked = _settings.RoutePlayback;
        OptMic.IsChecked = _settings.RouteMicrophone;
        OptDefaultOutput.IsChecked = _settings.SwitchDefaultOutput;
        var startupOn = StartupService.IsEnabled();
        _settings.StartWithWindows = startupOn;
        OptStartup.IsChecked = startupOn;
        StartupHint.Text = startupOn
            ? "已注册：HKCU\\…\\Run\\ScreenAudioRouter"
            : "关闭时不写入注册表开机项";
        PollSlider.Value = _settings.PollIntervalMs;
        PollText.Text = $"{_settings.PollIntervalMs} ms";
        ConfigPathText.Text = $"配置：{AppSettings.ConfigPath}";
        LogPathText.Text = $"日志：{_log.LogFilePath}";
        ShowPage("routing");

        _engine.DevicesRefreshed += OnDevicesRefreshed;
        _engine.ForegroundChanged += OnForegroundChanged;
        _engine.StatusChanged += s => Dispatcher.Invoke(() => StatusText.Text = s);
        _log.LineAdded += line => Dispatcher.Invoke(() => AppendLog(line));

        Loaded += OnLoaded;
        Closing += (_, _) => { /* close goes to tray via button; shutdown via ExitApp */ };
        StateChanged += (_, _) =>
        {
            if (WindowState == WindowState.Minimized)
                HideToTray();
        };

        InitTray();
        _engine.Start();
        SettingsService.Save(_settings);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _engine.RefreshDevices(autoMap: true);
    }

    private void InitTray()
    {
        try
        {
            var icoPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "ScreenAudioRouter.ico");
            if (File.Exists(icoPath))
                Icon = new System.Windows.Media.Imaging.BitmapImage(new Uri(icoPath));
        }
        catch { }

        _tray = new System.Windows.Forms.NotifyIcon
        {
            Text = "Screen Audio Router · 按显示器切换音频",
            Icon = IconHelper.LoadTrayIcon(),
            Visible = true
        };
        // Custom WPF flyout on right-click; no stock WinForms menu.
        _tray.MouseClick += (_, args) =>
        {
            if (args.Button == System.Windows.Forms.MouseButtons.Right)
                Dispatcher.Invoke(ShowTrayMenu);
            else if (args.Button == System.Windows.Forms.MouseButtons.Left)
                Dispatcher.Invoke(ShowFromTray);
        };
        _tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowFromTray);
    }

    private TrayMenuWindow? _trayMenu;

    private void ShowTrayMenu()
    {
        _trayMenu?.Close();
        _trayMenu = new TrayMenuWindow(_engine, _settings, ShowFromTray, ExitApp);
        _trayMenu.Closed += (_, _) => _trayMenu = null;
        _trayMenu.ShowNearTray();
    }

    private void ShowFromTray()
    {
        _trayMenu?.Close();
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void HideToTray()
    {
        Hide();
        _tray?.ShowBalloonTip(1500, "Screen Audio Router", "仍在后台按显示器切换音频", System.Windows.Forms.ToolTipIcon.Info);
    }

    private void ExitApp()
    {
        _tray!.Visible = false;
        _tray.Dispose();
        _engine.Dispose();
        Application.Current.Shutdown();
    }

    private bool _refreshing;

    private void OnDevicesRefreshed(
        IReadOnlyList<MonitorInfo> monitors,
        IReadOnlyList<AudioDeviceInfo> outputs,
        IReadOnlyList<AudioDeviceInfo> inputs)
    {
        Dispatcher.Invoke(() =>
        {
            if (_refreshing) return;
            _refreshing = true;
            try
            {
                _outputs = outputs.ToList();
                _inputs = inputs.ToList();

                _monitorItems.Clear();
                foreach (var m in monitors)
                {
                    var map = _settings.Monitors.FirstOrDefault(x => x.MonitorIndex == m.Index);
                    _monitorItems.Add(new MonitorItem
                    {
                        MonitorIndex = m.Index,
                        DisplayName = m.DisplayName + (m.IsPrimary && !m.DisplayName.Contains("主") ? " · 主屏" : ""),
                        DeviceName = m.DeviceName,
                        HardwareName = m.HardwareName,
                        X = m.X,
                        Y = m.Y,
                        Width = m.Width,
                        Height = m.Height,
                        IsPrimary = m.IsPrimary,
                        ResolutionText = $"{m.Width}×{m.Height} · {m.DeviceName}",
                        OutputName = map?.OutputDeviceName ?? "",
                        InputName = map?.InputDeviceName ?? "",
                        OutputLabel = "🔊 " + (string.IsNullOrEmpty(map?.OutputDeviceName) ? "扬声器未绑定" : map.OutputDeviceName),
                        InputLabel = "🎙 " + (string.IsNullOrEmpty(map?.InputDeviceName) ? "麦克风未绑定" : map.InputDeviceName)
                    });
                }

                RebuildTopology(monitors);
                if (_monitorItems.Count > 0)
                {
                    if (_selectedMonitor >= _monitorItems.Count)
                        _selectedMonitor = 0;
                    SelectMonitor(_selectedMonitor);
                }
            }
            finally
            {
                _refreshing = false;
            }
        });
    }

    private void RebuildTopology(IReadOnlyList<MonitorInfo> monitors)
    {
        TopologyCanvas.Children.Clear();
        if (monitors.Count == 0)
        {
            TopologyCanvas.Children.Add(new TextBlock
            {
                Text = "未检测到显示器",
                Foreground = Design.InkSecondaryBrush,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            });
            return;
        }

        int minX = monitors.Min(m => m.X);
        int minY = monitors.Min(m => m.Y);
        int maxX = monitors.Max(m => m.X + m.Width);
        int maxY = monitors.Max(m => m.Y + m.Height);
        double worldW = Math.Max(maxX - minX, 1);
        double worldH = Math.Max(maxY - minY, 1);

        double canvasW = Math.Max(TopologyHost.ActualWidth - 32, 600);
        double canvasH = 160;
        double scale = Math.Min(canvasW / worldW, canvasH / worldH);
        double offsetLeft = (canvasW - worldW * scale) / 2;
        double offsetTop = (canvasH - worldH * scale) / 2;

        foreach (var m in monitors)
        {
            var map = _settings.Monitors.FirstOrDefault(x => x.MonitorIndex == m.Index);
            var w = Math.Max(m.Width * scale, 72);
            var h = Math.Max(m.Height * scale, 48);
            var left = offsetLeft + (m.X - minX) * scale;
            var top = offsetTop + (m.Y - minY) * scale;

            var card = new Border
            {
                Width = w,
                Height = h,
                Margin = new Thickness(left, top, 0, 0),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                VerticalAlignment = System.Windows.VerticalAlignment.Top,
                CornerRadius = new CornerRadius(10),
                Background = new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
                BorderBrush = m.Index == _selectedMonitor ? Design.AccentBrush : Design.CardStrokeBrush,
                BorderThickness = new Thickness(m.Index == _selectedMonitor ? 2 : 1),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = $"{m.DisplayName}\n{map?.OutputDeviceName ?? "输出未绑定"}\n{map?.InputDeviceName ?? "输入未绑定"}"
            };

            var sp = new StackPanel { Margin = new Thickness(8) };
            sp.Children.Add(new TextBlock
            {
                Text = m.DisplayName,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = Design.InkBrush,
                TextWrapping = TextWrapping.Wrap
            });
            sp.Children.Add(new TextBlock
            {
                Text = Truncate("🔊 " + (map?.OutputDeviceName ?? "扬声器未绑定"), 20),
                FontSize = 10,
                Foreground = Design.AccentBrush,
                TextWrapping = TextWrapping.NoWrap,
                ToolTip = map?.OutputDeviceName ?? "扬声器未绑定"
            });
            sp.Children.Add(new TextBlock
            {
                Text = Truncate("🎙 " + (map?.InputDeviceName ?? "麦克风未绑定"), 18),
                FontSize = 10,
                Foreground = Design.InkTertiaryBrush,
                TextWrapping = TextWrapping.NoWrap,
                ToolTip = map?.InputDeviceName ?? "麦克风未绑定"
            });

            card.Child = sp;
            var idx = m.Index;
            card.MouseLeftButtonUp += (_, _) => SelectMonitor(idx);
            TopologyCanvas.Children.Add(card);
        }
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)] + "…";

    private void SelectMonitor(int index)
    {
        _selectedMonitor = index;
        var item = _monitorItems.FirstOrDefault(m => m.MonitorIndex == index);
        if (item is null) return;

        CardTitle.Text = item.DisplayName + " 的设备";

        _suppressEvents = true;
        try
        {
            OutputCombo.ItemsSource = new[]
            {
                new DeviceChoice { Id = null, DisplayName = "（未指定）" }
            }.Concat(_outputs.Select(d => new DeviceChoice
            {
                Id = d.Id,
                DisplayName = d.Name + (d.IsDefault ? " · 当前默认" : "")
            })).ToList();

            InputCombo.ItemsSource = new[]
            {
                new DeviceChoice { Id = null, DisplayName = "（未指定）" }
            }.Concat(_inputs.Select(d => new DeviceChoice
            {
                Id = d.Id,
                DisplayName = d.Name + (d.IsDefaultCommunications ? " · 当前默认" : "")
            })).ToList();

            var map = _settings.Monitors.FirstOrDefault(m => m.MonitorIndex == index);
            OutputCombo.SelectedIndex = Math.Max(0, ((List<DeviceChoice>)OutputCombo.ItemsSource)
                .FindIndex(c => c.Id == map?.OutputDeviceId));
            InputCombo.SelectedIndex = Math.Max(0, ((List<DeviceChoice>)InputCombo.ItemsSource)
                .FindIndex(c => c.Id == map?.InputDeviceId));
        }
        finally
        {
            _suppressEvents = false;
        }
    }

    private void OnMonitorSelected(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: MonitorItem item })
        {
            ShowPage("routing");
            SelectMonitor(item.MonitorIndex);
        }
    }

    private void OnForceRoute(object sender, RoutedEventArgs e) => _engine.ForceRouteTo(_selectedMonitor);

    private void OnMappingChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents) return;
        // keep selection UI consistent; persist on Save
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var map = _settings.Monitors.FirstOrDefault(m => m.MonitorIndex == _selectedMonitor);
        if (map is null) return;

        if (OutputCombo.SelectedItem is DeviceChoice oc)
        {
            map.OutputDeviceId = oc.Id;
            map.OutputDeviceName = oc.Id is null ? null : oc.DisplayName.Replace(" · 当前默认", "");
        }
        if (InputCombo.SelectedItem is DeviceChoice ic)
        {
            map.InputDeviceId = ic.Id;
            map.InputDeviceName = ic.Id is null ? null : ic.DisplayName.Replace(" · 当前默认", "");
        }

        // Reset route cache so next tick re-applies
        _engine.ApplySettings(_settings);
        SettingsService.Save(_settings);
        _log.Info($"Saved mapping for monitor {_selectedMonitor + 1}: out={map.OutputDeviceName} in={map.InputDeviceName}");
        StatusText.Text = "映射已保存";
        _engine.RefreshDevices(autoMap: false);
        // Force re-route on next tick
        _engine.ForceRouteTo(_selectedMonitor);
    }

    private void OnRefresh(object sender, RoutedEventArgs e) => _engine.RefreshDevices(autoMap: true);

    private void OnOptionChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents) return;
        _settings.RoutePlayback = OptPlayback.IsChecked == true;
        _settings.RouteMicrophone = OptMic.IsChecked == true;
        _settings.SwitchDefaultOutput = OptDefaultOutput.IsChecked != false;
        _engine.ApplySettings(_settings);
        SettingsService.Save(_settings);
    }

    private void OnNavRouting(object sender, RoutedEventArgs e) => ShowPage("routing");
    private void OnNavSettings(object sender, RoutedEventArgs e) => ShowPage("settings");

    private void ShowPage(string page)
    {
        var routing = string.Equals(page, "routing", StringComparison.OrdinalIgnoreCase);
        PageRouting.Visibility = routing ? Visibility.Visible : Visibility.Collapsed;
        PageSettings.Visibility = routing ? Visibility.Collapsed : Visibility.Visible;
        PageTitle.Text = routing ? "路由" : "设置";

        SetNavSelected(NavRouting, routing);
        SetNavSelected(NavSettings, !routing);

        // Monitor list is only meaningful on routing page
        if (MonitorList.Parent is FrameworkElement parent)
            parent.Opacity = routing ? 1.0 : 0.45;
    }

    private static void SetNavSelected(Button button, bool selected)
    {
        if (button?.Template?.FindName("bd", button) is Border bd)
        {
            bd.Background = selected
                ? new SolidColorBrush(Color.FromArgb(255, 210, 210, 227))
                : System.Windows.Media.Brushes.Transparent;
        }
        if (button?.Content is TextBlock tb)
        {
            tb.Foreground = selected ? Design.AccentBrush : Design.InkBrush;
        }
    }

    private void OnStartupChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents || !IsLoaded) return;
        var want = OptStartup.IsChecked == true;
        var ok = StartupService.SetEnabled(want);
        var actual = StartupService.IsEnabled();
        _settings.StartWithWindows = actual;
        SettingsService.Save(_settings);
        _suppressEvents = true;
        OptStartup.IsChecked = actual;
        _suppressEvents = false;
        StartupHint.Text = actual
            ? "已注册：HKCU\\…\\Run\\ScreenAudioRouter"
            : (ok || !want ? "未注册开机启动" : "写入注册表失败，请检查权限");
        _log.Info($"Startup autostart={(actual ? "ON" : "OFF")} requested={want} ok={ok}");
        StatusText.Text = actual ? "开机自启：已开启" : "开机自启：已关闭";
    }

    private void OnPollChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        _settings.PollIntervalMs = (int)PollSlider.Value;
        PollText.Text = $"{_settings.PollIntervalMs} ms";
        _engine.ApplySettings(_settings);
        SettingsService.Save(_settings);
    }

    private void OnMasterToggle(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        _settings.Enabled = MasterToggle.IsChecked == true;
        if (_settings.Enabled) _engine.Start();
        else _engine.Stop();
        SettingsService.Save(_settings);
    }

    private void OnForegroundChanged(TrackedWindow? window)
    {
        Dispatcher.Invoke(() =>
        {
            FocusText.Text = window is null
                ? "前台：—"
                : $"前台：{window.ProcessName} → 显示器 {window.MonitorIndex + 1}";
        });
    }

    private void AppendLog(string line)
    {
        const int maxChars = 4000;
        if (LogBox.Text.Length > maxChars)
            LogBox.Text = LogBox.Text[^maxChars..];
        LogBox.AppendText(line + Environment.NewLine);
        LogBox.ScrollToEnd();
    }

    private void OnOpenLog(object sender, RoutedEventArgs e)
    {
        try
        {
            if (File.Exists(_log.LogFilePath))
                Process.Start(new ProcessStartInfo("notepad.exe", _log.LogFilePath) { UseShellExecute = false });
        }
        catch { }
    }

    private void OnOpenConfig(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(AppSettings.ConfigDirectory);
            Process.Start(new ProcessStartInfo
            {
                FileName = AppSettings.ConfigDirectory,
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => HideToTray();
    private void OnMinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        // Rounded corners look wrong when maximized
        try
        {
            if (Content is System.Windows.Controls.Border root)
            {
                root.CornerRadius = WindowState == WindowState.Maximized
                    ? new CornerRadius(0)
                    : new CornerRadius(12);
                root.BorderThickness = WindowState == WindowState.Maximized
                    ? new Thickness(0)
                    : new Thickness(1);
            }
        }
        catch { }
    }

    protected override void OnClosed(EventArgs e)
    {
        // Close button goes to tray; this runs on real shutdown
        base.OnClosed(e);
    }
}

public sealed class MonitorItem : INotifyPropertyChanged
{
    public int MonitorIndex { get; set; }
    public string DisplayName { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string HardwareName { get; set; } = "";
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public bool IsPrimary { get; set; }
    public string ResolutionText { get; set; } = "";
    public string OutputName { get; set; } = "";
    public string InputName { get; set; } = "";
    public string OutputLabel { get; set; } = "🔊 扬声器未绑定";
    public string InputLabel { get; set; } = "🎙 麦克风未绑定";

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class DeviceChoice
{
    public string? Id { get; set; }
    public string DisplayName { get; set; } = "";
}

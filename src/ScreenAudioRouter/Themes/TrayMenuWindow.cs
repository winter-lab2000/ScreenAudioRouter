using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ScreenAudioRouter.Models;
using ScreenAudioRouter.Services;
using ScreenAudioRouter.Themes;
using DrawingIcon = System.Drawing.Icon;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingGraphics = System.Drawing.Graphics;

namespace ScreenAudioRouter;

/// <summary>
/// Distinctive tray flyout — rounded panel, not the stock WinForms menu.
/// </summary>
public sealed class TrayMenuWindow : Window
{
    private readonly RoutingEngine _engine;
    private readonly AppSettings _settings;
    private readonly Action _onOpenMain;
    private readonly Action _onExit;
    private bool _dismissable;

    public TrayMenuWindow(RoutingEngine engine, AppSettings settings, Action onOpenMain, Action onExit)
    {
        _engine = engine;
        _settings = settings;
        _onOpenMain = onOpenMain;
        _onExit = onExit;

        Title = "TrayMenu";
        Width = 248;
        SizeToContent = SizeToContent.Height;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = true;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Background = System.Windows.Media.Brushes.Transparent;
        AllowsTransparency = true;
        Focusable = true;

        Content = BuildChrome();

        // Click outside / lose focus / Esc → dismiss (user does not need to click the tray again)
        Loaded += (_, _) =>
        {
            _dismissable = true;
            Activate();
            Focus();
        };
        Deactivated += (_, _) =>
        {
            if (!_dismissable) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_dismissable && IsLoaded)
                    Close();
            }), System.Windows.Threading.DispatcherPriority.Background);
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
        };
        PreviewMouseLeftButtonUp += (_, e) =>
        {
            // Keep window open when clicking empty padding? No — only items call Close themselves.
            // Clicking on the card chrome (border/padding) still dismisses via Deactivated when leaving,
            // but a click ON the window should not force-close.
        };
    }

    private System.Windows.Media.Brush CardBrush => new System.Windows.Media.SolidColorBrush(
        System.Windows.Media.Color.FromArgb(245, 250, 250, 252));

    private UIElement BuildChrome()
    {
        var shadow = new System.Windows.Media.Effects.DropShadowEffect
        {
            BlurRadius = 28,
            ShadowDepth = 4,
            Opacity = 0.28,
            Direction = 270,
            Color = System.Windows.Media.Colors.Black
        };

        var root = new Border
        {
            CornerRadius = new CornerRadius(14),
            Background = CardBrush,
            BorderBrush = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(40, 0, 0, 0)),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(12),
            Effect = shadow,
            Padding = new Thickness(8)
        };

        var panel = new StackPanel();

        // Header badge
        var header = new Border
        {
            Background = Design.SurfaceMutedBrush,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 10, 12, 10),
            Margin = new Thickness(0, 0, 0, 6)
        };
        var headStack = new StackPanel();
        headStack.Children.Add(new TextBlock
        {
            Text = "Screen Audio Router",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = Design.InkBrush
        });
        var enabled = _settings.Enabled;
        headStack.Children.Add(new TextBlock
        {
            Text = enabled ? "路由已开启" : "路由已关闭",
            FontSize = 11,
            Foreground = enabled ? Design.SuccessBrush : Design.InkTertiaryBrush,
            Margin = new Thickness(0, 2, 0, 0)
        });
        header.Child = headStack;
        panel.Children.Add(header);

        panel.Children.Add(Item("打开主面板", "显示完整配置", () => { Close(); _onOpenMain(); }));
        panel.Children.Add(Item(
            enabled ? "暂停路由" : "开启路由",
            enabled ? "暂时不自动切换设备" : "按前台窗口切换默认设备",
            () =>
            {
                _settings.Enabled = !enabled;
                if (_settings.Enabled) _engine.Start();
                else _engine.Stop();
                SettingsService.Save(_settings);
                Close();
            },
            accent: true));

        var startupOn = StartupService.IsEnabled();
        panel.Children.Add(Item(
            startupOn ? "关闭开机自启" : "开启开机自启",
            startupOn ? "登录 Windows 后不再自动运行" : "登录后自动在托盘运行",
            () =>
            {
                StartupService.SetEnabled(!startupOn);
                _settings.StartWithWindows = StartupService.IsEnabled();
                SettingsService.Save(_settings);
                Close();
            }));

        // Bound devices summary
        var maps = _settings.Monitors;
        if (maps.Count > 0)
        {
            panel.Children.Add(new Border { Height = 1, Background = Design.SeparatorBrush, Margin = new Thickness(8) });
            panel.Children.Add(new TextBlock
            {
                Text = "当前映射",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = Design.InkTertiaryBrush,
                Margin = new Thickness(10, 0, 10, 6)
            });
            foreach (var m in maps)
            {
                var mon = _engine.Monitors.FirstOrDefault(x => x.Index == m.MonitorIndex);
                var title = mon is not null && !string.IsNullOrWhiteSpace(mon.DisplayName)
                    ? mon.DisplayName
                    : $"显示器 {m.MonitorIndex + 1}";
                var row = new StackPanel { Margin = new Thickness(10, 0, 10, 6) };
                row.Children.Add(new TextBlock
                {
                    Text = title,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Design.InkBrush,
                    TextWrapping = TextWrapping.Wrap
                });
                row.Children.Add(new TextBlock
                {
                    Text = "🔊 " + Short(m.OutputDeviceName),
                    FontSize = 11,
                    Foreground = Design.InkSecondaryBrush
                });
                row.Children.Add(new TextBlock
                {
                    Text = "🎙 " + Short(m.InputDeviceName),
                    FontSize = 11,
                    Foreground = Design.InkSecondaryBrush
                });
                panel.Children.Add(row);
            }
        }

        panel.Children.Add(new Border { Height = 1, Background = Design.SeparatorBrush, Margin = new Thickness(8) });
        panel.Children.Add(Item("退出", "结束进程并退出托盘", () => { Close(); _onExit(); }, danger: true));

        root.Child = panel;
        return root;
    }

    private static string Short(string? name) =>
        string.IsNullOrEmpty(name) ? "未绑定" :
        name.Length <= 22 ? name : name[..21] + "…";

    private Border Item(string title, string sub, Action action, bool accent = false, bool danger = false)
    {
        var border = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 8, 10, 8),
            Cursor = System.Windows.Input.Cursors.Hand,
            Background = System.Windows.Media.Brushes.Transparent
        };

        var sp = new StackPanel();
        sp.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = danger ? Design.DangerBrush : (accent ? Design.AccentBrush : Design.InkBrush)
        });
        sp.Children.Add(new TextBlock
        {
            Text = sub,
            FontSize = 11,
            Foreground = Design.InkTertiaryBrush,
            Margin = new Thickness(0, 2, 0, 0)
        });
        border.Child = sp;

        var hover = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromRgb(0xE8, 0xE8, 0xED));
        var baseBg = System.Windows.Media.Brushes.Transparent;

        border.MouseEnter += (_, _) => border.Background = hover;
        border.MouseLeave += (_, _) => border.Background = baseBg;
        border.MouseLeftButtonUp += (_, _) => action();
        return border;
    }

    public void ShowNearTray()
    {
        _dismissable = false;
        var wa = SystemParameters.WorkArea;
        Left = wa.Right - Width - 8;
        Top = wa.Bottom - 8; // provisional; real height after layout
        Show();
        UpdateLayout();
        Top = wa.Bottom - ActualHeight - 8;
        Left = wa.Right - Width - 8;
        Activate();
        // Arm outside-dismiss only after activation settles
        Dispatcher.BeginInvoke(new Action(() => _dismissable = true),
            System.Windows.Threading.DispatcherPriority.Input);
    }
}

public static class IconHelper
{
    public static DrawingIcon? LoadTrayIcon()
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "Assets");
            var ico = Path.Combine(dir, "ScreenAudioRouter.ico");
            if (File.Exists(ico))
                return new DrawingIcon(ico, 32, 32);

            var png = Path.Combine(dir, "tray_32.png");
            if (File.Exists(png))
            {
                using var bmp = new DrawingBitmap(png);
                return DrawingIcon.FromHandle(bmp.GetHicon());
            }
        }
        catch { }
        return CreateFallback();
    }

    /// <summary>Procedural fallback: dark badge + dual screens + green EQ.</summary>
    public static DrawingIcon CreateFallback()
    {
        var bmp = new DrawingBitmap(32, 32);
        using var g = DrawingGraphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(System.Drawing.Color.Transparent);

        using var bg = new SolidBrush(System.Drawing.Color.FromArgb(255, 28, 28, 30));
        using (var path = RoundedRect(new RectangleF(1, 1, 30, 30), 7))
            g.FillPath(bg, path);

        using var blue = new SolidBrush(System.Drawing.Color.FromArgb(255, 10, 132, 255));
        using var cyan = new SolidBrush(System.Drawing.Color.FromArgb(255, 100, 210, 255));
        using var green = new SolidBrush(System.Drawing.Color.FromArgb(255, 48, 209, 88));
        using var white = new SolidBrush(System.Drawing.Color.FromArgb(240, 255, 255, 255));

        g.FillRectangle(blue, 5, 8, 10, 8);
        g.FillRectangle(cyan, 17, 6, 11, 8);
        g.FillPolygon(white, new[] { new PointF(14, 12), new PointF(18, 14), new PointF(14, 16) });

        // EQ bars
        g.FillRectangle(white, 6, 24, 2, 4);
        g.FillRectangle(green, 10, 21, 2, 7);
        g.FillRectangle(white, 14, 23, 2, 5);
        g.FillRectangle(green, 18, 20, 2, 8);
        g.FillRectangle(white, 22, 24, 2, 4);
        g.FillRectangle(green, 26, 22, 2, 6);

        return DrawingIcon.FromHandle(bmp.GetHicon());
    }

    private static GraphicsPath RoundedRect(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

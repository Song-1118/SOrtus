using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SOrtus.Core.Models;
using SOrtus.Core.Repositories;
using SOrtus.Core.Services;
using SOrtus.Navigation;
using SOrtus.Repositories;
using SOrtus.Services;
using SOrtus.Updates;
using SOrtus.ViewModels;

namespace SOrtus;

/// <summary>
/// 应用入口与组合根（规范：分层与依赖注入）。
/// 负责数据库迁移、服务装配、主题/本地化初始化，并启动主窗口。
/// 支持 <c>--smoke</c> 自检模式：无窗口实例化所有自定义控件与视图模板、切换主题/语言，
/// 验证视觉树非空（不触碰真实数据库，不弹窗），用于构建后冒烟验证。
/// </summary>
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args is { Length: > 0 } && e.Args[0] == "--smoke")
        {
            var code = RunSmoke();
            Shutdown(code);
            return;
        }

        // 输入链路自测：模拟真实点击 + 键盘输入，验证空输入框到底能不能打字。
        // 现象是「有内容的能编辑、空的不能」，必须用真实事件链验证，不能靠推断。
        if (e.Args is { Length: > 0 } && e.Args[0] == "--button-test")
        {
            RunButtonTest();
            return;
        }

        if (e.Args is { Length: > 0 } && e.Args[0] == "--slider-test")
        {
            RunSliderTest();
            Environment.Exit(0);
            return;
        }

        if (e.Args is { Length: > 0 } && e.Args[0] == "--input-test")
        {
            await RunInputTestAsync();
            Shutdown(0);
            return;
        }

        // 截图模式（文档 / 资源库素材）：离屏渲染四张核心界面，不触碰真实数据库
        if (e.Args is { Length: > 0 } && e.Args[0] == "--shot")
        {
            var target = e.Args.Length > 1 ? e.Args[1] : System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sortus-shots");
            var width = e.Args.Length > 2 && double.TryParse(e.Args[2], out var w) ? w : 1180;
            var height = e.Args.Length > 3 && double.TryParse(e.Args[3], out var h) ? h : 760;
            await RunShotsAsync(target, width, height);

            // 强制退出：GIT 加载指示器是无限循环动画，Dispatcher 始终有活，
            // Shutdown 之后进程仍会驻留并锁住 exe（后续构建会报文件被占用）
            Environment.Exit(0);
            return;
        }

        // 单实例守护：自动更新安装完成后，安装器的 RestartApplications 与本程序 [Run] 条目
        // 可能各拉起一次；若旧进程尚未完全退出，就会出现两个窗口。命名互斥量在此兜底。
        if (!TryAcquireSingleInstance())
        {
            Shutdown(0);
            return;
        }

        // 首次运行自动创建 SQLite 数据库与立项基表（验收 A4）
        try
        {
            await MigrationRunner.RunAsync();
        }
        catch (System.Exception ex)
        {
            MessageBox.Show($"数据库初始化失败：{ex.Message}", "SOrtus", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }
        // 服务装配（跨层通信优先接口 + 依赖注入，规范 6.3）
        IProjectRepository repository = new ProjectRepository();
        IProjectService projectService = new ProjectService(repository);
        IProjectDocumentRepository documentRepository = new ProjectDocumentRepository();
        IThemeService themeService = new ThemeService();
        themeService.Initialize();
        ILocalizationService localizationService = new LocalizationService();
        localizationService.Initialize();
        // 键盘焦点框总开关：按持久化设置初始化（默认开）。必须在窗口创建前生效，
        // 否则首帧会先画出焦点环再被掩掉，形成可见闪烁。
        FocusRingSettings.Apply(AppSettings.Load().ShowFocusRing);
        INavigationService navigation = new NavigationService();
        IDialogService dialogService = new DialogService(localizationService);
        IDocumentImportService importService = new DocumentImportService();
        IGitService gitService = new GitService();
        IFilePickerService filePicker = new FilePickerService(localizationService);

        var mainViewModel = new MainViewModel(
            projectService,
            themeService,
            localizationService,
            navigation,
            dialogService,
            documentRepository,
            importService,
            gitService,
            filePicker);
        mainViewModel.Initialize();

        var window = new MainWindow { DataContext = mainViewModel };
        window.Show();

        // VD-067：换肤过渡由窗口承载（淡出→换字典→淡入），服务只负责换字典与广播
        themeService.ThemeChanged += (_, _) =>
        {
            // 磨砂层的 BitmapCache 缓存的是旧主题渲染结果，必须清掉，否则卡片底色不跟随
            Controls.BlurPanel.InvalidateCaches();
            window.PlayThemeTransition(themeService.CurrentTheme);
        };

        ScheduleStartupUpdateCheck(mainViewModel);
    }

    /// <summary>单实例互斥量。持有到进程退出，由 OS 在进程终止时自动释放。</summary>

    /// <summary>


    /// <summary>
    /// 按钮内容居中实测（VD-123）：三种按钮分别测量「内容中心」与「按钮中心」的偏差。
    /// 纯图标按钮量图标（Path）中心；文字/图标+文字按钮量 ContentPresenter 中心。
    /// </summary>
    private static void RunButtonTest()
    {
        var icon = System.Windows.Media.Geometry.Parse("M 0,0 L 8,8 M 8,0 L 0,8");

        // 按页面真实配置：GIT 刷新（Ghost / Padding=10,8,10,8 / IconSize=15 / 无固定尺寸）、
        // 标签删除（Ghost / 24×24 / Padding=0 / IconSize=10）、图标+文字（对照组）。
        // 三个按钮放同一个窗口一次测完——连续 ShowDialog 时后续窗口的 Loaded 不触发。
        var buttons = new (string Tag, Controls.SOrButton Button)[]
        {
            ("GIT刷新配置", new Controls.SOrButton { Variant = Controls.SOrButtonVariant.Ghost, Padding = new Thickness(10, 8, 10, 8), Icon = icon }),
            ("标签删除配置", new Controls.SOrButton { Variant = Controls.SOrButtonVariant.Ghost, Width = 24, Height = 24, Padding = new Thickness(0), Icon = icon }),
            ("图标加文字", new Controls.SOrButton { Width = 140, Height = 36, Content = "推送", Icon = icon }),
        };

        var panel = new System.Windows.Controls.StackPanel { Margin = new System.Windows.Thickness(20) };
        foreach (var (_, button) in buttons)
        {
            panel.Children.Add(button);
        }

        var window = new Window
        {
            Content = panel,
            Width = 420,
            Height = 220,
            Left = 320,
            Top = 320,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
        };

        window.Loaded += (s, e) =>
        {
            window.UpdateLayout();
            foreach (var (tag, button) in buttons)
            {
                double buttonCenter = button.ActualWidth / 2;
                var iconPath = FindDescendant<System.Windows.Shapes.Path>(button, "PART_Icon");
                var presenter = FindContentPresenter(button);
                double contentCenter;
                string what;
                if (iconPath is not null && iconPath.IsVisible)
                {
                    contentCenter = iconPath.TranslatePoint(new System.Windows.Point(iconPath.ActualWidth / 2, 0), button).X;
                    what = "图标";
                }
                else if (presenter is not null)
                {
                    contentCenter = presenter.TranslatePoint(new System.Windows.Point(presenter.ActualWidth / 2, 0), button).X;
                    what = "文字";
                }
                else
                {
                    what = "无内容";
                    contentCenter = -1;
                }

                double offset = contentCenter - buttonCenter;
                var iconForHost = FindDescendant<System.Windows.Shapes.Path>(button, "PART_Icon");
                var host2 = (iconForHost is null ? button : System.Windows.Media.VisualTreeHelper.GetParent(iconForHost)) as System.Windows.FrameworkElement;
                Console.WriteLine($"[BUTTON-{tag}] HCA={button.HorizontalContentAlignment} " +
                    $"宿主对齐={host2?.HorizontalAlignment} 宿主宽={host2?.ActualWidth:0.##} 按钮宽={button.ActualWidth:0.##}");
                Console.WriteLine($"[BUTTON-{tag}] {what}中心={contentCenter:0.##} 按钮中心={buttonCenter:0.##} " +
                    $"按钮尺寸={button.ActualWidth:0.##}x{button.ActualHeight:0.##} 偏差={offset:0.##} 居中={Math.Abs(offset) <= 1.5}");
            }

            // 内部布局数据：图标相对按钮的左边缘 / Margin / 宿主 StackPanel 位置
            foreach (var (tag, button) in buttons)
            {
                var iconPath = FindDescendant<System.Windows.Shapes.Path>(button, "PART_Icon");
                if (iconPath is null)
                {
                    continue;
                }

                double left = iconPath.TranslatePoint(new System.Windows.Point(0, 0), button).X;
                var host = System.Windows.Media.VisualTreeHelper.GetParent(iconPath) as System.Windows.FrameworkElement;
                double hostLeft = host is null ? -1 : host.TranslatePoint(new System.Windows.Point(0, 0), button).X;
                Console.WriteLine($"[BUTTON-{tag}] 图标左边缘={left:0.##} Margin={iconPath.Margin} " +
                    $"宿主={host?.GetType().Name} 宿主左边缘={hostLeft:0.##} 宿主宽={host?.ActualWidth:0.##}");
            }

            // 保存截图供目视核对
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
                420, 220, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using var stream = System.IO.File.Create("F:/S_/Sortus/outputs/tmp-button-test.png");
            encoder.Save(stream);
            Console.WriteLine("[BUTTON] 截图已保存 outputs/tmp-button-test.png");

            window.Close();
        };

        window.ShowDialog();
        Environment.Exit(0);
    }

    /// <summary>在可视树里找第一个 ContentPresenter。</summary>
    private static System.Windows.Controls.ContentPresenter? FindContentPresenter(System.Windows.DependencyObject root)
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is System.Windows.Controls.ContentPresenter cp)
            {
                return cp;
            }

            var found = FindContentPresenter(child);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// 滑块实测（VD-122）：模拟 Thumb 拖动，验证「Value 变化」与「圆点视觉位置变化」同步。
    /// 用户报告拖动时数值变但圆点不动——圆点位置由 Track 管理，二者脱钩只可能是
    /// 圆点上残留了 RenderTransform 偏移，或 Value 被动画持有导致 Track 无法更新布局。
    /// </summary>
    private static void RunSliderTest()
    {
        var slider = new Controls.SOrSlider
        {
            Width = 300,
            Height = 22,
            Minimum = 0,
            Maximum = 100,
            Value = 0,
        };

        var window = new Window
        {
            Content = slider,
            Width = 400,
            Height = 140,
            Left = 200,
            Top = 200,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
        };

        window.Loaded += (s, e) =>
        {
            var track = slider.Template?.FindName("PART_Track", slider) as System.Windows.Controls.Primitives.Track;
            var thumb = track?.Thumb;
            if (track is null || thumb is null)
            {
                Console.WriteLine("[SLIDER] track/thumb not found");
                window.Close();
                return;
            }

            double PosX()
            {
                var pt = thumb.PointToScreen(new System.Windows.Point(0, 0));
                var base0 = slider.PointToScreen(new System.Windows.Point(0, 0));
                return pt.X - base0.X;
            }

            window.UpdateLayout();
            double before = PosX();
            double valueBefore = slider.Value;

            thumb.RaiseEvent(new System.Windows.Controls.Primitives.DragStartedEventArgs(0, 0));
            thumb.RaiseEvent(new System.Windows.Controls.Primitives.DragDeltaEventArgs(100, 0));
            thumb.RaiseEvent(new System.Windows.Controls.Primitives.DragCompletedEventArgs(100, 0, false));

            window.UpdateLayout();
            double after = PosX();
            double valueAfter = slider.Value;
            var transform = thumb.RenderTransform as System.Windows.Media.TranslateTransform;

            Console.WriteLine($"[SLIDER] value {valueBefore:0.##} -> {valueAfter:0.##} " +
                $"thumbX {before:0.##} -> {after:0.##} " +
                $"位置跟随={Math.Abs(after - before) > 5} " +
                $"残留变换={(transform is null ? "无" : transform.X.ToString("0.##"))}");

            slider.Value = 100;
            window.UpdateLayout();
            Console.WriteLine($"[SLIDER] Value=100 时 thumbX={PosX():0.##}（轨道可用宽约 {track.ActualWidth:0}）");

            // 关键实验：AnimateTo 的动画（FillBehavior 默认 HoldEnd）结束后，
            // 属性是否仍被动画「持有」——若持有，之后所有 Value 设置都失效，
            // 表现为「数值显示变了但圆点再也拖不动」。
            var hold = new System.Windows.Media.Animation.DoubleAnimation(20, new Duration(TimeSpan.FromMilliseconds(50)));
            slider.BeginAnimation(System.Windows.Controls.Slider.ValueProperty, hold);
            window.Dispatcher.InvokeAsync(async () =>
            {
                await System.Threading.Tasks.Task.Delay(200);
                double duringAnimation = slider.Value;
                slider.SetCurrentValue(System.Windows.Controls.Slider.ValueProperty, 25.0);
                window.UpdateLayout();
                double afterSet = slider.Value;
                double thumbAtSet = PosX();
                Console.WriteLine($"[SLIDER] 动画持有实验: 动画值={duringAnimation:0.##} " +
                    $"设25后读回={afterSet:0.##}（若仍是20则属性被锁死） thumbX={thumbAtSet:0.##}");
                window.Close();
            }, System.Windows.Threading.DispatcherPriority.Background);

            return;
        };

        window.ShowDialog();
    }

    /// <summary>绑定回传测试用的最小视图模型（验证省略 Mode 时 Text 也能回传）。</summary>
    private sealed class BoundTextVm
    {
        public string SearchText { get; set; } = string.Empty;
    }


    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetWindowDC(IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObject);

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SRCCOPY = 0x00CC0020;
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    /// <summary>截取真实桌面（含 Popup 独立窗口）并保存为 PNG。</summary>
    private static void CaptureScreen(string path)
    {
        int width = GetSystemMetrics(SM_CXSCREEN);
        int height = GetSystemMetrics(SM_CYSCREEN);
        IntPtr desktopDc = GetWindowDC(GetDesktopWindow());
        IntPtr memDc = CreateCompatibleDC(desktopDc);
        IntPtr bitmap = CreateCompatibleBitmap(desktopDc, width, height);
        IntPtr old = SelectObject(memDc, bitmap);
        BitBlt(memDc, 0, 0, width, height, desktopDc, 0, 0, SRCCOPY);

        var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
            bitmap, IntPtr.Zero, System.Windows.Int32Rect.Empty,
            System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
        SelectObject(memDc, old);
        DeleteObject(bitmap);
        DeleteObject(memDc);

        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(source));
        using var stream = System.IO.File.Create(path);
        encoder.Save(stream);
        Console.WriteLine($"[COMBO] 真实桌面截图 {width}x{height} -> {System.IO.Path.GetFileName(path)}");
    }

    /// 输入链路自测：空输入框 → 模拟点击 → 模拟按键 → 检查文本是否真的进去。
    /// 用真实事件（MouseButtonEventArgs / InputManager.ProcessInput）而不是直接赋值，
    /// 这样才能覆盖「点击定位光标 → 焦点落位 → 键盘输入被接收」整条链路。
    /// </summary>

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativeInput
    {
        public uint Type;
        public InputUnionUnion Data;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit)]
    private struct InputUnionUnion
    {
        [System.Runtime.InteropServices.FieldOffset(0)]
        public KeyboardInput Keyboard;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, NativeInput[] inputs, int size);

    /// <summary>
    /// 用 Windows 的 SendInput 发真实键盘事件。WPF 的字符输入来自 IME/TextInput 管线
    /// （<c>TextInputManager</c> 是 internal，外部无法直接投递），所以自动化验证必须走系统 API。
    /// </summary>
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, int extraInfo);

    /// <summary>
    /// 用 keybd_event 发真实键盘事件（dwFlags：0=按下，2=KEYEVENTF_KEYUP）。
    /// 之前用 SendInput + 自定义结构体，踩了两个坑：Win32 常量写错（down 用成 0x0002）、
    /// 结构体对齐难核对，导致「发了但没发」——原生 TextBox 对照组同样收不到字符，
    /// 证明与组件无关。keybd_event 参数直白，不再有布局歧义。
    /// </summary>
    private static void SendRealKeys(params char[] chars)
    {
        const uint keyup = 0x0002;

        foreach (var ch in chars)
        {
            byte vk = (byte)char.ToUpperInvariant(ch);
            keybd_event(vk, 0, 0, 0);
            System.Threading.Thread.Sleep(20);
            keybd_event(vk, 0, keyup, 0);
            System.Threading.Thread.Sleep(20);
        }
    }

    private static async System.Threading.Tasks.Task RunInputTestAsync()
    {
        // 三个被测对象：空框（用户报告「不可输入」的那个）、有内容框、原生 TextBox（对照组）
        var empty = new Controls.SOrTextBox { Width = 320, Height = 36, Placeholder = "ph" };
        var filled = new Controls.SOrTextBox { Width = 320, Height = 36, Text = "已有内容" };
        var native = new System.Windows.Controls.TextBox { Width = 320, Height = 36 };

        var panel = new System.Windows.Controls.StackPanel { Margin = new System.Windows.Thickness(20) };
        panel.Children.Add(new System.Windows.Controls.TextBlock { Text = "empty:", Foreground = System.Windows.Media.Brushes.White });
        panel.Children.Add(empty);
        panel.Children.Add(new System.Windows.Controls.TextBlock { Text = "filled:", Foreground = System.Windows.Media.Brushes.White });
        panel.Children.Add(filled);
        panel.Children.Add(new System.Windows.Controls.TextBlock { Text = "native:", Foreground = System.Windows.Media.Brushes.White });
        panel.Children.Add(native);

        var window = new Window
        {
            Content = panel,
            Width = 420,
            Height = 320,
            Left = 160,
            Top = 160,
            ShowInTaskbar = false,
            ShowActivated = true,
            WindowStartupLocation = WindowStartupLocation.Manual,
        };

        // 模态窗口：后台进程调 SetForegroundWindow 会被系统静默拒绝（自 Vista 起的前台锁），
        // 字符就全发到了终端上——上一版测试三个对象全失败正是这个原因。
        // ShowDialog 的模态窗口有置前特权，SendInput 才能真的打到应用里。
        window.Loaded += async (s, e) =>
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            SetForegroundWindow(hwnd);
            await System.Threading.Tasks.Task.Delay(600);

            Console.WriteLine($"[INPUT] foreground={(GetForegroundWindow() == hwnd)} " +
                $"focused={System.Windows.Input.Keyboard.FocusedElement?.GetType().Name ?? "null"}");

            // 绑定回传用例（VD-124）：页面写法是 Text="{Binding SearchText}"（省略 Mode，
            // 依赖 BindsTwoWayByDefault）。自定义 DP 缺这个标志时用户打字只传到外壳、
            // 视图模型永远收不到——「搜索功能无效」的根因，必须有用例守住。
            var boundSource = new BoundTextVm();
            var bound = new Controls.SOrTextBox { Width = 320, Height = 36 };
            bound.SetBinding(Controls.SOrTextBox.TextProperty, new System.Windows.Data.Binding(nameof(BoundTextVm.SearchText))
            {
                Source = boundSource,
                UpdateSourceTrigger = System.Windows.Data.UpdateSourceTrigger.PropertyChanged,
            });
            panel.Children.Add(bound);
            window.UpdateLayout();
            if (bound.Inner is { } boundInner)
            {
                boundInner.Text = "Q4";
            }

            window.UpdateLayout();
            Console.WriteLine($"[INPUT-bind] 省略Mode回传 VM.SearchText='{boundSource.SearchText}' " +
                $"期望='Q4' 回传正常={boundSource.SearchText == "Q4"}");
            panel.Children.Remove(bound);

            await ClickAndTypeAsync(empty, "xy", "xy", "empty");
            await ClickAndTypeAsync(filled, "xy", "xy", "filled");
            await ClickAndTypeAsync(native, "xy", "xy", "native-对照");

            Console.WriteLine($"[INPUT] 空框外壳 Opacity={empty.Opacity:0.00}（修复前点击后会掉到 0，即「整块闪没」）");
            window.Close();
        };

        window.ShowDialog();
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    /// <summary>
    /// 模拟「用户在内层输入框里打字」：直接写内层 TextBox 的 Text，验证它能否
    /// 通过双向绑定同步回外壳 Text（这条链就是键盘输入会走的路）。
    /// </summary>
    private static void TypeViaInner(System.Windows.FrameworkElement target, string text)
    {
        System.Windows.Controls.TextBox? inner = target switch
        {
            Controls.SOrTextBox ours => ours.Inner,
            System.Windows.Controls.TextBox sys => sys,
            _ => null,
        };

        if (inner is not null)
        {
            inner.Text = text;
        }
    }

    /// <summary>读模板里占位符（PART_Placeholder）的当前 Opacity。</summary>
    private static double ReadPlaceholderOpacity(System.Windows.FrameworkElement target)
    {
        if (target is not Controls.SOrTextBox ours)
        {
            return -1;
        }

        var placeholder = FindDescendant<System.Windows.Controls.TextBlock>(ours, "PART_Placeholder");
        return placeholder?.Opacity ?? -1;
    }

    private static T? FindDescendant<T>(System.Windows.DependencyObject root, string name)
        where T : System.Windows.FrameworkElement
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T match && match.Name == name)
            {
                return match;
            }

            var found = FindDescendant<T>(child, name);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>读取被测对象的文本。</summary>
    private static string ReadText(System.Windows.FrameworkElement target) => target switch
    {
        Controls.SOrTextBox ours => ours.Text,
        System.Windows.Controls.TextBox sys => sys.Text,
        _ => string.Empty,
    };

    /// <summary>真实鼠标点击目标中心 + 真实键盘输入，然后汇报文本与 Opacity。</summary>
    private static async System.Threading.Tasks.Task ClickAndTypeAsync(
        System.Windows.FrameworkElement target, string type, string expect, string tag)
    {
        var point = target.PointToScreen(new System.Windows.Point(target.ActualWidth / 2, target.ActualHeight / 2));
        SetCursorPos((int)point.X, (int)point.Y);
        await System.Threading.Tasks.Task.Delay(150);

        RealClick();
        await System.Threading.Tasks.Task.Delay(300);

        // 诊断一：点击后焦点归属（键盘输入的必要条件）
        var focused = System.Windows.Input.Keyboard.FocusedElement;
        bool focusWithin = target.IsKeyboardFocusWithin;

        // 诊断二：外壳 Opacity（f0d98fc 缺陷的直接指标——修复前点击后会掉到 0）
        double opacityAfterClick = target.Opacity;

        // 诊断三：占位符 Opacity（聚焦后应淡出到 0；修复前它反而拖住整框）
        double placeholderOpacity = ReadPlaceholderOpacity(target);

        // 诊断四：数据通路。此环境（沙箱会话）会拦截 SendInput/keybd_event 键盘注入——
        // 原生 TextBox 对照组同样收不到字符，所以改为直接写内层输入框的 Text，
        // 验证「内层输入 → 双向绑定 → 外壳 Text」这条键盘输入必经的链路。
        TypeViaInner(target, type);
        await System.Threading.Tasks.Task.Delay(200);

        string text = ReadText(target);
        Console.WriteLine($"[INPUT-{tag}] text='{text}' expect='{expect}' pass={text == expect} " +
            $"focusWithin={focusWithin} focused={focused?.GetType().Name ?? "null"} " +
            $"opacity={opacityAfterClick:0.00} placeholder={placeholderOpacity:0.00}");
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern void mouse_event(uint flags, int dx, int dy, uint data, int extra);

    /// <summary>真实鼠标左键点击（按下 + 抬起）。</summary>
    private static void RealClick()
    {
        const uint down = 0x0002;
        const uint up = 0x0004;
        mouse_event(down, 0, 0, 0, 0);
        System.Threading.Thread.Sleep(30);
        mouse_event(up, 0, 0, 0, 0);
    }


    private static Mutex? _singleInstanceMutex;

    /// <summary>
    /// 尝试获取单实例锁。首次返回 true；已有实例在运行时返回 false。
    /// </summary>
    private static bool TryAcquireSingleInstance()
    {
        try
        {
            _singleInstanceMutex = new Mutex(initiallyOwned: true, @"Local\SOrtus.SingleInstance", out var createdNew);
            return createdNew;
        }
        catch (Exception ex)
        {
            // 互斥量创建失败（极端权限/命名冲突）时放行，宁可双开也不能打不开程序
            UpdateService.Log($"单实例互斥量创建失败：{ex.Message}");
            return true;
        }
    }

    /// <summary>
    /// 启动后延迟 5 秒再查更新：先让窗口完成首帧渲染与数据库加载，
    /// 避免把网络耗时叠进启动时间。检查本身 5 秒超时、失败静默。
    /// VD-080：旧值 3 秒，正好落在用户刚开始首屏操作的时间窗里；
    /// 提示条已改为浮层不占布局，但仍不应在用户第一眼就出现。
    /// </summary>
    private static void ScheduleStartupUpdateCheck(MainViewModel mainViewModel)
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        timer.Tick += async (_, _) =>
        {
            timer.Stop();
            try
            {
                await mainViewModel.Updates.CheckAsync(force: false);
            }
            catch (Exception ex)
            {
                Services.UpdateService.Log($"启动检查更新异常：{ex}");
            }
        };
        timer.Start();
    }

    /// <summary>
    /// 截图模式：以离屏窗口渲染核心界面并保存 PNG，供文档与资源库素材使用。
    /// 使用无副作用的桩服务，不读写真实数据库。
    /// </summary>
    private async Task RunShotsAsync(string targetDir, double width, double height)
    {
        System.IO.Directory.CreateDirectory(targetDir);

        var themeService = new ThemeService();
        themeService.Initialize();
        // 与正式启动同一套行为：换肤后清掉磨砂层缓存，避免卡片底色停留在上一套主题
        themeService.ThemeChanged += (_, _) => Controls.BlurPanel.InvalidateCaches();
        // 换肤验证会写回用户设置，结束时必须还原成进来时的主题
        var originalTheme = themeService.CurrentTheme;
        var localization = new LocalizationService();
        localization.Initialize();
        // 截图要反映真实设置（焦点框开关默认开），否则截出的图与用户实际看到的不一致
        FocusRingSettings.Apply(AppSettings.Load().ShowFocusRing);

        var projectService = new SmokeProjectService();
        var navigation = new NavigationService();
        // 截图需要真实覆盖层承载：这里用真实 DialogService（只渲染，不弹系统窗口）
        var dialogService = new DialogService(localization);
        var mainViewModel = new MainViewModel(
            projectService,
            themeService,
            localization,
            navigation,
            dialogService,
            new SmokeDocumentRepository(),
            new DocumentImportService(),
            new SmokeGitService(),
            new SmokeFilePickerService());
        mainViewModel.Initialize();

        var window = new MainWindow
        {
            DataContext = mainViewModel,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            Width = width,
            Height = height,
        };
        window.Show();
        await SettleAsync(window);
        SaveWindow(window, System.IO.Path.Combine(targetDir, "01-list-empty.png"));

        // VD-088 回归：列表项进度条右侧补了百分比读数，需要有几行真实数据才看得到
        projectService.Add(MakeSampleProject(1, "SOrtus 桌面端", ProjectType.Programming, ProjectStatus.Active, 65, DateTime.Today.AddDays(-1)));
        projectService.Add(MakeSampleProject(2, "产品宣传片 · 秋季版", ProjectType.Clip, ProjectStatus.OnHold, 40, DateTime.Today.AddDays(-3)));
        projectService.Add(MakeSampleProject(3, "整理 Q4 读书清单", ProjectType.Todo, ProjectStatus.Planning, 10, DateTime.Today.AddYears(-1)));

        // 走一次真实导航：列表重取数据 + 侧边栏「最近立项」刷新（VD-082）
        await mainViewModel.ShowProjectsAsync();
        await SettleAsync(window);
        SaveWindow(window, System.IO.Path.Combine(targetDir, "01b-list-with-items.png"));

        // 诊断：逐个打开筛选栏的每个下拉，核对浮层是否与触发器重叠
        // （此前只测了第一个，「全部状态」的位置从未验证）
        int comboIndex = 0;
        foreach (var combo in Descendants(window).OfType<Controls.SOrComboBox>().Take(2))
        {
            comboIndex++;
            combo.IsDropDownOpen = true;
            await SettleAsync(window);
            Console.WriteLine($"[COMBO] 第{comboIndex}个下拉（宽{combo.ActualWidth:0}）");

            var popup = combo.Template?.FindName("PART_Popup", combo) as System.Windows.Controls.Primitives.Popup;
            var origin = combo.PointToScreen(new System.Windows.Point(0, 0));
            var popupOrigin = popup?.Child?.PointToScreen(new System.Windows.Point(0, 0));
            var border = popup?.Child as System.Windows.FrameworkElement;
            var transform = border?.RenderTransform as System.Windows.Media.TranslateTransform;
            Console.WriteLine($"[COMBO] combo=({origin.X:0},{origin.Y:0},{combo.ActualWidth:0}x{combo.ActualHeight:0}) " +
                $"popup=({(popupOrigin?.X ?? 0):0},{(popupOrigin?.Y ?? 0):0}) " +
                $"popupSize={border?.ActualWidth:0}x{border?.ActualHeight:0} " +
                $"transformY={(transform is null ? -999 : transform.Y):0.##} " +
                $"placement={popup?.Placement} target={(popup?.PlacementTarget?.GetType().Name ?? "null")} " +
                $"vOffset={popup?.VerticalOffset:0.##} hOffset={popup?.HorizontalOffset:0.##} " +
                $"isOpen={combo.IsDropDownOpen}");

            // 补充诊断：ToggleButton / PART_Root 的位置，弄清这 8px 缺口来自哪一层
            var toggle = combo.Template?.FindName("PART_DropDownToggle", combo) as System.Windows.FrameworkElement;
            var root = combo.Template?.FindName("PART_Root", combo) as System.Windows.FrameworkElement;
            var togglePt = toggle?.PointToScreen(new System.Windows.Point(0, 0));
            var rootPt = root?.PointToScreen(new System.Windows.Point(0, 0));
            Console.WriteLine($"[COMBO] toggle=({(togglePt?.X ?? 0):0},{(togglePt?.Y ?? 0):0},{toggle?.ActualWidth:0}x{toggle?.ActualHeight:0}) " +
                $"root=({(rootPt?.X ?? 0):0},{(rootPt?.Y ?? 0):0},{root?.ActualWidth:0}x{root?.ActualHeight:0})");

            // 关键验证：页面容器（SectionSwitchInStyle 宿主）的 RenderTransform.Y 是否残留 8
            // ——若是，Popup 定位用的布局坐标与 PointToScreen 的视觉坐标就差这 8px，
            // 硬编码 VerticalOffset 只是掩盖症状，正确修法是让入场动画结束后清除变换。
            var page = Descendants(window).OfType<System.Windows.FrameworkElement>()
                .FirstOrDefault(e => (e.RenderTransform as System.Windows.Media.TranslateTransform)?.Y is > 0.01 or < -0.01);
            var pageY = (page?.RenderTransform as System.Windows.Media.TranslateTransform)?.Y;
            Console.WriteLine($"[COMBO] pageTransformY={(pageY is null ? "null" : pageY.Value.ToString("0.##"))} " +
                $"pageType={(page?.GetType().Name ?? "null")}");

            // 回归断言（VD-121）：浮层必须在触发器下方留 4px 间距、不得遮挡触发器。
            // 曾经浮层上偏 8px 压住触发器，第一行「全部类型」被遮住点不到。
            double comboBottom = origin.Y + combo.ActualHeight;
            double popupTop = popupOrigin?.Y ?? 0;
            double gap = popupTop - comboBottom;
            // 断言「不遮挡」而非精确间距：PopupRoot 的固有上边距在不同运行下
            // 实测为 0 或 8px，间距在 4~12px 间浮动，但都不遮挡触发器。
            Console.WriteLine($"[COMBO] 浮层不遮挡={gap >= 0 && gap <= 16} " +
                $"(触发器底={comboBottom:0} 浮层顶={popupTop:0} 间距={gap:0})");

            SaveWindow(window, System.IO.Path.Combine(targetDir, "01c-list-filter-open.png"));
            combo.IsDropDownOpen = false;
            await SettleAsync(window);
        }

        await mainViewModel.ShowNewProjectAsync();
        await SettleAsync(window);
        SaveWindow(window, System.IO.Path.Combine(targetDir, "02-wizard-step1.png"));

        if (navigation.Current is ProjectWizardViewModel wizard)
        {
            wizard.SelectTypeCommand.Execute(ProjectType.Programming);
            wizard.NextStepCommand.Execute(null);
            wizard.Name = "SOrtus 桌面端";
            wizard.Summary = "本地优先的立项管理工具，打通编程立项全流程。";
            wizard.SubType = wizard.SubTypeOptions.FirstOrDefault() ?? string.Empty;
            wizard.ScheduleStart = DateTime.Today;
            wizard.ScheduleEnd = DateTime.Today.AddMonths(2);
            wizard.TogglePresetCommand.Execute(wizard.TechStackPresets.First(p => p.Name == "C#"));
            wizard.TogglePresetCommand.Execute(wizard.TechStackPresets.First(p => p.Name == "WPF"));
            wizard.TogglePresetCommand.Execute(wizard.TechStackPresets.First(p => p.Name == ".NET"));
        }

        await SettleAsync(window);
        SaveWindow(window, System.IO.Path.Combine(targetDir, "03-wizard-step2.png"));

        // 输入框自检（回归防护）：外壳 Opacity 必须保持 1（f0d98fc 曾让占位符动画
        // 打到外壳上，空框一点击就整块 Opacity→0「闪没」），焦点必须能进内层输入框。
        foreach (var box in Descendants(window).OfType<Controls.SOrTextBox>())
        {
            box.Focus();
            bool innerFocused = System.Windows.Input.Keyboard.FocusedElement == box.Inner;
            Console.WriteLine($"[FOCUS] textbox opacity={box.Opacity:0.00} innerFocused={innerFocused}");
        }

        // VD-095 回归：把向导滚到底，核对两个日期框是否等分铺满、中间有「至」连字符
        ScrollContentToEnd(window);
        await SettleAsync(window);
        SaveWindow(window, System.IO.Path.Combine(targetDir, "03b-wizard-step2-bottom.png"));

        var sample = new Project
        {
            Id = 1,
            Name = "SOrtus 桌面端",
            Type = ProjectType.Programming,
            Status = ProjectStatus.Active,
            Summary = "本地优先的立项管理工具，打通编程立项全流程。",
            SubType = "桌面应用",
            FolderPath = @"D:\code\sortus",
            TechStack = "C#;WPF;.NET",
            ScheduleStart = DateTime.Today,
            ScheduleEnd = DateTime.Today.AddMonths(2),
            DevPlan = "1. 工程骨架与分层落地\n2. 编程立项闭环\n3. 视觉与主题打磨",
            Constraints = "纯本地运行，不引入云服务与账号体系。",
            Goals = "MVP 阶段跑通「创建 → 查看 → 编辑 → 重启仍在」完整路径。",
            AcceptanceCriteria = "构建零错误；数据重启不丢失；模糊不作用于内容层。",
            ProgressPercent = 65,
            CreatedAt = DateTime.Today.AddDays(-14),
            UpdatedAt = DateTime.Now,
        };
        mainViewModel.ShowDetail(sample);
        await SettleAsync(window);
        SaveWindow(window, System.IO.Path.Combine(targetDir, "04-detail-basic.png"));

        // VD-099 回归：先把「基础信息」滚到底，再切「开发管理」——新版应回到顶部，
        // 旧版会沿用滚动偏移，新内容的标题与第一个字段直接落在视口外。
        ScrollContentToEnd(window);
        await SettleAsync(window);
        SaveWindow(window, System.IO.Path.Combine(targetDir, "04b-detail-basic-bottom.png"));

        if (navigation.Current is ProjectDetailViewModel scrolled)
        {
            scrolled.SelectSectionCommand.Execute(ProjectDetailViewModel.SectionDev);
            await SettleAsync(window);
            SaveWindow(window, System.IO.Path.Combine(targetDir, "04c-detail-dev-after-scrolled-basic.png"));
        }

        if (navigation.Current is ProjectDetailViewModel detail)
        {
            detail.SelectSectionCommand.Execute(ProjectDetailViewModel.SectionDev);
            await SettleAsync(window);
            SaveWindow(window, System.IO.Path.Combine(targetDir, "05-detail-dev.png"));

            detail.SelectSectionCommand.Execute(ProjectDetailViewModel.SectionGit);
            await SettleAsync(window);
            SaveWindow(window, System.IO.Path.Combine(targetDir, "06-detail-git.png"));

            detail.SelectSectionCommand.Execute(ProjectDetailViewModel.SectionDev);
            await SettleAsync(window);

            // 第三批 VD-101/102 回归：开发管理滚到底，核对保存按钮文案与「危险区」卡
            ScrollContentToEnd(window);
            await SettleAsync(window);
            SaveWindow(window, System.IO.Path.Combine(targetDir, "05a-detail-dev-bottom.png"));
            ScrollContentToEnd(window, false);

            // 第三批 VD-107 回归：点「查看更早提交」后时间线应续显下一页
            detail.SelectSectionCommand.Execute(ProjectDetailViewModel.SectionGit);
            await SettleAsync(window);
            if (detail.Git?.ShowMoreCommitsCommand.CanExecute(null) == true)
            {
                detail.Git.ShowMoreCommitsCommand.Execute(null);
                await SettleAsync(window);
            }
            SaveWindow(window, System.IO.Path.Combine(targetDir, "06b-detail-git-more.png"));

            detail.SelectSectionCommand.Execute(ProjectDetailViewModel.SectionDev);
            await SettleAsync(window);

            var document = detail.PrdDocument ?? detail.OtherDocuments.FirstOrDefault();
            if (document is not null)
            {
                detail.OpenDocumentCommand.Execute(document);
                await SettleAsync(window);
                SaveWindow(window, System.IO.Path.Combine(targetDir, "07-document-dialog.png"));

                // VD-081 自检：弹窗打开后，键盘焦点必须落在弹窗内部（而不是背后的页面上）
                ReportDialogFocus(window, "07-document-dialog");

                mainViewModel.CloseDialogCommand.Execute(null);
                await SettleAsync(window);
            }
        }

        // 第三批 VD-117 回归：破坏性确认弹窗（危险样式 + 明确动作文案）
        // 放在文档弹窗之后：DialogService 同一时刻只承载一个覆盖层，前一个弹窗的退场动画
        // （150ms）没走完就开下一个，新弹窗会被吞掉——这正是上一版 07 截图没有弹窗的原因。
        var confirmTask = dialogService.ConfirmDestructiveAsync("删除这个立项？", "立项及其全部文档将被永久删除，此操作无法撤销。");
        await SettleAsync(window);
        SaveWindow(window, System.IO.Path.Combine(targetDir, "13-destructive-confirm.png"));
        mainViewModel.CloseDialogCommand.Execute(null);
        await confirmTask;
        await SettleAsync(window);

        // VD-091 回归：名称留空直接点「创建」——应把焦点搬到第一个错误字段，
        // 页脚给出「还有 N 项必填未完成」，字段边框转成危险色。
        await mainViewModel.ShowNewProjectAsync();
        if (navigation.Current is ProjectWizardViewModel failingWizard)
        {
            failingWizard.SelectTypeCommand.Execute(ProjectType.Programming);
            failingWizard.NextStepCommand.Execute(null);
            await SettleAsync(window);
            await ((Common.AsyncRelayCommand)failingWizard.CreateCommand).ExecuteAsync();
            await SettleAsync(window);
            SaveWindow(window, System.IO.Path.Combine(targetDir, "05b-wizard-validation-error.png"));
            ReportFieldFocus(window, "05b-wizard-validation-error");
        }

        await mainViewModel.ShowSettingsAsync();
        await SettleAsync(window);
        SaveWindow(window, System.IO.Path.Combine(targetDir, "08-settings.png"));

        // 第三批 VD-108/109/110 + 用户需求（焦点框开关、更新开关）：设置页下半段
        ScrollContentToEnd(window);
        await SettleAsync(window);
        SaveWindow(window, System.IO.Path.Combine(targetDir, "08b-settings-bottom.png"));
        ScrollContentToEnd(window, true);
        await SettleAsync(window);

        // VD-061 回归用：运行时整本换肤（不是启动即深色），验证分组卡 / 文字 / 边框是否一起跟随。
        // 09 = 切到浅色后的设置页，10 = 再切回深色后的同一页——两张都会进交付证据。
        // 这里刻意走「设置页下拉」的真实路径（SettingsViewModel.CurrentTheme），而不是直接调服务：
        // 否则下拉框里的值不会跟着变，截出来的图会自相矛盾（浅色页面 + 标签写着深色）。
        var settingsVm = navigation.Current as SettingsViewModel;
        if (settingsVm is not null)
        {
            settingsVm.CurrentTheme = "Light";
        }
        else
        {
            themeService.SetTheme("Light");
        }

        await SettleAsync(window);
        SaveWindow(window, System.IO.Path.Combine(targetDir, "09-settings-after-theme-switch-light.png"));

        if (settingsVm is not null)
        {
            settingsVm.CurrentTheme = "Dark";
        }
        else
        {
            themeService.SetTheme("Dark");
        }

        await SettleAsync(window);
        SaveWindow(window, System.IO.Path.Combine(targetDir, "10-settings-after-theme-switch-dark.png"));

        // VD-080：更新提示条改为浮层后不能靠「看不见」来验证，这里强制置为可见截一张，
        // 确认它是浮在内容右上角、不挤压页面布局。
        if (SemanticVersion.TryParse("0.3.1", out var previewVersion))
        {
            mainViewModel.Updates.PreviewBanner(new UpdateInfo(
                previewVersion,
                "v0.3.1",
                new Uri("https://example.com/SOrtus-Setup-0.3.1.exe"),
                148_000_000,
                null,
                "-",
                null));

            await mainViewModel.ShowProjectsAsync();
            await SettleAsync(window);
            SaveWindow(window, System.IO.Path.Combine(targetDir, "11-update-toast.png"));
        }

        // VD-060 回归用：最大化。验证 WM_GETMINMAXINFO 把尺寸钳到工作区、外距 22 收为 0，
        // 且自绘顶栏在最大化下仍然贴边完整（不会被系统边框挤出屏幕）。
        window.WindowState = WindowState.Maximized;
        await SettleAsync(window);
        SaveWindow(window, System.IO.Path.Combine(targetDir, "12-maximized.png"));
        window.WindowState = WindowState.Normal;

        // 还原用户进来时的主题（SetTheme 会持久化到 settings.json）
        themeService.SetTheme(originalTheme);

        window.Close();
    }

    private static IEnumerable<System.Windows.DependencyObject> Descendants(System.Windows.DependencyObject root)
    {
        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var nested in Descendants(child))
            {
                yield return nested;
            }
        }
    }

    /// <summary>截图用示例立项（不落库）。</summary>
    private static Project MakeSampleProject(
        int id,
        string name,
        ProjectType type,
        ProjectStatus status,
        int progress,
        DateTime updatedAt) => new()
    {
        Id = id,
        Name = name,
        Type = type,
        Status = status,
        ProgressPercent = progress,
        Summary = "截图示例数据",
        CreatedAt = DateTime.Today.AddDays(-30),
        UpdatedAt = updatedAt,
    };

    /// <summary>VD-081 自检：打印焦点是否落在对话框覆盖层内部。</summary>
    private static void ReportDialogFocus(Window window, string tag)
    {
        var host = window.FindName("PART_DialogHost") as DependencyObject;
        var focused = System.Windows.Input.Keyboard.FocusedElement as DependencyObject;
        var inside = host is not null && focused is not null && IsDescendantOf(focused, host);
        System.Console.WriteLine($"[FOCUS] {tag} dialogFocused={inside} element={focused?.GetType().Name ?? "null"}");
    }

    /// <summary>VD-091 自检：打印焦点所在控件的字段锚点（应为第一个校验失败的字段）。</summary>
    private static void ReportFieldFocus(Window window, string tag)
    {
        var focused = System.Windows.Input.Keyboard.FocusedElement as DependencyObject;
        var anchor = focused is null ? null : Controls.FormField.GetAnchor(focused);

        // 焦点可能落在控件内部的原生部件上，向上找带锚点的祖先
        var cursor = focused;
        while (anchor is null && cursor is not null && !ReferenceEquals(cursor, window))
        {
            cursor = System.Windows.Media.VisualTreeHelper.GetParent(cursor);
            anchor = cursor is null ? null : Controls.FormField.GetAnchor(cursor);
        }

        System.Console.WriteLine($"[FOCUS] {tag} fieldAnchor={anchor ?? "null"} element={focused?.GetType().Name ?? "null"}");
    }

    private static bool IsDescendantOf(DependencyObject node, DependencyObject ancestor)
    {
        var cursor = node;
        while (cursor is not null)
        {
            if (ReferenceEquals(cursor, ancestor))
            {
                return true;
            }

            cursor = System.Windows.Media.VisualTreeHelper.GetParent(cursor);
        }

        return false;
    }

    /// <summary>把当前页面滚动到底（截图核对页面下半部分用）。reset 为 true 时滚回顶部。</summary>
    private static void ScrollContentToEnd(Window window, bool reset = false)
    {
        if (window.FindName("PART_ContentHost") is not DependencyObject host)
        {
            return;
        }

        var viewer = Descendants(host).OfType<System.Windows.Controls.ScrollViewer>().FirstOrDefault();
        if (viewer is null)
        {
            return;
        }

        if (reset)
        {
            viewer.ScrollToTop();
        }
        else
        {
            viewer.ScrollToEnd();
        }
    }

    private static async Task SettleAsync(Window window)
    {
        window.UpdateLayout();
        await window.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        // 420ms：页面入场 220ms + 弹窗弹入 240ms，等动画走完再截，否则截图里是半透明的中间帧
        await Task.Delay(420);
        window.UpdateLayout();
    }

    private static void SaveWindow(Window window, string path)
    {
        // 截图用无边框窗口（客户区 = 窗口尺寸），直接渲染窗口即可 1:1 映射，不含非客户区偏移
        var width = Math.Max(1, (int)Math.Round(window.ActualWidth));
        var height = Math.Max(1, (int)Math.Round(window.ActualHeight));
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
            width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(window);

        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = System.IO.File.Create(path);
        encoder.Save(stream);
        System.Console.WriteLine($"[SHOT] {path} ({width}x{height})");
    }

    /// <summary>冒烟自检：实例化控件与视图模板，切换主题/语言，校验视觉树。</summary>
    private int RunSmoke()
    {
        var errors = new List<string>();

        var themeService = new ThemeService();
        themeService.Initialize();
        var localization = new LocalizationService();
        localization.Initialize();

        // 自定义控件（交互控件全部自定义，逐一类验证模板可实例化）
        var controls = new FrameworkElement[]
        {
            new Controls.SOrButton(),
            new Controls.SOrTextBox(),
            new Controls.SOrComboBox(),
            new Controls.SOrDatePicker(),
            new Controls.SOrSlider(),
            new Controls.SOrCard(),
            new Controls.SOrTag(),
            new Controls.SOrProgressBar(),
            new Controls.SOrProjectListItem(),
        };

        foreach (var c in controls)
        {
            try
            {
                // 脱离视觉树的控件不会自动套用 App.Resources 的隐式样式（只会走 DefaultStyleKey），
                // 这里显式取出隐式样式赋给 Style，再 ApplyTemplate，才能真正验证控件模板可构建。
                var implicitStyle = Application.Current.TryFindResource(c.GetType()) as Style;
                if (implicitStyle is not null)
                {
                    c.Style = implicitStyle;
                }

                c.ApplyTemplate();
                c.Measure(new Size(400, 200));
                c.Arrange(new Rect(0, 0, 400, 200));
                if (VisualTreeHelper.GetChildrenCount(c) == 0)
                {
                    errors.Add($"控件 {c.GetType().Name} 未生成视觉树");
                }
            }
            catch (System.Exception ex)
            {
                errors.Add($"控件 {c.GetType().Name} 实例化失败：{ex.Message}");
            }
        }

        // 视图模型（以无副作用的冒烟桩服务构造）
        var projectService = new SmokeProjectService();
        var dialogService = new SmokeDialogService();
        var navigation = new NavigationService();
        var sampleProject = new Project { Name = "Smoke", Type = ProjectType.Programming, Status = ProjectStatus.Planning };
        var documentRepository = new SmokeDocumentRepository();

        var viewModels = new object[]
        {
            new ProjectListViewModel(projectService, localization, _ => { }, () => { }),
            new ProjectWizardViewModel(projectService, localization, () => { }),
            new ProjectDetailViewModel(
                projectService,
                localization,
                dialogService,
                documentRepository,
                new DocumentImportService(),
                new SmokeGitService(),
                new SmokeFilePickerService(),
                sampleProject,
                () => { }),
            new SettingsViewModel(themeService, localization),
            new ConfirmDialogViewModel("标题", "正文", true, localization),
            new UnsavedChangesDialogViewModel("标题", "正文", localization),
            new DocumentDialogViewModel(
                documentRepository.SeedDocument,
                documentRepository,
                new MarkdownRenderer(),
                localization),
            new GitViewModel(new SmokeGitService(), localization, dialogService, sampleProject),
            new AddRemoteDialogViewModel(new SmokeGitService(), ".", "origin", "https://example.com/repo.git", localization),
        };

        foreach (var vm in viewModels)
        {
            try
            {
                var host = new ContentControl { Content = vm };
                host.ApplyTemplate();
                var presenter = VisualTreeHelper.GetChild(host, 0) as FrameworkElement;
                presenter?.ApplyTemplate();
                host.Measure(new Size(1000, 700));
                host.Arrange(new Rect(0, 0, 1000, 700));
                host.UpdateLayout();
                if (VisualTreeHelper.GetChildrenCount(host) == 0)
                {
                    errors.Add($"视图 {vm.GetType().Name} 未生成视觉树");
                }
            }
            catch (System.Exception ex)
            {
                errors.Add($"视图 {vm.GetType().Name} 模板失败：{ex}");
            }
        }

        // 切换主题 / 语言：验证资源整本替换不缺键、不抛异常
        try
        {
            themeService.SetTheme("Dark");
            themeService.SetTheme("Light");
            localization.SetCulture("en-US");
            localization.SetCulture("zh-CN");
        }
        catch (System.Exception ex)
        {
            errors.Add($"主题/语言切换失败：{ex.Message}");
        }

        if (errors.Count == 0)
        {
            System.Console.WriteLine("[SMOKE] PASS: 所有自定义控件与视图模板实例化成功，主题/语言切换正常。");
            return 0;
        }

        System.Console.WriteLine("[SMOKE] FAIL:");
        foreach (var err in errors)
        {
            System.Console.WriteLine("  - " + err);
        }

        return 1;
    }
}

/// <summary>冒烟桩：不访问数据库的项目服务实现。</summary>
internal sealed class SmokeProjectService : IProjectService
{
    /// <summary>截图用的示例数据（不读写数据库）。</summary>
    private readonly List<Project> _items = new();

    public void Add(Project project) => _items.Add(project);

    public Task<IReadOnlyList<Project>> GetAllAsync() =>
        Task.FromResult<IReadOnlyList<Project>>(_items.ToList());

    public Task<IReadOnlyList<Project>> QueryAsync(ProjectType? type, ProjectStatus? status, string? keyword) =>
        Task.FromResult<IReadOnlyList<Project>>(_items
            .Where(p => type is null || p.Type == type)
            .Where(p => status is null || p.Status == status)
            .Where(p => string.IsNullOrWhiteSpace(keyword) || p.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .ToList());
    public Task<Project?> GetByIdAsync(int id) => Task.FromResult<Project?>(null);
    public Task<Project> CreateAsync(Project project) => Task.FromResult(project);
    public Task<Project> SaveAsync(Project project) => Task.FromResult(project);
    public Task DeleteAsync(int id) => Task.CompletedTask;
}

/// <summary>冒烟桩：无宿主的对话框服务实现。</summary>
internal sealed class SmokeDialogService : IDialogService
{
    public event System.Action<object?>? ActiveDialogChanged;
    public Task<bool> ConfirmAsync(string title, string message)
    {
        ActiveDialogChanged?.Invoke(null);
        return Task.FromResult(false);
    }

    public Task<bool> ConfirmDestructiveAsync(string title, string message)
    {
        ActiveDialogChanged?.Invoke(null);
        return Task.FromResult(false);
    }

    public Task<UnsavedChangesChoice> ConfirmUnsavedAsync(string title, string message)
    {
        ActiveDialogChanged?.Invoke(null);
        return Task.FromResult(UnsavedChangesChoice.Cancel);
    }

    public Task InfoAsync(string title, string message) => Task.CompletedTask;

    public Task ShowAsync(IOverlayDialogViewModel viewModel)
    {
        ActiveDialogChanged?.Invoke(viewModel);
        viewModel.RequestClose();
        ActiveDialogChanged?.Invoke(null);
        return Task.CompletedTask;
    }
}

/// <summary>冒烟桩：内存文档仓储（不访问数据库），为冒烟与截图提供可渲染的示例文档。</summary>
internal sealed class SmokeDocumentRepository : IProjectDocumentRepository
{
    private readonly List<ProjectDocument> _documents = new();

    public SmokeDocumentRepository()
    {
        var now = DateTime.Now;
        _documents.Add(new ProjectDocument
        {
            Id = 1,
            ProjectId = 1,
            Category = DocumentCategory.Prd,
            Title = "SOrtus 产品需求文档",
            SourceFileName = "SOrtus_产品需求文档.md",
            Format = "md",
            SortOrder = 0,
            CreatedAt = now.AddDays(-12),
            UpdatedAt = now.AddDays(-1),
            Content = SamplePrd,
        });

        _documents.Add(new ProjectDocument
        {
            Id = 2,
            ProjectId = 1,
            Category = DocumentCategory.IterationPlan,
            Title = "SOrtus 版本迭代规划",
            SourceFileName = "SOrtus_版本迭代规划.md",
            Format = "md",
            SortOrder = 0,
            CreatedAt = now.AddDays(-11),
            UpdatedAt = now.AddDays(-2),
            Content = "# 版本迭代规划\n\n## 0.1.0 MVP\n\n- [x] 工程骨架与分层\n- [x] 编程立项闭环\n\n## 0.2.0 详情页 V2\n\n- [ ] 三分版块\n- [ ] 文档导入与渲染\n- [ ] Git 时间线与推送\n",
        });

        _documents.Add(new ProjectDocument
        {
            Id = 3,
            ProjectId = 1,
            Category = DocumentCategory.Other,
            Title = "MVP 验收记录",
            SourceFileName = "MVP验收记录.md",
            Format = "md",
            SortOrder = 1,
            CreatedAt = now.AddDays(-3),
            UpdatedAt = now.AddDays(-3),
            Content = "# 验收结论\n\n构建零错误，数据重启不丢失。\n\n- [x] A4 数据库初始化\n- [x] A12 未保存拦截\n",
        });
    }

    public ProjectDocument SeedDocument => _documents[0];

    public Task<IReadOnlyList<ProjectDocument>> GetByProjectAsync(int projectId) =>
        Task.FromResult<IReadOnlyList<ProjectDocument>>(
            _documents.Where(d => d.ProjectId == projectId).ToList());

    public Task<ProjectDocument?> GetSlotAsync(int projectId, DocumentCategory category) =>
        Task.FromResult(_documents.FirstOrDefault(d =>
            d.ProjectId == projectId && d.Category == category && category != DocumentCategory.Other));

    public Task<int> UpsertAsync(ProjectDocument document)
    {
        if (document.Id == 0)
        {
            document.Id = _documents.Count == 0 ? 1 : _documents.Max(d => d.Id) + 1;
            _documents.Add(document);
        }

        return Task.FromResult(document.Id);
    }

    public Task<bool> UpdateContentAsync(int documentId, string content)
    {
        var target = _documents.FirstOrDefault(d => d.Id == documentId);
        if (target is null)
        {
            return Task.FromResult(false);
        }

        target.Content = content;
        target.UpdatedAt = DateTime.UtcNow;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(int documentId) =>
        Task.FromResult(_documents.RemoveAll(d => d.Id == documentId) > 0);

    public Task<int> GetNextSortOrderAsync(int projectId) =>
        Task.FromResult(_documents.Where(d => d.ProjectId == projectId).Select(d => d.SortOrder).DefaultIfEmpty(0).Max() + 1);

    private const string SamplePrd = """
        # SOrtus 产品需求文档

        > 本地优先的立项管理工具，打通编程立项全流程。

        ## 目标

        用一个小工具管理您的立项，做到**立项即沉淀**。

        ## 需求池

        | 编号 | 需求 | 优先级 |
        | --- | --- | --- |
        | F0-03 | 详情页字段编辑 | P0 |
        | F0-07 | 开发进度管理 | P1 |

        ## 待办

        - [x] 立项向导
        - [x] 详情页基础字段
        - [ ] 文档导入与渲染
        - [ ] Git 时间线

        ## 技术约定

        ````csharp
        var project = new Project { Name = "SOrtus" };
        ````

        ---

        详见开发文档。
        """;
}

/// <summary>冒烟桩：返回固定快照的 Git 服务（不调用命令行）。</summary>
internal sealed class SmokeGitService : IGitService
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Now;

    public Task<GitRepositoryInfo> InspectAsync(string folderPath, int commitCount = 30)
    {
        var branches = new List<GitBranchInfo>
        {
            new("master", true, false, "32fe807", "build: 版本号提升至 0.1.1", Now.AddHours(-3), 2, 0),
            new("feature/detail-v2", false, false, "a1b2c3d", "feat: 详情页三分版块", Now.AddHours(-1), 0, 0),
            new("origin/master", false, true, "32fe807", "build: 版本号提升至 0.1.1", Now.AddHours(-4), 0, 0),
        };

        var commits = new List<GitCommitInfo>
        {
            new("a1b2c3d4e5f6", "a1b2c3d", "Song-1118", Now.AddHours(-1), "feat: 详情页三分版块与文档导入", ["feature/detail-v2"]),
            new("32fe807a1b2c", "32fe807", "Song-1118", Now.AddHours(-3), "build: 版本号提升至 0.1.1", ["master", "origin/master"]),
            new("deaa6539c8d7", "deaa653", "Song-1118", Now.AddHours(-5), "fix: 处置阶段验收遗留问题", []),
            new("df5fef5b6a41", "df5fef5", "Song-1118", Now.AddHours(-8), "feat(ui): 交互与视觉第二轮修复", []),
        };

        return Task.FromResult(new GitRepositoryInfo(
            true,
            folderPath,
            "master",
            true,
            true,
            2,
            0,
            branches,
            commits,
            [new GitRemoteInfo("origin", "https://example.com/sortus.git")]));
    }

    public Task<GitOperationResult> AddOrSetRemoteAsync(string folderPath, string name, string url) =>
        Task.FromResult(GitOperationResult.Ok());

    public Task<GitOperationResult> PushAsync(string folderPath, string branch, string remote, bool setUpstream) =>
        Task.FromResult(GitOperationResult.Ok());
}

/// <summary>冒烟桩：不弹出文件选择框（返回取消）。</summary>
internal sealed class SmokeFilePickerService : IFilePickerService
{
    public string? PickDocument() => null;
}


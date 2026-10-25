using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using SOrtus.Common;
using SOrtus.ViewModels;

namespace SOrtus;

/// <summary>
/// 主窗口外壳（规范：主窗口骨架与基础导航框架）。
/// 内容区通过 ViewTemplates.xaml 将视图模型映射为对应视图。
/// 关闭窗口时若当前详情页有未保存更改，先走「保存 / 不保存 / 取消」确认（验收 A12）。
/// <para>
/// VD-060：窗口改为自绘顶栏（WindowChrome）。顶栏的拖动 / 双击最大化 / 分屏吸附由系统处理，
/// 这里只负责三件事：三键命令、最大化工作区尺寸修正（WM_GETMINMAXINFO）、换主题时的一次性过渡。
/// </para>
/// </summary>
public partial class MainWindow : Window
{
    private const int WmGetMinMaxInfo = 0x0024;
    private const int MonitorDefaultToNearest = 0x00000002;

    private bool _closeConfirmed;
    private string? _lastTheme;

    public MainWindow()
    {
        InitializeComponent();
        WindowCommand = new RelayCommand(ExecuteWindowCommand);
    }

    /// <summary>顶栏三键命令（CommandParameter：Minimize / Maximize / Close）。</summary>
    public ICommand WindowCommand { get; }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        // 无边框窗最大化时必须把尺寸钳到「显示器工作区」，否则会按 WindowChrome 的边框补偿
        // 向外溢出几像素、把任务栏盖住一部分（VD-060）。
        var handle = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(handle)?.AddHook(WndProc);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_closeConfirmed || DataContext is not MainViewModel vm || !vm.HasUnsavedChanges)
        {
            base.OnClosing(e);
            return;
        }

        // 先拦下这次关闭，等用户在覆盖层里做完选择（异步）后再决定是否真正关闭
        e.Cancel = true;
        _ = ConfirmCloseAsync(vm);
    }

    /// <summary>
    /// 主题切换过渡（VD-067）：根视觉在 120ms 内压到 0.86，再 180ms 回 1。
    /// 换肤时画刷是整本替换的，新旧两套颜色会有一帧同时存在（半新半旧），
    /// 一次性压暗把这一帧藏进「沉一下」的过渡里，而不是让用户看到割裂的中间态。
    /// 只在主题真正变化时播放；模糊强度变化同样会触发 ThemeChanged，但那是连续拖动，不能闪。
    /// </summary>
    public void PlayThemeTransition(string themeName)
    {
        if (string.Equals(_lastTheme, themeName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _lastTheme = themeName;

        var animation = new DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(300) };
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(0.86, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(120)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        });
        animation.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(300)))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        });

        PART_Shell.BeginAnimation(OpacityProperty, animation);
    }

    private void ExecuteWindowCommand(object? parameter)
    {
        switch (parameter as string)
        {
            case "Minimize":
                SystemCommands.MinimizeWindow(this);
                break;

            case "Maximize":
                if (WindowState == WindowState.Maximized)
                {
                    SystemCommands.RestoreWindow(this);
                }
                else
                {
                    SystemCommands.MaximizeWindow(this);
                }

                break;

            case "Close":
                SystemCommands.CloseWindow(this);
                break;
        }
    }

    private async Task ConfirmCloseAsync(MainViewModel vm)
    {
        if (!await vm.CanCloseAsync())
        {
            return;
        }

        _closeConfirmed = true;
        Close();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmGetMinMaxInfo)
        {
            ApplyWorkAreaToMaximizedSize(hwnd, lParam);
            handled = true;
        }

        return IntPtr.Zero;
    }

    /// <summary>把最大化尺寸与位置钳到当前显示器的工作区（不含任务栏）。</summary>
    private static void ApplyWorkAreaToMaximizedSize(IntPtr hwnd, IntPtr lParam)
    {
        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return;
        }

        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info))
        {
            return;
        }

        var minMax = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        var work = info.WorkArea;
        var screen = info.Monitor;

        minMax.MaxPosition.X = work.Left - screen.Left;
        minMax.MaxPosition.Y = work.Top - screen.Top;
        minMax.MaxSize.X = work.Right - work.Left;
        minMax.MaxSize.Y = work.Bottom - work.Top;

        Marshal.StructureToPtr(minMax, lParam, true);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public NativePoint Reserved;
        public NativePoint MaxSize;
        public NativePoint MaxPosition;
        public NativePoint MinTrackSize;
        public NativePoint MaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public int Flags;
    }
}

using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace SOrtus.Controls;

/// <summary>
/// 动效接线层（《SOrtus 动效设计规范-0.3.0》§7 WPF 落地约束）。
/// <para>
/// 规范要求动画资源集中在 Animations.xaml、页面零自定义 Storyboard；但 Storyboard 的
/// 目标路径写死 RenderTransform 时，元素没有对应 Transform 就静默不动（VD-062 记录的真实事故）。
/// 这里用附加属性统一注入 RenderTransform 并在正确时机播放，模板只声明意图：
/// <c>Motion.PageEnter</c>（页面进场）、<c>Motion.StaggerIndex</c>（列表错开）、
/// <c>Motion.RefreshPulse</c>（筛选刷新脉冲）、<c>Motion.PlayOnLoad</c>（命名动作）。
/// </para>
/// <para>
/// M-57 减少动效：读系统「显示动画」（SystemParameters.ClientAreaAnimation / MinAnimate），
/// 关闭时全部动画降到 ≤120ms 且去掉位移与缩放——由 <see cref="Begin"/> 统一降级，
/// 功能路径不依赖动画完成（动画只跟状态，不阻塞输入）。
/// </para>
/// </summary>
public static class Motion
{
    /// <summary>页面进场方向：Forward = 列表→详情（+12px 上浮）；Return = 详情→列表（-8px 下落）；Default = 同层导航（+8px）。</summary>
    public enum EnterDirection
    {
        Default,
        Forward,
        Return,
    }

    /// <summary>
    /// 下一次页面进场的方向。导航发生在视图模型层，页面模板无法感知「是前进还是返回」，
    /// 由导航方在切换前写入，附加属性在 Loaded 时读取并清零（每页只消费一次）。
    /// </summary>
    public static EnterDirection PendingEnter { get; set; } = EnterDirection.Default;

    /// <summary>
    /// 是否处于「减少动效」模式（系统动画关闭）。
    /// 读 SystemParameters.ClientAreaAnimation（WPF）+ SPI_GETMINANIMATE（Win32 最小化动画，
    /// WPF 未封装，规范 §7 要求两者都看）。任一关闭即降级。
    /// </summary>
    public static bool IsReducedMotion =>
        !SystemParameters.ClientAreaAnimation || !IsMinAnimateEnabled();

    /// <summary>SPI_GETMINANIMATE（0x0048）：系统「最小化/还原动画」开关。</summary>
    private static bool IsMinAnimateEnabled()
    {
        try
        {
            int value = 1;
            return SystemParametersInfo(0x0048, 0, ref value, 0) && value != 0;
        }
        catch
        {
            // 读不到按「开启」处理：功能不受影响，只是不降级
            return true;
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref int pvParam, uint fWinIni);

    /// <summary>
    /// 活跃动画计数：大于 0 时 <c>BlurPanel</c> 暂停 LayoutUpdated 对齐回调
    /// （规范 §7「BlurPanel 的 LayoutUpdated 要节流」，多卡 + 动画会形成回调风暴）。
    /// </summary>
    public static int ActiveAnimations;

    /// <summary>
    /// 是否正处于滚动中（M-33：滚动时列表项不做入场，否则每次滚动都掉帧）。
    /// 由祖先 ScrollViewer 的 ScrollChanged 维护。
    /// </summary>
    public static bool IsScrolling { get; private set; }

    private static System.Windows.Threading.DispatcherTimer? _scrollIdleTimer;

    #region PageEnter（M-07 / M-08 / M-09 / M-11 / M-12）

    /// <summary>
    /// 页面根元素挂上后按 <see cref="PendingEnter"/> 播放进场动画。
    /// 值为 "Auto" 时读方向；"None" 时不动（例如截图、无障碍场景）。
    /// </summary>
    public static readonly DependencyProperty PageEnterProperty =
        DependencyProperty.RegisterAttached(
            "PageEnter", typeof(string), typeof(Motion),
            new PropertyMetadata(null, OnPageEnterChanged));

    public static string? GetPageEnter(DependencyObject obj) => (string?)obj.GetValue(PageEnterProperty);
    public static void SetPageEnter(DependencyObject obj, string? value) => obj.SetValue(PageEnterProperty, value);

    private static void OnPageEnterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement fe || e.NewValue is not string mode || mode == "None")
        {
            return;
        }

        EnsureTransformGroup(fe);

        fe.Loaded += (_, _) => PlayPageEnter(fe, mode == "Auto" ? PendingEnter : EnterDirection.Default);
    }

    private static void PlayPageEnter(FrameworkElement fe, EnterDirection direction)
    {
        PendingEnter = EnterDirection.Default;

        string key = direction switch
        {
            EnterDirection.Forward => "PageForwardStoryboard",   // M-08：220/280ms +12→0
            EnterDirection.Return => "PageBackStoryboard",      // M-09：220ms -8→0
            _ => "SlideUpStoryboard",                            // M-07：220ms +8→0
        };

        Begin(fe, key, () => fe.Opacity = 1);
    }

    #endregion

    #region StaggerIndex（M-27 列表 / M-39 设置卡 / M-51 时间线）

    /// <summary>列表项错开下标（0 起）。超过 8 项直接出现（规范「不要超过 8 项的错开」）。</summary>
    public static readonly DependencyProperty StaggerIndexProperty =
        DependencyProperty.RegisterAttached(
            "StaggerIndex", typeof(int), typeof(Motion),
            new PropertyMetadata(-1, OnStaggerIndexChanged));

    public static int GetStaggerIndex(DependencyObject obj) => (int)obj.GetValue(StaggerIndexProperty);
    public static void SetStaggerIndex(DependencyObject obj, int value) => obj.SetValue(StaggerIndexProperty, value);

    /// <summary>错开步长（毫秒）。M-27 列表 40ms；M-39 设置卡 / M-51 时间线 30ms。</summary>
    public static readonly DependencyProperty StaggerStepProperty =
        DependencyProperty.RegisterAttached(
            "StaggerStep", typeof(double), typeof(Motion),
            new PropertyMetadata(40.0));

    public static double GetStaggerStep(DependencyObject obj) => (double)obj.GetValue(StaggerStepProperty);
    public static void SetStaggerStep(DependencyObject obj, double value) => obj.SetValue(StaggerStepProperty, value);

    /// <summary>错开播放的 Storyboard key（默认列表入场；设置卡用 SettingsCardInStoryboard）。</summary>
    public static readonly DependencyProperty StaggerStoryboardProperty =
        DependencyProperty.RegisterAttached(
            "StaggerStoryboard", typeof(string), typeof(Motion),
            new PropertyMetadata("ListStaggerInStoryboard"));

    public static string GetStaggerStoryboard(DependencyObject obj) => (string)obj.GetValue(StaggerStoryboardProperty);
    public static void SetStaggerStoryboard(DependencyObject obj, string value) => obj.SetValue(StaggerStoryboardProperty, value);

    private static void OnStaggerIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement fe || e.NewValue is not int index || index < 0)
        {
            return;
        }

        EnsureTransformGroup(fe);

        fe.Loaded += (_, _) =>
        {
            TrackScrollState(fe);

            // M-27：最多错开 8 项（40ms 步长）；第 9 项起同时出现
            if (index >= 8)
            {
                fe.Opacity = 1;
                return;
            }

            // M-33：滚动中不播入场动画——滚动时逐条飞入必然掉帧
            if (IsScrolling)
            {
                fe.Opacity = 1;
                return;
            }

            var step = GetStaggerStep(fe);
            var delay = TimeSpan.FromMilliseconds(index * step);
            fe.Opacity = 0;
            var timer = new DispatcherTimer { Interval = delay };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                if (IsReducedMotion)
                {
                    fe.Opacity = 1;
                    return;
                }

                Begin(fe, GetStaggerStoryboard(fe));
            };
            timer.Start();
        };
    }

    #endregion

    #region RefreshPulse（M-10 筛选结果刷新）

    /// <summary>
    /// 绑定任意刷新信号（如搜索词/筛选值）。值变化时对宿主做 0.55→1 的 160ms 脉冲，
    /// 不重播整页入场（规范 M-10：每次输入都闪是错的）。
    /// </summary>
    public static readonly DependencyProperty RefreshPulseProperty =
        DependencyProperty.RegisterAttached(
            "RefreshPulse", typeof(object), typeof(Motion),
            new PropertyMetadata(null, OnRefreshPulseChanged));

    public static object? GetRefreshPulse(DependencyObject obj) => obj.GetValue(RefreshPulseProperty);
    public static void SetRefreshPulse(DependencyObject obj, object? value) => obj.SetValue(RefreshPulseProperty, value);

    /// <summary>脉冲播放的 Storyboard key（默认筛选刷新脉冲；错误条用 ErrorBannerInStoryboard）。</summary>
    public static readonly DependencyProperty PulseStoryboardProperty =
        DependencyProperty.RegisterAttached(
            "PulseStoryboard", typeof(string), typeof(Motion),
            new PropertyMetadata("RefreshPulseStoryboard"));

    public static string GetPulseStoryboard(DependencyObject obj) => (string)obj.GetValue(PulseStoryboardProperty);
    public static void SetPulseStoryboard(DependencyObject obj, string value) => obj.SetValue(PulseStoryboardProperty, value);

    /// <summary>信号清空时播放的 Storyboard key（如 M-23 错误行收起；未设置则不做）。</summary>
    public static readonly DependencyProperty PulseStoryboardOutProperty =
        DependencyProperty.RegisterAttached(
            "PulseStoryboardOut", typeof(string), typeof(Motion),
            new PropertyMetadata(null));

    public static string? GetPulseStoryboardOut(DependencyObject obj) => (string?)obj.GetValue(PulseStoryboardOutProperty);
    public static void SetPulseStoryboardOut(DependencyObject obj, string? value) => obj.SetValue(PulseStoryboardOutProperty, value);

    private static void OnRefreshPulseChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement fe || !fe.IsLoaded)
        {
            return;
        }

        if (e.NewValue is null)
        {
            // 信号清空（如错误消除）：播退出段
            var outKey = GetPulseStoryboardOut(fe);
            if (!string.IsNullOrEmpty(outKey))
            {
                Begin(fe, outKey);
            }

            return;
        }

        Begin(fe, GetPulseStoryboard(fe));
    }

    #endregion

    #region PlayOnLoad（M-01 启动 / M-02 侧栏 / M-11 空状态 / M-12 无结果 / M-45 文档正文 …）

    /// <summary>加载时按资源名播放一次（值为 Animations.xaml 里的 Storyboard key）。</summary>
    public static readonly DependencyProperty PlayOnLoadProperty =
        DependencyProperty.RegisterAttached(
            "PlayOnLoad", typeof(string), typeof(Motion),
            new PropertyMetadata(null, OnPlayOnLoadChanged));

    public static string? GetPlayOnLoad(DependencyObject obj) => (string?)obj.GetValue(PlayOnLoadProperty);
    public static void SetPlayOnLoad(DependencyObject obj, string? value) => obj.SetValue(PlayOnLoadProperty, value);

    private static void OnPlayOnLoadChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement fe || e.NewValue is not string key)
        {
            return;
        }

        EnsureTransformGroup(fe);

        fe.Loaded += (_, _) => Begin(fe, key);
    }

    /// <summary>
    /// 每次变为可见时播放指定 Storyboard（版块 tab 切换、下拉浮层打开等「反复出现」的场景）。
    /// <para>
    /// <b>为什么不再用 Style Trigger 动画</b>：Style Setter 里的 <c>TranslateTransform</c>
    /// 会被 WPF 冻结并共享给所有应用该样式的元素。第一个元素的动画播放成功后对象进入
    /// 只读状态，第二个元素再对它做动画就抛
    /// <c>InvalidOperationException: 无法对 TranslateTransform 进行动画处理</c>——
    /// 表现为「第一次进向导正常，再次进入直接崩溃」。
    /// 这里由代码为每个元素创建独立的 <c>TransformGroup</c>，动画目标不共享，无冻结问题。
    /// </para>
    /// </summary>
    public static readonly DependencyProperty PlayOnShowProperty =
        DependencyProperty.RegisterAttached(
            "PlayOnShow", typeof(string), typeof(Motion),
            new PropertyMetadata(null, OnPlayOnShowChanged));

    public static string? GetPlayOnShow(DependencyObject obj) => (string?)obj.GetValue(PlayOnShowProperty);
    public static void SetPlayOnShow(DependencyObject obj, string? value) => obj.SetValue(PlayOnShowProperty, value);

    private static void OnPlayOnShowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement fe || e.NewValue is not string key)
        {
            return;
        }

        EnsureTransformGroup(fe);

        if (fe.IsVisible)
        {
            Begin(fe, key);
        }

        fe.IsVisibleChanged += (_, _) =>
        {
            if (fe.IsVisible)
            {
                Begin(fe, key);
            }
        };
    }

    #endregion

    /// <summary>
    /// 监听祖先 ScrollViewer 的滚动状态：滚动中 <see cref="IsScrolling"/> 为 true，
    /// 停止 120ms 后复位（M-33）。只在有错开动画的元素上订阅，不给每个元素都挂。
    /// </summary>
    private static void TrackScrollState(FrameworkElement fe)
    {
        System.Windows.Controls.ScrollViewer? viewer = null;
        for (var parent = VisualTreeHelper.GetParent(fe); parent is not null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is System.Windows.Controls.ScrollViewer found)
            {
                viewer = found;
                break;
            }
        }

        if (viewer is null)
        {
            return;
        }

        viewer.ScrollChanged += (_, _) =>
        {
            // 任何滚动事件都算「正在滚」，停止 120ms 后由 idle 计时器复位
            IsScrolling = true;
            _scrollIdleTimer ??= new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(120),
            };
            _scrollIdleTimer.Stop();
            _scrollIdleTimer.Tick -= (_, _) => IsScrolling = false;
            _scrollIdleTimer.Tick += (_, _) => IsScrolling = false;
            _scrollIdleTimer.Start();
        };
    }

    /// <summary>
    /// 注入 TransformGroup[Scale, Translate]——规范 §7 说写死 RenderTransform 的 Storyboard
    /// 在元素没有对应变换时静默不动；Scale 与 Translate 同时要动（如弹窗 M-41）时必须是 Group，
    /// 且 Storyboard 路径用 Children[i] 索引。已有 Group 时复用。
    /// </summary>
    private static void EnsureTransformGroup(FrameworkElement fe)
    {
        if (fe.RenderTransform is TransformGroup group &&
            group.Children.Count >= 2 &&
            group.Children[0] is ScaleTransform &&
            group.Children[1] is TranslateTransform)
        {
            return;
        }

        fe.RenderTransform = new TransformGroup
        {
            Children =
            {
                new ScaleTransform(1, 1),
                new TranslateTransform(),
            },
        };
    }

    /// <summary>
    /// 播放资源字典里的 Storyboard：注入缺省变换、尊重「减少动效」、统计活跃动画。
    /// <paramref name="onCompleted"/> 在播放结束（或跳过）后执行，用于把终值固化。
    /// </summary>
    public static void Begin(FrameworkElement fe, string storyboardKey, Action? onCompleted = null)
    {
        if (Application.Current?.TryFindResource(storyboardKey) is not Storyboard storyboard)
        {
            fe.Opacity = 1;
            onCompleted?.Invoke();
            return;
        }

        if (IsReducedMotion)
        {
            // M-57：降到 ≤120ms 并去掉位移/缩放——直接落到终值，功能不受影响
            fe.Opacity = 1;
            if (fe.RenderTransform is TransformGroup group)
            {
                foreach (var child in group.Children)
                {
                    switch (child)
                    {
                        case TranslateTransform t:
                            t.X = 0;
                            t.Y = 0;
                            break;
                        case ScaleTransform s:
                            s.ScaleX = 1;
                            s.ScaleY = 1;
                            break;
                    }
                }
            }

            onCompleted?.Invoke();
            return;
        }

        var clone = storyboard.Clone();
        ActiveAnimations++;
        clone.Completed += (_, _) =>
        {
            ActiveAnimations--;
            onCompleted?.Invoke();
        };
        clone.Begin(fe, HandoffBehavior.SnapshotAndReplace, true);
    }
}

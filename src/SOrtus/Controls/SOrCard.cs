using System.Windows;
using System.Windows.Controls;

namespace SOrtus.Controls;

/// <summary>
/// 自定义磨砂卡片（MVP 控件库 · 规范 7.4.1）。
/// <para>
/// 模板内置 <c>PART_BlurHost</c> 专用模糊宿主（Border），由本控件把
/// <see cref="EnableBlur"/> / <see cref="BlurRadius"/> / <see cref="OverlayOpacity"/> 转发为
/// <see cref="BlurPanel"/> 附加属性；内容层（ContentPresenter）永远位于模糊层之上，
/// 保证「模糊只作用于背景、不模糊内容」（规范 7.4.1 / 7.9）。圆角裁剪由 PART_Root 承担。
/// </para>
/// <para>
/// 后期做空间模糊与动画：直接对模板内 <c>PART_BlurHost</c>（模糊）与 <c>PART_Root</c>（整体变换）编程即可。
/// 全局强度跟随 <c>GlobalBlurRadius</c> 令牌，设置页调节即时生效。
/// </para>
/// </summary>
public sealed class SOrCard : ContentControl
{
    /// <summary>是否启用背景模糊（默认启用）。</summary>
    public static readonly DependencyProperty EnableBlurProperty =
        DependencyProperty.Register(
            nameof(EnableBlur), typeof(bool), typeof(SOrCard),
            new PropertyMetadata(true, OnBlurPropertyChanged));

    /// <summary>模糊半径（缺省 <see cref="double.NaN"/> = 跟随全局令牌 GlobalBlurRadius，设置页调节即时生效）。</summary>
    public static readonly DependencyProperty BlurRadiusProperty =
        DependencyProperty.Register(
            nameof(BlurRadius), typeof(double), typeof(SOrCard),
            new PropertyMetadata(double.NaN, OnBlurPropertyChanged));

    /// <summary>覆盖层透明度（默认 0.86：保证磨砂卡片上正文对比度达标）。</summary>
    public static readonly DependencyProperty OverlayOpacityProperty =
        DependencyProperty.Register(
            nameof(OverlayOpacity), typeof(double), typeof(SOrCard),
            new PropertyMetadata(0.86, OnBlurPropertyChanged));

    /// <summary>圆角半径（缺省取令牌 CornerRadiusLarge）。</summary>
    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(
            nameof(CornerRadius), typeof(CornerRadius), typeof(SOrCard),
            new PropertyMetadata(new CornerRadius(18)));

    private Border? _blurHost;

    static SOrCard()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SOrCard), new FrameworkPropertyMetadata(typeof(SOrCard)));
    }

    public bool EnableBlur
    {
        get => (bool)GetValue(EnableBlurProperty);
        set => SetValue(EnableBlurProperty, value);
    }

    public double BlurRadius
    {
        get => (double)GetValue(BlurRadiusProperty);
        set => SetValue(BlurRadiusProperty, value);
    }

    public double OverlayOpacity
    {
        get => (double)GetValue(OverlayOpacityProperty);
        set => SetValue(OverlayOpacityProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _blurHost = GetTemplateChild("PART_BlurHost") as Border;
        ApplyBlur();
    }

    private static void OnBlurPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((SOrCard)d).ApplyBlur();

    private void ApplyBlur()
    {
        if (_blurHost is null)
        {
            return;
        }

        BlurPanel.SetEnableBlur(_blurHost, EnableBlur);
        if (double.IsNaN(BlurRadius))
        {
            // 未显式指定半径时绑定全局强度令牌（DynamicResource，改一处全应用同步更新，规范 7.3 / A16）
            _blurHost.SetResourceReference(BlurPanel.BlurRadiusProperty, "GlobalBlurRadius");
        }
        else
        {
            BlurPanel.SetBlurRadius(_blurHost, BlurRadius);
        }

        BlurPanel.SetOverlayOpacity(_blurHost, OverlayOpacity);
    }
}

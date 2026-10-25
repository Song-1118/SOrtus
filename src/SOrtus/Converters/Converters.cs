using System.Globalization;
using System.Windows;
using System.Windows.Data;
using SOrtus.Controls;
using SOrtus.Core.Models;

namespace SOrtus.Converters;

/// <summary>
/// 集合计数 → 可见性（ConverterParameter=Invert 时取反）。用于列表 / 空状态互斥显示。
/// </summary>
public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var count = value switch
        {
            int i => i,
            long l => (int)l,
            _ => 0,
        };
        var visible = count > 0;
        if (ConverterHelpers.IsInvert(parameter))
        {
            visible = !visible;
        }

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>布尔 → 可见性（ConverterParameter=Invert 时取反）。</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var visible = value is true;
        if (ConverterHelpers.IsInvert(parameter))
        {
            visible = !visible;
        }

        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>非 null → 可见（用于图标等可选元素）。</summary>
public sealed class NotNullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is null ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>字符串相等 → bool（导航高亮 / 设置卡片选中态）。</summary>
public sealed class StringEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value as string, parameter as string, StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>枚举（或任意对象）ToString 与参数比较 → bool（向导类型卡片选中态）。</summary>
public sealed class TypeEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is not null && parameter is string s
            && string.Equals(value.ToString(), s, StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>立项类型 → 本地化显示名（resx：List_Type*）。</summary>
public sealed class ProjectTypeDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            ProjectType.Programming => "List_TypeProgramming",
            ProjectType.Clip => "List_TypeClip",
            ProjectType.Todo => "List_TypeTodo",
            null => "List_TypeAll",
            _ => "List_TypeAll",
        };
        return ConverterHelpers.Lookup(key) ?? value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>项目状态 → 本地化显示名（resx：List_Status*）。</summary>
public sealed class ProjectStatusDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            ProjectStatus.Planning => "List_StatusPlanning",
            ProjectStatus.Active => "List_StatusActive",
            ProjectStatus.Completed => "List_StatusCompleted",
            ProjectStatus.OnHold => "List_StatusOnHold",
            ProjectStatus.Archived => "List_StatusArchived",
            null => "List_StatusAll",
            _ => "List_StatusAll",
        };
        return ConverterHelpers.Lookup(key) ?? value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>立项类型 → 标签色调（设计约定：编程靛紫 / 剪辑粉 / 待办天蓝）。</summary>
public sealed class ProjectTypeToToneConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            ProjectType.Programming => SOrTagTone.AccentA,
            ProjectType.Clip => SOrTagTone.AccentC,
            ProjectType.Todo => SOrTagTone.AccentB,
            _ => SOrTagTone.Neutral,
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>项目状态 → 标签色调（进行中=天蓝 / 已完成=绿 / 已暂停=橙 / 规划·归档=灰）。</summary>
public sealed class ProjectStatusToToneConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            ProjectStatus.Active => SOrTagTone.AccentB,
            ProjectStatus.Completed => SOrTagTone.Success,
            ProjectStatus.OnHold => SOrTagTone.Warning,
            _ => SOrTagTone.Neutral,
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>CalendarMode → 是否为「年 / 十年」视图（月视图=false）。驱动日历模板中年网格可见性。</summary>
public sealed class NotMonthModeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is System.Windows.Controls.CalendarMode.Year or System.Windows.Controls.CalendarMode.Decade;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// 更新时间 → 「更新于 MM-dd」/「更新于 yyyy-MM-dd」（resx：List_UpdatedAt / List_UpdatedAtCrossYear）。
/// VD-090：旧版永远只显示月日，跨年项目（2025-10-25 与 2026-10-25）在列表里看起来是同一天；
/// 同一份数据在详情页页脚却带年份。这里按是否跨年切换格式，保住列表的行高不变。
/// </summary>
public sealed class UpdatedAtDisplayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTime dt)
        {
            return string.Empty;
        }

        var local = dt.Kind == DateTimeKind.Utc ? dt.ToLocalTime() : dt;
        var sameYear = local.Year == DateTime.Now.Year;
        var key = sameYear ? "List_UpdatedAt" : "List_UpdatedAtCrossYear";
        var format = ConverterHelpers.Lookup(key) ?? (sameYear ? "Updated {0:MM-dd}" : "Updated {0:yyyy-MM-dd}");

        return string.Format(CultureInfo.CurrentCulture, format, local);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

internal static class ConverterHelpers
{
    public static bool IsInvert(object? parameter) =>
        parameter is string s && (s.Equals("Invert", StringComparison.OrdinalIgnoreCase)
                                  || s.Equals("Reverse", StringComparison.OrdinalIgnoreCase));

    public static string? Lookup(string key) =>
        Application.Current?.TryFindResource(key) as string;
}

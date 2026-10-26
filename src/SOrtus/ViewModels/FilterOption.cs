namespace SOrtus.ViewModels;

/// <summary>
/// 筛选项包装：「全部」也是一个真实选项（<see cref="Value"/> 为 <c>null</c>），
/// 不再把 <c>null</c> 本身塞进选项集合。
/// <para>
/// 为什么：WPF ComboBox 选不回 <c>null</c> 项——用户反馈「一旦选了非全部，
/// 就选不回去了」，根因就是「全部类型」是 ItemsSource 里的 null 项，
/// 且「无选中」与「选中全部」在 UI 上根本区分不开。
/// 改成真实项后：默认选中第一项（全部），点回来与点其它选项走同一条链路。
/// </para>
/// </summary>
internal sealed class FilterOption
{
    public FilterOption(object? value) => Value = value;

    /// <summary>筛选值；<c>null</c> = 全部（不筛选）。</summary>
    public object? Value { get; }

    /// <summary>是否「全部」项。</summary>
    public bool IsAll => Value is null;

    public override bool Equals(object? obj) =>
        obj is FilterOption other && Equals(Value, other.Value);

    public override int GetHashCode() => Value?.GetHashCode() ?? 0;

    public override string ToString() => IsAll ? "All" : Value?.ToString() ?? "?";
}

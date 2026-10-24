namespace SOrtus.Core.Services;

/// <summary>
/// 主题与高斯模糊强度服务契约（规范 7 / 4.4 / 6.3）。
/// 负责浅色/深色主题运行时切换、模糊强度全局调节与持久化。
/// </summary>
public interface IThemeService
{
    /// <summary>当前主题名（"Light" / "Dark"）。</summary>
    string CurrentTheme { get; }

    /// <summary>全局模糊半径（规范：改一处全应用生效）。</summary>
    double GlobalBlurRadius { get; }

    /// <summary>初始化并应用已持久化的主题与模糊设置。</summary>
    void Initialize();

    /// <summary>切换主题（运行时整本替换主题字典）。</summary>
    void SetTheme(string themeName);

    /// <summary>设置全局模糊半径并持久化。</summary>
    void SetBlurStrength(double radius);

    /// <summary>主题或模糊设置变更事件。</summary>
    event EventHandler? ThemeChanged;
}

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

    /// <summary>设置全局模糊半径并持久化（一次性设置，如启动读取）。</summary>
    void SetBlurStrength(double radius);

    /// <summary>
    /// 拖动预览（VD-109）：只改内存与全局资源，**不落盘、不广播事件**。
    /// 滑块每 1px 触发一次时调用它，避免一次拖动产生几十次同步写文件。
    /// </summary>
    void PreviewBlurStrength(double radius);

    /// <summary>拖动结束（或输入停止）后调用一次：落盘并广播一次变更。</summary>
    void CommitBlurStrength();

    /// <summary>主题或模糊设置变更事件。</summary>
    event EventHandler? ThemeChanged;
}

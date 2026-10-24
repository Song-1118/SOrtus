namespace SOrtus.Core.Services;

/// <summary>
/// 本地化服务契约（规范 4.4 / 7 / 5.10）。
/// 负责运行时切换界面语言，所有可见文本走资源文件。
/// </summary>
public interface ILocalizationService
{
    /// <summary>当前语言（如 "zh-CN" / "en-US"）。</summary>
    string CurrentCulture { get; }

    /// <summary>初始化并应用已持久化的语言设置。</summary>
    void Initialize();

    /// <summary>切换语言并广播变更。</summary>
    void SetCulture(string cultureName);

    /// <summary>按资源键获取当前语言的字符串。</summary>
    string GetString(string key);

    /// <summary>语言变更事件。</summary>
    event EventHandler? CultureChanged;
}

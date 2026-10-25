using System.Globalization;
using System.Resources;
using System.Windows;
using SOrtus.Core.Services;
using SOrtus.Resources.Strings;

namespace SOrtus.Services;

/// <summary>
/// 本地化服务实现（规范 4.4 / 7 / 5.10）。
/// 将 resx 资源按当前语言构建为 ResourceDictionary 合并进 App.Resources，所有 XAML 通过
/// DynamicResource 引用；切换语言时重建字典并广播 CultureChanged，实现即时生效。
/// 中性 resx（中文）为权威源，en-US 为卫星程序集；预留韩文/日文仅需补充对应 resx。
/// </summary>
internal sealed class LocalizationService : ILocalizationService
{
    private AppSettings _settings = new();
    private string _culture = "zh-CN";

    private static readonly ResourceManager Manager = SOrtus.Resources.Strings.Strings.ResourceManager;

    public string CurrentCulture => _culture;

    public event EventHandler? CultureChanged;

    public void Initialize()
    {
        _settings = AppSettings.Load();
        _culture = string.IsNullOrWhiteSpace(_settings.Culture) ? AppSettings.DefaultCulture : _settings.Culture;
        ApplyCulture(_culture);
    }

    public void SetCulture(string? cultureName)
    {
        cultureName = string.IsNullOrWhiteSpace(cultureName) ? AppSettings.DefaultCulture : cultureName;

        if (string.Equals(_culture, cultureName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _culture = cultureName;
        _settings.Culture = cultureName;
        _settings.Save();
        ApplyCulture(cultureName);
        CultureChanged?.Invoke(this, EventArgs.Empty);
    }

    public string GetString(string key)
    {
        return Manager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
    }

    private void ApplyCulture(string? cultureName)
    {
        // 兜底：设置文件里 Culture 为 null 时 new CultureInfo(null) 会抛，程序直接起不来
        var ci = new CultureInfo(string.IsNullOrWhiteSpace(cultureName) ? AppSettings.DefaultCulture : cultureName);
        CultureInfo.CurrentUICulture = ci;
        CultureInfo.CurrentCulture = ci;

        var set = Manager.GetResourceSet(ci, createIfNotExists: true, tryParents: true);
        var dictionary = new ResourceDictionary();
        dictionary["__SoStrings"] = true;
        if (set is not null)
        {
            foreach (System.Collections.DictionaryEntry entry in set)
            {
                if (entry.Key is string key && entry.Value is string value)
                {
                    dictionary[key] = value;
                }
            }
        }

        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        var existing = app.Resources.MergedDictionaries
            .FirstOrDefault(d => d.Contains("__SoStrings"));
        if (existing is not null)
        {
            app.Resources.MergedDictionaries.Remove(existing);
        }

        app.Resources.MergedDictionaries.Add(dictionary);
    }
}

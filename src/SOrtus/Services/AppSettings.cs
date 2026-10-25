using System.IO;
using System.Text.Json;

namespace SOrtus.Services;

/// <summary>
/// 应用设置（主题 / 模糊强度 / 语言）的本地持久化（JSON，位于 %LocalAppData%/SOrtus/settings.json）。
/// 规范 4.4：高斯模糊、主题、语言设置实时生效并持久化，重启后保持。
/// </summary>
internal sealed class AppSettings
{
    public string Theme { get; set; } = "Light";

    /// <summary>
    /// 用户自定义的模糊强度（null = 未自定义，取 Sizes.xaml 中 BlurRadiusNormal 档位默认值）。
    /// 保持可空，才能让「改档位令牌一处、全应用同步」在未改过设置时依然成立（规范 7.3 / A16）。
    /// </summary>
    public double? BlurRadius { get; set; }

    public string Culture { get; set; } = "zh-CN";

    /// <summary>
    /// 是否在启动时自动检查更新（默认开启）。
    /// 关闭后程序不再主动联网，仅保留设置页的「立即检查」手动入口——
    /// 这是「数据留在本地」承诺下用户可行使的退出权，必须真实生效，不能只是界面摆设。
    /// </summary>
    public bool AutoCheckForUpdates { get; set; } = true;

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SOrtus",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);
            return settings ?? new AppSettings();
        }
        catch
        {
            // 读取失败回退默认，避免启动崩溃（规范 6.2：禁止吞异常且至少记录/兜底）
            return new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // 持久化失败不影响运行，仅丢失偏好
        }
    }
}

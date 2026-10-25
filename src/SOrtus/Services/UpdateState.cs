using System.IO;
using System.Text.Json;

namespace SOrtus.Services;

/// <summary>
/// 更新检查的内部状态（节流窗口 / ETag 缓存 / 用户主动跳过的版本）。
/// 与用户可见设置分开存放：这些值对用户没有语义，混进 settings.json 会污染设置文件语义。
/// 位置：%LocalAppData%/SOrtus/update-state.json
/// </summary>
internal sealed class UpdateState
{
    /// <summary>上次成功检查的时间（UTC）。用于 24 小时节流。</summary>
    public DateTime? LastCheckUtc { get; set; }

    /// <summary>上次响应的 ETag。命中 304 可跳过解析。</summary>
    public string? LastEtag { get; set; }

    /// <summary>用户选择「跳过此版本」的版本号；该版本不再提示，直到出现更高版本。</summary>
    public string? SkippedVersion { get; set; }

    /// <summary>
    /// 「稍后提醒」的版本号（VD-116）。
    /// 旧版的「稍后提醒」命令就是关闭对话框，没有任何后续——文案承诺了未来行为、实现里没有。
    /// 现在真的会延后：到期前启动检查发现同一个版本就不再弹提示条。
    /// </summary>
    public string? SnoozedVersion { get; set; }

    /// <summary>「稍后提醒」的到期时间（UTC）。到点之后重新提示。</summary>
    public DateTime? SnoozedUntilUtc { get; set; }

    /// <summary>稍后提醒的默认延迟：一天（与 24 小时自动检查节流对齐）。</summary>
    public static readonly TimeSpan SnoozeDuration = TimeSpan.FromHours(24);

    private static readonly string StatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SOrtus",
        "update-state.json");

    public static UpdateState Load()
    {
        try
        {
            if (!File.Exists(StatePath))
            {
                return new UpdateState();
            }

            return JsonSerializer.Deserialize<UpdateState>(File.ReadAllText(StatePath)) ?? new UpdateState();
        }
        catch
        {
            // 状态文件损坏不应影响更新功能，直接重置
            return new UpdateState();
        }
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(StatePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(StatePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // 写入失败（磁盘满/权限）不影响主流程
        }
    }
}

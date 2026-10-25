using System.Text.Json;
using System.Text.RegularExpressions;

namespace SOrtus.Updates;

/// <summary>一次可用更新的完整描述（由 GitHub Release 解析而来）。</summary>
/// <param name="Version">新版本号。</param>
/// <param name="TagName">Release 标签（如 v0.2.0）。</param>
/// <param name="DownloadUri">安装包下载地址。</param>
/// <param name="SizeBytes">安装包字节数（用于进度与磁盘预检）。</param>
/// <param name="Sha256">安装包 SHA256（null 表示发布端未提供，调用方必须拒绝安装）。</param>
/// <param name="ReleaseNotes">更新说明（已剥离隐藏校验标记，可直接展示）。</param>
/// <param name="ReleasePageUri">Release 网页地址（下载失败时的降级出口）。</param>
public sealed record UpdateInfo(
    SemanticVersion Version,
    string TagName,
    Uri DownloadUri,
    long SizeBytes,
    string? Sha256,
    string ReleaseNotes,
    Uri? ReleasePageUri);

/// <summary>
/// GitHub Release JSON → <see cref="UpdateInfo"/> 的解析器。
/// 严格性说明：解析失败必须返回 false 并给出原因，绝不"尽力猜"——
/// 猜错的代价是下载到错误的资产后静默安装，属于最坏故障。
/// </summary>
public static partial class GitHubReleaseParser
{
    /// <summary>安装包命名约定（发布脚本必须遵守，否则此处匹配不到）。</summary>
    public const string AssetNamePrefix = "SOrtus-Setup-";

    /// <summary>
    /// 更新说明里携带 SHA256 的隐藏标记，形如 <c>&lt;!-- sortus-sha256: abc... --&gt;</c>。
    /// 放在 HTML 注释里：GitHub 页面不显示，解析器可读。
    /// </summary>
    public const string Sha256Marker = "sortus-sha256:";

    [GeneratedRegex(@"sortus-sha256:\s*([0-9a-fA-F]{64})", RegexOptions.IgnoreCase)]
    private static partial Regex Sha256Regex();

    [GeneratedRegex(@"<!--\s*sortus-sha256:[^>]*?-->", RegexOptions.IgnoreCase)]
    private static partial Regex Sha256MarkerRegex();

    /// <summary>解析 GitHub <c>releases/latest</c> 响应体。</summary>
    /// <param name="json">响应 JSON 文本。</param>
    /// <param name="info">解析成功时的更新信息。</param>
    /// <param name="error">解析失败时的原因（中文，可直接写入日志）。</param>
    public static bool TryParse(string? json, out UpdateInfo? info, out string? error)
    {
        info = null;
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "响应为空";
            return false;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            error = $"响应不是合法 JSON：{ex.Message}";
            return false;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                error = "响应不是 JSON 对象";
                return false;
            }

            // 草稿/预发布不作为更新目标，避免用户被推到测试版
            if (root.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True)
            {
                error = "最新 Release 是草稿";
                return false;
            }

            if (root.TryGetProperty("prerelease", out var prerelease) && prerelease.ValueKind == JsonValueKind.True)
            {
                error = "最新 Release 标记为预发布";
                return false;
            }

            var tagName = ReadString(root, "tag_name");
            if (string.IsNullOrWhiteSpace(tagName))
            {
                error = "Release 缺少 tag_name";
                return false;
            }

            if (!SemanticVersion.TryParse(tagName, out var version))
            {
                error = $"tag_name 无法解析为版本号：{tagName}";
                return false;
            }

            if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
            {
                error = "Release 缺少 assets";
                return false;
            }

            if (!TryPickAsset(assets, version, out var downloadUri, out var size, out var assetError))
            {
                error = assetError;
                return false;
            }

            var body = ReadString(root, "body") ?? string.Empty;
            var sha256 = ExtractSha256(body);

            var pageText = ReadString(root, "html_url");
            Uri? pageUri = null;
            if (!string.IsNullOrWhiteSpace(pageText) && Uri.TryCreate(pageText, UriKind.Absolute, out var parsedPage))
            {
                pageUri = parsedPage;
            }

            info = new UpdateInfo(
                Version: version,
                TagName: tagName,
                DownloadUri: downloadUri,
                SizeBytes: size,
                Sha256: sha256,
                ReleaseNotes: StripSha256Marker(body).Trim(),
                ReleasePageUri: pageUri);

            return true;
        }
    }

    /// <summary>从更新说明中提取 SHA256（取第一个匹配）。</summary>
    public static string? ExtractSha256(string? body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return null;
        }

        var match = Sha256Regex().Match(body);
        return match.Success ? match.Groups[1].Value.ToLowerInvariant() : null;
    }

    /// <summary>剥离隐藏校验标记，得到可直接展示给用户的更新说明。</summary>
    public static string StripSha256Marker(string? body)
    {
        if (string.IsNullOrEmpty(body))
        {
            return string.Empty;
        }

        return Sha256MarkerRegex().Replace(body, string.Empty).Trim();
    }

    /// <summary>
    /// 选择安装包资产：优先精确匹配 <c>SOrtus-Setup-{version}.exe</c>，
    /// 退化为"首个非 Framework 的 SOrtus-Setup-*.exe"。两者都没有即失败。
    /// </summary>
    private static bool TryPickAsset(
        JsonElement assets,
        SemanticVersion version,
        out Uri downloadUri,
        out long size,
        out string? error)
    {
        downloadUri = null!;
        size = 0;
        error = null;

        var expectedName = $"{AssetNamePrefix}{version}.exe";
        Uri? fallbackUri = null;
        long fallbackSize = 0;

        foreach (var asset in assets.EnumerateArray())
        {
            if (asset.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var name = ReadString(asset, "name");
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var urlText = ReadString(asset, "browser_download_url");
            if (string.IsNullOrWhiteSpace(urlText) || !Uri.TryCreate(urlText, UriKind.Absolute, out var uri))
            {
                continue;
            }

            var assetSize = asset.TryGetProperty("size", out var sizeElement)
                && sizeElement.ValueKind == JsonValueKind.Number
                && sizeElement.TryGetInt64(out var parsedSize)
                    ? parsedSize
                    : 0L;

            if (string.Equals(name, expectedName, StringComparison.OrdinalIgnoreCase))
            {
                downloadUri = uri;
                size = assetSize;
                return true;
            }

            if (fallbackUri is null
                && name.StartsWith(AssetNamePrefix, StringComparison.OrdinalIgnoreCase)
                && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                && !name.Contains("Framework", StringComparison.OrdinalIgnoreCase))
            {
                fallbackUri = uri;
                fallbackSize = assetSize;
            }
        }

        if (fallbackUri is not null)
        {
            downloadUri = fallbackUri;
            size = fallbackSize;
            return true;
        }

        error = $"未找到安装包资产（期望 {expectedName}）";
        return false;
    }

    private static string? ReadString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}

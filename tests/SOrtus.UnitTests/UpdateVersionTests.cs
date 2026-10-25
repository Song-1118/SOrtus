using SOrtus.Updates;
using Xunit;

namespace SOrtus.UnitTests;

/// <summary>
/// 版本比较与 Release 解析的单元测试。
/// 这两块是自动更新的判定核心：版本比较错一次就是「永远提示有新版本」或「永远不提示」，
/// 解析错一次就是「下载到错误的资产」。两者都必须由确定性纯函数保证，不能靠人工试。
/// </summary>
public class UpdateVersionTests
{
    [Theory]
    [InlineData("0.1.2", "0.1.10", -1)]   // 字符串比较会误判为 1，这是本用例存在的唯一理由
    [InlineData("0.1.10", "0.1.2", 1)]
    [InlineData("0.2.0", "0.1.99", 1)]
    [InlineData("1.0.0", "1.0.0", 0)]
    [InlineData("0.1.2", "0.1.2", 0)]
    [InlineData("0.10.0", "0.9.0", 1)]
    public void Compare_NumericSegments_AreOrderedNumerically(string left, string right, int expectedSign)
    {
        Assert.True(SemanticVersion.TryParse(left, out var a));
        Assert.True(SemanticVersion.TryParse(right, out var b));

        Assert.Equal(expectedSign, Math.Sign(a.CompareTo(b)));
    }

    [Fact]
    public void TryParse_AcceptsLeadingVAndBuildMetadata()
    {
        Assert.True(SemanticVersion.TryParse("v0.2.0", out var withV));
        Assert.Equal("0.2.0", withV.ToString());

        Assert.True(SemanticVersion.TryParse("0.2.0+abc123", out var withBuild));
        Assert.Equal("0.2.0", withBuild.ToString());
    }

    [Fact]
    public void PreRelease_IsLowerThanStable()
    {
        Assert.True(SemanticVersion.TryParse("1.0.0-beta.1", out var beta));
        Assert.True(SemanticVersion.TryParse("1.0.0", out var stable));

        Assert.True(beta < stable);
        Assert.False(stable < beta);
    }

    [Fact]
    public void PreRelease_ComparesNumericSegmentsNumerically()
    {
        Assert.True(SemanticVersion.TryParse("1.0.0-beta.2", out var two));
        Assert.True(SemanticVersion.TryParse("1.0.0-beta.10", out var ten));

        Assert.True(two < ten);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("1.2.3.4")]
    [InlineData("1..3")]
    [InlineData("-1.0.0")]
    [InlineData("1.0.0-")]
    public void TryParse_RejectsMalformedInput(string input)
    {
        Assert.False(SemanticVersion.TryParse(input, out _));
    }

    [Fact]
    public void Equality_IgnoresBuildMetadata()
    {
        Assert.True(SemanticVersion.TryParse("0.1.2+build1", out var a));
        Assert.True(SemanticVersion.TryParse("0.1.2+build2", out var b));

        Assert.Equal(a, b);
    }
}

/// <summary>GitHub Release JSON 解析测试（用真实响应的结构，不用臆造的字段）。</summary>
public class GitHubReleaseParserTests
{
    private const string ValidJson = """
    {
      "tag_name": "v0.2.0",
      "name": "SOrtus 0.2.0",
      "draft": false,
      "prerelease": false,
      "html_url": "https://github.com/Song-1118/SOrtus/releases/tag/v0.2.0",
      "body": "修复若干问题\n\n<!-- sortus-sha256: 0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef -->",
      "assets": [
        {
          "name": "SOrtus-Setup-0.2.0.exe",
          "size": 49545216,
          "browser_download_url": "https://github.com/Song-1118/SOrtus/releases/download/v0.2.0/SOrtus-Setup-0.2.0.exe"
        }
      ]
    }
    """;

    [Fact]
    public void TryParse_ExtractsVersionAssetAndHash()
    {
        Assert.True(GitHubReleaseParser.TryParse(ValidJson, out var info, out var error), error);
        Assert.NotNull(info);

        Assert.Equal("0.2.0", info.Version.ToString());
        Assert.Equal("v0.2.0", info.TagName);
        Assert.Equal(49545216, info.SizeBytes);
        Assert.Equal(
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            info.Sha256);
        Assert.Contains("SOrtus-Setup-0.2.0.exe", info.DownloadUri.ToString());
        Assert.NotNull(info.ReleasePageUri);
    }

    [Fact]
    public void TryParse_StripsHashMarkerFromNotes()
    {
        Assert.True(GitHubReleaseParser.TryParse(ValidJson, out var info, out _));
        Assert.NotNull(info);

        Assert.DoesNotContain("sortus-sha256", info.ReleaseNotes);
        Assert.Contains("修复若干问题", info.ReleaseNotes);
    }

    [Fact]
    public void TryParse_RejectsDraftAndPreRelease()
    {
        var draft = ValidJson.Replace("\"draft\": false", "\"draft\": true");
        Assert.False(GitHubReleaseParser.TryParse(draft, out _, out var draftError));
        Assert.NotNull(draftError);

        var pre = ValidJson.Replace("\"prerelease\": false", "\"prerelease\": true");
        Assert.False(GitHubReleaseParser.TryParse(pre, out _, out _));
    }

    [Fact]
    public void TryParse_IgnoresFrameworkDependencyAsset()
    {
        // 仓库同时上传了自包含包与框架依赖包时，必须选中自包含包
        var json = """
        {
          "tag_name": "v0.2.0",
          "draft": false,
          "prerelease": false,
          "body": "",
          "assets": [
            {
              "name": "SOrtus-Setup-0.2.0-Framework.exe",
              "size": 3069125,
              "browser_download_url": "https://example.com/framework.exe"
            },
            {
              "name": "SOrtus-Setup-0.2.0.exe",
              "size": 49545216,
              "browser_download_url": "https://example.com/selfcontained.exe"
            }
          ]
        }
        """;

        Assert.True(GitHubReleaseParser.TryParse(json, out var info, out _));
        Assert.NotNull(info);
        Assert.Contains("selfcontained.exe", info.DownloadUri.ToString());
        Assert.Null(info.Sha256); // 未提供校验值时如实返回 null，由上层拒绝安装
    }

    [Fact]
    public void TryParse_FailsWhenNoInstallerAsset()
    {
        var json = ValidJson.Replace("SOrtus-Setup-0.2.0.exe", "something-else.zip");
        Assert.False(GitHubReleaseParser.TryParse(json, out _, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void TryParse_FailsOnMalformedJson()
    {
        Assert.False(GitHubReleaseParser.TryParse("{ not json", out _, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void TryParse_FailsWhenTagIsNotVersion()
    {
        var json = ValidJson.Replace("v0.2.0", "nightly");
        Assert.False(GitHubReleaseParser.TryParse(json, out _, out var error));
        Assert.NotNull(error);
    }
}

using SOrtus.Core.Models;
using Xunit;

namespace SOrtus.UnitTests;

/// <summary>
/// 领域模型单元测试（规范：关键业务可单元测试、可脱离 UI 与数据库）。
/// </summary>
public class ProjectModelTests
{
    [Fact]
    public void SetTechStack_RoundTrips_ThroughTechStackItems()
    {
        var project = new Project();
        project.SetTechStack(new[] { "WPF", "C#", ".NET 10" });

        Assert.Equal("WPF;C#;.NET 10", project.TechStack); // 按输入顺序拼接
        Assert.Equal(3, project.TechStackItems.Count);
        Assert.Contains("WPF", project.TechStackItems);
    }

    [Fact]
    public void SetTechStack_IgnoresEmptyEntries()
    {
        var project = new Project();
        project.SetTechStack(new[] { "WPF", "  ", "", null! });

        Assert.Single(project.TechStackItems);
        Assert.Equal("WPF", project.TechStackItems[0]);
    }

    [Fact]
    public void ProgressPercent_ClampsToZeroOneHundred()
    {
        var project = new Project();
        project.ProgressPercent = 150;
        Assert.Equal(100, project.ProgressPercent);

        project.ProgressPercent = -20;
        Assert.Equal(0, project.ProgressPercent);
    }
}

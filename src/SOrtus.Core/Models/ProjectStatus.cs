namespace SOrtus.Core.Models;

/// <summary>
/// 立项状态。
/// </summary>
public enum ProjectStatus
{
    /// <summary>规划中</summary>
    Planning = 0,

    /// <summary>进行中</summary>
    Active = 1,

    /// <summary>已完成</summary>
    Completed = 2,

    /// <summary>已归档</summary>
    Archived = 3,

    /// <summary>已搁置</summary>
    OnHold = 4,
}

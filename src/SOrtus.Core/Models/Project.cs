using System.ComponentModel;

namespace SOrtus.Core.Models;

/// <summary>
/// 立项实体（基类）。三类立项共用核心字段，差异化字段在 MVP 阶段以编程类型所需列落地，
/// 后续版本通过迁移补齐剪辑/待办字段（规范：数据模型核心实体 + 表命名约定）。
/// </summary>
public class Project : INotifyPropertyChanged
{
    private int _id;
    private string _name = string.Empty;
    private ProjectType _type;
    private ProjectStatus _status;
    private DateTime _createdAt;
    private DateTime _updatedAt;
    private string _techStack = string.Empty;
    private string _summary = string.Empty;
    private string _subType = string.Empty;
    private string _folderPath = string.Empty;
    private DateTime? _scheduleStart;
    private DateTime? _scheduleEnd;
    private string _devPlan = string.Empty;
    private string _constraints = string.Empty;
    private string _goals = string.Empty;
    private string _acceptanceCriteria = string.Empty;
    private double _progressPercent;
    private string _version = string.Empty;

    /// <summary>主键</summary>
    public int Id
    {
        get => _id;
        set => SetField(ref _id, value);
    }

    /// <summary>名称（编程：项目名字 / 剪辑：作品名 / 待办：任务）</summary>
    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    /// <summary>立项类型</summary>
    public ProjectType Type
    {
        get => _type;
        set => SetField(ref _type, value);
    }

    /// <summary>状态</summary>
    public ProjectStatus Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt
    {
        get => _createdAt;
        set => SetField(ref _createdAt, value);
    }

    /// <summary>更新时间</summary>
    public DateTime UpdatedAt
    {
        get => _updatedAt;
        set => SetField(ref _updatedAt, value);
    }

    /// <summary>技术栈（`;` 分隔，可多选 + 自定义）</summary>
    public string TechStack
    {
        get => _techStack;
        set => SetField(ref _techStack, value);
    }

    /// <summary>简介</summary>
    public string Summary
    {
        get => _summary;
        set => SetField(ref _summary, value);
    }

    /// <summary>编程子类型 / 剪辑类型（Vlog/教程/微电影/纪录片/自定义）</summary>
    public string SubType
    {
        get => _subType;
        set => SetField(ref _subType, value);
    }

    /// <summary>关联文件夹（编程：项目目录 / 剪辑：素材文件夹）</summary>
    public string FolderPath
    {
        get => _folderPath;
        set => SetField(ref _folderPath, value);
    }

    /// <summary>工期起</summary>
    public DateTime? ScheduleStart
    {
        get => _scheduleStart;
        set => SetField(ref _scheduleStart, value);
    }

    /// <summary>工期止</summary>
    public DateTime? ScheduleEnd
    {
        get => _scheduleEnd;
        set => SetField(ref _scheduleEnd, value);
    }

    /// <summary>开发规划（编程）/ 拍摄进度（剪辑，后续扩展）</summary>
    public string DevPlan
    {
        get => _devPlan;
        set => SetField(ref _devPlan, value);
    }

    /// <summary>开发约束（编程）</summary>
    public string Constraints
    {
        get => _constraints;
        set => SetField(ref _constraints, value);
    }

    /// <summary>开发目标（编程）</summary>
    public string Goals
    {
        get => _goals;
        set => SetField(ref _goals, value);
    }

    /// <summary>验收标准（编程）</summary>
    public string AcceptanceCriteria
    {
        get => _acceptanceCriteria;
        set => SetField(ref _acceptanceCriteria, value);
    }

    /// <summary>进度百分比（0~100）</summary>
    public double ProgressPercent
    {
        get => _progressPercent;
        set => SetField(ref _progressPercent, Math.Clamp(value, 0, 100));
    }

    /// <summary>项目版本号（编程详情页 V2 展示与维护，可自由填写如 1.2.0）</summary>
    public string Version
    {
        get => _version;
        set => SetField(ref _version, value ?? string.Empty);
    }

    /// <summary>技术栈解析为列表（便于 UI 多选展示）。</summary>
    public IReadOnlyList<string> TechStackItems =>
        string.IsNullOrWhiteSpace(TechStack)
            ? Array.Empty<string>()
            : TechStack.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>将技术栈列表拼接为存储字符串。</summary>
    public void SetTechStack(IEnumerable<string> items) =>
        TechStack = string.Join(";", items.Where(i => !string.IsNullOrWhiteSpace(i)));

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void SetField<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

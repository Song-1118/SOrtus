using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SOrtus.Core.Models;

/// <summary>
/// 项目文档分类（规范：编程详情页 V2 文档区）。
/// <see cref="Prd"/> 与 <see cref="IterationPlan"/> 是「单槽位」分类，同一立项下至多一条；
/// <see cref="Other"/> 为普通文档，可有任意多条。
/// </summary>
public enum DocumentCategory
{
    /// <summary>产品需求文档（单槽位）</summary>
    Prd = 0,

    /// <summary>迭代规划（单槽位）</summary>
    IterationPlan = 1,

    /// <summary>其他文档（多实例）</summary>
    Other = 2,
}

/// <summary>
/// 项目文档实体。承载立项下的 Markdown 文本内容及其元信息，
/// 纯数据对象，不依赖任何 UI 框架（可在 SOrtus.Core 中安全使用）。
/// </summary>
public sealed class ProjectDocument : INotifyPropertyChanged
{
    private int _id;
    private int _projectId;
    private DocumentCategory _category = DocumentCategory.Other;
    private string _title = string.Empty;
    private string _sourceFileName = string.Empty;
    private string _format = "md";
    private string _content = string.Empty;
    private int _sortOrder;
    private DateTime _createdAt;
    private DateTime _updatedAt;

    /// <summary>主键</summary>
    public int Id
    {
        get => _id;
        set => SetField(ref _id, value);
    }

    /// <summary>所属立项 Id</summary>
    public int ProjectId
    {
        get => _projectId;
        set => SetField(ref _projectId, value);
    }

    /// <summary>文档分类</summary>
    public DocumentCategory Category
    {
        get => _category;
        set => SetField(ref _category, value);
    }

    /// <summary>文档标题（界面展示名）</summary>
    public string Title
    {
        get => _title;
        set => SetField(ref _title, value ?? string.Empty);
    }

    /// <summary>来源文件名（导入时记录，可为空）</summary>
    public string SourceFileName
    {
        get => _sourceFileName;
        set => SetField(ref _sourceFileName, value ?? string.Empty);
    }

    /// <summary>内容格式：md / txt / docx / doc</summary>
    public string Format
    {
        get => _format;
        set => SetField(ref _format, value ?? "md");
    }

    /// <summary>格式徽标文案（大写，供卡片上展示；空值回落 MD）。</summary>
    public string FormatLabel =>
        string.IsNullOrWhiteSpace(_format) ? "MD" : _format.ToUpperInvariant();

    /// <summary>文档正文（Markdown 原文）</summary>
    public string Content
    {
        get => _content;
        set => SetField(ref _content, value ?? string.Empty);
    }

    /// <summary>排序序号（同一立项内升序展示）</summary>
    public int SortOrder
    {
        get => _sortOrder;
        set => SetField(ref _sortOrder, value);
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

    /// <summary>是否为「单槽位」分类（PRD / 迭代规划）。</summary>
    public bool IsSingleton => Category is DocumentCategory.Prd or DocumentCategory.IterationPlan;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

using System.Collections.ObjectModel;
using System.Windows.Input;
using SOrtus.Common;
using SOrtus.Core.Models;
using SOrtus.Core.Repositories;
using SOrtus.Core.Services;
using SOrtus.Services;

namespace SOrtus.ViewModels;

/// <summary>
/// 立项详情视图模型（规范 F0-03 / A8 / A9 / A12；详情页 V2 三分版块）。
/// <para>
/// 编程类型分「基础信息 / 开发管理 / GIT 管理」三个版块；剪辑与待办保留原有单栏布局
/// （未实现类型的字段语义不同，不强套编程版块）。
/// </para>
/// <para>
/// 文档区：PRD 与版本迭代规划是单槽位（可覆盖导入），其他文档为多份列表；
/// 点击文档经 <see cref="IDialogService"/> 以覆盖层渲染，Markdown 待办勾选即时回写。
/// </para>
/// </summary>
internal sealed class ProjectDetailViewModel : ViewModelBase
{
    /// <summary>版块标识：基础信息。</summary>
    public const string SectionBasic = "Basic";

    /// <summary>版块标识：开发管理。</summary>
    public const string SectionDev = "Dev";

    /// <summary>版块标识：GIT 管理。</summary>
    public const string SectionGit = "Git";

    private readonly IProjectService _service;
    private readonly ILocalizationService _localization;
    private readonly IDialogService _dialogService;
    private readonly IProjectDocumentRepository? _documentRepository;
    private readonly IDocumentImportService? _importService;
    private readonly IGitService? _gitService;
    private readonly IFilePickerService? _filePicker;
    private readonly Action _onClose;
    private readonly Project _project;

    private readonly AsyncRelayCommand _selectSectionCommand;
    private readonly AsyncRelayCommand _importPrdCommand;
    private readonly AsyncRelayCommand _importPlanCommand;
    private readonly AsyncRelayCommand _importOtherCommand;
    private readonly AsyncRelayCommand _openDocumentCommand;
    private readonly AsyncRelayCommand _deleteDocumentCommand;
    private readonly AsyncRelayCommand _addTechStackCommand;
    private readonly RelayCommand _removeTechStackCommand;

    private GitViewModel? _git;
    private bool _isDirty;
    private string _activeSection = SectionBasic;
    private string _newTechStack = string.Empty;
    private string? _documentError;
    private string? _documentStatus;
    private ProjectDocument? _prdDocument;
    private ProjectDocument? _planDocument;

    public ProjectDetailViewModel(
        IProjectService service,
        ILocalizationService localization,
        IDialogService dialogService,
        Project project,
        Action onClose)
        : this(service, localization, dialogService, null, null, null, null, project, onClose)
    {
    }

    public ProjectDetailViewModel(
        IProjectService service,
        ILocalizationService localization,
        IDialogService dialogService,
        IProjectDocumentRepository? documentRepository,
        IDocumentImportService? importService,
        IGitService? gitService,
        IFilePickerService? filePicker,
        Project project,
        Action onClose)
    {
        _service = service;
        _localization = localization;
        _dialogService = dialogService;
        _documentRepository = documentRepository;
        _importService = importService;
        _gitService = gitService;
        _filePicker = filePicker;
        _project = project;
        _onClose = onClose;

        _project.PropertyChanged += OnProjectChanged;
        RebuildTechStackChips();

        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => !IsBusy);
        BackCommand = new AsyncRelayCommand(BackAsync, () => !IsBusy);

        _selectSectionCommand = new AsyncRelayCommand(SelectSectionAsync);
        _importPrdCommand = new AsyncRelayCommand(() => ImportAsync(DocumentCategory.Prd), CanUseDocuments);
        _importPlanCommand = new AsyncRelayCommand(() => ImportAsync(DocumentCategory.IterationPlan), CanUseDocuments);
        _importOtherCommand = new AsyncRelayCommand(() => ImportAsync(DocumentCategory.Other), CanUseDocuments);
        _openDocumentCommand = new AsyncRelayCommand(OpenDocumentAsync, _ => !IsBusy);
        _deleteDocumentCommand = new AsyncRelayCommand(DeleteDocumentAsync, _ => !IsBusy);
        _addTechStackCommand = new AsyncRelayCommand(AddTechStackAsync);
        _removeTechStackCommand = new RelayCommand(RemoveTechStack);

        if (CanUseDocuments())
        {
            _ = LoadDocumentsAsync();
        }
    }

    public Project Project => _project;

    public ICommand SaveCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand BackCommand { get; }

    /// <summary>版块切换（CommandParameter 为 <see cref="SectionBasic"/> / <see cref="SectionDev"/> / <see cref="SectionGit"/>）。</summary>
    public ICommand SelectSectionCommand => _selectSectionCommand;

    /// <summary>编程类型才展示三分版块。</summary>
    public bool IsProgramming => _project.Type == ProjectType.Programming;

    /// <summary>当前版块标识。</summary>
    public string ActiveSection
    {
        get => _activeSection;
        private set
        {
            if (SetProperty(ref _activeSection, value))
            {
                OnPropertyChanged(nameof(IsBasicSection));
                OnPropertyChanged(nameof(IsDevSection));
                OnPropertyChanged(nameof(IsGitSection));
                OnPropertyChanged(nameof(ShowBasicSection));
                OnPropertyChanged(nameof(ShowDevSection));
                OnPropertyChanged(nameof(ShowGitSection));
            }
        }
    }

    public bool IsBasicSection => string.Equals(_activeSection, SectionBasic, StringComparison.OrdinalIgnoreCase);

    public bool IsDevSection => string.Equals(_activeSection, SectionDev, StringComparison.OrdinalIgnoreCase);

    public bool IsGitSection => string.Equals(_activeSection, SectionGit, StringComparison.OrdinalIgnoreCase);

    /// <summary>编程类型 + 当前版块 = 基础信息（非编程类型走旧单栏布局）。</summary>
    public bool ShowBasicSection => IsProgramming && IsBasicSection;

    public bool ShowDevSection => IsProgramming && IsDevSection;

    public bool ShowGitSection => IsProgramming && IsGitSection;

    /// <summary>剪辑 / 待办沿用原有单栏布局。</summary>
    public bool ShowLegacySection => !IsProgramming;

    /// <summary>项目版本（如 v1.2.0）。</summary>
    public string Version
    {
        get => _project.Version;
        set
        {
            if (string.Equals(_project.Version, value, StringComparison.Ordinal))
            {
                return;
            }

            _project.Version = value;
            OnPropertyChanged();
            IsDirty = true;
        }
    }

    /// <summary>技术栈胶囊（由 <see cref="Project.TechStack"/> 派生，只读展示）。</summary>
    public ObservableCollection<string> TechStackChips { get; } = new();

    /// <summary>待添加的技术栈输入。</summary>
    public string NewTechStack
    {
        get => _newTechStack;
        set => SetProperty(ref _newTechStack, value);
    }

    public ICommand AddTechStackCommand => _addTechStackCommand;

    public ICommand RemoveTechStackCommand => _removeTechStackCommand;

    /// <summary>技术栈文本（分号分隔），保留给剪辑/待办等旧布局直接编辑。</summary>
    public string TechStackText
    {
        get => _project.TechStack;
        set
        {
            if (string.Equals(_project.TechStack, value, StringComparison.Ordinal))
            {
                return;
            }

            _project.TechStack = value;
            IsDirty = true;
        }
    }

    /// <summary>PRD 文档（单槽位，可为空）。</summary>
    public ProjectDocument? PrdDocument
    {
        get => _prdDocument;
        private set
        {
            if (SetProperty(ref _prdDocument, value))
            {
                OnPropertyChanged(nameof(HasPrd));
                OnPropertyChanged(nameof(IsPrdEmpty));
                OnPropertyChanged(nameof(PrdMetaText));
            }
        }
    }

    /// <summary>版本迭代规划文档（单槽位，可为空）。</summary>
    public ProjectDocument? PlanDocument
    {
        get => _planDocument;
        private set
        {
            if (SetProperty(ref _planDocument, value))
            {
                OnPropertyChanged(nameof(HasPlan));
                OnPropertyChanged(nameof(IsPlanEmpty));
                OnPropertyChanged(nameof(PlanMetaText));
            }
        }
    }

    public ObservableCollection<ProjectDocument> OtherDocuments { get; } = new();

    public bool HasPrd => _prdDocument is not null;

    public bool IsPrdEmpty => _prdDocument is null;

    public bool HasPlan => _planDocument is not null;

    public bool IsPlanEmpty => _planDocument is null;

    public bool HasOtherDocuments => OtherDocuments.Count > 0;

    public bool IsOtherDocumentsEmpty => OtherDocuments.Count == 0;

    /// <summary>PRD 卡片元信息（格式 · 更新时间）。</summary>
    public string PrdMetaText => BuildMetaText(_prdDocument);

    public string PlanMetaText => BuildMetaText(_planDocument);

    /// <summary>文档操作错误（导入失败 / 保存失败），空表示无错误。</summary>
    public string? DocumentError
    {
        get => _documentError;
        private set
        {
            if (SetProperty(ref _documentError, value))
            {
                OnPropertyChanged(nameof(HasDocumentError));
            }
        }
    }

    public bool HasDocumentError => !string.IsNullOrEmpty(_documentError);

    /// <summary>文档操作提示（导入成功 / 已忽略图片等）。</summary>
    public string? DocumentStatus
    {
        get => _documentStatus;
        private set
        {
            if (SetProperty(ref _documentStatus, value))
            {
                OnPropertyChanged(nameof(HasDocumentStatus));
            }
        }
    }

    public bool HasDocumentStatus => !string.IsNullOrEmpty(_documentStatus);

    /// <summary>文档功能是否可用（服务缺失时降级：按钮禁用，不误导用户）。</summary>
    public bool IsDocumentFeatureAvailable => CanUseDocuments();

    public ICommand ImportPrdCommand => _importPrdCommand;

    public ICommand ImportPlanCommand => _importPlanCommand;

    public ICommand ImportOtherCommand => _importOtherCommand;

    public ICommand OpenDocumentCommand => _openDocumentCommand;

    public ICommand DeleteDocumentCommand => _deleteDocumentCommand;

    /// <summary>GIT 版块视图模型（无 Git 服务时为 null，界面显示不可用提示）。</summary>
    public GitViewModel? Git
    {
        get
        {
            if (_gitService is null)
            {
                return null;
            }

            return _git ??= new GitViewModel(_gitService, _localization, _dialogService, _project);
        }
    }

    public bool IsGitAvailable => _gitService is not null;

    /// <summary>可编辑的状态选项（状态影响列表筛选，详情页必须能改）。</summary>
    public ObservableCollection<ProjectStatus> StatusOptions { get; } = new()
    {
        ProjectStatus.Planning,
        ProjectStatus.Active,
        ProjectStatus.OnHold,
        ProjectStatus.Completed,
        ProjectStatus.Archived,
    };

    /// <summary>项目状态（可直接在详情页切换）。</summary>
    public ProjectStatus Status
    {
        get => _project.Status;
        set
        {
            _project.Status = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusDisplay));
        }
    }

    /// <summary>状态下拉的本地化文本（复用列表胶囊文案）。</summary>
    public string StatusDisplay => _localization.GetString(_project.Status switch
    {
        ProjectStatus.Planning => "List_StatusPlanning",
        ProjectStatus.Active => "List_StatusActive",
        ProjectStatus.Completed => "List_StatusCompleted",
        ProjectStatus.OnHold => "List_StatusOnHold",
        ProjectStatus.Archived => "List_StatusArchived",
        _ => "List_StatusPlanning",
    });

    /// <summary>创建时间（本地时区展示）。</summary>
    public string CreatedText => string.Format(
        _localization.GetString("Detail_MetaCreated"), _project.CreatedAt.ToLocalTime());

    /// <summary>更新时间（本地时区展示，保存后刷新）。</summary>
    public string UpdatedText => string.Format(
        _localization.GetString("Detail_MetaUpdated"), _project.UpdatedAt.ToLocalTime());

    /// <summary>是否存在未保存更改（驱动详情页「未保存」徽标与离开拦截）。</summary>
    public bool IsDirty
    {
        get => _isDirty;
        private set => SetProperty(ref _isDirty, value);
    }

    /// <summary>工期展示文本（yyyy-MM-dd 至 yyyy-MM-dd；未设置为「未设置」）。</summary>
    public string ScheduleDisplay
    {
        get
        {
            if (_project.ScheduleStart is null && _project.ScheduleEnd is null)
            {
                return _localization.GetString("Detail_ScheduleEmpty");
            }

            var format = _localization.GetString("Detail_ScheduleText");
            var start = _project.ScheduleStart ?? DateTime.Today;
            var end = _project.ScheduleEnd ?? DateTime.Today;
            return string.Format(format, start, end);
        }
    }

    /// <summary>
    /// 离开前的守卫（导航切换 / 关闭窗口共用）：有未保存更改时询问「保存 / 不保存 / 取消」。
    /// 返回 true 表示可以离开。
    /// </summary>
    public async Task<bool> TryLeaveAsync()
    {
        if (!IsDirty)
        {
            return true;
        }

        var choice = await _dialogService.ConfirmUnsavedAsync(
            _localization.GetString("Dialog_UnsavedTitle"),
            string.Format(_localization.GetString("Dialog_UnsavedMessage"), _project.Name));

        switch (choice)
        {
            case UnsavedChangesChoice.Save:
                await SaveAsync();
                return !IsDirty;

            case UnsavedChangesChoice.Discard:
                return true;

            default:
                return false;
        }
    }

    private bool CanUseDocuments() =>
        _documentRepository is not null && _importService is not null && _filePicker is not null;

    private async Task SelectSectionAsync(object? parameter)
    {
        var section = parameter as string ?? SectionBasic;
        ActiveSection = section;

        if (IsGitSection)
        {
            // 每次进入 GIT 版块都重取快照：用户可能刚改过关联目录，或刚在外部推了代码
            await (Git?.ReloadAsync() ?? Task.CompletedTask);
        }
    }

    private async Task LoadDocumentsAsync()
    {
        if (_documentRepository is null)
        {
            return;
        }

        try
        {
            var documents = await _documentRepository.GetByProjectAsync(_project.Id);

            PrdDocument = documents.FirstOrDefault(d => d.Category == DocumentCategory.Prd);
            PlanDocument = documents.FirstOrDefault(d => d.Category == DocumentCategory.IterationPlan);

            OtherDocuments.Clear();
            foreach (var doc in documents
                         .Where(d => d.Category == DocumentCategory.Other)
                         .OrderBy(d => d.SortOrder)
                         .ThenBy(d => d.Id))
            {
                OtherDocuments.Add(doc);
            }

            OnPropertyChanged(nameof(HasOtherDocuments));
            OnPropertyChanged(nameof(IsOtherDocumentsEmpty));
        }
        catch (Exception ex)
        {
            DocumentError = ex.Message;
        }
    }

    private async Task ImportAsync(DocumentCategory category)
    {
        if (!CanUseDocuments())
        {
            return;
        }

        var path = _filePicker!.PickDocument();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        DocumentError = null;
        DocumentStatus = null;

        try
        {
            var imported = await _importService!.ImportAsync(path);

            var document = category == DocumentCategory.Other
                ? null
                : await _documentRepository!.GetSlotAsync(_project.Id, category);

            document ??= new ProjectDocument
            {
                ProjectId = _project.Id,
                Category = category,
                CreatedAt = DateTime.UtcNow,
            };

            document.Title = imported.Title;
            document.SourceFileName = imported.SourceFileName;
            document.Format = imported.Format;
            document.Content = imported.Content;
            document.UpdatedAt = DateTime.UtcNow;

            if (category == DocumentCategory.Other && document.SortOrder == 0)
            {
                document.SortOrder = await _documentRepository!.GetNextSortOrderAsync(_project.Id);
            }

            document.Id = await _documentRepository!.UpsertAsync(document);
            await LoadDocumentsAsync();

            var status = string.Format(_localization.GetString("Doc_Imported"), imported.Title);
            if (imported.Warnings.Count > 0)
            {
                status = $"{status} · {string.Join("；", imported.Warnings)}";
            }

            DocumentStatus = status;
        }
        catch (DocumentImportException ex)
        {
            DocumentError = ex.UserMessage;
        }
        catch (Exception ex)
        {
            DocumentError = ex.Message;
        }
    }

    private async Task OpenDocumentAsync(object? parameter)
    {
        if (parameter is not ProjectDocument document || _documentRepository is null)
        {
            return;
        }

        var dialog = new DocumentDialogViewModel(
            document,
            _documentRepository,
            new MarkdownRenderer(),
            _localization);

        await _dialogService.ShowAsync(dialog);
    }

    private async Task DeleteDocumentAsync(object? parameter)
    {
        if (parameter is not ProjectDocument document || _documentRepository is null)
        {
            return;
        }

        var confirmed = await _dialogService.ConfirmAsync(
            _localization.GetString("Doc_DeleteTitle"),
            string.Format(_localization.GetString("Doc_DeleteMessage"), document.Title));

        if (!confirmed)
        {
            return;
        }

        try
        {
            await _documentRepository.DeleteAsync(document.Id);
            DocumentStatus = null;
            await LoadDocumentsAsync();
        }
        catch (Exception ex)
        {
            DocumentError = ex.Message;
        }
    }

    private Task AddTechStackAsync()
    {
        var value = NewTechStack?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return Task.CompletedTask;
        }

        if (_project.TechStackItems.Any(item => string.Equals(item, value, StringComparison.OrdinalIgnoreCase)))
        {
            NewTechStack = string.Empty;
            return Task.CompletedTask;
        }

        _project.SetTechStack(_project.TechStackItems.Append(value));
        NewTechStack = string.Empty;
        IsDirty = true;
        return Task.CompletedTask;
    }

    private void RemoveTechStack(object? parameter)
    {
        if (parameter is not string value)
        {
            return;
        }

        _project.SetTechStack(_project.TechStackItems
            .Where(item => !string.Equals(item, value, StringComparison.OrdinalIgnoreCase)));
        IsDirty = true;
    }

    private void RebuildTechStackChips()
    {
        TechStackChips.Clear();
        foreach (var item in _project.TechStackItems)
        {
            TechStackChips.Add(item);
        }
    }

    private string BuildMetaText(ProjectDocument? document)
    {
        if (document is null)
        {
            return string.Empty;
        }

        var format = _localization.GetString("Doc_MetaFormat");
        var stamp = document.UpdatedAt == default
            ? string.Empty
            : document.UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

        return string.Format(format, document.Format.ToUpperInvariant(), stamp);
    }

    private void OnProjectChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        IsDirty = true;

        if (e.PropertyName == nameof(Project.TechStack))
        {
            RebuildTechStackChips();
            OnPropertyChanged(nameof(TechStackText));
        }

        // 保存过程中 UpdatedAt 变化时刷新页脚时间
        if (e.PropertyName == nameof(Project.UpdatedAt))
        {
            OnPropertyChanged(nameof(UpdatedText));
        }
    }

    private async Task SaveAsync()
    {
        await RunBusyAsync(async () =>
        {
            _project.SetTechStack(_project.TechStack.Split(
                ';',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            await _service.SaveAsync(_project);
            IsDirty = false;
            OnPropertyChanged(nameof(UpdatedText));
        });
    }

    private async Task DeleteAsync()
    {
        var confirmed = await _dialogService.ConfirmAsync(
            _localization.GetString("Dialog_DeleteTitle"),
            string.Format(_localization.GetString("Dialog_DeleteMessage"), _project.Name));

        if (!confirmed)
        {
            return;
        }

        await _service.DeleteAsync(_project.Id);
        _onClose();
    }

    private async Task BackAsync()
    {
        if (await TryLeaveAsync())
        {
            _onClose();
        }
    }
}

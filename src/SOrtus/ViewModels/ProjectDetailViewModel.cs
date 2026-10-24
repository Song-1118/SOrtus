using System.Collections.ObjectModel;
using System.Windows.Input;
using SOrtus.Common;
using SOrtus.Core.Models;
using SOrtus.Core.Services;
using SOrtus.Services;

namespace SOrtus.ViewModels;

/// <summary>
/// 立项详情视图模型（规范 F0-03 / A8 / A9 / A12）：
/// 查看与编辑立项后字段（状态/开发规划/约束/目标/验收标准/进度），保存/删除，
/// 离开时若有未保存变更给出「保存 / 不保存 / 取消」三选提示
/// （统一走 IDialogService 覆盖层，不用 MessageBox）。
/// </summary>
internal sealed class ProjectDetailViewModel : ViewModelBase
{
    private readonly IProjectService _service;
    private readonly ILocalizationService _localization;
    private readonly IDialogService _dialogService;
    private readonly Action _onClose;
    private readonly Project _project;
    private bool _isDirty;

    public Project Project => _project;

    public ICommand SaveCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand BackCommand { get; }

    /// <summary>可编辑的状态选项（状态影响列表筛选，详情页必须能改）。</summary>
    public ObservableCollection<ProjectStatus> StatusOptions { get; } = new()
    {
        ProjectStatus.Planning,
        ProjectStatus.Active,
        ProjectStatus.OnHold,
        ProjectStatus.Completed,
        ProjectStatus.Archived,
    };

    public ProjectDetailViewModel(
        IProjectService service,
        ILocalizationService localization,
        IDialogService dialogService,
        Project project,
        Action onClose)
    {
        _service = service;
        _localization = localization;
        _dialogService = dialogService;
        _project = project;
        _onClose = onClose;

        _project.PropertyChanged += OnProjectChanged;

        SaveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => !IsBusy);
        BackCommand = new AsyncRelayCommand(BackAsync, () => !IsBusy);
    }

    /// <summary>技术栈文本（分号分隔），便于在详情中以文本框编辑。</summary>
    public string TechStackText
    {
        get => _project.TechStack;
        set
        {
            _project.TechStack = value;
            IsDirty = true;
        }
    }

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

    private void OnProjectChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        IsDirty = true;

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
            _project.SetTechStack(
                _project.TechStack.Split(';', ',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
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

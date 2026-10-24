using System.Collections.ObjectModel;
using System.IO;
using Microsoft.Win32;
using System.Windows.Input;
using SOrtus.Common;
using SOrtus.Core.Models;
using SOrtus.Core.Services;

namespace SOrtus.ViewModels;

/// <summary>技术栈预设项（向导「预设多选 chips」，规范 A6）：选中即加入技术栈，取消即移出。</summary>
internal sealed class TechStackPreset : ViewModelBase
{
    private bool _isSelected;

    public TechStackPreset(string name) => Name = name;

    public string Name { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

/// <summary>
/// 立项向导视图模型（规范 F0-01 / A5 / A6）——两步向导：
/// 第 1 步选择立项类型（编程 / 剪辑 / 待办），第 2 步填写立项信息。
/// 项目名字（必填）、技术栈（预设多选 + 自定义输入，可多选）、简介、子类型、关联文件夹、开发工期。
/// </summary>
internal sealed class ProjectWizardViewModel : ViewModelBase
{
    private readonly IProjectService _service;
    private readonly ILocalizationService _localization;
    private readonly Action _onCreated;

    private string _name = string.Empty;
    private string _techStackInput = string.Empty;
    private string _summary = string.Empty;
    private string _subType = string.Empty;
    private string _folderPath = string.Empty;
    private DateTime? _scheduleStart;
    private DateTime? _scheduleEnd;
    private string _errorMessage = string.Empty;
    private int _step = 1;
    private ProjectType _selectedType = ProjectType.Programming;

    public ObservableCollection<string> TechStacks { get; } = new();

    /// <summary>常用技术栈预设（点击切换选中，等价于多选 chips）。</summary>
    public ObservableCollection<TechStackPreset> TechStackPresets { get; } = new();

    public ICommand AddTechStackCommand { get; }
    public ICommand RemoveTechStackCommand { get; }
    public ICommand TogglePresetCommand { get; }
    public ICommand BrowseFolderCommand { get; }
    public ICommand CreateCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand NextStepCommand { get; }
    public ICommand PreviousStepCommand { get; }
    public ICommand SelectTypeCommand { get; }

    /// <summary>第 1 步可选的立项类型。</summary>
    public List<ProjectType> TypeOptions { get; } = new() { ProjectType.Programming, ProjectType.Clip, ProjectType.Todo };

    /// <summary>项目子类型选项（本地化，构造时取值；切换语言后重新进入向导即刷新）。</summary>
    public List<string> SubTypeOptions { get; }

    /// <summary>当前步骤（1 = 选择类型，2 = 填写信息）。</summary>
    public int Step
    {
        get => _step;
        private set
        {
            if (SetProperty(ref _step, value))
            {
                OnPropertyChanged(nameof(IsStepType));
                OnPropertyChanged(nameof(IsStepInfo));
            }
        }
    }

    public bool IsStepType => Step == 1;
    public bool IsStepInfo => Step == 2;

    /// <summary>已选择的立项类型。</summary>
    public ProjectType SelectedType
    {
        get => _selectedType;
        set
        {
            if (SetProperty(ref _selectedType, value))
            {
                OnPropertyChanged(nameof(SelectedTypeName));
            }
        }
    }

    /// <summary>已选类型的本地化名称（向导第 2 步头部展示）。</summary>
    public string SelectedTypeName => _localization.GetString(_selectedType switch
    {
        ProjectType.Programming => "List_TypeProgramming",
        ProjectType.Clip => "List_TypeClip",
        ProjectType.Todo => "List_TypeTodo",
        _ => "List_TypeAll",
    });

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string TechStackInput
    {
        get => _techStackInput;
        set => SetProperty(ref _techStackInput, value);
    }

    public string Summary
    {
        get => _summary;
        set => SetProperty(ref _summary, value);
    }

    public string SubType
    {
        get => _subType;
        set => SetProperty(ref _subType, value);
    }

    public string FolderPath
    {
        get => _folderPath;
        set => SetProperty(ref _folderPath, value);
    }

    public DateTime? ScheduleStart
    {
        get => _scheduleStart;
        set => SetProperty(ref _scheduleStart, value);
    }

    public DateTime? ScheduleEnd
    {
        get => _scheduleEnd;
        set => SetProperty(ref _scheduleEnd, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public ProjectWizardViewModel(IProjectService service, ILocalizationService localization, Action onCreated)
    {
        _service = service;
        _localization = localization;
        _onCreated = onCreated;

        SubTypeOptions = new List<string>
        {
            localization.GetString("Wizard_SubTypeApp"),
            localization.GetString("Wizard_SubTypeLib"),
            localization.GetString("Wizard_SubTypeWeb"),
            localization.GetString("Wizard_SubTypeGame"),
            localization.GetString("Wizard_SubTypeOther"),
        };

        AddTechStackCommand = new RelayCommand(_ => AddTechStack());
        RemoveTechStackCommand = new RelayCommand(o => RemoveTechStack(o as string));
        TogglePresetCommand = new RelayCommand(o => TogglePreset(o as TechStackPreset));
        BrowseFolderCommand = new RelayCommand(_ => BrowseFolder());
        CreateCommand = new AsyncRelayCommand(CreateAsync, () => !IsBusy);
        CancelCommand = new RelayCommand(_ => _onCreated());
        NextStepCommand = new RelayCommand(_ => Step = 2);
        PreviousStepCommand = new RelayCommand(_ => Step = 1);
        SelectTypeCommand = new RelayCommand(o =>
        {
            if (o is ProjectType t)
            {
                SelectedType = t;
            }
        });

        InitializePresets();
    }

    /// <summary>技术栈预设（常用项；顺序即展示顺序）。</summary>
    private static readonly string[] DefaultPresets =
    {
        "C#", ".NET", "WPF", "WinUI", "MAUI", "Python",
        "TypeScript", "React", "Vue", "Node.js", "Rust", "Go",
    };

    private void InitializePresets()
    {
        foreach (var name in DefaultPresets)
        {
            TechStackPresets.Add(new TechStackPreset(name));
        }
    }

    private void TogglePreset(TechStackPreset? preset)
    {
        if (preset is null)
        {
            return;
        }

        preset.IsSelected = !preset.IsSelected;
        if (preset.IsSelected)
        {
            AddToTechStacks(preset.Name);
        }
        else
        {
            TechStacks.Remove(preset.Name);
        }
    }

    private void AddTechStack()
    {
        // 支持一次输入多个（逗号 / 分号 / 顿号分隔），提升录入效率
        var values = TechStackInput.Split(new[] { ',', '，', ';', '；', '、' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var value in values)
        {
            AddToTechStacks(value);
        }

        TechStackInput = string.Empty;
    }

    private void AddToTechStacks(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || TechStacks.Contains(value))
        {
            return;
        }

        TechStacks.Add(value);

        // 与预设同名（忽略大小写）时同步点亮对应 chip，保持两种录入方式状态一致
        var preset = TechStackPresets.FirstOrDefault(
            p => string.Equals(p.Name, value, StringComparison.OrdinalIgnoreCase));
        if (preset is not null)
        {
            preset.IsSelected = true;
        }
    }

    private void RemoveTechStack(string? value)
    {
        if (string.IsNullOrEmpty(value) || !TechStacks.Remove(value))
        {
            return;
        }

        var preset = TechStackPresets.FirstOrDefault(
            p => string.Equals(p.Name, value, StringComparison.OrdinalIgnoreCase));
        if (preset is not null)
        {
            preset.IsSelected = false;
        }
    }

    private void BrowseFolder()
    {
        // WPF 无内置文件夹对话框；通过选择一个文件取其所在目录作为关联文件夹（关联文件夹为选填）。
        var dialog = new OpenFileDialog
        {
            Title = _localization.GetString("Wizard_FolderBrowse"),
            CheckFileExists = false,
            FileName = "folder",
        };

        if (dialog.ShowDialog() == true)
        {
            FolderPath = Path.GetDirectoryName(dialog.FileName) ?? FolderPath;
        }
    }

    private async Task CreateAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ErrorMessage = _localization.GetString("Common_RequiredName");
            return;
        }

        if (TechStacks.Count == 0)
        {
            // PRD 5.1.1：技术栈为必填项
            ErrorMessage = _localization.GetString("Common_RequiredTechStack");
            return;
        }

        ErrorMessage = string.Empty;

        var project = new Project
        {
            Name = Name.Trim(),
            Type = SelectedType,
            Status = ProjectStatus.Planning,
            Summary = Summary,
            SubType = SubType,
            FolderPath = FolderPath,
            ScheduleStart = ScheduleStart,
            ScheduleEnd = ScheduleEnd,
        };
        project.SetTechStack(TechStacks);

        await _service.CreateAsync(project);
        _onCreated();
    }
}

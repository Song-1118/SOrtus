using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using SOrtus.Core.Models;
using SOrtus.Core.Repositories;
using SOrtus.Core.Services;
using SOrtus.Navigation;
using SOrtus.Repositories;
using SOrtus.Services;
using SOrtus.Updates;
using SOrtus.ViewModels;

namespace SOrtus;

/// <summary>
/// 应用入口与组合根（规范：分层与依赖注入）。
/// 负责数据库迁移、服务装配、主题/本地化初始化，并启动主窗口。
/// 支持 <c>--smoke</c> 自检模式：无窗口实例化所有自定义控件与视图模板、切换主题/语言，
/// 验证视觉树非空（不触碰真实数据库，不弹窗），用于构建后冒烟验证。
/// </summary>
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args is { Length: > 0 } && e.Args[0] == "--smoke")
        {
            var code = RunSmoke();
            Shutdown(code);
            return;
        }

        // 截图模式（文档 / 资源库素材）：离屏渲染四张核心界面，不触碰真实数据库
        if (e.Args is { Length: > 0 } && e.Args[0] == "--shot")
        {
            var target = e.Args.Length > 1 ? e.Args[1] : System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sortus-shots");
            var width = e.Args.Length > 2 && double.TryParse(e.Args[2], out var w) ? w : 1180;
            var height = e.Args.Length > 3 && double.TryParse(e.Args[3], out var h) ? h : 760;
            await RunShotsAsync(target, width, height);
            Shutdown(0);
            return;
        }

        // 单实例守护：自动更新安装完成后，安装器的 RestartApplications 与本程序 [Run] 条目
        // 可能各拉起一次；若旧进程尚未完全退出，就会出现两个窗口。命名互斥量在此兜底。
        if (!TryAcquireSingleInstance())
        {
            Shutdown(0);
            return;
        }

        // 首次运行自动创建 SQLite 数据库与立项基表（验收 A4）
        try
        {
            await MigrationRunner.RunAsync();
        }
        catch (System.Exception ex)
        {
            MessageBox.Show($"数据库初始化失败：{ex.Message}", "SOrtus", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }
        // 服务装配（跨层通信优先接口 + 依赖注入，规范 6.3）
        IProjectRepository repository = new ProjectRepository();
        IProjectService projectService = new ProjectService(repository);
        IProjectDocumentRepository documentRepository = new ProjectDocumentRepository();
        IThemeService themeService = new ThemeService();
        themeService.Initialize();
        ILocalizationService localizationService = new LocalizationService();
        localizationService.Initialize();
        INavigationService navigation = new NavigationService();
        IDialogService dialogService = new DialogService(localizationService);
        IDocumentImportService importService = new DocumentImportService();
        IGitService gitService = new GitService();
        IFilePickerService filePicker = new FilePickerService(localizationService);

        var mainViewModel = new MainViewModel(
            projectService,
            themeService,
            localizationService,
            navigation,
            dialogService,
            documentRepository,
            importService,
            gitService,
            filePicker);
        mainViewModel.Initialize();

        var window = new MainWindow { DataContext = mainViewModel };
        window.Show();

        // VD-067：换肤过渡由窗口承载（淡出→换字典→淡入），服务只负责换字典与广播
        themeService.ThemeChanged += (_, _) => window.PlayThemeTransition(themeService.CurrentTheme);

        ScheduleStartupUpdateCheck(mainViewModel);
    }

    /// <summary>单实例互斥量。持有到进程退出，由 OS 在进程终止时自动释放。</summary>
    private static Mutex? _singleInstanceMutex;

    /// <summary>
    /// 尝试获取单实例锁。首次返回 true；已有实例在运行时返回 false。
    /// </summary>
    private static bool TryAcquireSingleInstance()
    {
        try
        {
            _singleInstanceMutex = new Mutex(initiallyOwned: true, @"Local\SOrtus.SingleInstance", out var createdNew);
            return createdNew;
        }
        catch (Exception ex)
        {
            // 互斥量创建失败（极端权限/命名冲突）时放行，宁可双开也不能打不开程序
            UpdateService.Log($"单实例互斥量创建失败：{ex.Message}");
            return true;
        }
    }

    /// <summary>
    /// 启动后延迟 5 秒再查更新：先让窗口完成首帧渲染与数据库加载，
    /// 避免把网络耗时叠进启动时间。检查本身 5 秒超时、失败静默。
    /// VD-080：旧值 3 秒，正好落在用户刚开始首屏操作的时间窗里；
    /// 提示条已改为浮层不占布局，但仍不应在用户第一眼就出现。
    /// </summary>
    private static void ScheduleStartupUpdateCheck(MainViewModel mainViewModel)
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        timer.Tick += async (_, _) =>
        {
            timer.Stop();
            try
            {
                await mainViewModel.Updates.CheckAsync(force: false);
            }
            catch (Exception ex)
            {
                Services.UpdateService.Log($"启动检查更新异常：{ex}");
            }
        };
        timer.Start();
    }

    /// <summary>
    /// 截图模式：以离屏窗口渲染核心界面并保存 PNG，供文档与资源库素材使用。
    /// 使用无副作用的桩服务，不读写真实数据库。
    /// </summary>
    private async Task RunShotsAsync(string targetDir, double width, double height)
    {
        System.IO.Directory.CreateDirectory(targetDir);

        var themeService = new ThemeService();
        themeService.Initialize();
        // 换肤验证会写回用户设置，结束时必须还原成进来时的主题
        var originalTheme = themeService.CurrentTheme;
        var localization = new LocalizationService();
        localization.Initialize();

        var projectService = new SmokeProjectService();
        var navigation = new NavigationService();
        // 截图需要真实覆盖层承载：这里用真实 DialogService（只渲染，不弹系统窗口）
        var dialogService = new DialogService(localization);
        var mainViewModel = new MainViewModel(
            projectService,
            themeService,
            localization,
            navigation,
            dialogService,
            new SmokeDocumentRepository(),
            new DocumentImportService(),
            new SmokeGitService(),
            new SmokeFilePickerService());
        mainViewModel.Initialize();

        var window = new MainWindow
        {
            DataContext = mainViewModel,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            Width = width,
            Height = height,
        };
        window.Show();
        await SettleAsync(window);
        SaveWindow(window, System.IO.Path.Combine(targetDir, "01-list-empty.png"));

        await mainViewModel.ShowNewProjectAsync();
        await SettleAsync(window);
        SaveWindow(window, System.IO.Path.Combine(targetDir, "02-wizard-step1.png"));

        if (navigation.Current is ProjectWizardViewModel wizard)
        {
            wizard.SelectTypeCommand.Execute(ProjectType.Programming);
            wizard.NextStepCommand.Execute(null);
            wizard.Name = "SOrtus 桌面端";
            wizard.Summary = "本地优先的立项管理工具，打通编程立项全流程。";
            wizard.SubType = wizard.SubTypeOptions.FirstOrDefault() ?? string.Empty;
            wizard.ScheduleStart = DateTime.Today;
            wizard.ScheduleEnd = DateTime.Today.AddMonths(2);
            wizard.TogglePresetCommand.Execute(wizard.TechStackPresets.First(p => p.Name == "C#"));
            wizard.TogglePresetCommand.Execute(wizard.TechStackPresets.First(p => p.Name == "WPF"));
            wizard.TogglePresetCommand.Execute(wizard.TechStackPresets.First(p => p.Name == ".NET"));
        }

        await SettleAsync(window);
        SaveWindow(window, System.IO.Path.Combine(targetDir, "03-wizard-step2.png"));

        var sample = new Project
        {
            Id = 1,
            Name = "SOrtus 桌面端",
            Type = ProjectType.Programming,
            Status = ProjectStatus.Active,
            Summary = "本地优先的立项管理工具，打通编程立项全流程。",
            SubType = "桌面应用",
            FolderPath = @"D:\code\sortus",
            TechStack = "C#;WPF;.NET",
            ScheduleStart = DateTime.Today,
            ScheduleEnd = DateTime.Today.AddMonths(2),
            DevPlan = "1. 工程骨架与分层落地\n2. 编程立项闭环\n3. 视觉与主题打磨",
            Constraints = "纯本地运行，不引入云服务与账号体系。",
            Goals = "MVP 阶段跑通「创建 → 查看 → 编辑 → 重启仍在」完整路径。",
            AcceptanceCriteria = "构建零错误；数据重启不丢失；模糊不作用于内容层。",
            ProgressPercent = 65,
            CreatedAt = DateTime.Today.AddDays(-14),
            UpdatedAt = DateTime.Now,
        };
        mainViewModel.ShowDetail(sample);
        await SettleAsync(window);
        SaveWindow(window, System.IO.Path.Combine(targetDir, "04-detail-basic.png"));

        if (navigation.Current is ProjectDetailViewModel detail)
        {
            detail.SelectSectionCommand.Execute(ProjectDetailViewModel.SectionDev);
            await SettleAsync(window);
            SaveWindow(window, System.IO.Path.Combine(targetDir, "05-detail-dev.png"));

            detail.SelectSectionCommand.Execute(ProjectDetailViewModel.SectionGit);
            await SettleAsync(window);
            SaveWindow(window, System.IO.Path.Combine(targetDir, "06-detail-git.png"));

            detail.SelectSectionCommand.Execute(ProjectDetailViewModel.SectionDev);
            await SettleAsync(window);

            var document = detail.PrdDocument ?? detail.OtherDocuments.FirstOrDefault();
            if (document is not null)
            {
                detail.OpenDocumentCommand.Execute(document);
                await SettleAsync(window);
                SaveWindow(window, System.IO.Path.Combine(targetDir, "07-document-dialog.png"));
                mainViewModel.CloseDialogCommand.Execute(null);
                await SettleAsync(window);
            }
        }

        await mainViewModel.ShowSettingsAsync();
        await SettleAsync(window);
        SaveWindow(window, System.IO.Path.Combine(targetDir, "08-settings.png"));

        // VD-061 回归用：运行时整本换肤（不是启动即深色），验证分组卡 / 文字 / 边框是否一起跟随。
        // 09 = 切到浅色后的设置页，10 = 再切回深色后的同一页——两张都会进交付证据。
        themeService.SetTheme("Light");
        await SettleAsync(window);
        SaveWindow(window, System.IO.Path.Combine(targetDir, "09-settings-after-theme-switch-light.png"));

        themeService.SetTheme("Dark");
        await SettleAsync(window);
        SaveWindow(window, System.IO.Path.Combine(targetDir, "10-settings-after-theme-switch-dark.png"));

        // VD-080：更新提示条改为浮层后不能靠「看不见」来验证，这里强制置为可见截一张，
        // 确认它是浮在内容右上角、不挤压页面布局。
        if (SemanticVersion.TryParse("0.3.1", out var previewVersion))
        {
            mainViewModel.Updates.PreviewBanner(new UpdateInfo(
                previewVersion,
                "v0.3.1",
                new Uri("https://example.com/SOrtus-Setup-0.3.1.exe"),
                148_000_000,
                null,
                "-",
                null));

            await mainViewModel.ShowProjectsAsync();
            await SettleAsync(window);
            SaveWindow(window, System.IO.Path.Combine(targetDir, "11-update-toast.png"));
        }

        // VD-060 回归用：最大化。验证 WM_GETMINMAXINFO 把尺寸钳到工作区、外距 22 收为 0，
        // 且自绘顶栏在最大化下仍然贴边完整（不会被系统边框挤出屏幕）。
        window.WindowState = WindowState.Maximized;
        await SettleAsync(window);
        SaveWindow(window, System.IO.Path.Combine(targetDir, "12-maximized.png"));
        window.WindowState = WindowState.Normal;

        // 还原用户进来时的主题（SetTheme 会持久化到 settings.json）
        themeService.SetTheme(originalTheme);

        window.Close();
    }

    private static IEnumerable<System.Windows.DependencyObject> Descendants(System.Windows.DependencyObject root)
    {
        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var nested in Descendants(child))
            {
                yield return nested;
            }
        }
    }

    private static async Task SettleAsync(Window window)
    {
        window.UpdateLayout();
        await window.Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        // 420ms：页面入场 220ms + 弹窗弹入 240ms，等动画走完再截，否则截图里是半透明的中间帧
        await Task.Delay(420);
        window.UpdateLayout();
    }

    private static void SaveWindow(Window window, string path)
    {
        // 截图用无边框窗口（客户区 = 窗口尺寸），直接渲染窗口即可 1:1 映射，不含非客户区偏移
        var width = Math.Max(1, (int)Math.Round(window.ActualWidth));
        var height = Math.Max(1, (int)Math.Round(window.ActualHeight));
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(
            width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(window);

        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var stream = System.IO.File.Create(path);
        encoder.Save(stream);
        System.Console.WriteLine($"[SHOT] {path} ({width}x{height})");
    }

    /// <summary>冒烟自检：实例化控件与视图模板，切换主题/语言，校验视觉树。</summary>
    private int RunSmoke()
    {
        var errors = new List<string>();

        var themeService = new ThemeService();
        themeService.Initialize();
        var localization = new LocalizationService();
        localization.Initialize();

        // 自定义控件（交互控件全部自定义，逐一类验证模板可实例化）
        var controls = new FrameworkElement[]
        {
            new Controls.SOrButton(),
            new Controls.SOrTextBox(),
            new Controls.SOrComboBox(),
            new Controls.SOrDatePicker(),
            new Controls.SOrSlider(),
            new Controls.SOrCard(),
            new Controls.SOrTag(),
            new Controls.SOrProgressBar(),
            new Controls.SOrProjectListItem(),
        };

        foreach (var c in controls)
        {
            try
            {
                // 脱离视觉树的控件不会自动套用 App.Resources 的隐式样式（只会走 DefaultStyleKey），
                // 这里显式取出隐式样式赋给 Style，再 ApplyTemplate，才能真正验证控件模板可构建。
                var implicitStyle = Application.Current.TryFindResource(c.GetType()) as Style;
                if (implicitStyle is not null)
                {
                    c.Style = implicitStyle;
                }

                c.ApplyTemplate();
                c.Measure(new Size(400, 200));
                c.Arrange(new Rect(0, 0, 400, 200));
                if (VisualTreeHelper.GetChildrenCount(c) == 0)
                {
                    errors.Add($"控件 {c.GetType().Name} 未生成视觉树");
                }
            }
            catch (System.Exception ex)
            {
                errors.Add($"控件 {c.GetType().Name} 实例化失败：{ex.Message}");
            }
        }

        // 视图模型（以无副作用的冒烟桩服务构造）
        var projectService = new SmokeProjectService();
        var dialogService = new SmokeDialogService();
        var navigation = new NavigationService();
        var sampleProject = new Project { Name = "Smoke", Type = ProjectType.Programming, Status = ProjectStatus.Planning };
        var documentRepository = new SmokeDocumentRepository();

        var viewModels = new object[]
        {
            new ProjectListViewModel(projectService, localization, _ => { }, () => { }),
            new ProjectWizardViewModel(projectService, localization, () => { }),
            new ProjectDetailViewModel(
                projectService,
                localization,
                dialogService,
                documentRepository,
                new DocumentImportService(),
                new SmokeGitService(),
                new SmokeFilePickerService(),
                sampleProject,
                () => { }),
            new SettingsViewModel(themeService, localization),
            new ConfirmDialogViewModel("标题", "正文", true, localization),
            new UnsavedChangesDialogViewModel("标题", "正文", localization),
            new DocumentDialogViewModel(
                documentRepository.SeedDocument,
                documentRepository,
                new MarkdownRenderer(),
                localization),
            new GitViewModel(new SmokeGitService(), localization, dialogService, sampleProject),
            new AddRemoteDialogViewModel(new SmokeGitService(), ".", "origin", "https://example.com/repo.git", localization),
        };

        foreach (var vm in viewModels)
        {
            try
            {
                var host = new ContentControl { Content = vm };
                host.ApplyTemplate();
                var presenter = VisualTreeHelper.GetChild(host, 0) as FrameworkElement;
                presenter?.ApplyTemplate();
                host.Measure(new Size(1000, 700));
                host.Arrange(new Rect(0, 0, 1000, 700));
                host.UpdateLayout();
                if (VisualTreeHelper.GetChildrenCount(host) == 0)
                {
                    errors.Add($"视图 {vm.GetType().Name} 未生成视觉树");
                }
            }
            catch (System.Exception ex)
            {
                errors.Add($"视图 {vm.GetType().Name} 模板失败：{ex}");
            }
        }

        // 切换主题 / 语言：验证资源整本替换不缺键、不抛异常
        try
        {
            themeService.SetTheme("Dark");
            themeService.SetTheme("Light");
            localization.SetCulture("en-US");
            localization.SetCulture("zh-CN");
        }
        catch (System.Exception ex)
        {
            errors.Add($"主题/语言切换失败：{ex.Message}");
        }

        if (errors.Count == 0)
        {
            System.Console.WriteLine("[SMOKE] PASS: 所有自定义控件与视图模板实例化成功，主题/语言切换正常。");
            return 0;
        }

        System.Console.WriteLine("[SMOKE] FAIL:");
        foreach (var err in errors)
        {
            System.Console.WriteLine("  - " + err);
        }

        return 1;
    }
}

/// <summary>冒烟桩：不访问数据库的项目服务实现。</summary>
internal sealed class SmokeProjectService : IProjectService
{
    public Task<IReadOnlyList<Project>> GetAllAsync() => Task.FromResult<IReadOnlyList<Project>>(new List<Project>());
    public Task<IReadOnlyList<Project>> QueryAsync(ProjectType? type, ProjectStatus? status, string? keyword) =>
        Task.FromResult<IReadOnlyList<Project>>(new List<Project>());
    public Task<Project?> GetByIdAsync(int id) => Task.FromResult<Project?>(null);
    public Task<Project> CreateAsync(Project project) => Task.FromResult(project);
    public Task<Project> SaveAsync(Project project) => Task.FromResult(project);
    public Task DeleteAsync(int id) => Task.CompletedTask;
}

/// <summary>冒烟桩：无宿主的对话框服务实现。</summary>
internal sealed class SmokeDialogService : IDialogService
{
    public event System.Action<object?>? ActiveDialogChanged;
    public Task<bool> ConfirmAsync(string title, string message)
    {
        ActiveDialogChanged?.Invoke(null);
        return Task.FromResult(false);
    }

    public Task<UnsavedChangesChoice> ConfirmUnsavedAsync(string title, string message)
    {
        ActiveDialogChanged?.Invoke(null);
        return Task.FromResult(UnsavedChangesChoice.Cancel);
    }

    public Task InfoAsync(string title, string message) => Task.CompletedTask;

    public Task ShowAsync(IOverlayDialogViewModel viewModel)
    {
        ActiveDialogChanged?.Invoke(viewModel);
        viewModel.RequestClose();
        ActiveDialogChanged?.Invoke(null);
        return Task.CompletedTask;
    }
}

/// <summary>冒烟桩：内存文档仓储（不访问数据库），为冒烟与截图提供可渲染的示例文档。</summary>
internal sealed class SmokeDocumentRepository : IProjectDocumentRepository
{
    private readonly List<ProjectDocument> _documents = new();

    public SmokeDocumentRepository()
    {
        var now = DateTime.Now;
        _documents.Add(new ProjectDocument
        {
            Id = 1,
            ProjectId = 1,
            Category = DocumentCategory.Prd,
            Title = "SOrtus 产品需求文档",
            SourceFileName = "SOrtus_产品需求文档.md",
            Format = "md",
            SortOrder = 0,
            CreatedAt = now.AddDays(-12),
            UpdatedAt = now.AddDays(-1),
            Content = SamplePrd,
        });

        _documents.Add(new ProjectDocument
        {
            Id = 2,
            ProjectId = 1,
            Category = DocumentCategory.IterationPlan,
            Title = "SOrtus 版本迭代规划",
            SourceFileName = "SOrtus_版本迭代规划.md",
            Format = "md",
            SortOrder = 0,
            CreatedAt = now.AddDays(-11),
            UpdatedAt = now.AddDays(-2),
            Content = "# 版本迭代规划\n\n## 0.1.0 MVP\n\n- [x] 工程骨架与分层\n- [x] 编程立项闭环\n\n## 0.2.0 详情页 V2\n\n- [ ] 三分版块\n- [ ] 文档导入与渲染\n- [ ] Git 时间线与推送\n",
        });

        _documents.Add(new ProjectDocument
        {
            Id = 3,
            ProjectId = 1,
            Category = DocumentCategory.Other,
            Title = "MVP 验收记录",
            SourceFileName = "MVP验收记录.md",
            Format = "md",
            SortOrder = 1,
            CreatedAt = now.AddDays(-3),
            UpdatedAt = now.AddDays(-3),
            Content = "# 验收结论\n\n构建零错误，数据重启不丢失。\n\n- [x] A4 数据库初始化\n- [x] A12 未保存拦截\n",
        });
    }

    public ProjectDocument SeedDocument => _documents[0];

    public Task<IReadOnlyList<ProjectDocument>> GetByProjectAsync(int projectId) =>
        Task.FromResult<IReadOnlyList<ProjectDocument>>(
            _documents.Where(d => d.ProjectId == projectId).ToList());

    public Task<ProjectDocument?> GetSlotAsync(int projectId, DocumentCategory category) =>
        Task.FromResult(_documents.FirstOrDefault(d =>
            d.ProjectId == projectId && d.Category == category && category != DocumentCategory.Other));

    public Task<int> UpsertAsync(ProjectDocument document)
    {
        if (document.Id == 0)
        {
            document.Id = _documents.Count == 0 ? 1 : _documents.Max(d => d.Id) + 1;
            _documents.Add(document);
        }

        return Task.FromResult(document.Id);
    }

    public Task<bool> UpdateContentAsync(int documentId, string content)
    {
        var target = _documents.FirstOrDefault(d => d.Id == documentId);
        if (target is null)
        {
            return Task.FromResult(false);
        }

        target.Content = content;
        target.UpdatedAt = DateTime.UtcNow;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(int documentId) =>
        Task.FromResult(_documents.RemoveAll(d => d.Id == documentId) > 0);

    public Task<int> GetNextSortOrderAsync(int projectId) =>
        Task.FromResult(_documents.Where(d => d.ProjectId == projectId).Select(d => d.SortOrder).DefaultIfEmpty(0).Max() + 1);

    private const string SamplePrd = """
        # SOrtus 产品需求文档

        > 本地优先的立项管理工具，打通编程立项全流程。

        ## 目标

        用一个小工具管理您的立项，做到**立项即沉淀**。

        ## 需求池

        | 编号 | 需求 | 优先级 |
        | --- | --- | --- |
        | F0-03 | 详情页字段编辑 | P0 |
        | F0-07 | 开发进度管理 | P1 |

        ## 待办

        - [x] 立项向导
        - [x] 详情页基础字段
        - [ ] 文档导入与渲染
        - [ ] Git 时间线

        ## 技术约定

        ````csharp
        var project = new Project { Name = "SOrtus" };
        ````

        ---

        详见开发文档。
        """;
}

/// <summary>冒烟桩：返回固定快照的 Git 服务（不调用命令行）。</summary>
internal sealed class SmokeGitService : IGitService
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Now;

    public Task<GitRepositoryInfo> InspectAsync(string folderPath, int commitCount = 30)
    {
        var branches = new List<GitBranchInfo>
        {
            new("master", true, false, "32fe807", "build: 版本号提升至 0.1.1", Now.AddHours(-3), 2, 0),
            new("feature/detail-v2", false, false, "a1b2c3d", "feat: 详情页三分版块", Now.AddHours(-1), 0, 0),
            new("origin/master", false, true, "32fe807", "build: 版本号提升至 0.1.1", Now.AddHours(-4), 0, 0),
        };

        var commits = new List<GitCommitInfo>
        {
            new("a1b2c3d4e5f6", "a1b2c3d", "Song-1118", Now.AddHours(-1), "feat: 详情页三分版块与文档导入", ["feature/detail-v2"]),
            new("32fe807a1b2c", "32fe807", "Song-1118", Now.AddHours(-3), "build: 版本号提升至 0.1.1", ["master", "origin/master"]),
            new("deaa6539c8d7", "deaa653", "Song-1118", Now.AddHours(-5), "fix: 处置阶段验收遗留问题", []),
            new("df5fef5b6a41", "df5fef5", "Song-1118", Now.AddHours(-8), "feat(ui): 交互与视觉第二轮修复", []),
        };

        return Task.FromResult(new GitRepositoryInfo(
            true,
            folderPath,
            "master",
            true,
            true,
            2,
            0,
            branches,
            commits,
            [new GitRemoteInfo("origin", "https://example.com/sortus.git")]));
    }

    public Task<GitOperationResult> AddOrSetRemoteAsync(string folderPath, string name, string url) =>
        Task.FromResult(GitOperationResult.Ok());

    public Task<GitOperationResult> PushAsync(string folderPath, string branch, string remote, bool setUpstream) =>
        Task.FromResult(GitOperationResult.Ok());
}

/// <summary>冒烟桩：不弹出文件选择框（返回取消）。</summary>
internal sealed class SmokeFilePickerService : IFilePickerService
{
    public string? PickDocument() => null;
}


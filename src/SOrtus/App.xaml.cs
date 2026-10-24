using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SOrtus.Core.Models;
using SOrtus.Core.Repositories;
using SOrtus.Core.Services;
using SOrtus.Navigation;
using SOrtus.Repositories;
using SOrtus.Services;
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
        IThemeService themeService = new ThemeService();
        themeService.Initialize();
        ILocalizationService localizationService = new LocalizationService();
        localizationService.Initialize();
        INavigationService navigation = new NavigationService();
        IDialogService dialogService = new DialogService(localizationService);

        var mainViewModel = new MainViewModel(projectService, themeService, localizationService, navigation, dialogService);
        mainViewModel.Initialize();

        var window = new MainWindow { DataContext = mainViewModel };
        window.Show();
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
        var localization = new LocalizationService();
        localization.Initialize();

        var projectService = new SmokeProjectService();
        var navigation = new NavigationService();
        var dialogService = new SmokeDialogService();
        var mainViewModel = new MainViewModel(projectService, themeService, localization, navigation, dialogService);
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
        SaveWindow(window, System.IO.Path.Combine(targetDir, "04-detail.png"));

        await mainViewModel.ShowSettingsAsync();
        await SettleAsync(window);
        SaveWindow(window, System.IO.Path.Combine(targetDir, "05-settings.png"));

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
        await Task.Delay(200);
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

        var viewModels = new object[]
        {
            new ProjectListViewModel(projectService, localization, _ => { }, () => { }),
            new ProjectWizardViewModel(projectService, localization, () => { }),
            new ProjectDetailViewModel(projectService, localization, dialogService, sampleProject, () => { }),
            new SettingsViewModel(themeService, localization),
            new ConfirmDialogViewModel("标题", "正文", true, localization),
            new UnsavedChangesDialogViewModel("标题", "正文", localization),
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
}

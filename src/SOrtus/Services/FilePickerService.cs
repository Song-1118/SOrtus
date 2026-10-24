using SOrtus.Core.Services;

namespace SOrtus.Services;

/// <summary>
/// 文件选择服务（把 <c>OpenFileDialog</c> 从视图模型里隔离出来，便于测试替换）。
/// </summary>
public interface IFilePickerService
{
    /// <summary>选择要导入的项目文档；用户取消返回 null。</summary>
    string? PickDocument();
}

/// <summary>
/// 基于 <see cref="Microsoft.Win32.OpenFileDialog"/> 的文件选择实现（仅表现层使用）。
/// </summary>
internal sealed class FilePickerService : IFilePickerService
{
    private readonly ILocalizationService _localization;

    public FilePickerService(ILocalizationService localization)
    {
        _localization = localization;
    }

    public string? PickDocument()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = _localization.GetString("Doc_PickTitle"),
            Filter = _localization.GetString("Doc_PickFilter"),
            CheckFileExists = true,
            Multiselect = false,
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}

using System.Windows.Documents;
using System.Windows.Input;
using SOrtus.Common;
using SOrtus.Core.Markdown;
using SOrtus.Core.Models;
using SOrtus.Core.Repositories;
using SOrtus.Core.Services;
using SOrtus.Services;

namespace SOrtus.ViewModels;

/// <summary>
/// 文档弹窗视图模型（规范：详情页 V2「点击文档卡片 → 覆盖层渲染」）。
/// <para>
/// 渲染由 <see cref="IMarkdownRenderer"/> 完成；Markdown 待办勾选后**立即回写**数据库：
/// 用 <see cref="MarkdownParser.ToggleTask"/> 精确替换原文对应行，保留缩进与其他内容。
/// 写入用信号量串行化，避免连续勾选造成竞态；失败时回滚勾选态并显示内联错误。
/// </para>
/// </summary>
internal sealed class DocumentDialogViewModel : ViewModelBase, IClosableOverlayDialogViewModel
{
    private readonly IProjectDocumentRepository _repository;
    private readonly IMarkdownRenderer _renderer;
    private readonly ILocalizationService _localization;
    private readonly ProjectDocument _document;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly RelayCommand _closeCommand;

    private string _content;
    private string _saveStateText;
    private string? _errorMessage;

    public DocumentDialogViewModel(
        ProjectDocument document,
        IProjectDocumentRepository repository,
        IMarkdownRenderer renderer,
        ILocalizationService localization)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));

        _content = document.Content ?? string.Empty;
        _saveStateText = localization.GetString("Doc_AutoSaveHint");
        _closeCommand = new RelayCommand(_ => RequestClose());

        var markdown = MarkdownParser.Parse(_content);
        Document = _renderer.Render(markdown, OnTodoToggled);
    }

    public event Action? Closed;

    /// <summary>渲染后的文档（绑定到 FlowDocumentScrollViewer）。</summary>
    public FlowDocument Document { get; }

    public string Title => _document.Title;

    /// <summary>格式徽标（MD / TXT / DOCX / DOC）。</summary>
    public string FormatLabel => _document.Format.ToUpperInvariant();

    public string SourceFileText => _document.SourceFileName;

    /// <summary>保存状态提示（自动保存 / 保存中 / 已保存 / 保存失败）。</summary>
    public string SaveStateText
    {
        get => _saveStateText;
        private set => SetProperty(ref _saveStateText, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrEmpty(_errorMessage);

    public ICommand CloseCommand => _closeCommand;

    public void RequestClose() => Closed?.Invoke();

    private void OnTodoToggled(int sourceLine, bool isChecked) => _ = ToggleAsync(sourceLine, isChecked);

    private async Task ToggleAsync(int sourceLine, bool isChecked)
    {
        var updated = MarkdownParser.ToggleTask(_content, sourceLine, isChecked);
        if (string.Equals(updated, _content, StringComparison.Ordinal))
        {
            return;
        }

        await _writeLock.WaitAsync();
        try
        {
            ErrorMessage = null;
            SaveStateText = _localization.GetString("Doc_Saving");

            var saved = await _repository.UpdateContentAsync(_document.Id, updated);
            if (saved)
            {
                _content = updated;
                _document.Content = updated;
                SaveStateText = _localization.GetString("Doc_Saved");
            }
            else
            {
                Revert(sourceLine, isChecked);
                ErrorMessage = _localization.GetString("Doc_SaveFailed");
                SaveStateText = _localization.GetString("Doc_SaveFailed");
            }
        }
        catch (Exception ex)
        {
            Revert(sourceLine, isChecked);
            ErrorMessage = ex.Message;
            SaveStateText = _localization.GetString("Doc_SaveFailed");
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private void Revert(int sourceLine, bool isChecked) =>
        _renderer.TrySetTaskState(Document, sourceLine, !isChecked);
}

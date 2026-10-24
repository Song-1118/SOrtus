using SOrtus.Core.Models;

namespace SOrtus.Services;

/// <summary>
/// 提醒调度服务（规范：待办类型到期提醒）。
/// 范围属于 1.2（0.3.0）版本，本 MVP 仅预留模块，实现见后续版本。
/// 轻量实现：基于应用内定时器，触发本地通知，不要求系统级后台常驻。
/// </summary>
internal sealed class ReminderService
{
    // TODO(1.2): 启动时检查到期 + 运行中定时轮询，触发本地通知。
    public void Start() { }

    public void Stop() { }

    public void Register(Project project) { }
}

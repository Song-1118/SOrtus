# SOrtus

> 用一个小工具管理您的立项。

[English](README.md) · **简体中文**

SOrtus 是一款**本地优先**的立项管理桌面工具。它把三类工作 —— **编程开发**、**剪辑制作**、**待办** ——
收进同一个轻量 Windows 应用，让想法出现的那一刻就被记下来，而不是散落在一堆便签里。

所有数据存放在本机一个 SQLite 文件里：无账号、无后端、无遥测。

## 特性

- **一个工具管三类项目**：编程、剪辑、待办共用同一套立项流程。
- **立项时即采集**：项目创建时就把关键信息记下，后续始终集中在一处。
- **本地优先**：数据库就是你机器上的一个文件，直接复制即可备份。
- **现代质感**：高斯模糊面板、圆角、浅色/深色主题，模糊强度可自行调节。
- **双语界面**：简体中文与 English。

## 技术栈

| 层级 | 选型 |
| --- | --- |
| 运行时 | .NET 10 |
| 语言 | C# 14 |
| 表现层 | WPF（`net10.0-windows`） |
| 存储 | SQLite（单文件、本地） |
| 架构 | 分层架构 + MVVM |

## 安装

从 [Releases](https://github.com/Song-1118/SOrtus/releases) 页面下载 `SOrtus-Setup-<版本>.exe` 并运行。

- 按当前用户安装到 `%LocalAppData%\Programs\SOrtus`，**不需要管理员权限**。
- 安装包自带 .NET 10 运行时，无需另行安装运行环境。
- 系统要求：Windows 10 / 11 x64。

你的数据位于 `%LocalAppData%\SOrtus\`（`sortus.db` 与 `settings.json`），安装、升级、卸载都不会改动它。

## 自动更新

SOrtus 支持自动更新，且未经你确认不会发生任何事：

1. 启动 3 秒后向 GitHub 查询最新版本信息，每天最多一次。
2. 若存在更高版本，主窗口顶部出现一条不打断操作的提示条；此时**尚未开始下载**。
3. 你确认后，程序下载安装包，用发布说明里公布的 SHA256 校验完整性，并在替换任何文件之前，
   把数据库与设置备份到 `%LocalAppData%\SOrtus\backups\`。
4. 程序退出，安装器静默完成安装，随后新版自动启动。

若中途任何一步失败，更新会安静地放弃，原因写入 `%LocalAppData%\SOrtus\logs\update.log`。
**设置 → 软件更新**里随时可以手动检查。

## 隐私与联网说明

你的项目数据留在本机，SOrtus 不会对外发布任何项目内容。

SOrtus 自身唯一的出网请求，是检查更新时对
`GET https://api.github.com/repos/Song-1118/SOrtus/releases/latest` 的访问。与任何 HTTP 请求一样，
这会把你的 IP 地址暴露给 GitHub，并携带 User-Agent `SOrtus-Updater/1.0`；它不携带任何项目内容、
不含账号身份、不含遥测。

在**设置 → 软件更新**中关闭「自动检查更新」后，程序不再发起任何后台请求，只有你手动点击检查时才会联网。

另外，Git 面板会调用本机的 `git` 命令，对你**自己配置**的远端操作。推送由你发起、去向由你指定，
SOrtus 不会自行选择任何目标地址。

## 从源码构建

```bash
dotnet restore SOrtus.slnx
dotnet build SOrtus.slnx -c Release
dotnet run --project src/SOrtus/SOrtus.csproj
```

需要 .NET 10 SDK。

### 自检与截图

```bash
dotnet test                                                # 单元 / 集成测试
dotnet run --project src/SOrtus/SOrtus.csproj -- --smoke   # 实例化全部自定义控件与视图模板
SOrtus.exe --shot resources\screenshots\latest 1180 760     # 离屏渲染核心界面（不触碰真实数据库）
```

## 目录结构

```
SOrtus/
├── src/SOrtus.Core/   # 领域模型、仓储/服务契约、更新基础逻辑
├── src/SOrtus/        # WPF 主程序（视图模型、服务、仓储、控件、资源）
├── tests/             # 单元测试与集成测试
├── build/             # 构建脚本与版本号单一来源
├── docs/              # PRD、架构、规范、数据库设计、版本规划
├── resources/         # 图标、图片、主题视觉资源
├── CHANGELOG.md
├── LICENSE
└── README.md
```

## 发版（维护者）

打包与发版脚本位于工作区的 `EXE/` 目录（本仓库外一层）：

- `SOrtus.iss` —— Inno Setup 6 脚本；按用户安装，为静默升级而配置。
- `Publish-Release.ps1` —— 一条命令完成：发布 → 编译安装包 → 计算 SHA256 → 生成发布说明 → `gh release create`。

自动更新客户端依赖两条不能漂移的约定：

- Release 资产名必须是 `SOrtus-Setup-<版本>.exe`；
- Release 说明中必须包含隐藏标记 `<!-- sortus-sha256: <哈希> -->`。

任一条件缺失或出错，所有客户端都会静默地认为「已是最新版本」，且不会报任何错误。
发版脚本因此从 `build/version.props` 推导这两个值，不接受手工输入。

## 文档

- [`docs/README.md`](docs/README.md) —— 文档索引
- `docs/开发文档.md` —— 架构与技术决策
- `docs/产品需求文档.md` —— PRD
- `docs/项目开发规范.md` —— 代码与项目规范
- `docs/版本迭代规划.md` —— 版本路线与验收
- `docs/数据库设计.md` —— 表结构与迁移
- `CHANGELOG.md` —— 版本变更记录（Keep a Changelog）

## 许可证

MIT，详见 [LICENSE](LICENSE)。

# SOrtus（中文说明）

> 用一个小工具管理您的立项。

SOrtus 是一款**本地优先**的立项管理桌面工具，面向个人与小型团队，用统一入口管理三类立项：
**编程开发**、**剪辑**、**待办**。纯本地运行、无后端、无账号、无数据上传，免费开源（MIT）。

## 特性

- **一个工具管三类项目**：编程、剪辑、待办共用统一立项体验。
- **立项即沉淀**：立项时采集关键信息，全程信息集中留存、可回溯。
- **本地优先、隐私无忧**：纯本地 SQLite 存储，无网络请求、无遥测。
- **现代质感**：高斯模糊、圆角、平滑动画、浅色/深色主题。

## 技术栈

| 层级   | 选型                     |
|--------|--------------------------|
| 运行时 | .NET 10 LTS              |
| 语言   | C# 14                    |
| 表现层 | WPF（`net10.0-windows`） |
| 存储   | SQLite（单文件、本地）   |
| 架构   | 分层架构 + MVVM          |

## 构建运行

```bash
dotnet restore
dotnet build -c Release
dotnet run --project src/SOrtus/SOrtus.csproj
```

要求：Windows 10 / 11 x64，已安装 .NET 10 桌面运行时（或使用 `build/package.ps1` 产出的自包含发布包）。

## 自检与截图

```bash
dotnet test                                               # 单元 / 集成测试
dotnet run --project src/SOrtus/SOrtus.csproj -- --smoke   # 控件与视图模板冒烟自检
SOrtus.exe --shot resources\screenshots\latest             # 离屏渲染核心界面截图（不碰真实数据库）
```

## 文档

- `docs/MVP验收记录.md`（0.1.0 验收结论与缺陷修复记录）
- `docs/开发文档.md`、`docs/产品需求文档.md`、`docs/项目开发规范.md`、`docs/版本迭代规划.md`、`docs/数据库设计.md`

## 许可证

MIT，详见 [LICENSE](LICENSE)。

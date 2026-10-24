# SOrtus

> 用一个小工具管理您的立项。

SOrtus is a **local-first project-initiation management desktop tool** for individuals and small teams.
It unifies three kinds of initiatives — **Programming**, **Clip/Video**, and **Todo** — under one
lightweight, offline, open-source (MIT) entry point.

SOrtus 是一款面向个人与小型团队的**本地优先立项管理桌面工具**，用统一入口管理三类立项
（编程开发 / 剪辑 / 待办），纯本地运行、无后端、无账号、无数据上传。

## Highlights / 特性

- **One tool, three project types** — Programming, Clip, Todo share one initiation flow.
- **Local first** — all data lives in a local SQLite file; nothing leaves your machine.
- **Pure & free** — MIT licensed, no accounts, no ads, no telemetry.
- **Modern look** — Gaussian blur panels, rounded corners, smooth animations, Light/Dark themes.

## Technology / 技术栈

| Layer | Choice |
| --- | --- |
| Runtime | .NET 10 LTS |
| Language | C# 14 |
| UI | WPF (`net10.0-windows`) |
| Storage | SQLite (single file, local) |
| Pattern | Layered architecture + MVVM |

## Build & Run / 构建运行

```bash
dotnet restore
dotnet build -c Release
dotnet run --project src/SOrtus/SOrtus.csproj
```

Requirements: Windows 10 / 11 x64 with the .NET 10 desktop runtime (or use the self-contained
publish produced by `build/package.ps1`).

### Self-check / 自检与截图

```bash
dotnet test                                              # 单元 / 集成测试
dotnet run --project src/SOrtus/SOrtus.csproj -- --smoke  # 控件与视图模板冒烟自检
SOrtus.exe --shot resources\screenshots\latest            # 离屏渲染核心界面截图（不碰真实数据库）
```

## Project Layout / 目录结构

```
SOrtus/
├── src/SOrtus.Core/   # 可复用核心库：领域模型、仓储/服务契约
├── src/SOrtus/        # WPF 主程序（View/ViewModel/Service/Repository/Resources/Controls）
├── tests/             # 单元测试 / 集成测试
├── build/             # 构建与打包脚本、版本号集中管理
├── docs/              # 开发文档、PRD、规范、版本规划、数据库设计
├── resources/         # 图标、图片、主题视觉资源
├── LICENSE            # MIT
└── README.md          # 本文件
```

## Documentation / 文档

- `docs/MVP验收记录.md` — MVP（0.1.0）A1–A16 验收结论与缺陷修复记录
- `docs/开发文档.md` — architecture & technical decisions
- `docs/产品需求文档.md` — PRD
- `docs/项目开发规范.md` — coding & project conventions
- `docs/版本迭代规划.md` — version plan & acceptance
- `docs/数据库设计.md` — schema & migrations

## License

MIT — see [LICENSE](LICENSE).

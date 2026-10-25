# SOrtus

> Manage your project initiations with one small tool.

**English** · [简体中文](README.zh-CN.md)

SOrtus is a local-first desktop tool for capturing and tracking project initiations. It puts three
kinds of work — **programming**, **clip/video production**, and **todo** — behind one lightweight
Windows app, so a new idea is recorded the moment it appears instead of scattering across notes.

Everything is stored in a single local SQLite file. No account, no backend, no telemetry.

## Highlights

- **One tool, three project types** — programming, clip and todo share a single initiation flow.
- **Capture at the start** — key facts are collected when a project is created and stay in one place.
- **Local first** — the database is one file on your machine and can be copied as a backup.
- **Modern surface** — Gaussian-blur panels, rounded corners, light/dark themes, adjustable blur strength.
- **Bilingual UI** — 简体中文 and English.

## Tech stack

| Layer | Choice |
| --- | --- |
| Runtime | .NET 10 |
| Language | C# 14 |
| UI | WPF (`net10.0-windows`) |
| Storage | SQLite (single file, local) |
| Architecture | Layered + MVVM |

## Install

Download `SOrtus-Setup-<version>.exe` from the [Releases](https://github.com/Song-1118/SOrtus/releases) page and run it.

- Installs per user to `%LocalAppData%\Programs\SOrtus`; **no administrator rights required**.
- Ships a self-contained .NET 10 runtime — there is no separate runtime to install.
- Requires Windows 10 or 11, x64.

Your data lives in `%LocalAppData%\SOrtus\` (`sortus.db` and `settings.json`) and is untouched by
installs, upgrades and uninstalls.

## Updates

SOrtus updates itself, and nothing happens without your confirmation:

1. Three seconds after startup it asks GitHub for the latest release metadata — at most once every 24 hours.
2. If a newer version exists, a non-blocking banner appears in the main window. Nothing is downloaded yet.
3. On confirm, the installer is downloaded, its SHA256 is verified against the value published in the
   release notes, and your database and settings are copied to `%LocalAppData%\SOrtus\backups\`
   before any file is replaced.
4. The app exits, the installer runs silently, and the new version starts automatically.

If anything fails, the update is abandoned quietly and the reason is written to
`%LocalAppData%\SOrtus\logs\update.log`. A manual check is always available in
**Settings → Software update**.

## Privacy and network access

Your project data stays on your machine, and SOrtus publishes nothing about it.

The only request SOrtus makes on its own is `GET https://api.github.com/repos/Song-1118/SOrtus/releases/latest`
when checking for updates. Like any HTTP request it exposes your IP address to GitHub, and it sends the
user agent `SOrtus-Updater/1.0`. It carries no project content, no account identity, and no telemetry.

Turn the check off in **Settings → Software update**. With it off, the app makes no background requests
at all; a manual check is the only way it will contact the network.

Separately, the Git panel runs the local `git` executable against a remote **you** configure. Pushing is
initiated by you and goes exactly where you pointed it — SOrtus itself never picks a destination.

## Build from source

```bash
dotnet restore SOrtus.slnx
dotnet build SOrtus.slnx -c Release
dotnet run --project src/SOrtus/SOrtus.csproj
```

Requires the .NET 10 SDK.

### Self-check

```bash
dotnet test                                                # unit and integration tests
dotnet run --project src/SOrtus/SOrtus.csproj -- --smoke   # instantiates every control and view template
SOrtus.exe --shot resources\screenshots\latest 1180 760     # off-screen render of the core screens
```

## Project layout

```
SOrtus/
├── src/SOrtus.Core/   # domain models, repository and service contracts, update primitives
├── src/SOrtus/        # WPF app (view models, services, repositories, controls, resources)
├── tests/             # unit and integration tests
├── build/             # build scripts and the single source of the version number
├── docs/              # PRD, architecture, conventions, database design, release plan
├── resources/         # icons, images and theme assets
├── CHANGELOG.md
├── LICENSE
└── README.md
```

## Releasing (maintainers)

Packaging and release scripts live in the workspace's `EXE/` folder, one level above this repository:

- `SOrtus.iss` — Inno Setup 6 script; per-user install, built for silent upgrades.
- `Publish-Release.ps1` — one command: publish → compile installer → SHA256 → release notes → `gh release create`.

The update client depends on two conventions that must not drift:

- the release asset is named `SOrtus-Setup-<version>.exe`, and
- the release notes contain a hidden `<!-- sortus-sha256: <hash> -->` marker.

If either is missing or wrong, every client silently concludes it is already up to date, and nothing
reports an error. The release script derives both values from `build/version.props` for this reason.

## Documentation

- [`docs/README.md`](docs/README.md) — documentation index
- `docs/开发文档.md` — architecture and technical decisions
- `docs/产品需求文档.md` — product requirements
- `docs/项目开发规范.md` — coding and project conventions
- `docs/版本迭代规划.md` — version plan and acceptance criteria
- `docs/数据库设计.md` — schema and migrations
- `CHANGELOG.md` — release history (Keep a Changelog)

## License

MIT — see [LICENSE](LICENSE).

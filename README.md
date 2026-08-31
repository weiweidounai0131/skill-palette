# Skill Palette

[English](#english) · [简体中文](#简体中文)

![Skill Palette app icon](Resources/SkillPaletteIcon.png)

## 简体中文

**Skill Palette** 是一个完全本地运行的 Codex Skill 快速调用器，支持 **macOS 与 Windows**。在 Codex 输入默认触发字符 `#`（或在设置中改为其他单个字符）即可打开本机 Skill 搜索，按名称、描述、标签或目录筛选，并将 `@skill-name` 写回原输入框。

它只读取本机的 Skill 元数据，不上传 Skill 内容、搜索词或键盘输入。

### 功能

- 在 Codex 或 ChatGPT 输入默认 `#` 或自定义触发字符，立即打开本地搜索。
- 按名称、描述、中文标签进行模糊搜索；支持目录分类、收藏与最近使用。
- 自动扫描 `~/.codex/skills` / `~/.agents/skills`（Windows 对应 `%USERPROFILE%\\.codex\\skills` / `%USERPROFILE%\\.agents\\skills`）。
- 点击重新扫描后显示本次新增 Skill 数量和当前索引总数。
- 选中后写回 `@skill-name`；按 `Esc` 取消时保留原触发字符。
- 默认只在目标应用名称或标识包含 `codex` / `chatgpt` 时启用，可自行修改规则。
- 本地隐私设计：不上传 Skills、搜索词或按键内容。
- macOS 提供权限引导、菜单栏常驻、Dock 显示控制、诊断页和登录时启动。
- Windows 提供系统托盘、全局监听、目标进程规则、诊断页和当前用户登录时启动。

### 下载与安装

请优先在 [GitCode Releases](https://gitcode.com/gcw_mHRylKw0/skill-palette/releases) 下载最新版；如果 GitCode 暂时不可用，也可以访问 [GitHub Releases](https://github.com/weiweidounai0131/skill-palette/releases/latest)。

| 平台 | 文件 | 要求 | 安装方式 |
| --- | --- | --- | --- |
| macOS | `Skill-Palette-1.2.0-macos.zip` | macOS 13 或更高版本 | 解压后将 `Skill Palette.app` 移到“应用程序”或任意本地目录。首次启动时按引导开启“辅助功能”和“输入监控”。 |
| Windows | `SkillPalette-1.2.0-windows-x64-portable.exe` | Windows 10 版本 2004（19041）或更高版本，或 Windows 11 x64 | 下载后放到固定位置直接运行。该文件为免安装便携版；可在设置中选择登录时启动。 |

> Windows 便携版会使用全局键盘监听和剪贴板，将选中的 Skill 插入原来的 Codex 输入框。若安全软件提示，请根据自己的安全策略确认是否允许。

### 使用

1. 在 Codex 输入框中输入 `#`。
2. 搜索 Skill 名称、描述、标签或目录，例如 `PPT`、`飞书`、`图片`、`Excel`。
3. 鼠标点击或按 `Enter` 选择结果，调用文本会回到原输入框。
4. 按 `Esc` 关闭浮窗并保留 `#`，便于正常输入该符号。

### macOS 截图

| 权限引导 | 通用设置 |
| --- | --- |
| ![Skill Palette 权限引导](docs/screenshots/permission-guide.png) | ![Skill Palette 通用设置](docs/screenshots/settings-general.png) |

| Skill 管理 | 在 Codex 中选择 Skill |
| --- | --- |
| ![Skill Palette Skill 管理](docs/screenshots/settings-skills.png) | ![Skill Palette 在 Codex 中的选择浮窗](docs/screenshots/codex-picker.png) |

### 从源码构建

#### macOS

要求：macOS 13 或更高版本，以及 Xcode Command Line Tools 或 Xcode。

```bash
./build.sh
open "build.noindex/Skill Palette.app"
```

#### Windows

要求：Windows 10 版本 2004 或更高版本、.NET 10 SDK，以及 Visual Studio 2022（安装 Windows App SDK/WinUI 工作负载）或等效构建环境。

```powershell
dotnet restore windows/SkillPalette.Windows.slnx
dotnet publish windows/SkillPalette.Windows/SkillPalette.Windows.csproj -c Release -p:Platform=x64 -p:RuntimeIdentifier=win-x64
```

发布配置位于 `windows/SkillPalette.Windows/Properties/PublishProfiles/`，同时提供 x64、x86 与 ARM64 配置。

### 隐私

Skill Palette 仅在本机扫描 `SKILL.md` 的必要元数据以建立搜索索引。它不发送你的 Skills、搜索词或键盘输入到服务器。

## English

**Skill Palette** is a fully local launcher for Codex Skills on **macOS and Windows**. Type the default `#` trigger in Codex, or configure another single trigger character, to search local Skills by name, description, tag, or folder and insert `@skill-name` back into the original composer.

Skill content, search terms, and keystrokes stay on the device.

### Highlights

- Open local Skill search from Codex or ChatGPT with `#` or a custom trigger character.
- Fuzzy search by name, description, Chinese tags, and folder; browse categories, favorites, and recents.
- Scans `~/.codex/skills` and `~/.agents/skills` on macOS, or `%USERPROFILE%\\.codex\\skills` and `%USERPROFILE%\\.agents\\skills` on Windows.
- After a rescan, shows how many Skills were added and the current total index count.
- Insert `@skill-name` into the original prompt. Press `Esc` to close the palette and keep the trigger character.
- Restricts triggering to processes matching `codex` or `chatgpt` by default; target rules are configurable.
- Local-first: no Skill content, search terms, or keystrokes are uploaded.
- macOS includes permission guidance, diagnostics, login launch, menu-bar operation, and optional Dock visibility.
- Windows includes a system tray, global listener, target-process rules, diagnostics, and current-user startup.

### Download and install

Download the latest release from [GitCode Releases](https://gitcode.com/gcw_mHRylKw0/skill-palette/releases); if GitCode is temporarily unavailable, use [GitHub Releases](https://github.com/weiweidounai0131/skill-palette/releases/latest).

| Platform | Asset | Requirement | Installation |
| --- | --- | --- | --- |
| macOS | `Skill-Palette-1.2.0-macos.zip` | macOS 13+ | Unzip and move `Skill Palette.app` to Applications or another local folder. On first launch, grant Accessibility and Input Monitoring. |
| Windows | `SkillPalette-1.2.0-windows-x64-portable.exe` | Windows 10 version 2004 (19041)+ or Windows 11 x64 | Place the portable EXE in a stable folder and run it. Enable launch at sign-in from Settings if wanted. |

> The Windows portable app uses a global keyboard listener and the clipboard to return the selected Skill to Codex. Review any security-software prompt according to your own security policy.

### Usage

1. Focus the Codex composer and type `#`.
2. Search for a Skill by name, description, tag, or folder, such as `PPT`, `Feishu`, `image`, or `Excel`.
3. Click a result or press `Enter` to write the invocation back into Codex.
4. Press `Esc` to dismiss the palette and retain `#` in the composer.

### macOS screenshots

| Permission guidance | General settings |
| --- | --- |
| ![Skill Palette permission guidance](docs/screenshots/permission-guide.png) | ![Skill Palette general settings](docs/screenshots/settings-general.png) |

| Skill management | Choosing a Skill in Codex |
| --- | --- |
| ![Skill Palette Skill management](docs/screenshots/settings-skills.png) | ![Skill Palette picker in Codex](docs/screenshots/codex-picker.png) |

### Build from source

#### macOS

Requires macOS 13+ and Xcode Command Line Tools or Xcode.

```bash
./build.sh
open "build.noindex/Skill Palette.app"
```

#### Windows

Requires Windows 10 version 2004+ or Windows 11, the .NET 10 SDK, and Visual Studio 2022 with the Windows App SDK/WinUI workload, or an equivalent build environment.

```powershell
dotnet restore windows/SkillPalette.Windows.slnx
dotnet publish windows/SkillPalette.Windows/SkillPalette.Windows.csproj -c Release -p:Platform=x64 -p:RuntimeIdentifier=win-x64
```

Publishing profiles for x64, x86, and ARM64 are in `windows/SkillPalette.Windows/Properties/PublishProfiles/`.

### Privacy

Skill Palette scans only the local `SKILL.md` metadata needed to build its search index. It does not send Skills, search terms, or keystrokes to a server.

## License

Copyright © 2026 Jiang Jiawei. All rights reserved.

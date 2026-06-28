# mdviewx

A fast, offline, feature-rich **Markdown viewer** built with the [Uno Platform](https://platform.uno/) (C# Markup + MVUX). Point it at a `.md` file and it renders a clean, navigable document — with syntax highlighting, Mermaid diagrams, admonitions, a document outline, and history — entirely offline.

> 📖 **Try it out:** the [`docs/`](docs/index.md) folder is a self-contained sample wiki that doubles as a feature tour. It's the app's default startup document — open [`docs/index.md`](docs/index.md).

---

## Features

### Rendering
- **CommonMark + extensions** via [Markdig](https://github.com/xoofx/markdig) (`UseAdvancedExtensions`): tables, task lists, auto-links, footnotes, auto-identifiers, and more.
- **Syntax highlighting** with a bundled [highlight.js](https://highlightjs.org/) (all ~190 languages **plus** a Razor/cshtml grammar). Light/dark themes follow the OS.
- **Mermaid diagrams** — ` ```mermaid ` code blocks render as live diagrams (flowcharts, sequence, timeline, …) via a bundled, all-in-one Mermaid build.
- **Admonitions / callouts** in **both** Docusaurus and MkDocs syntaxes:
  - Docusaurus: `:::tip`, `:::warning[Custom title]`, … `:::`
  - MkDocs: `!!! note "Title"` and collapsible `??? note` / `???+ note`
- **YAML front matter** is parsed and hidden (not rendered as content).
- Everything is **inlined/sidecar-bundled** — no network access required to render.

### Links & navigation
- **Smart link rewriting** relative to the opened file's folder:
  - extension-less wiki links get `.md` appended (e.g. `[Houses](houses)` → `houses.md`)
  - links to a folder resolve to its `index.md` (e.g. `[Dune](dune/)` → `dune/index.md`)
- Clicking a local Markdown link **opens it in-app** as the active document.
- **Back / Forward** session history, plus a persisted **global history** of everything you've opened.
- **Auto-refresh**: edit the file in your editor and the view reloads automatically (no prompt).

### UI
- **Sidebar** with three tabs — **Headers** (document outline), **Session** history, **Global** history. It's **pinnable** and **resizable**, and those preferences are remembered.
- **Toolbar**: Back, Forward, Refresh, Print, Open containing folder, and a sidebar toggle.
- History entries show the **page title** (filename in a tooltip) and offer a **right-click menu**: Open, Open in new window, Copy filename, Open containing folder.

### Keyboard shortcuts

| Shortcut | Action |
|---|---|
| `Alt + ←` | Back |
| `Alt + →` | Forward |
| `F5` / `Ctrl + R` | Refresh |
| `Ctrl + P` | Print |
| `Ctrl + B` | Toggle sidebar |

---

## Getting started

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/)
- The Uno Platform workloads (`dotnet workload restore` from the `src` folder)
- On **Windows**: the [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (preinstalled on Windows 11)
- On **Linux**: `libgtk-3-0` and `libwebkit2gtk-4.1-0` (the WebView is backed by WebKitGTK)

### Build & run

```bash
cd src
# Windows (WinAppSDK head — most reliable WebView):
dotnet run --project mdviewx -f net10.0-windows10.0.26100 -- "../docs/index.md"

# Desktop / Linux (Skia head):
dotnet run --project mdviewx -f net10.0-desktop -- "../docs/index.md"
```

In Visual Studio, pick a launch profile and run — the default startup document is `docs/index.md`. To view any other file, pass its path as the first argument.

### File associations
- **Windows (packaged/MSIX):** `.md`/`.markdown` are declared in `Package.appxmanifest`; installing the packaged app registers it as a handler.
- **Linux:** run `packaging/linux/install-linux.sh <path-to-mdviewx>` to install a `.desktop` entry and set it as the default markdown handler via `xdg-mime`. Prebuilt binaries (and `.deb`/AUR packages) are published via the [release workflow](packaging/README.md).

---

## Tech stack

- **.NET 10**, **Uno Platform 6.5** (C# Markup, MVUX, Uno.Extensions Navigation/Hosting)
- **Markdig** for Markdown → HTML, with a custom extension for admonitions and a Mermaid code-block renderer
- **highlight.js** + **Mermaid** (bundled offline) running inside a `WebView2`
- Targets: `net10.0-windows10.0.26100` (WinAppSDK) and `net10.0-desktop` (Skia: Windows/Linux/macOS)

## Project layout

```
docs/                     Sample wiki / feature showcase (default startup doc)
src/
  mdviewx/                The app
    Presentation/         Pages (MainPage, SettingsPage) — C# Markup
    Services/             Markdown rendering, admonitions, history, preferences
    Highlighting/         Bundled highlight.js + Mermaid + themes
  packaging/linux/        Linux .desktop file + install script
```

## License

Personal project — all rights reserved unless stated otherwise.

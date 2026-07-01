# Welcome to MarkdownBlaze

A fast, **offline Markdown viewer**. You're seeing this page because MarkdownBlaze was
started without a file. Open a `.md` file to start reading — everything below is a quick tour.

:::tip How to open a document
- **Drag & drop** a `.md` file onto this window, or
- Right-click a `.md` file in your file manager → **Open with → MarkdownBlaze**, or
- From a terminal: `MarkdownBlaze "path/to/file.md"`
:::

## The toolbar

| Button | Action |
|---|---|
| ☰ | Toggle the sidebar |
| ← / → | Back / Forward through your session |
| ⟳ | Refresh the current document |
| 🖨 | Print |
| 🗀 | Open the current file's containing folder |
| ⋮ | More — opens **Settings** |

## The sidebar

Three tabs keep you oriented:

- **Headers** — a live outline of the current document. Click a heading to jump to it.
- **Session** — every document you've opened this session (back/forward history).
- **Global** — a persisted history across launches. Right-click any row for
  *Open*, *Open in new window*, *Copy filename*, and *Open containing folder*.

The sidebar is resizable and pinnable — your width and pinned state are remembered.

## Keyboard shortcuts

| Shortcut | Action |
|---|---|
| `Alt + ←` / `Alt + →` | Back / Forward |
| `F5` / `Ctrl + R` | Refresh |
| `Ctrl + P` | Print |
| `Ctrl + B` | Toggle sidebar |

## What renders

MarkdownBlaze supports CommonMark plus a rich set of extensions:

- **Tables**, **task lists**, footnotes, and auto-linked headings.
- **Syntax highlighting** for ~190 languages:

  ```csharp
  Console.WriteLine("Hello from MarkdownBlaze!");
  ```

- **Mermaid diagrams**:

  ```mermaid
  flowchart LR
      A[Open .md file] --> B{Has links?}
      B -- yes --> C[Click to navigate in-app]
      B -- no --> D[Read & enjoy]
  ```

- **Admonitions** in both Docusaurus and MkDocs styles:

  !!! note
      Local Markdown links open **inside** the app; external links open in your browser.

- **YAML front matter** is parsed and hidden.
- Local **images are inlined**, so documents render fully offline — no network access.

## Links & navigation

- Relative links resolve against the opened file's folder. Extension-less wiki links get
  `.md`; folder links resolve to `index.md`.
- Edit a file while it's open and the view **auto-refreshes**.

---

Learn more at the [MarkdownBlaze project page](https://github.com/bwets/MarkdownBlaze).

---
title: Sci-Fi Library
tags:
  - sample
  - home
---

# Sci-Fi Library

Welcome to the **mdviewx** sample library — a small hand-built wiki celebrating a few
landmark science-fiction sagas. It exists to show off what the viewer can do: linked
pages, diagrams, callouts, syntax highlighting, and a live document outline.

:::tip[How to use this]
Use the **sidebar** on the left. The **Headers** tab is the outline of the current page,
**Session** is your back/forward trail, and **Global** is every page you've ever opened.
Click any heading in the outline to jump to it.
:::

## The collections

Each collection lives in its own folder with an `index.md`. The links below point at the
*folders* — the viewer resolves them to each folder's `index.md` automatically.

| Collection | Author | Start here |
|---|---|---|
| **Dune** | Frank Herbert | [Open the Dune wing](dune/) |
| **The Foundation & Robot universe** | Isaac Asimov | [Open the Asimov wing](asimov/) |
| **Ender's Game** | Orson Scott Card | [Open the Ender wing](enders-game/) |

!!! note "About these notes"
    These pages are deliberately light on spoilers in the summaries, but the **character
    maps** and some collapsible sections do reveal relationships and fates. Tread carefully.

## What you're looking at

This very page demonstrates several features at once:

- A **YAML front matter** block (the `title`/`tags` at the top) — parsed but not shown.
- A **Docusaurus admonition** (the green *tip* above) and a **MkDocs admonition** (the *note*).
- A **table** with **folder links** that resolve to `index.md`.
- A document **outline** built from these headings — see the sidebar.

```text
Tip: edit this file in your favourite editor and save —
mdviewx reloads the view automatically.
```

Happy reading. Pick a collection above, or jump straight to the
[map of Great Houses](dune/houses) in the Dune wing.

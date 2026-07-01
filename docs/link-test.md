---
title: Link Handling Test
tags:
  - sample
  - test
---

# Link Handling Test

A scratch page for exercising how MarkdownBlaze routes clicks. The rule: **local Markdown
opens in-app; everything else opens in the matching OS default handler; nothing may navigate the
WebView away from the app.** Click each link below and confirm the behaviour noted beside it.

## Should open in the default **browser**

- [Plain https link](https://example.com) — opens in your browser.
- [http (insecure) link](http://example.com) — opens in your browser.
- [https link with path, query & hash](https://example.com/search?q=markdown#results) — full URL preserved.
- [Protocol-relative link](//example.com) — treated as `https:` and opened in the browser.
- Autolink: <https://www.rust-lang.org> — angle-bracket autolinks work too.

## Should launch the **email client**

- [Email the maintainer](mailto:gregory.kieffer@gmail.com) — opens your mail client.
- [Email with subject & body](mailto:someone@example.com?subject=MarkdownBlaze&body=Hello) — pre-fills the draft.

## Should launch **other registered handlers**

- [Phone number](tel:+3212345678) — hands off to the OS `tel:` handler (if any).
- [FTP resource](ftp://ftp.gnu.org/gnu/) — hands off to the OS `ftp:` handler (if any).
- [Blocked on purpose](javascript:alert('nope')) — shelled out, so it does **not** run inside the viewer.

## Should navigate **in-app** (local Markdown)

- [This library's home](index.md) — opens `index.md` in the viewer.
- [Extension-less wiki link](dune/houses) — resolves to `dune/houses.md`.
- [Folder link](asimov/) — resolves to `asimov/index.md`.

## Should scroll **within this page** (in-page anchors)

- [Jump to the top](#link-handling-test)
- [Jump to the browser section](#should-open-in-the-default-browser)

## Should do **nothing** (must not break the app shell)

These are links the viewer can't resolve to a local Markdown file. They should be inert — the app
must stay put rather than navigating the WebView to a dead page.

- [Root-absolute path](/etc/hosts)
- [Relative non-Markdown file](./diagram.png)
- [Relative HTML page](./some-page.html)

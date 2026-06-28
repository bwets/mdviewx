# Packaging & releases

mdviewx is released via the **`release` GitHub Actions workflow** (`.github/workflows/release.yml`),
which is **manual only** (`workflow_dispatch`) — it never runs on push.

## Running a release

Actions → **release** → *Run workflow*:

- **version** — leave empty to use `1.0.<run_number>` (the patch increases with each build), or
  type an explicit version to override.
- **publish_aur** — also push the AUR package (needs the AUR secrets below).

The workflow builds, then creates a GitHub **Release** `v<version>` with:

| Artifact | Contents |
|---|---|
| `mdviewx-<v>-win-x64.zip` | Portable Windows build (Skia desktop, self-contained) |
| `mdviewx-<v>-linux-x64.tar.gz` / `-arm64` | Self-contained Linux builds |
| `mdviewx_<v>_amd64.deb` | Debian/Ubuntu package |
| `*.msix` | Microsoft Store package (best effort — see below) |
| `SHA256SUMS` | Checksums for every artifact |

### Runtime dependencies (Linux)
The Linux builds need WebKitGTK at runtime: `libgtk-3-0` and `libwebkit2gtk-4.1-0` (declared by
the `.deb` and the AUR package).

## AUR

`packaging/aur/PKGBUILD.template` is rendered by the workflow (version + sha256 of the linux-x64
tarball) and pushed to the AUR.

**One-time setup:**
1. Create an [AUR account](https://aur.archlinux.org) and add your SSH public key to it.
2. The first publish creates the `mdviewx` AUR package (the SSH user must be allowed to push).
3. Add repo secrets:
   - `AUR_USERNAME` — your AUR account name
   - `AUR_EMAIL` — the commit email
   - `AUR_SSH_PRIVATE_KEY` — the private key whose public half is on your AUR account

Then run the release with **publish_aur = true**.

## Microsoft Store

See [microsoft-store/README.md](microsoft-store/README.md).

## Windows note
The portable Windows zip uses the Skia desktop head; on Windows the WinAppSDK/MSIX build (the Store
package) hosts the WebView natively and is the recommended Windows distribution.

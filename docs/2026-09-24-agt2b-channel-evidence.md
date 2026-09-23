# The vendors' own channels — evidence for AGT2b (2026-09-24)

AGT2b installs Claude Code and Codex at an exact pinned version from each vendor's own channel, and
verifies what it fetched, instead of going through npm (`docs/2026-09-23-agents-direction.md` §2–§3).
This records what was checked on 2026-09-24, read-only, from the vendors' documents, install scripts
and release metadata. Nothing was downloaded to run, only HEAD requests against binaries. **Every
fact here is a claim about somebody else's program**, so each carries its source, and what could not
be confirmed is listed at the end rather than guessed.

## 1. Claude Code (Anthropic)

**Layout.** Base `https://downloads.claude.ai/claude-code-releases`.
- `<base>/latest` and `<base>/stable` answer a version as plain text (2.1.281 and 2.1.273 on the
  day). `stable` is undocumented, and it works.
- `<base>/<version>/manifest.json`, and a detached signature beside it at `manifest.json.sig`
  ([setup, "Binary integrity and code signing"](https://code.claude.com/docs/en/setup)).
- `<base>/<version>/<platform>/claude.exe` on Windows, `…/claude` elsewhere. The platforms are
  `darwin-arm64`, `darwin-x64`, `linux-arm64`, `linux-x64`, `linux-arm64-musl`, `linux-x64-musl`,
  `win32-x64` and `win32-arm64`. The official Windows bootstrap picks `win32-arm64` when
  `PROCESSOR_ARCHITECTURE` is `ARM64`, and `win32-x64` otherwise.
- A compressed twin (`manifest.zst.json`, `claude.exe.zst`) is used by `bootstrap.sh` only and is
  undocumented.

**The manifest** (2.1.281):

```json
{ "version": "2.1.281", "commit": "…", "buildDate": "2026-09-23T02:33:18Z",
  "platforms": { "win32-x64": { "binary": "claude.exe", "checksum": "<sha256 hex>", "size": 240767648 }, … } }
```

`checksum` is the lowercase hex SHA-256 of the uncompressed binary, and a HEAD on that binary
answered the same `size`. Recent manifests add `manifestSignatureEnforcement`, `modsCommit` and
`sdkCompat`, which are undocumented. Older ones carry only `version`, `commit`, `buildDate` and
`platforms`.

**The signature covers the manifest.** The documentation says that *verifying the signature on the
manifest transitively verifies every binary it lists*.
- `manifest.json.sig` is an ASCII-armoured, detached OpenPGP signature: v4, RSA, SHA-512.
- The public key is at `https://downloads.claude.ai/keys/claude-code.asc`, fingerprint
  `31DD DE24 DDFA B679 F42D 7BD2 BAA9 29FF 1A7E CACE`, UID *Anthropic Claude Code Release Signing*.
- Manifests are signed from 2.1.89 on (2.1.87's `.sig` answers 404, 2.1.89's answers 200).
- Windows binaries are also Authenticode-signed by Anthropic, PBC.

**What the official installer checks: the hash, not the signature.**
- `install.ps1` redirects to `bootstrap.ps1`. That script always fetches **latest**, checks its
  SHA-256 against the manifest, and runs `claude install <target>`.
- So a pinned version is fetched by the latest binary's own subcommand, and what that subcommand
  verifies is undocumented.
- `DISABLE_UPDATES=1`, which a pinned spawn sets since AGT2a, blocks `claude install` as well.
- **So a managed pin fetches the version's own binary directly and does not go through the
  bootstrap.**

**Versions.** There is no listing endpoint: the bucket root, `/versions` and `/latest/manifest.json`
all answer 404, and version numbers skip (2.1.88 does not exist). The pointers, the repository's
`CHANGELOG.md` headings and its GitHub releases are what name versions.

**Updates** ([env vars](https://code.claude.com/docs/en/env-vars)).
- `DISABLE_AUTOUPDATER=1` stops the background update only.
- `DISABLE_UPDATES=1` blocks every update path. The documentation names it for *distributing Claude
  Code through your own channels*.

**An undocumented second channel.** The GitHub releases of `anthropics/claude-code` carry per-platform
archives and a `SHASUMS256.txt` signed by the same key. None of the setup documents mention them.

## 2. Codex (OpenAI, `openai/codex`, Apache-2.0)

**Versions.** Tags are `rust-vX.Y.Z`, and a `-alpha`/`-beta` suffix is a prerelease.
- `https://releases.openai.com/codex/channels/latest` answers GitHub-release-shaped JSON, with each
  asset's `digest` (`sha256:…`). `channels/stable` answers 404.
- `https://releases.openai.com/codex/releases/<X.Y.Z>/release.json` answers the same for one
  version, and serves its assets beside it.
- The GitHub API is the documented fallback.

**What to fetch on Windows: the package, not the bare executable.**
- `codex-package-x86_64-pc-windows-msvc.tar.gz` (arm64: `aarch64-pc-windows-msvc`) holds
  `codex-package.json`, `bin\codex.exe`, `bin\codex-code-mode-host.exe`, `codex-path\rg.exe` and
  `codex-resources\…`.
- The executable is `bin\codex.exe`. It finds `rg` and its sandbox helpers through
  `codex-package.json`, so **the bare `.exe` asset alone loses them**.

**Verification is by hash only.**
- `codex-package_SHA256SUMS` covers the package tarballs.
- Every asset's `digest` is in the release metadata.
- No signature exists for a Windows or macOS asset. Keyless sigstore bundles exist for Linux musl
  builds only, and the Windows executables are Authenticode-signed.
- The official `install.ps1` checks the SUMS file's hash against the metadata, the package against
  the SUMS line, the extracted layout, and `bin\codex.exe --version`. It has no signature check.
- It takes `-Release` / `CODEX_RELEASE`, so an exact version can be chosen.

**Updates.** `check_for_update_on_startup = false` turns the startup check off. It is documented as
*only when updates are centrally managed*. An executable outside Codex's own standalone layout gets
no update action.

## 3. Terms

- **Anthropic** ([legal and compliance](https://code.claude.com/docs/en/legal-and-compliance)):
  - The binary must be *installed and run as published* and never modified.
  - A developer may not collect, store or intermediate Claude.ai credentials. Daoris's accounts are
    directories it never reads (D49 §4, D66 §3), and that is the property this rests on.
  - Preinstalling or running Claude Code *in your products or services* is under the Commercial
    Terms.
  - The documentation acknowledges self-managed distribution: `DISABLE_UPDATES`, and "manage your
    own binary distribution".
- **OpenAI**: Apache-2.0. No page was found that speaks to third-party automated installers.

## 4. Not confirmed

- What `claude install <version>` verifies, and what `manifestSignatureEnforcement` and the darwin
  `bundle` field mean.
- Any official list of every Claude Code version.
- Whether the `anthropics/claude-code` GitHub assets are a supported channel.
- The exact contents of Codex's `.exe.zip`, and the Authenticode signer name on its Windows
  executables.
- Any vendor statement about third-party tools fetching from their release hosts automatically.

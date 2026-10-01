# The built-in list's first entries — evidence for TOOLS3 (2026-10-01)

**Carried by:** D121 and TOOLS3. It records where each line of the built-in `resources.json`
(`src/Daoris.Desktop/Daoris.Desktop.Driver/resources.json`, laid out at `app/resources.json`) was read from,
as the design's §3.2 asks. It is a record of the releases named below, not a contract: a newer version is read
again, from the same kind of source, and recorded the same way.

> Read on 2026-10-01. Every hash was read from the maker's own published checksum, and never computed first.
> Every archive's layout was read from its zip central directory by HTTP range requests. Afterwards, each whole
> file was streamed once, hashed and discarded, and all five matched the published hash and size (§6).
> `resources.test.ts` holds this document and the list together: every file the list names must appear here
> with its hash, its size and its address.

## 1. What the list holds

One version of each declared tool, for `win-x64`, because that is the platform an install is published for
(`tools/desktop-publish.mjs` publishes `-r win-x64`). Each version was the maker's newest stable release on
the day. Node.js is the exception: it is the newest LTS, not the newest release.

| Tool | Version | Archive | `exe` | `paths` | Licence |
|---|---|---|---|---|---|
| `git` | 2.56.0 (tag `v2.56.0.windows.1`) | MinGit 64-bit, zip | `cmd/git.exe` | `cmd` | GPL-2.0-only |
| `node` | 24.21.0 (LTS *Krypton*) | zip | `node-v24.21.0-win-x64/node.exe` | `node-v24.21.0-win-x64` | MIT |
| `pwsh` | 7.6.6 | zip | `pwsh.exe` | `.` (the archive's root) | MIT |
| `gh` | 2.102.0 | zip | `bin/gh.exe` | `bin` | MIT |
| `az` | 2.90.0 | zip | `bin/az.cmd` | `bin` | MIT |

Every maker publishes a zip for `win-x64` with a published sum, so no tool is left out of the list. §7 lists
the platforms that were not written.

## 2. Git for Windows: MinGit 2.56.0

- **Release page:** <https://github.com/git-for-windows/git/releases/tag/v2.56.0.windows.1>, published
  2026-09-28, and the one `releases/latest` answered.
- **Published sum:** the release notes' *Filename | SHA-256* table, which lists every artefact:
  `MinGit-2.56.0-64-bit.zip | 064b440ff870ed5198527e8f3a92cdf5bd2fd0fedf5e718af95e3fdaddeff718`.
- **Address:** <https://github.com/git-for-windows/git/releases/download/v2.56.0.windows.1/MinGit-2.56.0-64-bit.zip>
- **Size:** 39602073 bytes, from the release's asset record and the download's `Content-Range`.
- **Layout:** 376 entries, stored or deflated, none encrypted. At the root are `LICENSE.txt`, `cmd/`, `etc/`,
  `ucrt64/` and `usr/`, with no top folder. `cmd/git.exe` is the launcher that Git for Windows puts on `PATH`.
- **Licence:** Git's own `COPYING`, <https://github.com/git-for-windows/git/blob/main/COPYING>, is GPL version 2
  (*"the only valid version of the GPL as far as this project is concerned is this particular version"*).
  GitHub's detector answers `NOASSERTION` for the repository, because the file carries other notices too. MinGit
  bundles other projects under their own licences, in `ucrt64/share/licenses/` and `usr/share/licenses/`.

**What the layout says, for later rows:**
- **No `bash.exe`**, as the design expected (§2.6). The terminal's Git Bash cannot come from this git.
- **An ssh of its own:** `usr/bin/ssh.exe`, with OpenSSH's licence beside it. It answers one of TOOLS10's
  questions (*whether the minimal git carries an ssh of its own*): it does. Which ssh git calls when
  `core.sshCommand` is unset, and whether that ssh reads the person's keys, is still TOOLS10's and TOOLS11's.
- **Its own system configuration:** `etc/gitconfig`. Which keys it sets is TOOLS10's comparison.
- **`ucrt64/`**, where earlier MinGit releases had `mingw64/`. `exe` names `cmd/git.exe`, which does not move.

## 3. Node.js 24.21.0

- **Release index:** <https://nodejs.org/dist/index.json>, whose newest LTS on the day was `v24.21.0`, released
  2026-09-07, of the *Krypton* line. The newest release was `v26.10.0`, which is not LTS.
- **Published sum:** <https://nodejs.org/dist/v24.21.0/SHASUMS256.txt>:
  `158f7685b44de51f6c0df1d153526cbcd3e1bc739a8dfc607721cef75de9e541  node-v24.21.0-win-x64.zip`. The file has a
  detached signature beside it (`SHASUMS256.txt.sig`), which was not checked, since no gate holds a Node release key.
- **Address:** <https://nodejs.org/dist/v24.21.0/node-v24.21.0-win-x64.zip>
- **Size:** 37618919 bytes, from the download's `Content-Length`. Node.js publishes no size of its own.
- **Layout:** 2459 entries, stored or deflated, none encrypted, all under one top folder,
  `node-v24.21.0-win-x64/`. It holds `node.exe`, `npm`, `npm.cmd`, `npx` and `npx.cmd`, so one `paths` folder
  gives a child all three names Node.js answers for (§2.1, §2.7).
- **Licence:** <https://github.com/nodejs/node/blob/main/LICENSE> opens with the MIT grant for Node.js itself,
  then carries the notices of what it bundles (GitHub's detector answers `NOASSERTION`).

## 4. PowerShell 7.6.6

- **Release page:** <https://github.com/PowerShell/PowerShell/releases/tag/v7.6.6>, published 2026-09-08, and
  the one `releases/latest` answered.
- **Published sum, twice:**
  - the release notes' *SHA256 Hashes of the release artifacts*: `PowerShell-7.6.6-win-x64.zip`,
    `02FE458BE20493FBDF43F61EA20610B811EE6C738AB1676C61B9CFCD1A33C860`, in capitals;
  - the release's `hashes.sha256` asset, which is **UTF-16 little-endian with a byte-order mark and CRLF**:
    `02fe458be20493fbdf43f61ea20610b811ee6c738ab1676c61b9cfcd1a33c860 *PowerShell-7.6.6-win-x64.zip`.

  The two agree. The list writes the hash in lower case, and both readers accept either case and keep it lower.
- **Address:** <https://github.com/PowerShell/PowerShell/releases/download/v7.6.6/PowerShell-7.6.6-win-x64.zip>
- **Size:** 106328873 bytes, from the release's asset record.
- **Layout:** 661 entries, stored or deflated, none encrypted, with **no top folder**. `pwsh.exe` is at the
  root beside its libraries, so `exe` is `pwsh.exe` and `paths` is `.`.
- **Licence:** MIT, <https://github.com/PowerShell/PowerShell/blob/master/LICENSE.txt>, with
  `ThirdPartyNotices.txt` in the archive.

## 5. GitHub CLI 2.102.0

- **Release page:** <https://github.com/cli/cli/releases/tag/v2.102.0>, published 2026-09-30, and the one
  `releases/latest` answered.
- **Published sum:** the release's own `gh_2.102.0_checksums.txt`,
  <https://github.com/cli/cli/releases/download/v2.102.0/gh_2.102.0_checksums.txt>:
  `ae64e556ecc240b200f7eba60d550e4bb60d78e860e69dd88c449405b86067f4  gh_2.102.0_windows_amd64.zip`.
- **Address:** <https://github.com/cli/cli/releases/download/v2.102.0/gh_2.102.0_windows_amd64.zip>. The maker
  names the platform `windows_amd64`, and the list names it `win-x64`.
- **Size:** 15512013 bytes, from the release's asset record.
- **Layout:** two entries, `LICENSE` and `bin/gh.exe`.
- **Licence:** MIT, <https://github.com/cli/cli/blob/trunk/LICENSE>.

## 6. Azure CLI 2.90.0

- **Release page:** <https://github.com/Azure/azure-cli/releases/tag/azure-cli-2.90.0>, published 2026-09-01,
  and the one `releases/latest` answered.
- **Published sum:** the release notes' *SHA256 hashes of the release artifacts*:
  `c4ef59b14f0edd074427fd9981e57b0780965ccdcf6191c033fdf4b4361f33d7  azure-cli-2.90.0-x64.zip`.
- **Address:** <https://github.com/Azure/azure-cli/releases/download/azure-cli-2.90.0/azure-cli-2.90.0-x64.zip>
- **Size:** 90215627 bytes, from the release's asset record.
- **Layout:** 13252 entries, stored or deflated, none encrypted, with no top folder: an embedded Python
  (`python.exe`, `python314.dll`, `Lib/`) and **`bin/az.cmd`**, a batch file, beside `Scripts/az.bat`.
  - The executable is a `.cmd`. §2.4 already starts a `.cmd` through the shim rule D57's doors use.
  - Whether `bin/az.cmd` runs from wherever the archive is unpacked was not read. That is TOOLS11's real run.
- **Licence:** MIT, <https://github.com/Azure/azure-cli/blob/dev/LICENSE>. The archive also carries
  `CLI_LICENSE.rtf`, `NOTICE.txt` and `ThirdPartyNotices.txt`.

## 7. What was not written, and why

- **Other platforms.** An install is `win-x64`, so the list is too. For reference, on the same releases:
  - Git for Windows publishes MinGit for arm64 and 32-bit, and nothing for Linux or macOS. There, git stays
    the system's.
  - Node.js, PowerShell and GitHub CLI publish arm64 Windows zips and Linux and macOS `tar.gz` archives,
    each with its sum.
  - Azure CLI publishes no arm64 Windows zip, and publishes Linux and macOS `tar.gz` archives with sums.

  None was written, because each needs its own layout read. A location can add them without a release
  (§3.1).
- **GitHub's own digests.** GitHub's release API records a `sha256:` digest for each asset. It agreed with
  the maker's sum for all four GitHub-hosted files, but it is GitHub's computation, not the maker's published
  sum, so it is corroboration and never the source.
- **Signatures.** Node.js signs its sums file, and none of the others signs a sum that the list reads. The list
  records hashes, and D121 §3.5 says why a signed list waits.

## 8. The whole-file check

Each address was streamed once, on 2026-10-01, hashed while it arrived, and discarded:

| Tool | Bytes served | SHA-256 of what was served | Against the published sum |
|---|---|---|---|
| `git` | 39602073 | `064b440ff870ed5198527e8f3a92cdf5bd2fd0fedf5e718af95e3fdaddeff718` | matches |
| `node` | 37618919 | `158f7685b44de51f6c0df1d153526cbcd3e1bc739a8dfc607721cef75de9e541` | matches |
| `pwsh` | 106328873 | `02fe458be20493fbdf43f61ea20610b811ee6c738ab1676c61b9cfcd1a33c860` | matches |
| `gh` | 15512013 | `ae64e556ecc240b200f7eba60d550e4bb60d78e860e69dd88c449405b86067f4` | matches |
| `az` | 90215627 | `c4ef59b14f0edd074427fd9981e57b0780965ccdcf6191c033fdf4b4361f33d7` | matches |

**What this does not prove.** It does not prove that the archives unpack, start and answer their version. That
is TOOLS11's, on the install. It also proves nothing on any day after this one: a maker can move an address, and
a location fixes that without a release (§3.9). No gate fetches these addresses. §6 of the design keeps the
network out of every gate, and the `tools/resources-check.mjs` it mentions for a person editing the list is not
built.

## 9. Updating an entry

Read the maker's newest release and its published sum, as above. Then:
- read the archive's layout, so that `exe` and `paths` name what is inside it;
- write the version, the address, the sum, the size and the layout into the list;
- record them here, in the section for that tool.

`resources.test.ts` refuses a list line whose hash, size and address this document does not carry.

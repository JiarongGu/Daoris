# LAYOUT2: what each agent reads, measured without a turn (evidence note)

**Carried by:** D117 and LAYOUT2. It fills the cells `docs/2026-10-01-agent-layout-design.md` §1 left
*not measured*, per harness, at the versions named in §0. It is a record of those versions, not a
contract: a harness that moves is read again.

> Written 2026-10-01, **keylessly**: no turn was taken, no harness was logged in, no model was called.
> Every cell was read from the harness's own shipped code, or from its maker's documentation, and says
> which. The canary turn that would confirm them is §6, and it is the owner's to authorise.

## The answer: the Claude Code ACP adapter loads the repository's instructions

🔴 **`claude-agent-acp` 0.84.0 asks for project settings on every session, so the CLI it runs loads
`CLAUDE.md`, follows its `@AGENTS.md` import and lists `.claude/skills/`.** Driven Claude Code sessions
over the protocol door have been handed the repository's brief and the doctrine region. D117 §7's held
row, *the protocol door handing instructions over at `session/new`*, keeps its trigger unmet.

The chain, each link read in the shipped code:

1. **The adapter sets it.** `createSession` builds the SDK options as `systemPrompt` (Claude Code's own
   preset, `{ type: "preset", preset: "claude_code" }`), then `settingSources: ["user", "project",
   "local"]`, then spreads whatever the client put in `_meta.claudeCode.options`
   (`dist/acp-agent.js:6462`, `:6597-6601`). **source.** The older pin, 0.79.0, sets the same list
   (`dist/acp-agent.js:6043`). **source.**
2. **Daoris overrides nothing.** Its `session/new` carries `cwd`, `mcpServers` and
   `_meta.claudeCode.options.settings`, the rules file, and nothing else (`Acp.cs`, the `session/new`
   request; `Adapters.cs`, `AcpSessionMeta`). No code in the repository names `settingSources`,
   `CLAUDE_CODE_DISABLE_CLAUDE_MDS` or `CLAUDE_CODE_SIMPLE`. **source.**
3. **The SDK passes it on.** `@anthropic-ai/claude-agent-sdk` 0.3.284 turns the list into
   `--setting-sources=user,project,local` (`sdk.mjs:146`), and its own type says the list *"must include
   `'project'` to load CLAUDE.md files"* (`sdk.d.ts:2237`). **source.**
4. **Which CLI runs.** The adapter runs `CLAUDE_CODE_EXECUTABLE` when it is set, else the SDK's own
   platform binary (`dist/acp-agent.js:626-651`, `:6638`). The driver sets that variable only to a
   pinned `claude` (`Harnesses.cs`, `ClaudeAcp.PointAtClaude`), and this machine's install pins only the
   adapter. So a driven session here runs the SDK's `claude.exe`, **Claude Code 2.1.284**
   (`@anthropic-ai/claude-agent-sdk-win32-x64` 0.3.284; its hash matches the SDK manifest's win32-x64
   checksum). **source.**
5. **The CLI loads what the sources allow.** The instruction-file loader reads project files only when
   the `projectSettings` source is enabled, and `--setting-sources` is what enables it (the loader
   module embedded in `claude.exe` 2.1.284 at byte offset `0xc8950cd`: `We=av("projectSettings")` gates
   `projectFiles`; `av`, at `0xc8640b8`, asks `lr`, which reads the enabled sources, at `0xc124ad1`).
   There is no trust check in that path. **source.** The embedded changelog agrees: 2.1.281 *"Fixed
   CLAUDE.md and rules files from an `--add-dir` directory … being sent to the model twice in headless
   and SDK sessions"*. **doc.**

**What would switch it off**, so the first symptom has a name:

- a client that puts its own `settingSources` in `_meta.claudeCode.options`, which the spread in step 1
  lets win. Daoris sends none;
- `CLAUDE_CODE_DISABLE_CLAUDE_MDS` in the spawn's environment, which the loaders check and the CLI's
  safe mode sets. Daoris sets neither;
- **a maker-side flag, `tengu_paper_halyard`**, which drops every `Project` and `Local` instruction file
  from what the model is sent when it is on (`0xc8950cd`, `IRn` and `Wrt`). It defaults to off. It is
  the one switch in this chain that nobody on this machine controls, and only a turn shows it is off
  for an account.

**DEPLOY1's open question, half answered.** DEPLOY1 could not tell whether the adapter ignored an
untrusted room's allow-list because the room was untrusted or because project settings were never read.
They are read (step 1). So the ignored allow-list was the CLI's own trust gate. The trusted-room probe
that DEPLOY1 left to the owner would still confirm that from the other side.

## 0. What was read, at which version

| Harness (toolchain entry) | Version | Where it runs on this machine | What was read |
|---|---|---|---|
| Claude Code (`claude-code`) | **2.1.285** | on `PATH`, the native build | the JavaScript modules embedded in `claude.exe`, extracted as text runs. The cells were read in 2.1.284's copy of the same modules and checked against 2.1.285 (§1) |
| Claude Code over ACP (`claude-code-acp`) | adapter **0.84.0**, SDK **0.3.284**, CLI **2.1.284** | the install's pin; `npm pack` of the same version is byte-identical (`dist/acp-agent.js` sha256 `118db041…`) | the adapter's `dist/`, the SDK's `sdk.mjs` and `sdk.d.ts`, and the SDK's `claude.exe` |
| codex (`codex`) | **0.154.0** | not installed, not pinned | `@openai/codex@0.154.0-win32-x64`'s `codex.exe` as strings, and the maker's source at the tag `rust-v0.154.0` |
| codex over ACP (`codex-acp`) | adapter **1.12.0**, codex **0.154.0** | not installed, not pinned | `@agentclientprotocol/codex-acp@1.12.0`'s `dist/index.js`, and codex as above |
| dsh (`dsh`) | **0.1.6-alpha.2** | not installed, not pinned | `dsh-agent-instructions`, `dsh-skill-filesystem`, `dsh-skill`, `dsh-base` and `dsh-acp-app` at that version, source and shipped READMEs |
| a harness a plugin declares (D64) | — | — | not measured: unknown per harness |

codex, codex-acp and dsh are measured at the versions the dsh evaluation and ACP3 ran, because nothing
newer is on this machine. Each has moved since: codex **0.159.2**, codex-acp **2.0.1**, dsh
**0.2.0-rc.2**. codex's discovery was read again at `rust-v0.159.2`: `ext/skills/src/host_roots.rs` is
unchanged, and `core/src/agents_md.rs` gains only a filter on fallback file names and host-supplied
*thread* instructions. The other two were not read at their newest.

**Labels.** **source**: read in the harness's shipped code, at the place cited: a line for JavaScript, a
line of the maker's source at the release tag for codex, and for Claude Code, whose shipped code is one
binary, the JavaScript module text embedded in it, cited by the byte offset of the run that holds it.
**source (string)**: a constant in a native binary, which shows the program holds it and not how it uses
it. **doc**: the maker's documentation, shipped READMEs and the changelog Claude Code embeds.
**not measured**: nothing above reached it.

## 1. Claude Code, native (2.1.285) and over ACP (2.1.284)

One loader serves both doors: over ACP it runs with the three sources enabled, which is the native
default. The cells below were read in 2.1.284 (the ACP door's CLI). In 2.1.285 the same loader carries
the same byte limit (4,194,304), the same import pattern and the same import depth (5), and the same
AGENTS.md module and skill markers. The per-file offsets are 2.1.284's.

| Cell | Value | Label |
|---|---|---|
| Instruction files at the working directory | `CLAUDE.md`, `.claude/CLAUDE.md`, `.claude/rules/*.md` (project) and `CLAUDE.local.md` (local); plus the user's `CLAUDE.md` and `rules/` in its configuration home, which for a Daoris account is the profile. The loader `xRn` at `0xc8950cd` | source |
| `AGENTS.md` | 🔴 **read only where the project has no `CLAUDE.md` of its own**, since 2.1.277. A built-in module, `agents-md`, loads `AGENTS.md` and `.claude/AGENTS.md` *"exactly where and how CLAUDE.md would be"* when no `CLAUDE.md` is found at the session's root or in a folder above it; its setting `instructionFiles` can instead load both, or neither (`0xdfb10c0`). It is on by default behind a maker-side flag, `tengu_agents_md_mod`, which defaults to on. The changelog: 2.1.277, *"Added AGENTS.md support: in a project with no CLAUDE.md, Claude Code reads AGENTS.md instead"* | source; doc |
| Folders above the working directory | every folder from the top of the drive (not the drive's root itself) down to the working directory, each read as above (`xRn`'s walk, `0xc8950cd`). A driven tree lives under the Daoris home (`SessionTrees.TreesRoot`), so a `CLAUDE.md` in any folder above the home reaches every driven Claude Code session | source |
| Nested below it | loaded when the Read tool reads a file below the working directory: each folder between the two has its `CLAUDE.md`, `.claude/CLAUDE.md`, `CLAUDE.local.md` and `.claude/rules/` loaded (`FileReadTool` pushes `nestedMemoryAttachmentTriggers`, `0xc8d3792`; `CVt` and `n9o` pick the folders, `INn` loads them, `0xcb088f4` and `0xc8950cd`). A nested `AGENTS.md` alone is loaded the same way only where the project has no `CLAUDE.md`, or in the both mode (the `agents-md` module's Read hook, `0xdfb10c0`) | source |
| `@path` imports | followed: `@` then a path, in markdown text and HTML comments, not in code spans or blocks; `#…` stripped; resolved against the importing file's folder; to a depth of 5; an import outside the working directory is skipped unless the person approved external imports (`SRn`, `kRn=5` and `c9`, `0xc8950cd`) | source |
| `@path` inside a nested file | followed: the nested loader reads through the same `c9`, which follows imports at every level | source |
| Skill roots | the managed root; the configuration home's `skills/`; `.claude/skills/` in the working directory and each folder above it up to the repository's root, never the home folder (`Die`, *"getProjectDirsUpToHome"*, `0xc85f858`); the main checkout's `.claude/skills/` for a linked worktree that has none; each `--add-dir`'s; `.claude/commands/`; plugins; and `<folder>/.claude/skills/` for each folder between a file the Read tool reads and the working directory, unless git ignores that folder (`H1o`, `B1o`, `0xca9a19d`). Project skills need the `projectSettings` source | source |
| `.agents/skills/` | **not read.** The only code that reads it is an importer from other agents' layouts, which copies `.agents/skills/` and `~/.agents/skills/` into `.claude/skills/` (`0xd39a034`). That is D117's mirror, done once by a person | source |
| A link checked out as text | read as a regular file holding the path. A 9-byte `CLAUDE.md` reading `AGENTS.md` is loaded as the text `AGENTS.md`: there is no `@`, so it imports nothing, and because a `CLAUDE.md` exists the `agents-md` module loads no `AGENTS.md` either. The session gets a path and no instructions. `.claude/skills` held as a text file yields no skills: the loader reads it as a folder, and a file lists nothing (`jI`, `0xca9a19d`) | source |
| Byte limit | **4,194,304 bytes per instruction file** (`tX`, `0xc893a66`). A larger file is skipped whole, with a debug line (*"not a regular file or exceeds … byte limit"*); nothing is cut. Separately, a start-up notice fires over 40,000 characters in one file, or 5% of the context window if that is more, and over 120,000 in all (`iRn`, `cqe`, `aRn`); it is a notice, not a cut | source |
| A skill in two roots | listed once only when both paths are the same file: skills are deduplicated by resolved path (`k1o`, through `realpath`; *"Skipping duplicate skill … (same file already loaded from …)"*, `0xca9a19d`). Two copies are two files and stay two entries at the loader. How two same-named entries then show in the listing: not measured | source; not measured |

**The layout, read against these cells.** D117's root `CLAUDE.md` holding `@AGENTS.md` carries the
region to both doors. A room needs its pointer: with a root `CLAUDE.md` present, a room's `AGENTS.md`
alone is never loaded in the default mode, and a room's `CLAUDE.md` holding `@AGENTS.md` is loaded,
with the import, the first time the Read tool reads a file in that room. The `agents-md` module does not replace the
pointers. It fires only where no `CLAUDE.md` exists in the project or above it, a maker's flag or a
person's setting can turn it off, and builds before 2.1.277 do not have it.

**One observation this does not reconcile.** At 2.1.284 the loader carries a rule for a linked worktree
nested inside its main checkout (`Brt`/`jrt`, `0xc8950cd`) that reads as leaving the main checkout's
own folders out of the ancestor walk. The session that wrote D117, and this session, both in such a
worktree on builds that were not recorded, were handed the enclosing checkout's `CLAUDE.md`. Driven
trees are not nested in their checkout, so this does not reach a driven session. The canary's
fixture sits in a folder, not a worktree, so it does not settle it either.

## 2. codex, native (0.154.0)

| Cell | Value | Label |
|---|---|---|
| Instruction files | per folder, the first of `AGENTS.override.md`, `AGENTS.md` and `project_doc_fallback_filenames` (none by default) that exists, one file per folder (`core/src/agents_md.rs:39-42`, `:237-265`, `:267-281` at `rust-v0.154.0`), after the user's own instructions from `CODEX_HOME`. `CLAUDE.md` is not read | source |
| 🔴 Trust | **no project file at all in an untrusted project**: only the user's own instructions load (`agents_md.rs:61-63`) | source |
| Folders above the working directory | from the project root, the nearest folder holding a `.git` (`project_root_markers = [".git"]`), down to the working directory; never above the root; a repository cannot change the markers (`agents_md.rs:8-16`, `:195-235`; the packaged defaults in `codex.exe`, `0xda8564b`) | source; source (string) |
| Nested below it | not loaded. The default prompt tells the model the scope rule, *"The scope of an AGENTS.md file is the entire directory tree rooted at the folder that contains it"* (`codex.exe`, `0xe0d3929`), so the model may read one itself; the harness hands none over | source; source (string) |
| `@path` imports | not interpreted: files are concatenated as read (`agents_md.rs:141-176`) | source |
| `@path` inside a nested file | nothing nested is loaded | source |
| Skill roots | `.agents/skills/` in every folder from the project root down to the working directory, when it is a folder (`ext/skills/src/host_roots.rs:137-185`); the project's `.codex/skills/`; `$CODEX_HOME/skills/` (deprecated), `~/.agents/skills/` and a system cache; the system configuration's `skills/`; plugins (`host_roots.rs:73-131`). `.claude/skills/` is not a root | source |
| A link checked out as text | an `AGENTS.md` held as text is read as the path it holds; symlinks are followed (`agents_md.rs:185-186`). `.agents/skills` held as a text file is not a folder and is skipped silently (`host_roots.rs:169-175`). codex reads neither `CLAUDE.md` nor `.claude/skills`, so the reference's two links do not touch it | source |
| Byte limit | 🔴 **32,768 bytes for the whole project chain** (`project_doc_max_bytes`, the packaged default, `codex.exe` `0xda8564b`). Read from the root down; the file where the budget runs out is cut at that byte, with a warning in the log, and any deeper file is dropped (`agents_md.rs:65-91`, `:138-176`). A session at the root reads one file, so a root `AGENTS.md` over 32,768 bytes loses its tail, which in D117's layout is the region's last rules | source; source (string) |
| A skill in two roots | **listed twice**: roots are deduplicated by path and skills by `SKILL.md` path, never by name (`host_roots.rs:271-274`; `ext/skills/src/loader/host_merge.rs:232-249`). Two copies of one skill, say in `.agents/skills/` and `~/.agents/skills/`, are two entries | source |

## 3. codex over ACP (codex-acp 1.12.0 over codex 0.154.0)

The adapter starts codex's own app server and adds no instructions, so every cell of §2 holds, except
trust.

| Cell | Value | Label |
|---|---|---|
| What it runs | `codex app-server`: the codex it carries (`@openai/codex` `^0.154.0`), or `CODEX_PATH` (`dist/index.js:22098-22106`) | source |
| What `session/new` passes | `threadStart` with `cwd`, a config and the model provider; no base or developer instructions (`dist/index.js:28596-28603`) | source |
| 🔴 Trust | **the adapter marks the session's root, and each additional directory, `trust_level: "trusted"`** in the config it hands codex (`dist/index.js:28703-28708`). So a driven codex session loads the project's `AGENTS.md` chain in any folder, where native codex would load none until trusted | source |
| Skill roots | codex's (§2), plus `.agents/skills/` in each additional directory (`dist/index.js:28749-28762`) | source |
| Instruction files, folders above, nested, imports, links, byte limit, a skill in two roots | as §2 | source |

## 4. dsh (0.1.6-alpha.2), native and over ACP

Daoris drives dsh as `dsh --profile acp`, the automation app, which its own patch file names *"The
automation-only ACP application over dsh-base"* (`dsh-acp-app/cordis.patch.yml:1`). So dsh-base's rows
for instructions and skills are the ones mounted, on both doors (`dsh-base/cordis.patch.yml:275-284`).

| Cell | Value | Label |
|---|---|---|
| Instruction files | `AGENTS.md` and `CLAUDE.md`, then the overlays `AGENTS.local.md` and `CLAUDE.local.md`, in each folder; `$DSH_HOME/AGENTS.md` first (`dsh-agent-instructions/lib/index.js:16-19`, `:552-580`). Two files in one folder whose trimmed contents match load once; different contents both load (`:632-648`) | source |
| Folders above the working directory | from the project root, the nearest folder holding `.git`, or the working directory if none, down to the working directory; never above the root (`:480-508`) | source |
| Nested below it | loaded when a `read`, `write` or `edit` call touches a file below the working directory: each folder between them gains its files on the next request (`:515-521`, `:939`, `:1087-1089`; the README, *"Main flow"*). A shell `cd` does not count | source; doc |
| `@path` imports | not interpreted, at any level: *"lowercase names, `.claude/rules/`, and `@path` imports are not interpreted"* (the package README, *"Known Limitations"*). A `CLAUDE.md` holding the import region loads as its few bytes of text | doc |
| Skill roots | `<project root>/.dsh/skills` (rank 100), `<project root>/.agents/skills` (200), `customSkillDirs` (300), `$DSH_HOME/skills` (400), `~/.agents/skills` (500), one level deep (`dsh-skill-filesystem/lib/index.js:21-25`, `:150-187`). Only the project root's, never a nested folder's. `customSkillDirs` resolves once against the process's working directory (ACP3 §5) | source |
| A link checked out as text | read as a regular file: a 9-byte `CLAUDE.md` reading `AGENTS.md` loads as that text beside the real `AGENTS.md`, since their contents differ. A real link is followed, and collapses into its target (the README, *"Per-directory dedup"* and *"Symlinked instruction files"*). A skill root held as a text file answers `ENOTDIR`, which is read as an absent root: no skills, no warning (`dsh-skill-filesystem/lib/index.js:572-577`, `:639-651`) | source; doc |
| Byte limit | **65,536 bytes for the whole rendered baseline** (dsh-base's `maxBytes`), and **1,048,576 per source file**; a larger file is dropped whole (`:603-622`). Over the budget, whole files are dropped from the broadest end first, then the most specific file is cut, and a *"Workspace instruction budget"* notice names both (`:293-372`). In one folder `AGENTS.md` comes before `CLAUDE.md`, so at the root `AGENTS.md` is dropped before `CLAUDE.md` | source |
| A skill in two roots | 🔴 **listed once.** Within a provider's layer the lowest rank wins a name and each later one is dropped with a logged warning, *"skill … ignored because a higher-priority skill already exists"* (`dsh-skill/lib/index.js:312-330`). A skill in both `.agents/skills/` (200) and a `customSkillDirs` root `.claude/skills` (300) is listed once, from `.agents/skills/` | source |

**HELP2, read against these cells.** D117 held a change to the dsh profile's `customSkillDirs` until
this measurement said whether dsh lists a mirrored skill twice. It does not: the `.agents/skills/` copy
wins, and the mirror costs one warning line per skill per catalog build. The profile can keep the root
for as long as any repository on the machine is on the `.claude` layout. What remains is log noise.

## 5. The layout's questions, answered from these cells

| D117 asked | Answer | Label |
|---|---|---|
| Does the Claude Code ACP adapter load project instructions? | **Yes**, and nothing in Daoris switches it off (the opening) | source |
| Which file do all three read? | `AGENTS.md`: codex and dsh natively, Claude Code through the root `CLAUDE.md`'s import (or natively, where a project has no `CLAUDE.md`) | source |
| Which skill root do most of them read? | `.agents/skills/`: codex, per folder from the root to the working directory, and dsh, at the root. Claude Code reads only `.claude/skills/` | source |
| Is a nested instruction file loaded? | Claude Code: a nested `CLAUDE.md`, and its imports, when a file there is read. dsh: nested `AGENTS.md` and `CLAUDE.md` when a file there is touched. codex: never. D117 §1's rule, *nothing relies on a nested file being loaded*, stands | source |
| The smallest limit on the root file | **codex, 32,768 bytes**, cutting the tail. dsh caps the whole chain at 65,536 bytes; Claude Code skips a file over 4 MiB and never cuts one. So the figure D117 §5.2's `check` report is to compare the root `AGENTS.md` with is 32,768 | source |
| Does dsh list a mirrored skill twice? | No (§4) | source |
| Does a link held as text reach an agent as a path? | Claude Code: yes, and the path is all it gets. dsh: the path loads beside the real file. codex: reads neither linked file | source |

The root files today: `AGENTS.md` is 22,758 bytes and `CLAUDE.md` 24,405. codex reads the first whole.
dsh reads both, 47,163 bytes with no user file, under its 65,536. Claude Code reads both, through the
import.

## 6. The canary turn: the owner's to authorise

Source says what each program contains. A turn shows what a session is handed, on the owner's login.
One turn per door and fixture, the same prompt everywhere. Nothing here has been run.

### 6.1 The fixtures

Built under `local/scratch/layout2-canary/`, which git ignores, never in a real repository. `canary/`
is a plain folder that stands in for *a folder above the repository*, so only `repo/` sits under it.

```
layout2-canary/
  canary/
    CLAUDE.md                  CANARY-ABOVE-CLAUDE quill-7310
    AGENTS.md                  CANARY-ABOVE-AGENTS fern-2294
    repo/                      git init; the session's working directory
      CLAUDE.md                CANARY-ROOT-CLAUDE cobalt-5186, then a line: @AGENTS.md
      AGENTS.md                CANARY-ROOT-AGENTS lumen-8843, then a line: @docs/imported.md
      docs/imported.md         CANARY-IMPORTED saffron-3627
      .claude/rules/canary.md  CANARY-RULES basalt-9051
      room/AGENTS.md           CANARY-ROOM-AGENTS tundra-4470, then a line: @notes.md
      room/notes.md            CANARY-ROOM-IMPORTED orchid-1398
      room/CLAUDE.md           the pointer alone: @AGENTS.md
      room/probe.txt           nothing to see here
      bare/AGENTS.md           CANARY-BARE-AGENTS harbor-6602      (a room with no pointer)
      bare/probe.txt           nothing to see here
      .claude/skills/canary-claude/SKILL.md   name: canary-claude, description: CANARY-SKILL-CLAUDE garnet-2715
      .agents/skills/canary-agents/SKILL.md   name: canary-agents, description: CANARY-SKILL-AGENTS willow-5839
      .claude/skills/twin/SKILL.md            name: twin, description: CANARY-TWIN-CLAUDE cinder-7024
      .agents/skills/twin/SKILL.md            name: twin, description: CANARY-TWIN-AGENTS meadow-3561
  linked/                      git init, core.symlinks=false (below)
    AGENTS.md                  CANARY-LINKED-AGENTS prism-4128
    CLAUDE.md                  mode 120000, target AGENTS.md: a 9-byte text file on checkout
    .claude/skills             mode 120000, target ../.agents/skills: a text file on checkout
    .agents/skills/canary-linked/SKILL.md   name: canary-linked, description: CANARY-SKILL-LINKED ember-6093
  big/                         git init
    AGENTS.md                  exactly 40,000 bytes: CANARY-HEAD dune-1180 on line 1,
                               CANARY-MID reef-7746 near byte 30,000, CANARY-TAIL spire-2253 near byte 39,900,
                               filler between
    CLAUDE.md                  @AGENTS.md
```

Commit each repository once. `linked/`'s two links are made as git objects, then checked out:

```sh
git -C linked config core.symlinks false
git -C linked add AGENTS.md .agents
git -C linked update-index --add --cacheinfo 120000,$(printf 'AGENTS.md' | git -C linked hash-object -w --stdin),CLAUDE.md
git -C linked update-index --add --cacheinfo 120000,$(printf '../.agents/skills' | git -C linked hash-object -w --stdin),.claude/skills
git -C linked commit -qm fixture
git -C linked checkout -- CLAUDE.md .claude/skills
git -C linked ls-files -s CLAUDE.md .claude/skills     # both 120000; CLAUDE.md on disk is 9 bytes
```

This checkout's own `CLAUDE.md`, and any `CLAUDE.local.md` in the folders between, sit above every
fixture, so Claude Code also loads them. They hold no `CANARY-` token, so they do not disturb the
answer. They are also why one cell cannot be tried here: Claude Code reading `AGENTS.md` with no
`CLAUDE.md` in or above the project. That needs a folder with no `CLAUDE.md` above it, which the owner
chooses, holding a git repository `solo/` with `AGENTS.md` (`CANARY-SOLO-AGENTS lantern-9907`),
`room/AGENTS.md` (`CANARY-SOLO-NESTED thistle-3364`) and `room/probe.txt`.

### 6.2 The prompt

The same text for every door and every fixture:

> This is a probe of what you were given. Change nothing. First, without using any tool, quote every
> token that begins with CANARY- in your instructions or context so far, one per line, with the word
> after it, exactly as written, or write NONE. Second, list each skill available to you whose name
> begins with canary- or is exactly twin, with its description exactly as written. Third, read
> room/probe.txt and bare/probe.txt with your file-reading tool, not a shell command; where a file does
> not exist, say so and go on. Fourth, quote every CANARY- token that became visible to you after those
> reads and was not in your first answer.

### 6.3 The runs

Each in the fixture repository, on the owner's profile, with the permission posture Daoris sets. Every
permission request is refused, as the driver refuses it (D52). Keep every output under the scratch
folder.

| Door | Command |
|---|---|
| Claude Code, native | `claude -p "<prompt>" --output-format json`, with `CLAUDE_CONFIG_DIR` the owner's profile. Record `claude --version` |
| Claude Code over ACP | `node tools/dsh-probes/acp-client.mjs --cmd node --args "<the pinned claude-agent-acp 0.84.0>/dist/index.js" --cwd <fixture> --prompt "<prompt>" --log <scratch>/claude-acp-<fixture>.jsonl --permission reject`, with `CLAUDE_CONFIG_DIR` the owner's profile and `CLAUDE_CODE_EXECUTABLE` **unset**, as a driven session on this machine runs today |
| codex over ACP | the same client, `--cmd node --args "<codex-acp 1.12.0>/dist/index.js"`, with `CODEX_HOME` a logged-in profile that exists (ACP3 §3) |
| dsh over ACP | the same client, `--cmd dsh --args "--profile\|acp"` (the client splits its arguments at `\|`), with `DSH_HOME` a home holding the profile Daoris writes (with `customSkillDirs` naming `.claude/skills`), `DSH_PERMISSION_MODE=workspace-write` and `DSH_TELEMETRY_DISABLED=1` |

codex has no native adapter in Daoris, and its native cells differ from the ACP door's only in trust.
Run it natively only to see the untrusted cell: `codex exec "<prompt>"` in an untrusted folder should
name no project token.

### 6.4 What the source predicts

A cell that comes back different from its prediction is a finding. Record it in this note beside the
cell, labelled **turn**.

| Fixture, answer | Claude Code (both doors) | codex over ACP | dsh over ACP |
|---|---|---|---|
| `repo`, first | quill, cobalt, lumen, saffron, basalt | lumen | cobalt, lumen |
| `repo`, skills | canary-claude; twin once (cinder) | canary-agents; twin once (meadow), since codex does not read `.claude/skills` | canary-agents, canary-claude; twin once (meadow), and a warning in dsh's log |
| `repo`, after the reads | tundra, orchid (the room's pointer and its imports); not harbor | nothing | tundra and harbor; not orchid |
| `linked`, first | none: the text `AGENTS.md` and no token | prism | prism, and the 9 bytes as text |
| `linked`, skills | none | canary-linked | canary-linked |
| `big`, first | dune, reef, spire | dune, reef; **not spire** | dune, reef, spire |
| `solo`, first / after | lantern / thistle (2.1.277 or later) | lantern / nothing | lantern / thistle |

## 7. What this note does not establish

- **No turn was taken**, so nothing here says what a model was sent, only what the programs contain.
  §6 is how that is found.
- **Claude Code's source is read from its binary.** The modules embedded in `claude.exe` are plain
  JavaScript, and the cells cite them. Names like `xRn` are the minifier's and change between builds;
  the byte offsets hold for 2.1.284 only.
- **The listing of two same-named Claude Code skills** after the loader: not measured.
- **The worktree observation in §1** is not reconciled.
- **codex, codex-acp and dsh at their newest** were not read, except codex's two discovery files.
- **A harness a plugin declares** is unknown until its plugin says which harness it runs.
- `verify` checks this note's links and budget, and none of its words.

# Tools: the system's, managed by Daoris, or a file you name (TOOLS1)

**Status: the contract for TOOLS1, recorded as D121 before any code. TOOLS2 is built:** `tools.json`, its rules
and its resolution (§2.1–§2.3), twins in `tools.ts` and `Tools.cs`, and `daoris tool list|path|use <tool>
system|file <path>`. Nothing starts a tool through them yet (TOOLS5). TOOLS3 is built: `resources.json` schema 1, the
platform table and the merge (§3.1–§3.5), twins in `resources.ts` and `ToolResources.cs`, and the list built in at
`app/resources.json`, whose first entries `2026-10-01-tools-resources-evidence.md` records. TOOLS4 is built: a
version planned, downloaded, verified, unpacked and laid out (§3.6, §3.7), twins in `toolinstall.ts` with `zipfile.ts`
and `ToolInstall.cs`, `daoris tool download|use … managed|update|delete|locations|look`, and the driver's download as a
followed action. TOOLS5 is built: one answer for every child (§2.4, §2.6, §2.7), twins in `Tools.Children.cs` and
`tools.ts`. Daoris's own git, a hook's first word, a pin's `npm` and the tree guard's node are the resolved files, and
every child the driver and the modules start is handed the tools' `PATH`, held by source scans. `GIT_CONFIG_GLOBAL` is
TOOLS6's. D121's notes say what each row settled that this document left open. The owner, 2026-10-01:

> *"all tools that daoris using like git, [terminal] should all have a self managed option (and can be setup
> in settings) which can be download from locations … I perfer provide default download location and built
> into the app with a resouce json file that can be updated if need other resouce location"*

It extends the toolchain design (`2026-09-22-toolchain-design.md`, D57). D57 made an agent's binary Daoris's
to choose: *explicit command → managed pin → `PATH`*, and a pin nobody installed refuses. This design does the
same for every other program Daoris starts that is neither an agent nor Daoris's own.

- §1 is what the code does today, read from the files it names at `d618cbb`.
- §2–§5 are the design.
- §6 is what gates can prove, and what only a real download can.
- §7 is the build, as rows.
- §8 is what was not chosen.

## 1. What Daoris runs today

### 1.1 The inventory

Every external program an install starts. The driver library, the headless host, the desktop modules, the
application and the launcher were read, and so were the CLI, the service, the example plugins and the plugin
kit's templates. Almost every start goes through one of five places:
- `WorkingTree.GitAsync` for git;
- `Spawning.Shell` and `HarnessProbe.Apply` for an agent (D57);
- `HookProcess.StartInfo` for a plugin;
- `PseudoConsole.Start` for the terminal;
- `toolchain.ts` for everything the CLI starts (it is the only module that may spawn, held by a dogfood test).

| Program | Who starts it | How it is found today | When it is missing or wrong |
|---|---|---|---|
| **git**, by Daoris | The driver library, every tick and every press. `WorkingTree.GitAsync` and `GitBytesAsync` carry every git call in `WorkingTree`, `SessionTrees` (open, merge, landed, hand-off, review, leftovers, bringing up to date) and `FilePreview`: about sixty subcommands, from `status --porcelain` to `worktree add`, `merge`, `rebase --onto` and `fetch --no-write-fetch-head` | `FileName = "git"`, bare. The process's own `PATH`, with no PATHEXT lookup (.NET appends only `.exe` to a bare name), no setting, and no variable. `-c core.longpaths=true` on every call; `GIT_TERMINAL_PROMPT=0` on the fetch | `GitAsync` returns the system's own error text as git's answer. A tick holds the quest (*not a spawnable tree — git status failed: …*), a session tree does not open, a review and a preview read nothing, and a look says git's words on each row. No refusal code names git, and no version is checked, though the fetch needs 2.29 (D109 as amended by WSR7) |
| git, by a session | The agent's own shell | The PATH the session inherited from the application | The agent's command fails, in its own words |
| git, by a landing plugin | `git push -u origin <branch>` in both example landing plugins (`land.mjs`, D100) | Each plugin's own PATH and PATHEXT walk | `pushed: false`, with git's reason |
| **Node.js** (`node`, `npm`, `npx`) | Rarely Daoris itself. A plugin's hook command (`["node", "${plugin}/…"]`, which the kit writes by default); a plugin's servers (`npx …`, started by the agent); the tree guard, written into a session's settings as `"command": "node"` for the agent to start (`Permissions.cs`, `TreeGuard`); the npm-installed agents' own shims (the ACP adapters, dsh). `npm` directly: `agent install` (`npm install -g`), a package pin (`npm install --prefix`) and a package update (`npm view`), in both artefacts. The CLI itself runs on node | A hook's first word is handed to `Process.Start` bare (`HookProcess.StartInfo`), so a `.cmd` is not found. `npm` goes through the harness door's PATHEXT shim (`HarnessActions.WindowsShim`; `spawnable` in `toolchain.ts`). The tree guard and a plugin's servers resolve on the agent's own PATH | A hook: *could not start*, logged and skipped; a plugin at `quest/consider` fails closed, so the quest holds. npm: *could not be started — it is not on this machine's PATH*. The tree guard: the agent starts nothing, and whether the agent then blocks the write or lets it through is the agent's rule, **not measured here** |
| **The terminal's shells** (`pwsh`, `powershell`, `cmd`, Git Bash) | `TerminalShells.Available`, then `PseudoConsole.Start` (CONSOLE4, D96) | `CommandPresence.Resolve` on the process PATH. Git Bash is the `bin\bash.exe` within three folders above the git PATH finds, never WSL's `bash` | `TERMINAL_SHELL_MISSING` for one named, or `TERMINAL_NO_SHELL` |
| **GitHub CLI** (`gh`) | `examples/plugins/github-pull-request/land.mjs`: `gh pr create` | The plugin's own PATHEXT walk | The branch is pushed, and the answer says *gh did not open the pull request — `gh` is not on this machine's PATH*. The README's *What it needs* lists it; nothing checks it |
| **Azure CLI** (`az`) | `examples/plugins/azure-devops-pull-request/land.mjs`: `az repos pr create` | The same | The same, for Azure DevOps |
| Agents | The harness doors, probes and agent actions | D57: explicit command → managed pin → PATH, through `HarnessProbe.Apply` | D57's refusals |
| Daoris's own programs | The HTTP host (`ServiceHostLocator`), the MCP host (`KnowledgeConnector.Locate`), the application from the launcher, the browser (the application with `--daoris-browser`, D99) | Their own locators | Their own sentences |
| The system's own programs | Edge (`EdgeBrowser.Locate`), the file manager (the kit's shell launcher) | Fixed locations | Their own sentences |
| The service | Nothing. It starts no process (its own comments say so, in `Remote.cs` and the HTTP host's `Program.cs`, and no test holds it). The remote's sync is JSON over HTTP (D68), and the knowledge feed, which needs git, asks the checkout's driver (D69, `FeedOrder`) | — | — |

**Out of scope, and why.**
- This repository's dev tools (`tools/*.mjs` start `dotnet`, `npm`, `git` and `powershell`) and the release
  workflow run on a developer's machine or a runner, never in an install.
- The devkit starts `git` and a repository's declared gate lines (`Process.cs`, with its own PATHEXT lookup),
  but in a repository's gates, not in an install. Once the queue (D115) runs a repository's declared gates from
  the driver, those lines are the driver's children and get the tools' environment (§2.4) like any other.
- An agent is D57's, and §3.8 says why it stays there.
- Daoris's own programs are found by locators D60 and D93 already gate.
- The system's own programs (Edge, the file manager, the Windows shells `powershell` and `cmd`) are the
  system's by definition.

### 1.2 The measured example: WSR7

On the owner's workspace of 29 repositories, **every fetch failed** with *Could not read from remote
repository*. The git on `PATH` could not reach that machine's SSH remotes, while the person's own Git client
could. WSR7 built what it could within the words: it says once, first, what was not fetched, and names what the
git on the path needs (*a key its own ssh reads, or `core.sshCommand`*). It could not make the fetch work,
because nothing in Daoris chooses which git runs or what that git carries. Which ssh the person's own client
uses was not measured. A Git client that ships its own git and ssh does not lend them, and a git on PATH with its
own bundled ssh does not read keys that another ssh's agent holds.

### 1.3 What the inventory shows

- **Git is the program Daoris can least do without, and the one it chooses least.** It is found by a bare name
  that nothing resolves, its version is never asked, and nothing but the command line can hand it a setting.
- **Five resolvers answer one question**:
  - `CommandPresence.Resolve`, for the terminal and plugin presence;
  - `HarnessActions.WindowsShim`, for agents and npm;
  - the CLI's `onPath` and `spawnable`;
  - each landing plugin's own `which`;
  - the kit's `run.mjs`.

  Git and a hook's command use none of them.
- **A child's tools are whatever `PATH` it inherited.** USE1g is the same fact from the other side: a start from
  Git Bash handed sessions a `PATH` in Windows form that their shell could not read.
- **Nothing records which git or node ran.** A session record names its agent's version (D49 §4). Nothing names
  the version of the git that grew its tree or landed its work.

## 2. Three ways to run a tool

### 2.1 The tools, declared in code

A tool is a program Daoris starts, or hands to a session, that is neither an agent (D57) nor Daoris's own.
**The set is declared in code, twice (§5), never read from a manifest.** A resource location (§3) can offer
versions of a tool this build knows. It can never make Daoris run a program its code does not name.

| Id | Name | The names it answers for on a child's PATH | Version asked by | Settings it may carry | Why Daoris needs it |
|---|---|---|---|---|---|
| `git` | Git | `git` | `git --version` | `core.sshCommand` (§2.5) | trees, landings, reviews, bringing up to date; every session's git |
| `node` | Node.js | `node`, `npm`, `npx` | `node --version` | none | plugins' hooks and servers, the tree guard, the npm-installed agents, the CLI |
| `pwsh` | PowerShell | `pwsh` | `pwsh --version` | none | the terminal's default shell |
| `gh` | GitHub CLI | `gh` | `gh --version` | none | the GitHub landing plugin |
| `az` | Azure CLI | `az` | `az version` | none | the Azure DevOps landing plugin |

Windows PowerShell and Command Prompt are the system's and stay so. A sixth tool is a reviewed row in both
twins, like a harness (D57 §5).

### 2.2 The three ways, and the file

Each tool is run one of three ways. In the file:
- **`system`**: the one `PATH` finds, exactly as today. Absent means this.
- **`managed`**: one exact version, downloaded into the Daoris home and verified (§3.6). The file names the
  version, and nothing under the user profile is written (D63).
- **`file`**: an executable the person names, by its absolute path.

They live in `$DAORIS_HOME/tools.json`, one file for both doors (D50):

```json
{
  "tools": {
    "git":  { "use": "managed", "version": "<version>" },
    "node": { "use": "file", "file": "<an absolute path>/node.exe" },
    "gh":   { "use": "system" }
  },
  "git": { "core.sshCommand": "C:/Windows/System32/OpenSSH/ssh.exe" },
  "locations": ["https://example.org/daoris/resources.json"]
}
```

**The file's rules**, each a row in both twins' tables (§5):
1. No file, no entry, or `"use": "system"` is the system's.
2. `managed` needs an exact `version` of one to four numbers. `file` needs an absolute `file`. An entry with
   both, or with neither, is refused whole: its tool says the file's problem on both doors and is never run
   another way.
3. A tool id this build does not declare is kept as written and never applied, since a newer build may know it
   (twins: *an editor preserves what it has no field for*).
4. `git` holds only keys on the allow-list in §2.5. A key off the list is refused on a write, and on a read it is
   kept, not applied, and said.
5. `locations` holds only addresses `https://`, or `http://` to a loopback host. Anything else is refused on a
   write, and on a read it is skipped and said.
6. **Setting one way clears the others.** A tool is never both managed and a file, as a toolchain declares a
   channel or a package and never both (D57 §3a).

### 2.3 The rule: the way set decides, and a way that cannot run refuses

**The way set decides every question about the tool**: which file starts, whether the tool is there, which
version it is, and what a child's `PATH` holds. D57 learned this the hard way, when its rule was applied at the
spawn and not at the presence check (FIX-LOG, 2026-09-22).

- **`file`**: that file. A file that is gone refuses, naming the file. It never falls back to `PATH`.
- **`managed`**: `<home>/tools/<tool>/<version>/`, whose record names the executable inside (§3.6). A version
  nobody downloaded refuses, naming `daoris tool use <tool> managed <version>` (which downloads it) and
  `daoris tool use <tool> system`. It never falls back to `PATH`: **a pin nobody installed refuses** (D57).
- **`system`**: `PATH`, by PATHEXT on Windows. There is one resolver for a bare name in each artefact,
  `CommandPresence` and `onPath`, with `startable`. A tool not found is a sentence naming the three ways; the
  callers keep their own refusals as today.

This is D57's order read for a tool: a file you name is the explicit command, managed is the pin, and `PATH` is
the default. It differs in one way, on purpose. D57 layers the three, so an explicit command outranks a pin
that is also set. **A tool holds exactly one way**, because the screen offers them as one choice of three, and
a pin hidden under a file would be a setting nobody can see.

**Absent is today's behaviour, byte for byte.** A machine that never opens Settings → Tools runs every tool from
`PATH`, and every child's environment is the one it inherits today (D48 §2a). Nothing switches to managed on its
own. Switching would change the git under a running arrangement, and D57 rejected that for agents for the same
reason.

### 2.4 One answer for Daoris and every child

The driver's fetch and a session's `git push` must be the same git carrying the same settings. The finding in
§1.2 is what two answers look like. So there is **one resolution, taken from the file, and one environment
built from it**, in each artefact:

- **Daoris's own starts use the resolved file by its absolute path**, never a bare name:
  - `WorkingTree.GitAsync` (it keeps `-c core.longpaths=true`, Daoris's own need, on the command line);
  - a hook's command whose first word a tool answers for (`node`, `npm`, `npx`, `gh`, `az`, `git`, `pwsh`), a
    `.cmd` among them started through the shim rule D57's doors already use;
  - `npm` in a package pin or update;
  - the tree guard, written as the resolved node's path in the exec form it already uses.

  A plugin's command whose first word is not a tool's is resolved by `CommandPresence`, as an agent's is.
- **Every child gets the tools' environment**:
  - its `PATH` is each tool that is `managed` or `file`, in the declared order (git, node, pwsh, gh, az), its
    folders first, then the `PATH` it inherited;
  - `GIT_CONFIG_GLOBAL` names Daoris's git file (§2.5) when any git setting is set.

  Nothing else changes. With every tool the system's and no git setting set, the environment is the inherited
  one exactly.
- **"Every child" is every process the driver and the modules start**: a driven session on either door, a chat,
  an intake, Ask Daoris's helper, a probe, an agent action, a hook, a landing plugin, a plugin's trial, and each
  of the terminal's shells.
- **An MCP server a plugin declares** (such as `npx …`) is handed to the agent as written, and the agent starts it.
  The agent finds its first word on the `PATH` it was given, which is the tools' `PATH`. Whether each agent passes
  that `PATH` on to the servers it starts is the agent's own behaviour, **not measured here**. The knowledge
  connector is Daoris's own, handed by its absolute path (`KnowledgeConnector.Locate`), and is unchanged.
- **Resolved from the file at each start**, as the driver reads `driver.json` each tick. A running child keeps
  the tools it started with. Nothing mutates the application's own process environment: the modules' tests are
  serialized because the modules read process-global variables, and a variable rewritten while sessions start is
  the same trap at run time.
- **Held by a source-reading test** in the driver and in the modules. Every `new ProcessStartInfo` there is
  handed the tools' environment, or names why not (a Daoris program, the system's file manager), and so is the
  terminal's `PseudoConsole.Start`, which builds its environment block for `CreateProcessW` itself. This is the
  shape of `NoConsoleWindowTests`, which already holds every start to `CreateNoWindow`. The CLI's is the
  dogfood test's: `toolchain.ts` stays the one module that spawns, and it builds every child's environment
  through `tools.ts`.

### 2.5 What Daoris's git carries, without touching the person's git configuration

**The allow-list is one key: `core.sshCommand`.** WSR7 measured the failure it answers, and WSR7's own words
name it. That it cures the owner's fetch is TOOLS11's to show. `credential.helper`, for HTTPS that answers
without asking, was named in the same words for a failure nobody has met yet. It joins by a row with its reason once
a real fetch needs it. A key on the list applies whichever way git is run: the system's git needs it as much as a
managed one.

**How it is carried: `GIT_CONFIG_GLOBAL`** (git 2.32 and later). It names a file Daoris writes at
`<home>/tools/git/global.gitconfig`:

```ini
# Written by Daoris (D121). Your own global git configuration is included first, unchanged;
# the lines between the two markers are what Daoris's git carries, rewritten from tools.json.
[include]
	path = <your XDG git config, if git would read one>
	path = <your ~/.gitconfig>
# daoris: begin
[core]
	sshCommand = C:/Windows/System32/OpenSSH/ssh.exe
# daoris: end
```

- **The person's global configuration is read and never written.** It is included by its absolute path, in the
  order git itself reads the two global files. If the person's own environment names a `GIT_CONFIG_GLOBAL`, that
  file is included instead. An include is written only for a file that exists when Daoris writes, and git is
  expected to read an include whose file has since gone as nothing. TOOLS6's table writes both cases against a
  real git rather than trusting that.
- **Daoris's keys come after the include**, so the person's choice in Settings wins over their own global file.
- **A repository's own configuration still wins over both.** This is global scope, and a repository that sets its
  own `core.sshCommand` for a deploy key keeps it.
- **A child's `git config --global` writes this file, not the person's.** Daoris rewrites only the lines between
  its markers and keeps everything else, and Settings lists the lines it did not write.
- **Git's version is asked**, of the resolved git, once per change of way:
  - below 2.32, a git setting refuses, naming the version, because that git ignores the variable and the setting
    would silently not apply;
  - below 2.29, *Bring up to date* says it cannot fetch without spending the person's last fetch time (D109 as
    amended by WSR7).

**The `ssh` choices**, the same on both doors:
- **Git's own**: no key set;
- **Windows**: `%SystemRoot%\System32\OpenSSH\ssh.exe`, offered where it exists and written as its path;
- **a command the person names**.

Which of these reaches the owner's remotes is TOOLS11's measurement. The design does not assume it is Windows'.

### 2.6 The terminal's shells

`TerminalShells.Available` already takes the `PATH` it searches. It is handed the tools' `PATH`, so:
- a managed or named `pwsh` is the terminal's first shell;
- Windows PowerShell and Command Prompt are the system's, as before;
- Git Bash is the `bash.exe` beside the git Tools resolves.

A managed git from the minimal distribution carries no bash (§3.2). Where the resolved git has no bash beside it,
the terminal offers the bash beside the system's git, and none if the machine has no Git for Windows. Every shell
starts with the tools' environment, so the person's own `git fetch` in the terminal behaves as Daoris's does.

### 2.7 Node, npm and the agents

- **A managed node brings its own `npm` and `npx`**, so the names on a child's `PATH` resolve together.
- **A package pin** (`npm install --prefix <home>/toolchain/…`) runs the resolved `npm`, since its prefix is
  named. The agents installed that way then run on the node the child's `PATH` finds, which is Tools' node.
- **`agent install` keeps the system's npm.** Its meaning is *the agent's own installer, into the machine*
  (D57), and that is the machine's npm's job. Where a managed npm puts a global install is its own
  configuration's answer, **not measured here**. If it is the node version's folder, the next node version loses
  the agent. If it is anywhere else, it is the machine's, and the system's npm should make it. With no system
  npm, `agent install` refuses and names `agent pin`.
- **The CLI outside Daoris runs on the system's node.** Inside Daoris's terminal and sessions, it runs on the
  node Tools resolves, because it starts from a child's `PATH`.

### 2.8 gh and az: their sign-ins stay theirs

A managed gh or az keeps its configuration where the tool itself keeps it, and a sign-in stays the tool's own
flow (D49 §4). Daoris sets neither `GH_CONFIG_DIR` nor `AZURE_CONFIG_DIR`. Switching to managed should not sign the
person out, because the tool reads the same files. That is **not measured**, and TOOLS11 measures it. Accounts for
these tools, as D67 gives agents, were not asked for and are not designed. The Azure DevOps plugin needs az's
`azure-devops` extension, which az installs into its own extension folder. With a managed az that is the tool's
act, never Daoris's.

## 3. The resource manifest

### 3.1 Built in, and read beside more locations

**`resources.json` is built into the install.** Its source is
`src/Daoris.Desktop/Daoris.Desktop.Driver/resources.json`. It is copied beside the application and the headless
host, so an install carries it at `app/resources.json`, where `tools/desktop-publish.mjs` already lays out the
install's own offers (D103).
- The driver reads it from its own folder.
- The CLI reads it beside the home, as it reads the offers (`OFFERS_DIR`).
- A CLI with no install beside its home has no built-in list. It says so, and a location still serves it.

**It is never rewritten in place.** The install folder is the publish's (its marker file refuses a stranger), and
a republish replaces it whole. A newer list arrives as a **resource location**: the address of another
`resources.json`, listed in Settings and in `tools.json`, fetched only when the person asks (§3.7), and kept under
`<home>/tools/locations/`.

**No location is listed on a fresh home.** Daoris publishes nothing yet, so there is no location of Daoris's own
to name. Once a release publishes one, that release adds its address as the default, in one reviewed line. Until
then, every location is one the person added: a newer list, a team's own, or a mirror inside a network that cannot
reach the makers' hosts.

### 3.2 The shape, schema 1

```json
{
  "schema": 1,
  "tools": {
    "git": {
      "source": "https://github.com/git-for-windows/git/releases",
      "licence": { "id": "GPL-2.0-only", "url": "<the maker's licence page>" },
      "versions": {
        "<version>": {
          "files": {
            "win-x64": {
              "url": "<the maker's own download address>",
              "sha256": "<64 hex, from the maker's own published sum>",
              "size": 0,
              "archive": "zip",
              "exe": "cmd/git.exe",
              "paths": ["cmd"]
            }
          }
        }
      }
    }
  }
}
```

**What each field means.**
- **`schema`** is read first. A number this build does not know refuses the whole list, and says it needs a newer
  Daoris.
- **A version** is one to four numbers (`2.51.0`, `2.51.0.2`). The maker's own tag, where it differs, belongs in
  the URL.
- **A platform** is a .NET runtime identifier (`win-x64`, `win-arm64`, `linux-x64`, `osx-arm64`). The CLI maps
  `process.platform` and `process.arch` onto it, in a table both twins hold.
- **`url`** is the maker's own download, `https://`. A mirror may use `http://` to a loopback host.
- **`sha256` and `size`** are required. They are the file's whole check (§3.5).
- **`archive`** is `zip` or `tar.gz`. Nothing else is unpacked, and a third kind is a reviewed change to both
  readers.
- **`exe`** is the executable inside the unpacked archive. It is a relative path in `/` form, with no `..`.
- **`paths`** are the folders put first on a child's `PATH`. The default is the folder `exe` is in.
- **The archive is unpacked whole**, top folder included, as Codex's is (D57 §3a), so `exe` names the path as
  published and there is no strip rule to get wrong.
- **`licence`** is shown before a download. **Daoris redistributes nothing**: each machine fetches from the maker's
  host because a person on it chose a version, as D57 §3a says of the agents.

**What the first list holds is TOOLS3's to confirm**, each entry read from its maker's own page and published sum,
and recorded in an evidence document as AGT2b's channels were:
- MinGit, Git for Windows' minimal distribution for applications: a zip that carries **no bash**. A sibling project
  in the family downloads MinGit as a zip.
- Node.js, whose own site publishes a zip per version with `SHASUMS256.txt`.
- PowerShell and GitHub CLI, each believed to publish a zip per release with checksums.
- Azure CLI, believed to publish a Windows zip.

A tool whose maker publishes no archive Daoris can unpack stays the system's, and the list says so rather than
naming an installer.

### 3.3 Resource locations

The list in Settings and in `tools.json` is ordered. The **built-in list is always read, and always last**. Each
location has:
- its address;
- when it was last fetched, and the sha256 of the copy kept;
- what it names (tools, versions, platforms);
- what it changed since the copy before: the versions it added or dropped;
- its integrity (§3.5).

**A location that cannot be fetched** keeps its last copy, said with its age. One never fetched contributes
nothing, and says so.

**Removing a location** removes its versions from what is offered. A version already downloaded stays, since
its folder and record are the proof (§3.6), and keeps running. Its row says no location names it any more.

### 3.4 The merge rule

The lists are read in order: the person's locations top to bottom, then the built-in one.

1. **A tool is offered only if this build declares it** (§2.1). A tool a list names that the build does not know is
   listed as *not a tool this build runs*, and nothing of it is offered.
2. **A version of a tool, for one platform, is one download, whoever names it.** Every list that names the same
   tool, version and platform must agree on `sha256`, `size`, `archive`, `exe` and `paths`.
3. **Two lists that disagree refuse that version, and only that version.** The refusal names both locations and
   both hashes, and nothing is fetched until one of them changes. This is D57's Codex rule (*two published hashes
   that disagree refuse before anything large is fetched*) and D64's (*a conflict is refused before anything
   loads, naming both sides*).
4. **Lists that agree with different addresses are mirrors.** The download tries each address in read order,
   the person's first, and the hash is the check whichever address served it. A location the person added
   because the maker's host is unreachable is tried first. That is the point of it.
5. **A tool's versions are the union** of every list's, for this platform.
6. **The newest is the highest version by number.** No list's word makes a version newest, so a location cannot
   declare an older version *latest*. `update` never moves a managed tool back (D57 §3c).
7. **A tool's `source` and `licence` are the first list's that names them**, in read order, and every list naming
   the tool is shown beside it.

### 3.5 Integrity, stated for each list

| Where a list came from | What vouches for it | Shown as |
|---|---|---|
| Built in | The install's own file, replaced whole by a republish; its sha256 is shown | *built in* |
| A location over HTTPS | TLS to that host, which the person chose to trust | the host, *fetched* and when, the copy's sha256, and what changed |
| A location on loopback | This machine | *this machine* |

**Every file, from any list, is checked against the hash and size its list gives**, before anything is unpacked.
So a list vouches for which bytes are right, and the host that serves them does not need to be trusted.

**A signed list is later, not never.** Both twins already carry an OpenPGP verifier with a pinned key (D57 §3a).
Once Daoris publishes a list and has a release key, a location may carry a detached signature, verified as Claude
Code's manifest is. There is no key to pin today, and a signature under a key nobody holds is decoration.

### 3.6 A download

- **Addresses are `https://`**, or `http://` to a loopback host, as D57's channels and the sibling's override
  both hold. Redirects are followed and held to the same rule.
- **Staged, then moved, as D57 §3a stages a channel install.** The file lands at `<home>/tools/<tool>/<version>.part/`,
  where its hash and size are checked. It is unpacked there into `package/`, the executable is found at `exe`,
  and a record `tool.json` is written with the version, platform, sha256, the address that served it, the lists
  that named it, `exe`, `paths` and when. Then the whole folder is renamed to `<version>/`. A refusal removes the
  staging. **Finding `tool.json` is the proof**, so a managed version that resolves verified, and using a version
  already downloaded fetches nothing.
- **Unpacking refuses by name** what D57's tar reader refuses: an absolute name, a `..` name, a Windows stream
  name, a link of either kind, a header or entry that fails its checksum, and an archive that stops before its end.
  A zip also refuses an encrypted entry and a compression method other than stored or deflate. The CLI's zip reader
  is written by hand on `node:zlib`, as `tarball.ts` is, since the package has no dependencies. The driver's is .NET's
  own, with the same refusals held by the same table.
- **Nothing is patched.** The bytes that verified are the bytes that run, as D57 §3a says of the agents, and
  setting the executable bit off Windows is the only change.
- **A download is an action the page starts and follows**, as an agent's install is (`HARNESS_ACTION`'s start, and
  its end said after). It is never one call the bridge waits on, because the bridge gives up after 30 seconds
  (WSR7 a). The person's *stop* cancels it.
- **Deleting a version** removes its folder. A version in use refuses; a version a running child holds refuses with
  the system's reason. Older versions stay until deleted, each shown with its size.
- **The machine log** gets a line an event (D94): a tool's way changed, a download started, verified or refused
  (by code), and a location fetched or failed. Each line names a tool and a version, never an address the person
  typed.

### 3.7 Look for updates

**Fetching the lists is the person's press, never a timer, and never opening the screen.** *Look for updates* on
the screen and `daoris tool look` on the terminal fetch each location, in order, each bounded. Looking reaches the
network as the person, which is D109's reason for waiting on their press. The result says, for each tool:
- the version it runs;
- the newest a list names for this platform;
- which list named it.

Nothing downloads until *Download* or *Use* is pressed. A managed tool's `update` moves to the newest version the
lists named at the last look. It resolves that to one exact version first, then downloads and uses it as a typed
one would (D57 §3c). A tool run from `PATH` or from a named file is not Daoris's to update, and `update` says so:
*its updates are the machine's* (D57).

### 3.8 Agents stay on their makers' channels

The manifest is for tools. **An agent is never read from a resource list**, and D57 §3a's channels are unchanged.
- **Trust.** Claude Code's chain is a signature under a pinned release key, checked for the version it names. A
  list is a hash somebody wrote down. Moving the agent would trade the stronger check for the weaker one, and D57
  §3a already refused a fallback that does that.
- **Cadence.** Agents release weekly or faster, and their makers keep a newest-release pointer (`latest`,
  `channels/latest`). A list Daoris keeps would lag them or need editing every week.
- **The terms.** D57 §3a's reading of the vendor's terms rests on fetching from the vendor's own channel.

**What the two share, deliberately:**
- the layout discipline: a `.part` stage, a whole-folder move, and finding the file as the proof;
- HTTPS only;
- nothing redistributed and nothing patched;
- in the CLI, the fetcher the dispatcher hands in, so the network stays in `service.ts`;
- *update* resolving the newest to one exact version before anything moves.

**The one crossing is Node** (§2.7). A mirror of an agent's channel, for a network that cannot reach the vendor,
would be the same signed manifest on another host. That is a channel's design, and D57's to extend.

### 3.9 The alternative weighed: a separately released resource package

A sibling project in the family ships its resources as a separately released package. It was read, not
changed. As it stands:
- **What the package is.** A content-only package on a public package registry bundles three things: a browser
  driver with its own node, a minimal git, and a headless browser.
- **Every other tool is in the code.** Each one's version, address and hash is a constant in the application's
  source, with a separate release workflow for each half.
- **A new package needs a new application anyway.** The application names the package's version as a constant,
  and the sibling's own publish script warns that an upload is not used until that constant changes and the
  application ships.
- **The package's bytes are not hashed.** It is trusted as TLS to the registry and the registry's immutable
  versions.
- **Installed means marker files exist**, so an install never notices a newer package, and nothing downloads one.
- **Sizes were bent to fit.** The registry's size limit trimmed what the package could carry, and the size
  figures recorded for it disagree with one another.
- **Versions drifted.** The package's version was once written in three uncoordinated places, then made one
  constant, which has not changed since.

**Chosen: the list built in, and more locations.** The reasons, against that shape:
1. **No release is needed for a newer version.** A newer list is a small file at an address, and adding the
   address is a setting.
2. **Daoris redistributes nothing** (D57's rejection of vendoring). The package bundles other makers' binaries
   under a combined licence. The list points at the makers' own downloads.
3. **Every file is hashed.** The package is not.
4. **The version is known.** It is the folder name and the record, where the package knows only markers.
5. **Each tool moves alone.** Updating one part of a bundle re-releases all of it.
6. **No size limit.**

**The costs, stated.** The list's hashes must be kept current, which TOOLS3 does from the makers' published sums.
A location is trusted as its host until lists are signed (§3.5). A maker's address can move, and a location fixes
that without a release.

**What was taken from it:**
- an override address must be HTTPS, or HTTP to loopback;
- a download is staged, then moved;
- resolution is asked again at each start;
- an agent's need for Git Bash is met from the machine before anything is downloaded for it. The sibling sets the
  agent's Git Bash variable only when no Git Bash is otherwise found, which is why TOOLS10 measures that need
  first.

**Taken as a warning:** a version kept in two places drifts, so here the file and the record are the one place.

## 4. The doors

### 4.1 Settings → Tools

A machine domain (`machine: true`) after *Agents* in `settings/domains.ts`, so a browser is never offered it
(D47 §4). It is built on the frame's list and main area once FRAME1g moves Settings there.

- **A row for each tool**, named by its product. It shows:
  - what Daoris needs the tool for, in one sentence;
  - how it is run: *System*, *Managed* or *Custom*, one choice;
  - the file it runs, or why none;
  - the version.
- **Managed** adds a version choice: the versions downloaded, then the versions the lists name for this platform,
  each with its size and licence. Its presses:
  - *Download* fetches the chosen version without using it;
  - *Use this version* downloads it if absent, then switches to it;
  - *Delete* removes a downloaded version not in use, with *Confirm delete* as its second press.
- **Custom** adds a file field, and *Browse…* opens the system's picker. A named file is checked: it exists and
  starts with a version.
- **Git's row adds *SSH command***: *Git's own*, *Windows* where it exists, or *Custom*. The global file's own
  path is shown, with any lines in it that Daoris did not write.
- **A section, *Resource locations*.** The built-in list first, which cannot be removed, then each location with
  what §3.3 shows. It offers *Add location…* and *Remove*, and one *Look for updates* for all of them.
- **What a switch changes is said before it applies.** Moving git from the system's to a managed one names the
  keys that make the two read a checkout differently (`core.autocrlf`, `core.eol`, `core.symlinks`,
  `core.longpaths`), from each git's own `config --system --list`. A managed minimal git ships its maker's system
  configuration, not the one a Git for Windows installer wrote. Line endings that differ between the person's git
  and Daoris's, in one checkout, would show every file changed.
- **Every refusal is a code** in the modules' `Refusals`, with an entry in both catalogues and a throw site, as the
  handover requires of a module refusal. The codes: `TOOL_NOT_DOWNLOADED`, `TOOL_FILE_MISSING`,
  `TOOL_VERSION_UNKNOWN`, `TOOL_DOWNLOAD_REFUSED` (with the check that failed), `TOOL_LOCATION_REFUSED`,
  `TOOL_IN_USE`, `TOOL_BUSY` and `TOOL_GIT_TOO_OLD`.
- **The routes are `DAORIS.DRIVER` doors** in `DriverModule.Tools.cs`, called by the page's `bridge/tools.ts`:
  `TOOLS_LIST`, `TOOLS_USE`, `TOOLS_DOWNLOAD`, `TOOLS_DELETE`, `TOOLS_LOOK`, `TOOLS_LOCATION` (add and remove) and
  `TOOLS_GIT`. Each is named in the Desktop README's row (MOD5). `TOOLS_LOOK` and a download's follow wait by
  `hostBounds`, the twin of the driver's bounds (WSR7).

### 4.2 The terminal: `daoris tool`

A management verb in the CLI, with the shape of `agent` and `browser`: one file under the home, and either door
(D50).

```
  tool [verb]          the programs Daoris runs beside its agents — Git, Node.js,
                       PowerShell, GitHub CLI, Azure CLI — each the system's, managed,
                       or a file you name ($DAORIS_HOME/tools.json):
                         list                      how each is run, the file, its version
                         path <tool>               the file Daoris runs for it, or why none
                         use <tool> system         the one on PATH, as before
                         use <tool> managed [<version>]
                                                   a version kept in the home: downloaded
                                                   if absent, and verified; no version is
                                                   the newest the lists named at the last look
                         use <tool> file <path>    an executable you name
                         download <tool> [<version>]
                                                   fetch and verify it; nothing switches
                         update <tool>             managed: the newest the lists named
                         delete <tool> <version>   a downloaded version nothing uses
                         git ssh default|windows|<command>
                                                   the SSH command Daoris's git carries; your
                                                   own git configuration is never written
                         locations [add|remove <address>]
                                                   the lists versions come from: yours in
                                                   order, then the one built in
                         look                      fetch every location; say what is newer
```

- **The network stays in `service.ts`.** `download`, `use … managed`, `update` and `look` are handed the
  dispatcher's fetcher, as `agent pin` is. The usage footer names them beside `agent pin` and `agent update` as
  the verbs that open a connection themselves. The dogfood test's *reaches a release channel only through the
  fetcher the dispatcher hands in* gains `tools.ts` and the zip reader.
- **Spawning stays in `toolchain.ts`.** A tool's version is asked there, and `tools.ts` stays pure: the file, the
  lists, the merge, and the environment.
- **Exit codes are the contract:** 0 done; 1 refused by the file's rules or a check (a hash, a conflict, a
  version in use); 2 a tool error.
- **The headless host** (`daoris-driver`) answers the driver's own questions through the same library. It gains
  no verb of its own: the CLI is the terminal's door, as it is for agents.

### 4.3 Ask Daoris

Every control above is a door, a door owed, or exempt with its reason (D110), held by `HelpCoverageTests`.

| Control | Answer | Why |
|---|---|---|
| *System* | a door: the eleventh kind, `tool_propose {action, tool, …}`, action `use` | narrows what runs to the machine's own |
| *Managed* with *Use this version* | a door, action `use` with a version | the card names the version, its size, the host it downloads from and the licence, and says the download is made as the person, as an agent's pin card does (HELP6). It is judged against the lists as last fetched, and never fetches to judge |
| `update` | a door, action `update` | judged against the last look, whose time the card says. With no look yet it is refused, naming *Look for updates* |
| *SSH command*: *Git's own* or *Windows* | a door, action `ssh` | a value the screen offers, judged as the route judges it (Windows' ssh exists here) |
| *Remove* a location | a door, action `location` | it only narrows what is offered. The card lists the versions it stops offering, and says the downloaded ones stay |
| A place: Settings → Tools, a tool's row | the `go` kind's places, a twin of `help/places.ts` | as every domain |
| *Custom* file, and a custom SSH command | exempt: the person's own press | a program the person names runs as them on every call. A helper that can invent a path must not be one press away from running it (D89's keys and sign-ins, by the same principle) |
| *Add location…* | exempt: the person's own press | a location is a source of programs, with hashes of its own choosing. Naming one is the person's trust |
| *Download* | exempt: nothing changes | it changes nothing about what Daoris runs, and the use that would is a door |
| *Look for updates* | exempt: nothing changes | it reads lists. The room says when they were last read, and the `update` door works from that |
| *Delete* a version | exempt: a discard (D89) | |

**The room** says how each tool is run, its file and version, the versions the lists named at the last look and
when, the locations, and the SSH command. It names every `daoris tool` command beside its door.

### 4.4 Names (D116)

**Three new terms for the glossary**, each chosen as a name in each language:

| Term | English | 中文 | Means | `match` |
|---|---|---|---|---|
| `tool` | tool | 工具 | a program Daoris runs beside its agents: Git, Node.js, PowerShell, GitHub CLI, Azure CLI | `\btools?\b(?!\s+calls?)`, so *tool call* (工具调用) stays its own term |
| `managed` | managed | 托管 | a tool's version Daoris downloads into its home, verifies and runs; for an agent the word is *pin* | `\bmanaged\b` |
| `resource location` | resource location | 资源位置 | the address of a list of tools' versions and where each downloads, read before the one built in | `\bresource locations?\b` |

- **Why 工具 is free.** The `agent` term already forbids 工具, but the check applies that only to a label whose
  English the term's `match` finds, so a label naming a tool is not held to it. NAME1b renames the one door that called agents *tools*, *Tools &
  accounts*, to *Agent settings*. TOOLS7 comes after NAME1b, so *Tools* names one thing.
- **Why *managed* and not *pin*.** A pin is one press on an agent's row, from the agent's own channel. *Managed*
  is one of three ways offered side by side, and it is the owner's own word (*self managed*). The owner may
  overrule this on reading.

**The names, with their kinds and budgets** (D116 §4: English characters, Chinese units):

| Key (proposed) | Kind | English | 中文 | Within budget |
|---|---|---|---|---|
| `settings.domain.tools` | nav | Tools | 工具 | 5 / 2 of 16 / 5 |
| `settings.tools.use.system` · `.managed` · `.file` | choice | System · Managed · Custom | 系统 · 托管 · 自定义 | of 16 / 6 |
| `settings.tools.program` · `.version` | field | Program · Version | 程序 · 版本 | of 36 / 14 |
| `settings.tools.download` | button | Download | 下载 | of 20 / 8 |
| `settings.tools.useVersion` | button | Use this version | 使用这个版本 | 16 / 6 of 20 / 8 |
| `settings.tools.delete` · `.deleteConfirm` | button | Delete · Confirm delete | 删除 · 确认删除 | of 20 / 8 |
| `settings.tools.look` | button | Look for updates | 检查更新 | the house's press for a look (NAME1a) |
| `settings.tools.locations.title` | section | Resource locations | 资源位置 | 18 / 4 of 32 / 12 |
| `settings.tools.locations.add` · `.remove` | button | Add location… · Remove | 添加位置… · 移除 | of 20 / 8; *remove* keeps what was downloaded, as the glossary's word says |
| `settings.tools.git.ssh` | field | SSH command | SSH 命令 | of 36 / 14 |
| `settings.tools.git.ssh.own` · `.windows` · `.custom` | choice | Git's own · Windows · Custom | Git 自带 · Windows · 自定义 | of 16 / 6. *Windows OpenSSH* would be 7½ units in Chinese, and the bundled ssh is OpenSSH too |
| `settings.tools.status.*` | status | in use · downloaded · not downloaded · downloading · not found · built in | 使用中 · 已下载 · 未下载 · 下载中 · 未找到 · 内置 | of 16 / 5 |

- The product names (Git, Node.js, PowerShell, GitHub CLI, Azure CLI, Windows) stay Latin in Chinese (D116 §3b).
- The existing terms keep their words for their acts: *fetch* 获取, *version* 版本 and *update* 更新.
- *Install* is extended to a tool's downloaded version only if the owner prefers it to *Download*. The design uses
  *Download*, the brief's word, because nothing here runs an installer.

## 5. Twins, and where each thing lives

| The file or rule | The CLI | The driver | The service |
|---|---|---|---|
| `tools.json`: the three ways, absence, the rules of §2.2, the allow-list | `tools.ts`; `tools.test.ts`'s table | `Tools.cs`; `ToolsTests` holds the same rows | — |
| The declared tools (§2.1): ids, names, the names each answers for, the version question | `tools.ts` | `Tools.cs` | — |
| The resolution and the tools' environment (§2.3, §2.4), with the git file's text (§2.5) | `tools.ts` (pure); `toolchain.ts` spawns | `Tools.cs`, applied at every start | — |
| `resources.json`: schema 1, the platform table, the merge (§3.4) | `resources.ts` | `ToolResources.cs`; `ToolResourcesTests` | — |
| An archive's refusals (§3.6) | `zipfile.ts` beside `tarball.ts` | `ToolInstall.cs` on .NET's zip reader | — |
| The layout: `tools/<tool>/<version>/tool.json` | `tools.ts` | `ToolInstall.cs` | — |
| Where the built-in list is: `app/resources.json` | `resources.ts`, beside the home; `tools/desktop-publish.mjs` lays it out; `desktop-publish.test.ts` reads both spellings | `ToolResources.Layout`, beside the application | — |
| Ask Daoris's `tool` kind (§4.3) | — | `Help/Proposals/HelpToolProposals.cs` | `HelpProposalBox.Tool.cs`, `KnowledgeTools.Help.Tool.cs` |

`.claude/knowledge/twins.md` gains these rows in the change that builds each.

## 6. What gates can prove, and what only a real download can

**No gate touches the network**, as today. Everything below uses built archives, a stand-in fetcher, or a server on
loopback that the gate starts itself.

**Unit tables, in both twins, in the fast half:**
- the file's rules;
- the resolution in each way, and its refusals;
- the environment, byte for byte with every tool the system's;
- the git file's text: includes, order, markers, and a child's own lines kept;
- the version floors;
- the merge: order, mirrors, a conflict naming both, the union, the newest, an unknown tool and an unknown schema;
- the address rule;
- the archive refusals, on archives built in the test;
- the staging and the record, and finding it as the proof;
- a delete in use.

The source-reading tests of §2.4 hold every start.

**The family rehearsal** (Linux and Windows, no model and no account) gains a phase:
1. **A download from a list.** The rehearsal starts a loopback list server, which serves a list naming a stub tool
   for the platform: a `gh` it builds as a zip and as a `tar.gz`, that writes its own path to a marker when run.
   Then `daoris tool locations add`, `look`, and `use gh managed <v>`. It checks that the download is verified and
   laid out, and that `tool path gh` names it.
2. **A plugin's child runs the managed tool.** A landing with the example GitHub plugin runs that stub: the marker
   names `<home>/tools/gh/…`. So a plugin's child ran the managed tool.
3. **Refusals.** A second location naming the same version with another hash: `list` refuses that version, naming
   both. A served file whose bytes differ: refused, and nothing under `tools/gh/`.
4. **One answer for the driver and a session.** `tool git ssh "<a stub ssh>"`, where the stub records that it was
   called and fails, over a repository whose origin is an `ssh://` address:
   - `daoris-driver trees sync --repository …` calls the stub from the driver's own fetch;
   - a stub agent's session running `git ls-remote origin` calls it from a session;
   - the scratch home's planted `.gitconfig` is byte-identical afterwards.

   This needs the machine's git at 2.32 or later, which the rehearsal checks first.

**The deployment rehearsal** (Windows, the artefact):
- the install carries `app/resources.json`, it reads as schema 1, and it names every declared tool for `win-x64`
  that TOOLS3 confirmed;
- on a fresh home, the deployed shell's `TOOLS_LIST` answers every tool as the system's;
- a stub tool installed from a loopback location into the scratch home is what the deployed shell resolves.

**Only a real download, or the owner's machine, can prove:**
- that the built-in list's addresses and hashes answer as recorded. TOOLS3's evidence records it. A
  `tools/resources-check.mjs` a person runs when editing the list can fetch each one, but it is never a gate;
- that a real MinGit, node, pwsh, gh and az unpack, start and answer their version;
- what a minimal git's own system configuration reads differently from Git for Windows'
  (`core.autocrlf` above all);
- that a managed git with Windows' ssh fetches the owner's SSH remotes (WSR7, for real);
- that gh and az stay signed in as the person when managed;
- how Claude Code finds its Git Bash with a minimal git first on `PATH` (§7, TOOLS10);
- arm64, and az's size.

## 7. The build

Rows ready for `TASKS.md`, with DEV2's lane ids. The order is TOOLS2 → TOOLS3 → TOOLS4, then TOOLS5 ∥ TOOLS6,
then TOOLS7 (after NAME1b) ∥ TOOLS8, then TOOLS9. TOOLS10 runs before any session runs a managed git, and TOOLS11
comes last. Nothing switches on its own at any step: absent stays the system's.

- [ ] **TOOLS2 — `tools.json` and its resolution, twins** (§2.1–§2.3, §5; lanes `cli`, `driver`): the declared
  tools, the three ways, the file's rules, the resolution and its refusals, in `tools.ts` and `Tools.cs`, each
  with a table the other matches. `daoris tool list|path|use <tool> system|file` (no network yet). **Proof:** the
  two tables line for line; `tool list` on a home with no file prints every tool as the system's; the usage
  fixture.
- [ ] **TOOLS3 — the resource list and its merge, twins** (§3.1–§3.5; lanes `driver`, `cli`, `tools`):
  - schema 1's reader, the platform table and the merge;
  - locations in `tools.json`, and fetched copies under `<home>/tools/locations/`;
  - the built-in `resources.json` in the driver, laid out at `app/resources.json` by `desktop-publish.mjs`, and
    read by the CLI beside the home;
  - its first entries, each read from its maker's own page and published sum, in
    `docs/<date>-tools-resources-evidence.md`, with the tools §3.2 believes have no archive said so.

  **Proof:** the merge table in both; `desktop-publish.test.ts` reads the three spellings; the evidence document.
- [ ] **TOOLS4 — download, verify, unpack, lay out** (§3.6, §3.7; lanes `cli`, `driver`):
  - `zipfile.ts` on `node:zlib`, and the driver's `ToolInstall`, with one refusals table;
  - `.part` staging and `tool.json`;
  - `daoris tool download|use … managed|update|delete|look|locations`, with the dispatcher's fetcher;
  - the driver's download as a followed action, cancelled by *stop*.

  **Proof:** archives built in the tests, a stand-in fetcher, and the dogfood test extended to `tools.ts`.
- [ ] **TOOLS5 — one answer for every child** (§2.4, §2.6, §2.7; lanes `driver`, `modules`, `cli`):
  - the resolved git in `WorkingTree.GitAsync`, a hook's first word, `npm` in a pin, and the tree guard's node;
  - the tools' environment on every start;
  - the terminal's shells from the tools' `PATH`, with Git Bash's fallback;
  - `agent install` kept on the system's npm.

  **Proof:** the source-reading tests; the environment table; a `Process`-half case where a child's `git` is the
  resolved file.
- [ ] **TOOLS6 — what Daoris's git carries** (§2.5; lanes `driver`, `cli`): the allow-list, `GIT_CONFIG_GLOBAL`
  and the file with its includes and markers, the version floors (2.32, and the fetch's 2.29), and `daoris tool
  git ssh`. **Proof:** the file's text table in both; a `Process`-half case with a stub ssh; a real git asked
  `config --show-origin core.sshCommand` in a scratch repository.
- [ ] **TOOLS7 — Settings → Tools** (§4.1, §4.4; lanes `modules`, `web-settings`, `web-shell`; after NAME1b):
  - the `DAORIS.DRIVER` routes in `DriverModule.Tools.cs`, and the page's `bridge/tools.ts`;
  - the domain, its refusals in both catalogues, and the glossary's three terms;
  - what a switch of git changes, said before it applies;
  - its controls answered in `HelpCoverageTests` as doors owed until TOOLS8.

  **Proof:** the vitest loop over a mocked bridge; the names check; looked at on the window in both themes and
  both languages.
- [ ] **TOOLS8 — Ask Daoris's `tool` kind** (§4.3; lanes `service`, `driver`, `web-shell`): the kind and its doors,
  the exemptions, the go places, the room, and the card. **Proof:** the kinds tables on both sides, and the coverage
  test green with no owed row left.
- [ ] **TOOLS9 — the rehearsals** (§6; lane `tools`): the family rehearsal's tools phase, and the deployment
  rehearsal's three checks. **Proof:** each check seen failing once by breaking what it guards, then passing.
- [ ] **TOOLS10 — the probe before a managed git meets an agent** (docs; the owner allows one start of each agent):
  - how Claude Code on Windows finds its Git Bash when a minimal git is first on `PATH`;
  - whether the minimal git carries an ssh of its own;
  - its system configuration against Git for Windows' (`core.autocrlf`, `core.eol`, `core.longpaths`,
    `credential.helper`).

  If the agent cannot find a bash, the agent's native adapter hands it the bash §2.6 would offer, and only then.
  **Proof:** an evidence document.
- [ ] **TOOLS11 — the first real downloads, on the install** (the owner's run, after a republish):
  - a managed git with *SSH command: Windows*, and *Bring up to date* on the owner's workspace fetching its SSH
    remotes;
  - a managed node running a plugin;
  - a managed pwsh as the terminal's shell;
  - gh and az signed in as the person, landing a branch.

  **Proof:** the look's rows fetched, and the machine log's lines.

## 8. Not chosen

- **A separately released resource package.** §3.9 has the full reasoning. A newer package still needs a new
  application, and it bundles and redistributes other makers' binaries without hashing them.
- **Rewriting the built-in list in place.** The install folder is the publish's, and a republish replaces it whole.
- **Daoris's own default location today.** Nothing is published, so the address would name nothing.
- **Agents under the list** (§3.8): a weaker check for the same bytes, and a list that lags the makers.
- **Layering the three ways, as D57 layers an agent's.** A pin hidden under a file is a setting nobody sees on a
  screen that offers one choice.
- **Managed by default once it exists.** The git would switch under a running arrangement, as D57 refused for agents.
- **A tool set a location can extend.** A list could then make Daoris run a program its code never named.
- **Setting the tools' `PATH` on the application's own process.** It is process-global state rewritten while
  sessions start. Each start builds its child's environment instead.
- **Carrying git's settings on each of Daoris's own calls (`-c`).** Daoris's fetch would work and a session's `git
  push` would not: two answers.
- **`GIT_CONFIG_COUNT` or `GIT_SSH_COMMAND`.** Both have command-line scope and override a repository's own
  configuration, a deploy key's `core.sshCommand` included.
- **Writing the person's `~/.gitconfig`, or each checkout's `.git/config`.** The first touches the person's git
  configuration. The second reaches into the person's checkout, and both outlive Daoris.
- **A managed git's own system configuration.** It would patch the maker's files (D57 §3a), and it would apply only
  when git is managed.
- **Git's full portable distribution, for its bash.** It ships as a self-extracting archive or as `tar.bz2`. The
  first runs a downloaded program to unpack it, and neither runtime reads the second. The sibling runs the
  self-extracting one on request. It is not built until TOOLS10 shows an agent needs a bash that a machine without
  Git for Windows lacks.
- **A location in a folder (`file:`).** An offline mirror would need file download addresses too: a second trust
  story, and one nobody has asked for yet.
- **Accounts for gh and az.** They were not asked for. Their sign-ins stay the tools' own.
- **Per-workspace tools.** One git per machine makes *the driver and a session get the same answer* hold by
  construction. A workspace layer can be added later, as D57's was.
- **Proposing a named file, a custom SSH command, or a new location from Ask Daoris.** A helper that can invent a
  path or an address must not be one press away from what runs as the person (§4.3).

## 9. What this document's gate does not cover

This change is documents only.
- **Read from the code:** the statements about today (§1), from the files §1 names, at `d618cbb`.
- **Read from outside:** the sibling's package, from its folder, which was not changed.
- **Not measured:**
  - what the first list will hold (§3.2);
  - which ssh reaches the owner's remotes (§2.5);
  - how git reads an include whose file is gone (§2.5, TOOLS6's table);
  - what a minimal git's configuration reads differently, and how an agent finds its bash (TOOLS10);
  - whether gh and az stay signed in (§2.8);
  - where a managed npm puts a global install (§2.7);
  - whether an agent passes its `PATH` on to the servers it starts (§2.4);
  - whether the tree guard blocks a write when node is absent (§1.1).
- **Not built:** every name in §4.4 is a proposal. The glossary, the catalogues and the names check are TOOLS7's.
- **`verify` checks** the records' shape, the budgets and the duplicates, and none of these words.

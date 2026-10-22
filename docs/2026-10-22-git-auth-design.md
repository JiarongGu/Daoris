# Git identities and git access: who Daoris's git acts as on a code host, and what it may do there (GITAUTH1)

**Status: proposed, round 2** (D198, 2026-10-22). Written by Claude Opus from the code at `75bb61805`; nothing here is built.
Round 2 is revised by Codex's read of round 1 under D191: thirty-eight findings, twenty-seven blocking, every one checked against
the code and taken or answered (§16). It changes a foundation (where credentials live, how every git Daoris starts is wired), so the steward
sends it to Fable before accepting it (D191's amendment).

Daoris's sessions reach git remotes (a fetch at a tree's start, a session's own `git fetch` or `git push`, a landing's push) and
fail, because nothing in Daoris sets up how its git signs in to a code host. This design gives Daoris its own **git
identities**, several per host, kept apart from the person's own sign-ins; hands each git Daoris starts exactly the identity its
remote needs; says once, in plain words, when none serves a remote, and stops trying; and, in the same place, sets **git
access**: what a repository, a workspace and a session may do on a remote (read, write, dangerous writes), judged by one judge
at the one place every git act must pass to reach a host, for Daoris's own landing and an agent's own push alike.

## 0. What is there today

Read from the code; each fact with its file and line.

### 0.1 Which git runs, and what it carries

- **The git on the path, or the one Tools names.** Git is a declared tool: the system's, managed (the built-in list's MinGit
  2.56.0) or a file the person names (`Tools.cs:99-106`; D121). Every child is handed the tools' `PATH` and nothing else:
  `Tools.ChildEnvironment` sets `PATH` alone (`Tools.cs:654-663`), and `GIT_CONFIG_GLOBAL`, the file D121 §2.5 designed to carry
  Daoris's git settings, is TOOLS6's and not built (`Tools.cs:595`; `TASKS.md:394`).
- **One git setting is allowed, and none is applied.** `tools.json`'s `git` section takes only `core.sshCommand`
  (`Tools.cs:108-109`); `credential.helper` is refused by name (`ToolsTests.cs:208-209, 228-235`). Since TOOLS6 is not built, the
  allowed key is kept and handed to no git.
- **D121 rejected the command scope.** `GIT_CONFIG_COUNT` and `GIT_SSH_COMMAND` were rejected because "their command-line scope
  overrides a repository's own configuration" (`D121.md:109`), so a repository's own deploy-key `core.sshCommand` keeps winning
  (`docs/2026-10-01-tools-design.md:248-249`). This design reverses that for authentication, and says why (§4.1, §13).
- **Git for Windows' launcher is started past where its layout is recognised** (GITSTART1-5): a `cmd\git.exe` with a git under
  `mingw64`, `clangarm64` or `mingw32`, `usr\bin`, and no `etc\git-bash.config` (`GitForWindows.cs:31, 47-68`). The real git then
  gets the launcher's variables, among them `HOME` where unset, from `HOMEDRIVE` and `HOMEPATH`, else `USERPROFILE`
  (`GitForWindows.cs:70-96`). The managed MinGit holds its git under `ucrt64`, which the rule does not name, so it still starts
  through its launcher (GITSTART6; `docs/2026-10-01-tools-resources-evidence.md:52`). MinGit carries an ssh of its own
  (`:48-50`); whether it carries Git Credential Manager is not recorded.

### 0.2 What a session's git sees

- **A session inherits the desktop's environment.** `Spawning.Shell` starts every session, chat, intake and helper with the
  desktop's own environment, less the role variables, plus `DAORIS_REPOSITORY`, `DAORIS_SERVICE_URL` and the tools' `PATH`
  (`Adapters.cs:1497-1536`).
- **An account's home is the agent's configuration folder, not `HOME`.** The account is handed through the harness's own
  variable, `CLAUDE_CONFIG_DIR` or `CODEX_HOME` (`HarnessSelection.cs:566-579`; D175 point 1). Nothing sets `HOME`,
  `USERPROFILE` or any git variable for a session.
- **So a session's git authenticates with whatever is ambient**: the system's, the person's global and the repository's own
  configuration (Git for Windows' installer sets `credential.helper=manager` in the system's by default), an address's own user
  and password, the person's `~/.ssh` (git's own ssh reads `$HOME/.ssh`; Windows' OpenSSH reads `%USERPROFILE%\.ssh` and the
  Windows agent), Git Credential Manager's entries under the person's namespace, and any authentication variable the desktop
  inherited. None of it is Daoris's.
- **Nothing tells a session's git not to prompt.** Daoris's own fetch sets `GIT_TERMINAL_PROMPT=0` (`TreeRefs.cs:12-16`), as do a
  plugin's tool runs and trials (`PluginToolRuns.cs:40`, `PluginTrialProcess.cs:38`). A session's start does not, and nor does a
  landing plugin's process (`HookProcess.cs:85-111`). Git's own askpass programs run even where terminal prompts are off (git's
  documented prompt order: `GIT_ASKPASS`, `core.askPass`, `SSH_ASKPASS`, then the terminal). Whether Git Credential Manager then
  opens a window on the person's desktop for a session's or a plugin's HTTPS push is not measured.

### 0.3 Where Daoris and its sessions reach a remote, and what a failure does

| Who | Where | What a failure does today |
|---|---|---|
| Daoris's fetch of the line | `TreeRefs.FetchAsync` (`TreeRefs.cs:81-103`), "as the person, with their credentials" (`TreeRefs.cs:13-15`); one at a time per line (`RemoteFetches.OnceAsync`, `PullRequestMoments.cs:28-34`) | — |
| *Bring up to date* | `TreeSync` (`TreeSync.cs:112-115`) | said once per look, grouped, with what the git needs (WSR7; `SyncWords.cs:29-59`) |
| A landing's press, its line fetched first (LANDSYNC1) | `TreeLandSyncs.cs:94-97`; `TreeUpdates.cs:95-99` | the explicit update press refuses a failed fetch (`TreeUpdates.cs:95-99`); a landing's says it (`LandingSyncWords.NotFetched`) |
| A start, resume or carry-on in a tree that stands (LANDSYNC2) | `TreeUpdates.StartInSyncAsync` fetches the line (`TreeUpdates.cs:146-168`) | never a reason not to start: the failed fetch's sentence goes into the session's instruction (`TreeUpdates.cs:163-168`; `Adapters.cs:402-404`), which says it was not fetched and not to leave it alone |
| A pull request's moments (FABRIC-END6) | `PullRequestMoments.AskDueAsync` fetches before asking the plugin (`PullRequestMoments.cs:463-467`) | **discarded**: the fetch's answer is never read; the moment fetches again at the next focus, ref move or push past the minute (`PullRequestMoments.cs:183-187`) |
| A landing's push | the plugin's own process: "Daoris pushes nothing and opens nothing" (`LandingPlugins.cs:8-10`; D37, D87); the GitHub plugin runs `git push --quiet origin <tip>:refs/heads/<branch>` (`land.mjs:79`); a branch rule with no plugin leaves the branch for the person to push (`Landing.cs:24-28`) | the plugin's answer, `pushed: false` with the last line git wrote to stderr (`land.mjs:62-67`), which the driver says (`LandingPlugins.cs:113-114`) |
| A session's own `git fetch`, `pull`, `clone`, `ls-remote` | the agent's shell, any form | the agent reads git's words and decides; nothing in its instruction speaks of fetching but LANDSYNC2's sentence above (`Adapters.cs:985-1002`) |
| A session's own `git push` | the agent's shell | refused on both Claude doors by the shipped `no-push` default, four forms (`Permissions.cs:152-167`), and told to auto mode's classifier as a hard denial (`Permissions.cs:166-167, 190-195`); Codex takes no rules (D184's AUTONOMY1b note) |

### 0.4 The loop the owner saw

- **Nothing remembers that a remote cannot be reached.** Each path above tries on its own schedule and forgets: the moments fetch
  again past each minute and say nothing (`PullRequestMoments.cs:463-467`); every press, every look and every start in a standing
  tree fetches again and says it again; and an agent told the fetch failed is not told to leave it, so it tries its own.
- **Evidence, from the owner's own records** (machine-local, read 2026-10-22, quoted without names): on 2026-10-16 a work ask told
  its session that Daoris's git could not fetch origin over SSH an hour before, and to fetch first if it now worked. The session
  tried `git fetch origin`, which failed with `Permission denied (password,publickey)` from an Azure DevOps SSH host, and rebased
  onto the local line instead. The person carried the failure from one session to the next by hand.
- **The push side tries other forms.** UNBLOCK4 widened `no-push` from `git push …` to option-first forms because sessions pushed
  as `git -C . push` and `git -c <key>=<value> push` (`Permissions.cs:152-159`).
- **WSR7 measured the cause.** On the owner's workspace every fetch failed with *Could not read from remote repository*: the git on
  the path could not use the key the person's own Git client used (`docs/task-archive.md:8817-8825`;
  `docs/2026-10-01-tools-design.md:70-78`). WSR7 said it once per look; it could not make it work, and it did not measure which ssh
  the person's client used.

### 0.5 What is already decided that this rests on

- **D121 §2.5** (`docs/2026-10-01-tools-design.md:219-263`): Daoris's git settings travel in a global file named by
  `GIT_CONFIG_GLOBAL`, which **includes the person's own global configuration, unchanged, and never writes it**; the `ssh` choice is
  git's own, Windows', or a command the person names. `credential.helper` was to join "by a row with its reason once a real fetch
  needs it". This design is that row, and it amends §2.5's precedence (§13).
- **D125, D175, D197, for agents' accounts**: Daoris's sessions run on accounts Daoris holds, in folders of its own (D175 point 1);
  copying the person's credentials into a profile is rejected (D125; D175's rejections); the person's own sign-in is never signed
  in, out or written by Daoris (D197 point 3); accounts are **one set**, not lists per scope, because per-scope lists confused (D197
  points 1 and 9). They decide agents' accounts, not git's; this design takes the same principle to git and decides it here.
- **D72, D74**: what an agent may do is the harness's own rules in Daoris's scopes, handed at spawn; `deny` beats `ask` beats
  `allow`; `no-push` is a shipped default the person may switch off by id; an agent narrows at once and widens only on the person's
  yes, and `deny` to `ask` is a widening.
- **D73**: a trust is asked, then written, on a person's explicit act, and never silently. Host keys follow it (§3.5).
- **D135**: a go-ahead is asked once on the ask through `go_ahead_ask`, matched to earlier acts by words and handed to every later
  session on the ask (`D135.md:62-89`); a session with no ask has no such tool (`Adapters.cs:903-912`). So a go-ahead is not an
  exact, single-use permission for one git act, and §6.6 does not use it as one.
- **D184**: a repository's sessions run under an access setup set per repository, workspace and machine (`AccessRules.cs:64-81`),
  read as a stored shape where an unknown pause is passed over (`AccessRules.cs:74-76`); `SET_ACCESS` with no `access` clears the
  level (`AccessRules.cs:349-365`); its defaults are today's behaviour (point 5); under `asking` the protocol door holds a tool's
  permission request for the person (AUTONOMY1b3a; `Acp.cs:1935-1947`), keeping its call id, title, kind and choices and no command
  (`HeldPermissions.cs:27-45`); full access never lifts the boundary (a push stays the landing's, AUTONOMY1c).
- **D172 point 3**: `data/` holds no secret; a file that holds a key lives in `profiles/` (`DaorisHome.cs:362-363`); point 7:
  `profiles/` is kept as private as the machine, and losing it costs signing in again.
- **D67's key accounts**: an API key is plaintext at rest in `profiles/keys.json`, shown only by its last four characters, never
  over the bridge back; DPAPI was rejected there because the CLI twin could not write it without native code
  (`docs/2026-09-23-api-key-accounts.md:24-31, 52-54`). The shell already seals the browser's kept sign-in with DPAPI
  (`DpapiSeal.cs:7-17`).
- **Words that are kept today**: git's failure text is kept in an Ask Daoris sync proposal's file (`HelpSyncProposals.cs:243-247`)
  and said in sync and landing words; a plugin's last stderr line is its answer (`land.mjs:62-67`). The machine log never holds a
  secret (`docs/2026-09-30-machine-log-design.md:363-370`); the sensitive gate refuses a tracked private key block and GitHub's
  classic token shapes (`SensitiveGate.cs:76-81`), not GitHub's fine-grained `github_pat_` nor GitLab's `glpat-`.
- **The glossary**: *git remote* (远程仓库) is a repository's own remote, never the workspace's *remote* (远端)
  (`glossary.json:553-554`); *push* is a thing "Daoris never does on its own" (`:546`); allow (允许), ask (询问) and *deny* (拒绝)
  are the rules' words (`:501`); *go-ahead* is 放行 (`:485`).

### 0.6 Corrections to the brief

- A session's `HOME` is not the account's: only the harness's own configuration variable is (§0.2). An account's folder holds no git
  configuration, key or credential.
- There is no `LandingPush`: the landing's push is the plugin's process (§0.3). The `PUSH` rows in `TASKS.md` are D182's pushed
  state, not git pushes.

## 1. The owner's direction

Stated, not quoted. Sessions keep trying to fetch and push and fail, because Daoris has no git authentication set up for them.
Daoris should handle it comprehensively: several accounts, SSH key management, learned from the git tools people already use, with a
designed screen. Then, widened: a policy for what a repository, a workspace and a session may do on a git remote, in three classes
(read, write, dangerous writes), each allowed, asked or denied, set per workspace and per repository with a default; a session may
tighten its repository's and never loosen it; it fits the permission and autonomy layers Daoris already has rather than adding a
parallel system; Daoris's own landing push and an agent's own push are judged by the same policy.

## 2. What existing tools do, and what Daoris takes

Summarised from each tool's own documentation; nothing was run on this machine.

| Tool | What it does | Daoris takes | Daoris rejects |
|---|---|---|---|
| GitHub Desktop | Accounts per host (github.com, one GitHub Enterprise); sign-in by browser (OAuth); its own git answered by the app's account | an account per host, signed in by the browser; the app's git answered by the app, not the person's helper | one account per host |
| Git Credential Manager (GCM) | Stores credentials in the OS store (Windows Credential Manager), keyed by URL and user; OAuth for GitHub, GitLab, Azure Repos and Bitbucket; several users per host, chosen by the URL's user or `credential.<url>.username`; `credential.namespace` / `GCM_NAMESPACE` separates one tool's entries (default `git`); `credential.credentialStore` / `GCM_CREDENTIAL_STORE` chooses the store; `credential.interactive` / `GCM_INTERACTIVE` stops prompts and windows; settings may be scoped to a URL | the OS store; a namespace of Daoris's own set per URL so its entries never mix with the person's; the Windows store named, never inherited; a user per URL; never interactive in the background | Daoris storing a token in a file of its own |
| Fork, GitKraken, Tower | Profiles (a name, an email, and the host accounts beside them); SSH keys generated in the app or imported, per profile; a key chosen per host or per repository; the public key shown to copy, with a link to the host's key page | an identity: host, way, the name and email it commits as, a label; generate or import a key; copy the public key and open the host's page; choose per repository | a key the app keeps in the person's `~/.ssh` (GitKraken can); one global key for every host |
| VS Code's Git | Sets `GIT_ASKPASS` to a helper of its own, so a git started from the editor asks the editor for a credential | git asking a program Daoris names, never a terminal | a prompt for each act: Daoris's git never prompts in the background (§4.5) |
| OpenSSH `~/.ssh/config` | `Host` aliases with `HostName`, `User`, `IdentityFile`; `IdentitiesOnly yes` offers only the named key; `ssh-agent` and Windows' OpenSSH agent hold keys; `UserKnownHostsFile`, `StrictHostKeyChecking`, `BatchMode` | a configuration file of Daoris's own, read with `-F`, so the person's is never read; `IdentitiesOnly yes` and `IdentityAgent none`; Daoris's own known hosts, checked strictly; `BatchMode yes` always | an agent: Windows' is one per user and holds the person's keys; Daoris's keys in the person's file or agent |
| git `includeIf`, `url.<base>.insteadOf` | `includeIf "gitdir:…"` includes a file by where the repository is; `insteadOf` rewrites an address's start for every command (`pushInsteadOf` for pushes alone, and not where a remote names its own push address); the longest matching start wins | `insteadOf` to send every HTTPS act through a program of Daoris's (§6.5); D121 §2.5's include of the person's own global file | `includeIf` per repository: a path rule that a session's tree under another folder silently misses; Daoris composes each process's configuration for the repository it serves instead (§4.1) |

## 3. The model: git identities

### 3.1 A git identity

A **git identity** (Git 身份) is who Daoris's git acts as on one code host. It has:

| Field | What it holds |
|---|---|
| `id` | stable, drawn at random (`gi-7f3a…`); never shown as the name |
| `label` | the person's name for it, unique on the machine (`Work`, `Personal`, `Org A deploy`) |
| `host` | the host it signs in to: `github.com`, `dev.azure.com` (its SSH host `ssh.dev.azure.com`), `gitlab.com`, or a self-hosted host named by its address, with a port where not the default |
| `way` | `ssh`: a key pair Daoris holds; or `https`: a sign-in kept by GCM in the OS store. One HTTPS identity per host and user, since GCM keys an entry by both |
| `user` | the account on the host, as the host reports it at a test (`octo-work`), else as the person typed it |
| `commitsAs` | the name and email a commit carries where §3.6 applies (`Work Name`, `work@example.invalid`); optional |
| `key` (ssh) | the key's type (`ed25519`, or `rsa-4096` for a host that takes RSA alone) and its SHA-256 fingerprint; never the key |
| `readOnly` | the person's statement that the host grants this identity reading alone (a read-only deploy key, a read-only token): Daoris then never tries a write with it (§6.3) |
| `test` | the last test's answer and when: `reached` (as which user), `refused`, `host-unknown`, `host-changed`, `unreachable` |
| `default` | whether it is its host's default |

`data/git-identities.json` keeps every field above (no secret: D172 point 3), written by the driver under the home's lease,
atomically.

### 3.2 One set, and which identity a remote uses

**The identities are one set on this machine.** No workspace or repository has a list of its own. Each scope only *points*, and
the pointers live in `driver.json` beside the access keys (no secret):

| Key | Shape | Meaning |
|---|---|---|
| `gitIdentity` | `{ <repository>: { <remote>: <identity id> \| "own" } }` | the repository's choice for one of its remotes |
| `workspaceGitIdentity` | `{ <workspace>: { <host>: <identity id> \| "own" } }` | the workspace's choice for a host |
| `machineGitIdentity` | `{ <host>: "own" }` | this machine's *Your own git sign-ins* for a host; a host's identity default is the identity's own `default` |

A remote resolves, from the repository and the remote's **effective** address (the one git would use, rewrites applied):

1. The repository's choice for that remote.
2. Else the workspace's choice for that host.
3. Else this machine's `own` for that host, else the host's default identity.
4. Else none.

The answer names where it came from (*this repository*, *workspace Games*, *github.com's default*), as D184's access does. **A
pointer to an identity that is gone, or whose host is not the remote's, is passed over and said** on the row it was set on, never
read as none above it. A repository's remote whose address names no host Daoris can read (a local path, `file://`) needs no
identity (§6.5 names what such a remote's access means).

**Why one set and pointers, not a list per scope.** D197 found lists per scope confusing: which list a start reads, what a list
excludes, what joining one moves. A git identity is a secret on a host; a copy per scope would be a second secret to rotate. With
one set, removing an identity shows every scope that pointed at it before it goes. And unlike an agent's accounts, which are
interchangeable for a start, an identity is not: the wrong one is *Repository not found* on a private repository. So the scope must
be able to choose, and a pointer is the least it can hold.

**Considered and not taken: an identity that names the owners it serves** (`github.com/org-a/`, GCM's per-URL username as the
rule). It is a second rule to reason about beside the pointers; a workspace's pointer covers the case it was for (a work workspace
on one owner), and a repository its pointer misses is chosen on its own row.

### 3.3 Your own git sign-ins: an explicit choice, never a fallback

Daoris's git **hands no credential of the person's** unless the person chooses **Your own git sign-ins** (`own`) for a scope: a
repository's remote, a workspace's host, or this machine's host. That choice is a widening (§8), confirmed in place before it is
written, never applied by a migration, and shown on every row it reaches with its own mark. Under it, the remote's git runs with
what is ambient for that address (§0.2): the person's chosen ssh (git's own, Windows', or a named one, D121 §2.5) or the
repository's own `core.sshCommand` where it sets one, the person's agent and `~/.ssh`, and the credential helpers and GCM namespace
the person's configuration names. The judge still stands in the way (§6.5), and nothing still prompts (§4.5).

**What "hands no credential" covers, and what it cannot.** For a remote not chosen `own`, Daoris clears in the process's command
scope what git lets it clear for that address: the credential helpers (an empty helper resets the list), the extra HTTP headers
(an empty `http.extraHeader` resets them), and an address that carries a password (`transfer.credentialsInUrl=die`, git 2.37 and
later, refuses it). An authentication input git gives no way to clear (a client certificate or key, `http.sslCert`, `http.sslKey`;
a cookie file, `http.cookieFile`) found in the address's effective configuration makes the remote **held** (§5) as
`own-input`, naming the key, until the person chooses `own` for it or removes the setting. Daoris reads those keys' names, never
their values.

Daoris reads nothing of the person's own to set an identity up, with **one** exception the person starts: **importing a key file**
the person names (§7.2), which copies that one file into Daoris's own folder. Daoris never lists the person's `~/.ssh`, never offers
its keys, never reads the person's credential store and never writes either.

### 3.4 Secrets: where each lives

| Secret | Lives | Never |
|---|---|---|
| An SSH private key | `profiles/git/keys/<id>/key`, its `key.pub` beside it. The folder is made, and its access limited to the person's Windows account (inherited entries removed; SYSTEM and Administrators keep their implicit reach), **before** a key's bytes are written into it, by generation or import | in `data/`, a tracked file, the environment, a log line, an answer or an event; never with a passphrase Daoris would have to ask for in the background |
| An HTTPS sign-in (an OAuth token, a personal access token) | the Windows store through GCM, in Daoris's namespace (`daoris`), keyed by the host's URL and the identity's `user` | in any file of Daoris's; read by Daoris at all: GCM hands it to git, and Daoris only asks GCM to store or erase it |
| A host's key | `profiles/git/known_hosts`, Daoris's own | the person's `known_hosts` |

**A key is plaintext at rest, as `profiles/keys.json`'s API keys are** (D67), protected by its folder's access. OpenSSH needs a file
it reads for each connection. DPAPI sealing (the browser's seal, `DpapiSeal.cs`) was weighed again, since D67's reason (the CLI twin
had no native code) no longer applies after D194 moved the families into the driver. It would protect a copied file on another
account or machine, and that is its whole gain: a sealed key must be unsealed to a file for every connection, which a process of the
same account can read as easily as the plain one (§10). A passphrase is rejected for the same reason: nobody is there to type it at
a background fetch. **Backup and recovery** are D172's for `profiles/`: keep it as private as the machine or not at all; a key lost
with the folder is generated again and added on the host again, and an HTTPS sign-in is signed in again.

### 3.5 Host keys: asked, then written, never silently

Following D73. A host's key enters Daoris's `known_hosts` only at a person's press: **Test connection** on an identity, which shows
the key's fingerprint and, for a public host Daoris knows (github.com, gitlab.com, `ssh.dev.azure.com`), whether it matches the
fingerprint that host publishes, read from a list carried with the tool resources so it updates without a release (`resources.json`,
D121 §3). Every other connection runs with `StrictHostKeyChecking yes` against Daoris's file. ssh's own words tell the two failures
apart (its *host key is not known* and its *host identification has changed* warnings, measured in GITAUTH1d): a host never tested
fails as `host-unknown`, and one whose key changed as `host-changed`, said in danger ink (§7.4). Nothing is trusted on first use in
the background.

### 3.6 The name and email a commit carries

Authentication is not authorship, so the rule is its own: **a commit made in a session's tree, or by Daoris in the repository (a
squash's commit, `Landing.cs:41-45`), carries the `commitsAs` of the identity its repository's `origin` resolves to, only where
the repository's own configuration names no `user.name` and `user.email`** (read with `git config --local`, and the tree's
worktree scope). A repository that names its author keeps it; an identity with no `commitsAs`, `own`, or no identity leaves git's
own answer, the person's global configuration, as today. It is handed as `user.name` and `user.email` in the command scope of a
process serving that repository (§4.1), so nothing is written into the repository.

## 4. Wiring every git Daoris starts

### 4.1 Daoris's git file, and each process's command scope

**D121 §2.5's file** (TOOLS6), moved to `profiles/git/global.gitconfig`, stays as designed: the person's global configuration
included first and unchanged, then Daoris's keys. It holds no identity and no address.

**What must beat a repository's own configuration goes into each process's command scope**, composed for the one repository the
process serves (a session's, a landing's, Daoris's own git per call), and for no repository where it serves none (Ask Daoris's
room, an intake, a brainstorm: handed no identity, so they reach no private remote):

| Variable | Value | Why |
|---|---|---|
| `GIT_CONFIG_GLOBAL` | Daoris's file above | D121 §2.5 |
| `GIT_SSH_COMMAND` | `"<daoris-driver>" git ssh`, the driver's program by its absolute path, quoted | git reads it before `core.sshCommand` at any scope, so a repository's own ssh command cannot bypass the launcher (§4.2); the launcher itself runs the repository's own command where that remote is `own` |
| `GIT_SSH_VARIANT` | `ssh` | git then passes OpenSSH's options and never probes the launcher with `-G` |
| `GIT_CONFIG_COUNT`, `GIT_CONFIG_KEY_n`, `GIT_CONFIG_VALUE_n` | per remote address not `own`: `credential.<address>.helper` empty, then GCM's path; `credential.<address>.namespace=daoris`; `credential.<address>.credentialStore=wincredman`; `credential.<address>.interactive=false`; `credential.<address>.username=<user>` for an HTTPS identity; `http.<address>.extraHeader` empty. For every HTTPS or HTTP address of the repository's remotes: `url.daoris::<scheme>://<host>/.insteadOf=<scheme>://<host>/` (§6.5). Where any remote is not `own`: `transfer.credentialsInUrl=die`. Where §3.6 applies: `user.name`, `user.email` | composed per repository, so two repositories at one address may use two identities, and a repository's local or worktree `credential.username` cannot outrank it; an `own` remote gets no entry, so the person's own helpers answer it |
| `GIT_TERMINAL_PROMPT` | `0` | git asks no terminal |
| `GIT_ASKPASS` | set empty | git skips every askpass program, `core.askPass` and `SSH_ASKPASS` included (an empty value stops git's search, in its documented prompt order) |
| removed | `SSH_ASKPASS`, `SSH_ASKPASS_REQUIRE`, `GCM_NAMESPACE`, `GCM_CREDENTIAL_STORE`, `GCM_INTERACTIVE`'s inherited value (then set `never`), `GIT_TRACE_CURL`, `GIT_CURL_VERBOSE`; `GIT_TRACE_REDACT=1` | an inherited variable overrides GCM's configuration or prints a header; Daoris names each instead |

**Why the command scope now, against D121's rejection.** D121 kept a repository's own configuration winning so a deploy key in
`core.sshCommand` would keep working. For authentication that rule is the leak: a repository's own helper or ssh command answers
with the person's credentials, and Daoris's identity never reaches it. So for the keys above, Daoris's command scope wins, and a
repository's own authentication is honoured only where the person chose `own` for that remote: then the launcher runs the
repository's own `core.sshCommand` (read with `git config --local`), still judged. **The move** lists every repository whose own
configuration names authentication (§6.8), so nothing that works today stops without being said. Every other key of a repository's
own configuration still wins over Daoris's global file, as D121 §2.5 has it.

A git older than 2.37 refuses an identity's use, naming the version (`transfer.credentialsInUrl`'s floor; `GIT_CONFIG_GLOBAL`
needs 2.32 and the command scope 2.31), as D121 §2.5 refuses a git setting below 2.32. The managed MinGit is 2.56.

### 4.2 SSH: Daoris's launcher

`GIT_SSH_COMMAND` names **`daoris-driver git ssh`**. Its contract, as git calls an OpenSSH-variant ssh (git's documented behaviour,
held by a table against git 2.56 in GITAUTH1b):

- **Arguments it takes**: `-p <port>`, `-4`, `-6`, `-o SendEnv=GIT_PROTOCOL`, then `[<user>@]<host>` and one remote command:
  `git-upload-pack '<path>'` (read), `git-receive-pack '<path>'` (write), or `git-lfs-authenticate '<path>' download|upload` (read
  or write). Any other option or command is refused with one line naming it, never passed on.
- **What it does**:
  1. Finds the repository by its working folder (a checkout or a session's tree, as the trees' context maps it), and the identity
     serving `<host>/<path>` for it (§3.2), from the files alone, taking no lease.
  2. Asks the judge (§6.5); a refusal ends here with one line on stderr, which git prints, and a coded machine-log line.
  3. Starts the ssh Tools resolves (git's own, Windows', a named one; D121 §2.5) by its absolute path with
     `-F profiles/git/ssh_config -i <key> -o IdentitiesOnly=yes -o IdentityAgent=none -o BatchMode=yes
     -o StrictHostKeyChecking=yes -o UserKnownHostsFile=profiles/git/known_hosts -o ForwardAgent=no`, git's own `-p`, `-4`, `-6`
     and `SendEnv` after them, each path quoted as that ssh reads it. A named ssh that is not OpenSSH cannot carry an identity:
     the identity's use refuses, naming the command.
  4. Under `own`, starts the repository's own `core.sshCommand` where it names one, else the person's chosen ssh, with git's
     arguments and `-o BatchMode=yes` alone, so it reads the person's configuration and agent and still prompts for nothing.
  5. Relays the streams. For a write it reads the push's commands as they pass and asks the judge for the whole batch before
     relaying any of it (§6.5).
  6. Touches `run/git/used/<id>`, read by the page as *last used*.
- No key path or secret is put in any environment; the launcher finds the key from the identity.

### 4.3 HTTPS: GCM in Daoris's namespace

The helper is GCM, named by its path: the one beside the resolved git where it has one, else the system's. Daoris's entries carry
the namespace `daoris` and the Windows store, both set per address so an `own` remote keeps the person's; the per-address `username`
picks which of a host's identities answers. Where no GCM is found, an HTTPS identity cannot be added, said with the reason, and an
HTTPS remote is read anonymously where the host serves it so. Whether the built-in MinGit carries GCM, and the GCM version whose
commands this relies on (its store, its erase, its per-address settings, a provider's browser sign-in), are fixed in GITAUTH1f
against a real GCM before HTTPS identities ship; if MinGit carries none, GCM joins the declared tools as a managed one (D121's
resource list) in that row.

### 4.4 Who is wired

Every process the driver and the modules start that may run git for a repository: a driven session on either door, a chat, a
reviewer, a hook, a landing plugin, a plugin's trial and tool run, and Daoris's own git (`WorkingTree.GitAsync`), D121 §2.4's
list. A process that serves no repository (Ask Daoris's room, an intake, a brainstorm's pass) gets §4.5's variables and no identity.
**Not the terminal's shells**: the person's own shell in Daoris's panel runs the person's own git, as any terminal on the machine
does. This amends D121 §2.6's *the person's own `git fetch` in the terminal behaves as Daoris's does* for identities: a person's
command must not act as Daoris's identity, nor be judged by a policy written for agents. Held by D121's source-reading test of every
`new ProcessStartInfo`, extended with the variables above.

### 4.5 Nothing prompts

No git Daoris starts asks anyone anything, under an identity or `own` alike: `GIT_TERMINAL_PROMPT=0`, `GIT_ASKPASS` empty, no
`SSH_ASKPASS`, `GCM_INTERACTIVE=never`, ssh's `BatchMode yes` (§4.1, §4.2). A sign-in happens only at the person's press in the Git
place (§7.2), the one place GCM is started interactive. A credential that is missing or refused fails at once with git's words,
which §5 classifies.

## 5. When no identity serves a remote, or the host refuses one

### 5.1 Failures, classified

The launcher, the relay, the judge and Daoris's own git class a remote act's failure by the transport's own words, into a table
measured against git 2.56, OpenSSH and GCM in GITAUTH1a (and extended by 1b and 1f):

| Class | Read from | Holds |
|---|---|---|
| `no-identity` | the resolution (§3.2): nothing serves the remote, and the host refused anonymous use | reads and writes |
| `own-input` | §3.3: an authentication input git cannot clear | reads and writes |
| `refused` | ssh's `Permission denied (publickey…)`; git's `Authentication failed`; HTTP 401 | reads and writes |
| `write-refused` | HTTP 403 on a push, or the host's own refusal of a push it would read | writes only |
| `host-unknown`, `host-changed` | ssh's own host-key warnings (§3.5) | reads and writes |
| `denied`, `ask` | the judge (§6) | that act alone; nothing held |
| `unreachable` | a name not resolved, a refused or timed-out connection | nothing |
| `unclassed` | any other failure | nothing: said per occasion, as today |

### 5.2 Held until something changes, said once

- **A hold is keyed by** the repository, the remote's effective address (host and path), what served it (an identity's id, `own` or
  none) and what it holds (reads and writes, or writes alone). A changed address, a changed resolution or a changed identity is
  another key, so it is tried.
- **While held**, no Daoris act connects for what the hold covers: the moments skip it, a look lists it as held and judges against
  the last fetch (WSR7's rule), a press says it in place and does not connect. Nothing retries on a clock. Holds are kept in
  `run/git/held.json`, so a restart of Daoris tries each once.
- **What lifts it**: a change to the identity, a pointer, a host key, the address or the policy; a passing **Test connection**; or
  the person's **Try again**, on the row and on every press that says it. Each wakes the acts that skipped it once (FABRIC-EVENT's
  shape, never a poll).
- **`unreachable` and `unclassed` hold nothing**: today's minute (D148) bounds the next try.
- **Said once.** One row in *What needs you* per held group (§7.4): what is missing, where the person sets it, and a press that goes
  there. A session's or a press's words name the row rather than repeating its reason.

### 5.3 What a session is told

A session's instruction gains a **Git** section beside the boundary (`Adapters.cs:985-1002`), composed from the resolution, the
holds and its git access at its start; LANDSYNC2's sentence stays and points to it:

> **Git.** `origin` is `github.com:org-a/engine`, reached as the identity *Work* over SSH. You may fetch and pull it. Pushing is not
> yours: your accepted work is pushed by the landing. Force-pushing, deleting a remote branch, pushing a tag or pushing to `main`
> are refused.

where git access lets it ask (§6.6):

> … A push of a branch asks the person first: ask with `git_ask` (the remote, the refs, why) and end your turn; you are started
> again with the answer.

and where nothing serves the remote:

> **Git.** `origin` (`ssh.dev.azure.com`) has no git identity on this machine, so neither you nor Daoris can reach it now. Do not
> fetch, pull or push it: work from the local `main`, and say in your closing note that origin was not fetched.

A carry-on or a resume after a change is told the change after its words, as D184's go-aheads are (`Continuations`).

## 6. Git access: what may be done on a remote

### 6.1 The classes and their values

| Class | Acts, by what they do to the remote (not by the flags typed) | Values |
|---|---|---|
| **Read** (读取) | fetch, pull, clone, ls-remote, a submodule's fetch, an LFS download | allow · ask · deny |
| **Write** (推送) | an update of a branch that is a fast-forward, or the creation of a branch; an LFS upload | allow · ask · deny |
| **Dangerous** (危险推送) | an update that is not a fast-forward (what `--force`, `--force-with-lease` or a `+` refspec let through), a deletion of a remote branch, any update under `refs/tags/`, any update of the repository's line (its protected branch, D87) | allow · ask · deny |

A push of several refs is judged as a whole by its strictest update. A `--force` push that turns out a fast-forward is a Write: the
remote loses nothing, and the push's commands carry no flags to tell otherwise.

### 6.2 Levels, the default, and a session's tightening

- **Per repository, per workspace, this machine**, each class resolved on its own with its source, as D184's pauses resolve
  (`AccessRules.cs:70-72`).
- **Daoris's default is today's behaviour, door by door** (D184 point 5), since no single value is today's: **Read** *allow*;
  **Write** *ask* for Daoris's landing, answered by the person's yes as today (§6.7), and *deny* for a session; **Dangerous** *deny*,
  but for the one act a person's landing rule names (§6.7). The page says it in those words (*Daoris's default: the landing pushes on
  your yes; sessions do not push*), never as one value.
- **A value the person sets, at any level, binds every door alike**: the landing and a session meet the same value. That is where
  *the two can never disagree* is a rule; under the default they keep today's two behaviours, which the page says.
- **A session runs under the stricter of two**: what its repository's git access resolved to at its start, kept on its record as
  D184 keeps `access`; and what it resolves to now, read by the judge at each act. A loosening after the start does not reach a
  running session; a tightening reaches its next act, since the doors are always in the way (§6.5).
- **The person may tighten a running session** from its head (§7.5) or the terminal (§8), narrowing its record's value. Nothing
  loosens a session above its repository. **An agent changes no access**, its own included.

### 6.3 Identity and access

**Identity is who git acts as; access is what it may do.** They resolve apart and meet at the judge:

- A remote with no identity, where the host refuses anonymous use, **cannot act whatever its access says**: `no-identity` is the
  answer before access is read.
- A `readOnly` identity makes Write and Dangerous *deny* for its remotes, whatever the access, since the host would refuse them. It
  is the one guarantee no process on the machine can lift (§10), and the Git place says so on its row.
- Access never chooses an identity, and an identity never widens access.

### 6.4 Where it is kept: the access setup, not a parallel system

- **Stored** in `driver.json` beside `access`, `workspaceAccess` and `machineAccess`, as `gitAccess` (by repository),
  `workspaceGitAccess` and `machineGitAccess`, each `{read, write, dangerous}` with any subset set. Separate keys, not pauses in the
  same objects, because a level of `access` may be the string `"full"` (`AccessRules.cs:18-22`) and **full access never touches git
  access**: full access opens the pauses, and a push is a boundary, not a pause (D184's AUTONOMY1c note).
- **Resolved, said and edited by `AccessRules`**, the one place a start, the route and the terminal ask, through its own door,
  **`SET_GIT_ACCESS`** and `daoris-driver git access` (§8). Not a field on `SET_ACCESS`: a payload with no `access` clears the
  access setup on a host that does not know the field (`AccessRules.cs:365`), and a new route an older host refuses as unknown
  rather than misreads. Its effective values ride `LINES` with their sources.
- **An older build** reading the file passes the new keys over and keeps them (`AccessRules.cs:74-76`), and enforces none of them;
  a rollback says so in its first look's notice (*git access is set here, and this build does not judge it*).
- **Kept on the record** at each driven start beside `access` (machine-local, as `access` is), read back by `PriorSession`.

### 6.5 One judge, and where it is asked

**The judge** is one function in the driver's domain (`GitAccessRules.Judge`). It takes the repository (found by the act's working
folder, never by a variable alone), the session where its record is named, the remote's effective address, the act's class and its
ref updates, and the grants (§6.6), and answers *allow*, *ask* or *deny* with a code. It is asked at every place a remote act can
pass:

| Where | What it sees | What can pass it | Chosen as |
|---|---|---|---|
| **Daoris's own acts**, in process: a fetch, a press, a moment, a start's fetch, a landing before its plugin starts | everything: the act is Daoris's own | nothing | **yes**: the judge's first caller |
| **The harness's rules** (Claude Code's `Bash(…)` rules in Daoris's tier, D72) | the command as written | an alias, a script, a path to git, `sh -c`, Codex (no rules), any form no pattern names | **yes, for the flow**: the policy writes the rules (§6.8), so an ordinary push meets the person as a held call (§6.6) before git starts. Never the guarantee |
| **A `pre-push` hook** through a session's `core.hooksPath` | every ref update, over any transport | `git push --no-verify`; and `core.hooksPath` replaces the repository's own hooks, so every other hook needs a forwarder | **no**: skipped by one flag, and it would break a repository's own hooks |
| **A credential helper that refuses** | the host and path only: git does not tell a helper whether it fetches or pushes | — | **no**: it cannot tell a read from a write |
| **Daoris's ssh launcher** (§4.2) | the remote command (read, write, LFS), the host and path, and the push's commands in the stream | unsetting `GIT_SSH_COMMAND` in the agent's own shell, together with a key of the person's own | **yes, for SSH** |
| **The HTTPS relay**: `insteadOf` sends every HTTP and HTTPS act of the repository's remotes to `daoris::<address>`, a remote helper of Daoris's on the child's `PATH` (`git-remote-daoris`), which relays git's remote-helper conversation to git's own `git remote-https` for the original address | reads, and every `push` command | an explicit `-c url…` on the command line, which beats the command scope; unsetting the command scope's variables | **yes, for HTTP and HTTPS** |
| **An identity the host grants reading alone** (`readOnly`) | — | nothing on this machine | **yes, offered**: the hard bound, the person's to set up on the host |

**Reading a push.**
- *Over SSH*, git sends `git-receive-pack` one command per ref, `<old> <new> <ref>`, before the pack. A creation is an `<old>` of
  zeros; a deletion a `<new>` of zeros (in the repository's object format's length); `refs/tags/` a tag; the line's ref the protected
  branch. An update is a fast-forward where `<old>` is an ancestor of `<new>` in the local repository; where `<old>` is not held
  locally, git sends such an update only when forced, so it is not a fast-forward. The launcher reads every command until the
  flush, asks the judge for the batch, and relays it only on *allow*; on anything else it closes the connection before the pack, so
  no ref moves.
- *Over HTTPS*, the helper conversation names refspecs, not object ids. The relay keeps the remote's refs from the `list for-push`
  answer as it passes, resolves each `push <src>:<dst>` source in the local repository, reads `+` as a forced update, an empty source
  as a deletion and `option cas` lines as a lease, judges the whole batch (the `push` lines up to their blank line) as above, and
  forwards none of it unless the batch is allowed.
- Both are proved against a real git before anything is built on them (GITAUTH1b).

**Where the doors do not stand, said, and refused for Daoris.** At each start and each press, Daoris reads each remote's effective
fetch and push addresses with the process's own composed configuration (`git remote get-url --all` and `--push --all`). A push
address the relay's rewrite does not reach (a more specific rewrite of the person's), a `git://` or other helper's address, or a
local path or `file://` remote is said on the remote's row and in the session's Git section: Daoris's own acts treat its Write and
Dangerous as *deny*, and the policy's rules deny them in the harness; a session's git could still reach it, which §10 names. An LFS
upload over HTTPS goes to the LFS API with GCM's credential, past the relay: its objects may land, and the ref update that would
make them reachable is still judged.

**Why the transport, and not the hook.** The hook is the conventional answer and the wrong one here: the commonest agent bypass
(`--no-verify`) skips it, and taking `core.hooksPath` would silence a repository's own hooks. The transport is where git must pass to
reach the host at all, and Daoris already has to stand there to hand the identity. The doors are always installed for a process
serving a repository, whatever its access, so a tightening reaches the next act. The rules stay because they turn an ordinary push
into a question the person answers in the flow, before git starts.

### 6.6 An ask: a held call, or a git ask

An *ask* becomes a wait the person answers, and the answer is an **exact grant** for one act:

- **A grant** is one file in `run/git/grants/`, written by the driver: the session (or the landing), the repository, the remote's
  effective address, the class, the refs where known (and for a landing the new commit, §6.7), and its bound. The door that judges
  a push takes it by an exclusive rename before relaying, re-judges against the access as it stands then (a tightening since wins),
  and uses it for that one push. A restart drops grants, as a held request ends with its agent.
- **On the protocol door with Claude**, the policy's *ask* rules raise a permission request. Daoris holds it for the person
  **whatever the session's posture** where the request's command (the tool call's raw input, kept with the hold, bounded and
  machine-local, never logged) matches a rule git access wrote: the hold of D184's AUTONOMY1b3a widened from the asking posture's
  mark to git access's own rules. The card and its *What needs you* row say the act in git's words (*Push `daoris/fix-ranking` to
  `origin`*, its class). *Allow once* answers the agent and writes the grant, bound to the refs the command names where it names
  them, else to one push of that class to that remote.
- **Everywhere else** (the native door, Codex, a command no rule matched, a harness that does not ask under its posture), the door
  refuses with one line: *this push asks the person first: use `git_ask` and end your turn.* **`git_ask`** is a connector tool
  (remote, refs, class, why) every session's connector allows, on an ask or not: it writes the session's wait, which *What needs
  you* lists as a **git go-ahead** (放行) for that exact act, kept on the session (not the ask, unlike D135's), and the person's
  *Allow* writes the grant and starts the session again with the answer, as an answered question does. A refusal is said to it the
  same way. The same act asked again joins the waiting one.
- **Daoris's own fetch under Read *ask*** fetches only at a person's press (the press is the yes); its moments and looks skip the
  remote, and the repository's row says so once.

### 6.7 Daoris's landing, under the same judge

The landing asks the judge before it starts its plugin, for the push its frame describes: `<tip>:refs/heads/<branch>` to `origin`
(`LandingFrame`, `Landing.cs:334-343`; the GitHub plugin's push, `land.mjs:79`). On *allow* it writes a grant bound to that
remote's effective address, `refs/heads/<branch>` and the new commit `<tip>`, and hands its id to the plugin's process in
`DAORIS_GIT_GRANT` (a variable of that one start, `HookProcess`), so the door matches the plugin's own push exactly; no other push
can use it. A branch rule with no plugin pushes nothing (`Landing.cs:24-28`) and asks nothing.

- **Under Daoris's default**, Write is answered by the person's yes, as today: the *Accept…* press is it, and a landing rule that
  accepts automatically (D145, or D184's `landing: rule`) is the person's **standing yes for exactly that push**, which the person
  set on the rule. A landing rule whose branch is the line is the person's standing yes for that Dangerous act alone; no other
  Dangerous act may use it.
- **Under a value the person set**, the landing meets it as a session would: *deny* refuses before its plugin starts (*this
  repository's git access denies writes to `origin`*, with the press to its row; a merge landing, which pushes nothing, lands as
  before); *ask* is answered by *Accept…* or the standing yes above; *allow* lets it.
- A landing never forces.

### 6.8 Moving from today

- **`no-push`** keeps its id and becomes the policy's rule writer: at each spawn its rules and its classifier sentence (`HardDeny`,
  `Permissions.cs:166-167, 190-195`) are composed from the session's git access: Write *deny*, the four push forms as `deny`; Write
  *ask*, as `ask` (and the classifier told a push asks the person); Dangerous, the force, delete and tag forms the same way. Under the
  default a session's Write is *deny*, so a start reads exactly as today. A machine whose person switched `no-push` off reads, once,
  as this machine's `machineGitAccess.write` *allow*, written and said in the access section's notice; switching `no-push` back on
  is a narrowing, which writes this machine's Write back to the default's.
- **A landing rule whose branch is the line** keeps landing by its standing yes (§6.7); no level's value is written for it.
- **Every remote moves off ambient credentials** at the first start of the build that wires the move (GITAUTH1e). Until then, a
  remote no identity serves keeps today's ambient credentials, and only a remote an identity serves uses it. At the move, one row in
  *What needs you* says it: *Daoris's git no longer uses your own git sign-ins. {n} repositories' git remotes on {hosts} have no git
  identity yet*, listing apart the repositories whose own configuration names authentication (§4.1), with two presses: **Set up a
  git identity…** and **Keep using my own sign-ins for these…** (an inline confirmation naming what it lets Daoris's git use,
  writing `own` for those hosts at this machine). Nothing is pre-applied: until one is pressed, those remotes are held (§5.2).

## 7. The window

Parts from the UI/UX design (D185 §4): `Card`, `SettingList` and `SettingRow` (R1's label track), `FormGrid`, `Segmented`,
`SelectField`, `Pill`, `Dot`, `CodeText`, `PathText`, `MonoWell`, `InlineConfirm` (its warning tone for a widening), `Refused`
(R3), `PhaseList` (R2), `Inheritable`, `EmptyState`. A refusal is said under the press that started it, the form kept open with what
the person typed (R3). R1's geometry is held by the rendered check R1 names, not by the stories alone.

### 7.1 Settings → Git

A machine domain after *Tools* (`settings/domains.ts:47-69`): `{ id: 'git', label: 'settings.domain.git', machine: true }`, so a
browser is offered none. Three cards.

```
 wide, Settings → Git
┌──────────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ Git identities                                                                       [+ Add a git identity…] │
│ ─────────────────────────────────────────────────────────────────────────────────────────────────────────── │
│ Work                github.com · SSH ed25519 · SHA256:q3Vf…               ★ Default for github.com           │
│                     commits as Work Name <work@example.invalid>                                              │
│                     ● Reached as octo-work 2 min ago · last used 5 min ago · 12 repositories    [Test] ⋯    │
│ Personal            github.com · HTTPS · signed in through Git Credential Manager                            │
│                     ● Reached as octo-me yesterday · last used yesterday · 1 workspace          [Test] ⋯    │
│ Org A deploy        ssh.dev.azure.com · SSH rsa-4096 · SHA256:Zk1p… · read only on the host                  │
│                     ✕ Refused 1 h ago: the host does not take this key                                       │
│                                          [Copy public key] [Open key page] [Test again]                     │
├──────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ Defaults by host                                                                                              │
│ github.com          [Work ▾]                          set here · daoris-driver git default github.com Work      │
│ ssh.dev.azure.com   [Org A deploy ▾]                                                                          │
├──────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ Git access on this machine                                                                                    │
│ Read  allow   Write  as today   Dangerous  deny        Daoris's default: the landing pushes on your yes;      │
│                                                         sessions do not push                       [Change…]  │
│ Your own git sign-ins   not used                                                              [Use for…]     │
└──────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

- **An identity's row**: its label; host, way, key type and fingerprint (SSH) or *signed in through Git Credential Manager*
  (HTTPS); *read only on the host* where `readOnly`; *commits as* where set; the test's mark and words (*Reached as {user} {time}*,
  *Refused*, *Host not yet trusted*, *Host key changed*, *Not tested*), last used, and how many repositories and workspaces point at
  it. Acts by way and state: **Test** (**Test again** after a failure); for SSH **Copy public key** and **Open {host}'s key page**;
  for HTTPS refused **Sign in again**; **Set as default for {host}**; under ⋯ **Rename…**, **Commits as…**, **Read only on the
  host** (a switch), **Remove…**.
- **Remove…** opens an inline confirmation listing every scope pointing at it, which fall to the next step of §3.2; it deletes the
  key folder (SSH) or asks GCM to erase the entry (HTTPS).
- **Empty**: *No git identities yet. Daoris's git reaches a private repository only as an identity you set up here.* and the add press.
- **Git access on this machine**: the machine level's three classes (§7.3's row), and **Your own git sign-ins** with where it is used
  and **Use for…** (a host at this machine), a widening confirmed in place.

### 7.2 Add a git identity

A form in the pane (`FormGrid`), never a drawer, in steps, each step's refusal under its press with what was typed kept:

1. **Where**: host (`github.com`, `dev.azure.com`, `gitlab.com`, *Another host…* with its address), and a label.
2. **How**: *SSH key* or *HTTPS sign-in*. The way the repositories pointing at the host already use first.
3. **SSH**: *Generate a new key* (ed25519; RSA 4096 where the host takes RSA alone, said with the reason) or *Import a key file…* (a
   file picker; the one read of the person's own, §3.3, confirmed as a widening; a key with a passphrase is refused, saying why).
   Then the public key in a `MonoWell` with **Copy**, and **Open {host}'s SSH key page** (github.com's, gitlab.com's, Azure DevOps'
   for its organisation), or for another host *add it where that host takes SSH keys*.
4. **HTTPS**: **Sign in through your browser** or **Paste a token…** (a password field; Daoris hands it to GCM to store and keeps
   nothing). The sign-in is a `PhaseList` of the driver's own steps (R2), each with its time: *Starting Git Credential Manager*
   (until the driver has GCM's process up), *Waiting on your browser* (until GCM's process exits; GCM's own page is no phase of
   Daoris's), *Testing the connection* (the driver's own read, below), *Saving*. **Cancel** stops GCM's process and is reached by
   Tab, never Escape; a bound is said only where GCM states one; a refusal names the phase that failed with **Try again** and
   **Close**, as D185 §3.2's sign-in does.
5. **Commits as** (optional): name and email.
6. **Test connection**: the cheapest read the host answers, `git ls-remote` of a repository pointing at the host where there is
   one, else the host's own greeting (github.com and gitlab.com name the account). At first contact it shows the host's key
   fingerprint and whether it matches the host's published one, with **Trust this host** (§3.5). The identity is saved at step 3 or
   4; a failed test leaves it with its mark and the act that fixes it.

### 7.3 A repository's and a workspace's git remote and git access

**Repository → Setup**, a section **Git remote** after *Line and landing*:

```
 half (630 px), a repository's Setup
┌───────────────────────────────────────────────────────────────┐
│ Git remote                                                    │
│ origin       github.com:org-a/engine                          │
│              Work · SSH        ↑ workspace Games    [Change…] │
│ upstream     github.com:upstream/engine                       │
│              Personal · HTTPS  ↑ github.com's default [Change…]│
│ Git access   Read allow ↑  Write ask •  Dangerous deny ↑      │
│              ↑ from workspace Games · • set here   [Change…]  │
└───────────────────────────────────────────────────────────────┘
```

- **Each remote** says its effective address (host and path; a user, password or token in the address is never shown, §10), the
  identity it resolves to with its source, and **Change…**: the host's identities, *Your own git sign-ins* (a widening, confirmed),
  *As the workspace says* (clear). A remote nothing serves says *No git identity* in the danger ink with **Set one up…**; one the
  doors do not reach (§6.5) says so, and why.
- **Git access** is one compact row (R1): the three classes, each its value and a mark for where it came from (↑ from above, named in
  its tip; • set here), and **Change…**, which opens three `Segmented` controls (allow / ask / deny) under the row, each with *Clear*
  back to the level above (`Inheritable`). A change that loosens any class opens `InlineConfirm` in the warning tone naming what it
  lets happen (*Sessions may push branches to origin without asking you*); tightening asks nothing.
- **Workspace → Setup**, a section **Git**: a row per host its repositories reach, its identity choice for that host (**Change…**),
  and its git access row.
- **Settings → Driver's access card** gains the machine's git access row, the same rows at three levels as D184's pauses.

### 7.4 What needs you

Each a row with its acts, judged in .NET (`NeedsAPerson`), carrying its repository's workspace (`Scope`), counted where it holds
work, its hook named in Ask Daoris's coverage and its words in the glossary's kinds (`.claude/knowledge/person-waits.md`):

| Kind | Says | Acts | Holds work |
|---|---|---|---|
| `git.identity.missing` | *{n} git remotes on {host} have no git identity* (the repositories in its detail) | **Set one up…** (Settings → Git, host filled), **Choose…** (the first repository's row), **Try again** | where a session or a landing waits on it |
| `git.identity.refused` | *{host} refused {identity}* | SSH: **Copy public key**, **Open key page**; HTTPS: **Sign in again**; `own`: **Open Settings → Git**; every one: **Try again** | as above |
| `git.own.input` | *{repository}'s configuration names its own authentication for {host}* (the key's name) | **Use my own sign-ins for it…**, **Try again** | as above |
| `git.host.unknown` / `git.host.changed` | *{host} is not yet trusted* / *{host}'s key changed since you trusted it* (danger ink) | **Compare and trust…** (the identity's test), **Try again** | as above |
| `git.own.moved` | §6.8's one-time row | **Set up a git identity…**, **Keep using my own sign-ins for these…** | no |
| `git.ask` | a session's git go-ahead: *{session} asks to push {refs} to {remote}* (its class) | **Allow**, **Refuse**, and the door to the session | yes |
| a held call (`permission`) for git access's *ask* | the existing kind, worded from git's act and class | the existing presses | yes |

The git go-ahead is a wait of its session's (`work/waits/waits.ts`), drawn in the session's head and foot with the same handlers as
its row, as a held call is.

### 7.5 A session's head

*Details* lists the session's git access as it started and each remote's identity label (D185 §3.3's `AccessFact`, extended), with
**Tighten…** (the three classes, narrowing only). The facts line wears the plain warning chip D185 gives full access where the
session's Write resolves *allow* (*Pushes without asking*), and a second where its Dangerous does (*May force-push*).

### 7.6 Words, both languages, and the glossary

Chrome translates; an identity's label, a host, a user and an email are content and stay as written. A new catalogue
`settings.git.json` (en, zh), and rows in `work.attention.json`, `work.wait.json` and `note.json` for the codes, each key placed in
the glossary's kinds. Some of the keys:

| Key | English | 中文 |
|---|---|---|
| `settings.domain.git` | Git | Git |
| `settings.git.identities` | Git identities | Git 身份 |
| `settings.git.add` | Add a git identity… | 添加 Git 身份… |
| `settings.git.way.ssh` | SSH key | SSH 密钥 |
| `settings.git.way.https` | HTTPS sign-in | HTTPS 登录 |
| `settings.git.commitsAs` | commits as {{who}} | 以 {{who}} 提交 |
| `settings.git.reached` | Reached as {{user}} {{time}} | 已以 {{user}} 连接，{{time}} |
| `settings.git.refused` | Refused: the host does not take this identity | 被拒绝：主机不接受这个身份 |
| `settings.git.copyKey` | Copy public key | 复制公钥 |
| `settings.git.keyPage` | Open {{host}}'s key page | 打开 {{host}} 的密钥页面 |
| `settings.git.defaultFor` | Default for {{host}} | {{host}} 的默认身份 |
| `settings.git.readOnly` | read only on the host | 主机上只读 |
| `settings.git.own` | Your own git sign-ins | 你自己的 Git 登录 |
| `settings.git.none` | No git identity | 没有 Git 身份 |
| `settings.git.trustHost` | Trust this host | 信任这个主机 |
| `settings.git.tryAgain` | Try again | 重试 |
| `settings.access.git` | Git access | Git 访问权限 |
| `settings.access.git.read` | Read | 读取 |
| `settings.access.git.write` | Write | 推送 |
| `settings.access.git.dangerous` | Dangerous | 危险推送 |
| `settings.access.git.asToday` | Daoris's default: the landing pushes on your yes; sessions do not push | Daoris 的默认：落地在你同意后推送；会话不推送 |
| `work.access.pushesFreely` | Pushes without asking | 推送无需询问 |

The values are the rules' allow (允许), ask (询问) and deny (拒绝). *Git remote* (远程仓库) is already the glossary's
(`glossary.json:553`). The glossary gains (`terms`):

- **git identity** — *Git 身份*: who Daoris's git acts as on a code host: a host, a way (an SSH key or an HTTPS sign-in), and a
  label, with the name and email it may commit as; one set on this machine, which a repository's remote, a workspace's host or a
  host's default points at. Avoid *account* (账户, an agent's) and *profile*.
- **git access** — *Git 访问权限*: what Daoris's git and a session may do on a git remote: read, write and dangerous writes, each
  allowed, asked or denied. 访问 as in the sessions' access setup; 权限 because it is a set of permissions.
- **your own git sign-ins** — *你自己的 Git 登录*: the person's own SSH keys, git configuration and credential store, used by
  Daoris's git only where the person chose it.
- **git go-ahead** — the go-ahead term's sense widened: *go-ahead* (放行) gains *a session's ask for one exact git act on a remote*,
  kept on the session.

## 8. The terminal

The `daoris-driver git` family, each change one typed operation in the driver library called by its route and by the command alone
(D194 point 4), one text per verb, a refusal one line (`git: {sentence} ({code})`):

```
daoris-driver git identity list [--json]
daoris-driver git identity add-ssh <host> --label <l> [--name <n> --email <e>] [--import <file>] [--rsa]
daoris-driver git identity add-https <host> --label <l> [--name <n> --email <e>] [--token-stdin]
daoris-driver git identity public-key <identity>
daoris-driver git identity test <identity> [--trust-host <fingerprint>]
daoris-driver git identity rename <identity> <label>
daoris-driver git identity commits-as <identity> --name <n> --email <e> | --clear
daoris-driver git identity read-only <identity> on|off
daoris-driver git identity remove <identity>
daoris-driver git default <host> <identity>|--clear
daoris-driver git use <repository> [--remote <name>] <identity>|own|--clear
daoris-driver git use --workspace <w> --host <host> <identity>|own|--clear
daoris-driver git use --machine --host <host> own|--clear
daoris-driver git access [<repository>|--workspace <w>|--machine] [read=<v>] [write=<v>] [dangerous=<v>] [--clear]
daoris-driver git access --session <id> [read=<v>] [write=<v>] [dangerous=<v>]     # narrowing only
daoris-driver git held [--json]
daoris-driver git try-again <repository> [--remote <name>]
```

- **A token is read from stdin**, never an argument, as `agent key` reads one (D67). `add-https` without `--token-stdin` starts GCM's
  browser sign-in, which is the person's own act at the terminal.
- **Confirmed in the window first** (D194 point 5, D156): with the window running, a terminal's change is a press the window confirms,
  and the act runs only after the confirmation: a sign-in, an import, a host trusted or an identity removed starts nothing before it.
  **The widenings**, which the confirmation names as such and which the page asks in its own `InlineConfirm` too: any class loosened
  at any level, or a level cleared where the level above is looser; choosing `own` for any scope; an import (it reads a file of the
  person's); trusting a changed host key; turning `read-only` off. A new identity, a test, a rename, a default and a narrowing are not
  widenings.
- **From a session's environment** a person's press is refused (D194 point 5): an agent cannot run these.

## 9. Ask Daoris

- **What it may propose**: a `setting` door **`git-use`** (a repository's remote or a workspace's host pointed at an existing
  identity, or cleared; never `own`), the person applying it, in every place `.claude/knowledge/setting-doors.md` lists (the driver's
  list and case, the service's list, shape check and tool words, the coverage row, the room's row and goldens); and, from its sync
  card where a fetch failed or a remote is held (`HelpSyncProposals.cs:284` already carries the reach), a **screen door** to *Add a
  git identity* with the host filled.
- **What it never proposes or does**: create, import, test, rename or remove an identity; enter or see a key, a token or a passphrase;
  choose `own`; change git access (as D184 keeps `SET_ACCESS` out of every proposal); answer a git go-ahead. The facts it is handed are
  the identities' records (§3.1) and the holds' classes, which hold no secret by construction.
- **Coverage** (`HelpCoverageTests`): the Git place's hooks and the repository's and workspace's rows are each a door (`useSetGitUse` →
  `git-use`) or exempt with their reason (the person's own: identities, access, `own`, trust); the git go-ahead's answering hook is
  exempt in `Starts`, a person's own press.

## 10. Security

**The residuals, said plainly.**

- **A session runs as the person's Windows account.** Its agent can read any file that account can: Daoris's key files under
  `profiles/git/keys/`, the person's own `~/.ssh`, and it can ask GCM for a token in either namespace (`git credential fill`). The
  launcher, the relay, the rules and the instruction govern an agent's ordinary git, including forms no rule names; they are not a
  sandbox against an agent set on getting round them. An agent can unset `GIT_SSH_COMMAND` or the command scope's variables, pass its
  own `-c url…`, use the person's own key with a plain `ssh`, or push to an address the doors do not reach (§6.5).
- **The only bound no local process lifts is the host's**: an identity the host grants reading alone. The Git place offers *read
  only on the host* and says that it is the hard form of Write *deny*.
- **A key handed to a process environment would be readable by the agent**: so no key, path to a key or token is ever put in an
  environment; the launcher finds the key itself. The key file stays readable by the account (above).
- **An address that carries a password** is refused for a remote not chosen `own` (`transfer.credentialsInUrl=die`, §3.3); under `own`
  git uses it as written. Daoris never shows or logs an address's user-info part.
- **A grant file can be written by any process of the account**, as every local record can. Grants are bounded by their act, their
  refs and their minutes.

**One boundary for words that may hold a secret.** A shared redaction (`GitWords.Redact`, beside `WiringRedact.Scrub`) runs over
every text from git, ssh, GCM or a plugin before Daoris keeps, says or answers it: an address's user-info, an `Authorization` or
`Cookie` header, the sensitive gate's token shapes (with `github_pat_` and `glpat-` added), and a private key block. It covers
today's paths (`HelpSyncProposals.cs:243-247`'s kept `fetch`, `SyncWords`, `LandingSyncWords`, the start's LANDSYNC2 sentence, a
landing plugin's message as the driver says and records it, `LandingPlugins.cs:113-114`) and every new one (the launcher's and the
relay's lines, the Git place's test answers). Children run with `GIT_TRACE_REDACT=1` and without the curl tracing variables (§4.1).

**What is never written anywhere but its home** (§3.4): a private key, a token or a passphrase never goes into a log line, a
machine-log field, an event, a record, a route's answer, a bridge payload back to the page, Ask Daoris's facts, an instruction, a
tracked file or a commit message. A public key goes only where the person copies it. A host's answer is reduced to its class and,
for a test, the user it names. The sensitive gate gains the two token shapes above, so a repository using it catches one committed by
mistake.

## 11. The machine log

Coded lines, each with the door (`daoris`, `landing`, `session`, `press`, `terminal`), never a secret, an address's user-info or a
path beyond the repository's name, never an identity's user or email (its id stands for it):

| Event | Fields | When |
|---|---|---|
| `git.refused` | repository, remote, host, class, code (`denied`, `no-identity`, `own-input`, `refused`, `write-refused`, `host-unknown`, `host-changed`, `read-only`, `unreached`), door, session | each refusal by the judge or the doors |
| `git.asked` | repository, remote, class, door, session, how (`held`, `git-ask`) | each ask raised |
| `git.granted` / `git.grant.used` | repository, remote, class, session, by (`held-call`, `git-ask`, `standing`, `press`) | a grant written, and consumed |
| `git.held` / `git.lifted` | repository, remote, holds (`all`, `writes`), class, why | §5.2 |
| `git.identity` | identity id, host, way, act (`added`, `imported`, `tested`, `removed`, `default`), result code | each act on an identity |
| `git.wired` | count of identities, of pointers, git version, whether GCM was found | each start of the driver and each change of the pointers |

## 12. Considered and rejected

- **Daoris's git using the person's credentials by default** (today's ambient arrangement): it is the person's sign-in used without
  their say, as D125, D175 and D197 refuse for agents, and it is what failed (§0.4).
- **Setting `HOME` for sessions** to a folder of Daoris's: every tool a session runs reads `HOME` (npm, the agent's own caches), and
  moving it changes far more than git. Git's own variables reach git alone.
- **Daoris's keys in the person's `~/.ssh` or agent, or `Host` aliases in the person's `~/.ssh/config`**: writes the person's own
  configuration (D121 §2.5's rule).
- **An ssh agent of Daoris's own**: Windows' OpenSSH agent is one service per user, holding the person's keys; a private agent would
  need a socket per Daoris run that git's ssh and Windows' ssh reach alike. Not measured, and a key file with `IdentitiesOnly` does the
  same with nothing running.
- **DPAPI-sealed keys, or a passphrase**: §3.4.
- **Trust a host key on first use, in the background**: §3.5; D73's rule.
- **`includeIf` per repository, or one machine-wide file of per-address usernames**: a path rule misses a session's tree, and one
  file cannot give two repositories at one address two identities (§4.1).
- **A `pre-push` hook, a refusing credential helper, or the harness's rules alone as the judge**: §6.5.
- **Judging Dangerous by the flags typed**: the push's commands carry none; it is judged by what the update does (§6.1).
- **Doors installed only where access asks or denies**: a tightening could not reach a running session (§6.5).
- **Git access as pauses inside `access`, or a field on `SET_ACCESS`**: full access would open them, and an older host would clear the
  access setup (§6.4).
- **Write *ask* as Daoris's default for sessions**: `deny` to `ask` is a widening (D72); the default keeps today's two behaviours
  (§6.2).
- **A level's Dangerous written *ask* for a landing that pushes to the line**: it would let sessions ask to force or delete; the landing
  rule's standing yes covers that one act (§6.7).
- **D135's go-aheads as the transport's permission**: matched by words and shared across the ask's sessions, and absent where no ask
  is (§0.5); a git go-ahead is exact and the session's (§6.6).
- **A list of identities per scope**: §3.2.
- **An agent narrowing its own access through a tool**: there is nothing it could narrow that its instruction does not already let it
  decline to do.
- **Ask Daoris setting up an identity**: it would carry a secret or start a sign-in on the person's behalf (§9).
- **Applying *Keep using my own sign-ins* at the move without the person's press**: it is a sign-in's use the person has not chosen
  (D191 point 3 keeps a sign-in the owner's).
- **Signing `gh` and `az` in with the git identity**: their sign-ins stay theirs (D121 §2.8); a landing on GitHub may push as Daoris's
  git identity and open its pull request as `gh`'s account. The Git place says which `gh` account is signed in beside a github.com
  identity (read through `gh auth status`, no token). Handing `gh` a token is left for later.

## 13. What this amends, once accepted

- **D121**: §2.5's precedence, for authentication alone: the command scope and `GIT_SSH_COMMAND`, rejected there because they override a
  repository's own configuration, now carry Daoris's identity and the doors (§4.1), and a repository's own authentication is honoured
  under `own`; §2.5's file moves to `profiles/git/` and holds no identity; §2.6's terminal keeps the person's own git (§4.4). TOOLS6 is
  built inside GITAUTH1d.
- **D72**: the `no-push` default's rules and classifier sentence are composed from git access (§6.8); its id stays.
- **D184**: git access joins the access setup in keys and a route of its own, untouched by full access (§6.4); the hold of a permission
  request widens from the asking posture's mark to git access's rules, and keeps the call's raw input for them (§6.6).
- **D37 and D87**: "Daoris pushes nothing" stands (the plugin pushes); the landing asks the judge before it starts the plugin and grants
  exactly its push (§6.7).
- **D135**: unchanged; a git go-ahead is its own kind, kept on the session (§6.6), and the glossary's *go-ahead* widens to name it.
- **D172**: `profiles/git/` (keys, `ssh_config`, `known_hosts`, the git file) and `data/git-identities.json` join the home's table, in
  the three home twins; `run/git/` (held, grants, used) joins `run/`.

## 14. The build, in order

Each row a feature a person can see working, with its proof. The contracts every later row stands on are proved first (1b). Lanes as
`daoris.lanes.json` places them; the parent chooses at dispatch.

1. **GITAUTH1a — a remote that cannot authenticate is said once and left alone.** The failure classes of §5.1 that today's ambient
   credentials meet (`refused`, `write-refused`, `host-unknown`, `host-changed`, `unreachable`, `unclassed`), measured against git and
   OpenSSH; the hold and its keys (§5.2), lifted by **Try again**, a changed address, or a change of the git Tools runs; the moments reading
   their fetch's answer; one *What needs you* row per held group (scoped, worded, counted, its hook's coverage); §4.5's variables for
   every child but the launcher's `BatchMode`, which 1b adds; the redaction boundary over today's paths (§10); the session's Git section
   for a held remote (§5.3). Its promise, stated: nothing Daoris starts prompts through git or GCM, and an ssh started with no console
   cannot; no Daoris act repeats a held fetch; a session is told not to try. *Proof*: a fetch against a local SSH server refusing the
   key, held once, with no second connection across a minute of moments; a session's instruction; the row in en and zh; the redaction
   table; on the install, the owner's SSH remote said once (screenshot).
2. **GITAUTH1b — the doors, judging the default.** The judge with Daoris's default (§6.2), the launcher (§4.2) and the HTTPS relay
   (§6.5) installed for every process serving a repository, with today's ambient credentials passed through unchanged; the push's
   commands read on both transports; the landing's exact grant (§6.7); the coded lines (§11). Visible: a session's force push, branch
   deletion or tag push refused at the transport in any form and on any harness, with one line; a landing still pushes. *Proof first*:
   real-git tables against local servers (an sshd and an HTTP server over `git http-backend`) for each update kind (creation,
   fast-forward, a forced fast-forward, non-fast-forward with the old commit held and not held, deletion, tag, the line, several refs
   in one push, a lease, an explicit push address, a more specific rewrite), the batch refused before any ref moves, a grant consumed
   once across two concurrent pushes; a repository's own hooks still running; the person's terminal unwired.
3. **GITAUTH1c — git access, set by the person.** The keys and their resolution in `AccessRules`, `SET_GIT_ACCESS` and `git access`
   with their widenings confirmed, the record and *Tighten…*, the rows on Setup, Driver and the session's head (§7.3, §7.5), the rules
   and classifier sentence written from it (§6.8, the `no-push` fold), the hold widened and its exact grant (§6.6), `git_ask` and the
   git go-ahead in every person-waits place, the older build's notice. *Proof*: the resolution table at each level with the default's
   two doors; an older build's read of the file; a session's push asked by a held call and allowed, and by `git_ask` on a Codex
   session; a tightening reaching a running session's next push; en and zh; the window.
4. **GITAUTH1d — SSH identities, from the terminal.** `data/git-identities.json`, `profiles/git/` with its access set before any key
   byte, generate and import, the launcher handing the identity, Daoris's `ssh_config` and `known_hosts` and the host-key trust (§3.5),
   Daoris's git file and the command scope (§4.1; TOOLS6), the pointers and their resolution (§3.2), `own` and the own-input hold
   (§3.3), the author rule (§3.6), the `git identity`, `git default` and `git use` verbs. A remote no identity serves keeps today's
   ambient credentials until 1e. *Proof*: tables for the file, the folder's access, the launcher's arguments and refusals, the
   resolution (each step, a gone identity, a host mismatch, `own`), the command scope per repository (two repositories at one address);
   a fetch and a push through the launcher against a local sshd with a generated key; the sensitive gate's rows. *Owner-permitted*: a
   key added on a real host and a real fetch.
5. **GITAUTH1e — Settings → Git, the remote rows, and the move.** §7.1, §7.2's SSH steps, §7.3's remote half, the move off ambient
   credentials with its row (§6.8), Ask Daoris's `git-use` door and the sync card's screen door, the glossary's terms and
   `settings.git.json` in en and zh. *Proof*: stories per state at wide, half and narrow, en and zh, both themes, and R1's rendered
   check; `i18n:check` and `names:check -- --strict`; the move row once on a home with ambient-authenticated repositories; the
   setting-doors places; the window.
6. **GITAUTH1f — HTTPS identities through GCM.** The GCM version and commands this relies on fixed against a real GCM (store in
   Daoris's namespace and the Windows store, erase, per-address user and namespace, non-interactive refusal, a provider's browser
   sign-in, output free of tokens); GCM managed if MinGit carries none; the sign-in's phases and a pasted token; remove as erase.
   *Proof*: a real-process test against a real GCM with a local HTTP server and a pasted test token in a scratch Windows user's store
   namespace, erased after; the redaction table over GCM's output. *Owner-permitted*: a real browser sign-in.
7. **GITAUTH1g — the install, seen** (owner). One SSH identity and one HTTPS identity on real hosts; a held remote set up and lifted; a
   repository that authenticated by its own configuration moved to `own` and still fetching; a session's push asked and allowed; a
   force refused; the person's terminal and a repository's hooks unchanged; screenshots of each surface in en and zh.

## 15. What this design's gate does not cover

Written from the code; nothing was run, no key was generated, no sign-in started, and the person's `~/.ssh` and credential store were
not read. Every behaviour of git, OpenSSH and GCM here is from their documentation and owed a measurement by the row that needs it:
git's prompt order and the empty `GIT_ASKPASS` (1a); OpenSSH's and git's failure words (1a); the receive-pack command stream, the
remote-helper conversation and `insteadOf` over an explicit push address (1b); `transfer.credentialsInUrl` and the per-address resets
(1d); Windows OpenSSH's check of a key file's access under §3.4's folder (1d); which hosts take ed25519 (Azure DevOps is documented
as taking RSA keys; 1d reads the host's current documentation); GCM's per-address namespace and store (1f); whether an *ask* rule raises
a permission request under `bypassPermissions` and auto mode on each Claude door (1c); whether Codex's sandbox lets a session's git
reach the network at all on Windows (1c). Git LFS over HTTPS uploads objects past the relay (§6.5); its own judge is left for a row
once a repository needs it.

## 16. The reads (D191)

| Round | Read | By | At | Findings | Verdict |
|---|---|---|---|---|---|
| 1 | design and D198, round 1 | Codex (`codex-cli 0.162.0-alpha.17.2`, read-only) | `21c5e8441` | 38: 27 blocking, 11 non-blocking | all checked against the code; 37 taken in round 2, the smaller shape taken in part and answered in part (below) |

**Round 1, by heading** (Codex's numbering; the full answers are in D198's note):
- *Claims*: 1 (credentials are ambient, not proven the person's) taken, §0.2; 2 (a start is told a failed fetch,
  `TreeUpdates.cs:163-168`) taken, §0.3; 3 (the launcher bypass is conditional) taken, §0.1.
- *Credentials*: 1 (other authentication inputs) taken, §3.3; 2 (GCM's environment overrides) taken, §4.1; 3 (git's words are kept,
  `HelpSyncProposals.cs:243`, `land.mjs:66`) taken, §10's redaction; 4 (an address's password) taken, §3.3 and §10; 5 (DPAPI's real
  gain, access before bytes, recovery) taken, §3.4.
- *Wiring*: 1 (one file cannot be per repository) taken, §4.1's command scope per repository; 2 (precedence; `-c core.sshCommand` is no
  bypass) taken, §6.5 and §10; 3 (the launcher's contract) taken, §4.2; 4 (`own` could prompt; askpass) taken, §4.1, §4.5; 5 (holds keyed
  too broadly; 403) taken, §5.1-§5.2; 6 (`commitsAs` unwired) taken, §3.6, kept because the owner asked for the commit identity.
- *Git access*: 1 (no flags in receive-pack; creation) taken, §6.1, §6.5; 2 (the helper's `push` lines) taken, §6.5; 3 (push addresses
  the relay misses) taken, §6.5; 4 (tightening needs the doors always) taken, §6.5; 5 (a held call keeps no command) taken, §6.6; 6
  (go-aheads are not capabilities) taken, `git_ask`; 7 (the landing's grant) taken, §6.7; 8 (the defaults loosen) taken, §6.2, §6.8; 9
  (`SET_ACCESS` clears on an older host) taken, `SET_GIT_ACCESS`.
- *Decided rules*: 1 (D121 reversed) taken, §4.1 and §13; 2 (confirm before a sensitive act; no automatic `own`) taken, §8 and §6.8; 3
  (the integration places) taken, §7.4, §9, §14.
- *Window*: 1 (remedies by way) taken, §7.1, §7.4; 2 (R2's phases are the driver's) taken, §7.2; 3 (*git remote* exists; the chip)
  taken, §7.5, §7.6.
- *Rows*: 1-7 taken in §14's new order (the doors proved second, identities and the move together, GCM's lifecycle against a real
  GCM); 8 (the install's cases) taken, 1g.
- *A smaller shape*: taken in part (today's agent denial kept, exact landing grants, the doors before identities); answered in part:
  `commitsAs` stays, with its own rule (§3.6), since the owner asked for the commit identity.

**For the steward**, beside the reads: (1) Daoris's default is today's behaviour door by door (§6.2), so *the landing and a session can
never disagree* holds for every value the person sets, and under the default each keeps today's; whether that reading of the owner's
direction stands is the steward's to settle, or the owner's if it is a change of direction (D191 point 3); (2) Fable's read, since this
changes a foundation.

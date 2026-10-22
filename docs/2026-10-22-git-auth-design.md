# Git identities and git access: who Daoris's git acts as on a code host, and what it may do there (GITAUTH1)

**Status: proposed** (D198, 2026-10-22). Written by Claude Opus from the code at `75bb61805`; nothing here is built. Read by
Codex under D191 (§16). Changes a foundation (where credentials live and how every git Daoris starts is wired), so the steward
sends it to Fable before accepting it (D191's amendment).

Daoris's sessions reach git remotes (a fetch at a tree's start, a session's own `git fetch` or `git push`, a landing's push) and
fail, because nothing in Daoris sets up how its git signs in to a code host. This design gives Daoris its own **git
identities**, several per host, kept apart from the person's own sign-ins; hands each git Daoris starts exactly the identity its
remote needs; says once, in plain words, when none serves a remote, and stops trying; and, in the same place, sets **git
access**: what a repository, a workspace and a session may do on a remote (read, write, dangerous writes), judged by one judge
for Daoris's own landing and an agent's own `git push` alike.

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
- **Git for Windows' launcher is started past** (GITSTART1-5): the real git starts with the variables the launcher would set,
  among them `HOME` where unset, from `HOMEDRIVE` and `HOMEPATH`, else `USERPROFILE` (`GitForWindows.cs:70-96`), and
  `PLINK_PROTOCOL=ssh` where unset (`GitForWindows.cs:83`). The managed MinGit carries an ssh of its own, `usr/bin/ssh.exe`
  (`docs/2026-10-01-tools-resources-evidence.md:48-50`); whether it carries Git Credential Manager is not recorded.

### 0.2 What a session's git sees

- **A session inherits the desktop's environment.** `Spawning.Shell` starts every session, chat, intake and helper with the
  desktop's own environment, less the role variables, plus `DAORIS_REPOSITORY`, `DAORIS_SERVICE_URL` and the tools' `PATH`
  (`Adapters.cs:1497-1536`).
- **An account's home is the agent's configuration folder, not `HOME`.** The account is handed through the harness's own
  variable, `CLAUDE_CONFIG_DIR` or `CODEX_HOME` (`HarnessSelection.cs:566-579`; D175 point 1). Nothing sets `HOME`,
  `USERPROFILE` or any git variable for a session. So a session's git reads **the person's** `~/.gitconfig`, the system's
  configuration (Git for Windows' installer sets `credential.helper=manager` there by default), the person's `~/.ssh` (git's own ssh reads `$HOME/.ssh`;
  Windows' OpenSSH reads `%USERPROFILE%\.ssh` and the Windows agent) and Git Credential Manager's store under the person's
  namespace. Every credential a session's git uses today is the person's.
- **Nothing tells a session's git not to prompt.** Daoris's own fetch sets `GIT_TERMINAL_PROMPT=0` (`TreeRefs.cs:12-16`), as
  do a plugin's tool runs and trials (`PluginToolRuns.cs:40`, `PluginTrialProcess.cs:38`). A session's start does not, and nor
  does a landing plugin's process (`HookProcess.cs:85-111`). Whether Git Credential Manager then opens a window on the
  person's desktop for a session's or a plugin's HTTPS push is not measured.

### 0.3 Where Daoris and its sessions reach a remote, and what a failure does

| Who | Where | What a failure does today |
|---|---|---|
| Daoris's fetch of the line | `TreeRefs.FetchAsync` (`TreeRefs.cs:81-103`), "as the person, with their credentials" (`TreeRefs.cs:13-15`); one at a time per line (`RemoteFetches.OnceAsync`, `PullRequestMoments.cs:28-33`) | — |
| *Bring up to date* | `TreeSync` (`TreeSync.cs:112-115`) | said once per look, grouped, with what the git needs (WSR7; `SyncWords.cs:29-59`) |
| A landing's press, its line fetched first (LANDSYNC1) | `TreeLandSyncs.cs:94-97`, `TreeUpdates.cs:96, 164-167` | said on the press (`LandingSyncWords.NotFetched`) |
| A pull request's moments (FABRIC-END6) | `PullRequestMoments.AskDueAsync` fetches before asking the plugin (`PullRequestMoments.cs:463-467`) | **discarded**: the fetch's answer is never read; the moment fetches again at the next focus, ref move or push past the minute (`PullRequestMoments.cs:183-187`) |
| A landing's push | the plugin's own process: "Daoris pushes nothing and opens nothing" (`LandingPlugins.cs:8-10`; D37, D87); the GitHub plugin runs `git push --quiet origin <tip>:refs/heads/<branch>` (`examples/plugins/github-pull-request/land.mjs:79`) | the plugin's answer, `pushed: false` with its sentence |
| A session's own `git fetch`, `pull`, `clone`, `ls-remote` | the agent's shell, any form | the agent reads git's words and decides; nothing in its instruction speaks of fetching (`Adapters.cs:985-1002`) |
| A session's own `git push` | the agent's shell | refused on both Claude doors by the shipped `no-push` default, four forms (`Permissions.cs:152-167`), and told to auto mode's classifier as a hard denial (`Permissions.cs:166-167, 190-195`); Codex takes no rules (D184's AUTONOMY1b note) |

### 0.4 The loop the owner saw

- **Nothing remembers that a remote cannot be reached.** Each path above tries on its own schedule and forgets: the moments
  fetch again past each minute and say nothing (`PullRequestMoments.cs:463-467`); every press and every look fetches again and
  says it again; every new session's agent tries its own `git fetch`, since its instruction is silent on fetching.
- **Evidence, from the owner's own records** (machine-local, read 2026-10-22, quoted without names): on 2026-10-16 a work ask
  told its session that Daoris's git could not fetch origin over SSH an hour before, and to fetch first if it now worked. The
  session's answer: it tried `git fetch origin`, which failed with `Permission denied (password,publickey)` from an Azure DevOps
  SSH host, and it rebased onto the local line instead. The person carried the failure from one session to the next by hand.
- **The push side tries other forms.** UNBLOCK4 widened `no-push` from `git push …` to option-first forms because sessions
  pushed as `git -C . push` and `git -c <key>=<value> push` (`Permissions.cs:152-159`).
- **WSR7 measured the cause in 2026-10.** On the owner's workspace every fetch failed with *Could not read from remote
  repository*: the git on the path could not use the key the person's own Git client used (`docs/task-archive.md:8817-8825`;
  `docs/2026-10-01-tools-design.md:70-78`). WSR7 said it once per look; it could not make it work.

### 0.5 What is already decided that this rests on

- **D121 §2.5** (`docs/2026-10-01-tools-design.md:219-263`): Daoris's git settings travel in a global file named by
  `GIT_CONFIG_GLOBAL`, which **includes the person's own global configuration, unchanged, and never writes it**; Daoris's keys
  after the include; a repository's own configuration wins over both; the `ssh` choice is git's own, Windows', or a command the
  person names. `credential.helper` was to join "by a row with its reason once a real fetch needs it". This design is that row.
- **D125, D175, D197**: Daoris's sessions run on accounts Daoris holds, in folders of its own (D175 point 1); copying the
  person's credentials into a profile is rejected (D125; D175's rejections); the person's own sign-in is the machine's, never
  signed in, out or written by Daoris and never asked (D197 point 3); accounts are **one set** for every workspace, not lists per
  scope, because per-scope lists confused (D197 points 1 and 9).
- **D72, D74**: what an agent may do is the harness's own rules in Daoris's scopes, handed at spawn; `no-push` is a shipped
  default the person may switch off by id; an agent narrows its rules at once and widens only on the person's yes.
- **D73**: a trust is asked, then written, on a person's explicit act, and never silently. Host keys follow it (§3.5).
- **D135**: a go-ahead is asked once on the ask through `go_ahead_ask` (`kind`, `on`, `act`, `why`) and answered by the person
  alone; `push` is already one of its kinds (`GoAheadTests.cs:268-273`).
- **D184**: a repository's sessions run under an access setup set per repository, workspace and machine (`AccessRules.cs:64-81`),
  read as a stored shape where an unknown pause is passed over (`AccessRules.cs:74-76`); its defaults are today's behaviour; under
  `asking` the protocol door holds a tool's permission request for the person (AUTONOMY1b3a; `Acp.cs:1923`), shown as a card and
  in *What needs you* (AUTONOMY1b3c); full access never lifts the boundary (a push stays the landing's, AUTONOMY1c).
- **D172 point 3**: `data/` holds no secret; a file that holds a key lives in `profiles/` (`DaorisHome.cs:362-363`).
- **D67's key accounts**: an API key is plaintext at rest in `profiles/keys.json`, shown only by its last four characters, never
  over the bridge back; DPAPI was rejected there because the CLI twin could not write it
  (`docs/2026-09-23-api-key-accounts.md:24-31, 52-54`). The shell already seals the browser's kept sign-in with DPAPI
  (`DpapiSeal.cs:7-17`).
- **The machine log never holds a secret** (`docs/2026-09-30-machine-log-design.md:363-370`); the sensitive gate refuses a
  tracked private key block and GitHub's classic token shapes (`SensitiveGate.cs:76-81`), not GitHub's fine-grained
  `github_pat_` nor GitLab's `glpat-`.
- **The glossary**: *remote* (远端) is a Daoris deployment, "kept apart from a git remote (远程仓库)"; *push* is a thing "Daoris
  never does on its own"; *deny* (拒绝), allow (允许) and ask (询问) are the rules' words; *go-ahead* is 放行
  (`src/Daoris.Web/src/locales/glossary.json:485, 501, 546, 554`).

### 0.6 Corrections to the brief

- A session's `HOME` is not the account's: only the harness's own configuration variable is (§0.2). An account's folder holds no
  git configuration, key or credential.
- There is no `LandingPush`: the landing's push is the plugin's process (§0.3). The `PUSH` rows in `TASKS.md` are D182's pushed
  state, not git pushes.

## 1. The owner's direction

Stated, not quoted. Sessions keep trying to fetch and push and fail, because Daoris has no git authentication set up for them.
Daoris should handle it comprehensively: several accounts, SSH key management, learned from the git tools people already use,
with a designed screen. Then, widened: a policy for what a repository, a workspace and a session may do on a git remote, in three
classes (read, write, dangerous writes), each allowed, asked or denied, set per workspace and per repository with a default; a
session may tighten its repository's and never loosen it; it fits the permission and autonomy layers Daoris already has rather
than adding a parallel system; Daoris's own landing push and an agent's own push are judged by the same policy.

## 2. What existing tools do, and what Daoris takes

Summarised from each tool's own documentation; nothing was run on this machine.

| Tool | What it does | Daoris takes | Daoris rejects |
|---|---|---|---|
| GitHub Desktop | Accounts per host (github.com, one GitHub Enterprise); sign-in by browser (OAuth); hands its own git a credential helper that answers from its account | an account per host, signed in by the browser; the app's git answered by the app, not the person's helper | one account per host |
| Git Credential Manager (GCM) | Stores credentials in the OS store (Windows Credential Manager), keyed by URL and user; OAuth for GitHub, GitLab, Azure Repos and Bitbucket; several users per host, chosen by the URL's user or `credential.<url>.username`; `credential.namespace` / `GCM_NAMESPACE` separates one tool's entries from another's (default `git`); `credential.interactive` / `GCM_INTERACTIVE=never` stops every prompt and window | the OS store for HTTPS secrets; a namespace of Daoris's own, so its entries never mix with the person's; a user per URL to pick one of several accounts; never interactive in the background | Daoris storing a token in a file of its own |
| Fork, GitKraken, Tower | Profiles (a name, an email, and the host accounts beside them); SSH keys generated in the app or imported, per profile; a key chosen per host or per repository; the public key shown to copy, with a link to the host's key page | a profile is an identity: host, way, the name and email it commits as, a label; generate or import a key; copy the public key and open the host's page; choose per repository | a key the app keeps in the person's `~/.ssh` (GitKraken can); one global key for every host |
| VS Code's Git | Sets `GIT_ASKPASS` to a helper of its own, so a git started from the editor asks the editor for a credential, which the editor's account or a prompt answers | git asking Daoris rather than a terminal, through a program Daoris names | a prompt for each act: Daoris's git never prompts in the background (§4.5) |
| OpenSSH `~/.ssh/config` | `Host` aliases with `HostName`, `User`, `IdentityFile`; `IdentitiesOnly yes` offers only the named key; `ssh-agent` and Windows' OpenSSH agent hold keys; `UserKnownHostsFile`, `StrictHostKeyChecking`, `BatchMode` | a configuration file of Daoris's own, read with `-F`, so the person's is never read; `IdentitiesOnly yes` and `IdentityAgent none` so only the identity's key is offered; Daoris's own known hosts, checked strictly; `BatchMode yes` | an agent: Windows' is one per user and holds the person's keys; Daoris's keys in the person's file or agent |
| git `includeIf`, `url.<base>.insteadOf` | `includeIf "gitdir:…"` includes a file by where the repository is; `insteadOf` and `pushInsteadOf` rewrite a URL's start for every command, the second for pushes only | `pushInsteadOf` to send a push over HTTPS through a program of Daoris's (§6.5); D121 §2.5's include of the person's own global file | `includeIf` per repository for choosing an identity: a path rule per repository that a moved checkout or a session's tree under another folder silently misses; Daoris chooses by the remote's address instead (§4) |

## 3. The model: git identities

### 3.1 A git identity

A **git identity** (Git 身份) is who Daoris's git acts as on one code host. It has:

| Field | What it holds |
|---|---|
| `id` | stable, drawn at random (`gi-7f3a…`); never shown as the name |
| `label` | the person's name for it, unique on the machine (`Work`, `Personal`, `Org A deploy`) |
| `host` | the host it signs in to: `github.com`, `dev.azure.com` (its SSH host `ssh.dev.azure.com`), `gitlab.com`, or a self-hosted host named by its address, with a port where not the default |
| `way` | `ssh`: a key pair Daoris holds; or `https`: a sign-in kept by GCM in the OS store |
| `user` | the account on the host, as the host reports it at a test (`octo-work`), else as the person typed it |
| `commitsAs` | the name and email a commit made under it carries (`Work Name`, `work@example.invalid`) |
| `key` (ssh) | the key's type (`ed25519`, or `rsa-4096` for a host that takes RSA alone) and its SHA-256 fingerprint; never the key |
| `readOnly` | the person's statement that the host grants this identity reading alone (a read-only deploy key, a read-only token): Daoris then never tries a write with it (§6.3) |
| `test` | the last test's answer and when: `reached` (as which user), `refused`, `host-unknown`, `host-changed`, `unreachable` |
| `default` | whether it is its host's default |

`data/git-identities.json` keeps every field above (no secret: D172 point 3). Its writer is the driver, under the home's lease,
atomically, as every `data/` file.

### 3.2 One set, and which identity a remote uses

**The identities are one set on this machine.** No workspace or repository has a list of its own. Each scope only *points*:

1. The repository's choice for that remote (`origin`, `upstream`, … each its own).
2. Else the workspace's choice for that host.
3. Else the host's default.
4. Else none.

The answer names where it came from (*this repository*, *workspace Games*, *github.com's default*), as D184's access does.
A choice may name **Your own git sign-ins** (§3.3) in place of an identity.

**Why one set and pointers, not a list per scope.** D197 found lists per scope confusing: which list a start reads, what a list
excludes, what joining one moves. A git identity is a secret on a host; a copy per scope would be a second secret to rotate. With
one set, removing an identity shows every scope that pointed at it before it goes. And unlike an agent's accounts, which are
interchangeable for a start, an identity is not: the wrong one is *Repository not found* on a private repository. So the scope
must be able to choose, and a pointer is the least it can hold.

**Considered and not taken: an identity that names the owners it serves** (`github.com/org-a/`, GCM's per-URL username as the
rule). It is a second rule to reason about beside the pointers; a workspace's pointer covers the case it was for (a work
workspace on one owner), and a repository its pointer misses is chosen on its own row.

### 3.3 Your own git sign-ins: an explicit choice, never a fallback

Daoris's git **never** uses the person's own SSH keys, agent, `~/.ssh/config`, global git configuration's credential settings or
GCM's entries in the person's namespace, unless the person chooses **Your own git sign-ins** for a scope: the machine, a
workspace's host or a repository's remote. That choice is a widening (§8), confirmed in place, and shown on every row it
reaches with its own mark. Under it, the remote's git runs as the person's own: the `ssh` the person chose in Tools (git's own,
Windows', or a named one, D121 §2.5) with no Daoris option, and GCM in the person's namespace.

Daoris reads nothing of the person's own to set an identity up, with **one** exception the person starts: **importing a key
file** the person names (§7.2), which copies that one file into Daoris's own folder. Daoris never lists the person's `~/.ssh`,
never offers its keys, never reads the person's credential store and never writes either.

### 3.4 Secrets: where each lives

| Secret | Lives | Never |
|---|---|---|
| An SSH private key | `profiles/git/keys/<id>/key`, with its `key.pub` beside it, the folder's access limited to the person's Windows account (an ACL that removes inherited entries and grants the account alone; SYSTEM and Administrators keep their implicit reach) | in `data/`, a tracked file, the environment, a log line, an answer or an event; never with a passphrase Daoris would have to ask for in the background |
| An HTTPS sign-in (an OAuth token, a personal access token) | the OS store, through GCM, in Daoris's namespace (`credential.namespace=daoris`), keyed by the host's URL and the identity's `user` | in any file of Daoris's; read by Daoris at all: GCM hands it to git, and Daoris only asks GCM to store or erase it |
| A host's key | `profiles/git/known_hosts`, Daoris's own | the person's `known_hosts` |

**A key is plaintext at rest, as `profiles/keys.json`'s API keys are** (D67), protected by its folder's access alone. OpenSSH
needs a file it can read without asking. DPAPI sealing (the browser's seal, `DpapiSeal.cs`) was weighed and not taken: ssh needs
the key as a file for each connection, so a sealed key would be unsealed to disk for every fetch, and a process of the person's
account can unseal it as easily as it reads the file (§10). A passphrase is rejected for the same reason: nobody is there to type
it at a background fetch.

### 3.5 Host keys: asked, then written, never silently

Following D73. A host's key enters Daoris's `known_hosts` only at a person's press: **Test connection** on an identity, which
shows the key's fingerprint and, for a public host Daoris knows (github.com, gitlab.com, `ssh.dev.azure.com`), whether it
matches the fingerprint that host publishes, read from a list carried with the tool resources so it updates without a release
(`resources.json`, D121 §3). Every other connection runs with `StrictHostKeyChecking yes` against Daoris's file: a host never
tested fails as `host-unknown`, and one whose key changed fails as `host-changed`, said in danger ink (§7.4). Nothing is trusted
on first use in the background.

## 4. Wiring every git Daoris starts

### 4.1 Daoris's git file and the environment

TOOLS6 builds D121 §2.5's file, moved to `profiles/git/global.gitconfig` because it now names identities' wiring, and extended:

```ini
# Written by Daoris (D121, D198). Your own global git configuration is included first, unchanged;
# the lines between the markers are Daoris's, rewritten from tools.json and data/git-identities.json.
[include]
	path = <your XDG git config, if git would read one>
	path = <your ~/.gitconfig>
# daoris: begin
[credential "https://github.com/org-a/engine.git"]
	username = octo-work
# daoris: end
```

and every child's environment carries what must win over a repository's own configuration (git's command scope, read after every
file; git 2.31 and later):

| Variable | Value | Why it is here and not in the file |
|---|---|---|
| `GIT_CONFIG_GLOBAL` | Daoris's file above | D121 §2.5 |
| `GIT_SSH_COMMAND` | Daoris's launcher (§4.2), by its absolute path | git reads it before `core.sshCommand` at any scope, so a repository's own ssh command does not bypass it |
| `GIT_CONFIG_COUNT`, `GIT_CONFIG_KEY_n`, `GIT_CONFIG_VALUE_n` | `credential.helper` set empty (which empties the list every file built), then `credential.helper` naming GCM; `credential.namespace=daoris`; `credential.interactive=false`; `credential.guiPrompt=false`; and, where a push must be judged (§6.5), `url.daoris::https://<host>/.pushInsteadOf=https://<host>/` | a repository's own `credential.helper` would otherwise be added after the global file's reset and answer with the person's credentials |
| `GIT_TERMINAL_PROMPT` | `0` | nothing asks at a terminal nobody sees |
| `GCM_INTERACTIVE` | `never` | GCM opens no window in the background (it reads the variable and the setting both) |

A git older than 2.32 (`GIT_CONFIG_GLOBAL`'s floor; the command scope's variables need 2.31) refuses an identity's use, naming
the version, as D121 §2.5 refuses a git setting below 2.32.

### 4.2 SSH: Daoris's launcher

`GIT_SSH_COMMAND` names **`daoris-driver git ssh`**, the driver's program, which git starts as it would start ssh, with the host,
the port, the user and the remote command (`git-upload-pack '<path>'` to read, `git-receive-pack '<path>'` to write). It:

1. Reads which identity serves `<host>/<path>` for the repository its working folder belongs to (§3.2), from the files alone,
   taking no lease.
2. Asks the judge (§6.5) for the act; a refusal ends here with one line on stderr, which git prints, and a coded machine-log line.
3. Starts the `ssh` Tools names (git's own, Windows', a named one; D121 §2.5) with
   `-F profiles/git/ssh_config -i <key> -o IdentitiesOnly=yes -o IdentityAgent=none -o BatchMode=yes
   -o StrictHostKeyChecking=yes -o UserKnownHostsFile=profiles/git/known_hosts -o ForwardAgent=no`, and relays its streams. For
   a write it reads the push's ref updates as they pass (§6.5) before relaying them on.
4. Under **Your own git sign-ins**, starts the person's chosen ssh with none of these options, so it reads the person's
   configuration and agent as their own client would.
5. Records the identity's last use by touching `run/git/used/<id>`, read by the page as *last used*.

No key path or secret is put in any environment; the launcher finds the key from the identity.

### 4.3 HTTPS: GCM in Daoris's namespace

The helper is GCM, named by its path: the one beside the resolved git where it has one, else the system's. With
`credential.namespace=daoris`, GCM keeps Daoris's entries apart from the person's (`git:` stays theirs); the per-URL `username`
picks which of a host's identities answers. Where no GCM is found, an HTTPS identity cannot be added, said with the reason, and an
HTTPS remote is read anonymously (a public repository still answers). Whether the built-in MinGit carries GCM is measured first
(GITAUTH1d); if it does not, GCM joins the declared tools as a managed one (D121's resource list) in that row.

### 4.4 Who is wired

Every process the driver and the modules start that may run git: a driven session on either door, a chat, an intake, Ask
Daoris's helper, a reviewer, a brainstorm's pass, a hook, a landing plugin, a plugin's trial and tool run, and Daoris's own git
(`WorkingTree.GitAsync`) — D121 §2.4's list. **Not the terminal's shells**: the person's own shell in Daoris's panel runs the
person's own git, as any terminal on the machine does. This amends D121 §2.6's *the person's own `git fetch` in the terminal
behaves as Daoris's does* for identities: a person's command must not act as Daoris's identity, nor be judged by a policy written
for agents. Held by D121's source-reading test of every `new ProcessStartInfo`, extended with the variables above.

### 4.5 Nothing prompts

No git Daoris starts asks anyone anything: `GIT_TERMINAL_PROMPT=0`, `GCM_INTERACTIVE=never`, ssh's `BatchMode yes`. A sign-in
happens only at the person's press in the Git place (§7.2), the one place GCM is started interactive. A credential that is
missing or refused fails at once with git's words, which §5 classifies.

## 5. When no identity serves a remote, or the host refuses one

### 5.1 Failures, classified

The launcher, the judge and Daoris's own fetch class a remote act's failure by the transport's own words (a table measured in
GITAUTH1a, from OpenSSH's and git's documented messages):

| Class | Read from | Example |
|---|---|---|
| `no-identity` | the resolution (§3.2): nothing serves the remote, and it is SSH or a host that refused anonymous | decided before any connection |
| `refused` | ssh's `Permission denied (publickey…)`; git's `Authentication failed`, `could not read Username`, HTTP 401 or 403 | the host does not take this identity |
| `host-unknown`, `host-changed` | ssh's `Host key verification failed` against Daoris's file, told apart by whether the host has a line there | §3.5 |
| `denied`, `ask` | the judge (§6) | the policy |
| `unreachable` | a name not resolved, a refused or timed-out connection | the network |

### 5.2 Held until something changes, said once

- **`no-identity`, `refused`, `host-unknown` and `host-changed` hold the remote.** The pair (repository, remote) is kept in
  `run/git/held.json` with the class, since when and the identity tried. While held, no Daoris act connects to that remote: the
  moments skip it, a look lists it as held and judges against the last fetch (WSR7's rule), a press says it in place and does not
  connect. Nothing retries on a clock.
- **What lifts it**: a change to an identity, a pointer, a host key or the policy that touches the remote; a passing **Test
  connection**; or the person's **Try again** on the row. Each wakes the acts that skipped it once (an event, FABRIC-EVENT's
  shape, never a poll).
- **`unreachable` holds nothing**: the network comes back by itself, so today's minute (D148) bounds the next try.
- **Said once.** One row in *What needs you* per held remote group (§7.4): what is missing, where the person sets it, and a press
  that goes there. A session's or a press's words name the row rather than repeating its reason.

### 5.3 What a session is told

A session's instruction gains a **Git** section beside the boundary (`Adapters.cs:985-1002`), composed from the resolution and
the policy at its start:

> **Git.** `origin` is `github.com:org-a/engine`, reached as the identity *Work* over SSH. On it you may fetch and pull. A push
> asks the person first: ask a go-ahead (`go_ahead_ask`, kind `push`, on `origin`, the branch as its act) and end your turn;
> your accepted work is pushed by the landing, so a push of your own is rarely needed. Never force-push, delete a remote branch,
> push a tag or push to `main`: these are refused.

and where nothing serves the remote:

> **Git.** `origin` (`ssh.dev.azure.com`) has no git identity on this machine, so neither you nor Daoris can reach it. Do not
> fetch, pull or push it: work from the local `main`, and say in your closing note that origin was not fetched.

A carry-on or a resume after a change is told the change after its words, as D184's go-aheads are (`Continuations`).

## 6. Git access: what may be done on a remote

### 6.1 The classes and their values

| Class | Acts | Values |
|---|---|---|
| **Read** (读取) | fetch, pull, clone, ls-remote, a submodule's fetch | allow · ask · deny |
| **Write** (推送) | push of a branch, fast-forward | allow · ask · deny |
| **Dangerous** (危险推送) | force (`--force`, `--force-with-lease`, a `+` refspec, any update that is not a fast-forward), deleting a remote branch, pushing a tag, pushing to the repository's line (its protected branch, D87) | allow · ask · deny |

**Defaults** (Daoris's, below the machine): Read *allow*, Write *ask*, Dangerous *deny*. An act in two classes is judged by the
stricter.

**Against today.** Read *allow* is today's. Dangerous *deny* refuses what `no-push` refuses today. Write *ask* keeps the landing as
today (its push needs the person's yes, which their press or their landing rule gives, §6.7), and turns an agent's own push from
a refusal into a question to the person. That change is a session asking, never a push without the person, so nothing happens
without the person that did not before (D184 point 5's intent); it is §16's first question for the steward.

### 6.2 Levels, and a session's tightening

- **Per repository, per workspace, this machine, then Daoris's default**, each class resolved on its own with its source, as
  D184's pauses resolve (`AccessRules.cs:70-72`).
- **A session runs under the stricter of two**: what its repository's access resolved to at its start, kept on its record as D184
  keeps `access`; and what the repository's resolves to now. A loosening after the start does not reach a running session; a
  tightening reaches it at once.
- **The person may tighten a running session** from its head (§7.5) or the terminal (§8): the record's value narrows, through the
  record's existing access door (`POST /api/sessions/{id}/access`, D184's AUTONOMY1b2 note). Nothing loosens a session above its
  repository.
- **An agent changes no access**, its own included: there is nothing it could narrow that its instruction does not already let it
  decline to do, and a tool for it is one more door to hold.

### 6.3 Identity and access

**Identity is who git acts as; access is what it may do.** They resolve apart and meet at the judge:

- A remote with no identity (and not readable anonymously) **cannot act whatever its access says**: the judge answers
  `no-identity` before access is read.
- A `readOnly` identity makes Write and Dangerous `deny` for its remotes, whatever the access, since the host would refuse them.
  This is the one guarantee no process on the machine can lift (§10), and the Git place says so on its row.
- Access never chooses an identity, and an identity never widens access.

### 6.4 Where it is kept: the access setup, not a parallel system

Git access is part of D184's access setup:

- **Stored** in `driver.json` beside `access`, `workspaceAccess` and `machineAccess`, as `gitAccess` (by repository),
  `workspaceGitAccess` and `machineGitAccess`, each `{read, write, dangerous}` with any subset set. Separate keys, not pauses in
  the same objects, because a level of `access` may be the string `"full"` (`AccessRules.cs:18-22`) and **full access never
  touches git access**: full access opens the pauses, and a push is a boundary, not a pause (D184's AUTONOMY1c note). An older
  build reading the file passes the new keys over and keeps them (`AccessRules.cs:74-76`).
- **Resolved, said, edited and refused** by `AccessRules`, the one place the route, the terminal and a start ask; its doors
  `SET_ACCESS` (with a `git` field) and `daoris-driver access` (§8); its effective values on `LINES` with their sources.
- **Kept on the record** at each driven start beside `access` (machine-local, as `access` is), read back by `PriorSession`.
- **Shown** in the same *Sessions' access* section of Setup and Settings → Driver, and again in Settings → Git (§7).

### 6.5 One judge, and where it is asked

**The judge** is one function in the driver's domain (`GitAccessRules.Judge`): the repository (by the act's working folder, never
by a variable alone), the session where its record is named, the remote's host and path, the act's class and its ref updates, and
the grants (§6.6), answering *allow*, *ask* or *deny* with a code. It is asked at every place a remote act can pass:

| Where | What it sees | What can pass it | Chosen as |
|---|---|---|---|
| **Daoris's own acts**, in process: a fetch, a press, a moment, a landing before its plugin starts | everything: the act is Daoris's own | nothing | **yes**: the judge's first caller |
| **The harness's rules** (Claude Code's `Bash(…)` rules in Daoris's tier, D72) | the command as written | an alias, a script, a path to git, `sh -c`, Codex (no rules), and any form no pattern names | **yes, for the flow**: the policy writes the rules (deny → `deny` rules, ask → `ask` rules), so an ordinary `git push` meets the person as a held call (§6.6). Never the guarantee |
| **A `pre-push` hook** through a session's `core.hooksPath` | every ref update, over any transport | `git push --no-verify`; and `core.hooksPath` replaces the repository's own hooks, so every other hook needs a forwarder | **no**: skipped by one flag, and it would break a repository's own hooks (husky, lefthook) |
| **A credential helper that refuses** | the host and path only: git does not tell a helper whether it fetches or pushes | — | **no**: it cannot tell a read from a write |
| **Daoris's ssh launcher** (§4.2) | `git-upload-pack` or `git-receive-pack`, the host and path, and the push's ref updates in the stream | `-c core.sshCommand=…` together with a key of the person's own | **yes, for SSH**: one program both hands the identity and judges the act, unmoved by flags |
| **A push relay over HTTPS**: `pushInsteadOf` sends a push to `daoris::https://…`, a remote helper of Daoris's on the child's `PATH` (`git-remote-daoris`) that judges the helper protocol's `push` lines (a `+` is a force, an empty source a deletion, `refs/tags/` a tag, the line's ref the protected one; `option cas` a lease) and then relays the conversation to git's own `git remote-https` for the original address | every ref update of the push | a remote with an explicit `pushurl` (git ignores `pushInsteadOf` there), `-c url…` on the command line | **yes, for HTTPS writes**; reads over HTTPS pass the relay only where Read is not *allow* (`insteadOf`) |
| **An identity the host grants reading alone** (`readOnly`) | — | nothing on this machine | **yes, offered**: the hard bound, the person's to set up on the host |

**Reading a push's ref updates.** Over SSH, the first packet lines git sends `git-receive-pack` are its commands, `<old> <new>
<ref>`: a new of zeros is a deletion; `refs/tags/` a tag; the line's ref the protected branch; and a force where `<old>` is not an
ancestor of `<new>` in the local repository, or not held there at all (git sends such an update only when forced). Over HTTPS the
helper protocol says the same in its `push` lines. Both are measured against a real git before they are trusted (GITAUTH1g).

**Why the transport, and not the hook.** The hook is the conventional answer and the wrong one here: the commonest agent bypass
(`--no-verify`) skips it, and taking `core.hooksPath` would silence a repository's own hooks. The transport is where git must pass
to reach the host at all, and Daoris already has to stand there to hand the identity. The rules stay because they turn an ordinary
push into a question the person answers in the flow, before git starts.

### 6.6 An ask: a held call, or a go-ahead

An `ask` becomes a wait the person answers, through what already exists (`.claude/knowledge/person-waits.md`):

- **On the protocol door with Claude**, the policy's `ask` rule raises a permission request. Daoris holds it for the person
  **whatever the session's posture**, where the request's command matches a rule the git access wrote (the hold of D184's
  AUTONOMY1b3a, widened from the asking posture's mark to git access's own rules). The card and its *What needs you* row say what
  is asked in git's words (*Push `daoris/fix-ranking` to `origin`*, the class *Write*). *Allow once* answers the agent and writes a
  **grant** the transport door consumes: this session, this remote, this class, one act, within the hold's bound.
- **Everywhere else** (the native door, Codex, a command no rule matched, a session under unrestricted where the harness may not
  ask), the transport door refuses with one line: *this push asks the person first: ask a go-ahead (`go_ahead_ask`, kind `push`,
  on `origin`, act `daoris/fix-ranking`) and end your turn.* The person's approval of that go-ahead (D135) writes the same grant.
- **Grants** live in `run/git/grants/`, one file each (session, repository, remote, class, the ref where named, until), consumed
  by the door's judgement that uses them; a restart drops them, as a held request ends with its agent.
- **Daoris's own fetch under Read *ask*** fetches only at a person's press (the press is the yes); its moments and looks skip the
  remote and the repository's row says so once.

### 6.7 Daoris's landing, under the same judge

The landing asks the judge before it starts its plugin, for the push the frame describes (`<tip>:refs/heads/<branch>` to
`origin`, `LandingFrame`, `Landing.cs:334-343`), and writes a grant for exactly that ref, which the door matches when the plugin's
own `git push` passes it. So a landing and an agent's push meet one judge, one policy and one door, and can never disagree:

- **Write *deny*** refuses the landing before its plugin starts: *this repository's git access denies writes to `origin`*, with
  the press to its row; a merge landing, which pushes nothing, lands as before.
- **Write *ask*** is answered by the person's yes: the *Accept…* press is it; and a landing rule that accepts automatically (D145,
  or D184's `landing: rule`) is the person's **standing yes for exactly that push**, which the person set on the rule. No other act
  can use it.
- **Dangerous**: a landing whose branch is the line pushes to the protected branch. Under *deny* it is refused; under *ask* its
  standing yes answers it. A landing never forces.

### 6.8 Moving from today

- **`no-push`** stops being a fixed rule list: its rules are written from the policy at each spawn (Write *deny* or *ask*: the four
  push forms as `deny` or `ask`; Dangerous: the force, delete and tag forms). Its id stays, so a machine whose person switched it
  off (`DefaultsOff`) reads, once, as this machine's Write *allow*, said in the access section's first notice; switching it back on
  clears that.
- **A landing rule whose branch is the line** keeps landing: at the first read, its repository's Dangerous is written *ask* (so its
  standing yes answers it), and said once.
- **Every remote moves off the person's credentials** (§3.3). At the first start of the build that wires identities (GITAUTH1e),
  one row in *What needs you* says it: *Daoris's git no longer uses your own git sign-ins. 29 repositories' git remotes on 2 hosts
  have no git identity yet.* with two presses: **Set up a git identity…** and **Keep using my own sign-ins…** (an inline
  confirmation naming what it lets Daoris's git use, applied at this machine). Until one is pressed, those remotes are held
  (§5.2). Whether the owner wants the second pre-applied is §16's second question.

## 7. The window

Parts from the UI/UX design (D185 §4): `Card`, `SettingList` and `SettingRow` (R1's label track), `FormGrid`, `Segmented`,
`SelectField`, `Pill`, `Dot`, `CodeText`, `PathText`, `InlineConfirm` (its warning tone for a widening), `Refused` (R3),
`PhaseList` (R2), `Inheritable`, `EmptyState`. A refusal is said under the press that started it (R3).

### 7.1 Settings → Git

A machine domain after *Tools* (`settings/domains.ts`): `{ id: 'git', label: 'settings.domain.git', machine: true }`, so a browser
is offered none. Three cards.

```
 wide, Settings → Git
┌──────────────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ Git identities                                                                       [+ Add a git identity…] │
│ ─────────────────────────────────────────────────────────────────────────────────────────────────────────── │
│ Work                github.com · SSH ed25519 · SHA256:q3Vf…               ★ Default for github.com           │
│                     commits as Work Name <work@example.invalid>                                              │
│                     ● Reached as octo-work 2 min ago · last used 5 min ago · 12 repositories    [Test] ⋯    │
│ Personal            github.com · HTTPS · signed in through Git Credential Manager                            │
│                     commits as A Person <me@example.invalid>                                                 │
│                     ● Reached as octo-me yesterday · last used yesterday · 1 workspace          [Test] ⋯    │
│ Org A deploy        ssh.dev.azure.com · SSH rsa-4096 · SHA256:Zk1p… · read only on the host                  │
│                     ✕ Refused 1 h ago: the host does not take this key           [Copy public key] [Open key page] │
├──────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ Defaults by host                                                                                              │
│ github.com          [Work ▾]                          set here · daoris-driver git default github.com Work      │
│ ssh.dev.azure.com   [Org A deploy ▾]                                                                          │
├──────────────────────────────────────────────────────────────────────────────────────────────────────────────┤
│ Git access on this machine                                                                                    │
│ Read  allow ↑   Write  ask ↑   Dangerous  deny ↑                  Daoris's default            [Change…]      │
│ Your own git sign-ins   not used                                                              [Use for…]     │
└──────────────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

- **An identity's row**: its label; host, way, key type and fingerprint (SSH) or *signed in through Git Credential Manager*
  (HTTPS); *read only on the host* where `readOnly`; *commits as*; the test's mark and words (*Reached as {user} {time}*,
  *Refused*, *Host not yet trusted*, *Host key changed*, *Not tested*), last used, and how many repositories and workspaces point
  at it. Acts: **Test**; **Copy public key** and **Open {host}'s key page** (SSH); **Set as default for {host}**; under ⋯
  **Rename…**, **Commits as…**, **Read only on the host** (a switch), **Remove…**.
- **Remove…** opens an inline confirmation listing every scope pointing at it, which fall to the next step of §3.2; it deletes the
  key folder (SSH) or asks GCM to erase the entry (HTTPS).
- **Empty**: *No git identities yet. Daoris's git reaches a private repository only as an identity you set up here.* and the add
  press.
- **Git access on this machine**: the machine level's three classes (§7.3's row), and **Your own git sign-ins** with where it is
  used and **Use for…** (this machine, or a workspace's host).

### 7.2 Add a git identity

A form in the pane (`FormGrid`), never a drawer, in steps, each step's refusal under its press:

1. **Where**: host (`github.com`, `dev.azure.com`, `gitlab.com`, *Another host…* with its address), and a label.
2. **How**: *SSH key* or *HTTPS sign-in*. The host's usual way first: SSH for `dev.azure.com`'s and a self-hosted host's SSH
   remotes, HTTPS where the repositories pointing at the host use HTTPS.
3. **SSH**: *Generate a new key* (ed25519; RSA 4096 where the host takes RSA alone, said with the reason) or *Import a key file…*
   (a file picker; the one read of the person's own, §3.3; a key with a passphrase is refused, saying why). Then the public key in
   a `MonoWell` with **Copy**, and **Open {host}'s SSH key page** (github.com's, gitlab.com's, Azure DevOps' for its organisation),
   or for another host *add it where that host takes SSH keys*.
4. **HTTPS**: **Sign in through your browser** (GCM's provider for the host, as its own sign-in; the phases are R2's, `PhaseList`:
   *Starting Git Credential Manager* → *Waiting on your browser* → *Checking who signed in* → *Saving*), or **Paste a token…** (a
   password field; Daoris hands it to GCM to store and keeps nothing).
5. **Commits as**: name and email.
6. **Test connection**: the cheapest read the host answers, `git ls-remote` of a repository pointing at it where there is one,
   else the host's own greeting (github.com and gitlab.com name the account). At first contact it shows the host's key fingerprint
   and whether it matches the host's published one, with **Trust this host** (§3.5). The identity is saved at step 3 or 4; a
   failed test leaves it with its mark and the act that fixes it.

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
│ Git access   Read allow ↑  Write ask ↑  Dangerous deny •      │
│              ↑ from workspace Games · • set here   [Change…]  │
└───────────────────────────────────────────────────────────────┘
```

- **Each remote** says its address (host and path; a user, password or token in the address is never shown, §10), the identity it
  resolves to with its source, and **Change…**: the host's identities, *Your own git sign-ins*, *As the workspace says* (clear).
  A remote nothing serves says *No git identity* in the danger ink with **Set one up…**.
- **Git access** is one compact row, per D185 R1: the three classes, each its value and a mark for where it came from (↑ from
  above, named in its tip; • set here), and **Change…**, which opens three `Segmented` controls (allow / ask / deny) under the row,
  each with *Clear* back to the level above (`Inheritable`). A change that loosens any class opens `InlineConfirm` in the warning
  tone naming what it lets happen (*Sessions may push to origin without asking you*); tightening asks nothing.
- **Workspace → Setup**, a section **Git**: a row per host its repositories reach, its identity choice for that host
  (**Change…**), and its git access row.
- **Settings → Driver's access card** gains the machine's git access row too, the same rows at three levels as D184's pauses.

### 7.4 What needs you

Each a row with its act, judged in .NET (`NeedsAPerson`), counted where it holds work (`.claude/knowledge/person-waits.md`):

| Kind | Says | Acts | Holds work |
|---|---|---|---|
| `git.identity.missing` | *{n} git remotes on {host} have no git identity* (the repositories in its detail) | **Set one up…** (Settings → Git, host filled), **Choose…** (the first repository's row) | where a session or a landing waits on it |
| `git.identity.refused` | *{host} refused {identity}* | **Copy public key**, **Open key page**, **Test again** | as above |
| `git.host.unknown` / `git.host.changed` | *{host} is not yet trusted* / *{host}'s key changed since you trusted it* (danger ink) | **Compare and trust…** (the identity's test) | as above |
| `git.own.moved` | §6.8's one-time row | **Set up a git identity…**, **Keep using my own sign-ins…** | no |
| a held call (`permission`) and a go-ahead (`goAhead`) for git access's *ask* | the existing kinds, worded from git's act and class | the existing presses | yes |

### 7.5 A session's head

*Details* lists the session's git access as it started and each remote's identity label (D185 §3.3's `AccessFact`, extended), with
**Tighten…** (the three classes, narrowing only). Nothing on the facts line unless a class is *allow* above Daoris's default, which
wears the plain warning chip D185 gives full access, *Pushes freely*.

### 7.6 Words, both languages, and the glossary

Chrome translates; an identity's label, a host, a user and an email are content and stay as written. A new catalogue
`settings.git.json` (en, zh), and rows in `work.attention.json`, `work.wait.json` and `note.json` for the codes. Some of the keys:

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
| `settings.access.git` | Git access | Git 访问权限 |
| `settings.access.git.read` | Read | 读取 |
| `settings.access.git.write` | Write | 推送 |
| `settings.access.git.dangerous` | Dangerous | 危险推送 |

The values are the rules' allow (允许), ask (询问) and deny (拒绝). The glossary gains (`terms`):

- **git identity** — *Git 身份*: who Daoris's git acts as on a code host: a host, a way (an SSH key or an HTTPS sign-in), the name
  and email it commits as, and a label; one set on this machine, which a repository, a workspace or a host's default points at.
  Avoid *account* (账户, an agent's) and *profile*.
- **git remote** — *远程仓库*: a repository's remote in git's sense (`origin`); never *remote* (远端) alone, which is a Daoris
  deployment.
- **git access** — *Git 访问权限*: what Daoris's git and a session may do on a git remote: read, write and dangerous writes, each
  allowed, asked or denied. 访问 as in the sessions' access setup; 权限 because it is a set of permissions, as *full access*'s why
  keeps 访问 for a mode.
- **your own git sign-ins** — *你自己的 Git 登录*: the person's own SSH keys, git configuration and credential store, used by
  Daoris's git only where the person chose it.

## 8. The terminal

The `daoris-driver git` family, each change one typed operation in the driver library called by its route and by the command
alone (D194 point 4), one text per verb, a refusal one line (`git: {sentence} ({code})`):

```
daoris-driver git identity list [--json]
daoris-driver git identity add-ssh <host> --label <l> --name <n> --email <e> [--import <file>] [--rsa]
daoris-driver git identity add-https <host> --label <l> --name <n> --email <e> [--token-stdin]
daoris-driver git identity public-key <identity>
daoris-driver git identity test <identity> [--trust-host <fingerprint>]
daoris-driver git identity rename <identity> <label>
daoris-driver git identity commits-as <identity> --name <n> --email <e>
daoris-driver git identity read-only <identity> on|off
daoris-driver git identity remove <identity>
daoris-driver git default <host> <identity>|--clear
daoris-driver git use <repository> [--remote <name>] <identity>|own|--clear
daoris-driver git use --workspace <w> --host <host> <identity>|own|--clear
daoris-driver git held [--json]            # what is held, §5.2
daoris-driver git try-again <repository> [--remote <name>]
daoris-driver access [<repository>|--workspace <w>|--machine] git-read=<v> git-write=<v> git-dangerous=<v>
daoris-driver access --session <id> git-write=deny …   # narrowing only
```

- **A token is read from stdin**, never an argument, as `agent key` reads one (D67). `add-https` without `--token-stdin` starts
  GCM's browser sign-in, which is the person's own act at the terminal.
- **Confirmed in the window** (D194 point 5, D156): with the window running, a terminal's change is a press the window confirms.
  **The widenings**, which the window's confirmation names as such and which the page asks in its own `InlineConfirm` too: any
  class loosened at any level, or a level cleared where the level above is looser; choosing **own** for any scope; **import**
  (it reads a file of the person's); **trusting a changed host key**; turning **read-only** off. Adding a new identity, a test,
  a rename, a default and a narrowing are not widenings.
- **From a session's environment** a person's press is refused (D194 point 5): an agent cannot run these.

## 9. Ask Daoris

- **What it may propose**: a `setting` door **`git-use`** (a repository's remote or a workspace's host pointed at an existing
  identity, or cleared), the person applying it (`.claude/knowledge/setting-doors.md`); and, from its sync card where a fetch
  failed (`HelpSyncProposals.cs:284` already carries the reach), a **screen door** to *Add a git identity* with the host filled.
- **What it never proposes or does**: create, import, test, rename or remove an identity; enter or see a key, a token or a
  passphrase; choose *Your own git sign-ins*; change git access (as D184 keeps `SET_ACCESS` out of every proposal). The facts it
  is handed are the identities' records (§3.1), which hold no secret by construction.
- **Coverage** (`HelpCoverageTests`): the Git place's hooks and the repository's and workspace's rows are each a door
  (`useSetGitUse` → `git-use`) or exempt with their reason (the person's own: identities, access, own sign-ins, trust).

## 10. Security

**The residuals, said plainly.**

- **A session runs as the person's Windows account.** Its agent can read any file that account can: Daoris's key files under
  `profiles/git/keys/`, the person's own `~/.ssh`, and it can ask GCM for a token in either namespace (`git credential fill`).
  Daoris's launcher, relay, rules and instruction govern an agent's ordinary git, including the forms no rule names; they are not
  a sandbox against an agent set on getting round them. An agent can unset `GIT_SSH_COMMAND` or the command-scope variables, use
  the person's own key with a plain `ssh`, or name a push URL Daoris does not rewrite.
- **The only bound no local process lifts is the host's**: an identity the host grants reading alone (a read-only deploy key, a
  token scoped to read). The Git place offers *read only on the host* and says that it is the hard form of Write *deny*.
- **A key handed to a process environment would be readable by the agent**: so no key, path to a key or token is ever put in an
  environment; the launcher finds the key itself. The key file remains readable by the account (above).
- **An address that carries a credential** (`https://user:token@host/…`, written by the person or a tool into a repository's
  configuration) is used by git as written, whatever Daoris hands it. Daoris never shows or logs the user-info part, and the
  remote's row says *this address carries a credential, which Daoris's identity does not replace*.
- **A grant file can be written by any process of the account**, as every local record can. Grants are bounded by their act, their
  ref and their minutes.

**What is never written anywhere but its home** (§3.4): a private key, a token or a passphrase never goes into a log line, a
machine-log field, an event, a record, a route's answer, a bridge payload back to the page, Ask Daoris's facts, an instruction,
a tracked file or a commit message. A public key goes only where the person copies it. A host's answer is reduced to its class
and, for a test, the user it names. The sensitive gate gains GitHub's fine-grained (`github_pat_`) and GitLab's (`glpat-`) token
shapes and an OpenSSH private key's block it already has (`SensitiveGate.cs:76-81`), so a repository using it catches one
committed by mistake.

## 11. The machine log

Coded lines, each with the door (`daoris`, `landing`, `session`, `press`, `terminal`), never a secret, a URL's user-info or path
beyond the repository's name, never an identity's user or email (its id stands for it):

| Event | Fields | When |
|---|---|---|
| `git.refused` | repository, remote, host, class, code (`denied`, `no-identity`, `refused`, `host-unknown`, `host-changed`, `read-only`), door, session | each refusal by the judge or the transport |
| `git.asked` | repository, remote, class, door, session, how (`held`, `go-ahead`) | each ask raised |
| `git.granted` / `git.grant.used` | repository, remote, class, session, by (`held-call`, `go-ahead`, `standing`, `press`) | a grant written, and consumed |
| `git.held` / `git.lifted` | repository, remote, class, why | §5.2 |
| `git.identity` | identity id, host, way, act (`added`, `imported`, `tested`, `removed`, `default`), result code | each act on an identity |
| `git.wired` | count of identities, of pointers, git version, whether GCM was found | each rewrite of Daoris's git file |

## 12. Considered and rejected

- **Daoris's git using the person's credentials by default** (today's arrangement): it is the person's sign-in used without
  their say (D125, D175, D197 point 3), and it is what failed (§0.4).
- **Setting `HOME` for sessions** to a folder of Daoris's: every tool a session runs reads `HOME` (npm, the agent's own caches),
  and moving it changes far more than git. Git's own variables reach git alone.
- **Daoris's keys in the person's `~/.ssh` or agent, or `Host` aliases in the person's `~/.ssh/config`**: writes the person's own
  configuration (D121 §2.5's rule).
- **An ssh agent of Daoris's own**: Windows' OpenSSH agent is one service per user, holding the person's keys; a private agent
  would need a socket per Daoris run that git's ssh and Windows' ssh reach alike. Not measured, and a key file with
  `IdentitiesOnly` does the same with nothing running.
- **DPAPI-sealed keys, or a passphrase**: §3.4.
- **Trust a host key on first use, in the background**: §3.5; D73's rule.
- **`includeIf` per repository to choose an identity**: §2.
- **A `pre-push` hook, a refusing credential helper, or the harness's rules alone as the judge**: §6.5.
- **Git access as pauses inside `access`**: full access would open them (§6.4).
- **A list of identities per scope**: §3.2.
- **An agent narrowing its own access through a tool**: §6.2.
- **Ask Daoris setting up an identity**: it would carry a secret or start a sign-in on the person's behalf (§9).
- **Signing `gh` and `az` in with the git identity**: their sign-ins stay theirs (D121 §2.8); a landing on GitHub may push as
  Daoris's git identity and open its pull request as `gh`'s account. The Git place says which `gh` account is signed in beside a
  github.com identity (read through `gh auth status`, no token). Handing `gh` a token is left for later.

## 13. What this amends, once accepted

- **D121**: §2.5's allow-list gains `credential.helper` and the namespace, carried in the command scope (§4.1); the file moves to
  `profiles/git/`; §2.6's terminal keeps the person's own git (§4.4). TOOLS6 is built inside GITAUTH1b.
- **D72**: the `no-push` default's rules are written from git access (§6.8); its id stays.
- **D184**: git access joins the access setup in keys of its own, untouched by full access (§6.4); the hold of a permission
  request widens from the asking posture's mark to git access's rules (§6.6).
- **D37 and D87**: "Daoris pushes nothing" stands (the plugin pushes); the landing asks the judge before it starts the plugin and
  grants exactly its push (§6.7).
- **D172**: `profiles/git/` (keys, `ssh_config`, `known_hosts`, the git file) and `data/git-identities.json` join the home's
  table, in the three home twins.
- **D135**: an approved go-ahead of kind `push` (or `fetch`) on a session's git remote also writes a grant (§6.6).

## 14. The build, in order

Each row a feature a person can see working. Lanes as `daoris.lanes.json` places them; the parent chooses at dispatch.

1. **GITAUTH1a — a remote that cannot authenticate is said once and left alone.** The failure classes (§5.1), measured against git
   and OpenSSH; `run/git/held.json` and the hold (§5.2), the moments reading their fetch's answer; one *What needs you* row per
   held group; `GIT_TERMINAL_PROMPT=0` and `GCM_INTERACTIVE=never` for every child (§4.5); the session's Git section for a held
   or unreachable remote (§5.3). Before identities exist (1b), the row says what the git on the path needs, in WSR7's words
   (`SyncWords.Reaching`), and its press opens the first repository. *Proof*: a fetch
   against a local SSH server refusing the key held once, no second connection across a minute of moments, the row in en and zh,
   the instruction's section; on the install, the owner's SSH remote shown once and no session fetching it (screenshot).
2. **GITAUTH1b — SSH identities, from the terminal.** `data/git-identities.json`, `profiles/git/` with its ACL, generate and
   import, the launcher (§4.2) with Daoris's `ssh_config` and `known_hosts`, Daoris's git file and the environment (§4.1; TOOLS6),
   host-key trust at a test (§3.5), the `git identity` verbs and `git default`; Daoris's own fetch through it. *Proof*: tables for
   the file, the ACL, the launcher's arguments and its refusal; a Process test of a fetch through the launcher against a local
   sshd with a generated key; `SensitiveGate` rows. *Owner-permitted*: a key added on a real host and a real fetch (TOOLS11's
   kind).
3. **GITAUTH1c — Settings → Git for SSH.** §7.1, §7.2's SSH steps, the glossary's four terms, `settings.git.json` in en and zh,
   the coverage rows. *Proof*: stories per state at wide, half and narrow, en and zh, both themes; `i18n:check`,
   `names:check -- --strict`; the window.
4. **GITAUTH1d — HTTPS identities through GCM.** Whether MinGit carries GCM, measured; GCM managed if not; the namespace, the
   browser sign-in's phases, a pasted token, remove as erase, the per-URL user. *Proof*: a fake GCM's tables (what it is asked,
   with which namespace and user); *owner-permitted*: a real browser sign-in.
5. **GITAUTH1e — which identity a remote uses.** The pointers (§3.2), *Your own git sign-ins* (§3.3), the per-URL wiring, the
   Setup rows (§7.3's remote half), `git use`, the one-time move row (§6.8), Ask Daoris's `git-use` door and sync card's screen
   door. *Proof*: the resolution table (each step, a cleared pointer, a removed identity, own), the wiring per remote, the move
   row once; the window.
6. **GITAUTH1f — git access, set and judged for Daoris's own acts.** The keys and their resolution in `AccessRules`, the record,
   the rows on Setup, Driver and Git with their confirmations (§7.3), `access git-*=…`, the judge in process for fetches, presses,
   moments and the landing with its standing yes (§6.7), the migrations of §6.8, the machine-log lines. *Proof*: the resolution
   table, the stored shape read by an older build, the landing refused, asked and answered by a standing yes, the folds said once.
7. **GITAUTH1g — a session's git judged by the same policy.** The rules written from the policy, the widened hold and its grant,
   the transport's judgement of ref updates over SSH and the HTTPS push relay (both measured against a real git first), go-ahead
   grants, the session's Git section with its access, the head's *Details* and *Tighten…*. *Proof*: tables of ref updates
   (fast-forward, force with and without the old commit held, delete, tag, the line), a Process test of a push refused and one
   granted through each transport against a local server; the held call answered on the window.
8. **GITAUTH1h — the install, seen** (owner). One SSH identity and one HTTPS identity on real hosts, a held remote set up and
   lifted, a session's push asked and allowed, a force refused; screenshots of each surface in en and zh.

## 15. What this design's gate does not cover

Written from the code; nothing was run, no key was generated, no sign-in started, and the person's `~/.ssh` and credential store
were not read. Unmeasured, and owed by the rows that need them: GCM's namespace, per-URL user and non-interactive behaviour as
its documentation says (1d); whether MinGit carries GCM (1d); which hosts take ed25519 (Azure DevOps is documented as taking RSA
keys; 1b reads the host's current documentation); Windows OpenSSH's check of a key file's access with the ACL of §3.4 (1b);
whether an `ask` rule raises a permission request under `bypassPermissions` and under auto mode on each Claude door (1g);
whether Codex's sandbox lets a session's git reach the network at all on Windows (1g); the receive-pack command stream and the
remote helper's `push` lines as git 2.56 writes them (1g); Git LFS over SSH and HTTPS (not designed: an LFS object's transfer asks
the same credential, and its judge is left for a row once a repository needs it).

## 16. The reads (D191)

| Read | By | At | Findings | Verdict |
|---|---|---|---|---|
| 1 | Codex | `75bb61805` and this branch | pending | pending |

**For the steward**, beside the reads: (1) Write's default *ask* turns an agent's own push from a refusal into a question
(§6.1); (2) whether *Keep using my own sign-ins* should be applied without the person's press at the move (§6.8), which D191
point 3 may make the owner's, since it is a sign-in; (3) Fable's read, since this changes a foundation.

# Git's place in Daoris (GITDESIGN1)

**Proposed, for the owner's yes (D241).** The owner's direction is D240 points 3 to 5: git managed by design, the transport judge deciding
dangerous transports itself with a grant bound to its ref, a pull request composed by a place's workflow from git steps after auth is
applied correctly, and no tools installed to measure the doors. Written from the code at `f96e05f5d`; nothing was run against a host.

**In brief.** Git stays in Daoris's core: it is the substrate of trees, reads, landing and integration, and none of that needs a
credential. What needs one is four paths to a remote, and those are where auth goes wrong today. The recommendation (§3) keeps every git
act Daoris's own, the push included, through the two doors. A pull request becomes a place's workflow steps: Daoris's `push`, then a
hosted service's step performed by a plugin that runs no git and no CLI and is handed a token Daoris holds for the place's identity.
CI proves the whole path against stand-in remotes over SSH and HTTPS and a stand-in service API, with nothing installed.

## 1. What Daoris does with git today

### 1.1 Which git, and how every start is wired

- **The git.** Tools resolves it: the system's, a file the person names, or the managed MinGit 2.56
  (`Daoris.Desktop.Driver/resources.json:19-35`). Daoris's own git starts in `GitRuns.GitStart` by that file's path, with
  `core.longpaths` and the tools' environment (`Workplace/GitRuns.cs:17-53`).
- **Every start** passes `Tools.Hand`, which calls `GitWiring.Hand` (`Workplace/GitWiring.cs:69-94`). It sets the no-prompt variables
  (`:29-35`), removes askpass and trace variables (`:41-42`), names the home (`DAORIS_GIT_HOME`), and routes SSH and HTTPS to the
  doors only where they lie in the install's `app/bin/` (`:78-93`, `:135-136`).
- **The starts.** A session's start is handed so (`Agents/Spawning.cs:133`) and loses the `gh` and `az` token variables
  (`Workplace/AgentWorkplaceWiring.cs:15`, `GitWiring.cs:48`). A plugin's process is handed so as well, plus a landing's grant
  (`Plugins/HookProcess.cs:106-108`). The terminal's shells keep the person's own git (`GitWiring.cs:9-11`).
- **The doors** are one program under two names that git starts itself (`Daoris.Desktop.GitDoor/Program.cs:12-26`). The ssh door is
  `GitDoors.Ssh` (`Workplace/GitDoors.cs:80-125`) and the remote helper is `GitDoors.Helper` (`:405-512`). Each one finds the
  repository from the act's folder, resolves an identity (`:150-181`), asks the judge (`:529-575`) and carries the act.

### 1.2 Where git runs

| Place | What git does | Reaches a remote | Where |
|---|---|---|---|
| Session trees | worktrees made, slimmed, set aside and removed; the agent's own git in its shell | the agent's fetch, pull and push, through the doors where laid | `SessionTrees`, `TreeWorktrees`, `TreeOpening`; `Spawning.cs:133` |
| The look and Review | status, diffs, ranges, blame and graphs, cached by commit | no | `GitReads*.cs`, `ReviewGraphReads.cs`, `GitReadOnce.cs` |
| Landing | merge or branch, recovery refs, the delivery's phases | no; a `push` or `pull-request` landing starts a plugin that pushes | `LandPerformer.cs`, `TreeLands.cs`; `Plugins/LandingPlugins.cs:115-150` |
| Integration | merges, replays and promotions on the line | no | `Workplace/Integration*.cs` |
| The person's acts | ref acts, history edits, stage, commit, switch, stash, resolve, all on the act journal | the fetch of origin's heads | `ActRuns.cs`; `GitFetchActs.cs:31` |
| Daoris's fetch of the line | `fetch origin <line>` before a sync, a landing, a start in a standing tree and a pull request's moment | yes | `TreeRefs.cs:97-143` (fetch at `:129`); callers `TreeSync.cs:112`, `TreeUpdates.cs:108,176`, `TreeLandSyncs.cs:109`, `PullRequestMoments.cs:594` |
| A pull request | the plugin's `git push`, then `gh pr create` or `az repos pr create`; its state from `gh` or `az` | yes, as the person's CLI sign-in | `examples/plugins/github-pull-request/land.mjs:114,136,243-247`; `azure-devops-pull-request/land.mjs:149,171,346-352` |
| Record sync | quests and records with a shared host | HTTP with machine keys, not git | `Service/RemoteSync.cs` |

The local column is the core. About 580 git calls in 132 driver files start through `GitRuns`, and none of them carries a credential.
Four paths reach a remote: Daoris's fetch of the line, the person's fetch, a landing plugin's push and CLI calls, and the agent's own git.

### 1.3 Identities and credentials

- **SSH.** A key is generated (ed25519, or RSA 4096 where the host takes RSA alone) or imported (`GitIdentityActs.cs:83-112`). It is
  kept in `profiles/git/keys/<id>/`, and the ssh door starts OpenSSH with that key alone and Daoris's `known_hosts`
  (`GitDoors.cs:222-245`). The ssh is the one the person chose, else whichever `ssh` git's `PATH` finds (`:251-265`).
- **HTTPS.** The sign-in is Git Credential Manager's, in Daoris's namespace (`GitHttps.cs:117-149`). GCM is looked for beside git
  (`mingw64/bin`, `libexec/git-core`) or on `PATH` (`GitHttps.cs:30-53`). The managed MinGit's `ucrt64` layout is not among those
  folders, and the GCM case of the HTTPS proof skips where none is found (`GitHttpsProcessTests.cs:239-244`).
- **Hosted services' CLIs.** The pull-request plugins act as the person's `gh` and `az` sign-ins. Their manifests offer `gh auth login`
  and `az login` as fixes (`github-pull-request/plugin.json:22`, `azure-devops-pull-request/plugin.json:28-30`). A session's `gh` and
  `az` are the person's own as well, unjudged (area rule 12).

### 1.4 Where auth is applied, and where it fails

| # | Where | What happens |
|---|---|---|
| F1 | The installed window | The publish lays the doors only with `--git-doors` (`tools/desktop-publish.mjs:348-362`). Without them `GitMove.Moved` is false (`GitMove.cs:13-14`) and every git, a session's included, uses the ambient credentials. WSR7 measured the result: the git Daoris runs could not use the key the person's own client used (git-auth design §0.4). This is why the work workspace's SSH remotes cannot be set up today. |
| F2 | The doors laid, nothing serving | An SSH remote is held `no-identity` before it connects (`GitDoors.cs:108-113`), and HTTPS reads anonymously. No key has yet been added on a real host (GITAUTH1c). |
| F3 | A read-only identity | The doors judge Write without the identity's mark (`GitDoors.cs:529-536`). Only the landing reads it (`LandingPlugins.cs:99-113`), so a session's push under a read-only identity reaches the host (GITREADONLY1). |
| F4 | The landing's grant | It is bound to the address alone (`GitAccessRules.cs:187-230`) and also passes Dangerous (`:80-82`). For 15 minutes (`:190`), the plugin's process may force or delete any ref at origin. |
| F5 | `no-push` | The switch is read at each act as a Write allow (`GitAccessRules.cs:92-106`, used at `GitDoors.cs:536`). That is a second way to say what Write says (ONEWAY1q). |
| F6 | HTTPS on the managed git | No GCM is found, so nothing stores an HTTPS identity's token and the act reads anonymously (`GitHttps.cs:30-53`, `:127`). |
| F7 | A pull request | It is opened and read as the person's `gh` or `az` (F1's ambient credentials for the plugin's push where no door is laid). This is the credential of the person's that D198 point 3 says is never handed. |
| F8 | A session's `gh` and `az` | The token variables are gone, but each CLI's own configured sign-in remains, unjudged. |
| F9 | The proof | The SSH identity path runs only where `DAORIS_TEST_SSHD` names an sshd (`GitSshdProcessTests.cs:285-299`), and no CI job names one. The ssh door's push is proved against a stand-in ssh under `own` alone (`GitDoorsProcessTests.cs:6-16,270-298`). HTTPS is proved over loopback HTTP without TLS (`GitHttpBackendHost.cs:9-14`). |
| F10 | A pull request as a default | *Set up…* suggests a pull request wherever a plugin serves the remote's platform (`SetupPresets.cs:162-166`). A `push` or `pull-request` landing takes the platform's plugin at the bind (`LandNodes.cs:57-59`). |

## 2. The options

### 2.1 (a) Git's processes in a plugin

The core would hold no git: a plugin process would answer every read, tree, landing, merge and transport over the plugin wire.

- **Cost.** About 580 calls in 132 files would move behind a process boundary, the look's per-tick reads included. The wire is a
  JSON-RPC frame per point, with a process started per landing and asks bounded in seconds (plugins rules 9 and 10). The act journal's
  checkout lock, the recovery refs and integration's merges are Daoris's own guarantees, and they would become a plugin's promises.
- **Security.** The judge must always be installed (D198 point 11). A plugin can be switched off, and then a session's push passes
  unjudged and the move off ambient credentials (D233 choice 7) is undone.
- **What it proves.** Nothing about auth that (b) or (c) does not. The keys would sit in one long-lived process instead of a door
  started per act.
- **(a′), the remote half alone in a plugin** (the doors, identities, judge, push and pull request). The doors are already separate
  programs that git starts (`GitDoor/Program.cs`), so the process boundary already sits where it matters. The plugin's switch would
  still leave the judge optional, and git policy would have two owners.

### 2.2 (b) Built-in git as it stands, proven end to end

The doors are laid by default. Stand-in remotes over SSH and HTTPS run in the `Process` half, and the pull-request plugins keep their
`git push`, `gh` and `az`.

- **Cost.** The smallest: the stand-ins, the end of `--git-doors`, and GITAUTH1f and g as written.
- **What it proves.** The doors' transport, identities and judge, end to end.
- **What it leaves.** F6, F7 and F8. The pull request still authenticates as the person, and the work workspace still needs its hosted
  service's CLI installed and signed in on the owner's machine (D240 point 4). Two programs push: the plugin and the person's press (GITAPP2g).

### 2.3 (c) Built-in git, one push, and a hosted service reached without git or a CLI

Every git act is Daoris's own and passes the doors, the push included. A hosted service is reached only for what git cannot do: open a
pull request and read its state. That reach is a plugin's step that runs no git and no CLI, and it is handed a token Daoris holds for
the identity serving origin, for that one act.

- **One push.** One act on the act engine (journal, checkout lock, recovery) pushes a branch through the doors with a grant bound to
  the address, the ref, its old commit and its new one. The person's press (GITAPP2g) and a workflow's `push` step both use it.
  A lightweight tag (GITAPP2n) is the same act, judged Dangerous.
- **A pull request is steps** (§2.5): `land` (branch), then `push`, then `pull-request`. It is never a `land` method or a suggestion.
- **Tokens beside the keys.** An HTTPS identity's token, and an identity's API token for its host, live in `profiles/git/tokens/<id>/`.
  The folder is limited to the person's account before a byte is written, and tokens are plaintext at rest, as keys and D67's API keys
  are. git's HTTPS act reaches a token through a third name of the door, `git-credential-daoris`. The remote helper names it for its
  inner transport alone, and it answers only the identity's host. GCM leaves Daoris.
- **Cost.** About nine chunks (§3.1). The pull-request plugins are rewritten against their services' APIs. Graphs that land by
  `push` or `pull-request` are converted once, and the GCM sign-in half of GITAUTH1d and e is retired.
- **What it proves.** Everything (b) proves, plus the pull request's whole path: push, open and state, in CI against stand-ins, with
  no `gh`, no `az` and no GCM anywhere.

### 2.4 How auth is applied in (c), per place

Identity is resolved for every row as today: the repository's remote pointer, then the workspace's per host, then the machine's, then
the host's default (`GitIdentityUse`). Each class's access value resolves the same way: the repository, then the workspace, then the machine, then the default.

| Place | Who acts | Class | The credential reaches | What a session holds |
|---|---|---|---|---|
| A session's fetch or pull | the agent's git through the doors | Read | the door's ssh child (the key's path in its arguments) or its inner HTTPS transport (the credential door) | no key path, token or grant in its environment; the harness rules deny reading `profiles/git/` |
| A session's push | the same | Write: `deny` by default, `ask` held at the door, a read-only identity refused | the same | the same |
| A session's force, delete, tag, mirror or prune | the same | Dangerous, judged per update by the place's value: `allow` or `deny` decided at the door, with no person in the loop | the same | the same |
| A session's `gh` or `az` | the CLI | none | nothing: its configuration folders name an empty folder of Daoris's, and the token variables are removed | no hosted-service sign-in |
| Daoris's fetch of the line, the person's fetch | Daoris's git | Read | as above | none |
| The `push` step, the person's push | Daoris's git, one act | Write with a grant bound to its refs and commits | as above | none |
| The `pull-request` step and its state | the plugin's process, running no git | the step's own yes; refused where the identity is read-only or Write denies | the identity's API token, handed to that one process (`DAORIS_HOST_TOKEN`, as the grant is handed today) | none |
| The terminal's shells | the person | none | the person's own | n/a |

Per identity:

- **An SSH key** carries git's transport alone. A place that composes a pull request adds an API token to the identity serving its
  host. The work workspace takes one SSH key for its hosted service's host, pointed at by the workspace, and one API token.
- **An HTTPS token** carries git's transport. Where the host's scopes allow, the same token serves the API.
- **`own`** (the person's sign-ins, chosen per host and confirmed) covers git alone. A `pull-request` step under `own` is refused
  until an API token is set.
- **The residual, stated as D198 point 16 states it.** A session runs as the person's account. The doors govern an agent's ordinary
  git, and the harness rules keep it from reading the secret folders on the Claude doors. Codex holds no rules, which its block says.

### 2.5 A pull request as a workflow's steps

- **`land` keeps `merge`, `branch` and `integrate`.** Its `push` and `pull-request` methods leave it, with the tails they bring
  (workflows rule 10), and so does the platform plugin resolved at the bind (`LandNodes.cs:57-59`).
- **`push` becomes a performed node of Daoris's.** It pushes the branch the `land` made to `origin`, through the doors, under a
  ref-bound grant. It is reached when the remote's ref holds the commit, and Daoris writes that fact itself; today's `pushed` reads a
  plugin's answer instead (`BuiltInNodes.cs:175-186`).
- **`pull-request` is a plugin's performed step** on the work wire's `work/perform` (`PluginVocabulary.cs:72`). It opens the pull
  request from the pushed branch, and its state stays on `work/state` (`:36`) with D148's rules. `work/land` (`:29`) retires.
- **Auth first.** `push` does not start where nothing serves origin, the identity is read-only, Write denies, or a hold stands. It
  says which on its node. `pull-request` does not start where the identity has no API token.
- **Not a default.** *Set up…* stops suggesting a pull request (`SetupPresets.cs:165`). A place has one only where its saved workflow
  holds the steps. The shipped *pull request* graph stays a preset the person may choose.
- **Conversion.** A saved graph whose `land` pushes, or opens a pull request, converts once through ONEWAY1's conversion door to
  `land` (branch), `push` and, where it had one, `pull-request`. The old reading is registered with the condition that ends it.

## 3. The recommendation

**(c).** Git is the substrate of Daoris's core, and it is safe there because local git needs no credential. The remote edge is where
auth belongs, and (c) applies it at one place per act: the doors for git, and a per-act token for a hosted service. One push serves
the person's press and a workflow's step, and the plugin's push leaves. One secret home replaces GCM. Every path is provable in CI with stand-ins, so the owner's
machine needs no sshd, `gh`, `az`, git-lfs or GCM. (a) moves the core behind a wire and makes the judge optional. (b) proves the doors
but leaves the pull request on the person's sign-in, which is exactly the auth D240 point 5 says is not yet applied correctly.

### 3.1 The chunks, in waves

Each wave's first chunk is the exemplar, built by Opus; the chunks after it mirror the exemplar and are built by Sonnet.

| Chunk | Wave | Model | Rows | What it builds | Its proof |
|---|---|---|---|---|---|
| **G1** stand-in remotes | 0 | Opus | GITAUTH1b's proof, re-cut | A loopback SSH host in the test process (§4.1), a TLS host over `GitHttpBackendHost`, the doors' classes run with the pinned MinGit as well as the runner's git | §4.1's SSH and HTTPS rows, red first against today's code where F3 and F4 hold |
| **G2** the judge completed | 1 | Opus | GITREADONLY1, GITAUTH1f's door half, ONEWAY1q | The doors read the identity's read-only mark; a door checks that Daoris is running before it waits on an ask; `no-push`'s switch converted into Write and `PushesHere` retired | a read-only identity's push refused at each door with its words; ONEWAY1q's conversion rows |
| **G3** the transport judge | 1 | Sonnet | GITAUTH1g | A grant bound to address, ref, old and new commit; tag, mirror and prune judged as Dangerous by kind; Dangerous settable on the page; the batch refused before any ref moves | GITAUTH1g's own proof list, against G1's hosts |
| **G4** tokens and the credential door | 1 | Sonnet | GITAUTH1d re-cut, GITAUTH1e's sign-in half retired | `profiles/git/tokens/`; `git-credential-daoris`; an identity's API token; GCM, `GitHttps.Manager` and the browser sign-in leave | an HTTPS identity's fetch and push on the TLS host with nothing found beside git; `GitHttpsProcessTests` with no skip |
| **G5** a session holds nothing | 1 | Sonnet | GITAUTH1f's rest (redone as named types; the unmerged branch is dropped) | The session's access record with *Tighten…* and `git access --session`; the head's access facts; a session's `gh` and `az` folders pointed empty; the harness's deny on `profiles/git/` | an environment census of a session's start: no key path, token or grant |
| **G6** the doors always laid | 1 | Sonnet | GITAUTH1b's end | The publish always lays the doors; `--git-doors` and its skip line leave the publish and the rehearsal | `deployment`; the move's row on a rehearsed home |
| **G7** one push | 2 | Opus | GITAPP2g, GITAPP2n, ONEWAY1r | The push act on the journal with a ref-bound grant; the tag on the same act; every branch delete through `GIT_REF_DELETE` | a push on the press, refused without a grant; a tag judged Dangerous; each delete door in the journal with its Undo |
| **G8** a pull request as steps | 2 | Sonnet | D240 point 5 (new) | The `push` node, `pull-request` on `work/perform`, the conversion, the end of the platform suggestion and of `work/land` | §4.1's workflow row |
| **G9** the plugins on their APIs | 2 | Sonnet | D240 point 5 (new) | Both pull-request plugins open and read through their services' REST APIs with `DAORIS_HOST_TOKEN`; no `git`, `gh` or `az`, and no tool checks for them | each plugin's `node --test` against a stand-in API host |
| **G10** the install | 3 | the steward, then the owner | GITAUTH1c, WORKSETUP1 reworked, GITAUTH1h | One SSH identity for the work workspace's host with its key added there and the host key compared; one API token; the workspace's workflow composed as `land`, `push`, `pull-request`; the owner's Apply; the first pull request end to end | §4.2 |

G1 comes first. In wave 1, G2 comes first and G3 to G5 run beside each other. G6 waits on G1 and G4, since laying the doors by default
moves every install off ambient credentials. G7 waits on G3, G8 on G7, and G9 runs beside G8. The folded rows that wait on the first
pull request (AUTOTIDY1c, SQUASHTIDY1d, PLUGHOOK1d, FABRIC-PERFORM1l) follow G10. GITAPP1 gets no chunk: its part two is Review's
local reads, OPTIMIZE1a's, and the design keeps its premise. The backlog plan's BL5 and BL9 are replaced by G2 to G7.

### 3.2 What it retires (D228)

- **Rows.** GITREADONLY1 (into G2). GITAUTH1b's measurement list: a real sshd is replaced by G1, MinGit is pinned in G1, `gh` and
  `az` leave the product in G9, and git-lfs is said, not routed, until a repository needs it. GITAUTH1d's GCM half and GITAUTH1e's
  sign-in `PhaseList` and Cancel (G4); GITAUTH1e's sync card's screen door is not git's transport and stays its row. The unmerged `worktree-agent-a4dce1699fabacfa0` foundation (G5). WORKSETUP1's parts as written
  ("GITAUTH1's rest (c, e, f), GITAPP2g and n", the plugin and its CLI) are replaced by G10.
- **Ways.** The plugins' `git push`; `work/land`; `land`'s `push` and `pull-request` methods and their bind-time plugin; *Set up…*'s
  pull-request suggestion; the `gh` and `az` tool checks; GCM and its namespace; `PushesHere`; the `DAORIS_TEST_SSHD` and GCM skips;
  `--git-doors`. Each recorded shape still read (saved graphs, `defaultsOff`, GCM entries) keeps its reader only with the condition that
  ends it and the row that deletes it.
- **Decisions amended once accepted.** D87 and D100 (a plugin pushes; Daoris never pushes): Daoris pushes, as a step or a press. D198
  points 3, 4, 13 and 16, and its §12 rejection of a token for the hosted service: the pull request's API act takes the identity's
  token, and the CLIs leave. D148 keeps its rules on `work/state`.
- **Area rules to rewrite as each chunk lands.** git-and-remotes 3, 9, 12, 25, 29 to 32; plugins 9 and 23; workflows-review-landing
  10, 29 to 31 and 42.

## 4. The end-to-end proof

### 4.1 In CI, with nothing installed

The `Process` half runs in CI (`.github/workflows/release.yml:287-291`). Every row below runs there with the runner's git, the pinned
MinGit fetched by its recorded hash into `_fixtures/` (the toolchain's file, not an install), .NET and Node.

- **SSH.** A loopback SSH host runs in the test process from a test-only package (Microsoft's dev-tunnels SSH library is the first
  candidate). It serves `exec` of `git-upload-pack` and `git-receive-pack` with the test's git, asks a public key, and presents a host
  key the test compares. Against it: an identity Daoris generated fetches and pushes through the laid ssh door; a session's push is
  refused; a force, a delete and a tag are judged by kind; a read-only identity is refused; a grant moves its own ref and not another;
  an unknown key is held once with no second connection; an unknown or changed host key is held. G1 records which ssh the door found.
  If the package cannot serve OpenSSH's client, the fallback is the portable sshd fetched by its hash into `_fixtures/` and run as a
  plain process on a loopback port, never as a service.
- **HTTPS.** `GitHttpBackendHost` behind .NET's `SslStream` with a certificate made at the test's start, trusted through
  `http.sslCAInfo` in the test's command scope alone, and asking Basic. A token identity fetches and pushes through the remote helper
  and the credential door. A wrong token is held `refused`. A remote nothing serves reads anonymously. A token for another host is
  never answered.
- **A session's start.** A process started as a session is started holds no key path, token or grant in its environment. Its `gh` and
  `az` folders are empty. Its push is refused at each door.
- **The workflow.** A run whose graph is `land` (branch), `push`, `pull-request`, against the SSH host and a plugin talking to a
  stand-in service API on loopback. The branch is pushed under its grant, the plugin opens the pull request with the handed token, its
  state is read, and a `completed` answer clears the branch only where git holds the merge.
- **Each pull-request plugin's** `node --test` against its stand-in API host: open, state, a refused token, and no `git`, `gh` or
  `az` started.
- **The deployment rehearsal** lays the doors by default and sees the move's row.

### 4.2 Only the installed window

- A real host accepting a key Daoris generated, added there by the person, and its host key compared with the published one (GITAUTH1c).
- The work workspace's 29 repositories moved off ambient credentials: the move's row answered once, its identity pointed at by the
  workspace, every remote fetching (WORKSETUP1).
- The hosted service's real API accepting the token and opening the first pull request after the owner's Apply (WORKSETUP1).
- The screens in English and Chinese: Settings → Git with a token, the move's row, a held remote, a session's access, the workflow's
  `push` and `pull-request` nodes (GITAUTH1h).

## 5. Not covered here

- Which test-only SSH package serves OpenSSH's client, which key types it takes (G1 tests with an RSA identity where ed25519 is not
  served), and which TLS backend the pinned MinGit uses with a test certificate. G1 measures each and records it.
- Each hosted service's API: its endpoints, the scopes a token needs, and the words a refusal returns. Each plugin's README states
  them in G9.
- git-lfs: a repository that uses it is said ("needs git-lfs, not carried") until one in a workspace needs it.

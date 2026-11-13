# Git and remotes — how it works now

## What it does

Daoris's git reaches code hosts as git identities of its own, and hands no credential of the person's unless the person chooses
it. Every process Daoris starts gets its git from `GitWiring`: nothing prompts, and every remote act passes Daoris's doors (the
ssh door for SSH; the remote helper, through a `daoris::` rewrite, for HTTP and HTTPS). At each act a door finds the repository,
applies the identity its remote points at, and asks one judge about the act's class. A remote that cannot authenticate is held,
and said once in *What needs you*, until something changes it. The person's own git acts, and every branch delete Daoris's doors
judge, run through one journal under one lock; Daoris's one push is its push act, and a landing plugin opens a pull request.
Every git Daoris starts runs in a place of one scheduler. A workspace may also be wired to a shared deployment of the service:
quests, session records, messages, registrations and code maps sync with it as fetch, rebase, push.

## Its parts

Driver paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/`, `Workplace/` implied for a bare name; each type is in a file of
its name, and `docs/index/README.md` says where a route, verb or fixture is.

| Where | Its job |
|---|---|
| `GitWiring`, `GitRouting`, `GitDoors`, `GitCredentialDoor`, `GitFolders`, `GitRemotes`; `src/Daoris.Desktop/Daoris.Desktop.GitDoor/` | What every start is handed; the three door programs; the repository and address an act reads |
| `GitIdentit*`, `GitTokens`, `GitSsh*`, `GitHostKeys`, `GitAuthor`, `Install/GitGlobalFile`, `Install/GitVersions` | Identities, their secrets, acts and verbs; a commit's author; Daoris's global git file and the version floors |
| `GitFailures`, `GitHolds`, `GitOwnInput`, `GitMove`, `GitWords`, `GitLog` | Failures and holds, the move, redaction, the `git.*` log |
| `GitAccess*`, `GitGrantRef`, `GitPush*`, `GitHelperPush`, `GitSession*`, `GitAsks`, `GitDoorAsks` | Access and its judge, grants, a push's updates, a session's record and holds, go-aheads |
| `Act*`, `RefTransaction`, `Git*Acts`, `GitHistoryEdits`, `StageActs`, `StashDrops`, `JournaledDeletes`, `Recovery*`, `Git*Command` | The act engine; the person's acts and their terminal twins; every delete and keep |
| `Platform/GitScheduler`, `Workplace/Git/`, `GitReads*`, `GitLaunch`, `GitReadCache`, `GitLibrary*` | The scheduler, object reads, the stamp, the page's reads, the library |
| `TreeRefs`, `TreeSync`, `Plugins/LandingPlugins`, `PullRequest*` | The line's fetch and bring up to date; the landing plugin's wire; a pull request's state |
| `Modules/Bridge/Routes/GitRoutes`, `Modules/Platform/RemotesModule`, `src/Daoris.Web/src/settings/git/`, `src/Daoris.Web/src/projects/git/` | The window's doors and the page |
| `Service/Remote*`, `src/Daoris.Service/Daoris.Service.Core/Remote/`, the service's `Quests/` | Remote wiring, the pass, the host's sync, quest conflicts |

## Its rules

**Identities**

1. An identity is a host, a way (an SSH key or an HTTPS token), a label, the user the host reports and an optional
   `commitsAs`; several per host. `data/git-identities.json` holds no secret, and every write keeps an entry this build does not
   read. (D198)
2. No scope holds a list: a repository points per remote, a workspace per host, the machine may choose `own` per host, and a host
   has a default per way. Resolution is repository, workspace, machine, default, the answer naming its source. (D198)
3. A private key lives in `profiles/git/keys/<id>/`, an identity's tokens in `profiles/git/tokens/<id>/` (`https` for git's
   HTTPS act, `api` for a hosted service's act), each folder limited to the person's account before a byte is written, plaintext
   at rest. A token goes in through the identity's doors (the route, `git identity token --token-stdin`, the page) and is never
   said back, only whether one is set. (D198, D67, D241)
4. A host key is trusted only at the person's press, after a compare with the host's published keys, and lives in
   `profiles/git/known_hosts`. (D198, D73)
5. The person's `~/.ssh`, ssh agent and ssh configuration are never read or written, but a key file the person names to import;
   the ssh door gives its ssh `-F profiles/git/ssh_config`. An identity's acts run one ssh by its full path (`GitSshProgram`): the
   OpenSSH chosen in `tools.json`, else the `usr/bin/ssh` of the act's Git for Windows, else the first on `PATH`; Settings → Git
   shows it. (D198)
6. A commit Daoris or a session makes carries origin's identity's `commitsAs` only where the repository names no author;
   authorship is apart from authentication. (D198)

**Transport**

7. Every start passes `Tools.Hand`, which calls `GitWiring.Hand`: `GIT_TERMINAL_PROMPT=0`, an empty `GIT_ASKPASS`,
   `GCM_INTERACTIVE=never`, `GIT_TRACE_REDACT=1`, and askpass, trace, the holder key, the host token, the grant and the inbox's
   messaging variables stripped. A grant is never inherited. The terminal's shells keep the person's git. (D198)
8. Where the doors lie in the install's `app/bin/`, SSH runs `daoris-git-ssh.exe` by a bare path in `GIT_SSH_COMMAND` (else
   `GIT_SSH`), and HTTP(S) reaches `git-remote-daoris.exe` by `insteadOf` in the command scope; no doors, no routing. Under an
   identity the helper starts git's own transport with `credential.helper` emptied and then `daoris`, `DAORIS_GIT_IDENTITY`
   naming the identity: `git-credential-daoris.exe` answers `get` with that identity's token and user for its own host alone and
   changes nothing on `store` or `erase`. A remote nothing serves reads anonymously. (D198, D241)
9. Every publish lays all three doors in `app/bin/`; the deployment rehearsal proves their layout. (D198, D241)
10. A door finds the repository by the act's own working folder, so `git -C`, a clone or a submodule resolves for itself. (D198)
11. Under an identity the door removes the person's authentication inputs: helpers, extra headers, cookie files, certificate
    variables, `_netrc` (by an empty home), an address's user-info. An address carrying a password is refused. (D198)
12. A session holds no credential: its start drops `GH_TOKEN`, `GITHUB_TOKEN`, `GH_ENTERPRISE_TOKEN` and `AZURE_DEVOPS_EXT_PAT`,
    hands no grant or key path, and points `GH_CONFIG_DIR` and `AZURE_CONFIG_DIR` at empty folders (`run/git/sessions/empty/`).
    The Claude doors' rules deny reading `profiles/git/`; Codex holds no rules. The session still runs as the person's account
    (`GitSessionHolds`). (D198, D241)
13. One reader, `GitRemotes`, gives every address Daoris keeps, says or judges, `daoris::` taken off. A plugin tool that reads
    remotes is named the repository or runs unrouted. (D198)
14. Below git 2.31 the doors cannot stand: Daoris's fetches and the push act refuse as `old-git`. Below 2.32 a git setting
    refuses as `git-too-old`. (D198)
15. Every text from git, ssh, a door or a plugin passes `GitWords.Redact` before it is kept, said or answered. (D198)

**The move and the holds**

16. A home has moved where all three doors lie in the install's `app/bin/` (`GitMove.Moved`; `GitMove.Missing` names the
    absent); no mark is kept. A moved install reaches a host with its identity's key or token, or none: a remote nothing serves is
    held `no-identity` before SSH connects, and HTTPS reads anonymously. A home with no install beside it, or an install missing a
    door, is not moved and its git is as it was. (D198, D232, D233, D241)
17. The move is one *What needs you* row per workspace for every remote nothing serves (its repositories counted, those naming
    their own authentication apart), opening Settings → Git on the host; a row whose push was refused or whose repository names
    its own authentication opens that repository's git row. A held row holds work where a session of one of its repositories runs
    or is parked. (D198)
18. `own` is a widening, confirmed before it is written; no migration applies it. (D198)
19. A failure is classed by the transport's words and held, keyed by repository, effective address, what served it and whether
    it holds reads or writes. Network and unclassed failures hold nothing. (D198)
20. A hold lifts on a change of an identity, default or pointer, on a passing act, or on *Try again* (`GIT_TRY_AGAIN`); a session
    is told in its Git section not to try. (D198)
21. `own-input` is found before the act: a `core.sshCommand` that would outrank `GIT_SSH`, or an HTTPS certificate, key, header
    or cookie file in configuration where an identity serves. It holds until the setting goes or `own` is chosen. (D198)

**Access and go-aheads**

22. Git access has three classes, Read, Write and Dangerous, each `allow`, `ask` or `deny`, set in `gitAccess`,
    `workspaceGitAccess` and `machineGitAccess`, each resolving repository, workspace, machine, default. (D198)
23. The default is Read `allow`, Write `as-today` (the landing pushes on the person's yes; a session does not), Dangerous
    `deny`. Dangerous is only `allow` or `deny`: no person is waited on for it, and a stale `ask` reads as `deny`.
    (D198, D72, D241)
24. Git access is apart from `access`: full access never touches it, and a loosening is a confirmed widening. A running session
    holds a record, `run/git/sessions/{id}.json`, begun at its start: its repository's access now, narrowed by what the person
    tightened for it (*Tighten…*, `TIGHTEN_SESSION_GIT_ACCESS`, `git access --session <id> write=deny`). A tightening needs no
    confirmation and no later loosening lifts it; both doors judge a session's act by it, by `DAORIS_SESSION_ID`. A record is kept
    14 days. (D198, D184, D241)
25. The doors judge by the remote command: `push`, `list for-push` and `receive-pack` are Write. An identity read only on its
    host refuses every Write through it, a grant's included. `deny` refuses and no grant lifts it; `allow` passes; `ask` holds the
    act at the door. Each update of a push is judged by its kind at the doors, for every agent: the ssh door reads receive-pack's
    commands before forwarding them; the helper holds git's `push` lines to the batch's end and forwards only the objects it
    judged (`GitPushUpdates`, `GitHelperPush`). A delete, a force (the old commit not an ancestor of the new), a tag, a mirror
    (outside `refs/heads/`) and a prune are Dangerous: the place's value decides, unless a grant carries the update. A grant names
    an update by its ref, its new commit and its old one where bound; it carries a Dangerous kind only with its old commit bound,
    and never a tag. One refused update refuses the batch before any ref moves (`GitPushJudge`). (D198, D233, D241)
26. A held `ask` is a git go-ahead, `run/git/asks/{id}.json`, answered *Allow* or *Refuse* in *What needs you*, the session's head
    or `git answer`. The door waits up to 15 minutes; a late allow is a grant the identical retry takes once. Where no instance of
    Daoris holds the home's lease, the door takes a kept grant or refuses at once (`not-running`). (D198, D135)
27. The doors judge by git access. `no-push` and `no-publish` are opt-in defaults, off until the person switches them on (rules
    page, `agent rules default`, `defaultsOn` in the rules file); a session in full access has Write `allow` at the doors where no
    one set one (its record's `unrestricted`, Dangerous unchanged). A proposal to allow or ask a rule that a deny would beat, the
    person's own or `no-push` once on, is refused where it is proposed, naming the deny and where it is set (`DeadApprovals`).
    The home's migration made a switched-off `no-push` the machine's Write `allow` where unset (`GitPushStep`). (D198, D277)
28. While `no-push` is on, a session's harness rules follow its Write (`GitAccessPolicy`): `deny` or the default keeps every push
    form denied; `ask` or `allow` opens the plain push and denies force, delete, tag, mirror and prune unless Dangerous is `allow`.
    Codex holds no rules file; its block and the access setup say so. (D198, D72, D233)
29. Identity is who, access is what. A read-only identity refuses writes at both doors and at the landing. (D198)

**Landing and pull requests**

30. Daoris's one push is the push act (rule 36), opening no pull request. A workflow's `push` node performs it (`PushDoor`); a
    plugin's performed step on `work/perform` opens the pull request. A landing names no plugin; there is no `work/land` point,
    hand-off verb, route or page, and no automatic hand-on of a held-back push. A delivery or note recorded before that is read and
    said, never sent again. (D87, D100, D241)
31. A plugin process holds no git grant and runs no git: only the push act writes a grant, and `DAORIS_GIT_GRANT` is dropped from
    every start. A plugin's `pull-request` step opens and reads the request through its service's REST API. (D241)
32. That step's process starts one per place (`PlaceHands`, `HookSet`) with the plugin's own credential from its own `login`,
    never an identity's token; none starts where the place has no origin, its identity is read only, or Write is denied. An
    identity's `api` token is read only by the command that adopts it (`PluginTokenAdoption`), which clears it. `gh` and `az` are
    no declared tool. (D241, D242, D274)
33. A pull request's state is asked only of the plugin that opened it, at occasions that may remove, never on a timer. A
    `completed` answer clears a branch only where git holds its merge commit on the line. (D148)

**The line**

34. Daoris's own fetch of a line is `fetch origin <line>` through the doors: refused on an old git, held on `own-input` or a
    standing hold, counted for the pull request minute, moving only origin's refs. (D109, D198, D148)
35. Bring up to date is listed, then pressed: fast-forward only, replays in Daoris's own trees, local deletes of what merged;
    never a merge commit, a force or a push. (D109, D112)

**The person's acts**

36. The person's acts are a fetch of origin's branches; a push of one branch or lightweight tag to origin; a branch or
    lightweight tag made, renamed or deleted; and pick, revert or rebase onto a branch no tree holds. The push is one `push` step,
    `<commit>:refs/heads/<name>` or `refs/tags/<name>`, with no `+`, `-u`, followed tags, submodules or signing, its hooks run,
    only where origin names one push address and no setting runs another program. The step writes a grant bound to origin's push
    address, the ref, the commit origin had it at as last fetched (the zero id where none, and always for a tag) and the new
    commit, hands it to that git alone and drops it when git ends. It is refused where Write denies, the identity is read only,
    origin is held, the ref moved since shown, or the branch is `daoris/*`. It has no Undo; an end with no exit is unknown and
    never retried. A workflow's `push` step is the same act. (D204, D241)
37. Behind Setup's *Daoris may change this checkout's files* (`checkoutWrites`, off by default): stage by file, hunk or line,
    commit, amend, resolve, switch, and stash save, apply or drop. A drop is offered only for a stash a done Save of Daoris's own
    made, found by the commit its journal names; where git names another commit it stores that one again and reports not done.
    It has no Undo: its record keeps the commit and `git stash store -m <message> <id>`. (D204)
38. Every act takes one checkout lock, keyed by the common directory and held alone, and runs through the act journal; an act
    inside a flow already holding that lock runs under it. A reconciler settles an open act; nothing is re-run. Every branch
    delete is the journal's `ref.delete` act with its *Undo*: the person's `GIT_REF_DELETE` and every door of Daoris's
    (`DISCARD_SESSION_BRANCH`, `WITHDRAW_LANDING_BRANCH`, `SWEEP`, the look's clean-up, a removal, a run's `tidy`) through
    `ContentHolds.DeleteJudgedAsync` and `JournaledDeletes`, pressed by `daoris`, listed from the journal alone (`GIT_DELETES`).
    Each door keeps its judgement; the recovery ref its rule keeps is made in the delete's own transaction, and a door that keeps
    without deleting makes it in one `ref.keep` act, the record naming and dating it. A ref the transaction leaves out is gone only
    on `show-ref --exists`'s word; a branch at neither the judged commit nor gone is said as moved since judged, or unread. A
    `tidy` judges nothing itself: each part's delete rests on the recovery ref it kept. (D204, D228, D245)
39. Always refused: a hard reset, `clean`, any force, a remote delete, `push -u`, an act while a session runs there or a landing
    holds it, a delete or rename of Daoris's own branches, and any write into a session's tree. (D204)
40. Every write says how it recovers: deletes under `refs/daoris/discarded/`, index snapshots and Resolve's stages under
    `refs/daoris/acts/`; git's own lock files are named, never removed. A discarded ref no journal record names is unheld:
    `daoris work recovery drop --unheld --plan` counts them (`RecoveryCensus`), saying each one's age, commits and what its work is
    to the repository's line (`RecoveryWorks`), and `--unheld` drops only those whose work the line holds. (D204)
41. The person's fetch is fixed: origin's heads into `refs/remotes/origin/*`, no tags, no submodules, prune; a setting that runs
    another program or mirrors refuses it. (D204)
42. The page's reads use Tools' git, the one agents run, in machine formats, cached by full commit id; nothing is written into
    `.git`. Git is a part of Repositories, not a place of its own. (D147, D150)

**Remotes and record sync**

43. A remote is the same HTTP host in shared mode, serving one workspace. `remotes.json` maps workspace to `{url, key}`; the
    `DAORIS_REMOTE_*` variables override; half a pair is no remote. (D47, D48, D63)
44. The Remotes screen and `daoris remote` edit that one file. A key goes in and never comes out. Nothing local needs a remote.
    (D47, D50)
45. A shared host gates every route with per-person, per-machine expiring keys. A local host trusts loopback and binds nothing
    else. (D47)
46. A wired workspace's pass, on the tick or *Sync now*: registrations up, each sharing repository's content by commit with its
    line, retires, the team's registry mirrored; then the host syncs quests and records. (D47, D68, D69, D86)
47. What leaves a machine is what its manifest declares; silence is local. The feed carries no roots, transcripts or private
    content; knowledge feeds as content, never vectors; processes never sync. (D47, D21)
48. Every quest verb commits and succeeds locally; the remote orders the operations. A take is pushed and awaited before work;
    offline it stays unconfirmed and the session runs. (D68, D69)

**Two machines on one quest**

49. First push wins. A take that reached the remote second is a `conflict`; its machine's driver stops the session and keeps its
    tree, branch and transcript. Nothing merges two claims. (D68, D178)
50. On the losing machine the person may *Drop mine* or *Bring mine forward*: a fresh take and a marked dismissal in one
    transaction, then the kept session goes on in its tree. It may lose again; Daoris never retries. (D178)
51. A take that reached the remote after the reopen stays a loss. The quest remote carries no git objects. (D178)

## Git's starts, counted

Every git the desktop starts runs in a place of `GitScheduler`'s; the start gate and `HeldChild` ask the start policy a root hands
the machine (`GitStartAdmission`), which refuses a git's image holding no place (`git.start.outside`) and binds any start made in a
place to it.

- Four run at once. The background and every git waiting outside Daoris (a remote act, a hook) hold three together, so a
  person's read always finds the fourth; one repository runs at most two background reads. A bridge route's gits are a person's.
- Asks queue per repository, a person's oldest that may go first. A write passes nothing queued ahead of it, nothing passes it,
  and it never shares; a read shares an identical queued start only with no write queued after it.
- A lock-free read (`GitEndable`) is dropped or ended once its askers leave; every other git runs to its end or its bound. A git
  outliving its kill is counted apart (`git.place.apart`) and its repository's background asks answer `stalled`.
- Each start is classed before it queues by one table of git's commands (`GitCommands`): a read, a write or a drop (a branch's tip
  put at risk), with what it touches; an unrowed start reads as a drop of every word it names, and `GitCommandsCensusTests` holds
  every literal start to a row.
- A scheduler refusal (`stalled`, `going`, `depth`) is git's failure with its reason, never an exception. An ask beneath a placed
  git (a push's hook, a diff's textconv) takes a place that git lends, two deep, told by its process ancestry (`GitCallers`). A
  git whose descendant holds its pipe answers at its exit after two seconds (`PipeGrace.Wait`).
- Lazy fetch is off in a repository with a promisor, for a git proved once to honour `GIT_NO_LAZY_FETCH`; an unproved git's reads
  there wait outside Daoris. A missing blob there is asked once more allowed to fetch.
- Every object read is one `cat-file --batch` question read by one parser (`GitObjectReads`); `GitObjectFormsCensusTests` is red on
  any other form. A commit's parents are git's history's, never its stored bytes.
- `GitStamp` is the one stamp of what a git answer rests on (the git in use, every configuration file git would read with its
  includes, the replacements, grafts, shallow and reftable lists, the variables), each file a `FileStamp`. `GitReadCache` keeps a
  graph page, a commit's fields and a compare's commits under it while its fence is unchanged; the flow memos share an answer
  between two folders only where its context is one (`GitStamp.ContextOf`).
- The declarations' store (`Workplace/Declarations/`) answers a declaration present, absent or unread, by `FileStamp`,
  `ContentKey`, or a commit's listing kept only where each object's bytes hash to its id; while a replacement can stand it asks
  git and keeps nothing. Names are fields of `DeclarationNames`; the banned-name analyzer (`BannedSymbols.txt`) and the
  declaration censuses hold that nothing else reads one.
- `GitKeepersCensusTests`, `GitLifetimesTests`, `GitEndingsCensusTests` and `GitStartsCensusTests` hold the classes, each run with
  every member left out and one planted omission; `GitSchedulerProcessTests` holds the scheduler on real git.

The refs' one listing and a plain read's merge-bases, counts and changed names are answered in git's words by LibGit2Sharp where
admitted (`GitLibraryReads`), starting nothing; everything else is git's.

- `GitLibrary` opens a `Repository` per call, eight at once, libgit2's search paths emptied. `GitLibraryAdmission` refuses, at each
  question, any format but SHA-1 objects with files refs, any replacement, a pack rewrite under way (`GitLibraryHousekeeping`,
  D252) and the settings that change an answer; a refused question is git's. `GitLibraryJournal` (`run/git/library.json`) keeps
  why, listed in Settings → Git as *Git reads*.
- libgit2 runs in the desktop's process, so a fault ends the window: each call writes its open and close to a lease file per
  process (`GitLibraryLeases`), and a repository a process died inside is refused (`native-fault`) until the library package
  changes.
- Each answer counts as a read (`GitCount.Reads`). `GitLibraryEndsTests` fails on each of the library's two ends (GITLIB1g). The
  library classes run on the runner's git and on the pinned MinGit (`driver-process-git-pinned`, `PinnedMinGit`). The `win-x64`
  publish lays `git2-*.dll` beside the driver.

Each flow's gits are counted (`GitLaunch.Count`) and held to these budgets by the `*GitCountTests` on their fixtures. *Again* is a
command asked again with no write between; *object* what one held `cat-file --batch` could answer, *by id* those naming an id.

| View or landing | Starts | Again | Object (by id) |
|---|---|---|---|
| Branches (`GIT_BRANCHES`, one repository) | 2 | 0 | 1 (0) |
| The checkout's branch (`CHECKOUT_BRANCH`) | 1 | 0 | 0 |
| Changes (`GIT_CHANGES`), a file's hunks (`GIT_HUNKS`) | 5 each | 0 | 1 (0) each |
| Graph, a file's history, blame (first open; kept by id after) | 1 each | 0 | 0 |
| A commit, a compare (first open) | 2 each | 0 | 0 |
| The commit, switch and stash plans | 8, 5, 7 | 0 | 1 (0) each |
| The deletes (`GIT_DELETES`, the journal holding none) | 0 | 0 | 0 |
| Review, a tree ahead of its line, its second open (one untracked file, or ten) | 10 | 0 | 0 |
| Review, a tree the line holds by content | 11 | 0 | 1 (0) |
| A landed session's review | 3 | 0 | 2 (1) |
| The Accept offer of a tree of work | 3 | 0 | 0 |
| The look's tidy, six trees (two) | 5 (5) | 1 (`worktree list`) | 1 (0) |
| A session's start, to its spawn | 20 | 0 | 3 (0) |
| A squash press's landing | 19 | 0 | 4 (1) |
| A merge press's landing | 32 | 3 (the line's commit) | 9 (0) |

A held reader per repository does not earn its place: a view asks at most three object questions, and a landing reads its line
commit and head again because another process can move either. The measurements behind these figures are D147's note of
2026-11-12; the bench (`tools/git-library-bench`) and the library's further reads are `docs/2026-11-09-git-library-design.md`.

## Where a change goes

- How an act is carried or judged: the doors (`GitDoors`, `GitRouting`, `GitWiring`, compiled into `Daoris.Desktop.GitDoor`),
  never a `pre-push` hook, a credential helper or the harness's rules alone.
- Who an act is: `GitIdentities`, `GitIdentityUse`; the secret-holding acts in `GitIdentityActs`, `GitIdentityHttps`,
  `GitIdentityTokens`; the page in `settings/git/`.
- A failure or hold: `GitFailures`, `GitHolds`, `GitOwnInput`, with its row words.
- What an act may do: `GitAccessKeys`, `GitAccessRules`; what a harness is told, `GitAccessPolicy`.
- A person's act: a step kind on the act engine, a `Git*Command` twin, a `GitRoutes` route, its rows in `PipeTargets` and
  `PipeCensus`.
- A platform's push or pull request: the plugin, in the plugins repository; the driver changes only the wire (`LandingPlugins`).
- A record that syncs: `IRemote` and a `*Sync` in `Service.Core/Remote/`, and the host's routes; machine wiring through
  `RemoteOperations`; `RemoteTarget` and `RemoteConfig` are twins whose test tables must match.
- Quest conflicts: `QuestLog`, `QuestExchange` (service), `Planner` (driver), the quest page.

Tests are each unit's `<Unit>Tests` and `*ProcessTests`, and the censuses (`ProcessStartsCensusTests`,
`CheckoutWritersCensusTests`); the doors' process classes also run on the pinned MinGit. Their remotes are stand-ins on loopback
ports with nothing installed (D241 point 8): `GitSshHost` (RSA and ECDSA, never ed25519), `GitTlsHost`, `PullRequestApiHost`.
Gates: `driver`, `driver-process-git`, `driver-process`, `censuses`, `modules`, `service`, `web-unit`, `web-types`,
`deployment`; visible words need both catalogues.

## Contradictions

- D47 (one home per quest, written through) vs D68 and D69 (local commits, fetch-rebase-push): D68.
- D109 §1 and D147 point 3 (the line fetch with the person's credentials) vs D198 and `TreeRefs`' fetch through the doors: D198.
- D147 point 1 (Git a place on the activity bar) vs D150 (Git inside Repositories): D150.
- D204's head ("proposed, nothing built") vs its 2026-10-31 acceptance note and `ActRuns`: the note.
- D87 and D100 (the plugin pushes as the platform's tools are signed in) vs D241 point 4 (Daoris's push act under a grant; the
  plugin opens the request): D241.

## Open

- **D277**: a permission proposal naming a push is refused at the proposal, naming the place's Write and its verb; not built.
- **GITDESIGN1**: G8-CONVERT1 (the old deliveries' reading deleted, rule 30) and G10; **WORKSETUP1** follows it.
- **GITAUTH1** c, a key added on a real host and a real fetch, the git setting's doors waiting on Settings → Tools; h, the
  install seen.
- **ONEWAY1af**: once the unheld census reads zero on the install, `RecoveryRetention`, `RecoveryDropping` and the census go.
- **PERF1**: the cuts the counts name, and the other test classes that start git counted.
- **GITLIB1g**: the library's two ends.
- **FABRIC-PERFORM1l**: the pull request's fields kept on the wire evidence (`WorkEvidence`).
- The identity's `api` token setter, read only by the adoption, is the next removal (no row).
- D178 point 6, *Hand mine to the winner*, and D47's OIDC sign-in on a shared deployment: no row until asked for.

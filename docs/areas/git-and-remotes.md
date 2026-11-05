# Git and remotes — how it works now

## What it does

Daoris's git reaches code hosts as git identities of its own. It hands no credential of the person's unless the person chooses it.
Every process Daoris starts gets its git from `GitWiring`: nothing prompts, and every remote act passes Daoris's doors.
The ssh door carries SSH; the remote helper carries HTTP and HTTPS through a `daoris::` rewrite.
At each act a door finds the repository, applies the identity its remote points at, and asks one judge about the act's class.
A remote that cannot authenticate is held, and said once in *What needs you*, until something changes it.
The person's own git acts (fetch, refs, history edits, staging, commit, switch, stash, resolve) run through one journal under one lock.
Apart from code hosts, a workspace may be wired to a shared deployment of the service. Quests, session records, messages,
registrations and code maps sync with it as fetch, rebase, push. Two machines that take one quest end as a git conflict does.
A pull request is pushed and opened by a landing plugin, on the person's yes. Daoris itself pushes nothing.

## Its parts

Driver paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/`; `Workplace/` is implied for bare `Git*` names.

| Unit | File | Its job |
|---|---|---|
| Wiring | `GitWiring.cs`, `GitRouting.cs` | Hands every start the no-prompt variables, strips askpass, trace, token and the inbox's two messaging variables (each executable's entry strips them too), names the doors where laid |
| The doors | `GitDoors.cs`, `GitCredentialDoor.cs`, `src/Daoris.Desktop/Daoris.Desktop.GitDoor/Program.cs` | `daoris-git-ssh.exe`, `git-remote-daoris.exe` and `git-credential-daoris.exe`: class each act by its remote command, judge it, apply the identity; the third answers an identity's token to git for its own host |
| Which repository, which address | `GitFolders.cs`, `GitRemotes.cs`, `GitRemoteRows.cs` | The repository from the act's folder; the one address reader; Setup's remote rows and what serves each |
| Identities and pointers | `GitIdentities.cs`, `GitIdentityUse.cs` | `data/git-identities.json`; `gitIdentity`, `workspaceGitIdentity`, `machineGitIdentity` in `driver.json`; resolution with its source |
| Identity acts | `GitIdentityActs.cs`, `GitIdentityHttps.cs`, `GitIdentityTokens.cs`, `GitSshFiles.cs`, `GitHostKeys.cs` | Generate or import a key, add an HTTPS identity with its token and test it, set or clear an API token, Daoris's `ssh_config` and `known_hosts`, host-key compare |
| Secrets at rest | `GitTokens.cs`, `GitPrivateFolder.cs`, `GitKeys` in `GitIdentities.cs` | `profiles/git/tokens/<id>/` (`https`, `api`) beside `profiles/git/keys/<id>/`, each folder made limited to the person's account before a byte is written |
| Identity verbs | `GitIdentityCommand.cs` | `daoris git identity` and `git use`; a widening pressed through the running window |
| Author | `GitAuthor.cs` | The `commitsAs` of origin's identity on a session's or Daoris's commit |
| Config and floors | `Install/GitGlobalFile.cs`, `Install/GitVersions.cs`, `GitForWindows.cs` | `profiles/git/global.gitconfig`; the version floors; Git for Windows' launcher started past |
| Failures and holds | `GitFailures.cs`, `GitHolds.cs`, `GitOwnInput.cs` | Class a failure; hold in `run/git/held.json`; find `own-input` and `no-identity` before the act |
| The move | `GitMove.cs`, `GitMoved.cs` | Where all three doors are laid, a remote nothing serves gets no credential of the person's; the ssh door's one line |
| Redaction and log | `GitWords.cs`, `GitLog.cs`, `GitUnended.cs` | One redaction for every git, ssh, door or plugin text; coded `git.*` machine-log lines |
| Git access | `GitAccessKeys.cs`, `GitAccessActs.cs`, `GitAccessCommand.cs` | The three levels in `driver.json`, their resolution, `SET_GIT_ACCESS`, `git access` |
| The judge, grants | `GitAccessRules.cs` (`GitGrants`), `GitGrantRef.cs` | One verdict per act; a grant in `run/git/grants` bound to its address and its refs (ref, new commit, old commit where known) |
| A push's updates | `GitPushUpdates.cs`, `GitPushJudge.cs`, `GitPushDoor.cs`, `GitHelperPush.cs` | Each update read and kinded (plain, force, delete, tag, mirror, prune); the batch judged by grant and Dangerous; the helper's remote refs and objects |
| A session's record | `GitSessionAccess.cs`, `GitSessionAccessVerb.cs`, `Modules/Bridge/Routes/SessionGitAccessRoutes.cs`, `src/Daoris.Web/src/work/head/GitAccessFact.tsx` | The access a running session holds, tightened and read; `git access --session`; `TIGHTEN_SESSION_GIT_ACCESS`; the head's *Git access* fact |
| A session holds nothing | `GitSessionHolds.cs`, `AgentWorkplaceWiring.cs`, `Loop/Session/SessionRules.cs` | Token variables dropped, `gh` and `az` folders empty, the harness's deny on `profiles/git/` |
| Harness rules | `GitAccessPolicy.cs` | `no-push`'s rules and classifier sentence composed from a session's Write |
| Go-aheads | `GitAsks.cs`, `GitDoorAsks.cs` | A door's `ask` as `run/git/asks/{id}.json`, its answer and its late grant; held only while Daoris runs |
| `no-push` converted | `Transitional/Migration/GitPushStep.cs` | The home's migration: a switched-off `no-push` as the machine's Write `allow` and `no-publish` off |
| The act engine | `ActRuns.cs`, `ActJournal.cs`, `ActReconciler.cs`, `ActSteps.cs`, `RefTransaction.cs` | Checkout lock, journal, steps in a named job, postconditions, recovery refs |
| The person's acts | `GitFetchActs.cs`, `GitRefActs.cs`, `GitHistoryEdits.cs`, `StageActs.cs`, `ActCommits.cs`, `GitConflicts.cs`, each `Git*Command.cs` | Fetch, refs, pick/revert/rebase, stage, commit, resolve, switch, stash; each with its terminal twin |
| Reads | `GitReads*.cs`, `GitBranches*.cs`, `GitLaunch.cs`, `GitReadCache.cs` | The page's git reads through Tools' git, cached by commit id |
| Line fetch, bring up to date | `TreeRefs.cs`, `TreeSync.cs` | `fetch origin <line>` past the doors and holds; ff-only, replay, delete what merged |
| Landing plugin wire | `Plugins/LandingPlugins.cs` | `work/land` with origin's address, the judge before the plugin, `work/state` asks |
| Pull request state | `PullRequestAsks.cs`, `PullRequestStates.cs`, `PullRequestMoments.cs`, `PullRequestBounds.cs` | When a pull request is asked about, its minute, what a completed answer clears |
| Window doors | `Modules/Bridge/Routes/GitRoutes.cs`, `ToolsRoutes.cs`, `LinesRoutes.cs`, `Modules/Platform/RemotesModule.cs` | Git reads and acts; `GIT_IDENTITY`, `GIT_USE`, `GIT_ANSWER`; `SET_GIT_ACCESS`; the Remotes screen |
| Page | `src/Daoris.Web/src/settings/git/`, `settings/repository/GitAccess.tsx`, `work/waits/GitAsk.tsx`, `projects/git/RepositoryGitView.tsx` | Settings → Git, Setup's remote and access rows, the go-ahead, Git inside Repositories |
| Remote wiring, the pass | `Service/RemoteTarget.cs` (`RemoteFile`), `Service/RemoteOperations.cs`, `Service/RemoteSync.cs`, `Daoris.Desktop.Driver.Host/RemoteConsole.cs` | `remotes.json`; `daoris remote`; one pass per wired workspace |
| Host sync, conflicts | `src/Daoris.Service/Daoris.Service.Core/Remote/` (`Remote`, `QuestSync`, `SessionSync`, `ChannelSync`, `CodeMapSync`, `RemoteConfig`), `Quests/QuestLog.cs`, `QuestExchange.BringForwardAsync`, `WorkOrders/Domain/Planner.cs` | Fetch, rebase, push of quests and records; conflicts and bring-forward |

## Its rules

**Identities**

1. An identity is a host, a way (an SSH key or an HTTPS token), a label, the user the host reports and an optional
   `commitsAs`. Several per host. `data/git-identities.json` holds no secret. (from D198)
2. No scope holds a list. A repository points per remote, a workspace per host, the machine may choose `own` per host, and
   a host has a default per way. Resolution is repository, workspace, machine, default; the answer names its source. (from D198)
3. A private key lives in `profiles/git/keys/<id>/`, and an identity's tokens in `profiles/git/tokens/<id>/` (`https`, the token
   git's HTTPS act carries; `api`, the token a hosted service's act is handed, on any identity), each folder limited to the
   person's account before a byte is written, plaintext at rest. A token goes in through the identity's doors (the route, the
   terminal's `git identity token --token-stdin`, the page) and is never said back: only whether one is set. (from D198, D67, D241)
4. A host key is trusted only at the person's press, after a compare with the host's published keys. Host keys live in
   `profiles/git/known_hosts`. (from D198, D73)
5. The person's `~/.ssh`, ssh agent and ssh configuration are never read or written. The ssh door gives its ssh
   `-F profiles/git/ssh_config`. The one read of the person's own is a key file the person names to import. (from D198)
6. A commit Daoris or a session makes carries origin's identity's `commitsAs` only where the repository names no author.
   Authorship is apart from authentication. (from D198)

**Transport**

7. Every start passes `Tools.Hand`, which calls `GitWiring.Hand`: `GIT_TERMINAL_PROMPT=0`, an empty `GIT_ASKPASS`,
   `GCM_INTERACTIVE=never`, `GIT_TRACE_REDACT=1`. A grant is never inherited. The terminal's shells keep the person's git. (from D198)
8. Where the doors lie in the install's `app/bin/`, SSH runs `daoris-git-ssh.exe` by a bare path in `GIT_SSH_COMMAND`
   (else `GIT_SSH`), and HTTP/HTTPS reach `git-remote-daoris.exe` by `insteadOf` in the command scope. No doors, no routing.
   Under an identity the helper starts git's own transport with `credential.helper` emptied and then `daoris`, the install's
   folder first on that transport's `PATH` and `DAORIS_GIT_IDENTITY` naming the identity: `git-credential-daoris.exe` answers
   `get` with that identity's `https` token and user for the identity's own host and nothing for any other host, and changes
   nothing on `store` or `erase`. A remote nothing serves, or no credential door laid, reads anonymously. (from D198, D241)
9. Every publish lays all three doors in `app/bin/`; no flag asks for them. The deployment rehearsal proves their layout.
   (from D198, D241)
10. A door finds the repository by the act's own working folder, so `git -C`, a clone or a submodule resolves for itself.
    (from D198)
11. Under an identity the door removes the person's authentication inputs: helpers, extra headers, cookie files, certificate
    variables, `_netrc` (by an empty home), an address's user-info. An address carrying a password is refused. (from D198)
12. A session holds no credential. Its start drops `GH_TOKEN`, `GITHUB_TOKEN`, `GH_ENTERPRISE_TOKEN` and `AZURE_DEVOPS_EXT_PAT`, is
    handed no grant and no key path, and points `GH_CONFIG_DIR` and `AZURE_CONFIG_DIR` at empty folders of Daoris's
    (`run/git/sessions/empty/`), so its `gh` and `az` never reach the person's sign-ins. The Claude doors' rules deny reading
    `profiles/git/`; Codex holds no rules. The session still runs as the person's account (`GitSessionHolds`). (from D198, D241)
13. One reader, `GitRemotes`, gives every address Daoris keeps, says or judges, with `daoris::` taken off. A plugin tool
    that reads remotes is named the repository or runs unrouted. (from D198)
14. Below git 2.31 the doors cannot stand: Daoris's fetches and the landing's push refuse as `old-git`. Below 2.32 a git
    setting refuses as `git-too-old`. (from D198)
15. Every text from git, ssh, a door or a plugin passes `GitWords.Redact` before it is kept, said or answered. (from D198)

**The move and the holds**

16. A home has moved where all three doors lie in the install's `app/bin/` (`GitMove.Moved`, `GitMove.Missing` names the
    absent); no mark is kept. A moved install reaches a host with its identity's key or token, or with none: a remote nothing
    serves is held `no-identity` before SSH connects, and HTTPS reads anonymously. A home with no install beside it (a run
    from source), or an install missing a door (its fetch is not offered, the words naming the door), is not moved and its git
    is as it was. Nothing is pre-applied. (from D198, D232, D233, D241)
17. The move is one *What needs you* row per workspace: *Set up identity…* or *Keep using my own sign-ins…*. A held row
    holds work where a session of one of its repositories runs or is parked. (from D198)
18. `own` is a widening. It is confirmed before it is written, on the page or in the window for a terminal's press. No
    migration applies it. (from D198)
19. A failure is classed by the transport's words and held, keyed by repository, effective address, what served it and
    whether it holds reads or writes. Network and unclassed failures hold nothing. (from D198)
20. A hold lifts on a change of an identity, default or pointer, on a passing act, or on *Try again* (`GIT_TRY_AGAIN`).
    A session is told in its Git section not to try. (from D198)
21. `own-input` is found before the act: a `core.sshCommand` that would outrank `GIT_SSH`, or an HTTPS certificate, key,
    header or cookie file in configuration where an identity serves. It holds until the setting goes or `own` is chosen. (from D198)

**Access and go-aheads**

22. Git access has three classes, Read, Write and Dangerous, each `allow`, `ask` or `deny`, set in `gitAccess`,
    `workspaceGitAccess` and `machineGitAccess`. Each class resolves repository, workspace, machine, then the default. (from D198)
23. The default is Read `allow`, Write `as-today` (the landing pushes on the person's yes; a session on any harness does
    not), Dangerous `deny`. Dangerous is set on the page, in `git access` and by `SET_GIT_ACCESS` to `allow` or `deny`, never
    `ask`: no person is waited on for it, and a stale `ask` in the file reads as `deny`. (from D198, D72, D241)
24. Git access is apart from `access`: full access never touches it. A loosening is a widening and is confirmed. (from
    D198, D184)
    A running session holds a record, `run/git/sessions/{id}.json`, begun at its start with its repository and workspace: what it
    holds is its repository's access now, narrowed by the classes the person tightened for it (*Tighten…* in the head's *Git access*
    fact, `TIGHTEN_SESSION_GIT_ACCESS`, `git access --session <id> write=deny`). A tightening only narrows, needs no confirmation, and is
    never lifted by a later loosening of the repository. Both doors judge a session's act by it, by the marker `DAORIS_SESSION_ID`
    (`GitSessionAccess.Held`); `git access --session <id>` reads it, and the driver's state carries every record as `sessionGitAccess`.
    A record is kept 14 days. (from D241)
25. The doors judge by the remote command: `push`, `list for-push` and `receive-pack` are Write. Each door hands the judge
    the identity serving the act; one read only on its host refuses every Write through it, a grant's included, in its
    own line. `deny` refuses and no grant lifts it; `allow` passes; `ask` holds the act at the door. Each update of a push is
    judged by its kind at the doors for every agent whatever its harness: the ssh door reads receive-pack's commands before it
    forwards them; the helper holds git's `push` lines to the batch's end, reads the remote's refs from the `list for-push`
    answer, resolves each source to its object and forwards the objects it judged (`GitPushUpdates`, `GitHelperPush`). A delete,
    a force (the old commit is not an ancestor of the new, asked of git), a tag, a mirror (a ref outside `refs/heads/`) and a
    prune (a delete beside updates) are Dangerous: the place's Dangerous value, `allow` or `deny`, decides, unless the grant
    carries the update. One refused update refuses the batch before any ref moves (`GitPushJudge`, `GitPushDoor`). (from D198, D233, D241)
26. A held `ask` is a git go-ahead, `run/git/asks/{id}.json`, answered *Allow* or *Refuse* in *What needs you*, the
    session's head or `git answer`. The door waits up to 15 minutes; a late allow is a grant the identical retry takes once.
    Where no instance of Daoris (the desktop or a headless driver) holds the home's lease, the door takes a kept grant or
    refuses at once, saying Daoris is not running to ask (`not-running`). (from D198, D135)
27. The judge reads git access alone. `no-push` is a fixed default: its rules are on for every session and only git access's
    Write opens them; it has no switch on the page, in `agent rules` or in a proposal. `no-publish` carries the publishing
    and release sentence and is switched alone. The home's migration converted a switched-off `no-push`: the machine's Write
    became `allow` where it was unset, and `no-publish` went off. (from D198)
28. A session's harness rules follow its Write. `deny` or the default keeps every push form denied; `ask` or `allow` opens
    the plain push and still denies force, delete, tag, mirror and prune unless Dangerous is `allow`. Codex takes no rules file and holds none of the
    person's denies; its block and the access setup say so. (from D198, D72, D233)
29. Identity is who, access is what. A read-only identity refuses writes at both doors and at the landing. (from D198)

**Landing and pull requests**

30. Daoris pushes nothing and opens no pull request. A landing rule names a plugin that declares `work/land`; it pushes
    and opens the pull request after Daoris made the branch. With no plugin named, nothing pushes. (from D87, D100)
31. The landing asks the judge before it starts the plugin. The person's Accept, or a rule that accepts for them, is the
    yes, so only `deny` or a read-only origin identity refuses. The doors refuse the same push; the landing asks first
    because the plugin's start and its pull-request call pass no door. (from D198, D145)
32. The plugin's push carries a grant bound to its process, origin's push address and exactly its own ref and commit
    (`refs/heads/<branch>` at the frame's tip), taken once within the bound by the push that it carries; it carries a creation or a
    fast-forward and never a Dangerous kind, and a ref it does not name is refused at the door. A grant with the old commit
    bound carries that exact update whatever its kind. The frame names origin's address; the plugin has two minutes and answers
    `{pushed, pullRequest, message}`. (from D100, D198, D241)
33. A pull request's state is asked only of the plugin that pushed it, at occasions that may remove, never on a timer. A
    `completed` answer clears a branch only where git holds its merge commit on the line. (from D148)

**The line**

34. Daoris's own fetch of a line is `fetch origin <line>` through the doors. It is refused on an old git, held on
    `own-input` or a standing hold, and counted for the pull request minute. It moves only origin's refs. (from D109, D198, D148)
35. Bring up to date is listed, then pressed: fast-forward only, replays in Daoris's own trees, local deletes of what
    merged. Never a merge commit, a force or a push. (from D109, D112)

**The person's acts**

36. The person's acts are a fetch of origin's branches, a branch or lightweight tag made, renamed or deleted, and pick,
    revert or rebase onto a branch no tree holds. (from D204, D147)
37. Behind Setup's *Daoris may change this checkout's files* (`checkoutWrites`, off by default): stage by file, hunk or
    line, commit, amend, resolve, switch and stash save or apply. (from D204)
38. Every act takes one checkout lock, keyed by the common directory and held alone, and runs through the act journal.
    A reconciler settles an open act; nothing is re-run. (from D204)
39. Always refused: a hard reset, `clean`, any force, a remote delete, `push -u`, an act while a session runs there or a
    landing holds it, a delete or rename of Daoris's own branches, and any write into a session's tree. (from D204)
40. Every write says how it recovers: deletes under `refs/daoris/discarded/`, index snapshots and Resolve's stages under
    `refs/daoris/acts/`. git's own lock files are named, never removed. (from D204)
41. The person's fetch is fixed: origin's heads into `refs/remotes/origin/*`, no tags, no submodules, prune. A setting
    that runs another program or mirrors refuses it. (from D204)
42. The page's reads use Tools' git, the one agents run, in machine formats, cached in memory by full commit id. Nothing
    is written into `.git`. Git is a part of Repositories, not a place of its own. (from D147, D150)

**Remotes and record sync**

43. A remote is the same HTTP host in shared mode, serving one workspace. `remotes.json` maps workspace to `{url, key}`;
    the `DAORIS_REMOTE_*` variables override; half a pair is no remote. (from D47, D48, D63)
44. The Remotes screen and `daoris remote` edit that one file. A key goes in and never comes out. Nothing local needs a
    remote. (from D47, D50)
45. A shared host gates every route with per-person, per-machine expiring keys. A local host trusts loopback and binds
    nothing else. (from D47)
46. A wired workspace's pass, on the tick or *Sync now*: registrations up, each sharing repository's content by commit
    with its line, retires, the team's registry mirrored; then the host syncs quests and records. (from D47, D68, D69, D86)
47. What leaves a machine is what its manifest declares; silence is local. The feed carries no roots, transcripts or
    private content. Knowledge feeds as content, never vectors. Processes never sync. (from D47, D21)
48. Every quest verb commits and succeeds locally; the remote orders the operations. A take is pushed and awaited before
    work; offline it stays unconfirmed and the session runs. (from D68, D69)

**Two machines on one quest**

49. First push wins. A take that reached the remote second is a `conflict`; its machine's driver stops the session and
    keeps its tree, branch and transcript. Nothing merges two claims. (from D68, D178)
50. On the losing machine the person may *Drop mine* or *Bring mine forward*: a fresh take and a marked dismissal in one
    transaction, then the kept session goes on in its tree. It may lose again; Daoris never retries. (from D178)
51. A take that reached the remote after the reopen stays a loss. The quest remote carries no git objects. (from D178)

## Where a change goes

- **How an act is carried or judged:** the doors (`GitDoors`, `GitRouting`, `GitWiring`), compiled into
  `Daoris.Desktop.GitDoor`. Never a `pre-push` hook, a credential helper or the harness's rules alone.
- **Who an act is:** `GitIdentities` and `GitIdentityUse`; the secret-holding acts in `GitIdentityActs`, `GitIdentityHttps` or
  `GitIdentityTokens`, a secret's folder in `GitTokens` or `GitKeys`; a page change in `settings/git/`.
- **A new failure or hold:** `GitFailures`, `GitHolds` or `GitOwnInput`, with its row words where `git-held` rows are made.
- **What an act may do:** `GitAccessKeys` and `GitAccessRules`; what a session's harness is told in `GitAccessPolicy`.
- **A new person's act:** a step kind on the act engine, a `Git*Command.cs` twin, a `GitRoutes` route, and its rows in
  `OwnerTargetRows` and `PipeCensus`.
- **A platform's push or pull request:** the plugin, in the plugins repository. The driver changes only the wire
  (`LandingPlugins`, the hook points).
- **A record that syncs:** `IRemote` plus a `*Sync` in `Service.Core/Remote/`, and the host's routes. Machine wiring goes
  through `RemoteOperations`; `RemoteTarget` and `RemoteConfig` are twins whose test tables must match.
- **Quest conflicts:** `QuestLog` and `QuestExchange` (service), `Planner` (driver), the quest page.

**Tests that pin the area:** `GitDoorsTests`, `GitDoorStartTests`, `GitDoorsProcessTests`, `GitPromptsProcessTests`,
`GitSshProcessTests`, `GitSshHoldsProcessTests`, `GitTlsProcessTests`, `GitPushUpdatesTests`, `PullRequestApiHostTests`, `GitHttpsTests`, `GitHttpsProcessTests` (an HTTPS identity's token on the TLS host with nothing beside git, the credential door's refusals), `GitIdentitiesTests`, `GitIdentityCommandTests`,
`GitSessionAccessTests`, `GitSessionHoldsTests`, `DriverModuleSessionGitAccessTests`, `GitMoveTests`, `GitHoldsTests`, `GitOwnInputScopedGitTests`, `GitAccessTests`, `GitPushStepTests`, `GitRemotesTests` (the address reader's
census), `ProcessStartsCensusTests` (every start through `Tools.Hand`), `GitRefActTests`, `GitHistoryEditTests`,
`GitFetchTests`, `CheckoutWritersCensusTests`, `PullRequestStateTests`, `PullRequestProofTests`,
`PullRequestPresetPluginTests`, `RemoteSyncTests`, `RemoteTargetTests`, `QuestSyncTests`, `QuestReopenSyncTests`,
`SessionSyncTests`, `BroughtForwardLookTests`, `RemotesModuleTests`, `DriverModuleGitAccessTests`; on the page
`Git.test.tsx`, `GitRemotes.test.tsx`, `GitAccess.test.tsx`, `GitAccessFact.test.tsx`, `GitAsk.test.tsx`, `ProjectsView.git.test.tsx`.

**The remotes they reach** are stand-ins in the test process on free loopback ports, one per test class, with nothing installed
(D241 point 8): `GitSshHost` (an SSH host from Microsoft's dev-tunnels library serving `git-upload-pack` and `git-receive-pack`,
verifying RSA and ECDSA keys, never ed25519), `GitTlsHost` (`GitHttpBackendHost` behind TLS, its certificate trusted per command, asking Basic where a test sets `Basic`)
and `PullRequestApiHost` (GitHub's and Azure DevOps' pull-request calls, admitting a test's token). Each counts what it was asked.

**Gates that bite:** `driver`, `driver-process-git` (real git), `driver-process` (the doors' processes), `censuses`,
`modules`, `service`, `web-unit`, `web-types`, and `deployment` (the rehearsal publishes the install with all three doors). Visible words need both
catalogues (`i18n:check`).

## Contradictions

- D47's one home per quest, written through, vs D68 and D69's local commits and fetch-rebase-push: D68 reads right.
- D109 §1 and D147 point 3 (the line fetch runs with the person's credentials and helper) vs D198 and `TreeRefs.cs:128`
  through `GitRuns.cs` (`GitStart`): D198 and the code read right. `WorkingTree` is the door to git's reads and runs; the diff reads are
  `TreeDiffs` and the process starts, with their test seams, are `GitRuns`. A set-aside folder's marks, reads and keeping are
  `SetAsideMarks`, `SetAsideReads` and `SetAsideKeeps`, with `TreeSetAside` the front.
- D147 point 1 (Git a place on the activity bar) vs D150 and `ProjectsView.tsx:44` (Git inside Repositories): D150 reads right.
- D147 points 4-5 (`daoris git push`, the person's push) vs `Workplace/GitBranchesCommand.cs`' mark (no push verb): the code
  reads right; the push is GITAPP2g, open.
- D204's head ("proposed, not yet read; nothing built") vs its 2026-10-31 acceptance note and the built acts
  (`ActRuns.cs:67`): the note and the code read right.
- D87 and D100's plugin push "with whatever that platform's tools are signed in as" (still said at `LandingPlugins.cs:8-10`)
  vs D198 point 13 and `LandingPlugins.cs:122-131` (its git push passes the door under origin's identity and a grant): D198 reads right.

## Open

- **GITDESIGN1:** git's place, proposed in `docs/2026-11-05-git-design.md`: every remote act through the doors, one push, a pull
  request as a place's workflow steps, the path proven in CI against stand-in remotes. GITAUTH1b and WORKSETUP1 follow it.
- **GITAUTH1b:** the doors' classes run with the pinned MinGit beside the runner's git (G1's rest; the toolchain has no pinned
  MinGit fetch yet).
- **GITAUTH1c:** a key added on a real host and a real fetch (the steward's); the git setting's doors wait on Settings → Tools.
- **GITAUTH1e:** the sync card's screen door. The browser sign-in, its `PhaseList` and its Cancel left with Git Credential Manager (D241).
- **GITAUTH1h:** the install, seen (the owner).
- **GITAPP2g:** push one branch, on the person's press with a grant (after GITAUTH1f).
- **GITAPP2n:** push a lightweight tag (after GITAUTH1g). Stash Drop stays deferred. Ask Daoris's fetch card is not built.
- **D178 point 6:** *Hand mine to the winner*, no row until a way to publish a branch is asked for.
- **D47:** OIDC sign-in for people on a shared deployment, and the platform served from it; only machine keys are built.
- **FABRIC-PERFORM1l:** the pull request's fields kept on the wire evidence (`WorkWire.cs:354`).
- **OPTIMIZE1a:** git's starts counted per view, then a held reader per repository.

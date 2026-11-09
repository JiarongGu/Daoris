# Git and remotes — how it works now

## What it does

Daoris's git reaches code hosts as git identities of its own. It hands no credential of the person's unless the person chooses it.
Every process Daoris starts gets its git from `GitWiring`: nothing prompts, and every remote act passes Daoris's doors.
The ssh door carries SSH; the remote helper carries HTTP and HTTPS through a `daoris::` rewrite.
At each act a door finds the repository, applies the identity its remote points at, and asks one judge about the act's class.
A remote that cannot authenticate is held, and said once in *What needs you*, until something changes it.
The person's own git acts (fetch, push, refs, history edits, staging, commit, switch, stash, resolve) run through one journal under one lock,
and so does every branch delete Daoris's own doors judge.
Apart from code hosts, a workspace may be wired to a shared deployment of the service. Quests, session records, messages,
registrations and code maps sync with it as fetch, rebase, push. Two machines that take one quest end as a git conflict does.
Daoris's one push is the journal's push act under a grant bound to its ref and commits, on the person's press. A pull request is still
pushed and opened by a landing plugin, on the person's yes.

## Its parts

Driver paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/`; `Workplace/` is implied for bare names. A path opening with `Modules/`
is under `src/Daoris.Desktop/Daoris.Desktop.Modules/`.

| Unit | File | Its job |
|---|---|---|
| Wiring | `GitWiring.cs`, `GitRouting.cs` | Hands every start the no-prompt variables, strips askpass, trace, the holder key, the host token and the inbox's two messaging variables (each executable's entry strips them too), names the doors where laid |
| The doors | `GitDoors.cs`, `GitCredentialDoor.cs`, `src/Daoris.Desktop/Daoris.Desktop.GitDoor/Program.cs` | `daoris-git-ssh.exe`, `git-remote-daoris.exe` and `git-credential-daoris.exe`: class each act by its remote command, judge it, apply the identity; the third answers an identity's token to git for its own host |
| Which repository, which address | `GitFolders.cs`, `GitRemotes.cs`, `GitRemoteRows.cs` | The repository from the act's folder; the one address reader; Setup's remote rows and what serves each |
| Identities and pointers | `GitIdentities.cs`, `GitIdentityUse.cs` | `data/git-identities.json`; `gitIdentity`, `workspaceGitIdentity`, `machineGitIdentity` in `driver.json`; resolution with its source |
| Identity acts | `GitIdentityActs.cs`, `GitIdentityHttps.cs`, `GitIdentityTokens.cs`, `GitSshFiles.cs`, `GitSshProgram.cs`, `GitHostKeys.cs` | Generate or import a key, add an HTTPS identity with its token and test it, set or clear an API token, Daoris's `ssh_config` and `known_hosts`, host-key compare |
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
| The act engine | `ActRuns.cs`, `ActJournal.cs`, `ActReconciler.cs`, `ActSteps.cs`, `RefTransaction.cs` | Checkout lock, journal, steps in a named job, postconditions, recovery refs, an act's own ref (`ActRefs.cs`, `refs/daoris/acts/<act>`) |
| The person's acts | `GitFetchActs.cs`, `GitPushActs.cs`, `GitRefActs.cs`, `GitHistoryEdits.cs`, `StageActs.cs`, `ActCommits.cs`, `GitConflicts.cs`, `StashDrops.cs`, each `Git*Command.cs` | Fetch, push, refs, pick/revert/rebase, stage, commit, resolve, switch, stash (save, apply, drop); each with its terminal twin where one exists |
| The push | `GitPushActs.cs`, `ActPushSteps.cs`, `GitPushCommand.cs`, `src/Daoris.Web/src/projects/git/PushRef.tsx` | One ref to origin under a grant the step writes and drops; `GIT_PUSH`, `GIT_PUSH_PLAN`, `git push`; *Push* and *Push tag* |
| Every delete | `JournaledDeletes.cs`, `ContentHolds.DeleteJudgedAsync` | A delete a door of Daoris's judged, or forced, as the journal's `ref.delete` act pressed by `daoris`, undone by `GIT_REF_UNDO`; where the door's rule keeps the commits, the recovery ref is made in the delete's own transaction, its record names it, and the Undo takes it back |
| Every keep | `RecoveryKeeps.cs`, `RecoveryCensus.cs`, `RecoveryWorks.cs` | A recovery ref's one naming; a keep that deletes nothing as one `ref.keep` act; each ref a journal record names, dated from its act; the census of the refs no record names; what an unheld ref's work is to the repository's line |
| The deletes' list | `DeletedBranches.cs`, `GIT_DELETES`, `projects/git/DeletedRefs.tsx` | Every done `ref.delete` act of a repository from the journal alone, each with its Undo open or shut (undone, name stands again, commit gone), under the Branches tab in en and zh |
| The git scheduler | `Platform/GitScheduler.cs`, `Platform/GitPlaces.cs`, `Platform/GitPlace.cs`, `Platform/GitRunAsk.cs`, `Workplace/Git/GitStartAsks.cs`, `Workplace/Git/GitEndable.cs`, `Workplace/Git/GitMemoAsks.cs`, `Platform/GitCallers.cs`, `Modules/Platform/PipeGitCaller.cs`, `Workplace/Git/GitLazyFetch.cs`, `Workplace/Git/GitObjectReads.cs`, `Workplace/Git/GitObjectAnswer.cs`, `Workplace/Git/GitObjectName.cs`, `Workplace/Git/GitStartInputs.cs` | The one door every git of Daoris's passes: places for the run, a queue per repository, an identical queued start shared, a lock-free read nobody waits on ended, a removal admitted, re-entries told by the caller's ancestry and lent a place, an outlived git counted apart; the gate refusing a git's image with no place; lazy fetch off by a proof per git in use; every object read one `--batch` question through one parser; the memos partitioned by the asking tree's configuration |
| Reads | `GitReads*.cs`, `GitBranches*.cs`, `GitLaunch.cs`, `GitReadCache.cs`, `Workplace/Git/GitStamp.cs`, `Workplace/Git/GitConfigIncludes.cs`, `Workplace/Git/GitPackedRefs.cs`, `Workplace/Git/GitReplacements.cs`, `Platform/FileStamp.cs`, `Platform/VolumeClock.cs` | The page's git reads through Tools' git; what git reads from objects alone kept by commit id under the asker's stamp and its fence |
| Line fetch, bring up to date | `TreeRefs.cs`, `TreeSync.cs` | `fetch origin <line>` past the doors and holds; ff-only, replay, delete what merged |
| Landing plugin wire | `Plugins/LandingPlugins.cs` | Whether a plugin performs pull-request steps (`ProblemOf`, its manifest speaking on `work/perform`), and `work/state` asks |
| Pull request state | `PullRequestAsks.cs`, `PullRequestStates.cs`, `PullRequestMoments.cs`, `PullRequestBounds.cs`, `PullRequestRecords.cs`, `PullRequestWords.cs` | When a pull request is asked about, its minute, what a completed answer clears, where an opened one is written on its landing record, why one is not asked |
| Window doors | `Modules/Bridge/Routes/GitRoutes.cs`, `ToolsRoutes.cs`, `LinesRoutes.cs`, `Modules/Platform/RemotesModule.cs` | Git reads and acts; `GIT_IDENTITY`, `GIT_USE`, `GIT_ANSWER`; `SET_GIT_ACCESS`; the Remotes screen |
| Page | `src/Daoris.Web/src/settings/git/`, `settings/repository/GitAccess.tsx`, `work/waits/GitAsk.tsx`, `projects/git/RepositoryGitView.tsx` | Settings → Git, Setup's remote and access rows, the go-ahead, Git inside Repositories |
| Remote wiring, the pass | `Service/RemoteTarget.cs` (`RemoteFile`), `Service/RemoteOperations.cs`, `Service/RemoteSync.cs`, `Daoris.Desktop.Driver.Host/RemoteConsole.cs` | `remotes.json`; `daoris remote`; one pass per wired workspace |
| Host sync, conflicts | `src/Daoris.Service/Daoris.Service.Core/Remote/` (`Remote`, `QuestSync`, `SessionSync`, `ChannelSync`, `CodeMapSync`, `RemoteConfig`), the service's `Quests/QuestLog.cs` and `Quests/QuestBringForwards.cs` (reached by `QuestExchange.BringForwardAsync`), the driver's `WorkOrders/Domain/Planner.cs` | Fetch, rebase, push of quests and records; conflicts and bring-forward |

## Its rules

**Identities**

1. An identity is a host, a way (an SSH key or an HTTPS token), a label, the user the host reports and an optional
   `commitsAs`. Several per host. `data/git-identities.json` holds no secret. (from D198) An entry the file holds that this build does not read is kept as it stands by every write.
2. No scope holds a list. A repository points per remote, a workspace per host, the machine may choose `own` per host, and
   a host has a default per way. Resolution is repository, workspace, machine, default; the answer names its source. (from D198)
3. A private key lives in `profiles/git/keys/<id>/`, and an identity's tokens in `profiles/git/tokens/<id>/` (`https`, the token
   git's HTTPS act carries; `api`, the token a hosted service's act is handed, on any identity), each folder limited to the
   person's account before a byte is written, plaintext at rest. A token goes in through the identity's doors (the route, the
   terminal's `git identity token --token-stdin`, the page) and is never said back: only whether one is set. (from D198, D67, D241)
4. A host key is trusted only at the person's press, after a compare with the host's published keys. Host keys live in
   `profiles/git/known_hosts`. (from D198, D73)
5. The person's `~/.ssh`, ssh agent and ssh configuration are never read or written. The ssh door gives its ssh
   `-F profiles/git/ssh_config`. The one read of the person's own is a key file the person names to import.
   An identity's act, its key generation and its test run one ssh program by its full path (`GitSshProgram`): the OpenSSH the
   person chose in `tools.json`, else the `usr/bin/ssh` of the Git for Windows the act's git belongs to (so its key formats and
   `known_hosts` agree with that git's), else the first `ssh` on `PATH`; Settings → Git shows it on each SSH identity. (from D198)
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
14. Below git 2.31 the doors cannot stand: Daoris's fetches and the push act refuse as `old-git`. Below 2.32 a git
    setting refuses as `git-too-old`. (from D198)
15. Every text from git, ssh, a door or a plugin passes `GitWords.Redact` before it is kept, said or answered. (from D198)

**The move and the holds**

16. A home has moved where all three doors lie in the install's `app/bin/` (`GitMove.Moved`, `GitMove.Missing` names the
    absent); no mark is kept. A moved install reaches a host with its identity's key or token, or with none: a remote nothing
    serves is held `no-identity` before SSH connects, and HTTPS reads anonymously. A home with no install beside it (a run
    from source), or an install missing a door (its fetch is not offered, the words naming the door), is not moved and its git
    is as it was. Nothing is pre-applied. (from D198, D232, D233, D241)
17. The move is one *What needs you* row per workspace for every remote nothing serves, whatever its host (its repositories
    counted, those whose own configuration names authentication listed apart). It opens Settings → Git on the host, where an
    identity is added; a row whose push was refused or whose repository names its own authentication opens that repository's git
    row, where another identity or `own` is chosen. A held row holds work where a session of one of its repositories runs or is
    parked. (from D198)
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
    carries the update. A grant names an update by its ref, its new commit and its old one where bound, and Write passed on a grant reaches
    only the updates it names; it carries a Dangerous kind past the place's value only with its old commit bound, and never a tag. One
    refused update refuses the batch before any ref moves (`GitPushJudge`, `GitPushDoor`). (from D198, D233, D241)
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

30. Daoris's one push is the push act (rule 36); it opens no pull request. A workflow's `push` node performs it (`PushDoor`) and a
    plugin's performed step on `work/perform` opens the pull request. A landing names no plugin and its press and delivery tell none
    (`Landing` has no plugin; `TreeDeliveries` holds no dispatch). There is no `work/land` point, no hand-off verb, route or page, and no
    automatic hand-on of a held-back push: a pull request is a place's workflow steps and nothing else pushes for the person.
    A delivery or note recorded before that is read and said, never sent again (G8-CONVERT1 deletes the reading). (from D87, D100, D241)
31. A plugin process holds no git grant and runs no git: only the push act writes a grant, and `DAORIS_GIT_GRANT` is dropped
    from every start (`GitWiring.Hand`). A plugin's `pull-request` step opens the request through its service's REST API. (from D241)
32. That step's process is handed the API token of the identity serving the place's origin as `DAORIS_HOST_TOKEN`, one process per
    place (`PlaceHostTokens`, `PluginHostToken`, `HookSet`); one the desktop inherited never reaches it. No plugin is started where
    origin is served by `own`, no identity serves it, Write is denied or the identity is read only, or no API token is set. (from D241, D242)
33. A pull request's state is asked only of the plugin that opened it, at occasions that may remove, never on a timer. A
    `completed` answer clears a branch only where git holds its merge commit on the line. (from D148)

**The line**

34. Daoris's own fetch of a line is `fetch origin <line>` through the doors. It is refused on an old git, held on
    `own-input` or a standing hold, and counted for the pull request minute. It moves only origin's refs. (from D109, D198, D148)
35. Bring up to date is listed, then pressed: fast-forward only, replays in Daoris's own trees, local deletes of what
    merged. Never a merge commit, a force or a push. (from D109, D112)

**The person's acts**

36. The person's acts are a fetch of origin's branches, a push of one branch or lightweight tag to origin, a branch or lightweight tag
    made, renamed or deleted, and pick, revert or rebase onto a branch no tree holds. The push is one `push` step,
    `<commit>:refs/heads/<name>` or `refs/tags/<name>`, no `+`, no `-u`, no followed tags, no submodules, no signing, its hooks run, only
    where origin names one push address and no setting runs another program. The step writes a grant bound to origin's push address, the
    ref, the commit origin had it at as last fetched (the zero id where none, and always for a tag) and the commit it moves to, hands it to
    that git alone and drops it when git ends. It is refused before it starts where Write denies, the identity is read only, origin is
    held, the ref moved since shown, or the branch is `daoris/*`; a tag's Dangerous value is the door's to judge. It has no Undo; an end
    with no exit is unknown and never retried. A workflow's `push` step (G8) is the same act. (from D204, D241)
37. Behind Setup's *Daoris may change this checkout's files* (`checkoutWrites`, off by default): stage by file, hunk or
    line, commit, amend, resolve, switch and stash save, apply or drop (a drop of Daoris's own saves alone). (from D204)
38. Every act takes one checkout lock, keyed by the common directory and held alone, and runs through the act journal; an act
    reached inside a flow that holds that lock alone already (a tidy, a removal) runs under it. A reconciler settles an open act;
    nothing is re-run. (from D204)
    Every branch delete is the journal's `ref.delete` act with its *Undo*: the person's `GIT_REF_DELETE`, and every door of Daoris's
    (`DISCARD_SESSION_BRANCH`, `WITHDRAW_LANDING_BRANCH`, `SWEEP`, the look's clean-up, `housekeeping_discard_branch`, a removal, a run's
    `tidy`) through `ContentHolds.DeleteJudgedAsync` and `JournaledDeletes`, pressed by `daoris`. The Branches tab lists them from the
    journal alone (`GIT_DELETES`), each with its Undo. Each door keeps its judgement; the recovery ref its rule keeps is made in the
    delete's own transaction, so a refused delete makes none, and a late checkout's put-back takes it back with the branch; a door that keeps
    without deleting (a settled member, a tidied part, a detached tree, a missing landing) makes it in one `ref.keep` act. The record names it
    and the retention dates it from that act. Only an opening undoing the branch it just made deletes one itself. Where git ended the transaction refusing, a ref
    its list leaves out is gone only on `show-ref --exists`'s word (a broken one is unknown, never gone), and a branch standing at neither
    the judged commit nor gone is said as the door says it: moved since it was judged, or unread. A `tidy` (`TidyDoor`) judges nothing itself: the
    `monitor` before it read the chain's pull request merged, and each part's delete rests on the recovery ref it kept the part's commits
    on. (from D204, D228, D245)
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

## Git's starts, counted

Every git Daoris's desktop starts runs in a place of `GitScheduler`'s: `GitRuns`' three runners, an act's held step and a pool
claim's git (`Held`), an identity's `ls-remote` (a person's, waiting outside Daoris), and every question of a git's file through
`ChildProcess` (the Tools page's version and system configuration, `daoris tool use git`, the version probe, a plugin's declared git
and its checks); the start gate and `HeldChild` refuse a git's image holding no place at every purpose, said as `git.start.outside`.
Four run at once; the background and every git waiting outside Daoris (a remote act, an act that may run a hook) hold three together,
so a person's read always finds the fourth; outside-waiting gits hold at most two and count toward no repository's two; one
repository runs at most two of the background's reads. Asks queue per repository by common directory, a person's oldest that may go
first, else the oldest that may go; a write passes nothing queued ahead of it and nothing passes it. An ask shares an identical start
(file, folder, arguments, variables, runner, bound) only while that start is queued with no write of its repository queued after it,
never one running. A read `GitEndable` calls
lock-free (a `status` with `--no-optional-locks`, a working-tree diff also told `-c diff.autoRefreshIndex=false`) is dropped while
queued and ended while running once its askers leave, the last answered only once its runner has returned: with its own
cancellation, or as `ProcessUnendedException` where the git outlived its kill; every other git, placed, runs untokened to its end or
its bound. A
`worktree remove` is admitted once the tree's lock-free reads are dropped or ended and its other gits have ended, the `worktree add`
making it among them, and holds what is asked in the tree or to make it after it, which its own git passes. An ask beneath any placed git takes a place that git lends, above the four, one a parent, two deep,
deeper refused as `git.start.depth`: a pipe request's caller is told beneath one by its process ancestry (`PipeGitCaller`,
`GitCallers`, read only while a git holds a place), so a push's hook and a diff's textconv or filter helper that ask the window
never wait behind the place their own git holds. Lazy fetch is off for every read in a repository with a promisor
(`GitLazyFetch`): the git in use (`GitInUse`) is proved once to honour `GIT_NO_LAZY_FETCH`, by two starts in that repository with
every transport refused and git's trace on, the plain run fetching and the told one not, since git's sources read the variable in
2.39.4, 2.44.1 and 2.45.1 but not 2.44.0 or 2.45.0; a git not so proved is never told it, and its reads there wait outside Daoris
from their start and are never ended. Every object Daoris reads, a blob's bytes or text, an object's type or its presence, is one
`cat-file --batch` start asked one question and read by one parser (`GitObjectReads`, `GitObjectAnswer`), its size and type the
header's, a text decoded as git's text output is (UTF-8, a leading byte-order mark taken as one); a question is one line, so a path
holding a line break is asked by the id `rev-parse --verify` resolves for it (`GitObjectName`), and `GitObjectFormsCensusTests` is red
on any other form (`cat-file blob`, `-e`, `-t`, `show <rev>:<path>`, `ls-tree -l`) or a second parser of the answer, the lazy-fetch
proof's `-t` alone listed. A blob that answers missing in a repository with a promisor is asked once more as a start allowed to fetch,
a type or a presence never, and the landed preview says `PREVIEW_UNFETCHED` where that gives none. A commit's fields are never read
from its stored bytes: its parents are git's history's (a history edit lists them with `rev-list --no-walk --parents`, so a shallow
boundary is a root and a graft's parents are the graft's), the rest the format reads. `GitStamp` is the one stamp of what a git
answer rests on, for the folder that asks: the git in use, every configuration file git would read there, present or absent (the
`ProgramData` file on Windows, the system file, `GIT_CONFIG_SYSTEM`'s or the one the git in use names to `git var GIT_CONFIG_SYSTEM`,
asked once a git in use; Daoris's global file, the person's and the XDG file; the repository's `config` and the tree's
`config.worktree`; each file an `include.path` or `includeIf.*.path` names, followed to git's depth, conditional or not), the asking
folder where a `gitdir:` include stands and the tree's `HEAD` where an `onbranch:` one does, the replacements (loose, and the
`packed-refs` lines under `refs/replace/`), `info/grafts`, `shallow`, `reftable/tables.list` and the variables that change an answer;
a digest of their contents, each file a `FileStamp` (its length and write time before its bytes and after, the bytes hashed again
where the stat moved, is a link, or was not two seconds old on the volume's clock when hashed: this machine's on a local fixed volume,
the write time of the home's `run/clock.mark` on the home's, none elsewhere). `GitStampTests` holds it to git's own
`config --list --show-origin --show-scope --includes` on the runner's git and the pinned MinGit. `GitReadCache` keeps a graph page, a
commit's fields and a compare's commits under the asker's stamp, admitted only where the stamp's fence (each input's stat, an absent
one's nearest folder) read again after git answered is unchanged and settled; a commit's changes, a compare's, a file's history and
a blame are asked at every ask, in the asking tree under its attributes. `GitReplacements.CanBeInForce` (a loose or packed
replacement, `GIT_REPLACE_REF_BASE`, or reftable refs) is the library's admission's one judgement of a replacement, reading the
`packed-refs` parse the stamp reads (`GitPackedRefs`, made once a content). The declarations' store, `RepositoryDeclarations`
(`Workplace/Declarations/`), answers a declaration present, absent or unread: a checkout's or a home's file by its `FileStamp`, its
bytes by their `ContentKey`; a file at a commit id from the commit's listing, one `cat-file --batch` start asked the commit, its tree
and each folder on the way, kept by id only where each object's bytes hash to it (`GitObjectHash`), and its blob kept under its
`BlobId` only where its bytes hash to it; a blob asked by id answered only by bytes that hash to it, else *unread: replaced*; a
commit's tracked list kept by commit under the replacement gate's `StatFence`. While `CanBeInForce` holds it asks git by path and
admits nothing. A `DeclarationVersion` is compared by `Same` alone, an *unread* the same as none and without text; a kind's parse
(`IDeclarationKind`, the rule file's `RuleFileKind`) and a view (`IDeclarationView`) are kept by the content keys they are made from, a
parse naming a problem made again at each ask, and contents, tracked lists and indexes are each bounded alone (`DeclarationBound`:
16, 24 and 4 MB). `TipOf` reads a full ref from its loose file or the `packed-refs` parse, a symbolic ref followed to five, a
reftable's from git; `GitTreeReads` is the door for path-bearing reads, its search git's with the declarations excluded and their
lines from the store's bytes; `DeclarationScope` says whether the store or the door asked a start. Every declaration's name is a
read-only field of `DeclarationNames`, never a constant. The flow memos share an answer between two folders of one
repository only where the stamp's context is one (`GitStamp.ContextOf`: the common directory, the asking tree's `config.worktree` by
its bytes, and the folder itself where a `gitdir:` include stands), so `GitFacts.SameRepository`, `RefSnapshot.TreeHeld`,
`ListedFor` and `GitReadOnce`'s listings never answer a tree with replacements off from one with them on. A git whose kill it outlived leaves the four for a count apart
(`git.place.apart`, `git.place.exited`), and while its repository holds one the background's asks of it are answered `stalled`. An
ask waiting for its place is said as the queue of the look's stage whose work asked it (`start.queue=`), as the start gate's wait
is; work begun beside a stage says it to none. A bridge route's gits are a person's (`DriverModule`); the flow memos ask under the flow's token (`GitMemoAsks`). Four censuses
hold the classes, each run over the driver with each listed member left out in turn and with one planted omission, and red on each
(`SourceCensus`): every field holding a git answer, with its stamp or its flow (`GitKeepersCensusTests`); every field of the trees'
and the platform's code keeping a process, a library repository or a place, with what ends it (`GitLifetimesTests`); every file there
that ends a process early, with what it may end (`GitEndingsCensusTests`); and every file of the driver that starts a process
(`GitStartsCensusTests` over `GitLaunchTests`' list). `GitSchedulerProcessTests` holds the scheduler on real git: 240 gits over six
repositories at most four places at once; a person's read placed at once while a push running its hook, a fetch waiting on its
grant and a background diff whose textconv waits hold the rest; two commits' hooks in two partial clones reading a filtered blob
back through the service, and four diffs' textconv helpers asking back in, each lent a place and ending 0; a removal while a status
that locks is queued in its tree; a write's order against reads either side of it. Each git is counted
for a flow through `GitLaunch` (`GitLaunch.Count`,
`WorkingTree.Started`); a test counts a pass's starts at the process gate with the folder each ran in and an object read's question,
its input (`GitStartTally`, `GitStartInputs`). Measured on
2026-11-06 on the count classes' fixtures, and held there as budgets. *Again* is the same command in the same folder asked again in the
pass with no write between; *object* is what one held `git cat-file --batch` of the repository could answer instead (a name or an id
read as its object), *by id* those naming an id alone.

| View or landing | Starts | Again | Object (by id) | Held by |
|---|---|---|---|---|
| Branches (`GIT_BRANCHES`, one repository) | 2 | 0 | 1 (0) | `ViewGitCountTests` |
| The checkout's branch (`CHECKOUT_BRANCH`) | 1 | 0 | 0 | `ViewGitCountTests` |
| Changes (`GIT_CHANGES`), a file's hunks (`GIT_HUNKS`) | 5 each | 0 | 1 (0) each | `ViewGitCountTests` |
| Graph, a file's history, blame (first open; kept by id after) | 1 each | 0 | 0 | `ViewGitCountTests` |
| A commit, a compare (first open) | 2 each | 0 | 0 | `ViewGitCountTests` |
| The commit, switch and stash plans | 8, 5, 7 | 0, 0, 0 | 1 (0), 1 (0), 1 (0) | `ViewGitCountTests` |
| The deletes (`GIT_DELETES`, the journal holding none) | 0 | 0 | 0 | `ViewGitCountTests` |
| Review, a tree ahead of its line, its second open (one untracked file, or ten) | 10 | 0 | 0 | `ReviewGitCountTests` |
| Review, a tree the line holds by content | 11 | 0 | 1 (0) | `ReviewGitCountTests` |
| A landed session's review | 3 | 0 | 2 (1) | `ViewGitCountTests` |
| The Accept offer of a tree of work | 3 | 0 | 0 | `OfferGitCountTests` |
| The look's tidy, six trees (two) | 5 (5) | 1 (`worktree list`, the judgement's own) | 1 (0) | `TidyGitCountTests` |
| A session's start, to its spawn (the files stage's `core.ignorecase` among them; the landing plan's layout one start) | 20 | 0 | 3 (0) | `StartGitCountTests` |
| A squash press's landing | 19 | 0 | 4 (1) | `LandGitCountTests` |
| A merge press's landing | 32 | 3 (the line's commit) | 9 (0) | `LandGitCountTests` |

The rows above are as the cuts of PERF1 left them (measured again on the same fixtures); the refs are listed once for a repository in the
offer and Review, and a landing lists them twice (its judgement's, and its press's after the write), each listing the library's where
admitted. Measured on 2026-11-09 on the count classes' fixtures before the library took the listing and after: Review 17 to 16 (by
content 18 to 17), a landed review 6 to 5, the offer 6 to 5, the tidy's six trees 18 to 17 (two 10 to 9), a session's start 21 to 20,
a squash landing 24 to 22 and a merge landing 38 to 36; Branches stays 2, its listing git's. With the merge-bases, the counts and two
commits' names the library's as well, measured the same day on the same fixtures: Review 16 to 10 (by content 17 to 11), a landed
review 5 to 3, the offer 5 to 3, the tidy's six trees 17 to 5 (two 9 to 5), a squash landing 22 to 19 and a merge landing 36 to 32;
a session's start stays 20 and the Git page's views as they were. The branches' date listing a flow keeps from one open to the next
(`BranchesByDate`) is one start more where another flow let it go between them. A test class's own git, each class alone with git's trace per process (`GIT_TRACE2_EVENT`) and the fixture's count
(`DAORIS_TEST_GIT_SPAWNS`): `OfferGitCountTests` 43 starts (the fixture's 11), 3.7 s in git; `StartGitCountTests` 23, 1.8 s;
`ViewGitCountTests` 125 (28), 7.1 s; `LandGitCountTests` 127 (7), 8.4 s; `TidyGitCountTests` 148 (19), 10.4 s; `ReviewGitCountTests` 180
(15), 17.3 s. The other test classes that start git are not counted yet. Before and after the scheduler, measured on 2026-11-09
with the commit before it (`5cf077b03d`) in a scratch worktree, each class alone on this machine under its agents' load with
`GIT_TRACE2_EVENT` set to a folder (`dotnet test src/Daoris.Desktop/Daoris.Desktop.Driver.Tests --no-build --filter
FullyQualifiedName~ReviewGitCountTests`, then `~TidyGitCountTests`) and each top-level git's start and exit summed and swept:
Review's class 203 gits either way, 26.0 s in git and at most 3 at once before, 25.9 s and 2 after; the look's tidy 113 gits, 18.9 s
and 1 before, 21.2 s and 1 after. Their gits are asked one after another, so the bound holds none back and the scheduler costs them
nothing measurable; `GitSchedulerProcessTests`' fan-out ran 240 gits in 69 s, never more than four placed. Every object read one
`--batch` question (GITLIB1c), measured the same way the same day under a heavier load (each class's tests took over a minute each):
Review's class 203 gits, 58.8 s in git and at most 2 at once, its views 17, 17 and 18 starts; the tidy's 113 gits, 48.4 s and 1,
its six trees 18 starts and two 10. The gits are the same as before it, an object read one start where one was (a union join's file
one where it was two), so the times are the machine's load and not the change. `createdMs` is the gate's
own figure, read from the install's `review.read`, which git's trace cannot give.

A held reader per repository does not earn its place on these figures. A view asks at most three object questions a pass, one of them
by id, and the Git page's reads ask none once an id's answer is kept. A landing and the tidy asked more, and the starts are cut without
a process kept (PERF1): each branch's tree is the refs' one listing's (`%(tree)`, `RefSnapshot.TreeHeld`, a commit's tree being the
same in every folder of the repository); a read-only flow lists a repository's refs once whichever of its folders asks
(`GitReadOnce`, `RefFiles.CommonDirOf` telling two folders of one repository by their `.git` files); the offer reads a tree's paths,
head and branch in one `status --porcelain=v2 --branch` (`TreeStatus`); Review asks no count of origin's copy of the line where the
repository's files hold none (`RefFiles.Absent`: a loose or packed ref, or a reftable repository, is never called absent); a landed
review asks the checkout's top once. The switch and stash plans (and the press that judges them) read head, branch, changed tracked paths and files in
conflict in one `status --porcelain=v2 --branch -z` (`PlanStatus`), the stash plan hands the tip it read to the list instead of asking
`refs/stash` again, and the commit plan reads the head's tree, parents and message in one `log` (`ActCommits.FactsAsync`). Review reads
a session tree's untracked files' patches in two starts whatever their count (`UntrackedPatches`, nodes A and U after S): the files are
added as intents (`add --intent-to-add`, the paths NUL-ended on git's input and literal) to a private index beside the tree's own
(`GIT_INDEX_FILE`), and one `diff` of it against the tree is split by its file headers in status's order, each patch the bytes a file's
own `diff --no-index` gave; the tree's index is never written, and the private one is removed with its lock after. Left asked
again: a landing's line commit (3), which the promotion reads before the gates (they can run long), under the checkout's lock before
the move, and after it as the proof the move held, and its head, because another process can move either and a landing reads afresh
before each act; the plans' top, the stash tip and the layout (each a read their press compares by); the tree's files and the status
Review reads for its own diff. A held reader would still cost its lifetime, the repack hazard and the version
floor (the git-app design §4.3), for at most 9 starts of a merge press's 39, and Review asks it nothing.

The refs' one listing (`RefSnapshot.ReadAsync`: every branch and copy of origin's, its stored id, its object's type, its upstream in
full and short, a commit's tree) is read through LibGit2Sharp where the question is admitted, and starts nothing. So are a plain
read's merge-bases, counts and two commits' changed names, whoever asks them: `GitRuns` asks `GitLibraryReads` beneath its flow memos
before any git, which tells the read by its arguments exactly (git's own options `--no-optional-locks` and `-c core.quotePath=false`
alone) and answers it in git's words. `GitLibraryWalks` answers `merge-base` of two commits, `--is-ancestor` and `--independent`,
`rev-list --count` over tips, `^` and `--not` exclusions and `A..B`, and `--left-right --count A...B`; a merge-base with more than one
best base (a criss-cross) is git's, whose choice among them is its own, found where a commit both tips reach is not reachable from the
base libgit2 gave. `GitLibraryNames` answers two commits' `diff --name-only --no-renames`, sorted as git's tree order sorts them, a
rename as its two paths; a gitlink that changed without `--ignore-submodules=none`, a path git would quote without `-z`, rename
detection, a filter and a path after `--` are git's. A revision is read as git's rules read it (`GitLibraryRevisions`: a full id,
`HEAD`, a full ref, or a short name by git's rules before a short id), and one holding an operator, one a file in the git directory
could answer, or one whose object is missing is git's. `GitLibrary` opens
a `Repository` for one call on the thread pool and disposes it before it answers, eight calls at most at once (`GitLibrary.Open` is
zero between calls, `GitLibrary.AtOnce`), libgit2's
global, XDG, system and program-data search paths pointed at a folder holding no file. `GitLibraryRefs` prints `for-each-ref`'s
lines: sorted as bytes, a broken symbolic ref left out, the upstream from the common directory's `branch.` and `remote.` keys through
the remote's first matching fetch refspec, shortened by git's strict rule. `GitLibraryAdmission` refuses, from the repository's own
files at each question and by its class (`GitLibraryQuestion`), a folder with no git directory, a format the library cannot read and any
replacement (loose, packed or `GIT_REPLACE_REF_BASE`) for every question, a branch or remote section in a global file git is handed for the refs, and a
`diff.orderFile` or `diff.relative` in the repository's, the tree's or that global file for the names; libgit2 refuses an
extension it does not know and another owner's folder at its open; an object a call needs that is missing refuses the call. Grafts,
a shallow boundary (before and after a deepen), a split and a sparse index the library reads as git does. A refused question, and
every one under a test's stand-in runner, is git's. The formats come from `GitLibraryFormats`, the `git.read.formats` probe: a repository's
`extensions.objectformat` and `extensions.refstorage` (and a `reftable` folder) read from its own configuration with no git started, kept
for that file's length and write time, so a converted repository is read again at its next question and an unchanged one never; only
SHA-1 objects in files refs are the library's, and each other format is refused by its own word (`sha256`, `reftable`, `object-format`,
`ref-format`) before the library is opened. `GitLibraryJournal` keeps, per repository in `run/git/library.json`, each reason a question
went to git (every admission refusal, an unreadable repository, a failed call, a call the library declined) with its count, and the
answers the library gave; a reason new to a repository is written at once, counts at most every five seconds.
libgit2 runs in the desktop's process, so a fault there ends the window and .NET cannot catch it: every call appends its open before the library
is entered and its close after it returns (`GitLibraryLeases`, a file for each process in `run/git/library-leases/`, named by its id and start time,
unbuffered and written through, so its death keeps each line), and the first question of a start in a home reads the files of processes
that are gone (`GitLibraryFaults`). A repository such a process ended inside, its last open with no close, is journalled in `library.json`
as faulted under the library package's version and refused for every question while the package is that one: its reason is `native-fault`,
which the Git page's listing says, and each refused question is git's. A new package version lifts the refusal; the first fault itself is
accepted, and a home with none named runs no guard. `GitLibraryFaultProcessTests` kills the owner stand-in's `lease` mode inside a lease
and proves the next start refusing that repository alone, and the refusal lapsing when the package version moves. Each library answer is also a
read in the flow's `GitLaunch.Count` (`GitCount.Reads`, beside `Starts` and never one), which `review.read` says as `reads` and `readMs`
and the bench's times list apart from the starts. Settings → Git lists the journal as *Git reads* (`GitReads.tsx`, `TOOLS_LIST`'s
`identities.reads`): each repository by its folder's name with the library's answers, git's, and each reason in words. The worktree list, every other ref read and the Git page's branch listing (its
dates, subjects and distances are the commits') stay git's. This second way has two ends, each a failing test in
`GitLibraryEndsTests`: a case for each refusal and the questions it refuses fails, naming the refusal, the day the pinned library
reads it as git does (GITLIB1g deletes that refusal and its path to git), and a case fails the day the git in use, or a git the
built-in list pins (3.0 or past), makes reftable or SHA-256 repositories by default (GITLIB1g retires the library and its admission
for the scheduler alone). `GitLibraryWalksTests`, `GitLibraryNamesTests` and `GitLibraryRefsTests` hold each answer equal to git's,
value for value, and each read the library leaves to git answered through one start. These classes, `GitLibraryFormatsTests`,
`GitLibraryCountedTests` and `GitLibraryEndsTests` run twice: on the runner's git in `driver-process-git`, and on the MinGit the built-in
list pins in `driver-process-git-pinned` (`git.pinned.process.runsettings`, which sets `DAORIS_PINNED_GIT_LEG` for the test host). The host
fetches the pin into the repository's own `_fixtures/` (`PinnedMinGit`) and puts it first on `PATH`, so the product's starts and the
fixtures' are both that git (`GitLibraryEndsTests` fails a leg that ran another). Where the pin cannot be had (no pin for the platform, a
fetch that fails) every `[GitLegFact]` of the leg is skipped with that reason and never run on another git. `GitLibraryHoldTests` holds the call's hold on
Windows: `repack -ad`, `gc --prune=now`, `commit-graph write` and `multi-pack-index write` under library reads every ten milliseconds
exit 0, leave no old pack and never ask the `GIT_ASK_YESNO` stand-in, which is first seen asked where a pack is held open. The
`win-x64` publish lays the native `git2-*.dll` beside the driver, and the deployment rehearsal asks for it (`driverProblems`).

A start's cost, and LibGit2Sharp beside it, measured on 2026-11-09 by `tools/git-library-bench`, which no product project references:
`dotnet build tools/git-library-bench -c Release`, then from the root `tools/git-library-bench/bin/Release/net10.0/git-library-bench.exe`
with `starts`, `setup --name <n> --source <repo>`, `reads --name <n>`, `threads --name <n>`, `parity` or `opens`, its fixtures under
`_fixtures/gitlib`. On this machine under its agents' load a `rev-parse HEAD` start costs 0.2 to 0.6 s at the median one at a time
(two runs), up to 1.7 s with every core busy and 6.6 to 12.9 s when twenty start at once under that load, most of it in `Process.Start`,
while the command's own work is 2 to 8 ms. Review of a session tree on a clone of this repository, ten untracked files beside its changes, is
17 starts and 4.2 s at the median through git, 0.65 s in process with the repository held; the untracked cut, measured back to back by the
bench's `reads` (five passes each), took it from 25 starts and 10.5 s, its untracked tail from 4.3 s over ten starts to 0.35 s over two.
The look over six trees is 23 starts and 9.7 s against 0.55 s. On vscode (6,358 refs, 170,823 commits) the library
loses: Review 25.1 s against 19.6 s, its walk of the commits no branch holds 20.4 s against 1.1 s and twenty commits' patches 3.7 s
against 1.4 s, while a ref, a merge-base, the counts or two trees' file names stay under 55 ms against a second a start. Its answers
equal git's for refs, the worktree list, history, two commits' diffs, eol and ident attributes, ignore case, and a detached or linked
tree's status and untracked files; they differ in a rename's similarity score, wherever a filter driver runs, under sparse checkout, and
on a path past 260 characters in a deep tree, where its status fails. It refuses reftable and SHA-256, and a split or sparse index's
status. The library's further reads are proposed in `docs/2026-11-09-git-library-design.md`.

## Where a change goes

- **How an act is carried or judged:** the doors (`GitDoors`, `GitRouting`, `GitWiring`), compiled into
  `Daoris.Desktop.GitDoor`. Never a `pre-push` hook, a credential helper or the harness's rules alone.
- **Who an act is:** `GitIdentities` and `GitIdentityUse`; the secret-holding acts in `GitIdentityActs`, `GitIdentityHttps` or
  `GitIdentityTokens`, a secret's folder in `GitTokens` or `GitKeys`; a page change in `settings/git/`.
- **A new failure or hold:** `GitFailures`, `GitHolds` or `GitOwnInput`, with its row words where `git-held` rows are made.
- **What an act may do:** `GitAccessKeys` and `GitAccessRules`; what a session's harness is told in `GitAccessPolicy`.
- **A new person's act:** a step kind on the act engine, a `Git*Command.cs` twin, a `GitRoutes` route, and its rows in
  `PipeTargets`' marks and `PipeCensus`.
- **A platform's push or pull request:** the plugin, in the plugins repository. The driver changes only the wire
  (`LandingPlugins`, the hook points).
- **A record that syncs:** `IRemote` plus a `*Sync` in `Service.Core/Remote/`, and the host's routes. Machine wiring goes
  through `RemoteOperations`; `RemoteTarget` and `RemoteConfig` are twins whose test tables must match.
- **Quest conflicts:** `QuestLog` and `QuestExchange` (service), `Planner` (driver), the quest page.

**Tests that pin the area:** `GitDoorsTests`, `GitDoorStartTests`, `GitDoorsProcessTests`, `GitPromptsProcessTests`,
`GitSshProcessTests` (also two grants at once on one address each pushing only their own ref, one grant taken by one of two pushes, a ref moved after the grant refused and never pushed over), `GitSshHoldsProcessTests`, `GitTlsProcessTests`, `GitPushUpdatesTests`, `GitPushActProcessTests` (the press's push
under its grant on the SSH host, the same push refused without one, a tag refused by `deny` and passed by `allow`), `GitPushCommandTests`,
`JournaledDeletesTests` (a judged and a forced delete journaled with their Undo, the one-way census), `PullRequestApiHostTests`, `GitHttpsTests`, `GitHttpsProcessTests` (an HTTPS identity's token on the TLS host with nothing beside git, the credential door's refusals), `GitIdentitiesTests`, `GitIdentityCommandTests`,
`GitSessionAccessTests`, `GitSessionHoldsTests`, `DriverModuleSessionGitAccessTests`, `GitMoveTests`, `GitHoldsTests`, `GitOwnInputScopedGitTests`, `GitAccessTests`, `GitPushStepTests`, `GitRemotesTests` (the address reader's
census), `ProcessStartsCensusTests` (every start through `Tools.Hand`), `ViewGitCountTests` (each page read's git starts held to those measured), `GitRefActTests`, `GitHistoryEditTests`,
`GitFetchTests`, `CheckoutWritersCensusTests`, `PullRequestStateTests`, `PullRequestProofTests`,
`WorkflowSquashChainProcessTests` (land, push, pull-request, monitor and tidy against the stand-ins, a squash
merge ending a two-part chain once with an Undo), `RemoteSyncTests`, `RemoteTargetTests`, `QuestSyncTests`, `QuestReopenSyncTests`,
`SessionSyncTests`, `BroughtForwardLookTests`, `RemotesModuleTests`, `DriverModuleGitAccessTests`; on the page
`Git.test.tsx`, `GitRemotes.test.tsx`, `GitAccess.test.tsx`, `GitAccessFact.test.tsx`, `GitAsk.test.tsx`, `ProjectsView.git.test.tsx`, `PushRef.test.tsx`.

**The remotes they reach** are stand-ins in the test process on free loopback ports, one per test class, with nothing installed
(D241 point 8): `GitSshHost` (an SSH host from Microsoft's dev-tunnels library serving `git-upload-pack` and `git-receive-pack`,
verifying RSA and ECDSA keys, never ed25519; its `InterruptReceive` has a receive-pack list its refs, take and apply the pack, then drop the connection with no report), `GitTlsHost` (`GitHttpBackendHost` behind TLS, its certificate trusted per command, asking Basic where a test sets `Basic`)
and `PullRequestApiHost` (GitHub's and Azure DevOps' pull-request calls, admitting a test's token). Each counts what it was asked.

**Gates that bite:** `driver`, `driver-process-git` (real git), `driver-process` (the doors' processes), `censuses`,
`modules`, `service`, `web-unit`, `web-types`, and `deployment` (the rehearsal publishes the install with all three doors). Visible words need both
catalogues (`i18n:check`).

## Contradictions

- D47's one home per quest, written through, vs D68 and D69's local commits and fetch-rebase-push: D68 reads right.
- D109 §1 and D147 point 3 (the line fetch runs with the person's credentials and helper) vs D198 and `TreeRefs`' fetch
  through `GitRuns.GitStart`: D198 and the code read right. `WorkingTree` is the door to git's reads and runs; the diff reads are
  `TreeDiffs` and the process starts, with their test seams, are `GitRuns`. A set-aside folder's marks, reads and keeping are
  `SetAsideMarks`, `SetAsideReads` and `SetAsideKeeps`, with `TreeSetAside` the front.
- D147 point 1 (Git a place on the activity bar) vs D150 and `ProjectsView.tsx`' `RepositoryGitView` (Git inside Repositories): D150 reads right.
- D204's head ("proposed, not yet read; nothing built") vs its 2026-10-31 acceptance note and the built acts
  (`ActRuns`): the note and the code read right.
- D87 and D100's plugin push "with whatever that platform's tools are signed in as" vs D241 point 4 and `LandingPlugins`' remarks
  (Daoris pushes through its own push act under origin's identity and a grant; the plugin opens the pull request): D241 reads right.

## Open

- **GITDESIGN1:** git's place (D241, `docs/2026-11-05-git-design.md`): every remote act through the doors, one push, a pull
  request as a place's workflow steps, the path proven in CI against stand-in remotes. G1 to G9 are built; G8-CONVERT1 and G10 remain
  (`TASKS.md`). WORKSETUP1 follows it.
- **GITAUTH1b:** built. The doors' classes (`GitDoorsProcessTests`, `GitSshProcessTests`, `GitHttpsProcessTests`) run twice, with the
  runner's git and with the MinGit the built-in list pins (`PinnedMinGit`, fetched by the toolchain's own download into `_fixtures/`
  and held to the list's size and SHA-256; the `GitHttpsPinnedProcessTests` subclass). MinGit 2.56 carries both a Schannel and an
  OpenSSL libcurl and its system file names none; the test certificate is trusted through OpenSSL (`http.sslBackend=openssl` with
  `http.sslCAInfo`, `GitTlsHost.TrustedBy`), measured with all seven HTTPS facts passing. MinGit ships no `git-http-backend`, so the
  HTTP host behind the TLS front serves with the runner's git.
- **GITAUTH1c:** a key added on a real host and a real fetch (the steward's); the git setting's doors wait on Settings → Tools.
- **GITAUTH1e:** built: Ask Daoris's sync card, for each host a fetch over SSH or HTTPS did not reach, offers *Add a git identity for
  host…*, which opens Settings → Git on its Add form with the host filled (`HelpSyncUnfetched.Host`, `StarterDoor.gitHost`). The
  browser sign-in, its `PhaseList` and its Cancel left with Git Credential Manager (D241). The card's and the screen's advice on
  reaching an origin (`settings.sync.notFetched.ssh` and `.https`) names a git identity for the host in Settings → Git, with an SSH
  key or a token.
- **GITAUTH1h:** the install, seen (the owner).
- **GITAPP2:** Stash Drop is built (`StashDrops.cs`, `StashDropSteps.cs`, `GIT_STASH_DROP`): it is offered only for a stash a done Save of
  Daoris's own made, read from the Save step's journaled words (the commit saved; a message proves no ownership), by commit id, never
  its place; the step finds the place as it runs and, where git names another commit it dropped, stores that one again and reports
  not done (proved with a Save slipped in between the step's read and its git). It has no Undo: its answer and its record keep the commit
  and `git stash store -m <message> <id>`, good until git clears unused objects. Its terminal twin is `daoris git stash drop <repository>
  <commit> [--plan|--yes]` (`GitStashDropCommand`; `GIT_STASH_DROP` is a Person press with `said`, `person.route.GIT_STASH_DROP` its card's
  words): the plan names the store-back command, `--yes` asks the window or runs the act itself, a session's terminal is refused. The push's interruption (origin took the pack and the connection dropped before it said so:
  unknown, never retried) and the person's `pre-push` hook refusing (not done, nothing sent) are proved against the stand-in SSH
  host (`GitPushActProcessTests`).
- **ONEWAY1r:** a delete's record, its Undo and the page's list (`DeletedBranches`, `GIT_DELETES`) are the journal's alone, and every door's
  recovery ref is kept through the journal's act and dated from it (`RecoveryKeeps`). Left: `RecoveryRetention`'s dating from a ref's reflog
  and `RecoveryDropping`'s press stay for the refs under `refs/daoris/discarded/` no journal record names, which
  `daoris work recovery drop --unheld --plan` counts (`RecoveryCensus`) and the Branches tab says under the deletes; once it reads zero on the
  install, ONEWAY1af deletes both and the census (BL84). The plan says of each unheld ref its age, its commit count and what its work is to the
  repository's line (`RecoveryWorks`): every commit on the line by patch (`git cherry`), else every file it changed held on the line (the line
  carries the ref's content or has changed the file since their merge base), else the commits and files the line lacks. The line is the one
  set for the repository or its workspace, else `main`, else `master`, never a remote's `HEAD`; where none exists every ref is kept and the
  plan says so. `--unheld` drops only the refs whose work the line holds, each as before, and lists the rest as kept with why; a ref named by
  hand drops whatever the line holds of it.
- **G9:** the pull-request plugins open and read through their services' REST APIs with `DAORIS_HOST_TOKEN` and run no git; the loop
  starts each one per place with that place's origin identity's token (D242). `gh` and `az` are no declared tool (`Tools.Declared`,
  `resources.json`), and no web fixture, story, test or sample plugin-tool message names either as a sample tool.
- **D178 point 6:** *Hand mine to the winner*, no row until a way to publish a branch is asked for.
- **D47:** OIDC sign-in for people on a shared deployment, and the platform served from it; only machine keys are built.
- **FABRIC-PERFORM1l:** the pull request's fields kept on the wire evidence (`WorkEvidence` in `WorkWire.cs`).
- **OPTIMIZE1a:** counted per view, landing and count class (*Git's starts, counted*); the held reader is not proposed. Left: the
  cuts the figures name (PERF1's row), and the other test classes that start git counted beside a Process run.

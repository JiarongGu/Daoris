# Git and remotes — how it works now

## What it does

Daoris's git reaches code hosts as git identities of its own, and hands no credential of the person's unless the person chooses
it. Every process Daoris starts gets its git from `GitWiring`: nothing prompts, and every remote act passes Daoris's doors (the
ssh door for SSH; the remote helper, through a `daoris::` rewrite, for HTTP and HTTPS). At each act a door finds the repository,
applies the identity its remote points at, and asks one judge about the act's class. A remote that cannot authenticate is held,
and said once in *What needs you*, until something changes it. The person's own git acts, and every branch delete Daoris's doors
judge, run through one journal under one lock; Daoris's one push is its push act, and a landing plugin opens a pull request.
Every git Daoris starts is asked of one service and runs in a place of one scheduler. A workspace may also be wired to a shared deployment of the service:
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
| `Platform/GitService`, `Platform/GitScheduler`, `Workplace/Git/`, `GitReads*`, `GitLaunch`, `GitReadCache`, `GitLibrary*` | The scheduler, object reads, the stamp, the page's reads, the library |
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
   said back, only whether one is set. Key imports read unencrypted OpenSSH, PuTTY v2/v3 (Ed25519, RSA, NIST EC) and PEM
   RSA, EC or PKCS#8 through `GitKeyImport`, converting in memory before the private folder is made. PuTTY's MAC is checked
   before its private fields are used; `Daoris.Adapters.SshKeys` implements the `SshKeyConverter` port using Bouncy Castle's
   PEM and Argon2 routines with bounded parameters. Settings asks for
   the passphrase once and clears it after the attempt; `git identity add-ssh --import <file> --passphrase-stdin` reads it once
   locally, never through a pipe confirmation. Both doors say before writing that the kept OpenSSH copy is unencrypted and the
   original is unchanged; no passphrase is kept in the identity. (D198, D67, D241)
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
15. Every text from git, ssh, a door or a plugin passes `GitWords.Redact` before it is kept, said or answered; it cuts every token and key
    shape the sensitive scan's built-ins name (`SensitiveGate`), an address's user, a key block and an Authorization or Cookie header. (D198)

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
    person's own or `no-push` once on, is refused where it is proposed, naming the deny and where it is set (`DeadApprovals`). (D198, D277)
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
    standing hold, counted for the pull request minute, moving only origin's refs; a workflow `push` whose branch no longer stands
    makes it once, to read whether the line holds the work (`PushLineRead`). (D109, D198, D148)
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
    inside a flow already holding that lock runs under it; an act refused only because another holds the checkout answers `Busy`
    (`ActRun`), which a workflow's `push` waits out and tries again at a later pass. The integration's move of the line (the promotion's effect) also takes `local/merge-run.lock` of the checkout the line is moved in (the one that has it out, else the registered root), the merge tool's own file (verb `landing`, its pid and start), so the line never moves under a merge the tool has open: a live run holds the move, said, for the next look, and a lock a killed holder left is taken back by its process check, removed only under a reclaim guard beside it (`MergeRunLock`, `checkout-lock.ts`). A checkout with neither the folder nor the tool has no run to meet and takes none. A reconciler settles an open act; nothing is re-run. Every branch
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

A git's asker leaving ends it where it is a read (`GitScheduler`'s endable rule). A terminal's open read of the driver runs under a
token its connection's end cancels (`PipeReadStops`), so an abandoned `session list` stops walking trees and leaves no git queued
behind a client that has gone; a press, journaled, runs on to its answer.

A tree's content proof that found nothing holding its work is kept in the home (`memo/content-proofs.json`, `ContentProofMemo`, 400
entries and 14 days) by its branch tip, the line, the branches that may not hold it and the repository's counted refs read from
git's files, so a round or a restart over work nothing moved under asks git nothing; a hold, an unread proof and a lacking list are
asked again, and an act (a removal, a tidy) judges afresh. An owed opinion judged again reads its tree's work through that memo (the
review's `TreeWorkReads`), so a second judgment over an unchanged tip, line and refs starts no git, and a moved tip, line or ref asks
again (`OpinionClosesGitCountTests`).

A read that only object ids decide is kept for good, beneath every flow's memo, by `GitObjectMemo` (D357 point 6): `merge-base` (with
`--is-ancestor` or `--independent`), `rev-list` over ids, `cherry`, a two-commit `diff` of names with `--no-renames` and
`--ignore-submodules=none`, a `log` with `--no-show-signature` whose format names only ids, subject and side mark, a `cat-file` of one id,
and `AddressedReads`' commit listing (`ls-tree` of one id, `-z` with plain paths after `--` too; `GitLsTreeMemoTests`) (`GitObjectArguments` says which shapes; `grep` and `--numstat` follow `.gitattributes` and
configuration, so they are never kept). Every kept kind reads objects alone, so one key serves every tree of a repository: the common
folder, read from the folder's own `.git` (a subfolder keeps nothing), and the read's words, every commit named by its full id. A
`merge-base`, `rev-list`, `log`, `cherry` or `ls-tree` that names `HEAD` or a full ref name is asked by the ids git's files give them at the
ask (`GitRefIds`, from `GitRefReads`, which read the files sharing them with git's own writes; only where the read so written qualifies), so a ref that has not moved is a hit and a moved one is a
new key. A read naming the index or a working tree, or a name the files cannot say, never qualifies, and a repository where a replacement can stand or whose history is
shallow or grafted keeps nothing, as does a process with no home named. Only an answer git gave whole is kept (exit 0 with an empty
error stream, and exit 1 for `merge-base`). The answers have no age: a least-recently-used cap of 8 MB of estimate bounds them, in memory and
in the home (`memo/git-objects.json`, one row a line under a first line naming the format version; another version, a row that does
not read or a file that cannot be read is dropped at load). New answers are appended once 20 seconds after the first unwritten one, so
the disk takes their bytes and no more; the file is written whole only past 4 MB appended and when a start finds it that large. A
kept `--is-ancestor` can outlive a gc that pruned its commits, and the listing's upstream short name can depend on refs outside its stamp.
A hit starts no git, takes no place and opens no library; the look's stage line says hits and misses by caller and kind as
`gitmemo=caller:hits/misses[kind:hits/misses]` (`GitAskTally.RecordMemo`).

A ref's id is read from git's files, not git or the library: `rev-parse HEAD` and `rev-parse --verify --quiet NAME[^{commit}]` of `HEAD`
or a full ref name come from the loose file and `packed-refs`, a linked tree's own `HEAD` from its own directory (`GitRefReads`). A
name no rule has is absent. A directory where a ref is looked for, an empty or odd file, a dangling symbolic ref, an unborn head, a
reftable and a replacement that can stand are git's, and `NAME^{commit}` is answered from the files once a read of the library named the ref's own id as the commit (a tag, or a ref moved between the two reads, is learned nothing from). The refs' one listing is the library's, kept in memory while a stamp of every loose branch, `packed-refs` and the
configuration stands. `GitRefReadsParityTests` holds each against real git: loose, packed, a symbolic and a detached head, a linked
tree's own head, a ref deleted while packed and one moved, and a reftable.

The library answers the rest of `rev-parse` that no ref or working tree decides (`GitLibraryRevParseTests`, on real git, no start): `--abbrev-ref HEAD`
and `--path-format=absolute --git-common-dir` alone (`GitLibraryLayout`, with the combined read's refusals: a subfolder, a branch another ref
makes ambiguous, an included configuration), and `--verify --quiet` of an object by its full id (`GitLibraryIds`): the id, `ID^{commit}`,
`ID^{tree}` and `ID:path`, an existing object that has no such peel or a path the tree lacks being git's quiet exit 1. A missing object, a
path with `.`, `..` or an empty part and a gitlink are git's, as is a bare name (`main`), which an object's short id may also spell.

Every object Daoris reads is one `cat-file --batch` question (`GitObjectReads`), and a question whose every line is a blob's full id, or a full
id and a plain path, is answered from the object store by `GitLibraryObjects` through the git policy's `AnswerBytesInProcessAsync` (the adapter's
`ReadBytesAsync` asks it before any start): the stored raw bytes with git's header, cut at the asker's limit, `QUESTION missing` where the tree
lacks the path (`GitLibraryObjectsTests`). A tree, a commit, a tag, a missing object and a name that is no full id are one git start.

A tree's `status --porcelain` (v1, with or without `-z`; `--untracked-files` all, normal or no; `--no-renames`; `--ignore-submodules=none`)
is answered by `GitLibraryStatus` with no git started and never kept, since it reads the index and the working tree: the head against the
index read from its own file (`GitIndexFile`), the index against the tree as git reads it (a size moved is changed; on Windows a size,
write time and creation time, git's ctime there, matching and not racy are unchanged; any other entry hashed, every one off Windows,
where git also holds an inode), then the untracked paths, walked with the library's ignore rules. The personal ignore file decides only
a path the repository's rules leave, and where it alone ignores one a repository negation might re-include, git answers. Its settings are read from every configuration file git reads (`GitStamp.ConfigurationFilesOf`), and two files disagreeing on one
that changes the answer send it to git; so does a status asked before any stamp learned the system file's path, since it starts nothing to learn it. Git's: a rename git may pair (a staged addition beside a deletion, or a change where copies are
asked), a submodule, a sparse, intent-to-add, assume-unchanged or unmerged entry, a split index, a content holding a carriage return or
one an attribute file may filter, a link, `core.excludesFile`, `core.attributesFile`, `core.checkStat` or `core.trustCtime`, an index or
object store the process names (`GIT_INDEX_FILE`, `GIT_OBJECT_DIRECTORY`), `--ignored`, a path given, the `--porcelain=v2`
forms, and in the newline form a name git would quote (`GitLibraryStatusTests`, on real git).

Every git ask of the desktop goes through one service (`Platform/GitService`, D357): the scheduler's runs, the library's in-process
reads and nothing else reach `GitScheduler` or `GitLibraryReads` (`GitKeepersCensusTests` holds it, and holds that only `GitLibrary*`
files name the library's package). An ask has an id, its repository, its read kind (`GitReadKinds`: the git verb, `cherry` for a walk that
marks equivalent patches, `meta` for a start with no command), its asker, its class and its deadline, and is answered by the run or
failed as git's failure naming why. The class is the person's where the flow is (`AsPerson`, and a driven session's opening, `AsOpening`, told as caller `opening`), a step's for `run-step`, `opinion-gate` and
`check`, else the background's, and the scheduler places by it: a place that frees goes to the oldest ask that may go of the highest
class that has one, a shared read taking its highest asker's class, while the places' rules still read a step's as the background's. The deadline is the run's bound and an allowance
for a place: 5 minutes for a person's, 15 for a step's, 30 for the background's; a write or a git that waits outside Daoris (an act's
steps) has none, so an act is never dropped from its queue. The allowance is each asker's own and runs only while that asker's entry is
queued, decided against placement under the scheduler's lock, so an ask is run or failed, never both; a joined asker leaves a shared
read alone. A place not given in time is `deadline`. A library read not answered within its bound is handed to git; in-process reads
get no id and no place; a read the object-id memo or the ref files answered is counted as a memo hit. Asks and answers live in memory (`GitService.Open`), never entries of the bus's log. A shared
read's person joined to a long read is never failed once the read is placed (`GitServiceTests`).

*Parts.* A log walk (`log`, `cherry`, `rev-list`) or a tree's listing (`ls-tree`) may be asked in parts (`GitServiceParts`, over
`GitService.PartsAsync`): its answer comes as parts numbered from 1 on one ask's id, the ask open while they are read and its git placed
under that id, each part cut at a line's end once 64 K characters are held (a longer line where it stands), the last marked with git's
exit (0 where the read was stopped, -1 where git did not start). They ride the service's own lane, a channel four parts deep, never the
bus's frames: while four wait unread the read waits on its own flow. Git itself does not wait yet: the machine's run (`MachineRun`) drains
its pipe into an unbounded buffer, so a paused asker holds the rest of the answer in memory (GITBUS3c). Cancelling the ask or no longer
reading stops the read, and the parts end only once it has ended, a git that outlived its kill said as `ProcessUnendedException`; while its
git is placed the ask is shown with the repository it was placed in and its deadline (`GitServicePartsTests`). Any other kind is answered whole.

*Failed reads.* A read git did not answer (no exit: a deadline, a stalled repository, a git that did not start) is unknown, never
"clean", "not held" or "no change": every caller whose answer decides a tidy, a discard, a landing, a merge, a replay or a resolver's
settle leaves the state unknown and changes nothing. A dirty check is `MachinePorts.Repository.UncommittedAsync`, null on a failure;
whether a branch is on its remote (`TreeRefs.OnRemoteAsync`) and whether one commit is an ancestor of another that a replay's range
rests on (`TreeRefs.AncestryAsync`) say present, absent or unread, and an unread one refuses. A resolver's work the tree is put back
from is kept on the edits ref whether the rebase ended or not (`ResolveTree.RestoreAsync`), so a refused read costs it nothing.
Swept in the driver, each caller by its read:
- Fixed, with a test that fails the read and finds nothing changed (`FailedReadsDecideNothingTests`, `ResolveFailedReadTests`): the
  merge's dirty check and a tree's status in a landing, in the move onto what landed (failed from its own read on) and in an update's
  detached re-read (`TreeMerges`, `TreeBranches`, `TreeLandSyncs.MoveTreeAsync`, `TreeUpdates`); a branch's remote ref
  (`TreeLandSyncs`, `TreeBranches`); a step's recorded base in a submission update (`TreeUpdates`); the resolver's conflicted-files,
  other-changes and stop-commit reads (`ResolveTree.MarkedAsync`, `OthersAsync`, `ProvedAsync`, the first of its two reads) and the
  edits kept on a restore. Earlier: the partner's taken-in read (`PartnerActRunner`), `BranchJudging`'s and `BranchReplays`' status.
- Fixed, read in the code and compiled but with no test of its own: a branch advance's head, ancestor, count and join reads
  (`TreeBranches.AdvanceAsync` and the join before it), the tree update's other status reads and the `ProvedAsync` read of the stop
  commit's tree, `BranchJudging`'s record-base and remote reads, `TreeLandPlans`' remote read (a plan, which treats unread as pushed), and
  the generated writers' staged-difference check before a commit (`GeneratedWriters`).
- Already safe, reading the exit before the output: `LandPerformer`'s tree and root status, `OnlyHereProof`, `TogetherPress`,
  `RebaseReplay`'s uncommitted read, `ResolveTree`'s hand check, `DependencyKeys.InputChangedAsync` (a failure counts as changed),
  `RelatedSessions`, the content proof and `LandedProofs` (a failure is unread), `TreeSweeps`' empty-branch count (only "0" removes),
  `PieceKeeper.FreezeAsync` (a create-only ref, which git refuses on a ref that stands), `BranchReplays`' and `TreeUpdates`' put-back
  reads (a failure reads as not back), `TreeSquashes.IdentityProblemAsync` (a failure refuses the landing).
- Not deciding an act, left as they read: counts that only appear in words (`TreeRefs.CountAsync`), `SessionsSuperviseCommand`'s
  branch line, `ReviewRanges`' branch name, the stash plan's unmerged line and collision listing (`StashActs`, where git itself refuses
  an overwrite), and the partner piece's base (`PartnerPersonAsk.ReadTreeAsync`, which hands a piece the lead's tree and destroys none).

What holds git's places is counted by its caller and read kind (`GitAskers`, `GitAskTally`): each ask the scheduler places is told under
the caller its flow names (`look` for a look stage's work, `opinion-gate`, `door` for a person's read, `facts` for a machine-facts topic,
`check`, `run-step`, and the flows that ask outside a look: `pool`, `tidy`, `sync`, `pull-request`, `plugin`; else `other`, the innermost
name standing) and its kind, with the milliseconds it waited for its place. Each look's stage line says them as
`git=look:234/1925ms[status:90,diff:40,ls-tree:30],door:3/10ms[log:3]` (count, waited, then the caller's top three kinds), and the bus's
measures carry them by the day as `caller/kind`, one write for each caller and kind of a look, which `daoris bus stats` sums as `git asks
by caller` and lists as `git asks by caller and kind` (the top twelve). An ask no flow named is `other`; the entry of each is noted
(`GitAskers.NoteSite`) and a look's machine-log line `git.other` names the sites and counts, so what remains is named by the next look
and tagged at its entry.

The driver asks every whole, byte and streamed git through the git port (`IGit`) its root hands: `GitRuns` answers from its flow's
memos first and asks the port for the rest, and the roots hand `GitProcessRuns` (the git adapter, `Daoris.Adapters.Git`) over the
machine's process host. It runs each git as a process through the host, which joins it to the git job, and asks the driver's
`GitRunPolicy` (`IGitPolicy`) for everything that is Daoris's: the program and its variables, the read the library can give in
process, the scheduler's place, and the count and timing the launch is kept in. A unit test chooses its flow's git through the
test root's `FlowGit`: `ScriptedGit` answers or watches it, and nothing in the driver stands in for git. Starts and reads around the
port (a git start composed by hand, git's own files, the library) are counted per file by the layers census (`git`), an allowance
that only falls, and the port's contract runs on `ScriptedGit` in the unit tier and on `GitProcessRuns` in the adapter's own suite,
which runs only in the adapter's own development (`node tools/merge/packages.ts release <name>` after its sources or port change), never in a merge or the full set.

- Four run at once. The background and every git waiting outside Daoris (a remote act, a hook) hold three together, so a
  person's read always finds the fourth; one repository runs at most two background reads. A bridge route's gits are a person's.
- Asks queue per repository, the oldest that may go of the highest class first: a person's, a step's, the background's. A write passes nothing queued ahead of it, nothing passes it,
  and it never shares; a read shares an identical queued start only with no write queued after it. An opening's asks
  (`AsOpening`) alone queue ahead of the background's, a write included, behind only the openings' own queued before them, so the
  loop's queue of tens never holds an opening's tree write (`GitSchedulerTests`). A `worktree add` that holds its hooks
  (`TreeOpenings.HooksHeld`) runs none, so it waits on nothing outside Daoris and is not one of the two outside places that made
  four trees opened at once two and two (`GitStartAsks.WaitOf`).
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
- A pool act (a make, a take, a return, an end, a retirement, a finish) and the files stage ask git once for what full ids alone name
  (`AddressedReads`: a commit's `ls-tree -r -z`, a `grep` of it, a `cat-file --batch` of ids and paths at them), whatever writes come between,
  since an id names the same object; a read naming HEAD, a ref, the working tree or an index is never kept, nor an answer git did not give,
  nor anything while a replacement can be in force (`GitReplacements`), nor a missing object in a repository with a promisor (its fetching retry asks git).
  A take reads the file rules up to four times (the judgement, the plan, the toolchain's key, the check) and each reading's ignored tops
  are the slot's files', so those are read each time; `TreePoolStartsGitTests` holds an end and a take to one listing, search and pair of
  file reads each.
- The declarations' store (`Workplace/Declarations/`) answers a declaration present, absent or unread, by `FileStamp`,
  `ContentKey`, or a commit's listing kept only where each object's bytes hash to its id; while a replacement can stand it asks
  git and keeps nothing. Names are fields of `DeclarationNames`; the banned-name analyzer (`BannedSymbols.txt`) and the
  declaration censuses hold that nothing else reads one.
- `GitKeepersCensusTests`, `GitLifetimesTests`, `GitEndingsCensusTests` and `GitStartsCensusTests` hold the classes, each run with
  every member left out and one planted omission; `GitSchedulerProcessTests` holds the scheduler on real git.

The refs' one listing and a plain read's merge-bases, counts, changed names and `rev-parse --verify --quiet` of `HEAD` or a full ref
name (with or without `^{commit}`; a short name, an operator or a dangling ref is git's), and a plain `rev-parse HEAD`, are
answered in git's words by LibGit2Sharp where admitted (`GitLibraryReads`, `GitLibraryVerify`), starting nothing; so are the layout
and the settings read from the folder's own files: `--show-toplevel`, the combined layout `rev-parse` and its `--show-toplevel
--abbrev-ref HEAD` part, `worktree list --porcelain` (the main tree, then each linked tree by path, its lock and reason), `config
--bool core.ignorecase` and a branch's `@{upstream}` in its two forms (`GitLibraryLayout`, `GitLibraryWorktrees`,
`GitLibraryUpstream`, the path as the file system spells it by `GitLibrarySpelling`). A read after a write asks the files again, so
none is ever stale. What stays git's: a subfolder, a bare repository, `core.worktree`, an include, a tree git calls prunable, a
head unborn or ambiguous, an upstream whose ref is gone, a path through a link or a short name. Everything else is git's. The
look (`Driver.LookAsync`) holds one `GitFacts` table for its whole run, so a folder's layout and a commit's id-keyed reads
are asked once between two writes. A tick's git starts are held to a budget by `TickGitBudget`; what a resume tick still
starts is its writes (`switch`, `update-ref`, the rebase, a tree's lock and unlock), `status` where the library leaves it to git, the file reads
(`ls-files`, `ls-tree`, `log`) and the declarations' object reads, each of a different commit and asked by a different store.

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
| The look's tidy, six trees (two) | 5 (5) | 0 | 1 (0) |
| A tree's walk (`TreeReadPass`), a tree the line holds by content: first, then its kept read after an edit | 5 (1) | 0 | 1 (0) |
| Four trees of one repository walked at once after a branch of the person's moved (`BranchListingShareTests`) | 5 | 0 | 0 |
| A session's start, to its spawn | 20 | 0 | 3 (0) |
| A squash press's landing | 19 | 0 | 4 (1) |
| A merge press's landing | 32 | 3 (the line's commit) | 9 (0) |

A tree's walk takes its head and branch from the tree's own files and asks `status` once, where it learns which paths it needs: the
default one, or for a tree the line holds by content the proof's (every untracked file named, ignored ones too), whose non-ignored paths
are then its count. The declared output that proof read is kept with the read, not read again. The proof's last comparison lists
the repository's branches newest first (`BranchesByDate`): one listing per repository and version of its counted refs, shared by
every walk that asks for it while it runs as after, so the look's stages walking one repository's trees at once (the tidy, the owed
opinions, the nodes' reads) start it once when a branch of the person's moves, not once per tree. Each asker says the listing's wait
for a place as its own stage's queue; one leaving ends only its own wait, and the last leaving ends git's read, as the scheduler ends
a read nobody waits on (`BranchListingAskersTests`).

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
The scenario classes over real repositories (landings, the pool, integrations, clean-up, keep-size, the help trees' read, tree
syncs, the ref, stash and switch acts, fetches, tidying, the scheduler on real git) run in the driver's rehearsal project. The pool's
takes, returns, line ends and refusals run on `MemoryRepository` (`TreePoolMemoryTests`), its slots made by the real tree opening, which
asks the repository port; the pool cases left on real git are those whose git is a held, process-owned start (the journaled ref act
behind a take's undo and a finish's branch delete) or whose subject is the disk (outputs held and set aside, hooks, nested repositories).
The integration run's replay of a merge rule's press (a merge commit whose second parent is the frozen tip, a request the line holds
settled at once, a conflict aborted and handed back, a stop kept until the line moves) and its crash recovery by ancestry run on
`MemoryRepository` (`IntegrationReplayMemoryTests`, `IntegrationResumeMemoryTests`): the replay, resume, promotion, settle, line
pull and landed review ask the port. What stays on real git is whatever reads the line's holds by patch and content (`LineHolds`,
`ContentHolds`, `BranchJudging`), the backlog's rows, a diff's text, and `ResolveTree`'s rebase and index plumbing.

Whether a session's work landed is read by patch, never by commit ids alone: `SessionTrees.WorkAsync` (`TreeWork`) counts none to land
once the line holds the work by ancestry, a squash, a replay's patch or the merged branch as the line stands. The id counts
(`UnlandedAsync`, `UnlandedInTreeAsync`, `UnlandedHereAsync`) are named in `LandedByIdsCensusTests`; a new caller reads the tree's work or
is named there with why.

The landed proof (`LandedProofs.ProveAsync`, the changed names every proof reads, the standing tip), the hand-back read, the step refs, and
the workflow nodes' one-commit, ancestry, merge-base and ref-list reads ask the repository port, and their cases run on `MemoryRepository`
(`LandedProofMemoryTests`, `HandbackReaderTests`, `StepRefsMemoryTests`); the review graph's reads (a command-level census of what
starts), the diff and patch text, and the merges, rebases and the landing's judgements stay on git's commands.
Gates: `driver`, `driver-process-git`, `driver-process`, `driver-rehearsals`, `censuses`,
`modules`, `service`, `web-unit`, `web-types`, `deployment`; visible words need both catalogues.

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
- **STARTPERF1**: the cuts the counts name, and the other test classes that start git counted.
- **GITLIB1g**: the library's two ends.
- **FABRIC-PERFORM1l**: the pull request's fields kept on the wire evidence (`WorkEvidence`).
- The identity's `api` token setter, read only by the adoption, is the next removal (no row).
- D178 point 6, *Hand mine to the winner*, and D47's OIDC sign-in on a shared deployment: no row until asked for.

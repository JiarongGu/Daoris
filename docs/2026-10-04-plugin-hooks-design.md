# Plugin hooks: a landed branch's state on its platform, and where else a plugin may speak

> PLUGHOOK1, decided as D148 (2026-10-04). PLUGHOOK1a and 1c are built (their notes on D148); 1b, 1d and PLUGHOOK2 are not. `Dnn` is `docs/decisions/Dnn.md`, and the plugin design
> is [`2026-09-23-plugin-design.md`](2026-09-23-plugin-design.md). The owner, on what the work repository showed: *"this
> is more like an Azure DevOps related logic so we need to do this as a plugin change, and also this is a good chance
> to design plugin hooks into other processes"*.

## 0. What the work repository showed

The owner completed a pull request in Azure DevOps by squash. The session branches whose commits it carried still look
unmerged to git: `git merge-base --is-ancestor <session tip> <line>` fails, because a squash puts one new commit on the
line, though the line's tree equalled the merged branch's. Nothing Daoris has sees that work as landed:

- **LAND3's tidy** (its note on D102) removes a session branch whose tip the landed ref contains, at the landing. A
  squash afterwards makes no ancestor.
- **D88's proof** counts a session's commits as landed while a branch of the person's holds them. The platform deletes
  the source branch on completion, a fetch with `--prune` drops origin's copy, and once the local landed branch goes,
  they are on no branch.
- **D102's proof by content** clears a landed branch whose files read on the line as it left them. It fails once the line
  changes one of them again, or a reviewer pushed to the pull request, and it never judges a session branch.

Only the platform knows that the pull request completed, how, into which branch and with which merge commit. D102
rejected asking it as *a network call and a platform's API in core*. This design keeps that rejection for core and asks
the plugin that opened the pull request, which already speaks for its platform (D87, D100).

## 1. The hook points today

The wire is JSON-RPC 2.0, one frame per line, on a plugin process's stdio (plugin design §4). Daoris calls and the
plugin answers. A request from a plugin is never answered (D64 §7). `HookPoints.All` names three points, and the kit
carries one entry for each (D101).

| Point | When | Frame | Result | Waits | Who may act |
|---|---|---|---|---|---|
| `quest/consider`, a decision | Each look of the loop, for each consideration the planner marked *Start* | `quest { id, title, from, to }`, `repository`, `workspace`, `root` | `{ kind: "allow" }` or `{ kind: "hold", reason }` | 10 s. Late, wrong or silent holds the quest, naming the plugin | Every plugin switched on that listens there, in catalogue order. The first hold ends it, and a hold only stops a start |
| `session/ended`, an observation | After a tick concludes a session | `session`, `quest`, `repository`, `state`, `adapter`, `account`, `byPerson`, `note` | Anything; `{}` | 10 s. A failure is a console line | Every plugin switched on that listens there. It changes nothing |
| `work/land`, an act | Once a landing has made its branch: a press, a terminal's `trees land`, LAND2b's look; and a hand-off after it (D102) | `repository`, `workspace`, `root`, `branch`, `base`, `title`, `quest { id, title }`, `session`, `commits [{ sha, subject }]`. LAND2c adds `pullRequest` and `acceptedBy` | `{ pushed, pullRequest?, message? }` | 2 min. A failure leaves the branch | Only the plugin the repository's branch rule names, or the one named at a hand-off, installed, switched on and sound. It pushes and opens the pull request as the person's platform tools are signed in |

Around them, `initialize` carries `{ protocolVersion: 1, plugin, home, data, points }` and is answered, within the
point's own wait, with the points the process listens on, a subset of its manifest's. A process still there 2 s after
`shutdown` is ended. The loop keeps a process for each plugin on a loop point (`HookPoints.Loop`). A landing starts the
plugin it names for its one frame. Every plugin runs with the tools' environment (D121).

The examples are `hold-by-title` (the rehearsals' fixture, on both loop points), `github-pull-request` and
`azure-devops-pull-request` (on `work/land`), and `browser` and `in-app-browser`, which speak nothing and declare a
server. A harness on the ACP door and a server every session is handed are declared, never spoken (plugin design §3).

## 2. The first new point: `work/state`

`work/state` is a fourth kind of point, a **query**. Daoris asks a plugin for a fact only its platform holds, and acts on
the answer only where git confirms it.

### 2.1 Who is asked, and when

**The plugin that pushed the branch**, which the landing entry names (D102), since it opened the pull request on its
platform. Else the plugin the repository's landing rule names now. It must be installed, switched on, sound and speaking
on `work/state`: D100's four reasons, each in its own sentence. Otherwise nothing is asked, and the row says why. One
plugin is asked, never a waterfall, because one platform holds a branch's pull request.

**Only where git cannot tell.** Daoris's own proofs run first: D88's ancestry and D102's content. The plugin is asked
about an entry, standing or a trace (D113 §4), when all of these hold:
- its kept answer is not `completed`;
- it was not asked in the last minute, so a page that lists twice asks once;
- its answer could remove something now. Either it is standing and D102's proof did not clear it, or its recorded tip,
  where git still holds that commit, contains the tip of a session branch that stands and that D88 does not clear.

**At the occasions that may remove**, each a look before a press, or a rule the person set:
1. **LAND3's tidy**, after the landing's own plugin step, for the repository it landed in.
2. **The clean-up's look**: Settings → Workspace → Session branches, and `daoris-driver trees clean` without `--yes`.
3. **Bringing up to date's look**, after its fetch, which is where a merge commit first reaches this machine (D109).
4. **Ask again**, whatever the kept answer's age: a press on a landed session's review, and `daoris-driver trees state
   <session|branch> [--repository <name>]`.

**Never** at a press that removes, which re-judges with what is kept, so what was listed is what goes. Never at a read:
the review, Git's list, `trees land --plan`, `trace` and Ask Daoris show what is kept, and when. Never on the loop's look
or a timer, which D147 rejected for a fetch: a network call nobody is waiting on.

**The bound.** One process per plugin per occasion, started for the frames and stopped after them. Frames go one at a
time, the entry asked longest ago first. Each waits 30 s, the handshake included: one `az` or `gh` call takes seconds. An
occasion starts no frame after 60 s, and an entry it did not reach says *not asked this time*. A door that waits on an
occasion, such as the clean-up's list, waits that long.

### 2.2 The frame and the answer

`hook/work/state` is sent `repository`, `workspace`, `root` (the repository's checkout here), `branch`, `line` (the
repository's line, D86, or null), `pullRequest` (the record's, or null) and `pushedTip` (the commit the plugin pushed, or
null). A plugin finds the pull request by its address, or with none by its source branch, preferring one into `line`.

| Field | |
|---|---|
| `state` | Required: `open`, `completed`, `abandoned` or `unknown`. `unknown` is the platform answering without a state: no pull request from that branch, or a status Daoris has no word for |
| `pullRequest` | The pull request it answered for, as an absolute web address, or null |
| `mergeCommit` | Required for `completed`: the full id of the commit the completion put on its target |
| `sourceCommit` | Required for `completed`: the full id of the source commit the completion merged |
| `target` | The branch it merged into, without `refs/heads/`, or null |
| `how` | `merge`, `squash`, `rebase` or `rebase-merge`, or null. Any other word is read as null |
| `at` | When it completed or was abandoned, in ISO 8601, or null |
| `message` | The plugin's sentence, or null. Kept to 300 characters |

Anything else is not an answer (`unreadable`). A tool that fails is the plugin's error (`errored`), with the tool's last
line. Neither is a state.

### 2.3 What a completed pull request proves

A `completed` answer clears an entry when git confirms three things here:
1. **Its target is the line**, where the answer names one.
2. **Its merge commit is on the line**: held here, and an ancestor of the local line or of `origin/<line>`.
3. **It carried the branch**: the branch stands at its recorded tip (D102's rule), and that tip is the source commit or
   an ancestor of it held here.

Then **the landed branch goes**, at the clean-up's press and at bringing up to date's press, as D102's other proofs do.
It is marked `removedAs: "pull-request"`, with the form of the line that held the merge commit. **A session branch
goes**, at LAND3's tidy and at the clean-up's press, where its tip is the source commit, an ancestor of it, or an ancestor
of a tip the answer cleared. Every keep still applies: D102's for a landed branch, and LAND3's and D88's for a session
branch (a tree a session running or waiting holds, uncommitted work, a branch checked out). The order is D102's: session
branches first, then landed ones.

A row may say something else instead, each as a code, and nothing goes on any of them:
- `open`, `abandoned` and `unknown`;
- `other-target`: it completed into a branch other than the line;
- `merge-not-here`: the merge commit is not on the line here. Bringing the repository up to date fetches it;
- `beyond`: the branch holds commits the pull request did not carry, as after LAND2c advanced it;
- `source-not-here`: the pull request merged a commit this machine never had. A reviewer pushed to it, and the platform
  deleted the branch before this machine fetched.

### 2.4 When the plugin cannot answer

**Nothing is removed, and absent is never zero.** Nothing moves when the plugin is not ready, does not start, answers
late or wrongly, refuses, or is not reached within the occasion's bound. A failure never overwrites an answer. A pull
request kept as `open` at ten stays *open as of ten*, and the row adds that asking again at half past failed, and how:
`unstartable`, `exited`, `late`, `unreadable` or `errored`. On the Plugins page, the cost line for a failure at
`work/state` says *nothing is removed on its word until it answers*.

### 2.5 What is kept, and the doors that read it (D143)

Removing a branch on a platform's word is a choice, so Daoris keeps what it chose by on the landing entry
(`landings.json`, machine-local, D63):
- **`pullRequestState`**: the latest answer (`state`, `pullRequest`, `mergeCommit`, `sourceCommit`, `target`, `how`, `at`
  and `message`), with the plugin and when it was asked. D147 §3.4 left this field to be named. `completed` is final and
  is never asked again;
- **`pullRequestAskFailed`**: the latest failed ask's code, plugin and when, kept while no answer is newer;
- **`removedAs: "pull-request"`** and `removedOn`, when the branch went on it;
- **`carried`**: each session branch removed on its answer, with its tip, when, and whether the tidy or the clean-up
  removed it.

A trace keeps them, within LEFT3's 50. The machine log gains `plugin.*` lines by `state`, carrying the answer's code
and never the address or the plugin's words (D119 §4.2).

The doors that read it: the review's note (D113 §3), with *Ask again*, as in *its pull request completed by squash into
`main` on 4 October, as `azure-devops-pull-request` answered at 14:02*; `trees land --plan` (`LandedReviewWords`); each
row of the clean-up and of bringing up to date; `trees state`; Git's list and `git branches` (that design's §2.2, whose
GIT1a note carries only the link until this field is named); `trace`; and Ask Daoris. `trees state` is exempt from Ask
Daoris's proposals as a read (D110 §4): it changes nothing of the person's and nothing on the platform.

### 2.6 What the Azure DevOps plugin does

Its manifest declares `["work/land", "work/state"]`. `land.mjs` answers both through its `run()` helper, which quotes for
`az.cmd` (plugin design §9, rule 5). It runs in `root`, so `az` detects the organisation, project and repository from
`origin`:
1. **Find it.** Take the id from `pullRequest`'s `…/pullrequest/<id>`. With none, run `az repos pr list --source-branch
   <branch> --status all --output json` and take, among those into `line` where there are any, a completed one, else an
   active one, else the newest abandoned one. If none is found, answer `unknown`.
2. **Read it.** `az repos pr show --id <id> --output json`.
3. **Map it.**
   - `status`: `active` is `open`; `completed` and `abandoned` keep their names; anything else is `unknown`.
   - For a completed one: `lastMergeCommit.commitId` is `mergeCommit`, `lastMergeSourceCommit.commitId` is
     `sourceCommit`, `targetRefName` without `refs/heads/` is `target`, and `closedDate` is `at`.
   - `completionOptions.mergeStrategy` is `how`: `noFastForward` is `merge`, `squash` and `rebase` keep their names,
     `rebaseMerge` is `rebase-merge`, and absent is null.
   - Completed with no merge commit is answered `unknown`, saying so.
   - `pullRequest` is `repository.webUrl` followed by `/pullrequest/<pullRequestId>`, as `land` builds it.
4. **Fail as itself.** An `az` that fails, whether not signed in or missing the `azure-devops` extension, answers the
   call's error with `az`'s last line.

Its README gains the point under *What it runs*. *What it needs* is unchanged: `az login`, and `az extension add --name
azure-devops`. Its tests drive a fake `az` that prints the platform's JSON for each case, and never a network: completed
by squash, active, abandoned, completed without a merge commit, found by branch, none found, and `az` failing.

### 2.7 What the GitHub plugin does

The same, with `gh`. With an address it runs `gh pr view <pullRequest> --json
state,url,mergeCommit,headRefOid,baseRefName,mergedAt,closedAt`. With none it runs `gh pr list --head <branch> --state all
--json …`, with the same preference.
- `OPEN` is `open`, `MERGED` is `completed` and `CLOSED` is `abandoned`.
- `mergeCommit.oid`, `headRefOid` and `baseRefName` fill `mergeCommit`, `sourceCommit` and `target`, and `mergedAt` or
  `closedAt` is `at`.
- `how` is null, since `gh` does not say how a pull request was merged.

## 3. Other processes worth a hook, weighed

Each is weighed by D54 and D24: build only what is worth it. None gains a point now.

### 3.1 A pull request's review threads, back to its session (the analysis's §42)

**Point:** `work/review`, a query with `work/state`'s frame, answered with the threads (`[{ id, author, path, line,
status, text, at }]`, at most 50, each text kept to 2,000 characters). **Use:** the landed session's review shows them,
and the person picks which to send, to the session as words (D137), whose new commits advance the same pull request
(LAND2c), or as a new ask (D65). **Permissions:** it reads as the person's platform sign-in, and acts only on a press. A
reviewer's words are material shown as written (D142), never instructions. **Not now:** it needs LAND2c, nobody has yet
retyped a comment into Daoris, and the review (§3) holds §42 until a workspace asks. Its trigger is the first change
request carried over by hand. The name is kept here.

### 3.2 A pipeline's verdict as evidence (D144 §4)

**Point:** `work/checks`, a query: the pull request's checks (`az repos pr policy list`, `gh pr checks`), each with its
name, verdict and commit. **Use:** gate evidence for a repository that lands through pull requests, not the queue, which
D144 §5 leaves unread. **Not now:** EVID1 is not built, and the order conflicts. The pipeline runs after the push, while
D145 lands a done only once nothing holds it, so a check waiting on the landing would hold it forever. A platform check is
also not a declared gate (D26), so the gates file would have to map one to the other. It waits on EVID1b and a decision
on that order.

### 3.3 Work items as asks (§41)

**No point.** An ask is the person's words (D133), and the intake already reads a ticket through a server a plugin
declares (intake design §2, D78). A point turning a platform's items into asks would start work from words the person
never said, or poll a platform on a timer.

### 3.4 A session's start: capabilities and secrets (§25)

**No point.** A credential in a session's environment is one the agent can read. §25's shape exists already: a server a
plugin declares holds the credential and offers its acts as tools, and the session's rules allow or deny each (D72).
`quest/consider` already holds a start for a policy. Declared capabilities wait for the first plugin from outside Daoris
(the review's §3, PLUGDIST1f).

### 3.5 A quest's close

**No point.** `session/ended` already says a session ended on a done. Acting on a platform at the close comes too early,
since the pull request is the last human step (D145), and the platform completes a linked work item itself when the pull
request completes. `work/land`'s frame could later carry the ask's links (`Ask.Links`) for a plugin to link the item,
once a workspace's tickets live on the platform of its pull requests.

## 4. The rules every point keeps

1. **A folder that declares, and may speak** (D64). A point is a frame on the wire. No code loads into a host, and the
   host keeps nothing about a plugin beyond its manifest.
2. **Declared, then confirmed.** A plugin is asked only at points its manifest declares and its handshake confirms.
   `HookPoints.All` is the one list, and a point with no kit entry fails a test (D101).
3. **A new point is additive; `protocolVersion` stays 1.** Today a handshake naming a point this build lacks refuses the
   whole plugin, so a plugin that grew a point would lose its landing on an older build. From PLUGHOOK1a that point is
   named once and never asked. The kit stays strict.
4. **Each kind fails its own way.** A decision fails closed. An observation is contained. An act comes after Daoris's own
   step and undoes nothing. A query fails toward keeping: unanswered is *not known*, never a default.
5. **A bound per point**, the handshake inside it, stated in the kit's table and the plugin's README; 2 s after
   `shutdown`.
6. **Who is asked is stated.** A loop point asks every plugin switched on that listens. An act or a query on a
   repository's work asks the one plugin a rule or a record names. Nothing runs that the person did not install and
   switch on, and a plugin acts on a repository only where a rule names it (D100).
7. **A refusal says who and what.** Daoris's sentence names the plugin, the frame and the failure: a code on the page, the
   driver's English at the terminal (LANG1a). The plugin's own sentence is shown as written (D142), never guessed.
8. **What the person sees.** A console line under `plugin:<id>`, which stays on the machine; `plugin.*` lines in the
   machine log, without words; health on its page (D119); and the result where it is used.
9. **What a choice was made on is kept** where the choice is recorded, with when and which plugin (D143).
10. **Credentials stay the platform tools' own.** Daoris holds none. A plugin keeps its state in `${data}`.
11. **Two doors** (D50). Each act or query a screen presses has a terminal verb, and `daoris-driver plugins try` sends
    each point's sample.

## 5. Against the decisions

- **D102.** Its rejection of asking the platform stands for core and is lifted for the plugin. A landed branch gains a
  fourth proof, `pull-request`, and a hand-off refuses a branch it clears, as *work that already reads on the line*.
- **D88, and LAND3's note on D102.** A session branch's work is also landed where a completed pull request carried it
  and git confirms the merge commit on the line.
- **D113 §2–§3**: the review's `landed` answer carries `pullRequestState`, and its note says it. **D147 §3.4** and its
  GIT1a note: that is the field they leave to be named.
- **D64 §4, the plugin design §4 and §9, and D101**: a fourth kind and its point, the kit's entry, and a point this
  build lacks ignored rather than refused. **D100**: both example plugins speak a second point.
- **LAND2c** (D145 §3, not built) must read it: advancing a branch whose pull request completed would push work no pull
  request carries.
- **These stand.** Daoris never pushes on its own (D37, D87). Nothing is deleted without a press or a rule the person set
  (D51 rule 7, D88). A landing writes no quest state (D46). No plugin adds a view (D52).

## 6. The build

| Row | What lands | Proven by |
|---|---|---|
| **PLUGHOOK1a** (after LAND3 lands) | The point, its frame and its answer read by shape; the kit's entry; a point this build lacks ignored; `pullRequestState` and its companions on the landing entry; the Azure DevOps plugin's answer; LAND3's tidy and the clean-up asking it and acting on a completed answer (§2.1–§2.6, §4) | `HookTests`, the kit's answer table on both checkers, `LandedRecordTests`, git fixtures for each code of §2.3 with a fake plugin, `landing-plugins.test.ts` with a fake `az` |
| **PLUGHOOK1b** | The GitHub plugin's answer (§2.7) | `landing-plugins.test.ts` with a fake `gh` |
| **PLUGHOOK1c** | The terminal: `trees state`, `trees land --plan`'s line, the clean-up's codes, `git branches`' state, and bringing up to date asking after its fetch (§2.1, §2.5) | `DriverCommandTests`, `HelpCoverageTests`, `GitBranchesReadTests`, the sync's git fixtures |
| **PLUGHOOK1d** | The page: the review's note and *Ask again*, the Session branches rows and the sync's rows, in both catalogues (§2.5) | Module route tests, vitest and stories, catalogue parity, `names-check`, the look in both themes |
| **PLUGHOOK2** (held) | `work/review` (§3.1), once LAND2c lands and a change request is first carried over by hand | The design's §3.1 |

# The access model: Daoris extends every agent, restricted mode is the harness's own approval mode, and a workflow's step is the only wait Daoris makes (ACCESS1)

**Decided 2026-11-12 (D284).** The owner directed on 2026-11-12 that Daoris must never make an agent less able than its own harness
run by hand: there is no door kept for the person alone, a limit exists only where a workflow's author put a step or where the person
switched a rule on, restricted mode is the harness's own approval mode with nothing of Daoris's added, and approval is one model for
every agent Daoris runs. Written by Claude Opus 5.5 on 2026-11-12 from main at `47f84d9f29`, from the restrictions audit of the same
day (135 rows over the driver, the modules, the service, the web, the CLI, the plugins and the words agents are told; the steward's
working notes, untracked, so §8 says each limit in a phrase and stands alone) and the areas it names. Read with
`docs/areas/agents-and-harnesses.md` (rules 18-23), `docs/areas/roles-and-supervision.md` (rules 51-61),
`docs/areas/git-and-remotes.md` (rules 12, 23-28), `docs/areas/knowledge-memory-records.md` (rules 18-21),
`docs/areas/desktop-app.md` (rules 13-15), `docs/areas/plugins.md` (rules 28-29a), D253 point 7, D277, D280 and D283. Nothing here is
built. FULLACCESS1, on its own branch, is row a. Driver paths start at `src/Daoris.Desktop/Daoris.Desktop.Driver/`, module paths at
`src/Daoris.Desktop/Daoris.Desktop.Modules/`, service paths at `src/Daoris.Service/`.

## 0. In short

- **Daoris extends an agent and never takes capability away.** In full access an agent Daoris runs has at least what its harness
  has in its own full-access mode, and Daoris's doors besides: shell, files anywhere the account reaches, pushes of every kind, `gh`
  and `az` as the person, every Daoris verb.
- **Two modes, both the harness's.** Full access is the harness's mode that asks nothing. Restricted mode is the harness's own
  approval mode; its prompts are the only asking, and Daoris relays them. Daoris adds no card, no deny and no clock to either.
- **One approval request.** Every harness's prompt becomes one Daoris record (who asks, what act, where) that goes to the place's
  approver: the person, or an agent the person appointed for a workspace or repository. The answers are allow once, allow always for
  this place, and deny. A workflow step's waiting party is named the same way.
- **The only waits Daoris makes by itself are a workflow's steps.** Every other limit is a rule the person switches on, off by default.
- **The floor binds Daoris's own code, never an agent.** Daoris never deletes or overwrites work it did not make, tracked files or
  nested repositories; an agent may do any of these with its own tools.
- **Ask Daoris finishes a simple act itself and sends work that changes a repository's content to that repository.**

## 1. The principle

Daoris is a layer of shared knowledge, quests, co-working and workflows over agents that already work. It adds to what an agent can
do; it never narrows it (D253 point 7, now the whole of the model). Today it narrows in four ways that hold whatever the mode, and adds
asking of its own beside the harness's:

1. it composes its own denies beside the mode, and a harness's full mode still honours deny rules and hooks, so full access carries
   the write-across, cross-workspace, others'-paths, git-profile and heavy-gate denies (audit A1-A6);
2. git access is apart from the mode, so the doors refuse every session push and every force, delete and tag (D1-D2);
3. the session's environment signs `gh` and `az` out (C1);
4. the connector refuses by kind of work and by subagent lineage (S1-S2);
5. and restricted mode carries Daoris's own confirm cards (F1-F3, F10), a 15-minute refusal (B4), a refusal of the harness's own
   *always* (B5) and an outright refusal of a one-job session's asks (A14).

The model removes each. What remains is what an agent's harness does by itself, what a workflow's author asked for, what the person
chose, and a floor under Daoris's own acts.

## 2. The two modes, per harness

The mode is set where it is set today: a repository's access setup for its sessions (`permissions`: `auto`, `asking`,
`unrestricted`), Ask Daoris's mode for Ask Daoris. `unrestricted` and Ask Daoris's `full` are **full access**; `auto` and `asking` are
**restricted**, the harness's judged posture and its asking posture. The default stays restricted and judged (`auto`); nothing becomes
full access by a migration. Who changes a mode is no longer fenced: the verb is an act like any other, and in restricted mode the
harness asks before an agent runs it.

| | Full access | Restricted, judged (`auto`, the default) | Restricted, asking (`asking`) |
|---|---|---|---|
| **Claude Code**, both doors | `bypassPermissions` | `auto`, else `acceptEdits`, on the protocol door; `acceptEdits` on the native door | `default` |
| **Codex** (over its protocol door) | `agent-full-access` | `agent` | `workspace-write` |
| **Another protocol (ACP) agent**, a plugin's door | the posture its provider declares as `Full` | its `Ordinary` posture | its `Asking` posture |
| **A harness that declares no such posture** (dsh, a stub, a plugin's door that names none) | its most open mode, started and said; its asks go to the approver | its own mode, said | its own mode, said |

**What a start hands, in every mode.** The harness's mode as the table says; the person's own tool home through the harness's
session-only doors (D262), Ask Daoris included; Daoris's connector and the plugins' servers as declared; and, as rules, only what the
person set: their own allow, ask and deny rules in `permissions.json` and the opt-in rules they switched on (§5). Daoris hands no deny,
no hard denial in the classifier's sentence and no guard hook of its own. The hooks that only record (the lineage hook, the event
hooks) stay, and deny nothing. A harness with no rule language (Codex) holds none of the person's rules on its own tools; its
standing block says so, and the git doors still judge the person's git rules for it.

**Full access.** The agent runs its harness's mode that asks nothing, so nothing waits but a workflow's step. Where the harness
declares no such mode, Daoris starts it in its most open one and says so on the start and the panel; whatever that harness still asks
goes to the approver as in restricted mode. Daoris never answers a harness's prompt by policy (D81's rejection of it stands): the
answer is the harness's own or the approver's.

**Restricted mode.** The harness's own approval mode, as a person running it by hand would see it. Its prompts are the only asking:
Daoris relays each as an approval request (§3), with every option the harness offers, and adds nothing. A `daoris` verb an agent runs
is asked as the harness asks any command it would not run unasked; there is no Daoris card behind it. Nothing is refused by
construction: no request ends on a clock, *always* is offered wherever the harness offers it, a duplicate request joins the one held,
and a one-job session (reviewer, helper, partner, brainstorm pass, supervisor's turn, plugin step, packet reader) asks like any other.
On Claude Code's native door, which answers its own asks under `-p`, the `PreToolUse` ask hook (`permission-ask.mjs`) raises the
request for every session; the protocol doors raise it on their wire.

## 3. One approval request, one approver

### 3.1 The request

Every source of asking becomes one record, `ApprovalRequest`, generalising today's `HeldPermission`:

| Field | What it holds |
|---|---|
| `id` | Daoris's own id; the harness's request id kept beside it for the answer |
| `asker` | who asks: its kind (Ask Daoris, a session, a one-job session, a role's holder, a plugin step), its session and conversation, its harness and account |
| `act` | what: the source (a harness's prompt, the git door, a rule proposal, a rule the person set to `ask`), the act's kind (`execute`, `edit`, `fetch`, a push and its update's class, a rule's change), the agent's own title for it, bounded, and the options the harness offered, by id and kind |
| `place` | where: the workspace, the repository and the tree, or the machine |
| `approver` | who answers (§3.2), and why: the appointment that names it, or none |
| `since` | when it was raised |
| `answer` | the choice, who gave it, when, and for *always* the place and the rule written; or how it ended without one (`cancelled` with its turn, `ended` with its session) |

The sources: Claude Code's permission request on the protocol door (`ChatHolds`), the native door's hook (`NativePermits`), Codex's
and every ACP agent's `session/request_permission`, the git door's held act (`run/git/asks/{id}.json`, the one source in another
process, so its file is kept in the same shape), a rule proposal that widens (`permission_propose`), and any wait the person's own
rule asks for (a git access `ask`, a hand-over or helpers `ask`, routing `person`, commits `ask`, `checks-accept`, a memory approver).
A live request is held by the driver's loop, as `HeldPermissions` holds it today; a restart ends the processes that asked, so their
requests end with them. Each request is written on the asking conversation's events when raised and when answered, so its
conversation shows it after the fact.

### 3.2 The approver

The approver of a request is the place's approver for its kind, resolved by one rule (`Approvers.For(place, kind)`): the repository's
appointment, else the workspace's, else the machine's, else the person. The appointments are the role system's supervisors of a kind
(D233's note): permission prompts, rule widenings and a rule's `ask` are the `allow` kind; a review's Accept and a `human` approval are
the `landing` kind; a session's question is the `question` kind; a `go-ahead` is a kind of its own, which D233's note had excluded. A
workflow step may name its waiting party outright (`person`, or a role), and then that party answers whatever is appointed. Ask
Daoris's place is the machine, so its requests go to the person unless the person appointed a machine approver. An agent's own request
never goes to itself: where the asker is the approver, it goes to the person.

### 3.3 The answers

- **Allow once.** The harness's `allow_once`; the act runs.
- **Allow always for this place.** Kept as an allow rule at the request's place in `permissions.json` (the act's pattern, the
  harness's own suggested rule where it offers one), beneath every rule the person set, so it never lifts the person's own deny or ask.
  The running session takes the harness's own `allow_always` where it offers one; the next identical request at that place, on a
  harness that holds no rules, is answered from the kept allow and attributed to whoever gave it. Where the request was raised by a
  rule the person set (a git access `ask`, say), *always* keeps a grant for that exact act at that place, as the git door's kept grant
  does today, and the rule stands. Settings lists each with *Take back*.
- **Deny.** The harness's `reject_once`, with the approver's reason told to the asking agent. A standing deny is a rule the person sets.

A request waits until it is answered or its turn or session ends. Where a harness ends a wait itself (a hook's own bound), Daoris
hands it the longest bound it takes and, if that still ends, parks the line and says so; Daoris refuses nothing on a clock.

### 3.4 Where it shows

- **What needs you**: a request waiting on the person is one row, naming the asker, the act and the place, with the three answers.
  The window's Notifications group and the terminal's `daoris notices` read the same rows (D283 point 5).
- **Notifications**: a request waiting on an appointed agent is listed as *waiting on* that role, with *Answer yourself*; an
  appointed agent's answer is listed as its decision, a standing allow with *Take back*.
- **The session**: the card in its conversation where the agent asked, the held tool call's card CHATUX1 draws, with the same three
  answers; the session's head says whom it waits on.
- **The terminal**: `daoris approval list` and `daoris approval answer <id> once|always|deny`, over one route, `APPROVAL_ANSWER`, and
  one read, `APPROVALS`.

### 3.5 An appointed approver agent

An agent the person appoints as the `allow` supervisor of a repository, a workspace or the machine is handed each request at its
place as a subject of its kind, with the act, the asker, the place and the rules in force. It answers allow once, allow always or deny
with a reason, through the role's act; its answer takes effect at once (D233's note), is kept as its decision and listed in
Notifications, and the person may answer instead at any time or take a standing allow back. An approver agent that leaves a request,
cannot start, or has no account free hands it to the person; it is never refused for that. No approver lifts a rule the person set
(the allow kind's guard on the person's own deny stays).

### 3.6 Identity answers waits; it never refuses an act

The person key, a session's delegate key, a role holder's key and the pipe's placement of a process stay, for two uses only:
attributing each act to its caller, and knowing who answered a wait. They never decide whether an act may run. A wait (an approval
request, a workflow step) is answered by its named party: the person through the window, a terminal the pipe places outside every agent
Daoris runs, or their words applied as D277 applies them; an appointed agent through its role. A press of a wait by anyone else is
not refused: it is put to the wait's party as that caller's request ("session #4 asks you to accept").

**A wait guards itself.** A change that removes a wait or moves who answers it is answered by the party that answers that wait now:
a workflow change that drops a step or lowers its party's part (workflows rules 12-13, X6), an agent's change of a quest's review or
workflow choice (S8), and an appointment that names a new approver for a place. This is not a door on the act: anyone may make the
change, and it applies on that party's yes.

## 4. Workflow waits are Daoris's only waits

A run waits where its workflow's author put a step: `review` (the Accept), `human` (approve or reject, and the `look`), `go-ahead`,
an unchecked done (D255), a `choose`, and a workflow change that lowers a party's part (§3.6). Each names its waiting party (the person
by default, or an appointed role) and is answered as §3.6 says. The person's through words pass a step in both modes (D277; the
restricted-mode ban goes, as D280 already decided). Full access does not lift a workflow's step: the mode is the agent's reach, the
workflow is the work's process.

Beside the steps, the only other waits are the harness's own prompts in restricted mode and the rules the person switched on that ask
(§5). Every other wait Daoris makes today goes: the seeded supervisor rules (I1), routing waiting for the person by default (B2), the
hand-over's `ask` default (U4), a changed checks declaration waiting in restricted mode (D283 point 1), the drain's held presses (F7),
and the caps that refuse (§8). Row m closes with a census that every wait Daoris makes is one of these three.

## 5. Opt-in rules

Every limit beyond a workflow is a rule the person switches on, off by default, each where its kind already lives (`permissions.json`'s
three scopes, the access setup, git access). Settings → Rules lists every one with its switch and where it is set. The person's own
rules hold as set, in every mode; no agent's answer, an approver's included, lifts one.

| Rule | When on | Today |
|---|---|---|
| `no-push` | git access's Write `deny` at the scope: the doors refuse a session's push, the harness's rules deny every push form (one record, git access; rule 28's policy) | fixed default |
| `no-force` | git access's Dangerous `deny`: force, delete, tag, mirror and prune refused at the doors | default `deny` |
| a git access `ask` | the door raises an approval request (§3) | the person's value, with a 15-minute refusal |
| `no-publish` | publishing and releasing denied, with its sentence | default on |
| `tree-guard` | writes outside the session's tree and declared targets refused | default on, forced on for three one-job kinds |
| `keep-in-repository` | an edit deny on every other checkout but the declared write targets | default (write-across) |
| `keep-in-workspace` | a read deny on another workspace's checkouts | default |
| a repository's *read off*, Ask Daoris's turned-off checkouts | reads refused there | the person's, kept |
| `others-uncommitted` | `git add -A`, `commit -a`, `stash` denied where the start found others' uncommitted paths | default |
| `daoris-secrets` | Daoris's git profile (`profiles/git/`) unreadable to sessions | default |
| `credential-isolation` | `gh`, `az` and the hosts' token variables dropped and their folders emptied | default |
| `persons-processes` | the person's processes and ports never stopped or taken over | words in a set-up brief |
| `folder-no-git` | no git started or run in a folder place | words |
| `no-disclosure` | agents set nothing that leaves the machine (`remote.join`) | words |
| routing `person`, hand-over `ask`/`off`, helpers `ask`/`off`, commits `ask` | each waits for the approver, or refuses where `off` | routing and hand-over default to waiting |
| `checks-accept` | a changed `daoris.gates.json` waits for one press | waits in restricted mode (D283) |
| memory `own-repository`, memory approver | memory writes held to the caller's repository; `memory_end` waits for its approver | by-work refusals |
| supervisor rules (answers alone, failures, tries, moves) | each holds or asks as today | seeded on every home |
| a place's checkout writes off | Daoris's stage, commit and switch in a registered checkout refused | default off |
| a plugin's tools per place, the browser's code-running tools held back | as today | the switches stay; the browser's code tools are now declared |
| heavy gates under `light` | the session's heavy gate commands denied | already the person's workflow choice |

## 6. The floor

The floor binds what Daoris's own code does, whether Daoris starts the act (a clean-up, a sweep, a landing, a withdrawal, a tidy, a
tree's move or copy, `sync`) or is pressed to run it (the act engine, the push act). It never limits what an agent does with its own
tools: an agent's `git reset --hard`, `git push --force` or `rm -r` runs as its harness lets it.

- Every branch Daoris deletes is judged by content and keeps a recovery ref; work Daoris did not make is never deleted (W1).
- Nested repositories are never entered, moved or deleted by Daoris's tree moves and copies (W2).
- The line is never deleted; a held, occupied or moved ref is not deleted; every write says how it recovers (W3).
- The act engine runs no hard reset, `clean`, force over work Daoris did not make, or remote delete, and acts in no tree where a
  session runs or a landing holds it (W4); row p checks that this binds the act engine alone and never an agent's own git.
- `sync` never overwrites a drifted or edited file without `--force` and never writes through a link (K3); the links Daoris shares into
  a tree are said as its own (N15); its own browser server is restarted past its memory bound (P1).

## 7. Ask Daoris's rule

Ask Daoris either finishes a thing itself or sends it on. **A simple act it finishes itself**: any Daoris verb or setting, a read
anywhere, a clean-up, a restart, a machine task outside every repository's content (a tool, a sign-in through a plugin), and a git act
that changes no repository's content (a fetch, a push of committed work, a branch withdrawn, a pull request through its plugin).
**Work that changes a repository's content** (its files, its commits, its line) goes to that repository as an ask, a quest or a session
(`repository-owns-its-work`). The rule is the same in both modes; the mode decides only who says yes to each call its harness asks
about. It is the room's working rule, words and not a gate; a person who wants it held switches `keep-in-repository` on.

Ask Daoris acts on the person's words, so in full access its presses are the person's, attributed to it and its conversation (D253
point 1), and it passes a run's step only on the person's through words (D277). It may ask a helper, open a co-worker, read every
workspace, use the person's own servers, plugins and browser, and change its own mode, each asked by its harness in restricted mode.
D280's fences two to five go (its own mode and grants; a secret or a sign-in; staging, installing or restarting Daoris; another
repository's files): the first is now a rule the person may set, the second and third are acts its harness asks about, and the fourth
is this section's working rule. D280's first fence, the person's own rules and the floor, stays.

## 8. Every audit row's end state

*Gone*: the limit is removed and nothing replaces it but the harness's own asking in restricted mode. *Opt-in*: a rule of §5, off by
default. *Declared*: a step, rule or pass that starts the session names the posture. *Paces*: waits, queues, or cuts and says, never
refuses. *Workflow*: a step's wait (§4). *Floor*: §6. *Harness*: already left to the harness's asking. FA1-FA4 are the four families
FULLACCESS1 counts, beside the audit's rows.

| Rows | The limit today | End state | Row |
|---|---|---|---|
| FA1 | 157 bridge routes declared person-only (`PipeClass.Person`) | Gone: open to Ask Daoris in full access, then to every caller | a, d |
| FA2 | `no-push`, `no-publish` and the tree guard as fixed or default denies, with their classifier sentences | Opt-in | a |
| FA3 | terminal verbs that refuse while the desktop runs | Gone: they act through the running Daoris | a |
| FA4 | a plugin's command line refused from a session | Gone | a |
| A1 | Daoris's rules composed beside the mode, so full access carries every Daoris deny | Gone: a start hands the person's rules and switched-on opt-ins | b |
| A2, E1, U7 | an edit deny on every other checkout but a declared target | Opt-in `keep-in-repository`; a declaration narrows | k |
| A3 | a read deny on another workspace's checkouts | Gone; Opt-in `keep-in-workspace`; *read off* stays the person's | k |
| A4 | `add -A`, `commit -a`, `stash` denied over others' uncommitted paths | Opt-in `others-uncommitted` | b |
| A5 | a read deny on Daoris's git profile | Opt-in `daoris-secrets` | b |
| A6 | heavy gates denied under a `light` pipeline | the person's workflow choice, kept, never a default | — |
| A7, A8, A9, A11, A12, A16, N11 | one-job sessions' fixed read-only, no-shell, packet-only and connector-only postures and their words | Declared; the default is the working session's | l |
| A10 | a partner's push deny and acting-tool denies | Gone (the push deny: its clone has no remote); the rest Declared | l |
| A13 | the tree guard forced on for a partner, a supervisor's turn and a plugin step | Gone | b |
| A14 | one-job sessions handed no ask hook, refused outright | Gone: every session asks | c |
| A15 | a one-job session drops the person's own allows | Gone; the start's mode still outranks the person's settings | l |
| A17 | `knowledge_refresh`, `checkout_read` asked; a sentence says "refused" | Harness; the sentence fixed | h |
| B1, E2, E4, S21 | the default judged posture; reads asked, not refused | Harness, kept | — |
| B2 | routing waits for the person by default | Opt-in: default `intake` | m |
| B3 | a harness with no mapped mode refuses the start | Gone: it starts in its own mode, said | c |
| B4 | an unanswered request refused after 15 minutes | Gone: it waits | c |
| B5 | the harness's *always* refused | Gone: relayed | c |
| B6 | a second identical ask refused `held` | Gone: it joins the held ask | c |
| C1, N7 | `gh` and `az` signed out in every session; words that call them the person's | Opt-in `credential-isolation`; the words made true | j |
| C2 | other providers' variables stripped | Gone | r |
| D1, D4, P6, U3 | every session push refused by git access's default; an agent changes no git access | Write `allow` by default; `deny` and `ask` the person's (`no-push`); an agent changes git access, never lifting the person's own value | j |
| D2 | force, delete, tag, mirror and prune refused | Dangerous `allow` by default; Opt-in `no-force` | j |
| D3 | a git `ask` held 15 minutes then refused; `not-running` | an approval request with no bound; with no Daoris running, a kept grant, else the person's rule said | c, j |
| D5 | Daoris's push act refuses `-u` and force | `-u` taken; force as Dangerous says, under the floor | j |
| E3 | Ask Daoris's reads refused `not-readable`, `not-granted`, `not-handed`, git-ignored | the first two the person's, kept; the rest Gone | n |
| F1, F2, F5, F9, F10, U1, V1, V3 | confirm cards on agents' presses, act families, the card queue's bounds, `shape`, the service's held asks, the session-terminal refusal | Gone: a press runs as its caller's | d |
| F3 | terminal tiers | Gone | d |
| F4 | 46 window-only acts | Gone: open over the pipe; pickers and window mechanics stay window-side | f |
| F6 | the 17th press under way refused `busy` | Paces | f |
| F7 | a press refused or held while an update drains | Paces: kept and run after the restart | f |
| F8 | an agent's channel join refused `inside` | Gone | f |
| F11 | the service's 47 person-only doors | Gone; the review verdict and go-ahead answer are Workflow | d |
| G1 | restricted Ask Daoris's allow-list, everything else asked | Harness; the stale "refused by construction" fixed | n |
| G2, U8 | a plugin's server reaches Ask Daoris only after approval; execution-named tools never | Gone: declared servers handed, the harness asks per call | n |
| G3 | Ask Daoris never gets the person's own servers, plugins or browser switch | Gone | n |
| G4 | Ask Daoris never sets its own mode | Gone: its harness asks | n |
| G5, S12, I3 (`not-set-up`) | Ask Daoris and one-job sessions may not ask a helper; an unset agent refused | Gone; I3's `off` and `ask` stay the person's | h |
| H1 | role writes refused from an agent's job | Gone; a change of who answers a wait goes to that wait's party (§3.6) | g |
| H2, H3, H4, H8, V4 | the role terminal, `in-agent-job`, grants, `owner-set` | Gone | g |
| H5, H6 | the supervisor's reserved list and its refusals | the go-ahead and the Accept are Workflow, answered by the step's party; the rest Gone | g |
| H7 | an allow supervisor cannot lift the person's deny | kept: no approver lifts the person's rule; the default and publish halves Gone | c |
| H9 | the steward desk's caps refuse | Paces | g |
| I1 | seeded supervisor rules | Opt-in: none seeded | m |
| I2, I4, I5, I6 | a third helper or piece refused; a capture refused whole; a message never wakes a run | Paces or Gone: a slot waited for, a partial capture said, a message may wake a parked part | o |
| S1 | a subagent's calls read-only | Gone; lineage kept as a record | h |
| S2, S3, S4, S9, S10, S15, S16, S18, S19, N14 | the call table's and the boxes' refusals by work and shape | Gone: every tool to every caller; the table orders the listing | h |
| S5, S6 | a quest to another workspace, or to its own repository with no lane, refused | Gone | i |
| S7 | a quest's shape bounds refuse | Paces: raised, or cut and said | i |
| S8 | an agent's change of a quest's review or workflow choice needs the person's words | Workflow: a wait guards itself | g |
| S11 | channel caps refuse | Paces; the urgent bound already paces | o |
| S13 | memory refused by work; own repository; `memory_end`'s approver | by-work refusals Gone (o); the rest Opt-in (m) | o, m |
| S14 | `go_ahead_ask` refused `NoAsk`, `Production`, past 50 | the go-ahead Workflow; the refusals Gone | o |
| S17 | `review_serve`/`review_ready` for a set-up step only; the verdict | serve and ready Gone (h); the verdict Workflow | h |
| S20 | a widening waits for the person; a push proposal refused | an approval request to the place's approver; the person's deny stays theirs | c |
| K1 | non-doctrine verbs refused without a named driver | Gone: the PATH's driver, said | r |
| K2 | a session's stage or publish refused | Gone | e |
| K3, N15, W1, W2, W3, W4, P1 (memory restart) | Daoris's own deletes, nested repositories, tracked files, links, its server's upkeep | Floor | — (W4 checked in p) |
| U2 | the pauses page's defaults and the 15-minute words | follow B2, B4 and row m | c, m |
| U4 | hand-over `ask` by default (and the helpers rule's) | Opt-in: default `on` | m |
| U5, P5 | commits `ask`; a plugin's tools per place | the person's switches, kept | — |
| U6, W5 | checkout writes off by default | Opt-in: on by default | p |
| P1 (filter), P2 | the browser's tools filtered and its code-running tools withheld | Declared by the plugin; the person may hold them back | p |
| P1 (120 s), P3, P4, P7 | a tool call ended at 120 s; a tool's question refused; a tool run's caps; sign-in and sign-out no tools | Paces or Gone: a said timeout, the question relayed as the agent's, caps raised or cut and said, both offered as tools | r |
| N1 | Ask Daoris told it changes nothing, has no shell, never presses full access | Gone | d, n |
| N2, N3 | *yours alone*, *Done at a terminal*, the never-press verbs | Gone; a run's steps and a departure's yes stay Workflow | n |
| N4, X5 | the restricted-mode ban on pressing a step on the person's words | Gone: through words in both modes | n |
| N5 | the boundary words | the write part with k; push, publish and release Gone | k, q |
| N6, N8, N9, N10, N12 | words forbidding branches, pushes, merges, requests and the widening wait | Gone; Accept, go-ahead and kind stay Workflow; `persons-processes` and `no-disclosure` Opt-in | q |
| N13 | the co-working skill's *never now*, *no agent stops another*, *what is not available*, a wrong bound | Gone (canon, then sync); an agent's `now` reaches as urgent, a pace | o |
| N16 | a folder place told to run no git | Opt-in `folder-no-git` | b |
| V2 | some 15 verbs refused to a session outright | Gone; git access and identity with j | e, j |
| X1, X2, X3, X4, X6 | review, go-ahead, human and look, unchecked done, a change lowering a party's part | Workflow, each answered by its named party | g |

## 9. Migration of installed homes

Each row that changes a default carries one step of the home's migration, run once at the home's open, idempotent, keeping what it
removes in the salvage folder, as `GitPushStep` and `ConfiguredAccessStep` do. The rule throughout: **a value the person set stays as
set; a value that was Daoris's default takes the new default.** No mode changes: a repository's `auto`, `asking` or `unrestricted` and
Ask Daoris's mode stay as they were.

| Row | What the step changes | What it keeps |
|---|---|---|
| a | the default rules (`no-push`, `no-publish`, `tree-guard`, the classifier's sentences) off where the person never switched them | a default the person switched, either way |
| c | a held git ask in `run/git/asks/` read as an approval request; no bound applied | kept grants |
| d | `driver.json`'s `terminal`, `data/terminal-acts.json` and configured access's `acts` families removed | the turned-off checkouts |
| j | git access Write `as-today` and Dangerous `deny`, where they were defaults, become `allow` | every scope's value the person set; `GitPushStep`'s machine `allow` |
| k | nothing stored: write-across declarations stay as the targets they named | the declarations; a repository's *read off* |
| m | seeded supervisor rules unchanged from their seed removed; routing takes `intake`, the hand-over and helpers rules `on`, where unset | a rule the person changed or added; a routing, hand-over or helpers value the person set |
| n | Ask Daoris's plugin approvals (`HelpGrants`) removed, the servers handed as declared | — |
| p | `checkoutWrites` unset becomes on | a place the person set off |

After the first open that ran a step, Notifications says once, never as a wait, which limits turned off and where each is switched back
on (Settings → Rules). A seed that carries no mark of being a seed is compared with the seed's own values. The launcher already refuses
an older build (D232), so no older build reads a migrated home.

## 10. Rows, in order

Every row proves itself filtered to its own tests, each seen failing first, with `npm run quick`, and where it changes words,
`i18n:check` and `names:check -- --strict` in English and Chinese. Each rewrites, in the same chunk, the area sentences its change
makes untrue and the words that told an agent of the limit it lifts (D228: the older way goes in the change that replaces it). The
order: FULLACCESS1 first; full access made full beside it; the approval request before the cards go, so restricted mode's asking is
the harness's before Daoris's is removed; the doors, verbs, pipe and roles; the connector; git and reach; the postures and waits; Ask
Daoris; co-working; the words last, once what they describe has landed.

| Row (lanes; after) | What the person sees working | Proof | What it removes |
|---|---|---|---|
| **a = FULLACCESS1** (driver, modules, web; first; on its branch) | The default rules off on an installed home by its migration; each refusal it touches is the harness's prompt; Ask Daoris in full access presses every Daoris act; a branch withdrawn at any time; a terminal verb acts while the desktop runs; a session runs a plugin's command line; an approval that could change nothing refused where it is asked; Ask Daoris finishes a simple act or sends the work on | Its own tests and installed look, as its branch says | FA1 (for Ask Daoris), FA2, FA3, FA4 |
| **b** full access is full (driver; a) | An unrestricted session and Ask Daoris's full start carry no Daoris deny, hard denial or guard; the protective denies are rules the person switches on | `SessionRules` and `DrivenAccess` tests: an unrestricted start's settings hold only the person's rules and switched-on opt-ins; each opt-in on writes its deny in every mode; a one-job start under the person's `tree-guard` off carries none | A1, A4, A5, A13, N16, N9's processes part: the composition beside the mode, `PlaceDenies`' and `GitSessionHolds`' read deny as defaults, the forced guard |
| **c** one approval request (driver, modules, git door, web, terminal; b) | Every prompt of every harness, the git door's asks, a widening and a rule's `ask` appear as one request in What needs you, Notifications and the session, answered once, always or deny, by the person or the place's appointed approver, waiting with no clock | `ApprovalRequestTests`, one per source, building one record; `ApproversTests` (repository, workspace, machine, person; an agent's own request never to itself); a request unanswered for an hour on the stand-in door still waits; *always* writes an allow beneath the person's rules and answers the next identical request; a duplicate joins; a one-job session on the native door asks; an approver agent's deny tells the asker its reason; Codex's and an ACP stub's `allow_always` relayed | B3-B6, A14, D3's bound, H7's default and publish halves, S20's wait: `HeldPermissions.Bound`, `PermissionChoice.Choosable`'s filter, `NativePermits`' `held`, `PERMISSION_WAITS`, `SESSION_PERMISSION_ANSWER`, `GIT_ANSWER` and `git answer` (into `APPROVALS`, `APPROVAL_ANSWER`, `daoris approval`), `PushProposals` |
| **d** no card, no person door (modules, service, driver, web; a, c) | Every press runs at once as its caller's, attributed and said; no Confirm card for any press; a terminal's press is the person's or the agent's whose key it proves; the service's person doors open | Pipe tests: a session's proved press under `auto` runs with no card, a keyless terminal press runs; `PipeCensus` with no refusing person class; service door tests: a keyless call to a former person door runs, a keyless verdict on a review waiting on the person is put to the person as a request | F1-F3, F5, F9-F11, V1, V3, U1, N1's card words, FA1 for every caller: `PipeDelegate`'s card branch, `PipeConfirmations` for presses, `TerminalTiers`, `PipeTerminalTier`, `SET_TERMINAL`, `terminal-acts.json`, `ActFamilies`, `PersonConfirmations`, `DoorClass.Person` as a refusal, `SessionsTerminal` |
| **e** Daoris's verbs from a session (driver, tools; d) | A session runs `agent trust/login/install/key`, `update`, `tool`, `files`, `settings brainstorms`, `quest retry`, the role verbs, and stages an update | A session-terminal test per verb: it acts, or goes through the running Daoris, logged as the session's | V2 (but git access and identity), V4, H1's refusal, G4's verb, K2: `TerminalRole`, `desktop-publish/session.mjs`' refusal |
| **f** the pipe paces (modules; d) | The window's acts over the pipe; the 17th press waits and runs; a press during an update's drain kept and run after the restart; an agent joins a channel | Admission tests for each; `PipeClass.Window` holding window mechanics alone | F4, F6, F7, F8: the acts' 46 window marks, `busy`, `stopping`, `PipeOutsideOnly` |
| **g** roles as any caller; a wait guards itself (modules, driver; d) | The steward and a supervisor act as any caller; a go-ahead answered by the role the step names; a workflow change lowering a party's part, an agent's change of a quest's review choice and a new approver's appointment each wait on that wait's party | Role tests: a steward's act outside the former list runs; a press from inside an agent's job attributed to that agent; each of the three changes raises an approval request to the current party; a supervisor's answer of a go-ahead step that names it | H1-H6, H8, H9, S8, X6's card: `RoleJudge`'s grants, `StewardAllowedAct`, `in-agent-job`, the `owner-set` lock-out, `StewardDesk`'s refusing caps, the reserved list but the step's party |
| **h** the connector serves every caller (service, driver; d) | Every tool listed to every caller; a subagent's call is its session's; `knowledge_refresh` open; a box with nothing to act on says so | `CallLineageFilterTests`: a child's full `quest_publish` passes; `McpRoleFilterTests` serving every tool to each work, `call-table.json` the listing with a `step` column; each box's former by-work refusal a passing case | S1-S4, S9, S10, S12, S15-S19, G5, I3's `not-set-up`, A17's sentence, N14 |
| **i** quests anywhere (service; h) | A quest to another workspace, or to its own repository, publishes; a carry past a bound is cut and said | Publishing tests for each | S5, S6, S7 |
| **j** sessions push, force and use the person's CLIs (driver, web; b, c) | A session pushes, force-pushes, deletes a branch and tags under the default; `gh` and `az` work as the person; `no-push`, `no-force` and `credential-isolation` hold where switched on | Door tests on both doors: each kind passes under the default and is refused under the person's `deny`; `GH_CONFIG_DIR` and the token variables untouched by default, emptied under the rule; an agent's `git access` change below the person's value applies, one above it raises a request to the person | C1, D1-D5, P6, U3, N7, V2's git access and identity: `as-today`, Dangerous `deny` as default, `GitSessionHolds`' sign-out as default, `GitAccessCommand`'s refusal of an agent, the push act's `-u` refusal |
| **k** across repositories by default (driver, web; b) | A session edits another checkout and reads another workspace's; `keep-in-repository` and `keep-in-workspace` hold where on | Start tests with and without each rule; a read across workspaces passes | A2, A3, E1, U7, N5's write part |
| **l** postures declared (driver; c) | A one-job session runs the working session's posture and the person's allows unless its step, rule or pass declares `read-only` or `packet-only`; the shipped workflows declare `read-only` where a step only reads | Each one-job start's settings by declared posture; a review step declaring `read-only` holds; a helper with none declared runs a test | A7-A12, A15's one-job part, A16, N11: the fixed one-job permission sets |
| **m** no seeded waits (driver, web; c) | A new home seeds no supervisor rule; routing goes to the intake, and a hand-over and a helper go on, unless the person set otherwise; a changed checks declaration lands accepted unless `checks-accept` is on; memory's rules opt-in | The migration's step (§9); a census that every wait Daoris makes is a workflow step, a harness's prompt or a switched-on rule's ask | I1, B2, U2, U4, S13's rules, D283's restricted wait |
| **n** Ask Daoris whole (driver, web; d, h; CONVO1's row b) | Ask Daoris is handed the declared servers and the person's own servers, plugins and browser; reads what is handed now; its room says the working rule, through words in both modes, and none of *yours alone*, *Done at a terminal* or the four fences | Room census and goldens in both languages; `HelpPlacement` tests for both modes; Ask Daoris's mode change asked by its harness | G1's comment, G2-G4, E3's last two, U8, N1-N4, X5, D280's fences two to five |
| **o** co-working paces (driver, service, canon; h) | A third helper or piece waits for a slot; a capture past its bound is handed in part and said; a message may wake a parked part; channel caps queue; go-aheads and memory serve every caller; the co-working skill says no limit that is not there | Each pace a test; the canon edited, then synced to this repository and both examples | I2, I4-I6, S11, S13's refusals, S14's refusals, N13 |
| **p** a checkout's files and the browser's tools (driver, web, examples; b) | Daoris's stage, commit and switch in a registered checkout without a switch; the browser plugin declares its code-running tools; the floor binds only the act engine | Act tests with the switch unset and set off; the plugin's listing; an act-engine test that an agent's own `git reset --hard` in its tree is never refused by Daoris | W5, U6, P1's filter, P2 |
| **q** what an agent is told (driver, canon; j, k) | No brief tells an agent an act is not its own unless a rule or a step holds it | Brief goldens in both languages; a census of the briefs against §5's rules | N5's push part, N6, N8, N9, N10, N12's rest |
| **r** the small ones (driver, examples, CLI; c) | Provider variables kept; a browser call's timeout said; a plugin tool's question relayed; its caps raised or cut and said; sign-in and sign-out as tools; the PATH's driver used and named | A test each | C2, P1's 120 s, P3, P4, P7, K1 |
| **z** the installed proof (the steward; all) | On the installed window: a full-access session pushes, force-pushes a branch, deletes it and opens a pull request with `gh`; a restricted session's command asked in What needs you and in its conversation, answered *always*, the next one unasked; the same answered by an appointed approver agent; a review waiting on its named party; no card anywhere; an older home's migration notice | Dated shots in both languages, with the machine log's lines and the records of each request and answer | Nothing |

## 11. What is removed (D228: one way)

Each goes in the row that replaces it, with its words, routes, settings, tests and area sentences.

- Daoris's confirm cards for any press, agent or terminal (`PipeDelegate`'s card branch, `PipeConfirmations` for presses, the service's
  `PersonConfirmations`), with the card queue's bounds and its `shape` refusal (d).
- The terminal tiers (`TerminalTiers`, `PipeTerminalTier`, `SET_TERMINAL`, `daoris settings terminal`, `data/terminal-acts.json`) (d).
- Configured access's act families (`ActFamilies`, the grants page's *Acts without asking*) and Ask Daoris's plugin approvals
  (`HelpGrants`) (d, n).
- `PipeClass.Person` and `DoorClass.Person` as refusals; the `SessionsTerminal` refusal and the verbs' outright refusals of a session
  (d, e).
- The call table's refusals by kind of work and the lineage filter's refusal of a subagent; the table stays as the listing (h).
- Git access's session-push refusal (`as-today`), Dangerous `deny` as a default, `PushProposals`' refusal, and the refusal of an
  agent's git access change (j).
- `gh` and `az` signed out by default (`GitSessionHolds`) (j).
- The 15-minute refusals (`HeldPermissions.Bound`, the git door's wait) and the *Allow once / Refuse* filter on a harness's options (c).
- Two answers for one kind of wait: `SESSION_PERMISSION_ANSWER`, `PERMISSION_WAITS`, `GIT_ANSWER` and `git answer` become
  `APPROVAL_ANSWER`, `APPROVALS` and `daoris approval` (c).
- The one-job sessions' missing ask hook, their fixed permission sets and connector-only plans as defaults (c, l).
- A harness's refusal to start with no mapped mode (`DrivenAccess`, `HelpMode`) (c).
- The role terminal's refusals, `in-agent-job`, role grants, the `owner-set` lock-out and the steward desk's refusing caps (g).
- The seeded supervisor rules, routing's `person` default and the hand-over's `ask` default (m).
- The window-only marks on acts, the pipe's `busy` and `stopping` refusals and `PipeOutsideOnly` (f).
- The room's *yours alone* rows, *Done at a terminal*, the never-press verbs, the restricted ban on through words and D280's fences two
  to five (n).
- CONVO1's row b, which this design's row n replaces.

## 12. Rejected

- **Today's blocking model**: doors kept for the person alone, Daoris's fixed and default denies, git access apart from the mode,
  Daoris's confirm cards on top of the harness's prompts, terminal tiers, and a clock that refuses. It made Ask Daoris less able than
  the harness the person runs by hand, and every gap became a row of the backlog.
- **A Daoris approval layer beside the harness's**: two ways to ask for one act (D228), and the person would answer twice.
- **Daoris answering prompts by policy in full access** where a harness has no mode that asks nothing: the judgement is the
  harness's or the approver's (D81, D52).
- **Keeping the defaults on, with a switch that lifts them all**: the person would have to find each limit to lift it; a limit is the
  person's to add.
- **An approver per harness, or per source of asking**: three answers and three lists for one question, who says yes here.
- **Lifting workflow steps in full access**: the mode is the agent's reach; the steps are the work's process, put there by an author.
- **Refusing an answer from a party that is not the approver**: it becomes that caller's request to the approver, so no one is refused
  and no wait is answered by the wrong party.
- **The floor as a limit on agents** (denying `git reset --hard` or a force to an agent): it would narrow the harness.

## 13. Open

- **The audit is the steward's untracked notes.** §8 says each row in a phrase so it stands alone; filing the audit itself as a record
  is the owner's call.
- **`no-disclosure` off by default.** Whether what leaves this machine (`remote.join`) follows every other former default, as written
  here, or stays the one setting an agent never changes, is the owner's to confirm before row q.
- **A harness with no mode that asks nothing, in full access.** Its asks go to the approver (§2). The owner may prefer Daoris to allow
  them in full access; that would be Daoris answering prompts by policy, rejected above.
- **The shipped workflows' read-only review steps** (row l) keep today's behaviour by the author's declaration; the owner may want them
  to default to the working session's posture instead.
- **FULLACCESS1 was not read on its branch.** Row a is named from its brief; where its build differs, its branch is the truth and this
  design follows it.

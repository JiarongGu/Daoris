# A second agent reads the work before it lands

> XAGENT1, decided as D155 (2026-10-08). Nothing is built. `Dnn` is `docs/decisions/Dnn.md`. The owner, 2026-10-08:
> *"you can always use Codex to assist the development"*, and *"this is also the logic that applies to Daoris, since we
> have a multi-agent system in Daoris"*. A read-only second opinion from Codex was taken on this question before it was
> written; §11 weighs it point by point, as `2026-10-07-second-opinion-review.md` weighed the last one.

## 0. The case

**How this repository is developed.** Claude Code does the work. Codex, another maker's agent, reads merges and designs
as a read-only second opinion: `codex exec` with a read-only sandbox and no approvals, the repository as its root, given
the project's own rules to judge by (`2026-10-07-second-opinion-review.md`, *How it was asked*). Its findings are
suggestions. Each is checked against the source before it becomes a row, and the record says which were checked. Its
first run read nothing: it read `file-tool-discipline`, found its harness had no dedicated read tools, and stopped. The
canon now says what a harness without them does (CANONREAD1). The agent that owns the work verifies and routes; the
second agent never changes anything.

**What Daoris does today**, read at `25d7f716`:

- **One harness does a machine's driven work.** `Driver.cs:928` passes `config.Adapter` into
  `HarnessRoster.SelectAsync` (`Harnesses.cs:2420`), which walks the accounts of that one adapter's owner
  (`HarnessToolchain.Owner`, AGT7) in the workspace's scope (D130), past cool-offs (D125), sign-ins (ROSTER1) and pins
  (D57).
- **A second harness is named apart where a second job exists.** `intakeAdapter` answers asks and `helperAdapter` runs
  Ask Daoris (`DriverConfig.cs:282`, `:293`). Each is named apart from the adapter (the intake's: *named, never "the
  same as the adapter"*), and absent means off.
- **The adapters fall into families.** A door onto another agent runs as that agent's accounts (AGT7), and each
  toolchain may declare its maker:

| Adapter | Runs as | Maker declared |
|---|---|---|
| `claude-code` | `claude-code` | Anthropic |
| `claude-code-acp` | `claude-code` | Anthropic |
| `codex-acp` | `codex` | OpenAI |
| `dsh` | `dsh` | DeepSeek |
| `stub`, `acp-stub` | `stub` | none |
| a plugin's harness (D64) | as it declares | none: a plugin's manifest has no maker field (`Plugins.cs`, `DeclaredAcpAdapter`) |

- **Nothing sets one session to check or help another.** Every check before a landing is the working session's own,
  or, under D154, the person's look. D154's own case was a chain whose every check was an agent's, and the one that
  touched a page proved its own stand-in.

This design adds, each off until the person sets it: a setting (§2), a reviewer chosen from another maker (§3), a pass
that reads the exact work and cannot write back (§4–§5), findings that reach the working session and the person (§6),
and a gate that comes before D154's look (§7–§8). With nothing set anywhere, every screen and every landing is what it
is today (§2.6).

## 1. Words

- **A second opinion** is one agent's reading of another session's work: what it read, its findings, and its limits.
  The agent that gives it is the **reviewer**. The session whose work it reads is the **working session**.
- **The candidate** is exactly what was read: a base commit and a tip commit in one repository. An opinion is bound to
  it.
- **A finding** is one claim, with where it is and why it matters. **An answer** is the working session's disposition of
  one finding. An opinion is **settled** when the landing may go on as far as it is concerned (§8.2).

**Not *review*.** The word already names two things: the session's review, its diff pane (D51, D113), and D154's
*Review in `<environment>`* with the person's *reviewed*. A third meaning would let an agent's report read as
*reviewed*, which must stay the person's alone (D154 point 8). *Second opinion* is the owner's own term for this
practice. The glossary and both languages are XAGENT1g's (D116).

## 2. When it is asked, and where that is set

### 2.1 Four occasions

| Occasion | What it reads | When | Default once the setting is on |
|---|---|---|---|
| `landing` | A chain's work in one repository | Once that chain's last step there is done and not held (D133, D144), before the work is shown to the person (D154) or offered to land | On |
| `steps` | One chain step's work | Once that step is done, before the chain's next step starts. This is how a design is read before it is built | Off |
| failure | The work and endings of a quest its strikes hold, or one whose evidence a declared gate's verdict refused (D144) | Offered on the hold as *Ask another agent for help*; never started by itself | Offered wherever the level names a reviewer |
| asked | A session's tree tip, any time | At the person's press | Always, where a reviewer is named |

A design that is the work of a single quest is read by `landing`, since its document lands as code does. `steps` is
for a chain whose design step is followed by its build.

### 2.2 Where it lives: `driver.json`, a setting of its own

In two keys, `opinions` and `workspaceOpinions`, beside `reviews` (D154) and `landings` (D87). The reasons are D154
§1.2's: the driver resolves the landing and the chain, the person's process on this machine is not the repository's to
declare in a tracked manifest, and the registry is wiring. **Not a field of the review rule.** A repository may want a
second opinion with no running form to show, as a documents repository does, or a look with no second opinion. Not
a field of the landing rule either: a repository's landing rule replaces its workspace's whole (D145 §1).

### 2.3 The rule

```json
"workspaceOpinions": {
  "work": { "on": ["landing"], "reviewers": ["codex-acp", "dsh"], "required": true }
},
"opinions": {
  "notes-site": false,
  "web-app": { "on": ["landing", "steps"], "reviewers": ["codex-acp"], "verify": true, "minutes": 30 }
}
```

- **`reviewers`**: one or more adapter names, in the person's order. Each is an adapter this build carries or a plugin
  declares. **Named, never guessed** (D23, and the intake's precedent): Daoris never picks a reviewer from whatever is
  installed. An entry of the working session's own family is allowed only by name, and is used only as §3.3 says.
- **`on`**: `landing`, `steps`, or both. The failure offer and the person's ask need no switch: an offer starts nothing.
- **`required`**: only JSON `true` is on, written only when on, as `autoAccept` and D154's `required` are. It says what
  happens when no opinion can be had (§8.4). It never makes an opinion land anything.
- **`verify`**: only JSON `true` is on. The reviewer may build and run what the repository declares safe to run unasked
  (D122), in its own tree (§5.3). Off, it reads.
- **`minutes`**: the wall-clock bound of one pass, a whole number from 5 to 120, default 20. The default is a starting
  point, not a measurement; §8.6's log is what tunes it.
- **`recheck`**: `false` turns off the one focused recheck (§6.5). Absent is on.
- **`false` on a repository**: no second opinion here, whatever the workspace says.
- **A repository's rule replaces its workspace's whole**, as D145's and D154's do. `--clear` hands it back.

### 2.4 Three levels, and who sets the task level

For a chain's work in one repository, the first level that says anything decides:

| # | Level | Says | Set by |
|---|---|---|---|
| 1 | The chain, at the gate | *Go on anyway*, with the person's words | The person only (§8.5) |
| 2 | The chain | `off`, `on`, or one of the rule's reviewers by name; may add `steps` or `required` | The person, or the intake quoting the person |
| 3 | The ask | The same, for every chain it publishes that says none | The same |
| 4 | The repository | Its rule, or `false` | The person, at both doors |
| 5 | The workspace | Its rule | The person, at both doors |
| 6 | Nothing | No second opinion: today | |

**A task chooses among what is declared, never beyond it.** It may turn the opinion off for its work, turn it on where a
rule names reviewers, choose which of those reviewers, add `steps`, or require it. It never names a reviewer the rule
does not list, and never sets `verify` where the rule does not, because which agents may read a repository, and whether
they may run its commands, are the repository's and the workspace's standing choices. On a repository with no rule,
`on` is refused at the door that sets it, naming the Setup row that would declare one.

**The intake sets the task level only on the person's words**, a quote the exchange checks as it checks a requirement's
(DRIFT1c), in either direction: *"get Codex to look at it"* gives `on`, *"no need for a second look, it's a typo"* gives
`off`. Otherwise it **proposes**, with its reason in at most 300 characters, and only the person's press applies it.
This is D154 §1.5's rule, and D74's reason: an agent lowering a check on its own reading widens its own autonomy, and
raising one spends an account nobody asked to spend.

### 2.5 The doors

- **Terminal:** `daoris driver opinion <repository>|--workspace <name> --reviewers <a,b> [--on landing,steps]
  [--required|--not-required] [--verify|--no-verify] [--minutes <n>] [--no-recheck]`; `daoris driver opinion
  <repository> none` writes `false`; `--clear` hands a repository back; `daoris driver list` shows the rules. The task
  level: `daoris-driver ask … --opinion off|on|<reviewer>` and `daoris-driver quest opinion <quest> off|on|<reviewer>`.
- **Screen:** *Second opinion before landing* on a workspace's Setup → *Defaults* and a repository's Setup, beside D154's
  *Review before landing*, with *None here* and *Use the workspace's*. The composer, the ask's page and a quest's page
  carry the task level, as D154's choice does.
- **Ask Daoris:** its `setting` kind takes the `opinion` door, applied by the person (D110), and the room names the verb.
- **What each door says as it is set**, the twin sentences of `DriverConfig.cs` and `driverconfig.ts` (`twins.md` gains
  the row): *before work here lands, `<reviewer>` reads it, in a copy of its own that nothing is taken back from; its
  findings go to the session that did the work, and to you.* With `verify`: *it may build and run what this repository
  declares safe, in that copy.* With `required`: *if no reviewer can read it, the work waits for you.* With an entry of
  the working agent's own family: *a fresh conversation of the same agent is not an independent reading, and each
  opinion says so.*

### 2.6 Nothing set anywhere

No key, no choice on the ask or the chain: no reviewer is chosen, no session starts, nothing holds, and the review, the
quest's page and What needs you show nothing new. The Setup row reads *None: no other agent reads work here*. A person's
*Ask for a second opinion* is not offered, because no reviewer is named.

## 3. Who is asked: another maker's agent, then an account

### 3.1 A family is an owner and a maker

Two adapters are **one family** when they run as the same agent's accounts (`Owner`, AGT7) or declare the same maker.
So `claude-code` and `claude-code-acp` are one family, Claude Code by either door. Another account of the same agent is
the same family: an account is a credential, not a second reader.

The working session's family is every family that wrote commits in the candidate, read from the session records whose
work is on the candidate's branch. A chain worked by two agents is reviewed independently only by a third.

**A maker not declared is not known.** A plugin's harness declares none today. D64's manifest gains two optional
fields, `product` and `maker`, as the built-in toolchains have, and a maker a plugin declares is that plugin's word,
shown as such. An adapter with no maker is never taken as independent. It runs only where the person named it, and its
opinion says *maker not declared*.

### 3.2 The walk

1. **The rule's `reviewers`, in order.** Each entry outside the working session's families is tried first, in the
   list's order.
2. **For each, the account walk as it stands today**: `SelectAsync(reviewer, config, workspace, chosen: null,
   StartKind.Driven)`, so the reviewer's account comes from that agent's scope for the workspace (D130), past its
   cool-offs (D125), its sign-ins (ROSTER1) and its pin (D57), and never from the account kept for conversations (D130
   §4.6). A refusal moves to the next entry. Nothing is counted for an entry that was refused, as `ChooseAsync` already
   counts only a start it allowed.
3. **The first allowed entry is the reviewer.** Its start takes a slot under the machine's cap (D130 point 10), as an
   intake's does. It is planned before any quest's first start, since it finishes work that is already done.

The new step is a walk over agents, `ReviewerChoice`, placed before the account walk and calling it. The account walk is
unchanged.

### 3.3 When no other maker's agent can read it

**It is said, never downgraded silently.** The opinion is *unavailable*, with a code: `no-reviewer` (no listed agent is
installed), `cooling` (each independent entry's accounts cool, with the first reset), `signed-out`, `not-independent`
(only the working session's family is listed), or `refused` (each entry was refused for another reason, each named).

- **An entry of the working session's own family** is taken only when the person listed it and no independent entry
  can run, and the opinion is labelled *the same agent, in a fresh conversation: not an independent reading*. Listing
  it is the person's say-so, given once, and the door said what it means (§2.5).
- **Otherwise the gate offers it** as a press, *Ask the same agent, fresh*, beside *I looked myself…* and *Go on
  anyway…* (§8.5). It is never the default.

### 3.4 What an opinion says about its independence

Every opinion carries its reviewer's product, maker and label: *another maker's agent*, *the same agent, fresh*, or
*maker not declared*. It also names the families that wrote the candidate, and what both read alike: the same
requirements, and the same repository rules.

**Independence is by maker, and Daoris names no model** (D24). An agent that fronts several providers is one agent to
Daoris (D57 §5). Two makers' agents configured onto one provider are not independent in fact, and Daoris cannot see it.
The label says *maker*, never *model*, and the opinion's page says what it cannot know.

## 4. What it reads

**The packet** is assembled by Daoris with no model, from facts:

- **The candidate**: base and tip commits, the commits between, and the paths they change. The diff itself the reviewer
  reads from git in its own tree, which holds both commits (§5.1).
- **What was asked**: the quests of the chain in that repository, their requirements with the person's quoted words
  (D133), and the ask.
- **What the working session claims**: each quest's closing note, given as that session's claim.
- **What Daoris read**: the evidence it checked (D144), a declared gate's verdict from the queue where one exists
  (EVID1d), and whether the work is held.
- **The repository's own rules and records**, as they stand in the candidate. The reviewer reads them in its own
  harness's terms, the lesson CANONREAD1 wrote into the canon.

**Not the working session's conversation.** A reviewer anchored on the worker's reasoning reads the work the way the
worker did. It starts **a fresh conversation every pass**, and never resumes one.

**For failure**, the packet adds the failed sessions' endings (their notes and codes) and, where the last one left work
uncommitted, that work as a patch. Daoris reads the patch from the failing tree, and the reviewer never opens that tree.
**For a recheck**, it adds the first pass's findings, each answer, and the commits since (§6.5).

The larger parts are files in the opinion's own folder under the home, `<home>/opinions/<id>/`, which the reviewer is
handed a read of, as a resumed session is handed its files folder (INT4j).

## 5. What it may do

### 5.1 A tree of its own, which nothing is taken back from

The reviewer works in **a clone of the repository at the candidate**, made by Daoris under the home, checked out
detached at the tip, with **no remote**, and removed when the pass ends.

- **Never the working session's tree.** That tree is the unit of exclusion (D51), and the work under review.
- **A clone, not a worktree.** A worktree shares its repository's refs, so a branch or a tag the reviewer made would
  appear in the person's repository. A clone keeps its refs to itself, borrows the repository's objects to stay cheap,
  and with no remote has nowhere configured to push.
- **Nothing in it is read back.** No landing, no branch, no commit and no file of the review tree reaches the work. The
  only thing Daoris takes from a pass is the opinion said through its tool (§6.1).

### 5.2 Read-only beyond the prompt

**The isolation is the floor, for every agent.** A harness's own mode names cannot carry it alone. `codex-acp`'s mode
called `read-only` runs, as its bundle read at 1.12.0, with a workspace-write sandbox and approvals on request
(`2026-09-22-acp3-probe-evidence.md` §2). And the repository's own doctrine tells an agent to write as it goes
(`persist-working-state`). The instruction says that a reviewer writes nothing, and the tree makes that hold whether
or not it is followed.

**The posture is the adapter's, in its own words, and only where measured** (D53):

- **Claude Code, by either door:** Daoris's rules for an opinion's session (D72): a new default, `opinion`, that denies
  the edit tools; the `commit` default withheld; `no-push` and `tree-guard` (on its tree) as for every session. That
  rules in the command-line tier are honoured is measured (PERM1); this set on a reviewer is not, until XAGENT1j.
- **dsh** documents a `read-only` sandbox its permission variable selects (DSH1, probe 6). Declared once a reviewer's
  run shows it.
- **`codex-acp`:** none relied on, for the reason above. Its session runs at the adapter's `agent` posture, as every
  session on that door does.
- **A plugin's harness:** its declared posture.

The opinion's record says which held: *read-only by its agent's rules and its copy*, or *by its copy alone*.

### 5.3 Verification, where the rule allows it

With `verify`, the reviewer may build and run what the repository declares safe to run unasked (D122), in its own tree,
and report what it saw. Its outputs stay in that tree and go with it. It never deploys, and never touches a process, a
port or a server of the person's (D154 §2.5). It is told to run nothing else. On Claude Code that is a rule: the
declared commands are allowed, any other asks, and an ask is a refusal here (D52). On a door whose posture Daoris cannot
set as finely, the instruction and the copy hold it, and the record says which held (§5.2).

### 5.4 Its connector

The reviewer's connector knows its session's kind, as a set-up step's does (D154 §2.6). It offers the knowledge tools,
`quest_list` to read, and **`opinion_give`**. It never offers `quest_respond`, `quest_publish`, `go_ahead_ask`,
`permission_propose`, D154's `review_serve` and `review_ready`, or a way to ask for another opinion.

### 5.5 What it may do, and what never

| | |
|---|---|
| **Unasked** | Read its tree and the repository's history in it; read the packet; run read-only commands; with `verify`, build and run what the repository declares safe, in its tree; say its opinion |
| **Never** | Edit the working session's tree, or any tree but its own. Commit, merge or push. Deploy. Change a permission rule, or propose one. Touch a process or a port of the person's. Take, close, decline or publish a quest. Give *reviewed* or a go-ahead. Ask for a reviewer. Talk to the working session |

### 5.6 One turn, bounded

A pass is one turn: the reviewer is handed its whole packet at once, as a driven session is, and takes no line (INT4i).
Words said to its record are refused with a new code, `opinion` (D137): *Ask again…* with words is the door (§8.5). It
is stopped at `minutes`, or at the machine's session timeout if that comes first. It ends when it has said its opinion.
**It cannot spawn a reviewer**: its connector has no such tool, and its record is never a candidate for an opinion,
since nothing lands from its tree.

## 6. What it says, and where it goes

### 6.1 `opinion_give`

The reviewer says its opinion once, through `opinion_give`, which its session alone may call. It takes:

- **`findings`**, at most 20, each with:
  - `weight`: `must` (wrong to land as it is), `should`, or `note`;
  - `where`: a repository-relative path and line, or a commit, or `general`;
  - `claim`, at most 300 characters;
  - `consequence`, at most 300;
  - `reproduce`, at most 600: the steps or the command, and what it showed, where the reviewer could;
  - `sure`: `sure`, `likely` or `unsure`;
  - `proposal`, at most 2,000: a diagnosis, or a proposed change as text or a patch, optional.
- **`read`**, at most 600: what it covered (paths, commits, which checks it ran under `verify`).
- **`limits`**, at most 600: what it did not read or could not tell.

A pass with no findings must still say `read`. Its opinion reads *`<reviewer>` raised nothing in what it read*, with
`read` beside it, and never *no issues*. A pass that ends without calling the tool is `failed`, not empty.

### 6.2 The record, kept on this machine

The opinion is kept by the local host, beside the session records (D46), and answered only to a caller on this machine,
as a record's `said` is (MSG1a). It never goes on the wire to a remote or a shared host (D47 §4). Its candidate is
commits that only this machine holds until they land, and its reviewer names an account.

It keeps: its occasion; the candidate (base, tip, commits); the pass (first, or the recheck); the reviewer's adapter,
product, maker, label and account; the families that wrote the candidate; its session; the posture that held; the
findings, `read` and `limits`; each answer and the person's adjudication; its tier; when it was asked, given and
settled. The reviewer's session record is kind `Chat` with a new field, `opinion`, and serves no quest. That is the
intake's precedent (`Sessions.cs`, `Ask`): a new kind would read as driven on an older build, the one wrong reading.
Nothing that reads a quest's sessions counts it. Its usage counts toward its account as any session's does, and reads
*not measured* where its door carried none (D57 §4: absent is never zero).

**Cleared with the work it read** (D153): clearing a closed quest's work clears its opinions and their trees' folders.

### 6.3 To the working session, as its next turn

The first pass's findings go to the working session that made the candidate's tip, **as its next turn** (D137). Its
record reopens and its harness conversation resumes on its own account, by the id Daoris kept (D137 points 3, 7 and
8). Its quest is unmoved (D137 point 5).

- **They are another agent's claims, marked as such.** They wait in the record's `said` as an entry whose new field
  `by` names the opinion. Absent, `by` is the person's, so every word already kept reads as it did. `said` is this
  machine's own, so no older build on another machine reads the new field.
- **They are never kept on the ask as the person's words.** DRIFT1a hands every later session the person's words, and
  an agent's claims carried that way would become requirements. The ask keeps that an opinion was given, as a fact.
- **The turn's words are Daoris's, fixed:** *Another agent, `<product>` by `<maker>`, read your work at `<tip>` and
  claims what follows. These are its claims, not the person's words and not facts. Check each one against the code, and
  reproduce it where it says how. If it holds, fix it here and commit. If it does not, reject it with your evidence. If
  you cannot tell, say so. Answer every finding with `opinion_answer` before you end. The other agent has ended; you
  cannot ask it anything. The person sees your answers beside its claims.*
- **A turn already running** takes them at its end, never at its next step (D136): another agent's claims do not cut
  into a step in flight.
- **The conversation shows them** as a block from that agent, under its name and maker, never as the person's.

### 6.4 The answers

`opinion_answer`, offered only to a session handed an opinion, takes for each finding:

- `fixed`, with the commit;
- `rejected`, with evidence, at most 600 characters: a check that shows it, or a reading;
- `unresolved`, with why, at most 300.

**A commit named as a fix is checked as a fact.** It must be one this turn made: in the tree's history, after the
candidate's tip, at or before the tree's tip, read from git when the turn ends. What it fixes is the session's claim.
A finding left unanswered when the turn ends is `unresolved`, said as *not answered*.

### 6.5 One recheck, then the person

Where the turn added commits and `recheck` is not off, **one** focused pass reads the commits since the candidate,
handed the first pass's findings and their answers (§4). For each first-pass finding it says `stands` or `withdrawn`,
and it may add findings of its own. It is a fresh conversation in a fresh tree at the new tip.

**The recheck's findings go to the person, never back to the working session by themselves.** That is the loop's end:
at most two passes and one turn of the working session per landing (§8.3). The person's *Send back…* (D137's box) takes
any of it on in their own words.

### 6.6 Claims and facts

- **A defect is established only by a check Daoris reads itself**: a declared gate's verdict at the candidate (EVID1d),
  shown as a fact beside the finding. Daoris never runs a command an agent names (D154 §2.3), so a reproduction a
  reviewer reports is its claim, however exact.
- **Two agents agreeing establish no fact.** A finding the working session fixes is shown as *fixed, by its account*.
  A finding the recheck withdraws after the session's evidence is shown as *withdrawn*. Neither is called proven.
- **A dispute is the person's.** A `must` finding that the working session rejected or left unresolved, and that the
  recheck did not withdraw, or a `must` the recheck raised, is **disputed**. It waits for a press of the person's
  (§8.2). A `should` or a `note` is shown and waits for nothing.

### 6.7 When the working session cannot take it

Where D137 cannot resume the working session (its account cannot run, its conversation is open elsewhere, no id was
kept), a landing's or a step's quest is done, and nothing carries a closed quest's words on by itself (D137 point 4).
So **the opinion goes to the person instead**: its findings stand unanswered, each `must` disputed, with *Start a
conversation with these findings* (D137's press). It is never lost and never parked silently.

**For failure**, the opinion's next turn is the struck quest's last session. D137's reopen forgives its failure as *Try
again* does, so *Ask another agent for help* is *Try again*, with a diagnosis in hand. The quest is still taken or open
there, so where that session cannot resume, D137 point 4's carry-on hands the diagnosis to a new session.

## 7. With D154: one gate, in a fixed order

**One landing gate, and what it waits for is a list read in order.** The gate shows the first item that is not met:

1. **The quest's own holds**: a departure (D133) and evidence (D144), each lifted by the person's yes, on the quest.
2. **The second opinion**, on the landing: no agent still at work on it, then settled as §8.2 says.
3. **The person's look** (D154): its set-up step starts only once no agent is still at work on the opinion, and the
   person says *reviewed*.
4. **The landing**: *Accept…*, or the look under *Accept automatically* (D145).

- **Why the opinion comes before the look.** The person's look is the scarce step. Findings fixed before it are fixed in
  the work the person then sees, and their *reviewed* is on a set-up whose commit holds the fixes. After it, every fix
  would make the set-up stale (D154 §3.2), and the person would look twice.
- **Why after the quest's holds.** Work the person may decline, or whose evidence is missing, is not worth an account's
  reading yet, and those holds are the person's to lift.
- **The set-up step sits** while the opinion on the work it shows is being read, answered or read again: *waits for a
  second opinion on this work*. The planner sits it, as D154 §2.1 sits one it cannot honestly start, and nothing holds
  the build's done. A dispute does not hold it: a dispute waits for the person, and the person is about to look.
- **An agent settles neither.** An opinion never gives *reviewed* and never lifts D133's holds, and no connector tool
  can (D154 point 8). Only the person answers a dispute.
- **What the person sees beside *Reviewed***: the opinion's line, its reviewer, its findings and answers, its disputes,
  and, where the set-up's commit holds commits the opinion did not read (a correction after *not yet*, D154 §3.4), *N
  commits since were not read by another agent*. The person's *Reviewed*, pressed with those beside it, is also their
  answer to them (§8.2–§8.3). That is still the person speaking: the press is the one D154 already asks for, and it
  shows what it answers.
- **Once the set-up step has started, the person's words about a dispute go by D154's *Not yet…*.** They reach the
  set-up step's session, whose tree now holds the chain's tip and takes corrections (D154 §3.4). *Send back…* to the
  build's session is not offered then, since its commits would miss the work being shown.

**Two separate gates are rejected.** Two presses that each hold the same landing would race, and each would be
answered without the other's facts in view. **A second opinion in place of the look is rejected**: D154's case is a
chain whose every check was an agent's.

## 8. The gate

### 8.1 What waits

Wherever the level (§2.4) says `landing` for a chain's work in a repository:

- **The press.** While an agent is still at work on the opinion, or a required one is unavailable, the session's
  review, its page head and What needs you show the opinion's state where *Accept…* would be, above D154's.
  `LAND_SESSION_TREE` and `daoris-driver trees land` refuse with the same sentence, exit 1. A dispute is shown inside
  *Accept…* (§8.2). At a terminal, `trees land` lists it and refuses until `opinion anyway` answers it.
- **Accept automatically** (D145). The look keeps a due session due with a new code, `opinion`, reads it at every look
  as it reads `held` and `unreviewed`, and lands it at the first look once settled. The conversation is told once, as a
  landing note (`landing.opinion`).
- **A chain is read once per repository.** With `landing` on, the look does not land a chain step while a later step of
  the chain in the same repository is still to run. It keeps the step due, *waits for the chain's last step here*, so
  the opinion reads the chain's whole work there, and one opinion is spent, not one per step. This amends D145 §3, under
  this setting only. A press on an earlier step asks its own opinion, or offers *Go on anyway…*.
- **An advance** (D145 §3, D149) is a landing like any other, and its new commits are read as §8.3 says.
- **`steps`** sits the chain's next step while the opinion on the step before is unsettled, with the same states.
- **Failure** holds nothing new: the struck quest is already held, and the press is its offer.

### 8.2 What settles it

For a landing whose tip is T:

1. **An opinion covers T**: its candidate's tip is T or a descendant of T (`merge-base --is-ancestor`, read from git,
   no model). An opinion on a candidate that holds less than T does not cover T.
2. **The working session has answered**: its turn with the findings ended, or it could not take them (§6.7).
3. **No dispute is open, or the person answered it.**

Or the opinion is **unavailable**, and the rule does not require one (§8.4). Or the person said *Go on anyway*, which is
level 1 of §2.4.

**The person answers a dispute with the press that comes next anyway**, shown with the disputes in it, as §8.3 does with
commits nobody read:

- **A press is coming.** That is *Accept…* on a manual landing, or D154's *Reviewed* where a look is required. The press
  lists the disputes, and making it answers them. *Send back…* is the other answer.
- **No press is coming.** Under *Accept automatically* with no look required, the look holds with `opinion`, *`<n>`
  findings disputed*, with *Go on anyway…*, *Send back…* and *Ask again*. An automatic look never lands over a dispute.

### 8.3 Bound to its candidate

New commits after a pass mean the opinion no longer covers the tip. **The first time, the recheck reads them** (§6.5).
After that, the cap for this landing is spent: one pass and one recheck. What follows depends on who presses next.

- **Where the person presses** to land, by *Accept…* or by D154's *Reviewed* before it, the press shows *N commits since
  were not read by another agent*, and the press is the say-so.
- **Where nothing would ask the person**, under *Accept automatically* with no look required, the look holds with
  `opinion` and the same line, with *Go on anyway…* and *Ask again*.

**The person's own asks are not capped.** *Ask again* spends an account at the person's press, and its pass is counted
and measured like any other.

### 8.4 Required, and what happens when no opinion can be had

When the walk finds no reviewer (§3.3), or the pass fails or runs out of time, the opinion is *unavailable*, with its
code.

- **`required`**: the landing holds, *no second opinion: `<why>`*, with *Try again*, *Ask the same agent, fresh* where
  only that family can run, *I looked myself…*, and *Go on anyway…*. A cool-off holds until its reset, said with its
  time.
- **Not required**: nothing holds. The line *no second opinion: `<why>`* stands beside *Accept…*, and under *Accept
  automatically* the landing record and the conversation say it.

**An opinion that was given always counts, required or not.** Its disputes wait for the person (§8.2), because a
`must` finding that the working session rejected is what nobody should land over unread, least of all an automatic
look. `required` governs only absence.

### 8.5 The states, and the doors

| State | Says | Doors |
|---|---|---|
| Not yet asked | *A second opinion is asked once this chain's last step here is done* | *Ask now* |
| Being read | *Being read by `<product>` (`<maker>`), another maker's agent*, and its time against `minutes` | the reviewer's session; *Stop* |
| With the working session | *The working session is answering `<n>` findings* | its conversation |
| Read again | *Read again by `<product>`: the commits since* | the reviewer's session |
| Disputed | *`<n>` findings disputed*, each with its answer | in the next press (*Accept…*, D154's *Reviewed*); with none coming, *Go on anyway…*; and *Send back…*, *Ask again* |
| Unavailable, required | *No second opinion: `<why>`* | *Try again*, *Ask the same agent, fresh*, *I looked myself…*, *Go on anyway…* |
| Commits since, unread | *`<n>` commits since were not read by another agent* | beside the press; under *Accept automatically*, *Go on anyway…* and *Ask again* |
| Settled | nothing: the next item of §7 | |

- ***Go on anyway…*** shows what is unsettled (the disputes, the commits unread, or no opinion), takes optional words, and
  records the person's answer on the opinion and on the landing record. It settles the opinion's part of the gate and
  lands nothing itself: the gate's next item follows, which is D154's look, *Accept…*, or the automatic look.
- ***I looked myself…*** records the person's own reading as an opinion of tier *person*, with optional findings in the
  same shape. It settles an absence, as D154's *I set it up myself…* does a set-up.
- ***Send back…*** is D137's box, in the person's own words, to the working session.

### 8.6 What is recorded

- **On the opinion** (§6.2): every pass, answer and adjudication.
- **On the landing record** (D102, D143): `opinion`, with the reviewer, its label, the candidate, the passes, the
  findings counted by weight and answer, the adjudication, and the commits unread; or `none` with its code. `trace`,
  `trees land --plan` and the review's note say it.
- **In the machine log** (D94), codes and counts only:
  - `opinion.asked` (occasion, label, adapter, pass);
  - `opinion.given` (findings by weight, seconds, tier);
  - `opinion.answered` (by answer, and not answered);
  - `opinion.held` (the gate's state);
  - `opinion.unavailable` (its code).

## 9. Where it shows, and its two doors

- **The session's review** (审阅), beside D154's states: a *Second opinion* section. It shows the reviewer and its label;
  the candidate, and whether it covers the tip; each finding with its weight, its place (which opens the file's preview
  at the line, PREVIEW1), its consequence, reproduction and sureness, its proposal, and the working session's answer
  beside it; `read` and `limits`; the tier; and the reviewer's measured use. Its presses are §8.5's.
- **What needs you**: a disputed opinion, a required one that is unavailable, and commits unread under *Accept
  automatically*, oldest first, with the same doors.
- **The working session's conversation**: the findings as a block from the other agent, then the session's answers.
- **The reviewer's own session** in Sessions, beside the session whose work it read, marked *second opinion*, with its
  console and transcript as any session's.
- **The quest's and the ask's pages**: the task level's choice, and each chain's opinion state.
- **A struck quest's hold**: *Ask another agent for help*, where the level names a reviewer.

**The terminal**: `daoris-driver opinion ask <session> [--reviewer <adapter>] [--same-agent] ["…"]`, `opinion show
<session|opinion>`, `opinion stop <opinion>`, `opinion anyway <session> ["…"]`, `opinion myself <session> ["…"]`, and
`sessions say` for *Send back…*. Each prints the gate's state after it.

**Ask Daoris**: the setting through its `setting` card. *Ask for a second opinion*, *Ask the same agent, fresh* and
*Try again* are exempt as the person's choice to spend an account, as *Go on in a new session* is (MSG1g). *Go on
anyway…* and *I looked myself…* are exempt as judgements Ask Daoris cannot have made (D110), as D154's verdict is.

## 10. What the no-model tier does

`model-decoupling`: the feature is said without a model, and the floor is the facts.

- **The packet** is commits, paths, requirements, notes, evidence and verdicts, read from git and the store.
- **Choosing the reviewer** is a table: the rule's list, owners, makers, then the account walk.
- **Coverage and staleness** are `merge-base --is-ancestor`. A fix's commit is checked against the tree's history.
- **Routing** is D137's reopen, with fixed words. **The gate** is a table of facts and the person's presses.
- **A person's own reading** (*I looked myself…*) is accepted in the same shape, labelled *person*.
- **What needs a model is said as such.** Reading code and judging it is a session's work. With no reviewer to run,
  the opinion says *no agent could read this: `<why>`*, never *no issues*. Every opinion carries its tier: *agent*,
  with its label; *person*; or *none*, with its code.

**A canon principle, the parent's call.** The practice is project-agnostic and needs no service (D48 §2a), so it may
join `canon/core/knowledge/autonomous-development.md`'s *How to apply*, after *Done means gates green plus a reviewable
record*, and after D154's *See it run before it lands* once REVIEWENV1i has put that there:

> - **A second reader, from another maker.** Where another agent can read the work before it lands, let one that did
>   not write it, and is not made by whoever made the one that did, read it without changing anything. Its findings
>   are claims. Check each against the code, then fix it or answer it with evidence, and put the claims and the answers
>   in front of the person together. Agreement between two agents proves nothing, and no agent's reading stands in for
>   the person's.

Knowledge is read on demand, so the always-loaded core does not move. The change re-syncs this repository and
`examples/`, with the canon changelog's reason (XAGENT1k). This branch does not edit `canon/`.

## 11. The second opinion on this design, weighed

Codex read the question read-only before this was written, and recommended seven things. Each is adopted, amended or
rejected here, with the reason.

| # | Recommendation | Here | Why |
|---|---|---|---|
| 1a | Off when unconfigured | Adopted (§2.6) | D154's rule; an account spent unasked is not a default anyone chose (the intake's precedent) |
| 1b | Default trigger: one review before landing, after the session's checks, against the exact candidate | Amended (§2.1, §7, §8.1) | Before the person's look where D154 applies, and once per chain per repository, at its last step there |
| 1c | Holds the landing, not the build's completion | Adopted, and extended (§7) | The set-up step's start sits too, so the person never looks before the fixes |
| 1d | A person can ask any time | Adopted (§2.1) | |
| 1e | Design review and failure assistance start off | Adopted (§2.1) | `steps` is off; failure is only ever an offer |
| 1f | Assistance offered after a repeated failure, never automatic | Adopted, amended | The offer needs no switch: it starts nothing |
| 2a | Another agent family first, then an allowed account | Amended (§3.2) | From the person's named list only, never among whatever is installed (D23; the intake's named adapter) |
| 2b | A reviewer constraint before the account walk | Adopted (§3.2) | A walk over agents that calls the unchanged account walk |
| 2c | Alternate doors onto one agent are not independent | Adopted (§3.1) | By owner (AGT7) and maker |
| 2d | Keep allowances, cool-offs, sign-ins and pins | Adopted | `SelectAsync` does all four already |
| 2e | Another account alone is not independent | Adopted (§3.1) | |
| 2f | With one harness: say so; offer a labelled fresh pass, or human review | Adopted, amended (§3.3) | Listing the same agent by name is the person's say-so, given once and labelled on every opinion |
| 2g | Never downgrade silently | Adopted | |
| 3a | Read-only by default, enforced beyond the prompt | Amended (§5.1–§5.2) | The enforcement is the isolation: one mode named `read-only` runs a workspace-write sandbox, and doctrine tells agents to write |
| 3b | The packet: snapshot, requirements, doctrine, decisions, evidence, a fresh conversation | Adopted (§4) | The working session's conversation is left out on purpose |
| 3c | Building only in a verification mode, in a disposable tree at the candidate | Adopted, amended (§5.1, §5.3) | A clone, not a worktree, whose refs would be the person's |
| 3d | The list of what it never does | Adopted (§5.5) | |
| 3e | Assistance is diagnosis and proposals; the owner implements | Adopted (§6.1, §6.3) | D45: the work stays the repository's own agent's |
| 4a | Findings both ways, once, attributed | Adopted (§6.3, §6.5) | |
| 4b | A durable record of candidate, reviewer, scope, findings, limits | Adopted, amended (§6.2) | Kept on this machine, never as quest operations that travel |
| 4c | Delivered as the next turn, marked as another agent's claims | Adopted (§6.3) | `said` gains `by`; never kept on the ask as the person's |
| 4d | Shown beside *reviewed*, with the answers | Adopted (§7, §9) | |
| 4e | A reproducible check establishes a defect; otherwise the person adjudicates | Amended (§6.6) | Only a check Daoris reads itself establishes one; a reported reproduction is a claim (D154 §2.3) |
| 4f | Two agreeing agents establish no fact | Adopted (§6.6) | |
| 4g | Bound to its candidate; new commits make it stale | Adopted (§8.2–§8.3) | |
| 4h | An agent's report never satisfies the human verdict | Adopted (§7) | |
| 5a | Three levels, a setting of its own in `driver.json` | Adopted (§2.2–§2.4) | |
| 5b | Triggers, reviewer preference, verification, time and turn limits, required or optional | Adopted, amended (§2.3, §5.6) | No turn limit: a pass is one turn by construction (INT4i). `required` governs only absence |
| 5c | A task override selects declared capabilities only | Adopted (§2.4) | |
| 5d | The intake may quote or propose, never weaken | Amended (§2.4) | D154's rule: the person's quoted words set it either way; the intake's own reading never does |
| 5e | Required and unable to run: hold with retry, manual review, skip | Adopted (§8.4) | |
| 6 | The no-model tier: packet, staleness, human findings, routing, tier on every result | Adopted (§10) | And a pass with no findings must say what it read |
| 7a | Cost: one pass and one recheck, caps, measured use | Adopted (§6.5, §8.3) | Per landing; the person's own asks are counted, not capped |
| 7b | Loops: no reviewer spawns one; disputes to the person after one cycle | Adopted (§5.6, §6.5, §8.2) | The person answers a dispute in the press that comes next anyway; only where none is coming does the gate add one |
| 7c | Rubber-stamping: evidence, scope and shared provenance disclosed; *reviewed* stays human | Adopted (§3.4, §6.1, §7) | |

## 12. Rejected, and left out

### 12.1 Rejected

- **Daoris choosing a reviewer among whatever is installed.** It would spend an account the person never named on a
  tool they may not trust with this repository (D23: an adapter arrives deliberately, never guessed).
- **The reviewer as a quest, a chain step as D154's set-up is.** A quest is work a repository is asked to do, taken by
  whichever harness that machine drives with. Its done moves a chain, and it travels. An opinion does no work in the
  repository, must be read by a particular other agent, and moves nothing.
- **The opinion as quest operations.** Operations travel and are replayed on every machine (D68). The opinion's
  candidate is commits only this machine holds, and its reviewer names an account.
- **The reviewer in the working session's tree, or in a worktree of the repository.** The first is the work itself. The
  second shares the person's refs.
- **A harness's read-only mode as the guarantee.** One such mode runs a workspace-write sandbox, and a mode is a name.
- **Handing the reviewer the working session's conversation.** It would read the work the way the worker did.
- **Taking the reviewer's edits back as a patch.** The work's author is the session that owns it (D45). A proposal
  travels as text, and the working session applies its own version under its own checks.
- **Agents talking to each other until they agree.** A loop with no person in it, which COWORK1 is held for, and two
  agreeing agents establish nothing.
- **Findings as the person's words.** DRIFT1 would hand them to every later session as requirements.
- **An opinion after the person's look**, which makes every fix cost a second look; **an opinion instead of the look**;
  and **an opinion that gives or lifts *reviewed***.
- **Holding the build's done.** As D154 found, it is true, and holding it would stop the chain before its next step.
- **An automatic loop until the findings clear**, and **failure assistance started by itself.** Each spends accounts on
  an agent's reading with nobody deciding.
- **Another account, or a fresh conversation of the same agent, counted as independent.**
- **On by default.** Every landing would wait on a reviewer nobody chose.
- **A model deciding which findings are right** (D24, D54). That is the working session's claim, a check's fact, or the
  person's judgement.

### 12.2 Left out, not rejected

- **An opinion carried into the pull request** by a landing plugin. The frame would need to grow, as D154 left *reviewed
  in dev* out.
- **Teammates seeing opinions** on another machine. The commits are this machine's until they land; the pull request is
  where a team reads the work (D145).
- **One opinion across two repositories at once**, as D154 left one review across two.
- **A recheck that resumes the first pass's conversation.** It would need its tree back and a moved candidate; a fresh
  pass handed the first pass's findings is simpler, and costs one reading of the packet.
- **An Ask Daoris proposal that asks for an opinion.** It is exempt for now (§9).
- **The person adjudicating finding by finding.** *Go on anyway…* settles them together, and *Send back…* says which.
- **Opinions on a conversation's work.** A chat lands nothing by the driver's gate today.

## 13. Worked through

1. **This repository's own merge.** A lane's session on Claude Code finishes its row. The workspace's rule: `landing`,
   reviewers `codex-acp`, required. The done leaves no later step there, so the walk passes Claude Code's family and
   takes Codex on the account the workspace's scope gives it. Codex reads a clone at the branch's tip, with the
   repository's rules, the row's requirements and the session's closing note. It raises three findings: a `must` on an
   unquoted workspace name, a `should` on a duplicated table, and a `note`. Claude Code's session reopens with them.
   It reproduces the first, fixes it and commits; rejects the second, naming the twin test that holds the table; and
   leaves the note unresolved. The recheck reads the fix commit and says the first `withdrawn`. Nothing `must` is open,
   so the opinion is settled, and *Accept…* shows the three findings and their answers beside it. This is today's
   practice, with Daoris doing the routing: Codex's findings checked against the source, then routed.
2. **A ticket with D154's local review.** The build's done is read by Codex before the set-up step starts. Its one
   `must` is fixed in the working session's next turn, and the recheck withdraws it. Then the set-up step runs and shows
   the fixed work in Daoris's browser. The person looks, and presses *Reviewed* beside *0 commits since*. Under *Accept
   automatically*, the look lands it and the plugin opens the pull request.
3. **A machine with Claude Code only.** The rule names `codex-acp`, which is not installed. Required, the landing holds
   with *no second opinion: no listed reviewer is installed* and four presses. The person installs Codex and presses
   *Try again*, or presses *Ask the same agent, fresh* and the opinion says so on its face.

## 14. Against the decisions

- **Amended.**
  - D145 §3: under `landing`, a chain step's work waits for the chain's last step in that repository, and the look keeps
    a due session due with `opinion`.
  - D87 and D145: a landing waits for a settled opinion where the level says one.
  - D154: its set-up step sits while an agent is still at work on the opinion, the gate's list puts the opinion before
    the look, and the disputes and the commits since are shown beside *Reviewed*, which answers them.
  - D137: `said` gains `by`. An opinion's words reach the working session as another agent's claims, are never kept on
    the ask as the person's, and arrive at a turn's end. The say door refuses words to a reviewer's record with `opinion`.
  - D65 §1b and the intake's room: the task level is set only on the person's quoted words.
  - D64: a plugin's harness may declare `product` and `maker`.
  - D72: a default, `opinion`, denies the edit tools to an opinion's session, and `commit` is withheld from it.
  - D110: the `setting` kind takes `opinion`, and the presses are exempt as §9 says.
  - D94, D102 and D143: the log's lines and the landing record's `opinion`.
  - D153: opinions clear with the work they read.
  - D130: a reviewer's start counts and takes a slot.
- **Standing.**
  - D23 (adapters named, never guessed) and D24 (no model named).
  - D37 (a session never pushes; the outward acts stay the person's) and D45 (the work stays the owning repository's).
  - D46 (records observed; the driver posts what it read and moves no quest) and D47 §4 (machine-local material).
  - D51 (the tree is the unit), D52 (an ask is a refusal), and D53 (the posture is the adapter's, never guessed).
  - D57 (usage measured; absent is never zero) and D74 (no widening without the person).
  - D105 §3, D126 §2.1, D133, D144, INT4i, and D154's *reviewed* as the person's alone.

**What the checks do not cover.** Documents only. `Harnesses.cs`, `Adapters.cs`, `Driver.cs`, `Driver.Intake.cs`,
`DriverConfig.cs`, `Plugins.cs` and the service's `Sessions.cs` were read at `25d7f716`. No reviewer ran, and no
harness's read-only posture was measured as a reviewer's. `codex-acp`'s modes are ACP3's reading of its bundle at
1.12.0, and dsh's sandbox is DSH1's at 0.1.6-alpha.2. Two things are for XAGENT1d to measure: whether a clone that
borrows the repository's objects keeps a reviewer's ref writes out of the repository, and what it costs on a large
one. Whether D137's resume carries a word that is not the person's through both doors unchanged was read in MSG1's
notes, not run. The `minutes` default is not measured. `verify` checks D155's place, that it names what it rejected,
and this design's links, and none of their words.

## 15. The build

Each row ships both doors for its part (D50) and names its Ask Daoris door or its reason (D110).

- [ ] **XAGENT1a — the opinion rule** (cli, driver, modules, web-settings, service). `opinions` and `workspaceOpinions`:
  both twins on one table, refusals, each door's sentence, `daoris driver opinion`, the Setup rows, Ask Daoris's
  `setting` door, the room's table; declared only. Contract: §2.2–§2.6. Proof: `OpinionRulesTests` and
  `driverconfig.test.ts` cell for cell, module tests, vitest, `HelpSettingProposalTests`.
- [ ] **XAGENT1b — the reviewer chosen** (driver; after a). `ReviewerChoice` before the account walk: the rule's order,
  families by owner and maker, the same family only by name and labelled, unavailable codes; a plugin harness's
  `product` and `maker`. Contract: §3. Proof: `ReviewerChoiceTests`' table (both Claude doors, another account, no
  maker, cooling, signed out, unpinned).
- [ ] **XAGENT1c — the record and its tools** (service; after a). The opinion kept by the local host, refused on a
  shared host; `opinion_give` to an opinion's session only, `opinion_answer` to a session handed one; the record's
  `opinion` field; `said`'s `by`; the bounds. Contract: §5.4, §6.1–§6.2, §6.4. Proof: `OpinionStoreTests`,
  `McpToolsTests`, both hosts, `SessionStoreTests`.
- [ ] **XAGENT1d — the pass** (driver; after b, c). The packet; the clone at the candidate with no remote, removed
  after; the instruction; the rules handed; `minutes`; one turn; the `opinion` say code; posture and tier recorded.
  Contract: §4, §5. Proof: `OpinionPacketTests`, the instruction's golden, `ReviewTreeTests` (`Process`: a ref written in
  the clone never reaches the repository).
- [ ] **XAGENT1e — delivered and answered** (driver; after d). The working session's next turn (D137) with `by` and the
  fixed words; answers and the fix commit checked; unanswered as unresolved; one recheck, to the person; the fallback
  to the person; failure as *Try again*. Contract: §6.3–§6.7. Proof: `OpinionDeliveryTests`, `OpinionRecheckTests`,
  resume rows on both doors.
- [ ] **XAGENT1f — the gate** (driver, modules; after e). The occasions; the chain's last step here; the set-up step's
  and `steps`' sits; the gate at every landing door (`opinion` at the look); settled, stale and required; the presses'
  driver halves and terminal verbs; the landing record; the log. Contract: §7–§8. Proof: `OpinionGateTests`,
  `OpinionLandingTests` (`Process`), `trees land --plan`'s golden.
- [ ] **XAGENT1g — the screens** (web-shell, web-settings, modules; after f). The review's *Second opinion* beside
  D154's states, findings with answers, §8.5's presses, What needs you, the conversation's block, the reviewer's session
  row, the task level's choice, the glossary. Contract: §1, §8.5, §9. Proof: vitest, stories, `i18n:check`, `names:check
  --strict`, `HelpCoverageTests`, the look in both themes and 中文.
- [ ] **XAGENT1h — the intake** (driver, service; after c). The room's table names each repository's reviewers; the
  choice set only on a quote, a proposal otherwise. Contract: §2.4. Proof: `IntakePrompt`'s golden, the service's quote
  refusal, a stub intake in the driver's tests.
- [ ] **XAGENT1i — the family rehearsal phase** (tools, examples; after f). A plugin's ACP stub declared with another
  maker reads a stub chain's work; its finding reaches the stub session's next turn; the landing waits for the answer; a
  dispute holds until `opinion anyway`; a required opinion with no reviewer holds. Contract: §6, §8. Proof:
  `rehearse:family`'s new phase.
- [ ] **XAGENT1j — each reviewer's posture, measured** (driver; the owner's run where an account is spent). Claude Code
  under the `opinion` rules, dsh's `read-only`, `codex-acp`'s modes, each asked to write in its tree; a posture is
  declared only where it refused. Contract: §5.2, D53. Proof: an evidence note beside ACP3's, and the twin tables.
- [ ] **XAGENT1k — a second reader, in the canon** (canon, examples; the parent's call). §10's words in
  `autonomous-development`, re-synced here and into `examples/`, with the canon changelog's reason. Contract: §10's
  last part. Proof: `verify`'s `check`, and `rehearse:family`'s example sync.

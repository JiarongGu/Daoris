# Agents of different makers in one session: a lead, its partners, and one sheet per provider (COAGENT1, proposed, 2026-10-22)

**Standing:** proposed, with D199. A synthesis of three independent proposals written from one brief (Claude's, Codex's and
Fable's, under D191), each difference settled by the steward in §1 with its reason. Codex's and Fable's reads of this synthesis
are owed, and Fable reads because the design changes a foundation: the session record gains a field, an entry's landing leaves a
part out, and a new box passes between the service and the driver (D191's amendment). Nothing here publishes, signs in, spends an
account, or removes the person's data; the runs on the install in §8.5 are each the owner's yes.

Read for it: the row COAGENT1; D45, D46, D51, D53, D64, D72, D125, D130, D131, D155, D163, D167, D173 (its FABRIC-ACP1 note), D176,
D177, D187, D188, D189, D191, D193, D197, D198; the mixed-agents design, the second-agent design, the message-urgency design, the
session-context design and the cross-agent brainstorming design; `.claude/knowledge/session-record-fields.md`, `person-waits.md`,
`connector-tools.md`, `bridge-presses.md`, `session-words.md`, `model-decoupling.md`, `translation-parity.md`, `twins.md`; the
co-working skill; the glossary. The facts in §0 were read from the code at `6dc4d7803` and checked again at `11bfe3f70`, this
branch's base; a line that moved between the two is cited at the later.

## 0. The direction, and what is true today

### 0.1 The direction

The owner set it on 2026-10-22, in substance: Daoris's sessions should work with Codex now, and with other makers' agents later,
inside the same session, not only as an outside reader or a successor. That needs one interface every agent provider meets, so a
later maker joins by declaring what it can do rather than by a hand-built integration. The test plan is part of the design, and the
feature is built through the owner's local install, which drives this repository (DOGFOOD1) and holds a Codex account signed in for
Daoris. It is taken first among the features because most of its foundation exists.

### 0.2 What exists, with where it is

| Fact | Where | Standing |
|---|---|---|
| One adapter seam, `ISessionAdapter`, over the pipe door (stream-json) and the protocol door (ACP) | `Agents/Adapters/Adapters.cs:1227-1419` | built |
| The registry: `stub`, `acp-stub`, `claude-code`, `claude-code-acp`, `dsh`, `codex-acp`; `codex` is only the holder of Codex's accounts, not an adapter | `Adapters.cs:2410-2433`; `Accounts/SignIns/CodexAccounts.cs:30-52` | built |
| A plugin's harness, declared by its manifest with `product` and `maker`, always over ACP | `Plugins/Domain/PluginManifest.cs:25-36`; `Plugins/DeclaredAcpAdapter.cs:21-63` | built |
| A maker never declared is never another maker | `PluginManifest.cs:16-18`; `Review/Domain/ReviewerChoice.cs:17-38` (`AgentFamily.Same`) | built |
| The tool's facts: binary, profile variable, sign-in, usage question, limits, windows, turn report, product, maker, mark | `Accounts/Harnesses/Harnesses.cs:176-277` (`HarnessToolchain`) | built |
| What the protocol door negotiates at `initialize`: words at the next step (`promptQueueing`), steering, resume | `Agents/Acp/Acp.cs:962-987`, `:2619-2637` | built; declared nowhere |
| No capability table: no `CanSteer`, `SupportsResume` or `*Capabilit*` type | searched | missing |
| Codex over ACP takes words only at its turn's end, and its steer waits for STEER3 | `Acp.cs:962-965`, `:975-981`; `TASKS.md` STEER3 | built; measurement open |
| Codex takes no rules file, so the person's denies and the tree guard do not reach it | `Adapters.cs:1971-1972`; `Sessions/Domain/DrivenAccess.cs:93-95` | built |
| Codex's `read-only` mode has two readings, neither measured: the adapter's "asks for everything", D155 point 5's workspace-write sandbox | `Adapters.cs:1934-1939`, `:1975-1981`; D155 point 5 | unmeasured |
| Codex reports no usage per turn, and its windows are not carried during a session | `Adapters.cs:2013-2016` (`TurnReport` null); `Accounts/Usage/UsageHistory.cs:151-152` | built; measurement open |
| A session is an entry; each part is one conversation and one record; a part that would run beside a live one gets a tree of its own | D167:14-30, :73-82; `Agents/Adapters/SessionParts.cs:34-35, 111-115`; `Quests/Domain/Planner.cs:1559-1594` | built |
| A further part's tree may grow from a named branch | `Trees/SessionTrees/SessionTrees.cs:250-331` (`OpenAsync`'s `from`) | built |
| An entry's parts land in ledger order on one branch with one press; a part alongside joins by replay | D167:330 (ACCEPTALL1); `Trees/SessionTrees/EntryLands.cs` | built |
| A second live record in a tree is refused at every open | `Sessions/Sessions.cs:1056-1089` (`ActiveForAsync`); `SessionLedger.cs:607-617, 1304-1310` | built |
| The record names its agent (`Adapter`), account (`Profile`), harness version and tree, and no model | `Sessions.cs:63, 80-93` | built |
| Machine-only record fields `Opinion`, `Serves`, `From*` | `Sessions.cs:137-174` | built |
| A hand-over: the brief composed, the old part ended, the new started in the same tree | `Conversations/HandOver/HandOver.cs:201-293` | built |
| A helper: a read-only one-turn session on another maker, in a clone at the asker's last commit, its answer the asker's next turn; two at once | `Loop/DriverHelpers.cs:132, 420-464`; `Review/OpinionTree.cs:66-67`; `HelperDesk.cs:85-178` | built |
| The reviewer's and helper's rules: edits and git's moving acts denied, acting tools denied | `Review/Domain/OpinionPermissions.cs:22-111` | built; posture unmeasured (XAGENT1j) |
| A meeting's turn on a real agent is refused `no-message-tool` | `Loop/DriverMeetings.cs:316-322` | decided (AGENTROOM1b) |
| D188's levels, `said`, receipts; the answers-alone bound of two | D188; `Conversations/Domain/AnswersAlone.cs:21` | built (COWORK1a-e) |
| Only an ask has a channel; a session on none publishes a quest instead | `src/Daoris.Service/Daoris.Service.Mcp/ChannelTools.cs:15-30` | built |
| A quest held in the hand-over store runs on the agent the store names | `Loop/Driver.cs:831` | built |
| One account set per agent (D197); a resume stays on its record's account; the cap counts only driver starts | `Accounts/Harnesses/HarnessRoster.cs:860-975, 1041-1083`; D130:411 | built |
| Each part's tokens summed for the page | `SESSION_SPENT`, `Daoris.Desktop.Modules/Bridge/Routes/SessionsRoutes.cs:460` | built |
| No test runs a real `claude`, `codex` or `dsh`; no Codex-shaped session stub; the kit's ACP stub declares only resume | `tools/rehearsal-kit.mjs:821`; `Driver.Tests/Loop/ChannelNextStepTickTests.cs:185-187` | built |
| The family rehearsal already declares a plugin's harness, `gate-agent`, over the ACP stub | `tools/family-rehearsal.mjs:5945-5995` | built |
| Codex over ACP has never run a measured turn on a Daoris account; Claude Code's protocol door joined its connector calls across 147 sessions | D173's FABRIC-ACP1 note | measured for Claude Code only |

Driver paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/`, service paths under `src/Daoris.Service/Daoris.Service.Core/`.

### 0.3 What is missing

Another maker's agent reaches a session today only from outside it (an opinion, a helper), after it (a hand-over), or with no tree
(a meeting). Nothing lets a second maker **do work** inside the session; nothing tells what any provider can do in one place; the
reader a helper is never sees the work in progress, only the last commit; and two parts of a session on no ask have no channel.

## 1. The three proposals, and how each difference was settled

All three agree on more than they differ: one writer per tree; the second agent never writes the first's tree and nothing it makes
is imported as a patch; Daoris, never an agent's message, decides what makes a turn happen; D188's levels as they are; agreement
between agents establishes nothing, and no co-worker counts as D155's independent reader; a declaration of each provider's
capabilities that keeps *declared*, *negotiated* and *measured* apart; a family-rehearsal phase whose second maker is a plugin
declared by its manifest; real runs only as owner-permitted proofs, never a gate and never a sign-in; the dogfood through DOGFOOD1.
Where they differ, the steward chose:

| # | Question | Claude | Codex | Fable | Settled |
|---|---|---|---|---|---|
| 1 | What working together means | a pair: a writer and a navigator who reads | a writer and a navigator who answers questions | a lead and partners who each write a piece | **Fable's**, with Codex's reading role kept as the helper (§2) |
| 2 | Where the second agent works | the lead's own tree, turns in sequence, a snapshot compared after each reading | a copy holding the uncommitted bytes | a tree of its own grown from the lead's tip | **No second agent ever in the lead's tree**: a partner in a tree of its own (Fable), a reader in a copy with the work in progress (Codex) |
| 3 | Its memory | one conversation kept for the pair's life | a fresh pass each time | one turn per piece | **Fresh**, per piece and per reading (Codex, Fable); a kept conversation is a later row |
| 4 | What starts its turn | Daoris, after each lead turn that changed something, within rounds the person set | a question, within a grant of two turns | the lead's or the person's ask, under the person's rule | **An ask under the rule** (Fable), D163's bounds reused |
| 5 | Changing who leads | a swap at a round's end, both conversations kept | a hand-over, after the outgoing part's descendants end | the hand-over | **The hand-over** (Codex, Fable); Codex's barrier checked in a later row |
| 6 | The cap | a paired conversation spends none | every participant, the person's conversation enrolled | a partner takes a slot as a helper does | **Fable's**, CAPCOUNT1 unchanged |
| 7 | Records and tools | `pair_note`, `pair_ask`, `pair_read`, a `pairs` field | `helper_ask` and `helper_answer`, a `CoWorkRun` record | `partner_ask`, `partner_answer`, a box, a `piece` field | **Fable's** for partners, **Codex's** reuse for readers; the record gains `piece` alone |
| 8 | Names | pair, writer, navigator | co-work, writer, navigator | lead, partner, piece | **Fable's** (§6.7) |
| 9 | The first row | the measurements and the declaration, then the pair | one vertical slice, Codex enabled after a confinement proof | the partner end to end | **A partner by the person's press** (Fable's, smaller); confinement measured in its install run (Codex's) |
| 10 | A limit on the second agent | carried on to another account, or readings paused | the pass ends and that account cools, no second try | carried on once to the next ready account | **Fable's**, D176 point 7's one repair |
| 11 | The provider declaration | a `Capabilities` member, a manifest list, a measuring verb | a projected contract, a measurement record, a manifest object | a sheet derived from what exists, the manifest gaining two postures | **Fable's sheet**, with the version rule from Claude's and Codex's (§3) |
| 12 | D155's and D163's rejection of agents conversing with no person between | amended within the pair's rounds | narrowed by D188's solicited answer and a per-grant bound | stands; only D163's "no writing helpers in a first version" narrows | **Fable's**: the rejection stands (§4.3) |
| 13 | A piece the lead never took in | (none) | (none) | replayed after the lead's at the entry's landing | **Left out of the entry's Accept**: it lands only through the lead (§2.4) |
| 14 | A channel between them | `pair_read` | none | the line's channel for an entry with no ask | **Fable's**, after the core rows |

Why each:

1. **The partner serves the direction; the reader alone does not.** The owner asked for another maker working inside the session.
   A navigator that reads and advises is a helper with better eyes: worth having, and kept (§2.5), but it leaves every change of
   the tree to one maker. A partner writes, in its own tree, on a piece the lead or the person gave it, and its work returns as a
   branch the lead takes in. Most of it exists: a further part in a tree of its own, a tree grown from a named branch, the helper's
   box and choice, the hand of an answer, ACCEPTALL1's entry.
2. **Claude's shared tree is rejected.** It sees the uncommitted work and keeps the conversation's folder for a swap, but it amends
   the one-session-per-tree question every open relies on (`ActiveForAsync`), detects a navigator's write only after it happened,
   cannot see a process or a port it touched, and holds the lead still while the other reads. D51's reason, two agents corrupting
   one tree's git state, does not move. A tree or a copy of its own is prevention, and for Codex, which takes no rules file, it is
   the only floor there is.
3. **A kept conversation waits for resume to be measured.** Resume has never run on the native door, nor a Codex thread after its
   process ended (SESSCTX1a); a conversation kept across pieces also amends D189 point 6. Each piece starts fresh with Daoris's
   brief and `line_read`, which is what D189 built for exactly this.
4. **No turns granted by Daoris between the two.** Claude's automatic reading spends the second maker's account after every changed
   turn with no ask, and needs a new meaning for D188's bound. Codex's grant record bounds what D188 already bounds for helpers:
   two at once, the run's minutes, and two answers alone in a row; under the default `ask`, every piece is the person's yes anyway.
5. **The hand-over exists and keeps D189.** Codex's point stands that a background command of the outgoing part may still write the
   tree when the next part starts; it applies to every hand-over, not to this feature, so it is a row of its own, checked first
   against what the runner's stop already ends (§9, COAGENT1g).
6. **CAPCOUNT1 stays.** Its reason, idle conversations holding slots for hours, holds as much for a conversation that has a partner;
   enrolling the person's conversation would starve driven work. A partner's start is a driver start and takes a slot.
7. **One box per writer of it, one new record field.** Codex's `CoWorkRun` is a second store beside the boxes and the record (D173's
   one-store guard). Claude's three tools serve a pair that is not built. The partner's record takes `piece` alone, not `serves`
   as well: `serves` means a helper in every reader today, and a census of every reader is the cost of overloading it.
8. **Fable's names.** *Co-work* and *co-working* are D167's for sessions of several repositories on one ask, and the canon skill
   carries that name. *Writer* and *navigator* name a pair that is not built.
9. **The first row is the smallest one the owner can use.** The person's own ask needs no rule, no wait row and no agent tool; the
   lead's ask comes next. The provider sheet formalizes facts the first row already reads (a door, a connector, a maker), so it
   follows rather than precedes. Codex's confinement is measured in the first row's install run, before the person sets `on`.
10. **The one repair is built and bounded**; a second try that never runs throws away the piece's work for want of an account.
11. **A sheet derived from what exists cannot drift from it.** A parallel declaration in code beside `ISessionAdapter` would; a
    manifest list of claims about another program's behaviour cannot be checked. The wire answers what it negotiates, a measurement
    names its version, and a plugin's manifest says only what its author can stand behind.
12. See §4.3.
13. **The lead decides what lands.** A piece the lead left out was left out by the one writer who owns the line's tree; landing it
    anyway would land work the lead judged wrong, past the person's look at a whole that holds together.
14. **The line's channel is useful and not first.** A piece's instruction and its one answer carry the first rows' talk.

**Taken from each beyond its shape.** From Claude's: the turn-end protocol stand-in shaped on what codex-acp declares (§8.1); each
answer naming the agent, account, version and model where the door reports one (§5); the maker's marks on the head; the
measurements' list (§8.5); the parent's own Codex reads of each merge going on through the dogfood. From Codex's: the reader's copy
carrying the uncommitted bytes with its manifest and refusals (§2.5); confinement measured, never assumed (§8.5); the barrier on
the outgoing part's descendants as a row (COAGENT1g); a restart marking an attempted turn uncertain, never spent again. From
Fable's: the shape, the rule, the tools, the box, the facts read from git, the sheet, the line's channel, the rehearsal's second
maker by file.

## 2. The roles, and how the work moves

### 2.1 Four roles, two of them new

| Role | 中文 | What it is | Writes | Exists |
|---|---|---|---|---|
| **Lead** | 主导 | the entry's leading part: holds the entry's tree, its plan, its quest's take and close | its tree | yes: today's working session |
| **Partner** | 搭档 | a part of the same entry on another maker's agent, writing one **piece** (分项) in a tree of its own, one turn | its own tree only | new |
| **Helper** | 助手 | a read-only one-turn session on another agent answering one question | nothing | yes (D163); its copy now carries the work in progress (§2.5) |
| **Reviewer** | 阅读者 | a second opinion before landing | nothing | yes (D155), unchanged |

Each part stays one conversation on one record (D131, D189 point 1). There is no fused transcript and no shared harness context:
the shared context is Daoris's, the brief at a part's start (D189 point 4) and `line_read` over the line's parts while it runs.

### 2.2 Who writes

Nothing writes the lead's tree but the lead. A partner writes a tree of its own, a linked worktree on a session branch of its own,
grown from the lead's branch at its last commit when the piece is asked (`SessionTrees.OpenAsync`'s `from`). Two trees, two
writers, D51 unchanged and the one-session-per-tree question unchanged: the partner's record holds its own tree. The lead and a
partner run at once; nothing waits for the other but what one asks of the other.

### 2.3 A piece, end to end

1. **The ask.** The person presses *Ask another agent to work on a piece…* on the session's page, or runs `daoris-driver sessions
   partner <session> "<piece>" [--paths <p>…] [--agent <name>]`; from COAGENT1b, the lead calls `partner_ask` { piece (at most
   4,000 characters), paths (optional, repository-relative), agent (optional, one word), urgent (optional) }. The ask is written as
   `<home>/partners/<id>.ask.json` (one writer, never changed), the helper box's shape plus `paths`, refused in a sentence where the
   record can judge: an asker that is a partner, a helper, a reviewer, an intake, Ask Daoris's conversation or a brainstorm pass; a
   blank piece; a path outside the repository.
2. **The rule** (COAGENT1b). `partners` and `workspacePartners` in `driver.json`: `{ mode: ask | on | off, agents: […], minutes }`, a
   repository's whole over its workspace's, the helper rule's twin. `ask` (the default): the lead's ask waits for the person's yes;
   the person's own ask needs none. `off`: refused with its code. At most two pieces open per lead (`HelpersAtOnce`'s precedent,
   `Loop/DriverHelpers.cs:132`), a third refused `too-many`; a partner asks for no partner, helper or opinion (depth one).
3. **The choice.** `HelperChoice` through `ReviewerChoice.ChooseAsync`, unchanged: the named agent where the rule's list names it,
   else the list in order, else every agent outside the lead's family, another maker first, each account by the walk and its codes
   (`cooling`, `signed-out`, `no-reviewer`, `not-independent`, `refused`). The lead's own maker only where named; the piece then says
   *the same agent, fresh*. The partner must meet §3.3's row; otherwise refused with the sheet's code (`no-connector` and the like).
4. **The start.** The look opens the partner's tree from the lead's last commit, asks the host for the record (`POST /api/partners`:
   kind `chat`, `piece` = the ask's id, `entry` = the lead's), writes the state `started` naming the partner before its turn, and runs
   one turn under the partner's rules (§7.3) with its instruction: the piece as written, the paths, the commit it grew from, the
   lead's uncommitted paths at the ask as Daoris read them (so the partner leaves them alone), the line's brief (`ContextBrief`, the
   lead's record first), how to end (commit on its branch; say what it did, what it did not, what it would need) and that it asks
   nobody. Its bound is the rule's `minutes` (45 as a first value; the session timeout behind it); its end is what Daoris observes
   of its process (D46), never its words.
5. **The answer.** `partner_answer` { summary (at most 20,000 characters), left (what it could not do) }, once; else its last words,
   bounded as a helper's are; else `refused` with `no-answer`. Daoris adds what it reads itself, never from the words: the branch,
   the commits past the commit it grew from, the files changed, those outside the piece's paths (*beyond its piece*), the declared
   gates its tool calls ran and the newest failed one's kept output up to 2,000 characters (D189's check rule).
6. **The delivery.** Through `POST /api/partners/{partner}/hand`, a said word marked `partner` (beside `helper`), in fixed words
   (`PartnerWords`, no model): who answered on which account, the piece, the facts, the summary quoted as the partner's claim, and the
   line that takes it in: *in your tree: `git merge --no-ff <branch>`*. It goes at the ask's level under D188's one rule (§4.2), and
   it counts toward the answers-alone bound as a helper's does.
7. **Taking it in.** The lead merges, or cherry-picks, the branch in its own tree, in its own turn, and goes on. `partner.taken-in` is
   written when Daoris reads the piece's commits in the lead's branch.
8. **Stops and ends.** The person's *Stop…* on a partner ends its part `stopped`, its commits kept on its branch, the lead told at
   ordinary. A partner past its minutes ends `out-of-time`, the same. A lead that ends with a piece still working leaves the answer
   waiting on its record for the entry's next part, as a channel claim does (D188 point 6). A restart around a start or an answer
   marks it uncertain and runs nothing again unasked.

### 2.4 Landing

A piece reaches the line **only through the lead**: the commits the lead merged land once, with the lead's part. The entry's Accept
(ACCEPTALL1) leaves out every part that carries `piece`; a piece never taken in is named in the landing's plan as *not taken in*
(未并入), and its tree and branch stay until the person discards them (D153's clear). This amends ACCEPTALL1's replay for partner
parts alone; a part alongside that is not a partner's lands as it does today.

### 2.5 Reading the work in progress: the helper's copy

A helper's copy today is a clone at the asker's last commit (`Loop/DriverHelpers.cs:444`; `Review/OpinionTree.cs:66-67`), so a
helper on another maker never sees what the lead has not committed, which is where most of a turn's work is. From COAGENT1c the copy
carries it:

1. At the asker's turn boundary, with no turn in flight, Daoris captures the asker's tree: the bytes of every changed tracked file,
   the deletions and every untracked file not ignored, with a manifest (the commit, the paths, each file's hash, what was left out
   and why, when it was read). It changes neither the asker's index nor its refs.
2. It refuses an unresolved merge, a link that escapes the tree, a submodule, an unreadable file and a capture over a bound, each
   said; ignored files are never carried.
3. It reads the asker's state again after the capture, and a capture that changed under it is thrown away and taken again once.
4. The clone is made as today and the captured bytes laid over it; nothing is read back from it, as today.
5. The helper's answer names its capture, and the delivery says the work may have moved since.

This is how another maker reads alongside, a navigator in the other proposals' words, without a third mechanism. The opinion's copy
is unchanged: a reviewer reads a candidate's commits, never the work in progress (D155 point 6).

### 2.6 Changing who leads

The hand-over as built (D163, MIXAGENT1c, D189's brief and closing note): the old part ends at its turn's end, the new starts in the
same tree on the other maker, handed the brief. Going back is another hand-over. A partner never becomes the lead by itself, and a
piece's partner is never handed the lead's tree.

## 3. The provider contract: one sheet per agent

### 3.1 Sources and states

The interface is the adapter seam as it stands (`ISessionAdapter`; `HarnessToolchain`; a plugin's `PluginHarness` built as
`DeclaredAcpAdapter`), what the protocol door negotiates at `initialize`, and the connector every part is handed. COAGENT1 adds one
**sheet** per agent, `AgentCapabilities`, which the driver derives from those sources and never stores apart from them. Each row has a
**source**:

- **declared**: the adapter's own word in Daoris's code, or the manifest's for a plugin's harness;
- **negotiated**: the wire answered it at the agent's last start, kept per agent and version as the roster keeps a reading;
- **measured**: an evidence document under `docs/` names the harness version it was measured on; a different version reads *declared*
  until it is measured again;
- **absent**: nobody declared, negotiated or measured it, and its fallback applies; silence never reads as support.

Safety rests only on a tree or copy of its own and on measured rows; a declared row may enable a convenience, never a guarantee.

### 3.2 The rows

| Capability | Known by | Claude Code (ACP) | Claude Code (pipe) | Codex (ACP) | A plugin's harness | Fallback where absent |
|---|---|---|---|---|---|---|
| Door | declared (`Wire`) | protocol | pipe | protocol | protocol | none: without a door it is no provider |
| Conversation | declared (`Interactive`) | yes | yes | yes | yes | driven one-turn parts only: a partner, never a conversation's lead |
| Turn's end observed | protocol by contract; pipe by `StructuredOutput` | yes | yes | yes | yes | the record moves on exit and quest state alone (D46) |
| Resume | negotiated (`session/resume`, `session/load`) or declared (`PrepareResume`) | negotiated | declared, never run here | negotiated; `elsewhere` on another client | negotiated | a new part handed the brief (D189) |
| Words at the next step | negotiated (`promptQueueing`) | yes | no | no, until STEER3 | as negotiated | urgent waits for the turn's end, said so; the skill's pull reaches it at its next Daoris call |
| Steering (*now*) | negotiated, and measured (STEER evidence) | measured | no | offered, not measured | as negotiated | *now* waits as urgent, said `no-interrupt` |
| A stop that keeps the conversation | declared (`FrameInterrupt`) or `session/cancel` | yes | a conversation's only | yes | yes | *Stop* ends the part |
| Postures | declared, in the harness's own words | `auto`/`acceptEdits`, `default`, `bypassPermissions` | the same | `agent`, `read-only`, `agent-full-access` | `posture`; from COAGENT1d also `fullPosture`, `askingPosture` | a full or asking start is refused naming the agent |
| Takes Daoris's rules | declared (`TakesSettings`) | yes | yes | no | no | the tree is the floor; the page says the person's denies do not reach it |
| A held request reaches the person | protocol, under the asking posture | yes | no | yes | from COAGENT1d, where it declares `askingPosture` | refused under any other posture |
| The connector | protocol on the wire; pipe by `--mcp-config` and the repository's `.mcp.json` | yes | yes | yes | yes | never a partner (`no-connector`) |
| Connector calls joined to their session | measured (FABRIC-ACP1) | measured | not measured | not measured (M1) | not measured | read as a child's: reading tools only (C2a) |
| Instruction files | measured per harness (the entry-point evidence) | `CLAUDE.md`, else `AGENTS.md` | the same | `AGENTS.md`, 32,768 bytes for the chain | unknown | the role's instruction carries what the part needs |
| Usage per turn | declared (`TurnReport`), measured | per turn | per turn | none (M4) | none | *not measured*, never zero (D57) |
| Account windows | declared (`Usage`, `Limits`) | asked and forwarded | asked | asked of its server between turns | none | the walk has no reading for it |
| Accounts | declared on the toolchain | `claude-code`'s set | `claude-code`'s set | `codex`'s set | `accountOf`, `profileVariable` | its own home: no walk, no cool-off, a limit reads as a failure |
| Model and effort | declared (settings file) or negotiated (`configOptions`) | both | the settings file | `configOptions` | `configOptions` | the provider's default, said as unknown |
| Maker and product | declared (`Maker`, `Product`) | Anthropic, Claude Code | the same | OpenAI, Codex | `maker`, `product` | never another maker (`maker-not-declared`) |
| Native children | measured (D173's audit) | routed per session | parent ids, cannot be stopped | not routed | not routed | treated as a child: reading tools only |

### 3.3 What each role needs

- **A lead of a driven session**: what a lone session needs today. **A lead of a conversation**: a conversation.
- **A partner**: a door with a turn's end, the connector, a declared maker, and any work posture (its tree is the floor). Without the
  connector it is refused `no-connector`; it could not answer, read the line, or be held to the connector's guard.
- **A helper or a reviewer**: as today.
- **Words sooner** than a turn's end: words at the next step, negotiated; *now*: steering, measured.

### 3.4 What a plugin's manifest may say

It gains `fullPosture` and `askingPosture`, in the harness's own words (today both are refused for every plugin's harness,
`Adapters.cs:1266-1267, 1279-1280`), so a declared provider may run under a repository's unrestricted or asking setup once its author
measured them; the CLI's `plugins.ts` reads them by the same rules, the twins' table extended. It may not claim words at the next
step, steering or resume (the wire answers those), nor rules, limit words or child lineage (claims about another program that only a
measurement with a recorded sentence may make, D125, D53). A later maker over ACP joins with a manifest and nothing else; a maker over
another transport needs a door, never a branch on its name in the partner's code.

### 3.5 Measurements

A measurement is a proof run by a person on a machine set up for it, in `tools/proof-kit.mjs`'s shape (`:1-8`: a proof is not a gate,
it spends a login), and its evidence document names the harness and adapter versions, the scenario, the frames observed and the
date. No measuring verb is built now; the parent's proofs on the install are the first evidence (§8.5).

## 4. How the agents talk, under D188

### 4.1 The paths

Only through Daoris's connector and the co-working skill (D188's owner's note; D177 point 1): `partner_ask` and `partner_answer`,
`helper_ask` and `helper_answer`, `line_read`, quests, and from COAGENT1e the line's channel. Never a harness's own channel, never
another agent's process, and authorship always from the binding, never from what a message says.

### 4.2 The levels

A partner's answer is a solicited answer: ordinary, or urgent where the ask was. Urgent reaches the lead at its next step where its
door takes words then (Claude Code over ACP) and at its turn's end elsewhere (Codex, the pipe door, the stubs), marked and said so. A
solicited answer may extend or reopen its asker's run once per ask. A channel post is an unsolicited claim, which starts, reopens and
extends nothing, urgent within three of ten. Nobody but the person sends *now*, and no agent stops another's turn.

### 4.3 D155's and D163's rejection of agents conversing with no person between stands

It stands as a rule. What it guarded against was a loop with nobody in it: accounts spent without the person, and an agreement that
establishes nothing. Neither can form here:

- the only message that moves a run is an answer the person's rule allowed (`ask` by default: each piece is the person's yes);
- a partner cannot ask back, nor for a partner, a helper or an opinion, so there is no chain and no fan-out;
- a line goes on by answers alone at most twice in a row, the third waiting for the person (COWORK1e, unchanged, with a partner's
  answer counted as a helper's is);
- what returns is a branch the lead must take in itself, with the facts Daoris read from git beside the partner's words;
- agreement still establishes nothing: a defect is a gate's verdict (D155 point 8), and neither the lead's merge nor the partner's
  summary is a check.

What narrows is D163's rejection of **writing helpers in a first version**, which the mixed-agents design left out because their work
needed a landing design. §2.4 is that landing: a partner writes in its own tree, and its work lands only through the lead.

## 5. Accounts and usage, under D197

- **Each part runs on one account of its agent's one set** (D197 point 1). The lead's is its own; a partner's is the walk's for the
  partner's agent, through `HelperChoice` and `ReviewerChoice.ChooseAsync` with cool-offs, sign-ins, the order and the use settings.
  The person's own sign-in is never a partner's account and is never asked (D197 point 3).
- **A limit met mid-piece** cools the account, never a strike (D125), and the piece is carried on once on the next ready account of the
  same agent in the same tree, handed the brief and its commits so far (D176 point 7's one repair); with none ready, the piece waits
  `cooling` and the lead is told at ordinary.
- **The cap** (D130, CAPCOUNT1): a partner's start is a driver start and takes a slot, as a helper's does; a full cap waits, said. A
  conversation lead still holds none.
- **Usage is each part's**, summed by `SessionEvents.Spent` and answered by `SESSION_SPENT`; the head sums the parts per maker. A count
  no turn said stays unknown, never zero (D57). Codex reports no turn usage on its door today, so its figure reads *not measured* until
  M4 says otherwise; its account's windows still come from its server between turns (`HarnessRoster.AskUsageAsync`).
- **The tier that ran**: the record names the agent, account and harness version, never a model (`Sessions.cs:63, 80-88`); a piece's
  answer and a helper's say the agent, the account's name and the version, and the model where the door reported one (the pipe door's
  `result`, `Agents/Streams/StructuredOutput.cs:146-166`). Where no agent could take a piece, it says so and nothing claims the piece
  was worked.
- **Nothing spends unasked on the install.** Under `ask` every piece is the person's press. The first `on` for this repository is the
  owner's yes to Codex's account being spent on pieces (D191 point 3), after M1 and M2.

## 6. What the person sees and presses

### 6.1 The session's page

| Where | English | 中文 |
|---|---|---|
| The head, where the line holds more than one maker | *Lead: Claude Code* | *主导：Claude Code* |
| The head | *Partners: Codex, 1 piece working* | *搭档：Codex，1 个分项进行中* |
| The head's spend, per maker | *Spent: Claude Code 41k · Codex not measured* | *用量：Claude Code 41k · Codex 尚未测量* |
| The head's ⋯ | *Ask another agent to work on a piece…* | *请另一个智能体做一个分项…* |
| The *Parts* strip, a partner's part | *partner on piece p1* | *做分项 p1 的搭档* |
| The conversation, a piece's answer | *A partner's answer: claims to check* | *搭档的答复：需要核实的说法* |

Each maker is marked by its two letters (*CC*, *Cx*, a plugin's declared mark), never a hue alone. A line on one agent says nothing new,
as `leadingAgentOf` holds today. A partner's own page is titled *Partner* (搭档), shown under its lead (*Working for {{title}}*, the
helpers' rule), its review pane its branch's diff.

**The Pieces section** (分项), under the head, lists each piece with its state and acts:

| State | English | 中文 | Acts |
|---|---|---|---|
| asked | *Asked* | *已请求* | — |
| waiting for the person | *Waiting for you* | *等你处理* | *Let it work* 让它做 · *Not now* 暂不 |
| working | *Working* | *进行中* | *Open its part* 打开它的段 · *Stop…* 停止… |
| answered | *Answered* | *已答复* | the branch, the files, *{{n}} files beyond its piece* 超出分项的文件 {{n}} 个, *Discard…* 丢弃… |
| taken in | *Taken in* | *已并入* | — |
| not taken in, at the landing | *Not taken in* | *未并入* | *Discard…* 丢弃… |
| refused | *Refused* | *已拒绝* | why, in words |
| stopped | *Stopped* | *已停止* | — |
| out of time | *Out of time* | *超时* | the commits kept |

### 6.2 What needs you

One new row kind, from COAGENT1b: *A partner is asked to work on a piece* (*有一个分项请求搭档来做*), with *Let it work* and *Not now*,
judged in .NET and counted by the bell, in every place `person-waits.md` lists. A partner's answer held by the answers-alone bound uses
the built *Go on with this answer* row.

### 6.3 Setup and the Agents page

*Partners* (搭档) beside *Helpers* on a repository's Setup and a workspace's Defaults: the mode (*Ask me* / *On* / *Off*), the agents,
the minutes; the terminal twin `daoris driver partners`; Ask Daoris's `setting` door `partners`. Each agent's card gains *In a shared
session* (*共同工作时*): the sheet's rows in plain words with their source, *takes words mid-turn (its own word)* (*可在回合中接收消息
（它自己声明）*), *steers (measured {{date}})* (*可转向（{{date}} 测量）*), *takes Daoris's rules* (*接受 Daoris 的规则*), *not measured*
(*尚未测量*).

### 6.4 The terminal

`daoris-driver sessions partner <session> "<piece>" [--paths …] [--agent <name>]` asks one; `sessions partner <session> --list`
shows the pieces; `sessions partner <piece> --stop` stops one. Each is a person's door, reaching the running Daoris as the sessions
family's verbs do (D194), and its answer names the piece and its state: `asked`, `waiting`, `started`, `refused` (why), `stopped`.

### 6.5 Ask Daoris

It may propose a piece on a session (a `session` card), and the `partners` setting, with the terminal's equivalent; the person applies.
It never asks for a piece by itself (D110, D183).

### 6.6 What the person never needs to do

Nothing here asks the person to reason about trees or branches: the delivery tells the lead how to take a piece in, and the person's
acts are *Let it work*, *Not now*, *Stop…* and *Discard…*.

### 6.7 The names, for the glossary

| Term | English | 中文 | Why |
|---|---|---|---|
| lead | lead | 主导 | the part that holds the entry's tree and plan |
| partner | partner | 搭档 | a working partner, apart from 助手 (reads only) and 队友 (a person on another machine) |
| piece | piece | 分项 | one item of the work given to a partner |
| maker | maker | 厂商 | already the page's word (`opinion.json`) |

Avoided: *co-work* and *co-working* (D167's), *driver* (Daoris's program), *review* and *reviewed* (the person's), *room* (a pass's
sandbox), *meeting* (D177's), *writer* and *navigator* (a pair not built).

## 7. Records and their places

### 7.1 Files

The helper box's shapes with what a piece adds, under `<home>/partners/`: `<id>.ask.json` (id, asker, piece, paths, agent, urgent,
askedBy, at; written once), `<id>.state.json` (the driver's alone: `waiting`, `approved`, `started`, `answered`, `refused`, `stopped`,
`out-of-time`; code, partner, branch, grew-from, at), `<id>.answer.json` (summary, left, at) and `<id>.facts.json` (branch, commits,
files, beyond, checks). The service's `PartnerBox` and the driver's reader share no code and hold one table (`twins.md`).

### 7.2 The record

The session record gains `piece` (the ask's id), machine-only as `opinion` and `serves` are, through every place
`session-record-fields.md` lists; a partner's `entry` is its lead's, so its part is the session's (one rule in `SessionEntries`).
Every reader that refuses or groups a helper by `serves` is left as it is; every place that must treat a partner apart reads `piece`.

### 7.3 The connector and the rules

`partner_ask` and `partner_answer` in `PartnerTools.cs`, in every place `connector-tools.md` lists. The partner's rules (`PartnerPermissions`,
beside `OpinionPermissions`): edits, and git's commit, branch and checkout, allowed in its tree; push denied; `partner_answer`,
`line_read`, the knowledge tools and reading allowed; `partner_ask`, `helper_ask`, `opinion_give`, `handover_propose`, `quest_publish`,
`quest_respond`, `go_ahead_ask`, `question_ask` and `channel_post` denied. Codex takes no rules file: its tree, its instruction and
D198's git doors (once built, GITAUTH1) are its floor, and the page says the person's denies do not reach it.

### 7.4 Notes and the log

Coded notes in both catalogues: on the lead `partner.asked`, `.started`, `.answered`, `.taken-in`, `.refused` (why), `.stopped`,
`.out-of-time`, `.beyond`; on the partner `partner.working`. The machine log: `partner.asked`, `.started`, `.answered`, `.refused`, the
account by its profile name as `account.rotated`'s is.

### 7.5 The sheet

`AgentCapabilities`, answered on `HARNESSES` and `agent list`; the bridge's read in every place `bridge-presses.md` lists for a read.

## 8. The test plan

### 8.1 The stand-ins

- **A turn-end protocol stand-in shaped on codex-acp**: no `promptQueueing`, the steering extension offered and unused, the modes
  `agent`, `read-only` and `agent-full-access`, a permission request before each write in `read-only`, no usage per turn. It sits in
  `tools/rehearsal-kit.mjs` beside `ACP_STUB_AGENT`, and as a scripted in-memory agent for the fast half, so every partner test has a
  lead that takes words at its next step and one that does not.
- **`ACP_STUB_AGENT` gains a switch** (`DAORIS_STUB_QUEUEING`) to declare `promptQueueing`, since the kit's stub declares only resume
  (`tools/rehearsal-kit.mjs:821`).
- **A second maker by file**: a test plugin manifest whose harness names `maker: "Stub Two"` and `product: "Stub B"` over the same
  stub script, so `AgentFamily` reads another maker with no real agent, as the rehearsal's `gate-agent` is declared today.

### 8.2 The fast half

`PartnerAsksTests` and `PartnerJudgeTests` (each refused asker, a blank piece, a path outside, two open, depth one; from 1b the rule's
three modes), `PartnerChoiceTests` (another maker first, the same maker only when named, the walk's codes, the sheet's refusals),
`PartnerPassTests` (the tree grown from the lead's last commit, the state written before the turn, the rules' allows and denies, the
instruction's golden under `golden/partner-prompt/`, the lead's uncommitted paths listed), `PartnerFactsTests` (branch, commits, files,
beyond, checks, over a git tree), `PartnerDeliveryTests` (the ask's level on each door, once per ask, a conversation lead, an ended lead
reopened, a stopped piece told at ordinary), `AnswersAloneTests` with a partner's answer, `NoteCodesTests`; the service's
`PartnerBoxShapeTests`, `McpPartnerToolTests`, the connector's registration test, `SessionLedgerTests` for `piece` and the entry rule;
the modules' `DriverModulePartnerTests`, `StatePublisherTests`, `PipeCensusTests`; the web's `Pieces`, `PartsStrip`, `headFacts` tests
in English and Chinese and the stories; `HelpCoverageTests` rows; `i18n:check`; `names:check -- --strict`. From 1c,
`HelperCaptureTests` (deletions, untracked files, binary files, an escaping link refused, an unresolved merge refused, a capture over
its bound refused, a capture that changed under it taken again, the manifest). From 1d, `AgentCapabilitiesTests` (each row's source and
fallback per adapter, a version change turning *measured* into *declared*, the manifest's two postures and the twins' tables). Every
wait a test gives a real loop reads a held clock (`held-clocks.md`).

### 8.3 Real processes with stand-ins

`PartnerProcessTests` (the `Process` half): the loop's real tick with the ACP stub as lead calling `partner_ask` over the connector, the
partner on the second maker's stub in its own tree, committing (`STUB_COMMIT`), answering; the answer at the lead's next step on a stub
that queues and at the turn's end on the turn-end stand-in; a stalled partner cut at its minutes with its commits kept; a stop; a
restart around the answer's write, marked uncertain and run again by nothing. `PartnerLandingTests` (the git half): the lead merges the
piece and the entry lands once; a piece never taken in is left out and its tree stays; the partner's tree removed at the person's
*Discard*. `PartnerChatProcessTests`: a conversation lead hears the answer as a turn of its own between turns. `HelperCaptureProcessTests`
(git half): a helper's copy holds the asker's uncommitted bytes and nothing returns from it.

### 8.4 The family rehearsal

One phase, run serially by the parent, on an isolated home with the rehearsal's guards: the kit's stub as the lead, declaring queueing,
and the same script declared a second time by a rehearsal plugin manifest as `stub-b` of maker *Stub Two*, joining by a file with no
Daoris code; an ask, its quest, the lead's take, `partner_ask`, the partner's part in its own tree, its commit, its answer at the lead's
next step, the lead's merge, done, the entry landed on one branch holding both parts' commits; a second piece never taken in, left out of
the landing and still on its branch. The transcript and the facts files are kept as evidence.

### 8.5 Runs on the install (each the owner's yes, D191 point 3; proofs, never gates, never a sign-in)

With COAGENT1a's install proof, in this order:

- **M1** Codex's connector calls joined to their session: the partner's `partner_answer` call read as FABRIC-ACP1 read Claude Code's
  lineage (D173's note, its open half).
- **M2** Codex's confinement as a partner under `agent`: the lead's tree's files and refs hashed before and after the piece; whether any
  write left the partner's tree; whether `git push` was refused, and by what.
- **M3** the cost per maker: the brief's characters, each part's tokens as reported, the lead's wait, the files beyond the piece.
- **M4** what codex-acp reports of a turn's usage, so the sheet can state it.
- **Then the reverse** (COAGENT1f): Codex leading and Claude Code as the partner, so a turn-end lead is measured receiving an answer at
  its end; a Codex conversation resumed after its process ended (SESSCTX1a's Codex half); Codex's `turn/steer` (STEER3) where ready.

A failed M2 is filed as a row restricting Codex as a partner before `on` is set. Each measurement is an evidence document and a dated
note on D199.

### 8.6 The build dogfoods it

COAGENT1a is built by a branch-worker, since nothing exists for the install to use. From COAGENT1b, once M1 and M2 hold and the owner
says yes, this repository's `partners` rule is `on` with `agents: [codex-acp]`, and each later COAGENT row is filed as an ask to the
install (DOGFOOD1): the Claude Code lead gives Codex pieces that split cleanly (a terminal twin, catalogue rows and their tests, a
`Process` test class). The parent lands each branch through `tools/merge-branch.mjs`, files what blocked or misled a partner as a row or
a fix-log entry, and records per row the pieces asked, taken in and refused, the files beyond, and the cost per maker. The parent's own
Codex reads of each merge go on: a partner is never the merge's second reader. Installed-window proof stays distinct from a story or a
mocked bridge.

## 9. The rows

Each is one feature a person can see working (D187).

1. **COAGENT1a — a partner works one piece of a session in a tree of its own, asked by the person** (service, driver, modules,
   web-shell). §2.3 steps 1 and 3 to 8 for a driven lead and a conversation lead, by the person's press and the terminal verb; the box,
   `partner_answer` and the partner's rules; the record's `piece`; the facts from git; the delivery; §2.4's landing; the Pieces section,
   the head, the *Parts* strip, the notes and the log, in English and Chinese. Its eligibility reads today's facts (a door, the
   connector, a maker). Proof: §8.2 and §8.3's cases filtered; the window on the install with Claude Code leading and Codex as the
   partner, carrying M1 to M4. Large for one builder: it may split at dispatch into the service's and driver's half (the
   terminal verb) and the page's half, landed together.
2. **COAGENT1b — the lead asks for a piece** (service, driver, modules, web-settings, web-shell; after 1a). `partner_ask` under the
   `partners` rule (`ask`, `on`, `off`; the agents; the minutes), its wait in *What needs you* and every place `person-waits.md` lists,
   the Setup rows and `daoris driver partners`, Ask Daoris's `setting` door and its `session` card, the co-working skill's paragraph on
   partners in the canon, synced to this repository and both examples. Proof: the judge's and the tool's tests, the wait's census, the
   page in both languages; then the owner's `on` and the dogfood.
3. **COAGENT1c — a helper reads the work in progress** (driver; after 1a, beside 1b). §2.5's capture for every helper's copy. Proof:
   `HelperCaptureTests` and its `Process` case; the capture's cost on this repository measured on this machine and noted.
4. **COAGENT1d — the provider sheet, and a maker that joins by declaration** (driver, modules, web-settings, cli; after 1a). §3's
   `AgentCapabilities` and its sources, on `HARNESSES` and `agent list`, the Agents page's *In a shared session*, the manifest's two
   postures in both twins, the partner's judge refusing by the sheet; the turn-end stand-in and the kit's queueing switch; §8.4's
   rehearsal phase. Proof: the sheet's tests per adapter, the twins' tables, the rehearsal phase.
5. **COAGENT1e — the line's channel** (service, driver, web-shell; after 1b). An entry with no ask gets a channel keyed by its origin;
   `channel_post` and `channel_read` resolve it as they resolve an ask's; `to` names a part's maker or nobody; delivery and receipts as
   COWORK1d built them; the panel on a session with no ask. Proof: `ChannelTests` over a session origin, the tick test with two stubs, the
   page in both languages.
6. **COAGENT1f — measured both ways on the install** (the parent with the owner; after 1b). The reverse run, Codex's resume and steer
   where ready, the first values (45 minutes, two open, the brief per piece) read again in a D199 note.
7. **COAGENT1g — a hand-over waits for the outgoing part's descendants** (driver; checked first). Whether the runner's stop already ends
   a part's background commands and children before the next part starts in the tree; where it does not, the hand-over waits for them,
   said. Proof: a `Process` case with a background command outliving its turn.
8. **Later, each after its measurement or its own design**: a partner resuming its own conversation on a second piece (amends D189
   point 6; SESSCTX1a first); a reading by another maker at the lead's done, as an occasion of D155's kind; three makers in one line; a
   new conversation started with a partner from the composer; a piece as a fabric commitment with a mapping (D173 ADR-E).

## 10. What the decision amends, and what it rejects

**Amends:**

- **D163**: point 2's read-only helper gains a writing sibling, the partner, in its own tree on one piece; the rejection of writing
  helpers in a first version is spent; a helper's copy carries the asker's work in progress (§2.5).
- **D167**: a partner's part takes its lead's entry; ACCEPTALL1's entry Accept leaves out a partner's part, which lands only through
  its lead (§2.4).
- **D64**: a plugin's harness may declare `fullPosture` and `askingPosture`.
- **D72**: a partner's shape of the rules.
- **D177** point 5: the writing participant it left to a decision of its own is a session's partner, writing in its own tree; a
  meeting still talks and holds no checkout.

**Stands**: D51 and the one-session-per-tree question; D53 (each door a configuration); D131; D155 whole (a partner's commits are
among the work's families for its walk by point 4 as written); D188 whole; D189 (point 6 included); D197; CAPCOUNT1; D24; D46; D47 §4.

**Rejects**: a second agent in the lead's tree, at once or in turn; a reader in a copy without the work in progress; turns Daoris grants
between the lead and a partner; a conversation kept across pieces before resume is measured; a swap that keeps both conversations; a
co-work record beside the boxes; enrolling the person's conversation under the cap; a partner's work landed past its lead; a partner's
edits applied as a patch; the manifest claiming queueing, steering, rules, limit words or lineage; a capability sheet a model is asked
about; Daoris choosing a maker the person's rule did not list; a token or money cap no harness enforces.

## 11. What is not known

- **Codex as a partner** (M1, M2, M4): whether its connector calls join, whether its writes stay in its tree under `agent`, whether it
  can push, what it reports of a turn's usage. Until M2, the first `on` waits.
- **The brief's cost per piece**, and whether 45 minutes and two open pieces are the right first values (M3, COAGENT1f).
- **The capture's cost** on this repository on this machine, where a program starts slowly (COAGENT1c).
- **Whether the runner's stop ends a part's background work** before a hand-over starts the next part (COAGENT1g).
- **Whether a plugin's harness runs a second stand-in beside the first** in one rehearsal home (two commands under one map), which
  §8.4 assumes.

## 12. The reads (D191)

Owed: Codex's read, then Fable's (a foundation: the record's field, the landing's rule, a box between two processes). The table of
findings and verdicts is written here as they are taken.

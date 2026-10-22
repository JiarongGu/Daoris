# Agents of different makers in one session: a lead, its partners, and one sheet per provider (COAGENT1, accepted, round 3, 2026-10-22)

**Standing:** accepted with D199 by the steward on 2026-10-22, in round 3. Round 1 was a synthesis of three independent proposals written from one brief
(Claude's, Codex's and Fable's, under D191), each difference settled by the steward in §1. Codex read round 1 (twenty-one findings,
eighteen blocking) and Fable read it as a foundation (ten findings, four blocking); round 2 answered all of them. Codex read round 2
(eleven of its round-1 findings held, ten held in part; thirteen new findings, twelve blocking), and round 3 answers those (§12). Every
finding was checked against the code before it was taken. No read asked to rethink the shape; each found a promise that lacked the
mechanism keeping it, and each round added the mechanism.
Nothing here publishes, signs in, spends an account, or removes the person's data; the runs on the install in §8.5 are each the
owner's yes.

Read for it: the row COAGENT1; D45, D46, D51, D53, D64, D72, D125, D130, D131, D155, D163, D167, D173 (its FABRIC-ACP1 note), D176,
D177, D187, D188, D189, D191, D193, D194, D197, D198; the mixed-agents design, the second-agent design, the message-urgency design,
the session-context design and the cross-agent brainstorming design; `.claude/knowledge/session-record-fields.md`, `person-waits.md`,
`connector-tools.md`, `bridge-presses.md`, `session-words.md`, `ref-removal-transactions.md`, `model-decoupling.md`,
`translation-parity.md`, `twins.md`; the co-working skill; the glossary. The facts in §0 were read from the code at `6dc4d7803` and
checked again at `11bfe3f70`, this branch's base; a line that moved between the two is cited at the later.

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
| A plugin's harness, declared by its manifest with `product` and `maker`, always over ACP, interactive | `Plugins/Domain/PluginManifest.cs:25-36`; `Plugins/DeclaredAcpAdapter.cs:21-63` | built |
| A maker never declared is never another maker | `PluginManifest.cs:16-18`; `Review/Domain/ReviewerChoice.cs:17-38` (`AgentFamily.Same`) | built |
| The tool's facts: binary, profile variable, sign-in, usage question, limits, windows, product, maker, mark, and what its turn counts mean (`TurnReport`, null where never measured) | `Accounts/Harnesses/Harnesses.cs:176-277`, `:248-252` | built |
| What the protocol door negotiates at `initialize`: words at the next step (`promptQueueing`), steering, resume | `Agents/Acp/Acp.cs:962-987`, `:2619-2637` | built; declared nowhere |
| No capability table: no `CanSteer`, `SupportsResume` or `*Capabilit*` type | searched | missing |
| Codex over ACP takes words only at its turn's end, and its steer waits for STEER3 | `Acp.cs:962-965`, `:975-981`; `TASKS.md` STEER3 | built; measurement open |
| Codex takes no rules file, so the person's denies and the tree guard do not reach it | `Adapters.cs:1971-1972`; `Sessions/Domain/DrivenAccess.cs:93-95` | built |
| Codex's `agent` posture is a workspace-write sandbox with no network; its `read-only` has two readings, neither measured | `Adapters.cs:1934-1939`, `:1975-1981`; D155 point 5 | unmeasured |
| A protocol turn's counts are read from the prompt's answer where the agent reports them; what Codex's counts mean was never measured (`TurnReport` unset), and its windows are not carried during a session | `Acp.cs:1403-1408`; `Harnesses.cs:252`; `Adapters.cs:2013-2016`; `Accounts/Usage/UsageHistory.cs:151-152` | built; meaning unmeasured |
| A session is an entry; each part is one conversation and one record; a part that would run beside a live one gets a tree of its own | D167:14-30, :73-82; `Agents/Adapters/SessionParts.cs:34-35, 111-115`; `Quests/Domain/Planner.cs:1559-1594` | built |
| A helper and a reviewer keep their own entry and are no part of the line they serve | `src/Daoris.Service/Daoris.Service.Core/Sessions/SessionEntries.cs:76-77`; `HelperDesk.cs:99-102` (`ownEntry: true`) | built |
| An entry's Accept lands every part whose tree offers work; a part alongside joins by replay | `Trees/SessionTrees/EntryAccepts.cs:24-30`; D167:330 (ACCEPTALL1) | built |
| A session's tree is a linked worktree, sharing the repository's refs, configuration, hooks and objects; the reviewer's and helper's copy is a clone because a worktree would share them | `Trees/SessionTrees/SessionTrees.cs:369`; `Review/OpinionTree.cs:12-16, 66-67, 88-92` | built |
| A second live record in a tree is refused at every open | `Sessions/Sessions.cs:1056-1089` (`ActiveForAsync`); `SessionLedger.cs:607-617, 1304-1310` | built |
| The record names its agent (`Adapter`), account (`Profile`), harness version and tree, and no model | `Sessions.cs:63, 80-93` | built |
| Machine-only record fields `Opinion`, `Serves`, `From*`; `entry` travels | `Sessions.cs:137-174, 211-219` | built |
| A record with `serves`, `opinion` or an ask takes a slot under the cap | `Quests/Domain/Planner.cs:810-822` (`HoldsSlot`) | built |
| The service refuses a record with `serves` at the helper's, question's, hand-over's, opinion's and channel's doors; the quest tools judge no asker | `HelperDesk.cs:86`; `HelperBox.cs:127-128`; `QuestionBox.cs:139`; `HandOverAskBox.cs:102-103`; `Conversations/Domain/HandOverAsks.cs:118`; `Review/OpinionDesk.cs:110`; `Asks/Channel.cs:1164` | built |
| The connector records each call's lineage and passes every call on; deciding what a child may call is FABRIC-C2a's, held on its branch | `src/Daoris.Service/Daoris.Service.Mcp/CallLineageFilter.cs:8-20`; D173's FABRIC-ACP1 note | built (records only) |
| A hand-over: the brief composed, the old part ended, the new started in the same tree | `Conversations/HandOver/HandOver.cs:201-293` | built |
| A helper: a read-only one-turn session on another maker, in a clone at the asker's last commit, its answer the asker's next turn; two at work per asker | `Loop/DriverHelpers.cs:420-464`; `Conversations/Domain/HelperJudge.cs:41` (`PerAsker`); `HelperDesk.cs:85-178` | built |
| The helper's choice: a named agent the list does not hold falls back to the list; an empty list walks every installed agent of another maker | `Conversations/Helpers/HelperChoice.cs:13-27` | built |
| A helper's answer is deduplicated against words still waiting; the driver marks it handed after the call | `HelperDesk.cs:161-169`; `Loop/DriverHelpers.cs:385` | built |
| The reviewer's and helper's rules: edits and git's moving acts denied, acting tools denied | `Review/Domain/OpinionPermissions.cs:22-111` | built; posture unmeasured (XAGENT1j) |
| The second opinion's families: the last run's agent on each chain quest and the candidate's own record | `Loop/OpinionGateLook.cs:362-365` | built |
| A meeting's turn on a real agent is refused `no-message-tool` | `Loop/DriverMeetings.cs:316-322` | decided (AGENTROOM1b) |
| D188's levels, `said`, receipts; the answers-alone bound of two, counting go-ons only, an answer taken at a step counting nothing; solicited means a helper's or an opinion's | D188; `Conversations/Domain/AnswersAlone.cs:10-12, 21-24, 65` | built (COWORK1a-e) |
| `line_read` follows each record's `from`, refusing a record off the line | `src/Daoris.Service/Daoris.Service.Core/Sessions/LineEvents.cs:102-115, 147` | built |
| Only an ask has a channel; a message is for an entry's repository and never from the entry itself | `src/Daoris.Service/Daoris.Service.Mcp/ChannelTools.cs:15-30`; `Asks/Channel.cs:1044-1052` | built |
| A quest held in the hand-over store runs on the agent the store names, so an entry's parts may already run on two makers | `Loop/Driver.cs:831` | built |
| One account set per agent (D197); a resume stays on its record's account; the cap counts only what the driver starts | `Accounts/Harnesses/HarnessRoster.cs:860-975, 1041-1083`; D130:411 | built |
| Each part's tokens summed for the page | `SESSION_SPENT`, `Daoris.Desktop.Modules/Bridge/Routes/SessionsRoutes.cs:460` | built |
| A helper's files live under `data/helpers`; its routes are the driver's doors | `Platform/DaorisHome.cs:347`; `src/Daoris.Service/Daoris.Service.Http/PersonDoors.cs:217-218` | built |
| No test runs a real `claude`, `codex` or `dsh`; no Codex-shaped session stub; the kit's ACP stub declares only resume | `tools/rehearsal-kit.mjs:821`; `Driver.Tests/Loop/ChannelNextStepTickTests.cs:185-187` | built |
| The family rehearsal already declares a plugin's harness, `gate-agent`, over the ACP stub | `tools/family-rehearsal.mjs:6030`, driven at `:6054-6126` | built |
| `daoris plugin` is the driver program's whole; the CLI forwards | D194's PUSH1f2f note | built |
| Codex over ACP has never run a measured turn on a Daoris account; Claude Code's protocol door joined its connector calls across 147 sessions | D173's FABRIC-ACP1 note | measured for Claude Code only |

Driver paths are under `src/Daoris.Desktop/Daoris.Desktop.Driver/`, service paths under `src/Daoris.Service/Daoris.Service.Core/`.

### 0.3 What is missing

An entry's parts may already run on two makers, alongside each other in trees of their own, where a quest held for a hand-over names
its agent. What is missing is a governed way for another maker to do a bounded piece of the work a session asked for, return it to
that session, and have it land only through that session; one place that says what any provider can do; a reader that sees the work
in progress rather than the last commit; and the measurements that a writing partner of another maker would rest on.

## 1. The three proposals, and how each difference was settled

All three agree on more than they differ: one writer per tree; the second agent never writes the first's tree and nothing it makes
is imported as a patch; Daoris, never an agent's message, decides what makes a turn happen; D188's levels as they are; agreement
between agents establishes nothing, and no co-worker counts as D155's independent reader; a declaration of each provider's
capabilities that keeps *declared*, *negotiated* and *measured* apart; a family-rehearsal phase whose second maker is a plugin
declared by its manifest; real runs only as owner-permitted proofs, never a gate and never a sign-in; the dogfood through DOGFOOD1.
Where they differ, the steward chose (round 2's changes marked ²):

| # | Question | Claude | Codex | Fable | Settled |
|---|---|---|---|---|---|
| 1 | What working together means | a pair: a writer and a navigator who reads | a writer and a navigator who answers questions | a lead and partners who each write a piece | **Fable's**, with Codex's reading role kept as the helper (§2) |
| 2 | Where the second agent works | the lead's own tree, turns in sequence, a snapshot compared after each reading | a copy holding the uncommitted bytes | a tree of its own grown from the lead's tip | **Never in the lead's tree, nor in the repository's shared git state²**: a partner in a clone of its own with no remote, its piece imported by Daoris (Codex's read); a reader in a copy with the work in progress (Codex) |
| 3 | Its memory | one conversation kept for the pair's life | a fresh pass each time | one turn per piece | **Fresh**, per piece and per reading (Codex, Fable); a kept conversation is a later row |
| 4 | What starts its turn | Daoris, after each lead turn that changed something, within rounds the person set | a question, within a grant of two turns | the lead's or the person's ask, under the person's rule | **An ask under the rule** (Fable); the ask takes no `urgent`², so every answer is a go-on the count reads |
| 5 | Changing who leads | a swap at a round's end, both conversations kept | a hand-over, after the outgoing part's descendants end | the hand-over | **The hand-over** (Codex, Fable); Codex's barrier a row (COAGENT1g) |
| 6 | The cap | a paired conversation spends none | every participant, the person's conversation enrolled | a partner takes a slot as a helper does | **Fable's**: a partner carries `serves`, which `HoldsSlot` counts²; CAPCOUNT1 unchanged |
| 7 | Records and tools | `pair_note`, `pair_ask`, `pair_read`, a `pairs` field | `helper_ask` and `helper_answer`, a `CoWorkRun` record | `partner_ask`, `partner_answer`, a box, `serves` and `piece` | **Fable's, whole²**: the partner's record carries `serves` and `piece` and keeps its own entry, as a helper's does |
| 8 | Names | pair, writer, navigator | co-work, writer, navigator | lead, partner, piece | **Fable's** (§6.7) |
| 9 | The first row | the measurements and the declaration, then the pair | one vertical slice: Codex beside the work, reading a copy with the work in progress | the partner end to end | **Codex's²**: the reader of the work in progress first (small, safe, usable at once with Codex); the partner next, used on the install only once its confinement is measured |
| 10 | A limit on the second agent | carried on to another account, or readings paused | the pass ends and that account cools, no second try | carried on once to the next ready account | **Codex's²**: the piece ends `limited`, its work kept and handed; a new piece may be asked |
| 11 | The provider declaration | a `Capabilities` member, a manifest list, a measuring verb | a projected contract, a measurement record, a manifest object | a sheet derived from what exists, the manifest gaining two postures | **Fable's sheet**, its measurements a code table held by a census² and the version rule from Claude's and Codex's (§3) |
| 12 | D155's and D163's rejection of agents conversing with no person between | amended within the pair's rounds | narrowed by D188's solicited answer and a per-grant bound | stands; only D163's "no writing helpers in a first version" narrows | **Fable's**: the rejection stands (§4.3) |
| 13 | How a piece lands | (none) | (none) | through the lead's merge, and a piece never taken in replayed at the entry's landing | **Only through the lead's merge of the piece's frozen tip²**; every landing door refuses a partner's record (§2.4) |
| 14 | A channel between them | `pair_read` | none | the line's channel for an entry with no ask | **A later design²**: the lead and a partner share no entry, and a channel between them needs delivery by participant (Codex's read, finding 21) |

Why each:

1. **The partner serves the direction; the reader alone does not.** The owner asked for another maker working inside the session.
   A reader that advises is worth having, and is kept (§2.5), but it leaves every change of the work to one maker. A partner writes a
   piece the lead or the person gave it, and its work returns as a frozen commit the lead takes in.
2. **Prevention, all the way down.** Claude's shared tree was rejected in round 1: it amends the one-session-per-tree question every
   open relies on and detects a write only after it happened. Round 1 put the partner in a linked worktree; Codex's read showed that a
   linked worktree shares the repository's refs, configuration, hooks and objects (`SessionTrees.cs:369`), which is why the reviewer's
   copy is a clone (`OpinionTree.cs:12-16`), and Fable's read showed that Codex's sandbox likely cannot commit there at all, its git
   directory lying outside the writable root. A clone of its own, with no remote, holds the partner's whole git state inside the one
   folder its sandbox may write; Daoris reads the files it left, never running git there, and writes one commit into the repository
   itself (§7.9, round 3, after Codex's second read).
3. **A kept conversation waits for resume to be measured** (SESSCTX1a); it would also amend D189 point 6.
4. **No turns granted by Daoris between the two.** Claude's automatic reading spends the second maker's account after every changed
   turn with no ask. Codex's grant record bounds what the count bounds once every answer is a go-on, which round 2 makes true by giving
   `partner_ask` no `urgent` (Fable's read, F4): a piece's answer is a commit to merge, which the lead can only take in at a turn's end
   anyway, so an urgent level would buy nothing and escape the count.
5. **The hand-over exists and keeps D189.** Codex's barrier on the outgoing part's background work applies to every hand-over, so it
   is a row of its own (COAGENT1g).
6. **CAPCOUNT1 stays, and is kept by the record**: `HoldsSlot` already counts a record with `serves` (`Planner.cs:818-822`). Round 1's
   `piece` alone would have read as a free conversation (Codex's read, 12).
7. **The helper's precedent, whole.** Round 1 put the partner's record on the lead's entry with `piece` alone. Both reads showed the
   cost: the entry's Accept would land it (`EntryAccepts.cs:24-30`), a teammate's mirror would see a plain part of another maker since
   `entry` travels and `piece` would not, and every door that refuses a helper by `serves` would let a Codex partner through, a partner
   that takes no rules file (Fable F2; Codex 11). With `serves` and `piece` and its own entry, every helper refusal applies with no new
   code, ACCEPTALL1 is not amended, an older build and a mirror see a lone chat, and `piece` is read only where a partner differs.
8. **Fable's names.** *Co-work* and *co-working* are D167's for sessions of several repositories on one ask. *Writer* and
   *navigator* name a pair that is not built.
9. **Small first, safe before it writes.** Codex's read showed that the writing slice owes its stand-ins, its admission, its record's
   classification and its confinement before its first use, and that a host half and a page half is no split (D187 point 5). The
   reader of the work in progress needs none of that: it is a helper whose copy is better, safe as helpers are today (a clone, nothing
   read back), and usable with Codex at once. It goes first; the partner follows, gated by M2 (§8.5).
10. **No repair in the first rows.** Codex's read showed a repair needs a second record, a durable deadline and the attempts' usage
    (16). With Daoris keeping a piece's work at every end, a limited piece loses nothing: it is handed as it stands, and the person or
    the lead may ask another.
11. **A sheet derived from what exists cannot drift from it**, and an installed driver has no `docs/` to read (Fable F6): measurements
    are a code table in the driver, each naming its evidence document, which a census test proves exists.
12. See §4.3.
13. **The lead decides what lands, by one door.** A piece reaches the line only through the lead's merge of a tip Daoris froze; the
    landing's shared eligibility refuses a partner's record at every door, not at the entry's Accept alone (Codex's read, 13 and 14).
14. **A channel between a lead and its partners is a design of its own.** A message is never for its own entry and marks are kept per
    entry (`Channel.cs:1044-1052`), so the channel's delivery cannot be reused unchanged between participants; Codex's read (21) asks for
    delivery, receipts and cursors by participant first.

**Taken from each beyond its shape.** From Claude's: the turn-end protocol stand-in shaped on what codex-acp declares (§8.1); each
answer naming the agent, account, version and model where the door reports one (§5); the maker's marks on the head; the measurements'
list (§8.5); the parent's own Codex reads of each merge going on through the dogfood. From Codex's: the reader's copy carrying the
uncommitted bytes with its manifest and refusals (§2.5); confinement measured before a writing start, never assumed (§3.3); the barrier
on the outgoing part's descendants (COAGENT1g); a restart marking an attempted turn uncertain, never spent again. From Fable's: the
shape, the rule, the tools, the box, the record, the facts read from git, the sheet, the rehearsal's second maker by file.

## 2. The roles, and how the work moves

### 2.1 Four roles, two of them new

| Role | 中文 | What it is | Writes | Exists |
|---|---|---|---|---|
| **Lead** | 主导 | the session that asked for a piece, and whatever continues it by `from` (a hand-over, a move, a carry-on) | its own tree | yes: any working session of this machine's |
| **Partner** | 搭档 | a record of its own serving the lead, on another maker's agent, writing one **piece** (分项) in a clone of its own, one turn | its own clone only | new |
| **Helper** | 助手 | a read-only one-turn session on another agent answering one question | nothing | yes (D163); its copy now carries the work in progress (§2.5) |
| **Reviewer** | 阅读者 | a second opinion before landing | nothing | yes (D155), unchanged |

A piece belongs to the record that asked for it, its **lead**, and passes along the lead's `from` chain: a hand-over, a move or a
carry-on of the lead takes its pieces with it. Being the entry's leading part on the page (`Trees/SessionEntries.cs:106-108`,
`leadingAgentOf`)
is a matter of display and confers nothing; since a partner keeps its own entry, it never becomes that part. An entry with parts
alongside each other has a lead per piece, the part that asked.

Each part stays one conversation on one record (D131, D189 point 1). There is no fused transcript and no shared harness context: the
shared context is Daoris's, the brief at a piece's start (D189 point 4) and the lead's line through `line_read` (§7.3).

### 2.2 Who writes

Nothing writes the lead's tree but the lead. A partner writes a clone of its own, made by Daoris as the helper's copy is
(`OpinionTree`: a clone with no remote), under the repository's trees with a prefix of its own, `p-`, checked out at the exact commit
the piece was asked from. Its refs, configuration, hooks and objects are its own, inside the one folder its enforcement mechanism lets
it write. Daoris treats everything in that folder as data once the partner's turn began: it never runs git there again, and moves the
partner's work into the repository itself, by reading the files (§7.9). What a clone does not stop is a write by absolute path, a
`git push` to a local path, a link, or a call to Daoris's own local doors; those are stopped, if at all, by the agent's enforcement
mechanism, which is why a provider is a partner only where that mechanism was identified and measured in its effective configuration
(§3.3), and its process held in a strict container (§2.3 step 5).

### 2.3 A piece, end to end

1. **The ask.** The person presses *Ask another agent to work on a piece…* in the session's Pieces section or its head's ⋯, or runs
   `daoris-driver sessions partner <session> "<piece>" [--paths <p>…] --agent <name>`; from COAGENT1d, the lead calls `partner_ask`
   { piece (at most 4,000 characters), paths (optional, repository-relative), agent (optional, one word) }, with no level. The ask
   is written as `<home>/data/partners/<id>.ask.json` (one writer, never changed) with **the exact commit the lead's tree stood at**
   and the lead's uncommitted paths as Daoris read them then. It is refused in a sentence where the record can judge: an asker that is
   a partner, a helper, a reviewer, an intake, Ask Daoris's conversation or a brainstorm pass; a blank piece; a path outside the
   repository; a lead with no commit to grow from.
2. **The rule** (COAGENT1d). `partners` and `workspacePartners` in `driver.json`: `{ mode: ask | on | off, agents: […], minutes }`, a
   repository's whole over its workspace's. `ask` (the default): the lead's ask waits for the person's yes, which names the agent it
   resolved to before the yes; the person's own ask needs none, since it names its agent. `on`: the rule's **listed** agents only; an
   empty list under `on` admits nothing. `off`: refused with its code. A named agent the list does not hold is refused, never
   substituted (Codex's read, 7). At most two pieces open per lead, counted from the boxes from `approved` on, not from running records,
   so two looks cannot both pass the second; a partner asks for no partner, helper or opinion (depth one).
3. **The choice.** The walk is the helpers' (`ReviewerChoice.ChooseAsync`, each account by its codes) over the agents §2.3 step 2
   allows, another maker first; the lead's own maker only where named, then said *the same agent, fresh*. The agent must be admitted as a
   partner by its sheet (§3.3); otherwise refused with the sheet's code (`no-connector`, `not-measured`).
4. **The reservation.** Before anything is made, the look reserves the piece's slot under the machine's cap and its place in the
   lead's allowance in one step of the common admission every starter reads (§5), and writes the state `approved`. Two looks, or a look
   and a quest's start, never both take the last slot.
5. **The start.** The state `preparing` names the copy's folder before it is made; the copy is cloned and checked out at the ask's
   commit, refused if that commit is not in the repository (no fallback to another base: Codex's read, 15). The host opens the record
   (`POST /api/partners`, a driver door, idempotent by the piece's id: kind `chat`, `serves` = the lead, `piece` = the ask's id, its own
   entry), and the state `started` names it **before** the turn. The partner's process is started suspended in an empty job, joined, and
   only then let run, as a heavy run's gates are (`Agents/Consoles/ProcessJob.cs:23-26`, HEAVYRUN1a), under its effective configuration
   (§3.3); a start that cannot join its job is refused, never run in the best-effort way an ordinary session's is (`ProcessJob.cs:20-21`).
   The partner runs one turn under the partner's role (§7.4) with its instruction: the piece as written, the paths, the commit it grew
   from, the lead's uncommitted paths at the ask and again at the start (so it leaves them alone), the lead's line's brief
   (`ContextBrief`), how to end (say what it did, what it did not, what it would need; its files are what Daoris takes), and that it
   asks nobody. Its bound is the rule's `minutes` (45 as a first value; the session timeout behind it); its end is what Daoris observes
   of its process (D46), never its words.
6. **Its end.** The turn's end is observed, then the job is ended and **waited empty** (`WaitEmptyAsync`): no process of the partner's,
   nor any child, can still write the copy. Only then is `ending` written, with the moment that dates the keeper's commit. Where the
   job's state cannot be known (a restart), nothing reads or removes the copy until the person's act (§7.2).
7. **The work kept and imported, in one step Daoris owns** (§7.9). Daoris reads the copy's files as bytes, never running git in the copy,
   judges them by the import's rules, and writes **one commit** onto the ask's base in the repository itself, through a temporary
   index with no filters and no hooks, as Daoris's own commits are made; then it creates `refs/daoris/pieces/<id>` at that commit, a
   create-only update, never moved after. The partner's own commits in its copy are not imported: their history, and anything it wrote
   and deleted on the way, never reaches the repository. The state `imported` names the commit. A refused import is `held`, the copy
   kept for the person's look and *Discard…*.
8. **The answer.** `partner_answer` { summary (at most 20,000 characters), left (what it could not do) }, once; else its last words,
   bounded as a helper's are. Daoris adds what it reads itself, never from the words: the commit, the files changed, those outside the
   piece's paths (*beyond its piece*), what the import refused or left out and why, the declared gates its tool calls ran and the newest
   failed one's kept output up to 2,000 characters (D189's check rule). The state `answered`. The copy is then removed, a cleanup tried
   again until done (§7.2).
9. **The delivery.** Through `POST /api/partners/{partner}/hand` (a driver door), a said word marked `partner` (beside `helper`), with the
   piece's stable key, in fixed words (`PartnerWords`, no model): who answered on which account, the piece, the facts, the summary quoted
   as the partner's claim, and the one line that takes it in: *in your tree: `git merge --no-ff refs/daoris/pieces/<id>`*. Its
   destination is claimed on the piece's delivery receipt before anything is handed: the lead, or the newest live record continuing the
   lead by `from`, the claim moved along `from` explicitly; never a left conversation (D189 point 6); with none live, it waits on the
   newest record of that chain for its next part. It goes at the turn's end as an ordinary solicited answer, may reopen an ended lead
   once, and counts toward the answers-alone bound (§4.2). An attempt left uncertain by a restart waits for the person and is never
   handed again by itself (§7.6). The state `handed`.
10. **Taking it in.** The lead merges the frozen commit in its own tree, in its own turn. A piece is **taken in** when Daoris first reads
    the commit as an ancestor of the lead's branch (`merge-base --is-ancestor`), and that is recorded with the lead's commit that took it
    and never unset: a later rebase, a squash landing or a revert is the lead's own work on what it took in, and Daoris's replays and
    squashes carry the piece's provenance on their records (§7.7). A cherry-pick or a part of it is not taken in, and the page says so
    (Codex's read, 14).
11. **Other ends.** The person's *Stop…* ends the partner's turn and goes on from step 6 with the end `stopped`; past its minutes, the
    end is `out-of-time`; an account's limit met, `limited` (the account cools, D125). Each keeps and imports the work as above, and an
    end whose import holds work is handed as the piece's result (a solicited answer, step 9). An end with no work, a refusal and a
    discard are **notes** on the lead's record and the Pieces section, never words: they reach no prompt, reopen nothing and reset no
    count (Codex's reads, 19 and 32).

### 2.4 Landing, and the doors that must refuse it

A piece reaches the line **only through its lead**: its tip, merged by the lead, lands with the lead's work. A partner's record keeps its
own entry, so an entry's Accept never lands it (`SessionEntries.cs:76-77`; `EntryAccepts.cs:24-30`); and the landing's shared
eligibility refuses any record carrying `piece` at every door: the session's own *Accept…* and `LAND_SESSION_TREE`
(`Daoris.Desktop.Modules/Bridge/Routes/TreesRoutes.cs:626`, `Trees/SessionGroups.cs:1124`), the terminal's `trees land`, Ask Daoris's
card and the landing at done. A piece never taken in when its lead's work lands is *not taken in* (未并入): its ref stays until the
person's *Discard…*, which removes it in one transaction verified at the commit (`ref-removal-transactions.md`). Clearing a closed
quest's work clears its pieces (D153). Piece refs sit beside Daoris's recovery refs (`refs/daoris/discarded/`,
`Trees/SessionTrees/ContentHolds.cs:69`) and are classed as they are: no tidy, clear, sync or landing mutates one, and **no proof of
holding counts one as a holder** (`ContentHolds.AnyRef`, `:359`, and the strays' ref scan, `TreeStrays.cs:210`, exclude the prefix), since
a piece's commit contains the lead's history up to its base and would otherwise let a lead's branch be forgotten as held. A valid ref
keeps its objects through git's collection, which is what keeps the piece.

### 2.5 Reading the work in progress: the helper's copy

A helper's copy today is a clone at the asker's last commit (`Loop/DriverHelpers.cs:444`; `Review/OpinionTree.cs:66-67`), so a helper
on another maker never sees what the asker has not committed, which is where most of a turn's work is. From COAGENT1a the copy carries
it:

1. At the asker's turn boundary, with no turn in flight, Daoris captures the asker's tree: the bytes of every changed tracked file, the
   deletions and every untracked file not ignored, with a manifest (the commit, the paths, each file's hash, what was left out and why,
   when it was read). It changes neither the asker's index nor its refs.
2. It refuses an unresolved merge, a link that escapes the tree, a submodule, an unreadable file and a capture over a bound, each said;
   ignored files are never carried.
3. It reads the asker's state again after the capture, and a capture that changed under it is thrown away and taken again once.
4. The clone is made as today and the captured bytes laid over it; nothing is read back from it, as today.
5. The helper's answer names its capture, and the delivery says the work may have moved since.

This is how another maker reads alongside, a navigator in the proposals' words, with no third mechanism. The opinion's copy is
unchanged: a reviewer reads a candidate's commits, never the work in progress (D155 point 6).

### 2.6 Changing who leads

The hand-over as built (D163, MIXAGENT1c, D189's brief and closing note): the old part ends at its turn's end, the new starts in the
same tree on the other maker, handed the brief, and takes the old part's pieces along the `from` chain. Going back is another
hand-over. A partner never becomes the lead and is never handed the lead's tree.

## 3. The provider contract: one sheet per agent

### 3.1 Sources and states

The interface is the adapter seam as it stands (`ISessionAdapter`; `HarnessToolchain`; a plugin's `PluginHarness` built as
`DeclaredAcpAdapter`), what the protocol door negotiates at `initialize`, and the connector every part is handed. COAGENT1 adds one
**sheet** per agent, `AgentCapabilities`, which the driver derives. Each row has a **source**:

- **declared**: the adapter's own word in Daoris's code, or the manifest's for a plugin's harness;
- **negotiated**: the wire answered it at the agent's last start; the roster keeps that reading per agent and version as it keeps a
  usage reading, the one place the sheet is kept apart from its sources;
- **measured**: a row of the driver's measurements table (agent, harness version, adapter version, posture, capability, outcome, the
  evidence document's path), which a census test holds to documents that exist in the repository; a different version or posture
  reads *declared* until measured again;
- **absent**: nobody declared, negotiated or measured it, and its fallback applies; silence never reads as support.

Safety rests only on a copy or clone of its own and on measured rows; a declared row may enable a convenience, never a guarantee.

### 3.2 The rows

| Capability | Known by | Claude Code (ACP) | Claude Code (pipe) | Codex (ACP) | A plugin's harness | Fallback where absent |
|---|---|---|---|---|---|---|
| Door | declared (`Wire`) | protocol | pipe | protocol | protocol | none: without a door it is no provider |
| Conversation | declared (`Interactive`) | yes | yes | yes | yes | driven one-turn parts only: a partner, never a conversation's lead |
| Turn's end observed | protocol by contract; pipe by `StructuredOutput` | yes | yes | yes | yes | the record moves on exit and quest state alone (D46) |
| Resume | negotiated (`session/resume`, `session/load`) or declared (`PrepareResume`) | negotiated | declared, never run here | negotiated; `elsewhere` on another client | negotiated | a new part handed the brief (D189) |
| Words at the next step | negotiated (`promptQueueing`) | yes | no | no, until STEER3 | as negotiated | words wait for the turn's end, said so |
| Steering (*now*) | negotiated, and measured (STEER evidence) | measured | no | offered, not measured | as negotiated | *now* waits, said `no-interrupt` |
| A stop that keeps the conversation | declared (`FrameInterrupt`) or `session/cancel` | yes | a conversation's only | yes | yes | *Stop* ends the part |
| Postures | declared, in the harness's own words | `auto`/`acceptEdits`, `default`, `bypassPermissions` | `acceptEdits` driven, `default`, `bypassPermissions` | `agent`, `read-only`, `agent-full-access` | `posture`; from COAGENT1e also `fullPosture`, `askingPosture` | a full or asking start is refused naming the agent |
| Takes Daoris's rules | declared (`TakesSettings`) | yes | yes | no | no | the copy is the floor; the page says the person's denies do not reach it |
| A held request reaches the person | protocol, under the asking posture | yes | no | yes | from COAGENT1e, where it declares `askingPosture` | refused under any other posture |
| The connector | protocol on the wire; pipe by `--mcp-config` and the repository's `.mcp.json` | yes | yes | yes | yes | never a partner (`no-connector`) |
| Its own connector calls joined to its record | by its binding (`IntakeScope.Session`) on every door | yes | yes | yes | yes | — |
| Native children's lineage | measured (FABRIC-ACP1; D173's audit) | measured | not measured | not measured (M1) | not measured | recorded and passed on (C2a's guard is not installed); a partner's binding bounds what any child of it may call |
| Partner confinement | measured only (M2) | not measured | not measured | not measured | not measured | not admitted as a partner (`not-measured`) |
| Instruction files | measured per harness (the entry-point evidence) | `CLAUDE.md`, else `AGENTS.md` | the same | `AGENTS.md`, 32,768 bytes for the chain | unknown | the role's instruction carries what the part needs |
| Turn counts | read from the turn's answer; their meaning declared (`TurnReport`) or measured | per turn | per turn | read where reported, meaning not measured (M4) | as reported, meaning unknown | *not measured*, never zero (D57) |
| Account windows | declared (`Usage`, `Limits`) | asked and forwarded | asked | asked of its server between turns | none | the walk has no reading for it |
| Accounts | declared on the toolchain | `claude-code`'s set | `claude-code`'s set | `codex`'s set | `accountOf`, `profileVariable` | its own home: no walk, no cool-off, a limit reads as a failure |
| Model and effort | declared (settings file) or negotiated (`configOptions`) | both | the settings file | `configOptions` | `configOptions` | the provider's default, said as unknown |
| Maker and product | declared (`Maker`, `Product`) | Anthropic, Claude Code | the same | OpenAI, Codex | `maker`, `product` | never another maker (`maker-not-declared`) |

### 3.3 What each role needs

- **A lead of a driven session**: what a lone session needs today. **A lead of a conversation**: a conversation.
- **A partner**: a door with a turn's end, the connector, a declared maker, and **partner confinement measured** (Codex's reads, 10 and
  22). Confinement is not something an agent shows by declining to escape: it rests on an **identified enforcement mechanism** (for
  Codex, its sandbox; for an agent that has none, nothing), measured in its **effective configuration**: the harness and adapter
  versions, the posture, the writable roots Daoris hands it (the copy alone), its network and loopback reach, the operating system, and
  the digest of the settings Daoris passes. A start whose effective configuration differs from every measured row is refused
  `not-measured`, at every start, the person's own included. Without the connector, `no-connector`. A stand-in's rows exist for its
  tests and never admit a production start.
- **What the measurement must show** (M2, §8.5): through the mechanism itself, with deterministic probes and no agent in the loop where
  the mechanism can run a command (Codex's sandbox runs a probe script), that a write outside the copy by absolute path, through a link
  and transiently, a `git push` to a local path, a write under the home, an HTTP call to Daoris's local host and a connection to the
  shell's pipe are each refused; the agent-directed run is supplementary evidence beside it. An agent with no identified mechanism is
  not admitted as a partner, whatever its rules say.
- **A helper or a reviewer**: as today; its copy is the floor (D155 point 5).
- **Words sooner** than a turn's end: words at the next step, negotiated; *now*: steering, measured.

### 3.4 What a plugin's manifest may say

It gains `fullPosture` and `askingPosture`, in the harness's own words (today both are refused for every plugin's harness,
`Adapters.cs:1266-1267, 1279-1280`), read by the driver's manifest reader (`PluginManifest`) and its fixtures, since `daoris plugin` is
the driver program's (D194's PUSH1f2f note). It may not claim words at the next step, steering or resume (the wire answers those), nor
rules, limit words, lineage or confinement (claims about another program that only a measurement with a recorded sentence may make,
D125, D53). A later maker over ACP joins with a manifest and a measurement; a maker over another transport needs a door, never a branch
on its name in the partner's code.

### 3.5 Measurements

A measurement is a proof run by a person on a machine set up for it, in `tools/proof-kit.mjs`'s shape (`:1-8`: a proof is not a gate,
it spends a login). Its evidence document names the harness and adapter versions, the posture, the scenario, the frames observed and
the date, and its outcome becomes a row of the driver's measurements table in the same change. No measuring verb is built now.

## 4. How the agents talk, under D188

### 4.1 The paths

Only through Daoris's connector and the co-working skill (D188's owner's note; D177 point 1): `partner_ask` and `partner_answer`,
`helper_ask` and `helper_answer`, `line_read`, and quests where a lead's own role allows them. Never a harness's own channel, never
another agent's process, and authorship always from the binding, never from what a message says.

### 4.2 The levels

A partner's answer is a solicited answer, and always ordinary: `partner_ask` takes no level, since a piece's answer is a commit the lead
can only take in at a turn's end. It may reopen an ended lead once per ask; it is therefore always a go-on, which the answers-alone
count reads (`AnswersAlone.cs:10-12`), and its mark joins the helper's and the opinion's as solicited (`AnswersAlone.cs:24`). A piece
that ends with no work, is refused or is discarded leaves a **note**, never a word: a Daoris word would reset the count
(`AnswersAlone.cs:86-92`) and, beside an answer, would let a prompt pass the bound's hold (`:28-31`), so a note reaches no prompt and
resets nothing (Codex's read, 32). Nobody but the person sends *now*, and no agent stops another's turn.

### 4.3 D155's and D163's rejection of agents conversing with no person between stands

It stands as a rule. What it guarded against was a loop with nobody in it: accounts spent without the person, and an agreement that
establishes nothing. Neither can form here:

- the only message that moves a run is a piece's answer, which the person's rule allowed (`ask` by default: each piece is the person's
  yes, naming its agent), from an agent the rule listed;
- a partner cannot ask back, nor for a partner, a helper, an opinion, the person or a quest: its role is refused at each of those doors
  by the server, from its binding, with no rule the agent must honour (§7.4);
- every answer is a go-on, and a line goes on by answers alone at most twice in a row, the third waiting for the person (COWORK1e,
  unchanged); at most two pieces are open per lead;
- what returns is a frozen commit the lead must take in itself, with the facts Daoris read from git beside the partner's words;
- agreement still establishes nothing: a defect is a gate's verdict (D155 point 8), and neither the lead's merge nor the partner's
  summary is a check; a partner whose piece the candidate holds is one of the work's families for its second opinion (§7.7).

What narrows is D163's rejection of **writing helpers in a first version**, which the mixed-agents design left out because their work
needed a landing design. §2.3 and §2.4 are that landing.

## 5. Accounts and usage, under D197

- **Each part runs on one account of its agent's one set** (D197 point 1). The lead's is its own; a partner's is the walk's for the
  partner's agent, over the agents its rule allows, with cool-offs, sign-ins, the order and the use settings. The person's own sign-in is
  never a partner's account and is never asked (D197 point 3).
- **A limit met mid-piece** cools the account, never a strike (D125); the piece ends `limited`, its work kept, imported and handed.
  There is no automatic repair in these rows: the lead or the person may ask a new piece from its tip.
- **The cap** (D130, CAPCOUNT1): a partner's record carries `serves`, which `HoldsSlot` counts (`Planner.cs:818-822`). Before its record
  exists, its reservation counts instead: the shared count every starter reads (`Planner.cs:826`, `Held`) gains the reservations of
  pieces in `approved` and `preparing`, so a quest, a helper and a piece never take one slot twice; a reservation becomes its record's
  slot when the record opens, counted once, and is released at every failure and end (Codex's read, 29). A full cap waits, said. A
  conversation lead still holds none. A lead's two open pieces are counted across its `from` chain, so a hand-over does not reset them.
- **Usage is each part's**, summed by `SessionEvents.Spent` and answered by `SESSION_SPENT`; the lead's head sums its partners' per
  maker. A count no turn said stays unknown, never zero (D57); Codex's counts are shown as reported, their meaning *not measured* until
  M4; its account's windows still come from its server between turns (`HarnessRoster.AskUsageAsync`).
- **The tier that ran**: the record names the agent, account and harness version, never a model (`Sessions.cs:63, 80-88`); a piece's
  answer and a helper's say the agent, the account's name and the version, and the model where the door reported one (the pipe door's
  `result`, `Agents/Streams/StructuredOutput.cs:146-166`). Where no agent could take a piece, it says so and nothing claims the piece was
  worked.
- **Nothing spends unasked on the install.** The person's own press names its agent; under `ask` every lead's piece is the person's yes;
  the first `on` for this repository, with Codex listed, is the owner's yes to Codex's account being spent on pieces (D191 point 3).

## 6. What the person sees and presses

### 6.1 The session's page

| Where | English | 中文 |
|---|---|---|
| The head, where pieces exist | *Partners: Codex, 1 piece working* | *搭档：Codex，1 个分项进行中* |
| The head's spend, per maker | *Spent: Claude Code 41k · Codex 12k (meaning not measured)* | *用量：Claude Code 41k · Codex 12k（含义尚未测量）* |
| The head's ⋯ and the Pieces section | *Ask another agent to work on a piece…* | *请另一个智能体做一个分项…* |
| The conversation, a piece's answer | *A partner's answer: claims to check* | *搭档的答复：需要核实的说法* |
| A partner's own page, its title | *Partner* | *搭档* |
| A partner's own page, under its lead | *Working for {{title}}* | *为 {{title}} 工作* |

Each maker is marked by its two letters (*CC*, *Cx*, a plugin's declared mark), never a hue alone. A partner's page shows its piece's
diff against its base, read from the frozen ref; it offers no *Accept…*.

**The Pieces section** (分项), under the head, lists each piece with its state and acts:

| State | English | 中文 | Acts |
|---|---|---|---|
| asked, waiting for the person | *Waiting for you* | *等你处理* | *Let {{agent}} work* 让 {{agent}} 做 · *Not now* 暂不 |
| preparing, working | *Working* | *进行中* | *Open its page* 打开它的页面 · *Stop…* 停止… |
| answered, handed | *Answered* | *已答复* | the tip, the files, *{{n}} files beyond its piece* 超出分项的文件 {{n}} 个, *Discard…* 丢弃… |
| taken in | *Taken in* | *已并入* | — |
| not taken in, at the landing | *Not taken in* | *未并入* | *Discard…* 丢弃… |
| stopped, out of time, limited | *Stopped* · *Out of time* · *Account limited* | *已停止* · *超时* · *账户受限* | the work kept, *Discard…* 丢弃… |
| held, its import refused | *Held: its work was not taken into the repository* | *已搁置：它的工作未导入仓库* | what was refused and why, *Discard…* 丢弃… |
| refused | *Refused* | *已拒绝* | why, in words |
| uncertain, after a restart | *Uncertain: Daoris restarted while it worked* | *不确定：工作时 Daoris 重启了* | *Hand over what it left* 交付它留下的 · *Discard…* 丢弃… |
| discarded | *Discarded* | *已丢弃* | — |

### 6.2 What needs you

From COAGENT1d: *A piece waits for your yes* (*有一个分项等你同意*), with *Let {{agent}} work* and *Not now*, judged in .NET and counted
by the bell; and from COAGENT1b, *A piece is uncertain* (*有一个分项状态不确定*) with its two acts; each in every place `person-waits.md`
lists. A piece's answer held by the answers-alone bound uses the built *Go on with this answer* row.

### 6.3 Setup and the Agents page

*Partners* (搭档) beside *Helpers* on a repository's Setup and a workspace's Defaults: the mode (*Ask me* / *On* / *Off*), the agents,
the minutes; the terminal twin `daoris driver partners`; Ask Daoris's `setting` door `partners`, each in every place
`setting-doors.md` lists. Each agent's card gains *In a shared session* (*共同工作时*): the sheet's rows in plain words with their
source, *takes words mid-turn (its own word)* (*可在回合中接收消息（它自己声明）*), *steers (measured {{date}})*
(*可转向（{{date}} 测量）*), *works as a partner (measured {{date}})* (*可作为搭档（{{date}} 测量）*), *not measured* (*尚未测量*).

### 6.4 The terminal

`daoris-driver sessions partner <session> "<piece>" [--paths …] --agent <name>` asks one; `sessions partner <session> --list` shows the
pieces; `sessions partner <piece> --stop | --discard | --hand-over` acts on one. Each is a person's door, reaching the running Daoris as
the sessions family's verbs do (D194), with its confirmation words on the pipe; its answer names the piece and its state.

### 6.5 Ask Daoris

It may propose a piece on a session (a `session` card) and the `partners` setting, with the terminal's equivalent; the person applies.
It never asks for a piece by itself (D110, D183).

### 6.6 The names, for the glossary

| Term | English | 中文 | Why |
|---|---|---|---|
| lead | lead | 主导 | the session that asked for a piece, and whatever continues it |
| partner | partner | 搭档 | a working partner, apart from 助手 (reads only) and 队友 (a person on another machine) |
| piece | piece | 分项 | one item of the work given to a partner |
| maker | maker | 厂商 | already the page's word (`opinion.json`) |

Avoided: *co-work* and *co-working* (D167's), *driver* (Daoris's program), *review* and *reviewed* (the person's), *room* (a pass's
sandbox), *meeting* (D177's), *writer* and *navigator* (a pair not built).

## 7. Records and their places

Each addition goes into every place its knowledge document lists, in the same change, with the cross-lane tests those documents name.

### 7.1 The box

Under `<home>/data/partners/` (a home entry `partners` in both runtimes' `DaorisHome`, beside `helpers`; in the home inventory, the
history's accounting, a deletion's and a clear's ownership and the kept-names catalogue): `<id>.ask.json` (id, asker, piece, paths,
agent, base commit, uncommitted paths at the ask, askedBy, at; written once), `<id>.state.json` (the driver's alone: the state of §7.2,
its code, the copy's folder, the partner's record, the job's identity, the keeper's moment, the imported commit, whether the copy is
removed, at), `<id>.answer.json` (summary, left, at), `<id>.facts.json` (commit, files, beyond, refused and left out, checks) and
`<id>.delivery.json` (§7.6). Each is written whole, to a temporary name and renamed. The service's `PartnerBox` and the driver's reader
share no code and hold one table (`twins.md`).

### 7.2 The states, and how a restart reconciles each

A restart never trusts the last state alone: it reconciles what the state says with what it observes, by the stable identity of each
side effect (Codex's read, 25). A side effect is always made before the state that names it, and is idempotent by its identity, so a
crash between the two is repaired by observing and writing the state, never by doing it twice.

| State | Side effect and its identity | A restart observes, then |
|---|---|---|
| `asked`, `waiting` | the ask's file | judges it at the next look |
| `approved` | the reservation, in the common admission | prepares |
| `preparing` | the copy at its named folder | a partial copy is removed and prepared again (Daoris's work, no agent's) |
| (opening) | the record, by the piece's id (the open is idempotent by it) | a record already open for the piece is taken as this one; `started` is written |
| `started` | the job holding the partner | the job's state is known and empty: `ending`; unknown or not empty: **`uncertain`**, the copy kept and untouched |
| `ending` | the keeper's moment, written before keeping | keeps: the commit is deterministic from the base, the files and that moment, so a retry makes the same commit |
| (import) | `refs/daoris/pieces/<id>`, a create-only update | the ref at the keeper's commit: `imported`; at another commit: `uncertain` |
| `held` | the import refused; the copy kept | stays for the person |
| `imported` | the ref; the copy's removal a cleanup tried until done | reads the facts, then `answered` |
| `answered` | the answer's and the facts' files | hands, by the delivery receipt (§7.6) |
| `handed`, `taken-in`, `not-taken-in`, `discarded`, `refused` | as §2.3 and §2.4 say | nothing |
| `uncertain` | — | the person's acts: *Hand over what it left* (ends the job, keeps, imports, hands: Daoris's work) or *Discard…* |

A state write that fails (a full disk) leaves the previous state, which the next look reconciles as above; an unreadable or absent state
file is `uncertain`, naming the file. The end's cause (`answered`, `stopped`, `out-of-time`, `limited`) is kept beside `ending`. Nothing
at any state runs an agent again unasked.

The copy is owned by its box: the strays' cleanup leaves a `p-` folder whose box still holds it (`started`, `uncertain`, `held`, an
unfinished removal), as it leaves an opinion's and a replay's (`Trees/SessionTrees/TreeStrays.cs:72-77`), and never moves it as a pass's
leftover (`PassLeftovers`); the inventory lists it; only a verified import or the person's *Discard…* removes it (Codex's read, 26).

### 7.3 The record

The session record's `piece` (the ask's id) joins `serves` (the lead), machine-only as `opinion` and `serves` are, through every place
`session-record-fields.md` lists; the record keeps its own entry (`ownEntry`), as a helper's does. `piece` is read only where a partner
differs from a helper: its title and page, its answer door, the Pieces section, the head's count and spend, the landing's eligibility,
`line_read` and the opinion's families. On the page, `isHelper` means any record with `serves` (`src/Daoris.Web/src/work/identity.ts:79`):
the one-turn restrictions it carries stay shared, and the names, the rail's words and the head's facts read `piece` to say *partner*; the
rail already nests a serving record under its asker (`nestServing`, `src/Daoris.Web/src/work/groups.ts:304`), which is how the page shows
a partner as part of its lead's session. `line_read`, for a record with `serves` and `piece`, reads its lead's line read-only (both the
service's `LineEvents` and the driver's twin, with their tests), with no `from` link made, since the lead still runs.

### 7.4 The partner's role, enforced by the server

The partner's role is the driver's to bind: the driver starts the partner's connector with the session's scope in the environment it
sets (as `IntakeScope` is read today, `src/Daoris.Service/Daoris.Service.Core/Asks/IntakeScope.cs:22`), and the service, asked by that
scope, answers the record's `serves` and `piece`. The connector's host refuses, by that role and before the tool runs, every tool
outside the partner's allow-list: the knowledge and reading tools, `line_read` and `partner_answer`. A registration census fails on a
tool that is neither allowed nor refused for the role, so a tool added later is classified when it is added. The same refusal is the
existing one at each door that refuses a record with `serves`, and the quest tools gain it (a record with `serves` publishes, takes and
closes nothing). A native child inherits the environment, and with it the scope and the refusals; a connector started with the scope
stripped holds no session, and its acting tools refuse.

The local host's agent doors are not bound to a session: an agent may publish and respond to quests over HTTP with no person's key
(`src/Daoris.Service/Daoris.Service.Http/PersonDoors.cs:106-110, 179-183`; D156 point 3), and the shell's pipe and the terminal's verbs
answer a caller on the machine. For every session that is D156's residual; for a partner it is closed by admission instead: a partner is
admitted only where its enforcement mechanism was measured to refuse it the local host's address and the pipe (§3.3), so neither the
partner nor its children can reach those doors (Codex's read, 28). The partner's rules (`PartnerPermissions`, beside
`OpinionPermissions`), for an agent that takes them, deny the same tools by rule as a second layer, never the only one.

### 7.5 The doors

`POST /api/partners` and `POST /api/partners/{partner}/hand` are driver doors in `PersonDoors`, as the helper's are
(`PersonDoors.cs:217-218`); the person's presses are bridge presses in every place `bridge-presses.md` lists; the terminal's verbs are
the person's doors with their pipe words.

### 7.6 The word

A piece's answer is a said word marked `partner`, with the piece's key as its producer key, through the session's one dispatcher (kept,
attempted, went: `session-words.md`); the event that records it taken carries the key. Beside the dispatcher's own record of a word,
which is per record (`Sessions/SessionLedger.cs:846`), the piece keeps **one delivery receipt** for the whole piece,
`<id>.delivery.json`: the destination claimed, the key, and the outcome, `waiting`, `attempted`, `uncertain` or `taken` (Codex's read,
31). The claim is written before the hand, and moved along `from` explicitly when the lead's successor takes over, in the same act that
releases the predecessor's; the hand door answers a key already waiting with that word and a key already taken with *handed*. An
attempt the come-up settles as uncertain (`Conversations/GoOn/UncertainWords.cs:64-72`) marks the receipt `uncertain`: it waits for the
person (*Hand it again*), and nothing hands it again by itself. Every reader of a solicited word (`AnswersAlone.Solicited`,
`SaidWordView.Persons`, `PriorSession`, the ledger's `WentOnWith`, the resumed turn's opening) reads `partner` as it reads `helper`.

### 7.7 The opinion's families

The second opinion's families (`OpinionGateLook.cs:362-365`) gain the agent of every partner whose piece was **taken in** on the
candidate's line, as recorded when it was first taken in (§2.3 step 10), whatever happened to its ancestry after: Daoris's replays
(`Trees/SessionTrees/BranchReplays.cs:160-165`, a `rebase --onto`) and squash landings (`TreeLands.cs`) record the pieces the rewritten
branch had taken in, so the provenance outlives the commit ids. Where a rewrite leaves it unclear, the maker is kept among the families:
a maker that contributed a piece is never the reader of the work it is in (Codex's reads, 3 and 30). A merge later reverted stays taken
in; the revert is the lead's work, shown in its diff.

### 7.8 Notes, the log, the sheet

Coded notes in both catalogues: on the lead `partner.asked`, `.started`, `.answered`, `.taken-in`, `.refused` (why), `.stopped`,
`.out-of-time`, `.limited`, `.uncertain`, `.beyond`, `.kept`; on the partner `partner.working`. The machine log: `partner.asked`,
`.started`, `.answered`, `.refused`, `.imported`, `.held`, the account by its profile name as `account.rotated`'s is.
`AgentCapabilities` is answered on `HARNESSES` and `agent list`.

### 7.9 The keeper and the import

Once the partner's job is empty (§2.3 step 6), its copy is data the partner controlled, `.git` included: hooks, filters, `core.*`
settings, signing and identity may all have been rewritten there. So Daoris never runs git in the copy again (Codex's read, 23):

- **It reads the files, not git.** Daoris walks the copy's folder as bytes, never entering `.git`, never following a link, and compares
  what it finds with the ask's base commit, read in the repository.
- **It judges them by the import's rules** (Codex's read, 24): a regular or executable file only; a new or changed link, a junction or
  another reparse point, a submodule or a nested repository refused; a file the repository's own ignore rules at the base ignore left
  out (judged in the repository, its own trusted configuration, never the copy's); one file over 10 MB, or the piece over 50 MB, refused
  (first values, read again in COAGENT1f). Paths outside the piece's paths are kept and named *beyond its piece*, never refused. What is
  refused or left out is listed in the facts. Where a refusal leaves nothing worth handing, the import is `held`: the copy is kept for
  the person to look at and discard.
- **It writes one commit in the repository**: the base's tree read into a temporary index (`GIT_INDEX_FILE` under the home), each kept
  file's bytes written with `hash-object -w --no-filters`, deletions taken out, `write-tree`, then `commit-tree` onto the base, which
  runs no hook and signs nothing; its author and committer are made as Daoris's own squash commits are, by the repository's git
  identity (`Trees/SessionTrees/TreeSquashes.cs:28-32`; D198 point 5 where a git identity of Daoris's is set), never the copy's; its
  date is the keeper's moment, its message names the piece, the partner's agent and account. The same inputs make
  the same commit, which is what lets a restart repeat it (§7.2).
- **It imports nothing else.** The partner's own commits, branches and objects stay in the copy and go with it: anything the partner
  wrote and deleted on the way, a secret among them, never reaches the repository's history. Daoris claims no secret detection of its
  own: the lead's merge and the person's look see the commit's diff, and a gate the repository declares may scan it.
- **What the person reads outlives the copy.** The partner's page reads its diff from the frozen ref against its base.

## 8. The test plan

### 8.1 The stand-ins

- **A turn-end protocol stand-in shaped on codex-acp**: no `promptQueueing`, the steering extension offered and unused, the modes
  `agent`, `read-only` and `agent-full-access`, a permission request before each write in `read-only`, no turn counts. It sits in
  `tools/rehearsal-kit.mjs` beside `ACP_STUB_AGENT`, and as a scripted in-memory agent for the fast half, with COAGENT1b.
- **`ACP_STUB_AGENT` gains a switch** (`DAORIS_STUB_QUEUEING`) to declare `promptQueueing` (`tools/rehearsal-kit.mjs:821`), with
  COAGENT1b, whose `Process` proof needs a lead that queues and one that does not (Codex's read, 33).
- **A second maker by file**: a test plugin manifest whose harness names `maker: "Stub Two"` and `product: "Stub B"` over the same stub
  script, as the rehearsal's `gate-agent` is declared (`tools/family-rehearsal.mjs:6030`), with stand-in rows in the measurements table
  for its tests only, with COAGENT1b.

### 8.2 The fast half

- **COAGENT1a**: `HelperCaptureTests` (deletions, untracked and binary files, an escaping link, an unresolved merge and a capture over its
  bound each refused, a capture that changed under it taken again, the manifest, the answer naming its capture).
- **COAGENT1b on**: `PartnerAsksTests` and `PartnerJudgeTests` (each refused asker, a blank piece, a path outside, a lead with no commit, an
  unlisted named agent refused, two open counted from `approved` with two overlapping looks), `PartnerChoiceTests` (the allowed agents
  only, another maker first, the same maker only when named, `not-measured` for an effective configuration no row holds, a stand-in's row
  never admitting a production start, and `no-connector`), `PartnerPassTests` (the copy at the exact base and no fallback, the state
  written after each side effect, the role's allow-list, the instruction's golden under `golden/partner-prompt/`), `PartnerKeepTests`
  (the copy's hooks, filters, `core.*` settings and identity rewritten and never run or read; a link, a junction, a submodule and a
  nested repository refused; an ignored file left out by the repository's rules; a file and a piece over their bounds; deletions; the
  same inputs making the same commit; a commit in the copy never imported), `PartnerFactsTests`, `PartnerStatesTests` (an interruption
  between every side effect and the state that names it, a failed state write, an unreadable file, a job whose state is unknown, `held`,
  `uncertain` and both acts), `PartnerAdmissionTests` (a reservation counted with active records, a quest's start and a piece's look
  interleaved at the last slot, release at each failure, two open across a hand-over), `PartnerDeliveryTests` (the receipt's claim
  before the hand, moved along `from` with a successor taking over between keeping and acknowledging, never into a left conversation,
  an uncertain attempt waiting for the person, the key's idempotence with a crash after the take), `AnswersAloneTests` with a partner's
  answer and with an answer beside a refusal, an empty end and a timeout (none resetting the count), `OpinionGateLookTests` with a
  piece taken in and then replayed and squashed, `HoldsSlot` for a partner's record, the landing's eligibility at each door refusing a
  partner's record, `ContentHoldsTests` never counting a piece ref as a holder, `TreeStraysTests` leaving a held copy, `NoteCodesTests`;
  the service's `PartnerBoxShapeTests`, `McpPartnerToolTests`, the role's allow-list and its registration census, `LineEventsTests` for a
  partner reading its lead's line, `SessionLedgerTests` for `piece` and the own entry, the quest tools refusing `serves`; the modules'
  `DriverModulePartnerTests`, `StatePublisherTests`, `PipeCensusTests`, the home's and the doors' censuses; the web's `Pieces`, `headFacts`
  and `attention` tests in English and Chinese and the stories; `HelpCoverageTests` rows; `i18n:check`; `names:check -- --strict`.
- **COAGENT1e**: `AgentCapabilitiesTests` (each row's source and fallback per adapter, a version or posture change turning *measured* into
  *declared*, the measurements table's census over its documents, the manifest's two postures).

Every wait a test gives a real loop reads a held clock (`held-clocks.md`).

### 8.3 Real processes with stand-ins

`HelperCaptureProcessTests` (git half): a helper's copy holds the asker's uncommitted bytes and nothing returns from it.
`PartnerProcessTests` (`Process` half): the loop's real tick with a lead on the ACP stub and the partner on the second maker's stub in its
clone, writing files and answering; the answer at the lead's turn's end on the queueing stub and on the turn-end stand-in; the partner
started suspended in its job and a start refused where the job cannot be joined; a stalled partner cut at its minutes with its work kept;
a stop; a partner whose background command outlives its turn, the keeping waiting for the job to be empty; restarts around the start, the
end, the keeping, the import and the answer's write. `PartnerLandingTests` (git half): the lead merges the frozen commit and the lead's
work lands with it once; a cherry-pick read as not taken in; a piece taken in, then the lead's branch replayed and squashed by Daoris,
still taken in with its provenance; a piece never taken in left out and its ref kept through tidy, clear, sync, a landing and `git gc`;
*Discard…* removing the ref in one verified transaction; no landing door landing a partner's record. `PartnerChatProcessTests` (COAGENT1c): a
conversation lead hears the answer as a turn of its own between turns.

### 8.4 The family rehearsal (COAGENT1e)

One phase, run serially by the parent, on an isolated home with the rehearsal's guards: the kit's stub as the lead, declaring queueing,
and the same script declared a second time by a rehearsal plugin manifest as `stub-b` of maker *Stub Two*, joining by a file with no
Daoris code; an ask, its quest, the lead's take, `partner_ask`, the partner's clone, its commit, its answer at the lead's turn's end, the
lead's merge of the frozen tip, done, the entry landed holding both makers' commits; a second piece never taken in, left out of the
landing and its ref still standing. The transcript and the box's files are kept as evidence.

### 8.5 Runs on the install (each the owner's yes, D191 point 3; proofs, never gates, never a sign-in)

- **With COAGENT1a**: Codex as the helper of a Claude Code session, reading its work in progress. It carries **M1**, Codex's connector
  calls joined to their session (`helper_answer` read beside its lineage line, FABRIC-ACP1's open half), and **M4**, what codex-acp
  reports of a turn's counts and what they mean.
- **Before COAGENT1b is used on the install**: **M2**, partner confinement, in a disposable repository the proof makes (never this
  repository's own checkout, Fable's read F3): the partner's clone, a sibling folder standing in for the lead's tree, a local bare
  repository standing in for a remote, a marker folder under a scratch home, and a listener standing in for the local host. For each
  agent with an identified enforcement mechanism (Codex's sandbox; Claude Code only if one is identified), the proof first runs
  **deterministic probes through the mechanism**, in the effective configuration a partner start would hand it, with no agent in the
  loop: a write by absolute path into the sibling folder, through a link, and a transient one restored after; a `git push` to the bare
  repository's local path; a write under the home; an HTTP call to the listener; a connection to a named pipe. Each must be refused, and
  the proof hashes the sibling folder, the bare repository, the marker folder and the source repository's refs, configuration and hooks
  before and after (Codex's read, 22). Then, as supplementary evidence, the agent itself is asked a piece that tries the same. Each
  outcome becomes a row of the measurements table naming its effective configuration; any escape leaves the agent `not-measured`, filed
  as a row.
- **Then, with COAGENT1b**: the first real piece on this repository, Claude Code leading and Codex the partner, carrying **M3**, the cost
  per maker (the brief's characters, each part's counts, the lead's wait, the files beyond the piece, how much Daoris kept).
- **COAGENT1f**: the reverse, Codex leading with a partner of another maker where one is admitted (whether Codex can commit a merge in
  its own linked worktree is a measurement of its own here); a Codex conversation resumed after its process ended (SESSCTX1a's Codex
  half); Codex's `turn/steer` (STEER3) where ready.

Each measurement is an evidence document, a row of the measurements table where it states a capability, and a dated note on D199.

### 8.6 The build dogfoods it

COAGENT1a and 1b are built by branch-workers. From COAGENT1d, once M2 holds and the owner says yes, this repository's `partners` rule is
`on` with `agents: [codex-acp]`, and each later COAGENT row is filed as an ask to the install (DOGFOOD1): the Claude Code lead gives
Codex pieces that split cleanly (a terminal twin, catalogue rows and their tests, a `Process` test class). The parent lands each branch
through `tools/merge-branch.mjs`, files what blocked or misled a partner as a row or a fix-log entry, and records per row the pieces
asked, taken in and refused, the files beyond, Daoris's keeping, and the cost per maker. The parent's own Codex reads of each merge go
on: a partner is never the merge's second reader. Installed-window proof stays distinct from a story or a mocked bridge.

## 9. The rows

Each is one feature a person can see working (D187), split, if ever, only by sub-feature (D187 point 5).

1. **COAGENT1a — another maker reads the work in progress** (driver, modules). §2.5's capture for every helper's copy; the helper's
   answer naming its capture; the press that exists (*Ask another agent…*, MIXAGENT2b) now reads uncommitted work. Proof: §8.2 and
   §8.3's capture cases; on the install, Codex reading a Claude Code session's work in progress, carrying M1 and M4. **The first usable
   on the install**: small, safe as helpers are, and Codex at work beside a session at once.
2. **COAGENT1b — a partner writes one piece for a driven session, asked by the person** (service, driver, modules, web-shell; after 1a).
   §2.3 for a driven lead, by the Pieces section's press and the terminal verb: the box, its states and their reconciliation, the
   reservation in the common admission, the strict job, the clone owned by its box, the keeper and the import (§7.9), the frozen ref and
   its classing, the record's `serves` and `piece`, the role's binding, allow-list and census, the delivery receipt, the notes that reset
   nothing, §2.4's landing at every door, the opinion's families with their provenance, the admission by a measured row naming its
   effective configuration (the measurements table's first rows), the turn-end stand-in, the kit's queueing switch and the second maker
   by file, the Pieces section and the head in English and Chinese, the notes and the log. Proof: §8.2 and §8.3's partner cases; M2 in a
   disposable repository before its first use on the install; then the first real piece, carrying M3. It is one feature, large: a
   builder that cannot finish it splits by sub-feature only, never into a host half and a page half (D187 point 5).
3. **COAGENT1c — a conversation's lead, and the head's menu** (driver, modules, web-shell; after 1b). A conversation asks for a piece and
   hears its answer between turns; the head's ⋯ offers the ask. Proof: `PartnerChatProcessTests`; the page in both languages.
4. **COAGENT1d — the lead asks for a piece** (service, driver, modules, web-settings, web-shell; after 1b). `partner_ask` under the
   `partners` rule, the yes that names its agent, *What needs you*'s row, the Setup rows and `daoris driver partners`, Ask Daoris's
   `setting` door and its `session` card, the co-working skill's paragraph on partners in the canon, synced to this repository and both
   examples. Proof: the judge's and the tool's tests, the wait's census, the setting door's places, the page in both languages; then the
   owner's `on` and the dogfood.
5. **COAGENT1e — the provider sheet, and a maker that joins by declaration** (driver, modules, web-settings; after 1d). §3's
   `AgentCapabilities` with its sources and the measurements table whole, on `HARNESSES` and `agent list`, the Agents page's *In a shared
   session*, the manifest's two postures and their fixtures, §8.4's rehearsal phase. Proof: the sheet's tests per adapter, the census, the
   rehearsal phase.
6. **COAGENT1f — measured both ways on the install** (the parent with the owner; after 1d). §8.5's reverse run, Codex's resume and steer
   where ready, the first values (45 minutes, two open, the brief per piece) read again in a D199 note.
7. **COAGENT1g — a hand-over waits for the outgoing part's descendants** (driver; checked first; **before 1d**, since from 1d the
   install's leads hand over with pieces in flight). Whether the runner's stop ends a part's background commands and children before the
   next part starts in the tree; where it does not, the hand-over waits for them, said, with the strict job a partner's start uses. Proof:
   a `Process` case with a background command outliving its turn.

The order is 1a, 1b, 1c, 1g, 1d, 1e, 1f.
8. **Later, each after its measurement or its own design**: a channel between a lead and its partners, by participant (its own design,
   Codex's read 21); a partner resuming its own conversation on a second piece (amends D189 point 6; SESSCTX1a first); a limited piece
   carried on by itself, with its second record and its deadline; a cherry-pick's or a squash's provenance; a reading by another maker
   at the lead's done; three makers in one line; a new conversation started with a partner from the composer.

## 10. What the decision amends, and what it rejects

**Amends:**

- **D163**: point 2's read-only helper gains a writing sibling, the partner, in a clone of its own on one piece, landing only through its
  lead; the rejection of writing helpers in a first version is spent; a helper's copy carries the asker's work in progress (§2.5).
- **D64**: a plugin's harness may declare `fullPosture` and `askingPosture`.
- **D72**: a partner's shape of the rules, beside the server's refusals by role.
- **D177** point 5: the writing participant it left to a decision of its own is a session's partner, writing in a clone of its own; a
  meeting still talks and holds no checkout.

**Applies, and says so with a note**: D155 point 4, whose families include a partner whose piece the candidate holds (§7.7).

**Stands**: D51 and the one-session-per-tree question; D53; D131; D167 and ACCEPTALL1 (a partner keeps its own entry, as a helper does);
D188 whole; D189 (point 6 included); D197; CAPCOUNT1; D24; D46; D47 §4.

**Rejects**: a second agent in the lead's tree, at once or in turn; a partner in a linked worktree of the repository; a reader in a copy
without the work in progress; turns Daoris grants between the lead and a partner; an urgent level on a piece; a conversation kept across
pieces before resume is measured; a swap that keeps both conversations; a co-work record beside the boxes; a partner's record on the
lead's entry; enrolling the person's conversation under the cap; a partner's work landed past its lead or by a cherry-pick read as taken
in; a partner's edits applied as a patch; Daoris running git in a partner's copy after its turn; the partner's own history imported;
an unmeasured provider admitted as a partner, or one admitted on an agent's declining to escape, or on a stand-in's row; a partner
started in a best-effort container; a role enforced only by rules an agent may not take; a piece ref counted as a holder; a piece's
notice as a word; the manifest claiming queueing, steering, rules, limit words, lineage or confinement; a capability sheet a model is
asked about; Daoris choosing a maker the person's rule did not list; a token or money cap no harness enforces.

## 11. What is not known

- **Codex as a partner and a reader** (M1, M2, M4): whether its connector calls join, whether its sandbox on this machine refuses the
  probes of §8.5 (the local host's address and the pipe among them), whether it can run the probes without an agent, what its turn counts
  mean. Until M2, no partner runs on the install.
- **Claude Code as a partner**: no enforcement mechanism of its own is identified here (its rules do not reach a shell's write), so it
  is not admitted as a partner until one is identified and measured; it leads, and Codex is the partner, in the first rows.
- **Whether Codex can work without network** under `agent`: a piece that needs a package fetch may fail there; M3's run says how often.
- **The brief's cost per piece**, and whether 45 minutes and two open pieces are the right first values (M3, COAGENT1f).
- **The clone's and the capture's cost** on this repository on this machine, where a program starts slowly (COAGENT1a, 1b).
- **Whether the runner's stop ends a part's background work** before a hand-over starts the next part (COAGENT1g); a partner's end
  waits for its job to be empty either way (§2.3 step 6).
- **The import's first bounds** (10 MB a file, 50 MB a piece), read again in COAGENT1f.
- **Whether a plugin's harness runs a second stand-in beside the first** in one rehearsal home, which §8.4 assumes.

## 12. The reads (D191)

| Round | Read | By | At | Findings | Verdict |
|---|---|---|---|---|---|
| 1 | design and D199, round 1 | Codex (`codex-cli 0.162.0-alpha.17.2`, read-only, its prompt naming the files it might read) | `e44d52cba` | 21: 18 blocking, 3 non-blocking | all checked against the code; all taken in round 2, 2 in part with the rest answered |
| 1 | design and D199, round 1, as a foundation's read | Fable | `e44d52cba` | 10: 4 blocking, 6 non-blocking | accept once the blocking four are answered; all taken in round 2 |
| 2 | design and D199, round 2, and round 1's answers | Codex (the same) | `ba2ca4a48` | round 1's 21: 11 held, 10 held in part; 13 new: 12 blocking, 1 non-blocking | all checked against the code; all 13 taken in round 3, which also closes the ten held in part |

**Codex's read of round 1:**

| Finding | Taken or answered | Where |
|---|---|---|
| 1: an entry's parts may already run on two makers | taken: §0.3 reworded | §0.2, §0.3 |
| 2 (blocking): `line_read` follows `from`, so a partner reads only itself | taken: a partner reads its lead's line by `serves` and `piece`, both readers | §7.3 |
| 3 (blocking): the opinion's families miss a questless partner | taken | §7.7 |
| 4: unknown count meaning shown as no counts | taken | §0.2, §3.2, §5 |
| 5: `plugins.ts` gone; `PerAsker`, not `HelpersAtOnce`; the pipe's posture; the rehearsal's line | taken | §0.2, §3.2, §3.4, §8.1 |
| 6 (blocking): the lead's authority apart from the page's leading part | taken: a piece belongs to its asker and its `from` successors; a partner keeps its own entry | §2.1 |
| 7 (blocking): the helper's choice falls back to unlisted agents | taken: listed agents only, a named unlisted one refused, the yes naming its agent | §2.3 steps 2-3 |
| 8 (blocking): the first slice's order, and a host/page split against D187 point 5 | taken: the reader first; the partner with its stand-in, admission and record; splits by sub-feature only | §9 |
| 9 (blocking): a linked worktree shares refs, configuration and objects | taken: a clone of its own with no remote; Daoris imports a frozen tip | §2.2, §2.3 steps 5-7 |
| 10 (blocking): unmeasured writers admitted; C2a not installed | taken: partner confinement measured before every start, by versions and posture; the lineage row restated | §3.2, §3.3, §8.5 |
| 11 (blocking): restrictions by rules alone; `piece` escapes the `serves` refusals; omitted acting tools | taken: the server refuses by role with an allow-list and a census; `serves` carried | §7.3, §7.4 |
| 12 (blocking): the cap reads a `piece`-only record as a free conversation; reservations | taken: `serves` counted by `HoldsSlot`; slot and allowance reserved at `approved` | §2.3 steps 2 and 4, §5 |
| 13 (blocking): other landing doors and the tidy | taken: the landing's shared eligibility refuses a partner's record at every door; the ref outside every tidy | §2.4 |
| 14 (blocking): a cherry-pick contradicts the proof of taking in | taken: merge of a frozen tip only; a cherry-pick read as not taken in | §2.3 step 10 |
| 15 (blocking): the base not pinned | taken: the exact commit in the ask; no fallback | §2.3 steps 1 and 5 |
| 16 (blocking): the limit's repair lacks a record and a budget | answered: no repair in these rows; the piece ends `limited`, its work kept; the repair a later row | §5, §9 row 8 |
| 17 (blocking): no recoverable state machine | taken | §7.2 |
| 18 (blocking): the answer's delivery identity | taken: a stable key through the dispatcher, the hand door idempotent by it; every reader of solicited words | §7.6 |
| 19 (blocking): where a piece's answer goes after its lead ends or hands over | taken: the `from` successor, never a left conversation; notices apart from answers | §2.3 steps 9 and 11 |
| 20 (blocking): the files' and doors' ownership | taken: the home entry and its places, the driver doors, the pipe words | §7.1, §7.5 |
| 21 (blocking): the line's channel changes identity and addressing | taken in part: the channel row leaves this design for a design of its own that delivers by participant | §1 row 14, §9 row 8 |

**Fable's read of round 1:**

| Finding | Taken or answered | Where |
|---|---|---|
| F1 (blocking): the piece rests on the partner's own commit | taken: Daoris keeps what it left; committing by itself is recorded by M2 | §2.3 step 6, §8.5 |
| F2 (blocking): the partner on the lead's entry | taken whole: `serves` and `piece`, its own entry; ACCEPTALL1 and D167 not amended | §2.1, §7.3, §10 |
| F3 (blocking): M2 on the real repository | taken: M2 in a disposable repository before the partner's first use; the clone has no remote, so the push-address floor it proposed is not needed | §8.5 |
| F4 (blocking): an urgent answer escapes the count | taken: `partner_ask` takes no level | §2.3 step 1, §4.2 |
| F5: `line_read` reads an empty line | taken (as Codex's 2) | §7.3 |
| F6: the measured source and the negotiated store | taken: a code table held by a census; the negotiated reading named as the one kept apart | §3.1, §3.5 |
| F7: the lineage fallback would refuse M1's own call | taken: the row concerns native children; a partner's own call joins by its binding | §3.2 |
| F8: 1d needs 1b; split 1a now | taken: the rows reordered and the split decided by sub-feature | §9 |
| F9: `uncertain` missing from the states | taken | §6.1, §7.2 |
| F10: two citations drifted | taken | §0.2 |

**Codex's read of round 2.** On its round-1 findings: 1, 2, 4, 5, 6, 7, 9, 15, 16, 20 and 21 held; 3, 8, 10, 11, 12, 13, 14, 17, 18 and 19
held in part, each pointing at a new finding below, which round 3 takes. It confirmed that `serves` keeps a partner out of the lead's
entry, counts it under the cap and nests it under its lead on the rail (`groups.ts:304`), and that the page's names must read `piece`
(`identity.ts:79`).

| Finding | Taken or answered | Where |
|---|---|---|
| 22 (blocking): M2 measures an agent declining to escape; the effective configuration is not named | taken: admission rests on an identified enforcement mechanism in its effective configuration, measured by deterministic probes through the mechanism (local-path push, link, transient write, home, the local host, the pipe), the agent's run supplementary; a stand-in's row never admits production; Claude Code not admitted until a mechanism is identified | §3.3, §8.5, §11 |
| 23 (blocking): the keeper runs git in a copy the partner controlled | taken: Daoris never runs git in the copy after the turn; it reads the files and writes one commit in the repository through a temporary index, no filters, no hooks, no signing, the repository's identity | §2.3 step 7, §7.9 |
| 24 (blocking): no admission policy for the imported content and history | taken: regular and executable files only; links, junctions, submodules and nested repositories refused; the repository's ignore rules; bounds; the partner's own history never imported; secret detection not claimed; a refused import `held` with its copy | §7.9, §6.1 |
| 25 (blocking): the states do not reconcile completed side effects | taken: each side effect made before its state and idempotent by its identity (the open by the piece, a deterministic keeper commit, a create-only ref); a restart observes, then writes; the copy's removal a retried cleanup; whole-file state writes | §7.2 |
| 26 (blocking): stray cleanup would move a partner's copy | taken: the copy is owned by its box, left by the strays' cleanup while held, never a pass's leftover, listed in the inventory | §7.2 |
| 27 (blocking): a harness's exit does not mean every writer ended | taken: started suspended in an empty job, refused where it cannot join, the end waiting for the job to be empty; an unknown job keeps the copy untouched until the person's act | §2.3 steps 5-6, §7.2 |
| 28 (blocking): the role holds on MCP only; HTTP and the pipe stay open | taken: the driver binds the role through the scope a child inherits; the local host and the pipe are closed to a partner by admission, its mechanism measured to refuse them; D156 named as the residual for other sessions | §3.3, §7.4 |
| 29 (blocking): the box's reservation does not reserve the shared cap | taken: reservations counted in the shared count every starter reads, converted once and released at every end; two open counted across `from` | §2.3 step 4, §5 |
| 30 (blocking): ancestry is not durable incorporation or authorship | taken: taken-in recorded when first seen and never unset; Daoris's replays and squash landings carry the pieces' provenance; a contributing maker kept among the opinion's families where a rewrite leaves it unclear; a revert is the lead's work | §2.3 step 10, §7.7 |
| 31 (blocking): delivery omits uncertain attempts and ownership across successors | taken: one piece-wide delivery receipt with its claimed destination, moved along `from`; an uncertain attempt waits for the person | §2.3 step 9, §7.6 |
| 32 (blocking): a notice can reset the answers-alone count | taken: a piece's notices are notes, never words: they reach no prompt and reset nothing | §2.3 step 11, §4.2 |
| 33 (blocking): 1b's proof needs fixtures scheduled in 1e | taken: the queueing switch and the second maker by file move into 1b; the strict job is 1b's; 1g ordered before 1d | §8.1, §9 |
| 34: "no tidy reads the namespace" too strong | taken, and treated as needed: piece refs are classed beside the recovery refs, never mutated by generic cleanup and never counted as holders, with retention tests across tidy, clear, sync, a landing and `git gc` | §2.4, §8.3 |

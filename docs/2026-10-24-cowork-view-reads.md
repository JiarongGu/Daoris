# COWORKVIEW1: the reads of rounds 1 and 2, the Opus foundation reader's of both and Codex's of round 2 (D191)

The independent Opus 5.5 reader's foundation read of `docs/2026-10-24-cowork-view-design.md` and its D173 note at `ccb2e5a45`, verbatim but for its headings set one level down. The steward ran it from outside the install, read-only, on main. Codex's read stopped when its credits ran out and is owed. The Opus foundation reader asks another round (19 findings, 10 blocking). Round 2 answers it. Codex's read of round 2 with its named changes follows the Opus reads (another round, 20 findings, 11 blocking); round 3 answers it.

## Opus (foundation), round 1

The read, verbatim.

### 1. Claims about the code

Read against main at `47d00cb58`, which is the design's `fd1d2727e` plus docs commits only. Paths use the design's abbreviations
(`Driver/`, `Modules/`, `Core/`, `Mcp/`, `Web/`). Design line numbers refer to `docs/2026-10-24-cowork-view-design.md`.

Most of §1 holds as cited:
- **The lineage.** Its writers and their lines: `ChildLineage.cs:35`, `:52`; `LineageFile.cs:72-92`; `lineage-hook.mjs:7`,
  `:24-25`, `:38-39`; `Permissions.cs:561-576`; `ChatRunner.cs:1349-1360`, `:2497`; `Driver.cs:3087-3097`, `:3825`;
  `HarnessCapture.cs:123`.
- **The fold.** `ChildBindings.cs:13-37`, `:64-73`, `:128-266`, `:298-323`. `ChildLineage.Read` is called only by
  `ChildLineageTests` and `LineageHookProcessTests`.
- **The protocol door's announcements.** `AcpStreams.cs:152-204`.
- **The quest's fields, the refusal and the lane judge.** `Quests.cs:96-117`; `QuestExchange.cs:528-540`, `:707-743`.
- **The ledger.** It makes no move from working to working (`SessionLedger.cs:2181-2192`), and `updated` keeps its meaning
  (`SessionGroups.cs:36`; `headFacts.ts:169`, `:175`, `:207`).
- **The frame.** The views and their places (`placements.ts:9`, `:22`, `:28-30`); the dock (`layout.ts:44`).
- **The pipeline.** The fourteen slices, the five change-stream families, `HeldAnswers` and D56's NOBLOCK1 note.
- **Other records.** The brainstorm and meeting requests (`BrainstormFiles.cs:8`; `MeetingFiles.cs:9`); units and their one
  reader (`WorkUnit.cs:53-73`; `DriverWatch.cs:900`).

These claims are wrong or incomplete. Each is a finding in §4:
- **Lane sessions.** A lane quest to the session's own repository is said to be seen "nowhere in the publisher's session" and to
  run in "its own session, tree and turn, landed on its own" (:175, :455, :500). In the code its session is a further part of the
  publisher's own line: it appears in that line's Parts strip and lands on the line's one branch (F1).
- **CHATTAKE1.** "A chat takes no quest" (:476). A chat can take one, and CHATTAKE1 records it (F11).
- **The tool's description.** "No door said there was another way" (:26). `quest_publish`'s own description says it (F14a).
- **The id join.** The join of `subagentSessionId` to the hook's `agent_id` is called "not known" (:290). The CONSOLE2
  evidence already records the id, and the re-run suffix that breaks the join (F6).
- **The case replayed.** It states that the hook recorded each child's start and stop on the install (:190). That is
  unmeasured (F14b).
- **The cards.** "Each child … became one card" (:189), and "the label is already on the session's events" (:287-289). Only a
  child the root spawned gets a card (F14c).
- **DEV6.** "DEV6 … make[s] lanes run side by side" (:560-561). PAR1 already runs a repository's sessions side by side where
  that repository opens trees of its own (F14d).
- **The helper box.** It is under `data/helpers`, not `<home>/helpers` (:78) (F14e). The `TASKS.md` line numbers have moved
  (F14f).
- **The title.** §5.4 says *Team* "is not the view's title" (:444-447), yet the title is *Team at work* (:421) (F9).

### 2. The mechanism's holes

- **Parts shown that are not there, or missed that are.**
  - A lane's session to the publisher's own repository is drawn twice: as a part of the line and as a quest member (F1).
  - A native child whose door id and hook id do not join is drawn twice (F6).
  - Every child on the protocol door is drawn flat as *parent not said*, although the wire says its parent (F5).
  - A child of a killed root can read *finished*, and a failed child *finished* (F5).
  - A chat's taken quest is missed (F11). A chain's later steps are missed, or credited to the person (F12).
  - A Codex lead's silent children are missed under *Nobody else is working on this* (F18).
  - Helpers and partners are read correctly from the boxes and `serves`. The helper wait's join through `asker` holds
    (`Observation.cs:164-165`; `HelperBox.cs:73-83`).
  - Brainstorm voices are rightly deferred to row g, since neither request names its caller (`BrainstormFiles.cs:8`,
    `MeetingFiles.cs:9`).
- **State the records do not keep.**
  - No record keeps a turn, so *Inside this turn* cannot be said (F7).
  - The same holds for the edge "the turn has not ended because they run" (F7).
  - *Last heard* stays still while a child works (F8).
  - An untaken quest is shown without the reason the driver already gives (F13).
  - Nothing is parsed from the conversation's text: the labels are the harness's structured fields.
- **The window's thread (NOBLOCK1).** The composition runs off the thread, and the read answers what it holds; that part is
  right. But none of the wakes reaches a held read: `HeldAnswers` does its work only when the page asks (F4).
- **Routing.**
  - The doctrine's "end its turn" parks a driven lead on the person, the case HELPERWAIT1 fixed for helpers (F2).
  - A session publishing its own repository's rows as quests is bounded by no rule of the person's (F3).
  - It does nothing in a repository that is not driven, or that does not open trees of its own (F3).
  - The view starts no turn. D188 already settles how a chat hears its quests' ends (F11).
- **Names.**
  - *Team at work* and `sessions team` collide with D203's word, its rejection and its verb (F9).
  - Three Chinese words go against the glossary (F15).
- **Widths and themes.**
  - The stories are at the main area's widths, not the side bar's 300 px floor (F16).
  - The themes are right: tokens only, and `--warn` with a word for a lost child.

### 3. The rows

- **Row a** can be built alone. Its proof checks the fold, but not the parent, the end taken from the wire alone, the
  harness's end words or the unjoined id (F5, F6). It shows the owner nothing (F10).
- **Row b** cannot be built as written:
  - Its wakes need a seam that `HeldAnswers` lacks (F4).
  - Its verb is COWORKUX1a's, which defines another output (F9).
  - Its groups draw F1's lane sessions twice.
  - Its timing proof follows NOBLOCK1's precedent and is right.
- **Row c** is the owner's first visible row, and it depends on a and b. Its proof cannot happen in one run (F17). It does not
  test *last heard*, which is what made the case read as stuck (F8). It also carries F7's heading, F15's words and F16's widths.
- **Row d** can be built after c, with F1's partition. Its proof would pass with each lane session drawn twice. If the
  publishing session is a driven one that ends its turn, the session parks (F2). Its *not taken yet* should say why (F13).
- **Row e** holds once d does.
- **Row f's** canon section carries F2's and F3's content. It needs the canon-authoring rules and a sync to this repository and
  both examples. The hint's rule needs F3's preconditions and F7's run in place of a turn.
- **Row g** holds; it waits on BRAINSTORM1d and AGENTROOM1b.

**Is the first row the one that gives the owner the most?** No. Rows a and b are layers that D187 rejects as rows, and the first
thing the owner can see is row c. The first row should be a, b and c together: *a chat's subagents seen*.

### 4. Findings

1. **A lane quest to a session's own repository makes a further part of that session's line. Blocking.**
   - **The entry rule.** The entry is `<origin>/<repository>`. A quest's origin is the origin of the session that published it
     (`Core/Sessions/SessionEntries.cs:33-37`, `:108-113`), and the ledger opens the quest's session under that origin
     (`Core/Sessions/SessionLedger.cs:1599-1616`). A chat publishing to `itsrepo:lane` therefore opens a session in the chat's
     own entry. On the window that session is a further part of the same session, in its Parts strip and its one rail row
     (D167 points 2, 4 and 5, `D167.md:22-31`).
   - **How the part runs and lands.** It runs alongside in a tree of its own while a part holds the entry's tree. Otherwise it
     goes into the newest part's tree (`Driver/Quests/Domain/Planner.cs:1716-1759`; `D167.md:73-82`). It lands on the entry's
     one branch under the entry's one Accept (`D167.md:286-306`, ACCEPTALL1 `:330-336`). D115's "work for another lane is work
     for another session" (`QuestExchange.cs:533-534`) meets D167's entry rule here, and the design has not read either.
   - **What it gets wrong as a result.**
     - The rows at :175, :455 and :500 are wrong for lane quests to the session's own repository.
     - §3.1 reads the team "for its line (… every part, through `from`)" (:215), but the entry is not the `from` chain.
     - The same lane session is drawn twice: as a part under *This session* (:220, "the entry's parts") and as a member under
       *Asked through quests* (:223).
     - *Inside this turn* reads every part's lineage. It therefore puts the lane session's own children under a note that
       says they run "on its account and in its tree" (:430), which is false of them.
     - "The lead: the line's live part" (:220) names none where an entry has several live parts (`D167.md:77-78`).
     - The hint's "sessions of their own" (:442) describes parts of this session.
     - Row d's proof (:528-529) passes even with the duplicate row.
   - **Smallest change.**
     - State the entry rule's effect in §1.3, §1.9, §6.1 and §6.5.
     - Give such a record exactly one home in the view. It is either a quest member with its lane, leaving *This session* to
       the `from` chain, or a part that carries its lane, but not both.
     - Read *Inside this turn* only for the lead's `from` chain.
     - Say which live part is the lead.
     - Row d's proof asserts that each lane session is drawn once and that the view and the Parts strip agree.
     - Add a D167 note beside D115's.

2. **The routing doctrine parks a driven lead on the person. Blocking.**
   - **The park.** §6.2 has a session that published quests "say so and end its turn" (:470). A driven session that ends its
     turn cleanly while it holds its quest parks as `awaiting-person`, quoting its last words
     (`Driver/Sessions/Domain/Observation.cs:167-174`). That is the HELPERWAIT1 case D203 point 5 fixed for helpers.
   - **The one-quest wait.** The only wait a record carries is a single quest: `Quest.Awaits` holds one id
     (`Core/Quests/Quests.cs:96-102`). `quest_respond wait` takes one `on` (`Mcp/QuestTools.cs:374`), and its description
     covers "the question you published to another repository" (`:366-367`). So a driven lead that hands out three rows either
     parks on the person or waits on one of the three, and *Waiting on* (:236-238) can show one at most.
   - **Smallest change.** The skill section says what each kind of session does after publishing:
     - A chat ends its turn.
     - A driven session either finishes its quest, naming the quests it handed on, or `wait`s on one of them. It never ends a
       turn while it still holds its quest.
     - §3.2 says a lead waits on at most one quest.

3. **The routing names no rule of the person's and no precondition. Blocking.**
   - **No rule.** `quest_publish` writes a quest with no check beyond the exchange's own judgement
     (`Mcp/QuestTools.cs:148-163`). `ByAgent` matters only for review choices (`Core/Quests/QuestExchange.cs:108-113`). A
     helper, by contrast, waits on the person's `helpers` rule (`Core/Sessions/HelperBox.cs:86-88`), and a partner on
     `partners`. D203 point 4 has the lead start structures "by its tools under the person's rules" (`D203.md:31-33`). §6.2
     steers agents to "one quest per row", each of which spends an account, and §6.5 names only DEV6's unbuilt cap.
   - **No precondition.** A lane quest starts on this machine only where the repository is driven
     (`Driver/Quests/Domain/Planner.cs:1889-1891`). It runs beside the publisher only where the repository opens its own trees
     (`:1964-1966`; otherwise one session per repository, `:2010-2021`). The skill section and the hint (:489-491) would send an
     agent in a repository that is not driven to publish work that never starts. The hint would also say a lanes file is
     missing when driving may be.
   - **Smallest change.**
     - Name the rule that bounds an agent's quests to its own repository. It can be shaped like `helpers` (off, ask or on).
       Or state that the person's choice to drive the repository with trees is that rule, give the reason, and name the cap
       that holds today.
     - Make the skill section conditional on that rule.
     - Draw the hint only where the repository is driven here with trees, and name the missing drive where it is not.

4. **None of §4's wakes reaches a held read. Blocking.**
   - **No seam in the held reads.** `HeldAnswers` starts a read's work only when the page asks
     (`Modules/Bridge/HeldAnswers.cs:20-22`, `:74-106`). It tells only the pages that asked (`:170-177`). The page asks again
     only when a query is invalidated (`Web/bridge/state/held.ts:24-34`), and D169's one place for that is `stateHub.ts:93`.
     So "composes on a task of its own; … told as `DAORIS.ANSWERED`" on a wake (:341-354) has no seam to run through.
   - **The wakes themselves.**
     - `HomeWatch` registers its topics once and has no way to remove one (`Driver/Platform/HomeWatch.cs:274-286`). Parts
       "added while a team read is held and dropped with it" (:346-347) are therefore not available.
     - `ForDriver` wakes the driver's look, not a modules route (`:196-213`).
     - The `calls` slice moves only when a call starts or ends (`Modules/Loop/CallsStateSlice.cs:23-24`), so it cannot keep
       *last heard* fresh.
   - **Smallest change.**
     - Name the path. Each wake raises one Daoris event naming the session or ask. `stateChanged` turns that event into an
       invalidation of the `SESSION_TEAM` query. The query's next ask then runs the work and is told when it lands.
     - Alternatively, use a keyed pushed partition per attended session, as `calls` does, and amend D56's NOBLOCK1 note to say
       why.
     - Hear the lineage files through one fixed part over `sessions/` filtered to `*/lineage.jsonl`, in the window's own
       process (as `ForProposals` is), rather than parts added and dropped.

5. **The protocol door's `acp` line omits the parent and can write a false end. Blocking.**
   - **The parent.** The adapter announces each child on its parent's wire session (`from`), which is the root's or another
     child's (`Driver/Agents/Acp/AcpStreams.cs:152-158`). On the protocol door, which carries the install's sessions (D173's
     FABRIC-ACP1 note, `D173.md:5173-5180`), nothing else writes a parent:
     - the hook writes none (`lineage-hook.mjs:31-46`);
     - the protocol capture runs no stream watcher (`ChatRunner.cs:2397-2419`);
     - the fold takes a parent only from a stream line or a Codex call (`ChildBindings.cs:268-291`).

     §3.6's line (:280-282) keeps no parent either. Every child on that door is therefore *parent not said*, and the tree that
     §3.1 (:221) and §5.2 (:381) describe never forms.
   - **The end.** Only the wire's `subagent_state_update` may write the child's end (`AcpStreams.cs:186-204`). `EndAllAsync`
     closes every open child with the driver's own `SessionEnded` (`:110-148`). A line written there would fold as the
     harness's own stop (`ChildBindings.cs:195`, `:211`, `:278`), and a child of a killed root would read `finished` instead of
     `lost`.
   - **The end words.** The adapter's end states are `completed`, `failed`, `cancelled` and `disconnected`
     (`docs/2026-09-28-console2-streams-evidence.md:69-70`). The fold folds them all into `finished`.
   - **Smallest change.**
     - The `acp` line carries `parent`: the `from` id, or the root with depth 1.
     - The end line is written from `subagent_state_update` only.
     - The fold keeps the wire's end word, and the view draws it.
     - Row a's proof covers all three.

6. **The join's fallback draws one child twice, and the evidence about the id is already on record. Blocking.**
   - **The evidence.** The CONSOLE2 evidence records that `subagentSessionId` is "the harness's agent id, seventeen hex
     characters", and that a re-run gets `<id>:generation:<n>` (`docs/2026-09-28-console2-streams-evidence.md:61-64`). The hook's
     `agent_id` is Claude Code's agent id (`D173.md:100-102`).
   - **The double row.** The fallback "keeps each door's child apart" (:292-293). Wherever the ids do not join, as with a
     generation suffix, one child is drawn as two rows.
   - **Smallest change.**
     - Cite the evidence.
     - Row a measures both a first run and a re-run.
     - An announcement that does not join stays the child's row. The hook's unjoined agents become a count beside it, not a
       second row.
     - Row c's proof includes the unjoined case.

7. **No record keeps a turn, so *Inside this turn* names a boundary nothing holds. Blocking.**
   - **The run spans many turns.** A chat on the protocol door keeps one process across its turns, taking each new message
     into that process (`ChatRunner.cs:1804`, `:1826`, `:2397-2399`). Its lineage gets an end line only when the process lets go
     (`:2486-2497`), and its record moves to `working` once (§1.7).
   - **What follows.**
     - A child whose stop the hook missed stays `active` through later turns.
     - Every child of the run sits under 本轮之内 and under the note at :430.
     - The edge at :241, "the turn has not ended because they run", states a cause that no record holds. A background child
       does not hold a turn.
     - The hint's "two children in one turn" (:489) cannot be counted.
   - **Smallest change.** Name the group after what the lineage does hold, the run (*Subagents* 子智能体, in this run). Drop the
     edge at :241, or say "n running" instead. Count the hint per run.
   - **The alternative.** Have row a write a driver line at each turn's end, and list it in §3.6 as a stored change.

8. ***Last heard* stays still while a child works, and duplicates the watcher's. Blocking.**
   - **It does not move.** On the protocol door a child's words and tools go to its own stream in memory, never to the
     session's events (`AcpStreams.cs:16-21`, `:95-102`). Its lineage gets a line only at its start, at its stop and when it
     calls the connector. So "the newest `at` among its own events and its children's lineage lines" (:304) reads 38 minutes
     ago for a child that has been working for 38 minutes. That is the case's "reads as stuck" again, now with a time on it.
   - **It duplicates the watcher.** The head already says *quiet · N min* from the watcher's own hearing
     (`Driver/Sessions/Watch/SessionWatch.cs:69-71`; `CallsStateSlice.cs:37-41`; `headFacts.ts:199`). The head could then say
     two different things about one session.
   - **Smallest change.**
     - Take *last heard* from the newest line of the session's and its children's streams, kept in memory.
     - After a restart, say that nothing has been heard since the window started.
     - Then use the watcher's hearing, then the events and the lineage.
     - The head's new fragment yields to *quiet* wherever the watcher has marked the session.

9. ***Team at work* and `sessions team` take D203's word and D203's verb. Blocking.**
   - **The title.** D203 point 9 titles the window's section *Co-workers*, because *team* already names the people sharing a
     remote. It rejects "*Team* as the window's title" (`D203.md:58-60`, `:83`).
   - **The word.** Point 2 defines a session's team as "its line and every record serving any part of it" (`D203.md:22-24`).
     The D203 note says point 1 stands (`D203.md:199-206`), but the view puts in the team members that serve nothing, which
     widens point 2.
   - **The verb.** `sessions team` is D203's verb. Its output is defined in the coworker design (:282-283), and COWORKUX1a
     builds it (:661-664). Row b's widened version (:514-515) is a second contract, and the `Team` type (:314-337) has none of
     D203's fields.
   - **Smallest change.**
     - Give the view an English title without *team*, such as *At work*, and keep 分工.
     - Name the read and the verb apart, for example `SESSION_WORK` and `sessions work`. Or keep `sessions team` exactly as
       COWORKUX1a defines it, with a flag that adds the further groups.
     - Have the D203 note say what reading of point 2 the view widens.

10. **Rows a and b are layers, and the first row shows the owner nothing. Blocking.**
    - D187 point 1: the unit of work is a feature a person can see working, built driver to page by one builder
      (`D187.md:13-16`). Rows a and b are lane slices of the kind D187 rejects. Row c's install proof waits on both.
    - **Smallest change.** Make the first row *a chat's subagents seen* (driver, modules and web), with the join measured as its
      first step and F5's line in it.

11. **A chat can take a quest. Not blocking.**
    - CHATTAKE1 records the quest a chat takes as `Session.TookQuest`. The quest then joins the chat's line
      (`Mcp/QuestTools.cs:395-403`; `Core/Sessions/Sessions.cs:246-250`; `SessionEntries.cs:102-106`; `D167.md:176`).
    - §6.3's claim at :476 is therefore wrong, and the takers and *Asked by* (:223-224) would miss such a quest.
    - D188 already settles what §6.3 leaves to the readers (:478-480, :560): a quest's outcome is ordinary and never starts a
      turn (`D188.md:18`, `:29-30`).
    - **Smallest change.** Read `Session.Quest ?? Session.TookQuest`, and cite D188 point 2.

12. **A null `PublishedBy` does not mean "by you". Not blocking.**
    - `PublishedBy` is null both for the person's own publish and for a chain step (`Core/Quests/Quests.cs:104-109`). An intake
      also publishes as its ask.
    - §3.4's "*by you*" (:260) would credit the person with a chain's step. The group rule at :223 also misses the later steps
      of a quest the line published.
    - **Smallest change.**
      - A quest with a `Parent` is "the next step of #parent", followed from that parent (the unit's `follows`,
        `WorkUnit.cs:64-65`).
      - A quest whose `From` is `ask #id` belongs to that ask.
      - Only a quest with neither is *by you*.

13. **An untaken quest is shown without the reason it sits. Not blocking.**
    - D46 §3's reason is in the driver tick's `considered`, which the page reads through `sittingBecause`
      (`Web/data/signals.ts:5-7`, `:145-158`). The reasons include not drivable, repository busy, at capacity and waiting for an
      account.
    - The edge at :239 and *not taken yet* (:434) say none of this.
    - **Smallest change.** Carry the tick's verdict and its sentence on the edge.

14. **Smaller claims to correct. Not blocking.**
    - (a) §0 and §1.9 (:26-27, :182-184): the `to` parameter's description in `quest_publish` already calls a repository's own
      lanes "the one way to ask your own repository" (`Mcp/QuestTools.cs:37-40`). What the case lacked was lanes, not words.
    - (b) §1.10 (:190) states that the hook recorded each child. FABRIC-ACP1 saw no native child (`D173.md:5177-5180`), and
      `SubagentStart` over the adapter is unmeasured (`D173.md:131-133`). That is row a's to read.
    - (c) A card is emitted only for a child the root spawned (`AcpStreams.cs:158`, `:169-181`, `:202`). A nested child's name
      and task exist only in memory, so §3.6 (:287-289) writes them to disk for the first time.
    - (d) §9 (:560-561): PAR1 already runs a repository's sessions side by side where it opens its own trees
      (`Planner.cs:1964-1966`). DEV6 adds lane reservations and a cap.
    - (e) The helper box lives under `data/helpers` (`Core/Platform/DaorisHome.cs:340`), not `<home>/helpers` (:78).
    - (f) `TASKS.md` has moved. SELFASK1 is at :519, DEV6 at :521 and DEV9 at :524; the design cites :511, :513 and :516.
      Cite rows by id instead.

15. **Three Chinese words go against the glossary. Not blocking.**
    - `child.ended` names *its part*, but its zh uses 部分, not the glossary's 段 (`glossary.json:483`). Under `--strict`,
      `names-check.mjs:184-188` fails a label for this.
    - `insideNote` uses 账号, which the glossary avoids in favour of 账户 (`glossary.json:595`; `names-check.mjs:197`).
    - *Sub-agents* is not the glossary's *subagent* (`:593`).
    - **Smallest change.** Use 随所属段结束, 账户 and *Subagents*.

16. **The stories use the main area's widths, not the side bar's. Not blocking.**
    - `MAIN_WIDTHS` are the main area's 80rem, 39rem and 25rem (`Web/design/storyWidths.tsx:9-13`).
    - The side bar can be as narrow as `DOCK.floor`, 300 px (`layout.ts:44`), and side bar views are storied at 300 and 430 px
      (`Web/help/AskPanel.stories.tsx:339`; `Web/work/review/FilePreview.stories.tsx:5`).
    - §5.3 has no row for 300 px, where Chinese text is narrowest.
    - **Smallest change.** Story the view at 300 and 430 px as well as the full frame at 680 px, in both languages and both
      themes.

17. **Row c's proof cannot happen in one run. Not blocking.**
    - Killing the root ends the run, and every child not yet stopped becomes `lost` (`ChildBindings.cs:241-244`;
      `ChatRunner.cs:2486-2497`). So no child is left *working* (:521-523).
    - *ended 14:02* merges *finished* with *ended with its part*, which §2.3 keeps apart (:203-204).
    - **Smallest change.** Use two runs: a killed run, then a resumed one. Name the two states apart.

18. **A Codex lead's children are seen only when they call Daoris. Not blocking.**
    - A Codex child is bound only from the `thread` of its calls (`ChildBindings.cs:223-235`; `D173.md:120-122`), and Codex's
      protocol door is unmeasured (`D173.md:5184-5188`). Such a lead can read *Nobody else is working on this* (:412) while its
      children work.
    - **Smallest change.** For such a harness, the group says that children appear here only when they call Daoris, in place of
      the empty state.

19. **The quests group re-derives what the Fabric's units already carry. Not blocking.**
    - Quest units already carry `awaits`, `follows`, `produced-by`, their executors and their end (`WorkUnit.cs:53-73`,
      `:140-152`). GRAPHVIEW1 will draw from them.
    - §3.1 and §3.2 compose the same relations again from the `Quest` fields (:223, :236-239).
    - **Smallest change.** Read each quest member's state and wait from its unit, or say why not. Either way, the team view and
      the running view then agree on a quest's state.

**Findings: 19, of which 10 are blocking (1-10).**

**Verdict: another round.** The design reads the code carefully, and most of its citations hold. Its rules are right: the team
is a read and never a store, it comes from the records and never from the conversation's text, partial visibility is said
openly, and no model is involved. But four of its foundations do not hold as written:
- Under D167, a lane quest to the session's own repository opens a part of the publisher's own line. That changes the team's
  groups, which part is the lead, the routing table and the landing (F1).
- The routing doctrine would park a driven lead on the person, and would fan out sessions under no rule of the person's (F2,
  F3).
- None of §4's wakes reaches a held read (F4).
- The new lineage line and its fold miss the parent and the real end, and the records keep no turn and nothing that moves while
  a child works (F5-F8). The owner's case would still show a flat list, under a heading the record cannot back, that reads as
  stuck.

F9 and F10 are mechanical. F1's partition, F3's rule, F4's path and F7's run or turn each need a design choice, and that choice
should be checked against the code. The next round can be narrow: §1.3, §1.9, §3.1-§3.2, §3.6-§3.7, §4, §6 and §7, with the
other findings taken in passing.

## Opus (foundation), round 2

The independent Opus 5.5 reader's foundation read of round 2 at `eb11f2f5f`: 13 findings, 3 blocking; accept after named changes the steward can check without another read. Codex's read is owed.

### 0. Round 1's findings, checked

Read on main at `ffa1cfa9c`. Its `src/` differs from the design's `426ed2b73` in two workflow files only (`WorkflowRun.Read.cs` and
its tests), which this design does not touch. Paths use the design's abbreviations. Design line numbers refer to
`docs/2026-10-24-cowork-view-design.md` at `eb11f2f5f`.

| # | Round 1's finding | Status | Checked against the code |
|---|---|---|---|
| **1** | A lane quest to the session's own repository makes a further part of its line | **Taken** | Holds. The origin rule is `Core/Sessions/SessionEntries.cs:95-121`. Helpers and reviewers keep their own entries (`:76-77`). Further parts are planned at `Driver/Quests/Domain/Planner.cs:1716-1767`. The part is drawn once (§2 point 4, §3.1). Residues: the view's order disagrees with the strip's (F7). A part planned after the lead waits goes into the lead's tree, not alongside (F2). |
| **2** | The routing doctrine parks a driven lead on the person | **Taken** | Holds for the park. A clean end holding the quest parks (`Driver/Sessions/Domain/Observation.cs:173-174`, `:216-219`). A new wait ends `completed` with `ended.awaits` (`:104-111`, `:292-293`). The exchange refuses no wait on one's own lane's quest (`Core/Quests/QuestExchange.cs:1454-1505`). The wait it prescribes opens a new hole (F2, F3). |
| **3** | The routing names no rule of the person's and no precondition | **Taken** (the second way; the reason holds) | `Consider` never reads the publisher: drive (`Planner.cs:1889-1893`) and the cap (`:2046-2053`) bound every quest alike. Residues: trees decide alongside or after, not whether a quest starts (`:2010-2021`; F13e). The chat case has no drive precondition (F11). |
| **4** | None of §4's wakes reaches a held read | **Taken** | Holds. `STATE_CHANGED` leads to the hub's invalidation (`Web/bridge/state/stateHub.ts:93-100`), then the re-ask, then `HeldAnswers` (`Modules/Bridge/HeldAnswers.cs:74-106`). Every `STATE_CHANGED` also forgets the works under way (`Modules/Bridge/DriverModule.cs:102-107`), so a wake never joins a stale work. Residues: the narrowing by session (F1) and the stream-rate telling (F5). |
| **5** | The `acp` line omits the parent and can write a false end | **Taken** | Holds. A child speaks under its own `params.sessionId`, which is its `subagentSessionId` (`Driver/Agents/Acp/AcpStreams.cs:95-98`, `:155-164`), so an `acp` line's parent joins the announcing child. A stop ends the part unclean (`ChatRunner.cs:2497`), and the part's `end` closes open children as `lost` (`ChildBindings.cs:241-244`). |
| **6** | The join's fallback draws one child twice; the evidence is on record | **Taken** | Holds in shape. Residue: the generation suffix is the adapter source's word, not a measurement. The count's words assert what no record holds (F8). |
| **7** | No record keeps a turn | **Wrong in part** (taken) | The session's event log keeps each turn's end as a `turn` event with its stop reason (`Driver/Sessions/Domain/SessionEvents.cs:33-34`). The protocol door emits it at each prompt's response (`Driver/Agents/Acp/Acp.cs:1999-2005`). On that door the adapter holds the parent's turn open until the children it spawned end (`docs/2026-09-28-console2-streams-evidence.md:74-77`). The answer, grouping by the agent process, still stands. The claims built on the finding do not (F4). |
| **8** | *Last heard* stays still while a child works | **Taken** | Holds. `SessionOutput` keeps no moment today (`ConsoleLine(Sequence, Text)`, `SessionOutput.cs:9`; `Buffer`, `:98-112`), so §3.6 rightly names it new memory. Residues: §3.7's present tense (F13d) and the telling's route (F5). |
| **9** | *Team at work* and `sessions team` take D203's word and verb | **Taken** | Holds. Residues: the new scope reuses `work`, the word §11 rejects for the read (F9). The D203 note names the wrong design (F13a). |
| **10** | Rows a and b are layers | **Taken** | Holds: row a runs from the driver to the page, with the measurement as its first step. |
| 11 | A chat can take a quest | **Taken** | Holds for parts (`Session.Quest ?? Session.TookQuest`). Residue: the units' executors miss a chat's take (F6). |
| 12 | A null `PublishedBy` is not "by you" | **Taken** | Holds (`QuestUnits.cs:27-29`). |
| 13 | An untaken quest is shown without its reason | **Taken** | Holds (`Planner.cs:2048-2052`; `Web/data/signals.ts:145-158`). |
| 14 | Smaller claims | **Taken** | Each holds (`Mcp/QuestTools.cs:37-40`; `DaorisHome.cs:340`; rows cited by id). |
| 15 | Three Chinese words against the glossary | **Taken** | Holds (段, 账户, *subagent*). Residue: four words it calls the glossary's are not glossary terms (F10). |
| 16 | The stories use the main area's widths | **Taken** | Holds (`layout.ts` `DOCK`; `AskPanel.stories.tsx:336-342`). |
| 17 | Row c's proof cannot happen in one run | **Taken** | Holds. A go-on keeps the record and its lineage file (`ChatRunner.cs:2423`; `ChildBindings.cs:176-177`). Residue: the installed proof never draws *ended with its part* (F12). |
| 18 | A Codex lead's children are seen only when they call | **Taken** | Holds. |
| 19 | The quests group re-derives the units | **Taken** | Holds in shape. Residue: the inputs `QuestUnits.Read` needs are held by no route (F6). |

### 1. Claims about the code

Most of the design holds as cited. These parts were checked and stand:
- **The lineage.** `ChildLineage.cs:35`, `:52`, `:118-139`; `ChildBindings.cs:167-178`, `:241-244`, `:268-281`, `:298-323`;
  `lineage-hook.mjs:24-46`; `ChatRunner.cs:1349-1360`, `:2497`. Nothing outside tests calls `ChildLineage.Read`.
- **The protocol door.** `AcpStreams.cs:77-204`, with a card for a root's child only (`:169-181`, `:202`). Driven sessions are
  handed streams as chats are (`Driver.cs:4250`, `ChatRunner.cs:2593`).
- **The entry rule and its parts.** `SessionEntries.cs:71-121`; `EntryParts.cs:24-45`; `Planner.cs:1716-1767`, `:1889-1893`,
  `:1964-2060`; D167's amendments.
- **Waits and units.** `Observation.cs:104-111`, `:164-174`; `QuestExchange.cs:1454-1505`; `QuestUnits.cs:20-41`.
- **The pipeline.** `HeldAnswers.cs`; `DriverModule.cs:102-107`, `:189-211`; `stateHub.ts:27-100`; `held.ts:24-52`;
  `StatePublisher.cs` (the scopes and `Publish`); `QuestsStateSlice.cs:128-146`; `HomeWatch.cs:274-280`, `:316-344`;
  `DriverLoop.cs:806-816`.
- **The frame and the words.** `placements.ts:9`, `:22`; `layout.ts`'s `DOCK`; the panel's end words (`work.panel.json:16-20`).

These are wrong or incomplete. Each is a finding in §4:
- **No record keeps a turn, and a background child holds none** (:221-223, :364-365, :674, :849-851; D173.md:5443). The event log
  keeps each turn's end, and the adapter holds the turn until its children end (F4).
- **A wake names the session it is about** (:520-523, :539). A ledger wake carries families (`QuestsStateSlice.cs:132-137`). A home
  wake carries its topic's fixed subject (`HomeWatch.cs:342`). Neither names a session or a file (F1).
- **A waiting lead's lane part runs alongside** (:118-122, :267, row b). A part planned after the lead's turn ended goes into the
  lead's tree, and the lead resumes without the part's commits (F2).
- **"The quests and sessions the driver holds"** (:545-547). No route holds them. The loop's snapshot keeps open and taken quests
  and active sessions only (`Planner.cs:888-891`, `:1770-1771`) (F6).
- **"The view and the strip list the same parts in the same order"** (:307-308, :560-561, :812-813). Yet §3.1 orders lead, live,
  then ended folded (:327) (F7).
- **"A child's end reads the same in the panel and the view"** (:626-628). This is not so for an end the driver made (F10).
- **Four words called the glossary's** (:667-669) are not glossary terms (F10).
- **Smaller claims.** `SessionOutput` "keeps" a moment (:455-456). §9's unmeasured chat holder is answered by the code
  (:871-872). HomeWatch's limit is given as the watch's (:256, :854-855). The D203 § citations are the coworker design's. Trees are
  given as a bound on starting (:751-753) (F13).

### 2. The mechanism's holes

- **Parts shown that are not there, or missed that are.**
  - A lead that waits is `completed`, which is not live (`Web/data/api.ts:423-424`). So the lead rule moves the lead to the
    lane part it waits on, and the fold hides the waiting part among *earlier parts* (F3).
  - A quest a chat took, or a teammate took, has no executor in its unit (`QuestUnits.cs:36-37`), so it would read *not taken
    yet* (F6).
  - A part opened by a sibling request of the same ask has no routing words (F7).
  - The hook's unjoined children are counted under words that claim their agent did not announce them (F8).
  - Helpers and partners keep their own entries (`SessionEntries.cs:77`) and appear once, under *Co-workers*. Brainstorm voices
    are rightly row f's.
- **State read from text.** None. The labels are the wire's structured fields. A task's first line may name a path, which
  changes the lineage file's class (F13f).
- **The window's thread (NOBLOCK1).** The composition runs off the thread, and nothing polls. But the stream wake goes through
  `STATE_CHANGED` at ten seconds per working session. Each telling forgets every read under way, in every route (F5), and every
  composition makes two whole-store service reads (F6).
- **Routing.** The doctrine's "wait on one of them where your own work cannot go on without it" resumes the lead with the
  answer in its instruction and not the part's commits. Its start syncs the tree with the line alone (`Driver.cs:533-547`), and
  only a chain step is synced to its source (`:549-575`) (F2).
  - Whether the part runs alongside or in the lead's own tree is a race with the planner's look (`Planner.cs:1742-1767`;
    `WaitingInTree` holds a tree only for a helper's wait, `:2195-2199`, `HelperWaits.cs:36-38`).
  - The resume after a lane part's done is D79's quest door. D188 point 5 leaves that door unchanged, so no turn D188 forbids
    is started. Drive, cap and strikes bound the chain, which §6.5 should say.
  - A chat may hand rows out with no drive precondition (F11).
- **Names.**
  - The new scope `work` sits beside the pushed slice `work` (`WorkStateSlice.cs:45`). *Lead* is already the code's name for an
    entry's first part (`EntryAccepts.EntryLead`, `EntryAccepts.cs:32-44`) (F9).
  - D203's *team* is kept off the window, as round 1 asked.
- **Widths, themes, languages.**
  - The widths and themes hold.
  - `unannounced`'s zh counts 智能体 where the en counts subagents, and it ends on a dangling 的 (F10).
  - The hints' three conditions overlap and leave a case unworded (F11).

### 3. The rows

- **Row a, *a chat's subagents seen*,** can be built alone. It is the row that gives the owner the most: it is the owner's case.
  - Its unit proofs cover the fold, the door's lines, the end words, the join and the timing.
  - Its installed proof draws *completed*, *lost* and *working*, but never *ended with its part*, which §11 says it names apart
    (F12).
  - It sets the scope's name (F9) and the stream wake's route (F5), which every later row inherits.
- **Row b** can be built after a. Its proof cannot happen as written.
  - Under a cap of 1, a driven publisher holds the one slot itself (`Planner.cs:904`, `:1301`), so no lane part runs alongside
    it.
  - The other repository's session needs that slot to have started. The proof holds only with a chat publisher (CAPCOUNT1) and
    the other repository's quest started and waiting first (F12).
  - It must draw a waiting lead as live (F3). Its *runs alongside* is a race once the lead waits (F2).
- **Row c** holds. Its box wake cannot name the asker through `HomeWatch` (F1).
- **Row d** cannot refresh as §4 is written. An ask-keyed read is never invalidated by a wake that names sessions (F1).
- **Row e** depends on no view row for its skill section, which could land first, but only once F2 is answered. Its hints need
  F11's order.
- **Row f** holds, and waits on BRAINSTORM1d and AGENTROOM1b.

### 4. Findings

1. **The `work` wake's narrowing by session cannot be carried, and would miss the reads that hold the part that moved.
   Blocking.**
   - **What the design says.** §4 tells `work` "naming the session where it is one session's", and has the hub invalidate
     `SESSION_AT_WORK` "for the sessions named" (:520-523). A lineage move or a box is to tell `work` for that session or its
     asker (:539).
   - **What the wakes carry.** A ledger wake's subject is a list of families (`QuestsStateSlice.cs:132-137`). A home topic's
     wake carries its fixed subject, never which file moved (`HomeWatch.cs:316-344`, `_wake(topic, known.Subject)` at `:342`).
   - **What narrowing would miss.**
     - Attended part X's read draws part Y's state, children's count and *last heard*. A wake that names Y leaves X's query
       alone.
     - An ask's read is keyed by the ask, so it is never invalidated by a wake that names sessions. Row d's view would go
       stale.
   - **Smallest change.** The `work` scope names no session, and the hub asks every held `SESSION_AT_WORK` again. These reads are
     few (the attended views and the head), and the hub and `HeldAnswers` already coalesce them. Alternatively, name the entry
     and the ask, and key the hub's entry by them.

2. **The doctrine's wait on a lane part resumes the lead without the part's work, and may give the part the lead's tree.
   Blocking.**
   - **Without the part's work.** §6.2 has a driven lead "wait on one of them … where its own work cannot go on without that one"
     (:717-721).
     - The resume starts the lead in its own earlier tree (`Planner.cs:1773-1801`), with the answer in its instruction
       (`QuestExchange.cs:1503-1505`).
     - That start syncs the tree with the line alone (`Driver.cs:533-547`, LANDSYNC2). Only a chain step going on is synced to
       its source's submission (`:549-575`).
     - A lane part that ran alongside lands on the entry's branch, never in the lead's tree. So the lead resumes without the
       code it waited for.
   - **The lead's tree.** A D79 wait holds no tree: `WaitingInTree` reads only `ended.awaits-helper` (`Planner.cs:2195-2199`;
     `HelperWaits.cs:36-38`).
     - A lane quest the planner looks at after the lead's turn ended therefore goes *into* the lead's tree (`Planner.cs:1762-1767`),
       with whatever the lead left uncommitted. It does not run alongside.
     - Which of the two happens is a race between the lead's end and the planner's look.
   - **Smallest change.** §6.2 says three things.
     - A lead waits on a lane part for its answer, never for its code. Work the lead's next step builds on is not handed out,
       or the lead finishes its quest and lets the entry's one *Accept* join the parts.
     - The lead commits before it waits, since its tree may go to the next part.
     - The resume after a lane part's done is D79's door, unchanged by D188 point 5, bounded by the drive, the cap and the
       strikes.
   - **The alternative.** File a row that syncs a resumed lead's tree with the part it waited on, as `GoOnInSyncAsync` does for
     a step's source.
   - Either way, §1.2, §6.1 and row b say that *alongside* holds only while the lead is live, and the view reads *alongside* from
     the part's own tree.

3. **A waiting part reads as ended, so the lead moves off it and the fold hides it. Blocking.**
   - **The states.** A lead waiting on its quest (`ended.awaits`) or its helper (`ended.awaits-helper`) is `completed`
     (`Observation.cs:104-111`, `:164-165`), which is outside the page's active set (`Web/data/api.ts:423-424`).
   - **What the view does with it.**
     - §3.1 leaves *live* undefined. Read as active, its lead rule moves the lead to the newest live part (:321-323), which is the
       very lane part the lead waits on.
     - Its *This session* folds the waiting lead with the ended parts behind *{{count}} earlier parts* (:327). The person loses
       the wait the routing produces, exactly where row b's proof looks for it.
   - **Smallest change.** Define *live* as active, or completed with its quest taken and a wait code (`ended.awaits`,
     `ended.awaits-helper`). Such a part is never folded, and its own wait is the *Waiting on* line. Row b's proof has a lead
     waiting on its lane part.

4. **The turn claims are wrong: the event log keeps each turn's end, and the adapter holds the turn for its children. Not
   blocking.**
   - **The turn's end.** It is a `turn` event with its stop reason (`SessionEvents.cs:33-34`), emitted at each prompt's
     response on the protocol door (`Acp.cs:1999-2005`).
   - **The children.** CONSOLE2 measured that the adapter "held the parent's turn open until the child ended" and defers
     settling a turn for the subagents it spawned (`docs/2026-09-28-console2-streams-evidence.md:74-77`).
   - **What follows.** The grouping by process still stands. The lineage is the process's, and a turn boundary would need a join
     by time across two files. But "No record keeps a turn" (:221), "a background child holds no turn" (:223, :364-365),
     "*Turn* is not a boundary any record keeps" (:674), §8's reason (:849-851) and D173's note (D173.md:5443) are false.
   - **Smallest change.** Correct them: the events keep turns, but the lineage keeps none, and the view groups by what the
     lineage holds. On the protocol door the heading may say *its turn waits for them* where a child is working.

5. **The stream wake goes through `STATE_CHANGED`, which forgets every read under way. Not blocking.**
   - **The rate.** §4 tells `work` up to once per ten seconds per working session for *last heard* (:540-541).
   - **The cost.** Every `STATE_CHANGED` makes `DriverModule` forget every shared flight and held work (`DriverModule.cs:102-107`).
     Every window then re-asks, and each re-ask is a composition with two whole-store reads (F6). The joining that NOBLOCK1's
     heavy reads rely on weakens at the agents' rate.
   - **Smallest change.** Push *last heard* as a keyed partition of the `calls` slice, judged from memory as `quiet` is
     (`CallsStateSlice.cs:37-41`), with no `STATE_CHANGED`. The head and the view read it beside the held answer.

6. **The quests group's inputs are held by no route, and its units miss a chat's or a teammate's take. Not blocking.**
   - **The inputs.** `QuestUnits.Read` takes `QuestView`s and `SessionRecord`s (`QuestUnits.cs:20`). Routes get them from the
     service, every quest and every record (`HelpRoutes.cs:293-294`; `WorkUnits.cs:66-71`). The loop's snapshot holds open and
     taken quests and active sessions only (`Planner.cs:888-891`, `:1770-1771`), not the closed quests and ended parts the view
     draws.
   - **The executors.** A unit's executors are records whose `Quest` is the quest (`QuestUnits.cs:36-37`). A quest a chat took
     (`TookQuest`) or another machine took has none.
   - **Smallest change.**
     - §4 names the two service reads per composition, on the held work's task, and their cost per wake.
     - *Not taken yet* is said only where the quest's status is `Open`. A taken quest with no executor here reads *taken by
       {conversation}* through `TookQuest`, or *taken on another machine*.

7. **The parts group's order contradicts the strip's, and one way a part joins has no words. Not blocking.**
   - **The order.** §3.1 draws the lead first, then live parts, then ended ones folded (:327). §2 point 4, §5.1 and row b promise
     the strip's order (:307-308, :560-561, :812-813). The strip draws parts oldest first, as lineage (`SessionEntries.Read`;
     `Web/work/head/partsLife.test.ts:11`, `:88`).
   - **The missing words.** §3.4 has no routing for a part opened by a sibling request of the same ask (`SessionEntries.cs:97`;
     D167 point 2).
   - **Smallest change.** Promise the same parts, each once, ordered lead, live, ended. Row b asserts the set and one row each. Add
     *the ask's request #{quest}*.

8. **The join's residue: the count's words assert what no record holds. Not blocking.**
   - **The evidence.** The generation suffix is "according to the source" (`console2-streams-evidence.md:61-62`). Whether the
     hook's `agent_id` on a re-run is the same id or a new one is unknown.
   - **A new id.** The re-run's hook child joins nothing, and `unannounced` says *its agent did not announce* a child it did
     announce (:433-434, :644).
   - **No join at all.** If row a's measure finds that the door's ids never join the hook's, every hook child on an `acp` part
     is counted. Each child then reads twice, once as a row and once as a count.
   - **Smallest change.**
     - Word the count as the record holds it: *{{count}} more seen only by its hook*.
     - Where the measure finds no join on a door, the hook's children on that door's parts are neither rows nor a count, and
       D173's note records it.

9. **Two names collide. Not blocking.**
   - **The scope.** §11 rejects `SESSION_WORK` because "`work` already names the units and `WORK_STATE`" (:911). Then §4 names the
     scope `work` (:520-521), beside the pushed slice `work` (`WorkStateSlice.cs:45`).
   - **The lead.** The code's `EntryLead` is an entry's first part, the one its *Accept* is named for (`EntryAccepts.cs:32-44`;
     `TreesRoutes.cs:681-682`). The view's lead is the attended or the newest live part (:321).
   - **Smallest change.** Call the scope `atWork`. Say in §3.1 that the view's lead is not `EntryLead`, and name the type's field
     `attended` or `leadPart`.

10. **Words. Not blocking.**
    - **(a) Not glossary terms.** Four words §5.4 calls "the glossary's words kept" (:667-669) are none:
      - co-worker 同事, lead 主导, alongside 并行 and *not driven here* 不在本机驱动;
      - `glossary.json` holds none of them, and 同事 is in no zh catalogue;
      - so `names:check -- --strict` holds nothing for them. Add them to *New glossary terms* (co-worker and lead as D203 point 9
        names them).
    - **(b) `unannounced`'s zh.** 另有 {{count}} 个智能体未通告的 counts agents, where the en counts subagents, and ends on 的. It
      changes with F8's wording anyway.
    - **(c) The driver-made end.** The panel and the conversation's card say a child the driver closed "ended with its session"
      (`AcpStreams.cs:134-139`; `work.panel.json:20`). The view says *ended with its part* or *lost when its session stopped*.
      Either say that §5.4's sameness (:626-628) holds for the wire's words only, or let the panel's tab take the lineage's word.

11. **The hints overlap, and the chat's case has no drive precondition. Not blocking.**
    - **(a) The hints.** No order is given among the three (:738-745).
      - A repository with no lanes that is not driven gets `not-driven`, which speaks of "a quest to its lanes". That may be the
        owner's own case.
      - One with no lanes, driven, without trees of its own gets none of the three.
      - Give the order: `not-driven`, then `no-lanes`, then `one-at-a-time`. Word each for a repository with no lanes.
    - **(b) The chat's case.** §6.2 opens the way for "a chat whose person asked it" (:705-707) without the drive the driven case
      has by construction. A chat in a repository not driven here hands out rows that never start here. Add "and its repository is
      driven here" to the chat's case.

12. **Two proofs need correcting, and one row can move. Not blocking.**
    - **(a) Row b under a cap of 1** (:809-814). A driven publisher holds the slot itself (`Planner.cs:904`, `:1301`), so no lane
      part runs alongside it. The other repository's session needs the slot to have started. Name a chat publisher (CAPCOUNT1)
      and the order: the other repository's quest taken and waiting, then the lane quests.
    - **(b) Row a's installed proof** (:798-802) never draws *ended with its part*, a clean end with a child still open. Add it
      to a run, or say that only `ChildBindingsTests` holds it.
    - **(c) Row e's skill section** depends on no view row. It may land early, once F2's answer is in it.

13. **Smaller claims. Not blocking.**
    - **(a) Section citations.** "D203 §6.4", "§8.3", "§3.3" and "§3.4" (:346, :625, :768, :838) are the coworker design's
      sections, not D203's, which has points. The D203 note's "leaving `work.team.*` to this design's section"
      (D203.md:208-210) means the coworker design's.
    - **(b) §9's unmeasured chat holder** (:871-872) is answered by the code. A repository's holder is drawn from every active
      session, conversations included (`Planner.cs:1290-1292`; CAPCOUNT1's remark, `:1300`). A working chat holds a one-at-a-time
      repository, so `one-at-a-time` is true of a chat.
    - **(c) The watch's limit.** HomeWatch has no remove, but its `ChangeWatch` does (`Unwatch`, `ChangeWatch.cs:316-339`). §1.8's
      and §8's reason (:256, :854-855) is HomeWatch's surface, not the watch's. The fixed part remains a fine choice.
    - **(d) `SessionOutput`'s moment.** §3.7 says `SessionOutput` "keeps each stream's newest moment … (`SessionOutput.cs:90-110`)"
      (:455-456). It keeps none (`:9`, `:98-112`). This is §3.6's new memory.
    - **(e) Trees as a bound.** §6.5 and the D115 note (D115.md:572-573) give "with trees of its own" as part of what bounds a
      quest. A lane quest to a driven repository without trees still starts, one at a time (`Planner.cs:2010-2021`). Drive and cap
      bound whether it starts; trees decide whether it runs alongside or after.
    - **(f) The lineage's twin row.** `.claude/knowledge/twins.md:180` says a lineage value is "kept only where it is shaped as an
      id … so no path is written". A task's first line can name a path. Row a amends the twin row, and the D173 note says the
      file becomes transcript-class, kept on this machine (D47).

**Findings: 13, of which 3 are blocking (1-3).**

**Verdict: accept after named changes the steward can check without another read.** Round 2 takes every round-1 finding, and
each answer holds against the code. F7's premise was wrong in part, but the answer it drew still stands. The foundations now hold:
- a lane part is a part, drawn once;
- the `acp` line carries its parent and only the wire's end;
- the wakes reach the held read through the hub;
- the routing names its rule and its preconditions.

What remains is narrower:
- F1: the `work` wake must ask every held `SESSION_AT_WORK` read again (one sentence in §4).
- F2: §6.2 must say that a lead waits on a lane part for its answer, never its code, and commits before it waits, or file the
  resume-sync row.
- F3: §3.1 must count a waiting part as live.

Each is a sentence or a rule that the steward can check against the lines cited, and F4-F13 can be taken in passing. Under D191
the design still waits on Codex's read.

## Codex, round 2

Codex's read of round 2 with its named changes at `972b17577`, owed since round 1 when its credits ran out: 20 findings, 11 blocking; another round. The steward ran it read-only on main from outside the install. Round 3 answers it in the design's §13.

Read-only review; nothing changed. Paths below use the design’s `Driver/`, `Modules/`, `Core/`, `Mcp/` and `Web/` aliases.

### 1. Wrong claims about existing code

1. **Non-blocking — helpers consume the machine cap.** The design’s §6.5 says helpers run outside that cap (`docs/2026-10-24-cowork-view-design.md:870`). In fact, `Driver/Loop/DriverHelpers.cs:130` subtracts held slots before admitting one, and `Driver/Loop/Driver.cs:1006` counts helpers before planning quests; correct the rationale for rejecting a separate self-quest rule.

2. **Non-blocking — no local executor does not prove another machine took the quest.** The fallback at design line 375 overlooks a take made locally outside a recorded conversation. `Driver/Quests/Domain/Planner.cs:1758` explicitly distinguishes that case; use the take’s provenance, or say “taker unknown,” rather than “taken on another machine.”

3. **Non-blocking — SELFASK1 has landed in this checkout.** Design lines 309–312 still describe the old refusal and an open fix. `Core/Quests/QuestExchange.cs:542` now names lanes or their declaration, and `Mcp/KnowledgeSearchTools.cs:329` supplies the registry explanation; update the present-tense claims and row e’s dependency.

### 2. The read and its updates

1. **Blocking — historical waiting parts remain live after replacement.** Design line 357 tests `completed`, a taken quest and a wait code, without excluding a superseded executor. A D79 resume creates another record (`Core/Sessions/SessionLedger.cs:1613`) while the old waiting record remains completed, so both qualify while the quest stays taken; define current waiting responsibility and test “wait → answer → replacement working.”

2. **Blocking — an application crash leaves children falsely working.** `Driver/Trees/Orphans.cs:81` ends the abandoned session record without appending a lineage end, while `Driver/Sessions/Domain/ChildBindings.cs:278` leaves an unclosed child active. Design lines 459–462 therefore cover a root killed while its driver survives, but not a driver crash; specify recovery from terminal part evidence without inventing a harness end.

3. **Blocking — reopening a child does not reset its terminal state.** The generation join relies on the existing fold’s reopening behavior (design lines 485–486), but `ChildBindings.cs:177` resets only `Closed`; `Stopped` remains true and wins at line 278. Specify state and moment ownership per execution generation, and test a finished native ID appearing again as working.

4. **Blocking — the fixed watch misses dependencies and its first change.** Design line 605 stamps lineage only for sessions whose reads are held, although X’s read includes sibling Y’s children and an ask-keyed read includes several sessions. Moreover, `Driver/Platform/HomeWatch.cs:331` deliberately accepts a newly named file’s first stamp without waking; register every dependency with the composition’s baseline and test a final append between composition and watch registration.

5. **Blocking — scheduling and configuration changes have no complete wake path.** The design’s wakes at lines 599–608 omit changes to the driver’s considerations, driving choices and registry facts, although these determine waiting reasons and hints. `Modules/Loop/DriverLoop.cs:984` updates considerations and line 1027 emits `DRIVER_TICK` independently of ledger changes; test a changed cap/reason and driving configuration with no session or quest write.

6. **Non-blocking — background composition alone does not prove NOBLOCK1.** `Modules/Bridge/HeldAnswers.cs:154` serializes the answer while holding the same lock acquired by an incoming ask at line 78, and the proposed answer has no member-count bound. Extend row a’s timing proof to concurrent large-answer completion and multiple held reads; also exclude composition-only `at` changes from equality, otherwise design line 545 makes unchanged answers appear changed.

Shared-host leakage: **none found**. The new lineage material remains desktop-only, and `src/Daoris.Service/Daoris.Service.Http/Program.cs:358` confines static UI serving to local mode; line 2523 also requires local mode for machine-local answers.

### 3. The routing half

1. **Blocking — the chat’s driving precondition has no specified source.** Design line 800 requires a chat to know its repository is driven here, but line 1022 expressly leaves supplying that knowledge undesigned. `Mcp/KnowledgeSearchTools.cs:293` answers registry ownership and lanes, without the machine’s driving choice; provide desktop-owned eligibility in the chat’s context, or define a conservative rule for unknown eligibility.

2. **Non-blocking — explicitly exclude sessions holding restricted roles.** “One the driver started” at design line 799 also describes helpers and reviewers, whereas `Mcp/RoleFilter.cs:61` refuses serving records quest publication and response. Scope the doctrine to eligible leads and conversations; otherwise its instructions conflict with those roles even where enforcement correctly refuses them.

I found no new cross-repository edit, account-selection bypass or automatic landing door. The revised “answer, never code” rule and committing before a D79 wait match the planner and resume paths.

### 4. Decided rules and UI contracts

1. **Blocking — hiding unmatched hook evidence breaks truthful partial visibility.** Design line 492 suppresses all hook-only children when a door’s IDs never join; independently, `Driver/Sessions/ChildLineage.cs:137` turns an unreadable file into an empty fold. Both can erase evidence or render “Nobody else” without establishing absence, contrary to `docs/decisions/D173.md:11`; retain uncorrelated observations without counting them as distinct children, and distinguish unreadable from empty.

2. **Non-blocking — loading, failure and retained-stale states need designs.** The read returns pending at design line 583, but §5 specifies only populated and empty views. `Web/bridge/state/held.ts:34` passes a first pending answer through the reader, so define its rendering and stories explicitly, following `docs/decisions/D182.md:559`; translation parity alone cannot prevent a false empty state.

3. **Non-blocking — the remaining architectural boundaries hold.** The entry-part treatment follows D167, native children remain outside D203’s co-workers, and the proposed labels preserve English/Chinese placeholder parity. I agree with Opus’s grouping and resume corrections, but disagree with round-2 finding 3’s sufficient live predicate (`docs/2026-10-24-cowork-view-reads.md:527`), finding 8’s evidence suppression (line 581), and the resulting acceptance verdict (line 642).

### 5. The rows and their order

1. **Blocking — row a needs the lifecycle and watch contracts above.** Its current proofs at design lines 904–924 miss superseded waits, repeated finished IDs, application crashes and dependency-registration races. It remains the right first visible feature, once those cases have expected answers.

2. **Blocking — row e cannot precede observable routing eligibility.** Moving its doctrine forward is reasonable, but design line 927 makes it independently buildable while its chat precondition remains unavailable. Resolve that source first; row b then follows a, with the corrected live predicate and scheduling wakes.

3. **Blocking — row c promises a thread that another feature has not supplied.** Design line 960 requires opening the co-worker’s thread, but `docs/2026-10-23-coworker-design.md:671` assigns that surface to COWORKUX1b. Depend on that row and its prerequisites, or explicitly open today’s session page until the thread exists; sharing an exchange reader alone does not supply navigation.

4. **Non-blocking — row d needs an ask terminal twin.** Its ask view at design line 962 has no corresponding ask form of the terminal command declared at line 899. Add an explicit ask argument or verb and its output test to satisfy D185’s terminal-twin rule (`docs/decisions/D185.md:24`).

5. **Blocking — row f adds records without adding their wakes.** Its request/state files live under brainstorm and meeting directories (`Driver/Brainstorms/MeetingFiles.cs:29`), outside §4’s three watched parts. Add those dependencies and prove a meeting-state-only change updates the view; its existing caller-door prerequisites otherwise hold.

### 6. A smaller shape with the same outcome

1. **Non-blocking — compose existing presentations around one dependency-aware read.** Keep the existing parts and quest adapters, reuse D203’s exchange reader and thread, add the native-child tree, and reuse channel and meeting surfaces through links (`Web/work/AttendedSession.tsx:323`; `docs/2026-10-23-coworker-design.md:661`). Share source acquisition across held payloads, while keeping per-payload answers and the `calls.heard` partition; this preserves the outcome with fewer duplicated readers and UI contracts.

**Verdict: another round.** Resolve current-versus-historical responsibility, child execution generations and crash recovery, dependency registration and wakes, and observable routing eligibility before acceptance.

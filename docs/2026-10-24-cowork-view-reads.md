# COWORKVIEW1: the reads of round 1 (D191)

The independent Opus 5.5 reader's foundation read of `docs/2026-10-24-cowork-view-design.md` and its D173 note at `ccb2e5a45`, verbatim but for its headings set one level down. The steward ran it from outside the install, read-only, on main. Codex's read stopped when its credits ran out and is owed. The Opus foundation reader asks another round (19 findings, 10 blocking). Round 2 answers it.

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

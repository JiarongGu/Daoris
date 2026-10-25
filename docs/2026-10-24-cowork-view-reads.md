# COWORKVIEW1: the reads of rounds 1 to 5, the Opus foundation reader's of each and Codex's of rounds 2 to 5 (D191)

The independent Opus 5.5 reader's foundation read of `docs/2026-10-24-cowork-view-design.md` and its D173 note at `ccb2e5a45`, verbatim but for its headings set one level down. The steward ran it from outside the install, read-only, on main. Codex's read stopped when its credits ran out and is owed. The Opus foundation reader asks another round (19 findings, 10 blocking). Round 2 answers it. Codex's read of round 2 with its named changes follows the Opus reads (another round, 20 findings, 11 blocking); round 3 answers it. Round 3 was read by both: the Opus foundation reader would accept after named changes (11 findings, 3 blocking), and Codex asks another round (19 findings, 13 blocking); round 4 answers both. The Opus foundation reader read round 4 and would accept row a after named changes (12 findings, 4 blocking), which the design's §15 places; Codex read round 4 with those changes and asks another round for row a (11 findings, 7 blocking), which round 5 answers in the design's §16. Both read round 5 at `6736d2db9`: Codex asks another round for row a (10 findings, 4 blocking), and the Opus foundation reader would accept it after named changes (9 findings, 2 blocking); round 6 answers both in the design's §17.

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

## Opus (foundation), round 3

The independent Opus 5.5 reader's foundation read of round 3 at `308edb13d`: 11 findings, 3 blocking; accept after named changes. The steward ran it read-only from outside the install. Round 4 answers it in the design's §14.

### 0. Codex's round-2 findings

I read everything at `308edb13d`. Its `src/` is the same as `972b17577`'s, because round 3 changed only docs. Paths use the design's abbreviations. Design line numbers refer to `docs/2026-10-24-cowork-view-design.md` at `308edb13d`.

| # | Codex's finding | Status | Checked against the code |
|---|---|---|---|
| 1.1 | Helpers consume the cap | Taken; holds | `Driver/Loop/DriverHelpers.cs:130` subtracts the slots already held, and `Driver/Loop/Driver.cs:1006` judges helpers before the quests are planned. §6.5's new reason (the `helpers` rule is its own admission outside the plan) holds. |
| 1.2 | No local executor does not prove another machine took the quest | Taken; holds | `Holder` and `Here` are at `Driver/Quests/Domain/Planner.cs:33-37`, and `TakenElsewhere` is at `:1739-1765`. Residue: the live predicate never asks whose take it is (F4). |
| 1.3 | SELFASK1 has landed | Taken; holds | `Core/Quests/QuestExchange.cs:539-552`; `NoLanesWay` at `:759-761`; `Mcp/KnowledgeSearchTools.cs:326-330`. |
| **2.1** | Historical waiting parts stay live | Taken; holds in shape | A resume opens a new record (`Core/Sessions/SessionLedger.cs:1612-1616`). `Awaits` is kept after the answer (`Planner.cs:39-50`), and `ResumedOnAnswer` is at `:1577-1581`. Residue: "the newest record here" is not the planner's `LastRun` (F4). |
| **2.2** | An application crash leaves children working | Taken; holds | The sweep moves a record to `stopped` and writes no lineage line (`Driver/Trees/Orphans.cs:81`). `End` writes only to a file that exists (`Driver/Sessions/ChildLineage.cs:118-122`). The `start` line and the close at the record's own end answer the finding. Residues: the run rule drops Codex's children (F1), and the close moment is when Daoris noticed, not when the child stopped (F11e). |
| **2.3** | Reopening a child does not reset its terminal state | Taken; holds | `ChildBindings.cs:177` clears only `Closed`, and `:278` lets `Stopped` win. Runs answer it. Residue: F1. |
| **2.4** | The fixed watch misses dependencies and the first change | Taken in part | `HomeWatch.cs:331` is as Codex said. Composition stamps close the race. But comparing with a composition's stamp wakes again at every judgement until that composition is redone, and a held answer that no page asks again is never redone (F2). |
| **2.5** | Scheduling and configuration changes have no complete wake path | Taken in part | `DRIVER_TICK` goes out whenever the signature of the considerations moves (`Modules/Loop/DriverLoop.cs:940-943`, `:1025-1027`), so reading the tick's reason beside the answer holds. `driver.json` reaches the driver slice at each look (`:987`). The registry wake does not carry lanes (F3). |
| 2.6 | Background composition alone does not prove NOBLOCK1 | Taken; holds | `HeldAnswers.cs:78` and `:154` take the same gate. The 40-member bound, serialising off the gate and dropping `at` answer it. |
| **3.1** | The chat's driving precondition has no source | Taken; holds in shape | `registry` answers lanes but not this machine's drive (`KnowledgeSearchTools.cs:288-330`). BRWHINT1's places are `ChatRunner.cs:1382` and `:1920`, and `Agents/Adapters/Adapters.cs:334-339`. Residue: nothing records what standing was told, and the hint can differ from it (F10). |
| 3.2 | Exclude sessions holding restricted roles | Taken; one claim wrong | The connector refuses the quest tools only to a record with `serves` (`Mcp/RoleFilter.cs:58`, `:77`). A reviewer's record carries `opinion` and no `serves` (`Core/Review/OpinionDesk.cs:128-131`) (F6). |
| **4.1** | Hiding unmatched hook evidence | Taken; holds | `ChildLineage.cs:135-138` folds every `IOException` as empty, a missing file included, so the three outcomes are new work as the design says. Residue: the empty sentence drawn beside unmatched starts (F9). |
| 4.2 | Loading, failure and held states | Taken; holds in shape | `held.ts:18-34`; `HeldAnswers.cs:96-100`, `:164-165`. Residue: the `undefined` sentinel (F8). |
| 4.3 | The boundaries hold; three disagreements with the Opus reads | Taken | §13 decides both disagreements on the code as cited, and both decisions hold. |
| **5.1** | Row a needs the lifecycle and watch contracts | Taken | Residues: F1 and F2. |
| **5.2** | Row e cannot come before observable routing eligibility | Taken; holds | The judge and the line now come first. Residue: one of its proofs cannot pass (F3). |
| **5.3** | Row c promises a thread no row supplies | Taken; holds | Residue: COWORKUX1a's read holds helpers only (F7). |
| 5.4 | Row d needs an ask terminal twin | Taken; holds | `Driver.Host/Program.cs:470-475` routes `ask channel` before the ask's other words; D185.md:24-25. |
| **5.5** | Row f adds records without their wakes | Taken; holds | A meeting sits two folders down (`brainstorms/meetings/<id>/`, `MeetingFiles.cs:29-38`). Reading the dependencies by name covers that. |
| 6.1 | A smaller shape with the same outcome | Taken where the outcome holds; holds | `RouteFlights` takes any key (`Modules/Bridge/RouteFlights.cs:33-56`), so the service reads can be shared across payloads. |

### 1. Claims about the code

Round 3's new citations hold:
- `ChildBindings.cs:177`, `:278`
- `Orphans.cs:81`
- `ChildLineage.cs:118-139`
- `SessionLedger.cs:1612-1616`
- `Planner.cs:33-50`, `:1577-1581`, `:1739-1765`
- `DriverLoop.cs:984`, `:987`, `:1019`, `:1025-1027`
- `StateSlices.cs:378`
- `Web/bridge/driver.ts:337`
- `Web/asks/AskWork.tsx:56`
- `HeldAnswers.cs:78`, `:96-100`, `:145-165`
- `held.ts:20-34`
- `HomeWatch.cs:331`
- `DriverHelpers.cs:130-131`
- `Driver.cs:1006`, `:3892`
- `ChatRunner.cs:1382`, `:2524`
- `Adapters.cs:334-339`
- `QuestExchange.cs:539-552`, `:759-761`
- `KnowledgeSearchTools.cs:326-330`
- `Program.cs:470-475`
- `MeetingFiles.cs:29-38`

These claims are wrong:
- **"A call line … alone opens no run" (§3.6, :558-563).** A Codex child exists only through its call lines (`ChildBindings.cs:154-157`, `:223-233`; Codex says no thread's start or end, D173.md:121-122). The rule erases every Codex child, against §2 point 3 (:393), §3.1 (:466-467), §5.2 (:825), the story at :860 and row a's proof "a Codex child `ended`" (:1110-1111) (F1).
- **"A registry the tick heard move" tells `atWork` when lanes move (§4 :754-756; D173.md:5506).** `Repositories.Signature` is repository, adopted, workspace and root (`Planner.cs:733-736`), and `RepoView` has no lanes (`:722-723`). The change stream has no registry family (`Core/Platform/ChangeStream.cs:12-48`) (F3).
- **The current executor is "the record the planner reads as the quest's last session here (`LastRun`)" (§3.1 :420-424; D167.md:402).** `ReadLastRun` reads `quest` only, never `tookQuest`. It skips teammates' records (`ServiceClient.cs:2140`), stood-down records (`:2142`) and not-offered records (`:2145`, D205 §3.5(c)). On a tie it takes list order, not id (`:2148`) (F4).
- **"A held read let go … drops its files" bounds the watch (§4 :747-749).** `HeldAnswers` lets an answer go only once 128 are held (`HeldAnswers.cs:40`, `:181-188`). The hub's invalidation refetches only mounted queries (`Web/bridge/state/stateHub.ts:80`, TanStack's default), so the files of a held answer nobody asks again stay in the union indefinitely (F2).
- **"A helper, a partner, a reviewer: the connector refuses them `quest_publish` and `quest_respond`" (§6.2 :980-981; D203.md:226-227).** That is true of helpers and partners only. Reviewers get `serves` with COWORKUX1c (coworker design §4.1, row 4 at :674) (F6).
- **The co-workers are "helpers, partners and reviewers" from COWORKUX1a's exchange read (§3.1 :450, §3.9 :699, row c).** That read is the line's helper asks (coworker design §6.3 :490-497, row 1 :661-664). Reviewers and partners join at COWORKUX1c and COWORKUX1e (:674-681) (F7).
- **"Its page reader reads the pending answer as not answered yet (`undefined`)" (§4 :711-713).** TanStack Query 5 (`src/Daoris.Web/package.json:30`) fails a query whose function resolves `undefined`, and the app retries once (`Web/main.tsx:31`) (F8).
- **"The hint and the line never disagree" (§6.4 :1040-1042; D115.md:588).** They share a judge, not a moment. §6.2 itself says a moved standing is told only at the next start (:995-996) (F10).

### 2. The read and the view

- **Members shown live that are not, or missed that are.**
  - Every Codex child is missed (F1).
  - The current-executor rule fixes C2.1's double live part. It can fold a waiting lead under a resume that its agent refused before any prompt, a record the planner itself skips. It also reads a record here as waiting when its quest was carried on by another machine (F4).
  - A part is drawn once and matches the strip. Lane parts, *took*, the ask's sibling requests and helper waits are placed as §3.1 says.
  - Nested children and *parent not said* hold.
- **Crash, resume and restart.**
  - An application crash is closed by the record's end once the sweep runs. A go-on's `start` closes the earlier process. A D79 resume's waiting predecessor folds.
  - The close moment of rules 2 and 3 is the sweep's `updated` or the next start, which can be hours after the child stopped (F11e).
  - No doubled member was found: a re-run is a new run of one row, and unmatched hook starts are a count.
- **Watches.**
  - The ledger families hold. The tick's reason read beside the answer holds. Drive and trees hold through the driver slice: `Pushed` takes no arguments, so its hearer compares `drivable` and `trees` itself (`DriverStateSlice.cs:36`).
  - Lanes are missed (F3).
  - The lineage watch's comparison with composition stamps wakes again until recomposition, and indefinitely for a held answer no page asks again (F2).
  - A part's missing lineage file must be a dependency stamped absent. The case's first child is what creates that file (`lineage-hook.mjs:49`; `ChildLineage.cs:92`), and `HomePart.ByName` already stamps absence (`HomeWatch.cs:13-16`) (in F2).
- **Holding the window or answering late.**
  - Composing off the thread, serialising off the gate and sharing the two service reads all hold.
  - Every lineage line moves a wake, and every Daoris call writes two lines: the hook's on `PreToolUse` for `mcp__daoris-knowledge__.*` (`ChildLineage.cs:41`) and the connector's (`Core/Sessions/LineageFile.cs:59-70`). `ChangeWatch` coalesces at 250 ms to 2 s (`ChangeWatch.cs:177-180`). Each wake tells `STATE_CHANGED`, which forgets every route's flights and held works (`DriverModule.cs:106-111`).
  - That is the cost §4 rejects for *last heard* at one per ten seconds (:758-761) (F5). F2's repeated wakes add to it.
- **Shared host.** I found no leak. The lineage, the answer (whose labels may name a path), the standing and both verbs stay on the desktop. `ANSWERED` names the query's hash, never the payload (`HeldAnswers.cs:24-26`). The change stream carries families only.
- **Empty states.**
  - Reading, failed, held and in-part are now designed.
  - The `undefined` sentinel draws reading as a failure (F8).
  - The empty sentence can sit beside unmatched starts (F9).
  - `useConsidered` is `[]` until the first tick (`Web/bridge/driver.ts:333-340`) (F11d).

### 3. The routing half

- **Across ownership.** No. Work goes only to the session's own lanes or to another repository by quest, and Daoris writes no lanes file.
- **Past the person's door.** No new door.
  - Lane parts start under the drive (`Planner.cs:1940`), the cap, the strikes and an ask's pause (`:1357`; an ask-origin part's quests are the ask's).
  - They land only under the entry's *Accept*.
  - Outside acts keep `go_ahead_ask`.
- **Accounts and agents.** `quest_publish` names no agent or account (`Mcp/QuestTools.cs:34-75`). The planner chooses by the person's rules. Native children run on the lead's account, as the note says.
- **Roles.** Helpers and partners are refused the quest tools. Reviewers are not refused until COWORKUX1c, although the section's first line excludes them (F6).
- **The standing.**
  - It is observable to the agent, in the first message's briefing, the instruction and at each later start.
  - Its default with no line told is the conservative one, so doctrine still works without a service.
  - Its judge (drive list, `OpensOwnTree`, the registry's lanes) matches the planner's gates (`Planner.cs:1775`, `:1940`).
  - It is not observable to the person: nothing records which standing was told (F10).
  - The view's hint can stay stale when lanes are declared (F3).

### 4. The rows

- **Row a.** It is buildable first and alone, and it is the owner's case.
  - Its generation tests prove C2.2 and C2.3, and its *Process* case proves the sweep. Its watch tests prove the race and the sibling's and ask's appends. The person's two runs prove *completed*, *lost* and *working*.
  - As written, its fold contradicts its own "a Codex child `ended`" (F1).
  - No test holds the repeated wake (F2).
  - Its page test expects reading where the query would fail (F8).
- **Row e.** It is buildable after a. Its first half stands alone.
  - The judge's table and the line at each start prove C3.1.
  - "The hint moving … when the registry gains its lanes, with no session or quest written" (:1165-1166) cannot pass (F3).
- **Row b.** It is buildable after a and stands alone.
  - The cap-1 run works only if the other repository's question goes to a repository not driven here; otherwise that question takes the one slot first (F11b).
  - "The lane part finishes" (:1185) must close its quest done, by an answer or its own *Accept*. A done held for the person's yes keeps the lead waiting (F11a).
  - Its current-executor tests should include a refused resume (F4).
- **Row c.** It comes after b and COWORKUX1a. Its helper proof proves its claim. It draws partners and reviewers only after COWORKUX1e and COWORKUX1c (F7).
- **Row d.** It comes after c and its twin holds. The tick's reason, the co-workers and the channel are read beside the answer, and the twin does not print them (F11f).
- **Row f.** It comes after BRAINSTORM1d and AGENTROOM1b. Its proof (a meeting's state alone moving the row) proves C5.5.

### 5. Findings

1. **The run rule erases every Codex child. Blocking.**
   - **What is wrong.** §3.6 (:558-563) says a run begins only at a start the harness said, and "a call line … alone opens no run". A Codex child is known only from its calls (`ChildBindings.cs:223-233`), and Codex says no start (D173.md:121-122). So §2 point 3, §3.1's Codex lead and row a's proof all fail.
   - **Smallest change.** Where a harness says no start (Codex), a child's run begins at its first call line in a generation and ends at that generation's close. A call after a stop the harness said still opens nothing.
2. **The watch wakes again until a composition is redone, and indefinitely for a held answer no page asks again. Blocking.**
   - **What is wrong.** §4 (:738-749) compares each file with the stamp its composition read. Today the judge moves its baseline after each judgement (`HomeWatch.cs:337`); the design drops that. `HeldAnswers` keeps 128 answers (`:40`, `:181-188`), and the hub refetches only mounted queries (`stateHub.ts:80`).
   - **What follows.**
     - A closed view's answer wakes `atWork` at every hint on its files and at every 30 s deadline (`HomeWatch.cs:172`).
     - A mounted view's recomposition starts another composition at each hint (`HeldAnswers.cs:88-92`).
     - Each wake forgets every route's flights (`DriverModule.cs:106-111`).
   - **Smallest change.**
     - A composition's files wake once: on a wake they leave the union until that read is asked again and lands.
     - A part's missing lineage is a dependency stamped absent.
     - Row a's tests add: a held read no page asks again wakes once and then nothing, and three appends during one composition start one more composition, not three.
3. **The registry wake cannot hear lanes. Blocking.**
   - **What is wrong.** §4 (:754-756) and the D173 note rely on `RegistryHeard`. Its signature has no lanes (`Planner.cs:722-736`), and no change family carries the registry. A `connect` that declares lanes therefore moves nothing, and row e's proof (:1165-1166) cannot pass.
   - **Smallest change.** `Repositories.Signature` gains each repository's declared lane ids, read with the tick's registry. Alternatively, the composition keeps the lanes it read as a dependency compared at each tick.
4. **The current executor is not the planner's `LastRun`, and ignores whose take it is. Not blocking.**
   - **Where.** §3.1 (:420-433); D167.md:402; `ServiceClient.cs:2134-2150`; `Planner.cs:1739-1765`.
   - **Smallest change.** Read the current executor by `LastRun`'s own rule, as one shared function: skip teammates', stood-down and not-offered records, and take list order on a tie. Then let `TookQuest` override. Read a part as waiting or answered only where the take is this machine's (`Holder` is `Here` where the host answers both).
5. **The lineage wake tells `STATE_CHANGED` at the connector-call rate, the cost §4 rejects for *last heard*. Not blocking.**
   - **Where.** §4 (:733-737) against :758-761; §3.7's fourth source and `heard.at` (:626, :674) move the answer on every call.
   - **Smallest change.** The judge reads the lines appended since the stamp. It wakes at once for any line but a `call`, and for `call` lines at most once per ten seconds per file. Alternatively, drop the lineage's newest `at` where `calls.heard` holds the part.
6. **Reviewers are not refused the quest tools today. Not blocking.**
   - **Where.** §6.2 (:980-981); D203.md:226-227; `RoleFilter.cs:58`, `:77`; `OpinionDesk.cs:128-131`.
   - **Smallest change.** Say the connector refuses helpers and partners now, and reviewers once COWORKUX1c gives their record `serves`. Until then the section's first line excludes reviewers in words.
7. **COWORKUX1a's exchange read holds helpers alone. Not blocking.**
   - **Where.** §3.1 :450, §3.9 :699, row c (:1190-1199); coworker design :490-497, :661-681.
   - **Smallest change.** Row c draws what that read holds. Reviewers and partners appear as COWORKUX1c and COWORKUX1e extend it, with no change here.
8. **The `undefined` pending sentinel turns reading into a failure. Not blocking.**
   - **Where.** §4 :711-713; `package.json:30`; `main.tsx:31`.
   - **Smallest change.** Map the pending answer to a marker value, such as `{ reading: true }`, that the view draws as reading. The page test asserts that the query is not in error.
9. **The empty sentence beside unmatched starts contradicts itself. Not blocking.**
   - **What is wrong.** §3.6 (:590-594) says those starts never withhold *Nobody else is working on this*. Yet :597-599 says drawing that sentence over a hook that saw starts is what ADR-B forbids (D173.md:11-12).
   - **Smallest change.** Where a part's subagents hold only unmatched starts, the empty sentence is not drawn, and the count stands in its place, with its story.
10. **The standing told is not recorded, and "never disagree" overclaims. Not blocking.**
    - **Where.** §6.2 :983-996; §6.4 :1040-1042; D115.md:588.
    - **Smallest change.**
      - The start that tells the line writes one coded note on the record naming the standing told.
      - The view says *told: {standing}* where it differs from the hint's.
      - The notes say "the same judge", not "never disagree".
11. **Smaller. Not blocking.**
    - (a) **Row b's resume proof (:1183-1186).** Say that the lane quest closes done, by an answer or by its part's own *Accept* (ACCEPTALL1b). A done held for the person's yes keeps the lead waiting.
    - (b) **Row b's cap-1 run (:1174-1179).** The other repository's question goes to a repository not driven here.
    - (c) **A drawn child whose finished parent folded or fell past the 40 bound (§3.8 :656-657, §5.2 :821-822).** Its `parentKey` names no member. Keep a drawn child's ancestors, or draw it *under a folded child*.
    - (d) **Before the first tick `useConsidered` is `[]` (`driver.ts:333-340`).** An untaken row then says *not taken yet* with no reason, never an empty `{{why}}`.
    - (e) **A child closed by rule 2 or 3 (§3.6 :552-556).** It carries no end moment of its own. It reads *lost*, with its last line's moment as *last heard*, never the sweep's `updated`.
    - (f) **Row d's twin (:1200-1202).** It prints the answer's groups. Say that the tick's reason, the co-workers and the channel are not in it, or read the tick's considerations there too.

**Findings: 11, of which 3 are blocking (1-3).**

**Verdict: accept after named changes the steward can check without another read.** Round 3 takes all 20 of Codex's round-2 findings, and each answer holds against the code in shape. The current executor, the process generations and runs, the composition's dependencies, the tick's reason read beside the answer, the routing standing told at each start, unmatched starts as a count, and the reading and failed states all answer what Codex found. The rows now build in the order a, e, b, c, d, f. Three of the new contracts are wrong as written, and each needs a sentence:
- F1: a Codex child's run begins at its first call.
- F2: a file wakes once until its read is asked again and lands, and a missing lineage file is a dependency stamped absent.
- F3: the registry's signature carries the declared lanes.

The steward can check each against the lines cited. F4 to F11 can be taken in passing.

## Codex, round 3

Codex's read of round 3 at `308edb13d`: 19 findings, 13 blocking; another round. The steward ran it read-only on the design's branch from outside the install. Round 4 answers it in the design's §14.

Reviewed read-only at `308edb13d`; nothing changed. I did not read `local/` or files outside this checkout.

`Design` below means `docs/2026-10-24-cowork-view-design.md`. Other paths use its `Driver/`, `Modules/`, `Core/`, `Mcp/` and `Web/` aliases.

#### 0. Round-2 findings

Severity is the original finding’s severity; “taken” means resolved in the proposed design, not implemented.

| Finding | Severity | Disposition and evidence |
|---|---|---|
| 1.1 Helpers consume the cap | **non-blocking** | **Taken** — corrected rationale, `Design:1067`. |
| 1.2 No executor does not establish another machine | **non-blocking** | **Taken** — provenance and unknown fallback, `Design:451`. |
| 1.3 SELFASK1 landed | **non-blocking** | **Taken** — present-tense account corrected, `Design:361`. |
| 2.1 Historical waiting responsibility | **blocking** | **Open** — replacement handled, but the claimed `LastRun` selector differs from code; §1.1 below. |
| 2.2 Crash recovery | **blocking** | **Open** — ordinary orphan stop covered; completed waits escape generation closure; §2.3. |
| 2.3 Repeated finished child ID | **blocking** | **Taken** — explicit starts create fresh runs, `Design:558`; a new call-only regression remains. |
| 2.4 File dependencies and registration race | **blocking** | **Taken** — all folded parts and composition baselines specified, `Design:738`. |
| 2.5 Scheduling/configuration wakes | **blocking** | **Open** — tick reuse holds; the named registry signature cannot detect lanes changing; §1.2. |
| 2.6 Window blocking and equality | **non-blocking** | **Taken** — off-gate serialization, bounded members and no composition timestamp, `Design:775`. |
| 3.1 Chat eligibility source | **blocking** | **Taken** — desktop supplies standing; unknown means work locally, `Design:983`. |
| 3.2 Restricted roles excluded | **non-blocking** | **Taken** — explicit scope, `Design:979`; existing reviewer enforcement is overstated. |
| 4.1 Truthful partial visibility | **blocking** | **Open** — unreadable distinguished and starts retained, but their treatment still permits false emptiness; §4.1. |
| 4.2 Loading/failure/stale states | **non-blocking** | **Open** — initial states designed; failed refreshes silently retain apparently current answers, `Design:808`. |
| 4.3 Remaining boundaries | **non-blocking** | **Taken** — D203 categories and D167 grouping remain intact, `Design:398`. |
| 5.1 Row a’s contracts | **blocking** | **Open** — lifecycle and late-answer cases below need expected outcomes. |
| 5.2 Row e’s eligibility dependency | **blocking** | **Taken** — source now precedes doctrine, `Design:1155`. |
| 5.3 Row c’s unavailable thread | **blocking** | **Taken** — COWORKUX1a prerequisite and session-page fallback, `Design:1190`. |
| 5.4 Ask terminal twin | **non-blocking** | **Taken** — explicit command and proof, `Design:1200`. |
| 5.5 Row f’s wakes | **blocking** | **Open** — existing files covered; newly created requests have no discovery dependency; §5.6. |
| 6.1 Smaller composition | **non-blocking** | **Open** — reader reuse adopted; `RouteFlights` does not establish the promised once-per-burst acquisition; §1.4. |

#### 1. Wrong claims about existing code

1. **Blocking — “newest record” is not the planner’s `LastRun`.** `Design:420` includes `Quest ?? TookQuest`, orders by creation then ID, and treats every earlier record as history; `Driver/Service/ServiceClient.cs:2140` excludes teammate records, stood-down records and choices refused before prompting, reads only `quest`, and resolves equal creation times by answer order. Define the responsibility selector explicitly, including chat takes and unsuccessful replacement attempts, and test a refused resume leaving the earlier runnable responsibility intact.

2. **Blocking — the cited registry wake cannot observe lanes changing.** `Design:750` relies on `RegistryHeard`, but `Driver/Quests/Domain/Planner.cs:733` builds its signature from repository, adoption, workspace and root only; `Modules/Loop/DriverLoop.cs:1333` uses precisely that signature. Add lane-sensitive change detection and prove a lanes-only registration change updates the hint without another write.

3. **Non-blocking — reviewers are not currently refused those tools through this role filter.** `Design:980` and `docs/decisions/D203.md:226` describe that enforcement as existing, but `Mcp/RoleFilter.cs:114` reads only `serves`/`piece`, while `Core/Review/OpinionDesk.cs:128` creates reviewers with `opinion` and no `serves`. Keep the explicit doctrine exclusion, but identify reviewer enforcement as COWORKUX1c’s work (`docs/2026-10-23-coworker-design.md:674`).

4. **Blocking — `RouteFlights` shares overlapping requests, not a burst’s completed inputs.** `Design:771` promises two service reads however many payloads compose; `Modules/Bridge/RouteFlights.cs:71` removes a flight before answering its callers. Fast reads can finish before the next payload joins, so specify a burst/generation owner that retains those inputs and test sequential, immediately completing acquisitions—not only overlapping slow ones.

#### 2. The read and its updates

1. **Blocking — start-only runs discard valid call-only children.** `Design:558` permits only hook, stream or ACP starts to open a run, and expressly forbids a call from doing so; Codex’s existing children are established entirely by calls at `Driver/Sessions/Domain/ChildBindings.cs:223`. Preserve first-observed call-only children with an unknown start, while preventing a late call from reopening an already closed run; `Daoris.Desktop.Driver.Tests/Sessions/ChildBindingsTests.cs:125` already supplies the regression fixture.

2. **Blocking — process generations need process ends, not only the enclosing record’s end.** `Design:544` names the two outer start/end locations, but `Driver/Loop/DriverContinuations.cs:1242` and `:1299` start additional native processes within the same record, before the single end at `Driver/Loop/Driver.cs:3892`. Specify start/end recording at every actual process boundary; otherwise a clean first process’s unclosed child becomes falsely *lost* at the next start, or survives into another process.

3. **Blocking — logical responsibility still prevents crash closure.** `Design:553` synthesizes closure only when the record is not “live,” although `Design:426` deliberately makes completed quest/helper waits live. The driver concludes before appending lineage end (`Driver/Loop/Driver.cs:3876`, `:3892`), so a crash between those operations leaves a completed waiting record whose child remains working indefinitely. Separate responsibility from process liveness and test that exact crash boundary.

4. **Blocking — file stamps do not repair a stale ledger-only composition.** `Design:745` repairs old file inputs, but a session/quest change can reach the page before the asynchronous forgetting subscriber (`Modules/Bridge/DriverModule.cs:109`), letting the re-ask join the old work (`Modules/Bridge/HeldAnswers.cs:88`). That work may land unchanged and emit nothing, with no file mismatch to trigger recovery; add a source-generation fence or guaranteed dirty re-run covering ledger and configuration inputs, and test this ordering.

#### 3. The routing half

1. **Non-blocking — the one-at-a-time rule contradicts its decision amendment.** `Design:992` permits a driven lead told *one at a time* to publish and finish, while `docs/decisions/D115.md:586` permits handing rows out only when told *open*. State the lead exception consistently and test it separately from the chat prohibition.

2. **Non-blocking — no new ownership, person-door or account bypass found.** The proposed standing supplies information and leaves publication and execution through their existing doors (`Design:983`, `:1054`); the planner still checks drive and holds at `Driver/Quests/Domain/Planner.cs:1940`, and account selection remains the driver’s walk (`Driver/Loop/Driver.cs:693`). Describe *open* as routing availability, rather than a guarantee that an account or agent can immediately run the quest.

#### 4. Decided rules

1. **Blocking — unmatched starts still do not establish an empty view.** `Design:593` says unmatched hook starts never withhold the empty sentence, contradicting its own explanation at `Design:598` and D173’s truthful partial visibility (`docs/decisions/D173.md:11`). Keeping a count fixes evidence suppression, but unresolved starts without known ends still require an unknown/partial result rather than *Nobody else is working on this*.

2. **Blocking — a failed refresh silently masquerades as current state.** `Design:808` explicitly retains the old answer without notice because `Modules/Bridge/HeldAnswers.cs:164` reports nothing when a held refresh fails. D182 requires a failed read to say so (`docs/decisions/D182.md:564`); retain the rows, but mark their failed/stale status and provide retry, including an event path and story.

3. **Non-blocking — no additional D203 category, D167 grouping, D185 placement or translation-parity violation found.** The separate groups preserve their meanings (`Design:398`, `:403`), and the bilingual key table preserves the displayed placeholders (`Design:888`). D185’s terminal requirement is now explicitly addressed at `Design:1200`.

#### 5. Rows a, e, b, c, d and f

1. **Blocking — a needs the corrected lifecycle and delivery contracts first.** Its proofs at `Design:1114` do not cover call-only discovery, native processes within one record, a crash after completing a wait, or the ledger-only stale landing above. Add explicit expected answers for those cases; it remains the right first visible feature.

2. **Blocking — e’s source-first order holds, but its lane-change proof currently cannot pass through the named mechanism.** `Design:1155` correctly builds standing before doctrine, while `Design:1165` requires the registry-only wake missing from today’s signature. Resolve §1.2 and reconcile the one-at-a-time wording before filing it as buildable.

3. **Blocking — b inherits the unresolved responsibility selector.** Its wait → answer → replacement proof at `Design:1183` covers a successful replacement only. Add stood-down and choice-refused replacements, plus the chat-take case, before relying on its “current” waits; following a still holds.

4. **Non-blocking — c now has a valid reader prerequisite and navigation fallback.** `Design:1190` resolves the previous thread dependency without another reader. State that partner/reviewer coverage arrives with D203’s corresponding rows, since COWORKUX1a itself is the helper exchange feature (`docs/2026-10-23-coworker-design.md:661`).

5. **Non-blocking — d has the requested terminal twin and can follow c.** `Design:1201` correctly places its parser before ordinary ask text, matching `Daoris.Desktop.Driver.Host/Program.cs:470`. Clarify whether “same groups and waits” includes the co-worker, channel and consideration inputs composed beside `AtWork`, rather than just its raw DTO.

6. **Blocking — f cannot discover a new caller-linked request from previously read filenames alone.** `Design:738` restricts dependencies to named files already read, but meetings currently discover IDs by directory enumeration (`Driver/Brainstorms/MeetingFiles.cs:76`); a newly created request changes none of the old dependency stamps. Specify a request-list discovery dependency, then test creating a request after the view is held, without a ledger write; the state-only update test at `Design:1208` is insufficient.

**Verdict: another round.**

## Opus (foundation), round 4

The independent Opus 5.5 reader's foundation read of round 4 at `953ae0c48`: 12 findings, 4 blocking; accept row a after named changes. Codex's read of round 4 is owed, its credits out. The design's §15 places each named change.

### 0. Both reads of round 3

I read the design at `953ae0c48`. Its `src/` is the same as `b4f096cc0`'s, the base round 4 names. Design line numbers refer to `docs/2026-10-24-cowork-view-design.md` at `953ae0c48`, and paths use its abbreviations.

**The Opus foundation reader's F1-F11**

| # | Half | Status | Checked against the code |
|---|---|---|---|
| **F1** run rule erases Codex children | row a | **Taken**, holds | A Codex child is made only by its calls (`ChildBindings.cs:154-157`, `:223-233`). Rule 6 opens a run at an id's first line. Residue: an open Codex run reads *working* for the whole process (F1 below). |
| **F2** watch wakes again; no page asks again | row a | **Taken**, holds in shape | The baseline at `HomeWatch.cs:337` is replaced by registered versions and wake-once. Residue: a pending composition registers nothing (F2 below). |
| **F3** registry signature has no lanes | routing | Carried | Row a reads no registry, so carrying it leaves row a whole. |
| F4 current executor is not `LastRun` | routing | Carried | Row a draws no responsibility. Rule 17's test asserts that a part `completed` with `ended.awaits` is not called waiting. |
| F5 lineage wake at the call rate | row a | **Taken** | Call lines wake only where they open a run, and the lineage moment is dropped from *last heard*. Residue: calls by an unmatched agent id (F5 below). |
| F6 reviewers not refused | routing | Carried | — |
| F7 COWORKUX1a holds helpers only | routing | Carried | — |
| F8 `undefined` pending fails the query | row a | **Taken**, holds | `held.ts:31-33` passes the pending answer through the reader, so `{ reading: true }` is the reader's to write. |
| F9 empty sentence beside unmatched starts | row a | **Taken** | Residue: the *Nobody* rule leaves out the Codex line (F9 below). |
| F10 standing told not recorded | routing | Carried | — |
| F11 (a)(b)(d)(f) | routing | Carried | — |
| F11 (c)(e) | row a | **Taken**, holds | Ancestors are kept past the bound; *last seen* at the child's own last line. |

**Codex's round-3 findings**

| # | Half | Status | Checked against the code |
|---|---|---|---|
| **1.1** "newest record" is not `LastRun` | routing | Carried | Row a reads no quest. |
| **1.2** registry wake cannot hear lanes | routing | Carried | — |
| 1.3 reviewers not refused | routing | Carried (corrected in text) | — |
| **1.4** `RouteFlights` keeps no burst's inputs | row a, routing | **Answered** for row a by removing the need; carried for routing | Holds. The `sessions` slice holds every record, with `state`, `adapter` and `created` (`SessionsStateSlice.cs:39-47`, `:71`). The `groups` slice holds every entry (`GroupsStateSlice.cs:173-179`). Both are read from memory (`StateSlices.cs:170-180`). |
| **2.1** start-only runs drop call-only children | row a | **Taken** | Residue: F1 below. |
| **2.2** process ends at every boundary | row a | **Taken**, holds | The four sites are confirmed: `ChatRunner.cs:1441`, `Driver.cs:3643`, `DriverContinuations.cs:1242`, `:1299`. Residue: where the `end` is written, and files with no `start` (F4 below). |
| **2.3** responsibility blocks crash closure | row a | **Taken**, holds | The sweep takes `working` only, judged by `AliveOnThisMachine` (`Orphans.cs:74-81`), once per watch start (`DriverWatch.cs:715-717`). Residues: F6, F7 below. |
| **2.4** ledger-only stale landing | row a | **Taken**, holds | The records, the entry and `running` are now dependencies too. Residue: F2 below. |
| 3.1 one-at-a-time wording; 3.2 *open* as availability | routing | Carried (taken in text) | — |
| **4.1** unmatched starts do not establish empty | row a | **Taken** | Residue: F9 below. |
| **4.2** failed refresh shown as current | row a | **Taken in part** | A kept failure is now told. Residues: a failure with nothing held still loops, and neither failure carries its words (F3); `stale` is cleared by the next ask (F12a). |
| 4.3 check | — | Taken | — |
| **5.1** row a's lifecycle and delivery contracts | row a | **Taken**: the table | Residues: as above. |
| **5.2** row e, **5.3** row b, 5.4 row c, 5.5 row d, **5.6** row f | routing | Carried | — |

None of the carried findings is a contract row a reads, so carrying them leaves row a standing alone, apart from F10 below.

### 1. Claims about the code

These hold as cited: §1.1's four spawn sites and their order (`Driver.cs:3827` → `:3833` → `:3852`/`:3861` → `:3876` → `:3892`); `ChildBindings.cs:177`, `:278`; `ChildLineage.cs:118-122`, `:135-138`; `Orphans.cs:81`; `HeldAnswers.cs:40`, `:88-100`, `:145-165`, `:181-188`; `held.ts:41-52`; `stateHub.ts:80`; `HomeWatch.cs:172`, `:331`, `:337`; `StateSlices.cs:378`; `main.tsx:31`; `placements.ts:9`, `:22`; `layout.ts:44`.

These claims are wrong:
- **`running` is "the sessions with a live process", the same as `AliveOnThisMachine`** (:392, :629-630). `running` is only this window's own registry (`DriverRoutes.cs:642`; `SessionProcesses.cs:183-189`). `AliveOnThisMachine` also reads the markers of other drivers on this machine (`:455-459`, remark `:22-26`) (F6).
- **"A work that fails with nothing held is answered once as its refusal (`HeldAnswers.cs:96-100`)"** (:919). The page asks again at every failure (`held.ts:45-47`). Each ask after a failure begins a new work (`HeldAnswers.cs:88-91`) before it throws (`:96-100`) (F3).
- **"The next answer clears `stale`"** (:924, :1051). The next ask clears it: `askHeld` reads the held answer through the reader (`held.ts:30-31`). A landing equal to the held answer tells nothing (`HeldAnswers.cs:155-160`) (F12a).
- **"This replaces `Of`'s clearing of `Closed` alone"** (:654-655). That clearing is also what makes an `end` a run boundary today: "named again after an end runs again, in the part's next run" (`ChildBindings.cs:176-177`). The new fold drops that boundary, and files without `start` lines need it (F4).
- **"The first child's line is what creates" the lineage file** (:155-157, :659-660). The root's first call to the connector creates it as well: a line is written for every call (`LineageFile.cs:59-70`, the folder made at `:106`). On Claude Code the hook also writes the root's `PreToolUse` line, with no agent (`lineage-hook.mjs:31-46`).
- **"Every call … writes two lineage lines"** (:158-159). This holds only on Claude Code, whose hook it is (`Permissions.cs:573`). A Codex call writes one line.
- **The groups slice holds "each line of work's entry"** (:390). A non-live intake whose ask is asked of is in no entry (desktop `SessionEntries.cs:96-97`).
- **`HeldAnswersTests`** (rules 14, 15) does not exist. Held reads are tested in `Modules.Tests/Bridge/DriverModuleHeldTests.cs`.

### 2. Row a's rules

| # | Does the contract hold against the code? | Does the named test prove it? |
|---|---|---|
| 1 measure | Yes. A root child's acp id is already on the record as its card's `Id` (`AcpStreams.cs:165-176`), so the join can be measured against the hook's ids on the install. Codex's protocol door is not named (F1). | Yes, as far as it reaches. |
| 2 `acp` lines | Yes, against `AcpStreams.cs:152-204`. `AcpStreams` holds no home or session today (`:26-27`), so the writer is new. | Yes. |
| 3 pipe description | Yes (`ChildBindings.cs:303-311`). | Yes. |
| 4 bracketing | The sites hold. The chat's `end` is already per process (`ChatRunner.cs:2524`), and comes after its conclusion (`:2492`). "Once its exit is read" falls before the capture drains (`Driver.cs:3827` → `:3833`; `DriverContinuations.cs:1274`, `:1314`), so stream lines can follow the `end` (F4). | Partly: no late stream line is tested. |
| 5 close | Holds on the record's state. `running` is not the machine's (F6). `declined` and `stood-down` are unmapped (F7). | Yes, for what it names. |
| 6 runs | Holds for Claude Code. A Codex run stays *working* until its process ends (F1). After an `end` with no `start`, nothing is said (F4). | No. The Codex fixture's lines carry no `at` (`ChildBindingsTests.cs:127-136`), which *first seen* needs, and no open generation is tested. |
| 7 end words | A joined child's word depends on which line comes first (F8). | No. |
| 8 join | Holds. | Yes. Its wake is F5's. |
| 9 three outcomes | Holds. | Yes. |
| 10 inputs in memory | Holds (see §0, 1.4). A pending composition registers nothing (F2). | No: only "pending", never the answer that should follow. |
| 11 fence | Holds. The registration must be told by `Land` (`HeldAnswers.cs:147-149`), since the work cannot know it was superseded. | Yes. |
| 12 one judge | Holds. `HomeWatch`'s topic judge is fixed: it compares against its own `Last` and passes over this process's marked writes (`HomeWatch.cs:316-344`, `:332`). So `ForWork` needs a topic whose judge is the read's. | Yes. |
| 13 line wakes | A call by an unmatched id wakes (F5). A half-read last line (F12b). | No for those cases. |
| 14 off thread | Holds (the serialising under the gate is at `HeldAnswers.cs:151-160`). | Yes. |
| 15 reading, failed, stale | Reading holds. Failed loops (F3). Stale is cleared by any ask (F12a). Neither event carries `why` (F3). | No. |
| 16 nobody | The Codex line is missing from the conditions (F9). | No. |
| 17 parts | Holds. | Yes. |
| 18 last heard | Holds (`CallsStateSlice.cs:44-54`). | Yes. |
| 19 view | Holds. Row a's own keys are not marked (F10). | Yes. |
| 20 doors | The terminal verb's inputs when no window runs are not said (F11). | Partly. |
| 21 records | Holds. | Yes. |
| 22 person's first | Holds. A stop makes the `end` unclean, so the children read *lost* (`ChatRunner.cs:2524`). | Yes. |

What the brief asked about, case by case:
- **Shown live that is not.** A Codex child on `codex-acp` (protocol wire, `Adapters.cs:2063-2067`, one process across a chat's turns) reads *working* from its first call until the chat's process ends, and counts in *n working* and the head's fragment (F1). Children of later processes in a lineage file written before row a have no specified state (F4).
- **Missed that is.** A view restored open at startup stays *reading* (F2). A line half-read at composition can lose its wake (F12b).
- **Generations and a crash.** The record half holds. The machine half reads this window's processes only (F6).
- **Holding the window.** The failure loop runs on the window's route calls (F3). Calls by unmatched ids tell `STATE_CHANGED` at the call rate (F5).
- **Shared host.** Nothing new is served. The lineage, the answer and its labels, `heard`, the bus events and the terminal verb stay on the desktop. `SessionHomeFiles` only clears and deletes (`HistoryClearing`, `SessionDeletion`).

### 3. The split

Row a stands alone. It is buildable and useful: it is the owner's case, a protocol-door chat's subagents with their labels, states and ends, the entry's parts and *last heard*. It reads no quest, wait, registry, standing, box or meeting. Its acceptance amends only D173 and D118's frame (§10).

It leans on routing-half text in one place. §4.1 gives its path to the page as "§4.2's steps 1 to 3, unchanged" (:909-910), and §4.2 says it "settles nothing for row a" (:928). §4.2 also has the hub's `sessions`, `quests` and `asks` entries invalidate the read (:949), which §4.1 neither takes nor refuses. §5.4's table and its new glossary terms mix both halves' words. None of this is an unsettled routing contract; it is text to move (F10).

### 4. Findings

1. **A Codex child reads *working* for the life of its process. Blocking.**
   - **What is wrong.** Codex says no end (`D173.md:121-122`; `ChildBindings.cs:223-233`). A Codex run therefore stays open until its generation closes (§3.6 :642-652). On `codex-acp` that is the chat's whole process. Yet §3.8 gives it `working` (:789), the story draws *working · first seen 14:02* (:1111), and it counts in *n working*, in the head's *working with it* and in `turnWaits` (:778). CONSOLE2 measured the held turn only on `claude-code-acp` (`console2-streams-evidence.md:74-77`).
   - **A second gap on the same door.** The driver asks every streamed ACP session for `nativeSubagentSessions` (`Acp.cs:1667`, `:1756-1770`). If `codex-acp` announces children, a child is drawn twice, once from its `acp` line and once from its thread, with no join between them.
   - **Smallest change.**
     - An open run whose harness says no end has a state of its own: *first seen {time} · its end is not said*. It is not counted as working anywhere.
     - `turnWaits` applies to `claude-code-acp` alone.
     - Rule 1 measures `codex-acp`'s announcements and says which lines a Codex part draws its rows from.
     - Rule 6 tests an open Codex generation.
2. **A pending composition registers nothing, so the view stays *reading*. Blocking.**
   - **What is wrong.** Before the service answers, the slices are not judged (`SessionsStateSlice.cs:71`, `GroupsStateSlice.cs:111`; `StateSlices.cs:170-180`). The read answers pending (:875-876). But its dependencies name no partition (:877-881), so the slices' first `Pushed` (`StateSlices.cs:378-393`) finds nothing to compare, and nothing else asks again (:911). A view restored open at the window's start stays *Reading…* until a focus re-fetch after 15 s (`main.tsx:30`).
   - **Smallest change.** A pending composition registers each slice's not-judged status as a dependency, and a session missing from `entries` is registered as absent, as a file is. Rule 10 tests pending, then the answer once the slices judge, with no other ask.
3. **A failure with nothing held asks again at every failure, and no failure carries its words. Blocking.**
   - **What is wrong.** `answered` invalidates the query on `failed` (`held.ts:45-47`). `Land` clears the flight (`HeldAnswers.cs:147`). The next ask begins a new work (`:88-91`) and throws the fault (`:96-100`). So a work that always fails is begun again at every failure for as long as the view is mounted, each turn a route call on the window's thread. On top of that, the event is `{held, failed}` (`:176`), so *Could not read again: {{why}}* and *could not be read: {{why}}* have no source. This is NOBLOCK1's path today; row a takes it as written.
   - **Smallest change.** Both failure events carry the refusal's words. This read's reader writes `{ failed: { why } }` where nothing is held and `stale: { why }` where an answer is, and asks nothing on either; *Try again* is the only re-ask. Rule 15 tests that a work that always fails is begun once per ask, never once per failure.
4. **Lines after an `end` with no `start` are left to no rule. Blocking.**
   - **What is wrong.** Every file written before row a holds one `end` per process and no `start`: a pipe-door chat writes one per turn (`ChatRunner.cs:2524`). The owner's case is one of these files. As written, all of it is the first generation (:623), closed at its first `end` (:626). Runs begun by later processes then stand in a closed generation, and rule 3 never closes them, since that generation has an `end` (:629). Today's fold treats an `end` as the boundary (`ChildBindings.cs:176-177`), and the design drops that. Separately, the driven path's `end` is to be written "once the exit is read", which is before the capture drains (`Driver.cs:3827` → `:3833`; `DriverContinuations.cs:1274`, `:1314`). So a stream `ended` line can follow it (`ChildLineage.cs:145-152`).
   - **Smallest change.** An `end` with no `start` after it closes its generation, and the next line begins the next generation. Each `end` is written once the capture has drained. Rule 6 tests a file written before row a with three processes.
5. **A call by an unmatched agent id wakes the read at the call rate. Not blocking.**
   - **What is wrong.** On a part with `acp` lines, an unmatched id never has a run (:696-701). Under :903-905 each of its hook `event: call` lines (`lineage-hook.mjs:40-44`) therefore wakes `atWork`, and every wake forgets every flight (`DriverModule.cs:106-111`). Where the measure finds no join, that is every subagent's call to the connector.
   - **Smallest change.** A call naming an id that the composition holds, as a run or among its unmatched ids, wakes nothing. Rule 13 tests it.
6. **`running` is this window's processes, not the machine's. Not blocking.**
   - **Where.** :392, :629-630; `DriverRoutes.cs:642` against `SessionProcesses.cs:455-459`. The sweep runs once per watch start (`DriverWatch.cs:715-717`).
   - **Smallest change.** Correct the claim, and say that a part another Daoris process here runs is closed only by its record's move.
7. **Rule 5 leaves `declined` and `stood-down` unread. Not blocking.**
   - **Where.** :633-634; nine states at `api.ts:413-415`.
   - **Smallest change.** Name both; a clean end reads `ended`.
8. **A joined child's end word depends on line order. Not blocking.**
   - **Where.** :648-651, :669-671. The hook's `stopped` and the wire's `subagent_state_update` (`AcpStreams.cs:186-204`) end the same run, and "a stop after a run ended begins nothing" drops whichever comes second.
   - **Smallest change.** The wire's or the stream's word wins wherever a line of the run says one; otherwise the hook's `finished`.
9. ***Nobody* leaves out the Codex line. Not blocking.**
   - **Where.** §5.2 :1053-1055 and rule 16, against §2 point 3 (:451-457) and §3.1 (:538-539). A Codex lead with no lineage yet meets every condition §5.2 lists.
   - **Smallest change.** Rule 16 withholds the sentence for a harness seen only by its calls, read from the record's `adapter` (`SessionsStateSlice.cs:41`). Where rule 1's measure finds a door that shows no child, such as an ACP session started without streams (`Acp.cs:1756-1770`), the same applies.
10. **Row a's update path is borrowed from §4.2, and its words are not marked. Not blocking.**
    - **Where.** :909-910 against :928 and :949; §5.4 :1146-1193 and :1213-1214.
    - **Smallest change.** Copy steps 1 to 3 into §4.1, and say whether the hub's `sessions` entry names `SESSION_AT_WORK` in row a. Mark row a's keys, and its two terms *at work* and `disconnected`.
11. **The terminal verb's inputs are not said. Not blocking.**
    - **Where.** Rule 20 (:1389). §4.1's inputs are the window's slices. `sessions` verbs run in `SessionsConsole` (`Driver.Host/Program.cs:650-653`), and the pipe answers a held read by asking until it is not pending (`InstancePipe.cs:1061-1080`).
    - **Smallest change.** The verb asks the running Daoris. When no window runs, it either folds the same lineage over the service's records and `SessionEntries.Read`, using `AliveOnThisMachine`, or refuses, saying the window is not running. Its tests cover both cases.
12. **Smaller. Not blocking.**
    - (a) Keep `stale` through an ask that is answered from the held answer (`held.ts:30-31`). Have `Land` tell the first landing after a kept failure even when it is unchanged (`HeldAnswers.cs:155-160`). Otherwise *stale* flickers off at each wake during a lasting failure.
    - (b) Register a file's length only up to its last full line. The hook and the connector append from other processes (`lineage-hook.mjs:48-58`; `LineageFile.cs:104-120`), a half line read is skipped (`ChildBindings.cs:335-346`), and a judge reading from mid-line would miss that line's wake.
    - (c) Say how the read draws an attended intake that has no entry (`SessionEntries.cs:96-97`).
    - (d) Depend on the record fields the answer reads (state, adapter, created, the mark), not the whole record, whose `note`, `ran` and `updated` move often (`SessionsStateSlice.cs:39-47`).
    - (e) Correct the claims in §1 about the file's creation and "two lines per call", and name `DriverModuleHeldTests` as the place of rules 14 and 15.

**Findings: 12, of which 4 are blocking (1-4).**

**Verdict: accept after named changes the steward can check without another read.** Round 4 takes every row-a finding of both reads of round 3, and each answer holds against the code in shape:
- The four spawn sites are bracketed.
- A last generation closes on the process and the record's state, never on responsibility.
- Call-only children are kept.
- Every input is a dependency checked as the composition lands, and each read wakes once.
- No input is a service read.
- Reading, failed and stale are designed.

The routing half is carried cleanly, and row a stands alone once §4.2's three steps are copied into §4.1. Four contracts fail as written, and each needs a sentence and a test:
- F1: an open Codex run is not *working*.
- F2: a pending composition registers the slices' not-judged status, so their first push wakes it.
- F3: no failure makes the page ask again, and both failures carry their words.
- F4: an `end` with no `start` after it bounds a generation (so files written before row a read correctly), and each `end` is written after the capture drains.

Findings 5 to 12 can be taken in passing.

## Codex, round 4

Codex's read of round 4, with the Opus foundation reader's named changes taken in the design's §15, at `eced2688e`: 11 findings, 7 of them blocking; another round for row a. Its §0 re-reads round 3's answers and opens none of its own: the gaps it names there are its findings below. It was run read-only at that commit, on main. Round 5 answers it in the design's §16.

Reviewed read-only at `eced2688e`; nothing changed. I excluded checkout-relative `local/` and everything outside this checkout.

`Design` means `docs/2026-10-24-cowork-view-design.md`; `Foundation` means `docs/2026-10-25-foundation-design.md`. `Driver/`, `Modules/` and `Web/` use the design’s path aliases.

### 0. Round-3 findings

Severity is the original finding’s. “Taken” means specified in the proposal, not built.

| Finding | Severity | Disposition |
|---|---|---|
| 1.1 Responsibility selector | **blocking** | **Deferred to routing**: row a reads no responsibility; `Design:500`, `:1885`. |
| 1.2 Lane-sensitive registry wake | **blocking** | **Deferred to routing**: row a reads no registry; `Design:993`, `:1886`. |
| 1.3 Reviewer enforcement overclaimed | **non-blocking** | **Deferred to routing, corrected**: present enforcement and future reviewer work are distinguished; `docs/decisions/D203.md:234`. |
| 1.4 `RouteFlights` cannot retain burst inputs | **blocking** | **Answered for row a**: snapshots remove its service acquisitions; routing retains the unresolved acquisition contract; `Design:922`, `:1888`. |
| 2.1 Call-only children discarded | **blocking** | **Taken**: first observation opens a run; late calls cannot reopen it; `Design:679`, `:1482`. |
| 2.2 Process boundaries missing | **blocking** | **Taken for new writers**: all four sites are named; the new claim about historical boundaries is wrong, below; `Design:1480`. |
| 2.3 Responsibility prevents crash closure | **blocking** | **Taken**: closure uses process presence and record state, with the exact completed-wait crash case; `Design:661`, `:1481`. |
| 2.4 Ledger-only stale composition | **blocking** | **Taken**: records, entry and running membership join the source fence; `Design:939`, `:1487`. Delivery ordering remains a separate gap below. |
| 3.1 One-at-a-time contradiction | **non-blocking** | **Deferred to routing, corrected**: the lead exception is explicit; `docs/decisions/D115.md:591`. |
| 3.2 Routing availability is not execution assurance | **non-blocking** | **Answered**: *open* promises availability only; `docs/decisions/D115.md:597`. |
| 4.1 Unmatched evidence permits false emptiness | **blocking** | **Taken**: unmatched ids replace the empty sentence; `Design:735`, `:1492`. |
| 4.2 Failed refresh appears current | **blocking** | **Taken for composition failures**: failure events, retained rows and stale wording are specified; `Design:1004`, `:1491`. Input-owner failures remain uncovered below. |
| 4.3 Other boundaries held | **non-blocking** | **Answered**: row a introduces no co-worker category, responsibility or mutation; `Design:803`, `:1493`. |
| 5.1 Row-a lifecycle/delivery proofs missing | **blocking** | **Taken for the named cases**: rules 4–6 and 11 supply their expected outcomes; `Design:1480`. Further omissions are below. |
| 5.2 Row-e lane proof cannot pass | **blocking** | **Deferred to routing**: neither standing nor lane detection is row a’s input; `Design:1533`. |
| 5.3 Row-b responsibility proof incomplete | **blocking** | **Deferred to routing**: row a makes no executor selection; `Design:1885`, `:1900`. |
| 5.4 Row-c coverage needs qualification | **non-blocking** | **Deferred to routing, corrected**: helpers precede reviewer/partner coverage; `docs/decisions/D203.md:237`. |
| 5.5 Row-d terminal output ambiguous | **non-blocking** | **Deferred to routing**: row a’s session-keyed terminal door is independently specified; `Design:988`, `:1902`. |
| 5.6 New requests cannot be discovered | **blocking** | **Deferred to routing**: row a reads no meeting/request listing; `Design:993`, `:1903`. |

### 1. Wrong claims about existing code

1. **Blocking — historical files do not universally contain one `end` per process.** `Design:175`, `:653` and `docs/decisions/D173.md:5593` assert this, but `Driver/Loop/DriverContinuations.cs:1242` and `:1299` spawn additional processes before the single outer `ChildLineage.End` at `Driver/Loop/Driver.cs:3907`. Rule 5 must distinguish recorded boundaries from unavailable historical boundaries; its manufactured three-process/three-end fixture does not represent this existing case.

2. **Non-blocking — the strip does not simply word the record’s raw state.** `Design:1493` promises both strip-equivalent wording and a completed waiting record’s own state, but `Web/work/head/PartsStrip.tsx:56` uses `part.shown` through `shownOf`, which preserves derived waiting words at `Web/work/groups.ts:152`. Keep row a’s explicitly chosen raw-state behavior, but stop describing its state words as identical to the strip’s and test that deliberate difference.

### 2. Row a’s read and updates

1. **Blocking — delayed capture lines still have no reliable process attribution.** `Design:647` permits capture to outlive its `end`, but `:655` handles only late ending lines belonging to the just-closed generation. Capture remains running after abandonment (`Driver/Loop/Driver.cs:3969`, `:4023`), and its lineage callback identifies only the session (`Driver/Sessions/ChildLineage.cs:145`): a delayed start can invent another generation, or an old stop after the next start can close the reused id’s new run. Specify a process identity or writer fence, and test both delayed starts and delayed stops across a subsequent spawn.

2. **Blocking — rule 13’s id-membership test wakes on root calls.** Registered ids comprise child runs and unmatched ids (`Design:937`); a Codex root thread belongs to neither, yet satisfies the “new id” condition at `Design:968`. Existing code explicitly counts that thread as the root, never a child (`Driver/Sessions/Domain/ChildBindings.cs:223`). Register root identity too and exclude root-only calls; prove repeated root calls cause zero `atWork` wakes.

3. **Blocking — changing failure events does not cover immediate route failures.** A first work that fails inside `Patience` rejects `AskAsync` directly (`Modules/Bridge/HeldAnswers.cs:104`), and `askHeld` propagates that rejection (`Web/bridge/state/held.ts:30`); the application retries once (`Web/main.tsx:31`). `Design:1004` changes event handling only, so “Try again is the only re-ask” is not established. Specify how direct refusals become the same failed value, disable automatic error retries for this read, and test failures both before and after `Patience`.

4. **Blocking — failed input slices are indistinguishable from pending or healthy cached inputs.** Both owners merely log their failures (`Modules/Loop/SessionsStateSlice.cs:77`, `GroupsStateSlice.cs:117`), while `StateSlice.Snapshot` exposes no failure status (`Modules/Platform/StateSlices.cs:170`). Consequently `Design:931` can remain *Reading* indefinitely after a failed first judgement, or present an old successful snapshot without *stale* after a failed refresh. Add observable input health and its wake, with tests for first-read failure, retained-snapshot failure and recovery; the row-a composer must still make no service call.

### 3. Decided rules and foundation dependencies

1. **Blocking — the source fence does not prevent late delivery from regressing the page.** `Design:1487` fences composition and registration, but `HeldAnswers` releases its gate before emitting (`Modules/Bridge/HeldAnswers.cs:170`), and the page accepts answers without a version (`Web/bridge/state/held.ts:49`). An older accepted landing can therefore emit after a newer one, restoring old rows or an old failure after recovery, contrary to `docs/decisions/D182.md:24`. Number row-a replies and success/failure events, reject older deliveries, and test reversed delivery including an old direct reply.

2. **Non-blocking — Part A’s placement needs stating; Part B is not required.** `Foundation:1013` explicitly allows row a to proceed independently, but its map puts `AcpStreams` in Agents, lineage in Participants and `SessionOutput` in Context (`Foundation:1933`, `:1936`, `:1937`). The new ACP writer at `Design:628` should use an injected sink across that boundary, rather than introduce an Agents-to-Participants dependency forbidden by `Foundation:1955`. I found no substantive dependence on routing, the journal, move records or the desktop store.

3. **Non-blocking — hook-only `finished` lacks a specified bilingual display mapping.** The type and rule retain `finished` (`Design:841`, `:1483`), but §5.4 supplies no corresponding key; the existing panel catalogues have completed, failed, stopped, cancelled and session-ended only (`Web/locales/en/work.panel.json:16`, `Web/locales/zh/work.panel.json:16`). Add an explicit mapping/key and a hook-only finished story; equal catalogue key sets would not detect a key omitted from both languages.

No additional D173, D203, D115, D167 or translation-placeholder violation found.

### 4. Row a’s proof

1. **Blocking — rule 14’s bound and retention promises cannot all hold.** `Design:1490` requires at most 40 children while keeping every working/lost child and every drawn child’s ancestors; `Design:832` repeats that conjunction. Forty-one working children, or a retained child with more than 39 ancestors, makes it impossible. Choose an explicit overflow/ancestry policy and test those cases, including truthful counts.

2. **Non-blocking — coverage is substantially better, but remains a proposed proof plan.** Rules 1–3, 7–9 and 18–22 name suitable checks or measurements for their stated scope (`Design:1477`, `:1483`, `:1494`); rule 17 needs the wording clarification above. Rules 4–6 need historical and delayed-capture cases; 10–13 need input-health and root-call cases; 15–16 need immediate-failure and upstream-failure cases; 11/15 also need reversed delivery. Rule 14 needs the overflow decision, while rule 22’s installed run appropriately remains distinct from those regressions (`Design:1502`).

**Verdict for row a: another round.** Historical generation attribution, delayed writers, and failure/delivery ordering still require changes to the table’s contracts before its proof can establish them.

## Codex, round 5

Codex's read of round 5 at `6736d2db9`: 10 findings, 4 of them blocking; another round for row a. Its §0 re-reads round 4's answers and opens none of its own: the one it leaves open in part, 2.1, is its finding 2.1 below. It was run read-only at that commit, on main. Round 6 answers it in the design's §17, beside the Opus foundation reader's read of the same round.

Reviewed read-only at `6736d2db9`; nothing changed. I excluded checkout-relative `local/` and everything outside this checkout. `src/` is unchanged from `9f48860ba`.

Aliases: **Design** = `docs/2026-10-24-cowork-view-design.md`; **Foundation** = `docs/2026-10-25-foundation-design.md`; **Driver/**, **Modules/** and **Web/** follow Design’s path aliases.

### 0. Round-4 findings

“Taken” means specified adequately in the proposal, not implemented or demonstrated.

| Finding | Original severity | Disposition |
|---|---|---|
| 1.1 Historical process boundaries | **blocking** | **Taken.** Driven history becomes `unrecorded`; the fixture now represents the existing continuation case. `Design:1713`. |
| 1.2 Strip versus raw state | **non-blocking** | **Taken.** The deliberate difference and its regression are explicit. `Design:1725`. |
| 2.1 Delayed capture attribution | **blocking** | **Open in part.** Bound writers solve tagged captures; tokenless lines and lost starts still permit wrong attribution. `Design:724`, `:730`, `:1714`; §2.1 below. |
| 2.2 Root-call wakes | **blocking** | **Taken.** Root identities and the child-to-root exception are registered and tested. `Design:1063`, `:1721`. |
| 2.3 Immediate failures | **blocking** | **Taken.** Direct and delayed failures become values; bridge failures have `retry: false`. `Design:1168`, `:1723`. |
| 2.4 Input-slice failures | **blocking** | **Taken.** First failure, retained-snapshot failure and recovery have health dependencies and wakes. `Design:1044`, `:1718`. |
| 3.1 Reversed delivery | **blocking** | **Taken within one epoch.** Replies and both event outcomes are numbered; tests include late direct replies and failures after recovery. `Design:1175`, `:1723`. |
| 3.2 Foundation placement | **non-blocking** | **Taken.** The Agents port, Participants implementation and Loop wiring respect the map. `Design:1286`, `:1731`. |
| 3.3 Hook-only `finished` wording | **non-blocking** | **Taken.** Named bilingual mapping, exhaustive mapping test and story supplied. `Design:1424`, `:1529`, `:1727`. |
| 4.1 Impossible retention bound | **blocking** | **Taken.** Whole-chain admission, overflow counts and overlong-chain tests replace unconditional retention. `Design:1132`, `:1722`. |
| 4.2 Proof gaps | **non-blocking** | **Taken as a proof plan.** The named cases were added; the remaining gaps are below. `Design:2231`. |

### 1. Wrong claims about existing code

1. **Non-blocking — a chat does not invariably await capture before writing its end.** `Design:209` and `:219` describe the normal path as universal, but `Driver/Conversations/Chat/ChatRunner.cs:2893` can throw before the capture await at `:2896`; the catch at `:2938` reaches the unconditional end at `:2977`. Qualify the claim and cover this exceptional path in rule 4’s writer/drain test.

2. **Non-blocking — first judgement does not necessarily produce `Pushed`.** `Design:1068` relies on the first judgement’s push, but an empty sessions list produces no partitions (`Modules/Loop/SessionsStateSlice.cs:102`), while `StateSlices.cs:241` marks it judged and `:248` notifies only when partitions moved. A pending read registered before that healthy empty judgement therefore waits for another wake or reconciliation; explicitly notify the not-judged-to-judged transition and test this ordering.

### 2. New protocols

1. **Blocking — optional process attribution still permits the original cross-spawn corruption.** The immutable token on each of the four spawns’ capture writers is sound, but `Design:724` permits tokenless hook/connector lines and `:730` treats a token whose start was lost as positional; `Driver/Sessions/ChildLineage.cs:108` silently loses failed writes. An old tokenless stop after P2’s start can close P2’s reused X, and an old start can make a working child there; rule 6’s proposed positional test actually endorses that result (`Design:1714`). Require token propagation through the explicitly handed connector environment (`Driver/Service/KnowledgeConnector.cs:83`, `:114`; `Agents/Acp/Acp.cs:1739`) and define an honest unknown-attribution outcome when the token or start is unavailable—measurement counts alone do not prevent false state.

2. **Blocking — suppressing every known-id call conflicts with closed runs’ “last seen”.** `Design:768` defines that value from the child’s own last line, while `:791` lets delayed tagged lines affect their closed generation and `:1095` suppresses calls for already registered runs. After P1 closes and P2 starts, a delayed P1 call to an already drawn lost child can change its specified `lastSeen` without waking the page. Either freeze that field against suppressed calls or wake when it changes, and test the resulting projection against a fresh composition.

3. **Non-blocking — epoch replacement is weaker than the stated delivery guarantee.** Same-epoch reversed deliveries are handled, but `Design:1180` accepts any different epoch: E1 → E2 → delayed E1 can restore E1. Either state and enforce that a page cannot receive deliveries spanning owner lifetimes, or bind epoch changes to the active connection/baseline; the existing slice consumer resyncs on an unfamiliar epoch instead of installing its push (`Web/bridge/state/slices.ts:249`).

4. **Non-blocking — the remaining named counterexamples have adequate proposed answers.** Recorded versus driven-history generations avoid inventing historical process ends, root registration removes root-only wakes, and health transitions cover failed first judgement and unchanged recovery (`Design:1713`, `:1718`, `:1721`). Rule 14’s chain-first admission and state-counted overflow are now feasible, including forty-one working children and ancestry exceeding forty (`Design:1722`); these are specifications awaiting their stated proofs.

### 3. Decided rules and foundation dependencies

1. **Blocking — the attribution fallback still violates truthful partial visibility.** D173 requires unknown evidence to remain unknown (`docs/decisions/D173.md:11`, `:111`), whereas the fallback in `Design:731` can assign an old signal a definite newer process and outcome. This is the consequence of §2.1, not a separate implementation defect.

2. **Non-blocking — D173’s summary missed the raw-state correction.** `docs/decisions/D173.md:5593` still says the strip supplies the state wording, and round 5’s amendment names only points 2 and 4 (`:5636`), while rule 17 deliberately chooses the record’s wording (`Design:1725`). Amend point 1 explicitly; otherwise I found no additional D203, D115, D167, D182, D185 or translation-parity violation, no new upward module edge, and no dependence on routing or FOUNDATION1 Part B (`Design:1286`; `Foundation:2036`).

### 4. Row a’s proof

1. **Blocking — rules 1, 4, 6 and 13 do not yet prove the attribution and wake promises together.** Their tests establish correctly tagged captures, but leave missing-token/missing-start attribution unsafe and omit a delayed known-id call that changes a closed child’s displayed moment (`Design:1709`, `:1712`, `:1714`, `:1721`). A builder must still choose those failure semantics; add explicit expected outcomes before implementation.

2. **Non-blocking — the rest is a credible prospective proof plan, with measurements still owed.** Rules 2–3, 5, 7–12 and 14–21 name suitable checks for their stated scope, subject to the empty-first-judgement and epoch qualifications above (`Design:1710`–`:1729`); rule 23 covers the port placement (`:1731`). Rule 1’s real-door measurements and rule 22’s installed-window run remain separate evidence, correctly distinguished from stand-ins and stories (`Design:1709`, `:1735`).

**Verdict for row a: another round.** Process attribution without a usable start/token and the interaction between delayed calls, displayed moments and suppressed wakes still require protocol decisions.

## Opus (foundation), round 5

The independent Opus 5.5 reader's foundation read of round 5 at `6736d2db9`, verbatim, its title line (*COWORKVIEW1 round 5, row a: a foundation read*) given as this heading and its headings set one level down: 9 findings, 2 blocking; accept row a after named changes. Round 6 places each in the design's §17, beside Codex's read of the same round.

Read at `6736d2db9`. `src/` is identical at `eced2688e`, `9f48860ba` and `6736d2db9` (both diffs are empty), so every code line round 5 cites holds where it says unless noted below. Path aliases are the design's.

### 0. Codex's read of round 4

| Finding | Severity | Disposition | Why |
|---|---|---|---|
| 1.1 Historical files lack one `end` per process | blocking | **Taken, holds** | Today's fold has no `start` case (`ChildBindings.cs:184-246`). A chat writes `end` per process in `WatchAsync`'s `finally` (`ChatRunner.cs:2970-2977`), its SESSCTX1c retry included, since it returns before re-entering `RunAsync` (`:1810-1818`). A driven `RunAsync` writes one after its conclusion (`Driver.cs:3891`, `:3907`). That one `end` covers its retry (`:3867`) and its continuations (`:3876`). A parked record resumed in the same record writes another, so a driven history can hold several group `end`s. "Every driven history run reads `unrecorded`" is right for that case too. Absent `kind` is F9a. |
| 1.2 The strip's word is not the raw state | non-blocking | **Taken in the design** | D173's round-4 note was not amended (F5). |
| 2.1 Delayed capture lines | blocking | **Taken for the driver's writers, holds there** | The stream watcher is made per capture (`HarnessCapture.cs:123`) and `AcpSession` per process (`ChatRunner.cs:3071`, `Driver.cs:4330`), so a bound token fences an abandoned capture (`Driver.cs:4023`). The four spawns are the only agent spawns (`ContainedKind.Agent` appears only at `ChatRunner.cs:1596`, `Driver.cs:3658`, `DriverContinuations.cs:1242`, `:1299`). The hook and connector half rests on a wrong claim (F4). "Next `start` closes the previous generation" fails when processes overlap (F1). |
| 2.2 Root calls wake | blocking | **Taken, holds** | Roots are read over the whole file before placement (`ChildBindings.cs:139-163`). A Claude Code connector call names only `toolUse`, and the hook's root call names no agent (`lineage-hook.mjs:38`), so neither names a native id. A Codex call naming a root or a new parentless thread is a root call (`:224-227`). The one case that moves the answer is named and woken. |
| 2.3 Immediate route failures | blocking | **Taken, holds on the host** | The ask's `await flight` (`HeldAnswers.cs:105`) becomes the failed value, and `retry: false` is set. Page side: F2. |
| 2.4 Failed input slices | blocking | **Taken, holds for a judgement that throws** (`StateSlices.cs:207-214`) | A judge answering null (no `Service` yet, `SessionsStateSlice.cs:71`, `GroupsStateSlice.cs:111`) clears the flag (`StateSlices.cs:220`), so *reading* stands while the service never came up. That is PUSHUX1's contract for the view; for the verb see F7. |
| 3.1 Late delivery regresses | blocking | **Taken, holds on the host** | `Land` already drops a landing older than the newest (`HeldAnswers.cs:149`). `Landed` (`:150`) is set for failures and for unchanged answers too, so `begun` orders landings and a reply's number is the state it answers. Page side: F2. |
| 3.2 Part A placement | non-blocking | **Taken, holds** | The port's shape and new-file placement are under-said (F8). |
| 3.3 `finished` has no words | non-blocking | **Taken** | Other end words remain open (F3). |
| 4.1 Bound vs retention | blocking | **Taken, holds** | Every child is a row, folded, or counted in `more`, exactly once. Two small gaps are in F9f. |
| 4.2 Coverage gaps | non-blocking | **Taken** | Each named gap has a case. The proofs miss F1 and F2. |

### 1. The new protocols

- **`proc` and its fence.**
  - **Holds:** an abandoned or late capture, and a delayed `started` or `ended` across a later spawn. A tokened line is placed in its own process's generation and changes that generation alone.
  - **Holds:** a crash. With no `end`, the generation closes at the next `start` (`lost`) or by rule 3. Job containment means the agent dies with the application.
  - **Holds:** the retry and continuation re-arming. `AgainAsync` reuses `info` (`DriverContinuations.cs:1298-1299`), so "set anew" is needed, and rule 4 tests it.
  - **Does not hold:** rule 2 assumes a record's processes never overlap. `RunAsync` spawns (`Driver.cs:3658`) and starts its capture (`:3807-3822`) before it advances the record to working (`:3824-3837`). In the ANSWER2 race a second resume's process exists beside the live one; its `start` closes the live generation as `lost`, and the live process's later tokened lines land in that closed generation (F1).
  - **Hook and connector:** the hook inherits Claude Code's environment, so it gets the token. Codex's connector does not (F4).
  - **Proof:** rules 4 to 6 cover sequential spawns only.
- **Recorded versus history.**
  - **Holds:** the boundaries are as round 5 now says.
  - **Rollback:** an older fold ignores `start` and `acp` lines (no case for them), so it reads new files as today. A newer build reads an older build's lines as history.
  - **Proof:** rule 5's driven fixture is the right case.
- **The root's ids registered.** Holds, and rule 13's hundred-root-calls test proves it. For Claude Code the registration is not even needed: no root call names an id.
- **The numbered held read, `{epoch, n}`.**
  - **Holds on the host:** events told in reverse, an old direct reply after a newer event, an old failure after a recovery, a pending reply with no number, and *stale* carried on a reply.
  - **Epoch:** "another epoch replaces" would let a previous epoch's late event win. Only a re-created `HeldAnswers` with works in flight could produce one, and the window has one per module (`DriverModule.cs:100`). Acceptable.
  - **Fails on the page:** the number's lifetime against the query's data (F2).
- **The slices' health and its wake.**
  - **Holds:** first failure, failure over a kept snapshot, and recovery. `HealthMoved` must follow the swap (`:239-243`), not the flag (`:220`), and the design says so.
  - **Acceptable:** a groups judgement can fail in a tree walk (`GroupsStateSlice.cs:142-143`) and draw *stale* over entries that did not fail. That is honest.
  - **Gap:** the never-located service is not a failure (F7).
- **Rule 14's overflow and ancestry.** Holds. The counts stay true, and a chain longer than 40 is never drawn, its members drawn only by their own places. Tie order and parent cycles are unsaid (F9f).

### 2. The module map

Row a adds no upward edge, provided the port takes the pipe stream's raw line (F8a).

- **The cut works:** `HarnessCapture` (Agents, 9) calls `ChildLineage.Watch` (Participants, 12) today (`HarnessCapture.cs:123`). With the port declared in Agents, handed to `AcpSession` and `HarnessCapture`, implemented in Participants and wired by Loop at `ChatRunner`, `Driver` and `DriverContinuations`, that call is gone.
- **Every other edge row a touches points down:**
  - Loop writes `start` and `end` into Participants.
  - The verb (Work orders, 15) calls Participants, Workplace (`SessionEntries`), Agents (`SessionProcesses.AliveOnThisMachine`) and Service.
  - `ForWork` (Events, 7) takes a delegate.
  - `SessionOutput`'s moment (Context, 13) is read by the modules.
- **Upward edges row a leaves alone:** `Permissions.cs:508` and `:573` (Agents naming `LineageHook` and `ChildLineage.Matcher`), and `HarnessCapture`'s own names of Context types (`:56`, `:68`, `:119`). "The one it touches" is accurate; the census keeps the others.
- **Nothing of Part B is needed:** no journal, move record, store or routing contract.

### 3. Decided rules

- **D173:** the round-4 note's point 1 is stale (F5). ADR-B's "partial visibility said, not filled" is broken for a teammate's part (F6) and by F1's false `lost`.
- **D203, D115:** nothing of row a touches them.
- **D167:** round 4's "exactly as the strip lists them" reads as set and order, which is consistent with C4-1.2.
- **D182:**
  - Point 3's no-regression is met by the numbering on the host.
  - Point 6 holds: one .NET composition shared by the read and the verb.
  - PUSHUX1 holds, except the verb's unbounded wait (F7).
  - The failures' words may name paths; the read is desktop-only, so this is fine.
- **D185 R5:** a state-only surface needs no palette verb. The terminal twin and the Ask Daoris door are named. Holds.
- **Translation parity:**
  - Placeholders survive in every row a key, and `{{why}}` is passed through as a value.
  - zh *已结束* is both `child.finished` and the panel's `stream.ended`. That is acceptable.
  - `child.lost`'s words overstate (F9b).
- **Leaning on the routing half:** none load-bearing. §5.1's "the waiting row's *waits on* opens it" is the routing half's text; row a builds none of it.

### 4. Claims about the code that are wrong

1. **§1.1 (C4-2.1) and §3.6, on the connector's environment.** The design says the connector "reads its session from the environment it inherits … where the start sets `DAORIS_SESSION_ID` (`Adapters.cs:1574`)". In fact:
   - The connector is handed its session in its own server entry (`KnowledgeConnector.SessionScope`, `Driver/Service/KnowledgeConnector.cs:114-124`, offered at `Driver.cs:4523` and `ChatRunner.cs:3030`).
   - "Codex hands an MCP server only its entry's own variables" (`KnowledgeConnector.cs:95-96`, CONNECTOR2).
   - `:1574` is a quest session's line only. A chat's is `:1602`, an intake's `:1539`, a reviewer's, helper's or piece's `:1548`, a pass's `:1560`.
2. **§3.6 rule 2, "which only a crash … leaves".** The ANSWER2 abandon path (`Driver.cs:3831-3836`) also leaves a `start` with no `end`, and its process overlaps a live one.
3. **§3.6 *the fold*, "the end word … `completed`, `failed`, `cancelled` or `disconnected`".**
   - The stream keeps any id-shaped `status` (`ChildBindings.cs:316`). `stopped` is one of the stream's words (`Driver.Tests/fixtures/native/claude-background-cut.stream.jsonl:14`, `Driver.Tests/Agents/NativeStreamsTests.cs:57`).
   - `AcpStreams` defaults a missing `state` to `ended` (`AcpStreams.cs:189`).
4. **§16, "D173's notes gain one … for its points 2 and 4".** Point 1 changed too (`D173.md:5593-5594`).
5. **§4.3, "where none has [landed], they are where today's code has them".** Once mod-0 lands, a file added outside its subdomain's folder fails the gate (foundation §14.5 rule 1, §14.6's last paragraph).

All other round-5 citations I checked hold. That covers `ChildLineage.cs`, `ChildBindings.cs`, `HarnessCapture.cs`, `Acp.cs:1668`, `AcpStreams.cs`, `Driver.cs` (3658 through 4026), `DriverContinuations.cs`, `ChatRunner.cs` (1596, 2896, 2977), `Adapters.cs:1659-1665`, `lineage-hook.mjs`, `LineageFile.cs`, `HeldAnswers.cs`, `held.ts`, `main.tsx`, `StateSlices.cs`, both slices, `PartsStrip.tsx:56`, `groups.ts:148,159`, `AskWork.tsx:111`, `work.panel.json:16-20`, `api.ts:413-415,445`, and the foundation's `:1049`, `:2014`, `:2017`, `:2018` and `:2036`.

### 5. Findings

1. **Blocking: a duplicate resume's `start` closes a live process's generation as `lost`.**
   - **What happens:** `RunAsync` spawns (`Driver.cs:3658`) and starts its capture before it learns the record is already this run's (`:3824-3837`, ANSWER2: "a second resume met working → working"). The second resume's `start` closes the first, still-running generation by rule 2. The live process's later lines carry its own `proc`, so they land in that closed generation. Its children read `lost` or never *working* for the rest of the run, which is an end nobody said.
   - **Smallest change:** the abandon path writes that process's `end` marked as never the record's run (for example `void: true`). The fold reads a void generation's `start` as no boundary, so the live generation stays open.
   - **Test:** two resumes race, the second is abandoned, and the first's child stays *working*.
2. **Blocking: the page's kept number outlives the query's data.**
   - **What happens:** §4.1 has the page drop a delivery whose number is equal or older, "the query keeping what it holds". If the number is kept outside the cache, as `held.ts` keeps its `readers` (`held.ts:16`, `:29`), it survives the garbage collection of an unmounted query. A remount is then answered from held state with the same `n` (`HeldAnswers.cs:150`) and dropped. The query is left holding `undefined`, which TanStack Query fails, and with `retry: false` the view draws a false *failed*. The ask's new work then lands unchanged and tells nothing (`HeldAnswers.cs:160`), so the false *failed* stays until a wake or a focus re-fetch.
   - **Smallest change:** the number lives in the query's data. A delivery to a query holding no data is always taken.
   - **Test:** remount past the cache's garbage-collection time.
3. **Non-blocking: end words outside the four have no state or key.**
   - **What happens:** see §4 item 3. `AtWorkChild.state` and §5.4's mapping have no `stopped` and no fallback, although the panel already words `stopped` (`work.panel.json:18`). The `acp` default `ended` collides with the fold's own `ended`.
   - **Smallest change:** add `stopped` with the panel's key. The `acp` writer writes only the wire's own word. Any other word reads `finished`.
   - **Test:** an unknown word.
4. **Non-blocking: the token cannot reach Codex's connector through the spawn's environment.**
   - **What happens:** see §4 item 1. Every Codex child line, which is all Codex child evidence, will be placed by position. Rule 1's measure is already answered for Codex by CONNECTOR2.
   - **Smallest change:** correct §1.1. §3.6 says the token reaches the hook and Claude Code's connector by inheritance. It may be put in a server entry only where that entry is composed per process (the protocol door's `session/new`), and never in one reused by a record's continuation or retry ("the files the first was handed", `DriverContinuations.cs:1285-1286`). Otherwise it would name the first run's process.
5. **Non-blocking: D173's round-4 note still says row a words each part "as the strip words it"** (`D173.md:5593-5594`).
   - **Smallest change:** round 5's note amends point 1 as well, to "its record's own `sessionState.*` word, never the strip's".
6. **Non-blocking: a teammate's part is drawn as having no subagents.**
   - **What happens:** entries group every record by `EntryKey` with no teammate filter (`Trees/SessionEntries.cs:96-98`). A teammate's id is `origin/id` (`Trees/SessionGroups.cs:132`), which the lineage's session shape refuses (`ChildLineage.cs:44`, `:127`). So the read is "none", a source not read drawn as none.
   - **Smallest change:** such a part's children read *not visible on this machine*, never a count.
   - **Test:** one case.
7. **Non-blocking: the verb waits for ever while the slices are not judged.**
   - **What happens:** with no `Service` the read stays pending (F-0, 2.4). The pipe re-asks every 100 ms with no bound (`InstancePipe.cs:1069-1077`).
   - **Smallest change:** say the view stays *reading* there by design. Bound the verb's wait: past the bound it prints *not read yet* and exits as a tool error.
   - **Test:** one case.
8. **Non-blocking: §4.3 is under-said.**
   - **(a) The port's shape.** If the port takes announcements, `HarnessCapture` must parse the stream with `ChildBindings.StreamLine` (Participants, `ChildBindings.cs:298-323`), which is the same upward edge. **Change:** say the port takes the raw stream line, parsed behind it, or that `StreamLine` moves down.
   - **(b) New files.** **Change:** they go in their subdomain's folder once mod-0 has landed (§4 item 5).
9. **Non-blocking: smaller points.**
   - **(a)** `kind` is optional (`api.ts:445`): a record with none should read as driven.
   - **(b)** `child.lost` says "lost when its session stopped" for a failed part and after a crash, where nothing stopped. "Lost when its part ended" would be accurate.
   - **(c)** *Last seen* should take only lines that change a run. A late call that "begins nothing" wakes nothing (rule 13) and must not move it.
   - **(d)** An attended record not yet in the `sessions` slice has no state: say the read is pending.
   - **(e)** Rule 4's environment test should cover every target kind. `DAORIS_SESSION_ID` is set at five lines (`Adapters.cs:1539-1602`).
   - **(f)** The bound's ties should order by `since` then id, and its chain walk must guard a parent cycle, as `DepthOf` does (`ChildBindings.cs:288`).

9 findings, 2 of them blocking.

**Verdict for row a: accept after named changes the steward can check without another read.** The two required changes are F1 (a void `end` for the abandoned duplicate, with its race test) and F2 (the number kept in the query's data, a delivery to an empty query taken, with the remount test). F3 to F9 are one-line contract or record corrections.

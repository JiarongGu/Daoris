# REVIEWFLOW1 and LINESTOGETHER1: Codex's sealed proposal (2026-10-31)

**Standing:** a sealed proposal under D191, written from `2026-10-31-review-flow-brief.md` alone.

Sealed proposal, written independently. Nothing changed; no `local/` directory or other proposal read. Tests and installed-window proof below are proposed, not performed.

**1. The flow of a session’s page**

**Choice:** keep one attended session and make its position and next action explicit. Distinguish the work’s progression—working, ready for review, landed, integrating—from the page’s sections:

| Encountered section | Shows; standing and next step | Its action |
|---|---|---|
| Head | Target, attended part, authoritative recipient, current hold and who can release it | Answer the current wait, otherwise **Review changes**, a navigation door |
| Parts and chain | Recorded ancestry and workflow relationships; the attended part and head marked separately | **Open part** |
| Relations | Published requests and related lines, with their recorded outcomes and overlaps | **Bring together…**, opening its repository’s Integration form |
| Milestone | Result, cleanup and recovery as independent facts; integration standing where known | **Open integration** |
| Conversation | Earlier ancestry read-only, attended transcript and intended recipient; what sending does next | **Send** |
| Changes | Exact review range, files, gates, destination and acceptance scope | **Accept all…**, or **Accept…** for one part |

**Accept all** moves from `AttendedSession` into the review’s fixed footer beside the changes. The head’s door opens and focuses that review; no return scroll is required. One controller owns the plan, pending state and refusal. Choosing a narrower scope changes that controller’s scope rather than adding another acceptance button.

**Reason:** reading and deciding belong together. **Rejects:** duplicate acceptance controls, another selection independent of the frame, and treating milestone facts as completed steps in an invented history.

**2. A line and its parts**

**Choice:** “head” means the local record reached by the service’s authoritative forward walk, not the entry’s visually `leading` row or simply its newest timestamp.

A chat follows its continuation links. A dispatched quest has its own head; `publishedBy` relates it to its requester without making the requester its recipient. A carry-on receives subsequent words when it is the recorded successor.

Before typing, the box says: **“Writes to part 6 · waits for a driver slot · continues automatically.”** Earlier parts offer **Write to its head**, carrying the draft into that attended part before sending. An unresolved head or unread reach says so and offers no speculative Send.

The send still rechecks authority. A racing move follows the head with the same producer key, author, level and files. A held head keeps the word durably and displays its hold; sending never raises the cap, releases an owner hold or silently chooses another line. Attempted-but-uncertain words remain uncertain until an explicit **Say again**.

**Reason:** FOUNDATION1 already provides the routing and delivery identity. **Rejects:** reconstructing head selection in React, speaking to whichever branch looks active, and interpreting “later” as a failed send.

**3. Related lines of one repository**

**Choice:** show separate relationships: **continues**, **requested by**, **declared overlap**, and **observed changed-path overlap**. Each row names its head, branch, known tip, readiness and integration destination. Missing declarations or failed reads mean **unknown**, never “no overlap.” Show when observations were read.

Reuse INTEGRATE1l’s judgement, extending its read surface where necessary: today its running quest observations cannot establish every ended chat’s changed paths. The page must not imply otherwise. COWORKVIEW lineage identifies team members only where proven.

The appointed steward decides integration grouping and order within its granted policy; without that appointment, the person chooses. A head may recommend a grouping and resolve its own work, but cannot acquire authority over peers.

**Reason:** grouping delivery is management; accepting delivery remains the owner’s. **Rejects:** automatically combining overlapping work, inferring relationships from time or repository alone, and rewriting three session histories into one.

**4. The one door that brings lines together**

**Choice:** **Bring together…** opens one form in **Repositories → Integration**, prefilled with the attended line and evidenced relatives. It names a coordinating head, selected lines, destination and order. Confirmation records a durable operation binding those identities and reviewed tips.

It asks the head to coordinate readiness and resolve its own conflicts **through INTEGRATE1**, rather than merge every peer into its working tree. Each unit retains its workflow and landing gates. One explicitly scoped owner **Accept all** may invoke those existing landing doors, then their integration steps. Already accepted landings use their recorded proofs; records branches use their existing door.

Preflight every included gate. Any changed head, tip or membership invalidates the affected review and requires review again. Record each landing separately: a later refusal leaves earlier successes visible, and retry skips completed effects. Integration retains its own batching, confirmation, promotion fence and recovery.

Refuse cross-repository members, unknown ownership, unread tips, raw session branches presented as records branches, bypassed gates and implicit policy changes. Unsupported repositories get a Setup door.

While running, both pages project the same operation: preparing, awaiting acceptance, queued, replaying, resolving, checking, held, promoted or handed back. Show members, recorded timing, failure and the actor who can release it; never claim atomic delivery.

**Reason:** integration already owns combining and checking branches. **Rejects:** a second merge engine in the head’s tree, acceptance hidden inside “Bring together,” automatic omission of conflicting records, and silent rollback.

**5. What the person presses**

**Choice:** keep this action inventory, offered only where applicable:

| Actions and home | Steward substitution |
|---|---|
| Navigation, folds, parts, earlier transcripts, flow/list, related records, integration, preview, diff layout, viewed marks, retry reads, scroll/focus controls | Reads available; viewer choices stay personal |
| Header menu: stop, hand over, ask helper, pause/resume quest or ask, retry, folder, terminal, detach, archive/unarchive, delete, copy ID | Only D209’s exact stop, finish, own-hold resume, qualified retry and hand-over forms; never generic menu authority |
| Conversation: Send, urgency, stop turn, queued-word edit/remove, Say again, start-from and go-on variants | No free words, urgency or arbitrary continuation |
| Waits: offered question answers, finish/decline, go-ahead, helper, permission, rule, hand-over and answers-alone choices | Only an explicitly admitted existing role act; no widening or owner-only answer |
| Review: scope/member choices, Accept, superseded-part exclusion, Send back; setup/show again, reviewed/not yet, skip, opinion request/stop/self-review/override; discard and hand-off | No delivery acceptance, verdict, override, exclusion, deletion or push |
| Integration: Bring together, request, order, drop, requeue, recheck, decide split/hand-back; owner-only decide land | Eligible management forms only, with accepted gates and D215’s authority checks |
| Each form’s confirm/cancel | Confirmation preserves the underlying action’s authority |

Group preparation needs a closed, reviewed management capability before delegation. Every steward effect records its appointment and generation, checks revocation at execution, and yields to owner intervention. Built-dark authority remains unavailable until activation is proved.

**Reason:** fewer required clicks must come from bounded delegation. **Rejects:** treating appointment as blanket approval or attributing steward actions to the person.

**Read evidence**

Records: `docs/2026-10-31-review-flow-brief.md`, `docs/README.md`, `docs/index/README.md`, `.claude/INDEX.md`, `.claude/skills/doc-loader/SKILL.md`, `TASKS.md`, REVIEWCHAT1’s `docs/task-archive.md` entry; relevant ranges of `docs/decisions/D45.md`, `D137.md`, `D150.md`, `D167.md`, `D191.md`, `D193.md`, `D209.md`, `D212.md`, `D215.md`; `docs/2026-09-19-platform-ux.md`, `docs/2026-10-01-frame-model-design.md`, `docs/2026-10-03-language-design.md`, `docs/2026-10-20-ui-ux-design.md`; translation, person-waits, session-words and bridge-presses knowledge.

Code, under `src/Daoris.Web/src/`: `work/AttendedSession.tsx`, `work/WorkFrame.tsx`, `work/WorkFrame.reviewchat.test.tsx`, `work/entries.ts`, `work/say.ts`, `work/acts.ts`, `work/head/{SessionHead,SessionRelations,PartsFold,PartsFlow}.tsx`, `work/head/relations.ts`, `work/conversation/{line.ts,EarlierParts.tsx,SessionBox.tsx}`, `work/review/{EntryAccept,ReviewFrame,DiffPane}.tsx`, `work/gates/{reviewActs,opinionActs}.ts`, `projects/integration/{IntegrationTab,RepositoryIntegration}.tsx`, `projects/integration/IntegrationTab.test.tsx`, `bridge/repositories/integrate.ts`.

Driver: relevant ranges under `src/Daoris.Desktop/Daoris.Desktop.Driver/`: `Workplace/{CanonicalLine,EntryLands,IntegrationLines,IntegrationSteps,IntegrationRequests,IntegrationRequest,IntegrationPresses}.cs`, `WorkOrders/{SessionGroups.cs,Work/WorkOverlaps.cs,Domain/PathOverlap.cs}`, `Course/{GoOn/WordsHead.cs,Domain/LineCourse.Decide.cs}`, `Participants/ChildLineage.cs`; also `src/Daoris.Service/Daoris.Service.Core/Sessions/{Lines,SessionOpens}.cs` and `src/Daoris.Desktop/Daoris.Desktop.Modules/Loop/SessionWords.cs`.

**6. Rows, smallest first**

**Choice:** prove relocation before adding coordination. **Reason:** each row leaves a usable result. **Rejects:** one combined rewrite.

| Order | Row | Proof |
|---|---|---|
| 1 | REVIEWFLOW1a: acceptance beside changes | Stories and page tests: one control, correct scope, inline refusal, no return scroll |
| 2 | LINESTOGETHER1a: authoritative recipient and overlap facts | Driver/service tests: held words, racing heads, unknown reads; bilingual page tests |
| 3 | REVIEWFLOW1b: standing, next action and related-line navigation | Stories at 80/39/25rem, en/zh, light/dark; keyboard and single-selection tests |
| 4 | LINESTOGETHER1b: durable grouping and existing landing/integration doors | Three-branch git fixture: records preserved, conflict, changed tip, partial success, restart, duplicate press and revocation |
| 5 | LINESTOGETHER1c: operation UI and bounded role admission | Page tests for every phase/refusal; authority census and attribution tests |
| 6 | REVIEWFLOW1c: installed acceptance | Dated before/after shots at 1546/680px, en/zh, both themes; six-part chat plus quest plus filing, held-word delivery, acceptance from changes and integration outcome |
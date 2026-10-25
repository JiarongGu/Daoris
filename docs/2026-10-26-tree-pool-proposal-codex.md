# TREEPOOL1: Codex's sealed proposal (2026-10-26)

One of two sealed proposals written from `docs/2026-10-26-tree-pool-brief.md` and `docs/2026-10-26-tree-pool-survey.md` under D191, by
Codex, read-only, at `a3f5be21a`, without the other. Kept verbatim, its headings set one level down; the steward ran it from outside the
install. The other is `docs/2026-10-26-tree-pool-proposal-opus.md`.

## The proposal
## TREEPOOL1 — proposal

**Reuse the working copy; never reuse the identity of its work.** A pool slot is equipment. An entry identifies the person’s line of work, a branch work unit identifies its committed output, a session record identifies one conversation, and a lease identifies one occupation of equipment.

I propose retaining native resume when Daoris can reacquire the conversation’s original directory. When that directory is occupied, the line can take another free slot **in a new part, handed Daoris’s brief**. This explicitly narrows D189’s resume-first rule; it does not pretend that Claude Code’s conversation follows a branch.

Reset safety rests on three rules:

- Tracked edits, unfinished git operations and unexplained state keep the slot.
- Loose files are preserved outside the slot before they are cleared.
- Preparation uses non-forcing git operations and verifies the result before a harness starts.

This is a proposed design, checked against HEAD `a3f5be21a0e5c5f1ab15d63705f6268269736982`. No files were changed, tests run or install state inspected.

Paths below use the survey’s aliases: `Driver/` means `src/Daoris.Desktop/Daoris.Desktop.Driver/`; `Tests/` its test project; `Core/` means `src/Daoris.Service/Daoris.Service.Core/`; `Web/` means `src/Daoris.Web/src/`; `Modules/` means `src/Daoris.Desktop/Daoris.Desktop.Modules/`.

### 1. The pool’s shape

#### Identity and storage

| Identity | Meaning | Durable record |
|---|---|---|
| Repository pool | One pool for one local git common directory; registry names attach to it | `tree_pools` |
| Slot | A numbered, stable working-copy location belonging to that pool | `tree_pool_slots` |
| Lease | One occupation of a slot, with a generation and recorded holder | `tree_pool_leases` |
| Entry | The existing `entry` key: the person’s line of work in this repository | Existing entry semantics |
| Branch work unit | One branch’s continuing work, possibly executed by several sequential parts | `pool_work_units`, retaining `GrownBranch.Unit`’s purpose |
| Part | One session record and one harness conversation | Session ledger |
| End | One immutable account of what a part left when it released equipment | `pool_work_ends`, with explicit part membership |

The entry and branch work unit are deliberately different. Today a busy entry can start another part alongside its existing part; those parts have separate trees and work to reconcile. The pool must preserve that distinction rather than collapsing every branch of an entry into one branch. This is already the planner’s behaviour, despite D167’s original waiting rule. See survey §2 and `Driver/Quests/Domain/Planner.cs:1105`.

A slot lives at:

```text
<home>/work/tree-pools/<pool-id>/s-<number>
```

It is a linked worktree, normally detached when free. Its stable identity is `(pool-id, number)`, never its current branch. Its incarnation changes if its worktree must be recreated.

This new home entry is outside both historical `HomeEntry.Trees` locations. That is a compatibility boundary: an older folder tidy must not discover a pooled slot as an ordinary stray. Today tree ownership is a prefix test over those locations, and the stray walk enumerates their repository directories. See survey §§1.1, 6; `Driver/Trees/SessionTrees/TreeContext.cs:73`, `Driver/Trees/SessionTrees/TreeStrays.cs:137`.

#### Types and FOUNDATION1’s map

| Module | New types and responsibility |
|---|---|
| **Platform, 1** | Home-entry declarations; existing process-run and job primitives. No pool policy. |
| **Store, 2** | Numbered DDL migrations and transaction primitives. No slot or session decisions. |
| **Settings, 3** | Configuration import/export shapes and extension port, containing primitives. |
| **Service, 4** | Protocol capability and session DTOs containing opaque entry, unit and lease identifiers. No Workplace types. |
| **Events, 7** | New note codes and primitive values. No dependency on pool outcome types. |
| **Knowledge, 8** | Trace consumes Service’s primitive projections. It does not name Workplace’s types. |
| **Agents, 9** | Existing conversation-location and trust checks, given a concrete working directory by the caller. |
| **Workplace, 10** | `PoolId`, `TreeSlotId`, `TreeLeaseId`, `BranchWorkId`, `TreePoolPolicy`, `TreePool`, `PoolLease`, `PoolHold`, `WorkLocation`, `WorkSubject`, `ReusableFolders`, `PoolPreparation`, `PoolRelease`, `PoolRecovery`. Owns pool tables, git operations and eligibility. |
| **Participants, 12** | Conversation bindings gain their original slot/incarnation and working-directory identity in a new binding family. |
| **Context, 13** | Composes the existing brief when a move changes directory. |
| **Course, 14** | Chooses resume, briefed move or wait from location facts supplied by the executor. Owns delivery through FOUNDATION1’s move protocol. |
| **Work orders, 15** | Pool admission, entry/part selection and the reader census for groups and inventory. |
| **Review, 16** | Obtains and releases Workplace hold tokens for served output and heavy runs. |
| **Workflows, 18** | Joins records by entry and branch work unit. |
| **Setup, 19** | Applies its existing concurrency restriction alongside pool admission. |
| **Loop, 21** | Composes and executes these decisions. Owns no pool eligibility rule. |

Workplace accepts facts and hold tokens; it never names `ChatRunner`, `ContextBrief`, workflow records, Review’s classes or planner decisions. Lower modules needing a Workplace answer declare a primitive port, implemented from Workplace. HEAD already uses this pattern through module initializers in `Driver/Trees/ServiceWiring.cs:13` and `Driver/Trees/InstallWiring.cs:13`.

Store migrations can contain the family’s DDL without naming its C# types, as the existing `bindings` migration does. Typed pool reads and writes remain Workplace’s. This follows FOUNDATION1 §13 and §14, especially `docs/2026-10-25-foundation-design.md:1970`, `:2342` and `:2361`.

### 2. The nineteen questions

#### 1. The slot and its lease

The desktop store is authoritative for ownership. Git’s worktree lock protects the working copy from ordinary removal; it is not the allocation lock.

Allocation takes a slot-specific OS handle and then, in one `BEGIN IMMEDIATE` transaction:

1. Checks the slot is eligible and has no standing lease.
2. Creates a lease with an immutable id, increasing generation, process-run identity, start intent and branch work unit.
3. Marks it `preparing`.

A partial unique index permits one standing lease per slot and one writable occupation per branch work unit. Every mutating phase checks the lease id, generation and executor run before recording its effect.

The slot remains git-locked even while free, under a new reason such as:

```text
Daoris pool <pool-id> slot <number>
```

Lease details belong in the store. This reason deliberately matches neither the old session-lock prefix nor an interrupted opening. Existing code only releases recognised session locks; interrupted-opening cleanup has its own recogniser. See survey §5; `Driver/Trees/SessionTrees/SessionLocks.cs:28`, `Driver/Trees/SessionTrees/TreeStrays.cs:181`.

A crash releases the OS handle, **not the durable lease**. A reconciler can claim its execution only after the previous process run is proven dead by PID and start time. An unread start time means alive. It then reconciles the service record, git state, containment jobs, preservation journal and holds. A parked or dirty lease remains retained.

D201’s launcher supplies containment; it does not allocate or release slots. The start executor allocates; the end executor releases; either loop can reconcile a dead executor through the same Workplace operation.

**Reason and counterexample:** process death is evidence that an executor stopped, not evidence that its files are disposable. A timeout must not hand a paused but live executor’s directory to another agent. This follows FOUNDATION1’s claim rule at `docs/2026-10-25-foundation-design.md:2042`.

#### 2. The line and its branch

Each new branch work unit receives a create-only name:

```text
daoris/s-<full-random-id>
```

The name is independent of the slot number and is never assigned to unrelated work. Sequential parts continuing that unit use its branch; a genuinely parallel part gets another unit and branch.

The unit retains the useful facts of `GrownBranch`: original branch, current branch, base, canonical line, provenance, unit id and later landing evidence. These facts are recorded when the branch is created, whether or not a worktree was created then. Today they are coupled inside `OpenAsync`; see survey §§1.1, 5 and `Driver/Trees/SessionTrees/SessionTrees.cs:380`.

`GrownBranch.Now` remains the learned current branch, with two changes:

- Learning is scoped to the unit’s current lease.
- A pooled reader has no folder-derived fallback. Missing or inconsistent ownership is a hold.

An agent-created branch is followed after validation; it is not automatically considered Daoris-owned for deletion.

**Reason and counterexample:** after A releases `s-1` and B occupies it, `daoris/s-1` identifies neither A nor B. Today’s derivation is explicit at `Driver/Trees/SessionTrees/TreeFollows.cs:153`, surveyed in §6.

#### 3. The line’s identity and every way back

The person’s line is identified by the existing entry key. Its particular branch of work is identified by `BranchWorkId`. A part names its entry and unit explicitly. A lease supplies its current physical location.

The allocation rule for continuations is:

| Continuation | Location |
|---|---|
| Resume, carry-on, words going on | Existing retained lease; otherwise reacquire the original slot if free, else another free slot |
| Further sequential part | Existing unit and retained/free slot, when the unit is not being written |
| Further part alongside a live part | New unit and another free slot, preserving today’s parallel-part behaviour |
| Hand-over or limit move | Transfer the existing occupation where possible; otherwise allocate as a continuation |
| Lost take brought forward | Its preserved unit, then existing source-sync semantics |
| TREEGONE1 | Validate/recreate the allocated slot on the unit’s recorded branch or preserved tip; never infer from its name |

A requested unit already held by a writer waits. Another free slot does not authorise a second writer on that unit. When another unit occupies the old slot, the continuation can use a different slot subject to question 4.

An absent branch is distinguished from an unread branch. A preserved unit ref can restore a missing owned branch create-only; an unexplained conflicting branch is held. No continuation falls back to the registered checkout.

**Reason and counterexample:** an old session’s historical path must not send its next words to B’s checkout. HEAD currently chooses continuations by path and can ultimately select `root`; see survey §2 and `Driver/Loop/Driver.cs:2099`, `:2129`, `:2217`.

The complete reader disposition appears in section 4.

#### 4. The agent’s conversation

A binding records the exact original working directory, slot incarnation, agent, account and native conversation id.

Native resume is attempted only after Daoris has reacquired that **exact directory**, restored the correct unit there, and passed the existing account, trust and conversation checks. An intervening occupation does not itself erase a conversation identified by its explicit id, but it supplies no right to use the directory while another lease holds it.

If the original slot is busy and another is free, Course opens a new part in that other slot with Daoris’s brief. It records a new reason, `workplace-moved`. It never sends the old native id from a different directory. If all slots are held, the words wait.

Parked sessions retain their slot and therefore retain the existing resume opportunity. A conversation that already moved to a new part never returns to its superseded native conversation.

Append a note to **D189 points 2 and 3**: for released pooled equipment, resume-first means first prefer the original directory when available; it does not reserve that directory indefinitely or wait for it while another free slot can run the line with the brief. Report the move and usage; claim no cost saving for it.

**Reason and counterexample:** Claude Code’s project directory and trust are path-based; checking out the branch elsewhere does not relocate its conversation. See survey §6, `Driver/Platform/ToolHomeLeftovers.cs:54`, `Driver/Accounts/SignIns/ClaudeTrust.cs:114`, and SWITCHCTX1 §1.5 at `docs/2026-10-24-switch-context-design.md:184`.

#### 5. The start: brought to the line

Preparation is a journalled operation under the lease and the existing repository start/replay exclusion.

1. Validate repository identity, worktree registration, slot incarnation and the pool’s own git lock.
2. Prove that no harness, terminal, heavy job, served review or other hold still uses it.
3. Read HEAD, index flags, status including ignored/untracked paths, unfinished git operations, links and nested repositories.
4. Ensure the previous committed tip is durably pinned. Tracked edits or unexplained state refuse preparation.
5. Preserve loose non-cache files through the recovery operation described below.
6. Resolve the canonical line to a local commit id, without fetching. Reject cache paths that collide with tracked paths at the target.
7. Detach at the currently protected commit, then reset the detached worktree to the selected commit using `reset --keep`. Use non-forcing switches with `--no-overwrite-ignore`.
8. For a new unit, create its branch at that selected base. For a continuation, check out its recorded branch and apply the existing local replay/line-sync policy without dropping its own commits. Chain and landed-part bases retain their existing precedence.
9. Verify HEAD, index, tracked files, remaining loose files, cache exclusions and lease ownership. Only then mark `ready`, open the keyed session record and launch.

“Reset” here means restoring the selected tracked tree. It does not require `reset --hard`. There is no blanket `clean -fdx`, forced checkout, forced branch reuse or recursive submodule reset.

A held file, target collision, nested repository, person’s lock, unknown link, unread directory or failed operation leaves `preparation-held`. Partial preparation is reconciled before reuse; it is never labelled free because a subprocess exited.

**Reason and counterexample:** a previous clean observation is not permission for a later destructive reset. Today uncommitted paths hold a tree, and assume-unchanged paths and nested repositories require additional guards. See survey §§1.3–1.4; `Driver/Trees/SessionTrees/ContentHolds.cs:563`, `Driver/Trees/SessionTrees/TreeSetAside.cs:52`.

#### 6. The end: output and no output

The end executor waits for the harness and its contained processes to finish, then evaluates the actual work left.

- **Done with output:** committed candidate work exists on the unit’s branch; its exact tip and provenance are kept; its finished record is durable; no remaining condition holds the equipment.
- **Done without output:** there is no candidate committed change to land. Preserve the end record, detach the slot, and remove the unused owned branch only through an exact-tip transaction resting on its base holder.
- **Uncommitted tracked work:** the part’s run may have ended, but equipment release is held. The person sees “Uncommitted work keeps this tree”, with a way to inspect or continue it. Daoris makes no commit.
- **Loose files:** preserve them outside the slot, record their names and distinguish them from branch output. The end says that those files were kept separately and will not be included in Accept.

Loose-file preservation uses a new machine-local recovery area outside the legacy tidy:

```text
<home>/work/tree-pool-kept/<end-id>/...
```

A durable journal names each source and unique destination before a same-volume move. Nothing overwrites an existing destination. Completion verifies the moved content and records its digest. Interrupted moves retain both their journal and all surviving files. Cross-volume or unreadable cases hold the slot; they do not fall back to copy-and-delete.

Person-made links and nested repositories are not moved or cleared automatically. They retain the slot.

**Before reset, loose work has therefore already left the slot safely; tracked work remaining in it must be held by refs.** This preserves D164’s rule that Daoris does not commit, while meeting the brief’s requirement that a cleared untracked file be kept somewhere defined.

**Reason and counterexample:** a ref protecting HEAD protects neither an edited tracked file nor `notes.txt`. Current landing refuses precisely because uncommitted work would be left behind: survey §1.3; `Driver/Trees/SessionTrees/TreeLands.cs:221`.

#### 7. What holds a slot

| Condition | Holds equipment? | Release |
|---|---|---|
| Running harness, including between turns | Yes | Process and descendants finish |
| Parked record | Yes | Answer and eventual safe end, or the person ends the park |
| Accepted start or continuation reservation | Yes | Starts, is cancelled safely, or is reconciled |
| Words merely waiting on an ended record | No | They remain durable; admission takes equipment before delivery |
| Helper wait | Yes | Helper answer is consumed, or the wait ends and release is safe |
| Served review build awaiting verdict | Yes | Serving stops after verdict/cancellation; hold is then released |
| Setup/access continuation at the next look | Yes | Continuation completes or is cancelled safely |
| Clean D79 wait | No | Branch/context remain kept; answer allocates equipment |
| Clean kept lost take | No | Unit, exact tip, transcript and kept files remain available |
| Dirty lost take | Yes | Work is committed or explicitly dealt with |
| Done without output | No | Safe end record and branch retirement complete |
| Person’s terminal in the slot | Yes | Terminal and its descendants close |
| Unknown process/file state or failed preparation | Yes | Reconciliation proves it safe |

A served build is registered as a hold **before serving starts**, not discovered only at the next tidy. Its hold survives a shell crash. Closing a tab does not silently discard the review’s required build.

Append notes to **D79, D137 and D178** distinguishing retained work from retained equipment. D178’s clean lost take remains indefinitely recoverable, but no longer reserves a working copy indefinitely.

**Reason and counterexample:** preserving every resumable record as a physical reservation would fill a two-slot pool with two ended conversations. Conversely, freeing a served tree would let a browser reload display the next session’s build. Existing holds are surveyed in §8.4 and implemented at `Driver/Trees/Branches/BranchTidying.cs:140`, `Driver/Review/ReviewDesk.cs:102`.

#### 8. The count

Use a Workplace-owned desktop-store policy:

```text
tree_pool_policies:
  pool, mode, slots
```

Its import/export shape is `treePools`, addressed by repository names, containing no machine path. It is not a registry property and does not introduce a tracked `daoris.json` setting in TREEPOOL1.

At opt-in, absent an explicit count, initialise `slots` to the machine’s current `cap`, at least one. That becomes a stored count; later changes to the machine cap do not unexpectedly remove repository equipment.

All pooled writers, including the person’s conversations, require a slot. Person-held conversations remain excluded from **CAPCOUNT1’s machine agent count**; they are newly subject to the repository’s equipment bound. A queued conversation consumes no machine cap while it has no run.

Driven admission requires all applicable permissions:

```text
machine capacity
AND repository equipment
AND existing work/entry exclusion
AND setup atOnce
AND lane admission, once DEV6 exists
```

D115’s future lane count limits driven lanes; it does not create more working copies or exempt conversations. Helpers, partners, reviewers and rooms keep their existing separate places and machine-cap rules. Maintenance needing a session worktree uses a free pool slot, not an overflow slot.

**Reason and counterexample:** the machine cap cannot bound physical equipment when conversations do not spend it and parks can retain equipment after a process exits. Today that distinction is explicit in survey §3 and `Driver/Quests/Domain/Planner.cs:192`.

#### 9. Lazy creation, lowering and repository departure

Declare slot rows up to the target; create no folders until allocation first needs them. Reuse an existing eligible slot before materialising another.

Lowering marks surplus slots `draining`, highest number first. They receive no new unrelated work. Busy, dirty, parked or otherwise held slots stay until their holds end. The UI reports both target and retained surplus, for example “2 slots; 1 more waiting to retire.”

Retirement is its own proof and operation: preserve work, complete records, obtain the exclusive occupation, validate links, remove the selected worktree safely, then retire its row. It is never an age policy.

Repository departure stops admission and presents the remaining equipment and work. Unregistering a repository is not permission to delete held work.

Existing legacy trees remain legacy trees during transition; they are not silently counted as reusable slots. Active legacy occupations participate in transition admission so opt-in does not suddenly add a full pool of concurrent runs alongside them.

**Reason and counterexample:** lowering from four to two must not reset slots three and four while their sessions are still working. Existing removal refusals are surveyed in §1.5 and begin at `Driver/Trees/SessionTrees/TreeRemovals.cs:15`.

#### 10. Finished records, recovery refs and removal

The normal record-making moment becomes **equipment release**, not folder removal.

Each end record contains explicit session membership, unit, lease interval, bases, exact tip, commit attribution, landing facts, kept-file manifest and outcome. It never selects “all sessions before this time on this path.”

Before detachment, keep an immutable end ref:

```text
refs/daoris/pool/<pool-id>/<unit-id>/ends/<end-id>
```

A separate current-tip ref can advance with the unit. Immutable end refs protect earlier tips across later rebases. A failed record post is retried from a durable desktop outbox; it does not require retaining an otherwise safe working copy. Branch/ref retirement waits for the required record acknowledgement.

Session branches can be retired after existing landing/content proofs, independently of slot lifetime. Every ref removal verifies exactly the refs and commits its proof read, in one `update-ref --stdin` transaction. Pool pins are excluded from “a person’s holder” proofs.

Keep existing `refs/daoris/discarded/` semantics. Pool end refs have no age-only deletion: they follow the recovery rule when an independent person-owned ref holds the exact committed history, otherwise the person’s explicit disposal.

For D195:

- Legacy `trees` and `set-aside` retain their meanings.
- New `tree-pool` equipment retires by configured count/repository departure, behind proof.
- New `pool-kept` payloads are recovery material, with no automatic age purge in TREEPOOL1.
- `GoesByItself` no longer decides pooled equipment lifetime.

**Reason and counterexample:** moving the record trigger without changing membership would attribute A’s commits to B merely because both occupied `s-1`. See survey §6; `Core/Sessions/WorkRecords.cs:446`. Ref transaction requirements are implemented at `Driver/Trees/SessionTrees/ContentHolds.cs:456`.

#### 11. Dependencies and build outputs

Introduce one Workplace catalogue, `ReusableFolders`, read by scratch classification, pool preparation, dependency seeding and slimming. Each entry names capabilities separately: **retain**, **seed**, **validate**, **slim**. A folder being retainable does not make it seedable or safe to purge.

The initial retainable categories are today’s build-folder set plus `wwwroot`:

```text
node_modules, bin, obj, dist, build, out, .angular, .next,
coverage, target, __pycache__, .venv, .gradle, wwwroot
```

Retention also requires that the particular path is ignored, does not collide with tracked target paths, and passes link/nested-repository guards. Names alone never authorise deletion. Other ignored material is preserved separately rather than assumed to be a build cache.

For npm, record the lockfile fingerprint associated with a validated install:

- Matching lockfiles and install evidence permit the “skip `npm ci`” instruction.
- Changed, missing or uncertain evidence means “dependencies were retained, but are not validated for these lockfiles; run the repository’s install procedure.”
- Do not silently delete stale dependencies during preparation.

Keep `bin/`, `obj/` and other build outputs across commits. Describe them as retained incremental outputs, never as proof that a build or test passed at the new commit.

A newly materialised or recreated slot may seed npm dependencies through TREEDEPS1’s existing matching-lockfile copy. It claims no copy when the gate fails. This is a dated amendment to TREEGONE1’s pooled remake, which today copies nothing.

**Reason and counterexample:** retaining `node_modules` from lockfile A must not produce the current instruction that `npm ci` is unnecessary at lockfile B. See survey §4 and `Driver/Trees/SessionTrees/TreeDependencies.cs:85`. The three current policies differ at `TreeScratch.cs:15`, `Driver/Platform/KeepKinds.cs:11` and `TreeDependencies.cs:85`; the catalogue unifies classification without pretending their operations are identical.

#### 12. Git rules and Windows

- Preserve the `daoris/` branch prefix.
- Refuse a branch checked out in another standing worktree; never use `--ignore-other-worktrees`.
- Keep the existing repository `TreeLock` start/replay exclusion, plus the slot-specific occupation handle.
- Require the pool’s own git lock to be present. Replacing or finding another reason holds the slot; never lift a person’s lock.
- Recognise legacy opening, session and pass locks unchanged. The pool’s permanent lock is a fifth, distinct reason.
- Use `core.longpaths=true` for every git operation.
- Keep known copied workspace junctions in place without following them. Unknown or changed links hold preparation.
- Require contained jobs and terminal jobs to be empty before release.
- A refused reset/switch is a held preparation. Retry from observed state; do not assume git rolled every file back.

Git’s lock protects removal, not every possible external write. The allocation protocol must therefore cover every Daoris writer, and native tests must cover held-file failures and partial effects. It must not claim that a git lock excludes arbitrary programs on the machine.

**Reason and counterexample:** `Directory.Exists` can name a leftover folder that is no worktree, and ordinary removal can follow junctions outside it. See survey §5; `Driver/Trees/SessionTrees/TreeReturns.cs:50`, D51’s TREELINK1 note. Containment’s actual limits are documented at `Driver/Platform/ProcessJob.cs:15`.

#### 13. D51’s rejected alternative

Append a dated D51 amendment:

> A session’s branch and work identity are never reassigned. A managed working-copy slot may be reused between completed occupations, after its work is kept and its release is proved. One writer still holds each working copy. Parallel occupations use distinct slots. The registered checkout is never switched or cleaned by the pool.

Amend working-surface rules 2 and 4 for pooled trees: location is equipment rather than work identity, and the opening states which dependencies/build outputs were retained and validated.

The rejection of one directory switching branches still applies to concurrent work and the person’s working checkout. The pool switches **only released equipment**, with enough distinct slots for admitted concurrent occupations. It does not solve contention by switching an occupied tree.

**Reason and counterexample:** a pool of one cannot pretend to offer two simultaneous writers. D51’s rejection is at `docs/decisions/D51.md:47`; the current working-surface rule is at `docs/2026-09-21-working-surface-design.md:77`.

#### 14. Records and older builds

A pooled slot path must never be written into a legacy `sessions.tree` row.

I propose an explicit compatibility boundary:

- Legacy session rows and APIs retain their existing interpretation.
- Pooled session records live in new service tables, starting with `pool_sessions`, with new protocol doors.
- Their local record names entry, branch work unit and lease identifiers, not a mutable path as permanent session identity.
- New desktop readers merge the two record families.
- Old APIs expose no executable pooled session row, including to an older client attached to a newer host.
- A newer client requires an echoed pool protocol capability before opening or continuing pooled work. An older host produces a hold, with no fallback POST to the old session door.

The service owns its session state, words, keyed opens and settlements; it opens no desktop store. Allocation, paths and lease policy remain in `desktop.db`. The service sees opaque occupation identifiers for exclusion, not filesystem allocation.

The pooled session engine must share the ledger’s lifecycle rules rather than inventing a second state machine. A newer host checks active work and quest exclusions across both families.

This is more work than adding nullable columns. It is necessary because `tree = null` means main/unknown tree, and a new marker cannot constrain an older host. See survey §6, `Core/Sessions/Sessions.cs:89`, `:1147`, and `.claude/knowledge/compatible-builds.md`.

**Reason and counterexample:** setting A’s legacy `tree` to null after release could send its old-build continuation to the root; leaving it at `s-1` could send it into B’s work. Neither is a compatible reading.

Section 4 gives every record’s disposition and the cost of this boundary.

#### 15. The words

Keep `started.tree` and `started.tree-copied` unchanged. Add codes for pooled starts, release outcomes, preparation holds and directory-changing moves.

Suggested chrome:

| English | Chinese |
|---|---|
| Reused a tree at `{base}` for `{branch}`. | 已复用工作树，从 `{base}` 开始，在 `{branch}` 上工作。 |
| Dependencies and build outputs were retained. | 已保留依赖和构建产物。 |
| Dependencies match the lockfiles; no install is needed there. | 依赖与锁文件一致，无需重新安装。 |
| Retained dependencies need checking against these lockfiles. | 保留的依赖需要按当前锁文件检查。 |
| Done with output | 已完成，有产出 |
| Done without output | 已完成，无产出 |
| Waiting for a free tree | 等待可用工作树 |
| Uncommitted work keeps this tree | 未提交的工作仍占用此工作树 |
| Continued with Daoris’s brief in another tree | 已在另一工作树中使用道衍的简报继续 |

The first opening also states that this may release the tree for reuse and that later continuation may use a brief. Details explain kept loose files and cache validation.

Codes carry branch, base commit, counts and reason codes—no path or account name. Preserve authors’ words and git’s refusal text verbatim. A queue draws one neutral line, elapsed time and holder details behind disclosure; it does not light the person’s bell unless a concrete action of theirs is required.

The head/foot use one wait projection. Person-actionable preservation holds enter `NeedsAPerson` and the existing wait/attention chain together.

**Reason and counterexample:** translating an old “fresh tree” code into “reused tree” rewrites historical evidence. HEAD explicitly forbids this at `Driver/Trees/SessionTrees/SessionTrees.cs:443`; survey §8.11. The one-line head budget is recorded at `docs/2026-10-20-ui-ux-design.md:199`.

#### 16. The install’s trees

Do not adopt existing trees in place as pooled slots. Their paths remain promises made to legacy records, native conversations and older builds.

The install transition is:

1. Inventory existing trees and set-aside folders through an owner-reviewed migration plan.
2. Leave live, parked, dirty, served and otherwise retained trees operating under their old lifecycle.
3. Enable new pooled work separately.
4. Retire legacy trees only after their recorded work and recovery evidence are checked and the owner approves the listed removals.
5. Treat old moved-aside folders under their existing manifests and retention rules; any accelerated removal is a separate owner-approved action.

Qualified dependencies may be copied from a retiring tree through the same lockfile/link gate. No tree is relabelled, moved or reset merely because it looks idle.

The temporary disk footprint includes both systems. Opt-in is not a claim that the existing backlog of folders has disappeared.

**Reason and counterexample:** turning an ended legacy tree into slot one would preserve its path string while breaking what that path meant. Survey §§1.1, 6; `Driver/Trees/SessionTrees/TreeContext.cs:61`.

#### 17. Build order

All rows touching today’s `Trees/` wait for **FOUNDATION1 mod-x** and its applicable edge cuts. New code is written directly in Workplace. Pooled reuse remains disabled until the reader, compatibility, continuation, start and end rows have all landed.

The concrete rows are in section 5.

**Reason and counterexample:** introducing reuse before fixing even one historical reader can make an otherwise correct branch resolver show or land another session’s work.

#### 18. What is the owner’s

The owner decides the explicit amendments to direction, the downgrade cost, installation opt-in and any actual disposal of existing data. Those decisions are listed separately in section 6.

Routine implementation choices—table names, immutable identifiers, transaction boundaries, refusing unread state and splitting rows—do not each require another approval.

**Reason and counterexample:** accepting a design does not authorise deleting whichever installed folders a later script happens to find.

#### 19. Proof

Every row has focused unit, git and Process proof as applicable, plus the reached gates. Integration includes the whole suites named by the brief, the git Process half, family rehearsal and deployment rehearsal.

The decisive fixture is:

1. A acquires slot one, commits output and leaves an untracked file.
2. Its end preserves that file and pins its committed tip.
3. B acquires the same slot at the selected line commit.
4. Dependency/build sentinel files survive.
5. A’s tracked work and loose file are absent from B’s working tree.
6. A’s branch, review, record, kept file and later continuation remain correct.

Tests also kill processes at every marked boundary, exercise every holder, and run older builds’ own reads/writes against both record families.

Install proof measures a day’s bytes and start-to-first-turn latency before and after opt-in. No such measurement was made for this proposal.

**Reason and counterexample:** a fast second checkout alone proves neither preservation nor correct historical attribution. The survey’s existing measurements in §§0 and 4 are baselines, not results for a pool.

### 3. The lifecycle

```text
declared slot
    → lazy materialisation
    → preparing
    → ready
    → session occupation
    → ending
        → retained, with named holds
        → preserving loose files
        → recording exact output
        → detached and free
    → next occupation
```

A crash enters reconciliation at the last durable phase; it never jumps to `free`.

At a fresh start, *brought to the line* means the selected local canonical commit, or the existing explicit chain/part base. At a continuation it additionally means restoring that unit’s work and applying the established local synchronisation policy. It never means erasing the continuation’s commits to obtain a clean canonical checkout.

At an end, the equipment is free only after:

- Processes and all durable holds have let go.
- Committed output is pinned.
- Loose files have been preserved.
- No tracked edits or unexplained state remain.
- The finished record and release outcome are durable.
- The old branch is detached from the slot.

A completed run can therefore remain visibly held for preservation. This is distinct from both successful end outcomes.

A configured count bounds equipment, not retained branches, recovery payloads or bytes. Cache growth and preserved loose files can still consume disk. The pool removes repeated working-copy creation; it does not make an unqualified promise of bounded disk use.

### 4. Records, readers and older builds

#### Stored records

This is the disposition of every stored record named by survey §6.

| Record | Pooled disposition | Older-build reading |
|---|---|---|
| `sessions.tree` | Legacy rows unchanged. Pooled sessions use `pool_sessions`; current location is resolved separately. | Reads only legacy rows and their original paths. |
| `work_records`, `work_record_sessions` | Legacy records unchanged. New pool work-record tables use immutable end ids and explicit part membership. | Cannot select pooled records by reused path. |
| Helper, partner and opinion desks | New host resolves a pooled lead through the new session family, then compares the clone’s actual path against the current occupation. Clone storage/lifecycle is unchanged. | No pooled lead is offered through an old executable session API. |
| `session-branches.json` / `GrownBranch` | Legacy family unchanged. New unit facts live in `pool_work_units`; do not project pooled ownership into the old file. | Reads only legacy ownership. It may still see ordinary git branch names. |
| `landings.json` / `LandedBranch` | Preserve old rows. Pooled landing evidence uses a new Workplace table, retaining existing landing facts and unit membership. | No pooled landing instructions are placed in its actionable file. |
| Auto-landing records | New due records identify unit, session and judged tip; no path-based supersession. | No pooled dues in its old actionable family. |
| Set-aside manifests | Historical manifests unchanged. Pool release has its own end/preservation journal, outside `.tidied`. | Existing slim/purge cannot consume pooled preservation payloads. |
| Heavy holder | Keep its current meaning: the actual checkout of that live heavy run. That run holds the slot until it and its job end. | Its path remains true for the holder’s lifetime; it is not a historical work identity. |
| Desktop bindings and picks | Existing families unchanged. New pool binding family retains native id, account pick, original slot and incarnation. | Existing readers cannot resume a pooled id without its location contract. |
| Git registrations and `locked` | Stable slot registration, new permanent lock reason. | Git can list it; legacy home ownership and lock recognisers do not adopt it. |
| Agent homes | Unchanged. No conversation relocation, trust copying or filename rewriting. | Native files remain where the agent put them. |
| Opening note | Old codes and texts unchanged; new pool codes contain no path values. | Where a compatible read-only projection is available, unknown codes retain their English fallback. |

Sources checked: survey §6; `Core/Sessions/WorkRecords.cs:30`, `:446`; `Core/Sessions/HelperDesk.cs:85`; `PartnerDesk.cs:65`; `Core/Review/OpinionDesk.cs:103`; `Driver/Trees/Branches/SessionBranches.cs:19`; `Driver/Trees/LandedBranches.cs:14`; `Driver/Sessions/Heavy/HeavySlot.cs:19`; `Driver/Store/DesktopStore.cs:107`.

Pooled session event streams, attachments, process marks and executable boxes also receive a separate home entry. Otherwise an older history/orphan cleanup could find files belonging to sessions absent from its ledger. Course records that an older build scans for recovery likewise require a new family; a new session flag is insufficient. The row must audit whole-family scans, not just lookups by known session id.

Ordinary pathless quest and entry records retain their meanings. Existing quest-taking arbitration still applies across build versions. An older host cannot manage pooled sessions: the newer executor retains their leases and words when the required protocol disappears.

**Downgrade cost:** an older build does not show or continue pooled local lines. Their records, branches, refs, files and native conversations remain for a pool-aware build’s return. It can continue its legacy work. This is a deliberate limitation, preferable to letting an older build act on a misleading path.

Separate roots and tables do not hide git refs. An older branch tidy may still see `daoris/*`; HEAD enumerates that namespace at `Driver/Trees/SessionTrees/TreeSweeps.cs:46`. Pool end refs therefore independently protect output. A returning newer build can reconstruct a removed owned branch from its exact preserved tip. Tests must prove that old recovery retention does not remove the new ref namespace.

#### Reader census

Each row below requires fixture coverage for its individual sites. The implementation census lists every site as legacy-only, migrated or intentionally unchanged, and fails when a new uncategorised derivation appears.

| Reader family and checked sites | New reading |
|---|---|
| `TreeFollows.cs:74`, `:91`, `:132`, `:153`; `TreeReturns.cs:61` | Unit lookup and validated current occupation; folder derivation remains legacy-only. |
| `TreeRemovals.cs:42`, `:61`; `TreeLands.cs:1005`, `:1014`; `TreeUpdates.cs:369`; `TreeWorkReads.cs:157`; `SessionBranchDiscard.cs:79`, `:102` | Explicit `WorkSubject` with branch, base and unit. A slot-only request cannot identify historical output. |
| `WorkUnits.cs:92`; `WorkInventory.cs:400`, `:455`; `WorkInventories.cs:121` | Join sessions, branches, ends and physical occupations by explicit ids. Folder shorthand remains a legacy command input only. |
| `SessionGroups.cs:66`, `:115`; `Sessions/Domain/AskWork.cs:195` | Offers identify unit/branch, with an optional current occupation. |
| `Loop/Driver.cs:2196`, `:3436`; `WorkflowGraphRun.cs:343` | Parent/earlier work comes from unit and landing provenance, not a directory basename. |
| `Planner.cs:1120`, `:1353`; `Sessions/RunningSessions.cs:140` | Separate entry selection, writable-unit exclusion and slot admission. |
| `EntryLands.cs:108` | Deduplicate identical branch work units, not equal paths. Preserve existing joining/landing order. |
| `Workflows/ChatRuns.cs:66`, `:83`; `WorkflowRun.Read.cs:208` | Group by entry and work-unit lineage; preserve explicit taken-quest provenance. |
| `AutoLander.cs:209` | Newer output supersedes by the same unit and relevant submission/tip, never by shared equipment. |
| `SessionReviews.cs:40`, `:55` | Review the recorded candidate commit/branch; read dirty work only through a verified current occupation. |
| `ReviewDesk.cs:102`, `:176`; `ReviewServed.cs:22` | Use an explicit served-build hold and exact unit/lease. “Show again” must not read a later occupant. |
| `BranchTidying.cs:140`; `ServiceClient.cs:2226` | Legacy path holds remain. Pool occupancy is read from leases/holds; waiting words alone preserve logical work. |
| `ContentHolds.cs:573` | Pool attribution uses lease/end boundaries and manifests, never directory creation time. |
| Carry-on context and helper/partner capture | Resolve the unit’s verified current occupation before reading files. Ended work uses pinned output and kept files. |
| `TreeStrays.cs:137`, `:181` | Legacy roots retain their rules. Pool reconciliation owns the new root; slots are not age-based strays. |
| `Web/work/WorkFrame.tsx:477`; `attention/attention.ts:640`; `identity.ts:129` | Display explicit branch/work identity. Slot name appears only as equipment detail. |
| `WorkFrame.tsx:1309`; `acts.ts:100`, `:166` | Terminal/folder actions resolve a current occupation at the press and hold it while a terminal runs. No historical-path or root fallback for pooled records. |
| `HowItCameToBe.tsx:427`; `EarlierParts.tsx:56` | Preserve historical location as history; report work continuity separately from equipment equality. |
| Repository trees toggle | Keep the legacy choice. Pool mode/count is a distinct new policy until migration and opt-in complete. |

There are relevant HEAD changes since the survey: Review now runs through `ReviewGraphReads`, called at `Driver/Trees/GitReads/SessionReviews.cs:55`, and desktop bindings/picks already exist in SQLite at `Driver/Store/DesktopStore.cs:107`. The rows must extend those implementations, not recreate the survey’s earlier arrangement.

The pool changes **selection, identity and equipment acquisition** around landing. It does not redesign replay, conflict acceptance, combined entry landing, pull-request behaviour or verification gates. LINESTOGETHER1, ACCEPTCONFLICT1 and REVIEWFLOW1 remain separate work.

### 5. Rows after FOUNDATION1 mod-x

Each row lands alone. Production reuse stays off until row P8. Legacy fixtures keep their existing behaviour; pool fixtures exercise the new contract explicitly.

| Row | Changes | What a person sees | Required proof |
|---|---|---|---|
| **P1 — slot and lease, unused** | Workplace identifiers and pure eligibility; new desktop tables; declared/lazy slot states; allocation/release claims; reader census initially marked unmoved. No production caller. | Nothing. | Unit state tables; two processes racing one slot; live paused claimant cannot be stolen; PID reuse and unread start time; crash after claim; semantic structure gate with no new upward edge. |
| **P2 — explicit branch subject** | Add `WorkSubject`; move Workplace’s branch/folder derivations behind a legacy resolver; record branch creation independently of tree creation. No reuse. | Nothing. | Every Workplace resolver site: same slot/different unit, same unit/different slot, agent-created branch, detached state, unread ref, missing ref, branch held elsewhere. Git tests preserve existing landing answers. |
| **P3 — compatibility boundary, unused** | New service session family and protocol doors; new pool binding/event-file homes; new end-record shape with explicit membership. Old endpoints remain legacy-only. | Nothing. | Old SQL over migrated stores; old client/new host; new client/old host; downgrade/return; old cleanup over new roots; no pooled path in legacy rows or remote wire; idempotent open with lost response. No harness starts without protocol echo. |
| **P4a — Work orders readers** | Planner, running starts, groups, offers, AskWork and inventory use entry/unit/occupation separately. | Legacy behaviour unchanged. | Fixture per census site; two unrelated sessions reuse a location without joining; a busy unit cannot gain a second writer; existing parallel-part behaviour preserved. |
| **P4b — Workflows readers** | Chat runs, taken quests, graph-run branches and entry landing deduplication. | Legacy behaviour unchanged. | Same unit across locations remains continuous; two units in one location stay distinct; earlier independent parts are not omitted from Accept. Existing workflow and entry-landing suites whole. |
| **P4c — Review, trace and location actions** | Candidate reads by exact output; served-build holds; trace projection; terminal/folder resolution; no stale path fallback. | Correct historical review; safer location actions. | Review A after B occupies A’s old slot; served build cannot change underneath its tab; terminal press racing allocation; both trace identities; existing Review graph/oracle parity retained. |
| **P5 — continuation and admission** | Course location facts; original-directory preference; `workplace-moved` briefed move; pool count and holder rules; parked/helper/setup retention. | Queued work names equipment contention; a directory-changing continuation says it used a brief. | Unit table for every holder; Process cases for resume, carry-on, queued words, further part, hand-over, limit move, D79 and brought-forward take; native stand-in rejects resume from the wrong directory; move-word crash cases preserved. |
| **P6 — preparation and cache catalogue** | Lazy creation; safe preservation journal; non-forcing reset/switch; shared folder catalogue; npm validation and instructions. | Retained setup is stated accurately. | Git fixture with tracked edits, untracked and ignored material, changed lockfiles, target collisions, hidden index flags, nested repo and links. Windows Process cases for held files, long paths and junctions. Kill at every preservation/preparation boundary. |
| **P7 — end and retention** | Both end outcomes; immutable end refs; explicit-membership records/outbox; detach/free; pooled branch tidy; new disk kinds. No equipment removal yet. | “Done with output”, “Done without output”, or a named preservation hold. | The brief §8 two-session reuse fixture; no-work-loss adversaries; every ref race verified transactionally; failed record post; crash before/after detach and release; old tidy cannot consume kept payloads. |
| **P8 — complete UI and repository opt-in** | Remove remaining pooled path assumptions; bilingual notes, waits, attention and setting doors; enable only opted-in repositories. | Faster repeated starts; equipment queue and recoverable output remain understandable. | Entire reader census complete; en/zh tests and realistic-width stories; installed-window proof; full brief §9 suites, applicable gates, git Process half, family and deployment rehearsals. |
| **P9 — lowering, departure and install transition** | Draining/retirement plan, repository departure, legacy-tree inventory and owner-reviewed migration operation. | Target count, retained surplus and exact proposed removals. | Dirty/busy/parked/served surplus stays; lowering interrupted at each phase; removal never follows links; old manifests remain readable. Parent performs approved install transition and measurements. |

FOUNDATION1’s move and delivery protocol is reused, including claim fencing, keyed opens, uncertain words and head checks. Where a required FOUNDATION1 Part B seam is not yet built, its dependent pool row waits; it does not invent an interim second protocol.

P6 appends the folder-policy amendments to TREEKEEP1, TREEDEPS1 and KEEP1a. P5 appends the continuation/equipment amendments. P7 appends D88/D195’s record and lifetime amendments. These are dated notes, preserving historical decisions.

At P8, run the **whole suites named by the brief**, not representative cases: the tree opening/dependency/return/lock suites, start-close and lock ticks, planner, tidy/folder/cleanup/keeping suites, keep-size git stops, space, entry/auto-landing, service work-record/sync, web identity/note suites, and real-start tick suites. `daoris.gates.json` and `docs/development.md` supply the execution lanes and serial rehearsal rules.

#### Install measurement

The parent records, before and after:

- Total bytes split into live legacy trees, set-aside folders, pool slots, retained caches and kept payloads.
- Materialised slots, held slots and waiting time.
- Start request to first harness turn, with preparation and dependency work separated.
- Native resumes versus briefed moves caused by directory contention.
- At least one comparable day’s workload, including completed output and no-output work.

A result must show whether savings came from reuse or merely from deleting legacy folders. The proposal claims neither measured speedup nor a byte reduction before that comparison.

### 6. What is the owner’s to decide

These are decisions for the owner, not actions performed by this proposal.

1. **Accept the D189 amendment?**  
   A yes allows a released line whose original directory is busy to continue in another free slot with Daoris’s brief. It trades some native-conversation continuity for equipment reuse. Review question 4 and P5’s native/briefed Process cases.

2. **Accept the pooled downgrade boundary?**  
   A yes accepts that older builds retain their legacy work but cannot show or continue pooled local lines. New records remain intact for return to a pool-aware build. Review question 14, the stored-record table and P3’s cross-version results.

3. **Accept equipment admission for person-held conversations?**  
   A yes keeps them outside CAPCOUNT1 while requiring a free repository slot. Review question 8, all-held queue examples and P5’s admission table.

4. **Opt the install’s repository into the pool, at what count?**  
   A yes enables new pooled occupations after P8’s proofs. The suggested initial count is the machine cap at opt-in. Review the complete census, rehearsal results and baseline measurements.

5. **Approve each concrete legacy-tree or set-aside removal plan?**  
   A yes authorises only the listed folders/branches and specified effects, after inspection of their holds, refs, records and kept content. Review P9’s generated plan. Existing installed trees are neither reset nor adopted by the design’s development work.

6. **Approve any disposal of kept payloads or otherwise unheld work?**  
   A yes releases the specifically named data. TREEPOOL1 supplies no automatic age policy for such payloads and no implicit permission to destroy them.

7. **Publishing, pushing, history rewriting, sign-in or account expenditure beyond development runs?**  
   Each remains the owner’s separate decision if later needed. None is required to specify or build the local pool itself.

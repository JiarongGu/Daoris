# Daoris's git, as comprehensive as a git management app, and Review fast: the design (GITAPP1, 2026-10-23)

**Status: proposed (D202), round 5, in two parts.** Round 5, by Claude Opus at `49948558a` with round 4 and Codex's read of it
merged, answers that read (`docs/2026-10-23-git-app-reads.md`, "Codex, round 4": five findings, all against part one) in §19, each
at its file and line; part one below is revised in place where they land (P1.1-P1.8), and part two is untouched. Round 4, by Claude
Opus at `8fbeef346` with round 3 and Codex's read of it merged, answered that read (`docs/2026-10-23-git-app-reads.md`, "Codex,
round 3") in §18. The steward narrowed again so Review gets
faster now. **Part one** (below, before §0) is all D202 settles: **GITAPP1a**, an uncached parallel dependency graph whose answers are
exactly today's, every start counted through one seam under the existing gate, cancellation on the page's close reaching the
process's owner, and a test holding the graph's answer equal to today's chain on the same fixtures. **Part two** is everything that
keeps or reuses an answer across opens: round 3's text, §0 to §17, follows as round 3 wrote it, as part two's starting text,
proposed and read later; its open questions are listed in part two's head (P2). Where part one and round 3's text differ, part one
holds.

## Part one (rounds 4 and 5): GITAPP1a, Review as a graph of today's reads

### P1.1 What changes, and what does not

Review's route today asks its reads one after another (`Modules/Bridge/Routes/TreesRoutes.cs:56-100`): the uncommitted diff
(`WorkingTree.UncommittedDiffAsync`, `WorkingTree.cs:326-370`), the range (`ReviewRanges.ReviewRangeAsync`, `ReviewRanges.cs:35-82`),
the committed diff over `range.Reads`, the standing landing's review (`LandedReviews.cs:33-101`), the branch's name
(`WorkingTree.BranchAsync`, `:84-91`), then the Accept offer; a landed or gone session reads `LandedReviews` and `LandedProofs`
instead. Measured, `12 + I + M + Uᵣ` serial starts before the offer (§1).

**GITAPP1a runs the same reads as a dependency graph** (`ReviewGraph`, in `Driver/Trees/GitReads/`): each git start a node naming the
nodes whose answers it needs, started the moment they have answered. **Nothing changes but when each start is made:**

- **The same commands, the same arguments.** Each node runs the command today's chain runs for it, through today's helpers
  (`RangeAsync`, `UntrackedPatchAsync`, `DefaultBranchAsync`'s three questions), with today's flags: REVIEWLOCK1's and nothing new.
  No `--no-textconv`, no merge-driver refusal, no `--attr-source`, no `--no-lazy-fetch` (Codex R3-9): those change an answer and are
  part two's.
- **The same choices, by today's code.** The selection is today's functions called on the nodes' answers: `ReviewBase.Candidates`,
  `Newest` and `Incomparable`; `RecordedBaseAsync`'s order (the record's base, else the grown base for the branch `rev-parse
  --abbrev-ref HEAD` names); `CanonicalLine`'s choice, and git's guess in `DefaultBranchAsync`'s order; `known` for the counts; the
  untracked budget's fold; `LandedFromAsync` and `ProveAsync`'s orders. Each is lifted out of its chain into a pure function the chain
  and the graph both call, so the two cannot drift.
- **The same answer.** `SESSION_DIFF`'s answer is today's, field by field: the files, their statuses, counts and patches, the
  truncation; `Base` the range's `From`; `Behind` (the line, commits, ahead); the branch; the uncommitted part with its untracked
  files; the standing landing's review; a gone branch's proof; the offer. **The committed diff reads `range.Reads`, `Against ?? From`,
  and answers with `Base = From`** (Codex R3-1: REVIEWBASE3's merged comparison tree kept, as `TreesRoutes.cs:80` does today).
- **Nothing kept.** No refs observation, no cache, no context id, no watch: every open runs its graph whole, and an answer lives only
  as long as the open that asked it. The page's own keeping (`Web/src/bridge/sessions/trees.ts`: the read joined per session, the
  answer kept while fresh) is unchanged.
- **The existing gate.** Every start is `WorkingTree.GitStart` through `ProcessStarts` as `StartClass.Background`, unchanged. The graph
  has no run bound of its own beyond its width and the untracked window (P1.3).

**Reads that can run a program keep today's order, one at a time (round 5, Codex R4-1).** Part one keeps every configured program
git runs today, since suppressing one changes an answer (part two's effect policy). So it parts the nodes in two:

- **Π, the program-capable reads**: every read of file content through git's diff or merge machinery. On Tools' git 2.53, probed
  with a textconv and a clean filter that log each call: **S** (`status`) ran the clean filter; **W** (the diff against `HEAD`,
  `WorkingTree.cs:331` through `RangeAsync`'s arguments at `:466-480`) ran the clean filter and textconv on both sides; each **Uᵢ**
  (`diff --no-index`, `:385-392`) ran the clean filter and textconv; **D** (the committed range, `:304` through `:466-480`) ran
  textconv on both blobs. **M** (`merge-tree --write-tree`, `ReviewRanges.cs:92`) runs a configured merge driver. **The offer's chain**
  (`TreeWorkReads.cs:37-61`) reads `status` (`:77`), `git cherry` and `log --cherry-mark` (`LineHolds.cs:98,231,286`) and `merge-tree`
  (`LineHolds.cs:142`); **the landed review's changes** (`LandedReviews.cs:33`, past `:55`) a diff. `--no-ext-diff`
  (`WorkingTree.cs:388,479`) already keeps a configured external diff and a `diff.<driver>.command` from running, today and in the
  graph alike; textconv, the clean and process filters and merge drivers are not kept from running, and `core.fsmonitor`'s hook
  runs on a read of the index.
- **The rest run no configured program**: T, P, K, E, K′, G₁₋₃, B₁, B₂, N↑, N↓, I and H (`rev-parse`, `merge-base`, `rev-list
  --count`, `symbolic-ref`) read refs and the commit graph only; the same probe logged no call from any of them.

**Identical commands are not independent reads.** A configured program is a process of its own with state git does not see: a file
it appends to, a counter, a cache, a lock, a server it asks. Its output may depend on what it was asked before and how often, and git
promises nothing about either; so W's and D's conversions swapped, or run at once, can change a patch with no ref and no file moved,
and a speculative read that is then unused runs a program today's chain never ran on that path. Equality of command and arguments
says the read is the same question; it does not say asking it earlier, beside another, or needlessly leaves every later answer the
same. **So in part one Π is one chain in today's order, one read at a time, and nothing in Π is speculative**: W → S → U₁ … Uᵣ → M →
D → the landed review's changes → the offer, each started only where today's chain would start it (`TreesRoutes.cs:70`, then `:77`'s
range, then `:80`, then `:100-104` or `:91-94`), after the one before it in Π has answered and after its own inputs. Round 4's D₀
(the committed range started before Σ) is **withdrawn**, and the offer no longer starts beside D. The parallel set is the rest:
plain ref, ancestry and count questions, which run at once and ahead. A Π read and a non-Π read may run together, since the non-Π
read runs no program to interact with. Part two may widen Π's parallelism once its context proves no program applies to a read's
paths (P2).

**Speculation, and its one rule.** A node **outside Π** may start before the condition that decides whether its answer is used,
**only where the command it runs is the one today's chain would run if the condition went that way**, with the same arguments: so a
used speculative answer is today's answer, and an unused one is a wasted start, counted and named. A node never starts a command
today's chain would never run on any path, and a speculative node's failure is surfaced only where today's chain would have asked it
(H's refusal at `TreesRoutes.cs:88,113`, P1.7's branch list). `rev-parse --show-toplevel` of the tree, asked twice today (once by
each diff reader, `WorkingTree.cs:302,328`), is asked once and answers both.

**What it does not fix.** Today's chain names `HEAD` in each command, so a tree moving during the read can mix two heads, today and in
the graph alike; one head id and a final barrier are part two's (§5.2), since they need the refs observation. A graph that answers
faster narrows that window and does not close it.

### P1.2 The nodes

`HEAD` is the name, as today. "Spec." marks a node started speculatively under P1.1's rule; **Π** marks a program-capable read
(P1.1), never speculative, started in Π's order after the Π read before it.

| Node | Command (today's) | Today at | Needs | Spec. |
|---|---|---|---|---|
| **T** | `rev-parse --show-toplevel` in the tree | `WorkingTree.cs:850-865`, asked by both diff readers | nothing | |
| **P** | `rev-parse --path-format=absolute --git-common-dir` | `ReviewRanges.cs:42` | nothing | |
| **K** | `merge-base --is-ancestor <recorded> HEAD`, where the record is an id | `ReviewRanges.cs:104` | nothing | |
| **E** | `rev-parse --abbrev-ref HEAD` (the branch the grown base is looked up by) | `:106` | nothing; used where K says no | yes |
| **K′** | `merge-base --is-ancestor <grown> HEAD`, the grown base for E's branch | `:107-108` | E | yes, used where K says no |
| **G₁ G₂ G₃** | git's guess at an unset line, in the checkout P names: `symbolic-ref --short refs/remotes/origin/HEAD`; `rev-parse --verify --quiet refs/heads/main^{commit}`; the same of `master` | `WorkingTree.cs:105-124` | P; only where no line is set | G₂, G₃ beside G₁, chosen in today's order |
| **B₁ B₂** | `merge-base HEAD refs/heads/<line>`, `… refs/remotes/origin/<line>` | `ReviewRanges.cs:53-60` | the line (set: no start; guessed: G), and P answered | where the line is set, beside P |
| **N↑ N↓** | `rev-list --count <known…> --not HEAD` and its converse | `:125-126` | B₁, B₂ (`known`) | yes, beside B on both refs; used where both B answered, else run again on `known` |
| **I** | `merge-base --independent <candidates>` | `:114` | B₁, B₂, K (K′) where two or more distinct candidates | |
| **M** | `merge-tree --write-tree <line base> <record base>`, **as today** | `:92` | I, where incomparable; Π after S and the last U | **Π** |
| ~~D₀~~ | withdrawn in round 5 (Codex R4-1): a speculative committed range runs textconv today's chain may never run | | | |
| **D** | the committed range `<Against ?? From>..HEAD`, `Base = From` | `TreesRoutes.cs:77-80`; `WorkingTree.cs:298-305` | Σ, T saying top (`WorkingTree.cs:302`), a base that is not blank (`TreesRoutes.cs:72`, `WorkingTree.cs:300`); Π after M, else after the last U, else after S | **Π** |
| **W** | the tracked uncommitted diff against `HEAD` (`RangeAsync("HEAD", "HEAD")`) | `WorkingTree.cs:331` | T saying top (`:328`), an own tree that is here (`TreesRoutes.cs:70`); first in Π | **Π** |
| **S** | `--no-optional-locks status --porcelain -z --untracked-files=all` | `:334-337` | W answered (`:332`) | **Π** |
| **Uᵢ** | an untracked file's `diff --no-index /dev/null <path>` (`UntrackedPatchAsync`) | `:373-401` | S answered (`:337`); W's spent budget; the window and the fold, P1.3 | **Π** |
| **H** | `symbolic-ref --short -q HEAD` (`BranchAsync`) | `TreesRoutes.cs:88,113`; `WorkingTree.cs:84-91` | nothing; used where D answered (`:88`) or on the dirty-only path (`:113`), its failure thrown only there, as today | yes |
| **Σ** | today's selection (P1.1) on the answers | `ReviewRanges.cs:63-81` | B, K, K′, I, M where they ran | no process |
| **Offer** | today's offer chain (`TreeWorkReads`, `TreeFollows`, the content holds), its own order unchanged | `TreesRoutes.cs:91-94` | D answered and H answered (`:81,88`), Σ and N (ahead > 0), no landing, an own tree; Π last | **Π**, never speculative |
| **Landed, gone** | `LandedReviews`' and `LandedProofs`' starts, each a node with its own needs (T of the checkout, the tip's `rev-parse`, `--is-ancestor`s, the recorded `From`'s expansion, the line's guess there, `merge-base`, the range; `cat-file -e`, each line form's `rev-parse`, `--is-ancestor`, `merge-base`, `rev-list --count`, the names) | `LandedReviews.cs:33-101`, `LandedProofs.cs:54-187` | as today's code reads each from another; the landed path only where D did not answer or the base is blank (`TreesRoutes.cs:72,81,100`) | where P1.1 allows; the range read **Π** |

**The line's guess stays git's own** (Codex R3-2): `symbolic-ref` reads a dangling `refs/remotes/origin/HEAD` that `for-each-ref`
omits (`.claude/knowledge/git-ref-absence.md`), so round 3's derivation from the refs observation is withdrawn. Its three questions are
asked together and chosen in today's order; where a line is set in the person's choices none is asked, as today
(`CanonicalLine.cs:83-90`). A look (`TreeContext.OneLook`) still shares the guess within its flow.

**The offer stays in the answer.** Moving it out of `SESSION_DIFF` (round 3's 1a-v) changes the route's answer, so it is part two's
(P2). In part one its chain starts where today's does, **after D and H have answered** (round 5: it runs `status`, `cherry`,
`log --cherry-mark` and `merge-tree`, so it is Π and is never started beside D or ahead of D's success), and its starts are counted
in a budget of their own: the bench reports the review's depth and the offer's apart, since the offer's own serial chain (six
starts, then one per eligible branch, §1) can be the longer.

### P1.3 Untracked files: today's reads, in a window

Each untracked file stays one start of today's command (Codex R3-10): the display addition (§6.2) is part two's. Today they run one
after another, each read only while the patch budget is unspent (`WorkingTree.cs:340-364`: `spent` starts at W's patches, `:341`;
a file past the budget is listed with no read, `:353-357`; each read adds its patch, `:360-361`). **Round 5 (Codex R4-2) bounds the
window by the fold, not by running reads:**

- **Opened only after W and S have answered.** W's success and its spent budget (`:331-332,341`) and S's success (`:334-337`) are
  the window's prerequisites; W or S failing opens no window (the uncommitted answer is null, `:332,337`), and W's patches spending
  the whole budget starts **no** untracked read, as today.
- **Bounded by files started and not yet folded.** At most w files are started ahead of the fold; a slot is given back only when
  the ordered fold advances past a file, never when a later file's read completes first. A held first answer therefore holds the
  window at w, whatever completes behind it.
- **Stopped by the fold.** The fold takes each answer in `status`'s order by today's rule (`PatchBudget` over W's patches and each
  read patch, a file past it listed with no patch and counted in the truncation's words, `:366-368`); once it reaches the budget no
  further file is started, and each file started ahead of it is stopped and counted wasted. A cancelled read stops the window.
- **So the wasted starts are at most w − 1**, the files started ahead of the one whose patch reached the budget, and **zero where W
  spent it**. The size check before a read (`UntrackedPatchLimit`, `:377`) is today's and costs no start.

**Part one's w is 1** (P1.1: each Uᵢ runs the clean filter and textconv, so it is Π, read in today's order one at a time). With w = 1
the protocol is today's loop exactly: the files read are today's `Uᵣ` and **no untracked start is wasted**. The protocol is written
for any w so the census can hold it now; **w > 1 is part two's**, once its context shows no textconv or filter applies to the
untracked paths (P2). So part one does not shorten the untracked tail: the 50-untracked case (§1's 44.1 s) gains only the parallel
ref reads and the shared T, and the bench says so.

### P1.4 The census: depth and starts generated from the graph

**The counts are generated, not written** (Codex R3-3). `ReviewGraph` is built from a review's conditions: own tree, landed or gone;
the line set, guessed (origin's `HEAD` set, dangling, `main`, `master`, none) or absent; the recorded base an ancestor, displaced with a
grown base that is or is not, displaced with none, or not an id; the candidates one, two comparable, or incomparable (M, or M's
conflict); untracked files none, under the budget, past it; T saying top or not; the offer asked or not; **and, from round 5, every
route branch P1.7 lists** (a tree outside this home, a blank base, W, S, D or H failing, the landed and dirty-only fallbacks, the
refusal) **and the untracked window's held cases** (W held, W spending the whole budget, the first Uᵢ held while later ones could
complete, the fold reaching the budget mid-window; Codex R4-2). The census enumerates every
combination, builds the graph, runs it through the executor with held completions (a stand-in under the seam holds every answer until
the test releases it), and holds each path's **depth** (the longest chain of dependent starts) and **starts** (used, speculative,
wasted) to what the graph's data computes. A node started before its inputs answered, or not started once they had, fails; so does
any command the graph runs that today's chain would not run on that path, outside the speculative set P1.1 allows.

The table below is **provisional**: it is what part one expects the generator to give for the named paths, and **where the generated
number differs, the generated one stands** and the row corrects this table. The offer and the untracked tail are beside it.

| Path, cold (nothing kept, ever); round 5's Π chain | Today: serial starts | Graph: depth | Graph: starts |
|---|---|---|---|
| Ordinary: own tree; the line set; both its refs here; the record an ancestor; one distinct candidate | 12 | 4 (T → W → S → D) | 12: T, P, K, E (wasted), H, B₁, B₂, N×2, W, S, D |
| The same, the line guessed from origin's `HEAD` | 13 | 4 (T → W → S → D; P → G → B beside it) | 15: and G₁₋₃, G₂ and G₃ wasted |
| Two comparable candidates | 13 | 4 (T → W → S → D; P → B → I beside it) | 13: and I |
| Incomparable (REVIEWBASE3), M answering | 14 | 5 (T → W → S → M → D) | 14: and I, M |
| The record displaced, a grown base an ancestor, incomparable | 16 | 5 (T → W → S → M → D; E → K′ → I beside it) | 15: and K′, I, M |
| **The general ceiling**: the line guessed by `master`, the record displaced and grown, incomparable | 19 | 6 (P → G → B → I → M → D) | 18 |
| A blank base, dirty work only (`TreesRoutes.cs:72,109-113`) | 4: T, W, S, H | 3 (T → W → S) | 4: H speculative and used |
| A tree outside this home (`ReviewRanges.cs:37`; no W, no S, `TreesRoutes.cs:70`) | 3: T, D, H | 2 (T → D) | 3 |
| The untracked tail, Uᵣ files read | + Uᵣ, serial | + Uᵣ, in Π after S (w = 1) | + Uᵣ, none wasted |
| The offer, where asked | + 6 + e, serial | + its chain's own, in Π after D and H | + 6 + e |

Landed and gone paths are generated alike, from `LandedReviews`' and `LandedProofs`' own needs, with no table written ahead of the
generator. The **ceilings stay provisional** (Codex R3-3), and none of part one's numbers is a time: the bench reports times beside them.

### P1.5 One counting seam under the existing gate

**`Launch(kind, arguments, budget)`** beneath `RunGitAsync`, `ReadGitAsync` and `GitBytesAsync` (`WorkingTree.cs:1025,1104,1198`):
every git the driver starts passes through it, and `WorkingTree.Started` is raised there for all three runners (today only
`RunGitAsync` raises it, `:1031`). It **classifies and counts; it adds no flag**: each kind's arguments are asserted equal to what
today's call sites pass, REVIEWLOCK1's flags among them, and a census refuses a git started under `Trees/` outside the seam. Each
start carries its node, its budget (the review, the landed or gone review, the offer, the untracked tail) and whether it was used,
speculative or wasted, and the gate's wait, the creation and the run apart (§1.2). It passes on to `WorkingTree.GitStart` and
`ProcessStarts` exactly as today.

The terminal door, `daoris-driver git review <session> [--json] [--time]` (D50), reads through the same route code: `--time` prints
each node's command, budget, gate wait, creation and run, the depth reached, and writes `review.read` (`session`, `ms`, `depth`,
`starts` per budget, `wasted`, `waitedMs`, `createdMs`, `files`, `untracked`, no path). It is built on today's chain first, so the
before-bench and the after-bench are one verb.

### P1.6 Cancellation from the page's close to the process's owner (Codex R3-8)

**Today the page cannot stop a read it sent**: the bridge's `call` carries a payload and a timeout (`Web/src/bridge/call.ts:10-14`),
and Review's reader says so (`trees.ts:32-35,72-76`): leaving cancels the page's wait, while git reads on, and its answer is kept for
a return. The host side already ends git when a route's token is cancelled (`WorkingTree.cs:1063-1070`: `ChildProcess` kills git's
tree on the caller's cancellation; `:1165-1166` for a streamed read), and `RouteFlights` already joins callers, a leaving caller
dropping only itself and the last one stopping the flight (`Modules/Bridge/RouteFlights.cs:15-17,32-55`). What is missing is the
door between them:

- **A read id on the call.** `SESSION_DIFF` is asked with a `read` id the page makes per call. The route holds a cancellation source
  per live `read` id, linked into the token it runs under, and drops it when the route answers.
- **Joined by session.** `SESSION_DIFF` joins through `RouteFlights` on the route and the session (the `read` id kept out of the
  flight's key), so two windows on one review start one graph, and each window's leaving is its own.
- **`READ_LEAVE { read }`**, a route that cancels that id's source: its caller stops waiting, and the flight stops only where no other
  caller waits. A leave for an id already answered or unknown is answered `{ left: false }` and does nothing.
- **The graph's executor runs under the flight's token**: cancelled, it starts no further node, every running node's runner kills
  its process and waits for its exit (below: **added by row 1a-iii, not today's**), and nothing of a cancelled read is answered or
  kept.
- **The page sends it when its last waiter leaves.** `readDiff`'s entry counts the page's waiters; when the last leaves (the review
  unmounted, the session page left), it waits a **grace of 10 s** (provisional: REVIEW4's return to a read still running joins it),
  then sends `READ_LEAVE` and drops the entry, so a return after the grace asks afresh. A read answered within the grace is kept as
  REVIEW4 keeps it today. This is the door for **navigation inside a living page** only.

**Bound to the window that asked, never to the host's shutdown (round 5, Codex R4-4).** Round 4's last bullet, that a window's exit
ends the host's routes as shutdown does, is **withdrawn**: the application outlives a main window while another is open
(`Modules/Windows/MainWindows.cs:4-12,88-99`), the first main window is hidden rather than closed when the person closes it with
another open (`MainWindows.cs:64-82`; `Daoris.Desktop.App/MainForm.cs:204-211`), and a secondary window's close only forgets that
window (`Daoris.Desktop.App/SecondaryWindowHost.cs:154-166`). What binds a read to its window is the token the route is already
handed, read in the window kit at the version Daoris pins (`Shenora` 0.22.0, `src/Daoris.Desktop/Directory.Packages.props:26-31`;
paths in the kit's repository at its tag `v0.22.0`):

- each window's page has a bridge of its own: `ChromiumView` makes its browser (`src/Shenora.Windows/WebView/ChromiumView.cs`), whose
  `ChromiumIpcBridge` makes an `IpcHostBridge` (`src/Shenora.Chromium/Host/ChromiumIpcBridge.cs:72-80`, `CancelInFlightOnDispose`
  left at its default, true, `src/Shenora/Core/Ipc/IpcHostBridge.cs:42`);
- that bridge hands **its own lifetime token** to every dispatch it makes (`IpcHostBridge.cs:71-74,133`: "the CALLER's lifetime, not
  per-request client cancellation"), passed unchanged through `PageModuleDispatcher`
  (`src/Shenora.Chromium/Host/PageModuleDispatcher.cs:26-28`) and `ModuleBase.HandleMessageAsync`
  (`src/Shenora/Core/Ipc/ModuleBase.cs:39,58`) to `DriverModule.RouteMessageAsync` (`Modules/Bridge/DriverModule.cs:126-131`) and
  the route, or to `RouteFlights.JoinAsync` for a joined read (`DriverModule.cs:172-175`);
- the window's destruction cancels it, with no page script: the view's handle destroyed (`ChromiumView.cs:139-143`) disposes the
  browser (`src/Shenora.Chromium/ChromiumChildBrowser.cs:165-171`), whose close (`:297-301`) finishes it (`:232-235`), disposing the
  bridge (`ChromiumIpcBridge.cs:166-173`), which cancels the lifetime (`IpcHostBridge.cs:170-183`).

So in row 1a-iii **each `read` id's source is linked to the route's own token** (the asking window's bridge lifetime), and the read's
`RouteFlights` join waits under that linked token. **A window destroyed** cancels its bridge's lifetime, which leaves every read that
window asked (`RouteFlights.cs:47-54`, then `Leave`, `:93-103`) with no JavaScript; the flight stops only where that window was the
last waiter, so **another window still waiting keeps the read**. **The first main window hidden** destroys no handle: its page, its
bridge and its reads live on, and its page's own leave door is the only one that applies, as for any living page. **The host's
shutdown** stays what it is, and part one relies on it for nothing. This is a reading of the kit: row 1a-iii's proof demonstrates it
(P1.7) before anything relies on it, and a kit that stopped cancelling on a window's destruction fails that proof.

**The runners today, and the row that makes them kill and wait (round 5, Codex R4-5).** Round 4 said every process is "killed and
waited for by the runner, as today"; **that is not today's code**:

- `ChildProcess.RunAsync` (`Driver/Platform/ChildProcess.cs:54-92`, under `RunGitAsync`, `Driver/Trees/WorkingTree.cs:1069-1070`):
  on cancellation it calls `KillTree` (`:82`), then awaits the two output reads, which were started under the cancelled token
  (`:73-74,85`), and throws (`:86`); it **never awaits the process's exit** after the kill, and disposes it (`:68`).
- `ReadGitAsync` and `GitBytesAsync` (`WorkingTree.cs:1198-1248`, `:1104-1159`): the token's registration kills git's tree
  (`Ending`, `:1165-1166`, registered at `:1213` and `:1120`); a cancelled read lands in `EndAsync` (`:1236`, `:1147`), which kills
  again and lets stderr's read finish (`:1172-1176`); **no exit wait**, and the process is disposed in `finally` (`:1246`, `:1157`).
- `KillTree` (`ChildProcess.cs:112-122`) **swallows a failed termination** (`Win32Exception`, `InvalidOperationException`,
  `NotSupportedException`, `:118`) as "it ended as it was stopped", so a kill that failed reads as one that worked.
- **Cancellation during creation** is seen through (`Driver/Platform/ProcessStarts.cs:114-116,136-147`): a start under way returns its
  process, whose runner's registration on the already-cancelled token kills it at once (`WorkingTree.cs:1213`, `:1120`); in
  `ChildProcess` the first wait throws and the kill follows (`ChildProcess.cs:78-82`). Again no exit wait.
- **`RouteFlights.JoinAsync` releases a cancelled caller before the flight's cleanup**: the caller's wait throws
  (`RouteFlights.cs:47-54`), and the flight's stop is cancelled on another thread (`:102`) while `FlyAsync` (`:64-82`) still unwinds.
- **What today's tests observe** is that the tree was ended, by a marker a grandchild would have written after a delay
  (`Driver.Tests/Platform/ChildProcessTests.cs:62-73,113-124`; `Driver.Tests/Trees/GitStreamEndsTests.cs:39-72`); never that the
  runner returned after the process's exit.

**Row 1a-iii adds it** (driver, beside its modules and web halves; P1.8): each of the three runners, on cancellation or past its
bound, kills the tree and then **awaits the process's exit** under a bound of its own (provisional 5 s), disposing the process only
after; a kill that failed, or an exit not seen within that bound, is **said, never swallowed**: the runner logs `git.unended` (the
node, the pid, the reason, no path), throws its cancellation with that fact attached, and the executor counts the node *unended*.
**The owner is the graph's executor under the flight**: it keeps every started node until that node's runner has returned, and the
flight completes (`FlyAsync`'s `Done`, `RouteFlights.cs:68-81`) only once every node it started has returned. So **three milestones
are told apart**: a caller's wait released (at once, as today), the flight's stop (`:102`), and the owner's cleanup (every started
process exited, or said unended). **An unused speculative node** (outside Π, P1.1) is stopped as soon as the condition deciding it
goes the other way, its runner killing it and waiting for its exit like any other, and counted *wasted, stopped*; one that has
already answered is dropped. The answer is published after the owner's cleanup, so no node the answer did not use outlives it.

The process's owner is the flight's executor: one per review being read, holding the token every node's runner hears. The ownership
table of §4.4 is part two's beyond this.

### P1.7 The proof

- **Today's answer, compared** (the steward's test). A Process class (`Parallel=Git`) builds each fixture of P1.4's conditions in real
  git, among them **ReviewBaseTests' chain step that merged the line** (`ReviewBaseTests.cs:169-187`, Codex R3-1) and its conflicting
  variant, a dangling `refs/remotes/origin/HEAD` (Codex R3-2), `main` only, `master` only, neither, a displaced record with and without
  a grown base, two comparable candidates, untracked files under and past the budget and over 200 KiB, a folder that is no top, and a
  landed branch standing, not holding its `From`, not ours, and gone. On each it answers once through **today's chain** (kept as the
  oracle until part two retires it) and once through **the graph**, and asserts the two outcomes equal: **an answer** field by field,
  the patches byte for byte, **or a refusal** by its code and its words (round 5, Codex R4-3), and the multiset of commands each ran
  equal but for the speculative starts the graph names and the one `--show-toplevel` it shares. Nothing moves the tree between the
  two reads. **The comparison runs through the route** (`TreesRoutes.DiffAsync`, `Modules/Bridge/Routes/TreesRoutes.cs:56-117`)
  with only its reads swapped, so its guards and its fallbacks' precedence are the route's own and stay around the graph, and **every
  branch below has its fixture**, each named by the line that decides it:
  1. **The session unknown**: `SessionGroundAsync` refuses (`TreesRoutes.cs:62`) before any git; no node starts.
  2. **The tree gone** (`:66`; `TreeFolders.cs:13-25`: no path, no folder, an empty one, one nobody may list): no W, S or U
     (`TreesRoutes.cs:70`), no committed range (`:72`); with a landing, the landed review with its changes (`:100-104`); without one,
     the refusal (`:116`).
  3. **A tree outside this home** (`OwnTree`, `:120-`; the range's `context.Holds`, `ReviewRanges.cs:37`): no W, S or U (`TreesRoutes.cs:70`);
     the range answered as the record's base with no line and **no start** (`ReviewRanges.cs:37`), so no P, K, E, G, B, N, I or M; the
     committed review read (`TreesRoutes.cs:80`) and H (`:88`); **no offer** (`:91`, `OwnTree`).
  4. **W's T not top** (`WorkingTree.cs:328`), **W failing** (`:332`), **S failing** (`:338`): the uncommitted part null, and no S, U
     after the failure; no window opens (P1.3), so no status or untracked answer can make dirty work.
  5. **A blank base** (`TreesRoutes.cs:72`): no range, no D, no standing review, no offer; then the landing's review (`:100-104`), else
     **dirty work only** with H asked there (`:109-113`), else the refusal (`:116`). H, speculative, is used here and its refusal
     thrown here (`WorkingTree.cs:90`); on the landing and refusal paths it is wasted and its failure dropped.
  6. **The range's own branches** (`ReviewRanges.cs:35-82`): P failing, so no line and no counts (`:42-46,81`); the record an id and an
     ancestor (`:104`), not an id (no K, `:103-104`), displaced with a grown base that is (`:106-108`), is not, or is no id (`:107`);
     the line set, guessed by origin's `HEAD` (dangling too), `main`, `master`, or none (`WorkingTree.cs:105-124`); either line ref
     absent or failing (`ReviewRanges.cs:55-56`); fewer than two candidates (`:66-69`), two comparable, I failing (`:114-115`, so
     `Newest` over none, `:73`), incomparable with M answering, conflicting or failing (`:75-78,92-94`); N with no known refs
     (`:124`) or a count failing (`:127`).
  7. **D not answering**: its T not top (`WorkingTree.cs:302`) or git refusing the range (`:484`): the committed branch falls through
     (`TreesRoutes.cs:81`) to the landing's review with its changes (`:100-104`), else dirty work only (`:109-113`), else the refusal
     (`:116`); the offer, which follows D (P1.2), is not started.
  8. **D answering with a landing standing**: the standing review without its changes (`:83-85`; `LandedReviews.cs:55`), H, no offer
     (`TreesRoutes.cs:91`).
  9. **D answering, no landing**: H's refusal thrown as today (`:88`; `WorkingTree.cs:90`), a detached `HEAD` named null (`:89`); the
     offer only where ahead > 0 and the tree is its own (`TreesRoutes.cs:91`), and none where its chain answers nothing (`:92`).
  10. **Dirty work with no files** (`:109`): the refusal (`:116`), not an empty review.
  11. **The landed and gone reviews' own branches** (`LandedReviews.cs:33-101`, `LandedProofs.cs:54-187`): standing, not holding its
      `From`, not ours, and gone, as round 4 listed.
- **Program-capable reads in today's order** (round 5, Codex R4-1): a fixture whose `.gitattributes` gives its tracked, committed
  and untracked files a textconv and a clean filter that append each call to a log in the fixture and **write a count of their calls
  into their output**, and a merge driver on the REVIEWBASE3 chain step. The log and the counter are reset identically before the
  chain's read and the graph's; the two answers equal byte for byte and **the two logs equal line for line**, so the graph ran the
  same programs, as often, in the same order. A variant whose committed range would have been read speculatively under round 4
  (the record displaced) holds that no conversion runs that the chain did not run.
- **The census** (P1.4), with held completions through the seam, in the fast half; round 5 adds the untracked window's held cases:
  W held, W spending the whole budget (no Uᵢ started), the first Uᵢ held while later ones could complete (no slot given back), and
  the fold reaching the budget mid-window, each with its generated depth and waste, run at w = 1 (part one) and at w = 4 (the
  protocol part two would widen), the waste never above w − 1.
- **The seam** (P1.5): every runner raises one count; each kind's arguments equal today's; the source census.
- **Cancellation** (P1.6): through the real route boundary, a stand-in git that waits for a signal: the page's leave after its grace
  kills the stand-in and the route answers nothing; a second window still waiting keeps the read alive until it leaves too; a leave
  for an answered id does nothing; a return within the grace joins the read. A Vitest of `readDiff` holds the grace and the last waiter.
- **Bound to the window** (round 5, Codex R4-4): two of the kit's own `IpcHostBridge`s, one per window, dispatching to the real
  `DriverModule`: one window's `SESSION_DIFF` held on the stand-in git; **disposing that bridge** (what the window's destruction
  does, P1.6) cancels its wait and kills the stand-in, with no `READ_LEAVE` sent; **a shared read**, both bridges waiting, survives
  the first bridge's disposal and answers the second; a third case holds that the first main window **hidden** leaves its read
  running. The installed window shows it apart (row 1i): a secondary main window closed mid-read while the first stays open, and
  `review.read` saying the read left by its window.
- **Kill and wait, observed** (round 5, Codex R4-5): for each of the three runners (`ChildProcess.RunAsync` under `RunGitAsync`,
  `ReadGitAsync`, `GitBytesAsync`), a stand-in that records its pid and ignores its input, cancelled **during creation** (the gate
  held, the token cancelled as the start is under way) and **while running**; and the graph with **several running nodes** and an
  unused speculative one. Each asserts that when the runner (or the flight) returns, **every recorded pid has exited**
  (`Process.GetProcessById` refusing it, or its handle signalled), not only that the route's wait was cancelled; that the caller's
  release, the flight's stop and the owner's cleanup happen in that order; and, with a stand-in that cannot be ended (a kill refused
  by a stub under the runner), that the runner says `git.unended` instead of returning as if it had ended.
- `ReviewBaseTests`, `TreeUpdateTests` and `ReviewLockTests` unchanged.
- **The bench** (§10.3's three cases through the verb), before on today's chain and after on the graph, reported, never failing.

### P1.8 The rows

Each is satisfiable on its own, with no part-two mechanism (Codex R3-9, R3-10):

1. **GITAPP1a-i — the counting seam and the bench door, on today's chain** (driver; lands first and alone). P1.5: the seam beneath
   the three runners, counting and classifying, **adding no flag**; the census of today's arguments per kind; the source census;
   `daoris-driver git review --time` and `review.read` on today's chain. No answer changes. **Proof**: the counts of today's Review at
   `12 + I + M + Uᵣ` per path through the seam, the line's guesses and both `--show-toplevel`s included; the argument and source
   censuses; the before-bench.
2. **GITAPP1a-ii — Review as a graph of today's reads** (driver; after 1a-i). P1.1-P1.4: `ReviewGraph`, its executor, the pure
   selections lifted out of today's chain, the program-capable reads Π kept in today's order and never speculative, the untracked
   window bounded by the fold at w = 1, the landed and gone subgraphs; the route reading through it, its guards and fallbacks its
   own. **Proof**: P1.7's comparison on every fixture and every branch it lists, answers and refusals; the order-sensitive textconv
   fixture; the generated census with the window's held cases; the after-bench.
3. **GITAPP1a-iii — leaving Review stops its git** (driver, modules, web; after 1a-i, apart from 1a-ii). P1.6 on today's chain or the
   graph, whichever is landed: the `read` id, the join by session, `READ_LEAVE` and the page's grace; each `read` bound to the
   asking window's bridge lifetime, a window's destruction leaving its reads with no script; **in the driver, the three runners
   killing and then awaiting each process's exit, a failed kill said as `git.unended`, and the flight's executor owning every started
   node until its runner returns** (round 5). **Proof**: P1.7's cancellation, window and kill-and-wait cases through the real route
   boundary and the kit's own bridge, the page's Vitest, and the installed window's secondary-window close in row 1i.

## Part two: proposed, read later (P2)

Round 3's §0-§17 follow as round 3 wrote them, as part two's starting text. Part two is everything that keeps or reuses an answer
across opens, and what part one leaves out because it would change today's answer. It is not settled by D202's acceptance of part
one; it is revised and read under D191 after part one is accepted. **Its open questions, as today's code and the reads leave them:**

- **The refs observation and its validation** (§5.1; Codex R3-4, R1 Codex 11): large `packed-refs` past the whole-read bound, a
  same-size write within the debounce; the dirty generation apart from the debounced reconciliation.
- **The line from the observation** (§3.1): withdrawn in part one (Codex R3-2); whether part two reads the symbolic ref apart or keeps
  git's three questions.
- **The context and its key** (§5.3; Codex R3-5, R3-11, R1 Codex 12): a capture-and-validation protocol for C's separate reads; the
  binary's fingerprint and its replacement in place (`WorkingTree.cs:933-946`'s `GitInUse` already stamps it).
- **The frontend fallback** (Codex R3-6): cacheability carried to the page, below each floor.
- **Invalidation by Daoris's own inputs** (§5.4; Codex R3-7, R1 Codex 16): the line chosen, the recorded and grown bases, `SET_LINE`'s
  driver scope not reaching the diff key.
- **Warm ceilings** (§3.1's warm table; Codex R3-3): generated from the graph with M's generation and each cache's validity, never
  written; the lost-watch and changed-attribute REVIEWBASE3 case.
- **One head id and the final barrier** (§5.2), on the observation.
- **The effect policy** (§1.1): `--no-textconv` and the merge-driver refusal change today's answers, so they are part two's, with the
  context that tells when they apply.
- **Parallelism inside Π** (round 5, Codex R4-1, R4-2): part one keeps every program-capable read in today's order, one at a time,
  and the untracked window at w = 1. Running W, S, Uᵢ, M or D together, or speculatively, or the window at w > 1, needs the context
  to show that no textconv, filter, merge driver or `core.fsmonitor` hook applies to the paths a read touches, or the effect policy
  to keep them from running.
- **The untracked display addition** (§6.2) and its attributes node A.
- **The tree watch** (§6.3), the reader's run places (§4.2), the reader and its registry (§4.1), and process ownership beyond P1.6
  (§4.4).
- **The page**: the offer and HANDOFF_PLAN read after the diff (§3.2), *Read at*, the `git` scope and `GIT_CONTEXT` (§5.3), the
  15 s validation read (§3.3).
- **The open round-1 rows Codex's round-3 table lists**: R2-1, R2-2, R2-5, R1 Codex 5, 11, 12, 16 and 28, each where it lands above;
  R1 Codex 5 and 28 are met for the cold path by part one's generated census (P1.4) and seam (P1.5).
- **The later rows** (§12's 1b, 1c, 1d-i to 1d-iii, 1h, 1i) wait for part two.

The writes are GITAPP2's (D204), unchanged by round 4.

---

**Round 3** (part two's starting text, as written). **Status then: proposed (D202), round 3: Review and the reader only.** Round 3, by Claude Opus at `9900d32f7` with round 2 merged, answers
Codex's read of round 2 (`docs/2026-10-23-git-app-reads.md`, "Codex, round 2") in §17. The steward narrowed this round so Review fast
can be accepted and built first: this design and D202 settle the reads; **the person's acts and writes** (the checkout lock, the ref
and remote acts, history edits, stash, staging, the Changes tab, the switch, D198's prerequisites for a push) **moved whole into
GITAPP2** (`docs/2026-10-23-git-writes-design.md`, D204, proposed and read later), which states what round 2 said of them. The
sections below that named the acts point there. What round 2 wrote follows.

**Round 2: after Codex's read and Fable's foundation read of round 1 (D191).** Round 1 was the synthesis,
written by Claude Opus from the brief (`docs/2026-10-23-git-app-brief.md`) and the three sealed proposals
(`docs/2026-10-23-git-app-proposals.md`: Claude's, Codex's through Daoris's helper, Fable's), at `92c7b8ed5`. The two reads of it are
`docs/2026-10-23-git-app-reads.md`: Codex's (29 findings, 22 blocking; keep proposed and revise) and Fable's (B1-B3 blocking, N1-N16
not; accept after them). Round 2, by Claude Opus, checked each finding against the code at `fe4809b75` and against REVIEWLOCK1's
branch (`daoris/s-e276dfc9`, `445720582`..`f80814215`), and takes or answers each in §16. Nothing is built. `Dnn` is
`docs/decisions/Dnn.md`; `Driver/` is `src/Daoris.Desktop/Daoris.Desktop.Driver/`, `Modules/` is
`src/Daoris.Desktop/Daoris.Desktop.Modules/`, `Web/` is `src/Daoris.Web/`. "Claude", "Codex" and "Fable" below name the three
proposals, and §-numbers prefixed C, X and F point into them; "Codex n" and "Fable Bn/Nn" name the reads' findings.

## 0. The answer (round 3)

1. **Review's cost is serial process starts**, and a start on this machine costs 0.2-1.6 s. The likeliest cause, inferred from
   PROC1a's and UIFREEZE1's stacks and two probes rather than proved, is process creation slowed by a real-time scanner and load
   (§1.2). The cure is fewer starts, started together.
2. **Review is a dependency graph of id-keyed reads**, built as data in the driver and held by a test (§3): **depth 2 and at most
   10 starts cold in the ordinary case**, as §3.1 qualifies it exactly; none per untracked file, none for the tree's layout or for a
   guessed line (both read from disk and the refs observation), and none on an open where a valid watch says nothing moved. The graph
   generates every path (the grown base, the line's three sources, the landed and gone reviews, each failure), and the census counts
   each path's starts under the one launch seam in four budgets: the graph, the context read, the watch's establishment and the one
   retry (§3.1). ReviewBase's range semantics are kept whole.
3. **Targets, provisional**, under the brief's load, medians: 16.2 s → ≤ 3 s; 44.1 s → ≤ 3.2 s; 8.4 s → ≤ 4 s; opened again with
   nothing moved ≤ 0.1 s; a live session's commit shown ≤ 3 s after its ref write (§3.4). The graph's depth and start counts are the
   gate; the times are a row's acceptance, measured on the bench through the real gate.
4. **No lock and no index refresh in a tree Daoris does not own**, as REVIEWLOCK1 measured and built it: `git --no-optional-locks`
   before the command, and `-c diff.autoRefreshIndex=false` on a diff against the working tree, its stat-only rows dropped (§1.1).
   This design points to REVIEWLOCK1 and widens its census to the reader's one launch seam; it designs no second rule.
5. **One `GitReader` per common git directory** (§4), one registry per process, every launch through one counting and classifying
   seam, its runs bounded apart from the gate's creation places, one run place reserved for attended reads and the rest handed
   attended first among queued reads, walks limited per reader and per process.
6. **Freshness is an identified, validated observation** (§5): the refs are read between two stamps and a watch generation, and
   **kept only where they can be validated whole**: under the reader's ref watch, with every ref a review rests on read whole at each
   use; a repository with no valid watch keeps nothing and reads its refs again on every use (§5.1). **One head id answers both halves
   of a review**, and a **final barrier** over every returned component validates the refs, status's own `branch.oid`, the tree's
   watch generation and the context before an answer is kept or published (§5.2).
7. **The context is enumerated and watched** (§5.3): the binary and its version, the environment handed to git, every configuration
   file git names and every candidate it would read, every attribute file (`info/attributes`, the effective global and system ones),
   and the repository's shape; read by one counted context read, heard by the reader's watch with a deadline behind it, and carried to
   the page as a context revision through a `git` scope the state hub gains.
8. **Configured programs** (§1.1): no external diff or textconv on an object read, each command given only the controls it accepts;
   `merge-tree` keeps the repository's own merge semantics and is **not run where the configuration defines a merge driver**, which is
   the only way git runs one (§1.1). **Version floors are git's own release notes** (§5.3): `--attr-source` 2.41, `--no-lazy-fetch` 2.45,
   `git var`'s attribute and configuration paths 2.42, each passed as a global option an older git refuses rather than ignores.
9. **Who owns each read's git process** is one table (§4.4). **No held git process** in this design: the screens pay one start per
   question as GIT1b does; the held `cat-file` is deferred to its own design, with the protocol facts Codex found recorded (§4.3).
10. **The person's reads** (§7): what §7.1 lists, which is the capability boundary, the conflict preview among them. **The person's
    acts and writes are GITAPP2's** (D204).
11. **A session's tree is the session's**; the person reads it in full. **Nothing of Daoris's own written into `.git`** by a read:
    objects only where `merge-tree --write-tree` writes them, nothing cached on disk.
12. **Rows** (§12): REVIEWLOCK1 (landed on `main` after this tree's base), then GITAPP1a-i, the launch seam and the bench door,
    **landing alone first**; 1a-ii the reader, the observation and the context; 1a-iii Review's graph; 1a-iv untracked display
    additions; 1a-v Review's page; 1b, 1c, 1d-i to 1d-iii, 1h, 1i.
13. **Kept from round 1, as both reads keep it**: one reader per common directory, ReviewBase's semantics whole, the Accept offer
    and the plans read after the diff, session trees only their sessions own.

## 1. What this design checked, and the two facts it accounts for

Each claim a choice below rests on was read against the code at `fe4809b75`; round 2 read again every claim either read questioned.

| Claim | By | Read | Verdict |
|---|---|---|---|
| Review's `status` and `diff` pass no `--no-optional-locks` | Fable F1 | `WorkingTree.cs:330,335`, `TreeWorkReads.cs:77` on `main`; `HelperCapture.cs:104`, `HelpCheckouts.cs:104` pass it | Holds on `main`; **REVIEWLOCK1 is built** on `daoris/s-e276dfc9` (24 sites, `tools/optional-locks.mjs` in `verify`, `ReviewLockTests`), not yet merged (Fable N15) |
| A worktree `diff` honours `--no-optional-locks` | round 1 §1.1 | REVIEWLOCK1's probe on git 2.53 (its fix-log entry) | **Wrong**: it refreshes and writes the index whatever the flag; only `status`, `diff --cached` and a two-object diff honour it (Fable B1) |
| The behind/ahead counts are unions over the line here and on origin | Codex X1 | `ReviewRanges.cs:125-126` | Holds |
| Review's starts today | brief, round 1 "13-19 + U" | `WorkingTree.cs:326-358`, `ReviewRanges.cs:42-126`, `TreesRoutes.cs:70-92` | Corrected (Codex 1): `12 + I + M + Uᵣ` before the offer, I the independence read where candidates differ, M the incomparable merge, Uᵣ the untracked files actually sent to git before the budget stops; the brief's eighteen-call probe was a replay, not a route trace |
| The offer costs "at least six and up to 21 more" | round 1 §3.2 | `TreeWorkReads.cs:40`, `TreeFollows.cs:31,32,64`, `ContentHolds.cs:163,166,177` | Six first, then a comparison per eligible branch: **not bounded** by 21 (Codex 1); counted and reported, never a ceiling, until 1c |
| A page hit costs nothing | round 1 §3.1 | `Web/src/bridge/sessions/trees.ts:168-184` | A fresh live answer and an ended answer read after its last record move start nothing; a stale live answer reruns the whole chain (Codex 1) |
| GITAUTH1c's identities store is unbuilt | brief :155 | `Platform/GitIdentities.cs`, `GitIdentityCommand.cs`, `GitAuthor.cs`; D198's GITAUTH1c note | **Partly built** (Codex 2); the doors' installation is still opt-in at publish (`tools/desktop-publish.mjs:1326-1340`), so a remote act names it as a prerequisite (§8.3) |
| The ref watch hears every ref, over the repositories holding work | brief §6 point 3 | `RefWatch.cs:229-242` hears `HEAD`, `packed-refs`, heads, remotes, reftable, trees' `HEAD`; `:249-265` selects registered **main checkouts with a `.git` folder** holding work; `:291` groups by name | Narrower (Codex 3): not tags, replace refs or the stash's log; a registered linked checkout is omitted; identity is the name, not the common directory. §5.4 widens it by common-directory identity |
| `ChangeWatch.MostRoots` is 32 | §5.1 | `ChangeWatch.cs:189`, **per instance** (`:201`, `:255`) | Holds per watch (Fable N4); §5.4 says which watch holds which roots |
| The gate bounds a run | round 1 §4.2 | `ProcessStarts.cs:29-31`: "Only the start is gated, never the run" | It bounds **creation** only (Codex 6); the reader's run bound is a second scheduler, §4.2 |
| `Person` is the class for a start a person waits on | Claude C2, Fable F4.3 | `ProcessStarts.cs:10-17,215,223`: a reserved place and a queue of its own | Holds; §4.2 keeps it |
| `WorkingTree.Started` counts every git start | round 1 §10.1 | `WorkingTree.cs:953,1020`: raised in `RunGitAsync` only; `ReadGitAsync` (`:1187`) and `GitBytesAsync` (`:1093`) start apart | **No** (Codex 28); §4.1's one seam counts all |
| `blame.ignoreRevsFile` is given on the blame call | round 1 §5.2 | `GitReads.Formats.cs:72-73`: quote-path and encoding only | **Wrong** (Codex 12); §5.3 freezes it |
| The page's commit and detail answers are kept for ever | — | `Web/src/bridge/repositories/git.ts:262` (`staleTime: Infinity`) | Holds; right for an id-keyed answer only when its context is in the key (§5.3) |
| Review's page joins one read per session | — | `trees.ts:81-108`; `queries.ts:146` | Holds; the key stays the session (§5.2) |
| An ended session's own tree is read for its dirty state | — | `TreesRoutes.cs:70` | Holds (Codex 15); an ended tree is mutable and watched alike (§6.3) |
| `session-branches.json` maps branches to sessions | round 1 §3.3 | `SessionBranches.cs:19-20`: `GrownBranch` holds no session | **Wrong** (Fable N5); the session records' tree folder through `TreeFollows.BranchOfTree`, with `GrownBranch.Now` (§5.4) |
| A conversation's start takes the tree lock | — | `ChatRunner.cs:439`, `ChatGoOn.cs:442`: only for an isolated tree | Holds (Codex 20); a checkout's conversation takes none today (GITAPP2 §2) |
| The merge landing writes the checkout | — | `TreeLands.cs:162-168` ("the one place Daoris writes into a checkout it did not create") | Holds (Fable N6); GITAPP2 §2's lock covers it |
| A person's push passes the door | round 1 §8 | `GitAccessRules.cs:40-46`: Write only with a grant or `no-push` off | Needs a grant (Fable N7, Codex 26); GITAPP2 §4.3 |
| Tag push waits for GITAUTH1f | round 1 §8 | `docs/2026-10-22-git-auth-design.md` §6.1; `TASKS.md` GITAUTH1g | **GITAUTH1g**, the transport judge (Codex 26, Fable N7) |
| The terminal's writing verbs are the person's | round 1 §9 | `InstanceRoutes.cs:9-21,156-170`: `RouteClass.Person`, `PersonPresses`, `PipeCensusTests` | The existing seam (Codex 27); GITAPP2 §4.3 |
| The repository's `commit-graph` carries changed-path filters | Fable F1 | its chunk table, `BIDX BDAT`, read without a process (Fable N15) | Holds, read |
| `DAORIS_SESSION_ID` is named by 13 sources | round 1 §1 | 33 files under `src/`, tests included (Fable N15) | Count corrected; immaterial |
| `merge-tree --write-tree` writes objects only | Codex X11 | `ReviewRanges.cs:92` | Objects only, and only under a fixed attribute source (§1.1, Codex 10) |
| LANDING waits for the tree; HANDOFF_PLAN does not | Codex X11 | `Web/src/work/review/DiffPane.tsx:148-163,188` | Holds |

### 1.1 Locks and configured programs (REVIEWLOCK1, pointed to)

**The rule is REVIEWLOCK1's, as it measured and built it** (its fix-log entry and `445720582`): every `status` and `diff` the driver
asks passes **`git --no-optional-locks <command>`**, the option **before** the subcommand (round 1 wrote it after, which git refuses;
Codex 9); and a `diff` against the working tree also passes **`-c diff.autoRefreshIndex=false`**, because git 2.53 refreshes and
writes the index there whatever `--no-optional-locks` says (`builtin/diff.c`'s quiet refresh does not consult it; Fable B1). Unrefreshed,
git lists a file whose stat data alone moved as `M` with no count and no patch, and `RangeSplit` drops it. One worktree diff still
refreshes: `BranchReplays`' conflict listing, inside a replay Daoris makes in its own tree. This design adds no second rule; it
**widens REVIEWLOCK1's census** to the reader: every launch goes through one seam (§4.1) whose read kind decides the flags, a unit test
over every kind asserts them, and `tools/optional-locks.mjs` additionally refuses a `diff` whose arguments are not two ids or
`--cached` without `diff.autoRefreshIndex=false` (Fable B1). REVIEWLOCK1 lands before GITAPP1a-i.

**The read-command effect policy** (Codex 10). A read Daoris runs may still execute a program the repository configures. Each read
kind says which, and the census holds it:

| Kind | Configured programs | Rule |
|---|---|---|
| Object diffs (two-id `diff`, `log -p`, `show`) | external diff, textconv (whose cache can write `refs/notes/textconv/*`) | `--no-ext-diff --no-textconv` on each; a textconv'd file shows as git shows it without the conversion (binary, D111), said |
| `blame` | textconv | `--no-textconv`, the one control it takes (`--no-ext-diff` is a diff option `blame` does not accept) |
| `merge-tree --write-tree` (REVIEWBASE3, the conflict preview) | a custom merge driver; a clean or smudge filter where `merge.renormalize` is on | **not run where the context read (§5.3) shows any `merge.<name>.driver` defined, or `merge.renormalize` true beside any `filter.<name>.*`**; otherwise run with the repository's own merge semantics and no `--attr-source` (round 3, Codex R2-6; below) |
| Plain walks and object names (`rev-list`, `merge-base`, `log` without a patch) | none | no flag needed |
| Worktree reads (`status`, `diff <id>` against the tree) | clean filters (LFS's), the fsmonitor hook or daemon the configuration names | run as for any `git status` in that tree: they are the tree's own configuration, and Daoris turns nothing on; said in the read kind's remarks |
| Name and layout reads (`rev-parse`, `worktree list`, `for-each-ref`, `config --list`, `var -l`) | none | no flag needed, and the census never classes them as index writers (Codex 10's converse) |

**Why `merge-tree` is refused rather than suppressed** (Codex R2-6, checked at `ReviewRanges.cs:84-95` and git's own
`gitattributes.adoc:1116-1128,1162-1170`). Round 2's `--attr-source=<empty tree>` was wrong twice: it replaces only the worktree's
`.gitattributes`, so `info/attributes`, the global and system files and `merge.default` still name a driver; and it changes the answer,
since a committed `merge=union` or `merge=binary` rule decides the merged tree. Git runs a custom merge driver only where the
configuration defines it (`gitattributes.adoc:1165`: "The definition of a merge driver is done in the `.git/config` file"); an
attribute or `merge.default` naming an undefined driver falls back to the built-in three-way merge, and the built-in `text`, `binary`
and `union` run no program. So the rule is read where the drivers are defined: the context read's configuration (§5.3) lists every
`merge.*.driver` in effect, from every scope and include. Where one is defined, `merge-tree` is not started: Review reads from the
line's base, which is today's answer when git cannot merge (`ReviewRanges.cs:84-88`), and says *this repository defines a merge driver,
which Daoris does not run*; the conflict preview says *Not previewed*. Where none is, `merge-tree` runs **as today, in the asking tree,
under that tree's attributes** (`ReviewRanges.cs:92` passes no attribute source), so REVIEWBASE3 is unchanged. Its answer therefore
depends on the tree's `.gitattributes`: it is a worktree-context read (§5.3), kept only under the tree watch. The preview says what it
is: *Merges without conflict here* (此处可无冲突合并), a landing merging in the checkout under the checkout's attributes. Fixtures: a
driver that writes a marker, defined once each in the repository's configuration with a committed attribute, with `info/attributes`,
with a global attributes file, and as `merge.default`: none runs, each refused before the start; and a committed `merge=union` gives
the merged base git gives without Daoris.

### 1.2 Process creation under the scanner

Fable's probe (F3) started programs that open no repository (`hostname.exe`, `cmd.exe /c exit`) and found them as slow as git
(median 1.2 s), with eight starts together finishing in the time of one or two; PROC1a separated `Process.Start` from the whole run
(`docs/FIX-LOG.md:5226`), and UIFREEZE1 found the runtime's one lock and a slow `CreateProcess` (`:3374`). **The reading, an inference
the stacks and probes support, not a proof** (Codex 4): each start waits on something serialised per creation, a real-time scanner's
verdict being the kind that overlaps across concurrent starts and not across a chain, and load multiplies it. The proposals' probes
ran on different loads. What follows does not depend on the attribution being whole: fewer starts and fewer dependent steps are
cheaper under any cause.

**Accounted for**:

- **Start fewer.** The layout and refs come from disk and one observation, every answer named by ids is kept, and an untracked file
  costs no start (§3, §6.2).
- **Depend less.** A dependency graph, not a chain (§3). The gate's width stays four: creation is serialised under .NET's one lock
  (`ProcessStarts`' remarks), and PROC1a measured no gain past four.
- **Do not start for object questions**, where a screen asks hundreds: the held `cat-file` of §4.3. Not for Review, which asks none.
- **Measure the parts apart.** The `review.read` line and the bench carry, per start, the gate wait, the creation time and the
  child's run, never one number (Codex 4). An exclusion for git in the scanner is the person's call; Daoris changes nothing of it.
- **The targets are provisional, set under this load** (§3.4).

## 2. How the three compare, in one table

| Question | Claude | Codex | Fable | Chosen |
|---|---|---|---|---|
| Review's starts cold | ≤ 4 at once | ≤ 10, counted per path | ≤ 10, three rounds | **A held dependency graph**: ordinary depth 2, ≤ 10 starts; each path its own depth and ceiling (§3) |
| Review's targets | ≤ 1.5 / 1.5 / 2.5 s | ≤ 4 / 4.5 / 5 s median, p95 twice | ≤ 3 / 3.2 / 4 s median | **Fable's, provisional**, p90 twice; never claimed from arithmetic (§3.4) |
| Range semantics | merge-bases keyed by ids | union counts, candidate order, REVIEWBASE3 kept | `ahead-behind` for both counts | **Codex's**: kept whole |
| Untracked files | from disk, byte-equal to git | from disk, display data | from disk, byte-equal to git | **Codex's display addition**: from disk, exact for a stated subset, never a staging patch (§6.2) |
| The offer and the plans | off the critical path | off; HANDOFF_PLAN not eager | the offer over the snapshot's tips | **Off the critical path** (Claude, Codex) |
| Layout and refs | snapshot by generation | status + refs, two starts | from disk, stamped | **From disk, an identified observation**: stamps before and after, a watch generation, content where small (§5.1) |
| Optional locks | not seen | `status` "disabled optional writes" | found the defect | **REVIEWLOCK1, as built** |
| The reader | `GitRepository` per common dir | one coordinator in the host | `GitReader` per common dir | **`GitReader` per common dir** |
| A held process | `--batch-check`, 2 min idle, ≤ 8 | lazy, ≤ 4, retired idle | screens only, 30 s idle, hazard measured | **Fable's, four per process, framed and owned** (§4.3) |
| Gate class | Person | the reserved place kept | Background | **Background**, attended first inside the reader (§4.2) |
| Working-tree watch | sessions' trees and the checkout while open | attended trees | open reviews only | **Every attended mutable tree**, live or ended, and the checkout while its Changes tab is open (§6.3) |
| fsmonitor, untracked cache | never | never | a press | **Never; the command to copy** |
| `commit-graph` | a press, after measuring | never | a press | **Never; the command to copy** |
| Working-tree acts | person's checkout, behind a switch | a manual tree of the person's | an idle session's tree | **The person's checkout, behind a switch** (GITAPP2 §2) |
| Ref-only history edits | cherry-pick, revert, rebase by objects | in the manual tree | in the idle tree | **By objects, linear and unpublished, on a branch no tree holds** (GITAPP2 §3) |
| Conflict resolution | three-way view writing a chosen result | whole side or external tool | resolved on the page | **A side per hunk, or the person's editor** (Codex, D55) |
| Agents | nothing new | common reads and their own tree | nothing new | **Nothing new** |
| File history pages | continue from the last id | cache prefixes, prove a cursor | keep the id list | **Keep the id list with each commit's path** (Fable), in 1h |

The rows *Working-tree acts*, *Ref-only history edits* and *Conflict resolution* are GITAPP2's since round 3 (D204), kept here as
the comparison was made. The row *A held process* is deferred since round 3 (§4.3).

## 3. Review fast (Q1)

### 3.1 The graph

Review's reads are a **graph built as data** (`ReviewGraph`, in `Driver/Trees/GitReads/`): each node a read with its kind, the ids
or tree it is keyed by, and the nodes whose answers it needs. An executor starts each node the moment its inputs have answered and
never before, through the reader (§4). The chain changes when each question is asked, never which: ReviewBase's candidate order, the
grown base (WSR6), the replayed bases and REVIEWBASE3's merged base are kept exactly.

With **no process**:

- **The layout**, from disk: the tree's `.git` (a folder, or a file naming `gitdir:`), `<gitdir>/commondir`. A folder is a top
  exactly when it holds its own `.git` (D113 §1's walk-up hazard). Reftable, an unreadable or unknown layout is **one counted
  `rev-parse`**, and where that fails nothing is kept and the answer says so (fail closed, Codex 11).
- **The branch name** (step E): the tree's `HEAD` file.
- **The grown base** (WSR6): `context.Grown.Of(repository, branch)`, an in-memory record read with the branch name above
  (`ReviewRanges.cs:101-108` asks `rev-parse --abbrev-ref HEAD` for the name today; the `HEAD` file answers it).
- **The line** (round 3, Codex R2-1): a line set in the person's choices is read as today (`TreeContext.LineAsync`,
  `CanonicalLine.ResolveAsync`). **Git's guess** at an unset line is no longer up to three starts (`TreeContext.CheckoutLine` is
  `WorkingTree.DefaultBranchAsync`, `WorkingTree.cs:105-124`: `symbolic-ref` of `refs/remotes/origin/HEAD`, then `rev-parse --verify`
  of `main`, then `master`, each a start; `LineGuesses`' stamps keep it only for a main checkout, `LineGuesses.cs:33-48`). It is
  **derived from R's answer**, the same three questions in the same order: R's `%(symref)` of `refs/remotes/origin/HEAD` with
  `refs/remotes/origin/` taken off, as `symbolic-ref --short` and the slash rule give it; else `refs/heads/main`, else
  `refs/heads/master`, where R lists it and its object or its peeled object is a commit (`%(objecttype)`, `%(*objecttype)`), as
  `^{commit}` asks. So the line is a function of R, not a node, and costs no start; a unit test holds the derivation equal to
  `DefaultBranchAsync` over a fixture of each case.

Then the nodes, `h` being the head id the refs observation resolved for the tree's `HEAD` (its file's `ref:` read in R's answer, or
the id a detached `HEAD` holds):

| Node | Read | Kind | Needs | Starts |
|---|---|---|---|---|
| **C** | the context read (§5.3): `git version` once per binary per process, then `git var -l` and `git config --list --show-origin --show-scope -z` | names | nothing; **skipped** while the kept context validates | 0-3 |
| **R** | the refs observation (§5.1): one `for-each-ref` (`%(refname) %(objectname) %(objecttype) %(*objecttype) %(symref)`), between two stamps | names | nothing; **skipped** where a kept observation validates | 0-1 |
| **S** | `git --no-optional-locks status --porcelain=v2 --branch -z --untracked-files=all` | worktree | nothing | 1 |
| **A** | untracked files' attributes: one `check-attr --stdin -z` over all of them (§6.2) | worktree | S, where S lists any | 0-1 |
| **U** | the untracked display additions, read from disk | no process | S, A | 0 |
| **W** | `git --no-optional-locks -c diff.autoRefreshIndex=false diff … h` (the uncommitted diff, **pinned to `h`**) | worktree | R | 1 |
| **B₁, B₂** | `merge-base h <line>`, `merge-base h <origin-line>` | object | R | 0-2 |
| **K** | `merge-base --is-ancestor <recorded> h` | object | R, where the recorded base is an id | 0-1 |
| **K′** | `merge-base --is-ancestor <grown> h`, the grown base (WSR6), **speculative beside K** | object | R, where a grown base is recorded, is an id and differs from the recorded base | 0-1 |
| **N** | `rev-list --count <line-refs…> --not h` and its converse (REVIEWBASE1's unions) | object | R | 0-2 |
| **D₀** | the committed diff `<recorded>..h`, speculative (Fable N2b) | object | R, where the recorded base is an id | 0-1 |
| **I** | `merge-base --independent <candidates>` | object | B₁, B₂, K, K′, where two or more distinct candidates | 0-1 |
| **M** | `merge-tree --write-tree <line-base> <recorded-base>` (REVIEWBASE3), as today; not started where a merge driver is defined (§1.1) | worktree context | I, where incomparable | 0-1 |
| **Σ** | **selection**: the record's base (K, else K′, else none), the candidates, `from` (`ReviewBase.Newest`) and `against` (M), exactly as `ReviewRanges.ReviewRangeAsync` decides them (`ReviewRanges.cs:35-79`) | no process | B₁, B₂, K, K′, I and M where they ran | 0 |
| **D** | the committed diff `<from>..h` (REVIEW3's one-call reader, its caps unchanged) | object | Σ; **skipped where Σ selected the recorded base** and D₀ answered | 0-1 |
| **V** | **the final barrier** (§5.2): every returned component has answered (Σ, the selected D₀ or D, N, S, W, U), then the refs observation, S's `branch.oid` = `h`, the tree's watch generation and the context id are validated together | no process | everything returned | 0 |

**D₀ before Σ.** D₀ may answer before Σ decides whether it is the review's diff. It is an id-keyed read, correct for its own key
(`<recorded>..h`) whatever Σ decides, so it is kept by that key; only V's answer names it as the review's, where Σ selected it.

**The paths are generated, not listed** (round 3, Codex R2-1). `ReviewGraph` is built from the review's conditions: the
observation (kept or not), the line (set, guessed from R, none; one or both refs here), the recorded base (an ancestor, displaced, not
an id), the grown base (none, the same, differing), the candidates Σ finds (one, two comparable, incomparable), untracked files (none,
some), and the context (kept or not). The census test enumerates **every combination**, builds the graph for each, runs it through the
executor with held completions (§10.1), and holds each path's depth and starts to the ceilings below, computed from the data. A path
the table does not name is still generated and held to the general ceiling. **Four budgets, counted apart under the one launch seam
and reported with their total** (Codex R2-2): the **graph** (the table below), the **context** (C: 0 while kept; 2 per repository
context, 3 with the binary's first `git version`; one level before the graph), the **watch** (§6.3's establishment, beside the graph,
never on its path) and the **retry** (V's one re-read, at most the path's own graph again, once).

| Path, cold, the context kept | Depth | Graph starts | Notes |
|---|---|---|---|
| **Ordinary**: own tree; the recorded base an id and an ancestor of `h`; no grown base recorded apart from it; a line set or guessed from R; both line refs here; Σ finds one distinct candidate | **2** | **≤ 9**, **≤ 10** with untracked files | R ∥ S, then W, B₁, B₂, K, N×2, D₀ (and A after S); D₀ is the answer |
| The refs observation kept and valid | 1, **2 with untracked** (S → A) | ≤ 8, ≤ 9 with untracked | R skipped; A still follows S (Codex R2-2) |
| Two or more distinct candidates, comparable | 4 | ≤ 11, ≤ 12 | D₀ wasted, counted; I, Σ, then D |
| Incomparable bases (REVIEWBASE3) | 5 | ≤ 12, ≤ 13 | I, M, Σ, then D; where a merge driver is defined, M is not started and the path is the one above |
| No line (none set, none guessed) | 2 | ≤ 5, ≤ 6 | R, S, W, K, D₀ |
| The recorded base displaced, the grown base an ancestor | 3 | ≤ 11, ≤ 12 | K fails, K′ answered beside it; D₀ wasted; D after Σ |
| The same, two candidates; incomparable | 4; 5 | ≤ 12, ≤ 13; ≤ 13, ≤ 14 | I; I and M |
| The recorded base displaced, no grown base | 3, 4 with two candidates | ≤ 10, ≤ 11; ≤ 11, ≤ 12 | no M (`ReviewBase.Incomparable` needs a record) |
| The recorded base not an id | 3 | ≤ 9, ≤ 10 (K′ where a grown base is) | no K, no D₀ |
| **The general ceiling** | **5** | **≤ 14** | every node once |
| A layout git must name (reftable, unknown) | its path's + 1 | + 1 | one counted `rev-parse` before R |

**The landed and gone reviews** (`LandedReviews.cs:33-129`, `LandedProofs.cs:75-165`), through the reader in 1a-iii, with their own
nodes: X expands an abbreviated recorded `From` (`IsCommitId` takes 7-64 hex digits, `WorkingTree.cs:160-161`); L is
`--is-ancestor <entry tip> <tip>`; F is `--is-ancestor <from> <tip>`; D₀ₗ the diff `<from>..<tip>`, speculative; MBₗ `merge-base <line
ref> <tip>`, speculative; Σₗ selects as `LandedFromAsync` does. The checkout's top is the layout rule (no `IsTopLevelAsync` start);
the branch's tip and the line's forms are R's (no `rev-parse`).

| Landed path | Depth | Starts | Notes |
|---|---|---|---|
| No checkout, not a top, or the record's words not an id and a name | 0 | 0 | from disk and the record |
| Standing, its recorded `From` in the branch's history | 2 | ≤ 6 | R ∥ X, then L, F, D₀ₗ, MBₗ; D₀ₗ is the answer |
| Standing, `From` not held: from where it leaves the line | 3 | ≤ 7 | MBₗ, then D; D₀ₗ wasted |
| Not ours (L fails) | 2 | ≤ 6 | the speculative starts wasted, counted |
| **Gone** (no ref in R, or `GoneAt` recorded): WSR5's proof | 3 | ≤ 11 | R ∥ E (`cat-file -e <tip>^{commit}`); then per line form `--is-ancestor`, `merge-base` and the names `tip..form`, and one `rev-list --count`; then per form the names `base..tip`; Σ_g selects in `ProveAsync`'s order (first merged form, else the first with nothing still differing, else the closest). Today up to 12 serial starts and the line's guess |

**Warm** (Codex R2-2). Every object node is keyed by ids and the context (§5.3), so a second open reads none of them while they
stand.

| Open again | Starts | Depth |
|---|---|---|
| Landed or gone, the observation kept | 0 | 0 |
| Landed or gone, the observation not kept (§5.1) | 1 (R), then any object node whose key moved | 1 |
| Live or ended own tree, its tree watch valid (§6.3) and the observation kept | **0** | 0 |
| The tree watch not valid (never proved, released, lost, overflowed, hinted, or its index moved), the observation kept | S ∥ W, and A where S lists an untracked file: 2-3 | 1-2 |
| Neither valid | R, S, W, and A: 3-4, then object nodes whose keys moved | 2 |
| After a commit | as cold, its object nodes for the new `h` (their keys are new) | 2 |

**Round 1's three rounds are replaced** (Codex 5, 28; Fable N2, N3). Round 1 resolved the snapshot before its "round 1", ran
`merge-tree` in the same round as the `--independent` that decides it, and put the union counts after `from` though they need only
the pinned tips; its "three rounds" was a scheduling label, not the depth. Depth is now the longest chain of dependent starts on a
path, a property of the graph's data, and a test holds it.

**Handing a level to the reader** is longest first (Fable N2a): S, W and D₀ before the merge-bases and counts, so the slowest reads
are never in the gate's second wave.

### 3.2 What leaves the critical path

`SESSION_DIFF` answers the review: its patch, its range, its counts and the observation it was read at. **The Accept offer**
(`TreeWorkReads`, `TreeFollows` and the content holds: six starts, then a comparison per eligible branch, not bounded today) becomes
its own read, asked by the foot once the diff has answered, through the same reader. **HANDOFF_PLAN**, asked unconditionally today,
is asked after the diff too; `LANDING` already waits for the tree. The foot says *Reading…* while its reads run, and their starts and
time are reported apart in `review.read`, since a fast patch is not a finished plan (Codex X1). Bounding the offer's count is 1c's.

### 3.3 A live session moving

- **Nothing moved in git**: the page no longer re-reads on every record `updated` (`Web/src/bridge/sessions/trees.ts:182-184`); in
  1a-v the page keeps its 15 s freshness for the validation read; in 1b the tree watch keeps it at 0.
- **A commit or a switch in the tree**: the tree's `HEAD` and its branch's ref move; the reader's watch hears them (§5.4), drops the
  observation, and publishes the review's scope for the sessions whose tree that is. Pre-read and the page's refetch join the same
  in-flight read by its key (§4.1), so they cannot publish two answers.
- **The line moved** (a fetch, D109, a landing): every review in that repository depends on it (§5.4), not only those on the moved
  branch (Codex 16).
- **Claude's pushed slice per open session** (C2) is not taken: the scope and the id-keyed cache give the same effect without a new
  slice contract; it is reconsidered if 1b's measurement shows the refetch's round trip matters.

### 3.4 The targets, provisional

**Round 3 keeps every number provisional**, as the asker's round-3 instruction has it; the context read's first run and the watch's
establishment are reported beside them, never inside the graph's count. **Provisional acceptance targets**, held on the bench (§10.3) and on the installed window (GITAPP1i) under the brief's load, named
beside every number; median over at least ten runs with p90 beside; a ceiling fails a row's acceptance, never a gate (D54's rule for
budgets). **They are not derived**: round 1 called them the arithmetic of three rounds, and Codex 6 showed a permitted trace past five
seconds. What the graph does say is a floor: depth × one start's wait and creation, plus the longest read on the path. The bench
measures the real graph through the shared gate; where a target is missed, the row says which node and which part (wait, creation,
run), and the target is revisited with that evidence rather than the code bent to it.

| Case | Today | First open, median / p90 | Depth, starts | Opened again, nothing moved | A live session's commit |
|---|---|---|---|---|---|
| 368 files committed, nothing untracked | 16.2 s | **≤ 3 s / ≤ 6 s** | 2, ≤ 9 | watch valid: ≤ 0.1 s, 0 starts; else ≤ 1.5 s, 2-4 starts (§3.1's warm table). Landed: ≤ 0.1 s, 0 | shown ≤ 3 s after the ref write (1b) |
| 368 files, 50 untracked | 44.1 s | **≤ 3.2 s / ≤ 6.4 s** | 2, ≤ 10; 0 per untracked file | as above | as above |
| 1,771 files committed | 8.4 s | **≤ 4 s / ≤ 8 s** | 2, ≤ 9 | as above | as above |

The 1,771-file case is **a complete file list with bounded patches**, never the whole 16.6 MB patch (`WorkingTree.cs:271,274,451`;
Codex 6). The page's paint of 368 rows is measured, not assumed: 1a-v writes `review.painted`, and collapses rows past the first
forty only if the paint is over 0.5 s.

## 4. The reader (Q2)

### 4.1 One per common directory, one seam beneath every launch

**`GitReader`** (new, `Driver/Trees/GitReads/`), one per common git directory, identified by that directory's full path (case-folded
on Windows), so a linked tree shares its repository's; handed out by **`GitReaders`**, one registry per process: the window's loop
holds one and every route and judge in that process reads through it; `daoris-driver` holds one for a verb's life; a test takes its
own. It holds the layout, the refs observation (§5.1), the one shared `GitReadCache` (64 MiB, LRU, memory only), in-flight joining
and its run bound.

**One launch seam** (Codex 28). Every git the reader starts, streamed (`ReadGitAsync`), as bytes (`GitBytesAsync`) or whole
(`RunGitAsync`), goes through `GitReader.Launch(kind, arguments)`, which classifies the read (§1.1), applies its flags, raises the one
counting event (today's `WorkingTree.Started` moves beneath all three runners, so a test counts every start), and passes
`WorkingTree.GitStart` as today: Tools' git, `GitWiring.Hand`, `GitBounds`, the gate. `ProcessStartsCensusTests` names no new direct
start; a source census refuses git started under `Trees/GitReads/` except through `Launch`.

**Joining** is by the read's key, which carries its ids and its context (§5.3), never a name. A joined read has one owner job and
many waiters; a waiter's cancellation drops that waiter alone, and the job is cancelled only when its last waiter has gone (Codex 14).
A completion is kept only if the observation and context it was read under still validate (§5.1); otherwise it answers its waiters,
named by its ids, and is not kept. **A mutable read** (S, W, A, U, M, and a review's whole answer) adds to its key the tree's identity,
`h`, and the tree's **watch generation at the read's start** (round 3, Codex R2-3): a request made after a hint or a loss carries a
newer generation and never joins a read begun before it. The page keeps its own guard (`Web/src/bridge/sessions/trees.ts:81-104`: a
read a newer one replaced never lays its answer over the newer one's), unchanged.

**What the seam counts** (round 3, Codex R2-1-3): every start of a review, the landed reviews and the context read, including those
today outside Review's chain (`TreeContext.CheckoutLine`'s guesses, `IsTopLevelAsync`, `LineGuesses`), each tagged with its budget
(graph, context, watch, retry); the census fails on a start with no budget or past its budget's ceiling.

`GitFacts` and `RefSnapshot` keep their flow-scoped meaning ("asked once within this flow, dropped by this flow's writes"),
consulting the reader first for layout and ids.

**One, not per area** (Fable F4.1): Review, the landing's proof and the commit page ask the same ids. **Not Codex's one coordinator in
the host** (X2) beyond this: the reader is per repository because its observation is, and the registry is the coordinator.

### 4.2 Two schedulers, said apart

The **gate** (`ProcessStarts`) bounds **creation**: four places, three the background's, one reserved for a person's start; a place
is held from asking to `Process.Start` returning, never for the run (`ProcessStarts.cs:29-31`). It is unchanged, and the reader's
starts stay **`StartClass.Background`**, so a conversation's agent the person just asked for never queues behind git (round 1 §4.4,
kept by both reads).

The **reader's run places** bound **whole reads**: a place is held from the read's launch until its process has exited and its
output is consumed or dropped (Codex 6). Per reader:

- **Eight run places** (provisional, measured in 1a-ii): enough for the ordinary path's one level of eight.
- **One reserved, the rest attended first** (round 3, Codex R2-10, as `ProcessStarts.cs:37-42` reasons for the gate): unattended reads
  (a pre-read, a judgement's sweep) hold **at most seven**, so one place is always free of them; a read a person is waiting on (an open
  review, a screen in view) is handed a freed place **before any queued unattended read**. Round 2's "never queues behind unattended
  ones" overclaimed: priority alone cannot take back places already running, and the reserved place is what bounds the wait.
- **Long reads** (a pickaxe, a whole-history walk, a file's history over all commits) hold **at most one place per reader and two
  per process**, so a search never starves a short attended read of the same repository, and several repositories' searches never
  fill the gate's background places together (Codex 7).

Across readers, the gate's background queue stays first-come. `review.read`'s `waitedMs` per start measures whether a pressed review
waits there behind another repository's work; if it does, a later row gives attended reads a priority **inside** the background class,
never the person's reserved place (Fable F4.3). That part of Codex 7 is answered by measurement, not by changing the gate now.

### 4.3 The held object reader: deferred (round 3)

**Deferred out of D202** (round 3, Codex R2-8, the quest's "or that half deferred"). Nothing Review asks is an object question, and
no screen this design builds needs one held: the commit page is two dependent starts (`GitReads.cs:290-313`, §7.1), and the screens
pay one start per question as GIT1b does. A held `cat-file` is its own design when a screen's measured burst needs it. **Round 2's
protocol was wrong, checked against git's own `git-cat-file.adoc`** (`:143-175`, `:294-297`, `:341`, `:362`): `info <oid>` answers
a header alone, `<oid> <type> <size>`, with no body, and only `contents <oid>` is followed by `size` bytes and a newline, so reading a
body after `info` waits for bytes git never sends; `<oid> missing` has neither; a request already written cannot be withdrawn, its
answer stays in the stream to be drained or the process retired; `--batch` (the fallback round 2 named below git 2.36, whose
`--batch-command` is in `RelNotes/2.36.0.adoc:75`) takes object names, not commands, so below 2.36 the held reader is off; and
`cat-file` takes no diff options, its textconv and filters being off unless `--textconv` or `--filters` is given (`:73-79`), so it
needs no control appended. The later design starts from these and from round 2's lifetime and repack notes, which follow as round 2
wrote them.

**Round 2's lifetime and ownership.** The reader owns it: started on the first question, ended **30 s after its last** (Fable F4.2), on the
reader's context or observation changing (a replace ref, a new git), and on the registry's disposal at window exit or the verb's
end, which kills and waits for each. **At most four per process** (Fable N1: a registry is per process, so "across the machine"
could not be held); one per reader.

**The repack hazard** (Fable N1; Codex 19). Every linked tree shares the object store, so an agent's own `commit` or `fetch` in its
tree can run `gc --auto` there, and on Windows a pack a process holds mapped cannot be unlinked: the person's `git gc` prompts "Unlink of
file … failed", a detached auto gc warns and leaves the pack. So the held reader **never starts while `<commondir>/gc.pid` exists and
retires when it appears or the `objects/pack/` listing changes**; its later design measures it beside the person's `git gc` and an auto gc fired
from a linked fixture tree (`git -c gc.auto=1 commit`). If the hazard stands with those rules, the screens live without it and pay
one start a burst. Its integration into 1d's screens waits for that proof (Codex 29).

### 4.4 Who owns each git process

| Process | Owner | Started | Ended | On cancel | On window exit or verb end |
|---|---|---|---|---|---|
| A one-shot read (any node) | its job in the reader, holding a run place | at `Launch` | exit, its output consumed | the job's token, cancelled only when its last waiter goes: `Ending` kills the tree, stderr drained, waited for | the registry's token: killed and waited |
| A long read (pickaxe, walk) | the reader's long place | at `Launch` | exit, or the page closing | streamed results stop; killed and waited; nothing kept | killed and waited |
| The context read (C) and the watch's exclusion reads | the reader, as a one-shot read | at `Launch` | exit | as a one-shot read | killed and waited |
| `daoris-driver git …`'s read verbs | the verb's registry | per the rows above | the verb's end | Ctrl-C cancels its reads | as the window |

Every read is bounded as today: the runner ends git at its `GitBounds` bound whatever the caller asked (`WorkingTree.cs:1022-1059`),
and a read ended so is an unread answer, never kept. An act's process is GITAPP2's (D204), where Codex R2-15 is open.

### 4.5 In which process

The driver library, used by the modules in the window's process, by `daoris-driver` (D50) and by the tests. No git-serving process
of Daoris's own. The service's one `ls-files` for ignored paths (`IgnoredPaths`, `Daoris.Service.Core/Registry/IgnoredPaths.cs`) is
the service's and untouched; the driver's watches compute their own (§6.3, Fable N13).

## 5. Freshness, caching and invalidation (Q3)

### 5.1 The refs observation: identified, validated, never atomic

An **observation** is what the reader read of a repository's refs, with what identifies it: the common directory's identity, the
watch generation it was read under, the stamps taken before and after, and the ids it resolved. It is **an identified observation, not
an atomic view of external git** (Codex 11): git can move between any two reads, and the rules below make that seen, never hidden.

- **What is stamped** (each file's write time and size, read with no process): `packed-refs`; the common `HEAD`; each linked tree's
  `HEAD` and the **`worktrees/` folder's listing** (membership, Codex 11); each tree's `commondir` file; `logs/refs/stash` (a dropped
  older stash need not move the tip, Codex 11); and the loose files of the refs an answer rests on. **No walk of every loose ref and
  no folder-time stand-in** (round 3, Codex R2-4, replacing round 2's 256-file bound): a folder's write time moves for a name added to
  that folder, not to a nested one (`refs/heads/team/topic` moves `refs/heads/team`, not `refs/heads`), and no time at all shows a ref
  rewritten in place. The completeness a stamp census could not give comes from the watch, below.
- **What is read whole** (content validation, Codex 11), because it is small and decides the answer: each tree's `HEAD` file, and the
  loose files of the refs a review rests on (the tree's branch, the line here and on origin), 41 bytes each. `packed-refs` is hashed
  where it is at most 1 MiB; past that, its stamp and the watch generation guard it, said.
- **Kept only where it can be validated whole** (round 3, Codex R2-4: "where complete validation is unavailable, do not retain").
  An observation is retained **only while the repository is one of the reader's ref watch roots** (§5.4), that root open and its
  generation unchanged since before the read, with no loss, overflow or resume since: the watch hears every ref write under
  `refs/**`, `packed-refs`, the `HEAD` files and `worktrees/*`, nested or not, in place or not, and the stamps and whole-read contents
  are the check at each use behind it. **A repository with no valid watch** (past the cap of its roots, a root lost or never opened,
  a filesystem whose watch cannot be trusted) **keeps nothing**: its refs are read again on every use, one counted R, depth 1, and
  `review.read` says so. A ref the watch has not yet told of (its debounce is at most `ChangeWatch.Cap`, 2 s) can stand stale for that
  long in a branch list's decorations; never in a review, whose own refs are read whole at each use.
- **Order** (Fable B2): stamp and generation are taken **before** `for-each-ref` starts and again **after** it answers; the observation
  is kept only where both agree, as `LineGuesses.GuessAsync` already does (`LineGuesses.cs:33-37`). Otherwise it answers this read,
  named by its ids, and the next use reads afresh.
- **Racy time**: a stamped file whose write time is within two seconds of the stamp's own reading does not let the observation be kept
  (git's racy-index rule, applied to refs: same length, same coarse time is the case a stamp cannot tell). On a filesystem whose times
  are coarse (exFAT, a network share), nothing is kept.
- **Before every use**, the stamps, the whole-read contents and the generation are read again; any difference reads afresh.
- **Reftable** has no stamp: read on every use, one counted start, said. **An unread layout** fails closed (§3.1).

The ids the observation resolves are what is kept and what keys answers: **a ref is never a key** (D147 §4.2).

### 5.2 One head id for both halves of a review

The review's `h` is the observation's id for the tree's `HEAD`. **W** diffs the working tree against `h`, not the name `HEAD`
(identical semantics for a worktree diff), and **S** runs with `--branch`, whose `# branch.oid` names the `HEAD` git read (Fable B3,
Codex 14). **V is the final barrier** (round 3, Codex R2-3): it waits for **every component the answer returns**: Σ's selection,
the selected committed diff (D₀ or D), the counts N, S, W, the untracked display additions U and their attributes A. Then it validates,
together and after all of them: the refs observation (§5.1's stamps, whole-read contents and watch generation); S's `branch.oid` = `h`;
**the tree's watch generation** unchanged since before S, W, A and U started, with no hint, loss or index move since (§6.3); and the
context id (§5.3). Only an answer V passes is kept or published as current. Where any differs, the reader reads the graph once more
(only the nodes whose keys moved), and where the second read moves again it answers with what it read, flagged **changed during
read** with both ids, never a mixture shown as one state. Retries are bounded at one, in the retry budget. A tree with no valid watch
(none established yet, or released) cannot have its uncommitted part validated: its answer is shown *Read at …* and never kept.

**The page's key stays the session** (Codex 14): dirty files and the line's tips change without `h` changing (`queries.ts:146`). The
answer carries its observation (`h`, `from`, the line's tips, the tree's generation), the page draws *Read at … · HEAD …* from it, and
the driver's immutable patch cache is keyed by ids and context. A mutable observation of a session (its tree, its line) and an
immutable answer keyed by ids are kept apart and never joined by the same key.

### 5.3 What is kept, under what key and context

| Answer | Key | Kept |
|---|---|---|
| A range's files and patches (Review's committed part, a commit, a compare, a landed branch) | common dir, view, from, to, options, parser version, context id | until evicted |
| `merge-base`, `--is-ancestor` (its "no" is an answer), `--independent`, `rev-list --count` | common dir, command, ids, context id | until evicted |
| `merge-tree --write-tree` (M, the preview), a worktree-context read (§1.1) | common dir, ids, context id, the tree's identity and watch generation | under the tree watch only (§6.3); a generated tree found missing is a miss (below) |
| The context (C) | the common dir and the tree's identity | until a file it names or would read moves (below) |
| A graph page, a file's history, a blame, a commit's fields (GIT1b) | as today, with options and context id | until evicted |
| A file's history's id list (1h) | common dir, commit, path, context id | until evicted |
| A search by message, author or path | common dir, tips, query, context id | until evicted |
| The layout | the folder | until its `.git` stamp changes, by §5.1's order |
| The refs observation | the common dir | until §5.1 says otherwise |
| **Never** | `status`, a worktree diff, anything the working tree decides, outside §6.3's watched answer; a name alone; a read git did not answer; a pickaxe | |

**Context per read kind** (Codex 12). Every option that changes an answer is **frozen on the call** where git lets it be:
`--no-ext-diff --no-textconv`, `--no-color`, prefixes, `-c core.quotePath=false`, the rename detection and limit
(`-M`, `-c diff.renameLimit=…`), the diff algorithm (`-c diff.algorithm=myers`), `--no-mailmap` (identities shown as written; a
mailmap is a later display choice), `-c blame.ignoreRevsFile=` (round 1 claimed it was given; it is not, `GitReads.Formats.cs:73`), and
**`git --attr-source=<to>`** on object reads, so the committed `.gitattributes` come from the commit read rather than from whichever
linked tree asked (git 2.41 and later, below). `--attr-source` replaces only the worktree's `.gitattributes` (`git.adoc:229-232`;
`gitattributes.adoc:70-77`: `info/attributes` first, then the tree's files, then the global and system files), so the rest is the
context's.

**What forms the context id** (round 3, Codex R2-5), by category, each read by the context read C and each named with how its change
is heard:

| Input | Read by | Heard by |
|---|---|---|
| The binary: Tools' git's path and `git version` | the path from `GitWiring`; `git version` once per binary per process | a switch of Tools' git (the `tools` scope, `stateHub.ts`) makes a new binary |
| The environment the seam hands git (`GIT_CONFIG_*`, `GIT_CONFIG_COUNT`/`KEY_n`/`VALUE_n`, `GIT_CONFIG_NOSYSTEM`, `GIT_ATTR_NOSYSTEM`, `HOME`, `XDG_CONFIG_HOME`); the seam sets `GIT_ATTR_SOURCE` to nothing of its own | their values, hashed | fixed for the process's life |
| Configuration: every file `git config --list --show-origin --show-scope -z` names (system, global, the repository's `config`, the tree's `config.worktree` under `extensions.worktreeConfig`, each `include.path` and matched `includeIf` target), and **every candidate it would read though it named none**: `git var -l`'s `GIT_CONFIG_SYSTEM` and each `GIT_CONFIG_GLOBAL` line (two here, the XDG file and the home one), the repository's and the tree's files, and each include's target from its key, present or absent | C's answer, as text, hashed; each file stamped, an absent one stamped absent | inside the common directory, the reader's ref watch (§5.4); outside it, a root per folder holding such a file, non-recursive and filtered to its names, on the same instance; and that topic's deadline (provisional 60 s) re-stamping them with no process |
| Conditional includes: an `includeIf.onbranch:…` key in C's answer makes the configuration depend on the tree's branch; `includeIf.gitdir:…` on the tree | C's answer | where one is present, the tree's identity and `HEAD`'s branch join the context's key, and a `HEAD` move re-reads C |
| Attributes: `info/attributes`, `git var -l`'s `GIT_ATTR_GLOBAL` (the effective `core.attributesFile`) and `GIT_ATTR_SYSTEM` | the files' contents, hashed where at most 1 MiB, else their stamps | as configuration above |
| The repository's shape: `refs/replace/`, `info/grafts`, `shallow`, `objects/info/alternates`; `extensions.partialClone` and any `remote.<name>.promisor` from C's answer | stamps from disk; C's answer | the ref watch; where replace refs, grafts or `shallow` exist, nothing of that repository is kept |

The context id is **the hash of what was read**, not of the stamps: a stamp that moved re-runs C (2 starts), and a context whose
answers are unchanged keeps its id, so a touched file costs no page's answers. `git var -l` listing `GIT_ATTR_*` and
`GIT_CONFIG_*` is git 2.42 (`RelNotes/2.42.0.adoc:16`); below it, the global and system attribute files are not known, and no object
answer of that binary is kept. `--show-scope` is git 2.26 (`RelNotes/2.26.0.adoc:48`).

The context id is **validated before lookup, before joining and at completion**, and it **reaches a page that asks nothing**
(Codex R2-5, checked at `Web/src/bridge/state/stateHub.ts:25-68`, which has no `git` scope today, and `git.ts:262-294`, whose commit,
history and blame queries are kept with `staleTime: Infinity`). 1a-v adds: a `GIT_CONTEXT` read per repository answering its
**context revision** (the id), with no process while C's answer stands; the id-keyed queries carry that revision in their keys and
wait for it; and a `git` scope in the state hub mapped to the repositories' context queries and branch lists. When C's answer
changes, the driver publishes `git` for that repository; an open page's context query is asked again, its revision moves, and every
id-keyed query of the old revision is asked under the new key. A test changes a global attributes file while an open commit page
makes no request, and sees the page re-read under the new revision.

**Two linked trees** (round 3, Codex R2-17). Under `--attr-source=<to>`, an object read at the same ids in two linked trees with the
same context id gives the same answer and **shares one kept answer**; a fixture holds that equality. Their **worktree** reads (S, W, A,
M) answer from each tree's own `.gitattributes` and differ where those differ; they are never kept apart from their tree's watch and
never shared. A tree whose `config.worktree` or `onbranch` include gives it a different context id keeps its own object answers.

**Generated objects** (Codex 19, Fable N10). A `merge-tree` answer is a tree id no ref holds, which the person's `gc` may prune after
two weeks. A read that finds a kept generated object missing is a **miss**: the entry is dropped and the object computed again, never
an answer of absence and never "git could not read". A fixture prunes a kept generated tree and sees it computed again.

**Partial clones** (Codex 13; round 3, Codex R2-7). A local read must never fetch. **The floor is git's own release notes, read in
Tools' git's documentation, not memory**: `git --no-lazy-fetch` is git 2.45 (`RelNotes/2.45.0.adoc:120`), and `git.adoc:183-188` says
it equals `GIT_NO_LAZY_FETCH=1`; round 2's 2.44 was wrong. On git 2.45 and later every read that may touch an object passes
**`git --no-lazy-fetch`** as a global option, and the seam also sets the variable for git's children: the option is the guard, because
a git that does not know it refuses to run (an unknown global option), where an unknown variable is silently ignored and would fetch.
Below 2.45, the option is not passed and, where C shows a partial clone (`extensions.partialClone`, or a remote with `promisor`), every
object-dependent read is **refused before it starts**, said; a repository with no promisor has nothing to fetch. A fixture holds it on
Tools' git: a `--filter=blob:none` clone of a local bare promisor that logs each `upload-pack`, then a two-commit `diff -p` that
**demands a missing blob**: refused, and the log empty. The below-2.45 branch is held by the launch seam's stand-in answering as git
2.44, which must see no option and the read refused; no older git is installed here to run.

**The floors, together**, each from `RelNotes/` in Tools' git's documentation (round 3, Codex R2-7), and each checked against the
binary's `git version` from C before its option is passed:

| Capability | Floor | Where git says so | Below it |
|---|---|---|---|
| `merge-tree --write-tree` (M, the preview) | 2.38 | `2.38.0.adoc:16` | no merged base, the line's base, as `ReviewRanges.cs:84-88` already does |
| `git --attr-source=<tree>` on object reads | 2.41 | `2.41.0.adoc:116`; `merge-tree`'s crash with it fixed in `2.43.0.adoc:257`, which this design no longer needs since M passes none | object answers not kept |
| `git var -l` naming the attribute and configuration files | 2.42 | `2.42.0.adoc:16` | object answers not kept |
| `git --no-lazy-fetch` | 2.45 | `2.45.0.adoc:120`; `git.adoc:183-188` | partial clones' object reads refused |
| `cat-file --batch-command` (deferred, §4.3) | 2.36 | `2.36.0.adoc:75` | no held reader |

### 5.4 What a change invalidates, and who hears it

**Dependencies** (Codex 16): a review depends on its common directory's observation, **its tree's actual `HEAD`** (the file, not the
folder's name), the branch that names, the configured line and the line's tips here and on origin, and its recorded base. A tree whose
agent switched or detached is followed by its `HEAD` file (TREEFOLLOW1, D164); the session ↔ tree ↔ branch map is the session records'
tree folder through `TreeFollows.BranchOfTree`, with `GrownBranch.Now` for a tree its agent moved (Fable N5), never
`session-branches.json`, which names no session.

| Change | Invalidates | Published |
|---|---|---|
| A tree's `HEAD` or its branch's ref moves | that tree's review | `diff` for its session |
| The line here or on origin moves | **every** review in the repository, and its branch list | `diff` for each session there; `lines` |
| Any head, tag or remote ref moves | the branch list, holding-ref decorations, the graph's tips | the repository's `git` scope |
| `logs/refs/stash` moves | the stash list | `git` |
| `worktrees/` changes | the worktree list | `git` |
| A file the context names or would read moves (§5.3) | the context, read again by C; where its id changed, every kept answer of the repository | `git` (the page's context revision), `diff` |
| A tree's index moves (§6.3) | that tree's uncommitted answer, and its watch's exclusions until proved again | `diff` for its session |
| A watch's loss, overflow, error, cap or the machine's resume | everything that watch guarded | all of the above for its repositories (missed-event reconciliation), and the next use reads afresh |

The state hub carries invalidation keys, not ids (`Web/src/bridge/state/stateHub.ts:31`); the page's next read asks the reader,
which joins any read already in flight for the same key (§4.1).

**Which watch** (Fable N4, Codex 3). The ref watch's current set (repositories holding Daoris's work) keeps its consumers
(`LandingWakes.Moved`, `HelpFacts.Woken`; `DriverWatch.cs:432-438`) unchanged. The reader's roots are a **second `ChangeWatch`
instance**, its hint only dropping observations and publishing scopes: every registered repository with a checkout, **main or linked,
resolved to its common directory and identified by it**, those holding work first, within its own 32; **past the cap nothing of that
repository's refs is kept** (§5.1, round 3: round 2 let the stamp alone guard). It hears `refs/**`, `logs/refs/stash`, `worktrees/*`,
`config`, `config.worktree`, `info/attributes`, `info/grafts` and `shallow` beside what `RefWatch.Hears` hears today, and the context's
folders outside the repository (§5.3), shared by every repository that names them. The tree watches of §6.3 are a **third instance**,
capped at eight attended trees; past it, an open review pays the validation read.

## 6. Working-tree state (Q4)

### 6.1 Read, never written

Two reads, **S** and **W** (§3.1), both under REVIEWLOCK1's rule; status is kept apart from the patch, since staged and unstaged
changes that cancel give an empty patch while the tree is not clean (Codex X4). Unknown is never clean. **fsmonitor and the untracked
cache are not turned on, and no press offers them**: they write the repository's configuration and start a daemon the person did not
ask for. Where the person turns them on, Daoris's `status` is faster with them. Where `review.read` shows a slow status, Review's head
(and GITAPP2's Changes tab) names the command (`git config core.fsmonitor true`) to copy.

### 6.2 Untracked files: a display addition from disk

An untracked file is shown as **a display addition** (Codex 17, taking Codex X4's contract): its path, its lines as added, and its
facts, read from disk within bounds. It is **never a staging patch**: no act applies it (GITAPP2's acts compose their patches from
git's own comparisons). It is **exact**, equal to git's text for the file, for a stated subset; outside it, the row says what it is instead:

| The file | Shown |
|---|---|
| **The exact subset**: a regular file, no attribute among `diff`, `text`, `eol`, `filter`, `binary` set, no NUL in the first 8,000 bytes, valid UTF-8 or ASCII, LF or CRLF as they are | its lines, equal to git's patch text for it |
| Empty | *empty file*, as git prints no hunk |
| A NUL in the first 8,000 bytes, or `binary`/`-diff` set | *binary* (D111), its size |
| Not valid UTF-8 | its lines decoded as `ReadGitAsync` decodes git's output (U+FFFD for an invalid sequence), marked *not UTF-8* (Fable N16) |
| An attribute naming a filter or `eol`/`text` conversion | its size and *shown as stored, not as git would convert it* |
| A link or reparse point | *a link*, its target never read or followed |
| A path `status` lists with a trailing `/` (a nested repository, a folder) | the folder, never read |
| Changing during the read (size or time moved) | read once more, then *This file changed while it was read* |
| Over 200 KiB (`UntrackedPatchLimit`) or past the aggregate budget | listed, not read |

**Attributes cost one start for all the files, none per file**: node A, `git check-attr --stdin -z diff text eol filter binary`
over every untracked path at once. **The bounds stop reading, not only the string** (Codex 17): a file is read at most up to its
per-file limit and what remains of `PatchBudget`, so a large file allocates no more than the budget allows. Git on Windows writes mode
`100644` for these files (`core.fileMode` false); an executable bit is not shown.

**The proof** (§10.2) compares the exact subset with today's oracle as it is written (`WorkingTree.cs:386-387`:
`-c core.quotePath=false diff --no-index --no-color --no-ext-diff --src-prefix=a/ --dst-prefix=b/ -- /dev/null <path>`), at the
decoded text both sides go through, and shows each row of the table above for its fixture.

### 6.3 The tree watch

1b keeps the uncommitted answer (S, W, U) under a `ChangeWatch` root on the tree's folder **for every attended mutable tree**: an own
session tree with Review open, **live or ended** (an ended tree can still hold and gain uncommitted files, `TreesRoutes.cs:70`;
Codex 15); the person's checkout's Changes tab is GITAPP2's. Released when the view closes or after fifteen idle minutes.

- **Installed before the answer is taken**: the root is watching before S and W start, and a hint during them drops what they read.
- **Exclusions are proved, not assumed** (Codex 15, Fable N13): `.git`, and an ignored folder only where git tracks nothing inside
  it. The driver computes its own list per root (E₁, one counted `ls-files --others --ignored --directory --exclude-standard`, then
  E₂, one `ls-files -z -- <those folders>` that must be empty), never the service's `IgnoredPaths`, which is another process. A change
  to a `.gitignore`, `info/exclude` or the configuration runs both again.
- **The tree's index is watched, and its move revokes the exclusions** (round 3, Codex R2-9). An ignored folder can gain a tracked file
  (`git add -f`), which only the index shows. So the tree watch holds a second root, **the tree's own index file**: `<gitdir>/index`,
  which for a linked tree is `<common dir>/worktrees/<name>/index`, outside the tree's folder; non-recursive, filtered to `index`. Its
  move (a) drops the kept uncommitted answer and publishes `diff` for the session, so an idle open page re-reads rather than waiting
  for its next request; and (b) **revokes every exclusion** until E₂ has run again and found those folders still untracked (one
  counted start); a folder found tracked is watched from then on. Until E₂ answers, every hint under the tree drops the kept answer.
  An index stamp alone never proves the files unchanged (an unstaged edit moves no index), only the watch does.
- **Its budget** (Codex R2-2), the watch budget of §3.1, never on the graph's path: establishing a tree's watch is E₁ then E₂, 2
  starts, depth 2, run beside the first read; an index move costs E₂, 1; an ignore or configuration change E₁ and E₂, 2. **Until the
  exclusions are proved, the answer is shown and not kept**: the first open keeps nothing whose watch was not yet whole.
- **0 starts on reopen only while the watch is valid**: it was installed before the kept answer, its exclusions proved, and it has
  seen no loss, overflow, cap, release, resume or index move since. Otherwise the reopen pays the counted validation read of §3.1's warm
  table: S and W, and **A where S lists an untracked file**, since attributes kept under a watch that lapsed are not trusted: 2-3 starts,
  depth 1-2, said in `review.read`.

## 7. What the person gets (Q5)

### 7.1 The reads, and the capability boundary

**This table is the capability boundary** (Codex 8): what the person gets is what it lists, not "every read a git management app
offers". Rows name where each lives (D150 point 3: Repositories → a repository's tabs; a session's Review) and which row builds it.

| Capability | Per repository | Per session tree | Where | Row |
|---|---|---|---|---|
| A commit | message, author, committer, parents as doors, refs holding it, who made it (D143), its changes in `PatchView`: **two dependent starts**, its fields then its range against the first parent (`GitReads.cs:290-313`; round 3, Codex R2-17) | the same | a commit's page | 1d-i |
| Diff views | inline (built), side by side, word diff, whitespace ignored (a re-read with `-w`, keyed by it); a binary says so (D111) | the same in Review | Review, a commit's page | 1d-i |
| LFS | a pointer drawn as *a file kept by LFS* with its size, never its pointer text | the same | Review, a commit's page | 1d-i |
| History graph | built; filters by branch, author, path, since and until (one `log`, keyed by tips and filter) | the branch beside its line | History tab; Review's *Show in Git* | 1d-ii |
| Blame | a gutter, runs grouped, *Before this change* | at the tree's `h` | a file row's *Blame* | 1d-ii |
| A file's history | following renames; pages as GIT1b reads them today | the same | a file row's *History* | 1d-ii; its kept id list 1h |
| Compare | two refs resolved once; `A...B` and `A..B` | the tree's branch against its line | a branch's *Compare with…* | 1d-ii |
| Search | messages and authors (one `log`, kept); paths at a commit (`ls-tree`); content at a commit (`git grep`) | from the tree's `h` | History tab's search | 1d-ii |
| Stashes, tags, remotes, worktrees, a branch's reflog | `stash list` and a stash's changes by its id; tags on the graph and a commit; `remote -v` through `GitRemotes`; worktrees with the session holding each; `reflog show <branch>`, never kept | — | Branches tab; a branch's page | 1d-iii |
| Conflict preview | — | `merge-tree --write-tree` of `h` onto the line, in the session's tree under its attributes: would it conflict here, in which files and hunks (objects only); *Not previewed* where a merge driver is defined (§1.1) | Review's foot, beside Accept | 1d-iii |
| Content across history | `-S`/`-G` as a long read: path-scoped, streamed, cancellable, never kept | the same | History tab's search | 1h |
| Submodules | listed from `.gitmodules` and `160000` entries, each a door to its own repository where registered | the same | a commit's files | 1h |

**Deferred, said rather than implied** (Codex 8): an image diff; **interactive history editing** (reorder, squash, reword, drop: a
different act from GITAPP2's object rebase, which replays a linear range unchanged); a free three-way editor (D55); a submodule's own
writes; LFS content fetched or shown; staging in a session's tree; signing a commit Daoris writes; a held object reader (§4.3). Each
is a later row if asked. **Every act and write** (stage, commit, stash, switch, resolve, the ref and remote acts, history edits) is
GITAPP2's (D204).

### 7.2 The working-tree acts, the checkout lock and history edits: GITAPP2

Round 2's §7.2 (the person's checkout behind the switch, the checkout lock, the Changes tab's three observations) and §7.3 (history
edits from objects) moved whole to `docs/2026-10-23-git-writes-design.md` §2-§3 in round 3 (D204).

### 7.4 Agents

**Nothing new** (Claude, Fable). An agent runs its own git in its own tree; Daoris's reader is not offered to it as a tool. The terminal
read verbs (§9) read for anyone; the writing verbs are GITAPP2's.

## 8. Writes (Q6)

Round 2's §8 (what every act does, how each write recovers, D198's prerequisites for a push) moved whole to
`docs/2026-10-23-git-writes-design.md` §4 in round 3 (D204), with Codex R2-11 to R2-16 as its open questions. A read writes nothing of
Daoris's own into `.git`; `merge-tree --write-tree` writes objects only (§5.3's generated objects).

## 9. History at scale (Q7), the terminal door and Ask Daoris (Q8)

**History.** No index of Daoris's own, no cache on disk, and **no `commit-graph` press** (Codex X7): this repository already carries one
with changed-path filters (read, Fable N15); the other repositories here are at most a third its size. Where a history read is slow and
the repository has none (`objects/info/`, no process), the History tab's head names `git commit-graph write --reachable
--changed-paths` to copy. **A file's history** keeps its whole id list with each commit's path at `(commit, path)` from one
`log --follow --format=%H --name-status` walk (1h); until then, 1d-ii pages as GIT1b does today (Codex 28 found 1d promising what 1h
builds). **The pickaxe** is a long read (§4.2).

**The terminal door** (D50), one judge in the driver library for both doors:

- `daoris-driver git review <session> [--json] [--time]`: Review's answer; `--time` prints the graph's nodes, each start's command,
  gate wait, creation and run, each start's budget, what was kept, and writes `review.read`. The measurement door, built in 1a-i on
  today's chain before anything else changes (Fable N14).
- `daoris-driver git show|history|blame|compare|search|stashes|tags <repository> …`: the reads, each with its 1d slice.
- The writing verbs are GITAPP2's (`docs/2026-10-23-git-writes-design.md` §4.3).

Names are settled through the verb census (`cli-usage`) in their row. **Ask Daoris** (D110): the reads stay exempt, as `trace` is;
its `git` kind's act cards are GITAPP2's. `HelpCoverageTests` holds each control to its kind or exemption.

## 10. The test plan (Q9)

### 10.1 The graph and the counts, load-independent, the fast half

- **The graph, as data**: a test builds `ReviewGraph` for each path of §3.1's table and asserts its edges, its **depth** and its
  **start ceiling**, computed from the data; the table's numbers are this test's (Codex 5, 28).
- **The executor, with held completions** (Fable N3, Codex 28): through the launch seam (§4.1), a stand-in holds every answer until the
  test releases it. The test counts the starts issued before each release: a node whose inputs have answered is started before the next
  release, and no node is started before its inputs answer. Depth observed equals depth computed; one start more than the ceiling fails,
  and each is shown to fail when a per-file or serial start is put back. Fallbacks and retries (V's one re-read) are counted apart.
- **Every path generated** (round 3, Codex R2-1): the census enumerates every combination of §3.1's conditions, live, landed and gone,
  and holds each to its row's depth and ceiling or the general ceiling; the grown base, the three line sources and each failure are
  among them. Each start carries its budget (graph, context, watch, retry); a start with none, or past its budget, fails.
- **The line from R**: the derivation equals `WorkingTree.DefaultBranchAsync` over origin's `HEAD` set, unset and dangling, `main`,
  `master`, both and neither, and a tag object under `refs/heads/main`.
- **The barrier** (Codex R2-3): with held completions, V publishes nothing while any returned component (Σ, D₀ or D, N, S, W, A, U) is
  held; a hint to the tree's watch released before V makes V read once more; D₀ answering before Σ is kept by its key and named the
  review's diff only where Σ selects it.
- **Pinning**: a stand-in whose `HEAD` moves between R and S makes V see S's `branch.oid` differ, read once more, and flag *changed
  during read* when it moves again; never an answer mixing two heads (Fable B3, Codex 14).
- **Joining**: two waiters on one key start one read; one cancelling leaves the other answered; the last cancelling cancels the job; a
  request after a tree hint does not join the read begun before it (Codex R2-3).
- **Run places**: eight unattended reads never take the reserved place; an attended read arriving behind seven running unattended ones
  starts at once (Codex R2-10).
- **The context reaches an idle page** (Codex R2-5): a mocked bridge whose `git` scope moves the context revision re-asks an open commit
  page's query under the new key with no request from the page; a stand-in binary answering as git 2.40, 2.42 and 2.44 sees each floor's
  option withheld and its fallback taken (Codex R2-7).
- **REVIEWLOCK1's census, widened**: every read kind's flags asserted; `tools/optional-locks.mjs` refusing a worktree `diff` without
  `diff.autoRefreshIndex=false` (Fable B1); a source census refusing git started under `Trees/GitReads/` except through `Launch`.
- `ReviewBaseTests` and `TreeUpdateTests`, unchanged, prove the range's semantics did not move.

### 10.2 Real git, the Process half (`Parallel=Git`)

- **No lock**: REVIEWLOCK1's `ReviewLockTests`, run against the reader's nodes.
- **Configured programs**: a textconv and an external diff configured, neither runs on an object read; `blame` given only
  `--no-textconv`; a marker-writing merge driver defined in the repository's configuration with a committed attribute, with
  `info/attributes`, with a global attributes file, and as `merge.default`: M and the preview refused before starting, the marker
  never written; a committed `merge=union` giving the merged base plain git gives (Codex 10, R2-6).
- **The observation**: drops on a loose ref write, `pack-refs`, a `HEAD` move, a linked tree's `HEAD` move, a `worktree add`, a stash drop
  of an older entry; **a ref moved while a held stand-in `for-each-ref` is still answering** is not kept (Fable B2); a ref rewritten at the
  same length within the racy window is not kept (Codex 11); **a nested ref (`refs/heads/team/topic`) moved in a repository past the
  watch's cap**: nothing was kept, and the next use reads R again and sees it (Codex R2-4).
- **Context**: two linked trees at the same ids with different `.gitattributes` give **equal** object answers under `--attr-source` and
  share one kept answer, while their worktree reads differ and are never shared (Codex R2-17); a global attributes file, an include and
  `config.worktree` each changed re-read C and move the id where the answer changed; a repository with `refs/replace/` or `shallow`
  keeps nothing (Codex 12).
- **Partial clone**: §5.3's fixture, a blob demanded and refused, the promisor asked nothing (Codex 13, R2-7).
- **The display addition**: §6.2's table, a fixture per row (ASCII, UTF-8 with a non-ASCII path, CRLF, no trailing newline, empty, a NUL,
  Latin-1, an attribute with a filter, `eol`, a link, a nested repository, over 200 KiB, past the budget, a file growing during the read),
  the exact subset equal to the oracle (Codex 17, Fable N16).
- **The watch**: a write under a watched tree drops the kept answer; a write inside an ignored folder that holds a tracked file is heard;
  a watch released or lost makes the next reopen pay the validation read, **and a new untracked file appearing while it was lost makes
  that read include A** (Codex 15, R2-2); **`git add -f` of a file in an excluded ignored folder, then an edit of it**: the index move
  revoked the exclusion, E₂ found the folder tracked, and the edit drops the kept answer; **an index-only change** (`git add` of an
  already-edited file) publishes `diff` and an idle open page re-reads (Codex R2-9).
- **A pruned generated tree** recomputed, not answered as absent (Codex 19).

Per the person's standing instruction on this machine, a row runs its own classes filtered and the censuses; the whole halves run in CI
after the merge.

### 10.3 The bench and the window

- **The bench**, reported, never failing: `daoris-driver git review <session> --time` over the three cases on this repository (368 files;
  the same with 50 untracked made by the bench in `_fixtures/`; 1,771), at least ten runs each: median and p90 of wall, depth, starts, and
  per start the gate wait, creation and run, with the machine's load beside. Before: the brief's numbers, through the same verb once 1a-i
  lands its door on today's chain. After: §3.4's targets. Written to the row's fix-log entry with the sha.
- **The window** (GITAPP1i, the owner's): Review opened from the press, timed to first paint by `review.painted`; the driver's
  `review.read` (`session`, `ms`, `depth`, `starts` and each budget's, `waitedMs`, `createdMs`, `files`, `untracked`, `kept`, `head`,
  no path). Three opens
  before the republish and three after, under the owner's load.
- **Held afterwards**: the graph census is the gate; the bench's medians are read at each release rehearsal, and a rise past twice a
  target is a row, not a failure.

## 11. Surfaces in English and Chinese

Chrome only; commit messages, patches, file and branch names stay as written. Each new term passes D116's check before it is drawn,
git's own Chinese translation being the source where it has one. Existing glossary terms used as they stand: *review* 审阅, *branch*
分支, *tree* 工作树, *history* 历史, *graph* 提交图, *commit* 提交, *fetch* 获取, *push* 推送, *merge* 合并, *line* 主线, *landing*
落地, *git remote* 远程仓库.

| Surface | English | 中文 | Row |
|---|---|---|---|
| Review's head, a kept answer | Read at {time} · HEAD {id} | 读取于 {time} · HEAD {id} | 1a-v |
| Review's head, a read that moved | Changed while it was read: HEAD {from} → {to} | 读取期间已变化：HEAD {from} → {to} | 1a-v |
| Review's foot while its reads run | Reading… | 正在读取… | 1a-v |
| A file still changing | This file changed while it was read | 读取时该文件仍在变化 | 1a-v |
| An untracked file outside the exact set | Empty file · Not UTF-8 · Shown as stored, not as git would convert it · A link | 空文件 · 非 UTF-8 · 按存储内容显示，未按 git 转换 · 链接 | 1a-v |
| Review's head, no merged base | This repository defines a merge driver, which Daoris does not run | 此仓库定义了合并驱动，Daoris 不会运行它 | 1a-v |
| A commit's page | Commit · Parents · Held by · Made by | 提交 · 父提交 · 包含于 · 由谁创建 | 1d-i |
| Diff views | Inline · Side by side · Word diff · Ignore whitespace | 内联 · 并排 · 按词对比 · 忽略空白 | 1d-i |
| LFS | A file kept by LFS ({size}) | 由 LFS 保存的文件（{size}） | 1d-i |
| A file row | Blame · History · Before this change | 逐行追溯 · 历史 · 此更改之前 | 1d-ii |
| Compare | Compare with… | 对比… | 1d-ii |
| Search | Search messages, authors, paths · Search content (slow) | 搜索说明、作者、路径 · 搜索内容（较慢） | 1d-ii, 1h |
| Branches tab | Stashes · Tags · Remotes · Worktrees · Reflog | 贮藏 · 标签 · 远程仓库 · 工作树 · 引用日志 | 1d-iii |
| Review's foot, the conflict preview | Would conflict here in {n} files / Merges without conflict here / Not previewed: this repository defines a merge driver | 此处将在 {n} 个文件中冲突 / 此处可无冲突合并 / 未预览：此仓库定义了合并驱动 | 1d-iii |
| History's head | To speed this up in your own git: {command} | 如需在你自己的 git 中加速：{command} | 1h |

The acts' surfaces are GITAPP2's (`docs/2026-10-23-git-writes-design.md` §5). *Submodule* (子模块) and *merge driver* (合并驱动) are
the words D116's check settles first: git's own catalogue and the glossary's neighbours are read then. Stories at 1546 and 680 px in both themes and both languages, and UX6a's counter, hold each screen.

## 12. The rows (Q10)

Each row lands alone: its own proof, its terminal verb, bridge, Ask Daoris kind and translations within it (Codex 29, Fable N14).

**Round 3 splits round 2's 1a-i so the first row lands alone** (the quest: "split the rows so the first lands alone"): it changes no
behaviour, adds the seam and the door everything after is measured through, and its proof needs nothing later. Round 2's 1a-ii is
now 1a-v. The act rows (round 2's 1e, 1f, 1g-i to 1g-iv) are GITAPP2's (D204); 1d-iv is deferred (§4.3).

1. **REVIEWLOCK1** (landed on `main` at `808879b72`, after this tree's base). §1.1's rule and census. Nothing in this design rebuilds it.
2. **GITAPP1a-i — the launch seam and the bench door, on today's chain** (driver; after REVIEWLOCK1; **lands first and alone**). One
   `Launch(kind, arguments, budget)` beneath `RunGitAsync`, `ReadGitAsync` and `GitBytesAsync` (`WorkingTree.cs:953,1020,1093,1187`),
   classifying every start by read kind and budget and raising the one counting event, REVIEWLOCK1's flags applied there; §1.1's
   per-command controls asserted per kind; a source census refusing a git start under `Trees/` outside the seam; `daoris-driver git
   review <session> --time` over **today's** chain, printing each start's command, budget, gate wait, creation and run, and writing
   `review.read`. No answer changes. **Proof**: a census test over every read kind's flags; a test counting today's Review at
   `12 + I + M + Uᵣ` through the seam on a fixture of each path (§1's count), the line's guesses and `IsTopLevelAsync` included; the
   before-bench of §10.3 on the three cases, written to the fix log.
3. **GITAPP1a-ii — the reader, the observation and the context** (driver; after 1a-i). `GitReader`, `GitReaders`; the refs observation
   and its retention rule (§5.1) with the reader's ref watch (§5.4); the context read C, its inputs, floors and id (§5.3), its watch
   roots and deadline; the cache keyed by ids and context; joining with the mutable generation; the run places with one reserved
   (§4.2). Review still runs today's chain, reading R and C through the reader. **Proof**: §10.1's joining and run-place cases; §10.2's
   observation, context, configured-program (object reads) and partial-clone cases; the stand-in binaries for the floors.
4. **GITAPP1a-iii — Review's graph** (driver; after 1a-ii). `ReviewGraph`, its executor, Σ and the barrier V (§3.1, §5.2); the line
   from R; K′; the landed and gone subgraphs through the reader (`LandedReviews`, `LandedProofs`); M's merge-driver refusal (§1.1).
   Untracked files still as today (one start each) until 1a-iv. **Proof**: §10.1's generated census, line derivation, barrier and pinning
   cases; `ReviewBaseTests` and `TreeUpdateTests` unchanged; §10.2's merge-driver cases; the bench after.
5. **GITAPP1a-iv — untracked display additions** (driver; after 1a-iii). Nodes A and U (§6.2), none per untracked file. **Proof**:
   §10.2's display-addition fixtures against the oracle; the census's untracked paths at ≤ 10 cold; the 50-untracked bench.
6. **GITAPP1a-v — Review's page on the observation** (modules, web; after 1a-iv). `SESSION_DIFF` answering with its observation; the
   offer and HANDOFF_PLAN after the diff, timed apart; *Read at*, *Changed while it was read*, the merge-driver line; `review.read`
   from the route, `review.painted`; the page's re-read on every `updated` replaced by the 15 s validation read; the `git` scope and
   `GIT_CONTEXT` revision in the state hub and the id-keyed queries (§5.3). **Proof**: the route's counts per path; the page's stories
   in both languages; a mocked bridge proving the offer asked after the diff and the idle page re-read on a context change.
7. **GITAPP1b — Review follows the tree** (after 1a-v). §5.4's dependencies on the reader's watch; §6.3's tree watch for every attended
   tree, its index root, its exclusions proved and revoked, its budget; pre-read joining the page's refetch. **Proof**: a fake change
   source moving the line invalidates every review in the repository; a file written under the tree drops the kept answer; a lost
   watch makes the reopen pay 2-3 starts, A included where a new untracked file appeared; force-add-then-edit and an index-only change
   (§10.2); an idle reopen starts nothing; a commit shown ≤ 3 s on the bench.
8. **GITAPP1c — the judgements through the reader** (after 1a-ii). `help.*`, the tidy, the landing's plan, `GitBranches.ListAsync` and
   the offer take layout and ids from the reader; the offer's count bounded per eligible branch. **Proof**: PROC1a's start counts per
   judgement before and after.
9. **GITAPP1d-i — the commit page and diff views** (after 1a-v; takes GIT1i's commit page). A commit, side by side, word diff,
   whitespace, LFS pointers. **Proof**: `GitReadsTests` holding **each view's own start graph** (the commit page two dependent starts,
   `GitReads.cs:290-313`; a re-read with `-w` one) and its key with the context (Codex R2-17); stories and counters.
10. **GITAPP1d-ii — blame, a file's history, compare, search** (after 1d-i; takes GIT1f). As §7.1; file history paged as today.
    **Proof**: as 1d-i; blame with `blame.ignoreRevsFile` configured unchanged.
11. **GITAPP1d-iii — the Branches tab's reads and the conflict preview** (after 1d-i). Stashes, tags, remotes, worktrees, reflog; the
    conflict preview in Review's foot, refused where a merge driver is defined. **Proof**: as 1d-i; the stash list moving on an older
    drop; the preview's merge-driver cases (§10.2).
12. **GITAPP1h — history at scale** (after 1d-ii). The file history's kept id list, the pickaxe as a long read, submodules, the
    commands to copy. **Proof**: the third page of `TASKS.md`'s history no longer walking from the top (one walk counted); the pickaxe
    cancelled ends its git; two pickaxes in two repositories leave a short read a place.
13. **GITAPP1i — the install, seen** (the owner; after 1a-v, again after 1d-iii). §10.3's window timings and the screens in both
    languages. **Proof**: the fix-log entry and its screenshots.

1a-i makes Review measurable; 1a-ii gives it a reader that knows what it read; 1a-iii and 1a-iv make it fast; 1a-v puts it on the page;
1b keeps it fast while a session moves; 1c pays the same debt where the landing and the judgements wait; 1d draws the read half of D147
that is built and drawn nowhere.

## 13. Fit with D147, D113, D150 and D198

- **D147** is amended, never rewritten, in its read half only (round 3): point 6 and design §4.1 ("one process a view" becomes the
  reader: a held graph of bounded starts and an identified observation; still Tools' git and never a library). **§4.2 stands**, said
  exactly (Fable N10): a read writes no ref, configuration or file on disk of Daoris's own choosing and caches nothing on disk; objects
  only where `merge-tree --write-tree` writes them. Its points 2 and 3, its rejection of checking out in the person's checkout and its
  design §2.8 and §3.1 are what GITAPP2 (D204) would amend; until it is accepted they stand.
- **D113 §5 stands** (Fable N11): it rejects reading the person's checkout for a landed session's review, and the landed review still
  reads objects. Round 1 named it as amended; it is not.
- **D150 stands** in this design: the reads draw in the repository's existing tabs and Review. The Changes tab and the switch are
  GITAPP2's. No Git place returns to the activity bar; the branch, then the commit, stays the organising object (D55).
- **D198 stands**; a read passes no door. Its prerequisites for a push are GITAPP2's.
- **D51, D55, D109, D164, D173, D181 stand.**

## 14. Rejected, by proposal

**From Claude's**: ≤ 4 starts and its 1.5 s target (they drop REVIEWBASE1 and REVIEWBASE3); Review on the person's gate place (§4.2); a
held process for two idle minutes, eight across the machine (§4.3); the generation counter alone (§5.1 keeps a generation, never alone);
a pushed slice per review, this round (§3.3); a tree watch on every running session (§6.3 watches attended trees); the `commit-graph`
press (§9); the file history cursor (§9); a free three-way editor (D55).

*Round 3:* the rejections of a manual tree and of an idle session tree for the acts are GITAPP2's, and are kept here as they were
made.

**From Codex's**: the manual worktree of the person's (§7.2); "four short jobs, two walks" as one global bound above the gate (§4.2 keeps
per-reader places and a per-process walk limit); agents' authorised local operations through Daoris (§7.4). Taken from it: the union
counts and every range path kept, the offer and plans off the patch's path, the context in the key and partial clones, *Read at* rather
than *current*, the refusal list, serialised own writes, the held-process cap, and in round 2 the display addition, the effect policy,
the observation's validation, process ownership, the transactions and the D198 prerequisites.

**From Fable's**: the idle session tree's acts (§7.2); the offer over the snapshot's tips (§3.2); `ahead-behind` for both counts; the
`commit-graph`, fsmonitor and untracked-cache presses; idle-machine targets as a held column. Taken from it: REVIEWLOCK1, the layout and
refs from disk with the stamp and its order, the levels handed longest first and the speculative committed diff, one head id, the
per-reader bound and Background class, the held process's shape and its hazard, the kept file-history ids, the targets' medians, the
bench, the watch for attended trees.

**By all three**: libgit2 or a reader of git's files (D147 §4.3; the stamps and small contents of §5.1 validate git's answers, they do
not replace them); Daoris's own history index; a cache on disk; polling `status` on a clock; a patch per file fetched from git; checking
out in a session's tree; a force push; a delete on origin; a push by rule; line comments; fetching on a timer; changing the gate's width.

## 15. Not measured

The installed window (Review's paint and the press-to-paint time); LANDING, HANDOFF_PLAN and the offer beside the diff; a tree with
hundreds of real untracked files; the observation on a reftable repository (none here); the tree watch under an agent's dependency
install; `--no-lazy-fetch` against a real partial clone here; `--attr-source` on Tools' git; the gate's `waitedMs` under a sweep's
fan-out; the reader's eight run places; the context read's cost and how often its deadline finds a move; an idle machine; **any git
older than Tools' 2.53**, whose floors are read from git's release notes and held by stand-ins, never run here. Every target is
provisional, set from the brief's and Fable's probes under the load they named; the rows measure the rest before they are accepted.

## 16. The reads, answered

Each finding was checked by Claude Opus at the lines it cites, at `fe4809b75` and on REVIEWLOCK1's branch. All held. **Taken** means
the design now says what the finding asked, or the stricter of two ways it offered; **answered** means part of it is met another way,
said in the row.

**Codex's read** (`docs/2026-10-23-git-app-reads.md`, read-only, at `92c7b8ed5`). Verdict: keep proposed and revise. 29 findings, 22
blocking.

| # | Finding | Checked at | Taken or answered | Where |
|---|---|---|---|---|
| 1 | "13-19 + U" is neither minimum nor ceiling; the offer is unbounded; page vs driver hits | `WorkingTree.cs:326-358`, `ReviewRanges.cs:42-126`, `TreesRoutes.cs:70-92`, `ContentHolds.cs:163-177`, `trees.ts:168-184`, `LandedReviews.cs` | Taken: today's count `12 + I + M + Uᵣ`, the offer unbounded, page hits apart, the probe a replay | §1, §3.1, §3.2 |
| 2 | GITAUTH1c is partly built; the doors' installation is opt-in | `GitIdentities.cs`, `GitIdentityCommand.cs`, `GitAuthor.cs`, `desktop-publish.mjs:1326-1340` | Taken; the brief is kept as written and corrected here | §1, §8.3 |
| 3 | The ref watch hears less and selects fewer than said | `RefWatch.cs:229-242,249-265,291` | Taken; the brief corrected here; the reader's watch by common-directory identity, linked checkouts included | §1, §5.4 |
| 4 | The scanner is an inference, not an attribution | `FIX-LOG.md:5226,3374` | Taken; gate wait, creation and run measured apart | §0, §1.2, §10.3 |
| 5 | The cold path is deeper than three rounds | `ReviewRanges.cs:72-77` | Taken: a dependency graph with per-path depth; the counts moved beside the merge-bases | §3.1 |
| 6 | The targets do not follow from the arithmetic; the gate bounds creation | `ProcessStarts.cs:29-31`; `WorkingTree.cs:271,274,451` | Taken but for the numbers: kept as **provisional** targets (the asker's instruction), no longer claimed from arithmetic; run places defined apart; the bench through the real gate; 1,771 as a bounded list | §3.4, §4.2 |
| 7 | A per-reader bound does not reserve capacity for Review | `ProcessStarts.cs:192,215,223` | Taken within the reader (attended first, walks one per reader and two per process); answered across readers by `waitedMs` before any gate change | §4.2 |
| 8 | "Every read" exceeds the enumerated contract | design §7.1 | Taken: §7.1 is the boundary; deferred capabilities listed; object rebase told apart from interactive editing | §7.1, §7.3 |
| 9 | The global option is placed after the subcommand | `HelperCapture.cs:104`, `HelpCheckouts.cs:104` | Taken: `git --no-optional-locks <command>`; the census checks placement (as REVIEWLOCK1's does) | §1.1, §3.1 |
| 10 | The census proves less than "no writes"; textconv, filters, drivers | `WorkingTree.cs:166,187,205,335,472`, `TreeWorkReads.cs:77`, `ContentHolds.cs:497`, `TreeSlims.cs:27` | Taken: REVIEWLOCK1 covers the 24 sites; the effect policy per read kind; `--no-textconv`; the empty attribute source for `merge-tree`; fixtures for configured programs; name reads not classed as writers | §1.1, §10.2 |
| 11 | Time plus size cannot make the snapshot authoritative | design §5.1 | Taken: generation and stamps before and after, small contents read whole, the racy-time rule, `worktrees/`, `commondir`, the stash log, reftable counted, fail closed; "an identified observation, not an atomic view" | §5.1 |
| 12 | The cache context is underspecified across linked trees and the page | `GitReads.Formats.cs:73`, `GitReads.cs:492`, `git.ts:262` | Taken: options frozen, `--attr-source`, a context id validated before lookup, joining and completion, the tree's identity where a read depends on it, carried into the page's keys | §5.3 |
| 13 | The partial-clone fallback still fetches | design §5.2 | Taken: `GIT_NO_LAZY_FETCH` proved for the git in use, otherwise reads refused before they start | §5.3 |
| 14 | The snapshot, dirty diff and joining can describe different HEADs | `trees.ts:81-108`, `queries.ts:146` | Taken: W pinned to `h`, S's `branch.oid` checked by V, one bounded re-read, *changed during read*; the page's key stays the session; joining by ids and context with separate waiter cancellation | §4.1, §5.2 |
| 15 | Zero-start freshness lacks a safe fallback | `TreesRoutes.cs:70` | Taken: every attended mutable tree watched, live or ended; installed before the answer; exclusions proved; any loss pays the counted validation read | §6.3 |
| 16 | Invalidation follows too few dependencies | `TreeFollows.cs:152`, `stateHub.ts:31` | Taken: dependencies by the tree's actual `HEAD` and the line; a line move invalidates every review; mutable screen families and missed-event reconciliation named; pre-read and refetch join | §3.3, §5.4 |
| 17 | The hand-built untracked patch is not byte-equal as specified | `WorkingTree.cs:386-387` | Taken: Codex's display addition, exact for a stated subset, never a staging patch; attributes in one start; bounds stop reading | §6.2 |
| 18 | The held process's protocol and lifetime are incomplete | design §4.3 | Taken: framed by length, retired on fault or mid-answer cancel, owned by the reader, four per process | §4.3, §4.4 |
| 19 | Cached generated trees need an object-lifetime rule | `ReviewRanges.cs:92` | Taken: a missing generated object is a miss, recomputed | §5.3 |
| 20 | Checkout permission and conversation exclusion are not atomic | `ChatRunner.cs:439`, `ChatGoOn.cs:442` | Taken: `CheckoutLock`, the conversation's start and the merge landing taking it, everything re-read under it; session trees refused independently | §7.2 |
| 21 | Allowed side effects exceed the `.git` rule | design §8 | Taken: the rename's section named and kept; Daoris's fetch without auto maintenance; hooks, reflogs and maintenance said in plans | §8.1, §8.2, §13 |
| 22 | Old-value CAS loses holder proofs and late checkout | `ContentHolds.cs:373,378`; the two knowledge documents | Taken: the judged-delete transaction reused, absence only on git's "missing", late checkout put back, extended to rewrites and renames | §8.2 |
| 23 | Object history editing lacks semantics | design §7.3 | Taken: linear, unpublished, non-merge, non-root; each base, metadata, empty and signed rule; ids pinned; recovery in the transaction | §7.3, §8.2 |
| 24 | Stash conflict handling promises a transaction git lacks | design §8 | Taken: retained conflicts as the outcome on a clean start; drop by commit id; inclusion said; a session's stash refused | §8.2 |
| 25 | Two calls cannot support the staging controls | design §6.1, §8 | Taken: three observations for Changes; patches from the right comparison; entries and bytes revalidated | §7.2 |
| 26 | The remote acts point to the wrong D198 milestone; a person's push needs a grant | `GitAccessRules.cs:40-75,144`; git-auth design §6.1; `TASKS.md` GITAUTH1g | Taken: tag push on GITAUTH1g; the doors installed; a press's own grant | §8.3, §12 row 10 |
| 27 | The terminal write contract omits the person's press | `InstanceRoutes.cs:9-21,156-170` | Taken: writing verbs are `PersonPresses` routes; the session-marker refusal kept as an extra guard | §7.4, §8.3, §9 |
| 28 | The first row's census and order cannot prove its claims | `WorkingTree.cs:1020,1093,1187` | Taken: one launch seam counting all three runners; depth by held completions; `LandedReviews` moved into 1a-i; 1d-ii pages as today, 1h keeps the ids | §4.1, §10.1, §12 |
| 29 | Rows 1d and 1g are too broad | design §12 | Taken: 1d in four, 1g in four, each landing alone with its doors and words; the held reader proved before integration; the lock before any destructive act | §12 |

**Fable's foundation read** (at `92c7b8ed5` and REVIEWLOCK1's branch). Verdict: accept after B1-B3. Three blocking, sixteen not.

| # | Finding | Checked at | Taken or answered | Where |
|---|---|---|---|---|
| B1 | `--no-optional-locks` is not the no-lock rule for a worktree `diff` | REVIEWLOCK1's fix-log entry and `445720582` | Taken: REVIEWLOCK1's rule stated and pointed to; its census widened for `diff.autoRefreshIndex=false` | §0, §1.1, §10.1 |
| B2 | The stamp has no order or racy rule | `LineGuesses.cs:33-37` | Taken: before and after, the racy-time rule, coarse filesystems keep nothing; the held `for-each-ref` case | §5.1, §10.2 |
| B3 | The uncommitted diff reads symbolic `HEAD` while the range reads the id | `WorkingTree.cs:330` | Taken: one head id for both halves, checked after | §5.2 |
| N1 | The hazard is any auto gc in the shared store; the bound is per process | design §4.3 | Taken: `gc.pid` and the pack listing; auto gc from a linked tree measured; four per process | §4.3 |
| N2 | `status` is the long pole; a level of five is two waves | brief's status timings; `ReviewRanges.cs:63-69` | Taken, both: longest first, and the speculative `<recorded>..h` diff | §3.1 |
| N3 | The round census needs held answers | `WorkingTree.Answered`, `Runs` | Taken | §10.1 |
| N4 | The widened ref watch must not feed today's consumers; `MostRoots` is per watch | `DriverWatch.cs:432-438`, `HelpFacts.cs:141-142`, `ChangeWatch.cs:189,201,255` | Taken: a second watch for the reader, a third for trees, each capped | §5.4 |
| N5 | `session-branches.json` names no session | `SessionBranches.cs:19-20`, `TreeFollows.BranchOfTree` | Taken | §5.4 |
| N6 | The merge landing and the Changes tab need one lock | `TreeLands.cs:162-168,264-298` | Taken: `CheckoutLock`, `MergeAsync` among its takers | §7.2 |
| N7 | The person's push needs a grant at the door | `GitAccessRules.cs:40-46` | Taken | §8.3 |
| N8 | Commit runs hooks, which may write files, and auto gc | design §7.2 | Taken: hooks named in the plan, never skipped | §8.1 |
| N9 | History edits are two serial starts per commit; merges | design §7.3 | Taken: the count, progress, a cap; merges refused | §7.3 |
| N10 | "Nothing written into `.git`" is not whole; pruned kept trees | design §1, §13 | Taken | §0, §5.3, §13 |
| N11 | The amendments' targets | `D147.md:58`, built-in git design §3.1, D150 point 3 | Taken: D113 §5 stands; D147's rejection and design §3.1 amended; the switch under Reach; §2.8's bullet amended | §7.2, §13 |
| N12 | The stamp's walk is unbounded | design §5.1 | Taken: 256 loose files, then the folders' times | §5.1 |
| N13 | `IgnoredPaths` is the service's | `IgnoredPaths.cs:92-114` | Taken: the driver's own list, counted, proved | §6.3 |
| N14 | Split 1a; cite the ref transaction knowledge | design §12 | Taken: 1a-i (driver and bench door) before 1a-ii (page); the knowledge cited for every ref write | §8.2, §12 |
| N15 | REVIEWLOCK1 is built; the Bloom filters read; 33 files | REVIEWLOCK1's branch | Taken | §1 |
| N16 | No non-UTF-8 fixture | `WorkingTree.cs:911-912` | Taken: compared at the decoded text, marked *not UTF-8* | §6.2, §10.2 |

## 17. Round 3: Codex's round-2 read, answered for the read half

Checked by Claude Opus at `9900d32f7` with round 2 merged (its code lines unchanged since `fe4809b75` for every file cited), and git's
floors against the release notes and manual pages shipped with Tools' git 2.53 (`RelNotes/*.adoc`, `git.adoc`, `git-cat-file.adoc`,
`gitattributes.adoc`, `git-var.adoc` in its documentation folder), not memory. Every finding held as Codex stated it. Findings 11-16,
and the round-1 write findings Codex's table marks open or wrong, are GITAPP2's open questions (D204, its design §7), not answered
here, as the steward narrowed this round.

**Codex's round-2 findings** (`docs/2026-10-23-git-app-reads.md`, "Codex, round 2", §2-§3). 17 findings, 14 blocking.

| # | Finding | Checked at | Taken or answered | Where |
|---|---|---|---|---|
| R2-1 | The graph cannot generate its claimed paths: displaced base, grown base, the line's guesses, landed and gone | `ReviewRanges.cs:35-79,101-108`; `TreeContext.cs:75-79` (`CheckoutLine` is `DefaultBranchAsync`); `WorkingTree.cs:105-124,160-161`; `LineGuesses.cs:33-48`; `LandedReviews.cs:33-129`; `LandedProofs.cs:75-165`; `TreeGit.cs:16-33` | Taken: K′ (the grown base, speculative beside K), the line derived from R (three starts become none, held equal to `DefaultBranchAsync`), the branch from the `HEAD` file, Σ as a node; the landed and gone subgraphs with their own nodes, depths and ceilings; the paths **generated** from every combination of conditions, depth 2 claimed only for the qualified ordinary case; D202 point 1 says so | §3.1, §10.1 |
| R2-2 | Warm and watched counts omit A and the watch's own starts | design §3.1, §6.3 | Taken: four budgets (graph, context, watch, retry) reported with their total; A in every validation read where S lists an untracked file (depth 2 even with R kept); the watch's E₁ → E₂ counted apart and never on the graph's path; the lost-watch proof with a new untracked file | §3.1, §6.3, §10.2 |
| R2-3 | V is not a final barrier; joining ignores the mutable generation | `trees.ts:81-104` | Taken: Σ and V as nodes, V over every returned component, validating refs, `branch.oid`, the tree's watch generation and the context together; D₀ kept by its key, named the review's only where Σ selects it; mutable reads keyed with the tree, `h` and the generation at start; the page's replaced-read guard kept | §3.1, §4.1, §5.2 |
| R2-4 | The bounded refs fallback keeps an observation it cannot validate | design §5.1; `ChangeWatch.cs:189` | Taken, Codex's first way: **nothing is retained without a valid ref watch**; the 256-file bound and folder-time stand-in withdrawn; an unwatched repository reads R on every use; the nested-ref fixture past the cap | §5.1, §5.4, §10.2 |
| R2-5 | The context omits inputs and has no detection or delivery | `stateHub.ts:25-68` (no `git` scope); `git.ts:262-294` (`staleTime: Infinity`) | Taken: the inputs by category (binary, environment, every configuration file named or candidate, conditional includes, attribute files, shape), read by C (`git var -l`, `config --list --show-origin --show-scope -z`), the id a hash of what was read; heard by the reader's watch and outside roots with a deadline; a `git` scope and a `GIT_CONTEXT` revision in the page's keys; an idle-page test | §5.3, §5.4, §10.1, §12 row 6 |
| R2-6 | An empty attribute tree neither suppresses every driver nor keeps the semantics | `ReviewRanges.cs:84-95`; `gitattributes.adoc:70-77,1116-1128,1162-1170` | Taken: `--attr-source` withdrawn from `merge-tree`; the repository's own semantics kept (as REVIEWBASE3 runs today); `merge-tree` **not started** where the configuration defines any `merge.*.driver` (the only way git runs one) or `merge.renormalize` beside a filter, read from C; the preview qualified *here*; fixtures for committed, `info`, global and default-driver configurations | §1.1, §3.1, §7.1, §10.2 |
| R2-7 | The floors are wrong | `RelNotes/2.41.0.adoc:116`, `2.43.0.adoc:257`, `2.45.0.adoc:120`, `2.42.0.adoc:16`, `2.38.0.adoc:16`, `2.36.0.adoc:75`, `2.26.0.adoc:48`; `git.adoc:183-188,229-232` | Taken: `--attr-source` 2.41 (round 2 said 2.40); `--no-lazy-fetch` 2.45 (round 2 said 2.44), passed as a global option an older git refuses, the variable beside it; each floor checked against C's `git version`; below each, the stated fallback; a fixture that demands a missing blob; stand-in binaries for the lower versions, none being installed here | §5.3, §10.1, §10.2, §15 |
| R2-8 | The held reader's protocol would deadlock or desynchronise | `git-cat-file.adoc:73-79,143-175,294-297,341,362` | Answered by deferring, as the quest offers: no screen in this half needs a held reader; the protocol facts Codex found (header-only `info`, the body only after `contents`, no withdrawal once written, no commands under `--batch`, no diff options on `cat-file`) recorded for its own later design; row 1d-iv withdrawn from D202 | §4.3, §12 |
| R2-9 | Watch exclusions go unsafe when the index changes | design §6.3 | Taken: the tree's own index file watched (a linked tree's under `worktrees/<name>/`); its move drops the answer, publishes `diff`, and revokes every exclusion until E₂ proves it again; force-add-then-edit and index-only fixtures | §6.3, §5.4, §10.2 |
| R2-10 | Queue priority is described as a reservation | `ProcessStarts.cs:37-42` | Taken: one of the eight run places reserved for attended reads, the rest handed attended first among queued reads; round 2's "never queues behind" withdrawn | §4.2, §10.1 |
| R2-17 | Two acceptance expectations contradict the implementation | `GitReads.cs:290-313` | Taken: object reads in two linked trees under the same context are equal and share a kept answer, worktree reads differ; each view held to its own start graph, the commit page two dependent starts | §5.3, §7.1, §10.2, §12 row 9 |
| R2-11 to R2-16 | The checkout lock, ref transactions, rollback, stash drop, write outcomes, stash before resolve | — | **GITAPP2's open questions** (D204), not answered in this round | GITAPP2 §7 |

**The round-1 findings Codex's round-2 table marks open or wrong** (its §1).

| # | Its verdict | Answered by |
|---|---|---|
| Codex 5, dependency depth | open | R2-1: the generated graph |
| Codex 10, read effects and census | open | R2-6: `merge-tree` refused where a driver is defined; R2-8's per-command controls (`blame`, `cat-file`) |
| Codex 11, authoritative timestamp snapshot | open | R2-4: nothing kept without a valid watch |
| Codex 12, context and page caches | open | R2-5, R2-17 |
| Codex 13, partial clone | wrong | R2-7: the 2.45 floor, the option an older git refuses |
| Codex 14, mixed HEADs and joining | open | R2-3: the barrier and the mutable generation |
| Codex 15, ended trees and watch fallback | open | R2-2, R2-9 |
| Codex 16, invalidation and publication | open | R2-5: detection and delivery of the context; R2-9: the index |
| Codex 18, held process | wrong | R2-8: deferred, its protocol facts recorded |
| Codex 28, census and order | open | R2-1-3 and the split rows: one seam counting every start by budget, landing first and alone as 1a-i |
| Fable N12, the stamp's walk | wrong | R2-4: the bound withdrawn, nothing kept without the watch |
| Codex 20, 22, 24 | taken (with R2-11's remainder), wrong, open | GITAPP2's open questions (D204) |

**Kept as Codex's verdict keeps it**: the shared reader, ordinary-case speculation, separate action readiness (the offer after the
diff), exclusive session ownership. Its foundation list for the read half (the complete graph and census, validation and context
delivery, configured-program suppression) is what this round changes; checkout-writer exclusion and transaction and recovery protocols
are GITAPP2's.

## 18. Round 4: Codex's round-3 read, answered

Checked by Claude Opus at `8fbeef346`, each finding at the lines it cites. Every one held as Codex stated it. The steward narrowed the
round: part one (GITAPP1a) answers what an uncached graph of today's reads needs; what keeps or reuses an answer across opens is part
two's, its open questions listed in P2.

| # | Finding | Checked at | Taken, or moved to part two | Where |
|---|---|---|---|---|
| R3-1 | D discards REVIEWBASE3's merged comparison tree | `ReviewBase.cs:10-13` (`Reads => Against ?? From`); `TreesRoutes.cs:77-80`; `ReviewBaseTests.cs:169-187` | **Taken**: D reads `Against ?? From` and answers `Base = From`; the chain step's fixture runs through the graph in the comparison test | P1.1, P1.2, P1.7 |
| R3-2 | The line from the refs observation misreads a dangling origin `HEAD` | `WorkingTree.cs:105-124`; `git-ref-absence.md`'s table | **Taken**: the derivation withdrawn; git's three questions kept, asked together, chosen in today's order; a dangling-`HEAD` fixture compared | P1.2, P1.7 |
| R3-3 | Ceilings do not follow from the graph | `ReviewRanges.cs:63-79,101-108` | **Taken for the cold path**: every count generated from the graph's data, the table provisional and corrected by the generator; warm ceilings are part two's | P1.4; P2 |
| R3-4 | Large `packed-refs` keep the validation hole | `ChangeWatch.cs` (the 1 MiB bound) | **Part two**: part one keeps no observation | P2 |
| R3-5 | C has no stable-construction barrier | design §5.3; `LineGuesses.cs:33-37` | **Part two**: part one reads no context | P2 |
| R3-6 | "Not kept" does not reach the frontend fallback | `Web/src/bridge/repositories/git.ts:262-310`; `GitReads.cs:465-478` | **Part two**: part one changes no screen's keeping | P2 |
| R3-7 | Retained reviews omit Daoris's own inputs | `TreeContext.cs:67-75`; `SessionBranches.cs:97-105`; `LinesRoutes.cs:28-50`; `stateHub.ts:40-41` | **Part two**: part one retains no review; each open reads the line choice and the grown record afresh, as today | P2 |
| R3-8 | Page-close cancellation has no route to the process's owner | `Web/src/bridge/call.ts:10-14`; `trees.ts:32-35,72-76`; `RouteFlights.cs:15-17`; `WorkingTree.cs:1063-1070` | **Taken**: a `read` id per call, `SESSION_DIFF` joined by session, `READ_LEAVE`, the last waiter stopping the flight and the runner killing git; the page's last waiter after a grace; proved through the real route boundary | P1.6, P1.7, row 1a-iii |
| R3-9 | The first row cannot add the effect controls and keep today's answers | `WorkingTree.cs:459-476` (no `--no-textconv`) | **Taken**: 1a-i counts and classifies and adds no flag; the effect policy is part two's | P1.5, P1.8; P2 |
| R3-10 | The graph row needs the untracked display's proof | `WorkingTree.cs:341-365` | **Taken**: an interim untracked window of today's per-file reads, the budget folded in order, its own generated ceiling; the display addition is part two's | P1.3, P1.4; P2 |
| R3-11 | The binary's identity omits replacement in place | `WorkingTree.cs:933-946` (`GitInUse` stamps the file) | **Part two** (non-blocking): part one keeps nothing a binary decides | P2 |

**Codex's round-3 table of earlier findings.** Of those it marks open: R2-1 is met for the cold path by P1.2's nodes (the grown base,
the line's three questions, the landed and gone subgraphs) and P1.4's generator, its line derivation withdrawn (R3-2); R2-2, R2-5 and
R1 Codex 11, 12 and 16 are part two's; R1 Codex 5 and 28 are met for the cold path by P1.4 and P1.5. Those it marks taken stay in
part two's text as round 3 wrote them; those it marks deferred (R2-8, R1 Codex 18) stay deferred, R1 Codex 18's page cancellation now
P1.6. **Kept as Codex's verdict keeps it**: the read/write split (D204), the corrected version floors (part two's), the held reader
deferred.

## 19. Round 5: Codex's round-4 read, answered

Checked by Claude Opus at `49948558a`, each finding at the lines it cites and at the lines below. Every one held as Codex stated it.
All five are part one's; part two and GITAPP2 are untouched. **Kept as Codex's verdict keeps it**: the narrower scope, the merged
comparison (R3-1), git's own line guess (R3-2), the first row's unchanged flags (R3-9) and the separation from retained answers.
Paths are as in part one; the kit's are in its repository at its tag `v0.22.0`, the version `src/Daoris.Desktop/Directory.Packages.props:26-31`
pins.

| # | Finding | Checked at | Answer | Where |
|---|---|---|---|---|
| R4-1 | Identical commands do not make program-capable reads independent | `WorkingTree.cs:331,385-392,466-480` (W, Uᵢ and D through `RangeAsync`: `--no-ext-diff` only, textconv and filters left on); `ReviewRanges.cs:92` (M); `TreeWorkReads.cs:77`, `LineHolds.cs:98,142,231,286` (the offer); a probe on Tools' git 2.53 in which S ran the clean filter, W and each Uᵢ the clean filter and textconv, D textconv, and T, P, K, E, B, N, I, G and H no program | **Taken**: Π, the program-capable reads (W, S, Uᵢ, M, D, the landed review's changes, the offer), run as one chain in today's order, one at a time, and are never speculative; round 4's D₀ is withdrawn and the offer follows D and H; only ref, ancestry and count reads run in parallel or ahead. Why equality is not enough: a configured program keeps state git does not see, so order, concurrency and an unneeded call can each change a later answer | P1.1, P1.2, P1.4's table; P1.7's order-sensitive textconv fixture; P2 |
| R4-2 | The untracked window does not imply at most three wasted starts | `WorkingTree.cs:331-332,334-338` (W and S before any untracked read), `:341` (the budget starts at W's patches), `:353-357,360-361` (the fold) | **Taken**: the window opens only after W and S have answered, holds at most w files started and not yet folded, gives a slot back only as the ordered fold advances, and stops at the budget, on a failure or a cancellation; waste is at most w − 1 and zero where W spent the budget. **Part one's w is 1**, since each Uᵢ is Π (R4-1): today's loop exactly, no untracked start wasted; w > 1 is part two's | P1.3, P1.4; P1.7's held cases |
| R4-3 | The equivalence proof omits route branches that decide the answer | `TreesRoutes.cs:62,66,70,72,77-81,83-85,88,91-94,100-104,109-113,116,120`; `ReviewRanges.cs:37,42-46,55-56,66-79,103-108,114-115,124-127`; `WorkingTree.cs:89-90,300,302,328,332,338,484`; `TreeFolders.cs:13-25` | **Taken**: the comparison runs through the route with only its reads swapped, so its guards and fallbacks are its own around the graph; eleven branches listed one by one, each with its deciding line and a fixture, the fallbacks among them (a tree outside this home, a blank base, W, S, D or H failing, the landed and dirty-only paths, dirty work with no files, the refusal); outcomes compared as answers **or refusals**; the offer is Π and follows D's success, so it is never speculative; H's failure surfaced only where the chain asks it | P1.1, P1.2, P1.4, P1.7 |
| R4-4 | Closing one window is treated as host shutdown | `MainWindows.cs:4-12,64-82,88-99`; `MainForm.cs:204-211`; `SecondaryWindowHost.cs:154-166`; `RouteFlights.cs:32-56,93-103`; `DriverModule.cs:126-131,172-175`; the kit: `ChromiumView.cs:139-143`, `ChromiumChildBrowser.cs:165-171,232-235,297-301`, `ChromiumIpcBridge.cs:72-80,166-173`, `IpcHostBridge.cs:42,71-74,133,170-183`, `PageModuleDispatcher.cs:26-28`, `ModuleBase.cs:39,58` | **Taken**: round 4's window-exit bullet withdrawn. Each window's bridge hands its own lifetime token to every dispatch and cancels it when the window's view is destroyed; each `read` id's source is linked to that token, so a destroyed window leaves its reads with no script, another window's wait keeps a shared read, and the first main window hidden keeps its page and its reads. Demonstrated by a test over two of the kit's bridges before it is relied on, and in the installed window | P1.6, P1.7, row 1a-iii |
| R4-5 | The runners do not kill and wait | `ChildProcess.cs:54-92` (`:68,73-74,82,85-86`), `:112-122` (`:118`); `WorkingTree.cs:1069-1070,1104-1159,1165-1166,1172-1176,1198-1248`; `ProcessStarts.cs:114-116,136-147`; `RouteFlights.cs:47-54,64-82,102`; today's tests `ChildProcessTests.cs:62-73,113-124`, `GitStreamEndsTests.cs:39-72` (a marker, not an exit) | **Taken as the second option**: the design now says what the runners do today (kill, observe the output reads, dispose; no exit wait; a failed kill swallowed; a cancelled caller released before the flight's cleanup), and **row 1a-iii adds** the kill-then-await-exit in all three runners, `git.unended` for a failed kill or an unseen exit, and the executor owning every started node until its runner returns, unused speculative nodes stopped and waited for; P1.7 observes each recorded pid's exit for each runner, during creation and while running, and the three milestones in order | P1.6, P1.7, P1.8 row 3 |

**Two corrections of round 4's citations**, found while checking: `LandedReviews.cs:33-129` and `LandedProofs.cs:54-190` named lines
past those files' ends (102 and 187 lines); part one now reads `LandedReviews.cs:33-101` and `LandedProofs.cs:54-187`, and round 3's
text keeps its own as written. The census table's numbers changed with R4-1 (the ordinary path depth 4, not 1; no D₀ starts) and
stay provisional, the generator correcting them.

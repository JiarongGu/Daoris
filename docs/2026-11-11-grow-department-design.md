# Proposed design: a new department or room, opened by a set-up and staffed by quests (GROWDEPT1, D276)

**Standing:** proposed. D276 is reserved for the choice in §9, taken after a second read. No code moves with this document. Read with
`docs/areas/roles-and-supervision.md` (rules 55-58, Ask Daoris), `docs/areas/driving.md` (rules 9, 10, 133), `docs/areas/doctrine-and-cli.md`
(rules 33, 45-52), `docs/areas/workflows-review-landing.md` (rules 3, 5, 42-43), `docs/areas/desktop-app.md` (rules 22, 58, 60), D117
(rooms, a repository set up by its own session), D193 (the departments), D228 (one way) and D253 (Ask Daoris's modes).

## 0. In short

1. **Three answers, in this order of preference:** the department that already owns the work (a quest to it), a room in that department
   (a folder with an `AGENTS.md` of its own) where the work needs conventions its department does not, and only then a new department.
   The smallest structure that gives the work a home of its own wins; the person's own words ("a repository for X") skip the table (§2).
2. **The turn's hand ends where the new place would have anything of its own** (the doctrine rule `repository-owns-its-work`: an
   empty repository with no owner is set-up, "until it has anything of its own"). For a repository that is a folder, an empty history, a
   registration, a drive switch and two quests. For a room it is nothing at all: the room lives in a repository, so its whole is a quest to
   that repository (§3).
3. **Two presses, one way each.** `daoris repository create` makes a department; a new part `--room` of `daoris place` (the one door
   of a place's set-up, `PLACE_APPLY`) asks for a room. Each is a person's press: full access runs it at once as the person's own
   (rule 58), restricted mode puts it on a Confirm card, and neither is a grantable family of acts (§4).
4. **Everything with content is a quest to the new place's own session**: its adoption (the existing set-up quest, rule 45-47) and, chained
   behind it, the work the person asked for (§5).
5. **Nothing new in the window's chrome.** The department is a registry row, so it appears where every department does; what is new is one
   receipt line in Ask Daoris's conversation, with go links (§6).
6. **Mostly built.** Registration, driving, the workspace's inherited workflow, the adoption quest and its chain exist; what is new is the
   folder and history's creation, a plan-and-receipt press around it, a room's quest and the table that tells Ask Daoris when to use them (§7).

## 1. What is asked, and what is true today

When a person starts a new repository through Ask Daoris today, the turn does all of it inside itself: it makes the folder, initialises
git, writes the first files, registers the repository and does the work, in one conversation. That meets the need and goes around the
structure that keeps growth under control: no session of the new department did anything, nothing was reviewed or landed by the
workspace's rule, and the person sees a repository with no history of how it began.

What exists to build on, each read in the code or the area documents:

| Piece | What it does now | Where |
|---|---|---|
| `repository import [<folder>] [--workspace <name>]` | registers every subdirectory of a folder as a proposal read from each checkout (`RegistryImport.ProposeAt`): an adopted one by its manifest, any other as present but not adopted; silent about the workspace unless named, so a re-import re-points nothing | `Service/Core/Registry/RegistryImport.cs`, `RepositoryPresses.cs`, route `REGISTRY_IMPORT` |
| `repository add <folder>` | registers a folder that is no git checkout as a `folder` department; a git checkout is refused toward `connect` | `Service/FolderAdd.cs` (driving, first table) |
| `repository wire <repository> --workspace <name>` | moves a registered repository to a workspace, `REGISTRY_REWIRE`, with a view on the Confirm card | `RepositoryPresses.cs`, `Platform/Views` |
| `repository retire` | takes a repository off the registry; its folder is left | `REGISTRY_RETIRE` |
| `daoris connect`, the driver's follow | register an adopted repository from `daoris.json` and `daoris.lanes.json` read on its line (rooms among them, rule 52) | doctrine rule 52 |
| `place <repository>… --preset <id> … --apply` | `PLACE_PLAN` and `PLACE_APPLY`: a delivery, a second opinion, a look, `--adoption` (the set-up quest), `--drive`, receipt-kept, the one adoption door (workflows rule 43) | `Setup/PlacePlan.cs`, `Setup/PlaceApply.cs` |
| `place drive`, `undrive`, `hold`, `resume` | a repository is driven only when the person opted in; a held one starts nothing new (driving rule 9) | `SET_HOLD`, `ActFamilies.Pause` |
| the set-up quest | a repository's own session runs `init --harness agents`, `analyze`, `sync`, then writes the domain, brief, documents, gates and, where none are declared, proposes its rooms; Daoris runs no doctrine command into it | doctrine rules 45-48, D117 point 7 |
| workspace workflow and landing | a repository that names no workflow follows its workspace's whole (workflows rule 3); the landed route resolves on its own (rule 4) | `Workflows/` |
| a room | declared in `daoris.json`'s `rooms` and in the lanes file's `room`; `sync` keeps its `CLAUDE.md` pointer and never writes its text; a declared room with none fails `check` | D117 point 3, doctrine rule 33 |
| quest chains | a quest may carry `then` steps, each published, on behalf of the chain's asker, when the one before closes done | `Service/Quests/QuestChains.cs` |
| Ask Daoris's presses | a proved press of a verb runs at once in full access, on a card in restricted mode; a route with a view judges the press at the ask | roles rules 56, 58 |

What does not exist: nothing creates the folder or its history (the page's add and `import` register a checkout that is already there);
no setting says where a workspace's repositories sit; no verb asks for a room; and nothing ties the person's work to the adoption of the
place it needs, so the work would start on a place with no brief.

## 2. When a request calls for a new department or a room

The question is asked of the registry and the records, never of a classifier: Ask Daoris reads the departments' `domain` (`owns`,
`accepts`) and the knowledge search, and says in its turn which it looked at and why none owns the work. The answers, in order:

| The request | Answer | Why |
|---|---|---|
| A registered department, in this or another workspace, owns the work (its `owns` or `accepts` covers it, or its records show the same work done) | **That department**: an ask, which the intake turns into quests (D65) | Orchestration is central, ownership is local; Ask Daoris reaches every workspace |
| The work is a new part of a department's code, built, tested and released with it, and the conventions are the department's own | **That department** | A new folder in it is not a new place |
| The same, but the part needs instructions, gates or a claim group of its own (its own `AGENTS.md`, its own checks) | **A room** in that department | D117: a room is how an area gets its own conventions without a second history |
| The work is its own deliverable: it builds, tests or releases apart, has its own history, audience or access rules, and no department owns it | **A new repository** (kind `git`) | The unit the company grows by (D193 point 3) |
| The work is files that want no history or branches (documents, assets) | **A folder department** (`repository create --kind folder`) | The kind exists (`PlaceKind`); the same press says it |
| A system reached only through a plugin's console | **Not this design** | A system's adapter is the plugin's (`repository add --system`) |
| The request only names a thing, asks for a plan or an answer, or is a throwaway | **Nothing is created** | Scratch is a repository's `_fixtures` or a gitignored folder; a plan is words |

Where two rows could hold, the earlier one wins: an existing department over a room, a room over a department. The person's explicit
words win over the table ("make me a new repository for this"), with the table's refusals still applying. The choice is Ask Daoris's
judgment, shown as words in the turn; the press's refusals are mechanical (§4). A person is asked only what is theirs: a name where the
request gives none that fits, a workspace where the work spans two, a folder where the workspace has no repositories to infer one from.

Who may open one: Ask Daoris in either mode, and a working session through the same press, its tier deciding as for every press (rule 56).
An intake never creates; where no department owns an ask it says so, and that gap is a row (§7, f), not a creation.

## 3. What the turn does itself, and what it hands on

**The line** is the doctrine's: initialising a repository that has no owner yet is set-up, until it has anything of its own. So the press
does exactly the part that makes the place addressable, and stops before any content.

*The press's own hand* (one act, planned first, applied in this order, each step either undone by the next press or harmless to repeat):

1. **Folder.** Created under the folder its workspace's repositories sit in: the parent most of the workspace's registered git roots share
   (the one holding the newest if tied), read from the registry on this machine, never stored. With no registered root to read it from
   the press asks for the folder (`--under <folder>`); a folder that exists and is not empty is refused.
2. **History.** `git init` on the workspace's line name (the one most of its repositories land on, else the harness's default), one empty
   commit as the git identity the workspace's other repositories use (`git use`'s answer; none means refused, naming that verb). No remote,
   no push: publishing is the owner's call, and a remote is a later press.
3. **Registration.** The registry row for a git place at that root, in the named workspace, unadopted (the shape `ProposeAt` gives a
   checkout with no manifest). The driver's follow reads the root; the place now answers quests (driving rule 133).
4. **Driving.** Drive on, hold off. It inherits its workspace's workflow and landing by writing nothing (workflows rule 3). Where the
   inherited workflow holds a step that needs a remote (`push`, a plugin's `pull-request`) and the new place has none, the drive stays
   off and the receipt says which step and what the person's next press is (`place <repository> --preset <delivery> --apply`).
5. **Quests.** The set-up quest, and the work's quest chained behind it (§5).
6. **Receipt**, kept as `PLACE_APPLY`'s is: what was made, the commit it made, the quests, where it stopped if it did.

A re-press on a place whose folder holds exactly the empty commit the receipt names resumes at the step after the last done; any other
existing folder is refused. A failure leaves the receipt and no undoing: `repository retire` takes the row off, and the folder is the
person's to remove.

*What it hands on, as quests to the new place's own session:* the adoption (`init --harness agents`, `analyze`, `sync`, the domain, the
brief, the documents, the safe work, the checks, and its rooms; doctrine rules 45-47), then the work. Everything that is the department's
text, its first code included, is its session's, reviewed and landed by the workspace's rule. The turn writes no `daoris.json`, no brief,
no code.

*A room is the other case.* A room is a folder inside a repository whose `AGENTS.md` and declaration are that repository's files, and
Daoris never writes a room's text (D117 point 3). So the press has no hand of its own: it checks that the repository is registered and
adopted, that the path stays inside it and no room is there, and publishes one quest to that repository, carrying the playbook (the
canon's `rooms-and-classes`: create the folder with its own `AGENTS.md`, declare it in `rooms` and the lane's `room`, run `sync` and
`check`, keep it to the area's own conventions) and the person's reason. The repository's session does the rest on its branch.

## 4. The two presses, their views and the grant

**`daoris repository create <name> [--kind git|folder] [--workspace <name>] [--under <folder>] [--line <branch>] --purpose "…" [--work "…"]`**
(route `REGISTRY_CREATE`, a person's press, `--plan` shows what it would do and writes nothing). `--purpose` is the one line that goes
into the set-up quest's body and the empty commit's message; `--work` is the person's request, carried to the chain's second step.

**`daoris place <repository> --room <path> --purpose "…" --apply`**, a part of `PLACE_APPLY` beside `--adoption` and `--drive`
(`PlaceAsk` gains `Room`), so a place's set-up stays one door (workflows rule 43).

Each has a view of its own (`PressViews`, one unit per route, rule 56), judging at the ask with the route's own code and answering a
press it would refuse to the terminal in those words, with no card. Refused: a name already registered in any workspace (a quest is kept
under its receiver's registered spelling, so a name that differs by case is the same), a folder that exists and is not this receipt's, a
workspace that does not exist or has no repository to infer a folder from, an unusable line name, no git identity, `git` absent, and for a
room a path outside the repository, an existing room, a repository not registered, held or not adopted. The card names what the press
means and the command, then every value: the name, workspace, kind, the full folder, the line, the identity, that no remote is made,
the steps in order, and the first lines of each quest it publishes.

**Modes.** Both run the same route (rule 56). In full access a proved press runs at once with no card, said once on the conversation's
console, logged `help.pressed`, kept in the press record and listed in *Notifications* (rule 58); the second check selects it like any
press that ran with no card, so a creation is read afterwards with no new row in its table. In restricted mode the press waits on a card
naming Ask Daoris and runs on the person's yes.

**Not a family of acts.** `ActFamilies` lets the person grant restricted mode a set of presses that then run with no card (`pause`,
`clean-up`, `discard`, `settings`). Opening a department is not in that set and this design does not add it: it widens the company's
reach, which is the person's to say yes to at each use, and a grant would make restricted mode behave as full access for it. The two
presses are a *family* only in being one design with one rule (card in restricted, unasked in full); a person who later wants the grant
adds one family with these two routes, a one-line change to `ActFamilies`.

## 5. The work follows the set-up

Work sent to a place that has no brief starts without the repository's own instructions, so the work is chained behind the adoption:

- The press publishes the set-up quest (the one `place --adoption` publishes, built once and reused, so the playbook has one source) with
  one `then` step, the person's request as a quest to the same repository, asked on behalf of the same asker. The adoption closes done
  and the chain publishes the work; a decline stops the chain, and the person sees both quests in Quests with the chain named.
- The work is one quest, not an ask: the new place is the only department that owns it, and its session splits it as its own work.
  Where the request also touches other departments, the turn makes an ask for those as today (the intake routes it), and the chain
  carries only the new place's share.
- A room's quest has no chain.

The chain's one limit, found in `QuestChains.Judge`: every step is asked on behalf of the chain's asker and a step may not ask the
asker itself. Ask Daoris is the asker (`daoris:help`, no registry row, rule 55) and the new place is another, so the chain is fit; a
session pressing the press has its own repository as asker and the new place as receiver, also fit.

## 6. How the new department appears in the window

No place and no control is added (desktop rule 22: nine places, a tenth needs its own decision). It appears as every department does:

- **Repositories** lists it under its workspace; its row's second line reads *Adoption running* while the set-up quest is open, and
  Agents → Setup shows the same need (desktop rule 58). The Map draws it from the registry. Its page is the repository page, with
  Details, Branches, History and the rest (rule 60); a `folder` department's is the folder page.
- **Quests** holds the two quests as one chain; **Sessions** and *At work* show its session once it starts.
- **Ask Daoris** says the receipt once: one folded line in the conversation, *Opened `<name>` in `<workspace>`: folder, line, the quests*,
  with go links to the repository's page and each quest (the links Ask Daoris already writes, rule 55); in *Notifications* the same line
  under the press record, the asked apart under *Asked you*. A room's receipt is the quest's line alone.
- **What needs you** gains only what is the person's: a landing of the set-up that waits on their *Accept*, or the remote the drive needed.
- **Words.** The card, the receipt and the refusals are chrome, so each goes through the catalogue in English and Chinese (D222). The
  glossary already says *department* (部门) for any registered place; it gains *room* (房间), meaning a repository's folder with
  instructions of its own, apart from Ask Daoris's own room.

## 7. What is new, and rows in order

New: the folder and history's creation; the plan-and-receipt press around it (`RepositoryCreate`, mirroring `PlacePlan` and `PlaceApply`);
the derived folder; the room part of `PLACE_APPLY`; the two views; the chain's composition; the words; and the table of §2 in Ask Daoris's
room. Everything else is reused, so nothing is said twice (D228).

| Row | Piece | Proof |
|---|---|---|
| a | `REGISTRY_CREATE`: plan, apply, receipt and resume (§3), every refusal (§4), the terminal verb, fixtures with a real `git init` | tests seen failing first; a real-process run of the verb in a temp-free scratch folder |
| b | The set-up quest built once for `place --adoption` and `repository create`; the `then` step carrying the work | a chain publishes its second step when the first closes done, and stops on a decline |
| c | `--room` on `PLACE_APPLY`, its view and its quest's playbook | refusals; the quest names the canon's convention and the repository's own declaration |
| d | The views on the Confirm card, the receipt line and *Notifications* row, the catalogue words, the glossary's *room* | component tests and the card's story; both languages |
| e | The room's table (§2) and the press's verbs in `HelpRoomMayDo` and `HelpRoomVerbs`; the coverage table (`HelpCoverageTests`) names both | a conversation asked for a new repository creates one in each mode |
| f | The intake says *no department owns this* and parks the ask for Ask Daoris instead of routing it wrongly | an ask no department owns is held with that sentence |
| g | The installed proof: full access opens a department from words and its first quest lands; restricted shows the card; a room asked of an adopted repository | shots in both languages; the second check reads the full-access creation |

Two facts to confirm in row b before the chain is relied on, since this design read them in the area documents and `QuestChains` only:
that `place --adoption`'s quest is built where a `then` step can be attached, and that a chain's second step to a still-unadopted
place passes the planner (driving rule 9, "adoption where the door needs it").

## 8. Rejected

- **The turn does set-up and adoption itself (today).** It is why this exists: no session, no review, no landing by the workspace's rule,
  and a repository's text written by a conversation rather than by its department (doctrine rule 45, D117 point 7).
- **The press also writes `daoris.json`, the brief and the first files.** It would make the turn the department's author. The line is
  what the doctrine already draws: no content without an owner, and the owner is the new place's session.
- **An `ActFamilies` grant for creation.** Restricted mode would run it unasked; see §4.
- **A stored "workspace folder" setting.** A second source for a fact the registry's roots already say; it would drift from them.
- **A room verb that writes `AGENTS.md`.** D117 point 3: Daoris never writes a room's text, and a room's declaration is the repository's
  own file.
- **Room creation as a verb of its own beside `place`.** A place's set-up is one door (workflows rule 43); a second door for one job is the
  defect D228 names.
- **The intake creates departments.** It routes asks to the departments that own them; creating one is an act with a card, and an intake
  has none.
- **The work as an ask the intake splits.** There is one owner; an intake pass would only restate the request.
- **A template repository per kind of department.** It would hold what the adoption session writes from the department's own needs.
- **Deleting a created folder on failure.** A delete is the person's pause; the receipt and `repository retire` are enough.

## 9. The choice D276 takes

D276 settles, after a second read: the order of §2 (existing, room, new, and the person's words ahead of it); the line of §3 (the press's
hand ends before content, the room's is empty); the two presses as the one growth door with no grantable family (§4); and the work
chained behind the adoption (§5). A cross-check before it settles: the second read should try the set-up quest's chain against
`QuestChains` and `PlacePlan` in code, since this document read them in the area documents and the chain's file alone.

## 10. Open, and what to measure first

- Whether a workspace's repositories commonly share one parent. Where they do not (a repository elsewhere on the machine), the derived
  folder asks for `--under` more often than it should; count it on a machine that has several workspaces before row a is built.
- The set-up quest on a repository with one empty commit: `analyze` and the index read on an unborn tree have been exercised only on
  repositories with files; row a's real-process run says whether a seed file is needed.
- Whether the person wants the creation grant after living with the card (§4); the answer is one family, not a redesign.

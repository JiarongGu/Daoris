# How work moves: a workflow per repository and kind of task, drawn as a flowchart

> WORKFLOW1, decided as D157 (2026-10-09). Nothing is built. `Dnn` is `docs/decisions/Dnn.md`. The owner, 2026-10-09:
> *"when doing work with Daoris we should be able to set up a workflow for each repo or for a task type… presented as a
> flowchart, so we can define what work is highly co-worked with a human and what can be fully automated; this includes
> how to raise changes, merge, even CI/CD (which includes multiple different services and steps); this is more like
> LangGraph or n8n, more like workflow management… co-work with Codex for a good UI/UX design, and this should be set
> up with Ask Daoris"*. A read-only UI/UX proposal from Codex was taken before this was written; §11 weighs it point by
> point, as the second-agent design's §11 weighed the last one.

## 0. The case

**What Daoris does today**, read at `6e465e1f`. How a repository's work moves is a set of separate rules, each with its
own key, its own doors and its own Setup row, and none of them drawn:

| What | Decided by | Where it is set | What it decides | Built |
|---|---|---|---|---|
| The work | D46, D65, D82, D149 | the ask, and the chain's `then` | which repositories, which steps, in what order | Built |
| The quest's own holds | D133, D144 | the person's words; a requirement's evidence | a done that departs from their words, or lacks its evidence, waits for their yes | Built (EVID1c–d not) |
| A second opinion | D155 | `opinions`, `workspaceOpinions` | another maker's agent reads the work before it lands | Declared, chosen, the pass and its delivery; nothing holds a landing (XAGENT1f) |
| A look where it runs | D154 | `reviews`, `workspaceReviews` | the person's *reviewed* before it lands | The gate and the verdict; the intake composes no set-up step (REVIEWENV1f), and nothing shows it in Daoris's browser (1d) |
| How work lands | D87, D100, D145 | `landings`, `workspaceLandings` | merge or branch; a plugin pushes and opens the pull request; *Accept automatically* | Built |
| Go-aheads | D135 | asked by a session, held on the ask | the person's yes for an act outside the repository | Built |
| Plugin hooks | D64, D148 | the plugins switched on | a start held (`quest/consider`), the push (`work/land`), a landed branch's pull request (`work/state`) | Built |

They compose in one place only: the landing gate's fixed order (D155 §7: the quest's holds, the second opinion, the
person's look, the landing), written as each gate's own check. Only the review and the opinion have a task level (D154
§1.4, D155 §2.4). After the landing Daoris knows nothing: the pull request's state is read only to clean branches up
(D148 point 2), and no check, pipeline or deploy is anything Daoris reads or starts.

So four things are missing:

1. **One picture.** Nothing shows how work in a repository moves from the agent's work to its last stage, who acts at
   each step, and what is automatic.
2. **A difference by kind of work.** A repository's rules are the same for every piece of work. *Documentation goes
   straight to a pull request, a feature is looked at first* has no home but the person's skip on each piece.
3. **Anything after the pull request.** The checks that run on it, its merge, the build after it and a deploy to
   staging or production are the person's to follow by hand.
4. **A change said as a change.** Ask Daoris proposes each rule on its own card (D110), so a change that removes the
   person's look reads as a field, not as *you no longer see this work run before it lands*.

This design adds, each off until the person sets it: one model of how work moves (§2), the steps it is made of and what
each needs from the runtime (§3), a choice of workflow by workspace, repository, kind of task and the task (§4), a run
that keeps where each piece of work stands (§5), an editor on the repository's and the workspace's pages (§6), the run
drawn beside the session (§7), and Ask Daoris proposing a change as a drawn diff of the person's part (§8). With nothing
set, every gate and every landing is what it is today, and the new tab draws today's rules (§2.8).

## 1. Words

- **A workflow** is how a repository's work moves from the agent's work to its last stage: which steps, in what order,
  who takes part in each, what is automatic, and what Daoris reads to know each step is done. It is named and
  versioned.
- **A step** is one of a fixed set of **kinds** (§3.2), each one the runtime carries. A step's id stays the same across
  versions, so a change is drawn as a diff.
- **Participation** is the person's part in a step: none, a checkpoint, or the act itself. **The executor** is who does
  the step: an agent, Daoris, a plugin speaking for a service, or the person. They are said apart (§3.1).
- **A run** is one piece of work following one workflow: a chain's work in one repository, on the machine that drives
  it, which is the unit D154's and D155's gates already read.
- **A kind of task** is a name a workspace gives a sort of work, *Documentation* or *Feature*, which chooses a workflow
  (§4.2).
- **Current** is the workflow Daoris derives from the rules as they are set today (§2.7). It is the bottom of every
  choice.

**The word moves from D65.** D65 point 4 called a chain of quests a workflow. The chain stays what it is, the work's own
steps, and here it is one step of a workflow, *the work*. A workflow is the process around the work. D65's other half
stands: no script engine and no coordinator agent (D1). The owner's *LangGraph or n8n* is taken for the drawn, editable
graph and the run's place on it, never for an engine that runs nodes it was handed (§2.1).

The names in each language are the glossary's (D116, built in WORKFLOW1b). Two are taken in 中文: 工作流转 is *How this
work ran*, the chain's story, and 运行 is a process running. The design proposes 工作流 for a workflow and 工作类型 for a
kind of task, and leaves the run's word to the glossary's row.

## 2. The model

### 2.1 What a workflow is, and is not

A workflow is **data the driver reads**, as a landing rule is: a list of steps of fixed kinds, each with the fields its
kind takes. The driver does what each kind does and nothing else. No step runs a script, evaluates an expression, or
calls a service except through a plugin the person installed and switched on (D64). That is what keeps D65's *no engine*
true while the graph is the person's to draw.

**A workflow never composes the agent's work.** Which repositories an ask reaches and which steps its chain has stay the
intake's and the person's (D65). A workflow says what happens around that work: what reads it, who looks at it, how it
lands, and what follows.

### 2.2 Before the landing, fixed; after it, composed

**Before the landing the order is the runtime's.** D155 §7 made one gate in one order: the quest's own holds, the second
opinion, the person's look, then the landing. A workflow chooses which of those gates are on and how each is set, and
never reorders them. The opinion is read first so that the person looks once, at work that already answered it; the look
comes before the landing so that nothing lands unseen. The editor offers no move that breaks the order (§6.3).

**After the landing the order is the person's.** The pull request, the checks on it, its merge, the stages after it and
the go-aheads between them are a sequence the person composes from the kinds of §3.2, with branches on an outcome (§3.3).
This is where the owner's *CI/CD (which includes multiple different services and steps)* lives.

### 2.3 What a workflow says, and what the repository declares

Each rule today mixes two sorts of field. Some say **what the repository is on this machine**: where its app runs and how
it is shown, which agents may read it, which plugin speaks for its platform, its branch pattern. Others say **how its
work proceeds**: whether a look is required, whether a second opinion holds the landing, whether a done lands with no
press. A workflow takes the second sort. The first stays where it is set today, on the repository's Setup and its
workspace's, because a workflow chosen for a whole workspace cannot hold each repository's address.

| Rule | Stays a declaration (Setup) | The workflow's, where a named one governs |
|---|---|---|
| How work lands (D87, D145) | `pattern`, `plugin`, `tidy` | `form`, `autoAccept` (the landing step's participation) |
| Review before landing (D154) | `environments`; a repository's `false` | `required` (the look step's presence), which environment |
| Second opinion (D155) | `reviewers`, `verify`, `minutes` | `on`, `required`, `recheck`, which of the declared reviewers |
| The line, the standing answer, reading and writing across | all | none |

**A step chooses among what is declared, never beyond it.** D155 §2.4 set that rule for the task level, and it holds here
for the same reason: which agents may read a repository, whether they may run its commands, and how its app is shown are
the repository's standing facts. A look step names one of the repository's environments, or takes its default. An
opinion step names reviewers from the rule's list, or takes its order. A landing step may name a pattern and a plugin,
and where it names none it reads the repository's rule, then the workspace's. A workflow that needs a declaration a
repository lacks is refused where it is chosen for that repository, naming the Setup row that would declare it (§4.7),
as D154 refuses `on` for a repository that declares no environment.

### 2.4 Where it lives

Five homes were weighed.

- **`driver.json`, beside the landing, review and opinion rules.** Chosen for **the choice** of workflow (§4.1): which
  workflow a workspace, a repository and each kind follow. The reasons are D154 §1.2's: the driver resolves the landing
  and reads the chain, the choice is the person's process on this machine, and both doors already edit the file (D50).
- **A file of its own under the home**, `<home>/workflows/<id>.json`. Chosen for **the workflows themselves**. A workflow
  is a graph with every version a run may still name (§2.6), while `driver.json` is rewritten whole by both twins at every
  change of any setting: a history there would ride every edit of a line or a language. One file per workflow is also
  the unit a person carries to another machine (`export`, `import`, §4.7), the file being the API (D50). It is the home's
  (D63), written atomically, BOM-less, and machine-local (D46 §6).
- **The manifest** (D34, D122). Rejected, for D154 §1.2's reasons: it is tracked, so it binds every clone and every
  contributor who does not run Daoris; an unadopted repository has none; and whether the person looks before work lands
  is the person's question, not what the repository is.
- **The registry** (WSP1, WSP2). Rejected. It is the authority on which repositories are in a workspace and where their
  checkouts are: wiring, which `connect` and `import` rewrite. A workspace's choice names the workspace by name, as
  `workspaceLandings` does, and the registry still decides which repositories that reaches.
- **The service's store, or the remote.** Rejected for now. A workflow names this machine's plugins, accounts and
  environments, and its steps act through this machine's sign-ins. A teammate's machine follows its own person's process,
  and the pull request is where a team reads the work (D145). Sharing one is left out (§12.2).

### 2.5 The shape

A workflow, `<home>/workflows/docs-to-pr.json`:

```json
{
  "id": "docs-to-pr",
  "name": "Documentation to a pull request",
  "versions": [
    {
      "version": 2,
      "at": "2026-10-09T09:12:00Z",
      "door": "ask-daoris:p-3f1a",
      "steps": [
        { "id": "work", "kind": "work" },
        { "id": "opinion", "kind": "opinion" },
        { "id": "land", "kind": "landing", "form": "branch", "accept": "automatic" },
        { "id": "pr", "kind": "pull-request" },
        { "id": "checks", "kind": "check", "plugin": "azure-devops-pull-request", "checks": "required",
          "failed": "send-back" }
      ]
    }
  ]
}
```

The choice, in `driver.json`:

```json
"workspaceWorkflows": {
  "work": {
    "default": "feature-local-review",
    "kinds": {
      "docs": { "label": "Documentation", "paths": ["docs/**", "**/*.md"], "workflow": "docs-to-pr" },
      "feature": { "label": "Feature" }
    }
  }
},
"workflows": {
  "deploy-config": { "default": "release-with-stages" },
  "notes-site": { "default": "current" }
}
```

- **`id`**: lower-case letters, digits and dashes, at most 40, unique on the machine, and the file's name. **`name`**: the
  person's, at most 60 characters, in their own language (content, D142).
- **`steps`**, in order. The first is `work`, and there is exactly one `landing`. Before it, at most one `opinion` and one
  `look`, in that order (§2.2). After it, `pull-request`, `check`, `stage`, `go-ahead` and `all`, a group (§3.3). A
  `go-ahead` may also sit with the work (§3.6). At most 24 steps and one level of grouping.
- **A step's `id`**: as the workflow's, unique in it, and kept across versions. A new step takes a new id, and an id is
  never reused for another kind, so a diff matches steps by id (§8.2).
- **The fields a kind takes** are §3.2's. A field a kind does not take, a value it does not know, or a kind this build does
  not run makes that version unreadable, said with its step's id. The file keeps it, and it is not offered (§3.9).
- **`door`**: which door saved the version: `screen`, `terminal`, or `ask-daoris:<proposal>`. It never says *the person*,
  since a terminal's edit cannot be told from an agent's (D156 §4.1).
- **No summary is typed.** The line a workflow is shown by (*opens a pull request with no press; you merge it*) is
  composed from its steps, so it cannot drift from them (`claims-need-checks`).
- **In `driver.json`**, `workspaceWorkflows` by workspace and `workflows` by repository, each with a `default` and
  `kinds`, a kind mapping to a workflow's id, to `current`, or to nothing (the level's default). A workspace's `kinds` also
  declare the kind (§4.2). `current` names Current, so a repository can keep a workspace's workflow from reaching it.

### 2.6 Versions

- **A version is whole and never edited.** Saving makes the next number. A choice names a workflow, and work takes its
  newest version when it starts.
- **A run keeps its version** (§5.1). Editing a workflow changes new work only, and a run's view says which version it
  follows. Moving work already running onto a newer version is left out (§12.2): the person's presses at each gate
  (*Skip review for this work*, *Go on anyway…*) are how running work is changed today, and they stay.
- **A version a kept run names is kept.** Beyond those, a workflow keeps its newest 20.
- **Current is not versioned.** It is the rules as they stand, read at each gate as today (D145's look reads the rule as
  it stands). A run under Current keeps the graph it drew at its start, for the trace, and says it is read live.

### 2.7 Current, and the first presets: a derived view first, never a migration

**Current is derived.** One pure function on each twin reads a repository's rules and draws its workflow: the plugins that
may hold a start, the work with its holds, the second opinion where its rule says `landing`, the look where its rule is
`required`, the landing as its rule says, and the pull request where a plugin opens one. Each step carries **its source**
(*this repository's rule*, *the workspace's*, *Daoris's default*) and **its limit** where the runtime has one (*declared;
nothing holds a landing on it yet*, for the opinion until XAGENT1f). Its version is a digest of what it drew. The twins
are `DriverConfig.cs` and `driverconfig.ts`, held to one table of rules in and steps out (`twins.md` gains the row).

**Why derived first.** Every door, both twins, Ask Daoris's room and the Setup rows read the rules' keys today. A migration
would rewrite the person's file; an older build would then read none of it; and with nothing set, Daoris must be exactly
what it is today (D154 §1.8, D155 §2.6). A view over the keys changes none of that, and it is the first thing the owner
asked for: one picture.

**Named workflows sit beside it.** Where a named workflow is chosen (§4.1), its version is the authority for that work's
process, and the keys stay the authority for their declarations (§2.3) and for every piece of work no named workflow is
chosen for. There is one authority for each piece of work, and its run says which.

**The presets are built in**, a table both twins read, as the tools list is (D121), and nothing is stored until one is
saved:

| Preset | Steps | Its line |
|---|---|---|
| *Merge after you accept* | work; landing (merge, you) | *You accept each piece of work, and it merges into the line.* |
| *A branch for you to push* | work; landing (branch, you, no plugin) | *You accept each piece of work onto a branch, and push it yourself.* |
| *A pull request after you accept* | work; landing (branch, you); pull request | *You accept; the plugin pushes and opens a pull request; you merge it.* |
| *A pull request with no press* | work; landing (branch, automatic); pull request | *A pull request opens once its quest is done; you merge it.* |

Each is offered *with a second opinion* and *with your look first*, which add those steps and read what the repository
declares.

***Save as a workflow…*** copies Current into a new workflow's first version, draws the two side by side, and saves on the
person's press. The keys are not touched. Choosing it for the repository is a second, separate press, which says what
changes (§4.7).

### 2.8 Nothing set anywhere

No workflow file and no choice in `driver.json`: every repository follows Current, which is today's rules, and every
gate reads them as today. The repository's and the workspace's pages gain a *Workflow* tab that draws Current with its
sources, and Ask Daoris's room can say it. Nothing else on any screen changes.

## 3. The steps

### 3.1 Who takes part, and who acts

**Participation and executor are two things**, said apart on every step. Approving a deploy and reviewing a change are
different responsibilities, so no single slider says how automated a workflow is.

| Participation | Shown as | Means |
|---|---|---|
| none, an agent acts | *Agent alone* | An agent's session does it, and the person is told |
| none, Daoris or a service acts | *Automatic · Daoris*, *Automatic · `<service>`* | Daoris's own act (a landing), or a plugin speaking for a service |
| a checkpoint | *Agent + you* | An agent does the work, and one named press of the person's lets it go: *Reviewed* after a look |
| the act | *You* | The person's press is the step: *Accept…*, a go-ahead, a merge on the platform, a production deploy |

*Agent + you* always names its checkpoint, never open-ended watching. A service is never drawn as an agent. A
participation chip is neutral, since it is a declaration (platform-ux §4, *Chips*); only a run's state wears a status hue
(§7).

### 3.2 The kinds, and what each needs from the runtime

| Kind | What the person sees | Participation | What proves it done | Runtime today |
|---|---|---|---|---|
| `work` | The chain's steps in this repository and the agent; *a done that departs from your words or lacks its evidence waits for your yes* | *Agent alone*; it may park to ask, and asks go-aheads as it needs them | Every quest of the chain here done and not held (D133, D144) | **Built** (D46, D65, D82; EVID1a–b) |
| `opinion` | Which agent reads it, *another maker's*; required or not; also between steps | *Agent alone*; a dispute is the person's | D155 §8.2: an opinion covers the tip, answered, no dispute open | **Partial**: declared, chosen, the pass and its delivery built; nothing holds a landing until XAGENT1f |
| `look` | The environment, where it is shown, *Reviewed* and *Not yet* | *Agent + you* | The person's *reviewed* on a set-up whose commit holds the tip, or their skip (D154 point 7) | **Partial**: the gate and the verdict (REVIEWENV1c); the intake composes no set-up step (1f), nothing shows it in Daoris's browser (1d), no review run (1e). *Set it up* and *I set it up myself…* are the floor |
| `landing` | Merge or branch, the pattern, *Accept…* or with no press, which plugin pushes | *You*, or *Automatic · Daoris* | The landing record (D102) | **Built** (D87, D100, D145, LAND2c) |
| `pull-request` | Opened by the landing's plugin; its link; open, merged or abandoned | *Automatic · `<plugin>`* opens it; *You* merge it on the platform | The plugin's `work/state` answer `completed`, its merge commit on the line here (D148 point 4) | **Partial**: opened at the landing; its state asked only at the clean-up's occasions, never while someone waits (§5.4); the GitHub plugin does not answer it yet (PLUGHOOK1b) |
| `go-ahead` | The act in words, where it lands, *Approve* and *Refuse* | *You* | The person's answer (D135) | **Partial**: a session raises one; a run raising one and waiting on it is not built |
| `check` | Which service, which checks, against which commit | *Automatic · `<service>`* | The plugin's answer: each check it names passed at the run's commit (§3.5) | **Not built**: `work/checks`, the point D148 §3.2 held |
| `stage` | One service operation (a pipeline run, a deploy) and its environment | *Automatic · `<service>`*, or *You*; always *You* for production | The plugin's answer for the stage it started (§3.6) | **Not built**: `work/stage` |
| `all` | Checks or stages side by side, joined when all pass | as its members | Every member passed | **Not built** (with `check`) |

The fields each kind takes:

- `work`: none. The agent is the machine's (D23); the chain is the ask's.
- `opinion`: `reviewers` (a subset of the declared list, in the person's order; absent, the list), `required`, `steps`
  (also read between the chain's steps), `recheck`.
- `look`: `environment` (a declared name; absent, the declared default). Its presence is D154's `required`.
- `landing`: `form` (`merge` or `branch`), `accept` (`you` or `automatic`, the second on a branch only, as D145 refuses
  it on a merge), `pattern` and `plugin` (`none` or an id; absent, the declared rule's).
- `pull-request`: none; it needs a landing on a branch whose plugin opens one.
- `check`: `plugin`, `checks` (`required`, the platform's own list, or names), `failed`.
- `stage`: `plugin`, `stage` (an id the plugin declares), `start` (`automatic` or `you`), `failed`.
- `go-ahead`: `act` and `on`, at most 300 characters each (D135's naming), and `refused` (`stop` or `wait`).
- `all`: `steps` (checks and stages), `failed`.

Two more are named so they are not mistaken for missing:

- **The queue** (D115) is a third landing form. Once DEV5–DEV7 land it, `landing` takes `form: queue`. Until then it is
  not offered.
- **Plugins that hold a start** (`quest/consider`) are drawn above the work wherever one listens, from the plugins
  switched on, never as a step a workflow sets: such a plugin listens on the loop for every repository (D148 §1).

### 3.3 Branches, groups and returns

**A branch is an outcome, never an expression.** A step after the landing says what follows its `failed`: `wait` (the
default: the person decides), `send-back` (once, §3.7), or `stop`. A fact Daoris cannot read is **unknown**, which is
neither: an unknown step waits, says what it could not read and who can unblock it, and never takes the passed path or
the failed one. That is D148 point 5's rule, *an unanswered query is not known, never a default*, for every wait.

**The one condition before the landing is a kind's paths** (§4.4), and it only holds; it never chooses a branch by
itself.

**A group**, `all`, runs checks or stages side by side, after the landing only, and is done when every member passed. A
member failed is the group failed, and a member unknown is the group unknown. A race, the first to pass, is not offered.

**Returns are the kinds' own, bounded as today**: the look's *Not yet* goes back to the set-up step's session as its next
turn (D154 §3.4); the opinion reads again once (D155 §6.5); a failed check sends back once (§3.7). No edge is drawn
backwards and no step repeats by itself, so a workflow has no loop.

### 3.4 CI/CD across services: what Daoris coordinates, what the service owns

**The service owns its pipeline**: its definition, its jobs, its retries inside a run, its logs, its environments and
their secrets, and its own triggers (most pipelines start on a push or a merge by themselves). **Daoris coordinates
around it**: which stage follows which, which commit each is bound to, who must say yes before one starts, what it waits
on, what it shows the person when one fails, and what it keeps as the run's record. Daoris never runs a platform's tool
itself (D87, D100, D148): a plugin speaks for each service, as its platform tools are signed in, and Daoris holds no
credential (D148 point 10).

So a pipeline enters a workflow in one of two ways:

- **It starts on its own**, on the pull request or the merge, and the workflow **waits for its verdict** with a `check`
  (§3.5).
- **Daoris asks for it**, a deploy say, and the workflow **asks the plugin to start a stage** (§3.6), then waits for its
  verdict.

Several services are several plugins, each named on its steps: the pull request's checks by the platform's plugin, a
deploy by the platform's or a cloud's, a health check by a monitoring service's.

### 3.5 The outside check: a plugin's word

`work/checks` is a **query**, D148's fourth kind of point. The frame names the repository, the commit, the branch, the
pull request where one is open, and the checks the step names (or `required`, the platform's own list). The answer is a
list of checks, each with its `name`, its `state` (`queued`, `running`, `passed`, `failed`, `cancelled` or `unknown`),
and optionally its `url`, `at` and `message`, at most 300 characters, the service's words shown as written (D142). It is
read by shape, as `work/state` is, and anything else is no answer.

- **Bound to a commit.** A verdict counts only for the commit it names. A newer commit on the work (a fix after *send
  back*, an advance) makes every verdict before it stale, and the step waits on the new tip.
- **One plugin is asked**: the one the step names, installed, switched on, sound and speaking there (D100's four
  reasons), never a waterfall.
- **It is asked only while the run waits on it** (§5.4).

This answers the order D148 §3.2 held the point on: a check before the landing would wait on a pipeline that runs after
it. In a workflow a check is a step after the landing, so it waits on the pull request it was run for, and no landing
waits on it.

### 3.6 Service stages, production and go-aheads

`work/stage` is an **act**. Its frame names the stage, the commit, the repository, the workspace and the run. Its answer is
`{ started, reference?, url?, message? }`, and the stage's progress is then asked by `work/checks` under that reference. A
plugin's manifest gains `stages`, `[{ id, label, environment }]`, so the editor offers stages by name and never takes a
command: Daoris never runs a command it is handed (D154 point 5).

- **Who starts it.** *Automatic*: the run starts it on reaching it, and the step says, as it is set, that this is the
  person's standing say-so for `<plugin>` to start `<stage>` without asking each time, as *Accept automatically* says for
  a push (D145 point 5). *You*: the run waits for the person's *Run `<stage>`*.
- **Production is always the person's press.** A stage whose environment reads as production, by the words
  `GoAheadAct.ReadsAsProduction` already reads (D154 point 1), is only ever *You*, at every door. D37 keeps an
  irreversible outward act the person's, and a standing say-so for one is not offered.
- **A go-ahead step** raises a go-ahead at its place, in D135's words and store: on the ask where the work has one, so
  every session on it is handed the answer, and on the run where it has none. Placed with the work, it is raised when the
  run starts, so an act the agent will need (a configuration written to dev) is answered once and early. Placed before a
  stage, the run waits on it. A refusal stops the run there unless the step says `wait`. A go-ahead directly before a stage
  the person starts is not offered: that press is the go-ahead.

### 3.7 Failure and recovery

| What the step reads | Said | Doors |
|---|---|---|
| A plugin not ready, silent, late or wrong | *not known: `<why>`*, in D148's codes | *Ask again*; the plugin's page |
| `failed` | the service's message as written, its link, the commit | *Send back…*, *Run again* (a stage), *Go on anyway…* (never for production), *Stop the run* |
| `failed`, and the step says `send-back` | *sent back once: `<check>` failed* | the working session's conversation; after it, as `failed` |
| A stage started and unanswered within its bound | *unknown: it may have run* | *Check outcome* first; *Run again* only once it reads failed or not started |
| A go-ahead refused | *you refused `<act>`* | *Change your answer* (D135 replaces it) |
| A step that cannot start | *cannot start: `<why>`* (no plugin, no environment, no window here) | the door that fixes it; the person's own presses (*Skip review…*, *I set it up myself…*) |

- ***Send back*** reopens the working session (D137) with Daoris's fixed words and the service's message beside them,
  marked as the service's, never as the person's, as D155 marks another agent's findings (`by`): *`<check>` failed on
  `<commit>`; this is what it said*. Its fix commits advance the landed branch (LAND2c) and so the pull request, and the
  checks wait on the new tip. **Once per step per run**, then the person: D155's one recheck, for its reason, since an
  automatic loop has nobody in it.
- ***Run again*** for an automatic stage is a start under the same say-so; for a stage the person starts it is their
  press. **A deploy whose outcome is unknown is never run again by itself**, since it may have happened.
- ***Go on anyway…*** takes the person's optional words and keeps them on the run. It is never offered for a production
  stage, or for a check a production stage after it depends on.
- **Nothing is rolled back.** Daoris never undoes a service's act and promises nothing atomic across stages or
  repositories. A rollback is a stage the person composes on a failure branch, left out of the first build (§12.2).

### 3.8 Several repositories

**Not a step.** A workflow is one repository's process, and a run is a chain's work in one repository. An ask that reaches
two repositories makes two runs, each following its own repository's workflow, and the ask's page shows them together,
each at its own step, with what is finished and what waits. A step in one repository's workflow that waited on another
repository's work would be a dependency neither owns (D32). A stage after several repositories' work, deploying a service
and its front end together say, is a join, and it waits on AFTER1, the held design of a step after several quests
(§12.2).

### 3.9 What this build cannot run

The editor offers a kind only where this build runs it, and says a partial kind's limit where it offers it. A workflow
file naming a kind or a field this build does not know, a newer build's or a hand edit, is kept as written and refused
where it would be chosen, naming its step. Current draws a declared rule that nothing acts on yet as declared, with its
limit, never as working.

## 4. Choosing a workflow

### 4.1 The order

For a chain's work in a repository, the first level that names a workflow decides:

| # | Level | Set by |
|---|---|---|
| 1 | The task's choice: its ask's, or for a quest published without one, its page's before it starts | The person; an agent only as §4.3 allows |
| 2 | The repository's choice for the task's kind | The person, at both doors |
| 3 | The repository's default | The person, at both doors |
| 4 | The workspace's choice for the task's kind | The person, at both doors |
| 5 | The workspace's default | The person, at both doors |
| 6 | Current | |

**A repository's choice replaces its workspace's whole**, as its landing, review and opinion rules do (D145 §1, D154
§1.3, D155 §2.3). A repository that names any workflow follows its own kinds and its own default, and the workspace's
mapping of a kind does not reach it. That keeps rows 2 to 5 one reading rather than a merge of two, and the door says so
as a repository's choice is set (§4.7). `current` at any level is a choice like any other.

**The run says why**: *Documentation to a pull request, v2 · chosen by this repository for Documentation*, and keeps the
level, the kind and the version (D143).

### 4.2 A kind of task

A kind is **declared by a workspace**: an id (lower-case letters, digits and dashes, at most 24), a label in the person's
words, and optionally **`paths`**, the globs work of that kind keeps to. A workspace has one vocabulary, because an ask is
made at the workspace and its intake reads one list. A repository maps the workspace's kinds to its workflows, and maps
none the workspace does not declare. A kind mapped to nothing at a level takes that level's default. A workspace holds at
most 12 kinds.

### 4.3 Who sets the kind and the task's choice

- **The person**, wherever the work is shown: the composer (a quiet *Kind* beside the receiver, the workflow it gives
  named under it, *Choose a workflow…* behind it), the ask's page, a quest's page before it starts, at the gate (§4.4),
  and a terminal: `daoris-driver ask … --kind <kind> [--workflow <id>]` and `daoris-driver quest workflow <quest>
  --kind <kind>|<id>`. The latest stands, kept with when and the person's words, as D154 keeps a review's choice (its
  `reviewChoices`).
- **The intake proposes**, with its reason in at most 300 characters, kept on the ask and shown beside the choice. Only
  the person's press applies a proposal.
- **An agent never lowers the person's part.** REVIEWENV1b3 narrowed D154 §1.5: a quote proves the person said the words,
  not that the words switch a check off, and that reading is the agent's. The same holds for a kind. The intake, or any
  agent's publish, sets a kind or a workflow on the person's quoted words **only where it lowers nothing** in any
  repository the work reaches. Otherwise its choice is kept as a proposal, with the quote as its reason.
- **Lowering is a table, not a reading.** `Involvement.Lowers(chosen, otherwise)` compares, step by step, the workflow the
  choice gives with the one the work would follow without it: a look or an opinion it drops, an opinion it makes not
  required, a landing or a stage it makes automatic, a go-ahead it removes, a pull request it skips. Any one is a
  lowering. Raising one is not, and stays the agent's to set on the person's words, as D154's `on` does.
- **The ask's choice is the person's door alone** in D156's class, as *what a review is set to* is (D156 §3.2): with the
  key it is the person's; without it, an agent's, judged by the rule above.

### 4.4 A kind's paths, checked

Where a kind declares paths and its workflow lowers the person's part against the one the work would follow without the
kind (§4.3's table), the run checks **the paths the chain changed** against them, read from git, before the first gate
step: the opinion, the look or the landing. Inside them, the run goes on. Outside them, it **holds** with `kind-paths`:
*this work changed paths outside Documentation's: `src/app/report.ts`*, and three presses: *Follow `<the other
workflow>`*, *Keep `<this one>`* (the person's say-so, kept with their words) and *Choose…*. It never switches by itself.
Where the kind's workflow lowers nothing, the paths are reported on the run and hold nothing.

This amends D154 §1.6 in one case only. There, the changed paths are a report and never decide, because nothing told
Daoris what they should be. Here the person declared the kind's paths, and the changed paths against them are a fact
(D54), so they may hold. Nothing infers a kind from the paths.

### 4.5 When a run is bound, and on which machine

A run is bound **at its first start in the repository, on the machine that drives it**: the planner resolves §4.1 and the
run keeps the version (§5.1). Until then the ask's and the quest's pages say *will follow `<workflow>`, as chosen now*. A
workflow is the driving machine's process: a quest taken on another machine follows that machine's choice. The kind does
not travel with a quest in this design (§12.2), so such a run says *no kind: published on another machine*.

### 4.6 With D154's and D155's task levels

The person's presses on one piece of work stay what they are: *Skip review for this work*, *Set it up in
`<environment>`*, *Go on anyway…*, *I looked myself…*, and the ask's and the chain's review and opinion choices among what
is declared. They act on the run's steps and are drawn there (*skipped for this work, by you*; *a look added for this
work*), never written into the workflow. Under a named workflow, D154 §1.4's rows 5 and 6 and D155 §2.4's rows 4 and 5,
the repository's rule and the workspace's, are read from the version's steps; under Current, from the keys, as today.

### 4.7 The doors

- **The terminal** (the CLI's file-local family, D50):
  - `daoris driver workflow list`; `show <id>[@<version>]`; `show --repository <name> [--kind <kind>]` and `show
    --workspace <name> [--kind <kind>]`, the resolved workflow, Current included, each step with its source;
  - `new <id> --from <preset>|current:<repository>|<id>[@<version>] [--with-opinion] [--with-look]`;
  - `edit <id> [--base <version>] <change>…`, where a change is `add <kind> --after <step> [<field>=<value>…]`,
    `remove <step>`, `set <step> <field>=<value>…`, `move <step> --after <step>`, or `on <step>
    failed=wait|send-back|stop`, all saved as one version; `apply <id> <file>`; `export <id>` and `import <file>`;
  - `use <id>|current|--clear --repository <name>|--workspace <name> [--kind <kind>]`;
  - `kind <workspace> <kind> --label "…" [--paths <glob,…>]`, and `--drop`.

  Each edit prints the diff as §8.2 draws it and saves a new version; `--plan` prints it and saves nothing.
- **The screen**: the *Workflow* tab (§6), with *Use the workspace's*, *Set for this repository* and *Clear*, as
  `Inheritable` does on Setup.
- **Ask Daoris**: a `workflow` kind of proposal (§8).
- **What each door says as it is set**, in twin sentences on one table: what the person does in it (*you look at it
  running before it lands; you merge its pull request*); what happens without them (*`<plugin>` pushes and opens a pull
  request without asking you each time*, *`<plugin>` starts `<stage>` without asking you each time*); for a repository's
  choice, *this replaces the workspace's for every kind*; and for each repository a step's declaration is missing from,
  which one, and where it is set.
- **Setup's rows.** Where a named workflow governs some of a repository's work, *How work lands*, *Review before landing*
  and *Second opinion before landing* say which work follows it, with a door to it, and keep their declarations
  editable; their process controls set the rest.

## 5. The run

### 5.1 What a run is, and what it keeps

A run is a chain's work in one repository on this machine, from its first start to its last step. **Most of it is
derived**, as a session's timeline is (the working-surface design): the quests and their holds, the session records, the
landing record (D102, D145), the review's state (`ReviewStates`), the opinion (D155), the go-aheads on the ask, and the
pull request's kept state (D148). **What only a run knows is kept**, by the driver, in `<home>/workflows/runs/<id>.json`,
written atomically:

- the binding: the workflow and its version, or Current and the graph it drew; the level that chose it, the kind, and the
  paths check (D143);
- what the new kinds read and did: each check's answers by commit, each stage's start and its reference, a go-ahead raised
  on the run, a send-back made, and the person's *Go on anyway* and *Stop the run*, each with when and the commit.

It is machine-local, never on the wire (D47 §4), and it clears with the quest's work it served (D153).

### 5.2 Where a step stands

| State | Said | Who unblocks it |
|---|---|---|
| not reached | the step, dimmed | |
| working | *being worked on by `<agent>`*, *being read by `<reviewer>`*, *checks running on `<service>`* | nobody yet |
| waiting on you | *waits for your look in `<environment>`*, *your go-ahead*, *you to merge the pull request* | the person, by the press shown |
| waiting on an agent | *the working session is answering 2 findings* | the agent; the person may stop it |
| waiting on a service | *waiting for `<service>`: 2 of 3 checks passed* | the service; *Ask again* |
| not known | *could not read `<service>`: `<why>`* | *Ask again*; the plugin's page |
| cannot start | *cannot start: `<why>`* | the door that fixes it |
| done | what proved it: *landed on `work/a1-docs` at 14:02*, *merged on the platform* | |
| skipped | *skipped for this work, by you*, with their words | |
| failed | the service's message and its commit | §3.7's doors |
| stopped | *stopped by you* | |

**A quest done is not a run done.** A run says *the agent finished*, *landed*, *pull request open*, *merged* and
*`<stage>` done* apart, so a closed quest whose work waits on a merge reads as waiting, and an ask that is Done by its
quests (D65) shows how far each of its runs went.

### 5.3 Bound to commits

Every verdict a run reads names a commit: the opinion's candidate, the look's set-up, a check's. A newer commit on the work
makes the steps after it read again: D154's and D155's coverage by `merge-base --is-ancestor`, a check by its commit. What
the person said of an older commit stays drawn beside the newer one, marked as about the older.

### 5.4 Asked while someone waits

D148 asks a plugin only at the occasions that may remove a branch, never on the loop's look or on a timer, because *a
network call nobody waits on* is waste. A run waiting on a pull request or a check has someone waiting. So **a run whose
step waits on a plugin's answer asks at the look**: at the first look after the step starts, then backing off (1, 2, 5 and
10 minutes, then every 30), one process per plugin per look as D148 point 2 bounds an occasion. After seven days with no
change it stops and says *not asked since `<date>`*, with *Ask again*. A new commit, or the person's *Ask again*, starts
the backoff over. Nothing is asked for a run nobody waits on: a stopped run, a step not reached, or a run under Current,
whose pull request is read at the clean-up's occasions as today. This amends D148 point 2 for a waiting run only. The
numbers are judgements, and the machine log is what tunes them.

### 5.5 What is recorded

- **The landing record** gains `workflow` (its id, version and level) beside `review` and `opinion`, and the trace says it
  (D143).
- **The machine log** (D94), codes and counts only: `workflow.chosen` (level, kind, Current or named), `workflow.step`
  (kind, state, code), `workflow.held` (`kind-paths`, `cannot-start`), `workflow.asked` (point, plugin, state, seconds)
  and `workflow.saved` (door, steps changed).
- **`trees land --plan`**, the review's note and Ask Daoris's room say the run's workflow and where it stands.

## 6. The editor

### 6.1 Where it lives

**On the repository's page and the workspace's**, a *Workflow* tab beside *Details* and *Setup* (UX6f, UX6g; `tabs.ts`
gains it on both), because a setting lives on the thing it is about (D150). **No place on the activity bar**: the bar is
the one navigation (D66), and Repositories holds configuration, Sessions execution, Overview attention. A library of
workflows may earn a place later if managing them becomes daily work (§12.2).

- **A repository's tab** heads with what it shows, *Default* and each kind, a segmented choice remembered for the view;
  then the workflow that work follows, its version and why (*chosen by this repository for Documentation*, *the
  workspace's*, *Current*), with *Edit*, *Set for this repository*, *Use the workspace's* or *Clear*, and a ⋯ holding *Save
  as a workflow…*, *Export…* and its terminal twins.
- **A workspace's tab** holds its default and its kinds, a row each with its workflow, its paths and *Edit*, and the
  workflows on this machine, each with *Used by* (*this workspace's default; web-app for Documentation*).
- **A browser's page** has no tab: the workflows are this machine's (§2.4).

### 6.2 The drawing

```text
web-app · Workflow                              Default · Documentation · Feature
Feature with local review · v3                                       [Edit] [⋯]
Chosen by this repository for Feature
Lands on a branch with no press after your look; you merge its pull request.

  Plugins that may hold a start: hold-by-title
  ○ The work                                    Agent alone · Claude Code
  │   its quests here; a departure or missing evidence waits for your yes
  ○ Second opinion                              Agent alone · Codex, another maker
  │   required · a dispute comes to you
  ○ Your look in local                          Agent + you
  │   set up in its own tree, shown in Daoris's browser at http://localhost:4200
  ○ Landing on a branch                         Automatic · Daoris
  │   work/{quest}-{slug}, pushed by azure-devops-pull-request
  ─ After it lands
  ○ Pull request                                You merge it on the platform
  ○ Checks on the pull request                  Automatic · Azure Pipelines
  │   required checks · failed: sent back once, then you
  ● Finished
```

- **A vertical list drawn as a graph**, laid out by Daoris, with no free positioning, no panning and no zoom. The question
  it answers is *what happens next*, and a list answers it at every width. A group is its members side by side under one
  join; a failure branch is indented under its step, its outcome written on the edge.
- **Each step is one row**: its kind's glyph and label, its participation chip, its executor, then a line of what it is set
  to, its source where a value comes from a declaration (*the repository's environment*), and its limit where the runtime
  has one. A partial step's limit is never folded away.
- **The edges are ink** (platform-ux §3: *a mark that is content wears an ink*, never a container's line). *Before it
  lands* and *After it lands* are small section titles, and the landing is the hinge between them.
- **The platform's tokens**: the seven type tokens; fields outlined in `--line-strong`; the accent only for what is
  pressed; open's hue only for what waits on the person, in a run (§7); red only for an outcome; no status hue on a
  design-time chip. Both themes. Both languages, each label designed through the glossary and wrapping with no fixed
  English width; a path breaking at its separators (`PathText`) and a command between its words (`CodeText`).

### 6.3 Editing

*Edit* opens the workflow, with every place it is used said at its head, and turns on each row's ⋯:

- ***Add after…*** offers only the kinds valid at that place and run by this build: after the work, an opinion or a look
  not already there; after the landing, a pull request, a check, a stage, a go-ahead or a group.
- ***On failure…***, on a check or a stage: *wait for you*, *send back once*, *stop the run*.
- ***Side by side…*** makes a group of checks or stages.
- ***Move earlier*** and ***Move later*** offer only places the order allows; before the landing there are none (§2.2).
  Dragging is left out of the first build (§12.2).
- ***Remove…*** asks once, in `InlineConfirm` (platform-ux §4), and says what the person loses: *removes your look: work
  here lands without you seeing it run*; *removes the second opinion: no other agent reads this work*.
- ***Save*** makes the next version, and says what changed (§8.2's diff), what the person's part became, and that running
  work keeps its version. ***Never mind*** leaves it.

**A step's settings are a form, in the drawer** (the drawer keeps forms, D118 §3d):

```text
Second opinion                                                 [×]
Who reads it        Codex, then dsh            declared on Setup
Also between steps  [ ]
Required            [x]  without one, the work waits for you
Read again          once, after its findings are answered
Then                Your look in local

From this repository's Setup: these reviewers, may run its safe commands, 20 minutes
                                                         [Open Setup]
Terminal: daoris driver workflow edit feature-local-review set opinion required=true  [Copy]

[Save]  [Never mind]
```

What a step reads from a declaration is shown read-only, with a door to where it is set. The move comes first, then
*never mind* (platform-ux §4).

### 6.4 Keys, readers and narrow windows

- **The graph is a list** (`role="list"`; a group is a nested list named by its join). ↑ and ↓ move between steps, Enter
  opens a step's drawer, and Escape closes it and gives the focus back to its row. Tab reaches each row's controls. A
  reader hears each step in order: its label, its participation, its executor, its setting, and a branch's outcome and
  destination in words.
- **At 680 px** the frame's list becomes its strip, a group's members stack under their join, and the drawer takes the
  main area's width. Labels keep their type token and wrap; nothing shrinks to fit. At the main area's 400 px floor a row's
  chip goes under its label.
- **A story first, and a molecule imports no hook** (`docs/2026-09-21-working-surface-components.md`): every state above
  is a story's props.

## 7. The run, drawn

**In the session's side bar, a view of its own, *Workflow***, beside *Timeline* and *Review*, moving between the side bar
and the panel as they do (DOCK1). It draws the run of the attended session's work on the workflow it follows, *this
session* marked on its step. Off Sessions, it says which session it speaks for, as the session views do (DOCK1a).

```text
Workflow                                    Feature with local review · v3
✓ The work                     done · 2 quests · 41 min
✓ Second opinion               Codex: 2 findings, both answered
● Your look in local           waits for you · shown 8 min ago
    set-up at a13c9e2 · [Open] [Reviewed] [Not yet…]
○ Landing on a branch          not reached
○ Pull request                 not reached
○ Checks on the pull request   not reached
```

- **Derived, never inferred from the conversation** (§5.1). Its one home is this view. The quest's and the ask's pages
  carry one line among their head's facts (*Workflow: waits for your look*), whose door opens it, and What needs you's row
  for a waiting step opens that step.
- **A step's door opens where its record is**: the review's *Second opinion* section, the review in `<environment>`, the
  go-ahead on the ask's page, the landed branch in Git, the pull request's link (outside the window, as any link opens),
  a check's link, or the conversation's event. It never repeats the transcript.
- **The presses are their owners'**: *Reviewed* is the review's, a go-ahead's answer the ask's, *Go on anyway…* the
  opinion's. The view draws each owner's control where its step waits, and no press has a second implementation.
- **Hues**: waiting on the person in open's hue, the person's alone (platform-ux §3); a wait on an agent or a service
  neutral; done's green for a step done; red only for failed. A verdict on an older commit is drawn faint beside the
  newer one, *about an older commit*.
- **Narrow**: the side bar lays over the main area as it does today (D118). No separate switch between the conversation
  and the run.

## 8. Ask Daoris

### 8.1 What it proposes

A new kind, `workflow`, through the connector's `workflow_propose { scope, kind?, workflow, base?, steps | changes, why
}`: a whole version, or §4.7's changes against a base version, for a workflow, and optionally a choice (a repository's or
a workspace's, for a kind). The helper never applies it (D89), and the room is handed no door that saves.

**The driver judges it before the person sees it**, as every kind is judged (D110): the editor's validation (§2.5, §3.9);
the base version still the newest, else refused as stale in the editor's words; the declarations each step needs; and the
plugins and stages it names. A proposal that cannot be applied yet is drawn as a draft, with what it needs first, and no
*Apply*.

### 8.2 The card

```text
Ask Daoris proposes a workflow change
Documentation to a pull request · v1 → v2 · web-app, for Documentation · new work only

Your part
  − Your look: Documentation work lands without you seeing it run
  = You still merge its pull request

    The work                            The work
    Second opinion                      Second opinion
  − Your look in local
  ~ Landing on a branch: you accept     Landing on a branch: with no press
    Pull request                        Pull request

Without asking you each time
  azure-devops-pull-request pushes the branch and opens a pull request.

Checked
  Documentation's paths: docs/**, **/*.md. Work outside them waits for your choice.

the same at a terminal: daoris driver workflow edit docs-to-pr --base 1 remove look set land accept=automatic
Why: you asked to "make docs-only work land without my review".

[Apply]  [Open in the editor]  [Not now]
```

- ***Your part* comes first**, always, never folded: each step whose participation changes, each gate removed or added,
  and what stays. It is the line the owner's *what is co-worked and what is automated* is about.
- **The diff matches steps by id**: `+` added, `−` removed, `~` changed (with the field), `↕` moved. A reader hears it as
  text, step by step, in the list's order.
- ***Without asking you each time*** lists every outward act the change makes standing: a push, a pull request, a stage.
  Empty, it says *nothing new leaves this machine without you*.
- ***Needs first*** lists what is missing (a plugin not installed, a stage it does not declare, an environment not
  declared, a reviewer not listed), each with its door, and the card has no *Apply* until each is met.
- ***Applies to*** says new work only, and how many runs keep the version they started on.
- ***Apply* and *Not now*** are the card's own words (D89). *Confirm* is D156's word for the window confirming a terminal's
  request, a different act, so it is not reused here (D116). ***Open in the editor*** opens the change on the repository's
  tab, drawn as the card drew it, for the person to change and save there.

### 8.3 Two asks, as the owner might say them

- ***"Make docs-only work land without my review."*** The helper proposes a kind with paths, where the workspace has none,
  and a workflow for it whose landing needs no press and whose pull request stays: **a pull request opened with no press,
  never a merge**. D145 lets a done open a pull request with no press and rejects a merge at done, and the platform's merge
  stays the person's last step. The card's *Your part* says so. Merging with nobody is a decision of its own (§12.1), and
  the helper says that rather than translating one ask into the other.
- ***"Add a staging deploy after the merge."*** The card adds the merge's wait if the pull request step has none, a
  go-ahead where the person asked for one, *Deploy to staging* by the named plugin's stage, and a health check. A plugin
  that declares no such stage, or no plugin at all, is an unresolved field under *Needs first*, never a guess.

### 8.4 Who applies it

**Apply is the person's press, in the window.** The page draws the card, and Apply goes over the bridge, which no session
holds (D156 §2.2). Saving a version and setting a choice are file edits under the home that the shell's modules make, and
the version keeps `door: ask-daoris:<proposal>`. A terminal's `daoris driver workflow` writes the same files directly.
Like `driver.json` today, they are beyond the doors D156 guards: a session that runs the CLI could write them. D156 §7 says
so of `driver.json`, and PERSONDOOR2 designs that boundary. Until it lands, a workflow file is exactly as protected as the
landing rule beside it, and this design claims no more.

**What stays exempt** (D110): the run's presses that are judgements or the person's spending, as they are today:
*Reviewed*, a go-ahead's answer, *Go on anyway…*, *Run `<stage>`* and *Stop the run*. The room names them and their
terminal doors.

**The room** gains each repository's workflow (its name, version and source) in its workspaces table, and the workspace's
kinds, so the helper answers *how does work in web-app land* from the drawn workflow, Current included.

## 9. What the no-model tier does

`model-decoupling`: the feature is said without a model, and its floor is the facts.

- **Current is drawn from the keys**, by one function on each twin.
- **Choosing a workflow is a table** (§4.1), and so is *lowering* (§4.3).
- **A kind's paths are git's changed paths against the person's globs.**
- **Every step's state is a record or a plugin's answer**: a quest's state, a landing record, `merge-base
  --is-ancestor`, the review's states, a check's verdict at a commit. Unknown is said as unknown.
- **The editor, the terminal and the presets** make and change every workflow with no agent.
- **What needs a model is said as such.** Drafting a workflow from a sentence is Ask Daoris's, on its own agent; proposing
  a kind for an ask is the intake's, and its proposal names its tier. With no agent named, Ask Daoris's starters include
  *Draw how work here moves*, a door to the tab, and the intake proposes no kind: the ask takes the default.

## 10. Worked through

1. **The work workspace's web-app, two kinds.** The workspace declares *Documentation* (`docs/**`, `**/*.md`) and
   *Feature*. Its default is *Feature with local review*: an opinion by Codex, required; a look in `local`; a landing on a
   branch with no press; the pull request; its required checks. *Documentation* maps to *Documentation to a pull request*:
   an opinion, not required; no look; a landing with no press; the pull request. The person asks for a README fix and
   picks *Documentation* in the composer. The work's one quest is done; the run checks its changed paths, `README.md`,
   inside; Codex reads it and raises nothing in what it read; the landing makes the branch and the plugin opens the pull
   request; the person merges it on the platform, and the run reads *merged* at its next look. A second ask, also
   *Documentation*, changes `src/app/report.ts` beside its document: the run holds before the opinion, *changed paths
   outside Documentation's*, and the person presses *Follow Feature with local review*.
2. **This repository's own development.** Its workspace's default would be *Merge after you accept, with a second
   opinion*: Codex reads each lane's work, its findings go to the working session, and the parent's *Accept…* is the
   merge, as the merge tool's practice is today. Nothing follows the landing: the release workflow is dispatched by hand
   (CLAUDE.md), so no stage is composed.
3. **A release with stages.** A release-environment repository's default is *Release with stages*: the work; a landing on
   a branch the person accepts; the pull request and its checks; *merged*; *Deploy to staging*, automatic, the person's
   standing say-so said where it was set; a health check; a go-ahead; *Deploy to production*, always the person's press.
   The staging deploy's plugin answers nothing within its bound, so the step reads *unknown: it may have run*. The person
   presses *Check outcome*, which reads it passed. The production press is theirs, and the run ends at *done*.

## 11. The second opinion on this design, weighed

Codex read the question read-only before this was written. Each of its points is adopted, amended or rejected here, with
the reason.

| # | Codex's point | Here | Why |
|---|---|---|---|
| 0 | Workflows as the visible plan; one vertical graph shared by setup, Ask Daoris's proposals and live runs; sessions stay where work happens | Adopted (§6–§8) | |
| 1a | A workflow is a named, versioned agreement whose summary says its destination and the person's part | Adopted, amended (§2.5) | The summary is composed from the steps, never typed, so it cannot drift from them |
| 1b | No single autonomy slider | Adopted (§3.1) | |
| 1c | A step names its outcome, its executor and what proves it done | Adopted (§3.2) | What proves it is the kind's, a fact Daoris reads, never typed |
| 1d | An edge lets a step start; a condition is a readable test on recorded facts with an explicit alternative | Amended (§3.3) | Outcome branches from a fixed set and a kind's paths; no expressions, so nothing evaluates what it was handed |
| 1e | Unknown is apart from false and never counts as success | Adopted (§3.3, §3.7) | D148 point 5 for every wait |
| 1f | Nine step kinds | Amended (§3.2, §3.8) | Opening a pull request and its merge are one step; a CI/CD stage is a `stage` and a `check`; several repositories are not a step (D32) |
| 1g | Participation apart from executor; *Agent alone*, *Agent + you*, *You*, *Automatic · `<service>`*; neutral chips; *Agent + you* a concrete checkpoint | Adopted (§3.1) | |
| 1h | Current derived from the rules as resolved, each with its source | Adopted (§2.7) | And its limit where the runtime has one |
| 1i | Presets: manual merge, manual branch, automatic branch and pull request, each with a second opinion or a running review | Adopted (§2.7) | |
| 1j | D155's order kept; an opinion never gives *Reviewed*; a go-ahead never accepts work | Adopted (§2.2) | The order before the landing is the runtime's |
| 2a | On the repository's and the workspace's pages; Setup's rows become summaries linking into it; declarations through step details | Amended (§2.3, §4.7, §6.3) | Declarations stay set on Setup, as the repository's facts; a step shows them read-only with a door there |
| 2b | *Use workspace default*, *Set for this repository*, *Clear*, as `Inheritable` | Adopted (§4.7) | |
| 2c | No activity-bar place at first | Adopted (§6.1) | D66: one navigation; D150: a setting on its thing |
| 2d | The ask's and quest's pages show the workflow, its source and the run; the composer a quiet choice | Adopted, amended (§4.3, §7) | The composer offers the kind first, the workflow behind it; the pages carry one line, and the run's one home is the side bar's view |
| 2e | The selection order, six levels | Adopted (§4.1) | With its reason: a repository's choice replaces its workspace's whole, as each rule does |
| 2f | Say why: *selected by this repository's Documentation rule* | Adopted (§4.1) | |
| 2g | Kinds chosen by the person; the intake may propose | Adopted, amended (§4.3) | The intake sets one on quoted words only where it lowers nothing, by a table |
| 2h | An automatic docs-only rule needs a person-declared definition checked against the changed paths; a mismatch holds | Adopted (§4.4) | A fact the person declared may hold; it never switches by itself |
| 2i | Whole versions, never merged fragments; a faithful migration preview; runs keep their version; editing changes new work; changing active work needs its own diff | Adopted, amended (§2.6, §2.7) | No migration: Current is derived, and *Save as a workflow…* is the preview. Moving running work is left out; Current reads live, as today |
| 3a | A vertical list drawn as a graph, laid out by Daoris, nested branches, no free positioning | Adopted (§6.2) | |
| 3b | *Add after*, *Add condition*, *Run in parallel*; valid destinations only; removing a gate previews its loss; retries as bounded returns | Amended (§6.3) | *Add condition* is *On failure…*; parallel only for checks and stages, joined on all; removal asks once in `InlineConfirm` |
| 3c | A step opens the right-side form drawer | Adopted, amended (§6.3) | Declarations read-only, with a door to Setup |
| 3d | Tab, arrows, Enter, Escape; move commands beside optional dragging; an ordered structure for readers | Adopted, amended (§6.4) | Moves in the row's ⋯; dragging left out of the first build |
| 3e | At 680 px: the strip, stacked branches, the drawer at the content's width, labels kept | Adopted (§6.4) | |
| 3f | The platform's surfaces, accent and seven tokens; amber for the person, neutral outside waits, red failure; Chinese through the glossary | Adopted (§6.2, §7) | |
| 4a | A run drawn on the same graph from recorded transitions; *agent finished*, *branch*, *PR open*, *merged*, *deployed* apart; a quest done is not delivery done | Adopted (§5.1–§5.2) | Most of it derived from records that exist; only what a run alone knows is kept |
| 4b | A *Workflow* view in the session's right dock; a node opens evidence or the event, never duplicating the transcript | Adopted (§7) | |
| 4c | Narrow: the conversation and the run switch within the main area | Rejected (§7) | The frame already lays the side bar over the main area (D118); a second mechanism would be one more to keep |
| 4d | Every wait names who unblocks it; a failure shows the service's message, evidence and recovery; an unknown deploy offers *Check outcome* before a retry; Overview links to the waiting step | Adopted (§3.7, §5.2, §7) | |
| 4e | Verdicts bound to candidate commits; staleness visible; fan-out's partial completion; no atomic rollback across repositories | Adopted (§3.7, §3.8, §5.3) | |
| 5a | Ask Daoris produces a draft change through the same validation; the card says scope, conditions, the graph's changes, the person's part, outward acts, prerequisites and active work | Adopted (§8.1–§8.2) | |
| 5b | *Confirm*, *Edit proposal*, *Never mind* | Amended (§8.2) | *Apply*, *Open in the editor*, *Not now*: the card's own words; *Confirm* is D156's terminal confirmation, another act (D116) |
| 5c | *Land without my review* is an automatic pull request, never an automatic merge, which D145 rejects | Adopted (§8.3, §12.1) | |
| 5d | *Add a staging deploy after merge*: added steps; what is missing stays an unresolved field | Adopted (§8.3) | |
| 5e | Diffs by stable step ids with a text equivalent; applying checks the current version; Ask Daoris never gives the review's verdict | Adopted (§8.1–§8.2, §8.4) | |
| 6a | No programming canvas, scripts, loops, marketplace, credential designer or competing CI engine; services own their pipelines | Adopted (§3.4, §12.1) | |
| 6b | The smallest release: Current with its sources, presets and selection, editing through Ask Daoris with a diff, the run, the terminal, no model | Amended (§14) | In two releases: the picture first (Current, the run, the terminal, the room), adding no authority; then named workflows, the choice, the editor and the card; services last |
| 6c | Offer a step only where its runtime is connected; D154's and D155's are partial | Adopted (§3.2, §3.9) | |
| 6d | Then kinds, and one service integration whole: trigger, wait, failure, recovery; D148's query is not a CI subscription | Adopted (§3.4–§3.7) | `work/checks` and `work/stage`, asked only while a run waits |
| 6e | Versions, kind routing, service waits and automatic merge each need a decision | Adopted, amended | D157 decides the first three; automatic merge is rejected here and left to a decision of its own |

## 12. Rejected, and left out

### 12.1 Rejected

- **A free canvas**: positions, panning and zoom. The question a workflow answers is what happens next, and a list answers
  it at every width.
- **A programming canvas**: expressions, scripts, a step that runs a command, a loop. D65 and D1 decline an engine;
  Daoris never runs a command it is handed (D154 point 5); and a loop is agents talking with nobody in it (D155).
- **Daoris running a platform's tool, or holding its credential.** D87, D100 and D148 keep a platform's acts a plugin's,
  as the person's tools are signed in.
- **A CI engine of Daoris's own**: holding a pipeline's definition, starting its jobs, retrying inside it. The service owns
  its pipeline (§3.4).
- **A marketplace of connectors and a designer of credentials.** A plugin is installed by the person (D64, D120), and a
  credential stays the platform's tools' (D148 point 10).
- **A single slider from manual to automatic.** It cannot tell a look from a deploy.
- **Migrating today's rules into workflows.** It would rewrite the person's file, an older build would read none of it, and
  nothing set must stay today (§2.7).
- **The manifest, the registry, or the service's store and the remote, as a workflow's home** (§2.4).
- **Merging fragments**: a repository's steps added to its workspace's workflow. A choice is a whole version, and a
  repository's choice replaces its workspace's whole.
- **Reordering the gates before the landing** (§2.2, D155 §7).
- **An agent lowering the person's part**, on its own reading or on a quote (§4.3; D74, REVIEWENV1b3).
- **A kind inferred from the changed paths, or a workflow switched by itself on a mismatch** (§4.4; D54, D74).
- **Merging with nobody**, as *land without my review*. D145 rejected a merge at done; D51 rule 6 keeps nothing merging
  itself. Merging a pull request with no person is a decision of its own if it is ever wanted.
- **A standing say-so for a production act** (§3.6; D37).
- **Running a deploy whose outcome is unknown again by itself**, and **sending back more than once by itself** (§3.7).
- **A rollback across stages or repositories, or a promise of atomicity** (§3.7).
- **Several repositories as a step** of one repository's workflow (§3.8; D32).
- **A run's state read from the conversation.** A run is derived from records, as a timeline is.
- **Asking a platform on a timer for a run nobody waits on** (§5.4; D148 point 2 stands there).
- **Offering a step kind this build does not run** (§3.9), and **a typed summary** (§2.5).
- **A place on the activity bar now** (§6.1; D66).
- **The card's press named *Confirm*** (§8.2; D116).
- **A model judging a step done, a branch's outcome, or a task's kind** (D24, D54).

### 12.2 Left out, not rejected

- **A workflow shared with teammates** through the remote. A file carried by `export` and `import` is the first form.
- **A kind travelling with a quest** to another machine: a quest field and its census (`quest-operations.md`).
- **Moving running work onto a newer version**, with a diff for that run.
- **Dragging steps**, and **a library of workflows as a place** of its own.
- **A failure branch that runs stages** (a rollback), nested under a check or a stage.
- **A join across repositories**, which waits on AFTER1.
- **A conversation's work under a workflow.** A chat lands by the person's press as today, as D155 §12.2 left opinions on
  a conversation's work out.
- **A pull request's review threads back to the work** (PLUGHOOK2, held).
- **The queue as a landing form**, offered once DEV5–DEV7 land.
- **A repository suggesting a workflow in its own documents**, which would be a canon change.
- **A pull request merged with no person**, a decision of its own (§12.1).

## 13. Against the decisions

- **Amended.**
  - D65 point 4: *workflow* names the process around the work; the chain is its `work` step. No engine and no coordinator
    agent stand.
  - D87 and D145: where a named workflow governs, `form` and `autoAccept` are its landing step's; the landing rule keeps
    `pattern`, `plugin` and `tidy` as declarations and governs work no named workflow is chosen for.
  - D154: where a named workflow governs, the look step's presence is `required` and its environment the one chosen; §1.4's
    rows 5 and 6 read from the version; §1.6, a kind's declared paths may hold (§4.4).
  - D155: where a named workflow governs, `on`, `required`, `recheck` and which of the declared reviewers are its opinion
    step's; §2.4's rows 4 and 5 read from the version.
  - D148 point 2: a run waiting on a plugin's answer asks at the look, backing off. §3.2's `work/checks` is designed here,
    its order answered by its place after the landing.
  - D64 §4 and the plugin design: a query point, `work/checks`; an act point, `work/stage`; a manifest's `stages`.
  - D135: a go-ahead a run raises, on its ask or on the run.
  - D110: a `workflow` kind of proposal; the run's presses exempt as today's are.
  - D94, D102 and D143: the run's record, the log's lines, and the landing record's `workflow`.
  - D153: a run clears with the work it served.
  - D150 §4.2–§4.3: a *Workflow* tab on a repository's and a workspace's page. DOCK1 and D118: *Workflow* among the
    attended session's views.
  - D116: the glossary's rows for the new words.
- **Standing.**
  - D1 and D65's *no engine*; D23 and D24 (agents named, never guessed; no model named).
  - D32 (no cross-repository step); D37 (outward and irreversible acts stay the person's); D45 and D46 (the driver reads
    and does its own acts; the work is the repository's own agent's).
  - D47 §4 (machine-local material); D50 (two doors; files are the API); D51 rule 6 (nothing merges itself); D52; D54 (a
    fact gates, a judgement reports).
  - D66 (one navigation); D74 (no widening without the person); D89 (every change confirmed); D133 and D144 (the quest's
    own holds).
  - D154 point 8 (the verdict is the person's alone); D155 points 8 and 9 (claims and facts; one gate, in a fixed order);
    D156 (the person's doors).

**What the checks do not cover.** Documents only, and nothing is built. Read at `6e465e1f`: `DriverConfig.cs`'s rule keys,
`Landing.cs`'s `LandingRules`, `RepositorySetup.tsx`, `WorkspaceSetup.tsx`, `SetupParts.tsx`'s `Inheritable`,
`projects/tabs.ts`, `RightDock.tsx`'s views, the glossary and `help.json`'s card words, and the decisions' notes for what
D154's and D155's builds left. No plugin was asked for a check or a stage, and no platform's check or pipeline fields were
read; `work/checks`' and `work/stage`'s shapes are drafts that their row measures first, as D148's first slice did. The
bounds (24 steps, 12 kinds, 20 versions, the backoff, seven days) are judgements, not measurements. Codex's proposal was
read as the parent relayed it, and its claims about D154's and D155's runtime were checked against those decisions'
notes. `verify` checks D157's place, that it names what it rejected, and the links here, and none of the words.

## 14. The build

Each row ships both doors for its part (D50) and names its Ask Daoris door or its reason (D110). **The first release is
a–c**: the picture of today's rules, the run on it, and the terminal's `show`, adding no authority. **The second is d–i**:
named workflows, the choice, the gate, the editor, Ask Daoris and the intake. **Services follow, j–m**, and **n** is the
look on the install.

- [ ] **WORKFLOW1a — Current, derived and said** (driver, cli). One pure function per twin derives a repository's
  workflow from its rules, each step with its source and limit, held to a shared fixture table; `daoris driver workflow
  show --repository|--workspace` prints it. Contract: §2.7, §3.2, §4.7. Proof: `WorkflowCurrentTests` and
  `workflows.test.ts`, cell for cell.
- [ ] **WORKFLOW1b — drawn on the pages** (modules, web-shell; after a). A *Workflow* tab on a repository's and a
  workspace's page draws Current read-only: steps, participation, sources, limits, doors to Setup's rows; the glossary's
  words. Contract: §6.1–§6.2, §6.4. Proof: stories, vitest, `i18n:check`, `names:check --strict`, the look in both themes
  and 中文 at 1546 and 680 px.
- [ ] **WORKFLOW1c — the run, derived** (driver, modules, web-shell; after a). A chain's work in a repository read as steps
  from its records (quests, sessions, landing record, review state, opinion, go-aheads, kept pull-request state); the
  side bar's *Workflow* view; the quest's and ask's line; What needs you's door. Contract: §5.2, §7. Proof:
  `WorkflowRunTests`' state table, vitest, stories.
- [ ] **WORKFLOW1d — named workflows and their versions** (cli, driver; after a). `<home>/workflows/<id>.json`: steps,
  validation (kinds this build runs, the order before the landing, groups, bounds), versions never edited, presets built
  in; `workflow new|edit|apply|list|show|export|import`, each printing its diff. Contract: §2.4–§2.6, §3.3, §4.7. Proof:
  twin tests on one table.
- [ ] **WORKFLOW1e — kinds and the choice** (cli, driver, service; after d). `workflows` and `workspaceWorkflows` in
  `driver.json`, the workspace's kinds and paths; §4.1's table; the ask's kind and choice, the person's door (D156); the
  run bound at its first start with what chose it; `workflow use|kind`. Contract: §4.1–§4.3, §4.5. Proof:
  `WorkflowSelectionTests`, `driverconfig.test.ts`, the service's ask tests.
- [ ] **WORKFLOW1f — the gate reads the run's version** (driver, modules; after e, XAGENT1f). Landing, look, opinion
  and automatic acceptance read their process from a named version, Current unchanged; a step that cannot start
  sits, saying why; a kind's paths hold; the landing record, trace and log. Contract: §2.3, §4.4–§4.6, §5. Proof:
  `WorkflowGateTests`; `ReviewLandingTests`, `AutoLandingTests` rows (`Process`).
- [ ] **WORKFLOW1g — the editor** (web-shell, web-settings, modules; after d, e). Presets, *Save as a workflow…*, *Add
  after*, *On failure…*, moves, *Remove…* asked once with its loss, the step's drawer and terminal twin, the choice's
  rows, keys, 680 px. Contract: §6. Proof: stories, vitest, `i18n:check`, `names:check --strict`, `HelpCoverageTests`
  rows, the look in both themes and 中文.
- [ ] **WORKFLOW1h — Ask Daoris proposes a workflow** (service, driver, web-shell; after d, e). `workflow_propose`; the
  driver's judge (validation, base version, diff by step id, the person's part, outward acts, needs first); the card's
  *Apply*, *Open in the editor*, *Not now*; the room's column. Contract: §8. Proof: kinds and coverage tests, the room's
  goldens, `ProposalCard.test.tsx`.
- [ ] **WORKFLOW1i — the intake proposes a kind** (driver, service; after e). The room names the workspace's kinds and
  each repository's workflow; a kind is set on the person's quoted words only where `Involvement.Lowers` says nothing is
  lowered, else proposed with its reason. Contract: §4.3. Proof: `IntakePrompt`'s golden, `InvolvementTests`' table, the
  service's quote refusal.
- [ ] **WORKFLOW1j — a pull request waited on** (driver, examples; after f, PLUGHOOK1b). A run waiting on its pull request
  asks its plugin's `work/state` at the look, backing off, and moves to merged or abandoned; nothing asked for a run nobody
  waits on. Contract: §3.2, §5.4. Proof: `PullRequestWaitTests`; `PullRequestOccasionTests` amended; `landing-plugins.test.ts`.
- [ ] **WORKFLOW1k — outside checks** (driver, examples; after j). `work/checks`, a query: checks at a commit, stale on a
  newer one, unknown never failed; `failed` waits, sends back once or stops; groups join on all; the kit's entry; both
  example plugins answer, fields measured first. Contract: §3.3, §3.5, §3.7. Proof: `HookChecksTests`, kit tests, fake
  `gh`/`az` rows.
- [ ] **WORKFLOW1l — service stages and go-aheads** (driver, examples, service; after k). `work/stage`, an act on a stage
  the plugin's manifest declares; production only by the person's press; *Check outcome* before *Run again*; a go-ahead a
  run raises on its ask or itself. Contract: §3.6–§3.7. Proof: `HookStageTests`, `GoAheadTests`' raised row, kit tests.
- [ ] **WORKFLOW1m — the family rehearsal phase** (tools, examples; after k). A named workflow chosen by kind; a
  documentation kind's path mismatch holds; a stub plugin's check fails, the stub session is sent back once, then passes;
  a stage waits for the person's press. Contract: §4.4, §5. Proof: `rehearse:family`'s new phase.
- [ ] **WORKFLOW1n — on the install** (parent; after a republish carrying a–h). Current drawn for the work workspace's
  repositories; a named workflow saved through Ask Daoris and a run followed to its pull request; both themes and
  languages at 1546, 888 and 680 px. Contract: §6–§8. Proof: dated shots and a ledger under D157.

# Work is reviewed where it runs before it is offered to land

**Standing:** contract under D154, extended by D155–D157. D154's dated notes record implementation
and serving corrections; remaining proposals and installed proofs are in `TASKS.md`. §0 is the
design-time case, not a current code inventory.

> REVIEWENV1, decided as D154 (2026-10-08), before implementation. `Dnn` is `docs/decisions/Dnn.md`. The owner, of a ticket
> in their work workspace that Daoris had carried to *Accept…*: *"I never saw this set up in dev for me to review"*, and
> *"a development without any verification and want to merge to prod is not a good sign; this should also be some
> process rule set up in Daoris's development cycle"*. The same day: *"this should be configurable: some repos need it
> and some don't, or some tasks need it and some don't"*, *"a lot of development we all review locally, just like what
> we are doing with Daoris right now"*, and, after a session asked to stop their own dev servers to serve a branch:
> *"I still don't see it added, so Daoris should drive the browser just like how Claude Code does with the extension"*.

## 0. The case

A ticket in the owner's work workspace went through the intake (D65). It became a chain: a quest that built the change in
the reporting app's repository, then a `then` step that checked it in a browser. The sessions coded it, ran the
repository's tests and opened the page in a browser, where the check injected the new setting into the page's own network
answer. Nothing was written to the development environment. Both quests closed done, and the chain was offered
*Accept…*, which puts its work on a branch (D87) for the person to push and open a pull request toward the line that
ships to production. The person never saw it run.

Each part did what its rule says. The chain ran its steps on one branch (D65 §4, D82, D145 §3). The requirements were
answered (D133), and checking in a browser is a verify step's work. *Accept…* is offered once a session's work can land
(D87), and *Accept automatically* lands it at done (D145). What no rule knows is that the work has a running form the
person could look at, and nothing waits for them to look. Every check in the chain was an agent's, and the one that
touched a page stood in for the environment it was meant to check, so it proved the stand-in.

The same day brought three more facts from live work:

- **A route nobody documented.** A session about to verify its work proposed a deploy to the development environment
  that no document of its repository describes. The person chose instead to *run it locally against dev data*. The
  route to a running form is the repository's documented one, never one a session proposes.
- **Local is the common way.** Most work is reviewed on the person's own machine, the way this repository's own work is
  seen (`npm run desktop -- run`).
- **The person's processes are theirs.** To serve the branch on the usual port, a session asked to stop the person's own
  dev servers. The person refused and asked for what a harness's browser extension does: drive the browser, and show
  the change. So a local review is something the step **shows** the person in Daoris's browser. It is not something the
  person sets up, and the step takes over no process or port of theirs.

This design adds three things, each off until the person sets it: a **review environment** that a workspace or a
repository declares (§1), a **set-up step** in the chain that puts the work there and shows the person where to look
(§2), and a **gate** that offers the work to land only after the person says *reviewed* (§3). With nothing set anywhere,
every screen and every landing is what it is today (§5.2).

## 1. Where a review environment is declared, and at which level

### 1.1 Two kinds, and local comes first

A review environment is where a repository's work runs for the person to look at before it lands. It has a name, a kind,
the repository's own procedure for reaching it, and the address where the app is used.

- **Local** (`local`, the default kind). The step builds the work in its own tree and **shows it in Daoris's browser,
  at the address where the app normally runs.** It serves the branch's build to its own tab by request interception,
  navigates to the change and performs the interaction, then leaves the tab on screen (§2.3). Nothing is deployed, and
  no process or port of the person's is touched. The app's own calls, to the development environment's data, go out as
  they would for the person, under the sign-ins they already hold in that browser. This is how most work is reviewed,
  so it is designed first and is not a fallback.
- **Deployed** (`deployed`). A shared environment, such as `dev`, that the work reaches by the repository's documented
  route: a deploy, a pipeline run, or configuration written through its API. Every such act leaves the machine, so each
  needs the person's go-ahead (§2.5). The environment keeps running by itself (§2.4).

**Production is never a review environment.** A name that reads as production is refused at every door. The test uses the
words `GoAheads.cs` already reads a place by (`production`, `prod`, `prd`, `live`), as the whole name or as a word of it.
The set-up step never asks a go-ahead for production (§2.5).

**The procedure is the repository's, named by path.** Each environment names a repository-relative path to a document or
a skill in that repository that says how work reaches it. For local, that is how to build the app and point it at the
development environment's data, often a section of the README. For deployed, it is how to deploy to dev, or a skill.
Daoris checks that the path exists in the repository's line when the rule is set, and in the step's tree when the step
starts. It never reads the document for meaning: the step's session reads it and follows it. A repository that documents
no route has no review environment yet. Writing that document is the repository's own work, asked of it as a quest
(`repository-owns-its-work`), because what a session would guess in its place is what the first live fact refused.

### 1.2 Where it lives: `driver.json`, beside the landing rule

The declaration and the requirement live in `driver.json`, in two new keys beside `landings` and `workspaceLandings`.
Three places were weighed.

- **The landing rule's file** (D87, `driver.json`). Chosen. The gate is the landing's, and the driver is the one thing
  that resolves a landing rule, reads the chain and lands the work. A rule here reaches every registered repository,
  including one never adopted (D70). Whether this person's finished work waits for their look is their process on this
  machine, which is why D87 put how work lands here. Both doors already edit this file (D50).
- **The manifest** (D34, D122's `documents`). Rejected as the home. The manifest is tracked, so it would bind every clone
  and every contributor who does not run Daoris (the workspace design's §2a). An unadopted repository has no manifest.
  And *must the person look before it lands* is the person's question, not what the repository is. The manifest's part
  is already served another way: the procedure is a file in the repository, named by path. A later `documents` role for
  it would let the repository name its own procedure. That is a canon change, and it is left out (§5.1).
- **The workspace registry** (WSP1, WSP2). Rejected. The registry is the authority on *which repositories are in a
  workspace and where their checkouts are*. Its rows are wiring that `connect` and `import` rewrite with *silence
  preserves*. A review rule is a choice about finished work, which the driver resolves. A workspace's rule names the
  workspace by name, as `workspaceLandings` does, and the registry still decides which repositories that reaches.

**It is a setting of its own, not a field on the landing rule.** A repository's landing rule replaces its workspace's
whole (D145 §1), so a repository that wants the workspace's landing but not its review would have to restate the whole
landing rule. A review means something whatever the landing form is: a merge into the person's line waits for it as a
branch does. So it is ranked in its own keys, as reading across is (D107 §1). D145 rejected a separate setting for
*Accept automatically* because that switch means nothing apart from its branch and plugin. That reason does not hold
here.

### 1.3 The rule

```json
"reviews": {
  "web-app": {
    "required": true,
    "environments": [
      { "name": "local", "kind": "local", "procedure": "README.md", "address": "http://localhost:4200" },
      { "name": "dev", "kind": "deployed", "procedure": "docs/deploying-to-dev.md", "address": "https://dev.example.test" }
    ]
  },
  "infra-plans": false
},
"workspaceReviews": {
  "work": {
    "required": true,
    "environments": [ { "name": "local", "kind": "local", "procedure": "README.md", "address": "http://localhost:4200" } ]
  }
}
```

- **`environments`**: one or more, the first being the default. Each has a `name` (lower-case letters, digits and
  dashes, at most 32, unique in the rule, never production), a `kind` (`local` or `deployed`), a `procedure` (a
  repository-relative path with no `..`, no root, no drive and no link, as KNOWUSE1c names a router), and an `address`
  (an absolute `http` or `https` origin, D65's rule for a link).
  - For **local**, `address` is required. It is where the app normally runs, such as its usual loopback address, or the
    dev site whose front end the branch's build replaces in the step's tab. It is the only origin the step may serve to
    (§2.3). It is the person's to declare, never a step's to choose. A repository whose app runs somewhere other than
    its workspace's address has a rule of its own.
  - For **deployed**, `address` is the environment's own, and optional: the step names where the work shows.
  - A local environment may also carry **`run`**, the command that starts work needing a process of its own (§2.3,
    *A process of its own*). It is the person's standing say-so for Daoris to run that command in a set-up step's tree,
    on a port nobody holds, and every door says so as it is set.
- **`required`**: only JSON `true` is on, and it is written only when on (as `autoAccept` is). With it on, a chain's
  work waits for the person's *reviewed* before it lands. With it off, the environments are declared and a task may
  ask for one (§1.4).
- **`false` on a repository**: this repository has no review environment, whatever its workspace says. A documents-only
  repository or an infrastructure plan's repository says this.
- **A repository's rule replaces its workspace's whole**, as a landing rule does (D145 §1). `--clear` hands it back. A
  workspace's environment names a procedure path that each of its repositories is read at. A repository that does not
  hold that path is named when the rule is set, and its steps sit until it has one (§2.1).

### 1.4 Three levels, each over the one above

The **workspace** sets a default for every repository in it. The **repository** overrides that default, or says it has
no review environment. The **task**, an ask or a quest, overrides both, for its own work. The task chooses whether the
work is reviewed and where, from the environments its repository declares. It never declares an environment, because an
environment's procedure is the repository's.

For the work of a chain landing in a repository, read from the most specific down. The first that says anything decides:

| # | Level | Says | Set by |
|---|---|---|---|
| 1 | The chain, at the gate | *Skip review for this work*, with the person's words | The person only (§3.6) |
| 2 | The chain's set-up step | A set-up step for this repository in the chain means review, in that step's environment | The intake, or the person's *Set it up* (§2.1) |
| 3 | The chain | `off`, `on` (the default environment) or an environment's name, set at publish and inherited by each step | The person, or the intake quoting the person (§1.5) |
| 4 | The ask | The same three, for every chain the ask publishes that sets none | The person (§1.5) |
| 5 | The repository | Its rule, or `false` | The person, at both doors (§1.7) |
| 6 | The workspace | Its rule | The person, at both doors |
| 7 | Nothing | No review: today's behaviour | |

A **chain** is the `follows` chain (D149 point 1). A single quest is a chain of one. A chain that crosses repositories
reads the table once for each repository's part, against that repository's rule. Setting `on`, or naming an environment,
for a repository that declares none is refused at the door that sets it, naming the repository. On an ask that reaches
several repositories, `on` applies wherever an environment is declared, and the ask's page says which parts land without
a review.

### 1.5 Who sets the task level

**Both the person and the intake, on different terms.**

- **The person** sets it anywhere it is shown: in the composer when they ask (the ask), on the ask's page, on a quest's
  page for its chain, at the gate (§3.6), and at a terminal (`daoris-driver ask … --review off|on|<environment>`,
  `daoris-driver ask --review <id> …`, `daoris-driver quest review <quest> off|on|<environment>`). Each change is kept
  with when it was made and any words they gave, and the latest one stands, as a go-ahead's answer does (KNOWUSE1a).
- **The intake** sets it only on the person's own words. It writes the choice with a quote, which the exchange checks
  against what the person said on the ask, as it checks a requirement's quote (DRIFT1c, `QuestRequirement.QuotedIn`). Two
  examples: *"run it locally against dev data"* gives `local`, and *"no need to run it, it's a typo"* gives `off`.
  Without the person's words it does not set the level. It may **propose** one, with its reason in at most 300
  characters, kept on the ask and shown beside the choice. Only the person's press applies a proposal.
  *Narrowed by D154's REVIEWENV1b3 note: an agent never sets `off`, quoted or not. Its `off` is kept as a proposal, the
  person's words as its reason, so the `off` example above is a proposal too.*

The reason for the split is D74's, applied to verification. Lowering a gate the person set, on an agent's own reading,
is an agent widening its own autonomy. Raising one on its own reading is a press and a deploy nobody asked for. Neither
is the intake's to do unasked. Quoting the person is not a reading: it is what they said.

### 1.6 When the default says review and the work plainly needs none

Suppose the repository's default is on and the ask is a change to a document. **The intake does not ask, and does not
switch it off. It proposes `off` with its reason, and composes no set-up step for that chain.** Its report says so.

- **The work is not held up.** Asking would park the intake on a question that has no bearing on the build. The build and
  its checks run as they would. `autonomous-development` puts a choice like this at the checkpoint, as a decision with
  its analysis, not as a pause in the middle.
- **Nothing runs on a guess.** With no step composed, nothing is built for review or deployed until the person chooses.
- **The decision comes once, where it is needed.** The default still holds, so the gate holds the landing. It shows the
  intake's proposal and its reason, with two presses: *Skip review for this work* (level 1, with the proposal's reason
  shown and the person's own words if they add any) and *Set it up in `<environment>`* (level 2, §2.1). The person may
  also apply the proposal early, on the ask's page, and then nothing waits.
- **The floor says a fact beside it** (§4): which paths the chain changed. Text such as *changes only Markdown files*
  is reported, and never decides (D54).

The same holds the other way. A repository whose default is off, and an ask that changes what a page shows, gets a
proposal to review, and the person's press on the ask or at the quest's page applies it.

### 1.7 The doors

- **The terminal:** `daoris driver review <repository>|--workspace <name> <environment> --kind local|deployed --procedure
  <path> [--address <url>] [--run "<command>"] [--required|--not-required]` adds or replaces one environment, keeping the
  others. `daoris driver review <repository> none` writes `false`. `daoris driver review <repository>|--workspace <name>
  --drop <environment>` removes one. `--clear` hands a repository back to its workspace. `daoris driver list` shows the
  rules.
- **The screen:** the workspace's page, Setup → *Defaults*, gains *Review before landing*, beside the line and how work
  lands (D150's UX6g). A repository's page, Setup, gains the same row with *None here* and *Use the workspace's*. Each
  hint is its terminal twin.
- **Ask Daoris:** its `setting` kind takes the `review` door. Its card is applied by the person, as a landing rule's is
  (D110). The room names the verb.
- **What each door says as it is set.** With `required`: *before work here lands, it is shown to you in
  `<environment>` and waits for you to say it is right.* For a local environment: *a set-up step here builds the work in
  its own tree and shows it in Daoris's browser at `<address>`; your own servers and processes are not touched.* With
  `run`: *a set-up step here may run `<command>` in its tree on a port nobody holds, without asking you each time; it
  stops once you have reviewed.* For a deployed environment: *a set-up step here follows `<procedure>` toward
  `<environment>`; each deploy or write there asks your go-ahead once per ask.*
- **The twins:** `DriverConfig.cs` and `driverconfig.ts`, by one table, with the refusals of §1.1 and §1.3 in the same
  words (`.claude/knowledge/twins.md` gains the row).

### 1.8 Nothing set anywhere

No key in `driver.json`, no choice on the ask or the chain: there is no review environment. The intake composes no
set-up step, *Accept…* is offered as today, and *Accept automatically* lands at done as D145 says. The Setup row reads
*None: work is offered to land once its quest is done*. The intake's room says *no review environment* for the
repository, and the review screen shows nothing new.

## 2. The step

### 2.1 How a chain gains it

A **set-up step** is a quest. It is a chain step whose `review` names an environment: *show `{parent}` in `local` for
review*. It goes to the same repository as the step before it, directly after that repository's last step in the chain,
so its tree grows from the branch that step worked on (D82). It therefore holds exactly the work that was built and
checked. The exchange judges its shape at composition (D65 §4, INT5): the environment's name shape, never a production
name, the same repository as the step before, and no set-up step on a chain whose choice is `off`.

- **The intake composes it** where the level for that repository (§1.4, rows 3 to 6) says review. The driver renders
  the room's table with each repository's rule: its environments, which one is the default, and whether it is required.
- **The person's press composes it**: *Set it up in `<environment>`* at the gate or on a quest's page. It publishes the
  step, in Daoris's fixed words, following the chain's last quest in that repository. This is the door a chain needs
  when no intake ran, the intake proposed none (§1.6), or the person changed their mind. It needs the exchange to take
  `follows` at publish, which D149 point 1 allows: *a door that sets it later joins this rule*.
- **The planner sits a set-up step it cannot honestly start**, saying why and starting no session: no rule here names its
  environment, the procedure's path is not in the step's tree, or, for local, no address declared or no window here to
  show it in (a headless loop, where D78 §3.6 withholds the browser).

One set-up step per repository per chain. Work that must be reviewed together across two repositories, a front end and
the service it calls, is left out of this design (§5.1).

### 2.2 What it is told

The step's instruction is `TargetPrompt`'s, gaining a section for a set-up step. The driver hands the environment from
the rule as it stands at the start, as it hands the landing branch (`Driver.LandsAsAsync`). The section says:

- the environment, its kind and its address, and for local, which tab of Daoris's browser is the step's (§2.3);
- **the procedure's path. Read it first; it is this repository's own; follow it and never go past it.** Where it does
  not cover something, say so under *Needs you* (KNOWUSE1c) rather than improvise;
- that the tree holds the chain's work, and the step adds none. It makes no commit for the set-up itself. Files the
  procedure makes, such as a build's output, stay where the procedure keeps them, which is usually ignored by git;
- **never stop, restart or take over a process or a port of the person's.** Doing so is a go-ahead at the least, and the
  routes in §2.3 need none;
- what it may do unasked, what needs a go-ahead, and what it never does (§2.5);
- to capture proof of what it showed (D146 §9);
- to say the set-up through `review_ready` (§2.6), then close its quest done;
- that the person's *not yet*, and *Show it again*, come back to it as a new turn (§3.4).

### 2.3 Local: the step shows it, in Daoris's browser

**The review act is showing, and the person's part is looking.** The step does what a harness's browser extension does
for a person at their desk. It drives the browser the person is already in, opens the app where they normally use it,
goes to the change, does the thing the change is about, and leaves it there. Daoris's browser is that browser. The
person signs in to it once (D78), and a driven session is handed it through the in-app browser plugin's server (D78,
D99, `${browser}`).

**1. The step's tab.** When the step starts, the shell brings the browser up and opens a tab for the step, titled for its
quest, and brings it forward. The instruction names that tab. A tab an agent opens over CDP has no window (the in-app
browser design, §3.2), so the step works in the tab the person can see. The *driven by* chip on the strip names the
session while it drives (BRW8).

**2. Build, not serve.** The session follows the procedure to build the branch's app in its own tree, pointed at the
development environment's data as the procedure says. It never writes a credential: the sign-ins the app needs are the
person's, in that browser. A missing sign-in is a *Needs you*. Nothing listens on a port.

**3. Served where the app normally runs, by interception.** The session asks Daoris to serve the build to its tab
(`review_serve`: a folder in its tree, and the environment's `address`). The shell then holds request interception on
that tab alone, over its own CDP connection to its own browser:

- a request under the address whose path names a file in the folder gets that file;
- any other page request under the address gets the folder's `index.html`, so the app's own routes work;
- every other request, including the app's calls to the development environment's data, goes where it would go for the
  person.

Requests in that tab never reach whatever holds the address on this machine. The person's own dev server keeps serving
their own tabs and their own checkout, untouched. Serving at the usual address, rather than at a new one, is what keeps
the app's own calls working: the development environment already answers that origin, in its sign-in's return address
and its cross-origin rules. The address must be the rule's, so a step cannot serve over another
site, a sign-in page above all, in a browser the person is signed in to. The folder must be inside the step's tree:
no `..`, no link, and regular files only, read as `${proof}`'s names are (D146 §11).

**4. Shown.** The session navigates its tab to where the change is and performs the interaction: it opens the report,
turns on the setting, and reaches the state the requirement names. It captures that state as proof, and leaves the tab
there. An interaction that would save to a shared environment's data is a go-ahead (§2.5). Otherwise the session stops
just before it, and says so in what it showed.

**5. Said.** Through `review_ready` (§2.6) the session reports what it showed (`shows`), the exact address the tab is
at (`look`), and how to show it again by hand (`again`: the address and the clicks, in a few lines). Then it closes its
quest done and ends.

**Why Daoris holds the serving, not the session.** A session's interception lives in its browser server's connection,
and that ends with the session, as all its background work does (D105 §3). The next reload in that tab would then load
whatever holds the usual address, which is the person's own dev server, serving another branch. It would do so without
a word: a silent wrong result, on exactly the page the person is judging. Held by the shell, the tab goes on showing
the branch's build through reloads and the app's own navigation until the review ends.

**Kept shown, then stopped.** The shell keeps serving to the tab until the person says *reviewed* or *not yet*, presses
*Stop showing*, closes the tab, or the step's tree goes or the shell exits. The strip's chip (§3.3) says when the tab is
no longer served. A closed tab is reopened by *Show it again* (§3.3). Nothing of the person's is stopped by any of it.

**A process of its own.** Some work cannot be shown from a build folder: a server-side change, or an app that renders
on a server. For that, a local environment may carry `run`, the person's command (§1.3). Daoris starts it in the step's
tree as a **review run**: a process of its own, with the tools' environment (D121), its output kept under the home and
shown in the console panel. It runs only on a port nobody holds. If the command's port is held, by the person's own
server or anything else, the run is not started, and the step's page says which port is held. Freeing it is the
person's act, and a session's go-ahead at the least. The run is ready once its address answers over HTTP, within five
minutes, and the session then shows the work in its tab as above. Daoris never runs a command a session names on its
own, and never one it reads from the repository's files: the first is a side channel past the harness's permission
rules (`file-tool-discipline`), the second a guess. A command the step proposed runs at the person's press, which shows
it with the procedure line it came from. It is offered only where it stands verbatim in the procedure file at the
step's commit, whitespace aside, as a requirement's quote stands in the ask. *Keep it for `<repository>`* writes it to
the rule. Daoris stops the run's own process tree and nothing else, when the review ends.

**With no window here** (a headless loop), a local step sits (§2.1). The person can still review by hand and record
it (*I set it up myself…*, §3.3).

### 2.4 Deployed: set up by the documented route

The session follows the procedure toward the named environment: a deploy, a pipeline run, or the configuration the
work needs, written through the environment's API. Each of those acts is outward, so each is a go-ahead once per ask
(KNOWUSE1a) unless the repository's standing answer gives it (KNOWUSE1b). The session then shows the work there, in its
tab of Daoris's browser, as §2.3 step 4 does, captures proof, and says the set-up. There is no serving and no run, so
nothing of Daoris's stays behind. A tear-down, if the procedure has one, is the procedure's business.

**A procedure that needs the branch on a remote** cannot be followed by a session. Sessions never push (D37, the
`no-push` default, D122 point 7). The step says so under *Needs you*, naming the procedure's line. The person then does
that part and records the set-up themselves (*I set it up myself…*, §3.3). A plugin that does it for them is a later
design, once a real procedure shows the need (§5.1).

### 2.5 What the step may do unasked, what needs a go-ahead, and what never

| | Local | Deployed |
|---|---|---|
| **Unasked**: inside its tree, its tab and this machine | Read the procedure and the repository. Install and build as its permission scopes allow (D72, D122's declared safe work). Ask Daoris to serve its build to its tab. Drive its tab: navigate, read, and click through the app as far as reading goes. Capture proof into `${proof}` (D146) | The same, without the serving |
| **A go-ahead, once per ask** (KNOWUSE1a), unless the repository's standing answer gives it (KNOWUSE1b) | An interaction that saves to a shared environment's data, such as the new setting entered in dev. Stopping, restarting or taking over a process or a port of the person's, which the step should never need | The deploy. The configuration written there. A pipeline run. A sign-in |
| **Never** | Production. A push. A merge into a line. A commit for the set-up itself. Writing outside its tree except `${proof}`. Serving to an origin that is not the rule's address | The same |

- **Production is refused by construction, not only by words.** A go-ahead asked by a set-up step's session whose
  place reads as production is refused by the go-ahead door (`SessionLedger.AskGoAheadAsync`, by the place table
  `GoAheads.cs` already has), and the session is told to stop and say so.
- **The person's processes and ports are theirs.** The routes in §2.3 need none of them: the serving needs no port,
  and a review run takes only a free one. A step that still finds one in its way says so under *Needs you* and asks
  the go-ahead, naming the process and why. It never acts first.
- **A go-ahead is a yes to an act, not a permission rule.** A command the session's scopes refuse is still refused
  (D52), and widening a scope is the person's yes to a proposal (D74). The step needs both for an outward act, and this
  design adds neither silently.

### 2.6 Saying what was shown, and what is kept

- **`review_serve`** and **`review_ready`**, two connector tools allowed only to a set-up step's own session (its
  connector knows its quest). `review_serve` takes the folder and the address, and answers once the shell serves.
  `review_ready` takes:
  - `look`, the address the tab is at;
  - `shows`, what it showed and what to look at, at most 300 characters;
  - `again`, how to show it again by hand, at most 600 characters;
  - for a review run, `run`, the command, quoted from the procedure.

  Either may be said again in a later turn. The step's done is refused until one set-up has been said, so a done always
  has one.
- **The commit is read, never reported.** When the session ends, and at the end of each later turn, the driver reads
  the step's tree `HEAD` and posts each set-up it said, with that commit, through a local door. This is how EVID1 posts
  what it read (D144 point 3). The set-up is then an operation on the quest: `look`, `shows`, `again`, `served` (the
  folder `review_serve` named, as a path in the tree) or `run`, `commit`, the session, and when.
- **The done closes held, *waits for your review in `<environment>`*,** a new cause of D133's hold (`Quest.Hold`). It
  comes after a departure and after evidence, so their order is unchanged. Its next step waits, the quest stays on the
  outstanding list, and its ask is not done (D133 §4, as D144 left it).
- **Proof outlives the environment** (D146). The captures are kept on the machine and shown beside the set-up. Local
  serving stops once reviewed, and a dev environment is redeployed by someone else tomorrow. The captures and the
  person's words are then what the record keeps of what was looked at.

## 3. The gate

### 3.1 What waits

Wherever the level (§1.4) says review for a chain's work in a repository, that work waits for the person's *reviewed*.

- **The press.** The session's review, its page header and What needs you show *Waits for your review in
  `<environment>`* where *Accept…* would be, with the state of the set-up (§3.2). `LAND_SESSION_TREE` and
  `daoris-driver trees land` refuse with the same sentence and exit 1. A merge rule waits as a branch rule does.
- **Accept automatically** (D145). The look keeps a due session due with a new code, `unreviewed`, reads it again at
  every look as it does `held`, and lands it at the first look after the review. The conversation is told once, as a
  landing note (`landing.unreviewed`).
- **An advance** (D145 §3, D149) waits the same way. Work added after a review is work the person has not seen run.
- ***To review* keeps its rule** (D126 §2.1): work no branch of the person's holds. A session waiting here is in it,
  and says what it waits for.

### 3.2 What lets it go

**The person's *reviewed* on a set-up whose commit holds the work that lands.** The gate reads the chain's set-up step
in that repository, takes its newest set-up the person reviewed, and asks git whether the landing's tip is that set-up's
commit or an ancestor of it (`merge-base --is-ancestor`). A fact, with no model. A *reviewed* of an older set-up does not
let newer work through: the gate says *what you reviewed does not hold these commits* and offers *Show it again*. The
level's own row 1, the person's *skip*, also lets it go.

The states the gate shows, each with its doors:

| State | Says | Doors |
|---|---|---|
| No set-up step | *Not shown for review*, with the intake's proposal and the changed paths where there are any | *Set it up in `<environment>`*, *Skip review for this work…* |
| The step is open, taken or working | *Being set up in `<environment>`*, and the *driven by* chip while it drives the browser | The step's page |
| Shown, waiting for the person | *Shown in `<environment>`*, what it shows, and whether the tab is still served | *Open*, *Reviewed*, *Not yet…*, *Show it again*, *Stop showing* |
| Reviewed, but newer work since | *What you reviewed does not hold these commits* | *Show it again*, *Skip review for this work…* |
| Reviewed | Nothing: *Accept…* is back, or the look lands it | |

### 3.3 How the person says *reviewed*, or *not yet*

**The person's alone.** No connector tool and no Ask Daoris card gives a verdict, as none answers a go-ahead (KNOWUSE1a).
Ask Daoris is exempt, with its reason: the verdict is a look Ask Daoris cannot have taken (D110).

- **Beside the browser, never inside it.** The person reviews by looking at the step's tab. Daoris's browser has no
  bridge and is never given one (D78, BRW7 §2). Every page in it is one an agent can drive over CDP, so a *Reviewed*
  drawn in the page could be pressed by the session under review. Neither the engine's window nor Edge's is Daoris's to
  draw in (BRW8). The door is therefore the app strip, beside the browser's door and its *driven by* chip. While a
  set-up waits for the person, a chip reads *In review: `<quest>`*. It opens a small panel with what the step showed,
  *Open* (brings the step's tab forward), *Reviewed*, *Not yet…* and *Show it again*. The person looks in the browser
  and answers on the strip of the window beside it.
- **The quest's page.** A set-up step's page gains *Review in `<environment>`*. It lists each set-up, newest first: what
  it shows, the address as a link that opens the step's tab, how to show it again, its captures (D146 §12), its commit,
  and whether the tab is still served or a run is running, with its output. Under the newest come *Reviewed* and *Not
  yet…*, each with a box for words. *Not yet* needs words, because they are what the session acts on.
- **What needs you** lists a waiting set-up, oldest first, with the same doors. The ask's page shows its chains' reviews.
- **The terminal:** `daoris-driver quest review <quest> reviewed ["…"]`, `… not-yet "…"`, `… show`, `… stop`, and
  `… set-up --look <url> ["…"]` for the person's own set-up. Each prints what was shown, how to show it again and what the
  gate now says.
- ***Show it again*** has two halves, and the first needs no session. The shell serves the set-up's folder to a tab
  again and opens it at `look`, which takes a press and no model. *Show it again with the interaction* sends the step's
  session fixed words, which reopen it in its tree (D137), as *Capture proof* does (D146 §9). It serves, navigates and
  performs the interaction again, and says a new set-up. Either works while the step's tree is here, which it is until
  the work lands.
- **I set it up myself…** For a procedure only the person can follow, or a machine with no harness or no window, the
  person records a set-up of their own on the step: where to look, their words, and the commit, which is the chain's
  tip in that repository as Daoris reads it at the press. An open step closes done on their word (QUESTCLOSE1's door),
  and then waits for their *reviewed* as a session's set-up does.

### 3.4 *Not yet* goes back to the session as a new turn

The person's words are kept on the ask, verbatim, as the person's (DRIFT1a, a new word kind `not-yet`, with the set-up
it answers). They are then said to the set-up step's session as its next turn (D137 §3). Its record reopens in its tree,
and its harness conversation resumes, so it knows what it showed and where. Where it cannot resume, D137 §4's fallbacks
apply. Every later session on the ask is handed the words too (DRIFT1b). The tab stops being served when *not yet* is
said, so the person is never left looking at a build that is about to change.

The step's instruction says how to take them:

- **What the words name about the showing or the environment** (the wrong page, an interaction missed, unset data in
  dev) is put right there, under the same go-aheads.
- **What they name in the work** is corrected in this tree, which is on the chain's branch, and committed. The closing
  note says it was a correction under the person's words. D133 §5 wants a correction to reopen the quest that built the
  work, and that is DRIFT1e's design, not built. Until it is, the set-up step is where the tree and the context are. When
  DRIFT1e lands, a correction to the work goes there, and this paragraph follows it.

Then the session builds again, shows it again, and says a new set-up. Its commit holds the correction, so the gate needs
the person's *reviewed* on that set-up. A reopened run on a closed quest leaves the quest unmoved (D137 §5). The set-up
step stays done and held while this happens. Its new commits land with the chain once reviewed, as an advance (D145 §3).

### 3.5 What is recorded

- **On the quests, as operations that travel with them** (D68). One is the set-up, on the set-up step (`look`, `shows`,
  `again`, `served` or `run`, `commit`, the session or *the person*, when). The other is the verdict (`reviewed` or
  `skipped`, the set-up it answers and its commit, the person's words, when): on the set-up step, or for a skip with no
  set-up step, on the chain's last quest in that repository. These are two new kinds. Each goes into every place
  `.claude/knowledge/quest-operations.md` lists, census first, and an older build refuses each as not whole (D144 on
  D68).
- **A *reviewed* does not accept a departure, and an accept does not review.** D133's yes (`Accepted`) lifts a
  departure's hold or evidence's, and it no longer lifts a review's. A *reviewed* lifts only the review's hold. Each yes
  is said for what it is.
- **The person's words** are kept on the ask as a word of their own kind (`reviewed`, `not-yet`, `skipped`). An older
  build passes a word of a kind it does not know (DRIFT1a), and every later session on the ask is handed them.
- **On the landing record** (D102, D143): `reviewed`, with the environment, the set-up's commit and when, or `skipped`
  with the person's words. `trace`, `trees land --plan` and the review's note (D113 §3) say it.
- **In the machine log** (D94): `review.shown` (kind, served or run, seconds until shown), `review.verdict` (`reviewed`,
  `not-yet`, `skipped`), and `review.held` (the gate's state code). Codes and counts only.
- **What crosses machines** (D47): the operations, with no machine path and no output. A local `look` travels as *local,
  on `<machine>`*, because its tab is on that machine. A deployed `look` travels as given. Captures stay on the machine
  (D146 point 7).

### 3.6 The task level at the gate

- ***Skip review for this work…*** asks for the person's words, optional, and shows the intake's proposal and its reason
  where there is one. It records `skipped` on the chain's last quest in that repository, which is row 1 of §1.4. A
  set-up step not yet started is abandoned with those words (D132). One that is running is stopped first, and its tab
  is no longer served.
- ***Set it up in `<environment>`*** publishes the set-up step (§2.1). Where the repository declares several
  environments, the press offers each, the default first.
- **The chain's choice on the quest's page** (§1.5) is the same act at another time: `off` before the gate is the skip,
  and an environment's name is *Set it up* once the work is done.

## 4. What the no-model tier does

`model-decoupling`: the feature is said with no model in it, and the floor is the facts.

- **The step is a quest.** Its words are fixed, its shape is judged by the exchange, and its place in the chain is
  `follows`. The person's *Set it up* publishes it without an intake.
- **The gate is a recorded fact.** The level is a table read from `driver.json`, the ask and the chain. The set-up's
  commit is read from git. Containment is `merge-base --is-ancestor`. The verdict is the person's press. No model reads
  any of it.
- **Keeping it shown needs no model.** The shell's serving, the tab, the chip, *Show it again*'s first half and a review
  run on the person's command are all Daoris's own acts on facts: a folder, an address, a port that is free or held.
- **What needs a model is said as such.** Following a procedure written for people, and driving the app to the change,
  is a session's work. With no harness, a set-up step sits, and its page says that no harness takes it here; the person's
  *I set it up myself…* is the floor. The intake's proposal is a reading, and it names the tier that made it. The floor's
  own report beside *Skip review* is the changed paths (*changes only Markdown files*). It is labelled *by changed paths
  only*, and it never decides (D54).

## 5. What is rejected, and what a workspace with none sees

### 5.1 Rejected

- **Daoris deploying by itself**, as a deploy runner in the driver. It would carry a platform's deploy in core, which
  D87 and D100 kept to plugins. It would act outward on Daoris's own authority (D37). And it would guess a procedure the
  repository owns (D32: the *why* behind a codebase does not travel).
- **A gate that only reads CI**: a pipeline's result, or a check on the pull request. CI runs the checks the repository
  wrote. The owner's case passed every check an agent ran, and its browser check stubbed the environment. What was
  missing was a person seeing it run. CI's result is the platform's, for a plugin to read (D148).
- **Review on by default for every workspace.** A documents-only repository has nothing to show, and a workspace with no
  environment would hold every landing. Nothing set anywhere stays today's behaviour (§5.2).
- **Local as a fallback for when no deployed environment exists.** Most work is reviewed on the person's machine, and the
  owner said so. Local is the default kind.
- **Stopping or taking over the person's processes or ports**, to serve the branch where the app normally runs. That is
  what the person refused. The work is served to the step's own tab instead, and a run takes only a free port. A step
  that needs more asks a go-ahead (§2.5).
- **Leaving the review for the person to set up**: a command to run and an address to open. The person asked to be
  shown, as a browser extension shows them.
- **The session's own interception, left to die with it.** The first reload would show the person's own dev server's
  build, without a word (§2.3).
- **The set-up session staying alive to keep it shown.** BGWAIT1 would hold its turn until the timeout failed it. It
  would take a slot under the cap for hours. The native door ends background work with its turn, and the headless loop
  keeps no streams.
- **Daoris running a command a session names**, or one it reads from the repository's files (a `start` script). The
  first is a side channel past the harness's permission rules. The second is a guess.
- **A *Reviewed* inside Daoris's browser**, on the page or its frame. An agent driving the browser could press it (D78).
- **Serving to any origin the step names.** The person is signed in to that browser, and a step serving its own files
  over a sign-in page, or any site but the app's, is the risk D78 §3.4 names. Only the rule's address is served.
- **A set-up step that does not gate**: shown, but landing as before. A showing nobody has to look at races the landing,
  which is the owner's case with an extra build.
- **The intake switching review off on its own reading**, or **parking to ask** about every change to a document. The
  first lowers the person's gate on an agent's judgement (D74). The second stalls work the gate settles at the end with
  one press (§1.6).
- **Holding the build's done instead of the landing.** The build's done is true. Holding it would stop the chain before
  its set-up step could run.
- **A park as the gate** (D65's *a decision gate is a parked session asking*). The verdict would be the agent's report
  of the person's answer, and the gate must read the person's own act. The parked session would also hold its record
  open and its tree, and need a resume to close.
- **A *reviewed* of an older set-up letting newer work through.** Work the person never saw run would land.
- **The task level declaring its own environment.** Its procedure would be nobody's document.
- **A model judging the screenshot or the shown page** (D24, D54). The look is the person's.
- **Left out, not rejected**: a review across two repositories at once; a landing plugin writing *reviewed in dev* into
  its pull request (D146 rejected attaching proof, and this would need the frame to grow); a plugin that sets work up on
  a platform (a `review/set-up` point, D64 §4), designed once a real procedure needs a push or a pipeline only the person
  can run today; serving to the person's Edge, when the machine's browser is Edge (BRW12), which needs the same
  interception measured there; and a manifest `documents` role naming the procedure, which is a canon change (D122).

### 5.2 A workspace with no review environment

It sees today's behaviour, and the design says so rather than leaving it implied. No set-up step is composed. The gate
never holds. *Accept…* is offered when a session's work can land, and *Accept automatically* lands at done. The review,
the quest's page and What needs you show nothing new. The only new words are where the person would set one up: the
Setup row's *None: work is offered to land once its quest is done*, and the room's *no review environment*. A task that
asks for review there is refused at the door that sets it, naming the repository and the Setup row that would declare
one.

## 6. The owner's work workspace, worked through

1. **The reporting app's repository documents how to build it against dev's data.** If its README says so, that path is
   its procedure. If it does not, the first act is a quest to that repository asking for the document. That is its own
   work, and until it lands no rule can name the path. Whether its documents cover a dev deploy was not read here.
2. **The person sets the rule.** On the workspace's page: *Review before landing*, required, with a local environment
   named `local`, its procedure `README.md`, and the loopback address the workspace's front ends normally run at. A
   repository whose app runs elsewhere gets a rule of its own with its address. Any infrastructure-plan repository in
   the workspace is set to *None here*.
3. **A ticket like the case.** The intake composes build, then browser check, then *show it in `local` for review*. The
   browser check may still stub what it must, because it is an agent's check and no longer the last word. The set-up
   step's tree grows from the check's branch. The shell opens the step's tab in Daoris's browser. The session builds the
   app against dev's data and asks Daoris to serve the build to its tab at the usual address. The person's own dev
   server on that address goes on serving their own tabs. The session opens the report, turns the new setting on, and
   captures it. If showing the setting needs it saved in dev's data, that is a go-ahead once on the ask, unless the
   repository's standing answer already says *dev writes allowed*. The session leaves the tab on the report and says
   what it showed. The person looks at the browser, then presses *Reviewed* on the strip's chip. The tab is no longer
   served, the hold lifts, and *Accept…* is offered, or under *Accept automatically* the look lands it and the plugin
   opens the pull request.
4. **If the person had said *not yet: the label still reads the old name***, the tab would stop being served and the
   words would reach the set-up session as its next turn. It would correct the label in its tree, commit, build, show it
   again and say a new set-up. The gate then waits for a *reviewed* on that set-up, whose commit holds the correction.

## 7. A canon principle

**It belongs in canon.** The failure is project-agnostic and silent: gates green, a diff that reads right, an agent's
check that stubbed the environment, and work offered to land that nobody saw run. It is `autonomous-development`'s own
subject, since the person verifies the outcome and that knowledge says what the outcome is. It needs no service, so it
passes D48 §2a's test.

**Where**: `canon/core/knowledge/autonomous-development.md`, *How to apply*, directly after *Done means gates green plus a
reviewable record*. **The words:**

> - **See it run before it lands.** Green gates and a reviewable diff show what changed and that the repository's own
>   checks pass. They do not show the change working where it will be used, and an agent's check that stands in for
>   the environment, with a stubbed answer or a mocked response, proves the stand-in. Where the work has a running form
>   a person can look at, show it to them there: on their own machine, where the app normally runs, or in a shared
>   development environment reached by the route the repository documents. Offer the work to land only once they say
>   it is right. Showing it never stops or takes over what the person is running, and production is never that place.

**What it costs**: knowledge is read on demand, so the always-loaded core (CANON7) does not move. The frontmatter's
`enforces` is unchanged, so the index row does not move either. The change re-syncs this repository and `examples/` in
the same commit, and `canon/CHANGELOG.md` says why. That is the parent's call, and this branch does not edit `canon/`.

## 8. Against the decisions

- **Amended.** D87: a landing waits for the person's *reviewed* where a review is required, at a press and at
  `trees land`. D145: the look keeps a due session `unreviewed`, an advance waits the same way, and the landing record
  gains `reviewed`. D65 §4: a chain step may be a set-up step, and a door may publish a step after a chain's close
  (D149 point 1). D133 §4: the hold gains a cause, and its yes no longer lifts a review's hold. D137: the person's *not
  yet* is a word with a reach of its own on a set-up step's session. D68: two operation kinds. D78: the shell holds
  request interception on a set-up step's tab, and opens that tab for the step. D146: a set-up step's captures are
  shown beside its set-ups. D135: a go-ahead from a set-up step for production is refused. D110: the verdict is exempt
  from Ask Daoris, and the `setting` kind takes `review`.
- **Standing.** D37 (no push by a session, and production stays the person's), D46 (the driver moves no quest: it
  posts a fact it read and runs the person's command, as EVID1 and a landing plugin do), D51 rule 6 (nothing merges
  itself), D52 and D72 (a session's commands stay under its scopes), D78 (the browser has no bridge), D82, D105 §3 (a
  session's background work ends with it), D126 §2.1, D133 §1, D144 and D146 as each says.

**What the checks do not cover.** Documents only. The landing code, the hold, the go-ahead table, the browser's design
and BGWAIT1 were read at `9d174f6f`. Nothing was served to a tab, and no session followed a procedure or drove the
browser for this. Whether the shell's interception on a tab and a session's browser server on the same tab coexist over
CDP is to be measured before REVIEWENV1d is built, as D78 measured its browser first. Whether the reporting app's
repository documents a build against dev's data, and whether its dev route needs a push, was not read. Whether the app
strip's chip suits the browser's window side by side was not looked at. `verify` checks D154's place, that it names what
it rejected, and this design's links, and none of their words.

## 9. The build

Each row ships both doors for its part (D50), and names the Ask Daoris door or its reason (D110).

- [ ] **REVIEWENV1a — the review rule** (cli, driver, modules, web-settings, service). `reviews` and `workspaceReviews`
  in `driver.json`: both twins on one table, the refusals, each door's sentence, `daoris driver review`, the Setup rows,
  Ask Daoris's `setting` door, the room's table. Contract: §1.1–§1.3, §1.7–§1.8. Proof: `ReviewRulesTests` and
  `driverconfig.test.ts` cell for cell, module tests, vitest, `HelpSettingProposalTests`.
- [ ] **REVIEWENV1b — the review on the record** (service; after a). Ask and chain choices (the person's, or quoted);
  the intake's proposal; the set-up step's shape; its hold; the set-up and verdict kinds; both tools; the verdict's
  door; no production go-ahead from a set-up step. Contract: §1.4–§1.5, §2.1, §2.5–§2.6, §3.5.
  Proof: the census, `ReviewStepTests`, both hosts, `McpToolsTests`, `GoAheadTests`.
- [ ] **REVIEWENV1c — the step and the gate** (driver, modules; after b). The level's table, the planner's sit, the
  step's instruction, the set-up's commit posted, the gate at every landing door (`unreviewed` at the look), *not yet*
  to the step's session, the landing record, the log. Contract: §1.4, §2.1–§2.2, §2.6, §3.1–§3.4. Proof:
  `ReviewGateTests`, `ReviewLandingTests` (`Process`), the golden.
- [ ] **REVIEWENV1d — shown in Daoris's browser** (modules, driver; after c). Measured first: the shell's interception
  beside a session's browser server. Then the step's tab, `review_serve` at the rule's address only, kept
  served until the verdict, the chip, *Show it again*. Contract: §2.3 steps 1–5, §3.3. Proof: the measurement,
  `ReviewServingTests`, the person's own server never reached.
- [ ] **REVIEWENV1e — a process of its own** (driver, modules; after d). The review run on the rule's `run` or the
  person's press, the procedure-quote check, *Keep it*, a held port refused, readiness, output, its own tree stopped.
  Contract: §2.3's last part. Proof: `ReviewRunTests` with a stub server, a held port, and a stop taking nothing else.
- [ ] **REVIEWENV1f — the intake** (driver, service; after b). The room's table, a set-up step composed where the level
  says review, the choice set only on a quote, a proposal otherwise, and no step for a proposed `off`. Contract:
  §1.5–§1.6, §2.1. Proof: `IntakePrompt`'s golden, the service's quote refusal, a stub intake in the driver's tests.
- [ ] **REVIEWENV1g — the screens** (web-shell, web-settings, modules; after c, d). *Review in `<environment>`*, the
  gate's states in place of *Accept…*, the strip's chip, What needs you, the composer's and the ask's choice, the
  glossary's terms. Contract: §1.5, §3.1–§3.3, §3.6. Proof: vitest, stories, `i18n:check`, `names:check --strict`,
  `HelpCoverageTests`, the look in both themes and 中文.
- [ ] **REVIEWENV1h — the family rehearsal phase** (tools, examples; after c). An example declares a local environment.
  A stub chain says a set-up; its landing is refused until `quest review … reviewed`; *not yet* reaches the stub's next
  turn; a later commit holds again; a repository set to none lands at once. Contract: §3. Proof: `rehearse:family`'s
  new phase.
- [ ] **REVIEWENV1i — see it run, in the canon** (canon, examples; the parent's call). §7's words in
  `autonomous-development`, re-synced here and into `examples/`, with the canon changelog's reason. Contract: §7.
  Proof: `verify`'s `check`, and `rehearse:family`'s example sync.

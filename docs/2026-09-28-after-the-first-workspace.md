# After the first real workspace — what it showed Daoris lacks (study)

**Carried by:** WSR1–WSR3, HELP1, SESS1, MAP4 in `TASKS.md`. A study, input to the decisions each row
names; not yet a contract.

**Date:** 2026-09-28 · **From:** the owner, after the first goal ran end to end on a real, shared
workspace (FG5):

> *"I also found issues after the [first workspace] work, which points out things that daoris leak
> of. 1. workspace need rules, … since its a shared repo so we need to do PRs so instead merge to
> master it need to create feature bransh … set at daoris workspace level and manage them (should be
> easily setup by user or and we need to introduce workspace rules) 2. after merge to master or
> feature brach we should cleanup daoris branches (those hash key work branches) 3. master branch can
> be different branch so this more focus on default branch or setup by user 4. for all configuration
> or setup or how to use daoris we should also have a chat feature so user can directly "ask" daoris
> to help for setup or start something (a chat window or button design ui/ux will be needed) 5. we do
> need to improve the session ui/ux (mostly for session log display and working relationship) 6. the
> map view for relationship is not really done yet right? and the view need to be improve at ui/ux.
> Also you can add other requirements for all those features if you think will help ui/ux
> improvements."*

Each part below says what exists today (read from the code, 2026-09-28), the gap, the requirements,
and the calls that are the owner's. The order at the end is a recommendation.

## 1. Workspace rules — how work lands (WSR1)

> **Decided and built: D87** (2026-09-28). The owner's call: configurable, no push or pull request by
> default, and that form a plugin's (WSR4). What follows is the study as written.

**Today.** A session works on its own branch, `daoris/s-<id>`, in its own tree (D51). The one way that
work lands is the merge door (`SessionTrees.MergeAsync`): `--no-ff` into the repository's canonical
line, in the repository's own root, which must be clean and already on that line. Nothing pushes, and
nothing opens a pull request: both stay the owner's (D37). The only rules a workspace has are
**permission rules** (D72: what a session may run), at machine, workspace and repository scope.

**The gap.** A shared repository takes work through pull requests into a team's branch, never a merge
into its default branch on one person's machine. On the first real workspace, a session's work was
merged locally into the team's feature branch and left unpushed, where that team takes work through
review.

**Requirements.**
- **A workspace has an integration rule**, with a repository override: where a session's work lands
  and how. Three forms cover what was seen:
  1. *merge into a line*: today's door, onto a named branch rather than only the default one;
  2. *a feature branch*: the session's commits carried onto a branch named by a pattern the rule
     holds (for example `feature/<ticket>-<slug>`, from the quest), created from the rule's base
     branch, left for the person to push and open a pull request from;
  3. *a pull request*: form 2, then pushed and opened against the base branch.
- **The rule is easy to set.** A workspace domain in Settings (D75's menus), where the permission
  rules already are, and its terminal twin (D50), with a sentence per form saying what will and will
  not happen. A workspace with no rule behaves as today.
- **The rule reaches every place work lands**: the merge door (renamed for what it does under the
  rule), a chain's next step (D82 starts it on the step before's branch), and the session's
  instruction, so an agent knows its work will go through review.
- **The review screen says where a press sends the work** before the press, and what git answered
  after, as the merge door's sentences do today.

**The owner's calls.** Whether form 3 may push and open a pull request at all. D37 keeps push and
publish explicitly human, so form 3 is a durable authorisation that a person grants per workspace, or
it is left out and form 2 is the most Daoris does. Which host's pull requests (GitHub's `gh`, others)
if form 3 is kept.

## 2. Session branches cleaned up after they land (WSR3)

> **Decided and built: D88** (2026-09-28). What follows is the study as written.

**Today.** Removing a session tree deletes its branch, with `-d` when git proves the work is on the
canonical line and `-D` only when the person forces it. A merge leaves the tree and branch in place,
deliberately (D51 rules 6–7: nothing deletes itself). So branches pile up: FG5 left sixteen empty
ones in one repository before they were deleted by hand, and several with docs commits never merged.

**Requirements.**
- **After work lands, its tree and branch go**, under the workspace's rule: at once, after the person
  says so, or never. Only when git proves the branch's work is on its target. Unmerged work is never
  deleted without the person, and the refusal says what is unmerged.
- **A bulk clean**: every `daoris/s-*` branch and tree on this machine whose work is already on its
  target, and every empty one, listed first with what each holds, then removed on one press. A
  terminal twin says the same list.
- **A branch with commits that never landed** is shown as such, on the repository and the session,
  so work is not lost in a pile nobody reads.

## 3. The default branch is the repository's, and a person can set it (WSR2)

> **Decided and built: D86** (2026-09-28). What follows is the study as written.

**Today.** `WorkingTree.DefaultBranchAsync` asks the checkout (`origin/HEAD`), then tries `main` and
`master`, and the merge door, chains and sync all use that answer. A person cannot say otherwise.

**Requirements.**
- **A repository's line can be set**, per repository, with a workspace default, and it wins over the
  checkout's guess. The guess stays the answer when nothing is set, and the screen says which it is.
- **Everything that reads the line reads it from one place**: the merge door and WSR1's forms, chains,
  sync's ordering, and WSR3's "is it landed".
- Shown on the repository (Projects), with the guess, the setting and where each came from.

## 4. Ask Daoris (HELP1)

**Today.** Everything Daoris can be told has a terminal door (D50), and there are chats with a
repository's agent (D49) and an intake room that reads an ask and routes it (D65). No chat is about
Daoris itself: how to set it up, what a screen means, how to start something.

**Requirements.**
- **A door everywhere**: a button on the app strip, the palette's *Ask Daoris*, and `F1`. It opens a
  conversation in the right dock, so it sits beside whatever the person is looking at.
- **It knows where the person is**: the view, the selected session or repository, the workspace, and
  what the screen is saying (a refusal, a parked card), handed to it as context.
- **It helps with doing, not only explaining**: setting up a workspace, driving a repository,
  writing a rule, starting a task. It does it through Daoris's own terminal doors, which are complete
  by D50, so it needs nothing the person does not already have.
- **It proposes and the person confirms** any change to the machine: the exact command and what it
  will change, then a press. It never runs a session's work itself: *start something* becomes an ask
  or a quest, which the ordinary loop takes.
- **Starter prompts** on an empty chat, from what the machine lacks: no workspace, no drivable
  repository, a harness with no account, a parked session waiting.
- **It is a harness session like any other** (no model call of Daoris's own, D24): the intake's
  harness and account, in a room of its own holding Daoris's docs, allowed to run `daoris` and
  `daoris-driver` and nothing else, under D72's rules.

**The owner's calls.** Which harness and account it runs on when a machine has several. Whether its
confirmations may be remembered per kind of change.

## 5. The session view, again (SESS1)

**Today.** A session is a head (state, repository, tree, tool), the conversation (D76), the timeline
in the dock, and the console with a tab per stream (CONSOLE2). How a session relates to others is
told only in ids: the quest it took, the ask it came from, the step before it in a chain.

**The gap, as the owner put it:** *"session log display and working relationship"*.

**Requirements: the log.**
- **One reading order**: what was asked, what the agent did, how it ended, with the console as the
  raw view one press away. Today three regions show overlapping parts.
- **Long runs read well**: a turn's work folded to a line that counts it, the failures and the
  questions standing out, a jump to the first failure and to the last words, and search within the
  session.
- **A tool's output is readable**: long output collapsed with its size, diffs as diffs, a command's
  exit and duration, and a subagent's card opening its own tab (CONSOLE2).
- **What the session is waiting on** (a person, another repository, a sign-in) at the top, with the
  one action that moves it.

**Requirements: the working relationship.**
- **Where it came from**: the ask and the intake that routed it, the quest and who published it, the
  step before it, the session it carries on from, the answer that resumed it.
- **What it caused**: the quests it published, the questions it asked another repository and their
  answers, the next step of its chain, the branch and commits it left and whether they landed (WSR3).
- **Drawn as a thread**, the chain from the ask to the last step with each session a stop on it and
  this one marked, and every stop pressable. The chain strip (MAP1a) is the start of this.

**Also:** looked at against the first real workspace's actual sessions (long, parked, answered, carried
on, chained), since the example family never produces them.

## 6. The map, finished (MAP4)

**Today.** MAP2 draws a circle's repositories on a ring, a directed arrow per quest direction, and a
dashed line for a shared finding. A repository's code map is MAP3. The design deferred *"layers by
quest flow"* until a circle was too big for a ring. The first real workspace has twenty-nine
repositories, so that circle exists now.

**The gap:** the relationships a person wants to see are not all drawn, and a ring does not read at
that size.

**Requirements.**
- **A layout for a big circle**: by quest flow (who asks whom, in layers), with the ring kept for a
  small one, and pan and zoom, with search to find a repository.
- **More kinds of relationship**, each its own kind of line, switchable: quests (today); asks, from
  the ask through the intake to the quests it became; chains, a quest's `then` steps; shared findings
  (today); and **what depends on what**. The last needs a source: declarations (`owns`, `accepts`),
  code maps' dependencies, or package references. MAP4 decides which, and nothing is guessed from
  names.
- **Time and state**: open only, or everything within a window, so a busy circle stays readable.
- **Live**: sessions working on a node, as the rail says them, and a quest's line lighting while a
  session works it.
- **Looked at with twenty-nine nodes**, in both themes, not the example family's three.

## Order (recommended)

1. **WSR2** (the line), since WSR1 and WSR3 both read it.
2. **WSR1** (the integration rule), with the owner's call on form 3 first.
3. **WSR3** (cleanup), which needs "landed" from WSR1's target.
4. **SESS1** and **MAP4**, each a look pass on the real workspace, as UX5 was on the example family.
5. **HELP1**, after its design note, because it touches permissions, rooms and every screen's context.

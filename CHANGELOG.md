# Changelog

All notable changes to Daoris are recorded here. Entries are written under `## Unreleased` and stamped
with the version and date at release.

## Unreleased

- `daoris-driver ask channel` shows which messages are urgent and, under each message you posted, how far it got in each
  repository it went to: waiting, held, sent, taken, read, partly handed, answered or never reached, as the ask page shows it.
  Your post is still urgent by default. `--ordinary` posts it for each session to read when its turn ends instead.
- The desktop's machine log keeps every line when a second launch writes beside the running Daoris, where before their lines
  could overwrite each other's in the day's file. An error that ends the desktop's application is written there before the log closes.
- Settings → Driver lists the plugins installed here under *Installed plugins*: whether each is on, whether each of its servers is
  approved for Ask Daoris and which writes you accepted, and *Open*, which takes you to the plugin's page, where you change them.

- A usage curve that breaks a rule is refused naming its row, as the curve's editor shows it (*`weekly`'s row 2 has used
  20%…*, 第 2 行), on the Accounts screen, in `profile use --curve` and in Ask Daoris, where it said *point*.
- A driven session whose own conversation cannot be resumed moves to a new session by itself only where waiting would not help.
  A refusal from its agent still carries it on, handed what Daoris kept of it, but a conversation open in another client of its
  agent now waits for you until you say more. Claude Code ending before it reopens a conversation is tried once more first, in a
  conversation as in a driven session, and said apart from a conversation the agent no longer has. A session whose agent is no
  longer installed waits for you to hand it to another, and one whose run could not start ends failed with your words still on
  it. Each move to a new session is said on both sessions, with how much context the old one held and the brief's size.
- A machine fact Daoris could not judge in time, such as the help room's under load, is logged once as late, no longer a
  second time as an error.
- Ask Daoris no longer suggests setting a line for a repository you imported but Daoris does not drive: its *no line*
  starter, like the setup guide, counts only the repositories Daoris drives.
- The desktop window no longer starts out looking like a browser while Daoris's driver is still coming up. Sessions, the
  machine's Settings and the place you last had open stay where they are, and the status bar says the driver is *Starting*,
  where before the window moved to Overview, hid them and said Settings was a browser's.
- A session that goes on from another (a hand-over, a move onto another account, a carry-on, a fallback) can read what its
  brief left out: Daoris's connector gains `line_read`, which pages the earlier parts' words, messages, tool calls, plans and notes
  from this machine's records, a bounded page at a time, and the brief's note of what it left out names it. It changes nothing,
  reads only the session's own line and never crosses the network.
- *What needs you* lists a piece of work only where you can do something about it: work nothing records, or a session's
  tree that only the session branches list on its workspace's Branches can remove, and its Open goes there. Such a row is
  named by the session that worked in the tree, never by the tree's folder path, and says why once in the page's language.
  A branch the clean-up kept says why in your language on its session's page, and says it will try again only where it will.
- A finished session's tree that holds a repository inside a folder the project ignores, such as a test's scratch, is now tidied
  away like the rest of its ignored files, moved aside for 14 days, instead of waiting in *What needs you* as a cleanup nobody
  could do. A tree holding a repository the project does not ignore still waits for you.
- A repository moved to another workspace takes its knowledge with it: the workspace it moved to counts and searches its
  entries at once, rather than after the index is next read. Where the Overview finds the index empty, it offers *Refresh
  the index* there.
- A piece of work you asked for has one name. A session's page heads it with the ask's short title, as its row in Sessions does,
  and a quest an ask becomes takes the ask's short title where none other was given, instead of the ask's words cut short.
- In Chinese, why a quest is sitting is now said in Chinese for every reason the driver gives: the concurrency cap (with its
  number), a session or an earlier quest holding the repository or the tree, a question it waits on, a second opinion, a
  repository not registered or not adopted, and a reopened source. The status bar says the recall tier in the page's language
  too, with what it means in its tip.
- A session waiting on its own background work, such as a long test run, now ends within about a minute once its quest closes,
  is declined or asks another repository, instead of working on until the session timeout. It is recorded completed where its
  quest reached done, its conversation says the work still running ended with it, and that work ends with its process.
- A session running a long tool call, a shell command above all, says it in a few words: its row in Sessions and its page's head
  each show the call's first 40 characters or so, the same call in both, and hovering shows it whole. The row keeps room for
  its kind, its tree and when it moved, and the head's facts stay on one line.
- The Sessions icon counts the sessions Daoris is running for you, and the status bar shows the same number; neither counts
  a conversation you hold yourself, Ask Daoris's included. Something in Sessions waiting on you is a dot in the waiting colour
  beside the count, and hovering the icon says both in words. The window's title keeps to the count *What needs you* shows,
  and the chip beside the browser's button names the browser in Chinese too.
- Agents on one ask now tell each other things without stopping each other. A message an agent posts on the ask's channel is
  ordinary unless it says it is urgent: an ordinary one waits for a natural point, beside what the session is told anyway when its
  turn ends, its next part, or its own read of the channel, and an urgent one reaches a working session at its next step (three of a
  part's posts at most). Every answer an agent gets from Daoris's tools says when messages wait for it. A helper's answer arrives as
  the next turn, or at the next step where the agent asked for it urgently. On the ask's channel and a session's panel, each message
  says what it reached in each repository: waiting, held, taken, read, answered, or never reached once the ask's work ended, and
  *Send to its session* sends one on.
- A change Daoris could not make is said where you pressed it, no longer in a corner toast. Turning Ask Daoris's full access on
  asks once, waits for the driver with *Waiting…*, and says a refusal inside the ask so you can press again; a switch back is said
  under its chip. A refused change to a session pause is said under that row on Setup and in Settings → Driver, with its choice
  still open, and an ask's review choice or *Apply* says its refusal beside the press, keeping what you typed.
- A heavy run is seen running and waiting. A session running its repository's heavy gates says which gate of how many and for
  how long, on its rail line and its head, and its *Details* lists the gates; a session waiting for another's heavy run says
  whom it waits behind, a link to that session, in a neutral colour, never in *What needs you*. A repository's *Details* has a
  *Heavy run* row naming who holds it and who waits, and `daoris-driver run-heavy --look` says the same in a terminal.
- The Quests list shows one row for one piece of work: an ask that became a single quest is that quest's row, named by the
  ask's short title with the ask on its second line (its page opens from the row's right-click menu), and the Asks group
  keeps only asks still waiting on something of their own. A published ask no longer wears the finished colour.
- How many sessions Daoris starts at once on this machine is set in Settings → Driver, beside the strike limit and the
  cool-off, and `daoris driver cap <n>` sets the same number while Daoris runs. It counts the sessions Daoris starts, never
  a conversation you hold yourself.
- Restricted Ask Daoris can change five ordinary settings itself where you grant them: Settings → AI features → Ask Daoris's
  access → *Configured access* → Chosen settings lets you choose notifications, a repository's or a workspace's session
  language, how long a session may run, how many run at once, and the cool-off, each on its own. It changes them with no
  Apply, through the same checks as Settings, on every agent, Codex included; each change is kept in the machine log, and any
  other setting is still proposed for your Apply. The browser's row now shows its plugin's approval as it stands, and its
  *Manage* opens the plugin's page, the one place it is changed. On an agent that takes no permission rules, the browser and
  scoped checkout reads say they are unavailable until Daoris serves a tool that can hold them.
- A session started under full access says so: its head wears a *Full access* warning chip, and its *Details* list each pause
  (permission prompts, intake routing, landing, go-aheads, questions) as it was when the session started. A session started with
  any pause on wears nothing new.
- Nothing in the window says it is empty before Daoris knows. While the sessions, quests or usage are still being read, the status
  bar's count, Overview's quest tiles and an agent's *Usage* show placeholders, not *0 sessions*, *nothing is waiting* or *nothing
  measured yet*. A read that failed says why, and no longer says an ask or a session is gone. When Daoris reads a list again whole,
  *What needs you* and the session, quest and ask lists keep what they showed, dimmed, until it lands.
- Editing a work cycle on Setup no longer starts its plugin steps again where the edit leaves them be. A step you did not touch,
  and nothing it waits on, keeps what it reached, a skip you made and a step it already started; only the step you edited, and
  what follows from it, runs again. Work reached before this release is kept until the first edit after it.

- A session that goes on in a new part, on another agent, on another account or after it was cut off, is handed everything it
  took: every word you said, once, what each turn did, where the work stands and the last check that failed, not just its last
  words. A handed-over conversation opens with *Daoris's brief of the session it continues*, folded. A session's Parts strip
  says how each part began: carried on, retried or handed over.
- A named workflow's version saved as a graph lands its work once: it holds one landing, and at most one second opinion and one
  look. `daoris workflow apply` and `import` refuse one that does not, saying which, and a stored one reads as unreadable with
  the same words. Your look no longer has to be wired after the second opinion: Daoris already holds its set-up while an agent
  reads the opinion, so the look can stand beside it, and both still come before the landing.
- A session's page puts its conversation higher: the lines above it, its details, how the work ran, the folded parts, who it
  worked with, the channel, where the work stands and how it came to be, now sit 8 px apart instead of 16, and so do the page's
  head above them and the conversation below. On a half-width or narrower window the page's head keeps *Stop…* beside the title
  as its square button (its name in its tip), and how the work ran stays one line, its stops cut short with the whole line in
  their tip. A typical session's conversation starts within the top 200 px at every width.
- Your limit of sessions at once counts what the driver starts: a quest's session, an ask's intake, a helper, a second opinion and
  a brainstorm's or a meeting's pass. A conversation you hold, an Ask Daoris conversation or a chat, takes no place under it, idle
  or working, so conversations left open between turns no longer hold quests back. Ask Daoris says so when it describes the limit.
- A session's agent can ask to hand its session over to another agent, with a note of where the work stands that the next agent
  reads as its claim, after what Daoris kept of the session. Each repository's or workspace's *Hand-over* row on its Setup, and
  `daoris driver handover`, say what such an ask does: by default it waits at the foot of the session's page with *Hand over to …*
  and *Not now* (or `daoris-driver sessions hand-over <id> --approve|--decline`); set to take it, the session goes on with the next
  agent in your order once its turn ends, never in the middle of one; set to refuse, the session's timeline says so. A session that
  was itself just handed over cannot ask again before a turn of its own.
- A landed branch's page draws how it landed as a flowchart, folded under *How it landed* above its commits until you open it:
  its tries to land, three alike in a row folded into one you can open, the landing with who accepted it, each later session that
  moved it on, the push, its pull request's latest answer, where its work ended, its clean-up and what is kept for recovery. The
  work's milestone on a session's page and a run's landing step open it. `daoris-driver trace` now names the session a landing was
  made for, each session that moved it on, and the units of work it carries, and finds the landing from any of those sessions.
- Where a piece of work stands reads in one line on a session's record, folded: *Ended · clean-up pending · recovery kept
  until 10 Nov*, which says *blocked* and *held for you* when something waits on you, and opens in place to four facts, the end,
  the result, the clean-up and the recovery, each marked by its own colour: reached and kept in green, a clean-up that waits in
  grey, a blocked entry in red, and a recovery with nothing kept with no mark. A plugin step's attempt held for you stays its
  own row under them. A tidied branch's row says *blocked* and *held for you* in the same line.
- What you say to a session or a conversation while it works is kept on its record before it waits, so a restart of Daoris never
  loses it: once Daoris is back, the same session goes on with your words in the order you said them. A word Daoris had already
  written to the agent when it went away may have reached it, so it is never sent again: its place says *It may have read this; its
  answer was not kept*, with *Say it again* under your own words. Under words the agent took, the conversation shows when they were
  sent, taken and answered, each only where Daoris saw it.
- A driven session never runs without its own tree. Before a session goes on in its tree (your words, an answer, a carry-on
  or a further part), Daoris checks git still has that tree. If the folder was removed and the session's branch is still there,
  Daoris makes the tree again in the same place on that branch, and the session's conversation says so. If the branch is gone
  as well, the session does not start anywhere else. It waits for you, and its conversation says what went and how to go on;
  your words stay waiting on it. Before this, a session whose tree was gone could start with its shell in the repository's own
  checkout, or in a fresh tree without its commits.
- A session's Timeline draws its states as a flowchart, the one it is in lit and going on drawn as a guarded move; a session page draws its parts as a flowchart in the parts strip's place, the strip its list, and Accept's plan as a dashed node of where they would land.
- Signing an account in says where it is and for how long: *Starting*, *Waiting on your browser*, *Checking who signed in*
  and *Joining the lists*, each with its time, the one now counting. Codex's device code shows under the browser's step with
  when it expires, and the link and the code are there again when you come back to the page mid-sign-in. A sign-in that ends
  without signing anyone in says why in its own panel, with *Try again* and *Close*; one you cancel closes saying nothing. Under
  a row of *What needs you* the sign-in shows its step now alone.
- `daoris-driver run-heavy` holds a heavy gate's Jest or Vitest to two workers when the gate runs it through `yarn test`,
  `yarn run`, `pnpm test` or `pnpm run`, as it already did through npm. The flag goes where that yarn or pnpm version passes it
  on to the script; the version comes from the `packageManager` field or the install's own record. Where the version is not
  recorded, or is one whose argument handling was not read, the gate runs as written, and the run says why under the gate's line.
- A driven session's own tree is locked while the session holds it, so a tool that prunes merged branches or removes clean trees
  no longer takes it mid-session: `git worktree list` shows it locked for `daoris session <id>`, and git refuses `git worktree
  prune` and a `git worktree remove` without `--force` on it. The lock is lifted when the session ends, and kept while it waits
  on you; a lock of your own on a tree is never changed.
- A change of a repository's permission prompts reaches a driven session already running there at its next turn, whether made on
  its Setup tab or with `daoris-driver access`. Where you have said something to the session for its next turn, its run ends as the
  turn ends, never mid-turn, your words wait on it, and at the next look it goes on in the same conversation under the new setup,
  saying the change. The session's console says when a change made in the window applies, and the terminal says how a running
  session takes it.
- `daoris driver`, `agent`, `plugin` and `tool`, run while Daoris is open on the same home, hand each change to Daoris instead of
  writing its files beside it: Daoris's window asks you to confirm it, and every window shows it at once. With Daoris closed they
  edit the files as before. A Daoris that is open and does not answer refuses the change, and nothing is written. What needs the
  terminal itself (an agent's install, sign-in, key and pin; a tool's download) still runs there.
- Settings, This Daoris and a repository's or a workspace's Setup use the pane. A setting's value sits beside its words, on one
  label column a card shares, so a card's values form one column the eye runs down rather than sitting at the pane's far edge;
  a row's acts follow its value, and a notice sits under the value it is about. Below about 512 px of card each value sits under
  its words. Cards are 16 px apart. A form (a remote's address and key, a favourite's address and title, a rule, the heavy
  gates) takes the width its content needs up to the pane, by the pane's own width rather than the window's, and stacks its
  fields where the pane is narrow.
- A change of Ask Daoris's access reaches a running conversation at its next turn. Choosing full access or restricted, or granting
  or taking back a housekeeping operation, ends the conversation's agent as its turn ends, never mid-turn and at once where it is
  idle, and your next words go on in the same conversation under the new access, its history kept. Its console says so; the chip
  and *Configured access* mark the change *next turn* until then; and the panel keeps that conversation in view, so your words go
  on in it rather than starting a new one.
- *What needs you* is judged once by Daoris for every window, which then shows it as it moves: no window works it out from its own
  lists, and its count on Overview, on Sessions and on the taskbar button is the same judgement as the band's. Until Daoris has
  judged it, the band shows placeholder rows rather than *Nothing needs you*. A plain browser lists nothing there and says *What needs
  you* is the app's to show. Ask Daoris's conversations are no row of it and no count of the Sessions badge.
- A quest's page draws its statuses as a flowchart, folded under *Flowchart* after the chain until you open it: open,
  taken, done and declined, the one it stands at lit, and under each the moves out of it and what makes each. It is drawn
  left to right on a wide page, top to bottom on a narrower one, and as a list at the side bar's width; *Show as list* gives
  the list at any width and is remembered. The arrow keys move between the statuses and follow a move, and a screen reader
  hears each status with every move into and out of it.
- When you reopen a submission, the session that made it is told even while it still works: at its next step where its door
  takes words then, and when its turn ends where it takes them only then. What Daoris tells a session, a reopen or a step you
  went on with, shows in its conversation as *Daoris's words*, never as yours, and is not listed among your waiting words. A
  reopen that a later submission answered before the session read it is not sent, and the conversation says so. Your own
  words, even when they repeat Daoris's sentence, stay yours and never bring a branch up to date as Daoris's go-on does.
- A quest's flowchart now shows how the quest actually moved, read when you open it, in a browser as in the window: the moves
  it took drawn heavier, who made each and when, and inside each status what happened there, oldest first and three before
  *n more*: a wait, a submission, your verdict, a conflict marked ✕ and its dismissal, and a move that was recorded but not
  applied. A conflict that tried another status is drawn as a refused move toward it until it is dismissed, and a done held
  for you says what it awaits in the colour of what waits on you. A service from before this keeps no moves, and the
  flowchart says so. `daoris-driver trace quest <id>` lists the same moves.
- Words to a working session no longer stop its turn. While a turn runs the box's press is *Send*, which reaches the agent at
  its next step where its door takes words then, and the menu beside it sends at another time: *When its turn ends*, *At its
  next step*, or *Now, cutting its step*, offered only where the agent can be steered and otherwise shown with the reason.
  Each waiting word has the same choices, *Make urgent*, *Make ordinary* and *Send now*. Where a session takes words only when
  its turn ends, they wait for it, said so, and *Stop its turn so this goes next…* beside them stops the turn on your own press,
  asking first; a Claude Code run started with `claude -p` has no such stop and offers none. `daoris-driver sessions say` takes
  `--level ordinary|urgent|now` (`--now` is `--level now`), and `daoris-driver sessions stop <id> --turn` is the same stop.
- Done work and its pull requests move on sooner:
  - Focusing another main window asks about an open pull request as focusing the first one does.
  - A pull request whose line you fetched within a minute of its last check is asked about at the next chance after that minute,
    where the fetch was forgotten until you fetched again.
  - Done work whose hold or review you let go while its session was still running lands as that session ends, instead of at the
    next full look.
  - A `daoris/…` branch Daoris did not record is shown, in Ask Daoris, `daoris-driver work state` and the page, in a repository
    where nothing else of Daoris's is recorded too.
- `daoris-driver sessions archive` and `unarchive` run in a terminal while Daoris is open now ask Daoris's window instead of
  acting beside it: a card under the window's title bar names the press, every value it carries and the program that sent
  it, says Daoris cannot tell whether you ran it or an agent did, and offers *Refuse* and *Confirm*. The terminal says it
  waits, then prints what it printed before; refused, or left two minutes, nothing is done and the command exits 1. At most
  five wait at once. With Daoris closed, the commands act on their own as before.
- The Daoris folder keeps each kind of thing in a folder of its own beside `data/`: your sign-ins in `profiles/`, the agents'
  work in `work/`, what Daoris downloads again in `cache/`, what a running Daoris holds in `run/`, and the logs in `logs/`, so
  `data/` holds only your settings and records and no key. An install's `INSTALLED.md` says what each folder holds and which to
  back up. An install from before moves into them once, at a start after its update has settled with nothing else of the
  folder running, and says what moved; account homes, session trees, rooms, kept files and `data/bin/` stay where they are.
  A `daoris` installed apart from the install and older than it no longer sees the keys, the remotes or the running Daoris:
  update it too.
- The terminal judges a workflow's graph as the desktop does, in the same words, and saves what it judges:
  - `daoris driver landing … --cycle <file>` sets a landing rule's cycle after the landing, and `--no-cycle` takes it away;
    `daoris driver work-cycle <repository> quest|ask <file>` sets the cycle a quest or an ask follows to its end. A cycle that does
    not hold, such as one whose steps wait on each other, is refused naming its first problem, and nothing is written.
  - `daoris driver workflow apply` and `import` take a workflow version that is a graph, and write every version back as it was
    read, so a number written `1.0` or an option's keys in their order stay as you wrote them.
  - `daoris driver list` names a landing rule in `driver.json` that is no rule, such as one holding only `tidy`, and says that work
    there lands as though it set none, where it was skipped without a word.
- A session's state reads the same in the list, its page and `daoris-driver sessions`:
  - An open conversation in a tree of its own offers its work to land in the Sessions list and the terminal once its turn
    ends, as its page already did.
  - A session the words of a reopen go on in is listed as going on, or with what holds them, though a later session of its
    quest ran after it.
  - A session whose start was left waiting by a Daoris process that closed or crashed is ended by the next look, instead of
    holding its repository or Ask Daoris until you stop it.
  - A step stopped by your reopen of the work it grew from is carried on once you go on with it, where the service had
    refused it.
  - A session on an agent run without its protocol that ended its turn while its own background work still ran is carried
    on in the same tree, saying so, instead of waiting on you as if it had asked a question: that agent ends such work with
    the turn.
- Two changes to this machine's driver settings made at once, from Setup, Ask Daoris or the terminal, no longer lose one:
  each waits for the other's write, and two repositories made drivable, held or given trees from two places at once are
  both kept. Plugins, permission rules and API keys wait for each other's writes the same way. A repository named twice in
  different case in `driver.json` (a line, reading or writing across, a forgiven quest) is now read as first written by
  both the desktop and the terminal.
- Restricted Ask Daoris can clean up Daoris's own branches and trees where you grant it: Settings → AI features → Ask Daoris's
  access → *Configured access* → Daoris's housekeeping lets you choose to inspect what the cleanup would remove, clean up the
  branches it clears, and discard a session branch by name, each on its own. It does these through Daoris's own tools, on
  every agent, Codex included: each branch is judged again as it goes, nothing is forced, a branch a running or waiting
  session holds stays, and only repositories of the conversation's workspace are reached. A grant takes the conversation's
  next start; taking it back refuses its next use at once.
- Under a repository's asking permission prompts, a tool call its rules do not allow now waits for your answer instead of
  being refused: the session's page and *What needs you* show the call as the agent named it, how long it has waited and
  that it is refused after 15 minutes, with *Allow once* and *Refuse* as the agent offers them. Allowing lets that call
  alone run; the window's badge counts the wait. *In the conversation* on the waiting call leads to that call in the
  session's conversation, so you can read what it asks in context. An agent not run over its protocol still refuses it,
  and says so. The session's row in the list and its head say the call waits for your answer, and for how long, rather than that it runs.
- The window minimizes, maximizes and restores with Windows' own animations, Win+Up and Snap maximize it, and a window
  minimized while maximized comes back maximized from the tray, a notification or a later launch (the UI kit, Shenora
  0.22).
- Closing Daoris while a session waits its turn to start ends the wait at once, rather than starting the session first to stop
  it. A finished session you had written to, closed before it went on, keeps how it ended with your words still waiting, and
  goes on with them the next time Daoris starts.
- A terminal's words or stop for one session no longer wait behind another session's slow delivery, and the plugins'
  evaluation steps run beside the driver's look instead of holding the next one.
- The sessions list no longer runs git in every ended session's tree each time a window refreshes it. Whether a tree holds
  work to review is judged once per repository as its branches move, and every open window reads that, so more windows cost
  no more git, and a tree's *Accept* leaves the list as soon as its landing is done.
- A repository's heavy checks run one at a time across its trees: `daoris-driver run-heavy`, run in a checkout, runs the gates
  its `daoris.gates.json` marks `"heavy": true` one after another, or the one `--gate <name>` names, and stops at the first that
  fails. A second run of the same repository, from its checkout or any tree of it, waits rather than running beside the first,
  and says whose run it waits behind and for how long. Everything a gate starts, a build's lingering workers included, is ended
  before the next run begins, whether the run passed, failed, was stopped with Ctrl+C or was killed.
- In an installed Daoris, a session started in a repository that marks heavy gates is told to run the checks that cover its
  change while it works, and those gates once, at its end, through `daoris-driver run-heavy`, so two sessions finishing
  together take turns. A repository that marks none is told nothing new.
- A repository that marks no heavy gates of its own can be given them on this machine, never in the repository:
  `daoris-driver heavy <repository> "build=npm run build" "test=npm test -- --maxWorkers=2"` sets them in the order they run
  (`--cwd <folder>` after a gate runs it in a folder of the repository), `--clear` takes them away, and `heavy` alone lists
  them. `run-heavy` then runs them for that repository, and its sessions are told to, wherever its own `daoris.gates.json`
  marks none.
- A repository's Setup shows those heavy gates under *Heavy gates* and edits them there: each gate's name, command and
  folder, moved up or down, removed or added, then saved as one list in its order, or cleared. A list the driver would not
  run (a gate with no name or no command, a name twice, a folder outside the repository) is refused under *Save* in the
  driver's words, with nothing changed.
- A working session's row in the sessions list says the tool call its agent is inside and how long it has run, *running npm
  test for 8m*, as its page's head does, timed from the call's start even where it began before the window opened. A tool
  call the rules refused no longer reads as still running.
- Three of a repository's pauses can be turned off at a terminal, for the repository, its workspace or this machine:
- Ask Daoris follows the window's workspace. A conversation starts in the workspace the window shows when you press send,
  and stays there if the window moves while it starts. A window that shows every workspace asks which before it starts one,
  keeping your words in the box. Its history lists the window's workspace's conversations, or every workspace's with each
  labelled, and searches only the one chosen. Another workspace's conversation can be read from any window, named as that
  workspace's, and *Switch to games and continue* moves the window there before you write to it. In the terminal,
  `daoris-driver help list` takes `--workspace <name>` or `--all`.
- Removing a session's tree no longer deletes what a link in it points at. On Windows git's removal followed a junction out
  of the tree, so a tree holding `npm link`'s link, a workspace's junction or a link of the session's own could take files in
  your checkout or another repository with it. Each link in a tree now goes as a link before git removes the tree, whether
  you remove it, a landing or the clean-up does, or an opening is undone; a tree with a link that will not go, or whose own
  folder is a link, stays, and says why.
- Two of a repository's pauses can be turned off at a terminal, for the repository, its workspace or this machine:
  `daoris-driver access <repository> routing=intake` lets an ask's intake publish to its own best choice of repository rather
  than ask you which, saying in the quest that the route was its choice, and you can still publish the ask elsewhere.
  `goAheads=never` stops a session asking go-aheads before acts outside its repository or on a production system; a go-ahead your
  own words on the ask or the task demand is still asked. `landing=rule` lands a session's finished work when its quest is done,
  without your *Accept…*, as a branch rule's *Accept automatically* does: on its branch, and pushed with a pull request where the
  rule names a plugin. A held done still waits for you, and a rule that merges still waits for your Accept, its conversation
  saying why. A landing the setup accepted says so, and whose setup it was, in its session's *How this came to be* and to Ask
  Daoris. `full` turns them all off. Nothing changes until you set it, and a
  session that goes on in its own conversation is told where its go-aheads changed since it started.
- A session's own tree of a Node repository starts with the dependencies your checkout installed with npm, copied in where
  the tree's `package-lock.json` is the checkout's and the checkout's install is at it, so its session no longer spends
  minutes on a fresh `npm ci`, and is told to skip it. The tree's opening names what was copied, or says why a project was
  not: its lockfile moved, pnpm or yarn installs it, the checkout has not installed it, or git does not ignore it there. A
  copy, never a link, so nothing the session installs or builds reaches your checkout.
- A driven session asks you only what it cannot decide: a credential or a sign-in, a choice the task leaves to you, or a
  go-ahead (for an act outside its repository or on a production system, as before, or one its repository or task names). A
  name, or which of two sound ways, it now decides itself and says under *Readings* in its closing note, for you to change in
  review. It asks in its last message, and goes on in the same conversation when you answer. A session that asks again after
  your answer is announced again, and one you had already answered is not.
- Ask Daoris can act on this machine itself, if you choose. `daoris-driver help mode full` runs it with full access from its
  next start, in its agent's own mode that asks nothing (Claude Code's `bypassPermissions`, Codex's full access). It then has a
  shell, file edits, Daoris's commands and every plugin server here, and your own denies still hold where its agent takes them.
  Restricted, the default, is Ask Daoris as it was: it reads and proposes, and you apply. `daoris-driver help mode restricted`
  takes it back. On an agent with no such mode, the DeepSeek Harness or a plugin's, it does not start in full access, and says so.
  A conversation keeps the mode it started in while it runs, and its console says when a change takes it: end it, and your next
  words resume it under the mode you chose, which its transcript says first. `daoris-driver help list` marks a conversation that
  ran with full access.
- A session that waited on its own background work no longer hangs after it: when the work ends and Claude Code wakes the
  agent by itself, that turn's end is the session's end, where Daoris used to send a prompt the agent never answered and wait
  for the timeout. A session the timeout ends after its quest reached done is recorded completed, and its conversation says it
  timed out rather than that the agent exited.
- Several tasks no longer wait for each other to start. A task whose start is slow (its tree being made, its account being
  chosen, a plugin taking its time to answer) holds only itself: a task published meanwhile starts beside it, and the slow one
  is still started once, in its place under your limit of sessions at once. The sync with a remote runs on its own, so a remote
  that is slow to answer holds no task here, and each workspace's remote is synced at once. A plugin hearing that a session
  ended, and a brainstorm's or a meeting's packet being gathered, no longer hold what starts next, nor do a second opinion owed
  being weighed, a helper's answer being handed, or done work being chosen to land; a chain's next step whose second opinion is
  not weighed yet waits for it rather than start without it. Each of Daoris's looks writes
  in the machine log how long each of its parts took, and a part still running after five seconds is named there as it runs.
- A start no longer asks each account's agent how much it has used before choosing one, a process and a few seconds each
  time. Daoris works it out from the account's last reading and its own sessions on it since, and asks only where that comes
  within ten points of the account's usage curve, where the account has gone thirty minutes unread, where Daoris cannot weigh
  what it spent there, or after a limit or usage credits were said; *Read now* still asks at once. Whether an account is near
  or over its curve is still only what its agent said. Where Daoris's own turns moved an account's use since its reading, the
  start's first line says that use as projected.
- Each window holds Daoris's own state (what it drives and holds, the choices on Settings, what is running) as Daoris sends it
  when it changes: a choice made in one window or at a terminal shows in every window, and an open window no longer asks for
  it again when Daoris looks, when something changes elsewhere or when the window is brought forward.
- Ask Daoris starts at once. It no longer reads every repository and session tree with git before its first answer: Daoris
  keeps what it knows of the machine as it changes (a branch moved, a session ended) and Ask Daoris starts on that. Where
  something is not read yet, a repository whose line Daoris has not looked at, say, Ask Daoris is told so rather than guessing,
  its conversation says so first, and it learns the rest as it is read.
- Choosing an account for a start no longer waits for a question Daoris is asking another account: a sign-in or usage read
  that is slow to answer holds only the start that asked it. A start whose account another question still holds after 30
  seconds runs on the next account of its list, or waits for the next start where none is left, rather than running beside
  that question, which could sign the account out.
- An agent you start, Ask Daoris's among them, and a session going on with your words no longer queue behind Daoris's own git
  work for a place to start: of the four starts Daoris makes at once, its background work holds at most three, and a place that
  frees goes to yours first. Every git Daoris runs now has a bound, two minutes for a question and ten for work on a tree or the
  network, so a git that never answers (a lock, a prompt nobody types) is stopped there with what it started, and what it was
  reading is read again at the next look. A review's read and a file's preview stop their git when you leave them, and a task's
  stop no longer waits for ever on a session that does not answer.
- Ask Daoris can use Daoris's browser again, once you approve it. The *In-app browser* plugin, 1.1.0 (its page offers the
  update), offers its `browser` to Ask Daoris as well as to every session: its page says at its head what Ask Daoris is not
  handed yet, and under *Ask Daoris* you approve all its reads and tick each write you accept (clicking, typing, filling a form
  and the like), or withdraw it. An approval an update changed says so and asks again. Ask Daoris says once, at the start, what
  it lacks and where it is approved, and can propose the approval itself: its card names the plugin, the server and every tool,
  reads and writes apart, and nothing changes until you press Apply. A tool that runs commands or a page's own code is never
  handed to it. At a terminal, `daoris-driver plugins approve <id> <server> --writes <tool,…>` accepts only the writes named.
- In a week's last day, a start now goes to the account where the most of its week would otherwise go unused, for the hours
  left to its reset, rather than to the one that resets first: three points resetting in two hours no longer go ahead of half
  a week resetting in twenty. Only a reading of the week's use counts; a reset known with no reading ranks nothing. Daoris's
  smart curve lets the week rise from 95% to 100% as its reset nears, so less of it lapses, and a session that has to move
  goes where more would lapse. The start's first line says why: *its weekly window resets in 20 h with 40% of it unused, read
  10 min ago*, in English and 中文.
- Ask Daoris no longer sits silently while it starts on a busy machine. It waits up to 30 seconds to read this machine for its
  room; past that it starts on the room as last written and says so in the conversation, and the room is written again once the
  reading finishes. On a machine where Ask Daoris never ran before, the start ends saying why instead of waiting. A
  conversation left waiting to start by a Daoris that closed, which kept every new Ask Daoris conversation from opening, now
  ends when you stop it.
- A folder beside an agent's accounts whose name ends `.lock`, or starts with `.`, is no longer taken for an account: Claude Code
  makes such folders to lock an account's home, and two were listed, signed in to and run in. Nothing is deleted. Ask Daoris
  names each one, and says when one holds a sign-in: rename it to an account's name while nothing runs on it to keep it.
- Each main window of Daoris holds its own workspace. Choosing one in a window changes that window alone, and reloading it
  keeps it; a window opened later starts from the workspace last chosen in any. A session's own window keeps the workspace its
  session belongs to, or follows the window it was opened from, never another main window, and so does the monitor. The page's
  title names the workspace, *Daoris (道衍) — games*, which a browser's tab shows; the desktop window's own title does not say it
  yet.
- Daoris keeps every usage reading it sees, in `usage-history/` in its data folder: each window's use and reset as a session or a
  read said it, the account's plan word and when, for the estimate of how much work each window holds that comes later. Nothing
  reads it yet. It stays on this machine, holds no email, organisation, key or words, keeps 90 days and 64 MiB at most, and says
  what it pruned.

- A quest whose take on this machine lost, to another machine's take or to the reopen of the work it grew from, offers two
  choices under *Conflicts* on its page: *Drop mine* puts the conflict away and moves nothing else, and *Bring mine forward* takes
  the step again from this machine, so its kept session goes on (`daoris-driver sync forward` at a terminal). Where it cannot be
  brought forward (another machine holds the step, the step is closed, or its source is reopened) the page says why. A
  bring-forward that loses again says so, one made offline reads *unconfirmed* until the remote answers, and a settled conflict
  says how it ended. A step the reopen holds names its go-on and abandon the same way, and says that abandoning declines it for
  every machine. Once brought forward, confirmed or offline, the stopped session is carried on at Daoris's next look in the tree
  it worked in, brought up to date first with the work its step grows from, and only once: if that session stops too, the same
  press does not carry it on again, and a bring-forward that lost again carries nothing on.

- Beside the readings, Daoris keeps every turn's tokens in `usage-history/`: input, output and cache as the agent reported them,
  the account the session ran as, and the model they were spent on where the agent said it, with a helper model's share kept
  apart and a stopped turn marked. It shares the readings' 90 days and 64 MiB, stays on this machine, and holds no email,
  organisation, key or words. Codex's turns say their counts' meaning is not yet known.
- The machine log (Settings → Logs, `daoris-driver logs`) now holds the warnings and errors of the window's own toolkit and
  engine, which until now were written nowhere: among them why opening Daoris again did not reach the Daoris that runs. Opening
  it again is written as it arrives (`window.launch`), and anything that stops its window opening is written too.

- A first brainstorm across agents, at the terminal: `daoris-driver brainstorm "<question>" [--repository <name>] [--agents a,b]`.
  Daoris freezes one context packet from its own knowledge of the repository, which `brainstorm show <id>` prints whole; nothing
  goes to any agent until `brainstorm send <id>`. Each named agent then proposes once, in a folder holding only that packet,
  answers that break the proposal schema get one retry, one agent ranks the proposals without knowing whose they are, and the ranked
  document, saying who proposed what, is kept on this machine until `brainstorm clear <id>`. It changes nothing in any repository.
  An agent answers through two tools of Daoris's connector, which say a malformed answer's faults back so it can answer again,
  and that connector is the only one it is handed. Claude Code is also held by Daoris's rules to reading that folder and calling
  its one tool: no edits, no commands, no web. Another agent is given only what the folder holds, and whether it stays inside
  is not measured yet. A machine with no connector starts no pass. No real agent has run one yet.
  `daoris brainstorm` does the same from any terminal, `show`, `send`, `clear` and `list` included, through the Daoris running
  on this machine: it asks only this machine's own service, and a brainstorm is never kept on a shared host.
  A brainstorm is bounded and never hangs: its agents run side by side, ten minutes each and thirty for the whole question, with
  the ranking's minutes kept in reserve; an agent past its time is stopped and what answered is kept; one that crashes fails alone;
  one whose account meets its limit starts once more on the next account; a full machine waits within the time. The document says
  which agents did not answer, and why, and when the ranking does not finish it lists the proposals unranked.
  `daoris-driver brainstorm stop <id>` ends one and keeps what answered; `--id <id>` makes asking again the same brainstorm; a Daoris
  that stopped mid-brainstorm ends it as interrupted and runs nothing again by itself. `daoris driver brainstorms` sets the agents a
  brainstorm runs and its minutes, for this machine or with `--workspace` for a workspace's questions.

- A first meeting of agents, at the terminal: `daoris-driver meeting ask "<question>" --agents a,b [--repository <name>]`.
  Daoris freezes the first round's context packet as a brainstorm's, naming the agents in your order, and `meeting show <id>`
  prints it whole; nothing goes to any agent until `meeting send <id>`. Each agent then speaks once, one at a time in your order,
  within a brainstorm's minutes, and Daoris keeps each message with who said it from its own records, never from what the message
  claims. A second round goes only by `meeting send <id> --round 2`, its packet holding the first round's messages, which `show`
  says it adds before it goes. `meeting stop <id>` ends a meeting at once, and a Daoris that stopped mid-round ends it as
  interrupted and runs nothing again by itself. A meeting talks only: it changes nothing in any repository, and is kept on this
  machine until `meeting clear <id>`. No agent can speak in one yet: no tool of Daoris's connector carries a meeting's message, so
  each turn is refused and nothing is started.

- The desktop names two sign-ins of one email apart too. An agent's page, its own sign-in, the start's account picker, *What
  needs you* and a session's account say the organisation after the email where another sign-in shares it (else the plan),
  once: an account named `person@example.invalid·Org-B` says neither again. Adding an account offers the name the sign-in's end
  offered.
- Two Claude sign-ins of one email in different organisations are told apart. Daoris reads each sign-in's organisation and plan
  from `claude auth status` beside its email, fresh and kept nowhere. `daoris agent list` says the organisation after the email
  where another account shares it, and a sign-in whose email already names another account is offered the email with its
  organisation (`person@example.invalid·Org-B`) as its name, where the email alone was refused as taken.
- Accept, on a session's work or on a whole line of work, now looks at its quest again when it had to wait behind another landing
  of the same work. Where the quest was taken back or its result handed in again meanwhile, nothing lands and Accept says why; where
  that happens just before the push, the branch stays and `daoris-driver trees hand <session>` pushes it. A hold alone does not stop
  your Accept.
- Handing a landed branch on (*hand it to*, Ask Daoris's card, or `daoris-driver trees hand`) now looks again at the quest of the
  session whose work last moved the branch when it had to wait behind another push of it. Where that result was taken back or handed
  in again meanwhile, no plugin is told, the branch stays as it was, and the answer says why. A hold alone does not stop it.
- Under *Accept automatically*, a session you carry on with more words after its work landed now lands again when it is done
  again: its branch moves on and its pull-request plugin pushes once. Before, the later work waited for your Accept. A session
  stuck that way lands at the first look after the update, or is closed where nothing new is in it.
- Under *Accept automatically*, work whose branch was made but whose push waits for a second opinion now has that opinion asked by
  the driver at its next look, as the landing's gate says, and pushes once it settles. Before, it waited until you asked one
  (`daoris-driver opinion ask`), and an ask made then could be dropped at the next look before it was read.
- Under *Accept automatically*, done work waiting for a second opinion nobody asked now has it asked by the driver at its next
  look, as the landing's gate says, and lands once it settles. Before, where the repository's opinion rule started reading after the
  work was done, or was switched off and on again meanwhile, it waited until you asked one.
- Under *Accept automatically*, a session that landed, went on with your words and finished again with more work now has the second
  opinion on that work asked by the driver, and lands it once it settles. Before, its earlier landing counted as landing the new work
  too, so the opinion was never asked and the landing waited until you asked one.
- The full gates can run in a worktree of their own while merges continue (`node tools/merge-branch.mjs --full --beside`,
  GATEWT1), and `publish:desktop --stage --from-gated <commit>` stages the commit they passed, built in that worktree.
- On Windows, where the git Daoris finds is Git for Windows', Daoris's own git commands now start one process where they
  started two: the git behind the install's `cmd\git.exe` launcher starts directly, with the environment the launcher
  would have given it, so hooks, `!` aliases and ssh find what they found before. A git you named as a file in Settings →
  Tools, or a managed version, starts as it is.
- Accepting a session while its done work is landing itself now waits for that landing. Where it landed the same commit, the
  answer says where the work went and what its pull-request plugin answered, a failure included, and nothing more lands. A
  commit made meanwhile lands as usual. Before, a session whose agent moved its tree onto a branch of its own was landed twice
  and its plugin pushed twice. The same holds for a terminal's `trees land` beside the window. Handing a branch on to a plugin
  while its landing is still pushing also waits, and pushes nothing more.
- A landing plugin is now told `tip`, the commit the landing judged, and both example plugins push that commit to the branch
  rather than the branch as it stands: a commit made on the branch after the judgement waits for the next landing. A plugin
  copied from an example before this pushes by name until it is copied again.
- A workspace's *Branches* now lists what Daoris keeps for recovery that no piece of work names: folders set aside until a
  date, and recovery refs with who holds them or the command to delete them. Ask Daoris says the same when asked where work
  has got to.
- A workspace's Branches page can now delete the recovery refs nothing holds in one press, *Delete the ones nothing holds*, after a
  confirmation that lists them and says their commits may then be lost from this machine and that a git remote may still have the
  work; `daoris-driver work recovery drop` does the same from a terminal. A ref that moved, or that a branch or a tag now holds, is
  kept, and the answer says which and why.
- Work Daoris did not record: a `daoris/` branch that no record names now shows in What needs you and in `work state` as an
  unknown owner, blocked, saying only your Discard branch removes it; Daoris never removes it on its own.
- An update that could not move a file because another program held it now says which file and which program, in
  `daoris-driver update` and in the window's note of the rolled-back update.
- A plugin's manifest may declare `steps`, each one step of a piece of work as Daoris's own steps are (what it takes and makes,
  how it is retried, whether it is performed and how), and the `resources` they create with who cleans them. Daoris reads and
  checks them, and a broken declaration keeps the plugin from loading, saying which field is wrong; none is run yet. A plugin
  with a step Daoris asks it to perform must list `work/perform` among its hook points, or it does not load.
- A setting you add to `driver.json` by hand, or one a newer Daoris wrote there, now survives an edit from Setup, Ask Daoris or
  the terminal: an edit changes only the settings it edits, the rest keep their values and their places, and a file read and
  saved with no change keeps its bytes. A change one of them made while another was open survives that one's edit too; a file
  that no longer reads, or names a setting twice in one place, is refused and left as it is; and Windows line endings are kept.
- The same now holds for `plugins.json`, `permissions.json` and `keys.json`: switching a plugin on or off, changing a permission
  rule and adding or replacing an API key, from the window or the terminal, keep every setting their writer has no field for,
  at every level, and a file read and saved with no change keeps its bytes. Before, switching a plugin deleted everything in
  `plugins.json` but the list of disabled plugins, and a key added in the window over a `keys.json` that did not read replaced
  every other account's key; that edit is now refused, and the file left as it is.
  A rule or a group of settings another one removed meanwhile does not come back with an unrelated edit, one it turned into
  something else refuses the edit by name, and the first save where there was no `driver.json` keeps one written meanwhile.
- Each piece of work says where it has got to: a session's foot shows its *End* (结束), *Result* (成果), *Clean-up* (清理) and
  *Recovery* (找回); *What needs you* lists work whose clean-up waits on you; *Branches* gives each tidied branch a line; and
  Ask Daoris can answer where your work has got to.
- In the window, Ask Daoris shows a proposal and a conversation's progress as Daoris writes them, and Settings' sizes as their
  count ends, rather than asking every one to four seconds; it still reads again every 30 s in case a change was missed. A
  plain browser asks as before.
- The knowledge server reads a checkout again at the next search after its files or branches change, rather than once a
  minute, and at least every five minutes.
- A pull request your landing's plugin opened is asked about when its answer may have changed:
  - when your own fetch or pull moves the line;
  - right after Daoris pushes;
  - when you bring Daoris's window forward.

  Each pull request is asked at most once a minute, and the line is fetched at most once a minute. A completed pull request
  whose merge reaches this machine then ends its work, with no press.
- A conversation whose turn ends with commits beyond its line now offers *Accept…* (采纳…) at the foot of the chat and in its
  head, as a driven session does, not only in the review's side bar; none is offered while a turn runs. Its *Accept* names the
  branch for the work's first commit, never for your opening words; a ticket you named in those words still names it.
- Where an agent has no Daoris account signed in, *What needs you* says its starts wait and offers *Sign in for Daoris*
  (为 Daoris 登录) right there; the agent's page heads with the same press. Your own sign-in stays listed, read-only: Daoris
  starts no session in it, and a conversation begun there goes on there. Ask Daoris and the setup guide count only Daoris's
  accounts as signed in, and the terminal says the same.
- A session's branch and tree are tidied once Daoris has seen the line hold its work: right after the merge, or as soon as the
  session leaves a tree that kept it, rather than at the next five-minute pass. The pass no longer reads every branch the line
  holds none of again (about nine git commands fewer for each), and the work's state names what kept a tree and why, such as a
  session still in it or uncommitted work. A branch goes only while the line still holds what its removal was judged against: git
  checks both together, so a line moved at that moment keeps the branch. Where a landing's clean-up cannot tell what holds the
  other branches, it keeps them and says it could not judge them. A branch git cannot read, such as a broken ref, is kept and
  said unread rather than forgotten as gone, and a detached tree's removal keeps its commit at a recovery ref where what held it
  moved as the tree went.
- Work whose landing branch you deleted yourself, after its pull request, now reads as over without its result (已结束，未留成果)
  and its clean-up as completed, rather than waiting for ever; where no ref of yours held its commits, Daoris first keeps them on a
  recovery ref. Where your line holds its work (a pull request merged with a merge commit, or squash-merged with every file it changed
  reading the same on your line, then pulled), it reads as ended, its clean-up completed and no recovery ref made, rather than as moved
  to the line or over without its result. Where anything leaves doubt (a branch of that name, a read git could not make, its session branch still there) it is
  kept and says why, and a landing branch you renamed is followed to its new name rather than read as over. A recovery ref whose
  commit a branch or tag of yours holds goes after 30 days (never one only a Daoris branch holds); one nothing of yours holds is kept
  and `daoris-driver work state` says how to delete it. Folders set aside and recovery refs no work names are listed as recovery kept
  until their date, no longer as work with an unknown owner in *What needs you*; a folder whose deletion a later write, a link, or a
  read or delete that failed blocks past its date is in *What needs you*, saying why.
- A session starts sooner: it asks git once per fact before it starts (11 commands, from 18).
- A terminal's `sessions stop`, `say` or `take-back`, and its `trees` presses, reach the running Daoris as they are written
  (about a third of a second), where Daoris used to look for them once a second; it still looks every 3 s in case one is
  missed. The machine log times each look of the driver and says what woke it.
- What wakes the driver between its looks (a terminal's words, a session's end) now plans and starts work at once without
  asking your circles' remotes; the remotes are still synced every `pollSeconds`, after a session ends, and whenever a press
  asks the loop to look. The machine log says which part each look ran.
- A quest an agent publishes from its session, and a session record another process writes, now start Daoris's work at once,
  where the driver used to find them at its next look (up to `pollSeconds`, 15 s by default). The local service answers
  `GET /api/changes`, which says which of its records moved; a sync pass that changes nothing wakes nothing. An ask, and a
  message on an ask's channel, that another process writes wake the driver the same way, named `asks` and `channel`; reading
  a channel wakes nothing.
- Done work set to land automatically lands as soon as what held it lets it go, rather than at the driver's next look. This
  covers a hold released, a review given or skipped, a second opinion settled or answered, a workflow's declaration or your
  say-so, a commit in its tree, and a change to the landing rule. When a change releases none of the waiting work, the landing
  starts no git for it.
  Daoris still looks at all of it every `pollSeconds`.
- Done work that lands automatically is checked again just before it lands: its quest, its holds, its review and its tree. A
  hold, or a commit nobody reviewed, that came after Daoris chose it holds it again, and only the commit it checked lands. Only the
  loop holding the home lands work automatically: a `daoris-driver drive --share` loop beside it lands none, and says so as it
  starts.
- `daoris-driver sessions say <id> --now …` is *Send now* from a terminal, and says what the agent's door did.
- *This Daoris* also shows the settings files, the variables Daoris reads, the versions, the terminal's wiring, the
  knowledge store and the logs.
- *Send now* (立即发送) sits beside *Queue* while an agent works. On Claude Code it steers your words into the running turn
  at once, cutting the step in flight while earlier queued words keep their places; on an agent that takes words only when
  its turn ends, it stops the turn and keeps your words, so they go next. Where the agent can do neither, it says why in
  one line. A tool call cut by your words reads *cut off*.
- Settings → *This Daoris* (本机) shows how this Daoris is wired: its folders and their sizes (*Measure again*), the service
  it talks to and where that address came from, and who holds the loop. Driver's home row opens it.
- A plugin's skills now reach an intake's, a reviewer's and a helper's sessions too, as they reach a driven session's and a
  conversation's.
- Daoris's sessions no longer run in your own agent sign-in. Where nothing names an account, a start takes one of the agent's
  Daoris accounts; with none signed in, it waits and says so once (*No `claude-code` account of Daoris's own is signed in…*),
  in English and 中文, until you sign one in for Daoris. A conversation begun in your own sign-in still goes on there. A
  folder-trust hold on a Daoris account names Daoris's own command, since trusting the folder at your terminal would not
  reach that account.
- `daoris-driver wiring` says how this Daoris is wired: its folder and each top folder as it runs (with what is still at an
  old place and why), the service (its address and where it came from, its tier and version), who holds the home, each
  settings file (whether it reads, what overrides it), every variable Daoris reads with where it came from, the versions,
  and whether a new terminal finds this install's `daoris` and `daoris-driver`. `--measure` counts the folders' sizes first,
  and `--json` prints it whole. A key is only ever *set*, or a deployment key's short prefix; never its value. The service's
  status now names its version.
- Switching accounts is in view on an agent's page. Each account's five-hour and weekly window says its use against its usage
  curve now (*its curve allows 45% now*, or *over its curve's 45% now*) and whether it was read from the agent or said by a
  session, and how long ago; the page reads each account's use as it opens where a reading is older than a minute, and at
  *Read now*, with no prompt and no model request. *How accounts are used* replaces *Switch before the limit* and *Near at*
  with *Switch accounts*: *Smart* (Daoris's curve), *My own curve* or *Off*, a chart for each window with each account on it,
  and under *My own curve* each window's points to edit and *Clear my own curve*, a curve that breaks a rule said in the
  terminal's words in English and 中文. The list says what passes an account as *over its curve*, *limit reached* or *on usage
  credits*. Ask Daoris proposes the switch and your own curve. A conversation that went on in another now names that one by
  its title, with *Open session*.
- The wire test the plugin kit writes beside a new plugin (`plugin.test.mjs`) now ends as soon as the plugin leaves at
  `shutdown`, where it waited out the whole two seconds of grace first. A test written by an earlier kit keeps the wait until
  it is made again.
- Ask Daoris says when it has no door for what you asked: it names the terminal command, and says the door is one Daoris
  still owes, not a limit of its own. It does this for registering a repository, setting a workspace up, checking evidence
  again, an ask's review or kind, a workflow's file, import or kind, a tool's version or location, and clearing an
  account's default.
- A plugin's `plugin.json` can offer Ask Daoris an MCP server (`"to": ["help"]`, or both with `"sessions"`). It lists the
  tools that read (`allow`) and those that write outside Daoris (`writes`). Ask Daoris gets the server only once you approve
  its exact tools on the plugin's page, or with `daoris-driver plugins approve <id> <server> [--reads-only]`. You can take
  that back (`withdraw`), and an update that adds a tool asks again. A write you did not accept is refused. A tool whose
  name says it runs commands is never offered to Ask Daoris. The page says plainly that a plugin is code that runs as you.
- A plugin can carry skills (`"skills": [{ "name": …, "to": [...] }]`, each a `skills/<name>/SKILL.md`). Sessions, Ask
  Daoris or both get them as they start, by Claude Code's session-only plugin door. Nothing is written into a repository,
  and a skill that would allow itself tools is handed nowhere. The plugin's page lists each skill and who it goes to.

- `daoris-driver` and `daoris` from an install's `app/bin/`, in a terminal of your own with nothing set, now find the Daoris
  that runs: its home is the install's `data/` when `DAORIS_HOME` names none, and its service is the one the running window
  holds, by the address it leaves in that home, and only where the service there answers as that home's: another install's
  Daoris at that address is never used. With Daoris not running, a verb that needs its service says so and names the
  `Daoris.exe` to start. The variables still win. A `daoris-driver` verb that changes the home now holds it while it acts
  alone, as an update's swap and the first start's move of an older home do: another such verb waits up to thirty seconds
  and then says which one holds the home, and Daoris starting meanwhile waits for it, saying so if it takes that long. The install's knowledge connector (`app/daoris-knowledge/`), named in your
  own agent's `.mcp.json` with no `DAORIS_HOME`, and its HTTP host started from a terminal, now take the same home instead
  of refusing to start. Opening Daoris again while it runs now opens another main window of the
  same Daoris, which hears every press the first one does, rather than only bringing the first one forward. Closing the
  first of those windows keeps the others open, and Daoris ends with the last one you close; opening Daoris again, or the
  tray icon, brings the first one back.
- Ask Daoris can now set a repository up for you: asked to include a registered repository that has not adopted, it proposes
  the set-up as a card showing what was read on the repository's line, the rule it adds and the quest's whole text, and
  *Apply* publishes that one quest to the repository's own session, as `daoris-driver setup <repository>` does. The same press is
  on the repository's page: *Set up for agents* (为智能体配置) under its adoption steps shows the plan first, every reason it
  cannot go yet with what fixes it, then *Set it up*. Daoris still writes nothing into the repository.
- The review says before the press what *Accept* will do and why it would be refused: it lands the session's own branch as
  it stands, adds to a branch that already stands, or names the refusal (a branch somebody else made, one moved since or
  checked out elsewhere) and offers no *Accept* then, as the line's and the ask's *Accept* do for each part; a part's own
  *Accept* waits while its line lands it. Every door that lands one session (the review, `trees land`, the landing at done)
  puts a line's part on the line's branch. *Bring up to date* replays only what the line lacks and says what it leaves
  out, and a session or conversation going on in an older tree starts from the line, told so. A chain step that merged the
  line reviews only its own changes, and a session's *Details* say how its work is committed and lands. Helpers sit under
  the session they serve, *Ask another agent…* asks one from a session's page, and an agent's ask for a helper waits on you
  where every wait does, with *Let it ask* and *Not now*.
- An agent's ask for a helper that waits on your yes now offers *Not now* (暂不) beside *Let it ask*: no helper is started
  for it, and its session's timeline says you said no. It is one of the session's waits too, at the foot of its conversation
  as at its head, and *What needs you* lists it with the same two presses and a door to the session that asked.
- A part's own *Accept* now lands on its line of work's one branch, as the line's *Accept* does; a part you leave out of
  the line's *Accept* says so in its review, with *Discard tree* first; and under a squash rule a line's later parts are
  squashed onto its branch, fetching the line once. While work lands, its *Accept* says *Checking…* (检查中…) at the gate,
  then *Landing…*, and a line's or an ask's *Accept* shows it in every open window; `daoris-driver trees land --entry` or
  `--ask` lands them from a terminal. The window now opens in the theme you chose, from its first frame, and the splash stays
  until the first screen has its data (at most ten seconds). A word you take back never reaches a session that goes on in a
  new one, and withdrawing a stop or words from a terminal says truly whether the loop took them first.
- One *Accept* lands a whole line of work: under a session's parts, *Accept all N parts…* lands them on one branch in the
  order they were made, and in the list grouped by ask, *Accept the ask's N parts…* lands every repository's, each answered on
  its own. Before it lands, its plan says where and in what order, and names a part a later one superseded, which you may
  leave out. A plugin can now observe each change to what the driver holds (`state/changed`), after the press has answered.
  `daoris-driver trees land`, `remove`, `clean`, `sync` and `hand` in a terminal now show in the open window within a
  second. A stop or words sent to a session are no longer lost when two Daoris loops take them at once. *Read again* on the
  agents, a word said or a conversation deleted in Ask Daoris, and a change to the browser's settings now show in every open
  window. The plugin kit names `work/state` a query, not the raw word.
- A word you take back is never sent: a conversation that goes on just as you take back its only word now stays as it ended,
  saying so, and `sessions take-back` or `reword` in a terminal, while Daoris runs, is shown in the open conversation at once.
  A helper's or a reviewer's conversation no longer offers acts that would send it words it refuses, and rewording a waiting
  message keeps the box's length limit, the words staying in the box.
- Words you sent a session whose box then became its line, because the session no longer takes words, now come back to the
  box with a line under it saying they did not go, where before they stayed *not answered yet* until you opened the page
  again. Starting Ask Daoris from an earlier conversation opens at once, as a new one does, rather than waiting for the
  machine to be read. And the Branches tab's *Cleaned up on its own* now says when a folder set aside was a folder under the
  trees that was no tree, or an opening that was cut off.
- While a session's work lands, its review says *Landing…* (落地中…) wherever it is shown: after *Accept…* at the session's top,
  after switching tabs and back, and in every other open window, where it offered *Accept* again before. So too its *Discard
  tree* waits while its tree is discarded, and its hand-off says *Handing off…* while its branch is handed to its plugin. A
  window opened, or a page reloaded, while the work lands says *Landing…* too, where it offered *Accept* until the landing ended.
- Allowing a rule a session proposed from What needs you, or accepting it on the Agents page, now tells that session so, as
  allowing it in its conversation does, so it goes on instead of sitting idle. And a branch Daoris cleaned up that a landing
  made reads *A landing's branch, for session …* in the Branches tab's *Cleaned up on its own*, not a bare repository row.
- Answering a session that parked is one box: the words waiting on it fold to one line with their count, *Show them* opens them
  to edit or remove, and the strip at the foot no longer says "answer it in the box below" over that box. A conversation that
  answers you while you are reading it in a window in front no longer also toasts.
- A press in one window — a session's stop, archive or delete, a work's pause, a rule, a plugin, a sync, a history clear, a
  setting, an agent's account, a tool, a post in an ask's channel, an Ask Daoris proposal applied or a conversation renamed —
  now shows in every open window at once, as an Accept already did.
- A message you send in Sessions no longer vanishes on a busy machine. In a conversation, and in the box at the foot of any
  session (telling one at work something, answering one that parked, or writing to one that ended), your words show at once as
  *sending…*; where the machine is slow to answer they stay, saying it has not answered yet (本机尚未回应), rather than
  vanishing behind an error, and go once the session holds them. Nothing goes back in the box to be sent twice; words a
  conversation never took before it ended come back there, said so.
- When a conversation answers what you said while Daoris's window is behind another, Windows tells you so, as *engine — a
  conversation answered you*; a click opens it. Nothing is said while you are looking at a Daoris window (the page shows it
  instead), for a turn you stopped, or while notifications are off.
- What waits on you in a session is shown once on the screen. The strip at its foot steps aside while the session's top, where
  every wait stands, is wholly in view, and comes back as you scroll it away; it stays while an ask is open in it or you are
  working in it. Where the conversation raised a wait, a small mark replaces the line of words: its tip says what waits on you
  from there, and pressing it takes you to that wait, at the top when the top is in view.
- A rule a session proposed after it was refused something now waits on you in that session, at its top and at its foot, with
  what it would change and why: *Allow…* asks once, *Decline* is one press, and *In the conversation* shows the call that
  proposed it. Allowed there, the session is told the rule is allowed now and goes on with it; a session that takes no words
  says so instead. In What needs you, allowing one now says *Allowed* (已允许), as its press reads.
- While something waits on you, Daoris's taskbar button shows how many as a number on its icon (*9+* past nine), and the
  window's title leads with it, as *(3) Daoris (道衍)*, when you hover the button or switch windows with Alt-Tab. It is the
  count Overview's *What needs you* shows, and it goes when nothing waits.
- After a session's work is accepted (采纳), its *Accept…* and the plan beside it read anew wherever they are shown, in
  every open window and when you switch back to a tab, where an old plan used to stand until the driver's next change. The
  same holds after discarding a session's tree, handing a branch to its plugin, a press on a second opinion, the clean-up,
  bringing repositories up to date and saving how work lands.
- Ask Daoris no longer loses your message on a busy machine. It opens at once and reads the machine while your first words wait,
  shown as *opening Ask Daoris…*; words you send show at once as *sending…*, and where the machine is slow to answer they stay,
  saying it has not answered yet (本机尚未回应), rather than vanishing behind an error. Nothing goes back in the box to be sent
  twice; words that never arrived come back there, said so.
- The words you said to a session that ended, or that parked to ask you, wait above its box with *Edit* and *Remove* until it
  goes on with them, as words waiting behind a running turn do: remove one it no longer needs, or put new words in its place.
- What waits on you in a session is at its foot too, above the box you type in, so you need not scroll back to its top: its
  question (answered in the box), a go-ahead still open with *Approve* and *Refuse*, its work's *Accept…*, the words asking it
  to rebase, the second opinion's and the review's presses, and a branch to discard, each the same press as in its head, an
  ask opened in one place closing the other. Several fold to one line; *To the top* takes you to the head.
- Each thing waiting on you in a session leads to where its conversation raised it: *In the conversation* on its head and at
  its foot shows the question it asked, the call that asked a go-ahead, or the end of the turn whose work waits to be accepted,
  outlined; that place says what waits on you from there, with *To the wait* back to it at the foot.
- A theme you choose (light or dark, under Settings or View → Theme) now reaches the window's Chromium as well as the page:
  its scrollbars and controls, its own pages, and what a page reads as the system's theme follow your choice in the main
  window, the monitor and a detached session, where they followed Windows' setting before. *System* still follows Windows.
  Daoris's browser keeps its own setting.
- A session no longer thanks you for accepting when you type "accept": it says its work waits for *Accept…* on its page and
  where that is, and when it asks you to approve a rule it proposed, it names the Agents page's *Proposals*. In What needs you,
  the act on a rule an agent proposed reads *Allow…* (允许…), no longer the same *Accept…* (采纳…) as landing a session's work.
- Words that could not go on in a session whose tree was gone no longer leave a fresh tree and branch behind at each look.
- Words you said to a session that ended, or that parked to ask you, can be taken back or reworded until it goes on with
  them: `daoris-driver sessions take-back <id>` lists the words waiting with their ids, `sessions take-back <id> <word>` takes
  one back, and `sessions reword <id> <word> "…"` puts new words in its place, so a finished quest's session need not go on
  with words you no longer mean.
- The installed Daoris now ships the driver's own verbs to a terminal: `daoris-driver` (and `daoris-driver.cmd`) in the
  install's `app/bin/` runs `trees list|clean|land`, `sessions archive|take-back`, `history clear` and `update` against the
  install's home, so the clean-up no longer needs the window's list. `daoris driver` says where they are.
- The window is on the desktop kit's 0.21.1. Opening Daoris while it is still closing now starts it once the closing one
  has gone, where that launch used to be lost.
- Accepting a session's work lands only what its line lacks. Where the line already holds some of its commits (a pull request
  squash-merged, a cherry-pick), the review and the session's *Accept…* name the commits left out and why, and the press replays
  only the rest onto the line in a tree of Daoris's own, so the next pull request carries no second copy; the session's tree then
  moves to what landed. Where that replay conflicts, nothing is made: the review names the files and offers *Ask it to rebase*,
  which sends the session the words to replay its own commits onto the line. The press fetches the line first, and a fetch that
  fails is said, with when origin was last fetched, without stopping the landing. `daoris-driver trees land <session> --plan`
  says the same.
- The window no longer freezes for minutes while the Sessions page's clean-up list is read: its git work goes on away from
  the window's thread, at most four programs start at once, and each repository's line is asked of git once per list. The
  clean-up list and the lines asked again while one is still being read wait for that one rather than reading it all again,
  and every program Daoris starts (an agent, its sign-in or install, a plugin, a terminal's shell, the file manager, the
  launcher *Update now* restarts through) starts away from the window's thread too.
- Daoris now removes its own session branches by itself wherever *Clean up* would, once their work is on the line: a branch
  whose work was merged into the line or its copy on origin, is on the line by content after a squash merge (its commits kept
  on a ref, which the report names with how to bring the branch back), or was carried by a completed pull request, not only one
  with nothing beyond the line. Work accepted onto a branch whose pull request has not merged yet stays, so you can still ask its
  session to change it. A tree holding only ignored files (installed dependencies, a build's output, a session's own logs) goes
  too: its folder is moved aside for fourteen days, as before. Uncommitted or untracked work, a session still running or waiting,
  words you said to a session that ended, and a tree whose branch never moved keep it.
- The branches *Accept…* made (`feature/…`) now go by themselves too, once their pull request merged or the line holds their
  work, with the session branch that grew them: a squash merge no longer leaves both until *Clean up*. One whose pull request is
  still open, one checked out somewhere, one you pushed and moved since, and one a running session still works from stay. A tree
  whose installed packages hold paths past Windows' 260 characters is moved aside and deleted fourteen days later like any other.
- Folders beside a repository's session trees that are no tree, an empty one a removed tree left, a tree half removed, or one a
  session wrote next to its own (`devtools`, `.tmp`), are now moved aside by themselves once nothing has written in them for ten
  minutes, and deleted fourteen days later like a tree's; `daoris-driver trees list` names them *(not a tree)* until then, and
  each folder moved aside *(moved aside)* with the day. A session's start cut off as the app closes no longer leaves a
  half-made tree and branch behind: a cancel undoes it at once, and one a crash cut off is moved aside, its branch removed, by a
  later look. A folder a running session names stays, and so does a tree you locked.
- What a second opinion's pass or a replay left in a repository's trees when Daoris was closed or crashed mid-way (an `o-` copy,
  an `r-` tree) is now moved aside by the next pass of its kind in that repository, once no running Daoris holds it and nothing
  has written in it for ten minutes, and deleted fourteen days later like a tree's. A folder moved aside whose `node_modules`
  (or another build folder) holds links, as npm workspaces and pnpm leave, is now deleted after its fourteen days, each link
  removed as a link and what it points at untouched; a folder holding a link anywhere else still stays for you to look at.
- An ask's page has a *Channel* (频道), where its sessions and you talk: their messages and Daoris's lines about its quests
  (published, taken, done, declined, waiting on your yes) in your language, an open question marked, and a box to write to
  everyone on the ask or one of its repositories, answering a message if you choose. A session on an ask has the same channel
  kept to its own repository, folded under its head. The ask's row and *Group by ask* say how many items are unread, and
  `daoris-driver ask channel <id> [--to <repository>] ["…"]` reads the channel or posts to it from a terminal.
- Sessions working on an ask can post to and read the ask's channel (`channel_post`, `channel_read`), and Daoris hands them
  what was written there: a running session reads a message at its next step where its agent takes words during a turn,
  another session's always marked as that agent's claim and never as yours; your message reaches it as your words, and one
  addressed to a repository whose session has ended goes on in that session. What a session could not take yet is handed to
  the next session of that line of work. Another agent's message never starts or extends a session.
- Sessions lists one row per line of work in a repository: a request's retries, resumes, carry-ons, hand-overs and the
  requests the ask's other repositories sent it are its *parts*, shown with *N parts* and a parts strip on its page, where
  each part can be opened and acted on. An ask's finished intake leaves the list for its ask's page, which now lists the
  ask's sessions; *Group by ask* in the list's ⋯ arranges sessions under their asks; archiving a session archives all its
  parts. A new request of the same ask goes into its line's tree when that tree is free, and runs alongside when it is not.
- A session that goes on with your words and whose agent's connection fails no longer ends as *completed*: it ends failed,
  saying why, and your words wait to go again at the next look (after a second failure in a row, with the next words you
  say). Words you add while a turn hangs are no longer shown as taken when the turn fails; they wait to go again too. The
  conversation no longer shows an empty *waiting* turn above each resumed turn, and the agent's `API Error: …` line reads
  as part of the failure.
- A turn still waiting for its agent's first answer says since when, in the conversation and in the session's head, rather
  than showing nothing for minutes. A working session no longer shows its last ending's note, and a session whose quest
  waits on another repository's answer reads *awaiting reply* on the quest page, not *completed*.
- A message waiting behind a running turn can be edited or removed on its own, in a chat, a driven session's box and Ask
  Daoris; before, only *Stop turn* took waiting messages back, all at once.
- The desktop's own settings and record files, and a repository's `daoris.json` when the desktop edits it, are written with
  plain line ends on Windows too, and keep Chinese text and marks such as `<` as written rather than as `\u` escapes; Ask
  Daoris's JSON answers show them as written as well.
- A tool that ends just as Daoris stops waiting for it no longer breaks the check: setup says the tool did not answer in
  time, where it said the tool "would not start", and an account's probe no longer fails outright. A run Daoris stops early
  now ends the tool's own child processes too, where they were left running.
- The same file attached to a quest twice at once no longer collides in the service on one shared temporary name: the
  service writes its attachments, helper answers, proposals and rule proposals through one atomic write, each beside its
  target under a name of its own and moved into place.
- A conversation whose account has used more than its usage curve allows moves on its own as its turn ends, to another of the
  same agent's accounts that has room, under Daoris's curve or your own (`--switch smart` or `curve`); `--switch off` keeps it
  where it is. It goes on in a new conversation in the same tree, told what Daoris kept of the old one, and both conversations
  say which accounts it moved between and how far over its curve the one it left was, in both languages. It does not move again
  within thirty minutes, and where no account has room it stays, with the offer to go on with another agent as before.
- Words said to a conversation that was handed to another agent or moved to another account go to the conversation that
  continues it: said while it moves, they wait and reach the new one after what Daoris told it; said afterwards, the old one
  takes none and says which conversation to write to (`daoris-driver sessions say` names it too). An old conversation no longer
  starts again in the tree its successor holds.
- Review measures a session's work from the newest commit it shares with its line, so work merged on the line since the
  session began is no longer shown as the session's, and a tree that holds nothing beyond the line says so; where the line
  has commits the work does not, the review's head says it is not based on the latest line, in both languages. Where a tree
  holds nothing beyond its line, Review says there is nothing to land and leads with *Discard tree*, offering no *Accept*, and
  its head names the tree's own branch, or *detached HEAD*, rather than a session branch that is gone.
- `daoris agent profile use` takes when to switch accounts, `--switch smart|curve|off`, and your own curve per window,
  `--curve session=0:85` or `--curve weekly=0:30,50:70,100:100` (`--no-curve` takes it out), and prints both; a curve that
  breaks its rules is refused saying which window, which point and why. `--early` and `--near` still work as the old names,
  and settings saved with them are read as the switch and the curve they meant, and saved that way.
- A landing rule names the pull request's branch as your repository names its own: its pattern may say `{ticket}`, the
  ticket key found in the title, then the request, then its links (`TK-2202` by default, or the shape `--key` names, as in
  `daoris driver landing <repository> branch "feature/{ticket}-{slug}" --key "[A-Z]+-\d+"`), the session's id where none is
  found. A branch's words are never cut from a web address, so a title that was only a ticket's address no longer makes
  `feature/https-…`.
- A landing rule may squash: with `daoris driver landing <repository> branch <pattern> --squash`, Accept makes the pull
  request's branch one commit from the session's work, its message the quest's title and each of the session's commits'
  subjects, made as your repository's own `user.name` and `user.email` (Accept says how to set them where git has none). A
  chain's later step adds one more commit to the same branch, and the session's head, Discard and the clean-up read the
  squashed session as landed. A session that committed on a branch of its own still lands as it stands.
- Ask Daoris can propose a repository's or a workspace's commit rule and helpers rule, and a landing rule's ticket key and
  squash, each as a card you apply with its terminal command beside it. It may quote your repository's own instructions (its
  branch names, how it writes a commit), which become the process only once you apply the card. A landing card that names
  neither the key nor the squash keeps the ones already set there.
- A repository's Setup shows and sets how its sessions commit (*Line and landing → Commits*: as the work goes, each commit
  after your yes, or once at the end, with your convention for commit messages) and what a helper its agent asks for does
  (*Sessions → Helpers*: wait for your yes, start at once, or not offered, and the agents in the order they are tried); a
  workspace's *Defaults* sets both for its repositories. Each says what it inherits and where it was set. *How work lands*
  says and sets a branch rule's ticket key and squash, and saving it there no longer drops a key or a squash set from the
  terminal or Ask Daoris.
- Daoris reads each Claude account's real plan usage, the five-hour and weekly windows Claude Code's `/usage` shows, with no
  prompt and no usage spent, so a start chooses its account on what is really left, the person's own use included, rather
  than on what Daoris's last session there said hours before.
- Claude Code's own pace warning, which says an account's use is ahead of the time gone in its window, no longer makes a start
  pass that account as near its limit: an account is passed when its limit is reached, when it draws on usage credits, or when
  a window's use is over its curve (below). A conversation's first line, an account's row on the Agents page and `daoris agent list`
  say the warning in its own words, *ahead of its pace*, in both languages.
- The offer to go on with another agent names another tool on an account with room, never the same tool through its other door
  or an account that is near its limit, refused or cooling; *Choose…* lists those tools first.
- When every account is near its limit, a start takes the one with the most room first, so it no longer stays on one at 99%
  while another sits at 60%; the start's note says so in both languages.
- A start chooses its account by a usage curve: an account whose use is over its curve on any window goes last, the least over
  first, and of the rest the one with the most room under its curve goes first. The curve is Daoris's own (the five-hour
  window up to 90%, the week its pace plus 20 points, never above 95%) or yours under `--switch curve`; with `--switch off` a
  start still passes an account over Daoris's curve, and ranks the rest by its week's pace as before. A conversation's first
  line says the window, its use and the curve's value, in both languages, and an account's settings mark the same accounts
  *near its limit*.
- `daoris agent profile use`, `daoris agent list` and an agent's account settings say what passes an account at a start, its
  limit reached, usage credits or its use over its curve, and the next start's steps by the curve, in both languages; the next
  start's reason names which of the three passed an account and on which window. `daoris agent profile use` marks a reading
  over the scope's curve with the curve's value now, *over its weekly curve's 63%*, where it said *near at 90%* before.
- A session whose agent committed its work on a branch of its own (as its repository's rules ask) is followed: its work, Review
  and Accept read that branch, Accept lands it as it stands instead of making another, and the tidy no longer warns it cannot read
  the tree of the emptied `daoris/…` branch the agent left behind.
- Review shows the work a session's tree holds that it has not committed, apart from what it committed, under *Not committed
  yet*, and no longer says "Nothing landed" over it; where Accept would refuse until those files are committed, the review says
  so before the press, in both languages.
- A repository's or workspace's commit rule says how its work is committed: as you go, asking you first, or once at the end,
  with an optional convention in your words on how a commit is written (one paragraph, at most 400 characters). Set it with
  `daoris driver commits <repository>|--workspace <name> [--mode as-you-go|ask|at-end] [--convention "…"]`; a repository's own
  rule replaces its workspace's, and none set is as you go.
- A quest's session in a tree of its own is told its process at its start, on a first start, a resume and a carry-on alike:
  the commit rule standing for its repository (as you go, asking you first, or once at the end, where it was set, and your
  convention quoted) and the branch its pull request will come from, named by the repository's landing rule. It is told to
  create, switch and push no branch, so it no longer stops between its repository's own rules and Daoris's. A repository that
  merges is told its commit rule too; a session in your checkout is told none of it.
- A conversation in a tree of its own is told its repository's process with your first message: the commit rule standing for
  it, your convention quoted, and how its work lands, the branch named by the repository's rule when you accept it. The
  conversation shows what it was told under its own line, in both languages; one in your checkout or handed over from another
  agent is told none of it. A session's *What it was handed* names the landing section's source as this machine's landing and
  commit rules.
- A quest's session can go on with another agent from the terminal (`daoris-driver sessions hand-over <id> --agent <adapter>
  [--account <name>]`): the driver stops it, then carries the quest on in the same tree on the agent you name, told it was
  handed over from the first agent's session, and it stays on that agent until the quest closes or leaves this machine.
  Both sessions say where it went and where it came from, in both languages.
- A working session can ask another agent for help, and you can too (`daoris-driver sessions helper <id> "<question>"
  [--agent <name>]`): a read-only session on that agent reads a copy of the session's work at its last commit and answers the
  one question, and its answer comes back to the asking session as its next turn, marked as the helper's and never as your
  words. Your `helpers` rule says whether an agent's ask waits for your yes, starts at once or is off; at most two helpers work
  for a session at once, and each takes a place under the session cap. The session's notes say, in both languages, which agent
  was asked, that it answered (or why it did not), and what a helper is helping.
- A conversation that asks another agent for help is given the answer as its next turn, marked as the helper's and never as your
  words: at once when the conversation is between turns, when its turn ends if it is talking, and as it goes on if it had ended.
- Sessions says who a session continues and whom a helper helps, in both languages. A session handed over from another agent
  says which agent and why under its title, *Details* names the session it came from with *Open session*, and its row in the
  list says *continues* and that session's title. A helper is titled *Helper*, names the session it helps by its title in
  the same three places, and offers no message box, since it takes none.

- When a conversation's account runs low, Daoris offers to go on with another agent: a line under its head, where the account
  is near its limit by its own word or cut, with *Go on with {agent} ({account})* and *Choose…*. It is never taken without your
  press.

- A conversation can go on with another agent from its page: *Go on with another agent…* in its head's ⋯ (*Stop and hand
  over…* while it works) asks which agent and account, then it goes on in a new conversation there, which the page opens; the
  old conversation's last line presses through to it.

- A conversation can go on with another agent from the terminal (`daoris-driver sessions hand-over <id> --agent <adapter>
  [--account <name>]`), as when its account's allowance runs low: it ends, and a new conversation starts in the same tree on
  the agent you name, told what Daoris kept of it (your request, its last plan and words, what is uncommitted); both say
  where it went. The page's press follows.
- A session's own scratch files in an ignored folder (such as `devtools/.tmp`) are no longer removed with its tree unasked:
  accepting the work keeps the tree and says which files; build and dependency folders still go with it.
- A Codex session is handed Daoris's knowledge tools again: they failed to start under Codex, which passes an MCP server
  only the variables its entry names, and the entry now names the Daoris home.
- A new conversation's agent is told it has Daoris's browser, and by which tool name, with its first message, so it uses
  that browser (and the sign-in you made in it) rather than reaching for a tool of its own; the conversation shows what
  it was told.
- The tree chip on *New conversation* names the branch your checkout is on (*The checkout · main*), and says so when HEAD is
  detached.
- A quest's agent, and an intake's, is told it has Daoris's browser, and by which tool name, in its instruction, so it uses
  that browser for a page behind your sign-in; a session that was not handed one is told nothing.
- A question or task handed to Ask Daoris (*Make it a task with Daoris…*, *Ask Daoris about it*) opens on a new
  conversation when the last one has ended, instead of under its history.
- *New conversation* opens on the repository you were just in, and *Recent* lists where you have been working, not only
  what the composer started.
- Cleaning up a landed branch keeps its commits recoverable: they stay on a ref under `refs/daoris/discarded/`, and the
  removal says where, as removing a session's branch already did.
- Ask Daoris's go to *New conversation* opens the composer with the repository chosen and the words in (above any you had
  typed); nothing is sent. The composer gains a quiet *Make it a task with Daoris…*, which puts its draft in Ask Daoris for
  you to send.

- `daoris-driver ask` takes `--short "…"`, the ask's short title the composer's field gives, and Ask Daoris's ask card
  says it in its terminal line.

- A session that goes on with your answer no longer stops to ask you again while its own background work (a build, the
  gates) still runs: it waits for that work to end and goes on, as a new session already did.

- *New conversation* starts a conversation on your words. With nothing attended, Sessions asks what you would like to work
  on: type it, choose the repository (your recent ones first, each workspace's after, one with no checkout here saying so),
  where it works (the driver's choice, the checkout, or a tree of its own) and the agent with its account, and press Enter.
  It replaces *Start a session*, which opened an empty conversation. Ctrl+N and Workspace › *New conversation…* open it;
  *New ask…* is now Ctrl+Shift+N. A refused start says why under *Start* and keeps your words.
- *New conversation* adds a repository without leaving Sessions: *Add repository…* and *Import a folder…* sit at the foot of
  the repository list and open their drawers over the prompt, which keeps your words. What you add is chosen; an import of
  several opens the list on their workspace. With no repository to work in yet, *Add repository…* is the first press.
- A quest's, an ask's and a session's title section no longer moves its buttons: a long title takes two lines at most
  (one for a session) beside a fixed column of actions, and on a narrow window the actions take their own row. An ask's
  long request is clamped with *View full request*; a session no longer repeats its quest's whole title above the
  conversation, which moved into its *Details*.
- An ask can carry a short title of up to 40 characters, given in its composer or by Ask Daoris when it proposes the ask.
  Lists and the ask's page call it by that title, and the page shows the whole request under it.

- Ask Daoris can propose a workflow: a new one, or a new version of one on its newest, and optionally choose it for a
  repository or workspace. It is judged as the editor would, said with your part first and what it does without asking
  you, held as a draft while something it needs is missing, and saved only on your Apply. Its room names each
  repository's workflow.
- A branch's name in a repository's or a workspace's Branches tab opens the branch's own page: its kind and where it stands
  against its line, origin and its pull request, then its commits since it left its line beside the line's since then, 200
  at a time, with *Back to Branches* in its header. The line's own name opens History.
- The intake can propose which kind of work an ask is, quoting your words, from the kinds its workspace declares; it never
  sets one, and the ask keeps the proposal for you to apply.
- A second opinion's gate says how much of its context the reviewer used, or that it was not measured; the reviewer's own
  session's last note reads in your language.
- A repository's History reads further back: *Show more* adds the line's next 200 commits under the last. Its ⋯ draws
  *All branches*: the graph over every local branch and origin's beside the line.
- A workspace's Branches tab says which empty session branches Daoris cleaned up on its own in the last week, and which it
  left in place and why.
- A workspace's Branches tab lists each of its repositories' branches above the clean-up, a group per repository holding a
  branch of Daoris's, headed by a door to the repository's page; the first five are shown and the rest a press away. The
  repositories holding none are folded apart, and each one you include is listed too.
- A repository's Branches tab lists its branches by kind. Its line heads the list: what set it, how it stands to origin and
  when the checkout last fetched. Then sessions' branches, each named by its session's title and state; landed branches, each
  saying whether it is pushed and opening its pull request, with its state and when where a plugin answered; and yours, the
  first five shown and the rest a press away. Each says how far it stands from the line or from origin. Branches only
  origin holds are behind the list's ⋯.
- Ask Daoris can now propose clearing finished history for a quest, an ask or a workspace, or a closed quest's failed
  sessions. The card lists what goes and what is kept and why, and Apply clears exactly what it listed.
- Ask Daoris can take you to a repository's Branches or History tab.
- A plugin's update plan shows *Update now* as its one main button, and *Make a plugin* no longer says its heading twice.
- The plugin an agent was declared by, and the plugin a repository's or workspace's work lands through, open that plugin's
  page in Plugins.
- A conversation's notes that a review's set-up was or was not posted, and how a second opinion's findings were answered as
  its turn ended, now read in your language, in English and 中文. The service's message in them stays as written.
- A note that a session's background work ended says how in your language: completed, failed, stopped or ended, in
  English and 中文. A state Daoris does not know is shown as the agent sent it.

- A conversation's notes that its agent's session could not open or failed, that a turn could not be taken, and that the
  agent would not go on in its own conversation (and why, and that your answer is carried on in a new session) now read in
  your language, in English and 中文. The program's message and the agent's own words in them stay as written.
- A repository's page in Repositories has Branches and History tabs. Branches lists its branches, each with its last commit;
  History draws its line's commits newest first, each beside its lane, a merged branch in a lane of its own until the commit
  it grew from. Both read this machine's checkout: a browser shows Details alone, and a repository with no checkout here
  says so.
- Ask Daoris can now propose pausing or resuming an ask's or a quest's work on this machine. The card says what stops (the
  running sessions), what it adds where another machine may still take an open quest, and what is left as it is; Apply makes
  the same pause or resume the pages' buttons do. Abandoning stays yours: it takes your reason, so Ask Daoris never proposes it.
- A conversation held because its account is cooling or signed out, or its agent is not installed or its pin has nothing
  installed, now says so in the page's language, not in English only. So does one held because every account of its list is
  waiting or not ready (each account named as you named it), a pick refused, an account its provider refused, an agent you
  install with its own tooling, and the accounts a wait adds that are kept for conversations or outside the list.
- `daoris agent key <agent> --account <id|name>` writes a new key, read from stdin, into an account that is already an API
  key, so a key its provider refused is repaired without losing the account's name, lists, defaults or order; what marked it
  refused is cleared. Without `--account` the verb adds a new account as before.
- Ask Daoris can now propose an account's use, its order, *Try now* and the default cool-off: how an agent's list of accounts
  is used, the list itself (or a workspace sent back to this machine's accounts), an account's cool-off ended, and how long
  an account cools when its limit names no time. Each is a card you apply, judged as the Agents page and the terminal judge it.
- A key its provider refused now has *New API key* on its own row, which puts a new key into that same account, keeping its
  name, its lists and its place in them, where *Add an API key* made a second account.
- A conversation's opening line and its wait for an account now name the account as you named it, not by its id, where
  Daoris knows the name.
- `daoris agent list` and a sign-in's end now say an account's email once when it is also the account's name, and
  `agent list`'s name column grows to the longest name instead of cutting a long email short.
- A conversation's note that your words wait for its account's cool-off (whose account, until when and why) and the way out
  of the wait now read in your language, in English and 中文. The account is named by its id there, and the command keeps
  its spelling. So do the conversation's other holds (no checkout, the repository's branches being brought up to date, Ask
  Daoris answering elsewhere); a message from the program itself stays as it wrote it.
- Ask Daoris can now take you to one plugin on the Plugins page, by its id, or to one of Daoris's own plugins you have not
  installed; it says so when a plugin is not on this machine, naming the ones that are.
- A session's notes about its own background work (its turn ending while it runs, the work ending, or a wait that could not
  be read) now read in your language; the work's names stay as they are.
- A quest you marked done now reads in your language where its note is shown (the quest's page, a session's timeline and
  relations): in 中文 it says 已由人标为完成。, and the words you wrote stay as you wrote them.
- A session's *Accept…* where the repository's rule puts the work on a branch now has a *Branch name* field: a name you enter
  replaces the rule's whole name, blank keeps it, and a name git would not take, a branch that already stands which Daoris did
  not make, or a merge rule is said in the ask before you press Accept.
- Ask Daoris can propose archiving or unarchiving sessions, by id or everything that ended, and deleting one. The card says
  what the list keeps and why, and nothing changes until you press Apply.
- A conversation's notes that an agent would not take what you added, that messages you sent while it worked never reached
  it (and why), that a message found the agent gone, and that your words could not be taken off its record or kept for it
  to go on with now read in your language, in English and 中文. The agent's own words and the service's message in them
  stay as written.
- Where you choose how work is reviewed (the ask form, an ask's page, a quest's review gate), a quiet line now gives the
  matching `daoris-driver` command, and the driver's usage for `quest` names that choice.
- A conversation's notes of your acceptance of its work, of handing it on, of an option you set for it, of where you were
  told you are, and of a browser server held back now read in your language, in English and 中文. The agent's and a plugin's
  own words in them stay as written.
- The Sessions rail's search of what was said reads a page of sessions at a time: under the results it says how many
  sessions matched and how many it lists, and *Show more* reads the next page, where it said more matched than it showed.
- The line above an agent's accounts saying which account the next start takes now speaks for the workspace the
  window shows, named (*In work: …*), where that workspace has its own scope. It showed nothing where this machine
  runs on the tool's own sign-in.
- A set-up step Daoris cannot show here is worded in full in English and in Chinese, naming its environment and why,
  where a Chinese window mixed in the driver's English sentence.
- An agent Daoris has no installer for offers *How to install* in the Agents list, where it offered Install: it
  opens the agent's page and installs nothing. Its page shows no Install and says the agent is installed with its
  maker's own tooling.
- An agent's page says above its accounts which account this machine's next start takes and why, in the
  words of *How accounts are used*, and what holds the account its list begins on where the start passes
  that one. An account's *Runs for* marks *(first)* where the next start takes it, no longer where its list
  begins.
- The line a conversation opens with, naming the account it started on and why, reads in the window's
  language: *Opened on `account-1`: Daoris started on it least recently* says itself in 中文 under 驱动,
  with each account drawn as code. What each account last said about its limits, a refused turn and the
  step among the rest are worded too. A driver's line with nothing to word it by, as older conversations
  hold, draws its backticks as code rather than as backticks.
- The line a conversation goes on with after it was parked or ended, saying whose words are its next turn and
  which agent it resumed on, reads in the window's language too, and a harness version that moved since it
  opened is a line beside it.
- The line saying a permission was refused, because the rules the agent runs under do not allow it, reads in the
  window's language too; the call it names stays as the agent titled it.
- `daoris wire <repository> --workspace <name>` moves a registered repository to another workspace from a
  terminal, as the page's Manage → *Move to workspace* does, and changes nothing else about it. An unknown
  repository, or a workspace a shared host does not serve, is refused in the service's own words.
- A toast no longer covers an open drawer: while one is open, toasts stand at its left edge, so an ask at
  the drawer's foot, such as a repository's Manage → Retire, keeps its Confirm and Never mind in view. A
  press on a toast while a drawer is open stays on the toast, rather than closing the drawer or pressing
  what it covered.
- Ask Daoris can propose moving a repository to another workspace on this machine, as its page's Manage →
  *Move to workspace* does: the card names the repository and both workspaces, and Apply changes that one
  row of the registry and no file. A workspace no repository is in yet is allowed, and the card says the
  move starts it; a move to the workspace it is already in is refused. Adding or importing a repository
  stays yours, since it needs a folder.
- Ask Daoris's *Go there* to Repositories' *Add repository* or *Import a folder* can name the workspace
  you mean: the card says it, and says when no repository is in that workspace yet. A workspace with any
  other place is refused on the card, and one named as a folder is never proposed. The folder stays yours
  to pick.
- Ask Daoris can take you to Repositories → *Add repository* or *Import a folder* with the workspace
  filled in: its *Go there* opens the drawer with the workspace you named already in its field, shown
  before you choose the folder. The folder stays yours to pick, and Import keeps that workspace rather
  than offering the folder's name over it; a name you type there is kept the same way.
- An ask Ask Daoris proposes keeps the review choice you gave it, and your words with it: its card says
  whether the work is reviewed in the default environment, in a named one or not at all, and Apply sends
  the choice as the ask composer does. A named environment the workspace does not declare is refused at
  Apply, as at a terminal. The kind and workflow stay yours to choose on the ask.
- Ask Daoris history orders and searches every conversation before it shows a page, so a pinned or
  recently resumed old conversation stays listed and search reaches older ones; one Chinese character is
  enough to search. `daoris-driver help list` takes `--offset` and `--limit`. *Show more* lists the next
  page, a search says how many conversations it found, and a row shows where in a long line the words were.
  The sessions rail's search and a conversation's find also take one Chinese character, and the rail's
  search reaches older sessions.
- `daoris plugin new` and `try`, a new plugin's README and the install's README send people to the
  Plugins place, which replaced Settings → Plugins.
- Ask Daoris names the places the window has now: Knowledge, whose list switches between Search and
  Convergence, the Plugins place and Get started. A *Go there* to Search, Convergence or Settings →
  Plugins from an earlier conversation opens where that place went.
- Ask Daoris can take you to a workspace's page: its Details, Branches, Workflow and Setup tabs, and
  Setup's Defaults and Remote and reach, on the workspace in view. It no longer offers Settings →
  Workspace or Permissions; a *Go there* to either from an earlier conversation opens where it went.
- Ask Daoris can take you to what waits on you: Sessions' *Waiting on you* and *To review*, and Quests'
  asks and the quests waiting on you. The list opens with that group at its top and its heading focused;
  you open the session or quest you mean. The start that counts sessions waiting on you opens them the
  same way. A list you closed, or one the window draws narrow, lies over the page for the go; Sessions
  kept by repository is shown by state, and a search typed in its list is cleared. Quests filtered to one
  repository show everyone's for a go to the quests waiting on you, until you choose in the list's ⋯ or
  leave Quests. What you chose for the list stays as you left it.
- Ask Daoris can take you to one quest or ask: its *Go there* opens Quests with that quest or ask
  chosen. One this machine does not hold is refused on the card; a closed one is found, and the card
  says it closed.
- Ask Daoris can take you to one session: its *Go there* opens Sessions with that session chosen. It is
  told the sessions waiting on you by id, and you may ask for another; one this machine does not hold,
  a teammate's, or an Ask Daoris conversation is refused on the card. Its count of sessions waiting on
  you no longer counts a park you have already answered.
- Account rows show each usage window as its own cell, in the same place on every row: the share used,
  a bar of that window's allowance, its reset and how old its reading is. A window the agent has not
  reported says *unknown*, never 0%. A Codex window that is neither five hours nor a week is named in
  the reader's language (*90 分钟*).
- An API key its provider refused says *key refused* on the start form, and Settings' next start says a
  new key repairs it, where both told you to sign in.
- Deleting a tool version, removing a plugin, retiring a repository, pausing or abandoning work,
  declining a quest, closing an ask, finishing or declining a session that waits on you, archiving
  ended sessions and discarding a tree anyway all confirm in place: the ask stays open while the act
  runs, says a refusal inside it, and returns focus to the press. So do the asks on a *What needs you*
  row: approve, refuse, choose, accept a rule, let it run, not yet, and go on anyway; and a workspace's
  *Use this machine's accounts*.

- Contributor guidance is shared across agents; development verification checks documentation routing
  and guide freshness declarations, including missing described source paths. Document checks ignore
  fenced examples when finding live entries, and share the orientation generator's file inventory.

- Named workflows govern landing, review and opinion from their bound version. Damaged run bindings
  hold the work until they can be read, preserving its required checks.
  Run diagrams and terminal reads show that bound version; landing previews explain workflow holds
  before offering Accept, and Keep confirms that its approval was saved.

- Ask Daoris history groups conversations and keeps titles readable in narrow panels. Starting a
  new conversation waits for the current one to finish and preserves the draft if that is refused.
- Knowledge index results show translated kinds and source lines; entry headers show source ranges.
- Account pages present compact rows, named accounts, per-account actions and a three-step add flow.
  Chosen names also appear in session facts, held-start messages and completion notices.
- Help → Update brings the update card into view. Development gate reuse now rejects stale verdicts
  after lane-policy changes, even when record declarations are broadened.
- Development checks reuse verdicts when only unrelated paths changed; `--stale` and checkout
  `--rerun` report the remaining checks needed before staging. Decision checks now reject duplicate notes.
- Trying a parked quest again counts from its recorded failures, including after an earlier retry,
  through the page, terminal and Ask Daoris.
- Publishing the packaged CLI waits briefly when Windows holds a fresh package or launcher folder.
- A conversation refused because its account is signed out marks that account signed out, so the next
  start walks past it. A conversation's agent is told which session it is, so a quest it takes is
  marked on its record.
- Downloading a managed tool waits briefly when Windows holds the folder just unpacked, at both doors.
- A failed attempt's branch can be discarded from a workspace's Branches tab, and from a session's
  page once its tree is gone, after one confirmation; a branch a live session uses is kept.
- What needs you lists work waiting on its accounts and signed-out accounts that hold work, with
  *Sign in*, *Read* and *Let … run* beside each, from what Daoris already knows and without asking an
  agent anything.
- Below about 620 px the window's seven menus fold into one menu, reached and walked by keyboard as
  the bar is. A conversation's notes that an account is signed out or cooling read in Chinese too.
- Doctrine: the canon teaches a generated index of where things are (a role, a section, a doc-loader
  step, a set-up step and a template), and a manifest may declare `index` beside its router.
- Doctrine: the file-tool rule tells an agent harness that has no dedicated read tools to read with its
  shell's read-only commands, so such a harness no longer stops at a read-only task. Run `daoris sync`.
- Doctrine: the scratch rule tells a harness with no file-writing tool to write a file through a
  program's file API, never the console's redirection, and to treat a helper script as scratch. Run
  `daoris sync`.
- The index lists a repository's knowledge document that has no frontmatter by its first heading, in a
  table of its own, and `check` says how many once, never failing.
- A workspace with a remote no longer stops syncing for good when a person's wait on a quest crosses
  another machine closing it; that wait is now dropped rather than sent as a move the remote refuses.
- A permission rule followed by a line break is refused at every door, as the terminal already refused
  it; the window could write one into the rules file before.
- The terminal compares account, workspace and other names without case exactly as the driver does,
  so a name like `straße` is never mistaken for `STRASSE` at one door and not the other.
- `daoris agent list` and a terminal sign-in keep what they learn about an account's sign-in, so the
  window and the driver loop see it too.
- Development checks of documentation dates judge the commit an open merge would make, so a merge
  that changes source without its document is refused before it is committed; the devkit's own docs
  gate does this too, for any repository that gates a merge before committing it.
- An account refused for its sign-in after Daoris last read it signed in is asked once more before it
  is passed over, so signing in again is noticed at the next start. `daoris-driver --once` and
  `--until-idle` print each report as it is made and wait for their sessions before exiting.
- `daoris init` writes a `.gitattributes` holding `* text=auto eol=lf` where a repository has none, so
  the files `sync` writes keep their line endings on Windows; an existing one is never touched.
- The activity bar holds its places and Settings alone: refreshing the index and switching language
  are in View, the palette and Settings → Appearance. Settings keeps seven domains: Plugins is its own
  place (its folder is under Driver), and Setup is called Get started.
- Search and Convergence are one place, Knowledge, whose list switches between them; each keeps what
  it showed, and every way that reached either still lands there.
- Knowledge search finds an identifier from its words (`ProbeLock` from "probe lock") and lists each
  dated note of a decision as its own result, so a question lands on the note that answers it.
- A quest's requirement may name files the done must have committed: the driver reads them at the
  session's end (and `daoris-driver quest check` reads them from a terminal), and the done is held until
  they are found or the person says yes.
- The local service can list and clear finished history: a closed quest's work, an ask's, or a closed
  quest's failed sessions, each judged again as it goes. A quest a remote holds is forgotten on this
  machine only. The driver now clears what the home kept of them too, never a tree in use or a standing
  branch. At a terminal, `daoris-driver history` says what each workspace keeps, and `history clear
  --workspace`, `quest clear` and `ask --clear` list what would go until `--yes`. On the screen, a
  closed quest's ⋯ and a done ask's offer *Clear from this machine…*, and a workspace's Details shows
  what this machine keeps with *Clear history…*; each lists what goes and what stays before it clears. Deleting a conversation now also takes the
  four small files it used to leave behind.
- Deleting a quest or a conversation no longer lets the next change reuse a number a remote already
  holds, which could lose that change silently on a workspace with a remote.
- A refused account rename, workspace choice or add-flow answer stays open with what was entered and
  says why inside it. Terminal hints spell account and workspace names so they paste as one argument in
  Command Prompt, PowerShell and POSIX shells, or name the argument where no spelling would; Settings'
  account hints spell them the same way, and so do the driver's own sentences and Ask Daoris's
  terminal lines.
- A failed session's work is no longer carried on over a quest another machine has since taken; the
  refusal names who holds it.
- `daoris-driver sessions start-from <id>` carries a conversation's words into a new one at a terminal,
  as the window's *Start from* does. The sessions list shows a chat whose words wait as going on, or
  as resuming later with its reset time.
- `daoris-driver` exits 2 naming the timeout when the service does not answer in time; only Ctrl+C
  reads as a stop.
- Deleting a session names any of its files the disk kept, instead of saying all went; `daoris-driver
  sessions delete` exits 2 when one stayed.
- Knowledge search reads a repository's declared index, a section at a time, and every result names
  the lines of its file; asking for an entry can ask for a range of its lines.
- A question asked on a quest whose take this machine then lost to another machine no longer holds
  the winner's quest; the lost take's conflict names the question instead.
- The menu bar says which of its rows are ticked to a screen reader, its names and the folded menu's
  button are easier to hit, and arrowing through a segmented choice moves the focus with it.
- The map's size and a region's shown view are announced as the ticked choice, and the command
  center is easier to hit.
- Every destructive ask (clearing history, discarding a branch, stopping or deleting a session, a
  quest's or an ask's Delete…, removing an account) stays open while it works and says a refusal
  inside itself, then hands the focus back to what opened it.
- What needs you offers *Let … run …* only for an account known to be ready, and a signed-out account's
  row says when it was last read rather than how long it has waited.
- A workspace's history reading no longer says a clear would take nothing beside a *Clear history…*
  that would take records.
- Knowledge search reads an index's tables a row at a time, so a question about one route or one
  command lands on its row and names its line.
- What needs you lists an ask or a quest held because its accounts are signed out even when no
  account is cooling, with *Sign in* or *Read* beside it.
- Branch rows stack their sentence under the branch's name when their list is narrow, and a long
  branch name wraps whole instead of being cut.
- Red words (danger buttons, refusals, removed counts) are easier to read in the dark theme, and a
  workspace's history reading says why each thing is kept in fewer words.
- An agent whose own output quotes a refused-key error no longer marks its account refused, and a
  note about an account's limit no longer repeats the agent's own sentence to other machines.
- A repository whose documents repeat a heading next to one already numbered, such as `A`, `A` and
  `A (2)`, no longer fails its knowledge refresh, and a table quoted inside a longer code fence is no
  longer read as part of the index.
- A quest's evidence that could not be read (git did not answer, or a folder was refused) is said as
  not read, instead of as missing.
- A history clear that took nothing stays open and says why each thing was kept, a clear that took
  only part says so as an error, and the reading no longer claims there are no files when it only
  measured 0 B.
- A confirmation says it is working once pressed, leaves the focus somewhere sensible once its act
  lands, and a long refusal wraps instead of widening a narrow window.
- A session whose quest was taken first on another machine can no longer close or decline it, or
  make it wait, after this machine learned its take lost; it is told to stand down instead.
- A confirmation opened near the bottom of a page now comes fully into view, its buttons included.
- A history clear that would only take left-over files says just that, instead of naming conversations
  and records it will not touch.
- An abandon that left part of its work behind is said as an error rather than as done.
- Knowledge search reads an index's lists an item at a time too, so a question about one fixture or
  one method lands on its line.
- A workspace's Branches tab looks for updates, counts and lists only that workspace's repositories,
  not every checkout on the machine.
- A note about an account's limit says whether it was the five-hour or the weekly limit, and which
  agent's account it was; older notes keep reading as before.
- A comma or full stop after a command or branch name no longer starts a line of its own, and a
  session's header shows a long branch name whole.
- Status words (open, taken, done) are easier to read in both themes, as danger words already were.
- `daoris-driver trees clean` and `trees sync` take `--workspace <name>`, as a workspace's Branches tab
  does, and refuse a word they do not know instead of ignoring it.
- `daoris-driver trees land <session>` takes `--branch <name>`, in `--plan` and the press alike: the name replaces
  the landing rule's wholesale, a name git would not take or a branch Daoris did not make is refused in words about
  that name, and a rule that merges, making no branch, refuses a name rather than ignoring it.
- A quest sitting because another machine took it says so in Chinese too, naming that machine.
- Codex accounts show how much of their five-hour and weekly limits they have used, read from Codex
  itself without spending usage, and rotation switches before their limits as it does for Claude Code.
- After *Resume*, what still holds a quest reads in Chinese too.
- `daoris agent list` and `profile use` weigh a Codex account's usage windows, as they do Claude Code's.
- An account whose API key was refused says *key refused* and asks for a new key; a cooling account
  says until when Daoris waits, apart from when its limit resets; and its usage is easier to read.
- Codex's own sign-in shows its five-hour and weekly use after *Read again*, without signing in an
  account for Daoris and without spending usage.
- Agents → Codex offers *Add an account…* and *Sign in*, running Codex's own sign-in into an account
  Daoris keeps, and reads whether each Codex account and your own sign-in are signed in.
- Signing in to Codex shows a link and a one-time code to enter there, so it works where Windows has
  reserved the port Codex's browser sign-in listens on; `daoris agent login codex` uses the pinned Codex.
- Claude Code and Codex have their own marks in the folded agent list, and an account's state and who
  signed in can be read without hovering.
- Adding an API key or saving an account's model keeps what you entered until it is saved, and says a
  refusal there; a key added asks for its name and workspaces, as a sign-in does.
- Answering a session that stopped to ask you no longer fails it: it carries on with your answer, once.
- A session that ended with work still on its branch, even a failed one or one finished at a
  checkpoint, offers *Accept…* on its page to land that work, and the Branches tab says so.
- The window no longer redraws continuously for a pulsing status dot, and it logs when its own thread
  is held, so a frozen title bar can be traced.
- A quest left taken after you finished its session can be marked done by you, with a note, from its
  page, from the session's *Finish…*, or with `daoris-driver quest done`.
- Saying something to a session while the driver looks at it no longer fails with a server error: the
  service's database now takes one request at a time on its shared connection.
- An ask that names its repository now proposes that repository first, even one that has declared
  nothing, instead of whichever repository its other words matched.
- A working session's transcript file now fills as the session speaks, not only when it ends.
- A request the service fails on now answers with a sentence, and its log says what went wrong and where.
- An installed Daoris now carries its own knowledge connector and hands it to every session, instead of an
  older copy that could rebuild the knowledge index at an older version and break search.
- An update that meets a folder held by a scanner now waits for it rather than rolling back, and says how
  long it waited.
- Settings → Logs says when a value is cut short, and opens it whole — an error's frames one per line.
- An older Daoris meeting a knowledge index a newer one wrote leaves it alone and says so, instead of
  rebuilding it at its own version; quests and sessions keep working meanwhile.
- A quest published without a short title gets a list name that says more than its first word.
- Re-publishing an install that carries its own service without `--service` is refused, naming what it would
  leave behind.
- An ask's proposal of a repository its words name says *named in the ask*, not *shares …*.
- Answering every go-ahead a waiting session asked on its ask now sends that session on with your answers.
- A workspace or repository can declare where its work is reviewed before it lands (Setup → *Review before
  landing*, or `daoris driver review`); nothing waits on it yet.
- Settings → Logs no longer fills with a warning about `server/discover` for every session.
- The update banner says in your language why an update was put back or refused for its two newest reasons.
- A session branch whose work a squash-merged pull request or a cherry-pick already holds can be discarded
  without forcing, and Daoris says where its work is.
- The Branches tab says which completed pull request carried a session branch's work, instead of saying a
  session still holds it.
- Answering one of a waiting session's go-aheads on its page no longer sends it on while another is still open;
  the page says which one it waits on.
- A session branch discarded because its work is held elsewhere now keeps its commits at a recovery ref the
  sentence names, and a tree holding files only it has is kept.
- A workspace or repository can name the agents that give a second opinion before its work lands (Setup →
  *Second opinion before landing*, or `daoris driver opinion`); nothing asks one yet.
- Work in a repository whose review rule says so now waits for your *reviewed* (or a skip) before it lands;
  `daoris-driver quest review` gives it.
- A web page in your browser can no longer press a door on Daoris's local service.
- Setup's *Review before landing* now says that work there waits for your review, and how to give it.
- Daoris's local service answers only to its own loopback names, so a website cannot read it through a rebound
  name.
- A conversation is handed a plugin's servers even when that plugin also adds an agent.
- A session whose work a squash-merged pull request already carried offers its discard instead of *Accept…*.
- Work waiting for your review is shown in Daoris's own browser, served where the app runs, until you give
  your verdict; *In review* beside the browser's door shows it again.
- Ask Daoris keeps its conversations on this machine: *History* lists, searches, renames, pins and deletes them,
  an ended one goes on in its own conversation, and a new one can start from an earlier one's transcript.
- `daoris driver workflow show --repository <name>` says how work moves in a repository: each step, who does it,
  which rule sets it, and what is not built yet.
- Work waiting on your review says so where *Accept* was: *Review in <environment>*, with *Reviewed*, *Not yet…*,
  *Show it again* and *Skip…*, on the session, the set-up step's page, What needs you and the strip; an ask can
  choose its review when it is written.
- A repository's and a workspace's page have a *Workflow* tab: how work moves there, drawn step by step, with who
  does each step, which rule sets it, and a door to change it in Setup.
- Work already on the line by a squash merge is no longer landed a second time from the review's *Accept*: the review
  says so and offers to discard the branch instead.
- Daoris removes by itself a session branch whose work is already on the line and whose tree holds nothing, and
  says so in the machine log; everything else stays for *Clean up*.
- A Daoris host that fails to start says why on the window: the last lines it printed, not only that it exited.
- Where a repository asks for a second opinion, its work waits for it before landing, at every door; the person can
  ask, stop, land anyway or say they looked themselves (`daoris-driver opinion`).
- Ask Daoris's history no longer runs past the side bar's edge when a conversation's title is a long link.
- The review choice can be set from a terminal: `daoris-driver ask … --review`, `ask --set-review` and
  `quest review <id> off|on|<environment>`.
- Workflows can be named and saved: `daoris driver workflow new|edit|apply|list|show|export|import`, each version
  kept and each change shown with your part first; nothing chooses one yet.
- A session's side bar has a *Workflow* view: where its work stands in its repository's workflow, step by step, with
  doors from a quest, an ask and What needs you.
- A `driver.json` that does not read is said with its file and where, instead of a stack trace.
- `daoris-driver workflow run` prints where a piece of work stands in its workflow, step by step.
- Where a second opinion holds work, the session shows it before the review: what the other agent found, the working
  session's answers, and the presses to ask again, go on anyway or say you looked yourself.
- A repository or a workspace can choose its workflow, per kind of task (`daoris driver workflow use|kind`), and an
  ask can name its kind; work started then is bound to the version chosen, though no gate reads it yet.
- Accepting a candidate with Enter in a Chinese (or any) input method no longer sends the message or runs the
  command; Escape that drops a composition no longer closes the box.

The first version: doctrine that installs, is checked, and flows back.

### The tool

- **Nineteen commands.** `analyze` reports what adopting would do before it does it; `init` writes a
  manifest and reports what is available without guessing;
  `sync` materializes the selected packs and writes the lock; `check` gates on drift, staleness and
  index freshness, and **reports** the always-loaded budget rather than failing on it (a fact gates, a
  judgement reports — D54); `upstream` promotes a locally-improved file back into the
  canon (`--all` for every edit at once); `index` says where the roster went — the `AGENTS.md`
  region, which `sync` regenerates (D59);
  `status` summarizes — the remote disclosure declaration included, and this machine's wiring with
  `--machine` — and reports when a newer canon is available; `doctor` reports local documents that
  restate a canonical one under a different name; and the **management** commands (D35, D50) are
  opt-in and never run by a gate — `connect` registers the repository with a knowledge service,
  carrying its declaration and, to a local service only, its root; `retire` takes it off the machine's
  registry without touching a file; `wire` moves a registered repository to another workspace and
  changes nothing else about it; `import` registers a folder's subdirectories at once; `brainstorm`
  puts a question to this machine's agents through the Daoris running here, which freezes a packet the person reads before
  anything goes (D176); `remote`
  edits the machine's map of one deployment per workspace, talking to nothing and never printing a key
  back; `agent` manages the agent tools sessions run on and the named accounts they run as, spawning
  each tool's own installer and sign-in — and keeps an API key only for an account that is one
  (D67); `driver` sets what this machine drives; `plugin` lists, adds, updates, removes and switches the
  machine's plugins, loading no code from any of them; and `browser` keeps the in-app browser's
  favorites, which it shows in a Daoris folder on its bookmarks bar, and its settings, among them
  whether it is Daoris's own or the person's Edge and whether the page's links open there; and `tool`
  says which file each program Daoris runs beside its agents is — Git, Node.js, PowerShell, GitHub CLI
  and Azure CLI — the system's from `PATH` unless the person names a file (D121). `agent`
  **spawns**; `connect`, `retire`, `wire` and `import` talk to a service, and `brainstorm` to this machine's own only; `remote`, `driver`, `plugin`,
  `browser` and `tool` only edit files under the Daoris home.
- **A pack may switch a core row off, and the repository confirms it** (D71). A pack's `pack.json`
  offers `switchesOff`: a core rule, knowledge document or skill, with the reason its own document
  replaces it. The row goes off only when `daoris.json` names it under `switchedOff`. Until then it
  stays on, and `sync` says so, with the line that would confirm it. `sync`, `check`, `status`, the
  doctrine region's roster, `init` and `analyze` all name what is switched off and by which pack. An
  edited core row being switched off refuses and points at `upstream`, since the canonical file still
  exists. Two selected packs shipping the same document are now refused.
- **`doctor` covers the one gap the lock cannot.** A repository's own rule duplicating a canonical one is
  local, and local is invisible by design — it surfaced on the first adoption only because someone read
  the generated index end to end. Advisory by construction: word overlap is crude, and a false positive
  that failed a build would be worse than the duplication. Validated against the real case, where it
  independently finds a 58% overlap that previously took a manual read to notice.
- **Zero runtime dependencies.** Node ≥ 22, ESM, `node:test`. Nothing to install — every command runs
  as `npx daoris@<version>`, the npm package the release publishes, and `init` writes that as the
  manifest's `source` (D105).
- **The canon ships inside the package**, so the pinned version *is* the doctrine's version. No command
  fetches anything, which is what makes `check` offline by construction rather than by discipline —
  asserted by a test that deletes the canon and requires a clean exit.
- **Three layers.** Core installs everywhere, except a row a pack offers off and the repository
  confirms (D71); packs are named in the manifest; the
  repository's own documents are never synced and never touched. Anything absent from the lock is
  invisible to the tool.
- **Two refusals, distinguished by provenance.** A file in the lock that changed on disk is *drift* — the
  repository edited something Daoris owns. A file *not* in the lock at a canonical path is a *collision* —
  the repository wrote it before adopting Daoris. Both stop the sync; they carry different advice,
  because they are different mistakes.
- **Drift is measured against the lock, not against the current canon.** Comparing on-disk content to
  freshly-rendered canonical content made "the repository edited this" and "the canon improved" the same
  observation, so an improved rule could not propagate: every consumer's `sync` exited 1 over an edit
  nobody made. Now the lock's recorded hash answers "did this repository change it" and the canon answers
  "is there something new to install." See `docs/DECISIONS.md` D13.
- **Retirement.** A file removed from the canon is removed from every repository on the next sync — the
  one thing copy-paste can never do.
- **A renamed canonical file is reported as a rename**, not as a retirement plus an unrelated addition.
  Detected by pairing content rather than by a declared field, so it cannot claim a move that did not
  happen, and it covers core as well as packs. Conservative by design: an uncertain pair stays described
  as two separate changes.
- **A one-line provenance header** on every materialized file, because an agent that opens a rule needing
  a tweak will otherwise simply edit it. The lock's hash catches the edit either way.
- **`--force` names what it destroys.** It is the only way to lose work with this tool, and it was
  silent about doing so. A refusal names the file it is protecting; the override that overrules that
  refusal now names it too, or nothing anywhere records what went.
- **Retiring a file the repository has edited refuses instead of deleting it.** Retirement is the most
  destructive thing `sync` does and had the weakest guard: a retained file that drifted refused, while a
  retired one was deleted silently — at the worst moment, since the canonical file the edit belonged to
  is gone and `upstream` is no longer a route. It now advises keeping the edit as a local document.
  The full lock × disk × canon state space is enumerated in `docs/DECISIONS.md` D19, so the next gap is
  found by reading rather than by losing a file.
- **Every path is confined to the target directory.** `sync` resolves each write and delete against the
  target and refuses anything that escapes it, before touching a file. A lock entry containing `..` could
  otherwise reach arbitrary paths — and the lock is generated, so it is the file nobody reads closely in
  review. See `docs/DECISIONS.md` D18.
- Atomic, BOM-less, LF writes throughout; exit codes are the contract (`0` clean, `1` policy failure,
  `2` tool error).
- **A release rehearsal** (`npm run rehearse`) that packs the tarball, installs it into a clean
  repository, and drives the whole consumer lifecycle through the `bin` entry — adopt, collide, sync,
  drift, promote, upgrade, rename, check. The test suite exercises the source tree; this exercises the
  artefact, which is where install stories actually break.

### Skills

- **A third tier.** `skills/<name>/SKILL.md` installs, retires and drift-checks like everything else, and
  core is now laid out exactly like a pack (`core/rules/`, `core/skills/`) so one code path reads both.
- **Canonical skills are parameter-free** and delegate to the generated index; there is no substitution
  map in the manifest. Decided from a survey of twelve repositories and 134 skills — see
  `docs/DECISIONS.md` D14.
- **The index gained a skills table**, which is what a hand-written "here are our skills" skill always
  was: generated content. It marks the repository's own skills `(local)` like every other row.
- **The provenance header moves under the frontmatter.** Frontmatter is only frontmatter at byte 0 — the
  harness parses a skill's `description` to decide whether to surface it, so a comment above the opening
  fence would have made every canonical skill silently unreachable, with no error anywhere.
- **`skills-workflow` is now a core rule** — it appears in six of eleven surveyed repositories, tying
  `sensitive-info` as the strongest signal in the family, and its copies diverge the most.
- **A skill's supporting files travel with it.** A skill is a directory, and the platform lets it carry a
  reference document, a template, or a script it invokes through its own directory variable. Only the
  `SKILL.md` was being materialized, so such a skill would have installed with its first step pointing at
  a file that never arrived. Markdown is stamped with the provenance header; other files are copied
  verbatim, because an HTML comment in a script is a syntax error.
- **The return path closes without `--force`.** After `upstream`, the file on disk already *is* what the
  canon would write, so only the lock hash is stale — but `sync` read that as drift and demanded
  `--force`, whose documented meaning is "discard your local edit". The last step of contributing an
  improvement advised throwing it away. A file matching the current canon is no longer drift whatever the
  lock says.

### The canon

- **The always-loaded tier is a region of `AGENTS.md`, not a directory beside it.** Measured across the
  three harnesses this family drives: `.claude/rules/` is read by exactly one of them, so Daoris had
  been shipping an always-loaded tier whose always-loaded-ness belonged to one tool rather than to the
  doctrine. `sync` now writes the core rules into a marked region of `AGENTS.md` — the only file all
  three read — with `CLAUDE.md` carrying a one-line `@AGENTS.md` import region for the one that reads
  the other name and follows imports. Knowledge and skills do not move. The adopter's own text in
  either file is never touched, and a damaged marker, a reordered pair or a second region is **refused
  with its line number** rather than guessed at, because what is on the other side of that guess is
  their doctrine. `daoris.lock` gains one field: an entry carrying `in` is a span inside a file. See
  `docs/DECISIONS.md` D7 (amended) and D59.
- **Eight core rules**, each confirmed by appearing independently in multiple repositories in the family:
  `sensitive-info`, `task-lifecycle`, `no-tmp-for-repo-files`, `file-tool-discipline`,
  `persist-working-state`, `no-global-memory`, `skills-workflow` — and `repository-owns-its-work`:
  never write into another repository; publish the request and let whoever works there take it.
- **The doctrine does not require the tool that ships it.** An adopted repository stays fully workable
  for contributors who do not run Daoris, their agents included — they load the same vendored markdown.
  That survives almost everything the canon names, because what `sync` writes is committed: a generated
  index, a lock file and every rule are still there for someone who never installed anything. A
  *service* is the exception, and publishing a quest is the only one the canon instructs — so it names
  the alternative in the same breath ("a message to that repository's owner where none does"), and a
  gate refuses any canon file that instructs a quest without one.
- **Six core knowledge documents**, each knowledge rather than a rule because it applies to a
  situation, not to every task — a distinction the budget gate enforced more than once.
  `model-decoupling` (the model is a deployment choice: specify the feature without naming one, select
  the provider by deployment, report which tier ran), `claims-need-checks` (behavioural prose is
  verified against the implementation, with the check shipped in the same change), `leak-repair` (a
  committed leak is a history problem — how to actually scrub one), `reaching-in` (what happens after
  writing into another repository, and why the repair is worse), and `autonomous-development`
  (development is automation-first: a person sets the target and verifies the outcome, agents execute
  the steps between under gates, and destructive, irreversible, cross-repository and publishing actions
  stay explicitly human), and `development-documents` (the documents a repository keeps for a
  code-generating session: each with one role and one way it is read, a short brief every agent reads,
  ceilings on what is read whole, and what may run unasked declared where a tool reads it). The canon's
  own `CHANGELOG.md` carries the full reasoning per document.
- **Six core skills.** `set-up-documents` carries `development-documents`' procedure and a template per
  document shape. The other five were canonized from copies found across the family and reduced to
  what they share. `doc-loader` and `pattern-finder` (six repositories each) start a task; `post-feature` (four) and
  `fix-log` (three) close one; `caveman` (five) governs output. `fix-log`'s copies sat within 100 bytes of
  each other, so the invariant was nearly the whole file. `post-feature`'s looked least alike of any —
  one a stack checklist, another a diff-detection procedure — and the shared shape turned out to be the
  value. `caveman` is canonized for its **carve-outs** rather than its terseness: never compress a
  destructive or irreversible action, a security finding, or an order-sensitive sequence, and never write
  a durable artefact in the mode at all. A compressed warning reads as fluent English right until someone
  approves it without registering the consequence.
- **`status` names what a pending update would change** — `changed` / `new` / `retired` per file, instead
  of only reporting that a newer canon exists. Computed from the lock, so it stays offline; the
  provenance header is excluded, so a pure version bump reports "version only" rather than listing every
  file and training people to skip the list. `--json` renders the same facts machine-readable for the
  agent operator, computed once with the text so the two views cannot disagree.
- **…and why it changed.** The canon carries its own `CHANGELOG.md`, and `status` prints the entries for
  exactly the versions a repository is skipping. Which files moved is computable; whether it *matters* is
  a sentence only the author of the change can write, so the canon ships it alongside the documents.
- **A repository's own skills are reported as local** by `init` and `status`, as its own rules already
  were.
- **`doctor` scans skills, and its threshold is now set by measurement.** Checked against sixteen real
  pairs across the family: near-verbatim copies score 72–74%, twins that were *rewritten* rather than
  copied land at 34–43%, and unrelated documents at 7–16%. The old 0.5 sat above the middle band and
  caught 2 of 11; 0.3 catches 7 with no false positive. The threshold is asymmetric on purpose — the
  command is advisory, so a false positive costs a dismissed line and a miss costs lasting duplication.
  It also now states the duplicate it *cannot* find: word overlap detects restatement, not convergence.
- **Seven packs.** `windows-machine` (traps that succeed wrongly rather than failing),
  `dotnet-library` (package boundaries, naming, DI variation points, shipping registries, and API design),
  `storage-sql` (type affinity on read, migration numbering, full-text search for scripts without word
  boundaries), `desktop-app` (verifying a real desktop application — driving the running app, what
  synthetic input does not prove), `web-webview` (a web UI inside a native shell — resource serving,
  thread affinity, the silent failures), `durable-jobs` (long-running work that survives a restart
  — lanes, checkpoints, resume), and `localized-ui` (an interface shipped in more than one language —
  what belongs in a string catalogue and what must never, and the parity that stops a missing
  translation from silently working) — each with its reasoning in `canon/CHANGELOG.md`.
- Every canon file carries frontmatter that generates its index row; tests assert that, plus that no canon
  file contains a machine path.

### The service

- **`Daoris.Service`** — the knowledge layer beside the CLI (a separate deployable; the CLI keeps its
  zero dependencies and never learns about it). It indexes every repository's doctrine, decisions,
  fixes and task outcomes into SQLite with FTS5, answers ranked queries, and finds **convergence** —
  where two repositories reached the same conclusion in different words, which no text comparison can
  see. Semantic recall is opt-in by naming an embedding model; without one the service is lexical-only
  and says so on every answer.
- **The registry and quests.** Each repository declares in its manifest what it owns and what it
  accepts; the service serves that as the registry — search answers "has anyone solved this", the
  registry answers "whose problem is this". Cross-repository work moves as a **quest**: published to
  the service, pulled by the repository it addresses, taken / done / declined — declining needs a
  reason. Nothing is ever written into anyone's tree.
- **Deployable, two modes, one binary each.** Local needs no daemon: the MCP host (`daoris-knowledge`)
  is spawned per agent session and the persistent per-user store is what survives, shared by every
  repository's sessions on the machine. Remote is the HTTP host: registrations pushed by
  `daoris connect` persist across restarts, and quests publish and answer over the same shared judgement
  (`QuestExchange`) as the MCP host. There are two trust shapes and no third: **local** trusts the
  loopback — the OS account is the boundary, and the host refuses to bind anywhere else — and **shared**
  (the team deployment) gates every route with minted keys (below). It runs with **no model at all** and
  still carries the whole transfer of request and task.
- **The server ships as executables.** `npm run publish:service -- --install` publishes both hosts
  self-contained single-file into the home's `bin/` (D63) and prints the ready `.mcp.json` snippet with the
  family root filled in; releases carry the same binaries per platform with sha256s beside the
  devkit's. The hosts are safe to run from anywhere: the HTTP host finds its web bundle beside its own
  executable, and the MCP host says plainly when no family root is named instead of silently indexing
  the wrong tree.
- **The loops create their consumer.** Both the family rehearsal and the platform's Playwright suite
  run over a scratch copy of the example family and include a project **born mid-run**: created from
  nothing, joined through the real CLI — `init`, the domain declared, `sync`, `check` clean on first
  contact, `connect` — a member in the registry and the Projects view at once, quest-addressable on
  day one, and still there after a host restart.
- **`Daoris.Web` — the platform: the person's window over the family.** Its views land on
  management: **Overview** (is anything sitting and for how long, the family's health as stat tiles,
  the repositories by what the index holds), **Quests** (grouped by where each is in its life, sitting
  time made visible, publish behind a deliberate action — with the service's refusals shown verbatim
  and the form unable to offer the mistakes the service refuses), **Projects** (who is in the family,
  declarations as scannable chips, the local/canonical split, who cannot be asked yet with the join
  steps proposed as text), then **Convergence** — the knowledge half's lead view — and **Search**; *Sessions*, *Map* and *Settings* joined them on one
  activity bar (D66).
  Doctrine is never editable from the browser: where a rule should change, the UI proposes the command
  to run in the repository that owns the file.
- **A designed console, not a styled document.** One activity bar and a status bar with the global
  state stated once (D56, D66), a page header per view with its one primary action, a right drawer as the single
  detail-and-form surface (a knowledge entry, a quest's detail and its actions, the compose form),
  toasts carrying every outcome verbatim, designed empty states, and static skeletons. The quest-state
  palette is computed rather than tasted: both themes pass all six checks of a color-vision validator,
  and a status never appears without its text label.
- **Properly tooled, and bilingual.** The platform is built on headless libraries under the same
  design language — Tailwind v4 with the validated tokens as its theme, Radix primitives, lucide
  icons, TanStack Query for server state — and speaks **English and 简体中文** with an en/zh parity
  gate in the build. UI chrome translates; data and the service's own sentences render verbatim.
  Storybook serves as the living design tool over the shipped components, and the test pyramid runs
  as one gate: a Vitest inner loop over the view logic, the primitives and both catalogs, then a
  Playwright suite that boots the real host over the example family and drives the shipped bundle —
  members visible, a quest through its whole life in the drawers, the verbatim refusal, the language
  switch — so a release whose UI cannot do its job over the example family does not ship.
- **A refresh retires what the disk no longer has.** A repository renamed or removed used to stay in
  the index forever, served as though it were alive; a refresh now retires any repository the scan did
  not see — guarded on the scan having seen anything at all, so a mis-set root cannot wipe a good
  index. Found on the platform's own Overview, serving a repository renamed weeks earlier.
- **An example family under `examples/`**, tracked in full: two miniature adopters and
  `npm run rehearse:family`, which proves the router through the real artefacts — adoption,
  registration through `daoris connect`, a quest's whole life including its refusals, knowledge
  crossing projects, and a restart losing nothing.

### The driver

- **Daoris drives.** The service turns a quest queue into an execution queue: the local driver
  (`Daoris.Desktop`) watches the service, and where the person has opted a repository in, **spawns a
  fresh non-interactive agent session per open quest** — one active session per repository, onto a clean
  working tree only, oldest first. The spawned session **claims its own quest** over its own connector
  and closes it `done` or `declined`, exactly as an interactive session would; the quest state machine
  is the only lock, so a driven session and a hand-run one are indistinguishable. Driving is additive,
  never exclusive — a repository developed by hand loses nothing.
- **Observed, never self-reported.** A session's life is concluded from the two signals outside work
  also produces — the process's exit and the quest's own transitions — not an in-band protocol.
  Session **records** live in the service beside the quests (so the platform renders them and they
  survive a restart); the **process** never leaves the machine that spawned it. The record carries the
  reviewable evidence — the commits that landed — and a machine-local transcript path that is answered
  only to a caller on that machine.
- **One supported harness, others explicit.** The adapter seam's native adapter is `claude-code`;
  Codex arrives over the protocol door below as `codex-acp`. An unknown adapter is an error naming
  what exists, never a silent fallback. An
  adapter names a harness, never a model. Gate-proven with a **stub adapter** — real spawn, real claim,
  real commit, no model — and then by a **real `claude-code` run**: a quest became a session became a
  commit became `done` in 71 seconds.
- **A second door, and four harnesses through it.** Beside the pipe the driver has always had, a
  session may be held over the **Agent Client Protocol** — JSON-RPC over the spawned process's stdio,
  with tool boundaries, turn boundaries, thoughts and context usage arriving *by contract* rather than
  parsed out of another program's stdout. `claude-code-acp`, `codex-acp` and `dsh` are **configurations
  of that one door**, not three more seams, and an `acp-stub` gates the whole of it with no model.
  A permission request over the wire is **refused by the driver**, always. What a session did is still
  concluded from its exit code and its quest's state, never from what it said about itself — the
  protocol flattens an aborted or errored turn into an ordinary ending, so it enriches and never decides.
- **The permission posture is stated per harness, in that harness's own words.** The same boundary —
  edits inside the tree proceed, nothing outward is ever auto-approved — is `acceptEdits` to Claude
  Code, `agent` to Codex, and to dsh is not a wire concept at all but an environment setting. Each is
  named explicitly even where it matches a default, a mode the agent did not offer is never
  substituted for one it did, and a harness whose wire carries no posture is left at its own.
- **What Daoris writes into a harness home it created, and what it will not.** A dsh credential
  profile gets a patch layer turning off the two rows that ship enabled and send session material off
  the machine, plus a root that makes the repository's own skills reachable. Only in a directory
  Daoris made: run without one and dsh uses *your* configuration home, which Daoris does not write to
  — so it runs, and says what that means, and names the one command that fixes it. A home already
  holding a hand-written patch layer is reported, never overwritten.
- **The desktop shell.** `Daoris.exe` brings up the local host (adopting one already running rather
  than double-starting), carries the platform in its window — the same bytes a browser gets — and runs
  the driver loop in-process, re-reading the person's standing choices every tick: drivable and hold
  per repository, stop a running session, all through the platform's own session-control surface.
  The window renders on the Chromium the install carries, so the machine needs no WebView2 (D92), and
  an install is a small `Daoris.exe` launcher, the application under `app/`, and the home in `data/`
  (D93). Daoris's own browser runs on that same Chromium, as the application started as a browser, so
  an install carries one engine rather than two (D99).
- **Daoris's browser has a door on the strip, and the page's links can open there** (BRW7). A compass
  beside the region toggles opens it from every view, where View → *Browser* and the palette were the
  only ways in. Settings → Browser and `daoris browser links system|daoris` choose whether a link on the
  page — a ticket in a quest or an ask, a URL in a conversation, a link an agent wrote — opens in the
  system's browser or in Daoris's (its own, or the person's Edge). Every link opens through one place,
  held by a test; a sign-in link always opens in the system's browser, and a browser with no bridge
  opens links as it always did.
- **Daoris says who is driving its browser** (BRW8). A session handed a server that drives the browser
  is named beside the strip's door, *driven by engine · Read the ticket*, from the handing until it
  ends, and the chip opens it in Sessions; two or more are a count whose menu lists each. Settings →
  Browser names them too. It is read from what the driver handed, on a driven quest, an intake and a
  conversation alike, since the engine's window and Edge's are not Daoris's to draw in.
- **A plugin is a folder that declares, and may speak** (D64). Under the home's `plugins/<id>/`, a
  `plugin.json` names what a plugin declares — configurations of the ACP door, so a fifth harness
  arrives as a file — and what it speaks. The catalogue reads the API version before anything else,
  refuses a newer one naming both numbers, lists a broken manifest with its problem rather than
  crashing, and refuses a harness name this build carries or an earlier plugin declared, naming both
  sides. `daoris plugin list|add|remove|enable|disable`: an add replaces the install wholesale and
  leaves `.data/<id>` alone; a remove names the data folder rather than deleting it; disabled is a
  row, never a rename. No code from a plugin loads into any host.
- **A plugin may speak.** A manifest's `hooks` row names a process and the points it listens on; the
  driver starts it with the loop — in the plugin's own folder, its id, folder and data folder in the
  environment — completes a versioned handshake over JSON-RPC on its stdio, asks it at the points,
  and stops it with the loop. `quest/consider` is a fail-closed waterfall: the first hold in
  catalogue order becomes the quest's own reason for sitting, and a plugin that answers late, wrongly
  or not at all holds too, naming itself and the way out. `session/ended` is contained observation.
  Every line a plugin writes lands on the console under `plugin:<id>`. Reconciled every tick, so a
  plugin disabled between ticks is stopped at the next one. A plugin that only declares is never
  started. The family rehearsal drives one: a declared harness a session runs on, a quest held with
  the plugin's sentence, an ending kept in its data folder, and the switch from a terminal. The
  Settings page's Plugins card is the other door: a row per plugin with what it declares and speaks
  on, running or off, the driver's sentence under a refused one, the switch, and Remove naming what
  the plugin kept; a declared harness on the roster wears the plugin it came from.
  `examples/plugins/hold-by-title` is the tracked example of a plugin that speaks — the one the
  family rehearsal installs with the real `daoris plugin add` and drives.
- **A plugin hands every session its servers** (D65 §1f). A manifest's `servers` row declares MCP
  servers by name, command and environment, `${plugin}` expanded; the driver offers them beside the
  knowledge host over the protocol door, and over the pipe door hands a harness that takes a file at
  spawn one written under the home for that session — never the repository's own. The knowledge
  host's name is refused, and a name two plugins claim keeps the first by id; `plugin list` says what
  each hands. `examples/plugins/browser` declares the Playwright MCP, which is how *test it in a
  browser* becomes something a session can do; the family rehearsal proves it reaches a session from
  the agent's own account of what it was offered.
- **A conversation can run on a declared harness.** Both chat doors resolve the harness through the
  live adapter set — the desktop's through the roster the driver's tick updates, the terminal's by
  reading the home's plugins — so a plugin added after the shell started is a harness a chat can
  use now, without a restart.
- **What the shell adopted stands on the Settings page.** A host already running and serving another
  install's page was said once, as a toast raised before the page existed to hear it; the sentence
  now rides the driver state beside the home's, and stands for as long as it is true.
- **Settings is a page of rows.** Each setting is a label, a one-line hint naming
  its terminal twin, and the control at the right; the paragraph that motivated it is on an info
  glyph. The home's path sits under the header, the driver's two dials share a card, and wiring a
  deployment is behind a press — five cards of prose became a page a person scans.
- **The application's own folder is the Daoris home, and nothing lives under the user profile** (D63).
  Every machine-local file Daoris owns — the registry, `driver.json`, `harnesses.json` and the
  credential profiles beside it, the remotes map, the index, session records, the installed service
  binaries — lives under `DAORIS_HOME`, and every default in the CLI, the hosts and the driver derives
  from that one variable. The installed desktop sets it to its own `data/` for itself and every
  session it spawns, and once for the account when it has none, so a terminal's `daoris` meets the
  same machine; a `~/.daoris` from before moves in on the first start and the shell says so once. A
  second or moved install runs on its own `data/` even when the account's variable names another
  folder, leaves that variable as it is, and says so on Settings' home row (D105).
  With no home set the management commands and the hosts refuse in a sentence naming it, rather than
  writing somewhere nobody pointed them. `publish:service --install` lands the hosts under the home's
  `bin/` and prints the `.mcp.json` snippet with the home filled in.
- **Signing in happens on the account's row, and it can finish.** Measured on the real harness with
  no console: its login prints a sign-in link as a terminal hyperlink, then *paste the code* with no
  newline, and waits on stdin — which nothing held, so the flow could never complete and the page
  stayed disabled. A harness action now has its stdin, delivers a prompt that has no end once the
  stream goes quiet, and strips the terminal's own escapes; the page may answer it or stop it
  (`HARNESS_INPUT`, `HARNESS_CANCEL`, each refused naming the action when nothing runs). A process
  action is answered once it has started and **its end is news** (`HARNESS_ENDED`), because a login
  waits on a person longer than any request may wait on the bridge — the request had timed out at
  thirty seconds and closed the panel on a login still running. The row shows three steps — the link
  with a copy button, the code box once asked, the row's own pill as the result — with the tool's
  output one disclosure away, and a sentence naming the account when it ends.
- **A tooltip follows the editor's rules.** Below its control, aligned to its leading edge, beside a
  rail; gone on any scroll, key, click or loss of the window's focus, and the moment the pointer is
  off its trigger — it had stayed up over a scrolled list, a blurred window and a button that disabled
  itself on the click.
- **No spawn opens a console window.** The installed shell flashed a terminal on every tick: git,
  the harness probes and the sessions were started without `CreateNoWindow`, and a windowed process's
  console child is given a console of its own. A source scan holds it for every spawn site.
- **A quest carries links and files** (D65 §2). The composer takes links one per line, and files by
  drop, paste or *choose files…*; `quest_publish` takes `links` and `attachments` (paths on this
  machine); the drawer shows links as links and files by name and size, a picture as a picture. A
  link must be an http or https address. A quest carries at most 10 files and 20 MB in total, and
  the composer says why a file was left off while you are still choosing. **The bytes stay on the
  machine that published them**, under the home at `quests/<id>/attachments/`, kept only once the
  record exists. A remote learns names and hashes, and a shared deployment refuses content outright.
  A session is handed the files as `DAORIS_QUEST_ATTACHMENTS`, and its target names each link and
  file. A file kept on another machine is said to be elsewhere, never offered as a path. The host
  serves a kept file to this machine only, sandboxed, and anything but an image, a PDF or text as a
  download, so an attached page cannot run as the platform.
- **A quest can name what comes next** (D65 §4). `then` is a list of steps (develop, then verify,
  then report), and closing the quest `done` publishes the first in the same transaction, asked on
  behalf of the same asker. The step carries the rest of the chain and says which quest it follows;
  `{parent}` in its words becomes that id. The driver takes each step at its next look, so the
  chain *is* the loop, with no engine. A decline stops it. The chain is judged when it is composed:
  every step must be addressable, and every step must live in the same home. The composer offers
  a next step; the drawer shows what is coming and what a quest follows; `quest_publish` takes the
  list; and a driven session is told both what its quest follows and what closing it will publish.
- **One navigation** (D66). The activity bar lists Overview, Sessions, Quests, Projects,
  Convergence and Search, with Settings at its foot. The palette offers the same list, minus the
  view you are on. A remembered Sessions reopens it, and a browser has no Sessions to reopen.
- **A settings page, and the theme is yours to choose** (D66). Settings holds *Appearance* (theme:
  system, light or dark; language) and, on the desktop, everything *This machine* held. A chosen
  theme applies before the first paint and repaints the window's own caption buttons. In a browser,
  Settings is appearance alone.
- **The activity bar's counts are circles.** Measured on the window: 14 × 17.2px before, 16 × 16
  after. Waiting sessions wear the status hue, and outstanding quests the accent.
- **A new installation says what to do first.** With nothing registered, Projects says so and offers
  *add repository*. The ask and quest composers say nobody can be asked yet, instead of a form that
  could never send. A command in a sentence reads as code rather than between backticks, in both
  languages and in the service's own sentences. A failed session no longer wears a success-green
  dot. Chinese is never slanted by a made-up italic. And the rules card explains its defaults in
  中文, with one word for "agent" and one for "the driver" throughout.
- **Convergence answers about three times faster**, with the same answers: about seven seconds a
  call on a 1,052-entry index before, under two and a half after. A drawer no longer dims half the
  status bar, and two accounts signed in as one person say which is the tool's own.
- **Asks say what they wait for, and old claims are gone.** An ask's card says when an intake is
  reading it or has asked you, and names its place as a workspace. Its record no longer repeats a
  one-line ask or reads "answered by by". The map says *waiting on you* for a parked session and
  names the number in a repository; a search snippet shows the entry's prose, not its frontmatter;
  Convergence's empty answer offers a lower similarity. Sentences that stopped being true (the
  index scanning a folder, only adopters being askable) are rewritten, the default rules explain
  themselves without decision numbers, and 中文 calls a quest 委托 throughout.
- **A session is a conversation** (D76). An attended session shows what was asked and what the agent
  said and did: its words as Markdown, code highlighted with a copy button, thinking folded, each tool
  call as a row with its file, its status and an edit's line diff, the plan, and a finished turn's
  work folded under its answer. It is read back after a restart, a page at a time, and follows the
  newest words until you scroll up. The console stays, as the raw view. Sessions on the protocol door
  have it, and so do Claude Code's driven sessions, intakes and chats: the adapter reads the tool's
  own structured output, and a chat records each message you send.
- **A chat on the protocol door works.** A conversation with `claude-code-acp`, `codex-acp` or a
  plugin's harness used to open and never answer: the person's words went into the protocol's
  stream as raw text. Now it is one session, each message a turn on it, in the order sent. It has
  the same servers and rules a driven session gets, and *finish* closes the session before the
  harness exits. A fresh chat says nothing has been said yet, instead of claiming its door carries
  only text. And a conversation no longer loses a word when the page reads it while the agent is
  talking. A tool card names a file inside the session's tree relative to it. The context a session
  used is recorded for the native door too.
- **A turn can be stopped without ending the conversation.** Ctrl+C during a turn in
  `daoris-driver chat` stops it and keeps the session, on the protocol door and on Claude Code's own.
  A script sends a line holding only ETX to do the same. The turn ends *cancelled* in the record,
  never as a failure. A message you send while a turn runs waits for it on both doors: Claude Code
  used to be handed it mid-turn, and the record put it inside the turn before. A stop hands back what
  was still waiting, unsent, so nothing you queued fires after you said stop.
- **The composer stops a turn too.** While a turn runs, *send* becomes *queue*, what you sent
  behind it waits above the box, and *stop turn* stands beside *finish* and *stop*. A stop puts
  what was waiting back in the box. A stopped turn reads *the turn was stopped here*, and the tool
  call it cut reads *stopped* rather than failed. What you were typing to each session is kept per
  session, across a switch and a reload.
- **A message can carry files.** Drop them on the composer, paste a screenshot, or use the
  paperclip, and they go with your next message, alone if you type nothing. They are kept for that
  conversation beside its transcript, never in the repository, and the agent is allowed to read
  exactly that folder. On Claude Code's own door the message names each file's path, and on the
  protocol door it links each one. The conversation shows what you attached under what you asked.
  From a terminal, a line `:attach <path>` puts a file with the next message.
- **A chat you start stays open in front of you**, where it used to fall back to *Nothing attended*
  until you picked it from the list. A long code line scrolls inside its block, not widening the
  conversation.
- **A session never outlives its process on paper.** Closing the application while a chat ran left
  its record *working* forever, holding its repository, and *stop* could not end it. Now closing ends
  each chat and records it first. *Stop* ends a session nothing on this machine is running any more.
  And the driver's first look after a crash ends what was left behind and says so. A session a
  terminal's driver holds on the same machine is never taken for one of these, and neither is a
  teammate's.
- **A message is sent once.** Pressing *send* in a conversation sent every message twice. And
  cancelling an API key you had typed saved it anyway. Both came from a button that submitted its
  form as well as doing its own job, and a button now submits only when it is meant to.
- **Retiring the last repository empties the index.** A machine whose every repository was retired
  went on charting, searching and comparing their knowledge; a refresh now leaves it empty, while a
  registered repository whose checkout cannot be read still keeps what it had.
- **The menus are the setup domains** (D75). The title bar carries *Daoris · Workspace · Agents ·
  View*, and each setup item opens its own domain. Settings is one page with its domains in a
  list, one shown at a time, as an IDE's settings are. The Workspace menu lists every workspace with
  what it holds and scopes the window, and can import a folder of repositories as `daoris import`
  does. The workspace is named in the title bar and the status bar in every state, *no workspace
  yet* included. And the scope has one name everywhere, *workspace* / 工作区, in the interface and in
  what the CLI, the driver and the service print.
- **An account is made by signing in, and Remove removes it** (D66 §3). *Sign in to another
  account* runs the tool's own sign-in into a fresh account and keeps it only if the sign-in
  finished; the name box that came first is gone. Accounts are listed by who is signed in (the email
  `claude auth status` reports), the tool's own included. **Remove deletes the account, sign-in
  included**, after asking once — it used to keep a signed-in account on disk and on the list.
  From a terminal: `daoris agent login <agent> --new`, and `profile remove` deletes;
  `agent list` shows who is signed in to each account.
- **An account can be an API key** (AGT3, D67 §1). *Add an API key* on an agent that takes one
  (Claude Code today), or `daoris agent key <agent>` with the key on stdin. Daoris keeps it in the
  home's `keys.json`, beside the account and never inside the tool's own directory, and hands it to
  the agent at spawn through `ANTHROPIC_API_KEY`. It is shown back only as its last four
  characters, and reads *unchecked*, because the tool says signed in for any key, a wrong one
  included. Removing the account removes its key. A sign-in stays the tool's, as before.
- **A remote wired while the driver runs syncs on its next pass** (SYNC0d). It used to need a
  restart, although the remotes editor said the loop re-reads the map. A changed key is used, and a
  removed remote stops, the same way.
- **A repository's own code map** (MAP3a). A repository that keeps a `docs/code-map.json`, listing
  its modules and what each depends on, can be opened from its node on the Map. The modules are
  drawn in layers, with what uses a module above it; choose one to see its path, its summary, what
  it depends on and what uses it. Daoris reads the file and never writes it. A file that breaks a
  rule is refused whole, with the sentence naming the break, rather than half drawn.
- **A shared deployment answers with a fed code map** (MAP3b). The desktop's sync feeds a sharing
  repository's map beside its knowledge, at the same commit and by the same ordering rules. The
  deployment judges it whole again before it keeps it. A commit with no map removes the one held, and
  an older checkout cannot bring it back.
- **The devkit writes a repository's code map** (MAP3c). `daoris-devkit map` writes
  `docs/code-map.json` from the project files. C# projects and their references come from each
  `.csproj`, and packages and their dependencies on one another from each `package.json`, with no
  compiler. A summary or a dependency the files cannot say (a service called over HTTP) is written
  once by a person and kept. `daoris-devkit map --check` is the gate a repository declares, and Daoris
  keeps its own map with it.
- **A teammate's code map, on this machine** (MAP3e). A repository whose checkout is on another
  machine used to answer "no map" here even when it keeps one. Each sync now brings its map down as
  the circle's deployment holds it, only when it moved, and the Map view says where it came from:
  the commit, its line and whose key fed it. A commit that keeps no map says so by that commit.
- **What a start runs on** (MAP1b). Settings shows, for each workspace, the agent, account and
  version a driven session would start with, and which setting chose each: this workspace's
  default, this machine's, or the agent's own sign-in; a pin, or PATH. A start the driver would
  hold says why in the driver's own words. It is the driver's own answer, so it cannot disagree
  with what the loop does. Desktop only.
- **How this work ran** (MAP1a). A quest in a chain shows the whole chain in its drawer: the ask it
  came from, the quests before and after it, the steps still to come, and every session that ran
  each one, with its agent, version and account. Press a quest to open it, or a session to attend
  it. The same strip sits under a session in Sessions.
- **A map of the workspace** (MAP2, D67 §3). *Map* on the activity bar draws the circle's
  repositories, the quests that went between them (one arrow per direction, counted, solid while
  any is open), and where two repositories learned the same thing. Choose a repository or a line to
  see what it holds. It reads only what the service already serves, so it works in a browser too.
- **A big workspace's map, in layers** (MAP4a). Past twelve repositories the map lays them out in
  columns by who asks whom instead of a ring, so every name stays readable. A line that skips a
  column threads between the cards it passes, and two repositories that ask each other get two
  lines apart. What nothing connects waits below, by name. Drag or scroll to move about, Ctrl+wheel
  to zoom, and type to find a repository. The size is a menu behind the percentage: zoom in and out,
  the whole map, or a size by number.
- **The map draws what your asks became, and chains step by step** (MAP4b). Your asks are one source
  on the map, with a long-dashed line to each repository they put quests on. A chain draws a dotted
  arrow from the repository whose finished quest published the next step to where that step went,
  including the steps still to come. A *Lines* menu beside the size switches each kind of line on or
  off (quests, asks, chains, shared findings) and remembers your choice. The same menu chooses which
  quests the lines draw: all, open only, or what moved in the last 7 or 30 days (MAP4c). While a
  session is on one of a line's quests, the line's count wears the same dashed ring a repository with
  a session does, in the session's colour, and a repository with several sessions says how many
  (MAP4d).
- **The monitor and a detached session wear your theme on their title bar too** (WINDOW2). A dark
  choice on a light Windows no longer shows a light title bar over a dark page, and the title bar
  follows a change of theme at once.
- **A sign-in survives leaving the Agents page** (SIGNIN1). Signing in to an account and going
  elsewhere while the browser waits no longer loses the code panel or the sentence saying how it
  ended: the panel is on its row when you come back, and the end is said wherever you are.
- **A live chat says it moved when its last turn ended** (RAIL2). Seconds after an answer, a
  conversation's row and head no longer read *moved 4m ago*: the driver says when each turn ended on
  this machine, and the page shows the later of that and the record. Nothing new is recorded or
  synced.
- **A repository can say what it depends on** (MAP4e, D91). `domain.uses` in `daoris.json` names the
  repositories it uses. `connect` sends it, the service keeps it, and the desktop carries it to the
  team's deployment and keeps it when Projects edits the declaration. The map draws it as a line of its
  own, *says it uses*, which the Lines menu switches like the rest.
- **A flatter look.** Corners are one step smaller everywhere (controls, cards, drawers and menus),
  the scrollbar's thumb is squared, and Overview's knowledge bars are thin and square-ended on a
  track, so a short one reads as a share of the longest rather than a blob.
- **Menus look like menus, and scrollbars have no arrows.** The button that lists a region's views
  and moves them is now "⋯", as VS Code's is, rather than a down arrow. Scrollbars throughout are a
  thin bar with no ▲ and ▼ at its ends.
- **An account its provider refused is not spent again** (AGT3b). A session that fails with the
  tool's own "API Error: 401" ends saying which account was refused and what fixes it. Further
  starts on that account are held, instead of each sitting through the tool's minutes of silent
  retries, until you change the account or look again.
- **The protocol door runs as the agent's accounts** (AGT7). Claude Code over the protocol door
  (`claude-code-acp`) used to look for accounts under its own name, so an account made for
  Claude Code, a key account included, never reached it. Codex's never reached `codex-acp`. A door's
  accounts, defaults and keys are now the agent's; only its pinned version stays its own.
- **`daoris harness` is `daoris agent` now** (AGT1). The tools a session runs are agents to a
  person: the verb, its help, the CLI's output and the driver's refusals say so. `daoris harness`
  answers with where it went. Each tool names what it is and whose: *Claude Code* (Anthropic),
  *Codex* (OpenAI), *dsh* (DeepSeek), on its Settings card and in `agent list`.
- **A pinned Claude Code stays the version you pinned.** Claude Code updates itself by default, and
  a pinned copy reported exactly that. Every spawn of a pinned binary now runs with
  `DISABLE_UPDATES=1`: sessions and chats over either door, and the version and sign-in questions.
  A `claude` from `PATH` is left as the machine has it.
- **A repository that keeps a code map is asked to keep it current** (MAP3d). A driven session in a
  repository whose tree holds `docs/code-map.json` (or `code-map.json`) is asked, in its prompt, to
  bring the map up to date in the same change when its work adds, removes, moves or rewires a module:
  with the repository's own tool where it has one, else by hand in the map's format. A repository
  that keeps no map is not asked to start one, and the prompt is otherwise unchanged.
- **`daoris agent pin` installs Claude Code and Codex from their makers' own channels, verified**
  (AGT2b). Claude Code's release manifest must carry Anthropic's signature before its SHA-256 is
  trusted for the binary; a version before 2.1.89, which has none, is refused. Codex's package must
  match both of its published hashes. Nothing is pinned unless everything verified. The Machine
  view's pin does the same for Claude Code. npm still installs the ACP adapters and dsh.
- **Update on an agent does what it says** (USE1a). On a pinned agent, Update (on the Agents page, or
  `daoris agent update <agent>`) finds the newest release as one exact version, installs it the way
  a pin does, and pins it, saying `0.79.0 → 0.84.0`, or that the pin is already the newest and
  nothing was fetched. npm answers for a package, and the maker's channel for Claude Code and Codex.
  Unpinned, the agent's own updater runs, as before. An agent with neither offers no Update button,
  and the button's tip says which of the two it does.
- **An account's model and effort, from Daoris** (AGT6a, D98). Each Claude Code account on Settings →
  Agents & accounts says the model and effort its own settings file holds, and *Model & effort* changes
  them: the tool's own aliases or a full model id, the four efforts its settings keep, and an effort per
  model where the file sets one. `daoris agent settings <agent> [--account <name>] [model <v>] [effort
  <v> [--for <model>]]` is the same from a terminal, and prints the values given none. Only those keys
  move; every other key the tool keeps there stays. The tool's own configuration home is never touched,
  and a tool whose settings Daoris does not know (Codex, dsh) is offered nothing, and says so.
- **The machine log, read back at two doors, and a report to improve Daoris from** (LOG1c, LOG1d,
  D94). Every Daoris process writes what happens to it under the home's `logs/`, without anyone's
  words. `daoris-driver logs [--since 30m|2h|3d] [--source <name>] [--event <name>] [--level
  warn|error] [--json]` prints every source's lines merged by time, and Settings → Logs on the desktop
  shows the newest with the same filters, a count per level, and *Open the folder*; a line that cannot
  be read is skipped and counted. `node tools/usage-report.mjs --install <dir> [--days 7]` summarises
  an install's log for a development session: what was used most, how long conversations took to open
  and answer, how turns ended, what was refused and what failed. Nothing is sent anywhere.
- **The desktop stops its service host cleanly, and the host's log says so** (LOG2a). The desktop
  used to kill the host it started, so the host never wrote its own stop. It now closes the host's
  standard input, which the host it started takes as its stop, and kills it only if it is still running
  five seconds later. A host started from a terminal is unchanged, and a host the desktop found already
  running is still left running.
- **Daoris's browser says where its own failures happen** (LOG2b). A failure making its first window,
  or watching for the desktop that started it, is a line in the machine log naming that place, rather
  than an exception the runtime reported later as *an unobserved task*.
- **One conversation's model and effort, beside its composer** (AGT6b, D98). A conversation on the
  protocol door offers the model and the effort its agent offered when the session opened, in the
  agent's own words, and changing one changes it for that conversation, as the tool's own `/model`
  would. The record notes that the person made the change. The session's mode is never offered, and
  the driver refuses it. A door that offers none shows nothing.
- **The desktop finds an agent npm installed globally** (USE1f). On Windows such an agent is a `.cmd`
  on PATH, which `daoris agent list` found and the desktop reported as not installed, holding every
  start on it. The desktop now asks PATH for what Windows can start, as the terminal does, and a
  session over a `.cmd` whose prompt the command shell would reinterpret is refused in a sentence
  naming what to do instead.
- **Ask at a workspace, not a repository** (D65 §1a). `daoris-driver ask [--workspace <name>]
  [--to <repo>] [--file <path>]… [--url <address>]… "…"` records an ask: the sentence, its links
  and files, who asked, and what became of it, naming the tier that answered on every record. With
  no intake harness, the **declarations tier** ranks the workspace's repositories by the words their
  summary, `owns` and `accepts` share with the sentence, proposes with that evidence, and publishes
  nothing. `--to` publishes at once, asked by `ask #<id>` in the ask's circle, carrying its links
  and files. A refused `--to` keeps the ask with its proposal. `ask --publish <id> --to <repo>` and
  `ask --close <id> --reason "…"` finish it. The same words in the same circle are the same ask.
- **Ask from the screen, too** (INT4c, D50). Quests leads with *Ask*, and the palette offers *Ask the
  circle…*. The ask is made in the scoped circle, or the only one; with several and none chosen, the
  composer asks which. It carries links and files exactly as the quest composer does, because both
  now share one set of fields. Each ask's record says which tier answered and what it proposed. It
  publishes to a proposal or to any other adopter in the circle, opens the quests the ask became,
  names its files without saying where they are kept, and closes with a reason. A browser on this
  machine has the same door.
- **An ask can be answered by an intake session** (INT4b, D65 §1b). Name a harness with `daoris
  driver intake <adapter>`, and an ask the declarations did not settle opens a session in the
  circle's room under the home. The room is seeded with who owns and accepts what. The session
  publishes the quests itself, chains included, asked by `ask #<id>` in its own words with the
  asker's beneath. The ask names its intake, and its tier reads `intake`. Where the declarations do
  not settle it, the intake publishes nothing and the session waits for you; publishing or closing
  the ask ends it. The intake is off until a harness is named, because each intake spends a login,
  and `daoris driver intake off` goes back to declarations only.
- **An ask that waits on you is in *What needs you*** (INT4d). A proposal, or an ask whose intake
  parked asking, is a row of Overview's band that opens the ask's record. A parked intake is counted
  once, as its ask. The band stays live on the desktop: an ask made by the other door arrives with
  the next tick, without a reload. The record names who answered: the `intake` tier in words, and
  the intake session, which is a door into Sessions on the desktop. A band row with nowhere to go (a
  parked session, in a browser) is no longer a button.
- **An ask whose work is finished leaves the list** (USE1c). An ask that became quests, every one of
  which has closed (chain steps included), is **done**. The Asks list hides it as it hides a closed
  one, and *include closed* shows it with a *done* pill, after every live ask. Done is worked out from
  the quests each time the ask is read, so a quest closed on another machine counts as soon as it
  syncs in. The same words asked again after an ask is done make a new ask.
- **Delete a quest or an ask made by mistake** (QUEST1, D95). A duplicate or a test can go. Use the
  quest drawer's *Delete* or the ask record's, each asking once, or run `daoris-driver quest delete
  <id>` or `daoris-driver ask --delete <id>`. Only a quest nobody has started on goes: it is open, no
  session was started for it, and no taken quest waits on it. Anything else stays, and the refusal says
  to decline it or leave it closed. An ask goes with every quest it became, or not at all. A shared
  quest's delete travels to the remote as an operation, so no later sync brings it back. It is pushed
  before the answer returns, and if another machine's take got there first, the quest stays, taken.
- **A repository registered without adopting can be asked, and driven over the protocol door**
  (INT3, D70). Registered is addressable; adopted is disciplined. A repository registered on this
  machine with a root takes quests, and a session the driver starts in it on a protocol agent is
  handed its connector with nothing written into the tree. A direct agent holds the quest and says
  which door could carry it, and the publish tells the asker so. Every place that offers a receiver
  offers exactly who can be asked. Adding a folder with no manifest from Projects now registers it as
  not adopted.
- **What an agent Daoris starts may do is yours to set** (PERM1, D72). It uses Claude Code's own
  permission rules, in three scopes Daoris keeps (this machine, a circle, a repository), in
  `permissions.json` under the home. Daoris's defaults allow its connector's quest tools and deny a
  `git push`. Every session Daoris starts on Claude Code is handed them at spawn, on either door, as
  one more settings scope, so the agent's own order decides and a repository's deny still wins. They
  reach a session even in a folder the agent has never trusted, where a repository's own allow-list
  does not. Set them with `daoris agent rules`, or on Settings → *What agents may do*.
- **A session is told it may propose the rule it was refused** (PERM2b, D74). The quest target now
  names `permission_propose`. A session refused a command its work needs proposes the narrowest rule
  with its reason, then finishes or declines, since a widening waits for you. Proven on a real
  session, which proposed a rule for its one repository only.
- **An agent can propose a change to what agents may do** (PERM2, D74), through the connector's new
  `permission_propose` tool. A narrowing (a deny or ask added, an allow removed) applies at the
  driver's next tick. A widening waits for your yes, from `daoris agent rules proposals | accept <id>
  | decline <id>` or Settings → *What agents may do* → *Proposed by agents*, and it waits in *What
  needs you* meanwhile. Every proposal records the session that made it, its reason, and who settled
  it.
- **A session can read the files its quest or ask carries** (INT4j, D72). The files are kept under
  the Daoris home, outside the session's folder, and reading them was refused. Every session Daoris
  starts on Claude Code is now handed a read of exactly its own quest's or ask's kept folder, and
  nothing else under the home.
- **A real intake is proven end to end** (INT4f). A real Claude Code intake read a circle's
  declarations, chose the repository, and published the quest as the ask, carrying its link and file,
  in a room nobody had trusted.
- **A driven session may commit, by default** (PERM4, D72). Daoris's rules now allow `cd`, `git add`
  and `git commit`, so a session in a folder the agent never trusted can land its work instead of
  declining. A push is still refused. Switch it off with `daoris agent rules default commit off` or
  on Settings → *What agents may do*.
- **A session writes files only inside its own tree** (PERM3, D72). Every Claude Code session Daoris
  starts is handed a hook that refuses an edit or a write whose path, followed through links, is
  outside the session's tree, and says why: a change needed elsewhere is a quest. It is a default,
  `tree-guard`, switched like the others, and proven on a real session on both doors. A shell
  command's writes are not its call; the agent's own working-directory boundary stands there.
- **Trusting a folder for the agent is asked, then written** (DEPLOY1, D73). `daoris agent trust
  <agent> <folder> --yes` grants what Claude Code asks the first time it runs in a folder, in the
  agent's own file and nothing else; without `--yes` it only asks. On the desktop, a start held for
  trust shows in *What needs you* and in its quest's drawer, and *trust this folder…* grants exactly
  that hold. Since Daoris's own rules reach an untrusted session, the driver holds for trust only when
  those rules would not let the session reach its connector.
- **Claude Code over the protocol door is proven on a real login** (ACP2). A real quest reached a
  real commit, and the session closed its own quest through its connector, in a folder nobody had
  trusted. Getting there fixed a defect no gate could see: a real tool call carries its content as a
  list, and reading it as an object ended the driver's reader at the first tool call, reported as
  "the stream ended".
- **An unadopted repository can be opted into driving from Projects** (INT3c). One registered here
  with a root carries the same driving row as an adopter: drive, hold, own tree per session. On a
  machine that drives on a direct agent, the row says a quest there will sit until it drives on a
  protocol one. The group's explanation now says only what is proven about a real agent there.
- **A driven session takes no messages either** (INT4i). A line sent to a session the driver started
  on a quest landed, on the protocol door, in the driver's own JSON-RPC stream, and on the pipe door
  came back as "it ended". The driver now refuses it in its own words, naming the quest and saying
  that stopping is the one move that reaches it. Conversations are unchanged.
- **A running intake takes no messages** (INT4h). Sessions gave it a message box, and a typed line
  either went nowhere or, on the protocol door, landed in the middle of the driver's own JSON-RPC
  stream. It has no box now: its head says why, opens the ask it serves, and carries its stop. The
  driver refuses a line or a finish for an intake in its own words rather than answering "it ended".
- **A parked intake in Sessions leads to its ask** (INT4g). It offers *answer ask #id*, which opens
  the ask's record where it is published or closed, and a stop that says the ask then stays a
  proposal, instead of a parked session's finish, decline and stop, none of which answered the ask.
  It has no message box, and Sessions names it as an intake for its ask rather than a conversation.
- **Settings says what Daoris's own AI is** (AGT6): search and convergence with the service's tier,
  verbatim, and the variables that choose its model; on the desktop, the intake's agent — off, or a
  way in this machine has — and the account an intake in each circle runs as. What a start runs on
  lists the intake as a second job, and the status bar's tier leads there.
- **Ask Daoris: a conversation about Daoris itself, which proposes and you apply** (HELP1, D89). Its
  own agent under Settings → *Daoris's own AI*, off until named; with none, it lists what the machine
  lacks, each with the screen that fixes it and the command that does the same. With one, it talks in
  a room of its own, told what the machine holds and where you are on the screen, reading the family
  and nothing else, and it runs in the agent's own asking mode, the one exception to D81. A change it
  wants is a card saying what it changes and the command that does the same, checked by the driver
  first; nothing changes until you press **apply**. It is a tab of the right side bar on every view,
  opened by the side bar's toggle, `F1` or `Ctrl+Alt+I`.
- **Get started: the setup a machine needs, in order** (SETUP1a, D97). The first domain in Settings
  lists six steps, an agent signed in, Daoris's own agent, a repository registered, what is driven,
  how work lands and (optionally) what agents may do, each saying whether it is done, with a button to
  the screen that does it and the command that does the same, copyable. It reads the same facts Ask
  Daoris's starters do, so the two never disagree, and the starters end with a way to it. The Daoris
  menu's *Set up Daoris* and the palette open it; a browser sees the one step it can know. **A machine
  missing an agent, Daoris's own agent or a repository opens on it at start** (SETUP1b), once, never
  pulling you from where you went, unless you tick *Don't open at start*; the status bar says
  *setup: n of 5* until the required steps are done. *Set up with Ask Daoris* opens the side bar on a
  first message asking to be walked through what is left. A question handed to Ask Daoris, from the
  palette too, is no longer asked again when its tab or Quick Ask's box is drawn again.
- **Ask Daoris reaches every door** (HELP6). Beside settings and asks it now proposes an agent's
  **update**, or a **pin** to one exact version, where the Agents screen offers them; an account's own
  **model and effort**, in the tool's own words, never `max`; the **delete** of a quest or an ask made by
  mistake, only where nothing stands on it, the card saying what goes; and **a screen to open**: any
  view, Settings domain, card or setup step, a *go there* card that changes nothing. Each is checked
  first by the rules of the screen that makes the same change, and applied through that screen's own
  route when you press it. An update applied there runs as the Agents screen's own, its end said in the
  conversation. Its room lists the asks by id and every place it may take you.
- **Ask Daoris makes a plugin by asking for it, and installs one by your press** (PLUG9). A plugin runs
  as you, so it never writes one: it proposes an ask at the workspace of the repository that holds your
  plugins, saying what the plugin should do and the point it speaks on, and the session there makes it
  with its tests; with no such repository, where plugins live is yours to decide. Once it has landed, a
  **plugin** card adds it from its folder in that checkout, or switches one installed here on or off.
  The card shows what will run before you press **apply**: the plugin's id, the command it starts as
  its manifest writes it, the points it speaks on, and the agents and servers it declares. Adding
  copies the folder into Daoris's home under its id, as `daoris plugin add` does, and never replaces an
  installed plugin; nothing it runs starts at the press.
- **A plugin remembers where it came from, and takes a newer copy from there** (PLUG9 c, D103). Every
  add (`daoris plugin add <folder>`, an Ask Daoris card, **Install** beside one of Daoris's own) records
  the folder or the offer it came from, and its row in Settings → Plugins says which. **Update…** on the
  row, or `daoris plugin update <id>`, re-reads that source as the catalogue reads a plugin and says what
  changes (its version, command, points, agents and servers) before anything happens; **Update now**, or
  `--yes` at a terminal, replaces its folder whole and keeps what the plugin kept. A source that is gone,
  unsound, another plugin, or one this build refuses is refused in the same words at both doors. A plugin
  added before, or copied in by hand, has no record, and says so. Ask Daoris can propose an update too.
- **The install offers Daoris's own plugins** (PLUG9 d, D103). The published application carries
  `github-pull-request`, `azure-devops-pull-request` and `in-app-browser` in `app/plugin-offers/`, none
  installed. Settings → Plugins lists them under *Daoris's own plugins*, each with what it speaks on and
  what it needs in its README's words (`gh auth login`; `az login` and the devops extension), and
  **Install** copies one in, as `daoris plugin add --offer <id>` does. `daoris plugin list` shows them,
  and Ask Daoris names them and may propose installing one by its id. Installing one runs nothing: the
  driver starts it later, and a plugin that lands work only where a rule names it.
- **The layout toggles are on the strip** (DOCK1c, SURF11). The panel and the right side bar each
  have a toggle beside the window controls on every view, and the session list on Sessions, pressed
  while shown, and an item in the View menu with VS Code's keys: `Ctrl+B`, `Ctrl+J`, `Ctrl+Alt+B`.
  The side bar's tabs shrink as a browser's do, the selected one whole.
- **Views move between the right side bar and the panel** (DOCK1b), as VS Code's do. Ask Daoris, the
  console, the timeline and the review each carry *Move to* on their region's tab list, the button at
  the end of its tab row, and on a right-click of their tab; a moved view is shown where it went, and
  a region left empty closes. Where each stands is remembered for this viewer, and *Reset view
  locations*, in the tab list and the View menu, puts them back. The tab list names every tab in the
  region by its whole name, so a tab cut down to its icon is never the only way to it. The two
  regions are named *the side bar* and *the panel* everywhere now; the panel was called *the console*
  and the side bar *the panel*. **Or drag a tab to the other region** (DOCK1e): the region lights up
  while it is over, and a region with nothing in it appears for the drag, so there is always
  somewhere to drop.
- **Quick Ask** (DOCK1d), as VS Code's Quick Chat: Ask Daoris's conversation in a box where the
  palette opens, for one question without opening the side bar. `Ctrl+Shift+Alt+L` opens it, and so
  does the palette's *Quick Ask*; and whatever you type in the palette can be asked from its last row,
  *Ask Daoris: "…"*, which sends it straight in. *Open in the side bar* carries the same conversation
  on there. The palette's *Ask Daoris* now opens the side bar on it on Sessions, as `F1` does.
- **A session's head says how it stands** (SESS2). A finished session opens at its head rather than
  its last line. Under the title, the head now says how it ended or why it failed, in the record's own
  words; then what its tree left, with **review** beside work none of your branches holds; then one
  quiet line of reference, the tree's path on hover. How the work ran is one line of stops, whole on
  a press, instead of a list that pushed the conversation halfway down the window.
- **The frame is on every view** (DOCK1a). Overview, Quests, Projects and the rest keep the right side
  bar and the panel that Sessions has, with the same tabs, moves, drags and toggles, as VS Code's
  workbench is one frame whatever its editor shows. They hold Ask Daoris and the attended session's
  timeline, review and console, each saying which session it is with a way back to it. What is open
  and selected stays as you move between views. Ask Daoris no longer has a panel of its own at the
  edge: it is a tab of the side bar everywhere.
- **Tell a working session something** (SESS3, D90). A driven session running on the protocol door
  now has a message box. What you send waits for its current turn to end and is then its next prompt,
  in the same session with everything it already knows; *send now* stops the turn so it goes at once.
  Your words are kept in its record as yours. A session on a door that could not hear them offers no
  box.
- **A session says who it worked with** (SESS1). Its head, under the chain, names the session that
  asked for its quest, the answer it carried on after, and what it asked of other repositories, each
  with how it stands and its answer; every one is a door, and so are the chain's quests now. A quest
  records the session that published it (`publishedBy`), from the name the driver hands every
  session's connector, so none of this is guessed from the times.
- **Ask Daoris knows the window** (HELP2): its room says how the window is laid out, how a view moves
  and the layout keys, and on Sessions it is told where the views stand, so a question about the
  screen is answered rather than guessed.
- **A parked session is answered where you read its question, and your answer shows as yours.** A
  driven session parked to ask you gets a box at its foot, since its old door was a button at the top
  of a long record; your answer is kept in its record beneath the question and opens the session that
  carries on. Overview's *what needs you* rows say where each goes (*open it to answer*) and keep an
  analysis to two lines.
- **A branch a landing made goes once its pull request's work is on the line** (WSR5, D102). A squash
  merge puts none of the branch's commits on the line, so git calls it unmerged and the session-branch
  clean-up never looked at it. A landing now records the branch it makes, under the Daoris home, and
  Settings → Workspace → *Session branches* and `daoris-driver trees clean` list those branches in a
  group of their own. The same press removes each whose changed files all read on the line (or on
  `origin/<line>`) as it left them, and each inside another that does. One checked out anywhere, one with
  commits its remote lacks, one a kept session branch still needs, and one whose files differ are kept,
  with the files named. Only branches a landing made here are judged; landings from before the record,
  and your own branches, are never touched.
- **A landed branch can be handed to its plugin afterwards** (WSR5, D102). A ticket landed before its
  workspace named a plugin, or one whose plugin failed, had no door but a hand push: pressing Accept again
  is refused while the branch stands. The review of the session that landed it now offers *hand it to
  <plugin>* (its tree may be gone), `daoris-driver trees hand <session|branch>` does the same from a
  terminal (`--plugin <id>` where the rule names none, `--plan` to see it first), and Ask Daoris can
  propose it as a card. The plugin is told the same frame a landing tells it, for the branch as it stands,
  and its answer is said and kept the same way. A branch no landing made, one moved away from what the
  landing made, one whose work is already on the line, and one already on its remote at this commit with a
  pull request are refused, and a hand-off whose plugin does not push changes nothing.
- **`daoris-driver` drives only when asked to, and one loop drives a home** (DRV8, D104). Run with no
  verb to read its usage, it used to start a headless loop beside the desktop, and that loop took a
  quest first. With no verb, or a word it does not know, it now prints its usage and exits 2. The loop
  is `daoris-driver drive [--once | --until-idle]` (a bare `--once` or `--until-idle` still works). A
  loop takes the home's `driver.lock` first, and a home another live driver holds is refused, naming
  it: the desktop or a headless loop, its process id, and since when. `--share` runs beside it on
  purpose. A lock whose process is gone never blocks. The desktop's loop takes the same lock, and where
  a headless loop holds it, says so once and starts when that one stops.
- **A take a shutdown or the orphan sweep cut off is carried on** (DRV8, D104). Both ended the session's
  record `stopped`, and only a failed or answered take was carried on, so the quest sat taken with
  nothing to move it. Such a stop is now recorded as interrupted, a field of its own on the record, and
  its take is carried on in its tree like a cut-off, counting as a strike, the third parking it. The
  person's own stop is theirs, and is never carried on.
- **Agents read the workspace's other checkouts, and write only where you declare it** (READ1, D107).
  Reading across repositories is on by default: a driven session, and a conversation in a repository,
  may read another registered checkout in its workspace (its files, `git status`, the branch list) and
  change nothing there; Ask Daoris may read every workspace's, still with no shell. Reading can be
  switched off per repository or per workspace, and writing into another repository is allowed only
  toward one you declare (`daoris driver across plugins write-to engine`), in one direction. Both doors:
  `daoris driver across …` and Settings → Permissions → *Reading and writing across repositories*.
- **A pinned Daoris is one taskbar button** (TASKBAR1, D108). An install's windows name one taskbar id and
  relaunch `Daoris.exe` at the install's root, so a pin made from the running window survives a republish and
  later windows, a second one included, join it. Pin from the running button; a pin made on `Daoris.exe` in
  Explorer carries no id and still shows a second button.
- **Ask Daoris can set what was terminal-only** (HELP9, D110). It proposes reading and writing across
  repositories, a repository's cap and its adapter, each shown as the change it would make before you
  apply it; a write-to says that applying it is your standing say-so. Every driver verb and Settings
  control is now held, by a test, to a door Ask Daoris can propose, a door still owed, or a written
  reason it has none.
- **Read a file without leaving the window** (PREVIEW1, D111). A path in a conversation's tool card,
  or the file button in the review's list, opens the file read-only as a tab of the right side bar:
  numbered, highlighted lines, the lines a read named marked and scrolled to, and a *File · Changes*
  switch where the review holds a patch. It reads the session's own tree as it is now, up to 256 KiB, and
  refuses a path outside that tree, a link leading out of it, and `.git`, each with its own sentence.
- **One press brings a repository up to date after its pull request merges** (WSR6, D109). Settings →
  Workspace → Session branches → *Bring up to date*, or `daoris-driver trees sync [--repository <name>]
  [--yes]`, lists first and then: fetches the line and fast-forwards it only (a clean checkout on it, or a
  ref nothing has checked out; never a merge commit, a force or a push); replays the session branches not
  in use and the landed branches not yet on the line onto it, cut where each grew from, so a squash-merged
  parent's commits drop, in a tree of Daoris's own, kept only once the branch's own work is proven intact;
  and deletes the landed branches whose work reached the line. A conflict aborts and is named; a pushed
  branch is left.
- **The Daoris home's hint tells the truth when an install overrode your account's folder** (LEFT1). It
  says a terminal's `daoris` reads the same folder only when it does, and otherwise points at the
  notice. `INSTALLED.md` now says how to pin Daoris to the taskbar: from the running window.
- **Every name, designed in both languages** (NAME1, D116). Names on every screen follow one glossary: one term per concept in English and Chinese, chosen as names rather than translated, with rules for each kind of element. Projects is now **Repositories** (仓库), an ask is **需求**, Settings' sections are Setup, Appearance, AI features, Workspace, Driver, Agents, Permissions, Plugins, Browser and Machine log, and every button says what it does. A names check in the build keeps them from drifting.
- **A new quest no longer waits for a running session** (DEV3). A quest published while a long session runs starts at the next look beside it, within the cap, at once when published on the window; controls are heard while sessions run, and each ending is reported as it happens.
- **A repository can use a layout every agent reads** (LAYOUT3, D117). `daoris init --harness agents`, or flipping `harness` to `agents` and running `sync`, keeps knowledge and skills under `.agents/`, the always-loaded rules in `AGENTS.md`, and gives Claude Code a `CLAUDE.md` import and a skills mirror rather than links a Windows checkout would hold as text. The move refuses to leave your own documents behind, naming each `git mv`; `check` catches an edited mirror, and `upstream` takes it.
- **A quest can ask one lane of a repository** (DEV4, D115). A repository that declares its lanes in `daoris.lanes.json` sends their names on `connect`, and a quest addressed `repository:lane` is judged against them; a repository may ask its own lane. The Quests card and drawer show a quest's lanes.
- **Ask Daoris proposes what was still owed** (HELP10, under D110). Retrying a parked quest, making an
  account an agent's default, Daoris's browser and its favorites, a go to Permissions → *Reading and
  writing across*, and *Bring up to date*, whose card keeps the screen's two presses: the first looks
  (fetching, as you), the second acts on the rows it listed. The room now says a file opens in a preview.
- **An edit's file opens at its change** (LEFT2). A tool call's location line reaches the preview, so an
  edit's path opens scrolled to its first hunk; a whole-file read marks nothing. Opening a preview is a
  line in the machine log. The Daoris home's hint also knows a home named for one start alone, and a
  session can no longer start in a tree while *Bring up to date* replays it.
- **A dock tab shows its whole name or its icon** (TABS1). In a narrow side bar or panel, the tabs you are
  not looking at become icons, named in their tips, instead of names cut to one character.
- **Bring up to date works on a real workspace** (WSR7, D112). It takes the repositories holding Daoris's
  branches by default; every other checkout is listed apart, and you include it by a tick, `--all` or by
  name. It fetches four at a time, the window waits as long as the host may and says what it is looking at,
  and what could not be fetched is said once, first: how many, when each last heard from origin, and what
  your git needs to reach it. A failed fetch no longer empties your `FETCH_HEAD`.
- **A landed session reads as landed** (REVIEW2, D113). Once a landing tidied its tree, the review shows the
  changes from the landed branch in the repository's own checkout, says where and when it landed (and the
  pull request, if a plugin opened one), and offers no Accept or Send back; the hand-off where one applies.
  Its file preview reads from that branch, saying so. `trees land` says a session already landed.
- **Removing a plugin asks first** (PLUG10). *Remove…* in Settings → Plugins asks once, naming the data folder that stays, and a running plugin no longer wears done's green.
- **A push in any spelling stays yours** (UNBLOCK4). A session cannot push with options before the subcommand (`git -C . push`, `-c`, `--git-dir`, `--no-pager`); while the carve-out is on, auto mode is told that any push, publish or release is the person's. `git mv` joins what a commit may do.
- **Permission asks are counted** (UNBLOCK5). Every refused permission is a line in the machine log, and the usage report shows asks per session, the baseline the declared safe work (UNBLOCK2–3) will be measured against.
- **Which git, node, pwsh, gh and az Daoris runs is yours to set** (TOOLS2, TOOLS3; D121). `daoris tool list|path|use` says, for each tool, whether it is the system's or a file you name, and the install carries a resource list of the makers' own downloads with their published hashes (MinGit, Node.js, PowerShell, GitHub CLI, Azure CLI). Downloading a managed version and starting it come next.
- **Every list is one list pane** (FRAME1b, D118). Sessions' list becomes a strip only when the conversation would fall below 400 px, not below a fixed width; a strip opens the list over the conversation; ↑, ↓, Home and End move through any list, and pressing the current place on the activity bar shows or hides it.
- **Every view is laid out the same way** (FRAME1c, D118). Each view hands the frame its list, where it has one, and its main area; each remembers its own list's width, closing, chosen item and filters; and one opener carries every door, so a link to a quest, a plugin or a Settings domain lands on that item whichever screen it came from.
- **Plugins have a view of their own** (PLUGUI1b, D119). *Plugins* on the activity bar lists what is installed, grouped by what each does, with its own page: what it declares, whether it is running, its trial's report, an update's plan, and *Remove…*; Daoris's own offers have a page before they are installed. A browser over a remote is offered neither the view nor its doors.
- **A plugin's health and activity are kept, and the desktop answers the Plugins view** (PLUGUI1d, PLUGUI1e, D119). The machine log records seven plugin events (names, counts and times, never a message or a pull request), the driver loop keeps each plugin's health, and `daoris-driver plugins show <id>` and `plugins activity <id>` say both from a terminal. The window asks the desktop for a plugin's page, its activity, what a folder's plugin would run, an install from a folder (never over an installed one), and to open its folder.
- **Quests and Repositories are on the frame** (FRAME1d, FRAME1e, D118). Quests lists the asks, then the quests by state, with a receiver filter and *Include closed* in its ⋯ and remembered; a quest or an ask opens as a page with its acts in the header, and the composers stay drawers. Repositories lists the adopted, then *Registered, not adopted*, each with its standing on this machine; a repository's page carries *Open code map* and *Manage*.
- **A long list or menu stays inside the window** (SELECT1, MENU1). A select's list and every dropdown menu are capped at the room the window has and scroll inside it; every menu is one shared atom now, and a test holds that nothing builds its own.
- **A parked session's card shows its whole question** (PARK1). A driven session that stops to ask you is quoted from its saved conversation, lists and all, where the console's last lines had cut a question off at its first indented line.
- **A turn's meter says cached reads apart** (METER1). Under a finished turn, *in* is what the agent read anew and *cached* what it re-read from its cache, so a long turn no longer shows tens of millions of tokens "in" when it read a few hundred thousand.
- **A decision kept as its own file is listed by its heading** (DOC8c, D134). A record in a declared decisions, fixes or archive folder is titled by its first heading, or by its file name when it has none; a router folder keeps its file names.
- **A terminal command breaks only between its words** (LOOK5). Every code span on the window wraps at its spaces and never inside a flag, so `--no-keep` no longer splits as `--no-` / `keep` on a narrow settings card; the machine log's terminal command renders as code too.
- **Words written to a session that has stopped go on in the same conversation** (MSG1b, MSG1d, D137). Write to a parked or ended driven session and the next look resumes the agent's own conversation with your words, in the same row, on both doors; words written while a session winds up wait for it rather than being refused. Where a session can never take words (a teammate's, an intake, one stood down, an earlier session of a quest), the box says why and nothing is sent.
- **The install updates when its work allows** (UPDATE1, D139). `publish:desktop --stage` puts a checked build beside the running install; the desktop starts nothing new, installs it once no session's turn is running, and starts again, saying so once. A failed swap puts the old build back. *Update now*, *Not now* and `daoris-driver update` choose when. The first install carrying this is a publish in place.
- **Right-click opens Daoris's own menu** (CTX1, D138). A session, a quest, an ask, a repository, a plugin or a document offers the acts its ⋯ already has; selected words offer copy, *Search Daoris for it* and *Ask Daoris about it*; a link offers open and copy. Text fields and the in-app browser keep the engine's menu, and the title bar opens the window's menu.
- **A chat that took a quest keeps that work's record** (CHATTAKE1). Its record is marked as having taken a quest, so *Delete…* is no longer offered for it.
- **Every session takes words, and the box says how they reach it** (MSG1f, MSG1c, MSG1e, D137). A running session hears them at its next step or its turn's end; a parked or ended one, a chat among them, goes on in its own conversation; where a session can never take words, a line says why. `daoris-driver sessions say <id> "…"` is the terminal's door, and words said while no Daoris ran are taken up at the next start.
- **What Daoris says is in your language** (LANG1a, LANG1b, D142). Daoris's lines on a session's record are worded by the window in English or 中文; an agent's or your own words are shown as written; a record from before says *shown as recorded*.
- **A session language for the work** (LANG1c, D142). Set per repository or workspace (`daoris driver language`, a repository's page, Settings → Workspace, or Ask Daoris), it asks driven sessions to write their questions and closing notes to you in that language; the window's own language stays yours, and chats answer in whatever language you write.
- **A page's content takes its pane's width** (LAYOUT11, D141). A quest's body, a plugin's detail and every other block wrap at the pane's edge rather than a fixed column, which in 中文 had held half the words.
- **The plugins page is a catalogue, and each plugin has its icon** (PLUGUI2, D140). Installed, Daoris's own and available plugins, each with an icon its manifest names or a generated monogram; boxes that ask once draw on their own sunken surface (LOOK6).
- **Settings says which account the next start takes, and why** (TOOL6e). Each workspace's accounts name the next pick and the step that chose it, what holds each other account, and when a cool-off ended.
- **A park's go-aheads are answered on its page, in one press** (KNOWUSE1a2, D135). Approving or refusing one there also answers the park, and the same session goes on.
- **A quest's page shows what you required and takes your yes** (DRIFT1d2, D133). Each requirement with the words it quotes and how the done answered it; a held departure takes *Accept the departure*, and Ask Daoris can propose it.
- **`daoris-driver trace <commit|session|quest>` reads a change back to its ask** (TRACE1, D143), and **a session's page shows what it was handed**, section by section, with what each bound left out (CONTEXT1). Neither changes anything.
- **Settings → Driver shows the install's update** (UPDATE1b): what is staged, the drain, the last swap and the three words.
- **`daoris agent list` says the next start beneath each list** (TOOL6f): the walk's steps, each account cooling and until when, and where the screen names the account the next start takes.
- **Settings → Driver keeps the last update after the banner is dismissed** (UPDATE1d), and Ask Daoris names the update's doors without proposing them (UPDATE1f).
- **An answered park reads as going on** on the map and in Ask Daoris's waiting count (ANSWER1e), and a refused delete of a chat that took a quest says so instead of `#` (CHATTAKE1c).
- **The title bar behaves as a window's does** (FRAME2): a double-click maximizes and restores, a click on a maximized window leaves it maximized, and only a drag restores and moves it.
- **A session that goes on with your words gets the files you sent with them** (MSG1d3), **words you send as a session ends survive a restart** (MSG1d4), and the line saying a stopped turn may have read what you added is in your language (MSG1c3).
- **The review opens in seconds, and says what it is doing while it loads** (REVIEW3, REVIEW4). It reads a session's changes in one git process instead of one per file (a 61-file review went from about a minute to a couple of seconds), shows its frame and a skeleton at once with what it is reading and for how long, waits long enough instead of timing out at thirty seconds, keeps a finished session's review for a second open, and words each refusal with what to do next. A renamed file now shows its rename, and a path outside ASCII its changes.
- **Words to a session go on with the account it ran on** (MSG1g). If that account is cooling, the words wait for it, and *Go on in a new session* (`daoris-driver sessions go-on-new`) starts over on another account instead, without the conversation's context.
- **Done work can land itself** (LAND2a, LAND2b, D145). A branch rule may *Accept automatically* (自动采纳; `--auto-accept` at the terminal, or through Ask Daoris). Each door says that the rule's plugin then pushes and opens a pull request without asking, or warns that with no plugin nothing leaves the machine. A merge rule refuses it. Under the rule, a session whose quest is done lands at the next look exactly as the review's *Accept* would. A plugin that cannot land leaves the branch and names the hand-off. The landing records who accepted it, `person` or `auto`.
- **A landing tidies every session branch its work holds** (LAND3). It removes each session branch the landed work contains, keeping and naming one that a running session holds, that has uncommitted work, or that is checked out elsewhere. `daoris-driver trees remove <session|branch> --force` removes a failed attempt's branch, and `trees clean` offers it.
- **`daoris-driver git branches` lists what each repository holds** (GIT1a, GIT1c, D147): its line and branches by kind (the line, session branches, landed branches with their push standing, your own, and origin's), each with the git command that read it.
- **Words a cooling account holds say so** (MSG1g2, MSG1f2). The page says the words wait for the account's reset and offers *Go on in a new session*. *Start a conversation with these words* is one act that moves the words off the old record. The session list says *going on* for a record whose run is planned, and *Resumes later* with what holds its words.
- **How this came to be** (TRACE1b): a session's page and a quest's page fold out the ask, quest, sessions, the rules each was handed and where its work landed. This is the same reading as `daoris-driver trace`, and a link it could not read is marked missing.
- **One piece of work, one branch and one pull request** (LAND2c, D149). When a chain's later step finishes, its work moves the chain's landed branch on by fast-forward instead of making a second branch, and a follow-up ask's quest that *follows* the chain joins it. The pull-request plugins (1.1.0) push to the pull request that is still open and never open a second one. A branch whose pull request already merged is refused, and nothing moves.
- **An ask's page shows what a pause or an abandon reaches** (PAUSE1h): each of its quests with its state and why it sits, the questions its sessions asked other repositories, and those sessions as doors into Sessions.
- **Daoris no longer signs your accounts out, and says when one is** (TOOL6g). It asks an account's status one question at a time (shared with the terminal), never while a session runs on it, never at each look, and never your own sign-in unless you press refresh. A start held on accounts that are not signed in names each one and how to sign it in, in both languages, and `daoris agent list` shows Claude Code's accounts once, not once per way of running it.
- **A squash-merged pull request's branches are tidied on its plugin's word** (PLUGHOOK1a, D148). Where git sees no ancestor after a squash, the landing's tidy and the clean-up ask the branch's plugin, and remove the session branches only where git confirms the pull request's merge commit on the line. The Azure DevOps plugin (1.2.0) answers through `az`.
- **The shared doctrine says how often to verify** (CANON10): the checks a change can reach while working, the whole set once at the boundary, and after a fix only what failed.
- **What needs you leads Overview** (UX6c, D150): what is holding work, what waits for your word and what is ready for you, the longest waiting first, with go-aheads, departures and work to review among them. A row settles what one press can (*Publish to …*, *Try again*, *Accept the departure*) and asks once where a press would widen what Daoris may do; no row starts anything by itself.
- **A view you come back to opens on what is still live** (UX6b): a closed or deleted item you had chosen is no longer reopened, the side bar off Sessions follows only a session that is running or waiting on you, and a long note in the timeline folds to four lines with *Show all*.
- **`daoris-driver trees state` asks a landed branch's plugin again** (PLUGHOOK1c), and bringing a repository up to date asks after its fetch; the clean-up, `trees land --plan`, `git branches` and `trace` say what the plugin last answered about the branch's pull request, or why nothing was asked.
- **Publishing the desktop app refuses a tree the full gates have not passed** (GATE3): `node tools/merge-branch.mjs --full` runs them, and `--force-ungated` publishes anyway, saying so.
- **A plugin declares the tools it runs** (PLUGTOOL1a, D150): `tools` in `plugin.json` names each one, the version it needs and why, with up to four readiness checks; `daoris-driver plugins try` checks each before the handshake, and `daoris plugin list` names them. A malformed entry is shown, never a refusal of the plugin.
- **A repository's setup is on its own page** (UX6f, D150): Details and Setup tabs, with driving, line and landing, session language and standing answer, and reading and writing across, each section folded to one line when it holds Daoris's defaults. Settings keeps each workspace's default and a door to each repository that sets its own.
- **A workspace has its own page, and Settings is the machine's and yours** (UX6g, D150): Repositories groups by workspace; a workspace's page holds its repositories and accounts, its branches' clean-up and bring up to date, and its defaults, remote and rules. Settings → Workspace and Settings → Permissions are retired, and every door to them leads to the new pages.
- **Agents is a place, with one account list per agent** (UX6e, D150): each account's last known state and when it was read, its acts on its row, and how accounts are used, workspaces, ways in and what the agent may do folded beneath. Opening it asks no account; *Read again* reads one agent's. Settings → Agents is retired.
- **Daoris reads an account only when you ask** (ROSTER1): not at start, not at a look, not when a view opens. Each account keeps its last reading and when it was read across a restart, *Read again* reads one agent or one account, and changing a default or removing an account reads nothing.
- **A start refused for its sign-in no longer parks the work** (ROSTER1b): the account is marked signed out and the next start goes to another, the refusal never counts as a failure of the quest, and the record says which account needs signing in.
- **Every agent Daoris supports is on the Agents list** (AGENTS2), with *Install* where it is missing, including Codex and DeepSeek Harness (DeepSeek's own agent, on the protocol door).
- **An account is named by you, and signing in reaches the account you meant** (ACCT1, ACCT2, D125): a new account's id is `acct-` and eight hex characters, never reused, and its name is yours (`daoris agent profile rename`), kept by every list, default, reading and record. A sign-in reaches an account already here or is refused before anything starts, joins the rotation lists you name in the same step (`--join`), and an account in no list says so.
- **A menu bar like an IDE's** (UX7a, D152): Workspace, Edit, View, Go, Run, Terminal and Help, from one table that the palette, the keys and Help → *Keyboard shortcuts* also read. Alt or F10 reaches it, Alt and a letter opens a menu, and Run's and Edit's acts are enabled exactly when the page offers them.
- **A quest has a short title** (SESSUX1j, D126): its publisher's, at most 40 characters, from the intake, `quest_publish`, the HTTP doors or the composer; otherwise one read from its own words, a ticket key such as `TK-2203` leading. Lists, heads, session names and a landing's branch use it.
- **A quest's head and a session's lead with what matters** (UX7c, D152): the state, the title, and one line of facts; ids and the rest fold into *Details*, and no title is said twice.
- **This workspace's agents get a knowledge server that starts** (ORIENT1c) and an index of where things are (ORIENT1, `docs/index/`), kept fresh at every merge.
- **Managed tools download, verify and lay out** (TOOLS4, D121). `daoris tool download|update|delete|locations|look` and `use <tool> managed` fetch a version from the resource list, check its size and SHA-256 at each address, unpack it whole and record it; a refusal names its check and leaves nothing behind. Starting a managed tool is next.
- **A plugin installs from its package** (PLUGDIST1a, D120). `daoris-driver plugins install <file.nupkg>` reads a Daoris plugin package offline, refuses one that is not of the type, carries dependencies, or names a path outside its folder, and records where it came from.
- **Search finds what a long document says past its start** (SEM3, D123). The semantic tier embeds every part of an entry in pieces the deployment's window bounds (`DAORIS_EMBED_WINDOW`, default 2000), where it had cut each entry at its first 2,000 characters; a hit still names an entry once, and a refresh says how many entries were split.
- **The service reads the records a repository declares** (DOC5, D122), and this repository declares its own (DOC4): the decisions, fixes and archive at the paths `daoris.json`'s `documents` names, the router as a document, and a declared path judged where it lands rather than as spelled (DOC3a).
- **Every program Daoris starts is the tool you chose** (TOOLS5, D121). Daoris's own git, a hook's first word, a pin's npm, the tree guard's node and the terminal's shells start as the files the tools resolve, and every session, chat, hook, plugin and terminal is handed the tools' folders first on its `PATH`; a tool whose chosen way cannot run refuses rather than falling back to whatever `PATH` holds.
- **Search and Convergence are on the frame** (FRAME1f, D118). Search's list is the box, *each repository's own only* and the hits; the chosen entry is read in the main area. Convergence's list is the similarity and the findings; the chosen finding is read with each entry whole under the service's sentence. The reader drawer is gone, and the chosen item and the filters are remembered.
- **Settings → Tools** (TOOLS7, D121). Each tool Daoris runs has a card: why Daoris needs it, how it is run (the system's, a managed version, or a file you name), the file and the version it answers. Managed versions download with their console and a stop on the card, a switch of git first says what the two gits read differently, and resource locations are listed, added and looked at.
- **A driven session looks before it asks you** (WSSETUP9, D124). It reads the quest and its files, its repository's documents, code and history, and the workspace's knowledge first; what they settle it decides, a reading they lean to it takes and says, and it stops only for a sign-in, a go-ahead outside its repository or on a live system, or a preference nothing records. Every stop is now counted (WSSETUP11): the machine log records a park and each turn's tokens and calls, and the usage report shows parks per week by workspace and what each set-up cost.
- **A repository that has not adopted is searchable by its README** (WSSETUP8, D124), split at its headings, until it adopts; nothing is written into it.
- **A refresh says what it embedded** (SEM3b), and Settings → AI names `DAORIS_EMBED_WINDOW`. **Saving a declaration keeps what the repository uses** (MANAGE1): Repositories → Manage no longer erased its `uses` from the registry.
- **A repository is set up for agents by its own session** (LAYOUT7 as D124 amends it). `daoris-driver setup <repository> [--plan]` reads the repository's line as git objects (its layout, instruction files, tiers and rooms), says every reason it would refuse at once, and publishes one set-up quest as your ask: the doctrine tool's version checked first, the doctrine taken up, the knowledge a neighbour needs written (what it owns, its contracts and data, the computations others depend on, each with its place in the code), the brief, documents and safe work, in the repository's own tree and never pushed. The session is allowed exactly the `daoris` verbs it needs. The family rehearsal sets a repository up end to end (LAYOUT7a).
- **Sessions are listed by what they wait on** (SESSUX1c, D126): Waiting on you, To review, Working, Resumes later and Ended, with *Group by repository* in the list's ⋯, and the badge counts a parked quest. **Your stop holds its quest** (SESSUX1b, SESSUX1b2): it is not planned again until you press *Try again*, also `daoris driver retry --session` and Ask Daoris's `retry`.
- **The doctrine says a session pays for what it reads** (SESSOPT1a, D127): a backlog row, a router row and an archive outcome each take a short shape and point to their detail, and `verify` reports any that run over (SESSOPT1b).
- **Pause, resume and abandon an ask from its page** (PAUSE1d, PAUSE1e, D132): *Pause…* stops an ask's or a quest's work and keeps its place; *Abandon…* lists what would go and what stays, and with your reason declines its quests, discards only trees whose commits exist nowhere else, archives its sessions and shows *What went* with the command that brings a branch back; also `daoris-driver ask --abandon` and `quest abandon`. **A session's closing note keeps what needs you apart** (KNOWUSE1c, KNOWUSE1d, D135): *Needs you* holds only go-aheads, sign-ins and preferences; every reading names its ticket line, document line or code path; and your words quoted second-hand count only as someone's reading.
- **What you tell a working session reaches it at its next step** (STEER1, D136): on Claude Code's protocol door your words are handed over at once and read before its next model call, shown in the conversation as soon as you send them; other doors hold them to the turn's end and say so. **A go-ahead is asked once** (KNOWUSE1a, D135): a session asks for an act through one tool, a second request for the same act joins the first, and every session on the ask is handed your answer; answer on the ask's *Go-aheads* or with `daoris-driver ask --go-ahead`. **A standing answer per repository** (KNOWUSE1b): `daoris driver standing <repo> "…"`, the repository's page or Ask Daoris keep one answer on this machine that every session there is handed.
- **An ask's work can be paused and resumed** (PAUSE1b, PAUSE1c, D132): `daoris-driver ask --pause|--resume <id>` and `quest pause|resume <id>` stop what it runs as your own stop and keep each quest's place, tree and strikes, and a paused quest says so before any other reason; a decline that applies only while a quest is open never overrides a teammate's take.
- **A quest's requirements are your words, and a done answers each** (DRIFT1c, DRIFT1d, D133): a quest from an ask carries requirements, each your words quoted with its check, and words you never said are refused; a session is handed them, a done must say for each whether it was met or departed from (with your words), and a departure holds the next step until `daoris-driver quest accept`. **A conversation that served no quest can be deleted** (SESSUX1f, D126), from its header or row, asking once. **`daoris-driver sessions`** (SESSUX1g) lists sessions by group and stops, finishes, declines, archives or deletes one from a terminal.
- **Settings → Agents sets how your accounts are used** (TOOL4g, D130): each account's cool-off with *Try now*, its running sessions and what its agent last said; the machine's list and each workspace's own, with *Use accounts*, *Keep for conversations*, *Switch before the limit* and *Near at*; Settings → Driver sets the cool-off, and a quest waiting for an account says so. **Every session on an ask is handed your words** (DRIFT1b, DRIFT1a2, D133): beneath its quest, newest last, whatever account it starts on; a message you type into a running session is kept on its ask too. **An answered session reads as going on** (ANSWER1c): it moves to *Working* as *answered*, its card shows your answer, and the badge stops counting it.
- **Answering a session that asked you continues it** (ANSWER1b, D131): the session keeps your answer and, at the driver's next look, its own conversation resumes with your words, one row going on, on the same account and tree. **Everything you say on an ask is kept on the ask** (DRIFT1a, D133): its sentence, each answer and each message to a running session, verbatim, with when and to which session, in the ask's `words`.
- **What each account has left is read from its agent** (TOOL6c, D130): Claude Code's own report of each window's use and reset, on both doors, is kept per account; an account its agent says is near its limit is passed before it cuts a session off, the weekly pace ranks the rest, and `daoris agent list` and `profile use` show each account's last reading and its age. **An answer says why it starts a new session** (ANSWER1, D131): where the agent's own conversation can be resumed on the same account and tree, the answered session goes on; until the service keeps an answered park open (ANSWER1b), the new session's note names the reason.
- **Your accounts are used toward the most work** (TOOL6b, D130): by default each start goes to the listed account running the fewest of Daoris's sessions, then one whose weekly reset is within a day, then the one started least recently, so sessions running at once spread over your accounts and one limit cuts off fewer of them; `use order` keeps the one-by-one walk, and each start's first line says which step chose its account. **What needs you holds a parked quest** (SESSUX1i, D126): a quest parked on its failed sessions shows on Overview with a door to its page, and its park is said once. **The knowledge index is read on demand** (WSSETUP14a, D128, D129): `sync` writes `INDEX.md` beside the doctrine with the knowledge and skill tables, and the always-loaded region keeps the rules and a pointer to it, so a repository's knowledge no longer crowds out its rules.
- **How each account list is used is yours to set** (TOOL6a, D130): `daoris agent profile use <agent> [goal|order] [--keep <account>|--no-keep] [--early on|off] [--near <percent>] [--workspace W]` keeps, per machine or per workspace, whether its accounts are used toward the most work or in your order, an account kept for conversations, and switching before a limit; `daoris agent list` shows them, and a default or a kept account outside its list is refused. **A Codex limit is read, and a weekday reset** (ACPDATA1, TOOL4k, D125): the protocol door keeps the sentence a refusal carries, so Codex's *usage limit* cools its account, and *resets Mon 12:00am* is read.
- **Every act on a session is where the session is** (SESSUX1d, D126): Sessions has a pinned page header with the session's primary act, *Stop…* (asking once) and ⋯, and a row's ⋯ offers the same acts, *Open folder* and *Open a terminal here* among them; the quest page's *Try again* also releases a stop. **A limit that resets later today is read as tomorrow's when its hour has passed** (D125 fix): an agent that says *resets 4pm* at 4:12pm keeps that account cooling until tomorrow, instead of two minutes.
- **A limit moves the next start to your next ready account** (TOOL4f, D125): a start whose default account is cooling runs on the next ready account of your order, and its record says which account it moved from; a carry-on is handed the cut-off session's last plan and last words. **Ended sessions are archived from the list** (SESSUX1e, D126): *Archive* on a row, and *Show archived* and *Archive what ended…* in the list's ⋯.
- **Your account order is kept, and set from a terminal** (TOOL4e, D125): `daoris agent profile order|ready`, `daoris driver cooloff <minutes>`, and `daoris agent list` shows each cool-off; a sign-in or a key ends that account's cool-off.
- **An account limit no longer parks your work** (TOOL4a, TOOL4c, TOOL4d, D125). When an agent says its account hit a limit, that account cools off until the reset it named; the refused session is marked with the limit and is not a strike, and new starts on that account wait, said once, instead of being refused into a parked quest.
- **A whole workspace is set up one repository at a time** (WSSETUP6, D124): `daoris-driver setup --workspace <name>` orders the repositories by what other work touches, publishes one set-up at a time, and pauses after a pilot of two until you resume.
- **A plugin is switched on and off by 启用 / 停用** (NAME2, D116), a page header's line wraps whole, and *lane* is 泳道.
- **Daoris's sessions find the install's doctrine tool, and a repository registers itself once its set-up lands** (WSSETUP3, WSSETUP5, D124). Every program Daoris starts has the install's `app/bin/` first on its PATH, so a set-up session runs `daoris` by name; and after Daoris moves a repository's line (a merge landing, *Bring up to date*), once at start, or on `daoris-driver register`, the driver reads `daoris.json` from the line and registers the repository as `connect` would, for its checkout, saying any refusal on the repository's row.
- **The install carries its doctrine tool** (WSSETUP2, D124): the CLI as the release packs it, in `app/cli/`, with `daoris` launchers for Command Prompt, PowerShell and Git Bash in `app/bin/`. **An older tool never rewrites a newer lock** (WSSETUP4): `sync` and `upstream` refuse a lock written by a newer canon, naming the version to run, and `status` says so instead of offering a downgrade.
- **The monitor and a detached session are on the frame** (FRAME1h, D118): the monitor's rail is a list that closes to a strip and is never hidden, and a detached window's console is the output panel, resized and hidden like the main window's.
- **Agents are told to look before they ask** (WSSETUP10, D124). The canon's `autonomous-development` says a question the task's own material, the repository's documents, code and history, or a neighbour's documents can settle is the agent's to settle, with its evidence kept; only what no source holds and only the person can give reaches them mid-run.
- **Settings is on the frame** (FRAME1g, D118). Its domains are its list, which closes to a strip like every other list rather than stacking above the domain on a narrow window, and each domain opens at its top.
- **A repository says where each kind of document lives** (DOC3, D122). `daoris.json`'s `documents` maps a role (the decisions, the backlog, the archive, the changelog, and the rest) to its path, and `sync` writes a *Where things are* table every agent reads, so a session stops searching for where something is written.
- **Shenora 0.19 and Lyntai 3.5.3** (SHEN2, LYN1). The window is on the desktop kit's 0.19; the service is on the model library's 3.5.3, with no change to what it answers.
- **A plugin's landing waits as long as the plugin may, and the rest of the night's leftovers** (LEFT3).
  Accepting into a plugin's pull request, and handing a branch to one, wait up to six minutes rather than
  thirty seconds. Ask Daoris's *Bring up to date* card says on the card what it could not fetch and what it
  left apart. `daoris agent profile default <agent> --clear [--workspace <name>]` clears a default from a
  terminal, as the screen does. The usage report counts previews opened, and the landing record keeps
  each repository's newest fifty traces of gone branches.
- **The window says when the driver is up, and a landed session says where its work went** (LOOK2). Right after a start the status bar reads *starting* until the driver is ready, and every screen that asked too early asks again, so Settings no longer shows *no repository* for a moment that lasted. The session list names where a landed session's work went instead of a tree that is gone, and choosing a workspace default on the Agents screen says what sessions there will run as.
- **A plugin can push the branch and open the pull request** (WSR4, D100). A branch landing rule may
  name an installed plugin (`daoris driver landing … branch <pattern> --plugin <id>`, or the *who
  pushes it* chooser in Settings → Workspace → *How work lands*). Once accepting has made the branch,
  the plugin is told it on the new `work/land` point, pushes it and opens the pull request for its
  platform, and answers with the pull request's address, which the review offers as a link and
  `trees land` prints. Nothing pushes unless a rule names a plugin; Daoris itself still never pushes. A
  plugin that is missing, off or lands nothing is refused when the rule is set and again at the press,
  before anything is made. One that fails once the branch is made leaves the branch, and the sentence
  says how to push it by hand. Two example plugins are tracked, inert until installed and named:
  `examples/plugins/github-pull-request` (`gh`) and `examples/plugins/azure-devops-pull-request`
  (`az repos`).
- **A kit to make a plugin with** (PLUG8, D101). `daoris-driver plugins new <id> --point <point>…`,
  or *Make a plugin* in Settings → Plugins, writes a plugin's folder into one you name: a manifest, a
  wire script already answering each point in the right shape, a README carrying the wire and the rules,
  and a wire test that needs nothing of Daoris's, so `node --test` is a plugins repository's whole gate.
  It refuses a folder that holds anything, and installs nothing. `daoris-driver plugins try <folder|id>`,
  or **Try** beside each plugin that speaks, starts a plugin as the driver would, sends the driver's own
  frames, and says each check in its own sentence: a wrong answer, a silent plugin, a crash, a stray
  line on stdout, a plugin that stays after shutdown. The sample frames name no repository, so a
  plugin that pushes first pushes nothing. `daoris plugin new|try` say where the kit is. A JSON line on
  a plugin's stdout that is not a frame no longer ends its wire.
- **`daoris-driver trees land <session>`** accepts a session's work from a terminal, as the review's
  Accept does, by the workspace's rule; `--plan` says where it would go. A tidied landing no longer
  also says its tree is still there.
- **Session branches are cleaned up once their work lands** (WSR3, D88). They piled up: one repository
  had sixteen empty ones. A branch's work counts as landed when a branch of the person's holds every
  commit: the line, a feature branch, or one they pushed. Settings → Workspace → *Session branches*
  lists every session branch with what it holds, and one press removes those that hold nothing or
  whose work has landed, with their trees. Work nothing of the person's holds, uncommitted work, and a
  tree a session still uses are kept, and Projects names a repository whose branches hold such work.
  `daoris-driver trees clean` is the same list from a terminal, and `--yes` is the press. A landing
  rule can say *tidy*, so the tree and branch go as soon as the work lands. A tree whose work is on a
  feature branch is now removed without `--force`.
- **How work lands is a workspace's rule** (WSR1, D87). Accepting a session's work merged it into
  the line on this machine, which skips a team's review. A workspace, or one repository, can now say
  **branch** with a pattern such as `feature/{quest}-{slug}`. Accepting then puts the work on that new
  branch, from the session's branch, with nothing merged and no checkout touched, for the person to push
  and open a pull request from. The review says where a press sends the work before it is pressed. A
  session whose work goes through review is told not to merge or push it. Set it with `daoris driver
  landing` or Settings → Workspace → *How work lands*. Daoris never pushes; that form is a plugin's
  (WSR4, above).
- **A repository's line can be set** (WSR2, D86). The branch its work grows from and lands on was
  only git's guess (`origin/HEAD`, else `main`, else `master`). A person now sets it per repository or
  as a workspace's default, with `daoris driver line` or Settings → Workspace → *Lines*, and a
  repository's own setting wins. A session's tree, the merge, a tree's removal, sync's feed and a
  chain's next step all read it from one place. Projects shows each repository's line and what said
  so. A line only the remote has is grown from `origin/`, and one that is nowhere is refused, naming
  the verb that changes it.
- **Two conversations in one repository no longer break the driver.** A conversation in a checkout
  beside another in its own tree (D51) made every tick fail on a duplicate key, starting nothing
  until one ended.
- **Scrollbars follow the theme.** A scrollbar is painted by the browser, and with no
  `color-scheme` it stayed light down a dark drawer. The page now declares both schemes, and the
  bar wears the line token.
- **"Send it back as a quest" opens the composer again.** The review's door switched the window to
  Quests and opened nothing, because the composer consumed the draft during render and lost it to
  the parent's clear. The palette's *start a session* and *review* reached Sessions the same
  way. Both are now consumed once, by identity.

### The remote

- **Team mode: the same host, fed by the desktop.** The remote is a deployment of the existing HTTP
  host in **shared mode** (`DAORIS_MODE=shared`), not a second implementation — every route gated by
  **per-person per-machine minted keys** (`keys mint|list|revoke` on the binary; stored as a hash with
  a short non-secret audit prefix, shown once, expiring by default). It serves no page and answers no
  machine path, and a host asked to bind beyond loopback without shared mode refuses to start.
- **The quest lock is code.** A quest's transition table is enforced in the store itself — `Taken` only
  from `Open`, closed quests immovable — and the same table is replayed wherever a quest's history is,
  on every machine and at the remote. The same hardening runs in local mode.
- **A quest is its history, and the remote is where histories meet** (D68). Every verb — publish,
  take, done, decline — commits on the machine where it was made and always succeeds there, whether
  or not a remote is wired, reachable or down. Quest ids are twelve hex characters. The desktop's
  sync rides the driver tick. It feeds registrations and opted-in knowledge content **up**, and
  mirrors teammates' registrations **down**. **Session records travel both ways by cursor**: each is
  sent when it changes, and every machine sees the team's records, read-only and keyed by origin. A
  teammate's session is never this machine's lock. For quests it runs
  **fetch, rebase, push**: the remote orders what it accepts, the first push wins, and a move that
  lost is kept on the quest as a **conflict** rather than dropped. **A take claims by push** (D69).
  A take on a shared quest waits for the remote's answer, so a take that lost stands down before any
  work. Offline, the take stands unconfirmed, and a session whose take later loses is stopped by its
  own machine's driver. A quest leaves a machine only when its receiver is joined. What may leave a machine at all is two manifest declarations — **join**
  and **share knowledge** — and silence means local. Roots, transcripts and a file's bytes have no
  field in anything fed.
- **Proven by a two-machine rehearsal.** The family rehearsal grows a remote phase with a shared host
  and two simulated machines. A quest published on one is driven to done on the other and the closure
  crosses back. A machine that sees a quest already taken leaves it alone. When both machines take one
  quest online, the second stands down before any work. When both take it offline, the second's take
  is kept as a conflict everywhere. A session that took offline and lost is stopped before it lands
  anything. Verbs made while the remote is down are pushed when it returns. Knowledge crosses only where declared, keys are refused without
  being echoed, and the remote store is scanned to hold no machine path — no model in the gate.
  A two-workspace phase joins it: two circles on one machine, a search answering from one while the
  other holds the same lesson word for word, and a quest across the boundary refused with both sides
  named.

### Workspaces

- **The workspace is the unit of sharing.** Knowledge search, convergence, the registry, quests and
  session records all answer within one workspace and never across one — so a machine can hold a game
  family and a work family without either seeing the other. A machine that never names one runs exactly
  as before: everything lands in `default`, silently.
- **Membership is wiring, like a git remote — never a tracked declaration.** `daoris connect
  --workspace <name>` records it in this machine's registry and **writes nothing into the repository**,
  so a fork, a mirror and a second machine may each wire the same repository differently. Omitting the
  flag preserves whatever wiring already existed, because an ordinary `connect` runs on every sync tick
  and must not re-point anything; the command reports back the workspace that actually took.
- **A quest does not cross workspaces**, and the refusal names both sides, both circles, and the two
  things a person can do about it — re-wire one of them, or carry the request across by hand.
  Addressability offers only the asker's own circle.
- **A session's answers default to its own circle.** The MCP tools take an optional `workspace`; with
  none, the scope is the workspace of the repository the session is running in, resolved from the
  registry by path — an agent never has to know wiring that is not in its tree. Every answer says which
  scope ran, including when it spanned every workspace, and `all` is the spelled way to ask for that.
- **Nothing a feed claims decides where it lands.** Fed entries and fed session records take the
  *receiving* deployment's wiring, because a feed that could name its own workspace could write itself
  into someone else's.
- **One shared deployment serves one workspace**, and says which (`DAORIS_WORKSPACE`): every row it
  takes lands in that circle, and a registration declaring another is refused in a sentence naming
  both sides. A local host, which holds every circle the machine wired, refuses the variable outright
  rather than ignoring it. The machine's remotes became a **map** — `remotes.json` under the home, one
  deployment per workspace — with the sync running once per circle and the quest relay resolving by
  the quest's own workspace. A circle with no entry syncs nowhere, silently, which is what every
  machine does until someone says otherwise.
- **`daoris remote list|add|remove` and `status --machine`,** plus the desktop's new **Machine** view
  over the same file. Both are editors; the file is the truth, so hand-editing keeps working. A key is
  never printed back — only the audit prefix the deployment's own `keys list` shows — and the
  `remote` verbs are management commands that speak to nothing at all.
- **A feed carries the commit it speaks for.** Two checkouts of one repository are two points in its
  history, and wholesale replacement between them was a flapping generator: each tick, whichever fed
  last overwrote the other. Now the driver stamps `{ commit, committedAt, branch }` from git, a
  deployment takes knowledge **only from the repository's declared canonical line**, and a feed older
  than what it holds is refused. Deletion stays correct for free — an entry absent from the newest
  canonical view is one the repository deleted — and `/api/repositories` answers the commit each copy
  stands on, shown on Projects, because an index is a claim about a commit and staleness someone can
  see beats freshness they must assume.
- **Ancestry decides where git can say.** Before it feeds, the driver asks the deployment which commit
  it holds and asks git how its own commit stands to it. A descendant feeds with that commit as its
  base, and the deployment takes it as a fast-forward whatever its clock says. If another machine
  fed in between, the feed is refused as *moved*. A checkout that is behind, or has not fetched the
  held commit, feeds nothing and says whether a pull or a fetch would fix it. Commit time decides
  only for diverged histories.
- **The same commit is one reading.** A deployment hashes what it would store. The same commit with
  the same content is already held; read differently, the first reading stands and the second
  machine hears why, instead of the two replacing each other on every tick. And only a clean
  checkout feeds, because a feed speaks for a commit and work in flight is not yet that commit's.
- **A declaration is ordered the same way** (SYNC5b). A registration names the commit its manifest
  stands on, and a shared deployment orders it like knowledge. Before, the last machine to register
  won, so a checkout that was behind put its older declaration back on every tick. The first
  registration is taken from any line. After that, only the line the declaration calls canonical is
  taken, and a manifest with uncommitted changes names no commit and does not replace one that did.
- **Teammates' repositories stay current, and a retire travels.** The desktop used to copy a
  teammate's registration once and never again, and a retire reached no other machine. Now each pass
  rewrites a copy whose declaration changed, and removes a copy the circle no longer lists. A joined
  checkout that leaves a circle owes that circle a retire: removed, moved to another circle, or no
  longer joined. The store records the retire when the row changes, and the next pass carries it.
- **Sync when asked, and see where a circle stands** (SYNC6a). `daoris-driver sync` runs the tick's
  own pass now, for every circle with a remote or the one `--workspace` names. `daoris-driver sync
  status` prints where each circle stands on this machine: operations not yet pushed, quests left
  behind, quests in conflict, and when it last reached its remote. A pass that could not reach the
  remote keeps that time and names the wall as the last try. The status reads the machine's own
  host and never contacts the remote.
- **The status bar shows where a circle stands** (SYNC6b). A wired circle's remote item becomes a
  control: ↑ for work waiting to go up, ↓ for quests left behind, ⚠ for quests in conflict, `synced`
  when level, and `unreachable` when the last try hit a wall. Pressing it shows when the circle last
  synced, the wall in the host's own words, and the quests in conflict, each of which opens in its
  drawer. The desktop adds *Sync now*, the same pass `daoris-driver sync` runs. A browser on this
  machine reads the same standing and has no *Sync now*. A quest's drawer now shows each move that
  lost the race to the remote, with that session's note.
- **Every wired circle syncs, and a wall no longer hides itself.** A circle where nothing on this
  machine is joined still gets its pass, so the team's repositories and quests come down to a
  machine that has only asks to make. When the feed hits a wall, the quest pass still runs, and that
  is where the try is recorded, so the status bar can no longer say `synced` while the sync is
  failing.
- **A conflict can be dismissed, everywhere** (SYNC6c). The quest's drawer has *Dismiss* beside each
  move that lost the race, and `daoris-driver sync dismiss <quest>` dismisses every one a quest
  carries. A dismissal is an operation like a take, so the next pass carries it and every machine
  stops showing the conflict. It moves no status, and two people dismissing one conflict make one
  dismissal.
- **A refusal can be information.** A stale or branch feed is the rules working, not a fault, so it is
  flagged as such on the wire and the sync reports it as news rather than as a wall. Records and
  quests still travel from any checkout; only knowledge waits for the canonical line. A checkout git
  cannot answer for feeds no knowledge, and the machine that holds it says so itself.

### The working surface

- **A session's console, live.** The driver already captured every session's output to a transcript;
  that capture now tees into a bounded window the desktop streams to the session drawer as it
  happens — verbatim, never translated, and desktop-only: output is transcript-class material and has
  no HTTP route at all, so a browser over a keyed remote sees the record and never the stream. The
  window states what it dropped rather than showing two halves of a log as though they joined, and
  the transcript on the driving machine still holds everything.
- **A conversation is a session.** A person can open a chat with an agent in any repository: the same
  record the driver uses, the same observed lifecycle, and the same one-session-per-repository lock —
  because two agents in one working tree corrupt it regardless of who is typing, and the refusal names
  what holds it. A chat serves no quest by default; it may take one mid-conversation through its own
  connector, or end by publishing the work that came up, which is how work that was not yet an ask
  becomes one without anybody editing across. Ending it is two different verbs: finishing lets the
  harness wind up, stopping cuts it off, and the record says which happened.
- **The harness is the chat.** Daoris pipes the person's lines in and streams the session's out, and
  makes no model calls at all: which model answers is that repository's own harness configuration.
  The adapter seam grew one honest capability — an adapter that has not been wired for turn-taking
  says so rather than spawning something that will never answer.
- **Two doors, one conversation.** The desktop has a chat drawer over the live console; a machine with
  no screen has `daoris-driver chat --repository <name>`, where stdin is the person and stdout is the
  session. Same runner, same lock, same record — what stays desktop-only is the *stream*, not the
  capability.
- **Daoris manages the harnesses, and never a credential.** It finds the agent tools on the machine,
  reports their versions, and installs or updates them **through their own official mechanisms, only
  when asked** — never automatically and never mid-session, because a tool that changed between two
  runs nobody diffed is a gate that stopped meaning anything.
- **One harness, many accounts — as named profiles.** A harness holds one login per configuration
  home, so switching accounts used to mean logging in again. Now an account is a named, isolated
  configuration directory whose *location* Daoris owns, selected at spawn through the environment
  variable each harness already has for exactly this. Choose one per machine, one per workspace — a
  work account for the work circle, a personal one at home — or one for a single conversation.
  **Daoris never sees, stores, or copies a credential**: logging in runs the harness's own flow inside
  that directory, and what it obtains stays in the harness's own store under your OS account. What
  Daoris keeps is a directory and a name.
- **A record says which tool and which account did the work.** Every session now carries the harness
  version observed at spawn and the profile it ran as — the same authorship instinct as stamping a
  release, applied to the tool that did the work. The version travels with the record; the account
  name stays on the machine that ran it, guarded like the transcript beside it.
- **A spawn that cannot work refuses before anything is recorded, naming the fix.** A harness that is
  not installed, or a profile nobody has signed into, holds the start with the sentence that says what
  to run — not a bare not-found. The quest stays open and nobody's, and doing what the sentence says
  releases it on the very next tick, with nothing restarted.
- **Two more commands, and both surfaces do the same thing.** `daoris agent list|install|update|
  login|profile …` and `daoris driver list|drive|hold|resume|cap|adapter` set all of it from a
  terminal, because a server with no screen is still a machine; the desktop's roster edits the same
  files. Neither reaches a network.
- **Fixed: every refusal the desktop made reached you as a blank failure.** When the app could not do
  something — a remote missing half its pair, a declaration form pointed at a repository that has not
  adopted, a conversation asked for before the driver is up, an adapter it does not have — it had a
  sentence written for you, and you never saw one of them. Now you do, in your own language, and the
  driver's own wording travels word for word.
- **Fixed: `refresh` re-read the repositories but never the folder.** The root's subdirectories were
  listed once at startup, so a repository created afterwards was invisible to the index while being
  fully registered and quest-addressable — and the refresh reported success either way.

### The managed registry

- **The registry is the authority; being in a folder is not being a member.** The family used to be
  whatever a root folder happened to hold, and that failed the way scans fail: what a scan does not say
  governs as much as what it says, and nobody reviews a silence. Now it is an explicit list — name,
  workspace, declaration, and the checkout path, machine-locally. The index reads the registered paths,
  so a folder nobody added contributes nothing.
- **Two new commands, `retire` and `import`.** `daoris retire [name]` takes a repository off this
  machine's registry and **nothing else** — no file, no history, no doctrine — and says so; retiring
  something already retired is an answer, not a failure. `daoris import [folder]` is the old folder
  scan, demoted to a verb a person runs: safe to re-run, because it states no workspace and unstated
  wiring is preserved.
- **A store that has never been managed imports its root once, and says so.** Without it, a machine
  that had been running on `DAORIS_KNOWLEDGE_ROOT` would come up to an empty family after the upgrade —
  and an empty family is indistinguishable from a broken one. Once, because a second run would
  resurrect everything the person deliberately retired. A deployment that is fed rather than scanned
  imports nothing, by the same rule that keeps it off its own disk.
- **A registered checkout that vanished is named.** `refresh` reports it — moved, deleted, or
  registered from another machine — instead of quietly indexing nothing while the count still looks
  healthy. A registration with no path is not an absence: a teammate's mirrored row has no checkout
  here by construction.
- **The desktop manages the machine's repositories.** Projects gains add, re-wire, declaration and
  retire where a shell is attached — the shell supplies the folder (a browser may never learn a machine
  path) and the page registers through the ordinary loopback door. The two kinds of update are kept
  visibly apart: re-wiring edits one row here and touches no file; editing the declaration writes
  `daoris.json` in that repository and leaves the diff uncommitted for its own review. Adoption stays
  that repository's own act, shown as commands to run rather than a button. Doctrine stays unwritable.
- **The CLI's offline guarantee is now stated over a class, not a command.** `check`, `sync`,
  `upstream` and the rest are offline by construction; `connect`, `retire` and `import` are the opt-in
  management commands, and all three speak through one module — so the test still reads "exactly one
  file may touch the network, and nothing a doctrine command reaches may import it". `remote` joined
  the class and touches nothing at all: it edits a file under the profile, and a test walks its
  imports to keep it that way.

### Proven

- Daoris carries its own manifest and syncs core into its own `AGENTS.md` region and `.claude/`; a test
  asserts it stays clean.
- Adopted into **Lyntai** (a released .NET library): 4 collisions surfaced and resolved deliberately, a
  renamed twin found, 3 packs installed, its own 1337 tests still green — and the budget gate immediately
  caught a real 45% overage on first contact. Lyntai has since stepped back off the tool at its owner's
  request, keeping the synced files as local forks — the adoption remains the proof of the collision,
  twin and budget paths, and re-adoption is a decision that stays with that repository.
- The deployable service was driven live, not only tested: an unauthorized write answered 401,
  `daoris connect` registered through the real endpoint, the host was killed and restarted with the
  pushed registration and a taken quest both still served, and publishing to a non-adopter was refused
  naming who is addressable.
- **A deployment rehearsal** (`npm run rehearse:deploy`) that publishes the desktop shell to a folder
  and drives *that* — the artefact, not the checkout. The deployed window comes up with no help
  finding its service host, and a quest its own driver loop spawns is carried to done with the session
  transcript compared as bytes. Every other loop runs inside the workspace, where the workspace build
  and the console's own encoding hide exactly the defects the first real deployment found.

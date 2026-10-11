# A conversation started from a prompt, and heads whose buttons hold still

**Standing:** contract under D162. Designed with Codex as a second reader (read-only reviews, 2026-10-12); the parent wrote
this record and owns its calls. Its rows are NEW1 and HEAD2 in `TASKS.md`.

> The owner, 2026-10-12: *"we can design the user entry (UI/UX) more like how ChatGPT (Codex) does it, to add and select a
> repo to start a chat; you can design this with Codex. Currently I can only Ask Daoris and let that decide"* · *"also the
> title section for quest needs to be updated (some titles just way too long, and also the buttons' location); you might
> need to check the session title section too; both need to improve on UI/UX (please co-work with Codex for design
> decisions)"*.

It amends D161 point 3 and its rejection of *a second entry point* (§1), and UX7's title sections (D152 §7,
`docs/2026-10-05-ux7-design.md` §5) (§2). §3 is what was rejected; §4 the build.

## 0. What was looked at

- **Starting today.** Ask Daoris takes words and decides: it answers, goes to a place, or proposes an ask (D161). Sessions'
  *Start session* (`work/StartSession.tsx`) is a drawer of four choices (repository, agent, account, own tree) that starts
  an **empty** conversation (`START_CHAT` carries no words, `bridge/conversation.ts` ~:308-323); the person types after. Quests'
  ask composer (`asks/AskComposer.tsx`) publishes an ask. *Start a conversation with these words* (`SESSION_START_FROM`,
  `ChatRunner.StartFrom.cs`) is the one door that opens a conversation on its first words, and only from a session's own.
- **Titles on the install (2026-10-12).** Every quest carries a short title (11 of 11, 15 to 40 characters), as SESSUX1j
  asked. Every **ask** heads with its whole first line: its titles run 200 to 1,715 characters, and the ask page's head
  (`asks/AskPage.tsx` ~:225) has no clamp, its pills after the title and its id line kept: UX7c never reached it.
- **The heads' layout.** `PageHead` (`work/ViewMain.tsx` ~:28-70) wraps the title block and the acts in one flex row, so a
  long title moves the buttons, and below a width they fall under it. A session repeats a shortened quest's whole title
  under its head (`SessionHead.tsx` ~:250-253, `WholeTitle`).

## 1. New conversation: the prompt first

**Where.** Sessions' main area, as *New conversation* (新建对话): the composer stands there when nothing is attended, and the
window's *New conversation* (`Ctrl+N`, the File menu, Sessions' list head) opens it with the rail kept. Ask Daoris stays the
general front door (D161 ENTRY1): a go may open this composer with its repository and words filled (a go part), and the
composer offers *Make it a task with Daoris…*, which hands the draft and its repository to Ask Daoris to propose an ask.
A conversation started here is Sessions', as every conversation is (the working-surface design §3).

**The composer.**

```text
 Sessions' main area, about 800 px
┌──────────────────────────────────────────────────────────────────────────┐
│  What would you like to work on?                                         │
│ ┌──────────────────────────────────────────────────────────────────────┐ │
│ │ Fix the flaky retry in the sync look; the 2nd attempt reads a stale…│ │
│ │                                                                      │ │
│ └──────────────────────────────────────────────────────────────────────┘ │
│  [▣ engine ▾]  [⑂ checkout · main ▾]  [Claude Code · driver's ▾]  [Start] │
│  Make it a task with Daoris…                                             │
└──────────────────────────────────────────────────────────────────────────┘
 the repository chip, open
┌──────────────────────────────┐
│ ⌕ find a repository          │
│ Recent                       │
│   engine          aurora     │
│   tools           default    │
│ aurora                       │
│   game · busy (a session)    │
│ Add a repository…            │
│ Import a folder…             │
└──────────────────────────────┘
```

- **Repository**: searchable; *Recent* first (this machine's last successful starts), then each workspace's repositories,
  each with whether it has a checkout here; one with none is listed, not choosable, and says why. The last one chosen is
  remembered on this machine.
- **Tree**: *driver's choice* (the repository's standing opt-in), *the checkout* (its current branch shown) or *its own tree*
  (named, with D51 §4's cost). No other branch yet; the person's checkout is never switched.
- **Agent and account**: one chip, *Claude Code · driver's* by default; its menu lists the agents installed here and their
  accounts by the workspace's list (D130 §3.2), as `StartSession` reads them. A new agent clears an account it cannot use.
- **Enter starts, with the words** (Shift+Enter is a new line; an input method's composing Enter does nothing). The
  conversation opens on its first message, and the page attends it.

**What the driver needs** (NEW1a). `START_CHAT` takes the opening words (and files, as the composer's carry) and a request
id. The driver judges the start as today, opens the conversation, and hands the words to it as its first message through
the machinery *Start a conversation with these words* uses, answering the session and whether the words went. The same
request id again answers the same session and sends nothing twice. The tree choice is said exactly: today a request's
`ownTree` is OR'd with the repository's standing opt-in (`ChatRunner.cs` ~:192-206); *the checkout* must mean the checkout.

**Adding a repository without losing the draft** (NEW1c). *Add a repository…* and *Import a folder…* at the picker's foot
open the existing drawers over Sessions (Projects' `ProjectManage.tsx`); the draft and the other chips stay. Add's
`onAdded` chooses the repository it registered and puts the focus back in the prompt; Import gains the same callback, and
reopens the picker on the group it imported. Registering a repository never starts work or adopts anything.

**States.** First run, no repository here: the prompt takes words, *Start* waits, and *Add a repository…* is the primary
press. A busy checkout: *its own tree*, or open the session holding it; nothing is stopped. A signed-out account or a missing
agent: the driver's sentence in place, with its door. A refusal keeps the draft and every choice and says the whole
sentence where *Start* was pressed (UX5 U68). **A browser** has no shell: the composer is absent and the place says *Open
Daoris on this machine to start a conversation*; *Make it a task with Daoris…* still works there.

**At 680 px** the chips wrap under the prompt, one drawer at a time; chrome in en and zh, the words as written.

## 2. The heads: titles bounded, buttons where they were

**Titles.** A quest's and an ask's title take two lines at most, a session's one; the head never grows. *Show all* becomes
*View full request* (查看完整需求, the glossary's word for an ask), which moves the focus to the request as written, the body's first block, after any card
that waits on the person; the tip stays a convenience, not the way in. **A session stops repeating the quest's whole
title**: `WholeTitle` goes; the request as written is a row of its *Details*, with the quest's door; a conversation with no
quest puts its whole opening there when the head cut it.

**Asks get the quest's head and a short title** (HEAD2d, HEAD2e). The ask page heads like a quest's: its state pill leads,
the title takes two lines, a facts line follows, the id leaves for the ⋯ and *Details*. An ask carries a short title, at most
40 characters, written by whoever asks: the composer offers it (optional, the person's words), Ask Daoris's `ask_propose`
writes one, and a list shows it as a quest's. An ask with none heads with its first line, clamped. No screen rewrites a
title (the platform language §4).

**Buttons hold still** (HEAD2a). The head is a grid by the **main area's** width, not the window's:

- **Above 640 px of main area**: the title column flexes and the acts column is fixed, aligned to the title's first line; a
  long title wraps under itself and never moves the buttons.
- **At 640 px or less** (a 680 px window's main area is about 596): the acts take their own right-aligned row under the
  facts.
- **Order**: the primary act, the secondary, then ⋯. A quest: *Take* or *Mark done*, *Decline…*, ⋯; at the narrow width
  *Decline…* folds into the ⋯ first. A session: its state's loud act where it has one, *Stop…* while live, ⋯; *Stop…* stays
  and folds only after the primary has the row. The ⋯, the buttons and the right-click stay one list (CTX1).

**Pills and facts** (HEAD2b, HEAD2c). The state's pill leads the title, always with its text. *Held* and *Unreviewed* move to
the facts line, first, their sentences staying in their cards. A quest's facts: where it goes, its age, and one live fact
(the requester, the lanes, ids and exact times go to *Details*). A session's: its repository, its age, and one fact its
state makes matter; the account moves to *Details* unless it explains a hold. At 680 px the facts may take two lines; a
lower fact is left out before a live one is.

```text
 Quest, 1546                                        Quest, 680
[Open] Repair streaming hydration   [Take] [Decline…] [⋯]   [Open] Repair streaming hydration
       budget for the sync look                              budget for the sync look
to engine · 4 h ago · a session is starting here            to engine · 4 h ago
View full request ↓                                         a session is starting here
──────────────────────────────────────────────────                               [Take] [⋯]
the request, as written…                                    View full request ↓

 Session, 1546 (sticky)                             Session, 680 (sticky)
[Working] Repair streaming hydr…          [Stop…] [⋯]       [Working] Repair streaming hydr…
engine · 12 min · try 4                                     engine · 12 min · try 4
──────────────────────────────────────────────────                              [Stop…] [⋯]
conversation, waiting cards … Details ▸ (the request)
```

## 3. Considered and rejected

- **Ask/Code modes on the composer** (Codex's own). They would imply a conversation cannot change files, or that *Code* makes
  governed work; the quiet *Make it a task with Daoris…* hands it to the door that makes asks.
- **A kind picker or a branch picker in the composer.** Kinds are the workspace's (D157), and switching the person's checkout
  is never done for them.
- **Keeping only Ask Daoris as the door that takes words** (D161's rejection of a second entry point). The owner asked for a
  start that names its repository; Ask Daoris keeps every other start, and its go fills this composer.
- **A first-clause rule on the page for short titles.** Titles are content, shown as written; a short title is the
  publisher's words (SESSUX1j), and an ask's will be the asker's.
- **An expanding head** (*Show all* in place). It moved the buttons and pushed the conversation down.

## 4. The build

| Row | What | Where |
|---|---|---|
| NEW1a | `START_CHAT` with opening words, files and a request id; the tree choice said exactly | driver, modules |
| NEW1b | The composer in Sessions, its three chips, Enter, the window's *New conversation* | web |
| NEW1c | *Add a repository…* / *Import a folder…* from the picker, the draft kept | web |
| NEW1d | Ask Daoris's go fills the composer; *Make it a task with Daoris…* hands the draft over | driver, web |
| HEAD2a | `PageHead`'s grid by the main area's width; the acts' row, order and fold | web |
| HEAD2b | A quest's head: *View full request*, pills to facts, facts trimmed | web |
| HEAD2c | A session's head: one line, `WholeTitle` gone, the request in *Details* | web |
| HEAD2d | An ask's head as a quest's | web |
| HEAD2e | An ask's short title: the service's field, the composer, `ask_propose` | service, web, driver |

Each is proved by its tests and stories at 1546 and 680 px in en and zh, then on the installed window; the first window look
waits for an idle install (the owner was using it when this was written).

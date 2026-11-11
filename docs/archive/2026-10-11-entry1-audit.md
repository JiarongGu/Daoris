# ENTRY1: what Ask Daoris can start, against what a person starts (audit)

**Carried by:** D161 §3. It records main at `aea10f74`, read from the code: what a person starts in the window,
and whether Ask Daoris can start it, can only take the person to its place, or neither. It is a record of that
commit, not a contract. What people ask Ask Daoris for and cannot start is not measured here: the code cannot show
it, and D161 §3 asks for that measure before a door is built.

## §1 The coverage table has a blind spot

`HelpCoverageTests` (src/Daoris.Desktop/Daoris.Desktop.Driver.Tests/Help/HelpCoverageTests.cs) lists every control
with its door, its exemption or what it is owed. It takes its controls from two places only: the bridge hooks a Settings
domain or one of three places (`agents`, `projects`, `plugins`, line 1153) presses, and a ten-row `Elsewhere` list
(line 310). It never reads the page's own service calls (`queries.ts`), nor the Quests, Sessions, Overview, Knowledge and
Map views, nor the presses in `work/`, `asks/` and `help/`. So these starts are listed nowhere:

- **Quests and asks:** publishing, responding to, accepting and deleting a quest; asking, publishing, closing and
  deleting an ask (`queries.ts:251-259, 359-380`).
- **Repositories:** importing, registering, wiring and retiring one (`queries.ts:497-522`).
- **Review:** a review's verdict and set-up step, the ask's review choice, a person's own done, and answering a session
  (`queries.ts:414-437`).
- **Conversations and sessions:** starting, saying and ending; stopping, resolving, going on, archiving and deleting
  (`bridge/conversation.ts:308`, `bridge/sessions.ts:28-232`).
- **Trees:** landing, discarding and handing off (`bridge/trees.ts:381-441`).
- **The loop:** retry, nudge, trust a folder and sync now (`bridge/driver.ts:212-330`).
- **Work:** pause, resume and abandon (`bridge/work.ts:82-103`).

A start missing from Ask Daoris therefore fails no test. The table's reach is the first gap (ENTRY1a), since D161 §1
puts a rule that can be dropped into a mechanism.

## §2 What the table holds today

51 rows have a door: 21 verbs, 29 controls and 1 local. 53 are exempt: 4 verbs, 37 controls, 2 local and 10
elsewhere, beside 14 named constants for headless verbs. 18 are owed to a named row: 7 verbs, 10 controls and the
forms, beside 8 named constants. The exemptions keep their reasons: what an agent may do is never proposed (PERM2); a
sign-in, a key, a name and a discard are the person's (D89, D66); a second opinion spends an account at the person's
choice. Read against "start any task", the exemptions that begin work are already routed (a new plugin goes through
`ask_propose`, PLUG9), or move the person's own words on purpose (D133 §1, D143 point 4).

## §3 The starts, and what Ask Daoris does with each

| A person starts | Ask Daoris today |
|---|---|
| A task in words | Starts it: `ask_propose`, which the intake turns into quests (D65); it carries the sentence, workspace and why only |
| A quest published by hand; an ask published or closed | Neither: intake publishes quests from an ask, and `go` reaches only the Quests view |
| A conversation about work | Neither: the person's own words (D133) |
| A parked quest tried again | Starts it: the `setting` kind's retry door |
| A repository added or imported | Goes there only (`projects` parts `add`, `import`) |
| A repository or a workspace set up | Goes there only; its doors are owed (LAYOUT8, WSSETUP7) |
| An account or key added | Goes there only, by design (D89) |
| A plugin installed; tried; made | Installs it; trying is the person's; making goes through `ask_propose` |
| Work landed; handed; synced; a departure accepted | Landing neither (a verdict is the person's, D154 point 8); hand, sync and accept start it |
| A review's verdict; a second opinion | Neither; where each runs is a `setting` door |
| A session paused, archived or deleted; history cleared; a workflow saved | Owed doors, each recorded with its row |
| A setting | Starts it (20 `setting` doors, browser, agent, account), or goes there; theme, language and update are the viewer's |

## §4 The gaps

Each is a gap Ask Daoris can neither start nor reach, with what it would mirror. None is measured as wanted yet.

1. **The table's reach** (ENTRY1a): read `queries.ts` and the `work/`, `asks/`, `help/`, Quests, Sessions and Overview
   presses, so each start has a door, an exemption or an owed row, and a new one fails the test until it does.
2. **No place below Quests, Sessions or Overview** (ENTRY1b): "where do I answer what needs me?" reaches only the view.
   Mirror the go twin's parts (`HelpGoProposals.cs:115-130`, `help/places.ts`).
3. **`ask_propose` carries no review choice, kind or workflow** (ENTRY1c), though the composer takes them
   (`api.ts:508-513`). Mirror the service's `KnowledgeTools.Help.Ask.cs` and the driver's `HelpAskProposals.cs`.
4. **A repository added or imported as an applied proposal** (ENTRY1d): today `go` only. The folder stays the person's
   pick, since a path never crosses to a page (D48 §3/§7). Mirror the `plugin` kind's `add`.
5. **Set-up doors**: owed already (LAYOUT8, WSSETUP7); no new row.
6. **For the owner** (decisions, not builds): whether Ask Daoris may propose publishing a quest by hand when D65's
   intake already publishes from an ask; whether a landing may be proposed and applied, against D154 point 8's
   "a verdict is the person's"; whether trusting a folder stays the person's press alone. The owner answered the same
   day (D161's ENTRY1 note): a quest or a session keeps its own conversation, so none of these becomes an Ask Daoris
   proposal; Ask Daoris takes the person there.

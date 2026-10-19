# Ask Daoris's access modes: the screen, restricted mode's grants, and what was handed (proposed, 2026-10-19)

D183 decides the modes. This design is what HELPMODE1b (the page), HELPMODE1c (restricted mode's configured access) and
HELPWIRED1 (what was handed) build from. It was drafted from a read-only opinion by Codex the same day, checked against
the scout's map of the start. Its harness claims are labelled as claims until a row measures them on the installed pins.

## 1. Three things kept apart

- **The machine's selected mode**: what the person chose (`helpMode`, restricted or full).
- **The conversation's effective mode**: what its harness actually runs under. It can differ from the selected mode when a
  switch waits for the next start, or when a harness does not offer the mode (then the start fails plainly; D183's
  HELPMODE1a1 row).
- **Its recorded wiring**: what the start handed, kept as a receipt (HELPWIRED1).

The panel's chip shows the effective mode. A pending switch reads *Restricted · full access next start*.

## 2. What was found at the start (the scout's map and Codex's read)

- The native Claude door hard-codes `acceptEdits` for every chat, Ask Daoris included. HELP1a's asking mode reached the
  ACP door only. On the native door the allow-list held only because an unlisted tool is never allowed over `-p`.
- Codex over ACP is handed no Claude permission rules at all; its posture is its whole restriction.
- A posture a harness does not offer is left alone today, silently. Under full that would run restricted under a full
  label, so a full start fails plainly instead.
- *Claims to measure:* Claude's bypass still honours deny rules and blocking hooks, so Daoris's own guards may still stop
  housekeeping under full. Claude's ACP adapter allows bypass only when the session is created or loaded with it. Codex's
  ACP adapter stores a mode for later prompts and does not change a running turn.

## 3. Full access per harness

| Door | Full | Switching a running conversation |
|---|---|---|
| Claude, native (`claude-code`) | `--permission-mode bypassPermissions` | Not built; the next start or resume (a stream control request is unmeasured) |
| Claude over ACP (`claude-code-acp`) | `session/set_mode` `bypassPermissions`, from the offered modes | The adapter supports it; Daoris sends a mode only at open and resume today (HELPMODE1a2) |
| Codex over ACP (`codex-acp`) | `agent-full-access` (full-access sandbox, approvals never) | Stored for the next prompt |
| dsh, plugin-declared harnesses | none yet: a full start is refused, naming the harness | — |

Full removes the harness's own asking and its sandbox. It grants no operating-system privilege, no missing credential and
no server that is not running.

## 4. Restricted mode's configured access (HELPMODE1c)

Each grant is a typed capability Daoris enforces, never a widened permission mode. HELP1a showed that an allow-list is no
ceiling in a mode that judges its own actions, so no grant switches restricted to `auto`, `acceptEdits` or bypass. Nor is
any grant a rule like `Bash(daoris-driver *)`, an interpreter, or a generic execution tool.

1. **Daoris's housekeeping.** Inspect and clean Daoris's own trees, branches and recovery material through typed driver
   tools that use the existing cleanup judges. Targets are bound by their recorded identity and owner and checked again
   before acting. No arbitrary command and no path.
2. **The browser.** D170's exact server approval and its individually chosen writes, confined to Daoris's browser
   connection. The screen says signed-in pages may be affected. Claude takes exact MCP rules; another harness needs the
   tools filtered or mediated so the grant holds.
3. **Reading checkouts.** The workspace's checkouts intersected with D107's readable set. Claude takes `Read` and the two
   git prefixes. Another door needs a scoped read tool, since Claude's rule strings confer nothing there.
4. **Chosen settings without Apply.** Named ordinary settings through the existing validation and writer, as a typed tool
   with an outcome record. The mode, the grants, credentials and any authority change stay out.
5. **A confined shell, later.** Only with an enforced file-system and process boundary around chosen Daoris trees, away
   from account stores and sensitive home data. A working folder, a `cd` prefix or an instruction is not confinement.

### 4.1 Each grant's contents, read from the code (2026-10-19)

From a read-only Codex read of the code, its key claims checked against it. The screen shows **specific operations, each
with whether this agent can be held to it**, not five blanket switches. No grant exists yet: today's proposal tools still
need Apply.

| Grant | What it reaches | Its operations | Default | Claude (both doors) | Codex |
|---|---|---|---|---|---|
| Housekeeping | Daoris's own work: only recorded targets its cleanup judges permit | inspect (one group); remove landed, empty or carried session branches and trees (`TreeSweeps`, `LandedCleanups`); discard a named session branch (`SessionBranchDiscard`; a running or waiting session keeps its own; no force); delete recovery refs nothing holds (`RecoveryDropping`), each chosen on its own | off | typed tools Daoris serves and enforces | the same: held, since Daoris enforces it |
| Browser | Daoris's browser connection, signed-in pages included | the server's reads approved together; each write (click, type, press a key, fill a form, choose an option, check, drag, upload, answer a dialog) on its own; no page code | needs approval (`HelpGrants`, `plugin-approvals.json`); changed since approved is said apart | exact `mcp__browser__<tool>` rules | the whole server or none: a subset is **unavailable on this agent** until mediated |
| Checkouts | the workspace's checkouts within D107's readable set | read files; `git status`; `git branch --list` (never log, diff or show: their output options write files) | on, inherited (repository, workspace, then on) | `Read(//<checkout>/**)` and the two git prefixes | **unavailable as a scoped grant** until a scoped read tool exists |
| Chosen settings | only the settings chosen, through the existing validation and writer, with an outcome record | `notify`, a session's `language`, `timeout`, `cap`, `cooloff`, each chosen on its own; never the mode, the grants, accounts, plugin approval, access or across, standing, review, opinions, helpers, commits, landing, driving, or a safeguard | off | a typed tool Daoris enforces | the same |
| Confined shell | later | — | off | unavailable | unavailable |

**Housekeeping is built first**, narrowly: inspection, and removing branches and trees the judges have cleared, without
force or recovery deletion. It is what D183's own trigger asked for, and Daoris enforces it, so it holds on every agent.
*Built 2026-10-20 (HELPMODE1c2a, 1c2b):* the channel, the record and the screen's row are D183's note of that day.

**The traps:** a grant made to work by widening the harness's mode; grants read once and reused on a resume (each start
composes them again, each act is judged at execution, each start leaves its receipt); a typed tool that takes a path
(it takes a recorded identity, checks the owner, and judges again before acting); and a grant shown as in force where
the agent cannot be held to it (*What it has* keeps the start's receipt; the screen shows a change as pending).

*Drawn 2026-10-20 (HELPMODE1c1, a mock):* the states read *off*, *on*, *needs approval*, *changed since approved*, *unavailable
on this agent* and the shell's *not available yet* (关闭, 开启, 待批准, 批准后已改动, 这个智能体不可用, 尚未提供); *approved* is
批准, the plugins page's word, so no glossary term was added. An agent is held to the browser and the checkouts by whether it takes
permission rules, so on Codex the whole browser row is unavailable, its reads included, since the server goes whole or not at all.
The molecule is `help/ConfiguredAccess.tsx`, not `HelpGrants`, the driver's plugin approvals. *Left for the owner:* the browser's
boxes here and its plugin page's approval are one record drawn in two places.

*Decided 2026-10-20, on the owner's review of the mock:* the screen stands as drawn. A grant that configures a plugin is set
on that plugin's own tab, the one place its record is changed: the browser's reads and writes are approved on its plugin page,
where `HelpGrants` already keeps them (HELPPLUG1c). *Configured access* shows the browser row's state from that record, read
only, and leads to the plugin's tab to change it; it never draws a second set of boxes (HELPMODE1c3). Settings also gains a
*Plugins* section listing each installed plugin's settings and leading to its tab, at a lower priority (PLUGSET1).
Housekeeping, the checkouts and the chosen settings are Daoris's own and stay set here.

## 5. The screen (HELPMODE1b)

The picker sits beside the composer, the docked panel included. History keeps its own head. The text is translated, the
warning is coloured, and every control has keyboard access.

```text
Ask Daoris · games
[Restricted ▾]                    [What it has]
  Restricted — configured access
  Full access — acts without Apply
  Configure restricted access…
```

Choosing full asks once, inline: *Allow Ask Daoris to run commands, edit files and use every installed plugin server
without Apply or routine permission approval? This choice is the machine's: it affects future starts and supported
continuations across workspaces.* It shows the servers full access would add, and when it takes effect. The buttons are
**Enable full access** and **Never mind**. Nothing asks again per call.

```text
Settings → Daoris's own AI → Ask Daoris
Agent   [Claude…]
Access  [Restricted ▾]       This machine's choice
Configured access:
[ ] Daoris's housekeeping   [Choose operations]
Browser   [Approved reads; 2 writes] [Manage]
Checkouts [Workspace permissions]    [Manage]
[ ] Chosen settings         [Choose settings]
Shell     [Not available on this agent]
```

## 6. What it has (HELPWIRED1)

The panel's *What it has* reads the start's receipt, recorded at the start and never worked out again. A receipt holds:

- the effective mode;
- the harness and its version, and the account's label;
- the working folder;
- Daoris's tools and the skills;
- the servers and their tools, with each server's approval;
- the permission rules and the checkout grants.

It marks each item **handed**, **withheld**, **handed by full access without a restricted approval**, or **loading
unverified**. Each missing capability links to the door that grants it: its grant, its plugin's update, or its host. A
resume or a change of effective mode records a new receipt, and the earlier ones are kept. A receipt is desktop-only:
secrets in arguments or environment and machine paths in rules or errors are cleaned before it is shown, and it never
travels on a shared host or a feed.

## 7. The traps, each a test

| Trap | Test |
|---|---|
| A switch mid-conversation claims what did not happen | Hold a running tool and queued words; switch both ways; assert the boundary, the pending chip, a failure's handling and the receipt |
| A resumed conversation keeps the old authority | Restart, change the mode or a grant, resume both ways; the new policy is in force before the first resumed action and is announced; older receipts unchanged |
| The room's words and the real mode disagree | Compare the room, the spawn's arguments, the accepted ACP mode and the receipt; try an action off the grant; an unsupported mode fails plainly |
| Full's extra servers leak into restricted | One catalogue holding unapproved, changed and unavailable servers: restricted withholds them, full includes them deliberately, each status honest |
| The receipt leaks | Seed secrets in arguments and environment, paths in rules and errors; assert they are cleaned and never on a shared host |
| A headless or remote window claims the browser | No browser host, and no desktop attached: the list says withheld, never an unresolved endpoint |
| A workspace's label reads as confinement | Two workspaces in two windows; under full, the room says the shell can reach beyond the workspace; the doctrine stays words |

## 8. Not built

Modes per workspace (D158's ASKSCOPE1 note, point 7). Another approval engine. A raw rule editor. Wiring worked out again
from configuration. Automatic escalation. A shell called confined that is not.

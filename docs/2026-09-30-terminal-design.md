# The terminal: a place to type in the console panel (CONSOLE4)

**Status: the contract for CONSOLE4a–c, recorded as D96 before any code.** The owner, 2026-09-30:
*"we also need to make input line for console too, so we can control console just like regular
console window (more into powershell style)"*. It amends the interactive design's console
(`2026-09-20-interactive-design.md` §2), which shows what a session prints and takes no input.

## 1. What is there

The console panel holds each session's streams, read-only: what the session prints, its subagents'
and its background tasks' lines (CONSOLE2, CONSOLE3). A person speaks to a session through the
composer, never through its console. Nothing in the window runs a command of the person's own, so a
`git status`, an `npm test` or a `daoris` command means leaving Daoris for another window.

## 2. What it becomes

**A terminal view in the panel**, beside the console: a real shell under a Windows pseudo-console
(ConPTY), drawn by a terminal renderer in the page, so prompts, colours, Ctrl+C, history, tab
completion and full-screen programs behave exactly as they do in a console window. It is a view like
the others (`work/placements.ts`): it lives in the panel by default and moves to the side bar as they
do.

- **The shell**: PowerShell 7 (`pwsh`) when it is installed, else Windows PowerShell; a choice of the
  others the machine has (Command Prompt, Git Bash where found), each tab its own process, closed
  with it.
- **Where it starts**: the attended session's tree when there is one, else the workspace's first
  repository's root, else the home. The tab says where.
- **Its environment**: the person's own, plus the install's `DAORIS_HOME`, so the `daoris` CLI in it
  answers for this machine, as a terminal outside would once the account's variable is set (D63).
- **More than one**, each a tab in the view, with the shell's name and where it started.

**A session's own streams stay read-only.** The console shows what a session prints; the composer is
how a session is spoken to (D52: the driver answers no permission prompt, and a keystroke into an
agent's stream would be one). The terminal is the person's own shell, never a session's.

## 3. How it travels

Desktop-only (D47 §4): the keystrokes and the output ride the bridge, never HTTP, and a browser shows
no terminal at all.

| Bridge | Carries |
|---|---|
| `DAORIS.TERMINAL` · `OPEN` {shell?, cwd?} | a new terminal: its id, the shell and where it started |
| `DAORIS.TERMINAL` · `INPUT` {id, data} | what the person typed, as the renderer encodes it |
| `DAORIS.TERMINAL` · `RESIZE` {id, cols, rows} | the view's size in cells |
| `DAORIS.TERMINAL` · `CLOSE` {id} | the tab closed: the process tree ends with it |
| event `TERMINAL_OUTPUT` {id, data} | what the shell wrote, batched on a short window as the console's lines are |
| event `TERMINAL_EXITED` {id, code} | the shell ended on its own |

Every terminal ends when the application does. A terminal's words are the person's own and never go
into the machine log (D94).

## 4. Build order

1. **CONSOLE4a**: the pseudo-console in the driver library (P/Invoke to `CreatePseudoConsole`, the
   process started attached to it, output pumped, resize, the process tree ended on close), tested
   against `cmd /c echo` and a script that reads a line; the `DAORIS.TERMINAL` module.
2. **CONSOLE4b**: the terminal view in the page on a terminal renderer (`@xterm/xterm` and its fit
   addon), one tab first, in the panel; looked at on the window in both themes.
3. **CONSOLE4c**: more than one, the shell's choice, where it starts, and its tab's name.

## 5. Not chosen

- **A line-only input without a pseudo-console.** A prompt that asks a question, a progress bar and
  any full-screen program would all break; *just like a console window* is the requirement.
- **A native terminal library.** The pseudo-console is a few Win32 calls; a package that wraps them
  is one more thing an install carries.
- **Typing into a session's stream.** The composer is that door, and a keystroke into an agent's
  input could answer a prompt the driver refuses by construction.

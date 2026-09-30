# The terminal: a place to type in the console panel (CONSOLE4)

**Status: the contract for CONSOLE4a–c, recorded as D96 before any code; all three built
2026-09-30 (§4), the window's look still to take in both themes.** The owner, 2026-09-30:
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
| `DAORIS.TERMINAL` · `SHELLS` | the shells this machine has, and the default (added building CONSOLE4a, for 4c's choice) |
| `DAORIS.TERMINAL` · `OPEN` {shell?, cwd?, cols?, rows?} | a new terminal: its id, the shell and where it started |
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
   against `cmd /c echo` and a script that reads a line; the `DAORIS.TERMINAL` module. *Built
   2026-09-30* (`PseudoConsole`, `TerminalShells`, `TerminalModule`). What building it settled:
   - **The shell is started suspended and joined to the session processes' job** (`ProcessJob`)
     before it runs, so a child it starts detached is in the job from its first instruction and ends
     with the tab; a test starts one from `cmd` and watches its heartbeat stop.
   - 🔴 **The standard handles are named, as none.** Started from a process whose own handles are
     redirected (the test runner; a service would be the same), the shell wrote to *those* and the
     console showed only its first frame. Measured: `cmd /c echo` printed nothing until
     `STARTF_USESTDHANDLES` was set with no handles.
   - **An end is told once, after the last output.** The console host outlives its shell and holds
     the output pipe open, so the shell's exit closes the pseudo-console, which flushes its last
     frame and breaks the pipe; `TERMINAL_EXITED` goes out once the reader has read to the end. A tab
     the page closed tells no exit: the page already knows.
   - **Output is batched on 16ms, not the console's 120ms**: here the output is a keystroke's echo,
     and a tenth of a second between a key and its letter reads as a slow terminal.
   - **Where it starts is the page's to say** (`cwd`), since the attended session and the workspace in
     scope are the viewer's; the module opens in the home when the folder is not on this machine, and
     answers where it did open, so a tab never names a place it is not.
   - **The shells**: `pwsh`, else `powershell`, then `cmd` and Git Bash, each found by the plugin
     door's resolver (`CommandPresence`); Git Bash is the `bash.exe` beside `git`, never the first
     `bash` on PATH, which on Windows is usually WSL's launcher.
   - **Refusals**: a shell nobody offers, one this machine lacks, none at all, and one the system would
     not start, each a code in `Refusals` with the system's own reason carried for the last. A
     keystroke or a resize for a terminal that has gone is answered quietly: a toast per key would be
     noise, and the page already knows it ended.
2. **CONSOLE4b**: the terminal view in the page on a terminal renderer (`@xterm/xterm` and its fit
   addon), one tab first, in the panel; looked at on the window in both themes. *Built 2026-09-30*
   (`work/TerminalView.tsx`, `useTerminals`); **not yet looked at on the window**, which the build
   session could not open beside the owner's running install. What building it settled:
   - **A view like the others** (`placements.ts`): `terminal`, in the panel after the console, moved
     and dragged as they are, and absent where no shell handed it in (`WorkFrame`'s `terminal`, as
     `ask` is).
   - **The terminals are held by the Work frame, not by their view** (`useTerminals` in `shell.ts`):
     the view unmounts whenever it moves or another view of its region is shown, and a shell must
     outlive that. Output that comes while no screen listens is held, bounded, and handed to the next
     that does; each screen is kept beside the view and moved, not reopened, into wherever it is shown
     next, with its scrollback. An output batch can beat its terminal's own `OPEN` answer, so what is
     held is keyed by id before the page has heard of it.
   - **Shown with none open, it opens one**, once per showing: a person who closed the last one has
     closed it. A second open waits on the first, so strict mode's doubled effect opens one shell.
   - **Where it starts is the page's choice** (§2): the attended session's tree (a record with none
     works in its repository's root), else the first repository of the workspace in scope with a
     checkout here, else nothing said and the module's own answer, the home.
   - **Its colours are the tokens'** (`work/terminalTheme.ts`): the page, the ink, the accent as the
     cursor, and the four status colours as red, green, yellow and blue, validated in both themes;
     magenta and cyan are halfway between two of them; the greys turn round with the theme. A test
     keeps the file free of any colour of its own. It follows a theme chosen in Settings (tested) and
     the system's own when that is the choice (heard from the media query; no test reaches it).
   - **Keys as a Windows console and VS Code have them**: Ctrl+C copies a selection and interrupts
     without one, Ctrl+V pastes, and the frame's own keys (`shortcuts.ts`, now one list with the
     application's handler) go to the frame rather than the shell.
   - Ask Daoris's room names the terminal among the views, and says it is the person's, never a
     session's.
3. **CONSOLE4c**: more than one, the shell's choice, where it starts, and its tab's name. *Built
   2026-09-30* (`work/TerminalTabs.tsx`), and like 4b not yet looked at on the window:
   - **A row of tabs at the view's top**, each its own shell and process, named by its shell and the
     folder it started in (*PowerShell · engine*), the whole path in its tip; a name that repeats is
     numbered in the order the terminals opened, so a name already on a tab never changes.
   - **The shown tab's close after the tabs**, as the console's stop is (CONSOLE3a): a tab list owns
     tabs and nothing else. A middle click closes any tab. Closing ends the process tree, and the tab
     beside it is shown, as a browser's are. The shown tab is held with the terminals, so a view
     shown again shows the same one.
   - **"+" offers only the shells the machine has** (`SHELLS`, asked once), the default first and
     marked; with one shell there is nothing to choose, and "+" opens it. A new terminal starts where
     the frame says a terminal starts *now*, so one opened after attending another session starts in
     that session's tree.
   - **An ended tab says so** in its mark and its name, and keeps its screen until it is closed or
     started again.

## 5. Not chosen

- **A line-only input without a pseudo-console.** A prompt that asks a question, a progress bar and
  any full-screen program would all break; *just like a console window* is the requirement.
- **A native terminal library.** The pseudo-console is a few Win32 calls; a package that wraps them
  is one more thing an install carries.
- **Typing into a session's stream.** The composer is that door, and a keystroke into an agent's
  input could answer a prompt the driver refuses by construction.

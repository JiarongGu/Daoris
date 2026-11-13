# `daoris-in-app-browser` — sessions drive Daoris's own browser

Daoris's own plugin, in the `daoris-` family (a plugin's id is `<family>-<name>`; yours takes your own family). It was
`in-app-browser`; its manifest says so, and Daoris moves that id's switch, data and approval to it once.

The desktop shell carries a browser of its own (D78,
`docs/archive/2026-09-27-in-app-browser-design.md`): a window with an address bar, its profile under the
Daoris home, and no way into the application's own page. This plugin hands every session the
[Playwright MCP server](https://github.com/microsoft/playwright-mcp), attached to that window over
the Chrome DevTools Protocol. So what a session opens, clicks and reads is the page the person
can see.

```sh
daoris plugin add examples/plugins/daoris-in-app-browser
daoris agent rules allow mcp__browser            # or --workspace <name> for one workspace
```

**Sign in once, in the window.** Open it from View → *Daoris's browser* (or the title strip's *Open Daoris's
browser*), go to the ticket system, and sign in. The sign-in stays in the browser's profile, and
every session after uses it. Settings → Browser chooses whether that window is Daoris's own or your Edge on a
profile of Daoris's; `${browser}` is the chosen one's endpoint. Signing in is the person's to do, never a session's.

**One profile for every session; each session's tabs are its own.** `browser-server.mjs` starts Playwright MCP over the
browser's one context, the person's, so every session (Ask Daoris included) has the person's cookies, local storage and
IndexedDB, shared both ways, exactly as the person's own tabs see them. Playwright MCP would adopt every page that context
holds, make the person's first tab its current one and adopt every page another session opens, so the server hands it a
scoped view (`scoped-context.mjs`): it lists only the tabs the session opened and their popups. A session never takes,
navigates or closes the person's tabs or another session's, and ending it closes only its own tabs.

**Isolation, where you set it up.** List the agents that get a context of their own in `isolation.json` in the plugin's
data folder: `{ "isolated": ["codex", "role:reviewer"] }`. A word is the start of the agent's MCP client name (`codex`
matches `codex-mcp-client`), `role:<role>` the role the session took, `*` every agent. Nothing set, or a file that cannot be
read, means everyone shares the one profile. An isolated agent's context starts with the person's cookies and the local
storage of each origin the person has a page open on, and nothing goes back; its session storage and IndexedDB start empty.
Its context closes when the session ends. A named profile that keeps an agent's sign-ins is not built.

The first start installs the pinned Playwright MCP under the plugin's data folder (`runtime/`) with `npm`, once.

**Ask Daoris uses it once you approve it.** The server is offered to Ask Daoris too (`"to": ["sessions",
"help"]`), and Ask Daoris is handed it only once you approve its exact tools on the plugin's page
(Plugins → *In-app browser* → *Ask Daoris* → *Approve*), or press Apply on Ask Daoris's own proposal of
it, or run `daoris plugin approve daoris-in-app-browser browser`. Its reads (`allow`: navigating,
reading a page's snapshot, a screenshot, its tabs, its console and its requests) are approved
whole; each write (`writes`: clicking, typing, filling a form, choosing, dragging, uploading a file,
answering a dialog) only as you tick it, since it acts on a page as you. The tools that run a
page's own code (`browser_evaluate`, `browser_run_code_unsafe`) are offered to no one here: Ask
Daoris is never handed a generic execution tool (HELP4). An update that adds or widens a tool asks
you again.

`${browser}` is the in-app browser's endpoint. The driver fills it in when it hands a session its
servers, and brings the window up first, without taking focus. Where no shell answers (the headless
`daoris` with no window, or a gate), the server is not handed, and the session's transcript says why. For a
machine that runs without the shell, the `browser` example launches a browser of its own instead.

## What it needs

- **Daoris's desktop**, which carries the browser and fills `${browser}`; a headless Daoris
  hands a session no such server.
- **node** on the PATH, with `npm`: the server fetches `@playwright/mcp@0.0.82` the first time a session is handed it.
- **A rule that lets sessions call it**: `daoris agent rules allow mcp__browser`, for the machine or
  with `--workspace <name>`.
- **A sign-in, once, in the browser's window**, for a page that sits behind one.

## One of two

Both examples name their server `browser`, so one allow rule covers either, and a machine installs
one of them. The second to claim the name contributes nothing, and `daoris plugin list` says so.
The version is pinned to the one this was measured against (0.0.82); `browser-server.real.test.mjs` proves the server on a
Chromium it launches itself (`DAORIS_REAL_BROWSER=<executable>`). `--output-dir` keeps the
server's snapshots under the plugin's data folder, out of the session's tree.

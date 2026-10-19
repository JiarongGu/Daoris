# `in-app-browser` — sessions drive Daoris's own browser

The desktop shell carries a browser of its own (D78,
`docs/2026-09-27-in-app-browser-design.md`): a window with an address bar, its profile under the
Daoris home, and no way into the application's own page. This plugin hands every session the
[Playwright MCP server](https://github.com/microsoft/playwright-mcp), attached to that window over
the Chrome DevTools Protocol. So what a session opens, clicks and reads is the page the person
can see.

```sh
daoris plugin add examples/plugins/in-app-browser
daoris agent rules allow mcp__browser            # or --workspace <name> for one circle
```

**Sign in once, in the window.** Open it from View → *Browser* (or the palette's *Open Daoris's
browser*), go to the ticket system, and sign in. The sign-in stays in the browser's profile, and
every session after uses it. Signing in is the person's to do, never a session's.

**Ask Daoris uses it once you approve it.** The server is offered to Ask Daoris too (`"to": ["sessions",
"help"]`), and Ask Daoris is handed it only once you approve its exact tools on the plugin's page
(Plugins → *In-app browser* → *Ask Daoris* → *Approve*), or press Apply on Ask Daoris's own proposal of
it, or run `daoris-driver plugins approve in-app-browser browser`. Its reads (`allow`: navigating,
reading a page's snapshot, a screenshot, its tabs, its console and its requests) are approved
whole; each write (`writes`: clicking, typing, filling a form, choosing, dragging, uploading a file,
answering a dialog) only as you tick it, since it acts on a page as you. The tools that run a
page's own code (`browser_evaluate`, `browser_run_code_unsafe`) are offered to no one here: Ask
Daoris is never handed a generic execution tool (HELP4). An update that adds or widens a tool asks
you again.

`${browser}` is the in-app browser's endpoint. The driver fills it in when it hands a session its
servers, and brings the window up first, without taking focus. Where no shell answers (the headless
`daoris-driver`, or a gate), the server is not handed, and the session's transcript says why. For a
machine that runs without the shell, the `browser` example launches a browser of its own instead.

## What it needs

- **Daoris's desktop**, which carries the browser and fills `${browser}`; the headless `daoris-driver`
  hands a session no such server.
- **node** on the PATH, with `npx`: the server is started as `npx -y @playwright/mcp@0.0.82`, which
  fetches it the first time a session is handed it.
- **A rule that lets sessions call it**: `daoris agent rules allow mcp__browser`, for the machine or
  with `--workspace <name>`.
- **A sign-in, once, in the browser's window**, for a page that sits behind one.

## One of two

Both examples name their server `browser`, so one allow rule covers either, and a machine installs
one of them. The second to claim the name contributes nothing, and `daoris plugin list` says so.
The version is pinned to the one this was measured against (0.0.82). `--output-dir` keeps the
server's snapshots under the plugin's data folder, out of the session's tree.

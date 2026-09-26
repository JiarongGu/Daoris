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

`${browser}` is the in-app browser's endpoint. The driver fills it in when it hands a session its
servers, and brings the window up first, without taking focus. Where no shell answers (the headless
`daoris-driver`, or a gate), the server is not handed, and the session's transcript says why. For a
machine that runs without the shell, the `browser` example launches a browser of its own instead.

Both examples name their server `browser`, so one allow rule covers either, and a machine installs
one of them. The second to claim the name contributes nothing, and `daoris plugin list` says so.
The version is pinned to the one this was measured against (0.0.82). `--output-dir` keeps the
server's snapshots under the plugin's data folder, out of the session's tree.

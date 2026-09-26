# `browser` — the smallest plugin that hands a session a tool

A plugin may **declare MCP servers** (`docs/2026-09-23-intake-design.md` §1f, D65): every session
the driver spawns is handed them beside Daoris's own knowledge host. This one declares the
[Playwright MCP server](https://github.com/microsoft/playwright-mcp), which is what makes *test it
in a browser* something a session can do — open the app, click through the change, read the page —
with no browser extension and nothing of Daoris's in the loop. The harness reference's own
browser-use providers are MCP servers of exactly this kind.

```sh
daoris plugin add examples/plugins/browser        # copies it under the home's plugins/
```

Nothing runs until a session starts. Over the protocol door the server rides `session/new` (ACP4);
over the pipe door the harness is given a file Daoris writes under its own home for that session —
never the repository's `.mcp.json`. A server handed is a tool available, not a tool approved. What
the session may **call** is its permission rules: Daoris's scopes, handed at spawn, together with the
repository's own, with a deny winning (D72). To let sessions drive a browser, allow the server:
`daoris agent rules allow mcp__browser`, for the machine, or with `--workspace`/`--repository`.

A server needs a `name` (what the agent calls it — tools arrive as `mcp__<name>__<tool>`), a
`command`, and may carry `env`. `${plugin}` in either is the plugin's install folder, and `${data}`
its data folder (`plugins/.data/<id>/`), which an update never touches (D77). Give a placeholder its
own argument — `"--user-data-dir", "${data}/profile"` — because an argument holding one is resolved
as a path. The knowledge host's name is refused, and two plugins claiming one server name leave the
second contributing nothing, named in `daoris plugin list`.

## A page behind a sign-in

A ticket system is usually behind a sign-in, and a session's plain fetch cannot pass one. This
plugin keeps its browser's profile in `${data}/profile`, so a sign-in made there once is still there
for every session after it: open that profile, sign in, and close it before a session starts (a
profile has one browser at a time). The sign-in is the person's to make, never a session's.

The server launches Chrome by default. On a machine without it, add `"--browser", "msedge"` (or
`firefox`) to the command in the installed copy's manifest. Two other shapes exist and are not the
default: `--cdp-endpoint <url>` attaches to a browser the person started with a debugging port, and
keeps its tabs and sign-ins; `--isolated` gives every session an empty profile, which is right for
testing an app and wrong for reading a ticket.

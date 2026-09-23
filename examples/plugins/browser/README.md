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
never the repository's `.mcp.json`. What the session may **call** stays the repository's own
allow-list (D37): a server handed is a tool available, not a tool approved.

A server needs a `name` (what the agent calls it — tools arrive as `mcp__<name>__<tool>`), a
`command`, and may carry `env`. `${plugin}` in either is the plugin's install folder. The knowledge
host's name is refused, and two plugins claiming one server name leave the second contributing
nothing, named in `daoris plugin list`.

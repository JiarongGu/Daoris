# The in-app browser — Daoris's own, driven over MCP

> Written 2026-09-27 at the owner's direction: *"we should build a browser mcp within the app
> (deekseek harness has plugin for in app browser)"*, then *"so instead rely on things like claude
> extension we can have our own built-in browser system (or use plugin to support this)"*. It
> amends D65 §2, which declined *"a browser of Daoris's own"*. The decision is **D78**.

## 0. Why the first answer is not enough

FG3 gave sessions a browser through a plugin: Playwright MCP on its own Edge profile under the
plugin's data folder. That works, but the person has to sign in inside a browser they never see
open. It opens only when a session calls it, closes with the session, and lives in a profile the
person has to open by hand from a command line. A harness's own browser extension (Claude in
Chrome) is the other answer the machine already had, and it serves one harness, from the person's
own browser, only while that browser runs with the extension connected. Neither is a browser that
belongs to Daoris, which the person can open, sign in to once, and watch an agent use.

## 1. What the references do

- **orca** embeds a real Chromium per worktree, with an address bar, history and devtools. Agents
  drive **that same browser**, same tabs, through its CLI (`orca snapshot`, `orca click`) over a CDP
  proxy. A sign-in lives in a persistent partition, or is imported from the person's browsers.
- **deepseek-harness** has an in-app browser (`ui-sidebar-browser`) that is an iframe for the person
  only: *"register no tool, prompt section, or Session event"*. Its model-driven browser is
  browser-use: Playwright MCP or Chrome DevTools MCP, launched isolated, or **attached over CDP to a
  browser the person started**, which keeps that browser's sign-ins.

Daoris takes orca's shape, one browser that the person and the agents share, in dsh's mechanism:
a standard MCP server attached over CDP. Nothing Daoris-specific speaks to the page.

## 2. Measured before designed (2026-09-27)

Playwright MCP 0.0.82 was started with `--cdp-endpoint` against the scratch shell's WebView2 debug
port. It listed the tab, navigated, took a snapshot with a clickable ref, and opened a second tab.
**A WebView2 is drivable by a standard browser MCP over CDP.** The same probe navigated the app's
own page away: a page that holds the bridge, under an agent's CDP, would hand that agent the
machine. That sets the first rule below.

## 3. The shape

1. **The browser is the shell's window, in its own WebView2 environment**, with its own user-data
   folder at `<home>/browser/profile` and **no bridge**. It is never the app's page, and the app's
   page never gets a debug port in a published build. Two environments with two folders are two
   browser processes, so a debug port on one reaches nothing of the other.
2. **It is opened like the monitor**: from View → *Browser*, from the palette, or by the driver when
   a session is handed a server that needs it. It is one framed window with an address bar,
   back, forward and reload, following the viewer's theme where a window can (WINDOW2 says where it
   cannot). One page per window in v1. A link that asks for a new window opens in the same one.
   **Amended by BRW4 (2026-09-28): tabs.** Several pages, a WebView2 each on the one environment,
   so each is a CDP target on the same port. A tab the person asks for goes last, a page's new window
   is a tab beside that page, handed back to it as its window so `window.opener` works, and closing
   the tab in front brings its right neighbour forward. Ctrl+T, Ctrl+W, Ctrl+Tab and Ctrl+L. **Which
   tab an agent drives is the agent's:** Playwright MCP takes the first page it finds as current, not
   the one in front (measured), and moves when it opens or selects one. 🔴 **A tab an agent opens over
   CDP has no window** (measured): it is a page in the browser process that nothing shows, so the
   person cannot watch it. BRW8 carries that.
3. **Its profile is Daoris's**, under the home (D63), and it persists. The person signs in there
   once, and every session after uses that sign-in. The sign-in is the person's to make, never a
   session's. **Amended by BRW10 (2026-09-28): the sign-in survives a restart.** An identity server's
   session cookie has no expiry, so it ends with the browser process, and WebView2 restores none.
   The browser keeps its session cookies at `<home>/browser/session-cookies.bin`, sealed to the
   Windows account (DPAPI), after every page and every 30 seconds. It puts back the ones it does not
   already hold before its first page loads. A file this account cannot open restores nothing and
   says so under the bar. Cookies with an expiry stay the profile's own.
4. **It listens for CDP on loopback**, on a port the shell picks free when the browser first starts
   and keeps for the process's life. Any process on this machine can drive it while it runs, which
   is the same exposure as a browser started with a debugging port. The design says so rather than
   hides it.
5. **What drives it is a plugin's choice** (D64). A server a plugin declares may carry `${browser}`,
   which is the in-app browser's CDP endpoint. It is expanded when a session is handed its servers,
   never when the manifest is read, because the endpoint exists only while the shell runs. The
   example plugin declares Playwright MCP with `--cdp-endpoint ${browser}`. A machine may declare
   Chrome DevTools MCP with `--browserUrl ${browser}` instead.
6. **The driver asks for the browser before it hands the server.** Where a session's servers carry
   `${browser}`, the driver asks the shell to bring the browser up, without taking the person's focus,
   and hands the server the endpoint the shell answers. Where there is no shell (the headless
   `daoris-driver`, a gate), the answer is none. That server is then not handed, and the session's
   transcript says why.
7. **Nothing loads into a host** (D64 §7 stands). The browser is Daoris's own code in the shell. The
   plugin declares a process that speaks CDP to it, and a plugin still cannot add a view.

## 3a. Favorites (BRW5, 2026-09-28)

The person's, never a session's: a star in the bar keeps the page in front, a bar under the address
holds them, and a menu lists them all. **Two doors** (D50): the window, and `daoris browser favorite
add|list|remove` in a terminal. So `<home>/browser/favorites.json` is a **twin** file: the CLI's
`browser.ts` and the modules' `BrowserFavorites.cs` read and edit it with their own code, each with a
test table the other matches.

```json
{ "favorites": [ { "url": "https://site.example/board", "title": "Board" } ] }
```

1. **No file is no favorites.** A file that is not a JSON object, or whose `favorites` is not a list,
   shows none and says why, and **an editor refuses to write over it**.
2. **A row's address follows the bar's rule**: trimmed; nothing with a space; a host with no scheme is
   HTTPS unless it is this machine (`localhost`, `127.0.0.1`, `[::1]`), then HTTP; only `http` and
   `https`, with a host and no user or password; the form kept is the parsed absolute address.
   `about:blank` is no page to keep. A row whose `url` fails the rule is skipped by a reader and kept
   by an editor.
3. **A title** is a non-blank string, or else the address's host.
4. **The file's order is the bar's.** Adding appends. Adding an address already kept keeps its place,
   and takes the new title if one was given.
5. **Removing** is by address, under the same rule.
6. **An editor keeps what it has no field for**, on the file and on each row.
7. The two sides were compared on ASCII addresses. A host outside ASCII was not, and may be written in
   two forms.

## 4. What is deliberately not in it

- **Downloads, devtools, history UI.** v1 was one page an agent and a person share; tabs came with
  BRW4. orca's pane is the reference for what comes after, when somebody asks.
- **A browser per session or per worktree.** One browser, one profile, one sign-in. Two sessions at
  once share it, which the cap of one on this machine avoids today. That changes when somebody runs
  two.
- **Cookie import from the person's browsers.** orca's feature, with its own exclusions. The
  person signs in once instead.
- **The app's page in the browser's reach.** Never, by construction (§3.1).

## 5. Build order

| Item | What lands | Proven by |
|---|---|---|
| **BRW1** | The browser window: its own environment and profile under the home, a loopback CDP port, the address bar, and View → *Browser* with the palette's door | module tests for the pure parts (the endpoint, the address rules), and a look on the window with Playwright MCP attached from outside |
| **BRW2** | `${browser}` in a plugin server, expanded at hand-over from the shell's answer, and the server withheld where there is none. The driver brings the browser up before the spawn. The example plugin moves to it | driver tests with a stand-in browser host, the twin tests for the placeholder staying unexpanded at read, and a stub session handed the endpoint |
| **BRW3** | The owner's machine: the plugin, the one sign-in in the in-app browser, `mcp__browser` allowed, and the first ask whose ticket the intake reads through it | the owner's run, FG5 |

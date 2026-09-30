# The in-app browser — Daoris's own, driven over MCP

> Written 2026-09-27 at the owner's direction: *"we should build a browser mcp within the app
> (deekseek harness has plugin for in app browser)"*, then *"so instead rely on things like claude
> extension we can have our own built-in browser system (or use plugin to support this)"*. It
> amends D65 §2, which declined *"a browser of Daoris's own"*. The decision is **D78**.
>
> **Amended by D84 (2026-09-28).** The window this describes, Daoris's WebView2 browser, is retired
> once a Chromium Daoris ships lands (BRW11), with the person's Edge always an option (BRW12). What
> stands is the seam, §3.4–§3.6: a CDP endpoint on loopback, `${browser}` in a plugin's server, and
> the driver asking for the browser before the spawn. `docs/2026-09-28-managed-edge-evidence.md` is
> why.
>
> **Amended by D85 (2026-09-28).** The Chromium is embedded, under Daoris's own windows, and hosts
> the app's page too (`docs/2026-09-28-chromium-host-design.md`).
>
> **Amended by CHR3 (2026-09-28): the browser is `daoris-browser`, the engine's own window.** The
> owner took the recommendation (*"lets do it now"*). What each section is now:
> - **§3.1:** a process of its own, because the engine's port reaches every page in its process. It
>   holds no control and no Daoris page, and the shell's process has no port.
> - **§3.2:** the window, its tabs, history, bookmarks, find, devtools and downloads are the engine's
>   own. View → *Browser* and the palette open it, a second press brings it forward, and it closes
>   with the shell.
> - **§3.3:** the profile is `<home>/browser/engine`, and the engine keeps a sign-in across a restart
>   itself (`PersistSessionCookies`). BRW10's sealed file is not used by it.
> - **§3.4–§3.6 stand:** a port picked free for the shell's life, `${browser}`, and the driver asking
>   first. That port is a relay onto the engine's own (CHR6), which calls a new tab a `page` where the
>   engine says `other`, so an agent's browser MCP can open tabs
>   (`docs/2026-09-28-chromium-embedding-evidence.md` §12, §17–§19).
> - **§3a stands, shown differently (CHR5):** `favorites.json` is still Daoris's, a twin with
>   `daoris browser favorite`. At each start `daoris-browser` puts it in a *Daoris* folder on the
>   engine's bookmarks bar, found by its id wherever the person moved it. The person's own bookmarks
>   are never touched.
> - **§3b is retired:** the engine keeps its own history, and `daoris browser history` is gone.
> - **A settings file, `<home>/browser/settings.json` (CHR7):** another twin
>   (`daoris browser extensions`, `BrowserSettings.cs`). It says whether other software's Chrome
>   extensions are offered, as the engine does, or refused before it starts.
> - **The person's Edge, as an option (BRW12):** the same file's `browser` field, set with
>   `daoris browser use` or in Settings → Browser. Edge runs on `<home>/browser/edge`, with its port
>   recorded so a restarted shell adopts it, and it needs no relay. Its sign-in survives Edge
>   restarting (BRW13): Daoris reads its session cookies over CDP while it runs, seals them to the
>   account in `<home>/browser/edge-session-cookies.bin`, and puts them back when it starts Edge again.
>
> **Amended by CHR8 (2026-09-30, D99): one Chromium.** `daoris-browser` is the application's own
> executable started with `--daoris-browser`, on Shenora 0.18's `ChromiumBrowserProcess`, instead of an
> executable on CefSharp with a Chromium of its own. What the reader sees does not change: the same
> window, profile (`<home>/browser/engine`), favorites folder, extensions setting and closing with the
> shell. §3.4 stands with one difference: the relay behind the port is the kit's now, and announces a new
> tab as a `page` as Daoris's did.
>
> **Amended by BRW7 (2026-09-30): a door on the strip, and links routed to it.** §3.2's doors gain a
> third, the app strip's, and the settings file a `links` field, with its terminal door. §3c says what
> was built and why the door is where it is.
>
> **Amended by BRW8 (2026-09-30): who is driving.** §3.2's *which tab an agent drives is the agent's*
> stands, and Daoris now says whose hands are on the browser, beside its door and in Settings →
> Browser. §3d says what it is read from.

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

## 3b. History and completion (BRW6, 2026-09-28)

WebView2 keeps a history of its own in the profile and offers no way to read it, so the window
records each page a tab finishes loading in `<home>/browser/history.json`, on this machine only. The
address bar completes from that and the favorites. **Reading and clearing are a twin** (the CLI's
`daoris browser history list|clear` and `BrowserHistory.cs`, each with the same reading table);
recording a visit and completing are the window's alone.

```json
{ "visits": [ { "url": "https://site.example/board", "title": "Board", "last": "2026-09-28T09:00:00Z", "count": 3 } ] }
```

1. **No file is no history**, and a file that cannot be read shows none and is not written over, as
   §3a says for favorites.
2. **A row** follows the favorites' address rule and title rule; one that fails is skipped. `last` is
   a time, the earliest there is when it is not one; `count` is a positive whole number, once when it
   is not one. **Most recent first.**
3. **A visit** to a page that is there counts it again and moves it forward; a new one is added. Past
   500 pages the least recent go.
4. **Clearing** empties the list and keeps what an editor has no field for. The window also asks
   WebView2 to forget its own history for the profile. Sign-ins and favorites stay.
5. **A completion** is a favorite first, then history, each page once, matched in its address or its
   title whatever the case. A host that starts with what was typed comes before one that only contains
   it, and history then goes by how often, then how lately. A typed scheme alone matches nothing.

## 3c. The door, and where links open (BRW7, 2026-09-30)

**The door is on the app strip**, a compass before the region toggles, in the strip's right-hand
group, shell-only like them. It does what View → *Browser* and the palette do: opens whichever browser
the machine uses (Daoris's own or the person's Edge, §3's BRW12) on its own start page, or brings it
forward. **Not on the activity bar**, by the frame design's rules (§3a–b there): the bar is the one
navigation (D66), a place per icon, each item changing what the window's centre shows and wearing the
current-place mark, and its foot holds actions on this window. The browser is another window, and
opening it changes no place. The strip is where the application's acts are, the View menu that
already opens the browser among them, and its right end holds what changes the work's surroundings
rather than the view. The strip also has room for a session's name in words, which BRW8 needs; a 48px
bar has room for a count.

**Where the page's links open is the person's choice**, `links` in `<home>/browser/settings.json`:
`system` (the default, and anything that is not `daoris`) or `daoris`, which is whichever browser the
file's `browser` chooses. A twin like the rest of the file: `browser.ts` (`daoris browser links
[system|daoris]`) and `BrowserSettings.cs` (Settings → Browser, `SET_LINKS`), with the same table. The
page reads it from `DAORIS.BROWSER`'s state, not `daoris-browser`, so it needs no restart: a change
from Settings holds at the next click, and one from a terminal once an open window reads its settings
again, as it does coming back to the front.

1. **Every link on the page opens through one place**, `ExternalLink` (`links.tsx`): a quest's and an
   ask's links, a quest's kept files, a conversation's Markdown links and images. A test fails on a
   hand-written `<a` anywhere else. With no opener, or an address that is not an absolute `http` or
   `https` page with a host and no credentials, it is the link it always was, `target="_blank"`.
2. **The opener is the application's**, through a context: where a shell is here and `links` is
   `daoris`, a click is prevented and `DAORIS.WINDOWS` `OPEN_BROWSER` is asked with `{ url }`. A
   browser has no bridge and is never given one. The detached session's window provides the same.
3. **The shell checks again, by the favorites' address rule** (§3a.2), and refuses anything else by
   code (`BROWSER_LINK_NOT_A_PAGE`), since whatever it opens is a page agents can drive. It brings the
   chosen browser up as the person's press does and opens the page as a tab of its own
   (`Target.createTarget` with no `newWindow`, as an agent's tab is made), then brings that tab
   forward. A browser that was not running opens its start page first, with the link's tab beside it.
4. **A sign-in link always opens in the system's browser**, whatever `links` says: an account's
   sign-in is the person's own, and never belongs in a browser any process on this machine can drive
   (§3.4).

**Not verified when it was written**: no window was started, because the owner's install was running
on the machine. The tab's placement and focus in the engine's window, and in Edge's, are the look
after merging.

## 3d. Who is driving (BRW8, 2026-09-30)

An agent's current tab is the first page it found, not the one in front (§3.2, measured with BRW4),
and neither the engine's window nor Edge's is Daoris's to draw in. So it is said in Daoris, before the
person types into a page: beside the strip's door, and at the head of Settings → Browser.

1. **Read from what the driver handed**, never from the page. `InAppBrowserServers.HandAsync` answers
   whether a server that drives the browser was handed (`Drives`: an endpoint was answered and every
   server that needs it got it). The session's registry entry keeps it beside the process
   (`SessionProcesses.Track(…, drivesBrowser)`), on all three doors that hand servers: a driven
   quest's session, an intake, and a conversation. Ask Daoris is handed no plugin servers, so it never
   drives.
2. **Driving lasts from the handing until the session ends**, whether or not the agent has used the
   browser yet: that is its own, and never reaches the driver. The words say so in the tip.
3. **`drivingBrowser` in the driver's state** is those ids, running. Only this registry's: a
   terminal's driver has no shell to hand it a browser, so every session that can drive this shell's
   browser is held here.
4. **The page names each session as the rail does** (`browserDrivers`): its repository, then what it
   is for (`sessionTitle`). A session the page has no record of — the list has not caught up, or it is
   in a circle the window is not scoped to — is named by its id rather than left out.
5. **On the strip**, one session is a chip, *driven by engine · Read the ticket*, that opens it in
   Sessions; two or more are a count whose menu lists each. Nothing is said while nobody drives. In
   words, never a mark alone (D41 §6), and the strip's room is why the door is there (§3c). **In
   Settings → Browser**, *Driving it now* names each, a door into Sessions, or says no session is.

**Not verified when it was written**: the chip's width beside the command center at a narrow window,
and its look in both themes, are the look after merging.

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
| **BRW7** | The strip's door, `links` in the settings file on both doors, `ExternalLink` as the one place a link opens, and `OPEN_BROWSER` with a `url` (§3c) | the twin tables, module tests with a stand-in browser, vitest over a mocked bridge (the helper, the Settings row, the whole window's door and a quest's link), and a look on the window after merging |
| **BRW8** | Who is driving: `Drives` from the hand-over, kept in the registry, `drivingBrowser` in the driver's state, the strip's chip and Settings' row (§3d) | driver tests (the hand-over, the registry, a real tick's driven session and a real conversation, each with a stand-in browser and without), the module's state, vitest over a mocked bridge (no session, one, two, and the whole window) |

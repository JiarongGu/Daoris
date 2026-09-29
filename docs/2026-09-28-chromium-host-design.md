# Chromium under the windows — Daoris's page and its browser on an engine it ships

> Written 2026-09-28 at the owner's direction: *"to shift to chromeiun, because we mostly build the ui
> itself in react and the only missing part is the shenora currently dont support this, but the
> framework itself is still okay to use since there is no big difference, just webview2 to chromium,
> we can start the work here and also file the new task to shenora for this"*. It answers D84's open
> form and amends D84's *"Daoris's own UI stays on WebView2"*. The decision is **D85**.

## 0. What changes and what stays

**Changes:** the web view under every Daoris window. The main window, the secondary windows and the
in-app browser render in a Chromium that Daoris ships and embeds, instead of the system's WebView2
runtime. This answers D84's *which form*: the engine is **embedded**, not a separate browser program
Daoris starts. One engine hosts both the page and the browser.

**Stays:** everything above the web view. `Daoris.Web` is the one UI (D38), React, unchanged in what
it renders. Shenora's framework is kept: modules, the dispatcher, the event bus, the frameless form
and its caption buttons, window state, `ShenoraPaths`, secondary windows. The in-app browser keeps its
own chrome (BRW4–BRW6's tabs, favorites and history) and its CDP seam, `${browser}` in a plugin's
server (D78 §3.4–§3.6). The person's Edge stays an option (BRW12).

## 1. Why

1. **The limits met were WebView2's API, not Chromium's** (measured, 2026-09-28). A tab an agent opens
   over CDP has no window (BRW4), and a session cookie ends with the process (BRW10, and on Edge too:
   `docs/2026-09-28-managed-edge-evidence.md` §3). **CHR1 measured both on CEF:** the engine's own
   setting keeps a sign-in, and an agent's tab is visible, in an engine window the app is not told of
   (`docs/2026-09-28-chromium-embedding-evidence.md` §2, §5).
2. **One engine, one runtime in the install.** D84 already accepted shipping a Chromium, at 150–250 MB.
   Keeping WebView2 for the page alongside it would ship one engine and depend on another.
3. **A machine prerequisite goes.** Today the shell refuses to start without the Evergreen WebView2
   runtime. An engine in the install is one the artefact gate (D60) can start and check.
4. **The page is already engine-neutral.** The UI is React served by the host; only its host changes.

**The cost accepted.** The engine's security updates become Daoris's for the page as well as the
browser. Windows updated WebView2, and nothing updates an engine Daoris ships except a release of
Daoris. An embedded engine is pinned by the build, so D84's *"managed like a harness, fetched from its
maker's channel"* does not apply to it: it updates when Daoris releases. The host code WebView2 gave
through Shenora is Shenora's to give again (§3), so Daoris's move waits on Shenora's release. **A browser's basics may come back as well.** D84 retired BRW9 (find,
zoom, devtools, downloads) because a separate browser has its own. If CHR3 draws Daoris's own chrome
around the control, they are Daoris's to build again. The engine's own window has them (§4).

## 2. The shape

1. **Shenora builds the host, and Daoris takes it** (amended the same day, §3). The seams it builds on
   are the ones this section first named for Daoris: `IpcHostBridge.HandleIncomingAsync` with a
   `NotificationPump` on the host, which both of Shenora's shells already wrap, and `ShenoraBridge`'s
   `transport` on the page. Nothing in Shenora is edited from here (D32).
2. **The same contract a window has today.** It initializes, navigates to the host's URL, shows the
   splash until the page loads, and says what failed when it does not. The window commands get a
   `CoordinateSpace` for the new control, per monitor. A secondary window keeps its own environment
   (the 2026-09-22 trap).
3. 🔴 **The page that holds the bridge is never in CDP's reach** (D78 §3.1 stands). **Measured by
   CHR1** (`docs/2026-09-28-chromium-embedding-evidence.md` §1): the debug port is one setting per
   process, and it reached the app's page and its bridge. So **the browser runs in a process of its
   own**, in that process's global request context (so an agent's tab shares the sign-in, §4 there),
   and the page's process has no port in a published build.
4. **Profiles under the home** (D63): the page's and the browser's, apart, as `webview2/` and
   `browser/profile` are today.
5. **The WebView2 path stays until the Chromium one is proven on the install**, then it goes. No
   switch between them is kept longer than that.

## 3. The request to Shenora

*As filed:* Shenora grows by harvest (its D15), so Daoris would build the host first and Shenora take
it in once it had proved itself. Its answer, below, changed that. The owner said to
file the request in Shenora's own backlog (2026-09-28, *"you can file the TASKS.md"*), and it was filed
there as one new entry, uncommitted, with nothing else in that repository touched. It asks for:

- a Chromium web view beside `WebViewHost`, in the same shape;
- a page transport in `createHostTransport`'s chain;
- the window commands' `CoordinateSpace` for a control that is not a `WebView2`.

It leaves two questions to Shenora: where the engine's bytes come from (its D51, on shipped binaries
and their licences), and making the unsafe debug-port composition impossible. CHR1 answered the port
question (per process, so the page is in reach), and the answer went to the owner for Shenora's
entry, not into Shenora from here.

**Shenora's answer (the owner, in Shenora's session, 2026-09-28, as its backlog records it).** The
kit builds the host now, as a package of its own (`Shenora.Windows.Chromium` on CefSharp), with the
engine's bytes arriving through its upstream package and never inside a kit package. The first
adopter takes it *instead of writing its own host*, and a cross-platform desktop shell follows. So
Daoris does not build the host: CHR2 waits on that package. Shenora's entry says to read CHR1's
evidence rather than repeat it, which leaves the kit's own probe to what depends on the kit's frame.

**The second request (2026-09-30, D92).** 0.17 shipped the page's host as `ChromiumView` on the kit's own
binding (§4a), and nothing yet for a browser. At the owner's say-so (*"whats the limitation can you file
this to shenoras TASKS.md?"*), one entry was filed in Shenora's backlog, uncommitted, with nothing else
touched: a browser-only engine whose windows are Chrome style and made over CDP, a production debug port
for a process that holds no bridge, the engine settings `daoris-browser` sets today (persisted session
cookies, cache paths under the home, log, locale, first-run switches), and one CEF layout on disk. Until
it lands the install carries two engines (CHR8).

## 4. Open

- **The browser's form: decided, the engine's own window** (the owner, 2026-09-28: *"lets do it now
  please download"*, on the recommendation). CHR1 had found the engine offers two:
  - Daoris's own chrome around the control (BRW4–BRW6 carried, BRW9 back);
  - the engine's own Chromium window, where an agent's tabs already land, with its own history,
    bookmarks, find, devtools and downloads, but Chromium's UI rather than Daoris's.

  Either way, an agent's CDP tab escapes into an engine window, and Playwright MCP 0.0.82 cannot open a
  tab (the evidence's §2–§3).
- **The library.** CefSharp 152 was measured and did everything asked. It runs about two Chromium
  versions behind Chrome.
- **Size.** 352 MB on disk with two locales, 166 MB compressed. That is over the owner's 150–250 MB on
  disk, and within it as a download.
- **Codecs.** Confirmed without the proprietary ones: no H.264, AAC or HEVC.

## 4a. What Shenora 0.17 shipped (read 2026-09-30)

Shenora 0.17.0 (released 2026-09-29) carries the host CHR2 waited on, in a different shape than §3
expected: **`Shenora.Chromium`, the kit's own CEF binding** (not CefSharp), pinned to **CEF 154.0.28 /
Chromium 154.0.8037.58**, hosted in WinForms as **`ChromiumView`** beside `OptimizedForm`, the window
commands and `SecondaryWindows` (its ADOPTION.md, *Stage 2 on Chromium*). What that means here:

- **Adopting it:** the app project references `Shenora.Chromium` with a runtime identifier, and its
  assembly is named `<App>.App`; the build fetches the pinned CEF and makes `<App>.exe` CEF's launcher,
  so `daoris-desktop.exe` can keep its name. `UseChromiumEngine(...)` sits beside `UseWindows`, and a
  `ChromiumView` takes the WebView2 control's place, serving its page and bridging its IPC itself. A
  secondary window's page commands its own window.
- **Where the page comes from changes.** A view serves the bundle from a content folder at
  `https://{VirtualHost}/` (or a dev server, in development only), where WebView2 showed the host's URL.
  The page reaches its host at the loopback address instead, cross-origin, which the kit supports as its
  *server-backed profile* (0.17 made that fetch work in Chromium). So the host allows that one origin in
  local mode, and the page calls the host by an absolute address the shell gives it; in a browser the
  page keeps calling its own origin.
- **The instruments:** a DevTools port opens only in development (`IsDevelopment`, `DevToolsPort`), so a
  published app has none (D78 §3.1 holds by construction), and looking at the install means starting it
  as development.
- **The browser cannot share it yet.** The kit's pages are Alloy style, and it has no Chrome-style
  window, which is the form CHR3 chose for `daoris-browser` on CefSharp 152. Moving the page alone ships
  **two engines**: CEF 154 is a 173 MB download (about 350–400 MB on disk) beside CefSharp's 352 MB.

## 5. Build order

| Item | What lands | Proven by |
|---|---|---|
| **CHR1** ✓ | A scratch probe: an embedded Chromium in a WinForms window. It answered whether the debug port reaches every page in the process (it does); whether a tab a CDP client opens reaches the app (no: it gets an engine window); whether a session cookie survives a restart with the engine's own setting (yes); Playwright MCP attached and driving (yes, except opening a tab); the page-host round trip (0.2–0.3 ms median); and size, banner, codecs and licence | `docs/2026-09-28-chromium-embedding-evidence.md`, 2026-09-28 |
| **CHR2** ✓ | The main and secondary windows on `ChromiumView` (Shenora 0.17, §4a, D92): the page on `https://daoris.localhost/`, told its host's loopback address (CHR2a); the shell on `UseChromiumEngine`, the bundle found as the host finds it, and the dev loop's instruments on the development DevTools port (CHR2b, CHR2c) | a look at the main window, the monitor and a detached session in both themes; caption buttons answering `HTMINBUTTON`/`HTMAXBUTTON`/`HTCLOSE` from the rectangles the page reported; the deployment rehearsal (§5a) |
| **CHR3** ✓ | `daoris-browser`: the engine's own window, in a process of its own with its global request context and `PersistSessionCookies`, CDP on that process only, started by the shell behind `IInAppBrowser` and carried by the install under `app/daoris-browser/`. Supersedes BRW11 | the deployment rehearsal: the deployed shell's driver brings up the install's own browser for a session's server, and it goes with the shell; a look on the scratch shell (evidence §11–§15) |
| **CHR4** ✓ | The install carries it (D93): `Daoris.exe`, a launcher, at the root, and the application beside its Chromium in `app/`, recorded in `app/shell-files.txt` so a republish removes the last engine's files; the gate starts the install through the launcher and asserts the engine off the process tree. The WebView2 path goes | `npm run rehearse:deploy` |

### 5a. What the move found

- **The bundle a host serves is not always beside it.** In development ASP.NET serves the project's
  `wwwroot` to a host started from `bin/`, through its static assets manifest, so the first Chromium run
  said the bundle was missing. `DesktopPage.BundleOf` walks up from the host as the host effectively does.
- **Chromium's processes run from the application's executable** (`--type=renderer`, `gpu-process`,
  `utility`). Every tool that stopped or counted "what runs from this path" had to learn to tell the
  application apart: walked blindly, a stop waited fifteen seconds on each and then crashed a page.
- **A copied launcher wears CEF's icon.** The build stamps Daoris's onto it after the layout.
- **The kit anchors its data area at the executable's folder** unless told, so an application in
  `app/` hands it the install's root; otherwise its profile lands in a second `data/` inside `app/`.
- **Radix menus ignore a scripted `.click()`**: looking at a menu over CDP needs real pointer input
  (`Input.dispatchMouseEvent`). And the native caption buttons take no page input at all; their
  hit-test is read with `WM_NCHITTEST` against the window.

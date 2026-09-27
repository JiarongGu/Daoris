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
maker's channel"* does not apply to it: it updates when Daoris releases. And the host code WebView2
gave through Shenora (initialization, navigation, the bridge on `chrome.webview`) is Daoris's to write
until Shenora takes it in (§3). **A browser's basics may come back as well.** D84 retired BRW9 (find,
zoom, devtools, downloads) because a separate browser has its own. If CHR3 draws Daoris's own chrome
around the control, they are Daoris's to build again. The engine's own window has them (§4).

## 2. The shape

1. **Built here, on Shenora's public, engine-neutral surface.** The host bridge is
   `IpcHostBridge.HandleIncomingAsync` with a `NotificationPump`. Both of Shenora's shells (WebView2 and
   mobile) already wrap them. On the page, `ShenoraBridge` takes a `transport`. So a Chromium host is a
   control, a bridge over `IpcHostBridge`, and a page transport. No Shenora type is forked or
   patched, and nothing in Shenora is edited from here (D32).
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

Shenora grows by harvest (its D15): something proven in an application is generalized and moved in.
So Daoris builds the host first and Shenora takes it in once it has proved itself. The owner said to
file the request in Shenora's own backlog (2026-09-28, *"you can file the TASKS.md"*), and it was filed
there as one new entry, uncommitted, with nothing else in that repository touched. It asks for:

- a Chromium web view beside `WebViewHost`, in the same shape;
- a page transport in `createHostTransport`'s chain;
- the window commands' `CoordinateSpace` for a control that is not a `WebView2`.

It leaves two questions to Shenora: where the engine's bytes come from (its D51, on shipped binaries
and their licences), and making the unsafe debug-port composition impossible. CHR1 answered the port
question (per process, so the page is in reach), and the answer went to the owner for Shenora's
entry, not into Shenora from here.

## 4. Open

- **The browser's form (CHR3, the owner's call).** CHR1 found the engine offers two:
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

## 5. Build order

| Item | What lands | Proven by |
|---|---|---|
| **CHR1** ✓ | A scratch probe: an embedded Chromium in a WinForms window. It answered whether the debug port reaches every page in the process (it does); whether a tab a CDP client opens reaches the app (no: it gets an engine window); whether a session cookie survives a restart with the engine's own setting (yes); Playwright MCP attached and driving (yes, except opening a tab); the page-host round trip (0.2–0.3 ms median); and size, banner, codecs and licence | `docs/2026-09-28-chromium-embedding-evidence.md`, 2026-09-28 |
| **CHR2** | The host: the control, the bridge over `IpcHostBridge`, the page transport, the coordinate space. The main and secondary windows move to it. The reply goes through the control, never the message's frame (the evidence's §6) | module tests for the pure parts, the web suite on the page transport, and a look at the window |
| **CHR3** | The browser on the same engine, in a process of its own with its global request context and `PersistSessionCookies`, in the form the owner picks (§4). CDP on that process only. Supersedes BRW11 | a stub session handed `${browser}`, and an agent's tab seen by the person |
| **CHR4** | The install carries it: `publish:desktop` places the runtime, and the deployment rehearsal starts the published shell on it and asserts which engine answered. The WebView2 path and its refusal go | `npm run rehearse:deploy` |

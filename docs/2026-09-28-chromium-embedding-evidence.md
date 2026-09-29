# An embedded Chromium under a Daoris window: what CEF does (evidence)

**Carried by:** CHR1, and through it CHR2 and CHR3 (`docs/2026-09-28-chromium-host-design.md`). A
record, not a contract.

**Date:** 2026-09-28 · **Engine:** CefSharp 152.0.100, CEF 152.0.10, Chromium 152.0.7977.140 ·
**Instrument:** `tools/chromium-probe.mjs` with its host `tools/chromium-probe/` (a WinForms window
embedding CefSharp's control), and Playwright MCP 0.0.82 · **Cost:** no model, no account.

## The probe

The host takes one or both of the roles a Daoris window gives the engine: the **app's page** (the page
that would hold the bridge) in the global request context, and the **in-app browser** in a request
context of its own, or in the global one when it runs alone. Each run has its own root cache under
`_fixtures/`, and its windows are closed through UI Automation by the host's own pid. A stand-in site on
loopback plays the app's page and an identity server, which sets a cookie with no expiry at `/login`.
Every finding held across the runs: three complete, one stopped by a fault in the driver, and three of
the last phases alone. The round trip was measured in four of them. Where a number moved between
runs, the range is given.

## What came back

1. 🔴 **The debug port reaches every page in the process, the app's page included.** A process holding
   the page and the browser, with a port on it, listed both as CDP targets. Over that port,
   `CefSharp.PostMessage`, the page's bridge, was a callable function. The API has no per-context port:
   `RemoteDebuggingPort` is on the global `CefSettings` alone. **With the browser in a process of its
   own**, the port lists only the browser's page, and both processes run side by side, each on its own
   root cache.
2. **A tab an agent opens escapes into a Chromium window of the engine's own.** CEF 152 runs on the
   Chrome bootstrap. CefSharp's control is Alloy-styled by default, and Chrome-styled on request. In
   both styles, a target a CDP client creates (`Target.createTarget`) got a full Chromium browser
   window, with a tab strip, an address bar, a star and a profile button reading *Action required*. The
   person sees it, and its later targets become tabs there. **The app is not told**: no life-span
   callback of the control's ran. A page's own `window.open` is different, and reaches the app
   (`OnBeforePopup` with `NewForegroundTab`, then `OnAfterCreated`), as WebView2's `NewWindowRequested`
   did.
3. 🔴 **Playwright MCP 0.0.82 cannot open a tab in it.** `browser_tabs new` failed in every arrangement
   measured, Alloy, Chrome, own context and global context alike: *"TypeError: browserBackend.callTool:
   Cannot read properties of undefined (reading '_page')"*. The engine did make the target, as a Chromium
   window's `about:blank` tab, and Playwright never took it up. Listing tabs, snapshotting the page and
   navigating the current tab all worked. On WebView2 the same call made a page with no window (BRW4).
4. **An agent's tab carries the browser's sign-in when they share a request context.** Signed in on the
   control, then a CDP target opened: it was signed in with the browser in the global context, and in
   Chrome style. It was *signed out* with an Alloy control in a request context of its own, because a
   CDP target lands in the global one.
5. **A sign-in survives a restart with the engine's own setting.** With `PersistSessionCookies`, the
   same session came back after the window was closed and the host started again on the same cache.
   Without it, the site said signed out, which is the control showing that the probe can fail.
6. **The page-to-host round trip is fast.** `CefSharp.PostMessage` to the host, then a script back
   through the control, 500 times in a row: median 0.2–0.3 ms, p95 0.4–0.8 ms, the slowest 46–61 ms.
   ⚠ A reply sent
   through the event's own frame (`message.Frame.ExecuteJavaScriptAsync`) **never arrived, and nothing
   said so**. Through the control (`ExecuteScriptAsync`), every one did.
7. **What it adds to an install.** On disk, 400 MB, of which `libcef.dll` is 272 MB and 220 locale
   packs are 50 MB. With en-US and zh-CN only, 352 MB. That, compressed per file, is 166 MB, and the
   runtime's NuGet package is 181 MB. **On disk it is over the 150–250 MB the owner accepted; as a
   download it is within it.** The build is Chromium 152 while this machine's Edge is 154, so the
   embedding runs about two versions behind Chrome.
8. **Codecs: no proprietary ones.** H.264, AAC and HEVC: *no*. VP9, AV1 and Opus: *probably*, and
   Media Source takes them.
9. **Licences.** CEF is BSD, and the runtime points at `about:credits` for the rest. The build's credits
   page (19 million characters) names FFmpeg 15 times and carries LGPL text 291 times, GPL 363 times and
   MPL 94 times. Those are mentions in the text, not an audit of what is linked. That is Shenora's D51
   question, which the request left to it.
10. **No account.** CEF logged that *"Desktop Identity Consistency cannot be enabled as no OAuth client
    ID and client secret have been configured"*. This build signs in to no Google account, where a fresh
    Edge profile signed in to the person's Microsoft account on its own (D84's evidence, §5).

## What it means

- **The browser runs in a process of its own.** That is decided, not preferred: the port cannot be
  scoped, and the page's bridge is in reach otherwise. The page's process never has a port in a
  published build (D78 §3.1 stands, now by construction).
- **In that process, use the global request context**, so an agent's tabs share the person's sign-in,
  and keep sign-ins with `PersistSessionCookies`. That makes BRW10's sealed carry unnecessary on the
  engine.
- **The browser's form is open again**, because the engine offers two:
  - **Daoris's own chrome around the control** (BRW4–BRW6 carried over, BRW9 back). An agent's own tab
    still escapes into a Chromium window, and Playwright cannot open one.
  - **The engine's own Chromium window**, whose tabs an agent's targets already join. It brings its own
    tabs, history, bookmarks, find, devtools and downloads, the way D84 measured Edge, with no account.
    But its UI is Chromium's, not Daoris's (a profile button, its own settings), and
    `daoris browser favorite` would have Chromium's bookmarks, not Daoris's file, to edit.

  That choice is CHR3's, and it is the owner's.
- **Playwright's tabs are an agent's problem in either form**, until a Playwright version or a
  Daoris-side answer takes a target the engine made. orca answers it with a CDP proxy. Not measured
  here.

## What CHR3 then measured on `daoris-browser` itself (2026-09-28)

The browser as built: the engine with no control, in a process of its own, making its windows over
its own port (`src/Daoris.Desktop/Daoris.Desktop.Browser`), started by the shell.

11. **A process with no control opens its first window over its own port**, in 0.7 s: the engine's own
    Chromium window on its new-tab page. Closing that window ends the process in about 3 s. Ending the
    shell closes the browser's windows and ends it in under a second. A sign-in survived a restart.
12. 🔴 **Why no agent can open a tab.** A target made by `Target.createTarget` is announced in
    `Target.targetCreated` as type **`other`**, and only shows as a `page` in `/json/list` afterwards.
    Its browser context is the default one, so that is not the cause. Playwright MCP's
    `browser_tabs new` fails as before, and so does Chrome DevTools MCP's `new_page` (*"Failed to create
    a page for context (id = undefined)"*): both wait for a `page`. The tab they asked for does open,
    in the person's window. Driving the current tab works in both.
13. **Focus is Windows' to give.** Started from a background process, the first window took the
    foreground once without being asked to stay back, and once when asked. Windows' own foreground
    rules decided, not the request.
14. 🔴 **The machine's Chrome extensions reach the engine's profile.** On a fresh profile the app menu
    read *Action required: New extension added (…)*, naming a download manager's helper that another
    program had registered for Google Chrome under `HKLM\SOFTWARE\Google\Chrome\Extensions`, which the
    engine reads.
    It is not installed until the person approves it (the profile had no extensions folder). The profile
    is sealed from the person's accounts, and not from what other software registers for Chrome.
15. **Nothing of it lands under the user profile**, apart from the OS temp folder's timestamp, which
    the run's other processes also write.
16. **The engine's windows use `<root>/Default`, not the cache path they were given**: a cache path
    set to another folder left that folder empty. So the engine is told `Default`, and its preferences,
    bookmarks and cookies are all there. Beside it are the engine's own component folders (Safe
    Browsing lists, optimization hints and others), which it fetches from Google.

## What the relay changed (CHR6, 2026-09-28)

`daoris-browser` now keeps the engine on a port of its own and answers `${browser}` with a relay
(`CdpRelay`). The relay passes every request and message through unchanged, except a target the engine
announces as `other` whose address is a tab (a web page, a blank tab, the new-tab page), which it calls
a `page`.

*Since CHR8 (D99, 2026-09-30) the relay is Shenora's, behind `ChromiumBrowserProcess`'s debug port,
and Daoris's `CdpRelay` and its tests are gone. What follows is what Daoris's relay measured.*

17. **Playwright MCP 0.0.82 opens a tab through it**: `browser_tabs new` made tab 1 and made it
    current, and `browser_navigate` went to that tab, shown in front in the person's window.
18. **Chrome DevTools MCP opens one too**: `new_page` listed the new page and selected it.
19. **What it leaves alone**: the engine's own windows (`devtools://`), extensions' pages and its
    browser UI keep their types. Held by `CdpRelayTests`, with the correction watched failing.
20. **Seeding `extensions.external_uninstalls`** in the profile's preferences with a registered
    extension's id stopped the engine offering it (no *Action required*). The engine kept the entry
    rather than resetting it, and without the seed the offer came back.
21. **A bookmarks file written from nothing, with no checksum, is taken.** A *Daoris* folder written
    onto the bar was shown (with `bookmark_bar.show_on_all_tabs`) and was still there, unchanged,
    after a clean close.
22. **The settings applied at start, end to end** (`daoris-browser` on a scratch home):
    - *refuse*: the folder was on the bar and the offer was gone;
    - *offer*: the offer came back;
    - *no favorites*: the folder went;
    - an id the person had refused was still refused after Daoris offered again.

    The engine raises its *Action required* chip once: an offer left unanswered waits on its
    extensions page afterwards.

## Not measured

- Shenora's frameless form hosting the control, per-monitor DPI, and a secondary window's own engine.
- A process holding only the port and no control, making its first window over CDP.
- What *Action required* on the profile button asks for.
- Memory, start-up time, GPU, and a Playwright newer than 0.0.82.
- Any other OS.

# A real browser as Daoris's browser: what Edge does when Daoris starts it (evidence)

**Carried by:** the in-app browser's next shape (BRW11, BRW12). A record, not a contract.

**Date:** 2026-09-28 · **Browser:** Microsoft Edge 154.0.4258.37, the machine's installed one ·
**Instrument:** `tools/edge-probe.mjs`, with Playwright MCP 0.0.82 · **Cost:** no model, no account.

BRW4–BRW6 rebuilt a browser's basics on WebView2 and met two limits that no embedding answers: a tab
an agent opens over CDP has no window, and a session cookie ends with the process. The owner then
asked whether Daoris should ship a browser engine. This measures the other half of that choice: a real
browser, started and kept by Daoris on a profile of its own, driven over the same CDP endpoint the
plugins already take (D78 §3.5).

## The probe

The probe starts its own Edge with a fresh `--user-data-dir` under `_fixtures/` and a debug port. That
is a separate browser process from the person's own Edge, and the probe closes only its own instance,
over its own port or through its own window. A stand-in site on loopback sets a cookie with no expiry
at `/login` and says who is signed in at `/`.

## What came back

1. **A debug port on a profile that is not the default one is accepted**, and Playwright MCP attached
   over it listed, opened and navigated tabs. (Chromium refuses a debug port on the *default* profile
   since 136. That case was not probed, because it would mean restarting the person's own Edge.)
2. **A tab an agent opens is a tab the person sees.** After `browser_tabs new` and a navigate,
   Playwright, CDP and Edge's own window (read through UI Automation) agreed on two tabs. In Daoris's
   WebView2 window the same call made a page with no window at all (BRW4).
3. **A sign-in does not survive Edge restarting**, even with Edge's own *continue where you left off*
   (`session.restore_on_startup: 1`). That setting restored the tab and not the session cookie, even
   after a 35-second wait for the cookie store's flush and a close through the window, as a person
   closes it.
4. **Daoris can keep it itself**, as BRW10 does for its own window. It reads the session cookies over
   CDP (`Storage.getCookies`) before the close and puts them back (`Storage.setCookies`) before the first
   page after it. The same session came back.
5. 🔴 **A fresh profile is not isolated.** Within seconds of its first start, a new profile on this
   machine was **signed in to the person's Microsoft account and syncing**: it installed their
   extensions and opened pages of theirs. With `--disable-sync` the sync stopped (no sync state, no
   extension pages), and **the account stayed signed in**: Edge signs a new profile in with the
   Windows account on its own. No flag the probe tried changes that; a machine or user policy
   (`BrowserSignin`) does, which is the person's machine, not Daoris's to set. WebView2 does not do
   this unless asked (`AllowSingleSignOnUsingOSPrimaryAccount` is off by default), and a Chromium
   Daoris ships would not either.

## What it means

- **Edge, started by Daoris, is a full browser at no cost**: tabs, history, favorites, devtools,
  downloads and find are its own, and an agent's tabs are visible. What BRW4–BRW6 built by hand on
  WebView2 it has already. What BRW10 built (keeping a sign-in) it still needs, over CDP.
- **Edge carries the person's account.** For an option that means to use the person's Edge (their
  sign-ins, their extensions), that is the point. For a browser meant to be Daoris's own, it is a
  leak: an agent driving it could be handed the person's identity on a Microsoft sign-in page. That
  is why a browser Daoris ships is the isolated one, and Edge is the option.
- **What was not measured:** the default profile (see 1); whether the account's automatic sign-in
  reaches a Microsoft page in the driven profile (the claim in 5 is that the account is signed in, not
  that a page was seen to use it); a Chromium build Daoris would ship; and anything on another OS.

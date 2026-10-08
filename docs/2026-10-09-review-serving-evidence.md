# REVIEWENV1d — the shell's interception beside a session's browser server (evidence note)

**Carried by:** D154 point 5 and REVIEWENV1d. A record of one measurement, not a contract.

> Written 2026-10-09, before REVIEWENV1d was built, because D154 and the review environment design (§2.3, §8) left one
> question to be measured first: whether the shell's own request interception on a set-up step's tab works beside a
> session's own browser server attached to the same tab over CDP. **Driven, not read about**, with no model, no account and
> no credential, and nothing of the person's touched: a scratch browser on a scratch profile and port, stopped by its own
> process id.

## 0. What was probed

| | |
|---|---|
| Browser | Edge 154.0.4258.53, headless, its own profile and debug port under the worktree's gitignored scratch folder. A Chromium, like Daoris's own (D99), and not Daoris's own: see §3 |
| The shell | A raw CDP client on the browser's socket: `Target.createTarget` for the step's tab, `Target.attachToTarget` with `flatten`, then `Fetch.enable` on that session alone with one pattern, `<address>/v3/*` at the request stage. A paused request naming a file in the build folder is fulfilled with it, a `Document` request is given `index.html`, and anything else is continued |
| The session | Playwright 1.63 `connectOverCDP` on the same endpoint, which is how a browser MCP server attaches (`--cdp-endpoint`, D78 §3.5), finding the step's tab among the browser's pages. Its own interception is `page.route`, the real case's way (REALCASE1) |
| The person's server | A loopback HTTP server that answers every path and counts each hit: a page titled `PERSON` with its own script, and `/api/data` as `{"from":"person"}` |
| The build | `index.html` with `<base href="/v3/">`, titled `BRANCH`, and `app.js`, which marks the page and fetches `/api/data` |

The probe was a scratch script; the table is everything it set up.

## 1. What held

| # | Done | Seen |
|---|---|---|
| 1 | The shell serves; the session attaches with no route of its own and navigates to `/v3/reports/42` | The page and its script came from the folder. The person's server had **no hit under `/v3/`**. `/api/data`, outside the pattern, reached it once |
| 2 | The session adds its own route over the same paths, fulfilling them, and reloads | **The session's route answered**; the shell saw no request. The person's server had no hit |
| 2b | The session's route lets each request go on (`route.continue`) | The shell then answered both, page and script. **The two interceptions chain on one tab**: the session's first, then the shell's, then the network |
| 2c | The shell enables its interception again after the session's route | The session's still saw it first and answered. Enabling again does not move the shell ahead |
| 3 | The session's connection closes, as a session's end closes its server (D105 §3); the tab is reloaded over the shell's own connection | **The branch's build still showed**, and the person's server had no hit under `/v3/` |
| 4 | The contrast: the session's route alone, the shell not serving; then the session goes, and the tab is reloaded | While it lived, its build showed. Once it ended, **the reload loaded the person's server, with no word** (two hits under `/v3/`). This is D154 point 5's reason, seen |
| 5 | The shell serves again, then stops (`Fetch.disable`), as a verdict ends it | Served, then the person's server again. Nothing of theirs was stopped or restarted |
| 6 | Another tab opened at the same address while the shell serves the step's | It loaded the person's server: **the interception is the step's tab's alone** |

## 2. What it settles for the build

- **The shell's interception coexists with a session's browser server on the same tab.** Attaching Playwright over CDP
  did not disturb it, and closing that connection left it standing. Nothing needs the session to stop driving for Daoris to
  serve.
- **A session's own route is above Daoris's, never below it.** While a session lives, what it fulfils is what the tab
  shows; what it lets go on reaches Daoris's serving before the network. So the person's server is not reached for the
  served paths whichever way the session works, and once it ends Daoris's answer is what stands.
- **One tab, one address pattern.** A pattern at the address and the build's base covers the page, its routes and its
  files. A call outside the pattern, the app's data under `/api` here, goes where it goes for the person.

## 3. What it does not cover

- **Daoris's own browser was not started.** The engine is the kit's Chromium behind the kit's relay (D99), and the
  shell's own connection there goes through that relay. Agents' browser servers attach through it in flatten mode today,
  which is the mode used here, but this exact sequence was run against Edge's own port only.
- **Playwright MCP itself was not run**: the library it is built on was, by the call it makes to attach. Chrome DevTools
  MCP, the other server D78 names, was not.
- **Ordering past two clients, service workers, and a request the session's route aborts** were not measured.
- **Edge as the person's chosen browser (BRW12)** is the same protocol on its own port, with no relay, and was the
  browser measured here, headless rather than windowed.

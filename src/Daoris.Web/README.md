# Daoris.Web — the platform: the person's window over the family

**Status: built.** A React application over `Daoris.Service`, served by `Daoris.Service.Http`. Both open
questions in the original brief are settled — as `docs/DECISIONS.md` D30 and D31 — and it has since
grown into the platform (D38, `docs/2026-09-19-platform-design.md`): knowledge, tasks and setup in one
place.

## What it is

One activity bar (D66), landing on management (D40). The views that read the family:

| View | What it answers |
|---|---|
| **Overview** | the landing: is anything sitting and for how long, the family's health as tiles, the repositories by what the index holds |
| **Quests** | what has been asked of whom, grouped by where it is in its life; publish, take, done, decline — and, beside a driven quest, its session **record** (D46): state, adapter, note and evidence, read-only in a browser, with stop offered only where a shell's driver actually holds the process |
| **Projects** | who is in the family, what each owns and accepts as scannable chips — and who cannot be asked yet, with the join steps proposed as text; in the desktop shell, the person's per-machine driver controls (drive / hold) per repository |
| **Convergence** | where two repositories reached the same conclusion independently — the knowledge half's lead view |
| **Search** | what the family has already learned about X |

**Sessions** is the working surface — the agent sessions this machine runs, attended one at a
time (D55, `docs/2026-09-21-working-surface-design.md`) — and exists only in the desktop, because a
stream never leaves its machine. **Map** is how a workspace's repositories are wired (MAP2).
**Settings** is everywhere: appearance in a browser, and in the desktop this machine's wiring,
driver and agents too. Each domain is `src/settings/<Name>Domain.tsx`, with its tests beside it, and
`src/settings/domains.ts` lists them: the one place a domain is added, since the frame
(`SettingsView.tsx`) renders whichever one is chosen and names none itself (MOD4).

**The design language is written down** — `docs/2026-09-19-platform-ux.md` (D41): the console shell
(an activity bar, page headers, one primary action per view), the token system, the drawer as the single
detail-and-form surface, toasts carrying the service's sentences verbatim, and a status palette that
was **computed, not tasted** — both themes pass all six checks of the visualization validator, and a
status pill never appears without its text label.

## Built on (D42, `docs/2026-09-19-frontend-architecture.md`)

| Layer | Choice |
|---|---|
| Styling | **Tailwind v4** — the validated tokens are the theme (`src/tokens.css`), utilities live beside the markup |
| Primitives | **Radix UI** — dialog (the drawer), toast, select, tooltip, checkbox: behaviour without a look |
| Icons | **lucide-react**, tree-shaken |
| Server state | **TanStack Query** — deduped fetches, refetch-on-focus, invalidation after every mutation |
| i18n | **react-i18next**, `en` + `zh`, flat dotted keys, **one file per area** (`src/locales/<language>/<area>.json`, merged at load by `src/locales/index.ts`): a key lives in the file named by its longest dotted prefix that has one, so `settings.rules.add` is in `settings.rules.json` and `nav.quests` in `nav.json`. `scripts/i18n-check.mjs` fails the build when an area's two files diverge, a key is in two files, or a key is not in its home (MOD2) |
| Shell bridge | **@shenora/react** — in the desktop, `DAORIS.DRIVER` carries the person's controls and `DRIVER_TICK` pushes the loop's reports into toasts and refetches (`ShellSignals.tsx`); in a browser none of it mounts, by design. **One file per domain** (MOD3): each domain's types and calls are `src/bridge/<domain>.ts` (driver, sessions, conversation, console, terminal, trees, lines, agents, plugins, rules, help, browser, windows, remotes, registry, log), `src/shell.ts` is only the barrel re-exporting them, and `bridge/bridge.test.ts` holds that it stays one |
| Design tool | **Storybook** (`npm run storybook`) — the component states and the token gallery, on the shipped code |
| Test loop | **Vitest + Testing Library** as the millisecond inner loop (view logic, primitives, catalogs — with the sibling's proven jsdom shims), **Playwright** as the outer loop — the real host over `examples/`, driving the shipped bundle. `npm run test:web` at the workspace root runs the whole pyramid |

**The i18n boundary:** UI chrome translates; **data does not**. Quest content, registry declarations,
knowledge bodies and the service's own sentences render verbatim — machine-translating a refusal would
break the contract that the service's sentence is the message. `zh` is 简体, in a console register
(委托 for quest — deliberately not 任务, keeping the family's own distinction).

## The landing is management; convergence leads the knowledge half (D30, D40)

The platform lands on **Overview**, because its first job is the person's first question — *is
anything sitting, and for how long* (D40). D30's measured finding stands inside the knowledge half:
**search must not lead it**, because the finding that mattered most was a convergence between two
repositories whose vocabulary overlapped by **25%**, and no search could have surfaced it — to search
for it you must already know it exists. So Convergence leads the knowledge views, and Search follows
for when you know what you are looking for.

The similarity threshold is a slider rather than a constant. Measured on this family, 0.82 returns
nothing, 0.70 returns the true pairs, and 0.60 begins pulling in unrelated documents — a default nobody
can move would be wrong for someone.

## Doctrine reads; service state writes (D31, D38)

No editing of doctrine from the browser, ever. Where a rule should change the UI shows what to run in
the repository that owns the file, because `upstream` deliberately routes an improvement through the
repository that found it, where it meets that repository's review. The convergence detector already
states this for itself: it proposes, a person disposes, and a candidate is a prompt to look rather than
a merge (D21).

Quests are a different kind of thing — service state (D32), already writable over the HTTP surface
(D36) — and under the automation-first model, **filing a quest is how a person sets a target** (D37).
So publish and respond are in the UI, through the same endpoints and the same `QuestExchange`
judgement as every other door, with refusals shown verbatim. A shared deployment (D47) serves no page
at all — the remote is an API until person-auth exists, and the person's window stays the desktop over
its local host; locally — the default — the full surface works.

**The active tier is stated on every screen**, never implied — a reader looking at results has no way to
know the semantic half was absent, and would read them as complete rather than as
complete-for-word-overlap (D24).

## One UI, two shells

This app is the **only** UI. It is served over HTTP for the browser, and `Daoris.Desktop` carries the
same build in its WebView — the same bytes a browser gets, plus the shell's capabilities (the driver
controls) that only exist where a driver does. Two shells, one codebase; a second hand-written desktop
UI would be the same divergence problem in a new place.

The build outputs into `../Daoris.Service/Daoris.Service.Http/wwwroot`, so the page and the API share one
origin. That is what makes CORS unnecessary in a real deployment — the `DAORIS_WEB_ORIGIN` variable
exists only for the development server on another port, and it names an origin rather than wildcarding.

## Running it

```
# the service, with the UI it will serve
npm --prefix src/Daoris.Web run build
dotnet run --project src/Daoris.Service/Daoris.Service.Http     # http://localhost:5177

# or, developing the UI against a running service
npm --prefix src/Daoris.Web run dev                             # http://localhost:5178, proxies /api

# the loop (from the workspace root): unit layer, then build bundle + host, then drive the real thing
npm run test:web
npm --prefix src/Daoris.Web run test:watch                      # the inner loop, on save
npm --prefix src/Daoris.Web run e2e:ui                          # the outer loop, watch-and-poke mode

# the design tool
npm --prefix src/Daoris.Web run storybook
```

`test:web` is a declared gate in `daoris.gates.json`, so the devkit's `verify` — and the release
workflow — refuse a release whose UI cannot do its job over the example family. Note it rebuilds the
host, so stop a locally running instance first, or the build fails on the locked DLL.

Set `DAORIS_EMBED_MODEL` to turn the semantic tier on; without it the UI says so and convergence finds
copies and restatements only.

## Verified

Against the real family: **449 entries from 11 repositories**, convergence returning genuine groups —
`phase-review` across two repositories at 0.947, `test-coverage-priorities` at 0.940, `doc-loader` across
three at 0.913.

Convergence is the expensive call: about 31 seconds cold over that corpus, and **5 seconds warm** once
the detector holds its vectors. It was 31 seconds *every* time until the service stopped constructing a
new detector per request — which mattered because moving the threshold is the common interaction, not
the rare one.

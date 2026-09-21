# The platform's front-end architecture

**Status: approved direction from the owner, 2026-09-19 — "this is a long-term project, so a good use
of UI tooling and library is a must; use a design tool; support i18n; use the example project for
testing, with test-in-loop development." Written before the build; D42 records the decision.** This
sits on top of the design language (`2026-09-19-platform-ux.md`, D41): the *look* is settled and keeps;
this settles what it is **built on** so it stays maintainable for years.

## 1. What changes and what deliberately does not

**Changes:** the web app stops being hand-rolled at every layer. It gains a real styling system, real
interaction primitives, a real server-state layer, first-class internationalization, a living design
tool, and an end-to-end test loop over the example family.

**Does not change:** the design language — the paper character, the tokens, the computed status
palette, the console shell — and the boundaries: doctrine unwritable from the UI (D31), the service's
sentences verbatim (D38), the landing on management (D40). **The CLI is untouched**: its
zero-dependency guarantee was never about the web app, and remains absolute.

This amends D41's "not chosen: a component framework" — a decision made for a one-day build, reversed
by the owner for the long term. The character survives because the libraries chosen are *headless*:
they supply behaviour, ours supplies every pixel.

## 2. The stack, and why each piece

| Layer | Choice | Why this one |
|---|---|---|
| Styling | **Tailwind CSS v4** | Tokens become the theme (`@theme` maps our validated palette and scale); utilities keep styles beside the markup they style, which is what survives years of edits. The one growing global stylesheet was the thing that would not scale. |
| Primitives | **Radix UI** (Dialog, Toast, Select, Tooltip, Checkbox) | Headless and accessibility-complete — focus trapping, dismissal, typeahead, ARIA — the behaviour we were hand-rolling, without a look we would have to fight. |
| Icons | **lucide-react** | A maintained, consistent stroke set replacing hand-drawn paths; tree-shaken to only what is used. |
| Server state | **TanStack Query** | The console is a cache over a service. Query gives refetch-on-focus, invalidation after mutations, deduplicated requests, and stale-while-revalidate — the semantics we were approximating by hand, done correctly. |
| i18n | **react-i18next** (+ browser language detector) | The standard; catalogs are per-locale JSON, `en` and `zh` from day one, detection order `localStorage → navigator`, switcher at the activity bar's foot. |
| Design tool | **Storybook** (react-vite) | The design surface wired to the *real* components: every state of every primitive, plus a tokens gallery showing the validated palettes. A mockup tool would drift from the build; this cannot. |
| Test loop | **Playwright** over `examples/` | The example family (D39) becomes the UI's fixture: the suite boots the real HTTP host on a scratch store rooted at `examples/`, and drives the real platform — members visible, a quest composed, taken and finished through the drawers, the verbatim refusal, the language switch. |

Utilities: `clsx` + `tailwind-merge` behind one `cn()` helper — the standard composition idiom.

**Checked against the family's bilingual sibling first.** Its conventions were read (read-only) before
choosing: it also uses react-i18next — so the library is a family convergence, and two of its
hard-won practices are adopted outright: **flat dotted keys** (`keySeparator: false`, so every key
greps from code straight to the catalog) and an **en/zh parity gate** that fails the build when the
two key sets diverge. Its styling stack (a styled component framework behind a wrapper layer, fed by
a hand-duplicated theme file marked "keep in sync") is deliberately *not* adopted: that duplicated
theme is exactly the drift shape this project exists to remove, and headless primitives need no such
copy — the tokens are the only theme.

## 2a. A token is only a token if nothing can express it any other way (2026-09-21)

"The tokens are the only theme" was half true for two years' worth of components, and the half that
was false drifted silently. The **colours** were tokens and held: every component named
`text-ink-soft` or `bg-st-open`, because there was no other way to say it. The **type scale** was a
document, and the components wrote `text-[0.85rem]` — so by the time D56 measured it there were
**fifteen** distinct sizes across 203 sites where D41 named eight, four of them (0.7, 0.78, 0.82,
0.85) in no scale at all. Nothing reported it, because a hardcoded size is valid Tailwind, renders
perfectly, and reviews as a one-character diff.

The rule this leaves: **a scale that a component can express as a literal is a suggestion, not a
scale.** Name the steps in `tokens.css` (`--text-body`), and ship the check that makes the literal
impossible — `tokens.test.ts` fails on `text-[…rem|px|em]` anywhere in the platform. The check is the
half that matters; the tokens alone would have drifted the same way, one component at a time.

It generalises past type. Anywhere the design document names a finite set and the code can write a
value outside it, the two diverge and only a reader notices — eventually.

## 3. Internationalization rules

- **UI chrome is translated; data is not.** Quest titles and bodies, registry summaries, knowledge
  entries, and **the service's own sentences** (refusals, outcomes) are content — they render verbatim.
  Machine-translating a refusal would break the contract that the service's sentence *is* the message.
- **Keys are structural, not English-as-key**: `nav.quests`, `overview.tiles.openQuests.label`,
  `quests.compose.publish`. English is a catalog like any other, so a missing key is visible instead of
  silently "working" in English only.
- **`zh` is 简体**, in the professional register of a console — terse, no exclamation marks; the
  family's own nouns keep their English forms where they are identifiers (`daoris.json`, command
  names), and "quest" translates consistently everywhere it appears.
- Dates and counts go through the locale (`toLocaleString` with the active language).

## 4. The test loop

`npm run test:web` (a root script) builds the web app, builds the HTTP host, boots it over
`examples/` with a scratch store, and runs Playwright against the real bundle the host serves — the
same artefact a person uses, not a dev server with different behaviour.

**Where the line between the two loops falls, and why it is not a gap.** Every surface gated on a
shell (the driver controls, the console, chat, the harness roster and its profile picker) is
*unreachable* from a browser by construction — the bridge is absent, so the query never fires. Those
belong to the Vitest inner loop, which mocks the bridge and can therefore drive them. It is easy to
read "the outer loop cannot reach it" as "the outer loop has nothing to say", and that is the mistake
worth naming: the browser suite still owns two things about any shell-only feature. **What the record
shows** — a session's fields travel over HTTP and render in a drawer, so the whole chain from request
contract to DOM is browser-testable even when the controls are not. And **what a browser must never
learn** — the absence of a machine-local surface is a disclosure guarantee, and a real browser over
the real bundle is the only place it can honestly be checked. SES3 added one of each. It joins `daoris.gates.json`,
so the devkit's `verify` — and therefore the release workflow — refuses a release whose UI cannot do
its job over the example family. Development runs the same suite in watch/UI mode: change, see it
fail, make it pass — with subagents taking mechanical slices (catalog translation, story authoring)
where they help.

## 4a. How a failure becomes a sentence (2026-09-20)

**Two transports, two shapes, one rule: the person reads a sentence, never a code.**

- **The HTTP service answers prose, rendered verbatim.** A refusal from `QuestExchange` or the session
  ledger is the contract — it names what holds a repository, or what to run — and the platform never
  translates or rephrases it. That half is older than this section and does not move.
- **The shell's bridge rejects with a structured `code` and `parameters`**, because the framework's
  contract is that the *client* produces the text (`errors.<CODE>`). That is also what lets a
  machine-local refusal speak 中文, which a verbatim host string never could.

`sentence()` in `format.ts` is the one place that knows the difference: a rejection carrying a `code`
is looked up and interpolated, anything else passes through as the message it already was. **Every
`onError` goes through it** — a site that reaches for `(error as Error).message` directly is the bug
this section exists to prevent, and it is invisible, because that field is populated with a developer
fallback rather than being empty.

One deliberate exception rides the same machinery: **`DRIVER_REFUSED` carries the driver's own
sentence as a parameter** and its catalogue entry is `{{message}}`. `DriverException` is documented as
"a driver error a person can act on", and those sentences name what exists and what to run;
re-authoring each in two languages would mean two copies to drift. A test asserts that entry still
interpolates, because a translation that dropped the placeholder would silently replace every driver
refusal with one fixed sentence.

Adding a refusal is therefore three things, and a test holds each: a code in `Refusals`, an entry in
**both** catalogues, and a throw site using it.

## 5. What Storybook is here, and is not

It is the **design tool**: where states are designed, reviewed and kept — including the states real
data rarely shows (a declined quest with a long note, an empty family, a keyed remote's read-only
refusal). It is not a second app: stories import the shipped components and the shipped tokens, so a
divergence between design and product is a build error, not a discovery.

**Since 2026-09-21 it is also the first loop a component passes** (D52 as amended,
`docs/2026-09-21-working-surface-components.md`): the working surface is built component by component,
and a story for a component that does not exist yet is the cheapest available failing test. Two rules
come with it and apply to anything built that way — **a presentational component imports no hook**, so
every state is reachable by passing props rather than by arranging the world that produces it (a test
asserts the import boundary), and **every story is also a smoke test**, rendered by `composeStories`
inside the existing vitest run rather than behind a new gate row.

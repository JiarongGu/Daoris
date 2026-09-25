# The desktop application's own design — the brief

> Written 2026-09-21, from the owner's direction the same day, after SURF5a landed: **"I still dont
> see good design for the desktop app itself."**
>
> **This is a brief, not a contract.** It records what is observably wrong and what must not move,
> so the session that answers it starts ahead of where this one ended. The *answer* is a design
> pass in the shape SURF1 took — look at what real applications do with the **window**, decide what
> Daoris's own is, and record it as a decision before building. **Confirm the reading with the
> owner before building**, the way D53 and ARCH1 were confirmed.

## 1. What the direction is about

Every region SURF4 and SURF5a built works, and each passed its own loop: a story, a props-only
test, the mocked bridge, and — for the assembled ones — the real window. What is missing is the
**application they sit in**. The components were designed; the desktop was not.

That is a fair reading of how the arc ran. D52 settled the surface and D55 settled the frame, and
both were written about *what regions exist and what binds them*. Neither asked what the window
looks like, and the one item that does — SURF7 — was filed as a mechanism (frameless chrome, the
caption buttons, `IAppMaximizable`) rather than as a design. **SURF7 is a piece of the answer, not
the whole of it.**

## 2. What is observably wrong

From the real-window passes of SURF4d and SURF5a. Screenshots live in
`_fixtures/desktop/screenshots/` and are gitignored — re-take them with `npm run desktop -- shot`.

- **It is a web page in a window.** The OS title bar says `Daoris (道衍)` above a page that then
  draws its own everything. No app chrome, no menu, nothing below the title bar that says *desktop
  application*. This is what SURF7 was filed for.
- **Work borrows Manage's sidebar.** In Work the left column is
  Overview/Quests/Projects/Convergence/Search/Machine — six items belonging to the *other* frame —
  and beneath them roughly half the window's height is empty. The mode switch sits in that column
  because there is nowhere else yet; SURF7 §a moves it to the top strip, which is the reference
  arrangement and would free the column.
- **The attended column is mostly empty.** A head of six `label · value` pairs and a three-entry
  timeline, in a column built for watching. The right dock would absorb the timeline and is
  deliberately unbuilt until the diff gives it a second occupant (SURF6) — so the **interim**
  composition needs an answer rather than a promise.
- **The rail's top is a permanent form.** `StartSession` holds prime vertical space above the
  session list at all times. Every reference in the study puts *new* behind one control.
- **Web density.** D41's spacing was drawn for a management console read at arm's length. An IDE is
  denser, and the frame inherited the looser scale without anyone deciding to.
- **No affordances.** Work has no icons, no hover states worth the name, and no keyboard story. The
  command palette (SURF9) is filed and unbuilt, and the study already argued it is much cheaper
  before the surface count grows than after.

## 3. What must not move

So the answer stays Daoris's rather than becoming somebody else's application:

- **D41's language is the tokens and the validated status palette** — the warm paper, the thin ink,
  the one bronze accent, the six-check colour validation, and *never hue alone*. The **spacing** is
  not in that list and is the thing most likely to need to change; a denser scale is an amendment
  to D41 and gets written down as one rather than drifting in.
- **D42's stack** — headless primitives, tokens as the theme, Storybook over the shipped
  components, the i18n parity gate, and the component plan's dependency rule (**a molecule imports
  no hook**), which is what made every state of this surface reviewable in the first place.
- **No editor, at any point** (D55). That boundary is what keeps this a driver rather than a fork
  of somebody's IDE.
- **No model named anywhere** (D24), and no mid-run approval gate (D37/D52).
- **Structure and geometry may come from a reference; pixels and identity may not** (components
  §3a). The same rule that governed the deepseek-harness adoption governs whatever this pass reads.
- **The disclosure boundary** (D47 §4): over a keyed remote the Work frame is not rendered at all,
  and a browser sees the record and never a stream, a tree path or a diff. Playwright holds the
  negative.

## 4. What the answer owes

- A **reference pass** on the window rather than on the regions: what applications of this class do
  with chrome, menu, density, and the first ten seconds of a launch.
- A **decision** recorded before building, because this changes D41's spacing and D55's frame and
  both are written down.
- A **real-window loop**, since `npm run desktop -- shot` is the only instrument that can see any
  of it. Note the wrinkle: the running shell holds `Daoris.Desktop.Modules.dll` open, so the order
  is `kill`, then `build`, then `run`.

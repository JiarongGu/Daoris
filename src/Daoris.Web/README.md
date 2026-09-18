# Daoris.Web — the platform: the person's window over the family

**Status: built.** A React application over `Daoris.Service`, served by `Daoris.Service.Http`. Both open
questions in the original brief are settled — as `docs/DECISIONS.md` D30 and D31 — and it has since
grown into the platform (D38, `docs/2026-09-19-platform-design.md`): knowledge, tasks and setup in one
place.

## What it is

Five views, three halves of one job — landing on management (D40):

| View | What it answers |
|---|---|
| **Overview** | the landing: is anything sitting and for how long, the family's health as tiles, the repositories by what the index holds |
| **Quests** | what has been asked of whom, grouped by where it is in its life; publish, take, done, decline |
| **Projects** | who is in the family, what each owns and accepts as scannable chips — and who cannot be asked yet, with the join steps proposed as text |
| **Convergence** | where two repositories reached the same conclusion independently — the knowledge half's lead view |
| **Search** | what the family has already learned about X |

## The landing is management; convergence leads the knowledge half (D30, D40)

The platform lands on **Overview**, because its first job is the person's first question — *is
anything sitting, and for how long* (D40). D30's measured finding stands inside the knowledge half:
**search must not lead it**, because the finding that mattered most was a convergence between two
repositories whose vocabulary overlapped by **25%**, and no search could have surfaced it — to search
for it you must already know it exists. So Convergence leads the knowledge views, and Search follows
for when you know what you are looking for.

The similarity threshold is a slider rather than a constant. Measured on this family, 0.82 returns
nothing, 0.75 returns the true pairs, and 0.60 begins pulling in unrelated documents — a default nobody
can move would be wrong for someone.

## Doctrine reads; service state writes (D31, D38)

No editing of doctrine from the browser, ever. Where a rule should change the UI shows what to run in
the repository that owns the file, because `upstream` deliberately routes an improvement through the
repository that found it, where it meets that repository's review. The convergence detector already
states this for itself: it proposes, a person disposes, and a candidate is a prompt to look rather than
a merge (D21).

Quests are a different kind of thing — service state (D32), already writable over the HTTP surface
(D36) — and under the automation-first model, **filing a quest is how a person sets a target** (D37).
So publish and respond are in the UI, through the same key-gated endpoints and the same `QuestExchange`
judgement as every other door, with refusals shown verbatim. On a deployment that sets
`DAORIS_SERVICE_KEY` the browser has no key to present, so the platform is honestly read-only there
until person-auth exists (SVC2); locally — the default — the full surface works.

**The active tier is stated on every screen**, never implied — a reader looking at results has no way to
know the semantic half was absent, and would read them as complete rather than as
complete-for-word-overlap (D24).

## One UI, two shells

This app is the **only** UI. It is served over HTTP for the browser, and the same build is intended for
`Daoris.Desktop`. Two shells, one codebase; a second hand-written desktop UI would be the same divergence
problem in a new place.

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
```

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

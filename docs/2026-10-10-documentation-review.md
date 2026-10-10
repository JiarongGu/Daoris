# Documentation system review — 2026-10-10

## Scope

DOCSYS1: the owner's request to review the entire documentation system after many development
sessions, plan the work, compare documentation with current code, and repair drift. This record
holds the audit and its verification; current guidance belongs in the documents it reviews.

Initial working tree: clean. Discovery: `doc-loader`, `set-up-documents`, `pattern-finder` and
`post-feature`. Read: the document declarations, router, orientation index, development-documents,
claims-need-checks, autonomous-development, D45, D122 and D127. Historical evidence and decisions
remain records of their moment; newer amendments and open tasks determine current standing.

## Plan and progress

1. Inventory tracked documentation by role, generated ownership, reading cost and routing coverage.
2. Check current entry points, commands, configuration and component guides against implementation,
   tests and later decisions. Record each confirmed discrepancy with its evidence.
3. Refresh current guidance, repair contract pointers and routing, and preserve historical wording.
4. Make structural drift detectable using the repository's existing document/tooling conventions.
5. Regenerate owned indexes, run reached gates, review the diff, and move DOCSYS1 to the archive.

Steps 1–5 are complete; DOCSYS1 has moved to the task archive.
The shared brief and contributor guide now have separate homes; the
former CLAUDE guide and roadmap are preserved in `docs/archive/`. No product behavior or release
state is being changed by this review. D117's broader layout/rooms migration remains open.

## Confirmed drift

| Area | Evidence | Repair |
|---|---|---|
| Shared session startup | AGENTS held only doctrine; CLAUDE carried the long project guide. The manifest supports a brief ceiling (`documents.ts`) | Local brief in AGENTS, generated import in CLAUDE, procedures in development.md; preserve the former guide |
| Forward sequence | ROADMAP still called rotation held for measurements and conversations open after their completion notes | Replace arc/status retelling with current work ordering; preserve the old roadmap |
| Router standing | Pause, history clearing and orientation said nothing built; D132/D151/D153 and their service/driver/UI code carry implementation | Route to dated decision notes and open tasks rather than repeat mutable build status |
| Platform guide | Separate Search/Convergence and Settings Plugins contradict `commands.ts` and `settings/domains.ts` | Document Knowledge's two modes and Plugins' own view |
| Doctrine safety prose | Root README claimed every command fetches nothing and anything outside the lock is invisible; management and local indexing contradict that breadth | Scope offline and ownership claims to doctrine/materialized files |
| Freshness declarations | Gate configuration tracks only CLI, service Core and devkit Core; desktop/web and host changes have no guide mapping | Cover all maintained component guides and their behavior-owning source paths |
| Host startup guidance | MCP Program's comment still said user-profile storage despite D63 and its environment contract; the web run recipe omitted its required store | Correct the comment and identify the development-store requirement in the web guide |

## Inventory and maintenance decision

The first complete inventory after adding the guides contained 560 Markdown files: 76 agent
materials (vendored and local), 20 guides, 161 records, 40 canon files, 104 on-demand documents,
9 historical files, 133 generated index files and 17 fixtures. Adding D159 changes that total;
`node tools/doc-system.mjs --json` supplies the current inventory rather than a maintained count.

D159 records the placement and check decisions. The old CLAUDE guide and roadmap remain as body
snapshots with an added historical banner. The former router is also preserved, with its archive
link adjusted after relocation. Its 4,186 words became 1,654 in subject tables without losing routes;
the manifest now declares its 2,500-word ceiling. No decision, fix or task history was compacted.
The existing shape report found 195 post-cutover archive outcomes over 60 words. That advisory
does not authorize rewriting their evidence; new entries use the short outcome shape (D127).

The tool follows `doc-duplicates.mjs`: dependency-free, pure exported audit, `isMain` guard and
read-only CLI. Six regressions failed against an empty audit before implementation. The resulting
seven tests exercise missing contracts, stale inline paths, nested guides, archive coverage,
missing/empty guide mappings, duplicate mappings and generated/fixture exclusions. `verify` invokes
both the audit and its tests. This covers structure; the source comparison covers the prose.

## Source comparison

Current guidance was compared with CLI dispatch/package scripts, doctrine ownership and offline
tests, the manifest document schema, web `commands.ts` and Settings domains, Vite output/proxy
configuration, HTTP host mode/key handling and routes, workflow bindings/gates, pause and history
readers, the service's orientation/decision readers, declared gates, merge-tool usage and the
manual release workflow. Contract standing was checked against D118/D127/D129/D132/D134/D137/
D142/D150–D158 and their dated notes. Original design-time test limitations stay in place.

## Coverage and limits

An inventory or link check is not a semantic review of every historical paragraph. The findings
above name the documents and implementation checked. Installed behavior, vendor tools and
owner-only proofs retain the limits recorded in their own evidence and backlog entries.

## Verification

The first `npm run verify` passed CLI types/tests, doctrine, budgets, shapes, duplicates, routing,
tooling tests and release references. The final universal docs gate refused the newly declared
desktop/web mappings because it reads HEAD's old guide dates, not uncommitted edits. This is a
commit-date basis issue, not a semantic verdict on the refreshed prose. Universal gates passed
against the proposed commit's tree and dates without changing HEAD.

A later attempt to run the entire baseline under the miniature Git environment was invalid:
fixture tests inherited `GIT_DIR`/`GIT_WORK_TREE`, and `git init` redirected a fixture commit to the
local `main` reference. The working files stayed intact. After the run ended, the reference was
restored from `03b01732` to the recorded starting `5388a364`, and the index restored with `read-tree`
(no checkout or reset). Test failures from that run are not product evidence. Only the universal
commit-date check may use this preview; fixture tests must run with the ordinary Git environment.

The subsequent ordinary baseline passed CLI types/tests, doctrine, all document checks, 50 tooling
tests and release references; only the same HEAD-date signal remained. The restricted universal
preview then passed all five gates: sensitive, version, five guide dates, 56 links across 562
Markdown files, and doctrine. No hook or gate was bypassed. The development guide records the
Git-environment boundary; the scratch preview was restricted to universal verification. Its helper
was removed after use. Execution policy rejected both recursive and explicit nonrecursive deletion
of its ignored metadata directory; DOCSCRATCH1 records that cleanup in the backlog.

Additional verification: service 1,758 and HTTP 245 tests passed; devkit 85 tests passed;
orientation index fresh (133 generated files), code map fresh (18 modules/17 dependencies),
six document budgets within their ceilings, routing inventory without findings, and `git diff
--check` clean. Shape reports retain their historical advisory overruns. Post-feature reviewed
the actual diff, counterpart wiring, preserved snapshots and record roles; no implementation
or record work remains for DOCSYS1. Full process/UI/deployment rehearsals were not run for this
documentation change; their existing product proofs remain separate.

Committed-tree confirmation: after `6ffc0e2f`, ordinary universal verification passed all five
gates with five guides current and 56 links across 562 documents. Routing and orientation freshness
also passed, with a clean working tree. The preview's branch-reference incident was fully local;
no push occurred.

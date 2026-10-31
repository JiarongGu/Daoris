# KNOWCENTRAL1: Codex's sealed proposal

> Router standing, moved here when the router was cut to its size (DOCSIZE1): used by D220.

One of two sealed proposals written from `docs/2026-10-30-memory-currency-brief.md` under D191, before its author read the other (Lyntai's
session writes the second in its own repository). Added verbatim on 2026-10-30 and never edited; read read-only in Lyntai.

Keep explicit validity and interference decay as independent axes. Models should discover and propose changes; confirmed changes should end named claims while preserving their evidence. The coordinator should own authority, checks and distribution. Lyntai should supply neutral storage and memory operations.

References prefixed `fa30715f:` identify the unlanded branch. MC1–MC5 are proposals, not shipped capabilities (`fa30715f:TASKS.md:389`).

**1. Retirement: retain MC1’s ending; reject retirement through decay.**

Burial answers whether something deserves recall space. Ending records a writer’s statement that it ceased to hold. D40 measures interference since use; D41 makes burial relative and reversible. Neither establishes currency (`docs/DECISIONS.md:974`, `docs/DECISIONS.md:989`).

The owner’s proposal preserves recoverability and avoids deletion. However, MC1 already preserves recoverability. Burial adds no dependable exclusion: a frequently requested stale fact gets refreshed, and equally ranked predecessor and successor can survive together. Authoritative entries are exempt altogether. A persistent penalty that guarantees exclusion would introduce another eligibility state rather than reuse ordinary decay.

| Channel | Current recall | History and recovery |
|---|---|---|
| Decay and burial | The predecessor remains eligible; another query or later use can restore it. | Evidence remains, but decay supplies no validity boundary or replacement reason. |
| Confirmed ending | The predecessor is excluded after its stated end, independently of rank; the successor remains eligible. | `History` admits both; `AsOf` admits the appropriate interval; explicit expansion retrieves the ended entry. |

These are eligibility guarantees, not promises that bounded retrieval finds every eligible entry. Ended entries remain traversable, and accumulated endings can shorten candidate pages (`docs/memory.md:880`; `src/Lyntai.Core/Memory/Engines/GraphMemoryEngine.cs:675`).

Retain MC5’s failed-check ending **only when “Failed” means the host confirmed that the named claim no longer holds**. Timeout, missing access or an ambiguous answer is inconclusive: log and retry in the coordinator, without changing validity. Do not infer an earlier cessation instant from today’s failure.

A passing recheck should stamp confirmation without counting as use, as MC5 proposes. Otherwise scheduled maintenance keeps every checked entry fresh regardless of demand. Reject that default; evaluate any explicit verified-use policy separately (`fa30715f:docs/memory-currency-study.md:122`).

Decay alone should continue burying unused associative material. It cannot promise that nobody will retrieve it again.

**2. Upkeep: permit autonomous proposals; require evidence for consequential application.**

“Writer confirmed” must mean more than the proposing model calling another tool.

| Step | Initial authority | Measurement before relaxing it |
|---|---|---|
| Find candidates | A model may search alone within host-granted bounds. MC3 returns candidates without mutation. | Predecessor coverage, omissions, misleading candidates, latency and token cost, separated by retrieval basis. |
| Propose `Supersedes` | A model may propose exact returned references and supporting evidence alone. Applying it requires a source check or person. | Wrong predecessor selection, false endings, missed replacements and preservation of unaffected claims. |
| Propose `Amends` | Autonomous proposal; accepted relation requires checked source evidence or a person. | Confusion between partial amendment and complete replacement; unchanged-claim recall. |
| Propose `Disputes` | Autonomous, attributed hypothesis; accepted relation requires checked evidence or a person. Neither hypothesis nor accepted relation ends an entry. | False disputes among compatible claims; readers’ treatment of uncertain marks. |
| Run `Recheck` | The model may request an approved check. The host executes and interprets its registered predicate. Semantic checks require a person initially. | Predicate coverage, false passes/failures, environmental failures and repeatability. |
| End | Host applies a checked, authorized proposal; unresolved semantic cases require a person. | False retirement, audit completeness, concurrent changes and correction recovery. |
| Bury | The engine applies its configured relative ranking floor autonomously. A model gets no separate retirement-by-burial power. | Miss and pollution across repeated walks, stale-fact resurfacing and authoritative survival. |

MC3’s autonomous discovery is justified because it changes no entry. D109’s relevance judge endorsed superseded facts **3.4 times** as often as current facts; that evidence still binds any model proposing retirement (`docs/DECISIONS.md:3102`).

Likewise, use host-issued references and source identifiers, never model-invented identity: D217’s predecessor-key match was only a **quarter** of questions. Visible references remove free-form identity generation, but do not prove correct predecessor selection (`docs/DECISIONS.md:6197`).

Compare deterministic source checks, an independent model reviewing bounded claim pairs with evidence, and person review. Model review costs—tokens, latency, memory and correction effort—remain unmeasured here. Agreement alone earns no promotion. Advance a narrowly defined action class only after evaluation against independently adjudicated cases meets preregistered error bounds. Disagreement abstains. Reject both blanket human review and unmeasured model consensus.

**3. Shared memory: one owner, explicit readers, coordinator-enforced grants.**

Store a shared fact once under an owner-controlled namespace. Readers receive references and revision information; their observations become proposals to that owner, not competing copies with authority to end it.

Task keys remain isolation boundaries. Use a deliberately shared task for genuinely shared material, or combine separately authorized reads in the coordinator. Reject cross-task links as federation: Lyntai refuses them (`docs/memory.md:685`). Choose engine boundaries deliberately because interference advances per engine.

The coordinator authenticates callers and enforces read, propose, write and end grants on **every operation**, including expansion by reference. A scope string is insufficient authorization; current expansion is addressed by engine and reference (`src/Lyntai.Core/Memory/MemoryTools.cs:134`; `docs/DECISIONS.md:6227`).

An accepted owner change enters a durable coordinator change log. Reader subscriptions invalidate cached material; readers reconcile missed notifications through a cursor and check revisions before presenting cached knowledge as current. Notify does not silently rewrite another repository.

Lyntai owns reference identity, validity, optional capabilities and store consistency. The coordinator owns repository ownership, grants, subscriptions, scheduling and delivery guarantees. Reject embedding repository names, approval policy or a distributed service into the library.

**4. Records: keep the canonical record index separate; let memory cite it.**

Do not ingest every decision, knowledge document and backlog row as an authoritative memory entry. Authoritative grade protects against decay, truncation and recall-budget competition; it establishes neither currency nor complete retrieval (`docs/DECISIONS.md:1269`; `docs/memory.md:1274`).

The coordinator’s index should retain stable record identifiers, source revisions, locations, standing and explicit replacement/amendment marks. Current retrieval resolves those marks and returns the applicable amendment context alongside the source. Memory citations are revalidated against that index.

A whole-record replacement mark can retire that record’s current projection. An amendment mark must retain the original’s unaffected material. Ending whole amended entries cut amendment-claim recall from **66% to 14%**; reject automatic document-wide supersession (`docs/memory.md:717`).

Selected claims may later receive authoritative projections when the owner supplies stable claim identity and explicit replacement semantics. Those projections remain rebuildable from records. A model’s summary or extracted key cannot become the canonical record.

**5. Build order: establish identity and safe mutation before autonomous upkeep.**

Revise the proposed sequence (`fa30715f:docs/memory-currency-study.md:159`):

1. **MC1 first.** Preserve reasons, history and existing refusal rules across stores and portability. Add an optional conditional mutation contract so a confirmation cannot end state changed since review. Measure races, retries, correction recovery and partial failures; current successor insertion and predecessor ending are separate operations (`src/Lyntai.Core/Memory/Engines/GraphMemoryEngine.cs:296`).
2. **MC3 next.** Measure predecessor coverage before granting models consequential tools. Keep lookup read-only; reject low coverage being disguised as reliable replacement.
3. **MC4 before broad MC2 deployment.** Give writers a safe way to express partial change and disagreement. Prove relation meaning survives co-activation, decay and storage; measure unaffected-claim recall.
4. **MC2 behind coordinator enforcement.** Test grants, foreign references, stale confirmations, tool refusals and attribution. Compare bounded model proposals with checked and person-reviewed outcomes.
5. **MC5 after failure semantics are explicit.** Measure false endings, inconclusive runs, confirmation coverage and checking cost. Keep execution outside Lyntai.
6. **Pilot record indexing and shared distribution.** Measure amendment-context completeness, reader convergence, missed-notification recovery and unauthorized-operation refusals.

Evaluate recall through repeated n-shot walks with paired, counterbalanced arms: recall itself mutates the graph (`docs/memory.md:627`). Report stale answers, lost current claims and lost historical evidence separately. Reject one aggregate accuracy score as permission to trade away an invariant.
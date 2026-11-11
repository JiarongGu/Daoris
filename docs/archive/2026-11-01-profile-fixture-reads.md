# PROFILEFIX1: the read and the settlement (2026-11-01)

**Standing:** Codex's read under D191 of `2026-11-01-profile-fixture-proposal.md` (the Opus maker's, recommending no), and the steward's settlement: decided no, today, with a re-open condition.

## Codex's read

**Verdict: accept with named changes.** Keep the recommendation against a fixture today; qualify the history argument, correct the acceptance argument, and make re-opening executable. No reviewed fact demonstrably meets all three conditions: single-valued, stated by both person and sessions, and lacking a declared home.

1. **The existing homes support “no, today.”** Domain, doctrine, document roles, gates and lanes are declared in `daoris.json`, `daoris.gates.json` and `daoris.lanes.json`. The coordinator explicitly rejects ingesting records (`design §5`), consistent with D220. **Change:** frame the conclusion as “no unmet exact-read requirement demonstrated for these fields.” Having an authoritative home does not itself disqualify a derived projection; the proposal needs the stronger, supported point that no consumer presently needs one.

2. **The history establishes less than §4 claims.** A file holding one value per key cannot establish that nobody disputed that value. The current memory operation accepts free text and citations, without a schema field identifying the fact (`MemoryOps.cs:52`). That permits differently worded claims; it does not prove they occur. The proposal appropriately admits actual memory use was unverified. **Change:** replace “no disputed value” with “no disputed-claim trace found in the inspected declaration history.” Treat incomplete event coverage as a limitation of this fixture candidate, not independent evidence that profiles cannot serve it.

3. **Whole-file acceptance does not imply per-field approval.** `DriverConfig.cs:440` binds acceptance to a declaration blob. Nothing there requires an observational profile projecting that blob to introduce separate approval for every gate. **Change:** replace §5’s “would ask the person to accept each” with a requirement that any projection preserve whole-blob acceptance and existing execution authority. This removes an unsupported consequence without creating a reason to adopt.

4. **The re-open condition needs a concrete evidence packet and a discovery step.** Non-adoption alone is insufficient: `CanonicalLine.cs:14` resolves repository settings, workspace settings and checkout guesses without checking adoption. `HeavyMapping.cs:34` also provides a machine home for undeclared heavy test commands. Moreover, `KNOWCENTRAL2i’s measurement` does not currently require looking for repeated conflicting facts.

   **Change:** explicitly add that observation to the measurement. Re-open upon one documented candidate naming its subject, field, scope, person/session statements with timestamps and sources, conflicting live entries where applicable, absence of an existing home including machine settings, and a coordinator query with its expected answer. Compare a profile against existing curated supersession before counting Daoris as an application.

**Re-open assessment:** directionally sound, insufficiently operational today. The evidence packet makes it actionable; re-opening should trigger another evaluation, not automatically establish application status. I judged the unavailable Lyntai account on its stated terms. No files were changed or `local/` directories read.

## The steward's settlement (2026-11-01)

**Decided: no, today.** Daoris's repository facts are not offered as the first application of Lyntai's schema-first profile. No consumer in Daoris has an unmet need for an exact, current read of a single-valued repository fact: each fact read has a declared home (the declaration files, the person's settings and their precedence in code, the decisions), and the memory coordinator cites records, never ingesting them (D220; the coordinator design §5). A home does not of itself rule out a derived projection; the reason is that none is needed. The declaration history inspected shows first statements and corrections only; no disputed-claim trace was found there, which is a limit of that history, not evidence that disputes cannot occur in memory. Any later projection of a declaration keeps its whole-file acceptance and its execution authority (DriverConfig's accepted blob), adding no approval per field.

**Re-opens** on one documented candidate: its subject, field and scope; the person's and sessions' statements of it with times and sources; the conflicting live entries where they exist; the absence of any home for it, machine settings included (`CanonicalLine`, `HeavyMapping`); and a coordinator query with its expected answer. A candidate is first compared against curated supersession (2h); only a fact that supersession cannot serve counts toward D221. KNOWCENTRAL2i's installed measurement looks for such candidates: repeated, conflicting entries about one named fact.

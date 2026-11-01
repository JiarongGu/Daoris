# STAFFPOLICY1 — Codex's read of the design, and the steward's settlement

Status: read of `docs/2026-11-02-staff-policy-design.md`, 2026-11-02, beside row a; Codex, read-only, kept as it came. The
settlement is the design's §11, which takes every finding.

Read-only review. No files changed; no builds or tests run.

**Blocking findings**

1. **Rows a–b do not protect hard policy restrictions from older builds.** New tables hide the policy, but older builds still read runnable choices from `role_staffing` (`src/Daoris.Desktop/Daoris.Desktop.Driver/Roles/RoleStaffing.cs:185`). They can start a choice beyond its budget or outside its permitted work, without recording `role_starts`. The unchanged writer stamp permits their writes too (`src/Daoris.Desktop/Daoris.Desktop.Driver/Store/DesktopStore.cs:723`).

   The fallback is explicitly accepted at `docs/2026-11-02-staff-policy-design.md:346`, but it bypasses more than weights. Rows a–b need an executable rollback boundary for restricted policies, tested through older start paths. Also, a content digest cannot establish that **any** outside write makes the policy stale: identical writes, or changes reverted to the original tuples, match again. Correct that claim in `docs/decisions/D226.md:5`.

2. **Row b preserves a partial-write window across the person’s complete role change.** The design deliberately retains the separate catalogue write (`docs/2026-11-02-staff-policy-design.md:57`). Today, `RoleDoor.Apply` checks the revision, commits staffing, and only then writes the catalogue (`src/Daoris.Desktop/Daoris.Desktop.Driver/Roles/RoleDoor.cs:52`, `:64`, `:69`). The catalogue judges again inside its later transaction and can refuse (`src/Daoris.Desktop/Daoris.Desktop.Driver/Roles/RoleCatalog.cs:374`).

   Consequently, a concurrent change or interruption can leave new choices and policy active against the old role’s work or scope—even when the complete press fails. Row b needs atomic revision checking and publication of the role, staffing and policy, or a staged change that cannot run before publication. Its crash proof must cross the boundary between those writes.

3. **Row d’s arranged hand-over offer can start a different model from the one approved.** The design changes the offer’s order (`docs/2026-11-02-staff-policy-design.md:368`), but today the page sends only agent and account, dropping the displayed choice/model (`src/Daoris.Web/src/work/WorkFrame.tsx:2303`). `HandOverRequest` carries neither model nor effort (`src/Daoris.Desktop/Daoris.Desktop.Driver/Course/Domain/HandOverRecords.cs:31`). A handed agent then receives its **first stored** matching choice (`src/Daoris.Desktop/Daoris.Desktop.Driver/Roles/ActorChoices.cs:68`).

   If weighting offers an agent’s second model, the press can therefore start its first model instead. Carry the selected tuple through the approval, durable request and execution, and revalidate it without silently selecting another target.

   The same seam cannot implement every promised `ask`: existing offers exclude the current tool (`src/Daoris.Desktop/Daoris.Desktop.Driver/Course/Domain/HandOverAsks.cs:153`), and the course rejects the same agent/account (`src/Daoris.Desktop/Daoris.Desktop.Driver/Course/Domain/LineCourse.Decide.cs:456`). A policy-directed move between that agent’s models needs an explicitly defined press.

4. **Row e cannot derive all promised moves from part links.** The design counts in-start model/sign-in replacements toward `most`, then derives that count from parts’ `from_why` (`docs/2026-11-02-staff-policy-design.md:158`, `:205`). However, a same-account model replacement updates the existing session’s choice and asked model (`src/Daoris.Desktop/Daoris.Desktop.Driver/Loop/Driver.cs:4404`). It creates no new part link. Sign-in replacements also rerun the original start directly (`src/Daoris.Desktop/Daoris.Desktop.Driver/Loop/Driver.cs:1485`); `ContinuesFrom` returns no link without a carried record (`:5382`).

   Specify a durable count covering these replacements, its logical-line identity, and restart recovery. Reconcile the existing same-session reselection with the design’s universal “new part” statement. Test mixed refusal and condition moves against the bound.

5. **The rows expose policy settings before their enforcement is complete.** Row b accepts the policy; c enables starts; d adopts other walkers; e adds conditions and token budgets. Yet f’s complete editor depends only on b and c (`docs/2026-11-02-staff-policy-design.md:386`–`:390`). A saved token budget or `wait`/`ask` rule can therefore exist while execution still follows the preceding behavior.

   Keep activation dark until the necessary walkers and conditions land, or refuse unsupported fields with explicit capabilities. Make the complete editor depend on d and e. The same ordering must preserve participant fences: reviewers currently consume `plan.Order` through their separate maker-partitioned walk (`src/Daoris.Desktop/Daoris.Desktop.Driver/Participants/Domain/ReviewerChoice.cs:289`, `:337`).

6. **The new person holds have no specified answering and attention path.** The design says `ask` offers a choice, and `held.moves-bound` waits for the person, while declaring that no wait kind changes (`docs/2026-11-02-staff-policy-design.md:178`, `:225`). Existing attention does not discover arbitrary holds: the failed-quest row explicitly recognizes `Exhausted` (`src/Daoris.Desktop/Daoris.Desktop.Modules/Loop/NeedsAPerson.cs:295`), and session waits have a closed union (`src/Daoris.Web/src/work/waits/waits.ts:25`).

   Rows e–f must name an existing typed wait they extend, or introduce one, and carry its answer through the head, foot and attention view. Define what one approval releases at the moves bound; generic *Try again* must not silently renew unlimited automatic moves.

7. **Row b misses the terminal mutation classifier.** Adding `role policy` to `RoleVerbs.cs` is insufficient. `DriverChoicesCommand.Changes` recognizes only `role set`, `remove` and `default` (`src/Daoris.Desktop/Daoris.Desktop.Driver/Settings/DriverChoicesCommand.cs:255`); commands classified as reads enter `Said` before `Plan` (`:298`).

   Add `role policy` to that classifier and the terminal route census (`:127`). Test that it reaches `SET_ROLE` with the existing person authority, including from a session terminal.

**Non-blocking findings**

- **Keep reservation effects outside the pure judge.** `Arrange` is declared pure, but later reserves under a lock (`docs/2026-11-02-staff-policy-design.md:129`, `:148`). Make the ledger coordinate snapshot, arrangement and reservation; the judge should return values.

- **The editor contradicts the condition table.** Sign-in and model refusal allow configurable actions at `docs/2026-11-02-staff-policy-design.md:185`, but the wireframe calls both unconditional “Always” behavior (`:261`). Show their controls or explicitly narrow the supported configuration.

- **Row e needs a per-run retry bound.** `TransientRetry.Bound` is currently static (`src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/TransientRetry.cs:28`), and both ACP retry paths read it (`src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/Acp.cs:1688`, `:1784`). Name those changes and test simultaneous driven and conversation episodes so shortening one preserves the other’s five tries.

- **Strengthen budget proofs.** Specify stable resolved-role and tuple attribution through `TurnBinding`, rather than assuming the existing actor/choice fields suffice (`src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/UsageTurns.cs:19`; `src/Daoris.Desktop/Daoris.Desktop.Driver/Agents/UsageHistory.cs:637`). Add concurrent last-slot and fallback-reservation tests, plus reorder and continuation attribution tests.

Changes the rows should carry: executable rollback protection, atomic policy publication, exact approved choice delivery, durable move counting, enforcement-gated activation, answerable policy holds, and terminal mutation routing.
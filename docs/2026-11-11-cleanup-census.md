# Cleanup census, 2026-11-11

A census of the logic the day's changes superseded (D228: one way to do each thing), by family: what was removed, and what
remains with the condition that ends it and the row that deletes it. Paths are repository-relative; line numbers are this
branch's.

## 1. The per-repository trees switch (PLACEWORK1d, D254)

**Removed.** The `trees` verb and its refusal: `DriverChoicesCommand.TreesMoved`, its `case "trees"`, its entry in `Verbs` and its
usage line (`src/Daoris.Desktop/Daoris.Desktop.Driver/Settings/DriverChoicesCommand.cs`); the place group's `trees`
(`Setup/PlaceCommand.cs:16`); the help's `place trees (gone: …)` (`Loop/Usage/DriveUsagePlace.cs:24`). `daoris place trees` is now
no verb (exit 2, as any unknown word). The text fixtures (`Driver.Tests/fixtures/driver-choices-text.json`,
`driver-workflows-text.json`) and `DriverChoicesCommandTests.Trees_is_no_verb_and_writes_nothing` hold it; driving rule 12 says so.

**Remains, current.** The state's `trees` (`Modules/Bridge/Routes/DriverStateAnswer.cs:46`, web `bridge/driver.ts:42`) is read from
each place's `isolate` step, not the old key. A conversation's `ownTree` (`Web/src/work/start/NewConversation.tsx:268`) is its own
start's choice. `tools/acp2-proof.mjs:227`, `int4f-proof.mjs:173`, `perm2-proof.mjs:159` and `deployment-rehearsal.mjs:1598` still
write `trees` into a scratch `driver.json`, which the migration converts and no reader reads: left to their owners' next run.

**Remains, transitional.** `Transitional/Migration/PlaceWorkStep.cs` converts a home's `trees` and `overlap` into the step: this
release's migration (`nocompat1`), replaced by the next release that changes a stored shape (install area rule 54).

## 2. The landing as the person's state, and the session states (REVIEWUX1, D273: being designed)

Listed for the review redesign, not removed:

- The `review` group (*To review*): `Driver/Workplace/Domain/SessionGroup.cs:13` and its order (`:28`); web `work/groups.ts:14`,
  `GROUP_ORDER` (`:17`); `app/opener.ts:39`'s `review`; `work/attention/attention.ts:25`'s `ready`; the words
  `work.group.review` (`locales/en/work.json:76`, zh beside it).
- The nine record states the five (pending, working, needs you, done, failed) will replace:
  `Service.Core/Sessions/Records/SessionState.cs:10-37` (`Queued`, `Starting`, `Working`, `AwaitingPerson`, `Completed`, `Declined`,
  `StoodDown`, `Failed`, `Stopped`); driving rule 36 (`docs/areas/driving.md:425`); the groups' `shown` words (`work/groups.ts:24`).
- **Removed** here because no verb or route sets it: the landing rule's door. The room's *set how accepted work lands* row
  (`Knowledge/Room/HelpDoorRowsSetup.cs`, and its five help-room goldens) named `daoris place landing`, which is no verb; the
  refusals that sent the person there now name the workflow's `land` step (`Workplace/LandingAdvance.cs:55,80`,
  `LandingSayings.cs:53,66`, `TreeSquashes.cs:51`); the help's `landing|work-cycle` (`DriveUsagePlace.cs:25`); the words
  `person.route.SET_LANDING` (en, zh) for a route that does not exist, and the desktop README's mention of it.
- **Remains, transitional:** a place still on Current reads `driver.json`'s `landings` (`Settings/DriverChoicesList.cs:48`,
  `Knowledge/Domain/WorkflowFacts.cs:33`), the register's `current-followed`, deleted by row WORKFLOWCLEAN1z.

## 3. Proposal kinds converted to presses (ASKFULL1g)

**Removed.** `WorkflowDraftTold.Lead` (`Driver/Course/Domain/WorkflowDraftTold.cs:153`) told Ask Daoris to call `workflow_propose`,
a tool g-vii removed: it now names the draft in the room's `drafts/` folder and `daoris workflow apply … --base <version>`, held by
`WorkflowDraftToldTests`.

**Remains, current.** `go_propose` (the proposal box's, the sibling's), `handover_propose` (`Service.Core/Sessions/HandOverAskBox.cs`),
`memory_propose` (`Service.Mcp/MemoryTools.cs`) and `permission_propose` (`Service.Mcp/RuleProposalTools.cs`) are live tools of
their own. The tests naming the gone kinds (`HelpRoomMayProposeTests`, `HelpRoomTests:135-154`) assert their absence.

**Left to the sibling removing the proposal box.** The desktop README's `help` row (`src/Daoris.Desktop/README.md:920`) still names
`HelpWorkflowProposals` and `HELP_APPLY`'s `taken` and `left`, gone with g-vii.

## 4. Plugin credentials through a git identity (D274)

**Removed.** The Plugins offer story's sample need, *a git identity's token (Settings → Git)*, now a plugin's own sign-in
(`Web/src/plugins/OfferPage.stories.tsx:13`).

**Already gone** before this census: `DAORIS_HOST_TOKEN` (only `Driver.Tests/Plugins/PlaceHandTests.cs:76` asserting its absence),
`PluginHostToken`, `PlaceHostTokens`, the trial's `host-token` step.

**Remains, current.** `HandRefusals` (`Driver/Plugins/HandRefusals.cs`) now serves the place's hand refused for git access (no
origin, read only, Write denied), not a token.

**Remains, transitional.** `daoris git identity token --kind api` (`Workplace/GitIdentityCommand.cs:47`) and the identity page's
`api` kind: D274 point 5 keeps them until their last reader, the adoption (`Plugins/PluginTokenAdoption.cs`), goes; the row is
PLUGINEXT1 (*the identity's `api` token setter removed*). The git area says so (`docs/areas/git-and-remotes.md:581`).

## 5. Transitional migrations and compatibility paths

**Remains, transitional.** `HomeMigration.Steps` (`Transitional/Migration/HomeMigration.cs:26-55`), the release's one migration
`nocompat1`, no release cut since it began: each step goes when the next release that changes a stored shape replaces it. Install
rule 54 now names all 28. `ConfiguredAccessStep` (`:9` of its file) says its own end: the owner's install finding neither old list.

**Remains, to a row of its own.** The older build's move boxes, still imported (`Course/MoveBoxImports.cs:27`, `Chat/LimitBox.cs:8`,
`HandOver/HandOver.cs:4`, `Chat/ChatGoOn.cs:155`, `Chat/LimitWaits.cs:149`): the launcher refuses an older build on a home a newer
one wrote (`Install/HomeLastBuild.cs:8`), so no new box is written; what is left is a box an older build wrote before the update.
They name no condition or row today; a migration step importing every box once, then these readers deleted, ends them.
`Instance/SentPresses.cs:46` reads presses where an older build kept them, the same way.

## 6. Dead code and words

**Removed.** `src/Daoris.Desktop/README.md.rej`, a merge's rejected hunk the README already holds. The 53 `projects.supervisor.*`
keys in en and zh, read by nothing since the separate appointment cards were deleted (df0905dbd1), with the glossary, names and
audit lists that named them.

**Found, not removed (no reader in the page's code, by a scan of the catalogue against every non-test source; each family wants
its own check before it goes):** `settings.daoris.steward.*` (40 of 41, `locales/en/settings.daoris.json:107-147`, the same deleted
cards), `notifications.supervisor.*` (10, `notifications.json:96-105`), `notifications.standing.*` (5), `map.said.*` and
`map.list.*` (24, `map.json:62-108`), `agents.roles.appointed.*` (6), `settings.manifest.*` (6) and about 40 single keys.

**Red on main, not this branch's.** `DriverChoicesTextTests`' case *role, every role the seed's* lacks the `second-check` seed's
line that ASKSUPERVISE1 added (`Driver.Tests/fixtures/driver-choices-text.json:2614`).

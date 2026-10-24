# MODELROLE1d2 build record

Quest `#aec91ccbdc36`, what D205's row d still owed (the backlog's MODELROLE1d2; design `docs/2026-10-23-actors-design.md`
§3.5(a)'s curve move and §3.5(b)), on main after MODELROLE1d, LIMITGOON2 and MODELROLE1c3. The row lands in parts on one branch,
each committed green. This record says which parts are built and which remain.

## Parts

1. **The curve move judges each candidate's reading for the record's model** (§3.5(a); built). `SwitchCandidates` takes the
   conversation's agent, the version its record opened on and its model (the composer's `override` where one is kept, else its
   `asked`), and a candidate whose offer reading lacks or refused that model is not ready (`AgentOffers.Judge`), so the move never
   takes it.
2. **A conversation's pending start owns its attempt** (§3.5(b), `ChatPending`). planned.
3. **A `choice-not-offered` conversation's go-on is a fresh start of its actor** (§3.5(b)'s interruption, a pick, a refused
   continuation's brief). planned.
4. **The window follows the replacement** (`SESSION_REPLACED`, the page). planned.

## Checks run

Part 1: driver `CurveMoveTests` (the new case seen failing first: the move took `account-3`, whose reading refused `opus`), and
`CurveMoveProcessTests` with `process.runsettings`.

## Readings

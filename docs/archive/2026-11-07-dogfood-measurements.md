# DOGFOOD1 measurements — Daoris's rows built by the install's driven sessions

Evidence, dated. Each row Daoris's installed build worked on this repository, measured against a builder's run of a row of the
same size. Figures are the install's own `turn.ended` and `session.ended` readings and the harness's report of the builder; they
are not the same counter, so the token figures compare in kind, not exactly.

## Row 1 — MODELROLE1k2 (2026-11-07)

| | The install's driven session | A builder (MODELROLE1k, the row before it) |
|---|---|---|
| Agent | Claude Code through `claude-code-acp`, Sonnet 5.5 with Haiku 4.5 | a `branch-worker`, Opus 5.5 |
| Wall time | 80 min (queued behind the install's cap of two sessions, then one turn of 65 min) | 39 min |
| Tool calls | 124 | 150 |
| Tokens | 204 input, 20.1 M cache read, 288 K cache write, 61 K output; 288 K of a 1 M context at the turn's end | 232 K (the harness's count) |
| Result | four commits, the row archived, a follow-on row filed (MODELROLE1k3) | two commits, the row archived by the parent |
| Merge | one conflict in a test with main's rename landed meanwhile (`adapter` to `door`), resolved to main's; 99 tests green | clean |

Read: the session did the row whole and filed its own follow-on, at a little over twice a builder's wall time on a machine
loaded by a full CI and four builders. Its cache reads are the cost to watch: one turn of 124 calls carried a context that grew to
288 K, read again at every call. Two process findings: a quest to this repository must name its lanes (the service refuses one to
the repository whole and says so), and a row built from main's tip while other merges land conflicts on what they renamed, so rows
sharing files go one after another, as DOGFOOD1 says.

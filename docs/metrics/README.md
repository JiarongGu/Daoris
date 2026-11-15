# Performance receipts

Landing measures are not kept here per landing: each landing appends a line to the gitignored `local/metrics/landings.jsonl`, and
`tools/dev-metrics.mjs --landings --json` writes their summary (context read before the first edit, change wall time and tokens, main's
red rate and days since green) under the `landings` key of `<date>.json`.

Keep real installed-case receipts here. `tools/dev-metrics.mjs --json` writes `<date>.json`; fixture tests write snapshots only
under `_fixtures/`. The performance specification is [PERFMATRIX1 and RCASE1](../2026-11-13-performance-matrix.md), and
[the process area](../areas/code-structure-and-process.md) names the collector and its declared budgets.

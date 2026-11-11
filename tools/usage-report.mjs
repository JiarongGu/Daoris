#!/usr/bin/env node
/**
 * The usage report: an install's machine log, summarised for a development session.
 *
 *   node tools/usage-report.mjs --home <dir> [--days 7] [--json] [--service <url>]
 *   node tools/usage-report.mjs --install <dir> [--days 7] [--json] [--service <url>]     (the install's `data/` home)
 *
 * ## Why
 *
 * The machine log (`docs/2026-09-30-machine-log-design.md`) exists so Daoris can be improved from real
 * use rather than from a guess. Reading thirty days of JSON lines is not how anyone starts a session, so
 * this reads them for the period and says, in a terminal's width: what got used most (views opened,
 * commands run, panels moved, files previewed by kind), how the conversations and sessions went (how
 * many, by kind and adapter, the median and slowest open and first answer, how long turns took and how
 * they ended), the asks (the permissions each session's harness would have asked a person for,
 * refused because nobody is at the prompt, per session by adapter and repository), the rule proposals
 * (what waits for the person, and those made in the period by week and state), the set-ups
 * (each set-up session's minutes, tool calls, tokens and context), the driven sessions' cost
 * (each one's repository, adapter, models, turns, minutes, calls, tokens read anew, read from the cache,
 * written to it and written out, and peak context, newest first, then rolled up by repository and by adapter and
 * model with the cache read per call; a session with no `turn.ended` is said so, never zero), the parks (the
 * sessions that stopped to ask the person, per week by workspace, beside the sessions started there), the
 * agent actions (each sign-in, install or pin by agent and action, how it ended and each phase's time),
 * what got refused (by code, most first, a harness action's by its action), what failed (exceptions, the page's errors, error-level log lines,
 * failed or slow requests, grouped) and the lifecycle (starts, stops, uptime, versions). `--json` is the
 * same as data.
 *
 * ## Outcomes
 *
 * With `--service <url>` naming this machine's local host, it also says how each quest the period touched
 * went (`docs/archive/2026-10-03-future-directions-review.md` §3, row 4): its sessions and how each ended, its
 * carry-ons, the person's answers on it, its departures and the person's yes, its strikes, the limits it met counted apart, how and when it closed and the time from publish
 * to close; then the same rolled up by repository and by agent and account. It is derived from the stores
 * that hold each fact: the host's quests and session records, each ask's words (three GETs, `RECORD_ROUTES`),
 * and the machine log's parks. Nothing is stored and nothing is written. A report, never a gate: no
 * score folds the counts into one number, and each rate is said with its count, so a small sample says so.
 * A fact no store keeps is said missing with why, never zero. Without `--service`, or
 * when the host does not answer, the section says so and the rest of the report stands.
 *
 * ## What it never prints
 *
 * **Anyone's words.** There are none in the log, and this adds none: it counts and times. The
 * one free text a line carries, a caught error's message, is shown as its first line and never beyond
 * the log's own 120-character cap, so a stack or a paragraph cannot reach the screen through it. A rule
 * proposal holds words (its rule, its reason): only its id, its time and its state are read. A quest, a
 * session record and an ask hold words too (a title, a body, a note, a requirement's quote and check, a
 * done's answers, the person's words): only ids, names, states, flags, moments and counts are carried.
 *
 * ## A twin
 *
 * The driver's `MachineLogReader` reads the same lines with its own code; the two share none, and the
 * format is the contract (§3). What is a line and what is skipped is one table, held by both tests:
 * `MachineLogReaderTests.Parsing` and `usage-report.test.ts`. Change one and the other changes with it.
 *
 * It reads; it writes nothing, and nothing leaves the machine. Point it at a copy when the install is
 * running, or at the live folder: every file is opened for reading only. `--service` takes only a
 * loopback address (`hostOf`), so the host it asks is this machine's own, and every request is a GET.
 */
import { existsSync } from 'node:fs';
import { isMain } from './fsx.mjs';
import { homeEntry } from '../src/Daoris.Cli/src/platform/home.ts';
import { DAY_MS, USAGE, parseArguments, readLogs, readProposals, text } from './usage-report/reading.mjs';
import { readRecords } from './usage-report/outcomes.mjs';
import { summarise } from './usage-report/summary.mjs';
import { render } from './usage-report/render.mjs';
export { DAY_MS, SAMPLE, USAGE, parseLine, readLogs, readProposals, parseArguments } from './usage-report/reading.mjs';
export { median } from './usage-report/figures.mjs';
export { DRIVEN_LISTED, DRIVEN_WIDTH } from './usage-report/driven.mjs';
export { RECORD_ROUTES, readRecords, askOf, endingOf, outcomesOf } from './usage-report/outcomes.mjs';
export { summarise } from './usage-report/summary.mjs';
export { render } from './usage-report/render.mjs';
// Every home place is asked of the home layout, never joined here.
// The install's layout, from the script that makes it: its home is `data/` beside the launcher.

/**
 * The runner: the report for the period, as text or JSON, with the outcomes when `--service` names the local host. 0
 * printed, a host that did not answer included, since the section says so · 2 a flag it cannot use, or no log.
 */
export async function main(argv, io = {
  now: new Date(), out: (text) => console.log(text), err: (text) => console.error(text), fetch: globalThis.fetch,
}) {
  let options;
  try {
    options = parseArguments(argv);
  } catch (error) {
    io.err(`usage-report: ${error.message}.`);
    io.err(USAGE);
    return 2;
  }

  const folder = homeEntry(options.home, 'logs');
  if (!existsSync(folder)) {
    io.err(`usage-report: no machine log in ${folder} — nothing has written one there, or the home is another.`);
    return 2;
  }

  const from = new Date(io.now.getTime() - options.days * DAY_MS);
  const read = readLogs(folder, { since: from });
  const proposals = readProposals(homeEntry(options.home, 'rule-proposals'));
  const records = options.service ? await readRecords(options.service, { fetch: io.fetch ?? globalThis.fetch }) : null;
  const report = summarise(read.lines, { from, to: io.now, skipped: read.skipped, proposals, records });
  io.out(options.json ? JSON.stringify(report, null, 2) : render(report, folder));
  return 0;
}

if (isMain(import.meta.url)) {
  process.exitCode = await main(process.argv.slice(2));
}

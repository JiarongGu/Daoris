#!/usr/bin/env node
/**
 * The background run's schedule and its safeguards (CISCOPE1d, D207; design `docs/2026-10-24-ci-scope-design.md` §5.1 to §5.3,
 * §8 row 4). The merge tool's CI (`--ci`, `--ci --watch`) and the install's publisher read and write these records; each is
 * kept in the main checkout's ignored `local/`, outside `local/scratch/`, so no prune and no CI run's rewrite of the gate tree's
 * record loses it.
 *
 * ## Why
 *
 * The scoped run a merge will wait on (CISCOPE1e) runs what a change reaches, so what it misses is found by the full set in
 * the background, and the owner's install is republished only on a commit that background run passed. That needs four records
 * the CI did not keep: when each Process half was last run whole and when a night last finished (the due record), which tests
 * fail on main and since when (known red), which runs finished on which tree (the receipts), and what was staged (the history).
 *
 * ## The due record, `local/ci-sweep.json`
 *
 * - **The sweep.** Each Process half's last sweep and when its next is due, `SWEEP_BOUND_MS` after it. A half due is run whole
 *   by the next run, whatever its reach. A half's sweep is satisfied by a run that gave every class of the half a verdict, pass
 *   or fail: the half run whole, its trx read. A run ended or killed before its classes ran, or a build that left them unrun,
 *   leaves it due.
 * - **The night.** An unconditional run of every gate whole, the rehearsals included, at `NIGHT_HOUR` local time on main's tip.
 *   A night is satisfied by a nightly run that finished, whatever its verdict, its failures filed. A night missed (the machine
 *   asleep or off) is due at the next start, before any other background run.
 * - **Overdue** is a half whose sweep is past its bound, and a night that did not finish: the CI's status and the watch say it,
 *   and the stage refuses while a half is overdue.
 *
 * ## The run kinds (§5.1's matrix)
 *
 * `ordinary` after a promotion (the watch), a plain `ci`, `stage` (`--ci --stage`), `sweep` (a due half alone, with no promotion
 * to ride), `nightly`, and `full` (`--full --beside`). Each run's receipt names its kind, and so does each verdict it gives.
 *
 * ## Known red, `local/ci-findings.json`
 *
 * A background failure is filed, not waited on: the commit, the unit, the test with its case, its **fingerprint** (the test, the
 * exception type, the assertion's file and line, the message's first line normalised), its **signature** (row 3's
 * `signatureOf`: the location by method and source text, never its line), and the first lines of its failure. The fingerprint
 * is the record; the signature is the match: a sighting whose signature an open finding holds moves its *last seen*, else it is
 * a new finding. A known red is cleared only by a pass of the test on a commit that descends, on main's first-parent line, from
 * the commit it was last seen failing on, given by a CI run (`CLEARING_KINDS`); a sighting within a day of a clearing restores
 * the finding with its first sighting. A test known red past `KNOWN_RED_MS` from the first sighting of the test under any
 * fingerprint has expired: no candidate inherits it (CISCOPE1e), the watch says it, and twelve hours before, warns.
 *
 * ## The receipts, `local/ci-runs.jsonl`, and the stage history, `local/stage-history.jsonl`
 *
 * Every run of the gate tree appends its receipt at its end: the commit, its tree, its kind, when it started and ended, whether
 * it finished, and each gate's verdict with the kind of run that gave it. The stage needs a counted receipt on the tree it
 * stages (`COUNTED_KINDS`, a run that finished). Each successful stage appends to the history, which nothing rewrites; the
 * stage's report reads it to say what the stage brings since the last one, and nothing the stage decides depends on it.
 *
 * Exit codes: 0 · 2 a tool error. `node tools/ci-background.mjs` prints the due record and the known red, as the watch says them.
 */
import { appendFileSync, existsSync, mkdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { isMain, writeAtomic } from './fsx.mjs';
import { normalise, repositoryPath, signatureKey } from './test-failures.mjs';

export const CI_SWEEP = 'local/ci-sweep.json';
export const CI_FINDINGS = 'local/ci-findings.json';
export const CI_RUNS_LOG = 'local/ci-runs.jsonl';
export const STAGE_HISTORY = 'local/stage-history.jsonl';

const HOUR_MS = 60 * 60 * 1000;

/** PROCREACH1's bound, the merge tool's `SWEEP_BOUND_MS`: a half's next sweep is due a day after its last. */
export const SWEEP_BOUND_MS = 24 * HOUR_MS;

/** The hour, local time, the nightly run is due at: overnight, when the machine is least used. */
export const NIGHT_HOUR = 3;

/** How long a test stays known red, from its first sighting under any fingerprint, before it expires (§5.2). */
export const KNOWN_RED_MS = 72 * HOUR_MS;

/** How long before its expiry the watch warns of a known red. */
export const KNOWN_RED_WARN_MS = 12 * HOUR_MS;

/** A sighting this soon after a clearing contradicts it: the finding is restored with its first sighting. */
export const CONTRADICTED_MS = 24 * HOUR_MS;

/**
 * §5.1's matrix: what each kind of run runs, and whether its receipt counts for a stage. `rehearsals`: `owed` (CIFAST1, a plain
 * run never starts the family and deployment rehearsals), `run`, or `none` (a sweep runs the due half and nothing else).
 */
export const RUN_KINDS = Object.freeze({
  ordinary: Object.freeze({ on: "main's tip, after a promotion", runs: 'every gate with no pass standing on its tree, a due half whole', rehearsals: 'owed', counts: true }),
  ci: Object.freeze({ on: "the named commit, or main's tip", runs: 'every gate with no pass standing on its tree, a due half whole', rehearsals: 'owed', counts: true }),
  sweep: Object.freeze({ on: "main's tip", runs: 'the due half whole, and nothing else', rehearsals: 'none', counts: false }),
  nightly: Object.freeze({ on: "main's tip", runs: 'every gate whole', rehearsals: 'run', counts: true }),
  stage: Object.freeze({ on: 'the commit to stage', runs: 'what its verdicts lack, a due half whole', rehearsals: 'run', counts: true }),
  full: Object.freeze({ on: "the named commit, or main's tip (--full --beside)", runs: 'every gate', rehearsals: 'run', counts: true }),
});

/** The kinds whose finished run is a receipt a stage counts: every one but a sweep-only run, which ran one half. */
export const COUNTED_KINDS = Object.freeze(Object.keys(RUN_KINDS).filter((kind) => RUN_KINDS[kind].counts));

/**
 * The kinds whose pass clears a known red (§5.2): a run of the background's own (its verdicts count for the stage), never a
 * bisection's, recall's, or a candidate's that was not promoted. A promoted candidate's scoped run (CISCOPE1e) is `promoted`.
 */
export const CLEARING_KINDS = Object.freeze(['ordinary', 'ci', 'sweep', 'nightly', 'stage', 'full', 'promoted']);

const iso = (ms) => new Date(ms).toISOString().replace(/\.\d+Z$/, 'Z');
const timeOf = (value) => Date.parse(value ?? '');
const minute = (value) => (typeof value === 'string' && /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}/.test(value) ? `${value.slice(0, 10)} ${value.slice(11, 16)} UTC` : 'never');
const hours = (ms) => `${Math.round(ms / HOUR_MS)} hours`;

function readJson(file, empty) {
  try {
    const parsed = JSON.parse(readFileSync(file, 'utf8'));
    return parsed && typeof parsed === 'object' ? parsed : empty();
  } catch {
    return empty();
  }
}

/** A JSON-lines file's records, a line that does not read skipped: an append cut short by a kill loses that line only. */
function readLines(file) {
  if (!existsSync(file)) return [];
  const out = [];
  for (const line of readFileSync(file, 'utf8').split(/\r?\n/)) {
    if (!line.trim()) continue;
    try {
      const parsed = JSON.parse(line);
      if (parsed && typeof parsed === 'object') out.push(parsed);
    } catch {
      // A torn line: the rest still read.
    }
  }
  return out;
}

function appendLine(file, record) {
  mkdirSync(dirname(file), { recursive: true });
  appendFileSync(file, `${JSON.stringify(record)}\n`);
}

// ---------------------------------------------------------------------------------------------------
// The due record

/** The due record, `{ schema, halves: { <gate>: { last, commit, kind } }, night: { last, commit, ended } | null }`. */
export function readSweep(root) {
  const parsed = readJson(join(root, CI_SWEEP), () => ({}));
  const halves = {};
  for (const [gate, entry] of Object.entries(parsed.halves ?? {})) if (entry && typeof entry.last === 'string') halves[gate] = entry;
  const night = parsed.night && typeof parsed.night.last === 'string' ? parsed.night : null;
  return { schema: 1, halves, night };
}

export function writeSweep(root, record) {
  writeAtomic(join(root, CI_SWEEP), `${JSON.stringify({
    schema: 1,
    _why: 'Written by tools/merge-branch.mjs --ci (CISCOPE1d): each Process half\'s last sweep (a run that gave every class a verdict) and the last night that finished. The next is due a day after; the stage refuses a half overdue.',
    halves: record.halves,
    night: record.night,
  }, null, 2)}\n`);
}

/**
 * Each of `halves` (the plan's Process halves by name) with its sweep's standing at `now`: `last` its last sweep or null, `due`
 * when the next is, and `overdue` once that has passed. A half the record never saw is due, and overdue: no sweep of it is known.
 */
export function sweepState(record, halves, now = Date.now(), bound = SWEEP_BOUND_MS) {
  return halves.map((gate) => {
    const last = record.halves[gate]?.last ?? null;
    const at = timeOf(last);
    const due = Number.isFinite(at) ? at + bound : null;
    return { gate, last, due: due === null ? null : iso(due), overdue: due === null || now >= due };
  });
}

/** The halves whose sweep is due: run whole by the next run, whatever their reach. */
export const dueHalves = (record, halves, now = Date.now(), bound = SWEEP_BOUND_MS) => sweepState(record, halves, now, bound).filter((each) => each.overdue).map((each) => each.gate);

/**
 * Whether a run's result for a Process half satisfies its sweep: it ran whole (`whole`, the plan's reason, or a nightly run's)
 * and gave every class a verdict, pass or fail, its trx read. A run ended before its classes ran, or a build that left them
 * unrun (no trx), does not.
 */
export function sweepSatisfied({ whole, verdict, trx }) {
  return Boolean(whole) && ['PASS', 'FLAKE', 'FAIL'].includes(verdict) && Boolean(trx);
}

/** The record with each satisfied half's sweep noted: `swept` is `[{ gate, at, commit, kind }]`. */
export function noteSweeps(record, swept) {
  const halves = { ...record.halves };
  for (const { gate, at, commit, kind } of swept) halves[gate] = { last: at, commit, kind };
  return { ...record, halves };
}

/** The most recent instant the night was due at, at or before `now`: today's `hour` local time, or yesterday's. */
export function lastNight(now = Date.now(), hour = NIGHT_HOUR) {
  const at = new Date(now);
  at.setHours(hour, 0, 0, 0);
  if (at.getTime() > now) at.setDate(at.getDate() - 1);
  return at.getTime();
}

/**
 * Whether a night is due at `now`: no night finished since the last instant one was due at. A night missed is so at the next
 * start, which the watch runs before any other background run. `since` is that instant.
 */
export function nightDue(record, now = Date.now(), hour = NIGHT_HOUR) {
  const since = lastNight(now, hour);
  const last = timeOf(record.night?.last);
  return { due: !(Number.isFinite(last) && last >= since), since: iso(since), last: record.night?.last ?? null };
}

/** Whether a nightly run's end satisfies the night: it finished, green or failed, its failures filed; a stop or an error does not. */
export const nightSatisfied = (state) => state === 'green' || state === 'failed';

/** The record with the night noted as finished, the run's start the time it counts from. */
export const noteNight = (record, { at, commit, ended }) => ({ ...record, night: { last: at, commit, ended } });

/**
 * The overdue report (§5.1): each half past its sweep's bound, and a night that did not finish, as the CI's status and the watch
 * say them. `waiting` names a gate waiting and why, where the run knows it.
 */
export function overdueLines(record, halves, now = Date.now(), { bound = SWEEP_BOUND_MS, hour = NIGHT_HOUR } = {}) {
  const lines = [];
  for (const each of sweepState(record, halves, now, bound)) {
    if (!each.overdue) continue;
    lines.push(each.last
      ? `overdue: ${each.gate}'s sweep, last run whole ${minute(each.last)}, was due ${minute(each.due)}; the next run runs it whole`
      : `overdue: ${each.gate} has no sweep recorded; the next run runs it whole`);
  }
  const night = nightDue(record, now, hour);
  if (night.due) {
    lines.push(`overdue: the night due ${minute(night.since)} has not finished (the last finished night ran ${minute(night.last)}); the watch runs it before any other`);
  }
  return lines;
}

/**
 * Which run the watch starts next, and why, or null: a night due first (a missed night caught up before any other background
 * run), then a tip no run gave a verdict on (`ordinary`), then a due sweep with no promotion to ride (`sweep`). An `hour` of
 * null runs no night.
 */
export function nextRun({ record, halves, tipGated, now = Date.now(), bound = SWEEP_BOUND_MS, hour = NIGHT_HOUR }) {
  const night = hour === null ? { due: false } : nightDue(record, now, hour);
  if (night.due) return { kind: 'nightly', key: `nightly ${night.since}`, why: `the night due ${minute(night.since)} has not run` };
  if (!tipGated) return { kind: 'ordinary', key: 'ordinary', why: "main's tip has no verdict of a run here" };
  const due = dueHalves(record, halves, now, bound);
  if (due.length) return { kind: 'sweep', key: `sweep ${due.join(',')}`, why: `${due.join(' and ')} ${due.length === 1 ? 'is' : 'are'} due a sweep`, halves: due };
  return null;
}

/**
 * What a run of `kind` runs, from the plan's names, the CI's ordinary plan (`ciPlan`'s run and owed), and the halves due:
 * `run` (the gates it runs), `whole` (the halves it runs whole, with why), and `skipped` (what a sweep-only run leaves).
 */
export function kindPlan(kind, { plan, ordinary, due = [], halves = [] }) {
  if (!RUN_KINDS[kind]) throw new Error(`no run kind '${kind}'`);
  if (kind === 'nightly' || kind === 'full') {
    return { run: [...plan], owed: [], whole: new Map(halves.map((gate) => [gate, `the ${kind === 'nightly' ? 'nightly run' : 'full set'} runs every gate whole`])), skipped: [] };
  }
  const dueHere = due.filter((gate) => plan.includes(gate));
  const whole = new Map(dueHere.map((gate) => [gate, 'its sweep is due (ci-sweep.json): a due half runs whole, whatever its reach']));
  if (kind === 'sweep') return { run: dueHere, owed: [], whole, skipped: plan.filter((gate) => !dueHere.includes(gate)) };
  const run = plan.filter((gate) => ordinary.run.includes(gate) || dueHere.includes(gate));
  return { run, owed: ordinary.owed.filter((gate) => !run.includes(gate)), whole, skipped: [] };
}

// ---------------------------------------------------------------------------------------------------
// Known red

/**
 * A failure's fingerprint (§5.2), the identity of a known red: the test with its case, the exception type, the assertion's
 * location as file and line (the first frame in the test's own file), and the message's first line, normalised. Where no
 * location can be read the whole message line stands, normalised the same way.
 */
export function fingerprintOf(failure, root = null) {
  const unit = String(failure.unit ?? '').split('+')[0];
  const own = failure.runner === 'trx'
    ? failure.frames?.find((frame) => frame.file && (frame.method.startsWith(`${unit}.`) || frame.method.startsWith(`${failure.unit}.`)))
    : failure.frames?.find((frame) => frame.file && repositoryPath(frame.file, root) === repositoryPath(failure.unit, root));
  const at = own?.file && own.line ? `${repositoryPath(own.file, root) ?? own.file}:${own.line}` : null;
  return { test: failure.test, exception: failure.exception ?? '', at, message: normalise(failure.message) };
}

export const fingerprintKey = (fingerprint) => JSON.stringify([fingerprint.test, fingerprint.exception, fingerprint.at, fingerprint.message]);

/** The known-red record, `{ schema, findings: [...] }`. */
export function readFindings(root) {
  const parsed = readJson(join(root, CI_FINDINGS), () => ({}));
  return {
    schema: 1,
    findings: Array.isArray(parsed.findings) ? parsed.findings.filter((each) => typeof each?.test === 'string' && each.signature) : [],
    ...(Array.isArray(parsed.flakes) ? { flakes: parsed.flakes } : {}),
  };
}

export function writeFindings(root, record) {
  writeAtomic(join(root, CI_FINDINGS), `${JSON.stringify({
    schema: 1,
    _why: 'Written by tools/merge-branch.mjs --ci (CISCOPE1d): each failure the background run found on main, keyed by test and fingerprint, matched by signature; cleared by a pass on a descendant of where it was last seen. The parent files each and fixes it forward.',
    findings: record.findings,
    // CISCOPE1e: a failure a candidate could not place, filed as a flake for its unit's row.
    ...(Array.isArray(record.flakes) && record.flakes.length ? { flakes: record.flakes } : {}),
  }, null, 2)}\n`);
}

const open = (finding) => finding.state === 'red';

/** When the test's known red counts from: the first sighting of the test under any fingerprint still open, else `at`. */
function testSince(findings, test, at) {
  const since = findings.filter((each) => open(each) && each.test === test).map((each) => each.since);
  return [...since, at].sort()[0];
}

/**
 * A failure seen on `commit` filed into the record (§5.2). `sighting`: `{ gate, unit, test, fingerprint, signature, message,
 * lines, commit, at, kind, scoped }`. An open finding with the same signature moves its *last seen*, its fingerprint and message
 * updated; a cleared one whose clearing a sighting within `CONTRADICTED_MS` contradicts is restored with its first sighting;
 * otherwise a new finding is opened, its expiry counted from the test's first sighting while it stays known red. Answers the
 * record and what happened: `new`, `seen` or `restored`.
 */
export function fileSighting(record, sighting) {
  const key = signatureKey(sighting.signature);
  const findings = record.findings.map((each) => ({ ...each }));
  const seen = { commit: sighting.commit, at: sighting.at, message: sighting.message ?? '' };
  const same = findings.find((each) => open(each) && each.test === sighting.test && each.key === key);
  if (same) {
    Object.assign(same, { lastSeen: seen, fingerprint: sighting.fingerprint, lines: sighting.lines ?? same.lines, gate: sighting.gate ?? same.gate });
    return { record: { ...record, findings }, how: 'seen', finding: same };
  }
  const cleared = findings.filter((each) => each.state === 'cleared' && each.test === sighting.test && each.key === key)
    .sort((a, b) => String(b.cleared?.at).localeCompare(String(a.cleared?.at)))[0];
  if (cleared && timeOf(sighting.at) - timeOf(cleared.cleared?.at) <= CONTRADICTED_MS) {
    Object.assign(cleared, { state: 'red', lastSeen: seen, fingerprint: sighting.fingerprint, contradicted: cleared.cleared, cleared: null });
    cleared.since = testSince(findings.filter((each) => each !== cleared), cleared.test, cleared.since);
    return { record: { ...record, findings }, how: 'restored', finding: cleared };
  }
  const finding = {
    gate: sighting.gate ?? null,
    unit: sighting.unit ?? null,
    test: sighting.test,
    fingerprint: sighting.fingerprint,
    signature: sighting.signature,
    key,
    state: 'red',
    firstSeen: { commit: sighting.commit, at: sighting.at, kind: sighting.kind ?? null },
    lastSeen: seen,
    since: testSince(findings, sighting.test, sighting.at),
    lines: sighting.lines ?? [],
    scoped: sighting.scoped ?? null,
    cleared: null,
  };
  findings.push(finding);
  return { record: { ...record, findings }, how: 'new', finding };
}

/**
 * Known reds cleared by passes on `commit` (§5.2): each open finding whose test `passed(finding)` says passed, when the run's
 * `kind` is one that clears (`CLEARING_KINDS`) and `commit` descends, on main's first-parent line, from the commit it was last
 * seen failing on (`descends(from, to)`). A late run on an older commit, a bisection's run on a passing ancestor, or a run that
 * does not clear, clears nothing. Answers the record and the findings cleared.
 */
export function clearPasses(record, { commit, at, kind, passed, descends }) {
  if (!CLEARING_KINDS.includes(kind)) return { record, cleared: [] };
  const cleared = [];
  const findings = record.findings.map((each) => {
    if (!open(each) || !passed(each)) return each;
    if (!descends(each.lastSeen.commit, commit)) return each;
    const done = { ...each, state: 'cleared', cleared: { commit, at, kind } };
    cleared.push(done);
    return done;
  });
  return { record: { ...record, findings }, cleared };
}

/** An open finding's standing at `now`: `inherited` within `KNOWN_RED_MS` of its test's first sighting, else `expired`; `warn` within twelve hours of it. */
export function knownRedStanding(finding, now = Date.now()) {
  const age = now - timeOf(finding.since);
  return { expired: age > KNOWN_RED_MS, warn: age <= KNOWN_RED_MS && age > KNOWN_RED_MS - KNOWN_RED_WARN_MS, expires: iso(timeOf(finding.since) + KNOWN_RED_MS) };
}

/** The open finding a failure's signature matches, inherited and not expired, or null: what a candidate's base run asks (CISCOPE1e). */
export function knownRed(record, { test, signature }, now = Date.now()) {
  const key = signatureKey(signature);
  return record.findings.find((each) => open(each) && each.test === test && each.key === key && !knownRedStanding(each, now).expired) ?? null;
}

/** What the watch says of known red: each expired finding, and each expiring within twelve hours. */
export function knownRedLines(record, now = Date.now()) {
  const lines = [];
  for (const finding of record.findings.filter(open)) {
    const standing = knownRedStanding(finding, now);
    const what = `${finding.test}${finding.gate && finding.gate !== finding.test ? ` (${finding.gate})` : ''}, known red since ${minute(finding.since)}, last seen on ${String(finding.lastSeen.commit).slice(0, 7)}`;
    if (standing.expired) lines.push(`known red past ${hours(KNOWN_RED_MS)}: ${what}: no longer inherited; the stage refuses it, and only a direct commit that fixes it or skips it under its row ends it`);
    else if (standing.warn) lines.push(`known red expiring ${minute(standing.expires)}: ${what}`);
  }
  return lines;
}

/**
 * The tests a trx shows passing, by name with their case: what clears a known red. A test that failed and passed alone (FLAKE1)
 * is not among them, its trx holding the failure.
 */
export function passingTests(trx) {
  const passed = new Set();
  for (const [tag] of String(trx).matchAll(/<UnitTestResult\b[^>]*>/g)) {
    const outcome = /\boutcome="([^"]*)"/.exec(tag)?.[1];
    const name = /\btestName="([^"]*)"/.exec(tag)?.[1];
    if (outcome === 'Passed' && name) passed.add(name.replace(/&quot;/g, '"').replace(/&amp;/g, '&').replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&apos;/g, "'"));
  }
  return passed;
}

// ---------------------------------------------------------------------------------------------------
// The receipts

/** Every receipt, oldest first. */
export const readReceipts = (root) => readLines(join(root, CI_RUNS_LOG)).filter((each) => typeof each.commit === 'string' && typeof each.kind === 'string');

/** A run's receipt appended at its end: `{ commit, tree, kind, started, ended, finished, state, gates: [{ name, verdict, kind }] }`. */
export const appendReceipt = (root, receipt) => appendLine(join(root, CI_RUNS_LOG), receipt);

/** Whether a receipt counts for a stage: a run that finished, of a counted kind. */
export const countedReceipt = (receipt) => receipt.finished === true && COUNTED_KINDS.includes(receipt.kind);

/**
 * The stage's evidence on `tree` (§5.3 point 1): the newest counted receipt on that tree, or null with why. Matched by tree,
 * never by a commit's name alone; a sweep-only run's receipt and a run that did not finish are said, never counted.
 */
export function receiptFor(receipts, tree) {
  const here = receipts.filter((each) => each.tree === tree);
  const counted = here.filter(countedReceipt);
  if (counted.length) return { receipt: counted.at(-1), why: null };
  if (here.some((each) => each.kind === 'sweep' && each.finished)) return { receipt: null, why: 'only a sweep-only run finished on it, which ran one half and is no receipt a stage counts' };
  if (here.length) return { receipt: null, why: 'no background run on it finished (each was stopped or ended in an error)' };
  return { receipt: null, why: 'no background run finished on it' };
}

// ---------------------------------------------------------------------------------------------------
// The stage history

export const readStageHistory = (root) => readLines(join(root, STAGE_HISTORY)).filter((each) => typeof each.commit === 'string');

/** A successful stage appended: `{ commit, tree, to, at, forced }`. */
export const appendStage = (root, entry) => appendLine(join(root, STAGE_HISTORY), entry);

/**
 * What a stage of `commit` brings since the last (§5.3): the last stage, and the range from it, or why the range is unknown (no
 * history yet, or a last staged commit that is not an ancestor of this one). `isAncestor(a, b)` and `commits(a, b)` are git's.
 */
export function stageRange(history, commit, { isAncestor, commits }) {
  const last = history.at(-1) ?? null;
  if (!last) return { last: null, known: false, why: 'no stage is recorded yet' };
  if (last.commit === commit) return { last, known: true, commits: [] };
  if (!isAncestor(last.commit, commit)) return { last, known: false, why: `the last staged commit, ${last.commit.slice(0, 7)}, is not an ancestor of this one` };
  return { last, known: true, commits: commits(last.commit, commit) };
}

/** The stage's report lines from `stageRange`. */
export function stageRangeLines(range) {
  if (!range.known) return [`since the last stage: unknown (${range.why})`];
  const when = minute(range.last.at);
  if (!range.commits.length) return [`since the last stage (${range.last.commit.slice(0, 7)}, ${when}): the same commit`];
  return [
    `since the last stage (${range.last.commit.slice(0, 7)}, ${when}): ${range.commits.length} commit${range.commits.length === 1 ? '' : 's'}`,
    ...range.commits.slice(0, 10).map((line) => `  ${line}`),
    ...(range.commits.length > 10 ? [`  and ${range.commits.length - 10} more`] : []),
  ];
}

// ---------------------------------------------------------------------------------------------------

function main() {
  const root = process.cwd();
  const record = readSweep(root);
  const halves = Object.keys(record.halves);
  const lines = [...overdueLines(record, halves), ...knownRedLines(readFindings(root))];
  for (const [gate, entry] of Object.entries(record.halves)) console.log(`${gate}: last swept ${minute(entry.last)} on ${String(entry.commit ?? '').slice(0, 7)}`);
  console.log(record.night ? `the last night: ${minute(record.night.last)} on ${String(record.night.commit ?? '').slice(0, 7)}` : 'no night has finished');
  for (const line of lines) console.log(line);
  return 0;
}

if (isMain(import.meta.url)) {
  try {
    process.exitCode = main();
  } catch (error) {
    console.error(`ci-background: ${error.message}`);
    process.exitCode = 2;
  }
}

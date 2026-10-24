#!/usr/bin/env node
/**
 * Recall: each recorded failure judged against the commit that introduced it (CISCOPE1c, D207; design
 * `docs/2026-10-24-ci-scope-design.md` §3.5).
 *
 * ## Why
 *
 * The census says which units exist and that its declared edges hold; it cannot say whether a unit's real dependencies fall
 * inside its reach. Recall does: a failure that reached main is held against the one first-parent commit that turned its unit
 * red with its signature, and the census, pinned at this tool's commit and run over that commit's tree, says whether that
 * commit's own change reached the unit. Judging against a range instead would credit the census with whatever else the range
 * touched.
 *
 * ## The search (`introduction`)
 *
 * Over main's first-parent commits, oldest first, from the range's start (its newest recorded pass, or where none is recorded
 * the newer of the unit's first commit and the month's first, where the test must then pass) to `f`, the first commit a source
 * saw the failure on. `run(i)` runs the failing unit's whole class or file at commit `i` and answers `'pass'`, `'absent'` (no
 * such test), `'nobuild'`, or the failure's signature (`test-failures.mjs`). Only a pass moves `q`, the newest commit known to
 * pass; the **verified suffix** `v..f` is every commit run and showing the signature, contiguous. The **open set** is `q+1..v`.
 *
 * 1. **The walk**: f's parent, then its parent, each run, until the test passes (its successor is the last transition, exactly)
 *    or the open set's commits all get one verdict (step 5). Another signature or no test ends the search, *unsettled,
 *    signature changed*. A commit that does not build is named and walked past; it ends the suffix's contiguity.
 * 2. **A cap of 48 runs.** Only a walk that reaches it goes on.
 * 3. **Halving** below `v`, moving `q` up to the newest passing run it finds.
 * 4. **Probes, recursive**: every stretch longer than sixteen commits between two run commits of the open set is run at its
 *    midpoint until none is; a pass moves `q` and halving starts again above it.
 * 5. **The verdict's test**: each commit of the open set classified structurally (`classify(i)`); one verdict for all of them
 *    is the failure's, whichever introduced it; otherwise *unsettled, open set disagrees*.
 *
 * A FIX-LOG entry's named commit is run with its first parent; where the parent passes and the commit shows the signature, the
 * failure is first seen there (`f`), and the walk's first run is that parent's.
 *
 * ## The proof (`node tools/test-recall.mjs proof`)
 *
 * Reads the month's failure sources from the main checkout's records (the FIX-LOG's entries whose symptom is a test or check
 * failing; the CI runs' `status.json` and trx; `local/class-verdicts.json`; `local/gate-verdicts.json`), states each source's
 * span, count and observation lag, and judges each failure: by the search where `--run` lets it run historical trees (one at a
 * time, lowered, behind the memory gate, never while the gate tree's lock is held), structurally at the commit its FIX-LOG entry
 * names otherwise, which the search has not confirmed and which is therefore **unsettled** in the lower bound. Writes
 * `local/scratch/recall/proof.json` and prints the figures.
 *
 * Exit codes: 0 answered · 2 a tool error.
 */
import { spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';
import { isMain, writeAtomic } from './fsx.mjs';
import { readTrx, signatureKey } from './test-failures.mjs';

export const WALK_CAP = 48;
export const PROBE_STRETCH = 16;

/** The six verdicts, and which count against recall. */
export const VERDICTS = Object.freeze(['selected', 'missed', 'deferred', 'unscoped', 'execution', 'unsettled']);

/**
 * The introduction of one failure, searched as the header says. `commits` is the range, oldest first; `start` the index of the
 * range's start; `startPasses` whether a recorded pass stands there; `f` the first failing commit's index; `signature` the
 * failure's; `run(i)` and `classify(i)` as above; `named`, a FIX-LOG entry's named commit's index, or null. Answers
 * `{ settled, reason, introduction, verdict, runs, walked, capped, halved, probes, open, skipped }`.
 */
export function introduction({ commits, start = 0, startPasses = false, f, signature, run, classify, named = null, cap = WALK_CAP, stretch = PROBE_STRETCH }) {
  const key = signatureKey(signature);
  const results = new Map();
  let runs = 0;
  const at = (i) => {
    if (!results.has(i)) {
      runs += 1;
      const result = run(i, commits[i]);
      results.set(i, result === 'pass' || result === 'absent' || result === 'nobuild' ? result : signatureKey(result) === key ? 'signature' : 'other');
    }
    return results.get(i);
  };
  const skipped = [];
  const report = (fields) => ({
    runs, skipped: skipped.map((i) => commits[i]), walked: 0, capped: false, halved: false, probes: 0, ...fields,
  });
  const unsettled = (reason, extra = {}) => report({ settled: false, reason, introduction: null, verdict: 'unsettled', ...extra });

  // A FIX-LOG entry's named commit, checked by one run on it and one on its first parent.
  if (named !== null && named > start && named <= f) {
    if (at(named) === 'signature' && at(named - 1) === 'pass') f = named;
  }
  if (at(f) !== 'signature') return unsettled('the failure does not reproduce at its first sighting');
  let q = startPasses ? start : null;
  if (q === null) {
    const first = at(start);
    if (first !== 'pass') return unsettled('no pass found', { at: commits[start] });
    q = start;
  }
  let v = f;
  let contiguous = true;

  const classified = new Map();
  const verdictOf = (from, to) => {
    const counts = {};
    for (let i = from; i <= to; i += 1) {
      if (!classified.has(i)) classified.set(i, classify(i, commits[i]));
      const verdict = classified.get(i);
      counts[verdict] = (counts[verdict] ?? 0) + 1;
    }
    return counts;
  };
  const settleOpen = (extra) => {
    const counts = verdictOf(q + 1, v);
    const kinds = Object.keys(counts);
    const open = { size: v - q, counts };
    if (kinds.length === 1) {
      return report({ settled: true, reason: v === q + 1 ? 'the transition' : 'one verdict over the open set', introduction: v === q + 1 ? commits[v] : null, verdict: kinds[0], open, ...extra });
    }
    return unsettled('open set disagrees', { open, ...extra });
  };

  // 1, 2. The walk.
  let walked = 0;
  for (let i = f - 1; i > q; i -= 1) {
    if (Object.keys(verdictOf(q + 1, v)).length === 1) return settleOpen({ walked });
    if (walked >= cap) break;
    walked += 1;
    const result = at(i);
    if (result === 'pass') {
      q = i;
      return settleOpen({ walked });
    }
    if (result === 'nobuild') {
      skipped.push(i);
      contiguous = false;
      continue;
    }
    if (result !== 'signature') return unsettled('signature changed', { walked, at: commits[i] });
    if (contiguous) v = i;
  }
  if (walked < cap) return settleOpen({ walked });

  // 3, 4. Halving below v, then the probes, recursive.
  const extra = { walked, capped: true, halved: true };
  const halve = () => {
    let lo = q;
    let hi = v;
    while (hi - lo > 1) {
      const mid = Math.floor((lo + hi) / 2);
      if (at(mid) === 'pass') lo = mid;
      else hi = mid;
    }
    q = lo;
  };
  halve();
  let probes = 0;
  for (;;) {
    const known = [q, ...[...results.keys()].filter((i) => i > q && i < v).sort((a, b) => a - b), v];
    let wide = null;
    for (let k = 1; k < known.length; k += 1) if (known[k] - known[k - 1] > stretch) wide = [known[k - 1], known[k]];
    if (!wide) break;
    const mid = Math.floor((wide[0] + wide[1]) / 2);
    probes += 1;
    if (at(mid) === 'pass') {
      q = mid;
      halve();
    }
  }
  return settleOpen({ ...extra, probes });
}

/** Recall as a lower bound: selected over selected, missed and unsettled; and what it would be were every unsettled one selected. */
export function recallOf(judged) {
  const counts = Object.fromEntries(VERDICTS.map((verdict) => [verdict, 0]));
  for (const each of judged) counts[each.verdict] += 1;
  const denominator = counts.selected + counts.missed + counts.unsettled;
  return {
    counts,
    lowerBound: denominator ? counts.selected / denominator : null,
    ifUnsettledSelected: denominator ? (counts.selected + counts.unsettled) / denominator : null,
  };
}

/** One regression counted once: sightings sharing the unit, the signature and the introducing commit. */
export function dedupe(failures) {
  const seen = new Map();
  for (const failure of failures) {
    const key = JSON.stringify([failure.unit, signatureKey(failure.signature), failure.introduction ?? failure.first ?? null]);
    const kept = seen.get(key);
    if (kept) kept.sightings.push(...(failure.sightings ?? [failure.source]));
    else seen.set(key, { ...failure, sightings: [...(failure.sightings ?? [failure.source])] });
  }
  return [...seen.values()];
}

// ---------------------------------------------------------------------------------------------------
// The month's failure sources

const git = (root, args, input) => {
  const out = spawnSync('git', args, { cwd: root, windowsHide: true, encoding: 'utf8', maxBuffer: 256 * 1024 * 1024, input });
  if (out.status !== 0) throw new Error(`git ${args.join(' ')}: ${String(out.stderr).trim()}`);
  return out.stdout;
};

/** Main's first-parent commits from `since` to `until`, oldest first: `{ sha, time, merge }`. */
export function firstParent(root, since, until = 'main') {
  return git(root, ['log', '--first-parent', '--reverse', `--since=${since}`, '--format=%H %ct %P', until])
    .split('\n').filter(Boolean).map((line) => {
      const [sha, time, ...parents] = line.split(' ');
      return { sha, time: Number(time) * 1000, merge: parents.length > 1 };
    });
}

/**
 * The FIX-LOG's entries whose symptom is a test or a check failing, in a span: each entry's date, title, the units its text
 * names (a `*Tests` class, a `*.test.ts` file, a gate) and the commits it names.
 */
export function fixLogFailures(text, since, until) {
  const entries = [];
  const parts = String(text).replace(/\r\n/g, '\n').split(/^(?=## )/m);
  for (const part of parts) {
    const head = /^## (\d{4}-\d{2}-\d{2})\b[^\n]*/.exec(part);
    if (!head) continue;
    const date = head[1];
    if (date < since || date > until) continue;
    const title = head[0].replace(/^## /, '').trim();
    // The symptom says what was seen; a test the fix added is named in its fix and verification, not here.
    const field = (name) => new RegExp(`\\*\\*${name}[.:]\\*\\*([\\s\\S]*?)(?=\\n(?:- )?\\*\\*[A-Z][\\w ]*[.:]\\*\\*|$)`).exec(part)?.[1] ?? '';
    const symptom = field('Symptom');
    const cause = field('Root cause');
    const classes = [...new Set([...symptom.matchAll(/\b([A-Z]\w*Tests)\b/g)].map((m) => m[1]))];
    const files = [...new Set([...symptom.matchAll(/([\w./-]+\.test\.tsx?)\b/g)].map((m) => m[1]))];
    const gates = [...new Set([...symptom.matchAll(/`(driver-process-git|driver-process|modules-process|tool-tests|web-unit|web-types|web-rules|web-measured|code-map|orient-index|universal|deployment|rehearse-family|rehearse|devkit|service|modules|driver|cli|web)`/g)].map((m) => m[1]))];
    const failing = /\b(fail(?:s|ed|ing|ure)?|red|broke|broken)\b/i.test(symptom) && (classes.length || files.length || gates.length);
    if (!failing) continue;
    const commits = [...new Set([...`${symptom}\n${cause}`.matchAll(/`([0-9a-f]{7,40})`/g)].map((m) => m[1]))];
    entries.push({ source: 'fix-log', date, title, classes, files, gates, commits });
  }
  return entries;
}

/** Each failed CI run's failing units, from its `status.json` and trx files. */
export function ciRunFailures(scratch) {
  if (!existsSync(scratch)) return [];
  const out = [];
  for (const name of readdirSync(scratch)) {
    const dir = join(scratch, name);
    if (!/^ci-[0-9a-f]{7}$/.test(name) || !statSync(dir).isDirectory()) continue;
    const statusFile = join(dir, 'status.json');
    if (!existsSync(statusFile)) continue;
    let status;
    try {
      status = JSON.parse(readFileSync(statusFile, 'utf8'));
    } catch {
      continue;
    }
    const commit = status.commit ?? name.slice(3);
    const time = Date.parse(status.started ?? status.startedAt ?? '') || statSync(statusFile).mtimeMs;
    for (const file of readdirSync(dir).filter((each) => each.endsWith('.trx'))) {
      for (const failure of readTrx(readFileSync(join(dir, file), 'utf8'))) {
        out.push({ source: 'ci-run', gate: file.replace(/\.trx$/, ''), commit, time, unit: failure.unit, test: failure.test, failure });
      }
    }
    const gates = Array.isArray(status.gates) ? status.gates : Object.entries(status.gates ?? {}).map(([gate, each]) => ({ name: gate, ...each }));
    for (const gate of gates) {
      if (gate.verdict !== 'FAIL' || readdirSync(dir).includes(`${gate.name}.trx`)) continue;
      out.push({ source: 'ci-run', gate: gate.name, commit, time, unit: null, test: null, failure: null });
    }
  }
  return out;
}

/** Each class failure kept in `local/class-verdicts.json`. */
export function classVerdictFailures(file) {
  if (!existsSync(file)) return [];
  const record = JSON.parse(readFileSync(file, 'utf8'));
  const out = [];
  for (const [gate, classes] of Object.entries(record.gates ?? record)) {
    if (!classes || typeof classes !== 'object') continue;
    for (const [unit, verdicts] of Object.entries(classes)) {
      for (const each of Array.isArray(verdicts) ? verdicts : []) {
        if (each?.verdict === 'FAIL') out.push({ source: 'class-verdicts', gate, unit, commit: each.commit ?? null, time: Date.parse(each.at ?? each.time ?? '') || null });
      }
    }
  }
  return out;
}

/** Each failed gate verdict kept in `local/gate-verdicts.json`. */
export function gateVerdictFailures(file) {
  if (!existsSync(file)) return [];
  const record = JSON.parse(readFileSync(file, 'utf8'));
  const list = Array.isArray(record) ? record : record.verdicts ?? [];
  return list.filter((each) => each?.verdict === 'FAIL').map((each) => ({
    source: 'gate-verdicts', gate: each.gate, unit: null, commit: each.commit ?? null, time: Date.parse(each.at ?? each.time ?? '') || null,
  }));
}

/** A source's span, its count, and its observation lag: how long after the commit it names each failure was first seen there. */
export function sourceSummary(name, failures, commitTime) {
  const times = failures.map((each) => each.time).filter(Number.isFinite);
  const lags = failures.map((each) => (Number.isFinite(each.time) && commitTime.has(each.commit) ? each.time - commitTime.get(each.commit) : null))
    .filter((lag) => lag !== null && lag >= 0).sort((a, b) => a - b);
  const hours = (ms) => Math.round(ms / 36e5 * 10) / 10;
  return {
    source: name,
    count: failures.length,
    span: times.length ? [new Date(Math.min(...times)).toISOString(), new Date(Math.max(...times)).toISOString()] : null,
    lagHours: lags.length ? { median: hours(lags[Math.floor(lags.length / 2)]), max: hours(lags.at(-1)) } : null,
  };
}

// ---------------------------------------------------------------------------------------------------
// The tool

const MAIN_CHECKOUT = () => {
  const list = spawnSync('git', ['worktree', 'list', '--porcelain'], { encoding: 'utf8', windowsHide: true }).stdout ?? '';
  return /^worktree (.+)$/m.exec(list)?.[1] ?? process.cwd();
};

async function proof(argv) {
  const root = process.cwd();
  const main = argv.includes('--main') ? argv[argv.indexOf('--main') + 1] : MAIN_CHECKOUT();
  const since = argv.includes('--since') ? argv[argv.indexOf('--since') + 1] : '2026-09-24';
  const until = argv.includes('--until') ? argv[argv.indexOf('--until') + 1] : '2026-10-24';
  const { judgeStructurally } = await import('./test-map.mjs');
  const commits = firstParent(root, since, 'main');
  const commitTime = new Map(commits.map((each) => [each.sha, each.time]));
  for (const each of commits) commitTime.set(each.sha.slice(0, 7), each.time);
  const fixLog = fixLogFailures(readFileSync(join(root, 'docs/FIX-LOG.md'), 'utf8'), since, until)
    .map((entry) => ({ ...entry, time: Date.parse(`${entry.date}T12:00:00Z`) }));
  const ci = ciRunFailures(join(main, 'local/scratch'));
  const classes = classVerdictFailures(join(main, 'local/class-verdicts.json'));
  const gates = gateVerdictFailures(join(main, 'local/gate-verdicts.json'));
  const judged = [];
  for (const entry of fixLog) {
    const named = entry.commits.map((sha) => commits.find((each) => each.sha.startsWith(sha))).find(Boolean) ?? null;
    const units = [...entry.classes, ...entry.files];
    const structural = named && units.length ? await judgeStructurally(root, named, units) : null;
    judged.push({
      source: 'fix-log', date: entry.date, title: entry.title, units, named: named?.sha?.slice(0, 9) ?? null,
      structural: structural?.verdict ?? null, reached: structural?.reached ?? null,
      verdict: 'unsettled', reason: named ? 'the named commit has not been run with its first parent (no historical run made)' : 'no commit named, and no historical run made',
    });
  }
  const ciUnits = dedupe(ci.filter((each) => each.unit).map((each) => ({ unit: each.unit, signature: { test: each.test }, first: each.commit, source: each.commit })));
  for (const each of ciUnits) {
    const sha = commits.find((c) => c.sha.startsWith(each.first));
    judged.push({ source: 'ci-run', units: [each.unit], first: each.first, sightings: each.sightings, verdict: 'unsettled', reason: sha ? 'its introducing commit has not been searched (no historical run made)' : 'its first sighting is not on main\'s first-parent line' });
  }
  const recall = recallOf(judged);
  const fixOnly = recallOf(judged.filter((each) => each.source === 'fix-log'));
  const structuralCounts = {};
  for (const each of judged) if (each.structural) structuralCounts[each.structural] = (structuralCounts[each.structural] ?? 0) + 1;
  const result = {
    tool: git(root, ['rev-parse', 'HEAD']).trim(), since, until, firstParentCommits: commits.length,
    directCommits: commits.filter((each) => !each.merge).length,
    sources: [
      sourceSummary('fix-log', fixLog, commitTime), sourceSummary('ci-run', ci, commitTime),
      sourceSummary('class-verdicts', classes, commitTime), sourceSummary('gate-verdicts', gates, commitTime),
    ],
    recall, fixLogRecall: fixOnly, structuralAtNamedCommit: structuralCounts, historicalRuns: 0, judged,
  };
  const out = join(root, 'local/scratch/recall');
  mkdirSync(out, { recursive: true });
  writeAtomic(join(out, 'proof.json'), `${JSON.stringify(result, null, 2)}\n`);
  const pct = (x) => (x === null ? 'n/a' : `${(x * 100).toFixed(1)}%`);
  console.log(`recall over ${since} to ${until}, pinned at ${result.tool.slice(0, 9)}: ${commits.length} first-parent commits, ${result.directCommits} direct`);
  for (const each of result.sources) console.log(`  ${each.source}: ${each.count}, span ${each.span ? each.span.join(' to ') : 'none'}, lag ${each.lagHours ? `median ${each.lagHours.median} h, max ${each.lagHours.max} h` : 'unknown'}`);
  console.log(`  all: lower bound ${pct(recall.lowerBound)} (${JSON.stringify(recall.counts)}), ${pct(recall.ifUnsettledSelected)} were every unsettled one selected`);
  console.log(`  FIX-LOG alone: lower bound ${pct(fixOnly.lowerBound)} (${JSON.stringify(fixOnly.counts)})`);
  console.log(`  structurally at a FIX-LOG entry's named commit, unconfirmed by runs: ${JSON.stringify(structuralCounts)}`);
  return 0;
}

if (isMain(import.meta.url)) {
  const [verb, ...rest] = process.argv.slice(2);
  (verb === 'proof' ? proof(rest) : Promise.resolve((console.error('usage: node tools/test-recall.mjs proof [--since <date>] [--until <date>] [--main <checkout>]'), 2))).then(
    (code) => { process.exitCode = code; },
    (error) => {
      console.error(`test-recall: ${error.message}`);
      process.exitCode = 2;
    },
  );
}

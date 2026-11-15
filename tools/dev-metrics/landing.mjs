import { appendFileSync, mkdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { builderOf } from './builder.mjs';
import { landedBranches, mainMeasures, mergeRowOf } from './red-rate.mjs';
import { missing } from './records.mjs';

export const LANDINGS = 'local/metrics/landings.jsonl';

/** One landed commit's record: the builder's measures, the gates' row for its branch, main's standing. A source with nothing is null and says where it is logged. */
export function landingRecord(home, { commit, branch, at = new Date().toISOString() }, options = {}) {
  const built = builderOf(branch, home, options);
  const row = mergeRowOf(home, branch);
  return {
    schema: 1, commit, branch, at,
    builder: built.builder, ...(built.builder ? {} : { builderMissing: built.missing }),
    gates: row ? { passed: row.passed, firstPass: row.firstPass, ms: row.ms } : null,
    ...(row ? {} : { gatesMissing: missing(`${branch} in local/merge-record.jsonl`) }),
    main: mainMeasures(home, Date.parse(at)),
  };
}

/** One record appended to the machine-local log. Never throws: a measure that fails must not hold a landing. */
export function recordLanding(home, landing, options = {}) {
  try {
    const file = join(home, LANDINGS);
    mkdirSync(dirname(file), { recursive: true });
    appendFileSync(file, `${JSON.stringify(landingRecord(home, landing, options))}\n`);
    return true;
  } catch {
    return false;
  }
}

export function readLandings(home, { since } = {}) {
  let text = '';
  try { text = readFileSync(join(home, LANDINGS), 'utf8'); } catch { return []; }
  return text.split(/\r?\n/).filter(Boolean).flatMap(l => { try { return [JSON.parse(l)]; } catch { return []; } })
    .filter(r => !since || r.at.slice(0, 10) >= since);
}

/** The log's records, and for each branch the merge record shows landed before the log began, a record built now from the same sources (commit null). */
export function landingsSince(home, { since, options } = {}) {
  const logged = readLandings(home, { since });
  const known = new Set(readLandings(home).map(r => r.branch));
  const history = landedBranches(home)
    .filter(l => !known.has(l.branch) && (!since || l.at.slice(0, 10) >= since))
    .map(l => landingRecord(home, { commit: null, branch: l.branch, at: l.at }, options));
  return [...history, ...logged];
}

const mean =values => values.length ? Math.round(10 * values.reduce((a, b) => a + b, 0) / values.length) / 10 : null;

/** The three measures over a set of landing records: the context read before the first edit, the change's wall time and tokens, main's standing. */
export function summarizeLandings(records, home, now = Date.now()) {
  const built = records.filter(r => r.builder);
  const edited = built.filter(r => r.builder.preEditBytes !== null);
  return {
    landings: records.length,
    withBuilder: built.length,
    withoutBuilder: records.filter(r => !r.builder).map(r => ({ branch: r.branch, missing: r.builderMissing })),
    preEdit: edited.length
      ? { meanBytes: mean(edited.map(r => r.builder.preEditBytes)), meanTokens: mean(edited.map(r => r.builder.preEditTokens)), meanStartupTokens: mean(edited.map(r => r.builder.startupTokens)) }
      : missing('no landing in the range has a builder transcript with an edit'),
    change: built.length
      ? { meanWallMinutes: mean(built.map(r => r.builder.wallMinutes)), tokens: Object.fromEntries(['input', 'output', 'cacheRead', 'cacheWrite'].map(k => [k, built.reduce((n, r) => n + r.builder.tokens[k], 0)])) }
      : missing('no landing in the range has a builder transcript'),
    main: mainMeasures(home, now),
  };
}

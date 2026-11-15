import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { readHeavy } from '../merge/heavy-after.ts';
import { MERGE_RECORD, readRecords } from '../merge/merge-record.ts';
import { json, missing } from './records.mjs';

const DAY = 24 * 60 * 60 * 1000;
const CI_RUNS = 'local/ci-runs.jsonl';
const CI_STATE = 'local/ci.json';

const jsonLines = file => {
  try { return readFileSync(file, 'utf8').split(/\r?\n/).filter(Boolean).flatMap(l => { try { return [JSON.parse(l)]; } catch { return []; } }); } catch { return []; }
};

/** A branch's newest merge-record row, the passing one where it has one. */
export function mergeRowOf(home, branch) {
  const rows = readRecords(join(home, MERGE_RECORD)).filter(r => r.branch === branch);
  return rows.filter(r => r.passed).at(-1) ?? rows.at(-1) ?? null;
}

/** Every branch that landed (a passing merge-record row), with whether it counts red and why. */
export function landedBranches(home) {
  const heavy = readHeavy(home);
  const ci = jsonLines(join(home, CI_RUNS)).filter(r => r.finished);
  const byBranch = new Map();
  for (const r of readRecords(join(home, MERGE_RECORD))) byBranch.set(r.branch, [...(byBranch.get(r.branch) ?? []), r]);
  const out = [];
  for (const [branch, rows] of byBranch) {
    const landed = rows.filter(r => r.passed).at(-1);
    if (!landed) continue;
    const runs = heavy.filter(h => h.branches.includes(branch));
    const commits = new Set(runs.map(h => h.commit));
    const why = [];
    if (rows.some(r => r.firstPass === false)) why.push('the merged-result gates failed before passing');
    if (rows.some(r => (r.mainReds?.length ?? 0) > 0)) why.push('main stood red on a gate');
    if (runs.some(h => h.state === 'red')) why.push('its heavy run settled red');
    if (ci.some(c => c.state === 'failed' && commits.has(c.commit))) why.push('a CI run on its commit ended red');
    out.push({ branch, at: landed.at, red: why.length > 0, why });
  }
  return out.sort((a, b) => Date.parse(a.at) - Date.parse(b.at));
}

const rate = (landed, now, days) => {
  const inside = landed.filter(l => Date.parse(l.at) >= now - days * DAY && Date.parse(l.at) <= now);
  if (!inside.length) return missing(`no landing in the last ${days} days in ${MERGE_RECORD}`);
  const red = inside.filter(l => l.red).length;
  return { rate: Math.round(1000 * red / inside.length) / 1000, landings: inside.length, red };
};

/** Main's measures: the red rate of the landings of the last 7 and 30 days, and the days since the CI's last green. */
export function mainMeasures(home, now = Date.now()) {
  const landed = landedBranches(home);
  const green = json(join(home, CI_STATE))?.lastGreen;
  const at = green?.at && Number.isFinite(Date.parse(green.at)) ? green.at : null;
  return {
    redRate7d: rate(landed, now, 7), redRate30d: rate(landed, now, 30),
    daysSinceGreen: at ? Math.round(10 * (now - Date.parse(at)) / DAY) / 10 : missing(`${CI_STATE} lastGreen (written by merge-branch --ci)`),
    lastGreen: at ?? missing(`${CI_STATE} lastGreen`),
  };
}

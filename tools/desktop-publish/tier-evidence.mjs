import { spawnSync } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

/**
 * What a stage rests on: the tiers the merge tool runs. A merge runs the fast tier and lands only on a pass; the run after the
 * landing (`tools/merge/heavy-after.ts`) runs the heavy tier on the new tip and records its state per commit in
 * `local/heavy-runs.jsonl`. A commit with a green run (or whose red classes are each already filed as red findings) passed both. Read
 * as plain files, never through the merge tool, so nothing the deployment rehearsal runs imports it.
 */
const HEAVY_RUNS = 'local/heavy-runs.jsonl';
const FINDINGS = 'local/ci-findings.json';

const readJson = (file) => {
  try {
    return JSON.parse(readFileSync(file, 'utf8'));
  } catch {
    return null;
  }
};

/** Each run's newest record, as the file's later lines supersede its earlier ones. */
function heavyRuns(home) {
  const file = join(home, HEAVY_RUNS);
  if (!existsSync(file)) return [];
  const byId = new Map();
  for (const line of readFileSync(file, 'utf8').split(/\r?\n/).filter(Boolean)) {
    try {
      const run = JSON.parse(line);
      if (typeof run?.id === 'string' && typeof run?.commit === 'string') byId.set(run.id, run);
    } catch { /* a line that does not read is no run's */ }
  }
  return [...byId.values()];
}

/** Whether the findings hold this class (`gate|unit`) as an open red. */
const filedRed = (findings, id) => findings.some((one) => one.state === 'red' && `${one.gate}|${one.unit}` === id);

const headOf = (repoRoot) => {
  const asked = spawnSync('git', ['rev-parse', 'HEAD'], { cwd: repoRoot, encoding: 'utf8', windowsHide: true });
  return asked.status === 0 ? asked.stdout.trim() : null;
};

/**
 * Whether `commit` (HEAD of `repoRoot` unless named) passed the tiers, read from the checkout `home`'s records: `code` 0 when it did,
 * 1 when something is lacking (each said on a line, with the command that supplies it in `remedy`), 2 when it cannot be told.
 */
export function askTiers(repoRoot, { home = repoRoot, commit = null } = {}) {
  const sha = commit ?? headOf(repoRoot);
  if (!sha) return { code: 2, out: 'could not read the commit to stage' };
  const short = sha.slice(0, 7);
  const mine = heavyRuns(home).filter((run) => sha.startsWith(run.commit) || run.commit.startsWith(sha))
    .sort((a, b) => String(a.at).localeCompare(String(b.at))).at(-1);
  const byHand = `node tools/heavy-run.mjs --id heavy-${short}-by-hand --commit ${sha} --base ${sha}~1`;
  if (!mine) {
    return {
      code: 1,
      out: `no heavy run is recorded for ${short}: the fast tier passes at a merge and the heavy run starts when it lands, so a commit no merge landed has neither`,
      remedy: { what: 'the heavy tier', command: byHand },
    };
  }
  if (mine.state === 'running') return { code: 1, out: `heavy run ${mine.id} is still running on ${short} since ${mine.at}`, remedy: { what: 'the heavy run to end', command: `its log local/scratch/heavy-${short}.log says when` } };
  if (mine.state === 'error') return { code: 1, out: `heavy run ${mine.id} ended in error: ${mine.note ?? 'no counts'}`, remedy: { what: 'the heavy tier again', command: byHand } };
  if (mine.state === 'red') {
    const findings = readJson(join(home, FINDINGS))?.findings ?? [];
    const reds = mine.classes?.red ?? [];
    const unfiled = reds.filter((id) => !filedRed(findings, id));
    if (unfiled.length || reds.length === 0) {
      return { code: 1, out: `heavy run ${mine.id} is red on ${(unfiled.length ? unfiled : ['classes it does not name']).join(', ')}, which no finding holds as main's red`, remedy: { what: 'the red fixed or filed', command: byHand } };
    }
    return { code: 0, out: `the fast tier passed at the merge of ${short}, and heavy run ${mine.id} is red on ${reds.join(', ')}, each filed as main's red` };
  }
  return { code: 0, out: `the fast tier passed at the merge of ${short}, and heavy run ${mine.id} is green on ${short}` };
}

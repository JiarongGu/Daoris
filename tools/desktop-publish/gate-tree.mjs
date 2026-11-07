import { closeSync, mkdirSync, openSync, readFileSync, rmSync, writeSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { appendStage, readStageHistory, stageRange, stageRangeLines } from '../ci-background.mjs';
import { spawnGit, writeAtomic } from '../fsx.mjs';
import { running } from '../processes.mjs';
import { refusal } from './staging.mjs';

/** `merge-branch`'s three names for the full set's own worktree, its lock and its record, under the main checkout. */
export const GATE_TREE = 'local/gate-tree';
export const GATE_TREE_LOCK = 'local/gate-tree.lock';
export const GATE_TREE_RECORD = 'local/gate-tree.json';

/** Whether a process runs with this pid, as the merge tool asks of its lock: signal 0 sends nothing, and EPERM is one that runs. */
function pidAlive(pid) {
  if (!Number.isInteger(pid) || pid <= 0) return false;
  try {
    process.kill(pid, 0);
    return true;
  } catch (error) {
    return error?.code === 'EPERM';
  }
}

export const gitIn = (cwd, args) => spawnGit(args, { cwd, encoding: 'utf8', windowsHide: true });
export const samePath = (a, b) => (process.platform === 'win32' ? resolve(a).toLowerCase() === resolve(b).toLowerCase() : resolve(a) === resolve(b));
const minuteOf = (iso) => (/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}/.test(iso ?? '') ? `${iso.slice(0, 10)} ${iso.slice(11, 16)} UTC` : 'an unknown time');

/** The gate tree's lock as written, or null where there is none or it does not read. */
function readTreeLock(file) {
  try {
    return JSON.parse(readFileSync(file, 'utf8'));
  } catch {
    return null;
  }
}

/** Why a held gate tree is not staged, by who holds it: a CI run, another stage, or a full set beside. */
function heldSaid(lock) {
  const since = `pid ${lock.pid}, since ${minuteOf(lock.at)}`;
  const sha = String(lock.commit ?? '').slice(0, 7);
  if (lock.holder === 'ci') return `desktop-publish: the gate tree is not staged while a CI run is gating it (${since}, on ${sha}): its verdicts are not in yet`;
  if (lock.holder === 'stage') return `desktop-publish: the gate tree is not staged while another stage of ${sha} is building from it (${since})`;
  return `desktop-publish: the gate tree is not staged while its full set is still running (${since}): its verdicts are not in yet`;
}

/**
 * The gate tree to build `commit` from, or the refusal that says why it cannot be: no gate tree under the
 * main checkout, one that holds another commit (main moved on, or a later run moved it), or one a run holds (a CI run or a
 * full set, whose verdicts are not in yet, or another stage). `home` is the main checkout, where its record is kept.
 *
 * `hold` takes the gate tree's lock for the stage (`holder: 'stage'`) and answers its `release`, so no CI run, which a
 * watch starts as main moves, moves the tree while it builds: a run started meanwhile is refused by the lock as a second run is.
 * It is taken only where none is held (`wx`), a dead process's taken over, and the commit read
 * again under it, so a run that moved the tree between the look and the take is caught.
 */
export function gatedSource(repoRoot, commit, { alive = pidAlive, hold = false, now = () => new Date().toISOString().replace(/\.\d+Z$/, 'Z') } = {}) {
  const listed = gitIn(repoRoot, ['worktree', 'list', '--porcelain']);
  if (listed.error || listed.status !== 0) return { refusal: `desktop-publish: git could not list the worktrees: ${(listed.stderr || listed.error?.message || '').trim()}` };
  const paths = listed.stdout.split(/\r?\n/).filter((line) => line.startsWith('worktree ')).map((line) => resolve(line.slice('worktree '.length).trim()));
  const home = paths[0] ?? repoRoot;
  const tree = join(home, ...GATE_TREE.split('/'));
  const short = String(commit).slice(0, 7);
  if (!paths.some((path) => samePath(path, tree))) {
    return { refusal: `desktop-publish: no gate tree at ${GATE_TREE}: node tools/merge-branch.mjs --ci --stage ${short} makes one and gates it` };
  }
  const wanted = gitIn(home, ['rev-parse', '--verify', '--quiet', `${commit}^{commit}`]);
  if (wanted.status !== 0) return { refusal: `desktop-publish: no commit '${commit}' to stage from the gate tree` };
  const want = wanted.stdout.trim();
  const holds = () => {
    const head = gitIn(tree, ['rev-parse', 'HEAD']).stdout.trim();
    return head === want ? null : `desktop-publish: the gate tree holds ${head.slice(0, 7) || 'no commit'}, not ${want.slice(0, 7)}: node tools/merge-branch.mjs --ci --stage ${want.slice(0, 7)} gates that one there`;
  };
  const other = holds();
  if (other) return { refusal: other };
  const lockFile = join(home, ...GATE_TREE_LOCK.split('/'));
  const lock = readTreeLock(lockFile);
  if (lock && alive(Number(lock.pid))) return { refusal: heldSaid(lock) };
  if (!hold) return { refusal: null, tree, commit: want, home };
  // A lock whose process ended, or that does not read, is no run's, as the merge tool reads it: taken over.
  rmSync(lockFile, { force: true });
  let fd;
  try {
    mkdirSync(dirname(lockFile), { recursive: true });
    fd = openSync(lockFile, 'wx');
  } catch (error) {
    const taken = readTreeLock(lockFile);
    return { refusal: error?.code === 'EEXIST' && taken ? heldSaid(taken) : `desktop-publish: the gate tree's lock could not be taken: ${error?.message ?? error}` };
  }
  try {
    writeSync(fd, `${JSON.stringify({ pid: process.pid, commit: want, at: now(), holder: 'stage' })}\n`);
  } finally {
    closeSync(fd);
  }
  const release = () => {
    if (readTreeLock(lockFile)?.pid === process.pid) rmSync(lockFile, { force: true });
  };
  const moved = holds();
  if (moved) {
    release();
    return { refusal: moved };
  }
  return { refusal: null, tree, commit: want, home, release };
}

/** The arguments the gate tree's own publish runs with: these, but `--from-gated` and its commit, and `--to` absolute. */
export function childArgs(argv, to) {
  const out = [];
  for (let at = 0; at < argv.length; at += 1) {
    const arg = argv[at];
    const next = argv[at + 1];
    if (arg === '--from-gated' || arg === '--to') {
      if (next !== undefined && !next.startsWith('--')) at += 1;
      if (arg === '--to') out.push('--to', to);
    } else out.push(arg);
  }
  return out;
}

/** The record's note that the gate tree at `commit` is staged, which lets the next merge's prune remove it. The rest is kept. */
export function recordStaged(home, { commit, to, at }) {
  const file = join(home, ...GATE_TREE_RECORD.split('/'));
  let record = {};
  try {
    const parsed = JSON.parse(readFileSync(file, 'utf8'));
    if (parsed && typeof parsed === 'object') record = parsed;
  } catch {
    record = { schema: 1 };
  }
  writeAtomic(file, `${JSON.stringify({ ...record, staged: { commit, to, at } }, null, 2)}\n`);
}

/**
 * What a stage of `commit` brings since the last one, as lines, read from the main checkout's
 * `local/stage-history.jsonl`, which no CI run rewrites and no prune deletes (the gate tree's `staged` is no baseline). Where it
 * cannot say (no history yet, a last staged commit that is not an ancestor of this one), the range is said to be unknown;
 * nothing the stage decides depends on it.
 */
export function stageReport(home, commit) {
  const range = stageRange(readStageHistory(home), commit, {
    isAncestor: (a, b) => gitIn(home, ['merge-base', '--is-ancestor', a, b]).status === 0,
    commits: (a, b) => (gitIn(home, ['log', '--format=%h %s', `${a}..${b}`]).stdout ?? '').split(/\r?\n/).filter(Boolean),
  });
  return stageRangeLines(range);
}

/**
 * What a stage says of the drain it starts, printed after it. Staged while the install is idle, the install
 * drains: it starts nothing new and holds the presses that would start work until every driven session ends; the terminal's
 * `update --cancel` stages the build *not now* instead.
 */
export function stagedAdvice() {
  return [
    '  The desktop drains for it: it starts nothing new and holds the presses that would start work until every driven',
    '  session and turn has ended, then installs it and starts again (D139). `daoris update` says where it stands;',
    '  `daoris update --cancel` stages it *not now* (installed at the next start, or `update --when-idle` later),',
    '  and --now installs without waiting.',
  ];
}

/** A successful stage appended to the main checkout's history: the commit, its tree, where it went, when, forced or not. */
export function recordStage(home, { commit, to, at, forced = false }) {
  const tree = gitIn(home, ['rev-parse', '--verify', '--quiet', `${commit}^{tree}`]).stdout?.trim() || null;
  appendStage(home, { commit, tree, to, at, forced });
}

/** The main checkout of the repository at `repoRoot`: the first worktree git lists, or `repoRoot` where git cannot say. */
export function mainCheckout(repoRoot) {
  const listed = gitIn(repoRoot, ['worktree', 'list', '--porcelain']);
  const first = listed.status === 0 ? listed.stdout.split(/\r?\n/).find((line) => line.startsWith('worktree ')) : null;
  return first ? resolve(first.slice('worktree '.length).trim()) : repoRoot;
}

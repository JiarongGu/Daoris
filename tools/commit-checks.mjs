/**
 * The bookkeeping checks at the commit (the `pre-commit` hook `tools/index-hook.mjs` installs), on the staged paths, in seconds, so a
 * merge's `cli` gate finds them done: the comment gate on the staged sources, the size ratchet, the contracts where a contract source is
 * staged and the layers' counts where a `.cs` or `.csproj` is.
 *
 *   node tools/commit-checks.mjs [--root <dir>]   exit 0 passed or not run · 1 refused: a comment rise, a file grown past the size
 *                                                 list, a layers count breached (nothing else ever blocks a commit)
 *
 * A size shrink is written to `tools/size-ratchet.json` and staged into the commit; a stale contract is regenerated in the work tree
 * (its files are ignored). A step that cannot run says so and lets the commit go on. `DAORIS_COMMIT_HOOK=0`, a merge in progress and a
 * commit with nothing staged run nothing.
 */
import { spawnSync } from 'node:child_process';
import { existsSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { checkChange } from './comment-gate.mjs';
import { declares } from './contracts.mjs';
import { execGit, isMain, writeAtomic } from './fsx.mjs';
import { EMPTY, LIST_FILE, countProblems, treeAt } from './layers-census/counts.mjs';
import { baseList, settleStaged, sized, sizeOf } from './size-ratchet.mjs';

const SIZE_LIST = 'tools/size-ratchet.json';
const CODE = /\.(cs|csproj)$/;

const git = (root, args) => execGit(args, { cwd: root, encoding: 'utf8', maxBuffer: 256 * 1024 * 1024, stdio: ['ignore', 'pipe', 'pipe'] });
const gitOrNull = (root, args) => {
  try {
    return git(root, args);
  } catch {
    return null;
  }
};

/** The layers' counts of the tree at `root` against its list; `{ code, problems }`. */
export function layersCounts(root) {
  const list = { ...EMPTY, ...JSON.parse(readFileSync(join(root, LIST_FILE), 'utf8')) };
  const problems = countProblems(treeAt(root), list);
  return { code: problems.length ? 1 : 0, problems };
}

/** The contracts' writer run where they are stale (`tools/contracts.mjs --if-stale`); a repository without it has none to write. */
export function contractsIfStale(root) {
  const tool = join(root, 'tools', 'contracts.mjs');
  if (!existsSync(tool)) return;
  const ran = spawnSync(process.execPath, [tool, '--if-stale'], { cwd: root, encoding: 'utf8', windowsHide: true });
  if (ran.status !== 0) throw new Error(`tools/contracts.mjs --if-stale exited ${ran.status}: ${(ran.stderr || ran.stdout || '').trim().split('\n').at(-1)}`);
}

/** Runs the checks on what is staged at `root`; the exit code. The steps are injectable (`counts`, `contracts`) for tests. */
export function runCommitChecks({ root, env = process.env, say = console.log, err = console.error, counts = layersCounts, contracts = contractsIfStale, now = Date.now }) {
  if (env.DAORIS_COMMIT_HOOK === '0' || gitOrNull(root, ['rev-parse', '-q', '--verify', 'MERGE_HEAD']) !== null) return 0;
  const staged = (gitOrNull(root, ['--no-optional-locks', 'diff', '--cached', '--name-only', '-z', '--no-renames']) ?? '').split('\0').filter(Boolean);
  if (staged.length === 0) return 0;
  const started = now();
  const done = [];
  const failures = [];
  const step = (name, body) => {
    try {
      body();
    } catch (error) {
      err(`commit checks: ${name} not run: ${error.message}`);
    }
  };

  step('comments', () => {
    const base = gitOrNull(root, ['merge-base', 'main', 'HEAD'])?.trim();
    if (!base) throw new Error('no merge-base with main');
    const { problems, checked } = checkChange(root, base, null, { staged: true, only: staged });
    for (const problem of problems) err(`comment-gate: ${problem}`);
    if (problems.length) failures.push('comments');
    else if (checked) done.push(`comments clean (${checked} file${checked === 1 ? '' : 's'})`);
  });

  const sources = staged.filter(sized);
  if (sources.length && existsSync(join(root, SIZE_LIST))) {
    step('size', () => {
      const sizes = Object.fromEntries(sources.map((path) => {
        const text = gitOrNull(root, ['show', `:${path}`]);
        return [path, text === null ? null : sizeOf(text)];
      }));
      const list = JSON.parse(readFileSync(join(root, SIZE_LIST), 'utf8'));
      let base = null;
      try {
        base = baseList(root);
      } catch {
        // No main to hold the list to: the list's own sizes are the limit.
      }
      const settled = settleStaged(sizes, list, base);
      for (const problem of settled.problems) err(`size-ratchet: ${problem}`);
      if (settled.problems.length) {
        failures.push('size');
        return;
      }
      const lowered = Object.keys(list).filter((path) => settled.list[path] !== list[path]).length;
      if (lowered) {
        writeAtomic(join(root, SIZE_LIST), `${JSON.stringify(settled.list, null, 2)}\n`);
        git(root, ['add', '--', SIZE_LIST]);
        done.push(`size list lowered (${lowered})`);
      } else {
        done.push('size clean');
      }
    });
  }

  if (staged.some(declares)) {
    step('contracts', () => {
      contracts(root);
      done.push('contracts regenerated if stale');
    });
  }

  if (staged.some((path) => CODE.test(path))) {
    step('layers counts', () => {
      const { code, problems } = counts(root);
      for (const problem of problems) err(`layers-census: ${problem}`);
      if (code !== 0) failures.push('layers counts');
      else done.push('layers counts clean');
    });
  }

  const seconds = ((now() - started) / 1000).toFixed(1);
  if (failures.length) {
    err(`commit checks: refused by ${failures.join(', ')} in ${seconds}s; fix the lines above (the merge runs these again; DAORIS_COMMIT_HOOK=0 skips them for one commit)`);
    return 1;
  }
  if (done.length) say(`commit checks: ${done.join(' · ')} in ${seconds}s`);
  return 0;
}

function main(argv) {
  const at = argv.indexOf('--root');
  const root = at >= 0 ? argv[at + 1] : dirname(dirname(fileURLToPath(import.meta.url)));
  try {
    return runCommitChecks({ root });
  } catch (error) {
    console.error(`commit checks: not run: ${error.message}`);
    return 0;
  }
}

if (isMain(import.meta.url)) process.exitCode = main(process.argv.slice(2));

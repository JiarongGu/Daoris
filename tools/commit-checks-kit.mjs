/**
 * What the commit-checks test files share (`commit-checks.test.mjs`, `commit-checks-layers.test.mjs`): a scratch folder of each file's
 * own under `_fixtures/`, real git with the identity and settings a case needs, and `repository(files)`, a repository on `main` holding
 * `files` with the commit hook installed from this checkout's tools, built once for each set of files and copied for each case
 * (`hook-seeds.mjs`). The files are two so the runner runs them side by side: every case is a few dozen process starts, and one file
 * of all of them held the merge's `reached` gate longer than any other step.
 */
import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { after } from 'node:test';
import { fileURLToPath } from 'node:url';
import { seeds } from './hook-seeds.mjs';
import { install } from './index-hook.mjs';

const here = dirname(fileURLToPath(import.meta.url));

/** The kit of one test file, which names its scratch folder (removed as the file ends). */
export function commitKit(name) {
  const scratch = join(here, '..', '_fixtures', name);
  rmSync(scratch, { recursive: true, force: true });
  mkdirSync(scratch, { recursive: true });
  after(() => rmSync(scratch, { recursive: true, force: true }));

  const env = () => {
    const out = { ...process.env, DAORIS_INDEX_HOOK: '0' };
    for (const key of ['GIT_DIR', 'GIT_WORK_TREE', 'GIT_INDEX_FILE', 'DAORIS_COMMIT_HOOK']) delete out[key];
    return out;
  };
  const git = (cwd, ...args) => spawnSync('git', ['-c', 'user.name=t', '-c', 'user.email=t@t', '-c', 'core.autocrlf=false', ...args],
    { cwd, encoding: 'utf8', env: env(), windowsHide: true });
  const put = (dir, files) => {
    for (const [rel, text] of Object.entries(files)) {
      mkdirSync(dirname(join(dir, rel)), { recursive: true });
      writeFileSync(join(dir, rel), text);
    }
  };
  const kit = seeds(scratch);
  /** A repository on `main` holding `files`, the hook installed from this checkout's tools; git's own maintenance is off, so no commit starts one behind it. */
  const repository = (files) => kit.copy(JSON.stringify(files), (dir) => {
    git(dir, 'init', '-q', '-b', 'main');
    git(dir, 'config', '--local', 'maintenance.auto', 'false');
    put(dir, files);
    git(dir, 'add', '-A');
    assert.equal(git(dir, 'commit', '-q', '--no-verify', '-m', 'base').status, 0);
    assert.equal(install({ out() {}, err() {}, cwd: dir, env: env(), script: join(here, 'index-hook.mjs') }), 0);
  });
  const commit = (dir, ...more) => {
    git(dir, 'add', '-A');
    return git(dir, 'commit', '-q', '-m', 'change', ...more);
  };
  return { env, git, put, repository, commit };
}

export const code = (lines) => `${lines.map((line, i) => `// ${line}\nexport const v${i} = ${i};`).join('\n')}\n`;
export const plain = Array.from({ length: 20 }, (_, i) => `export const v${i} = ${i};`).join('\n') + '\n';

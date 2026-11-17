/**
 * What keeps an end-to-end spec's case short (E2EFAST1): how many of a file's cases run at once, a scratch repository seeded once
 * per test process and copied per case, and a branch written in one git start. A case's time is the git and tool starts it makes,
 * each slower the more the machine starts at once, so a case makes fewer and shares the machine with fewer.
 */
import { cpSync } from 'node:fs';
import { spawnSync } from 'node:child_process';
import assert from 'node:assert/strict';

/**
 * How many cases of one end-to-end file run at once. A case is a chain of a hundred or more real git and tool starts; with every
 * case of every file side by side, a start took seconds and a case 200 to 270 s. The tier's script runs a few files at once
 * (`--test-concurrency`), so the machine holds their product; a case waiting for its turn is not timed.
 */
export const CASES_AT_ONCE = 3;

/**
 * A repository `build` makes once in this test process, on the first case that asks, and `copy` lays at a case's place by a file
 * copy, starting no process; the seed is removed as the process exits. Every case gets the same first commit.
 */
export function seeded(build: () => { root: string; cleanup: () => void }): (to: string) => void {
  let seed: string | null = null;
  return (to) => {
    if (seed === null) {
      const made = build();
      seed = made.root;
      process.once('exit', made.cleanup);
    }
    cpSync(seed, to, { recursive: true });
  };
}

/** A branch to write: its name, the commit it starts from, its files by repository path, and its commit message. */
export interface BranchCommit { name: string; from: string; files: Record<string, string>; message: string }

/**
 * `branch` written by one `git fast-import` in the repository at `cwd`, with the case's environment `env` (its identity from
 * `GIT_COMMITTER_*`): one commit on `from` holding `files`, the checkout and its index untouched. It stands for a checkout of a
 * new branch, an add, a commit and a checkout back, four starts that leave the same branch and the same checkout.
 */
export function importBranch(cwd: string, env: Record<string, string | undefined>, branch: BranchCommit): void {
  const data = (text: string) => `data ${Buffer.byteLength(text)}\n${text}\n`;
  const who = `${env['GIT_COMMITTER_NAME'] ?? 'Fixture'} <${env['GIT_COMMITTER_EMAIL'] ?? 'fixture@example.test'}>`;
  const message = branch.message.endsWith('\n') ? branch.message : `${branch.message}\n`;
  const stream = [
    `commit refs/heads/${branch.name}\n`,
    `committer ${who} ${Math.floor(Date.now() / 1000)} +0000\n`,
    data(message),
    `from ${branch.from}\n`,
    ...Object.entries(branch.files).map(([path, text]) => `M 100644 inline ${path.replaceAll('\\', '/')}\n${data(text)}`),
    '\n',
  ].join('');
  const result = spawnSync('git', ['fast-import', '--quiet'], { cwd, env, input: stream, encoding: 'utf8', windowsHide: true });
  assert.equal(result.status, 0, `git fast-import of ${branch.name}: ${result.stderr}`);
}

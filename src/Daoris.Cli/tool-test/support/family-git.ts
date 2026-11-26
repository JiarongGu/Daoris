import { spawnSync } from 'node:child_process';
import { mkdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';

/**
 * Real git for the family's tests, run in a fixture's folder: the code under test reads git's files and never starts git, so these
 * repositories are laid out by git itself and read back by the reader. Each start is counted against a loaded machine's clock, so a
 * history of many commits is written by one `fast-import`.
 */
export function git(cwd: string, ...args: string[]): string {
  return gitWith(cwd, undefined, ...args);
}

function gitWith(cwd: string, input: string | undefined, ...args: string[]): string {
  const result = spawnSync('git', ['-c', 'user.name=Family Test', '-c', 'user.email=family@example.invalid', '-c', 'commit.gpgsign=false',
    '-c', 'tag.gpgsign=false', '-c', 'core.autocrlf=false', ...args], { cwd, input, encoding: 'utf8', windowsHide: true });
  if (result.status !== 0) throw new Error(`git ${args.join(' ')} failed in ${cwd}: ${result.stderr}`);
  return result.stdout.trim();
}

/** A repository on `main` with one empty commit, its id returned. */
export function initWithCommit(cwd: string, message = 'first'): string {
  git(cwd, 'init', '-q', '-b', 'main');
  return commit(cwd, message);
}

/** One more empty commit on the branch checked out, its id returned. */
export function commit(cwd: string, message: string): string {
  git(cwd, 'commit', '-q', '--allow-empty', '-m', message);
  return git(cwd, 'rev-parse', 'HEAD');
}

/**
 * One commit of the files given on the branch checked out, written over the tree (a null deletes the file), its id returned. A name
 * ending `*` is written executable.
 */
export function commitFiles(cwd: string, files: Record<string, string | null>, message: string): string {
  for (const [name, text] of Object.entries(files)) {
    const path = join(cwd, name.replace(/\*$/, ''));
    if (text === null) {
      rmSync(path, { force: true });
      continue;
    }
    mkdirSync(dirname(path), { recursive: true });
    writeFileSync(path, text);
  }
  git(cwd, 'add', '-A');
  for (const name of Object.keys(files).filter((each) => each.endsWith('*'))) git(cwd, 'update-index', '--chmod=+x', name.slice(0, -1));
  git(cwd, 'commit', '-q', '-m', message);
  return git(cwd, 'rev-parse', 'HEAD');
}

/** `count` commits on `main` of a new repository, each message long and alike so a repack stores them as deltas; ids oldest first. */
export function history(cwd: string, count: number): string[] {
  git(cwd, 'init', '-q', '-b', 'main');
  const lines: string[] = [];
  for (let index = 1; index <= count; index++) {
    const message = `${'the same long body of a commit message, '.repeat(60)}number ${index}\n`;
    lines.push('commit refs/heads/main', `mark :${index}`, `committer Family Test <family@example.invalid> ${1_700_000_000 + index} +0000`,
      `data ${Buffer.byteLength(message)}`, message);
    if (index > 1) lines.push(`from :${index - 1}`);
    lines.push('');
  }
  gitWith(cwd, `${lines.join('\n')}\n`, 'fast-import', '--quiet');
  return git(cwd, 'rev-list', '--reverse', 'main').split('\n');
}

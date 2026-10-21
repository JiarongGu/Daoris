// Git for Windows' launcher, started past (GITSTART4, after GITSTART1; D121 §2.4). The `git.exe` in an install's `cmd`
// folder is a launcher that starts `<top>\<prefix>\bin\git.exe` as a second process, and a start costs 100–700 ms on a
// loaded machine (PROC1a). Where the system's git is that launcher, the real git starts directly, handed the
// environment the launcher would have handed it.
//
// 🔴 A TWIN of the driver's `GitForWindows.cs`. The two share no code; `gitforwindows.test.ts` holds its tables to
// `GitForWindowsTests`, row for row. A rule changed here is changed there, in the same commit:
//
//   1. Recognised only where the layout is unmistakable: a `git.exe` in a `cmd` folder, a git under a known prefix
//      beside it, and `usr\bin`. An install with an `etc\git-bash.config`, whose `MSYS=` lines the launcher reads,
//      any other layout and any other platform start as named.
//   2. The launcher's variables are what it was measured setting (git 2.53.0.windows.3; FIX-LOG, GITSTART1): with no
//      `MSYSTEM` git repairs its own PATH in another order, and with any it repairs nothing, so `sh`, and with it every
//      hook and `!` alias, would go unfound; so the start sets them all.
//   3. A start whose PATH would pass one variable's 32,766 characters keeps the launcher.
//
// Only the system's git goes past: a git the person named or a managed one starts as it is (GITSTART5 is the managed
// one's). It stays PURE: it reads files and answers "which program, with which environment"; `tools/fsx.mjs`'s
// `spawnGit` starts the tools' git by it. The CLI's own `startChild` asked it too, until its one child became Daoris's driver
// program (PUSH1f2f), which starts its own git (`WorkingTree.GitStart`).

import { existsSync, statSync } from 'node:fs';
import { basename, dirname, isAbsolute, join, resolve } from 'node:path';
import { onPath } from '../platform/fsx.ts';

type Env = Record<string, string | undefined>;

/** The folders under an install's top that hold its git, in the order looked for. The driver's `GitForWindows.Prefixes`. */
export const GIT_PREFIXES: readonly string[] = ['mingw64', 'clangarm64', 'mingw32'];

/**
 * The longest `HOME` whose `bin` the launcher puts on PATH: its `MAX_PATH` is 4,096, not Windows' 260, so a `HOME` of
 * 4,088 characters still does and 4,089 does not (measured).
 */
const LONGEST_HOME = 4088;

/**
 * The longest PATH this start hands git: the largest value Windows sets in a variable, 32,767 characters with its
 * terminator. Past it a start keeps the launcher, exactly as before. The driver's `GitForWindows.LongestVariable`.
 */
export const LONGEST_VARIABLE = 32766;

/** A Git for Windows install's real git: the install's top folder, the prefix its git is under, and the file. */
export interface GitBehind {
  readonly top: string;
  readonly prefix: string;
  readonly git: string;
}

/** A program and the environment it starts with. */
export interface GitStart {
  readonly file: string;
  readonly env: Env;
}

const windows = process.platform === 'win32';
const same = (left: string, right: string) => left.toLowerCase() === right.toLowerCase();

function isFile(path: string): boolean {
  try {
    return statSync(path).isFile();
  } catch {
    return false;
  }
}

function isFolder(path: string): boolean {
  try {
    return statSync(path).isDirectory();
  } catch {
    return false;
  }
}

/** The real git behind the launcher at `file`, or null where it is not one. The driver's `GitForWindows.Behind`. */
export function gitBehind(file: string): GitBehind | null {
  if (!windows || !isAbsolute(file)) return null;
  if (!same(basename(file), 'git.exe')) return null;
  const folder = dirname(resolve(file));
  if (!same(basename(folder), 'cmd')) return null;
  const top = dirname(folder);
  if (top === folder || !isFolder(join(top, 'usr', 'bin')) || existsSync(join(top, 'etc', 'git-bash.config'))) return null;

  for (const prefix of GIT_PREFIXES) {
    const git = join(top, prefix, 'bin', 'git.exe');
    if (isFile(git)) return { top, prefix, git };
  }
  return null;
}

/**
 * The variables the launcher sets for its child, read from the environment it was started with: `MSYSTEM` named for
 * its prefix over any; `PLINK_PROTOCOL` `ssh` where unset; `HOME` where unset, from `HOMEDRIVE` and `HOMEPATH` where
 * that is a folder other than the system's, else `USERPROFILE`; and its two folders and `HOME`'s `bin` before PATH. A
 * variable set empty counts as set. The driver's `GitForWindows.ChildEnvironment`.
 *
 * @param variable A variable's value in that environment; undefined where it is not set.
 */
export function launcherEnvironment(behind: GitBehind, variable: (name: string) => string | undefined): Record<string, string> {
  const child: Record<string, string> = { MSYSTEM: behind.prefix.toUpperCase() };
  if (variable('PLINK_PROTOCOL') === undefined) child.PLINK_PROTOCOL = 'ssh';

  let home = variable('HOME');
  if (home === undefined) {
    const homePath = variable('HOMEPATH');
    if (homePath !== undefined) {
      const candidate = (variable('HOMEDRIVE') ?? '') + homePath;
      if (isFolder(candidate) && !isSystemFolder(candidate)) home = candidate;
    }
    home ??= variable('USERPROFILE');
    if (home !== undefined) child.HOME = home;
  }

  let path = `${join(behind.top, behind.prefix, 'bin')};${join(behind.top, 'usr', 'bin')};`;
  // Where nothing names a HOME the launcher still appends "\bin", as it does for one set empty (measured).
  if ((home?.length ?? 0) <= LONGEST_HOME) path += `${home !== undefined && home.endsWith('\\') ? `${home}bin` : `${home ?? ''}\\bin`};`;
  child.PATH = path + (variable('PATH') ?? '');
  return child;
}

/** The key `env` holds a variable under: whichever spelling it has, since Windows reads names in any case. */
const keyOf = (env: Env, name: string): string =>
  Object.keys(env).find((key) => same(key, name)) ?? name;

/**
 * The real git and its environment where `file` is the launcher: the launcher's variables over a copy of `env`, each
 * under the spelling `env` holds it in, so a child is never handed both `Path` and `PATH`. Null for any other start,
 * and for one whose PATH would pass `LONGEST_VARIABLE`. The driver's `GitForWindows.Direct`.
 */
export function pastLauncher(file: string, env: Env): GitStart | null {
  const behind = gitBehind(file);
  if (behind === null) return null;
  const child = launcherEnvironment(behind, (name) => env[keyOf(env, name)]);
  if (child.PATH!.length > LONGEST_VARIABLE) return null;
  const handed: Env = { ...env };
  for (const [name, value] of Object.entries(child)) handed[keyOf(env, name)] = value;
  return { file: behind.git, env: handed };
}

/**
 * The system's git for a start with `env`: the real one past the launcher where PATH finds Git for Windows', else the
 * bare name and `env` as they were, so a start that does not find git says so as before.
 */
export function systemGit(env: Env = process.env): GitStart {
  const found = windows ? onPath('git', { env, startable: true }) : null;
  return (found === null ? null : pastLauncher(found, env)) ?? { file: 'git', env };
}

function isSystemFolder(folder: string): boolean {
  const system = join(process.env.SystemRoot ?? process.env.windir ?? 'C:\\Windows', 'System32');
  const trimmed = (path: string) => resolve(path).replace(/[\\/]+$/, '');
  return same(trimmed(folder), trimmed(system));
}

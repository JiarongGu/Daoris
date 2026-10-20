import { test } from 'node:test';
import assert from 'node:assert/strict';
import { copyFileSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, parse } from 'node:path';
import {
  GIT_PREFIXES, LONGEST_VARIABLE, gitBehind, launcherEnvironment, pastLauncher, systemGit,
} from '../../src/tools/gitforwindows.ts';
import type { GitBehind } from '../../src/tools/gitforwindows.ts';
import { onPath } from '../../src/platform/fsx.ts';
import { homeEntry } from '../../src/platform/home.ts';
import { probe, readHarnessSettings } from '../../src/agents/toolchain.ts';
import type { Toolchain } from '../../src/agents/toolchain.ts';
import { driverRows as csharpRows, driverTestFile } from '../_csharp.ts';
import { makeFixture } from '../_fixture.ts';

/**
 * Git for Windows' launcher, started past (GITSTART4) — the CLI's half of a TWIN with the driver's `GitForWindows.cs`.
 * 🔴 `GitForWindowsTests.cs` holds the same tables, row for row and in the same order, and *the driver's tables are
 * these tables*, below, holds them cell for cell. The variables are the ones measured on git 2.53.0.windows.3 (FIX-LOG,
 * GITSTART1).
 *
 * An install is empty files and folders in a scratch folder; nothing starts a program but the toolchain's test, whose
 * launcher and git are copies of node.
 */

const windows = process.platform === 'win32';
const onWindows = { skip: !windows && 'Git for Windows is a Windows install' };

type Env = Record<string, string | undefined>;

function touch(file: string): void {
  mkdirSync(dirname(file), { recursive: true });
  writeFileSync(file, '');
}

function install(name: string) {
  const fx = makeFixture(name);
  const top = join(fx.root, 'Git');
  /** An install laid out as Git for Windows lays one, less whatever the case leaves out; its launcher's path. */
  const lay = ({ prefix = 'mingw64', launcherFolder = 'cmd', usr = true, real = true, bashConfig = false } = {}) => {
    const launcher = join(top, launcherFolder, 'git.exe');
    touch(launcher);
    if (real) touch(join(top, prefix, 'bin', 'git.exe'));
    if (usr) mkdirSync(join(top, 'usr', 'bin'), { recursive: true });
    if (bashConfig) touch(join(top, 'etc', 'git-bash.config'));
    return launcher;
  };
  const behind = (prefix = 'mingw64'): GitBehind => ({ top, prefix, git: join(top, prefix, 'bin', 'git.exe') });
  const folders = (prefix = 'mingw64') => `${join(top, prefix, 'bin')};${join(top, 'usr', 'bin')};`;
  return { fx, root: fx.root, top, lay, behind, folders };
}

const variables = (set: Env) => (name: string) => set[name];

// ——— The layout.

const PREFIX_ROWS: [string][] = [
  ['mingw64'],
  ['clangarm64'],
  ['mingw32'],
];

test('the launcher in a cmd folder beside its prefix and usr is recognised', onWindows, () => {
  assert.deepEqual(PREFIX_ROWS.map(([prefix]) => prefix), GIT_PREFIXES);
  for (const [index, [prefix]] of PREFIX_ROWS.entries()) {
    const at = install(`gitforwindows-prefix-${index}`);
    assert.deepEqual(gitBehind(at.lay({ prefix })), at.behind(prefix), prefix);
    at.fx.cleanup();
  }
});

test('the launcher is recognised in the case PATHEXT found it in', onWindows, () => {
  const at = install('gitforwindows-case');
  at.lay();
  assert.deepEqual(gitBehind(join(at.top, 'cmd', 'git.EXE')), at.behind());
  at.fx.cleanup();
});

const LAYOUT_ROWS: [string][] = [
  ['no usr folder'],
  ['no git under a prefix'],
  ['a git that is not in a cmd folder'],
  ['the real git itself'],
  ['a git-bash.config the launcher would read'],
  ['a bare name'],
];

test('any other layout is not the launcher', onWindows, () => {
  for (const [index, [layout]] of LAYOUT_ROWS.entries()) {
    const at = install(`gitforwindows-layout-${index}`);
    const laid = layout === 'no usr folder' ? at.lay({ usr: false })
      : layout === 'no git under a prefix' ? at.lay({ real: false })
        : layout === 'a git that is not in a cmd folder' ? at.lay({ launcherFolder: 'shims' })
          : layout === 'the real git itself' ? (at.lay(), at.behind().git)
            : layout === 'a git-bash.config the launcher would read' ? at.lay({ bashConfig: true })
              : 'git';
    assert.equal(gitBehind(laid), null, layout);
    at.fx.cleanup();
  }
});

test('a start of any other git is left as it was', onWindows, () => {
  const at = install('gitforwindows-other');
  assert.equal(pastLauncher(at.lay({ launcherFolder: 'shims' }), { PATH: 'C:\\a' }), null);
  at.fx.cleanup();
});

test('a start of the launcher starts its real git with the launcher’s variables, under the spelling the environment holds', onWindows, () => {
  const at = install('gitforwindows-direct');
  // The person's HOME, not a Daoris home: its bin is the launcher's folder, so it is spelled whole.
  const userHome = join(at.root, 'user');
  const env: Env = { Path: 'C:\\a;C:\\b', HOME: userHome, OTHER: 'x' };

  const start = pastLauncher(at.lay(), env)!;

  assert.equal(start.file, at.behind().git);
  assert.equal(start.env.MSYSTEM, 'MINGW64');
  assert.equal(start.env.HOME, userHome);
  assert.equal(start.env.Path, `${at.folders()}${userHome}\\bin;C:\\a;C:\\b`);
  assert.equal(start.env.PATH, undefined, 'never both `Path` and `PATH`');
  assert.equal(start.env.OTHER, 'x');
  assert.equal(env.Path, 'C:\\a;C:\\b', 'the environment handed in is not rewritten');
  at.fx.cleanup();
});

test('a PATH too long for one variable once the launcher’s folders are put first keeps the launcher', onWindows, () => {
  const at = install('gitforwindows-long-path');
  const launcher = at.lay();
  const userHome = join(at.root, 'user');
  const added = `${at.folders()}${userHome}\\bin;`.length;
  const env = (expanded: number): Env => ({ HOME: userHome, PATH: `C:\\${'p'.repeat(expanded - added - 3)}` });

  const fits = pastLauncher(launcher, env(LONGEST_VARIABLE));
  assert.equal(fits?.env.PATH?.length, LONGEST_VARIABLE);
  assert.equal(pastLauncher(launcher, env(LONGEST_VARIABLE + 1)), null);
  at.fx.cleanup();
});

// ——— The launcher's variables (measured: FIX-LOG, GITSTART1).

const MSYSTEM_ROWS: [string, string][] = [
  ['mingw64', 'MINGW64'],
  ['clangarm64', 'CLANGARM64'],
  ['mingw32', 'MINGW32'],
];

test('the launcher puts its folders and HOME’s bin first and names its MSYSTEM over any', onWindows, () => {
  const at = install('gitforwindows-msystem');
  for (const [prefix, msystem] of MSYSTEM_ROWS) {
    const child = launcherEnvironment(at.behind(prefix), variables({ PATH: 'C:\\a', HOME: 'C:\\h', MSYSTEM: 'PROBE' }));

    assert.equal(child.PATH, `${at.folders(prefix)}C:\\h\\bin;C:\\a`, prefix);
    assert.equal(child.MSYSTEM, msystem, prefix);
    assert.equal('HOME' in child, false, prefix);
  }
  at.fx.cleanup();
});

test('an unset PLINK_PROTOCOL is ssh, and a set one is kept even empty', onWindows, () => {
  const at = install('gitforwindows-plink');
  assert.equal(launcherEnvironment(at.behind(), variables({ HOME: 'C:\\h' })).PLINK_PROTOCOL, 'ssh');
  assert.equal('PLINK_PROTOCOL' in launcherEnvironment(at.behind(), variables({ HOME: 'C:\\h', PLINK_PROTOCOL: '' })), false);
  assert.equal('PLINK_PROTOCOL' in launcherEnvironment(at.behind(), variables({ HOME: 'C:\\h', PLINK_PROTOCOL: 'plink' })), false);
  at.fx.cleanup();
});

test('a set HOME is kept even empty, and its bin is named from it', onWindows, () => {
  const at = install('gitforwindows-empty-home');
  const child = launcherEnvironment(at.behind(), variables({ PATH: 'C:\\a', HOME: '', USERPROFILE: 'C:\\u' }));

  assert.equal('HOME' in child, false);
  assert.equal(child.PATH, `${at.folders()}\\bin;C:\\a`);
  at.fx.cleanup();
});

test('an unset HOME is HOMEDRIVE and HOMEPATH where that is a folder', onWindows, () => {
  const at = install('gitforwindows-homepath');
  const userHome = join(at.root, 'user');
  mkdirSync(userHome, { recursive: true });
  const drive = parse(userHome).root.replace(/\\$/, '');

  const child = launcherEnvironment(at.behind(),
    variables({ PATH: 'C:\\a', HOMEDRIVE: drive, HOMEPATH: userHome.slice(drive.length), USERPROFILE: 'C:\\u' }));

  assert.equal(child.HOME, userHome);
  assert.equal(child.PATH, `${at.folders()}${userHome}\\bin;C:\\a`);
  at.fx.cleanup();
});

test('an unset HOME is USERPROFILE where HOMEDRIVE and HOMEPATH is no folder', onWindows, () => {
  const at = install('gitforwindows-userprofile');
  const missing = join(at.root, 'no-such-home');
  const drive = parse(missing).root.replace(/\\$/, '');

  const child = launcherEnvironment(at.behind(),
    variables({ PATH: 'C:\\a', HOMEDRIVE: drive, HOMEPATH: missing.slice(drive.length), USERPROFILE: 'C:\\u' }));

  assert.equal(child.HOME, 'C:\\u');
  assert.equal(child.PATH, `${at.folders()}C:\\u\\bin;C:\\a`);
  at.fx.cleanup();
});

test('an unset HOME is never the system folder', onWindows, () => {
  const at = install('gitforwindows-system');
  const system = join(process.env.SystemRoot ?? 'C:\\Windows', 'System32');
  const drive = parse(system).root.replace(/\\$/, '');

  const child = launcherEnvironment(at.behind(), variables({ HOMEDRIVE: drive, HOMEPATH: system.slice(drive.length), USERPROFILE: 'C:\\u' }));

  assert.equal(child.HOME, 'C:\\u');
  at.fx.cleanup();
});

test('a HOME too long for the launcher’s buffer puts no bin on PATH', onWindows, () => {
  const at = install('gitforwindows-long-home');
  // Measured: the launcher's buffer is 4,096 characters, so a HOME of 4,088 still puts its bin first and 4,089 does not.
  const fits = `C:\\${'h'.repeat(4085)}`;
  const tooLong = `C:\\${'h'.repeat(4086)}`;

  assert.equal(launcherEnvironment(at.behind(), variables({ PATH: 'C:\\a', HOME: fits })).PATH, `${at.folders()}${fits}\\bin;C:\\a`);
  assert.equal(launcherEnvironment(at.behind(), variables({ PATH: 'C:\\a', HOME: tooLong })).PATH, `${at.folders()}C:\\a`);
  at.fx.cleanup();
});

// ——— The system's git (the tools' `spawnGit`).

test('the system’s git is the one PATH finds, past the launcher where it is one', () => {
  const found = onPath('git', { env: process.env, startable: true });
  const start = systemGit(process.env);

  assert.equal(start.file, found === null || !windows ? 'git' : gitBehind(found)?.git ?? 'git');
  if (start.file === 'git') assert.equal(start.env, process.env, 'a start that does not go past is handed its environment as it was');
});

test('a PATH that finds the launcher starts the git behind it; one that finds none starts git by its name', onWindows, () => {
  const at = install('gitforwindows-system-git');
  const launcher = at.lay();

  assert.equal(systemGit({ PATH: dirname(launcher), PATHEXT: '.EXE', HOME: 'C:\\h' }).file, at.behind().git);
  const none: Env = { PATH: join(at.root, 'nothing'), PATHEXT: '.EXE' };
  assert.deepEqual(systemGit(none), { file: 'git', env: none });
  at.fx.cleanup();
});

// ——— The toolchain's children (`startChild`): the system's git goes past, the person's named git does not.

test('the toolchain starts the system’s git past the launcher, and a git the person named as named', onWindows, () => {
  const at = install('gitforwindows-toolchain');
  // Both are copies of node, so the start can say which file it is and what MSYSTEM it was handed.
  const launcher = join(at.top, 'cmd', 'git.exe');
  mkdirSync(dirname(launcher), { recursive: true });
  copyFileSync(process.execPath, launcher);
  mkdirSync(join(at.top, 'mingw64', 'bin'), { recursive: true });
  copyFileSync(process.execPath, at.behind().git);
  mkdirSync(join(at.top, 'usr', 'bin'), { recursive: true });
  const script = join(at.root, 'says-which.mjs');
  writeFileSync(script, "console.log(`${process.execPath}|${process.env.MSYSTEM ?? ''}`);\n");
  const toolchain: Toolchain = { binary: [launcher, script], version: [], profileVariable: 'FAKE_HOME' };
  const home = join(at.root, 'data');
  mkdirSync(home, { recursive: true });

  const said = () => {
    const saved = process.env.DAORIS_HOME;
    process.env.DAORIS_HOME = home;
    try {
      const report = probe('fake', toolchain, at.root, readHarnessSettings(join(at.root, 'harnesses.json')));
      assert.ok(report.present, report.problem ?? '');
      return report.version!.split('|');
    } finally {
      if (saved === undefined) delete process.env.DAORIS_HOME;
      else process.env.DAORIS_HOME = saved;
    }
  };

  const [system, msystem] = said();
  assert.equal(system!.toLowerCase(), at.behind().git.toLowerCase(), 'the system’s git: the real one');
  assert.equal(msystem, 'MINGW64');

  writeFileSync(homeEntry(home, 'tools'), JSON.stringify({ tools: { git: { use: 'file', file: launcher } } }));
  const [named] = said();
  assert.equal(named!.toLowerCase(), launcher.toLowerCase(), 'the person’s named git: as named');
  at.fx.cleanup();
});

// ——— The twin, held: the driver's tables are these tables, row for row and in this order.

test('the driver’s tables are these tables, row for row and in this order', () => {
  const source = readFileSync(driverTestFile('GitForWindowsTests.cs'), 'utf8').replace(/\r\n/g, '\n');
  const rows = (method: string) => csharpRows(source, method, {}, 'GitForWindowsTests');

  assert.deepEqual(rows('The_launcher_in_a_cmd_folder_beside_its_prefix_and_usr_is_recognised'), PREFIX_ROWS);
  assert.deepEqual(rows('Any_other_layout_is_not_the_launcher'), LAYOUT_ROWS);
  assert.deepEqual(rows('The_launcher_puts_its_folders_and_HOMEs_bin_first_and_names_its_MSYSTEM_over_any'), MSYSTEM_ROWS);
});

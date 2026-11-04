import { join, posix, relative } from 'node:path';

/**
 * Where the install carries its doctrine tool (WSSETUP2, D124 §1.2), as path segments: the CLI package the
 * release publishes, unpacked the way npm lays a package out under a prefix, `<prefix>/node_modules/daoris`.
 *
 * 🔴 The `node_modules` folder is load-bearing. The CLI reads the canon it ships only when its own folder sits
 * under one (`resolveCanonRoot`, `src/Daoris.Cli/src/doctrine/canon.ts`); anywhere else it takes itself for a development
 * checkout and reads `../../canon`, which in an install is `<install>/canon`: a folder nothing publishes, or a
 * neighbour's under `--beside`. Laid out as npm would, the package finds its canon by the same rule wherever
 * it is installed, and that rule needs no second case. `desktop-publish.test.ts` holds the two together.
 */
export const CLI_HOME = Object.freeze(['app', 'cli']);
export const CLI_PACKAGE = Object.freeze([...CLI_HOME, 'node_modules', 'daoris']);

/** The package's entry inside it, as its `package.json`'s `bin` names it. */
export const CLI_ENTRY = Object.freeze(['bin', 'daoris.mjs']);

/**
 * The launchers' folder, beside the application, and `daoris`'s launchers in it (D124 §1.2; ONEWAY1ai): `daoris`, a
 * shell script, for Git Bash, and `daoris.cmd` for Command Prompt, which PowerShell finds too by `PATHEXT`. Nothing here
 * puts the folder on any `PATH`: the account's stays the person's (D124 §10).
 */
export const CLI_BIN = Object.freeze(['app', 'bin']);
export const CLI_LAUNCHERS = Object.freeze(['daoris', 'daoris.cmd']);

/**
 * The text of each `daoris` launcher, by name (ONEWAY1ai, the oneway design §11): the desktop's program, found from the
 * launcher's own folder, with the words whole; it needs no Node, and hands the doctrine commands to the CLI this install
 * carries itself. No machine path, so the files are the same in every install.
 *
 * The batch file is CRLF and both are ASCII: cmd.exe reads a batch file a line at a time in the console's code page. The
 * shell script turns its folder into a Windows path with `cygpath` where one exists, so Git Bash hands the program a path
 * it can open.
 */
export function cliLaunchers() {
  return launchersOf(CLI_LAUNCHERS, "Daoris's command, the desktop's program this install carries (ONEWAY1ai), run from its own folder.");
}

/**
 * The desktop's program (DRIVERDOOR1, ONEWAY1ai): the Host, published framework-dependent into a folder of its own under
 * `app/` that keeps its name for good, replaced whole by a republish (and carried by an update's swap of `app/`), and run
 * from `app/bin/` by `daoris`'s launchers, so the PATH every child Daoris starts has it by that bare name. It finds the
 * home by `DAORIS_HOME`, as the CLI does (D105).
 */
export const DRIVER_PROJECT = 'src/Daoris.Desktop/Daoris.Desktop.Driver.Host';
export const DRIVER_HOME = Object.freeze(['app', 'daoris-driver']);
export const DRIVER_EXE = 'daoris-driver.exe';

/** Each shell's launcher of the program, by the names given. */
function launchersOf(names, says) {
  const exe = posix.relative(CLI_BIN.join('/'), [...DRIVER_HOME, DRIVER_EXE].join('/'));
  const sh = [
    '#!/bin/sh',
    `# ${says}`,
    '# Written by the publish, and replaced by the next one.',
    'basedir=$(dirname "$(echo "$0" | sed -e \'s,\\\\,/,g\')")',
    'if command -v cygpath >/dev/null 2>&1; then basedir=$(cygpath -w "$basedir"); fi',
    `exec "$basedir/${exe}" "$@"`,
    '',
  ].join('\n');
  const cmd = [
    '@echo off',
    `rem ${says}`,
    'rem Written by the publish, and replaced by the next one.',
    `"%~dp0${exe.replace(/\//g, '\\')}" %*`,
    '',
  ].join('\r\n');
  return { [names[0]]: sh, [names[1]]: cmd };
}

import { join, posix, relative } from 'node:path';

/**
 * Where the install carries its doctrine tool, as path segments: the CLI package the
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
 * The command's folder, beside the application, and `daoris`'s program in it: `daoris.exe`, which Command Prompt, PowerShell and Git Bash
 * all find by `PATHEXT` or by name (SAYLINES1). Nothing here puts the folder on any `PATH`: the account's stays the person's.
 *
 * 🔴 A program, never a batch file: a batch file's `%*` ends at the first line break, so a verb's words of several lines
 * (`session say`, `ask`, `quest …`) reached the desktop's program as their first line and said nothing of the rest. `daoris.exe` is
 * `Daoris.Desktop.Command`: it starts the desktop's program, found from its own folder, with its arguments whole. It needs no Node, and
 * hands the doctrine commands to the CLI this install carries itself (the desktop's program does). It carries no machine path.
 */
export const CLI_BIN = Object.freeze(['app', 'bin']);
export const CLI_LAUNCHERS = Object.freeze(['daoris.exe']);

/**
 * The desktop's program: the Host, published framework-dependent into a folder of its own under
 * `app/` that keeps its name for good, replaced whole by a republish (and carried by an update's swap of `app/`), and run
 * from `app/bin/` by `daoris.exe`, so the PATH every child Daoris starts has it by that bare name. It finds the
 * home by `DAORIS_HOME`, as the CLI does.
 */
export const DRIVER_PROJECT = 'src/Daoris.Desktop/Daoris.Desktop.Driver.Host';
export const DRIVER_HOME = Object.freeze(['app', 'daoris-driver']);
export const DRIVER_EXE = 'daoris-driver.exe';

/**
 * Where the install carries its doctrine tool: one bundled file, `app/cli/daoris.mjs`, with its canon, its manifest (name and
 * version) and its build stamp beside it. The CLI reads the canon beside its own bundle (`resolveCanonRoot`) and the install's
 * programs run it by this path, never by a `PATH` lookup. `desktop-publish.test.ts` holds the twin in the driver (`CommandWords.CarriedCli`).
 */
export const CLI_HOME = Object.freeze(['app', 'cli']);
export const CLI_BUNDLE = Object.freeze([...CLI_HOME, 'daoris.mjs']);

/** The command's folder beside the application, and its program: never a batch file, whose arguments end at the first line break. */
export const CLI_BIN = Object.freeze(['app', 'bin']);
export const CLI_LAUNCHERS = Object.freeze(['daoris.exe']);

/** The desktop's program: the Host, published framework-dependent into a folder of its own under `app/`, run from `app/bin/` by `daoris.exe`. */
export const DRIVER_PROJECT = 'src/Daoris.Desktop/Daoris.Desktop.Driver.Host';
export const DRIVER_HOME = Object.freeze(['app', 'daoris-driver']);
export const DRIVER_EXE = 'daoris-driver.exe';

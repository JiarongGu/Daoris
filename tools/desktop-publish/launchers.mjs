/**
 * Where the install carries its doctrine tool: the CLI package the release publishes, laid out as npm does, `<prefix>/node_modules/daoris`.
 * 🔴 The `node_modules` folder is load-bearing: the CLI reads the canon it ships only when its own folder sits under one
 * (`resolveCanonRoot`); anywhere else it takes itself for a development checkout. `desktop-publish.test.ts` holds the two together.
 */
export const CLI_HOME = Object.freeze(['app', 'cli']);
export const CLI_PACKAGE = Object.freeze([...CLI_HOME, 'node_modules', 'daoris']);

/** The package's entry inside it, as its `package.json`'s `bin` names it. */
export const CLI_ENTRY = Object.freeze(['bin', 'daoris.mjs']);

/** The command's folder beside the application, and its program: never a batch file, whose arguments end at the first line break. */
export const CLI_BIN = Object.freeze(['app', 'bin']);
export const CLI_LAUNCHERS = Object.freeze(['daoris.exe']);

/** The desktop's program: the Host, published framework-dependent into a folder of its own under `app/`, run from `app/bin/` by `daoris.exe`. */
export const DRIVER_PROJECT = 'src/Daoris.Desktop/Daoris.Desktop.Driver.Host';
export const DRIVER_HOME = Object.freeze(['app', 'daoris-driver']);
export const DRIVER_EXE = 'daoris-driver.exe';

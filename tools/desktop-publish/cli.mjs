import { existsSync, mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { extractTarGz } from '../../src/Daoris.Cli/src/platform/tarball.ts';
import { renameHeld } from '../fsx.mjs';
import { CLI_BIN, CLI_ENTRY, CLI_HOME, CLI_PACKAGE, cliLaunchers } from './launchers.mjs';
import { SHELL_HOME } from './names.mjs';
import { unstage } from './staging.mjs';

/**
 * What is wrong with an unpacked package as the doctrine tool an install carries, or null when it is the
 * release's `daoris`: named `daoris`, its bin entry, its built dispatcher (without `dist/` the entry falls
 * back to sources a package does not ship), its canon at the package's own version (`release-prep` holds the
 * two together in the workspace, and this holds them on the artefact), and no TypeScript sources, which only
 * the source tree has: the release's `files` leave them out.
 */
function cliProblem(unpacked) {
  const manifestPath = join(unpacked, 'package.json');
  if (!existsSync(manifestPath)) return 'holds no package/package.json';
  let manifest;
  try {
    manifest = JSON.parse(readFileSync(manifestPath, 'utf8'));
  } catch (error) {
    return `holds a package/package.json that is not JSON (${error.message})`;
  }
  if (manifest?.name !== CLI_PACKAGE.at(-1)) return `is the package \`${manifest?.name}\`, not \`${CLI_PACKAGE.at(-1)}\``;
  for (const file of [CLI_ENTRY.join('/'), 'dist/cli.js', 'canon/canon.json']) {
    if (!existsSync(join(unpacked, ...file.split('/')))) return `carries no package/${file}`;
  }
  if (existsSync(join(unpacked, 'src'))) {
    return 'carries package/src/, the TypeScript sources: it is the source tree, not the package the release publishes';
  }
  let canon;
  try {
    canon = JSON.parse(readFileSync(join(unpacked, 'canon', 'canon.json'), 'utf8'));
  } catch (error) {
    return `holds a package/canon/canon.json that is not JSON (${error.message})`;
  }
  if (canon?.version !== manifest.version) {
    return `carries canon ${canon?.version} in package ${manifest.version}; a release's package and its canon are one version`;
  }
  return null;
}

/**
 * Lay the doctrine tool out in an install: the tarball `npm pack` made of
 * `src/Daoris.Cli`, unpacked by the CLI's own tar reader into `<install>/app/cli/node_modules/daoris/`, and
 * the launchers written into `<install>/app/bin/`. Both folders are staged beside and swapped in whole, so a
 * file an earlier publish wrote does not survive a republish, and a refused package leaves the last one
 * standing. Nothing is written under the home.
 * Fresh package and launcher folders use `renameHeld`'s bounded wait; unrelated errors still throw.
 *
 * @returns the package's version, which is the canon's.
 * @throws naming the tarball and what is wrong, when it is not the release's `daoris` as npm packs it (every
 *   entry under `package/`), before anything is replaced.
 */
export async function layCli(tarball, install, renameOptions = {}) {
  const app = join(install, SHELL_HOME[0]);
  mkdirSync(app, { recursive: true });
  const stagedCli = join(app, `.${CLI_HOME.at(-1)}-staging`);
  const stagedBin = join(app, `.${CLI_BIN.at(-1)}-staging`);
  const unstage = () => {
    rmSync(stagedCli, { recursive: true, force: true });
    rmSync(stagedBin, { recursive: true, force: true });
  };
  unstage();

  let version;
  try {
    // `npm pack` puts every entry under `package/`, so the archive unpacks into the folder that holds the
    // package and is then renamed to the package's name, as npm's own install does.
    const modules = join(stagedCli, ...CLI_PACKAGE.slice(CLI_HOME.length, -1));
    await extractTarGz(tarball, modules);
    const held = readdirSync(modules);
    if (held.length !== 1 || held[0] !== 'package') {
      throw new Error(`holds ${held.join(', ')} at its root, where npm's pack puts one folder, package/`);
    }
    const unpacked = join(modules, 'package');
    const problem = cliProblem(unpacked);
    if (problem) throw new Error(problem);
    version = JSON.parse(readFileSync(join(unpacked, 'package.json'), 'utf8')).version;
    renameHeld(unpacked, join(modules, CLI_PACKAGE.at(-1)), renameOptions);

    mkdirSync(stagedBin, { recursive: true });
    for (const [name, text] of Object.entries(cliLaunchers())) {
      writeFileSync(join(stagedBin, name), text, { mode: 0o755 });
    }
  } catch (error) {
    unstage();
    throw new Error(`desktop-publish: the doctrine tool's package ${tarball} ${error.message}`);
  }

  for (const [staged, segments] of [[stagedCli, CLI_HOME], [stagedBin, CLI_BIN]]) {
    const target = join(install, ...segments);
    rmSync(target, { recursive: true, force: true });
    renameHeld(staged, target, renameOptions);
  }
  return { version };
}

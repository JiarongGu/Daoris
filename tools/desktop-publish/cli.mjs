import { existsSync, mkdirSync, readFileSync, rmSync } from 'node:fs';
import { join } from 'node:path';
import { writeBuildStamp } from '../../src/Daoris.Cli/src/platform/build-stamp.ts';
import { copyTree, renameHeld } from '../fsx.mjs';
import { CLI_BIN, CLI_HOME } from './launchers.mjs';
import { SHELL_HOME } from './names.mjs';

/**
 * What is wrong with a folder as the doctrine tool an install carries, or null when it is what `bundleCli` makes: the bundle, a
 * manifest naming `daoris`, and the canon at the manifest's own version (`release-prep` holds the two together in the workspace,
 * and this holds them on the artefact). Nothing of an npm layout (`node_modules`, `dist`, `src`) belongs beside the bundle.
 */
function cliProblem(folder) {
  const manifestPath = join(folder, 'package.json');
  if (!existsSync(manifestPath)) return 'holds no package.json';
  let manifest;
  try {
    manifest = JSON.parse(readFileSync(manifestPath, 'utf8'));
  } catch (error) {
    return `holds a package.json that is not JSON (${error.message})`;
  }
  if (manifest?.name !== 'daoris') return `is the package \`${manifest?.name}\`, not \`daoris\``;
  for (const file of ['daoris.mjs', 'canon/canon.json']) {
    if (!existsSync(join(folder, ...file.split('/')))) return `carries no ${file}`;
  }
  for (const folderName of ['node_modules', 'dist', 'src']) {
    if (existsSync(join(folder, folderName))) return `carries ${folderName}/: the bundle stands alone beside its canon`;
  }
  let canon;
  try {
    canon = JSON.parse(readFileSync(join(folder, 'canon', 'canon.json'), 'utf8'));
  } catch (error) {
    return `holds a canon/canon.json that is not JSON (${error.message})`;
  }
  if (canon?.version !== manifest.version) {
    return `carries canon ${canon?.version} with package ${manifest.version}; a release's package and its canon are one version`;
  }
  return null;
}

/**
 * Lay the doctrine tool out in an install: the folder `bundleCli` made, copied to `<install>/app/cli/` with the build's stamp, and
 * `<install>/app/bin/` made empty for the programs laid out in it after (the command, the git doors, the partner's launcher:
 * `layBinPrograms`). Both folders are staged beside and swapped in whole, so a file an earlier publish wrote (an older layout's
 * `node_modules` among them) does not survive a republish, and a refused tool leaves the last one standing. Nothing is written
 * under the home. Fresh folders use `renameHeld`'s bounded wait; unrelated errors still throw. `build` is the stamp it carries.
 *
 * @returns the tool's version, which is the canon's.
 * @throws naming the folder and what is wrong, when it is not the bundle `bundleCli` makes, before anything is replaced.
 */
export async function layCli(source, install, renameOptions = {}, build = null) {
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
    const problem = cliProblem(source);
    if (problem) throw new Error(problem);
    version = JSON.parse(readFileSync(join(source, 'package.json'), 'utf8')).version;
    copyTree(source, stagedCli);
    if (build) writeBuildStamp(stagedCli, build);
    mkdirSync(stagedBin, { recursive: true });
  } catch (error) {
    unstage();
    throw new Error(`desktop-publish: the doctrine tool's folder ${source} ${error.message}`);
  }

  for (const [staged, segments] of [[stagedCli, CLI_HOME], [stagedBin, CLI_BIN]]) {
    const target = join(install, ...segments);
    rmSync(target, { recursive: true, force: true });
    renameHeld(staged, target, renameOptions);
  }
  return { version };
}

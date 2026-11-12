#!/usr/bin/env node
/**
 * Sets a builder's worktree up to build and test: the local props copied, the dependencies installed from the lockfiles,
 * and the build primed. A builder runs it once, first, from its worktree.
 *
 *   node tools/worktree-setup.mjs [--plan] [--build]
 *
 * - **Local props.** Each `local/*.props` of the main checkout (the sibling builds' feed paths, absolute and the main
 *   checkout's) and the sensitive-patterns file are copied into the worktree's `local/` as files, refreshed on every run, and
 *   never linked: a junction into the main checkout is followed by git and by an install, and emptied what it names. A
 *   `local/` or a `node_modules` that is a link is refused.
 * - **Dependencies.** Each folder of `DEPENDENCY_FOLDERS` whose `node_modules` is missing, or not from its lockfile as it stands,
 *   is installed with `npm ci`; one current by its stamp, or by npm's own record of the versions, is left alone.
 * - **The build primed.** The orientation index (written in each checkout), the contracts' generated types, and the restore of
 *   the desktop's and the service's test projects, so a builder's first build and test run starts from a warm tree. `--build`
 *   also builds the driver's and the modules' test projects.
 *
 * `--plan` says what would run and runs nothing. Exit codes: 0 done · 1 a step failed or a refusal · 2 a tool error.
 */
import { spawnSync } from 'node:child_process';
import { existsSync, lstatSync, mkdirSync, readdirSync, readFileSync, realpathSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { isMain } from './fsx.mjs';
import { dependencyState, DEPENDENCY_FOLDERS, stampDependencies } from './merge-branch/dependencies.mjs';

export { dependencyState };
import { copyPrivateFiles, privateGateFiles } from './private-gate-files.mjs';

/** The test projects the restore and `--build` take. */
const TEST_PROJECTS = [
  'src/Daoris.Desktop/Daoris.Desktop.Driver.Tests', 'src/Daoris.Desktop/Daoris.Desktop.Modules.Tests',
  'src/Daoris.Service/Daoris.Service.Tests', 'src/Daoris.Service/Daoris.Service.Http.Tests',
];

/** The files a builder is handed from the main checkout: every `local/*.props`, and what a gate's private files are. */
export function localFiles(home) {
  const props = existsSync(join(home, 'local')) ? readdirSync(join(home, 'local')).filter((name) => name.endsWith('.props')).map((name) => `local/${name}`) : [];
  return [...new Set([...props, ...privateGateFiles(home)])].sort();
}

/** Copies `localFiles` into the tree, as files; a `local/` that is a link there is refused, since a copy would write through it. */
export function copyLocal(home, tree) {
  const folder = join(tree, 'local');
  if (existsSync(folder) && lstatSync(folder).isSymbolicLink()) {
    throw new Error(`local is a link in ${tree}: remove the link (as a link, never its contents) and run the setup again`);
  }
  mkdirSync(folder, { recursive: true });
  return copyPrivateFiles(home, tree, localFiles(home)).map((line) => line.replace('into the gate tree', 'into the worktree').replace('so it is not copied into the gate tree: the gate that reads it answers for itself', 'so it is not copied'));
}

/** The commands the setup runs, from each dependency folder's state: its installs, then the priming. A link is refused. */
export function setupCommands(states, { build = false } = {}) {
  const commands = [];
  for (const folder of DEPENDENCY_FOLDERS) {
    const state = states[folder];
    if (state === 'linked') throw new Error(`${folder === '.' ? '' : `${folder}/`}node_modules is a link: remove the link (as a link, never its contents) and run the setup again`);
    if (state === 'missing' || state === 'stale') commands.push({ label: `install ${folder}`, command: 'npm ci --no-audit --no-fund', cwd: folder });
  }
  commands.push(
    { label: 'drivers', command: 'node tools/merge-rows.mjs --install && node tools/merge-backlog.mjs --install && node tools/index-hook.mjs --install', cwd: '.' },
    { label: 'index', command: 'node tools/orient-index.mjs', cwd: '.' },
    { label: 'contracts', command: 'node tools/contracts.mjs --if-stale', cwd: '.' },
    { label: 'restore', command: TEST_PROJECTS.map((project) => `dotnet restore ${project}`).join(' && '), cwd: '.' },
  );
  if (build) commands.push(...TEST_PROJECTS.slice(0, 2).map((project) => ({ label: 'build', command: `dotnet build ${project} -nodeReuse:false`, cwd: '.' })));
  return commands;
}

/** The main checkout is no builder's worktree: setting it up would copy over its own files. */
export function refuseHome(home, tree) {
  if (resolve(home) === resolve(tree)) throw new Error('this is the main checkout: the setup is for a builder\'s worktree, run from inside it');
}

/** The main checkout of the repository a tree belongs to: the folder of the git folder every worktree shares. */
export function homeOf(tree) {
  const common = spawnSync('git', ['rev-parse', '--path-format=absolute', '--git-common-dir'], { cwd: tree, encoding: 'utf8', windowsHide: true });
  if (common.status !== 0) throw new Error(`git could not name the main checkout: ${String(common.stderr).trim()}`);
  return realpathSync(dirname(common.stdout.trim()));
}

async function main(argv) {
  const tree = realpathSync(process.cwd());
  const home = homeOf(tree);
  refuseHome(home, tree);
  const states = Object.fromEntries(DEPENDENCY_FOLDERS.map((folder) => [folder, dependencyState(tree, folder)]));
  const commands = setupCommands(states, { build: argv.includes('--build') });
  if (argv.includes('--plan')) {
    for (const file of localFiles(home)) console.log(`copy ${file}`);
    for (const each of commands) console.log(`${each.label}: ${each.command} (in ${each.cwd})`);
    return 0;
  }
  for (const line of copyLocal(home, tree)) console.log(line);
  for (const each of commands) {
    console.log(`${each.label}: ${each.command}`);
    const ran = spawnSync(each.command, { cwd: join(tree, each.cwd), shell: true, stdio: 'inherit', windowsHide: true });
    if (ran.error || ran.status !== 0) {
      console.error(`worktree-setup: ${each.label} failed (${ran.error?.message ?? `exit ${ran.status}`})`);
      return 1;
    }
    if (each.label.startsWith('install ')) stampDependencies(tree, each.cwd);
  }
  console.log('worktree-setup: done');
  return 0;
}

if (isMain(import.meta.url)) {
  main(process.argv.slice(2)).then(
    (code) => { process.exitCode = code; },
    (error) => {
      console.error(`worktree-setup: ${error.message}`);
      process.exitCode = 1;
    },
  );
}

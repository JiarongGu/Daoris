#!/usr/bin/env node
/**
 * Publish the desktop shell as an installed application — the thing a person runs, not a checkout
 * with a build step. The sibling of `service-publish.mjs`, and it exists for the same reason: a
 * workspace build is not a deployment, and the difference only shows up once you try.
 *
 *   node tools/desktop-publish.mjs --to <folder>            publish the shell into <folder>
 *   node tools/desktop-publish.mjs --to <folder> --service  …and both service hosts beside it: the HTTP host,
 *                                                           with its bundle, and the knowledge connector
 *   node tools/desktop-publish.mjs --to <folder> --beside   …into a folder that holds other things —
 *                                                           the repositories it drives, typically
 *   node tools/desktop-publish.mjs --to <install> --service --stage
 *                                                           …beside a running install, in update/staged/,
 *                                                           which the desktop installs when idle: it drains
 *                                                           (starts nothing new, holds the presses that would start
 *                                                           work) until every driven session ends;
 *                                                           `daoris update --cancel` stages it *not now*
 *   … --force-ungated                                       …a tree the full set of gates has not passed
 *                                                           (see below): the person's call
 *   node tools/desktop-publish.mjs --to <install> --service --stage --from-gated <commit>
 *                                                           …the commit the full set gated in its own worktree
 *                                                           (see below), built there
 *   … --artifacts-path <folder>                             …every build of the publish into <folder>, none into a
 *                                                           project's own bin/ and obj/: the deployment
 *                                                           rehearsal's, which runs beside the driver's git half
 *
 * 🔴 **`--to` is required and has no default.** A machine path in a tracked file is exactly what
 * `sensitive-info` forbids, and a default would be one — the same rule `testbed.mjs` follows, for the
 * same reason.
 *
 * 🔴 **It publishes only a tree the full set of gates passed**. A merge runs only the gates its
 * lanes reach (`tools/merge-branch.mjs`), so this is where the rest is owed: before anything is built it
 * asks `merge-branch --passed`, and refuses, naming each gate that has not passed and the smallest command
 * that would pass it: `--stale`, `--rerun <gate>…`, or `--full`, which runs them all.
 * `--force-ungated` is the person's explicit override, and the publish says it is ungated. A folder under
 * the workspace's `_fixtures` is never asked: the deployment rehearsal publishes there, and it is one of the
 * gates being run.
 *
 * **A stage from the gate tree.** `merge-branch --ci` (after each merge) and `--full --beside` gate a commit
 * in a worktree of their own, `local/gate-tree` under the main checkout, while merges move main on, and record their
 * verdicts in the main checkout's store, keyed by tree. `--stage --from-gated <commit>` builds that tree, never this
 * checkout: it refuses when no gate tree stands, when it holds another commit, or while a run holds it; holds the tree's
 * lock itself through its build, so no CI run moves it meanwhile; asks the gates of the gate tree's own tree, here
 * and again in the gate tree's own publish, which it runs there with the same arguments, a refusal naming
 * `--ci --stage <commit>` (the CI's run that pays the rehearsals a plain run owes); and once that stages, notes it in the tree's record (`local/gate-tree.json`), so the next merge's prune removes the tree.
 *
 * **Framework-dependent on purpose.** The shell requires a Windows desktop runtime; a self-contained
 * publish would add ~150 MB to carry a .NET that this machine has. It carries its own Chromium,
 * so it needs no WebView2. The service hosts are the opposite case and are
 * self-contained — they are what a *server* runs, possibly without .NET at all.
 *
 * **The layout is a regular application's**: `Daoris.exe` at the root is a small launcher and
 * the one thing to run; the application is `app/Daoris.Desktop.exe`, CEF's launcher, beside its
 * Chromium and its libraries, with the HTTP host and the knowledge connector each in a folder of its own
 * under `app/`; `data/` is the
 * home. Daoris's browser is the application itself, started with the browser's
 * argument, so the install carries one Chromium. The names the shell's publish puts in `app/` are
 * listed in `app/shell-files.txt`, so the next publish removes exactly those and nothing else. Daoris's
 * own example plugins sit beside them in `app/plugin-offers/`, offered and never installed, and so
 * does the list of where each tool's versions download from, `app/resources.json`. And Daoris's
 * doctrine tool: the CLI packed as the release packs it, unpacked under `app/cli/`,
 * with a launcher for each shell in `app/bin/`, beside Daoris's git doors.
 *
 * **What a deployed shell finds.** Nothing is wired into it: with no `DAORIS_*` overrides it makes
 * the install's `data/` the Daoris home — the machine's registry, quests, drivable set and profiles
 * live there, and nothing under the user profile — and locates the HTTP host through
 * `ServiceHostLocator`, and the connector it hands each protocol-door session through
 * `KnowledgeConnector`, both of which look beside the shell before the home's `bin/`. `--service`
 * publishes both there, by the service publish's one recipe, so the folder is
 * self-sufficient and a republish refreshes both with the shell. A republish into an install that carries
 * either, without `--service`, is refused: it would leave an earlier build's hosts ranked first.
 */
import { execSync, spawnSync } from 'node:child_process';
import { cpSync, existsSync, mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, isAbsolute, join, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain } from './fsx.mjs';
import { running } from './processes.mjs';
import { publishHost } from './service-publish.mjs';
import { STAGE, STAGED, buildId, promoteStage, recordedShellFiles, refusal, stageRefusal, unstage, writeManifest } from './desktop-publish/staging.mjs';
import { appPublishCommand, artifactsOf, commandPublishCommand, driverPublishCommand, gitDoorPublishCommand, launcherPublishCommand, layCommand, layGitDoors, layPieceStart, pieceStartPublishCommand, publishStages } from './desktop-publish/publish-commands.mjs';
import { GATE_TREE, childArgs, gatedSource, gitIn, mainCheckout, recordStage, recordStaged, samePath, stageReport, stagedAdvice } from './desktop-publish/gate-tree.mjs';
import { insideFixtures, ungatedRefusal } from './desktop-publish/gates.mjs';
import { sessionRefusal } from './desktop-publish/session.mjs';
import { HOST_HOME, INSTALL_RID, LAUNCHER, MARKER, RETIRED_BROWSER_EXE, RETIRED_IN_APP, RETIRED_LAUNCHERS, SHELL_EXE, SHELL_FILES, SHELL_HOME, retiredPaths, servicePlan } from './desktop-publish/names.mjs';
import { HOME, KEPT_LOCALES, PLUGIN_OFFERS, RESOURCES, RESOURCES_SOURCE, layOffers, layResources } from './desktop-publish/lay.mjs';
import { CLI_BIN, CLI_HOME, CLI_LAUNCHERS, DRIVER_EXE, DRIVER_HOME } from './desktop-publish/launchers.mjs';
import { layCli } from './desktop-publish/cli.mjs';
import { installedNote } from './desktop-publish/install-note.mjs';
export { MARKER, MARKER_HEADER, LAUNCHER, SHELL_HOME, SHELL_EXE, RETIRED_LAUNCHERS, RETIRED_IN_APP, RETIRED_BROWSER_EXE, retiredPaths, SHELL_FILES, HOST_HOME, HOST_EXE, CONNECTOR_HOME, CONNECTOR_EXE, servicePlan, carriedHosts, isInstall } from './desktop-publish/names.mjs';
export { KEPT_LOCALES, HOME, PLUGIN_OFFERS, OFFERED_PLUGINS, layOffers, RESOURCES, RESOURCES_SOURCE, layResources } from './desktop-publish/lay.mjs';
export { CLI_HOME, CLI_PACKAGE, CLI_ENTRY, CLI_BIN, CLI_LAUNCHERS, DRIVER_PROJECT, DRIVER_HOME, DRIVER_EXE } from './desktop-publish/launchers.mjs';
export { driverPublishCommand, LAUNCHER_PROJECT, APP_PROJECT, launcherPublishCommand, appPublishCommand, publishStages, artifactsOf, GIT_DOOR_PROJECT, GIT_DOORS, gitDoorPublishCommand, layGitDoors, PIECE_START_PROJECT, PIECE_START, pieceStartPublishCommand, layPieceStart, COMMAND_PROJECT, COMMAND, commandPublishCommand, layCommand } from './desktop-publish/publish-commands.mjs';
export { layCli } from './desktop-publish/cli.mjs';
export { STAGE, STAGED, BUILD_MANIFEST, SWAP_JOURNAL, MANIFEST_SCHEMA, STAGED_REQUIRED, OWN, buildId, stagedManifest, writeManifest, stageRefusal, promoteStage, unstage, recordedShellFiles, refusal } from './desktop-publish/staging.mjs';
export { installedNote } from './desktop-publish/install-note.mjs';
export { GATE_COMMAND, insideFixtures, askGates, ungatedRefusal } from './desktop-publish/gates.mjs';
export { SESSION_VARIABLE, sessionRefusal } from './desktop-publish/session.mjs';
export { GATE_TREE, GATE_TREE_LOCK, GATE_TREE_RECORD, gatedSource, childArgs, recordStaged, stageReport, stagedAdvice, recordStage } from './desktop-publish/gate-tree.mjs';

// ---------------------------------------------------------------------------------------------
// Everything above the divider is the guard, exported so `src/Daoris.Cli/test/desktop-publish.test.ts`
// can hold every branch of it without a build; everything below it publishes.

async function main() {
  const repoRoot = dirname(dirname(fileURLToPath(import.meta.url)));
  const argv = process.argv.slice(2);
  const flag = (name) => argv.includes(name);
  const value = (name) => {
    const at = argv.indexOf(name);
    return at !== -1 && argv[at + 1] && !argv[at + 1].startsWith('--') ? argv[at + 1] : null;
  };

  const toArg = value('--to');
  if (!toArg) {
    console.error('desktop-publish: --to <folder> is required.\n'
      + '  It has no default on purpose: a machine path does not belong in a tracked file.');
    process.exit(2);
  }
  const to = isAbsolute(toArg) ? toArg : resolve(process.cwd(), toArg);
  const sessions = sessionRefusal(to, repoRoot);
  if (sessions) {
    console.error(sessions);
    process.exit(1);
  }
  // Where every build of this publish goes, when told; each project's own folders otherwise.
  const { artifacts, refusal: artifactsRefused } = artifactsOf(argv);
  if (artifactsRefused) {
    console.error(artifactsRefused);
    process.exit(2);
  }

  // A stage of the commit the full set gated in its own worktree is built there, by that tree's own publish,
  // never from this checkout, which merges may have moved on since. Its gates are asked here first, and there again.
  if (flag('--from-gated')) {
    const commit = value('--from-gated');
    if (!commit || !flag('--stage')) {
      console.error('desktop-publish: --from-gated <commit> stages the commit the gate tree holds: give it with --stage and the commit.');
      process.exit(2);
    }
    // The stage holds the gate tree's lock through its build, so a CI run (a watch starts one as main moves) waits for it.
    const source = gatedSource(repoRoot, commit, { hold: true });
    if (source.refusal) {
      console.error(source.refusal);
      process.exit(2);
    }
    const sha = source.commit.slice(0, 7);
    let built;
    try {
      const asked = ungatedRefusal(to, source.tree, {
        force: flag('--force-ungated'),
        light: flag('--light'),
        remedy: { what: "what this commit's verdicts lack", where: 'in the gate tree', command: `node tools/merge-branch.mjs --ci${flag('--light') ? '' : ' --stage'} ${sha}` },
      });
      if (asked.refusal) {
        console.error(asked.refusal);
        process.exitCode = 2;
        return;
      }
      for (const line of stageReport(source.home, source.commit)) console.log(`desktop-publish: ${line}`);
      console.log(`desktop-publish: building ${sha} from the gate tree (${GATE_TREE}), not from this checkout; it holds the tree until its build ends.`);
      built = spawnSync(process.execPath, [join(source.tree, 'tools', 'desktop-publish.mjs'), ...childArgs(argv, to)], { cwd: source.tree, stdio: 'inherit', windowsHide: true });
    } finally {
      source.release();
    }
    if (built.status === 0) {
      const at = new Date().toISOString().replace(/\.\d+Z$/, 'Z');
      recordStaged(source.home, { commit: source.commit, to, at });
      // The stage's history, apart from the gate tree's record, which every run rewrites.
      recordStage(source.home, { commit: source.commit, to, at, forced: flag('--force-ungated') });
      console.log('desktop-publish: the gate tree\'s stage is done; the next merge\'s prune removes the tree, or node tools/merge-branch.mjs --prune now.');
    }
    process.exit(built.status ?? 2);
  }

  const WEB = 'src/Daoris.Web';

  const run = (command) => execSync(command, { cwd: repoRoot, stdio: ['ignore', 'inherit', 'inherit'], windowsHide: true });

  // `--stage`: the same build, written beside the install into `update/staged/` instead of over it, for
  // the desktop to install when its work allows. Everything below writes under `root`, which is the install or the staging.
  const stage = flag('--stage');
  const refused = stage
    ? stageRefusal(to, { service: flag('--service') })
    : refusal(to, { beside: flag('--beside'), service: flag('--service') });
  if (refused) {
    console.error(refused);
    process.exit(2);
  }
  // Only a tree the full set passed is built into an install, before anything is built.
  const gated = ungatedRefusal(to, repoRoot, { force: flag('--force-ungated'), light: flag('--light') });
  if (gated.refusal) {
    console.error(gated.refusal);
    process.exit(2);
  }
  if (gated.note) console.log(gated.note);
  const root = stage ? join(to, ...STAGE, '.staging') : to;
  if (stage) {
    rmSync(root, { recursive: true, force: true });
    mkdirSync(root, { recursive: true });
  }

  /**
   * 🔴 A running install holds its own executable open, and a re-publish is exactly when it is running.
   *
   * Without this the failure is an MSBuild stack ending in
   * `System.UnauthorizedAccessException: Access to the path '…daoris-desktop.exe' is denied` — eleven
   * lines of `Microsoft.NET.HostModel.Bundle` internals for "close the app". Checked before anything
   * is built, so the answer arrives in a second rather than after the whole web bundle.
   */
  // The application and the single-file shell an older install may still have: the launcher at
  // the root exits once it has started the application, so it is the application that holds files.
  const shells = [join(to, ...SHELL_HOME, SHELL_EXE), ...RETIRED_LAUNCHERS.map((name) => join(to, name))]
    .filter(existsSync);
  // A stage writes nothing the running application holds, so it is never refused for one running.
  if (!stage && process.platform === 'win32' && shells.length > 0) {
    // Everything from the application's executable, its browser and its engine's processes
    // included; and the browser an older install runs from its own folder, which
    // this publish removes. The browser follows the shell out, so a running one is a shell still
    // running, or just gone.
    const held = [
      ...shells.flatMap((shell) => running(shell)),
      ...RETIRED_IN_APP.flatMap((name) => running(join(to, ...SHELL_HOME, name, RETIRED_BROWSER_EXE))),
    ];
    if (held.length > 0) {
      console.error(`desktop-publish: the install at \`${to}\` is running (pid ${held.join(', ')}).`);
      console.error('  Close it and re-run — a running application holds its own executable open, and');
      console.error('  publishing over it fails halfway through, leaving the folder part-written. Or stage the');
      console.error('  build beside it with --stage, which the desktop installs once its work allows (D139).');
      process.exit(2);
    }
  }

  // The dependency order, and it is not cosmetic: the platform builds INTO the host's wwwroot, so a
  // host published before the bundle carries the previous one — and the window shows it.
  console.log('desktop-publish: building the platform bundle…');
  run(`npm --prefix ${WEB} run build`);

  // 🔴 ONE executable at the root, and nothing else that looks like one.
  //
  // Published the default way, the root holds 24 entries, with the executable buried
  // alphabetically among DLLs, `.pdb`s and stray `.xml` doc files; here it holds one
  // small launcher, framework-dependent and single-file, wearing the application's icon, and
  // everything it starts sits under `app/`.
  const stages = publishStages(repoRoot, artifacts);

  // The command, beside them: `daoris.exe`.
  console.log(`desktop-publish: publishing the command into ${CLI_BIN.join('/')}/…`);
  const commandStage = join(stages, 'command');
  run(commandPublishCommand(commandStage, artifacts));
  try {
    layCommand(commandStage, root);
  } catch (error) {
    console.error(error.message);
    process.exit(1);
  }
  rmSync(stages, { recursive: true, force: true });

  // The doctrine tool: `src/Daoris.Cli` packed as the release packs it, `npm pack` in
  // its own folder, whose `prepack` builds `dist/` and stages the canon, the licence and the readme. The
  // tarball is the artefact the release rehearsal installs and the release workflow publishes, never the
  // source tree. Packed before anything is published, so a package that will not pack stops the publish
  // while the install is still untouched.
  console.log('desktop-publish: packing the doctrine tool…');
  const cliStage = join(stages, 'cli');
  mkdirSync(cliStage, { recursive: true });
  // `postpack` removes what the pack staged, and this removes it again whatever happened, as the release
  // rehearsal does: a `dist/` outliving a pack shadows the sources for every later bin-driven run (FIX-LOG
  // 2026-09-20).
  const unstagePackage = () => execSync(`node "${join(repoRoot, 'tools', 'stage-package.mjs')}" --clean`, { stdio: 'ignore', windowsHide: true });
  let cliTarball;
  try {
    // The tarball's name is the last line npm prints; the build `prepack` runs prints above it.
    const packed = execSync(`npm pack --pack-destination "${cliStage}"`, {
      cwd: join(repoRoot, 'src', 'Daoris.Cli'), encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'], windowsHide: true,
    }).trim().split('\n').pop().trim();
    cliTarball = join(cliStage, packed);
  } catch (error) {
    unstagePackage();
    console.error(`${error.stdout ?? ''}${error.stderr ?? ''}`);
    console.error('desktop-publish: `npm pack` of src/Daoris.Cli failed, so the install would carry no doctrine tool.');
    process.exit(1);
  }
  unstagePackage();
  if (!existsSync(cliTarball)) {
    console.error(`desktop-publish: \`npm pack\` named ${cliTarball}, and there is no such file.`);
    process.exit(1);
  }

  console.log('desktop-publish: publishing the launcher…');
  const launcherStage = join(stages, 'launcher');
  run(launcherPublishCommand(launcherStage, artifacts));

  // The application on its own Chromium: CEF's launcher beside the engine and the app's
  // libraries, and Daoris's browser too, which is the same executable started with
  // the browser's argument — so there is one engine's `locales/` to keep.
  // `AllowedReferenceRelatedFileExtensions=none` is what stops the package doc files; they come from
  // the WebView2 package rather than from this compile, so `GenerateDocumentationFile` does not reach
  // them. `DebugType=none` drops the symbols an installed app has no use for.
  console.log('desktop-publish: publishing the application…');
  const shellStage = join(stages, 'shell');
  run(appPublishCommand(shellStage, artifacts));
  // The kit lays out only the languages the app project names, each with three 18-byte
  // grammatical-gender stubs beside it (`zh-CN_FEMININE.pak`, …); the install keeps the two plain
  // files, and refuses to ship without them.
  const locales = join(shellStage, 'locales');
  if (!KEPT_LOCALES.every((file) => existsSync(join(locales, file)))) {
    console.error(`desktop-publish: the application carries no ${KEPT_LOCALES.join(' or ')} — `
      + 'the engine would start with no language it is asked for.');
    process.exit(1);
  }
  for (const file of readdirSync(locales)) {
    if (!KEPT_LOCALES.includes(file)) rmSync(join(locales, file));
  }
  // The list built in is laid out below, by its one writer. Whatever the build's publish carried
  // of it leaves the stage, so `shell-files.txt` never records it as the application's.
  rmSync(join(shellStage, RESOURCES.at(-1)), { force: true });

  // What the last publish put in `app/` goes first, so an engine upgrade leaves none of its files
  // behind — only the recorded names, never the host beside them. Then what an earlier publish wrote
  // and this one does not, by name: the single-file shell's launcher at the root, which
  // the launcher replaces, and the browser's own folder with its second engine.
  const app = join(root, ...SHELL_HOME);
  const staged = readdirSync(shellStage);
  // The doctrine tool's two folders are laid out whole below, so an application file of either name would
  // be replaced by them, and lost without a word.
  const clash = staged.filter((name) => name === CLI_HOME.at(-1) || name === CLI_BIN.at(-1) || name === DRIVER_HOME.at(-1));
  if (clash.length > 0) {
    console.error(`desktop-publish: the application's publish carries ${clash.join(' and ')}, where the install `
      + 'keeps the doctrine tool (WSSETUP2).');
    process.exit(1);
  }
  // A staging folder starts empty and is no install, so neither line below removes anything there.
  for (const name of recordedShellFiles(root)) rmSync(join(app, name), { recursive: true, force: true });
  for (const path of retiredPaths(root)) rmSync(path, { recursive: true, force: true });
  mkdirSync(app, { recursive: true });
  for (const name of staged) cpSync(join(shellStage, name), join(app, name), { recursive: true });
  writeFileSync(join(root, ...SHELL_FILES), `${staged.sort().join('\n')}\n`);
  cpSync(join(launcherStage, LAUNCHER), join(root, LAUNCHER));

  // The doctrine tool, beside the application: the package under `app/cli/`, as npm
  // lays one out, and the folder `app/bin/` for the programs laid out in it below. Nothing goes on any PATH from here.
  const build = { id: buildId(), commit: commitOf(repoRoot) };
  try {
    const tool = await layCli(cliTarball, root, {}, build);
    console.log(`desktop-publish: the doctrine tool is daoris ${tool.version} in ${CLI_HOME.join('/')}/, `
      + `run by ${CLI_LAUNCHERS.map((name) => `${CLI_BIN.join('/')}/${name}`).join(' or ')}, laid out below.`);
  } catch (error) {
    console.error(error.message);
    process.exit(1);
  }

  // The driver's verbs: the Host in a folder of its own, REPLACED whole as the service hosts are.
  console.log(`desktop-publish: publishing ${DRIVER_HOME.at(-1)} under ${DRIVER_HOME.join('/')}/…`);
  const driverFolder = join(root, ...DRIVER_HOME);
  rmSync(driverFolder, { recursive: true, force: true });
  run(`${driverPublishCommand(driverFolder, INSTALL_RID, artifacts)} --nologo`);
  if (!existsSync(join(driverFolder, DRIVER_EXE))) {
    console.error(`desktop-publish: the publish of ${DRIVER_HOME.at(-1)} made no ${DRIVER_EXE}.`);
    process.exit(1);
  }

  // The git doors, in `app/bin/` beside the launchers, laid after `layCli` replaces the folder: every
  // process Daoris starts is routed through them from the next start (`GitWiring.Hand`), and an install with all three has moved
  // (`GitMove.Moved`). Every publish lays all three.
  console.log(`desktop-publish: publishing the git doors into ${CLI_BIN.join('/')}/…`);
  const doorStage = join(stages, 'git-doors');
  run(gitDoorPublishCommand(doorStage, artifacts));
  try {
    layGitDoors(doorStage, root);
  } catch (error) {
    console.error(error.message);
    process.exit(1);
  }

  // The partner's launcher, beside the doors: a partner's start goes through it alone.
  console.log(`desktop-publish: publishing the partner's launcher into ${CLI_BIN.join('/')}/…`);
  const pieceStartStage = join(stages, 'piece-start');
  run(pieceStartPublishCommand(pieceStartStage, artifacts));
  try {
    layPieceStart(pieceStartStage, root);
  } catch (error) {
    console.error(error.message);
    process.exit(1);
  }
  rmSync(stages, { recursive: true, force: true });

  // Daoris's own example plugins, as offers beside the application: the Plugins place
  // lists them and installs one only when pressed, so the publish writes nothing under the home.
  const offered = layOffers(join(repoRoot, 'examples', 'plugins'), root);
  console.log(`desktop-publish: offering ${offered.join(', ')} in ${PLUGIN_OFFERS.join('/')}/ (none installed).`);

  // The list of where each tool's versions download from, beside the application.
  // The driver's build carries it too, and the stage is cleared of it above: this is the one writer.
  layResources(join(repoRoot, ...RESOURCES_SOURCE), root);
  console.log(`desktop-publish: the list built in is ${RESOURCES.join('/')}.`);

  if (flag('--service')) {
    // Supporting binaries go under `app/`, which is the shape the neighbouring applications on this
    // machine use: one launcher at the root, everything it needs out of sight, runtime state in `data/`.
    // Both hosts: the HTTP host the shell starts, and the knowledge connector its sessions are
    // handed, each by the service publish's one recipe into the folder that script gives it.
    for (const step of servicePlan(root)) {
      console.log(`desktop-publish: publishing ${step.host.binary} under ${relative(root, step.folder).split(sep).join('/')}/…`);

      // 🔴 REPLACED, not published over. `dotnet publish` does not clear its output, so a re-publish
      // leaves every previous hashed bundle in `wwwroot/assets` — and `index.html` names only the
      // current one, which makes the folder correct and unreadable: a real install reached SEVEN bundles,
      // and listing it gave a wrong answer about which build is live. `service-publish.mjs` does this too.
      rmSync(step.folder, { recursive: true, force: true });

      if (!publishHost(repoRoot, step.host, INSTALL_RID, step.folder, 'desktop-publish', artifacts)) process.exit(1);
      if (!existsSync(step.executable)) {
        console.error(`desktop-publish: the publish of ${step.host.binary} made no ${step.executable}.`);
        process.exit(1);
      }
    }

    // The bundle travels BESIDE the executable — a host installed without its page answers every API
    // call and serves 404 for the UI, which reads as a broken app rather than a missing file.
    const bundle = join(root, ...HOST_HOME, 'wwwroot');
    if (!existsSync(bundle)) {
      console.error('desktop-publish: the HTTP publish carries no wwwroot — the shell would serve no page.');
      process.exit(1);
    }
  }

  mkdirSync(root, { recursive: true });
  writeFileSync(join(root, MARKER), installedNote());

  if (stage) {
    // The build's manifest, which the application and the launcher check before anything is replaced, then
    // the folder swapped into `update/staged/` whole, so the desktop never meets half a stage.
    const identity = {
      id: build.id,
      version: JSON.parse(readFileSync(join(repoRoot, 'canon', 'canon.json'), 'utf8')).version,
      commit: build.commit,
      at: new Date().toISOString().replace(/\.\d+Z$/, 'Z'),
    };
    writeManifest(root, identity);
    promoteStage(root, to);
    // A stage built here is recorded here; the gate tree's child is recorded by its parent, and a rehearsal's
    // stage under `_fixtures` is no stage of the owner's install.
    const home = mainCheckout(repoRoot);
    const child = samePath(repoRoot, join(home, ...GATE_TREE.split('/')));
    if (!child && !insideFixtures(to, repoRoot)) {
      const commit = gitIn(repoRoot, ['rev-parse', 'HEAD']).stdout?.trim();
      if (commit) recordStage(home, { commit, to, at: identity.at, forced: flag('--force-ungated') });
    }
    console.log(`\ndesktop-publish: staged build ${identity.id} (${identity.version}${identity.commit ? `, ${identity.commit}` : ''}) `
      + `beside ${to}, in ${STAGED.join('/')}/`);
    for (const line of stagedAdvice()) console.log(line);
    return;
  }

  // A publish in place supersedes anything staged: left, it would replace this build at the next start.
  unstage(to);
  console.log(`\ndesktop-publish: installed to ${to}`);
  console.log(`  Its home is ${join(to, HOME)} (D63) — starting it starts the driver loop.`);
}

/** The commit this workspace is at, short, or null where git cannot say: what tells two builds at one version apart. */
function commitOf(repoRoot) {
  try {
    return execSync('git rev-parse --short HEAD', { cwd: repoRoot, encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'], windowsHide: true }).trim() || null;
  } catch {
    return null;
  }
}

// Guarded, because the guard above is imported by a unit test — and `node --test` importing this
// file must not publish anything. `desktop.mjs` and `deployment-rehearsal.mjs` guard the same way.
if (isMain(import.meta.url)) await main();

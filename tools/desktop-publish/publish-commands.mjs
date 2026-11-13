import { cpSync, existsSync, mkdirSync, readdirSync } from 'node:fs';
import { isAbsolute, join, resolve } from 'node:path';
import { builtInto, STAMPED } from '../service-publish.mjs';
import { INSTALL_RID } from './names.mjs';
import { refusal } from './staging.mjs';
import { CLI_BIN, CLI_LAUNCHERS, DRIVER_PROJECT } from './launchers.mjs';

/**
 * The publish of the Host into `folder`: a runtime-dependent folder, the shape the application and the launcher have. Its
 * builds go into `artifacts` when one is given ({@link artifactsOf}).
 */
export function driverPublishCommand(folder, rid = INSTALL_RID, artifacts = null) {
  return builtInto(`dotnet publish "${DRIVER_PROJECT}" -c Release -r ${rid} --self-contained false -p:DebugType=none ${STAMPED} -o "${folder}"`, artifacts);
}

/** The launcher at an install's root and the application under `app/`, the two projects the shell's publish makes. */
export const LAUNCHER_PROJECT = 'src/Daoris.Desktop/Daoris.Desktop.Launcher';
export const APP_PROJECT = 'src/Daoris.Desktop/Daoris.Desktop.App';

/** The launcher's publish into `folder` (the publish's own comments say why each option), its builds into `artifacts` when given. */
export function launcherPublishCommand(folder, artifacts = null) {
  return builtInto(`dotnet publish "${LAUNCHER_PROJECT}" -c Release -r ${INSTALL_RID} --self-contained false `
    + '-p:PublishSingleFile=true -p:DebugType=none '
    + `-p:AllowedReferenceRelatedFileExtensions=none ${STAMPED} -o "${folder}" --nologo`, artifacts);
}

/** The application's publish into `folder` (the publish's own comments say why each option), its builds into `artifacts` when given. */
export function appPublishCommand(folder, artifacts = null) {
  return builtInto(`dotnet publish "${APP_PROJECT}" -c Release -r ${INSTALL_RID} --self-contained false `
    + `-p:DebugType=none -p:AllowedReferenceRelatedFileExtensions=none ${STAMPED} -o "${folder}" --nologo`, artifacts);
}

/**
 * Where a publish lays its parts out before an install takes them: under `artifacts` when the publish is given
 * one, beside its builds; otherwise in the application project's `bin/`, as always.
 */
export function publishStages(repoRoot, artifacts = null) {
  return artifacts ? join(artifacts, 'publish-stage') : join(repoRoot, ...APP_PROJECT.split('/'), 'bin', 'publish-stage');
}

/**
 * `--artifacts-path <folder>`: the folder every build of this publish goes into, made absolute against `cwd` as
 * `--to` is, or null when not given: each project's own `bin/` and `obj/`. The
 * deployment rehearsal gives one of its own, so it can run beside the driver's git half, which builds into and runs from
 * the desktop projects' folders. Given with no folder, a refusal.
 */
export function artifactsOf(argv, cwd = process.cwd()) {
  const at = argv.indexOf('--artifacts-path');
  if (at === -1) return { artifacts: null, refusal: null };
  const given = argv[at + 1];
  if (!given || given.startsWith('--')) {
    return { artifacts: null, refusal: 'desktop-publish: --artifacts-path <folder> names the folder every build of the publish goes into; it was given none.' };
  }
  return { artifacts: isAbsolute(given) ? given : resolve(cwd, given), refusal: null };
}

/**
 * Daoris's three git doors: one program, published framework-dependent and
 * laid out in `app/bin/` whole (its executable, its library and the runtime's two files beside them), then its executable again
 * as the remote helper and as the credential door, which load the same library, since the apphost names its library inside. Twins of
 * `GitWiring.SshDoor`, `GitWiring.RemoteHelper` and `GitCredentialDoor.Exe`, which hand a start a door only where it is laid out here: until this lays them
 * out, nothing an install starts is routed. The shape `GitDoorsProcessTests` lays out from the build and proves.
 */
export const GIT_DOOR_PROJECT = 'src/Daoris.Desktop/Daoris.Desktop.GitDoor';
export const GIT_DOORS = Object.freeze(['daoris-git-ssh.exe', 'git-remote-daoris.exe', 'git-credential-daoris.exe']);

/** The doors' publish into `folder`: framework-dependent, the shape the launcher and the driver have; its builds into `artifacts` when given. */
export function gitDoorPublishCommand(folder, artifacts = null) {
  return builtInto(`dotnet publish "${GIT_DOOR_PROJECT}" -c Release -r ${INSTALL_RID} --self-contained false `
    + `-p:DebugType=none -p:AllowedReferenceRelatedFileExtensions=none ${STAMPED} -o "${folder}" --nologo`, artifacts);
}

/**
 * Lay the doors' publish in `published` out in `<install>/app/bin/`, beside the launchers (which {@link layCli} replaces whole, so
 * this runs after it), the ssh door copied as the remote helper and as the credential door. Throws naming what the publish did not make.
 */
export function layGitDoors(published, install) {
  const [ssh, ...others] = GIT_DOORS;
  if (!existsSync(join(published, ssh))) throw new Error(`desktop-publish: the publish of the git doors made no ${ssh}.`);
  const bin = join(install, ...CLI_BIN);
  mkdirSync(bin, { recursive: true });
  for (const name of readdirSync(published)) cpSync(join(published, name), join(bin, name), { recursive: true });
  for (const name of others) cpSync(join(published, ssh), join(bin, name));
}

/**
 * The partner's launcher: one program, published framework-dependent and laid out in
 * `app/bin/` whole beside the git doors. The driver starts it for a partner alone; it joins the piece's job, then starts the harness
 * inside it. A twin of `PieceLauncher.Program`, which `PieceLauncherTests` holds to this name.
 */
export const PIECE_START_PROJECT = 'src/Daoris.Desktop/Daoris.Desktop.PieceStart';
export const PIECE_START = 'daoris-piece-start.exe';

/** The launcher's publish into `folder`, in the git doors' shape; its builds into `artifacts` when given. */
export function pieceStartPublishCommand(folder, artifacts = null) {
  return builtInto(`dotnet publish "${PIECE_START_PROJECT}" -c Release -r ${INSTALL_RID} --self-contained false `
    + `-p:DebugType=none -p:AllowedReferenceRelatedFileExtensions=none ${STAMPED} -o "${folder}" --nologo`, artifacts);
}

/** Lay the launcher's publish in `published` out in `<install>/app/bin/`, after {@link layCli}. Throws naming what the publish did not make. */
export function layPieceStart(published, install) {
  if (!existsSync(join(published, PIECE_START))) throw new Error(`desktop-publish: the publish of the partner's launcher made no ${PIECE_START}.`);
  const bin = join(install, ...CLI_BIN);
  mkdirSync(bin, { recursive: true });
  for (const name of readdirSync(published)) cpSync(join(published, name), join(bin, name), { recursive: true });
}

/** The install's command: one program, published and laid out in `app/bin/` whole as the git doors are; a twin of `CommandLauncher.Program`. */
export const COMMAND_PROJECT = 'src/Daoris.Desktop/Daoris.Desktop.Command';
export const COMMAND = CLI_LAUNCHERS[0];

/** The command's publish into `folder`, in the git doors' shape; its builds into `artifacts` when given. */
export function commandPublishCommand(folder, artifacts = null) {
  return builtInto(`dotnet publish "${COMMAND_PROJECT}" -c Release -r ${INSTALL_RID} --self-contained false `
    + `-p:DebugType=none -p:AllowedReferenceRelatedFileExtensions=none ${STAMPED} -o "${folder}" --nologo`, artifacts);
}

/** Lay the command's publish in `published` out in `<install>/app/bin/`, after {@link layCli}. Throws naming what the publish did not make. */
export function layCommand(published, install) {
  if (!existsSync(join(published, COMMAND))) throw new Error(`desktop-publish: the publish of the command made no ${COMMAND}.`);
  const bin = join(install, ...CLI_BIN);
  mkdirSync(bin, { recursive: true });
  for (const name of readdirSync(published)) cpSync(join(published, name), join(bin, name), { recursive: true });
}

// The registration lifecycle from a terminal (D50, workspace design §2b): everything a person manages
// has a real surface in the desktop **or** the CLI, and no capability is stranded on a machine with no
// screen — a headless server running the driver is just another machine.
//
// These are MANAGEMENT commands, not doctrine commands: opt-in, loopback-talking, never run by a gate.
// They reach the network only through `service.ts`, which is the single module the offline-discipline
// tests name. Nothing here is imported by `check` or anything `check` reaches.

import { isAbsolute, resolve } from 'node:path';
import { DaorisError } from './errors.ts';
import { flagValue, operands } from './args.ts';
import { endpoint, isLocalService, refusal, request } from './service.ts';
import { repositoryName } from './connect.ts';
import type { CommandArgs } from './types.ts';
import type { ExitCode } from './errors.ts';

/** The first bare argument, if there is one. Flags are never a value. */
function named(argv: string[]): string | undefined {
  // `retire` takes no flag with a value, so every bare token is an operand. `import` reads its own.
  return operands(argv, new Set())[0];
}

/**
 * Take a repository off the machine's registry.
 *
 * @remarks
 * The counterpart to `connect`, and the verb a person is most right to be nervous about — so the
 * service's own sentence, which says what retiring does NOT do, is printed verbatim. Nothing on disk
 * is touched: files, history and doctrine are the repository's own, and its entries leave the index.
 */
export async function commandRetire({ root, argv, write }: CommandArgs): Promise<ExitCode> {
  const repository = named(argv) ?? repositoryName(root);

  if (argv.includes('--dry-run')) {
    write(`daoris: would retire ${repository} — the registration only; no file is ever touched`);
    return 0;
  }

  const { status, json } = await request('DELETE', `/api/registry/${encodeURIComponent(repository)}`);
  if (status < 200 || status >= 300) throw new DaorisError(refusal(status, json), 1);

  // A repository that was never registered is an answer, not a failure: the end state is the one
  // that was asked for, and a non-zero exit would make an idempotent script look broken.
  write(`daoris: ${json?.message ?? `${repository} is no longer registered.`}`);
  return 0;
}

/**
 * Move a registered repository to a workspace by name — the terminal door of the drawer's Move to
 * workspace and Ask Daoris's `repository_propose` (WIRE1, D50, D161's ENTRY1d1 note).
 *
 * @remarks
 * Re-wiring only: the route changes the workspace and nothing else about the row, unlike
 * `connect --workspace`, which registers the whole row again from inside the checkout. A repository the
 * registry does not hold (404) and a workspace outside a shared host's circle (409) are the service's own
 * sentences, relayed as refusals.
 */
export async function commandWire({ argv, write }: CommandArgs): Promise<ExitCode> {
  const workspace = flagValue(argv, '--workspace');
  const repository = operands(argv, new Set(['--workspace']))[0];
  if (repository === undefined || workspace === undefined) {
    throw new DaorisError('usage: daoris wire <repository> --workspace <name> — e.g. `daoris wire aurora-engine --workspace work`.');
  }

  if (argv.includes('--dry-run')) {
    write(`daoris: would move \`${repository}\` to workspace \`${workspace}\``);
    return 0;
  }

  const { status, json } = await request(
    'POST', `/api/registry/${encodeURIComponent(repository)}/workspace`, { workspace });
  if (status < 200 || status >= 300) throw new DaorisError(refusal(status, json), 1);

  write(`daoris: \`${repository}\` is now in workspace \`${workspace}\`.`);
  return 0;
}

/**
 * Register everything a folder's subdirectories propose — the bootstrap, run deliberately (D48 §3).
 *
 * @remarks
 * The scan used to BE the registry, which failed the way scans fail: what it did not say governed as
 * much as what it said. As a verb it is simply a fast way to add many repositories at once, and it is
 * safe to re-run — an unnamed import states no workspace, and unstated wiring is preserved. Naming
 * one with `--workspace` is a statement, and a statement re-points, as `connect --workspace` does.
 */
export async function commandImport({ root, argv, write }: CommandArgs): Promise<ExitCode> {
  // A named workspace sets the folder up AS one (D77): every row it registers lands there. Read
  // before the folder, so the flag's value is never taken for it.
  const workspace = flagValue(argv, '--workspace');
  const argument = operands(argv, new Set(['--workspace']))[0];
  // Absolute, always: the service is a different process and may sit in a different directory, so a
  // relative path would resolve against whatever the host happens to be running in.
  const folder = argument === undefined
    ? undefined
    : isAbsolute(argument) ? argument : resolve(root, argument);

  if (argv.includes('--dry-run')) {
    write(`daoris: would import ${folder ?? "the service's own knowledge root"}`
      + (workspace ? ` into workspace \`${workspace}\`` : ''));
    return 0;
  }

  // 🔴 A folder is a machine path, and it goes only to a service on this machine — the rule `connect`
  // keeps for a root (D47 §4). A shared deployment refused it anyway, but only after it arrived.
  if (folder !== undefined && !isLocalService(endpoint().url)) {
    throw new DaorisError(
      `${endpoint().url} is not on this machine, and a folder here means nothing there — import a `
      + 'folder into this machine\'s own service; a deployment is fed by the machines that join it.');
  }

  const { status, json } = await request('POST', '/api/registry/import', {
    ...(folder ? { folder } : {}),
    ...(workspace ? { workspace } : {}),
  });
  if (status < 200 || status >= 300) throw new DaorisError(refusal(status, json), 1);

  write(`daoris: ${json?.message ?? 'imported.'}`);
  return 0;
}

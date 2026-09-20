// `daoris remote` — the machine's wiring, from a terminal (D50, workspace design §2b).
//
// Management parity, in the shape the driver already proved: THE FILE IS THE API and this is an editor
// over it. `~/.daoris/remotes.json` maps a workspace to the deployment that serves it (D48 §5); the
// desktop's settings surface edits the same file, and hand-editing keeps working because the file —
// not the surface — is the truth.
//
// It is a MANAGEMENT command and it is nonetheless entirely OFFLINE: it edits one file under the
// profile and talks to nothing. Validating the url by calling it was considered and rejected — a verb
// that needed the deployment to be up could not wire a machine before the deployment exists, which is
// the order a person actually does it in, and the sync's own report is where an unreachable remote
// gets named (`RemoteSyncSet`).
//
// A KEY IS NEVER PRINTED. Only the audit prefix the deployment's own `keys list` shows, because a
// console is a transcript, a log, and eventually an issue someone pastes into.

import { createInterface } from 'node:readline';
import { flagValue } from './args.ts';
import { DaorisError } from './errors.ts';
import {
  KEY_VARIABLE, PATH_VARIABLE, URL_VARIABLE, WORKSPACE_VARIABLE,
  normalizeWorkspace, readRemotes, redactKey, remotesPath, writeRemotes,
} from './remotemap.ts';
import type { Remote } from './remotemap.ts';
import type { CommandArgs } from './types.ts';
import type { ExitCode } from './errors.ts';

/** Flags whose next token belongs to them, and is therefore not the workspace name. */
const VALUED = new Set(['--url', '--key']);

/**
 * The workspace name: the first bare argument after the verb.
 *
 * @remarks
 * A flag's VALUE is not a bare argument, however bare it looks. Skipping only the flags themselves
 * made `remote add --url https://…` read the url as the workspace name and wire a circle called
 * `https://aurora.example.com` — a wrong answer that looks like success, which is the shape this
 * whole command exists to avoid producing.
 */
function named(argv: string[]): string | undefined {
  for (let at = 1; at < argv.length; at += 1) {
    const token = argv[at]!;
    if (VALUED.has(token)) at += 1;
    else if (!token.startsWith('--')) return token;
  }

  return undefined;
}

/**
 * The key, from the flag, the environment, or the person.
 *
 * @remarks
 * A flag is the scriptable way and the environment is the headless way; typing it is the ordinary
 * one. Read as a plain line rather than through a terminal's hidden-input mode: this runs under agent
 * harnesses and CI shells where there is no TTY to put into raw mode, and a prompt that hangs there
 * would be worse than one that echoes. Pipe it (`echo $KEY | daoris remote add …`) when the echo
 * matters.
 */
async function resolveKey(
  argv: string[], workspace: string, write: (line: string) => void,
): Promise<string> {
  const flagged = flagValue(argv, '--key');
  if (flagged) return flagged;

  const fromEnv = process.env[KEY_VARIABLE];
  if (fromEnv?.trim()) {
    write(`  key from ${KEY_VARIABLE}`);
    return fromEnv.trim();
  }

  const typed = await new Promise<string>((resolve) => {
    const reader = createInterface({ input: process.stdin, output: process.stdout });
    reader.question(`  key for \`${workspace}\` (stored in the map, never tracked): `, (answer) => {
      reader.close();
      resolve(answer);
    });
  });

  if (!typed.trim()) {
    throw new DaorisError(
      `no key for \`${workspace}\` — pass \`--key dk_…\`, set ${KEY_VARIABLE}, or pipe it in. `
      + 'A deployment mints one per person per machine on its own console (`keys mint`).');
  }

  return typed.trim();
}

/**
 * Read, edit, or clear this machine's remotes.
 *
 * @remarks
 * Verbs, not flags on one verb: `add` and `remove` do opposite things to the same file, and a boolean
 * distinguishing them is the shape that eventually gets defaulted wrong.
 */
export async function commandRemote({ argv, write }: CommandArgs): Promise<ExitCode> {
  const verb = argv[0] ?? 'list';
  const path = remotesPath();

  switch (verb) {
    case 'list':
      return list(write);

    case 'add': {
      const workspace = normalizeWorkspace(requireName(argv, 'add'));
      const url = flagValue(argv, '--url');
      if (!url) {
        throw new DaorisError(
          `\`remote add ${workspace}\` needs the deployment's address — \`--url https://…\`. `
          + 'The workspace name keys the map; the url and the key are what speak to its server.');
      }

      write(`daoris: wiring \`${workspace}\` on this machine`);
      const key = await resolveKey(argv, workspace, write);

      const { remotes } = readFile(path);
      const replacing = remotes.get(workspace);
      remotes.set(workspace, { url: url.replace(/\/+$/, ''), key });
      writeRemotes(path, remotes);

      write(`  ${workspace}  ${url.replace(/\/+$/, '')}  ${redactKey(key)}`);
      if (replacing) write(`  (replacing ${replacing.url} — the previous key is untouched at its deployment)`);
      write(`  written to ${path} — machine-local, tracked by nothing (D47 §7).`);
      write('  Repositories feed it where their own manifest says they may; the map only says where.');
      return 0;
    }

    case 'remove': {
      const workspace = normalizeWorkspace(requireName(argv, 'remove'));
      const { remotes } = readFile(path);

      if (!remotes.delete(workspace)) {
        // The end state is the one that was asked for, so a non-zero exit would make an idempotent
        // script look broken — the same judgement `retire` makes.
        write(`daoris: \`${workspace}\` is not wired on this machine — nothing to remove.`);
        return 0;
      }

      writeRemotes(path, remotes);
      write(`daoris: \`${workspace}\` is no longer wired on this machine.`);
      write('  Nothing at the deployment changed: the key stays valid there until an operator revokes');
      write('  it, and what that workspace already fed stays where it is. This circle now syncs');
      write('  nowhere from here, silently — which is what having no remote has always meant.');
      return 0;
    }

    default:
      throw new DaorisError(`unknown remote verb '${verb}' — one of: list, add, remove`);
  }

  function list(out: (line: string) => void): ExitCode {
    const wiring = readRemotes();

    if (wiring.source === 'environment') {
      out(`daoris: the environment names this machine's remote, whole — ${path} is not read.`);
      out(`  (${URL_VARIABLE} / ${KEY_VARIABLE}, for the workspace ${WORKSPACE_VARIABLE} names.)`);
    }

    if (wiring.remotes.size === 0) {
      out('daoris: no remote on this machine — every workspace stays local, which is the default (D21).');
      out(`  \`daoris remote add <workspace> --url https://… --key dk_…\` wires one (${path}).`);
      return 0;
    }

    if (wiring.source === 'file') out(`daoris: ${path}`);
    for (const workspace of [...wiring.remotes.keys()].sort()) {
      const remote = wiring.remotes.get(workspace)!;
      out(`  ${workspace.padEnd(16)} ${remote.url}  ${redactKey(remote.key)}`);
    }

    return 0;
  }

  function readFile(target: string): { remotes: Map<string, Remote> } {
    // Deliberately the FILE, not the live wiring — an edit must land in the map even when the
    // environment currently outranks it, or a machine that happens to have the env pair set could
    // never wire a second workspace. `list` is where the person is told which one is live.
    return { remotes: readRemotes({ [PATH_VARIABLE]: target }).remotes };
  }
}

/** The workspace name, refused rather than defaulted: the name is what the map is keyed by. */
function requireName(argv: string[], verb: string): string {
  const name = named(argv);
  if (!name) {
    throw new DaorisError(
      `\`remote ${verb}\` needs a workspace name — e.g. \`daoris remote ${verb} aurora\`. `
      + 'The name is the circle, and it is what a shared deployment declares itself to be.');
  }

  return name;
}

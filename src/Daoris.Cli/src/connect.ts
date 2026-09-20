// `connect` — the registration half of the management class (D50). An explicit, opt-in registration
// with a knowledge service, never run by a gate and never on the path of anything that is; a
// repository that never runs it loses nothing but discoverability.
//
// The network itself lives in `service.ts`, which is the one module the offline-discipline tests name.
// D8's guarantee is about the DOCTRINE operations — `check`, `sync`, `index`, `upstream` — all of
// which are pure local hashing against the lock and stay that way.

import { flagValue } from './args.ts';
import { readManifest } from './config.ts';
import { DaorisError } from './errors.ts';
import { endpoint, isLocalService, refusal, request } from './service.ts';
import type { CommandArgs, Domain, Manifest } from './types.ts';
import type { ExitCode } from './errors.ts';

const REGISTRY_PATH = '/api/registry';

export { endpoint, isLocalService } from './service.ts';

/**
 * What this repository tells a service about itself.
 *
 * @remarks
 * Sent rather than scanned because a **remote** service cannot see the repository at all. A service
 * running on this machine can read manifests off disk, and does; one running anywhere else has no such
 * option, and pretending otherwise would make the hosted deployment a second-class citizen.
 *
 * The root travels only to a **local** service (D46): it is what the driver spawns a session in, and it
 * is a machine path — `sensitive-info` keeps those out of tracked files, and the same judgement keeps
 * them off the network. A remote deployment gets the declaration and nothing about anyone's disk.
 *
 * The manifest's remote declaration travels too (D47 §4): a sync loop feeds only what the repository
 * itself, under review, said may leave. Sent as explicit booleans — silence in the manifest becomes
 * `false` on the wire, so a service never guesses what an absent field meant.
 *
 * The workspace is the opposite shape, deliberately (D48 as amended). It is **wiring**, like a git
 * remote: it lives in the machine's registry, comes from `--workspace` rather than from any tracked
 * file, and is **omitted entirely** when unstated — because an absent field PRESERVES the existing
 * row, while an explicit `null` or `""` would re-point every repository to the default on the next
 * ordinary sync tick. Silence here means "I am not saying", not "I say default".
 */
export function registration(
  root: string, manifest: Manifest, name: string, serviceUrl: string, workspace?: string,
): {
  repository: string;
  packs: string[];
  domain: Domain | null;
  join: boolean;
  shareKnowledge: boolean;
  root?: string;
  workspace?: string;
} {
  return {
    repository: name,
    packs: manifest.packs,
    domain: manifest.domain ?? null,
    join: manifest.remote?.join ?? false,
    shareKnowledge: manifest.remote?.knowledge ?? false,
    ...(isLocalService(serviceUrl) ? { root } : {}),
    ...(workspace ? { workspace } : {}),
  };
}

/** True when the domain says enough for a sibling to know what is worth asking. */
export function isDeclared(domain: Domain | undefined | null): boolean {
  if (!domain) return false;
  return Boolean(domain.summary?.trim()) || domain.owns.length > 0 || domain.accepts.length > 0;
}

export async function commandConnect({ root, argv, write }: CommandArgs): Promise<ExitCode> {
  const manifest = readManifest(root);
  const name = root.replace(/[\\/]+$/, '').split(/[\\/]/).pop() ?? 'unknown';

  if (!isDeclared(manifest.domain)) {
    // Registering an empty declaration is worse than not registering: it puts the repository on the
    // map as something that answers nothing, and a sibling reading that learns less than from a gap.
    throw new DaorisError(
      `${name} has not said what it is. Fill in 'domain' in daoris.json first:\n`
      + "  summary — one line, for someone who has never opened this repository\n"
      + '  owns    — the areas where a change belongs here rather than anywhere else\n'
      + '  accepts — the kinds of work it is worth asking of you\n'
      + '  That declaration is how siblings know whether a quest is yours.',
      1);
  }

  const { url } = endpoint();
  const body = registration(root, manifest, name, url, flagValue(argv, '--workspace'));
  if (argv.includes('--dry-run')) {
    write(JSON.stringify(body, null, 2));
    write(`daoris: would register with ${url}${REGISTRY_PATH}`);
    return 0;
  }
  const { status, json } = await request('POST', REGISTRY_PATH, body);
  if (status < 200 || status >= 300) {
    throw new DaorisError(`the service refused the registration: ${refusal(status, json)}`);
  }

  // The workspace the service says TOOK, not the one that was asked for — silence preserves whatever
  // the row already held, so the only honest way to report the wiring is to read it back. A service
  // too old to answer with one simply goes unmentioned rather than being guessed at.
  const landed = json as { workspace?: string } | null;

  write(`daoris: registered ${name} with ${url}`);
  write(`  owns ${manifest.domain!.owns.length} area(s); accepts ${manifest.domain!.accepts.length} kind(s)`);
  if (landed?.workspace) write(`  workspace: ${landed.workspace} — this machine's wiring; nothing was written here`);
  write('  siblings can now address quests here, and see what is worth asking.');
  return 0;
}

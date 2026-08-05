import { readManifest, writeManifest } from './config.ts';
import { DaorisError } from './errors.ts';
import type { CommandArgs, Domain, Manifest } from './types.ts';
import type { ExitCode } from './errors.ts';

/**
 * **The only module in this CLI permitted to touch the network**, and the reason the offline guarantee
 * is scoped rather than absolute.
 *
 * D8 says `check` works offline, because it runs inside build gates and a gate that can fail on a
 * network call is not a gate. That is about the doctrine operations — `check`, `sync`, `index`,
 * `upstream` — all of which are pure local hashing against the lock and stay that way.
 *
 * `connect` is a different thing: an explicit, opt-in registration with a knowledge service, never run
 * by a gate and never on the path of anything that is. A repository that never runs it loses nothing
 * but discoverability.
 *
 * A test enforces exactly this shape: no other module may contain a network primitive, and nothing
 * `check` reaches may import this one.
 */
const REGISTRY_PATH = '/api/registry';

/** Where the service is, and the key it wants — supplied by the environment, never committed. */
export function endpoint(env: NodeJS.ProcessEnv = process.env): { url: string; key: string | null } {
  const url = env.DAORIS_SERVICE_URL;
  if (!url) {
    throw new DaorisError(
      'no DAORIS_SERVICE_URL — `connect` registers this repository with a knowledge service, and needs\n'
      + '  to know where one is. Set it in your environment; a local service is usually\n'
      + '  http://localhost:5177. Everything else daoris does works without one.');
  }

  return { url: url.replace(/\/+$/, ''), key: env.DAORIS_SERVICE_KEY ?? null };
}

/**
 * What this repository tells a service about itself.
 *
 * @remarks
 * Sent rather than scanned because a **remote** service cannot see the repository at all. A service
 * running on this machine can read manifests off disk, and does; one running anywhere else has no such
 * option, and pretending otherwise would make the hosted deployment a second-class citizen.
 */
export function registration(root: string, manifest: Manifest, name: string): {
  repository: string;
  packs: string[];
  canonSource: string;
  domain: Domain | null;
} {
  return {
    repository: name,
    packs: manifest.packs,
    canonSource: manifest.source,
    domain: manifest.domain ?? null,
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

  const body = registration(root, manifest, name);
  if (argv.includes('--dry-run')) {
    write(JSON.stringify(body, null, 2));
    write(`daoris: would register with ${endpoint().url}${REGISTRY_PATH}`);
    return 0;
  }

  const { url, key } = endpoint();
  const response = await fetch(`${url}${REGISTRY_PATH}`, {
    method: 'POST',
    headers: {
      'content-type': 'application/json',
      ...(key ? { authorization: `Bearer ${key}` } : {}),
    },
    body: JSON.stringify(body),
  }).catch((error: Error) => {
    throw new DaorisError(
      `could not reach the service at ${url} — ${error.message}\n`
      + '  Nothing else daoris does needs it; this only affects discoverability.');
  });

  if (!response.ok) {
    throw new DaorisError(`the service refused the registration: ${response.status} ${response.statusText}`);
  }

  write(`daoris: registered ${name} with ${url}`);
  write(`  owns ${manifest.domain!.owns.length} area(s); accepts ${manifest.domain!.accepts.length} kind(s)`);
  write('  siblings can now address quests here, and see what is worth asking.');
  return 0;
}

/** Kept out of the manifest writer so a caller can update a domain without rewriting the file by hand. */
export function declare(root: string, domain: Domain): void {
  const manifest = readManifest(root);
  writeManifest(root, { ...manifest, domain, harnessDescriptor: undefined } as unknown as Manifest);
}

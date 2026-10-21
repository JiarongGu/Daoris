// **The only module in this CLI permitted to touch the network**, and the reason the offline
// guarantee is scoped rather than absolute.
//
// D8 says `check` works offline, because it runs inside build gates and a gate that can fail on a
// network call is not a gate. That is about the DOCTRINE operations — `check`, `sync`, `index`,
// `upstream` — all of which are pure local hashing against the lock and stay that way.
//
// The MANAGEMENT commands are a different thing (D50): explicit, opt-in conversations with a
// knowledge service — `connect`, `retire`, `import` — never run by a gate and never on the path of
// anything that is. A repository that never runs them loses nothing but discoverability. They all
// speak through this one module, so the guarantee stays testable as one sentence: exactly one file
// here reaches the network, and nothing a doctrine command imports can reach it.
//
// Two tests enforce exactly that shape. The second is the one that matters — a gate breaks by an
// innocuous import three modules deep acquiring a socket, not by an obvious `fetch`.
//
// A vendor's release channel (AGT2b), a tool's maker and a resource location (TOOLS4) were fetched here once; each is Daoris's
// driver program's since its family became the program's (PUSH1f2c, PUSH1f2e), so no verb of this package fetches one.
//
// The running Daoris's own pipe was opened here too, from RPC1f until the last family that sent its routes over it, `daoris plugin`,
// became the driver program's (PUSH1f2f, D194): the program is the pipe's one client at a terminal (`InstanceDoor`), and holds the
// home's lease where it acts alone (`HomeLease`), so this package opens neither.

import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { DaorisError } from '../platform/errors.ts';
import { daorisHome, homeEntry, homeId, isHomeId, THIS_INSTALL } from '../platform/home.ts';

const NO_SERVICE =
  'no DAORIS_SERVICE_URL — this command talks to a knowledge service, and needs\n'
  + '  to know where one is. Set it in your environment; a local service is usually\n'
  + '  http://localhost:5177. Everything else daoris does works without one.';

/**
 * Where the service is, and the key it wants — supplied by the environment, never committed. With no variable, a `daoris`
 * the install carries finds the service the running desktop holds (FINDDAORIS1, D171 point 1, `runningService`), and refuses
 * saying Daoris is not running there and how to start it. A checkout reads no address file: on a machine with an install, the
 * account's `DAORIS_HOME` names that install's live home, and a test that cleared the variable must never reach it.
 */
export async function endpoint(
  env: NodeJS.ProcessEnv = process.env, get: Get = fetch, install: string | null = THIS_INSTALL,
): Promise<{ url: string; key: string | null }> {
  const found = await located(env, get, install);
  if ('why' in found) throw new DaorisError(found.why ? `${NO_SERVICE}\n  ${found.why}` : NO_SERVICE);
  return { url: found.url, key: env.DAORIS_SERVICE_KEY ?? null };
}

/** The service's address: the variable, else (installed) the running desktop's, else why there is none ('' where none was looked for). */
async function located(env: NodeJS.ProcessEnv, get: Get, install: string | null): Promise<{ url: string } | { why: string }> {
  const named = env.DAORIS_SERVICE_URL?.trim().replace(/\/+$/, '');
  if (named) return { url: named };
  const home = install ? daorisHome(env, install) : null;
  return install && home ? runningService(home, install, get) : { why: '' };
}

/** The address file the running desktop leaves in its home (the driver's `RunningAddress.FileName`). */
export const RUNNING_FILE = 'running.json';

/**
 * The service the running desktop holds for `home` (FINDDAORIS1, D171 point 1), from the address it left there: taken only
 * while the process it names lives, the address is this machine's (a terminal's verbs send this machine's paths, D46), and
 * its status door answers as this home's Daoris host: naming its search tier and this home's id (HOMEID1, `home.json`),
 * asked with redirects off, since an address that sends its asker elsewhere has not answered. This CLI cannot read another
 * process's start, so a process id the system gave to another process since lives to it: the id is what tells. Otherwise why
 * not, in a sentence that says how to start Daoris. The driver's `RunningService` holds the same contract in C#.
 */
export async function runningService(
  home: string, install: string, get: Get = fetch, lives: (pid: number) => boolean = processLives,
): Promise<{ url: string } | { why: string }> {
  const start = join(install, 'Daoris.exe');
  const entry = runningEntry(home);
  if (!entry || !lives(entry.pid)) return { why: `Daoris is not running on ${home}: start it (${start}), and run this again once its window is up.` };
  if (!isLocalService(entry.url)) {
    return {
      why: `the address Daoris left in ${home} (${entry.url}) is not on this machine, and only this machine's own service `
        + 'is taken from it; set DAORIS_SERVICE_URL to reach another.',
    };
  }

  let answered: { home: string | null } | null = null;
  try {
    const response = await get(`${entry.url}/api/status`, { method: 'GET', redirect: 'manual', signal: AbortSignal.timeout(2_000) });
    const status: unknown = await response.json().catch(() => null);
    if (response.ok && typeof status === 'object' && status !== null && typeof (status as Record<string, unknown>).tier === 'string') {
      const named = (status as Record<string, unknown>).home;
      answered = { home: isHomeId(named) ? named : null };
    }
  } catch {
    answered = null;
  }

  if (!answered) {
    return {
      why: `Daoris is running on ${home} (pid ${entry.pid}), and nothing answered as its service at ${entry.url}: run this `
        + `again once its window is up, or start it again (${start}).`,
    };
  }

  const mine = homeId(home);
  return mine && answered.home === mine
    ? { url: entry.url }
    : {
      why: `Daoris is running on ${home} (pid ${entry.pid}), and the service at ${entry.url} answered as `
        + `${answered.home ? "another Daoris home's" : "no Daoris home's"}, so nothing was sent to it: start Daoris on this home `
        + `(${start}), or set DAORIS_SERVICE_URL to the service you mean.`,
    };
}

/**
 * What a home's address file names, or null when there is none or it does not read. Its `pipe` (RPC1a) is the driver program's to
 * read since PUSH1f2f (`RunningAddress.Read`): nothing here sends over it.
 */
function runningEntry(home: string): { url: string; pid: number } | null {
  try {
    const entry: unknown = JSON.parse(readFileSync(homeEntry(home, 'running'), 'utf8'));
    if (typeof entry !== 'object' || entry === null) return null;
    const { url, pid } = entry as Record<string, unknown>;
    if (typeof url !== 'string' || url.length === 0 || !Number.isInteger(pid)) return null;
    return { url: url.replace(/\/+$/, ''), pid: pid as number };
  } catch {
    return null;
  }
}

/** Whether a process runs with this id: signal 0 tests and sends nothing; one not ours to signal still runs. */
function processLives(pid: number): boolean {
  try {
    process.kill(pid, 0);
    return true;
  } catch (error) {
    return (error as NodeJS.ErrnoException).code === 'EPERM';
  }
}

/**
 * Whether a service URL points at this machine. The hostname, not a substring — a remote host named
 * `localhost.example.com` is exactly the trap a substring check walks into. An unparseable URL answers
 * false: when in doubt, the machine path stays home.
 */
export function isLocalService(url: string): boolean {
  try {
    const { hostname } = new URL(url);
    return hostname === 'localhost' || hostname === '127.0.0.1' || hostname === '::1' || hostname === '[::1]';
  } catch {
    return false;
  }
}

/**
 * One request, with the service's own sentence preserved.
 *
 * @remarks
 * A refusal here is an answer the service composed deliberately — who is addressable, what a retire
 * does not do, why an import found nothing. Replacing it with a status code would make the CLI say
 * less than the service said, which is the failure the driver's own client already learned once.
 *
 * A door whose records are this machine's alone (BRAINSTORM1a3: a brainstorm, D176 point 8) names why in `here`, and is asked of
 * this machine's own service only: a service elsewhere is refused before anything is sent, as `import` refuses a folder.
 */
export async function request(
  method: 'GET' | 'POST' | 'DELETE', path: string, body?: unknown, { here }: { here?: string } = {},
): Promise<{ status: number; json: Record<string, unknown> | null; url: string }> {
  const { url, key } = await endpoint();
  if (here !== undefined && !isLocalService(url)) {
    throw new DaorisError(`${url} is not this machine's service, and ${here}: nothing was sent.`, 1);
  }

  const response = await fetch(`${url}${path}`, {
    method,
    headers: {
      ...(body === undefined ? {} : { 'content-type': 'application/json' }),
      ...(key ? { authorization: `Bearer ${key}` } : {}),
    },
    ...(body === undefined ? {} : { body: JSON.stringify(body) }),
  }).catch((error: Error) => {
    throw new DaorisError(
      `could not reach the service at ${url} — ${error.message}\n`
      + '  Nothing else daoris does needs it; this only affects discoverability.');
  });

  const json = await response.json().catch(() => null) as Record<string, unknown> | null;
  return { status: response.status, json, url };
}

/** The service's own sentence, or a bare status when it did not compose one. */
export function refusal(status: number, json: Record<string, unknown> | null): string {
  return typeof json?.error === 'string' ? json.error : `the service answered ${status}`;
}

// PUSH1f2b (D194 point 7): this machine's session records and the registry's checkouts, which `daoris driver retry` and `review`
// read here, are read by Daoris's driver program since the family became its (`DriverChoicesConsole`).

/** What `fetch` is to this module: handed in by a test, which answers as a host without opening a socket. */
export type Get = (url: string, init: RequestInit) => Promise<Response>;

// PUSH1f2e: a maker's release channel, which `daoris agent pin` and a pinned `update` fetched here, is Daoris's driver program's
// (`ReleaseChannels`), with the judgements `channels.ts` made of what arrived.

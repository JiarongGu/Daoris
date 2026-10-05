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
// It reaches one other kind of host (AGT2b): a vendor's release channel, for `agent pin` (and for
// `agent update`, which asks the channel for its newest release and then pins it, USE1a). That is the
// same shape of conversation — a person asked for it, and no gate runs it — so it lives here rather
// than giving the toolchain a socket of its own; the dispatcher hands the fetcher in.
//
// And since TOOLS4, a tool's maker and a resource location, for `daoris tool download|use … managed|update|look`
// (D121 §3.6, §3.7): the same fetcher, its hops held to the address rule the dispatcher names.

import { createWriteStream } from 'node:fs';
import { createHash } from 'node:crypto';
import { once } from 'node:events';
import { finished } from 'node:stream/promises';
import { DaorisError, RefusalError } from './errors.ts';
import type { Fetcher } from './channels.ts';

/** Where the service is, and the key it wants — supplied by the environment, never committed. */
export function endpoint(env: NodeJS.ProcessEnv = process.env): { url: string; key: string | null } {
  const url = env.DAORIS_SERVICE_URL;
  if (!url) {
    throw new DaorisError(
      'no DAORIS_SERVICE_URL — this command talks to a knowledge service, and needs\n'
      + '  to know where one is. Set it in your environment; a local service is usually\n'
      + '  http://localhost:5177. Everything else daoris does works without one.');
  }

  return { url: url.replace(/\/+$/, ''), key: env.DAORIS_SERVICE_KEY ?? null };
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
 */
export async function request(
  method: 'GET' | 'POST' | 'DELETE', path: string, body?: unknown,
): Promise<{ status: number; json: Record<string, unknown> | null }> {
  const { url, key } = endpoint();
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
  return { status: response.status, json };
}

/** The service's own sentence, or a bare status when it did not compose one. */
export function refusal(status: number, json: Record<string, unknown> | null): string {
  return typeof json?.error === 'string' ? json.error : `the service answered ${status}`;
}

/** The records door the driver reads its strikes from, closed records included (DRV6). */
const RECORDS_PATH = '/api/sessions?includeClosed=true';

/**
 * This machine's session records, for `daoris driver retry` to count a quest's failures from as the driver does (RETRY1b):
 * the list the host answered, or why it could not be read, in words the retry's refusal carries. It never throws, so a
 * terminal that cannot read them says so and how to go on without them, rather than guessing a mark.
 *
 * @remarks
 * Only a host on this machine is asked: the strikes are this machine's decision about spending (D58), counted from the
 * records its own host keeps, and a shared deployment's records are the team's. The type is `strikes.ts`'s
 * `SessionRecords`, spelled here so this module imports nothing a management verb's pure half holds.
 */
export async function sessionRecords(
  env: NodeJS.ProcessEnv = process.env, get: Get = fetch,
): Promise<{ records: unknown[] } | { unread: string }> {
  const url = env.DAORIS_SERVICE_URL?.trim().replace(/\/+$/, '');
  if (!url) return { unread: 'no DAORIS_SERVICE_URL is set, so this terminal knows no host to ask (the desktop\'s own is usually http://localhost:5177)' };
  if (!isLocalService(url)) {
    return { unread: `DAORIS_SERVICE_URL names ${url}, which is not this machine, and the strikes are counted from this machine's own host` };
  }

  const key = env.DAORIS_SERVICE_KEY;
  let response: Response;
  try {
    response = await get(`${url}${RECORDS_PATH}`, { method: 'GET', headers: key ? { authorization: `Bearer ${key}` } : {} });
  } catch (error) {
    return { unread: `the service at ${url} did not answer: ${error instanceof Error ? error.message : String(error)}` };
  }

  const json: unknown = await response.json().catch(() => null);
  if (!response.ok) {
    const said = typeof json === 'object' && json !== null && typeof (json as Record<string, unknown>).error === 'string'
      ? `: ${(json as Record<string, unknown>).error as string}` : '';
    return { unread: `the service at ${url} answered ${response.status}${said}` };
  }

  return Array.isArray(json) ? { records: json } : { unread: `the service at ${url} answered no list of records` };
}

/**
 * How a fetcher may follow a host (TOOLS4, D121 §3.6). Absent, it follows redirects as `fetch` does, as a maker's
 * release channel always has been.
 */
export interface FetchRules {
  /**
   * Whether an address may be fetched from: every hop is held to it — the first address and each redirect, which
   * are then followed here, one at a time, rather than by `fetch`. A tool's download and a resource location are
   * held to `isAddress`: https://, or http:// to this machine.
   */
  hop?: (url: string) => boolean;
  /** How many redirects one fetch follows before it is refused. */
  redirects?: number;
  /** How long a small read (`bytes`) may wait, in milliseconds; a download (`save`) waits for the person's stop. */
  bound?: number;
}

/** What `fetch` is to this module: handed in by a test, which answers as a host without opening a socket. */
export type Get = (url: string, init: RequestInit) => Promise<Response>;

/**
 * A vendor's release channel, over HTTPS (AGT2b), and since TOOLS4 a tool's download and a resource location,
 * held to the rules the caller names. It fetches and nothing else: every judgement about what arrived — the
 * signature, the hashes, the version, the list — is `channels.ts`'s or `toolinstall.ts`'s, where it is tested
 * without a network.
 *
 * @remarks
 * A download is streamed to disk and hashed on the way, never held whole: a Claude Code binary is
 * over 200 MB. A 404 answers null, because "that version is not there" is an answer the caller words;
 * anything else that is not a 2xx is refused here with the address and the status, as `unreachable`.
 */
export function releaseFetcher(rules: FetchRules = {}, request: Get = fetch): Fetcher {
  return {
    async bytes(url) {
      const response = await get(url, rules.bound);
      return response ? Buffer.from(await response.arrayBuffer()) : null;
    },

    async save(url, to) {
      const response = await get(url);
      if (!response?.body) return null;

      const hash = createHash('sha256');
      let size = 0;
      const out = createWriteStream(to);
      try {
        for await (const chunk of response.body as AsyncIterable<Uint8Array>) {
          hash.update(chunk);
          size += chunk.length;
          if (!out.write(chunk)) await once(out, 'drain');
        }
      } finally {
        out.end();
        await finished(out);
      }

      return { sha256: hash.digest('hex'), size };
    },
  };

  async function get(url: string, bound?: number): Promise<Response | null> {
    const { hop, redirects = 10 } = rules;
    let at = url;
    for (let hops = 0; ; hops += 1) {
      if (hop && !hop(at)) {
        throw new RefusalError('address', hops === 0
          ? `${at} is not https://, or http:// to this machine — nothing was fetched from it.`
          : `${url} redirected to ${at}, which is not https://, or http:// to this machine — nothing was fetched from it.`);
      }

      const response = await request(at, {
        redirect: hop ? 'manual' : 'follow',
        headers: { 'user-agent': 'daoris' },
        ...(bound === undefined ? {} : { signal: AbortSignal.timeout(bound) }),
      }).catch((error: Error) => {
        throw new RefusalError('unreachable', error.name === 'TimeoutError'
          ? `${new URL(at).host} did not answer within ${Math.round(bound! / 1000)} seconds. Nothing was installed.`
          : `could not reach ${new URL(at).host} — ${error.message}. Nothing was installed.`);
      });

      const location = response.headers.get('location');
      if (hop && response.status >= 300 && response.status < 400 && location) {
        if (hops >= redirects) {
          throw new RefusalError('unreachable', `${url} redirected more than ${redirects} times — nothing was fetched from it.`);
        }
        at = new URL(location, at).href;
        continue;
      }

      if (response.status === 404) return null;
      if (!response.ok) throw new RefusalError('unreachable', `${at} answered ${response.status} — nothing was installed.`);
      return response;
    }
  }
}

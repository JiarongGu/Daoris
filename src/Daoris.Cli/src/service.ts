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

import { DaorisError } from './errors.ts';

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

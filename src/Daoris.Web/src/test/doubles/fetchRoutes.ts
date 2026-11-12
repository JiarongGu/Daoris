import { vi } from 'vitest';

// The service as a suite stands it in over `fetch`: each route a URL prefix and its JSON answer (or a function of the
// URL), the first prefix that matches answering, and an empty list for any URL no route names.

export type Routes = Record<string, unknown | ((url: string) => unknown)>;

export function routed(routes: Routes, otherwise: unknown = []): (url: string) => Response {
  return (url) => {
    const prefix = Object.keys(routes).find((each) => url.startsWith(each));
    const answer = prefix === undefined ? otherwise : routes[prefix];
    return Response.json(typeof answer === 'function' ? (answer as (url: string) => unknown)(url) : answer);
  };
}

/** Stubs the global `fetch` with `routes`; the returned mock is what a suite asserts the reads on. */
export function stubFetch(routes: Routes, otherwise: unknown = []) {
  const respond = routed(routes, otherwise);
  const fetch = vi.fn(async (input: RequestInfo | URL) => respond(String(input)));
  vi.stubGlobal('fetch', fetch);
  return fetch;
}

import { vi } from 'vitest';
import { render } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// The views in SHELL mode share these: what the service and the driver answer, and a render with the
// providers every view expects. The bridge mock itself stays in each test file, because `vi.mock` is
// hoisted per file (MOD3, from `shell.test.tsx`).

export const DRIVER_STATE = { drivable: [], holds: [], running: ['s1a2b3c4'] };

/** What DAORIS.REMOTES answers: the wiring, with the key already reduced to its audit prefix. */
export const WIRING = {
  // Neutral by convention, like the driver's fixtures: a tracked file carries no machine path, not
  // even a plausible-looking one a scanner would have to be told to forgive.
  path: 'C:/somewhere/.daoris/remotes.json',
  fromEnvironment: false,
  remotes: [{ workspace: 'aurora', url: 'https://aurora.example.com', key: 'dk_abcd1234…' }],
};

export const REGISTRY = [
  { repository: 'engine', adopted: true, registered: true, summary: 'the engine', owns: [], accepts: [], packs: [], entries: 1 },
];
export const QUESTS = [{
  id: 'abc123', from: 'game', to: 'engine',
  title: 'Expose a streaming budget', body: 'World streaming needs a per-frame cap.',
  status: 'Open', filed: '2026-09-01T00:00:00Z', updated: '2026-09-01T00:00:00Z',
}];
export const SESSIONS = [{
  id: 's1a2b3c4', quest: 'abc123', repository: 'engine', adapter: 'claude-code', state: 'working',
  created: '2026-09-02T00:00:00Z', updated: '2026-09-02T01:00:00Z',
}];

/** What a SHARED deployment answers: counts, plus the commit its copy came from (D48 §6). */
export const REPOSITORIES = [{
  name: 'engine', total: 1, local: 1, canonical: 0, workspace: 'default',
  fed: {
    commit: 'c0ffee1234567890', shortCommit: 'c0ffee12',
    committedAt: new Date(Date.now() - 3 * 3600_000).toISOString(),
    branch: 'main', origin: 'person@machine-a',
  },
}];

/** What the service says answers search (D24) — the same answer every browser is given. */
export const STATUS = {
  semantic: false, tier: 'lexical only',
  note: 'Set DAORIS_EMBED_MODEL to enable semantic recall — it is what finds two repositories that reached the same conclusion in different words.',
};

export function respond(url: string): Response {
  if (url.startsWith('/api/status')) return Response.json(STATUS);
  if (url.startsWith('/api/sessions')) return Response.json(SESSIONS);
  if (url.startsWith('/api/quests')) return Response.json(QUESTS);
  if (url.startsWith('/api/registry')) return Response.json(REGISTRY);
  if (url.startsWith('/api/repositories')) return Response.json(REPOSITORIES);
  throw new Error(`unstubbed request: ${url}`);
}

/**
 * What went over the SERVICE while a shell-only surface rendered. The registry and the status are
 * the two things these surfaces may read from it — which circles this machine has, for choosing an
 * account per workspace, and which tier answers search, for Daoris's own AI (AGT6) — and both are
 * public, non-sensitive answers every browser is given. Everything else must be absent.
 */
export const serviceCalls = () =>
  vi.mocked(fetch).mock.calls.map((call) => String(call[0]))
    .filter((url) => !url.startsWith('/api/registry') && !url.startsWith('/api/status'));

export function show(node: React.ReactElement, client = new QueryClient({ defaultOptions: { queries: { retry: false } } })) {
  return render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>{node}</Tooltip.Provider>
    </QueryClientProvider>,
  );
}

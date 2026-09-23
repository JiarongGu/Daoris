import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';
import { MapView } from './MapView';
import { WorkspaceScopeProvider } from './scope';

// MAP2 (D67 §3): the workspace map, over a stubbed service — a view of its own, the owner's choice.

let REGISTRY: unknown[] = [];
const QUESTS = [
  { id: 'q1', from: 'game', to: 'engine', title: 'Expose a streaming budget', body: '', status: 'Open', filed: '', updated: '' },
  { id: 'q2', from: 'game', to: 'engine', title: 'Fix the loader', body: '', status: 'Done', filed: '', updated: '' },
  { id: 'q3', from: 'ask #abc', to: 'game', title: 'From an ask', body: '', status: 'Open', filed: '', updated: '' },
];
const CONVERGENCE = [
  { method: 'Convergent', similarity: 0.8, repositories: ['engine', 'game'], entries: [], suggestion: '' },
];
const SESSIONS = [
  { id: 's1', repository: 'engine', adapter: 'stub', state: 'working', created: '', updated: '' },
];

function respond(url: string): Response {
  if (url.startsWith('/api/registry')) return Response.json(REGISTRY);
  if (url.startsWith('/api/quests')) return Response.json(QUESTS);
  if (url.startsWith('/api/convergence')) return Response.json(CONVERGENCE);
  if (url.startsWith('/api/sessions')) return Response.json(SESSIONS);
  throw new Error(`unstubbed request: ${url}`);
}

function show(onOpenConvergence = () => {}) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <WorkspaceScopeProvider initial={null}>
          <MapView notify={() => {}} onOpenConvergence={onOpenConvergence} />
        </WorkspaceScopeProvider>
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

describe('the workspace map', () => {
  beforeEach(() => {
    REGISTRY = [
      { repository: 'engine', adopted: true, registered: true, summary: 'the engine', owns: ['rendering'], accepts: ['bugs'], packs: [], entries: 1 },
      { repository: 'game', adopted: true, registered: true, summary: 'the game', owns: [], accepts: [], packs: [], entries: 1 },
    ];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  });
  afterEach(() => vi.unstubAllGlobals());

  it('draws each repository, the quests between them, and what they learned alike', async () => {
    show();

    expect(await screen.findByRole('button', { name: 'engine, 1 open, a session working now' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'game, 1 open' })).toBeTruthy();
    expect(screen.getByRole('button', { name: '2 quests from game to engine' })).toBeTruthy();
    expect(screen.getByRole('button', { name: 'engine and game learned the same thing once' })).toBeTruthy();
    // The ask is not a repository: it is not invented as a node, and it is still counted.
    expect(screen.getByText(/1 quest comes from, or goes to, somewhere off this map/)).toBeTruthy();
    // The key says each line's meaning in words, not by hue alone (D41).
    expect(screen.getByText(/dashed line: the same thing learned in both/)).toBeTruthy();
  });

  it('a repository shows what it declares and the quests to and from it', async () => {
    show();

    await userEvent.click(await screen.findByRole('button', { name: /^engine, 1 open/ }));

    const detail = screen.getByText('quests to it').closest('div')!.parentElement!;
    expect(within(detail).getByText('rendering')).toBeTruthy();
    expect(within(detail).getByText('bugs')).toBeTruthy();
    expect(within(detail).getByText('Expose a streaming budget')).toBeTruthy();
    expect(within(detail).getByText('Fix the loader')).toBeTruthy();
    expect(within(detail).getByText('working now')).toBeTruthy();
  });

  it('a quest line lists the quests that went that way, each with its state in words', async () => {
    show();

    await userEvent.click(await screen.findByRole('button', { name: '2 quests from game to engine' }));

    expect(screen.getByText('game → engine')).toBeTruthy();
    expect(screen.getByText('Expose a streaming budget')).toBeTruthy();
    expect(screen.getByText('Done')).toBeTruthy();
  });

  it('a shared finding leads to Convergence, where the findings are', async () => {
    const open = vi.fn();
    show(open);

    await userEvent.click(await screen.findByRole('button', { name: 'engine and game learned the same thing once' }));
    await userEvent.click(screen.getByRole('button', { name: 'See them in Convergence' }));

    expect(open).toHaveBeenCalledOnce();
  });

  it('a keyboard reaches every part of it', async () => {
    show();
    const node = await screen.findByRole('button', { name: 'game, 1 open' });

    node.focus();
    await userEvent.keyboard('{Enter}');

    expect(node.getAttribute('aria-pressed')).toBe('true');
  });

  it('says so when the circle holds no repository', async () => {
    REGISTRY = [];
    show();

    expect(await screen.findByText('No repositories in this circle')).toBeTruthy();
  });
});

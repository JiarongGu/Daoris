import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';
import './i18n';
import { OverviewView } from './OverviewView';
import { WorkspaceScopeProvider } from './scope';
import { columnsFollow } from './test/mainSplit';

// Overview (D40), over a stubbed service, in a browser. Its page takes its final shape with FRAME1c: no
// list (D118 §4), and its two cards laid out by the main area's own width (§3b).

const REPOSITORIES = [{ name: 'engine', total: 3, local: 3, canonical: 0, workspace: 'default' }];
const REGISTRY = [{ repository: 'engine', adopted: true, registered: true, summary: 'the engine', owns: [], accepts: [], packs: [], entries: 3 }];

function respond(url: string): Response {
  if (url.startsWith('/api/repositories')) return Response.json(REPOSITORIES);
  if (url.startsWith('/api/registry')) return Response.json(REGISTRY);
  if (url.startsWith('/api/quests')) return Response.json([]);
  if (url.startsWith('/api/sessions')) return Response.json([]);
  if (url.startsWith('/api/asks')) return Response.json([]);
  throw new Error(`unstubbed request: ${url}`);
}

function show() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <WorkspaceScopeProvider initial={null}>
          <OverviewView onNavigate={() => {}} onOpenQuest={() => {}} notify={() => {}} />
        </WorkspaceScopeProvider>
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

describe('Overview', () => {
  beforeEach(() => vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input)))));
  afterEach(() => vi.unstubAllGlobals());

  /**
   * Audit OV3: *Outstanding* sat beside *Repositories by index size* from the viewport's 1024 px, so the
   * side bar narrowed both cards without the window changing. USE1 had moved the repository rows onto a
   * container query and left the grid on the viewport.
   */
  it('lays its two cards out by the main area\'s own width, never the viewport\'s', async () => {
    show();
    const card = (await screen.findByText('Outstanding — oldest first')).closest('section > div')!;

    expect(columnsFollow(card)).toEqual({ main: ['@4xl/main:grid-cols-2'], viewport: [] });
  });

  it('keeps its repository rows on their own card\'s width, as USE1 set them', async () => {
    show();
    const rows = (await screen.findByText('engine')).closest('ul')!;
    expect(rows).toHaveClass('@container');
  });
});

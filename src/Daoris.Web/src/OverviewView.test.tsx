import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';
import './i18n';
import { OverviewView } from './OverviewView';
import { WorkspaceScopeProvider } from './scope';
import { columnsFollow } from './test/mainSplit';
import type { Notify } from './ui';

// Overview (D40), over a stubbed service, in a browser. Its page takes its final shape with FRAME1c: no
// list (D118 §4), and its two cards laid out by the main area's own width (§3b). Since UX6c *What needs you* leads it,
// and settles from its rows what one press can.

const REPOSITORIES = [{ name: 'engine', total: 3, local: 3, canonical: 0, workspace: 'default' }];
const REGISTRY = [{ repository: 'engine', adopted: true, registered: true, summary: 'the engine', owns: [], accepts: [], packs: [], entries: 3 }];

let QUESTS: unknown[] = [];
let SESSIONS: unknown[] = [];
let ASKS: unknown[] = [];

/** An ask the declarations proposed and nobody has published (INT4a). */
const PROPOSED = {
  id: '7c1e9a04b2d5', workspace: 'default', sentence: 'Cap the hydration per frame.', state: 'Proposed',
  tier: 'declarations', asked: '2026-09-21T08:00:00Z', updated: '2026-09-21T08:00:00Z',
  links: [], attachments: [], quests: [], proposal: [{ repository: 'engine', score: 3, matched: ['frame'] }],
};

/** A done held for the person's yes, its second requirement departed (DRIFT1d2). */
const HELD = {
  id: '5e7a11', from: 'game', to: 'engine', title: 'Read the media field names from config', body: 'hard-coded.',
  status: 'Done', filed: '2026-09-20T00:00:00Z', updated: '2026-09-21T07:00:00Z', held: true,
  requirements: [{ quote: 'from config', check: 'a test' }],
  answers: [{ requirement: 1, departed: 'the names stay in code for now', quote: 'from config' }],
};

/** What the service answered each write, by its path, so a test reads what the row sent. */
const posted: { url: string; body: unknown }[] = [];

function respond(url: string, init?: RequestInit): Response {
  if (init?.method === 'POST') {
    posted.push({ url, body: init.body ? JSON.parse(String(init.body)) : null });
    if (url.endsWith('/publish')) return Response.json({ ask: { ...PROPOSED, state: 'Published' }, message: 'Published as quest `#9a8b7c` to `engine`.' });
    if (url.endsWith('/accept')) return Response.json({ quest: { ...HELD, held: false }, message: 'Accepted the departure on `#5e7a11`.' });
    if (url.includes('/go-aheads/')) return Response.json({ ask: PROPOSED, message: 'Go-ahead 1 on `#7c1e9a04b2d5` approved.' });
  }
  if (url.startsWith('/api/repositories')) return Response.json(REPOSITORIES);
  if (url.startsWith('/api/registry')) return Response.json(REGISTRY);
  if (url.startsWith('/api/quests')) return Response.json(QUESTS);
  if (url.startsWith('/api/sessions')) return Response.json(SESSIONS);
  if (url.startsWith('/api/asks')) return Response.json(ASKS);
  throw new Error(`unstubbed request: ${url}`);
}

function show(notify: Notify = () => {}) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <WorkspaceScopeProvider initial={null}>
          <OverviewView onNavigate={() => {}} onOpenQuest={() => {}} notify={notify} />
        </WorkspaceScopeProvider>
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

describe('Overview', () => {
  beforeEach(() => vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => respond(String(input), init))));
  afterEach(() => {
    vi.unstubAllGlobals();
    QUESTS = [];
    SESSIONS = [];
    ASKS = [];
    posted.length = 0;
  });

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
    const rows = (await screen.findAllByText('engine')).at(-1)!.closest('ul')!;
    expect(rows).toHaveClass('@container');
  });
});

/**
 * UX6c (design §6): *What needs you* is Overview's lead, in three groups, and settles from its rows what one press can.
 * Two asks sat proposed on the install until someone pressed through to publish them; now the row publishes.
 */
describe("Overview's lead, What needs you", () => {
  beforeEach(() => vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => respond(String(input), init))));
  afterEach(() => {
    vi.unstubAllGlobals();
    QUESTS = [];
    SESSIONS = [];
    ASKS = [];
    posted.length = 0;
  });

  it('comes first, above the tiles', async () => {
    ASKS = [PROPOSED];
    show();
    const band = await screen.findByRole('region', { name: 'What needs you' });
    const tile = screen.getByText('Adopted repositories');
    expect(band.compareDocumentPosition(tile) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  /** Never a card saying all clear (§6.1): one line, and what is running, which is the next thing a person asks. */
  it('is one line when nothing needs you, saying what is working', async () => {
    SESSIONS = [
      { id: 'w0rk1ng0', quest: 'q1', repository: 'engine', adapter: 'stub', kind: 'driven', state: 'working', created: '2026-09-21T09:00:00Z', updated: '2026-09-21T09:00:00Z' },
      { id: 'w0rk1ng1', quest: 'q2', repository: 'engine', adapter: 'stub', kind: 'driven', state: 'starting', created: '2026-09-21T09:00:00Z', updated: '2026-09-21T09:00:00Z' },
    ];
    show();
    const band = await screen.findByRole('region', { name: 'What needs you' });
    await waitFor(() => expect(within(band).getByText('2 sessions working')).toBeInTheDocument());
    expect(within(band).getByText('Nothing needs you')).toBeInTheDocument();
    expect(within(band).queryByRole('heading')).toBeNull();
    expect(within(band).queryByRole('list')).toBeNull();
  });

  /** A group with no rows is not shown (§6.2); a browser has no Sessions, so nothing is ever ready for review there. */
  it('shows the groups that hold rows, each named', async () => {
    ASKS = [PROPOSED];
    QUESTS = [HELD];
    show();
    const band = await screen.findByRole('region', { name: 'What needs you' });
    const word = await within(band).findByRole('group', { name: 'Waiting for your word' });
    expect(within(word).getByRole('listitem', { name: 'Cap the hydration per frame.' })).toBeInTheDocument();
    expect(within(word).getByRole('listitem', { name: 'Read the media field names from config' })).toBeInTheDocument();
    expect(within(band).queryByRole('group', { name: 'Holding work' })).toBeNull();
    expect(within(band).queryByRole('group', { name: 'Ready for you' })).toBeNull();
    expect(within(band).queryByText(/nothing yet keeps a record of what you have looked at/)).toBeNull();
  });

  /** §9.4: publishing an ask its declarations proposed is one press, on the ask's own route, its sentence said back. */
  it('publishes a proposed ask from its row, saying the service’s sentence', async () => {
    ASKS = [PROPOSED];
    const notify = vi.fn();
    show(notify);
    const row = await screen.findByRole('listitem', { name: 'Cap the hydration per frame.' });
    await userEvent.click(within(row).getByRole('button', { name: 'Publish to engine' }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith('Published as quest `#9a8b7c` to `engine`.'));
    expect(posted).toEqual([{ url: '/api/asks/7c1e9a04b2d5/publish', body: { to: 'engine' } }]);
  });

  it('accepts a departure from its row', async () => {
    QUESTS = [HELD];
    const notify = vi.fn();
    show(notify);
    const row = await screen.findByRole('listitem', { name: 'Read the media field names from config' });
    expect(within(row).getByText('Requirement 1 departed: the names stay in code for now')).toBeInTheDocument();
    await userEvent.click(within(row).getByRole('button', { name: 'Accept the departure' }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith('Accepted the departure on `#5e7a11`.'));
    expect(posted.map((one) => one.url)).toEqual(['/api/quests/5e7a11/accept']);
  });

  /** A go-ahead's yes reaches every session on its ask, so it asks once under the row before it is sent. */
  it('approves a go-ahead from its row once it has said what that hands every session', async () => {
    ASKS = [{
      ...PROPOSED, state: 'Published', quests: ['9a8b7c'],
      goAheads: [{
        number: 1, kind: 'push', on: 'prod', act: 'the menu entries', state: 'asked',
        asked: [{ session: 's1a2b3c4', quest: '9a8b7c', at: '2026-09-21T09:00:00Z', why: 'The menu ships with the release.' }],
      }],
    }];
    const notify = vi.fn();
    show(notify);
    const holding = await screen.findByRole('group', { name: 'Holding work' });
    const row = within(holding).getByRole('listitem', { name: 'push on prod: “the menu entries”' });
    await userEvent.click(within(row).getByRole('button', { name: 'Approve…' }));
    expect(posted).toEqual([]);
    await userEvent.click(within(row).getByRole('button', { name: 'Approve' }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith('Go-ahead 1 on `#7c1e9a04b2d5` approved.'));
    expect(posted).toEqual([{ url: '/api/asks/7c1e9a04b2d5/go-aheads/1', body: { answer: 'approved' } }]);
  });

  /** A refusal is the service's sentence, verbatim, and the row stays as it was. */
  it('says a refused publish in the service’s words', async () => {
    ASKS = [PROPOSED];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => (init?.method === 'POST'
      ? Response.json({ error: '`engine` is not an adopted repository in `default` — only an adopter can be asked.' }, { status: 400 })
      : respond(String(input), init))));
    const notify = vi.fn();
    show(notify);
    const row = await screen.findByRole('listitem', { name: 'Cap the hydration per frame.' });
    await userEvent.click(within(row).getByRole('button', { name: 'Publish to engine' }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      '`engine` is not an adopted repository in `default` — only an adopter can be asked.', 'error'));
    expect(within(row).getByRole('button', { name: 'Publish to engine' })).toBeEnabled();
  });
});

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';
import userEvent from '@testing-library/user-event';
import i18n from '../locales/i18n';
import { OverviewView } from './OverviewView';
import { WorkspaceScopeProvider } from '../data/scope';
import { columnsFollow } from '../test/mainSplit';
import type { Notify } from '../design/ui';

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
 * OVERVIEWNOISE1d: an empty index's card promised the next refresh and offered no way to start one, while the workspace's
 * repositories held thousands of entries stamped with the workspace they had moved from. It now offers the shell's refresh.
 */
describe("Overview's empty index", () => {
  beforeEach(() => vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) =>
    String(input).startsWith('/api/repositories') ? Response.json([]) : respond(String(input), init))));
  afterEach(async () => {
    vi.unstubAllGlobals();
    await i18n.changeLanguage('en');
  });

  function empty(props: { onRefresh?: () => void; refreshing?: boolean }) {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    return render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <WorkspaceScopeProvider initial={null}>
            <OverviewView onNavigate={() => {}} onOpenQuest={() => {}} notify={() => {}} {...props} />
          </WorkspaceScopeProvider>
        </Tooltip.Provider>
      </QueryClientProvider>,
    );
  }

  it('offers the refresh it names, and presses the shell\'s', async () => {
    const onRefresh = vi.fn();
    empty({ onRefresh });

    await screen.findByText('The index holds nothing yet');
    await userEvent.click(screen.getByRole('button', { name: 'Refresh the index' }));

    expect(onRefresh).toHaveBeenCalledTimes(1);
  });

  it('waits on a refresh that is running', async () => {
    empty({ onRefresh: vi.fn(), refreshing: true });

    expect(await screen.findByRole('button', { name: 'Refreshing the index…' })).toBeDisabled();
  });

  it('offers nothing where no refresh was handed to it', async () => {
    empty({});

    await screen.findByText('The index holds nothing yet');
    expect(screen.queryByRole('button', { name: 'Refresh the index' })).not.toBeInTheDocument();
  });

  it('says it in Chinese', async () => {
    await i18n.changeLanguage('zh');
    empty({ onRefresh: vi.fn() });

    await screen.findByText('索引里还什么都没有');
    expect(screen.getByRole('button', { name: '刷新索引' })).toBeEnabled();
  });
});

/**
 * UX6c (design §6): *What needs you* is Overview's lead. Who needs the person is judged by the app (PUSH1d4, D182 point 6), so a
 * plain browser has no rows to show: its band says it is the app's. `AttentionBand.test.tsx` holds its rows and their acts, in a
 * shell.
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

  /**
   * The owner's decision (PUSH1d4): a plain browser judges nothing, so it lists no row, even with an ask proposed and a done held
   * at the service, and says the band is the app's. Never *Nothing needs you*, which would be a claim it cannot make.
   */
  it('says in a browser that what needs you is the app’s, listing no row', async () => {
    ASKS = [PROPOSED];
    QUESTS = [HELD];
    show();
    const band = await screen.findByRole('region', { name: 'What needs you' });

    expect(within(band).getByText('What needs you is judged by the Daoris app: open the app to see what waits for your word.')).toBeInTheDocument();
    expect(within(band).queryByRole('listitem')).toBeNull();
    expect(within(band).queryByText('Nothing needs you')).toBeNull();
  });

  // FABRIC-CLOSE1d2: an outstanding quest reads as the quest's page does, timed by its submission.
  it('says submitted, timed by the submission, and source reopened; a quest no closure holds reads taken as before', async () => {
    const hoursAgo = (count: number) => new Date(Date.now() - count * 3_600_000).toISOString();
    const base = { from: 'game', to: 'engine', body: '', status: 'Taken', filed: hoursAgo(30), updated: hoursAgo(20) };
    QUESTS = [
      { ...base, id: 'aa11aa', title: 'Submitted one', closure: 'gph-1', submission: { machine: 'desk', sequence: 1, at: hoursAgo(2) } },
      { ...base, id: 'bb22bb', title: 'Reopened one', hold: 'source-reopened' },
      { ...base, id: 'cc33cc', title: 'Plain one' },
    ];
    show();
    const row = async (title: string) => (await screen.findByText(title)).closest('button')!;

    expect(await row('Submitted one')).toHaveTextContent(/^submittedSubmitted one.*submitted 2h ago/);
    expect(await row('Reopened one')).toHaveTextContent(/^takensource reopenedReopened one.*taken 20h ago/);
    const plain = await row('Plain one');
    expect(plain).toHaveTextContent(/^takenPlain one.*taken 20h ago/);
    expect(plain).not.toHaveTextContent(/submitted|source reopened/);
  });
});

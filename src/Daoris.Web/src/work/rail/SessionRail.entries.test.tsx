import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// SESSONE1c (D167 point 5): the rail over the driver's `entries`. The owner raised two asks, one over two repositories, and
// the list showed eleven records; it shows three lines of work. An organism's test, so the bridge is mocked as present.

const { invoke, notifyReady, bridge } = vi.hoisted(() => ({
  invoke: vi.fn(),
  notifyReady: vi.fn(() => Promise.resolve()),
  bridge: { available: true },
}));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => bridge.available,
  getBridge: () => ({ isAvailable: bridge.available, invoke, notifyReady }),
  useShenora: () => ({ isAvailable: bridge.available, bridge: { notifyReady } }),
  useShenoraEvent: () => {},
}));

import { EVIDENCE_ASK_RECORDS, EVIDENCE_ENTRIES, EVIDENCE_GROUPINGS, EVIDENCE_QUESTS, EVIDENCE_SESSIONS } from '../entries.fixtures';
import { SessionRail } from './SessionRail';

let ENTRIES: unknown = EVIDENCE_ENTRIES;
let SESSIONS: unknown[] = EVIDENCE_SESSIONS;
let GROUPINGS: unknown[] = EVIDENCE_GROUPINGS;

/** What is unread on each ask's channel (SESSONE2c), by the ask's id; an ask not named reads as an empty channel. */
let UNREAD: Record<string, number> = {};

const drive = async (_module: string, type: string, options?: { payload?: { ask?: string } }) => {
  if (type === 'CHANNEL_READ') {
    const ask = options?.payload?.ask ?? '';
    return { ask, entry: 'person', items: [], unread: UNREAD[ask] ?? 0, mark: null };
  }
  if (type === 'SESSION_GROUPS') return { sessions: GROUPINGS, ...(ENTRIES === undefined ? {} : { entries: ENTRIES }) };
  if (type === 'ASK_LANDING') {
    return { ask: 'c98a7e', entries: [{ entry: 'ask:c98a7e/app', repository: 'app', form: 'branch', target: 'feature/c98a7e', parts: [
      { session: 'ap000002', target: 'feature/c98a7e' }, { session: 'ap000003', target: 'feature/c98a7e' },
    ] }] };
  }
  if (type === 'LAND_ASK') return { ask: 'c98a7e', done: true, repositories: [{ entry: 'ask:c98a7e/app', repository: 'app', done: true, message: 'Landed 2 part(s).', parts: [] }] };
  return { drivable: ['app'], holds: [], trees: [], running: [] };
};

function respond(url: string): Response {
  if (url.startsWith('/api/sessions')) return Response.json(SESSIONS);
  if (url.startsWith('/api/quests')) return Response.json(EVIDENCE_QUESTS);
  if (url.startsWith('/api/asks')) return Response.json(EVIDENCE_ASK_RECORDS);
  if (url.startsWith('/api/registry')) return Response.json([]);
  throw new Error(`unstubbed request: ${url}`);
}

function show(props: Partial<Parameters<typeof SessionRail>[0]> = {}) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider><SessionRail notify={() => {}} {...props} /></Tooltip.Provider>
    </QueryClientProvider>,
  );
}

/** The session rows of a region: its buttons less each row's menu trigger. */
const rowsOf = (region: HTMLElement) => within(region).getAllByRole('button')
  .filter((button) => !button.getAttribute('aria-label')?.startsWith('more for'));

describe('the session rail over entries (SESSONE1c)', () => {
  beforeEach(() => {
    UNREAD = {};
    ENTRIES = EVIDENCE_ENTRIES;
    SESSIONS = EVIDENCE_SESSIONS;
    GROUPINGS = EVIDENCE_GROUPINGS;
    bridge.available = true;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(drive);
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
  });

  /** The owner's words: "I raised 2 tasks, with 1 task needing 2 repos to co-work, so there should be just 3 sessions". */
  it('shows the owner\'s two asks as three sessions, not eleven records', async () => {
    show();

    const working = (await screen.findByRole('heading', { level: 3, name: 'Working (2)' })).closest('section')!;
    const ended = (await screen.findByRole('heading', { level: 3, name: 'Ended (1)' })).closest('section')!;
    expect(rowsOf(working)).toHaveLength(2);
    expect(rowsOf(ended)).toHaveLength(1);
    // The two intakes, ended, are their asks' page's: no heading holds them.
    expect(screen.queryByText(/^Intake/)).toBeNull();
  });

  it('draws each row from its leading part and says how many parts it holds', async () => {
    show();

    const working = (await screen.findByRole('heading', { level: 3, name: 'Working (2)' })).closest('section')!;
    // The row of an ask's line is named by its ask, with its repository and its parts on its line.
    // The asks arrive after the list does: the name is theirs once they have.
    await waitFor(() => expect(rowsOf(working)[0]).toHaveTextContent('Add the export feed to the app, backed by the database'));
    const [first, second] = rowsOf(working);
    expect(first).toHaveTextContent(/app · .*3 parts/);
    expect(second).toHaveTextContent(/db · .*3 parts/);
    expect(first).toHaveTextContent('working');
  });

  it('marks the entry attended while any of its parts is', async () => {
    show({ selected: 'ap000001' });

    const working = (await screen.findByRole('heading', { level: 3, name: 'Working (2)' })).closest('section')!;
    const [first, second] = rowsOf(working);
    expect(first).toHaveAttribute('aria-current', 'true');
    expect(second).not.toHaveAttribute('aria-current');
  });

  it('presses to the leading part, whose page its parts strip then lists', async () => {
    const select = vi.fn();
    show({ onSelect: select });

    const working = (await screen.findByRole('heading', { level: 3, name: 'Working (2)' })).closest('section')!;
    await userEvent.click(rowsOf(working)[0]!);

    expect(select).toHaveBeenCalledWith('ap000003');
  });

  it('lists the records as it always did where the host answers no entries', async () => {
    ENTRIES = undefined;
    show();

    const ended = (await screen.findByRole('heading', { level: 3, name: /^Ended/ })).closest('section')!;
    expect(rowsOf(ended).length).toBeGreaterThan(5);
  });

  describe('Group by ask', () => {
    it('lists each ask with its entries, naming their repositories', async () => {
      show({ arrangement: 'ask' });

      const first = (await screen.findByRole('heading', { level: 3, name: 'Add the export feed to the app, backed by the database' })).closest('section')!;
      const second = screen.getByRole('heading', { level: 3, name: 'Tidy the settings page' }).closest('section')!;
      const inFirst = rowsOf(first);
      expect(inFirst).toHaveLength(2);
      expect(inFirst[0]).toHaveTextContent('app');
      expect(inFirst[1]).toHaveTextContent('db');
      expect(rowsOf(second)).toHaveLength(1);
      expect(second).toHaveTextContent('app');
    });

    /** SESSONE2c (D167): an ask's heading says what is unread on its channel, the person's count, and says nothing where none is. */
    it('says what is unread on an ask’s channel in its heading', async () => {
      UNREAD = { c98a7e: 2 };
      show({ arrangement: 'ask' });

      expect(await screen.findByRole('heading', { level: 3, name: 'Add the export feed to the app, backed by the database · 2 unread' })).toBeInTheDocument();
      expect(screen.getByRole('heading', { level: 3, name: 'Tidy the settings page' })).toBeInTheDocument();
    });

    /**
     * ACCEPTALL1 (D167's 2026-10-15 amendment point 4): an ask whose lines of work hold more than one part with work to land offers
     * one Accept under its heading; its plan is read only once the ask opens, and the press lands the ask's work.
     */
    it("offers one Accept for an ask's work, reading its plan only once asked", async () => {
      const lands = (session: string) => ({ branch: `daoris/s-${session}`, tree: `s-${session}`, commits: 1, uncommitted: 0 });
      GROUPINGS = EVIDENCE_GROUPINGS.map((row) => (row.session === 'ap000002' || row.session === 'db000001' ? { ...row, lands: lands(row.session) } : row));
      show({ arrangement: 'ask' });

      const first = (await screen.findByRole('heading', { level: 3, name: 'Add the export feed to the app, backed by the database' })).closest('section')!;
      const press = await within(first).findByRole('button', { name: "Accept the ask's 2 parts…" });
      expect(within(screen.getByRole('heading', { level: 3, name: 'Tidy the settings page' }).closest('section')!)
        .queryByRole('button', { name: /^Accept the ask's/ })).toBeNull();
      // The two rows are still the list's, and the press is not one of them.
      expect(rowsOf(first).filter((button) => !/^Accept/.test(button.textContent ?? ''))).toHaveLength(2);
      expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'ASK_LANDING', expect.anything());

      await userEvent.click(press);
      expect(await within(first).findByText('Lands 2 parts on feature/c98a7e, one after another, in the order they ran.')).toBeInTheDocument();
      expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'ASK_LANDING', { payload: { ask: 'c98a7e' } });

      await userEvent.click(within(first).getByRole('button', { name: 'Accept all' }));
      await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'LAND_ASK', { payload: { ask: 'c98a7e' }, timeoutMs: expect.any(Number) }));
    });

    it('puts a conversation under Not on an ask', async () => {
      SESSIONS = [...EVIDENCE_SESSIONS, {
        id: 'ch000001', repository: 'app', state: 'completed', adapter: 'claude-code', kind: 'chat', quest: null,
        created: new Date().toISOString(), updated: new Date().toISOString(),
      }];
      ENTRIES = [...EVIDENCE_ENTRIES, {
        entry: 'session:ch000001/app', origin: 'session:ch000001', repository: 'app', group: 'ended', leading: 'ch000001',
        parts: [{ session: 'ch000001', quest: null, shown: 'completed', group: 'ended', began: 'opened', after: null, askedBy: null }],
      }];
      show({ arrangement: 'ask' });

      const loose = (await screen.findByRole('heading', { level: 3, name: 'Not on an ask' })).closest('section')!;
      expect(rowsOf(loose)).toHaveLength(1);
    });
  });

  /** D167 point 5 and D126 §5.2: archive marks an entry's parts, so no part comes back as a row of its own. */
  describe('archive', () => {
    it('sends every part of the entry to be archived from its row', async () => {
      invoke.mockImplementation(async (module: string, type: string) => (type === 'SESSION_ARCHIVE'
        ? { archived: [], kept: [] }
        : drive(module, type)));
      show();

      const ended = (await screen.findByRole('heading', { level: 3, name: 'Ended (1)' })).closest('section')!;
      const user = userEvent.setup();
      within(ended).getAllByRole('button', { name: /^more for / })[0]!.focus();
      await user.keyboard('{Enter}');
      await user.click(screen.getByRole('menuitem', { name: 'Archive' }));

      await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_ARCHIVE', {
        payload: { ids: ['ap000011', 'ap000012', 'ap000013'], archived: true },
      }));
    });
  });
});

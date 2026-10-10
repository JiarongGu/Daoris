import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';
import {
  chooseRow, makeFromList, moreAct, openFilters, QuestsView, questList, questMain, questPage,
} from './test/questsView';

// The view over a stubbed service, on the frame (FRAME1d, D118): its list pane holds the asks and the quests, its
// main area the chosen record, and the composers are drawers. The shapes are what the real endpoints return, without
// a host. The Playwright loop owns the real end-to-end; this owns the view's own logic at millisecond speed.

// What a quest carries (D65 §2): a link, a file kept on this machine (it has a path — which the
// page must never SHOW), and a file named on the record whose bytes stayed where it was published.
const KEPT_PATH = 'C:/somewhere/data/quests/abc123/attachments/ab12cd34ef56-before.png';
const QUESTS = [{
  id: 'abc123', from: 'game', to: 'engine',
  title: 'Expose a streaming budget', body: 'World streaming needs a per-frame cap.',
  status: 'Open', filed: '2026-09-01T00:00:00Z', updated: '2026-09-01T00:00:00Z',
  links: ['https://tickets.example/T-1'],
  attachments: [
    { name: 'before.png', sha256: `ab12cd34ef56${'0'.repeat(52)}`, bytes: 2048, path: KEPT_PATH },
    { name: 'trace.log', sha256: `cd34${'1'.repeat(60)}`, bytes: 300 },
  ],
  // A step of a chain (D65 §4): it follows one quest, and its close publishes the next.
  parent: 'f0f0f0',
  then: [{ to: 'game', title: 'Report on {parent}', body: 'Say what was done.' }],
}];

const REGISTRY = [
  { repository: 'engine', adopted: true, registered: true, summary: 'the engine', owns: [], accepts: [], packs: [], entries: 1 },
  { repository: 'game', adopted: true, registered: true, summary: 'the game', owns: [], accepts: [], packs: [], entries: 1 },
];

// A driven session's record, attached to the quest above (D46): active, so the row wears its state.
// TWO records for the one quest, deliberately — a retry is its own record, and the view must show
// where things stand now (the later one), not the failed first attempt.
const SESSIONS = [{
  id: 's0f1r2s3', quest: 'abc123', repository: 'engine', adapter: 'stub', state: 'failed',
  note: 'the first attempt died', created: '2026-09-01T22:00:00Z', updated: '2026-09-01T23:00:00Z',
}, {
  id: 's1a2b3c4', quest: 'abc123', repository: 'engine', adapter: 'stub', state: 'working',
  note: 'the process is alive', evidence: 'commits landed:\nfff000 stub: answer quest abc123',
  created: '2026-09-02T00:00:00Z', updated: '2026-09-02T01:00:00Z',
}];

/** The asks the service holds (INT4c) — none, unless a test puts some there. */
let ASKS: unknown[] = [];

function respond(url: string): Response {
  if (url.startsWith('/api/sessions')) return Response.json(SESSIONS);
  if (url.startsWith('/api/quests')) return Response.json(QUESTS);
  if (url.startsWith('/api/registry')) return Response.json(REGISTRY);
  if (url.startsWith('/api/asks')) return Response.json(ASKS);
  throw new Error(`unstubbed request: ${url}`);
}

/**
 * The view as the app holds it: a draft handed in is an EVENT, consumed through `onOpened` — so the
 * holder clears it, exactly as `App` does, or the composer would reopen on every render.
 */
function Held({ opening, notify, door }: { opening: { from?: string; to?: string } | null; notify: () => void; door?: string }) {
  const [pending, setPending] = useState(opening);
  return <QuestsView notify={notify} opening={pending} onOpened={() => setPending(null)} door={door} />;
}

function view(opening: { from?: string; to?: string } | null = null, notify = vi.fn(), door?: string) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <Held opening={opening} notify={notify} door={door} />
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
  return notify;
}

/** Quests opened by a door that names `item` (D118 §3i), which the list chooses now. */
const openedOn = (item: string) => view(null, vi.fn(), item);

/** The main area with nothing chosen, found afresh as it settles: it is a new element from loading to its notice. */
const nothingChosen = () => waitFor(() => expect(questMain()).toHaveTextContent('Choose a quest or an ask'));

/** The body the last publish sent — what the local host would have been asked to keep. */
let published: {
  links?: string[]; attachments?: { name: string; content: string }[];
  then?: { to: string; title: string; body: string }[];
  short?: string;
} | null = null;

describe('QuestsView', () => {
  beforeEach(() => {
    published = null;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      if (init?.method === 'POST' && String(input) === '/api/quests') {
        published = JSON.parse(String(init.body));
        return Response.json({ quest: QUESTS[0], message: 'Published quest `#abc123` to `engine` — Open.' });
      }
      return respond(String(input));
    }));
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    // What the list chose and how it was filtered is remembered per viewer (D118 §3f), so each test starts afresh.
    window.localStorage.clear();
  });

  /**
   * 🔴 SURF6b's door — "send it back as a quest" — hands the composer a draft, and consuming it by
   * setting state during render looped until React gave up: the door crashed the view it opened.
   */
  it('a draft handed in by a door opens the composer on it, once', async () => {
    view({ from: 'game', to: 'engine' });
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByRole('heading', { name: 'New quest' })).toBeInTheDocument();

    // The draft LANDED: publish needs both repositories, and the person has chosen neither.
    fireEvent.change(within(dialog).getByLabelText('What is wanted, in one line'), { target: { value: 'An ask' } });
    fireEvent.change(within(dialog).getByLabelText('Why, and the evidence'), { target: { value: 'Its reason.' } });
    expect(within(dialog).getByRole('button', { name: 'Publish quest' })).toBeEnabled();
  });

  // ——— The frame (FRAME1d, D118 §2): a list pane and a main area; a record is the main area, a form a drawer.

  it('says how to choose with nothing chosen, and offers the ＋\'s two kinds, Ask first', async () => {
    view();
    expect(await within(questList()).findByText('Expose a streaming budget')).toBeInTheDocument();

    const main = questMain();
    expect(within(main).getByText('Choose a quest or an ask')).toBeInTheDocument();
    expect(within(main).getAllByRole('button').map((button) => button.textContent)).toEqual(['Ask', 'New quest']);
    await userEvent.click(within(main).getByRole('button', { name: 'New quest' }));
    expect(await screen.findByRole('dialog', { name: 'New quest' })).toBeInTheDocument();
  });

  it('opens a chosen quest on the page beside the list, with no drawer, and remembers the choice', async () => {
    view();
    const page = await chooseRow('Expose a streaming budget');

    expect(within(page).getByRole('heading', { level: 1, name: 'Expose a streaming budget' })).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).toBeNull();
    // The list is still there beside it, its row chosen.
    expect(within(questList()).getByRole('button', { name: /Expose a streaming budget/ })).toHaveAttribute('aria-current', 'true');
    expect(window.localStorage.getItem('daoris.list.quests.chosen')).toBe('abc123');
  });

  it('offers Ask first, then New quest, from the list\'s ＋, each opening its composer in a drawer', async () => {
    view();
    await within(questList()).findByText('Expose a streaming budget');

    await makeFromList('Ask');
    expect(await screen.findByRole('dialog', { name: 'Ask the workspace' })).toBeInTheDocument();
    await userEvent.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());

    await makeFromList('New quest');
    expect(await screen.findByRole('dialog', { name: 'New quest' })).toBeInTheDocument();
  });

  it('says a quest chosen now that is no longer here has gone, rather than showing nothing', async () => {
    openedOn('f1f1f1');

    expect(await within(questMain()).findByText('This quest is no longer here')).toBeInTheDocument();
  });

  // UX6b (design §1 rule 6): a remembered choice ends with what it chose. The install reopened yesterday's done quest.
  it('opens with nothing chosen on a remembered quest that has gone, never on its gone state, and forgets it', async () => {
    window.localStorage.setItem('daoris.list.quests.chosen', 'f1f1f1');
    view();

    await nothingChosen();
    expect(within(questMain()).queryByText('This quest is no longer here')).toBeNull();
    expect(window.localStorage.getItem('daoris.list.quests.chosen')).toBeNull();
  });

  it('opens with nothing chosen on a remembered quest that closed', async () => {
    window.localStorage.setItem('daoris.list.quests.chosen', 'd0d0d0');
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/quests')) {
        return Response.json(url.includes('includeClosed=true') ? [...QUESTS, { ...QUESTS[0], id: 'd0d0d0', title: 'An old one', status: 'Done' }] : QUESTS);
      }
      return respond(url);
    }));
    view();

    await nothingChosen();
    expect(screen.queryByRole('heading', { level: 1, name: 'An old one' })).toBeNull();
    expect(window.localStorage.getItem('daoris.list.quests.chosen')).toBeNull();
  });

  it('reopens a remembered quest that still waits', async () => {
    window.localStorage.setItem('daoris.list.quests.chosen', 'abc123');
    view();

    expect(await questPage('Expose a streaming budget')).toBeInTheDocument();
    expect(window.localStorage.getItem('daoris.list.quests.chosen')).toBe('abc123');
  });

  // ——— The list's filters (D118 §2, §3f): in its ⋯, and remembered.

  it('filters the quests to one receiver from the ⋯, says so at the list\'s head, and remembers it', async () => {
    const asked: string[] = [];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/quests')) asked.push(url);
      return respond(url);
    }));
    view();
    await within(questList()).findByText('Expose a streaming budget');

    const user = await openFilters();
    expect(screen.getByRole('menuitemradio', { name: 'Everyone' })).toHaveAttribute('aria-checked', 'true');
    await user.click(screen.getByRole('menuitemradio', { name: 'engine' }));

    await waitFor(() => expect(asked).toContain('/api/quests?includeClosed=false&repository=engine'));
    expect(within(questList()).getByText('Showing quests to engine')).toBeInTheDocument();
    expect(JSON.parse(window.localStorage.getItem('daoris.list.quests.filters')!)).toEqual({ to: 'engine' });
  });

  it('includes closed quests from the ⋯ in a group of their own, and remembers it', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/quests')) {
        return Response.json(url.includes('includeClosed=true') ? [...QUESTS, { ...QUESTS[0], id: 'd0d0d0', title: 'An old one', status: 'Done' }] : QUESTS);
      }
      return respond(url);
    }));
    view();
    await within(questList()).findByText('Expose a streaming budget');
    expect(within(questList()).queryByText('An old one')).toBeNull();

    const user = await openFilters();
    await user.click(screen.getByRole('menuitemcheckbox', { name: 'Include closed' }));

    expect(await within(questList()).findByText('Closed (1)')).toBeInTheDocument();
    expect(within(questList()).getByText('An old one')).toBeInTheDocument();
    expect(JSON.parse(window.localStorage.getItem('daoris.list.quests.filters')!)).toEqual({ closed: true });
  });

  it('reads the filters it remembered at the next start', async () => {
    window.localStorage.setItem('daoris.list.quests.filters', JSON.stringify({ to: 'game', closed: true }));
    view();

    expect(await within(questList()).findByText('Showing quests to game')).toBeInTheDocument();
    const user = await openFilters();
    expect(screen.getByRole('menuitemradio', { name: 'game' })).toHaveAttribute('aria-checked', 'true');
    expect(screen.getByRole('menuitemcheckbox', { name: 'Include closed' })).toHaveAttribute('aria-checked', 'true');
    await user.keyboard('{Escape}');
  });

  // ——— A quest's lanes (D115 §2.2, DEV4): `to` stays the repository, and its lanes are shown beside it.

  const LANED = [{ ...QUESTS[0], lanes: ['assets', 'core'] }];
  const LANED_REGISTRY = [{
    ...REGISTRY[0],
    lanes: [
      { id: 'core', title: 'Core', summary: 'The runtime.', steward: false },
      { id: 'assets', title: 'Assets', summary: 'The pipeline.', steward: false },
    ],
  }, REGISTRY[1]];

  function stubLaned() {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/quests')) return Response.json(LANED);
      if (url.startsWith('/api/registry')) return Response.json(LANED_REGISTRY);
      return respond(url);
    }));
  }

  it('shows the lanes a quest addresses beside its repository, on its row and on its page', async () => {
    stubLaned();
    view();

    expect(await within(questList()).findByText('lanes assets + core')).toBeInTheDocument();
    const page = await chooseRow('Expose a streaming budget');
    // In its Details (UX7c, D152 §7). A field's name is sentence case (the glossary's `field` kind, NAME1a).
    await userEvent.click(within(page).getByRole('button', { name: /^Details/ }));
    expect(within(page).getByText('Lanes')).toBeInTheDocument();
    // Named as the repository declares them, where its registration says.
    expect(within(page).getByText('assets (Assets), core (Core)')).toBeInTheDocument();
  });

  it('a quest to the whole repository shows no lanes', async () => {
    view();
    const page = await chooseRow('Expose a streaming budget');

    expect(screen.queryByText(/^lanes? /)).toBeNull();
    expect(within(page).queryByText('Lanes')).toBeNull();
  });

  it('says a quest\'s lanes in 中文, the lanes\' own names left as they are', async () => {
    const { default: i18n } = await import('./i18n');
    await i18n.changeLanguage('zh');
    try {
      stubLaned();
      view();
      expect(await within(questList()).findByText('泳道 assets + core')).toBeInTheDocument();
      const page = await chooseRow('Expose a streaming budget');
      await userEvent.click(within(page).getByRole('button', { name: /^详情/ }));
      expect(within(page).getByText('泳道')).toBeInTheDocument();
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  // ——— A conflict (D68 §5, SYNC6b): kept on the quest for a person, and reachable from the status bar.

  it('the page shows each move that lost the race, in its own words', async () => {
    const conflicted = [{
      ...QUESTS[0], status: 'Taken',
      conflicts: [{ machine: 'b7f2c9d1', attempted: 'Taken', note: 'machine b, offline', at: '2026-09-02T00:00:00Z' }],
    }];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) =>
      String(input).startsWith('/api/quests') ? Response.json(conflicted) : respond(String(input))));
    view();
    const conflicts = within(await chooseRow('Expose a streaming budget')).getByRole('region', { name: 'Conflicts' });

    expect(within(conflicts).getByText(/Machine b7f2c9d1 tried to mark it taken/)).toBeInTheDocument();
    expect(within(conflicts).getByText('machine b, offline')).toBeInTheDocument();
    expect(within(conflicts).getByText(/nothing was merged/)).toBeInTheDocument();
  });

  /**
   * SYNC6c: a person dismisses a conflict by name — the machine and sequence of the move that lost —
   * and the service's sentence is what they are told. The dismissal travels with the next pass.
   */
  it('dismisses a conflict by name and says what the service answered', async () => {
    const conflicted = [{
      ...QUESTS[0], status: 'Taken',
      conflicts: [{ machine: 'b7f2c9d1', sequence: 12, attempted: 'Taken', note: 'machine b, offline', at: '2026-09-02T00:00:00Z' }],
    }];
    let dismissed: unknown = null;
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (init?.method === 'POST' && url === '/api/quests/abc123/conflicts/dismiss') {
        dismissed = JSON.parse(String(init.body));
        return Response.json({
          quest: { ...conflicted[0], conflicts: [] },
          message: 'Dismissed one conflict on quest `#abc123`; every machine drops it on its next sync.',
        });
      }
      return url.startsWith('/api/quests') ? Response.json(conflicted) : respond(url);
    }));
    const notify = view();
    const conflicts = within(await chooseRow('Expose a streaming budget')).getByRole('region', { name: 'Conflicts' });

    await userEvent.click(within(conflicts).getByRole('button', { name: /dismiss/i }));

    await waitFor(() => expect(dismissed).toEqual({ machine: 'b7f2c9d1', sequence: 12 }));
    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      'Dismissed one conflict on quest `#abc123`; every machine drops it on its next sync.'));
    // The page shows the quest as the answer left it, before the list catches up.
    await waitFor(() => expect(within(questMain()).queryByRole('region', { name: 'Conflicts' })).toBeNull());
  });

  // ——— Deleting a quest made by mistake (QUEST1, D95): offered only where the service says it may go,
  // asked once, and answered in the service's own words.

  describe('deleting a quest', () => {
    const DELETABLE = [{ ...QUESTS[0], deletable: true }];
    let deleted: string[] = [];

    function stub(answer: () => Response) {
      deleted = [];
      vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
        const url = String(input);
        if (init?.method === 'DELETE') {
          deleted.push(url);
          return answer();
        }
        return url.startsWith('/api/quests') ? Response.json(DELETABLE) : respond(url);
      }));
    }

    it('offers no delete on a quest the service does not say may go', async () => {
      view();
      const page = await chooseRow('Expose a streaming budget');

      expect(within(page).queryByRole('button', { name: 'Delete…' })).toBeNull();
      within(page).getByRole('button', { name: 'More actions' }).focus();
      await userEvent.setup().keyboard('{Enter}');
      expect(await screen.findByRole('menuitem', { name: 'Copy quest ID' })).toBeInTheDocument();
      expect(screen.queryByRole('menuitem', { name: 'Delete…' })).toBeNull();
    });

    it('asks once under the header, then deletes, and says what the service answered, verbatim', async () => {
      stub(() => Response.json({ id: 'abc123', message: 'Deleted quest `#abc123` — it never left this machine, so nothing else holds a copy.' }));
      const notify = view();
      const page = await chooseRow('Expose a streaming budget');

      await moreAct(page, 'Delete…');
      const confirm = within(page).getByRole('group', { name: 'delete this quest' });
      expect(within(confirm).getByText(/cannot be undone/)).toBeInTheDocument();
      expect(deleted).toEqual([]);

      await userEvent.click(within(confirm).getByRole('button', { name: 'Delete quest' }));

      await waitFor(() => expect(deleted).toEqual(['/api/quests/abc123']));
      await waitFor(() => expect(notify).toHaveBeenCalledWith(
        'Deleted quest `#abc123` — it never left this machine, so nothing else holds a copy.'));
      // Gone, so the list chooses nothing, and the main area says how to choose.
      expect(await within(questMain()).findByText('Choose a quest or an ask')).toBeInTheDocument();
      expect(window.localStorage.getItem('daoris.list.quests.chosen')).toBeNull();
      // UXFIX2c: the ⋯ it was asked from went with the quest, so the focus goes to the main area that held it, never the
      // page's body.
      expect(questMain()).toHaveFocus();
    });

    /**
     * A refusal reaches the person in the service's words, and since UXFIX2 inside the ask where they pressed, which stays
     * open, rather than in a toast after it had closed on the press (the second-opinion review, `QuestPage.tsx:396`).
     */
    it('a refusal reaches the person in the service\'s words inside the ask, and the quest stays on its page', async () => {
      const refusal = 'Quest `#abc123` is open, but session `s1a2b3c4` was started for it and its record names the quest, so it stays. Decline it instead, with the reason, and the asker hears why.';
      stub(() => Response.json({ error: refusal }, { status: 409 }));
      const notify = view();
      const page = await chooseRow('Expose a streaming budget');

      await moreAct(page, 'Delete…');
      await userEvent.click(within(page).getByRole('button', { name: 'Delete quest' }));

      const confirm = within(page).getByRole('group', { name: 'delete this quest' });
      expect(await within(confirm).findByRole('alert')).toHaveTextContent(refusal.replaceAll('`', ''));
      expect(notify).not.toHaveBeenCalledWith(refusal, 'error');
      expect(screen.getByRole('heading', { level: 1, name: 'Expose a streaming budget' })).toBeInTheDocument();
    });

    it('never mind leaves the quest where it was, asks nothing of the service, and gives the focus back to the ⋯', async () => {
      stub(() => Response.json({ id: 'abc123', message: 'Deleted.' }));
      view();
      const page = await chooseRow('Expose a streaming budget');

      await moreAct(page, 'Delete…');
      // UXFIX2: what goes takes the focus, and describes *Delete quest*.
      const says = await within(page).findByText(/cannot be undone/);
      await waitFor(() => expect(says).toHaveFocus());
      expect(within(page).getByRole('button', { name: 'Delete quest' })).toHaveAccessibleDescription(says.textContent!);
      await userEvent.click(within(page).getByRole('button', { name: 'Never mind' }));

      expect(within(page).queryByRole('group', { name: 'delete this quest' })).toBeNull();
      expect(within(page).getByRole('button', { name: 'More actions' })).toHaveFocus();
      await moreAct(page, 'Delete…');
      expect(within(page).getByRole('group', { name: 'delete this quest' })).toBeInTheDocument();
      expect(deleted).toEqual([]);
    });
  });

  // ——— The person's yes to a done's departure (DRIFT1d2, D133 §4): listed first while it waits on them, one press on its
  // page, through the service's accept door, and answered in the service's own words.

  describe('accepting a departure', () => {
    const HELD = [{
      ...QUESTS[0], status: 'Done', held: true,
      requirements: [{ quote: 'cap each frame', check: 'no frame hydrates past the cap' }],
      answers: [{ requirement: 1, departed: 'the first frame from a cold cache cannot be capped', quote: 'cap each frame' }],
    }];
    let accepted: string[] = [];

    function stub(answer: () => Response) {
      accepted = [];
      vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
        const url = String(input);
        if (init?.method === 'POST' && url.endsWith('/accept')) {
          accepted.push(url);
          return answer();
        }
        return url.startsWith('/api/quests') ? Response.json(HELD) : respond(url);
      }));
    }

    it('is listed first while it awaits the yes, accepted in one press, and answered verbatim', async () => {
      stub(() => Response.json({
        quest: { ...HELD[0], held: false, accepted: '2026-09-03T00:00:00Z' },
        message: 'Accepted the departure on quest `#abc123`: what it held goes on.',
      }));
      const notify = view();
      expect(await within(questList()).findByText('Waiting on you (1)')).toBeInTheDocument();
      const page = await chooseRow('Expose a streaming budget');

      await userEvent.click(within(page).getByRole('button', { name: 'Accept the departure' }));

      await waitFor(() => expect(accepted).toEqual(['/api/quests/abc123/accept']));
      await waitFor(() => expect(notify).toHaveBeenCalledWith('Accepted the departure on quest `#abc123`: what it held goes on.'));
      // The page shows the quest as the answer left it, before the list catches up: no second yes.
      await waitFor(() => expect(within(questMain()).queryByRole('button', { name: 'Accept the departure' })).toBeNull());
    });

    it('a refusal reaches the person in the service\'s words, and the quest stays held', async () => {
      const refusal = 'Quest `#abc123`\'s departure was already accepted, 2026-09-03 00:00 UTC: nothing waits for a yes.';
      stub(() => Response.json({ error: refusal }, { status: 409 }));
      const notify = view();
      const page = await chooseRow('Expose a streaming budget');

      await userEvent.click(within(page).getByRole('button', { name: 'Accept the departure' }));

      await waitFor(() => expect(notify).toHaveBeenCalledWith(refusal, 'error'));
      expect(within(questMain()).getByRole('button', { name: 'Accept the departure' })).toBeEnabled();
    });
  });

  /** The sync item's conflict list names a quest through the opener; Quests' list has it chosen, and its page opens. */
  it('a quest a door names opens on the page', async () => {
    openedOn('abc123');

    const page = await questPage('Expose a streaming budget');
    expect(within(page).getByRole('heading', { level: 1, name: 'Expose a streaming budget' })).toBeInTheDocument();
  });

  /** A record the list leaves out is still a record: a closed quest a door names opens on its page all the same. */
  it('opens a closed quest a door names, though the list leaves closed quests out', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/quests')) {
        return Response.json(url.includes('includeClosed=true') ? [...QUESTS, { ...QUESTS[0], id: 'd0d0d0', title: 'An old one', status: 'Done' }] : QUESTS);
      }
      return respond(url);
    }));
    openedOn('d0d0d0');

    const page = await questPage('An old one');
    const header = within(page).getByRole('heading', { level: 1 }).closest('header')!;
    expect(within(header).getByText('done')).toBeInTheDocument();
    expect(within(questList()).queryByText('An old one')).toBeNull();
  });

  // ——— Asks (INT4c): the screen twin of `daoris-driver ask`, at the head of the list that holds what
  // they become. The family here is one circle (no row names a workspace), so an ask is made there.

  describe('asks', () => {
    const PROPOSED = {
      id: '7c1e9a04b2d5', workspace: 'default', sentence: 'Cap the hydration per frame.\n\nThe trace is attached.',
      state: 'Proposed', tier: 'declarations', asked: '2026-09-02T00:00:00Z', updated: '2026-09-02T00:00:00Z',
      links: [], attachments: [], quests: [],
      proposal: [{ repository: 'engine', score: 3, matched: ['frame', 'hydration'] }],
    };
    let posted: { url: string; body: unknown }[] = [];

    beforeEach(() => {
      posted = [];
      vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
        const url = String(input);
        if (init?.method === 'POST' && url.startsWith('/api/asks')) {
          posted.push({ url, body: JSON.parse(String(init.body)) });
          const ask = url.endsWith('/publish')
            ? { ...PROPOSED, state: 'Published', quests: ['abc123'] }
            : PROPOSED;
          return Response.json({ ask, message: url.endsWith('/publish')
            ? 'Published quest `#abc123` to `engine` — Open.'
            : 'Asked as `#7c1e9a04b2d5` in `default` — by declarations only; no intake agent ran.' });
        }
        return respond(url);
      }));
    });
    afterEach(() => { ASKS = []; });

    it('the ＋\'s Ask opens the composer in the one workspace there is, sends the ask whole, and opens its page', async () => {
      const notify = view();
      await within(questList()).findByText('Expose a streaming budget');

      await makeFromList('Ask');
      const composer = await screen.findByRole('dialog', { name: 'Ask the workspace' });
      expect(within(composer).getByText('Asked in workspace default')).toBeInTheDocument();
      await userEvent.type(within(composer).getByLabelText('What is wanted, and why'), 'Cap the hydration per frame.');
      fireEvent.change(within(composer).getByLabelText(/Links — a ticket/), { target: { value: 'https://tickets.example/T-42' } });
      await userEvent.upload(within(composer).getByLabelText('Choose files…'), new File(['pixels'], 'trace.log'));
      await userEvent.click(within(composer).getByRole('button', { name: 'Ask' }));

      await waitFor(() => expect(posted).toHaveLength(1));
      expect(posted[0]).toEqual({
        url: '/api/asks',
        body: {
          workspace: 'default', sentence: 'Cap the hydration per frame.', links: ['https://tickets.example/T-42'],
          attachments: [{ name: 'trace.log', content: btoa('pixels') }],
        },
      });
      // The service's sentence, verbatim — and the page opens on what it proposed, before the list has it.
      await waitFor(() => expect(notify).toHaveBeenCalledWith(
        'Asked as `#7c1e9a04b2d5` in `default` — by declarations only; no intake agent ran.'));
      const page = await questPage('Cap the hydration per frame.');
      expect(within(page).getByRole('button', { name: 'Publish to engine' })).toBeInTheDocument();
      expect(window.localStorage.getItem('daoris.list.quests.chosen')).toBe('ask:7c1e9a04b2d5');
    });

    it('lists the asks above the quests, and publishing a proposal goes through the ask\'s own door', async () => {
      ASKS = [PROPOSED];
      view();

      // Counted as the quest groups beside it are, in one form on one list (POLISH4).
      expect(await within(questList()).findByText('Asks (1)')).toBeInTheDocument();
      const page = await chooseRow('Cap the hydration per frame.');
      await userEvent.click(within(page).getByRole('button', { name: 'Publish to engine' }));

      await waitFor(() => expect(posted).toEqual([{ url: '/api/asks/7c1e9a04b2d5/publish', body: { to: 'engine' } }]));
      // The quest it became is a door into the quest's own page.
      await userEvent.click(await within(questMain()).findByRole('button', { name: /#abc123/ }));
      expect(await screen.findByRole('heading', { level: 1, name: 'Expose a streaming budget' })).toBeInTheDocument();
      expect(window.localStorage.getItem('daoris.list.quests.chosen')).toBe('abc123');
    });

    /**
     * UXFIX2 (the second-opinion review, `AskPage.tsx:260`): an ask's delete refused is said inside its ask, in the service's
     * words, which stays open; it was a toast after the ask had closed on the press. Landed, it closes with the page.
     */
    it('says a refused delete of an ask inside its ask, and closes the page once a delete lands', async () => {
      ASKS = [{ ...PROPOSED, deletable: true }];
      const refusal = 'Ask `#7c1e9a04b2d5` became quest `#abc123`, which a session took, so it stays. Close it instead.';
      let refuse = true;
      const before = globalThis.fetch;
      vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
        if (init?.method === 'DELETE' && String(input) === '/api/asks/7c1e9a04b2d5') {
          return refuse
            ? Response.json({ error: refusal }, { status: 409 })
            : Response.json({ id: '7c1e9a04b2d5', message: 'Deleted ask `#7c1e9a04b2d5`.' });
        }
        return before(input, init);
      }));
      const notify = view();

      const page = await chooseRow('Cap the hydration per frame.');
      await userEvent.click(within(page).getByRole('button', { name: 'Delete…' }));
      await userEvent.click(within(page).getByRole('button', { name: 'Delete ask' }));
      const confirm = within(page).getByRole('group', { name: 'delete this ask' });
      expect(await within(confirm).findByRole('alert')).toHaveTextContent(refusal.replaceAll('`', ''));
      expect(notify).not.toHaveBeenCalledWith(refusal, 'error');

      refuse = false;
      await userEvent.click(within(confirm).getByRole('button', { name: 'Delete ask' }));
      await waitFor(() => expect(notify).toHaveBeenCalledWith('Deleted ask `#7c1e9a04b2d5`.'));
      expect(await within(questMain()).findByText('Choose a quest or an ask')).toBeInTheDocument();
    });

    /** An ask may be published to any repository its circle can ask — adopted or not (D70). */
    it('offers a registered repository that has not adopted as a receiver of the ask', async () => {
      ASKS = [PROPOSED];
      const before = globalThis.fetch;
      vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) =>
        String(input).startsWith('/api/registry')
          ? Response.json([
            ...REGISTRY.map((row) => ({ ...row, addressable: true })),
            { repository: 'legacy', adopted: false, addressable: true, registered: false, owns: [], accepts: [], packs: [], entries: 0 },
          ])
          : before(input, init)));
      view();

      const page = await chooseRow('Cap the hydration per frame.');
      await userEvent.click(within(page).getByLabelText('publish to another'));
      expect(await screen.findByRole('option', { name: 'legacy' })).toBeInTheDocument();
    });

    /** The palette's "Ask the workspace…" is an event, consumed by identity and cleared by its holder (frontend §4b). */
    it('a palette request opens the composer, once', async () => {
      const onAsked = vi.fn();
      function Asking() {
        const [pending, setPending] = useState(true);
        return <QuestsView notify={() => {}} asking={pending} onAsked={() => { onAsked(); setPending(false); }} />;
      }
      const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
      render(
        <QueryClientProvider client={client}>
          <Tooltip.Provider><Asking /></Tooltip.Provider>
        </QueryClientProvider>,
      );

      expect(await screen.findByRole('dialog', { name: 'Ask the workspace' })).toBeInTheDocument();
      expect(onAsked).toHaveBeenCalledTimes(1);
    });

    /** Overview's band names an ask waiting on a person (INT4d) through the opener; Quests opens its page. */
    it('an ask a door names opens its page', async () => {
      ASKS = [PROPOSED];
      openedOn('ask:7c1e9a04b2d5');

      const page = await questPage('Cap the hydration per frame.');
      expect(within(page).getByRole('button', { name: 'Publish to engine' })).toBeInTheDocument();
      expect(within(questList()).getByRole('button', { name: /Cap the hydration per frame\./ })).toHaveAttribute('aria-current', 'true');
    });

    it('says an ask chosen now that is no longer here has gone', async () => {
      openedOn('ask:0b9f3c21aa77');

      expect(await within(questMain()).findByText('This ask is no longer here')).toBeInTheDocument();
    });

    // UX6b (design §1 rule 6): an ask waiting to be published reopens; one closed or gone opens nothing chosen.
    it('reopens a remembered ask that still waits to be published', async () => {
      ASKS = [PROPOSED];
      window.localStorage.setItem('daoris.list.quests.chosen', 'ask:7c1e9a04b2d5');
      view();

      expect(await questPage('Cap the hydration per frame.')).toBeInTheDocument();
    });

    it('opens with nothing chosen on a remembered ask that closed or went', async () => {
      ASKS = [{ ...PROPOSED, state: 'Closed', note: 'not needed' }];
      window.localStorage.setItem('daoris.list.quests.chosen', 'ask:7c1e9a04b2d5');
      view();

      await nothingChosen();
      expect(window.localStorage.getItem('daoris.list.quests.chosen')).toBeNull();
      cleanup();

      window.localStorage.setItem('daoris.list.quests.chosen', 'ask:0b9f3c21aa77');
      view();
      await nothingChosen();
      expect(within(questMain()).queryByText('This ask is no longer here')).toBeNull();
    });

    /** The page's intake line is a door into Sessions only where the view is handed one (INT4d). */
    it('an ask\'s intake session opens where Sessions exists', async () => {
      const INTAKE = {
        id: 'i9n8t7k6a5b4', quest: null, repository: 'ask #7c1e9a04b2d5', adapter: 'stub', kind: 'chat',
        state: 'awaiting-person', ask: '7c1e9a04b2d5', note: 'published nothing.',
        created: '2026-09-02T00:00:00Z', updated: '2026-09-02T01:00:00Z',
      };
      ASKS = [{ ...PROPOSED, intake: INTAKE.id }];
      vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
        const url = String(input);
        if (url.startsWith('/api/sessions')) return Response.json([...SESSIONS, INTAKE]);
        return respond(url);
      }));
      const onAttend = vi.fn();
      const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
      render(
        <QueryClientProvider client={client}>
          <Tooltip.Provider><QuestsView notify={() => {}} onAttend={onAttend} /></Tooltip.Provider>
        </QueryClientProvider>,
      );

      const page = await chooseRow('Cap the hydration per frame.');
      const line = within(page).getByRole('region', { name: 'Intake session' });
      await userEvent.click(await within(line).findByRole('button', { name: 'stub' }));
      expect(onAttend).toHaveBeenCalledWith('i9n8t7k6a5b4');
    });
  });

  // ——— A chain (D65 §4).

  it('the page says what a quest follows and what its close will publish next', async () => {
    view();
    const chain = within(await chooseRow('Expose a streaming budget')).getByRole('region', { name: 'How this work ran' });

    // The parent is closed and out of this page's list: named, not dropped (MAP1).
    expect(within(chain).getByText('#f0f0f0')).toBeInTheDocument();
    expect(within(chain).getByText(/Report on \{parent\}/)).toBeInTheDocument();
    expect(within(chain).getByText(/published when the one before it closes done/i)).toBeInTheDocument();
  });

  /** MAP1: every attempt at a step, on what it ran — the page's session section keeps only the latest. */
  it('the chain lists every session that ran the quest', async () => {
    view();
    const chain = within(await chooseRow('Expose a streaming budget')).getByRole('region', { name: 'How this work ran' });

    // UX7c (D152 §7): every attempt ran on the one agent, said once above its rows, each row its attempt.
    expect(within(chain).getAllByText('stub')).toHaveLength(1);
    expect(within(chain).getByText('attempt 1')).toBeInTheDocument();
    expect(within(chain).getByText('attempt 2')).toBeInTheDocument();
    expect(within(chain).getByText('failed')).toBeInTheDocument();
  });

  it('a row says it follows another quest', async () => {
    view();
    expect(await within(questList()).findByText(/follows #f0f0f0/)).toBeInTheDocument();
  });

  // ——— Ask and wait (D79): a taken quest waiting on another repository's answer says so — it is
  // not stuck, and there is nothing for the person to do.

  const WAITING = [
    { ...QUESTS[0], status: 'Taken', awaits: 'q2q2q2' },
    {
      id: 'q2q2q2', from: 'engine', to: 'backend', title: 'What does the notes endpoint take?',
      body: 'We need the contract.', status: 'Open', filed: '2026-09-02T00:00:00Z', updated: '2026-09-02T00:00:00Z',
    },
  ];
  const serving = (quests: unknown[]) =>
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) =>
      String(input).startsWith('/api/quests') ? Response.json(quests) : respond(String(input))));

  it('a taken quest waiting on a question says which, and its page opens that question', async () => {
    serving(WAITING);
    view();
    expect(await within(questList()).findByText('waits on #q2q2q2')).toBeInTheDocument();

    const page = await chooseRow('Expose a streaming budget');
    expect(within(page).getByText('Waits on')).toBeInTheDocument();
    expect(within(page).getByText(/resumes, in the same tree/)).toBeInTheDocument();

    await userEvent.click(within(page).getByRole('button', { name: /What does the notes endpoint take/ }));
    expect(await screen.findByRole('heading', { level: 1, name: 'What does the notes endpoint take?' })).toBeInTheDocument();
  });

  it('once its question is answered the row stops saying it waits, and the page says it was answered', async () => {
    serving([WAITING[0], { ...WAITING[1], status: 'Done' }]);
    view();
    await within(questList()).findByText('Expose a streaming budget');
    expect(screen.queryByText('waits on #q2q2q2')).not.toBeInTheDocument();

    const page = await chooseRow('Expose a streaming budget');
    expect(within(page).getByText('Asked')).toBeInTheDocument();
    expect(within(page).getByText(/^Answered/)).toBeInTheDocument();
  });

  it('a next step composed travels with the publish as its chain', async () => {
    view({ from: 'game', to: 'engine' });
    const dialog = await screen.findByRole('dialog');
    fireEvent.change(within(dialog).getByLabelText('What is wanted, in one line'), { target: { value: 'Develop it' } });
    fireEvent.change(within(dialog).getByLabelText('Why, and the evidence'), { target: { value: 'Because.' } });

    await userEvent.click(within(dialog).getByRole('button', { name: 'Add a next step…' }));
    await userEvent.click(within(dialog).getByLabelText('Then ask'));
    await userEvent.click(await screen.findByRole('option', { name: 'engine' }));
    fireEvent.change(within(dialog).getByLabelText('What is wanted next, in one line'), { target: { value: 'Verify {parent}' } });
    fireEvent.change(within(dialog).getByLabelText('Why, and how to tell it is done'), { target: { value: 'Open the app.' } });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Publish quest' }));

    await vi.waitFor(() => expect(published).not.toBeNull());
    expect(published!.then).toEqual([{ to: 'engine', title: 'Verify {parent}', body: 'Open the app.' }]);
  });

  /**
   * SESSUX1j (the session management design §6.1): the composer offers an optional short title, the person's own few
   * words, sent with the publish; one past 40 characters holds the publish back, as the service would refuse it, and an
   * empty one is not sent at all.
   */
  it('a short title written travels with the publish, and one past forty characters holds it back', async () => {
    view({ from: 'game', to: 'engine' });
    const dialog = await screen.findByRole('dialog');
    fireEvent.change(within(dialog).getByLabelText('What is wanted, in one line'), { target: { value: 'Expose a streaming budget on the chunk API' } });
    fireEvent.change(within(dialog).getByLabelText('Why, and the evidence'), { target: { value: 'Because.' } });

    const short = within(dialog).getByLabelText('Short title');
    fireEvent.change(short, { target: { value: 'A short title that runs well past the forty characters' } });
    expect(within(dialog).getByRole('button', { name: 'Publish quest' })).toBeDisabled();

    fireEvent.change(short, { target: { value: '  Streaming budget ' } });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Publish quest' }));

    await vi.waitFor(() => expect(published).not.toBeNull());
    expect(published!.short).toBe('Streaming budget');
  });

  it('a quest published with no short title sends none', async () => {
    view({ from: 'game', to: 'engine' });
    const dialog = await screen.findByRole('dialog');
    fireEvent.change(within(dialog).getByLabelText('What is wanted, in one line'), { target: { value: 'Develop it' } });
    fireEvent.change(within(dialog).getByLabelText('Why, and the evidence'), { target: { value: 'Because.' } });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Publish quest' }));

    await vi.waitFor(() => expect(published).not.toBeNull());
    expect(published).not.toHaveProperty('short');
  });

  /** A quest just published opens on its page, as an ask's record opens on its answer. */
  it('opens the quest just published on its page, and closes the composer', async () => {
    view({ from: 'game', to: 'engine' });
    const dialog = await screen.findByRole('dialog');
    fireEvent.change(within(dialog).getByLabelText('What is wanted, in one line'), { target: { value: 'Expose a streaming budget' } });
    fireEvent.change(within(dialog).getByLabelText('Why, and the evidence'), { target: { value: 'Because.' } });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Publish quest' }));

    expect(await questPage('Expose a streaming budget')).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
    expect(window.localStorage.getItem('daoris.list.quests.chosen')).toBe('abc123');
  });

  /**
   * Registered is addressable; adopted is disciplined (D70). The host answers which repositories can
   * be asked — its exchange's own judgement — so the form offers exactly those: one registered here
   * without a manifest, and never one nothing could answer.
   */
  it('offers every repository the host says can be asked, adopted or not, and nothing else', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      if (url.startsWith('/api/registry')) {
        return Response.json([
          ...REGISTRY.map((row) => ({ ...row, addressable: true })),
          { repository: 'legacy', adopted: false, addressable: true, registered: false, owns: [], accepts: [], packs: [], entries: 0 },
          { repository: 'rootless', adopted: false, addressable: false, registered: false, owns: [], accepts: [], packs: [], entries: 0 },
        ]);
      }
      return respond(url);
    }));
    view({ from: 'game' });
    const dialog = await screen.findByRole('dialog');

    await userEvent.click(within(dialog).getByLabelText('To'));
    expect(await screen.findByRole('option', { name: 'legacy' })).toBeInTheDocument();
    expect(screen.queryByRole('option', { name: 'rootless' })).toBeNull();
  });

  it('a next step started and left empty holds the publish back, and can be taken off', async () => {
    view({ from: 'game', to: 'engine' });
    const dialog = await screen.findByRole('dialog');
    fireEvent.change(within(dialog).getByLabelText('What is wanted, in one line'), { target: { value: 'Develop it' } });
    fireEvent.change(within(dialog).getByLabelText('Why, and the evidence'), { target: { value: 'Because.' } });

    await userEvent.click(within(dialog).getByRole('button', { name: 'Add a next step…' }));
    expect(within(dialog).getByRole('button', { name: 'Publish quest' })).toBeDisabled();

    await userEvent.click(within(dialog).getByRole('button', { name: 'Remove next step' }));
    expect(within(dialog).getByRole('button', { name: 'Publish quest' })).toBeEnabled();
  });

  // ——— What a quest carries (D65 §2).

  it('the page shows the links as links and opens a kept file — and never shows where it lies', async () => {
    view();
    const page = await chooseRow('Expose a streaming budget');

    expect(within(page).getByRole('link', { name: /tickets\.example\/T-1/ })).toHaveAttribute(
      'href', 'https://tickets.example/T-1');
    expect(within(page).getByRole('link', { name: /before\.png/ })).toHaveAttribute(
      'href', `/api/quests/abc123/attachments/ab12cd34ef56${'0'.repeat(52)}`);
    // A picture is shown as one — from the host's own route, never from the path.
    expect(within(page).getByRole('img', { name: 'before.png' })).toBeInTheDocument();
    // 🔴 A page does not name a machine path, even one it was answered.
    expect(page.textContent).not.toContain('C:/somewhere');
  });

  it('a file named on the record but not kept here is said to be elsewhere, not offered as a link', async () => {
    view();
    const page = await chooseRow('Expose a streaming budget');

    expect(within(page).getByText('trace.log')).toBeInTheDocument();
    expect(within(page).queryByRole('link', { name: /trace\.log/ })).not.toBeInTheDocument();
    expect(within(page).getByText(/kept on the machine that published it/)).toBeInTheDocument();
  });

  it('a row says what its quest carries, beside its state', async () => {
    view();
    expect(await within(questList()).findByLabelText('1 link · 2 files')).toBeInTheDocument();
  });

  it('links typed and a file chosen travel with the publish — the file whole, as base64', async () => {
    view({ from: 'game', to: 'engine' });
    const dialog = await screen.findByRole('dialog');
    // Set rather than typed key by key: what is under test is what travels, not the keyboard — and
    // typing three fields character by character outran the suite's timeout under a full run.
    fireEvent.change(within(dialog).getByLabelText('What is wanted, in one line'), { target: { value: 'Use the media config' } });
    fireEvent.change(within(dialog).getByLabelText('Why, and the evidence'), { target: { value: 'Field names are hard-coded.' } });
    fireEvent.change(
      within(dialog).getByLabelText(/^Links/), { target: { value: 'https://tickets.example/T-1\nhttps://docs.example/media' } });
    await userEvent.upload(within(dialog).getByLabelText('Choose files…'), new File(['pixels'], 'before.png'));

    expect(within(dialog).getByText('before.png')).toBeInTheDocument();
    await userEvent.click(within(dialog).getByRole('button', { name: 'Publish quest' }));

    await vi.waitFor(() => expect(published).not.toBeNull());
    expect(published!.links).toEqual(['https://tickets.example/T-1', 'https://docs.example/media']);
    expect(published!.attachments).toEqual([{ name: 'before.png', content: btoa('pixels') }]);
  });

  it('a pasted screenshot is attached rather than typed', async () => {
    view({ from: 'game', to: 'engine' });
    const dialog = await screen.findByRole('dialog');
    const shot = new File(['pixels'], 'image.png', { type: 'image/png' });

    fireEvent.paste(within(dialog).getByLabelText('Why, and the evidence'), { clipboardData: { files: [shot] } });

    expect(await within(dialog).findByText('image.png')).toBeInTheDocument();
  });

  it('a dropped file is attached, and a chosen one can be taken back off', async () => {
    view({ from: 'game', to: 'engine' });
    const dialog = await screen.findByRole('dialog');

    fireEvent.drop(within(dialog).getByText(/drop files here/i), {
      dataTransfer: { files: [new File(['stack'], 'trace.log')], types: ['Files'] },
    });
    expect(await within(dialog).findByText('trace.log')).toBeInTheDocument();

    await userEvent.click(within(dialog).getByRole('button', { name: 'Remove trace.log' }));
    expect(within(dialog).queryByText('trace.log')).not.toBeInTheDocument();
  });

  it('more files than a quest carries are left off, and the composer says why', async () => {
    view({ from: 'game', to: 'engine' });
    const dialog = await screen.findByRole('dialog');
    const eleven = Array.from({ length: 11 }, (_, i) => new File([`${i}`], `f${i}.txt`));

    await userEvent.upload(within(dialog).getByLabelText('Choose files…'), eleven);

    expect(within(dialog).getByText('At most 10 files travel together.')).toBeInTheDocument();
    expect(within(dialog).queryByText('f10.txt')).not.toBeInTheDocument();
  });

  it('groups what the service returns by where it is in its life', async () => {
    view();
    expect(await within(questList()).findByText('Open — waiting to be taken (1)')).toBeInTheDocument();
    expect(within(questList()).getByText('Expose a streaming budget')).toBeInTheDocument();
  });

  it('a row is a door: choosing it opens the quest\'s page, where the acting happens', async () => {
    view();
    const page = await chooseRow('Expose a streaming budget');
    expect(within(page).getByText('World streaming needs a per-frame cap.')).toBeInTheDocument();
    expect(within(page).getByRole('button', { name: 'Take' })).toBeInTheDocument();
  });

  /**
   * 🔴 UX5 U31: the drawer made *done* its one loud control whatever the quest's state, so an Open
   * quest led with closing it and offered taking it as the quiet choice. The loud control is the
   * quest's next step: take while it is open, done once it is taken.
   */
  it("makes taking an open quest the page's one loud control, in its header", async () => {
    view();
    const page = await chooseRow('Expose a streaming budget');
    const header = within(page).getByRole('heading', { level: 1 }).closest('header')!;

    expect(within(header).getByRole('button', { name: 'Take' }).className).toContain('bg-accent');
    // UX7c (D152 §7): marking done a quest nobody has taken is rarely the next step, so it is in the head's ⋯.
    expect(within(header).queryByRole('button', { name: 'Mark done…' })).toBeNull();
    expect(within(header).getAllByRole('button').map((button) => button.textContent || button.getAttribute('aria-label')))
      .toEqual(['Take', 'Decline…', 'More actions']);
    // U35: taking wore a check mark, the sign of done, beside a done that wore none.
    expect(within(header).getByRole('button', { name: 'Take' }).querySelector('svg')).toBeNull();
  });

  /** A decline needs its reason, asked under the header: the service refuses one without. */
  it('asks a decline\'s reason under the header, sends it, and never mind puts the press back', async () => {
    const answered: unknown[] = [];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (init?.method === 'POST' && url === '/api/quests/abc123/respond') {
        answered.push(JSON.parse(String(init.body)));
        return Response.json({ quest: { ...QUESTS[0], status: 'Declined', updated: '2026-09-03T00:00:00Z' }, message: 'Quest `#abc123` is now Declined.' });
      }
      return respond(url);
    }));
    view();
    const page = await chooseRow('Expose a streaming budget');

    await userEvent.click(within(page).getByRole('button', { name: 'Decline…' }));
    await userEvent.click(within(page).getByRole('button', { name: 'Never mind' }));
    expect(within(page).queryByLabelText('the reason — it is the part the asker can act on')).toBeNull();

    await userEvent.click(within(page).getByRole('button', { name: 'Decline…' }));
    const confirm = within(page).getByRole('button', { name: 'Decline with this reason' });
    expect(confirm).toBeDisabled();
    await userEvent.type(within(page).getByLabelText('the reason — it is the part the asker can act on'), 'Not ours.');
    await userEvent.click(confirm);

    await waitFor(() => expect(answered).toEqual([{ action: 'decline', reason: 'Not ours.' }]));
    // The page stays on the quest as the answer left it: declined, with nothing left to do.
    expect(await within(questMain()).findByText('declined')).toBeInTheDocument();
    expect(within(questMain()).queryByRole('button', { name: 'Take' })).toBeNull();
  });

  /**
   * USE1c: closing the last quest an ask became makes the ask done, which the service derives on each
   * read — so a quest's move asks the asks again, or the list would say "published" until something
   * else refreshed it.
   */
  it('closing a quest asks the asks again, since the ask it came from may now be done', async () => {
    const asked: string[] = [];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (url.startsWith('/api/asks')) asked.push(url);
      if (init?.method === 'POST' && url === '/api/quests/abc123/done') {
        return Response.json({ quest: { ...QUESTS[0], status: 'Done' }, message: 'Quest `#abc123` is now Done: you marked it done.' });
      }
      return respond(url);
    }));
    view();
    const page = await chooseRow('Expose a streaming budget');
    await waitFor(() => expect(asked.length).toBeGreaterThan(0));
    const before = asked.length;

    await moreAct(page, 'Mark done…');
    await userEvent.click(within(screen.getByRole('group', { name: 'mark this quest done' })).getByRole('button', { name: 'Mark done' }));

    await waitFor(() => expect(asked.length).toBeGreaterThan(before));
  });

  /**
   * QUESTCLOSE1 (D126's note): the person's done goes to the service's own door for it with their words, never to respond's,
   * which is an agent's done and would ask the person's requirements answered one by one. The page stays on the quest as the
   * answer left it, the ask closed, and the service's sentence is the notice.
   */
  it("sends the person's done with their note to its own door, and closes its ask once it landed", async () => {
    const sent: { url: string; body: unknown }[] = [];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (init?.method === 'POST' && url.startsWith('/api/quests/abc123/')) {
        sent.push({ url, body: JSON.parse(String(init.body)) });
        return Response.json({
          quest: { ...QUESTS[0], status: 'Done', note: 'The person marked this done: It is in the shared folder.' },
          message: 'Quest `#abc123` is now Done: you marked it done.',
        });
      }
      return url.startsWith('/api/quests') ? Response.json([{ ...QUESTS[0], status: 'Taken' }]) : respond(url);
    }));
    const notify = view();
    const page = await chooseRow('Expose a streaming budget');

    await userEvent.click(within(page).getByRole('button', { name: 'Mark done…' }));
    const ask = within(page).getByRole('group', { name: 'mark this quest done' });
    await userEvent.type(within(ask).getByLabelText('Your note (optional)'), 'It is in the shared folder.');
    await userEvent.click(within(ask).getByRole('button', { name: 'Mark done' }));

    await waitFor(() => expect(sent).toEqual([{ url: '/api/quests/abc123/done', body: { note: 'It is in the shared folder.' } }]));
    await waitFor(() => expect(notify).toHaveBeenCalledWith('Quest `#abc123` is now Done: you marked it done.'));
    await waitFor(() => expect(within(questMain()).queryByRole('group', { name: 'mark this quest done' })).toBeNull());
    expect(await within(questMain()).findByText('The person marked this done: It is in the shared folder.')).toBeInTheDocument();
  });

  /** A refusal of the person's done is the service's sentence, said inside its ask, and the quest stays as it was. */
  it("says the service's refusal of the person's done inside its ask", async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
      const url = String(input);
      if (init?.method === 'POST' && url === '/api/quests/abc123/done') {
        return Response.json(
          { error: 'Quest `#abc123` is Done — a closed quest does not move; a new ask is a new title.' }, { status: 409 },
        );
      }
      return url.startsWith('/api/quests') ? Response.json([{ ...QUESTS[0], status: 'Taken' }]) : respond(url);
    }));
    view();
    const page = await chooseRow('Expose a streaming budget');

    await userEvent.click(within(page).getByRole('button', { name: 'Mark done…' }));
    const ask = within(page).getByRole('group', { name: 'mark this quest done' });
    await userEvent.click(within(ask).getByRole('button', { name: 'Mark done' }));

    expect(await within(ask).findByRole('alert')).toHaveTextContent('a closed quest does not move');
  });

  it("makes closing a taken quest the page's one loud control", async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      return url.startsWith('/api/quests') ? Response.json([{ ...QUESTS[0], status: 'Taken' }]) : respond(url);
    }));
    view();
    const page = await chooseRow('Expose a streaming budget');

    expect(within(page).queryByRole('button', { name: 'Take' })).toBeNull();
    expect(within(page).getByRole('button', { name: 'Mark done…' }).className).toContain('bg-accent');
  });

  it('publish stays disabled until the ask is complete — the form does not offer the mistake', async () => {
    view();
    await within(questList()).findByText('Expose a streaming budget');
    await makeFromList('New quest');
    expect(await screen.findByRole('button', { name: 'Publish quest' })).toBeDisabled();
  });

  /** Seen on the installed window, 2026-09-24: with nothing registered, `from` and `to` offered nobody. */
  it('says nobody can be asked yet, rather than a form whose from and to offer nobody', async () => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      return url.startsWith('/api/registry') ? Response.json([]) : respond(url);
    }));
    view();
    await within(questList()).findByText('Expose a streaming budget');
    await makeFromList('New quest');
    const dialog = await screen.findByRole('dialog');

    expect(await within(dialog).findByText('Nobody can be asked yet')).toBeInTheDocument();
    expect(within(dialog).queryByRole('button', { name: 'Publish quest' })).toBeNull();
    expect(within(dialog).queryByLabelText('From')).toBeNull();
  });

  it('a quest a driver is working wears its session state on its row', async () => {
    view();
    expect(await within(questList()).findByText('working')).toBeInTheDocument();
  });

  /** The page's session section — where things stand NOW. The chain above it keeps the history. */
  const sessionSection = async () =>
    within(await chooseRow('Expose a streaming budget')).getByRole('region', { name: 'Session' });

  it("the page carries the session's record — state, adapter, and the evidence, verbatim", async () => {
    view();
    const section = await sessionSection();
    expect(within(section).getByText('working')).toBeInTheDocument();
    expect(within(section).getByText(/s1a2b3c4 · stub/)).toBeInTheDocument();
    expect(within(section).getByText(/stub: answer quest abc123/)).toBeInTheDocument();
  });

  it('a retried quest shows its freshest attempt, never the failed first one', async () => {
    view();
    const section = await sessionSection();
    expect(within(section).getByText(/s1a2b3c4 · stub/)).toBeInTheDocument();
    expect(within(section).queryByText('failed')).not.toBeInTheDocument();
    expect(within(section).queryByText(/s0f1r2s3/)).not.toBeInTheDocument();
  });

  it('a browser offers no stop — the control reaches a process, and only the desktop has one', async () => {
    view();
    const section = await sessionSection();
    // The record renders (above); the control must not — a browser could only wish (D46 §6).
    expect(within(section).getByText('working')).toBeInTheDocument();
    expect(within(questMain()).queryByRole('button', { name: 'Stop session' })).not.toBeInTheDocument();
  });
});

/**
 * ENTRY1b (D161's ENTRY1 note): a go to what waits on the person names a group of the list, the asks above it or the
 * quests held for the person's yes, which the view brings into view once the quests and the asks have answered: its heading
 * scrolled to and focused, then the door told. A list that draws no such group lets the door go.
 */
describe('a group a door brings into view', () => {
  const HELD = { ...QUESTS[0], id: 'def456', title: 'Hold the cap', status: 'Done', held: true, parent: undefined, then: [] };
  const ASK = {
    id: '7c1e9a04b2d5', workspace: 'default', sentence: 'Cap the hydration per frame.', state: 'Proposed', tier: 'declarations',
    asked: '2026-09-02T00:00:00Z', updated: '2026-09-02T00:00:00Z', links: [], attachments: [], quests: [], proposal: [],
  };
  let quests: unknown[] = [];

  function shown(group: 'asks' | 'held', onGroupBrought: () => void) {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider>
          <QuestsView notify={vi.fn()} group={group} onGroupBrought={onGroupBrought} />
        </Tooltip.Provider>
      </QueryClientProvider>,
    );
  }

  beforeEach(() => {
    ASKS = [ASK];
    quests = [...QUESTS, HELD];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => {
      const url = String(input);
      return url.startsWith('/api/quests') ? Response.json(quests) : respond(url);
    }));
  });
  afterEach(() => {
    ASKS = [];
    vi.unstubAllGlobals();
    window.localStorage.clear();
  });

  it('scrolls to the quests held for the person and focuses their heading, then tells the door', async () => {
    const scroll = vi.spyOn(Element.prototype, 'scrollIntoView');
    const brought = vi.fn();
    try {
      shown('held', brought);

      const heading = await within(questList()).findByRole('heading', { level: 3, name: 'Waiting on you (1)' });
      await waitFor(() => expect(brought).toHaveBeenCalledTimes(1));
      expect(heading).toHaveFocus();
      expect(scroll.mock.instances).toContain(heading);
    } finally {
      scroll.mockRestore();
    }
  });

  it('brings the asks above the quests into view', async () => {
    const brought = vi.fn();
    shown('asks', brought);

    const heading = await within(questList()).findByRole('heading', { level: 3, name: 'Asks (1)' });
    await waitFor(() => expect(brought).toHaveBeenCalledTimes(1));
    expect(heading).toHaveFocus();
  });

  /** ENTRY1g (D161's ENTRY1b note): where the list is a strip, the go lays it over the main area and brings the group there. */
  it('lays the list over the main area where it is a strip, and brings the held quests into view there', async () => {
    Object.defineProperty(window, 'innerWidth', { configurable: true, value: 600 });
    try {
      const brought = vi.fn();
      shown('held', brought);

      const over = await screen.findByRole('region', { name: 'Quests' });
      const heading = await within(over).findByRole('heading', { level: 3, name: 'Waiting on you (1)' });
      await waitFor(() => expect(brought).toHaveBeenCalledTimes(1));
      expect(heading).toHaveFocus();
    } finally {
      Object.defineProperty(window, 'innerWidth', { configurable: true, value: 1024 });
    }
  });

  it('lets the door go where nothing is held', async () => {
    quests = QUESTS;
    const brought = vi.fn();
    shown('held', brought);

    await within(questList()).findByText('Expose a streaming budget');
    await waitFor(() => expect(brought).toHaveBeenCalledTimes(1));
    expect(document.activeElement).toBe(document.body);
  });
});

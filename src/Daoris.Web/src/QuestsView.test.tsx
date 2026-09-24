import { useState } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';
import { QuestsView } from './QuestsView';

// The view over a stubbed service — the shapes the real endpoints return, without a host. The
// Playwright loop owns the real end-to-end; this owns the view's own logic at millisecond speed.

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

// A driven session's record, attached to the quest above (D46): active, so the card wears its state.
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
function Held({ opening }: { opening: { from?: string; to?: string } | null }) {
  const [pending, setPending] = useState(opening);
  return <QuestsView notify={() => {}} opening={pending} onOpened={() => setPending(null)} />;
}

function view(opening: { from?: string; to?: string } | null = null) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <Held opening={opening} />
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

/** The body the last publish sent — what the local host would have been asked to keep. */
let published: {
  links?: string[]; attachments?: { name: string; content: string }[];
  then?: { to: string; title: string; body: string }[];
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
  afterEach(() => vi.unstubAllGlobals());

  /**
   * 🔴 SURF6b's door — "send it back as a quest" — hands the composer a draft, and consuming it by
   * setting state during render looped until React gave up: the door crashed the view it opened.
   */
  it('a draft handed in by a door opens the composer on it, once', async () => {
    view({ from: 'game', to: 'engine' });
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByRole('heading', { name: 'New quest' })).toBeInTheDocument();

    // The draft LANDED: publish needs both repositories, and the person has chosen neither.
    fireEvent.change(within(dialog).getByLabelText('what is wanted, in one line'), { target: { value: 'An ask' } });
    fireEvent.change(within(dialog).getByLabelText('why, and the evidence'), { target: { value: 'Its reason.' } });
    expect(within(dialog).getByRole('button', { name: 'publish quest' })).toBeEnabled();
  });

  // ——— A conflict (D68 §5, SYNC6b): kept on the quest for a person, and reachable from the status bar.

  it('the drawer shows each move that lost the race, in its own words', async () => {
    const conflicted = [{
      ...QUESTS[0], status: 'Taken',
      conflicts: [{ machine: 'b7f2c9d1', attempted: 'Taken', note: 'machine b, offline', at: '2026-09-02T00:00:00Z' }],
    }];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) =>
      String(input).startsWith('/api/quests') ? Response.json(conflicted) : respond(String(input))));
    view();
    await userEvent.click(await screen.findByText('Expose a streaming budget'));
    const dialog = await screen.findByRole('dialog');
    const conflicts = within(dialog).getByRole('region', { name: 'Conflicts' });

    expect(within(conflicts).getByText(/Machine b7f2c9d1 tried to mark it Taken/)).toBeInTheDocument();
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
    const notify = vi.fn();
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
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider><QuestsView notify={notify} /></Tooltip.Provider>
      </QueryClientProvider>,
    );
    await userEvent.click(await screen.findByText('Expose a streaming budget'));
    const conflicts = within(await screen.findByRole('dialog')).getByRole('region', { name: 'Conflicts' });

    await userEvent.click(within(conflicts).getByRole('button', { name: /dismiss/i }));

    await waitFor(() => expect(dismissed).toEqual({ machine: 'b7f2c9d1', sequence: 12 }));
    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      'Dismissed one conflict on quest `#abc123`; every machine drops it on its next sync.'));
  });

  /** The sync item's conflict list names a quest; Quests opens it, and the holder is told, once. */
  it('a quest a door names opens in the drawer, once', async () => {
    const onFocused = vi.fn();
    function Focused() {
      const [pending, setPending] = useState<string | null>('abc123');
      return <QuestsView notify={() => {}} focus={pending} onFocused={() => { onFocused(); setPending(null); }} />;
    }
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={client}>
        <Tooltip.Provider><Focused /></Tooltip.Provider>
      </QueryClientProvider>,
    );

    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText('#abc123')).toBeInTheDocument();
    expect(onFocused).toHaveBeenCalledTimes(1);
  });

  // ——— Asks (INT4c): the screen twin of `daoris-driver ask`, at the head of the view that holds what
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
            : 'Asked as `#7c1e9a04b2d5` in `default` — by declarations only; no intake harness ran.' });
        }
        return respond(url);
      }));
    });
    afterEach(() => { ASKS = []; });

    it('the Ask button opens the composer in the one circle there is, and the ask is sent whole', async () => {
      const notify = vi.fn();
      const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
      render(
        <QueryClientProvider client={client}>
          <Tooltip.Provider><QuestsView notify={notify} /></Tooltip.Provider>
        </QueryClientProvider>,
      );

      await userEvent.click(await screen.findByRole('button', { name: 'ask' }));
      const composer = await screen.findByRole('dialog', { name: 'Ask the circle' });
      expect(within(composer).getByText('Asked in default')).toBeInTheDocument();
      await userEvent.type(within(composer).getByLabelText('what is wanted, and why'), 'Cap the hydration per frame.');
      fireEvent.change(within(composer).getByLabelText(/links — a ticket/), { target: { value: 'https://tickets.example/T-42' } });
      await userEvent.upload(within(composer).getByLabelText('choose files…'), new File(['pixels'], 'trace.log'));
      await userEvent.click(within(composer).getByRole('button', { name: 'ask' }));

      await waitFor(() => expect(posted).toHaveLength(1));
      expect(posted[0]).toEqual({
        url: '/api/asks',
        body: {
          workspace: 'default', sentence: 'Cap the hydration per frame.', links: ['https://tickets.example/T-42'],
          attachments: [{ name: 'trace.log', content: btoa('pixels') }],
        },
      });
      // The service's sentence, verbatim — and the record opens on what it proposed.
      await waitFor(() => expect(notify).toHaveBeenCalledWith(
        'Asked as `#7c1e9a04b2d5` in `default` — by declarations only; no intake harness ran.'));
      const record = await screen.findByRole('dialog', { name: 'Cap the hydration per frame.' });
      expect(within(record).getByRole('button', { name: 'publish to engine' })).toBeInTheDocument();
    });

    it('lists the asks above the quests, and publishing a proposal goes through the ask\'s own door', async () => {
      ASKS = [PROPOSED];
      view();

      expect(await screen.findByText('Asks · 1')).toBeInTheDocument();
      await userEvent.click(screen.getByText('Cap the hydration per frame.'));
      const record = await screen.findByRole('dialog', { name: 'Cap the hydration per frame.' });
      await userEvent.click(within(record).getByRole('button', { name: 'publish to engine' }));

      await waitFor(() => expect(posted).toEqual([{ url: '/api/asks/7c1e9a04b2d5/publish', body: { to: 'engine' } }]));
      // The quest it became is a door into the quest's own drawer.
      await userEvent.click(await within(record).findByRole('button', { name: /#abc123/ }));
      expect(await screen.findByRole('dialog', { name: 'Expose a streaming budget' })).toBeInTheDocument();
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

      await userEvent.click(await screen.findByText('Cap the hydration per frame.'));
      const record = await screen.findByRole('dialog', { name: 'Cap the hydration per frame.' });
      await userEvent.click(within(record).getByLabelText('publish to another'));
      expect(await screen.findByRole('option', { name: 'legacy' })).toBeInTheDocument();
    });

    /** The palette's "Ask the circle…" is an event, consumed by identity and cleared by its holder (frontend §4b). */
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

      expect(await screen.findByRole('dialog', { name: 'Ask the circle' })).toBeInTheDocument();
      expect(onAsked).toHaveBeenCalledTimes(1);
    });

    /** Overview's band names an ask waiting on a person (INT4d); Quests opens its record, once. */
    it('an ask a door names opens its record, once', async () => {
      ASKS = [PROPOSED];
      const onAskFocused = vi.fn();
      function Focused() {
        const [pending, setPending] = useState<string | null>('7c1e9a04b2d5');
        return (
          <QuestsView
            notify={() => {}}
            askFocus={pending}
            onAskFocused={() => { onAskFocused(); setPending(null); }}
          />
        );
      }
      const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
      render(
        <QueryClientProvider client={client}>
          <Tooltip.Provider><Focused /></Tooltip.Provider>
        </QueryClientProvider>,
      );

      const record = await screen.findByRole('dialog', { name: 'Cap the hydration per frame.' });
      expect(within(record).getByRole('button', { name: 'publish to engine' })).toBeInTheDocument();
      expect(onAskFocused).toHaveBeenCalledTimes(1);
    });

    /** The record's intake line is a door into Sessions only where the view is handed one (INT4d). */
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

      await userEvent.click(await screen.findByText('Cap the hydration per frame.'));
      const record = await screen.findByRole('dialog', { name: 'Cap the hydration per frame.' });
      const line = within(record).getByRole('region', { name: 'intake session' });
      await userEvent.click(await within(line).findByRole('button', { name: 'stub' }));
      expect(onAttend).toHaveBeenCalledWith('i9n8t7k6a5b4');
    });
  });

  // ——— A chain (D65 §4).

  it('the drawer says what a quest follows and what its close will publish next', async () => {
    view();
    await userEvent.click(await screen.findByText('Expose a streaming budget'));
    const dialog = await screen.findByRole('dialog');
    const chain = within(dialog).getByRole('region', { name: 'How this work ran' });

    // The parent is closed and out of this page's list: named, not dropped (MAP1).
    expect(within(chain).getByText('#f0f0f0')).toBeInTheDocument();
    expect(within(chain).getByText(/Report on \{parent\}/)).toBeInTheDocument();
    expect(within(chain).getByText(/published when the one before it closes done/i)).toBeInTheDocument();
  });

  /** MAP1: every attempt at a step, on what it ran — the drawer's session section keeps only the latest. */
  it('the chain lists every session that ran the quest', async () => {
    view();
    await userEvent.click(await screen.findByText('Expose a streaming budget'));
    const chain = within(await screen.findByRole('dialog')).getByRole('region', { name: 'How this work ran' });

    expect(within(chain).getAllByText('stub')).toHaveLength(2);
    expect(within(chain).getByText('failed')).toBeInTheDocument();
  });

  it('a card says it follows another quest', async () => {
    view();
    expect(await screen.findByText(/follows #f0f0f0/)).toBeInTheDocument();
  });

  it('a next step composed travels with the publish as its chain', async () => {
    view({ from: 'game', to: 'engine' });
    const dialog = await screen.findByRole('dialog');
    fireEvent.change(within(dialog).getByLabelText('what is wanted, in one line'), { target: { value: 'Develop it' } });
    fireEvent.change(within(dialog).getByLabelText('why, and the evidence'), { target: { value: 'Because.' } });

    await userEvent.click(within(dialog).getByRole('button', { name: 'add a next step…' }));
    await userEvent.click(within(dialog).getByLabelText('then ask'));
    await userEvent.click(await screen.findByRole('option', { name: 'engine' }));
    fireEvent.change(within(dialog).getByLabelText('what is wanted next, in one line'), { target: { value: 'Verify {parent}' } });
    fireEvent.change(within(dialog).getByLabelText('why, and how to tell it is done'), { target: { value: 'Open the app.' } });
    await userEvent.click(within(dialog).getByRole('button', { name: 'publish quest' }));

    await vi.waitFor(() => expect(published).not.toBeNull());
    expect(published!.then).toEqual([{ to: 'engine', title: 'Verify {parent}', body: 'Open the app.' }]);
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

    await userEvent.click(within(dialog).getByLabelText('to'));
    expect(await screen.findByRole('option', { name: 'legacy' })).toBeInTheDocument();
    expect(screen.queryByRole('option', { name: 'rootless' })).toBeNull();
  });

  it('a next step started and left empty holds the publish back, and can be taken off', async () => {
    view({ from: 'game', to: 'engine' });
    const dialog = await screen.findByRole('dialog');
    fireEvent.change(within(dialog).getByLabelText('what is wanted, in one line'), { target: { value: 'Develop it' } });
    fireEvent.change(within(dialog).getByLabelText('why, and the evidence'), { target: { value: 'Because.' } });

    await userEvent.click(within(dialog).getByRole('button', { name: 'add a next step…' }));
    expect(within(dialog).getByRole('button', { name: 'publish quest' })).toBeDisabled();

    await userEvent.click(within(dialog).getByRole('button', { name: 'no next step' }));
    expect(within(dialog).getByRole('button', { name: 'publish quest' })).toBeEnabled();
  });

  // ——— What a quest carries (D65 §2).

  it('the drawer shows the links as links and opens a kept file — and never shows where it lies', async () => {
    view();
    await userEvent.click(await screen.findByText('Expose a streaming budget'));
    const dialog = await screen.findByRole('dialog');

    expect(within(dialog).getByRole('link', { name: /tickets\.example\/T-1/ })).toHaveAttribute(
      'href', 'https://tickets.example/T-1');
    expect(within(dialog).getByRole('link', { name: /before\.png/ })).toHaveAttribute(
      'href', `/api/quests/abc123/attachments/ab12cd34ef56${'0'.repeat(52)}`);
    // A picture is shown as one — from the host's own route, never from the path.
    expect(within(dialog).getByRole('img', { name: 'before.png' })).toBeInTheDocument();
    // 🔴 A page does not name a machine path, even one it was answered.
    expect(dialog.textContent).not.toContain('C:/somewhere');
  });

  it('a file named on the record but not kept here is said to be elsewhere, not offered as a link', async () => {
    view();
    await userEvent.click(await screen.findByText('Expose a streaming budget'));
    const dialog = await screen.findByRole('dialog');

    expect(within(dialog).getByText('trace.log')).toBeInTheDocument();
    expect(within(dialog).queryByRole('link', { name: /trace\.log/ })).not.toBeInTheDocument();
    expect(within(dialog).getByText(/kept on the machine that published it/)).toBeInTheDocument();
  });

  it('a card says what its quest carries, beside the title', async () => {
    view();
    expect(await screen.findByLabelText('1 link · 2 files')).toBeInTheDocument();
  });

  it('links typed and a file chosen travel with the publish — the file whole, as base64', async () => {
    view({ from: 'game', to: 'engine' });
    const dialog = await screen.findByRole('dialog');
    // Set rather than typed key by key: what is under test is what travels, not the keyboard — and
    // typing three fields character by character outran the suite's timeout under a full run.
    fireEvent.change(within(dialog).getByLabelText('what is wanted, in one line'), { target: { value: 'Use the media config' } });
    fireEvent.change(within(dialog).getByLabelText('why, and the evidence'), { target: { value: 'Field names are hard-coded.' } });
    fireEvent.change(
      within(dialog).getByLabelText(/^links/), { target: { value: 'https://tickets.example/T-1\nhttps://docs.example/media' } });
    await userEvent.upload(within(dialog).getByLabelText('choose files…'), new File(['pixels'], 'before.png'));

    expect(within(dialog).getByText('before.png')).toBeInTheDocument();
    await userEvent.click(within(dialog).getByRole('button', { name: 'publish quest' }));

    await vi.waitFor(() => expect(published).not.toBeNull());
    expect(published!.links).toEqual(['https://tickets.example/T-1', 'https://docs.example/media']);
    expect(published!.attachments).toEqual([{ name: 'before.png', content: btoa('pixels') }]);
  });

  it('a pasted screenshot is attached rather than typed', async () => {
    view({ from: 'game', to: 'engine' });
    const dialog = await screen.findByRole('dialog');
    const shot = new File(['pixels'], 'image.png', { type: 'image/png' });

    fireEvent.paste(within(dialog).getByLabelText('why, and the evidence'), { clipboardData: { files: [shot] } });

    expect(await within(dialog).findByText('image.png')).toBeInTheDocument();
  });

  it('a dropped file is attached, and a chosen one can be taken back off', async () => {
    view({ from: 'game', to: 'engine' });
    const dialog = await screen.findByRole('dialog');

    fireEvent.drop(within(dialog).getByText(/drop files here/i), {
      dataTransfer: { files: [new File(['stack'], 'trace.log')], types: ['Files'] },
    });
    expect(await within(dialog).findByText('trace.log')).toBeInTheDocument();

    await userEvent.click(within(dialog).getByRole('button', { name: 'remove trace.log' }));
    expect(within(dialog).queryByText('trace.log')).not.toBeInTheDocument();
  });

  it('more files than a quest carries are left off, and the composer says why', async () => {
    view({ from: 'game', to: 'engine' });
    const dialog = await screen.findByRole('dialog');
    const eleven = Array.from({ length: 11 }, (_, i) => new File([`${i}`], `f${i}.txt`));

    await userEvent.upload(within(dialog).getByLabelText('choose files…'), eleven);

    expect(within(dialog).getByText('At most 10 files travel together.')).toBeInTheDocument();
    expect(within(dialog).queryByText('f10.txt')).not.toBeInTheDocument();
  });

  it('groups what the service returns by where it is in its life', async () => {
    view();
    expect(await screen.findByText('Open — waiting to be taken (1)')).toBeInTheDocument();
    expect(screen.getByText('Expose a streaming budget')).toBeInTheDocument();
  });

  it('a card is a door: clicking it opens the detail drawer, where the acting happens', async () => {
    view();
    await userEvent.click(await screen.findByText('Expose a streaming budget'));
    // Scoped to the dialog: the ask's text also lives in the card's excerpt behind the drawer —
    // the list surviving the detail is the drawer pattern's whole point.
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText('World streaming needs a per-frame cap.')).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'take' })).toBeInTheDocument();
  });

  it('publish stays disabled until the ask is complete — the form does not offer the mistake', async () => {
    view();
    await userEvent.click(await screen.findByRole('button', { name: 'new quest' }));
    expect(await screen.findByRole('button', { name: 'publish quest' })).toBeDisabled();
  });

  it('a quest a driver is working wears its session state on the card', async () => {
    view();
    expect(await screen.findByText('working')).toBeInTheDocument();
  });

  /** The drawer's session section — where things stand NOW. The chain above it keeps the history. */
  const sessionSection = async () => {
    await userEvent.click(await screen.findByText('Expose a streaming budget'));
    return within(await screen.findByRole('dialog')).getByRole('region', { name: 'session' });
  };

  it("the drawer carries the session's record — state, adapter, and the evidence, verbatim", async () => {
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
    expect(within(await screen.findByRole('dialog')).queryByRole('button', { name: 'stop session' }))
      .not.toBeInTheDocument();
  });
});

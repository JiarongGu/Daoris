import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// A press under way is the window's and the driver's, never the one component's that pressed. The
// review's *Accept* reads *Landing…* while its session's landing runs, after a tab switch remounted it and where another window
// pressed it; so too its *Discard* and its hand-off.

const { invoke, eventHandlers } = vi.hoisted(() => ({
  invoke: vi.fn(),
  // The push channel's seam: handlers land here by "module.type", and a test fires them as the host.
  eventHandlers: new Map<string, (payload: unknown) => void>(),
}));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke }),
  useShenora: () => ({ isAvailable: true, bridge: {} }),
  useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
    eventHandlers.set(`${module}.${type}`, handler);
  },
}));

import i18n from '../../locales/i18n';
import { useStateHub } from '../../bridge/state/stateHub';
import { DiffPane } from './DiffPane';

const FILES = [{ path: 'notes.txt', status: 'added', added: 1, removed: 0, patch: '@@ -0,0 +1 @@\n+notes' }];
const DIFF = { session: 's1a2b3c4', base: '0123456789abcdef', truncated: null, files: FILES, source: 'tree' };
const PLAN = { session: 's1a2b3c4', form: 'branch', target: 'feature/0fda18-fix-the-gap', source: 'workspace' };
const LANDED = { session: 's1a2b3c4', done: true, message: 'put its work on `feature/0fda18-fix-the-gap` — 1 commit(s).' };

/** The routes the review reads; a landing answers only when the test lets it. */
function answer() {
  let land: (value: unknown) => void = () => {};
  invoke.mockImplementation((_module: string, type: string) => {
    if (type === 'SESSION_DIFF') return Promise.resolve(DIFF);
    if (type === 'LANDING') return Promise.resolve(PLAN);
    if (type === 'LAND_SESSION_TREE') return new Promise((resolve) => { land = resolve; });
    return Promise.resolve({});
  });
  return { land: (value: unknown) => land(value) };
}

function Hub() {
  useStateHub();
  return null;
}

/** The window's root with the hub, and the review shown or not, as a tab switch shows it; a part's names its line of work. */
function root(line: { entry?: string; ask?: string } = {}) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const Window = ({ shown }: { shown: boolean }) => (
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <Hub />
        {shown && <DiffPane session="s1a2b3c4" hasTree onSendBack={vi.fn()} entry={line.entry} ask={line.ask} />}
      </Tooltip.Provider>
    </QueryClientProvider>
  );
  const view = render(<Window shown />);
  return { show: (shown: boolean) => view.rerender(<Window shown={shown} />) };
}

const told = (underWay: unknown) => act(() => eventHandlers.get('DAORIS.UNDER_WAY')!(underWay));

describe('a press under way, in the review', () => {
  afterEach(() => {
    invoke.mockReset();
    eventHandlers.clear();
  });

  it('still says Landing… once a tab switch remounted it mid-press, and Accept again once the landing answered', async () => {
    const pressed = answer();
    const { show } = root();
    await userEvent.click(await screen.findByRole('button', { name: 'Accept' }));
    expect(await screen.findByRole('button', { name: 'Landing…' })).toBeDisabled();

    show(false);
    expect(screen.queryByRole('button', { name: 'Landing…' })).toBeNull();
    show(true);

    expect(await screen.findByRole('button', { name: 'Landing…' })).toBeDisabled();
    expect(screen.queryByRole('button', { name: 'Accept' })).toBeNull();

    await act(async () => pressed.land(LANDED));
    expect(await screen.findByRole('button', { name: 'Accept' })).toBeEnabled();
  });

  it('says Landing… while another window’s landing of its session is under way, and nothing for another session’s', async () => {
    answer();
    root();
    await screen.findByRole('button', { name: 'Accept' });

    told({ seq: 1, presses: [{ route: 'LAND_SESSION_TREE', session: 'feedface' }] });
    await act(() => new Promise((resolve) => { setTimeout(resolve, 0); }));
    expect(screen.getByRole('button', { name: 'Accept' })).toBeEnabled();

    told({ seq: 2, presses: [{ route: 'LAND_SESSION_TREE', session: 'feedface' }, { route: 'LAND_SESSION_TREE', session: 's1a2b3c4' }] });
    expect(await screen.findByRole('button', { name: 'Landing…' })).toBeDisabled();

    told({ seq: 3, presses: [] });
    expect(await screen.findByRole('button', { name: 'Accept' })).toBeEnabled();
    // Nothing is pressed here: the press is the other window's.
    expect(invoke.mock.calls.filter(([, type]) => type === 'LAND_SESSION_TREE')).toHaveLength(0);
  });

  /** The driver says the landing is at its gate, and the review's press says so, then *Landing…* once it moves on. */
  it('says Checking… while the driver says the landing is at its gate, and Landing… once it has moved on', async () => {
    answer();
    root();
    await screen.findByRole('button', { name: 'Accept' });
    const landing = { route: 'LAND_SESSION_TREE', session: 's1a2b3c4' };

    told({ seq: 1, presses: [{ ...landing, step: 'gate' }] });
    expect(await screen.findByRole('button', { name: 'Checking…' })).toBeDisabled();

    told({ seq: 2, presses: [{ ...landing, step: 'landing' }] });
    expect(await screen.findByRole('button', { name: 'Landing…' })).toBeDisabled();

    // A step the page has no word for is the plain *Landing…*, and the press's end puts Accept back.
    told({ seq: 3, presses: [{ ...landing, step: 'replaying' }] });
    expect(await screen.findByRole('button', { name: 'Landing…' })).toBeDisabled();
    told({ seq: 4, presses: [] });
    expect(await screen.findByRole('button', { name: 'Accept' })).toBeEnabled();
  });

  /**
   * An Accept whose landing the runtime performs waits for the pass that lands it, never behind a tree's hold: the button
   * and the note beside it say so and say the pass's bound, in the page's language. Another session's wait is not this one's.
   */
  it.each([
    ['en', 'Awaiting its pass…', 'Landing…', /Accepted\. Daoris lands it at its next pass\. This waits up to 20 seconds/],
    ['zh', '等待落地…', '落地中…', /Daoris 会在它的下一轮检查时落地它；这里最多等待 20 秒/],
  ])('says it waits for the pass that lands it and for how long, in %s', async (language, waiting, landing, note) => {
    await i18n.changeLanguage(language);
    try {
      answer();
      root();
      await screen.findByRole('button', { name: language === 'en' ? 'Accept' : '采纳' });
      const press = { route: 'LAND_SESSION_TREE', session: 's1a2b3c4' };

      told({ seq: 1, presses: [{ route: 'LAND_SESSION_TREE', session: 'feedface', step: 'pass' }] });
      await act(() => new Promise((resolve) => { setTimeout(resolve, 0); }));
      expect(screen.queryByRole('button', { name: waiting })).toBeNull();
      expect(screen.queryByText(note)).toBeNull();

      told({ seq: 2, presses: [{ ...press, step: 'pass' }] });
      expect(await screen.findByRole('button', { name: waiting })).toBeDisabled();
      expect(screen.getByText(note)).toBeInTheDocument();

      told({ seq: 3, presses: [{ ...press, step: 'landing' }] });
      expect(await screen.findByRole('button', { name: landing })).toBeDisabled();
      expect(screen.queryByText(note)).toBeNull();
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  /**
   * A part's own *Accept* holds while its line of work's one Accept, or its ask's, lands it,
   * from this window or another, read by the entry's and the ask's subject; another entry's or ask's press holds nothing.
   */
  it('holds a part’s Accept while its line of work’s or its ask’s landing is under way, and not for another’s', async () => {
    answer();
    root({ entry: 'ask:a3/engine', ask: 'a3' });
    await screen.findByRole('button', { name: 'Accept' });

    told({ seq: 1, presses: [{ route: 'LAND_ENTRY', subject: 'ask:a3/game' }, { route: 'LAND_ASK', subject: 'a9' }] });
    await act(() => new Promise((resolve) => { setTimeout(resolve, 0); }));
    expect(screen.getByRole('button', { name: 'Accept' })).toBeEnabled();

    told({ seq: 2, presses: [{ route: 'LAND_ENTRY', subject: 'ask:a3/engine' }] });
    expect(await screen.findByRole('button', { name: 'Landing…' })).toBeDisabled();
    told({ seq: 3, presses: [] });
    expect(await screen.findByRole('button', { name: 'Accept' })).toBeEnabled();

    told({ seq: 4, presses: [{ route: 'LAND_ASK', subject: 'a3' }] });
    expect(await screen.findByRole('button', { name: 'Landing…' })).toBeDisabled();
    told({ seq: 5, presses: [] });
    expect(await screen.findByRole('button', { name: 'Accept' })).toBeEnabled();
    expect(invoke.mock.calls.filter(([, type]) => type === 'LAND_SESSION_TREE')).toHaveLength(0);
  });

  it('holds its Discard while another window discards its tree', async () => {
    answer();
    root();
    const discard = await screen.findByRole('button', { name: 'Discard tree' });
    expect(discard).toBeEnabled();

    told({ seq: 1, presses: [{ route: 'DISCARD_SESSION_TREE', session: 's1a2b3c4' }] });

    await vi.waitFor(() => expect(screen.getByRole('button', { name: 'Discard tree' })).toBeDisabled());
  });
});

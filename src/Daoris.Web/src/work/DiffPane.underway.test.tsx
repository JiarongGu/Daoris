import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// STATEHUB1c (D169 point 7): a press under way is the window's and the driver's, never the one component's that pressed. The
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

import i18n from '../locales/i18n';
import { useStateHub } from '../bridge/stateHub';
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
    if (type === 'HANDOFF_PLAN') return Promise.resolve({ session: 's1a2b3c4', branch: null });
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
    // Nothing was pressed here: the press was the other window's.
    expect(invoke.mock.calls.filter(([, type]) => type === 'LAND_SESSION_TREE')).toHaveLength(0);
  });

  /** PRESSSTEP1: the driver says the landing is at its gate, and the review's press says so, then *Landing…* once it moves on. */
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
   * LANDHOLD2 (D145 point 2, D173's LANDRACE1 note): the driver says the landing waits behind another landing of its tree, and the
   * press says so in the page's language, then *Landing…* once it has the hold. Another session's wait is not this one's.
   */
  it.each([
    ['en', 'Waiting its turn…', 'Landing…'],
    ['zh', '排队中…', '落地中…'],
  ])('says it waits behind another landing, in %s, and goes back to the landing once it has the hold', async (language, waiting, landing) => {
    await i18n.changeLanguage(language);
    try {
      answer();
      root();
      await screen.findByRole('button', { name: language === 'en' ? 'Accept' : '采纳' });
      const press = { route: 'LAND_SESSION_TREE', session: 's1a2b3c4' };

      told({ seq: 1, presses: [{ route: 'LAND_SESSION_TREE', session: 'feedface', step: 'waiting' }] });
      await act(() => new Promise((resolve) => { setTimeout(resolve, 0); }));
      expect(screen.queryByRole('button', { name: waiting })).toBeNull();

      told({ seq: 2, presses: [{ ...press, step: 'waiting' }] });
      expect(await screen.findByRole('button', { name: waiting })).toBeDisabled();

      told({ seq: 3, presses: [{ ...press, step: 'landing' }] });
      expect(await screen.findByRole('button', { name: landing })).toBeDisabled();
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  /** LANDHOLD2: a hand-off that waits behind the push of its branch says so, and says *Handing off…* once it has the hold. */
  it('says a hand-off waits behind another push of its branch', async () => {
    answer();
    const asked = invoke.getMockImplementation()!;
    invoke.mockImplementation((module: string, type: string) => type === 'HANDOFF_PLAN'
      ? Promise.resolve({ session: 's1a2b3c4', branch: 'feature/0fda18-fix-the-gap', repository: 'engine', plugin: 'example.lands', problem: null, commits: 1 })
      : asked(module, type));
    root();
    await screen.findByRole('button', { name: 'Hand to example.lands' });
    const press = { route: 'HANDOFF', session: 's1a2b3c4' };

    told({ seq: 1, presses: [{ ...press, step: 'waiting' }] });
    expect(await screen.findByRole('button', { name: 'Waiting its turn…' })).toBeDisabled();

    told({ seq: 2, presses: [{ ...press, step: 'handOff' }] });
    expect(await screen.findByRole('button', { name: 'Handing off…' })).toBeDisabled();
  });

  /**
   * ACCEPTALL1h (D169's ACCEPTALL1e note): a part's own *Accept* holds while its line of work's one Accept, or its ask's, lands it,
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

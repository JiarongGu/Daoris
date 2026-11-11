import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// A press under way is the window's, never the one component's that pressed. The review's *Accept* is the review record's word
// (`WORKFLOW_NODE`'s `accept`), and it says it waits for the pass that lands it after a tab switch remounted it; a part's Accept holds
// while its line of work's or its ask's landing runs, and its *Discard* while another window discards its tree.

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
const ACCEPT = { run: 'q1', node: 'review', attempt: 2, revision: 'feedc0de' };
const PLAN = { session: 's1a2b3c4', form: 'branch', target: 'feature/0fda18-fix-the-gap', source: 'workspace', accept: ACCEPT };
const ACCEPTED = { run: 'q1', node: 'review', act: 'accept', done: true, message: 'put its work on `feature/0fda18-fix-the-gap`.' };

/** The routes the review reads; an Accept answers only when the test lets it. */
function answer() {
  let accepted: (value: unknown) => void = () => {};
  invoke.mockImplementation((_module: string, type: string) => {
    if (type === 'SESSION_DIFF') return Promise.resolve(DIFF);
    if (type === 'LANDING') return Promise.resolve(PLAN);
    if (type === 'WORKFLOW_NODE') return new Promise((resolve) => { accepted = resolve; });
    return Promise.resolve({});
  });
  return { accept: (value: unknown) => accepted(value) };
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

  it('presses the record drawn, still waits once a tab switch remounted it mid-press, and offers Accept again once it answered', async () => {
    const pressed = answer();
    const { show } = root();
    await userEvent.click(await screen.findByRole('button', { name: 'Accept' }));
    expect(await screen.findByRole('button', { name: 'Awaiting its pass…' })).toBeDisabled();
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'WORKFLOW_NODE',
      expect.objectContaining({ payload: { run: 'q1', node: 'review', act: 'accept', attempt: 2, revision: 'feedc0de' } }));

    show(false);
    show(true);

    expect(await screen.findByRole('button', { name: 'Awaiting its pass…' })).toBeDisabled();
    expect(screen.queryByRole('button', { name: 'Accept' })).toBeNull();

    await act(async () => pressed.accept(ACCEPTED));
    expect(await screen.findByRole('button', { name: 'Accept' })).toBeEnabled();
  });

  /** An Accept waits for the pass that lands it, never behind a tree's hold: the button and the note say so and the pass's bound. */
  it.each([
    ['en', 'Accept', 'Awaiting its pass…', /Accepted\. Daoris lands it at its next pass\. This waits up to 20 seconds/],
    ['zh', '采纳', '等待落地…', /Daoris 会在它的下一轮检查时落地它；这里最多等待 20 秒/],
  ])('says it waits for the pass that lands it and for how long, in %s', async (language, accept, waiting, note) => {
    await i18n.changeLanguage(language);
    try {
      answer();
      root();
      expect(screen.queryByText(note)).toBeNull();
      await userEvent.click(await screen.findByRole('button', { name: accept }));

      expect(await screen.findByRole('button', { name: waiting })).toBeDisabled();
      expect(screen.getByText(note)).toBeInTheDocument();
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
    expect(invoke.mock.calls.filter(([, type]) => type === 'WORKFLOW_NODE')).toHaveLength(0);
  });

  it('holds its Discard while another window discards its tree', async () => {
    answer();
    root();
    const discard = await screen.findByRole('button', { name: 'Discard tree' });
    expect(discard).toBeEnabled();

    told({ seq: 1, presses: [{ route: 'DISCARD_SESSION_TREE', session: 's1a2b3c4' }] });

    await vi.waitFor(() => expect(screen.getByRole('button', { name: 'Discard tree' })).toBeDisabled());
  });

  it('offers no Accept where the run holds no review to accept', async () => {
    invoke.mockImplementation((_module: string, type: string) => {
      if (type === 'SESSION_DIFF') return Promise.resolve(DIFF);
      if (type === 'LANDING') return Promise.resolve({ ...PLAN, accept: null });
      return Promise.resolve({});
    });
    root();
    await screen.findByRole('button', { name: 'Discard tree' });

    expect(screen.queryByRole('button', { name: 'Accept' })).toBeNull();
  });
});

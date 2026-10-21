import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// FABRIC-CLOSE1h9 (D173's FABRIC-CLOSE1h3 note, decision 4): a press whose push was held back lands with no plugin told, and its answer
// says why (`withheld`). The review's foot says it as a push held back, the driver's sentence as written, with the hand-off as its way
// on, never as an ordinary landing; an answer with no `withheld` reads as before.

const { invoke, heard } = vi.hoisted(() => ({
  invoke: vi.fn(),
  heard: new Map<string, (payload: unknown) => void>(),
}));

vi.mock('@shenora/react', async () => {
  const { publishing } = await import('../../test/publishing');
  const published = publishing(invoke, (type, payload) => heard.get(type)?.(payload));
  return {
    isShenoraAvailable: () => true,
    getBridge: () => ({ isAvailable: true, invoke: published }),
    useShenora: () => ({ isAvailable: true, bridge: {} }),
    useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
      if (module === 'DAORIS') heard.set(type, handler);
    },
  };
});

import i18n from '../../locales/i18n';
import { useStateHub } from '../../bridge/shell';
import { DiffPane } from './DiffPane';

const FILES = [{ path: 'notes.txt', status: 'added', added: 1, removed: 0, patch: '@@ -0,0 +1 @@\n+notes' }];
const DIFF = { session: 's1a2b3c4', base: '0123456789abcdef', truncated: null, files: FILES, source: 'tree' };
const PLAN = { session: 's1a2b3c4', form: 'branch', target: 'mine/x', plugin: 'example.lands', source: 'workspace' };
const WHY = 'Its quest was taken back before its plugin was told, so its push was not tried: the branch stands, and once you have '
  + 'looked at the quest as it stands, `daoris-driver trees hand s1a2b3c4` hands it on';
const HELD = { session: 's1a2b3c4', done: true, message: `put its work on \`mine/x\` — 1 commit(s). ${WHY}.`, branch: 'mine/x', withheld: WHY };
const PUSHED = {
  session: 's1a2b3c4', done: true, message: 'put its work on `mine/x` — 1 commit(s).', branch: 'mine/x',
  plugin: { id: 'example.lands', pushed: true, message: 'pushed.' },
};

function answer(pressed: unknown) {
  let landed = false;
  invoke.mockImplementation(async (_module: string, type: string) => {
    if (type === 'SESSION_DIFF') return DIFF;
    // The branch the press made stands with no push tried, so the hand-off is offered once it landed.
    if (type === 'HANDOFF_PLAN') return landed ? { session: 's1a2b3c4', branch: 'mine/x', plugin: 'example.lands' } : { session: 's1a2b3c4', branch: null };
    if (type === 'LANDING') return PLAN;
    if (type === 'LAND_SESSION_TREE') {
      landed = true;
      return pressed;
    }
    return {};
  });
}

function Hub() {
  useStateHub();
  return null;
}

function pane() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={client}>
      <Tooltip.Provider>
        <Hub />
        <DiffPane session="s1a2b3c4" hasTree onSendBack={vi.fn()} />
      </Tooltip.Provider>
    </QueryClientProvider>,
  );
}

describe('a press whose push was held back', () => {
  afterEach(() => { invoke.mockReset(); });

  it('says the push was held back, the driver’s sentence as written, and offers the hand-off', async () => {
    answer(HELD);
    pane();

    await userEvent.click(await screen.findByRole('button', { name: 'Accept' }));

    expect(await screen.findByText(HELD.message)).toBeInTheDocument();
    expect(screen.getByText(
      'Its push was held back, so no plugin was told and the branch stands. Look at its quest as it stands, then hand it off here.',
    )).toBeInTheDocument();
    expect(await screen.findByRole('button', { name: 'Hand to example.lands' })).toBeInTheDocument();
  });

  it('says nothing of a held push where the press pushed', async () => {
    answer(PUSHED);
    pane();

    await userEvent.click(await screen.findByRole('button', { name: 'Accept' }));

    expect(await screen.findByText(PUSHED.message)).toBeInTheDocument();
    expect(screen.queryByText(/push was held back/)).toBeNull();
  });

  it('says it in Chinese too', async () => {
    answer(HELD);
    await i18n.changeLanguage('zh');
    try {
      pane();
      await userEvent.click(await screen.findByRole('button', { name: '采纳' }));
      expect(await screen.findByText('它的推送被暂缓，没有告知插件，分支保留着。先看看它的委托现在如何，再在这里交接它。')).toBeInTheDocument();
      expect(screen.getByText(HELD.message)).toBeInTheDocument();
    } finally {
      await i18n.changeLanguage('en');
    }
  });
});

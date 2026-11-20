import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// The session's head says where the work stands and what comes next, from the driver's facts, on the page.
vi.mock('@shenora/react', async () => (await import('../test/workFrameBridge')).shenoraMock());

import i18n from '../locales/i18n';
import { eventHandlers, invoke } from '../test/workFrameBridge';
import { DRIVEN, DRIVER_STATE, Hub, QUESTS, REGISTRY, ROSTER } from '../test/workFrameKit';
import { WorkFrame } from './WorkFrame';

let SESSIONS: unknown[] = [];

function respond(url: string): Response {
  if (url.startsWith('/api/sessions')) return Response.json(SESSIONS);
  if (url.startsWith('/api/quests')) return Response.json(QUESTS);
  if (url.startsWith('/api/registry')) return Response.json(REGISTRY);
  throw new Error(`unstubbed request: ${url}`);
}

const ENDED = {
  ...DRIVEN, id: 'c4c4c4c4', state: 'completed', tree: 'C:/somewhere/.daoris/trees/default/engine/s-c4c4c4c4',
  created: '2026-10-30T08:40:00Z', updated: '2026-10-30T09:00:00Z', note: 'Done; committed as a1b2c3d.',
};

/** The driver's grouping: the part needs the person for its review, offering work to land, or is working with nothing yet to land. */
function driver(phase: 'review' | 'working') {
  const state = phase === 'review' ? 'needs-you' : 'working';
  invoke.mockImplementation(async (_module: string, type: string) => {
    if (type === 'HARNESSES') return ROSTER;
    if (type === 'SESSION_HISTORY') return { session: 'c4c4c4c4', events: [], earlier: false, latest: 0 };
    if (type === 'SESSION_GROUPS') {
      return {
        sessions: [{
          session: 'c4c4c4c4', group: state, state, ...(phase === 'review' ? { reason: 'review' } : {}),
          shown: phase === 'review' ? 'completed' : 'working', archived: false, teammate: false,
          ...(phase === 'review' ? { lands: { branch: 'daoris/s-c4c4c4c4', tree: 's-c4c4c4c4', commits: 1, uncommitted: 0 } } : {}),
        }],
        entries: [{
          entry: 'session:c4c4c4c4/engine', origin: 'session:c4c4c4c4', repository: 'engine', group: state, leading: 'c4c4c4c4',
          parts: [{ session: 'c4c4c4c4', quest: 'abc123', shown: phase === 'review' ? 'completed' : 'working', group: state, began: 'opened', after: null, askedBy: null }],
        }],
      };
    }
    if (type === 'LANDING') return { session: 'c4c4c4c4', form: 'branch', target: 'main', source: 'workspace', accept: { run: 'q1', node: 'review', attempt: 1 } };
    return DRIVER_STATE;
  });
}

function show() {
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <Hub />
      <Tooltip.Provider><WorkFrame selected="c4c4c4c4" onSelect={() => {}} notify={() => {}} /></Tooltip.Provider>
    </QueryClientProvider>,
  );
}

const panel = () => screen.findByRole('region', { name: i18n.t('work.session.label') });

describe('a session’s head says where the work stands and what comes next', () => {
  beforeEach(() => vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input)))));
  afterEach(async () => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    eventHandlers.clear();
    await i18n.changeLanguage('en');
  });

  for (const [language, now, wait] of [['en', 'Waiting on you', /its work to accept/], ['zh', '等你处理', /待采纳的工作/]] as const) {
    it(`reads ${now} in the head and the wait as its card in the dock in ${language}`, async () => {
      await i18n.changeLanguage(language);
      SESSIONS = [ENDED];
      driver('review');
      show();
      const region = await panel();
      const dock = await waitFor(() => {
        const found = region.querySelector<HTMLElement>('[data-region="dock"]');
        if (!found) throw new Error('no dock yet');
        return found;
      });
      expect(await within(dock).findByRole('article', { name: wait })).toBeInTheDocument();
      expect(region.querySelector('[data-region="head"]')).toHaveTextContent(now);
    });
  }

  it('reads working in the head and draws no dock where nothing waits', async () => {
    SESSIONS = [{ ...ENDED, state: 'working', note: undefined }];
    driver('working');
    show();
    const region = await panel();
    await waitFor(() => expect(region.querySelector('[data-region="head"]')).toHaveTextContent('Working'));
    expect(region.querySelector('[data-region="dock"]')).toBeNull();
    expect(region.querySelector('[data-wait]')).toBeNull();
  });
});

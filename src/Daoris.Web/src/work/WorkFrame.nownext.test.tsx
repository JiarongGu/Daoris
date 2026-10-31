import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// REVIEWFLOW1b: the session's head says where the work stands and what comes next, from the driver's facts, on the page.
vi.mock('@shenora/react', async () => (await import('../test/workFrameBridge')).shenoraMock());

import i18n from '../locales/i18n';
import { eventHandlers, invoke } from '../test/workFrameBridge';
import { DRIVEN, DRIVER_STATE, Hub, QUESTS, REGISTRY, ROSTER, userEvent } from '../test/workFrameKit';
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

/** The driver's grouping: the part offers work to land (`review`), or is working with nothing yet to land. */
function driver(phase: 'review' | 'working') {
  invoke.mockImplementation(async (_module: string, type: string) => {
    if (type === 'HARNESSES') return ROSTER;
    if (type === 'SESSION_HISTORY') return { session: 'c4c4c4c4', events: [], earlier: false, latest: 0 };
    if (type === 'SESSION_GROUPS') {
      return {
        sessions: [{
          session: 'c4c4c4c4', group: phase, shown: phase === 'review' ? 'completed' : 'working', archived: false, teammate: false,
          ...(phase === 'review' ? { lands: { branch: 'daoris/s-c4c4c4c4', tree: 's-c4c4c4c4', commits: 1, uncommitted: 0 } } : {}),
        }],
        entries: [{
          entry: 'session:c4c4c4c4/engine', origin: 'session:c4c4c4c4', repository: 'engine', group: phase, leading: 'c4c4c4c4',
          parts: [{ session: 'c4c4c4c4', quest: 'abc123', shown: phase === 'review' ? 'completed' : 'working', group: phase, began: 'opened', after: null, askedBy: null }],
        }],
      };
    }
    if (type === 'LANDING') return { session: 'c4c4c4c4', form: 'branch', target: 'main', source: 'workspace' };
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

describe('a session’s head says where the work stands and what comes next', () => {
  beforeEach(() => vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input)))));
  afterEach(async () => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    eventHandlers.clear();
    await i18n.changeLanguage('en');
  });

  for (const [language, now, next] of [['en', '1 part to land', 'next: Accept…'], ['zh', '1 个部分待落地', '下一步：接受…']] as const) {
    it(`reads ${now} and a door to the Accept… in ${language}`, async () => {
      await i18n.changeLanguage(language);
      SESSIONS = [ENDED];
      driver('review');
      show();
      const door = await screen.findByRole('button', { name: next });
      expect(door.closest('p')).toHaveTextContent(new RegExp(now));
      await userEvent.click(door);
    });
  }

  it('reads working and no door where nothing waits', async () => {
    SESSIONS = [{ ...ENDED, state: 'working', note: undefined }];
    driver('working');
    show();
    expect(await within(screen.getByRole('main')).findByText(/engine · working/, { selector: 'span.truncate' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^next:/ })).toBeNull();
  });
});

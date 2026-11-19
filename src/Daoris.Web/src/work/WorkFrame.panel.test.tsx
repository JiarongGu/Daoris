import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';

// The session panel mounted in the Work frame: an attended session is drawn by `work/session/`'s panel alone.
vi.mock('@shenora/react', async () => (await import('../test/workFrameBridge')).shenoraMock());

import i18n from '../locales/i18n';
import { eventHandlers, invoke } from '../test/workFrameBridge';
import { CHAT, DRIVEN, DRIVER_STATE, PARKED, QUESTS, REGISTRY, STRUCTURED_ROSTER, show, userEvent } from '../test/workFrameKit';

let SESSIONS: unknown[] = [DRIVEN];
let ASKS: unknown[] = [];

/** A quest an ask asked, where the go-aheads its sessions asked are held. */
const ASKED_QUEST = {
  id: 'q4sk00', from: 'ask #a5k001', to: 'engine', title: 'Ship the comparison report', body: 'Both sites need it.',
  status: 'Taken', filed: '2026-09-01T00:00:00Z', updated: '2026-09-01T00:00:00Z',
};

/** A session on that quest, parked asking the person for one go-ahead. */
const PARKED_ASKING = {
  ...PARKED, id: 'g0ah3ad0', quest: 'q4sk00', note: 'I need go-ahead 1 before I can finish.',
  noteParts: [{ words: 'I need go-ahead 1 before I can finish.', by: 'agent' }],
};

const ASK_WITH_GO_AHEAD = {
  id: 'a5k001', workspace: 'default', sentence: 'Ship the comparison report', state: 'Published', tier: 'declarations',
  asked: '2026-09-01T00:00:00Z', updated: '2026-09-02T00:00:00Z', links: [], attachments: [], proposal: [], quests: ['q4sk00'],
  goAheads: [{
    number: 1, kind: 'write', on: 'production', act: 'dashboard configuration', state: 'asked',
    asked: [{ session: 'g0ah3ad0', quest: 'q4sk00', at: '2026-09-02T00:30:00Z', why: 'The tile reads its target from it.' }],
  }],
};

function respond(url: string): Response {
  if (url.startsWith('/api/sessions')) return Response.json(SESSIONS);
  if (url.startsWith('/api/asks')) return Response.json(ASKS);
  if (url.startsWith('/api/quests')) return Response.json([...QUESTS, ASKED_QUEST]);
  if (url.startsWith('/api/registry')) return Response.json(REGISTRY);
  throw new Error(`unstubbed request: ${url}`);
}

/** A record of `count` events, the person's words first and the agent's after, at one second apart. */
const history = (session: string, count: number, earlier = false) => ({
  session, earlier, latest: count,
  events: Array.from({ length: count }, (_, index) => (index === 0
    ? { seq: 1, at: '2026-09-26T10:00:00Z', kind: 'user', origin: 'person', text: 'read it' }
    : { seq: index + 1, at: `2026-09-26T10:${String(Math.floor(index / 60)).padStart(2, '0')}:${String(index % 60).padStart(2, '0')}Z`, kind: 'message', id: `msg_${index}`, text: `said ${index}` })),
});

const panel = () => screen.findByRole('region', { name: i18n.t('work.session.label') });

describe('the session panel in the Work frame', () => {
  beforeEach(() => {
    SESSIONS = [DRIVEN];
    ASKS = [];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async () => DRIVER_STATE);
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    eventHandlers.clear();
    window.localStorage.clear();
  });

  it('draws an attended session as the panel: its head, its story in time order and its composer, and nothing of the old page', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return STRUCTURED_ROSTER;
      if (type === 'SESSION_HISTORY') return history('c0ffee11', 3);
      return DRIVER_STATE;
    });
    show('c0ffee11');

    const region = await panel();
    expect(region.querySelector('[data-region="head"]')).not.toBeNull();
    const story = await within(region).findByRole('list', { name: i18n.t('work.session.story.label') });
    await waitFor(() => expect(within(story).getAllByRole('listitem').map((item) => item.textContent)).toEqual([
      expect.stringContaining('read it'), expect.stringContaining('said 1'), expect.stringContaining('said 2'),
    ]));
    expect(within(region).getByRole('textbox', { name: i18n.t('work.session.composer.label') })).toBeInTheDocument();
    // The old page's dock, its waiting panel's handle and its box are gone.
    expect(document.querySelector('[data-dock]')).toBeNull();
    expect(document.querySelector('[data-wait-panel]')).toBeNull();
    expect(screen.queryByLabelText('Message')).toBeNull();
  });

  it('sends the composer\'s words through the session\'s own path', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return STRUCTURED_ROSTER;
      if (type === 'SESSION_QUEUE') return { session: 'c0ffee11', queued: [], taking: false };
      if (type === 'SESSION_INPUT') return { sent: true };
      return DRIVER_STATE;
    });
    show('c0ffee11');

    const box = await within(await panel()).findByRole('textbox', { name: i18n.t('work.session.composer.label') });
    await userEvent.type(box, 'look at the budget');
    await userEvent.click(within(await panel()).getByRole('button', { name: new RegExp(i18n.t('work.session.composer.send')) }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', expect.objectContaining({
      payload: expect.objectContaining({ id: 'c0ffee11', text: 'look at the budget' }),
    })));
    expect(box).toHaveValue('');
  });

  it('draws each wait as its card and answers a press through the dock\'s reader', async () => {
    SESSIONS = [PARKED_ASKING];
    ASKS = [ASK_WITH_GO_AHEAD];
    invoke.mockImplementation(async (_module: string, type: string) => (type === 'SESSION_GO_AHEAD'
      ? { message: 'Go-ahead answered.', sent: true, reaches: 'resume' }
      : DRIVER_STATE));
    show('g0ah3ad0');

    const card = await within(await panel()).findByRole('article', { name: /dashboard configuration/ });
    expect(card).toHaveTextContent('The tile reads its target from it.');
    await userEvent.click(within(card).getByRole('button', { name: i18n.t('asks.goAhead.approve') }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_GO_AHEAD', expect.objectContaining({
      payload: expect.objectContaining({ number: 1, approved: true }),
    })));
  });

  it('keeps the way through a long story: the find bar and the earlier pages', async () => {
    SESSIONS = [CHAT];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'HARNESSES') return STRUCTURED_ROSTER;
      if (type === 'SESSION_HISTORY') return history('c0ffee11', 3, true);
      return DRIVER_STATE;
    });
    show('c0ffee11');

    const region = await panel();
    expect(await within(region).findByRole('toolbar', { name: i18n.t('work.find.label') })).toBeInTheDocument();
    await userEvent.click(within(region).getByRole('button', { name: i18n.t('work.conversation.earlier') }));
    await waitFor(() => expect(invoke.mock.calls.filter(([, type]) => type === 'SESSION_HISTORY').length).toBeGreaterThan(1));
  });
});

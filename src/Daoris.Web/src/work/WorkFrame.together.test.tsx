import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// LINESTOGETHER1b (the review-flow design §2): the box says where a word goes, and a word held on a part beside the head moves to
// the head by one press. The bridge is mocked as present (D55); what the parts share is in `test/workFrameBridge.ts`.
vi.mock('@shenora/react', async () => (await import('../test/workFrameBridge')).shenoraMock());

import i18n from '../locales/i18n';
import { eventHandlers, invoke } from '../test/workFrameBridge';
import { DRIVEN, DRIVER_STATE, Hub, QUESTS, REGISTRY, ROSTER, userEvent } from '../test/workFrameKit';
import { WorkFrame } from './WorkFrame';
import { draftOf, keepDraft, readDrafts } from './conversation/drafts';

let SESSIONS: unknown[] = [];

function respond(url: string): Response {
  if (url.startsWith('/api/sessions')) return Response.json(SESSIONS);
  if (url.startsWith('/api/quests')) return Response.json(QUESTS);
  if (url.startsWith('/api/registry')) return Response.json(REGISTRY);
  throw new Error(`unstubbed request: ${url}`);
}

const ended = (id: string) => ({
  ...DRIVEN, id, state: 'completed', created: '2026-10-30T08:40:00Z', updated: '2026-10-30T09:00:00Z', note: 'Done; committed as a1b2c3d.',
});
const HEAD = 'c4c4c4c4';
const BESIDE = 'b2b2b2b2';
const WORD = { id: 'w1', text: 'use the blue one', files: [] };
const PRESS = { name: 'Say “use the blue one” to the head instead' };

/** A line of two parts: the head, and a part beside it that a quest began by asking, with a word held on the record. */
function driver(head: string | null, move: unknown = { moved: true, to: HEAD, word: 'w9' }) {
  const part = (session: string, began: string) =>
    ({ session, quest: 'abc123', shown: 'completed', group: 'review', began, after: null, askedBy: null });
  invoke.mockImplementation(async (_module: string, type: string) => {
    if (type === 'HARNESSES') return ROSTER;
    if (type === 'SESSION_QUEUE') return { session: BESIDE, queued: [WORD], taking: false, reaches: 'resume' };
    if (type === 'SESSION_QUEUE_MOVE') {
      if (move instanceof Error) throw move;
      return move;
    }
    if (type === 'SESSION_GROUPS') {
      return {
        sessions: [HEAD, BESIDE].map((session) => ({
          session, group: 'later', shown: 'completed', archived: false, teammate: false,
          ...(session === BESIDE ? { holds: { why: 'busy', reason: 'a session is active in the tree' } } : {}),
        })),
        entries: [{
          entry: `session:${HEAD}/engine`, origin: `session:${HEAD}`, repository: 'engine', group: 'later', leading: BESIDE, head,
          parts: [part(HEAD, 'opened'), part(BESIDE, 'asked')],
        }],
      };
    }
    return DRIVER_STATE;
  });
}

function show(notify: (text: string) => void = () => {}) {
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <Hub />
      <Tooltip.Provider><WorkFrame selected={BESIDE} onSelect={() => {}} notify={notify} /></Tooltip.Provider>
    </QueryClientProvider>,
  );
}

describe('where a word goes, and the move to the head', () => {
  beforeEach(() => {
    SESSIONS = [ended(HEAD), ended(BESIDE)];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  });
  afterEach(async () => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    eventHandlers.clear();
    await i18n.changeLanguage('en');
  });

  for (const [language, line] of [
    ['en', 'To this part, beside the head · after a running session here ends'],
    ['zh', '发往本部分，在头部旁 · 本机上运行中的会话结束之后'],
  ] as const) {
    it(`says where a word on a part beside the head goes, in ${language}`, async () => {
      await i18n.changeLanguage(language);
      driver(HEAD);
      show();
      expect(await screen.findByText(line)).toBeInTheDocument();
    });
  }

  it('moves a held word to the head by one press, and says so', async () => {
    driver(HEAD);
    const notify = vi.fn();
    show(notify);

    await userEvent.click(await screen.findByRole('button', PRESS));

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_QUEUE_MOVE', { payload: { session: BESIDE, id: 'w1', to: HEAD } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith('Said to the head instead.'));
  });

  it('says a lost answer is not known, never that it moved', async () => {
    driver(HEAD, { moved: false, why: 'not-known', message: 'No answer came.' });
    const notify = vi.fn();
    show(notify);

    await userEvent.click(await screen.findByRole('button', PRESS));

    expect(await screen.findByText('It is not known whether it moved. Look again, then press again.')).toBeInTheDocument();
    expect(notify).not.toHaveBeenCalledWith('Said to the head instead.');
  });

  it('carries an unsent draft to the head’s box by To the head, sending nothing', async () => {
    keepDraft(BESIDE, 'also cap it');
    driver(HEAD);
    const onSelect = vi.fn();
    render(
      <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <Hub />
        <Tooltip.Provider><WorkFrame selected={BESIDE} onSelect={onSelect} notify={() => {}} /></Tooltip.Provider>
      </QueryClientProvider>,
    );

    await userEvent.click(await screen.findByRole('button', { name: 'To the head' }));

    expect(onSelect).toHaveBeenCalledWith(HEAD);
    expect(draftOf(readDrafts(), HEAD)).toBe('also cap it');
    expect(draftOf(readDrafts(), BESIDE)).toBe('');
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_SAY', expect.anything());
  });

  it('offers no To the head with nothing typed, or on a part whose line names no head', async () => {
    driver(HEAD);
    show();
    await screen.findByText('use the blue one');
    expect(screen.queryByRole('button', { name: 'To the head' })).toBeNull();
  });

  it('offers no press, and no line, on a part whose line names no head', async () => {
    driver(null);
    show();

    await screen.findByText('use the blue one');
    expect(screen.queryByRole('button', PRESS)).toBeNull();
    expect(screen.queryByText(/beside the head/)).toBeNull();
  });
});

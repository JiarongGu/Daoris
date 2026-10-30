import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import * as Tooltip from '@radix-ui/react-tooltip';

// REVIEWCHAT1: a driven session that ended and waits for the person's *Accept…* is read as a conversation, every part of its line, and
// is driven on from its page. One part of the Work frame's suite; the bridge is mocked as present (D55) and the records are the
// driver's answers, as the page reads them.
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

const at = '2026-10-30T09:00:00Z';
const asked = (seq: number, text: string) => ({ seq, at, kind: 'user', origin: 'person', text });
const said = (seq: number, text: string) => ({ seq, at, kind: 'message', id: `m${seq}`, text });

/** The first part timed out; the carry-on finished in the same tree and waits for the person's yes. */
const FIRST = {
  ...DRIVEN, id: 'f1f1f1f1', state: 'failed', tree: 'C:/somewhere/.daoris/trees/default/engine/s-f1f1f1f1',
  created: '2026-10-30T08:00:00Z', updated: '2026-10-30T08:30:00Z',
};
const CARRIED = {
  ...DRIVEN, id: 'c4c4c4c4', state: 'completed', from: 'f1f1f1f1', fromWhy: 'carry-on', tree: FIRST.tree,
  created: '2026-10-30T08:40:00Z', updated: '2026-10-30T09:00:00Z', note: 'Done; committed as a1b2c3d.',
};
const EVENTS: Record<string, unknown[]> = {
  f1f1f1f1: [asked(1, 'build the streaming budget'), said(2, 'first part reading')],
  c4c4c4c4: [asked(1, 'carry on'), said(2, 'carry-on finished')],
};

/** The driver as the page reads it: the line's records and events, the review's gate, and what a word said now would do. */
function driver(reach: object = { reaches: 'resume' }, more: Record<string, unknown> = {}, group = 'review') {
  invoke.mockImplementation(async (_module: string, type: string, body?: { payload?: { id?: string } }) => {
    if (type in more) return more[type];
    if (type === 'HARNESSES') return ROSTER;
    if (type === 'SESSION_QUEUE') return { session: 'c4c4c4c4', queued: [], taking: false, ...reach };
    if (type === 'SESSION_INPUT') return { sent: true, reaches: 'resume' };
    if (type === 'SESSION_HISTORY') {
      const list = EVENTS[body?.payload?.id ?? ''] ?? [];
      return { session: body?.payload?.id, events: list, earlier: false, latest: list.length };
    }
    if (type === 'SESSION_GROUPS') {
      return {
        sessions: [
          { session: 'f1f1f1f1', group: 'ended', shown: 'failed', archived: false, teammate: false },
          { session: 'c4c4c4c4', group, shown: 'completed', archived: false, teammate: false,
            ...(group === 'review' ? { lands: { branch: 'daoris/s-c4c4c4c4', tree: 's-c4c4c4c4', commits: 1, uncommitted: 0 } } : {}) },
        ],
        entries: [{
          entry: 'session:f1f1f1f1/engine', origin: 'session:f1f1f1f1', repository: 'engine', group, leading: 'c4c4c4c4',
          parts: [
            { session: 'f1f1f1f1', quest: 'abc123', shown: 'failed', group: 'ended', began: 'opened', after: null, askedBy: null },
            { session: 'c4c4c4c4', quest: 'abc123', shown: 'completed', group, began: 'moved', after: 'f1f1f1f1', askedBy: null },
          ],
        }],
      };
    }
    if (type === 'LANDING') return { session: 'c4c4c4c4', form: 'branch', target: 'feature/delta', source: 'workspace' };
    return DRIVER_STATE;
  });
}

function show(selected: string) {
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <Hub />
      <Tooltip.Provider><WorkFrame selected={selected} onSelect={() => {}} notify={() => {}} /></Tooltip.Provider>
    </QueryClientProvider>,
  );
}

describe('a driven session waiting for review is driven on from its page', () => {
  beforeEach(() => {
    SESSIONS = [FIRST, CARRIED];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
  });
  afterEach(async () => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    eventHandlers.clear();
    window.localStorage.removeItem('daoris.drafts');
    await i18n.changeLanguage('en');
  });

  /** What the page says in each language: the box's placeholder, its Send, and the gate's hold on Accept… (chrome, not content). */
  const WORDS = {
    en: { placeholder: 'write to it — the same session goes on with your words', send: 'Send', accept: 'Accept…' },
    zh: { placeholder: '写给它——同一个会话会带着你的话继续', send: '发送', accept: '采纳…' },
  } as const;

  /** Each state an ended driven session waits for review in: the page draws its line and the box, and a word goes on as the record's. */
  for (const [state, group] of [['completed', 'review'], ['failed', 'review'], ['stopped', 'ended']] as const) {
    for (const language of ['en', 'zh'] as const) {
      it(`shows a ${state} session’s every part and the box in ${language}, and goes on with the words given there`, async () => {
        await i18n.changeLanguage(language);
        SESSIONS = [FIRST, { ...CARRIED, state }];
        driver({ reaches: 'resume' }, {}, group);
        show('c4c4c4c4');

        expect(await screen.findByText('carry-on finished')).toBeInTheDocument();
        expect(await screen.findByText('first part reading')).toBeInTheDocument();
        const box = await screen.findByLabelText(language === 'en' ? 'Message' : '消息');
        expect(box).toHaveAttribute('placeholder', WORDS[language].placeholder);
        if (group === 'review') {
          expect(await within(screen.getByRole('main')).findByRole('button', { name: WORDS[language].accept })).toBeInTheDocument();
        }
        await userEvent.type(box, 'also update the notes');
        await userEvent.click(screen.getByRole('button', { name: WORDS[language].send }));
        expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
          payload: { id: 'c4c4c4c4', text: 'also update the notes' },
        });
        await waitFor(() => expect((box as HTMLTextAreaElement).value).toBe(''));
      });
    }
  }
});

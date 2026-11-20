import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';

// The Work frame as a page, the bridge mocked as present: a session's rule proposal, one part of the suite
// `WorkFrame.test.tsx` names, sharing `test/workFrameBridge.ts` and `test/workFrameKit.tsx`.
vi.mock('@shenora/react', async () => (await import('../test/workFrameBridge')).shenoraMock());

import i18n from '../locales/i18n';
import { eventHandlers, invoke } from '../test/workFrameBridge';
import { DRIVEN, DRIVER_STATE, QUESTS, REGISTRY, show, userEvent } from '../test/workFrameKit';

let SESSIONS: unknown[] = [DRIVEN];

function respond(url: string): Response {
  if (url.startsWith('/api/sessions')) return Response.json(SESSIONS);
  if (url.startsWith('/api/quests')) return Response.json(QUESTS);
  if (url.startsWith('/api/registry')) return Response.json(REGISTRY);
  throw new Error(`unstubbed request: ${url}`);
}

const panel = () => screen.findByRole('region', { name: i18n.t('work.session.label') });
const headOf = async () => (await panel()).querySelector<HTMLElement>('[data-region="head"]')!;

afterEach(() => window.localStorage.clear());

/**
 * A driven session's waiting `permission_propose` is not left on the Agents page and the band while the person keeps
 * asking it in its conversation. Its own waiting proposals are a
 * wait where they talk to it, its card in the dock; *Allow* settles it through the band's own door, then tells the
 * session it is allowed, so its next turn, composed with the rule, goes on. A session that takes no words is said so instead.
 */
describe('a session\'s rule proposal', () => {
  const RESTING = { ...DRIVEN, id: 'r3st1ng0', state: 'completed', updated: '2026-10-15T10:21:00Z' };
  const proposed = '2026-10-15T10:20:00Z';
  const MINE = {
    id: 'p0000007', state: 'waiting', action: 'add', scope: 'repository', name: 'engine', list: 'allow',
    rule: 'Bash(git branch -D:*)', why: 'Auto mode refused the local branch delete you asked for.', session: 'r3st1ng0', proposed,
  };
  const OTHERS = { ...MINE, id: 'p0000008', rule: 'Bash(git push:*)', session: 'o7h3r000' };
  let reach: Record<string, unknown> = { reaches: 'resume' };
  let proposals: Record<string, unknown>[] = [MINE, OTHERS];

  beforeEach(() => {
    SESSIONS = [RESTING];
    reach = { reaches: 'resume' };
    proposals = [MINE, OTHERS];
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async (_module: string, type: string, request?: { payload?: { id?: string; accept?: boolean } }) => {
      if (type === 'RULES') return { path: 'C:/somewhere/.daoris/permissions.json', defaults: [], scopes: [], proposals };
      if (type === 'RULE_PROPOSAL') {
        proposals = proposals.map((one) => (one.id === request?.payload?.id
          ? { ...one, state: request?.payload?.accept ? 'accepted' : 'declined' } : one));
        return { path: 'C:/somewhere/.daoris/permissions.json', defaults: [], scopes: [], proposals };
      }
      if (type === 'SESSION_QUEUE') return reach;
      if (type === 'SESSION_INPUT') return { sent: true, reaches: 'resume' };
      if (type === 'SESSION_HISTORY') {
        return {
          session: 'r3st1ng0', earlier: false, latest: 4, events: [
            { seq: 1, at: proposed, kind: 'user', text: 'Delete the merged branch and its worktree.' },
            {
              seq: 2, at: proposed, kind: 'tool', id: 't2', title: 'permission_propose', status: 'completed',
              input: JSON.stringify({ action: 'add', scope: 'repository', list: 'allow', rule: 'Bash(git branch -D:*)', why: MINE.why }),
            },
            { seq: 3, at: proposed, kind: 'message', id: 'm3', text: 'I proposed a rule; once it is allowed I can delete it.' },
            { seq: 4, at: proposed, kind: 'turn', stopReason: 'end_turn' },
          ],
        };
      }
      return DRIVER_STATE;
    });
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    eventHandlers.clear();
  });

  const said = () => invoke.mock.calls.filter(([, type]) => type === 'SESSION_INPUT');
  const ruleCard = async () => within(await panel()).findByRole('article', { name: 'a rule it proposed' });

  it('stands in the dock, not the head, and not another session\'s', async () => {
    show('r3st1ng0');
    const card = await ruleCard();
    expect(card.closest('[data-region="dock"]')).not.toBeNull();
    expect(within(card).getByRole('listitem')).toHaveTextContent('allow Bash(git branch -D:*) for repository engine');
    expect(await headOf()).not.toHaveTextContent(/Bash\(git branch/);
    expect(screen.queryByText(/Bash\(git push:\*\)/)).toBeNull();
  });

  it('allows it after asking once, then tells the session it is allowed, so its next turn goes on', async () => {
    const notify = vi.fn();
    show('r3st1ng0', notify);
    const item = within(await ruleCard()).getByRole('listitem');

    await userEvent.click(within(item).getByRole('button', { name: 'Allow…' }));
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'RULE_PROPOSAL', expect.anything());
    await userEvent.click(within(item).getByRole('button', { name: 'Allow' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RULE_PROPOSAL', { payload: { id: 'p0000007', accept: true } }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_INPUT', {
      payload: {
        id: 'r3st1ng0',
        text: 'The rule you proposed is allowed now: allow Bash(git branch -D:*) for repository engine. Go on with what you needed it for.',
      },
    }));
    // Settled before it is said: the turn the words start is composed with the rule.
    const order = invoke.mock.calls.map(([, type]) => type).filter((type) => type === 'RULE_PROPOSAL' || type === 'SESSION_INPUT');
    expect(order).toEqual(['RULE_PROPOSAL', 'SESSION_INPUT']);
    expect(notify).toHaveBeenCalledWith('Allowed: allow Bash(git branch -D:*) for repository engine. Sessions started from now on are handed it.');
    await waitFor(() => expect(notify).toHaveBeenCalledWith('Session r3st1ng0 is told the rule is allowed, and goes on.', 'ok'));
    // Settled, it waits no more.
    await waitFor(() => expect(screen.queryByRole('article', { name: 'a rule it proposed' })).toBeNull());
  });

  it('declines it in one press, telling the session nothing', async () => {
    const notify = vi.fn();
    show('r3st1ng0', notify);
    await userEvent.click(within(await ruleCard()).getByRole('button', { name: 'Decline' }));

    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RULE_PROPOSAL', { payload: { id: 'p0000007', accept: false } }));
    expect(notify).toHaveBeenCalledWith('Declined: allow Bash(git branch -D:*) for repository engine. The rules are as they were.');
    expect(said()).toEqual([]);
  });

  it('says on the card where the session takes no words, and allows it without telling it', async () => {
    reach = { sent: false, why: 'stood-down' };
    show('r3st1ng0');
    const item = within(await ruleCard()).getByRole('listitem');
    await waitFor(() => expect(item).toHaveTextContent('It takes no words now, so it is not told'));

    await userEvent.click(within(item).getByRole('button', { name: 'Allow…' }));
    await userEvent.click(within(item).getByRole('button', { name: 'Allow' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'RULE_PROPOSAL', { payload: { id: 'p0000007', accept: true } }));
    await waitFor(() => expect(screen.queryByRole('article', { name: 'a rule it proposed' })).toBeNull());
    expect(said()).toEqual([]);
  });

  it('leads between the proposal and the call that raised it', async () => {
    show('r3st1ng0');
    const card = await ruleCard();
    const story = await within(await panel()).findByRole('list', { name: i18n.t('work.session.story.label') });
    const block = await waitFor(() => story.querySelector<HTMLElement>('[data-part="tools"]')!.closest<HTMLElement>('[data-block]')!);
    const mark = await within(block).findByRole('button', { name: 'To the wait: a rule it proposed' });

    await userEvent.click(await within(card).findByRole('button', { name: 'In the conversation' }));
    await waitFor(() => expect(block.className).toContain('outline-accent'));
    await userEvent.click(mark);
    // Its card in the dock takes the focus, found by its key.
    await waitFor(() => expect(card).toHaveFocus());
  });
});

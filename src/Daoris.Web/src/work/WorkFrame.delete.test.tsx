import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';

// Deleting an ended conversation from its page header, in the Work frame, one part of its suite (the parts run beside each
// other); what the parts share is in `test/workFrameBridge.ts` and `test/workFrameKit.tsx`.
vi.mock('@shenora/react', async () => (await import('../test/workFrameBridge')).shenoraMock());

import { eventHandlers, invoke } from '../test/workFrameBridge';
import { CHAT, DRIVEN, DRIVER_STATE, QUESTS, REGISTRY, show, userEvent } from '../test/workFrameKit';

let SESSIONS: unknown[] = [DRIVEN];

function respond(url: string): Response {
  if (url.startsWith('/api/sessions')) return Response.json(SESSIONS);
  if (url.startsWith('/api/quests')) return Response.json(QUESTS);
  if (url.startsWith('/api/registry')) return Response.json(REGISTRY);
  throw new Error(`unstubbed request: ${url}`);
}

describe('deleting an ended conversation from its page header', () => {
  beforeEach(() => {
    vi.stubGlobal('fetch', vi.fn(async (input: RequestInfo | URL) => respond(String(input))));
    invoke.mockImplementation(async () => DRIVER_STATE);
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    invoke.mockReset();
    eventHandlers.clear();
    window.localStorage.clear();
  });

  /**
   * An ended conversation the reader says is deletable offers *Delete…* in its header's ⋯; it asks
   * once under the header, deletes nothing until its second press, and a driven session's ⋯ offers none.
   */
  it('deletes an ended conversation from its header’s ⋯, asking once, and offers a driven session none', async () => {
    const ended = { ...CHAT, state: 'completed' };
    SESSIONS = [ended, { ...DRIVEN, state: 'completed' }];
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_GROUPS') {
        return { sessions: [
          { session: 'c0ffee11', group: 'done', state: 'done', shown: 'completed', archived: false, teammate: false, deletable: true },
          { session: 's1a2b3c4', group: 'done', state: 'done', shown: 'completed', archived: false, teammate: false, deletable: false },
        ] };
      }
      if (type === 'SESSION_DELETE') return { deleted: 'c0ffee11', removed: ['record'] };
      return DRIVER_STATE;
    });
    const notify = vi.fn();
    show('c0ffee11', notify);

    const user = userEvent.setup();
    const acts = within(await screen.findByRole('group', { name: 'Session actions' }));
    await waitFor(() => expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'STATE_SLICE', { payload: { slice: 'groups' } }));
    acts.getByRole('button', { name: 'More actions' }).focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('menuitem', { name: 'Delete…' }));

    const ask = screen.getByRole('group', { name: 'delete this session' });
    expect(ask).toHaveTextContent('Nothing brings it back.');
    // Opened from the ⋯, its sentence takes the focus, and *Never mind* gives it back to the ⋯.
    await waitFor(() => expect(within(ask).getByText(/^Deletes this conversation’s record/)).toHaveFocus());
    await user.click(within(ask).getByRole('button', { name: 'Never mind' }));
    expect(screen.queryByRole('group', { name: 'delete this session' })).toBeNull();
    expect(acts.getByRole('button', { name: 'More actions' })).toHaveFocus();
    expect(invoke).not.toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_DELETE', expect.anything());

    acts.getByRole('button', { name: 'More actions' }).focus();
    await user.keyboard('{Enter}');
    await user.click(await screen.findByRole('menuitem', { name: 'Delete…' }));
    await user.click(screen.getByRole('button', { name: 'Delete session' }));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SESSION_DELETE', { payload: { id: 'c0ffee11' } });
    await waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringMatching(/^Deleted c0ffee11/)));
  });
});

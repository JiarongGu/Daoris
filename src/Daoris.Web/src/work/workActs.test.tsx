import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';

// The one owner of a pause, a resume and an abandon (PAUSE1e, D132 §7.1): whichever door pressed it, the ask's page, the
// quest's page or a session's header, the act runs here and says how it went. Over a mocked bridge, as an organism is held.

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn() }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => true,
  getBridge: () => ({ isAvailable: true, invoke }),
  useShenora: () => ({ isAvailable: true }),
  useShenoraEvent: () => undefined,
}));

import i18n from '../i18n';
import { ABANDON_ANSWER } from './pausingFixtures';
import { useWorkActs } from './workActs';

function acts() {
  const notify = vi.fn();
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const wrapper = ({ children }: { children: ReactNode }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  const { result } = renderHook(() => useWorkActs({ notify }), { wrapper });
  return { result, notify };
}

const refusal = (code: string, parameters: Record<string, string>) => Object.assign(new Error(code), { code, parameters });

describe('the acts on a work', () => {
  afterEach(async () => {
    invoke.mockReset();
    await i18n.changeLanguage('en');
  });

  it('pauses, says what it stopped, and names a session it could not stop as a failure', async () => {
    invoke.mockImplementation(async () => ({
      scope: 'ask', id: 'a1', did: 'paused', already: false,
      stopped: [{ session: 's1', quest: 'q1' }], kept: [{ session: 's2', quest: 'q2', why: 'unanswered' }],
    }));
    const { result, notify } = acts();
    const done = vi.fn();

    act(() => result.current.pause({ scope: 'ask', id: 'a1' }, done));

    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      'Paused ask #a1 and stopped 1 session: nothing of it starts on this machine until you resume it.', 'ok'));
    expect(notify).toHaveBeenCalledWith('Could not stop session s2: Stop… on its page asks again.', 'error');
    expect(done).toHaveBeenCalledOnce();
  });

  it('resumes, and says the one quest still held by its hold’s own sentence, translated by its verdict', async () => {
    invoke.mockImplementation(async () => ({
      scope: 'quest', id: 'q1', did: 'resumed', released: [{ quest: 'q1', session: 's1' }],
      holds: [{ quest: 'q1', verdict: 'Held', reason: '`engine` is held on this machine by you.' }],
    }));
    await i18n.changeLanguage('zh');
    const { result, notify } = acts();

    act(() => result.current.resume({ scope: 'quest', id: 'q1' }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith('已恢复委托 #q1：驱动的下一轮会照常安排它的工作。', 'ok'));
    expect(notify).toHaveBeenCalledWith('#q1 仍在搁置：受托方已被你暂停。', 'ok');
  });

  it('abandons with the reason and the pieces, and says how many went', async () => {
    invoke.mockImplementation(async () => ABANDON_ANSWER);
    const { result, notify } = acts();
    const done = vi.fn();

    act(() => result.current.abandon({ scope: 'ask', id: 'a1b2c3' }, 'It went the wrong way.', ['quest:9a8b7c'], done));

    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      'Abandoned ask #a1b2c3: 5 of 6 pieces; 1 changed since the list and stayed.', 'ok'));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'WORK_ABANDON', expect.objectContaining({
      payload: { ask: 'a1b2c3', reason: 'It went the wrong way.', pieces: ['quest:9a8b7c'] },
    }));
    expect(done).toHaveBeenCalledWith(ABANDON_ANSWER);
  });

  it('says a refusal in the catalogue’s words', async () => {
    invoke.mockImplementation(async () => { throw refusal('WORK_UNKNOWN', { id: 'zz' }); });
    const { result, notify } = acts();

    act(() => result.current.pause({ scope: 'ask', id: 'zz' }));

    await waitFor(() => expect(notify).toHaveBeenCalledWith('There is no ask #zz on this machine.', 'error'));
  });
});

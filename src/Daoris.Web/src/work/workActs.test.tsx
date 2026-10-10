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
import type { ResumeHold } from './pausing';
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

    act(() => result.current.pause({ scope: 'ask', id: 'a1' }, undefined, done));

    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      'Paused ask #a1 and stopped 1 session: nothing of it starts on this machine until you resume it.', 'ok'));
    expect(notify).toHaveBeenCalledWith('Could not stop session s2: Stop… on its page asks again.', 'error');
    expect(done).toHaveBeenCalledOnce();
  });

  it('tells a refused pause to the ask it came from, and to a toast where there is none (UXFIX2b2a)', async () => {
    invoke.mockImplementation(async () => { throw new Error('The driver is not running.'); });
    const { result, notify } = acts();
    const answered = { done: vi.fn(), refused: vi.fn() };

    act(() => result.current.pause({ scope: 'ask', id: 'a1' }, answered));
    await waitFor(() => expect(answered.refused).toHaveBeenCalledOnce());
    expect(answered.refused.mock.calls[0]![0]).toContain('The driver is not running.');
    expect(answered.done).not.toHaveBeenCalled();
    expect(notify).not.toHaveBeenCalled();

    act(() => result.current.abandon({ scope: 'ask', id: 'a1' }, 'why', ['quest:q1'], answered));
    await waitFor(() => expect(answered.refused).toHaveBeenCalledTimes(2));

    act(() => result.current.pause({ scope: 'ask', id: 'a1' }));
    await waitFor(() => expect(notify).toHaveBeenCalledOnce());
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

  /**
   * CARRY2d (D80's CARRY2c note): a resume's report of what still holds a quest of its work carried the verdict and the
   * driver's English alone, so 中文 said a stop, a pause, a park and a take elsewhere in English. The host now hands each
   * hold's facts beside the sentence, as the tick hands a sitting quest's, and the page words each from both catalogues; a
   * host older than the facts hands none, and the driver's sentence stands, as English always passes it through.
   */
  describe('a resume says what still holds a quest of it in the reader’s language', () => {
    const STOP = 'you stopped session `b3f0re00`; Try again carries it on — `daoris driver retry q8 --session b3f0re00`.';
    const PAUSE_QUEST = 'you paused `#q9`; Resume starts it — `daoris-driver quest resume q9`.';
    const PAUSE_ASK = 'paused with ask `#b2`; Resume starts it — `daoris-driver ask --resume b2`.';
    const PARK = '3 session(s) have failed on `#q10` without landing anything — parked, because trying again spends an account rather than making progress. `daoris driver retry q10` starts it again once you know why.';
    const OVER = 'the take is theirs, so session `cut0ff00` is not carried on over it.';
    const TEAMMATE = `Quest \`#q11\` is taken on \`laptop\`, by session \`laptop/s7\`: ${OVER}`;
    const ANOTHER = `Quest \`#q11\` is taken on another machine: ${OVER}`;
    const HERE = `Quest \`#q11\` was taken here after session \`cut0ff00\` ended, by a chat or by work outside Daoris: ${OVER}`;
    const HOLDS: { name: string; hold: ResumeHold; zh: string }[] = [
      {
        name: 'the person’s stop, by its session',
        hold: { quest: 'q8', verdict: 'Stopped', reason: STOP, heldBy: 'b3f0re00' },
        zh: '#q8 仍在搁置：你停止了会话 `b3f0re00`，这条委托被留在本机，直到你选择重试——`daoris driver retry q8 --session b3f0re00`。',
      },
      {
        name: 'the quest’s own pause',
        hold: { quest: 'q9', verdict: 'Paused', reason: PAUSE_QUEST, pausedBy: { scope: 'quest', id: 'q9' } },
        zh: '#q9 仍在搁置：随委托 `#q9` 暂缓；恢复后会继续——`daoris-driver quest resume q9`。',
      },
      {
        name: 'another ask’s pause',
        hold: { quest: 'q9', verdict: 'Paused', reason: PAUSE_ASK, pausedBy: { scope: 'ask', id: 'b2' } },
        zh: '#q9 仍在搁置：已随需求 `#b2` 暂缓；恢复后会继续——`daoris-driver ask --resume b2`。',
      },
      {
        name: 'a park, by how many failed',
        hold: { quest: 'q10', verdict: 'Exhausted', reason: PARK, strikes: 3 },
        zh: '#q10 仍在搁置：`#q10` 上已有 3 个会话失败，且没有落地任何工作——已挂起，因为继续尝试只会消耗账户，而不会有进展。弄清原因后，`daoris driver retry q10` 会让它重新开始。',
      },
      {
        name: 'a teammate’s take, by its machine and session',
        hold: { quest: 'q11', verdict: 'TakenElsewhere', reason: TEAMMATE, takenBy: { machine: 'laptop', session: 'laptop/s7', here: false, last: 'cut0ff00' } },
        zh: '#q11 仍在搁置：委托 `#q11` 已在 laptop 上由会话 `laptop/s7` 接下：这次接下属于对方，所以本机不会接续会话 `cut0ff00`。',
      },
      {
        name: 'a take on another machine no record names',
        hold: { quest: 'q11', verdict: 'TakenElsewhere', reason: ANOTHER, takenBy: { machine: null, session: null, here: false, last: 'cut0ff00' } },
        zh: '#q11 仍在搁置：委托 `#q11` 已在另一台机器上接下：这次接下属于对方，所以本机不会接续会话 `cut0ff00`。',
      },
      {
        name: 'a take made here after its session ended',
        hold: { quest: 'q11', verdict: 'TakenElsewhere', reason: HERE, takenBy: { machine: null, session: null, here: true, last: 'cut0ff00' } },
        zh: '#q11 仍在搁置：委托 `#q11` 在会话 `cut0ff00` 结束后才在本机被接下，接下它的是一次聊天或 Daoris 之外的工作：这次接下不属于该会话，所以不会接续会话 `cut0ff00`。',
      },
    ];

    const resumeWith = async (hold: ResumeHold) => {
      invoke.mockImplementation(async () => ({ scope: 'ask', id: 'a1', did: 'resumed', released: [], holds: [hold] }));
      const { result, notify } = acts();
      act(() => result.current.resume({ scope: 'ask', id: 'a1' }));
      await waitFor(() => expect(notify).toHaveBeenCalledTimes(2));
      return notify.mock.calls[1];
    };

    it.each(HOLDS)('in 中文 from its facts: $name', async ({ hold, zh }) => {
      await i18n.changeLanguage('zh');
      expect(await resumeWith(hold)).toEqual([zh, 'ok']);
    });

    it.each(HOLDS)('in the driver’s words where an older host hands no facts: $name', async ({ hold }) => {
      await i18n.changeLanguage('zh');
      const bare: ResumeHold = { quest: hold.quest, verdict: hold.verdict, reason: hold.reason };
      expect(await resumeWith(bare)).toEqual([`#${hold.quest} 仍在搁置：${hold.reason}`, 'ok']);
    });

    it.each(HOLDS)('in English as the driver’s own sentence: $name', async ({ hold }) => {
      expect(await resumeWith(hold)).toEqual([`#${hold.quest} still sits: ${hold.reason}`, 'ok']);
    });
  });

  it('abandons with the reason and the pieces, and says how many went', async () => {
    invoke.mockImplementation(async () => ABANDON_ANSWER);
    const { result, notify } = acts();
    const done = vi.fn();

    act(() => result.current.abandon({ scope: 'ask', id: 'a1b2c3' }, 'It went the wrong way.', ['quest:9a8b7c'], undefined, done));

    // A partial abandon, said in the error's tone (PAUSE1h).
    await waitFor(() => expect(notify).toHaveBeenCalledWith(
      'Abandoned ask #a1b2c3: 5 of 6 pieces; 1 changed since the list and stayed.', 'error'));
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

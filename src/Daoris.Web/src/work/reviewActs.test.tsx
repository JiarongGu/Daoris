import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';

// The one owner of a review's presses (REVIEWENV1g): over a mocked bridge and a stubbed service, as an organism is held
// (components plan §3).

const { invoke, shell } = vi.hoisted(() => ({ invoke: vi.fn(), shell: { here: true } }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => shell.here,
  getBridge: () => ({ isAvailable: shell.here, invoke }),
  useShenora: () => ({ isAvailable: shell.here }),
  useShenoraEvent: () => undefined,
}));

import i18n from '../i18n';
import type { Answered } from './InlineConfirm';
import { useReviewActs } from './reviewActs';
import { STEP_SHOWN } from './reviewFixtures';

/** What the stubbed service was sent, in order: where, and the body. */
const posted = () => vi.mocked(fetch).mock.calls.map(([path, init]) => ({
  path: String(path), body: init?.body ? JSON.parse(String(init.body)) : undefined,
}));

function acts() {
  const notify = vi.fn();
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  const wrapper = ({ children }: { children: ReactNode }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  const { result } = renderHook(() => useReviewActs({ notify }), { wrapper });
  return { result, notify };
}

const answered = () => ({ done: vi.fn<Answered['done']>(), refused: vi.fn<Answered['refused']>() });

/**
 * REVIEWENV1g (D154 point 8; the review environment design §3.3–§3.4, §3.6): each press of a review goes to its door, names the
 * set-up the view drew, and says the host's sentence; a *not yet*'s words go to the step's session as its next turn; a stale
 * verdict is refused inside the gate that was pressed.
 */
describe("a review's presses", () => {
  beforeEach(() => {
    shell.here = true;
    invoke.mockImplementation(async () => ({ drivable: [], holds: [], running: [] }));
    vi.stubGlobal('fetch', vi.fn(async () => Response.json({ quest: STEP_SHOWN, message: 'Reviewed set-up step `#q2` in `local`.' })));
  });
  afterEach(async () => {
    invoke.mockReset();
    vi.unstubAllGlobals();
    await i18n.changeLanguage('en');
  });

  it('sends a reviewed with the set-up the view drew, says the host\'s sentence, and asks the driver to look now', async () => {
    const { result, notify } = acts();
    const told = answered();

    act(() => result.current.actsFor({ state: 'shown', step: 'q2', record: STEP_SHOWN, work: 'q1' })
      .reviewed!({ machine: 'desk', sequence: 7 }, told));

    await waitFor(() => expect(told.done).toHaveBeenCalled());
    expect(posted()).toEqual([{ path: '/api/quests/q2/review', body: { verdict: 'reviewed', setUp: { machine: 'desk', sequence: 7 } } }]);
    expect(notify).toHaveBeenCalledWith('Reviewed set-up step `#q2` in `local`.');
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'NUDGE', expect.anything());
  });

  it("refuses a stale verdict inside the gate, in the host's sentence", async () => {
    const stale = 'Set-up step `#q2` was shown again since that set-up, at `f0e1d2c3`: review the newest. Nothing was kept.';
    vi.stubGlobal('fetch', vi.fn(async () => Response.json({ error: stale }, { status: 409 })));
    const { result, notify } = acts();
    const told = answered();

    act(() => result.current.actsFor({ state: 'shown', step: 'q2', record: STEP_SHOWN }).reviewed!({ machine: 'desk', sequence: 7 }, told));

    await waitFor(() => expect(told.refused).toHaveBeenCalledWith(stale));
    expect(told.done).not.toHaveBeenCalled();
    expect(notify).not.toHaveBeenCalled();
  });

  it("sends a not yet with the person's words, then says them to the set-up's session as its next turn", async () => {
    const { result, notify } = acts();
    const told = answered();

    act(() => result.current.actsFor({ state: 'shown', step: 'q2', record: STEP_SHOWN })
      .notYet!({ machine: 'desk', sequence: 7 }, 'The label still reads the old name.', told));

    await waitFor(() => expect(notify).toHaveBeenCalledWith('Your words went to session s9 as its next turn.'));
    expect(posted()[0]).toEqual({
      path: '/api/quests/q2/review',
      body: { verdict: 'not-yet', words: 'The label still reads the old name.', setUp: { machine: 'desk', sequence: 7 } },
    });
    expect(invoke).toHaveBeenCalledWith(expect.any(String), 'SESSION_INPUT', { payload: { id: 's9', text: 'The label still reads the old name.' } });
    expect(told.done).toHaveBeenCalled();
  });

  it("says where a not yet's words could not reach the session, and how to say them again", async () => {
    invoke.mockImplementation(async (_module: string, type: string) => {
      if (type === 'SESSION_INPUT') throw Object.assign(new Error('gone'), { code: 'SESSION_GONE' });
      return {};
    });
    const { result, notify } = acts();

    act(() => result.current.actsFor({ state: 'shown', step: 'q2', record: STEP_SHOWN })
      .notYet!({ machine: 'desk', sequence: 7 }, 'Not the right page.', answered()));

    await waitFor(() => expect(notify).toHaveBeenCalledWith(expect.stringMatching(/daoris-driver sessions say s9/), 'error'));
  });

  it('gives a skip on the set-up step, and on the work once the step was reviewed', async () => {
    const { result } = acts();
    const first = answered();
    const second = answered();

    act(() => result.current.actsFor({ state: 'shown', step: 'q2', work: 'q1' }).skip!('Only the docs changed.', first));
    await waitFor(() => expect(first.done).toHaveBeenCalled());
    act(() => result.current.actsFor({ state: 'not-held', step: 'q2', work: 'q1' }).skip!(null, second));
    await waitFor(() => expect(second.done).toHaveBeenCalled());

    expect(posted()).toEqual([
      { path: '/api/quests/q2/review', body: { verdict: 'skipped', words: 'Only the docs changed.' } },
      { path: '/api/quests/q1/review', body: { verdict: 'skipped' } },
    ]);
  });

  it('publishes a set-up step following the work, in the environment pressed', async () => {
    const { result } = acts();
    const told = answered();

    act(() => result.current.actsFor({ state: 'not-shown', work: 'q1' }).setUp!('local', told));

    await waitFor(() => expect(told.done).toHaveBeenCalled());
    expect(posted()).toEqual([{ path: '/api/quests/q1/set-up-step', body: { environment: 'local' } }]);
  });

  it("shows a set-up again through the shell, and says so", async () => {
    const { result, notify } = acts();

    act(() => result.current.actsFor({ state: 'shown', step: 'q2' }).showAgain!());

    await waitFor(() => expect(notify).toHaveBeenCalledWith("Shown again in Daoris's browser: #q2"));
    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'SHOW_REVIEW_AGAIN', { payload: { quest: 'q2' } });
  });

  it('offers no not yet and no show again in a browser, which has no shell to reach a session or a tab', () => {
    shell.here = false;
    const { result } = acts();

    const offered = result.current.actsFor({ state: 'shown', step: 'q2', record: STEP_SHOWN, work: 'q1' });

    expect(Object.keys(offered).sort()).toEqual(['reviewed', 'setUp', 'skip']);
  });
});

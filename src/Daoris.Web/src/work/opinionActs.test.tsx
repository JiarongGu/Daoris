import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';

// The one owner of the second opinion's presses (XAGENT1g): over a mocked bridge, as an organism is held (components plan §3).

const { invoke, shell } = vi.hoisted(() => ({ invoke: vi.fn(), shell: { here: true } }));

vi.mock('@shenora/react', () => ({
  isShenoraAvailable: () => shell.here,
  getBridge: () => ({ isAvailable: shell.here, invoke }),
  useShenora: () => ({ isAvailable: shell.here }),
  useShenoraEvent: () => undefined,
}));

import i18n from '../i18n';
import type { Answered } from './InlineConfirm';
import { useOpinionActs } from './opinionActs';
import { GATES } from './opinionFixtures';

function acts() {
  const notify = vi.fn();
  const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  const wrapper = ({ children }: { children: ReactNode }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  const { result } = renderHook(() => useOpinionActs({ notify }), { wrapper });
  return { result, notify };
}

const answered = () => ({ done: vi.fn<Answered['done']>(), refused: vi.fn<Answered['refused']>() });

/** Each route the driver was asked, with its payload. */
const sent = () => invoke.mock.calls.map(([, type, options]) => ({ type, payload: (options as { payload?: unknown })?.payload }));

/**
 * XAGENT1g (D155 point 10; the second-agent design §8.5, §9): each press at the second opinion's gate goes to the driver's own
 * press, says its sentence, and a refusal is said inside the gate that was pressed; *Send back…* is the person's words to the
 * working session.
 */
describe("the second opinion's presses", () => {
  beforeEach(() => {
    shell.here = true;
    invoke.mockImplementation(async () => ({ session: 's42', done: true, message: 'A second opinion on session s42\'s work is asked.' }));
  });
  afterEach(async () => {
    invoke.mockReset();
    await i18n.changeLanguage('en');
  });

  it('asks a pass, or the same agent fresh, and says the driver’s sentence', async () => {
    const { result, notify } = acts();
    const first = answered();
    const second = answered();

    act(() => result.current.actsFor({ session: 's42', gate: GATES.notAsked }).ask!(first));
    await waitFor(() => expect(first.done).toHaveBeenCalled());
    act(() => result.current.actsFor({ session: 's42', gate: GATES.unavailableRequired }).sameAgent!(second));
    await waitFor(() => expect(second.done).toHaveBeenCalled());

    expect(sent()).toEqual([
      { type: 'ASK_OPINION', payload: { id: 's42' } },
      { type: 'ASK_OPINION', payload: { id: 's42', sameAgent: true } },
    ]);
    expect(notify).toHaveBeenCalledWith("A second opinion on session s42's work is asked.");
  });

  it('keeps the person’s answer with their words, and says a refusal inside the gate in the driver’s sentence', async () => {
    const refusal = "nothing waits for a second opinion on session s42's work: Second opinion settled.";
    invoke.mockImplementationOnce(async () => ({ session: 's42', done: true, message: 'You went on without a settled second opinion.' }))
      .mockImplementationOnce(async () => ({ session: 's42', done: false, message: refusal }));
    const { result, notify } = acts();
    const anyway = answered();
    const myself = answered();

    act(() => result.current.actsFor({ session: 's42', gate: GATES.disputed }).anyway!('I read the bound.', anyway));
    await waitFor(() => expect(anyway.done).toHaveBeenCalled());
    act(() => result.current.actsFor({ session: 's42', gate: GATES.disputed }).myself!(null, myself));
    await waitFor(() => expect(myself.refused).toHaveBeenCalledWith(refusal));

    expect(sent()).toEqual([
      { type: 'OPINION_ANYWAY', payload: { id: 's42', words: 'I read the bound.' } },
      { type: 'OPINION_MYSELF', payload: { id: 's42' } },
    ]);
    expect(notify).toHaveBeenCalledTimes(1);
    expect(myself.done).not.toHaveBeenCalled();
  });

  it('stops the reviewer reading the opinion drawn, and offers no stop where none is named', async () => {
    const { result } = acts();
    const told = answered();

    act(() => result.current.actsFor({ session: 's42', gate: GATES.reading }).stop!(told));
    await waitFor(() => expect(told.done).toHaveBeenCalled());

    expect(sent()).toEqual([{ type: 'STOP_OPINION', payload: { opinion: 'o1' } }]);
    expect(result.current.actsFor({ session: 's42', gate: GATES.notAsked }).stop).toBeUndefined();
  });

  it("sends back the person's words to the working session as its next turn", async () => {
    const { result, notify } = acts();
    const told = answered();

    act(() => result.current.actsFor({ session: 's42', gate: GATES.disputed }).sendBack!('Add the test for a full page.', told));

    await waitFor(() => expect(told.done).toHaveBeenCalled());
    expect(invoke).toHaveBeenCalledWith(expect.any(String), 'SESSION_INPUT', { payload: { id: 's42', text: 'Add the test for a full page.' } });
    expect(notify).toHaveBeenCalledWith('Your words went to session s42 as its next turn.');
  });

  it('offers nothing in a browser, which has no shell to reach the driver', () => {
    shell.here = false;
    const { result } = acts();

    expect(result.current.actsFor({ session: 's42', gate: GATES.disputed, openSession: () => {} })).toEqual({});
  });
});

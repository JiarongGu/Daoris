import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';

// The standing confirmation over the bridge (TERMTRUST1a, D201 points 5 and 7): STATE's `standingConfirmation` read field by field,
// the window's grant or revoke (`SET_STANDING_CONFIRMATION`), heard through the state hub as every press since STATEHUB1h, and a
// press run under it (`STANDING_USED`) reading the driver's state again.

const { invoke, heard } = vi.hoisted(() => ({
  invoke: vi.fn(),
  // The state hub's ear: what the mocked shell publishes after a press reaches it, as the kit's pump would; and the shell's events.
  heard: new Map<string, (payload: unknown) => void>(),
}));

vi.mock('@shenora/react', async () => {
  const { publishing } = await import('../test/publishing');
  const published = publishing(invoke, (type, payload) => heard.get(type)?.(payload));
  return {
    isShenoraAvailable: () => true,
    getBridge: () => ({ isAvailable: true, invoke: published }),
    useShenora: () => ({ isAvailable: true, bridge: {} }),
    useShenoraEvent: (module: string, type: string, handler: (payload: unknown) => void) => {
      if (module === 'DAORIS') heard.set(type, handler);
    },
  };
});

import { keys } from '../data/queries';
import { standingOf, useSetStandingConfirmation, useStandingUsed } from './person';
import { useStateHub } from './state/stateHub';

const wrapped = () => {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  client.setQueryData(keys.driver, { held: true });
  const wrapper = ({ children }: { children: ReactNode }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  return { wrapper, invalid: () => client.getQueryState(keys.driver)?.isInvalidated ?? false };
};

describe('the standing confirmation over the bridge', () => {
  afterEach(() => {
    invoke.mockReset();
    heard.clear();
  });

  it('reads STATE’s standingConfirmation field by field, a null the bridge left out read as none', () => {
    expect(standingOf({
      offered: true, on: true, presses: ['stop', 'hold'], built: ['stop', 'finish', 'hold', 'resume'], since: '2026-10-23T09:30:00Z',
      unread: null, unwritten: false, used: 2, held: ['engine'],
    })).toEqual({
      offered: true, on: true, presses: ['stop', 'hold'], built: ['stop', 'finish', 'hold', 'resume'], since: '2026-10-23T09:30:00Z',
      unread: null, unwritten: false, used: 2, held: ['engine'], appointmentsOffered: false, appointments: [],
      supervisorOffered: false, stewardOffered: false, holders: [],
    });
    // SUPERVISOR1a-ii: the owner's appointments, each read field by field; one with no role or repository left out.
    expect(standingOf({
      appointmentsOffered: true,
      appointments: [
        { role: 'supervisor', repository: 'engine', holder: 'terminal', since: '2026-10-24T14:02:00Z', dormant: true, answers: 0 },
        { role: 'supervisor' }, 'engine',
      ],
    })).toMatchObject({
      appointmentsOffered: true,
      appointments: [{
        role: 'supervisor', repository: 'engine', holder: 'terminal', since: '2026-10-24T14:02:00Z', dormant: true, answers: 0,
        scope: 'repository:engine', holderId: null,
      }],
    });
    // STEWARDROLE1a-i (D209 §2.2 to §2.4): each role's own flag, the living holders, and an appointment's scope, the steward's
    // `machine` naming no repository.
    expect(standingOf({
      supervisorOffered: false, stewardOffered: true,
      holders: [{ id: 'h1', since: '2026-10-24T14:02:00Z', roles: ['steward', 'supervisor'] }, { roles: ['steward'] }],
      appointments: [{ role: 'steward', scope: 'machine', holder: 'terminal', since: '2026-10-24T14:02:00Z', dormant: false, answers: 2, holderId: 'h1' }],
    })).toMatchObject({
      supervisorOffered: false,
      stewardOffered: true,
      holders: [{ id: 'h1', since: '2026-10-24T14:02:00Z', roles: ['steward', 'supervisor'] }],
      appointments: [{ role: 'steward', repository: '', scope: 'machine', holderId: 'h1', answers: 2 }],
    });
    // The bridge leaves its nulls out: no since, no unread.
    expect(standingOf({ offered: false, on: false, presses: [], built: ['stop'], unwritten: true, used: 0, held: [] }))
      .toMatchObject({ offered: false, on: false, since: null, unread: null, unwritten: true });
    expect(standingOf({ unread: 'other-machine' })?.unread).toBe('other-machine');
    // A reason this page does not know is no reason it says, and on with no press is off.
    expect(standingOf({ unread: 'sideways', on: true, presses: [] })).toMatchObject({ unread: null, on: false });
    expect(standingOf({ presses: ['stop', 7, null], used: 'many' })).toMatchObject({ presses: ['stop'], used: 0 });
    // An older shell, or one serving no pipe, says none.
    expect(standingOf(undefined)).toBeNull();
    expect(standingOf(null)).toBeNull();
    expect(standingOf([])).toBeNull();
  });

  it('sends the presses ticked, and none to turn it off, and reads a refusal by its code', async () => {
    invoke.mockResolvedValueOnce({ done: true })
      .mockResolvedValueOnce({ done: false, why: 'not-offered', message: 'Not yet.' })
      .mockResolvedValueOnce({ done: true, why: 'unwritten', message: 'Turned off for this run.' });
    const { wrapper } = wrapped();
    const { result } = renderHook(() => useSetStandingConfirmation(), { wrapper });

    expect(await result.current.mutateAsync(['stop', 'hold'])).toEqual({ done: true, why: null, message: '' });
    expect(await result.current.mutateAsync(['stop'])).toEqual({ done: false, why: 'not-offered', message: 'Not yet.' });
    expect(await result.current.mutateAsync([])).toEqual({ done: true, why: 'unwritten', message: 'Turned off for this run.' });
    expect(invoke.mock.calls.map(([module, type, request]) => [module, type, request])).toEqual([
      ['DAORIS.DRIVER', 'SET_STANDING_CONFIRMATION', { payload: { presses: ['stop', 'hold'] } }],
      ['DAORIS.DRIVER', 'SET_STANDING_CONFIRMATION', { payload: { presses: ['stop'] } }],
      ['DAORIS.DRIVER', 'SET_STANDING_CONFIRMATION', { payload: { presses: [] } }],
    ]);
  });

  it('is heard through the state hub, which reads the driver’s state again; the hook asks nothing again itself', async () => {
    invoke.mockImplementation(async () => ({ done: true }));
    const hub = wrapped();
    const pressed = renderHook(() => { useStateHub(); return useSetStandingConfirmation(); }, { wrapper: hub.wrapper });
    await act(async () => { await pressed.result.current.mutateAsync([]); });
    await waitFor(() => expect(hub.invalid()).toBe(true));

    heard.clear();
    const alone = wrapped();
    const own = renderHook(() => useSetStandingConfirmation(), { wrapper: alone.wrapper });
    await act(async () => { await own.result.current.mutateAsync([]); });
    await act(() => new Promise((settled) => { setTimeout(settled, 10); }));
    expect(alone.invalid()).toBe(false);
  });

  it('reads the driver’s state again when a press ran under it', async () => {
    const { wrapper, invalid } = wrapped();
    renderHook(() => useStandingUsed(), { wrapper });

    expect(invalid()).toBe(false);
    act(() => { heard.get('STANDING_USED')!({ seq: 1, at: '2026-10-23T09:31:00Z' }); });
    await waitFor(() => expect(invalid()).toBe(true));
  });
});

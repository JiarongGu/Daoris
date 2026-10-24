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
import { standingOf, useSetAppointment, useSetStandingConfirmation, useStandingUsed } from './person';
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
      supervisorOffered: false, stewardOffered: false, holders: [], stewardActsOffered: [], stewardActs: [], stewardDesk: [],
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

  // STEWARDROLE1b (D209 §2.3, §5.1, §5.3): the acts the build offers a steward's appointment, this run's steward acts newest first,
  // and an appointment's granted acts and set-up tick, each read field by field; an older shell's missing fields read as none.
  it("reads the steward's offered acts, this run's acts and an appointment's grant, each field by field", () => {
    expect(standingOf({
      stewardOffered: true,
      stewardActsOffered: ['drive', 'undrive', 7, 'hold'],
      stewardActs: [
        { id: 'a2', act: 'stop', repository: null, quest: 'q1', session: 's1', at: '2026-10-25T10:02:00Z', standing: true, waiting: true },
        { id: 'a1', act: 'drive', repository: 'engine', at: '2026-10-25T10:01:00Z', standing: 'yes' },
        { act: 'hold', at: '2026-10-25T10:00:00Z' }, { id: 'a0', at: '2026-10-25T10:00:00Z' }, 'drive',
      ],
      appointments: [{ role: 'steward', scope: 'machine', since: '2026-10-25T09:00:00Z', acts: ['drive', 3, 'stop'], setup: true }],
    })).toMatchObject({
      stewardActsOffered: ['drive', 'undrive', 'hold'],
      stewardActs: [
        { id: 'a2', act: 'stop', repository: null, quest: 'q1', session: 's1', at: '2026-10-25T10:02:00Z', standing: true, waiting: true },
        { id: 'a1', act: 'drive', repository: 'engine', quest: null, session: null, at: '2026-10-25T10:01:00Z', standing: false, waiting: false },
      ],
      appointments: [{ role: 'steward', scope: 'machine', acts: ['drive', 'stop'], setup: true }],
    });
    expect(standingOf({ appointments: [{ role: 'supervisor', repository: 'engine' }] })).toMatchObject({
      stewardActsOffered: [], stewardActs: [], appointments: [{ acts: [], setup: false }],
    });
  });

  // STEWARDROLE1c (D209 §6.2): the steward's desk, each request read field by field, its words as the caller wrote them; one with a
  // field missing or a state this page does not know left out, and an older shell's missing desk read as empty.
  it("reads the steward's desk field by field", () => {
    expect(standingOf({
      stewardOffered: true,
      stewardDesk: [
        { id: 'r2', act: 'stop', target: 's1', why: 'it loops <b>', filed: '2026-10-25T10:02:00Z', state: 'declined', closed: '2026-10-25T10:03:00Z', reason: 'the owner stops it' },
        { id: 'r1', act: 'drive', target: 'engine', why: 'next row', filed: '2026-10-25T10:01:00Z', state: 'open' },
        { id: 'r0', act: 'drive', target: 'engine', why: 'x', filed: '2026-10-25T10:00:00Z', state: 'granted' },
        { id: 'r9', act: 'drive', target: 'engine', filed: '2026-10-25T10:00:00Z', state: 'open' }, 'drive',
      ],
    })?.stewardDesk).toEqual([
      { id: 'r2', act: 'stop', target: 's1', why: 'it loops <b>', filed: '2026-10-25T10:02:00Z', state: 'declined', closed: '2026-10-25T10:03:00Z', answeredBy: null, reason: 'the owner stops it' },
      { id: 'r1', act: 'drive', target: 'engine', why: 'next row', filed: '2026-10-25T10:01:00Z', state: 'open', closed: null, answeredBy: null, reason: null },
    ]);
    expect(standingOf({ stewardOffered: true })?.stewardDesk).toEqual([]);
  });

  it("sends the steward's appointment for the machine with its set-up tick, and the supervisor's without one", async () => {
    invoke.mockResolvedValue({ done: true });
    const { wrapper } = wrapped();
    const { result } = renderHook(() => useSetAppointment(), { wrapper });

    await result.current.mutateAsync({ role: 'steward', on: true, setup: false });
    await result.current.mutateAsync({ role: 'steward', on: true, setup: true, share: 'h1' });
    await result.current.mutateAsync({ role: 'steward', on: false });
    await result.current.mutateAsync({ role: 'supervisor', repository: 'engine', on: true });
    expect(invoke.mock.calls.map(([, type, request]) => [type, request])).toEqual([
      ['SET_APPOINTMENT', { payload: { role: 'steward', scope: 'machine', holder: 'terminal', on: true, setup: false } }],
      ['SET_APPOINTMENT', { payload: { role: 'steward', scope: 'machine', holder: 'terminal', on: true, share: 'h1', setup: true } }],
      ['SET_APPOINTMENT', { payload: { role: 'steward', scope: 'machine', holder: 'terminal', on: false } }],
      ['SET_APPOINTMENT', { payload: { role: 'supervisor', scope: 'repository:engine', holder: 'terminal', on: true } }],
    ]);
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

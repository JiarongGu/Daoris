import { afterEach, describe, expect, it, vi } from 'vitest';

const { invoke } = vi.hoisted(() => ({ invoke: vi.fn(async () => ({})) }));

vi.mock('@shenora/react', () => ({ getBridge: () => ({ invoke }) }));

import { call, lookBound, pluginBound, pressBound, stoppedWaiting } from './call';

/**
 * WSR7: a long route waits as long as the host may work. The bridge gives up after its default 30 seconds, and a
 * look that fetched 29 repositories one after another ran for minutes and was answered to nobody.
 */
describe('a call onto the driver', () => {
  afterEach(() => invoke.mockClear());

  it('says nothing of a bound where the route is quick, as every call did before', async () => {
    await call('LINES');
    await call('SET_LINE', { repository: 'engine', branch: 'main' });

    expect(invoke).toHaveBeenNthCalledWith(1, 'DAORIS.DRIVER', 'LINES', {});
    expect(invoke).toHaveBeenNthCalledWith(2, 'DAORIS.DRIVER', 'SET_LINE', { payload: { repository: 'engine', branch: 'main' } });
  });

  it('hands the bridge the bound a long route passes', async () => {
    await call('TREES_SYNC', { only: ['engine:main'] }, { timeoutMs: 420_000 });

    expect(invoke).toHaveBeenCalledWith('DAORIS.DRIVER', 'TREES_SYNC', { payload: { only: ['engine:main'] }, timeoutMs: 420_000 });
  });
});

describe("the host's bounds, as the page waits for them", () => {
  const minutes = (ms: number) => ms / 60_000;

  it('waits for a look a few repositories at a time, each within a fetch', () => {
    // 29 repositories, four at a time: eight rounds of two minutes, and two to spare.
    expect(minutes(lookBound(29))).toBe(18);
    expect(minutes(lookBound(4))).toBe(4);
    expect(minutes(lookBound(0))).toBe(4);
  });

  it('waits for a press one replay after another, and for a plugin its start and its answer', () => {
    expect(minutes(pressBound(3))).toBe(17);
    expect(minutes(pluginBound)).toBe(6);
  });

  it("tells the page's own giving up from the host's refusal", () => {
    expect(stoppedWaiting({ code: 'TIMEOUT' })).toBe(true);
    expect(stoppedWaiting({ code: 'DRIVER_NOT_READY' })).toBe(false);
    expect(stoppedWaiting(new Error('nope'))).toBe(false);
    expect(stoppedWaiting(null)).toBe(false);
  });
});

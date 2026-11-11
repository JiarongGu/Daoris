import { beforeEach, describe, expect, it, vi } from 'vitest';

const call = vi.fn();
vi.mock('../bridge/call', () => ({ call: (...args: unknown[]) => call(...args) }));

import { chooseInPlace, choosesInPlace } from './inPlace';

const ASK = { repositories: ['reports'], preset: 'in-place', opinion: false, look: false, adoption: false, drive: false };

describe('the add choosing Work in place', () => {
  beforeEach(() => call.mockReset());

  it('chooses it for a folder and a system always, and for a repository where the person ticks it', () => {
    expect([choosesInPlace('folder', false), choosesInPlace('system', false), choosesInPlace('git', false), choosesInPlace('git', true)])
      .toEqual([true, true, false, true]);
  });

  it('plans a set-up of one and applies that plan by its digest', async () => {
    call
      .mockResolvedValueOnce({ refusals: [], digest: 'd'.repeat(64) })
      .mockResolvedValueOnce({ done: true, message: 'Applied.', digest: 'd'.repeat(64), operations: [] });

    const applied = await chooseInPlace('reports');

    expect(applied.done).toBe(true);
    expect(call.mock.calls.map(([route, payload]) => [route, payload])).toEqual([
      ['PLACE_PLAN', ASK],
      ['PLACE_APPLY', { ...ASK, digest: 'd'.repeat(64), accept: [] }],
    ]);
  });

  it('applies nothing where the plan refuses, and says why', async () => {
    call.mockResolvedValueOnce({
      refusals: [{ repository: 'reports', part: 'delivery', code: 'place-kind', sentence: '`reports` cannot follow it.' }],
      digest: 'e'.repeat(64),
    });

    const applied = await chooseInPlace('reports');

    expect([applied.done, applied.refused, applied.message]).toEqual([false, 'place-kind', '`reports` cannot follow it.']);
    expect(call).toHaveBeenCalledTimes(1);
  });
});

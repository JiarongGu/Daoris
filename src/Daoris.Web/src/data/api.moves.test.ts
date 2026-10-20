import { afterEach, describe, expect, it, vi } from 'vitest';
import { api } from './api';

/**
 * FLOWCHART1a2 (D186; the flowchart design §2.1, §7 trap 18): a quest's moves, read from the service's own door on both hosts,
 * so a browser draws its flow as the window does. A host from before the door answers it not found, and that is no failure: the
 * page draws the statuses and says the path is not kept there.
 */
describe("a quest's moves door", () => {
  afterEach(() => { vi.unstubAllGlobals(); });

  it('reads the moves at the quest\'s own door, in the order the service answers them', async () => {
    const fetcher = vi.fn(async (_path: string) => Response.json({
      quest: 'q1',
      moves: [
        { kind: 'published', at: '2026-10-20T09:00:00Z', machine: 'm2', sequence: 1, number: 1, applied: true },
        { kind: 'taken', at: '2026-10-20T09:05:00Z', machine: 'm3', sequence: 1, applied: true, from: 'Open' },
      ],
    }));
    vi.stubGlobal('fetch', fetcher);

    const moves = await api.questMoves('q1');

    expect(fetcher.mock.calls[0]![0]).toBe('/api/quests/q1/moves');
    expect(moves?.map((move) => move.kind)).toEqual(['published', 'taken']);
  });

  it('answers none from a host before the door (trap 18), and says any other failure', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response('', { status: 404, statusText: 'Not Found' })));
    expect(await api.questMoves('q1')).toBeNull();

    vi.stubGlobal('fetch', vi.fn(async () => Response.json({ error: 'the history did not read' }, { status: 500 })));
    await expect(api.questMoves('q1')).rejects.toThrow('the history did not read');
  });
});

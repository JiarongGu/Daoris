import { afterEach, describe, expect, it, vi } from 'vitest';
import { api, holdPersonKey, holdsPersonKey } from './api';

/**
 * PERSONDOOR1f (D156 point 2, the person-door design §2.2, §2.3 and §5.2): the person key the shell answers is held in memory and
 * sent as `Daoris-Person` on every write and no read; one the host refuses `stale` is asked of the shell again and the write sent
 * once more; the shell answering none leaves every write as it was.
 */

const KEY = 'k'.repeat(43);
const NEW = 'n'.repeat(43);

/** Each request the stubbed host was sent: its method and the key it carried, if any. */
function carried(fetcher: ReturnType<typeof vi.fn>): { method: string; key: string | null }[] {
  return (fetcher.mock.calls as [string, RequestInit | undefined][]).map(([, init]) => ({
    method: init?.method ?? 'GET',
    key: (init?.headers as Record<string, string> | undefined)?.['Daoris-Person'] ?? null,
  }));
}

const kept = () => Response.json({ quest: { id: 'q2' }, message: 'kept' });

describe('the person key (PERSONDOOR1f)', () => {
  afterEach(async () => {
    vi.unstubAllGlobals();
    await holdPersonKey(null);
  });

  it('is sent as Daoris-Person on every write once the shell answers one, and never on a read', async () => {
    expect(await holdPersonKey(async () => KEY)).toBe(KEY);
    expect(holdsPersonKey()).toBe(true);
    const fetcher = vi.fn(async (path: string) => (path.startsWith('/api/quests?') ? Response.json([]) : kept()));
    vi.stubGlobal('fetch', fetcher);

    await api.reviewQuest('q2', 'skipped', null, null);
    await api.deleteQuest('q2');
    await api.quests(null, false, null);

    expect(carried(fetcher)).toEqual([
      { method: 'POST', key: KEY },
      { method: 'DELETE', key: KEY },
      { method: 'GET', key: null },
    ]);
  });

  it('is none where the shell answers none or cannot be asked, and a write is sent as it was', async () => {
    expect(await holdPersonKey(async () => null)).toBeNull();
    expect(await holdPersonKey(async () => { throw new Error('NO_ROUTE'); })).toBeNull();
    expect(holdsPersonKey()).toBe(false);
    const fetcher = vi.fn(async () => kept());
    vi.stubGlobal('fetch', fetcher);

    await api.reviewQuest('q2', 'skipped', null, null);

    const [[, init]] = fetcher.mock.calls as unknown as [[string, RequestInit]];
    expect(init.headers).toEqual({ 'content-type': 'application/json' });
  });

  it("is asked of the shell again when a write is refused stale, and the write is sent once more with the new one", async () => {
    let asked = 0;
    await holdPersonKey(async () => (asked++ === 0 ? KEY : NEW));
    const fetcher = vi.fn()
      .mockResolvedValueOnce(Response.json(
        { error: 'This call carried a key from an earlier start of this service, and each start has its own. Nothing was kept.', code: 'stale' },
        { status: 403 }))
      .mockResolvedValueOnce(kept());
    vi.stubGlobal('fetch', fetcher);

    await api.reviewQuest('q2', 'skipped', null, null);

    expect(asked).toBe(2);
    expect(carried(fetcher)).toEqual([{ method: 'POST', key: KEY }, { method: 'POST', key: NEW }]);
  });

  it("says a second stale, and the person's door refused, in the host's own sentence with its code", async () => {
    await holdPersonKey(async () => KEY);
    const stale = 'This call carried a key from an earlier start of this service, and each start has its own. Nothing was kept.';
    vi.stubGlobal('fetch', vi.fn(async () => Response.json({ error: stale, code: 'stale' }, { status: 403 })));
    await expect(api.reviewQuest('q2', 'skipped', null, null)).rejects.toMatchObject({ message: stale, code: 'stale' });

    await holdPersonKey(null);
    const only = "Only the person can give a review's verdict, and this call carried no key of theirs. Nothing was kept.";
    vi.stubGlobal('fetch', vi.fn(async () => Response.json({ error: only, code: 'person-only' }, { status: 403 })));
    await expect(api.reviewQuest('q2', 'skipped', null, null)).rejects.toMatchObject({ message: only, code: 'person-only' });
  });

  it("reads whether the host holds a key of its own, which a host the shell did not start answers with a proof", async () => {
    const fetcher = vi.fn(async (path: string) => Response.json(
      path.includes('prove=') ? { semantic: false, tier: 'lexical', proof: 'p'.repeat(43) } : { semantic: false, tier: 'lexical' }));
    vi.stubGlobal('fetch', fetcher);

    expect(await api.hostHoldsAKey()).toBe(true);
    const [[path]] = fetcher.mock.calls as unknown as [[string]];
    expect(path).toMatch(/^\/api\/status\?prove=[A-Za-z0-9_-]{16,}$/);

    vi.stubGlobal('fetch', vi.fn(async () => Response.json({ semantic: false, tier: 'lexical' })));
    expect(await api.hostHoldsAKey()).toBe(false);
  });
});

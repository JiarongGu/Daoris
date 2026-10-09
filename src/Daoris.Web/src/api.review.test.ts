import { afterEach, describe, expect, it, vi } from 'vitest';
import { api } from './api';

/** The one request the stubbed host was sent: where, and the body it carried. */
function sent(fetcher: ReturnType<typeof vi.fn>): { path: string; method: string; body: unknown } {
  const [[path, init]] = fetcher.mock.calls as [[string, RequestInit]];
  return { path, method: init.method ?? 'GET', body: init.body ? JSON.parse(String(init.body)) : undefined };
}

const answered = () => vi.fn(async () => Response.json({ quest: { id: 'q2' }, message: 'kept' }));

/**
 * REVIEWENV1g (D154 point 8; the review environment design §3.3, §3.6; REVIEWENV1b3): the person's verdict names the set-up
 * their view drew, whole, and a skip names none; *Set it up* and an ask's review choice reach their own doors. Each is one of
 * D156 §3.2's person-only doors, which the page reaches through one seam where PERSONDOOR1f puts the key.
 */
describe("the review's doors", () => {
  afterEach(() => { vi.unstubAllGlobals(); });

  it('sends a reviewed with the set-up the view drew, by its machine and sequence', async () => {
    const fetcher = answered();
    vi.stubGlobal('fetch', fetcher);

    await api.reviewQuest('q2', 'reviewed', null, { machine: 'desk', sequence: 4 });

    expect(sent(fetcher)).toEqual({
      path: '/api/quests/q2/review', method: 'POST', body: { verdict: 'reviewed', setUp: { machine: 'desk', sequence: 4 } },
    });
  });

  it("sends a not-yet with the person's words beside the set-up", async () => {
    const fetcher = answered();
    vi.stubGlobal('fetch', fetcher);

    await api.reviewQuest('q2', 'not-yet', 'the label still reads the old name', { machine: 'desk', sequence: 4 });

    expect(sent(fetcher).body).toEqual({
      verdict: 'not-yet', words: 'the label still reads the old name', setUp: { machine: 'desk', sequence: 4 },
    });
  });

  it('sends a skip naming no set-up, with words where they gave any', async () => {
    const fetcher = answered();
    vi.stubGlobal('fetch', fetcher);

    await api.reviewQuest('q1', 'skipped', 'a typo', { machine: 'desk', sequence: 4 });

    expect(sent(fetcher)).toEqual({ path: '/api/quests/q1/review', method: 'POST', body: { verdict: 'skipped', words: 'a typo' } });
  });

  it("says a stale verdict's refusal in the host's own sentence", async () => {
    const stale = 'Set-up step `#q2` was shown again since that set-up, at `abc12345`: review the newest, since what it holds is '
      + 'the work that lands. Nothing was kept.';
    vi.stubGlobal('fetch', vi.fn(async () => Response.json({ error: stale }, { status: 409 })));

    await expect(api.reviewQuest('q2', 'reviewed', null, { machine: 'desk', sequence: 3 })).rejects.toThrow(stale);
  });

  it('publishes a set-up step in an environment, following the quest', async () => {
    const fetcher = answered();
    vi.stubGlobal('fetch', fetcher);

    await api.setUpStep('q1', 'local');

    expect(sent(fetcher)).toEqual({ path: '/api/quests/q1/set-up-step', method: 'POST', body: { environment: 'local' } });
  });

  it("sets an ask's review choice with the person's words", async () => {
    const fetcher = vi.fn(async () => Response.json({ ask: { id: 'a1' }, message: 'kept' }));
    vi.stubGlobal('fetch', fetcher);

    await api.chooseAskReview('a1', 'off', 'only the docs change');

    expect(sent(fetcher)).toEqual({ path: '/api/asks/a1/review', method: 'POST', body: { choice: 'off', words: 'only the docs change' } });
  });

  it("carries the composer's review choice on the ask itself", async () => {
    const fetcher = vi.fn(async () => Response.json({ ask: { id: 'a1' }, message: 'kept' }));
    vi.stubGlobal('fetch', fetcher);

    await api.ask({ workspace: 'aurora', sentence: 'Add the compare setting', review: 'local', reviewWords: 'run it locally' });

    expect(sent(fetcher).body).toEqual({
      workspace: 'aurora', sentence: 'Add the compare setting', review: 'local', reviewWords: 'run it locally',
    });
  });
});

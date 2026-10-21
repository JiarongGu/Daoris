import { afterEach, describe, expect, it, vi } from 'vitest';
import { api, notFound } from './api';
import { sentence } from '../locales/format';
import i18n from '../locales/i18n';

/**
 * 🔴 UX5 U29: with the machine's service stopped, every request the page made failed with the
 * browser's own words, and the toast said *Failed to fetch*: nothing a person can act on, and not
 * Daoris's voice. A request that reaches nobody is a refusal of Daoris's own, which says the service
 * is not answering and that the screen shows the last it said. A request the page cancelled is still
 * a cancellation, which the query layer expects and nobody is told about.
 */
describe('a request that reaches nobody', () => {
  afterEach(() => { vi.unstubAllGlobals(); });

  it("is Daoris's sentence, in the reader's language, never the browser's", async () => {
    vi.stubGlobal('fetch', vi.fn(async () => { throw new TypeError('Failed to fetch'); }));

    const failed = await api.status().catch((error: unknown) => error);
    await i18n.changeLanguage('en');
    expect(sentence(failed)).toMatch(/not answering/);
    expect(sentence(failed)).not.toMatch(/Failed to fetch/);

    await i18n.changeLanguage('zh');
    expect(sentence(failed)).toMatch(/[一-鿿]/);
    await i18n.changeLanguage('en');

    vi.stubGlobal('fetch', vi.fn(async () => { throw new TypeError('Failed to fetch'); }));
    const posted = await api.refresh().catch((error: unknown) => error);
    expect(sentence(posted)).toMatch(/not answering/);
  });

  it('keeps a cancellation a cancellation', async () => {
    const aborted = new DOMException('The operation was aborted.', 'AbortError');
    vi.stubGlobal('fetch', vi.fn(async () => { throw aborted; }));

    await expect(api.status()).rejects.toBe(aborted);
  });
});

/**
 * FRAME1f: an entry chosen in Search is kept by its id, and the index may have let it go since. The service's
 * *no entry with that id* is an answer the page says as *gone*; a service that failed, or reached nobody, is not.
 */
describe('a read the service has nothing for', () => {
  afterEach(() => { vi.unstubAllGlobals(); });

  it('says so apart from a failure, in the service\'s own sentence', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => Response.json({ error: "no entry with id 'game:x.md'" }, { status: 404 })));
    const missing = await api.entry('game:x.md').catch((error: unknown) => error);
    expect(notFound(missing)).toBe(true);
    expect(sentence(missing)).toBe("no entry with id 'game:x.md'");

    vi.stubGlobal('fetch', vi.fn(async () => Response.json({ error: 'the index is being rebuilt' }, { status: 503 })));
    expect(notFound(await api.entry('game:x.md').catch((error: unknown) => error))).toBe(false);

    vi.stubGlobal('fetch', vi.fn(async () => { throw new TypeError('Failed to fetch'); }));
    expect(notFound(await api.entry('game:x.md').catch((error: unknown) => error))).toBe(false);
  });
});

/**
 * KNOWUSE1a: the person's yes or no to a go-ahead reaches the ask's go-ahead door, the go-ahead by its number, their
 * words only where they gave some, and a refusal is the service's own sentence.
 */
describe('answering a go-ahead', () => {
  afterEach(() => { vi.unstubAllGlobals(); });

  it('posts the answer to the go-ahead by its number, with the person\'s words only where they gave some', async () => {
    const sent: { url: string; body: unknown }[] = [];
    vi.stubGlobal('fetch', vi.fn(async (url: string, init: RequestInit) => {
      sent.push({ url, body: JSON.parse(init.body as string) });
      return Response.json({ ask: { id: 'a1' }, message: 'Go-ahead 2 on ask `#a1` approved.' });
    }));

    const answered = await api.answerGoAhead('a1', 2, true, 'only on the report site');
    await api.answerGoAhead('a1', 3, false);

    expect(answered.message).toBe('Go-ahead 2 on ask `#a1` approved.');
    expect(sent.map((request) => request.url)).toEqual(['/api/asks/a1/go-aheads/2', '/api/asks/a1/go-aheads/3']);
    expect(sent.map((request) => request.body)).toEqual([
      { answer: 'approved', words: 'only on the report site' }, { answer: 'refused' },
    ]);

    vi.stubGlobal('fetch', vi.fn(async () => Response.json({ error: 'Ask `#a1` holds no go-ahead 7: it holds 1, 2.' }, { status: 404 })));
    expect(sentence(await api.answerGoAhead('a1', 7, true).catch((error: unknown) => error))).toBe('Ask `#a1` holds no go-ahead 7: it holds 1, 2.');
  });

  /** GOAHEADSTALE1: a stale go-ahead's refusal says no park goes on, so it runs nothing; every other answer reads as it always did. */
  it('says no park goes on only where it is asked to', async () => {
    const sent: unknown[] = [];
    vi.stubGlobal('fetch', vi.fn(async (_url: string, init: RequestInit) => {
      sent.push(JSON.parse(init.body as string));
      return Response.json({ ask: { id: 'a1' }, message: 'Go-ahead 3 on ask `#a1` refused.' });
    }));

    await api.answerGoAhead('a1', 3, false, 'done another way', false);
    await api.answerGoAhead('a1', 3, false, undefined, true);

    expect(sent).toEqual([{ answer: 'refused', words: 'done another way', goesOn: false }, { answer: 'refused' }]);
  });
});

/**
 * FABRIC-CLOSE1d3: a quest's list reads a source's reopen (FABRIC-CLOSE1c4's `reopened`, with CLOSE1c8's `session`) and the session
 * that said a submission's ready, as the host answers them; a host before them answers neither, and both read as absent.
 */
describe("a source's reopen and its submitting session", () => {
  afterEach(() => { vi.unstubAllGlobals(); });

  const quest = { id: 'sr0sr0', from: 'game', to: 'engine', status: 'Taken', title: 'Stream the tiles', body: '', filed: '2026-10-18T00:00:00Z', updated: '2026-10-18T00:00:00Z' };

  it('reads both where the host answers them, and neither from an older host', async () => {
    const reopened = { machine: 'desk', sequence: 44, of: { machine: 'desk', sequence: 41 }, note: 'It seams.', at: '2026-10-18T02:00:00Z', session: 's-7f3a21' };
    const submission = { machine: 'desk', sequence: 45, at: '2026-10-18T03:00:00Z', note: 'ready', session: 's-7f3a21' };
    vi.stubGlobal('fetch', vi.fn(async () => Response.json([{ ...quest, reopened }, { ...quest, id: 'su0su0', submission }])));
    const [source, submitted] = await api.quests('engine', false, null);
    expect(source.reopened).toEqual(reopened);
    expect(submitted.submission?.session).toBe('s-7f3a21');

    vi.stubGlobal('fetch', vi.fn(async () => Response.json([quest, { ...quest, id: 'su0su0', submission: { ...submission, session: undefined } }])));
    const [older, olderSubmitted] = await api.quests('engine', false, null);
    expect(older.reopened).toBeUndefined();
    expect(olderSubmitted.submission?.session).toBeUndefined();
  });
});

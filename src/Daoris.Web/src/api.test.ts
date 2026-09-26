import { afterEach, describe, expect, it, vi } from 'vitest';
import { api } from './api';
import { sentence } from './format';
import i18n from './i18n';

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

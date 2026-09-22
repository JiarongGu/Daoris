import { describe, expect, it } from 'vitest';
import { SEARCH_SHOWN, page } from './results';

const rows = (n: number) => Array.from({ length: n }, (_, index) => index);

/**
 * Search paging, after a broad query on the deployed application returned exactly forty results
 * with nothing saying that was a limit (D62).
 */
describe('page', () => {
  it('shows everything when the service had no more to give', () => {
    const { shown, more } = page(rows(26), SEARCH_SHOWN);
    expect(shown).toHaveLength(26);
    expect(more).toBe(false);
  });

  /**
   * 🔴 The case the extra row exists for. Forty received and forty shown is ambiguous; forty-one
   * received is not — so the page asks for one more than it will ever render.
   */
  it('knows there are more only because it asked for one more', () => {
    const exactly = page(rows(SEARCH_SHOWN), SEARCH_SHOWN);
    expect(exactly.more).toBe(false);

    const overflowing = page(rows(SEARCH_SHOWN + 1), SEARCH_SHOWN);
    expect(overflowing.shown).toHaveLength(SEARCH_SHOWN);
    expect(overflowing.more).toBe(true);
  });

  it('never renders the extra row it fetched', () => {
    const { shown } = page(rows(41), 40);
    expect(shown[shown.length - 1]).toBe(39);
  });

  it('survives nothing to show and a limit of nothing', () => {
    expect(page([], 40)).toEqual({ shown: [], more: false });
    expect(page(rows(3), 0)).toEqual({ shown: [], more: true });
    expect(page(rows(3), -1).more).toBe(true);
  });

  it('does not mutate what it was given', () => {
    const given = rows(5);
    page(given, 2);
    expect(given).toHaveLength(5);
  });
});

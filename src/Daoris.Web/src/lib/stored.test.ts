import { afterEach, describe, expect, it, vi } from 'vitest';
import { store, stored, storedHere, storeHere } from './stored';

describe('stored', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    window.localStorage.clear();
    window.sessionStorage.clear();
  });

  it('remembers a value, and forgets it when handed null', () => {
    store('daoris.test', 'kept');
    expect(stored('daoris.test')).toBe('kept');

    store('daoris.test', null);
    expect(stored('daoris.test')).toBeNull();
  });

  it('answers nothing and throws nothing where storage is refused', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new DOMException('refused', 'SecurityError');
    });
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new DOMException('full', 'QuotaExceededError');
    });

    expect(stored('daoris.test')).toBeNull();
    expect(() => store('daoris.test', 'kept')).not.toThrow();
  });

  // MAINWINDOWS3: a window's own scope is kept where a reload of that window finds it and no other window reads.
  it('remembers for this window alone, apart from what every window shares', () => {
    storeHere('daoris.test', 'mine');
    expect(storedHere('daoris.test')).toBe('mine');
    expect(stored('daoris.test')).toBeNull();
    expect(window.sessionStorage.getItem('daoris.test')).toBe('mine');

    storeHere('daoris.test', null);
    expect(storedHere('daoris.test')).toBeNull();
  });

  it('answers nothing and throws nothing where this window\'s storage is refused', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new DOMException('refused', 'SecurityError');
    });
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new DOMException('full', 'QuotaExceededError');
    });

    expect(storedHere('daoris.test')).toBeNull();
    expect(() => storeHere('daoris.test', 'kept')).not.toThrow();
  });
});

/**
 * FRAME1c (audit A10): one guard. The map's lines kept a second one of their own, which REV3 CLEAN1 had
 * made this file's job, so a page reading the browser's storage itself is found here rather than by review.
 */
const sources = import.meta.glob('../**/*.{ts,tsx}', { eager: true, query: '?raw', import: 'default' }) as Record<string, string>;

/** A source that reads or writes the browser's storage itself: a call on it, not a word naming it. */
const reachesStorage = (source: string) => /\b(?:localStorage|sessionStorage)\s*\./.test(source);

describe('the one storage guard', () => {
  it('catches a page reading storage itself, and not a word naming it', () => {
    expect(reachesStorage("const kept = localStorage.getItem('daoris.mapLines');")).toBe(true);
    expect(reachesStorage("window.sessionStorage.setItem('x', '1');")).toBe(true);
    // i18next's detector is told where to look by name, and reads it itself.
    expect(reachesStorage("order: ['localStorage', 'navigator'],")).toBe(false);
  });

  it('is the only source that reads storage', () => {
    const own = Object.entries(sources)
      .filter(([path]) => !/\.(test|stories)\.tsx?$/.test(path) && !path.includes('/test/'))
      .filter(([, source]) => reachesStorage(source))
      .map(([path]) => path);
    expect(Object.keys(sources).length).toBeGreaterThan(0);
    expect(own).toEqual(['./stored.ts']);
  });
});

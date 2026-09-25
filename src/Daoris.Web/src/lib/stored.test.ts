import { afterEach, describe, expect, it, vi } from 'vitest';
import { store, stored } from './stored';

describe('stored', () => {
  afterEach(() => {
    vi.restoreAllMocks();
    window.localStorage.clear();
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
});

import { describe, expect, it } from 'vitest';
import { hostBase } from './host';

// D92: on the Chromium the desktop ships, the page lives on its engine's app origin and calls its host
// at the loopback address the shell puts in its URL. Only a loopback address, and only in the shell.

describe('where the page reaches its host', () => {
  it('takes the loopback address the shell gives it', () => {
    expect(hostBase('?host=http://127.0.0.1:5177', true)).toBe('http://127.0.0.1:5177');
    expect(hostBase('?window=monitor&host=http%3A%2F%2Flocalhost%3A5188', true)).toBe('http://localhost:5188');
    expect(hostBase('?host=http://[::1]:5177', true)).toBe('http://[::1]:5177');
  });

  it('keeps its own origin with no address given', () => {
    expect(hostBase('', true)).toBe('');
    expect(hostBase('?window=monitor', true)).toBe('');
  });

  /** A crafted link in a browser must never send the page's calls, and a remote's key, elsewhere. */
  it('ignores an address outside the shell, and anything that is not loopback inside it', () => {
    expect(hostBase('?host=http://127.0.0.1:5177', false)).toBe('');
    expect(hostBase('?host=https://elsewhere.example', true)).toBe('');
    expect(hostBase('?host=http://127.0.0.1.elsewhere.example:80', true)).toBe('');
    expect(hostBase('?host=http://127.0.0.1:5177/path', true)).toBe('');
    expect(hostBase('?host=https://127.0.0.1:5177', true)).toBe('');
  });
});

import { describe, expect, it } from 'vitest';
import { anotherMainWindow, secondaryWindow, sessionWindowName, WINDOW_PARAMETER } from './window';

/**
 * The window a page is (SURF8). Three readers agree on this string — a URL, an IPC payload and a
 * filename — so what counts as a name is asserted once, here.
 */
describe('which window this page is', () => {
  it('is the application itself when nothing says otherwise', () => {
    expect(secondaryWindow('')).toBeNull();
    expect(secondaryWindow('?')).toBeNull();
    expect(secondaryWindow('?lang=zh')).toBeNull();
  });

  it('reads the monitor', () => {
    expect(secondaryWindow('?window=monitor')).toEqual({ kind: 'monitor' });
    // The leading `?` is optional because `location.search` carries it and a test rarely does.
    expect(secondaryWindow('window=monitor')).toEqual({ kind: 'monitor' });
  });

  it('reads one session, including a mirrored record from another machine', () => {
    expect(secondaryWindow('?window=session:a1b2c3d4')).toEqual({ kind: 'session', id: 'a1b2c3d4' });
    // `origin/id` is how a feed keys a record it did not produce (D47 §6).
    expect(secondaryWindow('?window=session:laptop/a1b2c3d4'))
      .toEqual({ kind: 'session', id: 'laptop/a1b2c3d4' });
  });

  it('takes the name from a query with other parameters in it', () => {
    expect(secondaryWindow('?lang=zh&window=monitor&scope=core')).toEqual({ kind: 'monitor' });
  });

  /**
   * The value becomes a filename host-side. A name that walked out of the geometry directory, or
   * carried a separator into it, is not a session id — so it is refused rather than escaped.
   */
  it('refuses an id that is not one, rather than escaping it', () => {
    for (const bad of [
      'session:../../secret',
      'session:..',
      'session:a b',
      'session:a\\b',
      'session:a:b',
      'session:',
      'session:/leading',
      'session:trailing/',
      'session:-leading-dash'.replace('-leading-dash', '-nope'),
    ]) {
      expect(secondaryWindow(`?${WINDOW_PARAMETER}=${encodeURIComponent(bad)}`), bad).toBeNull();
    }
  });

  it('does not answer a window it has never heard of', () => {
    expect(secondaryWindow('?window=settings')).toBeNull();
    expect(secondaryWindow('?window=MONITOR')).toBeNull();
  });

  it('names a session window the way the host opens it', () => {
    expect(sessionWindowName('a1b2c3d4')).toBe('session:a1b2c3d4');
    // The round trip is the contract: what the page names, the page can read back.
    expect(secondaryWindow(`?window=${encodeURIComponent(sessionWindowName('a1b2c3d4'))}`))
      .toEqual({ kind: 'session', id: 'a1b2c3d4' });
  });
});

/**
 * ONEINSTANCE1 (D171 point 2): a later launch of the executable opens another main window of the instance that runs, named
 * `main:<n>` from two. It is the application itself, so no secondary root; its name is what its page tells its frame by.
 */
describe('another main window', () => {
  it('is the application itself, and its name is read for its frame', () => {
    expect(secondaryWindow('?window=main%3A2')).toBeNull();
    expect(anotherMainWindow('?window=main%3A2&host=http%3A%2F%2Flocalhost%3A5177')).toBe('main:2');
    expect(anotherMainWindow('window=main:12')).toBe('main:12');
  });

  it('is not the application\'s own window, another kind, or a name that is not one', () => {
    for (const search of ['', '?window=monitor', '?window=session:a1b2c3d4', '?window=main:1', '?window=main:0',
      '?window=main:02', '?window=main:', '?window=main:x', `?window=${encodeURIComponent('main:2/../x')}`]) {
      expect(anotherMainWindow(search), search).toBeNull();
    }
  });
});

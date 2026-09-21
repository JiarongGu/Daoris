import { describe, expect, it } from 'vitest';
import { secondaryWindow, sessionWindowName, WINDOW_PARAMETER } from './window';

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

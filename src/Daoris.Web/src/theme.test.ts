import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { applyTheme, effectiveDark, readThemeChoice, setThemeChoice, subscribeTheme, THEME_KEY, themeChoice } from './theme';

// The theme is the viewer's choice (D66): system, light or dark — per window-profile, like the
// language and the scope, and applied as `data-theme` on <html>, which the tokens key their forced
// blocks on. `system` is the absence of the attribute, so the OS's media query decides as it always has.

describe('the theme choice', () => {
  beforeEach(() => {
    window.localStorage.removeItem(THEME_KEY);
    document.documentElement.removeAttribute('data-theme');
  });
  afterEach(() => vi.unstubAllGlobals());

  it('is the system by default, and the system leaves no attribute', () => {
    expect(readThemeChoice()).toBe('system');
    applyTheme('system');
    expect(document.documentElement.hasAttribute('data-theme')).toBe(false);
  });

  it('a chosen theme is set on the document and remembered', () => {
    setThemeChoice('dark');

    expect(document.documentElement.dataset.theme).toBe('dark');
    expect(window.localStorage.getItem(THEME_KEY)).toBe('dark');
    expect(themeChoice()).toBe('dark');
    expect(readThemeChoice()).toBe('dark');
  });

  it('going back to the system removes the attribute rather than writing "system" onto the page', () => {
    setThemeChoice('light');
    setThemeChoice('system');

    expect(document.documentElement.hasAttribute('data-theme')).toBe(false);
    expect(readThemeChoice()).toBe('system');
  });

  it('a remembered value it does not know is the system — a stale key never picks a theme', () => {
    window.localStorage.setItem(THEME_KEY, 'sepia');
    expect(readThemeChoice()).toBe('system');
  });

  /** What the window's native chrome is told (SET_THEME): the choice when there is one, else the OS. */
  it('is dark when chosen dark, light when chosen light, and the OS when the system decides', () => {
    vi.stubGlobal('matchMedia', (query: string) => ({ matches: query.includes('dark') }));
    expect(effectiveDark('light')).toBe(false);
    expect(effectiveDark('dark')).toBe(true);
    expect(effectiveDark('system')).toBe(true);
  });

  it('tells whoever listens when it changes', () => {
    const heard = vi.fn();
    const stop = subscribeTheme(heard);

    setThemeChoice('dark');
    stop();
    setThemeChoice('light');

    expect(heard).toHaveBeenCalledTimes(1);
  });

  /**
   * WINDOW1: a detached session is another document on the same origin, and it read the choice once,
   * at open — so the main window's change never reached it. The browser tells every OTHER document of
   * a storage write, and this is the half that hears it.
   */
  it("follows a choice another window made, and tells whoever listens", () => {
    const heard = vi.fn();
    const stop = subscribeTheme(heard);

    // What the browser does when the main window chooses dark: the store holds it, and this
    // document is told — its own setter never ran.
    window.localStorage.setItem(THEME_KEY, 'dark');
    window.dispatchEvent(new StorageEvent('storage', { key: THEME_KEY, newValue: 'dark' }));
    stop();

    expect(themeChoice()).toBe('dark');
    expect(document.documentElement.dataset.theme).toBe('dark');
    expect(heard).toHaveBeenCalledTimes(1);
  });

  it('is not moved by another window writing a key that is not the theme', () => {
    setThemeChoice('light');
    window.dispatchEvent(new StorageEvent('storage', { key: 'daoris.workspace', newValue: 'aurora' }));

    expect(themeChoice()).toBe('light');
  });
});

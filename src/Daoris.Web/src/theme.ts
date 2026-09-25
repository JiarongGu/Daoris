import { useSyncExternalStore } from 'react';
import { store, stored } from './lib/stored';

// The theme is the viewer's choice (D66): the system, or light, or dark. It is a per-window-profile
// preference like the language and the scope — never machine wiring and never a tracked file — and
// it is applied as `data-theme` on <html>, which `tokens.css` keys its forced blocks on. `system` is
// the ABSENCE of that attribute, so the OS's media query decides exactly as it did before there was
// a choice to make.

export type ThemeChoice = 'system' | 'light' | 'dark';

/** Where the choice is remembered. Per viewer; a lost or blocked store is simply the system. */
export const THEME_KEY = 'daoris.theme';

/** The remembered choice, or the system when there is none or it is one this build does not know. */
export function readThemeChoice(): ThemeChoice {
  const held = stored(THEME_KEY);
  return held === 'light' || held === 'dark' ? held : 'system';
}

let current: ThemeChoice = typeof window === 'undefined' ? 'system' : readThemeChoice();
const listeners = new Set<() => void>();

/** The choice now in force. */
export function themeChoice(): ThemeChoice {
  return current;
}

/** Put a choice on the document — the attribute for light or dark, nothing for the system. */
export function applyTheme(choice: ThemeChoice = current): void {
  const root = document.documentElement;
  if (choice === 'system') root.removeAttribute('data-theme');
  else root.dataset.theme = choice;
}

/** Choose, remember, apply, and tell whoever listens — the window's native chrome among them. */
export function setThemeChoice(choice: ThemeChoice): void {
  current = choice;
  // Not remembered where storage is refused; still applied for as long as this page lives.
  store(THEME_KEY, choice === 'system' ? null : choice);

  applyTheme(choice);
  for (const listener of listeners) listener();
}

/** Hear every change of choice. Returns the way to stop hearing. */
export function subscribeTheme(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

/**
 * Whether the page is dark under a choice — what the window's native chrome is told (SET_THEME),
 * because the caption buttons and the DWM border are painted natively and never see the CSS.
 */
export function effectiveDark(choice: ThemeChoice = current): boolean {
  if (choice !== 'system') return choice === 'dark';
  try {
    return window.matchMedia('(prefers-color-scheme: dark)').matches;
  } catch {
    // A jsdom without matchMedia, or a locked-down embedder. Dark is the shell's own default fill.
    return true;
  }
}

/** The choice, as a component reads and sets it. */
export function useThemeChoice(): [ThemeChoice, (choice: ThemeChoice) => void] {
  return [useSyncExternalStore(subscribeTheme, themeChoice, themeChoice), setThemeChoice];
}

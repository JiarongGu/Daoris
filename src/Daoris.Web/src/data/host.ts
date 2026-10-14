import { isShenoraAvailable } from '@shenora/react';

/**
 * Where the page reaches its host (D92).
 *
 * @remarks
 * **On the Chromium the desktop ships, the page lives on its engine's app origin**
 * (`https://daoris.localhost`), and the host that answers its API is at a loopback address, so the page
 * calls it by an absolute address the shell puts in the page's own URL (`?host=`). Everywhere else —
 * a browser, and the desktop on WebView2 — the page is served by its host and calls its own origin, so
 * the base is empty and every path stays relative.
 *
 * 🔴 **Only a loopback address, and only inside the shell.** A browser that opened a crafted link
 * (`…/?host=https://elsewhere`) would otherwise send every call, and a remote's key with it, to whoever
 * the link named. Inside the shell the URL is the shell's to write; outside it, `host` is ignored.
 */
const LOOPBACK = /^http:\/\/(127\.0\.0\.1|localhost|\[::1\]):\d{1,5}$/;

/** The host's base address from a page URL's query, or empty: the page's own origin. */
export function hostBase(search: string, inShell: boolean): string {
  if (!inShell) return '';
  let given: string | null;
  try {
    given = new URLSearchParams(search).get('host');
  } catch {
    return '';
  }
  return given && LOOPBACK.test(given) ? given : '';
}

/** Read once, as the page loads: the shell writes it into the URL before the page exists. */
const BASE = typeof window === 'undefined' ? '' : hostBase(window.location.search, isShenoraAvailable());

/** A host path as this page must ask for it: on its own origin, or at the host's loopback address. */
export function onHost(path: string): string {
  return `${BASE}${path}`;
}

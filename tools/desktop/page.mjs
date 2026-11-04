import { applicationsIn, browsersIn } from '../processes.mjs';
import { startedHere } from './session.mjs';

/**
 * The shell page's origin on the Chromium it ships (D92) — the shell's `DesktopPage.VirtualHost`,
 * spelled here too so the instruments can tell the shell from any other page (`twins.md`).
 */
export const SHELL_ORIGIN = 'https://daoris.localhost';

/**
 * Whether a page is THIS run's shell, from what the page says of itself (`PAGE_IDENTITY`).
 *
 * On Chromium (CHR2) the page is on the app's own origin, carries Shenora's Chromium mark, and names
 * the host it was told to reach. That host is how a run is told apart, because every shell's page has
 * the same origin. A WebView2 page is no shell of Daoris's since D93.
 */
export function isShell(page, serviceUrl) {
  if (!page?.chromium) return false;
  const service = serviceUrl ? new URL(serviceUrl).origin : null;
  let told = null;
  try { told = page.host ? new URL(page.host).origin : null; } catch { told = null; }
  return page.origin === SHELL_ORIGIN && (!service || told === service);
}

/** What `isShell` reads, evaluated in the page — here and by the deployment gate (DEPLOY5). */
export const PAGE_IDENTITY ='({ webview: !!window.chrome?.webview, chromium: !!window.__shenora_chromium, '
  + "origin: location.origin, host: new URLSearchParams(location.search).get('host') })";

/**
 * Where the page remembers the viewer's theme choice: `theme.ts`'s `THEME_KEY`, spelled here too
 * (`twins.md`), because `shot --theme` sets the choice for a capture and puts it back (LOOK1).
 */
export const THEME_KEY = 'daoris.theme';

/**
 * The theme the page is in, evaluated in it: the forced attribute a light or dark choice puts on
 * `<html>`, else the scheme the system (or the emulation) reports. The same reading as `theme.ts`'s
 * `effectiveDark`, including its dark default where the media query cannot be asked.
 */
export const PAGE_THEME = '(() => { const forced = document.documentElement.dataset.theme; '
  + "if (forced === 'light' || forced === 'dark') return forced; "
  + "try { return matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'; } "
  + "catch { return 'dark'; } })()";

/**
 * An expression that stores `value` as the viewer's choice (null forgets it, which is System) and tells
 * the page, answering the choice it replaced: `{ prior }`, or `{ refused }` with the store's reason.
 *
 * The page hears a write through `followStoredTheme`, which the browser calls only in the OTHER
 * documents of the origin. So this document is told as they are, by the same event, and nothing new is
 * reached into the page: it follows the write as it follows one made in a second window (WINDOW1).
 */
export function themeChoiceExpression(value) {
  return `(() => { const key = ${JSON.stringify(THEME_KEY)}; let prior;
    try {
      prior = localStorage.getItem(key);
      ${value === null ? 'localStorage.removeItem(key);' : `localStorage.setItem(key, ${JSON.stringify(value)});`}
    } catch (error) { return { refused: String((error && error.message) || error) }; }
    dispatchEvent(new StorageEvent('storage', { key }));
    return { prior }; })()`;
}

/**
 * Do `work` with the page in `theme`, and leave the viewer's choice as it was (LOOK1).
 *
 * The system's scheme is emulated first, which is all a viewer on System needs, and it ends with the
 * debug session. A viewer who chose light or dark overrides the emulation, and `--theme light`
 * photographed their dark window with nothing said. So where the emulation does not take, the viewer's
 * choice is set for the capture through the page's own store, and put back after it, whether the
 * capture worked or not. A page that still is not in the theme is refused rather than photographed, and
 * a choice that could not be put back fails the shot and names what it was.
 */
export async function withPageTheme(cdp, theme, work, { settle = 700 } = {}) {
  await cdp.send('Emulation.setEmulatedMedia', { features: [{ name: 'prefers-color-scheme', value: theme }] });

  let changed = false;
  let prior = null;
  const before = await cdp.evaluate(PAGE_THEME);
  if (before !== theme) {
    const taken = await cdp.evaluate(themeChoiceExpression(theme));
    if (!taken || taken.refused) {
      throw new Error(`refusing: the page's own theme choice keeps it ${before} over the emulated scheme, and it `
        + `could not be set to ${theme} for the capture (${taken?.refused ?? 'the page said nothing'}). `
        + 'Choose System under Settings → Theme, or take the shot without --theme.');
    }
    changed = true;
    prior = taken.prior ?? null;
  }

  let outcome = null;
  try {
    // The page's listeners fire and the SET_THEME round trip lands before the window has repainted.
    // Nothing reports when that finished, so this waits.
    await new Promise((resolve) => setTimeout(resolve, settle));
    const now = await cdp.evaluate(PAGE_THEME);
    if (now !== theme) {
      throw new Error(`refusing: the page is still ${now} with its theme choice set to ${theme}, `
        + 'so the capture would photograph the other theme.');
    }
    await work();
  } catch (error) {
    outcome = error;
  }

  if (changed) {
    const back = await cdp.evaluate(themeChoiceExpression(prior))
      .catch((error) => ({ refused: error.message }));
    if (!back || back.refused) {
      const left = `⚠ the viewer's theme choice was not put back (${back?.refused ?? 'the page said nothing'}): `
        + `it is ${theme} now and was ${prior ?? 'System'}. Settings → Theme sets it back.`;
      outcome = new Error(outcome ? `${outcome.message}\n${left}` : left);
    }
  }

  if (outcome) throw outcome;
}

/** What the shell captions that window — how the OS-level capture finds it. */
function windowCaption(window) {
  // Daoris's own browser (D85) is the engine's own window, captioned `<page> - Chromium`.
  if (window === 'browser') return 'Chromium';
  return window === 'monitor' ? 'Monitor' : window;
}

/**
 * Which process `shot` photographs, and the caption it looks for among that process's windows
 * (SHOTPICK1), from the `pid|command line` rows of every process running from the shell's executable.
 *
 * Since D99 the browser runs from that executable too, with windows of its own, so the capture script's
 * "the first process from this path with a window" was Windows' choice between two processes. On an
 * install it was the browser, photographed in the application's place. So the choice is made here, by
 * command line, as `eval` chooses the application's page, and the script is handed the id:
 * `--window browser` is the browser; every other window, the main one, the monitor or a detached
 * session, is the application's own. Among applications from one path (a scratch run and a `--real` one
 * from the same build) the one the last run recorded wins, as it does for `kill`; with none recorded the
 * first is taken and the rest are named in `others`.
 *
 * @returns {{ pid: number, title: string | null, others?: number[] } | { refusal: string }}
 */
export function shotTarget(rows, { window = null, record = null, exe = '' } = {}) {
  const browsers = browsersIn(rows);
  if (window === 'browser') {
    return browsers.length
      ? { pid: browsers[0], title: windowCaption(window) }
      : { refusal: 'no browser of this shell is running — open it with View → Browser, then photograph it.' };
  }

  const applications = applicationsIn(rows);
  if (!applications.length) {
    return {
      refusal: `no application of this shell is running${exe ? ` from ${exe}` : ''}`
        + (browsers.length ? ' (only its browser is, which `--window browser` photographs)' : '')
        + ' — `node tools/desktop.mjs run` starts one.',
    };
  }
  const recorded = startedHere(applications, record);
  const [pid, ...others] = recorded.length ? recorded : applications;
  return { pid, title: window ? windowCaption(window) : null, ...(others.length ? { others } : {}) };
}

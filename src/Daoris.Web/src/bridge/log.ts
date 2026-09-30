import { useMutation, useQuery } from '@tanstack/react-query';
import { getBridge, useShenora } from '@shenora/react';
import { keys } from '../queries';
import type { LogFilters, LogReading } from '../settings/Logs';

// The machine log (MOD3): what the page reports into it, and Settings → Logs reading it back (D94).

/** The longest string the page reports: the machine log keeps a clue, never a paragraph (D94). */
export const LOG_TEXT = 120;

const clipped = (text: string) => (text.length <= LOG_TEXT ? text : `${text.slice(0, LOG_TEXT)}…`);

/**
 * What the person did on the screen, into the machine log (LOG1b, D94): a view opened, a command run, a
 * view moved, a message counted, a proposal settled, a failure the page caught.
 *
 * @remarks
 * **Fire and forget, and never a throw**: the log is evidence, never a reason for the page to fail, so a
 * report the bridge refuses — or a bridge that breaks — is dropped here. **Nothing in a browser**, which
 * has no bridge: the log is the machine's, and a browser over a remote is not on it (D47 §4).
 *
 * **Counts and names, never words.** The shell's `DAORIS.LOG` module keeps only the catalogue's events and
 * fields and drops the rest, but a caller hands it a message's LENGTH, never its text. Called from
 * organisms and the application only: a molecule imports no hook, and this is the bridge.
 */
export function logEvent(event: string, data: Record<string, string | number | boolean>): void {
  try {
    const bridge = getBridge();
    if (!bridge.isAvailable) return;
    void Promise.resolve(bridge.invoke('DAORIS.LOG', 'EVENT', { payload: { event, data } })).catch(() => {});
  } catch {
    // Dropped: the report is never the page's problem.
  }
}

/** How many lines Settings → Logs asks for: a screen of the newest, never the month. */
export const LOG_LINES = 200;

/**
 * The machine log read back (LOG1c, D94): the newest lines with the terminal's filters, applied by the
 * shell — the reading `daoris-driver logs` prints. Desktop only: no HTTP route serves the log, and a
 * browser has no bridge to ask (D47 §4).
 */
export const useMachineLog = (filters: LogFilters) => {
  const { isAvailable } = useShenora();
  return useQuery({
    queryKey: keys.machineLog(filters.since, filters.source, filters.event, filters.level),
    queryFn: () => getBridge().invoke<LogReading>('DAORIS.LOG', 'LINES', {
      payload: {
        since: filters.since,
        // An empty filter is no filter, and is left out rather than sent as nothing.
        ...(filters.source ? { source: filters.source } : {}),
        ...(filters.event ? { event: filters.event } : {}),
        ...(filters.level ? { level: filters.level } : {}),
        limit: LOG_LINES,
      },
    }),
    enabled: isAvailable,
    // A filter changed keeps the last lines on the screen until the new ones land, rather than a blank.
    placeholderData: (previous) => previous,
  });
};

/** Open the log's folder in the file manager: the shell names the folder, never the page. */
export const useOpenLogFolder = () => useMutation({
  mutationFn: () => getBridge().invoke<{ opened: boolean; folder: string | null }>('DAORIS.LOG', 'OPEN_FOLDER', {}),
});

let pageErrorsHeard = false;

/**
 * The page's own failures into the machine log (LOG1b): an error nothing caught, and a promise nobody
 * handled — the message only, cut short, never a stack or what was on the screen. Installed once, at the
 * page's start, however often it is asked.
 */
export function installPageErrors(): void {
  if (pageErrorsHeard || typeof window === 'undefined') return;
  pageErrorsHeard = true;
  window.addEventListener('error', (event: ErrorEvent) => {
    logEvent('page.error', { where: 'window', message: clipped(event.message || String(event.error ?? 'an error')) });
  });
  window.addEventListener('unhandledrejection', (event: PromiseRejectionEvent) => {
    const reason: unknown = event.reason;
    logEvent('page.error', { where: 'promise', message: clipped(reason instanceof Error ? reason.message : String(reason)) });
  });
}

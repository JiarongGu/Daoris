/**
 * Which window this page is (D55 §b, SURF8).
 *
 * @remarks
 * **A secondary window is a route into the same bundle**, not a second frontend — the same bytes,
 * the same components, a different root. That is what makes a monitor on a second screen cost the
 * components it already has rather than a second application to keep in step.
 *
 * **Pure, because the parsing is the part that can be wrong.** A window name arrives as a query
 * parameter, is turned into a window *name* host-side, and that name becomes a filename for the
 * geometry store — so what counts as a name is a decision with three readers, and it is asserted
 * here rather than trusted three times.
 */

/** A window other than the application's own. Null — the absence — is the main window. */
export type SecondaryWindow =
  | { kind: 'monitor' }
  | { kind: 'session'; id: string };

/** The query parameter the host puts the window's name in. */
export const WINDOW_PARAMETER = 'window';

/** The rail plus every live stream, read-only, for a second screen. */
export const MONITOR_WINDOW = 'monitor';

/**
 * Which session ids may be named. Deliberately narrow: the alphabet the ledger actually mints
 * (eight hex characters) plus the `origin/id` a mirrored record wears (D47 §6), and nothing else.
 *
 * @remarks
 * It is a whitelist rather than an escape, because the value crosses three boundaries — a URL, an
 * IPC payload and a filename — and an escape has to be right at all three. `..`, a separator, a
 * query character or a space is simply not a session id, so none of them needs an answer.
 */
const SESSION_ID = /^[A-Za-z0-9][A-Za-z0-9._-]*(?:\/[A-Za-z0-9][A-Za-z0-9._-]*)?$/;

/**
 * Read the window name out of a location's query string.
 *
 * @param search - `window.location.search`, with or without its leading `?`.
 * @returns the secondary window this page is, or null for the application itself.
 *
 * @remarks
 * **An unrecognised name is the main window, not an error.** A page that refused to render because
 * it did not understand its own URL would be a blank window, and the thing a person does next with
 * a blank window is kill the application.
 */
export function secondaryWindow(search: string): SecondaryWindow | null {
  let name: string | null;
  try {
    name = new URLSearchParams(search).get(WINDOW_PARAMETER);
  } catch {
    // A malformed query is not a reason to fail to open.
    return null;
  }

  if (name === MONITOR_WINDOW) return { kind: 'monitor' };

  if (name?.startsWith('session:')) {
    const id = name.slice('session:'.length);
    return SESSION_ID.test(id) ? { kind: 'session', id } : null;
  }

  return null;
}

/** The window name for one session's own window — the same string the host opens it under. */
export function sessionWindowName(id: string): string {
  return `session:${id}`;
}

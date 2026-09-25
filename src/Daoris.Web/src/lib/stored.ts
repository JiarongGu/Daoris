/**
 * What this browser remembers for the page: a view, a theme, a scope, a layout, a draft.
 *
 * @remarks
 * Guarded, because storage can be absent or refused (a private window, a blocked origin), and not
 * remembering is a lesser failure than not working. Five files each wrote this guard for themselves
 * (REV3 CLEAN1). Per viewer and per browser by nature: nothing here is state another viewer or the
 * machine may rely on.
 */
export function stored(key: string): string | null {
  try {
    return window.localStorage.getItem(key);
  } catch {
    return null;
  }
}

/** Remember `value` under `key`, or forget the key when it is null. A refusal is dropped, as above. */
export function store(key: string, value: string | null): void {
  try {
    if (value === null) window.localStorage.removeItem(key);
    else window.localStorage.setItem(key, value);
  } catch {
    // Not remembered: the page still works for as long as it lives.
  }
}

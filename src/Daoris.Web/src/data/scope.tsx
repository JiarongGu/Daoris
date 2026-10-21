import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { store, stored, storedHere, storeHere } from '../lib/stored';
import { FIRST_MAIN_WINDOW, ownWindowName, secondaryWindow } from '../work/window';

// The workspace scope (WSP5; workspace design §4): every cross-repository answer is scoped to ONE
// workspace per query, and the platform never silently mixes circles. The scope is global chrome
// state — it lives in exactly one place (platform language §2) — and is read here by the query layer,
// so a view that never mentions workspaces still asks for one. Nothing chosen means every circle the
// deployment holds, stated out loud by the switcher rather than picked on the person's behalf: the
// door never invents a default (the host's own words), so the page does not either.
//
// Remembered per browser, like the language (D42): a viewer's convenience, not machine wiring. It is
// never written into a tracked file and never leaves the browser that chose it.
//
// 🔴 Each main window holds its own (MAINWINDOWS3; D171's note has the rule and why). One key every document followed
// meant a choice in one main window moved them all, so two windows could not hold two workspaces side by side.

/** The last scope chosen in any window, which a new main window starts from. Shared; absent for every workspace. */
const REMEMBERED = 'daoris.workspace';
/** This window's own scope, in its own storage: `''` for every workspace, absent where none is held here yet. */
const OWN = 'daoris.workspace';
/** What the window named after the `@` holds, for the windows it opened: `''` for every workspace. Shared. */
const HELD = 'daoris.workspace@';
/** Which window each opened window follows, or the workspace it holds: `{ [name]: { follows } | { workspace } }`. Shared. */
const OPENED = 'daoris.workspace.opened';
/** How many opened windows are kept, the newest last: one per session ever detached would otherwise grow for ever. */
const OPENED_KEPT = 32;
/** The application's own window, which the host names none (`ownWindowName`): `main:<n>` starts from two. */
const FIRST = FIRST_MAIN_WINDOW;

/** Where a window keeps what is its alone: its session storage, or a second document's in a test. */
export type WindowStore = {
  read: (key: string) => string | null;
  write: (key: string, value: string | null) => void;
};

const SESSION_STORE: WindowStore = { read: storedHere, write: storeHere };

type Scope = {
  /** The chosen workspace, or null for every circle this deployment holds. */
  workspace: string | null;
  setWorkspace: (workspace: string | null) => void;
  /**
   * This window is opening the window `name` (the monitor, a session detached): it follows this window's scope from now on,
   * or holds `workspace`, its session's own, where the session names one. Said before the window is asked for, so its page
   * finds it on arrival.
   */
  opens: (name: string, workspace?: string | null) => void;
};

// Without a provider the scope is "every", and choosing is a no-op — which is what every existing view
// test renders under, and exactly today's behaviour.
const ScopeContext = createContext<Scope>({ workspace: null, setWorkspace: () => {}, opens: () => {} });

function remembered(): string | null {
  return stored(REMEMBERED) || null;
}

function remember(workspace: string | null): void {
  store(REMEMBERED, workspace || null);
}

/** A held scope as kept: `''` is every workspace, null is nothing kept. */
function kept(value: string | null): string | null | undefined {
  return value === null ? undefined : value || null;
}

type Opened = { follows?: unknown; workspace?: unknown };

function openedWindows(): Record<string, Opened> {
  try {
    const read: unknown = JSON.parse(stored(OPENED) ?? '{}');
    return read !== null && typeof read === 'object' && !Array.isArray(read) ? read as Record<string, Opened> : {};
  } catch {
    // A value this build cannot read is no window opened.
    return {};
  }
}

function openedAs(name: string, entry: { follows: string } | { workspace: string }): void {
  const windows = openedWindows();
  delete windows[name];
  windows[name] = entry;
  const names = Object.keys(windows);
  for (const old of names.slice(0, Math.max(0, names.length - OPENED_KEPT))) delete windows[old];
  store(OPENED, JSON.stringify(windows));
}

/**
 * What a utility window holds: its session's workspace where it was opened with one, else what the window that opened it
 * holds, the first main window where nothing says which. The last chosen anywhere only where that window has said nothing,
 * which no window of this build leaves unsaid.
 */
function followed(self: string): string | null {
  const entry = openedWindows()[self];
  if (typeof entry?.workspace === 'string' && entry.workspace !== '') return entry.workspace;
  const from = typeof entry?.follows === 'string' && entry.follows !== self ? entry.follows : FIRST;
  const held = kept(stored(HELD + from));
  return held === undefined ? remembered() : held;
}

/** What a main window starts from: its own where a reload finds it, else the last chosen anywhere. */
function starting(own: WindowStore): string | null {
  const held = kept(own.read(OWN));
  return held === undefined ? remembered() : held;
}

/**
 * Holds the scope for the tree beneath it. `initial` pins the starting value (tests, stories), and a pinned scope follows
 * nothing and keeps nothing of its own; omitted, the window's own scope is read once, or the last chosen anywhere.
 * `search` and `own` are the window this is, its address and its own storage: the page's own unless a test plays another.
 */
export function WorkspaceScopeProvider({ initial, search, own = SESSION_STORE, children }: {
  initial?: string | null;
  search?: string;
  own?: WindowStore;
  children: ReactNode;
}) {
  const address = search ?? globalThis.location?.search ?? '';
  const self = ownWindowName(address) ?? FIRST;
  const utility = secondaryWindow(address);
  const pinned = initial !== undefined;
  // A utility window follows; a main window holds its own. A browser opening a utility window's address renders the
  // platform, and follows as that window would.
  const follows = !pinned && utility !== null;
  // A session's window opens none, so it says nothing of what it holds: a key per session would only grow.
  const tells = !pinned && utility?.kind !== 'session';

  const [workspace, set] = useState<string | null>(() => (pinned ? initial : follows ? followed(self) : starting(own)));

  // Chosen here, it is this window's, and the last chosen anywhere, which the next main window starts from.
  const setWorkspace = useCallback((next: string | null) => {
    set(next);
    remember(next);
  }, []);

  // A main window keeps what it holds as its own from its first paint and on each change, so a reload keeps it whatever
  // another window has chosen since; and a window that opens others says what it holds under its name, which they follow.
  useEffect(() => {
    if (!pinned && !follows) own.write(OWN, workspace ?? '');
    if (tells) store(HELD + self, workspace ?? '');
  }, [pinned, follows, tells, own, self, workspace]);

  // 🔴 A workspace the window that opened this one moved to (WINDOW1). A detached session read the scope once, at open,
  // so its queries kept asking for the circle it opened in after the main window moved. The browser tells every OTHER
  // document of a storage write; a utility window hears what its opener says, or its opening again from another window.
  useEffect(() => {
    if (!follows) return undefined;
    const follow = (event: StorageEvent) => {
      if (event.key === null || event.key === OPENED || event.key.startsWith(HELD)) set(followed(self));
    };
    window.addEventListener('storage', follow);
    return () => window.removeEventListener('storage', follow);
  }, [follows, self]);

  const opens = useCallback((name: string, sessionWorkspace?: string | null) => {
    openedAs(name, sessionWorkspace ? { workspace: sessionWorkspace } : { follows: self });
  }, [self]);

  const value = useMemo(() => ({ workspace, setWorkspace, opens }), [workspace, setWorkspace, opens]);
  return <ScopeContext.Provider value={value}>{children}</ScopeContext.Provider>;
}

export const useScope = () => useContext(ScopeContext);

import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from 'react';

// The workspace scope (WSP5; workspace design §4): every cross-repository answer is scoped to ONE
// workspace per query, and the platform never silently mixes circles. The scope is global chrome
// state — it lives in exactly one place (platform language §2) — and is read here by the query layer,
// so a view that never mentions workspaces still asks for one. Nothing chosen means every circle the
// deployment holds, stated out loud by the switcher rather than picked on the person's behalf: the
// door never invents a default (the host's own words), so the page does not either.
//
// Remembered per browser, like the language (D42): a viewer's convenience, not machine wiring. It is
// never written into a tracked file and never leaves the browser that chose it.

const STORAGE_KEY = 'daoris.workspace';

type Scope = {
  /** The chosen workspace, or null for every circle this deployment holds. */
  workspace: string | null;
  setWorkspace: (workspace: string | null) => void;
};

// Without a provider the scope is "every", and choosing is a no-op — which is what every existing view
// test renders under, and exactly today's behaviour.
const ScopeContext = createContext<Scope>({ workspace: null, setWorkspace: () => {} });

function remembered(): string | null {
  try {
    const value = window.localStorage.getItem(STORAGE_KEY);
    return value && value.length > 0 ? value : null;
  } catch {
    // Storage can be absent or refused (a private window, a blocked origin); the console still works.
    return null;
  }
}

function remember(workspace: string | null): void {
  try {
    if (workspace) window.localStorage.setItem(STORAGE_KEY, workspace);
    else window.localStorage.removeItem(STORAGE_KEY);
  } catch {
    // Not remembering is a lesser failure than not working.
  }
}

/**
 * Holds the scope for the tree beneath it. `initial` pins the starting value (tests, stories);
 * omitted, the browser's remembered choice is read once.
 */
export function WorkspaceScopeProvider({ initial, children }: { initial?: string | null; children: ReactNode }) {
  const [workspace, set] = useState<string | null>(() => (initial === undefined ? remembered() : initial));
  const setWorkspace = useCallback((next: string | null) => {
    set(next);
    remember(next);
  }, []);
  const value = useMemo(() => ({ workspace, setWorkspace }), [workspace, setWorkspace]);
  return <ScopeContext.Provider value={value}>{children}</ScopeContext.Provider>;
}

export const useScope = () => useContext(ScopeContext);

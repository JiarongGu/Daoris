import { useCallback, useMemo, useRef, useState } from 'react';
import { getBridge, useShenora, useShenoraEvent } from '@shenora/react';
import type { OpenTerminal, TerminalOpening, TerminalShellChoice, Terminals } from '../work/terminals';

// The person's own terminals (MOD3), held by the frame over `DAORIS.TERMINAL` (D96).

/** How much of one terminal's output the page holds while nothing shows it: enough to catch a screen up, never a log. */
const TERMINAL_HELD = 256 * 1024;
/** How many terminals' output it holds so at most: one that never becomes a tab is not kept for ever. */
const TERMINALS_HELD = 16;

const callTerminal = <TData,>(type: string, payload?: Record<string, unknown>): Promise<TData> =>
  // `resolve`: a bridge that answers nothing, or a mocked one, is still a promise to catch.
  Promise.resolve(getBridge().invoke<TData>('DAORIS.TERMINAL', type, payload ? { payload } : {}));

/**
 * The person's own terminals (CONSOLE4b, D96): opened over `DAORIS.TERMINAL`, typed at, and heard as
 * `TERMINAL_OUTPUT` batches.
 *
 * @remarks
 * **Held by the frame, not by the view that shows them**: the view unmounts whenever it moves between
 * the side bar and the panel or another view is shown, and a shell must outlive that. Output that comes
 * while no screen listens is held, bounded, and handed to the next that does, so a shown-again screen
 * carries on where it stopped. An output batch may beat its terminal's own `OPEN` answer over the bridge,
 * so what is held is keyed by id whether or not the page has heard of it yet; an exit likewise.
 *
 * **One open at a time**: a second waits for the first's answer, so a view shown twice in one render
 * (React's strict mode runs an effect twice) opens one shell, not two.
 *
 * **Desktop-only** (D47 §4): in a browser there is no bridge, and nothing here asks anything. Nothing is
 * logged: a terminal's words are the person's own (D94).
 */
export function useTerminals(): Terminals {
  const { isAvailable } = useShenora();
  const [list, setList] = useState<OpenTerminal[]>([]);
  // The tab shown (CONSOLE4c): held with the terminals, so a view shown again shows the same one.
  const [selected, setSelected] = useState<string | null>(null);
  const [opening, setOpening] = useState(false);
  const [refused, setRefused] = useState<unknown>(null);
  const [shells, setShells] = useState<TerminalShellChoice | null>(null);
  const inFlight = useRef(false);
  const listed = useRef<OpenTerminal[]>([]);
  listed.current = list;
  const sinks = useRef(new Map<string, (data: string) => void>());
  const held = useRef(new Map<string, string>());
  const ended = useRef(new Map<string, number>());

  const open = useCallback((choice: TerminalOpening = {}) => {
    if (!isAvailable || inFlight.current) return;
    inFlight.current = true;
    setOpening(true);
    setRefused(null);
    void callTerminal<{ id?: unknown; shell?: unknown; cwd?: unknown } | undefined>('OPEN', { ...choice })
      .then((answer) => {
        if (typeof answer?.id !== 'string') return;
        const id = answer.id;
        setList((was) => [...was, {
          id,
          shell: typeof answer.shell === 'string' ? answer.shell : choice.shell ?? '',
          cwd: typeof answer.cwd === 'string' ? answer.cwd : '',
          exit: ended.current.get(id) ?? null,
        }]);
        // A new terminal is shown, as a new tab is: the person asked for it to type in.
        setSelected(id);
      })
      .catch((error: unknown) => setRefused(error))
      .finally(() => {
        inFlight.current = false;
        setOpening(false);
      });
  }, [isAvailable]);

  // A keystroke, a size and a close that go nowhere are the terminal having gone, which its exit says.
  const input = useCallback((id: string, data: string) => {
    void callTerminal('INPUT', { id, data }).catch(() => {});
  }, []);

  const resize = useCallback((id: string, cols: number, rows: number) => {
    void callTerminal('RESIZE', { id, cols, rows }).catch(() => {});
  }, []);

  const close = useCallback((id: string) => {
    // The shown tab closed: the one after it is shown, else the one before, as a browser's tabs do.
    const was = listed.current;
    const at = was.findIndex((row) => row.id === id);
    const neighbour = was[at + 1]?.id ?? was[at - 1]?.id ?? null;
    setSelected((shown) => (shown === id ? neighbour : shown));
    setList((rows) => rows.filter((row) => row.id !== id));
    sinks.current.delete(id);
    held.current.delete(id);
    ended.current.delete(id);
    void callTerminal('CLOSE', { id }).catch(() => {});
  }, []);

  const listen = useCallback((id: string, sink: (data: string) => void) => {
    const waiting = held.current.get(id);
    held.current.delete(id);
    if (waiting) sink(waiting);
    sinks.current.set(id, sink);
    return () => {
      if (sinks.current.get(id) === sink) sinks.current.delete(id);
    };
  }, []);

  const askShells = useCallback(() => {
    if (!isAvailable) return;
    void callTerminal<{ shells?: unknown; default?: unknown } | undefined>('SHELLS')
      .then((answer) => {
        const rows: unknown[] = Array.isArray(answer?.shells) ? answer.shells : [];
        setShells({
          shells: rows.map((row) => (row as { shell?: unknown } | null)?.shell).filter((shell): shell is string => typeof shell === 'string'),
          default: typeof answer?.default === 'string' ? answer.default : null,
        });
      })
      .catch(() => {});
  }, [isAvailable]);

  useShenoraEvent('DAORIS', 'TERMINAL_OUTPUT', (payload) => {
    const batch = payload as { id?: unknown; data?: unknown } | undefined;
    if (typeof batch?.id !== 'string' || typeof batch.data !== 'string' || !batch.data) return;
    const sink = sinks.current.get(batch.id);
    if (sink) {
      sink(batch.data);
      return;
    }
    const kept = (held.current.get(batch.id) ?? '') + batch.data;
    held.current.delete(batch.id);
    held.current.set(batch.id, kept.length > TERMINAL_HELD ? kept.slice(-TERMINAL_HELD) : kept);
    if (held.current.size > TERMINALS_HELD) held.current.delete(held.current.keys().next().value!);
  });

  useShenoraEvent('DAORIS', 'TERMINAL_EXITED', (payload) => {
    const exit = payload as { id?: unknown; code?: unknown } | undefined;
    if (typeof exit?.id !== 'string') return;
    const id = exit.id;
    const code = typeof exit.code === 'number' ? exit.code : -1;
    ended.current.set(id, code);
    setList((was) => was.map((row) => (row.id === id ? { ...row, exit: code } : row)));
  });

  return useMemo(
    () => ({ list, selected, select: setSelected, opening, refused, open, input, resize, close, listen, shells, askShells }),
    [list, selected, opening, refused, open, input, resize, close, listen, shells, askShells],
  );
}

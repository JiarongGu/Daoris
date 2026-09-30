import { useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { sentence } from '../format';
import { Button } from '../ui';
import { effectiveDark, subscribeTheme } from '../theme';
import { createScreen, type Screen, type ScreenLook } from './terminalScreen';
import { readToken, terminalTheme } from './terminalTheme';
import type { OpenTerminal, Terminals } from './terminals';

/**
 * Each terminal's screen, for as long as the terminal lives. Kept outside the view, which unmounts
 * whenever it moves between the side bar and the panel or another view is shown, while a screen holds
 * the terminal's scrollback.
 */
const screens = new Map<string, Screen>();
/** The size each terminal was last told, so a view shown again does not tell it twice. */
const told = new Map<string, string>();

/** The tokens' colours and the platform's monospace at the size `host` wears. */
function lookOf(host: HTMLElement | null): ScreenLook {
  const style = host ? getComputedStyle(host) : null;
  return {
    theme: terminalTheme(readToken, effectiveDark()),
    // What the host's `font-mono` and `text-body` resolve to; a page with no styles (a test) has neither.
    fontFamily: style?.fontFamily || 'monospace',
    fontSize: Number.parseFloat(style?.fontSize ?? '') || 13,
  };
}

/** A shell by the name the person knows it by; one this build has no name for, by its id. */
export function shellName(t: (key: string, options?: Record<string, unknown>) => string, shell: string): string {
  return t(`work.terminal.shell.${shell}`, { defaultValue: shell });
}

/**
 * The terminal view (CONSOLE4b, D96): the person's own shell, drawn by a terminal renderer, in whichever
 * region the view stands — the panel by default, beside the console.
 *
 * @remarks
 * **The terminals are handed in**, held by the Work frame (`useTerminals`), because a shell must outlive
 * this view: it unmounts when it moves or when another view of its region is shown. So this reaches the
 * bridge only through what it is handed, and its screens are kept beside it for the same reason.
 *
 * **Shown with none open, it opens one**, where the frame says: the attended session's tree, else the
 * workspace's first repository, else, with nothing said, the module's own answer, the home. Only on
 * showing: a person who closed the last one closed it, and the view offers another rather than forcing it.
 *
 * **Every open terminal is heard while the view is here**, not only the one shown, so a screen is never
 * behind its shell; while the view is away the frame holds what comes, and this catches each screen up.
 */
export function TerminalView({ terminals, cwd }: {
  terminals: Terminals;
  /** Where a terminal opened here starts; absent, the module decides. */
  cwd?: string;
}) {
  const { t } = useTranslation();
  const root = useRef<HTMLDivElement>(null);
  const host = useRef<HTMLDivElement>(null);
  const { list, listen, input, resize } = terminals;
  const shown: OpenTerminal | null = list[list.length - 1] ?? null;
  const startHere = () => terminals.open(cwd ? { cwd } : {});

  const screenOf = (id: string) => {
    let screen = screens.get(id);
    if (!screen) {
      screen = createScreen(lookOf(root.current));
      screens.set(id, screen);
    }
    return screen;
  };

  useEffect(() => {
    if (terminals.list.length === 0) startHere();
    // Once per showing: what is open, and where to start, are read, not watched.
  }, []);

  // The shown terminal in the host: attached, fitted, focused, and typed into while it runs.
  const shownId = shown?.id ?? null;
  const running = shown ? shown.exit === null : false;
  useEffect(() => {
    const element = host.current;
    if (!shownId || !element) return undefined;
    const screen = screenOf(shownId);
    screen.attach(element);
    const fit = () => {
      const size = screen.fit();
      const said = size ? `${size.cols}x${size.rows}` : null;
      if (!size || told.get(shownId) === said) return;
      told.set(shownId, said!);
      resize(shownId, size.cols, size.rows);
    };
    fit();
    screen.focus();
    const typed = running ? screen.onData((data) => input(shownId, data)) : () => {};
    const observer = typeof ResizeObserver === 'undefined' ? null : new ResizeObserver(() => fit());
    observer?.observe(element);
    return () => {
      typed();
      observer?.disconnect();
    };
  }, [shownId, running, input, resize]);

  // Every terminal's output to its screen while the view is here, the ones not shown as well.
  const ids = list.map((row) => row.id).join('\n');
  useEffect(() => {
    const heard = list.map((row) => listen(row.id, (data) => screenOf(row.id).write(data)));
    return () => heard.forEach((stop) => stop());
  }, [ids, listen]);

  // A terminal the page no longer holds lets its screen go.
  useEffect(() => {
    for (const [id, screen] of screens) {
      if (list.some((row) => row.id === id)) continue;
      screen.dispose();
      screens.delete(id);
      told.delete(id);
    }
  }, [ids]);

  // The theme followed: a choice made in Settings, and the system's own when that is the choice.
  useEffect(() => {
    const relook = () => {
      const look = lookOf(root.current);
      for (const row of list) screens.get(row.id)?.look(look);
    };
    const media = typeof window.matchMedia === 'function' ? window.matchMedia('(prefers-color-scheme: dark)') : null;
    media?.addEventListener?.('change', relook);
    const stop = subscribeTheme(relook);
    return () => {
      stop();
      media?.removeEventListener?.('change', relook);
    };
  }, [ids]);

  const again = () => {
    if (!shown) return;
    terminals.close(shown.id);
    terminals.open({ shell: shown.shell, cwd: shown.cwd });
  };

  return (
    <div ref={root} className="flex min-h-0 flex-1 flex-col font-mono text-body">
      {shown
        ? (
          <>
            {/* The renderer draws inside; its background is the page's, so the gutter is one surface. */}
            <div ref={host} className="min-h-0 flex-1 overflow-hidden pl-3 pt-1" />
            {shown.exit !== null && (
              <p className="m-0 flex shrink-0 items-center gap-2 border-t border-line px-3 py-1.5 font-sans text-small text-ink-faint">
                <span>{t('work.terminal.ended', { shell: shellName(t, shown.shell), code: shown.exit })}</span>
                <Button variant="ghost" onClick={again}>{t('work.terminal.again')}</Button>
              </p>
            )}
          </>
        )
        : (
          <div className="flex flex-col items-start gap-2 p-3 font-sans text-small text-ink-faint">
            <p className="m-0">
              {terminals.opening
                ? t('work.terminal.opening')
                : terminals.refused ? sentence(terminals.refused) : t('work.terminal.none')}
            </p>
            {!terminals.opening && <Button onClick={startHere}>{t('work.terminal.open')}</Button>}
          </div>
        )}
    </div>
  );
}

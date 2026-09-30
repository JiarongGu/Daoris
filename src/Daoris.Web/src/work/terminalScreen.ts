import { Terminal } from '@xterm/xterm';
import { FitAddon } from '@xterm/addon-fit';
import '@xterm/xterm/css/xterm.css';
import { frameShortcut } from '../shortcuts';
import type { TerminalTheme } from './terminalTheme';

/** How a screen looks: the tokens' colours, and the platform's monospace at its size. */
export type ScreenLook = { theme: TerminalTheme; fontFamily: string; fontSize: number };

/**
 * One terminal's screen (CONSOLE4b): the renderer, kept thin so the view above it is tested over the
 * bridge with this module stood in for. What is typed goes out through `onData`; what the shell wrote
 * comes in through `write`.
 */
export type Screen = {
  /** Show it in `host`: opened there the first time, moved there after, with its scrollback. */
  attach: (host: HTMLElement) => void;
  write: (data: string) => void;
  /** What the person types, as the renderer encodes it. Returns the way to stop hearing it. */
  onData: (listener: (data: string) => void) => () => void;
  /** Fit it to its host: the size in cells, or null where the host cannot be measured yet. */
  fit: () => { cols: number; rows: number } | null;
  look: (look: ScreenLook) => void;
  focus: () => void;
  dispose: () => void;
};

/**
 * A screen on xterm.js, with its fit addon.
 *
 * @remarks
 * **Moved, not reopened.** A terminal's screen holds its scrollback, and the view that shows it unmounts
 * whenever it moves between the side bar and the panel or another view is shown; xterm opens once, so a
 * shown-again screen has its element moved into the new host, as VS Code moves a terminal between its
 * panel and an editor.
 *
 * **Copy and paste as a Windows console does**: Ctrl+C copies when there is a selection and interrupts
 * when there is none; Ctrl+V is the page's paste, which xterm hears from its own text area. The frame's
 * own keys (`shortcuts.ts`) go to the frame, and every other key to the shell.
 *
 * **The pty is Windows'** (`windowsPty`): xterm then keeps ConPTY's rows as it expects them on a resize.
 */
export function createScreen(look: ScreenLook): Screen {
  const terminal = new Terminal({
    theme: look.theme,
    fontFamily: look.fontFamily,
    fontSize: look.fontSize,
    cursorBlink: true,
    scrollback: 5000,
    windowsPty: { backend: 'conpty' },
  });
  const fitting = new FitAddon();
  terminal.loadAddon(fitting);
  terminal.attachCustomKeyEventHandler((event) => {
    // The frame's own keys are the frame's, as VS Code's terminal leaves its commands to the workbench:
    // left unhandled here, they reach the application's handler rather than the shell.
    if (frameShortcut(event)) return false;
    if (event.type !== 'keydown' || !event.ctrlKey || event.altKey || event.metaKey) return true;
    const key = event.key.toLowerCase();
    if (key === 'c' && terminal.hasSelection()) {
      void navigator.clipboard?.writeText(terminal.getSelection()).catch(() => {});
      terminal.clearSelection();
      return false;
    }
    // Left to the page, whose paste event xterm hears; sending it on would type a ^V.
    return key !== 'v';
  });

  let opened = false;
  return {
    attach: (host) => {
      if (!opened) {
        terminal.open(host);
        opened = true;
      } else if (terminal.element && terminal.element.parentElement !== host) {
        host.appendChild(terminal.element);
      }
    },
    write: (data) => terminal.write(data),
    onData: (listener) => {
      const heard = terminal.onData(listener);
      return () => heard.dispose();
    },
    fit: () => {
      const proposed = opened ? fitting.proposeDimensions() : undefined;
      if (!proposed || !Number.isFinite(proposed.cols) || !Number.isFinite(proposed.rows) || proposed.cols < 1 || proposed.rows < 1) {
        return null;
      }
      fitting.fit();
      return { cols: terminal.cols, rows: terminal.rows };
    },
    look: (next) => {
      terminal.options.theme = next.theme;
      terminal.options.fontFamily = next.fontFamily;
      terminal.options.fontSize = next.fontSize;
    },
    focus: () => terminal.focus(),
    dispose: () => terminal.dispose(),
  };
}

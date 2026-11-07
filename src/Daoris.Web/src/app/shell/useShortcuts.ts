import type * as React from 'react';
import { useEffect, useRef } from 'react';
import { isTextField } from '../../menus/press';
import { commandForKey } from '../../menus/shortcuts';
import { logEvent } from '../../bridge/shell';
import { type CommandDoors, type CommandState, commandTable, MENUS } from '../../menus/commands';
import { focusMenuBar } from '../../work/strip/AppMenu';
import { loggedId } from '../shellHelpers';

export function useShortcuts({ commandState, commandDoors, translate, openMenu, attached, setMnemonics, setOpenMenu }: {
  commandState: () => CommandState;
  commandDoors: CommandDoors;
  translate: (key: string, values?: Record<string, unknown> | undefined) => string;
  openMenu: string | null;
  attached: boolean;
  setMnemonics: React.Dispatch<React.SetStateAction<boolean>>;
  setOpenMenu: React.Dispatch<React.SetStateAction<string | null>>;
}) {
  // The keys, read from the one table at the press, so a key runs what its menu row runs and only while it
  // applies: a field keeps what a field means, the terminal that and a shell's next line, and a browser what it keeps.
  const tableNow = useRef<() => ReturnType<typeof commandTable>>(() => []);
  tableNow.current = () => commandTable(commandState(), commandDoors, translate);
  const menuOpen = useRef(openMenu);
  menuOpen.current = openMenu;
  useEffect(() => {
    // Alt pressed and let go with nothing between reaches the bar, as Windows' menu bars do (the design §3.3).
    let altAlone = false;
    const onKey = (event: KeyboardEvent) => {
      // A shell's alone: a browser keeps Alt and F10 for its own menu.
      if (attached && event.key === 'Alt' && !event.ctrlKey && !event.shiftKey && !event.metaKey) {
        altAlone = true;
        setMnemonics(true);
        return;
      }
      altAlone = false;
      if (attached && event.altKey && !event.ctrlKey && !event.shiftKey && !event.metaKey) {
        const menu = MENUS.find((each) => (!each.shell || attached) && event.code === `Key${each.letter}`);
        if (menu) {
          event.preventDefault();
          setMnemonics(false);
          setOpenMenu(menu.id);
          return;
        }
      }
      if (attached && event.key === 'F10' && !event.shiftKey && !event.ctrlKey && !event.altKey && !event.metaKey) {
        event.preventDefault();
        focusMenuBar(document);
        return;
      }
      if (event.defaultPrevented) return;
      const target = event.target instanceof Element ? event.target : null;
      const entry = commandForKey(tableNow.current(), event, {
        field: isTextField(target), terminal: Boolean(target?.closest('.xterm')),
      });
      if (!entry) return;
      event.preventDefault();
      logEvent('command.run', { command: loggedId(entry.id) });
      entry.run();
    };
    const onKeyUp = (event: KeyboardEvent) => {
      if (event.key !== 'Alt') return;
      setMnemonics(false);
      if (!altAlone) return;
      altAlone = false;
      event.preventDefault();
      // With a menu open, Alt closes it, as Windows' does; with none, it reaches the bar.
      if (menuOpen.current !== null) setOpenMenu(null);
      else focusMenuBar(document);
    };
    const reset = () => {
      altAlone = false;
      setMnemonics(false);
    };
    window.addEventListener('keydown', onKey);
    window.addEventListener('keyup', onKeyUp);
    window.addEventListener('blur', reset);
    return () => {
      window.removeEventListener('keydown', onKey);
      window.removeEventListener('keyup', onKeyUp);
      window.removeEventListener('blur', reset);
    };
  }, [attached]);
}

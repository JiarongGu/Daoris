import { Fragment, useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Menu, type MenuAct } from '../ui';
import {
  type ContextDoors, contextSections, insideMenu, isMenuKey, isTextField, keyPoint, offeredContext, pressedOn,
} from './press';

/**
 * **The right-click menu** (CTX1, D138): MENU1's menu, opened at a point rather than under a trigger, its groups between
 * rules. A molecule: every state is reached by its props.
 *
 * @remarks
 * - **Anchored on a point**: a trigger of no size, fixed where the press was, so the menu's place and its turning back
 *   from the window's edge are the menu atom's, as every other menu's are.
 * - **Named by what it is for** (`label`): the surface's *Actions for …*, else *Actions here*. Radix names a menu by its
 *   trigger, which here is nothing a reader can hear, so the name is said on the menu itself.
 * - **Opened by a key, its first act has the focus** (design §5); opened by the pointer, the menu itself does, and the
 *   arrows go from there, as Radix's own context menu does.
 * - **Closed, the focus goes back where it was**, unless the act chosen moved it: a reason's field that opened, Quick
 *   Ask's box. Radix's own would focus the trigger, which is nothing.
 */
export function ContextMenu({ open = true, at, label, sections, keyed = false, returnTo = null, onCopy, onClose }: {
  open?: boolean;
  /** Where it opens, in the window's pixels: the pointer, or the focused element's corner. */
  at: { x: number; y: number };
  label: string;
  /** Its groups, most specific first; a rule between each two. */
  sections: readonly (readonly MenuAct[])[];
  /** Opened by its key, so its first act takes the focus. */
  keyed?: boolean;
  /** Where the focus was before it opened, given back on closing. */
  returnTo?: HTMLElement | null;
  /** A copy chosen: the menu owner copies and says so. */
  onCopy?: (text: string) => void;
  onClose: () => void;
}) {
  return (
    <Menu.Root open={open} onOpenChange={(next) => { if (!next) onClose(); }}>
      <Menu.Trigger asChild>
        <span aria-hidden tabIndex={-1} className="pointer-events-none fixed h-0 w-0" style={{ left: at.x, top: at.y }} />
      </Menu.Trigger>
      <Menu.Content
        aria-label={label}
        // Named on itself, never by its trigger, which is no name a reader hears.
        aria-labelledby={undefined}
        side="bottom"
        align="start"
        sideOffset={2}
        className="min-w-48"
        ref={keyed ? focusFirst : undefined}
        onCloseAutoFocus={(event) => {
          event.preventDefault();
          const now = document.activeElement;
          // An act that moved the focus keeps it; only a focus the closing dropped goes back.
          if (now && now !== document.body) return;
          if (returnTo?.isConnected) returnTo.focus({ preventScroll: true });
        }}
      >
        {sections.map((acts, index) => (
          // A group is its acts' ids, which are unique in a menu.
          <Fragment key={acts.map((act) => act.id).join(' ') || index}>
            {index > 0 && <Menu.Separator />}
            <Menu.Acts acts={acts} onCopy={onCopy} />
          </Fragment>
        ))}
      </Menu.Content>
    </Menu.Root>
  );
}

/**
 * A menu opened by its key gives its first act the focus, once the menu atom has focused the menu itself: a beat after it
 * is drawn. Radix moves to the first item only for a key it heard, and the menu key was heard before the menu existed.
 */
function focusFirst(content: HTMLDivElement | null) {
  if (!content) return;
  window.setTimeout(() => {
    content.querySelector<HTMLElement>('[role="menuitem"]:not([data-disabled])')?.focus({ preventScroll: true });
  }, 0);
}

/** A menu the window has open: where, what, and how it was asked for. */
type Opened = {
  /** Each opening its own, so a menu asked for elsewhere is a new one at its new place. */
  id: number;
  open: boolean;
  at: { x: number; y: number };
  label: string;
  sections: MenuAct[][];
  keyed: boolean;
  returnTo: HTMLElement | null;
};

/**
 * **The window's one right-click handler** (CTX1, D138, design §3): mounted once per window, it reads what a press is on
 * and opens the menu for it, with the doors the frame hands it (copy, search, ask, open in Daoris's browser).
 *
 * @remarks
 * - **A surface offers its acts with `contextOffer`** on its root; it holds no hook, so a molecule offers its own. This
 *   reads the innermost offer from the event once every surface has heard it, since it listens on the document.
 * - **A text field keeps the engine's own menu**, and a surface that answered the press itself (a tab's views menu) is
 *   left to it. Everywhere else the engine's menu is suppressed, menu or none (D138 §4).
 * - **The keys** (design §5): the menu key and Shift+F10 ask for the focused element's menu, at its corner, by the same
 *   path a right-click takes, so a surface offers once for both. The engine asks for its own on the menu key's way up on
 *   Windows, so the key is stopped there too, and a second ask while a menu is open asks for nothing.
 * - **It closes when the window loses the focus or changes size**, as a menu of the system's does.
 */
export function ContextMenus({ doors, shell = true }: {
  doors: ContextDoors;
  /**
   * Whether this window is the desktop's, whose engine's menu offers a web page's acts and is suppressed. A browser is
   * the person's own, with its own acts (a new tab, its translation, its extensions): there the page's menu opens only
   * where a surface offers acts, and the browser's stays everywhere else (design §2).
   */
  shell?: boolean;
}) {
  const { t } = useTranslation();
  const [menu, setMenu] = useState<Opened | null>(null);
  // Read when a press comes, so the handlers are installed once and still see this render's doors, words and window.
  const latest = useRef({ doors, t, shell });
  latest.current = { doors, t, shell };
  // Set while the keys' own ask is dispatched, so the menu it opens knows a key asked for it.
  const keyed = useRef(false);
  const opened = useRef(0);

  useEffect(() => {
    const onContext = (event: MouseEvent) => {
      if (event.defaultPrevented) return;
      const target = event.target instanceof Element ? event.target : null;
      if (!target) return;
      if (insideMenu(target)) {
        event.preventDefault();
        return;
      }
      if (isTextField(target)) return;
      const offer = offeredContext(event);
      // A browser's own menu, where no surface offers acts.
      if (!latest.current.shell && !offer) return;
      event.preventDefault();

      const made = contextSections({
        pressed: pressedOn(target, window.getSelection()),
        offer,
        doors: latest.current.doors,
        t: latest.current.t,
      });
      if (made.sections.length === 0) {
        setMenu((was) => (was ? { ...was, open: false } : was));
        return;
      }
      opened.current += 1;
      const focused = document.activeElement;
      setMenu({
        id: opened.current,
        open: true,
        at: { x: event.clientX, y: event.clientY },
        label: made.label,
        sections: made.sections,
        keyed: keyed.current,
        returnTo: focused instanceof HTMLElement && focused !== document.body ? focused : null,
      });
    };

    const onKeyDown = (event: KeyboardEvent) => {
      if (!isMenuKey(event)) return;
      const target = document.activeElement ?? document.body;
      if (isTextField(target) || insideMenu(target)) return;
      event.preventDefault();
      const point = keyPoint(target.getBoundingClientRect(), { width: window.innerWidth, height: window.innerHeight });
      keyed.current = true;
      try {
        target.dispatchEvent(new MouseEvent('contextmenu', {
          bubbles: true, cancelable: true, clientX: point.x, clientY: point.y, button: 2,
        }));
      } finally {
        keyed.current = false;
      }
    };

    // Windows asks for the engine's own menu as the menu key comes up: stopped wherever the page answers the key.
    const onKeyUp = (event: KeyboardEvent) => {
      if (event.key === 'ContextMenu' && !isTextField(document.activeElement)) event.preventDefault();
    };

    // A window that changes size moves what the menu was opened on. Losing the focus closes it too, which the menu atom does.
    const close = () => setMenu((was) => (was?.open ? { ...was, open: false } : was));

    document.addEventListener('contextmenu', onContext);
    document.addEventListener('keydown', onKeyDown);
    document.addEventListener('keyup', onKeyUp);
    window.addEventListener('resize', close);
    return () => {
      document.removeEventListener('contextmenu', onContext);
      document.removeEventListener('keydown', onKeyDown);
      document.removeEventListener('keyup', onKeyUp);
      window.removeEventListener('resize', close);
    };
  }, []);

  if (!menu) return null;
  return (
    <ContextMenu
      key={menu.id}
      open={menu.open}
      at={menu.at}
      label={menu.label}
      sections={menu.sections}
      keyed={menu.keyed}
      returnTo={menu.returnTo}
      onCopy={(text) => latest.current.doors.copy(text)}
      onClose={() => setMenu((was) => (was && was.id === menu.id ? { ...was, open: false } : was))}
    />
  );
}

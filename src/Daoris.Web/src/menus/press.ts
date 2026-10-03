import type { MouseEvent as ReactMouseEvent } from 'react';
import { webAddress } from '../links';
import type { MenuAct } from '../ui';

// The right-click menu's rules (CTX1, D138, design §3): what a press is on, what a surface offers, and the groups the
// menu draws, most specific first. Pure, so `ContextMenus` only listens and draws, and each case is an assertion.

/** What a surface offers a right-click: its acts, as its ⋯ or its page lists them, and what it is called. */
export type ContextOffer = {
  /** The surface's name (a session's title, a quest's), which names the menu: *Actions for …*. */
  label?: string;
  acts: readonly MenuAct[];
};

/** What the frame can do with a press's words or link; each absent where this window cannot (design §6). */
export type ContextDoors = {
  /** Copy the words and say so. */
  copy: (text: string) => void;
  /** Search Daoris for the words: Search, with them in its box. */
  search?: (text: string) => void;
  /** Ask Daoris about the words: Quick Ask, with them quoted in its box. A shell's. */
  ask?: (text: string) => void;
  /** Open a web page in Daoris's browser, whatever the links setting says. A shell's. */
  openInBrowser?: (address: string) => void;
};

/** A link a press is on: its address, the web page Daoris's browser may open, and whether it must stay the system's. */
export type PressedLink = { href: string; web: string | null; system: boolean; element: HTMLAnchorElement };

/** What a press is on, besides the surface that offers acts. */
export type Pressed = {
  /** The selected words, where the press is on the selection. */
  selection: string | null;
  link: PressedLink | null;
  /** What a code span, a block of code or a path copies. */
  copy: string | null;
};

const offers = new WeakMap<Event, ContextOffer>();

/**
 * A surface offers its acts for this press. The innermost surface is the first to hear the event, so it wins; an offer
 * of nothing leaves the press to the surface around it.
 */
export function offerContext(event: Event, offer: ContextOffer): void {
  if (offer.acts.length === 0 || offers.has(event)) return;
  offers.set(event, offer);
}

/** What the nearest surface offered for this press, or null. */
export function offeredContext(event: Event): ContextOffer | null {
  return offers.get(event) ?? null;
}

/**
 * What a surface spreads on its root to offer its acts to a right-click (design §6). It holds no hook, so a molecule may
 * offer; with nothing to offer it adds nothing.
 */
export function contextOffer(offer: ContextOffer | null | undefined): { onContextMenu?: (event: ReactMouseEvent) => void } {
  if (!offer || offer.acts.length === 0) return {};
  return { onContextMenu: (event) => offerContext(event.nativeEvent, offer) };
}

/** An input that takes words: every kind but a box, a choice, a button, a range, a colour and a file. */
const NOT_TYPED = new Set(['checkbox', 'radio', 'button', 'submit', 'reset', 'image', 'range', 'color', 'file', 'hidden']);

/**
 * Whether a press is in a field that takes words, where the engine's own menu stays (D138 §4): cut, copy and paste, which
 * the page could not offer without a clipboard permission. The terminal is one: its menu pastes into the shell.
 */
export function isTextField(element: Element | null): boolean {
  if (!element) return false;
  if (element.closest('textarea, .xterm, [contenteditable=""], [contenteditable="true"], [contenteditable="plaintext-only"]')) return true;
  const input = element.closest('input');
  return input !== null && !NOT_TYPED.has(input.type);
}

/** Whether a press is inside a menu already open: a right-click there asks for nothing. */
export function insideMenu(element: Element | null): boolean {
  return element?.closest('[role="menu"]') != null;
}

/** The keys that ask for a menu (design §5): the menu key alone, and Shift+F10. */
export function isMenuKey(event: KeyboardEvent): boolean {
  const others = event.ctrlKey || event.altKey || event.metaKey;
  if (event.key === 'ContextMenu') return !others && !event.shiftKey;
  return event.key === 'F10' && event.shiftKey && !others;
}

/** The room kept between a menu opened by its key and the window's edge. */
const EDGE = 8;
/** A row's height and a little: below it, an element is a page, and its first line is where the menu goes. */
const LINE = 32;

/**
 * Where a menu opened by its key goes: under the focused element, a little in from its left, or under its first line
 * where it is taller than a row, and inside the window. A page focused whole would otherwise put it past the window's foot.
 */
export function keyPoint(rect: DOMRect, viewport: { width: number; height: number }): { x: number; y: number } {
  const clamp = (value: number, most: number) => Math.max(EDGE, Math.min(value, most - EDGE));
  return {
    x: clamp(rect.left + EDGE, viewport.width),
    y: clamp(rect.top + Math.min(rect.height, LINE), viewport.height),
  };
}

/** The selected words, where the press is on them, or null: a selection left elsewhere is not what was pressed. */
function selectedAt(target: Element, selection: Selection | null): string | null {
  if (!selection || selection.isCollapsed || selection.rangeCount === 0) return null;
  const words = selection.toString();
  if (!words.trim()) return null;
  // Each range asked whether it meets the press, rather than `containsNode`, which jsdom answers wrongly for an element
  // whose contents are selected and which the window answers the same as this.
  for (let index = 0; index < selection.rangeCount; index += 1) {
    if (selection.getRangeAt(index).intersectsNode(target)) return words;
  }
  return null;
}

/** What a press is on: the selection under it, the link around it, and the code or path it copies. */
export function pressedOn(target: Element, selection: Selection | null): Pressed {
  const anchor = target.closest<HTMLAnchorElement>('a[href]');
  const link = anchor
    ? {
      href: anchor.getAttribute('href') ?? '',
      web: webAddress(anchor.getAttribute('href')),
      // A sign-in's link is always the system's browser (D78 §3.4), and says so on itself.
      system: anchor.dataset.link === 'system',
      element: anchor,
    }
    : null;
  // Code, never a bare `pre`: an entry read as it is written and a session's evidence are pages of text, not a span.
  const copying = target.closest<HTMLElement>('[data-copy], code');
  const copy = copying ? (copying.dataset.copy || copying.textContent || '') : null;
  return { selection: selectedAt(target, selection), link, copy: copy || null };
}

type Translate = (key: string, values?: Record<string, unknown>) => string;

/**
 * The groups the menu draws for a press (design §3), most specific first, each a list of acts: the selection's, the
 * link's, the code's where nothing is selected, then the surface's. Its name is the surface's where one offered. No
 * group is no menu, and the engine's is suppressed.
 */
export function contextSections({ pressed, offer, doors, t }: {
  pressed: Pressed;
  offer: ContextOffer | null;
  doors: ContextDoors;
  t: Translate;
}): { label: string; sections: MenuAct[][] } {
  const sections: MenuAct[][] = [];
  const { selection, link, copy } = pressed;

  if (selection) {
    const { search, ask } = doors;
    sections.push([
      { id: 'copy', label: t('contextMenu.act.copy'), icon: 'copy', copy: selection },
      ...(search ? [{ id: 'search', label: t('contextMenu.act.search'), icon: 'search' as const, onSelect: () => search(searchWords(selection)) }] : []),
      ...(ask ? [{ id: 'ask', label: t('contextMenu.act.ask'), icon: 'help' as const, onSelect: () => ask(quoted(selection)) }] : []),
    ]);
  }

  if (link) {
    const { openInBrowser } = doors;
    const web = link.system ? null : link.web;
    sections.push([
      // The link's own click, so it opens where the person chose (BRW7), as a press on it would.
      { id: 'open', label: t('contextMenu.act.open'), icon: 'external', onSelect: () => link.element.click() },
      ...(openInBrowser && web
        ? [{ id: 'openInBrowser', label: t('contextMenu.act.openInBrowser'), icon: 'browser' as const, onSelect: () => openInBrowser(web) }]
        : []),
      { id: 'copyLink', label: t('contextMenu.act.copyLink'), icon: 'link', copy: link.href },
    ]);
  }

  if (copy && !selection) sections.push([{ id: 'copyCode', label: t('contextMenu.act.copy'), icon: 'copy', copy }]);

  if (offer) sections.push([...offer.acts]);

  return { label: offer?.label ? t('contextMenu.for', { name: offer.label }) : t('contextMenu.label'), sections };
}

/** The selected words as Quick Ask's box holds them: quoted, each line its own, and room for the question after. */
export function quoted(text: string): string {
  return `${text.trim().split('\n').map((line) => `> ${line.trim()}`).join('\n')}\n\n`;
}

/** The longest search a selection hands on: a box holds a line, not a page. */
const SEARCH_MOST = 200;

/** The selected words as Search's box takes them: one line, spaces collapsed, not too many. */
export function searchWords(text: string): string {
  return text.replace(/\s+/g, ' ').trim().slice(0, SEARCH_MOST);
}

/** What a copy's toast says it copied: one line, cut where it would not fit. */
export function clipped(text: string, most = 60): string {
  const line = text.replace(/\s+/g, ' ').trim();
  return line.length <= most ? line : `${line.slice(0, most - 1).trimEnd()}…`;
}

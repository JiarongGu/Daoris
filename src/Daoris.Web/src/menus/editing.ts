import type { EditAct } from '../commands';
import { isTextField } from './press';

// The menu bar's Edit and Find (UX7a, D152 §3.2), as the window carries them out: a field's own acts on the field the
// person was in, and Find on the box in front. Pure over a document, so jsdom asserts each case.

/** Whether an element is a field the Edit menu acts on: one that takes words, and not the terminal, whose keys are its own. */
const isEditField = (element: Element | null): element is HTMLElement =>
  element instanceof HTMLElement && isTextField(element) && !element.closest('.xterm');

/** Whether the focus went into the menu bar or a menu, which takes it from a field and gives it back. */
const isMenuChrome = (element: Element | null) => Boolean(element?.closest('[data-menu-bar], [role="menu"]'));

/**
 * The field the person was last in, kept while the focus is on the menu bar or in a menu (D152 §3.4: a menu takes the
 * focus from the field its Edit acts on) and let go when it moves anywhere else.
 */
export function fieldTracker(doc: Document): { field: () => HTMLElement | null; stop: () => void } {
  let last: HTMLElement | null = isEditField(doc.activeElement) ? doc.activeElement : null;
  const onFocus = (event: FocusEvent) => {
    const target = event.target as Element | null;
    if (isEditField(target)) last = target;
    else if (!isMenuChrome(target)) last = null;
  };
  doc.addEventListener('focusin', onFocus);
  return {
    field: () => (last?.isConnected ? last : null),
    stop: () => doc.removeEventListener('focusin', onFocus),
  };
}

/**
 * Edit's act on the field (the design §3.2): the focus goes back to it first, then the engine's own command, so undo
 * keeps the field's own history. *Copy* copies the page's selection where no field holds one. *Paste* reads the clipboard,
 * which the engine may refuse a page; the promise says whether it landed, so the window can say so.
 */
export async function runEdit(act: EditAct, field: HTMLElement | null, doc: Document = document): Promise<boolean> {
  field?.focus();
  if (act === 'paste') {
    if (!field) return false;
    try {
      const text = await navigator.clipboard.readText();
      return doc.execCommand('insertText', false, text);
    } catch {
      return false;
    }
  }
  if (act === 'selectAll' && (field instanceof HTMLInputElement || field instanceof HTMLTextAreaElement)) {
    field.select();
    return true;
  }
  return doc.execCommand(act === 'selectAll' ? 'selectAll' : act);
}

/**
 * The box Find goes to (the design §3.2): the conversation's find while a session is attended on Sessions, else the
 * view's list's box (Sessions' search, Repositories' filter, Knowledge's), else none, and Find is off.
 */
export function findTarget(doc: Document, view: string): HTMLInputElement | null {
  const search = (scope: string) => doc.querySelector<HTMLInputElement>(`${scope} input[type="search"]:not([disabled])`);
  return (view === 'sessions' ? search('[data-region="main"]') : null) ?? search('[data-region="list"]');
}

import { fireEvent, screen, within } from '@testing-library/react';

// The right-click, as the tests of each surface press it (CTX1, D138).

/**
 * A right-click on an element, and whether the engine's own menu was left to open (the event not prevented).
 *
 * @remarks
 * **It focuses first what the engine's press would**: the nearest focusable element around it, or the page. A real
 * press does, and jsdom needs it: once a focused element is removed (a menu that closed) jsdom counts the document as
 * focused, so the next focus fires `blur` at the window, which closes the next menu as a window losing the focus does.
 * Chromium fires no such `blur`. And as a right press on selected words keeps them selected, the selection is kept
 * across that focus, which jsdom would drop.
 */
export function rightClick(element: Element, at = { clientX: 40, clientY: 60 }): boolean {
  const selection = window.getSelection();
  const ranges = selection
    ? Array.from({ length: selection.rangeCount }, (_, index) => selection.getRangeAt(index).cloneRange())
    : [];
  const focusable = element.closest<HTMLElement>('button, a[href], input, textarea, select, [tabindex]')
    ?? document.querySelector<HTMLElement>('[data-page]');
  if (focusable) focusable.focus();
  else {
    // A press on nothing focusable leaves nothing focused: jsdom's "the document is focused" is reset by a focus and a
    // blur on something of its own, before the menu opens, so the menu's own focus fires no blur at the window.
    const nothing = document.body.appendChild(document.createElement('button'));
    nothing.focus();
    nothing.blur();
    nothing.remove();
  }
  if (selection && ranges.some((range) => !range.collapsed)) {
    selection.removeAllRanges();
    for (const range of ranges) selection.addRange(range);
  }
  return fireEvent.contextMenu(element, at);
}

/** The menu open now and its acts' names, in order. */
export async function menuActs(name?: string): Promise<string[]> {
  const menu = await screen.findByRole('menu', name ? { name } : {});
  return within(menu).getAllByRole('menuitem').map((item) => item.textContent ?? '');
}

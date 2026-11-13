/** What marks an Accept card in the conversation, for the lines that tell the person a card waits to lead to it. */
export const CARD_MARK = 'data-review-card';

/**
 * Leads the person to the newest Accept card drawn under `root`'s conversation: scrolls it into view and gives it the focus.
 * False where none is drawn, so the caller can lead elsewhere.
 */
export function seekReviewCard(root: ParentNode = document): boolean {
  const cards = root.querySelectorAll<HTMLElement>(`[${CARD_MARK}]`);
  const card = cards[cards.length - 1];
  if (!card) return false;
  card.scrollIntoView?.({ block: 'center' });
  card.focus({ preventScroll: true });
  return true;
}

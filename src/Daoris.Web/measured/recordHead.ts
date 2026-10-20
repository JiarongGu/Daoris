/**
 * **How far down the record's head puts the conversation, page-side** (MILESTONEUX1; the UI/UX design §3.7 and §9 row 6; UX7's
 * budget, the UX7 design §6.2): from the main area's top, which holds the pinned page head, to the top of the conversation's
 * first box, as the browser laid it out, and each block between them with its own height so a run that passes the budget says
 * which block costs it.
 *
 * Self-contained, with no import and nothing from outside its body, so Playwright can send it to the page as source.
 */
export type RecordHeadMeasure = {
  /** The main area's width as the browser drew it, in px. */
  width: number;
  /** The conversation's top, down from the main area's top, in px. */
  down: number;
  /** The pinned page head's height, and each block of the record in order. */
  blocks: { name: string; top: number; height: number }[];
};

export function measureRecordHead(): RecordHeadMeasure {
  const main = document.querySelector<HTMLElement>('[data-record-frame] main')!;
  const conversation = document.querySelector<HTMLElement>('[data-conversation]')!;
  const record = main.querySelector<HTMLElement>('article')!;
  // The main area scrolls by itself, and pressing a control below its fold scrolls it: a place is its place on the page, not on
  // the screen, so a scroll is no move.
  const origin = main.getBoundingClientRect().top - main.scrollTop;
  const name = (element: Element) => {
    const label = element.getAttribute('aria-label') ?? element.getAttribute('data-region');
    const text = (element.textContent ?? '').replace(/\s+/g, ' ').trim().slice(0, 40);
    return label ? `${element.tagName.toLowerCase()} [${label}] ${text}` : `${element.tagName.toLowerCase()} ${text}`;
  };
  const box = (element: Element) => {
    const rect = element.getBoundingClientRect();
    return { name: name(element), top: Math.round((rect.top - origin) * 10) / 10, height: Math.round(rect.height * 10) / 10 };
  };
  // The page head is the main area's own first child above the record; the record's blocks are the article's.
  const pinned = [...main.children].filter((element) => element !== record && element !== conversation);
  return {
    width: Math.round(main.getBoundingClientRect().width),
    down: Math.round((conversation.getBoundingClientRect().top - origin) * 10) / 10,
    blocks: [...pinned.map(box), ...[...record.children].map(box)],
  };
}

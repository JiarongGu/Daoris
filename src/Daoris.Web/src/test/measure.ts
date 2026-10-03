/**
 * Every width cap a rendered page's blocks wear, as `<tag> class` (D141, LAYOUT11). A page is one column and the column
 * is its pane, so a record page's blocks cap nothing: `max-w-full` and `max-w-none` are the column's own width and are
 * not caps. jsdom lays nothing out, so the class is what a test can read; the source scan in `tokens.test.ts` holds the
 * names, and this holds what a page actually draws, a class an atom adds included.
 */
export function cappedBlocks(root: Element): string[] {
  return [...root.querySelectorAll('[class*="max-w-"]')].flatMap((element) =>
    (element.getAttribute('class') ?? '').split(/\s+/)
      .filter((name) => /(?:^|:)max-w-/.test(name) && !/(?:^|:)max-w-(?:full|none)$/.test(name))
      .map((name) => `<${element.tagName.toLowerCase()}> ${name}`));
}

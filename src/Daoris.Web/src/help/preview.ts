/**
 * An answer's Markdown as a line of plain words (ASKHIST1c): what a row of Ask Daoris's history shows of a conversation, and
 * of where a search found its words. The row read the agent's raw Markdown (`**#d208d4**`, backticks); a row is a button,
 * so its line holds words only, never a link, a code span or a button of its own.
 *
 * @remarks
 * The delimiters go and the words stay: emphasis, headings, quotes, list and task marks, a fence's backticks and its
 * language, a table's bars, and a link's or an image's address (its words kept). A code span keeps its contents. An
 * underscore stays, since `__init__` and `snake_case` are names, not emphasis (the rail's `readable` keeps them for the same
 * reason). The driver cuts a line before the page has it, so a delimiter may have no partner: it goes all the same.
 *
 * Pure, and shared by the row's line and its search's find, so the two cannot disagree on what a line says.
 */
export function previewText(markdown: string): string {
  return markdown
    .split(/\r?\n/)
    .map(line => line
      // A table's divider row says nothing.
      .replace(/^\s*\|?\s*:?-{2,}:?\s*(\|\s*:?-{2,}:?\s*)*\|?\s*$/, '')
      // A line's own marks: a heading, a quote, a list's bullet or number, then a task's box.
      .replace(/^\s*#{1,6}\s+/, '')
      .replace(/^\s*(>\s?)+/, '')
      .replace(/^\s*(?:[-*+]|\d+[.)])\s+/, '')
      .replace(/^\[[ xX]\]\s+/, ''))
    .join(' ')
    // A fence and its language, wherever the driver's cut left it.
    .replace(/(`{3,}|~{3,})[\w+-]*/g, ' ')
    // An image's words, a link's words, an address in angle brackets.
    .replace(/!\[([^\]]*)\]\([^)]*\)/g, '$1')
    .replace(/\[([^\]]+)\]\([^)]*\)/g, '$1')
    .replace(/\[([^\]]+)\]\[[^\]]*\]/g, '$1')
    .replace(/<((?:https?|mailto):[^>\s]+)>/g, '$1')
    // A code span keeps its contents; strong, struck and starred emphasis lose their marks. An escaped mark is the mark.
    .replace(/`+/g, '')
    .replace(/\*\*|~~/g, '')
    .replace(/(^|[^\\\w*])\*(?=\S)([^*\n]*?\S)\*(?![\w*])/g, '$1$2')
    .replace(/\\([\\`*_{}[\]()#+\-.!|>~])/g, '$1')
    // A table's bars between its cells.
    .replace(/\s*\|\s*/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
}

/** How much of a parsed line a row holds (ASKHIST1d2): past the two lines its clamp shows at any width the panel takes. */
export const ROW_LINE = 240;

/** How much of a found line is kept before the words a search found, so they show in a row's two lines at the dock's floor. */
export const ROW_BEFORE = 24;

/** Whether the unit at `at` is the second half of a character outside the basic plane, which a cut there would split. */
const second = (text: string, at: number) => /[\uDC00-\uDFFF]/.test(text.charAt(at));

/**
 * A parsed line cut to what a row holds (ASKHIST1d2): from its start, or with `words` found in it, from a little before them,
 * so a clamp to two lines never hides what a search found; cut between words where it has spaces, by its characters where it
 * has none (中文), never inside one, with an ellipsis wherever it goes on.
 *
 * @remarks
 * Run on what {@link previewText} made of the driver's Markdown, never before it: a cut made on Markdown can split a delimiter
 * from its partner (D158's ASKHIST1d1 note). The words are found as the row marks them (`marked`), case aside. The clamp
 * still cuts what is too wide; this cut keeps a row's name, which a reader hears whole, to a few lines.
 */
export function rowLine(plain: string, words = '', limit = ROW_LINE): string {
  const wanted = words.trim().toLowerCase();
  const at = wanted ? plain.toLowerCase().indexOf(wanted) : -1;
  let from = 0;
  if (at > ROW_BEFORE) {
    from = at - ROW_BEFORE;
    const space = plain.indexOf(' ', from);
    if (space >= 0 && space < at) from = space + 1;
    else if (second(plain, from)) from -= 1;
  }
  let to = Math.min(plain.length, from + limit);
  if (to < plain.length && plain[to] !== ' ') {
    const space = plain.lastIndexOf(' ', to);
    if (space > Math.max(from, at + wanted.length)) to = space;
    else if (second(plain, to)) to -= 1;
  }
  return `${from > 0 ? '…' : ''}${plain.slice(from, to).trim()}${to < plain.length ? '…' : ''}`;
}

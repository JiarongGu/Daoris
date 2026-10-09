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

/** One run of an excerpt: either a term the query matched, or the text between matches. */
export interface Segment {
  text: string;
  hit: boolean;
}

/**
 * The query's terms: lower-cased, and nothing of two characters or fewer — so what is marked is what
 * could have matched, never a stray "a" or "of".
 *
 * 🔴 **The floor is a Latin heuristic and says so.** Two characters is a whole word in 中文 — 记录,
 * 会话, 委托 — and a floor that dropped them would mark nothing for exactly the readers this
 * platform ships a second language for. A term in a script whose words are that short keeps
 * itself whatever its length. Found by this module's own test, which is what a test in the other
 * language is for.
 */
export function termsOf(query: string): string[] {
  return [...new Set(
    query.toLowerCase().split(/[^\p{L}\p{N}_-]+/u)
      .filter((term) => term.length > 2 || SHORT_WORD_SCRIPTS.test(term)),
  )];
}

/** Scripts in which a one- or two-character token is a word, not a fragment. */
const SHORT_WORD_SCRIPTS = /[\p{Script=Han}\p{Script=Hiragana}\p{Script=Katakana}\p{Script=Hangul}]/u;

/**
 * An excerpt split into the runs a query matched and the runs it did not.
 *
 * @remarks
 * 🔴 **Found on the deployed application** (D62), on the first real index. The service centres each
 * excerpt on the first matching term and says why in its own source: *"a result list that cannot
 * show its reasoning gets treated as an oracle."* The view then rendered the excerpt as plain
 * text, so the reasoning was there and invisible — twenty-six results for one word, each a
 * sentence the reader had to scan for the word themselves.
 *
 * Case-insensitive and substring, matching how the service finds the term (`IndexOf`, ordinal,
 * ignoring case). A term that appears in the middle of a longer word is marked inside it, which is
 * what actually matched. Pure and cheap: it runs once per excerpt on render.
 */
export function mark(text: string, query: string): Segment[] {
  const terms = termsOf(query);
  if (!text || terms.length === 0) return text ? [{ text, hit: false }] : [];

  const pattern = new RegExp(`(${terms.map(escape).join('|')})`, 'giu');
  const segments: Segment[] = [];
  let last = 0;
  for (const found of text.matchAll(pattern)) {
    const at = found.index ?? 0;
    if (at > last) segments.push({ text: text.slice(last, at), hit: false });
    segments.push({ text: found[0], hit: true });
    last = at + found[0].length;
  }
  if (last < text.length) segments.push({ text: text.slice(last), hit: false });
  return segments;
}

const escape = (term: string) => term.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

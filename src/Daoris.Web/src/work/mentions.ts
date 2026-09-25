// `@` a file in the session's tree (CONV4d). The mention is TEXT, and both doors expand it as typed
// (`docs/2026-09-25-message-content-evidence.md`), so all the page adds is help writing it: where the
// mention being written is, which files it could mean, and how to spell the one chosen. Pure.

/** How many files the list offers at once. The rest are one more letter away. */
export const MENTION_OPTIONS = 8;

/**
 * The mention the caret is in: the `@` (`start`), the end of the word it runs to (`end`), and what was
 * typed between the `@` and the caret (`query`).
 */
export type Mention = { start: number; end: number; query: string };

// An `@` at the start or after whitespace, then either a quoted path still open, or a bare word. An
// `@` inside a word is an address, never a mention.
const WRITING = /(?:^|\s)@("[^"\n]*|[^\s"]*)$/;

/** The mention being written at `caret`, or null when the caret is not in one. */
export function mentionAt(text: string, caret: number): Mention | null {
  const found = WRITING.exec(text.slice(0, caret));
  if (!found) return null;

  const token = found[1];
  const start = caret - token.length - 1;
  const after = text.slice(caret);
  if (token.startsWith('"')) {
    // A quoted mention runs to its closing quote on this line, which is its own; open, it ends here.
    const close = after.split('\n', 1)[0].indexOf('"');
    return { start, end: close < 0 ? caret : caret + close + 1, query: token.slice(1) };
  }

  // A bare one runs on to the end of the word the caret is in, so accepting replaces all of it.
  return { start, end: caret + (/^[^\s"]*/.exec(after)?.[0].length ?? 0), query: token };
}

/**
 * The files `query` could mean, best first: a name that starts with it, a name that holds it, a path
 * that starts with it, a path that holds it, then a name holding its letters in order. Within each,
 * the shorter path first, since a shallower file is the likelier one. Case does not count, and a
 * backslash is the slash a Windows hand types.
 *
 * A path holding the letters in order ACROSS its folders is the last resort, offered only when nothing
 * above matched: on the window, `des` offered the one file it meant and then seven whose letters were
 * scattered through `.claude/rules/…`.
 */
export function rankMentions(files: readonly string[], query: string, limit = MENTION_OPTIONS): string[] {
  const wanted = query.toLowerCase().replaceAll('\\', '/');
  if (!wanted) {
    // Nothing typed yet: the tree's top first.
    return [...files]
      .sort((a, b) => depth(a) - depth(b) || a.length - b.length || compare(a, b))
      .slice(0, limit);
  }

  const ranked: Array<{ path: string; tier: number }> = [];
  for (const path of files) {
    const tier = tierOf(path.toLowerCase(), wanted);
    if (tier >= 0) ranked.push({ path, tier });
  }

  const better = ranked.some(({ tier }) => tier < SCATTERED);
  return ranked
    .filter(({ tier }) => !better || tier < SCATTERED)
    .sort((a, b) => a.tier - b.tier || a.path.length - b.path.length || compare(a.path, b.path))
    .slice(0, limit)
    .map(({ path }) => path);
}

/** The tier of a path whose letters match only scattered across its folders. */
const SCATTERED = 5;

function tierOf(path: string, wanted: string): number {
  const name = path.slice(path.lastIndexOf('/') + 1);
  if (name.startsWith(wanted)) return 0;
  if (name.includes(wanted)) return 1;
  if (path.startsWith(wanted)) return 2;
  if (path.includes(wanted)) return 3;
  if (inOrder(name, wanted)) return 4;
  return inOrder(path, wanted) ? SCATTERED : -1;
}

function inOrder(path: string, wanted: string): boolean {
  let at = 0;
  for (const letter of wanted) {
    at = path.indexOf(letter, at);
    if (at < 0) return false;
    at += letter.length;
  }
  return true;
}

const depth = (path: string) => path.split('/').length;
const compare = (a: string, b: string) => (a < b ? -1 : a > b ? 1 : 0);

/**
 * A path as both doors read it: bare, or in double quotes when it holds whitespace. Measured on both
 * (CONV4d): `@"my notes.md"` expands, and `@my\ notes.md` does not.
 */
export function spellMention(path: string): string {
  return /\s/.test(path) ? `@"${path}"` : `@${path}`;
}

/**
 * The text with the mention being written replaced by `path`, and where the caret goes: after it and
 * one space, so the next word starts clean. A space already there is used rather than doubled.
 */
export function withMention(text: string, mention: Mention, path: string): { text: string; caret: number } {
  const spelled = spellMention(path);
  const rest = text.slice(mention.end);
  const gap = /^\s/.test(rest) ? '' : ' ';
  return {
    text: `${text.slice(0, mention.start)}${spelled}${gap}${rest}`,
    caret: mention.start + spelled.length + 1,
  };
}

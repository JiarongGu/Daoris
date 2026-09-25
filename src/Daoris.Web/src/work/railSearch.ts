import type { Session } from '../api';

// Searching the rail (RAIL1). By name happens here, at once: the derived title — a conversation's
// first line, a quest's title — the repository and the id. By content is the host's, over the bridge,
// because what a session said is on this machine and nowhere else (D47 §4). Pure.

/**
 * The sessions whose title, repository or id holds what was typed, whatever the case, in the order
 * given. Nothing typed finds nothing — the rail's own list is what shows then.
 */
export function byName(sessions: readonly Session[], query: string, title: (session: Session) => string): Session[] {
  const wanted = query.trim().toLowerCase();
  if (!wanted) return [];
  return sessions.filter((session) =>
    [title(session), session.repository, session.id].some((field) => field.toLowerCase().includes(wanted)));
}

/**
 * A snippet as a line of words: the Markdown marks that only style it — bold's asterisks, code's
 * backticks — go. Underscores stay, because `__init__` is a name, not emphasis (seen on the window).
 */
export function readable(snippet: string): string {
  return snippet.replaceAll('**', '').replaceAll('`', '');
}

/** A snippet cut around every match of what was typed, so the match can be marked; case does not count. */
export function marked(text: string, query: string): Array<{ text: string; match: boolean }> {
  const wanted = query.trim().toLowerCase();
  if (!wanted) return [{ text, match: false }];

  const parts: Array<{ text: string; match: boolean }> = [];
  const lower = text.toLowerCase();
  let from = 0;
  for (let at = lower.indexOf(wanted); at >= 0; at = lower.indexOf(wanted, from)) {
    if (at > from) parts.push({ text: text.slice(from, at), match: false });
    parts.push({ text: text.slice(at, at + wanted.length), match: true });
    from = at + wanted.length;
  }
  if (from < text.length) parts.push({ text: text.slice(from), match: false });
  return parts.length > 0 ? parts : [{ text, match: false }];
}

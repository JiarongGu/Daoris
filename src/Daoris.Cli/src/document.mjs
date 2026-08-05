const HEADER_PREFIX = '<!-- daoris:';

/** A rule or knowledge document: the three fields its index row is built from. */
export const RULE_FIELDS = ['name', 'applies_when', 'enforces'];

/**
 * A skill: the two fields the agent harness itself requires. `description` is
 * the trigger the harness matches on, so it doubles as the index's "use when"
 * without a third field that could disagree with it.
 */
export const SKILL_FIELDS = ['name', 'description'];

/**
 * One line at the top of every vendored file. It exists because an agent that
 * opens a rule needing a tweak will otherwise just edit it in place — which is
 * exactly how the same doctrine ended up diverging across repos.
 */
export function makeHeader(pack, source, version) {
  return `${HEADER_PREFIX} ${pack}/${source} @ ${version} — canonical; edit via \`daoris upstream\` -->`;
}

/**
 * Where the frontmatter ends, or -1 when there is no complete block. Kept in one
 * place because withHeader and stripHeader must agree on it exactly, or a
 * materialized file stops round-tripping back through `upstream`.
 */
function frontmatterEnd(text) {
  if (!text.startsWith('---\n')) return -1;
  const fence = text.indexOf('\n---\n', 3);
  return fence === -1 ? -1 : fence + 5;
}

/**
 * Stamp a canonical body with its provenance line.
 *
 * The line goes UNDER the frontmatter, not above it. Frontmatter is only
 * frontmatter when it starts at the first byte: the agent harness parses a
 * skill's `name` and `description` to decide whether to surface it at all, so a
 * comment above the opening fence does not merely look untidy — it makes the
 * skill unreachable, silently and with no error anywhere.
 */
export function withHeader(header, body) {
  const end = frontmatterEnd(body);
  return end === -1 ? `${header}\n${body}` : `${body.slice(0, end)}${header}\n${body.slice(end)}`;
}

/**
 * What a canon file becomes on disk — the single answer to that question.
 *
 * Only markdown gets stamped: an HTML comment in a script is a syntax error, and the lock's hash
 * catches an edit to it either way (D6).
 *
 * It lives here because `sync` and `analyze` both have to answer it and must answer it identically.
 * `analyze` decides whether a file on disk is a collision by comparing against this; `sync` decides
 * what to write. If the two ever disagreed, `analyze` would promise a clean adoption and `sync` would
 * then report every file as a collision — the tool's own thesis, failing inside the tool.
 */
export function renderCanonFile(file, body, version) {
  return file.target.endsWith('.md')
    ? withHeader(makeHeader(file.pack, file.source, version), body)
    : body;
}

export function stripHeader(text) {
  if (text.startsWith(HEADER_PREFIX)) {
    const end = text.indexOf('\n');
    return end === -1 ? '' : text.slice(end + 1);
  }
  const start = frontmatterEnd(text);
  if (start === -1 || !text.startsWith(HEADER_PREFIX, start)) return text;
  const end = text.indexOf('\n', start);
  return end === -1 ? text.slice(0, start) : text.slice(0, start) + text.slice(end + 1);
}

/**
 * Frontmatter is flat `key: value` lines — exactly the index table's columns,
 * and nothing more. A block missing any required field is treated as absent, so
 * the index marks the file rather than half-listing it.
 */
export function parseFrontmatter(text, required = RULE_FIELDS) {
  if (!text.startsWith('---\n')) return { meta: null, body: text };
  const end = text.indexOf('\n---\n', 3);
  if (end === -1) return { meta: null, body: text };

  const meta = {};
  for (const line of text.slice(4, end + 1).split('\n')) {
    const at = line.indexOf(':');
    if (at === -1) continue;
    meta[line.slice(0, at).trim()] = line.slice(at + 1).trim();
  }
  if (required.some((field) => !meta[field])) return { meta: null, body: text };
  return { meta, body: text.slice(end + 5) };
}

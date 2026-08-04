const HEADER_PREFIX = '<!-- daoris:';
const REQUIRED_FIELDS = ['name', 'applies_when', 'enforces'];

/**
 * One line at the top of every vendored file. It exists because an agent that
 * opens a rule needing a tweak will otherwise just edit it in place — which is
 * exactly how the same doctrine ended up diverging across repos.
 */
export function makeHeader(pack, source, version) {
  return `${HEADER_PREFIX} ${pack}/${source} @ ${version} — canonical; edit via \`daoris upstream\` -->`;
}

export function stripHeader(text) {
  if (!text.startsWith(HEADER_PREFIX)) return text;
  const end = text.indexOf('\n');
  return end === -1 ? '' : text.slice(end + 1);
}

/**
 * Frontmatter is flat `key: value` lines — exactly the index table's columns,
 * and nothing more. A block missing any required field is treated as absent, so
 * the index marks the file rather than half-listing it.
 */
export function parseFrontmatter(text) {
  if (!text.startsWith('---\n')) return { meta: null, body: text };
  const end = text.indexOf('\n---\n', 3);
  if (end === -1) return { meta: null, body: text };

  const meta = {};
  for (const line of text.slice(4, end + 1).split('\n')) {
    const at = line.indexOf(':');
    if (at === -1) continue;
    meta[line.slice(0, at).trim()] = line.slice(at + 1).trim();
  }
  if (REQUIRED_FIELDS.some((field) => !meta[field])) return { meta: null, body: text };
  return { meta, body: text.slice(end + 5) };
}

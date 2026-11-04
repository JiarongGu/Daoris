import { readFileSync, readdirSync } from 'node:fs';
import { join, parse } from 'node:path';
import { BRIEF, NOT_CORPUS, PARTS, REPO, TASKS, taskPrompt } from './design.mjs';
import { formatRanking, hookSource, rankBm25, readFrontmatter, searchable } from './search.mjs';

/** Split a markdown table row on its unescaped pipes; `\|` inside a cell is a literal pipe. */
function cells(line) {
  return line.trim().replace(/^\|/, '').replace(/\|$/, '').split(/(?<!\\)\|/).map((c) => c.trim().replace(/\\\|/g, '|'));
}

/**
 * The router's rows by file name: `{ kind, about, standing? }`. Its three tables have three headers, and the
 * column that says what a document is for differs in each.
 */
export function parseRouterRows(text) {
  const rows = new Map();
  let header = null;
  for (const line of text.split(/\r?\n/)) {
    if (!line.startsWith('|')) { header = null; continue; }
    const row = cells(line);
    if (/^:?-+:?$/.test(row[0])) continue;
    if (row[0] === 'Document') { header = row; continue; }
    if (!header) continue;
    const file = row[0].replace(/`/g, '');
    const col = (name) => row[header.indexOf(name)];
    if (header.includes('For')) rows.set(file, { kind: col('Kind'), about: col('For'), standing: col('Where it stands') });
    else if (header.includes('Carried by')) rows.set(file, { kind: col('Kind'), about: col('Carried by') });
    else if (header.includes('What it holds')) rows.set(file, { kind: 'record', about: col('What it holds') });
  }
  return rows;
}

/** A router cell as a plain frontmatter value: no markup, no pipe, no colon-space a YAML reader would trip on. */
function plain(text) {
  return String(text)
    .replace(/`/g, '').replace(/\*+/g, '').replace(/\|/g, '/')
    .replace(/: /g, ' — ').replace(/\s+/g, ' ').trim();
}

/** Cut on a word boundary at `max` bytes. */
function cut(text, max) {
  if (Buffer.byteLength(text) <= max) return text;
  let out = '';
  for (const word of text.split(' ')) {
    if (Buffer.byteLength(`${out} ${word}…`) > max) break;
    out = out ? `${out} ${word}` : word;
  }
  return `${out.replace(/[,;.—\s]+$/, '')}…`;
}

/**
 * The frontmatter a set-up session would write for a document from its router row. A study's or evidence's row
 * names only the decisions it carries, so its title joins it, as a session that opened the document would add.
 */
export function frontmatterFor(name, row, title = '') {
  const about = plain(row.about);
  if (row.kind === 'contract' || row.kind === 'method') {
    return {
      name,
      applies_when: cut(`working on or deciding about ${about}`, 240),
      enforces: cut(`the ${row.kind} for ${about}; ${plain(row.standing ?? '')}`, 240),
    };
  }
  if (row.kind === 'record') {
    return { name, applies_when: cut(`looking up ${about}`, 240), enforces: cut(`an append-only record of ${about}`, 240) };
  }
  const what = row.kind === 'study' ? 'input to a decision, read for its reasoning' : 'what was measured, a record of that moment';
  const carried = about.split('. ')[0].replace(/\.$/, '');
  const titled = title ? `${carried} — ${plain(title)}` : carried;
  return { name, applies_when: cut(`revisiting the reasoning behind ${titled}`, 240), enforces: cut(`${row.kind} — ${what}`, 240) };
}

/** A corpus document as it is written into a fixture: frontmatter, then the body. */
export function renderDocument(doc) {
  return `---\nname: ${doc.name}\napplies_when: ${doc.applies_when}\nenforces: ${doc.enforces}\n---\n\n${doc.body}`;
}

/** The corpus, read from this repository: `[{ name, applies_when, enforces, title, body, source }]`, by name. */
export function loadCorpus(repo = REPO) {
  const rows = parseRouterRows(readFileSync(join(repo, 'docs', 'README.md'), 'utf8'));
  const corpus = [];
  for (const file of readdirSync(join(repo, 'docs')).filter((f) => f.endsWith('.md') && !NOT_CORPUS.has(f)).sort()) {
    const row = rows.get(file);
    if (!row) throw new Error(`docs/${file} has no row in docs/README.md, so it has no frontmatter to write`);
    const name = parse(file).name;
    const body = readFileSync(join(repo, 'docs', file), 'utf8').replace(/\r\n/g, '\n');
    const title = firstHeading(body, name);
    corpus.push({ ...frontmatterFor(name, row, title), title, body, source: 'docs' });
  }
  for (const file of readdirSync(join(repo, 'canon', 'core', 'knowledge')).filter((f) => f.endsWith('.md')).sort()) {
    const { fields, body } = readFrontmatter(readFileSync(join(repo, 'canon', 'core', 'knowledge', file), 'utf8').replace(/\r\n/g, '\n'));
    const name = parse(file).name;
    corpus.push({ name, applies_when: fields.applies_when, enforces: fields.enforces, title: firstHeading(body, name), body, source: 'canon' });
  }
  return corpus.sort((a, b) => a.name.localeCompare(b.name));
}

function firstHeading(body, fallback) {
  return /^#\s+(.+)$/m.exec(body)?.[1].trim() ?? fallback;
}

// ---------------------------------------------------------------------------------------------------------
// The fixtures

/** Where a design keeps a document, from the fixture's root. */
export function knowledgePath(design, name) {
  return design === 'C' ? `.claude/skills/${name}/SKILL.md` : `knowledge/${name}.md`;
}

/** A body's references to other corpus documents, pointed at where the fixture keeps them. */
function rewriteReferences(body, design, names) {
  return body.replace(/(?:docs|canon\/core\/knowledge|\.claude\/knowledge)\/([\w.-]+)\.md/g,
    (whole, name) => (names.has(name) ? knowledgePath(design, name) : whole));
}

/** Every file of a design's fixture, as path → text. Pure: nothing is written. */
export function fixtureFiles(design, corpus) {
  if (!(design in PARTS)) throw new Error(`no design ${design}`);
  const names = new Set(corpus.map((d) => d.name));
  const files = new Map();
  files.set('CLAUDE.md', '@AGENTS.md\n');
  const part = PARTS[design](corpus);
  files.set('AGENTS.md', part ? `${BRIEF}\n${part}` : BRIEF);
  for (const doc of corpus) {
    const body = rewriteReferences(doc.body, design, names);
    files.set(knowledgePath(design, doc.name), design === 'C'
      ? `---\nname: ${doc.name}\ndescription: ${doc.applies_when}\n---\n\n${body}`
      : renderDocument({ ...doc, body }));
  }
  if (design === 'B') {
    files.set('INDEX.md', [
      '# Index',
      '',
      'Generated from the files. Edit the documents, not this.',
      '',
      '## Knowledge',
      '',
      '| Document | Applies when | Enforces |',
      '|---|---|---|',
      ...corpus.map((d) => `| \`knowledge/${d.name}.md\` | ${d.applies_when} | ${d.enforces} |`),
      '',
    ].join('\n'));
  }
  if (design === 'G') {
    files.set('.claude/settings.json', `${JSON.stringify({
      hooks: { UserPromptSubmit: [{ hooks: [{ type: 'command', command: 'node .claude/hooks/rank.mjs' }] }] },
    }, null, 2)}\n`);
    files.set('.claude/hooks/rank.mjs', hookSource());
  }
  return files;
}

const bytes = (text) => Buffer.byteLength(text ?? '');

/**
 * What a design loads before the first prompt, in bytes: its instruction files (the harness reads `AGENTS.md`
 * itself and through `CLAUDE.md`'s import, once), C's skill listing (each skill's name and description, as the
 * harness lists them; an estimate, since the listing's own framing is not counted) and G's hook output for a
 * typical prompt. `fixed` and `perDocument` are what the projection multiplies.
 */
export function alwaysLoaded(design, files, corpus) {
  const instructionFiles = bytes(files.get('CLAUDE.md')) + bytes(files.get('AGENTS.md'));
  const rowOf = (d) => bytes(`| [${d.name}](knowledge/${d.name}.md) | ${d.applies_when} | ${d.enforces} |\n`);
  const listingOf = (d) => bytes(`- ${d.name}: ${d.applies_when}\n`);
  const skillListing = design === 'C' ? corpus.reduce((n, d) => n + listingOf(d), 0) : 0;
  const perPrompt = design === 'G' ? bytes(formatRanking(rankBm25(searchable(corpus), taskPrompt(TASKS[0].question)).slice(0, 5))) : 0;
  const total = instructionFiles + skillListing + perPrompt;
  const mean = (f) => (corpus.length ? corpus.reduce((n, d) => n + f(d), 0) / corpus.length : 0);
  let perDocument = 0;
  if (design === 'A') perDocument = mean(rowOf);
  if (design === 'C') perDocument = mean(listingOf);
  const fixed = total - Math.round(perDocument * corpus.length);
  return { instructionFiles, skillListing, perPrompt, total, fixed, perDocument };
}

/** The always-loaded bytes at `count` documents, computed from the measured fixed part and the mean per document. */
export function project(design, files, corpus, count) {
  const a = alwaysLoaded(design, files, corpus);
  return a.fixed + Math.round(a.perDocument * count);
}

/** B's on-demand index at `count` documents. */
export function projectIndex(files, corpus, count) {
  const index = files.get('INDEX.md') ?? '';
  const rows = corpus.reduce((n, d) => n + bytes(`| \`knowledge/${d.name}.md\` | ${d.applies_when} | ${d.enforces} |\n`), 0);
  return Math.round(bytes(index) - rows + (rows / corpus.length) * count);
}

// ---------------------------------------------------------------------------------------------------------
// The push design's ranker. Each function below is copied into the hook by its source text, so it may use
// nothing but the names the hook defines: STOPWORDS and these functions.

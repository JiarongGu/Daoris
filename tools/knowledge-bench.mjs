#!/usr/bin/env node
/**
 * The knowledge bench (KNOW3): how well each knowledge design lets a real agent session find the document
 * that governs a task.
 *
 * ## Why
 *
 * D128 proposes moving the knowledge list out of the always-loaded region into a generated index read on
 * demand, and KNOW2 (D129) reviews the designs from the makers' documentation. Neither is a measurement. The
 * first real set-up made the region 53 KB with 169 documents, and real sessions at the owner's install found
 * knowledge mostly by searching. This bench puts one corpus behind six designs, asks the same questions of a
 * real headless session in each, and reads the stream for what the session opened and when.
 *
 * ## What it runs
 *
 * - **The corpus** is this repository's own `docs/*.md` (the router and the three append-only records left
 *   out: the router is itself an index, and the records are read by lookup) and `canon/core/knowledge/*.md`.
 *   Each design document's frontmatter is written from its row in `docs/README.md`, the way a set-up session
 *   would write it; the canon's keep their own.
 * - **The designs** are one fixture each under `--root`: A, the table in the always-loaded `AGENTS.md`; B, a
 *   generated `INDEX.md` that `AGENTS.md` points to (D128); C, every document a skill; E, search-first with no
 *   index; G, a project prompt hook that prints a local BM25 ranking; 0, the files and no guidance. The brief is
 *   one text in all six, and only each design's own part differs.
 * - **The isolation.** The harness reads instruction files from every folder above the one it runs in, so the
 *   root is refused when one sits above it, and the fixtures belong under the OS temp folder: generated test
 *   material that must not inherit this repository's own doctrine. Each run loads project settings only, no MCP
 *   server, an exact tool set, hook events in the stream, and nothing persisted. A canary turn per design reads
 *   the stream's `init` event and its hook events, and the bench stops if any of it is not as built.
 *
 * Runs are serial, never parallel, and the bench stops at the first sign of a rate or spend limit, keeping what
 * it measured. The raw streams and the scores go to `local/scratch/knowledge-bench/` (gitignored).
 *
 *   node tools/knowledge-bench.mjs run --root <dir under the OS temp folder> [--pilot | --tasks T1,T5]
 *        [--designs 0,A,B,C,E,G] [--label <name>] [--max-turns 20]
 *   node tools/knowledge-bench.mjs report [--label <name>]
 *   node tools/knowledge-bench.mjs rank "<question>"     the push design's ranking, with no model
 *
 * The helpers are tested by `node --test tools/knowledge-bench.test.mjs`. The results are
 * `docs/2026-10-02-knowledge-bench-results.md`.
 */
import { spawn, spawnSync } from 'node:child_process';
import { existsSync, mkdirSync, readFileSync, readdirSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import { dirname, join, parse, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { isMain } from './fsx.mjs';

const REPO = join(dirname(fileURLToPath(import.meta.url)), '..');

/** The tools a run may call, and the ones it is refused (the brief's flags). */
export const ALLOWED_TOOLS = 'Read,Grep,Glob,Skill';
export const DISALLOWED_TOOLS = 'Edit,Write,Bash,WebFetch,WebSearch';

/** The five-hour window's share at which the bench stops, to leave the owner's own sessions room. */
export const WINDOW_CEILING = 0.85;

/** Router entries that are not knowledge: the router itself is an index, and the records are read by lookup. */
const NOT_CORPUS = new Set(['README.md', 'DECISIONS.md', 'FIX-LOG.md', 'task-archive.md']);

/** The one brief every design shares. It names the repository and says nothing about where knowledge lives. */
export const BRIEF = [
  '# Brief',
  '',
  'This is the documentation repository of Daoris, a desktop application that drives coding-agent sessions,',
  'one per repository, together with the command-line tool and the service around it.',
  '',
].join('\n');

/** Each design's own part of `AGENTS.md`, after the brief. */
const PARTS = {
  '0': () => '',
  A: (corpus) => [
    '## Read on demand',
    '',
    'Before a non-trivial task, scan the *Applies when* column and read every document that matches.',
    '',
    '| Knowledge | Applies when | Enforces |',
    '|---|---|---|',
    ...corpus.map((d) => `| [${d.name}](knowledge/${d.name}.md) | ${d.applies_when} | ${d.enforces} |`),
    '',
  ].join('\n'),
  B: () => [
    '## Read on demand',
    '',
    'The knowledge, each document with when it applies, is listed in `INDEX.md`, generated from the files: read it',
    'before a non-trivial task, and search it when it is long.',
    '',
  ].join('\n'),
  C: () => [
    '## Read on demand',
    '',
    'The knowledge is packaged as skills in `.claude/skills/`, one per document, each described by when it applies:',
    'before a non-trivial task, use every skill whose description matches.',
    '',
  ].join('\n'),
  E: () => [
    '## Read on demand',
    '',
    'The knowledge lives in `knowledge/`, one document per topic, each opening with frontmatter (`name`,',
    '`applies_when`, `enforces`). There is no index: before a non-trivial task, search `knowledge/` for the task\'s',
    'words and read every document that matches.',
    '',
  ].join('\n'),
  G: () => [
    '## Read on demand',
    '',
    'The knowledge lives in `knowledge/`. Each prompt arrives with the documents a local search ranked for it: read',
    'the ones that apply.',
    '',
  ].join('\n'),
};

export const DESIGNS = {
  '0': 'Control: the files, no guidance',
  A: 'Index table in the always-loaded AGENTS.md',
  B: 'Generated INDEX.md, read on demand (D128)',
  C: 'Knowledge as skills (progressive disclosure)',
  E: 'Search-first, no index',
  G: 'Push: a prompt hook prints a local BM25 top 5',
};

/**
 * The questions. Each is phrased in different words from its target, because synonymy is what keyword
 * discovery fails on; the comment after each says which of the target's own words it avoids.
 */
export const TASKS = [
  { id: 'T1', truth: ['2026-09-23-plugin-design'], // avoids: plugin, harness, declare, manifest
    question: 'An add-on that someone drops into the app wants to supply its own coding-assistant program for the work to run on. What is such an add-on allowed to state about that program, and does any of its code get loaded into the app itself?' },
  { id: 'T2', truth: ['2026-10-01-account-rotation-design'], // avoids: account, limit, cool-off, rotation
    question: 'My subscription ran out of its weekly quota halfway through a job. Which sign-in picks up the next job, and when does the exhausted one get used again?' },
  { id: 'T3', truth: ['2026-10-01-naming-design'], // avoids: name, label, glossary, budget
    question: 'How should the words on a settings screen be chosen for English and Chinese readers: is it just translation, and is there a cap on how long each one may be?' },
  { id: 'T4', truth: ['2026-09-30-machine-log-design'], // avoids: log, machine, event
    question: 'What does the desktop app record locally about how I use it, so the product can be improved, and what is it forbidden to keep in that record?' },
  { id: 'T5', truth: ['leak-repair'], // avoids: credential, leak, history, scrub
    question: 'Last week I pushed a commit that contains an access token for our cloud account. Is deleting the line in a new commit enough? What does a proper cleanup involve?' },
  { id: 'T6', truth: ['claims-need-checks'], // avoids: claim, check, guarantee, verify
    question: 'Our readme now promises the program never opens a network connection. Before we ship, what should stand behind a sentence like that?' },
  { id: 'T7', truth: ['2026-09-30-terminal-design'], // avoids: terminal, console, shell, input line
    question: 'Can I type my own commands in the panel at the bottom of the window where an agent\'s output scrolls, the way I would in a normal command window? How does that work?' },
  { id: 'T8', truth: ['2026-09-27-ask-and-wait-design'], // avoids: quest, repository, resume, block
    question: 'An agent working on the web front end finds it needs facts about an API that a different team\'s codebase owns. Instead of guessing, how is it supposed to pause, get the answer, and carry on?' },
  { id: 'T9', truth: ['2026-10-01-tools-design'], // avoids: tool, resource, managed
    question: 'Which copy of git and of Node does the app use for the programs it runs alongside the agents, and can I make it use one I installed myself?' },
  { id: 'T10', truth: ['2026-09-27-in-app-browser-design', '2026-09-28-chromium-host-design'], // avoids: browser MCP, CDP, Chromium
    question: 'When an agent needs to click through a web page and sign in to a site, whose web browser is it driving, and how does it get hold of it?' },
  { id: 'T11', truth: ['model-decoupling'], // avoids: model, provider, tier, AI
    question: 'We want to add "find similar notes" using an embeddings vendor. What rules govern which vendor we pick, and what should happen when none is configured?' },
  { id: 'T12', truth: ['2026-09-22-first-deployment-case-study'], // avoids: deploy, install, case study, checkout
    question: 'The first time the product ran as a real copy on someone\'s computer instead of from the developers\' working folder, what turned out to be broken?' },
];

/** One task of each kind: a design contract, a canon knowledge document, and an evidence record. */
export const PILOT_TASKS = ['T1', 'T5', 'T12'];

/** What every run is asked, around its question: discovery only. */
export function taskPrompt(question) {
  return [
    'This is a discovery task. Find and read the documents in this repository that govern the question below, then',
    'say in two lines what they require, and name the documents you used. Change nothing.',
    '',
    `Question: ${question}`,
  ].join('\n');
}

/** The canary's question: the session reports what it was handed, and the stream says the same. */
export const CANARY_PROMPT = [
  'Without calling any tool, list what your context holds, one line per item: every instruction file (CLAUDE.md,',
  'AGENTS.md, rules, memory or any other) with its first heading; every tool; every MCP server; every skill; and',
  'any text a hook added to this prompt. Write NONE for an empty list.',
].join('\n');

// ---------------------------------------------------------------------------------------------------------
// The corpus

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

/** A document's frontmatter fields and the body after them. */
export function readFrontmatter(text) {
  const m = /^---\r?\n([\s\S]*?)\r?\n---\r?\n?/.exec(text);
  if (!m) return { fields: {}, body: text };
  const fields = {};
  for (const line of m[1].split(/\r?\n/)) {
    const kv = /^([A-Za-z_][\w-]*):\s?(.*)$/.exec(line);
    if (kv) fields[kv[1]] = kv[2].trim();
  }
  return { fields, body: text.slice(m[0].length).replace(/^\r?\n/, '') };
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

const STOPWORDS = new Set(('a about after again all also an and any are as at be been before being both but by can could did do '
  + 'does doing done each for from get gets got had has have having he her here how i if in into is it its itself just me '
  + 'more most my no nor not now of off on once one only or other our out over own same she should so some such than that '
  + 'the their them then there these they this those through to too under until up very was way we were what when where '
  + 'which while who whom why will with would you your question document documents find read say two lines name used '
  + 'change nothing discovery task govern repository below require').split(' '));

/** Fold a word to a rough stem, so a plural and its singular meet. */
export function stem(word) {
  if (word.length > 5 && word.endsWith('ing')) return word.slice(0, -3);
  if (word.length > 4 && word.endsWith('ies')) return `${word.slice(0, -3)}y`;
  if (word.length > 4 && word.endsWith('es') && /(sses|shes|ches|xes)$/.test(word)) return word.slice(0, -2);
  if (word.length > 4 && word.endsWith('ed')) return word.slice(0, -2);
  if (word.length > 3 && word.endsWith('s') && !word.endsWith('ss')) return word.slice(0, -1);
  return word;
}

/** Words of a text, lower-cased, stop words out, stemmed. */
export function tokenize(text) {
  const out = [];
  for (const word of String(text).toLowerCase().match(/[\p{L}\p{N}]+/gu) ?? []) {
    if (word.length < 2 || STOPWORDS.has(word)) continue;
    out.push(stem(word));
  }
  return out;
}

/**
 * BM25 over each document's frontmatter (name, applies_when, enforces, title: counted three times, a field
 * boost) and its body. `docs` is `[{ path, fields, body }]`; returns them scored, best first, zero scores out.
 */
export function rankBm25(docs, query, k1 = 1.2, b = 0.75) {
  if (!docs.length) return [];
  const bags = docs.map((d) => {
    const head = [d.fields.name ?? '', String(d.fields.name ?? '').replace(/-/g, ' '), d.fields.applies_when ?? '',
      d.fields.enforces ?? '', d.fields.title ?? ''].join(' ');
    const words = [...tokenize(head), ...tokenize(head), ...tokenize(head), ...tokenize(d.body)];
    const tf = new Map();
    for (const w of words) tf.set(w, (tf.get(w) ?? 0) + 1);
    return { doc: d, tf, length: words.length };
  });
  const avg = bags.reduce((n, x) => n + x.length, 0) / bags.length;
  const df = new Map();
  for (const bag of bags) for (const w of bag.tf.keys()) df.set(w, (df.get(w) ?? 0) + 1);
  const terms = [...new Set(tokenize(query))];
  const scored = bags.map((bag) => {
    let score = 0;
    for (const t of terms) {
      const f = bag.tf.get(t);
      if (!f) continue;
      const n = df.get(t);
      const idf = Math.log(1 + (bags.length - n + 0.5) / (n + 0.5));
      score += idf * ((f * (k1 + 1)) / (f + k1 * (1 - b + (b * bag.length) / avg)));
    }
    return { path: bag.doc.path, title: bag.doc.fields.title ?? bag.doc.path, score };
  });
  return scored.filter((s) => s.score > 0).sort((x, y) => y.score - x.score || x.path.localeCompare(y.path));
}

/** What the hook prints: a line saying what it is, then one numbered line per document, title and path. */
export function formatRanking(ranked) {
  if (!ranked.length) return '';
  return `Knowledge a local search ranked for this prompt (read the ones that apply):\n${
    ranked.map((r, i) => `${i + 1}. ${r.title} (${r.path})`).join('\n')}\n`;
}

/** The corpus as the ranker reads it in a fixture: frontmatter fields, title, body, by path. */
function searchable(corpus) {
  return corpus.map((d) => ({ path: `knowledge/${d.name}.md`, fields: { ...d }, body: d.body }));
}

/** The hook script G's settings run: the ranker above, by source, over the fixture's `knowledge/`. */
export function hookSource() {
  return [
    '#!/usr/bin/env node',
    '// Generated by tools/knowledge-bench.mjs (KNOW3, design G): a local BM25 ranking of knowledge/ for the prompt',
    '// on stdin, printed as the top five titles and paths. No service and no model.',
    "import { readFileSync, readdirSync } from 'node:fs';",
    "import { dirname, join } from 'node:path';",
    "import { fileURLToPath } from 'node:url';",
    `const STOPWORDS = new Set(${JSON.stringify([...STOPWORDS])});`,
    stem.toString(),
    tokenize.toString(),
    readFrontmatter.toString(),
    rankBm25.toString(),
    formatRanking.toString(),
    "let input = '';",
    "process.stdin.setEncoding('utf8');",
    "process.stdin.on('data', (chunk) => { input += chunk; });",
    "process.stdin.on('end', () => {",
    "  let prompt = input;",
    "  try { prompt = JSON.parse(input).prompt ?? ''; } catch { /* a bare prompt */ }",
    "  const dir = join(dirname(fileURLToPath(import.meta.url)), '..', '..', 'knowledge');",
    "  const docs = readdirSync(dir).filter((f) => f.endsWith('.md')).sort().map((f) => {",
    "    const { fields, body } = readFrontmatter(readFileSync(join(dir, f), 'utf8'));",
    "    const title = (/^#\\s+(.+)$/m.exec(body) ?? [])[1] ?? f;",
    "    return { path: `knowledge/${f}`, fields: { ...fields, title: title.trim() }, body };",
    '  });',
    '  process.stdout.write(formatRanking(rankBm25(docs, prompt).slice(0, 5)));',
    '});',
    '',
  ].join('\n');
}

// ---------------------------------------------------------------------------------------------------------
// Isolation

const INSTRUCTION_FILES = ['CLAUDE.md', 'CLAUDE.local.md', 'AGENTS.md', '.claude/CLAUDE.md'];

/** Refuse a root with an instruction file in it or in any folder above it: the harness would load that file. */
export function assertIsolatedRoot(root, { exists = existsSync } = {}) {
  if (!root) throw new Error('--root is required: a folder under the OS temp folder, with no CLAUDE.md or AGENTS.md above it');
  let dir = resolve(root);
  for (;;) {
    for (const f of INSTRUCTION_FILES) {
      const p = join(dir, f);
      if (exists(p)) throw new Error(`refusing --root ${root}: ${p.replace(/\\/g, '/')} would be loaded by every run`);
    }
    const up = dirname(dir);
    if (up === dir) return;
    dir = up;
  }
}

/** The arguments of one run. The brief's flags, plus `--tools` (the exact set), hook events and no persistence. */
export function claudeArgs(prompt, { maxTurns = 20 } = {}) {
  return [
    '-p', prompt,
    '--output-format', 'stream-json', '--verbose',
    '--setting-sources', 'project',
    '--strict-mcp-config', '--mcp-config', '{"mcpServers":{}}',
    '--tools', ALLOWED_TOOLS,
    '--allowedTools', ALLOWED_TOOLS,
    '--disallowedTools', DISALLOWED_TOOLS,
    '--max-turns', String(maxTurns),
    '--include-hook-events',
    '--no-session-persistence',
  ];
}

/** The stream's lines, parsed; a line that is not JSON is kept as `{ type: 'unparsed', line }`. */
export function parseStream(text) {
  return String(text).split(/\r?\n/).filter((l) => l.trim()).map((line) => {
    try { return JSON.parse(line); } catch { return { type: 'unparsed', line }; }
  });
}

const isHook = (e) => e.type === 'system' && String(e.subtype ?? '').startsWith('hook_');
const hookText = (e) => [e.output, e.stdout, e.hook_output, e.additionalContext].filter((x) => typeof x === 'string').join('\n');

/**
 * Why a canary's stream does not prove isolation, as sentences; empty when it does. The tools are exactly the
 * allowed four, no MCP server, only the harness's built-in plugins, no skill beyond the harness's own (and C's
 * documents), and no hook but G's own ranker, which G must show firing.
 */
export function isolationProblems(events, { design, baselineSkills, corpusNames = [], fixtureDir } = {}) {
  const problems = [];
  const init = events.find((e) => e.type === 'system' && e.subtype === 'init');
  if (!init) return ['no init event in the stream'];
  const tools = [...(init.tools ?? [])].sort();
  if (tools.join(',') !== ALLOWED_TOOLS.split(',').sort().join(',')) problems.push(`tools are ${tools.join(', ')}, not exactly ${ALLOWED_TOOLS}`);
  if ((init.mcp_servers ?? []).length) problems.push(`MCP servers present: ${init.mcp_servers.map((s) => s.name ?? s).join(', ')}`);
  for (const p of init.plugins ?? []) {
    if (p.path !== 'builtin') problems.push(`plugin ${p.name} is not the harness's own (${p.source ?? p.path})`);
  }
  const allowed = new Set([...(baselineSkills ?? []), ...(design === 'C' ? corpusNames : [])]);
  const extra = (init.skills ?? []).filter((s) => !allowed.has(s));
  if (extra.length) problems.push(`skill(s) beyond the harness's own: ${extra.join(', ')}`);
  if (design === 'C') {
    const missing = corpusNames.filter((n) => !(init.skills ?? []).includes(n));
    if (missing.length) problems.push(`${missing.length} corpus skill(s) not listed, e.g. ${missing.slice(0, 3).join(', ')}`);
  }
  if (fixtureDir && resolve(init.cwd ?? '') !== resolve(fixtureDir)) problems.push(`ran in ${init.cwd}, not the fixture`);
  const hooks = events.filter(isHook);
  if (design === 'G') {
    const ours = hooks.filter((e) => /local search ranked/.test(hookText(e)));
    if (!ours.length) problems.push('the ranker hook did not fire, or printed nothing');
    const others = hooks.filter((e) => !/UserPromptSubmit/.test(`${e.hook_event ?? ''} ${e.hook_name ?? ''}`));
    if (others.length) problems.push(`hook events other than the ranker's: ${others.map((e) => e.hook_name ?? e.subtype).join(', ')}`);
  } else if (hooks.length) {
    problems.push(`hook events in a design with no hook: ${hooks.map((e) => `${e.subtype} ${e.hook_name ?? ''}`).join(', ')}`);
  }
  return problems;
}

/** A sentence when the stream shows a rate or spend limit, or a window past the ceiling; else null. */
export function limitReached(events, ceiling = WINDOW_CEILING) {
  for (const e of events) {
    if (e.type === 'rate_limit_event') {
      const info = e.rate_limit_info ?? {};
      if (info.status && !String(info.status).startsWith('allowed')) return `rate limit ${info.status} (${info.rateLimitType ?? 'window'})`;
      for (const [window, w] of Object.entries(info.unifiedWindows ?? {})) {
        if ((w?.utilization ?? 0) >= ceiling) return `${window} window at ${Math.round(w.utilization * 100)}%, past the bench's ${Math.round(ceiling * 100)}% ceiling`;
      }
    }
    if (e.type === 'result' && e.is_error && /limit|quota|credit|spend|budget|429/i.test(`${e.result ?? ''} ${e.api_error_status ?? ''}`)) {
      return `the run ended on a limit: ${String(e.result ?? e.api_error_status).slice(0, 160)}`;
    }
  }
  return null;
}

// ---------------------------------------------------------------------------------------------------------
// Scoring

/** The corpus document a path names, in either layout; null for anything else. */
export function docNameOf(path) {
  const p = String(path ?? '').replace(/\\/g, '/');
  return /(?:^|\/)knowledge\/([^/]+)\.md$/i.exec(p)?.[1]
    ?? /(?:^|\/)\.claude\/skills\/([^/]+)\/SKILL\.md$/i.exec(p)?.[1]
    ?? null;
}

const INDEX_FILES = /(?:^|\/)(AGENTS|CLAUDE|INDEX)\.md$/i;

/** The top-level tool calls, in order; a subagent's (with a parent) are not the session's own. */
export function toolCalls(events) {
  const calls = [];
  for (const e of events) {
    if (e.type !== 'assistant' || e.parent_tool_use_id) continue;
    for (const block of e.message?.content ?? []) {
      if (block.type === 'tool_use') calls.push({ n: calls.length + 1, name: block.name, input: block.input ?? {} });
    }
  }
  return calls;
}

/**
 * The corpus document a call aims at: what it reads, or the one file or skill folder a search is confined to. A
 * session that searches inside the right document has already chosen it, which is the discovery being measured.
 */
export function targetOf(call) {
  if (call.name === 'Skill') return call.input.skill ?? call.input.name ?? null;
  if (call.name === 'Glob') {
    // A pattern that spells one document's file or skill folder out, with no wildcard in the name.
    const p = String(call.input.pattern ?? '').replace(/\\/g, '/');
    return /(?:^|\/)knowledge\/([^/*?{}[\]]+)\.md$/.exec(p)?.[1] ?? /(?:^|\/)\.claude\/skills\/([^/*?{}[\]]+)(?:\/|$)/.exec(p)?.[1] ?? null;
  }
  const path = call.name === 'Read' ? (call.input.file_path ?? call.input.path) : call.name === 'Grep' ? call.input.path : null;
  if (!path) return null;
  const p = String(path).replace(/\\/g, '/');
  return docNameOf(p) ?? /(?:^|\/)\.claude\/skills\/([^/]+)\/?$/.exec(p)?.[1] ?? null;
}

/** What a call read: a corpus document's name, an index file, or nothing. */
function readOf(call) {
  if (call.name === 'Read') {
    const path = call.input.file_path ?? call.input.path ?? '';
    return { doc: docNameOf(path), index: INDEX_FILES.test(String(path).replace(/\\/g, '/')) };
  }
  if (call.name === 'Skill') return { doc: call.input.skill ?? call.input.name ?? null, index: false };
  return { doc: null, index: false };
}

const SEARCHES = new Set(['Grep', 'Glob']);

/** One run's measures, from its stream. */
export function scoreRun(events, truth) {
  const calls = toolCalls(events);
  const init = events.find((e) => e.type === 'system' && e.subtype === 'init') ?? {};
  const result = events.find((e) => e.type === 'result') ?? {};
  const usage = result.usage ?? {};
  const truths = new Set(truth);
  let hitAt = null;
  const wrong = [];
  const truthRead = [];
  let indexReads = 0;
  for (const call of calls) {
    const { doc, index } = readOf(call);
    if (index) indexReads += 1;
    if (!doc) continue;
    if (truths.has(doc)) {
      if (hitAt === null) hitAt = call.n;
      if (!truthRead.includes(doc)) truthRead.push(doc);
    } else if (!wrong.includes(doc)) {
      wrong.push(doc);
    }
  }
  const before = hitAt === null ? calls : calls.slice(0, hitAt - 1);
  const wrongBeforeHit = [...new Set(before.map((c) => readOf(c).doc).filter((d) => d && !truths.has(d)))];
  const targetedAt = calls.find((c) => truths.has(targetOf(c)))?.n ?? null;
  return {
    hit: hitAt !== null,
    hitAt,
    targetedAt,
    callsBeforeHit: hitAt === null ? null : hitAt - 1,
    searchesBeforeHit: hitAt === null ? null : before.filter((c) => SEARCHES.has(c.name)).length,
    truthRead,
    wrongRead: wrong,
    wrongBeforeHit,
    indexReads,
    calls: calls.length,
    searches: calls.filter((c) => SEARCHES.has(c.name)).length,
    searchTerms: calls.filter((c) => SEARCHES.has(c.name) || c.name === 'Skill')
      .map((c) => `${c.name}: ${c.input.pattern ?? c.input.skill ?? ''}`),
    turns: result.num_turns ?? null,
    inputTokens: usage.input_tokens ?? 0,
    outputTokens: usage.output_tokens ?? 0,
    cacheRead: usage.cache_read_input_tokens ?? 0,
    cacheCreation: usage.cache_creation_input_tokens ?? 0,
    costUsd: result.total_cost_usd ?? null,
    durationMs: result.duration_ms ?? null,
    subtype: result.subtype ?? null,
    isError: result.is_error ?? null,
    model: init.model ?? null,
    harness: init.claude_code_version ?? null,
    answer: typeof result.result === 'string' ? result.result : null,
  };
}

const median = (xs) => {
  const s = xs.filter((x) => x !== null && x !== undefined).sort((a, b) => a - b);
  if (!s.length) return null;
  const mid = Math.floor(s.length / 2);
  return s.length % 2 ? s[mid] : (s[mid - 1] + s[mid]) / 2;
};

/** Per design: runs, hits, hit rate, median calls to the hit, tokens per hit, wrong reads, wall time, cost. */
export function aggregate(runs) {
  const designs = [...new Set(runs.map((r) => r.design))];
  return designs.map((design) => {
    const mine = runs.filter((r) => r.design === design);
    const hits = mine.filter((r) => r.hit);
    const tokens = mine.reduce((n, r) => n + r.inputTokens + r.outputTokens + r.cacheRead + r.cacheCreation, 0);
    return {
      design,
      runs: mine.length,
      hits: hits.length,
      hitRate: mine.length ? hits.length / mine.length : 0,
      medianCallsToHit: median(hits.map((r) => r.hitAt)),
      medianCallsToTarget: median(mine.map((r) => r.targetedAt)),
      medianSearchesBeforeHit: median(hits.map((r) => r.searchesBeforeHit)),
      meanWrongReads: mine.length ? mine.reduce((n, r) => n + r.wrongRead.length, 0) / mine.length : 0,
      tokens,
      tokensPerHit: hits.length ? Math.round(tokens / hits.length) : null,
      outputTokens: mine.reduce((n, r) => n + r.outputTokens, 0),
      medianTurns: median(mine.map((r) => r.turns)),
      medianWallMs: median(mine.map((r) => r.wallMs)),
      costUsd: mine.reduce((n, r) => n + (r.costUsd ?? 0), 0),
      costPerHit: hits.length ? mine.reduce((n, r) => n + (r.costUsd ?? 0), 0) / hits.length : null,
    };
  });
}

// ---------------------------------------------------------------------------------------------------------
// The runner

function writeAtomic(path, text) {
  mkdirSync(dirname(path), { recursive: true });
  writeFileSync(`${path}.tmp`, text);
  renameSync(`${path}.tmp`, path);
}

const MARKER = '.knowledge-bench';

/** Write a design's fixture as a fresh git repository with one commit, so the harness sees a clean tree. */
export function buildFixture(dir, design, corpus) {
  if (existsSync(dir)) {
    if (!existsSync(join(dir, MARKER))) throw new Error(`${dir} exists and is not a fixture of this bench; refusing to replace it`);
    rmSync(dir, { recursive: true, force: true });
  }
  mkdirSync(dir, { recursive: true });
  for (const [path, text] of fixtureFiles(design, corpus)) writeAtomic(join(dir, path), text);
  writeAtomic(join(dir, MARKER), `knowledge bench fixture, design ${design}\n`);
  const git = (...args) => {
    const r = spawnSync('git', ['-c', 'core.autocrlf=false', '-c', 'user.name=knowledge-bench', '-c', 'user.email=bench@example.invalid', ...args],
      { cwd: dir, encoding: 'utf8' });
    if (r.status !== 0) throw new Error(`git ${args[0]} in ${dir}: ${r.stderr}`);
  };
  git('init', '-q', '-b', 'main');
  git('add', '-A');
  git('commit', '-q', '-m', 'The documents');
}

/** The child's environment: this session's own variables out, so the run is not a child of it nor shares its effort. */
function childEnv() {
  return Object.fromEntries(Object.entries(process.env).filter(([k]) => !/^CLAUDE/i.test(k)));
}

/** Run one session and keep its whole stream. Resolves `{ stdout, stderr, code, wallMs }`. */
function runClaude(claude, cwd, args, timeoutMs) {
  return new Promise((done) => {
    const t0 = Date.now();
    const child = spawn(claude, args, { cwd, env: childEnv(), stdio: ['ignore', 'pipe', 'pipe'] });
    let stdout = '';
    let stderr = '';
    child.stdout.setEncoding('utf8').on('data', (c) => { stdout += c; });
    child.stderr.setEncoding('utf8').on('data', (c) => { stderr += c; });
    const timer = setTimeout(() => child.kill(), timeoutMs);
    child.on('close', (code) => { clearTimeout(timer); done({ stdout, stderr, code, wallMs: Date.now() - t0 }); });
    child.on('error', (err) => { clearTimeout(timer); done({ stdout, stderr: `${stderr}${err.message}`, code: -1, wallMs: Date.now() - t0 }); });
  });
}

function argOf(argv, flag, fallback) {
  const i = argv.indexOf(flag);
  return i >= 0 && argv[i + 1] !== undefined ? argv[i + 1] : fallback;
}

const pct = (x) => `${Math.round(x * 100)}%`;
const kb = (n) => `${(n / 1000).toFixed(1)} KB`;

/** The per-design table and the per-task matrix, as markdown, from the run records and the fixtures' bytes. */
export function renderReport(records, corpus) {
  const order = Object.keys(DESIGNS).filter((d) => records.some((r) => r.design === d));
  const rows = aggregate(records).sort((a, b) => order.indexOf(a.design) - order.indexOf(b.design));
  const out = ['| Design | Hits | Hit rate | Median calls to the target | Median calls to the read | Median searches before it | Wrong reads per run | Tokens per hit | List price per hit | Median turns | Median wall | Always loaded | At 169 documents |',
    '|---|---|---|---|---|---|---|---|---|---|---|---|---|'];
  for (const r of rows) {
    const files = fixtureFiles(r.design, corpus);
    const loaded = alwaysLoaded(r.design, files, corpus);
    const at169 = project(r.design, files, corpus, 169);
    out.push(`| ${r.design}: ${DESIGNS[r.design]} | ${r.hits}/${r.runs} | ${pct(r.hitRate)} | ${r.medianCallsToTarget ?? '—'} | ${r.medianCallsToHit ?? '—'} | ${r.medianSearchesBeforeHit ?? '—'} | ${r.meanWrongReads.toFixed(1)} | ${r.tokensPerHit?.toLocaleString('en-US') ?? '—'} | ${r.costPerHit === null ? '—' : `$${r.costPerHit.toFixed(3)}`} | ${r.medianTurns ?? '—'} | ${((r.medianWallMs ?? 0) / 1000).toFixed(0)} s | ${kb(loaded.total)} | ${kb(at169)} |`);
  }
  const tasks = TASKS.filter((t) => records.some((r) => r.task === t.id));
  out.push('', `| Task | ${order.join(' | ')} |`, `|---|${order.map(() => '---').join('|')}|`);
  for (const t of tasks) {
    const cellsOf = order.map((d) => {
      const r = records.find((x) => x.design === d && x.task === t.id);
      if (!r) return '';
      return r.hit ? `read at call ${r.hitAt}` : `miss (${r.calls} calls)`;
    });
    out.push(`| ${t.id} | ${cellsOf.join(' | ')} |`);
  }
  return `${out.join('\n')}\n`;
}

/** The run records of a label, each scored again from its kept stream, so a scoring change reaches old runs. */
function readRecords(outDir) {
  if (!existsSync(outDir)) return [];
  return readdirSync(outDir).filter((f) => /^[0A-Z]-T\d+\.json$/.test(f)).map((f) => {
    const record = JSON.parse(readFileSync(join(outDir, f), 'utf8'));
    const stream = join(outDir, f.replace(/\.json$/, '.jsonl'));
    return existsSync(stream) ? { ...record, ...scoreRun(parseStream(readFileSync(stream, 'utf8')), record.truth) } : record;
  });
}

async function main(argv) {
  const command = argv[0];
  const label = argOf(argv, '--label', 'run');
  const outDir = join(REPO, 'local', 'scratch', 'knowledge-bench', label);
  if (command === 'rank') {
    const corpus = loadCorpus();
    process.stdout.write(formatRanking(rankBm25(searchable(corpus), taskPrompt(argv[1] ?? '')).slice(0, 5)) || 'nothing ranked\n');
    return 0;
  }
  if (command === 'report') {
    const corpus = loadCorpus();
    process.stdout.write(renderReport(readRecords(outDir), corpus));
    return 0;
  }
  if (command !== 'run') {
    process.stderr.write('usage: knowledge-bench.mjs run --root <dir> [--pilot | --tasks T1,T5] [--designs 0,A,B,C,E,G] [--label <name>] [--max-turns 20]\n'
      + '       knowledge-bench.mjs report [--label <name>]\n       knowledge-bench.mjs rank "<question>"\n');
    return 2;
  }
  const root = argOf(argv, '--root');
  try { assertIsolatedRoot(root); } catch (err) { process.stderr.write(`${err.message}\n`); return 2; }
  const designs = argOf(argv, '--designs', Object.keys(DESIGNS).join(',')).split(',');
  const taskIds = argv.includes('--pilot') ? PILOT_TASKS : argOf(argv, '--tasks', TASKS.map((t) => t.id).join(',')).split(',');
  const tasks = TASKS.filter((t) => taskIds.includes(t.id));
  const maxTurns = Number(argOf(argv, '--max-turns', '20'));
  const claude = argOf(argv, '--claude', 'claude');
  const corpus = loadCorpus();
  const corpusNames = corpus.map((d) => d.name);
  mkdirSync(outDir, { recursive: true });
  writeAtomic(join(outDir, 'corpus.json'), `${JSON.stringify(corpus.map(({ name, applies_when, enforces, title, source }) => ({ name, applies_when, enforces, title, source })), null, 2)}\n`);
  console.log(`corpus: ${corpus.length} documents (${corpus.filter((d) => d.source === 'canon').length} canon); designs ${designs.join(' ')}; tasks ${tasks.map((t) => t.id).join(' ')}`);

  // Fixtures and canaries. The control's skills are the harness's own, which every other design is held to.
  let baselineSkills = null;
  const baselineFile = join(outDir, 'baseline-skills.json');
  if (existsSync(baselineFile)) baselineSkills = JSON.parse(readFileSync(baselineFile, 'utf8'));
  for (const design of ['0', ...designs.filter((d) => d !== '0')]) {
    const dir = join(resolve(root), design);
    const canaryFile = join(outDir, `canary-${design}.json`);
    if (existsSync(canaryFile) && existsSync(join(dir, MARKER))) continue;
    buildFixture(dir, design, corpus);
    const run = await runClaude(claude, dir, claudeArgs(CANARY_PROMPT, { maxTurns: 2 }), 300_000);
    writeAtomic(join(outDir, `canary-${design}.jsonl`), run.stdout);
    const events = parseStream(run.stdout);
    const limit = limitReached(events);
    if (limit) { console.log(`STOPPED at the canary of ${design}: ${limit}`); return 3; }
    const init = events.find((e) => e.type === 'system' && e.subtype === 'init');
    if (design === '0') {
      baselineSkills = (init?.skills ?? []).filter((s) => !s.includes(':'));
      writeAtomic(baselineFile, `${JSON.stringify(baselineSkills)}\n`);
    }
    const problems = isolationProblems(events, { design, baselineSkills, corpusNames, fixtureDir: dir });
    const memory = init?.memory_paths?.auto;
    if (memory && existsSync(join(memory, 'MEMORY.md'))) problems.push(`an auto-memory index exists for the fixture: ${memory}`);
    const answer = events.find((e) => e.type === 'result')?.result ?? '';
    writeAtomic(canaryFile, `${JSON.stringify({ design, code: run.code, stderr: run.stderr.slice(0, 2000), problems, init, answer,
      hooks: events.filter(isHook) }, null, 2)}\n`);
    console.log(`canary ${design}: ${problems.length ? `NOT ISOLATED\n  ${problems.join('\n  ')}` : 'isolated'} (model ${init?.model}, harness ${init?.claude_code_version})`);
    if (problems.length || !init) return 1;
  }

  // The runs: task by task, the design order turned each time so no design always runs first or last.
  let i = 0;
  for (const task of tasks) {
    const order = designs.map((_, k) => designs[(k + i) % designs.length]);
    i += 1;
    for (const design of order) {
      const record = join(outDir, `${design}-${task.id}.json`);
      if (existsSync(record)) continue;
      const dir = join(resolve(root), design);
      const run = await runClaude(claude, dir, claudeArgs(taskPrompt(task.question), { maxTurns }), 900_000);
      writeAtomic(join(outDir, `${design}-${task.id}.jsonl`), run.stdout);
      const events = parseStream(run.stdout);
      const limit = limitReached(events);
      const score = scoreRun(events, task.truth);
      const problems = isolationProblems(events, { design, baselineSkills, corpusNames, fixtureDir: dir });
      const pushRank = rankBm25(searchable(corpus), taskPrompt(task.question)).findIndex((r) => task.truth.includes(docNameOf(r.path))) + 1 || null;
      writeAtomic(record, `${JSON.stringify({ design, task: task.id, truth: task.truth, ...score, wallMs: run.wallMs, code: run.code,
        stderr: run.stderr.slice(0, 2000), isolation: problems, pushRank }, null, 2)}\n`);
      console.log(`${design} ${task.id}: ${score.hit ? `hit at call ${score.hitAt}` : 'miss'}; ${score.calls} calls, ${score.turns} turns, ${(run.wallMs / 1000).toFixed(0)} s${problems.length ? `; ISOLATION: ${problems.join('; ')}` : ''}`);
      if (limit) { console.log(`STOPPED after ${design} ${task.id}: ${limit}`); return 3; }
      if (problems.length) return 1;
    }
  }
  const report = renderReport(readRecords(outDir), corpus);
  writeAtomic(join(outDir, 'report.md'), report);
  process.stdout.write(report);
  return 0;
}

if (isMain(import.meta.url)) {
  main(process.argv.slice(2)).then((code) => { process.exitCode = code; }, (err) => {
    process.stderr.write(`${err.stack ?? err}\n`);
    process.exitCode = 2;
  });
}

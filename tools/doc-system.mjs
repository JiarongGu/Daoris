#!/usr/bin/env node
// DOCSYS1: structural documentation drift. Semantic claims still require a source review.
// Mirrors doc-duplicates.mjs: pure audit, an import-safe runner, no dependency and no file writes.
import { readFileSync } from 'node:fs';
import { dirname, join, posix } from 'node:path';
import { fileURLToPath } from 'node:url';
import { fenced } from './doc-duplicates.mjs';
import { isMain, repositoryFiles } from './fsx.mjs';

function role(file) {
  if (file.startsWith('docs/index/')) return 'generated';
  if (file.startsWith('canon/')) return 'canon';
  if (/(^|\/)\.claude\//.test(file) || /^examples\/(engine|game)\/(AGENTS|CLAUDE)\.md$/.test(file)) return 'doctrine';
  if (file.startsWith('docs/archive/')) return 'historical';
  if (file.startsWith('docs/decisions/') || /^docs\/(task-archive|FIX-LOG|DECISIONS)\.md$/.test(file)
    || file === 'CHANGELOG.md') return 'record';
  if (/\/golden\//.test(file) || file.startsWith('tools/question-bench-fixtures/')) return 'fixture';
  if (/^docs\/[^/]+\.md$/.test(file) && file !== 'docs/README.md') return 'document';
  return 'guide';
}

/** First cells of router tables, including the inline-code paths the link gate cannot check. */
function routedPaths(file, text) {
  const paths = [];
  const lines = text.split(/\r?\n/);
  const inFence = fenced(lines);
  for (const [i, line] of lines.entries()) {
    if (inFence[i]) continue;
    const match = /^\|\s*(?:`([^`]+)`|\[[^\]]+\]\(([^)]+)\))\s*\|/.exec(line);
    // A Markdown section link still routes to its file; inline-code cells are literal paths.
    const value = match?.[1] ?? match?.[2]?.split(/[?#]/, 1)[0];
    if (!value || !(/\.(md|json)$/.test(value) || value.endsWith('/'))) continue;
    if (/^[a-z]+:/i.test(value) || value.startsWith('/')) continue;
    paths.push({ path: posix.normalize(posix.join(posix.dirname(file), value)).replace(/\/$/, ''), folder: value.endsWith('/') });
  }
  return paths;
}

/** Normalize declared repository paths, accepting Windows separators but never outside paths. */
function repositoryPath(value) {
  if (typeof value !== 'string' || !value.trim() || value.includes('\0')) return null;
  const path = value.replace(/\\/g, '/');
  if (path.startsWith('/') || /^[a-z]+:/i.test(path) || path.split('/').includes('..')) return null;
  return posix.normalize(path).replace(/\/$/, '');
}

/** Audit a repository file inventory. The caller supplies reads so fixtures need no Git checkout. */
export function auditDocuments(files, read) {
  const present = new Set(files);
  const markdown = files.filter((file) => file.endsWith('.md'));
  const inventory = {};
  const findings = [];
  for (const file of markdown) {
    const kind = role(file);
    inventory[kind] = (inventory[kind] ?? 0) + 1;
  }
  const routers = ['docs/README.md', 'docs/archive/README.md'];
  const rows = new Map();
  for (const router of routers) {
    if (!present.has(router)) {
      findings.push(`${router}: documentation router missing`);
      rows.set(router, []);
      continue;
    }
    const paths = routedPaths(router, read(router));
    rows.set(router, paths);
    for (const { path, folder } of paths) {
      const exists = folder ? files.some((file) => file.startsWith(`${path}/`)) : present.has(path);
      if (!exists) findings.push(`${router}: stale row names ${path}`);
    }
  }
  for (const file of markdown) {
    const kind = role(file);
    if (['generated', 'canon', 'doctrine', 'fixture'].includes(kind) || routers.includes(file)) continue;
    // Decisions have their generated digest; adoption mechanics are inventoried by their directory.
    if (file.startsWith('docs/decisions/')) continue;
    const router = kind === 'historical' ? 'docs/archive/README.md' : 'docs/README.md';
    const covered = rows.get(router).some(({ path, folder }) => file === path || (folder && file.startsWith(`${path}/`)));
    if (!covered) findings.push(`${file}: no row in ${router}`);
  }

  const tracked = JSON.parse(read('daoris.gates.json')).docs?.tracked ?? [];
  const seen = new Set();
  for (const row of tracked) {
    const document = repositoryPath(row?.document);
    if (document === null) {
      findings.push('daoris.gates.json: freshness needs a repository-relative document path');
      continue;
    }
    if (seen.has(document)) findings.push(`${document}: freshness declared twice`);
    seen.add(document);
    if (!present.has(document)) findings.push(`${document}: freshness names a missing document`);
    if (!Array.isArray(row.describes) || row.describes.length === 0) {
      findings.push(`${document}: freshness needs nonempty described source paths`);
      continue;
    }
    for (const value of row.describes) {
      const source = repositoryPath(value);
      if (source === null) {
        findings.push(`${document}: described source needs a repository-relative path`);
        continue;
      }
      // Checking only Git dates would silently accept a typo or a path deleted since its last commit.
      const exists = present.has(source) || source === '.'
        || files.some((file) => file.startsWith(`${source}/`));
      if (!exists) findings.push(`${document}: described source missing from inventory: ${source}`);
    }
  }
  for (const file of markdown.filter((file) => /^src\/[^/]+\/README\.md$/.test(file))) {
    if (!seen.has(file)) {
      findings.push(`${file}: component guide has no code-freshness declaration`);
    }
  }
  return { total: markdown.length, inventory, findings };
}

if (isMain(import.meta.url)) {
  try {
    const args = process.argv.slice(2);
    if (args.length > 1 || (args.length === 1 && args[0] !== '--json')) {
      throw new Error('usage: node tools/doc-system.mjs [--json]');
    }
    const root = dirname(dirname(fileURLToPath(import.meta.url)));
    // Include new documentation before staging; exclude ignored package copies and private scratch.
    const files = repositoryFiles(root);
    const result = auditDocuments(files, (file) => readFileSync(join(root, file), 'utf8'));
    if (args[0] === '--json') console.log(JSON.stringify(result, null, 2));
    else {
      console.log(`doc-system: ${result.total} Markdown files — ${Object.entries(result.inventory)
        .map(([kind, count]) => `${kind} ${count}`).join(', ')}`);
      if (result.findings.length) console.error(result.findings.join('\n'));
      else console.log('doc-system: router coverage and component freshness declarations pass; semantic claims need source review');
    }
    process.exitCode = result.findings.length ? 1 : 0;
  } catch (error) {
    console.error(`doc-system: ${error.message}`);
    process.exitCode = 2;
  }
}

import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { CODE } from './outline.mjs';
import { GENERATED, LARGE, kb } from './common.mjs';

/** `daoris.json` at `root`, or an empty manifest where there is none. */
export function readManifest(root) {
  const manifest = join(root, 'daoris.json');
  return existsSync(manifest) ? JSON.parse(readFileSync(manifest, 'utf8')) ?? {} : {};
}

/** The path a role is declared at in the manifest's `documents`, `/`-separated, or null: a path, or `{ path }`. */
export function declaredPath(manifest, role) {
  const entry = manifest.documents?.[role];
  const path = typeof entry === 'string' ? entry : entry?.path;
  return typeof path === 'string' ? path.replace(/\\/g, '/').replace(/\/+$/, '') : null;
}

/**
 * The records read by lookup, never whole, which get no outline: their headings are their entries, and a record is
 * searched for the identifier a row names (development-documents). The decisions have the digest instead.
 */
function lookupRecords(root) {
  const manifest = readManifest(root);
  const paths = ['decisions', 'archive', 'fixes', 'changelog', 'backlog'].map((role) => declaredPath(manifest, role)).filter(Boolean);
  return (path) => paths.some((record) => path === record || path.startsWith(`${record}/`)) || path.startsWith('docs/archive/');
}

export function largeFiles(ctx) {
  const record = lookupRecords(ctx.root);
  return ctx.files
    .filter((path) => (CODE.test(path) || path.endsWith('.md')) && !record(path) && !/(^|\/)[^/]*fixtures\//.test(path))
    .map((path) => ({ path, bytes: Buffer.byteLength(ctx.read(path), 'utf8') }))
    .filter((file) => file.bytes > LARGE)
    .map((file) => ({ ...file, lines: ctx.read(file.path).split('\n').length - (ctx.read(file.path).endsWith('\n') ? 1 : 0) }));
}

export function outlinePage(file, entries) {
  const kind = file.path.endsWith('.md') ? 'heading' : 'declaration';
  const out = [
    `# Outline of \`${file.path}\``,
    '',
    `${GENERATED} ${kb(file.bytes)} KB, ${file.lines} lines. Each row is a ${kind} and its lines: read it with offset = the first line and limit = the range's length.`,
    '',
  ];
  if (entries.length === 0) out.push(`No ${kind} was found.`);
  for (const entry of entries) out.push(`${'  '.repeat(entry.depth)}- ${entry.start}-${entry.end} ${entry.name}`);
  return `${out.join('\n')}\n`;
}

// ---------------------------------------------------------------------------------------------------
// Bridge routes

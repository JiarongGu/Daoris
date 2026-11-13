/**
 * The private files a gate tree is handed from the main checkout, and the copy of them.
 */
import { createHash } from 'node:crypto';
import { existsSync, mkdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { writeAtomic } from './fsx.mjs';

/** The local sibling builds' props: their feed paths are absolute, the main checkout's, so a tree restores the same versions. */
const SIBLING_PROPS = ['local/lyntai-local.props', 'local/shenora-local.props'];

/**
 * The private files a gate reads under `local/`, as the declaration names them: today the sensitive scan's
 * patterns file (`sensitive.patternsFile`, the devkit's default where undeclared), and the sibling builds' props. A path not
 * under `local/` is not one of these (a tracked file is in the tree already), and none may climb out of it.
 */
export function privateGateFiles(checkout) {
  let declared = 'local/sensitive-patterns.txt';
  try {
    const parsed = JSON.parse(readFileSync(join(checkout, 'daoris.gates.json'), 'utf8'));
    if (typeof parsed?.sensitive?.patternsFile === 'string') declared = parsed.sensitive.patternsFile;
  } catch {
    // No readable declaration: the plan's own reading refuses it, with its reason.
  }
  const path = declared.replace(/\\/g, '/');
  return path.startsWith('local/') && !path.split('/').includes('..') ? [path, ...SIBLING_PROPS] : [...SIBLING_PROPS];
}

/** A digest of what the tree at `tree` holds of each private file a gate reads, `missing` where absent: a replay's key holds it. */
export function privateDigest(tree) {
  const parts = privateGateFiles(tree).map((file) => {
    const path = join(tree, file);
    return `${file}\0${existsSync(path) ? createHash('sha1').update(readFileSync(path)).digest('hex') : 'missing'}`;
  });
  return createHash('sha1').update(parts.join('\n')).digest('hex');
}

/** Copies each of `files` from `checkout` into `tree`, refreshed on every run; returns the lines to say. */
export function copyPrivateFiles(checkout, tree, files) {
  return files.map((file) => {
    const from = join(checkout, file);
    if (!existsSync(from)) return `${file} is missing in the main checkout, so it is not copied into the gate tree: the gate that reads it answers for itself`;
    mkdirSync(dirname(join(tree, file)), { recursive: true });
    writeAtomic(join(tree, file), readFileSync(from));
    return `${file}: copied into the gate tree`;
  });
}

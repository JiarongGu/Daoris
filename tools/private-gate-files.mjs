/**
 * The private files a gate tree is handed from the main checkout, and the copy of them.
 */
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
export function privateGateFiles(home) {
  let declared = 'local/sensitive-patterns.txt';
  try {
    const parsed = JSON.parse(readFileSync(join(home, 'daoris.gates.json'), 'utf8'));
    if (typeof parsed?.sensitive?.patternsFile === 'string') declared = parsed.sensitive.patternsFile;
  } catch {
    // No readable declaration: the plan's own reading refuses it, with its reason.
  }
  const path = declared.replace(/\\/g, '/');
  return path.startsWith('local/') && !path.split('/').includes('..') ? [path, ...SIBLING_PROPS] : [...SIBLING_PROPS];
}

/** Copies each of `files` from `home` into `tree`, refreshed on every run; returns the lines to say. */
export function copyPrivateFiles(home, tree, files) {
  return files.map((file) => {
    const from = join(home, file);
    if (!existsSync(from)) return `${file} is missing in the main checkout, so it is not copied into the gate tree: the gate that reads it answers for itself`;
    mkdirSync(dirname(join(tree, file)), { recursive: true });
    writeAtomic(join(tree, file), readFileSync(from));
    return `${file}: copied into the gate tree`;
  });
}

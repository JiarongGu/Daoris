/**
 * The private files a gate tree is handed from the main checkout, and the copy of them.
 */
import { createHash } from 'node:crypto';
import { existsSync, mkdirSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { writeAtomic } from './fsx.mjs';
import { budgetSourceOf, keptOf } from './merge/budget-overlay.ts';

/**
 * The private files a tree is handed, read as the driver's `TreeLocalInputs.Declared` reads them: the local inputs in order, then
 * the sensitive patterns file (the devkit's default where none is named), each once. The home's record of the repository names them
 * (`source`), the declaration's members where it names none. Only paths under `local/`, none climbing out.
 */
export function privateGateFiles(checkout, source = budgetSourceOf(checkout)) {
  let declared = [];
  let patterns = 'local/sensitive-patterns.txt';
  try {
    const parsed = JSON.parse(readFileSync(join(checkout, 'daoris.gates.json'), 'utf8'));
    if (Array.isArray(parsed?.localInputs)) declared = parsed.localInputs.filter((each) => typeof each === 'string' && each.length > 0);
    if (typeof parsed?.sensitive?.patternsFile === 'string' && parsed.sensitive.patternsFile.length > 0) patterns = parsed.sensitive.patternsFile;
  } catch {
    // No readable declaration: the plan's own reading refuses it, with its reason.
  }
  const kept = keptOf(source);
  if (kept.localInputs) declared = kept.localInputs.filter((each) => typeof each === 'string' && each.length > 0);
  if (typeof kept.patternsFile === 'string' && kept.patternsFile.length > 0) patterns = kept.patternsFile;
  const paths = [...declared, patterns].map((each) => each.replace(/\\/g, '/')).filter((path) => path.startsWith('local/') && !path.split('/').includes('..'));
  return [...new Set(paths)];
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

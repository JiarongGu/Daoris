// The words of each node kind, beside the names check: every built-in kind has a title in the editor and on a run (`workflow.edit.kind.<kind>`,
// `workflow.step.<kind>`), and each outcome it routes on is worded (`workflow.outcome.<kind>.<outcome>`), in every language. The kinds are
// `NODE_KINDS` in the generated `src/bridge/generated/routes.ts`, read off the driver's kit; the generator writes that list one kind a line
// as JSON, so plain JavaScript reads it with no TypeScript.
import { existsSync, readFileSync } from 'node:fs';
import { ROUTES } from './person-words.mjs';

/** The kinds a generated routes file's `NODE_KINDS` lists, or null where it lists none. */
export function nodeKinds(text) {
  const start = text.indexOf('export const NODE_KINDS');
  const open = start < 0 ? -1 : text.indexOf('= [', start);
  const close = open < 0 ? -1 : text.indexOf('\n];', open);
  return close < 0 ? null : JSON.parse(text.slice(open + 2, close + 2));
}

/** The kinds the generated file at `path` lists, or null where it is not written or lists none. */
export function readNodeKinds(path = ROUTES) {
  return existsSync(path) ? nodeKinds(readFileSync(path, 'utf8')) : null;
}

/** The catalogue keys a kind needs words for: its two titles, and each outcome it routes on. */
export function kindKeys(kinds) {
  return kinds.flatMap((row) => [`workflow.edit.kind.${row.kind}`, `workflow.step.${row.kind}`, ...row.outcomes.map((outcome) => `workflow.outcome.${row.kind}.${outcome}`)]);
}

/** A finding for each key a language's catalogue leaves out or leaves blank, in the names check's shape: a fact, so `--strict` fails it. */
export function unwordedKinds(kinds, catalogues) {
  return Object.entries(catalogues).flatMap(([language, catalogue]) => kindKeys(kinds)
    .filter((key) => typeof catalogue[key] !== 'string' || catalogue[key].trim() === '')
    .map((key) => ({ key, kind: 'sentence', rule: 'kind', language, message: 'a node kind whose title or outcome has no words', value: catalogue[key] ?? null })));
}

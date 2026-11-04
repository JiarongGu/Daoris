// The words of each person's press, beside the names check: every route a person presses names its act in each language, by the
// catalogue key the shell's pipe words it with. The keys are `PERSON_WORDS` in the generated `src/bridge/generated/routes.ts`,
// read off the routes' marks; the generator writes that list as a JSON array, so plain JavaScript reads it with no TypeScript.
import { existsSync, readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

export const ROUTES = join(dirname(fileURLToPath(import.meta.url)), '..', 'src', 'bridge', 'generated', 'routes.ts');

/** The keys a generated routes file's `PERSON_WORDS` lists, or null where it lists none. */
export function personWords(text) {
  const start = text.indexOf('export const PERSON_WORDS');
  const open = start < 0 ? -1 : text.indexOf('[', start);
  const close = open < 0 ? -1 : text.indexOf('\n];', open);
  return close < 0 ? null : JSON.parse(text.slice(open, close + 2));
}

/** The keys the generated file at `path` lists, or null where it is not written or lists none. */
export function readPersonWords(path = ROUTES) {
  return existsSync(path) ? personWords(readFileSync(path, 'utf8')) : null;
}

/** A finding for each key a language's catalogue leaves out or leaves blank, in the names check's shape: a fact, so `--strict` fails it. */
export function unworded(keys, catalogues) {
  return Object.entries(catalogues).flatMap(([language, catalogue]) => keys
    .filter((key) => typeof catalogue[key] !== 'string' || catalogue[key].trim() === '')
    .map((key) => ({ key, kind: 'sentence', rule: 'person', language, message: 'a person\'s press whose act has no words', value: catalogue[key] ?? null })));
}

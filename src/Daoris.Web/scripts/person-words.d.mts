// The types of `person-words.mjs`, for the test that imports its helpers. The script is plain JavaScript so the build runs it on
// any Node the package supports.

export const ROUTES: string;

export function personWords(text: string): string[] | null;

export function readPersonWords(path?: string): string[] | null;

export type PersonFinding = {
  key: string;
  kind: 'sentence';
  rule: 'person';
  language: string;
  message: string;
  value: string | null;
};

export function unworded(keys: readonly string[], catalogues: Record<string, Record<string, string>>): PersonFinding[];

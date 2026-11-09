// The types of `kind-words.mjs`, for the test that imports its helpers. The script is plain JavaScript so the build runs it on any Node
// the package supports.

export type KindRow = { kind: string; outcomes: readonly string[]; arrives?: { outcomes: readonly string[] }; waits?: readonly string[] };

export function nodeKinds(text: string): KindRow[] | null;

export function readNodeKinds(path?: string): KindRow[] | null;

export function kindKeys(kinds: readonly KindRow[]): string[];

export type KindFinding = {
  key: string;
  kind: 'sentence';
  rule: 'kind';
  language: string;
  message: string;
  value: string | null;
};

export function unwordedKinds(kinds: readonly KindRow[], catalogues: Record<string, Record<string, string>>): KindFinding[];

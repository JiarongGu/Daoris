export type BudgetSource = { home: string | null; repository: string };
export function privateGateFiles(checkout: string, source?: BudgetSource): string[];
export function privateDigest(tree: string): string;
export function copyPrivateFiles(checkout: string, tree: string, files: string[]): string[];

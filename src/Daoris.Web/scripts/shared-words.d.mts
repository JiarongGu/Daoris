// The types of `shared-words.mjs`, for the test that imports its helpers. The script is plain JavaScript so the build runs it on any Node
// the package supports.

import type { Finding, Glossary } from './names-check.mjs';

export type SeededRole = { id: string; name?: { en?: string; zh?: string } };
export type Registers = { roles: SeededRole[] | null; levels: string[] | null };
export type Thing = { thing: string; words: string[]; zh: string[] };

export const ROLES: string;
export const LINE: string;
export function lineLevels(text: string): string[] | null;
export function readRegisters(roles?: string, line?: string): Registers;
export function things(glossary: Glossary, registers: Registers): Thing[];
export function shared(list: Thing[]): Finding[];

import { store, stored } from '../lib/stored';

/**
 * Knowledge's two modes (UX6i, D150 §2.2), in the order its list's head draws them: Search, which finds what the family
 * wrote by words or meaning, and Convergence, where two repositories reached one conclusion. Two views of one index, so
 * one place whose list switches between them.
 */
export const KNOWLEDGE_MODES = ['search', 'convergence'] as const;

export type KnowledgeMode = (typeof KNOWLEDGE_MODES)[number];

/** Where the mode chosen is kept: one for the place, as a repository's page's tab is kept for Repositories. */
const KEY = 'daoris.list.knowledge.mode';

export const isKnowledgeMode = (value: string | null | undefined): value is KnowledgeMode =>
  KNOWLEDGE_MODES.some((mode) => mode === value);

/** The mode Knowledge last showed, or Search, the first, where nothing readable is kept. */
export function readKnowledgeMode(): KnowledgeMode {
  const kept = stored(KEY);
  return isKnowledgeMode(kept) ? kept : 'search';
}

/** Remember the mode chosen; Search, the first, is what nothing kept means. */
export function storeKnowledgeMode(mode: KnowledgeMode): void {
  store(KEY, mode === 'search' ? null : mode);
}

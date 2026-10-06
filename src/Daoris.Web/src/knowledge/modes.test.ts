import { afterEach, describe, expect, it } from 'vitest';
import { KNOWLEDGE_MODES, readKnowledgeMode, storeKnowledgeMode } from './modes';

// UX6i (D150 §2.2): Knowledge is one place whose list switches between Search and Convergence, and the choice is
// remembered for the place, as a repository's page's tab is for Repositories.

const KEY = 'daoris.list.knowledge.mode';

describe("Knowledge's mode", () => {
  afterEach(() => window.localStorage.clear());

  it('is Search, then Convergence, in the order the choice draws them', () => {
    expect(KNOWLEDGE_MODES).toEqual(['search', 'convergence']);
  });

  it('opens on Search where nothing is kept, and on what was kept', () => {
    expect(readKnowledgeMode()).toBe('search');
    window.localStorage.setItem(KEY, 'convergence');
    expect(readKnowledgeMode()).toBe('convergence');
  });

  it('reads anything unreadable as Search, the first', () => {
    window.localStorage.setItem(KEY, 'map');
    expect(readKnowledgeMode()).toBe('search');
  });

  it('keeps Convergence, and keeps nothing for Search, which is what nothing kept means', () => {
    storeKnowledgeMode('convergence');
    expect(window.localStorage.getItem(KEY)).toBe('convergence');
    storeKnowledgeMode('search');
    expect(window.localStorage.getItem(KEY)).toBeNull();
  });
});

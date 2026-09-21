import { describe, expect, it } from 'vitest';
import i18n from '../i18n';
import type { Quest, Session } from '../api';
import { ownTree, sessionOrigin, sessionTitle, treeName } from './identity';

const session = (over: Partial<Session> = {}): Session => ({
  id: 's1',
  repository: 'engine',
  adapter: 'claude-code',
  state: 'working',
  created: '2026-09-21T09:00:00Z',
  updated: '2026-09-21T09:04:00Z',
  ...over,
});

const quest = (over: Partial<Quest> = {}): Quest => ({
  id: '7a82cc',
  from: 'platform',
  to: 'engine',
  title: 'Expose a streaming budget on the chunk API',
  body: 'World streaming needs to cap hydration work per frame.',
  status: 'Taken',
  created: '2026-09-14T09:00:00Z',
  updated: '2026-09-21T09:00:00Z',
  ...over,
} as Quest);

describe('a session title', () => {
  it('is the quest it serves, when the quest is in hand', () => {
    expect(sessionTitle(session({ quest: '7a82cc' }), quest()))
      .toBe('Expose a streaming budget on the chunk API');
  });

  it('falls back to the quest reference rather than inventing a name for it', () => {
    expect(sessionTitle(session({ quest: '7a82cc' }), null)).toBe('#7a82cc');
  });

  it('names a conversation by its kind, because a chat serves no quest by design', () => {
    expect(sessionTitle(session({ kind: 'chat' }))).toBe('conversation');
  });

  it('names a driven session with no quest by its kind too — nothing is fabricated', () => {
    expect(sessionTitle(session({ kind: 'driven' }))).toBe('session');
  });

  it('serves the derived name in the active language', async () => {
    await i18n.changeLanguage('zh');
    expect(sessionTitle(session({ kind: 'chat' }))).toBe('对话');
    await i18n.changeLanguage('en');
  });

  it('prefers the quest over the kind — a chat that took one is still about that quest', () => {
    expect(sessionTitle(session({ kind: 'chat', quest: '7a82cc' }), quest()))
      .toBe('Expose a streaming budget on the chunk API');
  });
});

/**
 * Where a session runs (D55). The feed keys a mirrored record by `origin/id` (D47 §6), and the
 * origin is the key's own identity — whose key, on which machine. So the answer is already in the
 * record, and this is where reading it lives rather than in whichever component asked first.
 */
describe('where a session runs', () => {
  it('names the machine a mirrored record came from', () => {
    expect(sessionOrigin(session({ id: 'person@machine-a/s1a2b3c4' }))).toBe('person@machine-a');
  });

  it('answers null for a record this deployment made itself — silence means here', () => {
    expect(sessionOrigin(session({ id: 's1a2b3c4' }))).toBeNull();
  });

  it('splits on the FIRST separator, so a machine name is never cut short by a second one', () => {
    expect(sessionOrigin(session({ id: 'person@host/lab/s1a2b3c4' }))).toBe('person@host');
  });

  it('answers null rather than an empty name when the prefix is missing', () => {
    expect(sessionOrigin(session({ id: '/s1a2b3c4' }))).toBeNull();
  });
});

/**
 * A session tree's short name. Daoris owns where trees live (D51 §2), so the last segment is the
 * branch the tree was cut for — the part a person recognises, and the only part that fits a rail.
 */
describe('a tree name', () => {
  it('is the last segment of the path Daoris created', () => {
    expect(treeName('/home/p/.daoris/trees/default/engine/streaming-budget')).toBe('streaming-budget');
  });

  it('reads a Windows path too — the same tree, spelled the other way', () => {
    expect(treeName('C:\\Users\\p\\.daoris\\trees\\default\\engine\\streaming-budget'))
      .toBe('streaming-budget');
  });

  it('ignores a trailing separator rather than answering with nothing', () => {
    expect(treeName('/home/p/.daoris/trees/default/engine/streaming-budget/')).toBe('streaming-budget');
  });

  /** No tree is the registered root, and a browser is told no path at all (D51 §9). Both are null. */
  it('answers null for no tree at all', () => {
    expect(treeName(null)).toBeNull();
    expect(treeName(undefined)).toBeNull();
    expect(treeName('   ')).toBeNull();
  });
});

/**
 * Found by the first real-window pass (SURF4d): the rail said `in engine` under the heading
 * `engine`, because an ordinary conversation DOES carry a tree — the registered root's own path.
 * The ledger resolves an unstated tree to the root before recording it, so "has a tree" was never
 * the question; "which tree" is.
 */
describe('a tree the session opened for itself', () => {
  const root = 'D:/checkouts/engine';

  it('is null when the session is working in the registered checkout', () => {
    expect(ownTree(session({ tree: root }), root)).toBeNull();
  });

  it('ignores separators and case, because one path can be spelled several ways', () => {
    expect(ownTree(session({ tree: 'D:\\checkouts\\Engine\\' }), root)).toBeNull();
  });

  it('names it when the session opened one for itself (D51)', () => {
    expect(ownTree(session({ tree: '/home/p/.daoris/trees/default/engine/streaming' }), root))
      .toBe('streaming');
  });

  it('claims nothing where the root is not known — a browser is told neither path', () => {
    expect(ownTree(session({ tree: '/home/p/.daoris/trees/default/engine/streaming' }), null))
      .toBeNull();
  });

  it('claims nothing when the record carries no tree at all', () => {
    expect(ownTree(session(), root)).toBeNull();
  });
});

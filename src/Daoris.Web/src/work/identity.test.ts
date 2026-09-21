import { describe, expect, it } from 'vitest';
import i18n from '../i18n';
import type { Quest, Session } from '../api';
import { sessionTitle } from './identity';

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

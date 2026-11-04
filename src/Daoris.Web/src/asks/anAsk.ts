import type { Ask } from '../data/api';

/** An ask as the service answers one, every value it always carries set to its plainest, and `over` laid on it. */
export const anAsk = (over: Partial<Ask> = {}): Ask => ({
  id: 'a1', workspace: 'default', sentence: 'An ask', state: 'Open', tier: 'declarations',
  asked: '2026-10-01T00:00:00Z', updated: '2026-10-01T00:00:00Z',
  links: [], attachments: [], proposal: [], quests: [],
  ...over,
});

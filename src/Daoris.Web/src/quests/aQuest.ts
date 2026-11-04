import type { Quest } from '../data/api';

/** A quest as the service answers one, every value it always carries set to its plainest, and `over` laid on it. */
export const aQuest = (over: Partial<Quest> = {}): Quest => ({
  id: 'q1', from: 'game', to: 'engine', title: 'A quest', body: '', status: 'Open',
  filed: '2026-10-01T00:00:00Z', updated: '2026-10-01T00:00:00Z', workspace: '',
  links: [], attachments: [], then: [], conflicts: [],
  ...over,
});

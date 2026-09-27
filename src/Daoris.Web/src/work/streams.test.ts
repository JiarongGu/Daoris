import { describe, expect, it } from 'vitest';
import { panelTabs, type StreamRow } from './streams';

const row = (over: Partial<StreamRow> = {}): StreamRow => ({
  key: 's1/task/bs00', kind: 'task', name: 'dev server', live: true, state: null, ...over,
});

describe("the output panel's tabs (CONSOLE2c)", () => {
  it('are none for a session that runs nothing beside itself: one tab is no tabs', () => {
    expect(panelTabs('s1', [])).toBeUndefined();
  });

  it("put the session's own console first, then each stream in the order it opened", () => {
    const tabs = panelTabs('s1', [
      row(),
      row({ key: 's1/subagent/a2fe', kind: 'subagent', name: 'Read README first line' }),
    ])!;

    expect(tabs.map((tab) => [tab.key, tab.kind, tab.label])).toEqual([
      ['s1', 'session', 'session'],
      ['s1/task/bs00', 'task', 'dev server'],
      ['s1/subagent/a2fe', 'subagent', 'Read README first line'],
    ]);
  });

  it('name how each stands in words, so the mark is never hue alone', () => {
    const [, running, done, failed, orphaned, unknown] = panelTabs('s1', [
      row(),
      row({ live: false, state: 'completed' }),
      row({ kind: 'subagent', live: false, state: 'failed' }),
      row({ live: false, state: 'session-ended' }),
      row({ live: false, state: 'disconnected' }),
    ])!;

    expect([running!.tone, running!.status]).toEqual(['live', 'background · running']);
    expect([done!.tone, done!.status]).toEqual(['ended', 'background · completed']);
    expect([failed!.tone, failed!.status]).toEqual(['failed', 'subagent · failed']);
    expect([orphaned!.tone, orphaned!.status]).toEqual(['ended', 'background · ended with its session']);
    // A word this build has no key for is the wire's own word, never a blank.
    expect(unknown!.status).toBe('background · disconnected');
  });
});

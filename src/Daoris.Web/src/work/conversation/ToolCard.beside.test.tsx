import { describe, expect, it } from 'vitest';
import { render } from '@testing-library/react';
import '../../locales/i18n';
import type { Block } from './conversation';
import { ToolCard } from './ToolCard';

// A call that started a sub-agent or a background task names it on its card and says how it ended.

const at = '2026-11-19T10:00:00Z';
const call = (beside: Block['beside']): Block => ({ key: '1', kind: 'tool', at, title: 'Agent', toolKind: 'think', status: 'completed', beside });
const said = (beside: Block['beside']) => render(<ToolCard call={call(beside)} tree={null} />).container.querySelector('[data-beside]')?.textContent;

describe('a spawning call\'s card', () => {
  it('names its subagent and how it ended, the name as its agent gave it', () => {
    expect(said({ kind: 'child', id: 'a1', name: 'Read README first line', end: 'ok', by: 'reported' }))
      .toBe('subagent · Read README first line · finished');
  });

  it('names a background task running, and one only its session\'s close ended as ended with it', () => {
    expect(said({ kind: 'task', id: 'b1', name: 'npm run gates' })).toBe('background task · npm run gates · running');
    expect(said({ kind: 'task', id: 'b1', end: 'lost', by: 'inferred' })).toBe('background task · ended with its session');
    expect(said({ kind: 'task', id: 'b1', end: 'stopped', by: 'reported' })).toBe('background task · stopped');
  });

  it('says nothing of a call that started none', () => {
    expect(said(undefined)).toBeUndefined();
  });
});

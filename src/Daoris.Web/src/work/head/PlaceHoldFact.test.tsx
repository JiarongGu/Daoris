import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import type { Session } from '../../data/api';
import { PlaceHoldFact, placeHolds } from './PlaceHoldFact';

// A parked line that keeps uncommitted paths of its own in its repository's checkout says so in its head, with
// *Let others work here*, which lets the next session start there; a park with nothing uncommitted holds nothing and says nothing.

const session = (over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4', quest: 'abc123', repository: 'reports', door: 'claude-code', state: 'awaiting-person', kind: 'driven',
  created: '2026-11-10T09:00:00Z', updated: '2026-11-10T09:30:00Z', ...over,
});

afterEach(async () => {
  cleanup();
  await i18n.changeLanguage('en');
});

describe('a line holding its checkout', () => {
  it('says how many uncommitted paths of its own it keeps, and lets others work there on the press', async () => {
    const onRelease = vi.fn();
    render(<PlaceHoldFact paths={['draft.md', 'notes/plan.md', 'todo.txt']} onRelease={onRelease} />);

    expect(screen.getByText(/holds the checkout: 3 uncommitted paths of its own/)).toBeTruthy();
    expect(screen.getByTitle(/^draft\.md\s+notes\/plan\.md\s+todo\.txt$/)).toBeTruthy();
    await userEvent.click(screen.getByRole('button', { name: 'Let others work here' }));
    expect(onRelease).toHaveBeenCalledTimes(1);
  });

  it('is said only of a line out of its turn that kept paths', () => {
    expect(placeHolds(session({ holdsPlace: ['draft.md'] }))).toEqual(['draft.md']);
    expect(placeHolds(session({ holdsPlace: [] }))).toBeNull();
    expect(placeHolds(session())).toBeNull();
    expect(placeHolds(session({ state: 'working', holdsPlace: ['draft.md'] }))).toBeNull();
  });

  it('says its words in Chinese too', async () => {
    await i18n.changeLanguage('zh');
    render(<PlaceHoldFact paths={['draft.md']} onRelease={vi.fn()} />);

    expect(screen.getByText(/占用着检出/)).toBeTruthy();
    expect(screen.getByRole('button', { name: '让别人在此工作' })).toBeTruthy();
  });
});

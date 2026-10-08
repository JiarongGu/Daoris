import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import '../i18n';
import type { Quest, Session } from '../api';
import { SessionRelations } from './SessionRelations';

// SESS1: who a session worked with, beyond its chain — each relation a door, as the strip's are.

const quest = (over: Partial<Quest> & { id: string }): Quest => ({
  from: 'portal-ui', to: 'iothub', title: over.id, body: '', status: 'Open', filed: '2026-09-29T01:00:00Z', updated: '2026-09-29T01:00:00Z',
  ...over,
});
const ASKER: Session = {
  id: 'a1b2c3d4', quest: null, repository: 'game', adapter: 'claude-code-acp', state: 'completed', kind: 'driven',
  created: '2026-09-29T00:00:00Z', updated: '2026-09-29T00:30:00Z',
};

afterEach(cleanup);

describe('who a session worked with', () => {
  it('draws nothing when it asked and was asked nothing', () => {
    const { container } = render(<SessionRelations relations={{ askedBy: null, resumedAfter: null, asked: [] }} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('names the session that asked for its quest, and attends it on a press', async () => {
    const onSession = vi.fn();
    render(<SessionRelations relations={{ askedBy: { id: ASKER.id, session: ASKER }, resumedAfter: null, asked: [] }} onSession={onSession} />);

    await userEvent.click(screen.getByRole('button', { name: 'session a1b2c3d4 in game' }));
    expect(onSession).toHaveBeenCalledWith(ASKER);
  });

  it('still names an asker whose record this machine does not hold, with no door', () => {
    render(<SessionRelations relations={{ askedBy: { id: 'z9y8x7w6', session: null }, resumedAfter: null, asked: [] }} onSession={vi.fn()} />);
    expect(screen.getByText(/session z9y8x7w6, whose record this machine does not hold/)).toBeTruthy();
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('lists what it asked of others with how each stands, marks its question, and says the answer', async () => {
    const onQuest = vi.fn();
    const question = quest({ id: 'q9', title: 'Which module id does the line report?', status: 'Done', note: 'It is 5fc13d67,\n\nfrom the conveyor.' });
    const open = quest({ id: 'q8', to: 'report-db', title: 'Expose the empty count' });
    render(
      <SessionRelations
        relations={{ askedBy: null, resumedAfter: null, asked: [{ quest: open, question: false }, { quest: question, question: true }] }}
        onQuest={onQuest}
      />,
    );

    expect(screen.getByText('Asked of other repositories (2)')).toBeTruthy();
    expect(screen.getByText('its question: its quest waited on this')).toBeTruthy();
    // The close's words, on one line: content, never translated.
    expect(screen.getByText('Answered: It is 5fc13d67, from the conveyor.')).toBeTruthy();
    expect(screen.getByText('Not answered yet.')).toBeTruthy();

    await userEvent.click(screen.getByRole('button', { name: 'Which module id does the line report?' }));
    expect(onQuest).toHaveBeenCalledWith(question);
  });

  it('says it carried on once an earlier session\'s question was answered', () => {
    const question = quest({ id: 'q9', title: 'Where is the empty count kept?', status: 'Declined', note: 'Not ours: ask report-db.' });
    render(<SessionRelations relations={{ askedBy: null, resumedAfter: question, asked: [] }} />);

    expect(screen.getByText('Carried on once #q9 was answered')).toBeTruthy();
    expect(screen.getByText('Declined: Not ours: ask report-db.')).toBeTruthy();
  });
});

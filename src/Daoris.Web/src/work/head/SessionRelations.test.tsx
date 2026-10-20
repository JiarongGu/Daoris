import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import type { Quest, Session } from '../../data/api';
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
    render(<SessionRelations defaultOpen relations={{ askedBy: { id: ASKER.id, session: ASKER }, resumedAfter: null, asked: [] }} onSession={onSession} />);

    await userEvent.click(screen.getByRole('button', { name: 'session a1b2c3d4 in game' }));
    expect(onSession).toHaveBeenCalledWith(ASKER);
  });

  it('still names an asker whose record this machine does not hold, with no door', () => {
    render(<SessionRelations defaultOpen relations={{ askedBy: { id: 'z9y8x7w6', session: null }, resumedAfter: null, asked: [] }} onSession={vi.fn()} />);
    expect(screen.getByText(/session z9y8x7w6, whose record this machine does not hold/)).toBeTruthy();
    // Only the press that folds it again: the asker is no door.
    expect(screen.getAllByRole('button').map((button) => button.textContent)).toEqual(['Hide who it worked with']);
  });

  it('lists what it asked of others with how each stands, marks its question, and says the answer', async () => {
    const onQuest = vi.fn();
    const question = quest({ id: 'q9', title: 'Which module id does the line report?', status: 'Done', note: 'It is 5fc13d67,\n\nfrom the conveyor.' });
    const open = quest({ id: 'q8', to: 'report-db', title: 'Expose the empty count' });
    render(
      <SessionRelations
        defaultOpen
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
    render(<SessionRelations defaultOpen relations={{ askedBy: null, resumedAfter: question, asked: [] }} />);

    expect(screen.getByText('Carried on once #q9 was answered')).toBeTruthy();
    expect(screen.getByText('Declined: Not ours: ask report-db.')).toBeTruthy();
  });

  // QUESTCLOSE1b2: a coded note is worded in the reader's language beside the answer's label.
  it('words the person’s done in 中文 beside the answer’s label', async () => {
    await i18n.changeLanguage('zh');
    try {
      const question = quest({
        id: 'q9', status: 'Done', note: 'The person marked this done.',
        noteParts: [{ code: 'quest.person-done', values: {} }],
      });
      render(<SessionRelations defaultOpen relations={{ askedBy: null, resumedAfter: null, asked: [{ quest: question, question: true }] }} />);
      expect(screen.getByText(/答复：/)).toHaveTextContent('答复：已由人标为完成。');
    } finally {
      await i18n.changeLanguage('en');
    }
  });

  // FABRIC-CLOSE1d2: what it asked of others reads as the quest's page does.
  it('says submitted for a submitted quest it asked, source reopened beside a step held so, and taken for any other', () => {
    const submission = { machine: 'desk', sequence: 1, at: '2026-09-29T02:00:00Z' };
    const submitted = quest({ id: 'q1', status: 'Taken', closure: 'gph-1', submission });
    const reopened = quest({ id: 'q2', status: 'Taken', hold: 'source-reopened' });
    const plain = quest({ id: 'q3', status: 'Taken' });
    render(
      <SessionRelations
        defaultOpen
        relations={{ askedBy: null, resumedAfter: null, asked: [submitted, reopened, plain].map((q) => ({ quest: q, question: false })) }}
      />,
    );

    const rows = screen.getAllByRole('definition');
    expect(rows[0]).toHaveTextContent(/^submitted/);
    expect(rows[1]).toHaveTextContent(/^taken.*source reopened/);
    expect(rows[2]).toHaveTextContent(/^taken/);
    expect(rows[2]).not.toHaveTextContent(/submitted|source reopened/);
  });
});

// HEADFIT1 (the UI/UX design §3.7 and R4, the UX7 design §6.2): the relations cost 126 px between the record's head and its
// conversation, so they fold to one line that says what is there, and open in place. The page mounts it under the session's id, so
// another record starts folded; here its one state is whether it is open.
describe('the relations, folded to one line', () => {
  const everything = {
    askedBy: { id: ASKER.id, session: ASKER },
    resumedAfter: quest({ id: 'q7', status: 'Done', note: '64 KiB.' }),
    asked: [{ quest: quest({ id: 'q8' }), question: false }, { quest: quest({ id: 'q9', status: 'Done', note: 'No.' }), question: true }],
  };

  it('says each relation in one line and draws none of them, its region named as before', () => {
    render(<SessionRelations relations={everything} onQuest={vi.fn()} onSession={vi.fn()} />);

    const region = screen.getByRole('region', { name: 'Who it worked with' });
    expect(region).toHaveTextContent('asked by game · carried on once #q7 was answered · asked 2 of other repositories');
    expect(within(region).queryByRole('definition')).toBeNull();
    expect(within(region).queryByRole('heading')).toBeNull();
    expect(within(region).getAllByRole('button').map((button) => button.textContent)).toEqual(['Show who it worked with']);
  });

  it('names an asker this machine holds no record of by its id, and counts one quest asked in the singular', () => {
    render(<SessionRelations relations={{ askedBy: { id: 'z9y8x7w6', session: null }, resumedAfter: null, asked: [{ quest: quest({ id: 'q8' }), question: false }] }} />);
    expect(screen.getByRole('region', { name: 'Who it worked with' })).toHaveTextContent('asked by z9y8x7w6 · asked 1 of another repository');
  });

  it('opens in place on the press, with the title as a heading and every door, and folds again on Hide', async () => {
    const user = userEvent.setup();
    const onSession = vi.fn();
    render(<SessionRelations relations={everything} onSession={onSession} />);

    await user.click(screen.getByRole('button', { name: 'Show who it worked with' }));
    const region = screen.getByRole('region', { name: 'Who it worked with' });
    expect(within(region).getByRole('heading', { name: 'Who it worked with' })).toBeInTheDocument();
    expect(within(region).getAllByRole('definition').length).toBeGreaterThan(2);
    await user.click(within(region).getByRole('button', { name: 'session a1b2c3d4 in game' }));
    expect(onSession).toHaveBeenCalledWith(ASKER);

    await user.click(screen.getByRole('button', { name: 'Hide who it worked with' }));
    expect(screen.queryByRole('definition')).toBeNull();
    expect(screen.getByRole('button', { name: 'Show who it worked with' })).toBeInTheDocument();
  });

  it('is said in 中文, the line and its two presses', async () => {
    const user = userEvent.setup();
    await i18n.changeLanguage('zh');
    try {
      render(<SessionRelations relations={everything} />);
      expect(screen.getByRole('region', { name: '协作关系' })).toHaveTextContent('由 game 发起 · #q7 得到答复后继续 · 向其他仓库提出 2 项');
      await user.click(screen.getByRole('button', { name: '展开协作关系' }));
      expect(screen.getByRole('button', { name: '收起协作关系' })).toBeInTheDocument();
    } finally {
      await i18n.changeLanguage('en');
    }
  });
});

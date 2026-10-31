import { afterEach, describe, expect, it } from 'vitest';
import { cleanup, render, screen, within } from '@testing-library/react';
import i18n from '../../locales/i18n';
import { EVIDENCE_ENTRIES, EVIDENCE_SESSIONS } from '../entries.fixtures';
import { PartsStrip } from '../head/PartsStrip';
import { AtWorkView } from './AtWorkView';
import { answerOf } from './atWork.fixtures';
import type { AtWorkHeld } from './types';

// COWORKVIEW1a rule 17: the view's parts are the strip's set and order, each with its record's own state; the strip's derived
// word (the reader's grouping) is deliberately not the view's.

afterEach(async () => {
  cleanup();
  await i18n.changeLanguage('en');
});

const APP = EVIDENCE_ENTRIES[0]!;
const rows = Object.fromEntries(EVIDENCE_SESSIONS.map((session) => [session.id, { session, root: 'C:/work/app' }]));

describe('the strip and the view over one entry', () => {
  it('lists the same parts in the same order, the strip wording a completed part awaiting a helper and the view its record\'s own state', () => {
    const entry = { ...APP, parts: APP.parts.map((part) => (part.session === 'ap000002' ? { ...part, shown: 'awaiting-helper' } : part)) };
    const held: AtWorkHeld = answerOf(
      { read: 'read', harness: 'claude-code', working: 0, children: [] },
      {
        attended: 'ap000003',
        parts: entry.parts.map((part) => {
          const record = EVIDENCE_SESSIONS.find((each) => each.id === part.session)!;
          return { key: part.session, agent: record.adapter, state: record.state, children: { count: 0, working: 0 } };
        }),
      },
    );

    render(<PartsStrip entry={entry} attended="ap000003" rows={rows} onShow={() => {}} />);
    const strip = within(screen.getByRole('navigation', { name: 'Parts' })).getAllByRole('button');
    expect(strip[1]).toHaveTextContent('awaiting helper');
    expect(strip[1]).not.toHaveTextContent('completed');
    cleanup();

    render(<AtWorkView held={held} />);
    const view = screen.getAllByRole('listitem').map((item) => item.textContent ?? '');
    expect(view).toHaveLength(entry.parts.length);
    expect(view.map((line, index) => [line.includes(['failed', 'completed', 'working'][index]!), index])).toEqual([[true, 0], [true, 1], [true, 2]]);
    expect(view[1]).not.toContain('awaiting helper');
  });
});

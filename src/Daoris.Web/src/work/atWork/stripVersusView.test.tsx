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

  // COWORKVIEW1b-i: every part the strip draws waiting the view draws waiting and unfolded, worded as the rail words it.
  it('draws every part the strip draws waiting as waiting, listed and never among the earlier parts', () => {
    const entry = {
      ...APP,
      parts: [
        { ...APP.parts[0]!, session: 'old1', shown: 'completed' },
        { ...APP.parts[0]!, session: 'old2', shown: 'completed' },
        { ...APP.parts[0]!, session: 'old3', shown: 'completed' },
        { ...APP.parts[0]!, session: 'old4', shown: 'completed' },
        { ...APP.parts[0]!, session: 'old5', shown: 'completed' },
        { ...APP.parts[0]!, session: 'reply', shown: 'awaiting-reply' },
        { ...APP.parts[0]!, session: 'helper', shown: 'awaiting-helper' },
        { ...APP.parts[2]!, session: 'now', shown: 'working' },
      ],
    };
    // What the driver answers for it: the attended part first, the waiting parts kept, the ended ones that wait on nothing past the newest three folded.
    const waitsOf = (shown: string) => (shown === 'awaiting-reply' ? { quest: 'q7', of: 'core' } : shown === 'awaiting-helper' ? { helper: true as const } : undefined);
    const listed = [entry.parts[7]!, ...entry.parts.filter((part, index) => part.session !== 'now' && (waitsOf(part.shown) || index >= 3))];
    const held: AtWorkHeld = answerOf(
      { read: 'none', working: 0, children: [] },
      {
        attended: 'now',
        earlier: 2,
        parts: listed.map((part) => ({ key: part.session, state: part.shown === 'working' ? 'working' : 'completed', children: {}, waits: waitsOf(part.shown) })),
      },
    );

    render(<PartsStrip entry={entry} attended="now" rows={{}} onShow={() => {}} />);
    const waiting = within(screen.getByRole('navigation', { name: 'Parts' })).getAllByRole('button')
      .filter((button) => /awaiting[- ](reply|helper)/.test(button.textContent ?? '')).length;
    cleanup();

    render(<AtWorkView held={held} />);
    const rows = screen.getAllByRole('listitem').map((item) => item.textContent ?? '');
    expect(waiting).toBe(2);
    expect(rows.filter((line) => /waits on #q7, asked of core|awaiting helper/.test(line))).toHaveLength(waiting);
    expect(screen.getByText('2 earlier parts')).toBeTruthy();
    expect(rows).toHaveLength(listed.length);
  });
});

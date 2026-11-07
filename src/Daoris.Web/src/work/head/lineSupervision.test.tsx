import { afterEach, describe, expect, it } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../../locales/i18n';
import type { Session } from '../../data/api';
import { DetailsFold } from './DetailsFold';
import { countRows, type LineSupervision, readSupervision, supervisionChip, supervisorRow } from './lineSupervision';
import { SessionPageHead } from './SessionPageHead';

// The line's counts and the acts the person's rules made on it (CONTEXTDRIVE1h): *Details*' course and Supervisor line, and the head's
// chip with its count, in both languages.

const t = i18n.t.bind(i18n);

const held: LineSupervision = {
  counts: [
    { count: 'answers-alone', n: 2, known: 'exact', from: 'admission', watch: { at: 2, do: 'hold', place: 'repository:daoris' } },
    { count: 'failures', n: 1, known: 'exact', from: 'records', watch: { at: 3, do: 'hold', place: 'machine' } },
    { count: 'moves', n: null, known: 'unknown', why: 'no log', from: 'log' },
    { count: 'tries', n: 1, known: 'at-least', from: 'ended.transient', watch: { at: 5, do: 'ask', place: 'workspace:main' } },
  ],
  acts: [
    { count: 'answers-alone', n: 2, known: true, rule: { at: 2, do: 'hold', place: 'repository:daoris' }, point: 'go-on', when: new Date().toISOString() },
  ],
};

const session: Session = {
  id: 's1a2b3c4', quest: 'abc123', repository: 'daoris', door: 'claude-code', state: 'working', kind: 'driven',
  created: '2026-10-02T09:00:00Z', updated: '2026-10-02T09:30:00Z',
};

describe('a line’s supervision', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('reads an older driver’s answer as none', () => {
    expect(readSupervision({ decisions: [] })).toEqual({ counts: [], acts: [] });
    expect(readSupervision(null)).toEqual({ counts: [], acts: [] });
  });

  it('draws each count with what it stands at and the rule that acts on it, and says when it is not known', () => {
    const rows = countRows(t, held);
    expect(rows.map((row) => [row.label, row.value])).toEqual([
      ['Answers alone in a row', '2 · your rule: Wait for you at 2 (daoris)'],
      ['Failed sessions on a quest', '1 · your rule: Wait for you at 3 (This Daoris)'],
      ['Moves to another choice a day', 'not known'],
      ['The service did not answer', 'at least 1 · your rule: Ask before the next at 5 (main)'],
    ]);
  });

  it('says the supervisor’s line from the acts kept, and none where no rule acted', () => {
    const row = supervisorRow(t, held);
    expect(row.label).toBe('Supervisor');
    expect(String(row.value)).toMatch(/^your rules acted 1 time, last .+: Answers alone in a row 2, Wait for you$/);
    expect(supervisorRow(t, { counts: [], acts: [] }).value).toBeNull();
  });

  it('chips the count a rule holds the line at, else the one a count short of its rule, else nothing', () => {
    expect(supervisionChip(t, held)).toMatchObject({ count: 'answers-alone', held: true, text: 'answers alone in a row 2 of 2' });
    const near = supervisionChip(t, { counts: [{ count: 'failures', n: 2, known: 'exact', from: 'records', watch: { at: 3, do: 'hold', place: 'machine' } }], acts: [] });
    expect(near).toMatchObject({ held: false, text: 'failed sessions on a quest 2 of 3' });
    expect(supervisionChip(t, { counts: [{ count: 'failures', n: 1, known: 'exact', from: 'records', watch: { at: 3, do: 'hold', place: 'machine' } }], acts: [] })).toBeNull();
    // A count not known exactly, or one the rule does not hold on, never chips.
    expect(supervisionChip(t, { counts: [{ count: 'tries', n: 5, known: 'at-least', from: 'ended.transient', watch: { at: 5, do: 'hold', place: 'machine' } }], acts: [] })).toBeNull();
    expect(supervisionChip(t, { counts: [{ count: 'limits', n: 1, known: 'exact', from: 'events', watch: { at: 1, do: 'wait', place: 'machine' } }], acts: [] })).toBeNull();
  });

  it('opens Details at the course and the Supervisor line, in Chinese too', async () => {
    await i18n.changeLanguage('zh');
    render(<DetailsFold summary="" rows={[...countRows(i18n.t.bind(i18n), held), supervisorRow(i18n.t.bind(i18n), held)]} />);
    await userEvent.click(screen.getByRole('button', { name: /详情/ }));
    expect(screen.getByText('连续仅靠回答继续')).toBeInTheDocument();
    expect(screen.getByText('2 · 你的规则：到 2 时等你处理（daoris）')).toBeInTheDocument();
    expect(screen.getByText('主管')).toBeInTheDocument();
    expect(screen.getByText(/你的主管规则已生效 1 次/)).toBeInTheDocument();
  });

  it('puts the chip on the head with its count and its tip', async () => {
    render(
      <Tooltip.Provider>
        <SessionPageHead session={session} title="Build the policy store" shown="awaiting-person" acts={[]} chip={supervisionChip(t, held)} />
      </Tooltip.Provider>,
    );
    const chip = within(screen.getByRole('banner')).getByText('answers alone in a row 2 of 2');
    expect(chip.getAttribute('data-supervision-chip')).toBe('held');
  });
});

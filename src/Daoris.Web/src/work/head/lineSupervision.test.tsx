import { afterEach, describe, expect, it } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import * as Tooltip from '@radix-ui/react-tooltip';
import i18n from '../../locales/i18n';
import type { Session } from '../../data/api';
import { DetailsFold } from './DetailsFold';
import { type CourseRead, countRows, courseRow, type LineSupervision, readSupervision, supervisionChip, supervisorRow } from './lineSupervision';
import { SessionPageHead } from './SessionPageHead';

// The line's counts and the acts the person's rules made on it: *Details*' course and Supervisor line, and the head's
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
  courses: [],
};

/** A course decision as `SESSION_SUPERVISOR` answers it, kept two minutes ago and going on in three. */
const course = (state: string, over: Partial<CourseRead> = {}): CourseRead => ({
  id: 'd1', point: 'rule-1', count: 'answers-alone', n: 2, rule: 'aa', place: 'repository:engine', do: 'go-on', state, why: null,
  holder: { kind: 'terminal' }, keptAt: new Date(Date.now() - 120_000).toISOString(), goesOnAt: new Date(Date.now() + 180_000).toISOString(),
  stateAt: new Date(Date.now() - 120_000).toISOString(), earlier: false, ...over,
});

const session: Session = {
  id: 's1a2b3c4', quest: 'abc123', repository: 'daoris', door: 'claude-code', state: 'working', kind: 'driven',
  created: '2026-10-02T09:00:00Z', updated: '2026-10-02T09:30:00Z',
};

describe('a line’s supervision', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('reads an older driver’s answer as none', () => {
    expect(readSupervision({ decisions: [] })).toEqual({ counts: [], acts: [], courses: [] });
    expect(readSupervision(null)).toEqual({ counts: [], acts: [], courses: [] });
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
    expect(supervisorRow(t, { counts: [], acts: [], courses: [] }).value).toBeNull();
  });

  it('chips the count a rule holds the line at, else the one a count short of its rule, else nothing', () => {
    expect(supervisionChip(t, held)).toMatchObject({ count: 'answers-alone', held: true, text: 'answers alone in a row 2 of 2' });
    const near = supervisionChip(t, { counts: [{ count: 'failures', n: 2, known: 'exact', from: 'records', watch: { at: 3, do: 'hold', place: 'machine' } }], acts: [], courses: [] });
    expect(near).toMatchObject({ held: false, text: 'failed sessions on a quest 2 of 3' });
    expect(supervisionChip(t, { counts: [{ count: 'failures', n: 1, known: 'exact', from: 'records', watch: { at: 3, do: 'hold', place: 'machine' } }], acts: [], courses: [] })).toBeNull();
    // A count not known exactly, or one the rule does not hold on, never chips.
    expect(supervisionChip(t, { counts: [{ count: 'tries', n: 5, known: 'at-least', from: 'ended.transient', watch: { at: 5, do: 'hold', place: 'machine' } }], acts: [], courses: [] })).toBeNull();
    expect(supervisionChip(t, { counts: [{ count: 'limits', n: 1, known: 'exact', from: 'events', watch: { at: 1, do: 'wait', place: 'machine' } }], acts: [], courses: [] })).toBeNull();
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

  it('says the supervisor’s decision on a referred point: which rule referred, what it chose and when, and where it stands', () => {
    const say = (state: string, over: Partial<CourseRead> = {}) =>
      String(courseRow(t, { counts: [], acts: [], courses: [course(state, over)] }).value);
    expect(courseRow(t, { counts: [], acts: [], courses: [] })).toEqual({ label: "Supervisor's decision", value: null });
    expect(courseRow(t, { counts: [], acts: [], courses: [course('held', { kind: 'course' })] }).label).toBe("Supervisor's decision on the line's course");
    expect(courseRow(t, { counts: [], acts: [], courses: [course('held', { kind: undefined })] }).label).toBe("Supervisor's decision on the line's course");
    expect(say('held')).toMatch(/^Your rule for answers alone in a row \(engine\) asked the supervisor at 2; it chose to go on 2m ago\. It goes on at .+ unless you overrule it\.$/);
    expect(say('went-on')).toMatch(/it chose to go on 2m ago\. It went on\.$/);
    expect(say('overruled')).toMatch(/\. You overruled it\.$/);
    expect(say('stale', { why: 'rule-holds' })).toMatch(/\. It did not go on: your rule holds the line now\.$/);
    expect(say('left', { do: 'hold' })).toMatch(/it chose to leave it to you 2m ago\. It waits for you\.$/);
    // The newest decision speaks for the line.
    const both = courseRow(t, { counts: [], acts: [], courses: [course('overruled', { id: 'd0' }), course('went-on', { id: 'd1' })] });
    expect(String(both.value)).toMatch(/It went on\.$/);
  });

  it('reads the courses the driver answers, and none from an older driver', () => {
    const held = course('held');
    expect(readSupervision({ courses: [held] }).courses).toEqual([held]);
    expect(readSupervision({ courses: 'nope' }).courses).toEqual([]);
  });

  it('says the decision in Chinese too', async () => {
    await i18n.changeLanguage('zh');
    const zh = i18n.t.bind(i18n);
    render(<DetailsFold summary="" rows={[courseRow(zh, { counts: [], acts: [], courses: [course('held')] })]} />);
    await userEvent.click(screen.getByRole('button', { name: /详情/ }));
    expect(screen.getByText('主管对这条线去向的决定')).toBeInTheDocument();
    expect(screen.getByText(/^你为连续仅靠回答继续（engine）设的规则在 2 时请主管判断；它决定继续，2 分钟前。到 .+ 时继续，除非你否决它。$/)).toBeInTheDocument();
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

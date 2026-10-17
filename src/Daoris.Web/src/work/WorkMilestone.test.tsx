import { afterEach, describe, expect, it } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import i18n from '../locales/i18n';
import userEvent from '@testing-library/user-event';
import { type HeldAttempt, type MilestoneEntry, type MilestoneUnit, readWorkState, unitOfBranch, waitsOnPerson } from './milestone';
import { MilestoneLine, WorkMilestone } from './WorkMilestone';
import { attentionActs, needsAPerson } from './attention';
import { AttentionRow } from './AttentionRow';

// The milestone on the work's foot (FABRIC-MILESTONE1b, the fabric events design §D0): a molecule over the driver's answer, each
// state reached by its props. The driver's own clauses pass through as recorded; the labels and the proofs' codes are the page's.

const entry = (over: Partial<MilestoneEntry>): MilestoneEntry => ({
  unit: 'u1', kind: 'session-branch', resource: 'daoris/s-1', role: 'temporary', disposition: 'kept', detail: 'standing', ...over,
});

const unit = (over: Partial<MilestoneUnit> = {}): MilestoneUnit => ({
  unit: 'u1', repository: 'engine', branch: 'daoris/s-1', session: 's-1',
  end: { state: 'ended', by: 'merged', on: 'main', revision: 'a1b2c3d4e5f6', at: '2026-10-17T09:00:00Z', said: 'every commit of it is on `main`' },
  result: [entry({ kind: 'line', resource: 'main', role: 'result', detail: 'the line holds it' })],
  cleanup: { state: 'completed', waits: [], gone: [entry({ disposition: 'removed', detail: 'went 2026-10-17 11:00 UTC' })] },
  recovery: [],
  entries: [],
  ...over,
});

describe('the milestone on the work’s foot', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('says the end reached by its proof, the result kept, the clean-up completed with what went, and no recovery', () => {
    render(<WorkMilestone unit={unit()} />);
    const region = screen.getByRole('region', { name: 'Where this work stands' });
    expect(within(region).getByText('End')).toBeInTheDocument();
    expect(region).toHaveTextContent('Reached: every commit of it is on main');
    expect(region).toHaveTextContent('Kept: main');
    expect(region).toHaveTextContent('Completed: removed session branch daoris/s-1');
    expect(region).toHaveTextContent('Nothing kept for recovery');
  });

  it('says a clean-up pending with what waits and why, a blocked wait apart from one that waits', () => {
    render(<WorkMilestone unit={unit({
      cleanup: {
        state: 'pending',
        waits: [
          entry({ kind: 'tree', resource: 's-1', disposition: 'blocked', detail: 'a session still holds this tree' }),
          entry({ kind: 'end', resource: 'the end', disposition: 'pending', detail: 'the cycle waits' }),
        ],
        gone: [],
      },
    })} />);
    const region = screen.getByRole('region', { name: 'Where this work stands' });
    expect(region).toHaveTextContent('Pending');
    expect(region).toHaveTextContent('Blocked: tree s-1, a session still holds this tree');
    expect(region).toHaveTextContent('Waits: the end, the cycle waits');
  });

  it('says the recovery kept and until when, and a folder or ref kept with no date', () => {
    render(<WorkMilestone unit={unit({
      recovery: [
        entry({ kind: 'moved-aside', resource: 's-1-20261017', role: 'recovery', disposition: 'retained', detail: 'set aside', until: '2026-10-24T09:00:00Z' }),
        entry({ kind: 'recovery-ref', resource: 'refs/daoris/discarded/x', role: 'recovery', disposition: 'retained', detail: 'kept' }),
      ],
    })} />);
    const region = screen.getByRole('region', { name: 'Where this work stands' });
    expect(region).toHaveTextContent(/folder set aside s-1-20261017, kept until/);
    expect(region).toHaveTextContent('recovery ref refs/daoris/discarded/x, kept');
  });

  it('says an end not reached in the driver’s sentence, and an unknown owner as one', () => {
    const { rerender } = render(<WorkMilestone unit={unit({
      end: { state: 'not-ended', said: 'nothing was handed on and the line does not hold it' }, result: [],
      cleanup: { state: 'pending', waits: [], gone: [] },
    })} />);
    const region = screen.getByRole('region', { name: 'Where this work stands' });
    expect(region).toHaveTextContent('Not reached: nothing was handed on and the line does not hold it');
    expect(region).toHaveTextContent('None kept yet');
    rerender(<WorkMilestone unit={unit({ end: { state: 'unknown', said: 'no record names its unit, so its end is not said' } })} />);
    expect(region).toHaveTextContent('Unknown: no record names its unit');
  });

  // CLEANUP1: a landing whose branch the person removed outside Daoris is over without its result, never read as not reached.
  it('says an end the person dropped as over without its result, in both languages, and as one line', async () => {
    const dropped = unit({
      end: { state: 'dropped', said: '`feature/x` was removed outside Daoris, and git said it is missing' }, result: [],
      cleanup: { state: 'completed', waits: [], gone: [entry({ kind: 'landing-branch', resource: 'feature/x', disposition: 'removed' })] },
    });
    const { unmount } = render(<WorkMilestone unit={dropped} />);
    const region = screen.getByRole('region', { name: 'Where this work stands' });
    expect(region).toHaveTextContent('Over without its result: `feature/x` was removed outside Daoris');
    expect(region).not.toHaveTextContent('Not reached');
    unmount();
    render(<MilestoneLine unit={dropped} />);
    expect(screen.getByText(/Over, no result kept/)).toHaveTextContent('clean-up completed');
    expect(waitsOnPerson(dropped)).toEqual([]);
    await i18n.changeLanguage('zh');
    expect(i18n.t('work.milestone.end.dropped', { said: 'x' })).not.toBe(i18n.t('work.milestone.end.dropped', { said: 'x', lng: 'en' }));
  });

  // FABRIC-END2d: a landing ended inside another landing is said by the page's words for its code, in both languages.
  it('says an end inside another landing in the page’s words, never the driver’s sentence', async () => {
    const inside = unit({ end: { state: 'ended', by: 'inside', on: 'feature/q2-more', said: 'it is inside `feature/q2-more`, whose work is on the line' } });
    render(<WorkMilestone unit={inside} />);
    const region = screen.getByRole('region', { name: 'Where this work stands' });
    expect(region).toHaveTextContent('Reached: it is inside feature/q2-more, whose work is on the line');
    await i18n.changeLanguage('zh');
    expect(i18n.t('work.milestone.by.inside', { on: 'x' })).not.toBe(i18n.t('work.milestone.by.inside', { on: 'x', lng: 'en' }));
  });

  it('is said in Chinese by the same facts', async () => {
    await i18n.changeLanguage('zh');
    render(<WorkMilestone unit={unit({
      cleanup: { state: 'pending', waits: [entry({ kind: 'tree', resource: 's-1', disposition: 'blocked', detail: 'a session still holds this tree' })], gone: [] },
      recovery: [entry({ kind: 'moved-aside', resource: 's-1-x', role: 'recovery', disposition: 'retained', detail: 'set aside', until: '2026-10-24T09:00:00Z' })],
    })} />);
    const region = screen.getByRole('region', { name: i18n.t('work.milestone.title') });
    expect(i18n.t('work.milestone.title')).not.toBe('Where this work stands');
    expect(region).toHaveTextContent(i18n.t('work.milestone.end.ended', { proof: i18n.t('work.milestone.by.merged', { on: 'main' }) }));
    expect(region).toHaveTextContent('成果');
    expect(region).toHaveTextContent('结束');
  });

  it('is one line for a branch row: the end, the clean-up and what is kept', () => {
    render(<MilestoneLine unit={unit({ recovery: [entry({ kind: 'moved-aside', until: '2026-10-24T09:00:00Z', disposition: 'retained', role: 'recovery' })] })} />);
    const line = screen.getByText(/Ended/);
    expect(line).toHaveTextContent('Ended');
    expect(line).toHaveTextContent('clean-up completed');
    expect(line).toHaveTextContent('recovery kept until');
  });
});

describe('what of a unit waits on the person', () => {
  it('is the blocked entries of its clean-up and nothing a cycle waits on by itself', () => {
    const blocked = entry({ kind: 'tree', disposition: 'blocked' });
    expect(waitsOnPerson(unit({ cleanup: { state: 'pending', waits: [blocked, entry({ disposition: 'pending' })], gone: [] } }))).toEqual([blocked]);
    expect(waitsOnPerson(unit({ cleanup: { state: 'pending', waits: [entry({ disposition: 'pending' })], gone: [] } }))).toEqual([]);
    expect(waitsOnPerson(unit())).toEqual([]);
  });

  it('finds the unit a branch belongs to by its repository and either of its branches', () => {
    const held = unit({ entries: [entry({ kind: 'landing-branch', resource: 'feature/x' })] });
    expect(unitOfBranch([held], 'Engine', 'daoris/s-1')).toBe(held);
    expect(unitOfBranch([held], 'engine', 'feature/x')).toBe(held);
    expect(unitOfBranch([held], 'other', 'feature/x')).toBeUndefined();
  });
});

// FABRIC-PERFORM1g (the fabric events design §D6): a plugin step's attempt held for the person, shown where they look, What needs you's
// row with their three words and the node's place on the work's foot, in both languages.
const heldAttempt = (over: Partial<HeldAttempt> = {}): HeldAttempt => ({
  unit: 'u1', node: 'deploy', attempt: 2, plugin: 'acme.deploy', step: 'acme.deploy/deploy', nodeState: 'unknown', why: 'unknown',
  at: '2026-10-17T09:00:00Z', said: 'its plugin was asked and cannot say what became of it', ...over,
});

describe('an attempt held for the person', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('joins each unit its held attempts, keeps every one held of any unit, and reads none from an older shell', () => {
    const read = readWorkState({ units: [unit()], recovery: [], unread: [], held: [heldAttempt(), heldAttempt({ unit: 'quest:q7' })] });
    expect(read.held).toHaveLength(2);
    expect(read.units[0]!.held).toEqual([heldAttempt()]);
    expect(readWorkState({ units: [unit()] }).held).toEqual([]);
  });

  it('is a row in What needs you naming its step, its attempt and why, offering the three words until one is recorded', () => {
    const [row] = needsAPerson([], [], [], [], [], [], [], [], undefined, [], [], [], [heldAttempt()]);
    expect(row).toMatchObject({ kind: 'work', title: 'deploy, attempt 2', where: 'u1' });
    expect(row!.detail).toContain('Its plugin was asked and cannot say what became of it');
    expect(attentionActs(row!)).toEqual(['happened', 'did-not', 'stop-step']);

    const [worded] = needsAPerson([], [], [], [], [], [], [], [], undefined, [], [], [], [heldAttempt({ resolution: 'did-not' })]);
    expect(worded!.detail).toContain('your word “it did not” is recorded');
    expect(attentionActs(worded!)).toEqual([]);
  });

  it('presses the person’s word for the row it is on', async () => {
    const [row] = needsAPerson([], [], [], [], [], [], [], [], undefined, [], [], [], [heldAttempt()]);
    const words: string[] = [];
    render(<ul><AttentionRow item={row!} acts={{ held: (_, resolution) => words.push(resolution) }} /></ul>);
    await userEvent.click(screen.getByRole('button', { name: 'It did not: try again' }));
    await userEvent.click(screen.getByRole('button', { name: 'Stop this step' }));
    await userEvent.click(screen.getByRole('button', { name: 'It happened' }));
    expect(words).toEqual(['did-not', 'stop', 'happened']);
  });

  // FABRIC-CLOSE1q: Daoris's own close held for the person is the door's, never a plugin's step: its row says what the door answered
  // and offers only the close's recoveries and its stop, never *It happened* or *It did not*, in both languages.
  const heldClose = (over: Partial<HeldAttempt> = {}): HeldAttempt => heldAttempt({
    unit: 'quest:q7', node: 'close', attempt: 1, plugin: 'quest.close', step: 'quest.close', nodeState: 'unknown', why: 'no-acceptance',
    outcome: 'reached', words: ['record', 'resend', 'stop'], said: 'the quest was closed', ...over,
  });

  it('offers a held close only its own recoveries and its stop, saying what its door answered', async () => {
    const [row] = needsAPerson([], [], [], [], [], [], [], [], undefined, [], [], [], [heldClose()]);
    expect(row).toMatchObject({ kind: 'work', title: 'close, attempt 1', where: 'quest:q7' });
    expect(row!.detail).toContain('The quest was closed, and Daoris could not record that on this work (no-acceptance)');
    expect(row!.detail).toContain('never closed a second time');
    expect(attentionActs(row!)).toEqual(['record-close', 'resend-close', 'stop-close']);
    const [unkept] = needsAPerson([], [], [], [], [], [], [], [], undefined, [], [], [], [heldClose({ outcome: null, words: ['resend', 'stop'] })]);
    expect(attentionActs(unkept!)).toEqual(['resend-close', 'stop-close']);
    const [older] = needsAPerson([], [], [], [], [], [], [], [], undefined, [], [], [], [heldClose({ words: undefined })]);
    expect(attentionActs(older!)).toEqual([]);

    const words: string[] = [];
    render(<ul><AttentionRow item={row!} acts={{ held: (_, resolution) => words.push(resolution) }} /></ul>);
    expect(screen.queryByRole('button', { name: 'It happened' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'It did not: try again' })).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: 'Ask for the answer again' }));
    await userEvent.click(screen.getByRole('button', { name: 'Stop the rest of this work' }));
    await userEvent.click(screen.getByRole('button', { name: 'Record the answer again' }));
    expect(words).toEqual(['resend', 'stop', 'record']);

    const [worded] = needsAPerson([], [], [], [], [], [], [], [], undefined, [], [], [], [heldClose({ resolution: 'resend' })]);
    expect(worded!.detail).toContain('your word “ask for the answer again” is recorded');
    expect(attentionActs(worded!)).toEqual([]);
  });

  it('says a held close on the work’s foot in English and in 中文', async () => {
    const { unmount } = render(<WorkMilestone unit={unit({ held: [heldClose({ why: 'not-close-word' })] })} />);
    expect(screen.getByRole('region', { name: 'Where this work stands' }))
      .toHaveTextContent("close, attempt 1: Your word could not be taken: only the close's own answer says whether the quest closed. It waits for your word.");
    unmount();

    await i18n.changeLanguage('zh');
    render(<WorkMilestone unit={unit({ held: [heldClose({ outcome: 'failed' })] })} />);
    expect(screen.getByRole('region', { name: '这项工作进展到哪里' }))
      .toHaveTextContent('close，第 1 次尝试：委托的关闭被拒绝了，但 Daoris 没能把这一结果记到这项工作上（no-acceptance）。它在等你的答复。');
  });

  it('takes its node’s place on the work’s foot, in English and in 中文', async () => {
    const { unmount } = render(<WorkMilestone unit={unit({ held: [heldAttempt({ why: 'no-reconcile' })] })} />);
    const region = screen.getByRole('region', { name: 'Where this work stands' });
    expect(within(region).getByText('Held for you')).toBeInTheDocument();
    expect(region).toHaveTextContent('deploy, attempt 2: Its step declares no way to ask what became of it. It waits for your word.');
    unmount();

    await i18n.changeLanguage('zh');
    render(<WorkMilestone unit={unit({ held: [heldAttempt({ why: 'acme-refused' })] })} />);
    const zh = screen.getByRole('region', { name: '这项工作进展到哪里' });
    expect(within(zh).getByText('等你定夺')).toBeInTheDocument();
    expect(zh).toHaveTextContent('deploy，第 2 次尝试：它的插件的答复与它所针对的工作不符（acme-refused）。它在等你的答复。');
  });

  it('shows nothing held where the driver lists none, as on an install no plugin step governs', () => {
    render(<WorkMilestone unit={unit({ held: [] })} />);
    expect(screen.queryByText('Held for you')).not.toBeInTheDocument();
    expect(needsAPerson([], [], [], [], [], [], [], [], undefined, [], [], [], [])).toEqual([]);
  });
});

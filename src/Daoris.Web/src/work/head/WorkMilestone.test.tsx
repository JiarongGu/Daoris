import { afterEach, describe, expect, it } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import i18n from '../../locales/i18n';
import userEvent from '@testing-library/user-event';
import { type HeldAttempt, type MilestoneEntry, type MilestoneUnit, readWorkState, unitOfBranch, waitsOnPerson } from './milestone';
import { MilestoneLine, WorkMilestone } from './WorkMilestone';
import { attentionActs, attentionWorded } from '../attention/attention';
import { AttentionRow } from '../attention/AttentionRow';

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
    render(<WorkMilestone defaultOpen unit={unit()} />);
    const region = screen.getByRole('region', { name: 'Where this work stands' });
    expect(within(region).getByText('End')).toBeInTheDocument();
    expect(region).toHaveTextContent('Reached: every commit of it is on main');
    expect(region).toHaveTextContent('Kept: main');
    expect(region).toHaveTextContent('Completed: removed session branch daoris/s-1');
    expect(region).toHaveTextContent('Nothing kept for recovery');
  });

  it('says a clean-up pending with what waits and why, a blocked wait apart from one that waits', () => {
    render(<WorkMilestone defaultOpen unit={unit({
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
    render(<WorkMilestone defaultOpen unit={unit({
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
    const { rerender } = render(<WorkMilestone defaultOpen unit={unit({
      end: { state: 'not-ended', said: 'nothing was handed on and the line does not hold it' }, result: [],
      cleanup: { state: 'pending', waits: [], gone: [] },
    })} />);
    const region = screen.getByRole('region', { name: 'Where this work stands' });
    expect(region).toHaveTextContent('Not reached: nothing was handed on and the line does not hold it');
    expect(region).toHaveTextContent('None kept yet');
    rerender(<WorkMilestone defaultOpen unit={unit({ end: { state: 'unknown', said: 'no record names its unit, so its end is not said' } })} />);
    expect(region).toHaveTextContent('Unknown: no record names its unit');
  });

  // CLEANUP1: a landing whose branch the person removed outside Daoris is over without its result, never read as not reached.
  it('says an end the person dropped as over without its result, in both languages, and as one line', async () => {
    const dropped = unit({
      end: { state: 'dropped', said: '`feature/x` was removed outside Daoris, and git said it is missing' }, result: [],
      cleanup: { state: 'completed', waits: [], gone: [entry({ kind: 'landing-branch', resource: 'feature/x', disposition: 'removed' })] },
    });
    const { unmount } = render(<WorkMilestone defaultOpen unit={dropped} />);
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
    render(<WorkMilestone defaultOpen unit={inside} />);
    const region = screen.getByRole('region', { name: 'Where this work stands' });
    expect(region).toHaveTextContent('Reached: it is inside feature/q2-more, whose work is on the line');
    await i18n.changeLanguage('zh');
    expect(i18n.t('work.milestone.by.inside', { on: 'x' })).not.toBe(i18n.t('work.milestone.by.inside', { on: 'x', lng: 'en' }));
  });

  it('is said in Chinese by the same facts', async () => {
    await i18n.changeLanguage('zh');
    render(<WorkMilestone defaultOpen unit={unit({
      cleanup: { state: 'pending', waits: [entry({ kind: 'tree', resource: 's-1', disposition: 'blocked', detail: 'a session still holds this tree' })], gone: [] },
      recovery: [entry({ kind: 'moved-aside', resource: 's-1-x', role: 'recovery', disposition: 'retained', detail: 'set aside', until: '2026-10-24T09:00:00Z' })],
    })} />);
    const region = screen.getByRole('region', { name: i18n.t('work.milestone.title') });
    expect(i18n.t('work.milestone.title')).not.toBe('Where this work stands');
    expect(region).toHaveTextContent(i18n.t('work.milestone.end.ended', { proof: i18n.t('work.milestone.by.merged', { on: 'main' }) }));
    expect(region).toHaveTextContent('成果');
    expect(region).toHaveTextContent('结束');
  });

  // OVERVIEWNOISE1b: a blocked entry is said by its code in the reader's language, its reason once, and a tree by its folder's name.
  it('says a blocked entry by its code in each language, its reason once, and a tree by its folder', async () => {
    const blocked = unit({
      cleanup: {
        state: 'pending',
        waits: [
          entry({
            kind: 'session-branch', resource: 'daoris/s-1', disposition: 'blocked', code: 'kept', facts: { why: 'nested', at: '2026-10-20T09:00:00Z' },
            detail: 'the look’s tidy kept it at 2026-10-20 09:00 UTC, since its tree holds another repository at docs/inner',
          }),
          entry({
            kind: 'tree', resource: 'X:/daoris/trees/default/engine/s-1', disposition: 'blocked', code: 'unfollowed', facts: { branch: 'daoris/s-1' },
            detail: 'its session recorded it, but no branch record follows `daoris/s-1`',
          }),
        ],
        gone: [],
      },
    });
    const { unmount } = render(<WorkMilestone defaultOpen unit={blocked} />);
    const region = screen.getByRole('region', { name: 'Where this work stands' });
    expect(region).toHaveTextContent('Blocked: session branch daoris/s-1, the look kept it, since its tree holds another repository');
    expect(region).toHaveTextContent('Blocked: tree s-1, its session recorded it, but no branch record follows daoris/s-1');
    expect(region).not.toHaveTextContent('X:/daoris');
    expect(region.textContent?.match(/another repository/g)).toHaveLength(1);
    unmount();

    await i18n.changeLanguage('zh');
    render(<WorkMilestone defaultOpen unit={blocked} />);
    const zh = screen.getByRole('region', { name: i18n.t('work.milestone.title') });
    expect(zh).toHaveTextContent('受阻：会话分支 daoris/s-1，查看时把它留下了，因为它的工作树里含有另一个仓库');
    expect(zh).not.toHaveTextContent('another repository');
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
  // OVERVIEWNOISE1b, as `NeedsAPerson` judges it: only what the person's press settles; a guard the look keeps a branch by is not theirs.
  it('is what only the clean-up’s press settles, and never a guard or a wait that ends by itself', () => {
    const unfollowed = entry({ kind: 'tree', disposition: 'blocked', code: 'unfollowed', facts: { branch: 'daoris/s-1' } });
    const kept = entry({ disposition: 'blocked', code: 'kept', facts: { why: 'nested' } });
    const pending = entry({ disposition: 'pending' });
    expect(waitsOnPerson(unit({ cleanup: { state: 'pending', waits: [unfollowed, kept, pending], gone: [] } }))).toEqual([unfollowed]);
    expect(waitsOnPerson(unit({ cleanup: { state: 'pending', waits: [kept, entry({ kind: 'tree', disposition: 'blocked' })], gone: [] } }))).toEqual([]);
    expect(waitsOnPerson(unit({ cleanup: { state: 'pending', waits: [pending], gone: [] } }))).toEqual([]);
    expect(waitsOnPerson(unit())).toEqual([]);
    // An owner nothing records: every entry blocked, else the unit by its first.
    const unknown = { state: 'unknown', said: 'no record names its unit' };
    expect(waitsOnPerson(unit({ end: unknown, cleanup: { state: 'pending', waits: [kept, pending], gone: [] } }))).toEqual([kept]);
    expect(waitsOnPerson(unit({ end: unknown, cleanup: { state: 'pending', waits: [], gone: [] }, entries: [pending] }))).toEqual([pending]);
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

/** What needs you's row for a held attempt, as the shell pushes it (PUSH1d4: .NET judges it) and the page words it. */
const heldRow = (attempt: HeldAttempt) => attentionWorded({
  kind: 'work', id: `${attempt.unit}#${attempt.node}#${attempt.attempt}`, since: attempt.resolvedAt ?? attempt.at, where: attempt.unit, held: attempt,
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
    const row = heldRow(heldAttempt());
    expect(row).toMatchObject({ kind: 'work', title: 'deploy, attempt 2', where: 'u1' });
    expect(row.detail).toContain('Its plugin was asked and cannot say what became of it');
    expect(attentionActs(row)).toEqual(['happened', 'did-not', 'stop-step']);

    const worded = heldRow(heldAttempt({ resolution: 'did-not' }));
    expect(worded.detail).toContain('your word “it did not” is recorded');
    expect(attentionActs(worded)).toEqual([]);
  });

  it('presses the person’s word for the row it is on', async () => {
    const row = heldRow(heldAttempt());
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
    const row = heldRow(heldClose());
    expect(row).toMatchObject({ kind: 'work', title: 'close, attempt 1', where: 'quest:q7' });
    expect(row.detail).toContain('The quest was closed, and Daoris could not record that on this work (no-acceptance)');
    expect(row.detail).toContain('never closed a second time');
    expect(attentionActs(row)).toEqual(['record-close', 'resend-close', 'stop-close']);
    expect(attentionActs(heldRow(heldClose({ outcome: null, words: ['resend', 'stop'] })))).toEqual(['resend-close', 'stop-close']);
    expect(attentionActs(heldRow(heldClose({ words: undefined })))).toEqual([]);

    const words: string[] = [];
    render(<ul><AttentionRow item={row!} acts={{ held: (_, resolution) => words.push(resolution) }} /></ul>);
    expect(screen.queryByRole('button', { name: 'It happened' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'It did not: try again' })).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: 'Ask for the answer again' }));
    await userEvent.click(screen.getByRole('button', { name: 'Stop the rest of this work' }));
    await userEvent.click(screen.getByRole('button', { name: 'Record the answer again' }));
    expect(words).toEqual(['resend', 'stop', 'record']);

    const worded = heldRow(heldClose({ resolution: 'resend' }));
    expect(worded.detail).toContain('your word “ask for the answer again” is recorded');
    expect(attentionActs(worded)).toEqual([]);
  });

  it('says a held close on the work’s foot in English and in 中文', async () => {
    const { unmount } = render(<WorkMilestone defaultOpen unit={unit({ held: [heldClose({ why: 'not-close-word' })] })} />);
    expect(screen.getByRole('region', { name: 'Where this work stands' }))
      .toHaveTextContent("close, attempt 1: Your word could not be taken: only the close's own answer says whether the quest closed. It waits for your word.");
    unmount();

    await i18n.changeLanguage('zh');
    render(<WorkMilestone defaultOpen unit={unit({ held: [heldClose({ outcome: 'failed' })] })} />);
    expect(screen.getByRole('region', { name: '这项工作进展到哪里' }))
      .toHaveTextContent('close，第 1 次尝试：委托的关闭被拒绝了，但 Daoris 没能把这一结果记到这项工作上（no-acceptance）。它在等你的答复。');
  });

  it('says an unknown close as its door’s silence, asked again at each look, in English and in 中文 (FABRIC-CLOSE1y)', async () => {
    const unknown = heldClose({ why: 'unknown', outcome: null, words: ['resend', 'stop'] });
    const { unmount } = render(<WorkMilestone defaultOpen unit={unit({ held: [unknown] })} />);
    const en = screen.getByRole('region', { name: 'Where this work stands' });
    expect(en).toHaveTextContent(
      "close, attempt 1: Its door has not answered the quest's close, and Daoris asks it again at each look. It waits for your word.");
    expect(en).not.toHaveTextContent('keeps no answer');
    unmount();

    await i18n.changeLanguage('zh');
    render(<WorkMilestone defaultOpen unit={unit({ held: [unknown] })} />);
    const zh = screen.getByRole('region', { name: '这项工作进展到哪里' });
    expect(zh).toHaveTextContent('close，第 1 次尝试：受理委托关闭的一方还没有答复，Daoris 每次查看时都会再问一次。它在等你的答复。');
    expect(zh).not.toHaveTextContent('没有留下');
  });

  it('says a word taken back on a close whose submission was reopened or replaced, in English and in 中文 (FABRIC-CLOSE1q2)', async () => {
    const { unmount } = render(<WorkMilestone defaultOpen unit={unit({ held: [heldClose({ why: 'submission-moved' })] })} />);
    expect(screen.getByRole('region', { name: 'Where this work stands' })).toHaveTextContent(
      'close, attempt 1: The submission this close names was reopened or replaced since, so the work now is not this close\'s to record or stop; it can be sent again.');
    unmount();

    await i18n.changeLanguage('zh');
    render(<WorkMilestone defaultOpen unit={unit({ held: [heldClose({ why: 'submission-moved' })] })} />);
    expect(screen.getByRole('region', { name: '这项工作进展到哪里' }))
      .toHaveTextContent('close，第 1 次尝试：这次关闭针对的交付此后已被重新打开或替换，现在的工作不能由这次关闭来记录或停止，只能再发送一次。');
  });

  it('offers a close whose submission moved the one press the driver names, resend alone (FABRIC-CLOSE1q3)', async () => {
    const row = heldRow(heldClose({ why: 'submission-moved', words: ['resend'] }));
    expect(attentionActs(row)).toEqual(['resend-close']);
    const words: string[] = [];
    render(<ul><AttentionRow item={row!} acts={{ held: (_, resolution) => words.push(resolution) }} /></ul>);
    expect(screen.queryByRole('button', { name: 'Record the answer again' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Stop the rest of this work' })).toBeNull();
    await userEvent.click(screen.getByRole('button', { name: 'Ask for the answer again' }));
    expect(words).toEqual(['resend']);
  });

  it('takes its node’s place on the work’s foot, in English and in 中文', async () => {
    const { unmount } = render(<WorkMilestone defaultOpen unit={unit({ held: [heldAttempt({ why: 'no-reconcile' })] })} />);
    const region = screen.getByRole('region', { name: 'Where this work stands' });
    expect(within(region).getByText('Held for you')).toBeInTheDocument();
    expect(region).toHaveTextContent('deploy, attempt 2: Its step declares no way to ask what became of it. It waits for your word.');
    unmount();

    await i18n.changeLanguage('zh');
    render(<WorkMilestone defaultOpen unit={unit({ held: [heldAttempt({ why: 'acme-refused' })] })} />);
    const zh = screen.getByRole('region', { name: '这项工作进展到哪里' });
    expect(within(zh).getByText('等你定夺')).toBeInTheDocument();
    expect(zh).toHaveTextContent('deploy，第 2 次尝试：它的插件的答复与它所针对的工作不符（acme-refused）。它在等你的答复。');
  });

  it('shows nothing held where the driver lists none, as on an install no plugin step governs', () => {
    render(<WorkMilestone defaultOpen unit={unit({ held: [] })} />);
    expect(screen.queryByText('Held for you')).not.toBeInTheDocument();
  });
});

// MILESTONEUX1 (the UI/UX design §3.7 and R4): where a piece of work stands is four independent facts, each marked by `Dot`'s
// tone and never as a sequence, folded to `MilestoneLine` behind a disclosure and opened in place; the line says *blocked* and
// *held* so nothing that waits on the person hides behind the fold.
describe('where a piece of work stands: four facts, folded to their line', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  const blocked = entry({ kind: 'tree', resource: 's-1', disposition: 'blocked', detail: 'a session still holds this tree' });
  const waiting = entry({ kind: 'end', resource: 'the end', disposition: 'pending', detail: 'the cycle waits' });
  const kept = entry({ kind: 'moved-aside', resource: 's-1-x', role: 'recovery', disposition: 'retained', detail: 'set aside', until: '2026-10-24T09:00:00Z' });
  const pending = (...waits: MilestoneEntry[]): MilestoneUnit['cleanup'] => ({ state: 'pending', waits, gone: [] });
  /** A fact's mark: the decorative dot beside its words, or null where it wears none. */
  const markOf = (label: string) => screen.getByText(label).parentElement!.querySelector('[aria-hidden="true"]');

  it('is folded to its line behind a disclosure, and the facts open in place', async () => {
    render(<WorkMilestone unit={unit({ recovery: [kept] })} />);
    const region = screen.getByRole('region', { name: 'Where this work stands' });
    const fold = within(region).getByRole('button', { name: /Where this work stands/ });
    expect(fold).toHaveAttribute('aria-expanded', 'false');
    expect(fold).toHaveTextContent('Ended · clean-up completed · recovery kept until');
    expect(within(region).queryByText('End')).toBeNull();
    expect(region).not.toHaveTextContent('Reached:');

    await userEvent.click(fold);
    expect(fold).toHaveAttribute('aria-expanded', 'true');
    expect(region.querySelector('dl')).not.toBeNull();
    expect(fold).toHaveAttribute('aria-controls', region.querySelector('dl')!.id);
    expect(region).toHaveTextContent('Reached: every commit of it is on main');
    // The line stays the disclosure's head, folded or open, and the press folds it again.
    expect(fold).toHaveTextContent('Ended · clean-up completed');
    await userEvent.click(fold);
    expect(fold).toHaveAttribute('aria-expanded', 'false');
    expect(within(region).queryByText('End')).toBeNull();
  });

  it('marks an end reached, a result kept, a clean-up completed and a recovery kept each in done’s hue, and a recovery with nothing kept not at all', () => {
    const { rerender } = render(<WorkMilestone defaultOpen unit={unit({ recovery: [kept] })} />);
    for (const label of ['End', 'Result', 'Clean-up', 'Recovery']) expect(markOf(label), label).toHaveClass('bg-st-done');

    // A clean-up completed with nothing kept for recovery is ordinary: no fact is done because another is (R4).
    rerender(<WorkMilestone defaultOpen unit={unit()} />);
    expect(markOf('Clean-up')).toHaveClass('bg-st-done');
    expect(screen.getByText('Recovery').parentElement).toHaveTextContent('Nothing kept for recovery');
    expect(markOf('Recovery')).toBeNull();
  });

  it('marks what waits neutral and what is blocked in the danger hue, each fact by its own value', () => {
    const { rerender } = render(<WorkMilestone defaultOpen unit={unit({
      end: { state: 'not-ended', said: 'nothing was handed on and the line does not hold it' }, result: [], cleanup: pending(waiting),
    })} />);
    expect(markOf('End')).toHaveClass('bg-ink-soft');
    expect(markOf('Result')).toHaveClass('bg-line');
    expect(markOf('Clean-up')).toHaveClass('bg-ink-soft');
    expect(markOf('Recovery')).toBeNull();

    rerender(<WorkMilestone defaultOpen unit={unit({ cleanup: pending(blocked, waiting) })} />);
    expect(markOf('Clean-up')).toHaveClass('bg-st-declined');
    expect(markOf('End')).toHaveClass('bg-st-done');
  });

  it('keeps an unknown end, a dropped one and a handed one each in its own word and mark, never reached', () => {
    const marks = (end: MilestoneUnit['end']) => {
      const { unmount } = render(<WorkMilestone defaultOpen unit={unit({ end })} />);
      const row = screen.getByText('End').parentElement!;
      const shown = { mark: row.querySelector('[aria-hidden="true"]')!.className, text: row.textContent ?? '' };
      unmount();
      return shown;
    };
    const unknown = marks({ state: 'unknown', said: 'no record names its unit' });
    const dropped = marks({ state: 'dropped', said: '`feature/x` was removed outside Daoris' });
    const handed = marks({ state: 'handed', said: 'it went to another session' });
    expect(unknown.text).toContain('Unknown: no record names its unit');
    expect(unknown.mark).toContain('bg-line');
    expect(dropped.text).toContain('Over without its result');
    expect(dropped.mark).toContain('bg-ink-faint');
    expect(handed.text).toContain('Not reached: its work was handed off');
    expect(handed.mark).toContain('bg-ink-soft');
    for (const one of [unknown, dropped, handed]) expect(one.mark).not.toContain('bg-st-done');
  });

  it('keeps a held attempt as its own row under the facts, its mark the danger hue until the person has worded it', () => {
    const attempt = { unit: 'u1', node: 'deploy', attempt: 2, plugin: 'acme.deploy', step: 'acme.deploy/deploy', nodeState: 'unknown', why: 'unknown', at: '2026-10-17T09:00:00Z', said: 'asked' };
    const { rerender } = render(<WorkMilestone defaultOpen unit={unit({ held: [attempt] })} />);
    expect(markOf('Held for you')).toHaveClass('bg-st-declined');
    expect(screen.getByText('Held for you').parentElement).toHaveTextContent('deploy, attempt 2');
    // Under the four facts, which stay together: the end, the result, the clean-up and the recovery.
    const labels = [...screen.getByRole('region').querySelectorAll('dt')].map((term) => term.textContent);
    expect(labels).toEqual(['End', 'Result', 'Clean-up', 'Recovery', 'Held for you']);
    rerender(<WorkMilestone defaultOpen unit={unit({ held: [{ ...attempt, resolution: 'did-not' }] })} />);
    expect(markOf('Held for you')).toHaveClass('bg-ink-soft');
  });

  it('says blocked and held on its line, so nothing that waits on the person hides behind the fold', async () => {
    const attempt = { unit: 'u1', node: 'deploy', attempt: 2, plugin: 'acme.deploy', step: 'acme.deploy/deploy', nodeState: 'unknown', why: 'unknown', at: '2026-10-17T09:00:00Z', said: 'asked' };
    const stuck = unit({ cleanup: pending(blocked, waiting), held: [attempt] });
    const { unmount } = render(<WorkMilestone unit={stuck} />);
    const fold = screen.getByRole('button', { name: /Where this work stands/ });
    expect(fold).toHaveAttribute('aria-expanded', 'false');
    expect(fold).toHaveTextContent('Ended · clean-up pending · blocked · held for you');
    expect(within(fold).getByText('blocked')).toHaveClass('text-ink-danger');
    expect(within(fold).getByText('held for you')).toHaveClass('text-ink-danger');
    unmount();
    // A clean-up that only waits, and nothing held, say neither.
    render(<MilestoneLine unit={unit({ cleanup: pending(waiting) })} />);
    expect(screen.getByText(/clean-up pending/)).not.toHaveTextContent(/blocked|held/);
    // OVERVIEWNOISE1b: a guard's block is said here, on the work's own line, and is no row of *What needs you*.
    expect(waitsOnPerson(stuck)).toEqual([]);
  });

  it('says blocked and held on the line of a branch row, in English and in 中文', async () => {
    const attempt = { unit: 'u1', node: 'deploy', attempt: 2, plugin: 'acme.deploy', step: 'acme.deploy/deploy', nodeState: 'unknown', why: 'unknown', at: '2026-10-17T09:00:00Z', said: 'asked' };
    const stuck = unit({ cleanup: pending(blocked), held: [attempt] });
    const { unmount } = render(<MilestoneLine unit={stuck} />);
    expect(screen.getByText(/Ended/)).toHaveTextContent('Ended · clean-up pending · blocked · held for you');
    unmount();

    await i18n.changeLanguage('zh');
    expect(i18n.t('work.milestone.line.blocked')).toBe('受阻');
    expect(i18n.t('work.milestone.line.held')).toBe('等你处理');
    render(<MilestoneLine unit={stuck} />);
    expect(screen.getByText(/已结束/)).toHaveTextContent('已结束 · 清理未完成 · 受阻 · 等你处理');
  });
});

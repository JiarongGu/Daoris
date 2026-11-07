import { afterEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen } from '@testing-library/react';
import i18n from '../../locales/i18n';
import type { GoAhead, Session } from '../../data/api';
import type { RuleProposal } from '../../settings/repository/AgentRules';
import { bellCount, needsListed, obligationOf, placeCounts } from '../attention/attention';
import type { PushedAttention } from '../attention/attentionPushed';
import { WaitStrip } from './WaitStrip';
import { waitObligation, waitObligations } from './waitObligations';
import { type Wait, waitsOf } from './waits';

// One key per obligation drives the foot, the bell and *Notifications*, so one wait is
// counted once in every place it shows. The rows below are the ones .NET pushes for the same waits (`NeedsAPerson`): a park on a
// go-ahead its session asked, or on its agent's ask to hand it over, is that one row and never a parked row beside it.

afterEach(async () => {
  cleanup();
  await i18n.changeLanguage('en');
});

const AT = '2026-09-21T11:00:00Z';
const session = (over: Partial<Session> = {}): Session => ({
  id: 's1a2b3c4', quest: '7a82cc', repository: 'engine', door: 'claude-code', state: 'awaiting-person', kind: 'driven',
  created: '2026-09-21T09:46:00Z', updated: '2026-09-21T11:56:00Z', note: 'I need go-ahead 2 before I write to production.', ...over,
});
const goAhead = (number: number, state: GoAhead['state']): GoAhead => ({
  number, kind: 'write', on: 'production', act: `act ${number}`, state,
  asked: [{ session: 's1a2b3c4', quest: '7a82cc', at: AT, why: 'The tile reads its target from it.' }],
});
const RULE: RuleProposal = {
  id: 'r1', state: 'waiting', action: 'add', scope: 'repository', name: 'engine', list: 'allow', rule: 'Bash(git branch -D:*)',
  why: 'Auto mode refused the branch delete you asked for.', session: 's1a2b3c4', proposed: AT,
};
const pushed = (kind: PushedAttention['kind'], id: string, over: Partial<PushedAttention> = {}): PushedAttention =>
  ({ kind, id, since: AT, session: 's1a2b3c4', ...over });

/** The foot's count, read from its region's name: one wait is said with no number. */
const footCount = () => {
  const name = screen.getByRole('region', { name: /waiting on you|等你处理/i }).getAttribute('aria-label') ?? '';
  return Number(/^(\d+)/.exec(name)?.[1] ?? 1);
};

/** Every place's count of the rows: *Notifications*' list, the bell with no press, and the places' sum. */
const counted = (rows: PushedAttention[]) => ({
  listed: needsListed(rows).length,
  bell: bellCount([], rows),
  places: Object.values(placeCounts(rows)).reduce((sum, each) => sum + each, 0),
});

describe('COMMS1j each wait of a session is the obligation What needs you keys it by', () => {
  it('keys every kind of wait as the row .NET pushes for it', () => {
    const on = { session: 's1a2b3c4', ask: '7c1e9a04b2d5', quest: '7a82cc' };
    const pairs: [Wait, PushedAttention][] = [
      [{ kind: 'question', below: false }, pushed('parked', 's1a2b3c4')],
      [{ kind: 'intake', ask: '7c1e9a04b2d5' }, pushed('intake', '7c1e9a04b2d5')],
      [{ kind: 'goAhead', number: 2 }, pushed('go-ahead', '7c1e9a04b2d5#2', { obligation: 'go-ahead:7c1e9a04b2d5/2' })],
      [{ kind: 'permission', request: 'q9' }, pushed('permission', 's1a2b3c4#q9')],
      [{ kind: 'gitAsk', id: 'g4' }, pushed('git-ask', 'g4')],
      [{ kind: 'answerAlone' }, pushed('answer', 's1a2b3c4')],
      [{ kind: 'rule', id: 'r1' }, pushed('rule', 'r1')],
      [{ kind: 'helper', id: 'h7' }, pushed('helper', 'h7')],
      [{ kind: 'handOver', id: 'o3' }, pushed('hand-over', 'o3')],
      [{ kind: 'accept' }, pushed('review', 's1a2b3c4')],
      [{ kind: 'rebase' }, pushed('review', 's1a2b3c4')],
      [{ kind: 'opinion' }, pushed('opinion', 's1a2b3c4')],
      [{ kind: 'review' }, pushed('set-up', '7a82cc')],
      [{ kind: 'discard' }, pushed('work', 's1a2b3c4')],
    ];
    for (const [wait, row] of pairs) expect([wait.kind, waitObligation(wait, on)]).toEqual([wait.kind, obligationOf(row)]);
  });

  it('counts a park on the go-ahead it asked once, its question being that go-ahead', () => {
    const facts = { session: session(), onResolve: vi.fn(), goAheads: [goAhead(1, 'approved'), goAhead(2, 'asked')], goAheadsOn: '7c1e9a04b2d5', onGoAhead: vi.fn() };
    expect(waitsOf(facts).map((wait) => wait.kind)).toEqual(['question', 'goAhead']);
    expect(waitObligations(facts, waitsOf(facts))).toEqual(['go-ahead:7c1e9a04b2d5/2']);
  });

  it('counts each go-ahead it asked as its own obligation, and the park as none beside them', () => {
    const facts = { session: session(), onResolve: vi.fn(), goAheads: [goAhead(1, 'asked'), goAhead(2, 'asked')], goAheadsOn: '7c1e9a04b2d5', onGoAhead: vi.fn() };
    expect(waitObligations(facts, waitsOf(facts))).toEqual(['go-ahead:7c1e9a04b2d5/1', 'go-ahead:7c1e9a04b2d5/2']);
  });

  it('counts a park on its agent\'s ask to hand it over once', () => {
    const facts = {
      session: session(), onResolve: vi.fn(),
      handOver: { ask: { id: 'o3', note: 'My context is full.', at: AT, state: 'waiting', code: null, agent: 'codex' }, onApprove: vi.fn() },
    };
    expect(waitObligations(facts, waitsOf(facts))).toEqual(['hand-over:o3']);
  });

  it('counts a park on nothing it asked as the park', () => {
    const facts = { session: session(), onResolve: vi.fn(), rules: { proposals: [RULE], onAnswer: vi.fn() } };
    expect(waitObligations(facts, waitsOf(facts))).toEqual(['parked:s1a2b3c4', 'rule:r1']);
  });
});

describe('COMMS1j one wait counted once on the foot, the bell and Notifications', () => {
  it('says one wait where a park waits on the go-ahead it asked, as the bell and What needs you count it', () => {
    render(
      <WaitStrip
        session={session()} onResolve={vi.fn()} goAheads={[goAhead(1, 'approved'), goAhead(2, 'asked')]} goAheadsOn="7c1e9a04b2d5"
        onGoAhead={vi.fn()} onTop={vi.fn()}
      />,
    );
    // .NET pushes the go-ahead's row alone: the park waits on it.
    const rows = [pushed('go-ahead', '7c1e9a04b2d5#2', { obligation: 'go-ahead:7c1e9a04b2d5/2' })];
    expect(screen.getByRole('region', { name: 'Waiting on you' })).toBeInTheDocument();
    expect(footCount()).toBe(1);
    expect(counted(rows)).toEqual({ listed: 1, bell: 1, places: 1 });
  });

  it('says two where the park and a rule it proposed wait, as the bell and What needs you count them, in Chinese too', async () => {
    const rows = [pushed('parked', 's1a2b3c4'), pushed('rule', 'r1')];
    const strip = <WaitStrip session={session()} onResolve={vi.fn()} rules={{ proposals: [RULE], onAnswer: vi.fn() }} onTop={vi.fn()} />;
    render(strip);
    expect(screen.getByRole('region', { name: '2 waiting on you' })).toBeInTheDocument();
    expect(counted(rows)).toEqual({ listed: 2, bell: 2, places: 2 });
    cleanup();

    await i18n.changeLanguage('zh');
    render(strip);
    expect(screen.getByRole('region', { name: '2 项等你处理' })).toBeInTheDocument();
    expect(footCount()).toBe(counted(rows).bell);
  });
});

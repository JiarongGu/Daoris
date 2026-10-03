import { describe, expect, it } from 'vitest';
import type { Consideration } from '../signals';
import type { WorkPlan, WorkPlanQuest } from '../work/pausing';
import { LISTED_ASK, LISTED_CONSIDERED, PAUSABLE_ASK, PAUSED_LISTED_ASK } from '../work/pausingFixtures';
import { whyItSits, type WorkItem, workTree } from './workTree';

// An ask's work as its page lists it (PAUSE1h, D132 §7.1): each quest with its sessions and the questions they asked, each
// question listed the same way under the quest whose session asked it, and why each sits.

/** The tree as ids, so a case reads as the shape it asserts. */
const shape = (items: readonly WorkItem[]): unknown[] => items.map((item) => ({
  quest: item.quest.quest,
  sessions: item.sessions.map((session) => session.session),
  ...(item.questions.length > 0 ? { questions: shape(item.questions) } : {}),
}));

const quest = (over: Partial<WorkPlanQuest> & Pick<WorkPlanQuest, 'quest'>): WorkPlanQuest => ({
  title: over.quest, to: 'engine', status: 'Open', joined: 'asked', by: null, pause: 'paused', pausedBy: null,
  key: `quest:${over.quest}`, abandon: 'decline', kept: null, machine: null, whileOpen: true, ...over,
});

const plan = (quests: WorkPlanQuest[], sessions: WorkPlan['sessions'] = []): WorkPlan => ({ ...PAUSABLE_ASK, quests, sessions });

describe('workTree', () => {
  it('lists each quest the ask asked with its sessions, and under it the questions its sessions asked', () => {
    expect(shape(workTree(LISTED_ASK))).toEqual([
      {
        quest: '9a8b7c', sessions: ['f41led00', 's1a2b3c4'],
        questions: [
          { quest: '0c1d2e', sessions: ['studio/t3amm8t0'] },
          { quest: '7f6e5d', sessions: ['a5w3r3d0'] },
        ],
      },
      { quest: '5e4f3d', sessions: ['w0rk1ng0'] },
      { quest: '2d3e4f', sessions: [] },
      { quest: '3c2b1a', sessions: ['st0pp3d0'] },
    ]);
  });

  it('leaves the intake out: the page names it in a section of its own', () => {
    const sessions = workTree(LISTED_ASK).flatMap(function all(item): string[] {
      return [...item.sessions.map((session) => session.session), ...item.questions.flatMap(all)];
    });
    expect(sessions).not.toContain('i9n8t7k6');
  });

  it('applies itself again to what a question adds: a question its own session asked sits under it', () => {
    const tree = workTree(plan(
      [quest({ quest: 'q1' }), quest({ quest: 'q2', joined: 'published', by: 's1' }), quest({ quest: 'q3', joined: 'published', by: 's2' })],
      [
        { ...PAUSABLE_ASK.sessions[0], session: 's1', quest: 'q1' },
        { ...PAUSABLE_ASK.sessions[0], session: 's2', quest: 'q2' },
      ],
    ));
    expect(shape(tree)).toEqual([{ quest: 'q1', sessions: ['s1'], questions: [{ quest: 'q2', sessions: ['s2'], questions: [{ quest: 'q3', sessions: [] }] }] }]);
  });

  it('finds a teammate’s session by its record’s key, which the feed writes as origin/id', () => {
    const tree = workTree(plan(
      [quest({ quest: 'q1' }), quest({ quest: 'q2', joined: 'published', by: 'abc' })],
      [{ ...PAUSABLE_ASK.sessions[0], session: 'studio/abc', quest: 'q1', teammate: true }],
    ));
    expect(shape(tree)).toEqual([{ quest: 'q1', sessions: ['studio/abc'], questions: [{ quest: 'q2', sessions: [] }] }]);
  });

  it('lists a question whose asker it cannot place at the top, so nothing of the work is hidden', () => {
    const tree = workTree(plan([quest({ quest: 'q1' }), quest({ quest: 'q2', joined: 'published', by: 'gone' })]));
    expect(shape(tree)).toEqual([{ quest: 'q1', sessions: [] }, { quest: 'q2', sessions: [] }]);
  });

  it('lists every quest once, even where the records ask in a circle', () => {
    const tree = workTree(plan(
      [quest({ quest: 'q1', joined: 'published', by: 's2' }), quest({ quest: 'q2', joined: 'published', by: 's1' })],
      [
        { ...PAUSABLE_ASK.sessions[0], session: 's1', quest: 'q1' },
        { ...PAUSABLE_ASK.sessions[0], session: 's2', quest: 'q2' },
      ],
    ));
    expect(shape(tree)).toEqual([{ quest: 'q1', sessions: ['s1'], questions: [{ quest: 'q2', sessions: ['s2'] }] }]);
  });

  it('lists nothing for a work with no quests', () => {
    expect(workTree(plan([]))).toEqual([]);
  });
});

describe('whyItSits', () => {
  const item = (id: string, from: WorkPlan = LISTED_ASK) => {
    const found = workTree(from).flatMap(function all(each): WorkItem[] { return [each, ...each.questions.flatMap(all)]; })
      .find((each) => each.quest.quest === id);
    if (!found) throw new Error(`no ${id} in the fixture`);
    return found;
  };
  const sitting = (id: string, considered: readonly Consideration[] = LISTED_CONSIDERED) =>
    considered.find((each) => each.quest === id) ?? null;

  it('says the driver’s sentence for a quest it holds', () => {
    expect(whyItSits(item('2d3e4f'), sitting('2d3e4f'), 'a1b2c3')).toEqual({ sitting: sitting('2d3e4f') });
  });

  it('says a wait as its question below, where its question is listed under it', () => {
    expect(whyItSits(item('9a8b7c'), sitting('9a8b7c'), 'a1b2c3')).toEqual({ line: { key: 'asks.work.waits' } });
  });

  it('keeps the driver’s sentence for a wait whose question it does not list', () => {
    const lone = { ...item('9a8b7c'), questions: [] };
    expect(whyItSits(lone, sitting('9a8b7c'), 'a1b2c3')).toEqual({ sitting: sitting('9a8b7c') });
  });

  it('says a quest’s own pause, which resuming the ask does not release', () => {
    expect(whyItSits(item('3c2b1a'), sitting('3c2b1a'), 'a1b2c3')).toEqual({ line: { key: 'asks.work.pausedOwn' } });
  });

  it('says this ask’s pause briefly, since its header says it and offers Resume', () => {
    const paused = { quest: '2d3e4f', repository: 'game', verdict: 'Paused', reason: 'paused with ask `#a1b2c3`; …', pausedBy: { scope: 'ask' as const, id: 'a1b2c3' } };
    expect(whyItSits(item('2d3e4f', PAUSED_LISTED_ASK), paused, 'a1b2c3')).toEqual({ line: { key: 'asks.work.pausedHere' } });
  });

  it('says a pause from the plan where the tick has no verdict, since its session runs or waits on you', () => {
    expect(whyItSits(item('5e4f3d', PAUSED_LISTED_ASK), null, 'a1b2c3')).toEqual({ line: { key: 'asks.work.pausedHere' } });
  });

  it('names another ask’s or another quest’s pause by its id', () => {
    const other = { ...item('2d3e4f'), quest: { ...item('2d3e4f').quest, pausedBy: { scope: 'ask' as const, id: 'f00d00' } } };
    expect(whyItSits(other, null, 'a1b2c3')).toEqual({ line: { key: 'asks.work.pausedAsk', values: { id: 'f00d00' } } });
    const asker = { ...item('2d3e4f'), quest: { ...item('2d3e4f').quest, pausedBy: { scope: 'quest' as const, id: '9a8b7c' } } };
    expect(whyItSits(asker, null, 'a1b2c3')).toEqual({ line: { key: 'asks.work.pausedQuest', values: { id: '9a8b7c' } } });
  });

  it('says a quest taken on another machine is out of a pause’s reach, by the machine where it is named', () => {
    expect(whyItSits(item('0c1d2e'), null, 'a1b2c3')).toEqual({ line: { key: 'asks.work.elsewhere', values: { machine: 'studio-pc' } } });
    const unnamed = { ...item('0c1d2e'), quest: { ...item('0c1d2e').quest, machine: null } };
    expect(whyItSits(unnamed, null, 'a1b2c3')).toEqual({ line: { key: 'asks.work.elsewhereUnnamed' } });
    const outside = { ...item('0c1d2e'), quest: { ...item('0c1d2e').quest, machine: null, kept: 'taken-outside' as const } };
    expect(whyItSits(outside, null, 'a1b2c3')).toEqual({ line: { key: 'asks.work.outside' } });
  });

  it('says so of a quest taken elsewhere even while the ask is paused, since the pause does not reach it', () => {
    expect(whyItSits(item('0c1d2e', PAUSED_LISTED_ASK), null, 'a1b2c3'))
      .toEqual({ line: { key: 'asks.work.elsewhere', values: { machine: 'studio-pc' } } });
  });

  it('says nothing of a closed quest, nor of one the driver is starting or says nothing about', () => {
    const paused = { quest: '7f6e5d', repository: 'game', verdict: 'Paused', reason: '…', pausedBy: { scope: 'ask' as const, id: 'a1b2c3' } };
    expect(whyItSits(item('7f6e5d', PAUSED_LISTED_ASK), paused, 'a1b2c3')).toBeNull();
    const starting = { quest: '2d3e4f', repository: 'game', verdict: 'Start', reason: 'starting in `game`.' };
    expect(whyItSits(item('2d3e4f'), starting, 'a1b2c3')).toBeNull();
    expect(whyItSits(item('5e4f3d'), null, 'a1b2c3')).toBeNull();
  });
});

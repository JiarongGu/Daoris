import { describe, expect, it } from 'vitest';
import i18n from '../i18n';
import {
  clearList, clearOffered, clearSaid, historyPayload, historyPlanOf, keptDoor, readingSaid, reasonSaid,
} from './history';
import {
  ASK_PLAN, FAILED_NONE, FAILED_PLAN, QUEST_ASKED, QUEST_CLEARED, QUEST_PLAN, QUEST_TREE_HERE, WORKSPACE_CLEARED,
  WORKSPACE_EMPTY, WORKSPACE_KEPT, WORKSPACE_LEFT_OVER, WORKSPACE_PLAN,
} from './historyFixtures';

// What a clear lists, sends and says on the screen (HIST1e, D153; the history-clearing design §2.4, §5, §6.1): pure, so every
// plan the driver could answer is an argument, and the quest's page, the ask's page and the workspace's read one rule.

const en = i18n.getFixedT('en');
const zh = i18n.getFixedT('zh');

describe('what a route is asked (design §6.3)', () => {
  it('names one workspace, one quest, one quest with its failed sessions, or one ask', () => {
    expect(historyPayload({ scope: 'workspace', id: 'aurora' })).toEqual({ workspace: 'aurora' });
    expect(historyPayload({ scope: 'quest', id: '9a8b7c' })).toEqual({ quest: '9a8b7c' });
    expect(historyPayload({ scope: 'failed', id: '9a8b7c' })).toEqual({ quest: '9a8b7c', failed: true });
    expect(historyPayload({ scope: 'ask', id: 'a1b2c3' })).toEqual({ ask: 'a1b2c3' });
  });
});

describe('a plan as the driver answers it', () => {
  it('is read only where it has a plan’s shape, and its unit’s lists and sizes default where the bridge left them out', () => {
    expect(historyPlanOf({ drivable: [], holds: [], running: [] })).toBeNull();
    expect(historyPlanOf(null)).toBeNull();
    const plan = historyPlanOf({ scope: 'quest', id: 'q1', units: [{ kind: 'quest', id: 'q1', clearable: true, quests: ['q1'] }] });
    expect(plan?.units[0]).toMatchObject({ sessions: [], teammates: [], forgotten: [], asks: [], kept: [], keep: null });
    expect(plan?.units[0]?.bytes.total).toBe(0);
    expect(plan?.reading).toBeNull();
  });
});

describe('what the first press lists, and the second sends (design §5)', () => {
  it('sends exactly the units the plan says may go, and lists every unit kept with its reason', () => {
    const list = clearList(WORKSPACE_PLAN);
    expect(list.units).toEqual([{ kind: 'ask', id: 'a1b2c3' }, { kind: 'quest', id: '0c1d2e' }, { kind: 'quest', id: '3f4a5b' }]);
    expect(list.staying.map((each) => each.id)).toEqual(['6c7d8e', 'b2c3d4', '9f0a1b', '2b3c4d', '5d6e7f']);
    expect(list).toMatchObject({ quests: 4, asks: 1, sessions: 6, teammates: 1, forgotten: 1, room: 0 });
    expect(list.leftOver).toEqual({ count: 4, bytes: WORKSPACE_PLAN.reading!.leftOver.bytes });
    expect(list.bytes).toBe(WORKSPACE_PLAN.reading!.takes.bytes);
    expect(list.takes).toBe(true);
  });

  it('takes left-over files and the intake’s room with no unit, which the press sends as an empty list', () => {
    const list = clearList(WORKSPACE_LEFT_OVER);
    expect(list.units).toEqual([]);
    expect(list.room).toBe(40 * 1024);
    expect(list.takes).toBe(true);
  });

  it('keeps a teammate’s failed session listed beside the two that go', () => {
    const list = clearList(FAILED_PLAN);
    expect(list.units).toEqual([{ kind: 'failed', id: '9a8b7c' }]);
    expect(list.sessions).toBe(2);
    expect(list.kept).toEqual([{ code: 'HISTORY_NOT_OURS', session: 'laptop/f9e8d7c6', machine: 'laptop' }]);
  });
});

describe('where a clear is offered (design §6.1)', () => {
  it('is offered where the plan says something may go, and nowhere it lists nothing', () => {
    expect(clearOffered(QUEST_PLAN)).toBe(true);
    expect(clearOffered(ASK_PLAN)).toBe(true);
    expect(clearOffered(FAILED_PLAN)).toBe(true);
    expect(clearOffered(WORKSPACE_PLAN)).toBe(true);
    expect(clearOffered(WORKSPACE_LEFT_OVER)).toBe(true);
    // Kept, or nothing to take: absent, never disabled (D119 §3.2).
    expect(clearOffered(QUEST_ASKED)).toBe(false);
    expect(clearOffered(QUEST_TREE_HERE)).toBe(false);
    expect(clearOffered(FAILED_NONE)).toBe(false);
    expect(clearOffered(WORKSPACE_KEPT)).toBe(false);
    expect(clearOffered(WORKSPACE_EMPTY)).toBe(false);
    expect(clearOffered({ scope: 'quest', id: 'q1', units: [] })).toBe(false);
    expect(clearOffered(null)).toBe(false);
  });
});

describe('why a unit stays, in the reader’s language (design §1.2)', () => {
  it('says the catalogue’s sentence for its code and the variant its context names, in each language', () => {
    expect(reasonSaid(en, { code: 'HISTORY_NEEDS_YOU', context: 'ask', ask: 'b2c3d4' }))
      .toBe('Ask #b2c3d4 is waiting for you to publish or close it, so it was not cleared.');
    expect(reasonSaid(zh, { code: 'HISTORY_NEEDS_YOU', context: 'ask', ask: 'b2c3d4' })).toBe('需求 #b2c3d4 正等你发布或关闭，所以没有清除。');
    expect(reasonSaid(en, { code: 'HISTORY_LANDING_STANDS', branch: 'feature/x', repository: 'engine' }))
      .toBe('The branch feature/x a landing made still stands in engine, so it was not cleared. Clean it up once it has merged.');
    // A newer service's word is said as the driver passed it on, verbatim.
    expect(reasonSaid(en, { code: 'DRIVER_REFUSED', message: 'A newer reason.' })).toBe('A newer reason.');
  });

  it('opens the door that frees it, where one exists', () => {
    expect(keptDoor({ code: 'HISTORY_LANDING_STANDS', session: 's1' })).toEqual({ to: 'branches' });
    expect(keptDoor({ code: 'HISTORY_TREE_HERE', session: 's1' })).toEqual({ to: 'branches' });
    expect(keptDoor({ code: 'HISTORY_UNPUSHED', workspace: 'aurora' })).toEqual({ to: 'sync' });
    expect(keptDoor({ code: 'HISTORY_LIVE', session: 's1' })).toEqual({ to: 'session', id: 's1' });
    expect(keptDoor({ code: 'HISTORY_NEEDS_YOU', session: 's1' })).toEqual({ to: 'session', id: 's1' });
    expect(keptDoor({ code: 'HISTORY_NEEDS_YOU', context: 'held', quest: 'q1' })).toEqual({ to: 'quest', id: 'q1' });
    expect(keptDoor({ code: 'HISTORY_NEEDS_YOU', context: 'conflict', quest: 'q1' })).toEqual({ to: 'quest', id: 'q1' });
    expect(keptDoor({ code: 'HISTORY_NEEDS_YOU', context: 'ask', ask: 'a1' })).toEqual({ to: 'ask', id: 'a1' });
    expect(keptDoor({ code: 'HISTORY_AWAITED', quest: 'q2' })).toEqual({ to: 'quest', id: 'q2' });
    expect(keptDoor({ code: 'HISTORY_ASKED', quest: 'q1', ask: 'a1' })).toEqual({ to: 'ask', id: 'a1' });
    // A teammate's record, or one still running on their machine, has no door here.
    expect(keptDoor({ code: 'HISTORY_LIVE', context: 'teammate', session: 'laptop/s1', machine: 'laptop' })).toBeNull();
    expect(keptDoor({ code: 'HISTORY_NOT_OURS', session: 'laptop/s1', machine: 'laptop' })).toBeNull();
  });
});

describe('the reading of what the home keeps (design §2.4)', () => {
  it('says what is kept, what a clear would take, what keeps the rest by reason, and the home’s own', () => {
    const said = readingSaid(en, WORKSPACE_PLAN.reading!);
    expect(said.holds).toBe('11 closed quests, 2 asks, 14 sessions, 1 copy of a teammate’s record: 23.6 MB.');
    expect(said.takes).toBe('A clear would take 4 closed quests, 1 ask, 6 sessions, 1 copy of a teammate’s record: 20.6 MB.');
    // In the order a person meets them (design §1.2), each with the door the workspace's page holds.
    expect(said.kept.map((each) => [each.code, each.text, each.door])).toEqual([
      ['HISTORY_LIVE', '1 kept: a session is still running', null],
      ['HISTORY_NEEDS_YOU', '2 kept: waiting on you', null],
      ['HISTORY_LANDING_STANDS', '1 kept: a landing’s branch still stands', { to: 'branches' }],
      ['HISTORY_UNPUSHED', '1 kept: its last moves have not reached the remote', { to: 'sync' }],
    ]);
    expect(said.conversations).toBe('3 conversations that served no quest, 1.2 MB: only Delete… in Sessions takes them, one at a time.');
    expect(said.leftOver).toBe('3.1 MB on this machine is left over from records already gone; every workspace’s clear takes it.');
    expect(said.log).toBe('The machine log holds 2.4 MB and keeps its own 30 days; a clear never touches it.');
  });

  it('says nothing finished is kept, and that a clear would take nothing, in 中文 too', () => {
    const said = readingSaid(en, WORKSPACE_EMPTY.reading!);
    expect(said.holds).toBe('Nothing finished is kept here.');
    expect(said.takes).toBe('A clear would take nothing now.');
    expect(said.conversations).toBeNull();
    expect(said.leftOver).toBeNull();
    expect(readingSaid(zh, WORKSPACE_KEPT.reading!).takes).toBe('现在清除不会带走任何东西。');
  });
});

describe('what the second press says (design §5, §6.1)', () => {
  it('says a quest cleared from this machine', () => {
    expect(clearSaid(en, QUEST_CLEARED)).toEqual([{ text: 'Cleared `#9a8b7c` from this machine.', tone: 'ok' }]);
    expect(clearSaid(zh, QUEST_CLEARED)).toEqual([{ text: '已从本机清除 `#9a8b7c`。', tone: 'ok' }]);
  });

  it('says how many of the listed units went, and how many changed since the list and were kept', () => {
    expect(clearSaid(en, WORKSPACE_CLEARED)).toEqual([{
      text: 'Cleared 2 of 3 from aurora, 14.6 MB. 1 changed since the list and was kept.', tone: 'ok',
    }]);
  });

  it('says a press that sent no unit took what was left over, and a file the disk kept', () => {
    expect(clearSaid(en, { ...WORKSPACE_CLEARED, listed: 0, cleared: [], changed: [], bytes: 3 * 1024 * 1024, failed: 1 })).toEqual([
      { text: 'Cleared what was left over on this machine, 3 MB.', tone: 'ok' },
      { text: '1 file could not be removed; the next clear of a workspace takes it.', tone: 'error' },
    ]);
  });

  it('says a quest’s failed sessions by how many went', () => {
    expect(clearSaid(en, { ...QUEST_CLEARED, scope: 'failed', cleared: [{ kind: 'failed', id: '9a8b7c' }], quests: 0, sessions: 2 }))
      .toEqual([{ text: 'Cleared 2 failed sessions of `#9a8b7c` from this machine.', tone: 'ok' }]);
  });
});

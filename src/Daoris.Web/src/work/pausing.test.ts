import { describe, expect, it } from 'vitest';
import {
  abandonList, abandonNotice, type AbandonAnswer, type AbandonEntry, askOf, outcomeOf, pauseAsk, type PauseAnswer,
  pauseNotices, pieceName, resumeNotices, type ResumeAnswer, wiredFor, workOffers, type WorkPlan,
} from './pausing';

// What a pause and an abandon say on the screen (PAUSE1e, D132, design §2.6, §3.1, §3.2, §4.2, §7.1): pure, so every plan
// the driver could answer is an argument, and the ask's page, the quest's page and a session's header read one rule.

const plan = (over: Partial<WorkPlan> = {}): WorkPlan => ({
  scope: 'ask', id: 'a1', pausable: true, paused: null,
  quests: [], sessions: [], trees: [], landings: [],
  abandon: { abandonable: false, pieces: [], closes: null, abandoned: null },
  ...over,
});

const quest = (id: string, over: Partial<WorkPlan['quests'][number]> = {}): WorkPlan['quests'][number] => ({
  quest: id, title: `The work of #${id}`, to: 'engine', status: 'Open', joined: 'asked', by: null,
  pause: 'paused', pausedBy: null, key: `quest:${id}`, abandon: 'decline', kept: null, machine: null, whileOpen: true,
  ...over,
});

const session = (id: string, over: Partial<WorkPlan['sessions'][number]> = {}): WorkPlan['sessions'][number] => ({
  session: id, repository: 'engine', state: 'working', quest: 'q1', intake: false, teammate: false, branch: null,
  pause: 'stopped', key: `session:${id}`, abandon: 'stop', archive: true, kept: null, machine: null,
  ...over,
});

describe('the ask a quest was asked by', () => {
  it('reads it from the sender the service writes for an ask, and nothing from a repository', () => {
    expect(askOf({ from: 'ask #a1b2c3' })).toBe('a1b2c3');
    expect(askOf({ from: 'game' })).toBeNull();
    expect(askOf(null)).toBeNull();
  });
});

describe('whether another machine may take an open quest (design §5.1)', () => {
  it('is a workspace the machine wires, the default when unstated, or every one where the environment names the remote', () => {
    const wiring = { fromEnvironment: false, remotes: [{ workspace: 'aurora' }, { workspace: 'default' }] };
    expect(wiredFor(wiring, 'aurora')).toBe(true);
    expect(wiredFor(wiring, undefined)).toBe(true);
    expect(wiredFor({ ...wiring, remotes: [{ workspace: 'aurora' }] }, 'lantern')).toBe(false);
    expect(wiredFor({ fromEnvironment: true, remotes: [] }, 'lantern')).toBe(true);
    expect(wiredFor(undefined, 'aurora')).toBe(false);
  });
});

describe('what the header offers (design §7.1)', () => {
  it('offers Pause… while something would be held and it is not paused, and Abandon… while the reader takes anything', () => {
    expect(workOffers(plan({ abandon: { abandonable: true, pieces: ['quest:q1'] } })))
      .toEqual({ pause: true, resume: false, abandon: true, paused: false });
  });

  it('offers Resume, and no Pause…, once paused here', () => {
    const paused = plan({ paused: { at: '2026-10-03T09:00:00Z', stopped: [] } });
    expect(workOffers(paused)).toEqual({ pause: false, resume: true, abandon: false, paused: true });
  });

  it('offers nothing where the driver has not answered: the acts are absent, never guessed', () => {
    expect(workOffers(null)).toEqual({ pause: false, resume: false, abandon: false, paused: false });
  });
});

describe('what a pause asks before it ends work in flight (design §2.6)', () => {
  it('applies at once where it stops nothing: there is no ask', () => {
    expect(pauseAsk(plan({ sessions: [session('s1', { pause: 'parked', state: 'awaiting-person' })] }), { wired: false })).toBeNull();
  });

  it('says how many sessions it stops, that their trees keep what they wrote, and nothing more on an unwired workspace', () => {
    const asked = pauseAsk(plan({ quests: [quest('q1'), quest('q2')], sessions: [session('s1'), session('s2'), session('s3', { pause: 'ended' })] }), { wired: false });
    expect(asked).toEqual([{ key: 'work.pause.ask.stops', values: { count: 2 } }]);
  });

  it('names each open quest another machine may still take, on a wired workspace, and the intake that keeps reading', () => {
    const asked = pauseAsk(plan({
      quests: [quest('q1', { status: 'Taken' }), quest('q2'), quest('q3', { pause: 'closed', status: 'Done' })],
      sessions: [session('s1'), session('i1', { intake: true, quest: null, pause: 'intake' })],
    }), { wired: true });
    expect(asked).toEqual([
      { key: 'work.pause.ask.stops', values: { count: 1 } },
      { key: 'work.pause.ask.elsewhere', values: { quests: '#q2' } },
      { key: 'work.pause.ask.intake' },
    ]);
  });
});

describe('what the abandon lists on its first press (design §3.1, §3.2)', () => {
  const listed = plan({
    quests: [
      quest('q1'),
      quest('q2', { status: 'Taken', whileOpen: false }),
      quest('q3', { status: 'Taken', abandon: 'keep', kept: 'taken-elsewhere', machine: 'laptop' }),
      quest('q4', { status: 'Taken', abandon: 'keep', kept: 'taken-elsewhere' }),
      quest('q5', { status: 'Done', abandon: 'keep', kept: 'done' }),
      quest('q6', { status: 'Declined', abandon: 'none' }),
    ],
    sessions: [
      session('s1', { quest: 'q2' }),
      session('s2', { quest: 'q2', abandon: 'stop', archive: false, kept: 'review' }),
      session('s3', { state: 'awaiting-person', abandon: 'end' }),
      session('s4', { state: 'failed', abandon: 'archive' }),
      session('i1', { intake: true, quest: null, abandon: 'stop' }),
      session('laptop/s5', { teammate: true, abandon: 'keep', kept: 'teammate', machine: 'laptop' }),
    ],
    trees: [
      {
        repository: 'engine', branch: 'daoris/s-1a2b3c4d', sessions: ['s1'], key: 'tree:engine:daoris/s-1a2b3c4d',
        abandon: 'discard', kept: null, gone: false, tip: '9f3e2a1', commits: 3, uncommitted: 7,
        files: ['a.ts', 'b.ts', 'c.ts', 'd.ts', 'e.ts'], where: null,
      },
      {
        repository: 'engine', branch: 'daoris/s-5e6f7a8b', sessions: ['s4'], key: 'tree:engine:daoris/s-5e6f7a8b',
        abandon: 'delete', kept: null, gone: true, tip: '1b2c3d4', commits: 1, uncommitted: null, files: [], where: null,
      },
      {
        repository: 'game', branch: 'daoris/s-0a0b0c0d', sessions: ['s2'], key: 'tree:game:daoris/s-0a0b0c0d',
        abandon: 'keep', kept: 'elsewhere', gone: false, tip: 'abc1234', commits: 2, uncommitted: 0, files: [], where: 'main',
      },
    ],
    abandon: { abandonable: true, pieces: [], closes: 'a1', abandoned: null },
  });

  it('lists every piece it takes with what it does, in the order the second press takes them', () => {
    const { goes } = abandonList(listed);
    expect(goes.map((row) => [row.piece, row.act.key])).toEqual([
      ['quest:q1', 'work.abandon.act.declineOpen'],
      ['quest:q2', 'work.abandon.act.declineTaken'],
      ['session:s1', 'work.abandon.act.stop'],
      ['session:s2', 'work.abandon.act.stopReview'],
      ['session:s3', 'work.abandon.act.end'],
      ['session:s4', 'work.abandon.act.archive'],
      ['session:i1', 'work.abandon.act.stop'],
      ['tree:engine:daoris/s-1a2b3c4d', 'work.abandon.act.discard'],
      ['tree:engine:daoris/s-5e6f7a8b', 'work.abandon.act.delete'],
      ['ask:a1', 'work.abandon.act.close'],
    ]);
  });

  it('says what a discarded tree holds, naming up to five uncommitted files and never a path', () => {
    const tree = abandonList(listed).goes.find((row) => row.piece.startsWith('tree:engine:daoris/s-1a2b3c4d'))!;
    expect(tree.holds).toEqual([
      { key: 'work.abandon.holds.commits', values: { count: 3 } },
      { key: 'work.abandon.holds.uncommitted', values: { count: 7, files: 'a.ts, b.ts, c.ts, d.ts, e.ts, …' } },
    ]);
  });

  it('names each piece it keeps with its reason, in the words of §3.2', () => {
    const { stays } = abandonList(listed);
    expect(stays.map((row) => [row.piece, row.why.key, row.why.values ?? {}])).toEqual([
      ['quest:q3', 'work.abandon.why.takenElsewhere', { machine: 'laptop' }],
      ['quest:q4', 'work.abandon.why.takenElsewhereUnnamed', {}],
      ['quest:q5', 'work.abandon.why.done', {}],
      ['session:laptop/s5', 'work.abandon.why.teammate', { machine: 'laptop' }],
      ['tree:game:daoris/s-0a0b0c0d', 'work.abandon.why.elsewhere', { where: 'main' }],
    ]);
  });

  it('names a piece by what a person reads: a quest with its title, a session with its quest, a tree by repository and branch', () => {
    const { goes } = abandonList(listed);
    expect(goes[0].name).toEqual({ key: 'work.abandon.piece.quest', values: { quest: 'q1', title: 'The work of #q1' } });
    expect(goes[2].name).toEqual({ key: 'work.abandon.piece.sessionOn', values: { session: 's1', quest: 'q2' } });
    expect(goes[6].name).toEqual({ key: 'work.abandon.piece.intake', values: { session: 'i1' } });
    expect(goes[7].name).toEqual({ key: 'work.abandon.piece.tree', values: { repository: 'engine', branch: 'daoris/s-1a2b3c4d' } });
    expect(goes[9].name).toEqual({ key: 'work.abandon.piece.ask', values: { ask: 'a1' } });
  });
});

describe('a piece named by its key alone (the answer and the record carry keys)', () => {
  it('reads each kind of key, a branch with its slash included', () => {
    expect(pieceName('quest:q1')).toEqual({ key: 'work.abandon.piece.questId', values: { quest: 'q1' } });
    expect(pieceName('session:laptop/s5')).toEqual({ key: 'work.abandon.piece.session', values: { session: 'laptop/s5' } });
    expect(pieceName('tree:engine:daoris/s-1a2b3c4d'))
      .toEqual({ key: 'work.abandon.piece.tree', values: { repository: 'engine', branch: 'daoris/s-1a2b3c4d' } });
    expect(pieceName('ask:a1')).toEqual({ key: 'work.abandon.piece.ask', values: { ask: 'a1' } });
    // A kind this page does not know is shown as the driver wrote it.
    expect(pieceName('landing:x')).toEqual({ key: 'work.abandon.piece.raw', values: { piece: 'landing:x' } });
  });
});

describe('what went and what stayed (design §4.2)', () => {
  const answer: AbandonAnswer = {
    scope: 'ask', id: 'a1', did: 'abandoned', listed: 6, went: 4,
    changed: [{ piece: 'tree:engine:daoris/s-9', why: 'landed', changed: true }],
    failed: [{ piece: 'quest:q9', why: 'refused', detail: 'Quest `#q9` is already Done.' }],
    joined: ['quest:q8'],
    declined: ['q1', 'q2'], closed: true,
    stopped: [{ session: 's1', quest: 'q2' }],
    discarded: [{ repository: 'engine', branch: 'daoris/s-1a2b3c4d', tip: '9f3e2a1', commits: 3, uncommitted: 7, sessions: ['s1'], alone: false }],
    archived: ['s1', 's4'],
    stayed: [{ piece: 'quest:q5', why: 'done' }],
    declines: [{ quest: 'q1', answer: 'confirmed' }, { quest: 'q2', answer: 'lost' }],
    stillPaused: true,
  };

  it('says from the answer what went, with each discarded branch’s way back while git keeps its commits', () => {
    const outcome = outcomeOf(answer);
    expect(outcome.went.map((line) => line.key)).toEqual([
      'work.abandon.went.declined', 'work.abandon.went.closed', 'work.abandon.went.stopped', 'work.abandon.went.discarded',
      'work.abandon.went.archived', 'work.abandon.declines.confirmed', 'work.abandon.declines.lost',
    ]);
    expect(outcome.went[3].values).toEqual({ repository: 'engine', branch: 'daoris/s-1a2b3c4d', tip: '9f3e2a1' });
  });

  it('says what stayed: what the list kept, what changed since, what a step could not take, what joined, and the pause left', () => {
    const outcome = outcomeOf(answer);
    expect(outcome.stayed.map((row) => [row.name.key, row.why.key])).toEqual([
      ['work.abandon.piece.questId', 'work.abandon.why.done'],
      ['work.abandon.piece.tree', 'work.abandon.why.landedChanged'],
      ['work.abandon.piece.questId', 'work.abandon.why.refused'],
      ['work.abandon.piece.questId', 'work.abandon.why.joined'],
    ]);
    expect(outcome.stayed[2].why.values).toEqual({ detail: 'Quest `#q9` is already Done.' });
    expect(outcome.stillPaused).toBe(true);
  });

  it('says the same from the record, which keeps fixed words and no detail, for a later look', () => {
    const entry: AbandonEntry = {
      at: '2026-10-03T10:00:00Z', door: 'terminal', reason: 'It went the wrong way.', declined: ['q1'], closed: false,
      trees: [{ repository: 'engine', branch: 'daoris/s-5e6f7a8b', tip: '1b2c3d4', commits: 1, uncommitted: 0, sessions: [], alone: true }],
      stopped: ['s1'], archived: [], stayed: [{ piece: 'session:s2', why: 'review' }], declines: [],
    };
    const outcome = outcomeOf(entry);
    expect(outcome.went.map((line) => line.key)).toEqual([
      'work.abandon.went.declined', 'work.abandon.went.stopped', 'work.abandon.went.deleted',
    ]);
    expect(outcome.stayed.map((row) => row.why.key)).toEqual(['work.abandon.why.review']);
    expect(outcome.stillPaused).toBe(false);
  });
});

describe('the notices', () => {
  it('says a pause in one line, and names each session it could not stop as its own failure', () => {
    const paused: PauseAnswer = {
      scope: 'ask', id: 'a1', did: 'paused', already: false,
      stopped: [{ session: 's1', quest: 'q1' }, { session: 's2', quest: 'q2' }],
      kept: [{ session: 's3', quest: 'q3', why: 'parked' }, { session: 's4', quest: 'q4', why: 'elsewhere' }],
    };
    expect(pauseNotices(paused)).toEqual([
      { key: 'work.pause.done.stopped', values: { count: 2 }, tone: 'ok' },
      { key: 'work.pause.unreached', values: { session: 's4' }, tone: 'error' },
    ]);
    expect(pauseNotices({ ...paused, stopped: [], kept: [] })).toEqual([{ key: 'work.pause.done.nothingStopped', values: {}, tone: 'ok' }]);
    expect(pauseNotices({ ...paused, already: true, stopped: [], kept: [] })).toEqual([{ key: 'work.pause.done.already', values: {}, tone: 'ok' }]);
    expect(pauseNotices({ ...paused, did: 'nothing', stopped: [], kept: [] })).toEqual([{ key: 'work.pause.nothing', values: {}, tone: 'ok' }]);
  });

  it('says a resume, and what still holds a quest of it, by the hold’s own sentence', () => {
    const resumed: ResumeAnswer = { scope: 'quest', id: 'q1', did: 'resumed', released: [{ quest: 'q1', session: 's1' }], holds: [] };
    expect(resumeNotices(resumed)).toEqual([{ key: 'work.pause.resumed', values: {}, tone: 'ok' }]);
    const held = { ...resumed, holds: [{ quest: 'q1', verdict: 'Held', reason: 'the receiver is held on this machine by you.' }] };
    expect(resumeNotices(held)).toEqual([
      { key: 'work.pause.resumed', values: {}, tone: 'ok' },
      { key: 'work.pause.stillSits', values: { quest: 'q1' }, hold: held.holds[0], tone: 'ok' },
    ]);
    const many = { ...resumed, holds: [held.holds[0], { ...held.holds[0], quest: 'q2' }] };
    expect(resumeNotices(many)[1]).toEqual({ key: 'work.pause.stillSitMany', values: { count: 2 }, tone: 'ok' });
    expect(resumeNotices({ ...resumed, did: 'not-paused', released: [] })).toEqual([{ key: 'work.pause.notPaused', values: {}, tone: 'ok' }]);
  });

  /**
   * CARRY2d: the hold a resume names carries the facts its sentence is said from (`heldBy`, `pausedBy`, `strikes`,
   * `takenBy`, the tick's own), and the notice hands them on whole, so the act says it in the reader's language.
   */
  it('hands on the facts a hold is said from with its sentence', () => {
    const stopped: ResumeAnswer = {
      scope: 'ask', id: 'a1', did: 'resumed', released: [],
      holds: [{ quest: 'q8', verdict: 'Stopped', reason: 'you stopped session `b3f0re00`.', heldBy: 'b3f0re00' }],
    };
    const taken: ResumeAnswer = {
      ...stopped,
      holds: [{ quest: 'q11', verdict: 'TakenElsewhere', reason: 'Quest `#q11` is taken on another machine.', takenBy: { machine: null, session: null, here: false, last: 'cut0ff00' } }],
    };
    expect(resumeNotices(stopped)[1].hold).toEqual(stopped.holds![0]);
    expect(resumeNotices(taken)[1].hold?.takenBy).toEqual({ machine: null, session: null, here: false, last: 'cut0ff00' });
  });

  it('says an abandon as how many of the listed pieces went, and how many changed since the list', () => {
    const went = { scope: 'ask', id: 'a1', did: 'abandoned', listed: 10, went: 9, changed: [{ piece: 'quest:q1', why: 'gone', changed: true }] } as AbandonAnswer;
    expect(abandonNotice(went)).toMatchObject({ key: 'work.abandon.done.changed', values: { went: 9, count: 10, changed: 1 } });
    expect(abandonNotice({ ...went, went: 10, changed: [] })).toEqual({ key: 'work.abandon.done.all', values: { went: 10, count: 10 }, tone: 'ok' });
    expect(abandonNotice({ ...went, did: 'nothing' })).toEqual({ key: 'work.abandon.nothing', values: {}, tone: 'ok' });
  });

  /**
   * PAUSE1h: a partial abandon was said in the `ok` tone, the shape HIST1n found in a partial clear (D153's HIST1n note).
   * Only an abandon that took every piece it listed is a plain success; one that kept some, because they changed since the
   * list or the disk would not let them go (the driver's `went` is what was listed less both), says so in the error's tone.
   */
  it('says a partial abandon in the error’s tone, never as a plain success', () => {
    const partial = { scope: 'ask', id: 'a1', did: 'abandoned', listed: 10, went: 9, changed: [{ piece: 'quest:q1', why: 'gone', changed: true }] } as AbandonAnswer;
    expect(abandonNotice(partial).tone).not.toBe('ok');
    expect(abandonNotice(partial)).toEqual({ key: 'work.abandon.done.changed', values: { went: 9, count: 10, changed: 1 }, tone: 'error' });
    // One the disk kept changed nothing since the list, and still did not all go.
    const failed: AbandonAnswer = { ...partial, changed: [], failed: [{ piece: 'tree:s1', why: 'in-use' }] };
    expect(abandonNotice(failed)).toEqual({ key: 'work.abandon.done.all', values: { went: 9, count: 10 }, tone: 'error' });
  });
});

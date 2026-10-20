import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { afterEach, describe, expect, it } from 'vitest';
import type { QuestMove } from '../data/api';
import { QUEST_TONE } from '../design/ui';
import i18n from '../locales/i18n';
import { en, zh } from '../locales';
import { moment } from '../locales/format';
import { DECLINED, DONE, OPEN, TAKEN } from './fixtures';
import { QUEST_MOVES, QUEST_STATUSES, questFlow } from './questLife';

// FLOWCHART1a1 (the flowchart design §2.1, §7 traps 8, 11 and 19): a quest's four statuses and the moves between them,
// drawn on its page. The moves are a page constant, so this holds them to the code's own table, `QuestTransitions.Allows`,
// read from the service's source (`.claude/knowledge/twins.md`): a second copy no test holds drifts.

const root = join(process.cwd(), '..', '..');
const core = 'src/Daoris.Service/Daoris.Service.Core';
const found = readdirSync(join(root, ...core.split('/')), { recursive: true, encoding: 'utf8' }).filter((path) => /(^|[\\/])QuestLog\.cs$/.test(path));
if (found.length !== 1) throw new Error(`QuestLog.cs is expected once under ${core}, found ${found.length}`);
const source = readFileSync(join(root, ...core.split('/'), found[0]!), 'utf8');

/**
 * The pairs `QuestTransitions.Allows` allows, read from its switch: each arm names its targets (`QuestStatus.Done or
 * QuestStatus.Declined =>`) and the statuses it allows them from (`from == QuestStatus.Open`, `from is QuestStatus.Open or
 * QuestStatus.Taken`); the discard arm allows nothing.
 */
function allowed(text: string): [string, string][] {
  const body = /public static bool Allows\(QuestStatus from, QuestStatus to\) => to switch\s*\{([\s\S]*?)\};/.exec(text)?.[1] ?? '';
  return body.split('\n').flatMap((line) => {
    const arm = /^\s*(.+?)\s*=>\s*(.+?),?\s*$/.exec(line);
    if (!arm || arm[1]!.trim() === '_') return [];
    const targets = [...arm[1]!.matchAll(/QuestStatus\.(\w+)/g)].map((match) => match[1]!);
    const froms = [...arm[2]!.matchAll(/QuestStatus\.(\w+)/g)].map((match) => match[1]!);
    return targets.flatMap((to) => froms.map((from) => [from, to] as [string, string]));
  });
}

afterEach(async () => { await i18n.changeLanguage('en'); });

describe('a quest\'s moves, held to the code (trap 11)', () => {
  it('reads the arms of QuestTransitions.Allows: a scan that matched nothing would pass on a moved table', () => {
    expect(allowed(source).length).toBeGreaterThan(0);
  });

  it('draws exactly the moves the code allows, and every status the code has', () => {
    const sort = (pairs: readonly (readonly [string, string])[]) => pairs.map(([from, to]) => `${from}>${to}`).sort();
    expect(sort(QUEST_MOVES)).toEqual(sort(allowed(source)));
    // The enum's members, one a line between their comments, in Quests.cs beside QuestLog.cs.
    const quests = readFileSync(join(root, ...core.split('/'), found[0]!.replace(/QuestLog\.cs$/, 'Quests.cs')), 'utf8');
    const block = /public enum QuestStatus\s*\{([^}]*)\}/.exec(quests)?.[1] ?? '';
    const members = block.split('\n').flatMap((line) => /^\s*(\w+)\s*,?\s*$/.exec(line)?.[1] ?? []);
    expect(members).toEqual([...QUEST_STATUSES]);
  });

  it('words every move in both languages', () => {
    for (const [from, to] of QUEST_MOVES) {
      expect(en[`quests.flow.move.${from}.${to}`], `${from} → ${to}`).toBeTruthy();
      expect(zh[`quests.flow.move.${from}.${to}`], `${from} → ${to}`).toBeTruthy();
    }
  });
});

describe('the flow a quest\'s page draws (§2.1)', () => {
  const t = i18n.t.bind(i18n);

  it('lights the status it stands at, in its own tone, and is entered at Open', () => {
    const flow = questFlow(t, TAKEN);
    expect(flow.entries).toEqual(['Open']);
    expect(flow.nodes.filter((node) => node.current).map((node) => node.id)).toEqual(['Taken']);
    expect(flow.nodes.find((node) => node.id === 'Taken')!.state).toEqual({ word: 'here now', tone: 'taken' });
    expect(flow.label).toBe(`How #${TAKEN.id} moves`);
  });

  it('says each move by its cause, and draws no label the foot does not need', () => {
    const flow = questFlow(t, OPEN);
    expect(flow.edges.map((edge) => [edge.from, edge.to, edge.says])).toEqual([
      ['Open', 'Taken', 'when a repository\'s agent takes it'],
      ['Open', 'Done', 'when it is marked done before anyone takes it'],
      ['Open', 'Declined', 'when it is declined before anyone takes it'],
      ['Taken', 'Done', 'when its work is done'],
      ['Taken', 'Declined', 'when it is declined after it was taken'],
    ]);
    expect(flow.edges.every((edge) => edge.label === undefined)).toBe(true);
  });

  it('claims only what the record keeps: what is still to come is not reached, and a closed quest says nothing of a take (trap 19)', () => {
    const reached = (quest: typeof OPEN) => Object.fromEntries(questFlow(t, quest).nodes.map((node) => [node.id, node.reached]));
    expect(reached(OPEN)).toEqual({ Open: true, Taken: false, Done: false, Declined: false });
    expect(reached(TAKEN)).toEqual({ Open: true, Taken: true, Done: false, Declined: false });
    // A done quest may have been taken or not: without its moves (FLOWCHART1a2) the page cannot say, so it says neither.
    expect(reached(DONE)).toEqual({ Open: true, Taken: undefined, Done: true, Declined: false });
    expect(reached(DECLINED)).toEqual({ Open: true, Taken: undefined, Done: false, Declined: true });
  });

  it('says a submission where the status is taken, as the quest\'s own pill does', () => {
    const flow = questFlow(t, { ...TAKEN, submission: { machine: 'm1', sequence: 3, at: '2026-10-20T14:02:00Z' } });
    expect(flow.nodes.find((node) => node.id === 'Taken')!.state).toEqual({ word: 'submitted', tone: 'taken' });
  });

  it('words its chrome in 中文, the statuses by the catalogue\'s names', async () => {
    await i18n.changeLanguage('zh');
    const flow = questFlow(t, TAKEN);
    expect(flow.label).toBe(`委托 #${TAKEN.id} 如何流转`);
    expect(flow.nodes.map((node) => node.title)).toEqual(['待接', '已接下', '已完成', '已谢绝']);
    expect(flow.nodes.find((node) => node.id === 'Taken')!.state!.word).toBe('当前所在');
  });
});

describe('the way a quest went, drawn from its moves (FLOWCHART1a2; §2.1, traps 12, 18 and 20)', () => {
  const t = i18n.t.bind(i18n);
  const at = (minute: number) => `2026-10-20T09:${String(minute).padStart(2, '0')}:00Z`;
  const move = (kind: string, machine: string, sequence: number, minute: number, rest: Partial<QuestMove> = {}): QuestMove =>
    ({ kind, machine, sequence, at: at(minute), applied: true, ...rest });
  const PUBLISHED = move('published', 'm2', 1, 0, { number: 1 });
  const TOOK = move('taken', 'm3', 1, 5, { number: 2, from: 'Open' });
  const CLOSED = move('done', 'm3', 2, 30, { number: 3, from: 'Taken' });
  const taken = (flow: ReturnType<typeof questFlow>) => flow.edges.filter((edge) => edge.taken).map((edge) => edge.id);
  const reached = (flow: ReturnType<typeof questFlow>) => Object.fromEntries(flow.nodes.map((node) => [node.id, node.reached]));
  const node = (flow: ReturnType<typeof questFlow>, id: string) => flow.nodes.find((each) => each.id === id)!;
  const texts = (flow: ReturnType<typeof questFlow>, id: 'Open' | 'Taken' | 'Done' | 'Declined') => flow.lines[id].map((line) => line.text);

  it('draws the way it went: each move on the path heavier, the statuses it reached, and who made each move into them', () => {
    const flow = questFlow(t, DONE, [PUBLISHED, TOOK, CLOSED]);

    expect(taken(flow)).toEqual(['Open>Taken', 'Taken>Done']);
    expect(reached(flow)).toEqual({ Open: true, Taken: true, Done: true, Declined: false });
    expect(flow.edges.find((edge) => edge.id === 'Taken>Done')!.says).toBe('when its work is done; it went this way');
    expect(flow.edges.find((edge) => edge.id === 'Open>Done')!.says).toBe('when it is marked done before anyone takes it');
    expect(node(flow, 'Open').actor).toBe(`published — machine m2, ${moment(at(0))}`);
    // A take names the machine and never a session: it keeps none (§2.1).
    expect(node(flow, 'Taken').actor).toBe(`taken — machine m3, ${moment(at(5))}`);
  });

  it('draws a history from before the log as recorded: its Open → Done, and no take filled in', () => {
    const flow = questFlow(t, DONE, [PUBLISHED, move('done', 'm2', 2, 30, { number: 2, from: 'Open' })]);

    expect(taken(flow)).toEqual(['Open>Done']);
    expect(reached(flow)).toEqual({ Open: true, Taken: false, Done: true, Declined: false });
  });

  it('lists what changed no status inside the status it happened in, in the order they replay and never by time (trap 20)', () => {
    const flow = questFlow(t, { ...TAKEN, here: 'laptop' }, [
      PUBLISHED, TOOK,
      move('conflict', 'm4', 7, 6, { number: 3, from: 'Taken', attempted: 'Taken' }),
      move('dismissed', 'm3', 2, 20, { number: 4, from: 'Taken', dismisses: { machine: 'm4', sequence: 7 } }),
      // This machine's own wait, still pending, made on a clock behind the remote's: it comes last all the same.
      move('waited', 'laptop', 9, 1, { from: 'Taken', waitsOn: 'q2q2q2' }),
    ]);

    expect(texts(flow, 'Taken')).toEqual([
      `a move to taken that reached the remote second, kept as a conflict — machine m4, ${moment(at(6))}`,
      `dismissed the conflict: machine m4's move to taken — machine m3, ${moment(at(20))}`,
      `waited on #q2q2q2, whose close resumes it — this machine, ${moment(at(1))}`,
    ]);
    expect(flow.lines.Taken.map((line) => line.refused ?? false)).toEqual([true, false, false]);
    // A take that lost to a take attempted the status it happened in: said inside it, never drawn as an edge into itself.
    expect(flow.edges.filter((edge) => edge.style === 'refused')).toEqual([]);
    expect(flow.lines.Open).toEqual([]);
  });

  it('draws a move the replay did not apply inside the status it happened in, never on the path (trap 20)', () => {
    const flow = questFlow(t, DONE, [PUBLISHED, TOOK, CLOSED, move('done', 'm5', 1, 31, { number: 4, from: 'Done', applied: false })]);

    expect(flow.lines.Done).toEqual([
      { id: 'm5:1', text: `marked done — machine m5, ${moment(at(31))} (recorded, not applied)`, notApplied: true },
    ]);
    expect(taken(flow)).toEqual(['Open>Taken', 'Taken>Done']);
  });

  it('draws a conflict that attempted another status as a refused edge toward it, until a dismissal names it by its reference', () => {
    const declined = [
      PUBLISHED, TOOK, move('declined', 'm3', 2, 30, { number: 3, from: 'Taken' }),
      move('conflict', 'm4', 7, 40, { number: 4, from: 'Declined', attempted: 'Done' }),
      move('conflict', 'm5', 2, 41, { number: 5, from: 'Declined', attempted: 'Done' }),
    ];
    const refused = (flow: ReturnType<typeof questFlow>) =>
      flow.edges.filter((edge) => edge.style === 'refused').map((edge) => [edge.from, edge.to, edge.says]);

    expect(refused(questFlow(t, DECLINED, declined))).toEqual([
      ['Declined', 'Done', "machine m4's move to done, kept as a conflict"],
      ['Declined', 'Done', "machine m5's move to done, kept as a conflict"],
    ]);
    // Dismissing m4's settles m4's alone: the dismissal names its conflict by the machine and sequence it keeps.
    const settled = questFlow(t, DECLINED, [
      ...declined, move('dismissed', 'm3', 3, 50, { number: 6, from: 'Declined', dismisses: { machine: 'm4', sequence: 7 } }),
    ]);
    expect(refused(settled)).toEqual([['Declined', 'Done', "machine m5's move to done, kept as a conflict"]]);
    expect(texts(settled, 'Declined').at(-1)).toBe(`dismissed the conflict: machine m4's move to done — machine m3, ${moment(at(50))}`);
  });

  it('says a held Done in open\'s hue, as the quest\'s own pill does, and a hold set or let go on the move that did it (§2.1)', () => {
    const held = questFlow(t, { ...DONE, hold: 'departed' }, [PUBLISHED, TOOK, { ...CLOSED, hold: 'departed' }]);
    expect(node(held, 'Done').state).toEqual({ word: 'awaits your yes', tone: QUEST_TONE.Open });
    expect(node(held, 'Done').actor).toBe(`marked done — machine m3, ${moment(at(30))} · awaits your yes`);
    expect(node(questFlow(t, { ...DONE, hold: 'unreviewed' }), 'Done').state).toEqual({ word: 'awaits review', tone: QUEST_TONE.Open });
    // Without moves too: the hold is the quest's own.
    expect(node(questFlow(t, DONE), 'Done').state).toEqual({ word: 'here now', tone: QUEST_TONE.Done });

    const accepted = questFlow(t, DONE, [PUBLISHED, TOOK, { ...CLOSED, hold: 'departed' }, move('accepted', 'm1', 1, 35, { from: 'Done', hold: 'none' })]);
    expect(texts(accepted, 'Done')).toEqual([`your yes to what held it — machine m1, ${moment(at(35))} · let go what held it`]);
  });

  it('says what the record does not keep, and shows a kind it does not know as recorded', () => {
    const flow = questFlow(t, TAKEN, [
      PUBLISHED, TOOK,
      move('submitted', 'm3', 2, 10, { from: 'Taken' }),
      move('waited', 'm3', 3, 11, { from: 'Taken' }),
      move('conflict', 'm4', 1, 12, { from: 'Taken' }),
      move('rewound', 'm6', 1, 13, { from: 'Taken', applied: false }),
    ]);

    expect(texts(flow, 'Taken')).toEqual([
      `submitted; its session is not recorded — machine m3, ${moment(at(10))}`,
      `waited on a quest not recorded — machine m3, ${moment(at(11))}`,
      `a move that reached the remote second, kept as a conflict; what it attempted is not recorded — machine m4, ${moment(at(12))}`,
      `\`rewound\`, shown as recorded — machine m6, ${moment(at(13))} (recorded, not applied)`,
    ]);
  });

  it('draws the statuses alone where its moves are not kept (trap 18): nothing on the path and nothing listed', () => {
    for (const none of [null, []]) {
      const flow = questFlow(t, DONE, none);
      expect(taken(flow)).toEqual([]);
      expect(Object.values(flow.lines).flat()).toEqual([]);
      expect(reached(flow).Taken).toBeUndefined();
    }
  });

  it('words every kind of quest operation the service has, in both languages (trap 12)', () => {
    const block = /public enum QuestOperationKind\s*\{([\s\S]*?)\n\}/.exec(source)?.[1] ?? '';
    const kinds = block.split('\n').flatMap((line) => /^\s{4}(\w+),?\s*$/.exec(line)?.[1] ?? []).map((kind) => kind.toLowerCase());
    expect(kinds.length).toBeGreaterThan(10);
    for (const kind of kinds) {
      expect(en[`quests.flow.kind.${kind}`], kind).toBeTruthy();
      expect(zh[`quests.flow.kind.${kind}`], kind).toBeTruthy();
    }
  });

  it('says the way it went in 中文', async () => {
    await i18n.changeLanguage('zh');
    const flow = questFlow(t, { ...TAKEN, here: 'laptop' }, [PUBLISHED, TOOK, move('waited', 'laptop', 9, 1, { from: 'Taken', waitsOn: 'q2q2q2' })]);

    expect(flow.edges.find((edge) => edge.id === 'Open>Taken')!.says).toBe('当某个仓库的智能体接下它时；它走的正是这一步');
    expect(node(flow, 'Taken').actor).toBe(`接下——机器 m3，${moment(at(5))}`);
    expect(texts(flow, 'Taken')).toEqual([`等待 #q2q2q2，它关闭后继续——本机，${moment(at(1))}`]);
  });
});

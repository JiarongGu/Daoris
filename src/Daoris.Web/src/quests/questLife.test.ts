import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { afterEach, describe, expect, it } from 'vitest';
import i18n from '../locales/i18n';
import { en, zh } from '../locales';
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

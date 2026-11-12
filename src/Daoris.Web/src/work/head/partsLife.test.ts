import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { SESSION_TONE } from '../../design/ui';
import i18n from '../../locales/i18n';
import { en, zh } from '../../locales';
import { driverSource } from '../../test/driverSource';
import { EVIDENCE_ENTRIES, EVIDENCE_SESSIONS } from '../entries.fixtures';
import type { EntryPart, SessionEntry } from '../entries';
import { PART_BEGAN, partsFlow } from './partsLife';

// A line of work's parts drawn as lineage. A part keeps no start,
// no end and no agent, and its order is the ledger's, so the drawing hangs each part by `after` and says nothing of when a part ran
// or whether two ran at once.

const t = i18n.t.bind(i18n);
const APP = EVIDENCE_ENTRIES[0]!;
const rows = Object.fromEntries(EVIDENCE_SESSIONS.map((session) => [session.id, { session, root: 'C:/work/app' }]));

const part = (session: string, began: string, over: Partial<EntryPart> = {}): EntryPart => ({
  session, quest: 'q0000a', shown: 'completed', group: 'done', began, after: null, askedBy: null, ...over,
});
const entry = (parts: EntryPart[], over: Partial<SessionEntry> = {}): SessionEntry => ({
  entry: 'quest:q0000a/app', origin: 'quest:q0000a', repository: 'app', group: 'done', leading: parts.at(-1)!.session, parts, ...over,
});
const flowOf = (e: SessionEntry, attended = e.leading, held: typeof rows = {}) =>
  partsFlow(t, e, { attended, rows: held });
const shape = (flow: ReturnType<typeof partsFlow>) => flow.edges.map((edge) => `${edge.from}>${edge.to}`);

beforeEach(() => {
  vi.useFakeTimers();
  vi.setSystemTime(new Date('2026-10-20T12:00:00Z'));
});
afterEach(async () => {
  vi.useRealTimers();
  await i18n.changeLanguage('en');
});

describe('the parts, hung by what each follows (§2.3)', () => {
  it('draws each part as a node oldest first, with the start and the repository that asked as nodes of their own', () => {
    const flow = flowOf(APP);

    expect(flow.nodes.map((node) => [node.id, node.title])).toEqual([
      ['start', 'Start'], ['ap000001', 'Part 1'], ['ap000002', 'Part 2'], ['asked:db', 'asked by db'], ['ap000003', 'Part 3'],
    ]);
    expect(flow.entries).toEqual(['start', 'asked:db']);
    expect(flow.label).toBe('How this line of work\'s parts follow one another');
  });

  it('hangs each part from what it followed and labels the edge by how it began, in the strip\'s own words', () => {
    const flow = flowOf(APP);

    expect(flow.edges.map((edge) => [edge.id, edge.label, edge.says])).toEqual([
      ['start>ap000001', 'started', 'started'],
      ['ap000001>ap000002', 'retried', 'retried after part 1'],
      ['asked:db>ap000003', 'asked', 'asked by db'],
    ]);
    // An asked part hangs from a faint node outside the entry, which does not claim it is never reached.
    const asked = flow.nodes.find((node) => node.id === 'asked:db')!;
    expect(asked.faint).toBe(true);
    expect(asked.reached).toBeUndefined();
  });

  it('says every way a part began, held to the driver\'s words in both languages', () => {
    // The driver's `PartBegan`, read from its source (`.claude/knowledge/twins.md`): the page's list is a copy no test would hold else.
    const source = driverSource('SessionEntries.cs');
    expect(source).toMatch(/static class PartBegan\b/);
    const block = /static class PartBegan\s*\{([\s\S]*?)\n\}/.exec(source)?.[1] ?? '';
    const words = [...block.matchAll(/public const string \w+ = "([^"]+)";/g)].map((match) => match[1]!);
    expect(words.length).toBeGreaterThan(4);
    expect([...PART_BEGAN].sort()).toEqual([...words].sort());

    for (const began of words) {
      expect(en[`work.parts.edge.${began}`], began).toBeTruthy();
      expect(zh[`work.parts.edge.${began}`], began).toBeTruthy();
    }
  });

  it('labels a part moved on from one whose conversation was lost by its own word (SESSCTX1b)', () => {
    const flow = flowOf(entry([part('a1', 'opened'), part('b2', 'moved', { after: 'a1' })]));

    expect(flow.edges.map((edge) => [edge.id, edge.label, edge.says])).toEqual([
      ['start>a1', 'started', 'started'],
      ['a1>b2', 'moved on', 'moved on from part 1: its own conversation was lost'],
    ]);
  });

  it('draws parts with no `after` between them as branches from the start: lineage, never a chain or a concurrency (trap 19)', () => {
    // Listed oldest first, and a part that began apart from the others: none follows another, and none is said to run beside one.
    const flow = flowOf(entry([part('a1', 'opened'), part('b2', 'opened'), part('c3', 'retried', { after: 'a1' })]));

    expect(shape(flow)).toEqual(['start>a1', 'start>b2', 'a1>c3']);
    const words = JSON.stringify([flow.nodes.map((node) => [node.title, node.actor]), flow.edges.map((edge) => [edge.says, edge.label])]);
    expect(words).not.toMatch(/then|while|same time|at once|parallel|concurrent|together|next/i);
  });

  it('hangs a part carried on from a chain\'s parent, which keeps no `after`, from the start, as the strip says it', () => {
    const flow = flowOf(entry([part('a1', 'carried-on'), part('b2', 'handed-over', { after: 'a1' })]));

    expect(flow.edges.map((edge) => [edge.from, edge.to, edge.label, edge.says])).toEqual([
      ['start', 'a1', 'carried on', 'carried on'],
      ['a1', 'b2', 'handed over', 'handed over from part 1'],
    ]);
  });

  it('hangs a part whose earlier part the page does not hold from the start, and says which it followed by its id', () => {
    const flow = flowOf(entry([part('b2', 'retried', { after: 'gone0001' })]));

    expect(flow.edges.map((edge) => [edge.from, edge.says])).toEqual([['start', 'retried after gone0001']]);
  });

  it('shows a way of beginning it does not know as recorded, hung by what it follows (trap 10)', () => {
    const flow = flowOf(entry([part('a1', 'opened'), part('b2', 'cloned', { after: 'a1' })]));

    expect(flow.edges.map((edge) => [edge.from, edge.to, edge.label, edge.says])).toEqual([
      ['start', 'a1', 'started', 'started'],
      ['a1', 'b2', 'cloned', '`cloned`, shown as recorded'],
    ]);
  });

  it('draws no start where every part hangs from something else', () => {
    const flow = flowOf(entry([part('a1', 'asked', { askedBy: 'db' }), part('b2', 'resumed', { after: 'a1' })]));

    expect(flow.nodes.map((node) => node.id)).toEqual(['asked:db', 'a1', 'b2']);
    expect(flow.entries).toEqual(['asked:db']);
  });

  it('names a request whose repository is not said, as the strip does', () => {
    const flow = flowOf(entry([part('a1', 'asked')]));

    expect(flow.nodes.map((node) => node.title)).toEqual(['asked by another session', 'Part 1']);
  });
});

describe('what a part says of itself (§2.3)', () => {
  it('lights the part the page is on, and says which part leads the entry\'s row', () => {
    const flow = flowOf(APP, 'ap000002');

    expect(flow.nodes.filter((node) => node.current).map((node) => node.id)).toEqual(['ap000002']);
    expect(flow.lines.ap000003).toContain('shown by its row in the list');
    expect(flow.lines.ap000001).not.toContain('shown by its row in the list');
  });

  it('says its word in the tone the list wears it, from the page\'s record where it holds one, else as the driver said it', () => {
    const held = flowOf(APP, 'ap000003', rows);
    expect(held.nodes.find((node) => node.id === 'ap000001')!.state).toEqual({ word: 'failed', tone: SESSION_TONE.failed });
    expect(held.nodes.find((node) => node.id === 'ap000003')!.state).toEqual({ word: 'working', tone: SESSION_TONE.working });

    const bare = flowOf(entry([part('a1', 'opened', { shown: 'parked' }), part('b2', 'retried', { shown: 'paused', after: 'a1' })]));
    expect(bare.nodes.find((node) => node.id === 'a1')!.state).toEqual({ word: 'parked', tone: 'open' });
    expect(bare.nodes.find((node) => node.id === 'b2')!.state).toEqual({ word: 'paused', tone: 'neutral' });
  });

  it('says its quest, and the agent and when it began only where the page holds its record (a part keeps neither)', () => {
    const flow = flowOf(APP, 'ap000003', { ap000002: rows.ap000002! });

    expect(flow.nodes.find((node) => node.id === 'ap000002')!.actor).toMatch(/^claude-code, began /);
    expect(flow.nodes.find((node) => node.id === 'ap000001')!.actor).toBeUndefined();
    expect(flow.lines.ap000001).toContain('#q0000a');
    expect(JSON.stringify(flow.edges)).not.toMatch(/ago|began \d/);
  });

  it('says where a part\'s work is where the page holds its record, as the strip does', () => {
    const held = {
      ...rows,
      ap000002: { ...rows.ap000002!, where: { treeGone: true, landed: { repository: 'app', branch: 'daoris/t-one', state: 'standing' as const } } },
    };
    const flow = flowOf(APP, 'ap000003', held);

    expect(flow.lines.ap000002).toContain('landed on daoris/t-one');
    expect(flow.lines.ap000001).toContain('in t-one');
    expect(flow.lines.ap000003).toContain('in t-two');
  });
});

describe('no landing among the parts (§2.3, trap 19)', () => {
  it('draws no landing node: the Accept is the review page’s', () => {
    const flow = flowOf(APP, 'ap000003', rows);
    expect(flow.nodes.some((node) => node.id === 'landing')).toBe(false);
    expect(flow.edges.some((edge) => edge.to === 'landing')).toBe(false);
  });
});

describe('the parts in 中文', () => {
  it('says them as the strip says them, and the start', async () => {
    await i18n.changeLanguage('zh');
    const flow = flowOf(APP, 'ap000002', rows);

    expect(flow.nodes.map((node) => node.title)).toEqual(['起点', '第 1 段', '第 2 段', '由 db 委托', '第 3 段']);
    expect(flow.edges.map((edge) => [edge.label ?? null, edge.says])).toEqual([
      ['开始', '开始'], ['重试', '接第 1 段重试'], ['委托', '由 db 委托'],
    ]);
    expect(flow.label).toBe('这条工作线的各段如何接续');
  });
});

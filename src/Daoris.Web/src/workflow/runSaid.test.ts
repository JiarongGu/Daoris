import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import i18n from '../locales/i18n';
import type { GraphRun, RunStep } from './run';
import { bindingProblemSaid } from './place/effective';
import { addedSaid, chatChecksSaid, graphSaid, noRunSaid, runSaid, runShort, stateSaid } from './runSaid';
import { MERGED, WORKING } from './runFixtures';

// The English a run says is one table with the driver (`.claude/knowledge/twins.md`), its
// tests' `fixtures/workflow-run-said.json`, which `WorkflowRunSaidTwinTests` holds `WorkflowRunWords` to. Each row is said here
// through the real English catalogue, word for word, so a sentence changed on one side alone fails the other's suite.

type Row = Record<string, string | number | null> & { kind: string; detail: string; said: string | null };
const TABLE = JSON.parse(readFileSync(
  join(process.cwd(), '..', 'Daoris.Desktop', 'Daoris.Desktop.Driver.Tests', 'fixtures', 'workflow-run-said.json'), 'utf8')) as {
  states: Record<string, string>;
  added: Record<string, string>;
  rows: Row[];
  chatChecks: { checks: string; said: string };
  none: { code: string; id?: string; workflow?: string; said: string }[];
  graph: (Partial<Pick<GraphRun, 'quest' | 'branch' | 'judged' | 'unread' | 'ended'>> & { said: string[] })[];
};

const t = i18n.getFixedT('en');

const stepOf = (row: Row): RunStep => ({
  step: { id: row.kind, kind: row.kind, participation: 'agent', executor: 'agent', press: null, limit: null, source: { rule: null, level: 'default' }, settings: {}, runtime: 'built' },
  state: (row.state as string | undefined) ?? 'working',
  detail: row.detail,
  quest: row.quest as string | undefined,
  agent: row.agent as string | undefined,
  environment: row.environment as string | undefined,
  commit: row.commit as string | undefined,
  branch: row.branch as string | undefined,
  plugin: row.plugin as string | undefined,
  count: row.count as number | undefined,
  goAhead: row.goAhead as number | undefined,
  words: row.words as string | undefined,
  code: row.code as string | undefined,
  node: row.node as string | undefined,
  on: row.on as string | undefined,
} as RunStep);

/** A graph run's own part as the table's `graph` rows give it. */
const graphOf = (row: TABLE_GRAPH): GraphRun => ({
  run: 'q2', entries: ['claimed'], end: 'handed', wires: [],
  quest: row.quest ?? null, branch: row.branch ?? null, judged: row.judged ?? false, unread: row.unread ?? false, ended: row.ended ?? null,
});
type TABLE_GRAPH = (typeof TABLE.graph)[number];

describe("a run's English, as the driver's table holds it", () => {
  it.each(TABLE.rows.map((row, index) => [`${index} ${row.kind}.${row.detail || '(none)'} ${row.state ?? ''} ${row.code ?? ''}`, row] as const))(
    'says row %s in the table’s words',
    (_name, row) => {
      expect(runSaid(t, stepOf(row))).toBe(row.said);
    },
  );

  // What a graph run says above its nodes, line for line, as the driver's `WorkflowRunWords.GraphSaid` says it.
  it.each(TABLE.graph.map((row, index) => [index, row] as const))('says graph row %s in the table’s words', (_index, row) => {
    expect(graphSaid(t, graphOf(row))).toEqual(row.said);
  });

  // A chat run's checks, not yet enforced for a conversation, as the driver's `ChatChecks` says them.
  it("says a chat run's checks not yet enforced in the table's words, and in Chinese", () => {
    const chat = { ...WORKING, chat: { run: 'chat-c1', checks: TABLE.chatChecks.checks, enforced: false } };
    expect(chatChecksSaid(t, chat)).toBe(TABLE.chatChecks.said);
    expect(chatChecksSaid(i18n.getFixedT('zh'), chat)).toMatch(/[一-鿿]/);
    expect(chatChecksSaid(t, { ...chat, chat: { ...chat.chat, enforced: true } })).toBeNull();
    expect(chatChecksSaid(t, WORKING)).toBeNull();
  });

  it('says a graph run in Chinese too, every line and a node on its subject', () => {
    const zh = i18n.getFixedT('zh');
    for (const row of TABLE.graph) {
      const lines = graphSaid(zh, graphOf(row));
      expect(lines).toHaveLength(row.said.length);
      for (const line of lines) expect(line).toMatch(/[一-鿿]/);
    }
    const handed = TABLE.rows.find((row) => row.node === 'handed' && row.detail === 'active' && row.commit)!;
    expect(runSaid(zh, stepOf(handed))).toBe('在 `daoris/q2-work` 的 `11111111` 上观察中。');
  });

  // Each code a read that names no run answers, in the en catalogue's words, which the driver's
  // `WorkflowNoRun.Said` says on the terminal; and each has its zh words.
  it.each(TABLE.none.map((row) => [row.code, row] as const))('words the no-run code %s in the table’s words', (_code, row) => {
    const answer = { runs: [], problem: 'the driver’s sentence', code: row.code, id: row.id ?? null, workflow: row.workflow ?? null };
    expect(noRunSaid(t, answer)).toBe(row.said);
    expect(noRunSaid(i18n.getFixedT('zh'), answer)).toMatch(/[一-鿿]/);
  });

  it('words each state and each look added as the table has it', () => {
    for (const [state, word] of Object.entries(TABLE.states)) expect(stateSaid(t, state)).toBe(word);
    for (const [level, said] of Object.entries(TABLE.added)) {
      expect(addedSaid(t, { ...stepOf({ kind: 'look', detail: '', said: null }), added: level })).toBe(said);
    }
    expect(addedSaid(t, stepOf({ kind: 'look', detail: '', said: null }))).toBeNull();
  });

  // A chain composed through the tidy ends there, and only a merged request starts it, so a run finished at its tidy is a merged one.
  it('says a run finished at its tidy as merged, as one finished at the pull request is', () => {
    const finished = (kind: string) => ({ ...MERGED, steps: MERGED.steps.map((step) => (step.step.kind === 'pull-request' ? { ...step, step: { ...step.step, kind } } : step)) });
    for (const kind of ['pull-request', 'tidy']) expect(runShort(t, finished(kind))).toBe(runShort(t, MERGED));
    expect(runShort(t, finished('push'))).not.toBe(runShort(t, MERGED));
  });

  // A reopened submission waits on its agent, which the head says as the reopen, never as another repository's answer.
  it('says a reopened submission in a few words, in English and Chinese', () => {
    const reopened = { ...WORKING, steps: [{ ...WORKING.steps[0], state: 'waiting-on-agent', detail: 'reopened' }, ...WORKING.steps.slice(1)] };
    expect(runShort(t, reopened)).toBe('reopened, waits for its agent');
    expect(runShort(i18n.getFixedT('zh'), reopened)).toBe('已重新打开，等智能体');
    expect(runSaid(i18n.getFixedT('zh'), reopened.steps[0])).toBe('你重新打开了委托 #q1 的交付：等它的智能体再次交付。');
  });

  // FABRIC-CLOSE1c7: a source made ready again leaves its started step on the person's go-on or abandon, said as theirs to choose.
  it('says a started step waiting on the go-on in a few words, in English and Chinese', () => {
    const goOn = { ...WORKING, steps: [{ ...WORKING.steps[0], state: 'waiting-on-you', detail: 'go-on', quest: 'q2' }, ...WORKING.steps.slice(1)] };
    expect(runShort(t, goOn)).toBe('waits for your go-on');
    expect(runShort(i18n.getFixedT('zh'), goOn)).toBe('等你继续或放弃');
    expect(runSaid(i18n.getFixedT('zh'), goOn.steps[0])).toBe('你重新打开的工作已再次交付：它的下一步委托 #q2 等你选择在新的工作上继续它，或放弃它。');
  });
});

// A run's binding problem is worded from the catalogue by its code, the driver's English only where it sent none.
describe('a run held on a binding problem', () => {
  const coded = { problem: 'the driver’s English', problemCoded: { code: 'no-version', values: { workflow: 'docs-to-pr' } } };

  it('says its code in English and Chinese, never the driver’s sentence', () => {
    expect(bindingProblemSaid(t, coded)).toBe('`docs-to-pr` keeps no version that reads here.');
    expect(bindingProblemSaid(i18n.getFixedT('zh'), coded)).toBe('`docs-to-pr` 在这里没有可读取的版本。');
  });

  it('says the driver’s sentence where it sent no code, or one this page does not know', () => {
    expect(bindingProblemSaid(t, { problem: 'its graph changed.' })).toBe('its graph changed.');
    expect(bindingProblemSaid(t, { problem: 'newer', problemCoded: { code: 'later', values: {} } })).toBe('newer');
  });
});

import { afterEach, describe, expect, it } from 'vitest';
import i18n from '../locales/i18n';
import type { WorkflowStep } from './current';
import { BRANCH_AUTOMATIC, FULL, NOTHING_SET, WORK } from './fixtures';
import { GRAPH_RUN, GRAPH_RUN_ADDED_LOOK } from './runFixtures';
import { FINISHED, outcomeOf, workflowFlow } from './workflowFlow';

// FLOWCHART1d (D186; the flowchart design §2.4, §2.5, §5.1): a workflow drawn as a flowchart, from what `WORKFLOW_CURRENT` and
// `WORKFLOW_RUN` answer. Its steps in their version's order and the end of the line, each edge the move the step names; a step that
// says what follows its failure or its refusal hangs that outcome on a failure path; the landing divides before from after; a run
// lights each step by its state and draws no path. No group the answer does not hold is invented (`current.ts:27-42`).

const t = i18n.t.bind(i18n);

/** A check a newer driver may answer, with what follows its failure; this build runs none (`WorkflowKindTable`). */
const CHECK = (failed: string): WorkflowStep => ({
  id: 'checks', kind: 'check', participation: 'automatic', executor: 'plugin', press: null,
  source: { rule: 'workflow', level: 'task' }, settings: { plugin: 'example.checks', checks: 'required', failed }, runtime: 'declared', limit: null,
});

/** A go-ahead in a named version, with what its refusal does (the workflow design §3.6). */
const GO_AHEAD = (refused: string): WorkflowStep => ({
  id: 'release', kind: 'go-ahead', participation: 'you', executor: 'daoris', press: null,
  source: { rule: 'workflow', level: 'task' }, settings: { act: 'release to prod', on: 'ask', refused }, runtime: 'partial', limit: null,
});

describe('a workflow as a flowchart', () => {
  afterEach(async () => { await i18n.changeLanguage('en'); });

  it('draws its steps in their order and the end of the line, each edge the move its step names, and nothing else', () => {
    const flow = workflowFlow(t, FULL, 'engine');
    expect(flow.label).toBe('How work in engine moves');
    expect(flow.nodes.map((node) => node.id)).toEqual(['work', 'opinion', 'look', 'landing', 'pull-request', FINISHED]);
    expect(flow.nodes.map((node) => node.title)).toEqual(
      ['The work', 'Second opinion', 'Your look in dev', 'Landing on a branch', 'Pull request', 'Finished']);
    expect(flow.entries).toEqual(['work']);
    expect(flow.edges.map((edge) => [edge.from, edge.to, edge.says, edge.style ?? 'next'])).toEqual([
      ['work', 'opinion', 'automatically', 'next'],
      ['opinion', 'look', 'automatically', 'next'],
      ['look', 'landing', 'once you say it is reviewed', 'next'],
      ['landing', 'pull-request', 'automatically', 'next'],
      ['pull-request', FINISHED, 'once you merge it on the platform', 'next'],
    ]);
  });

  it('says who acts to a reader, the person\'s part and the executor, its names as words', () => {
    const [work, opinion] = workflowFlow(t, FULL, 'engine').nodes;
    expect(work!.actor).toBe('agent alone, by an agent');
    expect(opinion!.actor).toBe('agent alone, by codex-acp, else dsh');
  });

  it('divides before the landing from after it, where the rail has its section titles', () => {
    expect(workflowFlow(t, FULL, 'engine').sections).toEqual([
      { title: 'Before it lands', from: 'work' }, { title: 'After it lands', from: 'pull-request' },
    ]);
    // Nothing after the landing: one section.
    expect(workflowFlow(t, NOTHING_SET, 'engine').sections).toEqual([{ title: 'Before it lands', from: 'work' }]);
  });

  it('hangs a failure path on a step that says what follows its failure, a forward edge to its outcome, never a return', () => {
    for (const [failed, title] of [['wait', 'Waits for you'], ['send-back', 'Sent back once, then you'], ['stop', 'The run stops']] as const) {
      const flow = workflowFlow(t, { steps: [WORK(), BRANCH_AUTOMATIC, CHECK(failed)] }, 'engine');
      const outcome = flow.nodes.find((node) => node.id === 'checks:failed')!;
      expect(outcome).toMatchObject({ title, faint: true });
      expect(flow.edges.find((edge) => edge.to === 'checks:failed')).toMatchObject({ from: 'checks', style: 'failed', says: 'if it fails', label: 'on failure' });
    }
    // An outcome a newer driver names is drawn as recorded, never dropped.
    const odd = workflowFlow(t, { steps: [WORK(), BRANCH_AUTOMATIC, CHECK('retry-twice')] }, 'engine');
    expect(odd.nodes.find((node) => node.id === 'checks:failed')!.title).toBe('retry-twice, shown as recorded');
  });

  it('hangs a go-ahead\'s refusal on it, as a rejection', () => {
    const flow = workflowFlow(t, { steps: [WORK(), GO_AHEAD('wait'), BRANCH_AUTOMATIC] }, 'engine');
    expect(flow.nodes.find((node) => node.id === 'release:refused')).toMatchObject({ title: 'Waits for you', faint: true });
    expect(flow.edges.find((edge) => edge.to === 'release:refused')).toMatchObject({ style: 'rejected', says: 'if you refuse it' });
    expect(outcomeOf(GO_AHEAD('stop'))).toEqual({ id: 'release:refused', kind: 'refused', outcome: 'stop' });
  });

  it('invents no outcome a step does not name, and no group the answer does not hold', () => {
    expect(FULL.steps.map(outcomeOf)).toEqual([null, null, null, null, null]);
    const flow = workflowFlow(t, FULL, 'engine');
    // One edge into each step but the first, one out of each: a sequence, as today's answer holds it.
    for (const node of flow.nodes.slice(1)) expect(flow.edges.filter((edge) => edge.to === node.id)).toHaveLength(1);
  });

  it('lights a run: each step by its state, the step it stands at current, one not reached dashed, and no path', () => {
    const lit = {
      steps: {
        work: { state: { word: 'done', tone: 'done' as const }, reached: true, current: false, settled: true },
        landing: { state: { word: 'waiting on you', tone: 'open' as const }, reached: true, current: true, settled: false },
      },
      finished: null,
    };
    const flow = workflowFlow(t, NOTHING_SET, 'engine', lit);
    expect(flow.nodes).toEqual([
      expect.objectContaining({ id: 'work', state: { word: 'done', tone: 'done' }, reached: true, current: false, settled: true }),
      expect.objectContaining({ id: 'landing', state: { word: 'waiting on you', tone: 'open' }, current: true }),
      expect.objectContaining({ id: FINISHED, reached: false }),
    ]);
    expect(flow.edges.some((edge) => edge.taken)).toBe(false);
    const ended = workflowFlow(t, NOTHING_SET, 'engine', { ...lit, finished: { word: 'done', tone: 'done' } });
    expect(ended.nodes.at(-1)).toMatchObject({ id: FINISHED, state: { word: 'done', tone: 'done' }, reached: true, settled: true });
  });

  // FABRIC-CUTOVER1e (D186's FLOWCHART1d note): a run bound to a graph version answers its graph's wires, the first read that does, so its
  // drawing follows them: a fork and its join as the version holds them, a failure's path in its style, the end of the line after its end.
  it('draws a graph run by its wires, a fork and its join, a failure path, and the end after its end', () => {
    const { graph, steps } = GRAPH_RUN;
    const flow = workflowFlow(t, { steps: steps.map((step) => step.step) }, 'engine', undefined, graph);
    expect(flow.entries).toEqual(['claimed']);
    expect(flow.nodes.map((node) => node.id)).toEqual(['claimed', 'build', 'lint', 'both', 'halt', 'opinion', 'handed', 'deployed', FINISHED]);
    expect(flow.nodes.map((node) => node.title)).toEqual(
      ['The work', 'built by acme.ci', 'linted by acme.ci', 'The join', 'Stop', 'Second opinion', 'Landing on a branch', 'deployed by acme.deploy', 'Finished']);
    expect(flow.edges.filter((edge) => edge.from === 'claimed').map((edge) => edge.to)).toEqual(['build', 'lint']);
    expect(flow.edges.filter((edge) => edge.to === 'both').map((edge) => edge.from)).toEqual(['build', 'lint']);
    expect(flow.edges.find((edge) => edge.to === 'halt')).toMatchObject({ from: 'both', style: 'failed', says: 'if it fails', label: 'on failure' });
    expect(flow.edges.find((edge) => edge.from === 'handed')).toMatchObject({ to: 'deployed', says: 'once you accept it' });
    expect(flow.edges.at(-1)).toMatchObject({ from: 'deployed', to: FINISHED, says: 'automatically' });
    expect(flow.sections).toEqual([{ title: 'Before it lands', from: 'claimed' }, { title: 'After it lands', from: 'deployed' }]);
    // No outcome is hung on a node by its options: the graph's own wires draw its failure paths.
    expect(flow.nodes.filter((node) => node.id !== FINISHED && node.id.includes(':'))).toEqual([]);
  });

  it('draws a look the work added beside a graph as an entry of its own, joined to nothing', () => {
    const { graph, steps } = GRAPH_RUN_ADDED_LOOK;
    const flow = workflowFlow(t, { steps: steps.map((step) => step.step) }, 'engine', undefined, graph);
    expect(flow.entries).toEqual(['claimed', 'look']);
    expect(flow.edges.some((edge) => edge.from === 'look' || edge.to === 'look')).toBe(false);
    expect(flow.nodes.find((node) => node.id === 'release')!.title).toBe('Your go-ahead');
  });

  it('is worded in 中文', async () => {
    await i18n.changeLanguage('zh');
    const flow = workflowFlow(t, { steps: [WORK(), BRANCH_AUTOMATIC, CHECK('stop')] }, 'engine');
    expect(flow.sections[0]!.title).toBe('落地之前');
    expect(flow.nodes.find((node) => node.id === 'checks:failed')!.title).toBe('进展就此停止');
    expect(flow.edges[0]!.says).toBe('自动继续');
    const graph = workflowFlow(t, { steps: GRAPH_RUN.steps.map((step) => step.step) }, 'engine', undefined, GRAPH_RUN.graph);
    expect(graph.nodes.map((node) => node.title).slice(1, 5)).toEqual(['acme.ci 的 built', 'acme.ci 的 linted', '汇合', '停止']);
  });
});

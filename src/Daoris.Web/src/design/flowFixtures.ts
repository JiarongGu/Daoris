import type { EdgeStyle } from './flowLayout';

/**
 * The hardest graphs a flowchart draws (FLOWCHART1a1; the flowchart design §7, trap 3), as data in both languages: the
 * layout's tests read their shapes, `Flow`'s stories draw them, and the measured-box check runs over those stories in a
 * rendered browser. Tests and stories only: nothing in the product imports this.
 *
 * @remarks
 * Each fixture's words are authored here in English and 中文, as a caller would word them (a flow arrives worded); the
 * chrome around them is the catalogues'. A title in 中文 is several times longer than its English on purpose where the
 * fixture is about unequal boxes.
 */

/** Words in both languages. */
export type Said = { en: string; zh: string };

export type FixtureNode = {
  id: string;
  title: Said;
  state?: { word: Said; tone: 'neutral' | 'open' | 'taken' | 'done' | 'declined' };
  current?: boolean;
  reached?: boolean;
};

export type FixtureEdge = { id: string; from: string; to: string; says: Said; label?: Said; style?: EdgeStyle };

export type Fixture = { name: string; label: Said; nodes: FixtureNode[]; edges: FixtureEdge[]; entries: string[] };

const said = (en: string, zh: string): Said => ({ en, zh });

/**
 * A node's lifecycle, `NodeLifecycle.Table` as it stood on 2026-10-20 (`NodeLifecycle.cs:241-288`), its *any live state*
 * and *any bound state* rows expanded: the multigraph of §2.6, with reciprocal moves (blocked ⇄ ready, active ⇄ unknown),
 * several returns to ready, and parallel moves of one source and target by different causes.
 */
export const LIFECYCLE: Fixture = (() => {
  const state = (id: string, en: string, zh: string): FixtureNode => ({ id, title: said(en, zh) });
  const nodes = [
    state('blocked', 'Blocked', '受阻'),
    state('ready', 'Ready', '就绪'),
    state('active', 'Active', '进行中'),
    state('reached', 'Reached', '已达成'),
    state('failed', 'Failed', '已失败'),
    state('rejected', 'Rejected', '已驳回'),
    state('unknown', 'Unknown', '未知'),
    state('skipped', 'Skipped', '已跳过'),
    state('stopped', 'Stopped', '已停止'),
    state('invalidated', 'Invalidated', '已失效'),
  ];
  const by = {
    daoris: said('by Daoris', '由 Daoris'),
    authority: said('by its authority', '由其权威'),
    person: said('by the person', '由人'),
  };
  const rows: [string, string, keyof typeof by, string, string][] = [
    ['blocked', 'ready', 'daoris', 'when its wires, inputs and authority are satisfied', '当它的连线、输入和权威都满足时'],
    ['ready', 'blocked', 'daoris', 'when that stops being true before it starts', '当开始前这些不再成立时'],
    ['ready', 'active', 'daoris', 'when it starts', '当它开始时'],
    ['active', 'reached', 'authority', 'on accepted, bound evidence', '凭已接受、已绑定的证据'],
    ['active', 'failed', 'authority', 'on an authoritative negative outcome', '凭权威的否定结果'],
    ['active', 'rejected', 'person', 'when a person rejects it, with a reason', '当人附理由驳回时'],
    ['active', 'unknown', 'daoris', 'when the outcome cannot be established now', '当结果此刻无法确定时'],
    ['unknown', 'active', 'daoris', 'when reconciliation says it is running', '当核对表明它仍在运行时'],
    ['unknown', 'reached', 'authority', 'when reconciliation establishes it', '当核对确定结果时'],
    ['unknown', 'failed', 'authority', 'when reconciliation establishes it', '当核对确定结果时'],
    ['unknown', 'reached', 'person', 'when the person settles it happened', '当人认定它已发生时'],
    ['unknown', 'failed', 'person', 'when the person settles it did not', '当人认定它没有发生时'],
    ['failed', 'ready', 'daoris', 'on a declared retry', '按声明的重试'],
    ['failed', 'ready', 'person', 'when the person asks for a new attempt', '当人要求再试一次时'],
    ['rejected', 'ready', 'person', 'when the person asks for a new attempt', '当人要求再试一次时'],
    ['stopped', 'ready', 'person', 'when the person asks for a new attempt', '当人要求再试一次时'],
    ['ready', 'skipped', 'person', 'when the person passes it over', '当人跳过它时'],
    ['blocked', 'skipped', 'person', 'when the person passes it over', '当人跳过它时'],
    ['active', 'skipped', 'person', 'when the person passes it over', '当人跳过它时'],
    ['active', 'skipped', 'authority', 'when its record passes it over', '当它的记录跳过它时'],
    ['skipped', 'blocked', 'person', 'when the person withdraws the skip', '当人撤回跳过时'],
    ['skipped', 'invalidated', 'daoris', 'when its graph or run is another', '当它的图或运行已换时'],
    ...(['blocked', 'ready', 'active', 'unknown', 'invalidated'] as const).map((from): [string, string, keyof typeof by, string, string] =>
      [from, 'stopped', 'person', 'when the person stops it', '当人停止它时']),
    ...(['active', 'reached', 'failed', 'rejected', 'unknown'] as const).map((from): [string, string, keyof typeof by, string, string] =>
      [from, 'invalidated', 'daoris', 'when its binding changed', '当它的绑定变化时']),
    ['invalidated', 'blocked', 'daoris', 'when judged again on a fresh binding', '当在新绑定上重新判断时'],
    ['invalidated', 'ready', 'daoris', 'when judged again on a fresh binding', '当在新绑定上重新判断时'],
  ];
  const edges = rows.map(([from, to, cause, en, zh], index): FixtureEdge => ({
    id: `l${index}`, from, to, label: by[cause], says: said(`${by[cause].en}, ${en}`, `${by[cause].zh}，${zh}`),
    style: to === 'failed' || to === 'rejected' ? (to === 'failed' ? 'failed' : 'rejected') : undefined,
  }));
  return { name: 'lifecycle', label: said('How a node moves', '节点如何流转'), nodes, edges, entries: ['blocked'] };
})();

/**
 * A dense process at the bounds (the fabric design §E7): 24 authored steps with `allOf` and `anyOf` groups, each group's
 * synthetic join, failure sinks and tasks, the protected order's `after` wires from each hold to every later gate, and
 * data wires across the steps, 96 wires in all.
 */
export const DENSE: Fixture = (() => {
  const nodes: FixtureNode[] = [];
  const edges: FixtureEdge[] = [];
  const step = (id: string, en: string, zh: string) => nodes.push({ id, title: said(en, zh) });
  const wire = (from: string, to: string, kind: 'after' | 'data' | 'failed') => edges.push({
    id: `d${edges.length}`, from, to, style: kind === 'failed' ? 'failed' : undefined,
    says: kind === 'data'
      ? said(`its output feeds ${to}`, `它的输出交给 ${to}`)
      : kind === 'failed' ? said('on failure', '失败时') : said(`after ${from}`, `在 ${from} 之后`),
    label: kind === 'data' ? said('data', '数据') : kind === 'failed' ? said('on failure', '失败时') : undefined,
  });
  step('h1', 'Hold: trust', '拦住：信任');
  step('h2', 'Hold: account', '拦住：账号');
  step('w1', 'Read the brief', '读需求');
  for (const id of ['a1', 'a2', 'a3']) step(id, `Build ${id}`, `构建 ${id}`);
  step('jA', 'All of A', 'A 全部');
  step('w2', 'Integrate', '集成');
  for (const id of ['b1', 'b2', 'b3']) step(id, `Probe ${id}`, `探查 ${id}`);
  step('jB', 'Any of B', 'B 任一');
  step('w3', 'Write the change', '写改动');
  for (const id of ['c1', 'c2']) step(id, `Check ${id}`, `检查 ${id}`);
  step('jC', 'All of C', 'C 全部');
  step('w4', 'Migrate', '迁移');
  step('w5', 'Document', '写文档');
  for (const id of ['d1', 'd2']) step(id, `Verify ${id}`, `验证 ${id}`);
  step('jD', 'Any of D', 'D 任一');
  step('w6', 'Benchmark', '基准测试');
  step('w7', 'Polish', '打磨');
  step('w8', 'Summarise', '总结');
  step('op', 'Second opinion', '第二意见');
  step('look', 'Look', '查看');
  step('land', 'Land', '落地');
  step('pr', 'Pull request', '拉取请求');
  step('s1', 'Stop', '停止');
  step('t1', 'Wait for you', '等你');
  step('s2', 'Stop', '停止');
  step('t2', 'Wait for you', '等你');
  const order: [string, string][] = [
    ['h1', 'h2'], ['h2', 'w1'], ['w1', 'a1'], ['w1', 'a2'], ['w1', 'a3'], ['a1', 'jA'], ['a2', 'jA'], ['a3', 'jA'],
    ['jA', 'w2'], ['w2', 'b1'], ['w2', 'b2'], ['w2', 'b3'], ['b1', 'jB'], ['b2', 'jB'], ['b3', 'jB'], ['jB', 'w3'],
    ['w3', 'c1'], ['w3', 'c2'], ['c1', 'jC'], ['c2', 'jC'], ['jC', 'w4'], ['w4', 'w5'], ['w5', 'd1'], ['w5', 'd2'],
    ['d1', 'jD'], ['d2', 'jD'], ['jD', 'w6'], ['w6', 'w7'], ['w7', 'w8'], ['w8', 'op'], ['op', 'look'], ['look', 'land'],
    ['land', 'pr'],
  ];
  for (const [from, to] of order) wire(from, to, 'after');
  // The protected order (§E7): an `after` from each hold, the opinion and the look into every gate after it.
  for (const [from, to] of [['h1', 'op'], ['h1', 'look'], ['h1', 'land'], ['h2', 'op'], ['h2', 'look'], ['h2', 'land'], ['op', 'land']]) {
    wire(from!, to!, 'after');
  }
  for (const [from, to] of [['w2', 's1'], ['w4', 't1'], ['op', 's2'], ['land', 't2']]) wire(from!, to!, 'failed');
  // Data across the steps, to the bound.
  const data: [string, string][] = [
    ['w1', 'w2'], ['w1', 'w3'], ['a1', 'w2'], ['a2', 'w3'], ['a3', 'w4'], ['w2', 'w3'], ['b1', 'w3'], ['b2', 'w4'],
    ['b3', 'w5'], ['w3', 'w4'], ['w3', 'w5'], ['c1', 'w4'], ['c2', 'w5'], ['c1', 'w6'], ['w4', 'd1'], ['w4', 'd2'],
    ['w4', 'w6'], ['w5', 'w6'], ['d1', 'w6'], ['d2', 'w7'], ['w6', 'w8'], ['w6', 'op'], ['w7', 'op'], ['w5', 'op'],
    ['w3', 'op'], ['w8', 'look'], ['w7', 'look'], ['w6', 'land'], ['w8', 'land'], ['w1', 'op'], ['a1', 'look'],
    ['b1', 'land'], ['c2', 'look'], ['d2', 'land'], ['jA', 'w3'], ['jB', 'w4'], ['jC', 'w5'], ['jD', 'w7'], ['w2', 'w4'],
    ['w2', 'w5'], ['w1', 'w4'], ['w3', 'w6'], ['w4', 'w7'], ['w5', 'w7'], ['w5', 'w8'], ['op', 'pr'], ['look', 'pr'],
    ['w8', 'pr'], ['w1', 'pr'], ['a2', 'w4'], ['b2', 'w5'], ['c1', 'w5'],
  ];
  for (const [from, to] of data) wire(from, to, 'data');
  return { name: 'dense', label: said('How the work moves', '工作如何流转'), nodes, edges, entries: ['h1'] };
})();

/** Unequal boxes: a fork of three and its join, the middle title long in 中文 and short in English. */
export const UNEQUAL: Fixture = {
  name: 'unequal',
  label: said('How the checks run', '检查如何进行'),
  nodes: [
    { id: 'start', title: said('Start', '开始'), reached: true },
    { id: 'lint', title: said('Lint', '按工作区约定的规则逐个检查每个仓库的代码风格并把每一条发现都记下来'), current: true, state: { word: said('running', '进行中'), tone: 'taken' } },
    { id: 'types', title: said('Check every type across the whole workspace before anything lands', '类型'), reached: false },
    { id: 'tests', title: said('Tests', '测试'), reached: false },
    { id: 'end', title: said('Ready to land', '可以落地，等所有检查都通过以后再把改动合并进主线'), reached: false },
  ],
  edges: [
    { id: 'u1', from: 'start', to: 'lint', says: said('when it starts', '开始时') },
    { id: 'u2', from: 'start', to: 'types', says: said('when it starts', '开始时') },
    { id: 'u3', from: 'start', to: 'tests', says: said('when it starts', '开始时') },
    { id: 'u4', from: 'lint', to: 'end', says: said('when it passes', '通过时'), label: said('passes', '通过') },
    { id: 'u5', from: 'types', to: 'end', says: said('when it passes', '通过时'), label: said('passes', '通过') },
    { id: 'u6', from: 'tests', to: 'end', says: said('when they pass', '通过时'), label: said('pass', '通过') },
    { id: 'u7', from: 'start', to: 'end', says: said('when there is nothing to check', '无需检查时'), label: said('nothing to check', '无需检查') },
  ],
  entries: ['start'],
};

/**
 * An instance before and after a node is appended (§3.1): ranks [A, B] → [X, Y] with A → X and B → Y; then A → Z, which
 * the sweeps order X, Z, Y, so Y moves.
 */
export const APPEND_BEFORE: Fixture = {
  name: 'append-before',
  label: said('How the parts began', '各部分如何开始'),
  nodes: [
    { id: 'A', title: said('Opened', '开启') },
    { id: 'B', title: said('Asked by the engine', '由 engine 请求') },
    { id: 'X', title: said('Retried', '重试') },
    { id: 'Y', title: said('Carried on', '接续'), current: true, state: { word: said('working', '工作中'), tone: 'taken' } },
  ],
  edges: [
    { id: 'x', from: 'A', to: 'X', says: said('retried', '重试'), label: said('retried', '重试') },
    { id: 'y', from: 'B', to: 'Y', says: said('carried on', '接续'), label: said('carried on', '接续') },
  ],
  entries: ['A', 'B'],
};

export const APPEND_AFTER: Fixture = {
  ...APPEND_BEFORE,
  name: 'append-after',
  nodes: [...APPEND_BEFORE.nodes, { id: 'Z', title: said('Resumed', '恢复') }],
  edges: [...APPEND_BEFORE.edges, { id: 'z', from: 'A', to: 'Z', says: said('resumed', '恢复'), label: said('resumed', '恢复') }],
};

/** A process with several nodes current at once, each lit by its own state. */
export const MANY_CURRENT: Fixture = {
  name: 'many-current',
  label: said('How the run stands', '运行进展'),
  nodes: [
    { id: 'claim', title: said('Claim', '认领'), state: { word: said('done', '已完成'), tone: 'done' }, reached: true },
    { id: 'build', title: said('Build', '构建'), state: { word: said('working', '工作中'), tone: 'taken' }, current: true },
    { id: 'docs', title: said('Docs', '文档'), state: { word: said('working', '工作中'), tone: 'taken' }, current: true },
    { id: 'review', title: said('Your review', '你的审阅'), state: { word: said('waiting on you', '等你'), tone: 'open' }, current: true },
    { id: 'join', title: said('All of them', '全部'), reached: false },
    { id: 'land', title: said('Land', '落地'), reached: false },
  ],
  edges: [
    { id: 'm1', from: 'claim', to: 'build', says: said('after the claim', '认领之后') },
    { id: 'm2', from: 'claim', to: 'docs', says: said('after the claim', '认领之后') },
    { id: 'm3', from: 'claim', to: 'review', says: said('after the claim', '认领之后') },
    { id: 'm4', from: 'build', to: 'join', says: said('when it is reached', '达成时') },
    { id: 'm5', from: 'docs', to: 'join', says: said('when it is reached', '达成时') },
    { id: 'm6', from: 'review', to: 'join', says: said('when you accept', '你接受时') },
    { id: 'm7', from: 'join', to: 'land', says: said('when all are reached', '全部达成时') },
    { id: 'm8', from: 'build', to: 'land', says: said('on failure, to wait for you', '失败时，等你'), style: 'failed', label: said('on failure', '失败时') },
  ],
  entries: ['claim'],
};

/** The same process with nothing current: a run not started. Its layout is MANY_CURRENT's, since only what is lit differs. */
export const NONE_CURRENT: Fixture = {
  ...MANY_CURRENT,
  name: 'none-current',
  nodes: MANY_CURRENT.nodes.map(({ current: _current, state: _state, ...node }) => ({ ...node, reached: false })),
};

/** Every fixture the measured-box check and the crossing table run over, by name. */
export const HARDEST: readonly Fixture[] = [LIFECYCLE, DENSE, UNEQUAL, APPEND_BEFORE, APPEND_AFTER, MANY_CURRENT, NONE_CURRENT];

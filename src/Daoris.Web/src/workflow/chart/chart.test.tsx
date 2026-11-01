import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18n from '../../locales/i18n';
import type { NodeContext } from '../../design/FlowCanvas';
import { drawnFromJson, graphFlow } from '../graphFlow';
import { RELEASE_GRAPH, TWO_OUTCOMES_GRAPH } from '../graphFixtures';
import { EdgeChip } from './EdgeChip';
import { type ChartEdit, FlowChart } from './FlowChart';
import { HoldsGroup } from './HoldsGroup';
import { StepNode } from './StepNode';
import { Terminal } from './Terminal';
import type { ChartNode, ChartStep } from './stepKinds';

// FLOWCHART2a (D223): the chart's components. Each draws one kind of node as the canvas hands it over: the same list item, tab stop and
// description as every node, and a decision's outcomes as ports a keyboard presses. jsdom lays nothing out, so the geometry is the
// measured-box check's.

const t = i18n.t.bind(i18n);
afterEach(async () => { await i18n.changeLanguage('en'); });

const context = (node: ChartNode, over: Partial<NodeContext> = {}): NodeContext => ({
  node, stop: false, feet: [], description: '', onFollow: () => {}, ...over,
});
const step = (over: Partial<ChartStep> = {}): ChartStep => ({ kind: 'claimed', variant: 'work', ports: [], ...over });

describe('StepNode', () => {
  it.each(['work', 'you', 'check', 'landing', 'line', 'plugin', 'other', 'unknown'] as const)('draws the %s variant as a list item named by its title', (variant) => {
    const node: ChartNode = { id: 'a', title: 'The step', step: step({ variant }), who: <span>Who</span> };
    render(<StepNode context={context(node)} step={node.step!} />);
    const item = screen.getByRole('listitem', { name: 'The step' });
    expect(item).toHaveAttribute('data-variant', variant);
    expect(item).toHaveAttribute('data-flow-node', 'a');
    expect(item).toHaveTextContent('Who');
  });

  it('holds the stop, names its state, marks the current one and says the description', () => {
    const node: ChartNode = { id: 'a', title: 'The work', state: { word: 'here now', tone: 'taken' }, current: true, step: step() };
    render(<StepNode context={context(node, { stop: true, description: 'Next: Review.' })} step={node.step!} />);
    const item = screen.getByRole('listitem', { name: 'The work here now' });
    expect(item).toHaveAttribute('tabindex', '0');
    expect(item).toHaveAttribute('aria-current', 'step');
    expect(item).toHaveAccessibleDescription('Next: Review.');
  });

  it('draws a step not reached dashed, never faint by opacity', () => {
    const node: ChartNode = { id: 'a', title: 'The work', reached: false, step: step() };
    render(<StepNode context={context(node)} step={node.step!} />);
    const item = screen.getByRole('listitem');
    expect(item.className).toContain('border-dashed');
    expect(item.className).not.toMatch(/opacity/);
  });

  it('draws its foot\'s presses, each moving the focus to its target', async () => {
    const onFollow = vi.fn();
    const node: ChartNode = { id: 'a', title: 'The work', step: step() };
    render(<StepNode context={context(node, { onFollow, feet: [{ to: 'b', back: false, text: 'Review, automatically' }] })} step={node.step!} />);
    await userEvent.click(screen.getByRole('button', { name: /Review, automatically/ }));
    expect(onFollow).toHaveBeenCalledWith('a', 'b');
  });

  const decision = (): [ChartNode, NodeContext] => {
    const node: ChartNode = {
      id: 'find', title: 'Find a release',
      step: step({
        kind: 'p/find', variant: 'decision',
        ports: [{ outcome: 'release', routed: true }, { outcome: 'none', routed: false }],
      }),
    };
    return [node, context(node, {
      onFollow: vi.fn(),
      feet: [
        { to: 'record', back: false, text: 'Record, when it comes to `release`', port: 'release' },
        { to: 'find:none:none', back: false, text: 'No further step, when it comes to `none`, nothing follows', port: 'none' },
        { to: 'other', back: false, text: 'Other, automatically' },
      ],
    })];
  };

  it('draws a decision\'s outcomes as ports on its lower edge, routed or ending at a ◎, each pressable', async () => {
    const [node, ctx] = decision();
    render(<StepNode context={ctx} step={node.step!} />);
    const release = screen.getByRole('button', { name: 'Record, when it comes to release' });
    const none = screen.getByRole('button', { name: 'No further step, when it comes to none, nothing follows' });
    expect(release).toHaveAttribute('data-flow-port', 'find:release');
    expect(release).toHaveAttribute('data-routed', 'true');
    expect(none).toHaveAttribute('data-flow-port', 'find:none');
    expect(none).toHaveAttribute('data-routed', 'false');
    expect(release).toHaveTextContent('release');
    await userEvent.click(release);
    expect(ctx.onFollow).toHaveBeenCalledWith('find', 'record');
  });

  it('says a route once: the foot omits the lines the ports carry and keeps the rest', () => {
    const [node, ctx] = decision();
    render(<StepNode context={ctx} step={node.step!} />);
    const item = screen.getByRole('listitem');
    expect(within(item).getAllByRole('button').map((button) => button.getAttribute('data-flow-port'))).toEqual([null, 'find:release', 'find:none']);
    expect(within(item).getByRole('button', { name: /Other, automatically/ })).toBeInTheDocument();
  });

  it('marks the outcome a run\'s decision returned', () => {
    const node: ChartNode = {
      id: 'find', title: 'Find', step: step({ variant: 'decision', ports: [{ outcome: 'release', routed: true, taken: true }, { outcome: 'none', routed: false, taken: false }] }),
    };
    render(<StepNode context={context(node, { feet: [{ to: 'r', back: false, text: 'R', port: 'release' }, { to: 'n', back: false, text: 'N', port: 'none' }] })} step={node.step!} />);
    expect(screen.getByRole('button', { name: 'R' })).toHaveAttribute('data-taken', 'true');
    expect(screen.getByRole('button', { name: 'N' })).not.toHaveAttribute('data-taken', 'true');
  });
});

describe('Terminal', () => {
  it('draws ◎ No further step as a faint terminal with nothing to press', () => {
    const node: ChartNode = { id: 'x', title: 'No further step', glyph: 'stepNoFurther', faint: true, step: step({ kind: 'none', variant: 'other', terminal: 'none' }) };
    render(<Terminal context={context(node)} />);
    const item = screen.getByRole('listitem', { name: 'No further step' });
    expect(item).toHaveAttribute('data-terminal', 'none');
    expect(item.className).toContain('rounded-full');
    expect(item.className).toContain('border-dashed');
    expect(within(item).queryByRole('button')).toBeNull();
  });

  it('draws a run\'s Finished with its state', () => {
    const node: ChartNode = { id: ':finished', title: 'Finished', state: { word: 'done', tone: 'done' }, reached: true, step: step({ terminal: 'finished' }) };
    render(<Terminal context={context(node)} />);
    expect(screen.getByRole('listitem', { name: 'Finished done' })).toHaveAttribute('data-terminal', 'finished');
  });
});

describe('HoldsGroup', () => {
  it('draws the folded holds as one node with its members and the press that opens them', async () => {
    const open = vi.fn();
    const node: ChartNode = {
      id: 'holds:a', title: 'Daoris makes sure · 3', lines: '▸ a, b, c', open,
      step: step({ kind: 'holds', variant: 'holds', group: { count: 3, open: false, members: ['a', 'b', 'c'] } }),
    };
    render(<HoldsGroup context={context(node)} step={node.step!} />);
    const item = screen.getByRole('listitem', { name: 'Daoris makes sure · 3' });
    expect(item).toHaveTextContent('▸ a, b, c');
    await userEvent.click(within(item).getByRole('button', { name: 'Show all 3' }));
    expect(open).toHaveBeenCalledOnce();
    expect(within(item).getByRole('button', { name: 'Show all 3' })).toHaveAttribute('aria-expanded', 'false');
  });

  it('is opened by a step that carries the press to fold the group again', async () => {
    const open = vi.fn();
    const node: ChartNode = {
      id: 'requests', title: 'The work\'s requests', open,
      step: step({ kind: 'work.requests', variant: 'holds', group: { count: 3, open: true, members: ['a', 'b', 'c'] } }),
    };
    render(<StepNode context={context(node)} step={node.step!} />);
    const press = screen.getByRole('button', { name: 'Fold the 3 again' });
    expect(press).toHaveAttribute('aria-expanded', 'true');
    await userEvent.click(press);
    expect(open).toHaveBeenCalledOnce();
  });
});

describe('EdgeChip', () => {
  it('says nothing for a decision\'s route: its port names it', () => {
    const { container } = render(<EdgeChip style="when" text="release" count={1} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('says a return by its mark, a failure and a refusal by their words, and a data wire by what it carries as code', () => {
    const { container } = render(
      <>
        <EdgeChip style="back" text="↺" count={1} />
        <EdgeChip style="failed" text="on failure" count={1} />
        <EdgeChip style="rejected" text="refused" count={1} />
        <EdgeChip style="data" text="release" count={1} />
      </>,
    );
    expect(container).toHaveTextContent('↺on failurerefusedrelease');
    expect(container.querySelector('code, .font-mono')).toHaveTextContent('release');
  });

  it('says a problem with a ⚠ in the danger ink and leaves the edge\'s ink to the canvas', () => {
    const { container } = render(<EdgeChip style="next" text="" count={1} problem="needs a role" />);
    expect(container).toHaveTextContent('⚠ needs a role');
    expect(container.innerHTML).toContain('text-ink-danger');
  });

  it('wears no status hue in any kind', () => {
    const kinds = ['next', 'when', 'back', 'failed', 'rejected', 'refused', 'conditional', 'data'] as const;
    const { container } = render(<>{kinds.map((kind) => <EdgeChip key={kind} style={kind} text="x" count={2} problem="p" />)}</>);
    expect(container.innerHTML).not.toMatch(/st-(open|taken|done|declined)/);
  });
});

describe('the release workflow through the components', () => {
  it('draws its decision with a port for each outcome, the ◎ for the one routed nowhere, and the holds as one group', () => {
    const flow = graphFlow(t, drawnFromJson(t, i18n, RELEASE_GRAPH), { label: 'How work moves' });
    render(<FlowChart label={flow.label} nodes={flow.nodes} edges={flow.edges} entries={flow.entries} sections={flow.sections} track={flow.track} />);
    const list = screen.getByRole('list', { name: 'How work moves' });
    const find = within(list).getByRole('listitem', { name: /^find by/ });
    expect(within(find).getAllByRole('button').filter((button) => button.hasAttribute('data-flow-port')).map((button) => button.getAttribute('data-flow-port')))
      .toEqual(['find:release', 'find:none']);
    expect(list.querySelector('[data-terminal="none"]')).toHaveAccessibleName('No further step');
    expect(within(list).getByRole('listitem', { name: 'Daoris makes sure · 3' })).toBeInTheDocument();
    expect(list.querySelector('[data-variant="you"]')).toHaveAccessibleName('Your review');
  });

  it('keeps two outcomes that reach one step as two ports', () => {
    const flow = graphFlow(t, drawnFromJson(t, i18n, TWO_OUTCOMES_GRAPH), { label: 'x' });
    render(<FlowChart label={flow.label} nodes={flow.nodes} edges={flow.edges} entries={flow.entries} sections={flow.sections} track={flow.track} />);
    const ports = [...document.querySelectorAll('[data-flow-port]')].map((port) => port.getAttribute('data-flow-port'));
    expect(ports).toEqual(['find:release', 'find:again', 'find:none']);
  });
});

// FLOWCHART2b (D223 §3): the chart as the editor draws it. A step pressed is selected; a port pressed or dragged from starts its way, which a
// step that can take it picks or open ground gives a new step; a ◎ and each way the editor offers one wear a ＋.
describe('the chart in the editor', () => {
  const edit = (more: Partial<ChartEdit> = {}): ChartEdit => ({
    selected: null, onSelect: vi.fn(), onPort: vi.fn(), onGround: vi.fn(),
    insertOn: (edge) => (edge.style === undefined ? { label: `Add a step between ${edge.from} and ${edge.to}`, onPress: vi.fn() } : null),
    insertAt: (node) => ({ label: `Add a step at ${node.id}`, onPress: vi.fn() }),
    ...more,
  });
  const drawRelease = (chart: ChartEdit) => {
    const flow = graphFlow(t, drawnFromJson(t, i18n, RELEASE_GRAPH), { label: 'How work moves' });
    return render(<FlowChart label={flow.label} nodes={flow.nodes} edges={flow.edges} entries={flow.entries} sections={flow.sections} track={flow.track} edit={chart} />);
  };

  it('selects a step pressed, marks the one selected, and leaves a press on its foot to the foot', async () => {
    const chart = edit({ selected: 'review' });
    drawRelease(chart);
    const review = screen.getByRole('listitem', { name: 'Your review' });
    expect(review).toHaveAttribute('data-selected', 'true');
    await userEvent.click(screen.getByRole('listitem', { name: 'The checks' }));
    expect(chart.onSelect).toHaveBeenCalledWith('check');
    await userEvent.click(within(review).getAllByRole('button')[0]!);
    expect(chart.onSelect).not.toHaveBeenCalledWith('review');
  });

  it("starts a decision's way from its port, never following it, and offers a ＋ on each way it is given one and in each ◎", async () => {
    const chart = edit();
    drawRelease(chart);
    await userEvent.click(document.querySelector<HTMLElement>('[data-flow-port="find:none"]')!);
    expect(chart.onPort).toHaveBeenCalledWith('find', 'none');
    expect(screen.getByRole('button', { name: 'Add a step between review and landing' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Add a step at find:none:none' })).toBeInTheDocument();
  });

  it('picks the step a port is dragged to where it can take the way, and gives open ground a new step', async () => {
    const pick = { key: 'find:none', can: (id: string) => id === 'publish', onPick: vi.fn(), onCancel: vi.fn() };
    const chart = edit({ pick });
    const { container } = drawRelease(chart);
    const port = container.querySelector<HTMLElement>('[data-flow-port="find:none"]')!;
    const user = userEvent.setup();
    await user.pointer([{ keys: '[MouseLeft>]', target: port }, { target: screen.getByRole('listitem', { name: /^publish by/ }) }, { keys: '[/MouseLeft]' }]);
    expect(pick.onPick).toHaveBeenCalledWith('publish');
    await user.pointer([{ keys: '[MouseLeft>]', target: port }, { target: container.querySelector<HTMLElement>('[data-flow-pane]')! }, { keys: '[/MouseLeft]' }]);
    expect(chart.onGround).toHaveBeenCalledOnce();
  });
});

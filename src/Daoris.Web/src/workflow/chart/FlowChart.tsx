import { type PointerEvent, useRef } from 'react';
import { type FlowCanvasProps, type FlowEdge, type FlowPick, FlowCanvas, type LabelContext, type NodeContext } from '../../design/FlowCanvas';
import { FlowNodeBox } from '../../design/Flow';
import { EdgeChip } from './EdgeChip';
import { GapInsert, type InsertOffer } from './GapInsert';
import { HoldsGroup } from './HoldsGroup';
import { StepNode } from './StepNode';
import { Terminal } from './Terminal';
import { type ChartNode, isChartNode } from './stepKinds';

// The workflow's flowchart (FLOWCHART2a; D223): the canvas drawing `graphFlow`'s nodes through the chart's components, a node for each
// kind of step, a decision's outcomes as ports, a terminal for each end, the holds as one group, and a chip for each way. A node the
// canvas drew of its own (a fold of a large run's settled stretch) is `Flow`'s box. In the editor (FLOWCHART2b) the same chart is edited
// on: a step pressed is selected, a port pressed or dragged from starts its way, and each way and each ◎ wears a ＋.

/**
 * What the editor hands the chart (FLOWCHART2b; D223 §3): the step selected and the press that selects one, the ＋ on each way and in each
 * ◎, a port's press, and pick mode. A drag from a port picks the step it is dropped on where that step can take the way, and gives open
 * ground (`onGround`) a new step for it.
 */
export type ChartEdit = {
  selected: string | null;
  onSelect: (id: string) => void;
  insertOn?: (edge: FlowEdge) => InsertOffer | null;
  insertAt?: (node: ChartNode) => InsertOffer | null;
  /** A decision's port pressed or dragged from: the way it starts, by its outcome. */
  onPort?: (node: string, outcome: string) => void;
  onGround?: () => void;
  pick?: FlowPick | null;
  /** How a proposal marks a way (FLOWCHART2f): new or to remove, its sign and its word, drawn in the way's chip; null where it marks none. */
  wayMark?: (edge: string) => { sign: string; word: string } | null;
};

function drawNode(context: NodeContext, edit: ChartEdit | undefined) {
  const { node } = context;
  if (!isChartNode(node)) return <FlowNodeBox {...context} />;
  const { step } = node;
  if (step.terminal) return <Terminal context={context} insert={edit?.insertAt?.(node) ?? null} />;
  if (step.group && !step.group.open) return <HoldsGroup context={context} step={step} />;
  return <StepNode context={context} step={step} edit={edit} />;
}

function drawLabel({ style, text, count, edge }: LabelContext, edit: ChartEdit | undefined) {
  const mark = edge && edit?.wayMark ? edit.wayMark(edge.id) : null;
  return <EdgeChip style={style} text={text} count={count} problem={edge?.problem} {...(mark ? { mark } : {})} />;
}

export function FlowChart({ edit, ...props }: Omit<FlowCanvasProps, 'renderNode' | 'renderLabel' | 'renderInsert' | 'pick'> & { edit?: ChartEdit }) {
  // A drag from a port: where it started, read again at the release, where the editor's pick mode has begun.
  const dragged = useRef<string | null>(null);
  const latest = useRef(edit);
  latest.current = edit;

  const onPointerDown = (event: PointerEvent<HTMLDivElement>) => {
    const port = (event.target as HTMLElement).closest<HTMLElement>('[data-flow-port]');
    const at = port?.dataset.flowPort;
    if (!edit?.onPort || !at || event.button !== 0) return;
    const split = at.indexOf(':');
    dragged.current = at;
    edit.onPort(at.slice(0, split), at.slice(split + 1));
  };
  const onPointerUp = (event: PointerEvent<HTMLDivElement>) => {
    const from = dragged.current;
    dragged.current = null;
    const now = latest.current;
    if (from === null || !now) return;
    const target = event.target as HTMLElement;
    if (target.closest('[data-flow-port]')?.getAttribute('data-flow-port') === from) return;
    const holder = target.closest<HTMLElement>('[data-flow-node]');
    if (holder) {
      const id = holder.dataset.flowNode!;
      if (now.pick?.can(id)) now.pick.onPick(id);
      return;
    }
    if (!target.closest('button, a, input, select, textarea')) now.onGround?.();
  };

  const offerOn = edit?.insertOn;
  const canvas = (
    <FlowCanvas
      {...props}
      renderNode={(context) => drawNode(context, edit)}
      renderLabel={(context) => drawLabel(context, edit)}
      {...(offerOn ? { renderInsert: (edge: FlowEdge) => { const offer = offerOn(edge); return offer ? <GapInsert offer={offer} /> : null; } } : {})}
      pick={edit?.pick ?? null}
    />
  );
  // Only the editor's chart hears a drag; the place's and a run's are the canvas alone.
  return edit ? <div className="min-w-0" onPointerDown={onPointerDown} onPointerUp={onPointerUp}>{canvas}</div> : canvas;
}

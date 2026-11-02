import { useRef, useState } from 'react';
import type { TFunction } from 'i18next';
import { useTranslation } from 'react-i18next';
import type { CanvasOptions, CanvasSelection } from '../../design/canvas/Canvas';
import type { Connect } from '../../design/canvas/useConnection';
import { type FlowCanvasProps, type FlowEdge, type FlowPick, FlowCanvas, type LabelContext, type NodeContext } from '../../design/FlowCanvas';
import { FlowNodeBox } from '../../design/Flow';
import { EdgeChip } from './EdgeChip';
import { GapInsert, type InsertOffer } from './GapInsert';
import { HoldsGroup } from './HoldsGroup';
import type { SelectHow } from './NodeFrame';
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
  /** The steps chosen with it, for Copy alone (CANVAS1b): outlined as it is, never the inspector's. */
  chosen?: ReadonlySet<string>;
  /** A press on a step; with Ctrl (`toggle`) or Shift (`add`) held it chooses more than one where `onBox` is given. */
  onSelect: (id: string, how?: SelectHow) => void;
  /** A Shift+drag box on the ground chose these steps, in reading order. */
  onBox?: (ids: string[]) => void;
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

/**
 * The workflow's viewport (CANVAS1a; D224, the canvas design §3.1): 80–200% while it is edited, so the ＋ and the ports stay usable, and
 * 50–200% while it is read, in the workflow's own words for its fit and how it moves.
 */
export function workflowViewport(t: TFunction, mode: 'edit' | 'display'): CanvasOptions {
  return {
    mode, zoom: mode === 'edit' ? { min: 0.8, max: 2 } : { min: 0.5, max: 2 }, fit: t('workflow.canvas.fit'), moves: t('workflow.canvas.moves'),
  };
}

export function FlowChart({ edit, canvas, ...props }: Omit<FlowCanvasProps, 'renderNode' | 'renderLabel' | 'renderInsert' | 'pick' | 'viewport' | 'selection' | 'connect'> & {
  edit?: ChartEdit;
  /** Drawn in the canvas's viewport from its floor (CANVAS1a): edited in the editor, read on a place's chart, the tab's and a run's. */
  canvas?: 'edit' | 'display';
}) {
  const { t } = useTranslation();
  const latest = useRef(edit);
  latest.current = edit;
  // A chart read, not edited, still selects a step (an outline): one, released by the ground and by a second press with Ctrl (CANVAS1b).
  const [shown, setShown] = useState<string | null>(null);
  const reading = canvas === 'display' && !edit;
  const acting: ChartEdit | undefined = edit ?? (reading
    ? { selected: shown, onSelect: (id, how) => setShown((was) => (how === 'toggle' && was === id ? null : id)) }
    : undefined);

  // The connection line from a port (CANVAS1b): the press begins the way and the editor's pick mode with it; a release over a step that
  // can take it picks it, over open ground is the editor's, and Escape or a lost capture gives the way up.
  const connect: Connect | null = edit?.onPort ? {
    from: (target) => {
      const at = target.closest('[data-flow-port]')?.getAttribute('data-flow-port');
      if (!at) return null;
      const split = at.indexOf(':');
      return { id: at.slice(0, split), port: at.slice(split + 1) };
    },
    onStart: (id, port) => latest.current?.onPort?.(id, port),
    can: (id) => latest.current?.pick?.can(id) ?? false,
    onConnect: (to) => latest.current?.pick?.onPick(to),
    onGround: () => latest.current?.onGround?.(),
    onCancel: () => latest.current?.pick?.onCancel(),
  } : null;
  // The ground: released in display, nothing in edit, and open ground to choose in pick mode. Many are chosen in the editor, for Copy.
  const selection: CanvasSelection | null = edit
    ? { many: edit.onBox !== undefined, onGround: () => { if (latest.current?.pick) latest.current.onGround?.(); }, onBox: (ids) => latest.current?.onBox?.(ids) }
    : reading ? { many: false, onGround: () => setShown(null), onBox: () => {} } : null;

  const offerOn = edit?.insertOn;
  return (
    <FlowCanvas
      {...props}
      renderNode={(context) => drawNode(context, acting)}
      renderLabel={(context) => drawLabel(context, edit)}
      {...(offerOn ? { renderInsert: (edge: FlowEdge) => { const offer = offerOn(edge); return offer ? <GapInsert offer={offer} /> : null; } } : {})}
      pick={edit?.pick ?? null}
      viewport={canvas ? workflowViewport(t, canvas) : 'none'}
      selection={selection}
      connect={connect}
    />
  );
}

import { type ComponentProps, type CSSProperties, type ReactNode } from 'react';
import { type CanvasOptions, type CanvasSelection } from './canvas/Canvas';
import { type ChipPlaced } from './canvas/marks';
import { type Connect } from './canvas/useConnection';
import { type InkLabel, type InkPath } from './flowInk';
import { type EdgeStyle } from './flowLayout';
import { type IconName, Pill } from './ui';

/** A pill's tone, from the caller's own table (`QUEST_TONE`, `SESSION_TONE`): the flow adds none. */
export type PillTone = NonNullable<ComponentProps<typeof Pill>['tone']>;

/**
 * A mark on a node: a short word the caller says about it, such as a refusal's *To put right* or a
 * proposal's *New*. It is drawn as an outline on the node (the caller's, which takes no room) and a chip beside it where one clears
 * every box; the caller keeps the words in the node's description. A mark never adds a line or a border's width to a node.
 */
export type FlowMark = { word: string; kind: 'refused' | 'new' | 'remove' | 'changed' };

/** One place in the flow, worded by its caller. */
export type FlowNode = {
  id: string;
  title: string;
  glyph?: IconName;
  /** The outcomes of a decision in the order its ports are drawn: its routes' targets start the layout in that order. */
  ports?: readonly string[];
  /** Its state as a pill, in the caller's tone. */
  state?: { word: string; tone: PillTone };
  /** Who acts there: drawn, unless `who` is, and said to a reader. */
  actor?: string | null;
  /**
   * Who acts, as the caller marks it: drawn in `actor`'s place where its words need marks a string cannot carry, a
   * workflow step's part by its shape and its executor's names as code. `actor` stays the words a reader hears.
   */
  who?: ReactNode;
  /** The caller's own lines: its moves, a door. */
  lines?: ReactNode;
  /**
   * A flag on the node: the word of a mark the caller draws beside the step, with the foot it adds, such as where a
   * workflow's work is done and that steps after it still run. Kept apart from `lines`, which a caller fills with its own.
   */
  mark?: { word: string; foot?: string | null };
  /** The marks on it: each a chip placed beside the node, its outline the caller's. */
  marks?: readonly FlowMark[];
  /** False: not reached, drawn dashed with its words at the soft ink. Absent: the record does not say. */
  reached?: boolean;
  /**
   * Drawn as a node not reached is, dashed with its words at the soft ink, without saying it has not been reached: a node outside the
   * record's own, such as the repository that asked a part.
   */
  faint?: boolean;
  current?: boolean;
  /**
   * Done: what a settled stretch folds, said as *n steps done*, once a flow is past 24 nodes (§3.6 step 2). A step
   * passed over is not, so a skip stays in view.
   */
  settled?: boolean;
  /** Its record's door: Enter on the node opens it. */
  open?: () => void;
  /**
   * What makes it like the nodes beside it: three or more with one `key`, each
   * following the one before in a chain, are drawn as one fold, titled by their shared `word` and counted, with the first and the
   * last `at` (worded by the caller, where its record keeps a time). A try's code is one key; an advance is another.
   */
  stack?: { key: string; word: string; at?: string | null };
};

/** One move between two nodes, with its cause. */
export type FlowEdge = {
  id: string;
  from: string;
  to: string;
  /** The whole cause, said in the source's foot and to a reader. */
  says: string;
  /** A word or two, drawn on the edge where it fits. */
  label?: string;
  style?: EdgeStyle;
  /**
   * On the path its record kept: drawn heavier, in ink and never a status hue. Its cause says so in
   * words, for the rail and a reader. What is lit lays nothing out again: the layout never reads it.
   */
  taken?: boolean;
  /**
   * The outcome of a decision this edge is the route of: it leaves that outcome's port, which the caller's node draws
   * with `data-flow-port="<node>:<port>"`, and each port is a path of its own even where two go to one step.
   */
  port?: string;
  /**
   * A problem the driver's judgement found in this way on: said in a chip beside the edge, which never changes the edge's
   * ink, since an edge wears no status hue.
   */
  problem?: string;
  /**
   * Words said to a reader in the source's description and drawn nowhere: a refusal's sentence, which as a line in the
   * source's foot would move every rank below it.
   */
  note?: string;
};

export type FlowProps = {
  /** Names the flow for a reader (*How #q1 moves*). */
  label: string;
  nodes: FlowNode[];
  edges: FlowEdge[];
  entries: string[];
  /** What stands after the flow. */
  end?: ReactNode;
  /**
   * The caller's own rail: drawn where only the rail fits, or the person chose the list, in place of the nodes' list.
   * The parts' strip is one: its buttons show a part, which the node list's feet cannot. The nodes remain the list wherever the
   * drawing fits.
   */
  rail?: ReactNode;
  /**
   * Its sections, read rank by rank under their titles: a workflow's *Before it lands* and *After it lands*, the
   * landing's divider. A flow in sections is never placed left to right.
   */
  sections?: FlowSection[];
  /**
   * Its node track's floor and most, in rem: §3.2's 10rem and 16rem unless a caller's node holds more, as a
   * workflow's step holds its settings and its run's facts.
   */
  track?: { least?: number; most?: number };
  className?: string;
};

/** One press in a node's foot: the edge out of it, said in words, and the port it leaves where it is a decision's route. */
export type NodeFoot = { to: string; back: boolean; text: string; port?: string };

/** What the canvas hands the node a caller draws: the node whole, its stop, its feet and its description, in reading order. */
export type NodeContext = {
  node: FlowNode;
  /** Whether it holds the flow's one Tab stop. */
  stop: boolean;
  feet: NodeFoot[];
  description: string;
  onFollow: (from: string, to: string) => void;
  style?: CSSProperties;
  /** In pick mode, whether this node can take the way being chosen; absent outside it. */
  picking?: boolean;
};

/**
 * Pick mode: choosing where a way goes on the chart. The keys walk only the nodes that `can` take it, the first of
 * them taking the focus when the mode starts (a new `key`); Enter picks the node, Escape cancels, and a press never focuses one that cannot.
 */
export type FlowPick = { key: string; can: (id: string) => boolean; onPick: (id: string) => void; onCancel: () => void };

/** What the canvas hands the chip a caller draws on a path: what it would say by default, the edge it carries, and how many are merged. */
export type LabelContext = { id: string; style: EdgeStyle; text: string; count: number; edge: FlowEdge | undefined };

export type FlowCanvasProps = FlowProps & {
  /** Draws a node where `FlowNodeBox` (the generic box) is not wanted: it must set `data-flow-node` on its element and take the stop's `tabIndex`. */
  renderNode: (context: NodeContext) => ReactNode;
  /** Draws the chip on a path, or none (null): the default is the path's text. */
  renderLabel?: (context: LabelContext) => ReactNode;
  /**
   * The node selected, or none (null): the data wires of a flow that is given this prop are drawn while one of their ends is
   * selected, and their lanes are kept free at the right whether or not one is. A flow not given it draws no data wire.
   */
  selected?: string | null;
  /**
   * The ＋ an editor puts on a way, or none (null): drawn on the edge's path where it clears every node, label and other ＋
   * (`placeInserts`), and under its source in the rail. A way back and a path that merges several edges take none.
   */
  renderInsert?: (edge: FlowEdge) => ReactNode | null;
  /** Pick mode, while a way's target is being chosen on the chart. */
  pick?: FlowPick | null;
  /**
   * The canvas's viewport, which a caller opts into: from 36rem of the pane the drawing stands in a region that pans and
   * zooms, and a drawing wider than the pane is drawn top to bottom and pans rather than turning to the rail. Below the floor, and where
   * none is given (`'none'`, an inline flow in a scrolling page), the flow is placed as it is without a canvas.
   */
  viewport?: 'none' | CanvasOptions;
  /** What a press on the ground and a Shift+drag box do; the node's own press is the caller's. */
  selection?: CanvasSelection | null;
  /** The connection line from a port, which works with or without the viewport. */
  connect?: Connect | null;
  /**
   * A press on a way, for a flow whose edges are pressable: each path wears a wide invisible stroke that takes the press,
   * under nodes and controls. The workflow's edges are inert but for their ＋.
   */
  onEdge?: (edge: FlowEdge) => void;
};

/**
 * A section of a flow, from the rank of its node `from`. The first heads the flow with its `note`, which a reader
 * hears; each later one divides the drawing before its rank, a mark the eye reads, and a reader hears its title at its first node.
 * A caller's own rail carries its own titles.
 */
export type FlowSection = { title: string; from: string; note?: ReactNode };

/** The drawn geometry: its paths, labels and ＋s, the drawing's size, and how many times it has been drawn. */
export type Ink = { paths: InkPath[]; labels: InkLabel[]; inserts: InkLabel[]; chips: ChipPlaced[]; width: number; height: number; drawn: number };

import type { Meta, StoryObj } from '@storybook/react-vite';
import { useLayoutEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { FlowNodeBox } from '../FlowNodeBox';
import { FlowCanvas, type FlowNode } from '../FlowCanvas';
import { DENSE, type Fixture, LIFECYCLE, type Said } from '../flowFixtures';
import { inDark } from '../flowFrames';
import { chinese } from '../storyLanguage';
import { storyWidths } from '../storyWidths';
import { useRootRem, useWidth } from '../useWidth';
import { Canvas, CANVAS_FLOOR, type CanvasHandle, type CanvasOptions } from './Canvas';
import { syntheticFlow } from './canvasFixtures';
import { observeWorld, readWorld } from './worldBoxes';

// The canvas's stories (CANVAS1a; D224, the canvas design §2.6, §5): a flow standing in the viewport at the main area's 80, 39 and 25rem,
// in English and 中文 and both themes. At 25rem, below the 36rem floor, the flow is placed as it always was. The dense graph is drawn whole
// once *Show whole* is pressed and pans at 80rem; the synthetic worlds of 100, 500 and 2,000 nodes are what the frame times are measured
// on (`measured/canvas.spec.ts`). Keyboard: Tab reaches the toolbar, then the drawing's one stop; the arrows walk it and the view follows.

/** A fixture worded in the reader's language and drawn in the canvas, read or edited. */
function CanvasFlow({ fixture, mode = 'display', marked = false }: { fixture: Fixture | number; mode?: 'display' | 'edit'; marked?: boolean }) {
  const { t, i18n } = useTranslation();
  const marks = marked ? [
    [{ word: t('workflow.edit.chart.markRefused'), kind: 'refused' as const }], [{ word: t('workflow.edit.proposal.mark.added'), kind: 'new' as const }],
    [{ word: t('workflow.edit.proposal.mark.removed'), kind: 'remove' as const }], [{ word: t('workflow.edit.proposal.mark.changed'), kind: 'changed' as const }],
  ] : [];
  const say = (words: Said) => (i18n.language.startsWith('zh') ? words.zh : words.en);
  const viewport: CanvasOptions = {
    mode, zoom: mode === 'edit' ? { min: 0.8, max: 2 } : { min: 0.5, max: 2 }, fit: t('workflow.canvas.fit'), moves: t('workflow.canvas.moves'),
  };
  const marked12 = (nodes: FlowNode[]) => nodes.map((node, at) => (marks[at - 1] ? { ...node, marks: marks[at - 1]! } : node));
  const flow = typeof fixture === 'number' ? { label: `${fixture} steps`, ...syntheticFlow(fixture) } : {
    label: say(fixture.label),
    entries: fixture.entries,
    nodes: fixture.nodes.map((node) => ({
      id: node.id, title: say(node.title), current: node.current, reached: node.reached, settled: node.settled,
      state: node.state && { word: say(node.state.word), tone: node.state.tone },
    })),
    edges: fixture.edges.map((edge) => ({
      id: edge.id, from: edge.from, to: edge.to, says: say(edge.says), label: edge.label && say(edge.label), style: edge.style,
    })),
  };
  return <FlowCanvas {...flow} nodes={marked12(flow.nodes)} viewport={viewport} renderNode={(context) => <FlowNodeBox {...context} />} />;
}

const meta: Meta<typeof CanvasFlow> = {
  title: 'Design/Canvas',
  component: CanvasFlow,
  args: { fixture: LIFECYCLE },
  decorators: [storyWidths.wide],
};
export default meta;

type Story = StoryObj<typeof CanvasFlow>;

/** The node lifecycle, top to bottom with its returns on their lanes, in the viewport at 80 and 39rem; at 25rem the rail, as today. */
export const Lifecycle: Story = {};
export const LifecycleHalf: Story = { decorators: [storyWidths.half] };
export const LifecycleNarrow: Story = { decorators: [storyWidths.narrow] };
export const LifecycleChinese: Story = { decorators: [storyWidths.wide, chinese] };
export const LifecycleChineseHalf: Story = { decorators: [storyWidths.half, chinese] };
export const LifecycleChineseNarrow: Story = { decorators: [storyWidths.narrow, chinese] };
export const LifecycleDark: Story = { decorators: [storyWidths.wide, inDark] };
export const LifecycleDarkHalf: Story = { decorators: [storyWidths.half, inDark] };
export const LifecycleChineseDarkNarrow: Story = { decorators: [storyWidths.narrow, chinese, inDark] };
/** Edited: zoom floored at 80%, so the ＋ and the ports stay usable. */
export const LifecycleEdit: Story = { args: { mode: 'edit' } };

/** Twelve steps plain, then with a mark of each kind on four of them: a chip beside each where it clears every box, no node moved. */
export const Plain12: Story = { args: { fixture: 12 } };
export const Marked: Story = { args: { fixture: 12, marked: true } };
export const MarkedHalf: Story = { args: { fixture: 12, marked: true }, decorators: [storyWidths.half] };
export const MarkedNarrow: Story = { args: { fixture: 12, marked: true }, decorators: [storyWidths.narrow] };
export const MarkedChinese: Story = { args: { fixture: 12, marked: true }, decorators: [storyWidths.wide, chinese] };
export const MarkedChineseHalf: Story = { args: { fixture: 12, marked: true }, decorators: [storyWidths.half, chinese] };
export const MarkedDark: Story = { args: { fixture: 12, marked: true }, decorators: [storyWidths.wide, inDark] };
export const MarkedChineseDarkNarrow: Story = { args: { fixture: 12, marked: true }, decorators: [storyWidths.narrow, chinese, inDark] };

/** The dense graph at the bounds: folded after its entries, and drawn whole once *Show whole* is pressed, wider than 80rem, so it pans. */
export const Dense: Story = { args: { fixture: DENSE } };
export const DenseChineseDark: Story = { args: { fixture: DENSE }, decorators: [storyWidths.wide, chinese, inDark] };

/** Synthetic placed worlds, *Show whole* pressed to draw them whole: what the frame times are measured on. */
export const Synthetic100: Story = { args: { fixture: 100 } };
export const Synthetic500: Story = { args: { fixture: 500 } };

/**
 * 2,000 cards of one size in a grid laid out by CSS alone, standing in the canvas: the viewport measured at a size past any placed
 * world's bound (a workflow's 24 steps, a run's 96), whose ranked layout is not what is timed here.
 */
function GridWorld({ count }: { count: number }) {
  const { t } = useTranslation();
  const pane = useRef<HTMLDivElement>(null);
  const grid = useRef<HTMLDivElement>(null);
  const handle = useRef<CanvasHandle>(null);
  const width = useWidth(pane);
  const rem = useRootRem();
  const active = width !== undefined && width >= CANVAS_FLOOR * rem - 0.5;
  useLayoutEffect(() => {
    const element = grid.current;
    if (!element || !active) return undefined;
    return observeWorld(element, () => { const read = readWorld(element); if (read) handle.current?.measured(read); });
  }, [active, width]);
  return (
    <Canvas ref={handle} active={active} options={{ mode: 'display', zoom: { min: 0.5, max: 2 }, fit: t('workflow.canvas.fit'), moves: t('workflow.canvas.moves') }}
      label={`${count} cards`} width={width} rem={rem} pane={pane} paneProps={{ 'data-flow-pane': '', 'data-placement': 'down' }} tools={null}
      showTools={false} entry="g0">
      <div ref={grid} data-drawn="1" role="list" className="grid w-max grid-cols-[repeat(10,10rem)] gap-6 p-4">
        {Array.from({ length: count }, (_, index) => (
          <div key={index} role="listitem" data-flow-node={`g${index}`} tabIndex={index === 0 ? 0 : -1}
            className="h-15 rounded-card border border-line bg-raised p-2 text-small text-ink">
            {`Step ${index + 1}`}
          </div>
        ))}
      </div>
    </Canvas>
  );
}

export const Grid2000: StoryObj<typeof GridWorld> = { render: () => <GridWorld count={2000} /> };

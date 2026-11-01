import { FlowCanvas, type FlowCanvasProps, type LabelContext, type NodeContext } from '../../design/FlowCanvas';
import { FlowNodeBox } from '../../design/Flow';
import { EdgeChip } from './EdgeChip';
import { HoldsGroup } from './HoldsGroup';
import { StepNode } from './StepNode';
import { Terminal } from './Terminal';
import { isChartNode } from './stepKinds';

// The workflow's flowchart (FLOWCHART2a; D223): the canvas drawing `graphFlow`'s nodes through the chart's components, a node for each
// kind of step, a decision's outcomes as ports, a terminal for each end, the holds as one group, and a chip for each way. A node the
// canvas drew of its own (a fold of a large run's settled stretch) is `Flow`'s box.

function drawNode(context: NodeContext) {
  const { node } = context;
  if (!isChartNode(node)) return <FlowNodeBox {...context} />;
  const { step } = node;
  if (step.terminal) return <Terminal context={context} />;
  if (step.group && !step.group.open) return <HoldsGroup context={context} step={step} />;
  return <StepNode context={context} step={step} />;
}

function drawLabel({ style, text, count, edge }: LabelContext) {
  return <EdgeChip style={style} text={text} count={count} problem={edge?.problem} />;
}

export function FlowChart(props: Omit<FlowCanvasProps, 'renderNode' | 'renderLabel'>) {
  return <FlowCanvas {...props} renderNode={drawNode} renderLabel={drawLabel} />;
}

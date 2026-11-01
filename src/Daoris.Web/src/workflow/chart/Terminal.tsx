import { cn } from '../../lib/cn';
import { Icon, Pill } from '../../design/ui';
import type { NodeContext } from '../../design/FlowCanvas';
import { NodeFrame } from './NodeFrame';
import type { ChartNode, ChartStep } from './stepKinds';

// A terminator (FLOWCHART2a): the flowchart's rounded end. Only ◎ *No further step*, where an outcome has no route, and *Finished*, which
// a run's record alone draws. The configured end is no terminal: it is a mark on its own step.

export function Terminal({ context }: { context: NodeContext }) {
  const { node } = context;
  const step: ChartStep | undefined = (node as ChartNode).step;
  const faint = node.reached === false || node.faint === true;
  return (
    <NodeFrame context={context} shape="pill" data={{ 'data-variant': 'terminal', 'data-terminal': step?.terminal }}>
      {(ids) => (
        <div className="flex min-w-0 flex-wrap items-baseline justify-center gap-x-2 gap-y-1">
          <span id={ids.title} className={cn('min-w-0 text-small font-semibold [overflow-wrap:anywhere]', faint ? 'text-ink-soft' : 'text-ink')}>
            {node.glyph && <Icon name={node.glyph} size={14} className="mr-1.5 inline translate-y-0.5 text-ink-soft" />}
            {node.title}
          </span>
          {node.state && <span id={ids.state}><Pill tone={node.state.tone}>{node.state.word}</Pill></span>}
        </div>
      )}
    </NodeFrame>
  );
}

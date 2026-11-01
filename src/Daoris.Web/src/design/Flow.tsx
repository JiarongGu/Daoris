import { useId } from 'react';
import { cn } from '../lib/cn';
import { FlowCanvas, type FlowProps, type NodeContext } from './FlowCanvas';
import { Icon, Pill } from './ui';

export type { FlowEdge, FlowNode, FlowProps, FlowSection, PillTone } from './FlowCanvas';

/**
 * **A flowchart** (FLOWCHART1a1; D186, the flowchart design §3 and §4): the canvas drawing its nodes as the generic box, a glyph and
 * title, a state's pill, who acts, the caller's lines and a foot of presses. The layout, placement, ink, keys and folds are
 * `FlowCanvas`'s; a caller whose nodes need more (the workflow's steps and their ports) draws them through the canvas itself.
 *
 * A molecule: the flow arrives worded and its doors go out.
 */
export function Flow(props: FlowProps) {
  return <FlowCanvas {...props} renderNode={(context) => <FlowNodeBox {...context} />} />;
}

/** One node (§3.3): its words in reading order, and its foot's presses. */
export function FlowNodeBox({ node, stop, feet, description, onFollow, style }: NodeContext) {
  const titleId = useId();
  const stateId = useId();
  const descriptionId = useId();
  const faint = node.reached === false || node.faint === true;
  return (
    <div
      role="listitem"
      data-flow-node={node.id}
      tabIndex={stop ? 0 : -1}
      aria-current={node.current ? 'step' : undefined}
      aria-labelledby={node.state ? `${titleId} ${stateId}` : titleId}
      aria-describedby={descriptionId}
      style={style}
      className={cn(
        'min-w-0 rounded-card bg-raised px-3 py-2',
        // A state never changes the box: the current node's second pixel is an inset ring, not a wider border (CANVAS1b, trap 8).
        node.current ? 'border border-ink-soft ring-1 ring-inset ring-ink-soft' : faint ? 'border border-dashed border-line-strong' : 'border border-line',
      )}
    >
      <div className="flex min-w-0 flex-wrap items-baseline gap-x-2 gap-y-1">
        <span id={titleId} className={cn('min-w-0 text-body font-semibold [overflow-wrap:anywhere]', faint ? 'text-ink-soft' : 'text-ink')}>
          {node.glyph && <Icon name={node.glyph} size={14} className="mr-1.5 inline translate-y-0.5 text-ink-soft" />}
          {node.title}
        </span>
        {node.state && <span id={stateId}><Pill tone={node.state.tone}>{node.state.word}</Pill></span>}
      </div>
      {node.who
        ? <div className="mt-1 min-w-0 text-small text-ink-soft">{node.who}</div>
        : node.actor && <p className="m-0 mt-1 text-small text-ink-soft [overflow-wrap:anywhere]">{node.actor}</p>}
      {node.lines && <div className="mt-1 min-w-0 text-small text-ink-soft">{node.lines}</div>}
      {node.mark && (
        <p data-flow-mark className="m-0 mt-1 min-w-0 text-small text-ink-soft [overflow-wrap:anywhere]">
          <Icon name="stepFinished" size={12} className="mr-1 inline translate-y-px" />
          <span className="font-semibold">{node.mark.word}</span>
          {node.mark.foot && <span className="block text-meta">{node.mark.foot}</span>}
        </p>
      )}
      {feet.length > 0 && (
        <div className="mt-1.5 grid min-w-0 gap-0.5">
          {feet.map((foot, index) => (
            <button
              key={index}
              type="button"
              tabIndex={-1}
              onClick={() => onFollow(node.id, foot.to)}
              className="cursor-pointer border-0 bg-transparent p-0 text-left text-meta text-ink-soft [overflow-wrap:anywhere] hover:text-ink"
            >
              <span aria-hidden className="mr-1">{foot.back ? '↺' : '→'}</span>{foot.text}
            </button>
          ))}
        </div>
      )}
      <span id={descriptionId} hidden>{description}</span>
    </div>
  );
}

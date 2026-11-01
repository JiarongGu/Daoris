import { type ReactNode, useId } from 'react';
import { cn } from '../../lib/cn';
import type { NodeContext, NodeFoot } from '../../design/FlowCanvas';

// What every node of the chart shares (FLOWCHART2a; the flowchart design §3.3, §3.7): one list item named by its title and state and
// described by every edge in and out, one Tab stop, a border that says current or not reached without a status hue, and the foot's
// presses. The components differ in what they draw inside.

/** The ids a node's words carry, for its name. */
export type FrameIds = { title: string; state: string };

/** A node's frame: `box` for a step, `pill` for a terminal. */
export function NodeFrame({ context, shape = 'box', data, children }: {
  context: NodeContext;
  shape?: 'box' | 'pill';
  /** Attributes of the component, such as its variant. */
  data?: Record<`data-${string}`, string | undefined>;
  children: (ids: FrameIds) => ReactNode;
}) {
  const titleId = useId();
  const stateId = useId();
  const descriptionId = useId();
  const { node, stop, description, style } = context;
  const faint = node.reached === false || node.faint === true;
  return (
    <div
      role="listitem"
      data-flow-node={node.id}
      {...data}
      tabIndex={stop ? 0 : -1}
      aria-current={node.current ? 'step' : undefined}
      aria-labelledby={node.state ? `${titleId} ${stateId}` : titleId}
      aria-describedby={descriptionId}
      style={style}
      className={cn(
        'min-w-0 bg-raised',
        shape === 'pill' ? 'rounded-full px-3 py-1.5' : 'rounded-card px-3 py-2',
        node.current ? 'border-2 border-ink-soft' : faint ? 'border border-dashed border-line-strong' : 'border border-line',
      )}
    >
      {children({ title: titleId, state: stateId })}
      <span id={descriptionId} hidden>{description}</span>
    </div>
  );
}

/** The presses of a node's foot: one for each edge out of it, moving the focus to its target. */
export function NodeFeet({ node, feet, onFollow }: { node: string; feet: NodeFoot[]; onFollow: NodeContext['onFollow'] }) {
  if (feet.length === 0) return null;
  return (
    <div className="mt-1.5 grid min-w-0 gap-0.5">
      {feet.map((foot, index) => (
        <button
          key={index}
          type="button"
          tabIndex={-1}
          onClick={() => onFollow(node, foot.to)}
          className="cursor-pointer border-0 bg-transparent p-0 text-left text-meta text-ink-soft [overflow-wrap:anywhere] hover:text-ink"
        >
          <span aria-hidden className="mr-1">{foot.back ? '↺' : '→'}</span>{foot.text}
        </button>
      ))}
    </div>
  );
}

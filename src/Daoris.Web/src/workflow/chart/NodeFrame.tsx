import { type MouseEvent, type ReactNode, useId } from 'react';
import { useTranslation } from 'react-i18next';
import { cn } from '../../lib/cn';
import type { NodeContext, NodeFoot } from '../../design/FlowCanvas';

// What every node of the chart shares: one list item named by its title and state and
// described by every edge in and out, one Tab stop, a border that says current or not reached without a status hue, and the foot's
// presses. The components differ in what they draw inside. In the editor a press selects the node, the one selected wears an
// outline, and in pick mode the nodes that can take the way wear a dashed one, each said in words to a reader; an outline takes no room.

/** How a press selects: Ctrl toggles the node in or out of the selection, Shift adds it, where many may be chosen. */
export type SelectHow = 'toggle' | 'add';

/** The ids a node's words carry, for its name. */
export type FrameIds = { title: string; state: string };

/** Whether a press landed on a control inside the node, which is the control's own. */
const onControl = (event: MouseEvent) => (event.target as HTMLElement).closest('button, a, input, select, textarea') !== null;

/** A node's frame: `box` for a step, `pill` for a terminal. */
export function NodeFrame({ context, shape = 'box', data, press, selected = false, refused = [], children }: {
  context: NodeContext;
  shape?: 'box' | 'pill';
  /** Attributes of the component, such as its variant. */
  data?: Record<`data-${string}`, string | undefined>;
  /** The press on the node, its controls apart: a plain press, or one with Ctrl (`toggle`) or Shift (`add`) held. */
  press?: (how?: SelectHow) => void;
  selected?: boolean;
  /** What the judgement refused here: an outline in declined's hue (its chip is the canvas's), and each refusal is said to a reader. */
  refused?: readonly string[];
  children: (ids: FrameIds) => ReactNode;
}) {
  const { t } = useTranslation();
  const titleId = useId();
  const stateId = useId();
  const descriptionId = useId();
  const { node, stop, description, style, picking } = context;
  const faint = node.reached === false || node.faint === true;
  const said = [
    description,
    ...refused.map((words) => t('workflow.edit.chart.refusedStep', { words })),
    selected ? t('workflow.edit.chart.selected') : '',
    picking === undefined ? '' : t(picking ? 'workflow.edit.pick.can' : 'workflow.edit.pick.cannot'),
  ].filter(Boolean).join(' ');
  return (
    <div
      role="listitem"
      data-flow-node={node.id}
      {...data}
      data-selected={selected ? 'true' : undefined}
      data-refused={refused.length > 0 ? 'true' : undefined}
      data-picking={picking === undefined ? undefined : String(picking)}
      tabIndex={stop ? 0 : -1}
      aria-current={node.current ? 'step' : undefined}
      aria-labelledby={node.state ? `${titleId} ${stateId}` : titleId}
      aria-describedby={descriptionId}
      style={style}
      onClick={press ? (event) => {
        if (!onControl(event)) press(event.ctrlKey || event.metaKey ? 'toggle' : event.shiftKey ? 'add' : undefined);
      } : undefined}
      className={cn(
        'min-w-0 bg-raised',
        shape === 'pill' ? 'rounded-full px-3 py-1.5' : 'rounded-card px-3 py-2',
        // Every state keeps the one-pixel border, so none moves a box: the second pixel of a refusal or of the current
        // step is an inset ring, which takes no room.
        refused.length > 0 ? 'border border-st-declined ring-1 ring-inset ring-st-declined'
          : node.current ? 'border border-ink-soft ring-1 ring-inset ring-ink-soft'
            : faint ? 'border border-dashed border-line-strong' : 'border border-line',
        press && 'cursor-pointer',
        selected && 'outline outline-2 outline-offset-2 outline-accent',
        picking === true && !selected && 'outline outline-2 outline-offset-2 outline-dashed outline-accent',
      )}
    >
      {children({ title: titleId, state: stateId })}
      <span id={descriptionId} hidden>{said}</span>
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

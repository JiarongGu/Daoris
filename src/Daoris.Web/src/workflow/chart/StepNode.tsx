import { useTranslation } from 'react-i18next';
import { cn } from '../../lib/cn';
import { Icon, Pill } from '../../design/ui';
import type { NodeContext } from '../../design/FlowCanvas';
import type { ChartEdit } from './FlowChart';
import { HoldsPress } from './HoldsGroup';
import { NodeFeet, NodeFrame, type SelectHow } from './NodeFrame';
import { PortRow, splitFeet } from './PortRow';
import { type ChartStep, VARIANT_GLYPH } from './stepKinds';

// One node for each palette kind: the surface is every node's, and the kind is carried by its
// glyph, its who and, for a decision, the row of ports on its lower edge, never by a rotated box, which could not wrap 中文. Its state
// is a pill and a border, in words and never by hue alone; where a run paints one it paints it here without moving the node. In the
// editor a press selects it and a port's press starts that outcome's way.

export function StepNode({ context, step, edit }: { context: NodeContext; step: ChartStep; edit?: ChartEdit }) {
  const { t } = useTranslation();
  const { node, feet, onFollow } = context;
  const faint = node.reached === false || node.faint === true;
  // A kind the step's own glyph names (a landing's method) keeps it; the generic tool gives way to the variant's.
  const glyph = node.glyph && node.glyph !== 'tool' ? node.glyph : VARIANT_GLYPH[step.variant];
  const { ported, rest } = splitFeet(step.ports, feet);
  const onPort = edit?.onPort;
  return (
    <NodeFrame
      context={context}
      data={{ 'data-variant': step.variant }}
      {...(edit ? {
        press: (how?: SelectHow) => (how ? edit.onSelect(node.id, how) : edit.onSelect(node.id)),
        selected: edit.selected === node.id || (edit.chosen?.has(node.id) ?? false),
      } : {})}
      refused={step.refused ?? []}
    >
      {(ids) => (
        <>
          <div className="flex min-w-0 flex-wrap items-baseline gap-x-2 gap-y-1">
            <span id={ids.title} className={cn('min-w-0 text-body font-semibold [overflow-wrap:anywhere]', faint ? 'text-ink-soft' : 'text-ink')}>
              <Icon name={glyph} size={14} className="mr-1.5 inline translate-y-0.5 text-ink-soft" />
              {node.title}
            </span>
            {node.state && <span id={ids.state}><Pill tone={node.state.tone}>{node.state.word}</Pill></span>}
          </div>
          {node.who
            ? <div className="mt-1 min-w-0 text-small text-ink-soft">{node.who}</div>
            : node.actor && <p className="m-0 mt-1 text-small text-ink-soft [overflow-wrap:anywhere]">{node.actor}</p>}
          {node.lines && <div className="mt-1 min-w-0 text-small text-ink-soft">{node.lines}</div>}
          {step.group?.open && <div className="mt-1.5"><HoldsPress count={step.group.count} open onPress={node.open} /></div>}
          {node.mark && (
            <p data-flow-mark className="m-0 mt-1 min-w-0 text-small text-ink-soft [overflow-wrap:anywhere]">
              <Icon name="stepFinished" size={12} className="mr-1 inline translate-y-px" />
              <span className="font-semibold">{node.mark.word}</span>
              {node.mark.foot && <span className="block text-meta">{node.mark.foot}</span>}
            </p>
          )}
          <NodeFeet node={node.id} feet={rest} onFollow={onFollow} />
          <PortRow
            node={node.id}
            ported={ported}
            onFollow={onFollow}
            {...(onPort ? {
              edit: {
                onPress: (outcome: string) => onPort(node.id, outcome),
                label: (outcome: string) => t('workflow.edit.port.choose', { outcome, step: node.title }),
              },
            } : {})}
          />
        </>
      )}
    </NodeFrame>
  );
}

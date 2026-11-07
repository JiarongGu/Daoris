import { useTranslation } from 'react-i18next';
import { Icon, Pill } from '../../design/ui';
import type { NodeContext } from '../../design/FlowCanvas';
import { NodeFrame } from './NodeFrame';
import type { ChartStep } from './stepKinds';

// The declared checks, requests and holds drawn as one node (`graphFlow`'s holds group): folded to its title and its
// members and the press that opens them. Opened, its first member carries the press that folds them again (`HoldsPress`, which `StepNode`
// draws). A caller opens it where a refusal lies inside it, by handing `graphFlow` the group open.

const PRESS = 'cursor-pointer border-0 bg-transparent p-0 text-left text-meta text-ink-soft underline-offset-2 hover:text-ink hover:underline';

/** The press that opens the folded group or folds it again; Enter on the node does the same. */
export function HoldsPress({ count, open, onPress }: { count: number; open: boolean; onPress: (() => void) | undefined }) {
  const { t } = useTranslation();
  if (!onPress) return null;
  return (
    <button type="button" tabIndex={-1} aria-expanded={open} onClick={onPress} className={PRESS}>
      {t(open ? 'flow.stack.close' : 'flow.stack.open', { count })}
    </button>
  );
}

export function HoldsGroup({ context, step }: { context: NodeContext; step: ChartStep }) {
  const { node } = context;
  const faint = node.reached === false || node.faint === true;
  return (
    <NodeFrame context={context} data={{ 'data-variant': step.variant }}>
      {(ids) => (
        <>
          <div className="flex min-w-0 flex-wrap items-baseline gap-x-2 gap-y-1">
            <span id={ids.title} className={`min-w-0 text-body font-semibold [overflow-wrap:anywhere] ${faint ? 'text-ink-soft' : 'text-ink'}`}>
              <Icon name="stepHold" size={14} className="mr-1.5 inline translate-y-0.5 text-ink-soft" />
              {node.title}
            </span>
            {node.state && <span id={ids.state}><Pill tone={node.state.tone}>{node.state.word}</Pill></span>}
          </div>
          {node.lines && <div className="mt-1 min-w-0 text-small text-ink-soft [overflow-wrap:anywhere]">{node.lines}</div>}
          <div className="mt-1.5"><HoldsPress count={step.group?.count ?? 0} open={false} onPress={node.open} /></div>
        </>
      )}
    </NodeFrame>
  );
}

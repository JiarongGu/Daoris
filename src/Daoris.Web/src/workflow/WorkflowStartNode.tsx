import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Flow, type FlowEdge, type FlowNode } from '../design/Flow';
import { Button, Inline } from '../design/ui';
import { type Answered, Refused } from '../work/InlineConfirm';
import type { WorkflowLanded, WorkflowRoute, WorkflowStart } from './current';
import { type RouteScope, whySaid } from './routeSaid';

/** A blob's or a commit's short name, as git abbreviates one. */
const short = (id: string) => id.slice(0, 7);

/** Where the landed route's gates stand against the line's tip, the declaration to accept, and the person's press. */
function LandedLines({ landed, page, onAccept, accepting }: {
  landed: WorkflowLanded;
  page: 'repository' | 'workspace';
  onAccept?: (answered: Answered) => void;
  accepting?: boolean;
}) {
  const { t } = useTranslation();
  const [refused, setRefused] = useState<string | null>(null);
  if (landed.workflow === 'current') return null;
  const tip = landed.tip ?? null;
  const at = tip ? { branch: tip.line, commit: short(tip.commit) } : null;
  const waits = landed.state === 'changed' || landed.state === 'unaccepted';

  let said: string;
  if (landed.state === 'accepted' && at) {
    said = landed.gates === 'none'
      ? t('workflow.start.landed.acceptedNone', at)
      : t('workflow.start.landed.accepted', { ...at, blob: short(landed.gates ?? '') });
  } else if (landed.state === 'changed') {
    said = t('workflow.start.landed.changed');
  } else if (landed.state === 'unaccepted') {
    said = t('workflow.start.landed.unaccepted');
  } else {
    // A workspace has no line: its route's gates are each repository's to accept. Else the driver's own words for why it was not read.
    said = page === 'workspace' ? t('workflow.start.landed.perRepository') : landed.problem ?? t('workflow.start.landed.unread');
  }

  return (
    <div className="grid min-w-0 gap-1">
      <p className="m-0 text-small text-ink-soft"><Inline text={said} /></p>
      {waits && landed.declaration && at && (
        <pre
          aria-label={t('workflow.start.landed.declaration', at)}
          className="m-0 max-h-48 overflow-auto rounded-control border border-line bg-raised px-2 py-1 font-mono text-meta text-ink"
        >
          {landed.declaration}
        </pre>
      )}
      {waits && onAccept && (
        <div className="flex flex-wrap items-center gap-1.5">
          <Button
            variant="ghost"
            className="min-h-0 border border-line px-2 py-0.5 text-small"
            disabled={accepting}
            onClick={() => { setRefused(null); onAccept({ done: () => undefined, refused: setRefused }); }}
          >
            {t('workflow.start.landed.accept')}
          </Button>
        </div>
      )}
      {refused && <Refused sentence={refused} />}
      <p className="m-0 text-meta text-ink-faint">{t('workflow.start.landed.notRun')}</p>
    </div>
  );
}

/**
 * **The start node** (FABRIC-PIPELINE1a; D192 point 2, the pipeline design §1.2, §1.3): drawn first on a Workflow tab, the routes the
 * work that comes here takes, read from the choice table the driver keeps: the default, each kind the workspace declares by its label
 * and paths, and the work that lands, each wired to the workflow it names with what chose it, laid out by D186's `Flow`. A route
 * naming nothing says what follows then: Current, or no verification here.
 *
 * @remarks
 * **The landed route says its gates** (§4.2): the declaration the person accepted by its blob at the line's tip, where it stands
 * against the tip now, the declaration that waits to be accepted, and *Accept the gates*, the person's door (D156), where the page
 * hands one. This build keeps the route and runs no check yet, and says so.
 *
 * A molecule: the answer arrives as a prop, and the one press goes out.
 */
export function WorkflowStartNode({ start, page, scope, onAccept, accepting = false }: {
  start: WorkflowStart;
  page: 'repository' | 'workspace';
  scope: RouteScope;
  /** The person's *Accept the gates*: the landed route's gates at the line's tip, accepted by their blob. */
  onAccept?: (answered: Answered) => void;
  accepting?: boolean;
}) {
  const { t } = useTranslation();
  const headId = useId();
  const title = t('workflow.start.title');

  const nodes: FlowNode[] = [{ id: 'start', title: t('workflow.start.node'), lines: <p className="m-0 text-small text-ink-soft">{t('workflow.start.says')}</p> }];
  const edges: FlowEdge[] = [];
  const targets = new Map<string, FlowNode>();
  const wire = (route: string, title: string, lines: FlowNode['lines'], says: string, target: { id: string; title: string }, why: string) => {
    nodes.push({ id: route, title, lines });
    edges.push({ id: `${route}:in`, from: 'start', to: route, says });
    if (!targets.has(target.id)) targets.set(target.id, { id: target.id, title: target.title });
    edges.push({ id: `${route}:out`, from: route, to: target.id, says: why });
  };
  const named = (route: WorkflowRoute | WorkflowLanded) => ({ id: `to:${route.workflow}`, title: route.name ?? route.workflow });

  for (const route of start.routes) {
    const kind = route.kind === null ? null : route.label ?? route.kind;
    wire(
      `route:${route.kind ?? '*'}`,
      kind ?? t('workflow.start.default'),
      route.paths.length > 0
        ? <p className="m-0 text-small text-ink-soft"><Inline text={t('workflow.start.paths', { paths: route.paths.map((path) => `\`${path}\``).join(t('workflow.listJoin')) })} /></p>
        : undefined,
      kind === null ? t('workflow.start.edge.default') : t('workflow.start.edge.kind', { kind }),
      route.workflow === 'current' ? { id: 'to:current', title: t('workflow.start.current') } : named(route),
      whySaid(t, route.level, scope, kind, false),
    );
  }

  const { landed } = start;
  wire(
    'route:landed',
    t('workflow.start.landed.title'),
    <LandedLines landed={landed} page={page} onAccept={onAccept} accepting={accepting} />,
    t('workflow.start.edge.landed'),
    landed.workflow === 'current' ? { id: 'to:none', title: t('workflow.start.noVerification') } : named(landed),
    whySaid(t, landed.level, scope, null, true),
  );

  return (
    <section aria-labelledby={headId} className="grid min-w-0 gap-2">
      <h3 id={headId} className="m-0 text-body font-semibold text-ink">{title}</h3>
      <Flow label={title} nodes={[...nodes, ...targets.values()]} edges={edges} entries={['start']} />
    </section>
  );
}

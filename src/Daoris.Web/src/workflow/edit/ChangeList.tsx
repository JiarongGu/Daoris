import type { TFunction, i18n as I18n } from 'i18next';
import { useTranslation } from 'react-i18next';
import type { WorkflowGraphChange } from '../../help/ProposalCard';
import { cn } from '../../lib/cn';
import { Button, Inline, Pill } from '../../design/ui';
import type { GraphFlow } from '../graphFlow';
import type { WorkflowPaletteAnswer } from './answers';
import { howSaid, stepTitle } from './editWords';
import { draftOf } from './graphDraft';
import type { DraftProposal, Mark, Union } from './proposalTake';

// Ask Daoris's proposal listed change by change: each change in the page's words, marked as the chart marks
// it, with a door to its step, what taking it brings with it, and *Take* and *Leave*; a change whose base the draft does not hold says
// so, with *Leave* and *Ask again*. A molecule: the rows arrive worded, and every press goes out.

/** How a change stands in the person's review. */
export type RowState = 'open' | 'taken' | 'left' | 'stale';

/** A change as the list shows it. */
export type ChangeRow = {
  id: string;
  said: string;
  mark: Mark;
  /** The step its door shows on the chart, where the chart draws it. */
  step: { id: string; title: string } | null;
  state: RowState;
  /** The other changes taking it brings, in words. */
  with: readonly string[];
  /** The change its take is refused at, where that is another. */
  staleAt: string | null;
};

const record = (value: unknown): Record<string, unknown> =>
  value && typeof value === 'object' && !Array.isArray(value) ? (value as Record<string, unknown>) : {};
const nodesOf = (graph: unknown) => (Array.isArray(record(graph).nodes) ? (record(graph).nodes as unknown[]).map(record) : []);
const wiresOf = (graph: unknown) => (Array.isArray(record(graph).wires) ? (record(graph).wires as unknown[]).map(record) : []);

/** A step's title by its id, from the proposal's graph or, for one it removes, the draft it is proposed against; its id where neither holds it. */
function titleIn(t: TFunction, i18n: I18n, palette: WorkflowPaletteAnswer | null, proposal: DraftProposal, id: string): string {
  const kind = kindOf(proposal, id);
  return kind ? stepTitle(t, i18n, palette, { kind }) : id;
}

/** The kind of a step of the proposal, or of the base where the proposal dropped it. */
function kindOf(proposal: DraftProposal, id: string | null): string | undefined {
  const node = nodesOf(proposal.proposed).find((each) => each.id === id) ?? nodesOf(proposal.base).find((each) => each.id === id);
  return typeof node?.kind === 'string' ? node.kind : undefined;
}

/** A way's fields as their names, an option's by its own. */
const fieldsSaid = (fields: readonly string[]) => fields.map((field) => `\`${field.replace(/^options\./, '')}\``).join(', ');

/** The way a change is of, as the proposal has it, or the base for one it removes. */
function wayOf(proposal: DraftProposal, change: WorkflowGraphChange) {
  const json = change.change === 'removed'
    ? wiresOf(proposal.base).find((each) => each.id === change.way)
    : wiresOf(proposal.proposed).find((each) => each.id === change.way);
  return json ? draftOf({ wires: [json] }).wires[0]! : null;
}

/** The step a change's door shows: its own, a way's source, the end it moves to. */
export function changeStep(proposal: DraftProposal, change: WorkflowGraphChange): string | null {
  if (change.what === 'step') return change.step ?? null;
  if (change.what === 'way') return wayOf(proposal, change)?.from ?? null;
  return change.fields.includes('end') && typeof record(proposal.proposed).end === 'string' ? record(proposal.proposed).end as string : null;
}

/** A change in the page's words, from what it changes: the driver's English is the terminal's. */
export function changeSaid(t: TFunction, i18n: I18n, palette: WorkflowPaletteAnswer | null, proposal: DraftProposal, change: WorkflowGraphChange): string {
  const title = (id: string) => titleIn(t, i18n, palette, proposal, id);
  if (change.what === 'step') {
    const step = title(change.step ?? '');
    if (change.change === 'added') return t('workflow.edit.proposal.said.stepAdded', { step });
    if (change.change === 'removed') return t('workflow.edit.proposal.said.stepRemoved', { step });
    return t('workflow.edit.proposal.said.stepChanged', { step, fields: fieldsSaid(change.fields) });
  }
  if (change.what === 'way') {
    const wire = wayOf(proposal, change);
    const words = { from: wire?.from ? title(wire.from) : '—', to: wire ? title(wire.to) : '—', how: wire ? howSaid(t, wire, kindOf(proposal, wire.from)) : change.kind ?? '' };
    if (change.change === 'added') return t('workflow.edit.proposal.said.wayAdded', words);
    if (change.change === 'removed') return t('workflow.edit.proposal.said.wayRemoved', words);
    return t('workflow.edit.proposal.said.wayChanged', { ...words, fields: fieldsSaid(change.fields) });
  }
  const graph = record(proposal.proposed);
  if (change.fields.includes('end')) {
    return t('workflow.edit.proposal.said.end', { step: title(String(graph.end ?? '')), was: title(String(record(proposal.base).end ?? '')) });
  }
  if (change.fields.includes('entries')) {
    const entries = Array.isArray(graph.entries) ? graph.entries.filter((each): each is string => typeof each === 'string') : [];
    return t('workflow.edit.proposal.said.entries', { steps: entries.map(title).join(t('workflow.listJoin')) });
  }
  return t('workflow.edit.proposal.said.cleanup');
}

const TONE = { added: 'open', removed: 'declined', changed: 'taken' } as const;

/** The sign a mark wears beside its word: added, removed, changed. */
export const MARK_SIGN: Record<Mark, string> = { added: '+', removed: '−', changed: '~' };

/**
 * The union drawn as the proposal marks it: a step new, to remove or changed wears the word in a pill, a step to remove drawn
 * dashed besides; a way new or to remove says so in its cause, which the rail and a reader hear, and the chart's chip shows beside it.
 */
export function withChanges(t: TFunction, flow: GraphFlow, union: Union | null): GraphFlow {
  if (!union || (union.steps.size === 0 && union.ways.size === 0)) return flow;
  return {
    ...flow,
    nodes: flow.nodes.map((node) => {
      const mark = union.steps.get(node.id);
      return mark
        ? { ...node, state: { word: t(`workflow.edit.proposal.mark.${mark}`), tone: TONE[mark] }, ...(mark === 'removed' ? { faint: true } : {}) }
        : node;
    }),
    edges: flow.edges.map((edge) => {
      const mark = union.ways.get(edge.id);
      return mark ? { ...edge, says: t('workflow.edit.proposal.waySays', { says: edge.says, mark: t(`workflow.edit.proposal.mark.${mark}`) }) } : edge;
    }),
  };
}

export function ChangeList({ rows, busy = false, onTake, onLeave, onAskAgain, onShow }: {
  rows: readonly ChangeRow[];
  busy?: boolean;
  onTake: (id: string) => void;
  onLeave: (id: string) => void;
  /** Ask Daoris filled in with what no longer fits; absent where this window has no Ask Daoris. */
  onAskAgain?: () => void;
  onShow: (step: string) => void;
}) {
  const { t } = useTranslation();
  const small = 'min-h-0 border border-line py-0.5 text-small';
  return (
    <ul aria-label={t('workflow.edit.proposal.changes')} className="m-0 grid min-w-0 list-none gap-1.5 p-0">
      {rows.map((row) => (
        <li key={row.id} data-change={row.id} data-change-state={row.state}
          className={cn('grid min-w-0 gap-1 border-l-2 py-0.5 pl-2.5', row.state === 'stale' ? 'border-st-declined' : row.state === 'open' ? 'border-accent' : 'border-line')}>
          <div className="flex min-w-0 flex-wrap items-baseline gap-x-2 gap-y-1">
            <span className="flex items-baseline gap-1">
              <span aria-hidden className="font-mono text-small text-ink-soft">{MARK_SIGN[row.mark]}</span>
              <Pill tone={TONE[row.mark]}>{t(`workflow.edit.proposal.mark.${row.mark}`)}</Pill>
            </span>
            <span className={cn('min-w-0 text-small [overflow-wrap:anywhere]', row.state === 'left' ? 'text-ink-soft line-through' : 'text-ink')}>
              <Inline text={row.said} />
            </span>
            {row.state !== 'open' && <Pill tone={row.state === 'stale' ? 'declined' : 'neutral'}>{t(`workflow.edit.proposal.mark.${row.state}`)}</Pill>}
          </div>
          {row.state === 'stale' && (
            <p className="m-0 text-small text-ink-danger">
              <Inline text={row.staleAt ? t('workflow.edit.proposal.staleAt', { change: row.staleAt }) : t('workflow.edit.proposal.stale')} />
            </p>
          )}
          {row.state !== 'taken' && row.state !== 'stale' && row.with.length > 0 && (
            <p className="m-0 text-meta text-ink-soft"><Inline text={t('workflow.edit.proposal.with', { changes: row.with.join(t('workflow.listJoin')) })} /></p>
          )}
          <div className="flex flex-wrap items-center gap-1.5">
            {row.step && (
              <Button variant="ghost" className="min-h-0 px-1 py-0.5 text-small text-accent" aria-label={t('workflow.edit.proposal.show', { step: row.step.title })}
                onClick={() => onShow(row.step!.id)}>
                {row.step.title}
              </Button>
            )}
            {(row.state === 'open' || row.state === 'left') && (
              <Button variant="ghost" className={small} disabled={busy} aria-label={t('workflow.edit.proposal.takeOne', { change: row.said.replace(/`/g, '') })}
                onClick={() => onTake(row.id)}>
                {t('workflow.edit.proposal.act.take')}
              </Button>
            )}
            {(row.state === 'open' || row.state === 'stale') && (
              <Button variant="ghost" className={small} disabled={busy} aria-label={t('workflow.edit.proposal.leaveOne', { change: row.said.replace(/`/g, '') })}
                onClick={() => onLeave(row.id)}>
                {t('workflow.edit.proposal.act.leave')}
              </Button>
            )}
            {row.state === 'stale' && onAskAgain && (
              <Button variant="ghost" className={small} disabled={busy} onClick={onAskAgain}>{t('workflow.edit.proposal.act.askAgain')}</Button>
            )}
          </div>
        </li>
      ))}
    </ul>
  );
}

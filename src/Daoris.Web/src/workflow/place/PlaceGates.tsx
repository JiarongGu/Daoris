import { useId, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button } from '../../design/ui';
import type { IntegrationRequest } from '../../settings/repository/IntegrationDeclaration';
import { type Answered, InlineConfirm, Refused } from '../../work/InlineConfirm';
import type { WorkflowIntegration, WorkflowLanded } from '../current';
import { Acceptances } from './Acceptances';

const short = (id: string) => id.slice(0, 7);

/** A purpose the declaration is accepted for. */
type Purpose = 'landed' | 'integration';

/** What a purpose's acceptance reads as: its words, the blob it would pin, and whether it waits for the person. */
type Standing = { waits: boolean; said: string; blob: string | null };

type Tip = { line: string; commit: string; gates: string } | null | undefined;

const at = (tip: Tip) => (tip ? { branch: tip.line, commit: short(tip.commit) } : null);

/** Where the landed route's gates stand, in this section's words; nothing where the route names no verification. */
function landedStanding(t: ReturnType<typeof useTranslation>['t'], landed: WorkflowLanded): Standing | null {
  if (landed.workflow === 'current') return null;
  const where = at(landed.tip);
  const waits = landed.state === 'changed' || landed.state === 'unaccepted';
  if (landed.state === 'accepted' && where) {
    return {
      waits: false, blob: null,
      said: landed.gates === 'none' ? t('workflow.place.gates.landed.acceptedNone', where) : t('workflow.place.gates.landed.accepted', { ...where, blob: short(landed.gates ?? '') }),
    };
  }
  if (waits && where && landed.tip) {
    const waiting = t('workflow.place.gates.landed.waits', { ...where, blob: short(landed.tip.gates) });
    return { waits, blob: landed.tip.gates, said: landed.state === 'changed' ? `${t('workflow.place.gates.landed.changed')} ${waiting}` : waiting };
  }
  return { waits: false, blob: null, said: landed.problem ?? t('workflow.place.gates.landed.unread') };
}

/** Where the integration declaration stands, in this section's words. */
function integrationStanding(t: ReturnType<typeof useTranslation>['t'], integration: WorkflowIntegration): Standing {
  const where = at(integration.tip);
  const waits = integration.state === 'changed' || integration.state === 'unaccepted';
  if (integration.state === 'accepted' && where) {
    return {
      waits: false, blob: null,
      said: integration.gates === 'none'
        ? t('workflow.place.gates.integration.acceptedNone', where)
        : t('workflow.place.gates.integration.accepted', { ...where, blob: short(integration.gates ?? '') }),
    };
  }
  if (waits && where && integration.tip) {
    const waiting = t('workflow.place.gates.integration.waits', { ...where, blob: short(integration.tip.gates) });
    return {
      waits, blob: integration.tip.gates,
      said: integration.state === 'changed'
        ? `${t('workflow.place.gates.integration.changed', { blob: short(integration.gates ?? '') })} ${waiting}`
        : waiting,
    };
  }
  return { waits: false, blob: null, said: integration.problem ?? t('workflow.place.gates.integration.unread') };
}

/** Whether either declaration waits for the person: never accepted, or changed since. */
export const gatesOwed = (landed: WorkflowLanded | null, integration: WorkflowIntegration | null): boolean =>
  (landed !== null && landed.workflow !== 'current' && (landed.state === 'changed' || landed.state === 'unaccepted'))
  || (integration !== null && (integration.state === 'changed' || integration.state === 'unaccepted'));

/**
 * Gates folded to one line: that something waits for the person, where the accepted declarations stand, that a tip could not be read, or
 * that nothing here asks for an acceptance.
 */
export function gatesSummary(t: ReturnType<typeof useTranslation>['t'], landed: WorkflowLanded | null, integration: WorkflowIntegration | null): string {
  if (gatesOwed(landed, integration)) return t('workflow.place.gates.summary.waits');
  const verifying = landed !== null && landed.workflow !== 'current' ? landed : null;
  const blobs = [verifying?.state === 'accepted' ? verifying.gates : null, integration?.state === 'accepted' ? integration.gates : null]
    .filter((each): each is string => each !== null && each !== 'none');
  if (blobs.length > 0) return t('workflow.place.gates.summary.accepted', { blob: short(blobs[0]!) });
  if (verifying?.state === 'accepted' || integration?.state === 'accepted') return t('workflow.place.gates.summary.acceptedNone');
  if (verifying || integration) return t('workflow.place.gates.summary.unread');
  return t('workflow.place.gates.summary.none');
}

/** One acceptance pressed, awaited: its answer is its record's own, never another purpose's. */
const pressed = (press: (answered: Answered) => void) =>
  new Promise<string | null>((resolve) => press({ done: () => resolve(null), refused: (sentence) => resolve(sentence) }));

/**
 * **Gates** (AGENTSPLACE1h; the Agents-place design §3): the declaration at the line's tip shown once, and its acceptance as two
 * choices, *Accept for landed verification* and *Accept for integration*. Each names its purpose, the blob it pins and the standing it
 * leaves, keeps its own record and, for integration, its own withdrawal; one confirmation applies the ones chosen, each by the blob
 * that was read, so a tip that moved is refused for that purpose alone and the other stands.
 *
 * @remarks
 * Changed landed gates stop verification; changed integration declarations go on under the last accepted blob (the standings say each).
 * The acceptance is the person's alone (D156, D157 point 10): nothing here takes it for a role. The page judges nothing: a refusal is
 * the driver's sentence, said inside the confirmation.
 *
 * A molecule: the standings arrive as props and each acceptance goes out as its own press.
 */
export function PlaceGates({ landed, integration, busy = false, bare = false, onAcceptLanded, onAcceptIntegration }: {
  landed: WorkflowLanded | null;
  integration: WorkflowIntegration | null;
  busy?: boolean;
  /** Drawn inside a fold that carries the name *Gates*: no heading of its own. */
  bare?: boolean;
  /** The landed route's acceptance at the line's tip, by its blob (`WORKFLOW_LANDED`); none, and the choice is not offered. */
  onAcceptLanded?: (answered: Answered) => void;
  /** The integration declaration's acceptance or withdrawal (`INTEGRATION_GATES`); none, and the choice is not offered. */
  onAcceptIntegration?: (request: IntegrationRequest, answered: Answered) => void;
}) {
  const { t } = useTranslation();
  const headId = useId();
  // A tick is the key of what it was made at (purpose, line and blob), and starts empty.
  const [ticks, setTicks] = useState<Partial<Record<Purpose, string>>>({});
  const [asking, setAsking] = useState(false);
  const [withdrawn, setWithdrawn] = useState<string | null>(null);

  const rows: { purpose: Purpose; standing: Standing; label: string; why: string; text: string | null | undefined; tip: Tip; press?: () => Promise<string | null> }[] = [];
  const landedAt = landed && landedStanding(t, landed);
  if (landed && landedAt) {
    rows.push({
      purpose: 'landed', standing: landedAt, label: t('workflow.place.gates.landed.label'), why: t('workflow.place.gates.landed.purpose'),
      text: landed.declaration, tip: landed.tip,
      press: onAcceptLanded ? () => pressed(onAcceptLanded) : undefined,
    });
  }
  if (integration) {
    const standing = integrationStanding(t, integration);
    rows.push({
      purpose: 'integration', standing, label: t('workflow.place.gates.integration.label'), why: t('workflow.place.gates.integration.purpose'),
      text: integration.declaration, tip: integration.tip,
      press: onAcceptIntegration && integration.tip ? () => pressed((answered) => onAcceptIntegration({ gates: integration.tip!.gates }, answered)) : undefined,
    });
  }

  const keyOf = (row: (typeof rows)[number]) => `${row.purpose}|${row.tip?.line ?? ''}|${row.standing.blob ?? ''}`;
  const offered = rows.filter((row) => row.standing.waits && row.press);
  const picked = offered.filter((row) => ticks[row.purpose] === keyOf(row));
  const cleared = offered.filter((row) => ticks[row.purpose] !== undefined && ticks[row.purpose] !== keyOf(row)).map((row) => t('workflow.place.gates.cleared', { label: row.label }));
  const kept = integration !== null && integration.gates !== null && (integration.state === 'accepted' || integration.state === 'changed');

  const confirm = async (answered: Answered) => {
    const refusals: string[] = [];
    for (const row of picked) {
      const refusal = await row.press!();
      if (refusal) refusals.push(`${row.label}: ${refusal}`);
    }
    if (refusals.length === 0) {
      answered.done();
      setTicks({});
      setAsking(false);
    } else answered.refused(refusals.join(' '));
  };

  const withdraw = () => {
    if (!onAcceptIntegration) return;
    setWithdrawn(null);
    onAcceptIntegration({ clear: true }, { done: () => undefined, refused: setWithdrawn });
  };

  return (
    <section aria-labelledby={bare ? undefined : headId} className="grid min-w-0 gap-2">
      {!bare && <h3 id={headId} className="m-0 text-body font-semibold text-ink">{t('workflow.place.gates.title')}</h3>}
      {rows.length === 0
        ? <p className="m-0 text-small text-ink-soft">{t('workflow.place.gates.nothing')}</p>
        : <p className="m-0 text-small text-ink-soft">{t('workflow.place.gates.body')}</p>}
      <Acceptances
        rows={rows.map((row) => ({
          id: row.purpose,
          label: row.label,
          why: row.why,
          said: row.standing.said,
          offered: Boolean(row.standing.waits && row.press),
          text: row.standing.waits ? row.text : null,
          textLabel: t('workflow.place.gates.declaration', at(row.tip) ?? { branch: '', commit: '' }),
          extra: row.purpose === 'integration' && kept && onAcceptIntegration
            ? (
              <span className="flex flex-wrap gap-1.5 pt-0.5">
                <Button variant="ghost" className="min-h-0 border border-line px-2 py-0.5 text-small" disabled={busy} onClick={withdraw}>
                  {t('workflow.place.gates.withdraw')}
                </Button>
              </span>
            )
            : undefined,
        }))}
        ticked={picked.map((row) => row.purpose)}
        onTick={(purpose, on) => setTicks((was) => ({ ...was, [purpose]: on ? keyOf(rows.find((row) => row.purpose === purpose)!) : undefined }))}
        disabled={busy || asking}
        cleared={cleared}
      />
      {withdrawn && <Refused sentence={withdrawn} />}
      {offered.length > 0 && !asking && (
        <div className="flex flex-wrap items-center gap-1.5">
          <Button variant="ghost" className="min-h-0 border border-line px-2.5 py-0.5 text-small" disabled={busy || picked.length === 0} onClick={() => setAsking(true)}>
            {t('workflow.place.gates.confirm.open')}
          </Button>
          {picked.length === 0 && <span className="text-meta text-ink-faint">{t('workflow.place.gates.pick')}</span>}
        </div>
      )}
      {asking && (
        <InlineConfirm
          tone="primary"
          block
          busy={busy}
          label={t('workflow.place.gates.confirm.label')}
          says={t('workflow.place.gates.confirm.says')}
          meanIt={t('workflow.place.gates.confirm.meanIt')}
          onConfirm={confirm}
          onClose={() => setAsking(false)}
        />
      )}
    </section>
  );
}

import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import { DeclarationAcceptance, declarationOwed, declarationStanding, type IntegrationRequest } from '../../settings/repository/DeclarationAcceptance';
import type { Answered } from '../../work/InlineConfirm';
import type { WorkflowIntegration, WorkflowLanded } from '../current';

const start = (landed: WorkflowLanded, integration: WorkflowIntegration | null) => ({ landed, integration });

/** Whether the line's declaration waits for the person, for either purpose it serves. */
export const gatesOwed = (landed: WorkflowLanded | null, integration: WorkflowIntegration | null): boolean =>
  landed !== null && declarationOwed(start(landed, integration));

/**
 * Gates folded to one line: that the line's checks wait for the person, that they are accepted, that the tip could not be read, or that
 * nothing here asks for an acceptance.
 */
export function gatesSummary(t: ReturnType<typeof useTranslation>['t'], landed: WorkflowLanded | null, integration: WorkflowIntegration | null): string {
  const standing = landed ? declarationStanding(start(landed, integration)) : integration;
  if (standing === null) return t('workflow.place.gates.summary.none');
  if (standing.state === 'changed' || standing.state === 'unaccepted') return t('workflow.place.gates.summary.waits');
  if (standing.state === 'accepted') return t(standing.gates === 'none' ? 'workflow.place.gates.summary.acceptedNone' : 'workflow.place.gates.summary.accepted');
  return t('workflow.place.gates.summary.unread');
}

/**
 * **Gates**: the line's declaration accepted once, for every purpose it serves. What changed shows as checks added, removed and
 * changed with each one's command; one *Accept* presses it and one *Withdraw* takes it back.
 *
 * @remarks
 * The acceptance is the person's alone: nothing here takes it for a role. The page judges nothing: a refusal is the driver's sentence.
 * A molecule: the standing arrives as props and the press goes out.
 */
export function PlaceGates({ repository = '', landed, integration, busy = false, bare = false, onAcceptIntegration }: {
  repository?: string;
  landed: WorkflowLanded | null;
  integration: WorkflowIntegration | null;
  busy?: boolean;
  /** Drawn inside a fold that carries the name *Gates*: no heading of its own. */
  bare?: boolean;
  /** The acceptance or its withdrawal (`INTEGRATION_GATES`); none, and the choice is not offered. */
  onAcceptIntegration?: (request: IntegrationRequest, answered: Answered) => void;
}) {
  const { t } = useTranslation();
  const headId = useId();
  const standing = landed ? declarationStanding(start(landed, integration)) : integration;

  return (
    <section aria-labelledby={bare ? undefined : headId} className="grid min-w-0 gap-2">
      {!bare && <h3 id={headId} className="m-0 text-body font-semibold text-ink">{t('workflow.place.gates.title')}</h3>}
      {standing === null
        ? <p className="m-0 text-small text-ink-soft">{t('workflow.place.gates.nothing')}</p>
        : (
          <>
            <p className="m-0 text-small text-ink-soft">{t('workflow.place.gates.body')}</p>
            <DeclarationAcceptance repository={repository} standing={standing} busy={busy} onAccept={onAcceptIntegration ?? (() => undefined)} />
          </>
        )}
    </section>
  );
}

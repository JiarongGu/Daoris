import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Inline } from '../../design/ui';
import { type Answered, Refused } from '../../work/InlineConfirm';
import type { DeclarationChange, WorkflowIntegration, WorkflowLanded, WorkflowStart } from '../../workflow/current';

/** What the person's press sends: the blob of the declaration they read, or a withdrawal. */
export type IntegrationRequest = { gates: string } | { clear: true };

const waitsOf = (state: string | undefined) => state === 'changed' || state === 'unaccepted';

/**
 * Where the line's declaration stands as one acceptance for every purpose it serves: the integration's standing where it waits, else the
 * landed route's where that route verifies and its gates wait, else the integration's. Null where the page has neither.
 */
export function declarationStanding(start: Pick<WorkflowStart, 'landed' | 'integration'>): WorkflowIntegration | null {
  const integration = start.integration ?? null;
  if (integration && waitsOf(integration.state)) return integration;
  const landed: WorkflowLanded = start.landed;
  if (landed.workflow !== 'current' && (waitsOf(landed.state) || (integration === null && landed.state === 'accepted'))) {
    return { gates: landed.gates, state: landed.state, tip: landed.tip, problem: landed.problem, declaration: landed.declaration, change: landed.change };
  }
  return integration;
}

/** Whether the line's declaration waits for the person's acceptance, for either purpose. */
export const declarationOwed = (start: Pick<WorkflowStart, 'landed' | 'integration'> | undefined | null): boolean =>
  start ? waitsOf(declarationStanding(start)?.state) : false;

/** The change as checks added, removed and changed by name with what each runs, and the other parts of the file that changed. */
export function DeclarationChangeWords({ change }: { change: DeclarationChange }) {
  const { t } = useTranslation();
  const lines = [
    ...change.added.map((each) => ({ key: `added:${each.name}`, text: t('settings.integration.change.added', { name: each.name, run: each.run }) })),
    ...change.removed.map((each) => ({ key: `removed:${each.name}`, text: t('settings.integration.change.removed', { name: each.name, run: each.run }) })),
    ...change.changed.map((each) => ({
      key: `changed:${each.name}`,
      text: each.was
        ? t('settings.integration.change.changedFrom', { name: each.name, run: each.run, was: each.was })
        : t('settings.integration.change.changed', { name: each.name, run: each.run }),
    })),
    ...change.other.map((key) => ({ key: `other:${key}`, text: t('settings.integration.change.other', { key }) })),
  ];
  if (lines.length === 0) return <p className="m-0 text-small text-ink-soft">{t('settings.integration.change.none')}</p>;
  return (
    <ul className="m-0 grid list-none gap-0.5 p-0 text-small text-ink">
      {lines.map((line) => <li key={line.key} className="[overflow-wrap:anywhere]"><Inline text={line.text} /></li>)}
    </ul>
  );
}

/**
 * **The line's declaration, accepted once**: where the acceptance of the repository's `daoris.gates.json` stands, what it changed in the
 * person's words (checks added, removed and changed, each with its command), the file as written folded under it, and *Accept* and
 * *Withdraw*. One press accepts it for every purpose it serves, landed verification and integration alike.
 *
 * @remarks
 * *Accept* sends the blob of the declaration shown, so a tip that moved is refused in the driver's sentence, said under the press.
 * The page judges nothing and says no blob id. A molecule: the standing arrives as props and the press goes out.
 */
export function DeclarationAcceptance({ repository, standing, busy = false, onAccept }: {
  repository: string;
  standing: WorkflowIntegration;
  busy?: boolean;
  onAccept: (request: IntegrationRequest, answered: Answered) => void;
}) {
  const { t } = useTranslation();
  const [refused, setRefused] = useState<string | null>(null);
  const tip = standing.tip ?? null;
  const waits = waitsOf(standing.state);
  const kept = standing.gates !== null && (standing.state === 'accepted' || standing.state === 'changed');

  let said: string;
  if (standing.state === 'accepted') {
    said = standing.gates === 'none' ? t('settings.integration.acceptedNone', { branch: tip?.line ?? '' }) : t('settings.integration.accepted');
  } else if (standing.state === 'changed') {
    said = t('settings.integration.changed');
  } else if (standing.state === 'unaccepted') {
    said = t('settings.integration.unaccepted');
  } else {
    said = standing.problem ?? t('settings.integration.unread');
  }

  const press = (request: IntegrationRequest) => {
    setRefused(null);
    onAccept(request, { done: () => undefined, refused: setRefused });
  };

  return (
    <div className="grid gap-2" data-repository={repository}>
      <p className="m-0 text-small text-ink-soft"><Inline text={said} /></p>
      {waits && standing.change && <DeclarationChangeWords change={standing.change} />}
      {waits && standing.declaration && tip && (
        <details className="m-0 min-w-0 text-small">
          <summary className="cursor-pointer text-ink-soft">{t('settings.integration.readFile')}</summary>
          <pre
            aria-label={t('settings.integration.declaration', { branch: tip.line })}
            className="mt-1 max-h-48 overflow-auto rounded-control border border-line bg-raised px-2 py-1 font-mono text-meta text-ink"
          >
            {standing.declaration}
          </pre>
        </details>
      )}
      <div className="flex flex-wrap items-center gap-1.5">
        {waits && tip && (
          <Button variant="ghost" className="min-h-0 border border-line px-2.5 py-0.5 text-small" disabled={busy} onClick={() => press({ gates: tip.gates })}>
            {t('settings.integration.accept')}
          </Button>
        )}
        {kept && (
          <Button variant="ghost" className="min-h-0 border border-line px-2.5 py-0.5 text-small" disabled={busy} onClick={() => press({ clear: true })}>
            {t('settings.integration.withdraw')}
          </Button>
        )}
      </div>
      {refused && <Refused sentence={refused} />}
    </div>
  );
}

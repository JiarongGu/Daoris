import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { Button, Inline, SettingRow } from '../../design/ui';
import { type Answered, Refused } from '../../work/InlineConfirm';
import type { WorkflowIntegration } from '../../workflow/current';

/** What the person's press sends: the blob of the declaration they read, or a withdrawal. */
export type IntegrationRequest = { gates: string } | { clear: true };

const short = (id: string) => id.slice(0, 7);

/**
 * **A repository's integration declaration** (INTEGRATE1d part 3; the integration design §11 row d, §17.7), on its Setup beside the landed
 * route: where the person's acceptance of the `daoris.gates.json` `integration` object stands against the line's tip, the declaration as
 * written where it waits, and *Accept* and *Withdraw*.
 *
 * @remarks
 * The acceptance is the person's alone, and its own apart from the landed route's gates (D156; D157 point 10). *Accept* sends the blob of
 * the declaration shown, never one the person did not read: when the tip moved, the driver refuses `moved` in its sentence, said under
 * the press, and the start node asked again shows the declaration now there. The declaration is authored content and is shown as
 * written. The page judges nothing.
 *
 * A molecule: every state is reached by its props, and the press goes out.
 */
export function IntegrationDeclaration({ repository, integration, busy = false, onAccept }: {
  repository: string;
  integration: WorkflowIntegration;
  busy?: boolean;
  onAccept: (request: IntegrationRequest, answered: Answered) => void;
}) {
  const { t } = useTranslation();
  const [refused, setRefused] = useState<string | null>(null);
  const tip = integration.tip ?? null;
  const at = tip ? { branch: tip.line, commit: short(tip.commit) } : null;
  const waits = integration.state === 'changed' || integration.state === 'unaccepted';
  const kept = integration.gates !== null && (integration.state === 'accepted' || integration.state === 'changed');

  let said: string;
  if (integration.state === 'accepted' && at) {
    said = integration.gates === 'none'
      ? t('settings.integration.acceptedNone', at)
      : t('settings.integration.accepted', { ...at, blob: short(integration.gates ?? '') });
  } else if (integration.state === 'changed') {
    said = t('settings.integration.changed');
  } else if (integration.state === 'unaccepted') {
    said = t('settings.integration.unaccepted');
  } else {
    said = integration.problem ?? t('settings.integration.unread');
  }

  const press = (request: IntegrationRequest) => {
    setRefused(null);
    onAccept(request, { done: () => undefined, refused: setRefused });
  };

  return (
    <SettingRow label={t('settings.integration.label')} why={t('settings.integration.why')}>
      <div className="grid gap-2" data-repository={repository}>
        <p className="m-0 text-small text-ink-soft"><Inline text={said} /></p>
        {waits && integration.declaration && at && (
          <pre
            aria-label={t('settings.integration.declaration', at)}
            className="m-0 max-h-48 overflow-auto rounded-control border border-line bg-raised px-2 py-1 font-mono text-meta text-ink"
          >
            {integration.declaration}
          </pre>
        )}
        <div className="flex flex-wrap items-center gap-1.5">
          {waits && tip && (
            <Button
              variant="ghost"
              className="min-h-0 border border-line px-2.5 py-0.5 text-small"
              disabled={busy}
              onClick={() => press({ gates: tip.gates })}
            >
              {t('settings.integration.accept')}
            </Button>
          )}
          {kept && (
            <Button
              variant="ghost"
              className="min-h-0 border border-line px-2.5 py-0.5 text-small"
              disabled={busy}
              onClick={() => press({ clear: true })}
            >
              {t('settings.integration.withdraw')}
            </Button>
          )}
        </div>
        {refused && <Refused sentence={refused} />}
      </div>
    </SettingRow>
  );
}
